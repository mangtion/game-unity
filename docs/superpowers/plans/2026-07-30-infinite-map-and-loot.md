# 무한 맵 + 랜덤 아이템 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 카메라가 플레이어를 따라다니고 배경이 끊김 없이 반복되는 "무한 맵"으로 전환하고, 체력 회복/임시 버프 아이템이 적 처치 및 맵 탐색 중 랜덤하게 드롭되도록 만든다.

**Architecture:** 카메라(`CameraFollow`)와 배경(`InfiniteBackground`)은 새 컴포넌트로 교체/추가하고, `GameManager`의 고정 `bounds` 필드는 매 프레임 카메라 위치 기준으로 재계산하는 메서드로 바뀐다. 신규 아이템(`HealthPotion`/`BuffItem`)은 기존 `XpGem`과 동일한 "단순 데이터 컴포넌트 + GameManager 픽업 루프" 패턴을 따른다. 임시 버프는 `PlayerController`의 시간 기반 상태로 관리되고, `GameManager.Update()`가 매 프레임 영구 업그레이드 배율과 합성한다. 모든 신규 스포너(`AmbientItemSpawner`)는 기존 `SpawnController`와 동일한 `Func<float> Rng` 주입 패턴을 따라 EditMode에서 결정론적으로 테스트한다.

**Tech Stack:** Unity 6000.5.5f1, C# (Editor 스크립트), URP 2D, Unity Test Framework(EditMode/PlayMode), Python/PIL(아이템 아이콘 플레이스홀더).

## Global Constraints

- 스펙 문서: `docs/superpowers/specs/2026-07-30-infinite-map-and-loot-design.md` — 모든 태스크는 이 스펙을 따른다 (`/spec-review` 인터뷰로 확정된 세부사항 포함).
- 승리 조건(`GameConfig.WinTime`=300초 생존 시 Win, `GameConfig.BossSpawnTime`=240초 보스 스폰)은 값도 로직도 변경하지 않는다.
- `PlayerController.Tick`은 `Tick(float dt)`로 시그니처가 바뀐다 (기존 `Tick(float dt, Rect bounds)`에서 clamp 로직 제거). 이 시그니처를 참조하는 **모든** 호출부(`GameManager.cs`, `Assets/Tests/EditMode/PlayerControllerTests.cs` 4곳, `Assets/Tests/PlayMode/FullPlaytestTests.cs` 1곳)를 빠짐없이 갱신한다.
- `CameraFit.cs`는 완전히 삭제되고 `CameraFollow.cs`로 대체된다 — `CameraFit`을 참조하는 코드(`ProjectBootstrap.cs`, `Assets/Tests/PlayMode/FullPlaytestTests.cs`의 letterboxing 검증부)도 함께 정리한다.
- 새 아이템(`HealthPotion`/`BuffItem`)의 스프라이트는 PIL로 직접 그린 128×128 플레이스홀더다 (AI 아트 아님) — CHANGELOG의 Known limitations에 `arrow.png`/`medal.png`와 동일하게 기록한다.
- 모든 신규 확률/타이밍 로직(`AmbientItemSpawner`, `GameManager`의 보너스 드롭)은 `SpawnController.Rng`와 동일한 `public Func<float> Rng = () => UnityEngine.Random.value;` 주입 패턴을 쓴다 — EditMode 테스트에서 결정론적 시드로 검증하기 위함.
- `Assets/Editor/ProjectBootstrap.cs`가 스프라이트 임포트/프리팹/씬을 전부 코드로 생성하는 기존 빌드 스크립트 패턴을 그대로 따른다 — 씬/프리팹은 직접 수정하지 않고 `ProjectBootstrap.cs`만 고친 뒤 `-executeMethod ProjectBootstrap.BuildProject`로 재생성한다.

---

## Task 1: `PlayerController.Tick` 시그니처 변경 (clamp 제거) + 기존 호출부 전부 수정

**Files:**
- Modify: `Assets/Scripts/PlayerController.cs` (`Tick` 메서드, 74-99번째 줄 부근)
- Modify: `Assets/Scripts/GameManager.cs` (`Update()`의 `Player.Tick(dt, bounds)` 호출부, 135번째 줄 부근)
- Modify: `Assets/Tests/EditMode/PlayerControllerTests.cs` (bounds 인자를 쓰는 4개 테스트, `Tick_ClampsToBounds` 삭제)
- Modify: `Assets/Tests/PlayMode/FullPlaytestTests.cs` (112-115번째 줄 부근 — `arenaBounds` 로컬 변수와 그 사용처. 이 파일은 스펙에 언급되지 않았지만 검토 중 발견된 5번째 호출부)

**Interfaces:**
- Produces: `PlayerController.Tick(float dt)` — 이후 모든 태스크가 이 시그니처를 전제한다.

- [ ] **Step 1: `PlayerControllerTests.cs`에서 실패하는 테스트부터 수정 (TDD: 시그니처 변경을 테스트로 먼저 못박기)**

`Assets/Tests/EditMode/PlayerControllerTests.cs`에서 `bounds`를 쓰는 4개 테스트를 다음으로 교체(`Tick(1f, bounds)` → `Tick(1f)`, `bounds` 선언 줄 삭제)하고 `Tick_ClampsToBounds`는 통째로 삭제한다:

```csharp
    [Test]
    public void Tick_MovesAtMoveSpeed_AlongSingleAxis()
    {
        go.transform.position = new Vector3(100f, 100f, 0f);
        player.SetInput(Vector2.right);
        player.Tick(1f);
        Assert.AreEqual(300f, go.transform.position.x, 1e-3f); // 100 + 200*1
        Assert.AreEqual(100f, go.transform.position.y, 1e-3f);
    }

    [Test]
    public void Tick_NormalizesDiagonalMovement()
    {
        go.transform.position = new Vector3(0f, 0f, 0f);
        player.SetInput(new Vector2(1f, 1f));
        player.Tick(1f);
        float distance = ((Vector2)go.transform.position).magnitude;
        Assert.AreEqual(200f, distance, 1e-2f);
    }
```

`Tick_ClampsToBounds` 테스트 전체(더 이상 성립하지 않는 동작)를 삭제한다.

`IFrames_ExpireAfterTick`도 수정:

```csharp
    [Test]
    public void IFrames_ExpireAfterTick()
    {
        player.TakeDamage(10f);
        player.Tick(0.5f); // exactly InvincibilityDuration
        Assert.IsTrue(player.TakeDamage(10f));
        Assert.AreEqual(80f, player.Hp);
    }
```

