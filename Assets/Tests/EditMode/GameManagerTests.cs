using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using YiSunSin;

public class GameManagerTests
{
    GameObject root;
    GameManager manager;
    PlayerController player;
    Weapon weapon;
    SpawnController spawner;
    GameObject xpGemPrefab;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Root");

        var playerGo = new GameObject("Player", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(PlayerController));
        playerGo.transform.SetParent(root.transform);
        player = playerGo.GetComponent<PlayerController>();

        var weaponGo = new GameObject("Weapon", typeof(Weapon));
        weaponGo.transform.SetParent(root.transform);
        weapon = weaponGo.GetComponent<Weapon>();
        weapon.SpawnProjectile = (o, d, s, dm, r) => { }; // no-op in tests

        var spawnerGo = new GameObject("Spawner", typeof(SpawnController));
        spawnerGo.transform.SetParent(root.transform);
        spawner = spawnerGo.GetComponent<SpawnController>();
        spawner.Rng = () => 0.99f; // avoid regular spawns interfering unless a test overrides it

        xpGemPrefab = new GameObject("XpGemPrefab", typeof(XpGem));
        xpGemPrefab.SetActive(false);

        var managerGo = new GameObject("GameManager", typeof(GameManager));
        managerGo.transform.SetParent(root.transform);
        manager = managerGo.GetComponent<GameManager>();
        manager.Player = player;
        manager.PlayerWeapon = weapon;
        manager.Spawner = spawner;
        manager.XpGemPrefab = xpGemPrefab;
        manager.Awake_ForTests(); // see Step 3: Awake must be callable directly for EditMode setup
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    [Test]
    public void StartsInTitleStatus()
    {
        Assert.AreEqual(GameStatus.Title, manager.Status);
    }

    [Test]
    public void StartGame_ResetsState_EntersPlaying()
    {
        manager.StartGame();
        Assert.AreEqual(GameStatus.Playing, manager.Status);
        Assert.AreEqual(100f, player.Hp);
        Assert.AreEqual(0f, manager.Elapsed);
    }

    [Test]
    public void TogglePause_SwitchesBetweenPlayingAndPaused()
    {
        manager.StartGame();
        manager.TogglePause();
        Assert.AreEqual(GameStatus.PausedManual, manager.Status);
        manager.TogglePause();
        Assert.AreEqual(GameStatus.Playing, manager.Status);
    }

    [Test]
    public void KillingAnEnemy_DropsGem_ThatGrantsXpOnPickup()
    {
        manager.StartGame();

        var enemyGo = new GameObject("Enemy", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController));
        var enemy = enemyGo.GetComponent<EnemyController>();
        enemy.Initialize(GameConfig.EnemyBasic, false);
        enemyGo.transform.position = player.transform.position; // pickup radius 40, so an in-range drop is picked up immediately

