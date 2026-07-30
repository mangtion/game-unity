# 무한 맵 + 랜덤 아이템 설계

## 배경 (Why)

현재 게임은 고정 크기 아레나(`GameConfig.ArenaWidth`×`ArenaHeight` = 1600×900)를 카메라가 항상 프레임 전체에 담는 방식이다(`CameraFit`). 플레이어 이동은 이 아레나 경계로 clamp되고(`PlayerController.Tick(dt, bounds)`), 적 스폰도 이 고정 경계의 가장자리에서만 발생한다(`SpawnController`).

사용자는 이를 "무한 맵"으로 바꾸고 싶어한다: 카메라가 플레이어를 따라다니며, 배경 이미지가 계속 반복 렌더링되고, 탐색 중 아이템(체력 회복/임시 버프)이 랜덤하게 나타나며, 적 처치 시에도 기존 전공훈장(XP젠) 외에 이런 아이템이 낮은 확률로 추가 드롭된다.

승리 조건(300초 생존 시 Win, 240초 보스 스폰)은 이번 스펙에서 **건드리지 않는다** — 사용자가 향후 "고정 아레나/Win 모드"와 "무한 생존 모드"를 별도 모드로 분리할 계획이며, 지금은 무한 맵 위에서도 기존 시간 기반 Win 로직이 그대로 동작해야 한다.

## 범위 (Scope)

**포함:**
- 카메라가 플레이어를 따라다니는 방식으로 전환 (`CameraFit` 교체)
- 배경(`background.png`)이 끊김 없이 계속 반복되도록 타일링
- 플레이어 이동의 고정 아레나 clamp 제거
- 적 스폰 기준을 "고정 아레나 가장자리" → "카메라 주변 가장자리"로 변경
- 신규 아이템 2종: `HealthPotion`(체력 회복), `BuffItem`(임시 공격속도 강화)
- 적 처치 시 낮은 확률로 위 아이템 추가 드롭 (기존 XP젠 100% 드롭은 유지)
- 맵 탐색 중 주기적으로 아이템이 카메라 주변에 랜덤 스폰되는 `AmbientItemSpawner`
- 관련 EditMode/PlayMode 테스트 추가 및 기존 아레나-clamp 전제 테스트 수정

**제외 (Out of scope, 별도 스펙으로 분리):**
- "고정 아레나/Win 모드" vs "무한 생존 모드" 두 모드 분리 — 이번엔 무한 맵 위에 기존 300초 Win 로직을 그대로 얹는다
- 버프 종류 다양화 (이번엔 공격속도 버프 1종만) — 추가 버프는 후속 스펙
- 진짜 무한(좌표 정밀도 걱정 없는 영구 플레이) — 아래 "배경 타일링" 절 참고, 이번 구현은 세션 길이(수 분~수십 분) 동안 아무 문제 없이 동작하는 수준을 목표로 함
- 아이템 드롭율/스탯 밸런싱 정교화 — 첫 구현치를 아래에 명시하되, 플레이테스트 후 조정은 별도 작업으로 취급

## 아키텍처 개요

```
GameManager.Update()
  ├─ bounds = RectAroundCamera(CameraTransform.position, ArenaWidth, ArenaHeight)   (기존 고정 bounds 대체)
  ├─ Spawner.Tick(dt, elapsed, bounds, transform)              (기존 그대로, bounds만 카메라 기준)
  ├─ AmbientItemSpawner.Tick(dt, expandedBounds, transform)    (신규)
  ├─ PlayerWeapon.FireInterval = Mathf.Max(
  │      UpgradeEffects.EffectiveFireInterval(stacks) * Player.BuffFireIntervalMultiplier,
  │      UpgradeEffects.MinFireInterval)                       (버프 배율 합성 지점, 기존 142번줄 확장)
  └─ Player.Tick(dt)                                           (bounds clamp 제거, 시그니처 변경. 버프 타이머 감소도 내부에서 처리)

CameraFollow (신규, Main Camera에 부착)
  └─ LateUpdate: transform.position ← Player.transform.position (즉시 추적)

InfiniteBackground (신규, 배경 Quad에 부착)
  └─ LateUpdate: 카메라가 타일 절반 이상 벗어나면 Quad를 재배치 + material.mainTextureOffset 보정
```

