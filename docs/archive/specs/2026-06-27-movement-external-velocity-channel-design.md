# 이동 외력 속도 채널 (입력 + 외력) — dash 첫 사용처

**Date:** 2026-06-27
**Branch (제안):** `feature/movement-external-velocity` (LOP-Shared / Client / Server / infrastructure / MasterData-Client·Server)
**Related:** [Ability 페이즈 머신 설계](2026-06-27-ability-phase-machine-design.md) (Active 창 behavior) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [netcode-redesign](../../netcode-redesign.md) (예측/보정) · 메모리 [[ability-statuseffect-world-core]]
**Supersedes:** `docs/superpowers/plans/2026-06-27-dash-ability.md`의 `AddForce`(impulse) 방식 — 폐기. dash는 이 채널의 첫 사용처로 재설계.

## Goal

캐릭터 수평 속도를 **두 채널로 분리**한다: **입력 velocity**(플레이어 제어, 직접 세팅) + **외력 velocity**(넉백·바람·대시 등, 누적·감쇠). 최종 = 합산. 현재는 입력이 수평 velocity를 *깡으로 덮어써서* 외력(넉백/바람/대시)을 표현할 수 없다. 외력 채널을 도입하고, **dash를 그 첫 사용처**(Active 창 동안 forward 외력)로 구현한다. wind/모래늪/넉백은 같은 채널의 후속 사용처.

**거동 보존:** 외력이 0이면(평소) 합산 결과 = 입력 그대로 → 기존 이동 무변화.

## 배경 / 동기

이전 dash 설계는 `AddForce(Impulse)` 1회였으나, 실측으로 우리 이동이 **set-velocity 모델**(`MovementSystem`이 입력 방향×speed로 수평 velocity를 *세팅*, Y 보존)임이 드러났다. 그래서:
- **단발 `AddForce`** → 다음 틱 이동이 수평 velocity를 입력값으로 *덮어써* 대시 부스트가 사라짐(취약).
- **매 틱 `AddForce`** → 속도 누적(가속), 보통 원치 않음.
- 근본: **수평 외력 채널이 없어** 입력이 독점. (수직 Y는 사실상 외력 채널 — 중력·점프가 거기 삶.)

업계 정석 = **입력/외력 속도 채널 분리 + 합산**(아래 표준 매핑). 사용자 합의로 이 정석(B)을 채택.

## 산업 표준 매핑 (리서치 박제)

| 개념 | 표준 | 우리 |
|---|---|---|
| 속도 합성 | **입력(control) velocity + 외력(additive) velocity 합산** — PhysX Character Controller "base + additive", AAA 키네마틱 컨트롤러 | `MovementSystem`: `velocity = inputH + externalH` (Y 보존) |
| 외력 감쇠 | **지수 감쇠**(동적 마찰 흉내) — 반응성 좋은 startup/turnaround | `ExternalVelocitySystem`: 매 틱 `*= decay` |
| 입력 이동 | 직접 세팅(반응성) 또는 가속 기반 | 현행 직접 세팅 유지(입력 채널) |
| 지속 외력(바람/늪) | 외력 채널에 매 틱 가속 더함 → 입력과 공존 | (후속) 같은 채널 사용처 |
| 대시/넉백(버스트) | 외력 채널에 일시 velocity(감쇠 or held) | dash=held(active 동안 set) / knockback=add+decay |

> 출처: PhysX CCT(base+additive velocity), Unity rigidbody velocity-vs-AddForce, physics character controller(input vs external + 지수감쇠). 핵심: **"입력이 외력을 덮지 않게 채널 분리 후 합산."**

## 설계

### 두 채널

```
최종 수평 velocity = inputH(입력 방향×speed)  +  externalH(외력 채널)
수직(Y) = 기존대로 보존(중력·점프 = 이미 외력 채널)

externalH: 넉백·바람·대시가 얹는 별도 velocity. 매 틱 지수 감쇠.
입력은 inputH만 세팅 → externalH 안 건드림(덮어쓰기 위험 해소).
```

### 구조 (LOP-Shared)

- **`ExternalVelocity : GameFramework.World.Component`** (LOP-Shared, `UnityEngine.Vector3 Value` — 수평만, Y 무시). 외력 채널 상태. (LOP-Shared는 UnityEngine 참조 가능 — `MovementSystem`과 동형. 커널과 타입 일치로 변환 회피.)
- **`MovementSystem.ProcessMovement` 확장**: `MovementInput`에 `externalVelocity` 추가. 출력:
  ```
  inputVel = (입력 있으면 dir×speed, else 0)
  horizontal = inputVel.xz + externalVelocity.xz
  velocity = (horizontal.x, currentVelocity.y, horizontal.z)
  rotation = 입력 있을 때만 (외력 방향으론 안 돈다)
  hasMove = 입력 있음 || externalVelocity ≠ 0   ← 외력만 있어도 적용(넉백 이동)
  ```
- **`ExternalVelocitySystem.Tick(entity, deltaTime)`**: `ExternalVelocity.Value *= decayPerTick`(지수 감쇠; ε 이하면 0). `LOPWorld.Mutation` sweep에서 구동(StatusEffect/Ability tick 옆).