- [ ] **Step 2: 테스트 실행 — 컴파일 에러로 실패하는지 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat
```

기대값: `PlayerController`에 아직 `Tick(float dt)` 오버로드가 없으므로 컴파일 에러(`No overload for method 'Tick' takes 1 arguments`) 발생.

- [ ] **Step 3: `PlayerController.Tick` 시그니처 변경**

`Assets/Scripts/PlayerController.cs`의 기존:

```csharp
        public void Tick(float dt, Rect bounds)
        {
            EnsureInitialized();
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
```

를 다음으로 교체:

```csharp
        public void Tick(float dt)
        {
            EnsureInitialized();
            if (flipbook != null) flipbook.IsMoving = inputDirection.sqrMagnitude > 0f;
            if (inputDirection.sqrMagnitude > 0f)
            {
                Vector2 delta = inputDirection.normalized * moveSpeed * dt;
                // NOTE: Rigidbody2D.MovePosition does not apply synchronously
                // in EditMode tests (no active physics step / Play Mode loop),
                // so it leaves transform.position unchanged until a physics
                // step runs. Setting transform.position directly keeps Tick()
                // deterministic and correct in both EditMode tests and Play
                // Mode, at the cost of not going through the physics
                // interpolation MovePosition would otherwise provide.
                transform.position = (Vector2)transform.position + delta;
            }

            if (invincibleTimer > 0f)
                invincibleTimer = Mathf.Max(0f, invincibleTimer - dt);
        }
```

(고정 아레나가 없어졌으므로 clamp 로직 전체 삭제 — 무한 맵이므로 이동 범위 제한 없음)

- [ ] **Step 4: `GameManager.cs` 호출부 수정**

`Assets/Scripts/GameManager.cs`의 `Update()`에서:

```csharp
            Player.Tick(dt, bounds);
```

를:

```csharp
            Player.Tick(dt);
```

로 변경 (`bounds` 필드 자체는 Task 4에서 제거하므로 지금은 그대로 둔다 — 아래 `Spawner.Tick(dt, Elapsed, bounds, transform)` 호출에서 여전히 쓰인다).

- [ ] **Step 5: `FullPlaytestTests.cs`의 5번째 호출부 수정**

`Assets/Tests/PlayMode/FullPlaytestTests.cs`의 "Step 3: movement" 블록에서:

```csharp
            var arenaBounds = new Rect(-GameConfig.ArenaWidth / 2f, -GameConfig.ArenaHeight / 2f, GameConfig.ArenaWidth, GameConfig.ArenaHeight);
            Vector3 posBefore = gm.Player.transform.position;
            gm.Player.SetInput(new Vector2(1f, 1f));
            gm.Player.Tick(1f, arenaBounds);
```

를:

```csharp
            Vector3 posBefore = gm.Player.transform.position;
            gm.Player.SetInput(new Vector2(1f, 1f));
            gm.Player.Tick(1f);
```

로 변경 (`arenaBounds`는 이 한 곳에서만 쓰이므로 선언째 삭제).

- [ ] **Step 6: EditMode 테스트 재실행 — 통과 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat
grep 'result="Failed"' results.xml
```

기대값: 아무 출력 없음 (전부 통과, 테스트 개수는 `Tick_ClampsToBounds` 삭제로 1개 줄어든 54개).

- [ ] **Step 7: Commit**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
git add Assets/Scripts/PlayerController.cs Assets/Scripts/GameManager.cs Assets/Tests/EditMode/PlayerControllerTests.cs Assets/Tests/PlayMode/FullPlaytestTests.cs results.xml
git commit -m "Remove PlayerController arena clamp (Tick(dt) signature)

PlayerController.Tick no longer takes a bounds Rect or clamps movement -
the upcoming infinite map has no fixed arena to clamp to. Updates all 5
call sites (GameManager, 4 PlayerControllerTests, 1 FullPlaytestTests)
and deletes the now-meaningless Tick_ClampsToBounds test.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 2: `CameraFollow.cs` (신규) — `CameraFit.cs` 전면 교체

**Files:**
- Create: `Assets/Scripts/CameraFollow.cs`
- Delete: `Assets/Scripts/CameraFit.cs`, `Assets/Scripts/CameraFit.cs.meta`
- Create: `Assets/Tests/EditMode/CameraFollowTests.cs`
- Modify: `Assets/Editor/ProjectBootstrap.cs` (`BuildMainScene()`의 Main Camera 생성부, 311-316번째 줄 부근)
- Modify: `Assets/Tests/PlayMode/FullPlaytestTests.cs` (`CameraFit` 참조 4곳: `mainCamera.GetComponent<CameraFit>()`, `VerifyLetterbox` 메서드 전체, "Step 8" 블록 전체, `CaptureFull`의 `camFit.Fit()` 2곳)

**Interfaces:**
- Produces: `CameraFollow` 컴포넌트, `public Transform Target` 필드. `Target`이 설정되면 `LateUpdate()`에서 카메라 위치가 즉시 그 Transform을 따라간다.

- [ ] **Step 1: 실패하는 EditMode 테스트 작성**

`Assets/Tests/EditMode/CameraFollowTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using YiSunSin;

public class CameraFollowTests
{
    GameObject cameraGo;
    CameraFollow follow;
    GameObject targetGo;

    [SetUp]
    public void SetUp()
    {
        cameraGo = new GameObject("Camera", typeof(Camera), typeof(CameraFollow));
        follow = cameraGo.GetComponent<CameraFollow>();
        targetGo = new GameObject("Target");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(cameraGo);
        Object.DestroyImmediate(targetGo);
    }

    [Test]
    public void Follow_MatchesTargetXY_PreservesOwnZ()
    {
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);
        targetGo.transform.position = new Vector3(123f, -45f, 0f);
        follow.Target = targetGo.transform;

        follow.Follow(); // exposed so EditMode tests can call it without a live LateUpdate loop

        Assert.AreEqual(123f, cameraGo.transform.position.x, 1e-3f);
        Assert.AreEqual(-45f, cameraGo.transform.position.y, 1e-3f);
        Assert.AreEqual(-10f, cameraGo.transform.position.z, 1e-3f); // z untouched
    }

    [Test]
    public void Follow_DoesNothing_WhenTargetIsNull()
    {
        cameraGo.transform.position = new Vector3(5f, 5f, -10f);
        follow.Target = null;

        follow.Follow();

        Assert.AreEqual(5f, cameraGo.transform.position.x, 1e-3f);
        Assert.AreEqual(5f, cameraGo.transform.position.y, 1e-3f);
    }
}
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat
```

기대값: `CameraFollow` 타입이 없어 컴파일 에러.

- [ ] **Step 3: `CameraFollow.cs` 구현**

`Assets/Scripts/CameraFollow.cs`:

```csharp
using UnityEngine;

namespace YiSunSin
{
    // Infinite-map camera: follows Target's X/Y instantly (no smoothing - twin-stick
    // survivor genre standard, avoids input-lag feel). Replaces CameraFit, which
    // used to grow orthographicSize on narrow aspects to guarantee the full fixed
    // arena stayed visible - there is no fixed arena anymore, so that logic is gone
    // and orthographicSize is just set once in ProjectBootstrap and left alone.
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public Transform Target;

        void LateUpdate() => Follow();

        // Exposed so EditMode tests can call it directly without a live LateUpdate loop.
        public void Follow()
        {
            if (Target == null) return;
            var pos = transform.position;
            transform.position = new Vector3(Target.position.x, Target.position.y, pos.z);
        }
    }
}
```

- [ ] **Step 4: 테스트 재실행 — 통과 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat
grep 'result="Failed"' results.xml
```

기대값: 아무 출력 없음.

- [ ] **Step 5: `CameraFit.cs` 삭제**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
rm Assets/Scripts/CameraFit.cs Assets/Scripts/CameraFit.cs.meta
```

- [ ] **Step 6: `ProjectBootstrap.cs`의 Main Camera 생성부 수정**

`Assets/Editor/ProjectBootstrap.cs`의 `BuildMainScene()`에서 기존:

```csharp
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(CameraFit));
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = GameConfig.ArenaHeight / 2f; // baseline for 16:9+; CameraFit grows this at runtime for narrower aspects so the full arena stays visible (never cropped)
            camera.transform.position = new Vector3(0f, 0f, -10f);
```

를:

```csharp
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(CameraFollow));
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = GameConfig.ArenaHeight / 2f; // fixed zoom level, same as the old CameraFit baseline - no more aspect-based resizing since there's no fixed arena to guarantee visible
            camera.transform.position = new Vector3(0f, 0f, -10f);
```

`playerInstance`가 생성된 직후(같은 메서드 내 `var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab); playerInstance.transform.position = Vector3.zero;` 바로 다음 줄)에 추가:

```csharp
            cameraGo.GetComponent<CameraFollow>().Target = playerInstance.transform;
```

- [ ] **Step 7: `FullPlaytestTests.cs`에서 `CameraFit` 참조 제거**

"Step 8: camera letterboxing across aspect ratios" 블록 전체(다음 코드)를 삭제한다:

```csharp
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
```

(무한 맵에는 "어떤 화면비에서도 고정 아레나 전체가 반드시 보여야 한다"는 보장이 더 이상 없으므로 이 검증 자체가 무의미해짐 — 스펙의 CameraFollow 설계 결정과 일치)

`VerifyLetterbox` 메서드 전체(`IEnumerator VerifyLetterbox(Camera cam, CameraFit fit, float aspect, string label) { ... }`)를 삭제한다.

`CaptureFull` 메서드에서 다음 두 줄:

```csharp
            var camFit = cam.GetComponent<CameraFit>();
            float prevCamAspect = cam.aspect;
            cam.aspect = 1600f / 900f;
            if (camFit != null) camFit.Fit();
```

를:

```csharp
            float prevCamAspect = cam.aspect;
            cam.aspect = 1600f / 900f; // consistent screenshot aspect; orthographicSize is now fixed by CameraFollow, no Fit() needed
```

로 변경하고, 같은 메서드 뒤쪽의:

```csharp
            cam.aspect = prevCamAspect;
            if (camFit != null) camFit.Fit();
```

를:

```csharp
            cam.aspect = prevCamAspect;
```

로 변경한다.

- [ ] **Step 8: 재빌드 + 전체 테스트 실행**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-playtest.bat
```

기대값: `playtest-results.xml`에 `result="Passed"`. `09_letterbox_*.png` 스크린샷 4개는 더 이상 생성되지 않는다 (의도된 변화).

- [ ] **Step 9: Commit**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
git add Assets/Scripts/CameraFollow.cs Assets/Tests/EditMode/CameraFollowTests.cs Assets/Editor/ProjectBootstrap.cs Assets/Tests/PlayMode/FullPlaytestTests.cs Assets/Scenes/Main.unity build.log playtest-results.xml playtest.log
git rm Assets/Scripts/CameraFit.cs Assets/Scripts/CameraFit.cs.meta
git commit -m "Replace CameraFit with CameraFollow (infinite-map camera)

Camera now follows the player instantly instead of resizing to keep a
fixed arena fully visible - there is no fixed arena anymore. Removes
the now-meaningless letterboxing PlayMode checks (CameraFit.Fit() /
VerifyLetterbox) along with the component itself.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 3: `InfiniteBackground.cs` (신규) — 배경 타일 재배치

**Files:**
- Create: `Assets/Scripts/BackgroundTiling.cs` (순수 함수, 좌표 계산만)
- Create: `Assets/Scripts/InfiniteBackground.cs` (MonoBehaviour, `BackgroundTiling`을 호출)
- Create: `Assets/Tests/EditMode/BackgroundTilingTests.cs`
- Modify: `Assets/Editor/ProjectBootstrap.cs` (`ImportBackground()`, `BuildMainScene()`의 Background Quad 생성부)

**Interfaces:**
- Produces: `BackgroundTiling.ShouldRecenter(...)`, `BackgroundTiling.OffsetAfterRecenter(...)` — 순수 함수라 EditMode에서 직접 테스트. `InfiniteBackground` 컴포넌트는 `public Transform CameraTransform` 필드로 카메라를 주입받는다.

- [ ] **Step 1: `BackgroundTiling`에 대한 실패하는 테스트 작성**

`Assets/Tests/EditMode/BackgroundTilingTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using YiSunSin;

public class BackgroundTilingTests
{
    [Test]
    public void ShouldRecenter_FalseWhenWithinThreshold()
    {
        bool result = BackgroundTiling.ShouldRecenter(
            quadCenter: Vector2.zero, cameraPos: new Vector2(100f, 50f),
            thresholdX: 800f, thresholdY: 450f);
        Assert.IsFalse(result);
    }

    [Test]
    public void ShouldRecenter_TrueWhenXExceedsThreshold()
    {
        bool result = BackgroundTiling.ShouldRecenter(
            quadCenter: Vector2.zero, cameraPos: new Vector2(801f, 0f),
            thresholdX: 800f, thresholdY: 450f);
        Assert.IsTrue(result);
    }

    [Test]
    public void ShouldRecenter_TrueWhenYExceedsThreshold()
    {
        bool result = BackgroundTiling.ShouldRecenter(
            quadCenter: Vector2.zero, cameraPos: new Vector2(0f, -451f),
            thresholdX: 800f, thresholdY: 450f);
        Assert.IsTrue(result);
    }

    [Test]
    public void OffsetAfterRecenter_ShiftsByFractionalTileDelta()
    {
        // camera moved 1600 (exactly one tile width) + 400 (a quarter tile) on X.
        // The integer-tile part (1600) needs no offset correction (periodic pattern
        // repeats identically); only the 400/1600 = 0.25 fractional part matters.
        Vector2 result = BackgroundTiling.OffsetAfterRecenter(
            currentOffset: Vector2.zero,
            quadCenter: Vector2.zero, cameraPos: new Vector2(2000f, 0f),
            tileWidth: 1600f, tileHeight: 900f);

        Assert.AreEqual(0.75f, result.x, 1e-4f); // Mathf.Repeat(0 - 2000/1600, 1) = Mathf.Repeat(-1.25, 1) = 0.75
        Assert.AreEqual(0f, result.y, 1e-4f);
    }

    [Test]
    public void OffsetAfterRecenter_WrapsIntoZeroToOneRange()
    {
        Vector2 result = BackgroundTiling.OffsetAfterRecenter(
            currentOffset: new Vector2(0.9f, 0f),
            quadCenter: Vector2.zero, cameraPos: new Vector2(0f, 0f), // no movement at all
            tileWidth: 1600f, tileHeight: 900f);

        Assert.AreEqual(0.9f, result.x, 1e-4f); // unchanged, and still in [0,1)
        Assert.GreaterOrEqual(result.x, 0f);
        Assert.Less(result.x, 1f);
    }
}
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat
```

기대값: `BackgroundTiling` 타입이 없어 컴파일 에러.

- [ ] **Step 3: `BackgroundTiling.cs` 구현**

`Assets/Scripts/BackgroundTiling.cs`:

```csharp
using UnityEngine;

namespace YiSunSin
{
    // Pure math for InfiniteBackground's single-quad tiling approach: instead of a
    // huge quad (risking float-precision issues over a long session) or a 3x3 tile
    // grid (more moving parts), one modest-sized quad (2x2 tiles) gets snapped to
    // stay centered near the camera whenever it drifts too far, with the texture
    // offset corrected by the fractional (non-whole-tile) part of the move so the
    // repeating pattern doesn't visibly jump at the recenter moment.
    public static class BackgroundTiling
    {
        public static bool ShouldRecenter(Vector2 quadCenter, Vector2 cameraPos, float thresholdX, float thresholdY) =>
            Mathf.Abs(cameraPos.x - quadCenter.x) > thresholdX ||
            Mathf.Abs(cameraPos.y - quadCenter.y) > thresholdY;

        public static Vector2 OffsetAfterRecenter(Vector2 currentOffset, Vector2 quadCenter, Vector2 cameraPos, float tileWidth, float tileHeight)
        {
            Vector2 delta = cameraPos - quadCenter; // how far the quad is about to move
            float x = Mathf.Repeat(currentOffset.x - delta.x / tileWidth, 1f);
            float y = Mathf.Repeat(currentOffset.y - delta.y / tileHeight, 1f);
            return new Vector2(x, y);
        }
    }
}
```

- [ ] **Step 4: 테스트 재실행 — 통과 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat
grep 'result="Failed"' results.xml
```

기대값: 아무 출력 없음.

- [ ] **Step 5: `InfiniteBackground.cs` 구현 (MonoBehaviour 글루)**

`Assets/Scripts/InfiniteBackground.cs`:

```csharp
using UnityEngine;

namespace YiSunSin
{
    [RequireComponent(typeof(MeshRenderer))]
    public class InfiniteBackground : MonoBehaviour
    {
        public Transform CameraTransform;

        MeshRenderer meshRenderer;

        void Awake() => meshRenderer = GetComponent<MeshRenderer>();

        void LateUpdate()
        {
            if (CameraTransform == null) return;
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

            Vector2 quadCenter = transform.position;
            Vector2 camPos = CameraTransform.position;
            // Recenter threshold = half of ONE background tile (GameConfig.ArenaWidth/Height,
            // the original 1600x900 image), not half the whole 2x2 quad - see BackgroundTiling.
            if (!BackgroundTiling.ShouldRecenter(quadCenter, camPos, GameConfig.ArenaWidth / 2f, GameConfig.ArenaHeight / 2f))
                return;

            var mat = meshRenderer.material;
            mat.mainTextureOffset = BackgroundTiling.OffsetAfterRecenter(
                mat.mainTextureOffset, quadCenter, camPos, GameConfig.ArenaWidth, GameConfig.ArenaHeight);

            var pos = transform.position;
            transform.position = new Vector3(camPos.x, camPos.y, pos.z);
        }
    }
}
```

- [ ] **Step 6: `ProjectBootstrap.cs`의 배경 임포트 + Quad 생성부 수정**

`Assets/Editor/ProjectBootstrap.cs`의 `ImportBackground()`에서 기존:

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

를:

```csharp
        static void ImportBackground(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) { Debug.LogWarning($"Missing sprite: {path}"); return; }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Repeat; // needed for the 2x2 tiling in BuildMainScene's background Quad
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
```

`BuildMainScene()`의 Background Quad 생성부에서 기존:

```csharp
            var background = GameObject.CreatePrimitive(PrimitiveType.Quad);
            background.name = "Background";
            Object.DestroyImmediate(background.GetComponent<Collider>());
            background.transform.localScale = new Vector3(GameConfig.ArenaWidth, GameConfig.ArenaHeight, 1f);
            background.transform.position = new Vector3(0f, 0f, 1f);
```

를:

```csharp
            var background = GameObject.CreatePrimitive(PrimitiveType.Quad);
            background.name = "Background";
            Object.DestroyImmediate(background.GetComponent<Collider>());
            // 2x2 tiles of the 1600x900 background image, recentered on the camera by
            // InfiniteBackground whenever it drifts - see BackgroundTiling for the math.
            float bgQuadWidth = GameConfig.ArenaWidth * 2f;
            float bgQuadHeight = GameConfig.ArenaHeight * 2f;
            background.transform.localScale = new Vector3(bgQuadWidth, bgQuadHeight, 1f);
            background.transform.position = new Vector3(0f, 0f, 1f);
```

그리고 조금 아래, 기존:

```csharp
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
```

를:

```csharp
            var backgroundMaterial = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
            var backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/background.png");
            if (backgroundSprite != null)
            {
                backgroundMaterial.mainTexture = backgroundSprite.texture;
                backgroundMaterial.mainTextureScale = new Vector2(2f, 2f); // tile 2x2 across the enlarged quad
            }
            else
            {
                backgroundMaterial.color = new Color(0.16f, 0.18f, 0.22f); // moonlit-deck tone fallback
                Debug.LogWarning("BuildMainScene: Assets/Sprites/background.png not found, background will render as flat color.");
            }
            background.GetComponent<MeshRenderer>().sharedMaterial = backgroundMaterial;
            background.AddComponent<InfiniteBackground>().CameraTransform = camera.transform;
```

(`backgroundMaterial`은 `new Material(...)`로 새로 만든 인스턴스라서 `sharedMaterial`을 통해 `meshRenderer.material`로 접근해도 다른 오브젝트와 공유되지 않는다 — `InfiniteBackground`가 런타임에 `mainTextureOffset`을 수정해도 안전함.)

- [ ] **Step 7: 재빌드 + 회귀 테스트**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-playtest.bat
grep 'result="Failed"' playtest-results.xml
```

기대값: 아무 출력 없음. 스크린샷(`02_gameplay_enemy_onscreen.png` 등)을 열어 배경이 이전과 비슷하게 보이는지(2x2 반복이라 달이 두 번 보일 수 있음 — 스펙에서 허용한 트레이드오프) 육안 확인.

- [ ] **Step 8: Commit**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
git add Assets/Scripts/BackgroundTiling.cs Assets/Scripts/InfiniteBackground.cs Assets/Tests/EditMode/BackgroundTilingTests.cs Assets/Editor/ProjectBootstrap.cs Assets/Scenes/Main.unity build.log playtest-results.xml playtest.log results.xml
git commit -m "Add InfiniteBackground: single-quad tiling that recenters on the camera

Background Quad is now 2x2 tiles (3200x1800) with texture repeat
tiling, and InfiniteBackground snaps it back near the camera whenever
it drifts past half a tile, correcting mainTextureOffset by the
fractional tile delta so the pattern doesn't visibly jump. Avoids both
a huge-quad float-precision risk and a multi-tile-grid's complexity.
BackgroundTiling.cs factors the recenter/offset math into pure,
directly EditMode-testable functions.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 4: `GameManager`의 스폰 경계를 카메라 기준으로 변경

**Files:**
- Modify: `Assets/Scripts/GameManager.cs` (`bounds` 필드 제거, `BoundsAroundCamera()` 추가, `CameraTransform` 필드 추가, `Update()`의 `Spawner.Tick` 호출부)
- Modify: `Assets/Editor/ProjectBootstrap.cs` (`BuildMainScene()`의 GameManager 필드 배선)
- Modify: `Assets/Tests/EditMode/GameManagerTests.cs` (`SetUp`에 카메라 Transform 추가 + `BoundsAroundCamera` 테스트)

**Interfaces:**
- Consumes: Task 2의 `CameraFollow`(카메라가 실제로 움직인다는 전제) — 다만 이 태스크 자체는 `CameraFollow`를 직접 참조하지 않고 `Transform` 하나만 필요로 한다.
- Produces: `GameManager.CameraTransform` (public field), `GameManager.BoundsAroundCamera()` (internal, EditMode 테스트용).

- [ ] **Step 1: `GameManagerTests.cs`에 실패하는 테스트 작성**

`Assets/Tests/EditMode/GameManagerTests.cs`의 `SetUp()`에 카메라 Transform을 추가한다. 기존:

```csharp
        var managerGo = new GameObject("GameManager", typeof(GameManager));
        manager = managerGo.GetComponent<GameManager>();
        manager.Player = player;
        manager.PlayerWeapon = weapon;
        manager.Spawner = spawner;
        manager.XpGemPrefab = xpGemPrefab;
        manager.Awake_ForTests(); // see Step 3: Awake must be callable directly for EditMode setup
```

를:

```csharp
        var cameraGo = new GameObject("Camera");
        cameraGo.transform.SetParent(root.transform);

        var managerGo = new GameObject("GameManager", typeof(GameManager));
        managerGo.transform.SetParent(root.transform);
        manager = managerGo.GetComponent<GameManager>();
        manager.Player = player;
        manager.PlayerWeapon = weapon;
        manager.Spawner = spawner;
        manager.XpGemPrefab = xpGemPrefab;
        manager.CameraTransform = cameraGo.transform;
        manager.Awake_ForTests(); // see Step 3: Awake must be callable directly for EditMode setup
```

파일 맨 아래(마지막 `[Test]` 뒤, 클래스 닫는 `}` 앞)에 새 테스트를 추가한다:

```csharp
    [Test]
    public void BoundsAroundCamera_IsCenteredOnCameraPosition_NotOrigin()
    {
        manager.CameraTransform.position = new Vector3(500f, -200f, -10f);
        Rect bounds = manager.BoundsAroundCamera();

        Assert.AreEqual(GameConfig.ArenaWidth, bounds.width, 1e-3f);
        Assert.AreEqual(GameConfig.ArenaHeight, bounds.height, 1e-3f);
        Assert.AreEqual(500f - GameConfig.ArenaWidth / 2f, bounds.xMin, 1e-3f);
        Assert.AreEqual(-200f - GameConfig.ArenaHeight / 2f, bounds.yMin, 1e-3f);
    }
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat
```

기대값: `GameManager.CameraTransform`/`BoundsAroundCamera`가 없어 컴파일 에러.

- [ ] **Step 3: `GameManager.cs` 수정**

기존 필드 선언부:

```csharp
        public GameObject XpGemPrefab;
        public GameObject BossSpotlight;
