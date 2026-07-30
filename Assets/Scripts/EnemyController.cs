using UnityEngine;

namespace YiSunSin
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class EnemyController : MonoBehaviour
    {
        // Per-type sprite frame arrays, assigned on the shared Enemy prefab by
        // ProjectBootstrap.BuildEnemyPrefab(). Initialize() picks the right
        // one based on type.Id and wires it into the SpriteFlipbook (and sets
        // the initial SpriteRenderer frame) since a single shared prefab has
        // no other way to know which enemy variant it represents until spawn
        // time.
        public Sprite[] BasicSprites;
        public Sprite[] FastSprites;
        public Sprite[] BossSprites;

        float hp;
        float speed;
        float contactDamage;
        float radius;
        bool isBoss;
        string typeId;

        public float Hp { get { EnsureInitialized(); return hp; } }
        public float Speed { get { EnsureInitialized(); return speed; } }
        public float ContactDamage { get { EnsureInitialized(); return contactDamage; } }
        public float Radius { get { EnsureInitialized(); return radius; } }
        public bool IsBoss { get { EnsureInitialized(); return isBoss; } }
        public string TypeId { get { EnsureInitialized(); return typeId; } }
        public bool IsDead { get { EnsureInitialized(); return hp <= 0f; } }

        Rigidbody2D rb;
        CircleCollider2D col;
        SpriteRenderer spriteRenderer;
        SpriteFlipbook flipbook;
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
            spriteRenderer = GetComponent<SpriteRenderer>();
            // SpriteFlipbook isn't required by this component (some EditMode
            // test fixtures build the GameObject without it), so it's looked
            // up but not assumed present.
            flipbook = GetComponent<SpriteFlipbook>();
        }

        public void Initialize(GameConfig.EnemyTypeData type, bool isBoss)
        {
            EnsureInitialized();
            hp = type.Hp;
            speed = type.Speed;
            contactDamage = type.ContactDamage;
            radius = type.Radius;
            typeId = type.Id;
            this.isBoss = isBoss;
            col.radius = radius;

            var sprites = SpritesForType(typeId);
            if (sprites != null && sprites.Length > 0)
            {
                if (flipbook != null) flipbook.Sprites = sprites;
                if (spriteRenderer != null) spriteRenderer.sprite = sprites[0];
            }
        }

        Sprite[] SpritesForType(string id)
        {
            switch (id)
            {
                case "basic": return BasicSprites;
                case "fast": return FastSprites;
                case "boss": return BossSprites;
                default: return null;
            }
        }

        public void Tick(float dt, Vector2 targetPosition)
        {
            EnsureInitialized();
            Vector2 toTarget = targetPosition - (Vector2)transform.position;
            // Nothing else in the project ever set SpriteFlipbook.IsMoving, so the walk-cycle
            // animation never actually advanced regardless of movement - this is the one place
            // that knows, each Tick, whether this enemy is currently moving toward its target.
            if (flipbook != null) flipbook.IsMoving = toTarget.sqrMagnitude > 0.0001f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Vector2 step = toTarget.normalized * speed * dt;
                // NOTE: Rigidbody2D.MovePosition does not apply synchronously
                // in EditMode tests (no active physics step / Play Mode
                // loop), so it leaves transform.position unchanged until a
                // physics step runs. Setting transform.position directly
                // keeps Tick() deterministic and correct in both EditMode
                // tests and Play Mode, at the cost of not going through the
                // physics interpolation MovePosition would otherwise
                // provide. This matches the plan's constraint that movement
                // is Kinematic Rigidbody2D + direct Transform movement only.
                transform.position = (Vector2)transform.position + step;
            }
        }

        public void TakeDamage(float amount)
        {
            EnsureInitialized();
            hp = Mathf.Max(0f, hp - amount);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            var player = other.GetComponent<PlayerController>();
            if (player != null && GameManager.Instance != null)
                GameManager.Instance.OnEnemyTouchedPlayer(this);
        }
    }
}
