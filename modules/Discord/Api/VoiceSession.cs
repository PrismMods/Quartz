using Newtonsoft.Json.Linq;
using Quartz.Async;
using Quartz.Core;
namespace Quartz.Features.Discord;
public static class VoiceSession {
    public enum State { Idle, Requesting, Signalling, Connected, Failed }
    private static DiscordGateway attached;
    private static VoiceGateway voice;
    private static VoiceUdp udp;
    private static string pendingGuild;
    private static string pendingChannel;
    private static string sessionId;
    private static string voiceToken;
    private static string endpoint;
    private static bool starting;
    private static System.Diagnostics.Stopwatch alive;
    private static DaveSession dave;
    public static string DaveStatus { get; private set; } = "off";
    public static State Current { get; private set; } = State.Idle;
    public static string Status { get; private set; } = "";
    public static string ChannelId { get; private set; }
    public static string Mode { get; private set; } = "";
    public static bool SecretKeyReceived { get; private set; }
    public static event Action Changed;
    public static bool Connected => Current == State.Connected;
    private static int joinCount;
    public static void Join(string guildId, string channelId) {
        MainCore.Log.Msg($"[Discord] voice Join #{++joinCount} guild={guildId} channel={channelId}");
        DiscordGateway gateway = DiscordSession.Gateway;
        if(gateway == null) {
            Set(State.Failed, "not connected to Discord");
            return;
        }
        if(string.IsNullOrEmpty(guildId) || guildId == DiscordSession.DirectMessagesId) {
            Set(State.Failed, "voice is only wired up for server channels");
            return;
        }
        Reset(false);
        Attach(gateway);
        pendingGuild = guildId;
        pendingChannel = channelId;
        ChannelId = channelId;
        Set(State.Requesting, "asking Discord to move you...");
        Task.Run(async () => {
            try {
                await gateway.SendVoiceStateAsync(guildId, channelId, false, false);
            } catch(Exception e) {
                Set(State.Failed, "join failed: " + e.Message);
            }
        });
    }
    public static void Leave() {
        DiscordGateway gateway = DiscordSession.Gateway;
        string guildId = pendingGuild;
        Reset(true);
        Set(State.Idle, "left the voice channel");
        if(gateway == null || guildId == null) return;
        Task.Run(async () => {
            try {
                await gateway.SendVoiceStateAsync(guildId, null, false, false);
            } catch(Exception e) {
                Diag.Ignore(e);
            }
        });
    }
    private static void Attach(DiscordGateway gateway) {
        if(attached == gateway) return;
        if(attached != null) {
            attached.VoiceState -= OnVoiceState;
            attached.VoiceServer -= OnVoiceServer;
        }
        attached = gateway;
        gateway.VoiceState += OnVoiceState;
        gateway.VoiceServer += OnVoiceServer;
    }
    private static void OnVoiceState(string channelId, string session) {
        if(pendingChannel == null) return;
        if(channelId != pendingChannel) return;
        sessionId = session;
        TryStart();
    }
    private static void OnVoiceServer(string guildId, string token, string host) {
        if(pendingGuild == null || guildId != pendingGuild) return;
        voiceToken = token;
        endpoint = host;
        TryStart();
    }
    private static void TryStart() {
        if(starting || sessionId == null || voiceToken == null || endpoint == null) return;
        if(DiscordSession.SelfId == null) return;
        starting = true;
        ushort daveVersion = 0;
        if(DaveNative.Load()) {
            daveVersion = DaveNative.MaxProtocolVersion;
            DaveStatus = $"libdave ready, advertising v{daveVersion}";
        } else {
            DaveStatus = "off — " + DaveNative.LoadError;
        }
        Set(State.Signalling, "connecting to the voice server...");
        VoiceGateway created = new(
            endpoint, pendingGuild, DiscordSession.SelfId, sessionId, voiceToken, daveVersion);
        voice = created;
        created.Status += text => Set(Current, text);
        created.Error += text => Set(State.Failed, "voice: " + text);
        created.Closed += (code, reason) => {
            if(voice != created) return;
            double seconds = alive == null ? 0d : alive.Elapsed.TotalSeconds;
            MainCore.Log.Wrn(
                $"[Discord] voice CLOSED {code} '{reason}' after {seconds:F1}s "
                + $"(sent {VoiceAudio.FramesSent}, recv {VoiceAudio.FramesReceived})");
            Set(State.Failed, $"voice closed ({code}) {reason} after {seconds:F0}s");
        };
        created.Ready += (ssrc, ip, port, modes) => {
            if(voice != created) return;
            Set(State.Signalling, $"discovering our address via {ip}:{port}...");
            Task.Run(async () => {
                try {
                    VoiceUdp transport = new(ssrc);
                    udp = transport;
                    (string ourIp, int ourPort) = await transport.DiscoverAsync(ip, port);
                    string mode = VoiceUdp.PickMode(modes);
                    Mode = mode;
                    Set(State.Signalling, $"selecting {mode} from {ourIp}:{ourPort}...");
                    await created.SendSelectProtocolAsync(ourIp, ourPort, mode);
                } catch(Exception e) {
                    Set(State.Failed, "udp: " + e.Message);
                }
            });
        };
        created.Speaking += (userId, speakerSsrc) => {
            if(voice != created) return;
            dave?.MapSsrc(speakerSsrc, userId);
        };
        created.DaveBinary += (op, payload) => {
            if(voice != created) return;
            HandleDaveBinary(created, op, payload);
        };
        created.DaveControl += (op, data) => {
            if(voice != created) return;
            HandleDaveControl(created, op, data);
        };
        created.SessionDescription += (key, mode) => {
            if(voice != created) return;
            int keyLength = key == null ? 0 : key.Length;
            SecretKeyReceived = keyLength > 0;
            Mode = mode;
            if(keyLength == SodiumNative.KeyBytes && udp != null) {
                udp.SetKey(key);
                udp.Error += text => MainCore.Log.Wrn("[Discord] voice udp: " + text);
                udp.StartReceiving();
                VoiceAudio.Begin(udp, created);
                alive = System.Diagnostics.Stopwatch.StartNew();
                Send(created.SendSpeakingAsync(true, created.Ssrc));
            }
            Set(
                State.Connected,
                $"connected — {mode}, {(keyLength > 0 ? keyLength + "-byte key" : "no key")}");
        };
        Task.Run(async () => {
            try {
                await created.StartAsync();
            } catch(Exception e) {
                Set(State.Failed, "voice connect failed: " + e.Message);
            }
        });
    }
    private static void EnsureDave(VoiceGateway gateway) {
        if(dave != null || !DaveNative.Available || gateway == null) return;
        ulong groupId = ulong.TryParse(pendingChannel, out ulong parsed) ? parsed : 0UL;
        dave = new DaveSession(DaveNative.MaxProtocolVersion, groupId, DiscordSession.SelfId, (uint)gateway.Ssrc);
        VoiceAudio.Dave = dave;
    }
    private static IReadOnlyList<string> Recognized() =>
        DiscordSession.SelfId == null ? [] : new[] { DiscordSession.SelfId };
    private static (int TransitionId, byte[] Body) SplitTransition(byte[] payload) {
        if(payload.Length < 2) return (-1, payload);
        int id = (payload[0] << 8) | payload[1];
        byte[] body = new byte[payload.Length - 2];
        Buffer.BlockCopy(payload, 2, body, 0, body.Length);
        MainCore.Log.Msg($"[Discord] dave transition id={id}, body {body.Length}B of {payload.Length}B");
        return (id, body);
    }
    private static void HandleDaveBinary(VoiceGateway gateway, int op, byte[] payload) {
        try {
            switch(op) {
                case 25:
                    EnsureDave(gateway);
                    dave?.SetExternalSender(payload);
                    byte[] keyPackage = dave?.KeyPackage();
                    if(keyPackage != null && keyPackage.Length > 0) {
                        DaveStatus = "sent our key package";
                        Send(gateway.SendDaveBinaryAsync(26, keyPackage));
                    }
                    break;
                case 27:
                    byte[] commitWelcome = dave?.ProcessProposals(payload, Recognized());
                    if(commitWelcome != null) {
                        DaveStatus = "sent commit + welcome";
                        Send(gateway.SendDaveBinaryAsync(28, commitWelcome));
                    }
                    break;
                case 29: {
                    (int id, byte[] body) = SplitTransition(payload);
                    dave?.ProcessCommit(body);
                    DaveStatus = Describe();
                    if(id >= 0) Send(gateway.SendDaveTransitionReadyAsync(id));
                    break;
                }
                case 30: {
                    (int id, byte[] body) = SplitTransition(payload);
                    dave?.ProcessWelcome(body, Recognized());
                    DaveStatus = Describe();
                    if(id >= 0) Send(gateway.SendDaveTransitionReadyAsync(id));
                    break;
                }
                default:
                    MainCore.Log.Msg($"[Discord] dave: unhandled binary op {op} ({payload.Length}B)");
                    break;
            }
        } catch(Exception e) {
            DaveStatus = $"binary op {op} failed: {e.Message}";
            MainCore.Log.Wrn($"[Discord] dave binary op {op} failed: {e}");
        }
        Notify();
    }
    private static string Describe() =>
        dave == null ? "off"
        : dave.KeysReady ? $"encrypting media ({dave.RosterCount} in group)"
        : "waiting for a key ratchet";
    private static void HandleDaveControl(VoiceGateway gateway, int op, JToken data) {
        try {
            switch(op) {
                case 21: {
                    int id = Json.Int(data, "transition_id") ?? 0;
                    int version = Json.Int(data, "protocol_version") ?? -1;
                    if(version > 0) dave?.SetVersion((ushort)version);
                    DaveStatus = $"transition {id} to protocol v{version}";
                    Send(gateway.SendDaveTransitionReadyAsync(id));
                    break;
                }
                case 22:
                    dave?.RefreshRatchets();
                    DaveStatus = Describe();
                    break;
                case 24:
                    DaveStatus = "preparing a new epoch";
                    break;
            }
        } catch(Exception e) {
            DaveStatus = $"control op {op} failed: {e.Message}";
            MainCore.Log.Wrn($"[Discord] dave control op {op} failed: {e}");
        }
        Notify();
    }
    private static void Send(Task work) {
        if(work == null) return;
        _ = work.ContinueWith(
            t => MainCore.Log.Wrn("[Discord] dave send failed: " + t.Exception?.GetBaseException().Message),
            TaskContinuationOptions.OnlyOnFaulted);
    }
    public static void Refresh() => Notify();
    private static void Notify() {
        Action handler = Changed;
        if(handler != null) MainThread.Enqueue(handler);
    }
    private static void Reset(bool clearChannel) {
        VoiceAudio.End();
        starting = false;
        sessionId = null;
        voiceToken = null;
        endpoint = null;
        SecretKeyReceived = false;
        Mode = "";
        DaveSession previousDave = dave;
        dave = null;
        VoiceAudio.Dave = null;
        VoiceGateway previous = voice;
        VoiceUdp transport = udp;
        voice = null;
        udp = null;
        if(clearChannel) {
            ChannelId = null;
            pendingChannel = null;
            pendingGuild = null;
        }
        if(previous == null && transport == null && previousDave == null) return;
        Task.Run(() => {
            try {
                previous?.Dispose();
            } catch(Exception e) {
                Diag.Ignore(e);
            }
            try {
                transport?.Dispose();
            } catch(Exception e) {
                Diag.Ignore(e);
            }
            try {
                previousDave?.Dispose();
            } catch(Exception e) {
                Diag.Ignore(e);
            }
        });
    }
    private static void Set(State state, string status) {
        Current = state;
        Status = status;
        MainCore.Log.Msg($"[Discord] voice {state}: {status}");
        Notify();
    }
}
