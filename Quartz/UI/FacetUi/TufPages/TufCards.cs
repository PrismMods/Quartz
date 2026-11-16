#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Features.Tuf;
using Quartz.Resource;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.TufPages;
/// <summary>What a TUF card shows: Badge, Delete and Rollback are the installed level's buttons a page offers, through
/// Service. Runner owns the card's level: it acts, queues and launches, as the Levels service's runner does for its list
/// and the pack service's for a pack's levels.</summary>
internal sealed class TufCardOptions {
    internal TufService Service;
    internal TufLevelActionRunner Runner;
    internal bool Grid;
    internal bool Badge = true, Delete = true, Rollback = true;
    /// <summary>A pack level's meta line, as classic's pack card: artist, creator, clears and likes, even when blank.</summary>
    internal bool PackMeta;
}
/// <summary>
/// One TUF level's card in the new menu, part for part as the classic browser's: the difficulty rail, id, difficulty, update
/// badge, song, meta line, action, delete and rollback, and the chart rows under a level that asks which chart to load.
/// Every button that acts goes through QuartzShell.Do. A delete arms on its first click and lapses after 4 s.
/// </summary>
internal static class TufCards {
    internal const float ArmSeconds = 4f;
    /// <summary>The level whose delete is armed (0: none), and when the arm lapses (unscaled time).</summary>
    internal static int Armed;
    internal static float ArmedUntil;
    /// <summary>Raised when a card found its level's state stale (a rollback list gone from disk): the page redraws.</summary>
    internal static event Action Stale;
    // The last drawn action and delete button per level id: a Changed relabels the actions and an arm repaints the deletes.
    internal static readonly Dictionary<int, Button> Actions = [];
    private static readonly Dictionary<int, Button> deletes = [];
    // Card keys by id, made once: ImageCards reads every card's key on each refresh pass.
    private static readonly Dictionary<int, string> keys = [];

    internal static string Key(TufLevel l) {
        if(!keys.TryGetValue(l.Id, out string key)) keys[l.Id] = key = l.Id.ToString();
        return key;
    }

    internal static bool Installed(TufLevel l) =>
        l.InstallFolder != null && l.State is not TufItemState.Downloading and not TufItemState.Extracting and not TufItemState.Loading;

    internal static bool Actionable(TufLevel l) =>
        l.State is not TufItemState.Unavailable and not TufItemState.Downloading and not TufItemState.Extracting and not TufItemState.Loading
        || (l.State == TufItemState.Unavailable && TufMainLevel.Resolve(l, out _) != TufMainLevel.TufMainAction.None);

    internal static string ActionLabel(TufLevel l, int queuePosition) => l.State switch {
        TufItemState.Downloading => l.Progress < 0
            ? Tr("TUF_DOWNLOADING", "Downloading…")
            : string.Format(Tr("TUF_DOWNLOADING_PROGRESS", "Downloading {0}%"), Mathf.Clamp((int)(l.Progress * 100f), 0, 100)),
        TufItemState.Extracting => Tr("TUF_EXTRACTING", "Extracting…"),
        TufItemState.Loading => Tr("TUF_LOADING_LEVEL", "Loading…"),
        TufItemState.Load => Tr("TUF_LOAD", "Load"),
        TufItemState.Retry => Tr("TUF_RETRY", "Retry"),
        TufItemState.Unavailable => TufMainLevel.Resolve(l, out _) switch {
            TufMainLevel.TufMainAction.Play => Tr("TUF_PLAY", "Play"),
            TufMainLevel.TufMainAction.BuyDlc => Tr("TUF_BUY_DLC", "Buy DLC"),
            _ => Tr("TUF_UNAVAILABLE", "Unavailable"),
        },
        TufItemState.ChooseChart => Tr("TUF_CANCEL", "Cancel"),
        TufItemState.Queued => string.Format(Tr("TUF_QUEUED", "Queued #{0}"), queuePosition),
        _ => Tr("TUF_DOWNLOAD", "Download"),
    };

    /// <summary>The level's thumbnail when previews are on and it is decoded; otherwise asks for it (once per key while the
    /// cache holds it) and returns null.</summary>
    internal static Texture Image(TufLevel l, TufService s) {
        if(s == null || !s.ShowPreviews) return null;
        string key = Key(l);
        if(TufPreviewCache.TryGet(key, out Texture2D tex) && tex != null) return tex;
        TufPreviewCache.Request(key, TufPreviewSource.Video(l.VideoLink));
        return null;
    }