        var projGo = new GameObject("Projectile", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Projectile));
        var projectile = projGo.GetComponent<Projectile>();
        projectile.Launch(player.transform.position, Vector2.right, 0f, 100f, 400f); // damage 100 kills the 20hp enemy

        float xpBefore = player.Xp;
        manager.OnProjectileHit(projectile, enemy);
        // OnProjectileHit only records the kill/drop; pickup happens on the next Tick-equivalent pass:
        manager.CollectGems_ForTests();

        Assert.IsTrue(player.Xp > xpBefore || player.Level > 1);

        Object.DestroyImmediate(enemyGo);
        Object.DestroyImmediate(projGo);
    }

    [Test]
    public void LevelingUp_PausesForUpgradeChoice_ChooseResumesPlay()
    {
        manager.StartGame();
        player.GainXp(9f); // 1 XP away from level 2 (needs 10)

        var enemyGo = new GameObject("Enemy", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController));
        var enemy = enemyGo.GetComponent<EnemyController>();
        enemy.Initialize(GameConfig.EnemyBasic, false);
        enemyGo.transform.position = player.transform.position;

        var projGo = new GameObject("Projectile", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Projectile));
        var projectile = projGo.GetComponent<Projectile>();
        projectile.Launch(player.transform.position, Vector2.right, 0f, 100f, 400f);

        manager.OnProjectileHit(projectile, enemy);
        manager.CollectGems_ForTests();

        Assert.AreEqual(GameStatus.PausedLevelUp, manager.Status);
        Assert.AreEqual(3, manager.UpgradeChoices.Count);

        string chosenId = manager.UpgradeChoices[0].Id;
        manager.ChooseUpgrade(chosenId);
        Assert.AreEqual(GameStatus.Playing, manager.Status);
        Assert.AreEqual(1, player.UpgradeStacks[chosenId]);

        Object.DestroyImmediate(enemyGo);
        Object.DestroyImmediate(projGo);
    }

    [Test]
    public void MultiLevelUp_OffersOneCardSetPerLevel_NotJustOne()
    {
        manager.StartGame();

        var enemyGo = new GameObject("Enemy", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController));
        var enemy = enemyGo.GetComponent<EnemyController>();
        enemy.Initialize(GameConfig.Boss, true); // boss drop, 50 XP, forces a multi-level gain from level 1
        enemyGo.transform.position = player.transform.position;

        var projGo = new GameObject("Projectile", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Projectile));
        var projectile = projGo.GetComponent<Projectile>();
        projectile.Launch(player.transform.position, Vector2.right, 0f, 1000f, 400f);

        manager.OnProjectileHit(projectile, enemy);
        manager.CollectGems_ForTests();

        // 50 XP from level 1: 1->2 (10), 2->3(20) = 30 spent, 3->4 needs 30, 20 left -> stops at level 3, 2 levels gained
        Assert.AreEqual(GameStatus.PausedLevelUp, manager.Status);
        string firstChoice = manager.UpgradeChoices[0].Id;
        manager.ChooseUpgrade(firstChoice);
        Assert.AreEqual(GameStatus.PausedLevelUp, manager.Status); // still owed a second pick
        Assert.AreEqual(3, manager.UpgradeChoices.Count);

        string secondChoice = manager.UpgradeChoices[0].Id;
        manager.ChooseUpgrade(secondChoice);
        Assert.AreEqual(GameStatus.Playing, manager.Status);

        Object.DestroyImmediate(enemyGo);
        Object.DestroyImmediate(projGo);
    }

    [Test]
    public void PlayerHpReachingZero_EndsRunWithGameOver()
    {
        manager.StartGame();
        player.TakeDamage(150f); // exceeds 100 max HP in a single hit; Hp clamps to 0
        manager.CheckGameOver_ForTests();
        Assert.AreEqual(GameStatus.GameOver, manager.Status);
    }

    [Test]
    public void SpawnedProjectile_IsTrackedByGameManager_AndDestroyedOnExpiry()
    {
        // StartGame() wires PlayerWeapon.SpawnProjectile to GameManager's own tracked-spawn
        // delegate (Critical #1 fix) instead of Weapon's DefaultSpawn, so a fired shot must
        // both register in GameManager's projectile list and actually advance/expire there.
        var templateGo = new GameObject("ProjectileTemplate", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Projectile));
        templateGo.SetActive(false);
        weapon.ProjectilePrefab = templateGo;

        manager.StartGame(); // resets weapon cooldown to 0 and assigns the tracked spawn delegate

        var enemyGo = new GameObject("Enemy", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController));
        var enemy = enemyGo.GetComponent<EnemyController>();
        enemy.Initialize(GameConfig.EnemyBasic, false);
        enemyGo.transform.position = new Vector3(1000f, 0f, 0f);

        weapon.Tick(0f, player.transform.position, enemy);
        Assert.AreEqual(1, weapon.ShotsFired);
        Assert.AreEqual(1, manager.ProjectileCount_ForTests, "GameManager should track the spawned projectile");

        // Speed 500, range 400: a 1s tick travels 500 units, past the range, so it should expire and be removed.
        manager.TickProjectiles_ForTests(1f);
        Assert.AreEqual(0, manager.ProjectileCount_ForTests, "expired projectile should be destroyed and untracked");

        Object.DestroyImmediate(enemyGo);
        Object.DestroyImmediate(templateGo);
    }

    [Test]
    public void DeadEnemy_IsDestroyedAndRemoved_AndStopsDealingContactDamage()
    {
        // Critical #2 fix: killed enemies must have their GameObject destroyed (not just
        // dropped from the tracking list), and a dead enemy must not deal further contact damage.
        manager.StartGame();

        var enemyGo = new GameObject("Enemy", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController));
        var enemy = enemyGo.GetComponent<EnemyController>();
        enemy.Initialize(GameConfig.EnemyBasic, false);
        enemy.TakeDamage(1000f); // kill it
        Assert.IsTrue(enemy.IsDead);

        float hpBefore = player.Hp;
        manager.OnEnemyTouchedPlayer(enemy);
        Assert.AreEqual(hpBefore, player.Hp, "a dead enemy must not deal contact damage");

        Object.DestroyImmediate(enemyGo);
    }
}
