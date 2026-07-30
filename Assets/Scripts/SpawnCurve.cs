using UnityEngine;

namespace YiSunSin
{
    public static class SpawnCurve
    {
        public static float IntervalAt(float elapsedSeconds)
        {
            int decaySteps = Mathf.FloorToInt(elapsedSeconds / GameConfig.Spawner.DecayPeriod);
            float interval = GameConfig.Spawner.BaseInterval *
                Mathf.Pow(1f - GameConfig.Spawner.DecayRate, decaySteps);
            return Mathf.Max(interval, GameConfig.Spawner.MinInterval);
        }
    }
}
