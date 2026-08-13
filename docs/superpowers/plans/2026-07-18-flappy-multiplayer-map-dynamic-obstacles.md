# Flappy Race — 다인용 맵 + 동적 장애물 3종 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 현재 단일-틈 플래피 슬라이스를 4인용 혼합 구간 코스로 바꾸고, 동적 장애물 3종(오르내리는 파이프·회전 도넛·조리개)을 유니티 씬에 이식한다.

**Architecture:** 기존 `FlappyRaceSlice`의 독립 MonoBehaviour 스타일 유지(정식 World Core/VContainer 미통합 — 손맛 확인용 throwaway 슬라이스). 재사용 `FlappyCourseGenerator` 컴포넌트가 인스펙터 구간 리스트로 코스를 절차 생성하고, 동적 장애물은 타입별 작은 스크립트가 자기 모션만 담당한다. 충돌은 기존 `FlappyObstacle` 마커 재활용.

**Tech Stack:** Unity 6000.3.16f1, URP(Lit), New Input System, UnityMCP(에디터 조작·컴파일 확인·플레이모드). 조작은 전부 UnityMCP 클라이언트 인스턴스 대상.

## Global Constraints

- **모든 신규 스크립트 경로**: `Assets/Scripts/FlappyRaceSlice/` (기존 슬라이스와 동일 폴더).
- **UnityMCP 인스턴스 타깃팅(필수)**: 이 프로젝트는 client. 매 UnityMCP 툴 호출에 `unity_instance`를 명시한다. 실행 시점에 `mcpforunity://instances`에서 `name == "LeagueOfPhysical-Client"`인 인스턴스의 전체 `id`(`Name@hash`)를 읽어 사용(해시는 바뀔 수 있음). `set_active_instance` 핀은 이 프로젝트에서 신뢰 불가.
- **씬**: `Assets/Art/Scenes/FlappyRace.unity` (이미 열려 있음).
- **파이프 머티리얼**: `Assets/Art/Environment/FlappyRace/Pipe.mat` (URP/Lit, 초록 `RGB(0.2,0.72,0.25)`, GUID `ea5236f49d5fa4888874f5159d4015a6`). 생성기 인스펙터 필드에 할당해서 씀(런타임 `AssetDatabase` 접근 회피).
- **플레이어 물리 기준값(통과보장 계산·좌표 범위의 근거, `FlappyPlayer` 인스펙터)**: `forwardSpeed = 10.5`, `flapImpulse = 23`, `ceilingY = 14`, `groundY = -8`, 콜라이더 반지름 0.5. 생성기 기본값을 이에 맞춘다.
- **테스트 방식**: 이 슬라이스는 기존에도 자동화 테스트 없이 컴파일+플레이모드 관찰로 검증해 왔다(EditMode/PlayMode 테스트 asmdef 없음). 이 플랜도 각 태스크의 "테스트"를 **① `read_console`로 컴파일 무오류 확인 → ② 플레이모드/에디터에서 실제 동작 관찰**로 정의한다. 통과보장 산식만 순수 계산이라 관찰로 검증(막히는 구간=위반).
- **커밋**: `/Users/insoobae/workspace/LOP`는 git repo가 아니다(형제 repo만 git). 따라서 커밋 스텝은 없음. 각 태스크 끝은 "컴파일 무오류 + 관찰 통과"로 완료 판정.
- **주석 컨벤션**: 자명한 것엔 주석 금지, 비자명한 의도(왜)만 일상어 한 줄. `/// <summary>`는 public 타입에.

---

### Task 1: `FlappyMovingPipe` — 수직 왕복 파이프쌍 스크립트

동적 장애물 셋 중 가장 단순. 부모 Transform을 `y = baseY + amp·sin` 으로 위아래 왕복. 파이프 지오메트리는 생성기가 자식으로 붙인다(이 스크립트는 모션만).

**Files:**
- Create: `Assets/Scripts/FlappyRaceSlice/FlappyMovingPipe.cs`

