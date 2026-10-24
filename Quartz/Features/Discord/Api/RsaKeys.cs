using System.Security.Cryptography;
using Quartz.Core;
namespace Quartz.Features.Discord;
public static class RsaKeys {
    public const int RequiredBits = 2048;
    public static RSA Create2048() {
        RSA rsa = RSA.Create();
        try {
            rsa.KeySize = RequiredBits;
        } catch(Exception e) {
            Diag.Ignore(e);
        }
        if(Bits(rsa) == RequiredBits) return rsa;
        rsa.Dispose();
        RSA fallback = new RSACryptoServiceProvider(RequiredBits);
        if(Bits(fallback) == RequiredBits) return fallback;
        fallback.Dispose();
        throw new CryptographicException(
            "this runtime would not produce an RSA-" + RequiredBits + " key");
    }
    public static int Bits(RSA rsa) {
        try {
            byte[] modulus = rsa.ExportParameters(false).Modulus;
            return modulus == null ? 0 : modulus.Length * 8;
        } catch(Exception e) {
            Diag.Ignore(e);
            return 0;
        }
    }
}
