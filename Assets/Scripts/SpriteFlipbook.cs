// game-unity/Assets/Scripts/SpriteFlipbook.cs
using UnityEngine;

namespace YiSunSin
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFlipbook : MonoBehaviour
    {
        public Sprite[] Sprites;
        public float FrameRate = 8f;
        public bool IsMoving;

        // Read-only test/debug hook so external code (e.g. Task 16's PlayMode playtest) can
        // verify the walk-cycle actually advances/resets without reaching into private state.
        public int CurrentFrameIndex => frameIndex;

        SpriteRenderer spriteRenderer;
        float frameTimer;
        int frameIndex;

        void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

        void Update()
        {
            if (Sprites == null || Sprites.Length == 0) return;

            if (!IsMoving)
            {
                frameIndex = 0;
                frameTimer = 0f;
                spriteRenderer.sprite = Sprites[0];
                return;
            }

            frameTimer += Time.deltaTime;
            float frameDuration = 1f / FrameRate;
            if (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex = (frameIndex + 1) % Sprites.Length;
            }
            spriteRenderer.sprite = Sprites[frameIndex];
        }
    }
}