```

를:

```csharp
        public GameObject XpGemPrefab;
        public GameObject BossSpotlight;
        public Transform CameraTransform;
```

로 확장.

기존:

```csharp
        Rect bounds;

        void Awake() => Awake_ForTests();

        // Exposed so EditMode tests can drive setup without a running Play Mode Awake pass.
        internal void Awake_ForTests()
        {
            Instance = this;
            bounds = new Rect(-GameConfig.ArenaWidth / 2f, -GameConfig.ArenaHeight / 2f, GameConfig.ArenaWidth, GameConfig.ArenaHeight);
        }
```

를:

```csharp
        void Awake() => Awake_ForTests();

        // Exposed so EditMode tests can drive setup without a running Play Mode Awake pass.
        internal void Awake_ForTests()
        {
            Instance = this;
        }

        // Replaces the old fixed-arena `bounds` field: the infinite map has no fixed
        // arena, so spawn/despawn bounds now recenter on the camera every frame.
        // internal (not private) so EditMode tests can call it directly - see
        // [assembly: InternalsVisibleTo("Tests.EditMode")] at the top of this file.
        internal Rect BoundsAroundCamera() => new Rect(
            CameraTransform.position.x - GameConfig.ArenaWidth / 2f,
            CameraTransform.position.y - GameConfig.ArenaHeight / 2f,
            GameConfig.ArenaWidth, GameConfig.ArenaHeight);
```

`Update()`에서 기존:

```csharp
            var spawned = Spawner.Tick(dt, Elapsed, bounds, transform);
```

를:

```csharp
            var bounds = BoundsAroundCamera();
            var spawned = Spawner.Tick(dt, Elapsed, bounds, transform);
```

로 변경 (`bounds`는 이제 `Update()` 내부의 로컬 변수 — Task 8에서 `AmbientItemSpawner`에도 같은 변수를 재사용한다).

- [ ] **Step 4: `ProjectBootstrap.cs`에 `CameraTransform` 배선 추가**

`BuildMainScene()`의 GameManager 필드 배선부에서 기존:

```csharp
            manager.Player = playerInstance.GetComponent<PlayerController>();
            manager.PlayerWeapon = weaponGo.GetComponent<Weapon>();
            manager.Spawner = spawnerGo.GetComponent<SpawnController>();
            manager.XpGemPrefab = xpGemPrefab;
            manager.BossSpotlight = spotlightGo;
```

를:

```csharp
            manager.Player = playerInstance.GetComponent<PlayerController>();
            manager.PlayerWeapon = weaponGo.GetComponent<Weapon>();
            manager.Spawner = spawnerGo.GetComponent<SpawnController>();
            manager.XpGemPrefab = xpGemPrefab;
            manager.BossSpotlight = spotlightGo;
            manager.CameraTransform = cameraGo.transform;
```

- [ ] **Step 5: 테스트 재실행 — 통과 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat
grep 'result="Failed"' results.xml
```

기대값: 아무 출력 없음.

