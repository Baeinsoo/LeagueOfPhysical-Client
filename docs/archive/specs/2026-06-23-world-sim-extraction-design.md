# 시뮬 코어 추출 — `Runner → World` 빈 골격 (Slice 4 ②, =4b)

**Date:** 2026-06-23
**Branch (제안):** `feature/world-extraction` (GameFramework / LOP-Shared / Client / Server)
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (Engine↔Simulation 컴포지션·4b) · [LOP 저장소 토폴로지](../../lop-repo-topology.md) (LOP-Shared = 공유 시뮬) · 프로젝트 메모리 `world-core-runner-world-naming` · 선행: ①`Engine→Runner` 리네임(완료, main)

## Goal

결정론 시뮬 코어 레이어를 **빈 골격**으로 신설한다: `IWorld` + `WorldBase`(GameFramework) + `LOPWorld`(LOP-Shared, 클·서 공유 1벌). `LOPRunner`가 이걸 보유하고 매 틱 `world.Tick(tick, dt)`을 호출한다(**ticked**). 단, **페이즈 훅은 빈 no-op이고 게임 로직은 0줄 옮긴다** — 동작 100% 보존. 이 슬라이스의 가치는 *공유 시뮬 seam을 세우는 것* 이다. (로직 흡수는 4c, Game층 흡수는 ③ — 둘 다 별도.)

## 배경 / 동기

**왜 시뮬을 빼나:** 최종 목표(클라 예측·롤백)는 *클·서가 같은 입력으로 같은 결과*(결정론)를 요구하고, 그걸 보장하는 유일한 길은 **양쪽이 문자 그대로 같은 시뮬 코드 1벌**을 돌리는 것이다. 지금 시뮬 로직은 클·서 *각자의* `LOPRunner`(서로 다른 본문)에 들어 있어 갈라질 수 있다. `LOPWorld`를 **LOP-Shared(양쪽이 import하는 패키지)에 1벌** 두면 결정론이 구조적으로 강제된다. (산업 표준: Quake `qvm`, Source game DLL, Quantum `QuantumGame`, Unreal `UWorld` — host가 보유·구동하는 inner sim.)

**왜 빈 골격부터(=4b):** 로직을 한 번에 옮기면 위험하다. 먼저 *빈 그릇 + 양쪽 배선 + 구동 호출*만 세우고(이번), 4c가 매니저를 *하나씩* 훅으로 흡수한다. 각 단계가 컴파일·동작 보존.

**왜 지금:** ①로 host가 `Runner`가 됐으니, 새 sim이 올바른 짝(`Runner → World`)으로 처음부터 올라탄다.

**이름 근거(메모리 `world-core-runner-world-naming`):** sim = `World`(DOTS `World`/Unreal `UWorld` 이중 선례 — "틱 도는 엔티티 그릇"). `IWorld`/`WorldBase`/`LOPWorld`(맨 `World` 타입 금지 → 네임스페이스 `GameFramework.World`와 충돌 0).

## 현재 상태 (실측)

- **`GameFramework.World`** asmdef = 순수(`noEngineReferences: true`, `autoReferenced: true`). 보유: `Entity`/`Component`/`EntityRegistry`, `WorldEventBuffer`(`Events/`), `HealthSystem`/`ManaSystem`/`LevelSystem`/`StatsSystem`(`Systems/`). → `WorldBase`/`IWorld`를 여기 두면 같은 asmdef에서 `EntityRegistry`/`WorldEventBuffer` 직접 사용 가능(추가 참조 불필요).
- **`EntityRegistry`/`WorldEventBuffer`**는 클·서 `GameLifetimeScope`에서 **DI 싱글톤**으로 등록, 여러 시스템·Sink·EntityManager가 주입받아 공유.
- **`LOPRunner`**(클·서 각자, `: RunnerBase`) — `UpdateRunner()`가 per-tick 루프. 클라 루프: `BeginUpdate → ProcessNetworkMessage → ProcessInput → InterpolateEntity → UpdateEntity → UpdateAI → SimulatePhysics → UpdateVisualEffect → ProcessEvent → EndUpdate`. 서버 루프는 다름(AI 의도 push, 보간 없음 — plan에서 grep 확정). 틱 값: `Runner.Time.tick`(long), `tickUpdater.interval`(double).
- **`LOP-Shared.Runtime`** asmdef = `references: ["baegames.GameFramework.Runtime"]` 만 — **`baegames.GameFramework.World` 미참조**. `Runtime/Scripts/Game/` 디렉터리 없음. → LOPWorld 추가 시 **asmdef에 `baegames.GameFramework.World` 참조 추가 + Game 디렉터리 생성** 필요.
- 클·서 Assembly-CSharp는 `GameFramework.World`·`LOP-Shared.Runtime`(둘 다 autoReferenced) 자동 참조 — LOPRunner가 `IWorld`(GameFramework.World)·`LOPWorld`(LOP-Shared) 사용 가능.