**참조 배선**: `GameManager`에 `public Transform CameraTransform` 필드를 추가하고, `Player`/`Spawner`와 동일하게 `ProjectBootstrap.BuildMainScene`에서 Main Camera의 Transform을 연결한다. (검토 결과: 기존 코드에 Camera 참조가 전혀 없었고, 스펙 초안이 이 배선을 누락하고 있었음 — 싱글톤 패턴 대신 명시적 필드 주입을 택함, 기존 `Player`/`PlayerWeapon`/`Spawner` 필드와 일관성 유지)

## 컴포넌트별 설계

### 1. 카메라: `CameraFollow.cs` (신규, `CameraFit.cs` 대체)

- `LateUpdate()`에서 `transform.position = new Vector3(player.position.x, player.position.y, -10f)` — 스무딩 없이 즉시 추적(트윈스틱 서바이버 장르 표준, 조작감 지연 방지)
- `orthographicSize`는 `GameConfig.ArenaHeight / 2f`로 고정 — 기존과 동일한 줌 레벨 유지. 기존 `CameraFit`의 "좁은 화면비에서 orthographicSize를 키워 아레나 전체 폭을 보장"하는 로직은 더 이상 의미가 없으므로 제거(더 이상 "꼭 보여줘야 할 고정 폭"이 없음)
- `Player` 참조가 필요하므로 `Awake()`에서 `GameManager`를 통해 주입받거나 Inspector에서 직접 연결 (기존 `UIController.Player` 배선 패턴과 동일하게 `ProjectBootstrap.BuildUI`/`BuildMainScene`에서 연결)
- 기존 `CameraFit.cs`, `CameraFit`을 참조하는 코드(`ProjectBootstrap.cs`의 Main Camera 생성부)를 `CameraFollow`로 교체

### 2. 배경 타일링: `InfiniteBackground.cs` (신규, Background Quad에 부착)

방 안에서 논의한 "거대한 단일 쿼드 + 텍스처 반복" 방식을 정밀화한다:

- Background Quad 크기는 크게 키우지 않는다 — **3200×1800** (배경 이미지 1600×900의 2×2 타일)로 고정한다. 대신 **매 프레임 카메라와의 거리를 체크해 Quad 자체를 재배치**한다
- 머티리얼의 `mainTexture.wrapMode = TextureWrapMode.Repeat`, `mainTextureScale = (2, 2)`로 설정해 텍스처가 2×2로 반복되도록 함
- `LateUpdate()`: 카메라 위치가 Quad 중심에서 `타일크기/2`를 넘게 벗어나면, Quad의 `transform.position`을 카메라 위치로 스냅 이동시키고, 이동한 거리만큼 `material.mainTextureOffset`을 보정해 시각적으로 이음매 없이 이어지도록 함 (오프셋 보정값 = 이동 거리 ÷ 타일 실제 크기, 소수부만 사용)
- 이 방식은 Quad를 딱 1개만 유지하면서도(9분할 그리드 불필요) 좌표가 무한정 커지지 않아(주기적으로 원점 근처로 재배치되는 효과) 부동소수점 정밀도 문제도 회피함
- **오픈 아이템**: 텍스처 반복 배율이 낮아(위 예시 기준 2×2) 달/마을 스카이라인이 비교적 자주 반복되어 보일 수 있음 — 사용자가 "같은 이미지 지속 렌더링"을 명시적으로 원했으므로 허용하되, 반복 주기가 너무 짧아 어색하면 Quad/타일 배율을 키우는 것으로 조정 (아트 재작업 불필요, 상수 조정만으로 해결)

### 3. 플레이어 이동: `PlayerController.cs` 수정

