# Flappy Race 남의 새 시뮬 전환 — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Flappy Race에서 남의 새를 외삽으로 그리는 대신 진짜 게임 코드로 굴린다.

**Architecture:** 동기화 정책을 `CharactersPredictedSyncPolicy`로 되돌려 모든 캐릭터를 `Predicted`로
만든다. 그러면 `EntityBinder`가 남의 새에도 `Simulated` 표식을 붙여 `FlappyWorld.Tick`이 굴리고,
스냅샷은 `Reconciler`의 되감기·재생 경로로 간다. 남의 입력은 오지 않으므로 `InputBuffer.Current`가
계속 `null`이라 "안 눌렀다"가 저절로 성립한다. 새로 생기는 위험은 **남의 스턴을 클라가 예측하게
된다**는 것이라, 스턴을 서버 권위로 덮는 보정 핸들러를 함께 넣는다.

**Tech Stack:** Unity 6000.3, VContainer, Mirror, protobuf(Luban 아님 — wire용 protoc), NUnit EditMode

**Spec:** `docs/superpowers/specs/2026-08-27-flappy-remote-simulation-design.md`

## Global Constraints

- **범위는 Flappy Race만.** 다른 게임 모드의 동기화 정책은 건드리지 않는다.
- **LOP 측 파일에서 `using GameFramework.World;`를 추가하지 않는다.** World 타입은 항상 풀
  네임스페이스로 한정한다(`GameFramework.World.Entity` 등) — `Component`가 `UnityEngine.Component`와
  겹치기 때문이다.
- **`.cs`와 짝 `.meta`를 함께 `git mv`/`git add`** 한다. 새 파일은 Unity가 만든 `.meta`를 반드시 커밋한다.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고, 커밋 전 `git status --short`로
  스테이지된 것이 의도한 파일뿐인지 확인한다.
- **워킹트리에 커밋하면 안 되는 로컬 픽스처가 있다**(서버 `ConfigureRoomComponent.cs`, 폰트,
  프로젝트 설정, 아트 서브모듈 포인터). 절대 스테이지하지 않는다.
- **에디터가 Play Mode면 리컴파일하지 말고 멈춰서 보고한다.** 사람이 라이브 세션을 돌리고 있을 수 있다.
- **테스트 실행**: `unity cmd run_tests --project-path <절대경로> --mode EditMode --async_tests true`
  → `unity cmd test_status`로 폴링. `--async_tests` 없이 돌리면 타임아웃 후 `total:0`을 **초록처럼**
  돌려주므로 반드시 `total`이 기대한 개수인지 확인한다.
- **기준선**: 이 계획 시작 시점 client 624/624, server 587/587.

---

## 스펙 정정 — 스턴 와이어 (이 계획에서 결정)

스펙 §6.4는 "스턴이 다르면 서버 값으로 덮는다"고만 적었는데, **지금 와이어로는 그게 불가능하다.**
스냅샷의 `stunned`/`invulnerable`은 **불리언**이라 남은 시간을 담지 못한다. 서버가 0.3초 남은
상태를 "켜짐"으로 보내면 클라는 0.8초를 새로 채우고, 다음 스냅샷도 "켜짐"이라 불일치로도 안 잡혀
**0.5초를 더 얼어붙는다.**

이 코드베이스엔 이미 표준이 있다 — 어빌리티가 `ability_end_tick`(시전이 끝나는 **절대 틱**)을
나른다. 같은 방식으로 간다:

| 지금 | 바꾼 뒤 |
|---|---|
| `bool stunned = 12` | `int64 stun_end_tick = 14` |
| `bool invulnerable = 13` | `int64 invuln_end_tick = 15` |

12·13은 `reserved`로 은퇴시킨다(번호 재사용 금지 — 판치기가 은퇴 슬롯을 재사용해 충돌한 사고가
바로 지난 슬라이스에 있었다). 화면 표시는 `endTick > 현재 틱`으로 바뀔 뿐이다.

