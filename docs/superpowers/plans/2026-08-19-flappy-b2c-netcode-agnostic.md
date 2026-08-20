# 슬라이스 B2-c — 넷코드를 게임 비종속으로 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 되감기(`Reconciler`)가 스킬·상태이상을 모르게 만든다 — 상태 저장·복원은 월드가 하고, 재생은 입력을 놓고 `world.Tick`만 부른다.

**Architecture:** GGPO의 `save_game_state`/`load_game_state`를 `IWorld.SaveState`/`LoadState`로 옮긴다. `WorldBase`가 위치·속도를 담고 각 게임 월드가 자기 상태를 얹는다(Unreal `FSavedMove_Character` 서브클래싱). 어빌리티 발동은 넷코드가 아니라 `LOPWorld.Mutation`이 입력 버퍼에서 읽어 수행한다(Quantum `PollInput`, Unity NetCode `ICommandData`). 서버 스냅의 상태이상만은 클라 전용 타입에 묶여 있어 클라 쪽 게임별 훅으로 남긴다(Unreal `ServerMoveHandleClientError`).

**Tech Stack:** Unity 6000.3.16f1, C#, VContainer, NUnit(EditMode). 4개 레포: GameFramework / LeagueOfPhysical-Shared / LeagueOfPhysical-Client / LeagueOfPhysical-Server.

**Spec:** `docs/superpowers/specs/2026-08-17-flappy-race-gameplay-b2-design.md` §4

## Global Constraints

- **주석**: 코드로 자명한 것은 쓰지 않는다. 비자명한 *왜*만 일상어 한국어로 짧게. 전문용어를 설명 없이 던지지 않는다.
- **명명**: 업계 표준 용어를 따른다. 이 슬라이스의 표준 대응은 spec §4의 표에 있다 — 임의 명명 금지.
- **`.meta` 파일**: 새 스크립트를 만들면 Unity가 생성한 `.meta`를 반드시 함께 커밋한다.
- **git**: main 직접 커밋 금지. 이 슬라이스는 브랜치 `feature/flappy-b2c-netcode-agnostic`에서 진행한다(이미 존재, spec 커밋 2개 포함).
- **worktree 금지**: 유니티 프로젝트는 git worktree를 쓰지 않는다(에셋 임포트·Library 충돌). 일반 브랜치로 작업한다.
- **레포별 브랜치**: GameFramework / LOP-Shared / LOP-Server 도 각자 같은 이름의 피처 브랜치에서 작업한다.
- **컴파일 확인 전 필수 점검**: 에디터가 Play 중이면 컴파일이 진행되지 않는다. `unity cmd editor_play` 상태(또는 `EditorApplication.isPlaying`)를 먼저 확인한다.
- **모든 태스크 끝에서 컴파일이 통과해야 한다.** 타입 이동은 "추가 후 삭제"로 나누지 말고 한 태스크 안에서 원자적으로 한다(같은 네임스페이스에 사본이 둘이면 참조가 모호해진다).
- **완료 기준에 FlapWang 회귀 없음이 포함된다** — 이 슬라이스는 지금 잘 도는 예측·롤백을 건드린다.

## 저장소별 경로

| 레포 | 로컬 경로 |
|---|---|
| GameFramework | `/Users/insoobae/workspace/LOP/GameFramework` |
| LOP-Shared | `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared` |
| LOP-Client | `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client` |
| LOP-Server | `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server` |

## File Structure

**GameFramework**
- Modify `Runtime/Scripts/Netcode/SequenceBuffer.cs` — `LatestTick` 노출
- Modify `Runtime/Scripts/World/IWorld.cs` — 저장/복원 5개 멤버
- Modify `Runtime/Scripts/World/WorldBase.cs` — 위치·속도 저장/복원 + 게임 훅
- Create `Tests/World/WorldStateSaveLoadTests.cs`

**LOP-Shared**
- Modify `Runtime/Scripts/Game/LOPWorld.cs` — 게임 상태 저장/복원 + 입력 발동
- Rename `Runtime/Scripts/Game/PredictedAbilityState.cs` → `LOPSavedState.cs`
- Create `Runtime/Scripts/Game/AbilityActivator.cs` (클·서 사본 통합)
- Create `Tests/EditMode/LOPWorldSaveLoadTests.cs`
- Create `Tests/EditMode/LOPWorldInputActivationTests.cs`

**LOP-Client**
- Delete `Assets/Scripts/Game/AbilityActivator.cs`
- Create `Assets/Scripts/Netcode/IServerCorrectionHandler.cs`
- Create `Assets/Scripts/Netcode/LOPServerCorrectionHandler.cs`
- Create `Assets/Scripts/Netcode/NoServerCorrection.cs`
- Modify `Assets/Scripts/Netcode/Reconciler.cs` — 슬림화
- Modify `Assets/Scripts/Game/TickSystems/LocalSnapshotSystem.cs` — `world.SaveState`
- Modify `Assets/Scripts/Game/PlayerInputManager.cs` — 발동 호출 제거
- Modify `Assets/Scripts/Game/GameplayInstaller.cs` — 등록 정리
- Modify `Assets/Scripts/Game/FlapWangLifetimeScope.cs` / `FlappyRaceLifetimeScope.cs` — 훅 등록
- Modify `Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs` — 히스토리 조회 경로

**LOP-Server**
- Delete `Assets/Scripts/Game/AbilityActivator.cs`
- Modify `Assets/Scripts/Game/TickSystems/ServerInputSystem.cs` — 발동 호출 제거
- Modify DI 등록(`AbilityActivator` 팩토리)

**문서**
- Modify `docs/netcode-redesign.md` §6.5 (클라·서버 양쪽 사본)
- Modify `docs/world-core-connection-architecture.md` (클라·서버 양쪽 사본)

---

### Task 1: GameFramework — 월드에 저장·복원을 붙인다

GGPO `save_game_state`/`load_game_state`에 대응하는 자리를 만든다. 이 태스크는 **추가만** 한다 — 기존 호출자가 없으므로 아무 동작도 바뀌지 않는다.

**Files:**
- Modify: `GameFramework/Runtime/Scripts/Netcode/SequenceBuffer.cs`
- Modify: `GameFramework/Runtime/Scripts/World/IWorld.cs`
- Modify: `GameFramework/Runtime/Scripts/World/WorldBase.cs`
- Test: `GameFramework/Tests/World/WorldStateSaveLoadTests.cs`

**Interfaces:**
- Produces: `IWorld.SaveState(long)`, `IWorld.LoadState(long) → bool`, `IWorld.FirstSavedTick`, `IWorld.LatestSavedTick`, `IWorld.TryGetSavedMotion(long, string, out GameFramework.Netcode.EntitySnapshot)`, `WorldBase`의 `protected virtual void SaveGameState(long)` / `protected virtual bool LoadGameState(long)`
- Consumes: 기존 `EntityRegistry`, `Simulated`, `Transform`, `Velocity`, `Netcode.SequenceBuffer<T>`, `Netcode.EntitySnapshot`

- [ ] **Step 1: 브랜치 생성**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git checkout main && git pull --ff-only
git checkout -b feature/flappy-b2c-netcode-agnostic
```

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`GameFramework/Tests/World/WorldStateSaveLoadTests.cs` 새 파일:

```csharp
using System.Numerics;
using GameFramework.World;
using NUnit.Framework;

namespace GameFramework.Tests.World
{
    public class WorldStateSaveLoadTests
    {
        // 게임 훅이 불리는지 세는 최소 월드. WorldBase는 추상이라 테스트용 구체가 필요하다.
        private class TestWorld : WorldBase
        {
            public int SaveGameCalls;
            public int LoadGameCalls;
            public bool LoadGameResult = true;

            public TestWorld(EntityRegistry registry)
                : base(registry, new WorldEventBuffer()) { }

            protected override void SaveGameState(long tick) => SaveGameCalls++;
            protected override bool LoadGameState(long tick)
            {
                LoadGameCalls++;
                return LoadGameResult;
            }
        }

        private static Entity MakeSimulated(string id, float x)
        {
            var e = new Entity(id);
            e.Add(new Simulated());
            e.Add(new GameFramework.World.Transform { Position = new Vector3(x, 0f, 0f) });
            e.Add(new Velocity { Linear = new Vector3(0f, x, 0f) });
            return e;
        }

