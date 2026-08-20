using System.Runtime.InteropServices;
namespace Quartz.Features.Discord;
public static class SodiumNative {
    public const int KeyBytes = 32;
    public const int NonceBytes = 24;
    public const int TagBytes = 16;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int InitFn();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EncryptFn(
        IntPtr cipher, out ulong cipherLength, IntPtr message, ulong messageLength,
        IntPtr additional, ulong additionalLength, IntPtr nsec, IntPtr nonce, IntPtr key);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DecryptFn(
        IntPtr message, out ulong messageLength, IntPtr nsec, IntPtr cipher, ulong cipherLength,
        IntPtr additional, ulong additionalLength, IntPtr nonce, IntPtr key);
    private static InitFn init;
    private static EncryptFn encrypt;
    private static DecryptFn decrypt;
    public static bool Available { get; private set; }
    public static string LoadError { get; private set; } = "not loaded";
    public static bool Load() {
        if(Available) return true;
        string path = VoiceNatives.Locate("sodium");
        if(path == null) {
            LoadError = "libsodium is not installed";
            return false;
        }
        IntPtr library = NativeLib.Load(path);
        if(library == IntPtr.Zero) {
            LoadError = "the operating system refused to load " + Path.GetFileName(path);
            return false;
        }
        init = NativeLib.Bind<InitFn>(library, "sodium_init");
        encrypt = NativeLib.Bind<EncryptFn>(library, "crypto_aead_xchacha20poly1305_ietf_encrypt");
        decrypt = NativeLib.Bind<DecryptFn>(library, "crypto_aead_xchacha20poly1305_ietf_decrypt");
        if(encrypt == null || decrypt == null) {
            LoadError = "libsodium is missing the xchacha20-poly1305 entry points";
            return false;
        }
        if(init != null && init() < 0) {
            LoadError = "sodium_init failed";
            return false;
        }
        Available = true;
        LoadError = "";
        return true;
    }
    public static byte[] Encrypt(byte[] plain, int plainLength, byte[] additional, byte[] nonce, byte[] key) {
        byte[] cipher = new byte[plainLength + TagBytes];
        IntPtr cipherBuffer = Marshal.AllocHGlobal(cipher.Length);
        IntPtr plainBuffer = Marshal.AllocHGlobal(Math.Max(1, plainLength));
        IntPtr adBuffer = Marshal.AllocHGlobal(Math.Max(1, additional.Length));
        IntPtr nonceBuffer = Marshal.AllocHGlobal(nonce.Length);
        IntPtr keyBuffer = Marshal.AllocHGlobal(key.Length);
        try {
            Marshal.Copy(plain, 0, plainBuffer, plainLength);
            Marshal.Copy(additional, 0, adBuffer, additional.Length);
            Marshal.Copy(nonce, 0, nonceBuffer, nonce.Length);
            Marshal.Copy(key, 0, keyBuffer, key.Length);
            int result = encrypt(
                cipherBuffer, out ulong written, plainBuffer, (ulong)plainLength,
                adBuffer, (ulong)additional.Length, IntPtr.Zero, nonceBuffer, keyBuffer);
            if(result != 0) return null;
            int length = (int)written;
            byte[] output = new byte[length];
            Marshal.Copy(cipherBuffer, output, 0, length);
            return output;
        } finally {
            Marshal.FreeHGlobal(cipherBuffer);
            Marshal.FreeHGlobal(plainBuffer);
            Marshal.FreeHGlobal(adBuffer);
            Marshal.FreeHGlobal(nonceBuffer);
            Marshal.FreeHGlobal(keyBuffer);
        }
    }
    public static byte[] Decrypt(byte[] cipher, int cipherLength, byte[] additional, byte[] nonce, byte[] key) {
        if(cipherLength < TagBytes) return null;
        IntPtr plainBuffer = Marshal.AllocHGlobal(cipherLength);
        IntPtr cipherBuffer = Marshal.AllocHGlobal(cipherLength);
        IntPtr adBuffer = Marshal.AllocHGlobal(Math.Max(1, additional.Length));
        IntPtr nonceBuffer = Marshal.AllocHGlobal(nonce.Length);
        IntPtr keyBuffer = Marshal.AllocHGlobal(key.Length);
        try {
            Marshal.Copy(cipher, 0, cipherBuffer, cipherLength);
            Marshal.Copy(additional, 0, adBuffer, additional.Length);
            Marshal.Copy(nonce, 0, nonceBuffer, nonce.Length);
            Marshal.Copy(key, 0, keyBuffer, key.Length);
            int result = decrypt(
                plainBuffer, out ulong written, IntPtr.Zero, cipherBuffer, (ulong)cipherLength,
                adBuffer, (ulong)additional.Length, nonceBuffer, keyBuffer);
            if(result != 0) return null;
            int length = (int)written;
            byte[] output = new byte[length];
            Marshal.Copy(plainBuffer, output, 0, length);
            return output;
        } finally {
            Marshal.FreeHGlobal(plainBuffer);
            Marshal.FreeHGlobal(cipherBuffer);
            Marshal.FreeHGlobal(adBuffer);
            Marshal.FreeHGlobal(nonceBuffer);
            Marshal.FreeHGlobal(keyBuffer);
        }
    }
}
