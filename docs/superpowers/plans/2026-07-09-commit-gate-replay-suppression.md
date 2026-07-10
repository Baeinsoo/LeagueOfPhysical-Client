# 확정 게이트 — 재생 억제 (commit gate via replay suppression) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 내 캐릭 롤백 재생(replay)이 만드는 연출 이벤트(현재 발동 cue)가 화면으로 새어나가지 않도록, `WorldEventBuffer`에 억제 스코프를 추가하고 `Reconciler`의 손-회피를 정식 통로 + 억제로 대체한다.

**Architecture:** `WorldEventBuffer.Suppress()`가 `IDisposable` 스코프를 반환하고, 그 스코프 동안 `Append`는 no-op(depth 카운터로 중첩·예외 안전). `Reconciler`는 재생 루프를 `using (worldEventBuffer.Suppress())`로 감싸고, 발동 재현을 `AbilitySystem.TryActivate` 직접 호출에서 정식 통로 `AbilityActivator.TryActivate`로 바꾼다 — cue Append는 억제가 버린다. 억제는 동기 블록이라 "시간 창"이 아닌 "재생이 만든 Append"만 잡고, 서버 연출(루프 밖 Append)은 영향 없다.

**Tech Stack:** C# / Unity / VContainer(DI) / NUnit(EditMode). 순수 C# 코어는 `GameFramework` 패키지(`file:` 참조), 배선은 `LeagueOfPhysical-Client`.

## Global Constraints

- **2개 저장소:** Task 1 = `GameFramework` 패키지(`C:/Users/re5na/workspace/LOP/GameFramework`), Task 2 = `LeagueOfPhysical-Client`. **각 저장소에서 피처 브랜치 `feature/commit-gate-replay-suppression`** 로 작업·커밋(main 직접 커밋 금지). Client 브랜치는 이미 생성됨.
- **네임스페이스:** 코어 타입은 `GameFramework.World`. Client 파일은 `using GameFramework.World;` 없이 World 타입을 풀 네임스페이스로 한정(기존 컨벤션).
- **테스트:** 순수 C#는 EditMode(NUnit), `namespace GameFramework.World.Tests`, `new WorldEventBuffer()` 직접 생성 스타일(기존 `WorldEventBufferTests.cs` 답습).
- **서버 무변경:** 서버는 재생이 없어 `Suppress()`를 호출하지 않는다. 서버 코드/저장소 손대지 않음.
- **패키지 편집 후:** Unity가 재컴파일하도록 client 인스턴스에 `refresh_unity` 후 `read_console`로 컴파일 에러 확인. UnityMCP 호출은 **항상 `unity_instance`=client**(`mcpforunity://instances`에서 `LeagueOfPhysical-Client@<hash>` 해석) 명시.
- **주석:** 비자명한 의도만 짧게, 일상어로(프로젝트 주석 컨벤션).

---