    internal static void Body(VisualElement card, TufLevel l, TufCardOptions o) {
        TufService s = o.Service;
        bool installed = Installed(l);
        VisualElement box = Ui.Row(card);
        box.name = "tuf-card-" + l.Id;
        box.style.alignItems = Align.Stretch;
        box.style.flexGrow = 1f;

        Color hue = ColorUtility.TryParseHtmlString(l.DifficultyColor, out Color c) ? c : Color.white;
        // Readable on dark thumbnails too (TufContrast): a dark colour lightened to 3:1 with a light glow, a light one a dark shadow.
        ((float R, float G, float B) shown, bool glow) = TufContrast.Readable(hue.r, hue.g, hue.b);
        Color rail = new(shown.R, shown.G, shown.B, 1f);
        VisualElement bar = Ui.Box(box);
        bar.style.width = 6f;
        bar.style.flexShrink = 0f;
        Ui.SetRadius(bar, 2f);
        bar.style.backgroundColor = new Color(hue.r, hue.g, hue.b, 1f);   // TUF's own colour, unlightened, as classic's rail

        VisualElement text = Ui.Box(box);
        text.style.flexGrow = 1f;
        text.style.flexShrink = 1f;
        text.style.minWidth = 0f;
        text.style.paddingLeft = 10f;

        VisualElement head = Ui.Row(text);
        Label id = Literal(head, "#" + l.Id, Role.TextMuted);
        id.style.marginRight = 12f;
        Label diff = Literal(head, l.Difficulty ?? "", Role.Text);
        diff.name = "tuf-difficulty";
        Paint.Forget(diff);
        diff.style.color = rail;
        // Bold (the face's real weight) over a soft shadow, light for a dark colour and dark for a light one. Set once here.
        diff.style.unityFontStyleAndWeight = FontStyle.Bold;
        diff.style.textShadow = new TextShadow {
            offset = glow ? Vector2.zero : new Vector2(1f, 1f), blurRadius = 3f,
            color = glow ? new Color(1f, 1f, 1f, TufContrast.GlowAlpha) : new Color(0f, 0f, 0f, TufContrast.ShadowAlpha),
        };
        diff.style.marginRight = 12f;
        if(installed && o.Badge) Badge(head, l, s);

        string song = string.IsNullOrEmpty(l.Song) ? Tr("TUF_UNKNOWN_LEVEL", "Level") + " #" + l.Id : l.Song;
        Label title = Literal(text, song, Role.Text);
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.fontSize = Theme.For(card).Sizes.Body * 1.3f;
        bool known = !string.IsNullOrEmpty(l.Artist) || !string.IsNullOrEmpty(l.Creator);
        if(o.Grid) {
            Literal(text, known ? $"{l.Artist}  ·  {l.Creator}" : Tr("TUF_INSTALLED_UNKNOWN", "Downloaded before Quartz tracked level details."),
                Role.TextMuted);
            if(known) Literal(text, $"✓ {l.Clears:N0}    ♥ {l.Likes:N0}", Role.TextMuted);
        } else {
            Literal(text, o.PackMeta ? $"{l.Artist}  ·  {l.Creator}  ·  ✓ {l.Clears:N0}  ♥ {l.Likes:N0}" : Meta(l), Role.TextMuted);
        }

        // List: the buttons right of the text; grid: under it.
        VisualElement buttons = Ui.Row(o.Grid ? text : box);
        buttons.style.alignItems = Align.Center;
        buttons.style.flexShrink = 0f;
        if(o.Grid) buttons.style.marginTop = 6f;
        Action(buttons, l, o.Runner, o.Grid);
        if(installed && o.Rollback && s.SnapshotCount(l.Id) > 0) {
            Button back = null;
            back = Ui.IconButton(buttons, Background.FromSprite(MainCore.Spr.Get(UISprite.ClockRewind128)), "DESC_TUF_ROLLBACK", () => {
                Disarm();
                Rollback(back, l, s);
            });
            back.name = "tuf-rollback";
            Paint.Enable(back, !s.IsBusy);
        }
        if(installed && o.Delete) Delete(buttons, l, s);
    }

