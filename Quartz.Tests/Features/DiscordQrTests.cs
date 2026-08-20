using System.Text;
using Quartz.Features.Discord;
using static Asserts;
static class DiscordQrTests {
    public static void TestReedSolomonRemainderIsDivisibleByTheGenerator() {
        byte[] data = new byte[19];
        for(int i = 0; i < data.Length; i++) data[i] = (byte)((i * 37) + 5);
        byte[] ec = QrCode.ReedSolomon(data, 7);
        byte[] full = new byte[data.Length + ec.Length];
        Array.Copy(data, full, data.Length);
        Array.Copy(ec, 0, full, data.Length, ec.Length);
        byte[] remainder = QrCode.ReedSolomon(full, 7);
        foreach(byte b in remainder)
            Assert(b == 0, "a codeword block plus its EC must divide evenly by the generator polynomial");
    }
    public static void TestVersionGrowsWithLength() {
        Assert(QrCode.Version(new string('a', 17)) == 1, "17 bytes fits version 1");
        Assert(QrCode.Version(new string('a', 18)) == 2, "18 bytes needs version 2");
        Assert(QrCode.Version(new string('a', 53)) == 3, "53 bytes fits version 3");
        Assert(QrCode.Version(new string('a', 54)) == 4, "54 bytes needs version 4");
    }
    public static void TestMatrixStructure() {
        bool[,] matrix = QrCode.Encode("https://discord.com/ra/" + new string('A', 43));
        int size = matrix.GetLength(0);
        Assert(size == 33, $"a 66-byte payload should be version 4 (33 modules), got {size}");
        foreach((int x, int y) in new[] { (0, 0), (size - 7, 0), (0, size - 7) }) {
            Assert(matrix[x + 0, y + 0] && matrix[x + 6, y + 0], "finder pattern outer ring");
            Assert(!matrix[x + 1, y + 1] && !matrix[x + 5, y + 1], "finder pattern inner gap");
            Assert(matrix[x + 3, y + 3], "finder pattern centre");
        }
        for(int i = 8; i < size - 8; i++) {
            Assert(matrix[i, 6] == (i % 2 == 0), "horizontal timing pattern must alternate");
            Assert(matrix[6, i] == (i % 2 == 0), "vertical timing pattern must alternate");
        }
        Assert(matrix[8, size - 8], "the dark module must be set");
    }
    public static void TestRoundTripsThroughADecoder() {
        string[] samples = [
            "a",
            "https://discord.com/ra/abcdef0123456789",
            "https://discord.com/ra/" + new string('Z', 43),
            new string('x', 100),
            "mixed 123 !@#$%^&*() text",
        ];
        foreach(string sample in samples) {
            string decoded = Decode(QrCode.Encode(sample));
            Assert(decoded == sample, $"round trip failed: expected '{sample}', decoded '{decoded}'");
        }
    }
    public static void TestEveryLengthUpToTheVersionLimitRoundTrips() {
        for(int length = 1; length <= 120; length++) {
            string sample = new('q', length);
            Assert(Decode(QrCode.Encode(sample)) == sample, $"round trip failed at length {length}");
        }
    }
    static string Decode(bool[,] matrix) {
        int size = matrix.GetLength(0);
        int version = (size - 17) / 4;
        bool[,] reserved = QrCode.FunctionMap(version);
        int format = 0;
        for(int i = 0; i < 15; i++) {
            bool on;
            if(i < 6) on = matrix[8, i];
            else if(i == 6) on = matrix[8, 7];
            else if(i == 7) on = matrix[8, 8];
            else if(i == 8) on = matrix[7, 8];
            else on = matrix[14 - i, 8];
            if(on) format |= 1 << i;
        }
        format ^= 0x5412;
        int mask = (format >> 10) & 0x7;
        bool[,] plain = (bool[,])matrix.Clone();
        for(int y = 0; y < size; y++)
            for(int x = 0; x < size; x++)
                if(!reserved[x, y] && MaskAt(mask, x, y)) plain[x, y] = !plain[x, y];
        List<bool> bits = [];
        bool upward = true;
        for(int right = size - 1; right >= 1; right -= 2) {
            if(right == 6) right = 5;
            for(int step = 0; step < size; step++) {
                int y = upward ? size - 1 - step : step;
                for(int column = 0; column < 2; column++) {
                    int x = right - column;
                    if(reserved[x, y]) continue;
                    bits.Add(plain[x, y]);
                }
            }
            upward = !upward;
        }
        int total = QrCode.TotalFor(version);
        byte[] stream = new byte[total];
        for(int i = 0; i < total; i++) {
            int value = 0;
            for(int bit = 0; bit < 8; bit++) value = (value << 1) | (bits[(i * 8) + bit] ? 1 : 0);
            stream[i] = (byte)value;
        }
        int blocks = QrCode.BlocksFor(version);
        int ecCount = QrCode.EcFor(version);
        int dataTotal = total - (ecCount * blocks);
        int shortLength = dataTotal / blocks;
        int longBlocks = dataTotal % blocks;
        byte[][] dataBlocks = new byte[blocks][];
        for(int b = 0; b < blocks; b++)
            dataBlocks[b] = new byte[shortLength + (b >= blocks - longBlocks ? 1 : 0)];
        int index = 0;
        int longest = shortLength + (longBlocks > 0 ? 1 : 0);
        for(int i = 0; i < longest; i++)
            for(int b = 0; b < blocks; b++)
                if(i < dataBlocks[b].Length) dataBlocks[b][i] = stream[index++];
        List<byte> data = [];
        foreach(byte[] block in dataBlocks) data.AddRange(block);
        List<bool> dataBits = [];
        foreach(byte b in data)
            for(int bit = 7; bit >= 0; bit--) dataBits.Add(((b >> bit) & 1) != 0);
        int cursor = 0;
        int mode = Read(dataBits, ref cursor, 4);
        Assert(mode == 4, $"expected byte mode, got mode {mode}");
        int length = Read(dataBits, ref cursor, version >= 10 ? 16 : 8);
        byte[] payload = new byte[length];
        for(int i = 0; i < length; i++) payload[i] = (byte)Read(dataBits, ref cursor, 8);
        return Encoding.UTF8.GetString(payload);
    }
    static int Read(List<bool> bits, ref int cursor, int count) {
        int value = 0;
        for(int i = 0; i < count; i++) value = (value << 1) | (bits[cursor++] ? 1 : 0);
        return value;
    }
    static bool MaskAt(int mask, int x, int y) => mask switch {
        0 => (x + y) % 2 == 0,
        1 => y % 2 == 0,
        2 => x % 3 == 0,
        3 => (x + y) % 3 == 0,
        4 => ((y / 2) + (x / 3)) % 2 == 0,
        5 => ((x * y) % 2) + ((x * y) % 3) == 0,
        6 => ((((x * y) % 2) + ((x * y) % 3)) % 2) == 0,
        _ => ((((x + y) % 2) + ((x * y) % 3)) % 2) == 0,
    };
}
