using System.Security.Cryptography;
using System.Text;
using Quartz.Features.Discord;
using static Asserts;
static class DiscordCryptoTests {
    public static void TestSpkiMatchesTheFrameworkEncoder() {
        for(int i = 0; i < 8; i++) {
            using RSA rsa = RSA.Create(2048);
            byte[] mine = Der.SubjectPublicKeyInfo(rsa.ExportParameters(false));
            byte[] theirs = rsa.ExportSubjectPublicKeyInfo();
            Assert(
                mine.Length == theirs.Length,
                $"SPKI length {mine.Length} != framework {theirs.Length}"
            );
            for(int b = 0; b < mine.Length; b++)
                Assert(mine[b] == theirs[b], $"SPKI byte {b} differs: {mine[b]:X2} vs {theirs[b]:X2}");
        }
    }
    public static void TestSpkiRoundTripsThroughTheFrameworkParser() {
        using RSA source = RSA.Create(2048);
        using RSA target = RSA.Create();
        target.ImportSubjectPublicKeyInfo(Der.SubjectPublicKeyInfo(source.ExportParameters(false)), out int read);
        RSAParameters expected = source.ExportParameters(false);
        RSAParameters actual = target.ExportParameters(false);
        Assert(read > 0, "the framework parser consumed no bytes");
        Assert(Same(expected.Modulus, actual.Modulus), "modulus survived the round trip");
        Assert(Same(expected.Exponent, actual.Exponent), "exponent survived the round trip");
    }
    public static void TestSpkiHandlesASmallExponent() {
        RSAParameters parameters = new() {
            Modulus = [0x00, 0xFF, 0x01],
            Exponent = [0x03],
        };
        byte[] der = Der.SubjectPublicKeyInfo(parameters);
        Assert(der[0] == 0x30, "SPKI must be a DER SEQUENCE");
        Assert(der.Length > 20, "SPKI was implausibly short");
    }
    public static void TestTokenBoxRoundTrips() {
        byte[] keys = TokenBox.NewKeyMaterial();
        const string token = "mfa.notarealtoken_0123456789";
        Assert(TokenBox.Unprotect(keys, TokenBox.Protect(keys, token)) == token, "token survived protect/unprotect");
    }
    public static void TestTokenBoxRejectsTamperingAndWrongKeys() {
        byte[] keys = TokenBox.NewKeyMaterial();
        byte[] blob = TokenBox.Protect(keys, "sensitive");
        byte[] tampered = (byte[])blob.Clone();
        tampered[^1] ^= 0xFF;
        Assert(TokenBox.Unprotect(keys, tampered) == null, "a flipped ciphertext bit must fail the MAC");
        Assert(TokenBox.Unprotect(TokenBox.NewKeyMaterial(), blob) == null, "a wrong key must not decrypt");
        Assert(TokenBox.Unprotect(keys, [1, 2, 3]) == null, "a truncated blob must be rejected");
        Assert(TokenBox.Unprotect(keys, null) == null, "a null blob must be rejected");
    }
    public static void TestTokenBoxUsesAFreshIvEachTime() {
        byte[] keys = TokenBox.NewKeyMaterial();
        byte[] first = TokenBox.Protect(keys, "same");
        byte[] second = TokenBox.Protect(keys, "same");
        Assert(!Same(first, second), "identical tokens must not produce identical blobs");
    }
    public static void TestBase64UrlHasNoPaddingOrUnsafeCharacters() {
        for(int length = 1; length <= 8; length++) {
            byte[] bytes = Encoding.UTF8.GetBytes(new string('\xff', length));
            string encoded = Der.Base64Url(bytes);
            Assert(!encoded.Contains('='), "base64url must not be padded");
            Assert(!encoded.Contains('+') && !encoded.Contains('/'), "base64url must not contain + or /");
        }
    }
    static bool Same(byte[] a, byte[] b) {
        if(a == null || b == null || a.Length != b.Length) return false;
        for(int i = 0; i < a.Length; i++)
            if(a[i] != b[i]) return false;
        return true;
    }
}
static class DiscordOaepTests {
    public static void TestManagedUnwrapMatchesTheFrameworkWrap() {
        using RSA rsa = RSA.Create(2048);
        foreach(string message in new[] { "a", "quartz-remote-auth-probe", new string('x', 190) }) {
            byte[] plaintext = Encoding.UTF8.GetBytes(message);
            byte[] ciphertext = rsa.Encrypt(plaintext, RSAEncryptionPadding.OaepSHA256);
            byte[] opened = RsaOaep.DecryptSha256Managed(rsa, ciphertext);
            Assert(
                Encoding.UTF8.GetString(opened) == message,
                $"the managed OAEP unwrap must reproduce the framework's plaintext ({message.Length} bytes)"
            );
        }
    }
    public static void TestManagedWrapIsReadableByTheFramework() {
        using RSA rsa = RSA.Create(2048);
        byte[] plaintext = Encoding.UTF8.GetBytes("round trip through the framework");
        byte[] ciphertext = RsaOaep.EncryptSha256(rsa, plaintext);
        byte[] opened = rsa.Decrypt(ciphertext, RSAEncryptionPadding.OaepSHA256);
        Assert(
            Encoding.UTF8.GetString(opened) == "round trip through the framework",
            "the framework must be able to open what the managed wrap produced"
        );
    }
    public static void TestManagedRoundTripsAgainstItself() {
        using RSA rsa = RSA.Create(2048);
        byte[] plaintext = Encoding.UTF8.GetBytes("self round trip");
        byte[] opened = RsaOaep.DecryptSha256Managed(rsa, RsaOaep.EncryptSha256(rsa, plaintext));
        Assert(Encoding.UTF8.GetString(opened) == "self round trip", "managed wrap and unwrap must agree");
    }
    public static void TestEmptyMessageSurvives() {
        using RSA rsa = RSA.Create(2048);
        byte[] ciphertext = rsa.Encrypt([], RSAEncryptionPadding.OaepSHA256);
        Assert(RsaOaep.DecryptSha256Managed(rsa, ciphertext).Length == 0, "an empty payload must decode to empty");
    }
    public static void TestTamperedCiphertextIsRejected() {
        using RSA rsa = RSA.Create(2048);
        byte[] ciphertext = rsa.Encrypt(Encoding.UTF8.GetBytes("secret"), RSAEncryptionPadding.OaepSHA256);
        ciphertext[^1] ^= 0xFF;
        Assert(Throws(() => RsaOaep.DecryptSha256Managed(rsa, ciphertext)), "a tampered ciphertext must be rejected");
    }
    public static void TestWrongKeyIsRejected() {
        using RSA sender = RSA.Create(2048);
        using RSA other = RSA.Create(2048);
        byte[] ciphertext = sender.Encrypt(Encoding.UTF8.GetBytes("secret"), RSAEncryptionPadding.OaepSHA256);
        Assert(Throws(() => RsaOaep.DecryptSha256Managed(other, ciphertext)), "the wrong key must not decode");
    }
    public static void TestOversizedCiphertextIsRejected() {
        using RSA rsa = RSA.Create(2048);
        Assert(Throws(() => RsaOaep.DecryptSha256Managed(rsa, new byte[257])), "an oversized ciphertext must be rejected");
    }
    public static void TestCreate2048ReturnsATrue2048BitKey() {
        using RSA rsa = RsaKeys.Create2048();
        Assert(
            RsaKeys.Bits(rsa) == RsaKeys.RequiredBits,
            "Create2048 must return a key whose exported modulus is actually 2048 bits"
        );
        Assert(
            Der.SubjectPublicKeyInfo(rsa.ExportParameters(false)).Length == 294,
            "an RSA-2048 SPKI is 294 bytes; a shorter one means the runtime downgraded the key"
        );
    }
    static bool Throws(Action action) {
        try {
            action();
            return false;
        } catch(CryptographicException) {
            return true;
        }
    }
}
