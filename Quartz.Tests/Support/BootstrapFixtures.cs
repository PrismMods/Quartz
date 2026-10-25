// Stand-ins for the MSBuild-generated BootstrapInfo/EngineInfo consts (Full+ML values).
namespace Quartz.Bootstrap {
    internal static class BootstrapInfo {
        public const string ModName = "Quartz";
        public const string Version = "9.9.8";
        public const string Author = "test";
        public const string GithubLink = "https://github.com/PrismMods/Quartz";
        public const string PayloadFileName = "Quartz.dll";
        public const string EngineFileName = "Quartz.UpdateEngine.dll";
        public const string RepoOwner = "PrismMods";
        public const string RepoName = "Quartz";
        public const string AssetName = "Quartz.zip";
        public const string ZipRuntimeRel = "UserData/Quartz/Runtime";
    }
}
namespace Quartz.UpdateEngine {
    internal static class EngineInfo {
        public const string Version = "9.9.8";
        public const string Channel = "alpha";
        public const string RepoOwner = "PrismMods";
        public const string RepoName = "Quartz";
        public const string AssetName = "Quartz.zip";
        public const string PayloadFileName = "Quartz.dll";
        public const string EngineFileName = "Quartz.UpdateEngine.dll";
        public const string ZipDataRel = "UserData/Quartz";
        public const string ZipRuntimeRel = "UserData/Quartz/Runtime";
    }
}
