# 물리 시뮬레이션 어댑터 격리 — Slice 4 첫 조각

**Date:** 2026-06-21
**Branch (제안):** `feature/slice4-physics-simulator-adapter`
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (`IPhysicsSimulator` 추상 · Engine↔Simulation 컴포지션) · [LOP 저장소 토폴로지](../../lop-repo-topology.md) · [netcode-redesign](../../netcode-redesign.md) §6.5

## Goal

`LOPGameEngine`의 `SimulatePhysics` 단계에서 직접 호출하던 `Physics.Simulate(...)`를 **인터페이스(`IPhysicsSimulator`)로 추상화하고 DI로 주입**한다. 구체 구현은 Unity `Physics.Simulate`를 호출하는 얇은 어댑터(`UnityPhysicsSimulator`)다. **동작은 100% 보존**(어댑터가 동일한 호출을 위임)하며, 이 슬라이스의 가치는 *seam 자체* — 물리 시뮬을 엔진 본체에서 분리해 주입 가능하게 만드는 것이다.

## 배경 / 동기

Stage④(시뮬 코어 추출 = Slice 4)의 **가장 작은 첫 조각**이다. 전체 4a+4b 골격(시뮬 추상 5종 + `LOPGameSimulation` 빈 골격)을 한 번에 만들지 않고, *실제로 주입 가치가 즉시 드러나는* 물리 어댑터 하나만 먼저 격리한다.

- `world-core-connection-architecture.md`는 물리를 `IPhysicsSimulator` **어댑터**로 다루는 방향을 이미 박아 두었다(시뮬이 물리를 *구동*하되 구체 타입은 모름; "PhysX 호출 추상 — 양쪽 동일 구체").
- 현재 클·서 `LOPGameEngine`이 `UnityEngine.Physics.Simulate`를 **직접** 호출한다 → 엔진 본체가 PhysX에 강결합. 어댑터로 빼면 향후 결정론/모킹 물리, 룸별 독립 `PhysicsScene` 등을 *교체 가능한 주입점*으로 받을 수 있다.
- 모션 권위 역전(World Core가 위치 진실원본)은 **이 슬라이스 범위 밖** — 물리 *호출*만 어댑터화하고 물리 *상태 소유*는 그대로 둔다.

## 현재 상태 (실측)

- 클라 `Assets/Scripts/Game/LOPGameEngine.cs:76-83` · 서버 `Assets/Scripts/Game/LOPGameEngine.cs:154-158` — 각각 `SimulatePhysics()`:
  ```csharp
  DispatchEvent<BeforePhysicsSimulation>();
  Physics.Simulate((float)tickUpdater.interval);
  DispatchEvent<AfterPhysicsSimulation>();
  ```
  호출부는 **베이스(`GameEngineBase`)가 아니라 클·서 각 `LOPGameEngine`에 별도로** 존재.
- `Physics.simulationMode = SimulationMode.Script`는 이미 클·서 `LOPGame`(`LOPGame.cs:60` / `:85`)에서 설정 — **어댑터가 건드리지 않는다.**
- 주입은 `[DIMonoBehaviour]` + `[Inject]` 필드(VContainer). `tickUpdater`는 `GameEngineBase` 프로퍼티.
- GameFramework asmdef: `baegames.GameFramework.World`(noEngineReferences **true**, 순수 엔티티 코어) / `baegames.GameFramework.Runtime`(noEngineReferences **false**, MonoBehaviour `GameEngineBase`·`ITickUpdater`·`IGameEngine` 보유).

## 설계

### 신규 타입 (GameFramework)

`baegames.GameFramework.Runtime` 어셈블리, `Runtime/Scripts/Game/`(기존 `IGameEngine`/`ITickUpdater`와 같은 위치 — 짝 일관):

- **`IPhysicsSimulator`** — I/O 포트 추상.
  ```csharp
  namespace GameFramework
  {
      public interface IPhysicsSimulator
      {
          void Simulate(float deltaTime);
      }
  }
  ```
  내용상 엔진 타입을 노출하지 않는다(엔진-프리 시그니처). 단 배치는 기존 포트 추상(`ITickUpdater`/`IGameEngine`)과 동일 어셈블리 — 이들 역시 `Runtime`에 산다.

- **`UnityPhysicsSimulator`** — Unity 어댑터(엔진 바운드).
  ```csharp
  using UnityEngine;

  namespace GameFramework
  {
      public sealed class UnityPhysicsSimulator : IPhysicsSimulator
      {
          public void Simulate(float deltaTime) => Physics.Simulate(deltaTime);
      }
  }
  ```
  현재 호출(`Physics.Simulate(default 물리 씬)`)을 그대로 미러. `simulationMode`는 호출자(`LOPGame`) 책임으로 둔다.

