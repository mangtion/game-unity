using NUnit.Framework;
using UnityEngine;
using YiSunSin;

public class PlayerControllerTests
{
    GameObject go;
    PlayerController player;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("Player", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(PlayerController));
        player = go.GetComponent<PlayerController>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(go);
    }

    [Test]
    public void StartsAtFullHp_WithConfiguredDefaults()
    {
        Assert.AreEqual(100f, player.Hp);
        Assert.AreEqual(100f, player.MaxHp);
        Assert.AreEqual(1, player.Level);
        Assert.AreEqual(0f, player.Xp);
    }

    [Test]
    public void Tick_MovesAtMoveSpeed_AlongSingleAxis()
    {
        go.transform.position = new Vector3(100f, 100f, 0f);
        player.SetInput(Vector2.right);
        var bounds = new Rect(-800f, -450f, 1600f, 900f);
        player.Tick(1f, bounds);
        Assert.AreEqual(300f, go.transform.position.x, 1e-3f); // 100 + 200*1
        Assert.AreEqual(100f, go.transform.position.y, 1e-3f);
    }

    [Test]
    public void Tick_NormalizesDiagonalMovement()
    {
        go.transform.position = new Vector3(0f, 0f, 0f);
        player.SetInput(new Vector2(1f, 1f));
        var bounds = new Rect(-800f, -450f, 1600f, 900f);
        player.Tick(1f, bounds);
        float distance = ((Vector2)go.transform.position).magnitude;
        Assert.AreEqual(200f, distance, 1e-2f);
    }

    [Test]
    public void Tick_ClampsToBounds()
    {
        go.transform.position = new Vector3(790f, 0f, 0f);
        player.SetInput(Vector2.right);
        var bounds = new Rect(-800f, -450f, 1600f, 900f);
        player.Tick(1f, bounds);
        Assert.AreEqual(800f, go.transform.position.x, 1e-3f);
    }

    [Test]
    public void TakeDamage_ReducesHp_AndGrantsIFrames()
    {
        Assert.IsTrue(player.TakeDamage(30f));
        Assert.AreEqual(70f, player.Hp);
        Assert.IsFalse(player.TakeDamage(30f));
        Assert.AreEqual(70f, player.Hp);
    }

    [Test]
    public void IFrames_ExpireAfterTick()
    {
        player.TakeDamage(10f);
        var bounds = new Rect(-800f, -450f, 1600f, 900f);
        player.Tick(0.5f, bounds); // exactly InvincibilityDuration
        Assert.IsTrue(player.TakeDamage(10f));
        Assert.AreEqual(80f, player.Hp);
    }

    [Test]
    public void GainXp_ReturnsLevelsGained()
    {
        Assert.AreEqual(0, player.GainXp(5f));
        Assert.AreEqual(1, player.GainXp(10f)); // 5+10=15 >= 10
        Assert.AreEqual(2, player.Level);
    }

    [Test]
    public void GainXp_ReturnsMultipleLevelsGained_InOneCall()
    {
        Assert.AreEqual(2, player.GainXp(35f)); // matches LevelingSystem cascade test
        Assert.AreEqual(3, player.Level);
    }

    [Test]
    public void ApplyUpgrade_IncrementsStack_AndAppliesEffectImmediately()
    {
        player.ApplyUpgrade("moveSpeed");
        Assert.AreEqual(1, player.UpgradeStacks["moveSpeed"]);
        Assert.AreEqual(220f, player.MoveSpeed, 1e-3f);
    }

    [Test]
    public void ApplyUpgrade_MaxHp_HealsByTheIncrease()
    {
        player.TakeDamage(50f); // Hp = 50
        player.ApplyUpgrade("maxHp"); // MaxHp 100 -> 120, heal by +20
        Assert.AreEqual(120f, player.MaxHp);
        Assert.AreEqual(70f, player.Hp);
    }

    [Test]
    public void ResetState_RestoresFreshPlayer()
    {
        player.TakeDamage(50f);
        player.GainXp(15f);
        player.ApplyUpgrade("weaponDamage");
        player.ResetState();
        Assert.AreEqual(100f, player.Hp);
        Assert.AreEqual(100f, player.MaxHp);
        Assert.AreEqual(1, player.Level);
        Assert.AreEqual(0f, player.Xp);
        Assert.AreEqual(0, player.UpgradeStacks.Count);
    }
}