- `Tick(float dt, Rect bounds)` → `Tick(float dt)`로 시그니처 변경, 내부의 아레나 clamp 로직(`Mathf.Clamp(pos.x, bounds.xMin, ...)` 등)을 제거
- 호출부 `GameManager.cs`의 `Player.Tick(dt, bounds)` → `Player.Tick(dt)`로 수정
- **영향받는 기존 테스트**: `Assets/Tests/EditMode/PlayerControllerTests.cs`의 `bounds` 인자를 쓰는 모든 테스트(`Tick_ClampsToBounds` 포함 4곳)를 시그니처 변경에 맞춰 수정. `Tick_ClampsToBounds`는 더 이상 성립하지 않는 동작이므로 삭제

### 4. 적 스폰: `GameManager.cs` 수정 (`SpawnController.cs`는 변경 없음)

- 기존 고정 `bounds` 필드(`new Rect(-ArenaWidth/2, -ArenaHeight/2, ArenaWidth, ArenaHeight)`, `Awake`/`Start`에서 1회 계산)를 매 프레임 카메라 위치 기준으로 재계산하는 것으로 변경:
  ```csharp
  Rect BoundsAroundCamera() => new Rect(
      cameraTransform.position.x - GameConfig.ArenaWidth / 2f,
      cameraTransform.position.y - GameConfig.ArenaHeight / 2f,
      GameConfig.ArenaWidth, GameConfig.ArenaHeight);
  ```
- `SpawnController.Tick(dt, elapsed, bounds, parent)` 호출부만 이 값을 넘기도록 수정 — `SpawnController`/`SpawnCurve`/`EdgeOffset` 등 스폰 로직 자체는 무수정
- 폭/높이 상수(`ArenaWidth`/`ArenaHeight`)는 "화면에 실제로 보이는 크기"가 아니라 "적이 화면 밖 얼마나 먼 가장자리에서 스폰될지"를 결정하는 튜닝값으로 재해석됨 — 화면비와 무관하게 기존 밸런스(스폰 거리감)를 그대로 유지하기 위해 값 자체는 바꾸지 않음

### 5. 신규 아이템: `HealthPotion.cs`, `BuffItem.cs`

`XpGem.cs`와 동일하게 단순한 데이터 컴포넌트로 만든다:

```csharp
public class HealthPotion : MonoBehaviour
{
    public float HealAmount; // 픽업 시 GameManager가 Player.MaxHp * HealFraction으로 설정
}

public class BuffItem : MonoBehaviour
{
    public float FireIntervalMultiplier = 0.5f; // 공격속도 2배 = 발사 간격 절반
    public float Duration = 12f; // 초
}
```

- `GameConfig`에 상수 추가: `HealFraction = 0.25f`(최대체력의 25% 회복 — 체력 업그레이드로 MaxHp가 커져도 항상 의미 있는 회복량이 되도록 비율 기반), `BuffFireIntervalMultiplier = 0.5f`, `BuffDuration = 12f`