        [Test]
        public void LoadState_되돌리면_저장한_위치와_속도로_돌아온다()
        {
            var registry = new EntityRegistry();
            var entity = MakeSimulated("a", 1f);
            registry.Add(entity);
            var world = new TestWorld(registry);

            world.SaveState(10);

            entity.Get<GameFramework.World.Transform>().Position = new Vector3(99f, 0f, 0f);
            entity.Get<Velocity>().Linear = new Vector3(0f, 99f, 0f);

            Assert.IsTrue(world.LoadState(10));
            Assert.AreEqual(1f, entity.Get<GameFramework.World.Transform>().Position.X);
            Assert.AreEqual(1f, entity.Get<Velocity>().Linear.Y);
        }

        [Test]
        public void SaveState_Simulated가_없는_엔티티는_담지_않는다()
        {
            var registry = new EntityRegistry();
            var plain = new Entity("b");
            plain.Add(new GameFramework.World.Transform { Position = new Vector3(5f, 0f, 0f) });
            plain.Add(new Velocity());
            registry.Add(plain);
            var world = new TestWorld(registry);

            world.SaveState(10);

            Assert.IsFalse(world.TryGetSavedMotion(10, "b", out _));
        }

        [Test]
        public void LoadState_기록없는_틱이면_false이고_게임훅도_안_부른다()
        {
            var world = new TestWorld(new EntityRegistry());

            Assert.IsFalse(world.LoadState(7));
            Assert.AreEqual(0, world.LoadGameCalls);
        }

        [Test]
        public void LoadState_게임훅이_false면_전체도_false다()
        {
            var registry = new EntityRegistry();
            registry.Add(MakeSimulated("a", 1f));
            var world = new TestWorld(registry) { LoadGameResult = false };

            world.SaveState(10);

            Assert.IsFalse(world.LoadState(10));
        }

        [Test]
        public void SaveState_는_게임훅을_함께_부른다()
        {
            var registry = new EntityRegistry();
            registry.Add(MakeSimulated("a", 1f));
            var world = new TestWorld(registry);

            world.SaveState(10);

            Assert.AreEqual(1, world.SaveGameCalls);
        }

        [Test]
        public void FirstSavedTick_과_LatestSavedTick_이_기록범위를_알려준다()
        {
            var registry = new EntityRegistry();
            registry.Add(MakeSimulated("a", 1f));
            var world = new TestWorld(registry);

            Assert.IsNull(world.FirstSavedTick);

            world.SaveState(10);
            world.SaveState(11);

            Assert.AreEqual(10, world.FirstSavedTick);
            Assert.AreEqual(11, world.LatestSavedTick);
        }
    }
}
```

> `Transform`은 `UnityEngine.Transform`과 이름이 겹치지 않도록 테스트에서도 풀 네임스페이스로 쓴다. 테스트 어셈블리는 `noEngineReferences`가 아니므로 이 습관을 지킨다.

- [ ] **Step 3: 테스트가 실패하는 것을 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
export PATH="$HOME/.unity/bin:$PATH"
unity cmd recompile
```

컴파일 에러(`SaveState`/`LoadState`/`TryGetSavedMotion` 없음)가 나야 정상이다. 에러가 없다면 테스트 파일이 어셈블리에 포함되지 않은 것이니 경로를 다시 확인한다.

- [ ] **Step 4: `SequenceBuffer`에 최신 틱을 노출한다**

`GameFramework/Runtime/Scripts/Netcode/SequenceBuffer.cs`의 `FirstRecordedTick` 프로퍼티 **바로 아래**에 추가:

```csharp
        /// <summary>가장 최근에 기록한 틱. 한 번도 기록하지 않았으면 null.</summary>
        public long? LatestTick => _hasAny ? _latestTick : (long?)null;
```

- [ ] **Step 5: `IWorld`에 저장·복원을 추가한다**

`GameFramework/Runtime/Scripts/World/IWorld.cs` 전체를 교체:

```csharp
namespace GameFramework.World
{
    public interface IWorld
    {
        EntityRegistry EntityRegistry { get; }
        WorldEventBuffer EventBuffer { get; }
        void Tick(long tick, float deltaTime);

        /// <summary>
        /// 이번 틱 시뮬 상태를 보관한다. 되돌릴 수 있는 건 여기 담긴 것뿐이다.
        /// 무엇을 담을지는 각 게임의 월드가 정한다 — 부르는 쪽(넷코드)은 내용을 모른다.
        /// GGPO <c>save_game_state</c> 대응.
        /// </summary>
        void SaveState(long tick);

        /// <summary>
        /// 그 틱 상태로 되돌린다. 기록이 없으면 아무것도 바꾸지 않고 false.
        /// GGPO <c>load_game_state</c> 대응.
        /// </summary>
        bool LoadState(long tick);

        /// <summary>
        /// 보관을 시작한 가장 이른 틱. 조회 실패가 "아직 살지 않은 틱"인지 "밀려난 틱"인지 가른다 —
        /// 앞은 손대면 안 되고 뒤는 따라잡아야 한다.
        /// </summary>
        long? FirstSavedTick { get; }

        /// <summary>가장 최근 보관 틱(진단용).</summary>
        long? LatestSavedTick { get; }

        /// <summary>
        /// 보관된 위치·속도를 읽는다. 예측이 서버와 얼마나 어긋났는지 재는 데 쓴다 —
        /// 위치는 게임 종류와 무관한 값이라 이걸 노출해도 부르는 쪽이 게임을 알게 되지 않는다.
        /// </summary>
        bool TryGetSavedMotion(long tick, string entityId, out Netcode.EntitySnapshot motion);
    }
}
```

- [ ] **Step 6: `WorldBase`가 위치·속도를 담는다**

`GameFramework/Runtime/Scripts/World/WorldBase.cs` 전체를 교체:

```csharp
using System.Collections.Generic;

namespace GameFramework.World
{
    public abstract class WorldBase : IWorld
    {
        /// <summary>
        /// 되감기 보관 길이. 128틱 ≈ 2.5초 — 이보다 오래된 서버 스냅은 재생 대신 텔레포트로 처리한다.
        /// 게임이 자기 상태를 담을 때도 <b>같은 길이</b>를 써야 한다. 한쪽만 짧으면 되돌리기가 반쪽이 되는데,
        /// 컴파일도 테스트도 그걸 잡아주지 못한다.
        /// </summary>
        protected const int SaveCapacity = 128;

        // 틱 → (엔티티 id → 위치·회전·속도).
        private readonly Netcode.SequenceBuffer<Dictionary<string, Netcode.EntitySnapshot>> _motionFrames
            = new Netcode.SequenceBuffer<Dictionary<string, Netcode.EntitySnapshot>>(SaveCapacity);

        public EntityRegistry EntityRegistry { get; }
        public WorldEventBuffer EventBuffer { get; }

        protected WorldBase(EntityRegistry entityRegistry, WorldEventBuffer eventBuffer)
        {
            EntityRegistry = entityRegistry;
            EventBuffer = eventBuffer;
        }

        public void Tick(long tick, float deltaTime)
        {
            Collection(tick, deltaTime);
            Mutation(tick, deltaTime);
            Detection(tick, deltaTime);
        }

        public long? FirstSavedTick => _motionFrames.FirstRecordedTick;

        public long? LatestSavedTick => _motionFrames.LatestTick;

        public void SaveState(long tick)
        {
            var frame = new Dictionary<string, Netcode.EntitySnapshot>();
            foreach (var entity in EntityRegistry.All)
            {
                if (!entity.Has<Simulated>())
                {
                    continue;   // 시뮬하지 않는 엔티티는 되돌릴 것도 없다(보간으로 따라옴)
                }
                var transform = entity.Get<Transform>();
                var velocity = entity.Get<Velocity>();
                if (transform == null || velocity == null)
                {
                    continue;
                }
                frame[entity.Id] = new Netcode.EntitySnapshot(
                    tick, transform.Position, transform.Rotation, velocity.Linear);
            }
            _motionFrames.Record(tick, frame);

            SaveGameState(tick);
        }

        public bool LoadState(long tick)
        {
            if (!_motionFrames.TryGet(tick, out var frame))
            {
                return false;
            }

            foreach (var pair in frame)
            {
                var entity = EntityRegistry.Get(pair.Key);
                if (entity == null)
                {
                    continue;   // 그 사이 사라진 엔티티 — 되돌릴 대상이 없다
                }
                var transform = entity.Get<Transform>();
                var velocity = entity.Get<Velocity>();
                if (transform == null || velocity == null)
                {
                    continue;
                }
                transform.Position = pair.Value.Position;
                transform.Rotation = pair.Value.Rotation;
                velocity.Linear = pair.Value.Velocity;
            }

            return LoadGameState(tick);
        }

        public bool TryGetSavedMotion(long tick, string entityId, out Netcode.EntitySnapshot motion)
        {
            motion = default;
            return _motionFrames.TryGet(tick, out var frame) && frame.TryGetValue(entityId, out motion);
        }

        /// <summary>
        /// 게임이 자기 상태를 얹는 자리. 베이스는 위치·속도만 담으므로, 스킬·상태이상처럼
        /// 그 게임에만 있는 것은 여기서 담는다. Unreal <c>FSavedMove_Character</c> 서브클래싱과 같은 자리다.
        /// </summary>
        protected virtual void SaveGameState(long tick) { }

        /// <summary>게임이 얹은 상태를 되돌린다. 그 틱 기록이 없으면 false.</summary>
        protected virtual bool LoadGameState(long tick) => true;

        // Generation 페이즈 (world-core-connection-architecture.md).
        protected virtual void Collection(long tick, float deltaTime) { }
        protected virtual void Mutation(long tick, float deltaTime) { }
        protected virtual void Detection(long tick, float deltaTime) { }
    }
}
```

