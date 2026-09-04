# 결승선 구현 계획 — 판정 일원화 + 골인 연출

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 결승선 통과 판정을 클·서 공통 시뮬로 합치고, 통과한 새가 감속해 멈추며 서버가 확정한 등수가 뜨게 한다.

**Architecture:** 판정은 시뮬(`Detection` 페이즈)이 하고 결과를 `FinishState` 컴포넌트에 적는다. 서버는 그 기록을 추적기로 옮겨 담아 등수를 매기고, 등수 숫자만 스냅샷으로 클라에 보낸다. Flappy와 Skydive가 같은 공용 부품을 쓴다.

**Tech Stack:** Unity 6 / C# / VContainer / R3 / Mirror / Luban(MasterData) / NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-09-04-race-finish-design.md`

## Global Constraints

- **레포 6개**: LOP-Shared, LOP-Client, LOP-Server, MasterData-Client, MasterData-Server, infrastructure. 레포마다 따로 커밋·푸시한다.
- **푸시 절차(레포마다)**: `git fetch origin` → `git rebase --autostash origin/main` → `git checkout main` → `git merge --ff-only origin/main` → `git merge --no-ff <feature>` → `git push origin main`. **한 줄씩 확인하고 넘어간다. `&&`로 잇지 않는다.**
- **`git push --force` / `--force-with-lease` 금지.** 거절되면 다시 fetch → rebase → 재시도.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고 커밋 전에 `git status --short`로 확인한다.
- **main에 직접 커밋 금지.** 유니티 레포에서 **git worktree 금지** — 일반 브랜치로 전환한다.
- **커밋하지 않는 로컬 픽스처** — 클라: `Assets/Art`, `Assets/Scenes/Room.unity`, `Assets/UI/Theme/Fonts/Jua-Regular SDF.asset`, `ProjectSettings/PackageManagerSettings.asset`, `ProjectSettings/ProjectSettings.asset`. 서버: `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`, 볼륨 프로파일, URP 에셋, `ProjectSettings/ProjectSettings.asset`, 빌드 디렉터리, `test-results.xml`. **절대 스테이지하지 않는다.**
- **`.meta`는 반드시 함께 커밋한다.** 새 폴더를 만들면 **폴더의 `.meta`도** 커밋한다(추격자 때 빠뜨렸다). 직접 만들지 않는다.
- **테스트를 위해 어셈블리를 옮기지 않는다.**
- **`run_tests`는 컴파일을 다시 하지 않는다.** 테스트를 건드린 뒤에는 `unity cmd recompile` → `recompile_status` 완료 확인 → `run_tests` → `test_status` 폴링. **`total`이 늘었는지 확인**해 새 테스트가 실제로 돌았는지 본다.
- **뮤테이션으로 확인한다.** 새 테스트마다 구현을 한 줄 망가뜨려 빨강을 본 뒤 되돌린다.
- **유니티 CLI**: `unity`는 `~/.unity/bin/unity`. `unity cmd <command> --project-path <절대경로>`를 항상 쓴다. 테스트는 `--mode EditMode --async_tests true`, 결과는 `unity cmd test_status`로 폴링한다(`run_tests` 응답의 `Total:0`은 아직 시작 전이라는 뜻이다).
- **플레이 모드를 임의로 멈추지 않는다.** 플레이 중엔 `recompile`하지 않는다.
- **주석**: 코드로 자명한 것은 달지 않는다. 비자명한 *의도(왜)* 만 일상어로 짧게. 아직 없는 미래 기능을 현재 주석에 섞지 않는다.
- **시뮬은 엔진 트랜스폼(콜라이더·`GameObject.transform`)을 읽지 않는다.** 되돌리기 재생 중엔 물리를 안 돌려 얼어 있어, 같은 코드가 라이브와 재생에서 다른 답을 낸다. 몸 바운드는 항상 `World.Transform` + `CapsuleShape`로 조립한다.
- **Part 1은 동작이 바뀌지 않는 정리다.** 등수 결과가 지금과 같아야 한다.

### 알려진 환경 문제

- 로컬 `Assets/Art` 서브모듈이 main이 기록한 커밋과 다르면 `SkydiveCloudColliderTests`·`SkydiveWindBuildTests`가 빨갛다. 내 변경과 무관하다. 맞추려면 `git submodule update`(사용자 판단).

---

## 파일 구조

| 레포 | 파일 | 책임 |
|---|---|---|
| Shared | `FinishState.cs` (신규) | 컴포넌트 — 처음 넘은 틱(−1 = 아직) + 그때의 깊이 |
| Shared | `FinishLineBounds.cs` (신규) | 결승선 바운드 홀더. 마커가 맵 로드 때 스스로 등록 |
| Shared | `FinishSystem.cs` (신규) | 축·방향을 받아 판정. `FinishLineOverlap.Past`를 부른다 |
| Shared | `FinishLine.cs` | 자기 등록(`[SceneInjectMonoBehaviour]` + `Construct`) |
| Shared | `FlappyWorld.cs` / `SkydiveWorld.cs` | `Detection`에서 판정 |
| Shared | `FlappySavedState.cs` / `SkydiveSavedState.cs` | 되돌리기에 `FinishState` 포함 |
| Shared | `FlappyMoveSystem.cs` | 통과 뒤 감속 분기 |
| Shared | `FlappyConfig.cs` | `FinishBrake` |
| Shared | `FlappyChaserCurve.cs` | 결승선 상한 |
| Shared | `FlappyRaceProgress.cs` / `SkydiveProgress.cs` | **삭제** (사용처 없음) |
| 서버 | `FinishTrackingSystem.cs` (신규) | `FinishState` → `FinishOrderTracker` 옮겨 담기 |
| 서버 | `FinishLineTrackingSystem.cs` | **삭제** |
| 서버 | `FlappyRaceRuleSystem.cs` / `SkydiveRuleSystem.cs` | 타입 이름 교체 |
| 서버 | `FinishPlacements.cs` | 등수 뽑기 순수 함수 추가(`PlacementIn`) |
| 서버 | 두 `*LifetimeScope.cs` | 배선 |
| 서버 | `EntitySnapshotBroadcastSystem.cs` | 스냅에 등수 채우기 |
| 서버 | `FlappyChaserSystem.cs` | 결승선 상한 전달 |
| proto | `EntitySnap.proto` | `finish_placement` |
| Shared | `FinishPlacement.cs` (신규) | 서버가 정한 등수(표시값). 되돌리기 대상 아님 |
| 클라 | `EntitySnap.cs`(도메인), `GameEntityMessageHandler.cs` | 등수 받아 컴포넌트에 적기 |
| 클라 | `RaceFinishView` + UXML/USS + 카탈로그 | 등수 표시 |
| 클라 | `FlappyHudCoordinator.cs` | 완주 시 화면 전환 |
| 클라 | `FlappyChaserView.cs` | 결승선 상한 전달 |
| 클라·서버 | `FlappyBirdCreator.cs` / `SkydivePlayerCreator.cs` | `FinishState` 붙이기 |
| 클라·서버 | `FlappyConfigProvider.cs` | `FinishBrake` |
| infrastructure | `#FlappyConfig.xlsx` | `finish_brake` |

---

# Part 1 — 통과 판정을 시뮬로 (동작 불변)

## Task 1: 공용 판정 부품

Shared에 부품 셋을 만든다. 아직 아무도 안 쓴다.

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FinishState.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FinishLineBounds.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FinishSystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FinishSystemTests.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FinishLineBoundsTests.cs`

**Interfaces:**
- Consumes: `FinishLineOverlap.Past(Bounds body, Bounds line, FinishAxis axis, bool increasing) -> float` (기존)
- Produces: `FinishState` 컴포넌트 — `const long NotFinished = -1`, `long FinishedTick`, `float Depth`, `bool Finished`
- Produces: `FinishLineBounds(FinishAxis axis, float? fallbackCoordinate = null)` — `Register(Bounds)`, `Unregister()`, `bool TryGet(out Bounds)`
- Produces: `FinishSystem(FinishLineBounds line, FinishAxis axis, bool increasing)` — `void Tick(GameFramework.World.Entity entity, long tick)`

- [ ] **Step 1: 브랜치를 판다 (레포 6개)**

```bash
cd <repo>
git fetch origin
git status --short          # 로컬 픽스처만 있는지 확인
git checkout -b feature/race-finish origin/main
```

`infrastructure`, `LeagueOfPhysical-Shared`, `LeagueOfPhysical-Client`, `LeagueOfPhysical-Server`,
`LeagueOfPhysical-MasterData-Client`, `LeagueOfPhysical-MasterData-Server`.

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/FinishSystemTests.cs`:

