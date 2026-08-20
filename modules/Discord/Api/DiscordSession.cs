using Quartz.Async;
using Quartz.Core;
namespace Quartz.Features.Discord;
public static class DiscordSession {
    public const string DirectMessagesId = "@me";
    private sealed class GuildView {
        public readonly List<DiscordChannel> Channels = [];
        public readonly Dictionary<string, string> Categories = [];
        public readonly HashSet<string> Locked = [];
        public readonly HashSet<string> ReadOnly = [];
    }
    private static readonly Dictionary<string, GuildView> guildCache = [];
    private static GuildView view = new();
    private static readonly Dictionary<string, List<DiscordMessage>> messageCache = [];
    private static CancellationTokenSource navCts;
    public static DiscordRest Rest { get; private set; }
    public static DiscordGateway Gateway { get; private set; }
    public static string SelfId { get; private set; }
    public static string SelfName { get; private set; }
    public static string Status { get; private set; } = "";
    public static bool Loading { get; private set; }
    public static bool SigningIn { get; private set; }
    public static bool LoggedIn => Rest != null && SelfId != null;
    public static List<DiscordGuild> Guilds { get; } = [];
    public static List<DiscordChannel> Channels { get; } = [];
    public static List<DiscordMessage> Messages { get; } = [];
    public static Dictionary<string, string> CategoryOf => view.Categories;
    public static bool IsLocked(string channelId) => channelId != null && view.Locked.Contains(channelId);
    public static bool IsReadOnly(string channelId) => channelId != null && view.ReadOnly.Contains(channelId);
    public static bool CanSendHere => CurrentChannelId != null && !IsReadOnly(CurrentChannelId);
    public static string CurrentGuildId { get; private set; }
    public static string CurrentChannelId { get; private set; }
    public static string CurrentChannelName { get; private set; } = "";
    public static int CurrentChannelType { get; private set; }
    public static event Action Changed;
    public static bool HasSavedToken => TokenStore.HasSaved;
    private static RemoteAuth auth;
    public static string QrUrl { get; private set; }
    public static string QrStatus { get; private set; } = "";
    public static bool QrActive => auth != null;
    public static void BeginQrLogin() {
        if(auth != null || SigningIn) return;
        QrUrl = null;
        QrStatus = "connecting...";
        RemoteAuth started = new();
        auth = started;
        started.QrUrl += url => {
            if(auth != started) return;
            QrUrl = url;
            Notify();
        };
        started.UserPreview += name => {
            if(auth != started) return;
            QrStatus = "approve the login as " + name;
            Notify();
        };
        started.Status += text => {
            if(auth != started) return;
            QrStatus = text;
            Notify();
        };
        started.Failed += text => {
            QrStatus = "failed: " + text;
            EndQr(started);
        };
        started.Token += token => {
            EndQr(started);
            Start(token, true);
        };
        Notify();
        Task.Run(async () => {
            try {
                await started.StartAsync();
            } catch(Exception e) {
                QrStatus = "failed: " + Describe(e);
                EndQr(started);
            }
        });
    }
    public static void CancelQrLogin() {
        RemoteAuth started = auth;
        if(started == null) return;
        QrStatus = "cancelled";
        EndQr(started);
    }
    private static void EndQr(RemoteAuth started) {
        if(auth == started) {
            auth = null;
            QrUrl = null;
        }
        Task.Run(() => {
            try {
                started.Dispose();
            } catch(Exception e) {
                Diag.Ignore(e);
            }
        });
        Notify();
    }
    public static void Resume() {
        if(LoggedIn || SigningIn) return;
        string token = TokenStore.Load();
        if(token != null) Start(token, false);
    }
    public static void LogIn(string token) {
        if(SigningIn) return;
        if(string.IsNullOrWhiteSpace(token)) {
            Set("enter a token first");
            return;
        }
        Start(token.Trim(), true);
    }
    private static void Start(string token, bool save) {
        SigningIn = true;
        Set("signing in...");
        Run(async () => {
            DiscordRest rest = new(token);
            (string id, string name) = await rest.GetSelfAsync();
            Rest = rest;
            SelfId = id;
            SelfName = name;
            if(save) {
                try {
                    TokenStore.Save(token);
                } catch(Exception e) {
                    MainCore.Log.Wrn($"[Discord] could not save the token: {e.Message}");
                }
            }
            List<DiscordGuild> guilds = await rest.GetGuildsAsync();
            Guilds.Clear();
            Guilds.AddRange(guilds);
            StartGateway(token);
            Set($"signed in as {name} — {guilds.Count} server(s)");
            MainThread.Enqueue(() => OpenGuild(DirectMessagesId));
        }, () => SigningIn = false);
    }
    private static void StartGateway(string token) {
        Gateway?.Dispose();
        Gateway = new DiscordGateway(token);
        Gateway.MessageCreated += OnMessageCreated;
        Gateway.MessageUpdated += OnMessageUpdated;
        Gateway.Status += text => Set(text);
        Gateway.Error += text => Set("gateway: " + text);
        Gateway.Start();
    }
    private static void OnMessageCreated(DiscordMessage message) {
        if(messageCache.TryGetValue(message.ChannelId, out List<DiscordMessage> cached)) cached.Add(message);
        if(message.ChannelId != CurrentChannelId) return;
        Messages.Add(message);
        Notify();
    }
    private static void OnMessageUpdated(DiscordMessage message) {
        if(messageCache.TryGetValue(message.ChannelId, out List<DiscordMessage> cached)) Replace(cached, message);
        if(message.ChannelId != CurrentChannelId) return;
        Replace(Messages, message);
        Notify();
    }
    private static void Replace(List<DiscordMessage> list, DiscordMessage message) {
        for(int i = 0; i < list.Count; i++)
            if(list[i].Id == message.Id) {
                list[i] = message;
                return;
            }
    }
    public static void OpenGuild(string guildId) {
        if(Rest == null || guildId == null || guildId == CurrentGuildId) return;
        CancellationToken ct = BeginNavigation();
        CurrentGuildId = guildId;
        Channels.Clear();
        Messages.Clear();
        CurrentChannelId = null;
        CurrentChannelName = "";
        if(guildCache.TryGetValue(guildId, out GuildView cached)) {
            view = cached;
            Channels.AddRange(cached.Channels);
            Loading = false;
            Set(Summary(cached));
            string first = FirstOpenable(cached);
            if(first != null) OpenChannel(first);
            return;
        }
        view = new GuildView();
        Loading = true;
        Set("loading channels...");
        Run(async () => {
            GuildView loaded = guildId == DirectMessagesId
                ? await DirectMessagesAsync()
                : await GuildChannelsAsync(guildId);
            if(ct.IsCancellationRequested || CurrentGuildId != guildId) return;
            guildCache[guildId] = loaded;
            view = loaded;
            Channels.Clear();
            Channels.AddRange(loaded.Channels);
            Loading = false;
            Set(Summary(loaded));
            string first = FirstOpenable(loaded);
            if(first != null) MainThread.Enqueue(() => OpenChannel(first));
        }, () => Loading = false);
    }
    private static string Summary(GuildView loaded) {
        if(loaded.Channels.Count == 0) return "no channels here";
        int locked = loaded.Locked.Count;
        return locked == 0
            ? $"{loaded.Channels.Count} channel(s)"
            : $"{loaded.Channels.Count} channel(s), {locked} locked";
    }
    public static bool IsChattable(int type) => type is 0 or 5 or 2 or 13 or 1 or 3;
    public static bool IsVoice(int type) => type is 2 or 13;
    private static string FirstOpenable(GuildView loaded) {
        foreach(DiscordChannel channel in loaded.Channels)
            if(!loaded.Locked.Contains(channel.Id) && !IsVoice(channel.Type)) return channel.Id;
        foreach(DiscordChannel channel in loaded.Channels)
            if(!loaded.Locked.Contains(channel.Id)) return channel.Id;
        return null;
    }
    private static async Task<GuildView> DirectMessagesAsync() {
        GuildView loaded = new();
        loaded.Channels.AddRange(await Rest.GetDmChannelsAsync());
        return loaded;
    }
    private static async Task<GuildView> GuildChannelsAsync(string guildId) {
        List<DiscordChannel> all = await Rest.GetChannelsAsync(guildId);
        GuildPerms perms = await Rest.GetGuildAsync(guildId);
        List<string> memberRoles = await Rest.GetSelfMemberRolesAsync(guildId);
        Dictionary<string, DiscordChannel> parents = [];
        foreach(DiscordChannel channel in all)
            if(channel.Type == 4) parents[channel.Id] = channel;
        GuildView loaded = new();
        List<DiscordChannel> text = [];
        foreach(DiscordChannel channel in all) {
            if(!IsChattable(channel.Type)) continue;
            text.Add(channel);
            ulong bits = DiscordPermissions.ChannelPermissions(
                guildId, perms.OwnerId, perms.Roles, SelfId, memberRoles, channel.Overwrites);
            if((bits & DiscordPermissions.ViewChannel) == 0) loaded.Locked.Add(channel.Id);
            else if((bits & DiscordPermissions.SendMessages) == 0) loaded.ReadOnly.Add(channel.Id);
        }
        text.Sort((a, b) => Compare(a, b, parents));
        loaded.Channels.AddRange(text);
        foreach(DiscordChannel channel in text)
            loaded.Categories[channel.Id] =
                channel.ParentId != null && parents.TryGetValue(channel.ParentId, out DiscordChannel parent)
                    ? parent.Name
                    : "";
        return loaded;
    }
    private static int Compare(DiscordChannel a, DiscordChannel b, Dictionary<string, DiscordChannel> parents) {
        (int positionA, string idA) = CategoryKey(a, parents);
        (int positionB, string idB) = CategoryKey(b, parents);
        if(positionA != positionB) return positionA.CompareTo(positionB);
        int byCategoryId = string.CompareOrdinal(idA, idB);
        if(byCategoryId != 0) return byCategoryId;
        int byPosition = a.Position.CompareTo(b.Position);
        return byPosition != 0 ? byPosition : string.CompareOrdinal(a.Id, b.Id);
    }
    private static (int, string) CategoryKey(DiscordChannel channel, Dictionary<string, DiscordChannel> parents) =>
        channel.ParentId != null && parents.TryGetValue(channel.ParentId, out DiscordChannel parent)
            ? (parent.Position, parent.Id)
            : (-1, "");
    public static void OpenChannel(string channelId) {
        if(Rest == null || channelId == null || channelId == CurrentChannelId) return;
        DiscordChannel? found = null;
        foreach(DiscordChannel channel in Channels)
            if(channel.Id == channelId) {
                found = channel;
                break;
            }
        if(found == null || IsLocked(channelId)) return;
        DiscordChannel target = found.Value;
        CancellationToken ct = BeginNavigation();
        CurrentChannelId = target.Id;
        CurrentChannelName = target.Name;
        CurrentChannelType = target.Type;
        Messages.Clear();
        if(messageCache.TryGetValue(target.Id, out List<DiscordMessage> cached)) {
            Messages.AddRange(cached);
            Loading = false;
            Set($"#{target.Name} — {cached.Count} message(s)");
            return;
        }
        Loading = true;
        Set("loading messages...");
        Run(async () => {
            List<DiscordMessage> history = await Rest.GetMessagesAsync(target.Id, 50);
            if(ct.IsCancellationRequested || CurrentChannelId != target.Id) return;
            messageCache[target.Id] = history;
            Messages.Clear();
            Messages.AddRange(history);
            Loading = false;
            Set($"#{target.Name} — {history.Count} message(s)");
            if(history.Count > 0) await Rest.AckAsync(target.Id, history[^1].Id);
        }, () => Loading = false);
    }
    private static CancellationToken BeginNavigation() {
        try {
            navCts?.Cancel();
        } catch(Exception e) {
            Diag.Ignore(e);
        }
        navCts?.Dispose();
        navCts = new CancellationTokenSource();
        return navCts.Token;
    }
    public static void Send(string text) {
        if(Rest == null || CurrentChannelId == null || string.IsNullOrWhiteSpace(text)) return;
        string channelId = CurrentChannelId;
        Run(async () => {
            await Rest.SendMessageAsync(channelId, text);
            Set("sent");
        }, null);
    }
    public static void LogOut() {
        try {
            navCts?.Cancel();
        } catch(Exception e) {
            Diag.Ignore(e);
        }
        Gateway?.Dispose();
        Gateway = null;
        Rest?.Dispose();
        Rest = null;
        SelfId = null;
        SelfName = null;
        CurrentGuildId = null;
        CurrentChannelId = null;
        CurrentChannelName = "";
        Loading = false;
        SigningIn = false;
        Guilds.Clear();
        Channels.Clear();
        Messages.Clear();
        view = new GuildView();
        guildCache.Clear();
        messageCache.Clear();
        UserCache.Clear();
        TokenStore.Clear();
        Set("signed out");
    }
    private static void Run(Func<Task> work, Action done) {
        Task.Run(async () => {
            try {
                await work();
            } catch(OperationCanceledException e) {
                Diag.Ignore(e);
            } catch(Exception e) {
                Set("failed: " + Describe(e));
                MainCore.Log.Wrn($"[Discord] {e}");
            } finally {
                done?.Invoke();
                Notify();
            }
        });
    }
    private static string Describe(Exception e) {
        Exception inner = e;
        while(inner.InnerException != null) inner = inner.InnerException;
        return inner.Message;
    }
    private static void Set(string status) {
        Status = status;
        Notify();
    }
    private static void Notify() {
        Action handler = Changed;
        if(handler == null) return;
        MainThread.Enqueue(handler);
    }
}
