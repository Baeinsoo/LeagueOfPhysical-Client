# Flappy Race B2-d1 — 맵을 진짜 코스로 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Flappy 맵 씬을 "기하와 콜라이더만" 남는 정상 맵으로 정리해, 서버가 씬을 읽어도 깨지지 않고 새가 파이프에 실제로 막히게 만든다.

**Architecture:** 맵 씬에서 클라 전용 프로토타입 스크립트를 걷어내고(147개), 통과 가능(트리거)이던 콜라이더 119개를 막히게 바꾸고, 게임 씬과 중복되는 카메라·라이트를 지운다. 플레이어 시작 지점은 **양쪽 프로젝트가 참조하는 패키지**에 마커 컴포넌트를 두어(GUID가 같아야 missing script가 안 된다) 서버 룰이 찾아 쓴다.

**Tech Stack:** Unity 6 (6000.3.16f1) · C# · NUnit EditMode · git 서브모듈(아트) · Addressables 원격 번들

**Spec:** `docs/superpowers/specs/2026-08-17-flappy-race-gameplay-b2-design.md` (§6 = 이 슬라이스, §8 확정된 결정)

## Global Constraints

- **`namespace LOP`** — LOP-Shared의 신규 타입 전부.
- **World 타입은 항상 풀 네임스페이스** — LOP 측 파일에 `using GameFramework.World;`를 넣지 않는다(`Component`가 `UnityEngine.Component`와 겹친다).
- **`.meta`는 유니티가 만든 것만 커밋** — 직접 만들거나 고치지 않는다.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고, 커밋 전 `git status --short`로 확인한다.
- **푸시하지 않는다** — 각 태스크는 피처 브랜치 커밋까지만. 4개 저장소의 머지·푸시는 전 태스크 후 컨트롤러가 사용자와 함께.
- **유니티 저장소에 git worktree를 쓰지 않는다** — 일반 브랜치로 전환.
- 브랜치 이름: 아트 서브모듈은 `feature/flappy-b2d1-map-cleanup`, 나머지 셋은 `feature/flappy-b2d1-map`.
- 주석은 최소·일상어. 코드로 자명한 것은 주석 없이, 비자명한 *의도(왜)* 만 짧게.

### 이 슬라이스가 손대는 저장소 넷

| 저장소 | 경로 | 무엇 |
|---|---|---|
| LeagueOfPhysical-Shared | `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared` | 스폰 마커 타입 + 배치 로직 + 테스트 |
| **LeagueOfPhysical-Art** (서브모듈) | `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Art` | 맵 씬 자체 |
| LeagueOfPhysical-Client | `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client` | 서브모듈 포인터 갱신, 주석 정정, 임시 에디터 도구(커밋 안 함) |
| LeagueOfPhysical-Server | `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server` | 룰이 스폰 마커를 읽게, 주석 정정 |

> **아트가 서브모듈이라는 게 이 슬라이스의 유일한 낯선 점이다.** 맵 씬은 클라 저장소가 아니라
> `Assets/Art` 안에 있고, 그건 별개 git 저장소다. 씬을 고치면 **아트 저장소에 커밋**하고, 그다음
> 클라 저장소가 가리키는 **서브모듈 포인터를 갱신**해 커밋해야 한다. 두 번째를 빠뜨리면 다른
> 머신·CI는 옛 씬을 계속 본다.

### 유니티 저장소의 상시 미커밋 픽스처 (멈추지 말 것)

- **LOP-Client:** `Assets/Art`(서브모듈 포인터), `Assets/UI/Theme/Fonts/Jua-Regular SDF.asset`, `ProjectSettings/ProjectSettings.asset`
- **LOP-Server:** `Assets/DefaultVolumeProfile.asset`, `Assets/URPDefaultResources/*.asset` 7개, `Assets/UniversalRenderPipelineGlobalSettings.asset`, `ProjectSettings/ProjectSettings.asset`, untracked `Assets/Editor.meta`·`Assets/Settings.meta`·`Assets/Settings/`·`Assets/StreamingAssets/`·`GameServer/Build*/`
- **아트 서브모듈:** `Characters/{Archer,Knight,Necromancer}/*.mat`, `Items/ExpMarble/Bottle_green.mat`, `Scenes/floor.mat` (재직렬화 잔상 — **절대 스테이지하지 말 것**)
- **LOP-Shared:** 깨끗해야 한다.

이 목록 **밖의** 변경이 보이면 멈추고 보고한다.

### 유니티 CLI 사용법

```bash
export PATH="$HOME/.unity/bin:$PATH"     # 비대화형 셸에선 프로필이 안 읽힌다
unity cmd eval_file --file <path>        # 열려 있는 에디터에서 C# 실행 (반드시 return)
unity cmd recompile ; unity cmd console  # 컴파일 + 콘솔 읽기
unity cmd run_tests                      # EditMode 전체 (필터 인자 안 먹음)
```

**⚠️ 플레이 모드면 `recompile`이 끝나지 않는다.** 먼저 확인한다:

```bash
echo 'return UnityEditor.EditorApplication.isPlaying;' > /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/is-playing.cs
unity cmd eval_file --file /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/is-playing.cs
```

`True`면 사용자에게 정지를 요청한다 — 임의로 끄면 플레이 중 편집분이 날아간다.

