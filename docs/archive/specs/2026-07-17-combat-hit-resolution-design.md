# 전투 히트 해소 — 닷지 on-hit 게이트 + 크리/회피 상수 MasterData 승격

**Date:** 2026-07-17
**Branch (제안):** `feature/combat-hit-resolution`
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [엔티티 시스템 설계](../../entity-system-design.md) · [MasterData 키 규약](../../lop-repo-topology.md) · [ROADMAP](../../ROADMAP.md)

## Goal

두 가지를 한 슬라이스로 처리한다:

1. **버그 픽스 — 닷지가 넉백(및 미래 on-hit 효과)을 게이트하지 못함.** 지금은 닷지해서 데미지를 피해도 넉백은 그대로 당한다. 표준 구조로 고쳐 **닷지=공격 빗나감 → on-hit 효과 전부 무효**로 만든다.
2. **리팩터 — 크리/회피 상수 MasterData 승격.** `LOPCombatSystem`에 하드코딩된 회피/크리 확률·배수 상수를 전역 설정 테이블 `TbCombatConfig`로 뺀다(밸런스를 데이터로 조정).

## 배경 — 근본 원인 (systematic-debugging Phase 1)

공격 어빌리티는 **독립적인 효과 2개**를 갖고, 각자 **따로 히트 판정**을 한다:

```
공격 어빌리티 (Active 진입)
├─ DamageEffect   → DamageEffectHandler   → OverlapSphere+AttackSector → LOPCombatSystem.Attack
│                                              └─ 여기서만 isDodged 판정 (닷지면 데미지 0)
└─ KnockbackEffect → KnockbackEffectHandler → OverlapSphere+AttackSector → 넉백 적용
                                               └─ ❌ 닷지를 전혀 안 봄 → 부채꼴 안 전원 넉백
```

- 닷지 판정은 `LOPCombatSystem.Attack` **안에서만** 난다(`LOPCombatSystem.cs`, `isDodged ? 0 : damage`).
- `KnockbackEffectHandler`는 그 결과를 모른 채 부채꼴 안 모든 대상을 민다 → **데미지는 피했는데 넉백은 당함**.
- 단순히 넉백 핸들러에 닷지 체크를 추가할 수 없다: 닷지 seed가 `hash(matchSeed, tick, attacker, target, effectIndex)`라 **효과마다 effectIndex가 달라 닷지 답이 다르다**.

하드코딩 상수(승격 대상):

| 상수 | 값 | 위치 |
|---|---|---|
| dodge chance clamp min/max | 0.05 / 0.95 | `LOPCombatSystem.IsDodge` |
| crit chance clamp min/max | 0.05 / 0.50 | `LOPCombatSystem.IsCritical` |
| crit multiplier min/max | 1.25 / 1.75 | `LOPCombatSystem.Attack` |

## 설계 결정 (브레인스토밍 합의 + 업계 표준 확인)

| 축 | 결정 | 근거 |
|---|---|---|
| 히트 게이팅 위치 | **디스패치 게이트(구조 ②)** — 효과 *위*에서 히트를 1회 해소하고 명중 대상만 on-hit 효과에 넘김. 핸들러는 닷지를 모른다 | WoW attack table(공격당 단일 롤, 회피가 전체 게이트) · GAS(hit result → 효과 소비). "각 효과가 닷지 재확인"은 비표준 |
| 히트 정의자 | **데미지 효과가 히트를 정의(②b)** — 데미지의 타겟팅+dodge가 "명중 대상"을 확정, 넉백은 그 위에 올라타는 on-hit 라이더 | 유저 모델("넉백은 데미지 히트에 의존") + LoL on-hit(평타 히트에 라이더가 올라탐) |
| 닷지 seed | **per-attack** — `hash(matchSeed, tick, attacker, target)` (effectIndex 제거) | 한 공격의 닷지 결과는 대상당 하나 |
| 크리 | **데미지-로컬 유지** (회피만 히트 게이트) | 크리는 회피가 아니라 데미지 배수 → 넉백 무관. WoW도 회피만 전체 게이트 |
| 상수 위치 | **전역 `TbCombatConfig`**(단일 행, 클·서 공통 projection) | 전투 공식 경계값은 전역 튜닝. TrinityCore 전투 config 테이블 정합 |
| 트리거 범주 | on-hit(Damage/Knockback)만 이번에 세움. self/on-activation(Motion=대시)은 현행 직접 적용 유지 | GAS Apply-to-Self vs Apply-to-Target / WoW `EffectImplicitTarget`. on-try 라이더는 YAGNI |

