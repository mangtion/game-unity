# 비주얼 리부트 (데모 0.0.1 → 벡터/카툰 아트) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 플레이어/적/보스/투사체/픽업 스프라이트를 "진지한 그래픽노벨풍" 벡터 아트로 교체하고, 조선시대 마을/전장터 배경 이미지를 추가한다.

**Architecture:** 이 프로젝트는 `Assets/Editor/ProjectBootstrap.cs`가 스프라이트 임포트 설정과 프리팹·씬을 전부 코드로 생성하는 빌드 스크립트 패턴을 쓴다. 따라서 새 아트를 만들고 `ProjectBootstrap.cs`의 프레임 크기/개수/필터모드/배경 텍스처 관련 부분만 수정한 뒤 `-executeMethod ProjectBootstrap.BuildProject`로 전체를 재생성하면 프리팹과 씬이 자동으로 새 아트에 맞춰진다. 수작업으로 프리팹을 열어 스프라이트를 드래그하거나 콜라이더 크기를 손으로 맞출 필요가 없다.

**Tech Stack:** Unity 6000.5.5f1, C# (Editor 스크립트), URP 2D, Unity Test Framework(EditMode/PlayMode), Hugging Face 이미지 생성 도구(에셋 제작).

## Global Constraints

- 스펙 문서: `docs/superpowers/specs/2026-07-30-visual-reboot-design.md` — 모든 태스크는 이 스펙을 따른다.
- 아트 톤: "진지한 그래픽노벨풍" — 귀여운 모바일게임풍이나 픽셀아트가 아니다. 굴곡 있는 라인, 절제된 색 팔레트, 조선시대 전투물에 어울리는 무게감.
- `GameConfig.cs`의 모든 상수(Radius, Hp, Speed 등)는 변경하지 않는다 — 게임 로직/밸런스는 이번 스펙 범위 밖.
- `SpriteFlipbook.cs`의 프레임 스왑 방식은 변경하지 않는다. `FrameRate` 필드도 기존 기본값(8)을 유지한다 — 이번 스펙 범위 밖.
- 화살(`arrow.png`)은 비행 방향으로 회전하는 기존 로직(`Projectile.cs`, 코드 변경 없음)을 그대로 쓰므로 좌우/상하 대칭에 가까운 디자인으로 제작한다.
- 캐릭터 전용 히트/사망 프레임은 만들지 않는다 (기존 파티클 이펙트로 표현). 파티클 프리팹(`HitEffect`, `DeathEffect`, `LevelUpEffect`, `BossSpawnEffect`) 자체는 이번 스펙에서 건드리지 않는다.
- 적 basic/fast 구분은 기존 색상/실루엣 구분 방식을 유지한다.
- 모든 스프라이트시트는 프레임을 가로로 나란히, 간격 없이 배치한다 (`ProjectBootstrap.ImportSheet`가 `new Rect(i * frameSize, 0, frameSize, frameSize)`로 자르기 때문).
- 프레임 크기: 모든 스프라이트(플레이어/적 2종/보스/화살/메달)를 **128×128px**로 통일한다. 시각적 크기는 `PixelsPerUnitFor(frameSize, radius)` 공식이 `GameConfig`의 radius에 맞춰 자동 계산하므로, frameSize를 캐릭터마다 다르게 가져갈 이유가 없다.
- 걷기 애니메이션 프레임 수: 스펙의 "6~8" 범위 중 **8프레임**으로 확정한다 (가장 매끄러운 쪽).
- 배경 이미지(`background.png`)는 **1600×900px**(16:9, `GameConfig.ArenaWidth`:`ArenaHeight`와 동일 비율)로 제작한다 — 씬의 Background Quad가 정확히 이 비율로 늘어나므로, 다른 비율로 만들면 이미지가 찌그러져 보인다.
- 모든 새 PNG는 투명 배경(알파 채널 포함)이어야 한다 (배경 이미지 자체는 예외 — 불투명 전체 그림).
- 스프라이트 임포트 필터모드는 `Point` → `Bilinear`로 전환한다 (벡터 아트에는 안티에일리어싱 필요).

---

## Task 1: 캐릭터/오브젝트 스프라이트시트 7종 생성

**Files:**
- Create/Replace: `Assets/Sprites/player.png` (1024×128, 8프레임 × 128px)
- Create/Replace: `Assets/Sprites/enemy-basic.png` (1024×128, 8프레임 × 128px)
- Create/Replace: `Assets/Sprites/enemy-fast.png` (1024×128, 8프레임 × 128px)
- Create/Replace: `Assets/Sprites/boss.png` (1024×128, 8프레임 × 128px)
- Create/Replace: `Assets/Sprites/arrow.png` (128×128, 1프레임)
- Create/Replace: `Assets/Sprites/medal.png` (128×128, 1프레임)
- Create: `Assets/Sprites/background.png` (1600×900, 1프레임, 불투명)

