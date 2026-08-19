using Quartz.Core;
namespace Quartz.Features.Discord;
public static class TokenStore {
    private static string Dir => MainCore.Paths.RootPath;
    private static string CredentialsFile => Path.Combine(Dir, "Discord.credentials");
    private static string KeyFile => Path.Combine(Dir, "Discord.master.key");
    public static bool HasSaved => File.Exists(CredentialsFile) && File.Exists(KeyFile);
    public static void Save(string token) {
        Directory.CreateDirectory(Dir);
        File.WriteAllBytes(CredentialsFile, TokenBox.Protect(LoadOrCreateKeys(), token));
    }
    public static string Load() {
        try {
            if(!HasSaved) return null;
            return TokenBox.Unprotect(File.ReadAllBytes(KeyFile), File.ReadAllBytes(CredentialsFile));
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    public static void Clear() {
        Delete(CredentialsFile);
        Delete(KeyFile);
    }
    private static void Delete(string path) {
        try {
            if(File.Exists(path)) File.Delete(path);
        } catch(Exception e) {
            Diag.Ignore(e);
        }
    }
    private static byte[] LoadOrCreateKeys() {
        if(File.Exists(KeyFile)) {
            byte[] existing = File.ReadAllBytes(KeyFile);
            if(existing.Length == TokenBox.KeyMaterialSize) return existing;
        }
        byte[] keys = TokenBox.NewKeyMaterial();
        File.WriteAllBytes(KeyFile, keys);
        return keys;
    }
}