---

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Client: Assets/Scripts/EntitySync/CharactersPredictedSyncPolicy.cs` | **복원.** 캐릭터는 전부 예측, 그 외는 보간 |
| `Shared: Protos/EntitySnap.proto` | 스턴 와이어를 불리언 → 종료 틱으로 |
| `Client: Assets/Scripts/Netcode/EntitySnap.cs` | 클라 스냅 DTO의 같은 두 필드 |
| `Server: .../TickSystems/EntitySnapshotBroadcastSystem.cs` | 종료 틱을 채운다 |
| `Shared: Runtime/Scripts/Game/FlappyWorld.cs` | `TryGetSavedStun` 조회 추가 |
| `Client: Assets/Scripts/Netcode/FlappyServerCorrectionHandler.cs` | **신규.** 스턴 권위 = 서버 |
| `Client: Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` | 정책·보정 핸들러 DI 교체 |
| `Client: Assets/Scripts/Netcode/Reconciler.cs` | 재생 루프의 틀린 주석 정정 |
| `Client/Shared: 각 EditMode 테스트` | 아래 각 태스크 |

---

### Task 1: 스턴을 종료 틱으로 나른다 (와이어)

**Files:**
- Modify: `LeagueOfPhysical-Shared/Protos/EntitySnap.proto`
- Regenerate: `LeagueOfPhysical-Shared/Runtime.Generated/Scripts/Protobuf/EntitySnap.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/EntitySnap.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/StunVisuals.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/SnapshotEntityInterpolator.cs`,
  `ExtrapolatedEntityInterpolator.cs`, `PredictedEntityInterpolator.cs` (호출부)
- Test: `LeagueOfPhysical-Client/Assets/Tests/Editor/StunFieldMapperTests.cs`,
  `StunVisualsTests.cs`

**Interfaces:**
- Produces: `EntitySnap.stunEndTick` (long), `EntitySnap.invulnEndTick` (long),
  `StunVisuals.Of(EntitySnap snap, long currentTick)`, `StunVisuals.Of(FlappyStun stun)`

- [ ] **Step 1: proto를 고친다**

`LeagueOfPhysical-Shared/Protos/EntitySnap.proto`의 마지막 두 줄을 바꾼다:

```proto
	reserved 12, 13;                // 옛 bool stunned / invulnerable — 남은 시간을 못 담아 은퇴
	int64 stun_end_tick = 14;       // Flappy: 멈춤이 풀리는 절대 틱. 0 = 안 멈춤
	int64 invuln_end_tick = 15;     // Flappy: 다시 안 걸리는 구간이 끝나는 절대 틱. 0 = 아님
}
```

- [ ] **Step 2: 생성 스크립트를 돌린다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Scripts
bash compile_protos.sh && bash generate_imessage.sh && bash generate_message_ids.sh && bash generate_message_initializer.sh
```

`.sh`에 실행 권한이 없으므로 **반드시 `bash`로** 돌린다. 스크립트가 `.meta`까지 지우므로 끝나면
지워진 `.meta`를 되돌린다(GUID 보존):

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git status --short | grep "^ D" | sed 's/^ D //' | tr '\n' '\0' | xargs -0 -r git checkout --
git diff --stat   # EntitySnap.cs 하나만 바뀌어야 한다. MessageIds.cs가 바뀌면 멈추고 보고할 것
```

- [ ] **Step 3: 매퍼 테스트를 새 필드로 바꾸고 빨강을 확인한다**

`LeagueOfPhysical-Client/Assets/Tests/Editor/StunFieldMapperTests.cs` 전체를 교체:

```csharp
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// EntitySnap.proto의 스턴 필드가 AutoMapper convention(ForMember 없이 이름만 맞춰 매핑)으로
    /// 실제로 옮겨지는지 확인한다. ProtoMapperProfile은 이 필드들을 명시적으로 다루지 않으므로,
    /// convention이 깨지면 클라가 항상 0을 보게 되는데 그걸 잡아낼 테스트가 이거뿐이다.
    /// </summary>
    public class StunFieldMapperTests
    {
        [Test]
        public void 멈춤_종료틱이_AutoMapper_컨벤션으로_옮겨진다()
        {
            var proto = new global::EntitySnap { StunEndTick = 1234 };

            EntitySnap mapped = MapperConfig.mapper.Map<EntitySnap>(proto);

            Assert.AreEqual(1234, mapped.stunEndTick);
        }

        [Test]
        public void 무적_종료틱이_AutoMapper_컨벤션으로_옮겨진다()
        {
            var proto = new global::EntitySnap { InvulnEndTick = 5678 };

            EntitySnap mapped = MapperConfig.mapper.Map<EntitySnap>(proto);

            Assert.AreEqual(5678, mapped.invulnEndTick);
        }
    }
}
```

Run: `unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --filter StunFieldMapperTests --async_tests true`
Expected: 컴파일 실패 — `EntitySnap`에 `stunEndTick`이 없다.

- [ ] **Step 4: 클라 스냅 DTO를 고친다**

`LeagueOfPhysical-Client/Assets/Scripts/Netcode/EntitySnap.cs`에서 두 줄을 바꾼다:

```csharp
        public long stunEndTick { get; set; }     // Flappy: 멈춤이 풀리는 절대 틱. 0 = 안 멈춤
        public long invulnEndTick { get; set; }   // Flappy: 다시 안 걸리는 구간이 끝나는 절대 틱
```

- [ ] **Step 5: 서버가 종료 틱을 채우게 한다**

`LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs`에서
`snap.Stunned`/`snap.Invulnerable`을 세우던 두 줄을 바꾼다. `Tick(long tick, ...)`의 `tick`을 쓴다:

```csharp
                var stun = worldEntity.Get<FlappyStun>();
                //  남은 시간이 아니라 "끝나는 절대 틱"을 보낸다 — 받는 쪽이 자기 틱과 빼면 되고,
                //  스냅이 늦게 도착해도 값이 낡지 않는다(어빌리티의 ability_end_tick과 같은 관례).
                snap.StunEndTick = StunEndTick(stun?.StunRemaining ?? 0f, tick, deltaTime);
                snap.InvulnEndTick = StunEndTick(stun?.InvulnRemaining ?? 0f, tick, deltaTime);
```

같은 클래스 안에 헬퍼를 더한다:

```csharp
        //  남은 시간(초) → 끝나는 절대 틱. 0 이하면 0(= 해당 상태 아님).
        private static long StunEndTick(float remaining, long tick, float deltaTime)
        {
            if (remaining <= 0f || deltaTime <= 0f)
            {
                return 0;
            }
            return tick + (long)System.Math.Ceiling(remaining / deltaTime);
        }