```csharp
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 결승선 통과 판정. 서버가 등수를 매길 때 쓰는 것과 <b>같은 식</b>(FinishLineOverlap)을 쓰되,
    /// 몸 바운드를 콜라이더가 아니라 진실원본에서 조립한다 — 되돌리기 재생 중엔 콜라이더가 얼어 있어
    /// 같은 코드가 라이브와 재생에서 다른 답을 내기 때문이다.
    /// </summary>
    public class FinishSystemTests
    {
        private const float Radius = 0.45f;
        private const float Height = 0.9f;

        //  결승선은 x=100에 두께 2로 선다 — 근접면이 99다.
        private static FinishLineBounds Line()
        {
            var line = new FinishLineBounds(FinishAxis.X);
            line.Register(new Bounds(new Vector3(100f, 0f, 0f), new Vector3(2f, 50f, 2f)));
            return line;
        }

        private static Entity Bird(float x)
        {
            var bird = new Entity("bird");
            bird.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, 0f, 0f) });
            bird.Add(new CapsuleShape(Radius, Height));
            bird.Add(new FinishState());
            return bird;
        }

        [Test]
        public void 아직이면_틱이_없음_표시다()
        {
            var bird = Bird(50f);

            new FinishSystem(Line(), FinishAxis.X, increasing: true).Tick(bird, 10);

            Assert.AreEqual(FinishState.NotFinished, bird.Get<FinishState>().FinishedTick);
            Assert.IsFalse(bird.Get<FinishState>().Finished);
        }

        [Test]
        public void 부리가_닿으면_통과다()
        {
            //  근접면 99. 중심 98.6이면 부리는 99.05라 닿았다 — 중심 기준이면 아직이다.
            var bird = Bird(98.6f);

            new FinishSystem(Line(), FinishAxis.X, increasing: true).Tick(bird, 10);

            Assert.IsTrue(bird.Get<FinishState>().Finished);
            Assert.AreEqual(10, bird.Get<FinishState>().FinishedTick);
            Assert.That(bird.Get<FinishState>().Depth, Is.EqualTo(0.05f).Within(1e-3f));
        }

        [Test]
        public void 부리가_아직_안_닿았으면_통과가_아니다()
        {
            //  중심 98.5면 부리는 98.95라 근접면 99에 못 미친다.
            var bird = Bird(98.5f);

            new FinishSystem(Line(), FinishAxis.X, increasing: true).Tick(bird, 10);

            Assert.IsFalse(bird.Get<FinishState>().Finished);
        }

        [Test]
        public void 처음_넘은_틱만_기록한다()
        {
            //  등수는 처음 닿은 순간이 정답이다. 뒤 틱이 덮어쓰면 더 오래 달린 사람이 유리해진다.
            var bird = Bird(99f);
            var system = new FinishSystem(Line(), FinishAxis.X, increasing: true);

            system.Tick(bird, 10);
            bird.Get<GameFramework.World.Transform>().Position = new System.Numerics.Vector3(200f, 0f, 0f);
            system.Tick(bird, 11);

            Assert.AreEqual(10, bird.Get<FinishState>().FinishedTick);
            Assert.That(bird.Get<FinishState>().Depth, Is.EqualTo(0.45f).Within(1e-3f));
        }

        [Test]
        public void 아래로_달리는_축도_같은_규칙이다()
        {
            //  Skydive는 y가 작아지는 방향이다. 몸의 아랫면이 선의 윗면을 지나면 통과.
            var line = new FinishLineBounds(FinishAxis.Y);
            line.Register(new Bounds(new Vector3(0f, 10f, 0f), new Vector3(50f, 2f, 50f)));

            var diver = new Entity("diver");
            //  캡슐은 발밑이 기준이라(collider.center.y = height/2) 바운드 아랫면이 곧 위치의 y다.
            diver.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(0f, 10.9f, 0f) });
            diver.Add(new CapsuleShape(Radius, Height));
            diver.Add(new FinishState());

            new FinishSystem(line, FinishAxis.Y, increasing: false).Tick(diver, 7);

            Assert.IsTrue(diver.Get<FinishState>().Finished);
            Assert.AreEqual(7, diver.Get<FinishState>().FinishedTick);
        }

        [Test]
        public void 결승선을_모르면_아무도_통과하지_않는다()
        {
            //  맵이 아직 안 올라온 순간이 실제로 있다. 그때 전원 통과로 읽으면 판이 즉시 끝난다.
            var bird = Bird(9999f);

            new FinishSystem(new FinishLineBounds(FinishAxis.X), FinishAxis.X, increasing: true).Tick(bird, 10);

            Assert.IsFalse(bird.Get<FinishState>().Finished);
        }
    }
}
```

`LeagueOfPhysical-Shared/Tests/EditMode/FinishLineBoundsTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 결승선 바운드 홀더. 맵 마커가 스스로 등록하고, 마커가 없는 맵을 위해 폴백 좌표를 받는다
    /// (Skydive가 지면 높이를 넘긴다).
    /// </summary>
    public class FinishLineBoundsTests
    {
        [Test]
        public void 등록하면_그_바운드를_준다()
        {
            var line = new FinishLineBounds(FinishAxis.X);
            line.Register(new Bounds(new Vector3(100f, 0f, 0f), new Vector3(2f, 50f, 2f)));

            Assert.IsTrue(line.TryGet(out Bounds bounds));
            Assert.That(bounds.min.x, Is.EqualTo(99f).Within(1e-3f));
        }

        [Test]
        public void 아무것도_등록_안_했고_폴백도_없으면_없다고_한다()
        {
            Assert.IsFalse(new FinishLineBounds(FinishAxis.X).TryGet(out _));
        }

        [Test]
        public void 폴백만_있으면_두께_0인_선이다()
        {
            var line = new FinishLineBounds(FinishAxis.Y, fallbackCoordinate: 12f);

            Assert.IsTrue(line.TryGet(out Bounds bounds));
            Assert.That(bounds.min.y, Is.EqualTo(12f).Within(1e-3f));
            Assert.That(bounds.max.y, Is.EqualTo(12f).Within(1e-3f));
        }

        [Test]
        public void 등록된_것이_폴백보다_우선이다()
        {
            var line = new FinishLineBounds(FinishAxis.X, fallbackCoordinate: 5f);
            line.Register(new Bounds(new Vector3(100f, 0f, 0f), new Vector3(2f, 50f, 2f)));

            line.TryGet(out Bounds bounds);
            Assert.That(bounds.min.x, Is.EqualTo(99f).Within(1e-3f));
        }

        [Test]
        public void 등록을_거두면_다시_없다()
        {
            //  라운드가 여러 판이면 맵을 다시 로드한다 — 옛 마커가 남아 있으면 안 된다.
            var line = new FinishLineBounds(FinishAxis.X);
            line.Register(new Bounds(new Vector3(100f, 0f, 0f), new Vector3(2f, 50f, 2f)));
            line.Unregister();

            Assert.IsFalse(line.TryGet(out _));
        }
    }
}
```

- [ ] **Step 3: 컴파일해서 빨간지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

기대: `FinishState` / `FinishLineBounds` / `FinishSystem` 이 없어 **컴파일 에러**.

- [ ] **Step 4: 컴포넌트를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/FinishState.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 결승선을 언제 어떤 깊이로 넘었는지. 시뮬이 적고, 서버가 등수를 매길 때 읽는다.
    ///
    /// <para>깊이를 함께 적는 이유는 <b>같은 틱에 둘이 닿는 일이 기본값</b>이기 때문이다 —
    /// 모든 새가 같은 속도로 달린다. 더 깊이 넘어가 있다는 것은 그만큼 먼저 닿았다는 뜻이다.</para>
    /// </summary>
    public class FinishState : GameFramework.World.Component
    {
        /// <summary>아직 안 넘었다는 표시. 틱 0이 실제로 올 수 있어 0을 못 쓴다.</summary>
        public const long NotFinished = -1;

        public long FinishedTick = NotFinished;

        /// <summary>처음 닿은 틱에 결승선을 넘어간 깊이(m).</summary>
        public float Depth;

        public bool Finished => FinishedTick != NotFinished;
    }
}
```

- [ ] **Step 5: 홀더를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/FinishLineBounds.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 결승선이 어디 있는지. 맵 씬의 <see cref="FinishLine"/> 마커가 맵 로드 때 스스로 등록한다
    /// (<see cref="WindVolume"/>과 같은 통로).
    ///
    /// <para>시뮬이 첫 틱에 씬을 훑지 않게 하려는 것이다 — 그러면 시뮬이 엔진 씬을 알게 되고,
    /// 되돌리기 재생 중에 무엇을 보는지가 불분명해진다.</para>
    /// </summary>
    public class FinishLineBounds
    {
        private readonly FinishAxis axis;
        private readonly float? fallbackCoordinate;

        private Bounds registered;
        private bool hasRegistered;

        /// <param name="fallbackCoordinate">
        /// 마커가 없는 맵을 위한 대비. 그 좌표에 두께 0인 선을 세운다. 주지 않으면 결승선이
        /// 없는 것으로 보고 아무도 통과하지 않는다.
        /// </param>
        public FinishLineBounds(FinishAxis axis, float? fallbackCoordinate = null)
        {
            this.axis = axis;
            this.fallbackCoordinate = fallbackCoordinate;
        }

        public void Register(Bounds bounds)
        {
            registered = bounds;
            hasRegistered = true;
        }

        /// <summary>맵을 다시 로드하면 옛 마커가 사라진다 — 그때 거둔다.</summary>
        public void Unregister()
        {
            hasRegistered = false;
        }

        public bool TryGet(out Bounds bounds)
        {
            if (hasRegistered)
            {
                bounds = registered;
                return true;
            }
            if (fallbackCoordinate.HasValue)
            {
                bounds = new Bounds(Center(fallbackCoordinate.Value), Vector3.zero);
                return true;
            }
            bounds = default;
            return false;
        }

        private Vector3 Center(float coordinate)
        {
            switch (axis)
            {
                case FinishAxis.X: return new Vector3(coordinate, 0f, 0f);
                case FinishAxis.Y: return new Vector3(0f, coordinate, 0f);
                default: return new Vector3(0f, 0f, coordinate);
            }
        }
    }
}
```