**Interfaces:**
- Produces: 위 7개 PNG 파일. Task 2가 이 파일들의 정확한 픽셀 크기를 전제로 `ProjectBootstrap.cs`를 수정하므로, 여기서 만든 실제 크기와 Task 2에서 코드에 넣을 크기가 반드시 일치해야 한다.

기존 참고 이미지(교체 대상 톤 확인용): `Assets/Sprites/player.png`, `enemy-basic.png`, `boss.png` — 현재는 저해상도 4프레임 픽셀아트.

- [ ] **Step 1: 플레이어 걷기 스프라이트시트 생성**

이미지 생성 도구(예: `mcp__plugin_huggingface-skills_huggingface-skills__gr1_z_image_turbo_generate`)로 아래 프롬프트를 사용해 생성한다. 도구가 정확히 1024×128을 지원하지 않으면 지원 가능한 가장 가까운 와이드 비율로 생성한 뒤 128×128 8칸 그리드로 리샘플/크롭한다.

프롬프트:
```
Serious graphic-novel style 2D game sprite sheet, 8-frame walking cycle,
side view, Joseon-era Korean soldier/commander character (dark blue durumagi
robe, black gat hat, sword at hip), flat-shaded vector illustration, bold
clean linework, muted desaturated color palette, dramatic lighting, no
background (transparent), 8 frames arranged left to right in a single
horizontal row, each frame square and same character proportions/scale/
palette across all frames, consistent silhouette
```

- [ ] **Step 2: 생성 결과를 검증하고 저장**

`Assets/Sprites/player.png`에 저장 후 실제 치수와 프레임별 일관성을 확인한다:

```bash
identify "Assets/Sprites/player.png"
```

기대값: `1024x128`, `8-bit/color RGBA` (알파 채널 포함). 8프레임을 육안으로 비교해 캐릭터 비율/색상/디자인이 프레임마다 크게 다르지 않은지 확인한다. 일관성이 깨졌으면 프롬프트에 "identical character design across all 8 frames, only pose changes"를 강조해 재생성한다 (스펙의 재시도 정책).

- [ ] **Step 3: 적(basic) 스프라이트시트 생성**

프롬프트:
```
Serious graphic-novel style 2D game sprite sheet, 8-frame walking cycle,
side view, invading soldier enemy character (red/crimson armor, spear),
flat-shaded vector illustration, bold clean linework, muted desaturated
color palette, dramatic lighting, no background (transparent), 8 frames
arranged left to right in a single horizontal row, each frame square and
same character proportions/scale/palette across all frames, consistent
silhouette
```

`Assets/Sprites/enemy-basic.png`에 저장, Step 2와 동일한 방식으로 `identify`로 1024x128 + RGBA 확인.

- [ ] **Step 4: 적(fast) 스프라이트시트 생성**

프롬프트 (basic과 구분되도록 실루엣/색상을 다르게 — 기존처럼 더 가볍고 날렵한 형태):
```
Serious graphic-novel style 2D game sprite sheet, 8-frame running cycle,
side view, lightly armored fast-moving enemy skirmisher character (leaner
silhouette than a heavy soldier, dark orange/brown leather armor, dagger),
flat-shaded vector illustration, bold clean linework, muted desaturated
color palette, dramatic lighting, no background (transparent), 8 frames
arranged left to right in a single horizontal row, each frame square and
same character proportions/scale/palette across all frames, consistent
silhouette
```

`Assets/Sprites/enemy-fast.png`에 저장, 동일 검증. 육안으로 basic과 나란히 놓았을 때 색상/실루엣이 한눈에 구별되는지 확인 (Global Constraints의 "적 구분 유지" 요건).

- [ ] **Step 5: 보스 스프라이트시트 생성**

프롬프트:
```
Serious graphic-novel style 2D game sprite sheet, 8-frame walking cycle,
side view, imposing armored general boss character (ornate dark red/black
lamellar armor, long spear, larger and more detailed than a common soldier),
flat-shaded vector illustration, bold clean linework, muted desaturated
color palette, dramatic lighting, no background (transparent), 8 frames
arranged left to right in a single horizontal row, each frame square and
same character proportions/scale/palette across all frames, consistent
silhouette
```

`Assets/Sprites/boss.png`에 저장, 동일 검증.

- [ ] **Step 6: 화살 투사체 생성**