**첫 호출이 `Main thread operation timed out`으로 실패하면 한 번 더 부른다** — 브리지가 붙은 직후엔 흔한 일이고, 두 번째는 대개 붙는다. 안 되면 batchmode 폴백:

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath <PROJECT> -logFile <LOG> [-executeMethod <Type.Method>]
```

---

## File Structure

| 파일 | 책임 |
|---|---|
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SpawnPoint.cs` (신규) | 맵 씬에 찍는 시작 지점 마커. 데이터만(`Order`) |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SpawnPlacement.cs` (신규) | 마커들을 배정 순서대로 세우는 순수 함수 |
| `LeagueOfPhysical-Shared/Tests/EditMode/SpawnPlacementTests.cs` (신규) | 위 정렬 규칙 테스트 |
| `LeagueOfPhysical-Client/Assets/Editor/FlappyMapCleanup.cs` (신규 → **삭제**) | 씬 수술 일회용 도구. 돌리고 지운다(커밋하지 않음) |
| `Assets/Art/Scenes/FlappyRaceMap.unity` (수정, **아트 저장소**) | 정리 대상 |
| `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` (수정) | 이제 틀린 주석 정정 |
| `LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` (수정) | 같음 |
| `LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceRuleSystem.cs` (수정) | 스폰 마커를 읽어 새를 세운다 |

## 이 슬라이스가 하지 않는 것 (B2-d2 몫)

- 클라 새에 `Simulated` 붙이기 — 그래서 **끝나도 클라는 여전히 자기 새를 시뮬하지 않는다.** 서버만 돈다.
- 몸 규격 통일(`PhysicsFollower`의 0.35/1.5 ↔ config의 0.45/0.9)
- 플랩을 누를 UI
- 프로토타입 스크립트 **파일** 삭제 — 이번엔 *씬에서* 떼어내기만 한다. 파일은 죽은 코드로 남고, 지우는 건 별도 정리다(`Logic/` 폴더의 순수 로직과 그 테스트는 남길 것이라 판단이 필요하다).

## 완료 뒤의 검증 (컨트롤러 + 사용자, 태스크 아님)

정적 검증(Task 2)까지가 서브에이전트의 몫이다. **살아 있는 확인**은 콘텐츠 배포 CI와 k8s가 걸려 있어 컨트롤러가 사용자와 함께 한다:

1. 아트·클라 푸시 후 `gh workflow run content-deploy -f target=gameserver` (CI가 이 맥에서 돈다 — **유니티 에디터를 닫아야 한다**)
2. 로비에서 FlappyRace 입장 → 파드 로그에서 `The referenced script (Unknown) ... is missing!` **0건**(전 588건), `InjectSceneObjects` NRE **0건**
3. 새가 스폰 마커 자리(x≈-2)에서 시작하고 파이프에 막히는지

---

### Task 1: 스폰 지점 마커와 배치 규칙

**Files:**
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/SpawnPoint.cs`
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/SpawnPlacement.cs`
- Test: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/SpawnPlacementTests.cs`

**Interfaces:**
- Consumes: 없음.
- Produces:
  - `LOP.SpawnPoint : MonoBehaviour` — `public int Order;` (직렬화되는 public 필드)
  - `LOP.SpawnPlacement.Arrange(IEnumerable<SpawnPoint> points)` → `List<UnityEngine.Vector3>`
  Task 2의 수술 도구가 `SpawnPoint`를 붙이고, Task 3의 서버 룰이 `Arrange`를 부른다.

**왜 공용 패키지에 두는가:** 맵 씬은 클라에서 만들고 **서버가 읽는다.** 스크립트가 한쪽에만 있으면 반대쪽에서 missing script가 되고, 그 null 컴포넌트가 씬 주입을 NRE로 끊는다 — 지금 고치고 있는 그 문제다(스펙 §3에서 588건 실측). 양쪽이 같은 패키지를 참조하면 GUID가 같아 그 일이 안 생긴다. 산업 표준으로는 Unreal의 `APlayerStart`에 대응한다(시작 지점을 클래스로 두고 게임모드가 찾아 쓴다).

**왜 이름이 아니라 `Order`인가:** `FindObjectsByType`이 돌려주는 순서는 보장되지 않는다. 정렬 기준이 없으면 실행할 때마다 누가 어느 자리에서 시작할지 달라진다.