- [ ] **Step 7: 테스트를 돌린다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
export PATH="$HOME/.unity/bin:$PATH"
unity cmd recompile && unity cmd run_tests --mode EditMode --filter GameFramework.Tests.World.WorldStateSaveLoadTests
```

`unity cmd`에 테스트 실행 명령이 없으면 `unity cmd` 목록에서 test 관련 명령 이름을 확인해 쓴다(`unity cmd | grep -i test`). 전부 통과해야 한다.

- [ ] **Step 8: 일부러 깨뜨려 테스트가 진짜 도는지 확인한다**

`WorldBase.LoadState`의 `transform.Position = pair.Value.Position;`를 잠시 주석 처리하고 테스트를 돌려 **실패하는지** 본다. 실패를 확인했으면 되돌린다.

- [ ] **Step 9: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git add Runtime/Scripts/Netcode/SequenceBuffer.cs Runtime/Scripts/World/IWorld.cs Runtime/Scripts/World/WorldBase.cs Tests/World/WorldStateSaveLoadTests.cs Tests/World/WorldStateSaveLoadTests.cs.meta
git commit -m "$(cat <<'EOF'
feat(world): 월드가 자기 상태를 저장·복원하게 한다

되감기가 무엇을 저장할지 알 필요가 없어야 한다. GGPO의 save_game_state/
load_game_state처럼 월드에게 "네 상태 담아둬 / 그 틱으로 돌아가"만 시킨다.

베이스는 위치·속도만 담고, 게임에만 있는 것(스킬·상태이상)은 각 게임 월드가
훅으로 얹는다 — Unreal FSavedMove_Character를 게임이 서브클래싱하는 것과 같다.

TryGetSavedMotion은 위치를 노출하지만 위치는 게임 종류와 무관한 값이라,
부르는 쪽이 게임을 알게 되지는 않는다. 예측 오차를 재려면 필요하다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: LOP-Shared — `LOPWorld`가 게임 상태를 담는다

지금 넷코드가 들고 있던 스킬·상태이상 사진첩을 월드 안으로 옮긴다. 이 태스크도 **추가만** 한다 — 클라의 기존 히스토리는 아직 그대로라 동작이 바뀌지 않는다.

**Files:**
- Rename: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/PredictedAbilityState.cs` → `LOPSavedState.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/LOPWorld.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/LOPWorldSaveLoadTests.cs`

**Interfaces:**
- Consumes: Task 1의 `WorldBase.SaveGameState`/`LoadGameState`
- Produces: `LOPSavedState`(구 `PredictedAbilityState` — `Capture`/`RestoreTo` 시그니처 동일), `LOPWorld.TryGetSavedStatusEffects(long tick, string entityId, out List<ActiveEffect> effects)`

- [ ] **Step 1: 브랜치 생성**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git checkout main && git pull --ff-only
git checkout -b feature/flappy-b2c-netcode-agnostic
```

- [ ] **Step 2: 기존 참조를 먼저 센다**

```bash
cd /Users/insoobae/workspace/LOP
grep -rn "PredictedAbilityState" --include=*.cs GameFramework LeagueOfPhysical-Shared LeagueOfPhysical-Client LeagueOfPhysical-Server | grep -v "/Library/"
```

여기서 나온 목록이 Step 3의 rename 대상 전부다. 클라 쪽 3곳(`LocalSnapshotSystem`, `Reconciler`, `GameplayInstaller`)은 **Task 6에서 지워질 것**이므로, 지금은 이름만 바꿔 컴파일을 통과시킨다.

- [ ] **Step 3: 이름을 바꾼다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git mv Runtime/Scripts/Game/PredictedAbilityState.cs Runtime/Scripts/Game/LOPSavedState.cs
git mv Runtime/Scripts/Game/PredictedAbilityState.cs.meta Runtime/Scripts/Game/LOPSavedState.cs.meta
```

`LOPSavedState.cs` 안의 클래스 이름과 문서 주석을 바꾼다:

```csharp
    /// <summary>
    /// 되감기용으로 남기는 LOP 고유 상태의 한 틱 사진(깊은 복사) — 어빌리티·상태이상·스탯·마나.
    /// 위치·속도는 <see cref="GameFramework.World.WorldBase"/>가 담으므로 여기엔 없다.
    /// Unreal <c>FSavedMove_Character</c>를 게임이 서브클래싱해 자기 데이터를 얹는 것과 같은 자리.
    /// </summary>
    public sealed class LOPSavedState
```

`Capture`의 반환 타입과 `new PredictedAbilityState()`도 함께 바꾼다. 그다음 Step 2 목록의 나머지 파일에서 타입 이름을 치환한다:

```bash
cd /Users/insoobae/workspace/LOP
for f in $(grep -rln "PredictedAbilityState" --include=*.cs LeagueOfPhysical-Client LeagueOfPhysical-Server | grep -v "/Library/"); do
  sed -i '' 's/PredictedAbilityState/LOPSavedState/g' "$f"
done
grep -rn "PredictedAbilityState" --include=*.cs . | grep -v "/Library/" | grep -v "/.git/"
```

마지막 grep은 **아무것도 출력하지 않아야** 한다.

- [ ] **Step 4: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/LOPWorldSaveLoadTests.cs` 새 파일:

```csharp
using System.Numerics;
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    // 월드가 자기 게임 상태(마나·상태이상)를 담고 되돌리는지. 위치·속도는 WorldBase 몫이라 여기선 안 본다.
    public class LOPWorldSaveLoadTests
    {
        private static Entity MakeEntity(string id)
        {
            var e = new Entity(id);
            e.Add(new Simulated());
            e.Add(new GameFramework.World.Transform());
            e.Add(new Velocity());
            e.Add(new Abilities());
            e.Add(new StatusEffects());
            e.Add(new Stats());
            e.Add(new Mana(100));
            return e;
        }

        private static LOPWorld MakeWorld(EntityRegistry registry)
        {
            return new LOPWorld(
                registry,
                new WorldEventBuffer(),
                new MovementSystem(new StatsSystem(), new MotionContributionSystem()),
                new AbilitySystem(new ManaSystem()),
                new StatusEffectSystem(new StatsSystem()),
                new AbilityEffectExecutor(null),
                new KinematicMoveSystem(new FakeQuery(), ~0),
                new SpyBridge());
        }

        [Test]
        public void LoadState_마나를_저장한_시점으로_되돌린다()
        {
            var registry = new EntityRegistry();
            var entity = MakeEntity("a");
            registry.Add(entity);
            var world = MakeWorld(registry);

            world.SaveState(10);
            entity.Get<Mana>().Current = 5;

            Assert.IsTrue(world.LoadState(10));
            Assert.AreEqual(100, entity.Get<Mana>().Current);
        }

        [Test]
        public void LoadState_기록없는_틱이면_false다()
        {
            var registry = new EntityRegistry();
            registry.Add(MakeEntity("a"));
            var world = MakeWorld(registry);

            Assert.IsFalse(world.LoadState(7));
        }

        [Test]
        public void TryGetSavedStatusEffects_저장시점_목록을_돌려준다()
        {
            var registry = new EntityRegistry();
            var entity = MakeEntity("a");
            entity.Get<StatusEffects>().Effects.Add(new ActiveEffect(100, 20, 1, "src", "srcId"));
            registry.Add(entity);
            var world = MakeWorld(registry);

            world.SaveState(10);
            entity.Get<StatusEffects>().Effects.Clear();

            Assert.IsTrue(world.TryGetSavedStatusEffects(10, "a", out var effects));
            Assert.AreEqual(1, effects.Count);
        }
    }
}
```

`MakeWorld`와 테스트 더블은 **이미 있는 것을 그대로 쓴다** — `LeagueOfPhysical-Shared/Tests/EditMode/LOPWorldTests.cs`의 `FakeQuery`·`SpyBridge`를 복사해 넣고, 조립은 그 파일과 같은 형태로 한다(실측한 시그니처):

```csharp
        private class FakeQuery : GameFramework.Physics.ICollisionQuery
        {
            public GameFramework.Physics.CollisionHit CapsuleCast(UnityEngine.Vector3 p1, UnityEngine.Vector3 p2,
                float radius, UnityEngine.Vector3 dir, float dist, int mask) => GameFramework.Physics.CollisionHit.None;
        }

        private class SpyBridge : GameFramework.World.IMotionBridge
        {
            public void SyncTransforms() { }
            public void Depenetrate(GameFramework.World.Entity e) { }
            public void Separate(GameFramework.World.Entity e) { }
            public void PushMotion(GameFramework.World.Entity e) { }
        }

        private static LOPWorld MakeWorld(EntityRegistry registry)
            => new LOPWorld(registry, new WorldEventBuffer(),
                new MovementSystem(new StatsSystem(), new MotionContributionSystem()),
                new AbilitySystem(new ManaSystem()), new StatusEffectSystem(new StatsSystem()),
                new AbilityEffectExecutor(null), new KinematicMoveSystem(new FakeQuery(), ~0),
                new SpyBridge());
