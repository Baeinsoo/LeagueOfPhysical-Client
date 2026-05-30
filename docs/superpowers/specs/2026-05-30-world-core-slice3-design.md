# World Core Slice 3: Damage/Death Event Pipeline Design

**Date:** 2026-05-30
**Branch:** `feature/world-core-slice3`
**Related:** [Connection Architecture](../../world-core-connection-architecture.md) · [Entity System Design](../../entity-system-design.md) · [Slice 1 Plan](../plans/2026-05-28-world-core-migration-slice-1-walking-skeleton.md) · [Slice 2 Plan](../plans/2026-05-29-world-core-slice2-entity-registry-cleanup.md)

## Goal

서버 `DamageEventToC` 메시지를 입구로 하는 **데미지 → 사망 경로 end-to-end 복구**를 Generation/Application 분리 아키텍처 위에 구현한다. 현재 `OnDamageEventToC`가 `return;` 스텁이라 클라 데미지 기능 전체가 죽어 있음. 슬라이스 3이 이를 새 구조로 살린다.

슬라이스 3은 **server-authoritative 모드**: 서버가 Generation(Phase 1+2+3)을 모두 수행해 결과 이벤트를 전송, 클라는 Application(쓰기) + Bridge(프레젠테이션 fan-out)만 수행. 클라 측 Generation, 예측, commit gate 본격 구현은 Stage ④로 미룬다.

## Architecture (1-pager)

```
[서버]
  게임 로직 → DamageEventToC 메시지 발사 (Mirror 네트워크 — 슬라이스 3 기준 wire format은 레거시 유지)

──────────────── 네트워크 경계 ────────────────

[클라] LOPGameEngine.UpdateEngine() 한 틱:

  ProcessNetworkMessage   ┐
  ProcessInput            │  Mirror 핸들러가 비동기로 임의 시점에 발사 →
  InterpolateEntity       │  GameDamageMessageHandler가 와이어 어댑터 역할:
  UpdateEntity            │    DamageEventToC → DamageDealtEvent (+ if isDead → DeathEvent)
  UpdateAI                │    WorldEventBuffer.Append(...)
  SimulatePhysics         │
  UpdateVisualEffect      │
  ProcessEvent ◀──────────┘  ← 새 드레인 스텝 (현재 본문 비어있음)
    1) snapshot = buffer.Snapshot()
    2) WorldEventApplicator.Apply(snapshot)
         · DamageDealtEvent → HealthSystem.ApplyDamageDealt → Health.Current = e.remaining
         · DeathEvent       → no-op (Health.Current 이미 0)
    3) WorldEventBridge.FanOut(snapshot)
         · DamageDealtEvent → EventBus.Publish(EntityDamage(...))
                              → DamageView 데미지 숫자 popup, LOPEntityView 피격 효과
         · DeathEvent       → Debug.Log("[World] Death entity {id}") (구독자 없음, future-proof)
    4) buffer.Clear()
  EndUpdate
```

핵심 디자인 결정 (브레인스토밍에서 합의):

- **버퍼는 `GameFramework.World` 안**(코어 측). Generation/Wire/Application 모두 같은 폴리모픽 큐.
- **Application 결정 없음**: `Health.Current = e.remaining` 같은 그대로-쓰기. `if (isDead)` 같은 가드 X.
- **commit gate는 trivial**: 매 드레인 = commit. Stage ④에서 tick stamp + 예측/확정 분리 + dedupe 추가.
- **버퍼의 슬라이스 3 정당화 = async ↔ sync 정렬**: Mirror 핸들러가 비동기 임의 시점, 드레인은 결정론적 틱 스텝 안. 이 정렬이 commit gate 없어도 버퍼를 필요하게 만듦.
- **와이어 추상은 슬라이스 4+로 미룸**: 슬라이스 3은 레거시 `DamageEventToC`를 어댑터에서 격리. `WorldEventBatch` 폴리모픽 envelope 전환은 별도 슬라이스.
- **클라 LOP 측 EditMode 테스트 인프라 신설 안 함** (슬라이스 2와 같은 판단). 테스트는 `baegames.GameFramework.World.Tests`에서 core 타입 검증.

## Scope

### In Scope (슬라이스 3 구현)

