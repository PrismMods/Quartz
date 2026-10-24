using System.Text;
namespace Quartz.Features.Discord;
public static class QrCode {
    private static readonly int[] CapacityL = [0, 17, 32, 53, 78, 106, 134, 154, 192, 230, 271];
    private static readonly int[] TotalCodewords = [0, 26, 44, 70, 100, 134, 172, 196, 242, 292, 346];
    private static readonly int[] EcPerBlock = [0, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18];
    private static readonly int[] Blocks = [0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4];
    private static readonly int[] RemainderBits = [0, 0, 7, 7, 7, 7, 7, 0, 0, 0, 0];
    private static readonly int[][] Alignment = [
        [], [], [6, 18], [6, 22], [6, 26], [6, 30], [6, 34],
        [6, 22, 38], [6, 24, 42], [6, 26, 46], [6, 28, 50],
    ];
    public static int Version(string text) {
        int length = Encoding.UTF8.GetByteCount(text);
        for(int version = 1; version <= 10; version++)
            if(length <= CapacityL[version]) return version;
        throw new ArgumentException("text is too long for a version-10 QR code", nameof(text));
    }
    public static bool[,] Encode(string text) {
        int version = Version(text);
        byte[] codewords = Codewords(Encoding.UTF8.GetBytes(text), version);
        int size = (version * 4) + 17;
        bool[,] reserved = new bool[size, size];
        bool[,] matrix = new bool[size, size];
        DrawFunctionPatterns(matrix, reserved, version);
        PlaceData(matrix, reserved, codewords, version);
        int mask = BestMask(matrix, reserved, version);
        ApplyMask(matrix, reserved, mask);
        DrawFormat(matrix, mask);
        return matrix;
    }
    public static bool[,] FunctionMap(int version) {
        int size = (version * 4) + 17;
        bool[,] matrix = new bool[size, size];
        bool[,] reserved = new bool[size, size];
        DrawFunctionPatterns(matrix, reserved, version);
        return reserved;
    }
    public static int BlocksFor(int version) => Blocks[version];
    public static int EcFor(int version) => EcPerBlock[version];
    public static int TotalFor(int version) => TotalCodewords[version];
    private static byte[] Codewords(byte[] data, int version) {
        int total = TotalCodewords[version];
        int blocks = Blocks[version];
        int ecCount = EcPerBlock[version];
        int dataTotal = total - (ecCount * blocks);
        List<bool> bits = [];
        Write(bits, 4, 4);
        Write(bits, data.Length, version >= 10 ? 16 : 8);
        foreach(byte b in data) Write(bits, b, 8);
        int capacity = dataTotal * 8;
        for(int i = 0; i < 4 && bits.Count < capacity; i++) bits.Add(false);
        while(bits.Count % 8 != 0) bits.Add(false);
        byte[] pad = [0xEC, 0x11];
        for(int i = 0; bits.Count < capacity; i++) Write(bits, pad[i % 2], 8);
        byte[] dataCodewords = new byte[dataTotal];
        for(int i = 0; i < dataTotal; i++) {
            int value = 0;
            for(int bit = 0; bit < 8; bit++) value = (value << 1) | (bits[(i * 8) + bit] ? 1 : 0);
            dataCodewords[i] = (byte)value;
        }
        int shortLength = dataTotal / blocks;
        int longBlocks = dataTotal % blocks;
        List<byte[]> dataBlocks = [];
        List<byte[]> ecBlocks = [];
        int offset = 0;
        for(int b = 0; b < blocks; b++) {
            int length = shortLength + (b >= blocks - longBlocks ? 1 : 0);
            byte[] block = new byte[length];
            Array.Copy(dataCodewords, offset, block, 0, length);
            offset += length;
            dataBlocks.Add(block);
            ecBlocks.Add(ReedSolomon(block, ecCount));
        }
        List<byte> result = [];
        int longest = 0;
        foreach(byte[] block in dataBlocks) longest = Math.Max(longest, block.Length);
        for(int i = 0; i < longest; i++)
            foreach(byte[] block in dataBlocks)
                if(i < block.Length) result.Add(block[i]);
        for(int i = 0; i < ecCount; i++)
            foreach(byte[] block in ecBlocks) result.Add(block[i]);
        return [.. result];
    }
    private static void Write(List<bool> bits, int value, int count) {
        for(int i = count - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
    }
    public static byte[] ReedSolomon(byte[] data, int ecCount) {
        byte[] generator = Generator(ecCount);
        byte[] remainder = new byte[ecCount];
        foreach(byte value in data) {
            byte factor = (byte)(value ^ remainder[0]);
            Array.Copy(remainder, 1, remainder, 0, ecCount - 1);
            remainder[ecCount - 1] = 0;
            for(int i = 0; i < ecCount; i++) remainder[i] ^= Multiply(generator[i], factor);
        }
        return remainder;
    }
    private static byte[] Generator(int degree) {
        byte[] result = new byte[degree];
        result[degree - 1] = 1;
        byte root = 1;
        for(int i = 0; i < degree; i++) {
            for(int j = 0; j < degree; j++) {
                result[j] = Multiply(result[j], root);
                if(j + 1 < degree) result[j] ^= result[j + 1];
            }
            root = Multiply(root, 2);
        }
        return result;
    }
    private static byte Multiply(byte a, byte b) {
        int result = 0;
        for(int i = 7; i >= 0; i--) {
            result = (result << 1) ^ ((result >> 7) * 0x11D);
            result ^= ((b >> i) & 1) * a;
        }
        return (byte)result;
    }
    private static void DrawFunctionPatterns(bool[,] matrix, bool[,] reserved, int version) {
        int size = matrix.GetLength(0);
        Finder(matrix, reserved, 0, 0);
        Finder(matrix, reserved, size - 7, 0);
        Finder(matrix, reserved, 0, size - 7);
        for(int i = 8; i < size - 8; i++) {
            bool on = i % 2 == 0;
            Set(matrix, reserved, i, 6, on);
            Set(matrix, reserved, 6, i, on);
        }
        int[] centers = Alignment[version];
        foreach(int x in centers)
            foreach(int y in centers) {
                if((x == 6 && y == 6) || (x == 6 && y == size - 7) || (x == size - 7 && y == 6)) continue;
                Alignment5(matrix, reserved, x, y);
            }
        Set(matrix, reserved, 8, size - 8, true);
        for(int i = 0; i < 9; i++) {
            Reserve(reserved, i, 8);
            Reserve(reserved, 8, i);
        }
        for(int i = 0; i < 8; i++) {
            Reserve(reserved, size - 1 - i, 8);
            Reserve(reserved, 8, size - 1 - i);
        }
        if(version < 7) return;
        int info = VersionInfo(version);
        for(int i = 0; i < 18; i++) {
            bool on = ((info >> i) & 1) != 0;
            int x = i / 3;
            int y = size - 11 + (i % 3);
            Set(matrix, reserved, x, y, on);
            Set(matrix, reserved, y, x, on);
        }
    }
    private static void Finder(bool[,] matrix, bool[,] reserved, int left, int top) {
        for(int dy = -1; dy <= 7; dy++)
            for(int dx = -1; dx <= 7; dx++) {
                int x = left + dx;
                int y = top + dy;
                if(x < 0 || y < 0 || x >= matrix.GetLength(0) || y >= matrix.GetLength(0)) continue;
                bool on = dx >= 0 && dx <= 6 && dy >= 0 && dy <= 6
                    && (dx == 0 || dx == 6 || dy == 0 || dy == 6 || (dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4));
                Set(matrix, reserved, x, y, on);
            }
    }
    private static void Alignment5(bool[,] matrix, bool[,] reserved, int cx, int cy) {
        for(int dy = -2; dy <= 2; dy++)
            for(int dx = -2; dx <= 2; dx++)
                Set(matrix, reserved, cx + dx, cy + dy,
                    Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
    }
    private static void Set(bool[,] matrix, bool[,] reserved, int x, int y, bool on) {
        matrix[x, y] = on;
        reserved[x, y] = true;
    }
    private static void Reserve(bool[,] reserved, int x, int y) => reserved[x, y] = true;
    private static void PlaceData(bool[,] matrix, bool[,] reserved, byte[] codewords, int version) {
        int size = matrix.GetLength(0);
        int bitCount = (codewords.Length * 8) + RemainderBits[version];
        int index = 0;
        bool upward = true;
        for(int right = size - 1; right >= 1; right -= 2) {
            if(right == 6) right = 5;
            for(int step = 0; step < size; step++) {
                int y = upward ? size - 1 - step : step;
                for(int column = 0; column < 2; column++) {
                    int x = right - column;
                    if(reserved[x, y]) continue;
                    bool on = index < codewords.Length * 8
                        && ((codewords[index / 8] >> (7 - (index % 8))) & 1) != 0;
                    if(index < bitCount) matrix[x, y] = on;
                    index++;
                }
            }
            upward = !upward;
        }
    }
    private static bool MaskAt(int mask, int x, int y) => mask switch {
        0 => (x + y) % 2 == 0,
        1 => y % 2 == 0,
        2 => x % 3 == 0,
        3 => (x + y) % 3 == 0,
        4 => ((y / 2) + (x / 3)) % 2 == 0,
        5 => ((x * y) % 2) + ((x * y) % 3) == 0,
        6 => ((((x * y) % 2) + ((x * y) % 3)) % 2) == 0,
        _ => ((((x + y) % 2) + ((x * y) % 3)) % 2) == 0,
    };
    private static void ApplyMask(bool[,] matrix, bool[,] reserved, int mask) {
        int size = matrix.GetLength(0);
        for(int y = 0; y < size; y++)
            for(int x = 0; x < size; x++)
                if(!reserved[x, y] && MaskAt(mask, x, y)) matrix[x, y] = !matrix[x, y];
    }
    private static int BestMask(bool[,] matrix, bool[,] reserved, int mask) {
        int best = 0;
        int bestPenalty = int.MaxValue;
        for(int candidate = 0; candidate < 8; candidate++) {
            ApplyMask(matrix, reserved, candidate);
            DrawFormat(matrix, candidate);
            int penalty = Penalty(matrix);
            ApplyMask(matrix, reserved, candidate);
            if(penalty >= bestPenalty) continue;
            bestPenalty = penalty;
            best = candidate;
        }
        return best;
    }
    private static void DrawFormat(bool[,] matrix, int mask) {
        int size = matrix.GetLength(0);
        int format = FormatInfo(mask);
        for(int i = 0; i < 15; i++) {
            bool on = ((format >> i) & 1) != 0;
            if(i < 6) matrix[8, i] = on;
            else if(i == 6) matrix[8, 7] = on;
            else if(i == 7) matrix[8, 8] = on;
            else if(i == 8) matrix[7, 8] = on;
            else matrix[14 - i, 8] = on;
            if(i < 8) matrix[size - 1 - i, 8] = on;
            else matrix[8, size - 15 + i] = on;
        }
    }
    private static int FormatInfo(int mask) {
        int value = (1 << 3) | mask;
        int remainder = value;
        for(int i = 0; i < 10; i++) remainder = (remainder << 1) ^ ((remainder >> 9) * 0x537);
        return ((value << 10) | (remainder & 0x3FF)) ^ 0x5412;
    }
    private static int VersionInfo(int version) {
        int remainder = version;
        for(int i = 0; i < 12; i++) remainder = (remainder << 1) ^ ((remainder >> 11) * 0x1F25);
        return (version << 12) | (remainder & 0xFFF);
    }
    private static int Penalty(bool[,] matrix) {
        int size = matrix.GetLength(0);
        int penalty = 0;
        int dark = 0;
        for(int y = 0; y < size; y++)
            for(int x = 0; x < size; x++)
                if(matrix[x, y]) dark++;
        for(int y = 0; y < size; y++) {
            int runRow = 1;
            int runColumn = 1;
            for(int x = 1; x < size; x++) {
                runRow = matrix[x, y] == matrix[x - 1, y] ? runRow + 1 : 1;
                if(runRow == 5) penalty += 3;
                else if(runRow > 5) penalty++;
                runColumn = matrix[y, x] == matrix[y, x - 1] ? runColumn + 1 : 1;
                if(runColumn == 5) penalty += 3;
                else if(runColumn > 5) penalty++;
            }
        }
        for(int y = 0; y < size - 1; y++)
            for(int x = 0; x < size - 1; x++)
                if(matrix[x, y] == matrix[x + 1, y]
                    && matrix[x, y] == matrix[x, y + 1]
                    && matrix[x, y] == matrix[x + 1, y + 1]) penalty += 3;
        int percent = dark * 100 / (size * size);
        penalty += Math.Abs(percent - 50) / 5 * 10;
        return penalty;
    }
}
