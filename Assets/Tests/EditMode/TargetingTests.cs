using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using YiSunSin;

public class TargetingTests
{
    List<GameObject> spawned = new List<GameObject>();

    EnemyController MakeEnemy(Vector2 pos, float hp)
    {
        var go = new GameObject("Enemy", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController));
        go.transform.position = pos;
        var enemy = go.GetComponent<EnemyController>();
        var type = GameConfig.EnemyBasic;
        type.Hp = hp;
        enemy.Initialize(type, false);
        spawned.Add(go);
        return enemy;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in spawned) Object.DestroyImmediate(go);
        spawned.Clear();
    }

    [Test]
    public void ReturnsNull_WhenNoEnemies()
    {
        Assert.IsNull(Targeting.FindNearest(Vector2.zero, new List<EnemyController>()));
    }

    [Test]
    public void ReturnsClosestLiveEnemy()
    {
        var near = MakeEnemy(new Vector2(10f, 0f), 5f);
        var far = MakeEnemy(new Vector2(100f, 0f), 5f);
        var result = Targeting.FindNearest(Vector2.zero, new List<EnemyController> { far, near });
        Assert.AreEqual(near, result);
    }

    [Test]
    public void IgnoresDeadEnemies()
    {
        var dead = MakeEnemy(new Vector2(1f, 0f), 5f);
        dead.TakeDamage(999f);
        var alive = MakeEnemy(new Vector2(50f, 0f), 5f);
        var result = Targeting.FindNearest(Vector2.zero, new List<EnemyController> { dead, alive });
        Assert.AreEqual(alive, result);
    }
}