- [ ] **Step 1: 브랜치 생성**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git fetch origin
git checkout -b feature/flappy-b2d1-map origin/main
```

`git status --short`가 비어 있지 않으면 멈추고 보고한다(이 저장소는 깨끗해야 한다).

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/SpawnPlacementTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class SpawnPlacementTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            created.Clear();
        }

        private SpawnPoint Marker(string name, int order, Vector3 position)
        {
            var go = new GameObject(name);
            created.Add(go);
            go.transform.position = position;
            var point = go.AddComponent<SpawnPoint>();
            point.Order = order;
            return point;
        }

        [Test]
        public void 찾은_순서와_무관하게_Order_순으로_세운다()
        {
            // 이름 순서를 Order와 **거꾸로** 매긴다 — 이름으로 정렬하는 구현이 통과해 버리면
            // 이 테스트는 아무것도 지키지 못한다. 찾아오는 순서도 일부러 뒤섞는다.
            var points = new List<SpawnPoint>
            {
                Marker("A", 3, new Vector3(0f, 4f, 0f)),
                Marker("C", 1, new Vector3(0f, -6f, 0f)),
                Marker("B", 2, new Vector3(0f, -1f, 0f)),
            };

            var slots = SpawnPlacement.Arrange(points);

            Assert.AreEqual(3, slots.Count);
            Assert.AreEqual(-6f, slots[0].y, 1e-4f);
            Assert.AreEqual(-1f, slots[1].y, 1e-4f);
            Assert.AreEqual(4f, slots[2].y, 1e-4f);
        }

        [Test]
        public void Order가_같으면_이름을_바이트_순서로_갈라_순서가_흔들리지_않는다()
        {
            // 대문자 'B'(66)가 소문자 'a'(97)보다 앞인 것은 **바이트 순서**로 볼 때뿐이다.
            // 언어권 규칙으로 비교하면 'a'가 먼저 온다 — 그래서 이 쌍이라야 둘을 구분한다.
            // (언어권 비교는 실행 환경의 지역 설정에 따라 달라질 수 있어 시뮬에는 못 쓴다.)
            var points = new List<SpawnPoint>
            {
                Marker("a", 1, new Vector3(0f, 1f, 0f)),
                Marker("B", 1, new Vector3(0f, 2f, 0f)),
            };

            var slots = SpawnPlacement.Arrange(points);

            Assert.AreEqual(2f, slots[0].y, 1e-4f);   // B
            Assert.AreEqual(1f, slots[1].y, 1e-4f);   // a
        }

        [Test]
        public void 마커가_없으면_빈_목록을_돌려준다()
        {
            Assert.IsEmpty(SpawnPlacement.Arrange(new List<SpawnPoint>()));
        }

        [Test]
        public void 목록_자체가_null이어도_빈_목록을_돌려준다()
        {
            Assert.IsEmpty(SpawnPlacement.Arrange(null));
        }

        [Test]
        public void 사라진_마커는_건너뛴다()
        {
            // 씬이 바뀌는 도중에 부르면 목록에 파괴된 오브젝트가 섞일 수 있다
            var alive = Marker("alive", 1, new Vector3(0f, 5f, 0f));
            var doomed = Marker("doomed", 2, new Vector3(0f, 9f, 0f));
            Object.DestroyImmediate(doomed.gameObject);

            var slots = SpawnPlacement.Arrange(new List<SpawnPoint> { alive, doomed });

            Assert.AreEqual(1, slots.Count);
            Assert.AreEqual(5f, slots[0].y, 1e-4f);
        }
    }
}
```

- [ ] **Step 3: 실패를 확인한다**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile; unity cmd console | grep "error CS" | head -3
```

기대: `SpawnPoint`/`SpawnPlacement`가 없다는 컴파일 오류. **오류가 없으면 멈춘다** — 테스트가 실패할 수 있음을 확인하는 것이 이 스텝의 목적이다.

- [ ] **Step 4: `SpawnPoint`를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/SpawnPoint.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 맵 씬에 찍어 두는 플레이어 시작 지점. 게임 룰이 매치를 시작할 때 찾아 쓴다.
    ///
    /// 이 마커가 <b>공용 패키지</b>에 있는 이유: 맵 씬은 클라에서 만들고 서버가 읽는데, 스크립트가
    /// 한쪽에만 있으면 반대쪽에서 missing script가 되고 그 빈 컴포넌트가 씬 주입을 끊는다.
    /// 양쪽이 같은 패키지를 참조하면 GUID가 같아 그 일이 생기지 않는다.
    /// (Unreal의 APlayerStart에 대응 — 시작 지점을 클래스로 두고 게임모드가 찾아 쓰는 방식.)
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        /// <summary>배정 순서. 작을수록 먼저 쓴다. 씬에서 찾아오는 순서는 보장되지 않아 이 값이 필요하다.</summary>
        public int Order;
    }
}
```

- [ ] **Step 5: `SpawnPlacement`를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/SpawnPlacement.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LOP
{
    /// <summary>맵에 찍힌 시작 지점 마커를 배정 순서대로 세운다.</summary>
    public static class SpawnPlacement
    {
        /// <summary>
        /// <see cref="SpawnPoint.Order"/> 오름차순으로 자리를 세운다. Order가 같으면 오브젝트 이름으로
        /// 가른다 — 그러지 않으면 찾아온 순서가 그대로 남아 실행할 때마다 자리가 바뀔 수 있다.
        /// </summary>
        public static List<Vector3> Arrange(IEnumerable<SpawnPoint> points)
        {
            if (points == null)
            {
                return new List<Vector3>();
            }

            return points
                .Where(point => point != null)
                .OrderBy(point => point.Order)
                .ThenBy(point => point.name, System.StringComparer.Ordinal)
                .Select(point => point.transform.position)
                .ToList();
        }
    }
}
```

- [ ] **Step 6: 테스트 통과 확인**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd console | grep -c "error CS"
unity cmd run_tests
```

기대: `error CS` 0건. `SpawnPlacementTests` 4개 통과. EditMode 전체 실패 0 (이 태스크 전 518 → 522).

- [ ] **Step 7: 테스트가 진짜로 실패할 수 있는지 확인한다**

`SpawnPlacement.Arrange`의 `.OrderBy(point => point.Order)`를 `.OrderByDescending(point => point.Order)`로 잠깐 바꾼다. `찾은_순서와_무관하게_Order_순으로_세운다`가 **실패해야 한다**. 확인했으면 되돌린다.

(줄을 아예 지우면 안 된다 — 뒤따르는 `ThenBy`가 `OrderBy`를 요구해서 컴파일이 깨지고, 그러면
"테스트가 실패하는지"가 아니라 "빌드가 깨지는지"를 본 것이 된다.)

통과만 보고 "검증됐다"고 하지 않는다 — 일부러 깨뜨려 본다.

- [ ] **Step 8: `.meta` 확인 후 커밋**

