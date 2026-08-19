namespace Quartz.Features.Discord;
public static class DiscordPermissions {
    public const ulong Administrator = 1UL << 3;
    public const ulong ViewChannel = 1UL << 10;
    public static bool CanView(
        string guildId, string ownerId, IReadOnlyList<DiscordRole> roles,
        string userId, IReadOnlyList<string> memberRoles, IReadOnlyList<PermissionOverwrite> overwrites
    ) => (ChannelPermissions(guildId, ownerId, roles, userId, memberRoles, overwrites) & ViewChannel) != 0;
    public static ulong ChannelPermissions(
        string guildId, string ownerId, IReadOnlyList<DiscordRole> roles,
        string userId, IReadOnlyList<string> memberRoles, IReadOnlyList<PermissionOverwrite> overwrites
    ) {
        if(!string.IsNullOrEmpty(userId) && userId == ownerId) return ulong.MaxValue;
        Dictionary<string, ulong> rolePerms = [];
        foreach(DiscordRole role in roles) rolePerms[role.Id] = role.Permissions;
        ulong basePerms = rolePerms.TryGetValue(guildId, out ulong everyoneBits) ? everyoneBits : 0UL;
        foreach(string roleId in memberRoles)
            if(rolePerms.TryGetValue(roleId, out ulong bits)) basePerms |= bits;
        if((basePerms & Administrator) != 0) return ulong.MaxValue;
        ulong perms = basePerms;
        Dictionary<string, PermissionOverwrite> byId = [];
        foreach(PermissionOverwrite overwrite in overwrites) byId[overwrite.Id] = overwrite;
        if(byId.TryGetValue(guildId, out PermissionOverwrite everyone)) {
            perms &= ~everyone.Deny;
            perms |= everyone.Allow;
        }
        ulong allow = 0UL;
        ulong deny = 0UL;
        foreach(string roleId in memberRoles)
            if(byId.TryGetValue(roleId, out PermissionOverwrite overwrite) && overwrite.Type == 0) {
                allow |= overwrite.Allow;
                deny |= overwrite.Deny;
            }
        perms &= ~deny;
        perms |= allow;
        if(byId.TryGetValue(userId, out PermissionOverwrite member) && member.Type == 1) {
            perms &= ~member.Deny;
            perms |= member.Allow;
        }
        return perms;
    }
}