```

- [ ] **Step 6: 화면 표시가 종료 틱을 읽게 한다**

`LeagueOfPhysical-Client/Assets/Scripts/Entity/StunVisuals.cs`의 스냅 오버로드를 바꾼다:

```csharp
        /// <summary>남의 새 — 서버 스냅샷이 진실원본이다. 종료 틱을 현재 틱과 비교한다.</summary>
        public static StunVisual Of(EntitySnap snap, long currentTick)
        {
            if (snap == null)
            {
                return StunVisual.None;
            }
            return Resolve(snap.stunEndTick > currentTick, snap.invulnEndTick > currentTick);
        }
```

`FlappyStun` 오버로드는 그대로 둔다.

- [ ] **Step 7: 호출부 세 곳에 현재 틱을 넘긴다**

세 보간기가 `StunVisuals.Of(snap)`을 부르고 있다. 각각 러너의 현재 틱을 넘기게 고친다.

`SnapshotEntityInterpolator.cs`, `ExtrapolatedEntityInterpolator.cs`:

```csharp
            stunAppearance?.SetState(StunVisuals.Of(snap, runner.tickUpdater.tick));
```

두 클래스에 `runner`가 없으면 `[Inject] GameFramework.Runner.IRunner runner;`를 필드로 더한다
(`PredictedEntityInterpolator`가 이미 그렇게 갖고 있으니 그 선언을 그대로 베낀다).
`PredictedEntityInterpolator`는 `FlappyStun` 오버로드를 쓰므로 손대지 않는다.

- [ ] **Step 8: StunVisualsTests를 새 시그니처로 고친다**

`LeagueOfPhysical-Client/Assets/Tests/Editor/StunVisualsTests.cs`에서 스냅을 쓰는 테스트들을
종료 틱 형태로 바꾼다. 예:

```csharp
        [Test]
        public void 스냅의_멈춤은_정지로_보인다()
        {
            Assert.AreEqual(StunVisual.Stunned, StunVisuals.Of(new EntitySnap { stunEndTick = 100 }, 50));
        }

        [Test]
        public void 종료틱이_지났으면_평소다()
        {
            Assert.AreEqual(StunVisual.None, StunVisuals.Of(new EntitySnap { stunEndTick = 100 }, 100));
        }

        [Test]
        public void 둘_다_남아_있으면_멈춤이_이긴다()
        {
            var snap = new EntitySnap { stunEndTick = 100, invulnEndTick = 120 };

            Assert.AreEqual(StunVisual.Stunned, StunVisuals.Of(snap, 50));
        }
```

`내_새와_남의_새가_같은_규칙으로_보인다` 테스트도 스냅 쪽 인자를 `(snap, 0)` 형태로 맞춘다.

- [ ] **Step 9: 클라·서버 전체 테스트**

Run:
```bash
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --async_tests true
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server --mode EditMode --async_tests true
```
Expected: 둘 다 전부 통과. `total`이 기준선(624 / 587)에서 새 테스트 수만큼 늘어야 한다.

- [ ] **Step 10: 커밋 (3레포)**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git add Protos/EntitySnap.proto Runtime.Generated/Scripts/Protobuf/EntitySnap.cs
git status --short
git commit -m "refactor(wire): 스턴을 켜짐/꺼짐 대신 끝나는 틱으로 나른다

불리언은 남은 시간을 못 담아, 서버가 0.3초 남은 상태를 '켜짐'으로 보내면 클라가 0.8초를 새로
채워 더 얼어붙는다. 다음 스냅도 '켜짐'이라 불일치로도 안 잡힌다. 어빌리티의 ability_end_tick과
같은 관례로 끝나는 절대 틱을 보낸다. 12·13은 reserved로 은퇴시킨다 — 은퇴 번호 재사용이
지난 슬라이스에서 판치기와 충돌한 적이 있다."
```

Client와 Server도 각각 바꾼 파일만 지정해 커밋한다.

---

### Task 2: 되감기가 그 틱의 스턴을 돌려준다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldSaveStateTests.cs`

**Interfaces:**
- Consumes: `FlappySavedState { float StunRemaining; float InvulnRemaining; }` (기존)
- Produces: `bool FlappyWorld.TryGetSavedStun(long tick, string entityId, out FlappySavedState state)`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`FlappyWorldSaveStateTests.cs`에 더한다:

```csharp
        [Test]
        public void 저장된_틱의_스턴을_되돌려_읽을_수_있다()
        {
            //  보정 핸들러가 "그 틱에 내가 뭘 예측했나"를 서버 값과 비교하려면 이 조회가 필요하다.
            var world = FlappyWorldFixture.Create(new FlappyWorldFixture.AlwaysHit(), out var bird);
            world.GameplayStartTick = 0;

            for (long t = 1; t <= 5; t++) { world.Tick(t, 0.02f); world.SaveState(t); }
            float atFive = bird.Get<FlappyStun>().StunRemaining;

            Assert.IsTrue(world.TryGetSavedStun(5, bird.Id, out var saved));
            Assert.AreEqual(atFive, saved.StunRemaining, 1e-4f);
        }

        [Test]
        public void 저장이_없는_틱은_false다()
        {
            var world = FlappyWorldFixture.Create(new FlappyWorldFixture.AlwaysHit(), out var bird);
            world.GameplayStartTick = 0;

            Assert.IsFalse(world.TryGetSavedStun(999, bird.Id, out _));
        }
```

