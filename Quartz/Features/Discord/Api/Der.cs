using System.Security.Cryptography;
namespace Quartz.Features.Discord;
public static class Der {
    private static readonly byte[] RsaEncryptionOid = [0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01];
    private static readonly byte[] DerNull = [0x05, 0x00];
    public static byte[] SubjectPublicKeyInfo(RSAParameters parameters) {
        byte[] rsaPublicKey = Sequence(Concat(Integer(parameters.Modulus), Integer(parameters.Exponent)));
        byte[] algorithm = Sequence(Concat(RsaEncryptionOid, DerNull));
        byte[] subjectPublicKey = Tlv(0x03, Concat([0x00], rsaPublicKey));
        return Sequence(Concat(algorithm, subjectPublicKey));
    }
    public static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    private static byte[] Sequence(byte[] content) => Tlv(0x30, content);
    private static byte[] Integer(byte[] magnitude) {
        int start = 0;
        while(start < magnitude.Length - 1 && magnitude[start] == 0) start++;
        int length = magnitude.Length - start;
        bool pad = (magnitude[start] & 0x80) != 0;
        byte[] content = new byte[pad ? length + 1 : length];
        Buffer.BlockCopy(magnitude, start, content, pad ? 1 : 0, length);
        return Tlv(0x02, content);
    }
    private static byte[] Tlv(byte tag, byte[] content) {
        byte[] length = Length(content.Length);
        byte[] result = new byte[1 + length.Length + content.Length];
        result[0] = tag;
        Buffer.BlockCopy(length, 0, result, 1, length.Length);
        Buffer.BlockCopy(content, 0, result, 1 + length.Length, content.Length);
        return result;
    }
    private static byte[] Length(int value) {
        if(value < 0x80) return [(byte)value];
        int bytes = 1;
        for(int probe = value; probe > 0xFF; probe >>= 8) bytes++;
        byte[] result = new byte[bytes + 1];
        result[0] = (byte)(0x80 | bytes);
        for(int i = 0; i < bytes; i++) result[bytes - i] = (byte)(value >> (8 * i));
        return result;
    }
    private static byte[] Concat(params byte[][] parts) {
        int total = 0;
        foreach(byte[] part in parts) total += part.Length;
        byte[] result = new byte[total];
        int offset = 0;
        foreach(byte[] part in parts) {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }
        return result;
    }
}