프롬프트 (회전 시 부자연스럽지 않도록 좌우/상하 대칭 강조):
```
Serious graphic-novel style 2D game icon, single Korean traditional arrow
(hwasal) projectile, flat-shaded vector illustration, bold clean linework,
muted desaturated color palette, pointing right, symmetric along its long
axis so it looks natural when rotated to any angle, no background
(transparent), single centered sprite
```

`Assets/Sprites/arrow.png`에 저장. `identify`로 128x128 + RGBA 확인.

- [ ] **Step 7: 메달(경험치 픽업) 생성**

프롬프트:
```
Serious graphic-novel style 2D game icon, small round Korean medal/token
(gold-bronze medallion), flat-shaded vector illustration, bold clean
linework, muted desaturated color palette, no background (transparent),
single centered sprite, radially symmetric silhouette
```

`Assets/Sprites/medal.png`에 저장. `identify`로 128x128 + RGBA 확인.

- [ ] **Step 8: 배경 이미지 생성**

프롬프트:
```
Serious graphic-novel style 2D game background illustration, Joseon-era
Korean village and battlefield at night, wide establishing shot, dark
moonlit atmosphere, muted desaturated color palette, bold clean linework,
flat-shaded, no characters, wide 16:9 landscape composition, opaque full
illustration (no transparency needed)
```

`Assets/Sprites/background.png`에 저장. `identify`로 1600x900 확인 (알파 채널은 없어도 됨, 있어도 무방).

- [ ] **Step 9: 최종 파일 점검**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity\Assets\Sprites"
for f in player.png enemy-basic.png enemy-fast.png boss.png arrow.png medal.png background.png; do identify "$f"; done
```

기대값: player/enemy-basic/enemy-fast/boss = `1024x128`, arrow/medal = `128x128`, background = `1600x900`. 하나라도 어긋나면 해당 Step으로 돌아가 재생성.

- [ ] **Step 10: Commit**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
git add Assets/Sprites/player.png Assets/Sprites/enemy-basic.png Assets/Sprites/enemy-fast.png Assets/Sprites/boss.png Assets/Sprites/arrow.png Assets/Sprites/medal.png Assets/Sprites/background.png
git commit -m "Replace placeholder sprites with graphic-novel style vector art

New 8-frame (128x128/frame) sheets for player/enemy-basic/enemy-fast/boss,
new arrow/medal icons, and a new 1600x900 Joseon-era village/battlefield
background image. Not yet wired into the import pipeline (Task 2).

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
git push origin main
```

---

## Task 2: `ProjectBootstrap.cs` — 새 아트 규격에 맞춰 임포트/빌드 로직 수정

**Files:**
- Modify: `Assets/Editor/ProjectBootstrap.cs:29-79` (`ImportSpriteSheets`, `ImportSheet`, `ImportSingle`)
- Modify: `Assets/Editor/ProjectBootstrap.cs:196-221` (`BuildEnemyPrefab`, `BuildPlayerPrefab`)
- Modify: `Assets/Editor/ProjectBootstrap.cs:268-320` (`BuildMainScene` 배경 부분)

**Interfaces:**
- Consumes: Task 1에서 만든 `Assets/Sprites/*.png` 7개 파일 (정확한 치수: 1024×128 4개, 128×128 2개, 1600×900 1개)
- Produces: 재생성된 `Assets/Prefabs/*.prefab`, `Assets/Scenes/Main.unity` — Task 3이 이걸 빌드/테스트한다

- [ ] **Step 1: `ImportSheet`/`ImportSingle`의 필터모드를 Bilinear로 변경**

`Assets/Editor/ProjectBootstrap.cs`의 `ImportSheet` 메서드(52번째 줄 부근)에서:

```csharp
importer.filterMode = FilterMode.Point;
```

를 다음으로 변경 (`ImportSheet`, `ImportSingle` 두 곳 모두):

```csharp
importer.filterMode = FilterMode.Bilinear;
```

- [ ] **Step 2: `ImportSpriteSheets()`의 frameSize/frameCount를 새 규격으로 변경**

기존:
```csharp
public static void ImportSpriteSheets()
{
    ImportSheet("Assets/Sprites/player.png", 96, 4, PixelsPerUnitFor(96, GameConfig.Player.Radius));
    ImportSheet("Assets/Sprites/enemy-basic.png", 96, 4, PixelsPerUnitFor(96, GameConfig.EnemyBasic.Radius));
    ImportSheet("Assets/Sprites/enemy-fast.png", 96, 4, PixelsPerUnitFor(96, GameConfig.EnemyFast.Radius));
    ImportSheet("Assets/Sprites/boss.png", 128, 4, PixelsPerUnitFor(128, GameConfig.Boss.Radius));
    ImportSingle("Assets/Sprites/arrow.png", 96, PixelsPerUnitFor(96, GameConfig.Weapon.ProjectileRadius));
    ImportSingle("Assets/Sprites/medal.png", 96, PixelsPerUnitFor(96, MedalVisualDiameter / 2f));
    AssetDatabase.Refresh();
}
```

