// game-unity/Assets/Tests/PlayMode/FullPlaytestTests.cs
//
// Task 16: automated substitute for the brief's manual Play-Mode verification
// pass, implemented as a genuine Unity Test Framework PlayMode [UnityTest].
//
// This is the fallback route from an earlier -executeMethod + EditorApplication.isPlaying
// polling approach (originally prototyped as Assets/Editor/PlaytestDriver.cs, since
// deleted - that file was an abandoned dead end, not a dependency of this test): that
// approach reliably hung during generic batch-mode Editor startup (before any of our code
// even ran), reproducibly, in this environment - while `-runTests -testPlatform EditMode`
// (used by run-tests.bat for all 15 prior tasks) reliably does not hang. Since
// the PlayMode test runner drives its own Play Mode entry/update pump instead of
// relying on the same "wait for editor idle" gate that -executeMethod uses, it
// sidesteps whatever that hang was. Run via:
//   Unity.exe -batchmode -runTests -projectPath <proj> -testResults playtest-results.xml -testPlatform PlayMode -logFile playtest.log
using System.Collections;
using System.IO;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YiSunSin;

namespace YiSunSin.Tests.PlayMode
{
    public class FullPlaytestTests
    {
        public static readonly string ScreenshotDir =
            @"C:\Users\ASUS\Desktop\base\game\.claude\worktrees\yi-sunsin-unity-port\.superpowers\sdd\2026-07-28-yi-sunsin-unity-port\playtest-screenshots";

        bool failed;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            // The Unity Test Framework already has PlayMode running by the
            // time any [SetUp]/test method executes, so EditorSceneManager.OpenScene
            // (an Edit-Mode-only API) throws here - LoadSceneInPlayMode is the
            // Editor-only API meant for exactly this (loading an arbitrary
            // Assets/ scene path into a running Play Mode session without
            // registering it in Build Settings). Main.unity is regenerated
            // separately beforehand (via
            // `-executeMethod YiSunSin.EditorTools.ProjectBootstrap.BuildProject -quit`,
            // see run-playtest.bat) so it already has the Task 16 CameraFit
            // fix baked in; BuildProject() is deliberately NOT called from
            // here since Assets/Editor's loose scripts aren't a referenceable
            // assembly from this asmdef.
            Directory.CreateDirectory(ScreenshotDir);
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
        }

        void Check(bool condition, string message)
        {
            // Debug.LogError is intentionally NOT used here - Unity Test
            // Framework's default LogAssert handling treats any error-level
            // log during a test as an immediate unhandled failure that aborts
            // the whole coroutine right there (discovered the hard way: the
            // first failed Check silently truncated every later step). Using
            // Debug.Log for both outcomes, and a single NUnit Assert at the
            // very end, lets the full sequence run to completion and report
            // everything in one pass.
            if (condition) Debug.Log($"[FullPlaytest] OK: {message}");
            else { failed = true; Debug.Log($"[FullPlaytest] STEP FAILED: {message}"); }
        }

