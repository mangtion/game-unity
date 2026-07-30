namespace YiSunSin
{
    public static class GameConfig
    {
        public static class Player
        {
            public const float MaxHp = 100f;
            public const float MoveSpeed = 200f;
            public const float InvincibilityDuration = 0.5f;
            public const float Radius = 16f;
        }

        public static class Weapon
        {
            public const float Damage = 10f;
            public const float FireInterval = 0.5f;
            public const float ProjectileSpeed = 500f;
            public const float ProjectileRange = 400f;
            public const float ProjectileRadius = 4f;
        }

        public struct EnemyTypeData
        {
            public string Id;
            public float Hp;
            public float Speed;
            public float ContactDamage;
            public float Radius;
        }

        public static readonly EnemyTypeData EnemyBasic = new EnemyTypeData
        {
            Id = "basic", Hp = 20f, Speed = 80f, ContactDamage = 10f, Radius = 16f
        };

        public static readonly EnemyTypeData EnemyFast = new EnemyTypeData
        {
            Id = "fast", Hp = 10f, Speed = 150f, ContactDamage = 5f, Radius = 14f
        };

        public static readonly EnemyTypeData Boss = new EnemyTypeData
        {
            Id = "boss", Hp = 500f, Speed = 90f, ContactDamage = 20f, Radius = 24f
        };

        public const float BossSpawnTime = 240f;
        public const float BossXpValue = 50f;

        public const float XpGemValue = 1f;
        public const float XpGemPickupRadius = 40f;

        public const int XpPerLevel = 10;

        public const float WinTime = 300f;

        public static class Spawner
        {
            public const float BaseInterval = 2f;
            public const float DecayRate = 0.05f;
            public const float DecayPeriod = 15f;
            public const float MinInterval = 0.3f;
        }

        public const float ArenaWidth = 1600f;
        public const float ArenaHeight = 900f;
    }
}