**Interfaces:**
- Consumes: 없음(독립).
- Produces: `FlappyMovingPipe` (public 필드 `float amplitude`, `float speed`, `float phase`). 생성기가 `AddComponent<FlappyMovingPipe>()` 후 이 필드들을 세팅.

- [ ] **Step 1: 스크립트 작성**

`create_script`(UnityMCP, `unity_instance` 명시)로 아래 내용을 `Assets/Scripts/FlappyRaceSlice/FlappyMovingPipe.cs`에 생성:

```csharp
using UnityEngine;

/// <summary>
/// 동적 장애물 — 위/아래 파이프쌍(틈 크기 고정)이 세로로 왕복.
/// 부모 오브젝트를 baseY 기준 sin 파형으로 움직인다. 파이프 지오메트리는 FlappyCourseGenerator가 자식으로 붙인다.
/// 통과보장: 틈 크기는 항상 통과 가능(고정), amplitude가 플레이 세로범위를 벗어나지 않게 튜닝.
/// </summary>
public class FlappyMovingPipe : MonoBehaviour
{
    public float amplitude = 3f;   // 왕복 진폭(유닛)
    public float speed = 1.2f;     // 각속도(rad/s)
    public float phase = 0f;       // 시작 위상 — 여러 개가 엇박으로 움직이게

    float baseY;

    void Start() => baseY = transform.position.y;

    void Update()
    {
        var p = transform.position;
        p.y = baseY + amplitude * Mathf.Sin(Time.time * speed + phase);
        transform.position = p;
    }
}
```

- [ ] **Step 2: 컴파일 확인**

`read_console`(UnityMCP, `unity_instance` 명시, `types:["error"]`, `action:"get"`)로 컴파일 에러 0 확인. 필요 시 `editor_state`의 `isCompiling`이 false될 때까지 폴링.
Expected: 에러 없음. `FlappyMovingPipe` 타입 사용 가능.

---

### Task 2: `FlappyRotatingDonut` — 회전 링 스크립트

Z축(화면 축)으로 링을 천천히 회전. 링 지오메트리(빈틈 있는 세그먼트 다발)는 생성기가 자식으로 붙인다.

**Files:**
- Create: `Assets/Scripts/FlappyRaceSlice/FlappyRotatingDonut.cs`

**Interfaces:**
- Consumes: 없음.
- Produces: `FlappyRotatingDonut` (public 필드 `float rotSpeed`).

- [ ] **Step 1: 스크립트 작성**

`create_script`로 `Assets/Scripts/FlappyRaceSlice/FlappyRotatingDonut.cs`:

```csharp
using UnityEngine;

/// <summary>
/// 동적 장애물 — 둘레에 빈틈 하나가 있는 링(도넛)이 화면 축(Z) 기준으로 회전.
/// 링 세그먼트(장애물)는 FlappyCourseGenerator가 자식으로 붙인다.
/// 통과보장: 링 중앙 구멍은 항상 열려 있어(회전과 무관) 타이밍만 맞추면 스레딩 통과 가능 — 공정.
/// </summary>
public class FlappyRotatingDonut : MonoBehaviour
{
    public float rotSpeed = 40f;   // deg/s

    void Update() => transform.Rotate(0f, 0f, rotSpeed * Time.deltaTime, Space.Self);
}
```

- [ ] **Step 2: 컴파일 확인**

`read_console`로 에러 0 확인.
Expected: 에러 없음.

---

### Task 3: `FlappyIris` — 조리개(개폐) 스크립트

위/아래 파이프를 중심 기준으로 대칭으로 벌렸다 좁혔다 → 틈이 개폐. 두 자식 파이프 Transform을 참조.

**Files:**
- Create: `Assets/Scripts/FlappyRaceSlice/FlappyIris.cs`

**Interfaces:**
- Consumes: 없음.
- Produces: `FlappyIris` (public 필드 `Transform topPipe`, `Transform bottomPipe`, `float baseGap`, `float amp`, `float speed`, `float phase`). 생성기가 두 파이프 Transform과 값들을 세팅.

- [ ] **Step 1: 스크립트 작성**

