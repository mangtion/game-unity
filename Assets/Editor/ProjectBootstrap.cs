// game-unity/Assets/Editor/ProjectBootstrap.cs
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using YiSunSin;

namespace YiSunSin.EditorTools
{
    public static class ProjectBootstrap
    {
        // The original web-canvas game's js/render.js drawEntity() always draws every entity at
        // exactly 2x its GameConfig radius on screen - visual size is driven by the config radius,
        // not by the source image's native pixel dimensions. This project's world-unit convention
        // (GameConfig.Player.Radius=16, ArenaWidth=1600, etc. used directly as Unity world units)
        // means each sprite must be imported at its OWN spritePixelsPerUnit = frameSize / (2 *
        // thatSprite'sRadius), not one shared value - a single global value (e.g. 1, matching only
        // the character radii) would render arrow/medal ~11x too large relative to their actual
        // GameConfig-driven collision/gameplay footprint, since those sprites have no radius in
        // the same range as the characters. XpGem/medal has no existing GameConfig visual-size
        // constant (XpGemPickupRadius=40 is a magnet/pickup RANGE, not a sprite size), so its
        // target diameter (MedalVisualDiameter below) is a judgment call, not a derived value -
        // documented at its definition.
        const float MedalVisualDiameter = 16f; // pickup icon, kept small/subordinate to characters

        public static void ImportSpriteSheets()
        {
            ImportSheet("Assets/Sprites/player.png", 128, 8, PixelsPerUnitFor(128, GameConfig.Player.Radius));
            ImportSheet("Assets/Sprites/enemy-basic.png", 128, 8, PixelsPerUnitFor(128, GameConfig.EnemyBasic.Radius));
            ImportSheet("Assets/Sprites/enemy-fast.png", 128, 8, PixelsPerUnitFor(128, GameConfig.EnemyFast.Radius));
            ImportSheet("Assets/Sprites/boss.png", 128, 8, PixelsPerUnitFor(128, GameConfig.Boss.Radius));
            ImportSingle("Assets/Sprites/arrow.png", 128, PixelsPerUnitFor(128, GameConfig.Weapon.ProjectileRadius));
            ImportSingle("Assets/Sprites/medal.png", 128, PixelsPerUnitFor(128, MedalVisualDiameter / 2f));
            ImportBackground("Assets/Sprites/background.png");
            AssetDatabase.Refresh();
        }

        // spritePixelsPerUnit such that the sprite renders at diameter = 2 * radius world units,
        // matching js/render.js's drawEntity() convention (see ImportSpriteSheets' comment).
        static float PixelsPerUnitFor(int frameSize, float radius) => frameSize / (2f * radius);

        static void ImportSheet(string path, int frameSize, int frameCount, float pixelsPerUnit)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) { Debug.LogWarning($"Missing sprite: {path}"); return; }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = pixelsPerUnit;