**Core (`GameFramework.World`):**
- `WorldEvent` — 폴리모픽 이벤트 추상 (abstract record)
- `DamageDealtEvent : WorldEvent` — record
- `DeathEvent : WorldEvent` — record
- `WorldEventBuffer` — Append/Snapshot/Clear API를 가진 단일 폴리모픽 큐
- `HealthSystem.ApplyDamageDealt(Health, DamageDealtEvent)` — Application 메서드
- `WorldEventApplicator(EntityRegistry, HealthSystem)` — 이벤트 타입별 dispatch로 상태 쓰기

**Client LOP (`Assets/Scripts/World/`):**
- `WorldEventBridge` — 이벤트 타입별 dispatch로 `EventBus.Default.Publish` (프레젠테이션 fan-out)

**Client LOP 기존 파일 변경:**
- `GameDamageMessageHandler` — `OnDamageEventToC` 본체 복구 (스텁 → 와이어 어댑터)
- `LOPGameEngine.ProcessEvent` — 본체 추가 (snapshot → Apply → FanOut → Clear)
- `SceneLifetimeScope` (또는 `RoomLifetimeScope`) — `WorldEventBuffer`, `WorldEventApplicator`, `WorldEventBridge`, `HealthSystem` Singleton 등록

**테스트:**
- `baegames.GameFramework.World.Tests`에 추가:
  - `WorldEventBuffer` Append/Snapshot/Clear 동작
  - `HealthSystem.ApplyDamageDealt` 동작 (정확히 e.remaining을 그대로 씀)
  - `WorldEventApplicator` 이벤트 dispatch + 미등록 targetId 안전 처리

**런타임 검증:**
- 슬라이스 1/2 동일 패턴: 플레이 모드에서 캐릭터 1마리 데미지 받기 + 사망 → 콘솔 로그 + 데미지 숫자 popup 확인.

### Out of Scope (이 슬라이스에서 안 함, 별도 슬라이스)

**서버 측:** 슬라이스 3은 클라 전용. 서버 측 Generation 구조 변경은 별도 작업. 슬라이스 3은 기존 서버 wire(`DamageEventToC`)를 그대로 받음.

**Stage ④ 항목 (예측·롤백 인프라):**
- 이벤트에 `tick` 스탬프
- `source: Predicted/Confirmed` 태그
- Commit gate 본격 구현 (예측 fan-out vs 확정 fan-out 분리)
- 재시뮬 시 이벤트 dedupe
- `Snapshot/Restore` on World Core
- 결정론적 RNG
- 클라 측 Generation (`AttackSystem` 등)
- Reconciliation 로직 (predicted ↔ confirmed 비교)

**별도 슬라이스 (슬라이스 3 이후):**
- 와이어 envelope (`WorldEventBatch`) 도입 — 서버 측 변경 필요
- `EntityDeath` view 이벤트의 실제 구독자 추가 (사망 애니메이션, 카메라 효과 등)
- Death 컴포넌트/시스템 (respawn timer 등 사망 상태 모델)
- Stats/Mana/Level 마이그레이션 (`enum StatType` 정의 등)
- 클라 LOP EditMode 테스트 인프라 (`Assets/Tests/EditMode/` asmdef + `[InternalsVisibleTo]`) — 진짜 두꺼운 seam 생길 때 도입

## Core Type Additions

위치: `GameFramework/Runtime/Scripts/World/Events/` (신규 폴더). 네임스페이스: `GameFramework.World`.

### `WorldEvent` (`Events/WorldEvent.cs`)

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

### `DamageDealtEvent` (`Events/DamageDealtEvent.cs`)

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

### `DeathEvent` (`Events/DeathEvent.cs`)

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

### `WorldEventBuffer` (`Events/WorldEventBuffer.cs`)

```csharp
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
        private readonly List<WorldEvent> _events = new();

        /// <summary>이벤트 1개 append. null은 ArgumentNullException.</summary>
        public void Append(WorldEvent e);

        /// <summary>현재 누적된 이벤트의 읽기 전용 뷰. 자체 mutation 없음.</summary>
        public IReadOnlyList<WorldEvent> Snapshot { get; }

        /// <summary>누적된 이벤트 전부 제거. 드레인 후 호출.</summary>
        public void Clear();

        /// <summary>현재 누적 개수 (테스트 편의).</summary>
        public int Count { get; }
    }
}
```

### `HealthSystem.ApplyDamageDealt` (확장, `Systems/HealthSystem.cs`)

