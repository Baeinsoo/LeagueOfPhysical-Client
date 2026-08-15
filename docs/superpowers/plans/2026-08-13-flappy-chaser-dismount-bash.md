# 플래피 추격자 · 낙마 · 대시 박치기 구현 플랜

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 레이스에 "추격자와의 거리"라는 단일 통화를 도입해, 대시·낙마·박치기가 모두 생존이라는 하나의 숫자로 수렴하게 만든다.

**Architecture:** 순수 로직(속도 곡선·위치 규칙·생존 판정)은 엔진 비의존 asmdef로 빼서 EditMode 유닛 테스트로 검증하고, MonoBehaviour는 그 로직을 호출하는 얇은 배선만 담당한다. 코어 물리(플랩·중력·전진·대시)는 손대지 않는다 — 손맛이 거기서 나오므로 추가 규칙만 얹는다.

**Tech Stack:** Unity 6 (URP, orthographic 2D 룩), New Input System, uGUI 런타임 생성 HUD, NUnit EditMode 테스트, UnityMCP(에디터 자동화).

**Spec:** `docs/superpowers/specs/2026-08-05-flappy-multiplayer-concept-design.md`

---

## Global Constraints

- **코어 물리 값을 바꾸지 않는다.** 씬 인스턴스 직렬화 값이 진실원본이다: `forwardSpeed 11`, `flapImpulse 23`, `gravity 70`, `maxFall 30`, `dashMult 2`, `dashDur 0.2`, `ceilingY 20`, `groundY -80`, `ghostTime 0.5`, `invulnTime 0.5`.
- **⚠️ 코드 기본값과 씬 인스턴스 값이 다르다.** `FlappyPlayer.cs`의 필드 기본값은 `forwardSpeed 7` 등 옛 값이고, 씬의 직렬화 값이 이긴다. 인스펙터 값을 바꾸려면 코드 기본값만 고치면 안 되고 씬 인스턴스를 직접 세팅한 뒤 저장해야 한다.
- **직렬화 필드 이름을 바꾸지 않는다.** `ghostTime`/`invulnTime`은 의미가 "낙마"로 바뀌지만 이름은 유지한다 — 바꾸면 씬의 값이 날아간다. 의미는 주석으로 설명한다.
- **추격자 최고 속도 < 11.** 완벽하게 난 사람은 끝까지 잡히면 안 된다. 이 제약을 깨는 튜닝은 스펙 위반이다.
- **낙마 트리거는 둘뿐이다.** 맵 장애물 충돌, 대시 박치기. 코리도 상하 경계는 막기만 하고 페널티가 없다.
- **⚠️ 플레이 모드에서 한 씬 편집은 정지하면 전부 되돌아간다.** 씬 편집·저장은 반드시 에디트 모드에서 한다. 플레이 모드는 관찰 전용.
- **⚠️ 새로 만든 `.cs`는 `refresh_unity(scope="all")` 후에야 `execute_code`가 타입을 인식한다.** `scope="scripts"`는 신규 파일을 임포트하지 않는다.
- **⚠️ 트리거 콜라이더 오버랩은 `QueryTriggerInteraction.Collide`를 명시해야 한다.** 프로젝트 전역 설정이 Ignore라 기본값이면 장애물을 감지하지 못한다.
- **⚠️ UnityMCP 호출마다 `unity_instance`를 명시한다.** `mcpforunity://instances`에서 `LeagueOfPhysical-Client`의 전체 id(`Name@hash`)를 읽어 매 호출에 넘긴다. 서버 인스턴스로 라우팅되면 안 된다.
- **씬:** `Assets/Art/Scenes/FlappyRace.unity`. 코스 길이 `startX -3` → `finishX 632`. 카메라는 orthographic size 16.

---