- [ ] **Step 6: 재빌드 + PlayMode 회귀**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-playtest.bat
grep 'result="Failed"' playtest-results.xml
```

기대값: 아무 출력 없음. (플레이어가 원점에서 시작하므로 이 시점까지는 스폰 위치가 이전과 동일하게 보여야 함 — 카메라가 아직 원점에 있을 때는 `BoundsAroundCamera()`가 옛 고정 `bounds`와 수학적으로 동일)

- [ ] **Step 7: Commit**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
git add Assets/Scripts/GameManager.cs Assets/Editor/ProjectBootstrap.cs Assets/Tests/EditMode/GameManagerTests.cs Assets/Scenes/Main.unity build.log playtest-results.xml playtest.log results.xml
git commit -m "Recompute enemy spawn bounds around the camera every frame

Replaces GameManager's fixed bounds field (set once at Awake, centered
on the origin) with BoundsAroundCamera(), recomputed every Update()
from CameraTransform.position - enemies now spawn relative to wherever
the player currently is, not a fixed arena. SpawnController itself is
unchanged (only the Rect it receives moves).

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 5: 임시 버프 시스템 (`PlayerController` + `UpgradeEffects` + `GameManager`)

**Files:**
- Modify: `Assets/Scripts/UpgradeEffects.cs` (`MinFireInterval` → `internal`, 새 `EffectiveFireIntervalWithBuff` 메서드)
- Modify: `Assets/Scripts/PlayerController.cs` (버프 필드, `Heal`, `ApplyBuff`, `Tick`의 타이머 감소, `ResetState`)
- Modify: `Assets/Scripts/GameManager.cs` (`Update()`의 `PlayerWeapon.FireInterval` 계산)
- Modify: `Assets/Tests/EditMode/PlayerControllerTests.cs` (버프 관련 테스트 추가)
- Modify: `Assets/Tests/EditMode/UpgradeEffectsTests.cs` (`EffectiveFireIntervalWithBuff` 테스트 추가)

**Interfaces:**
- Consumes: 없음 (독립적으로 동작 — `HealthPotion`/`BuffItem` 컴포넌트 자체는 Task 6에서 생성됨. 이 태스크는 "버프가 적용된 상태"를 직접 만들어 테스트하지, 아이템 픽업 경로는 아직 없음)
- Produces: `PlayerController.BuffTimeRemaining` (float, get), `PlayerController.BuffFireIntervalMultiplier` (float, get), `PlayerController.Heal(float amount)`, `PlayerController.ApplyBuff(float fireIntervalMultiplier, float duration)`, `UpgradeEffects.EffectiveFireIntervalWithBuff(Dictionary<string,int> stacks, float buffMultiplier)`.

- [ ] **Step 1: `UpgradeEffectsTests.cs`에 실패하는 테스트 작성**

`Assets/Tests/EditMode/UpgradeEffectsTests.cs`가 이미 있다면(기존 `EffectiveFireInterval` 등을 테스트하는 파일) 그 파일에, 없다면 새로 만들어 다음을 추가한다:

```csharp
    [Test]
    public void EffectiveFireIntervalWithBuff_MultipliesPermanentValue()
    {
        var stacks = new Dictionary<string, int>(); // no permanent upgrades: base 0.5s
        float result = UpgradeEffects.EffectiveFireIntervalWithBuff(stacks, 0.5f); // buff halves it
        Assert.AreEqual(0.25f, result, 1e-4f);
    }

    [Test]
    public void EffectiveFireIntervalWithBuff_NoBuff_MatchesPlainEffectiveFireInterval()
    {
        var stacks = new Dictionary<string, int> { { "fireRate", 3 } };
        float withoutBuff = UpgradeEffects.EffectiveFireInterval(stacks);
        float withNeutralBuff = UpgradeEffects.EffectiveFireIntervalWithBuff(stacks, 1f);
        Assert.AreEqual(withoutBuff, withNeutralBuff, 1e-4f);
    }

    [Test]
    public void EffectiveFireIntervalWithBuff_ReClampsToFloor_EvenAfterBuffMultiply()
    {
        // 20 fireRate stacks already sits at (or very near) the 0.05s floor on its own;
        // multiplying by the buff's 0.5x must not push it below that floor.
        var stacks = new Dictionary<string, int> { { "fireRate", 20 } };
        float result = UpgradeEffects.EffectiveFireIntervalWithBuff(stacks, 0.5f);
        Assert.AreEqual(0.05f, result, 1e-4f);
    }
```

파일 상단에 `using System.Collections.Generic;`이 없다면 추가한다.

- [ ] **Step 2: `PlayerControllerTests.cs`에 실패하는 테스트 작성**

파일 맨 아래, 마지막 테스트 뒤에 추가:

```csharp
    [Test]
    public void Heal_IncreasesHp_ButNotAboveMaxHp()
    {
        player.TakeDamage(60f); // Hp = 40
        player.Heal(25f);
        Assert.AreEqual(65f, player.Hp, 1e-3f);

        player.Heal(9999f);
        Assert.AreEqual(player.MaxHp, player.Hp, 1e-3f); // clamped, not overflowed
    }

    [Test]
    public void ApplyBuff_SetsMultiplierAndDuration()
    {
        player.ApplyBuff(0.5f, 12f);
        Assert.AreEqual(0.5f, player.BuffFireIntervalMultiplier, 1e-4f);
        Assert.AreEqual(12f, player.BuffTimeRemaining, 1e-4f);
    }

    [Test]
    public void ApplyBuff_ReApplying_RefreshesDuration_DoesNotStackMultiplier()
    {
        player.ApplyBuff(0.5f, 12f);
        player.Tick(8f); // 4s remaining
        Assert.AreEqual(4f, player.BuffTimeRemaining, 1e-3f);

        player.ApplyBuff(0.5f, 12f); // picked up a second BuffItem
        Assert.AreEqual(12f, player.BuffTimeRemaining, 1e-3f); // refreshed, not 4+12=16
        Assert.AreEqual(0.5f, player.BuffFireIntervalMultiplier, 1e-4f); // still 2x, not 4x
    }

    [Test]
    public void Tick_ExpiresBuff_AfterDurationElapses()
    {
        player.ApplyBuff(0.5f, 5f);
        player.Tick(5f); // exactly Duration
        Assert.AreEqual(0f, player.BuffTimeRemaining, 1e-3f);
        Assert.AreEqual(1f, player.BuffFireIntervalMultiplier, 1e-4f); // back to neutral
    }

    [Test]
    public void ResetState_ClearsActiveBuff()
    {
        player.ApplyBuff(0.5f, 12f);
        player.ResetState();
        Assert.AreEqual(0f, player.BuffTimeRemaining);
        Assert.AreEqual(1f, player.BuffFireIntervalMultiplier);
    }
```

- [ ] **Step 3: 테스트 실행 — 실패 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat
```

기대값: `Heal`/`ApplyBuff`/`BuffTimeRemaining`/`BuffFireIntervalMultiplier`/`EffectiveFireIntervalWithBuff`가 없어 컴파일 에러.

- [ ] **Step 4: `UpgradeEffects.cs` 수정**

기존:

```csharp
        const float MinFireInterval = 0.05f;
```

를:

```csharp
        // internal (not private): GameManager and this class's own EditMode tests both
        // need to reference the exact floor value, via [assembly: InternalsVisibleTo("Tests.EditMode")].
        internal const float MinFireInterval = 0.05f;
```

`EffectiveFireInterval` 메서드 바로 아래에 새 메서드 추가:

```csharp
        // EffectiveFireInterval is already clamped to MinFireInterval on its own, so
        // multiplying by a buff (e.g. 0.5x from BuffItem) could push it back below that
        // floor - re-clamp after applying the buff too. See GameManager.Update(), the
        // one place this composes with the per-frame permanent-upgrade recalculation.
        public static float EffectiveFireIntervalWithBuff(Dictionary<string, int> stacks, float buffMultiplier) =>
            Mathf.Max(EffectiveFireInterval(stacks) * buffMultiplier, MinFireInterval);
```

- [ ] **Step 5: `PlayerController.cs` 수정**

필드 선언부, 기존:

```csharp
        float maxHp;
        float hp;
        float moveSpeed;
        int level;
        float xp;
        float invincibleTimer;
        Dictionary<string, int> upgradeStacks;
```

를:

```csharp
        float maxHp;
        float hp;
        float moveSpeed;
        int level;
        float xp;
        float invincibleTimer;
        Dictionary<string, int> upgradeStacks;
        float buffTimeRemaining;
        float buffFireIntervalMultiplier;
```

프로퍼티부, 기존:

```csharp
        public float InvincibleTimer { get { EnsureInitialized(); return invincibleTimer; } }
        public Dictionary<string, int> UpgradeStacks { get { EnsureInitialized(); return upgradeStacks; } }
```

를:

```csharp
        public float InvincibleTimer { get { EnsureInitialized(); return invincibleTimer; } }
        public Dictionary<string, int> UpgradeStacks { get { EnsureInitialized(); return upgradeStacks; } }
        public float BuffTimeRemaining { get { EnsureInitialized(); return buffTimeRemaining; } }
        public float BuffFireIntervalMultiplier { get { EnsureInitialized(); return buffFireIntervalMultiplier; } }
```

`ResetState()`, 기존:

```csharp
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
```

를:

```csharp
        public void ResetState()
        {
            maxHp = GameConfig.Player.MaxHp;
            hp = maxHp;
            moveSpeed = GameConfig.Player.MoveSpeed;
            level = 1;
            xp = 0f;
            invincibleTimer = 0f;
            upgradeStacks = new Dictionary<string, int>();
            buffTimeRemaining = 0f;
            buffFireIntervalMultiplier = 1f;
        }
```

`Tick(float dt)`(Task 1에서 이미 clamp를 제거한 버전), 기존 끝부분:

```csharp
            if (invincibleTimer > 0f)
                invincibleTimer = Mathf.Max(0f, invincibleTimer - dt);
        }
```

를:

```csharp
            if (invincibleTimer > 0f)
                invincibleTimer = Mathf.Max(0f, invincibleTimer - dt);

            if (buffTimeRemaining > 0f)
            {
                buffTimeRemaining = Mathf.Max(0f, buffTimeRemaining - dt);
                if (buffTimeRemaining <= 0f) buffFireIntervalMultiplier = 1f;
            }
        }
```

`ApplyUpgrade` 메서드 뒤(파일 마지막 메서드 뒤), 클래스 닫는 `}` 앞에 새 메서드 2개 추가:

```csharp
        public void Heal(float amount)
        {
            EnsureInitialized();
            hp = Mathf.Min(maxHp, hp + amount);
        }

        // Re-picking up a BuffItem while one is already active refreshes the duration
        // but does not stack the multiplier (see spec's Q6 decision) - simply
        // overwriting both fields achieves that naturally, no extra branching needed.
        public void ApplyBuff(float fireIntervalMultiplier, float duration)
        {
            EnsureInitialized();
            buffFireIntervalMultiplier = fireIntervalMultiplier;
            buffTimeRemaining = duration;
        }
```

- [ ] **Step 6: `GameManager.cs`의 `Update()` 수정**

기존:

```csharp
            PlayerWeapon.Damage = UpgradeEffects.EffectiveWeaponDamage(Player.UpgradeStacks);
            PlayerWeapon.FireInterval = UpgradeEffects.EffectiveFireInterval(Player.UpgradeStacks);
```

를:

```csharp
            PlayerWeapon.Damage = UpgradeEffects.EffectiveWeaponDamage(Player.UpgradeStacks);
            PlayerWeapon.FireInterval = UpgradeEffects.EffectiveFireIntervalWithBuff(Player.UpgradeStacks, Player.BuffFireIntervalMultiplier);
```

- [ ] **Step 7: 테스트 재실행 — 통과 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat
grep 'result="Failed"' results.xml
```

기대값: 아무 출력 없음.

- [ ] **Step 8: 재빌드 + PlayMode 회귀**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-playtest.bat
grep 'result="Failed"' playtest-results.xml
```

기대값: 아무 출력 없음.

- [ ] **Step 9: Commit**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
git add Assets/Scripts/UpgradeEffects.cs Assets/Scripts/PlayerController.cs Assets/Scripts/GameManager.cs Assets/Tests/EditMode/UpgradeEffectsTests.cs Assets/Tests/EditMode/PlayerControllerTests.cs results.xml playtest-results.xml playtest.log
git commit -m "Add temporary fire-rate buff state to PlayerController

BuffTimeRemaining/BuffFireIntervalMultiplier live on PlayerController
and tick down in Tick(dt). UpgradeEffects.EffectiveFireIntervalWithBuff
composes the buff multiplier with permanent fireRate upgrades at the
one place GameManager.Update() recalculates PlayerWeapon.FireInterval
every frame - putting it anywhere else (e.g. inside Weapon.cs) would
get silently overwritten next frame. Re-clamps to MinFireInterval after
the buff multiply so a buff can't push fire rate past the intended
floor. Re-applying a buff while active refreshes duration without
stacking magnitude.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 6: `HealthPotion`/`BuffItem` 컴포넌트 + PIL 플레이스홀더 아트

**Files:**
- Create: `Assets/Scripts/HealthPotion.cs`
- Create: `Assets/Scripts/BuffItem.cs`
- Create: `Assets/Sprites/health-potion.png`, `Assets/Sprites/buff-item.png` (PIL 생성)
- Modify: `Assets/Editor/ProjectBootstrap.cs` (스프라이트 임포트, 프리팹 빌더 2개, `BuildProject`/`BuildMainScene` 시그니처 확장, `GameManager` 필드 배선)
- Modify: `Assets/Scripts/GameManager.cs` (`HealthPotionPrefab`/`BuffItemPrefab` 필드만 추가 — 아직 사용처는 Task 7)

**Interfaces:**
- Produces: `HealthPotion`(`public float HealAmount`), `BuffItem`(`public float FireIntervalMultiplier`, `public float Duration`) 컴포넌트. `Assets/Prefabs/HealthPotion.prefab`, `Assets/Prefabs/BuffItem.prefab`.

- [ ] **Step 1: PIL로 플레이스홀더 아이콘 생성**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
python -c "
from PIL import Image, ImageDraw

def make_health_icon(path, size=128):
    im = Image.new('RGBA', (size, size), (0,0,0,0))
    d = ImageDraw.Draw(im)
    cx, cy, r = size//2, size//2, size//2 - 8
    d.ellipse([cx-r, cy-r, cx+r, cy+r], fill=(200,30,40,255), outline=(20,20,20,255), width=4)
    bar_w, bar_h = 14, 56
    d.rectangle([cx-bar_w//2, cy-bar_h//2, cx+bar_w//2, cy+bar_h//2], fill=(255,255,255,255))
    d.rectangle([cx-bar_h//2, cy-bar_w//2, cx+bar_h//2, cy+bar_w//2], fill=(255,255,255,255))
    im.save(path)

def make_buff_icon(path, size=128):
    im = Image.new('RGBA', (size, size), (0,0,0,0))
    d = ImageDraw.Draw(im)
    cx, cy, r = size//2, size//2, size//2 - 8
    d.ellipse([cx-r, cy-r, cx+r, cy+r], fill=(40,140,220,255), outline=(20,20,20,255), width=4)
    bolt = [(70,24),(46,68),(62,68),(50,104),(84,56),(66,56)]
    d.polygon(bolt, fill=(255,225,40,255), outline=(20,20,20,255))
    im.save(path)

make_health_icon('Assets/Sprites/health-potion.png')
make_buff_icon('Assets/Sprites/buff-item.png')
print('done')
"
```

기대값: `Assets/Sprites/health-potion.png`, `Assets/Sprites/buff-item.png` 생성, 콘솔에 `done` 출력.

- [ ] **Step 2: 결과 확인 (치수/알파 채널)**

```bash
python -c "
from PIL import Image
for f in ['health-potion.png', 'buff-item.png']:
    im = Image.open('Assets/Sprites/' + f)
    print(f, im.size, im.mode)
"
```

기대값: 둘 다 `(128, 128) RGBA`.

- [ ] **Step 3: 컴포넌트 작성**

`Assets/Scripts/HealthPotion.cs`:

```csharp
using UnityEngine;

namespace YiSunSin
{
    public class HealthPotion : MonoBehaviour
    {
        public float HealAmount; // set by GameManager on spawn: Player.MaxHp * GameConfig.HealFraction
    }
}
```

`Assets/Scripts/BuffItem.cs`:

```csharp
using UnityEngine;

namespace YiSunSin
{
    public class BuffItem : MonoBehaviour
    {
        public float FireIntervalMultiplier = GameConfig.BuffFireIntervalMultiplier;
        public float Duration = GameConfig.BuffDuration;
    }
}
```

- [ ] **Step 4: `GameConfig.cs`에 상수 추가**

기존:

```csharp
        public const float XpGemValue = 1f;
        public const float XpGemPickupRadius = 40f;
```

를:

```csharp
        public const float XpGemValue = 1f;
        public const float XpGemPickupRadius = 40f;

        public const float HealFraction = 0.25f; // fraction of MaxHp a HealthPotion restores
        public const float BonusItemDropChance = 0.08f; // chance an enemy kill also drops a HealthPotion/BuffItem
        public const float BuffFireIntervalMultiplier = 0.5f; // BuffItem: 2x fire rate
        public const float BuffDuration = 12f; // seconds
        public const float AmbientItemSpawnMinInterval = 20f;
        public const float AmbientItemSpawnMaxInterval = 40f;
        public const float AmbientSpawnMargin = 150f; // expands the camera-relative spawn Rect for ambient items
```

- [ ] **Step 5: `ProjectBootstrap.cs`에 임포트 + 프리팹 빌더 추가**

`ImportSpriteSheets()`, 기존:

```csharp
            ImportSingle("Assets/Sprites/medal.png", 128, PixelsPerUnitFor(128, MedalVisualDiameter / 2f));
            ImportBackground("Assets/Sprites/background.png");
```

를:

```csharp
            ImportSingle("Assets/Sprites/medal.png", 128, PixelsPerUnitFor(128, MedalVisualDiameter / 2f));
            ImportSingle("Assets/Sprites/health-potion.png", 128, PixelsPerUnitFor(128, ItemVisualDiameter / 2f));
            ImportSingle("Assets/Sprites/buff-item.png", 128, PixelsPerUnitFor(128, ItemVisualDiameter / 2f));
            ImportBackground("Assets/Sprites/background.png");
```

`MedalVisualDiameter` 상수 선언 바로 아래에 추가:

```csharp
        const float ItemVisualDiameter = 16f; // HealthPotion/BuffItem icons, same subordinate size as the medal
```

`BuildXpGemPrefab()` 메서드 뒤에 새 메서드 2개 추가:

```csharp
        static GameObject BuildHealthPotionPrefab()
        {
            var go = new GameObject("HealthPotion", typeof(SpriteRenderer), typeof(HealthPotion));
            go.GetComponent<SpriteRenderer>().sprite = LoadSprite("Assets/Sprites/health-potion.png", "health-potion");
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/HealthPotion.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject BuildBuffItemPrefab()
        {
            var go = new GameObject("BuffItem", typeof(SpriteRenderer), typeof(BuffItem));
            go.GetComponent<SpriteRenderer>().sprite = LoadSprite("Assets/Sprites/buff-item.png", "buff-item");
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/BuffItem.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }
```

`BuildProject()`에서 기존:

```csharp
            var projectilePrefab = BuildProjectilePrefab();
            var xpGemPrefab = BuildXpGemPrefab();
            var enemyPrefab = BuildEnemyPrefab();
            var playerPrefab = BuildPlayerPrefab();
```

를:

```csharp
            var projectilePrefab = BuildProjectilePrefab();
            var xpGemPrefab = BuildXpGemPrefab();
            var healthPotionPrefab = BuildHealthPotionPrefab();
            var buffItemPrefab = BuildBuffItemPrefab();
            var enemyPrefab = BuildEnemyPrefab();
            var playerPrefab = BuildPlayerPrefab();
```

같은 메서드의 `BuildMainScene(...)` 호출부, 기존:

```csharp
            BuildMainScene(playerPrefab, enemyPrefab, projectilePrefab, xpGemPrefab, hitEffect, deathEffect, levelUpEffect, bossSpawnEffect);
```

를:

```csharp
            BuildMainScene(playerPrefab, enemyPrefab, projectilePrefab, xpGemPrefab, healthPotionPrefab, buffItemPrefab, hitEffect, deathEffect, levelUpEffect, bossSpawnEffect);
```

`BuildMainScene`의 시그니처, 기존:

```csharp
        static void BuildMainScene(GameObject playerPrefab, GameObject enemyPrefab, GameObject projectilePrefab, GameObject xpGemPrefab,
            ParticleSystem hitEffect, ParticleSystem deathEffect, ParticleSystem levelUpEffect, ParticleSystem bossSpawnEffect)
```

를:

```csharp
        static void BuildMainScene(GameObject playerPrefab, GameObject enemyPrefab, GameObject projectilePrefab, GameObject xpGemPrefab,
            GameObject healthPotionPrefab, GameObject buffItemPrefab,
            ParticleSystem hitEffect, ParticleSystem deathEffect, ParticleSystem levelUpEffect, ParticleSystem bossSpawnEffect)
```

- [ ] **Step 6: `GameManager.cs`에 필드 추가 + `ProjectBootstrap.cs`에서 배선**

`Assets/Scripts/GameManager.cs`의 필드 선언부, 기존:

```csharp
        public GameObject XpGemPrefab;
        public GameObject BossSpotlight;
        public Transform CameraTransform;
```

를:

```csharp
        public GameObject XpGemPrefab;
        public GameObject HealthPotionPrefab;
        public GameObject BuffItemPrefab;
        public GameObject BossSpotlight;
        public Transform CameraTransform;
```

`ProjectBootstrap.cs`의 GameManager 필드 배선부, 기존:

```csharp
            manager.XpGemPrefab = xpGemPrefab;
            manager.BossSpotlight = spotlightGo;
            manager.CameraTransform = cameraGo.transform;
```

를:

```csharp
            manager.XpGemPrefab = xpGemPrefab;
            manager.HealthPotionPrefab = healthPotionPrefab;
            manager.BuffItemPrefab = buffItemPrefab;
            manager.BossSpotlight = spotlightGo;
            manager.CameraTransform = cameraGo.transform;
```

- [ ] **Step 7: 재빌드 — 프리팹이 정상 생성되는지 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
"C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\ASUS\Desktop\base\game-unity" -executeMethod YiSunSin.EditorTools.ProjectBootstrap.BuildProject -logFile build.log
grep -i "error\|missing sprite" build.log
ls Assets/Prefabs/HealthPotion.prefab Assets/Prefabs/BuffItem.prefab
```

기대값: `grep`에 아무 출력 없음, 두 프리팹 파일 모두 존재.

- [ ] **Step 8: EditMode/PlayMode 회귀**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat && ./run-playtest.bat
grep 'result="Failed"' results.xml playtest-results.xml
```

기대값: 아무 출력 없음 (이 태스크는 아직 아무것도 스폰하지 않으므로 기존 동작 100% 유지).

- [ ] **Step 9: Commit**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
git add Assets/Scripts/HealthPotion.cs Assets/Scripts/BuffItem.cs Assets/Scripts/GameConfig.cs Assets/Scripts/GameManager.cs Assets/Sprites/health-potion.png Assets/Sprites/buff-item.png Assets/Editor/ProjectBootstrap.cs Assets/Prefabs/HealthPotion.prefab Assets/Prefabs/BuffItem.prefab Assets/Scenes/Main.unity build.log results.xml playtest-results.xml playtest.log
git commit -m "Add HealthPotion/BuffItem components, config, and prefabs

Simple data components matching the existing XpGem pattern. Sprites
are PIL-drawn placeholders (red cross in a circle / lightning bolt in
a circle) - same treatment as arrow.png/medal.png - to be swapped for
AI-generated art once HuggingFace ZeroGPU quota is available. Nothing
spawns these yet (drop/pickup logic lands in the next task); this task
only wires the prefabs and config constants through.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 7: 적 처치 시 보너스 드롭 + `worldItems` 픽업/정리

**Files:**
- Modify: `Assets/Scripts/GameManager.cs` (`Rng` 필드, `worldItems` 리스트, `ProcessPendingHits`의 보너스 드롭, `CollectWorldItems_ForTests`, `StartGame()`의 정리)
- Modify: `Assets/Tests/EditMode/GameManagerTests.cs` (보너스 드롭 + 픽업 + 재시작 정리 테스트)

**Interfaces:**
- Consumes: Task 6의 `HealthPotionPrefab`/`BuffItemPrefab`, Task 5의 `PlayerController.Heal`/`ApplyBuff`.
- Produces: `GameManager.CollectWorldItems_ForTests()` (internal), `GameManager.worldItems`(내부 상태, 직접 노출 안 함).

- [ ] **Step 1: `GameManagerTests.cs`에 실패하는 테스트 작성**

`SetUp()`에서 `manager.HealthPotionPrefab`/`manager.BuffItemPrefab`을 배선해야 한다. 기존 `xpGemPrefab` 생성부 바로 아래에 추가:

```csharp
        var healthPotionPrefab = new GameObject("HealthPotionPrefab", typeof(HealthPotion));
        healthPotionPrefab.SetActive(false);
        var buffItemPrefab = new GameObject("BuffItemPrefab", typeof(BuffItem));
        buffItemPrefab.SetActive(false);
```

그리고 `manager.XpGemPrefab = xpGemPrefab;` 바로 아래에:

```csharp
        manager.HealthPotionPrefab = healthPotionPrefab;
        manager.BuffItemPrefab = buffItemPrefab;
        manager.Rng = () => 0f; // deterministic default; individual tests override where needed
```

파일 맨 아래에 새 테스트 추가:

```csharp
    [Test]
    public void KillingAnEnemy_WithLowRng_DropsBonusHealthPotion_ThatHealsOnPickup()
    {
        manager.Rng = () => 0f; // < BonusItemDropChance (always triggers), and < 0.5 (picks HealthPotion)
        manager.StartGame();
        player.TakeDamage(50f); // Hp = 50, so healing is observable

        var enemyGo = new GameObject("Enemy", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController));
        var enemy = enemyGo.GetComponent<EnemyController>();
        enemy.Initialize(GameConfig.EnemyBasic, false);
        enemyGo.transform.position = player.transform.position;

        var projGo = new GameObject("Projectile", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Projectile));
        var projectile = projGo.GetComponent<Projectile>();
        projectile.Launch(player.transform.position, Vector2.right, 0f, 100f, 400f);

        manager.OnProjectileHit(projectile, enemy);
        manager.CollectGems_ForTests();
        manager.CollectWorldItems_ForTests();

        Assert.Greater(player.Hp, 50f, "HealthPotion should have healed the player on pickup");

        Object.DestroyImmediate(enemyGo);
        Object.DestroyImmediate(projGo);
    }

    [Test]
    public void KillingAnEnemy_WithHighRng_DropsNoBonusItem()
    {
        manager.Rng = () => 0.99f; // > BonusItemDropChance, never triggers
        manager.StartGame();

        var enemyGo = new GameObject("Enemy", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController));
        var enemy = enemyGo.GetComponent<EnemyController>();
        enemy.Initialize(GameConfig.EnemyBasic, false);
        enemyGo.transform.position = player.transform.position;

        var projGo = new GameObject("Projectile", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Projectile));
        var projectile = projGo.GetComponent<Projectile>();
        projectile.Launch(player.transform.position, Vector2.right, 0f, 100f, 400f);

        float hpBefore = player.Hp;
        manager.OnProjectileHit(projectile, enemy);
        manager.CollectGems_ForTests();
        manager.CollectWorldItems_ForTests();

        Assert.AreEqual(hpBefore, player.Hp, "no bonus item should have dropped, so no heal");

        Object.DestroyImmediate(enemyGo);
        Object.DestroyImmediate(projGo);
    }

    [Test]
    public void StartGame_ClearsLeftoverWorldItems_FromPreviousRun()
    {
        manager.Rng = () => 0f; // always drops a bonus item
        manager.StartGame();

        var enemyGo = new GameObject("Enemy", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController));
        var enemy = enemyGo.GetComponent<EnemyController>();
        enemy.Initialize(GameConfig.EnemyBasic, false);
        enemyGo.transform.position = player.transform.position + Vector3.right * 500f; // far from player: won't be auto-picked-up

        var projGo = new GameObject("Projectile", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Projectile));
        var projectile = projGo.GetComponent<Projectile>();
        projectile.Launch(enemyGo.transform.position, Vector2.right, 0f, 100f, 400f);

        manager.OnProjectileHit(projectile, enemy);
        manager.CollectGems_ForTests();
        manager.CollectWorldItems_ForTests(); // too far to pick up, so the item remains in the world

        int itemsBeforeRestart = Object.FindObjectsByType<HealthPotion>(FindObjectsSortMode.None).Length +
                                  Object.FindObjectsByType<BuffItem>(FindObjectsSortMode.None).Length;
        Assert.Greater(itemsBeforeRestart, 0, "sanity check: an unpicked item should exist before restart");

        manager.StartGame();

        int itemsAfterRestart = Object.FindObjectsByType<HealthPotion>(FindObjectsSortMode.None).Length +
                                 Object.FindObjectsByType<BuffItem>(FindObjectsSortMode.None).Length;
        Assert.AreEqual(0, itemsAfterRestart, "StartGame() must clear leftover world items from the previous run");

        Object.DestroyImmediate(enemyGo);
        Object.DestroyImmediate(projGo);
    }
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat
```

기대값: `manager.Rng`, `CollectWorldItems_ForTests`가 없어 컴파일 에러.

- [ ] **Step 3: `GameManager.cs` 수정**

필드 선언부, 기존:

```csharp
        int pendingLevelUps;
        readonly List<EnemyController> enemies = new List<EnemyController>();
        readonly List<GameObject> xpGems = new List<GameObject>();
        readonly List<Projectile> projectiles = new List<Projectile>();
        readonly List<(Projectile projectile, EnemyController enemy)> pendingHits = new List<(Projectile, EnemyController)>();
