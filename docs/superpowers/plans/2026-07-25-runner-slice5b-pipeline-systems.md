# Runner Slice 5-B — 인라인 파이프라인 스텝 → ITickSystem (god-object 해체)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`).

**Goal:** `LOPRunner.UpdateRunner`의 인라인 스텝들을 각각 `ITickSystem`(5-A registry)으로 추출해, `LOPRunner`를 얇은 오케스트레이터로 만든다. 빈 no-op 삭제. **로직 변경 0, 순서 정확 재현.**

**Architecture:** 정적 스텝 = 주입된 `ITickSystem`을 `UpdateRunner`에서 **순서대로 직접 `system.Tick()` 호출**(순서가 코드에 명시 — 넷코드 리뷰 유리). 동적 훅(ProcessInput/End/Begin)만 `RunPhase`. `world.Tick`은 sim 코어라 별도 인라인 유지. 각 시스템은 자기 저수준 deps를 ctor 주입 → `LOPRunner`의 `[Inject]`가 12개(저수준)에서 ~6(클)/~8(서) 시스템으로 줄어 god-object 해소.

**Tech Stack:** Unity, C#, VContainer, GameFramework `ITickSystem`(이미 존재).

## Global Constraints

- **로직 불변**: 각 스텝의 코드를 그대로 새 클래스 `Tick`으로 이동(verbatim). `tickUpdater.tick`→`tick` 파라미터, `(float)tickUpdater.interval`→`deltaTime` 파라미터로만 치환(정적 스텝은 `Tick(tick, interval)`로 호출됨 — deltaTime 파라미터에 interval이 들어감, 고정 틱 델타라 의미 동일).
- **순서 불변식(넷코드) 정확 재현** — 아래 UpdateRunner 최종 순서 그대로. 특히: 입력→world.Tick / deaths→eventDrain(버퍼 clear) / End 훅→스냅샷 브로드캐스트 / despawn 맨 마지막.
- **GF 변경 0** (ITickSystem은 5-A서 이미 존재). 5-B는 **클라·서버만**.
- **커밋 스테이징 명시 파일만**. `git add -A`/`.` 금지. **서버 픽스처**(`DefaultVolumeProfile.asset`, `ConfigureRoomComponent.cs`, `GameRuleSystem.cs`) 커밋 금지.
- **새 단위 테스트 없음**(순수 배선 이동) — 검증 = 컴파일 + GF EditMode + 머지 후 플레이 스모크(넷코드 필수).
- 클라 코드는 워크트리서 컴파일 불가(에디터 main 바인딩) → 서버 먼저 실제 검증, 클라는 정적 + 머지 게이트.

## 추출 패턴 (정적 direct-call 시스템)

새 파일 `Assets/Scripts/Game/TickSystems/{Name}.cs` (양쪽 레포 각자):
```csharp
namespace LOP
{
    public class ReconcileSystem : GameFramework.ITickSystem
    {
        private readonly Reconciler reconciler;                 // 이 스텝이 쓰던 deps만 ctor 주입
        public ReconcileSystem(Reconciler reconciler) { this.reconciler = reconciler; }

        public void Tick(long tick, float deltaTime)
        {
            reconciler.Reconcile(tick, deltaTime);              // 구 LOPRunner 코드 그대로 이동
        }
    }
}
```
- 시스템은 `tickUpdater`를 주입받지 않는다 — `tick`/`deltaTime`는 `Tick` 파라미터로 받음.
- DI 등록: `GameLifetimeScope.Configure`에 `builder.Register<ReconcileSystem>(Lifetime.Singleton);`. LOPRunner가 `[Inject]`로 받아 생성됨(별도 eager-resolve 불필요 — LOPRunner 의존이 트리거).
- LOPRunner: `[Inject] private ReconcileSystem reconcileSystem;` 추가, `UpdateRunner`에서 `reconcileSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);`로 호출.

---

## Repos & Branches (생성됨)

- Server `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server` — `refactor/runner-slice5b` (base 0a7f8ca)
- Client — 이 워크트리 `worktree-slice5b`(로컬 main 097e9ee 리셋됨). GF 변경 없음.

Unity: 클라 `LeagueOfPhysical-Client@de70658b9450cbb4`, 서버 `LeagueOfPhysical-Server@f99391fa2dbaaf3c`.