- [ ] **Step 2: 빨강을 확인한다**

Run: `unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --filter FlappyWorldSaveStateTests --async_tests true`
Expected: 컴파일 실패 — `TryGetSavedStun`이 없다.

- [ ] **Step 3: 조회를 더한다**

`FlappyWorld.cs`의 `LoadGameState` 아래에 더한다. `LOPWorld.TryGetSavedStatusEffects`와 같은 모양이다:

```csharp
        /// <summary>그 틱에 저장해 둔 스턴 상태. 서버 스냅과 비교해 되돌릴지 정할 때 쓴다.</summary>
        public bool TryGetSavedStun(long tick, string entityId, out FlappySavedState state)
        {
            if (_gameFrames.TryGet(tick, out var frame) && frame.TryGetValue(entityId, out state))
            {
                return true;
            }
            state = default;
            return false;
        }
```

- [ ] **Step 4: 초록을 확인한다**

Run: 같은 명령. Expected: 2개 통과.

- [ ] **Step 5: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/FlappyWorld.cs Tests/EditMode/FlappyWorldSaveStateTests.cs
git status --short
git commit -m "feat(flappy): 저장된 틱의 스턴을 되돌려 읽는다

보정 핸들러가 '그 틱에 내가 예측한 스턴'을 서버 값과 비교하려면 필요하다.
LOPWorld.TryGetSavedStatusEffects와 같은 모양."
```

---

### Task 3: 스턴 권위를 서버로 (보정 핸들러)

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/FlappyServerCorrectionHandler.cs` (+ `.meta`)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`
- Test: `LeagueOfPhysical-Client/Assets/Tests/Editor/FlappyServerCorrectionHandlerTests.cs` (+ `.meta`)
  (StunVisualsTests·StunFieldMapperTests와 같은 폴더 — 이 폴더엔 asmdef가 없어 기본
  Editor 어셈블리로 들어간다. 새 폴더를 만들지 말 것)

**Interfaces:**
- Consumes: `FlappyWorld.TryGetSavedStun(long, string, out FlappySavedState)` (Task 2),
  `EntitySnap.stunEndTick` / `invulnEndTick` (Task 1), `IServerCorrectionHandler` (기존)
- Produces: `FlappyServerCorrectionHandler : IServerCorrectionHandler`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

테스트가 `FlappyWorld`를 직접 만들기 어려우면 **비교 규칙만 순수 함수로 뽑아** 검사한다.
아래처럼 정적 판정 함수를 두고 그것을 테스트한다:

```csharp
using NUnit.Framework;

namespace LOP.Tests
{
    public class FlappyServerCorrectionHandlerTests
    {
        [Test]
        public void 둘_다_안_멈췄으면_맞는다()
        {
            Assert.IsTrue(FlappyServerCorrectionHandler.StunMatches(
                predictedStun: 0f, predictedInvuln: 0f, snapStunEnd: 0, snapInvulnEnd: 0, tick: 10));
        }

        [Test]
        public void 서버는_멈췄는데_내가_안_멈췄으면_틀리다()
        {
            Assert.IsFalse(FlappyServerCorrectionHandler.StunMatches(
                predictedStun: 0f, predictedInvuln: 0f, snapStunEnd: 50, snapInvulnEnd: 0, tick: 10));
        }

        [Test]
        public void 내가_멈췄는데_서버는_안_멈췄으면_틀리다()
        {
            Assert.IsFalse(FlappyServerCorrectionHandler.StunMatches(
                predictedStun: 0.4f, predictedInvuln: 0f, snapStunEnd: 0, snapInvulnEnd: 0, tick: 10));
        }

        [Test]
        public void 무적_여부가_달라도_틀리다()
        {
            Assert.IsFalse(FlappyServerCorrectionHandler.StunMatches(
                predictedStun: 0f, predictedInvuln: 0f, snapStunEnd: 0, snapInvulnEnd: 30, tick: 10));
        }

