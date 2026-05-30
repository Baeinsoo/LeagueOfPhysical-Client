# World Core Slice 3: Damage/Death Event Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** End-to-end restoration of the client damage path on the Generation/Application architecture from `docs/world-core-connection-architecture.md` and `docs/superpowers/specs/2026-05-30-world-core-slice3-design.md`.

**Architecture:** Server's existing `DamageEventToC` is the input. A wire adapter (`GameDamageMessageHandler`) translates into core `WorldEvent` records appended to a `WorldEventBuffer`. The tick pipeline's `ProcessEvent` step drains the buffer: `WorldEventApplicator` writes core state (`HealthSystem.ApplyDamageDealt` sets `Health.Current`), then `WorldEventBridge` fans out to LOP's `EventBus.Default` so `DamageView`/`LOPEntityView` light up. `DeathEvent` is data-only in this slice (state no-op, log-only fan-out — no subscribers exist).

**Tech Stack:** C# 9 records, NUnit (Unity Test Framework, EditMode), VContainer DI, Mirror network messages, Unity 6 (`baegames.GameFramework.World` package with `noEngineReferences: true`).

---

## Repos Touched

Slice 3 spans **two git repos**, both already in the workspace as siblings:

| Repo | Path | Role | Branch for this slice |
|---|---|---|---|
| `GameFramework` | `C:\Users\re5na\workspace\LOP\GameFramework` | Shared package with server; `noEngineReferences` pure C# core | `feature/world-core-slice3` (to be created) |
| `LeagueOfPhysical-Client` | `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client` | Client Unity project; consumes GameFramework via `file:` package | `feature/world-core-slice3` (already created at commit `b19afdd`) |

Both repos get their own `feature/world-core-slice3` branch and their own `--no-ff` merge ceremony at the end (slice 1/2 pattern).

## File Structure

### GameFramework (core)

**New files** (`GameFramework/Runtime/Scripts/World/Events/` — new folder):
- `Events/WorldEvent.cs` — polymorphic base record
- `Events/DamageDealtEvent.cs` — damage result record
- `Events/DeathEvent.cs` — death record
- `Events/WorldEventBuffer.cs` — Append/Snapshot/Clear queue

**New file** (`GameFramework/Runtime/Scripts/World/Systems/`):
- `Systems/WorldEventApplicator.cs` — buffer → state-writer dispatch

**Modified files**:
- `Runtime/Scripts/World/Systems/HealthSystem.cs` — add `ApplyDamageDealt`

**Test files** (`GameFramework/Tests/World/` — flat, follows existing convention):
- `Tests/World/WorldEventBufferTests.cs` — new
- `Tests/World/WorldEventApplicatorTests.cs` — new
- `Tests/World/HealthSystemTests.cs` — extend with `ApplyDamageDealt` tests

### LeagueOfPhysical-Client (LOP)

**New file** (`Assets/Scripts/World/` — new folder):
- `Assets/Scripts/World/WorldEventBridge.cs` — buffer → EventBus fan-out

**Modified files**:
- `Assets/Scripts/Game/MessageHandler/Game.Damage.MessageHandler.cs` — restore `OnDamageEventToC` body as wire adapter
- `Assets/Scripts/Game/LOPGameEngine.cs` — add `[DIMonoBehaviour]` + `[Inject]` fields + `ProcessEvent` body
- `Assets/Scripts/Scene/SceneLifetimeScope.cs` — register `WorldEventBuffer`, `HealthSystem`, `WorldEventApplicator`, `WorldEventBridge` Singletons

---

## Task 1: Create `feature/world-core-slice3` branch on GameFramework

The client branch already exists (`b19afdd`). GameFramework is still on `main`.

**Files:** none (git only)

- [ ] **Step 1: Confirm clean GameFramework working tree**

Run from `C:\Users\re5na\workspace\LOP\GameFramework`:
```bash
git status --short
git branch --show-current
```
Expected: empty status output, current branch `main`. If anything dirty, surface it before branching — slice 3 work would inherit unrelated changes.

- [ ] **Step 2: Create the branch**

```bash
cd /c/Users/re5na/workspace/LOP/GameFramework
git switch -c feature/world-core-slice3
git branch --show-current
```
Expected: `Switched to a new branch 'feature/world-core-slice3'`, current branch `feature/world-core-slice3`.

No commit yet.

---

## Task 2: Add core event records (`WorldEvent`, `DamageDealtEvent`, `DeathEvent`)

Pure data records, no behavior — language semantics make TDD pointless. Just create.

**Files:**
- Create: `GameFramework/Runtime/Scripts/World/Events/WorldEvent.cs`
- Create: `GameFramework/Runtime/Scripts/World/Events/DamageDealtEvent.cs`
- Create: `GameFramework/Runtime/Scripts/World/Events/DeathEvent.cs`

- [ ] **Step 1: Create `WorldEvent.cs`**

Write `C:\Users\re5na\workspace\LOP\GameFramework\Runtime\Scripts\World\Events\WorldEvent.cs`:

```csharp
namespace GameFramework.World
{
    /// <summary>
    /// 모든 월드 이벤트의 폴리모픽 베이스. 불변 데이터 레코드.
    /// Generation이 만들고 Application이 상태에 쓰며 Bridge가 프레젠테이션으로 fan-out한다.
    /// Stage ④에서 tick 스탬프 + source 태그(Predicted/Confirmed)가 여기에 추가될 예정.
    /// </summary>
    public abstract record WorldEvent;
}
```

- [ ] **Step 2: Create `DamageDealtEvent.cs`**