---

### Task 1: 서버 파이프라인 시스템 추출 (검증 가능한 쪽 먼저)

**File source:** `Assets/Scripts/Game/LOPRunner.cs`. 각 스텝의 현재 코드를 읽어 새 시스템 `Tick`으로 verbatim 이동.

**추출 시스템 (`Assets/Scripts/Game/TickSystems/`):**

| 시스템 | 출처 메서드 | ctor deps | 비고 |
|---|---|---|---|
| `ServerInputSystem` | `ProcessInput` | `entityRegistry`, `inputBufferSystem`, `abilityActivator`, `sessionManager` | tick 사용 |
| `PhysicsSimulationSystem` | `SimulatePhysics` | `entityRegistry`, `motionBridge`, `physicsSimulator` | deltaTime(=interval) |
| `DeathResolveSystem` | `ProcessDeaths` | `worldEventBuffer`, `deathCascade` | |
| `WorldEventDrainSystem` | `ProcessEvent` | `worldEventBuffer`, `eventSink` | |
| `InputTimingFeedbackSystem` | `SendInputTimingFeedback` (+const `InputTimingFeedbackIntervalTicks=15`) | `entityRegistry`, `sessionManager` | tick%15 스로틀 |
| `EntitySnapshotBroadcastSystem` | `EndUpdate` L288–322 + `BuildAllEntitySnaps` (+const `MaxEntityBytesPerMessage=1000`) | `entityRegistry`, `sessionManager` | tick 사용 |
| `UserEntitySnapshotSystem` | `EndUpdate` L324–361 | `sessionManager`, `entitySpawner`, `entityRegistry` | |
| `DespawnFlushSystem` | `EndUpdate` L363 (`entitySpawner.FlushDespawns()`) | `entitySpawner` | 맨 마지막 |

- [ ] **Step 1:** 위 8개 시스템 클래스 생성(코드 verbatim 이동, `tickUpdater.tick`→`tick`, `(float)tickUpdater.interval`→`deltaTime`).
- [ ] **Step 2:** `GameLifetimeScope.Configure`에 8개 `builder.Register<X>(Lifetime.Singleton);` 추가.
- [ ] **Step 3:** `LOPRunner.cs` 수정:
  - 8개 시스템 `[Inject]` 추가.
  - 빈 no-op 메서드 삭제: `ProcessNetworkMessage`, `UpdateEntity`, `UpdateAI`.
  - 추출된 private 메서드(`ProcessInput`/`SimulatePhysics`/`ProcessDeaths`/`ProcessEvent`/`SendInputTimingFeedback`/`EndUpdate`/`BuildAllEntitySnaps`) + 관련 const 삭제.
  - 이동으로 더는 안 쓰는 `[Inject]` 삭제(`sessionManager`/`abilityActivator`/`worldEventBuffer`/`eventSink`/`deathCascade`/`entityRegistry`/`motionBridge`/`physicsSimulator`/`inputBufferSystem`/`entitySpawner` — **단, Init/Deinit/UpdateRunner에 잔여 사용 있으면 유지**. `world`/`mapLoader`/`gameRuleSystem`/`networkTimeSource`는 유지). grep로 각 dep 잔여 사용 확인 후 삭제.
  - `UpdateRunner()` 본문을 아래 **정확한 순서**로 교체:
    ```csharp
    RunPhase<Begin>(tickUpdater.tick, (float)tickUpdater.deltaTime);
    serverInputSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
    world.Tick(tickUpdater.tick, (float)tickUpdater.interval);
    physicsSimulationSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
    deathResolveSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
    worldEventDrainSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
    inputTimingFeedbackSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
    RunPhase<End>(tickUpdater.tick, (float)tickUpdater.deltaTime);
    entitySnapshotBroadcastSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
    userEntitySnapshotSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
    despawnFlushSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
    ```
- [ ] **Step 4:** 서버 컴파일 검증 — `refresh_unity(mode="force", scope="all", compile="request", unity_instance="...Server...")` → `read_console(types=["error"])` 0. (새 .cs .meta 생성됨.)
- [ ] **Step 5:** GF EditMode 무회귀 — `run_tests(mode="EditMode", assembly_names=["baegames.GameFramework.Runtime.Tests"], unity_instance="...Client...")` PASS.
- [ ] **Step 6:** 커밋 (Server) — 새 8개 `.cs`+`.meta`, `LOPRunner.cs`, `GameLifetimeScope.cs`만. `git status --short`로 픽스처 3개 미스테이징 확인. Co-Authored-By 트레일러.