        [Test]
        public void 종료틱이_이미_지났으면_안_멈춘_것이다()
        {
            //  같은 값이라도 틱이 지나면 뜻이 뒤집힌다 — 비교는 반드시 같은 시점 기준이어야 한다.
            Assert.IsTrue(FlappyServerCorrectionHandler.StunMatches(
                predictedStun: 0f, predictedInvuln: 0f, snapStunEnd: 50, snapInvulnEnd: 0, tick: 50));
        }
    }
}
```

- [ ] **Step 2: 빨강을 확인한다**

Run: `unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --filter FlappyServerCorrectionHandlerTests --async_tests true`
Expected: 컴파일 실패 — 타입이 없다.

- [ ] **Step 3: 핸들러를 만든다**

`LeagueOfPhysical-Client/Assets/Scripts/Netcode/FlappyServerCorrectionHandler.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// Flappy Race의 서버 보정 — 스턴이 서버 권위다.
    ///
    /// 남의 새까지 클라가 굴리게 되면서 "남이 벽에 부딪혀 멈췄나"도 클라가 예측하게 됐다.
    /// 그 판정이 서버와 갈리면 0.8초 얼음이 통째로 어긋나므로, 위치가 맞아도 되돌린다.
    /// </summary>
    public class FlappyServerCorrectionHandler : IServerCorrectionHandler
    {
        private readonly FlappyWorld world;       // 같은 게임 안이므로 구체를 직접 본다
        private readonly GameFramework.Runner.IRunner runner;

        public FlappyServerCorrectionHandler(FlappyWorld world, GameFramework.Runner.IRunner runner)
        {
            this.world = world;
            this.runner = runner;
        }

        //  비교는 반드시 같은 시점끼리 한다 — 앵커 틱에 "내가 그때 예측했던" 스턴 vs 서버가 그 틱에
        //  갖고 있던 스턴. 지금 살아있는 값과 비교하면 클라가 앞서 달리는 리드 구간 내내 시점이
        //  어긋나 보여, 스턴이 걸리거나 풀릴 때마다 불필요한 되돌리기가 난다.
        public bool Matches(long tick, EntitySnap snap)
        {
            //  앵커 틱 기록이 없으면(정상 경로엔 없는 엣지) 비교 불가 — 불일치로 단정하지 않고
            //  위치 판정에 맡긴다.
            if (!world.TryGetSavedStun(tick, snap.entityId, out var predicted))
            {
                return true;
            }
            return StunMatches(predicted.StunRemaining, predicted.InvulnRemaining,
                               snap.stunEndTick, snap.invulnEndTick, tick);
        }

        /// <summary>켜짐/꺼짐만 본다. 남은 시간의 미세한 차이는 다음 틱 시뮬이 알아서 좁힌다.</summary>
        public static bool StunMatches(float predictedStun, float predictedInvuln,
                                       long snapStunEnd, long snapInvulnEnd, long tick)
        {
            return (predictedStun > 0f) == (snapStunEnd > tick)
                && (predictedInvuln > 0f) == (snapInvulnEnd > tick);
        }

        public void ApplyAuthoritative(GameFramework.World.Entity entity, EntitySnap snap)
        {
            var stun = entity.Get<FlappyStun>();
            if (stun == null)
            {
                return;
            }
            //  끝나는 틱에서 남은 시간을 되계산한다. 불리언이었다면 여기서 전체 시간을 새로 채울
            //  수밖에 없어, 서버가 이미 절반쯤 지난 스턴을 처음부터 다시 시작하게 만든다.
            float interval = runner.tickUpdater.interval;
            stun.StunRemaining = RemainingSeconds(snap.stunEndTick, tick: snap.tick, interval);
            stun.InvulnRemaining = RemainingSeconds(snap.invulnEndTick, tick: snap.tick, interval);
        }

        private static float RemainingSeconds(long endTick, long tick, float interval)
        {
            long remainingTicks = endTick - tick;
            return remainingTicks > 0 ? remainingTicks * interval : 0f;
        }
    }
}
```

- [ ] **Step 4: 초록을 확인한다**

Run: 같은 명령. Expected: 5개 통과.

- [ ] **Step 5: DI를 갈아끼운다**

`FlappyRaceLifetimeScope.cs`에서 `NoServerCorrection` 등록과 그 위의 "알려진 한계" 주석 6줄을
지우고 이렇게 바꾼다:

```csharp
            //  스턴은 서버 권위다. 남의 새까지 클라가 굴리면서 "남이 부딪혔나"도 예측하게 됐고,
            //  그 판정이 갈리면 0.8초 얼음이 통째로 어긋난다.
            builder.Register<IServerCorrectionHandler, FlappyServerCorrectionHandler>(Lifetime.Singleton);
```

`FlappyServerCorrectionHandler`가 `FlappyWorld` 구체를 받으므로, 이 스코프가 `IWorld`를
`FlappyWorld`로 등록하고 있는지 확인한다. `Register<GameFramework.World.IWorld>(c => new FlappyWorld(...))`
형태라면 구체로도 꺼낼 수 있게 `.As<FlappyWorld>()`를 함께 붙이거나, 등록을
`Register<FlappyWorld>(...).As<GameFramework.World.IWorld>().AsSelf()`로 바꾼다.

- [ ] **Step 6: 전체 테스트 + 커밋**

Run: 클라·서버 EditMode 전체. Expected: 전부 통과.

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Netcode/FlappyServerCorrectionHandler.cs Assets/Scripts/Netcode/FlappyServerCorrectionHandler.cs.meta \
        Assets/Scripts/Game/FlappyRaceLifetimeScope.cs \
        Assets/Tests/Editor/FlappyServerCorrectionHandlerTests.cs Assets/Tests/Editor/FlappyServerCorrectionHandlerTests.cs.meta
git status --short
git commit -m "feat(flappy): 스턴을 서버 권위로 되돌린다

남의 새까지 클라가 굴리게 되면 '남이 부딪혔나'도 클라 예측이 된다. 그 판정이 서버와 갈리면
0.8초 얼음이 통째로 어긋나므로 위치가 맞아도 되돌린다. 덤으로 '내 새의 스턴이 보정되지 않는다'는
기존 알려진 한계가 함께 닫힌다."
```

---