```bash
ls -l /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/SpawnPoint.cs.meta \
      /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/SpawnPlacement.cs.meta \
      /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/SpawnPlacementTests.cs.meta
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/SpawnPoint.cs Runtime/Scripts/Game/SpawnPoint.cs.meta \
        Runtime/Scripts/Game/SpawnPlacement.cs Runtime/Scripts/Game/SpawnPlacement.cs.meta \
        Tests/EditMode/SpawnPlacementTests.cs Tests/EditMode/SpawnPlacementTests.cs.meta
git commit -m "feat(map): 맵이 플레이어 시작 지점을 정하게 한다

마커를 공용 패키지에 두는 이유는 맵 씬을 클라가 만들고 서버가 읽기 때문이다 —
스크립트가 한쪽에만 있으면 반대쪽에서 missing script가 되고 씬 주입이 끊긴다."
```

`.meta` 셋이 없으면 유니티 임포트가 안 돈 것이니 `unity cmd refresh` 후 다시 확인한다.

---

### Task 2: 맵 씬 수술

**Files:**
- Create → **삭제**: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Editor/FlappyMapCleanup.cs`
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Art/Scenes/FlappyRaceMap.unity` (**아트 저장소**)
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client` 의 서브모듈 포인터 `Assets/Art`

**Interfaces:**
- Consumes: `LOP.SpawnPoint`(Task 1) — 수술 도구가 스폰 마커에 붙인다.
- Produces: 정리된 `FlappyRaceMap.unity`. Task 3의 서버 룰이 이 씬에서 `SpawnPoint` 4개를 찾는다.

**지금 씬에 뭐가 있는지 (실측)**

| 스크립트 | 개수 | 붙은 곳 | 처리 |
|---|---|---|---|
| `FlappyObstacle` | 118 | `Cube`×72, `ArmN/S/E/W_marker` 등 | 스크립트만 제거 |
| `FlappyWindmill` | 8 | `Windmill`, `FillWindmill` | 스크립트만 제거 |
| `FlappyBird` | 4 | `Player`, `Pacer_*` | 오브젝트째 삭제 |
| `FlappyPacer` | 3 | `Pacer_Cyan/Red/Yellow` | 오브젝트째 삭제 |
| `FlappyIris` | 2 | `Iris`, `FillIris` | 스크립트만 제거 |
| `FlappyPlayer`·`FlappyAutoPilot`·`FlappyPlayRecorder`·`FlappyDashFx` | 4 | 전부 `Player` | 오브젝트째 삭제 |
| `FlappyCameraFollow` | 1 | `Main Camera` | 오브젝트째 삭제 |
| `FlappyHUD`·`FlappyRaceManager`·`FlappySimJudge`·`FlappyRaceStart`·`FlappyChaser` | 5 | 각자 동명 오브젝트 | 오브젝트째 삭제 |
| `FlappyCourseGenerator` | 1 | `---Course---` | 스크립트만 제거(코스 루트라 오브젝트는 남긴다) |
| `FlappyBoostZone` | 1 | `BoostHole` | 스크립트만 제거 |

그 밖에: `BoxCollider` 119개가 전부 `m_IsTrigger: 1`, `Main Camera` 1개, `Directional Light` 1개, `PlayerSpawn_1~4`(부모 `---Players---`, 전부 **비활성**, 위치 x=-2 / y=-6,-1,4,9 / z=0).

**`FlappyCourseGenerator`를 지워도 되는 이유:** `ContextMenu`로 도는 **에디터 전용 도구**이고(`Awake`/`Start` 없음) 코스 기하는 이미 씬에 구워져 있다. 인스펙터에 넣어 둔 생성 설정은 **git 히스토리에 남으므로**, 나중에 코스를 다시 굽고 싶으면 옛 커밋의 씬에서 값을 꺼내 오면 된다.

**목표 모양:** `FlapWangMap.unity`(정상 맵)는 MonoBehaviour 0 · 카메라 0 · 라이트 0이다. 게임 씬 `FlappyRace.unity`가 카메라·라이트·오디오리스너를 이미 갖고 있다.

- [ ] **Step 1: 아트 저장소에 브랜치를 만든다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Art
git status --short
git fetch origin
git checkout -b feature/flappy-b2d1-map-cleanup origin/main
```

`git status --short`에 아래 다섯만 보여야 한다(재직렬화 잔상 — **절대 스테이지하지 말 것**):
`Characters/Archer/Archer.mat`, `Characters/Knight/Knight.mat`, `Characters/Necromancer/Necromancer.mat`, `Items/ExpMarble/Bottle_green.mat`, `Scenes/floor.mat`.
그 밖의 것이 보이면 멈추고 보고한다.

브랜치 전환이 이 파일들 때문에 거부되면 `git stash push -u -m b2d1` 후 전환하고 `git stash pop`.

