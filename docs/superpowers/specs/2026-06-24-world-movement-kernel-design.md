# 이동 결정론 커널 공유 — 클·서 1벌 (Slice 4c #1)

**Date:** 2026-06-24
**Branch (제안):** `feature/world-movement-kernel` (LeagueOfPhysical-Shared / Client / Server)
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (Engine↔Simulation·Generation 페이즈) · [Netcode 재설계](../../netcode-redesign.md) (§3.2 점프, §6.2 클·서 동시수정) · [LOP 저장소 토폴로지](../../lop-repo-topology.md) (LOP-Shared = 공유 시뮬) · 프로젝트 메모리 `world-core-runner-world-naming` · 선행: 4b 시뮬 추출(완료, main — `LOPWorld` 빈 골격 + `world.Tick` 배선)

## Goal

클·서에 **각자 복사본으로 존재해 갈라질 위험이 있는** 이동 계산 로직(`LOPMovementManager.ProcessInput`의 순수 계산부)을 **LOP-Shared의 순수 함수 1벌**로 추출한다. 클라·서버 양쪽 매니저가 이 한 벌을 호출 → *이동 로직이 글자 그대로 같은 코드*가 되어 결정론이 구조적으로 강제된다. **동작 100% 보존**(계산식 그대로 이전, 적용·물리는 무변경).

이 슬라이스의 가치는 *예측·보정의 토대가 되는 이동 로직을 클·서 단일 진실원본으로 만드는 것*이다.

## 배경 / 동기

### 왜 이동부터인가 (리서치 결론)

참조한 모든 fast-paced 게임이 **결정론 게임 로직을 한 곳에 두고 클·서가 똑같이 돌린다.** 그리고 그중 **이동이 거의 항상 "1번 공유 대상"**이다 — 클라가 실제로 *예측*하는 게 이동이기 때문:

- **Quake 3**: `bg_pmove.c` — 파일명의 `bg`가 *"both games"*. `playerState + usercmd → playerState'` **순수 함수**, server(`game`)·client(`cgame`) 모듈 **양쪽에 똑같이 컴파일**. 이동만 공유, 나머지는 분리.
- **Source / CS (Valve)**: `CGameMovement::ProcessMovement` — client.dll·server.dll 양쪽에 같이 들어가 예측 시스템 아래 동일 실행.
- **Overwatch** (GDC 2017): 클라 ~46 시스템 중 넷코드(예측·롤백) 대상은 **3개(이동·무기·스테이트스크립트)**뿐. 예측 = 서버 스냅으로 되돌린 뒤 *같은 결정론 이동 심을 재생*.
- **Photon Quantum**: 시뮬 전체가 순수 C# 1벌. (가장 강한 형태.)

### 클라의 결정론은 "얇다" — 오해 방지

이 클라이언트는 **전투·데미지·사망의 결정론 권위가 아니다.** 그건 서버가 계산해 메시지(`DamageEventToC`)로 보내고, 클라는 `WorldEventBuffer`에 담아 *연출만* 한다(HP = 스냅샷 권위). → 클라 `LOPWorld`로 흡수할 "결정론"의 실체는 **이동 / 엔티티 per-tick 틱(Status·Action) / 물리 구동** 정도로 얇다.

"공유 시뮬"의 정확한 의미는 **코드 한 벌을 서로 다른 *엔티티 범위*에 돌리는 것**이다:

```
같은 이동 커널(한 벌)
   ├─ 서버:  모든 엔티티에 적용  (전부 = 권위)
   └─ 클라:  내 캐릭터에만 적용  (예측 1개)
```

클라가 세상 전체를 시뮬하는 lockstep(Quantum, 인원 bounded)이 **아니라** server-authoritative + 내 캐릭 예측(오버워치/Source/Quake/LOP — Area of Interest로 수십·수백 확장). 결정론은 *예측하는 부분(내 캐릭 이동)*에만 필요하고, 그건 내 입력만으로 자기완결되므로 클라가 남을 안 굴려도 안 깨진다. → **이동 커널을 공유하는 건 "그 한 개를 예측할 때 서버와 똑같은 코드로 굴려 어긋나지 않게" 하려는 것**이지, 클라에 세상 전체를 떠넘기는 게 아니다.

