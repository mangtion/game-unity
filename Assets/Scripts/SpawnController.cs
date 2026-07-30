using System;
using System.Collections.Generic;
using UnityEngine;

namespace YiSunSin
{
    public class SpawnController : MonoBehaviour
    {
        public GameObject EnemyPrefab;
        public Func<float> Rng = () => UnityEngine.Random.value;

        float timeSinceLastSpawn;
        bool bossSpawned;

        const float EdgeOffset = 20f;

        public List<EnemyController> Tick(float dt, float elapsedSeconds, Rect bounds, Transform parent)
        {
            var spawned = new List<EnemyController>();
            timeSinceLastSpawn += dt;
            float interval = SpawnCurve.IntervalAt(elapsedSeconds);

            while (timeSinceLastSpawn >= interval)
            {
                timeSinceLastSpawn -= interval;
                var type = Rng() < 0.5f ? GameConfig.EnemyBasic : GameConfig.EnemyFast;
                spawned.Add(SpawnEnemy(type, false, bounds, parent));
            }

            if (!bossSpawned && elapsedSeconds >= GameConfig.BossSpawnTime)
            {
                bossSpawned = true;
                spawned.Add(SpawnEnemy(GameConfig.Boss, true, bounds, parent));
            }

            return spawned;
        }

        public void ResetState()
        {
            timeSinceLastSpawn = 0f;
            bossSpawned = false;
        }

        EnemyController SpawnEnemy(GameConfig.EnemyTypeData type, bool isBoss, Rect bounds, Transform parent)
        {
            Vector2 pos = EdgePosition(bounds);
            var obj = Instantiate(EnemyPrefab, pos, Quaternion.identity, parent);
            obj.SetActive(true);
            var enemy = obj.GetComponent<EnemyController>();
            enemy.Initialize(type, isBoss);
            return enemy;
        }

        Vector2 EdgePosition(Rect bounds)
        {
            int edge = Mathf.FloorToInt(Rng() * 4f);
            float along = Rng();
            switch (edge)
            {
                case 0: return new Vector2(bounds.xMin + along * bounds.width, bounds.yMax + EdgeOffset);
                case 1: return new Vector2(bounds.xMax + EdgeOffset, bounds.yMin + along * bounds.height);
                case 2: return new Vector2(bounds.xMin + along * bounds.width, bounds.yMin - EdgeOffset);
                default: return new Vector2(bounds.xMin - EdgeOffset, bounds.yMin + along * bounds.height);
            }
        }
    }
}
