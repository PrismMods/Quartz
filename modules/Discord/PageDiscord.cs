using Quartz.Async;
using Quartz.Core;
using Quartz.Features.Discord;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Factory.Page;
public static class PageDiscord {
    private static readonly Color RailBg = new(0.118f, 0.122f, 0.133f);
    private static readonly Color SidebarBg = new(0.169f, 0.176f, 0.192f);
    private static readonly Color ChatBg = new(0.192f, 0.200f, 0.220f);
    private static readonly Color PanelBg = new(0.137f, 0.141f, 0.157f);
    private static readonly Color ComposerBg = new(0.220f, 0.227f, 0.251f);
    private static readonly Color ChannelActive = new(0.251f, 0.259f, 0.286f);
    private static readonly Color Divider = new(0.247f, 0.255f, 0.278f);
    private static readonly Color HeadLine = new(0.122f, 0.129f, 0.141f);
    private static readonly Color TextNormal = new(0.859f, 0.871f, 0.882f);
    private static readonly Color TextBright = new(0.949f, 0.953f, 0.961f);
    private static readonly Color TextMuted = new(0.580f, 0.608f, 0.643f);
    private static readonly Color Blurple = new(0.345f, 0.396f, 0.949f);
    private static readonly Color Green = new(0.137f, 0.647f, 0.353f);
    private static readonly Color Shell = new(0.208f, 0.216f, 0.235f);
    private static readonly Color TextLocked = new(0.400f, 0.420f, 0.451f);
    private static readonly Color Gold = new(0.941f, 0.698f, 0.196f);
    private static readonly Color SoftRed = new(0.949f, 0.247f, 0.263f);
    private static readonly Color[] AvatarColors = [
        new(0.345f, 0.396f, 0.949f), new(0.137f, 0.647f, 0.353f), new(0.941f, 0.698f, 0.196f),
        new(0.831f, 0.361f, 0.361f), new(0.541f, 0.404f, 0.906f), new(0.196f, 0.663f, 0.784f),
    ];
    private static string pendingToken = "";
    public static void Create(RectTransform parent) {
        Transform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(content)), "SECTION_DISCORD", "Discord");
        TextMeshProUGUI status = GenerateUI.AddMutedText(GenerateUI.Row(content, 30f), 15f, 0.6f);
        RectTransform shell = GenerateUI.Row(content, 660f);
        shell.gameObject.AddComponent<ThemeExempt>();
        RectTransform client = Fill(shell, "Client", 4f, 0f, 4f, 0f);
        Paint(client, Shell, 2);
        client.gameObject.AddComponent<RectMask2D>();
        void Refresh() {
            if(client == null) {
                DiscordSession.Changed -= Refresh;
                VoiceSession.Changed -= Refresh;
                return;
            }
            status.text = DiscordSession.LoggedIn
                ? $"{DiscordSession.SelfName} · {DiscordSession.Status}"
                : DiscordSession.Status;
            GenerateUI.ClearChildren(client);
            if(DiscordSession.LoggedIn) BuildClient(client);
            else BuildLogin(client);
        }
        DiscordSession.Changed += Refresh;
        VoiceSession.Changed += Refresh;
        Diagnostics(content);
        VoiceSection(content);
        Refresh();
        DiscordSession.Resume();
    }
    private static void BuildLogin(RectTransform client) {
        Paint(client, ChatBg, 2);
        bool qr = DiscordSession.QrActive;
        RectTransform card = Box(client, "Login", 40f, 32f, 660f, qr ? 400f : 300f);
        Paint(card, SidebarBg, 2);
        Label(Box(card, "Title", 28f, 20f, 600f, 30f), "Log in to Discord", 22f, TextBright, FontStyles.Bold);
        Label(
            Box(card, "Body", 28f, 56f, 600f, 60f),
            "Scan a QR code with the Discord mobile app, or paste a user token. A user token grants full "
            + "account access, and third-party clients break Discord's terms of service.",
            14f, TextMuted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        if(qr) {
            QrPanel(card);
            return;
        }
        RectTransform field = Box(card, "Field", 28f, 126f, 600f, 50f);
        UIInput input = GenerateUI.Input(
            field, "", pendingToken, v => pendingToken = v, "Discord token", null, "discord_token", 0f);
        input.InputField.contentType = TMP_InputField.ContentType.Password;
        input.InputField.ForceLabelUpdate();
        RectTransform login = Box(card, "Login", 28f, 190f, 290f, 50f);
        GenerateUI.Button(login, () => DiscordSession.LogIn(pendingToken), "Log In", "discord_login", 0f);
        RectTransform scan = Box(card, "Scan", 338f, 190f, 290f, 50f);
        GenerateUI.Button(scan, DiscordSession.BeginQrLogin, "Log In with QR", "discord_qr", 0f).SetSecondary();
        if(!DiscordSession.HasSavedToken) return;
        RectTransform forget = Box(card, "Forget", 28f, 246f, 290f, 46f);
        GenerateUI.Button(forget, DiscordSession.LogOut, "Forget Saved Token", "discord_forget", 0f).SetSecondary();
    }
    private static void QrPanel(RectTransform card) {
        RectTransform frame = Box(card, "QrFrame", 28f, 122f, 216f, 216f);
        Paint(frame, Color.white, 1);
        Sprite sprite = QrSprite(DiscordSession.QrUrl);
        if(sprite == null) {
            Label(frame, "generating...", 14f, new Color(0.3f, 0.3f, 0.3f), FontStyles.Normal, TextAlignmentOptions.Center);
        } else {
            RectTransform code = Box(frame, "Code", 8f, 8f, 200f, 200f);
            Image image = code.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
        }
        Label(
            Box(card, "QrHelp", 264f, 122f, 370f, 90f),
            "On your phone: open Discord, go to Settings, then Scan QR Code, and point it at this code.",
            14f, TextNormal, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        Label(
            Box(card, "QrStatus", 264f, 216f, 370f, 50f),
            DiscordSession.QrStatus, 14f, TextMuted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        RectTransform cancel = Box(card, "QrCancel", 264f, 276f, 250f, 46f);
        GenerateUI.Button(cancel, DiscordSession.CancelQrLogin, "Cancel", "discord_qr_cancel", 0f).SetSecondary();
    }
    private static string qrUrl;
    private static Sprite qrSprite;
    private static Texture2D qrTexture;
    private static Sprite QrSprite(string url) {
        if(string.IsNullOrEmpty(url)) return null;
        if(url == qrUrl && qrSprite != null) return qrSprite;
        try {
            bool[,] matrix = QrCode.Encode(url);
            int modules = matrix.GetLength(0);
            const int quiet = 4;
            const int scale = 5;
            int dimension = (modules + quiet + quiet) * scale;
            Color32 dark = new(0, 0, 0, 255);
            Color32 light = new(255, 255, 255, 255);
            Color32[] pixels = new Color32[dimension * dimension];
            for(int i = 0; i < pixels.Length; i++) pixels[i] = light;
            for(int my = 0; my < modules; my++)
                for(int mx = 0; mx < modules; mx++) {
                    if(!matrix[mx, my]) continue;
                    for(int py = 0; py < scale; py++)
                        for(int px = 0; px < scale; px++) {
                            int x = ((mx + quiet) * scale) + px;
                            int y = dimension - 1 - (((my + quiet) * scale) + py);
                            pixels[(y * dimension) + x] = dark;
                        }
                }
            Texture2D texture = new(dimension, dimension, TextureFormat.RGBA32, false) {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            if(qrTexture != null) UnityEngine.Object.Destroy(qrTexture);
            qrTexture = texture;
            qrUrl = url;
            qrSprite = SpriteManager.Create(texture);
            return qrSprite;
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Discord] could not render the QR code: {e.Message}");
            return null;
        }
    }
    private static void BuildClient(RectTransform client) {
        Rail(LeftBand(client, "Rail", 0f, 72f));
        Sidebar(LeftBand(client, "Sidebar", 72f, 240f));
        Chat(Fill(client, "Chat", 312f, 0f, 0f, 0f));
    }
    private static void Rail(RectTransform rail) {
        Paint(rail, RailBg);
        RectTransform list = ScrollList(rail, 0f, 0f, new RectOffset(12, 12, 12, 12), 8f);
        Guild(list, DiscordSession.DirectMessagesId, "@", Blurple);
        Paint(Sized(list, "Separator", 2f), Divider, 1);
        foreach(DiscordGuild guild in DiscordSession.Guilds)
            Guild(list, guild.Id, Initials(guild.Name), ColorFor(guild.Id));
    }
    private static RectTransform ScrollList(
        RectTransform parent, float top, float bottom, RectOffset padding, float spacing
    ) {
        RectTransform viewport = Fill(parent, "Viewport", 0f, top, 0f, bottom);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<EmptyGraphic>().raycastTarget = true;
        RectTransform list = Node(viewport, "List");
        list.anchorMin = new Vector2(0f, 1f);
        list.anchorMax = new Vector2(1f, 1f);
        list.pivot = new Vector2(0.5f, 1f);
        list.offsetMin = Vector2.zero;
        list.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = padding;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = list.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UIScrollController scroll = viewport.gameObject.AddComponent<UIScrollController>();
        scroll.SetContent(list, viewport);
        return list;
    }
    private static void Guild(RectTransform list, string id, string label, Color color) {
        RectTransform slot = Sized(list, "Guild", 48f);
        RectTransform icon = Box(slot, "Icon", 0f, 0f, 48f, 48f);
        Paint(icon, color, 2);
        Label(icon, label, 17f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        Clickable(icon, () => DiscordSession.OpenGuild(id));
        if(DiscordSession.CurrentGuildId == id) Paint(Box(slot, "Pill", -12f, 8f, 4f, 32f), Color.white, 1);
    }
    private static void Sidebar(RectTransform sidebar) {
        Paint(sidebar, SidebarBg);
        RectTransform head = Strip(sidebar, "Head", 0f, 0f, 0f, 48f);
        string title = DiscordSession.CurrentGuildId == DiscordSession.DirectMessagesId
            ? "Direct Messages"
            : NameOfGuild(DiscordSession.CurrentGuildId);
        Label(Box(head, "Name", 16f, 0f, 208f, 48f), title, 16f, TextBright, FontStyles.Bold);
        Paint(Strip(sidebar, "HeadLine", 0f, 47f, 0f, 1f), HeadLine);
        RectTransform list = ScrollList(sidebar, 48f, 52f, new RectOffset(8, 8, 8, 8), 2f);
        if(DiscordSession.Loading && DiscordSession.Channels.Count == 0) {
            Label(Sized(list, "Loading", 32f), "loading...", 14f, TextMuted);
            return;
        }
        string category = null;
        foreach(DiscordChannel channel in DiscordSession.Channels) {
            string name = DiscordSession.CategoryOf.TryGetValue(channel.Id, out string found) ? found : "";
            if(name != category) {
                category = name;
                if(!string.IsNullOrEmpty(name))
                    Label(Sized(list, "Category", 26f), name.ToUpperInvariant(), 12f, TextMuted, FontStyles.Bold);
            }
            Channel(list, channel);
        }
    }
    private static RectTransform Sized(RectTransform parent, string name, float height) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        LayoutElement element = obj.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
        return rect;
    }
    private static void Channel(RectTransform list, DiscordChannel channel) {
        bool locked = DiscordSession.IsLocked(channel.Id);
        bool selected = !locked && channel.Id == DiscordSession.CurrentChannelId;
        RectTransform row = Sized(list, "Channel", 32f);
        Paint(row, selected ? ChannelActive : new Color(0f, 0f, 0f, 0f), 1);
        Color fg = selected ? TextBright : locked ? TextLocked : TextMuted;
        RectTransform mark = Box(row, "Mark", 8f, 0f, 18f, 32f);
        if(locked) LockIcon(mark, fg);
        else if(DiscordSession.IsVoice(channel.Type)) SpeakerIcon(mark, fg);
        else Label(mark, channel.Type is 1 or 3 ? "@" : "#", 17f, fg, FontStyles.Normal, TextAlignmentOptions.Center);
        Label(Fill(row, "Name", 30f, 0f, 8f, 0f), channel.Name, 15f, fg);
        if(locked) return;
        Clickable(row, () => DiscordSession.OpenChannel(channel.Id));
    }
    private static void SpeakerIcon(RectTransform mark, Color color) {
        Paint(Box(mark, "Body", 2f, 12f, 7f, 8f), color, 1);
        RectTransform arc = Box(mark, "Arc", 7f, 9f, 14f, 14f);
        Image ring = Paint(arc, color, 0);
        ring.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256P1024);
        ring.type = Image.Type.Sliced;
        Paint(Box(mark, "ArcCut", 0f, 9f, 9f, 14f), SidebarBg);
        Paint(Box(mark, "Cone", 5f, 10f, 5f, 12f), color, 1);
    }
    private static void LockIcon(RectTransform mark, Color color) {
        RectTransform shackle = Box(mark, "Shackle", 5f, 9f, 8f, 8f);
        Image ring = Paint(shackle, color, 0);
        ring.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256P1024);
        ring.type = Image.Type.Sliced;
        Paint(Box(mark, "Cut", 5f, 14f, 8f, 4f), SidebarBg);
        Paint(Box(mark, "Body", 3f, 15f, 12f, 9f), color, 1);
    }
    private static void UserPanel(RectTransform panel) {
        Paint(panel, PanelBg);
        RectTransform avatar = Avatar(panel, 8f, 10f, 32f, ColorFor(DiscordSession.SelfId), Initials(DiscordSession.SelfName));
        Status(avatar, Green, PanelBg);
        Label(Box(panel, "Name", 48f, 8f, 110f, 18f), DiscordSession.SelfName ?? "", 14f, TextBright, FontStyles.Bold);
        Label(Box(panel, "Tag", 48f, 26f, 110f, 16f), "Online", 12f, TextMuted);
        RectTransform logout = BoxRight(panel, "LogOut", 8f, 10f, 74f, 32f);
        Paint(logout, ChannelActive, 1);
        Label(logout, "Log Out", 12f, TextMuted, FontStyles.Bold, TextAlignmentOptions.Center);
        Clickable(logout, DiscordSession.LogOut);
    }
    private static void Chat(RectTransform chat) {
        Paint(chat, ChatBg);
        RectTransform head = Strip(chat, "ChatHead", 0f, 0f, 0f, 48f);
        bool any = DiscordSession.CurrentChannelId != null;
        Label(Box(head, "Mark", 16f, 0f, 20f, 48f), any ? "#" : "", 20f, TextMuted, FontStyles.Normal, TextAlignmentOptions.Center);
        Label(Box(head, "Title", 40f, 0f, 300f, 48f), any ? DiscordSession.CurrentChannelName : "no channel", 16f, TextBright, FontStyles.Bold);
        Label(BoxRight(head, "Count", 16f, 0f, 200f, 48f), $"{DiscordSession.Messages.Count} loaded", 12f, TextMuted, FontStyles.Normal, TextAlignmentOptions.Right);
        Paint(Strip(chat, "ChatHeadLine", 0f, 47f, 0f, 1f), HeadLine);
        bool voice = DiscordSession.IsVoice(DiscordSession.CurrentChannelType);
        if(voice) VoiceBar(Strip(chat, "VoiceBar", 0f, 48f, 0f, 56f));
        Messages(Fill(chat, "Messages", 0f, voice ? 104f : 48f, 0f, 72f));
        Composer(StripBottom(chat, "Composer", 16f, 16f, 16f, 44f));
    }
    private static void VoiceBar(RectTransform bar) {
        Paint(bar, PanelBg);
        bool here = VoiceSession.Connected && VoiceSession.ChannelId == DiscordSession.CurrentChannelId;
        bool busy = VoiceSession.Current is VoiceSession.State.Requesting or VoiceSession.State.Signalling;
        Color accent = here ? Green : busy ? Gold : TextMuted;
        Paint(Box(bar, "Dot", 16f, 24f, 8f, 8f), accent, 3);
        Label(
            Box(bar, "Status", 32f, 4f, 520f, 24f),
            VoiceSession.Status.Length == 0 ? "not connected" : VoiceSession.Status,
            13f, here ? TextNormal : TextMuted, FontStyles.Normal, TextAlignmentOptions.Left);
        Label(
            Box(bar, "Dave", 32f, 28f, 400f, 22f),
            "E2EE: " + VoiceSession.DaveStatus
                + $"  ·  sent {VoiceAudio.FramesSent} / recv {VoiceAudio.FramesReceived}"
                + $" / dropped {VoiceAudio.FramesDropped}  ·  mic {VoiceAudio.MicLevel:F3} (peak {VoiceAudio.MicPeak:F3})",
            12f, TextMuted, FontStyles.Normal, TextAlignmentOptions.Left);
        if(here) {
            RectTransform mute = BoxRight(bar, "Mute", 134f, 12f, 96f, 32f);
            Paint(mute, VoiceAudio.Muted ? SoftRed : ChannelActive, 1);
            Label(
                mute, VoiceAudio.Muted ? "Unmute" : "Mute", 13f,
                VoiceAudio.Muted ? Color.white : TextNormal, FontStyles.Bold, TextAlignmentOptions.Center);
            Clickable(mute, () => {
                VoiceAudio.Muted = !VoiceAudio.Muted;
                VoiceSession.Refresh();
            });
        }
        RectTransform button = BoxRight(bar, "Toggle", 16f, 12f, 110f, 32f);
        bool leave = here || busy;
        Paint(button, leave ? SoftRed : Green, 1);
        Label(button, leave ? "Disconnect" : "Join Voice", 13f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        string guildId = DiscordSession.CurrentGuildId;
        string channelId = DiscordSession.CurrentChannelId;
        Clickable(button, () => {
            if(leave) VoiceSession.Leave();
            else VoiceSession.Join(guildId, channelId);
        });
    }
    private static void Messages(RectTransform area) {
        RectTransform list = ScrollList(area, 0f, 0f, new RectOffset(0, 0, 8, 8), 10f);
        if(DiscordSession.Messages.Count == 0) {
            Label(
                Sized(list, "Empty", 30f),
                DiscordSession.Loading ? "loading messages..." : "no messages here yet",
                14f, TextMuted);
            return;
        }
        foreach(DiscordMessage message in DiscordSession.Messages) Message(list, message);
        UIScrollController scroll = list.parent.GetComponent<UIScrollController>();
        if(scroll == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(list);
        scroll.ScrollTo(float.MaxValue);
    }
    private static void Message(RectTransform list, DiscordMessage message) {
        GameObject obj = new("Message");
        obj.transform.SetParent(list, false);
        RectTransform row = obj.AddComponent<RectTransform>();
        VerticalLayoutGroup layout = obj.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(72, 16, 0, 0);
        layout.spacing = 2f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        RectTransform avatar = Avatar(row, 16f, 0f, 40f, ColorFor(message.AuthorId), Initials(message.AuthorName));
        LayoutElement floating = avatar.gameObject.AddComponent<LayoutElement>();
        floating.ignoreLayout = true;
        string stamp = message.Timestamp.ToLocalTime().ToString("HH:mm");
        TextMeshProUGUI header = Label(row, "", 15f, TextBright, FontStyles.Bold);
        header.richText = true;
        header.text = Escape(message.AuthorName)
            + "  <size=11><color=#949BA4>" + stamp + "</color></size>";
        header.overflowMode = TextOverflowModes.Ellipsis;
        string body = message.Content;
        if(string.IsNullOrEmpty(body) && message.Attachments.Count > 0) body = "[attachment]";
        if(string.IsNullOrEmpty(body) && message.Embeds.Count > 0) body = "[embed]";
        if(string.IsNullOrEmpty(body)) return;
        TextMeshProUGUI text = Label(row, body, 15f, TextNormal, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        text.richText = false;
        text.overflowMode = TextOverflowModes.Overflow;
    }
    private static string Escape(string value) =>
        string.IsNullOrEmpty(value) ? "" : value.Replace("<", "<noparse><</noparse>");
    private static void Composer(RectTransform composer) {
        Paint(composer, ComposerBg, 2);
        if(DiscordSession.CurrentChannelId == null) {
            Label(Box(composer, "Hint", 16f, 0f, 400f, 44f), "pick a channel", 15f, TextMuted);
            return;
        }
        if(!DiscordSession.CanSendHere) {
            Paint(composer, PanelBg, 2);
            Label(
                Box(composer, "ReadOnly", 16f, 0f, 620f, 44f),
                "You do not have permission to send messages in this channel.",
                14f, TextLocked);
            return;
        }
        RectTransform field = Fill(composer, "Field", 8f, 4f, 108f, 4f);
        UIInput input = GenerateUI.Input(
            field, "", "", null, "Message #" + DiscordSession.CurrentChannelName, null, "discord_message", 0f);
        input.OnComplete = value => {
            if(string.IsNullOrWhiteSpace(value)) return;
            DiscordSession.Send(value);
            input.Set("", false);
        };
        RectTransform send = BoxRight(composer, "Send", 8f, 6f, 92f, 32f);
        Paint(send, Blurple, 1);
        Label(send, "Send", 13f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        Clickable(send, () => {
            DiscordSession.Send(input.Value);
            input.Set("", false);
        });
    }
    private static void Diagnostics(Transform content) {
        TextMeshProUGUI probe = GenerateUI.AddMutedText(GenerateUI.Row(content, 72f), 14f, 0.5f);
        void Update() {
            if(probe == null) return;
            probe.text = "HTTPS: " + DiscordNet.Https
                + "\nGateway: " + DiscordNet.Gateway
                + "\nCrypto: " + DiscordNet.Crypto;
        }
        GenerateUI.Button(
            GenerateUI.Row(content), () => DiscordNet.SelfTest(Update), "Test Connection", "discord_selftest");
        Update();
    }
    private static string voiceStatus = "";
    private static bool voiceInstalling;
    private static void VoiceSection(Transform content) {
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(content)), "DISCORD_VOICE", "Voice");
        TextMeshProUGUI state = GenerateUI.AddMutedText(GenerateUI.Row(content, 62f), 14f, 0.5f);
        void Update() {
            if(state == null) return;
            string rid = VoiceNatives.Rid();
            if(rid == null) {
                state.text = "No voice runtime is published for this platform.";
                return;
            }
            state.text = "Platform: " + rid
                + "\nRuntime: " + (VoiceNatives.InstalledVersion ?? "not installed")
                + (voiceStatus.Length == 0 ? "" : "\n" + voiceStatus);
        }
        GenerateUI.Button(
            GenerateUI.Row(content), () => InstallVoice(Update), "Install Voice Runtime", "discord_voice_install");
        GenerateUI.Button(
            GenerateUI.Row(content),
            () => {
                VoiceNatives.Uninstall();
                voiceStatus = "removed";
                Update();
            },
            "Remove Voice Runtime",
            "discord_voice_remove"
        ).SetSecondary();
        Update();
    }
    private static void InstallVoice(Action update) {
        if(voiceInstalling) return;
        voiceInstalling = true;
        voiceStatus = "starting...";
        update();
        Task.Run(async () => {
            bool installed = await VoiceNatives.InstallAsync(
                (stage, fraction) => {
                    voiceStatus = stage + " " + (int)(fraction * 100f) + "%";
                    MainThread.Enqueue(update);
                },
                CancellationToken.None);
            voiceStatus = installed ? "installed" : "install failed — see the log";
            voiceInstalling = false;
            MainThread.Enqueue(update);
        });
    }
    private static string NameOfGuild(string id) {
        foreach(DiscordGuild guild in DiscordSession.Guilds)
            if(guild.Id == id) return guild.Name;
        return "Discord";
    }
    private static string Initials(string name) {
        if(string.IsNullOrEmpty(name)) return "?";
        string trimmed = name.Trim();
        return trimmed.Length == 0 ? "?" : trimmed[..1].ToUpperInvariant();
    }
    private static Color ColorFor(string id) {
        if(string.IsNullOrEmpty(id)) return Blurple;
        int hash = 0;
        foreach(char c in id) hash = ((hash * 31) + c) & 0x7FFFFFF;
        return AvatarColors[hash % AvatarColors.Length];
    }
    private static void Clickable(RectTransform rect, Action onClick) {
        Button button = rect.gameObject.GetComponent<Button>();
        if(button == null) button = rect.gameObject.AddComponent<Button>();
        Image image = rect.gameObject.GetComponent<Image>();
        if(image != null) image.raycastTarget = true;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick());
    }
    private static RectTransform Node(Transform parent, string name) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static RectTransform Fill(Transform parent, string name, float left, float top, float right, float bottom) {
        RectTransform rect = Node(parent, name);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }
    private static RectTransform LeftBand(Transform parent, string name, float left, float width) {
        RectTransform rect = Node(parent, name);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(left, 0f);
        rect.sizeDelta = new Vector2(width, 0f);
        return rect;
    }
    private static RectTransform Box(Transform parent, string name, float x, float y, float width, float height) {
        RectTransform rect = Node(parent, name);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    private static RectTransform BoxRight(Transform parent, string name, float right, float y, float width, float height) {
        RectTransform rect = Node(parent, name);
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-right, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    private static RectTransform Strip(Transform parent, string name, float left, float y, float right, float height) {
        RectTransform rect = Node(parent, name);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -(y + height));
        rect.offsetMax = new Vector2(-right, -y);
        return rect;
    }
    private static RectTransform StripBottom(Transform parent, string name, float left, float y, float right, float height) {
        RectTransform rect = Node(parent, name);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(left, y);
        rect.offsetMax = new Vector2(-right, y + height);
        return rect;
    }
    private static RectTransform AutoRow(Transform parent, float x, float y, float height, float spacing) {
        RectTransform rect = Box(parent, "AutoRow", x, y, 10f, height);
        HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rect;
    }
    private static RectTransform Avatar(Transform parent, float x, float y, float size, Color color, string initial) {
        RectTransform rect = Box(parent, "Avatar", x, y, size, size);
        Paint(rect, color, 3);
        Label(rect, initial, size * 0.44f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        return rect;
    }
    private static void Status(RectTransform avatar, Color color, Color ring) {
        float size = Mathf.Round(avatar.sizeDelta.x * 0.4f);
        RectTransform outer = Node(avatar, "Status");
        outer.anchorMin = new Vector2(1f, 0f);
        outer.anchorMax = new Vector2(1f, 0f);
        outer.pivot = new Vector2(1f, 0f);
        outer.anchoredPosition = new Vector2(2f, -2f);
        outer.sizeDelta = new Vector2(size, size);
        Paint(outer, ring, 3);
        RectTransform inner = Node(outer, "Dot");
        inner.anchorMin = new Vector2(0.5f, 0.5f);
        inner.anchorMax = new Vector2(0.5f, 0.5f);
        inner.pivot = new Vector2(0.5f, 0.5f);
        inner.anchoredPosition = Vector2.zero;
        inner.sizeDelta = new Vector2(size - 5f, size - 5f);
        Paint(inner, color, 3);
    }
    private static Image Paint(RectTransform rect, Color color, int radius = 0) {
        Image image = rect.gameObject.GetComponent<Image>();
        if(image == null) image = rect.gameObject.AddComponent<Image>();
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        if(radius == 1) {
            image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
            image.type = Image.Type.Sliced;
        } else if(radius == 2) {
            image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
            image.type = Image.Type.Sliced;
        } else if(radius == 3) {
            image.sprite = MainCore.Spr.Get(UISprite.Circle256);
        }
        return image;
    }
    private static TextMeshProUGUI Label(
        Transform parent, string value, float size, Color color,
        FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.Left
    ) {
        TextMeshProUGUI text = GenerateUI.AddText(parent, true);
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = align;
        text.characterSpacing = 0f;
        text.raycastTarget = false;
        text.richText = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
}