- [ ] **Step 2: 수술 전 상태를 숫자로 찍어 둔다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
S=Assets/Art/Scenes/FlappyRaceMap.unity
echo "MonoBehaviour: $(grep -c 'm_Script: {fileID: 11500000' $S)"
echo "trigger=1:     $(grep -c 'm_IsTrigger: 1' $S)"
echo "Camera:        $(grep -c -- '--- !u!20 &' $S)"
echo "Light:         $(grep -c -- '--- !u!108 &' $S)"
```

기대: `147 / 119 / 1 / 1`. 숫자가 다르면 씬이 그 사이 바뀐 것이니 멈추고 보고한다.

- [ ] **Step 3: 수술 도구를 만든다**

`LeagueOfPhysical-Client/Assets/Editor/FlappyMapCleanup.cs` — **일회용이고, 돌린 뒤 지운다. 커밋하지 않는다.**

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// FlappyRaceMap 씬을 "기하와 콜라이더만" 남는 맵 씬으로 정리하는 일회용 도구.
/// 프로토타입 씬에서 그대로 승격돼 클라 전용 스크립트가 잔뜩 붙어 있는데, 서버엔 그 타입이 없어
/// missing script가 되고 씬 주입이 끊긴다. 돌리고 나면 이 파일은 지운다.
/// </summary>
public static class FlappyMapCleanup
{
    private const string ScenePath = "Assets/Art/Scenes/FlappyRaceMap.unity";
    private const string MarkerPrefix = "PlayerSpawn_";

    // 오브젝트째 지울 것 — 프로토타입 전용 액터·시스템(플레이어, 페이서, HUD 등)
    private static readonly HashSet<string> DeleteObject = new HashSet<string>
    {
        "FlappyBird", "FlappyPacer", "FlappyPlayer", "FlappyAutoPilot", "FlappyPlayRecorder",
        "FlappyDashFx", "FlappyCameraFollow", "FlappyHUD", "FlappyRaceManager",
        "FlappySimJudge", "FlappyRaceStart", "FlappyChaser",
    };

    // 스크립트만 뗄 것 — 기하는 코스 그 자체라 남긴다
    private static readonly HashSet<string> StripComponent = new HashSet<string>
    {
        "FlappyObstacle", "FlappyWindmill", "FlappyIris", "FlappyCourseGenerator", "FlappyBoostZone",
    };

    public static string Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        int deletedObjects = 0;
        foreach (var go in AllGameObjects(scene))
        {
            if (go == null)
            {
                continue;   // 부모가 먼저 지워져 같이 사라진 것
            }
            bool prototypeActor = go.GetComponents<MonoBehaviour>()
                .Any(mb => mb != null && DeleteObject.Contains(mb.GetType().Name));
            if (prototypeActor)
            {
                Object.DestroyImmediate(go);
                deletedObjects++;
            }
        }

        // 게임 씬이 카메라·라이트를 이미 갖고 있다. 맵에도 있으면 둘이 겹친다(FlapWang 맵엔 없다).
        int cameras = 0;
        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(camera.gameObject);
            cameras++;
        }
        int lights = 0;
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(light.gameObject);
            lights++;
        }

        int strippedComponents = 0;
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb != null && StripComponent.Contains(mb.GetType().Name))
            {
                Object.DestroyImmediate(mb);
                strippedComponents++;
            }
        }

        // 트리거 콜라이더는 sweep이 걸러 버린다(QueryTriggerInteraction.Ignore) — 막히게 하려면 솔리드여야 한다.
        int solidified = 0;
        foreach (var collider in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (collider.isTrigger)
            {
                collider.isTrigger = false;
                solidified++;
            }
        }

        int markers = 0;
        foreach (var go in AllGameObjects(scene))
        {
            if (go == null || !go.name.StartsWith(MarkerPrefix))
            {
                continue;
            }
            go.SetActive(true);
            LOP.SpawnPoint point = go.GetComponent<LOP.SpawnPoint>();
            if (point == null)
            {
                point = go.AddComponent<LOP.SpawnPoint>();
            }
            point.Order = int.Parse(go.name.Substring(MarkerPrefix.Length));
            markers++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        return $"deletedObjects={deletedObjects} cameras={cameras} lights={lights} "
             + $"strippedComponents={strippedComponents} solidified={solidified} markers={markers}";
    }

    // 비활성 오브젝트까지 전부 — 스폰 마커가 꺼져 있어서 활성만 훑으면 놓친다.
    private static List<GameObject> AllGameObjects(UnityEngine.SceneManagement.Scene scene)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject)
            .ToList();
    }
}
```

- [ ] **Step 4: 수술을 돌린다**

에디터가 플레이 중이 아닌지 먼저 확인한 뒤(위 "유니티 CLI 사용법"), 컴파일하고 실행한다.

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd console | grep -c "error CS"
SP=/private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad
echo 'return FlappyMapCleanup.Run();' > "$SP/run-cleanup.cs"
unity cmd eval_file --file "$SP/run-cleanup.cs"
```

기대(손으로 세어 둔 값):

```
deletedObjects=10 cameras=0 lights=1 strippedComponents=130 solidified=119 markers=4
```

셈은 이렇다:

- `deletedObjects=10` — `Player`, `Pacer_Cyan/Red/Yellow`(3), `Main Camera`, `FlappyHUD`,
  `RaceManager`, `SimJudge`, `FlappyRaceStart`, `Chaser`. **`Main Camera`는 `FlappyCameraFollow`가
  붙어 있어 여기서 먼저 지워진다** — 그래서 다음 줄의 `cameras=0`이다.
- `lights=1` — `Directional Light`.
- `strippedComponents=130` — `FlappyObstacle` 118 + `FlappyWindmill` 8 + `FlappyIris` 2 +
  `FlappyCourseGenerator` 1 + `FlappyBoostZone` 1.
- `solidified=119` — 트리거 `BoxCollider` 전부. (프로토타입 새의 `SphereCollider` 4개는 트리거가
  아니었고, 그 오브젝트는 이미 지워졌다.)

**숫자가 기대와 다르면 멈추지 말고 Step 5의 정적 검증으로 판정한다** — 최종 상태가 맞으면
통과다. 다만 실제 출력값을 보고서에 그대로 적는다.

`eval_file`이 `Main thread operation timed out`으로 실패하면 한 번 더 부른다(첫 호출 실패는 흔하다). 그래도 안 되면 배치모드로:

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client \
  -executeMethod FlappyMapCleanup.Run \
  -logFile /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/cleanup.log
```

(배치모드로 돌리려면 유니티 에디터가 닫혀 있어야 한다 — 열려 있으면 사용자에게 요청하고 그동안 멈춘다.)

- [ ] **Step 5: 저장된 씬 파일로 검증한다 (메모리가 아니라 파일)**

