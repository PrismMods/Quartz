using System.Numerics;
using System.Security.Cryptography;
using Quartz.Core;
namespace Quartz.Features.Discord;
public static class RsaOaep {
    private const int HashLength = 32;
    public static byte[] DecryptSha256(RSA rsa, byte[] ciphertext) {
        try {
            return rsa.Decrypt(ciphertext, RSAEncryptionPadding.OaepSHA256);
        } catch(CryptographicException e) {
            Diag.Ignore(e);
        } catch(NotSupportedException e) {
            Diag.Ignore(e);
        }
        return DecryptSha256Managed(rsa, ciphertext);
    }
    public static byte[] DecryptSha256Managed(RSA rsa, byte[] ciphertext) {
        if(rsa == null) throw new ArgumentNullException(nameof(rsa));
        if(ciphertext == null) throw new ArgumentNullException(nameof(ciphertext));
        RSAParameters parameters = rsa.ExportParameters(true);
        if(parameters.D == null || parameters.Modulus == null)
            throw new CryptographicException("the private key is not exportable");
        int size = parameters.Modulus.Length;
        if(ciphertext.Length > size) throw new CryptographicException("ciphertext is longer than the modulus");
        BigInteger modulus = Positive(parameters.Modulus);
        BigInteger value = Positive(ciphertext);
        if(value >= modulus) throw new CryptographicException("ciphertext is not less than the modulus");
        BigInteger plain = BigInteger.ModPow(value, Positive(parameters.D), modulus);
        return Unpad(ToFixed(plain, size));
    }
    public static byte[] EncryptSha256(RSA rsa, byte[] message) {
        RSAParameters parameters = rsa.ExportParameters(false);
        if(parameters.Modulus == null || parameters.Exponent == null)
            throw new CryptographicException("the public key is not exportable");
        int size = parameters.Modulus.Length;
        if(message.Length > size - (2 * HashLength) - 2)
            throw new CryptographicException("message is too long for this key");
        byte[] db = new byte[size - HashLength - 1];
        byte[] labelHash = Sha256([]);
        Buffer.BlockCopy(labelHash, 0, db, 0, HashLength);
        db[db.Length - message.Length - 1] = 0x01;
        Buffer.BlockCopy(message, 0, db, db.Length - message.Length, message.Length);
        byte[] seed = new byte[HashLength];
        using(RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(seed);
        byte[] maskedDb = Xor(db, Mgf1(seed, db.Length));
        byte[] maskedSeed = Xor(seed, Mgf1(maskedDb, HashLength));
        byte[] em = new byte[size];
        Buffer.BlockCopy(maskedSeed, 0, em, 1, HashLength);
        Buffer.BlockCopy(maskedDb, 0, em, 1 + HashLength, maskedDb.Length);
        BigInteger cipher = BigInteger.ModPow(Positive(em), Positive(parameters.Exponent), Positive(parameters.Modulus));
        return ToFixed(cipher, size);
    }
    private static byte[] Unpad(byte[] em) {
        bool bad = em.Length < (2 * HashLength) + 2;
        if(bad) throw new CryptographicException("OAEP decoding failed");
        bad |= em[0] != 0x00;
        byte[] maskedSeed = Slice(em, 1, HashLength);
        byte[] maskedDb = Slice(em, 1 + HashLength, em.Length - HashLength - 1);
        byte[] seed = Xor(maskedSeed, Mgf1(maskedDb, HashLength));
        byte[] db = Xor(maskedDb, Mgf1(seed, maskedDb.Length));
        byte[] expected = Sha256([]);
        for(int i = 0; i < HashLength; i++) bad |= db[i] != expected[i];
        int index = HashLength;
        while(index < db.Length && db[index] == 0x00) index++;
        bad |= index >= db.Length || db[index] != 0x01;
        if(bad) throw new CryptographicException("OAEP decoding failed");
        return Slice(db, index + 1, db.Length - index - 1);
    }
    private static byte[] Mgf1(byte[] seed, int length) {
        byte[] output = new byte[length];
        byte[] block = new byte[seed.Length + 4];
        Buffer.BlockCopy(seed, 0, block, 0, seed.Length);
        int written = 0;
        for(uint counter = 0; written < length; counter++) {
            block[seed.Length] = (byte)(counter >> 24);
            block[seed.Length + 1] = (byte)(counter >> 16);
            block[seed.Length + 2] = (byte)(counter >> 8);
            block[seed.Length + 3] = (byte)counter;
            byte[] digest = Sha256(block);
            int take = Math.Min(digest.Length, length - written);
            Buffer.BlockCopy(digest, 0, output, written, take);
            written += take;
        }
        return output;
    }
    private static byte[] Sha256(byte[] value) {
        using SHA256 sha = SHA256.Create();
        return sha.ComputeHash(value);
    }
    private static byte[] Xor(byte[] a, byte[] b) {
        byte[] result = new byte[a.Length];
        for(int i = 0; i < a.Length; i++) result[i] = (byte)(a[i] ^ b[i]);
        return result;
    }
    private static BigInteger Positive(byte[] bigEndian) {
        byte[] little = new byte[bigEndian.Length + 1];
        for(int i = 0; i < bigEndian.Length; i++) little[i] = bigEndian[bigEndian.Length - 1 - i];
        return new BigInteger(little);
    }
    private static byte[] ToFixed(BigInteger value, int size) {
        byte[] little = value.ToByteArray();
        byte[] result = new byte[size];
        int copy = Math.Min(size, little.Length);
        for(int i = 0; i < copy; i++) {
            byte b = little[i];
            int target = size - 1 - i;
            if(target < 0) break;
            result[target] = b;
        }
        for(int i = size; i < little.Length; i++)
            if(little[i] != 0) throw new CryptographicException("plaintext is longer than the modulus");
        return result;
    }
    private static byte[] Slice(byte[] source, int offset, int length) {
        byte[] result = new byte[length];
        Buffer.BlockCopy(source, offset, result, 0, length);
        return result;
    }
}
