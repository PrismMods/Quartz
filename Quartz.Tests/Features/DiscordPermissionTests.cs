using Quartz.Features.Discord;
using static Asserts;
static class DiscordPermissionTests {
    const string Guild = "1";
    const string Everyone = "1";
    const string RoleA = "10";
    const string Member = "99";
    public static void TestOwnerSeesEverything() {
        List<DiscordRole> roles = [new DiscordRole(Everyone, "@everyone", 0UL)];
        List<PermissionOverwrite> deniedToAll = [new PermissionOverwrite(Everyone, 0, 0UL, DiscordPermissions.ViewChannel)];
        Assert(
            DiscordPermissions.CanView(Guild, Member, roles, Member, [], deniedToAll),
            "the guild owner must see a channel even when @everyone is denied"
        );
    }
    public static void TestEveryoneDenyHidesTheChannel() {
        List<DiscordRole> roles = [new DiscordRole(Everyone, "@everyone", DiscordPermissions.ViewChannel)];
        List<PermissionOverwrite> overwrites = [new PermissionOverwrite(Everyone, 0, 0UL, DiscordPermissions.ViewChannel)];
        Assert(
            !DiscordPermissions.CanView(Guild, "owner", roles, Member, [], overwrites),
            "an @everyone deny must hide the channel"
        );
    }
    public static void TestRoleAllowBeatsEveryoneDeny() {
        List<DiscordRole> roles = [
            new DiscordRole(Everyone, "@everyone", DiscordPermissions.ViewChannel),
            new DiscordRole(RoleA, "mods", 0UL),
        ];
        List<PermissionOverwrite> overwrites = [
            new PermissionOverwrite(Everyone, 0, 0UL, DiscordPermissions.ViewChannel),
            new PermissionOverwrite(RoleA, 0, DiscordPermissions.ViewChannel, 0UL),
        ];
        Assert(
            DiscordPermissions.CanView(Guild, "owner", roles, Member, [RoleA], overwrites),
            "a role allow must win over the @everyone deny"
        );
    }
    public static void TestMemberDenyBeatsRoleAllow() {
        List<DiscordRole> roles = [
            new DiscordRole(Everyone, "@everyone", DiscordPermissions.ViewChannel),
            new DiscordRole(RoleA, "mods", 0UL),
        ];
        List<PermissionOverwrite> overwrites = [
            new PermissionOverwrite(RoleA, 0, DiscordPermissions.ViewChannel, 0UL),
            new PermissionOverwrite(Member, 1, 0UL, DiscordPermissions.ViewChannel),
        ];
        Assert(
            !DiscordPermissions.CanView(Guild, "owner", roles, Member, [RoleA], overwrites),
            "the member-specific deny must be applied last and win"
        );
    }
    public static void TestAdministratorIgnoresOverwrites() {
        List<DiscordRole> roles = [
            new DiscordRole(Everyone, "@everyone", 0UL),
            new DiscordRole(RoleA, "admin", DiscordPermissions.Administrator),
        ];
        List<PermissionOverwrite> overwrites = [new PermissionOverwrite(Everyone, 0, 0UL, DiscordPermissions.ViewChannel)];
        Assert(
            DiscordPermissions.CanView(Guild, "owner", roles, Member, [RoleA], overwrites),
            "Administrator must short-circuit to every permission"
        );
    }
}