```csharp
namespace GameFramework.World
{
    /// <summary>
    /// 데미지 적용 결과를 운반한다. attackerId/isCritical/isDodged는 코어 로직이
    /// 추론하지 않고 데이터로 통과시키는 패스 필드 (프레젠테이션이 사용).
    /// remaining/isDead는 적용 후 상태.
    /// </summary>
    public sealed record DamageDealtEvent(
        string targetId,
        string attackerId,
        int    amount,
        bool   isCritical,
        bool   isDodged,
        int    remaining,
        bool   isDead
    ) : WorldEvent;
}
```

- [ ] **Step 3: Create `DeathEvent.cs`**

```csharp
namespace GameFramework.World
{
    /// <summary>
    /// 사망 사건. 슬라이스 3에선 Application 측 상태 변경 없음 (Health.Current가 이미 0).
    /// Bridge는 슬라이스 3에서 구독자 없어 log-only. 사망 애니메이션·UI 구독은 별도 슬라이스.
    /// </summary>
    public sealed record DeathEvent(
        string victimId,
        string attackerId
    ) : WorldEvent;
}
```

- [ ] **Step 4: Verify compile via Unity client (pinned)**

Resolve the client instance id from `mcpforunity://instances` (the project's CLAUDE.md requires `unity_instance` per call; at session start it was `LeagueOfPhysical-Client@de70658b9450cbb4` but the hash can change — re-resolve each task).

```
refresh_unity(unity_instance="<client-id>", scope="scripts", compile="request", wait_for_ready=true)
read_console(unity_instance="<client-id>", types=["error","warning"], count="30", format="detailed")
```
Expected: zero new errors, zero new warnings. Records are C# 9; Unity 6 supports them natively — no `System.Runtime.CompilerServices.IsExternalInit` shim needed.

If a `CS0518: System.Runtime.CompilerServices.IsExternalInit is not defined` appears, the project is on a pre-C#-9 target. Workaround: add a one-line `IsExternalInit.cs` to the `World/` runtime folder:
```csharp
namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
```
But verify the error appears first — don't add preemptively.

- [ ] **Step 5: Commit on GameFramework branch**

```bash
cd /c/Users/re5na/workspace/LOP/GameFramework
git add Runtime/Scripts/World/Events/
git commit -m "feat(world): add WorldEvent base + DamageDealtEvent + DeathEvent records

Slice 3 building block. WorldEvent is the polymorphic base for all
events flowing through WorldEventBuffer. DamageDealtEvent carries
the full damage payload including pass-through presentation hints
(isCritical, isDodged) per the rich-data / narrow-system principle.
DeathEvent has no Application-side state in this slice — Health
already conveys death via Current == 0; the event exists for Bridge
fan-out (presentation, log-only for now).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

If `*.meta` files were auto-generated by Unity for the new `.cs` files, `git status` may show them as untracked — include them in the commit (`git add Runtime/Scripts/World/Events/`).

---

## Task 3: Add `WorldEventBuffer` with TDD

Polymorphic queue. Append/Snapshot/Clear/Count surface. Null guard on Append.

**Files:**
- Create: `GameFramework/Tests/World/WorldEventBufferTests.cs`
- Create: `GameFramework/Runtime/Scripts/World/Events/WorldEventBuffer.cs`

- [ ] **Step 1: Write the failing tests**

Write `C:\Users\re5na\workspace\LOP\GameFramework\Tests\World\WorldEventBufferTests.cs`:

```csharp
using NUnit.Framework;
using System;

namespace GameFramework.World.Tests
{
    public class WorldEventBufferTests
    {
        [Test]
        public void Empty_buffer_has_Count_zero_and_empty_snapshot()
        {
            var buffer = new WorldEventBuffer();

            Assert.AreEqual(0, buffer.Count);
            Assert.AreEqual(0, buffer.Snapshot.Count);
        }

        [Test]
        public void Append_increases_Count_and_appears_in_snapshot()
        {
            var buffer = new WorldEventBuffer();
            var e = new DamageDealtEvent("1", "2", 50, false, false, 50, false);

            buffer.Append(e);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(1, buffer.Snapshot.Count);
            Assert.AreSame(e, buffer.Snapshot[0]);
        }

        [Test]
        public void Multiple_Appends_preserve_insertion_order()
        {
            var buffer = new WorldEventBuffer();
            var a = new DamageDealtEvent("1", "2", 10, false, false, 90, false);
            var b = new DeathEvent("3", "2");
            var c = new DamageDealtEvent("3", "2", 20, false, false, 0, true);

            buffer.Append(a);
            buffer.Append(b);
            buffer.Append(c);

            Assert.AreEqual(3, buffer.Count);
            Assert.AreSame(a, buffer.Snapshot[0]);
            Assert.AreSame(b, buffer.Snapshot[1]);
            Assert.AreSame(c, buffer.Snapshot[2]);
        }

        [Test]
        public void Clear_empties_the_buffer()
        {
            var buffer = new WorldEventBuffer();
            buffer.Append(new DamageDealtEvent("1", "2", 50, false, false, 50, false));
            buffer.Append(new DeathEvent("1", "2"));

            buffer.Clear();

            Assert.AreEqual(0, buffer.Count);
            Assert.AreEqual(0, buffer.Snapshot.Count);
        }

        [Test]
        public void Append_after_Clear_works_normally()
        {
            var buffer = new WorldEventBuffer();
            buffer.Append(new DeathEvent("1", "2"));
            buffer.Clear();
            var e = new DamageDealtEvent("4", "5", 30, false, false, 70, false);

            buffer.Append(e);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreSame(e, buffer.Snapshot[0]);
        }

        [Test]
        public void Append_null_throws_ArgumentNullException()
        {
            var buffer = new WorldEventBuffer();

            Assert.Throws<ArgumentNullException>(() => buffer.Append(null));
        }
    }
}
```

- [ ] **Step 2: Run tests, verify they fail with compile error**

UnityMCP, pinned to client:
```
refresh_unity(unity_instance="<client-id>", scope="scripts", compile="request", wait_for_ready=true)
read_console(unity_instance="<client-id>", types=["error"], count="20", format="detailed")
```
Expected: compile error `CS0246: WorldEventBuffer not found` (because the test references it but `WorldEventBuffer.cs` doesn't exist yet). This is the failing-test signal in Unity's compile-then-test model.

- [ ] **Step 3: Implement `WorldEventBuffer.cs`**

Write `C:\Users\re5na\workspace\LOP\GameFramework\Runtime\Scripts\World\Events\WorldEventBuffer.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace GameFramework.World
{
    /// <summary>
    /// 단일 폴리모픽 이벤트 큐. Generation/와이어 어댑터가 Append, Application과 Bridge가
    /// 같은 Snapshot을 본 뒤 Clear. 비동기 네트워크 수신과 결정론적 틱 드레인 사이의 정렬 버퍼.
    /// 슬라이스 3은 commit gate 없이 매 드레인 = commit. Stage ④에서 tick 분리·dedupe 추가.
    /// </summary>
    public class WorldEventBuffer
    {
        private readonly List<WorldEvent> _events = new List<WorldEvent>();

        /// <summary>이벤트 1개 append. null은 ArgumentNullException.</summary>
        public void Append(WorldEvent e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            _events.Add(e);
        }

        /// <summary>현재 누적된 이벤트의 읽기 전용 뷰. 자체 mutation 없음.</summary>
        public IReadOnlyList<WorldEvent> Snapshot => _events;

        /// <summary>누적된 이벤트 전부 제거. 드레인 후 호출.</summary>
        public void Clear() => _events.Clear();

        /// <summary>현재 누적 개수.</summary>
        public int Count => _events.Count;
    }
}
```

- [ ] **Step 4: Run tests, verify they pass**

```
refresh_unity(unity_instance="<client-id>", scope="scripts", compile="request", wait_for_ready=true)
read_console(unity_instance="<client-id>", types=["error","warning"], count="20", format="detailed")
```
Expected: clean. Then run tests:
```
run_tests(unity_instance="<client-id>", mode="EditMode",
          assembly_names=["baegames.GameFramework.World.Tests"],
          include_failed_tests=true)
get_test_job(unity_instance="<client-id>", job_id="<returned-id>", wait_timeout=60, include_failed_tests=true)
```
Expected: total grows from slice 2's 48 to 54 (6 new tests added), all pass, `resultState: "Passed"`.

- [ ] **Step 5: Commit**

```bash
cd /c/Users/re5na/workspace/LOP/GameFramework
git add Runtime/Scripts/World/Events/WorldEventBuffer.cs Tests/World/WorldEventBufferTests.cs
git commit -m "feat(world): add WorldEventBuffer with append/snapshot/clear API

Single polymorphic queue used by Generation/wire-adapters to append
and by Application/Bridge to read snapshots. Slice 3 justification
is async-network to sync-tick alignment; Stage IV adds commit gate
semantics (tick stamps, predicted/confirmed dedupe) without
changing this API surface.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Extend `HealthSystem` with `ApplyDamageDealt` (TDD)

Pure rewrite of `Health.Current` from `DamageDealtEvent.remaining`. No decisions.

**Files:**
- Modify: `GameFramework/Tests/World/HealthSystemTests.cs`
- Modify: `GameFramework/Runtime/Scripts/World/Systems/HealthSystem.cs`

- [ ] **Step 1: Append failing tests to `HealthSystemTests.cs`**

Open `C:\Users\re5na\workspace\LOP\GameFramework\Tests\World\HealthSystemTests.cs`. Add these test methods at the end of the class, before the closing brace:

```csharp
        [Test]
        public void ApplyDamageDealt_writes_remaining_to_Current()
        {
            var health = new Health(100);
            var e = new DamageDealtEvent("1", "2", amount: 30, isCritical: false, isDodged: false, remaining: 70, isDead: false);

            _system.ApplyDamageDealt(health, e);

            Assert.AreEqual(70, health.Current);
        }

        [Test]
        public void ApplyDamageDealt_writes_zero_when_event_says_dead()
        {
            var health = new Health(100) { Current = 50 };
            var e = new DamageDealtEvent("1", "2", amount: 60, isCritical: true, isDodged: false, remaining: 0, isDead: true);

            _system.ApplyDamageDealt(health, e);

            Assert.AreEqual(0, health.Current);
        }

        [Test]
        public void ApplyDamageDealt_does_not_branch_on_isDodged_or_isCritical()
        {
            // Application 메서드는 결정 없음 — remaining을 그대로 씀. isDodged/isCritical은 무시.
            var health = new Health(100);
            var e = new DamageDealtEvent("1", "2", amount: 999, isCritical: true, isDodged: true, remaining: 42, isDead: false);

            _system.ApplyDamageDealt(health, e);

            Assert.AreEqual(42, health.Current);
        }
```

- [ ] **Step 2: Verify failing compile (method not yet defined)**

```
refresh_unity(unity_instance="<client-id>", scope="scripts", compile="request", wait_for_ready=true)
read_console(unity_instance="<client-id>", types=["error"], count="20", format="detailed")
```
Expected: `CS1061: HealthSystem does not contain a definition for ApplyDamageDealt`.

- [ ] **Step 3: Implement `ApplyDamageDealt` in `HealthSystem.cs`**

Open `C:\Users\re5na\workspace\LOP\GameFramework\Runtime\Scripts\World\Systems\HealthSystem.cs`. Add this method inside the `HealthSystem` class, after `SetMax`:

```csharp
        /// <summary>
        /// 데미지 결과 이벤트를 그대로 Health에 반영한다. 결정/계산/가드 없음 (Application 메서드).
        /// </summary>
        public void ApplyDamageDealt(Health health, DamageDealtEvent e)
        {
            health.Current = e.remaining;
        }
```

- [ ] **Step 4: Run tests, verify pass**

```
refresh_unity(unity_instance="<client-id>", scope="scripts", compile="request", wait_for_ready=true)
read_console(unity_instance="<client-id>", types=["error","warning"], count="20", format="detailed")
run_tests(unity_instance="<client-id>", mode="EditMode",
          assembly_names=["baegames.GameFramework.World.Tests"],
          include_failed_tests=true)
get_test_job(unity_instance="<client-id>", job_id="<returned-id>", wait_timeout=60, include_failed_tests=true)
```
Expected: total 57 tests (54 + 3 new), all pass.

- [ ] **Step 5: Commit**

```bash
cd /c/Users/re5na/workspace/LOP/GameFramework
git add Runtime/Scripts/World/Systems/HealthSystem.cs Tests/World/HealthSystemTests.cs
git commit -m "feat(world): add HealthSystem.ApplyDamageDealt application method

Writes event.remaining to Health.Current verbatim — no branches, no
guards, no calculations. The Application side of the
Generation/Application split. Tests assert the no-decision property
explicitly (isDodged/isCritical are ignored at this layer).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Add `WorldEventApplicator` with TDD

Dispatches buffer snapshot to per-type Application handlers. Missing-target safety, no-op for `DeathEvent`.

**Files:**
- Create: `GameFramework/Tests/World/WorldEventApplicatorTests.cs`
- Create: `GameFramework/Runtime/Scripts/World/Systems/WorldEventApplicator.cs`

- [ ] **Step 1: Write failing tests**

Write `C:\Users\re5na\workspace\LOP\GameFramework\Tests\World\WorldEventApplicatorTests.cs`:

```csharp
using NUnit.Framework;
using System.Collections.Generic;

namespace GameFramework.World.Tests
{
    public class WorldEventApplicatorTests
    {
        private EntityRegistry _registry;
        private HealthSystem _healthSystem;
        private WorldEventApplicator _applicator;

        [SetUp]
        public void SetUp()
        {
            _registry = new EntityRegistry();
            _healthSystem = new HealthSystem();
            _applicator = new WorldEventApplicator(_registry, _healthSystem);
        }

        [Test]
        public void Apply_DamageDealtEvent_writes_remaining_to_Health()
        {
            var entity = new Entity("1");
            entity.Add(new Health(100));
            _registry.Add(entity);

            var events = new List<WorldEvent> {
                new DamageDealtEvent("1", "2", 30, false, false, 70, false)
            };

            _applicator.Apply(events);

            Assert.AreEqual(70, entity.Get<Health>().Current);
        }

        [Test]
        public void Apply_DamageDealtEvent_for_unregistered_target_does_not_throw()
        {
            var events = new List<WorldEvent> {
                new DamageDealtEvent("missing-id", "2", 30, false, false, 70, false)
            };

            Assert.DoesNotThrow(() => _applicator.Apply(events));
        }

        [Test]
        public void Apply_DamageDealtEvent_for_entity_without_Health_does_not_throw()
        {
            var entity = new Entity("1");
            _registry.Add(entity);

            var events = new List<WorldEvent> {
                new DamageDealtEvent("1", "2", 30, false, false, 70, false)
            };

            Assert.DoesNotThrow(() => _applicator.Apply(events));
        }

        [Test]
        public void Apply_DeathEvent_is_noop_for_state()
        {
            var entity = new Entity("1");
            entity.Add(new Health(100) { Current = 0 });
            _registry.Add(entity);

            var events = new List<WorldEvent> { new DeathEvent("1", "2") };

            _applicator.Apply(events);

            Assert.AreEqual(0, entity.Get<Health>().Current);
            Assert.AreEqual(100, entity.Get<Health>().Max);
        }

        [Test]
        public void Apply_processes_events_in_snapshot_order()
        {
            var entity = new Entity("1");
            entity.Add(new Health(100));
            _registry.Add(entity);

            var events = new List<WorldEvent> {
                new DamageDealtEvent("1", "2", 30, false, false, 70, false),
                new DamageDealtEvent("1", "2", 20, false, false, 50, false),
                new DamageDealtEvent("1", "2", 50, false, false, 0,  true),
            };

            _applicator.Apply(events);

            Assert.AreEqual(0, entity.Get<Health>().Current);
        }
    }
}
```

Note on the missing-Entity API: `Entity` exposes `Get<T>()`; in `WorldEventApplicator`, prefer `entity.Get<Health>()` and null-check the result rather than `entity.Has<Health>()` then `entity.Get<Health>()` (one lookup instead of two). Confirm this from `GameFramework/Runtime/Scripts/World/Entity.cs` if the implementation differs.

- [ ] **Step 2: Verify failing compile**

```
refresh_unity(unity_instance="<client-id>", scope="scripts", compile="request", wait_for_ready=true)
read_console(unity_instance="<client-id>", types=["error"], count="20", format="detailed")
```
Expected: `CS0246: WorldEventApplicator not found`.

- [ ] **Step 3: Implement `WorldEventApplicator.cs`**

Write `C:\Users\re5na\workspace\LOP\GameFramework\Runtime\Scripts\World\Systems\WorldEventApplicator.cs`:

```csharp
using System.Collections.Generic;

namespace GameFramework.World
{
    /// <summary>
    /// WorldEventBuffer의 스냅샷을 받아 이벤트 타입별로 dispatch해 상태에 쓴다.
    /// EntityRegistry에 없는 targetId는 조용히 건너뛴다 (네트워크 race나 비등록 엔티티 대응).
    /// 슬라이스 3 처리 이벤트: DamageDealtEvent (→ HealthSystem.ApplyDamageDealt), DeathEvent (no-op).
    /// </summary>
    public class WorldEventApplicator
    {
        private readonly EntityRegistry _registry;
        private readonly HealthSystem _healthSystem;

        public WorldEventApplicator(EntityRegistry registry, HealthSystem healthSystem)
        {
            _registry = registry;
            _healthSystem = healthSystem;
        }

        public void Apply(IReadOnlyList<WorldEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case DamageDealtEvent dde:
                        ApplyDamageDealt(dde);
                        break;
                    case DeathEvent:
                        // 슬라이스 3: state 변화 없음. Stage ④에서 Death 컴포넌트가 생기면 여기서 처리.
                        break;
                }
            }
        }

        private void ApplyDamageDealt(DamageDealtEvent e)
        {
            var entity = _registry.Get(e.targetId);
            if (entity == null) return;

            var health = entity.Get<Health>();
            if (health == null) return;

            _healthSystem.ApplyDamageDealt(health, e);
        }
    }
}
```

If `Entity.Get<T>()` returns a non-nullable struct or otherwise can't be null-checked this way, fall back to:
```csharp
if (!entity.Has<Health>()) return;
var health = entity.Get<Health>();
```
Verify against `GameFramework/Runtime/Scripts/World/Entity.cs` before writing this fallback.

- [ ] **Step 4: Run tests, verify pass**

```
refresh_unity(unity_instance="<client-id>", scope="scripts", compile="request", wait_for_ready=true)
read_console(unity_instance="<client-id>", types=["error","warning"], count="20", format="detailed")
run_tests(unity_instance="<client-id>", mode="EditMode",
          assembly_names=["baegames.GameFramework.World.Tests"],
          include_failed_tests=true)
