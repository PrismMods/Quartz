namespace Quartz.Core;
public static class Info {
    public const string Name = "Quartz";
    public const string Author = "koren";
    public const string Version = "2.0.0";
    public const string Channel = "alpha";
    public static readonly int Build = BuildInfo.Number;
    public static ReleaseChannel ChannelKind => SemVer.ParseChannel(Channel);
    public static bool IsPrerelease => ChannelKind != ReleaseChannel.Stable;
    public static bool IsDev => ChannelKind == ReleaseChannel.Dev;
    public static string DisplayVersion => IsPrerelease
        ? $"{Version}-{SemVer.ChannelTag(ChannelKind)}-{Build}"
        : Version;
    public static SemVer Current => SemVer.TryParse(DisplayVersion, out SemVer v) ? v : default;
    public const string Description = ":thumbs_up:";
    public const string GithubLink = "https://github.com/PrismMods/Quartz";
    public const string RepoOwner = "PrismMods";
    public const string RepoName = "Quartz";
    // Community translations live in their own repo so translators never touch this
    // one; LangUpdateService pulls newer language files from it at runtime.
    public const string I18nRepoOwner = "PrismMods";
    public const string I18nRepoName = "Quartz-i18n";
    public const string I18nBranch = "main";
}
