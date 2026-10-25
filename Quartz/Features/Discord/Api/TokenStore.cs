using System.Runtime.InteropServices;
using Quartz.Core;
namespace Quartz.Features.Discord.Api;
public static class TokenStore {
    private static string Dir => MainCore.Paths.RootPath;
    private static string CredentialsFile => Path.Combine(Dir, "Discord.credentials");
    private static string KeyFile => Path.Combine(Dir, "Discord.master.key");
    public static bool HasSaved => File.Exists(CredentialsFile) && File.Exists(KeyFile);
    public static void Save(string token) {
        Directory.CreateDirectory(Dir);
        WritePrivate(CredentialsFile, TokenBox.Protect(LoadOrCreateKeys(), token));
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
            if(existing.Length == TokenBox.KeyMaterialSize) {
                // older builds wrote the key world-readable
                Restrict(KeyFile);
                return existing;
            }
        }
        byte[] keys = TokenBox.NewKeyMaterial();
        WritePrivate(KeyFile, keys);
        return keys;
    }
    // shortcut: on Windows the key sits beside the ciphertext with default ACLs (obfuscation only; netstandard2.1 has no ProtectedData), wrap it with DPAPI if System.Security.dll gets referenced.
    private static void WritePrivate(string path, byte[] bytes) {
        File.WriteAllBytes(path, bytes);
        Restrict(path);
    }
    private static void Restrict(string path) {
        // 0x180 == 0600: owner read/write only
        if(!NativeLib.IsWindows && Chmod(path, 0x180) != 0)
            MainCore.Log.Wrn($"[Discord] could not restrict permissions on {Path.GetFileName(path)}");
    }
    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
    private static extern int Chmod(string path, uint mode);
}