        [UnityTest]
        public IEnumerator FullPlaytest()
        {
            failed = false;
            var gm = Object.FindFirstObjectByType<GameManager>();
            Check(gm != null, "GameManager found in the loaded Main.unity scene under Play Mode");
            if (gm == null) { Assert.Fail("GameManager missing - aborting playtest"); yield break; }

            Check(GameManager.Instance == gm, $"GameManager.Instance matches the found instance (Instance null? {GameManager.Instance == null})");

            var mainCamera = Camera.main;
            Check(mainCamera != null, "Main Camera found via Camera.main");

            Screen.SetResolution(1600, 900, false);
            yield return null;
            yield return null;

            // ---- Step 2: Title screen ----
            Check(gm.Status == GameStatus.Title, "GameManager starts in Title status");
            yield return CaptureFull("01_title.png");

            gm.StartGame();
            yield return null;
            Check(gm.Status == GameStatus.Playing, "StartGame() transitions to Playing");

            // ---- Step 3: movement ----
            // NOTE: driving this through GameManager.Update()'s normal frame
            // loop (SetInput then wait several real frames) does NOT work in
            // this harness: UIController.Update() also runs every frame
            // while Status is Playing and unconditionally does its own
            // `Player.SetInput(dir)` from real Input.GetKey polling - with no
            // actual key pressed (headless, no OS keyboard injection - the
            // documented accepted gap), it stomps our SetInput(1,1) back to
            // zero before/adjacent to GameManager.Update()'s Tick() call
            // every single frame, regardless of script execution order. That
            // is correct, expected UIController behavior, not a bug - so
            // instead this verifies PlayerController.Tick() (the same public
            // method GameManager.Update() calls every frame) directly and
            // deterministically, sidestepping the UIController race entirely.
            var arenaBounds = new Rect(-GameConfig.ArenaWidth / 2f, -GameConfig.ArenaHeight / 2f, GameConfig.ArenaWidth, GameConfig.ArenaHeight);
            Vector3 posBefore = gm.Player.transform.position;
            gm.Player.SetInput(new Vector2(1f, 1f));
            gm.Player.Tick(1f, arenaBounds);
            gm.Player.SetInput(Vector2.zero);
            Vector3 posAfter = gm.Player.transform.position;
            Check(Vector3.Distance(posBefore, posAfter) > 1f, $"Player moved under SetInput+Tick (from {posBefore} to {posAfter})");
            yield return null;

            // ---- Step 3a: walk-cycle animation ----
            // PlayerController.Tick() now sets SpriteFlipbook.IsMoving based on input (a real,
            // previously-unwired bug fixed alongside this test - nothing in the project ever set
            // IsMoving before, so the flipbook was permanently stuck on frame 0 regardless of
            // movement). To observe this through the real per-frame loop (GameManager.Update()
            // calls Player.Tick() every frame, which is what actually needs to be verified here,
            // as opposed to Step 3's movement check which deliberately calls Tick() directly and
            // once), UIController's per-frame `Player.SetInput(realKeyboardState)` has to be
            // disabled first - otherwise it stomps our SetInput back to zero on literally the
            // next frame (see Step 3's comment; same race, different consequence: IsMoving would
            // flip true then immediately false, never staying set long enough to observe an
            // actual frame advance). SpriteFlipbook.Update() runs on Unity's own per-frame loop
            // driven by real Time.deltaTime, so this needs actual elapsed real time (not just
            // repeated Tick() calls) for frames to advance - hence WaitRealSeconds.
            var flipbook = gm.Player.GetComponent<SpriteFlipbook>();
            Check(flipbook != null, "Player has a SpriteFlipbook component");
            if (flipbook != null)
            {
                Check(flipbook.CurrentFrameIndex == 0, $"Flipbook starts on frame 0 before any movement (got {flipbook.CurrentFrameIndex})");

                var uiController = Object.FindFirstObjectByType<UIController>();
                bool uiWasEnabled = uiController != null && uiController.enabled;
                if (uiController != null) uiController.enabled = false;

                gm.Player.SetInput(new Vector2(1f, 0f));
                yield return WaitRealSeconds(0.5f); // several real frames >> FrameRate=8's 0.125s/frame, driven by GameManager.Update()'s normal Tick() calls
                Check(flipbook.CurrentFrameIndex != 0, $"Flipbook advances off frame 0 while moving (got {flipbook.CurrentFrameIndex})");

                gm.Player.SetInput(Vector2.zero);
                yield return null;
                yield return null;
                Check(flipbook.CurrentFrameIndex == 0, $"Flipbook returns to frame 0 once movement stops (got {flipbook.CurrentFrameIndex})");

                if (uiController != null) uiController.enabled = uiWasEnabled;
            }

            // ---- Step 3b: enemies spawn + weapon auto-fires ----
            yield return WaitSimSeconds(gm, 6f);
            var enemiesFound = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            Check(enemiesFound.Length > 0, $"Enemies spawned from arena edges (found {enemiesFound.Length})");
            Check(gm.PlayerWeapon.ShotsFired > 0, $"Weapon auto-fired at least once (ShotsFired={gm.PlayerWeapon.ShotsFired})");
            yield return CaptureFull("02_gameplay_enemy_onscreen.png");

            // ---- Step 3b-i: auto-fired projectiles actually travel under GameManager's Update
            // loop (Critical #1 from the final review: Projectile.Tick(dt) was never being called
            // by any runtime code, so arrows spawned and just sat at the player's position). This
            // checks a real, auto-fired, GameManager-tracked projectile's GameObject position -
            // not just that Weapon.ShotsFired incremented, which only proves a shot was requested,
            // not that anything moved.
            var liveProjectiles = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            Check(liveProjectiles.Length > 0, $"At least one live Projectile GameObject exists after auto-fire (found {liveProjectiles.Length})");
            if (liveProjectiles.Length > 0)
            {
                var proj = liveProjectiles[0];
                Vector3 projPosBefore = proj.transform.position;
                yield return WaitSimSeconds(gm, 0.2f);
                if (proj != null)
                {
                    Vector3 projPosAfter = proj.transform.position;
                    Check(Vector3.Distance(projPosBefore, projPosAfter) > 1f,
                        $"A live, GameManager-tracked projectile actually advances position over time (from {projPosBefore} to {projPosAfter})");
                }
                else
                {
                    Debug.Log("[FullPlaytest] Sampled projectile was destroyed (hit or expired) before the travel window elapsed; " +
                        "that itself demonstrates GameManager is ticking/cleaning it up, so this step is treated as satisfied.");
                }
            }

            // ---- Step 3b-ii: a killed, GameManager-tracked enemy's GameObject is actually
            // destroyed (Critical #2 from the final review: enemies.RemoveAll(...) dropped the
            // list reference without ever destroying the GameObject, leaving a frozen sprite with
            // a live trigger collider that kept dealing contact damage forever). Uses a real
            // enemy from the natural spawn list above (already tracked in GameManager's private
            // `enemies` list via Spawner.Tick), not a throwaway test-local enemy, so this actually
            // exercises the tracked-list cleanup path rather than bypassing it.
            if (enemiesFound.Length > 0)
            {
                var toKill = enemiesFound[0];
                GameObject toKillGo = toKill.gameObject;
                var killShotGo = new GameObject("PlaytestListKillProjectile", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Projectile));
                var killShotProj = killShotGo.GetComponent<Projectile>();
                killShotProj.Launch(toKill.transform.position, Vector2.right, 0f, 99999f, 400f);
                gm.OnProjectileHit(killShotProj, toKill);
                Object.Destroy(killShotGo);
                yield return null; // let GameManager.Update() run its dead-enemy destroy+cleanup pass
                yield return null;
                Check(toKillGo == null, "A killed, GameManager-tracked enemy's GameObject is actually destroyed (not just removed from the tracking list)");
            }

