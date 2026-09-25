# 4e-1: 속도 적용을 World-기반으로 (적용 방향 뒤집기)

**Date:** 2026-07-02
**Branch (제안):** `feature/4e-velocity-apply-world` (LeagueOfPhysical-Client, 이후 Server 미러)
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (Stage④ · 4e 얇은 호스트) · 선행 [Motion 권위 → World.Entity](2026-07-01-stage4-motion-world-authority-design.md) · 메모리 [[world-core-runner-world-naming]] · [[ability-statuseffect-world-core]]

## Goal

두 속도 "적용부"(이동 커널 `LOPMovementManager`, 대시 `MotionEffectHandler`)가 **`Rigidbody.AddForce` 대신 `entity.velocity`(World.Velocity 파사드)에 쓰도록** 뒤집는다. 그러면 속도 흐름이 **게임 로직 → World.velocity → (PhysicsComponent 반응 동기) → Rigidbody → Simulate → World** 가 되어, 적용 로직이 **엔진(Rigidbody)-프리**가 된다. 동작 100% 보존. **클라 먼저, 서버 미러.**

이건 4e(물리·어빌리티 로직을 공유 시뮬 `LOPWorld.Tick`으로 흡수)의 **첫 전제 슬라이스** — 로직이 Rigidbody를 안 만져야 `LOPWorld`(엔진-프리)로 옮길 수 있다.

## 배경 / 동기 — 왜 "적용 방향 뒤집기"인가

Motion 권위 이전(선행 슬라이스)으로 위치/속도 *데이터*는 이제 `World.Entity`가 소유한다. 하지만 속도 *적용*은 아직 Rigidbody 경유다:

```
[지금]  커널·대시 ──AddForce──▶ Rigidbody ──Simulate──▶ SyncPhysics ──▶ World
        (로직이 Rigidbody 직접 씀 = 엔진 결합)
```

`LOPWorld`(LOP-Shared, `noEngineReferences`)는 Rigidbody/LOPEntity/PhysicsComponent를 참조할 수 없다. 그래서 커널·대시가 Rigidbody에 `AddForce`하는 한 `LOPWorld`로 못 옮긴다. **적용을 World.velocity 쓰기로 바꾸면** 로직이 엔진-프리가 되어 이주 가능해진다.

### ⭐ 핵심 발견 — 브릿지가 이미 존재

`entity.velocity`를 세팅하면 파사드 setter가 `PropertyChange` 이벤트를 발화하고, **`PhysicsComponent.OnPropertyChange`가 이미 `rb.linearVelocity = entity.velocity`로 반응 동기**한다(클·서 PhysicsComponent에 존재). 즉 **World→Rigidbody 브릿지가 이미 깔려 있어** 별도 스텝 불필요 — `AddForce`를 `entity.velocity =`로 바꾸기만 하면 반응 동기가 Rigidbody에 넣어준다. (reconciler·AI가 이미 `entity.velocity` 쓰기 → rb 동기하는 것과 동일 경로.)

## 현재 상태 (실측)

### 이동 커널 — `LOPMovementManager.ProcessInput` (클·서 동일)
- `:44-45` 이동: `Vector3 delta = result.velocity - entity.velocity; rb.AddForce(new Vector3(delta.x, 0f, delta.z), VelocityChange);` — **수평(x/z)만**, Y는 중력에 맡김(보존).
- `:46-49` 회전: `if (result.hasRotation) entity.rotation = result.rotation;` — **이미 World 파사드 기반**(무변경).
- `:52-57` 점프: `rb.AddForce(Vector3.up * (jumpSpeed - rb.linearVelocity.y), VelocityChange);` — Y를 `jumpSpeed`(=`characterComponent.masterData.JumpPower`)로 세팅.
- `:25-28` `PhysicsComponent` 존재 체크 — AddForce/점프용. 적용 전환 후 **불필요**(제거 대상).
- `:30-34` 대시(모션 어빌리티) Active면 early-return(입력 이동 무시) — 유지.