- **임시 버프 적용 방식 (검토로 확정, 초안의 모호한 서술 대체)**: `PlayerController`에 `float BuffTimeRemaining`, `float BuffFireIntervalMultiplier`(기본 1.0) 필드 추가.
  - **배선 지점**: `GameManager.Update()`가 매 프레임 `PlayerWeapon.FireInterval = UpgradeEffects.EffectiveFireInterval(stacks)`로 덮어쓰고 있으므로(영구 업그레이드만 반영, 기존 142번줄), 버프는 반드시 **이 대입문 자체**에 합성해야 한다. 다른 지점(예: `Weapon.cs` 내부)에 버프 배율을 넣으면 매 프레임 이 대입문에 덮어써져 버프가 즉시 사라진다:
    ```csharp
    PlayerWeapon.FireInterval = Mathf.Max(
        UpgradeEffects.EffectiveFireInterval(Player.UpgradeStacks) * Player.BuffFireIntervalMultiplier,
        UpgradeEffects.MinFireInterval);
    ```
  - `UpgradeEffects.MinFireInterval`은 현재 `private const`이므로 `public const`으로 변경 (GameManager에서 재사용하기 위함)
  - **바닥값 재적용 이유**: `EffectiveFireInterval`은 이미 0.05초로 clamp된 값이라, 여기에 버프 배율(0.5배)을 또 곱하면 후반부(fireRate 업그레이드를 많이 찍은 상태)에 0.025초까지 내려갈 수 있음 — 의도한 바닥보다 빨라지는 것을 막기 위해 버프 적용 후에도 동일한 `MinFireInterval`로 다시 clamp
  - `PlayerController.Tick(dt)`에서 `BuffTimeRemaining`을 감소시키다 0 이하가 되면 `BuffFireIntervalMultiplier`를 1.0으로 되돌림
  - **버프 중복 픽업**: 버프가 이미 적용된 상태에서 `BuffItem`을 또 주우면 지속시간만 `BuffDuration`(12초)으로 리프레시한다 (배율은 중첩되지 않음 — 항상 2배로 고정, 여러 개 주워도 4배가 되지 않도록)
  - **재시작 시 초기화**: `PlayerController.ResetState()`에 `BuffTimeRemaining = 0f; BuffFireIntervalMultiplier = 1f;`를 추가해, 새 게임 시작 시 이전 판의 버프가 이어지지 않도록 함 (기존 `maxHp`/`hp`/`upgradeStacks` 초기화와 동일한 위치)

- **픽업 반경**: `HealthPotion`/`BuffItem` 모두 XP젠과 동일한 `UpgradeEffects.EffectivePickupRadius(Player.UpgradeStacks)`를 사용한다 (별도 상수 불필요 — pickupRadius 업그레이드를 찍은 플레이어는 모든 픽업에 일관되게 혜택을 받음)

- 픽업(마그넷) 처리는 기존 XP젠과 동일한 `GameManager`의 픽업 루프 패턴(반경 체크 → 효과 적용 → `Destroy`)을 그대로 재사용해 `HealthPotion`/`BuffItem` 케이스를 추가

- **스프라이트**: 이번 스펙에서 처음 등장하는 아이템이라 전용 아트가 없다. `arrow.png`/`medal.png`를 만들 때와 동일하게 **PIL로 직접 그린 플레이스홀더**(단순 도형, 체력=붉은 십자/물약 모양, 버프=번개 아이콘 등)로 우선 제작하고, 나중에 HuggingFace ZeroGPU 할당량이 풀리면 그래픽노벨풍 AI 아트로 교체한다 (기존 `arrow.png`/`medal.png`와 같은 "Known limitations" 취급 — CHANGELOG에 동일하게 기록)

### 6. 적 처치 시 추가 드롭 (`GameManager.cs`)

- 기존: 적 처치 시 XP젠 100% 드롭 (변경 없음)
- 신규: 별도 확률 굴림으로 `GameConfig.BonusItemDropChance = 0.08f`(8%) 확률로 보너스 아이템 1개 추가 드롭, 당첨 시 50/50으로 `HealthPotion`/`BuffItem` 중 하나를 적 사망 위치에 생성 (XP젠과 겹쳐도 무방 — 기존 게임의 파티클/픽업 밀도 수준에서 허용 범위)

### 7. 맵 탐색 중 랜덤 스폰: `AmbientItemSpawner.cs` (신규)

`SpawnController`와 같은 구조의 새 컴포넌트. 특히 `SpawnController.Rng`(`public Func<float> Rng = () => UnityEngine.Random.value;`)와 동일한 주입 패턴을 그대로 따른다 — 기존 `SpawnControllerTests`처럼 EditMode 테스트에서 결정론적 시드로 검증하기 위함 (검토로 확정: 초안엔 이 패턴 언급이 없었음):

```csharp
public class AmbientItemSpawner : MonoBehaviour
{
    public Func<float> Rng = () => UnityEngine.Random.value;
    // ...
}
```