- [ ] **Step 6: 판정 시스템을 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/FinishSystem.cs`:

```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 결승선을 넘었는지 보고 <see cref="FinishState"/>에 적는다. 판정식은 서버가 등수를 매길 때
    /// 쓰던 <see cref="FinishLineOverlap"/> 그대로다.
    ///
    /// <para>몸 바운드를 콜라이더가 아니라 <b>진실원본 + <see cref="GameFramework.World.CapsuleShape"/></b>로
    /// 조립한다. 콜라이더가 원래 그 둘로 만들어지므로(<see cref="PhysicsBodyFactory"/>) 값은 같고,
    /// 되돌리기 재생 중에도 얼지 않는다 — 재생 중엔 물리를 안 돌려 엔진 트랜스폼이 한 틱 전에
    /// 멈춰 있다.</para>
    /// </summary>
    public class FinishSystem
    {
        private readonly FinishLineBounds line;
        private readonly FinishAxis axis;
        private readonly bool increasing;

        public FinishSystem(FinishLineBounds line, FinishAxis axis, bool increasing)
        {
            this.line = line;
            this.axis = axis;
            this.increasing = increasing;
        }

        public void Tick(GameFramework.World.Entity entity, long tick)
        {
            var state = entity.Get<FinishState>();
            if (state == null || state.Finished)
            {
                return;   // 등수는 처음 닿은 순간이 정답이다 — 덮어쓰지 않는다
            }

            var transform = entity.Get<GameFramework.World.Transform>();
            var shape = entity.Get<GameFramework.World.CapsuleShape>();
            if (transform == null || shape == null || line.TryGet(out Bounds lineBounds) == false)
            {
                return;
            }

            float past = FinishLineOverlap.Past(BodyBounds(transform, shape), lineBounds, axis, increasing);
            if (past < 0f)
            {
                return;
            }

            state.FinishedTick = tick;
            state.Depth = past;
        }

        //  콜라이더와 같은 모양으로 맞춘다 — PhysicsBodyFactory가 center를 (0, height/2, 0)에 둔다.
        private static Bounds BodyBounds(GameFramework.World.Transform transform,
                                         GameFramework.World.CapsuleShape shape)
        {
            Vector3 center = transform.Position.ToUnity() + new Vector3(0f, shape.Height * 0.5f, 0f);
            return new Bounds(center, new Vector3(shape.Radius * 2f, shape.Height, shape.Radius * 2f));
        }
    }
}
```

- [ ] **Step 7: 컴파일하고 테스트가 초록인지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path $P
unity cmd recompile_status --project-path $P          # completed / failed:false 확인
unity cmd run_tests --project-path $P --mode EditMode --async_tests true
sleep 60; unity cmd test_status --project-path $P
```

기대: 전부 통과(로컬 Art 문제로 Skydive 맵 테스트 2개는 빨갈 수 있다). **`total`이 11 늘었는지 확인한다.**

- [ ] **Step 8: 뮤테이션으로 확인한다**

`FinishSystem.BodyBounds`의 `shape.Radius * 2f`를 `0f`로 바꾸고(= 두께 0인 몸) 다시 돌린다.

기대: `부리가_닿으면_통과다`, `처음_넘은_틱만_기록한다`, `아래로_달리는_축도_같은_규칙이다`가 빨강.
확인 후 되돌리고 초록을 다시 본다.

- [ ] **Step 9: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/FinishState.cs Runtime/Scripts/Game/FinishState.cs.meta \
        Runtime/Scripts/Game/FinishLineBounds.cs Runtime/Scripts/Game/FinishLineBounds.cs.meta \
        Runtime/Scripts/Game/FinishSystem.cs Runtime/Scripts/Game/FinishSystem.cs.meta \
        Tests/EditMode/FinishSystemTests.cs Tests/EditMode/FinishSystemTests.cs.meta \
        Tests/EditMode/FinishLineBoundsTests.cs Tests/EditMode/FinishLineBoundsTests.cs.meta
git status --short
git commit -m "feat(race): 결승선 통과 판정을 공용 부품으로 뽑는다"
```

---

## Task 2: 두 월드가 판정하게 한다

시뮬이 실제로 판정하고 되돌리기까지 담는다. **서버는 아직 옛 경로로 등수를 매긴다** — 동작은 그대로다.

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FinishLine.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveWorld.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappySavedState.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveSavedState.cs`
- Modify: 클·서 `Assets/Scripts/Entity/FlappyBirdCreator.cs`, `Assets/Scripts/Entity/SkydivePlayerCreator.cs` (4개)
- Modify: 클·서 `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`, `Assets/Scripts/Game/SkydiveLifetimeScope.cs` (4개)
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappySavedStateFinishTests.cs`

**Interfaces:**
- Consumes: `FinishState`, `FinishLineBounds`, `FinishSystem` (Task 1)
- Produces: 두 월드가 `Detection`에서 `FinishSystem.Tick(entity, tick)`을 돈다
- Produces: DI에 `FinishLineBounds`(게임별 축·폴백)와 `FinishSystem` 등록

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/FlappySavedStateFinishTests.cs`:

```csharp
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 되돌리기가 통과 기록까지 담는지. 클라가 통과를 예측하므로, 되돌릴 때 같이 안 되돌리면
    /// 재생 뒤에도 "이미 통과함"이 남아 새가 영영 감속한 채로 있는다.
    /// </summary>
    public class FlappySavedStateFinishTests
    {
        private static Entity Bird()
        {
            var bird = new Entity("bird");
            bird.Add(new FlappyStun());
            bird.Add(new FlappyDash());
            bird.Add(new FinishState());
            return bird;
        }

        [Test]
        public void 통과_전의_사진으로_되돌리면_통과가_취소된다()
        {
            var bird = Bird();
            var before = FlappySavedState.Capture(bird);

            bird.Get<FinishState>().FinishedTick = 500;
            bird.Get<FinishState>().Depth = 0.3f;
            before.RestoreTo(bird);

            Assert.AreEqual(FinishState.NotFinished, bird.Get<FinishState>().FinishedTick);
            Assert.AreEqual(0f, bird.Get<FinishState>().Depth);
        }

        [Test]
        public void 통과_뒤의_사진은_그대로_되살아난다()
        {
            var bird = Bird();
            bird.Get<FinishState>().FinishedTick = 500;
            bird.Get<FinishState>().Depth = 0.3f;
            var after = FlappySavedState.Capture(bird);

            bird.Get<FinishState>().FinishedTick = FinishState.NotFinished;
            bird.Get<FinishState>().Depth = 0f;
            after.RestoreTo(bird);

            Assert.AreEqual(500, bird.Get<FinishState>().FinishedTick);
            Assert.That(bird.Get<FinishState>().Depth, Is.EqualTo(0.3f).Within(1e-4f));
        }
    }
}
```

- [ ] **Step 2: 컴파일해서 빨간지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

기대: 컴파일은 되고 **두 테스트가 실패**한다(`FlappySavedState`가 아직 통과 기록을 안 담는다).
`run_tests` → `test_status`로 빨강을 확인한다.

- [ ] **Step 3: 되돌리기에 통과 기록을 담는다**

`FlappySavedState.cs` — 필드·생성자·`Capture`·`RestoreTo`에 각각 추가:

```csharp
        public readonly float DashRemaining;
        public readonly long FinishedTick;
        public readonly float FinishDepth;

        private FlappySavedState(float stunRemaining, float invulnRemaining,
                                 float dashCharge, float dashRemaining,
                                 long finishedTick, float finishDepth)
        {
            StunRemaining = stunRemaining;
            InvulnRemaining = invulnRemaining;
            DashCharge = dashCharge;
            DashRemaining = dashRemaining;
            FinishedTick = finishedTick;
            FinishDepth = finishDepth;
        }
```

`Capture`에서:

```csharp
            var finish = entity.Get<FinishState>();
            return new FlappySavedState(
                stun?.StunRemaining ?? 0f,
                stun?.InvulnRemaining ?? 0f,
                dash?.Charge ?? 0f,
                dash?.DashRemaining ?? 0f,
                finish?.FinishedTick ?? FinishState.NotFinished,
                finish?.Depth ?? 0f);
```

`RestoreTo`에서:

```csharp
            var finish = entity.Get<FinishState>();
            if (finish != null)
            {
                finish.FinishedTick = FinishedTick;
                finish.Depth = FinishDepth;
            }
```

`SkydiveSavedState.cs`도 같은 모양으로 넣는다(필드 이름·순서는 그 파일의 관례를 따른다).

- [ ] **Step 4: 테스트가 초록인지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path $P
unity cmd recompile_status --project-path $P
unity cmd run_tests --project-path $P --mode EditMode --async_tests true
sleep 60; unity cmd test_status --project-path $P
```

기대: 통과. **`total`이 2 늘었는지 확인한다.**

- [ ] **Step 5: 뮤테이션으로 확인한다**

`FlappySavedState.RestoreTo`의 `finish.FinishedTick = FinishedTick;` 줄을 지우고 다시 돌린다.
기대: `통과_전의_사진으로_되돌리면_통과가_취소된다`가 빨강. 되돌린다.

- [ ] **Step 6: 두 월드가 판정하게 한다**

`FlappyWorld.cs` — 생성자에 `FinishSystem finishSystem`을 받아 필드로 두고, `Detection`을 추가:

```csharp
        //  이동이 다 끝난 자리에서 본다. Mutation 안에 두면 한 틱 전 자리를 보고 통과를 한 틱
        //  늦게 잡는다. 아키텍처 문서가 Detection을 "상태를 스캔해 파생 사건을 만드는 자리"로
        //  정의해 둔 그 페이즈다.
        protected override void Detection(long tick, float deltaTime)
        {
            for (int i = 0; i < _birds.Count; i++)
            {
                _finishSystem.Tick(_birds[i], tick);
            }
        }
```

`SkydiveWorld.cs`도 같은 모양으로(`_divers` 목록을 돈다).

> `_birds`/`_divers`는 `Mutation`의 `CollectBirds`/`CollectDivers`가 채운다. `Detection`은 그
> 뒤에 도므로 목록이 이미 채워져 있다.

- [ ] **Step 7: 새에 `FinishState`를 붙인다**

클·서의 `FlappyBirdCreator.cs`에서 `CapsuleShape`를 붙이는 줄 아래에:

```csharp
            worldEntity.Add(new FinishState());