### 지금 코드의 위험

`LOPMovementManager.ProcessInput`(클·서 각자 복사본)이 갈라질 위험을 넷코드 문서 §6.2가 명시: *"`LOPMovementManager`는 클·서 양쪽에 같은 코드가 있을 수 있으니 동시 수정."* 수기 동기화는 깨지기 쉽다 — 한 벌로 묶으면 구조가 강제한다.

## 현재 상태 (실측)

클라 `Assets/Scripts/Game/LOPMovementManager.cs` (`: IMovementManager<LOPEntity>`):

```csharp
public void ProcessInput(LOPEntity entity, EntityTransform entityTransform,
                         float horizontal, float vertical, bool jump)
{
    // CharacterComponent / PhysicsComponent 가드 (없으면 throw)
    Vector3 direction = new Vector3(horizontal, 0, vertical).normalized;
    if (direction.sqrMagnitude > 0)
    {
        var velocity = direction * characterComponent.masterData.Speed;   // 속도 = 방향 × Speed
        entity.velocity = new Vector3(velocity.x, entity.velocity.y, velocity.z); // y 유지
        float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        entity.rotation = new Vector3(0, angle, 0);                       // 결정론 snap (Phase 1)
    }
    if (jump)
    {
        rb.linearVelocity -= new Vector3(0, rb.linearVelocity.y, 0);      // y속도 0 리셋
        rb.AddForce(Vector3.up * characterComponent.masterData.JumpPower, ForceMode.Impulse);
    }
}
```

- **순수 계산부 vs 적용부가 이미 나뉘어 있다**: 방향→속도·회전·점프결정 = *계산* / `entity.velocity`/`rotation` 세팅(이게 `EventBus.Publish(PropertyChange)` 연출을 깨움) + `Rigidbody.AddForce` = *적용*.
- **입력 출처**: `PlayerInputManager`가 입력 캡처 후 이 메서드 호출(예측 적용). 서버는 네트워크 입력 큐에서.
- **서버 측**: 대응 `LOPMovementManager` 복사본 존재(넷코드 §6.2). 정확한 시그니처·미세 차이는 plan에서 grep 확정.
- **4b 골격**: `LOPWorld : WorldBase`(LOP-Shared, 클·서 공유 1벌, 빈 훅) + `LOPRunner`가 `world.Tick()` 매틱 호출(현재 no-op).

## 설계

### 접근 A — 순수 커널 추출 (이번 범위)

| 구분 | 어디로 | 무엇 |
|---|---|---|
| **순수 이동 계산** | **LOP-Shared (한 벌)** | `(현재 velocity, h, v, jump, speed, jumpPower) → 이동 결과(새 velocity, 회전, 점프 여부)` 순수 함수. `UnityEngine.Vector3`/`Mathf`만 사용 — **`MasterData`·`LOPEntity`·`Rigidbody` 미참조**(primitives in/out). |
| **적용** | **host (클·서 각자)** | 결과를 `entity.velocity`/`rotation`에 세팅(EventBus 연출 동반), 점프는 `Rigidbody.AddForce`. 무변경. |
| **물리 적분** | **host** | `Physics.Simulate` 그대로. |

**왜 primitives in/out**: Quake `bg_pmove`가 `playerState`(엔진 비의존 순수 데이터)만 만진 것과 동형. 커널이 `LOPEntity`/`Rigidbody`/`MasterData`(전부 클·서 특화 or 무거운 의존)를 모르게 해야 LOP-Shared 1벌로 깨끗이 산다. host가 그 값들을 *추출해 넘기고*, 결과를 *받아 적용*한다.