```

를:

```csharp
        public Func<float> Rng = () => UnityEngine.Random.value;

        int pendingLevelUps;
        readonly List<EnemyController> enemies = new List<EnemyController>();
        readonly List<GameObject> xpGems = new List<GameObject>();
        readonly List<GameObject> worldItems = new List<GameObject>(); // HealthPotion/BuffItem, shared list
        readonly List<Projectile> projectiles = new List<Projectile>();
        readonly List<(Projectile projectile, EnemyController enemy)> pendingHits = new List<(Projectile, EnemyController)>();
```

`StartGame()`에서 기존:

```csharp
            foreach (var g in xpGems) if (g != null) SafeDestroy(g);
            xpGems.Clear();
            foreach (var p in projectiles) if (p != null) SafeDestroy(p.gameObject);
            projectiles.Clear();
```

를:

```csharp
            foreach (var g in xpGems) if (g != null) SafeDestroy(g);
            xpGems.Clear();
            foreach (var item in worldItems) if (item != null) SafeDestroy(item);
            worldItems.Clear();
            foreach (var p in projectiles) if (p != null) SafeDestroy(p.gameObject);
            projectiles.Clear();
```

`ProcessPendingHits()`에서 기존:

```csharp
                if (enemy.IsDead)
                {
                    if (ParticleEffects.Instance != null) ParticleEffects.Instance.PlayDeath(enemy.transform.position);
                    float xpValue = enemy.IsBoss ? GameConfig.BossXpValue : GameConfig.XpGemValue;
                    var gem = Instantiate(XpGemPrefab, enemy.transform.position, Quaternion.identity, transform);
                    gem.SetActive(true);
                    gem.GetComponent<XpGem>().Value = xpValue;
                    xpGems.Add(gem);
                }
```

를:

```csharp
                if (enemy.IsDead)
                {
                    if (ParticleEffects.Instance != null) ParticleEffects.Instance.PlayDeath(enemy.transform.position);
                    float xpValue = enemy.IsBoss ? GameConfig.BossXpValue : GameConfig.XpGemValue;
                    var gem = Instantiate(XpGemPrefab, enemy.transform.position, Quaternion.identity, transform);
                    gem.SetActive(true);
                    gem.GetComponent<XpGem>().Value = xpValue;
                    xpGems.Add(gem);

                    if (Rng() < GameConfig.BonusItemDropChance)
                        worldItems.Add(SpawnWorldItem(enemy.transform.position));
                }
```

새 private 헬퍼를 `ProcessPendingHits()` 바로 아래에 추가:

```csharp
        GameObject SpawnWorldItem(Vector3 position)
        {
            GameObject prefab = Rng() < 0.5f ? HealthPotionPrefab : BuffItemPrefab;
            var item = Instantiate(prefab, position, Quaternion.identity, transform);
            item.SetActive(true);
            if (item.TryGetComponent<HealthPotion>(out var potion))
                potion.HealAmount = Player.MaxHp * GameConfig.HealFraction;
            return item;
        }
```

`CollectGems_ForTests()` 메서드 뒤에 새 메서드 추가:

```csharp
        internal void CollectWorldItems_ForTests()
        {
            float pickupRadius = UpgradeEffects.EffectivePickupRadius(Player.UpgradeStacks);
            var remaining = new List<GameObject>();

            foreach (var itemObj in worldItems)
            {
                if (itemObj == null) continue;

                float dist = Vector2.Distance(itemObj.transform.position, Player.transform.position);
                if (dist > pickupRadius) { remaining.Add(itemObj); continue; }

                if (itemObj.TryGetComponent<HealthPotion>(out var potion))
                    Player.Heal(potion.HealAmount);
                else if (itemObj.TryGetComponent<BuffItem>(out var buff))
                    Player.ApplyBuff(buff.FireIntervalMultiplier, buff.Duration);

                SafeDestroy(itemObj);
            }

            worldItems.Clear();
            worldItems.AddRange(remaining);
        }
```

`Update()`에서 `CollectGems_ForTests();` 바로 아래(기존 `if (Status != GameStatus.Playing) return;` 다음)에 추가:

```csharp
            CollectWorldItems_ForTests();
