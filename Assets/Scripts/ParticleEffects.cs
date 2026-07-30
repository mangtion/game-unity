// game-unity/Assets/Scripts/ParticleEffects.cs
using UnityEngine;
using UnityEngine.Pool;

namespace YiSunSin
{
    public class ParticleEffects : MonoBehaviour
    {
        public static ParticleEffects Instance { get; private set; }

        public ParticleSystem HitPrefab;
        public ParticleSystem DeathPrefab;
        public ParticleSystem LevelUpPrefab;
        public ParticleSystem BossSpawnPrefab;

        ObjectPool<ParticleSystem> hitPool;
        ObjectPool<ParticleSystem> deathPool;
        ObjectPool<ParticleSystem> levelUpPool;
        ObjectPool<ParticleSystem> bossSpawnPool;

        void Awake()
        {
            Instance = this;
            hitPool = MakePool(HitPrefab);
            deathPool = MakePool(DeathPrefab);
            levelUpPool = MakePool(LevelUpPrefab);
            bossSpawnPool = MakePool(BossSpawnPrefab);
        }

        ObjectPool<ParticleSystem> MakePool(ParticleSystem prefab) => new ObjectPool<ParticleSystem>(
            createFunc: () =>
            {
                var instance = Instantiate(prefab, transform);
                var main = instance.main;
                main.stopAction = ParticleSystemStopAction.Callback;
                instance.gameObject.SetActive(false);
                return instance;
            },
            actionOnGet: ps => ps.gameObject.SetActive(true),
            actionOnRelease: ps => ps.gameObject.SetActive(false),
            actionOnDestroy: ps => Destroy(ps.gameObject),
            defaultCapacity: 20,
            maxSize: 100
        );

        public void PlayHit(Vector2 position) => Play(hitPool, position);
        public void PlayDeath(Vector2 position) => Play(deathPool, position);
        public void PlayLevelUp(Vector2 position) => Play(levelUpPool, position);
        public void PlayBossSpawn(Vector2 position) => Play(bossSpawnPool, position);

        void Play(ObjectPool<ParticleSystem> pool, Vector2 position)
        {
            var ps = pool.Get();
            ps.transform.position = position;
            var holder = ps.GetComponent<PooledParticleReturner>();
            if (holder == null) holder = ps.gameObject.AddComponent<PooledParticleReturner>();
            holder.Init(pool, ps);
            ps.Play();
        }
    }

    /// <summary>Returns a ParticleSystem to its pool when Unity signals it has stopped
    /// (requires main.stopAction = ParticleSystemStopAction.Callback, set in MakePool).</summary>
    public class PooledParticleReturner : MonoBehaviour
    {
        ObjectPool<ParticleSystem> pool;
        ParticleSystem ps;

        public void Init(ObjectPool<ParticleSystem> pool, ParticleSystem ps)
        {
            this.pool = pool;
            this.ps = ps;
        }

        void OnParticleSystemStopped() => pool.Release(ps);
    }
}
