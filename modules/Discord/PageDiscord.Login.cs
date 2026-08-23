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
public static partial class PageDiscord {
    private static string pendingToken = "";
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
}