```csharp
namespace GameFramework.World
{
    public class HealthSystem
    {
        // 기존 Generation 측 메서드 유지 (TakeDamage, Heal, SetMax) — 슬라이스 1.

        /// <summary>
        /// 데미지 결과 이벤트를 그대로 Health에 반영한다. 결정/계산/가드 없음.
        /// Application 측 메서드.
        /// </summary>
        public void ApplyDamageDealt(Health health, DamageDealtEvent e)
        {
            health.Current = e.remaining;
        }
    }
}
```

### `WorldEventApplicator` (`Systems/WorldEventApplicator.cs`)

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

        public WorldEventApplicator(EntityRegistry registry, HealthSystem healthSystem);

        public void Apply(IReadOnlyList<WorldEvent> events);
    }
}
```

내부 dispatch는 `switch` 표현식 또는 패턴 매칭으로:

```csharp
foreach (var e in events) {
    switch (e) {
        case DamageDealtEvent dde:
            if (_registry.TryGet(dde.targetId, out var entity) && entity.Get<Health>() is Health h)
                _healthSystem.ApplyDamageDealt(h, dde);
            break;
        case DeathEvent:
            // 슬라이스 3: state 변화 없음. Stage ④에서 Death 컴포넌트가 생기면 여기서 처리.
            break;
    }
}
```

## Client LOP Additions

### 새 파일

위치: `Assets/Scripts/World/` (신규 폴더, 슬라이스 3 LOP 측 글루 코드 모음).

#### `WorldEventBridge.cs`

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
            foreach (var e in events) {
                switch (e) {
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

### 기존 파일 수정

#### `Assets/Scripts/Game/MessageHandler/Game.Damage.MessageHandler.cs`

`OnDamageEventToC` 본체 스텁을 와이어 어댑터로 복구. `WorldEventBuffer` 주입 받음.

```csharp
using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameDamageMessageHandler : IGameMessageHandler
    {
        [Inject] private GameFramework.World.WorldEventBuffer worldEventBuffer;

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

            if (msg.IsDead) {
                worldEventBuffer.Append(new GameFramework.World.DeathEvent(
                    victimId:   msg.TargetId,
                    attackerId: msg.AttackerId
                ));
            }
        }
    }
}
```

주의: `IGameEngine` 의존성은 더 이상 필요 없어 제거. `IGameMessageHandler` 등록은 `RoomLifetimeScope.cs`에 이미 있음 (`builder.Register<IGameMessageHandler, GameDamageMessageHandler>(Lifetime.Transient)`) — 변경 없음.

#### `Assets/Scripts/Game/LOPGameEngine.cs`

`ProcessEvent` 본체 추가. `WorldEventBuffer` + `WorldEventApplicator` + `WorldEventBridge` 주입 받음.

```csharp
using GameFramework;
using LOP.Event.LOPGameEngine.Update;
using UnityEngine;
using VContainer;

namespace LOP
{
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

        // ... 기존 메서드 그대로 ...

