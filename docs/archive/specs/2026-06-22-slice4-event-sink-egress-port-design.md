# 이벤트 송출 포트 — `IEventSink` (egress, Slice 4)

**Date:** 2026-06-22
**Branch (제안):** `feature/slice4-event-sink`
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (이벤트 모델·"와이어 표현"·cleanup backlog) · 자매 슬라이스 [물리 어댑터](2026-06-21-slice4-physics-simulator-adapter-design.md) · [RNG 추상화](2026-06-22-slice4-random-abstraction-design.md)

## Goal

`LOPGameEngine.ProcessEvent`가 직접 호출하던 **이벤트 송출(egress)** 을 주입된 `IEventSink` 포트 뒤로 격리한다. 동시에 **서버의 `WorldEventBridge`(death→despawn은 송출이 아니라 cascade reactor)를 sink에서 분리·rename**(`WorldEventReactor`)해 "egress는 순수 송출"이라는 모델(connection-arch)에 맞춘다. **동작 100% 보존**(같은 호출·순서, 이름/타입만 포트화 + reactor 분리).

## 배경 / 동기

connection-arch가 정의한 3계층 중 **③ Projection/Egress의 *송출* 부분**을 포트화하는 Slice 4 조각. 물리(`IPhysicsSimulator`)·RNG(`IRandom`)와 같은 port/adapter 패턴이나, 송출은 **클·서 본문이 달라서**(클=EventBus 프레젠테이션 / 서=wire) 인터페이스 1 + 사이드별 구현 2다.