> **배치 근거:** `Physics.Simulate`는 LOP 도메인이 아니라 **엔진 일반**이라 재사용 인프라인 GameFramework에 둔다(LOP-Shared 아님). 기존 사이드별 포트(`ITickUpdater` 클=Mirror smoothing / 서=자기 클럭)와 달리 `UnityPhysicsSimulator`는 **양쪽 동일 구체** — 그래서 한 곳(GameFramework)에 두고 클·서가 함께 참조한다.

### 호출부 교체 (클·서 각 `LOPGameEngine`)

- `[Inject] private GameFramework.IPhysicsSimulator physicsSimulator;` 필드 추가.
- `SimulatePhysics()` 본문 중간 한 줄만 교체:
  ```csharp
  DispatchEvent<BeforePhysicsSimulation>();
  physicsSimulator.Simulate((float)tickUpdater.interval);   // was: Physics.Simulate(...)
  DispatchEvent<AfterPhysicsSimulation>();
  ```
- `Before/AfterPhysicsSimulation` 디스패치는 **무변경**(엔진 이벤트지 물리가 아님).

### DI 등록

`IPhysicsSimulator → UnityPhysicsSimulator`(Singleton)를 **클·서 각각**, `LOPGameEngine`의 다른 `[Inject]` deps(`WorldEventBuffer` 등)를 제공하는 동일 스코프/인스톨러에 등록한다. (정확한 인스톨러 파일은 구현 plan에서 확정.)

## 동작 보존 / 검증

- **동작 보존:** 어댑터가 `Physics.Simulate(deltaTime)`를 *동일 인자로* 호출 → 시뮬 결과 바이트 동등. 교체는 "직접 호출 → 1-홉 위임"뿐.
- **검증:**
  - 클·서 양쪽 컴파일 통과(`read_console` 클린).
  - 실행 시 이동/점프/낙하/물리 충돌이 교체 전과 **육안 동일**(rubberbanding·발산 없음).
- **유닛 테스트:** `UnityPhysicsSimulator`는 정적 `Physics.Simulate` 래핑이라 의미 있는 단위 테스트 부적합. `LOPGameEngine`은 Assembly-CSharp라 asmdef 테스트 불가(클·서 게임코드 전부 Assembly-CSharp — PlayMode 리플렉션 외 단위 테스트 경로 없음). 이 슬라이스는 *동작 보존 슬라이스*라 신규 자동 테스트 없음 — seam의 페이오프는 *향후* 모킹/결정론 물리 주입 가능성이다.

## 산업 표준 매핑

- `IPhysicsSimulator` / `Simulate(float)` = `world-core-connection-architecture.md`가 박은 GameFramework 추상(`IPhysicsSimulator` — "PhysX 호출 추상, 양쪽 동일 구체")에 대응. 메서드는 Unity `Physics.Simulate(float step)` API 미러(파라미터명만 `deltaTime`로 — 호출자가 넘기는 `tickUpdater.interval`의 의미).
- 컴포지션 모델(시뮬이 물리를 *주입된 어댑터로* 구동, 구체 타입 비의존) = Photon Quantum의 물리 분리, Unity DOTS `PhysicsWorld`/`Simulation` 분리와 같은 계열의 *port/adapter*(헥사고날) 적용.

## Out of Scope

- **다른 I/O 어댑터**(`IInputSource`/`IEventSink`/`INetworkSession`) — 첫 실제 사용 시(Slice 4d)로 미룸. 구현체·호출부 없는 인터페이스 미리 안 만듦(YAGNI).
- **`IGameSimulation`/`GameSimulationBase`/`LOPGameSimulation`** — 시뮬 골격은 별도 슬라이스.
- **모션 권위 역전**(World Core 위치 진실원본) — 물리 *상태 소유*는 그대로(현재 Unity Rigidbody).
- **룸별 독립 `PhysicsScene`** — 어댑터가 향후 받을 수 있는 주입점이나 지금 만들지 않음.
- **결정론 RNG / Snapshot·Restore / 예측·롤백** — Stage④ 후속.

## Open Questions (구현 plan에서 해소)

- DI 등록 위치: 클·서 각 인스톨러/`LifetimeScope`의 정확한 파일(현재 `WorldEventBuffer` 등록 지점과 동일 스코프).
- `IPhysicsSimulator.cs`/`UnityPhysicsSimulator.cs`를 `Runtime/Scripts/Game/` 직하에 둘지 `Runtime/Scripts/Game/Physics/` 하위 폴더로 둘지(사소 — 기존 파일 배치 관습 따름).

## 진행

- [x] 브레인스토밍 합의(물리 어댑터부터, GameFramework 배치, 동작 보존)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