## 설계

### 신규 타입 — GameFramework.World (`Runtime/Scripts/World/`)

`IWorld` — 시뮬 코어 포트(상태 access + Tick). 엔진-프리.
```csharp
namespace GameFramework.World
{
    public interface IWorld
    {
        EntityRegistry EntityRegistry { get; }
        WorldEventBuffer EventBuffer { get; }
        void Tick(long tick, float deltaTime);
    }
}
```

`WorldBase` — 재사용 골격. 기존 DI 싱글톤을 **참조**(생성자 주입)해 프로퍼티로 노출. `Tick`이 페이즈 훅을 순서대로 호출. 훅은 **virtual no-op**(파생이 필요한 것만 override).
```csharp
namespace GameFramework.World
{
    public abstract class WorldBase : IWorld
    {
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

        // Generation 페이즈 (world-core-connection-architecture.md). 4b에선 전부 no-op; 4c가 채움.
        protected virtual void Collection(long tick, float deltaTime) { }
        protected virtual void Mutation(long tick, float deltaTime) { }
        protected virtual void Detection(long tick, float deltaTime) { }
    }
}
```
> **상태 소유권 무이동(D2):** sim은 `EntityRegistry`/`WorldEventBuffer`를 *소유*하지 않고 *참조*만 한다 → 기존 주입처(시스템·Sink·EntityManager) 전부 무변경. `world.EntityRegistry`가 같은 DI 싱글톤 인스턴스 반환. ownership 통합은 이번 범위 밖.
> **배치 근거:** 시뮬은 결정론 위해 엔진-프리 → 순수 asmdef `GameFramework.World`. `EntityRegistry`/`WorldEventBuffer`와 동거(같은 asmdef, 추가 참조 0).

### 신규 타입 — LOP-Shared (`Runtime/Scripts/Game/LOPWorld.cs`)

`LOPWorld` — LOP의 시뮬 구체, **클·서 공유 1벌**. 4b에선 훅 override 없음(빈 골격).
```csharp
namespace LOP
{
    public class LOPWorld : GameFramework.World.WorldBase
    {
        public LOPWorld(GameFramework.World.EntityRegistry entityRegistry, GameFramework.World.WorldEventBuffer eventBuffer)
            : base(entityRegistry, eventBuffer) { }
        // 4c가 Collection/Mutation/Detection override로 매니저 로직 흡수.
    }
}
```
> 컨벤션: World 타입은 풀 네임스페이스 한정(`using GameFramework.World;` 금지). LOPWorld는 엔진-프리 유지(LOP-Shared.Runtime이 `noEngineReferences:false`여도 sim은 엔진 미사용).

### asmdef 배선
- **LOP-Shared.Runtime** `references`에 **`baegames.GameFramework.World` 추가** (EntityRegistry/WorldEventBuffer/WorldBase 사용). `Runtime/Scripts/Game/` 디렉터리 신설.
- GameFramework.World·GameFramework.Runtime asmdef 무변경(IWorld/WorldBase는 World 내부, 기존 타입만 사용).

### DI 등록 (클·서 각 `GameLifetimeScope`)
`EntityRegistry`/`WorldEventBuffer` 등록 라인들과 같은 스코프에:
```csharp
builder.Register<GameFramework.World.IWorld, LOPWorld>(Lifetime.Singleton);
```
VContainer가 생성자의 `EntityRegistry`/`WorldEventBuffer`를 기존 싱글톤에서 주입.