get_test_job(unity_instance="<client-id>", job_id="<returned-id>", wait_timeout=60, include_failed_tests=true)
```
Expected: total 62 tests (57 + 5 new), all pass.

- [ ] **Step 5: Commit**

```bash
cd /c/Users/re5na/workspace/LOP/GameFramework
git add Runtime/Scripts/World/Systems/WorldEventApplicator.cs Tests/World/WorldEventApplicatorTests.cs
git commit -m "feat(world): add WorldEventApplicator dispatching buffer to state writers

Drains a WorldEventBuffer snapshot and dispatches by event type to
the matching Application method on the appropriate system. Missing
target ids and entities without the required component are skipped
silently — they're expected race conditions, not bugs. DeathEvent
is a no-op for state in slice 3 (Health.Current already 0 conveys
death; Death component arrives in a future slice).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

This completes the GameFramework side. Branch tip now has 4 commits ahead of `main`.

---

## Task 6: LOP `WorldEventBridge`

Per-event-type fan-out to `EventBus.Default`. No tests (slice 2/3 judgment: thin glue, runtime-verified).

**Files:**
- Create: `Assets/Scripts/World/WorldEventBridge.cs` (new folder)

- [ ] **Step 1: Create the directory and the file**

Write `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client\Assets\Scripts\World\WorldEventBridge.cs`:

```csharp
using GameFramework;
using LOP.Event.Entity;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 스냅샷을 프레젠테이션 EventBus로 fan-out한다. 코어 상태는 안 만짐.
    /// 슬라이스 3 처리 이벤트:
    ///   DamageDealtEvent → EventBus.Publish(EntityDamage) → DamageView, LOPEntityView 소비
    ///   DeathEvent       → Debug.Log only (구독자 없음, future-proof 자리만 잡음)
    /// </summary>
    public class WorldEventBridge
    {
        public void FanOut(IReadOnlyList<GameFramework.World.WorldEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case GameFramework.World.DamageDealtEvent dde:
                        EventBus.Default.Publish(
                            EventTopic.EntityId<LOPEntity>(dde.targetId),
                            new EntityDamage(dde.isDodged, dde.isCritical, dde.amount, dde.remaining, dde.isDead)
                        );
                        break;
                    case GameFramework.World.DeathEvent de:
                        Debug.Log($"[World] Death entity {de.victimId} (killer={de.attackerId})");
                        break;
                }
            }
        }
    }
}
```

- [ ] **Step 2: Verify compile**

```
refresh_unity(unity_instance="<client-id>", scope="scripts", compile="request", wait_for_ready=true)
read_console(unity_instance="<client-id>", types=["error","warning"], count="20", format="detailed")
```
Expected: clean. `Assets/Scripts/World/` is a new folder under `Assets/`, no asmdef → falls into `Assembly-CSharp` (default), can reference `baegames.GameFramework.World` because that package's `autoReferenced: true`.