### Task 4: 남의 새를 굴린다 (정책 교체)

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/EntitySync/CharactersPredictedSyncPolicy.cs` (+ `.meta`)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/Reconciler.cs` (주석만)
- Test: `LeagueOfPhysical-Client/Assets/Tests/EditMode/EntitySync/EntitySyncPolicyTests.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldTests.cs`

**Interfaces:**
- Produces: `CharactersPredictedSyncPolicy : IEntitySyncPolicy`

- [ ] **Step 1: 삭제됐던 정책을 되살린다**

이 파일은 커밋 `a2fb9d85`에서 지워졌다. 그때 내용 그대로 복원한다:

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git show a2fb9d85^:Assets/Scripts/EntitySync/CharactersPredictedSyncPolicy.cs \
  > Assets/Scripts/EntitySync/CharactersPredictedSyncPolicy.cs
git show a2fb9d85^:Assets/Scripts/EntitySync/CharactersPredictedSyncPolicy.cs.meta \
  > Assets/Scripts/EntitySync/CharactersPredictedSyncPolicy.cs.meta
```

복원된 내용(확인용):

```csharp
namespace LOP
{
    /// <summary>
    /// 캐릭터는 전부 예측하고 그 외는 보간한다(Flappy Race). 몸싸움처럼 서로 부딪히는 게 게임성인
    /// 경우, 남을 지연된 위치에 두면 "화면에 안 닿았는데 밀리는" 판정이 된다.
    /// </summary>
    public class CharactersPredictedSyncPolicy : IEntitySyncPolicy
    {
        public EntitySyncMode For(GameFramework.World.Entity entity)
        {
            return entity.Get<EntityKind>()?.Kind == EntityType.Character
                ? EntitySyncMode.Predicted
                : EntitySyncMode.Interpolated;
        }
    }
}
```

- [ ] **Step 2: 정책 테스트를 바꾸고 빨강을 확인한다**

`EntitySyncPolicyTests.cs`의 외삽 테스트 두 개(`내_캐릭터는_예측하고_남은_외삽한다`,
`내_id를_아직_모르면_전부_외삽이다`)를 지우고 이것으로 바꾼다:

```csharp
        [Test]
        public void 캐릭터는_내_것이든_남의_것이든_예측한다()
        {
            var policy = new CharactersPredictedSyncPolicy();

            Assert.AreEqual(EntitySyncMode.Predicted, policy.For(Character("me")));
            Assert.AreEqual(EntitySyncMode.Predicted, policy.For(Character("other")));
        }

        [Test]
        public void 캐릭터가_아니면_보간한다()
        {
            var policy = new CharactersPredictedSyncPolicy();

            Assert.AreEqual(EntitySyncMode.Interpolated, policy.For(Item("coin")));
        }
```

`Character(...)`/`Item(...)` 헬퍼가 이 파일에 이미 있으면 그대로 쓰고, 없으면 기존 테스트들이
엔티티를 만드는 방식을 그대로 베껴 쓴다.

Run: `unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --filter EntitySyncPolicyTests --async_tests true`
Expected: 컴파일 실패 또는 실패 — 아직 DI가 옛 정책을 쓴다면 테스트 자체는 통과할 수 있으니,
**정책 클래스가 없어서 컴파일이 실패하는 것**을 확인하는 것이 이 단계의 목적이다.

- [ ] **Step 3: DI를 갈아끼운다**

`FlappyRaceLifetimeScope.cs`에서 외삽 정책 등록과 그 위 주석을 지우고 바꾼다:

```csharp
            //  캐릭터는 전부 예측한다. 남을 외삽으로 그리면 게임 규칙 밖에서 움직여
            //  낙하 상한을 모르고 맵을 뚫는다 — 실측은 스펙 §2 참고.
            builder.Register<IEntitySyncPolicy, CharactersPredictedSyncPolicy>(Lifetime.Singleton);
```

- [ ] **Step 4: 재생 루프의 틀린 주석을 고친다**

`Reconciler.cs`의 재생 루프 위 주석에서 근거가 틀린 문장을 바꾼다.

지금:
```
            // world.Tick이 예측 대상 전부를 굴리지만, 입력을 넣는 건 내 엔티티뿐이다 — 남의 엔티티는
            // InputBuffer가 없어 자동으로 "안 누른 것"이 된다.
```

바꾼 뒤:
```
            // world.Tick이 예측 대상 전부를 굴리지만, 입력을 넣는 건 내 엔티티뿐이다 — 아래에서
            // 내 엔티티의 버퍼만 집어 Current를 세우므로, 남의 새는 Current가 null인 채로 굴러
            // "안 누른 것"이 된다. (남의 새도 InputBuffer는 갖고 있다 — CharacterCreator가 클·서
            // 양쪽에서 모든 캐릭터에 붙인다. "버퍼가 없어서"가 아니다.)