```

확인해 둔 시그니처(추측하지 말 것):

| 타입 | 생성자 |
|---|---|
| `MovementSystem` | `(StatsSystem, MotionContributionSystem)` |
| `AbilitySystem` | `(ManaSystem)` |
| `StatusEffectSystem` | `(StatsSystem)` |
| `AbilityEffectExecutor` | `(IEnumerable<IAbilityEffectHandler>)` — 테스트는 `null` 전달 |
| `KinematicMoveSystem` | `(ICollisionQuery, int layerMask)` |
| `WorldEventBuffer` | 인자 없음 |
| `ActiveEffect` | `(int effectId, long expireTick, int stackCount, string sourceEntityId, string sourceId)` |
| `AbilityData` | **readonly struct** — 널 표현은 `AbilityData?` |

> Task 4에서 `LOPWorld` 생성자에 인자가 하나 더 붙는다. 그때 이 `MakeWorld`와 **`LOPWorldTests.cs`의 기존 호출 7곳**을 함께 고쳐야 한다.

- [ ] **Step 5: 테스트가 실패하는 것을 확인한다**

컴파일 에러(`TryGetSavedStatusEffects` 없음, `LOPWorld` 생성자 인자 수 불일치)가 나야 정상이다.

- [ ] **Step 6: `LOPWorld`에 게임 상태 저장·복원을 넣는다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/LOPWorld.cs`의 클래스 안, 필드 선언 아래에 추가:

```csharp
        // 스킬·상태이상·스탯·마나의 틱별 사진. 위치·속도는 WorldBase가 담는다.
        private readonly GameFramework.Netcode.SequenceBuffer<System.Collections.Generic.Dictionary<string, LOPSavedState>> _gameFrames
            = new GameFramework.Netcode.SequenceBuffer<System.Collections.Generic.Dictionary<string, LOPSavedState>>(SaveCapacity);

        protected override void SaveGameState(long tick)
        {
            var frame = new System.Collections.Generic.Dictionary<string, LOPSavedState>();
            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Has<GameFramework.World.Simulated>())
                {
                    frame[entity.Id] = LOPSavedState.Capture(entity);
                }
            }
            _gameFrames.Record(tick, frame);
        }

        protected override bool LoadGameState(long tick)
        {
            if (!_gameFrames.TryGet(tick, out var frame))
            {
                return false;
            }
            foreach (var pair in frame)
            {
                var entity = EntityRegistry.Get(pair.Key);
                if (entity != null)
                {
                    pair.Value.RestoreTo(entity);
                }
            }
            return true;
        }

        /// <summary>
        /// 그 틱에 내가 예측했던 상태이상 목록. 서버 스냅과 <b>같은 시점끼리</b> 비교해야 해서
        /// 클라 보정이 읽는다 — 지금 살아있는 목록과 비교하면 클라가 서버보다 앞서 달리는 만큼
        /// 늘 달라 보여 불필요한 되돌리기가 매 스냅마다 일어난다.
        /// </summary>
        public bool TryGetSavedStatusEffects(long tick, string entityId, out System.Collections.Generic.List<ActiveEffect> effects)
        {
            effects = null;
            if (_gameFrames.TryGet(tick, out var frame) && frame.TryGetValue(entityId, out var saved))
            {
                effects = saved.StatusEffects;
                return true;
            }
            return false;
        }
```

> 보관 길이는 리터럴 `128`이 아니라 베이스의 `SaveCapacity`를 그대로 쓴다(Task 1에서 `protected const`로 열어 뒀다). 두 값이 어긋나면 되돌리기가 반쪽이 되는데 컴파일도 테스트도 그걸 잡아주지 못한다.

- [ ] **Step 7: 테스트를 돌리고 일부러 깨뜨려 본다**

Task 1 Step 7과 같은 방법. 통과 후 `LoadGameState`의 `pair.Value.RestoreTo(entity);`를 잠시 주석 처리해 **실패하는지** 확인하고 되돌린다.

- [ ] **Step 8: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/LOPWorld.cs Runtime/Scripts/Game/LOPSavedState.cs Runtime/Scripts/Game/LOPSavedState.cs.meta Tests/EditMode/LOPWorldSaveLoadTests.cs Tests/EditMode/LOPWorldSaveLoadTests.cs.meta
git commit -m "$(cat <<'EOF'
feat(world): LOPWorld가 스킬·상태이상을 자기 상태로 담는다

넷코드가 들고 있던 사진첩을 월드로 옮긴다. 이제 저장·복원은 world.SaveState /
LoadState 한 쌍이고, 그 안에 무엇이 담기는지는 게임만 안다.

PredictedAbilityState는 LOPSavedState로 이름을 바꿨다 — 담는 게 어빌리티만이
아니라 상태이상·스탯·마나까지이고, 예측 전용도 아니기 때문이다.

TryGetSavedStatusEffects는 클라 보정이 같은 시점끼리 비교하려고 읽는다.
지금 살아있는 목록과 비교하면 클라가 앞서 달리는 만큼 늘 달라 보인다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 9: 클·서의 rename 치환분을 각 레포에 바로 커밋한다**

Step 3의 `sed`가 클라·서버 파일도 고쳤다. 그 변경을 **여기서 커밋한다** — 다음 태스크까지 미루면 그 태스크의 리뷰 패키지에 남의 변경이 섞여 리뷰어가 범위를 가릴 수 없다.

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git checkout -b feature/flappy-b2c-netcode-agnostic 2>/dev/null || git checkout feature/flappy-b2c-netcode-agnostic
git add -u Assets/Scripts
git commit -m "refactor: PredictedAbilityState를 LOPSavedState로 따라 바꾼다"
```

서버도 같은 방식으로(브랜치 `feature/flappy-b2c-netcode-agnostic`). **경로를 명시해** 커밋한다 — 서버 레포에는 빌드 타깃 때문에 생긴 URP 에셋 노이즈가 미커밋으로 남아 있고, 그건 절대 커밋하지 않는다.

---

### Task 3: `AbilityActivator`를 공용으로 올린다

클라와 서버에 주석 한 줄만 다른 사본이 있다. 하나로 합친다. **세 레포를 한 태스크에서** 바꾼다 — 같은 네임스페이스에 사본이 둘 남으면 참조가 모호해져 컴파일이 깨지기 때문이다.

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/AbilityActivator.cs`
- Delete: `LeagueOfPhysical-Client/Assets/Scripts/Game/AbilityActivator.cs` (+ `.meta`)
- Delete: `LeagueOfPhysical-Server/Assets/Scripts/Game/AbilityActivator.cs` (+ `.meta`)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/GameplayInstaller.cs`
- Modify: 서버의 DI 등록 파일(아래 Step 4에서 찾는다)

**Interfaces:**
- Produces: `LOP.AbilityActivator(AbilitySystem, System.Func<int, AbilityData?>, EntityRegistry, WorldEventBuffer)` — 기존 메서드 3개(`TryActivate`, `TryGetAbilityIdBySlot`, `TryActivateSlot`)의 시그니처는 그대로다. 호출부 변경 없음.

- [ ] **Step 1: 공용 파일을 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/AbilityActivator.cs` — 클라 사본을 그대로 옮기되 **생성자의 데이터 조회만** 바꾼다:

```csharp
namespace LOP
{
    /// <summary>
    /// 어빌리티 id로 발동을 라우팅한다(런타임 식별=int id).
    /// id가 마스터데이터에 있으면 <see cref="AbilitySystem.TryActivate"/>로 발동하고 true, 아니면 false.
    /// </summary>
    public class AbilityActivator
    {
        private readonly AbilitySystem abilitySystem;
        // 마스터데이터는 클·서가 서로 다른 패키지를 보므로(상호 비참조) 공용 코드가 직접 읽을 수 없다.
        // 그래서 조회만 사이드에서 받아 온다 — StatusEffectApplyEffectHandler가 쓰는 방식과 같다.
        private readonly System.Func<int, AbilityData?> resolveAbility;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.WorldEventBuffer worldEventBuffer;

        public AbilityActivator(
            AbilitySystem abilitySystem,
            System.Func<int, AbilityData?> resolveAbility,
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.WorldEventBuffer worldEventBuffer)
        {
            this.abilitySystem = abilitySystem;
            this.resolveAbility = resolveAbility;
            this.entityRegistry = entityRegistry;
            this.worldEventBuffer = worldEventBuffer;
        }

        public bool TryActivate(string casterEntityId, int abilityId, long currentTick)
        {
            var ability = resolveAbility(abilityId);
            if (ability == null)
            {
                return false;
            }

            var caster = entityRegistry.Get(casterEntityId);
            if (caster == null)
            {
                return false;
            }

            // effect는 ability.Effects에 실려 있고, Active 창에서 executor가 타입별 핸들러로 디스패치한다.
            bool activated = abilitySystem.TryActivate(caster, ability.Value, caster, currentTick);
            if (activated)
            {
                // 발동 연출 cue — 플레이어·AI 모든 발동 경로가 여기로 모이므로 발화를 한 곳에서 한다.
                worldEventBuffer.Append(new GameFramework.World.AbilityActivatedEvent(casterEntityId, abilityId));
            }
            return activated;
        }

        /// <summary>슬롯에 장착된 어빌리티 id를 찾는다. 입력 캡처가 슬롯을 id로 풀 때 쓴다.</summary>
        public bool TryGetAbilityIdBySlot(string casterEntityId, int slot, out int abilityId)
        {
            abilityId = 0;
            var caster = entityRegistry.Get(casterEntityId);
            if (caster == null)
            {
                return false;
            }
            return abilitySystem.TryGetAbilityIdBySlot(caster, slot, out abilityId);
        }

        /// <summary>슬롯으로 발동. id를 푼 뒤 기존 <see cref="TryActivate"/> 경로로 합류한다.</summary>
        public bool TryActivateSlot(string casterEntityId, int slot, long currentTick)
        {
            if (TryGetAbilityIdBySlot(casterEntityId, slot, out int abilityId) == false)
            {
                return false;
            }
            return TryActivate(casterEntityId, abilityId, currentTick);
        }
    }
}
```

> `AbilityData`가 struct인지 class인지 먼저 확인한다(`grep -n "AbilityData" LeagueOfPhysical-Shared/Runtime/Scripts/Game/*.cs`). struct면 위처럼 `AbilityData?` + `.Value`, class면 `AbilityData` + null 검사로 쓴다.

- [ ] **Step 2: 사본 두 개를 지운다**

```bash
cd /Users/insoobae/workspace/LOP
git -C LeagueOfPhysical-Client rm Assets/Scripts/Game/AbilityActivator.cs Assets/Scripts/Game/AbilityActivator.cs.meta
git -C LeagueOfPhysical-Server rm Assets/Scripts/Game/AbilityActivator.cs Assets/Scripts/Game/AbilityActivator.cs.meta
```

- [ ] **Step 3: 클라 DI 등록을 팩토리로 바꾼다**

`LeagueOfPhysical-Client/Assets/Scripts/Game/GameplayInstaller.cs`의 `builder.Register<AbilityActivator>(Lifetime.Singleton);` 한 줄을 교체:

```csharp
            // 마스터데이터 조회만 사이드에서 넣어 준다(클·서 패키지가 서로 다름).
            builder.Register(c => new AbilityActivator(
                c.Resolve<AbilitySystem>(),
                id => c.Resolve<AbilityDataProvider>().TryGet(id, out var data) ? data : (AbilityData?)null,
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>()), Lifetime.Singleton);
```

- [ ] **Step 4: 서버 DI 등록도 같은 모양으로 바꾼다**

```bash
grep -rn "AbilityActivator" /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts | grep -i "register\|installer\|scope"
```

찾은 곳의 등록을 Step 3과 같은 팩토리 형태로 바꾼다(타입 이름은 서버 쪽 `AbilityDataProvider`).

- [ ] **Step 5: 양쪽 컴파일 확인**

클라와 서버 에디터에서 각각 컴파일한다. 서버 에디터가 안 떠 있으면 배치모드로:

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -nographics -projectPath /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server \
  -logFile - 2>&1 | grep -E "error CS|Compilation failed|Exiting batchmode" | head -20
```

`error CS`가 없어야 한다.

- [ ] **Step 6: 세 레포에 각각 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/AbilityActivator.cs Runtime/Scripts/Game/AbilityActivator.cs.meta
git commit -m "$(cat <<'EOF'
refactor(ability): 발동 라우터를 공용으로 올린다

클라와 서버에 주석 한 줄만 다른 사본이 있었다. 곧 월드가 입력에서 직접
발동시켜야 해서 공용 자리가 필요하고, 겸사겸사 사본 하나가 사라진다.

마스터데이터는 클·서가 서로 다른 패키지를 보므로(상호 비참조) 공용 코드가
직접 읽을 수 없다. 그래서 조회만 델리게이트로 받는다 —
StatusEffectApplyEffectHandler가 이미 쓰는 방식이다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

클라·서버는 각각 삭제 + 등록 변경을 커밋한다(메시지: `refactor(ability): 발동 라우터 사본을 공용 것으로 대체한다`).

---

### Task 4: 발동을 월드 안으로 옮긴다 (클라·서버 동시)

재생 중에 넷코드가 스킬을 발동시키던 것을 없앤다. 입력은 버퍼에 놓이고, 그걸 읽어 발동하는 건 월드다.

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/LOPWorld.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/PlayerInputManager.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/ServerInputSystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/LOPWorldInputActivationTests.cs`

**Interfaces:**
- Consumes: Task 3의 `AbilityActivator`(공용)
- Produces: `LOPWorld` 생성자에 `AbilityActivator` 인자 추가(마지막 자리). Task 2의 테스트 조립도 이 시그니처를 쓴다.

