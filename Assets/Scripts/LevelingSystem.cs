namespace YiSunSin
{
    public static class LevelingSystem
    {
        public static int XpForLevel(int level) => GameConfig.XpPerLevel * level;

        public struct XpResult
        {
            public int Level;
            public float Xp;
            public bool LeveledUp;
            public int LevelsGained;
        }

        public static XpResult ApplyXp(int level, float xp, float amount)
        {
            xp += amount;
            int levelsGained = 0;
            while (xp >= XpForLevel(level))
            {
                xp -= XpForLevel(level);
                level += 1;
                levelsGained += 1;
            }
            return new XpResult
            {
                Level = level,
                Xp = xp,
                LeveledUp = levelsGained > 0,
                LevelsGained = levelsGained
            };
        }
    }
}