```

- [ ] **Step 5: 입력 없는 새는 안 눌린 채로 굴러간다는 테스트**

`LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldTests.cs`에 더한다. 이 설계 전체가 이 성질에
기대고 있으므로 고정한다:

```csharp
        [Test]
        public void 입력이_안_들어온_새는_안_누른_것으로_굴러간다()
        {
            //  남의 새는 클라에서 아무도 InputBuffer.Current를 세우지 않는다. 그 상태로 굴렸을 때
            //  마지막 입력이 남아 계속 재적용되면 새가 로켓처럼 솟는다 — 그게 안 일어나는지 본다.
            var registry = new EntityRegistry();
            var bird = Bird("bird-1", Vector3.zero, simulated: true);
            registry.Add(bird);
            var world = World(registry, new NoopMotionBridge());

            //  한 번 날갯짓시킨 뒤, 그 다음부터는 아무도 Current를 안 세운다.
            bird.Get<InputBuffer>().Current = new InputCommand { Jump = true };
            world.Tick(1, 0.02f);
            float afterFlap = VelocityOf(bird).y;

            for (long t = 2; t <= 5; t++)
            {
                world.Tick(t, 0.02f);
            }

            //  중력만 먹었어야 한다 — 날갯짓이 되풀이됐다면 y속도가 계속 FlapImpulse로 덮인다.
            Assert.Less(VelocityOf(bird).y, afterFlap);
        }
```

- [ ] **Step 6: 초록을 확인한다**

Run: 클라·서버 EditMode 전체.
Expected: 전부 통과. **여기서 실패하는 테스트가 있으면 그 테스트가 외삽을 전제하고 있었다는
뜻이므로, 지우지 말고 새 동작에 맞춰 고친 뒤 무엇을 왜 고쳤는지 보고한다.**

- [ ] **Step 7: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/EntitySync/CharactersPredictedSyncPolicy.cs Assets/Scripts/EntitySync/CharactersPredictedSyncPolicy.cs.meta \
        Assets/Scripts/Game/FlappyRaceLifetimeScope.cs Assets/Scripts/Netcode/Reconciler.cs \
        Assets/Tests/EditMode/EntitySync/EntitySyncPolicyTests.cs
git status --short
git commit -m "feat(flappy): 남의 새를 외삽 대신 진짜 게임 코드로 굴린다

외삽은 게임 규칙 밖의 산수라 낙하 상한을 모르고 맵을 뚫고 몸싸움이 없다. 실측(스펙 §2):
날갯짓 없는 구간에서 시뮬은 정확히 0, 외삽은 항상 0.126m 틀리고 빠르게 떨어질 땐 1.13m까지
벌어진다. 날갯짓 낀 구간은 두 방식이 사실상 같다 — 즉 기록에 남은 '3m 텔레포트'는 시뮬 탓이
아니었다.

남의 입력은 오지 않으므로 InputBuffer.Current가 null인 채로 굴러 '안 눌렀다'가 저절로 성립한다."
```

---

### Task 5: 라이브 검증 (사람)

**Files:** 없음 — 사람이 눈으로 본다.

이 슬라이스는 **EditMode로 증명할 수 없는 것**이 본체다. 스펙 §5가 지목한 대로, 예전 실패의
유력한 원인이 오차 크기가 아니라 **오차가 화면에 나타나는 방식**이었다.

- [ ] **Step 1: 2인 리그를 세운다**

`[[local-two-client-test-rig]]` 절차를 따른다. 서버·클라 환경을 `local`로 두고, 서버
`ConfigureRoomComponent.cs`의 명단을 2인으로 하고, MPPM 클론의 uuid를 서버 콘솔의
`명단에 없는 참가자: <uuid>`에서 복사해 넣는다. **클론을 껐다 켜면 uuid가 바뀐다.**

- [ ] **Step 2: 이것들을 본다**

1. **남의 새가 날갯짓할 때 튀는가** — 예전 "3m 텔레포트"의 재발 여부. 이 슬라이스의 핵심 질문이다.
2. **남의 새가 벽에 막히는가** — 외삽 시절엔 뚫었다.
3. **남의 새가 빠르게 떨어질 때 서버와 같은 속도로 떨어지는가** — `MaxFallSpeed` 19% 오차의 소멸.
4. **남의 스턴이 정확한가** — 클라가 예측한 스턴이 서버와 갈릴 때 되돌아오는가.
5. **프레임이 견디는가** — 되감기가 이제 9틱 × 인원수를 굴린다.

- [ ] **Step 3: 판정**

- 1번이 재발하면 → **스무딩 상수를 남에게 따로 준다**(스펙 §9). 남은 조작감이 없으니 더 느리게
  흡수해도 된다. 그것으로도 안 되면 리드 축소를 검토한다.
- 2·3·4가 안 되면 → 배선이 덜 된 것이다. 어느 경로가 안 탔는지 로그로 확인한다.
- 5가 안 되면 → 되감기 좁히기(스펙 §9)를 검토한다. **실측 전에는 하지 않는다.**

---

### Task 6: 외삽 경로를 걷어낸다 — Task 5 통과 후에만

**Files:**
- Delete: `Assets/Scripts/EntitySync/OwnerPredictedRemotesExtrapolatedSyncPolicy.cs` (+ `.meta`)
- Delete: `Assets/Scripts/Netcode/ExtrapolatedEntityInterpolator.cs` (+ `.meta`)
- Delete: `Assets/Scripts/EntitySync/IExtrapolationAcceleration.cs`,
  `FlappyExtrapolationAcceleration.cs`, `ZeroExtrapolationAcceleration.cs` (+ `.meta`)