**씬 검증은 반드시 저장된 YAML을 본다.** 에디터 메모리로 세면 옛 컴포넌트 슬롯이 null로 보여 "0개"로 나오는데 파일에는 남아 있는 사고가 이 프로젝트에서 실제로 있었다.

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
S=Assets/Art/Scenes/FlappyRaceMap.unity
GUID=$(awk '/^guid:/{print $2}' /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/SpawnPoint.cs.meta)
echo "MonoBehaviour 총 개수 (기대 4): $(grep -c 'm_Script: {fileID: 11500000' $S)"
echo "그중 SpawnPoint (기대 4):       $(grep -c "m_Script: {fileID: 11500000, guid: $GUID" $S)"
echo "trigger=1 (기대 0):             $(grep -c 'm_IsTrigger: 1' $S)"
echo "Camera (기대 0):                $(grep -c -- '--- !u!20 &' $S)"
echo "Light (기대 0):                 $(grep -c -- '--- !u!108 &' $S)"
echo "PlayerSpawn 오브젝트 (기대 4):  $(grep -c 'm_Name: PlayerSpawn_' $S)"
echo "--- 스폰 마커가 켜졌는지 ---"
python3 - <<'PY'
import re
txt=open("Assets/Art/Scenes/FlappyRaceMap.unity",encoding="utf-8",errors="replace").read()
for d in re.split(r"^--- ",txt,flags=re.M)[1:]:
    if re.match(r"!u!1 &",d) and "m_Name: PlayerSpawn_" in d:
        name=re.search(r"^  m_Name: (.*)$",d,re.M).group(1).strip()
        active=re.search(r"^  m_IsActive: (\d)",d,re.M).group(1)
        print(f"{name}: active={active} (기대 1)")
PY
```

여섯 숫자와 네 마커가 전부 기대와 같아야 한다. **하나라도 다르면 멈추고 보고한다.**

> **정정 (최종 리뷰 Finding A, 2026-08-22).** 위에서 마커를 켠(`active=1`) 것이 문제였다 —
> 마커 자식(Halo, `Bird_PN` 프리팹 인스턴스)까지 함께 켜져 출발선에 가짜 새 네 마리가 보였다.
> 서버 룰은 `FindObjectsInactive.Include`로 찾으므로 마커는 꺼둬도 된다. 이 문제를 고치는
> 수정 웨이브에서 네 마커를 다시 `m_IsActive: 0`으로 되돌렸다(`SpawnPoint`/`Order`는 그대로).
> 최종 완료 기준은 위 "완료 기준" 절의 정정된 문구를 따른다.

- [ ] **Step 6: 수술 도구를 지운다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
rm -f Assets/Editor/FlappyMapCleanup.cs Assets/Editor/FlappyMapCleanup.cs.meta
export PATH="$HOME/.unity/bin:$PATH"
unity cmd recompile && unity cmd console | grep -c "error CS"
git status --short | grep FlappyMapCleanup && echo "!! 아직 남아 있다" || echo "OK: 흔적 없음"
```

기대: `error CS` 0건, `OK: 흔적 없음`.

- [ ] **Step 7: 아트 저장소에 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Art
git status --short
git add Scenes/FlappyRaceMap.unity
git status --short   # 스테이지된 것이 씬 하나뿐인지 눈으로 확인 (.mat 다섯 개가 섞이면 안 된다)
git commit -m "chore(flappy): 레이스 맵을 기하와 콜라이더만 남는 맵 씬으로 정리한다

프로토타입 씬에서 그대로 승격되는 바람에 클라 전용 스크립트가 잔뜩 붙어 있었다.
서버엔 그 타입이 없어 missing script가 되고 씬 주입이 끊긴다(588건 실측).

콜라이더는 전부 트리거였다 — 프로토타입이 닿으면 잠깐 멈추는 페널티를 트리거로
만들던 시절의 흔적이다. sweep은 트리거를 걸러 버리므로 그대로 두면 새가 파이프를
그냥 통과한다.

카메라·라이트는 게임 씬이 이미 갖고 있어 중복이다(FlapWang 맵에도 없다).
스폰 마커는 켜고 SpawnPoint를 붙였다."
```

- [ ] **Step 8: 클라 저장소의 서브모듈 포인터를 갱신한다**

이 단계를 빠뜨리면 **다른 머신과 CI는 옛 씬을 계속 본다.**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git checkout -b feature/flappy-b2d1-map origin/main 2>/dev/null || git checkout feature/flappy-b2d1-map
git add Assets/Art
git status --short   # 스테이지된 것이 Assets/Art 하나뿐인지 확인
git commit -m "chore(art): 정리된 레이스 맵 씬을 가리키게 한다"
```

> 참고: 클라 저장소에서 `Assets/Art`는 평소 "커밋하지 않는 로컬 픽스처"로 취급하지만, **아트에
> 의도한 커밋을 올린 지금은 포인터를 앞으로 옮기는 게 정상 절차다.** 옮긴 뒤에도 아트 워킹트리의
> `.mat` 다섯 개 때문에 서브모듈이 계속 dirty로 보이는 것은 정상이다.

---

### Task 3: 서버가 스폰 지점을 쓰게 하고, 틀린 주석을 고친다

**Files:**
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceRuleSystem.cs`
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`

**Interfaces:**
- Consumes: `LOP.SpawnPoint`(Task 1), `LOP.SpawnPlacement.Arrange(IEnumerable<SpawnPoint>)` → `List<Vector3>`(Task 1), 정리된 맵 씬(Task 2).
- Produces: 없음 (이 슬라이스의 마지막 태스크).

**지금 서버가 하는 일:** `FlappyRaceRuleSystem.Initialize`가 참가자마다 새를 `(0, i*2, 0)`에 세운다 — 맵을 보지 않는다. 맵의 스폰 마커는 x=-2, y=-6/-1/4/9에 있다.

**호출 시점이 맞는가:** `LOPRunner.InitializeAsync`가 `await mapLoadTask` 다음에 `gameRuleSystem.Initialize()`를 부른다 — 즉 맵 씬이 이미 로드된 뒤다. 마커를 찾을 수 있다.

- [ ] **Step 1: 브랜치를 만든다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git status --short
git fetch origin
git checkout -b feature/flappy-b2d1-map origin/main
```

미커밋 픽스처 목록은 위 "Global Constraints"를 볼 것. 그 밖의 것이 보이면 멈추고 보고한다. 브랜치 전환이 거부되면 `git stash push -u -m b2d1` → 전환 → `git stash pop`.

- [ ] **Step 2: 서버 룰이 마커를 읽게 한다**

`LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceRuleSystem.cs`의 `Initialize`를 아래로 바꾼다:

```csharp
        public void Initialize()
        {
            //  시작 지점은 맵이 정한다 — 룰이 좌표를 들고 있으면 맵을 새로 만들 때마다 룰을 고쳐야 한다.
            //  비활성 마커까지 찾는다: 마커는 보일 필요가 없어 꺼 둘 수도 있다.
            var slots = SpawnPlacement.Arrange(
                Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            if (slots.Count == 0)
            {
                Debug.LogWarning("[FlappyRace] 맵에 SpawnPoint가 없다 — 원점에 세로로 세운다");
            }

            var playerList = roomDataStore.match.playerList;
            for (int i = 0; i < playerList.Length; i++)
            {
                //  자리가 사람보다 적으면 앞에서부터 다시 쓴다. 겹쳐 서긴 해도 아무도 맵 밖에 나지 않는다.
                Vector3 position = slots.Count > 0
                    ? slots[i % slots.Count]
                    : new Vector3(0f, i * SpawnSpacingY, 0f);

                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = playerList[i],
                    entityId = entitySpawner.GenerateEntityId(),
                    visualId = BirdVisualId,
                    characterCode = "",
                    position = position,
                    rotation = Vector3.zero,
                    velocity = Vector3.zero,
                });
            }
        }