```

- [ ] **Step 4: 테스트 재실행 — 통과 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat
grep 'result="Failed"' results.xml
```

기대값: 아무 출력 없음.

- [ ] **Step 5: 재빌드 + PlayMode 회귀**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-playtest.bat
grep 'result="Failed"' playtest-results.xml
```

기대값: 아무 출력 없음.

- [ ] **Step 6: Commit**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
git add Assets/Scripts/GameManager.cs Assets/Tests/EditMode/GameManagerTests.cs Assets/Scenes/Main.unity build.log results.xml playtest-results.xml playtest.log
git commit -m "Add bonus item drops on enemy kill + worldItems pickup/cleanup

8% chance (GameManager.Rng-driven, injectable for deterministic tests
matching SpawnController's pattern) an enemy kill also drops a
HealthPotion or BuffItem alongside the always-dropped XP gem.
CollectWorldItems_ForTests picks them up using the same
EffectivePickupRadius as XP gems. StartGame() now clears worldItems
the same way it already clears xpGems/enemies/projectiles, so unpicked
items don't leak across restarts.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 8: `AmbientItemSpawner.cs` (신규) — 맵 탐색 중 랜덤 스폰

**Files:**
- Create: `Assets/Scripts/AmbientItemSpawner.cs`
- Create: `Assets/Tests/EditMode/AmbientItemSpawnerTests.cs`
- Modify: `Assets/Scripts/GameManager.cs` (`AmbientSpawner` 필드, `Update()`/`StartGame()` 배선)
- Modify: `Assets/Editor/ProjectBootstrap.cs` (`AmbientItemSpawner` GameObject 생성 + 배선)

**Interfaces:**
- Consumes: Task 6의 `HealthPotionPrefab`/`BuffItemPrefab` (타입만, `AmbientItemSpawner`는 자체 필드로 받음), Task 4의 `GameManager.BoundsAroundCamera()`.
- Produces: `AmbientItemSpawner.Tick(float dt, Rect expandedBounds, Transform parent)` → `GameObject`(스폰 안 했으면 `null`), `AmbientItemSpawner.ResetState()`.

- [ ] **Step 1: 실패하는 EditMode 테스트 작성**

`Assets/Tests/EditMode/AmbientItemSpawnerTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using YiSunSin;

public class AmbientItemSpawnerTests
{
    GameObject spawnerGo;
    AmbientItemSpawner spawner;
    GameObject healthPrefab;
    GameObject buffPrefab;

    static readonly Rect Bounds = new Rect(-950f, -600f, 1900f, 1200f); // arena bounds + AmbientSpawnMargin

    [SetUp]
    public void SetUp()
    {
        healthPrefab = new GameObject("HealthPotionPrefab", typeof(HealthPotion));
        healthPrefab.SetActive(false);
        buffPrefab = new GameObject("BuffItemPrefab", typeof(BuffItem));
        buffPrefab.SetActive(false);

        spawnerGo = new GameObject("AmbientSpawner", typeof(AmbientItemSpawner));
        spawner = spawnerGo.GetComponent<AmbientItemSpawner>();
        spawner.HealthPotionPrefab = healthPrefab;
        spawner.BuffItemPrefab = buffPrefab;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(spawnerGo);
        Object.DestroyImmediate(healthPrefab);
        Object.DestroyImmediate(buffPrefab);
    }

    [Test]
    public void Tick_SpawnsNothing_BeforeIntervalElapses()
    {
        spawner.Rng = () => 0f; // picks the minimum interval (20s)
        var result = spawner.Tick(1f, Bounds, null);
        Assert.IsNull(result);
    }

    [Test]
    public void Tick_SpawnsOnce_WhenMinIntervalElapses()
    {
        spawner.Rng = () => 0f; // interval = AmbientItemSpawnMinInterval (20s), always picks HealthPotion (< 0.5)
        var result = spawner.Tick(GameConfig.AmbientItemSpawnMinInterval, Bounds, null);
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.GetComponent<HealthPotion>());
        Object.DestroyImmediate(result);
    }

    [Test]
    public void Tick_SpawnPosition_IsWithinExpandedBounds()
    {
        spawner.Rng = () => 0.5f; // interval midpoint, position midpoint, picks BuffItem (>= 0.5)
        var result = spawner.Tick(GameConfig.AmbientItemSpawnMaxInterval, Bounds, null);
        Assert.IsNotNull(result);
        Assert.IsTrue(Bounds.Contains(result.transform.position));
        Object.DestroyImmediate(result);
    }

    [Test]
    public void ResetState_ForcesFreshIntervalRoll_OnNextTick()
    {
        spawner.Rng = () => 0f;
        spawner.Tick(GameConfig.AmbientItemSpawnMinInterval, Bounds, null); // consumes the first interval
        spawner.ResetState();

        // Immediately after ResetState, a tiny dt should not be enough to spawn again -
        // proves ResetState rolled a fresh interval rather than leaving a near-zero countdown.
        var result = spawner.Tick(0.01f, Bounds, null);
        Assert.IsNull(result);
    }
}
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat
```

기대값: `AmbientItemSpawner` 타입이 없어 컴파일 에러.

- [ ] **Step 3: `AmbientItemSpawner.cs` 구현**

```csharp
using System;
using UnityEngine;

namespace YiSunSin
{
    // Same Func<float> Rng injection pattern as SpawnController, for deterministic
    // EditMode tests. Unlike SpawnController (enemies spawn only at the bounds edge),
    // ambient items can appear anywhere inside the (margin-expanded) bounds - they're
    // static pickups, not something that needs to "walk in" from off-screen.
    public class AmbientItemSpawner : MonoBehaviour
    {
        public GameObject HealthPotionPrefab;
        public GameObject BuffItemPrefab;
        public Func<float> Rng = () => UnityEngine.Random.value;

        float timeUntilNextSpawn = -1f; // sentinel: roll a real interval on the first Tick

        public GameObject Tick(float dt, Rect expandedBounds, Transform parent)
        {
            if (timeUntilNextSpawn < 0f) timeUntilNextSpawn = NextInterval();
            timeUntilNextSpawn -= dt;
            if (timeUntilNextSpawn > 0f) return null;

            timeUntilNextSpawn = NextInterval();
            Vector2 pos = new Vector2(
                expandedBounds.xMin + Rng() * expandedBounds.width,
                expandedBounds.yMin + Rng() * expandedBounds.height);
            GameObject prefab = Rng() < 0.5f ? HealthPotionPrefab : BuffItemPrefab;
            var obj = Instantiate(prefab, pos, Quaternion.identity, parent);
            obj.SetActive(true);
            return obj;
        }

        float NextInterval() =>
            GameConfig.AmbientItemSpawnMinInterval +
            Rng() * (GameConfig.AmbientItemSpawnMaxInterval - GameConfig.AmbientItemSpawnMinInterval);

        public void ResetState() => timeUntilNextSpawn = -1f;
    }
}
```

- [ ] **Step 4: 테스트 재실행 — 통과 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat
grep 'result="Failed"' results.xml
```

기대값: 아무 출력 없음.

- [ ] **Step 5: `GameManager.cs` 배선**

필드 선언부, 기존:

```csharp
        public SpawnController Spawner;
```

를:

```csharp
        public SpawnController Spawner;
        public AmbientItemSpawner AmbientSpawner;
```

`StartGame()`에서 기존:

```csharp
            Spawner.ResetState();
```

를:

```csharp
            Spawner.ResetState();
            AmbientSpawner.ResetState();
```

`Update()`의 `var bounds = BoundsAroundCamera();` / `Spawner.Tick(...)` 블록 뒤(보스 스폰 처리가 끝나는 `foreach (var e in spawned) { ... }` 블록 바로 뒤)에 추가:

```csharp
            var expandedBounds = new Rect(
                bounds.xMin - GameConfig.AmbientSpawnMargin, bounds.yMin - GameConfig.AmbientSpawnMargin,
                bounds.width + 2f * GameConfig.AmbientSpawnMargin, bounds.height + 2f * GameConfig.AmbientSpawnMargin);
            var ambientItem = AmbientSpawner.Tick(dt, expandedBounds, transform);
            if (ambientItem != null)
            {
                if (ambientItem.TryGetComponent<HealthPotion>(out var potion))
                    potion.HealAmount = Player.MaxHp * GameConfig.HealFraction;
                worldItems.Add(ambientItem);
            }
```

- [ ] **Step 6: `ProjectBootstrap.cs`에 `AmbientItemSpawner` GameObject 생성 + 배선**

`BuildMainScene()`에서 기존:

```csharp
            var spawnerGo = new GameObject("Spawner", typeof(SpawnController));
            spawnerGo.GetComponent<SpawnController>().EnemyPrefab = enemyPrefab;
```

를:

```csharp
            var spawnerGo = new GameObject("Spawner", typeof(SpawnController));
            spawnerGo.GetComponent<SpawnController>().EnemyPrefab = enemyPrefab;

            var ambientSpawnerGo = new GameObject("AmbientItemSpawner", typeof(AmbientItemSpawner));
            var ambientSpawner = ambientSpawnerGo.GetComponent<AmbientItemSpawner>();
            ambientSpawner.HealthPotionPrefab = healthPotionPrefab;
            ambientSpawner.BuffItemPrefab = buffItemPrefab;
```

기존:

```csharp
            manager.Spawner = spawnerGo.GetComponent<SpawnController>();
```

를:

```csharp
            manager.Spawner = spawnerGo.GetComponent<SpawnController>();
            manager.AmbientSpawner = ambientSpawner;
```

- [ ] **Step 7: `GameManagerTests.cs`의 `SetUp()`에도 `AmbientSpawner` 배선 추가 (안 하면 Task 5~7의 기존 테스트들이 `StartGame()` 호출 시 NullReferenceException으로 깨짐)**

`SetUp()`에서 기존 `spawner` 생성부 바로 아래에 추가:

```csharp
        var ambientSpawnerGo = new GameObject("AmbientSpawner", typeof(AmbientItemSpawner));
        ambientSpawnerGo.transform.SetParent(root.transform);
        var ambientSpawner = ambientSpawnerGo.GetComponent<AmbientItemSpawner>();
        ambientSpawner.HealthPotionPrefab = healthPotionPrefab;
        ambientSpawner.BuffItemPrefab = buffItemPrefab;
        ambientSpawner.Rng = () => 0.99f; // avoid ambient spawns interfering unless a test overrides it
```

(이 줄은 `healthPotionPrefab`/`buffItemPrefab` 변수가 선언된 뒤에 와야 한다 — Task 7에서 이미 추가한 위치 다음)

그리고 `manager.AmbientSpawner = ambientSpawner;`를 `manager.HealthPotionPrefab = healthPotionPrefab;` 근처에 추가.

- [ ] **Step 8: 전체 테스트 재실행 — 통과 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat
grep 'result="Failed"' results.xml
```

기대값: 아무 출력 없음.

- [ ] **Step 9: 재빌드 + PlayMode 회귀**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-playtest.bat
grep 'result="Failed"' playtest-results.xml
```

기대값: 아무 출력 없음.

- [ ] **Step 10: Commit**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
git add Assets/Scripts/AmbientItemSpawner.cs Assets/Tests/EditMode/AmbientItemSpawnerTests.cs Assets/Scripts/GameManager.cs Assets/Tests/EditMode/GameManagerTests.cs Assets/Editor/ProjectBootstrap.cs Assets/Scenes/Main.unity build.log results.xml playtest-results.xml playtest.log
git commit -m "Add AmbientItemSpawner: periodic random item spawns while exploring