**커널 시그니처 (제안 — 명명은 Open)**:
```csharp
namespace LOP
{
    // 순수 C#. 클·서 공유 1벌.
    public static class MovementSystem   // 명명: 업계어 ProcessMovement(Source)/Pmove(Quake) 검토 — Open
    {
        public static MovementResult ProcessMovement(in MovementInput input);
    }

    public readonly struct MovementInput   // primitives only
    {
        public readonly UnityEngine.Vector3 currentVelocity;
        public readonly float horizontal, vertical;
        public readonly bool jump;
        public readonly float speed, jumpPower;
        // ctor ...
    }

    public readonly struct MovementResult
    {
        public readonly bool hasMove;                 // direction.sqrMagnitude > 0
        public readonly UnityEngine.Vector3 velocity; // 새 수평 속도(y는 currentVelocity.y 유지)
        public readonly UnityEngine.Vector3 rotation; // (0, angle, 0)
        public readonly bool jump;                    // host가 AddForce 적용
        // ctor ...
    }
}
```
> 점프의 `y속도 0 리셋 + 임펄스`는 host가 `Rigidbody`로 적용(커널은 `jump` 여부만 전달) — 임펄스는 PhysX API라 host 책임. y리셋도 host가 점프 시 수행. *결정론 결정(점프할지)*은 커널, *물리 적용*은 host로 가른다. (대안: 커널이 y리셋 포함 새 velocity까지 반환 — plan에서 둘 중 택1.)

### host 호출부 (클·서 각 `LOPMovementManager.ProcessInput`)

```csharp
var result = MovementSystem.ProcessMovement(new MovementInput(
    entity.velocity, horizontal, vertical, jump,
    characterComponent.masterData.Speed, characterComponent.masterData.JumpPower));

if (result.hasMove)
{
    entity.velocity = result.velocity;   // EventBus 연출은 여기서(host)
    entity.rotation = result.rotation;
}
if (result.jump)
{
    rb.linearVelocity -= new Vector3(0, rb.linearVelocity.y, 0);
    rb.AddForce(Vector3.up * characterComponent.masterData.JumpPower, ForceMode.Impulse);
}
```
가드(`CharacterComponent`/`PhysicsComponent` 존재)는 host 잔류. **계산식은 0줄 바뀌지 않고 위치만 커널로 이동** → 동작 보존.

### asmdef / 배치

- 커널 = LOP-Shared `Runtime/Scripts/Game/`(LOPWorld 옆). LOP 특화(Speed/JumpPower 의미는 LOP)라 GameFramework 아님, LOP-Shared.
- LOP-Shared.Runtime은 이미 `UnityEngine`(noEngineReferences:false) 사용 가능 → `Vector3`/`Mathf` OK. 추가 asmdef 참조 불필요(GameFramework.World는 4b에서 이미 추가됨, 이번 커널은 그것도 불필요).

### LOPWorld 훅은 이번엔 안 채운다

커널은 **매니저가 호출**한다(`LOPWorld.Mutation`이 직접 부르지 않음). 이유: `LOPWorld.Mutation`이 이동을 *소유*하려면 per-entity 입력·속도를 World 데이터로 읽어야 하는데, 속도/위치 권위가 아직 `Rigidbody`(클라 전용)에 있음 → 그 데이터를 World 컴포넌트로 옮기는 **접근 B = Stage④**. 이번은 *커널 1벌 통일*만으로 가치를 확정하고, 훅 배선은 데이터가 World로 들어온 다음 슬라이스에 연결한다. (4b가 깐 `world.Tick` no-op 자리는 그대로 둔다.)

## 동작 보존 / 검증

- **동작 보존:** 계산식을 글자 그대로 커널로 옮기고 host는 같은 값을 같은 곳에 적용 → 이동/회전/점프 *이전과 동일*.
- **단위 테스트(신규 가치):** 커널이 순수 함수라 LOP-Shared(또는 GameFramework) EditMode로 단위 테스트 가능 — 입력→예상 velocity/rotation/jump 검증. *이전엔 매니저가 LOPEntity/Rigidbody에 묶여 불가했던* 테스트가 처음으로 가능해짐(추가 가치).
- **클·서 동일성:** 양쪽 매니저가 같은 커널 호출 → 이동 로직 단일 진실원본. 회귀 시 한 곳만 고침.
- **컴파일:** 3 repo(Shared/Client/Server) 코디네이트 후 양쪽 인스턴스 `refresh_unity`(scope=all, force) → `read_console` 에러 0.
- **런타임:** 플레이 시 이동/회전/점프 *이전과 동일*. (특히 §3.2 낙하 중 점프 거동 — 커널 공유는 *디자인을 바꾸지 않음*, 클·서 일치만 보장.)