**순서가 중요하다.** 지금 라이브 경로는 양쪽 모두 `입력 확정 → 발동 → world.Tick(이동…)`이다(클라 `PlayerInputManager`, 서버 `ServerInputSystem` 둘 다 `world.Tick` 앞에서 돈다 — 실측). 그래서 발동은 `Mutation`의 **첫 페이즈**, 이동보다 앞에 와야 한다. 대시 발동 틱의 입력 게이트 타이밍이 이 순서에 걸려 있다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/LOPWorldInputActivationTests.cs`:

```csharp
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    // 입력에 실린 어빌리티는 월드가 읽어서 발동시킨다 — 넷코드가 아니라.
    public class LOPWorldInputActivationTests
    {
        private const int AbilityId = 1;

        private class FakeQuery : GameFramework.Physics.ICollisionQuery
        {
            public GameFramework.Physics.CollisionHit CapsuleCast(UnityEngine.Vector3 p1, UnityEngine.Vector3 p2,
                float radius, UnityEngine.Vector3 dir, float dist, int mask) => GameFramework.Physics.CollisionHit.None;
        }

        private class SpyBridge : GameFramework.World.IMotionBridge
        {
            public void SyncTransforms() { }
            public void Depenetrate(GameFramework.World.Entity e) { }
            public void Separate(GameFramework.World.Entity e) { }
            public void PushMotion(GameFramework.World.Entity e) { }
        }

        // 효과 없이 페이즈만 도는 최소 어빌리티. 발동 성공 여부만 볼 것이라 효과는 필요 없다.
        private static AbilityData? Resolve(int id)
            => id == AbilityId
                ? (AbilityData?)new AbilityData(AbilityId, 10, 0, 2, 3, 2, new AbilityEffect[0])
                : null;

        private static LOPWorld MakeWorld(EntityRegistry registry)
        {
            var eventBuffer = new WorldEventBuffer();
            return new LOPWorld(registry, eventBuffer,
                new MovementSystem(new StatsSystem(), new MotionContributionSystem()),
                new AbilitySystem(new ManaSystem()), new StatusEffectSystem(new StatsSystem()),
                new AbilityEffectExecutor(null), new KinematicMoveSystem(new FakeQuery(), ~0),
                new SpyBridge(),
                new AbilityActivator(new AbilitySystem(new ManaSystem()), Resolve, registry, eventBuffer));
        }

        private static Entity MakeEntity(string id, bool simulated, InputCommand command)
        {
            var e = new Entity(id);
            if (simulated)
            {
                e.Add(new Simulated());
            }
            e.Add(new GameFramework.World.Transform());
            e.Add(new Velocity());
            e.Add(new Abilities());
            e.Add(new StatusEffects());
            e.Add(new Stats());
            e.Add(new Mana(100));
            var buffer = new InputBuffer { Current = command };
            e.Add(buffer);
            return e;
        }

        [Test]
        public void Tick_입력에_어빌리티가_실려_있으면_발동한다()
        {
            var registry = new EntityRegistry();
            var entity = MakeEntity("a", true, new InputCommand { AbilityId = AbilityId });
            registry.Add(entity);

            MakeWorld(registry).Tick(1, 0.02f);

            Assert.IsNotNull(entity.Get<Abilities>().Activation);
        }

        [Test]
        public void Tick_어빌리티가_0이면_아무것도_발동하지_않는다()
        {
            var registry = new EntityRegistry();
            var entity = MakeEntity("a", true, new InputCommand());
            registry.Add(entity);

            MakeWorld(registry).Tick(1, 0.02f);

            Assert.IsNull(entity.Get<Abilities>().Activation);
        }

        [Test]
        public void Tick_Simulated가_없으면_발동하지_않는다()
        {
            // 클라에서 남의 캐릭이 입력을 들고 있어도 내가 대신 굴리면 안 된다.
            var registry = new EntityRegistry();
            var entity = MakeEntity("other", false, new InputCommand { AbilityId = AbilityId });
            registry.Add(entity);

            MakeWorld(registry).Tick(1, 0.02f);

            Assert.IsNull(entity.Get<Abilities>().Activation);
        }
    }
}
```

> `AbilityData` 생성자의 뒷 인자들(`StartupMoveScale` 등)은 기본값이 있는지 확인해 맞춘다 —
> `AbilityReplayDeterminismTests.Haste()`가 7인자로 부르고 있으므로 그 형태를 따르면 된다.
> `Abilities.Activation`이 발동 직후 어떤 값이 되는지도 그 테스트에 선례가 있다.

- [ ] **Step 2: 테스트가 실패하는 것을 확인한다**

- [ ] **Step 3: `LOPWorld`가 입력에서 발동한다**

생성자에 `AbilityActivator abilityActivator`를 마지막 인자로 추가하고 필드에 보관한다. `Mutation`의 **맨 앞**에 페이즈를 하나 넣는다:

```csharp
            // 입력에 실린 어빌리티 발동. 이동보다 먼저 해야 한다 — 대시 발동 틱의 입력 게이트가
            // 이 순서에 걸려 있고, 라이브(입력 캡처 → world.Tick)와 재생의 순서가 같아야 결과가 갈리지 않는다.
            foreach (var entity in EntityRegistry.All)
            {
                if (!entity.Has<GameFramework.World.Simulated>())
                {
                    continue;
                }
                var command = entity.Get<InputBuffer>()?.Current;
                if (command != null && command.AbilityId != 0)
                {
                    _abilityActivator.TryActivate(entity.Id, command.AbilityId, tick);
                }
            }
```

> 두 번 발동할 걱정은 없다. `InputBuffer.Current`는 틱마다 새로 확정되고(서버 `Consume`, 클라 `SetCurrent`), 유실 틱을 메우는 `PredictMissing`은 **이동만 이어 쓰고 어빌리티·점프는 일부러 뺀다**(그쪽 주석 참고).

- [ ] **Step 3b: 늘어난 생성자 인자에 맞춰 기존 호출부를 고친다**

`LOPWorld` 생성자에 인자가 하나 붙었다. 고쳐야 할 곳:

```bash
cd /Users/insoobae/workspace/LOP
grep -rn "new LOPWorld(" LeagueOfPhysical-Shared LeagueOfPhysical-Client LeagueOfPhysical-Server | grep -v "/Library/"
```

- `LeagueOfPhysical-Shared/Tests/EditMode/LOPWorldTests.cs` — **7곳**(실측). 각 호출 끝에 `new AbilityActivator(...)`를 더한다. 그 파일에 이미 `FakeQuery`/`SpyBridge`가 있으므로 조립은 그대로 재사용한다
- `LeagueOfPhysical-Shared/Tests/EditMode/LOPWorldSaveLoadTests.cs` — Task 2에서 만든 `MakeWorld`
- 클라·서버는 DI가 생성자를 자동 해석하므로 **등록 코드 변경이 필요 없다**(`AbilityActivator`가 이미 컨테이너에 있다). 컴파일로 확인한다

- [ ] **Step 4: 클라의 발동 호출을 지운다**

`PlayerInputManager.ProcessInput`에서 아래 블록을 삭제한다(`inputHistory.Record`는 남긴다):

```csharp
            // 어빌리티 예측 발동(연출 cue는 AbilityActivator가 내부에서 append).
            if (command.AbilityId != 0)
            {
                abilityActivator.TryActivate(playerContext.entityId, command.AbilityId, tick);
            }
```

`abilityActivator` 필드는 **남긴다** — `TryGetAbilityIdBySlot`(슬롯→id 해석)을 아직 쓴다. 안 쓰게 됐는지 컴파일러 경고로 확인한다.

- [ ] **Step 5: 서버의 발동 호출을 지운다**

`ServerInputSystem.Tick`에서 아래 블록과 `abilityActivator` 필드·생성자 인자를 삭제한다:

```csharp
                if (input.AbilityId != 0)
                {
                    abilityActivator.TryActivate(worldEntity.Id, input.AbilityId, tick);
                }
```

`gap` 계산 등 나머지는 그대로 둔다. 서버 AI(`EnemyBrain.TryActivateSlot`)는 입력이 아니라 의도이므로 **건드리지 않는다.**

- [ ] **Step 6: 테스트 + 양쪽 컴파일**

Shared EditMode 테스트 전체를 돌린다 — 특히 `AbilityReplayDeterminismTests`가 통과해야 한다(재생 순서를 바꿨으므로). 클라·서버 컴파일도 확인한다.

- [ ] **Step 7: 세 레포에 커밋**

Shared 커밋 메시지:

```
feat(world): 어빌리티 발동을 월드가 입력에서 읽어 한다

되감기가 스킬을 직접 발동시키고 있었다. 표준은 반대다 — 입력은 데이터로
버퍼에 놓이고, 그게 무슨 뜻인지는 시뮬이 안다(Quantum PollInput,
Unity NetCode ICommandData).

이동보다 먼저 도는 페이즈에 넣는다. 라이브가 입력 캡처 직후 발동시키고
그다음 world.Tick으로 이동하므로, 순서가 같아야 재생 결과가 갈리지 않는다.
```

---

### Task 5: 클라 — 서버 보정의 게임 고유 부분을 훅으로 뺀다

`Reconciler`에 남은 마지막 게임 의존(상태이상 비교·적용)을 게임별 구현체로 옮긴다.

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/IServerCorrectionHandler.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/LOPServerCorrectionHandler.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/NoServerCorrection.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlapWangLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`

**Interfaces:**
- Consumes: Task 2의 `LOPWorld.TryGetSavedStatusEffects`
- Produces: `IServerCorrectionHandler.Matches(long tick, EntitySnap snap) → bool`, `IServerCorrectionHandler.ApplyAuthoritative(GameFramework.World.Entity entity, EntitySnap snap)`. Task 6의 `Reconciler`가 이 두 개만 부른다.