```

`SpawnSpacingY` 상수는 폴백에서 계속 쓰므로 지우지 않는다. 파일 맨 위 주석(`private const float SpawnSpacingY = 2f;` 위)을 아래로 바꾼다:

```csharp
        // 맵에 스폰 마커가 없을 때만 쓰는 폴백 간격. 같은 자리에 겹쳐 세우면 누가 누군지 안 보인다.
```

- [ ] **Step 3: 이제 틀린 주석 두 개를 고친다**

클라 `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`와 서버 `LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` **양쪽**에서, `FlappyWorld` 등록 앞에 붙은 주석 블록을 아래로 바꾼다. (두 파일의 현재 문구는 같다 — 새 문구도 양쪽 동일하게 넣는다.)

바꾸기 전(양쪽 공통):

```csharp
            // sweep이 볼 것은 맵 지오메트리뿐이다 — 새끼리는 물리엔진이 아니라 우리 계산으로 민다.
            // 이 가정은 "새는 Default 레이어에 있으면 안 된다"를 전제로 한다 — 지켜지지 않으면
            // 다른 새가 sweep 벽이 되어 몸싸움이 두 군데(PhysX + 우리 계산)에서 이중으로 돈다.
            // 새 프리팹을 전용 레이어(예: Character)로 옮기고 이 마스크에서 빼둘 것(B2-d 숙제).
```

바꾼 뒤:

```csharp
            // sweep이 볼 것은 맵 지오메트리뿐이다 — 새끼리는 물리엔진이 아니라 우리 계산으로 민다.
            // 새의 물리 몸은 PhysicsFollower가 만들면서 무조건 Character 레이어에 둔다. 그래서 이
            // 마스크에 Character가 없는 한 새끼리는 sweep에 걸리지 않는다.
            // (겉모습 프리팹 Bird.prefab에는 콜라이더가 없어 물리에는 아예 존재하지 않는다.)
```

- [ ] **Step 4: 양쪽 컴파일과 테스트를 확인한다**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd console | grep -c "error CS"
unity cmd run_tests
```

기대: `error CS` 0건, EditMode 전체 실패 0(522개).

서버 프로젝트도 확인한다. 서버 에디터에 CLI가 붙지 않으면 배치모드로:

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -runTests -testPlatform EditMode \
  -projectPath /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server \
  -logFile /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/server-tests.log \
  -testResults /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/server-tests.xml
grep -o 'result="[^"]*"' /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/server-tests.xml | head -1
```

기대: 첫 줄이 `result="Passed"`.

- [ ] **Step 5: 서버가 실제로 마커를 찾는지, 씬을 열어 확인한다**

컴파일만으로는 "찾긴 찾나"를 모른다. 클라 에디터에서 맵 씬을 열어 마커가 조회되는지 본다(서버와 같은 타입·같은 씬이다).

```bash
export PATH="$HOME/.unity/bin:$PATH"
SP=/private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad
cat > "$SP/check-spawns.cs" <<'EOF'
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
    "Assets/Art/Scenes/FlappyRaceMap.unity",
    UnityEditor.SceneManagement.OpenSceneMode.Single);