- 다음 스폰까지 대기 시간을 `Rng()`로 `Random.Range(20f, 40f)`에 해당하는 값을 뽑음 (요청하신 "수십 초에 하나" 밀도)
- 스폰 위치: 카메라 사각형을 사방으로 `AmbientSpawnMargin`(예: 150유닛)만큼 확장한 사각형 내부의 임의의 점 — 적처럼 가장자리에만 스폰할 필요는 없음(정적 픽업이라 화면 안쪽에 나타나도 자연스러움). 화면 안쪽에 바로 나타나기보단 약간 바깥쪽까지 포함해 "이동해서 찾아가는" 느낌을 살림
- 스폰 시 `HealthPotion`/`BuffItem` 중 50/50 랜덤 선택 (같은 `Rng()` 사용)
- `GameManager.Update()`에서 `Spawner.Tick(...)` 옆에 나란히 호출

### 8. 아이템 인스턴스 추적 및 재시작 시 정리 (`GameManager.cs`)

기존 `xpGems`(`readonly List<GameObject> xpGems`)와 동일한 패턴으로, 6번(적 드롭)과 7번(ambient 스폰) 양쪽에서 생성되는 `HealthPotion`/`BuffItem` 인스턴스를 추적할 리스트를 추가한다 (검토로 확정: 초안은 이 추적/정리 로직이 누락되어 있었음):

```csharp
readonly List<GameObject> worldItems = new List<GameObject>(); // HealthPotion/BuffItem 공용
```

- 생성 시 `worldItems.Add(...)`, 픽업 루프(`CollectGems_ForTests`에 통합하거나 병렬 메서드 추가)에서 소비 시 리스트에서 제거
- `StartGame()`의 기존 정리 블록(`foreach (var g in xpGems) ... xpGems.Clear();`)과 동일하게 `worldItems`도 순회하며 `SafeDestroy` 후 `Clear()` — 별도의 시간 기반 소멸 타이머는 두지 않는다 (세션이 어차피 300초 Win으로 제한되어 있어 아이템 누적이 자연스럽게 bound됨, 검토로 확정)

## 데이터 흐름 요약

1. `CameraFollow.LateUpdate()`가 매 프레임 카메라 위치를 플레이어 위치로 갱신
2. `GameManager.Update()`가 `CameraTransform.position`을 읽어 `BoundsAroundCamera()` 계산
3. 이 bounds를 `SpawnController.Tick`(적)과 `AmbientItemSpawner.Tick`(아이템, margin 포함한 확장 버전)에 전달
4. 적 처치 시 `GameManager`가 XP젠(100%) + 보너스 아이템(8%, 조건부, `worldItems`에 추가)을 인스턴스화
5. 매 프레임 `GameManager`가 `PlayerWeapon.FireInterval`을 영구 업그레이드 배율 × 버프 배율로 재계산(바닥값 재clamp 포함, 5절 참고)
6. 매 프레임 `GameManager`의 픽업 루프가 플레이어 반경(모든 아이템 공통 `EffectivePickupRadius`) 내 XP젠/`worldItems`(HealthPotion/BuffItem)를 모두 체크해 효과 적용 후 제거
7. `PlayerController.Tick(dt)`가 clamp 없이 이동 처리, 버프 잔여시간 감소도 여기서 처리
8. `StartGame()`이 `xpGems`와 동일하게 `worldItems`를 정리(destroy + clear)하고 `PlayerController.ResetState()`가 버프 상태를 초기화

## 테스트 / 검증