핵심 정리 한 가지가 끼어 있다 (connection-arch "죽음 4단계" + backlog #2):
- 클라 `WorldEventBridge.FanOut` → EventBus → 플로터/뷰 = **순수 프레젠테이션 egress** (진짜 sink).
- 서버 `WireBroadcaster.Broadcast` → wire = **순수 네트워크 egress** (진짜 sink).
- 서버 `WorldEventBridge.FanOut` → `LOPGame.HandleDeath`(디스폰+경험치) = egress가 아니라 **death cascade reactor**. 이름만 "Bridge"지 역할은 reactor.

→ 이 슬라이스는 *진짜 송출* 둘을 `IEventSink`로 포트화하고, 서버의 reactor를 sink에서 떼어 `WorldEventReactor`로 이름붙인다. **death→despawn을 진짜 Generation cascade로 옮기는 건 범위 밖**(backlog #2-full) — 여기선 *호출 위치 분리 + rename*만(behavior-preserving).

## 현재 상태 (실측)

- 클라 `LOPGameEngine.cs:89-99` `ProcessEvent`: `worldEventApplicator.Apply(snap)` → `worldEventBridge.FanOut(snap)` → `Clear`.
- 서버 `LOPGameEngine.cs:163-174` `ProcessEvent`: `Apply(snap)` → `wireBroadcaster.Broadcast(snap)` → `worldEventBridge.FanOut(snap)` → `Clear`.
- 호출부는 **각 `ProcessEvent` 단 한 곳**(다른 caller 없음 — grep 확인). LOPCombatSystem의 `WorldEventBridge.FanOut` 언급은 *주석*.
- 세 메서드 시그니처 동일: `void X(IReadOnlyList<GameFramework.World.WorldEvent>)`. `WorldEventBuffer.Snapshot`이 그 타입.
- 구현체: 클 `WorldEventBridge`(`Assets/Scripts/World/`) / 서 `WorldEventBridge`(death→HandleDeath) + `WireBroadcaster`(ctor `ISessionManager`).
- DI: 클 `GameLifetimeScope:35` `Register<WorldEventBridge>`; 서 `:26` `Register<WireBroadcaster>` + `:27` `Register<WorldEventBridge>`.
- GameFramework asmdef: `baegames.GameFramework.World`(noEngineReferences=true, `WorldEvent`/`WorldEventBuffer` 보유) / `baegames.GameFramework.Runtime`(engine, `IPhysicsSimulator`/`IRandom` 보유).

## 설계

### 신규 타입 (GameFramework)

**`IEventSink`** — egress 포트. `WorldEvent`를 참조하므로 **`baegames.GameFramework.World` 어셈블리**(`Runtime/Scripts/World/Events/`, `WorldEventBuffer` 옆)에 둔다.

```csharp
using System.Collections.Generic;

namespace GameFramework.World
{
    /// <summary>
    /// 확정된 WorldEvent를 바깥(프레젠테이션·네트워크)으로 송출하는 egress 포트.
    /// 순수 송출 — 상태를 바꾸거나 새 이벤트를 만들지 않는다(그건 Generation 소관).
    /// 사이드별 구현: 클라=프레젠테이션 EventBus, 서버=wire broadcast.
    /// </summary>
    public interface IEventSink
    {
        void Emit(IReadOnlyList<WorldEvent> events);
    }
}
```

> **배치 근거:** `IEventSink`는 `WorldEvent`를 시그니처에 노출하므로 `WorldEvent`가 사는 World 어셈블리여야 한다(Runtime은 World 미참조). 또한 *port는 core에 정의, adapter는 outer에 구현*(헥사고날)이 정석이라 World(core)에 두는 게 맞다. `WorldEventBuffer`(이벤트 큐) 바로 옆이라 응집도 좋다. (물리/RNG 포트가 Runtime에 있는 것과 다른 이유 = 그것들은 World 타입 비참조.) 순수 인터페이스라 noEngineReferences 위반 없음.

### 구현 — 기존 클래스가 직접 구현 + rename (래퍼 신설 X)

추상화 대상이 *우리 자신의 클래스*라 불필요한 indirection 없이 직접 구현한다. 포트가 `IEventSink`이므로 구현체도 어휘를 맞춰 rename한다(짝 일관). **클·서 egress 구현은 같은 이름 `WorldEventSink`** — 기존 `WorldEventBridge`가 클·서 *같은 이름·다른 본문*이던 사이드별 분기 관습 그대로(`LOPGameEngine` 패턴). `WorldEvent*` 패밀리로 일관: `WorldEvent`(데이터) / `WorldEventBuffer`(큐) / `WorldEventSink`(egress) / `WorldEventReactor`(서버 cascade).

**클라 `WorldEventBridge` → `WorldEventSink : IEventSink`** (`Assets/Scripts/World/WorldEventBridge.cs` → `WorldEventSink.cs`):
- 클래스 + 파일 rename(`git mv` + `.meta` 동반), `: GameFramework.World.IEventSink` 추가, 메서드 `FanOut` → **`Emit`**(본문 동일). 클라 sink = EventBus 프레젠테이션.

**서버 `WireBroadcaster` → `WorldEventSink : IEventSink`** (`Assets/Scripts/World/WireBroadcaster.cs` → `WorldEventSink.cs`):
- 클래스 + 파일 rename(`git mv` + `.meta` 동반), `: GameFramework.World.IEventSink` 추가, 메서드 `Broadcast` → **`Emit`**(본문·ctor `ISessionManager` 동일). 서버 sink = wire broadcast.

**서버 `WorldEventBridge` → `WorldEventReactor`** (`Assets/Scripts/World/WorldEventBridge.cs` → `WorldEventReactor.cs`):
- 클래스 + 파일 rename(`git mv` + `.meta` 동반), 메서드 `FanOut` → **`React`**(본문 동일 — DeathEvent → EventBus → HandleDeath). **`IEventSink` 구현 안 함**(reactor지 sink 아님). 순수 C# 클래스.
- 주석 정합: 클래스 doc + `LOPCombatSystem.cs:73,76`의 "WorldEventBridge.FanOut → HandleDeath" 언급을 `WorldEventReactor.React`로 갱신.

> 서버 파일 rename 충돌 없음: `WireBroadcaster.cs` → `WorldEventSink.cs`, `WorldEventBridge.cs` → `WorldEventReactor.cs` (서로 다른 타깃). 서버엔 `WorldEventSink.cs` + `WorldEventReactor.cs` 둘, 클라엔 `WorldEventSink.cs` 하나.

### 호출부 재배선 (`ProcessEvent`, Apply·Clear 그대로)

**클라 `LOPGameEngine`:**
- `[Inject] private WorldEventBridge worldEventBridge;` → `[Inject] private GameFramework.World.IEventSink eventSink;`
- `worldEventBridge.FanOut(snapshot);` → `eventSink.Emit(snapshot);`

**서버 `LOPGameEngine`:**
- `[Inject] private WireBroadcaster wireBroadcaster;` + `[Inject] private WorldEventBridge worldEventBridge;` → `[Inject] private GameFramework.World.IEventSink eventSink;` + `[Inject] private WorldEventReactor reactor;`
- `wireBroadcaster.Broadcast(snapshot); worldEventBridge.FanOut(snapshot);` → `eventSink.Emit(snapshot); reactor.React(snapshot);` (같은 두 동작·같은 순서 — egress 먼저, reactor 다음)

### DI 등록

- 클 `GameLifetimeScope`: `Register<WorldEventBridge>(Singleton)` → `Register<GameFramework.World.IEventSink, WorldEventSink>(Singleton)`.
- 서 `GameLifetimeScope`: `Register<WireBroadcaster>(Singleton)` → `Register<GameFramework.World.IEventSink, WorldEventSink>(Singleton)`; `Register<WorldEventBridge>(Singleton)` → `Register<WorldEventReactor>(Singleton)`.
- (각 클래스의 다른 concrete 주입 없음 — grep 확인 — 이라 인터페이스 등록만으로 충분.)

## 동작 보존 / 검증

- **동작 보존:** 송출은 같은 메서드 본문이 같은 인자로 호출됨(이름만 Emit). 서버는 Broadcast+FanOut 두 동작이 Emit+React 두 동작으로 그대로(순서 보존). reactor 분리는 *호출 위치*만 정리, 로직 무변경.
- **검증:** GameFramework + 클·서 컴파일 클린(`read_console`). 실행 시 — 전투 시 데미지 **플로터/체력바 정상**(클라 egress), 클라가 데미지 **수신 정상**(서버 wire egress), **죽음 시 디스폰+경험치 구슬 정상**(서버 reactor). 콘솔 신규 에러 없음.
- **유닛 테스트:** 구현체가 EventBus/Mirror/세션에 의존 + Assembly-CSharp라 단위 테스트 부적합. 동작 보존 슬라이스라 신규 테스트 없음 — seam 페이오프는 향후 시뮬이 `IEventSink`로 송출(사이드 무지) + 모킹 가능.

## 산업 표준 매핑

- `IEventSink` = connection-arch가 정의한 I/O 어댑터(클=EventBus / 서=wire) 포트화. port/adapter(헥사고날) — port를 core(World)에 정의.
- **egress와 reactor 분리** = connection-arch "③ Egress는 새 사실 생성 안 함" 불변식 + CQRS "projection은 이벤트 안 만듦" 적용. 서버 death cascade는 reactor(Generation 계열)로 명명 분리.
- **네이밍:** 구현체 `WorldEventSink`는 클·서 *같은 이름·다른 본문*(`LOPGameEngine`/구 `WorldEventBridge`의 "의도적 양쪽 분기" 관습 — `lop-repo-topology.md`). 포트 `IEventSink` ↔ 구현 `WorldEventSink` ↔ `WorldEvent*` 패밀리로 짝 일관.

## Out of Scope (backlog 유지)

- **death→despawn을 진짜 Generation cascade로 이전**(`DespawnEvent` emit → Application 적용) — backlog #2-full, 별도. 이번엔 reactor *분리·rename*만(HandleDeath 내부 로직 무변경, 여전히 EventBus→HandleDeath).
- **이중 HP 경로**(#3), **이중 apply**(#1) — 별도.
- **클라 reactor** — 클라엔 death reactor 없음(DeathEvent→Debug.Log 자리만). 안 만듦.

## Open Questions (구현 plan에서 해소)

- 파일 rename 3건 모두 `.meta` GUID 보존(`git mv`로 `.cs`+`.meta` 동반): 클 `WorldEventBridge.cs`→`WorldEventSink.cs`, 서 `WireBroadcaster.cs`→`WorldEventSink.cs`, 서 `WorldEventBridge.cs`→`WorldEventReactor.cs`.
- `IEventSink.cs` 배치 = `Runtime/Scripts/World/Events/`(WorldEventBuffer 옆) 확정.

## 진행

- [x] 브레인스토밍 합의(egress 포트 B-minimal, 기존클래스 직접구현+rename, 클·서 egress=`WorldEventSink`/서버 cascade=`WorldEventReactor`, IEventSink=World 어셈블리)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