### 틱 순서 (핵심 — dash=held, knockback=decay를 한 채널로)

```
1. ProcessInput → 이동: ExternalVelocity 읽어 inputH+externalH 합산 → entity.velocity 세팅
2. world.Tick(Mutation): 어빌리티 페이즈 전진 + ExternalVelocity 감쇠(*decay)
3. (side) AbilityMotionSystem: Active 어빌리티가 motion_speed>0면 ExternalVelocity = forward×motion_speed로 *세팅*(덮어씀)
4. SimulatePhysics
```

- **dash**(3에서 매 active 틱 set) → 2의 감쇠를 덮어써 **active 동안 full 유지**. active 끝나면 3이 멈춤 → 2 감쇠가 0으로(자연 종료).
- **knockback/wind**(외부에서 한 번 add, 3에서 안 덮음) → 2 감쇠로 자연 소멸.
- 1이 3보다 앞(읽기 먼저) → dash는 *다음 틱*부터 이동에 반영(1틱 지연, 무해).
- 클·서 동일(예측+권위). 페이즈 타이밍·감쇠·합산 모두 결정론 공유.

### dash (외력 채널 첫 사용처)

- **side `AbilityMotionSystem`**(클·서): 매 틱 `world.Tick` 후, `ActiveAbility.Phase==Active` & 그 어빌리티 `motion_speed>0`인 엔티티 → `ExternalVelocity.Value = forward × motion_speed`(전방, Y 0). (`md.Tables.TbAbility.Get(id).MotionSpeed` 직접 읽음 — 물리/모션은 side, 공유 `AbilityData`에 안 넣음.)
- **`TbAbility` 컬럼**: `motion_speed`(float, 0=없음). + frame 컬럼 외부화(`cast_time_ticks`→`startup_ticks`, `active_ticks`, `recovery_ticks` — provider가 하드코딩 졸업). dash 행: id2/code"dash"/`startup0/active~5/recovery0`/motion_speed(튜닝)/cooldown.
- **거리 = motion_speed × active_ticks × dt**(질량·드래그 무관, 결정론 제어). 단발 임펄스의 취약성 해소.
- **트리거**: GamePad DASH 버튼 → `SetAbilityId(2)`. `CharacterCreator`가 dash grant + `ExternalVelocity` 컴포넌트 부여.

### 거동 보존 / 검증

- **외력 0이면 합산 = 입력** → 평소 이동/점프/전투 무변화. (`ExternalVelocity` 기본 0.)
- **EditMode(LOP-Shared)**: `MovementSystem` 합산(input+external, Y 보존, hasMove 조건), `ExternalVelocitySystem` 감쇠.
- **플레이**: DASH → 제어된 전방 대시(거리 일정). 평소 이동/점프 동일. wind/넉백 미도입이라 그 외 변화 0. NRE 0.

## Phasing (이 슬라이스 = 채널 + dash)
이 슬라이스 하나에 외력 채널 + dash(첫 사용처)를 함께 — 채널은 사용처 없이는 검증 어렵고, dash가 자연스러운 첫 사용처.
- **이번**: `ExternalVelocity` + `MovementSystem` 합산 + `ExternalVelocitySystem` 감쇠 + `AbilityMotionSystem`(dash) + TbAbility `motion_speed`/frame 컬럼 + 버튼/grant.
- **후속 사용처**(별도): wind/태풍(지속 외력 add), 모래늪(외력 끌림 or speed 배율), 넉백(전투 hit → 외력 add). 전부 같은 채널.

## Out of Scope
- **wind·모래늪·넉백** — 외력 채널의 *후속 사용처*. 이 슬라이스는 채널 + dash만.
- **가속 기반 입력 이동**(MoveTowards 블렌딩) — 입력 채널은 현행 직접 세팅 유지.
- **외력 채널의 prediction/reconciliation 정밀화** — Stage④(현 velocity 권위=host, 외력도 그 경로).
- **Attack 어빌리티**(다음 슬라이스) — frame 컬럼은 이 슬라이스가 깔아둠.
- **3f 레거시 은퇴**.

## Open Questions (plan에서 해소)
- **`decayPerTick` 값** — 0.8~0.95 범위 튜닝(대시 held엔 무관, knockback 소멸 속도). 상수 시작, 후에 per-source 가능.
- **`ExternalVelocity` 위치** — LOP-Shared 컴포넌트(UnityEngine.Vector3, 커널 정합) vs GameFramework.World(Numerics, Velocity와 동형). → 권고: LOP-Shared(변환 회피, dash/모션은 LOP 도메인).
- **dash 거리 정밀** — 틱 순서상 active 동안 full 유지(감쇠 덮음). motion_speed×active_ticks로 튜닝. recovery 줄지 결정.
- **입력 ↔ 외력 상호작용** — 현 설계는 *독립 합산*(넉백 중 입력 추가 가능). 입력이 외력을 줄이는 모델은 후속(필요 시).

## 진행
- [x] brainstorm — set-velocity 모델 실측 → AddForce 부적합 확인 → 2채널(B) 정석 채택(리서치 기반)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] plan 작성 → 구현