**EditMode (신규):**
- `BoundsAroundCamera`류 함수: 카메라 위치가 원점이 아닐 때도 올바른 Rect를 반환하는지
- `AmbientItemSpawner`: `Rng`를 고정 시드로 주입해 스폰 간격이 20~40초 범위 안에서 결정론적으로 나오는지, 스폰 위치가 확장된 카메라 사각형 안에 들어오는지 (기존 `SpawnControllerTests`와 동일한 `Rng` 주입 패턴 재사용)
- `BuffItem` 적용/만료: 버프 적용 시 발사 간격이 즉시 줄어들고, `Duration` 경과 후 정확히 원래 값(영구 업그레이드 배율만 반영된 값)으로 복귀하는지
- `BuffItem` 바닥값: fireRate 업그레이드를 최대로 찍은 상태에서 버프를 적용해도 `PlayerWeapon.FireInterval`이 `UpgradeEffects.MinFireInterval` 밑으로 내려가지 않는지
- `BuffItem` 중복 픽업: 버프 적용 중 다시 주우면 배율은 그대로(2배 유지)이고 `BuffTimeRemaining`만 `BuffDuration`으로 리셋되는지
- `HealthPotion`: `Player.MaxHp`의 `HealFraction`만큼 회복하고 `MaxHp`를 넘지 않는지 (기존 `PlayerControllerTests`의 체력 관련 테스트 패턴 참고)
- `PlayerController.ResetState()`: 버프 필드(`BuffTimeRemaining`/`BuffFireIntervalMultiplier`)가 초기값으로 돌아오는지
- `worldItems` 정리: `StartGame()` 호출 후 이전 판에서 생성된 `HealthPotion`/`BuffItem`이 씬에 남아있지 않은지
- 수정: `PlayerControllerTests.cs`의 `Tick(dt, bounds)` 호출 4곳을 `Tick(dt)`로, `Tick_ClampsToBounds`는 삭제

**PlayMode (`FullPlaytestTests.cs` 확장):**
- 플레이어를 일정 시간 이동시킨 뒤 카메라 위치가 플레이어 위치를 따라갔는지 확인
- 장거리 이동(예: 수천 유닛) 후에도 배경 Quad가 여전히 카메라 근처에 존재하고(재배치 로직이 동작), 화면에 빈 공간(배경 없음)이 노출되지 않는지 확인
- 시간을 충분히 흘려보내(`WaitSimSeconds` 활용) `AmbientItemSpawner`가 최소 1개 이상 아이템을 스폰하는지 확인
- 적을 다수 처치시켜(기존 boss/dummy enemy 패턴 재사용) 낮은 확률이라도 보너스 아이템이 최소 1개는 드롭되는지 확인 (충분히 많은 처치 횟수로 확률적 실패 가능성을 낮춤)

## 리스크

- **기존 테스트 회귀**: `PlayerController.Tick` 시그니처 변경으로 최소 4개 EditMode 테스트가 컴파일 에러 상태가 됨 — 구현 계획에서 반드시 첫 단계로 처리해야 다른 테스트 실행 자체가 막히지 않음
- **배경 반복 어색함**: 타일 배율이 낮으면 달/스카이라인이 눈에 띄게 자주 반복될 수 있음 — 상수 조정으로 완화 가능한 수준이므로 큰 리스크는 아니나, 플레이테스트 스크린샷으로 육안 확인 필요
- **카메라 즉시 추적의 스냅감**: 스무딩 없이 즉시 카메라가 플레이어를 따라가면 다소 딱딱하게 느껴질 수 있음 — 1차 구현은 즉시 추적으로 가고, 체감이 나쁘면 후속 조정(Lerp)으로 분리 가능
- **PIL 플레이스홀더 아이템 아트**: 체력/버프 아이템이 정식 AI 아트가 아니라 임시 도형이라 다른 캐릭터들(그래픽노벨풍)과 톤이 어긋날 수 있음 — `arrow.png`/`medal.png`와 동일하게 CHANGELOG의 Known limitations에 기록하고 후속 아트 교체 작업으로 넘김

> [!WARNING] OPEN ITEM: 카메라 follow 대상이 `Player`(플레이어 GameObject) 자체인지, 아니면 플레이어의 시각적 중심(스프라이트 중심)인지는 동일하다고 가정했다 — `PlayerController`가 붙은 GameObject의 `transform.position`이 곧 콜라이더/스프라이트의 기준점이므로 별도 오프셋 계산은 불필요하다고 보지만, 구현 중 실제로 화면 중앙에 플레이어가 정확히 오는지 육안 확인 필요.