- Delete: `Assets/Tests/EditMode/EntitySync/ExtrapolationAccelerationTests.cs` (+ `.meta`)
- Modify: `Assets/Scripts/EntitySync/EntitySyncMode.cs` (`Extrapolated` 제거)
- Modify: `Assets/Scripts/Entity/EntityBinder.cs`, `Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs`,
  `Assets/Scripts/Game/FlapWangLifetimeScope.cs`

> **이 태스크는 Task 5가 통과한 뒤에만 한다.** 되돌릴 가능성이 살아 있는 동안 지우면 복구가 비싸다.
> Task 5가 실패해 외삽으로 돌아가야 하면 이 태스크는 통째로 버린다.

- [ ] **Step 1: 정말 아무도 안 쓰는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP
grep -rn "Extrapolated\|ExtrapolationAcceleration\|ExtrapolatedEntityInterpolator" \
  LeagueOfPhysical-Client/Assets/Scripts LeagueOfPhysical-Server/Assets/Scripts LeagueOfPhysical-Shared/Runtime | grep -v meta
```
Expected: 지울 파일들 자신 + `EntityBinder`/`GameEntityMessageHandler`/`FlapWangLifetimeScope`의
분기만 나와야 한다. 다른 게 나오면 멈추고 보고한다.

- [ ] **Step 2: 지운다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git rm Assets/Scripts/EntitySync/OwnerPredictedRemotesExtrapolatedSyncPolicy.cs{,.meta} \
       Assets/Scripts/EntitySync/IExtrapolationAcceleration.cs{,.meta} \
       Assets/Scripts/EntitySync/FlappyExtrapolationAcceleration.cs{,.meta} \
       Assets/Scripts/EntitySync/ZeroExtrapolationAcceleration.cs{,.meta} \
       Assets/Scripts/Netcode/ExtrapolatedEntityInterpolator.cs{,.meta} \
       Assets/Tests/EditMode/EntitySync/ExtrapolationAccelerationTests.cs{,.meta}
```

- [ ] **Step 3: 남은 참조를 정리한다**

- `EntitySyncMode.cs` — `Extrapolated` 멤버와 그 XML 주석을 지운다.
- `EntityBinder.cs` — `IExtrapolationAcceleration` 필드·생성자 인자·`case EntitySyncMode.Extrapolated:`
  블록·`extrapolatedInterpolator` 변수와 그 `stunAppearance` 연결을 지운다.
- `GameEntityMessageHandler.cs` — `ExtrapolatedEntityInterpolator` 분기를 지운다
  (`SnapshotEntityInterpolator` 분기와 `else → reconciler` 는 남긴다).
- `FlapWangLifetimeScope.cs` — `IExtrapolationAcceleration` 등록 줄을 지운다.

- [ ] **Step 4: `SnapshotExtrapolation`은 남긴다**

`GameFramework.Netcode.SnapshotExtrapolation`(순수 커널)과 그 테스트는 **지우지 않는다.**
스펙 §6.6의 판단이다 — 리드 축소를 검토할 때 다시 필요할 수 있고 테스트가 딸려 있어 비용이 없다.
그 파일 클래스 주석에 한 줄 남긴다:

```
/// 현재 사용처 없음 — 남의 새를 외삽으로 그리던 경로가 2026-08-27에 시뮬로 바뀌며 사라졌다.
/// 리드 축소(스펙 §9)를 검토할 때 다시 쓸 수 있어 남겨 둔다.
```

- [ ] **Step 5: 전체 테스트 + 커밋**

Run: 클라·서버 EditMode 전체. Expected: 전부 통과.

```bash
git status --short
git commit -m "chore(flappy): 외삽 경로를 걷어낸다

남의 새를 시뮬로 굴리게 되면서 마지막 사용처가 사라졌다. 순수 커널
(GameFramework.Netcode.SnapshotExtrapolation)은 리드 축소 검토에 다시 쓸 수 있어 남긴다."
```

---

## 자체 점검

**스펙 커버리지**

| 스펙 절 | 태스크 |
|---|---|
| §3 결정 — 남을 `Simulated`로 | Task 4 |
| §6.1 정책 교체 | Task 4 |
| §6.2 입력은 손댈 것 없음 + 주석 정정 | Task 4 (Step 4·5) |
| §6.3 스냅 도착 경로 | Task 4 (정책 교체의 자동 결과 — 별도 코드 없음) |
| §6.4 스턴 권위 | Task 1·2·3 (+ 이 계획이 채운 와이어 구멍) |
| §6.5 화면 스무딩 | Task 5 (배선은 이미 있음 — 상수 조정 여부는 실측 후) |
| §6.6 사라지는 것 | Task 6 |
| §8 테스트 | 각 태스크에 분산 |
| §9 열린 질문 | Task 5의 판정 단계에서 다룬다 |

**타입 일관성**

- `TryGetSavedStun(long, string, out FlappySavedState)` — Task 2에서 정의, Task 3에서 사용. 일치.
- `EntitySnap.stunEndTick` / `invulnEndTick` (long) — Task 1에서 정의, Task 3에서 사용. 일치.
- `StunVisuals.Of(EntitySnap, long)` — Task 1에서 시그니처 변경, 호출부 3곳 같은 태스크에서 정리.
- `FlappyServerCorrectionHandler.StunMatches(float, float, long, long, long)` — Task 3 안에서만 쓰임.
