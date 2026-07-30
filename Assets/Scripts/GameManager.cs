using System;
using System.Collections.Generic;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Tests.EditMode")]

namespace YiSunSin
{
    public enum GameStatus { Title, Playing, PausedManual, PausedLevelUp, GameOver, Win }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public PlayerController Player;
        public Weapon PlayerWeapon;
        public SpawnController Spawner;
        public GameObject XpGemPrefab;
        public GameObject BossSpotlight;

        public GameStatus Status { get; private set; } = GameStatus.Title;
        public float Elapsed { get; private set; }
        public List<UpgradeDefinition> UpgradeChoices { get; private set; } = new List<UpgradeDefinition>();
        public float BossBannerTimer { get; private set; }

        public event Action<GameStatus> OnStatusChanged;

        const float BossBannerDuration = 2f;

        int pendingLevelUps;
        readonly List<EnemyController> enemies = new List<EnemyController>();
        readonly List<GameObject> xpGems = new List<GameObject>();
        readonly List<Projectile> projectiles = new List<Projectile>();
        readonly List<(Projectile projectile, EnemyController enemy)> pendingHits = new List<(Projectile, EnemyController)>();
        Rect bounds;

        void Awake() => Awake_ForTests();

        // Exposed so EditMode tests can drive setup without a running Play Mode Awake pass.
        internal void Awake_ForTests()
        {
            Instance = this;
            bounds = new Rect(-GameConfig.ArenaWidth / 2f, -GameConfig.ArenaHeight / 2f, GameConfig.ArenaWidth, GameConfig.ArenaHeight);
        }

        void SetStatus(GameStatus status)
        {
            Status = status;
            OnStatusChanged?.Invoke(status);
        }