## 산업 표준 매핑

- **순수 이동 함수 공유** = Quake `bg_pmove`("both games", `playerState+usercmd→playerState'`) / Source `CGameMovement::ProcessMovement`(client·server dll 공유) 직접 적용. → 메서드명 후보 `ProcessMovement`(Source)/`Pmove`(Quake).
- **primitives in/out**(엔진 객체 비참조) = `bg_pmove`가 `playerState` 순수 데이터만 만진 것과 동형.
- **적용·연출은 host, 계산은 공유** = `world-core-connection-architecture.md`의 "sim은 순수·결정론, egress/연출은 외각" 분리.
- **클·서 1벌(LOP-Shared)** = Quake `qvm`/Overwatch 단일 ECS 코드 — 양쪽 동일 컴파일로 결정론.

> **결정론의 범위 (과장 방지):** 이 슬라이스가 얻는 결정론 = *로직 단일화*(클·서가 같은 계산식)이지, 플랫폼 간 **bit-identical float 보장이 아니다.** LOP는 PhysX 부동소수점 비결정성을 lockstep이 아니라 **server-authoritative + reconciliation**으로 흡수한다(netcode §3.5). 커널 공유는 *코드가 갈라져 생기는* 발산을 없애는 것 — float 미세오차는 여전히 보정(reconcile) 담당. (Quantum식 fixed-point 결정론은 LOP 비채택.)

## Out of Scope (다음)

- **접근 B — `LOPWorld`가 이동을 직접 소유:** 속도/위치 권위를 `Rigidbody → World.Velocity/Transform` 컴포넌트로 이전 + `LOPWorld.Mutation`이 World.Entity 순회하며 커널 구동 + 물리가 World 속도를 읽고 적분. **Stage④**(위치 진실원본 이전)와 묶임.
- **다른 매니저 흡수:** 엔티티 per-tick 틱(Status/Action), 물리 구동 호출 relocation 등 — 각자 별도 슬라이스.
- **점프 vy 게임디자인(§3.2/Phase 5):** 낙하 중 점프 세기 — 커널 공유와 무관한 게임플레이 콜.
- **클라 예측·롤백·Snapshot/Restore:** Stage④. (이 슬라이스는 그 *토대*인 이동 단일 진실원본만.)

## Open Questions (plan에서 해소)

- **커널 명명**: 타입 `MovementSystem` vs 업계어 직접(`Pmove`)? 메서드 `ProcessMovement`(Source 정합) 채택 여부. CLAUDE.md "업계 표준 명명" 규칙 — plan에서 확정.
- **점프 처리 분할**: 커널이 `jump` 플래그만 반환(host가 y리셋+임펄스) vs 커널이 y리셋 포함 새 velocity까지 반환. 동작 동일하되 경계 위치 차이.
- **서버 매니저 미세 차이**: 서버 `LOPMovementManager` 복사본이 클라와 *정확히* 동일한지 grep 확인 — 다르면 어느 쪽이 정답인지(커널 1벌로 합치며 결정).
- **커널 위치**: LOP-Shared `Runtime/Scripts/Game/`(LOPWorld 옆) 확정. 별도 `Movement/` 하위 폴더 둘지.
- **테스트 위치**: LOP-Shared Tests.EditMode vs GameFramework — 커널이 LOP-Shared에 사니 전자가 자연스러움(asmdef 있으면).

## 진행

- [x] 브레인스토밍 합의 (이동 커널 공유 = 접근 A, LOPWorld 훅은 다음, 접근 B=Stage④)
- [x] 이 spec 작성
- [x] spec self-review (결정론 범위 명확화, 과장 방지)
- [ ] 사용자 spec 리뷰
- [x] 구현 plan 작성 (`docs/superpowers/plans/2026-06-24-world-movement-kernel.md`)