## Part 1 — 히트 해소 + on-hit 게이트 (구조 ②b)

### 흐름

```
어빌리티 Active 진입 (executor가 발동당 AttackHitContext 1개 생성)
├─ [히트 정의] DamageEffect: 자기 Range/Angle로 대상 찾기 + IsDodged 1회/대상
│     → dodge 아닌 대상 = "명중 대상". 데미지 적용 + DamageDealtEvent
│     → 명중 대상 id들을 AttackHitContext에 기록
├─ [on-hit 라이더] KnockbackEffect: AttackHitContext의 "명중 대상"에만 넉백
│     → 자기 overlap/부채꼴/dodge 재확인 없음. Range/Angle 미사용(히트 형상=데미지)
│     → Strength/DurationTicks/DecayPerTick(넉백 세기 파라미터)만 유지
└─ [self/on-activation] MotionEffect(대시): 히트와 무관, 발동 즉시 시전자 적용 (현행 유지)
```

### 구성 요소

- **`AttackHitContext`** (LOP-Shared, 발동당 1개) — 이번 공격 발동에서 "명중한(닷지 안 된) 대상 id 집합"을 담는 가변 컨테이너. executor가 `DriveActiveEntity`(OnActiveEnter)당 하나 만들어 `AbilityEffectContext`에 실어 핸들러에 전달.
- **`LOPCombatSystem`**:
  - `Attack(...)`을 히트-정의 형태로: 각 대상마다 `IsDodged`로 명중 여부 판정, 명중 대상에 데미지(+크리) 적용, 명중 대상 id를 `AttackHitContext`에 등록.
  - `IsDodged(attacker, target, tick, matchSeed)` — 단일 진실원본, per-attack seed(effectIndex 없음). 결정론.
  - 크리는 명중 대상 데미지 적용 시 데미지-로컬 롤(현행 위치 유지, config 값 사용).
- **`DamageEffectHandler`** — 그대로 대상 탐색(overlap+sector) + `combatSystem.Attack` 호출하되, `Attack`이 명중 대상을 `AttackHitContext`에 기록하도록 ctx 전달.
- **`KnockbackEffectHandler`** — overlap/sector/자기 dodge 제거. `AttackHitContext`의 명중 대상만 순회해 `MotionContributions`에 넉백 기여 등록. `KnockbackEffect`에서 `Range`/`Angle` 필드 제거(히트 형상은 데미지가 정의).
- **`AbilityEffectExecutor`** — `OnActiveEnter` 시 `AttackHitContext` 1개 생성, 효과별 `AbilityEffectContext`에 실어 전달.

### 순서 제약 (박제)

넉백(라이더)이 데미지(히트 정의자)의 결과를 읽으므로, **어빌리티 effect 리스트에서 DamageEffect가 KnockbackEffect보다 앞**이어야 한다. 데이터 저작 규약으로 문서화. (미래에 "데미지 없는 히트 정의자"나 executor 2-pass가 필요해지면 히트 타겟팅을 어빌리티 레벨로 올리는 ②a로 일반화 — 지금은 YAGNI.)

### 결정론/예측

데미지·넉백 핸들러 모두 **서버 전용 등록**(클라 미등록, 결과는 스냅샷 수신). 히트 해소 전체가 서버권위 → 클라 예측 복잡도 0, 현행 유지.

## Part 2 — 크리/회피 상수 → MasterData

### `TbCombatConfig` (전역 단일 행)

클·서 공통 projection(전투가 공유 코드라 양쪽 필요). 필드:

| 필드 | 대체하는 하드코딩 |
|---|---|
| `dodge_chance_min` / `dodge_chance_max` | 0.05 / 0.95 |
| `crit_chance_min` / `crit_chance_max` | 0.05 / 0.50 |
| `crit_mult_min` / `crit_mult_max` | 1.25 / 1.75 |

### 배선 (`AbilityData`와 동일 패턴 — Shared는 MasterData 직접 참조 불가)

