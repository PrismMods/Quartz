using Quartz.Core;
using Quartz.Features.Discord;
using Quartz.Resource;
using Quartz.UI.Generator;
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
    private static readonly Color Gold = new(0.941f, 0.698f, 0.196f);
    private static readonly Color Red = new(0.949f, 0.247f, 0.263f);
    private static readonly Color Link = new(0f, 0.659f, 0.988f);
    private static readonly Color Shell = new(0.208f, 0.216f, 0.235f);
    public static void Create(RectTransform parent) {
        Transform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(content)), "SECTION_DISCORD", "Discord");
        GenerateUI.AddLocalizedMutedText(
            GenerateUI.Row(content, 34f),
            "DISCORD_PREVIEW_NOTE",
            "Layout preview only — nothing here is connected to Discord yet."
        );
        TextMeshProUGUI status = GenerateUI.AddMutedText(GenerateUI.Row(content, 72f), 15f, 0.6f);
        void Refresh() {
            if(status == null) return;
            status.text = "HTTPS: " + DiscordNet.Https
                + "\nGateway: " + DiscordNet.Gateway
                + "\nCrypto: " + DiscordNet.Crypto;
        }
        GenerateUI.Button(
            GenerateUI.Row(content),
            () => DiscordNet.SelfTest(Refresh),
            "Test Connection",
            "discord_selftest"
        );
        Refresh();
        RectTransform shell = GenerateUI.Row(content, 660f);
        shell.gameObject.AddComponent<ThemeExempt>();
        RectTransform client = Fill(shell, "Client", 4f, 0f, 4f, 0f);
        Paint(client, Shell, 2);
        client.gameObject.AddComponent<RectMask2D>();
        Rail(LeftBand(client, "Rail", 0f, 72f));
        Sidebar(LeftBand(client, "Sidebar", 72f, 240f));
        Chat(Fill(client, "Chat", 312f, 0f, 210f, 0f));
        Members(RightBand(client, "Members", 0f, 210f));
    }
    private static void Rail(RectTransform rail) {
        Paint(rail, RailBg);
        RectTransform home = Box(rail, "Home", 12f, 12f, 48f, 48f);
        Paint(home, Blurple, 2);
        Icon(Box(home, "Logo", 10f, 10f, 28f, 28f), UISprite.QuartzLogo, Color.white);
        Paint(Box(rail, "Separator", 20f, 72f, 32f, 2f), Divider, 1);
        Server(rail, 84f, "A", new Color(0.345f, 0.396f, 0.949f), true);
        Server(rail, 140f, "Q", new Color(0.137f, 0.647f, 0.353f), false);
        Server(rail, 196f, "M", new Color(0.831f, 0.361f, 0.361f), false);
        RectTransform add = Box(rail, "AddServer", 12f, 252f, 48f, 48f);
        Paint(add, ChatBg, 2);
        Label(add, "+", 26f, Green, FontStyles.Normal, TextAlignmentOptions.Center);
    }
    private static void Server(RectTransform rail, float y, string initial, Color color, bool selected) {
        RectTransform icon = Box(rail, "Server", 12f, y, 48f, 48f);
        Paint(icon, color, 2);
        Label(icon, initial, 20f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        if(!selected) return;
        Paint(Box(rail, "Pill", 0f, y + 8f, 4f, 32f), Color.white, 1);
    }
    private static void Sidebar(RectTransform sidebar) {
        Paint(sidebar, SidebarBg);
        RectTransform head = Strip(sidebar, "Head", 0f, 0f, 0f, 48f);
        Label(Box(head, "Name", 16f, 0f, 176f, 48f), "Quartz Community", 16f, TextBright, FontStyles.Bold);
        Icon(BoxRight(head, "Chevron", 14f, 19f, 12f, 12f), UISprite.ChevronDown128, TextMuted);
        Paint(Strip(sidebar, "HeadLine", 0f, 47f, 0f, 1f), HeadLine);
        CategoryLabel(sidebar, 62f, "TEXT CHANNELS");
        Channel(sidebar, 86f, "general", true);
        Channel(sidebar, 120f, "adofai-chat", false);
        Channel(sidebar, 154f, "mod-support", false);
        Channel(sidebar, 188f, "screenshots", false);
        CategoryLabel(sidebar, 230f, "VOICE CHANNELS");
        RectTransform voice = Strip(sidebar, "Voice", 8f, 254f, 8f, 32f);
        Icon(Box(voice, "VoiceIcon", 8f, 8f, 16f, 16f), UISprite.Users128, TextMuted);
        Label(Fill(voice, "VoiceName", 32f, 0f, 8f, 0f), "Charting Lounge", 15f, TextMuted);
        RectTransform inVoice = Box(sidebar, "InVoice", 40f, 290f, 190f, 26f);
        Avatar(inVoice, 0f, 3f, 20f, Green, "O");
        Label(Box(inVoice, "InVoiceName", 28f, 0f, 150f, 26f), "otto", 14f, TextMuted);
        UserPanel(StripBottom(sidebar, "UserPanel", 0f, 0f, 0f, 52f));
    }
    private static void UserPanel(RectTransform panel) {
        Paint(panel, PanelBg);
        RectTransform avatar = Avatar(panel, 8f, 10f, 32f, Gold, "K");
        Status(avatar, Green, PanelBg);
        Label(Box(panel, "Name", 48f, 8f, 92f, 18f), "koren", 14f, TextBright, FontStyles.Bold);
        Label(Box(panel, "Tag", 48f, 26f, 92f, 16f), "Online", 12f, TextMuted);
        Icon(BoxRight(panel, "Mic", 68f, 16f, 20f, 20f), UISprite.Note128, TextMuted);
        Icon(BoxRight(panel, "Headset", 42f, 16f, 20f, 20f), UISprite.Monitor128, TextMuted);
        Icon(BoxRight(panel, "Settings", 16f, 16f, 20f, 20f), UISprite.Gear128, TextMuted);
    }
    private static void CategoryLabel(RectTransform parent, float y, string text) =>
        Label(Strip(parent, "Category", 16f, y, 8f, 18f), text, 12f, TextMuted, FontStyles.Bold);
    private static void Channel(RectTransform parent, float y, string name, bool selected) {
        RectTransform row = Strip(parent, "Channel", 8f, y, 8f, 32f);
        if(selected) Paint(row, ChannelActive, 1);
        Color fg = selected ? TextBright : TextMuted;
        Label(Box(row, "Hash", 8f, 0f, 18f, 32f), "#", 18f, fg, FontStyles.Normal, TextAlignmentOptions.Center);
        Label(Fill(row, "Name", 30f, 0f, 8f, 0f), name, 15f, fg);
    }
    private static void Chat(RectTransform chat) {
        Paint(chat, ChatBg);
        RectTransform head = Strip(chat, "ChatHead", 0f, 0f, 0f, 48f);
        Label(Box(head, "Hash", 16f, 0f, 20f, 48f), "#", 20f, TextMuted, FontStyles.Normal, TextAlignmentOptions.Center);
        Label(Box(head, "Title", 40f, 0f, 140f, 48f), "general", 16f, TextBright, FontStyles.Bold);
        Paint(Box(head, "Split", 190f, 16f, 1f, 16f), Divider);
        Label(Box(head, "Topic", 204f, 0f, 320f, 48f), "modding, charts and general chaos", 13f, TextMuted);
        Icon(BoxRight(head, "Members", 16f, 14f, 20f, 20f), UISprite.Users128, TextMuted);
        Icon(BoxRight(head, "Search", 48f, 14f, 20f, 20f), UISprite.MagnifyingGlass128, TextMuted);
        Icon(BoxRight(head, "Help", 80f, 14f, 20f, 20f), UISprite.QuestionMarkCircle128, TextMuted);
        Paint(Strip(chat, "ChatHeadLine", 0f, 47f, 0f, 1f), HeadLine);
        RectTransform messages = Fill(chat, "Messages", 0f, 48f, 0f, 72f);
        messages.gameObject.AddComponent<RectMask2D>();
        Messages(messages);
        Composer(StripBottom(chat, "Composer", 16f, 16f, 16f, 44f));
        Label(StripBottom(chat, "Typing", 20f, 2f, 16f, 12f), "otto is typing...", 11f, TextMuted);
    }
    private static void Messages(RectTransform area) {
        Paint(Strip(area, "DayLine", 16f, 19f, 16f, 1f), Divider);
        RectTransform day = BoxCenter(area, "Day", 10f, 76f, 18f);
        Paint(day, ChatBg);
        Label(day, "TODAY", 11f, TextMuted, FontStyles.Bold, TextAlignmentOptions.Center);
        Avatar(area, 16f, 40f, 40f, Gold, "K");
        RectTransform headA = AutoRow(area, 72f, 40f, 24f, 8f);
        Label(headA, "koren", 16f, Gold, FontStyles.Bold);
        Chip(headA, "MOD", Blurple, 34f, 15f);
        Label(headA, "Today at 4:20 PM", 11f, TextMuted);
        Label(Strip(area, "BodyA", 72f, 66f, 16f, 22f), "pushed the Others tab — Discord and Minecraft modules are in", 15f, TextNormal);
        Label(Strip(area, "BodyA2", 72f, 92f, 16f, 22f), "the minecraft one is a placeholder for now", 15f, TextNormal);
        Paint(Box(area, "ReplySpineV", 46f, 128f, 2f, 12f), Divider);
        Paint(Box(area, "ReplySpineH", 46f, 128f, 26f, 2f), Divider);
        RectTransform reply = AutoRow(area, 76f, 118f, 20f, 6f);
        MiniAvatar(reply, 16f, Gold, "K");
        Label(reply, "koren", 12f, TextMuted, FontStyles.Bold);
        Label(reply, "pushed the Others tab — Discord and Minecraft modules are in", 12f, TextMuted);
        Avatar(area, 16f, 142f, 40f, Green, "O");
        RectTransform headB = AutoRow(area, 72f, 142f, 24f, 8f);
        Label(headB, "otto", 16f, Green, FontStyles.Bold);
        Label(headB, "Today at 4:21 PM", 11f, TextMuted);
        Label(Strip(area, "BodyB", 72f, 168f, 16f, 22f), "the embed preview looks great", 15f, TextNormal);
        Avatar(area, 16f, 200f, 40f, Blurple, "Q");
        RectTransform headC = AutoRow(area, 72f, 200f, 24f, 8f);
        Label(headC, "Quartz", 16f, Blurple, FontStyles.Bold);
        Chip(headC, "BOT", Blurple, 32f, 15f);
        Label(headC, "Today at 4:22 PM", 11f, TextMuted);
        Label(Strip(area, "BodyC", 72f, 226f, 16f, 22f), "a new release just went out", 15f, TextNormal);
        Embed(Box(area, "Embed", 72f, 252f, 460f, 232f));
        RectTransform reactions = AutoRow(area, 72f, 490f, 26f, 6f);
        Reaction(reactions, Green, "12");
        Reaction(reactions, Gold, "7");
        Reaction(reactions, Red, "3");
    }
    private static void Embed(RectTransform embed) {
        Paint(embed, SidebarBg, 1);
        Paint(Box(embed, "Accent", 0f, 0f, 4f, 232f), Blurple, 1);
        RectTransform author = AutoRow(embed, 16f, 12f, 20f, 8f);
        MiniAvatar(author, 20f, Blurple, "Q");
        Label(author, "Quartz", 13f, TextBright, FontStyles.Bold);
        Label(Box(embed, "Title", 16f, 38f, 300f, 24f), "Quartz v2.0.0-alpha-119", 16f, Link, FontStyles.Bold);
        TextMeshProUGUI desc = Label(
            Box(embed, "Desc", 16f, 64f, 300f, 42f),
            "Adds the Others tab with the Discord and Minecraft modules, plus a handful of Key Viewer fixes.",
            13f, TextNormal, FontStyles.Normal, TextAlignmentOptions.TopLeft
        );
        desc.overflowMode = TextOverflowModes.Truncate;
        Field(embed, 16f, 112f, "Downloads", "1,204");
        Field(embed, 176f, 112f, "Stars", "128");
        RectTransform thumb = BoxRight(embed, "Thumb", 12f, 12f, 80f, 80f);
        Paint(thumb, RailBg, 1);
        Icon(Box(thumb, "ThumbArt", 20f, 20f, 40f, 40f), UISprite.QuartzLogo, new Color(1f, 1f, 1f, 0.85f));
        RectTransform image = Box(embed, "Image", 16f, 156f, 428f, 46f);
        Paint(image, RailBg, 1);
        Icon(Box(image, "ImageArt", 12f, 11f, 24f, 24f), UISprite.Image128, new Color(1f, 1f, 1f, 0.35f));
        Label(Box(image, "ImageName", 46f, 0f, 300f, 46f), "release-notes.png", 12f, TextMuted);
        RectTransform footer = AutoRow(embed, 16f, 206f, 16f, 6f);
        MiniAvatar(footer, 16f, Divider, "G");
        Label(footer, "GitHub  -  Today at 4:22 PM", 11f, TextMuted);
    }
    private static void Field(RectTransform embed, float x, float y, string title, string value) {
        Label(Box(embed, "FieldTitle", x, y, 140f, 16f), title, 12f, TextBright, FontStyles.Bold);
        Label(Box(embed, "FieldValue", x, y + 18f, 140f, 18f), value, 13f, TextNormal);
    }
    private static void Reaction(RectTransform row, Color color, string count) {
        RectTransform pill = Chip(row, "", ChannelActive, 56f, 24f);
        Paint(Box(pill, "Emoji", 8f, 6f, 12f, 12f), color, 3);
        Label(Box(pill, "Count", 26f, 0f, 24f, 24f), count, 12f, TextNormal, FontStyles.Bold);
    }
    private static void Composer(RectTransform composer) {
        Paint(composer, ComposerBg, 2);
        RectTransform plus = Box(composer, "Plus", 12f, 10f, 24f, 24f);
        Paint(plus, TextMuted, 3);
        Label(plus, "+", 18f, ComposerBg, FontStyles.Bold, TextAlignmentOptions.Center);
        Label(Box(composer, "Placeholder", 48f, 0f, 320f, 44f), "Message #general", 15f, TextMuted);
        Icon(BoxRight(composer, "Gift", 16f, 12f, 20f, 20f), UISprite.Star128, TextMuted);
        Icon(BoxRight(composer, "Attach", 48f, 12f, 20f, 20f), UISprite.Image128, TextMuted);
    }
    private static void Members(RectTransform members) {
        Paint(members, SidebarBg);
        CategoryLabel(members, 16f, "ONLINE - 3");
        Member(members, 40f, "koren", Gold, "K", Green, 1f);
        Member(members, 82f, "otto", Green, "O", Gold, 1f);
        Member(members, 124f, "quartz", Blurple, "Q", Green, 1f);
        CategoryLabel(members, 178f, "OFFLINE - 2");
        Member(members, 202f, "chartmaker", TextMuted, "C", Divider, 0.4f);
        Member(members, 244f, "beatbot", TextMuted, "B", Divider, 0.4f);
    }
    private static void Member(RectTransform parent, float y, string name, Color color, string initial, Color status, float alpha) {
        RectTransform row = Strip(parent, "Member", 8f, y, 8f, 40f);
        RectTransform avatar = Avatar(row, 8f, 4f, 32f, Fade(color, alpha), initial);
        Status(avatar, Fade(status, alpha), SidebarBg);
        Label(Box(row, "Name", 50f, 0f, 130f, 40f), name, 15f, Fade(color, alpha), FontStyles.Bold);
    }
    private static Color Fade(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
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
    private static RectTransform RightBand(Transform parent, string name, float right, float width) {
        RectTransform rect = Node(parent, name);
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-right, 0f);
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
    private static RectTransform BoxCenter(Transform parent, string name, float y, float width, float height) {
        RectTransform rect = Node(parent, name);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -y);
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
    private static RectTransform Chip(Transform row, string text, Color background, float width, float height) {
        GameObject obj = new("Chip");
        obj.transform.SetParent(row, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        LayoutElement element = obj.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.minWidth = width;
        element.preferredHeight = height;
        element.minHeight = height;
        Paint(rect, background, 1);
        if(!string.IsNullOrEmpty(text))
            Label(rect, text, 10f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        return rect;
    }
    private static RectTransform MiniAvatar(Transform row, float size, Color color, string initial) {
        RectTransform rect = Chip(row, "", color, size, size);
        Image image = rect.GetComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISprite.Circle256);
        image.type = Image.Type.Simple;
        Label(rect, initial, size * 0.5f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
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
        Image image = rect.gameObject.AddComponent<Image>();
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
    private static Image Icon(RectTransform rect, UISprite sprite, Color color) {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(sprite);
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = true;
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
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
}
