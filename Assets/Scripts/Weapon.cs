using System;
using UnityEngine;

namespace YiSunSin
{
    public class Weapon : MonoBehaviour
    {
        public GameObject ProjectilePrefab;
        public Action<Vector2, Vector2, float, float, float> SpawnProjectile;
        public float Damage = GameConfig.Weapon.Damage;
        public float FireInterval = GameConfig.Weapon.FireInterval;
        public int ShotsFired { get; private set; }

        float cooldown;
        bool initialized;

        void Awake()
        {
            EnsureInitialized();
        }

        // Unity does not reliably invoke Awake() synchronously for components
        // attached via `new GameObject(name, typeof(...))` in EditMode test
        // contexts (no active PlayMode loop driving the initialization
        // callback). All public entry points funnel through this idempotent
        // guard so the default SpawnProjectile assignment happens whether or
        // not Awake happened to run. (The EditMode tests always override
        // SpawnProjectile explicitly, so they wouldn't catch a missing guard
        // here, but real Play Mode gameplay relies on the DefaultSpawn path
        // when no override is provided.)
        void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            if (SpawnProjectile == null)
                SpawnProjectile = DefaultSpawn;
        }

        void DefaultSpawn(Vector2 origin, Vector2 direction, float speed, float damage, float range)
        {
            var obj = Instantiate(ProjectilePrefab, origin, Quaternion.identity);
            obj.GetComponent<Projectile>().Launch(origin, direction, speed, damage, range);
        }

        public void Tick(float dt, Vector2 origin, EnemyController target)
        {
            EnsureInitialized();
            cooldown = Mathf.Max(0f, cooldown - dt);
            if (cooldown > 0f || target == null) return;

            Vector2 direction = (Vector2)target.transform.position - origin;
            SpawnProjectile(origin, direction, GameConfig.Weapon.ProjectileSpeed, Damage, GameConfig.Weapon.ProjectileRange);
            ShotsFired++;
            cooldown = FireInterval;
        }

        // Clears the fire-cooldown timer. Called by GameManager.StartGame() so a
        // fresh run doesn't inherit leftover cooldown state from a prior run
        // (deferred from Task 10's review, folded in during the final review pass).
        public void ResetState()
        {
            cooldown = 0f;
        }
    }
}
