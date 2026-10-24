using System.Security.Cryptography;
using System.Text;
namespace Quartz.Features.Discord;
public static class TokenBox {
    public const int KeySize = 32;
    public const int KeyMaterialSize = KeySize * 2;
    private const int MacSize = 32;
    private const int IvSize = 16;
    public static byte[] Protect(byte[] keyMaterial, string token) {
        if(keyMaterial == null || keyMaterial.Length != KeyMaterialSize)
            throw new ArgumentException("key material must be " + KeyMaterialSize + " bytes", nameof(keyMaterial));
        byte[] iv = Random(IvSize);
        byte[] ciphertext;
        using(Aes aes = NewAes(Slice(keyMaterial, 0, KeySize), iv)) {
            using ICryptoTransform encryptor = aes.CreateEncryptor();
            byte[] plaintext = Encoding.UTF8.GetBytes(token);
            ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
        }
        byte[] signed = new byte[IvSize + ciphertext.Length];
        Buffer.BlockCopy(iv, 0, signed, 0, IvSize);
        Buffer.BlockCopy(ciphertext, 0, signed, IvSize, ciphertext.Length);
        byte[] mac = Mac(keyMaterial, signed);
        byte[] blob = new byte[MacSize + signed.Length];
        Buffer.BlockCopy(mac, 0, blob, 0, MacSize);
        Buffer.BlockCopy(signed, 0, blob, MacSize, signed.Length);
        return blob;
    }
    public static string Unprotect(byte[] keyMaterial, byte[] blob) {
        if(keyMaterial == null || keyMaterial.Length != KeyMaterialSize) return null;
        if(blob == null || blob.Length < MacSize + IvSize + 1) return null;
        byte[] signed = Slice(blob, MacSize, blob.Length - MacSize);
        if(!FixedTimeEquals(Slice(blob, 0, MacSize), Mac(keyMaterial, signed))) return null;
        byte[] iv = Slice(signed, 0, IvSize);
        byte[] ciphertext = Slice(signed, IvSize, signed.Length - IvSize);
        if(ciphertext.Length == 0 || ciphertext.Length % IvSize != 0) return null;
        using Aes aes = NewAes(Slice(keyMaterial, 0, KeySize), iv);
        using ICryptoTransform decryptor = aes.CreateDecryptor();
        byte[] plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        return Encoding.UTF8.GetString(plaintext);
    }
    public static byte[] NewKeyMaterial() => Random(KeyMaterialSize);
    private static Aes NewAes(byte[] key, byte[] iv) {
        Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        return aes;
    }
    private static byte[] Mac(byte[] keyMaterial, byte[] content) {
        using HMACSHA256 hmac = new(Slice(keyMaterial, KeySize, KeySize));
        return hmac.ComputeHash(content);
    }
    private static byte[] Random(int size) {
        byte[] bytes = new byte[size];
        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return bytes;
    }
    private static byte[] Slice(byte[] source, int offset, int length) {
        byte[] result = new byte[length];
        Buffer.BlockCopy(source, offset, result, 0, length);
        return result;
    }
    private static bool FixedTimeEquals(byte[] a, byte[] b) {
        if(a.Length != b.Length) return false;
        int difference = 0;
        for(int i = 0; i < a.Length; i++) difference |= a[i] ^ b[i];
        return difference == 0;
    }
}
