#nullable enable
using VoltRpc.IO;
using VoltRpc.Types;
using VoltstroStudios.UnityWebBrowser.Shared.Events;
namespace Quartz.Features.Minecraft;
internal sealed class McPixelsReader : TypeReadWriter<PixelsEvent> {
    private readonly object gate = new();
    private byte[] buffer = [];
    private bool dirty;
    public int Length {
        get { lock(gate) return buffer.Length; }
    }
    public override void Write(BufferedWriter writer, PixelsEvent value)
        => throw new NotSupportedException("Quartz never sends pixels to the engine.");
    public override PixelsEvent Read(BufferedReader reader) {
        int size = reader.ReadInt();
        if(size <= 0) return default;
        ReadOnlySpan<byte> data = reader.ReadBytesSpanSlice(size);
        lock(gate) {
            if(buffer.Length != size) buffer = new byte[size];
            data.CopyTo(buffer);
            dirty = true;
        }
        return default;
    }
    // Hands the live buffer to the caller under the lock rather than copying it out:
    // the frame is already copied once off the wire, and a second staging copy per
    // frame is pure cost at 720p.
    public bool TryApply(Action<byte[]> upload) {
        lock(gate) {
            if(!dirty || buffer.Length == 0) return false;
            upload(buffer);
            dirty = false;
            return true;
        }
    }
}
