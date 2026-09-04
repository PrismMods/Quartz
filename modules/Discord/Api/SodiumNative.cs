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
        if(plainLength < 0 || plainLength > (plain?.Length ?? 0)) return null;
        byte[] cipher = new byte[plainLength + TagBytes];
        int written = EncryptInto(
            plain, 0, plainLength,
            additional, 0, additional?.Length ?? 0,
            nonce, key,
            cipher, 0
        );
        if(written < 0) return null;
        if(written != cipher.Length) Array.Resize(ref cipher, written);
        return cipher;
    }
    public static byte[] Decrypt(byte[] cipher, int cipherLength, byte[] additional, byte[] nonce, byte[] key) {
        if(cipherLength < TagBytes || cipherLength > (cipher?.Length ?? 0)) return null;
        byte[] plain = new byte[cipherLength - TagBytes];
        int written = DecryptInto(
            cipher, 0, cipherLength,
            additional, 0, additional?.Length ?? 0,
            nonce, key,
            plain, 0
        );
        if(written < 0) return null;
        if(written != plain.Length) Array.Resize(ref plain, written);
        return plain;
    }
    internal static unsafe int EncryptInto(
        byte[] plain, int plainOffset, int plainLength,
        byte[] additional, int additionalOffset, int additionalLength,
        byte[] nonce, byte[] key,
        byte[] output, int outputOffset
    ) {
        if(!SliceFits(plain, plainOffset, plainLength)
            || !SliceFits(additional, additionalOffset, additionalLength)
            || nonce == null || nonce.Length != NonceBytes
            || key == null || key.Length != KeyBytes
            || !SliceFits(output, outputOffset, plainLength + TagBytes)) return -1;
        fixed(byte* plainStart = plain)
        fixed(byte* additionalStart = additional)
        fixed(byte* nonceStart = nonce)
        fixed(byte* keyStart = key)
        fixed(byte* outputStart = output) {
            byte* message = plainLength == 0 ? null : plainStart + plainOffset;
            byte* ad = additionalLength == 0 ? null : additionalStart + additionalOffset;
            int result = encrypt(
                (IntPtr)(outputStart + outputOffset), out ulong written,
                (IntPtr)message, (ulong)plainLength,
                (IntPtr)ad, (ulong)additionalLength,
                IntPtr.Zero, (IntPtr)nonceStart, (IntPtr)keyStart
            );
            return result == 0 && written <= int.MaxValue ? (int)written : -1;
        }
    }
    internal static unsafe int DecryptInto(
        byte[] cipher, int cipherOffset, int cipherLength,
        byte[] additional, int additionalOffset, int additionalLength,
        byte[] nonce, byte[] key,
        byte[] output, int outputOffset
    ) {
        if(cipherLength < TagBytes
            || !SliceFits(cipher, cipherOffset, cipherLength)
            || !SliceFits(additional, additionalOffset, additionalLength)
            || nonce == null || nonce.Length != NonceBytes
            || key == null || key.Length != KeyBytes
            || !SliceFits(output, outputOffset, cipherLength - TagBytes)) return -1;
        fixed(byte* cipherStart = cipher)
        fixed(byte* additionalStart = additional)
        fixed(byte* nonceStart = nonce)
        fixed(byte* keyStart = key)
        fixed(byte* outputStart = output) {
            byte* ad = additionalLength == 0 ? null : additionalStart + additionalOffset;
            int result = decrypt(
                (IntPtr)(outputStart + outputOffset), out ulong written,
                IntPtr.Zero,
                (IntPtr)(cipherStart + cipherOffset), (ulong)cipherLength,
                (IntPtr)ad, (ulong)additionalLength,
                (IntPtr)nonceStart, (IntPtr)keyStart
            );
            return result == 0 && written <= int.MaxValue ? (int)written : -1;
        }
    }
    private static bool SliceFits(byte[] buffer, int offset, int length) =>
        buffer != null && offset >= 0 && length >= 0 && offset <= buffer.Length - length;
}
