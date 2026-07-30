using UnityEngine;

namespace YiSunSin
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class Projectile : MonoBehaviour
    {
        public float Damage { get; private set; }

        Rigidbody2D rb;
        CircleCollider2D col;
        Vector2 velocity;
        float range;
        float traveled;
        bool initialized;

        void Awake()
        {
            EnsureInitialized();
        }

        // Unity does not reliably invoke Awake() synchronously for components
        // attached via `new GameObject(name, typeof(...))` in EditMode test
        // contexts (no active PlayMode loop driving the initialization
        // callback). All public entry points funnel through this idempotent
        // guard so behavior is correct whether or not Awake happened to run.
        void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = GameConfig.Weapon.ProjectileRadius;
        }

        public void Launch(Vector2 origin, Vector2 direction, float speed, float damage, float maxRange)
        {
            EnsureInitialized();
            transform.position = origin;
            velocity = direction.normalized * speed;
            Damage = damage;
            range = maxRange;
            traveled = 0f;
        }

        /// <summary>Advances the projectile; returns true once it has traveled past its range.</summary>
        public bool Tick(float dt)
        {
            EnsureInitialized();
            Vector2 step = velocity * dt;
            // NOTE: Rigidbody2D.MovePosition does not apply synchronously in
            // EditMode tests (no active physics step / Play Mode loop), so it
            // leaves transform.position unchanged until a physics step runs.
            // Setting transform.position directly keeps Tick() deterministic
            // and correct in both EditMode tests and Play Mode.
            transform.position = (Vector2)transform.position + step;
            traveled += step.magnitude;
            return traveled >= range;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var enemy = other.GetComponent<EnemyController>();
            if (enemy != null && !enemy.IsDead && GameManager.Instance != null)
            {
                GameManager.Instance.OnProjectileHit(this, enemy);
                Destroy(gameObject);
            }
        }
    }
}