### 대시 — `MotionEffectHandler.OnActiveTick` (클·서 동일)
- `:22-26`: `forward = Quaternion.Euler(entity.rotation)*Vector3.forward; target = (forward.x,0,forward.z).normalized * effect.Speed; delta = target - rb.velocity(x,z); rb.AddForce((delta.x,0,delta.z), VelocityChange);` — 바라보는 방향 수평 속도로 세팅(Y 보존).
- `ctx.EntityManager.GetEntity<LOPEntity>(ctx.Caster.Id)`로 LOPEntity 획득 + `PhysicsComponent` — `ctx.EntityManager`는 "interim"(DI 순환 회피, 메모리 박제).

### 반응 브릿지 — `PhysicsComponent.OnPropertyChange` (클·서 동일)
- `entity.velocity` 세팅 → `PropertyChange("velocity")` → `rb.linearVelocity = entity.velocity`. (position/rotation도 동일.)

### 다른 속도 writer (전환 대상 아님 — 이미 World)
- reconciler(`SnapReconciler`/`ServerStateReconciler`): `entity.velocity =`(파사드) — 이미 World. AI(`EnemyBrain`, 서버): `entity.velocity/rotation =` — 이미 World. → **Rigidbody 직접 쓰는 로직은 커널·대시 둘뿐**(전환 대상 확정).

## 설계

### ① `LOPMovementManager.ProcessInput` — AddForce → `entity.velocity` (클·서)

이동·점프를 **하나의 `entity.velocity` 쓰기**로 통합(수평은 result, Y는 점프면 jumpSpeed 아니면 보존):
```csharp
// (CharacterComponent 체크는 유지 — JumpPower 필요. PhysicsComponent 체크·변수는 제거.)
// ... HasActiveMotionEffect early-return, worldStats/speed, MovementSystem.ProcessMovement 는 무변경 ...

Vector3 velocity = entity.velocity;
velocity.x = result.velocity.x;
velocity.z = result.velocity.z;
if (jump)
{
    velocity.y = characterComponent.masterData.JumpPower;
}
entity.velocity = velocity;   // World 쓰기 → PropertyChange → rb.linearVelocity

if (result.hasRotation)
{
    entity.rotation = result.rotation;
}
```
- `entity.TryGetComponent<PhysicsComponent>(...)` 체크(현 25-28행) **제거** — 더는 안 씀.
- `using UnityEngine;` 유지(Vector3). 커널은 이제 Rigidbody 미참조 = 엔진-프리 방향으로 한 걸음(단 LOPEntity/CharacterComponent는 여전히 참조 — 완전 이주는 후속).

> **등가 논거:** 현 `AddForce((delta.x,0,delta.z),VelocityChange)`는 `rb.velocity.x/z += (result - 현재).x/z` = `rb.velocity.x/z = result.x/z`(적용 시점 `entity.velocity==rb.velocity`). Y는 안 건드림. 새 코드는 `velocity.x/z=result.x/z`, Y는 보존(점프면 jumpSpeed) → **동일**. 점프: 현 `AddForce(up*(jumpSpeed-rb.vy))` = `rb.vy=jumpSpeed`, 새 `velocity.y=jumpSpeed` → **동일**.

### ② `MotionEffectHandler.OnActiveTick` — AddForce → `entity.velocity` (클·서)

```csharp
var entity = ctx.EntityManager.GetEntity<LOPEntity>(ctx.Caster.Id);
if (entity == null)
{
    return;
}

Vector3 forward = Quaternion.Euler(entity.rotation) * Vector3.forward;
Vector3 target = new Vector3(forward.x, 0f, forward.z).normalized * effect.Speed;

Vector3 velocity = entity.velocity;
velocity.x = target.x;
velocity.z = target.z;
entity.velocity = velocity;   // World 쓰기 → PropertyChange → rb
```
- `PhysicsComponent` 체크·`rb`·`delta` 제거. `ctx.EntityManager` 의존은 **유지**(LOPEntity 파사드 접근용) — 이걸 없애 World 직접 쓰기로 가는 건 후속 슬라이스(명시적 브릿지와 함께).
- 등가: 현 `AddForce(target - rb.vel(x,z))` = `rb.vel.x/z = target.x/z`(Y 보존). `entity.velocity`(=rb.velocity, 대시 시점 동기) 읽어 x/z=target, Y 보존 → **동일**.

