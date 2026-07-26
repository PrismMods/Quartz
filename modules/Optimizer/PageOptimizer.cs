using Quartz.Core;
using Quartz.Features.Optimizer;
using Quartz.UI.Factory;
using Quartz.UI.Generator;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
public static class PageOptimizer {
    public static void Create(RectTransform parent) {
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        Optimizer.EnsureConf();
        OptimizerSettings opt = Optimizer.Conf;
        OptimizerSettings optDef = new();
        var optimizerSec = GenerateUI.FlatSection(content.transform, "Optimizer");
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.SmoothGC,
            opt.SmoothGC,
            v => { opt.SmoothGC = v; Optimizer.Apply(); Optimizer.Save(); },
            "Smooth GC",
            "opt_smoothgc",
            "Holds off garbage collection while a level is playing and runs it when the run ends, so a GC pause can't land mid-run and nudge your timing. The heap grows during the run (a safety collect kicks in on very long levels). Best paired with Clean Heap On Load."
        );
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.LeakGuard,
            opt.LeakGuard,
            v => { opt.LeakGuard = v; Optimizer.Apply(); Optimizer.Save(); },
            "Fix Game Memory Leaks",
            "opt_leakguard",
            "Patches known memory leaks in the game itself: decoration render textures and materials that survive level unloads, frame-rate-effect screen buffers, workshop thumbnails, practice-mode waveforms, and internal caches that only ever grow. Reduces RAM creep during long sessions."
        );
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.CollectOnLevelLoad,
            opt.CollectOnLevelLoad,
            v => { opt.CollectOnLevelLoad = v; Optimizer.Apply(); Optimizer.Save(); },
            "Clean Heap On Load",
            "opt_collectonload",
            "Runs a garbage collection every time a scene loads, so each run starts from a clean heap. The load screen already hitches, so the collection is free here."
        );
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.BoostProcessPriority,
            opt.BoostProcessPriority,
            v => { opt.BoostProcessPriority = v; Optimizer.Apply(); Optimizer.Save(); },
            "Boost Process Priority",
            "opt_priority",
            "Asks the OS to give the game more consistent CPU time (Above Normal priority). Takes effect on Windows; ignored where the system doesn't allow it (usually macOS/Linux)."
        );
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.RunInBackground,
            opt.RunInBackground,
            v => { opt.RunInBackground = v; Optimizer.Apply(); Optimizer.Save(); },
            "Run In Background",
            "opt_runinbg",
            "Keeps the game running at full speed when its window loses focus, so a run or practice session doesn't stall when you alt-tab."
        );
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.LossyTextureCompression,
            opt.LossyTextureCompression,
            v => { opt.LossyTextureCompression = v; Optimizer.Save(); },
            "Lossy Texture Compression",
            "opt_lossytexture",
            "Compresses custom textures loaded from disk (DXT) to cut their memory use ~4-8x, with a small visual quality cost. Applies to textures loaded after it's turned on."
        );
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.FastBloom,
            opt.FastBloom,
            v => { opt.FastBloom = v; Optimizer.Save(); },
            "Fast Bloom",
            "opt_fastbloom",
            "Forces ADOFAI's bloom post-process to use its cheaper low-quality render path while bloom is active. This targets real GPU work and can improve FPS on bloom-heavy levels, with softer/less precise bloom."
        );
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.SkipNoOpScreenFilters,
            opt.SkipNoOpScreenFilters,
            v => { opt.SkipNoOpScreenFilters = v; Optimizer.Save(); },
            "Skip No-Op Screen Filters",
            "opt_skipnoopfilters",
            "Skips ADOFAI full-screen screen-tile/screen-scroll shader passes when their current values are visually identity, replacing the shader pass with a plain copy. This removes real render work without wrapping an existing game setting."
        );
        GenerateUI.ToggleTip(
            optimizerSec.Body,
            optDef.RenderAllHitSounds,
            opt.RenderAllHitSounds,
            v => { opt.RenderAllHitSounds = v; Optimizer.Apply(); Optimizer.Save(); },
            "Render All Hit Sounds",
            "opt_renderhitsounds",
            "At very high note density (Hz / high-KPS charts) the game can't fire every hit-sound voice, so overlapping hits get dropped. This mixes all of a level's scheduled hit sounds into a continuous rendered audio track and plays that instead, so none are lost. It replaces the game's own hit sounds while active and costs a little CPU and memory during play. Off by default."
        );
    }
}