- [ ] **Step 1: 인터페이스**

```csharp
namespace LOP
{
    /// <summary>
    /// 서버 스냅 중 <b>게임마다 다른 부분</b>을 다룬다. 위치·속도는 게임과 무관해 되감기가 직접 처리하고,
    /// 여기엔 그 게임에만 있는 것(예: 상태이상)이 온다.
    /// Unreal의 <c>ServerMoveHandleClientError</c>가 게임의 이동 컴포넌트에 있는 것과 같은 자리.
    /// </summary>
    public interface IServerCorrectionHandler
    {
        /// <summary>그 틱 내 예측이 서버와 맞는가. false면 위치가 맞아도 되돌린다.</summary>
        bool Matches(long tick, EntitySnap snap);

        /// <summary>서버가 진실인 부분을 덮어쓴다. 되돌린 직후에 불린다.</summary>
        void ApplyAuthoritative(GameFramework.World.Entity entity, EntitySnap snap);
    }
}
```

- [ ] **Step 2: LOP 구현**

`LOPServerCorrectionHandler.cs` — 지금 `Reconciler`에 있는 상태이상 게이트·적용 로직을 **주석까지 그대로** 옮긴다:

```csharp
namespace LOP
{
    /// <summary>FlapWang(캐릭터 게임)의 서버 보정 — 상태이상이 서버 권위다.</summary>
    public class LOPServerCorrectionHandler : IServerCorrectionHandler
    {
        private readonly LOPWorld world;   // 같은 게임 안이므로 구체를 직접 본다
        private readonly StatusEffectSystem statusEffectSystem;
        private readonly StatusEffectDataProvider statusEffectDataProvider;

        public LOPServerCorrectionHandler(
            LOPWorld world,
            StatusEffectSystem statusEffectSystem,
            StatusEffectDataProvider statusEffectDataProvider)
        {
            this.world = world;
            this.statusEffectSystem = statusEffectSystem;
            this.statusEffectDataProvider = statusEffectDataProvider;
        }

        public bool Matches(long tick, EntitySnap snap)
        {
            // 앵커 틱 기록이 없으면(정상 경로엔 없는 엣지) 비교 불가 — 불일치로 단정하지 않고 위치 판정에 맡긴다.
            if (!world.TryGetSavedStatusEffects(tick, snap.entityId, out var predicted))
            {
                return true;
            }
            return !StatusEffectReconcileGate.ShouldReconcile(
                predicted, snap.statusEffects, statusEffectDataProvider.Get);
        }

        public void ApplyAuthoritative(GameFramework.World.Entity entity, EntitySnap snap)
        {
            statusEffectSystem.ApplyAuthoritativeState(entity, snap.statusEffects, statusEffectDataProvider.Get);
        }
    }
}
```

> `Reconciler`가 이 게이트 앞에 달아 둔 긴 설명 주석(같은 시점끼리 비교해야 하는 이유, 만료틱까지 봐야 하는 이유)을 **여기로 옮긴다.** 그 주석은 지금도 유효하고, 없으면 왜 이렇게 비교하는지 알 수 없다.

- [ ] **Step 3: 무동작 구현**

```csharp
namespace LOP
{
    /// <summary>스냅에서 위치 말고 맞춰야 할 게 없는 게임용(예: Flappy Race). 아무 일도 하지 않는다.</summary>
    public class NoServerCorrection : IServerCorrectionHandler
    {
        public bool Matches(long tick, EntitySnap snap) => true;
        public void ApplyAuthoritative(GameFramework.World.Entity entity, EntitySnap snap) { }
    }
}
```

- [ ] **Step 4: 게임 스코프에 등록한다**

`FlapWangLifetimeScope.ConfigureGame`의 `IWorld` 등록 아래:

```csharp
            // LOPWorld를 구체로도 해석할 수 있어야 보정 핸들러가 자기 게임 월드를 직접 본다.
            builder.Register<LOPWorld>(Lifetime.Singleton).As<GameFramework.World.IWorld>().AsSelf();
            builder.Register<IServerCorrectionHandler, LOPServerCorrectionHandler>(Lifetime.Singleton);
```

기존 `builder.Register<GameFramework.World.IWorld, LOPWorld>(...)` 한 줄을 위 두 줄로 바꾼다. 목적은 **한 인스턴스를 두 이름으로 해석**하게 하는 것이다 — 두 번 `Register` 하면 월드가 두 개 생겨 되감기와 보정이 서로 다른 상태를 본다. `As<T>().AsSelf()`가 그 방법이고, 이 프로젝트도 이미 쓰고 있다(`Assets/Scripts/RootLifetimeScope.cs:58-64`).

`FlappyRaceLifetimeScope.ConfigureGame`에는:

```csharp
            builder.Register<IServerCorrectionHandler, NoServerCorrection>(Lifetime.Singleton);
```

- [ ] **Step 5: 컴파일 확인 후 커밋**

`.meta` 3개를 함께 커밋한다.

---

### Task 6: 클라 — `Reconciler`를 슬림하게 만든다

이 태스크가 슬라이스의 목적지다. 끝나면 넷코드에서 "스킬"·"상태이상"이 사라진다.

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/Reconciler.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/TickSystems/LocalSnapshotSystem.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/GameplayInstaller.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs`

**Interfaces:**
- Consumes: Task 1의 `IWorld` 저장/복원, Task 5의 `IServerCorrectionHandler`
- Produces: 없음(내부 정리)

- [ ] **Step 1: `LocalSnapshotSystem`을 한 줄로 만든다**

`Tick` 본문 전체를 교체하고, `snapshotHistory`·`LOPSavedState`·`entityRegistry`·`playerContext` 의존을 지운다:

```csharp
        public void Tick(long tick, float deltaTime)
        {
            // 이번 틱 시뮬 결과를 보관한다. 무엇을 담을지는 월드가 안다.
            world.SaveState(tick);
        }
```

클래스 문서 주석도 고친다 — "내 캐릭의 스냅샷을 남긴다"가 아니라 "월드에게 이번 틱 상태를 보관시킨다". 파일 이름은 `LocalSnapshotSystem`으로 두되, 이름이 더 이상 하는 일과 맞지 않으면 `WorldStateSaveSystem`으로 바꾸고 `.meta`와 `LOPRunner`의 필드도 함께 고친다.

- [ ] **Step 2: `Reconciler`의 생성자를 줄인다**

지우는 것: `abilityActivator`, `snapshotHistory`, `predictedAbilityStateHistory`(= `SequenceBuffer<LOPSavedState>`), `statusEffectSystem`, `statusEffectDataProvider`.
더하는 것: `IServerCorrectionHandler correction`.
남는 것: `playerContext`, `entityRegistry`, `worldEventBuffer`, `inputHistory`, `world`, `motionBridge`, `reconciliationStats`, `inputTimingStats`, `renderCorrectionSmoother`.

- [ ] **Step 3: `Reconcile` 본문을 새 순서로 고친다**

바뀌는 곳만 정확히:

1. 오차 계산의 조회를 바꾼다:
   ```csharp
   if (world.TryGetSavedMotion(anchorTick, entityId, out var predicted))
   ```
   (기존 `snapshotHistory.TryGet(anchorTick, out var predicted)`. `predicted.Position`/`.Velocity` 사용부는 그대로.)

2. 상태이상 게이트를 훅 호출로 바꾼다:
   ```csharp
   bool statusMatches = correction.Matches(anchorTick, snap);
   ```
   (기존 `predictedAbilityStateHistory.TryGet(...)` + `StatusEffectReconcileGate...` 블록 전체를 대체. 그 긴 설명 주석은 Task 5에서 핸들러로 옮겼다.)

3. **복원과 권위 적용의 순서를 바꾼다.** 지금은 "권위 적용 → 예측 상태 복원"인데, 새 구조에선 `LoadState`가 위치까지 되돌리므로 **되돌린 다음 권위를 덮어써야** 한다:

   ```csharp
   reconciliationStats.RecordCorrection();

   // 예측 상태로 되돌린다(위치·속도·게임 상태 전부). 기록이 없는 두 경우를 가른다:
   //  · 앵커가 내 첫 기록보다 과거 = 내가 아직 매치에 없던 틱. 되돌릴 게 없을 뿐 재생은 정상으로 한다.
   //  · 그 외 = 살았던 틱인데 밀려남. 그때 상태를 알 수 없어 재생은 생략한다(권위 위치는 아래서 적용).
   bool restored = world.LoadState(anchorTick);
   bool tooOld = !restored
       && (world.FirstSavedTick is not long first || anchorTick >= first);

   // 권위 값을 그 위에 덮는다 — 서버가 진실인 축(위치·회전·속도·외력·게임 고유분).
   GameFramework.World.EntityMotionExtensions.SetPosition(worldEntity, snap.position);
   GameFramework.World.EntityMotionExtensions.SetRotation(worldEntity, snap.rotation);
   GameFramework.World.EntityMotionExtensions.SetVelocity(worldEntity, snap.velocity);

   var motionContributions = worldEntity.Get<MotionContributions>();
   if (motionContributions != null)
   {
       motionContributions.Items.Clear();
       motionContributions.Items.AddRange(snap.contributions);
   }

   correction.ApplyAuthoritative(worldEntity, snap);

   motionBridge.PushMotion(worldEntity);
   Physics.SyncTransforms();

   if (tooOld || currentTick - anchorTick > MaxReplayTicks)
   {
       renderCorrectionSmoother.OnCorrection(
           preCorrectionPos.ToNumerics(),
           GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity).ToNumerics());
       return;
   }
   ```

   > 예전엔 재생을 생략하고 빠져나갈 때 렌더 스무더를 부르지 않았다. 이제는 부른다 — 그 경로에서도 시뮬 위치가 튀는 건 같으므로, 안 부르면 화면이 순간이동한다. 이건 **의도한 동작 개선**이니 검증에서 눈으로 확인한다.

4. 재생 루프에서 발동 호출과 히스토리 재기록 두 줄을 지운다:

   ```csharp
   using (worldEventBuffer.Suppress())
   {
       for (long t = anchorTick + 1; t < currentTick; t++)
       {
           inputBuffer.Current = inputHistory.TryGet(t, out var recorded) ? recorded : null;

           // 재생 = 라이브와 같은 단일 진입점. 입력에 실린 발동도 월드가 알아서 한다.
           world.Tick(t, deltaTime);

           // 보정값으로 다시 보관한다(다음 비교·재생이 낡은 값을 안 보도록).
           world.SaveState(t);
       }
   }
   ```

- [ ] **Step 4: DI 등록과 HUD를 정리한다**

`GameplayInstaller`에서 지운다:

```csharp
            builder.Register(_ => new GameFramework.Netcode.SnapshotHistory(128), Lifetime.Singleton);
            builder.Register(_ => new GameFramework.Netcode.SequenceBuffer<LOPSavedState>(128), Lifetime.Singleton);
