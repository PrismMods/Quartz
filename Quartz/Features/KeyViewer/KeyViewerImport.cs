using Quartz.Features.Interop;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Interop;
using UnityEngine;
namespace Quartz.Features.KeyViewer;
public sealed class KeyViewerImport : IImportHandler {
    public int Apply(ImportSource source) {
        if(!source.TryExtra(ImportKeys.KeyViewerPayload, out ImportedKeyViewer payload) || payload == null) return 0;
        SettingsImportReplaceMode mode = source.TryExtra(ImportKeys.KeyViewerMode, out SettingsImportReplaceMode m)
            ? m : SettingsImportReplaceMode.ReplaceAll;
        SettingsImportKeyViewerPart parts = source.TryExtra(ImportKeys.KeyViewerParts, out SettingsImportKeyViewerPart p)
            ? p : SettingsImportKeyViewerPart.All;
        return ApplyKeyViewerImport(payload, mode, parts);
    }
    public void Refresh() {
        KeyViewerOverlay.EnsureConf();
        KeyViewerOverlay.SyncKeysToKeyLimiter();
        KeyViewerOverlay.Rebuild();
        KeyViewerOverlay.Apply();
        KeyViewerOverlay.Save();
    }
    private static int ApplyKeyViewerImport(ImportedKeyViewer kv, SettingsImportReplaceMode mode, SettingsImportKeyViewerPart parts) {
        if(kv == null || mode == SettingsImportReplaceMode.KeepOld) return 0;
        SettingsImportKeyViewerPart effective = mode == SettingsImportReplaceMode.ReplaceCertain
            ? parts & kv.Available
            : kv.Available;
        if(effective == SettingsImportKeyViewerPart.None) return 0;
        KeyViewerOverlay.EnsureConf();
        KeyViewerSettings target = KeyViewerOverlay.Conf;
        target.Mode = KvMigrationPlan.LegacyModeSimple;
        int count = 0;
        if((effective & SettingsImportKeyViewerPart.KeysLayout) != 0) {
            if(kv.HasStyle) { target.Style = Mathf.Clamp(kv.Style, 0, 3); }
            if(kv.Key10 is { Length: 10 }) { target.Key10 = kv.Key10; }
            if(kv.Key12 is { Length: 12 }) { target.Key12 = kv.Key12; }
            if(kv.Key16 is { Length: 16 }) { target.Key16 = kv.Key16; }
            if(kv.Key20 is { Length: 20 }) { target.Key20 = kv.Key20; }
            if(kv.HasFoot) {
                target.FootStyle = Mathf.Clamp(kv.FootStyle, 0, KeyViewerSettings.MaxFootStyle);
                if(kv.FootKeys is { Length: > 0 }) {
                    int[] dest = target.FootKeysForStyle(target.FootStyle);
                    int n = Mathf.Min(kv.FootKeys.Length, dest.Length);
                    for(int i = 0; i < n; i++) { dest[i] = kv.FootKeys[i]; }
                }
            }
            if(kv.GhostKey10 is { Length: 10 }) { target.GhostKey10 = kv.GhostKey10; }
            if(kv.GhostKey12 is { Length: 12 }) { target.GhostKey12 = kv.GhostKey12; }
            if(kv.GhostKey16 is { Length: 16 }) { target.GhostKey16 = kv.GhostKey16; }
            if(kv.GhostKey20 is { Length: 20 }) { target.GhostKey20 = kv.GhostKey20; }
            count++;
        }
        if((effective & SettingsImportKeyViewerPart.Labels) != 0) {
            if(kv.Key10Text is { Length: 10 }) { target.Key10Text = kv.Key10Text; }
            if(kv.Key12Text is { Length: 12 }) { target.Key12Text = kv.Key12Text; }
            if(kv.Key16Text is { Length: 16 }) { target.Key16Text = kv.Key16Text; }
            if(kv.Key20Text is { Length: 20 }) { target.Key20Text = kv.Key20Text; }
            count++;
        }
        if((effective & SettingsImportKeyViewerPart.Colors) != 0) {
            if(kv.Bg is { } bg) { target.SetBg(bg); }
            if(kv.BgClicked is { } bgc) { target.SetBgPressed(bgc); }
            if(kv.Outline is { } ol) { target.SetOutline(ol); }
            if(kv.OutlineClicked is { } olc) { target.SetOutlinePressed(olc); }
            if(kv.Text is { } tx) { target.SetText(tx); }
            if(kv.TextClicked is { } txc) { target.SetTextPressed(txc); }
            if(kv.Rain is { } rc) { target.SetRain(rc); }
            if(kv.Rain2 is { } rc2) { target.SetRain2(rc2); }
            if(kv.Rain3 is { } rc3) { target.SetRain3(rc3); }
            if(kv.GhostRain is { } gr) { target.SetGhostRain(gr); }
            count++;
        }
        if((effective & SettingsImportKeyViewerPart.Rain) != 0) {
            if(kv.HasRainEnabled) { target.RainEnabled = kv.RainEnabled; }
            if(kv.HasRainSpeed) { target.RainSpeed = kv.RainSpeed; }
            if(kv.HasRainHeight) { target.RainHeight = kv.RainHeight; }
            count++;
        }
        if((effective & SettingsImportKeyViewerPart.PositionSize) != 0) {
            if(kv.HasSize) { target.Size = kv.Size; }
            count++;
        }
        if(mode == SettingsImportReplaceMode.ReplaceAll) {
            if(kv.HasEnabled) { target.Enabled = kv.Enabled; }
            if(kv.HasSync) { target.SyncToKeyLimiter = kv.SyncToKeyLimiter; }
        }
        return count;
    }
}