### Runner가 World를 보유 + ticked (클·서 각 `LOPRunner`)
- `[Inject] private GameFramework.World.IWorld world;` 필드 추가.
- `UpdateRunner()`의 **결정론 sim 자리**(입력/AI 수집 후 ~ egress 전; 클라는 `UpdateAI()` 뒤·`SimulatePhysics()` 앞, 서버는 대응 위치 — plan이 각 루프 확정)에 호출:
  ```csharp
  world.Tick(Runner.Time.tick, (float)tickUpdater.interval);
  ```
  훅이 no-op이라 **실행해도 아무 상태 안 바뀜**(동작 보존). 호출 지점만 확립 → 4c가 채울 자리.

### 무변경
- 기존 루프 단계(UpdateEntity/SimulatePhysics/ProcessEvent 등) 본문·순서 그대로. 게임 로직 0줄 이동.
- Game/Room 레이어 무변경(③에서 다룸).

## 동작 보존 / 검증
- **동작 보존:** `world.Tick`이 빈 훅만 호출 → 상태 변경 0. 신규 타입은 보유·구동만, 로직 미이전.
- **컴파일:** 4 repo(GF/Shared/Client/Server) 코디네이트 후 양쪽 인스턴스 `refresh_unity`(scope=all,force) → `read_console` 에러 0. (LOP-Shared·GameFramework 변경이 클·서에 전파.)
- **런타임:** 플레이 시 이동/점프/물리/HP/데미지/죽음 **이전과 동일**. `IWorld` resolve 성공(NPE 없음), `world.Tick` 매 틱 호출되나 무효과.
- **유닛 테스트:** `WorldBase`는 순수 C# → GameFramework EditMode에서 "Tick이 훅을 Collection→Mutation→Detection 순서로 1회씩 호출" 테스트 추가 가능(골격 계약 고정). LOPWorld는 빈 골격이라 의미 테스트 없음.

## 산업 표준 매핑
- `Runner`(host)가 `World`(inner sim)를 보유·매틱 `Tick` = Quake `engine→G_RunFrame(qvm)` / Source `engine→GameFrame(server.dll)` / Unreal `UEngine::Tick→UWorld::Tick` / Quantum `Runner→Game` 와 동형(메모리 리서치).
- `World` = DOTS `World`/Unreal `UWorld`(엔티티+시스템 담는 틱 단위). `EntityRegistry`(≈DOTS `EntityManager`)를 보유.
- 공유 1벌(LOP-Shared) = Quake `qvm`/Overwatch 단일 ECS World 코드 — 양쪽 동일 컴파일로 결정론.
- Collection→Mutation→Detection = `world-core-connection-architecture.md` Generation 페이즈.

## Out of Scope (다음)
- **4c — 로직 흡수:** 매니저(combat/movement/entity update 등)를 `LOPWorld`의 Collection/Mutation/Detection 훅으로 *하나씩* 이전. 각자 별도 슬라이스. **이번엔 0줄.**
- **③ Game→Runner 합치기(2층화):** 진행자층 흡수, Room↔Game 배선 재정리. 별도 spec.
- **상태 소유권 통합:** EntityRegistry/WorldEventBuffer를 sim이 소유하게 바꾸는 것. 지금은 참조만.
- **Snapshot/Restore·예측·롤백·통합 fan-out:** Stage④.
- **물리/입력/네트워크 어댑터를 World가 구동:** 4c~4d.

## Open Questions (plan에서 해소)
- `Tick` 시그니처: `long tick, float dt` 채택(LOP가 long tick) — connection-arch 문서의 `int`는 예시. 확정.
- `world.Tick` 정확한 삽입 위치(클·서 각 `UpdateRunner` 루프) — no-op이라 동작 무관하나 결정론 자리에 둠. 서버 루프 구조는 plan이 grep 확정.
- 페이즈 훅 3개(Collection/Mutation/Detection) 고정 vs Cascade 추가 — 4b는 3개 no-op. Cascade는 4c 필요 시.
- LOP-Shared `Tests` EditMode에 WorldBase 골격 테스트를 둘지(GameFramework EditMode가 더 적합 — WorldBase가 거기 삶).

## 진행
- [x] 브레인스토밍 합의 (Runner→World 2층, ticked, IWorld/WorldBase/LOPWorld, 네임스페이스 World 유지)
- [x] 이 spec 작성
- [x] spec self-review
- [x] 사용자 spec 리뷰
- [x] `writing-plans`로 구현 plan 작성 (`docs/superpowers/plans/2026-06-23-world-sim-extraction.md`)
