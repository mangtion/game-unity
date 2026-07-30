using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using YiSunSin;

public class WeaponTests
{
    GameObject weaponGo;
    Weapon weapon;

    [SetUp]
    public void SetUp()
    {
        weaponGo = new GameObject("Weapon", typeof(Weapon));
        weapon = weaponGo.GetComponent<Weapon>();
        weapon.SpawnProjectile = (origin, direction, speed, damage, range) => { }; // no-op, no prefab needed
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(weaponGo);

    [Test]
    public void Projectile_MovesAlongNormalizedDirection_AtGivenSpeed()
    {
        var go = new GameObject("Projectile", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Projectile));
        var projectile = go.GetComponent<Projectile>();
        projectile.Launch(Vector2.zero, new Vector2(1f, 0f), 500f, 10f, 400f);
        projectile.Tick(1f);
        Assert.AreEqual(500f, go.transform.position.x, 1e-1f);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Projectile_ExpiresAtRange()
    {
        var go = new GameObject("Projectile", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Projectile));
        var projectile = go.GetComponent<Projectile>();
        projectile.Launch(Vector2.zero, new Vector2(1f, 0f), 500f, 10f, 400f);
        Assert.IsFalse(projectile.Tick(0f));
        Assert.IsTrue(projectile.Tick(1f)); // travels 500px in 1s, past the 400px range
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Weapon_DoesNotFire_WhenNoTarget()
    {
        weapon.Tick(1f, Vector2.zero, null);
        Assert.AreEqual(0, weapon.ShotsFired);
    }

    [Test]
    public void Weapon_Fires_AndResetsCooldown()
    {
        var enemyGo = new GameObject("Enemy", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController));
        enemyGo.transform.position = new Vector3(100f, 0f, 0f);
        var enemy = enemyGo.GetComponent<EnemyController>();
        enemy.Initialize(GameConfig.EnemyBasic, false);

        weapon.Tick(0f, Vector2.zero, enemy);
        Assert.AreEqual(1, weapon.ShotsFired);

        Object.DestroyImmediate(enemyGo);
    }

    [Test]
    public void Weapon_DoesNotFireAgain_BeforeCooldownElapses()
    {
        var enemyGo = new GameObject("Enemy", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController));
        enemyGo.transform.position = new Vector3(100f, 0f, 0f);
        var enemy = enemyGo.GetComponent<EnemyController>();
        enemy.Initialize(GameConfig.EnemyBasic, false);

        weapon.Tick(0f, Vector2.zero, enemy);   // fires, cooldown = 0.5
        weapon.Tick(0.1f, Vector2.zero, enemy); // cooldown 0.4, should not fire
        Assert.AreEqual(1, weapon.ShotsFired);

        Object.DestroyImmediate(enemyGo);
    }
}
