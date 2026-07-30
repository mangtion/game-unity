using UnityEngine;

namespace YiSunSin
{
    // Keeps the full 1600x900 arena visible at any window aspect ratio without
    // stretching or cropping ("letterboxing"). A plain fixed orthographicSize
    // (= ArenaHeight / 2) only guarantees the full arena is visible when the
    // window is at least as wide as 16:9 - on a narrower/taller window the
    // sides of the arena would be clipped, since Unity's orthographic camera
    // only ever locks the vertical extent to orthographicSize and lets the
    // horizontal extent float with Camera.aspect. This component instead
    // grows orthographicSize whenever the window is narrower than 16:9 so the
    // horizontal extent still covers the full 1600-wide arena (revealing more
    // vertical background above/below instead of cropping the sides) -
    // discovered and fixed during Task 16's automated playtest, which
    // rendered the Game view at several aspect ratios and found sub-16:9
    // ratios cropped the arena horizontally.
    [RequireComponent(typeof(Camera))]
    public class CameraFit : MonoBehaviour
    {
        Camera cam;

        void Awake() => cam = GetComponent<Camera>();

        void LateUpdate() => Fit();

        // Exposed so automated/editor verification can force a fit after
        // overriding Camera.aspect, without waiting for a real resize event.
        public void Fit()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null || !cam.orthographic) return;

            float halfHeight = GameConfig.ArenaHeight / 2f;
            float halfWidthNeeded = (GameConfig.ArenaWidth / 2f) / cam.aspect;
            cam.orthographicSize = Mathf.Max(halfHeight, halfWidthNeeded);
        }
    }
}