```

`SkydivePlayerCreator.cs`(클·서)도 같다. **네 파일 전부.**

- [ ] **Step 8: 배선한다**

서버 `FlappyRaceLifetimeScope.cs` — `FinishLineTrackingSystem` 등록 **위**에:

```csharp
            //  새는 +x로 달린다. 폴백을 주지 않는다 — 마커가 없으면 룰이 Initialize에서 터뜨린다.
            builder.Register(c => new FinishLineBounds(FinishAxis.X), Lifetime.Singleton);
            builder.Register(c => new FinishSystem(
                c.Resolve<FinishLineBounds>(), FinishAxis.X, increasing: true), Lifetime.Singleton);
```

그리고 `FlappyWorld` 생성에 `c.Resolve<FinishSystem>()`을 넘긴다.

클라 `FlappyRaceLifetimeScope.cs`도 같은 두 줄 + `FlappyWorld` 인자.

서버 `SkydiveLifetimeScope.cs`:

```csharp
            //  아래로 떨어지므로 y가 작아지는 방향이다. 마커가 없는 맵을 위해 지면 높이를 폴백으로 준다.
            builder.Register(c => new FinishLineBounds(
                FinishAxis.Y, c.Resolve<SkydiveConfig>().GroundY), Lifetime.Singleton);
            builder.Register(c => new FinishSystem(
                c.Resolve<FinishLineBounds>(), FinishAxis.Y, increasing: false), Lifetime.Singleton);
```

클라 `SkydiveLifetimeScope.cs`도 같다. 그리고 두 `SkydiveWorld` 생성에 인자를 넘긴다.

- [ ] **Step 9: 마커가 스스로 등록하게 한다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/FinishLine.cs`를 이렇게 바꾼다:

```csharp
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 맵 씬에 찍어 두는 결승선. 맵이 올라올 때 <see cref="FinishLineBounds"/>를 주입받아 스스로
    /// 등록한다(<c>GameLifetimeScope</c>가 <c>sceneLoaded</c>를 듣고 <c>InjectSceneObjects</c>를 부른다).
    ///
    /// <see cref="SpawnPoint"/>와 같은 이유로 <b>공용 패키지</b>에 있다: 맵 씬은 클라에서 만들고
    /// 서버가 읽는데, 스크립트가 한쪽에만 있으면 반대쪽에서 missing script가 되고 그 빈 컴포넌트가
    /// 씬 주입을 끊는다. (Unreal의 ATriggerVolume 계열 골 마커에 대응 — 좌표를 코드가 아니라
    /// 맵이 정한다.)
    ///
    /// 어느 축을 읽을지는 마커가 아니라 <b>게임이 정한다</b> — 마커는 형상만 내줄 뿐이다.
    /// </summary>
    [SceneInjectMonoBehaviour]
    public class FinishLine : MonoBehaviour
    {
        private FinishLineBounds line;

        [Inject]
        public void Construct(FinishLineBounds line)
        {
            this.line = line;
            line.Register(Bounds());
        }

        //  보이는 판이 곧 결승선이다. 렌더러가 없으면(마커만 찍어 둔 맵) 두께 0인 선으로 쓴다.
        private Bounds Bounds()
        {
            var renderer = GetComponentInChildren<Renderer>();
            return renderer != null
                ? renderer.bounds
                : new Bounds(transform.position, Vector3.zero);
        }

        private void OnDestroy()
        {
            // 라운드가 여러 판이면 맵을 다시 로드한다 — 안 거두면 옛 선이 남는다.
            line?.Unregister();
        }
    }
}
```

- [ ] **Step 10: 클·서 둘 다 컴파일한다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
```

**둘 다 한다.** 한쪽만 보고 푸시해서 main이 안 되는 상태로 올라간 적이 있다.

- [ ] **Step 11: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/FinishLine.cs Runtime/Scripts/Game/FlappyWorld.cs \
        Runtime/Scripts/Game/SkydiveWorld.cs Runtime/Scripts/Game/FlappySavedState.cs \
        Runtime/Scripts/Game/SkydiveSavedState.cs \
        Tests/EditMode/FlappySavedStateFinishTests.cs Tests/EditMode/FlappySavedStateFinishTests.cs.meta
git status --short
git commit -m "feat(race): 시뮬이 결승선 통과를 판정하고 되돌리기가 그걸 담는다"

# 클라 / 서버 각각
git add Assets/Scripts/Entity/FlappyBirdCreator.cs Assets/Scripts/Entity/SkydivePlayerCreator.cs \
        Assets/Scripts/Game/FlappyRaceLifetimeScope.cs Assets/Scripts/Game/SkydiveLifetimeScope.cs
git status --short
git commit -m "feat(race): 결승선 판정을 시뮬에 배선한다"
```

---

## Task 3: 서버가 시뮬의 기록으로 등수를 매긴다

옛 경로를 걷어낸다. **여기서도 등수 결과는 안 바뀐다.**

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/FinishTrackingSystem.cs`
- Delete: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/FinishLineTrackingSystem.cs` (+`.meta`)
- Delete: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyRaceProgress.cs`, `SkydiveProgress.cs` (+`.meta`, +테스트 2개)
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceRuleSystem.cs`, `SkydiveRuleSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`, `SkydiveLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/FlappyChaserSystem.cs`

**Interfaces:**
- Consumes: `FinishState` (Task 1), 시뮬이 채운 값 (Task 2)
- Produces: `FinishTrackingSystem` — `Watch(string)`, `Reset()`, `bool HasFinished(string)`, `IReadOnlyList<FinishRecord> Ordered`, `bool AllWatchedFinished`, `Tick(long, float)`
  — **옛 `FinishLineTrackingSystem`과 같은 표면**이라 룰은 타입 이름만 바뀐다

- [ ] **Step 1: 새 추적 시스템을 만든다**

`Assets/Scripts/Game/TickSystems/FinishTrackingSystem.cs`:

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 시뮬이 적어 둔 통과 기록(<see cref="FinishState"/>)을 모아 순서를 들고 있는다.
    ///
    /// <para><b>왜 따로 들고 있나:</b> 완주한 사람이 나가면 그 몸이 사라지면서 컴포넌트의 기록도
    /// 같이 사라진다. 그러면 등수를 매길 때 그 사람이 "나간 사람"(최하위)으로 둔갑한다. 한 번
    /// 관측한 통과는 몸과 무관하게 남아야 한다.</para>
    ///
    /// <para>판정은 하지 않는다 — 옮겨 담기만 한다. 판정은 클·서 공통 시뮬의 몫이다.</para>
    /// </summary>
    public class FinishTrackingSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        private readonly FinishOrderTracker tracker = new FinishOrderTracker();
        private readonly List<string> watched = new List<string>();

        public FinishTrackingSystem(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        /// <summary>먼저 닿은 순. 같은 틱이면 깊이 넘은 쪽이 앞.</summary>
        public IReadOnlyList<FinishRecord> Ordered => tracker.Ordered;

        public bool HasFinished(string entityId) => tracker.HasFinished(entityId);

        public void Watch(string entityId) => watched.Add(entityId);

        public void Reset()
        {
            watched.Clear();
            tracker.Reset();
        }

        public void Tick(long tick, float deltaTime)
        {
            for (int i = 0; i < watched.Count; i++)
            {
                var state = entityRegistry.Get(watched[i])?.Get<FinishState>();
                if (state != null && state.Finished)
                {
                    //  이미 기록된 사람은 Observe가 알아서 무시한다.
                    tracker.Observe(watched[i], state.FinishedTick, state.Depth);
                }
            }
        }

        /// <summary>
        /// 남아 있는 사람이 전원 통과했나. <b>아무도 없으면 false</b> — 스폰 직전에 판이 끝나는 것을 막는다.
        /// </summary>
        public bool AllWatchedFinished
        {
            get
            {
                int alive = 0;
                for (int i = 0; i < watched.Count; i++)
                {
                    if (entityRegistry.Get(watched[i]) == null)
                    {
                        continue;   // 나간 사람은 세지 않는다. 세면 한 명 나간 판이 절대 안 끝난다
                    }
                    alive++;
                    if (tracker.HasFinished(watched[i]) == false)
                    {
                        return false;
                    }
                }
                return alive > 0;
            }
        }
    }
}
```

- [ ] **Step 2: 룰의 타입 이름을 바꾼다**

`FlappyRaceRuleSystem.cs`와 `SkydiveRuleSystem.cs`에서 `FinishLineTrackingSystem`을
`FinishTrackingSystem`으로 바꾼다(필드·생성자 인자). **다른 줄은 안 바뀐다** — 표면이 같다.

`FlappyRaceRuleSystem.Initialize`의 `RequireFinishLineMarker()`는 그대로 둔다(마커가 없으면
크게 터뜨리는 것이 여전히 옳다).

- [ ] **Step 3: 배선을 바꾼다**

서버 `FlappyRaceLifetimeScope.cs`:

```csharp
            builder.Register<FinishTrackingSystem>(Lifetime.Singleton);
            builder.Register<FlappyChaserSystem>(Lifetime.Singleton);

            //  도착 감시를 러너의 End 페이즈에 문다. 시스템이 스스로 IRunner를 잡으면
            //  러너→룰→도착→러너로 고리가 생겨 컨테이너가 아예 안 만들어진다.
            //  추격자는 그 뒤여야 한다 — 앞에 두면 같은 틱에 결승선을 넘은 새를 잡는다.
            builder.RegisterBuildCallback(container =>
            {
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<FinishTrackingSystem>());
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<FlappyChaserSystem>());
            });