    /// <summary>The chart rows under a level that asks which chart to load, or null.</summary>
    internal static Action<VisualElement> Below(TufLevel l, Action<TufLevel, string> launch) {
        if(l.State != TufItemState.ChooseChart || l.Charts == null) return null;
        IReadOnlyList<string> charts = l.Charts;
        return box => {
            for(int i = 0; i < charts.Count; i++) {
                string chart = charts[i];
                int n = i + 1;
                Button row = Ui.Btn(box, "", () => QuartzShell.Do("tuf.chart:" + l.Id + ":" + n, () => launch(l, chart)));
                row.text = "▶  " + ChartName(l, chart);
                row.name = "tuf-chart-" + n;
                row.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.style.marginTop = 4f;
                row.style.marginRight = 0f;
                // The rows fade in 35 ms apart, as classic's.
                row.style.opacity = 0f;
                float delay = 35f * i;
                int total = 180 + 35 * i;
                row.experimental.animation.Start(0f, total, total, (e, ms) => e.style.opacity = Mathf.Clamp01((ms - delay) / 180f));
            }
        };
    }

    /// <summary>The level's rollback copies in a popup over owner, each with its Restore; with none left on disk the counts
    /// are read again and the page redraws.</summary>
    internal static void Rollback(VisualElement owner, TufLevel l, TufService s) {
        IReadOnlyList<TufSnapshot> snapshots = s.Snapshots(l.Id);
        if(snapshots.Count == 0) {
            s.RefreshSnapshotCounts();
            Stale?.Invoke();
            return;
        }
        string heading = string.IsNullOrEmpty(l.Song) ? Tr("TUF_UNKNOWN_LEVEL", "Level") + " #" + l.Id : l.Song + "  ·  #" + l.Id;
        Popup popup = null;
        popup = Popup.Open(owner, box => {
            box.name = "tuf-rollback-list";
            Ui.Text(box, "TUF_ROLLBACK_TITLE").style.unityFontStyleAndWeight = FontStyle.Bold;
            Literal(box, heading, Role.TextMuted);
            int n = 0;
            foreach(TufSnapshot snap in snapshots) {
                long stamp = snap.Stamp;
                VisualElement row = Ui.Row(box);
                row.name = "tuf-snapshot-" + ++n;
                row.style.alignItems = Align.Center;
                row.style.marginTop = 4f;
                Label what = Literal(row, TufRollbackDialog.Describe(snap), Role.Text);
                what.style.flexGrow = 1f;
                what.style.marginRight = 8f;
                Ui.Btn(row, "TUF_ROLLBACK_RESTORE", () => {
                    popup?.Close();
                    QuartzShell.Do("tuf.restore:" + l.Id + ":" + stamp, () => s.RestoreSnapshot(l, stamp));
                }).name = "tuf-restore";
            }
            Ui.Btn(box, "TUF_ROLLBACK_CANCEL", () => popup?.Close()).name = "tuf-rollback-cancel";
        });
    }

    /// <summary>Re-sets each drawn action's label and enabled state: the queue position is in the label.</summary>
    internal static void Relabel(IReadOnlyList<TufLevel> levels, TufLevelActionRunner r) {
        foreach(TufLevel l in levels) {
            if(!Actions.TryGetValue(l.Id, out Button b) || b.panel == null) continue;
            string label = ActionLabel(l, l.State == TufItemState.Queued ? r?.QueuePosition(l.Id) ?? 0 : 0);
            if(b.text != label) b.text = label;
            bool on = Actionable(l) && !(r?.IsLaunching ?? false);
            if(b.enabledSelf != on) Paint.Enable(b, on);
        }
    }

    internal static void Disarm() {
        if(Armed == 0) return;
        Armed = 0;
        PaintDeletes();
    }

    /// <summary>Forgets the drawn buttons and the arm: the page left the panel.</summary>
    internal static void Forget() {
        Armed = 0;
        Actions.Clear();
        deletes.Clear();
        keys.Clear();
    }

