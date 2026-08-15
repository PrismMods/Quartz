using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Features.KeyViewer.Layout;
internal static class KvExportEmbedding {
    private static readonly string[] PositionTables = [
        "keyPositions", "statPositions", "graphPositions", "knobPositions",
    ];
    private static readonly string[] ImageFields = ["inactiveImage", "activeImage"];
    internal static List<string> EmbedLocalImages(JObject root) {
        List<string> notes = [];
        if(root == null) return notes;
        JToken imagesToken = root["embeddedLocalImages"];
        if(imagesToken != null && imagesToken.Type != JTokenType.Null && imagesToken is not JArray) {
            notes.Add("The embedded-image table is malformed; local images were left as file paths.");
            return notes;
        }
        JArray images = imagesToken as JArray;
        Dictionary<string, string> byData = new(StringComparer.Ordinal);
        Dictionary<string, string> byPath = new(StringComparer.Ordinal);
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> referenced = new(StringComparer.Ordinal);
        long used = 0;
        if(images != null) {
            foreach(JToken token in images) {
                used = KvDocument.SaturatingAdd(used, KvDocument.StoredTokenBytes(token));
                if(token is not JObject image
                    || image["imageId"]?.ToString() is not { Length: > 0 } id) continue;
                ids.Add(id);
                if(image["dataBase64"]?.ToString() is { Length: > 0 } data) byData.TryAdd(data, id);
            }
        }
        int embedded = 0;
        string Embed(string path) {
            if(byPath.TryGetValue(path, out string known)) return known;
            byte[] bytes;
            try {
                FileInfo info = new(path);
                if(!info.Exists) {
                    notes.Add("Image file not found; left as a path in the export: " + path);
                    return null;
                }
                if(info.Length <= 0 || info.Length > KvImageSafety.MaxEncodedBytes) {
                    notes.Add("Image too large to embed (max "
                        + KvImageSafety.MaxEncodedBytes / (1024 * 1024)
                        + " MB); left as a path: " + path);
                    return null;
                }
                bytes = File.ReadAllBytes(path);
            } catch(Exception e) {
                notes.Add("Could not read image " + path + ": " + e.Message);
                return null;
            }
            if(!KvImageSafety.TryIdentify(bytes, out _)) {
                notes.Add("Not an embeddable image; left as a path: " + path);
                return null;
            }
            string data = Convert.ToBase64String(bytes);
            if(byData.TryGetValue(data, out string sameData)) {
                byPath[path] = sameData;
                return sameData;
            }
            string id = Guid.NewGuid().ToString();
            while(ids.Contains(id)) id = Guid.NewGuid().ToString();
            string extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            JObject entry = new() {
                ["imageId"] = id,
                ["extension"] = extension.Length > 0 ? extension : "png",
                ["dataBase64"] = data,
            };
            long entryBytes = KvDocument.StoredTokenBytes(entry);
            if(entryBytes > KvDocument.MaxEmbeddedImageStorageBytes
                || used > KvDocument.MaxEmbeddedImageStorageBytes - entryBytes) {
                notes.Add("Embedded-image storage budget exhausted; left as a path: " + path);
                return null;
            }
            if(images == null) root["embeddedLocalImages"] = images = [];
            images.Add(entry);
            used += entryBytes;
            ids.Add(id);
            byData[data] = id;
            byPath[path] = id;
            return id;
        }
        foreach(JObject element in ElementObjects(root)) {
            foreach(string field in ImageFields) {
                string value = element[field]?.ToString()?.Trim() ?? "";
                if(value.Length == 0) continue;
                if(KvDocument.TryImageId(value, out string existingId)) {
                    referenced.Add(existingId);
                    continue;
                }
                if(!TryLocalPath(value, out string path)) continue;
                string id = Embed(path);
                if(id == null) continue;
                element[field] = KvDocument.DmLocalImagePrefix + id;
                referenced.Add(id);
                embedded++;
            }
        }
        if(images != null) {
            for(int i = images.Count - 1; i >= 0; i--) {
                string id = (images[i] as JObject)?["imageId"]?.ToString();
                if(string.IsNullOrEmpty(id) || !referenced.Contains(id)) images.RemoveAt(i);
            }
            if(images.Count == 0) root.Remove("embeddedLocalImages");
        }
        if(embedded > 0) notes.Add("Embedded " + embedded + " local image reference(s) into the export.");
        return notes;
    }
    private static bool TryLocalPath(string value, out string path) {
        path = null;
        if(value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;
        if(value.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) {
            try {
                path = new Uri(value).LocalPath;
                return true;
            } catch(Exception e) {
                Diag.Ignore(e);
                return false;
            }
        }
        if(value.Length > 1 && (value[0] == '/' || value.StartsWith("\\\\", StringComparison.Ordinal)
            || (char.IsLetter(value[0]) && value[1] == ':'))) {
            path = value;
            return true;
        }
        return false;
    }
    private static IEnumerable<JObject> ElementObjects(JObject root) {
        foreach(string table in PositionTables) {
            if(root[table] is not JObject byTab) continue;
            foreach(JProperty tab in byTab.Properties()) {
                if(tab.Value is not JArray arr) continue;
                foreach(JToken entry in arr) {
                    if(entry is not JObject outer) continue;
                    yield return outer;
                    if(outer["position"] is JObject inner) yield return inner;
                }
            }
        }
    }
}
