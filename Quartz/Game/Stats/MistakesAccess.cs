using Quartz.Compat.Game;
namespace Quartz.Game.Stats
{
    public static class MistakesAccess
    {
        public static scrMistakesManager Get() => GameApi.MistakesManager;
        public static float PercentAcc(scrMistakesManager m) => GameApi.PercentAcc(m);
        public static float PercentXAcc(scrMistakesManager m) => GameApi.PercentXAcc(m);
        public static int PlayerCount() => GameApi.PlayerCount();
        public static object Tracker(int playerID) => GameApi.Tracker(playerID);
    }
}
