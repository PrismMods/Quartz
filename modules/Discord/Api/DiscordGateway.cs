using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json.Linq;
using Quartz.Async;
using Quartz.Core;
using static Quartz.Features.Discord.Json;
namespace Quartz.Features.Discord;
public sealed class DiscordGateway : IDisposable {
    private const string GatewayUrl = "wss://gateway.discord.gg/?v=10&encoding=json";
    private const int ReconnectSeconds = 5;
    private readonly string token;
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly Dictionary<string, string> userNames = [];
    private ClientWebSocket socket;
    private CancellationTokenSource outerCts;
    private CancellationTokenSource connCts;
    private int? seq;
    private int attempts;
    private bool disposed;
    public event Action<DiscordMessage> MessageCreated;
    public event Action<DiscordMessage> MessageUpdated;
    public event Action<string> Ready;
    public event Action<IReadOnlyList<ReadStateEntry>> ReadState;
    public event Action<IReadOnlyDictionary<string, string>> ChannelGuildMap;
    public event Action<string, string> MessageAck;
    public event Action<ReactionUpdate> ReactionChanged;
    public event Action<string, string> VoiceState;
    public event Action<string, string, string> VoiceServer;
    public string SelfId { get; private set; }
    public event Action<string> Status;
    public event Action<string> Error;
    public DiscordGateway(string token) => this.token = token;
    public void Start() {
        outerCts = new CancellationTokenSource();
        _ = Task.Run(() => RunLoopAsync(outerCts.Token));
    }
    private async Task RunLoopAsync(CancellationToken outer) {
        while(!outer.IsCancellationRequested) {
            connCts = CancellationTokenSource.CreateLinkedTokenSource(outer);
            CancellationToken ct = connCts.Token;
            seq = null;
            try {
                socket = new ClientWebSocket();
                Raise(Status, "connecting to the gateway...");
                await socket.ConnectAsync(new Uri(GatewayUrl), ct);
                MainCore.Log.Msg($"[Discord] main gateway connected (attempt {++attempts})");
                Raise(Status, "gateway connected");
                await ReceiveLoopAsync(ct);
            } catch(OperationCanceledException e) {
                Diag.Ignore(e);
            } catch(Exception e) {
                MainCore.Log.Wrn("[Discord] main gateway threw: " + e.Message);
                Raise(Error, e.Message);
            } finally {
                try {
                    connCts.Cancel();
                } catch(Exception e) {
                    Diag.Ignore(e);
                }
                socket?.Dispose();
                socket = null;
            }
            if(outer.IsCancellationRequested) break;
            MainCore.Log.Wrn(
                $"[Discord] main gateway DROPPED — reconnecting in {ReconnectSeconds}s; "
                + "any voice session is now stale (4006)");
            Raise(Status, $"gateway dropped — reconnecting in {ReconnectSeconds}s");
            try {
                await Task.Delay(TimeSpan.FromSeconds(ReconnectSeconds), outer);
            } catch(OperationCanceledException e) {
                Diag.Ignore(e);
                break;
            }
        }
    }
    private async Task ReceiveLoopAsync(CancellationToken ct) {
        byte[] buffer = new byte[64 * 1024];
        using MemoryStream frame = new();
        while(socket != null && socket.State == WebSocketState.Open && !ct.IsCancellationRequested) {
            frame.SetLength(0);
            WebSocketReceiveResult result;
            do {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if(result.MessageType == WebSocketMessageType.Close) {
                    Raise(Status, $"gateway close {(int?)result.CloseStatus} {result.CloseStatusDescription}");
                    return;
                }
                frame.Write(buffer, 0, result.Count);
            } while(!result.EndOfMessage);
            string text = Encoding.UTF8.GetString(frame.GetBuffer(), 0, (int)frame.Length);
            await HandlePayloadAsync(Parse(text), ct);
        }
    }
    private async Task HandlePayloadAsync(JToken payload, CancellationToken ct) {
        int op = Int(payload, "op") ?? -1;
        int? sequence = Int(payload, "s");
        if(sequence.HasValue) seq = sequence;
        switch(op) {
            case 10:
                int interval = Int(Prop(payload, "d"), "heartbeat_interval") ?? 41250;
                _ = Task.Run(() => HeartbeatLoopAsync(interval, ct), ct);
                await IdentifyAsync(ct);
                break;
            case 0:
                try {
                    HandleDispatch(payload);
                } catch(Exception e) {
                    MainCore.Log.Wrn($"[Discord] dispatch '{Str(payload, "t") ?? "?"}' threw: {e.Message}");
                }
                break;
            case 1:
                await SendHeartbeatAsync(ct);
                break;
            case 7:
            case 9:
                MainCore.Log.Wrn($"[Discord] main gateway op {op} — server asked us to reset the connection");
                Raise(Status, $"gateway op {op} — resetting the connection");
                try {
                    connCts?.Cancel();
                } catch(Exception e) {
                    Diag.Ignore(e);
                }
                break;
        }
    }
    private void HandleDispatch(JToken payload) {
        JToken d = Prop(payload, "d");
        switch(Str(payload, "t")) {
            case "READY":
                CacheUsers(d);
                Raise(Ready, DisplayName(Prop(d, "user")));
                ParseReadState(d);
                ParseChannelGuildMap(d);
                break;
            case "MESSAGE_CREATE":
                RaiseMessage(MessageCreated, d);
                break;
            case "MESSAGE_UPDATE":
                RaiseMessage(MessageUpdated, d);
                break;
            case "MESSAGE_ACK":
                string ackChannel = Str(d, "channel_id");
                string ackMessage = Str(d, "message_id");
                if(ackChannel != null && ackMessage != null && MessageAck != null) {
                    Action<string, string> handler = MessageAck;
                    MainThread.Enqueue(() => handler(ackChannel, ackMessage));
                }
                break;
            case "MESSAGE_REACTION_ADD":
                EmitReaction(d, true);
                break;
            case "MESSAGE_REACTION_REMOVE":
                EmitReaction(d, false);
                break;
            case "VOICE_STATE_UPDATE":
                if(Str(d, "user_id") == SelfId) {
                    string tail = Str(d, "session_id") ?? "";
                    if(tail.Length > 6) tail = tail[^6..];
                    MainCore.Log.Msg(
                        $"[Discord] VOICE_STATE_UPDATE self guild={Str(d, "guild_id")} "
                        + $"chan={Str(d, "channel_id") ?? "(none)"} sess=…{tail}");
                }
                if(Str(d, "user_id") == SelfId && VoiceState != null) {
                    string voiceChannel = Str(d, "channel_id");
                    string voiceSession = Str(d, "session_id");
                    Action<string, string> voiceHandler = VoiceState;
                    MainThread.Enqueue(() => voiceHandler(voiceChannel, voiceSession));
                }
                break;
            case "VOICE_SERVER_UPDATE":
                MainCore.Log.Msg(
                    $"[Discord] VOICE_SERVER_UPDATE guild={Str(d, "guild_id")} "
                    + $"endpoint={Str(d, "endpoint")} hasToken={Str(d, "token") != null}");
                if(VoiceServer != null) {
                    string voiceGuild = Str(d, "guild_id");
                    string voiceToken = Str(d, "token");
                    string voiceEndpoint = Str(d, "endpoint");
                    Action<string, string, string> serverHandler = VoiceServer;
                    MainThread.Enqueue(() => serverHandler(voiceGuild, voiceToken, voiceEndpoint));
                }
                break;
        }
    }
    private void RaiseMessage(Action<DiscordMessage> handler, JToken d) {
        if(handler == null || d == null) return;
        DiscordMessage message = DiscordRest.ParseMessage(d);
        MainThread.Enqueue(() => handler(message));
    }
    private void CacheUsers(JToken d) {
        JObject self = Obj(d, "user");
        if(self != null) {
            string id = Str(self, "id");
            if(id != null) {
                userNames[id] = DisplayName(self);
                SelfId = id;
            }
        }
        JArray users = Arr(d, "users");
        if(users == null) return;
        foreach(JToken user in users) {
            string id = Str(user, "id");
            if(id != null) userNames[id] = DisplayName(user);
        }
    }
    public string NameOf(string userId) =>
        userId != null && userNames.TryGetValue(userId, out string name) ? name : UserCache.Resolve(userId);
    private void ParseReadState(JToken d) {
        JToken raw = Prop(d, "read_state");
        JArray entries = raw as JArray ?? Arr(raw, "entries");
        if(entries == null) return;
        List<ReadStateEntry> list = [];
        foreach(JToken entry in entries) {
            string id = Str(entry, "id");
            if(id == null) continue;
            list.Add(new ReadStateEntry(id, Str(entry, "last_message_id"), Int(entry, "mention_count") ?? 0));
        }
        if(list.Count == 0 || ReadState == null) return;
        Action<IReadOnlyList<ReadStateEntry>> handler = ReadState;
        MainThread.Enqueue(() => handler(list));
    }
    private void ParseChannelGuildMap(JToken d) {
        JArray guilds = Arr(d, "guilds");
        if(guilds == null) return;
        Dictionary<string, string> map = [];
        foreach(JToken guild in guilds) {
            string guildId = Str(guild, "id");
            if(guildId == null) continue;
            JArray channels = Arr(guild, "channels");
            if(channels == null) continue;
            foreach(JToken channel in channels) {
                string channelId = Str(channel, "id");
                if(channelId != null) map[channelId] = guildId;
            }
        }
        if(map.Count == 0 || ChannelGuildMap == null) return;
        Action<IReadOnlyDictionary<string, string>> handler = ChannelGuildMap;
        MainThread.Enqueue(() => handler(map));
    }
    private void EmitReaction(JToken d, bool added) {
        string channelId = Str(d, "channel_id");
        string messageId = Str(d, "message_id");
        if(channelId == null || messageId == null || ReactionChanged == null) return;
        JObject emoji = Obj(d, "emoji");
        ReactionUpdate update = new(
            channelId,
            messageId,
            Str(d, "user_id") ?? "",
            emoji == null ? null : Str(emoji, "id"),
            emoji == null ? "" : Str(emoji, "name") ?? "",
            emoji != null && Flag(emoji, "animated"),
            added);
        Action<ReactionUpdate> handler = ReactionChanged;
        MainThread.Enqueue(() => handler(update));
    }
    private static string DisplayName(JToken user) =>
        user == null ? "?" : Str(user, "global_name") ?? Str(user, "username") ?? "?";
    private Task IdentifyAsync(CancellationToken ct) {
        object identify = new {
            op = 2,
            d = new {
                token,
                capabilities = 16381,
                properties = new {
                    os = "Mac OS X",
                    browser = "Chrome",
                    device = "",
                    system_locale = "en-US",
                    browser_user_agent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) "
                        + "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                    browser_version = "120.0.0.0",
                    os_version = "10.15.7",
                    referrer = "",
                    referring_domain = "",
                    release_channel = "stable",
                    client_build_number = 250000,
                },
                presence = new { status = "online", since = 0, activities = Array.Empty<object>(), afk = false },
                compress = false,
                client_state = new { guild_versions = new { } },
            },
        };
        Raise(Status, "identify sent");
        return SendJsonAsync(identify, ct);
    }
    private async Task HeartbeatLoopAsync(int intervalMs, CancellationToken ct) {
        try {
            await Task.Delay((int)(intervalMs * 0.5), ct);
            while(!ct.IsCancellationRequested && socket != null && socket.State == WebSocketState.Open) {
                await SendHeartbeatAsync(ct);
                await Task.Delay(intervalMs, ct);
            }
        } catch(OperationCanceledException e) {
            Diag.Ignore(e);
        } catch(Exception e) {
            Raise(Error, "heartbeat: " + e.Message);
        }
    }
    private Task SendHeartbeatAsync(CancellationToken ct) => SendJsonAsync(new { op = 1, d = seq }, ct);
    public Task SendVoiceStateAsync(string guildId, string channelId, bool selfMute, bool selfDeaf) =>
        SendJsonAsync(
            new {
                op = 4,
                d = new { guild_id = guildId, channel_id = channelId, self_mute = selfMute, self_deaf = selfDeaf },
            },
            connCts?.Token ?? CancellationToken.None);
    private async Task SendJsonAsync(object payload, CancellationToken ct) {
        if(socket == null || socket.State != WebSocketState.Open) return;
        byte[] bytes = Encoding.UTF8.GetBytes(Serialize(payload));
        await sendLock.WaitAsync(ct);
        try {
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        } finally {
            sendLock.Release();
        }
    }
    private static void Raise(Action<string> handler, string value) {
        if(handler == null) return;
        MainThread.Enqueue(() => handler(value));
    }
    public void Dispose() {
        if(disposed) return;
        disposed = true;
        try {
            outerCts?.Cancel();
        } catch(Exception e) {
            Diag.Ignore(e);
        }
        try {
            if(socket != null && socket.State == WebSocketState.Open)
                socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                    .GetAwaiter().GetResult();
        } catch(Exception e) {
            Diag.Ignore(e);
        }
        socket?.Dispose();
        connCts?.Dispose();
        outerCts?.Dispose();
        sendLock.Dispose();
    }
}