## File Structure

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/FlappyRaceSlice/Logic/FlappyRaceSlice.Logic.asmdef` | 엔진 비의존 순수 로직 어셈블리 (테스트 가능) |
| `Assets/Scripts/FlappyRaceSlice/Logic/FlappyChaserCurve.cs` | 추격자 속도 곡선 (초기속도 → 선형 가속 → 상한) |
| `Assets/Scripts/FlappyRaceSlice/Logic/FlappyChaserPosition.cs` | 추격자 위치 규칙 (가속선 vs 선두−화면폭) |
| `Assets/Scripts/FlappyRaceSlice/Logic/FlappyChaserOutcome.cs` | 충돌 분포 → 완주/탈락 판정 (튜닝용) |
| `Assets/Tests/EditMode/FlappyRaceSlice.Tests.EditMode.asmdef` | EditMode 테스트 어셈블리 |
| `Assets/Tests/EditMode/FlappyChaserCurveTests.cs` | 곡선·위치 규칙 테스트 |
| `Assets/Tests/EditMode/FlappyChaserOutcomeTests.cs` | 생존 판정 테스트 |
| `Assets/Scripts/FlappyRaceSlice/FlappyChaser.cs` | 추격자 러너 — 위치 갱신·가시화·탈락 판정 |
| `Assets/Scripts/FlappyRaceSlice/FlappyBoundary.cs` | 코리도 경계 마커 (막기만, 페널티 없음) |
| `Assets/Scripts/FlappyRaceSlice/IFlappyRacer.cs` | 플레이어/봇 공통 인터페이스 (대시 여부·속도·낙마·넉백) |
| `Assets/Scripts/FlappyRaceSlice/FlappyDismountFx.cs` | 낙마 연출 (라이더가 튕겨 날아갔다 복귀) |
| `Assets/Editor/FlappyBoundaryMarker.cs` | 경계 일괄 마킹 + 장애물 인벤토리 출력 |
| `Assets/Scripts/FlappyRaceSlice/FlappyPlayer.cs` (수정) | 낙마 이벤트·인터페이스 구현·넉백 |
| `Assets/Scripts/FlappyRaceSlice/FlappyPacer.cs` (수정) | 동일 규칙 적용 |
| `Assets/Scripts/FlappyRaceSlice/FlappyBird.cs` (수정) | 박치기 판정 + 운동량 튕김 |
| `Assets/Scripts/FlappyRaceSlice/FlappyCameraFollow.cs` (수정) | 선두 추종 |
| `Assets/Scripts/FlappyRaceSlice/FlappyHUD.cs` (수정) | 목숨 게이지 |
| `Assets/Scripts/FlappyRaceSlice/FlappySimJudge.cs` (수정) | 추격자 튜닝 리포트 |

---

## Task 1: 추격자 순수 로직 + 테스트 하네스

이 프로젝트에는 아직 EditMode 테스트 어셈블리가 없다. 이 태스크가 그것을 만들고, 추격자 로직 중 엔진이 필요 없는 부분을 거기에 넣어 유닛 테스트로 못 박는다. 이후 태스크의 MonoBehaviour는 이 로직을 호출만 한다.

**Files:**
- Create: `Assets/Scripts/FlappyRaceSlice/Logic/FlappyRaceSlice.Logic.asmdef`
- Create: `Assets/Scripts/FlappyRaceSlice/Logic/FlappyChaserCurve.cs`
- Create: `Assets/Scripts/FlappyRaceSlice/Logic/FlappyChaserPosition.cs`
- Create: `Assets/Tests/EditMode/FlappyRaceSlice.Tests.EditMode.asmdef`
- Test: `Assets/Tests/EditMode/FlappyChaserCurveTests.cs`

**Interfaces:**
- Consumes: 없음 (첫 태스크)
- Produces:
  - `FlappyRace.FlappyChaserCurve` — 필드 `float InitialSpeed, Acceleration, MaxSpeed`; 메서드 `float SpeedAt(float elapsed)`, `float PressureOnsetTime()`
  - `FlappyRace.FlappyChaserPosition` — `static float Resolve(float curveX, float leaderX, float screenWidth)`

- [ ] **Step 1: 순수 로직 어셈블리 정의 생성**

`Assets/Scripts/FlappyRaceSlice/Logic/FlappyRaceSlice.Logic.asmdef`:

```json
{
    "name": "FlappyRaceSlice.Logic",
    "rootNamespace": "FlappyRace",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

`noEngineReferences: true`라 UnityEngine 없이 순수 C#으로 컴파일된다. `autoReferenced: true`라 `Assembly-CSharp`(= FlappyRaceSlice의 MonoBehaviour들)이 자동으로 이 어셈블리를 참조한다. 방향이 반대라는 점이 중요하다 — asmdef는 `Assembly-CSharp`을 참조할 수 없으므로, 테스트하고 싶은 로직은 반드시 asmdef 쪽으로 내려와야 한다.

- [ ] **Step 2: 테스트 어셈블리 정의 생성**

`Assets/Tests/EditMode/FlappyRaceSlice.Tests.EditMode.asmdef`:

```json
{
    "name": "FlappyRaceSlice.Tests.EditMode",
    "rootNamespace": "",
    "references": [
        "FlappyRaceSlice.Logic",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: 실패하는 테스트 작성**

`Assets/Tests/EditMode/FlappyChaserCurveTests.cs`:

```csharp
using NUnit.Framework;
using FlappyRace;

public class FlappyChaserCurveTests
{
    const float PlayerForwardSpeed = 11f;   // 씬 직렬화 값 — 추격자는 절대 이걸 넘지 않는다

    static FlappyChaserCurve MakeDefault() =>
        new FlappyChaserCurve { InitialSpeed = 7f, Acceleration = 0.075f, MaxSpeed = 10f };

    [Test]
    public void 시작_시점에는_초기속도다()
    {
        var c = MakeDefault();
        Assert.AreEqual(7f, c.SpeedAt(0f), 1e-4f);
    }

    [Test]
    public void 시간이_지나면_선형으로_빨라진다()
    {
        var c = MakeDefault();
        Assert.AreEqual(7f + 0.075f * 10f, c.SpeedAt(10f), 1e-4f);
    }

    [Test]
    public void 상한을_넘지_않는다()
    {
        var c = MakeDefault();
        Assert.AreEqual(10f, c.SpeedAt(10000f), 1e-4f);
    }

    [Test]
    public void 어떤_시각에도_플레이어_전진속도를_넘지_않는다()
    {
        var c = MakeDefault();
        for (float t = 0f; t <= 300f; t += 0.5f)
            Assert.Less(c.SpeedAt(t), PlayerForwardSpeed, $"t={t}에서 추격자가 플레이어보다 빠르다");
    }

    [Test]
    public void 압박_전환점은_상한_도달_시각이다()
    {
        var c = MakeDefault();
        Assert.AreEqual(40f, c.PressureOnsetTime(), 1e-3f);
        Assert.AreEqual(c.MaxSpeed, c.SpeedAt(c.PressureOnsetTime()), 1e-4f);
    }

    [Test]
    public void 가속이_0이면_전환점이_무한이다()
    {
        var c = new FlappyChaserCurve { InitialSpeed = 7f, Acceleration = 0f, MaxSpeed = 10f };
        Assert.IsTrue(float.IsPositiveInfinity(c.PressureOnsetTime()));
    }

    [Test]
    public void 위치는_가속선과_선두뒤_중_앞선_것이다()
    {
        // 가속선이 앞선 경우
        Assert.AreEqual(100f, FlappyChaserPosition.Resolve(100f, 120f, 57f), 1e-4f);
        // 선두가 멀리 달아난 경우 — 화면 한 폭 뒤가 앞선다
        Assert.AreEqual(143f, FlappyChaserPosition.Resolve(100f, 200f, 57f), 1e-4f);
    }

    [Test]
    public void 선두와의_격차는_화면폭을_넘지_않는다()
    {
        float leader = 500f, screen = 57f;
        float chaser = FlappyChaserPosition.Resolve(0f, leader, screen);
        Assert.LessOrEqual(leader - chaser, screen + 1e-4f);
    }
}
```

- [ ] **Step 4: 테스트가 실패하는지 확인**

Unity Editor에서 **Window > General > Test Runner > EditMode > Run All**.
기대: `FlappyChaserCurve` / `FlappyChaserPosition` 타입이 없어 **컴파일 에러**. 콘솔에 `error CS0246: The type or namespace name 'FlappyChaserCurve' could not be found` 가 나와야 한다.

- [ ] **Step 5: 최소 구현 작성**

`Assets/Scripts/FlappyRaceSlice/Logic/FlappyChaserCurve.cs`:

```csharp
namespace FlappyRace
{
    /// <summary>
    /// 추격자 속도 곡선. 초기속도에서 선형으로 빨라지다 상한에서 평평해진다.
    /// 상한은 플레이어 전진속도보다 낮아야 한다 — 완벽하게 난 사람은 끝까지 잡히지 않는 것이 원칙.
    /// </summary>
    public sealed class FlappyChaserCurve
    {
        public float InitialSpeed = 7f;
        public float Acceleration = 0.075f;
        public float MaxSpeed = 10f;

        public float SpeedAt(float elapsed)
        {
            if (elapsed <= 0f) return InitialSpeed;
            float s = InitialSpeed + Acceleration * elapsed;
            return s < MaxSpeed ? s : MaxSpeed;
        }

        /// <summary>상한에 도달하는 시각. 이 뒤로는 실수 여유가 더 늘지 않는다 = 클라이맥스 시작.</summary>
        public float PressureOnsetTime()
        {
            float gap = MaxSpeed - InitialSpeed;
            if (gap <= 0f) return 0f;
            if (Acceleration <= 0f) return float.PositiveInfinity;
            return gap / Acceleration;
        }
    }
}
```

`Assets/Scripts/FlappyRaceSlice/Logic/FlappyChaserPosition.cs`:

```csharp
namespace FlappyRace
{
    /// <summary>추격자가 설 자리를 정한다.</summary>
    public static class FlappyChaserPosition
    {
        /// <summary>
        /// 가속선과 "선두 한 화면 뒤" 중 더 앞선 쪽. 앞의 것이 선두에게도 압박을 주고,
        /// 뒤의 것이 격차가 화면을 넘는 순간 뒤를 잘라 전원을 한 화면에 묶는다.
        /// </summary>
        public static float Resolve(float curveX, float leaderX, float screenWidth)
        {
            float trailing = leaderX - screenWidth;
            return curveX > trailing ? curveX : trailing;
        }
    }
}
```

- [ ] **Step 6: 테스트가 통과하는지 확인**

Unity Editor에서 **Test Runner > EditMode > Run All**.
기대: 8개 테스트 전부 **PASS**. 실패하면 컴파일 에러부터 확인 — 신규 `.cs`는 `refresh_unity(scope="all")`가 필요하다.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/FlappyRaceSlice/Logic Assets/Tests
git commit -m "feat(flappy): 추격자 속도 곡선·위치 규칙 + EditMode 테스트 하네스"
```

---

## Task 2: 추격자 러너 — 위치 갱신·가시화·탈락

Task 1의 순수 로직을 씬에 붙인다. 추격자가 실제로 화면 왼쪽에서 다가오고, 뒤처진 레이서를 잡는다.

**Files:**
- Create: `Assets/Scripts/FlappyRaceSlice/FlappyChaser.cs`
- Modify: `Assets/Scripts/FlappyRaceSlice/FlappyBird.cs` (레이서 레지스트리 추가)
- Scene: `Assets/Art/Scenes/FlappyRace.unity` (`Chaser` 오브젝트 추가)

**Interfaces:**
- Consumes: `FlappyRace.FlappyChaserCurve`, `FlappyRace.FlappyChaserPosition` (Task 1)
- Produces:
  - `FlappyBird.All` — `static readonly List<FlappyBird>`, 활성 레이서 전원
  - `FlappyChaser.Instance` — `static FlappyChaser`
  - `FlappyChaser.X` — `float { get; }` 추격자 현재 X
  - `FlappyChaser.Eliminated` — `static event System.Action<FlappyBird>`, 잡힌 순간 발행 (Plan 2의 정산이 여기 붙는다)

- [ ] **Step 1: 레이서 레지스트리 추가**

`Assets/Scripts/FlappyRaceSlice/FlappyBird.cs`의 클래스 본문 맨 위(`static readonly Collider[] _buf` 바로 위)에 추가:

```csharp
    /// <summary>활성 레이서 전원. 추격자·카메라·HUD가 선두를 찾는 데 쓴다.</summary>
    public static readonly System.Collections.Generic.List<FlappyBird> All = new System.Collections.Generic.List<FlappyBird>();

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }
```

- [ ] **Step 2: 추격자 컴포넌트 작성**

`Assets/Scripts/FlappyRaceSlice/FlappyChaser.cs`:

```csharp
using UnityEngine;
using FlappyRace;

/// <summary>
/// 화면 왼쪽에서 쫓아오는 추격자. 닿으면 탈락.
/// 위치는 "가속선"과 "선두 한 화면 뒤" 중 앞선 쪽 — 앞의 것이 선두도 압박하고,
/// 뒤의 것이 전원을 한 화면에 묶어 박치기·몸싸움 기회를 끝까지 유지한다.
/// </summary>
public class FlappyChaser : MonoBehaviour
{
    [Header("속도 곡선 (상한은 플레이어 전진속도 11보다 반드시 낮게)")]
    public float initialSpeed = 7f;
    public float acceleration = 0.075f;
    public float maxSpeed = 10f;

    [Header("위치")]
    public float startX = -60f;          // 스타트라인(-3) 한참 뒤에서 출발
    public float screenPadding = 4f;     // 화면폭에서 이만큼 당겨 잡아 완전히 화면 밖에서 죽지 않게
    public float fallbackScreenWidth = 57f;   // 카메라를 못 찾을 때(헤드리스 등) 쓰는 값

    [Header("가시화")]
    public float wallHeight = 300f;
    public Color wallColor = new Color(0.85f, 0.15f, 0.15f, 0.65f);

    public float X { get; private set; }
    public float Elapsed { get; private set; }

    /// <summary>잡힌 순간 발행. 정산·관전 전환이 여기 붙는다.</summary>
    public static event System.Action<FlappyBird> Eliminated;
    public static FlappyChaser Instance { get; private set; }

    readonly FlappyChaserCurve curve = new FlappyChaserCurve();
    float curveX;
    Camera cam;

    void Awake() { Instance = this; }

    void OnEnable()
    {
        curveX = X = startX;
        Elapsed = 0f;
        EnsureWall();
    }

    /// <summary>카메라가 비추는 가로 폭(월드 단위). orthographic 전제.</summary>
    public float ScreenWidthWorld()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || !cam.orthographic) return fallbackScreenWidth;
        return cam.orthographicSize * 2f * cam.aspect;
    }

    public float LeaderX()
    {
        float best = float.NegativeInfinity;
        foreach (var b in FlappyBird.All)
            if (b != null && b.transform.position.x > best) best = b.transform.position.x;
        return float.IsNegativeInfinity(best) ? startX : best;
    }

    void Update()
    {
        if (FlappyRaceStart.RaceFrozen) return;   // 카운트다운 중엔 안 움직인다

        float dt = Time.deltaTime;
        Elapsed += dt;

        curve.InitialSpeed = initialSpeed;
        curve.Acceleration = acceleration;
        curve.MaxSpeed = maxSpeed;
        curveX += curve.SpeedAt(Elapsed) * dt;

        X = FlappyChaserPosition.Resolve(curveX, LeaderX(), Mathf.Max(1f, ScreenWidthWorld() - screenPadding));

        var p = transform.position;
        p.x = X;
        p.y = FollowY();
        transform.position = p;

        Catch();
    }

    // 벽이 화면 세로를 덮게 카메라 높이를 따라간다(고도 맵에서도 항상 막힌 것처럼 보이게).
    float FollowY()
    {
        if (cam == null) cam = Camera.main;
        return cam != null ? cam.transform.position.y : transform.position.y;
    }

    void Catch()
    {
        // 역순회 — Eliminated 핸들러가 오브젝트를 끄면 All에서 빠지므로
        for (int i = FlappyBird.All.Count - 1; i >= 0; i--)
        {
            var b = FlappyBird.All[i];
            if (b == null) continue;
            if (b.transform.position.x > X) continue;
            Debug.Log($"[Chaser] {b.name} 탈락 — t={Elapsed:F1}s, x={b.transform.position.x:F1}");
            Eliminated?.Invoke(b);
            if (b != null && b.gameObject.activeSelf) b.gameObject.SetActive(false);
        }
    }

    // 자식으로 붉은 벽 하나. 추격자의 정체(비급 소재)는 아트 단계에서 이 자리에 들어간다.
    void EnsureWall()
    {
        if (transform.Find("ChaserWall") != null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "ChaserWall";
        var col = go.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);   // 물리 충돌 없음 — 판정은 X 비교로만
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(2f, wallHeight, 2f);
        var mr = go.GetComponent<MeshRenderer>();
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh != null)
        {
            var m = new Material(sh);
            m.color = wallColor;
            mr.sharedMaterial = m;
        }
    }
}
```

- [ ] **Step 3: 씬에 추격자 배치 (에디트 모드)**

Unity Editor가 **플레이 모드가 아닌지** 먼저 확인한다. UnityMCP `execute_code`로 실행:

```csharp
var go = GameObject.Find("Chaser") ?? new GameObject("Chaser");
go.transform.position = new Vector3(-60f, 0f, 0f);
if (go.GetComponent<FlappyChaser>() == null) go.AddComponent<FlappyChaser>();
UnityEditor.EditorUtility.SetDirty(go);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
Debug.Log("[Setup] Chaser 배치 완료 x=" + go.transform.position.x);
```

기대 출력: `[Setup] Chaser 배치 완료 x=-60`

- [ ] **Step 4: 플레이 모드로 동작 확인**

플레이 모드에 들어가 15초 정도 둔 뒤 `execute_code`로 확인:

```csharp
var c = Object.FindObjectOfType<FlappyChaser>();
Debug.Log($"elapsed={c.Elapsed:F1} chaserX={c.X:F1} leaderX={c.LeaderX():F1} " +
          $"gap={c.LeaderX() - c.X:F1} screenW={c.ScreenWidthWorld():F1}");
```

기대:
- `screenW`가 55~58 사이 (orthographic size 16 × 2 × 16:9 종횡비)
- `gap`이 양수이고 `screenW`보다 크지 않다
- 봇이 충돌로 멈추면 gap이 줄어들고, 계속 뒤처지면 콘솔에 `[Chaser] Pacer_... 탈락` 이 찍힌다

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/FlappyRaceSlice/FlappyChaser.cs Assets/Scripts/FlappyRaceSlice/FlappyBird.cs Assets/Art/Scenes/FlappyRace.unity
git commit -m "feat(flappy): 추격자 러너 — 가속선/선두추적 위치 규칙 + 탈락 판정"
```

---

## Task 3: 코리도 경계와 장애물 분리

페널티가 "0.5초 정지"에서 "낙마"로 무거워지므로, 코리도 상하 경계를 스치기만 해도 낙마하면 플레이가 불가능해진다. 경계를 페널티 대상에서 뺀다.

씬의 `ComposedMap` 아래 구조는 이렇다 — 경계는 `CorridorTop` / `CorridorBot` / `FunnelTop` / `FunnelBot` 아래의 타일드 박스들이고, 나머지(`Windmill`, `Iris`, `PinchTop`, `PinchBot`, `Fill/*`)는 통과해야 할 내부 장애물이다.

**Files:**
- Create: `Assets/Scripts/FlappyRaceSlice/FlappyBoundary.cs`
- Create: `Assets/Editor/FlappyBoundaryMarker.cs`
- Modify: `Assets/Scripts/FlappyRaceSlice/FlappyPlayer.cs` (`ResolveObstacles`)
- Modify: `Assets/Scripts/FlappyRaceSlice/FlappyPacer.cs` (`ResolveObstacles`)

**Interfaces:**
- Consumes: 없음
- Produces: `FlappyBoundary` — 마커 컴포넌트. 이게 붙은(또는 부모에 붙은) 콜라이더는 밀어내되 낙마시키지 않는다.

- [ ] **Step 1: 경계 마커 작성**

`Assets/Scripts/FlappyRaceSlice/FlappyBoundary.cs`:

```csharp
using UnityEngine;

/// <summary>
/// 코리도 상하 경계 마커. 막기만 하고 낙마 페널티는 주지 않는다.
/// 경계는 "통과해야 할 장애물"이 아니라 "플레이 영역의 벽"이라 스침에 벌을 주면 안 된다.
/// </summary>
public class FlappyBoundary : MonoBehaviour { }
```

- [ ] **Step 2: 인벤토리 출력으로 대상 확인**

`Assets/Editor/FlappyBoundaryMarker.cs`:

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>코리도 경계를 일괄 마킹하고, 마킹 전후를 눈으로 확인할 인벤토리를 뽑는다.</summary>
public static class FlappyBoundaryMarker
{
    // 이 이름을 가진 조상 아래에 있으면 경계로 본다.
    static readonly string[] BoundaryRoots = { "CorridorTop", "CorridorBot", "FunnelTop", "FunnelBot" };

    [MenuItem("Flappy/장애물 인벤토리 출력")]
    public static void Inventory()
    {
        var byPath = new Dictionary<string, int>();
        foreach (var ob in Object.FindObjectsByType<FlappyObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string key = TopPath(ob.transform) + (IsBoundary(ob.transform) ? "  [경계]" : "  [장애물]");
            byPath[key] = byPath.TryGetValue(key, out int n) ? n + 1 : 1;
        }
        var sb = new System.Text.StringBuilder("[FlappyObstacle 인벤토리]\n");
        foreach (var kv in byPath) sb.AppendLine($"  {kv.Key} × {kv.Value}");
        Debug.Log(sb.ToString());
    }

    [MenuItem("Flappy/코리도 경계 마킹")]
    public static void Mark()
    {
        int added = 0;
        foreach (var ob in Object.FindObjectsByType<FlappyObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsBoundary(ob.transform)) continue;
            if (ob.GetComponent<FlappyBoundary>() != null) continue;
            Undo.AddComponent<FlappyBoundary>(ob.gameObject);
            added++;
        }
        Debug.Log($"[FlappyBoundary] {added}개 마킹됨");
        if (added > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    static bool IsBoundary(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
            foreach (var n in BoundaryRoots)
                if (p.name == n) return true;
        return false;
    }

    // 조상 경로를 최상위 2단계까지만(로그가 폭발하지 않게)
    static string TopPath(Transform t)
    {
        var chain = new List<string>();
        for (var p = t; p != null; p = p.parent) chain.Add(p.name);
        chain.Reverse();
        int take = Mathf.Min(3, chain.Count);
        return string.Join("/", chain.GetRange(0, take));
    }
}
```

Unity 메뉴 **Flappy > 장애물 인벤토리 출력** 실행.
기대: `ComposedMap/CorridorTop/... [경계]`, `ComposedMap/Gauntlet/Windmill... [장애물]` 처럼 갈려서 나온다. `[경계]`로 분류된 게 하나도 없으면 씬의 실제 부모 이름이 다르다는 뜻이므로, 출력된 경로를 보고 `BoundaryRoots` 배열을 고친 뒤 다시 실행한다.

- [ ] **Step 3: 경계 마킹 실행 + 씬 저장**

**에디트 모드**에서 메뉴 **Flappy > 코리도 경계 마킹** 실행 → `Ctrl/Cmd + S`로 씬 저장.
기대 로그: `[FlappyBoundary] N개 마킹됨` (N > 0).

- [ ] **Step 4: 페널티 판정에서 경계 제외**

`Assets/Scripts/FlappyRaceSlice/FlappyPlayer.cs`의 `ResolveObstacles` 루프 본문을 아래로 교체:

```csharp
        for (int i = 0; i < n; i++)
        {
            var o = _pen[i];
            if (o == col || o.GetComponentInParent<FlappyObstacle>() == null) continue;
            bool isBoundary = o.GetComponentInParent<FlappyBoundary>() != null;
            if (Physics.ComputePenetration(col, col.transform.position, col.transform.rotation,
                                           o, o.transform.position, o.transform.rotation,
                                           out Vector3 dir, out float dist))
            {
                if (!isBoundary) touching = true;   // 경계는 막기만 — 낙마 없음
                transform.position += dir * dist;   // 밖으로 밀어냄(관통 방지)
                if (dir.y > 0.5f && vy < 0f) vy = 0f;
                else if (dir.y < -0.5f && vy > 0f) vy = 0f;
            }
        }
```

`Assets/Scripts/FlappyRaceSlice/FlappyPacer.cs`의 `ResolveObstacles`에도 **똑같은 변경**을 적용한다(변수명 `_buf`만 다르다):

```csharp
        for (int i = 0; i < n; i++)
        {
            var o = _buf[i];
            if (o == col || o.GetComponentInParent<FlappyObstacle>() == null) continue;
            bool isBoundary = o.GetComponentInParent<FlappyBoundary>() != null;
            if (Physics.ComputePenetration(col, col.transform.position, col.transform.rotation,
                                           o, o.transform.position, o.transform.rotation,
                                           out Vector3 dir, out float dist))
            {
                if (!isBoundary) touching = true;   // 경계는 막기만 — 낙마 없음
                transform.position += dir * dist;
                if (dir.y > 0.5f && vy < 0f) vy = 0f;
                else if (dir.y < -0.5f && vy > 0f) vy = 0f;
            }
        }
```

- [ ] **Step 5: 경계 스침에 페널티가 없는지 확인**

플레이 모드에서 `execute_code`로 플레이어를 코리도 바닥에 붙인 뒤 상태를 본다:

```csharp
var pl = Object.FindObjectOfType<FlappyPlayer>();
var p = pl.transform.position;
pl.transform.position = new Vector3(p.x, p.y - 40f, 0f);   // 아래로 처박아 경계에 닿게
Debug.Log("[Test] 경계로 이동");
```

1초쯤 뒤:

```csharp
var pl = Object.FindObjectOfType<FlappyPlayer>();
Debug.Log($"Ghost(낙마중)={pl.Ghost}  x={pl.transform.position.x:F1}");
```

기대: `Ghost=False` (경계에 눌려 있어도 낙마하지 않는다) 이고 `x`가 계속 증가한다(전진이 멈추지 않는다).

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/FlappyRaceSlice/FlappyBoundary.cs Assets/Editor/FlappyBoundaryMarker.cs Assets/Scripts/FlappyRaceSlice/FlappyPlayer.cs Assets/Scripts/FlappyRaceSlice/FlappyPacer.cs Assets/Art/Scenes/FlappyRace.unity
git commit -m "feat(flappy): 코리도 경계를 낙마 페널티 대상에서 분리"
```

---

## Task 4: 낙마 상태와 연출 훅

기존의 "그 자리 0.5초 정지"를 낙마로 승격한다. 정지 로직 자체는 이미 검증된 것(상승 엣지 판정, 관통 방지, 무한 정지 방지)이라 그대로 두고, **외부에서 발동시킬 수 있는 진입점**과 **연출이 붙을 이벤트**를 연다.

라이더 아트는 Plan 2(탈것 스킨)에서 들어오므로, 여기서는 기존 스프라이트를 복제한 **자리표시 라이더**로 연출한다.

**Files:**
- Modify: `Assets/Scripts/FlappyRaceSlice/FlappyPlayer.cs`
- Modify: `Assets/Scripts/FlappyRaceSlice/FlappyPacer.cs`
- Create: `Assets/Scripts/FlappyRaceSlice/FlappyDismountFx.cs`

**Interfaces:**
- Consumes: 없음
- Produces:
  - `FlappyPlayer.Dismount()` / `FlappyPacer.Dismount()` — `public void`. 무적·낙마 중이면 무시. 성공 시 `ghostT = ghostTime` 세팅 + 이벤트 발행
  - `FlappyPlayer.Dismounted` / `FlappyPacer.Dismounted` — `public event System.Action`

- [ ] **Step 1: 플레이어에 낙마 진입점 추가**

`Assets/Scripts/FlappyRaceSlice/FlappyPlayer.cs`의 `StartBoost` 메서드 바로 아래에 추가:

```csharp
    /// <summary>낙마 시 발행. 연출(FlappyDismountFx)이 구독한다.</summary>
    public event System.Action Dismounted;

    /// <summary>
    /// 낙마 — 그 자리에서 ghostTime초 정지 후 복귀. 맵 장애물 충돌과 대시 박치기, 이 둘만 호출한다.
    /// 무적 중이거나 이미 낙마 중이면 무시(벽에 얹혀 무한 낙마하는 것 방지).
    /// </summary>
    public void Dismount()
    {
        if (ghostT > 0f || invuln > 0f) return;
        ghostT = ghostTime;
        Dismounted?.Invoke();
    }
```

- [ ] **Step 2: 기존 충돌 경로를 낙마 진입점으로 통일**

같은 파일 `Update()` 안의 아래 줄을

```csharp
        if (touching && !_wasTouching && invuln <= 0f) ghostT = ghostTime;  // 새 충돌(상승엣지)에만 0.5s 정지
```

이렇게 바꾼다:

```csharp
        if (touching && !_wasTouching) Dismount();   // 새 충돌(상승엣지)에만 낙마. 무적 체크는 Dismount 안에서
```

- [ ] **Step 3: 봇에도 동일 적용**

`Assets/Scripts/FlappyRaceSlice/FlappyPacer.cs`의 `StartBoost` 바로 아래에 추가:

```csharp
    /// <summary>낙마 시 발행. 연출이 구독한다.</summary>
    public event System.Action Dismounted;

    /// <summary>낙마 — 플레이어와 동일 규칙. 무적/낙마 중이면 무시.</summary>
    public void Dismount()
    {
        if (ghostT > 0f || invuln > 0f) return;
        ghostT = ghostTime;
        Dismounted?.Invoke();
    }
```

같은 파일 `Update()` 안의

```csharp
        if (touching && !_wasTouching && invuln <= 0f) ghostT = ghostTime;
```

를 이렇게 바꾼다:

```csharp
        if (touching && !_wasTouching) Dismount();
```

- [ ] **Step 4: 낙마 연출 작성**

`Assets/Scripts/FlappyRaceSlice/FlappyDismountFx.cs`:

```csharp
using UnityEngine;

/// <summary>
/// 낙마 연출 — 라이더가 탈것에서 튕겨 나가 빙글빙글 날아갔다 돌아온다.
/// 탈것은 제자리에서 태연하게 떠 있는다. 이 대비가 비급 감성의 자리다.
/// 라이더 아트가 아직 없어 지금은 탈것 스프라이트를 축소 복제해 자리표시로 쓴다.
/// </summary>
public class FlappyDismountFx : MonoBehaviour
{
    public float flyDuration = 0.45f;     // 튕겨 나가는 시간
    public Vector2 flyVelocity = new Vector2(-3f, 9f);
    public float flyGravity = 22f;
    public float spinSpeed = 900f;
    public float riderScale = 0.55f;

    SpriteRenderer source;
    Transform rider;
    SpriteRenderer riderSr;
    float t = -1f;
    Vector2 vel;

    void Awake()
    {
        source = GetComponentInChildren<SpriteRenderer>();

        var p = GetComponent<FlappyPlayer>();
        if (p != null) p.Dismounted += Play;
        var b = GetComponent<FlappyPacer>();
        if (b != null) b.Dismounted += Play;
    }

    void Play()
    {
        if (source == null) return;
        EnsureRider();
        rider.position = transform.position;
        rider.localScale = Vector3.one * riderScale;
        rider.gameObject.SetActive(true);
        vel = flyVelocity;
        t = 0f;
    }

    void EnsureRider()
    {
        if (rider != null) return;
        var go = new GameObject("DismountRider");
        riderSr = go.AddComponent<SpriteRenderer>();
        riderSr.sprite = source.sprite;
        riderSr.color = new Color(1f, 0.9f, 0.45f, 1f);   // 자리표시 — Plan 2에서 라이더 아트로 교체
        riderSr.sortingOrder = source.sortingOrder + 1;
        rider = go.transform;
    }

    void Update()
    {
        if (t < 0f || rider == null) return;
        float dt = Time.deltaTime;
        t += dt;

        if (t < flyDuration)
        {
            vel.y -= flyGravity * dt;
            rider.position += (Vector3)(vel * dt);
            rider.Rotate(0f, 0f, spinSpeed * dt);
        }
        else
        {
            // 탈것이 라이더를 낚아채 복귀
            float k = Mathf.Clamp01((t - flyDuration) / 0.2f);
            rider.position = Vector3.Lerp(rider.position, transform.position, k);
            rider.rotation = Quaternion.Slerp(rider.rotation, Quaternion.identity, k);
            if (k >= 1f) { rider.gameObject.SetActive(false); t = -1f; }
        }
    }
}
```

- [ ] **Step 5: 씬의 네 레이서에 연출 부착 + 낙마 시간 0.7초로**

**에디트 모드**에서 `execute_code`:

```csharp
int fx = 0, tuned = 0;
foreach (var b in Object.FindObjectsByType<FlappyBird>(FindObjectsInactive.Include, FindObjectsSortMode.None))
{
    if (b.GetComponent<FlappyDismountFx>() == null) { b.gameObject.AddComponent<FlappyDismountFx>(); fx++; }
    var p = b.GetComponent<FlappyPlayer>(); if (p != null) { p.ghostTime = 0.7f; tuned++; UnityEditor.EditorUtility.SetDirty(p); }
    var c = b.GetComponent<FlappyPacer>();  if (c != null) { c.ghostTime = 0.7f; tuned++; UnityEditor.EditorUtility.SetDirty(c); }
}
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
Debug.Log($"[Setup] 연출 {fx}개 부착, 낙마시간 {tuned}개 0.7초로");
```

기대: `[Setup] 연출 4개 부착, 낙마시간 4개 0.7초로`

- [ ] **Step 6: 낙마가 실제로 터지는지 확인**

플레이 모드에서 `execute_code`로 강제 낙마:

```csharp
var pl = Object.FindObjectOfType<FlappyPlayer>();
pl.Dismount();
Debug.Log($"Ghost={pl.Ghost}");
```

기대: `Ghost=True` 이고 게임 뷰에 노란 자리표시 라이더가 튕겨 날아갔다 0.65초쯤 뒤 돌아온다. 이어서 다시 호출하면 무적 때문에 `Ghost=False`가 아니라 무시된다 — 0.7 + 0.5 = 1.2초 뒤에 다시 걸린다.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/FlappyRaceSlice/FlappyPlayer.cs Assets/Scripts/FlappyRaceSlice/FlappyPacer.cs Assets/Scripts/FlappyRaceSlice/FlappyDismountFx.cs Assets/Art/Scenes/FlappyRace.unity
git commit -m "feat(flappy): 낙마 상태 진입점과 연출 훅"
```

---

## Task 5: 대시 박치기와 운동량 튕김

충돌 규칙 세 줄을 구현한다. 대시로 박으면 상대가 낙마하고, 맞대시는 서로 튕기기만 하며, 그 외 접촉은 페널티 없이 속도가 반영된 튕김이 된다. 셋 다 같은 코드 자리(새끼리 충돌 해소)에서 갈리므로 한 태스크로 묶는다.

**Files:**
- Create: `Assets/Scripts/FlappyRaceSlice/IFlappyRacer.cs`
- Modify: `Assets/Scripts/FlappyRaceSlice/FlappyBird.cs`
- Modify: `Assets/Scripts/FlappyRaceSlice/FlappyPlayer.cs`
- Modify: `Assets/Scripts/FlappyRaceSlice/FlappyPacer.cs`

**Interfaces:**
- Consumes: `FlappyPlayer.Dismount()`, `FlappyPacer.Dismount()` (Task 4)
- Produces:
  - `IFlappyRacer` — `bool IsDashing { get; }`, `Vector2 Velocity { get; }`, `void Dismount()`, `void AddKnockback(Vector2 impulse)`
  - `FlappyBird.Racer` — `IFlappyRacer { get; }`, Awake에서 캐시

- [ ] **Step 1: 공통 인터페이스 작성**

`Assets/Scripts/FlappyRaceSlice/IFlappyRacer.cs`:

```csharp
using UnityEngine;

/// <summary>
/// 플레이어와 봇이 충돌 처리에서 동일하게 다뤄지기 위한 공통 창구.
/// 새끼리 충돌 코드가 어느 쪽이 대시 중인지, 얼마나 빨리 다가왔는지 알아야 규칙을 가를 수 있다.
/// </summary>
public interface IFlappyRacer
{
    bool IsDashing { get; }
    Vector2 Velocity { get; }
    void Dismount();
    void AddKnockback(Vector2 impulse);
}
```

- [ ] **Step 2: 플레이어에 인터페이스 구현**

`Assets/Scripts/FlappyRaceSlice/FlappyPlayer.cs`의 클래스 선언을 바꾼다:

```csharp
public class FlappyPlayer : MonoBehaviour, IFlappyRacer
```

`Dismount()` 메서드 아래에 넉백 관련을 추가:

```csharp
    [Header("충돌 튕김")]
    public float knockDecay = 5f;      // 밀림이 사그라드는 속도(클수록 짧게 튕김)
    public float maxKnock = 22f;       // 폭발 방지 상한
    Vector2 knock;

    public bool IsDashing => dashT > 0f;
    public Vector2 Velocity => new Vector2(forwardSpeed * (dashT > 0f ? dashMult : 1f), vy);

    /// <summary>부딪힌 순간의 밀림을 얹는다. 코어 속도는 그대로 두고 이 값만 따로 감쇠시킨다.</summary>
    public void AddKnockback(Vector2 impulse)
    {
        knock = Vector2.ClampMagnitude(knock + impulse, maxKnock);
    }
```

`Update()`에서 위치를 확정하는 `transform.position = p;` **바로 위**에 밀림 적분을 끼워 넣는다:

```csharp
        // 충돌 밀림 — 코어 물리와 분리해 따로 얹고 감쇠(엔진 물리에 넘기면 손맛이 흔들린다)
        if (knock.sqrMagnitude > 0.0001f)
        {
            p.x += knock.x * dt;
            p.y += knock.y * dt;
            knock *= Mathf.Exp(-knockDecay * dt);
        }
        transform.position = p;
```

- [ ] **Step 3: 봇에 인터페이스 구현**

`Assets/Scripts/FlappyRaceSlice/FlappyPacer.cs`의 클래스 선언을 바꾼다:

```csharp
public class FlappyPacer : MonoBehaviour, IFlappyRacer
```

`Dismount()` 아래에 추가:

```csharp
    [Header("충돌 튕김(플레이어와 동일)")]
    public float knockDecay = 5f;
    public float maxKnock = 22f;
    Vector2 knock;

    public bool IsDashing => boostT > 0f;   // 봇은 스타트 부스트가 대시에 해당
    public Vector2 Velocity => new Vector2(forwardSpeed * (boostT > 0f ? startBoostMult : 1f), vy);

    public void AddKnockback(Vector2 impulse)
    {
        knock = Vector2.ClampMagnitude(knock + impulse, maxKnock);
    }
```

> **알아둘 것: 봇은 대시 메커닉이 없다.** `FlappyPacer`가 가진 유일한 버스트는 스타트 부스트라, 위 구현에서 봇이 "대시 중"인 순간은 레이스 출발 직후뿐이다. 즉 **봇은 사실상 박치기를 하지 못하고 당하기만 한다.** 지금은 봇이 넷코드 전까지의 임시 상대라 이대로 두지만, 봇에게도 다이브 충전 대시를 주는 것은 별도 안건이다. 사람끼리 붙는 실제 멀티에서는 이 비대칭이 사라진다.

`Update()`에서 `transform.position = p;` **바로 위**에 끼워 넣는다:

```csharp
        if (knock.sqrMagnitude > 0.0001f)
        {
            p.x += knock.x * dt;
            curY += knock.y * dt;
            p.y = curY;
            knock *= Mathf.Exp(-knockDecay * dt);
        }
        transform.position = p;
```

- [ ] **Step 4: 충돌 해소에 세 규칙 구현**

`Assets/Scripts/FlappyRaceSlice/FlappyBird.cs`를 통째로 아래로 교체:

```csharp
using UnityEngine;

/// <summary>
/// 새(플레이어/페이서) 마커 + 새끼리 충돌 처리.
/// 규칙 세 줄:
///   1. 대시 중에 남과 겹치면 그 사람이 낙마한다. 박은 쪽은 감속 없이 지나간다.
///   2. 맞대시로 서로 박으면 둘 다 낙마 없이 튕기기만 한다.
///   3. 그 외 접촉은 페널티 없이 속도가 반영된 튕김이다 — 세게 받히면 궤도가 무너져 파이프에 박는다.
/// 각 새가 자기 Update에서 절반씩 밀어내 양쪽 합쳐 완전 분리(대칭).
/// </summary>
public class FlappyBird : MonoBehaviour
{
    /// <summary>활성 레이서 전원. 추격자·카메라·HUD가 선두를 찾는 데 쓴다.</summary>
    public static readonly System.Collections.Generic.List<FlappyBird> All = new System.Collections.Generic.List<FlappyBird>();

    /// <summary>부딪힌 상대 속도 1당 얹을 밀림 세기. 크면 요란하게 튕긴다.</summary>
    public static float KnockbackScale = 0.85f;

    public IFlappyRacer Racer { get; private set; }

    static readonly Collider[] _buf = new Collider[16];

    void Awake() { Racer = GetComponent<IFlappyRacer>(); }
    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    /// <summary>self 새를 겹친 다른 새들 밖으로 절반씩 밀어냄. 수직 밀림 합을 반환(호출부가 vy 상쇄에 사용).</summary>
    public static float ResolveBirdCollisions(Collider self)
    {
        if (self == null) return 0f;
        var selfBird = self.GetComponentInParent<FlappyBird>();
        if (selfBird == null) return 0f;
        float vpush = 0f;

        int n = Physics.OverlapSphereNonAlloc(self.bounds.center, self.bounds.extents.magnitude + 0.05f,
                                              _buf, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var o = _buf[i];
            if (o == self) continue;
            var otherBird = o.GetComponentInParent<FlappyBird>();
            if (otherBird == null || otherBird == selfBird) continue;

            if (!Physics.ComputePenetration(self, self.transform.position, self.transform.rotation,
                                            o, o.transform.position, o.transform.rotation,
                                            out Vector3 dir, out float dist))
                continue;

            var selfR = selfBird.Racer;
            var otherR = otherBird.Racer;
            bool selfDash = selfR != null && selfR.IsDashing;
            bool otherDash = otherR != null && otherR.IsDashing;

            // 규칙 1 — 나만 대시 중이면 상대가 낙마. (규칙 2: 둘 다 대시면 낙마 없음)
            if (selfDash && !otherDash && otherR != null) otherR.Dismount();

            self.transform.position += dir * dist * 0.5f;   // 절반만(상대도 절반 밀어냄)
            vpush += dir.y;

            // 규칙 3 — 대시로 박는 쪽은 감속 없이 지나가고, 그 외에는 다가온 속도만큼 튕긴다
            if (!selfDash && selfR != null)
            {
                Vector2 rel = (otherR != null ? otherR.Velocity : Vector2.zero) - selfR.Velocity;
                float closing = Vector2.Dot(rel, (Vector2)dir);
                if (closing > 0f) selfR.AddKnockback((Vector2)dir * (closing * KnockbackScale));
            }
        }
        return vpush;
    }
}
```

- [ ] **Step 5: 박치기가 상대를 낙마시키는지 확인**

플레이 모드에서 `execute_code`로 플레이어를 대시 상태로 만들고 봇 위에 겹친다:

```csharp
var pl = Object.FindObjectOfType<FlappyPlayer>();
var bot = GameObject.Find("Pacer_Cyan").GetComponent<FlappyPacer>();
pl.StartBoost(0.5f);                                  // 대시 강제 발동
pl.transform.position = bot.transform.position;       // 겹치기
Debug.Log($"준비: 플레이어대시={pl.Dashing} 봇낙마={bot.Ghost}");
```

0.3초쯤 뒤:

```csharp
var bot = GameObject.Find("Pacer_Cyan").GetComponent<FlappyPacer>();
Debug.Log($"박치기 후 봇 낙마={bot.Ghost}");
```

기대: `박치기 후 봇 낙마=True`

- [ ] **Step 6: 비대시 충돌이 낙마 없이 튕기는지 확인**

```csharp
var pl = Object.FindObjectOfType<FlappyPlayer>();
var bot = GameObject.Find("Pacer_Yellow").GetComponent<FlappyPacer>();
pl.transform.position = bot.transform.position;   // 대시 아닌 상태로 겹치기
Debug.Log($"겹침 직후 봇낙마={bot.Ghost} 거리={Vector3.Distance(pl.transform.position, bot.transform.position):F2}");
```

0.3초쯤 뒤:

```csharp
var pl = Object.FindObjectOfType<FlappyPlayer>();
var bot = GameObject.Find("Pacer_Yellow").GetComponent<FlappyPacer>();
Debug.Log($"봇낙마={bot.Ghost} 거리={Vector3.Distance(pl.transform.position, bot.transform.position):F2}");
```

기대: `봇낙마=False` 이고 거리가 0.9 이상으로 벌어져 있다(콜라이더 지름만큼 분리). 거리가 계속 0에 가깝거나 위치가 발산하면 `maxKnock`이 너무 큰 것이니 낮춘다.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/FlappyRaceSlice/IFlappyRacer.cs Assets/Scripts/FlappyRaceSlice/FlappyBird.cs Assets/Scripts/FlappyRaceSlice/FlappyPlayer.cs Assets/Scripts/FlappyRaceSlice/FlappyPacer.cs
git commit -m "feat(flappy): 대시 박치기 낙마 + 운동량 반영 튕김"
```

---

## Task 6: 카메라 선두 추종

추격자 규칙이 "선두 − 화면 한 폭"으로 뒤를 자르므로, 카메라도 선두를 기준으로 잡아야 전원이 화면에 담긴다. 지금은 플레이어만 따라가서 플레이어가 뒤처지면 선두가 화면 밖으로 나간다.

**Files:**
- Modify: `Assets/Scripts/FlappyRaceSlice/FlappyCameraFollow.cs`

**Interfaces:**
- Consumes: `FlappyBird.All` (Task 2)
- Produces: `FlappyCameraFollow.followLeader` — `bool`, 켜면 target 대신 선두를 따라간다

- [ ] **Step 1: 선두 추종 추가**

`Assets/Scripts/FlappyRaceSlice/FlappyCameraFollow.cs`의 필드 선언부(`public float lerp = 6f;` 아래)에 추가:

```csharp
    [Header("선두 추종 — 추격자가 뒤를 자르므로 카메라는 선두를 잡아야 전원이 화면에 담긴다")]
    public bool followLeader = true;
    public float leaderOffsetX = -14f;   // 선두를 화면 오른쪽 어디에 둘지(음수 = 선두 왼쪽으로 카메라를 당김)
```

`LateUpdate()`의 x 계산 줄을 교체한다. 기존:

```csharp
        p.x = Mathf.Lerp(p.x, target.position.x + offsetX, Time.deltaTime * lerp);
```

교체 후:

```csharp
        float focusX = followLeader ? LeaderX() : target.position.x;
        float wantX = followLeader ? focusX + leaderOffsetX : focusX + offsetX;
        p.x = Mathf.Lerp(p.x, wantX, Time.deltaTime * lerp);
```

`LateUpdate()` 아래에 헬퍼를 추가:

```csharp
    float LeaderX()
    {
        float best = float.NegativeInfinity;
        foreach (var b in FlappyBird.All)
            if (b != null && b.transform.position.x > best) best = b.transform.position.x;
        return float.IsNegativeInfinity(best) ? target.position.x : best;
    }
```

`followY`의 세로 추적은 여전히 `target`(플레이어)을 따라간다 — 내 캐릭터가 화면 세로 중앙에 있어야 조작이 편하므로 그대로 둔다.

- [ ] **Step 2: 전원이 한 화면에 담기는지 확인**

플레이 모드에서 20초쯤 둔 뒤 `execute_code`:

```csharp
var cam = Camera.main;
float halfW = cam.orthographicSize * cam.aspect;
var sb = new System.Text.StringBuilder();
foreach (var b in FlappyBird.All)
{
    float dx = b.transform.position.x - cam.transform.position.x;
    sb.Append($"{b.name}: dx={dx:F1} {(Mathf.Abs(dx) <= halfW ? "화면안" : "★화면밖")}  ");
}
Debug.Log($"halfW={halfW:F1} | {sb}");
```

기대: 네 레이서 전부 `화면안`. 하나라도 `★화면밖`이면 `leaderOffsetX`를 조정하거나 `FlappyChaser.screenPadding`을 키운다.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/FlappyRaceSlice/FlappyCameraFollow.cs
git commit -m "feat(flappy): 카메라 선두 추종"
```

---

## Task 7: HUD 목숨 게이지

추격자까지 남은 거리를 보조 지표로 띄운다. 주 정보원은 화면에 보이는 추격자 자체이므로, 게이지는 위험할 때 눈에 띄되 평소엔 조용해야 한다.

**Files:**
- Modify: `Assets/Scripts/FlappyRaceSlice/FlappyHUD.cs`

**Interfaces:**
- Consumes: `FlappyChaser.Instance`, `FlappyChaser.X` (Task 2)
- Produces: 없음 (화면 표시만)

- [ ] **Step 1: 게이지 UI 요소 추가**

`Assets/Scripts/FlappyRaceSlice/FlappyHUD.cs`의 필드 선언부에 추가 (`RectTransform trackRect;` 아래):

```csharp
    Image lifeBg, lifeFill; Text lifeLabel;
    /// <summary>이 거리 이하로 좁혀지면 게이지가 붉게 경고한다.</summary>
    public float dangerDistance = 18f;
```

`BuildCanvas()` 맨 끝(대시 게이지 생성 뒤)에 추가:

```csharp
        // ── 목숨 게이지 = 추격자까지 남은 거리 (우측 하단) ──
        lifeBg = MakeImage(root, "LifeBg", new Color(0f, 0f, 0f, 0.55f));
        SetRect(lifeBg, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(400, 54));
        lifeFill = MakeImage(lifeBg.rectTransform, "LifeFill", new Color(0.45f, 1f, 0.55f, 1f));
        SetRect(lifeFill, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(5, 0), new Vector2(0, 44));
        lifeLabel = MakeText(lifeBg.rectTransform, "LifeLbl", 26, TextAnchor.MiddleCenter, Color.white);
        SetRect(lifeLabel, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
```

- [ ] **Step 2: 매 프레임 갱신**

`Update()` 맨 끝(대시 게이지 갱신 뒤)에 추가:

```csharp
        // 목숨 게이지 — 추격자까지 남은 거리. 화면 한 폭이 만땅.
        var chaser = FlappyChaser.Instance;
        if (chaser != null && player != null && lifeFill != null)
        {
            float full = Mathf.Max(1f, chaser.ScreenWidthWorld());
            float gap = Mathf.Max(0f, player.transform.position.x - chaser.X);
            float k = Mathf.Clamp01(gap / full);
            lifeFill.rectTransform.sizeDelta = new Vector2(5f + k * 390f - 10f, 44f);
            bool danger = gap <= dangerDistance;
            lifeFill.color = danger ? new Color(1f, 0.3f, 0.25f, 1f) : new Color(0.45f, 1f, 0.55f, 1f);
            lifeLabel.text = danger ? "⚠ 잡힌다!  " + Mathf.RoundToInt(gap) + "m" : Mathf.RoundToInt(gap) + "m";
        }
```

- [ ] **Step 3: 게이지가 맞는 값을 보이는지 확인**

플레이 모드에서 `execute_code`:

```csharp
var pl = Object.FindObjectOfType<FlappyPlayer>();
var ch = FlappyChaser.Instance;
Debug.Log($"HUD가 표시해야 할 값: {Mathf.RoundToInt(pl.transform.position.x - ch.X)}m");
```

기대: 게임 뷰 우하단 게이지의 숫자가 이 값과 같다. 플레이어를 뒤로 옮기면 게이지가 줄고 18m 이하에서 붉게 변한다:

```csharp
var pl = Object.FindObjectOfType<FlappyPlayer>();
var ch = FlappyChaser.Instance;
pl.transform.position = new Vector3(ch.X + 10f, pl.transform.position.y, 0f);
Debug.Log("추격자 10m 앞으로 이동 — 게이지가 붉어져야 함");
```

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/FlappyRaceSlice/FlappyHUD.cs
git commit -m "feat(flappy): HUD 목숨 게이지(추격자 거리)"
```

---

## Task 8: 추격자 곡선 튜닝 — 완주율 게이트

이 플랜에서 가장 중요한 숫자가 추격자 곡선이다. 감으로 잡지 않고, 이미 있는 `FlappySimJudge`의 실력대별 충돌 분포를 재활용해 완주율을 계산한다.

**통과 조건은 하나다 — 고수 실력대 완주율이 100%.** 완주가 기본이라는 원칙이 지켜지는지를 이 숫자로 검증한다. 동시에 초보 실력대에서는 탈락이 실제로 나와야 한다(안 나오면 추격자가 장식이다).

**Files:**
- Create: `Assets/Scripts/FlappyRaceSlice/Logic/FlappyChaserOutcome.cs`
- Test: `Assets/Tests/EditMode/FlappyChaserOutcomeTests.cs`
- Modify: `Assets/Scripts/FlappyRaceSlice/FlappySimJudge.cs`

**Interfaces:**
- Consumes: `FlappyRace.FlappyChaserCurve` (Task 1)
- Produces: `FlappyRace.FlappyChaserOutcome.Survives(...)` — 아래 시그니처

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/Tests/EditMode/FlappyChaserOutcomeTests.cs`:

```csharp
using NUnit.Framework;
using FlappyRace;

public class FlappyChaserOutcomeTests
{
    static FlappyChaserCurve Curve() =>
        new FlappyChaserCurve { InitialSpeed = 7f, Acceleration = 0.075f, MaxSpeed = 10f };

    // 코스: -3 → 632, 전진 11, 낙마 0.7초, 10유닛 버킷
    const float StartX = -3f, FinishX = 632f, Fwd = 11f, Dismount = 0.7f, Bucket = 10f;
    const float ChaserStart = -60f;

    static float[] Uniform(float clipsPerBucket)
    {
        int n = (int)((FinishX - StartX) / Bucket) + 1;
        var a = new float[n];
        for (int i = 0; i < n; i++) a[i] = clipsPerBucket;
        return a;
    }

    [Test]
    public void 무결점_주행은_반드시_완주한다()
    {
        var clips = Uniform(0f);
        bool ok = FlappyChaserOutcome.Survives(clips, Bucket, StartX, FinishX, Fwd, Dismount,
                                               Curve(), ChaserStart, out float caught);
        Assert.IsTrue(ok, $"완벽하게 났는데 t={caught}에 잡혔다");
        Assert.AreEqual(-1f, caught);
    }

    [Test]
    public void 충돌이_아주_많으면_잡힌다()
    {
        // 버킷마다 평균 0.6회 = 코스 전체 38회쯤, 시간손실 27초
        var clips = Uniform(0.6f);
        bool ok = FlappyChaserOutcome.Survives(clips, Bucket, StartX, FinishX, Fwd, Dismount,
                                               Curve(), ChaserStart, out float caught);
        Assert.IsFalse(ok, "충돌 38회짜리 주행이 완주했다 — 추격자가 너무 느슨하다");
        Assert.Greater(caught, 0f);
    }

    [Test]
    public void 충돌이_많을수록_더_일찍_잡힌다()
    {
        // 둘 다 확실히 잡히는 수준이라야 시각 비교가 의미를 갖는다
        bool a = FlappyChaserOutcome.Survives(Uniform(0.8f), Bucket, StartX, FinishX, Fwd, Dismount,
                                              Curve(), ChaserStart, out float early);
        bool b = FlappyChaserOutcome.Survives(Uniform(0.6f), Bucket, StartX, FinishX, Fwd, Dismount,
                                              Curve(), ChaserStart, out float late);
        Assert.IsFalse(a); Assert.IsFalse(b);
        Assert.Less(early, late);
    }

    [Test]
    public void 추격자가_뒤에서_출발할수록_여유가_늘어난다()
    {
        var clips = Uniform(0.6f);
        bool nearOk = FlappyChaserOutcome.Survives(clips, Bucket, StartX, FinishX, Fwd, Dismount,
                                                   Curve(), -20f, out float near);
        bool farOk = FlappyChaserOutcome.Survives(clips, Bucket, StartX, FinishX, Fwd, Dismount,
                                                  Curve(), -120f, out float far);
        Assert.IsFalse(nearOk, "추격자가 코앞에서 출발했는데 완주했다");
        // 멀리서 출발하면 더 늦게 잡히거나 아예 안 잡힌다
        Assert.IsTrue(farOk || far > near, $"near={near} far={far}");
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

**Test Runner > EditMode > Run All**.
기대: `error CS0246: ... 'FlappyChaserOutcome' could not be found` 로 컴파일 실패.

- [ ] **Step 3: 판정 로직 구현**

`Assets/Scripts/FlappyRaceSlice/Logic/FlappyChaserOutcome.cs`:

```csharp
namespace FlappyRace
{
    /// <summary>
    /// 시뮬 충돌 분포를 시간축으로 풀어 "이 실력이면 완주하는가"를 판정한다.
    /// 추격자 곡선 튜닝의 통과 조건(고수 완주율 100%)을 계산하는 도구.
    ///
    /// 단독 주행 모델이라 "선두 한 화면 뒤" 규칙은 적용하지 않는다 —
    /// 혼자 달리면 자기가 곧 선두이고, 그 규칙은 항상 자기보다 뒤를 가리켜 절대 잡지 못한다.
    /// 따라서 가속선만 비교하면 되고, 그것이 곧 최악 조건이다.
    /// </summary>
    public static class FlappyChaserOutcome
    {
        /// <param name="bucketClips">구간별 평균 충돌 횟수(런당). 인덱스 i = startX + i*bucketWidth 구간.</param>
        /// <param name="caughtAtTime">잡힌 시각(초). 완주했으면 -1.</param>
        /// <returns>완주하면 true.</returns>
        public static bool Survives(
            float[] bucketClips, float bucketWidth,
            float startX, float finishX,
            float forwardSpeed, float dismountTime,
            FlappyChaserCurve curve, float chaserStartX,
            out float caughtAtTime)
        {
            caughtAtTime = -1f;
            if (bucketClips == null || bucketClips.Length == 0) return true;

            const float dt = 0.05f;
            float t = 0f;
            float x = startX;
            float chaser = chaserStartX;
            float pause = 0f;
            int bucket = -1;
            int guard = 0;

            while (x < finishX && guard++ < 2000000)
            {
                chaser += curve.SpeedAt(t) * dt;

                if (pause > 0f)
                {
                    pause -= dt;   // 낙마 중 — 전진 정지
                }
                else
                {
                    float nx = x + forwardSpeed * dt;
                    int nb = (int)((nx - startX) / bucketWidth);
                    if (nb > bucket)
                    {
                        bucket = nb;
                        if (nb >= 0 && nb < bucketClips.Length)
                            pause += bucketClips[nb] * dismountTime;
                    }
                    x = nx;
                }

                t += dt;

                if (x <= chaser) { caughtAtTime = t; return false; }
            }
            return true;
        }
    }
}
```

- [ ] **Step 4: 테스트가 통과하는지 확인**

**Test Runner > EditMode > Run All**.
기대: Task 1의 8개 + 이번 4개 = **12개 전부 PASS**.

- [ ] **Step 5: 시뮬 리포트에 완주율 붙이기**

`Assets/Scripts/FlappyRaceSlice/FlappySimJudge.cs`의 클래스 안(다른 `[ContextMenu]` 메서드들 옆)에 추가:

```csharp
    [Header("추격자 튜닝")]
    public float chaserInitialSpeed = 7f;
    public float chaserAcceleration = 0.075f;
    public float chaserMaxSpeed = 10f;
    public float chaserStartX = -60f;

    /// <summary>
    /// 실력대별로 추격자에게 잡히는지 계산한다.
    /// 통과 조건: 고수 완주율 100%(완주가 기본이라는 원칙) + 초보 탈락이 실제로 발생(추격자가 장식이 아님).
    /// 전진속도와 낙마시간은 씬의 FlappyPlayer에서 읽는다 — 시뮬과 게임이 어긋나면 튜닝이 무의미하다.
    /// </summary>
    [ContextMenu("Run Chaser Tuning")]
    public void RunChaserTuning()
    {
        if (!Prepare()) return;   // fwd/startX/endX/birdR 등을 씬에서 읽어 채운다

        var curve = new FlappyRace.FlappyChaserCurve
        {
            InitialSpeed = chaserInitialSpeed,
            Acceleration = chaserAcceleration,
            MaxSpeed = chaserMaxSpeed
        };

        if (curve.MaxSpeed >= fwd)
        {
            Debug.LogError($"[Chaser] 상한 {curve.MaxSpeed}이 전진속도 {fwd} 이상이다 — " +
                           "완벽한 플레이도 잡히므로 스펙 위반이다.");
            return;
        }

        float dismount = player.ghostTime;   // 낙마 1회당 잃는 시간
        int nBucket = NBucket;
        var heat = new int[nBucket];
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== 추격자 튜닝 (초기{curve.InitialSpeed} 가속{curve.Acceleration} 상한{curve.MaxSpeed} " +
                      $"전환점{curve.PressureOnsetTime():F0}s | 전진{fwd} 낙마{dismount}s) ===");

        for (int tier = 0; tier < 3; tier++)
        {
            int clean;
            float mean = SimTier(reactDelay[tier], timingJitter[tier], samplesPerTier, heat, out clean);

            var clips = new float[nBucket];
            for (int b = 0; b < nBucket; b++) clips[b] = (float)heat[b] / samplesPerTier;

            bool ok = FlappyRace.FlappyChaserOutcome.Survives(
                clips, 10f, startX, endX, fwd, dismount,
                curve, chaserStartX, out float caught);

            sb.AppendLine($"  [{TierNames[tier]}] 평균충돌 {mean:F2}회 | " +
                          (ok ? "완주" : $"탈락 (t={caught:F1}s)"));
        }
        Debug.Log(sb.ToString());
    }
```

여기서 쓰는 `Prepare()`(bool 반환), `NBucket`, `SimTier`, `player`, `fwd`, `startX`, `endX`, `reactDelay`, `timingJitter`, `samplesPerTier`, `TierNames`는 전부 `FlappySimJudge` 안에 이미 있는 멤버다. `TierNames`의 순서는 `{ "초보", "중수", "고수" }` 이므로 인덱스 2가 고수다.

- [ ] **Step 6: 튜닝 실행 및 게이트 확인**

Unity에서 씬의 `SimJudge` 오브젝트를 선택 → 인스펙터의 `FlappySimJudge` 컴포넌트 우클릭 → **Run Chaser Tuning**.

기대 출력 형태:

```
=== 추격자 튜닝 (초기7 가속0.075 상한10 전환점40s | 전진11 낙마0.7s) ===
  [초보] 평균충돌 16.80회 | 탈락 (t=52.3s)
  [중수] 평균충돌 13.80회 | 완주
  [고수] 평균충돌 10.20회 | 완주
```

**게이트 판정:**
- 고수가 `탈락`이면 → `chaserMaxSpeed`를 낮추거나 `chaserStartX`를 더 뒤로. 고수 완주는 타협 불가다.
- 초보까지 전부 `완주`면 → 추격자가 장식이다. `chaserInitialSpeed`를 올리거나 `chaserAcceleration`을 키운다.
- 조정한 값은 씬의 `Chaser` 오브젝트 인스펙터에도 **똑같이** 반영한다(에디트 모드에서 세팅 후 씬 저장). 시뮬 파라미터와 실제 게임 파라미터가 어긋나면 튜닝이 무의미하다.

- [ ] **Step 7: 확정된 값을 씬에 반영**

**에디트 모드**에서 `execute_code` (아래 숫자는 Step 6에서 확정한 값으로 바꾼다):

```csharp
var ch = Object.FindObjectOfType<FlappyChaser>();
ch.initialSpeed = 7f; ch.acceleration = 0.075f; ch.maxSpeed = 10f; ch.startX = -60f;
UnityEditor.EditorUtility.SetDirty(ch);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
Debug.Log($"[Chaser] 확정: 초기{ch.initialSpeed} 가속{ch.acceleration} 상한{ch.maxSpeed} 시작{ch.startX}");
```

- [ ] **Step 8: 커밋**

```bash
git add Assets/Scripts/FlappyRaceSlice/Logic/FlappyChaserOutcome.cs Assets/Tests/EditMode/FlappyChaserOutcomeTests.cs Assets/Scripts/FlappyRaceSlice/FlappySimJudge.cs Assets/Art/Scenes/FlappyRace.unity
git commit -m "feat(flappy): 추격자 완주율 시뮬 게이트 + 곡선 확정"
```

---

## 완료 기준

이 플랜이 끝나면 다음이 성립해야 한다.

- EditMode 테스트 12개 전부 통과
- 추격자가 화면 왼쪽에서 실제로 보이고, 가속하되 전진속도를 넘지 않는다
- 코리도 경계를 스쳐도 페널티가 없고, 맵 장애물에 부딪히면 낙마한다
- 대시로 남을 박으면 그 사람이 낙마하고, 맞대시는 튕기기만 하며, 그 외 접촉은 궤도가 흐트러진다
- 네 레이서가 항상 한 화면에 담긴다
- HUD에 추격자까지 남은 거리가 뜨고 위험하면 붉어진다
- 시뮬 게이트에서 고수 완주율 100%

## 다음 플랜

`정산 + 관전 + 탈것 스킨` — `FlappyChaser.Eliminated` 이벤트와 결승선 통과에 순위·Elo 확정 커밋을 붙이고, 관전 UI로 전환하며, 탈것을 성능 동일한 순수 스킨 시스템으로 만든다. 낙마 연출의 자리표시 라이더가 실제 라이더 아트로 교체되는 것도 그 플랜이다.
