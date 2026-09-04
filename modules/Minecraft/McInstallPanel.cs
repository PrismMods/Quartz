#nullable enable
using Quartz.Core;
using TMPro;
using UnityEngine;
namespace Quartz.Features.Minecraft;
public sealed class McInstallPanel : MonoBehaviour {
    private McInstaller? installer;
    private volatile string status = string.Empty;
    private volatile bool finished;
    public TextMeshProUGUI? Label { get; set; }
    public Action? OnInstalled { get; set; }
    public bool Busy => installer?.Busy ?? false;
    public void Begin() {
        if(Busy) return;
        status = "starting";
        finished = false;
        installer ??= new McInstaller(MainCore.Paths.RootPath);
        _ = Task.Run(async () => {
            bool ok = await installer.InstallAsync(p => {
                status = p.Stage == "download"
                    ? $"downloading {p.Fraction * 100f:F0}% ({p.Bytes / 1048576} / {p.Total / 1048576} MB)"
                    : p.Stage;
            }, CancellationToken.None).ConfigureAwait(false);
            status = ok ? "installed" : "failed";
            finished = true;
        });
    }
    private void Update() {
        if(Label != null && Label.text != status) Label.text = status;
        if(!finished) return;
        finished = false;
        if(status == "installed") OnInstalled?.Invoke();
    }
    private void OnDestroy() {
        installer?.Dispose();
        installer = null;
    }
}
