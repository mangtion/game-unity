using NUnit.Framework;
using UnityEngine;
using YiSunSin;

public class EnemyControllerTests
{
    GameObject go;
    EnemyController enemy;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("Enemy", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController));
        enemy = go.GetComponent<EnemyController>();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(go);

    [Test]
    public void Initialize_CopiesStatsFromType()
    {
        enemy.Initialize(GameConfig.EnemyBasic, false);
        Assert.AreEqual(20f, enemy.Hp);
        Assert.AreEqual(80f, enemy.Speed);
        Assert.AreEqual(10f, enemy.ContactDamage);
        Assert.AreEqual(16f, enemy.Radius);
        Assert.AreEqual("basic", enemy.TypeId);
        Assert.IsFalse(enemy.IsBoss);
    }

    [Test]
    public void Tick_MovesTowardTarget_AtItsSpeed()
    {
        enemy.Initialize(GameConfig.EnemyBasic, false);
        go.transform.position = Vector3.zero;
        enemy.Tick(1f, new Vector2(80f, 0f)); // 1s at 80px/s should reach (80,0)
        Assert.AreEqual(80f, go.transform.position.x, 1e-2f);
        Assert.AreEqual(0f, go.transform.position.y, 1e-3f);
    }

    [Test]
    public void TakeDamage_ReducesHp_IsDeadReflectsIt()
    {
        enemy.Initialize(GameConfig.EnemyBasic, false);
        enemy.TakeDamage(15f);
        Assert.AreEqual(5f, enemy.Hp);
        Assert.IsFalse(enemy.IsDead);
        enemy.TakeDamage(15f);
        Assert.AreEqual(0f, enemy.Hp);
        Assert.IsTrue(enemy.IsDead);
    }

    [Test]
    public void Initialize_Boss_SetsIsBossTrue()
    {
        enemy.Initialize(GameConfig.Boss, true);
        Assert.IsTrue(enemy.IsBoss);
        Assert.AreEqual("boss", enemy.TypeId);
        Assert.AreEqual(500f, enemy.Hp);
    }

    [Test]
    public void Initialize_SelectsSpriteArray_MatchingType_AndAssignsToFlipbook()
    {
        // Regression test for the "invisible enemy" bug: Initialize() must pick the
        // sprite array matching type.Id and push it into SpriteFlipbook.Sprites (and
        // set SpriteRenderer.sprite to frame 0) rather than leaving the flipbook empty.
        var flipbook = go.AddComponent<SpriteFlipbook>();
        var basicSprites = new[] { Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero) };
        var fastSprites = new[] { Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero) };
        var bossSprites = new[] { Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero) };
        enemy.BasicSprites = basicSprites;
        enemy.FastSprites = fastSprites;
        enemy.BossSprites = bossSprites;

        enemy.Initialize(GameConfig.EnemyFast, false);

        Assert.AreSame(fastSprites, flipbook.Sprites);
        Assert.AreSame(fastSprites[0], go.GetComponent<SpriteRenderer>().sprite);
    }
}