Every 20-40s (randomized, Rng-injectable like SpawnController) a
HealthPotion or BuffItem spawns at a random point within the camera
bounds expanded by AmbientSpawnMargin - items can appear anywhere in
view or just off-screen, unlike enemies which only spawn at the edge.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 9: PlayMode 검증 확장 + 전체 회귀

**Files:**
- Modify: `Assets/Tests/PlayMode/FullPlaytestTests.cs` (카메라 추적, 배경 연속성, ambient 스폰, 보너스 드롭 검증 추가)

**Interfaces:**
- Consumes: 전체 이전 태스크 산출물.

- [ ] **Step 1: 카메라 추적 검증 추가**

"Step 3: movement" 블록(`gm.Player.Tick(1f);` 직후, `yield return null;` 앞)에 추가:

```csharp
            Vector3 camPosAfterMove = Camera.main.transform.position;
            Check(Vector2.Distance(camPosAfterMove, gm.Player.transform.position) < 0.5f,
                $"Camera follows the player after movement (camera={camPosAfterMove}, player={gm.Player.transform.position})");
```

- [ ] **Step 2: 배경 연속성 검증 추가 (장거리 이동 후)**

"Step 6: boss spawn" 블록 바로 앞, 즉 `Debug.Log($"[FullPlaytest] Buffed player for fast-forward survival...` 다음, `yield return FastForwardTo(gm, GameConfig.BossSpawnTime + 1f, "boss spawn (240s)");` 앞에 추가:

```csharp
            // ---- Step 5b: infinite background stays near the camera after a long move ----
            gm.Player.transform.position = new Vector3(5000f, -3000f, 0f); // far beyond one background tile
            yield return null;
            yield return null; // let InfiniteBackground.LateUpdate() recenter
            var backgroundGo = GameObject.Find("Background");
            Check(backgroundGo != null, "Background GameObject still exists after a long jump");
            if (backgroundGo != null)
            {
                float dist = Vector2.Distance(backgroundGo.transform.position, gm.Player.transform.position);
                Check(dist < GameConfig.ArenaWidth, $"Background quad recentered near the player after a long jump (distance={dist})");
            }
            yield return CaptureFull("10_infinite_background_far.png");
            gm.Player.transform.position = Vector3.zero; // back to origin so the rest of the run behaves as before
            yield return null;
```

- [ ] **Step 3: Ambient 아이템 스폰 관측 검증 추가**

같은 위치(Step 2에서 추가한 블록 바로 뒤)에 추가:

```csharp
            // ---- Step 5c: ambient item spawner produces at least one item over enough time ----
            yield return WaitSimSeconds(gm, GameConfig.AmbientItemSpawnMaxInterval + 5f);
            int ambientItemCount = Object.FindObjectsByType<HealthPotion>(FindObjectsSortMode.None).Length +
                                    Object.FindObjectsByType<BuffItem>(FindObjectsSortMode.None).Length;
            Check(ambientItemCount > 0, $"AmbientItemSpawner produced at least one item within {GameConfig.AmbientItemSpawnMaxInterval + 5f}s (found {ambientItemCount})");
```

- [ ] **Step 4: 보너스 드롭 관측 검증 추가**

같은 위치, 바로 뒤에 추가 (여러 번 처치해 8% 확률이 최소 1번은 터지도록 함 — 50회 처치 시 미발생 확률은 약 1.4%로 통계적으로 충분히 낮음):

```csharp
            // ---- Step 5d: bonus item drop chance eventually fires across enough kills ----
            int bonusDropsBefore = Object.FindObjectsByType<HealthPotion>(FindObjectsSortMode.None).Length +
                                    Object.FindObjectsByType<BuffItem>(FindObjectsSortMode.None).Length;
            for (int i = 0; i < 50; i++)
            {
                var farmEnemyGo = new GameObject("PlaytestFarmEnemy",
                    typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(EnemyController));
                var farmEnemy = farmEnemyGo.GetComponent<EnemyController>();
                farmEnemy.Initialize(GameConfig.EnemyBasic, false);
                farmEnemyGo.transform.position = gm.Player.transform.position + Vector3.right * 900f; // far from player: won't be auto-picked-up, so counts survive to the check below

                var farmProjGo = new GameObject("PlaytestFarmProjectile", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Projectile));
                var farmProj = farmProjGo.GetComponent<Projectile>();
                farmProj.Launch(farmEnemyGo.transform.position, Vector2.right, 0f, GameConfig.EnemyBasic.Hp + 100f, 400f);

                gm.OnProjectileHit(farmProj, farmEnemy);
                Object.Destroy(farmProjGo);
            }
            yield return null;
            int bonusDropsAfter = Object.FindObjectsByType<HealthPotion>(FindObjectsSortMode.None).Length +
                                   Object.FindObjectsByType<BuffItem>(FindObjectsSortMode.None).Length;
            Check(bonusDropsAfter > bonusDropsBefore,
                $"At least one bonus item dropped across 50 kills at {GameConfig.BonusItemDropChance:P0} chance (before={bonusDropsBefore}, after={bonusDropsAfter})");
```

- [ ] **Step 5: 재빌드 + 전체 회귀 실행**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-playtest.bat
grep 'result="Failed"' playtest-results.xml
```

기대값: 아무 출력 없음. `10_infinite_background_far.png` 스크린샷을 열어 배경이 끊기지 않고 캐릭터 뒤로 채워져 있는지 육안 확인 (여기서 만약 배경이 눈에 띄게 "점프"하듯 보이면 `BackgroundTiling.OffsetAfterRecenter`의 부호가 반대일 가능성이 높음 — `Assets/Scripts/BackgroundTiling.cs`에서 `currentOffset.x - delta.x / tileWidth`를 `currentOffset.x + delta.x / tileWidth`로 뒤집어 재시도).

- [ ] **Step 6: EditMode도 재확인 (이 태스크는 EditMode 파일을 건드리지 않았지만 전체 회귀 습관 유지)**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat
grep 'result="Failed"' results.xml
```

기대값: 아무 출력 없음.

- [ ] **Step 7: Commit**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
git add Assets/Tests/PlayMode/FullPlaytestTests.cs build.log playtest-results.xml playtest.log
git commit -m "Extend PlayMode playtest: camera follow, infinite background, ambient/bonus item drops

Adds automated checks for the new systems: camera tracks the player
after movement, the background Quad stays recentered near the camera
after a long jump (with a screenshot for visual seam verification),
AmbientItemSpawner produces at least one item within its max interval,
and the 8% bonus drop chance fires at least once across 50 kills.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 10: CHANGELOG / 버전 갱신 + 최종 커밋

**Files:**
- Modify: `CHANGELOG.md`
- Modify: `ProjectSettings/ProjectSettings.asset` (`bundleVersion`)

**Interfaces:** 없음 (문서/메타 작업).

- [ ] **Step 1: 현재 버전 확인**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && grep -n "bundleVersion" ProjectSettings/ProjectSettings.asset
```

- [ ] **Step 2: `bundleVersion`을 MINOR 버전으로 올리기 (신규 기능이므로 PATCH가 아닌 MINOR)**

`ProjectSettings/ProjectSettings.asset`에서 `bundleVersion: 0.2.0`을 `bundleVersion: 0.3.0`으로 변경 (직전 커밋이 0.2.0이라는 전제 — 실제 파일의 현재 값을 Step 1 결과로 확인 후 그 다음 MINOR로 맞춘다).

- [ ] **Step 3: `CHANGELOG.md`에 새 버전 섹션 추가**

`## [Unreleased]` 섹션 바로 아래에 다음을 삽입 (버전 번호는 Step 2에서 확정한 값과 일치시킨다):

```markdown
## [0.3.0] - 2026-07-31

### Added
- Infinite map: camera now follows the player (`CameraFollow`, replacing the fixed-arena
  `CameraFit`), and the background tiles seamlessly forever via a single recentering Quad
  (`InfiniteBackground`/`BackgroundTiling`) instead of one static 1600x900 image.
- Two new pickups: `HealthPotion` (heals 25% of max HP) and `BuffItem` (2x fire rate for
  12s, refreshes duration on re-pickup without stacking magnitude). Drop from enemy kills
  (8% bonus chance alongside the always-dropped XP gem) and from `AmbientItemSpawner`
  (random spawn every 20-40s while exploring).
- Enemy spawning now recenters on the camera every frame instead of a fixed arena edge
  (`GameManager.BoundsAroundCamera()`), keeping the existing spawn-distance tuning intact.

### Changed
- `PlayerController.Tick` dropped its `Rect bounds` parameter - movement is no longer
  clamped to a fixed arena.

### Known limitations
- `HealthPotion`/`BuffItem` sprites are PIL-drawn placeholders (same treatment as
  `arrow.png`/`medal.png`), not AI-generated art - swap pending HuggingFace ZeroGPU quota.
- Win/boss timers (300s Win, 240s boss spawn) are unchanged from the fixed-arena version -
  splitting "bounded/Win" vs "infinite/survival" into separate modes is future work.
- Background tiling repeats a 2x2 grid of the same 1600x900 image, so the moon/skyline can
  appear more than once on screen at once - accepted trade-off per the original request.

### Process notes
- Design spec: `docs/superpowers/specs/2026-07-30-infinite-map-and-loot-design.md`
  (refined via `/spec-review` interview - see spec's git history for the issues surfaced)
- Implementation plan: `docs/superpowers/plans/2026-07-30-infinite-map-and-loot.md`
```

- [ ] **Step 4: 최종 전체 회귀 (커밋 전 마지막 확인)**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity" && ./run-tests.bat && ./run-playtest.bat
grep 'result="Failed"' results.xml playtest-results.xml
```

기대값: 아무 출력 없음.

- [ ] **Step 5: Commit + Push**

```bash
cd "C:\Users\ASUS\Desktop\base\game-unity"
git add CHANGELOG.md ProjectSettings/ProjectSettings.asset
git commit -m "Bump to 0.3.0 and document the infinite map + loot feature

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
git push origin main
```

---

## Self-Review Notes (완료됨, 참고용)

- **스펙 커버리지:** 카메라 follow(Task 2), 배경 타일링(Task 3), 스폰 경계 카메라 기준화(Task 4), 버프 시스템+바닥값 clamp(Task 5), 신규 아이템+PIL 아트(Task 6), 보너스 드롭+정리(Task 7), ambient 스폰(Task 8), PlayMode 검증(Task 9), CHANGELOG/버전(Task 10) — 스펙의 8개 컴포넌트 섹션 전부 태스크로 매핑됨.
- **검토 중 발견해 스펙에 없던 추가 수정:** `FullPlaytestTests.cs`의 5번째 `PlayerController.Tick(dt, bounds)` 호출부(Task 1)와 `CameraFit` 의존 코드 전체(Task 2의 letterboxing 검증부) — 스펙 작성 시점엔 몰랐던 실제 코드 의존성으로, 플랜 작성 중 전체 파일을 다시 읽으며 발견해 반영함.
- **플레이스홀더 스캔:** 모든 스텝에 실제 코드/명령어/파일 경로를 채워 넣었음 — "적절히 처리", "TODO" 등 모호한 문구 없음.
- **타입/시그니처 일관성:** `PlayerController.Tick(float dt)`, `GameManager.BoundsAroundCamera()`(internal), `GameManager.Rng`/`AmbientItemSpawner.Rng`(둘 다 `Func<float>`), `UpgradeEffects.EffectiveFireIntervalWithBuff(stacks, multiplier)` — 태스크 간 참조가 전부 동일한 이름/타입으로 일치함.