변경 후:
```csharp
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
```

- [ ] **Step 3: 배경 임포트용 `ImportBackground` 메서드 추가**

`ImportSingle` 메서드 바로 아래(79번째 줄 부근)에 새 메서드를 추가:

```csharp
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
```

(배경은 `PixelsPerUnitFor` 계산이 필요 없다 — Quad 메시가 `GameConfig.ArenaWidth/Height`로 이미 정확히 늘어나므로 텍스처의 픽셀당 유닛 값은 최종 렌더링 크기에 영향을 주지 않는다.)

- [ ] **Step 4: `BuildEnemyPrefab`/`BuildPlayerPrefab`의 프레임 개수를 8로 변경**

`BuildEnemyPrefab()` (204-206번째 줄 부근):
```csharp
controller.BasicSprites = LoadSpriteSheet("Assets/Sprites/enemy-basic.png", "enemy-basic", 4);
controller.FastSprites = LoadSpriteSheet("Assets/Sprites/enemy-fast.png", "enemy-fast", 4);
controller.BossSprites = LoadSpriteSheet("Assets/Sprites/boss.png", "boss", 4);
```
→
```csharp
controller.BasicSprites = LoadSpriteSheet("Assets/Sprites/enemy-basic.png", "enemy-basic", 8);
controller.FastSprites = LoadSpriteSheet("Assets/Sprites/enemy-fast.png", "enemy-fast", 8);
controller.BossSprites = LoadSpriteSheet("Assets/Sprites/boss.png", "boss", 8);
```

`BuildPlayerPrefab()` (216번째 줄 부근):
```csharp
flipbook.Sprites = LoadSpriteSheet("Assets/Sprites/player.png", "player", 4);
```
→
```csharp
flipbook.Sprites = LoadSpriteSheet("Assets/Sprites/player.png", "player", 8);
```

- [ ] **Step 5: `BuildMainScene()`의 배경을 단색 Quad에서 텍스처 Quad로 변경**

기존 (302-319번째 줄 부근):
```csharp
var background = GameObject.CreatePrimitive(PrimitiveType.Quad);
background.name = "Background";
Object.DestroyImmediate(background.GetComponent<Collider>());
background.transform.localScale = new Vector3(GameConfig.ArenaWidth, GameConfig.ArenaHeight, 1f);
background.transform.position = new Vector3(0f, 0f, 1f);
var backgroundMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.16f, 0.18f, 0.22f) };
background.GetComponent<MeshRenderer>().sharedMaterial = backgroundMaterial;
```

변경 후 (배경 이미지를 텍스처로 사용, 이미지가 없으면 기존 단색으로 폴백):
```csharp
var background = GameObject.CreatePrimitive(PrimitiveType.Quad);
background.name = "Background";
Object.DestroyImmediate(background.GetComponent<Collider>());
background.transform.localScale = new Vector3(GameConfig.ArenaWidth, GameConfig.ArenaHeight, 1f);
background.transform.position = new Vector3(0f, 0f, 1f);
var backgroundMaterial = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
var backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/background.png");
if (backgroundSprite != null)
    backgroundMaterial.mainTexture = backgroundSprite.texture;
else
    Debug.LogWarning("BuildMainScene: Assets/Sprites/background.png not found, background will render as flat white.");
background.GetComponent<MeshRenderer>().sharedMaterial = backgroundMaterial;
```

- [ ] **Step 6: 문법/컴파일 확인**

Unity 에디터가 열려 있다면 콘솔에 컴파일 에러가 없는지 확인한다. 닫혀 있다면 Task 3의 배치 빌드 단계에서 함께 확인된다.

- [ ] **Step 7: Commit**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
git add Assets/Editor/ProjectBootstrap.cs
git commit -m "Update sprite pipeline for 128px/8-frame vector art + background texture

