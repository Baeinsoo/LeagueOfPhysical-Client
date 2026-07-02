# 4e-2: 대시 World 직접 쓰기 + 명시적 velocity 브릿지 + MotionEffectHandler Shared 통합

**Date:** 2026-07-02
**Branch (제안):** `feature/4e-dash-world-bridge` (GameFramework 무 / LOP-Shared + Client + Server 원자적 한 묶음)
**Related:** 선행 [4e-1 속도 적용 World-기반](2026-07-02-4e-velocity-apply-to-world-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · 메모리 [[world-core-runner-world-naming]] · [[ability-statuseffect-world-core]]

## Goal

대시(`MotionEffectHandler`)가 `ctx.EntityManager → LOPEntity` 파사드 대신 **`ctx.Caster`(World.Entity)의 Velocity/Transform에 직접** 읽고 쓰게 해 **엔진(Rigidbody)·LOPEntity 완전 비참조**로 만든다. World 직접 쓰기는 `PropertyChange`를 안 울리므로, `Simulate` 전 **명시적 World→Rigidbody velocity 브릿지**(`SyncPhysics`의 대칭짝)를 `LOPEntityController`에 추가하고, 이제 중복인 `PhysicsComponent.OnPropertyChange`의 velocity case를 제거한다. 대시가 엔진-프리가 되므로 **클·서 중복 2벌을 LOP-Shared 1벌로 통합**한다. 동작 100% 보존.

## 배경 / 동기

4e-1로 커널·대시가 `entity.velocity`(World 파사드)에 쓰고, 반응 `PhysicsComponent.OnPropertyChange`가 World→Rigidbody를 이었다. 다음 단계 = 대시를 **완전 World**로:
- 대시가 `ctx.EntityManager`(host, "interim" — DI 순환 회피용 잔재)를 안 쓰게 → `ctx.Caster.Get<Velocity>()` 직접.
- World 직접 쓰기 → PropertyChange 미발화 → **명시적 브릿지** 필요(반응 동기 대체).
- 엔진-프리가 된 핸들러 → **Shared 1벌 통합**(공유 `MovementSystem`과 같은 결).

이는 4e(어빌리티 로직을 `LOPWorld.Tick`으로 이주)로 가는 발판 — effect 핸들러가 World-only가 되면 sim 안으로 옮길 수 있다.

### 조사 결론 (등가 verified — Explore 2026-07-02)
- **reconciler는 Rigidbody 직접 안 만짐**, 전부 `entity.velocity`(World 파사드). reconcile는 **End(=Simulate 후)** 실행 → 속도 보정은 다음 틱 브릿지가 rb에 반영(등가).
- **Simulate 전 velocity write = 커널(ProcessInput) + 대시(DriveAbilityEffects) 둘뿐** → 브릿지를 `BeforePhysicsSimulation`(Simulate 직전)에 두면 둘 다 커버.
- **게임로직이 `rb.linearVelocity`를 읽는 곳 없음**(SyncPhysics만 읽어 World로 되씀) → 브릿지로 rb 갱신 시점이 늦어져도 무영향.
- **`ctx.Caster` = `GameFramework.World.Entity`** → `ctx.Caster.Get<Velocity/Transform>()` 가능. `ctx.EntityManager`는 오직 LOPEntity 파사드 획득용(제거 가능).
- **PhysicsComponent velocity case만 제거 안전**(position/rotation 반응 유지, 초기 시드는 Initialize).

## 현재 상태 (실측)

- `MotionEffectHandler`(클·서 바이트 동일, `Assets/Scripts/Game/`): `ctx.EntityManager.GetEntity<LOPEntity>` → `entity.rotation` 읽고 `entity.velocity` 씀(4e-1 후). `AbilityEffectHandler<MotionEffect>` 상속, parameterless.
- `MotionEffect`(`LOP.MotionEffect`, LOP-Shared `Ability/AbilityEffect.cs:39`): `float Speed`. `AbilityEffectHandler<T>` base = Shared `Ability/IAbilityEffectHandler.cs`.
- `LOPEntityController`(클·서 동일): `[RunnerListen(AfterPhysicsSimulation)] → entity.SyncPhysics()`. `using LOP.Event.LOPRunner.Update;` 있음.
- `PhysicsComponent.OnPropertyChange`(클·서): position/rotation/velocity 3 case. velocity case = `entityRigidbody.linearVelocity = entity.velocity`. `Initialize`가 rb를 entity 모션으로 시드.
- `BeforePhysicsSimulation` 이벤트 struct 존재(클·서 `Event.LOPRunner.Update.cs`), `SimulatePhysics`가 `Simulate` 직전 dispatch.
- DI 등록(클 `GameLifetimeScope:43` / 서 `:35`): `builder.Register<MotionEffectHandler>(Lifetime.Singleton).As<IAbilityEffectHandler>();` — 타입명 `LOP.MotionEffectHandler`.

## 설계

### ① LOP-Shared — `MotionEffectHandler` 신설 (World-only)

`Runtime/Scripts/Game/Ability/MotionEffectHandler.cs` (StatusEffectApplyEffectHandler 옆):
```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// MotionEffect 핸들러(코어). Active 동안 매 틱 시전자가 바라보는 방향으로 그 속도로 민다(대시).
    /// World.Velocity(진실원본)에 직접 쓴다 → 호스트 velocity 브릿지가 Rigidbody에 반영.
    /// 엔진(Rigidbody)·LOPEntity·EntityManager 비참조 = 클·서 공유 1벌.
    /// </summary>
    public class MotionEffectHandler : AbilityEffectHandler<MotionEffect>
    {
        protected override void OnActiveTick(AbilityEffectContext ctx, MotionEffect effect)
        {
            var worldTransform = ctx.Caster.Get<GameFramework.World.Transform>();
            var worldVelocity = ctx.Caster.Get<GameFramework.World.Velocity>();
            if (worldTransform == null || worldVelocity == null)
            {
                return;
            }

            Vector3 forward = worldTransform.Rotation.ToUnity() * Vector3.forward;
            Vector3 target = new Vector3(forward.x, 0f, forward.z).normalized * effect.Speed;

            Vector3 v = worldVelocity.Linear.ToUnity();
            v.x = target.x;
            v.z = target.z;
            worldVelocity.Linear = v.ToNumerics();
        }
    }
}
```
- `namespace LOP` — 클·서 DI 등록(`Register<MotionEffectHandler>`)이 그대로 이 Shared 타입을 resolve(등록 무변경).
- LOP-Shared는 UnityEngine 참조(공유 `MovementSystem`이 이미 사용) → Vector3/Quaternion 사용 OK. `ToUnity`/`ToNumerics`(GameFramework)로 World(Numerics)↔Unity 변환.
- Y 보존(수평 x/z만 세팅).

### ② 클·서 로컬 `MotionEffectHandler` 삭제

`{Client,Server}/Assets/Scripts/Game/MotionEffectHandler.cs` (+ `.meta`) **둘 다 삭제**. ⚠️ **원자적**: Shared 타입 추가 + 로컬 삭제를 한 커밋에 — 한쪽만 지우면 그 사이드에서 `LOP.MotionEffectHandler` 중복(로컬+Shared) 컴파일 에러. DI 등록 라인은 무변경(Shared로 resolve).

### ③ 명시적 velocity 브릿지 — `LOPEntity` + `LOPEntityController` (클·서)

`LOPEntity`에 `SyncPhysics`(rb→World)의 대칭짝 추가:
```csharp
/// <summary>World.velocity를 물리 바디에 밀어넣는다(Simulate 직전 호출). SyncPhysics(rb→World)의 역방향.</summary>
public void PushVelocityToPhysics()
{
    PhysicsComponent physicsComponent = this.GetEntityComponent<PhysicsComponent>();
    if (physicsComponent == null)
    {
        return;
    }
    physicsComponent.entityRigidbody.linearVelocity = velocity;
}
```
`LOPEntityController`에 핸들러 추가(`OnUpdateAfterPhysicsSimulation` 옆):
```csharp
[RunnerListen(typeof(BeforePhysicsSimulation))]
private void OnBeforePhysicsSimulation()
{
    entity.PushVelocityToPhysics();
}
```
- 타이밍: `BeforePhysicsSimulation`(Simulate 직전) → 커널·대시의 모든 velocity write 이후. reconcile(End)의 write는 다음 틱 브릿지가 반영(등가).
- position/rotation은 반응 PropertyChange 유지(이 슬라이스 무변경) — velocity만 브릿지로.

### ④ `PhysicsComponent.OnPropertyChange` — velocity case 제거 (클·서)

```csharp
case nameof(entity.velocity):
    entityRigidbody.linearVelocity = entity.velocity;
    break;
```
**삭제**. position/rotation case 유지. (velocity는 이제 브릿지 단일 경로. 초기 시드는 `Initialize`의 `entityRigidbody.linearVelocity = entity.velocity` 유지.)

### 안 바뀌는 것 / 잔재
- 커널(`LOPMovementManager`)은 그대로 `entity.velocity`(파사드) 씀 — World에 들어가고 브릿지가 rb 반영(velocity PropertyChange는 이제 무소비=무해 fire; 커널 World-직접화는 후속 정리).
- reconciler/AI: 무변경(World 파사드).
- `ctx.EntityManager`는 context에 **유지** — 서버 `DamageEffectHandler`가 아직 사용(그건 별도 후속에 World화). MotionEffectHandler만 사용 중단.
- `MotionEffect` 도크(`AbilityEffect.cs:38` "실제 push는 side 핸들러")는 이제 부정확 → "핸들러가 World.Velocity에 push"로 정정.

## 동작 보존 / 검증

- **보존 논거:** (조사 결론) 모든 velocity write는 결국 같은 `World.Velocity` 컴포넌트에 반영되고, 브릿지가 Simulate 직전 rb에 넣는다. 게임로직이 rb.velocity를 안 읽어 rb 갱신 시점 지연이 무영향. 대시는 같은 World.Velocity에 쓰되 경로만 직접(EntityManager 우회) — 값 동일.
- **원자적 양쪽 슬라이스:** Shared 통합 때문에 클라-먼저 불가 → 클·서·Shared 함께 편집 후 **양쪽 컴파일 검증** + 플레이 검증.
- **검증:** 클·서 refresh + `read_console` 에러 0(특히 MotionEffectHandler 중복/미해결, BeforePhysicsSimulation·PushVelocityToPhysics 해석). 플레이: **대시**(방향·거리·속도 동일), 이동/점프/reconciliation 무회귀, 아이템(kinematic rb에 velocity push=무해) 정상, 콘솔 에러 0.
- **테스트:** 적용부/컨트롤러는 Assembly-CSharp라 asmdef 테스트 불가. Shared MotionEffectHandler는 World 조작이라 이론상 EditMode 가능하나 동작 보존 슬라이스 — 신규 테스트는 만들지 않음(가짜 금지). 공유 `MovementSystem` 테스트 무영향.

## 산업 표준 매핑
- effect 핸들러가 엔진 상태(Rigidbody) 대신 도메인 상태(World.Velocity)를 조작 = Quake `bg_pmove`/Source `CGameMovement`가 playerState.velocity를 다루고 엔진이 적분하는 것과 동형. World→엔진 명시적 sync(브릿지) + 엔진→World(SyncPhysics)의 대칭 쌍은 시뮬-엔진 경계의 표준 어댑터(DOTS `PhysicsWorld` export/import, Unreal `SyncBodies`).

## Out of Scope (후속)
- **커널 World 직접 쓰기**(entity.velocity 파사드 → World.Velocity 직접) — velocity PropertyChange 무소비 fire 제거.
- **position/rotation도 브릿지로**(반응 PropertyChange 전부 제거) — 이번은 velocity만.
- **`ctx.EntityManager` 완전 제거** — 서버 `DamageEffectHandler`를 World화한 뒤.
- **커널·대시·판정을 `LOPWorld.Tick`으로 이주**, **Physics.Simulate를 sim이 소유**.

## Open Questions
- 브릿지가 kinematic 아이템에도 매 틱 `rb.linearVelocity = 0` 세팅(무해, kinematic 무시). 가드(isKinematic 스킵) 추가 여부 — 추천: 미추가(단순, 무해).
- `PushVelocityToPhysics` 명명 — `SyncVelocityToPhysics`/`ApplyVelocityToPhysics` 대안. (SyncPhysics 짝이라 Push/Apply가 방향 명확.)

## 진행
- [x] 브레인스토밍 — 대시 World 직접, 브릿지=LOPEntityController, MotionEffectHandler Shared 통합(사용자 확정)
- [x] reconciler 타이밍·ctx.Caster·PhysicsComponent 실측(등가 verified)
- [x] 이 spec 작성
- [ ] 구현(원자적 클·서·Shared) + 컴파일 + 플레이 검증
