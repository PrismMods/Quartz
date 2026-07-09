namespace Quartz.Features.Status
{
    internal static class MistakesAccess
    {
        internal static scrMistakesManager Get()
        {
            scrController ctrl = scrController.instance;
            return ctrl != null ? ctrl.mistakesManager : null;
        }
        internal static float PercentAcc(scrMistakesManager m) => m != null ? m.percentAcc : 1f;
        internal static float PercentXAcc(scrMistakesManager m) => m != null ? m.percentXAcc : 1f;
        internal static int PlayerCount()
        {
            try { return scrPlayerManager.playerCount; }
            catch { return 1; }
        }
        internal static scrMarginTracker Tracker(int playerID)
        {
            try
            {
                scrPlayerManager pm = ADOBase.playerManager;
                if (pm == null) return null;
                scrPlayer[] players = pm.players;
                if (players == null || playerID < 0 || playerID >= players.Length) return null;
                scrPlayer p = players[playerID];
                return p != null ? p.marginTracker : null;
            }
            catch { return null; }
        }
    }
}
