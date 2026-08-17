namespace Quartz.Core;
public static class Info {
    // The KeyViewer flavour (-p:Flavor=KeyViewer) is the same core shipping only the
    // key-viewer module. Name is the mod's identity everywhere it matters — the data
    // root under UserData/, the Harmony id, the MelonInfo name, the update asset —
    // so a single const keeps the two builds from ever sharing a file or a patch id.
#if QUARTZ_KEYVIEWER
    public const string Name = "QuartzKeyViewer";
#else
    public const string Name = "Quartz";
#endif
    // A property, deliberately NOT a const: modules and addons compile against
    // sdk/QuartzAddon.dll, which is built from the Full flavour, and the compiler
    // bakes a const's value into every call site. As a const this would read false
    // inside every .qmod no matter which core was actually running.
    public static bool KeyViewerOnly =>
#if QUARTZ_KEYVIEWER
        true;
#else
        false;
#endif
    public static string DisplayName => AprilFools.Active ? AprilFools.BrandName : Name;
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
    public const int ModuleAbi = 1;
    public const int AddonAbi = 1;
    public const string Description = ":thumbs_up:";
    public const string GithubLink = "https://github.com/PrismMods/Quartz";
    public const string RepoOwner = "PrismMods";
    public const string RepoName = "Quartz";
    public const string I18nRepoOwner = "PrismMods";
    public const string I18nRepoName = "Quartz-i18n";
    public const string I18nBranch = "main";
}
