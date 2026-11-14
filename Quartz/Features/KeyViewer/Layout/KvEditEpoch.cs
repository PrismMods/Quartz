namespace Quartz.Features.KeyViewer.Layout;
// Tells the editor's undo steps whether the document they were recorded against is still the
// one in use: the token stays the same object while the document is the one the editor last
// owned and its structure is unchanged, and becomes a new object (stale steps) otherwise.
// The structure hash is memoised per document and frame, since the header reads it many times
// a frame; a change made elsewhere is therefore seen from the next frame on. The frame source
// is required (the game passes Time.frameCount) so the memo can never be left off.
internal sealed class KvEditEpoch(Func<int> frame) {
    private object token = new();
    private KvDocument owned;
    private int ownedHash;
    private KvDocument memoDoc;
    private int memoFrame;
    private int memoHash;

    internal object Token(KvDocument current) {
        int hash = Hash(current, false);
        if(!ReferenceEquals(current, owned) || hash != ownedHash) {
            token = new object();
            owned = current;
            ownedHash = hash;
        }
        return token;
    }
    // Called from the step's capture, which runs after the change and before the step is
    // recorded, so the editor's own structure change never renews the token.
    internal void Own(KvDocument current) {
        if(current == null || !ReferenceEquals(current, owned)) return;
        ownedHash = Hash(current, true);
    }
    // Called from the step's restore: the replacement document keeps the same token.
    internal void Adopt(KvDocument next) {
        owned = next;
        ownedHash = Hash(next, true);
    }
    private int Hash(KvDocument doc, bool fresh) {
        if(doc == null) return 0;
        int now = frame();
        if(!fresh && ReferenceEquals(doc, memoDoc) && now == memoFrame) return memoHash;
        memoDoc = doc;
        memoFrame = now;
        return memoHash = doc.StructureHash();
    }
}
