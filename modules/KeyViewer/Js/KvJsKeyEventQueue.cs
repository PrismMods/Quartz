using System.Threading;

namespace Quartz.Features.KeyViewer.Js;

internal sealed class KvJsKeyEventQueue {
    internal const int Capacity = 2048;
    private const int Mask = Capacity - 1;

    internal readonly struct Event {
        internal readonly int Key;
        internal readonly bool Down;
        internal readonly int Generation;
        internal Event(int key, bool down, int generation) {
            Key = key;
            Down = down;
            Generation = generation;
        }
    }

    private struct Slot {
        internal int Sequence;
        internal int Key;
        internal bool Down;
        internal int Generation;
    }

    private readonly Slot[] ring = new Slot[Capacity];
    private int writeIndex;
    private int readIndex;
    private int readShared;
    private int overflowed;

    internal bool TryEnqueue(int key, bool down, int generation = 0) {
        int write = writeIndex;
        if(write - Volatile.Read(ref readShared) >= Capacity) {
            Volatile.Write(ref overflowed, 1);
            return false;
        }
        int slot = write & Mask;
        ring[slot].Key = key;
        ring[slot].Down = down;
        ring[slot].Generation = generation;
        Volatile.Write(ref ring[slot].Sequence, write + 1);
        writeIndex = write + 1;
        return true;
    }

    internal bool TryDequeue(out Event item) {
        int read = readIndex;
        int slot = read & Mask;
        if(Volatile.Read(ref ring[slot].Sequence) != read + 1) {
            item = default;
            return false;
        }
        item = new Event(ring[slot].Key, ring[slot].Down, ring[slot].Generation);
        readIndex = read + 1;
        Volatile.Write(ref readShared, read + 1);
        return true;
    }

    internal bool TakeOverflow() => Interlocked.Exchange(ref overflowed, 0) != 0;

    internal void Clear() {
        while(TryDequeue(out _)) { }
        Volatile.Write(ref overflowed, 0);
    }
}
