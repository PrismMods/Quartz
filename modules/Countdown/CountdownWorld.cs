using System;
using Quartz.Core;
namespace Quartz.Features.Countdown;
internal static class CountdownWorld {
    internal static void Log(string message) => MainCore.Log.Msg("[Countdown] " + message);
    internal static void Warn(Exception e, string context) => Diag.Warn(e, "Countdown/" + context);
}
