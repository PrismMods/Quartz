using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Features.KeyViewer.Layout;
internal enum KvEmbeddedImageRejectionReason {
    Missing,
    Invalid,
    OverBudget,
    MalformedDestination,
}
internal readonly struct KvEmbeddedImageWarning {
    private const int MaxSourceIdCharacters = 96;
    internal string SourceId { get; }
    internal KvEmbeddedImageRejectionReason Reason { get; }
    internal string Message => "Embedded image " + JsonConvert.SerializeObject(SafeSourceId(SourceId))
        + " was skipped: " + ReasonText + ".";
    internal KvEmbeddedImageWarning(string sourceId, KvEmbeddedImageRejectionReason reason) {
        SourceId = sourceId ?? "";
        Reason = reason;
    }
    private string ReasonText => Reason switch {
        KvEmbeddedImageRejectionReason.Missing => "its payload is missing",
        KvEmbeddedImageRejectionReason.Invalid => "its payload is invalid or too large",
        KvEmbeddedImageRejectionReason.OverBudget => "the embedded-image storage budget is exhausted",
        KvEmbeddedImageRejectionReason.MalformedDestination => "the current embedded-image table is malformed",
        _ => "it could not be imported",
    };
    private static string SafeSourceId(string value) {
        if(string.IsNullOrEmpty(value)) return "(empty)";
        int length = Math.Min(value.Length, MaxSourceIdCharacters);
        System.Text.StringBuilder safe = new(length + (value.Length > length ? 1 : 0));
        for(int i = 0; i < length; i++) {
            char c = value[i];
            UnicodeCategory category = char.GetUnicodeCategory(c);
            safe.Append(category is UnicodeCategory.Control
                or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator
                or UnicodeCategory.ParagraphSeparator
                or UnicodeCategory.Surrogate ? '?' : c);
        }
        if(value.Length > length) safe.Append('…');
        return safe.ToString();
    }
}
internal sealed partial class KvDocument {
    internal bool TryEmbeddedImage(string reference, out string dataBase64, out string extension) {
        return TryEmbeddedImage(reference, out dataBase64, out extension, out _);
    }
    internal bool TryEmbeddedImage(string reference, out string dataBase64, out string extension,
        out object contentScope) {
        dataBase64 = null;
        extension = null;
        contentScope = null;
        if(!TryImageId(reference, out string id) || Root["embeddedLocalImages"] is not JArray images)
            return false;
        foreach(JToken token in images) {
            if(token is not JObject image
                || !string.Equals(image["imageId"]?.ToString(), id, StringComparison.Ordinal)) continue;
            JToken dataToken = image["dataBase64"];
            string data = dataToken?.ToString();
            if(string.IsNullOrWhiteSpace(data)) return false;
            dataBase64 = data;
            extension = image["extension"]?.ToString();
            contentScope = dataToken;
            return true;
        }
        return false;
    }
    private static HashSet<string> ReferencedEmbeddedImages(KvDocument document, IEnumerable<string> sourceTabs) {
        HashSet<string> result = new(StringComparer.Ordinal);
        foreach(string tab in sourceTabs)
            foreach((_, KvElementKind kind) in Tables)
                foreach(KvElement element in document.Elements(tab, kind))
                    foreach(string field in EmbeddedImageFields)
                        if(TryImageId(element.Raw[field]?.ToString(), out string id)) result.Add(id);
        return result;
    }
    private Dictionary<string, string> MergeEmbeddedImages(
        KvDocument other, HashSet<string> referenced, long budgetBytes,
        List<KvEmbeddedImageWarning> warnings
    ) {
        Dictionary<string, string> remap = new(StringComparer.Ordinal);
        if(referenced.Count == 0) return remap;
        JToken destinationToken = Root["embeddedLocalImages"];
        if(destinationToken != null
            && destinationToken.Type != JTokenType.Null
            && destinationToken is not JArray) {
            AddWarnings(referenced, KvEmbeddedImageRejectionReason.MalformedDestination, warnings);
            return remap;
        }
        if(other.Root["embeddedLocalImages"] is not JArray source || source.Count == 0) {
            AddWarnings(referenced, KvEmbeddedImageRejectionReason.Missing, warnings);
            return remap;
        }
        JArray destination = destinationToken as JArray;
        Dictionary<string, JObject> existing = new(StringComparer.Ordinal);
        Dictionary<string, KvEmbeddedImageRejectionReason> rejections = new(StringComparer.Ordinal);
        long usedBytes = 0;
        if(destination != null) {
            foreach(JToken token in destination) {
                usedBytes = SaturatingAdd(usedBytes, StoredTokenBytes(token));
                if(token is JObject image && image["imageId"]?.ToString() is { Length: > 0 } id)
                    existing.TryAdd(id, image);
            }
        }
        foreach(JToken token in source) {
            if(token is not JObject sourceImage
                || sourceImage["imageId"]?.ToString() is not { Length: > 0 } sourceId
                || !referenced.Contains(sourceId)
                || remap.ContainsKey(sourceId)) continue;
            string data = sourceImage["dataBase64"]?.ToString();
            if(!IsValidBase64(data)) {
                rejections.TryAdd(sourceId, KvEmbeddedImageRejectionReason.Invalid);
                continue;
            }
            string destinationId = sourceId;
            if(existing.TryGetValue(sourceId, out JObject sameId)) {
                if(string.Equals(
                    sameId.ToString(Formatting.None), sourceImage.ToString(Formatting.None),
                    StringComparison.Ordinal
                )) {
                    remap[sourceId] = sourceId;
                    rejections.Remove(sourceId);
                    continue;
                }
                do destinationId = Guid.NewGuid().ToString();
                while(existing.ContainsKey(destinationId));
            }
            JObject copy = (JObject)sourceImage.DeepClone();
            copy["imageId"] = destinationId;
            long copyBytes = StoredTokenBytes(copy);
            if(copyBytes > budgetBytes || usedBytes > budgetBytes - copyBytes) {
                rejections[sourceId] = KvEmbeddedImageRejectionReason.OverBudget;
                continue;
            }
            if(destination == null) Root["embeddedLocalImages"] = destination = [];
            destination.Add(copy);
            existing[destinationId] = copy;
            usedBytes += copyBytes;
            remap[sourceId] = destinationId;
            rejections.Remove(sourceId);
        }
        foreach(string sourceId in referenced) {
            if(remap.ContainsKey(sourceId)) continue;
            KvEmbeddedImageRejectionReason reason = rejections.TryGetValue(sourceId, out var rejection)
                ? rejection
                : KvEmbeddedImageRejectionReason.Missing;
            warnings.Add(new KvEmbeddedImageWarning(sourceId, reason));
        }
        return remap;
    }
    private static void AddWarnings(IEnumerable<string> sourceIds,
        KvEmbeddedImageRejectionReason reason, List<KvEmbeddedImageWarning> warnings) {
        foreach(string sourceId in sourceIds) warnings.Add(new KvEmbeddedImageWarning(sourceId, reason));
    }
    private static void RemapEmbeddedImageRefs(JObject position, Dictionary<string, string> remap) {
        if(position == null) return;
        foreach(string field in EmbeddedImageFields) {
            if(!TryImageId(position[field]?.ToString(), out string sourceId)) continue;
            if(remap.TryGetValue(sourceId, out string destinationId))
                position[field] = DmLocalImagePrefix + destinationId;
            else position.Remove(field);
        }
    }
    internal static long StoredTokenBytes(JToken token) {
        try { return System.Text.Encoding.UTF8.GetByteCount(token.ToString(Formatting.None)); }
        catch(Exception e) { Diag.Ignore(e); return long.MaxValue; }
    }
    internal static long SaturatingAdd(long left, long right) =>
        right >= long.MaxValue - left ? long.MaxValue : left + right;
    private static bool IsValidBase64(string value) {
        if(string.IsNullOrWhiteSpace(value) || value.Length > KvImageSafety.MaxBase64Characters) return false;
        long chars = 0;
        int padding = 0;
        bool sawPadding = false;
        foreach(char c in value) {
            if(char.IsWhiteSpace(c)) continue;
            if(c == '=') {
                sawPadding = true;
                if(++padding > 2) return false;
            } else {
                bool valid = c is >= 'A' and <= 'Z'
                    || c is >= 'a' and <= 'z'
                    || c is >= '0' and <= '9'
                    || c is '+' or '/';
                if(sawPadding || !valid) return false;
            }
            chars++;
        }
        long decodedBytes = chars / 4 * 3 - padding;
        return chars > 0 && chars % 4 == 0 && decodedBytes <= KvImageSafety.MaxEncodedBytes;
    }
    internal static bool TryImageId(string reference, out string id) {
        id = null;
        if(string.IsNullOrWhiteSpace(reference)) return false;
        string value = reference.Trim();
        if(!value.StartsWith(DmLocalImagePrefix, StringComparison.OrdinalIgnoreCase)) return false;
        id = value.Substring(DmLocalImagePrefix.Length);
        return id.Length > 0;
    }
}