`create_script`로 `Assets/Scripts/FlappyRaceSlice/FlappyIris.cs`:

```csharp
using UnityEngine;

/// <summary>
/// 동적 장애물 — 위/아래 파이프가 중심 기준 대칭으로 벌어졌다 좁혀지는 조리개. 틈 크기가 sin 파형으로 개폐.
/// 두 파이프의 mouth(입구)를 ±gap/2 로 옮긴다. 지오메트리는 FlappyCourseGenerator가 만들어 topPipe/bottomPipe에 연결.
/// 통과보장: 최소 구경(baseGap − amp)을 통과 가능 크기 이상으로 유지 → 치명적으로 닫히지 않음(공정).
/// </summary>
public class FlappyIris : MonoBehaviour
{
    public Transform topPipe;
    public Transform bottomPipe;
    public float baseGap = 5f;
    public float amp = 2.2f;
    public float speed = 1.4f;
    public float phase = 0f;

    void Update()
    {
        float gap = baseGap + amp * Mathf.Sin(Time.time * speed + phase);
        float half = gap * 0.5f;
        if (topPipe != null)
        {
            var t = topPipe.localPosition; t.y = half; topPipe.localPosition = t;
        }
        if (bottomPipe != null)
        {
            var b = bottomPipe.localPosition; b.y = -half; bottomPipe.localPosition = b;
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

`read_console`로 에러 0 확인.
Expected: 에러 없음. 세 동적 스크립트 모두 존재 → 다음 태스크의 생성기가 참조 가능.

---

### Task 4: `FlappyCourseGenerator` — 코스 조립기

인스펙터 구간 리스트로 코스를 절차 생성. WideGap/MultiSlot 정적 구간 + Dynamic 구간(Task 1~3 스크립트 부착)을 순서대로 배치. 통과보장은 연속 틈 중심 델타 ≤ `MaxGapDelta`로 강제.

**Files:**
- Create: `Assets/Scripts/FlappyRaceSlice/FlappyCourseGenerator.cs`

**Interfaces:**
- Consumes: `FlappyMovingPipe`(Task 1), `FlappyRotatingDonut`(Task 2), `FlappyIris`(Task 3), `FlappyObstacle`(기존 마커).
- Produces: `FlappyCourseGenerator` — `[ContextMenu("Generate Course")] void Generate()`, `[ContextMenu("Clear Course")] void Clear()`. public 필드는 아래 코드 참조(생성기 자체는 다른 스크립트가 참조하지 않음 — 씬/인스펙터/ContextMenu로만 구동).

- [ ] **Step 1: 스크립트 작성**

`create_script`로 `Assets/Scripts/FlappyRaceSlice/FlappyCourseGenerator.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FlappyRace 코스 조립기 — 인스펙터 구간 리스트를 읽어 파이프/동적 장애물을 절차 생성한다.
/// ContextMenu의 Generate/Clear로 반복 튜닝. 일회성 execute_code 생성을 대체.
/// 통과보장: 연속 틈(또는 슬롯 세트)의 세로 이동량을 MaxGapDelta 이하로 제한 → 타이밍만 맞으면 항상 통과 가능.
/// </summary>
public class FlappyCourseGenerator : MonoBehaviour
{
    public enum SectionType { WideGap, MultiSlot, Dynamic }
    public enum DynamicKind { MovingPipe, Donut, Iris }

    [System.Serializable]
    public class Section
    {
        public SectionType type = SectionType.WideGap;
        public int count = 3;
        public DynamicKind dynamicKind = DynamicKind.MovingPipe; // type==Dynamic일 때만 의미
    }

    [Header("배치")]
    public Transform courseRoot;       // 비우면 이 오브젝트 자신을 루트로
    public float startX = 6f;
    public float wallSpacing = 14f;
    public int seed = 12345;

    [Header("세로 범위 (플레이어 천장 14 / 바닥 -8 안쪽)")]
    public float yMin = -6f;
    public float yMax = 12f;