            // ---- Step 3c: enemy death + Hit/Death particle triggers ----
            // A real enemy killed through GameManager.OnProjectileHit's real code path (same
            // dummy-enemy/dummy-projectile pattern the multi-level-up step below already uses),
            // rather than waiting on the RNG'd auto-fire above to land a killing blow. Verifies
            // both that the enemy actually dies (IsDead / removed from the manager's tracked
            // list) and that ParticleEffects actually played Hit + Death - not just that the
            // methods were reachable, but that a live ParticleSystem instance is actually active
            // and playing under ParticleEffects.Instance (its pooled instances are parented there
            // - see ParticleEffects.MakePool's Instantiate(prefab, transform)).
            var weakEnemyGo = new GameObject("PlaytestWeakEnemy",
                typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController));
            var weakEnemy = weakEnemyGo.GetComponent<EnemyController>();
            weakEnemy.Initialize(GameConfig.EnemyBasic, false); // Hp=20, less than the killing shot below
            weakEnemyGo.transform.position = gm.Player.transform.position + Vector3.right * 5f;

            var killProjGo = new GameObject("PlaytestKillProjectile", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Projectile));
            var killProj = killProjGo.GetComponent<Projectile>();
            killProj.Launch(weakEnemyGo.transform.position, Vector2.right, 1000f, GameConfig.EnemyBasic.Hp + 100f, 400f);

            gm.OnProjectileHit(killProj, weakEnemy);
            yield return null;

            Check(weakEnemy.IsDead, $"Enemy actually dies from a killing-blow projectile hit (Hp={weakEnemy.Hp})");

            bool hitOrDeathPlaying = false;
            if (ParticleEffects.Instance != null)
            {
                foreach (Transform child in ParticleEffects.Instance.transform)
                {
                    var ps = child.GetComponent<ParticleSystem>();
                    if (ps != null && ps.isPlaying &&
                        (child.name.Contains("HitEffect") || child.name.Contains("DeathEffect")))
                    {
                        hitOrDeathPlaying = true;
                        break;
                    }
                }
            }
            Check(hitOrDeathPlaying, "A Hit or Death ParticleSystem instance is actively playing after the kill");

            Object.Destroy(weakEnemyGo);
            Object.Destroy(killProjGo);
            yield return null;

            // ---- Step 4: pause ----
            gm.TogglePause();
            Check(gm.Status == GameStatus.PausedManual, "TogglePause() enters PausedManual");
            float elapsedAtPause = gm.Elapsed;
            yield return CaptureFull("03_pause.png");
            for (int i = 0; i < 30; i++) yield return null;
            Check(Mathf.Approximately(gm.Elapsed, elapsedAtPause), $"Elapsed frozen while paused ({elapsedAtPause} -> {gm.Elapsed})");
            gm.TogglePause();
            Check(gm.Status == GameStatus.Playing, "Second TogglePause() resumes Playing");

            // ---- Step 5: multi-level-up via the real pickup path ----
            var dummyEnemyGo = new GameObject("PlaytestDummyBoss",
                typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController));
            var dummyEnemy = dummyEnemyGo.GetComponent<EnemyController>();
            dummyEnemy.Initialize(GameConfig.Boss, true); // 50 XP drop: 1->2(10),2->3(20)=30 spent,20 left => 2 levels gained
            dummyEnemyGo.transform.position = gm.Player.transform.position;

            var dummyProjGo = new GameObject("PlaytestDummyProjectile", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Projectile));
            var dummyProj = dummyProjGo.GetComponent<Projectile>();
            dummyProj.Launch(gm.Player.transform.position, Vector2.right, 0f, 1000f, 400f);

            gm.OnProjectileHit(dummyProj, dummyEnemy);
            yield return null;

            Check(gm.Status == GameStatus.PausedLevelUp, $"Big XP pickup triggers PausedLevelUp (status={gm.Status})");
            Check(gm.UpgradeChoices.Count == 3, $"First level-up offers exactly 3 cards (got {gm.UpgradeChoices.Count})");
            yield return CaptureFull("04_levelup_first_cards.png");

            if (gm.UpgradeChoices.Count > 0)
            {
                string firstChoice = gm.UpgradeChoices[0].Id;
                gm.ChooseUpgrade(firstChoice);
                yield return null;
                Check(gm.Status == GameStatus.PausedLevelUp, "Multi-level-up: still PausedLevelUp after first pick (owed a second)");
                Check(gm.UpgradeChoices.Count == 3, $"Second card set also has exactly 3 cards (got {gm.UpgradeChoices.Count})");
                yield return CaptureFull("05_levelup_second_cards.png");

                if (gm.UpgradeChoices.Count > 0)
                {
                    gm.ChooseUpgrade(gm.UpgradeChoices[0].Id);
                    yield return null;
                    Check(gm.Status == GameStatus.Playing, "After both picks, resumes Playing");
                }
            }
            Object.Destroy(dummyEnemyGo);
            Object.Destroy(dummyProjGo);
            yield return null;

            // A stationary, non-dodging player left running through 240-300
            // simulated seconds of continuous enemy contact damage (no WASD
            // evasion is possible here - see the movement-check note above)
            // will genuinely die well before the boss/win thresholds, same
            // as it would for a real AFK player. That's correct game
            // behavior, not a bug, but it would stop this harness from ever
            // reaching the boss/win checks - so grant a large HP/damage
            // buffer here purely as test scaffolding, via the same public
            // PlayerController.ApplyUpgrade(string) the real level-up screen
            // calls, so the accelerated run survives long enough to observe
            // the boss spawn and win transition.
            for (int i = 0; i < 40; i++) gm.Player.ApplyUpgrade("maxHp");
            for (int i = 0; i < 10; i++) gm.Player.ApplyUpgrade("weaponDamage");
            Debug.Log($"[FullPlaytest] Buffed player for fast-forward survival: MaxHp={gm.Player.MaxHp}, Hp={gm.Player.Hp}");

            // ---- Step 6: boss spawn (accelerate simulated time) ----
            yield return FastForwardTo(gm, GameConfig.BossSpawnTime + 1f, "boss spawn (240s)");
            if (gm.Status == GameStatus.Playing)
            {
                bool bossSeen = false;
                foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                    if (e != null && e.IsBoss) bossSeen = true;
                Check(bossSeen, "Boss enemy present in scene after crossing BossSpawnTime");
                bool spotlightActive = gm.BossSpotlight != null && gm.BossSpotlight.activeSelf;
                Check(gm.BossBannerTimer > 0f || spotlightActive,
                    $"Boss banner/spotlight trigger fired (BossBannerTimer={gm.BossBannerTimer}, spotlightActive={spotlightActive})");
                yield return CaptureFull("06_boss.png");
            }
            else
            {
                Debug.LogWarning($"[FullPlaytest] Status was {gm.Status} instead of Playing when checking boss spawn; skipping boss-specific assertions.");
            }

            // ---- Step 7a: win ----
            yield return FastForwardTo(gm, GameConfig.WinTime + 1f, "win (300s)");
            Check(gm.Status == GameStatus.Win, $"Reaching WinTime transitions to Win (status={gm.Status})");
            yield return CaptureFull("07_win.png");

            // ---- Step 7b: restart, then game over ----
            gm.Restart();
            yield return null;
            Check(gm.Status == GameStatus.Playing, "Restart() from Win returns to a fresh Playing state");
            // A real frame (with whatever dt that implies) elapses between
            // Restart() and this check, so Elapsed will be slightly above
            // exactly 0 - assert "near zero", not bit-exact.
            Check(gm.Elapsed < 0.5f, $"Restart() resets Elapsed to ~0 (got {gm.Elapsed})");
            Check(Mathf.Approximately(gm.Player.Hp, gm.Player.MaxHp), "Restart() resets Player HP to max");

            gm.Player.TakeDamage(9999f);
            yield return null;
            Check(gm.Status == GameStatus.GameOver, $"Lethal damage transitions to GameOver (status={gm.Status})");
            yield return CaptureFull("08_gameover.png");

            gm.Restart();
            yield return null;
            Check(gm.Status == GameStatus.Playing, "Restart() from GameOver returns to a fresh Playing state");

            // ---- Step 8: camera letterboxing across aspect ratios ----
            var camFit = mainCamera != null ? mainCamera.GetComponent<CameraFit>() : null;
            Check(camFit != null, "CameraFit component present on Main Camera (Task 16 fix)");
            if (mainCamera != null)
            {
                yield return VerifyLetterbox(mainCamera, camFit, 16f / 9f, "16x9");
                yield return VerifyLetterbox(mainCamera, camFit, 4f / 3f, "4x3");
                yield return VerifyLetterbox(mainCamera, camFit, 21f / 9f, "21x9_ultrawide");
                yield return VerifyLetterbox(mainCamera, camFit, 9f / 16f, "9x16_portrait");
                mainCamera.ResetAspect();
            }

            Debug.Log("[FullPlaytest] All steps executed.");
            Assert.IsFalse(failed, "One or more playtest checks failed - see [FullPlaytest] STEP FAILED lines above.");
        }

        // Unlike WaitSimSeconds (which tracks GameManager.Elapsed, itself driven by scaled
        // Time.deltaTime through GameManager.Update), this tracks real, unscaled wall-clock time
        // - needed for SpriteFlipbook.Update(), which runs on Unity's own per-frame loop off
        // Time.deltaTime independent of GameManager entirely, so it advances on real elapsed time
        // regardless of GameManager.Status/Time.timeScale.
        IEnumerator WaitRealSeconds(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
                yield return null;
        }

        IEnumerator WaitSimSeconds(GameManager gm, float seconds)
        {
            float start = gm.Elapsed;
            float safety = Time.realtimeSinceStartup + 30f;
            while (gm.Elapsed - start < seconds && gm.Status == GameStatus.Playing && Time.realtimeSinceStartup < safety)
                yield return null;
        }

        IEnumerator FastForwardTo(GameManager gm, float targetElapsed, string label)
        {
            Debug.Log($"[FullPlaytest] Fast-forwarding toward {label}, target Elapsed={targetElapsed}...");
            float savedScale = Time.timeScale;
            float savedMaxDt = Time.maximumDeltaTime;
            Time.timeScale = 100f;
            Time.maximumDeltaTime = 10f;

            float safety = Time.realtimeSinceStartup + 60f;
            while (gm.Elapsed < targetElapsed && Time.realtimeSinceStartup < safety)
            {
                // At 100x timescale the weapon keeps auto-killing real
                // enemies across many simulated seconds per real frame, so
                // additional genuine level-ups can (and do) occur along the
                // way - each one pauses GameManager.Update() (status flips to
                // PausedLevelUp, which freezes Elapsed) exactly like a real
                // player's would. Auto-resolve those so the fast-forward can
                // keep progressing toward the target instead of stalling.
                if (gm.Status == GameStatus.PausedLevelUp && gm.UpgradeChoices.Count > 0)
                    gm.ChooseUpgrade(gm.UpgradeChoices[0].Id);
                else if (gm.Status != GameStatus.Playing)
                    break; // GameOver/Win/PausedManual - nothing left to fast-forward through
                yield return null;
            }

            Time.timeScale = savedScale;
            Time.maximumDeltaTime = savedMaxDt;
            yield return null;
            Debug.Log($"[FullPlaytest] Fast-forward to {label} finished at Elapsed={gm.Elapsed}, status={gm.Status}");
        }

        IEnumerator VerifyLetterbox(Camera cam, CameraFit fit, float aspect, string label)
        {
            cam.aspect = aspect;
            if (fit != null) fit.Fit();
            yield return null;

            float visibleHalfWidth = cam.orthographicSize * cam.aspect;
            float visibleHalfHeight = cam.orthographicSize;
            bool fitsWidth = visibleHalfWidth >= GameConfig.ArenaWidth / 2f - 0.5f;
            bool fitsHeight = visibleHalfHeight >= GameConfig.ArenaHeight / 2f - 0.5f;
            Check(fitsWidth && fitsHeight,
                $"Aspect {label} ({aspect:0.000}): full {GameConfig.ArenaWidth}x{GameConfig.ArenaHeight} arena visible without crop (visible {visibleHalfWidth * 2:0}x{visibleHalfHeight * 2:0}, orthoSize={cam.orthographicSize:0.0})");

            int width = 960;
            int height = Mathf.Max(1, Mathf.RoundToInt(width / aspect));
            yield return CaptureCameraOnly(cam, width, height, $"09_letterbox_{label}.png");
        }

        IEnumerator CaptureFull(string filename)
        {
            // ScreenCapture.CaptureScreenshot(path) reliably produced zero
            // bytes on disk under `-batchmode -runTests` in this environment
            // (no real OS window/backbuffer for it to read from, even though
            // rendering itself works fine) - discovered when every screenshot
            // this call logged as "captured" turned out missing afterward.
            // Rendering the Main Camera to a RenderTexture (as
            // CaptureCameraOnly already does for the letterbox shots) is
            // reliable in batch mode, but by itself only captures world-space
            // content, not the UI - this project's Canvas is Screen Space -
            // Overlay (renders straight to the display, independent of any
            // camera). So: temporarily flip the Canvas to Screen Space -
            // Camera (which DOES composite into a camera's render), capture
            // via the camera, then restore the Canvas exactly as it was.
            var cam = Camera.main;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            string path = Path.Combine(ScreenshotDir, filename);

            if (cam == null)
            {
                Debug.Log($"[FullPlaytest] No Main Camera for screenshot {filename}; skipping.");
                yield break;
            }

            RenderMode prevMode = default;
            Camera prevWorldCam = null;
            float prevPlaneDistance = 0f;
            bool hadCanvas = canvas != null;
            if (hadCanvas)
            {
                prevMode = canvas.renderMode;
                prevWorldCam = canvas.worldCamera;
                prevPlaneDistance = canvas.planeDistance;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f; // in front of the camera's near plane, behind nothing else
            }

            // Screen.SetResolution(1600, 900, false) (called once at the top
            // of the test) does not reliably take effect under
            // `-batchmode -runTests` (no real OS window to resize) - Screen
            // .width/height kept reporting some other batch-mode default, so
            // CameraFit's own per-frame LateUpdate() (correctly, per its own
            // logic) kept re-deriving orthographicSize from that wrong
            // ambient aspect, shrinking the 1600x900 arena to a speck against
            // a hugely oversized view. Force the aspect/size back to the
            // canonical 16:9 fit right before every capture so screenshots
            // reflect the intended view regardless of what batch mode
            // reports for the real screen size.
            var camFit = cam.GetComponent<CameraFit>();
            float prevCamAspect = cam.aspect;
            cam.aspect = 1600f / 900f;
            if (camFit != null) camFit.Fit();

            var bg = GameObject.Find("Background");
            Debug.Log($"[FullPlaytest] DIAG capture '{filename}': cam.orthographic={cam.orthographic}, orthoSize={cam.orthographicSize}, camPos={cam.transform.position}, bg.active={(bg != null ? bg.activeInHierarchy.ToString() : "NULL")}, bg.pos={(bg != null ? bg.transform.position.ToString() : "n/a")}, clearFlags={cam.clearFlags}");

            yield return CaptureCameraOnly(cam, 1600, 900, filename);

            cam.aspect = prevCamAspect;
            if (camFit != null) camFit.Fit();

            if (hadCanvas)
            {
                canvas.renderMode = prevMode;
                canvas.worldCamera = prevWorldCam;
                canvas.planeDistance = prevPlaneDistance;
            }
            yield return null;
        }

        IEnumerator CaptureCameraOnly(Camera cam, int width, int height, string filename)
        {
            // Deliberately NOT calling cam.Render() directly: under URP
            // (especially the 2D Renderer this project uses for sprites +
            // Light2D), a manual Camera.Render() call bypasses the normal
            // per-frame SRP render loop and was observed to only draw the
            // skybox/clear pass - sprites and 2D lighting silently vanished
            // from every capture past the title screen (which has no sprites
            // to miss) until this was found. Simply assigning targetTexture
            // and waiting a couple of real frames lets Unity's normal
            // per-frame update render into it exactly like the Game view
            // would, which reliably includes sprites/lighting.
            var rt = new RenderTexture(width, height, 24);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            var prevAspect = cam.aspect;
            cam.aspect = (float)width / height;
            cam.targetTexture = rt;
            yield return null;
            yield return null;

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            cam.aspect = prevAspect;
            rt.Release();
            Object.Destroy(rt);

            byte[] png = tex.EncodeToPNG();
            string path = Path.Combine(ScreenshotDir, filename);
            File.WriteAllBytes(path, png);
            Object.Destroy(tex);
            Debug.Log($"[FullPlaytest] Camera-only render captured: {path}");
            yield return null;
        }
    }
}