- [ ] **Step 3: Commit (client repo)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/World/WorldEventBridge.cs
git commit -m "feat(world): add WorldEventBridge fanning core events to EventBus

Drains a WorldEventBuffer snapshot and publishes per-event-type to
LOP's EventBus.Default. DamageDealtEvent maps to the existing
EntityDamage view event consumed by DamageView/LOPEntityView.
DeathEvent emits a Debug.Log only — slice 3 has no EntityDeath
subscribers; the case structure is there for the future slice that
adds death animation/SFX.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

Note: Unity may auto-generate `Assets/Scripts/World.meta` and `Assets/Scripts/World/WorldEventBridge.cs.meta`. Include both via `git add Assets/Scripts/World/` (recursive).

---

## Task 7: Refactor `GameDamageMessageHandler` as wire adapter

Stub `OnDamageEventToC` body becomes: translate `DamageEventToC` to core `DamageDealtEvent` (+ optional `DeathEvent`) and append to `WorldEventBuffer`.

**Files:**
- Modify: `Assets/Scripts/Game/MessageHandler/Game.Damage.MessageHandler.cs`

- [ ] **Step 1: Replace file contents**

Open `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client\Assets\Scripts\Game\MessageHandler\Game.Damage.MessageHandler.cs`. Replace the existing class body (`using` block, namespace, class) so the file becomes:

```csharp
using GameFramework;
using VContainer;

namespace LOP
{
    public class GameDamageMessageHandler : IGameMessageHandler
    {
        [Inject]
        private GameFramework.World.WorldEventBuffer worldEventBuffer;

        public void Register()
        {
            EventBus.Default.Subscribe<DamageEventToC>(nameof(IMessage), OnDamageEventToC);
        }

        public void Unregister()
        {
            EventBus.Default.Unsubscribe<DamageEventToC>(nameof(IMessage), OnDamageEventToC);
        }

        private void OnDamageEventToC(DamageEventToC msg)
        {
            // 와이어 → 코어 이벤트 변환 어댑터. 슬라이스 3에선 레거시 메시지를 코어 도메인으로 격리.
            worldEventBuffer.Append(new GameFramework.World.DamageDealtEvent(
                targetId:   msg.TargetId,
                attackerId: msg.AttackerId,
                amount:     (int)msg.Damage,
                isCritical: msg.IsCritical,
                isDodged:   msg.IsDodged,
                remaining:  (int)msg.RemainingHP,
                isDead:     msg.IsDead
            ));

            if (msg.IsDead)
            {
                worldEventBuffer.Append(new GameFramework.World.DeathEvent(
                    victimId:   msg.TargetId,
                    attackerId: msg.AttackerId
                ));
            }
        }
    }
}
```

Notes:
- Old `[Inject] private IGameEngine gameEngine;` removed (unused after refactor).
- Old commented-out body (`ServerStateReconciler` wait loop, direct `EventBus.Publish`) removed entirely — that flow is now Generation Phase 2/3 on the server.
- `(int)` casts intentional: `DamageEventToC.Damage`/`RemainingHP` are `long` on the wire, `Health.Current` is `int`. Practical HP scale (≤100000 per slice 1 memo) is well within int range.
- DI registration in `RoomLifetimeScope.cs:28` (`builder.Register<IGameMessageHandler, GameDamageMessageHandler>(Lifetime.Transient)`) is unchanged.

- [ ] **Step 2: Verify compile**

```
refresh_unity(unity_instance="<client-id>", scope="scripts", compile="request", wait_for_ready=true)
read_console(unity_instance="<client-id>", types=["error","warning"], count="20", format="detailed")
```
Expected: clean. If `DamageEventToC` field names differ (e.g. `Damage` vs `Amount`), fix the casts to match the actual protobuf-generated property names; resolve by reading `Assets/Scripts/Generated/Protobuf/` or wherever `DamageEventToC` is defined.

- [ ] **Step 3: Commit**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/MessageHandler/Game.Damage.MessageHandler.cs
git commit -m "feat(world): restore GameDamageMessageHandler as wire adapter to WorldEventBuffer

Stub body replaced. DamageEventToC now translates to a core
DamageDealtEvent (and, on death, an additional DeathEvent) appended
to WorldEventBuffer. The legacy ServerStateReconciler wait-loop and
direct EventBus.Publish are gone — those duties are now the server's
Generation pipeline and the client's WorldEventBridge respectively.
Wire format quarantine: the rest of the client code sees WorldEvent
records, not Mirror packet types.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: Wire `LOPGameEngine.ProcessEvent` to drain the buffer

Add `[DIMonoBehaviour]`, inject buffer/applicator/bridge, fill the empty `ProcessEvent` body.

**Files:**
- Modify: `Assets/Scripts/Game/LOPGameEngine.cs`

- [ ] **Step 1: Update file**

Open `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client\Assets\Scripts\Game\LOPGameEngine.cs`.

Add `using VContainer;` to the using block (after `using UnityEngine;`).

Add `[DIMonoBehaviour]` attribute to the class.

Add three `[Inject]` fields at the top of the class body.

Replace the empty `ProcessEvent()` method body with the drain logic.

After edits, the file should read:

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;
using LOP.Event.LOPGameEngine.Update;
using VContainer;

namespace LOP
{
    [DIMonoBehaviour]
    public class LOPGameEngine : GameEngineBase
    {
        [Inject] private GameFramework.World.WorldEventBuffer worldEventBuffer;
        [Inject] private GameFramework.World.WorldEventApplicator worldEventApplicator;
        [Inject] private WorldEventBridge worldEventBridge;

        public new LOPEntityManager entityManager => base.entityManager as LOPEntityManager;

        public override void UpdateEngine()
        {
            BeginUpdate();

            ProcessNetworkMessage();

            ProcessInput();

            InterpolateEntity();

            UpdateEntity();

            UpdateAI();

            SimulatePhysics();

            UpdateVisualEffect();

            ProcessEvent();

            EndUpdate();
        }

        private void BeginUpdate()
        {
            DispatchEvent<Begin>();
        }

        private void ProcessNetworkMessage()
        {

        }

        private void ProcessInput()
        {
            DispatchEvent<ProcessInput>();
        }

        private void InterpolateEntity()
        {
        }

        private void UpdateEntity()
        {
            DispatchEvent<BeforeEntityUpdate>();

            entityManager.UpdateEntities();

            DispatchEvent<AfterEntityUpdate>();
        }

        private void UpdateAI()
        {
        }