    [Header("틈 크기")]
    public float wideGapSize = 9f;     // 4인 몸싸움 틈
    public int multiSlotCount = 4;     // 슬롯(구멍) 수
    public float slotGapSize = 2.8f;   // 슬롯 하나 높이

    [Header("통과보장 (플레이어 값과 일치시킬 것)")]
    public float flapImpulse = 23f;
    public float forwardSpeed = 10.5f;
    public float reachFactor = 0.45f;  // 안전 마진 계수 (작을수록 보수적)

    [Header("파이프 비주얼")]
    public Material pipeMaterial;      // Pipe.mat 할당
    public float pipeWidth = 1.6f;     // X 두께
    public float pipeDepth = 2f;       // Z 두께
    public float lipExtra = 0.5f;      // 립이 몸통보다 넓은 정도
    public float lipHeight = 0.8f;

    [Header("동적 장애물")]
    public float movingPipeGap = 5f;
    public float movingPipeAmp = 3f;
    public float movingPipeSpeed = 1.2f;
    public float donutRadius = 3.2f;
    public float donutRotSpeed = 40f;
    public float irisBaseGap = 5f;
    public float irisAmp = 2.2f;
    public float irisSpeed = 1.4f;

    [Header("코스 시퀀스")]
    public List<Section> sections = new List<Section>()
    {
        new Section { type = SectionType.WideGap,  count = 3 },
        new Section { type = SectionType.MultiSlot, count = 3 },
        new Section { type = SectionType.Dynamic,  count = 2, dynamicKind = DynamicKind.MovingPipe },
        new Section { type = SectionType.WideGap,  count = 2 },
        new Section { type = SectionType.Dynamic,  count = 2, dynamicKind = DynamicKind.Donut },
        new Section { type = SectionType.MultiSlot, count = 3 },
        new Section { type = SectionType.Dynamic,  count = 2, dynamicKind = DynamicKind.Iris },
        new Section { type = SectionType.WideGap,  count = 2 },
    };

    /// <summary>연속 틈이 세로로 움직일 수 있는 최대치 = (플랩힘/2)×(간격/전진속도)×마진. 이하로 제한하면 항상 도달 가능.</summary>
    public float MaxGapDelta => (flapImpulse * 0.5f) * (wallSpacing / forwardSpeed) * reachFactor;

    Transform Root => courseRoot != null ? courseRoot : transform;

    [ContextMenu("Generate Course")]
    public void Generate()
    {
        Clear();
        Random.InitState(seed);
        var root = Root;

        float x = startX;
        float gapCenter = (yMin + yMax) * 0.5f;
        float slotOffset = 0f;

        foreach (var s in sections)
        {
            for (int k = 0; k < Mathf.Max(0, s.count); k++)
            {
                switch (s.type)
                {
                    case SectionType.WideGap:
                        gapCenter = StepClamped(gapCenter, wideGapSize);
                        BuildWideGapWall(root, x, gapCenter);
                        break;
                    case SectionType.MultiSlot:
                        slotOffset = Mathf.Clamp(slotOffset + RandStep(), -3f, 3f);
                        BuildMultiSlotWall(root, x, slotOffset);
                        break;
                    case SectionType.Dynamic:
                        gapCenter = StepClamped(gapCenter, movingPipeGap);
                        BuildDynamic(root, x, gapCenter, s.dynamicKind);
                        break;
                }
                x += wallSpacing;
            }
        }
    }

    [ContextMenu("Clear Course")]
    public void Clear()
    {
        var root = Root;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var c = root.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
    }

    float RandStep() => Random.Range(-MaxGapDelta, MaxGapDelta);

    // 랜덤워크 한 걸음(델타 ≤ MaxGapDelta) 후 범위 클램프. 클램프는 델타를 줄이기만 하므로 통과보장 유지.
    float StepClamped(float current, float gapSize)
    {
        float lo = yMin + gapSize * 0.5f;
        float hi = yMax - gapSize * 0.5f;
        return Mathf.Clamp(current + RandStep(), lo, hi);
    }

    // --- 정적 구간 ---

