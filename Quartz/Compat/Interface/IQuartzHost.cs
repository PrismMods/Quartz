namespace Quartz.Compat.Interface;
public interface IQuartzHost {
    IQuartzLogger QuartzLogger { get; }
    string QuartzFilePath { get; }
    string ModsPath { get; }
    string UserLibsPath { get; }
    bool SupportsSelfUpdate { get; }
    string UpdateAssetName { get; }
    string UpdateExtractRoot { get; }
}