        private void SimulatePhysics()
        {
            DispatchEvent<BeforePhysicsSimulation>();

            Physics.Simulate((float)tickUpdater.interval);

            DispatchEvent<AfterPhysicsSimulation>();
        }

        private void UpdateVisualEffect()
        {
        }

        private void ProcessEvent()
        {
            // --- World Core — 슬라이스 3: 이벤트 버퍼 드레인 ---
            var snapshot = worldEventBuffer.Snapshot;
            if (snapshot.Count == 0) return;

            worldEventApplicator.Apply(snapshot);
            worldEventBridge.FanOut(snapshot);
            worldEventBuffer.Clear();
            // --- end World Core slice 3 ---
        }

        private void EndUpdate()
        {
            DispatchEvent<End>();

            entityManager.DestroyMarkedEntities();
        }
    }
}
```

- [ ] **Step 2: Verify compile**

```
refresh_unity(unity_instance="<client-id>", scope="scripts", compile="request", wait_for_ready=true)
read_console(unity_instance="<client-id>", types=["error","warning"], count="20", format="detailed")
```
Expected: clean.

- [ ] **Step 3: Commit**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/LOPGameEngine.cs
git commit -m "feat(world): drain WorldEventBuffer in LOPGameEngine.ProcessEvent

The previously empty ProcessEvent step is now the slice 3 pipeline
fixed point: snapshot the buffer, run WorldEventApplicator (state
writes), run WorldEventBridge (presentation fan-out), clear. The
empty-buffer fast-path early-returns to keep no-event ticks cheap.
LOPGameEngine becomes [DIMonoBehaviour] so SceneLifetimeScope's
attribute scan can inject the three new fields, matching the slice 2
pattern.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: Register new services in `SceneLifetimeScope`

Singletons for `WorldEventBuffer`, `HealthSystem`, `WorldEventApplicator`, `WorldEventBridge`.

**Files:**
- Modify: `Assets/Scripts/Scene/SceneLifetimeScope.cs`

- [ ] **Step 1: Update `Configure`**

Open `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client\Assets\Scripts\Scene\SceneLifetimeScope.cs`. Replace the `Configure` method body so the file reads:

```csharp
using GameFramework;
using UnityEngine.SceneManagement;
using VContainer.Unity;
using VContainer;