### 안 바뀌는 것
- `MovementSystem.ProcessMovement`(공유 순수 커널), 회전(`entity.rotation`, 이미 World), reconciler/AI(이미 World), `SyncPhysics`(rb→World, after-physics), `PhysicsComponent.OnPropertyChange`(반응 브릿지 — 이게 World→rb를 담당), `Physics.Simulate` 호출 위치(호스트), 틱 순서.

### 클라 먼저인 이유
Motion 슬라이스와 동일 — 각 사이드 적용부는 독립 사본, 결과 rb.velocity 동일 → 물리·와이어 무영향. 클라 검증 후 서버 미러(바이트 동일 편집).

## 동작 보존 / 검증

- **보존:** 두 적용부가 같은 시점에 같은 값을 rb에 반영(직접 AddForce → 파사드→PropertyChange→rb, 동기적). 이동/점프/대시 결과 velocity 동일. Y(중력)·충돌 물리·SyncPhysics 무변경.
- **검증:** 클라 컴파일 클린(`read_console`). 플레이: **이동/방향전환/점프/대시**가 이전과 시각·체감 동일, reconciliation(본인+타인) 무회귀, 콘솔 에러 0. (등가라 gap 변화 없어야 함.) 이후 서버 미러 후 서버 Play 포함 재검증.
- **테스트:** 적용부는 클·서 Assembly-CSharp라 asmdef 단위테스트 불가. 동작 보존 슬라이스 — 신규 테스트 없음(공유 `MovementSystem`은 무변경이라 기존 EditMode 그대로 유효).

## 산업 표준 매핑
- 로직이 "속도를 정함"(순수 데이터)과 엔진이 "적분함"(Rigidbody)을 분리 = Quake `bg_pmove`(playerState.velocity 순수 데이터)·Source `CGameMovement`(mv->m_vecVelocity 조작, 엔진이 적분)·DOTS(컴포넌트 velocity → PhysicsWorld). 적용부가 엔진 상태 대신 도메인 상태(World.velocity)를 쓰는 것은 시뮬-엔진 분리의 표준 첫 단계.

## Out of Scope (후속 4e 슬라이스)
- **로직을 `LOPWorld.Tick`으로 이주** — 커널·대시·판정을 공유 시뮬 안으로(이 슬라이스는 적용부를 엔진-프리로 만들 뿐, 여전히 호스트가 호출).
- **`ctx.EntityManager` 제거 + World 직접 쓰기** — 대시가 LOPEntity 파사드 대신 `ctx.Caster.Get<Velocity>()` 직접. 이땐 반응 PropertyChange가 안 걸려 **명시적 World→Rigidbody 브릿지** 필요.
- **`Physics.Simulate` 호출을 `LOPWorld`가 소유**(IPhysicsSimulator via sim).
- **`UpdateEntity`(프레젠테이션)** — 영구 호스트.

## Open Questions
- 이동+점프를 단일 `entity.velocity` 쓰기로 통합(추천) vs 분리 2회 쓰기. (통합 = PropertyChange 1회, 더 깔끔. 등가.)
- 클·서 한 슬라이스로 묶기 vs 클라-먼저 분리 머지. (추천: 클라 먼저 검증 후 서버 미러 — Motion 슬라이스 패턴. 편집은 바이트 동일.)

## 진행
- [x] 브레인스토밍 — 4e 첫 슬라이스=적용 방향 뒤집기, 반응 브릿지 재사용, 클라 먼저 합의
- [x] 현재 적용 경로(커널/대시 AddForce)·반응 브릿지·타 writer 실측
- [x] 이 spec 작성 + 사용자 리뷰
- [x] **클라 구현 + 플레이 검증 + 서버 미러 (2026-07-02).** 커널·대시 `AddForce` → `entity.velocity`(World). 반응 브릿지(PhysicsComponent) 재사용. 클·서 바이트 동일 편집, 양쪽 컴파일 0에러. plan 없이 직접 구현(2파일, 동작 보존). **다음 4e: 로직을 LOPWorld.Tick으로 이주 / 대시 World 직접 쓰기 + 명시적 브릿지 / Physics.Simulate를 sim이 소유.**