var found = UnityEngine.Object.FindObjectsByType<LOP.SpawnPoint>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
var slots = LOP.SpawnPlacement.Arrange(found);
return $"found={found.Length} " + string.Join(" | ", slots.ConvertAll(v => $"({v.x},{v.y},{v.z})"));
EOF
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd eval_file --file "$SP/check-spawns.cs"
```

기대: `found=4` 이고 자리가 **y 오름차순**으로 `(-2,-6,0) | (-2,-1,0) | (-2,4,0) | (-2,9,0)`.

y 순서가 뒤섞여 있으면 `Order`가 잘못 붙은 것이니 멈추고 보고한다.

- [ ] **Step 6: 커밋 (2개 저장소)**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add Assets/Scripts/Game/FlappyRaceRuleSystem.cs Assets/Scripts/Game/FlappyRaceLifetimeScope.cs
git commit -m "feat(flappy): 새를 맵이 정한 자리에서 출발시킨다

룰이 좌표를 들고 있으면 맵을 새로 만들 때마다 룰을 고쳐야 한다.
마커가 없는 맵을 대비해 옛 방식(원점에 세로로)은 폴백으로 남겼다.

sweep 레이어마스크 주석도 사실에 맞게 고쳤다 — 새의 물리 몸은 PhysicsFollower가
만들면서 이미 Character 레이어에 두므로, 원래 적혀 있던 'B2-d 숙제'는 할 일이 아니다."

cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git branch --show-current   # feature/flappy-b2d1-map 이어야 한다 (Task 2 Step 8에서 만들었다)
git status --short
git add Assets/Scripts/Game/FlappyRaceLifetimeScope.cs
git commit -m "docs(flappy): sweep 레이어마스크 주석을 사실에 맞게 고친다"
```

---

## 완료 기준

- [ ] 저장된 `FlappyRaceMap.unity`에서: MonoBehaviour 4개(전부 `SpawnPoint`) · 트리거 콜라이더 0 · 카메라 0 · 라이트 0 · `PlayerSpawn_1~4` 전부 **비활성**(자식 장식이 출발선에 그려지지 않게 — 서버 룰은 `FindObjectsInactive.Include`로 찾으므로 꺼둬도 된다. 최종 리뷰 Finding A로 되돌림)
- [ ] `SpawnPlacementTests` 4개 통과, 일부러 깨뜨렸을 때 실패하는 것 확인
- [ ] 클라 EditMode 522/522, 서버 EditMode 통과
- [ ] 씬을 열어 조회했을 때 마커 4개가 y 오름차순으로 나옴
- [ ] 수술 도구(`Assets/Editor/FlappyMapCleanup.cs`)가 흔적 없이 사라짐
- [ ] 아트 저장소 커밋에 씬 하나만, `.mat` 다섯 개는 안 섞임
- [ ] 클라 저장소가 새 아트 커밋을 가리킴
- [ ] 4개 저장소 각각 피처 브랜치에 커밋됨 (푸시는 아직)

## 태스크가 끝난 뒤 (컨트롤러가 사용자와 함께)

> **정정 (최종 리뷰 Finding C, 2026-08-22).** 아래 원래 절차는 `content-deploy`만 돌렸는데,
> **Task 3은 서버 C#(`FlappyRaceRuleSystem.cs`)을 바꿨다.** `content-deploy`는 Addressables
> 콘텐츠를 구워 S3에 올릴 뿐 서버 바이너리는 만들지 않는다 — 서버 바이너리는
> `LeagueOfPhysical-Server/.github/workflows/gameserver-deploy.yml`이 만든다. 그것만 돌리면
> **맵은 깨끗한데 새는 여전히 원점에 스폰되고**, 그 증상은 "`SpawnPlacement`가 마커를 못 찾았다"처럼
> 보여 원인을 엉뚱한 데서 찾게 된다. 또한 두 워크플로 모두 `LeagueOfPhysical-Shared`를
> **원격에서** 클론하므로, `SpawnPoint.cs`가 푸시되기 전에 CI가 돌면 구워진 맵 번들 안의
> `SpawnPoint` 네 개가 missing script가 되어 서버 조회 결과 0 → 조용히 원점 폴백(지금 고치고
> 있는 그 버그의 축소판). 아래 순서는 이 두 가지를 반영해 고쳤다.

1. 4개 저장소를 `CLAUDE.md`의 푸시 규약대로 **이 순서로** 머지·푸시: **Shared → Art → Client → Server.**
   (Art가 Client보다 먼저여야 원격 Client가 없는 아트 커밋을 가리키지 않는다. Shared가 두 CI 중
   어느 쪽보다도 먼저여야 `SpawnPoint` missing script가 안 난다.) 한 저장소씩 결과를 확인하고 다음으로 넘어간다.
2. `gh workflow run gameserver-deploy` (`LeagueOfPhysical-Server` 레포) — 서버 코드 변경분을 실제로 배포한다.
3. `gh workflow run content-deploy -f target=gameserver` — 맵 콘텐츠를 구워 올린다.
4. 2·3 둘 다 이 맥의 self-hosted 러너에서 도니 **먼저 유니티 에디터를 닫고** 돌린다(에디터가 떠 있으면 Burst 단계가 네이티브 크래시를 낸다).
5. 로비에서 FlappyRace 입장 → 파드 로그에서 missing script 0건(전 588건) · `InjectSceneObjects` NRE 0건 확인, 새가 x≈-2(마커 자리)에서 스폰되는지 확인.
   **"파이프에 막히는지"는 이 슬라이스에서 채점하지 않는다** — 첫 콜라이더가 x=16인데 스폰은
   x=-2, 그 사이 1.64초 동안 플랩 수단이 없어 40m 넘게 떨어져 코스를 아래로 통과한다(최종 리뷰
   Finding D). 플랩 수단이 생기는 B2-d2에서 확인한다.
6. 스펙 §6에 결과 절 추가.