namespace LOP
{
    public class SceneLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.EntityRegistry>(Lifetime.Singleton);
            builder.Register<GameFramework.World.WorldEventBuffer>(Lifetime.Singleton);
            builder.Register<GameFramework.World.HealthSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.WorldEventApplicator>(Lifetime.Singleton);
            builder.Register<WorldEventBridge>(Lifetime.Singleton);
        }

        public static SceneLifetimeScope instance { get; private set; }

        public static void Inject(object obj)
        {
            instance.Container.Inject(obj);
        }

        public static T Resolve<T>()
        {
            return instance.Container.Resolve<T>();
        }

        protected override void Awake()
        {
            base.Awake();

            instance = this;

            var activeScene = SceneManager.GetActiveScene();

            var DIGameObjects = activeScene.FindGameObjectsWithAttribute<DIGameObjectAttribute>();
            foreach (var DIGameObject in DIGameObjects.OrEmpty())
            {
                Container.InjectGameObject(DIGameObject);
            }

            var DIMonoBehaviours = activeScene.FindComponentsWithAttribute<DIMonoBehaviourAttribute>();
            foreach (var DIMonoBehaviour in DIMonoBehaviours.OrEmpty())
            {
                Container.Inject(DIMonoBehaviour);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
```

Notes:
- `WorldEventApplicator` ctor takes `(EntityRegistry, HealthSystem)` — VContainer's ctor injection auto-resolves both since they're registered Singletons.
- `WorldEventBridge` has no ctor dependencies — registration is enough.
- `HealthSystem` is added here (newly DI-managed); previously it was used directly via `new HealthSystem()` in EditMode tests, which keeps working — VContainer singleton is for client runtime injection only.
- `RoomLifetimeScope : SceneLifetimeScope` calls `base.Configure(builder)` so it inherits all 5 registrations — no change to `RoomLifetimeScope` needed.

- [ ] **Step 2: Verify compile**

```
refresh_unity(unity_instance="<client-id>", scope="scripts", compile="request", wait_for_ready=true)
read_console(unity_instance="<client-id>", types=["error","warning"], count="20", format="detailed")
```
Expected: clean.

- [ ] **Step 3: Commit**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Scene/SceneLifetimeScope.cs
git commit -m "feat(world): register WorldEventBuffer/Applicator/Bridge + HealthSystem singletons

Wires up the slice 3 collaborators on the scene-level container.
RoomLifetimeScope inherits via base.Configure. WorldEventApplicator
takes (EntityRegistry, HealthSystem) — both already singletons so
VContainer's ctor injection resolves automatically.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 10: Runtime verification in Unity play mode

Same human-in-loop pattern as slices 1/2. Cannot be automated by the agent — user drives play mode and confirms.

**Files:** none.

- [ ] **Step 1: Pre-flight — confirm console clean and EditMode green**

Agent: re-run the EditMode suite to confirm no regression.

```
run_tests(unity_instance="<client-id>", mode="EditMode",
          assembly_names=["baegames.GameFramework.World.Tests"],
          include_failed_tests=true)
get_test_job(unity_instance="<client-id>", job_id="<returned-id>", wait_timeout=60, include_failed_tests=true)
```
Expected: total 62, all pass.

- [ ] **Step 2: Ask user to enter play mode in the Room scene**

Tell the user: "Unity 클라이언트에서 Room scene으로 진입 + 플레이 모드 시작 → 캐릭터 1마리에게 데미지를 인가해 주세요 (사망까지 가면 더 좋음). 끝나면 OK 한 마디로 알려주세요." Do NOT toggle play mode programmatically — slices 1/2 pattern is human-driven.

- [ ] **Step 3: After user says OK, capture console**

```
read_console(unity_instance="<client-id>", action="get", types=["all"], count="200", format="plain", filter_text="[World]")
read_console(unity_instance="<client-id>", action="get", types=["error"], count="50", format="detailed")
```

- [ ] **Step 4: Verify the success criteria**

Expected observations:

| 기대 | 의미 |
|---|---|
| `[World] Registered entity {id}` 라인들 출현 | 슬라이스 1 회귀 없음 — 캐릭터가 등록됨 |
| 데미지가 일어난 캐릭터에 대응하는 데미지 숫자 popup 화면에 출현 | `WorldEventBridge → EntityDamage → DamageView` 끝-끝 동작 |
| (사망 시) `[World] Death entity {id} (killer={id})` 라인 출현 | `DeathEvent`까지 Bridge에 도달 |
| (사망 + 디스폰 시) `[World] Unregistered entity {id}` 라인 출현 | 슬라이스 2 회귀 없음 — 디스폰 정리 동작 |
| 에러 0건 (NRE, ArgumentException 등) | DI 주입 정상 (`[DIMonoBehaviour]` 스캔 OK), EntityRegistry 룩업 안전 |
| Mirror 핸들러가 비동기로 발사돼도 데미지 popup 순서 결정론적 | 버퍼 → 틱 드레인 정렬 동작 |

`Registered`와 `Unregistered`는 슬라이스 1/2가 보장하는 동작, 슬라이스 3이 그걸 깨지 않았는지 확인용. `데미지 popup`이 슬라이스 3의 새 success criteria.

- [ ] **Step 5: Report observations to user**

Per superpowers:verification-before-completion — evidence before claims. Paste:
- 한 쌍의 `Registered`/`Unregistered` (있다면) + 데미지/사망 로그 라인.
- 에러 카운트 (0이면 그렇게).
- 화면에서 데미지 popup이 떴는지 (user-confirmed; agent can't see screen).

DO NOT claim slice 3 verified unless the user confirmed seeing the damage popup AND the console showed expected logs without errors.

---

## Task 11: Finishing-a-development-branch ceremony — both repos

After Task 10 passes, invoke `superpowers:finishing-a-development-branch` twice (once per repo). The skill handles test verification + 4-option menu + cleanup.

**Order**:
1. GameFramework first (it's the dependency)
2. Client second

Each merge: `--no-ff`, no push (slice 1/2 pattern).

Expected end state:
- GameFramework: `main` advances by ~4 commits (event records, buffer, HealthSystem extension, applicator) → `--no-ff` merge commit. `feature/world-core-slice3` deleted.
- Client: `main` advances by ~5 commits (doc updates, spec, plan, bridge, handler refactor, GameEngine, DI) → `--no-ff` merge commit. `feature/world-core-slice3` deleted.

Update memory `entity-world-core-rebuild.md` after both merges to record slice 3 completion + new tip SHAs + Stage ④ entry point.

---

## Self-Review

### Spec coverage

Skimming `docs/superpowers/specs/2026-05-30-world-core-slice3-design.md` and mapping to tasks:

| Spec section | Implemented in |
|---|---|
| Core: `WorldEvent` | Task 2 |
| Core: `DamageDealtEvent` | Task 2 |
| Core: `DeathEvent` | Task 2 |
| Core: `WorldEventBuffer` | Task 3 (TDD) |
| Core: `HealthSystem.ApplyDamageDealt` | Task 4 (TDD) |
| Core: `WorldEventApplicator` | Task 5 (TDD) |
| LOP: `WorldEventBridge` | Task 6 |
| LOP: `GameDamageMessageHandler` refactor | Task 7 |
| LOP: `LOPGameEngine.ProcessEvent` body | Task 8 |
| LOP: `SceneLifetimeScope` DI | Task 9 |
| EditMode tests for buffer/applicator/healthsystem | Tasks 3, 4, 5 |
| Runtime verification | Task 10 |
| Branching strategy (slice 1/2 parity) | Task 1, Task 11 |

All in. No gaps.

### Placeholder scan

No "TBD", no "TODO" beyond intentional Stage ④ marker comments in code. All test bodies are concrete code blocks. All commit messages are filled.

### Type consistency

- `DamageDealtEvent` fields used identically across:
  - Definition (Task 2): `(string targetId, string attackerId, int amount, bool isCritical, bool isDodged, int remaining, bool isDead)`
  - HealthSystem test (Task 4): same field order via named args
  - Applicator test (Task 5): same field order positional
  - Bridge (Task 6): reads `dde.isDodged`, `dde.isCritical`, `dde.amount`, `dde.remaining`, `dde.isDead`
  - Wire adapter (Task 7): named-arg constructor with same field names
- `DeathEvent` fields: `(string victimId, string attackerId)` — used identically in tests and bridge and adapter.
- `WorldEventBuffer` API: `Append(WorldEvent)`, `Snapshot { get; }` (`IReadOnlyList<WorldEvent>`), `Clear()`, `Count { get; }` — same across all callers.
- `HealthSystem.ApplyDamageDealt(Health, DamageDealtEvent)` — same signature across Task 4 test and Task 5 Applicator call.
- `WorldEventApplicator(EntityRegistry, HealthSystem)` ctor — matches `Configure` registration order in Task 9.

All consistent.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-30-world-core-slice3-implementation.md`. Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks, fast iteration. Good for ~10-task slices with TDD discipline.
2. **Inline Execution** — execute tasks in this session using `executing-plans`, batch execution with checkpoints. Faster turnaround but heavier on this conversation's context.

Which approach?
