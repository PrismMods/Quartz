#nullable enable
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
namespace Quartz.Features.Minecraft;
public static class McTar {
    public const int MaxEntries = 20_000;
    private const int BlockSize = 512;
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
    private static extern int Chmod(string path, uint mode);
    public static int ExtractGz(string archivePath, string destination) {
        string root = Path.GetFullPath(destination);
        Directory.CreateDirectory(root);
        string prefix = root.EndsWith(Path.DirectorySeparatorChar.ToString())
            ? root : root + Path.DirectorySeparatorChar;
        using FileStream file = File.OpenRead(archivePath);
        using GZipStream gzip = new(file, CompressionMode.Decompress);
        byte[] header = new byte[BlockSize];
        int written = 0;
        int emptyBlocks = 0;
        while(true) {
            if(!ReadExactly(gzip, header, BlockSize)) break;
            if(IsAllZero(header)) {
                if(++emptyBlocks >= 2) break;
                continue;
            }
            emptyBlocks = 0;
            if(++written > MaxEntries) throw new InvalidDataException("Archive contains too many entries.");
            string name = ReadString(header, 0, 100);
            string namePrefix = ReadString(header, 345, 155);
            string entry = namePrefix.Length == 0 ? name : namePrefix + "/" + name;
            long size = ReadOctal(header, 124, 12);
            uint mode = (uint)ReadOctal(header, 100, 8);
            char typeFlag = (char)header[156];
            if(size < 0) throw new InvalidDataException("Archive declares a negative entry size.");
            long padded = (size + BlockSize - 1) / BlockSize * BlockSize;
            if(typeFlag == '5' || entry.EndsWith('/')) {
                Directory.CreateDirectory(SafeTarget(root, prefix, entry));
                Skip(gzip, padded);
                continue;
            }
            if(typeFlag != '0' && typeFlag != '\0') throw new InvalidDataException($"Archive contains an unsupported entry type '{typeFlag}'.");
            string target = SafeTarget(root, prefix, entry);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using(FileStream output = new(target, FileMode.Create, FileAccess.Write, FileShare.None))
                CopyBounded(gzip, output, size);
            Skip(gzip, padded - size);
            if(!IsWindows && (mode & 0b001_001_001) != 0) Chmod(target, mode & 0xFFF);
        }
        return written;
    }
    private static string SafeTarget(string root, string prefix, string entry) {
        string relative = entry.Replace('\\', '/').TrimStart('/');
        if(relative.Length == 0) throw new InvalidDataException("Archive contains an empty entry name.");
        string target = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if(!target.StartsWith(prefix, StringComparison.Ordinal) && target != root.TrimEnd(Path.DirectorySeparatorChar))
            throw new InvalidDataException("Archive path escapes the destination folder.");
        return target;
    }
    private static bool IsAllZero(byte[] block) {
        for(int i = 0; i < block.Length; i++) if(block[i] != 0) return false;
        return true;
    }
    private static string ReadString(byte[] block, int offset, int length) {
        int end = offset;
        int limit = offset + length;
        while(end < limit && block[end] != 0) end++;
        return Encoding.UTF8.GetString(block, offset, end - offset);
    }
    private static long ReadOctal(byte[] block, int offset, int length) {
        long value = 0;
        for(int i = offset; i < offset + length; i++) {
            byte c = block[i];
            if(c == 0 || c == (byte)' ') continue;
            if(c < (byte)'0' || c > (byte)'7') throw new InvalidDataException("Archive header contains a malformed octal field.");
            value = (value << 3) + (c - (byte)'0');
        }
        return value;
    }
    private static bool ReadExactly(Stream stream, byte[] buffer, int count) {
        int read = 0;
        while(read < count) {
            int n = stream.Read(buffer, read, count - read);
            if(n <= 0) return false;
            read += n;
        }
        return true;
    }
    private static void CopyBounded(Stream input, Stream output, long size) {
        byte[] buffer = new byte[81920];
        long remaining = size;
        while(remaining > 0) {
            int want = (int)Math.Min(buffer.Length, remaining);
            int n = input.Read(buffer, 0, want);
            if(n <= 0) throw new InvalidDataException("Archive ended before the declared entry size.");
            output.Write(buffer, 0, n);
            remaining -= n;
        }
    }
    private static void Skip(Stream stream, long count) {
        byte[] buffer = new byte[81920];
        long remaining = count;
        while(remaining > 0) {
            int want = (int)Math.Min(buffer.Length, remaining);
            int n = stream.Read(buffer, 0, want);
            if(n <= 0) return;
            remaining -= n;
        }
    }
}