- **`CombatConfig`** struct (LOP-Shared) — 위 6값 보유.
- side-local **`CombatConfigProvider`** (Client + Server) — `TbCombatConfig` 행 → `CombatConfig` 매핑(`AbilityDataProvider`와 대칭).
- **`LOPCombatSystem`** 생성자가 `CombatConfig` 받음 — 각 사이드 Installer가 프로바이더로 빌드해 등록. `IsDodge`/`IsCritical`/크리 배수가 하드코딩 대신 config 값 사용.

### 범위 밖 (하드코딩 유지)

데미지 공식(`damage *= 3` 밸런스, `+attackerStrength`)은 "크리/회피 상수"가 아니라 데미지 공식 — 이번 스코프 밖. 필요 시 후속.

## 산업 표준 매핑

- **WoW attack table** — 공격당 서버 단일 롤로 miss/dodge/parry/crit/hit 결정, 회피가 공격 전체를 게이트. 우리 "히트 1회 해소 → 명중 대상만 효과 적용"의 원형. (`EffectImplicitTarget`로 효과별 self/enemy 타겟 선언 = 우리 on-activation vs on-hit 범주.)
- **Unreal GAS** — 어빌리티가 hit result/target data 판정 후 효과 적용, Apply-to-Self(발동) vs Apply-to-Target(히트). 우리 구조 ②와 트리거 범주의 근거.
- **LoL on-hit 효과** — 평타(데미지 히트)에 둔화/흡혈 등이 올라탐. 우리 ②b(데미지=히트 정의자, 넉백=라이더)의 직접 대응.
- **TrinityCore** — 전투 상수를 전역 config 테이블로. `TbCombatConfig`의 근거.

## 영향 범위 (구현 plan에서 단계화)

- **LOP-Shared**: `LOPCombatSystem`(히트-정의 Attack + `IsDodged` per-attack + config), `CombatConfig` struct, `AttackHitContext`, `AbilityEffect.KnockbackEffect`(Range/Angle 제거), `AbilityEffectContext`(+HitContext), `AbilityEffectExecutor`, `KnockbackEffectHandler`(라이더화), `DamageEffectHandler`(명중 대상 기록). **EditMode 테스트**(닷지→넉백 스킵, config 값 적용, 명중 대상 공유).
- **infrastructure**: `#CombatConfig.xlsx`(또는 config 테이블) + `__tables__` 등록 + Luban gen. `#Ability.xlsx`의 넉백 effect 셀에서 range/angle 제거(데미지가 히트 정의).
- **MasterData-Client/Server**: 재생성(`TbCombatConfig`, 갱신된 `TbAbility`).
- **LOP-Client/Server**: `CombatConfigProvider`(side-local) + DI 배선(CombatConfig 빌드·등록, `LOPCombatSystem`에 주입).

## 테스트

- **Shared EditMode**(A2.2b로 이미 히트 판정 테스트 가능):
  - 닷지 강제(seed) → 데미지 0 **그리고** `MotionContributions`에 넉백 미등록(명중 대상 아님).
  - 비-닷지 → 데미지 적용 + 넉백 등록.
  - 여러 대상 중 일부만 닷지 → 명중 대상만 넉백.
  - `CombatConfig` 값이 clamp/배수에 반영되는지(경계값 테스트).
- **플레이 검증**(서버 에디터 + 사람): 닷지 뜬 대상은 넉백 안 당함, 명중 대상은 데미지+넉백 정상. `#CombatConfig.xlsx` 값 조정이 인게임 확률에 반영.

## Out of Scope

- 데미지 공식 상수(×3, +Strength) 승격.
- on-activation/on-try 라이더 트리거 범주 정식화(YAGNI — Motion은 현행 직접 적용).
- 히트 타겟팅을 어빌리티 레벨로(②a) — "데미지 없는 히트 정의자" 콘텐츠 착수 시.
- 크리를 히트 해소로 통합(회피만 게이트 대상).

## 진행

- [x] systematic-debugging Phase 1 (근본 원인 규명)
- [x] 브레인스토밍 합의 (구조 ②b, per-attack dodge, TbCombatConfig) + 업계 표준 확인(WoW/GAS/LoL/TrinityCore)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
