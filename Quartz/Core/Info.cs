namespace Quartz.Core;
public static class Info {
    // The KeyViewer flavour has its own Name, so the builds never share a data root, Harmony id or update asset.
#if QUARTZ_KEYVIEWER
    public const string Name = "QuartzKeyViewer";
#else
    public const string Name = "Quartz";
#endif
    // Not a const: addons would bake in the Full flavour's value.
    public static bool KeyViewerOnly =>
#if QUARTZ_KEYVIEWER
        true;
#else
        false;
#endif
    public static string DisplayName => AprilFools.Active ? AprilFools.BrandName : Name;
    public const string Author = "koren";
    public const string Version = "3.0.0";
    public const string Channel = "alpha";
    public static readonly int Build = BuildInfo.Number;
    public static ReleaseChannel ChannelKind => SemVer.ParseChannel(Channel);
    public static bool IsPrerelease => ChannelKind != ReleaseChannel.Stable;
    public static bool IsDev => ChannelKind == ReleaseChannel.Dev;
    public static string DisplayVersion => IsPrerelease
        ? $"{Version}-{SemVer.ChannelTag(ChannelKind)}-{Build}"
        : Version;
    public static SemVer Current => SemVer.TryParse(DisplayVersion, out SemVer v) ? v : default;
    public const int AddonAbi = 1;
    public const string Description = ":thumbs_up:";
    public const string GithubLink = "https://github.com/PrismMods/Quartz";
    public const string RepoOwner = "PrismMods";
    public const string RepoName = "Quartz";
    public const string I18nRepoOwner = "PrismMods";
    public const string I18nRepoName = "Quartz-i18n";
    public const string I18nBranch = "main";
}
