using Quartz.Core;
namespace Quartz.Interop;
public interface IImportHandler {
    int Apply(ImportSource source);
    void Refresh();
}
public static class ImportRegistry {
    private static readonly List<(string OwnerId, IImportHandler Handler)> handlers = [];
    public static void Register(string ownerId, IImportHandler handler) {
        if(handler == null) throw new ArgumentNullException(nameof(handler));
        handlers.Add((ownerId ?? "", handler));
    }
    public static void Unregister(string ownerId) => handlers.RemoveAll(entry => entry.OwnerId == (ownerId ?? ""));
    public static void UnregisterHandler(IImportHandler handler) => handlers.RemoveAll(entry => entry.Handler == handler);
    public static int Deliver(ImportSource source) {
        if(source == null) return 0;
        int count = 0;
        foreach((string ownerId, IImportHandler handler) in handlers.ToArray()) {
            try {
                count += handler.Apply(source);
            } catch(Exception e) {
                MainCore.Log.Wrn($"[Import] '{ownerId}' could not read {source.Kind} settings: {e.Message}");
            }
        }
        return count;
    }
    public static void RefreshAll() {
        foreach((string ownerId, IImportHandler handler) in handlers.ToArray()) {
            try {
                handler.Refresh();
            } catch(Exception e) {
                MainCore.Log.Wrn($"[Import] '{ownerId}' refresh step failed: {e.Message}");
            }
        }
    }
}