### Task 1: `WorldEventBuffer.Suppress()` — 억제 스코프 (GameFramework, TDD)

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/World/Events/WorldEventBuffer.cs`
- Test: `C:/Users/re5na/workspace/LOP/GameFramework/Tests/World/WorldEventBufferTests.cs` (기존 파일에 테스트 추가)

**Interfaces:**
- Consumes: (없음)
- Produces:
  - `IDisposable WorldEventBuffer.Suppress()` — 반환된 스코프가 살아있는 동안 `Append(e)`는 무시된다(버림). `Dispose()`로 해제. 중첩 시 depth 카운트, 예외 시에도 `using`이 해제.
  - 기존 `Append`/`Snapshot`/`Clear`/`Count` 시그니처 불변.

- [ ] **Step 1: 실패 테스트 작성** — 기존 `WorldEventBufferTests.cs`의 마지막 `}`(클래스 닫기) 앞에 아래 6개 테스트를 추가한다.

```csharp
        [Test]
        public void Append_during_Suppress_is_ignored()
        {
            var buffer = new WorldEventBuffer();

            using (buffer.Suppress())
            {
                buffer.Append(new DeathEvent("1", "2"));
            }

            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void Append_after_Suppress_scope_works_normally()
        {
            var buffer = new WorldEventBuffer();

            using (buffer.Suppress()) { buffer.Append(new DeathEvent("1", "2")); }
            var e = new DamageDealtEvent("3", "4", 10, false, false);
            buffer.Append(e);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreSame(e, buffer.Snapshot[0]);
        }

        [Test]
        public void Events_appended_before_Suppress_are_preserved()
        {
            var buffer = new WorldEventBuffer();
            var a = new DamageDealtEvent("1", "2", 10, false, false);

            buffer.Append(a);
            using (buffer.Suppress()) { buffer.Append(new DeathEvent("9", "2")); }

            Assert.AreEqual(1, buffer.Count);
            Assert.AreSame(a, buffer.Snapshot[0]);
        }

        [Test]
        public void Suppress_is_released_even_if_scope_throws()
        {
            var buffer = new WorldEventBuffer();

            try
            {
                using (buffer.Suppress())
                {
                    buffer.Append(new DeathEvent("1", "2"));   // 무시됨
                    throw new InvalidOperationException("boom");
                }
            }
            catch (InvalidOperationException) { }

            var e = new DamageDealtEvent("3", "4", 5, false, false);
            buffer.Append(e);                                   // 해제됐으니 정상
            Assert.AreEqual(1, buffer.Count);
            Assert.AreSame(e, buffer.Snapshot[0]);
        }

        [Test]
        public void Nested_Suppress_stays_active_until_all_scopes_dispose()
        {
            var buffer = new WorldEventBuffer();

            var outer = buffer.Suppress();
            var inner = buffer.Suppress();
            inner.Dispose();
            buffer.Append(new DeathEvent("1", "2"));            // 아직 outer가 억제 중
            Assert.AreEqual(0, buffer.Count);

            outer.Dispose();
            var e = new DamageDealtEvent("3", "4", 5, false, false);
            buffer.Append(e);                                   // 모두 해제 → 정상
            Assert.AreEqual(1, buffer.Count);
            Assert.AreSame(e, buffer.Snapshot[0]);
        }

        [Test]
        public void Disposing_same_Suppress_scope_twice_does_not_double_decrement()
        {
            var buffer = new WorldEventBuffer();

            var outer = buffer.Suppress();
            var inner = buffer.Suppress();
            inner.Dispose();
            inner.Dispose();                                    // 두 번째는 무시돼야 함
            buffer.Append(new DeathEvent("1", "2"));            // outer가 여전히 억제 중
            Assert.AreEqual(0, buffer.Count);

            outer.Dispose();
            buffer.Append(new DamageDealtEvent("3", "4", 5, false, false));
            Assert.AreEqual(1, buffer.Count);
        }
```

- [ ] **Step 2: 테스트가 실패하는지 실행** — Unity Test Runner(Window > General > Test Runner > EditMode) 또는 MCP `run_tests(mode="EditMode", filter="WorldEventBufferTests", unity_instance="LeagueOfPhysical-Client@<hash>")`.
Expected: 컴파일 실패 — `'WorldEventBuffer' does not contain a definition for 'Suppress'`.

- [ ] **Step 3: 최소 구현** — `WorldEventBuffer.cs`를 아래로 교체한다(억제 depth + `Suppress()` + `Append` 가드 추가; 나머지 그대로).

```csharp
using System;
using System.Collections.Generic;

namespace GameFramework.World
{
    /// <summary>
    /// 단일 폴리모픽 이벤트 큐. Generation/와이어 어댑터가 Append, egress sink가 Snapshot을 본 뒤 Clear.
    /// 재생(확정 전 재시뮬) 구간은 Suppress() 스코프로 감싸 그 사이 Append를 버린다 — 이미 라이브에
    /// 방출된 연출이 재생 때 재발화하지 않도록(commit gate).
    /// </summary>
    public class WorldEventBuffer
    {
        private readonly List<WorldEvent> _events = new List<WorldEvent>();
        private int _suppressDepth;

        /// <summary>이벤트 1개 append. 억제 스코프 안이면 무시된다. null은 ArgumentNullException.</summary>
        public void Append(WorldEvent e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            if (_suppressDepth > 0) return;
            _events.Add(e);
        }

        /// <summary>이 스코프가 살아있는 동안 Append를 버린다. Dispose 시 해제(중첩 안전).</summary>
        public IDisposable Suppress() => new SuppressScope(this);

        /// <summary>현재 누적된 이벤트의 읽기 전용 뷰. 자체 mutation 없음.</summary>
        public IReadOnlyList<WorldEvent> Snapshot => _events;

        /// <summary>누적된 이벤트 전부 제거. 드레인 후 호출.</summary>
        public void Clear() => _events.Clear();

        /// <summary>현재 누적 개수.</summary>
        public int Count => _events.Count;

        private sealed class SuppressScope : IDisposable
        {
            private WorldEventBuffer _buffer;

            public SuppressScope(WorldEventBuffer buffer)
            {
                _buffer = buffer;
                _buffer._suppressDepth++;
            }

            public void Dispose()
            {
                if (_buffer == null) return;   // 이중 Dispose 무시
                _buffer._suppressDepth--;
                _buffer = null;
            }
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인** — Step 2와 동일하게 실행.
Expected: 기존 6개 + 신규 6개 모두 PASS.

- [ ] **Step 5: 커밋** (GameFramework 저장소)

```bash
cd /c/Users/re5na/workspace/LOP/GameFramework
git checkout -b feature/commit-gate-replay-suppression
git add Runtime/Scripts/World/Events/WorldEventBuffer.cs Tests/World/WorldEventBufferTests.cs
git commit -m "feat(world): WorldEventBuffer.Suppress() 억제 스코프 (재생 commit gate)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: `Reconciler` — 재생 루프 억제 + 정식 통로 사용 (Client, 배선)

**Files:**
- Modify: `Assets/Scripts/Entity/Reconciler.cs`

**Interfaces:**
- Consumes:
  - `WorldEventBuffer.Suppress()` (Task 1)
  - `AbilityActivator.TryActivate(string casterEntityId, int abilityId, long currentTick) → bool` (기존)
  - `GameFramework.World.Entity.Id → string` (기존)
- Produces: (없음 — 내부 배선 변경, 외부 시그니처 불변)

> **주의:** `Reconciler`는 Unity 의존(`Physics`, `[Inject]`)이라 EditMode 단위테스트 불가. 억제 **정확성**은 Task 1이 이미 증명한다. Task 2는 **컴파일 + 플레이 검증**으로 확인한다.

- [ ] **Step 1: 주입 필드 교체** — `Reconciler.cs` 상단 `[Inject]` 블록에서 `abilityDataProvider`(전환 후 미사용)를 제거하고 `worldEventBuffer`·`abilityActivator`를 추가한다.

제거:
```csharp
        [Inject] private AbilityDataProvider abilityDataProvider;
```
추가(예: `entityRegistry` 줄 아래):
```csharp
        [Inject] private GameFramework.World.WorldEventBuffer worldEventBuffer;
        [Inject] private AbilityActivator abilityActivator;
```

- [ ] **Step 2: 재생 루프를 억제 스코프로 감싸고 발동 재현을 정식 통로로 교체** — 현재 `Reconciler.cs`의 재생 준비/루프(입력 버퍼 획득 ~ 루프 끝, 대략 118–157행)를 아래로 교체한다.

```csharp
            // 재생: 이미 예측했던 과거 틱(anchor+1 ~ currentTick-1)을 이동+물리로 재구성.
            var inputBuffer = worldEntity.Get<InputBuffer>();   // 입력 버퍼 (WorldEventBuffer 아님 — 이름 구분)
            if (inputBuffer == null)
            {
                return;
            }
            int layerMask = LayerMask.GetMask("Default");

            // 재생이 만든 연출 이벤트(cue 등)는 이미 라이브 때 방출됐으므로 버린다.
            using (worldEventBuffer.Suppress())
            {
                for (long t = anchorTick + 1; t < currentTick; t++)
                {
                    var cmd = inputHistory.TryGet(t, out var recorded) ? recorded : null;
                    inputBuffer.Current = cmd;

                    // 발동 재현: 라이브와 같은 정식 통로. cue Append는 위 억제 스코프가 버린다.
                    if (cmd != null && cmd.AbilityId != 0)
                    {
                        abilityActivator.TryActivate(worldEntity.Id, cmd.AbilityId, t);
                    }

                    movementSystem.Tick(worldEntity, t, deltaTime);
                    abilitySystem.Tick(worldEntity, t);
                    statusEffectSystem.Tick(worldEntity, t);
                    abilityEffectExecutor.DriveActiveEntity(worldEntity, entityManager, t);

                    // 재생: 서버 MoveCharacters와 동일한 키네마틱 한 틱.
                    Physics.SyncTransforms();
                    entity.GetEntityComponent<PhysicsComponent>().Depenetrate(layerMask);
                    kinematicMoveSystem.Tick(worldEntity, deltaTime);
                    entity.PushMotionToPhysics();

                    // 보정값으로 두 히스토리 갱신(다음 비교/재생이 stale값을 안 보도록).
                    var transform = worldEntity.Get<GameFramework.World.Transform>();
                    var velocity = worldEntity.Get<GameFramework.World.Velocity>();
                    snapshotHistory.Record(new GameFramework.Netcode.EntitySnapshot(
                        t, transform.Position, transform.Rotation, velocity.Linear));
                    predictedAbilityStateHistory.Record(t, PredictedAbilityState.Capture(worldEntity));
                }
            }
```

> 교체 시 기존의 `abilityDataProvider.TryGet(...)` 가드와 `abilitySystem.TryActivate(worldEntity, data, worldEntity, t)` 직접 호출, 그리고 그 위의 "AbilityActivator가 아니라 …" 설명 주석 블록이 사라진다(정식 통로가 조회·발동·cue를 담당). `abilitySystem`은 `abilitySystem.Tick`에 여전히 쓰이므로 주입 유지.

- [ ] **Step 3: 컴파일 확인** — client 인스턴스 `refresh_unity` 후 `read_console`.
Run(MCP): `refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")` → `read_console(types=["error"], unity_instance="LeagueOfPhysical-Client@<hash>")`.
Expected: 컴파일 에러 0. (특히 `abilityDataProvider` 미사용 경고/에러 없음, `AbilityActivator`·`WorldEventBuffer` 주입 해석됨.)

- [ ] **Step 4: 플레이 검증** — 클라·서버 에디터 실행 → 룸 접속 → 스킬(cue 있는 어빌리티, 예: 공격) 사용하며 이동해 롤백을 유발한다. LatencySimulation으로 RTT를 키우면(예: 150ms) 재생이 자주 돈다.
Expected(육안): 스킬 발동 연출이 **한 번만** 재생된다(재생 시 유령 중복 없음). 이동/대시 위치는 기존과 동일하게 보정된다(재생의 발동 계산은 정상). 콘솔 예외 없음.

> 관찰 팁: 필요하면 `WorldEventSink.Emit`의 `AbilityActivated` publish 지점에 임시 로그를 넣어 발동당 1회만 나가는지 확인 후 제거.

- [ ] **Step 5: 커밋** (Client 저장소)

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Entity/Reconciler.cs
git commit -m "refactor(netcode): 재생 cue 손-회피 제거 — WorldEventBuffer.Suppress() + 정식 통로

Reconciler 재생 루프를 worldEventBuffer.Suppress()로 감싸고, 발동 재현을
AbilitySystem.TryActivate 직접 호출에서 AbilityActivator.TryActivate로 교체.
라이브/재생 발동 경로가 하나로 합쳐지고, cue 중복 방출은 억제가 처리.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: spec 진행 갱신 + 브랜치 마무리

**Files:**
- Modify: `docs/superpowers/specs/2026-07-09-commit-gate-replay-suppression-design.md` (진행 체크박스)

- [ ] **Step 1: spec 진행 체크** — "사용자 spec 리뷰"·"`writing-plans`"·구현 항목 체크, 필요 시 "구현 완료(Task 1/2)" 한 줄 추가.

- [ ] **Step 2: 커밋 + 두 저장소 main 머지** — 각 저장소에서 `feature/commit-gate-replay-suppression`를 `--no-ff`로 main에 머지(플레이 검증 통과 후). GameFramework 먼저(패키지), Client 나중.

```bash
# GameFramework
cd /c/Users/re5na/workspace/LOP/GameFramework
git checkout main && git merge --no-ff feature/commit-gate-replay-suppression -m "Merge feature/commit-gate-replay-suppression: WorldEventBuffer.Suppress() 억제 스코프"
# Client (spec 진행 커밋 포함)
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add docs/superpowers/specs/2026-07-09-commit-gate-replay-suppression-design.md
git commit -m "docs(spec): 재생 억제 구현 완료 표시"
git checkout main && git merge --no-ff feature/commit-gate-replay-suppression -m "Merge feature/commit-gate-replay-suppression: 재생 cue 억제 (commit gate 방식 1)"
```

> **주의:** Client working tree의 기존 변경(`Assets/Art`, `UIRoot.prefab`, `GraphicsSettings.asset`)은 이번 작업과 무관 — `git add`에 포함하지 말 것.

- [ ] **Step 3: ROADMAP 갱신** — 손대는 김에 `docs/ROADMAP.md`에서 이 슬라이스를 **Next #1 → 한 일(Done ledger)** 로 이동(07-09 줄 추가). Task 3 머지 커밋에 포함하거나 별도 docs 커밋.

---

## Self-Review

**1. Spec coverage:**
- 방식 1 억제 스코프(`WorldEventBuffer.Suppress()`) → Task 1 ✓
- `Reconciler` 손-회피 제거 + 정식 통로(`AbilityActivator`) → Task 2 ✓
- EditMode 테스트(억제/보존/예외안전/중첩) → Task 1 Step 1의 6개 테스트 ✓ (사용자 우려 "재생 밖 서버 연출 보존" = `Events_appended_before_Suppress_are_preserved` + `Append_after_Suppress_scope_works_normally`)
- 플레이 검증(cue 1회) → Task 2 Step 4 ✓
- 서버 무변경 → 어느 태스크도 서버 저장소 안 건드림 ✓
- Out of scope(방식3/틱도장/통합 fan-out/틱가드) → 태스크 없음(의도적) ✓

**2. Placeholder scan:** `<hash>`는 런타임 해석 값(플레이스홀더 아님, 컨벤션대로 명시). 코드 스텝은 전부 완전한 코드. TODO/TBD 없음. ✓

**3. Type consistency:**
- `Suppress()` 반환 `IDisposable` — Task 1 정의, Task 2에서 `using (worldEventBuffer.Suppress())` 사용 일치 ✓
- `AbilityActivator.TryActivate(string, int, long)` — Task 2가 `worldEntity.Id`(string), `cmd.AbilityId`(int), `t`(long)로 호출, 시그니처 일치 ✓
- `abilityDataProvider` 제거 후 Task 2 코드에서 미참조 ✓
- `inputBuffer`(로컬, `InputBuffer`) vs `worldEventBuffer`(필드) 이름 구분 명시 ✓

이상 없음.

## Execution Handoff

(작성자가 실행 방식 선택을 사용자에게 제시)