    private static void Action(VisualElement row, TufLevel l, TufLevelActionRunner r, bool grow) {
        string label = ActionLabel(l, l.State == TufItemState.Queued ? r?.QueuePosition(l.Id) ?? 0 : 0);
        Button b = Ui.Btn(row, "", () => QuartzShell.Do("tuf.act:" + l.Id, () => r?.Act(l)));
        b.text = label;
        b.name = "tuf-action";
        b.style.minWidth = 128f;
        if(grow) b.style.flexGrow = 1f;
        Paint.Enable(b, Actionable(l) && !(r?.IsLaunching ?? false));
        if(l.State == TufItemState.Queued) Words.Tip(b, "DESC_TUF_QUEUED");
        if(!string.IsNullOrWhiteSpace(l.Error)) {
            string error = l.Error.Length > 900 ? l.Error[..900] + "…" : l.Error;
            Controls.Hook(b, o => o.TooltipText = () => error);
        }
        Actions[l.Id] = b;
    }

    private static void Badge(VisualElement row, TufLevel l, TufService s) {
        TufUpdateState state = l.UpdateState;
        Button b = Ui.Btn(row, state switch {
            TufUpdateState.Checking => "TUF_UPDATE_CHECKING",
            TufUpdateState.Available => "TUF_UPDATE_AVAILABLE",
            TufUpdateState.UpToDate => "TUF_UPDATE_CURRENT",
            TufUpdateState.Updating => "TUF_UPDATE_RUNNING",
            _ => "TUF_INSTALLED",
        }, () => {
            if(l.UpdateState == TufUpdateState.Available) QuartzShell.Do("tuf.update:" + l.Id, () => s.UpdateLevel(l));
            else QuartzShell.Do("tuf.check:" + l.Id, () => s.CheckUpdate(l));
        });
        b.name = "tuf-badge";
        Paint.Look(b, Theme.For(b).Looks.FlatButton);
        if(state == TufUpdateState.Available) Paint.Select(b, true);
        bool clickable = !s.IsLaunching && state is not TufUpdateState.Checking and not TufUpdateState.Updating;
        Paint.Enable(b, clickable);
        Words.Tip(b, "DESC_TUF_UPDATE_BADGE");
    }

    private static void Delete(VisualElement row, TufLevel l, TufService s) {
        Button b = Ui.IconButton(row, Background.FromSprite(MainCore.Spr.Get(UISprite.Trash128)), "DESC_TUF_DELETE", () => {
            if(Armed == l.Id) {
                Disarm();
                QuartzShell.Do("tuf.delete:" + l.Id, () => s.DeleteInstalled(l));
                return;
            }
            Armed = l.Id;
            ArmedUntil = Time.unscaledTime + ArmSeconds;
            PaintDeletes();
        });
        b.name = "tuf-delete";
        Paint.Look(b, Theme.For(b).Looks.DangerButton);
        Paint.Enable(b, !s.IsBusy);
        Paint.Select(b, Armed == l.Id);
        deletes[l.Id] = b;
    }

    private static void PaintDeletes() {
        foreach(KeyValuePair<int, Button> d in deletes)
            if(d.Value.panel != null) Paint.Select(d.Value, d.Key == Armed);
    }

    private static string Meta(TufLevel l) {
        string size = l.SizeBytes > 0 ? "  ·  " + TufDiskSpace.Describe(l.SizeBytes) : "";
        if(string.IsNullOrEmpty(l.Artist) && string.IsNullOrEmpty(l.Creator))
            return Tr("TUF_INSTALLED_UNKNOWN", "Downloaded before Quartz tracked level details.") + size;
        return $"{l.Artist}  ·  {l.Creator}  ·  ✓ {l.Clears:N0}  ♥ {l.Likes:N0}{size}";
    }

    private static string ChartName(TufLevel l, string chart) {
        try {
            return string.IsNullOrEmpty(l.ChartsRoot) ? Path.GetFileName(chart) : Path.GetRelativePath(l.ChartsRoot, chart);
        } catch(Exception e) {
            Diag.Ignore(e);
            return Path.GetFileName(chart);
        }
    }

    // Text TUF supplies (a song, an artist) or Quartz formats: shown as given, never looked up as a key, so it skips the
    // relabel table Ui.Text joins; one line, cut with an ellipsis, at the theme's body size.
    internal static Label Literal(VisualElement parent, string value, Role role) {
        var l = new Label(value);
        l.style.fontSize = Theme.For(parent).Sizes.Body;
        l.style.whiteSpace = WhiteSpace.NoWrap;
        l.style.textOverflow = TextOverflow.Ellipsis;
        l.style.overflow = Overflow.Hidden;
        parent.Add(l);
        Paint.Fg(l, role);
        return l;
    }

    private static string Tr(string key, string english) => QuartzHost.Text(key, english);
}
#endif