```

`SkydiveLifetimeScope.cs`도 같은 모양으로(`FinishTrackingSystem` 하나만).

`FlappyChaserSystem.cs`의 생성자 인자 타입도 `FinishTrackingSystem`으로 바꾼다.

- [ ] **Step 4: 죽은 코드를 지운다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git rm Assets/Scripts/Game/TickSystems/FinishLineTrackingSystem.cs \
       Assets/Scripts/Game/TickSystems/FinishLineTrackingSystem.cs.meta

cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git rm Runtime/Scripts/Game/FlappyRaceProgress.cs Runtime/Scripts/Game/FlappyRaceProgress.cs.meta \
       Runtime/Scripts/Game/SkydiveProgress.cs Runtime/Scripts/Game/SkydiveProgress.cs.meta \
       Tests/EditMode/FlappyRaceProgressTests.cs Tests/EditMode/FlappyRaceProgressTests.cs.meta \
       Tests/EditMode/SkydiveProgressTests.cs Tests/EditMode/SkydiveProgressTests.cs.meta
```

`SkydiveProgressTests.cs`도 함께 지운다(존재 확인됨).

지우기 전에 **정말 아무도 안 쓰는지** 확인한다:

```bash
cd /Users/insoobae/workspace/LOP
grep -rn "FlappyRaceProgress\|SkydiveProgress\|FinishLineTrackingSystem" \
  LeagueOfPhysical-Shared LeagueOfPhysical-Client/Assets LeagueOfPhysical-Server/Assets | grep -v '\.meta'
```

기대: 지운 파일 자신 말고는 안 나온다.

- [ ] **Step 5: 컴파일하고 테스트가 초록인지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
for P in /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server; do
  unity cmd recompile --project-path $P
  unity cmd recompile_status --project-path $P
done
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server --mode EditMode --async_tests true
sleep 60; unity cmd test_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
```

기대: 컴파일 통과. **`FinishPlacements`·`FinishOrderTracker`·`FinishLineOverlap` 테스트가
하나도 안 바뀌고 그대로 통과해야 한다** — 그게 "먹이는 쪽만 바뀌었다"의 증거다.
`FlappyRaceProgress` 테스트가 사라진 만큼만 `total`이 줄어든다.

- [ ] **Step 6: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/TickSystems/FinishTrackingSystem.cs \
        Assets/Scripts/Game/TickSystems/FinishTrackingSystem.cs.meta \
        Assets/Scripts/Game/TickSystems/FinishLineTrackingSystem.cs \
        Assets/Scripts/Game/TickSystems/FinishLineTrackingSystem.cs.meta \
        Assets/Scripts/Game/FlappyRaceRuleSystem.cs Assets/Scripts/Game/SkydiveRuleSystem.cs \
        Assets/Scripts/Game/FlappyRaceLifetimeScope.cs Assets/Scripts/Game/SkydiveLifetimeScope.cs \
        Assets/Scripts/Game/TickSystems/FlappyChaserSystem.cs
git status --short
git commit -m "refactor(race): 서버가 시뮬의 통과 기록으로 등수를 매긴다"

cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git add -u Runtime/Scripts/Game Tests/EditMode
git status --short
git commit -m "refactor(race): 쓰이지 않던 좌표 기준 통과 규칙을 지운다"
```

---

## Task 4: Part 1 라이브 검증 — 등수가 그대로인가

**Files:** 코드 변경 없음.

- [ ] **Step 1: 여섯 레포를 순서대로 푸시한다**

의존 순서: infrastructure → MasterData 둘 → LOP-Shared → 서버·클라.
(이번 Part 1에는 MasterData·infrastructure 변경이 없으므로 실제로는 Shared → 서버·클라.)

레포마다 (`&&`로 잇지 말고 한 줄씩):

```bash
cd <repo>
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/race-finish
git push origin main
```

- [ ] **Step 2: 두 에디터로 한 판 돌린다**

서버 에디터(명단 2인 확인) + 클라 에디터 + MPPM 클론. 양쪽 `LOP ▸ Debug ▸ Auto Flap` 켬.

- [ ] **Step 3: 등수가 옮기기 전과 같은지 대조한다**

서버 콘솔에서 확인한다.

```
[Finish] <id> tick=<틱> 넘은깊이=<깊이>m
[Outcome] tick=<틱> 1위 <userId> · 2위 <userId>
```

기대(이전 실측): 오토파일럿 두 대는 **완주 깊이 0.214m / 0.019m**, 틱 간격 **87틱**으로 나온다.
같은 값이 나오면 판정이 그대로 옮겨진 것이다. 값이 다르면 **멈추고 원인을 찾는다** — Part 1은
동작이 바뀌면 안 된다.

- [ ] **Step 4: Skydive도 한 판 돌린다**

`ConfigureRoomComponent`의 `gameModeId`를 Skydive로 바꿔 한 판 돌린다(이 파일은 커밋하지 않는다).
등수가 나오는지, 예외가 없는지만 본다.

- [ ] **Step 5: 관측값을 기록한다**

완주 틱·깊이·등수를 적어 둔다. Part 2에서 감속을 넣은 뒤 **통과 틱이 그대로인지** 대조할 기준이 된다.

---

# Part 2 — 골인 연출

## Task 5: 통과 뒤 감속

**Files:**
- Modify: `infrastructure/table/Datas/#FlappyConfig.xlsx`
- Modify: MasterData-Client/Server `Runtime.Generated/**`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyConfig.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyMoveSystem.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`
- Modify: 클·서 `Assets/Scripts/Game/FlappyConfigProvider.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyMoveSystemFinishTests.cs`

**Interfaces:**
- Consumes: `FinishState` (Task 1)
- Produces: `FlappyConfig.FinishBrake` (float, 선택 인자 `finishBrake = 0f`)
- Produces: `FlappyMoveSystem.Tick(entity, deltaTime, bool dashing, bool finished)` — 인자가 하나 는다

- [ ] **Step 1: 엑셀에 열을 넣는다**

`infrastructure/table`에서:

```python
python3 - <<'PY'
import zipfile, re, os
src = 'Datas/#FlappyConfig.xlsx'
cols = [('T', 'finish_brake', '5.5')]

z = zipfile.ZipFile(src)
items = [(i, z.read(i.filename)) for i in z.infolist()]
z.close()

def text(col, row, value):
    return f'<c r="{col}{row}" t="inlineStr"><is><t>{value}</t></is></c>'
def number(col, row, value):
    return f'<c r="{col}{row}" t="n"><v>{value}</v></c>'
def append_to_row(xml, row, extra):
    m = re.search(r'(<row r="%d"[^>]*>)(.*?)(</row>)' % row, xml, re.S)
    assert m, f'row {row} not found'
    return xml[:m.end(2)] + extra + xml[m.end(2):]

out = []
for info, data in items:
    if info.filename == 'xl/worksheets/sheet1.xml':
        xml = data.decode('utf-8')
        assert '<dimension ref="A1:S5" />' in xml, '이미 고쳐졌거나 모양이 다르다'
        xml = xml.replace('<dimension ref="A1:S5" />', '<dimension ref="A1:T5" />')
        xml = append_to_row(xml, 1, ''.join(text(c, 1, n) for c, n, _ in cols))
        xml = append_to_row(xml, 2, ''.join(text(c, 2, 'float') for c, _, _ in cols))
        xml = append_to_row(xml, 4, ''.join(text(c, 4, n) for c, n, _ in cols))
        xml = append_to_row(xml, 5, ''.join(number(c, 5, v) for c, _, v in cols))
        data = xml.encode('utf-8')
    out.append((info, data))

tmp = src + '.tmp'
with zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED) as w:
    for info, data in out:
        w.writestr(info, data)
os.replace(tmp, src)
print('ok')
PY
```

값 5.5는 11m/s에서 **2초 만에 11m를 더 가고 멈추는** 감속이다.

- [ ] **Step 2: MasterData를 다시 생성한다**

생성물의 `.meta` GUID는 기계마다 다르다. **생성 전에 두 패키지를 fetch해 최신인지 확인한다.**

```bash
cd /Users/insoobae/workspace/LOP
git -C LeagueOfPhysical-MasterData-Client fetch origin
git -C LeagueOfPhysical-MasterData-Server fetch origin
cd infrastructure/table
bash gen.sh 2>&1 | tail -3
git -C ../../LeagueOfPhysical-MasterData-Client status --short
git -C ../../LeagueOfPhysical-MasterData-Server status --short
grep -n "FinishBrake" ../../LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/FlappyConfig.cs
```

`gen.sh`는 실행 권한이 없어 `bash gen.sh`로 부른다. `.meta`가 삭제로 남아 있지 않은지 확인한다.

- [ ] **Step 3: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/FlappyMoveSystemFinishTests.cs`:

```csharp
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 통과 뒤의 움직임. 100m 주자가 결승선을 지나 감속하는 그림이고, 조작이 끊겨야 골인 뒤
    /// 행동이 등수에 영향을 주지 않는다(레이싱 장르 관례).
    /// </summary>
    public class FlappyMoveSystemFinishTests
    {
        private const float Dt = 0.02f;
        private const float Tolerance = 1e-4f;

        private static FlappyConfig Config()
            => new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                                stunTime: 0.8f, invulnTime: 0.6f,
                                dashMult: 2f, dashDuration: 0.2f, dashChargeBase: 0.13f, dashChargeDive: 1.2f,
                                chaserStartX: -60f, chaserInitialSpeed: 7f,
                                chaserAcceleration: 0.075f, chaserMaxSpeed: 10f,
                                finishBrake: 5.5f);

        private static Entity Bird(float forwardSpeed, float verticalSpeed, bool jump)
        {
            var bird = new Entity("bird");
            bird.Add(new Velocity
            {
                Linear = new System.Numerics.Vector3(forwardSpeed, verticalSpeed, 0f)
            });
            var buffer = new InputBuffer();
            buffer.SetCurrent(new InputCommand { Jump = jump });
            bird.Add(buffer);
            return bird;
        }

        [Test]
        public void 통과하면_중력이_안_실린다()
        {
            var bird = Bird(11f, 0f, jump: false);

            new FlappyMoveSystem(Config()).Tick(bird, Dt, dashing: false, finished: true);

            Assert.That(bird.Get<Velocity>().Linear.Y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void 통과하면_날갯짓이_안_먹는다()
        {
            //  골인 뒤 행동이 등수에 영향을 주면 안 된다.
            var bird = Bird(11f, 0f, jump: true);

            new FlappyMoveSystem(Config()).Tick(bird, Dt, dashing: false, finished: true);

            Assert.That(bird.Get<Velocity>().Linear.Y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void 통과하면_전진이_줄어든다()
        {
            var bird = Bird(11f, 0f, jump: false);

            new FlappyMoveSystem(Config()).Tick(bird, Dt, dashing: false, finished: true);

            Assert.That(bird.Get<Velocity>().Linear.X, Is.EqualTo(11f - 5.5f * Dt).Within(Tolerance));
        }

        [Test]
        public void 감속은_0에서_멈추고_뒤로_안_간다()
        {
            //  음수로 내려가면 새가 결승선 쪽으로 되돌아온다.
            var bird = Bird(0.01f, 0f, jump: false);

            new FlappyMoveSystem(Config()).Tick(bird, Dt, dashing: false, finished: true);

            Assert.That(bird.Get<Velocity>().Linear.X, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void 통과_전에는_평소대로다()
        {
            var bird = Bird(11f, 0f, jump: false);

            new FlappyMoveSystem(Config()).Tick(bird, Dt, dashing: false, finished: false);

            Assert.That(bird.Get<Velocity>().Linear.X, Is.EqualTo(11f).Within(Tolerance));
            Assert.That(bird.Get<Velocity>().Linear.Y, Is.EqualTo(-70f * Dt).Within(Tolerance));
        }
    }
}
```

> `InputBuffer.SetCurrent`의 정확한 이름은 기존 테스트(`FlappyMoveSystemTests.cs`)에서 확인해
> 그대로 쓴다. 다르면 그쪽에 맞춘다.

- [ ] **Step 4: 컴파일해서 빨간지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

기대: `finished` 인자와 `finishBrake`가 없어 **컴파일 에러**.

- [ ] **Step 5: 설정값을 붙인다**

`FlappyConfig.cs` — `ChaserMaxSpeed` 뒤에:

```csharp
        /// <summary>결승선을 넘은 뒤의 감속(m/s²). 이 값이 골인 뒤 몇 미터를 더 가는지를 정한다.</summary>
        public readonly float FinishBrake;
```

생성자 꼬리에 `float finishBrake = 0f`를 더하고 본문에 `FinishBrake = finishBrake;`를 넣는다.
(추격자 값과 같은 이유로 기본값을 준다 — 이 값과 무관한 테스트가 자리채움을 안 적게.)

클·서 `FlappyConfigProvider.cs` 양쪽에 `finishBrake: r.FinishBrake,`를 더한다.

- [ ] **Step 6: 이동 규칙에 감속을 넣는다**

`FlappyMoveSystem.Tick` 시그니처에 `bool finished`를 더하고, `dashing` 분기 **앞**에:

```csharp
            if (finished)
            {
                //  골인한 새는 조작이 끊기고 스스로 멈춘다. 중력도 날갯짓도 없는 수평 직선이라
                //  대시와 같은 모양이고, 다른 것은 전진이 지속이 아니라 감속이라는 것뿐이다.
                velocity.y = 0f;
                velocity.x = velocity.x - config.FinishBrake * deltaTime;
                if (velocity.x < 0f)
                {
                    velocity.x = 0f;   // 음수로 가면 결승선 쪽으로 되돌아온다
                }
                velocity.z = 0f;
                worldVelocity.Linear = velocity.ToNumerics();
                return;
            }
```

- [ ] **Step 7: 월드가 그 값을 넘긴다**

`FlappyWorld.Mutation`의 이동 호출을 바꾼다:

```csharp
                bool finished = _birds[i].Get<FinishState>()?.Finished ?? false;
                _moveSystem.Tick(_birds[i], deltaTime, _dashSystem.IsDashing(_birds[i]), finished);
```

- [ ] **Step 8: 컴파일하고 테스트가 초록인지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path $P
unity cmd recompile_status --project-path $P
unity cmd run_tests --project-path $P --mode EditMode --async_tests true
sleep 60; unity cmd test_status --project-path $P
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
```

기대: 전부 통과. **`total`이 5 늘었는지 확인한다.**

- [ ] **Step 9: 뮤테이션으로 확인한다**

`if (velocity.x < 0f) { velocity.x = 0f; }`를 지우고 다시 돌린다.
기대: `감속은_0에서_멈추고_뒤로_안_간다`가 빨강. 되돌린다.

- [ ] **Step 10: 커밋한다 (레포 5개)**

```bash
cd /Users/insoobae/workspace/LOP/infrastructure
git add 'table/Datas/#FlappyConfig.xlsx'
git status --short
git commit -m "feat(table): 골인 뒤 감속 값을 넣는다"

cd ../LeagueOfPhysical-MasterData-Client   # 그리고 -Server
git add Runtime.Generated
git status --short
git commit -m "chore(masterdata): 골인 감속 값 반영 재생성"

cd ../LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/FlappyConfig.cs Runtime/Scripts/Game/FlappyMoveSystem.cs \
        Runtime/Scripts/Game/FlappyWorld.cs \
        Tests/EditMode/FlappyMoveSystemFinishTests.cs Tests/EditMode/FlappyMoveSystemFinishTests.cs.meta
git status --short
git commit -m "feat(flappy): 골인한 새가 수평으로 감속해 멈춘다"

# 클라 / 서버 각각
git add Assets/Scripts/Game/FlappyConfigProvider.cs
git commit -m "feat(flappy): 골인 감속 값을 시뮬에 넘긴다"
```

---

## Task 6: 등수를 스냅샷으로 보낸다

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FinishPlacement.cs`
- Modify: `LeagueOfPhysical-Shared/Protos/EntitySnap.proto`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Domain/FinishPlacements.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/FinishTrackingSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs:85` 부근
- Modify: 클·서 `Assets/Scripts/Entity/FlappyBirdCreator.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/EntitySnap.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs:145` 부근
- Test: `LeagueOfPhysical-Server/Assets/Tests/Editor/FinishPlacementsTests.cs`

**Interfaces:**
- Produces: `FinishPlacement` 컴포넌트 — `int Value` (0 = 아직)
- Produces: `EntitySnap.finish_placement` (int32, 0 = 아직)
- Produces: `FinishPlacements.PlacementIn(IReadOnlyList<FinishRecord> ordered, string entityId) -> int` (0 = 아직)

> **왜 보관소가 아니라 컴포넌트인가.** `EntitySnapshotBroadcastSystem`은 **게임을 안 가리는
> 공용**이다(`GameplayInstaller`에 등록). Flappy 전용 `FinishTrackingSystem`을 거기에 주입하면
> 판치기·FlapWang이 깨진다. 이 시스템이 이미 쓰는 방식은 **엔티티에서 컴포넌트를 읽고 없으면
> 기본값**이다(Skydive 전용 필드가 그렇게 나간다). 등수도 같은 모양으로 태운다.

> ⚠️ **`EntitySnap`은 타입이 둘이다** — 와이어(proto, PascalCase)와 도메인
> (`Assets/Scripts/Netcode/EntitySnap.cs`, camelCase). 다만 **이 짝은 AutoMapper가 이름으로
> 자동 변환**한다(`MapperConfig.mapper.Map<EntitySnap>`) — `InputCommand`처럼 수기 변환부를
> 찾아다닐 필요가 없다. 도메인 필드 이름만 규칙(camelCase)에 맞추면 된다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`FinishPlacementsTests.cs` 끝(마지막 `}` 두 개 앞)에 추가:

```csharp
        [Test]
        public void 순위를_하나만_뽑을_수도_있다()
        {
            var ordered = new[] { Rec("a", 10, 0.5f), Rec("b", 11, 0.2f), Rec("c", 12, 0.1f) };

            Assert.AreEqual(1, FinishPlacements.PlacementIn(ordered, "a"));
            Assert.AreEqual(2, FinishPlacements.PlacementIn(ordered, "b"));
            Assert.AreEqual(3, FinishPlacements.PlacementIn(ordered, "c"));
        }

        [Test]
        public void 하나만_뽑을_때도_동점은_공동_순위다()
        {
            var ordered = new[] { Rec("a", 10, 0.5f), Rec("b", 10, 0.5f), Rec("c", 11, 0.1f) };

            Assert.AreEqual(1, FinishPlacements.PlacementIn(ordered, "a"));
            Assert.AreEqual(1, FinishPlacements.PlacementIn(ordered, "b"));
            Assert.AreEqual(3, FinishPlacements.PlacementIn(ordered, "c"));
        }

        [Test]
        public void 아직_안_들어온_사람은_0이다()
        {
            //  0은 "아직"이다. proto3가 기본값을 안 실으므로 와이어 비용도 0이다.
            Assert.AreEqual(0, FinishPlacements.PlacementIn(new[] { Rec("a", 10, 0.5f) }, "b"));
        }
```

- [ ] **Step 2: 컴파일해서 빨간지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
```

기대: `PlacementIn`이 없어 **컴파일 에러**.

- [ ] **Step 3: 순위 뽑기를 만든다**

`FinishPlacements.cs`에 추가:

```csharp
        /// <summary>
        /// 한 사람의 순위만 뽑는다(1부터). <b>아직 안 들어왔으면 0.</b>
        /// 공동 순위 규칙은 <see cref="Resolve"/>와 같다 — 1·1·3.
        /// </summary>
        public static int PlacementIn(IReadOnlyList<FinishRecord> ordered, string entityId)
        {
            int placement = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                if (i == 0 || ordered[i].SameRankAs(ordered[i - 1]) == false)
                {
                    placement = i + 1;
                }
                if (ordered[i].EntityId == entityId)
                {
                    return placement;
                }
            }
            return 0;
        }
```

- [ ] **Step 4: 테스트가 초록인지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
unity cmd recompile --project-path $P
unity cmd recompile_status --project-path $P
unity cmd run_tests --project-path $P --mode EditMode --async_tests true
sleep 60; unity cmd test_status --project-path $P
```

기대: 통과. **`total`이 3 늘었는지 확인한다.**

- [ ] **Step 5: 뮤테이션으로 확인한다**

`PlacementIn`의 `placement = i + 1;`을 `placement = i;`로 바꾸고 돌린다.
기대: `순위를_하나만_뽑을_수도_있다`, `하나만_뽑을_때도_동점은_공동_순위다`가 빨강. 되돌린다.

- [ ] **Step 6: 등수 컴포넌트를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/FinishPlacement.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 서버가 정한 결승선 등수(1부터, 0 = 아직). <b>시뮬은 이 값을 읽지도 쓰지도 않는다</b> —
    /// 서버가 채워 스냅샷으로 보내고 화면이 읽는 표시값이다.
    ///
    /// <para>그래서 되돌리기 대상이 아니다(<c>FlappySavedState</c>에 안 담는다). 반대로
    /// <see cref="FinishState"/>는 시뮬이 적는 값이라 담는다 — 둘을 나눠 둔 이유가 이것이다.</para>
    /// </summary>
    public class FinishPlacement : GameFramework.World.Component
    {
        public int Value;
    }
}
```

클·서 `FlappyBirdCreator.cs`에서 `FinishState`를 붙이는 줄 아래에:

```csharp
            worldEntity.Add(new FinishPlacement());
```

- [ ] **Step 7: 서버가 매 틱 채운다**

`FinishTrackingSystem.Tick`의 루프 끝에 추가:

```csharp
                //  등수는 남들이 언제 들어왔는지에 달려 있어 서버만 안다. 스냅샷에 태우려고
                //  컴포넌트에 적어 둔다 — 스냅샷을 만드는 코드는 게임을 안 가려서 이 시스템을
                //  알 수 없다.
                var placement = entityRegistry.Get(watched[i])?.Get<FinishPlacement>();
                if (placement != null)
                {
                    placement.Value = FinishPlacements.PlacementIn(tracker.Ordered, watched[i]);
                }
```

- [ ] **Step 8: proto에 필드를 더한다**

`LeagueOfPhysical-Shared/Protos/EntitySnap.proto`의 `dash_charge = 21;` 아래:

```proto
	int32 finish_placement = 22;    // 결승선 등수(1부터). 0 = 아직 안 들어옴
```

생성은 **`compile_protos.sh`만** 부른다 — `generate_protos.sh`는 `rm -rf`로 생성 폴더를 지워
`.meta`(GUID)를 날린다.

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
find . -name 'compile_protos.sh' -not -path './.git/*'
bash <위에서 찾은 경로>
git status --short          # Runtime.Generated의 EntitySnap.cs 하나만 바뀌어야 한다
```

- [ ] **Step 9: 서버 스냅에 싣는다**

`EntitySnapshotBroadcastSystem.cs`의 `snap.DashCharge = dash?.Charge ?? 0f;` 아래:

```csharp
                //  결승선이 없는 게임에는 이 컴포넌트가 없다 — 그러면 0(아직)이 나간다.
                //  위의 Skydive 전용 필드와 같은 방식이다.
                snap.FinishPlacement = worldEntity.Get<FinishPlacement>()?.Value ?? 0;
```

- [ ] **Step 10: 클라 도메인 타입에 더한다**

`LeagueOfPhysical-Client/Assets/Scripts/Netcode/EntitySnap.cs`의 `dashCharge` 아래:

```csharp
        /// <summary>결승선 등수(1부터). 0 = 아직 안 들어옴.</summary>
        public int finishPlacement { get; set; }
```

이름이 `FinishPlacement` ↔ `finishPlacement`로 맞으므로 AutoMapper가 알아서 채운다.

- [ ] **Step 11: 클라가 컴포넌트에 적는다**

`GameEntityMessageHandler.OnEntityShapsToC`의 per-entity 루프에서 `targetEntity`를 얻은 직후
(예측/보간 갈래로 나뉘기 **전**):

```csharp
                //  내 새든 남의 새든 등수는 서버 값 하나뿐이라 갈래를 나누기 전에 적는다.
                var placement = targetEntity?.Get<FinishPlacement>();
                if (placement != null)
                {
                    placement.Value = entitySnap.finishPlacement;
                }
```

- [ ] **Step 12: 클·서 둘 다 컴파일한다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
for P in /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server; do
  unity cmd recompile --project-path $P
  unity cmd recompile_status --project-path $P
done
```

- [ ] **Step 13: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/FinishPlacement.cs Runtime/Scripts/Game/FinishPlacement.cs.meta \
        Protos/EntitySnap.proto Runtime.Generated/Scripts/Protobuf/EntitySnap.cs
git status --short
git commit -m "feat(race): 서버가 정한 등수를 스냅샷에 싣는다"

cd ../LeagueOfPhysical-Server
git add Assets/Scripts/Domain/FinishPlacements.cs Assets/Tests/Editor/FinishPlacementsTests.cs \
        Assets/Scripts/Game/TickSystems/FinishTrackingSystem.cs \
        Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs \
        Assets/Scripts/Entity/FlappyBirdCreator.cs
git status --short
git commit -m "feat(race): 서버가 등수를 컴포넌트에 적어 스냅에 태운다"

cd ../LeagueOfPhysical-Client
git add Assets/Scripts/Netcode/EntitySnap.cs Assets/Scripts/Entity/FlappyBirdCreator.cs \
        Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs
git status --short
git commit -m "feat(race): 클라가 등수를 받아 컴포넌트에 적는다"
```

---

## Task 7: 골인 화면

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/UI/RaceFinish/RaceFinishView.uxml`, `.uss`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/UI/RaceFinish/RaceFinishView.cs`
- Modify: `LeagueOfPhysical-Client/Assets/UI/UIViewCatalog.asset`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyHudCoordinator.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`

**Interfaces:**
- Consumes: `FinishPlacement.Value` (Task 6), `FinishState.Finished` (Task 1)
- Produces: `LOP.UI.RaceFinishView` — `void SetPlacement(int placement)`

- [ ] **Step 1: 레이아웃을 만든다**

`Assets/UI/RaceFinish/RaceFinishView.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="race-finish-root" class="race-finish-root" picking-mode="Ignore">
        <ui:Label name="race-finish-place" class="race-finish-place" text="" picking-mode="Ignore" />
        <ui:Label name="race-finish-note" class="race-finish-note" text="완주" picking-mode="Ignore" />
    </ui:VisualElement>
</ui:UXML>
```

`Assets/UI/RaceFinish/RaceFinishView.uss`:

```css
/* 아래 화면을 가리지 않는다 — 남은 사람들이 계속 보여야 한다 */
.race-finish-root {
    flex-grow: 1;
    justify-content: flex-start;
    align-items: center;
    padding-top: 80px;
}

.race-finish-place {
    font-size: 88px;
    color: rgb(255, 214, 92);
    -unity-text-align: middle-center;
}

.race-finish-note {
    font-size: 26px;
    color: rgba(255, 255, 255, 0.6);
    -unity-text-align: middle-center;
}
```

- [ ] **Step 2: 뷰를 만든다**

`Assets/Scripts/UI/RaceFinish/RaceFinishView.cs`:

```csharp
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 완주했음과 등수를 알린다. 등수는 서버가 정해 스냅샷으로 오므로, 코디네이터가 받아서 넣어 준다.
    ///
    /// <para>Notification이 아니라 Window인 이유는 <see cref="RaceStartView"/>와 같다:
    /// 토스트가 아니라 게임 화면이라 로딩·결과 같은 전체화면 오버레이에 <b>가려져야</b> 한다.</para>
    /// </summary>
    public class RaceFinishView : UIView
    {
        public override UILayer Layer => UILayer.Window;

        /// <summary>0이면 아직 서버 답이 안 왔다는 뜻이라 자리만 비워 둔다.</summary>
        public void SetPlacement(int placement)
        {
            var label = Root.Q<Label>("race-finish-place");
            label.text = placement > 0 ? $"{placement}등" : string.Empty;
        }
    }
}
```

- [ ] **Step 3: 유니티에 임포트시켜 `.meta`를 만든다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
ls /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/UI/RaceFinish/
```

`.uxml.meta`와 `.uss.meta`가 생겨야 한다.

- [ ] **Step 4: 카탈로그에 등록한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
grep guid Assets/UI/RaceFinish/RaceFinishView.uxml.meta
grep guid Assets/UI/RaceFinish/RaceFinishView.uss.meta
```

`Assets/UI/UIViewCatalog.asset`의 `- viewName: RaceEliminatedView` 항목 아래에 같은 모양으로 넣는다
(`fileID`는 다른 항목과 같은 값: uxml `9197481963319205126`, uss `7433441132597879392`).

```yaml
  - viewName: RaceFinishView
    uxml: {fileID: 9197481963319205126, guid: <uxml guid>, type: 3}
    uss: {fileID: 7433441132597879392, guid: <uss guid>, type: 3}
```