            var metas = new SpriteMetaData[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                metas[i] = new SpriteMetaData
                {
                    name = $"{System.IO.Path.GetFileNameWithoutExtension(path)}_{i}",
                    rect = new Rect(i * frameSize, 0, frameSize, frameSize)
                };
            }
            importer.spritesheet = metas;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        static void ImportSingle(string path, int frameSize, float pixelsPerUnit)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) { Debug.LogWarning($"Missing sprite: {path}"); return; }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        static void ImportBackground(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) { Debug.LogWarning($"Missing sprite: {path}"); return; }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        // Root cause of Task 16's "nothing renders" playtest failures: Task 1 added the
        // com.unity.render-pipelines.universal PACKAGE to manifest.json, but no task ever
        // created a UniversalRenderPipelineAsset + Renderer2DData and assigned it in Graphics
        // Settings / Quality Settings. As a result the project was silently still running the
        // Built-in Render Pipeline the whole time: every Light2D (Task 15's Global Light 2D +
        // BossSpotlight) was inert, and SpriteRenderers were using the Built-in default material,
        // never actually validated under a live 2D URP setup. This creates and assigns the
        // missing URP asset (with a 2D Renderer) so the pipeline the project was designed around
        // is the one actually active. Must run before sprite import / prefab / scene generation
        // so everything downstream is built under the correct active pipeline.
        public static void SetupRenderPipeline()
        {
            Directory.CreateDirectory("Assets/Settings");

            var rendererData = AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/Settings/URP2DRenderer.asset");
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(rendererData, "Assets/Settings/URP2DRenderer.asset");
            }

            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/URPAsset.asset");
            if (pipelineAsset == null)
            {
                pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipelineAsset, "Assets/Settings/URPAsset.asset");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            QualitySettings.renderPipeline = pipelineAsset;

            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();

            if (GraphicsSettings.defaultRenderPipeline == null)
                Debug.LogError("SetupRenderPipeline: GraphicsSettings.defaultRenderPipeline is still null after assignment!");
            else
                Debug.Log($"SetupRenderPipeline: active render pipeline is now '{GraphicsSettings.defaultRenderPipeline.name}'.");
        }

        public static void BuildProject()
        {
            SetupRenderPipeline();
            ImportSpriteSheets();

            Directory.CreateDirectory("Assets/Prefabs");
            Directory.CreateDirectory("Assets/Scenes");
            var projectilePrefab = BuildProjectilePrefab();
            var xpGemPrefab = BuildXpGemPrefab();
            var enemyPrefab = BuildEnemyPrefab();
            var playerPrefab = BuildPlayerPrefab();

            var hitEffect = BuildSimpleBurstEffect("HitEffect", Color.yellow, 0.2f);
            var deathEffect = BuildSimpleBurstEffect("DeathEffect", new Color(0.6f, 0.1f, 0.1f), 0.4f);
            var levelUpEffect = BuildSimpleBurstEffect("LevelUpEffect", Color.cyan, 0.6f);
            var bossSpawnEffect = BuildSimpleBurstEffect("BossSpawnEffect", new Color(0.8f, 0.7f, 0.1f), 1.0f);

            BuildMainScene(playerPrefab, enemyPrefab, projectilePrefab, xpGemPrefab, hitEffect, deathEffect, levelUpEffect, bossSpawnEffect);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Convenience entry point for -executeMethod: opens the saved Main.unity scene fresh
        // (simulating a new Editor session) before running the wiring check, so the check
        // reflects what was actually persisted to disk, not just in-memory state from BuildProject.
        public static void OpenMainSceneAndVerifyUIWiring()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            VerifyUIWiring();
        }

        public static void VerifyUIWiring()
        {
            var uiController = Object.FindAnyObjectByType<UIController>();
            if (uiController == null)
            {
                Debug.LogError("VerifyUIWiring: no UIController found in the open scene!");
                return;
            }
            var fields = typeof(UIController).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            bool anyError = false;
            foreach (var field in fields)
            {
                if (field.GetValue(uiController) == null)
                {
                    Debug.LogError($"UIController.{field.Name} is not wired!");
                    anyError = true;
                }
            }
            if (!anyError)
                Debug.Log("VerifyUIWiring: all UIController fields are wired.");
        }

        static GameObject BuildProjectilePrefab()
        {
            var go = new GameObject("Projectile", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(Projectile));
            go.GetComponent<SpriteRenderer>().sprite = LoadSprite("Assets/Sprites/arrow.png", "arrow");
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/Projectile.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject BuildXpGemPrefab()
        {
            var go = new GameObject("XpGem", typeof(SpriteRenderer), typeof(XpGem));
            go.GetComponent<SpriteRenderer>().sprite = LoadSprite("Assets/Sprites/medal.png", "medal");
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/XpGem.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject BuildEnemyPrefab()
        {
            var go = new GameObject("Enemy", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController), typeof(SpriteFlipbook));
            var controller = go.GetComponent<EnemyController>();
            // One shared prefab represents all 3 enemy types, so every per-type
            // sprite sheet is loaded onto the prefab here; EnemyController.Initialize()
            // then selects the correct array (by TypeId) and wires it into the
            // SpriteFlipbook + SpriteRenderer at spawn time.
            controller.BasicSprites = LoadSpriteSheet("Assets/Sprites/enemy-basic.png", "enemy-basic", 8);
            controller.FastSprites = LoadSpriteSheet("Assets/Sprites/enemy-fast.png", "enemy-fast", 8);
            controller.BossSprites = LoadSpriteSheet("Assets/Sprites/boss.png", "boss", 8);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/Enemy.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject BuildPlayerPrefab()
        {
            var go = new GameObject("Player", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(PlayerController), typeof(SpriteFlipbook));
            var flipbook = go.GetComponent<SpriteFlipbook>();
            flipbook.Sprites = LoadSpriteSheet("Assets/Sprites/player.png", "player", 8);
            go.GetComponent<SpriteRenderer>().sprite = flipbook.Sprites[0];
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/Player.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static ParticleSystem BuildSimpleBurstEffect(string name, Color color, float duration)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = duration;
            main.loop = false;
            main.startLifetime = duration;
            main.startColor = color;
            main.startSize = 0.3f;
            main.startSpeed = 2f;
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;

            var prefabPath = $"Assets/Prefabs/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ParticleSystem>();
        }

        static Sprite LoadSprite(string path, string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(path); // single-sprite import mode: one Sprite sub-asset at the path itself

        static Sprite[] LoadSpriteSheet(string path, string name, int frameCount)
        {
            var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            var byName = new System.Collections.Generic.Dictionary<string, Sprite>();
            foreach (var rep in reps)
                if (rep is Sprite s)
                    byName[s.name] = s;

            var sprites = new Sprite[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                var key = $"{name}_{i}";
                if (byName.TryGetValue(key, out var sprite))
                    sprites[i] = sprite;
                else
                    Debug.LogWarning($"Sprite sub-asset '{key}' not found at {path}");
            }
            return sprites;
        }

        static void BuildMainScene(GameObject playerPrefab, GameObject enemyPrefab, GameObject projectilePrefab, GameObject xpGemPrefab,
            ParticleSystem hitEffect, ParticleSystem deathEffect, ParticleSystem levelUpEffect, ParticleSystem bossSpawnEffect)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(CameraFit));
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = GameConfig.ArenaHeight / 2f; // baseline for 16:9+; CameraFit grows this at runtime for narrower aspects so the full arena stays visible (never cropped)
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.tag = "MainCamera";
            // Default Camera.clearFlags is Skybox, which (with no custom skybox ever assigned)
            // renders Unity's placeholder procedural sky/ground gradient - clearly visible as
            // mismatched blue/brown bands above and below the arena at non-16:9 aspects (found
            // during Task 16's letterbox screenshots). This is a 2D top-down game with no sky to
            // show; a flat solid color is the correct "outside the arena" background at any
            // aspect ratio.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.07f);

            var lightGo = new GameObject("Global Light 2D", typeof(Light2D));
            var light2D = lightGo.GetComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Global;
            light2D.color = new Color(0.55f, 0.6f, 0.75f); // cool dim moonlit-deck tone
            light2D.intensity = 0.6f;

            var spotlightGo = new GameObject("BossSpotlight", typeof(Light2D));
            var spotlight = spotlightGo.GetComponent<Light2D>();
            spotlight.lightType = Light2D.LightType.Point;
            spotlight.color = new Color(1f, 0.85f, 0.4f);
            spotlight.intensity = 3f;
            spotlight.pointLightOuterRadius = 6f;
            spotlightGo.SetActive(false); // GameManager enables this when the boss spawns

            var background = GameObject.CreatePrimitive(PrimitiveType.Quad);
            background.name = "Background";
            Object.DestroyImmediate(background.GetComponent<Collider>());
            background.transform.localScale = new Vector3(GameConfig.ArenaWidth, GameConfig.ArenaHeight, 1f);
            background.transform.position = new Vector3(0f, 0f, 1f);
            // GameObject.CreatePrimitive() leaves Unity's builtin "Default-Material" assigned
            // (the Standard/lit primitive material) unless explicitly replaced. That material
            // is lit and picks up ambient/skybox reflection, which on a huge flat quad seen
            // through an orthographic camera renders as a smooth, wrong-looking radial gradient
            // across the whole arena floor instead of a flat painted color - found during Task
            // 16's automated playtest screenshots (every "bare gameplay" capture showed this
            // gradient where the arena should be) and confirmed by checking this quad's material
            // reference directly in the saved scene (fileID 10303 = builtin Default-Material).
            // Sprites/Default is unlit (matches Player/Enemy/Projectile, which already use it and
            // were never affected), so this makes the background immune to ambient/skybox
            // reflection and renders as the intended flat tone.
            var backgroundMaterial = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
            var backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/background.png");
            if (backgroundSprite != null)
                backgroundMaterial.mainTexture = backgroundSprite.texture;
            else
            {
                backgroundMaterial.color = new Color(0.16f, 0.18f, 0.22f); // moonlit-deck tone fallback
                Debug.LogWarning("BuildMainScene: Assets/Sprites/background.png not found, background will render as flat color.");
            }
            background.GetComponent<MeshRenderer>().sharedMaterial = backgroundMaterial;

            var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            playerInstance.transform.position = Vector3.zero;

            var weaponGo = new GameObject("Weapon", typeof(Weapon));
            weaponGo.transform.SetParent(playerInstance.transform);
            weaponGo.GetComponent<Weapon>().ProjectilePrefab = projectilePrefab;

            var spawnerGo = new GameObject("Spawner", typeof(SpawnController));
            spawnerGo.GetComponent<SpawnController>().EnemyPrefab = enemyPrefab;

            var managerGo = new GameObject("GameManager", typeof(GameManager));
            var manager = managerGo.GetComponent<GameManager>();
            manager.Player = playerInstance.GetComponent<PlayerController>();
            manager.PlayerWeapon = weaponGo.GetComponent<Weapon>();
            manager.Spawner = spawnerGo.GetComponent<SpawnController>();
            manager.XpGemPrefab = xpGemPrefab;
            manager.BossSpotlight = spotlightGo;

            var particleEffectsGo = new GameObject("ParticleEffects", typeof(ParticleEffects));
            var particleEffects = particleEffectsGo.GetComponent<ParticleEffects>();
            particleEffects.HitPrefab = hitEffect;
            particleEffects.DeathPrefab = deathEffect;
            particleEffects.LevelUpPrefab = levelUpEffect;
            particleEffects.BossSpawnPrefab = bossSpawnEffect;

            BuildUI(manager);

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
        }

        static Font DefaultFont() =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static Text MakeText(string name, Transform parent, string content, int fontSize = 24, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = DefaultFont();
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            return text;
        }

        static Button MakeButton(string name, Transform parent, string label, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            var labelText = MakeText("Label", go.transform, label, 20);
            labelText.color = Color.white;

            return go.GetComponent<Button>();
        }

        static void BuildUI(GameManager manager)
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);

            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

            GameObject MakePanel(string name, Transform parent)
            {
                var panel = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                panel.transform.SetParent(parent, false);
                var rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
                return panel;
            }

            var titleScreen = MakePanel("TitleScreen", canvasGo.transform);
            var pauseScreen = MakePanel("PauseScreen", canvasGo.transform);
            var levelUpScreen = MakePanel("LevelUpScreen", canvasGo.transform);
            var gameOverScreen = MakePanel("GameOverScreen", canvasGo.transform);
            var winScreen = MakePanel("WinScreen", canvasGo.transform);
            var hud = MakePanel("Hud", canvasGo.transform);
            hud.GetComponent<Image>().color = Color.clear; // HUD is just a container, not a dimming overlay

            // ---- TitleScreen: title label + Start button ----
            var titleLabelGo = new GameObject("TitleLabel", typeof(RectTransform), typeof(Text));
            titleLabelGo.transform.SetParent(titleScreen.transform, false);
            var titleLabelRect = titleLabelGo.GetComponent<RectTransform>();
            titleLabelRect.anchorMin = new Vector2(0.5f, 0.65f);
            titleLabelRect.anchorMax = new Vector2(0.5f, 0.65f);
            titleLabelRect.sizeDelta = new Vector2(800, 100);
            var titleLabelText = titleLabelGo.GetComponent<Text>();
            titleLabelText.text = "이순신 서바이버";
            titleLabelText.font = DefaultFont();
            titleLabelText.fontSize = 48;
            titleLabelText.alignment = TextAnchor.MiddleCenter;
            titleLabelText.color = Color.white;

            var startButton = MakeButton("StartButton", titleScreen.transform, "시작", new Vector2(0, -60), new Vector2(240, 70));

            // ---- PauseScreen: label ----
            var pauseLabelGo = new GameObject("PauseLabel", typeof(RectTransform), typeof(Text));
            pauseLabelGo.transform.SetParent(pauseScreen.transform, false);
            var pauseLabelRect = pauseLabelGo.GetComponent<RectTransform>();
            pauseLabelRect.anchorMin = Vector2.zero;
            pauseLabelRect.anchorMax = Vector2.one;
            pauseLabelRect.offsetMin = Vector2.zero;
            pauseLabelRect.offsetMax = Vector2.zero;
            var pauseLabelText = pauseLabelGo.GetComponent<Text>();
            pauseLabelText.text = "일시정지 (ESC로 재개)";
            pauseLabelText.font = DefaultFont();
            pauseLabelText.fontSize = 40;
            pauseLabelText.alignment = TextAnchor.MiddleCenter;
            pauseLabelText.color = Color.white;

            // ---- LevelUpScreen: title label + upgrade card container ----
            var levelUpLabelGo = new GameObject("LevelUpLabel", typeof(RectTransform), typeof(Text));
            levelUpLabelGo.transform.SetParent(levelUpScreen.transform, false);
            var levelUpLabelRect = levelUpLabelGo.GetComponent<RectTransform>();
            levelUpLabelRect.anchorMin = new Vector2(0.5f, 0.8f);
            levelUpLabelRect.anchorMax = new Vector2(0.5f, 0.8f);
            levelUpLabelRect.sizeDelta = new Vector2(800, 80);
            var levelUpLabelText = levelUpLabelGo.GetComponent<Text>();
            levelUpLabelText.text = "레벨 업! 강화를 선택하세요";
            levelUpLabelText.font = DefaultFont();
            levelUpLabelText.fontSize = 32;
            levelUpLabelText.alignment = TextAnchor.MiddleCenter;
            levelUpLabelText.color = Color.white;

            var upgradeCardContainerGo = new GameObject("UpgradeCardContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            upgradeCardContainerGo.transform.SetParent(levelUpScreen.transform, false);
            var containerRect = upgradeCardContainerGo.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.4f);
            containerRect.anchorMax = new Vector2(0.5f, 0.4f);
            containerRect.sizeDelta = new Vector2(1200, 300);
            var layoutGroup = upgradeCardContainerGo.GetComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 24;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            // ---- UpgradeCardPrefab: Button + Image + [Name, Description] Text children ----
            var upgradeCardGo = new GameObject("UpgradeCard", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            var cardRect = upgradeCardGo.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(280, 280);
            upgradeCardGo.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f, 0.95f);
            var cardLayoutElement = upgradeCardGo.GetComponent<LayoutElement>();
            cardLayoutElement.preferredWidth = 280;
            cardLayoutElement.preferredHeight = 280;

            var nameTextGo = new GameObject("NameText", typeof(RectTransform), typeof(Text));
            nameTextGo.transform.SetParent(upgradeCardGo.transform, false);
            var nameRect = nameTextGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0.6f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = new Vector2(10, 0);
            nameRect.offsetMax = new Vector2(-10, 0);
            var nameText = nameTextGo.GetComponent<Text>();
            nameText.text = "Upgrade Name";
            nameText.font = DefaultFont();
            nameText.fontSize = 22;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;

            var descTextGo = new GameObject("DescriptionText", typeof(RectTransform), typeof(Text));
            descTextGo.transform.SetParent(upgradeCardGo.transform, false);
            var descRect = descTextGo.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0f, 0f);
            descRect.anchorMax = new Vector2(1f, 0.6f);
            descRect.offsetMin = new Vector2(10, 10);
            descRect.offsetMax = new Vector2(-10, 0);
            var descText = descTextGo.GetComponent<Text>();
            descText.text = "Upgrade description";
            descText.font = DefaultFont();
            descText.fontSize = 18;
            descText.alignment = TextAnchor.UpperCenter;
            descText.color = Color.white;

            Directory.CreateDirectory("Assets/Prefabs");
            var upgradeCardPrefab = PrefabUtility.SaveAsPrefabAsset(upgradeCardGo, "Assets/Prefabs/UpgradeCardPrefab.prefab");
            Object.DestroyImmediate(upgradeCardGo);

            // ---- GameOverScreen: stats text + Restart button ----
            var gameOverStatsText = MakeText("GameOverStatsText", gameOverScreen.transform, "", 32);
            var gameOverRestartButton = MakeButton("RestartButton", gameOverScreen.transform, "다시 시작", new Vector2(0, -120), new Vector2(240, 70));

            // ---- WinScreen: stats text + Restart button ----
            var winStatsText = MakeText("WinStatsText", winScreen.transform, "", 32);
            var winRestartButton = MakeButton("RestartButton", winScreen.transform, "다시 시작", new Vector2(0, -120), new Vector2(240, 70));

            // ---- Hud: HP text + timer text ----
            var hpTextGo = new GameObject("HpBarText", typeof(RectTransform), typeof(Text));
            hpTextGo.transform.SetParent(hud.transform, false);
            var hpRect = hpTextGo.GetComponent<RectTransform>();
            hpRect.anchorMin = new Vector2(0f, 1f);
            hpRect.anchorMax = new Vector2(0f, 1f);
            hpRect.pivot = new Vector2(0f, 1f);
            hpRect.anchoredPosition = new Vector2(24, -24);
            hpRect.sizeDelta = new Vector2(320, 50);
            var hpText = hpTextGo.GetComponent<Text>();
            hpText.text = "HP 100/100";
            hpText.font = DefaultFont();
            hpText.fontSize = 28;
            hpText.alignment = TextAnchor.UpperLeft;
            hpText.color = Color.white;

            var timerTextGo = new GameObject("TimerText", typeof(RectTransform), typeof(Text));
            timerTextGo.transform.SetParent(hud.transform, false);
            var timerRect = timerTextGo.GetComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 1f);
            timerRect.anchorMax = new Vector2(0.5f, 1f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerRect.anchoredPosition = new Vector2(0, -24);
            timerRect.sizeDelta = new Vector2(200, 50);
            var timerText = timerTextGo.GetComponent<Text>();
            timerText.text = "00:00";
            timerText.font = DefaultFont();
            timerText.fontSize = 28;
            timerText.alignment = TextAnchor.UpperCenter;
            timerText.color = Color.white;

            // ---- UIController wiring ----
            var uiControllerGo = new GameObject("UIController", typeof(UIController));
            var uiController = uiControllerGo.GetComponent<UIController>();
            uiController.Manager = manager;
            uiController.Player = manager.Player;
            uiController.TitleScreen = titleScreen;
            uiController.PauseScreen = pauseScreen;
            uiController.LevelUpScreen = levelUpScreen;
            uiController.GameOverScreen = gameOverScreen;
            uiController.WinScreen = winScreen;
            uiController.Hud = hud;
            uiController.UpgradeCardContainer = upgradeCardContainerGo.transform;
            uiController.UpgradeCardPrefab = upgradeCardPrefab;
            uiController.GameOverStatsText = gameOverStatsText;
            uiController.WinStatsText = winStatsText;
            uiController.HpBarText = hpText;
            uiController.TimerText = timerText;

            // Plain onClick.AddListener() registers a non-persistent (runtime-only) listener that
            // does NOT get serialized by SaveScene — verified empirically: after a first pass using
            // AddListener, the saved Main.unity's Button.m_OnClick.m_PersistentCalls.m_Calls came
            // back empty. UnityEventTools.AddPersistentListener is required so the listener survives
            // into the saved scene asset and fires when a fresh Editor/Player opens it.
            UnityEditor.Events.UnityEventTools.AddPersistentListener(startButton.onClick, uiController.OnStartClicked);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(gameOverRestartButton.onClick, uiController.OnRestartClicked);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(winRestartButton.onClick, uiController.OnRestartClicked);
        }
    }
}
