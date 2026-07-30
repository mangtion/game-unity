using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using YiSunSin;

public class SpawnControllerTests
{
    GameObject controllerGo;
    SpawnController spawner;
    GameObject prefab;
    List<GameObject> spawnedRoots = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        prefab = new GameObject("EnemyPrefab", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController));
        prefab.SetActive(false); // keep it out of the "live" scene, used purely as an Instantiate source

        controllerGo = new GameObject("Spawner", typeof(SpawnController));
        spawner = controllerGo.GetComponent<SpawnController>();
        spawner.EnemyPrefab = prefab;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in spawnedRoots) if (go != null) Object.DestroyImmediate(go);
        spawnedRoots.Clear();
        Object.DestroyImmediate(controllerGo);
        Object.DestroyImmediate(prefab);
    }

    static readonly Rect Bounds = new Rect(-800f, -450f, 1600f, 900f);

    [Test]
    public void DoesNotSpawnBoss_BeforeSpawnTime()
    {
        spawner.Rng = () => 0.99f;
        var spawned = spawner.Tick(1f, 100f, Bounds, null);
        spawnedRoots.AddRange(EnemyGameObjects(spawned));
        foreach (var e in spawned) Assert.IsFalse(e.IsBoss);
    }

    [Test]
    public void SpawnsBoss_ExactlyOnce_AtOrAfter4Minutes()
    {
        spawner.Rng = () => 0.99f;
        var first = spawner.Tick(0.01f, 240f, Bounds, null);
        spawnedRoots.AddRange(EnemyGameObjects(first));
        Assert.AreEqual(1, first.FindAll(e => e.IsBoss).Count);

        var second = spawner.Tick(0.01f, 241f, Bounds, null);
        spawnedRoots.AddRange(EnemyGameObjects(second));
        Assert.AreEqual(0, second.FindAll(e => e.IsBoss).Count);
    }

    [Test]
    public void EmitsRegularSpawn_OnceIntervalElapses()
    {
        spawner.Rng = () => 0f; // deterministic: always "basic" type, edge 0, along 0
        var spawned = spawner.Tick(2f, 0f, Bounds, null); // base interval is 2s at t=0
        spawnedRoots.AddRange(EnemyGameObjects(spawned));
        Assert.AreEqual(1, spawned.Count);
        Assert.AreEqual("basic", spawned[0].TypeId);
        Assert.IsFalse(spawned[0].IsBoss);
    }

    static List<GameObject> EnemyGameObjects(List<EnemyController> enemies)
    {
        var result = new List<GameObject>();
        foreach (var e in enemies) result.Add(e.gameObject);
        return result;
    }
}