---

### Task 2: 클라 파이프라인 시스템 추출

**추출 시스템 (`Assets/Scripts/Game/TickSystems/`):**

| 시스템 | 출처 | ctor deps | 비고 |
|---|---|---|---|
| `ReconcileSystem` | inline `reconciler.Reconcile` (UpdateRunner L98) | `reconciler` | tick+deltaTime(=interval) |
| `PhysicsSimulationSystem` | `SimulatePhysics` | `entityRegistry`, `motionBridge`, `physicsSimulator` | deltaTime(=interval) |
| `WorldEventDrainSystem` | `ProcessEvent` | `worldEventBuffer`, `eventSink` | |
| `LocalSnapshotSystem` | `RecordLocalSnapshot` | `playerContext`, `entityRegistry`, `snapshotHistory`, `predictedAbilityStateHistory` | tick 사용. `IPlayerContext` 등록 provider 확인(GameLifetimeScope 밖일 수 있음 — grep) |
| `DespawnFlushSystem` | `EndUpdate` 내 `entitySpawner.FlushDespawns()` | `entitySpawner` | 맨 마지막 |

- [ ] **Step 1:** 위 5개 시스템 생성(코드 verbatim 이동).
- [ ] **Step 2:** 클라 `GameLifetimeScope.Configure`에 5개 `Register<X>(Lifetime.Singleton)` 추가.
- [ ] **Step 3:** 클라 `LOPRunner.cs` 수정:
  - 5개 시스템 `[Inject]` 추가.
  - 빈 no-op 삭제: `ProcessNetworkMessage`, `InterpolateEntity`, `UpdateAI`, `UpdateVisualEffect`.
  - 추출된 메서드(`SimulatePhysics`/`ProcessEvent`/`RecordLocalSnapshot`/`EndUpdate`) 삭제(`ProcessInput`은 RunPhase 래퍼라 인라인化).
  - 안 쓰는 `[Inject]` 삭제(`worldEventBuffer`/`eventSink`/`physicsSimulator`/`entityRegistry`/`motionBridge`/`snapshotHistory`/`predictedAbilityStateHistory`/`reconciler`/`entitySpawner`/`playerContext` — 잔여 사용 grep 확인. `world`/`mapLoader`/`networkTimeSource` 유지).
  - `UpdateRunner()` 본문 → **정확한 순서**:
    ```csharp
    reconcileSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
    RunPhase<ProcessInput>(tickUpdater.tick, (float)tickUpdater.deltaTime);
    world.Tick(tickUpdater.tick, (float)tickUpdater.interval);
    physicsSimulationSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
    worldEventDrainSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
    localSnapshotSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
    RunPhase<End>(tickUpdater.tick, (float)tickUpdater.deltaTime);
    despawnFlushSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
    ```
- [ ] **Step 4:** 컴파일 검증 — 클라 `refresh_unity(scope=all force, ...client...)` → `read_console` (⚠️ 워크트리 클라 편집은 컴파일 안 됨 → 서버 Task1 그린 + 정적 grep으로 대신, 실제 클라는 머지 후 게이트). 서버 에디터도 `read_console`로 무영향 확인.
- [ ] **Step 5:** 커밋 (Client 워크트리) — 새 5개 `.cs`+`.meta`, `LOPRunner.cs`, `GameLifetimeScope.cs`.

---

## 완료 후

- 최종 whole-branch 리뷰(2-레포 — 특히 **순서 불변식 재현** 정밀 검토). `finishing-a-development-branch` 로컬 `--no-ff` 머지 → 머지 후 main 클라 에디터 컴파일(scope=all force) + GF EditMode + **플레이 스모크(넷코드 필수)**: 이동·전투·AI·스냅샷·매치종료 정상 = 파이프라인 순서 보존 확인. 서버 픽스처 보존.

## 스코프 밖

- 클·서 중복 시스템(`PhysicsSimulationSystem`/`WorldEventDrainSystem`) LOP-Shared 공유화 = 후속(선택). 네이밍(마커 구조체·페이즈명)=슬라이스 6.