ImportSheet/ImportSingle now use Bilinear filtering (was Point), frame
size is 128 and frame count is 8 for all four animated sheets. Added
ImportBackground() and wired background.png as the arena Background
quad's texture instead of a flat color.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
git push origin main
```

---

## Task 3: 프로젝트 재생성 및 회귀 검증

**Files:**
- (실행만, 파일 수정 없음) `run-playtest.bat`, `run-tests.bat`
- Generated/Overwritten: `Assets/Prefabs/*.prefab`, `Assets/Scenes/Main.unity` (Task 2의 `BuildProject`가 재생성)

**Interfaces:**
- Consumes: Task 1의 아트 파일 + Task 2로 수정된 `ProjectBootstrap.cs`
- Produces: 통과하는 테스트 결과 (`results.xml`, `playtest-results.xml`) — 이 태스크가 마지막 검증 게이트

- [ ] **Step 1: 씬/프리팹 재생성 + PlayMode 테스트 실행**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
./run-playtest.bat
```

이 스크립트가 내부적으로 `-executeMethod YiSunSin.EditorTools.ProjectBootstrap.BuildProject`를 먼저 돌려 씬/프리팹을 재생성한 뒤, PlayMode 테스트(`Assets/Tests/PlayMode/FullPlaytestTests.cs`)를 실행한다.

- [ ] **Step 2: 빌드 로그에서 임포트 경고/에러 확인**

```bash
grep -i "warning\|error\|missing sprite\|sprite sub-asset" build.log
```

기대값: `Missing sprite`나 `Sprite sub-asset '...' not found` 같은 경고가 없어야 한다 (있다면 Task 1의 파일명/경로 또는 Task 2의 frameCount가 실제 파일과 어긋난 것 — 해당 태스크로 돌아가 수정).

- [ ] **Step 3: PlayMode 테스트 결과 확인**

```bash
grep 'result="Failed"' playtest-results.xml
```

기대값: 아무 것도 출력되지 않아야 한다 (Failed 없음 = 전부 통과).

- [ ] **Step 4: EditMode 테스트 실행**

```bash
./run-tests.bat
grep 'result="Failed"' results.xml
```

기대값: 아무 것도 출력되지 않아야 한다 (기존 55개 EditMode 테스트는 게임 로직만 검증하므로 아트 교체와 무관하게 통과해야 정상).

- [ ] **Step 5: 육안 확인 (Unity 에디터, Play 모드)**

Unity 에디터에서 `Assets/Scenes/Main.unity`를 열고 ▶ Play를 눌러 다음을 확인한다:
- 플레이어/적/보스가 새 그래픽노벨풍 아트로 보이고, 걷는 동안 애니메이션이 자연스럽게 움직이는지 (더 이상 정적으로 안 보이는지)
- 배경이 조선시대 마을/전장터 이미지로 채워져 있고, 늘어나거나 찌그러진 부분이 없는지
- 화살이 날아갈 때 회전해도 부자연스러워 보이지 않는지
- 캐릭터/오브젝트가 화면 밖으로 비정상적으로 크거나 작지 않은지 (콜라이더/스케일이 맞는지)
- 히트/사망 시 기존 파티클 이펙트가 새 아트와 색感이 크게 어긋나지 않는지 (약간의 어긋남은 허용 범위 — Global Constraints 참고)

문제가 발견되면 해당 문제의 원인이 되는 태스크(아트라면 Task 1, 코드라면 Task 2)로 돌아가 수정 후 이 태스크를 다시 실행한다.

- [ ] **Step 6: 결과 리포트 파일 커밋**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
git add Assets/Prefabs Assets/Scenes/Main.unity results.xml playtest-results.xml build.log
git commit -m "Regenerate prefabs/scene for new sprite pipeline; all tests passing

Ran ProjectBootstrap.BuildProject to rebuild Assets/Prefabs and
Assets/Scenes/Main.unity against the new 128px/8-frame art and textured
background. 55 EditMode + 1 PlayMode tests passing.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
git push origin main
```

---

## Self-Review Notes (완료됨, 참고용)

- **스펙 커버리지:** 범위(스프라이트 7종 교체, 배경 추가, 8프레임 애니메이션, 필터모드/PPU 조정, 콜라이더 자동 스케일) → Task 1~3에서 전부 다룸. 제외 항목(UI, 파츠 애니메이션, parallax, 파티클 재작업, 적 구분 변경)은 어떤 태스크에서도 건드리지 않도록 Global Constraints에 명시함.
- **플레이스홀더 스캔:** 각 스텝에 실제 프롬프트/코드/명령어를 채워 넣었음 — "적절히 처리" 같은 모호한 문구 없음.
- **타입/네이밍 일관성:** `LoadSpriteSheet(path, name, frameCount)`, `ImportSheet(path, frameSize, frameCount, pixelsPerUnit)`, `PixelsPerUnitFor(frameSize, radius)` — 기존 시그니처를 그대로 유지하고 인자값만 바꿨으므로 태스크 간 불일치 없음.