    void BuildWideGapWall(Transform parent, float x, float gapCenter)
    {
        var wall = NewChild(parent, "WideGap", new Vector3(x, 0f, 0f));
        float topExtent = yMax + 4f, bottomExtent = yMin - 4f;
        float topMouth = gapCenter + wideGapSize * 0.5f;
        float bottomMouth = gapCenter - wideGapSize * 0.5f;
        BuildPipe(wall, "Top", new Vector3(0f, topMouth, 0f), topExtent - topMouth, true);
        BuildPipe(wall, "Bottom", new Vector3(0f, bottomMouth, 0f), bottomMouth - bottomExtent, false);
    }

    void BuildMultiSlotWall(Transform parent, float x, float offset)
    {
        var wall = NewChild(parent, "MultiSlot", new Vector3(x, 0f, 0f));
        float topExtent = yMax + 4f, bottomExtent = yMin - 4f;
        float range = yMax - yMin;

        float lowerEdge = bottomExtent;
        for (int i = 0; i < multiSlotCount; i++)
        {
            float center = yMin + (i + 0.5f) * (range / multiSlotCount) + offset;
            float slotBottom = center - slotGapSize * 0.5f;
            BuildSegment(wall, lowerEdge, slotBottom);
            lowerEdge = center + slotGapSize * 0.5f;
        }
        BuildSegment(wall, lowerEdge, topExtent);
    }

    // 세로 [fromY, toY] 구간을 메우는 solid 세그먼트 + 양 끝(슬롯 대면) 립.
    void BuildSegment(Transform wall, float fromY, float toY)
    {
        if (toY - fromY <= 0.05f) return;
        float center = (fromY + toY) * 0.5f;
        float len = toY - fromY;
        MakeBox(wall, "Seg", new Vector3(0f, center, 0f), new Vector3(pipeWidth, len, pipeDepth));
        MakeBox(wall, "LipTop", new Vector3(0f, toY, 0f), LipSize());
        MakeBox(wall, "LipBottom", new Vector3(0f, fromY, 0f), LipSize());
    }

    // --- 동적 구간 ---

    void BuildDynamic(Transform parent, float x, float gapCenter, DynamicKind kind)
    {
        switch (kind)
        {
            case DynamicKind.MovingPipe: BuildMovingPipe(parent, x, gapCenter); break;
            case DynamicKind.Donut: BuildDonut(parent, x, gapCenter); break;
            case DynamicKind.Iris: BuildIris(parent, x, gapCenter); break;
        }
    }

    void BuildMovingPipe(Transform parent, float x, float gapCenter)
    {
        var go = NewChild(parent, "MovingPipe", new Vector3(x, gapCenter, 0f));
        float half = movingPipeGap * 0.5f;
        BuildPipe(go, "Top", new Vector3(0f, half, 0f), 24f, true);
        BuildPipe(go, "Bottom", new Vector3(0f, -half, 0f), 24f, false);
        var mp = go.gameObject.AddComponent<FlappyMovingPipe>();
        mp.amplitude = movingPipeAmp;
        mp.speed = movingPipeSpeed;
    }

    void BuildIris(Transform parent, float x, float gapCenter)
    {
        var go = NewChild(parent, "Iris", new Vector3(x, gapCenter, 0f));
        var top = BuildPipe(go, "Top", new Vector3(0f, irisBaseGap * 0.5f, 0f), 24f, true);
        var bottom = BuildPipe(go, "Bottom", new Vector3(0f, -irisBaseGap * 0.5f, 0f), 24f, false);
        var iris = go.gameObject.AddComponent<FlappyIris>();
        iris.topPipe = top;
        iris.bottomPipe = bottom;
        iris.baseGap = irisBaseGap;
        iris.amp = irisAmp;
        iris.speed = irisSpeed;
    }