```

`SequenceBuffer<InputCommand>` 등록은 **남긴다**(재생이 입력을 다시 먹이려면 필요하다).

`DebugHudViewModel`의 두 프로퍼티를 월드에서 읽게 바꾼다:

```csharp
        public long SnapshotFirstTick => world.FirstSavedTick ?? -1;
        public long SnapshotLatestTick => world.LatestSavedTick ?? -1;
```

`SnapshotCount`를 쓰는 View가 있으면 함께 고친다(`grep -rn "SnapshotCount" Assets/Scripts`).

- [ ] **Step 5: 목표가 달성됐는지 기계적으로 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
grep -nE "Ability|StatusEffect|LOPSavedState" Assets/Scripts/Netcode/Reconciler.cs
```

**아무것도 출력되지 않아야 한다.** 이게 이 슬라이스의 완료 신호다.

- [ ] **Step 6: 컴파일 + 커밋**

---

### Task 7: 뒤집힌 결정을 문서에 반영한다

`Snapshot/Restore를 코어에 두지 않는다`는 결정이 이 슬라이스에서 뒤집혔다. 두 문서는 **매 세션 자동 로드**되므로 낡은 채로 두면 다음 작업이 잘못된 전제로 시작한다.

**Files:**
- Modify: `LeagueOfPhysical-Client/docs/netcode-redesign.md` (§6.5)
- Modify: `LeagueOfPhysical-Client/docs/world-core-connection-architecture.md`
- 서버 레포에 같은 파일 사본이 있으면 함께(확인: `ls /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server/docs`)

- [ ] **Step 1: `netcode-redesign.md` §6.5를 고친다**

"Snapshot/Restore 책임 위치" 절의 결론을 뒤집고, 왜 뒤집었는지 남긴다:

```markdown
### 6.5 상태 저장/복원 책임 위치 — **월드** (2026-08-19 정정)

> ⚠️ **뒤집힌 결정.** 이 절은 원래 "`Snapshot()`/`Restore(snap)`를 시뮬 코어에 두지 않는다 —
> 보관·복원은 클라 외각의 책임"이었다. B2-c에서 **반대로 정정한다.**

이유: 외각이 상태의 *모양*을 소유하면 외각이 게임을 알아야 한다. 실제로 그렇게 됐다 —
`Reconciler`가 `SequenceBuffer<PredictedAbilityState>`를 들고 `AbilityActivator`를 부르느라
스킬이 없는 게임은 되감기를 만들 수조차 없었다.

표준은 시뮬이 소유하는 쪽이다: GGPO `save_game_state`/`load_game_state`(엔진은 불투명한
바이트로만 받는다), Photon Quantum 프레임 스냅샷, Unreal `FSavedMove_Character`(게임이
서브클래싱해 자기 데이터를 얹는다).

당시 근거였던 YAGNI("서버는 전체 롤백을 안 한다")는 실체가 없다 — 서버는 `SaveState`를
부르지 않으면 그만이고, 인터페이스에 메서드가 있다고 비용이 생기지 않는다.

현재 모양은 `IWorld.SaveState`/`LoadState` + `WorldBase`의 위치·속도 + 게임별
`SaveGameState`/`LoadGameState` 훅이다. 상세는
`docs/superpowers/specs/2026-08-17-flappy-race-gameplay-b2-design.md` §4.
```

- [ ] **Step 2: `world-core-connection-architecture.md`의 같은 문장을 고친다**

```bash
grep -n "Snapshot()/Restore\|Snapshot()\`/\`Restore\|코어에 두지 않는다" docs/world-core-connection-architecture.md
```

찾은 두 곳(“코어에 요구되는 능력” 절과 `IGameSimulation` 책임 경계 노트)을 새 결정으로 고치고, §6.5를 가리키게 한다.

- [ ] **Step 3: 커밋**

---

### Task 8: 런타임 검증 — FlapWang 회귀 없음 + Flappy 동작

이 슬라이스는 잘 도는 예측·롤백을 건드렸다. **회귀 없음을 눈과 숫자로 확인해야 끝난다.**

- [ ] **Step 1: 4개 레포를 각각 main에 머지한다**

각 레포에서 `git fetch && git rebase origin/main` 후 `--no-ff` 머지, 푸시. **푸시 전 사용자 확인을 받는다**(공유 브랜치).

- [ ] **Step 2: 서버 이미지를 빌드·배포한다**

서버 코드가 바뀌었으므로 콘텐츠만으로는 안 된다. 배포 경로는 `docs/lop-repo-topology.md`의 GitHub Actions → 이미지 → infrastructure 태그 bump → ArgoCD.

- [ ] **Step 3: FlapWang 회귀 확인**

에디터에서 FlapWang으로 입장해 확인한다:

| 항목 | 기대 |
|---|---|
| 공중에서 점프 → 낙하 | 예전과 같은 궤적. 고무줄 튕김 없음 |
| `DebugHud`의 reconciliation distance (last/avg/max) | 개편 전과 같은 수준 |
| 스킬 발동(대시 포함) | 즉시 반응, 서버 확정 후 위치 튐 없음 |
| 상태이상(슬로우 등) 피격 | 걸릴 때·풀릴 때 불필요한 되돌리기가 매 스냅 일어나지 않음 |
| 콘솔 | `[ReconSpike]` 경고가 개편 전보다 늘지 않음 |

- [ ] **Step 4: Flappy Race 확인**

플래피 레이스로 입장해 새가 스폰되고 서버 파드에 에러가 없는지 본다. B2-b/d가 아직 없어 **게임플레이는 없다** — 여기서 보는 건 "되감기가 스킬 없이도 성립한다"뿐이다.

- [ ] **Step 5: 결과를 spec에 기록한다**

`docs/superpowers/specs/2026-08-17-flappy-race-gameplay-b2-design.md` §4 끝에 "결과" 절을 더한다: 실제 커밋 범위(4개 레포), Step 3 표의 실측값, 그리고 계획과 달랐던 점.