        // Unity's Destroy() is deferred and only valid in Play Mode; it throws
        // in EditMode (as EditMode tests run) where DestroyImmediate is
        // required instead. This routes through the correct call in both
        // contexts.
        static void SafeDestroy(UnityEngine.Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        public void StartGame()
        {
            Elapsed = 0f;
            pendingLevelUps = 0;
            BossBannerTimer = 0f;
            UpgradeChoices.Clear();
            foreach (var e in enemies) if (e != null) SafeDestroy(e.gameObject);
            enemies.Clear();
            foreach (var g in xpGems) if (g != null) SafeDestroy(g);
            xpGems.Clear();
            foreach (var p in projectiles) if (p != null) SafeDestroy(p.gameObject);
            projectiles.Clear();
            pendingHits.Clear();
            if (BossSpotlight != null) BossSpotlight.SetActive(false);

            Player.transform.position = Vector3.zero;
            Player.ResetState();
            Spawner.ResetState();
            PlayerWeapon.ResetState();
            // GameManager owns projectile spawning (rather than Weapon's own DefaultSpawn) so it
            // gets a direct reference to each spawned Projectile to Tick()/destroy in Update() -
            // see SpawnProjectileTracked below.
            PlayerWeapon.SpawnProjectile = SpawnProjectileTracked;

            SetStatus(GameStatus.Playing);
        }

        void SpawnProjectileTracked(Vector2 origin, Vector2 direction, float speed, float damage, float range)
        {
            var obj = Instantiate(PlayerWeapon.ProjectilePrefab, origin, Quaternion.identity);
            var projectile = obj.GetComponent<Projectile>();
            projectile.Launch(origin, direction, speed, damage, range);
            projectiles.Add(projectile);
        }

        // Exposed so EditMode tests can drive the projectile-tracking loop without a running
        // Play Mode Update() pass (same pattern as CheckGameOver_ForTests/CollectGems_ForTests).
        internal int ProjectileCount_ForTests => projectiles.Count;

        internal void TickProjectiles_ForTests(float dt)
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = projectiles[i];
                if (projectile == null) { projectiles.RemoveAt(i); continue; }
                if (projectile.Tick(dt))
                {
                    SafeDestroy(projectile.gameObject);
                    projectiles.RemoveAt(i);
                }
            }
        }

        public void Restart() => StartGame();

        public void TogglePause()
        {
            if (Status == GameStatus.Playing) SetStatus(GameStatus.PausedManual);
            else if (Status == GameStatus.PausedManual) SetStatus(GameStatus.Playing);
        }

        void Update()
        {
            if (Status != GameStatus.Playing) return;
            float dt = Time.deltaTime;

            Elapsed += dt;
            if (Elapsed >= GameConfig.WinTime)
            {
                SetStatus(GameStatus.Win);
                return;
            }

            Player.Tick(dt, bounds);

            foreach (var enemy in enemies)
                enemy.Tick(dt, Player.transform.position);

            var target = Targeting.FindNearest(Player.transform.position, enemies);
            PlayerWeapon.Damage = UpgradeEffects.EffectiveWeaponDamage(Player.UpgradeStacks);
            PlayerWeapon.FireInterval = UpgradeEffects.EffectiveFireInterval(Player.UpgradeStacks);
            PlayerWeapon.Tick(dt, Player.transform.position, target);

            TickProjectiles_ForTests(dt);

            ProcessPendingHits();

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                if (enemy == null) { enemies.RemoveAt(i); continue; }
                if (enemy.IsDead)
                {
                    SafeDestroy(enemy.gameObject);
                    enemies.RemoveAt(i);
                }
            }

            CheckGameOver_ForTests();
            if (Status != GameStatus.Playing) return;

            if (BossBannerTimer > 0f) BossBannerTimer = Mathf.Max(0f, BossBannerTimer - dt);

            CollectGems_ForTests();
            if (Status != GameStatus.Playing) return;

            var spawned = Spawner.Tick(dt, Elapsed, bounds, transform);
            foreach (var e in spawned)
            {
                enemies.Add(e);
                if (e.IsBoss)
                {
                    BossBannerTimer = BossBannerDuration;
                    if (ParticleEffects.Instance != null) ParticleEffects.Instance.PlayBossSpawn(e.transform.position);
                    if (BossSpotlight != null)
                    {
                        BossSpotlight.SetActive(true);
                        BossSpotlight.transform.position = e.transform.position;
                    }
                }
            }
        }

        // Called from Projectile.OnTriggerEnter2D (Play Mode) or directly by tests.
        public void OnProjectileHit(Projectile projectile, EnemyController enemy)
        {
            pendingHits.Add((projectile, enemy));
            // Projectile.OnTriggerEnter2D already destroys the projectile's GameObject on hit,
            // so untrack it here rather than letting the Update() Tick loop find it later (it
            // would find a destroyed/fake-null entry, which is handled defensively there too,
            // but removing eagerly avoids relying on that timing).
            projectiles.Remove(projectile);
            ProcessPendingHits();
        }

        void ProcessPendingHits()
        {
            foreach (var (projectile, enemy) in pendingHits)
            {
                if (enemy == null || enemy.IsDead) continue;
                enemy.TakeDamage(projectile.Damage);
                if (ParticleEffects.Instance != null) ParticleEffects.Instance.PlayHit(enemy.transform.position);
                if (enemy.IsDead)
                {
                    if (ParticleEffects.Instance != null) ParticleEffects.Instance.PlayDeath(enemy.transform.position);
                    float xpValue = enemy.IsBoss ? GameConfig.BossXpValue : GameConfig.XpGemValue;
                    var gem = Instantiate(XpGemPrefab, enemy.transform.position, Quaternion.identity, transform);
                    gem.SetActive(true);
                    gem.GetComponent<XpGem>().Value = xpValue;
                    xpGems.Add(gem);
                }
            }
            pendingHits.Clear();
        }

        public void OnEnemyTouchedPlayer(EnemyController enemy)
        {
            // Defense-in-depth against the same-frame window between an enemy dying and its
            // GameObject actually being destroyed (Destroy() is deferred in Play Mode): a dead
            // enemy's trigger collider can still fire OnTriggerStay2D for the rest of the frame.
            if (enemy == null || enemy.IsDead) return;
            if (Status == GameStatus.Playing)
                Player.TakeDamage(enemy.ContactDamage);
        }

        internal void CheckGameOver_ForTests()
        {
            if (Player.Hp <= 0f)
                SetStatus(GameStatus.GameOver);
        }

        internal void CollectGems_ForTests()
        {
            float pickupRadius = UpgradeEffects.EffectivePickupRadius(Player.UpgradeStacks);
            bool leveledUpThisFrame = false;
            var remaining = new List<GameObject>();

            foreach (var gemObj in xpGems)
            {
                if (gemObj == null) continue;
                if (leveledUpThisFrame) { remaining.Add(gemObj); continue; }

                float dist = Vector2.Distance(gemObj.transform.position, Player.transform.position);
                if (dist > pickupRadius) { remaining.Add(gemObj); continue; }

                var gem = gemObj.GetComponent<XpGem>();
                int levelsGained = Player.GainXp(gem.Value);
                SafeDestroy(gemObj);

                if (levelsGained > 0)
                {
                    pendingLevelUps += levelsGained;
                    leveledUpThisFrame = true;
                    UpgradeChoices = UpgradeSystem.PickChoices(Player.UpgradeStacks, () => UnityEngine.Random.value);
                    SetStatus(GameStatus.PausedLevelUp);
                    if (ParticleEffects.Instance != null) ParticleEffects.Instance.PlayLevelUp(Player.transform.position);
                }
            }

            xpGems.Clear();
            xpGems.AddRange(remaining);
        }

        public void ChooseUpgrade(string upgradeId)
        {
            Player.ApplyUpgrade(upgradeId);
            pendingLevelUps = Mathf.Max(0, pendingLevelUps - 1);

            if (pendingLevelUps > 0)
                UpgradeChoices = UpgradeSystem.PickChoices(Player.UpgradeStacks, () => UnityEngine.Random.value);

            // Guard against a theoretical deadlock: if levels are still owed but every upgrade is
            // already maxed out (PickChoices returns an empty list), there is nothing to render
            // and no way for the player to advance a PausedLevelUp screen with zero cards - so
            // only stay paused when there are actual choices to show.
            if (pendingLevelUps > 0 && UpgradeChoices.Count > 0)
            {
                // Status value is unchanged (still PausedLevelUp), but SetStatus must be called
                // anyway so OnStatusChanged fires again — UIController relies on this event to
                // know a fresh card set is ready; see Task 13's UpgradeChoices-identity check.
                SetStatus(GameStatus.PausedLevelUp);
            }
            else
            {
                pendingLevelUps = 0;
                UpgradeChoices.Clear();
                SetStatus(GameStatus.Playing);
            }
        }
    }
}
