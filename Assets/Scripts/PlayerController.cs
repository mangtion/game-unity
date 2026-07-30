using System.Collections.Generic;
using UnityEngine;

namespace YiSunSin
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class PlayerController : MonoBehaviour
    {
        float maxHp;
        float hp;
        float moveSpeed;
        int level;
        float xp;
        float invincibleTimer;
        Dictionary<string, int> upgradeStacks;

        public float MaxHp { get { EnsureInitialized(); return maxHp; } }
        public float Hp { get { EnsureInitialized(); return hp; } }
        public float MoveSpeed { get { EnsureInitialized(); return moveSpeed; } }
        public int Level { get { EnsureInitialized(); return level; } }
        public float Xp { get { EnsureInitialized(); return xp; } }
        public float InvincibleTimer { get { EnsureInitialized(); return invincibleTimer; } }
        public Dictionary<string, int> UpgradeStacks { get { EnsureInitialized(); return upgradeStacks; } }

        Rigidbody2D rb;
        CircleCollider2D col;
        SpriteFlipbook flipbook;
        Vector2 inputDirection;
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
            col.radius = GameConfig.Player.Radius;
            // Not required by this component (some EditMode test fixtures build the GameObject
            // without it) - looked up but not assumed present, same pattern as EnemyController.
            flipbook = GetComponent<SpriteFlipbook>();
            ResetState();
        }

        public void ResetState()
        {
            maxHp = GameConfig.Player.MaxHp;
            hp = maxHp;
            moveSpeed = GameConfig.Player.MoveSpeed;
            level = 1;
            xp = 0f;
            invincibleTimer = 0f;
            upgradeStacks = new Dictionary<string, int>();
        }

        public void SetInput(Vector2 direction)
        {
            EnsureInitialized();
            inputDirection = direction;
        }

        public void Tick(float dt, Rect bounds)
        {
            EnsureInitialized();
            // Nothing else in the project ever set SpriteFlipbook.IsMoving, so the walk-cycle
            // animation never actually advanced regardless of movement - this is the one place
            // that knows, each Tick, whether the player is currently receiving movement input.
            if (flipbook != null) flipbook.IsMoving = inputDirection.sqrMagnitude > 0f;
            if (inputDirection.sqrMagnitude > 0f)
            {
                Vector2 delta = inputDirection.normalized * moveSpeed * dt;
                Vector2 newPos = (Vector2)transform.position + delta;
                newPos.x = Mathf.Clamp(newPos.x, bounds.xMin, bounds.xMax);
                newPos.y = Mathf.Clamp(newPos.y, bounds.yMin, bounds.yMax);
                // NOTE: Rigidbody2D.MovePosition does not apply synchronously
                // in EditMode tests (no active physics step / Play Mode loop),
                // so it leaves transform.position unchanged until a physics
                // step runs. Setting transform.position directly keeps Tick()
                // deterministic and correct in both EditMode tests and Play
                // Mode, at the cost of not going through the physics
                // interpolation MovePosition would otherwise provide.
                transform.position = newPos;
            }

            if (invincibleTimer > 0f)
                invincibleTimer = Mathf.Max(0f, invincibleTimer - dt);
        }

        public bool TakeDamage(float amount)
        {
            EnsureInitialized();
            if (invincibleTimer > 0f) return false;
            hp = Mathf.Max(0f, hp - amount);
            invincibleTimer = GameConfig.Player.InvincibilityDuration;
            return true;
        }

        public int GainXp(float amount)
        {
            EnsureInitialized();
            var result = LevelingSystem.ApplyXp(level, xp, amount);
            level = result.Level;
            xp = result.Xp;
            return result.LevelsGained;
        }

        public void ApplyUpgrade(string upgradeId)
        {
            EnsureInitialized();
            upgradeStacks.TryGetValue(upgradeId, out int current);
            upgradeStacks[upgradeId] = current + 1;

            moveSpeed = UpgradeEffects.EffectiveMoveSpeed(upgradeStacks);
            float newMaxHp = UpgradeEffects.EffectiveMaxHp(upgradeStacks);
            if (!Mathf.Approximately(newMaxHp, maxHp))
            {
                hp += newMaxHp - maxHp;
                maxHp = newMaxHp;
            }
        }
    }
}