    void BuildDonut(Transform parent, float x, float gapCenter)
    {
        var go = NewChild(parent, "Donut", new Vector3(x, gapCenter, 0f));
        const int segs = 12;
        const int gapSegs = 2;              // 링 둘레의 빈틈(생략할 세그먼트 수)
        float bandThickness = 1f;
        float segLen = (2f * Mathf.PI * donutRadius / segs) * 1.2f;
        for (int i = 0; i < segs; i++)
        {
            if (i < gapSegs) continue;
            float ang = (360f / segs) * i;
            float rad = ang * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * donutRadius;
            var seg = MakeBox(go, "Seg", pos, new Vector3(bandThickness, segLen, pipeDepth));
            seg.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
        }
        go.gameObject.AddComponent<FlappyRotatingDonut>().rotSpeed = donutRotSpeed;
    }

    // --- 프리미티브 헬퍼 ---

    // mouth(입구) 기준으로 파이프 한 개 생성. up=true면 몸통이 위로, false면 아래로 뻗음. 립은 입구에 얹음.
    Transform BuildPipe(Transform parent, string name, Vector3 mouthLocalPos, float length, bool up)
    {
        var pipe = NewChild(parent, name, mouthLocalPos);
        float dir = up ? 1f : -1f;
        float len = Mathf.Max(0.1f, length);
        MakeBox(pipe, "Body", new Vector3(0f, dir * len * 0.5f, 0f), new Vector3(pipeWidth, len, pipeDepth));
        MakeBox(pipe, "Lip", Vector3.zero, LipSize());
        return pipe;
    }

    Vector3 LipSize() => new Vector3(pipeWidth + lipExtra, lipHeight, pipeDepth + lipExtra);

    Transform NewChild(Transform parent, string name, Vector3 localPos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        return t;
    }