        private void ProcessEvent()
        {
            var snapshot = worldEventBuffer.Snapshot;
            if (snapshot.Count == 0) return;

            worldEventApplicator.Apply(snapshot);
            worldEventBridge.FanOut(snapshot);
            worldEventBuffer.Clear();
        }
    }
}
```

주의:
- `LOPGameEngine`는 MonoBehaviour. `[Inject]` 주입을 받으려면 **`[DIMonoBehaviour]` 어트리뷰트 필요** — 슬라이스 2의 `LOPEntityManager`와 동일 패턴.
- `RoomLifetimeScope`가 `builder.RegisterComponent(gameEngine).As<IGameEngine>()` 로 인스턴스 등록 중인데, 이건 *컨테이너가 이 인스턴스를 제공하는 것*이지 인스턴스의 `[Inject]` 필드를 채우진 않음. 필드 주입은 별도로 `[DIMonoBehaviour]` 자동 스캔(`SceneLifetimeScope.Awake`의 `FindComponentsWithAttribute<DIMonoBehaviourAttribute>` → `Container.Inject(...)`)에 의존.

#### `Assets/Scripts/Scene/SceneLifetimeScope.cs` (또는 `RoomLifetimeScope.cs`)

`WorldEventBuffer`, `HealthSystem`, `WorldEventApplicator`, `WorldEventBridge` Singleton 등록.

```csharp
protected override void Configure(IContainerBuilder builder)
{
    builder.Register<GameFramework.World.EntityRegistry>(Lifetime.Singleton);
    builder.Register<GameFramework.World.WorldEventBuffer>(Lifetime.Singleton);
    builder.Register<GameFramework.World.HealthSystem>(Lifetime.Singleton);
    builder.Register<GameFramework.World.WorldEventApplicator>(Lifetime.Singleton);
    builder.Register<WorldEventBridge>(Lifetime.Singleton);
}
```

배치는 `SceneLifetimeScope` (월드는 씬 소유, 슬라이스 1과 일관) 또는 `RoomLifetimeScope`(Room 씬 한정). 슬라이스 1이 `EntityRegistry`를 `SceneLifetimeScope`에 둔 패턴 따름 → 동일 위치.

## 데이터 흐름 예시 — 단일 데미지 1건

1. 서버: `LOPEntity` id=7 (HP=100/100)에 데미지 50 발생. `DamageEventToC{TargetId=7, AttackerId=3, Damage=50, RemainingHP=50, IsCritical=false, IsDodged=false, IsDead=false}` 전송.
2. 클라 Mirror가 비동기로 `EventBus.Default.Publish<DamageEventToC>` (Mirror integration) → `GameDamageMessageHandler.OnDamageEventToC` 호출.
3. 핸들러: `worldEventBuffer.Append(new DamageDealtEvent(targetId="7", attackerId="3", amount=50, isCritical=false, isDodged=false, remaining=50, isDead=false))`. `IsDead=false`라 `DeathEvent` append 안 됨.
4. (시간 차) 다음 tick의 `LOPGameEngine.UpdateEngine()` 진행. `ProcessEvent` 도달.
5. `snapshot = [DamageDealtEvent("7", "3", 50, false, false, 50, false)]`.
6. `worldEventApplicator.Apply(snapshot)`:
   - `EntityRegistry.TryGet("7", out var entity)` → true.
   - `entity.Get<Health>()` → Health(Max=100, Current=100).
   - `healthSystem.ApplyDamageDealt(health, dde)` → `health.Current = 50`.
7. `worldEventBridge.FanOut(snapshot)`:
   - `EventBus.Default.Publish(EventTopic.EntityId<LOPEntity>("7"), new EntityDamage(isDodged=false, isCritical=false, damage=50, remainingHP=50, isDead=false))`.
   - `DamageView`가 데미지 숫자 "50" popup, `LOPEntityView`가 피격 효과.
8. `worldEventBuffer.Clear()`.

## 데이터 흐름 예시 — 사망 1건

1. 서버: id=7 (HP=10/100)에 데미지 50. `DamageEventToC{... Damage=50, RemainingHP=0, IsDead=true}`.
2. 핸들러:
   - `Append(DamageDealtEvent("7", "3", 50, _, _, 0, true))`
   - `Append(DeathEvent("7", "3"))`
3. ProcessEvent 드레인:
   - Applicator: `health.Current = 0`. DeathEvent는 no-op.
   - Bridge: `EntityDamage` popup ("50"), `Debug.Log("[World] Death entity 7 (killer=3)")`.

## Wiring & DI 변경 요약

| 파일 | 변경 |
|---|---|
| `SceneLifetimeScope.cs` | `WorldEventBuffer`, `HealthSystem`, `WorldEventApplicator`, `WorldEventBridge` Singleton 추가 |
| `RoomLifetimeScope.cs` | 변경 없음 (`SceneLifetimeScope` 상속) |

## 테스트 전략

**EditMode (`baegames.GameFramework.World.Tests`):**

- `WorldEventBufferTests.cs`
  - Empty 상태에서 `Snapshot.Count == 0`, `Count == 0`
  - 1개 Append → `Count == 1`, `Snapshot[0] == expected`
  - N개 Append → 삽입 순서로 `Snapshot` 보임
  - Clear 후 `Count == 0`, 새 Append 정상
  - null Append → ArgumentNullException

- `HealthSystemTests.cs` 확장
  - `ApplyDamageDealt(health, event)` → `health.Current == event.remaining` (단순 그대로 쓰기)
  - 기존 `TakeDamage`/`Heal`/`SetMax` 테스트 그대로 통과 (회귀 보호)

- `WorldEventApplicatorTests.cs`
  - 등록된 엔티티에 DamageDealtEvent → Health.Current 변경 확인
  - 미등록 targetId → 예외 없이 조용히 스킵
  - Health 컴포넌트 없는 엔티티 → 예외 없이 조용히 스킵
  - DeathEvent → 어떤 상태도 변경 안 함 (no-op 동작 명시)

총 EditMode 테스트: 슬라이스 2 기준 48개 → 슬라이스 3 추가 ~10개 → ~58개.

**Client EditMode 테스트 (`WorldEventBridge`, 어댑터):** 신설 안 함. 슬라이스 2와 동일 이유 — 인프라 비용 vs 코드량 ROI 낮음. 런타임 검증으로 커버.

**런타임 검증 (Unity Play Mode):**

슬라이스 1/2 패턴 — 사용자가 플레이 모드 진입 후 캐릭터 1마리에 데미지 인가, 다음 항목 콘솔 + 화면 관찰:

1. **데미지 숫자 popup 출현** — `DamageView`가 살아났음을 의미
2. **`[World] Death entity {id} (killer={id})` 로그** (사망 시) — Bridge 동작 + DeathEvent까지 끝-끝
3. **에러 0건** — DI 누락(NRE) 없음, EntityRegistry 룩업 실패 없음
4. **재스폰 후 새 데미지에도 정상** — 슬라이스 2의 Unregister 동작과 호환 (id 재사용 안전)

## Open Design Decisions (브레인스토밍에서 잠금)

| 결정 | 선택 | 이유 |
|---|---|---|
| 이벤트 페이로드 폭 | 풀-필드 (isCritical/isDodged 포함) | 시스템은 자기 영역만 만지지만 데이터는 운반. 본인 원칙 "데이터 풀, 시스템 fit" |
| 1패스 vs 2패스 | 2패스 (Collection / Application 분리) | 동시 사망/트레이드 지원, 결정론, future 예측 호환 |
| Cascade (Damage→Death) | Generation Phase 3 (서버 측). 클라는 받기만. | 서버 권위 슬라이스. 클라 측 cascade 검출 코드 0. |
| 와이어 추상 (`WorldEventBatch`) | 슬라이스 3 안 함. 어댑터에서 격리. | 서버 측 변경 의존. 안쪽 코드는 `WorldEvent`만 본다. |
| Commit gate | 슬라이스 3 안 만듦 (trivial drain). 주석으로 Stage ④ 표시. | YAGNI. 서버 권위라 재시뮬 없음. |
| Applicator + Bridge 클래스 분리 | 분리 (각자 책임 SRP) | 코어/클라 어셈블리 경계 자연 — Applicator는 코어, Bridge는 LOP |
| DeathEvent state 변경 | 슬라이스 3 no-op (Health.Current 이미 0) | Death 컴포넌트 미존재. Stage ④/별도 슬라이스에서 도입 |
| `EntityDeath` view 이벤트 fan-out | 슬라이스 3 안 함 (구독자 0건). Bridge log only. | 구독자 추가될 때 별도 슬라이스 |
| `DeathEvent.position` 필드 | 슬라이스 3 안 둠 | 현재 활용처 없음, 필요해지면 추가 |
| 클라 LOP EditMode 테스트 asmdef | 슬라이스 3에서도 안 만듦 | 슬라이스 2와 동일 판단, ROI 낮음 |

## Stage ④ 확장 경로 (참고용, 슬라이스 3 코드 변경 없이 추가)

- `WorldEvent`에 `int tick`, `EventSource source` 필드 추가 (Predicted/Confirmed/Authoritative)
- `WorldEventBuffer`에 commit gate API: 확정된 tick까지만 Snapshot, 재시뮬에서 같은 (tick, eventId) dedupe
- `WorldEventApplicator`에 predicted vs confirmed 정책: 일치하면 no-op, 불일치하면 Snapshot/Restore + 재적용
- `WorldEventBridge`에 fan-out 정책: optimistic(predicted 즉시 발사 + 정정) vs conservative(확정만 발사)
- 클라 측 `AttackSystem` (Generation Phase 1+2): 자기 캐릭 인풋 → 로컬 DamageDealtEvent 생성 → 위 commit gate가 예측 fan-out 처리

슬라이스 3 코드는 이 확장에서 *수정되지 않는다*. 추가만 일어남.

## 진행

- [x] 브레인스토밍 합의
- [x] 연결 아키텍처 doc 갱신 (`d33b119`)
- [ ] 이 spec 사용자 리뷰
- [ ] writing-plans로 구현 plan 작성
- [ ] subagent-driven 또는 inline 실행

머지 후 클라 main tip = 신규 commit. 슬라이스 1/2와 동일한 `--no-ff` 패턴, push 안 함.