- [ ] **Step 5: DI에 등록한다**

`FlappyRaceLifetimeScope.cs`의 `RaceEliminatedView` 등록 아래:

```csharp
            builder.Register<RaceFinishView>(Lifetime.Transient);
```

`RegisterViewFactories`에:

```csharp
            sink.Add(windowManager.RegisterViewFactory<RaceFinishView>(() => container.Resolve<RaceFinishView>()));
```

- [ ] **Step 6: 코디네이터가 완주를 처리한다**

`FlappyHudCoordinator.cs`는 이미 `EntityRegistry`를 받고 있다. 완주는 사건 알림이 없고 매 틱
바뀌는 상태라(스턴처럼) 코디네이터가 매 프레임 확인한다 — `ITickable`을 함께 구현하고
`Tick()`에서 아래를 부른다:

```csharp
        private RaceFinishView _finishView;

        //  내 새가 결승선을 넘었는지는 시뮬이 안다(FinishState). 등수는 서버가 알려 준다.
        //  둘의 도착 시점이 달라서(통과가 먼저, 등수가 0.2초쯤 뒤) 화면을 먼저 띄우고
        //  등수는 오는 대로 채운다.
        private void UpdateFinish()
        {
            if (_matchEnded || _flapPad == null)
            {
                return;
            }

            var mine = entityRegistry.Get(gameDataStore.userEntityId);
            if (mine?.Get<FinishState>()?.Finished != true)
            {
                return;
            }

            windowManager.Close(_flapPad);
            _flapPad = null;
            _finishView = windowManager.Open<RaceFinishView>();
        }
```

등수 갱신은 `RaceFinishView`가 스스로 매 프레임 읽는 편이 배선이 적다 — 뷰에
`IVisualElementScheduledItem`을 두고(`RaceStartView`와 같은 방식)
`SetPlacement(entityRegistry.Get(myEntityId)?.Get<FinishPlacement>()?.Value ?? 0)`을 부른다.
그러면 코디네이터는 열기만 하면 된다. 뷰가 `EntityRegistry`와 `IGameDataStore`를 주입받으므로
DI 등록은 `Lifetime.Transient` 그대로면 된다.

`UpdateFinish`를 부를 자리가 필요하므로, 코디네이터를 `ITickable`로도 등록한다
(`builder.RegisterEntryPoint<FlappyHudCoordinator>()`가 이미 있으므로 인터페이스만 더한다).

- [ ] **Step 7: 컴파일하고 테스트를 돌린다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path $P
unity cmd recompile_status --project-path $P
unity cmd run_tests --project-path $P --mode EditMode --async_tests true
sleep 60; unity cmd test_status --project-path $P
```

- [ ] **Step 8: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/UI/RaceFinish Assets/UI/RaceFinish.meta \
        Assets/Scripts/UI/RaceFinish Assets/Scripts/UI/RaceFinish.meta \
        Assets/UI/UIViewCatalog.asset \
        Assets/Scripts/Game/FlappyHudCoordinator.cs Assets/Scripts/Game/FlappyRaceLifetimeScope.cs
git status --short
git commit -m "feat(flappy): 완주하면 입력면을 닫고 등수를 띄운다"
```

**폴더 `.meta` 둘을 빠뜨리지 않는다** — 추격자 때 놓쳤다.

---

## Task 8: 추격자 벽을 결승선에서 멈춘다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyChaserCurve.cs`
- Modify: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyChaserCurveTests.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/FlappyChaserSystem.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyChaserView.cs`

**Interfaces:**
- Produces: `FlappyChaserCurve.XAt(in FlappyConfig config, float elapsedSeconds, float stopAtX)` — 인자가 하나 는다

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`FlappyChaserCurveTests.cs`에 추가:

```csharp
        [Test]
        public void 결승선을_지나서는_안_간다()
        {
            //  벽이 결승선을 지나가면, 그 뒤에 멈춰 선 완주자를 통과하는 그림이 나온다.
            //  완주자는 추격자 판정에서 빠져 안 죽는데 화면에서는 먹히는 것처럼 보인다.
            Assert.That(FlappyChaserCurve.XAt(Config(), 120f, stopAtX: 632f),
                Is.EqualTo(632f).Within(Tolerance));
        }

        [Test]
        public void 결승선_전에는_상한이_영향을_주지_않는다()
        {
            Assert.That(FlappyChaserCurve.XAt(Config(), 40f, stopAtX: 632f),
                Is.EqualTo(280f).Within(Tolerance));
        }
```

기존 테스트들의 `XAt(Config(), t)` 호출에는 `stopAtX: float.MaxValue`를 더한다.

- [ ] **Step 2: 컴파일해서 빨간지 본다**

기대: 인자 개수가 안 맞아 **컴파일 에러**.

- [ ] **Step 3: 상한을 넣는다**

`FlappyChaserCurve.XAt`의 시그니처와 반환에:

```csharp
        /// <param name="stopAtX">
        /// 벽이 더 가지 않는 자리(결승선). 여기서 멈춰도 잡는 능력은 안 줄어든다 — 벽이 결승선에
        /// 닿은 시점에 아직 못 들어온 사람은 전부 벽 뒤에 있다.
        /// </param>
        public static float XAt(in FlappyConfig config, float elapsedSeconds, float stopAtX)
        {
            float x = /* 기존 계산 그대로 */;
            return x > stopAtX ? stopAtX : x;
        }
```

기존 세 갈래(출발 전 / 가속 중 / 등속)를 지역 변수 `x`에 담고 마지막에 한 번만 자른다.

- [ ] **Step 4: 두 호출부가 결승선을 넘긴다**

서버 `FlappyChaserSystem`과 클라 `FlappyChaserView`가 `FinishLineBounds`를 주입받아
근접면 좌표를 넘긴다:

```csharp
            float stopAtX = line.TryGet(out var bounds) ? bounds.min.x : float.MaxValue;
            float wallX = FlappyChaserCurve.XAt(config, elapsed, stopAtX);
```

- [ ] **Step 5: 컴파일하고 테스트가 초록인지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
for P in /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server; do
  unity cmd recompile --project-path $P
  unity cmd recompile_status --project-path $P
done
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --async_tests true
sleep 60; unity cmd test_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

기대: 통과. **`total`이 2 늘었는지 확인한다.**

- [ ] **Step 6: 뮤테이션으로 확인한다**

`return x > stopAtX ? stopAtX : x;`를 `return x;`로 바꾸고 돌린다.
기대: `결승선을_지나서는_안_간다`가 빨강. 되돌린다.

- [ ] **Step 7: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/FlappyChaserCurve.cs Tests/EditMode/FlappyChaserCurveTests.cs
git commit -m "feat(flappy): 추격자 벽이 결승선에서 멈춘다"

cd ../LeagueOfPhysical-Server
git add Assets/Scripts/Game/TickSystems/FlappyChaserSystem.cs
git commit -m "feat(flappy): 서버가 벽 상한으로 결승선을 넘긴다"

cd ../LeagueOfPhysical-Client
git add Assets/Scripts/Game/FlappyChaserView.cs
git commit -m "feat(flappy): 클라가 벽 상한으로 결승선을 넘긴다"
```

---

## Task 9: Part 2 라이브 검증과 푸시

**Files:** 코드 변경 없음.

- [ ] **Step 1: 여섯 레포를 순서대로 푸시한다**

의존 순서: infrastructure → MasterData 둘 → LOP-Shared → 서버·클라.

레포마다 (`&&`로 잇지 말고 한 줄씩):

```bash
cd <repo>
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/race-finish
git push origin main
git branch -d feature/race-finish
```

- [ ] **Step 2: 두 에디터로 한 판 돌린다**

오토파일럿을 켜고 봇 둘이 완주하게 한다(현재 값에서 봇은 약 70초에 완주한다).

- [ ] **Step 3: 여섯 가지를 확인한다**

1. **통과 틱이 Part 1과 같다** — Task 4에서 적어 둔 값과 대조. 감속을 넣었다고 통과 시점이
   바뀌면 안 된다(감속은 통과 *다음* 틱부터다)
2. **수평 직선으로 감속해 멈춘다** — 결승선 뒤 약 11m에서 정지
3. **등수가 뜬다** — 서버 `[Outcome]` 로그와 화면의 등수가 일치
4. **입력면이 닫힌다** — 날갯짓·대시 버튼이 사라진다
5. **남의 새도 감속해 보인다** — 로컬 판정 없이 스냅샷만으로 그렇게 보여야 한다
6. **벽이 결승선에서 멈춘다** — 완주자를 통과하지 않는다

- [ ] **Step 4: Skydive가 안 깨졌는지 본다**

`ConfigureRoomComponent`를 Skydive로 바꿔 한 판. 등수가 나오고 예외가 없으면 된다.

- [ ] **Step 5: 로드맵에 적는다**

`docs/ROADMAP.md`의 닫힌 것 표에 한 줄. 관측한 **수치**(통과 틱·깊이·등수, 정지 거리)를 함께
남긴다. 열린 항목의 "결승선 연출"을 지우고 **"관전·나가기 UI"** 를 추가한다(완주·탈락 공통).

`docs/` 변경은 별도 브랜치에서 같은 절차로 푸시한다.

---

## 마지막 확인

- [ ] 여섯 레포 전부 `git rev-list --left-right --count origin/main...HEAD`가 `0 0`
- [ ] 클라·서버 로컬 픽스처가 커밋되지 않았다
- [ ] `FinishPlacements`·`FinishOrderTracker`·`FinishLineOverlap` 테스트가 **한 줄도 안 바뀌었다**
      (Part 1이 동작을 안 바꿨다는 증거)
- [ ] 새로 만든 폴더의 `.meta`가 전부 커밋됐다