    GameObject MakeBox(Transform parent, string name, Vector3 localPos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        if (pipeMaterial != null) go.GetComponent<MeshRenderer>().sharedMaterial = pipeMaterial;
        go.GetComponent<BoxCollider>().isTrigger = true; // 플레이어(kinematic RB)와 트리거로 충돌
        go.AddComponent<FlappyObstacle>();
        return go;
    }
}
```

- [ ] **Step 2: 컴파일 확인**

`read_console`(`types:["error"]`)로 에러 0 확인. `isCompiling` false까지 대기.
Expected: 에러 없음. `FlappyCourseGenerator` 컴포넌트로 부착 가능.

---

### Task 5: 씬 배선 + 코스 생성 + 정적 구간 검증

씬에 생성기 오브젝트를 만들고 `Pipe.mat`·`courseRoot`·시퀀스를 세팅해 코스를 생성한다. **이전 단일-틈 코스(execute_code로 구운 loose 파이프)는 제거**한다. WideGap·MultiSlot 정적 구간이 제대로 나오는지 플레이로 확인.

**Files:**
- Modify: `Assets/Art/Scenes/FlappyRace.unity` (씬 오브젝트 추가/삭제 — MCP로 조작, 파일 직접 편집 아님)

**Interfaces:**
- Consumes: `FlappyCourseGenerator`(Task 4), `Pipe.mat`.
- Produces: 씬에 `CourseGenerator` 오브젝트(+ 자식 `Course` 루트)와 생성된 코스.

- [ ] **Step 1: 기존 단일-틈 코스 식별**

`find_gameobjects`(UnityMCP, `unity_instance` 명시)로 `FlappyObstacle` 컴포넌트를 가진 기존 오브젝트를 조회한다(이전 세션 execute_code가 만든 위/아래 파이프+립 = 단일-틈 코스). 루트 오브젝트 이름/구조를 기록.
Expected: 이전 코스의 파이프 오브젝트 목록 확보.

- [ ] **Step 2: 기존 단일-틈 코스 삭제**

`manage_gameobject`(action delete, `unity_instance` 명시)로 Step 1에서 찾은 이전 코스 오브젝트(및 그 컨테이너)를 삭제. Bird/카메라/지면/구름/게이트/장식은 건드리지 않는다.
Expected: 씬에 이전 파이프 없음. Bird·환경은 유지.

- [ ] **Step 3: 생성기 오브젝트 생성 + 루트 자식**

`manage_gameobject`로 빈 오브젝트 `CourseGenerator`(위치 원점) 생성, 그 자식으로 빈 `Course` 생성. `CourseGenerator`에 `FlappyCourseGenerator` 컴포넌트 추가.
Expected: `CourseGenerator`(FlappyCourseGenerator 부착) + 자식 `Course` 존재.

- [ ] **Step 4: 인스펙터 값 세팅**

`manage_gameobject`(또는 `manage_components`)로 `FlappyCourseGenerator` 필드 설정:
- `courseRoot` = `Course`(자식 Transform)
- `pipeMaterial` = `Assets/Art/Environment/FlappyRace/Pipe.mat`
- 나머지는 기본값 유지(시퀀스·물리값이 이미 기본에 반영됨).

Expected: 생성기에 머티리얼·루트 연결됨.

- [ ] **Step 5: 코스 생성 (Generate 호출)**

`execute_code`(UnityMCP, `unity_instance` 명시)로 씬의 `FlappyCourseGenerator`를 찾아 `Generate()` 호출:

```csharp
var gen = Object.FindObjectOfType<FlappyCourseGenerator>();
gen.Generate();
Debug.Log($"[Course] generated children = {gen.transform.GetChild(0).childCount}");
```
(주의: `courseRoot`가 `Course` 자식이므로 자식 수는 `Course`의 childCount. 위 로그는 상황에 맞게 courseRoot 기준으로 확인.)
Expected: `Course` 아래 벽/장애물 오브젝트가 시퀀스대로 다수 생성(19벽 기준 정적+동적 컨테이너들). 콘솔 에러 없음.

- [ ] **Step 6: 정적 구간 육안 확인 (에디터/플레이)**

`get_viewport_screenshot`(있으면) 또는 플레이모드 진입(`manage_editor` play) 후 관찰:
- WideGap 벽 = 위/아래 파이프 + 넓은 틈(≈9), 초록.
- MultiSlot 벽 = 슬롯 4개(구멍 4개)로 나뉜 세그먼트.
- 파이프에 립(넓은 캡)이 붙어 있음.

Expected: 정적 구간이 시퀀스대로 배치됨. 파이프 초록(Pipe.mat 적용).

- [ ] **Step 7: 컴파일/런타임 에러 확인**

`read_console`(`types:["error","warning"]`)로 런타임 에러 0 확인.
Expected: 에러 없음.

---

### Task 6: 엔드투엔드 플레이스루 — 통과보장 + 동적 장애물 + 4인 폭 검증/튜닝

전체 혼합 코스를 로컬 새 1마리로 완주 시도하며 ① 모든 구간이 타이밍만 맞추면 통과 가능한지(통과보장), ② 동적 장애물 3종이 정상 동작·유령 페널티 발동, ③ WideGap/MultiSlot이 4인 폭 여유를 주는지 확인·튜닝.

**Files:**
- Modify: (튜닝 시) `FlappyCourseGenerator` 인스펙터 값 — MCP로 조정 후 재Generate. 스크립트 수정이 필요하면 해당 `.cs` 재작성.

**Interfaces:**
- Consumes: Task 5의 생성된 코스, `FlappyPlayer`(기존).
- Produces: 튜닝 완료된 플레이 가능 코스.

- [ ] **Step 1: 플레이모드 완주 시도**

`manage_editor`로 플레이 진입. Space/클릭=플랩, Shift/D=대시로 코스를 끝까지 진행. 각 구간에서 막히는(통과 불가) 지점이 있는지 관찰.
Expected: 정적·동적 모든 구간이 타이밍만 맞추면 통과 가능. 막히면 통과보장 위반 → Step 2.

- [ ] **Step 2: 통과보장 위반 튜닝 (필요 시)**

막히는 구간 유형별 조정 후 재Generate(`execute_code`로 `Generate()` 재호출):
- WideGap/Dynamic 도달 불가 → `reachFactor`를 낮추거나(더 보수적) `wideGapSize`/`movingPipeGap` 확대.
- MultiSlot 슬롯 간 이동 불가 → `slotOffset` 범위(코드의 `Clamp(...,-3,3)`)를 줄이거나 `slotGapSize` 확대.
- 동적: MovingPipe `movingPipeAmp`↓/틈↑, Donut `donutRadius`↑(중앙 구멍 확대)·`donutRotSpeed`↓, Iris `irisAmp`↓(최소 구경 = `irisBaseGap−irisAmp` ≥ 통과 가능 유지).

Expected: 조정 후 해당 구간 통과 가능.

- [ ] **Step 3: 동적 장애물 동작 + 유령 페널티 확인**

플레이 중 관찰:
- MovingPipe: 위아래 왕복. Donut: 회전. Iris: 개폐.
- 각 동적 장애물에 부딪히면 `FlappyPlayer`의 유령 정지(💫)가 발동(HUD 상태 텍스트).

Expected: 세 동적 장애물 모두 모션·충돌 페널티 정상.

- [ ] **Step 4: 4인 폭 검증**

WideGap 틈(≈9)과 MultiSlot 슬롯 폭이 새(콜라이더 지름 1) 4마리가 나란히/추월할 여유를 주는지 수치·육안 확인. 부족하면 `wideGapSize`↑ 또는 `multiSlotCount`/`slotGapSize` 조정 후 재Generate.
Expected: 4인 몸싸움/라인 선택이 성립하는 폭.

- [ ] **Step 5: 최종 에러 확인 + 플레이 종료**

`read_console`(`types:["error"]`) 에러 0 확인 후 `manage_editor`로 플레이 종료. 씬 저장(`manage_scene` save)으로 생성기 오브젝트·설정을 씬에 영속화.
Expected: 에러 없음. 씬에 코스·생성기 저장됨.

---

## Self-Review

**1. Spec coverage** (spec의 각 요구 → 태스크):
- FlappyCourseGenerator(구간 리스트·Generate/Clear·MaxGapDelta·WideGap/MultiSlot/Dynamic) → Task 4. ✅
- FlappyMovingPipe/RotatingDonut/Iris(통과보장 파라미터) → Task 1/2/3. ✅
- 혼합 구간 시퀀스(WideGap×3→MultiSlot×3→Pipe×2→WideGap×2→Donut×2→MultiSlot×3→Iris×2→WideGap×2) → Task 4 기본 `sections`. ✅
- 4인용 지오메트리(넓은 틈·다중슬롯) → Task 4 파라미터 + Task 6 Step 4 검증. ✅
- 통과보장 유지(정적 델타 제한 + 동적 진폭/주기/최소구경) → Task 4 `StepClamped`/`MaxGapDelta`, Task 1~3 파라미터, Task 6 Step 2 튜닝. ✅
- 범위(지오메트리만, netcode/봇 제외, 로컬 1마리 검증) → 플랜 전체가 봇/netcode 무포함, Task 6 로컬 1마리. ✅
- 충돌=기존 FlappyObstacle 재활용 → Task 4 `MakeBox`가 `FlappyObstacle` 부착. ✅
- 이전 단일-틈 코스 정리 → Task 5 Step 1~2. ✅

**2. Placeholder scan:** "TBD"/"적절히"/빈 코드 없음. 모든 스크립트 전체 코드 포함. 튜닝 스텝은 구체 파라미터·방향 명시. ✅

**3. Type consistency:**
- `FlappyMovingPipe`(amplitude/speed/phase), `FlappyRotatingDonut`(rotSpeed), `FlappyIris`(topPipe/bottomPipe/baseGap/amp/speed/phase) — Task 4 생성기가 세팅하는 필드명과 Task 1~3 정의가 일치. ✅
- `BuildPipe`가 `Transform` 반환 → `FlappyIris.topPipe`(Transform)에 대입. ✅
- `MakeBox`가 `GameObject` 반환 → Donut에서 `seg.transform.localRotation` 사용. ✅
- `NewChild`가 `Transform` 반환 → `AddComponent`는 `.gameObject.AddComponent` 로 호출(코드 일치). ✅
- `Clear()`가 `Generate()` 앞에서 호출 — `courseRoot` 기준 동일 `Root`. ✅

이슈 없음.
