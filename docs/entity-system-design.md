# Entity System Design

월드 엔티티 데이터 모델 시스템. CBD(Component-Based Design) 기반으로, Entity는 빈 껍데기이고
능력(`Component`)을 조합하여 캐릭터·몬스터·오브젝트 등을 구성한다.

> **코드 위치 (중요).** 이 문서가 설명하는 World Core는 **이 클라 repo에 없다.** 실제 코드는:
> - **`GameFramework` 패키지 (`namespace GameFramework.World`)** — 앱 비종속 코어: `Entity`,
>   `Component`(추상 클래스), `EntityRegistry`, `EntityStatType`, `StatModifier`/`ModifierType`,
>   공통 컴포넌트(`Health`/`Mana`/`Level`/`Stats`/`Ownership`/`Transform`/`Velocity`)와
>   그 System(`HealthSystem`/`ManaSystem`/`LevelSystem`/`StatsSystem`), 이벤트(`WorldEventBuffer`,
>   `DamageDealtEvent`/`DeathEvent`/`AbilityActivatedEvent`), `IWorld`/`WorldBase`.
> - **`LeagueOfPhysical-Shared` 패키지 (`namespace LOP`)** — LOP 도메인 컴포넌트/시스템:
>   `StatusEffects`/`Abilities`/`InputBuffer`/`MotionContributions`/`PredictedAbilityState`,
>   `LOPCombatSystem`/`MovementSystem`/`KinematicMoveSystem`/`AbilitySystem`/`StatusEffectSystem` 등.
>
> 코드가 어느 repo에 사는지는 `docs/lop-repo-topology.md`, 코어↔프레젠테이션 연결은
> `docs/world-core-connection-architecture.md` 참고. 이 문서는 **데이터 모델(CBD 컴포지션 + 스탯)**
> 자체에 집중한다.

## Overview

- 상호작용이 있는 모든 월드 객체를 엔티티로 취급 (렌더링만 하는 배경 객체 제외)
- 엔티티 공통: `Id` (문자열 — 런타임 인스턴스 식별자)
- CBD 컴포지션: Entity는 빈 컨테이너, 능력은 `Component` 서브클래스로 조합
- 컴포넌트는 자신이 속한 Entity(`Owner`)를 알고 있음 (`Entity.Add` 시 자동 설정)
- **Anemic Domain Model**: 컴포넌트는 데이터와 파생 속성(읽기 전용 계산값)만 소유. 생성자에서
  구조적 무결성(필수 초기값)만 처리하고, 상태 변경 로직은 두지 않는다. 모든 처리 로직은 System에 둔다
- 스탯: Base + Modifiers(출처 추적, 해제 가능) = Current

## Architecture: CBD 컴포지션 (Dictionary 기반)

World Core는 순수 C#(`noEngineReferences`)이며 `GameFramework.World`에 있다.

### `Component` (추상 클래스)

모든 엔티티 컴포넌트의 기반. **인터페이스가 아니라 `abstract class Component`** 다. 자신이 속한
`Entity Owner`를 노출하며(`Owner { get; internal set; }`), Owner는 `Entity.Add<T>` 시 자동 설정된다.

> (역사 노트) 초기 설계는 `IEntityComponent` 인터페이스였으나 실제 구현은 추상 클래스로 정착했다.
> `GameFramework`에는 옛 MonoBehaviour 시스템의 별개 `interface IComponent`가 남아 있으나 World
> Core와 무관하다.

### `Entity`

엔티티의 빈 컨테이너.

- **Id**: `string` 식별자 (불변). 생성자 `Entity(string id)`
- **컴포넌트 저장소**: `Dictionary<Type, Component>` — 타입당 하나
- **`Add<T>(T)`**: 컴포넌트 추가 + Owner를 자기 자신으로 설정 (같은 타입은 교체)
- **`Get<T>()`**: 타입으로 조회. 없으면 null
- **`Has<T>()`**: 존재 여부
- **`Remove<T>()`**: 제거 + 제거된 컴포넌트의 Owner를 null로 해제. 제거 여부 반환

### `EntityRegistry`

월드의 엔티티 보관소 (코어 순수 C# 컨테이너). `Entity.Id`(string)를 키로 관리한다.

- **`Add(Entity)`**: 등록. null 엔티티 / null Id / 중복 Id에는 예외 (레지스트리는 진실원본 — 중복은 버그)
- **`Get(id)` / `TryGet(id, out)`**: 조회. 없으면 null / false
- **`Remove(id)`**: 제거 및 제거 여부 반환
- **`Contains(id)`**: 존재 여부
- **`Count` / `All`**: 총 개수 / 전체 열거

엔티티 생성·등록은 게임 측(LOP)의 크리에이터가 `new Entity(id)` + `Add<T>` + `EntityRegistry.Add`로
수행한다. 외부 시스템(뷰·UI·netcode)은 이 레지스트리를 통해 entityId로 엔티티에 접근한다. (전용
`EntityFactory`는 World Core에 없다 — 직접 조립.)

## 스탯 모델

### `EntityStatType` (enum, `GameFramework.World`)

스탯 종류. 확장 가능하며 현재 값은 **RPG primary 스탯 6종**:

- `Strength`
- `Dexterity`
- `Intelligence`
- `Vitality`
- `MoveSpeed`
- `JumpPower`

> `LOPCombatSystem`은 `Strength`(공격)·`Dexterity`(회피/치명 관련)를, `MovementSystem`은
> `MoveSpeed`/`JumpPower`를 읽는다.

### `StatModifier` (struct) + `ModifierType` (enum)

스탯 보정값 하나를 표현하는 불변 구조체(`readonly struct StatModifier`):

- **`StatType`** (`int`): 어떤 스탯에 적용되는지 (`EntityStatType` 캐스트)
- **`Value`** (`float`): 보정 수치
- **`Type`** (`ModifierType`): 보정 방식
- **`SourceId`** (`string`): 출처 식별자 — 같은 출처의 모디파이어를 일괄 해제할 때 사용

`ModifierType`은 **합연산이 아니라 적용 방식**을 나눈다:

- `Flat` — 절대값 가산
- `PercentAdd` — 퍼센트 가산(서로 더해짐)
- `PercentMult` — 퍼센트 곱연산(각각 곱해짐)

최종값 계산: `(Base + ΣFlat) × (1 + ΣPercentAdd) × Π(1 + PercentMult)`.

> (역사 노트) 초기 설계의 `EntityStatModifier` + `ModifierSource`(Equipment/Buff/Debuff/Passive)는
> 구현되지 않았다. 출처 종류 대신 `SourceId` 문자열로 일괄 해제한다.

## 컴포넌트 인벤토리 (실제 존재)

모두 Anemic Domain Model — 데이터 + 파생 속성만. 다른 컴포넌트를 직접 참조하지 않는다.

### World Core (`GameFramework.World`, `World/Components/`)

| 컴포넌트 | 데이터 | 파생/비고 |
|---|---|---|
| `Health` | `int Max`, `int Current` | `IsAlive`/`IsDead`. ctor `Health(int max)` |
| `Mana` | `int Max`, `int Current` | ctor `Mana(int max)` |
| `Level` | `int Value`, `long Exp`, `long ExpToNext` | |
| `Stats` | `Dictionary<int,float> BaseStats`, `List<StatModifier> Modifiers`, `int UnspentPoints` | |
| `Ownership` | `string OwnerId` (불변) | 존재 자체가 "플레이어/비-NPC" 마커 |
| `Transform` | `System.Numerics.Vector3 Position`, `Quaternion Rotation` | 엔진 비의존이라 `System.Numerics` |
| `Velocity` | `System.Numerics.Vector3 Linear` | 진실원본 (키네마틱 이동이 씀) |

### LOP 도메인 (`namespace LOP`, LeagueOfPhysical-Shared `Runtime/Scripts/Game/`)

- `StatusEffects` — `List<ActiveEffect> Effects` (상태이상 목록)
- `Abilities` — 어빌리티 슬롯/활성 상태(`AbilitySlot`/`ActiveAbility`/`AbilityPhase`)
- `InputBuffer` — 서버 입력 버퍼(구 `EntityInputComponent`, 입력-as-데이터로 rename)
- `MotionContributions` — 외력(넉백 등) 기여 채널 (Additive)
- `PredictedAbilityState` — 클라 예측/롤백용 어빌리티 상태 스냅샷

> **존재하지 않음**: `Combat`/`Dialogue`/`Interactable` 컴포넌트, `EntityFactory`. 초기 설계에만
> 있던 것들로 구현되지 않았다. (`Combat`/`Ability`/`StatusEffect`라는 이름은 System 또는 MasterData
> Luban 클래스로만 존재.)

## Systems (처리 로직)

컴포넌트 데이터를 읽고 쓰는 모든 처리 로직은 System에 둔다. World Core System은 무상태 로직 클래스
(상태는 컴포넌트에). Generation(룰/결정)과 Application(권위 상태 쓰기)을 별도 시그니처로 노출한다 —
상세는 `docs/world-core-connection-architecture.md`의 Generation vs Application 절 참고.

### World Core (`GameFramework.World`, `World/Systems/`)

- **`HealthSystem`**: `TakeDamage(Health,int)`, `Heal(Health,int)`, `SetMax(Health,int)`,
  `ApplyAuthoritativeState(Health, int max, int current)` (스냅샷 적용)
- **`ManaSystem`**: `bool Spend(Mana,int)`, `ApplyAuthoritativeState(Mana,int,int)`
- **`LevelSystem`**: `int AddExperience(Level,long)` (레벨업 처리 후 획득 statPoints 반환),
  `ApplyAuthoritativeState(Level, int value, long exp)`
- **`StatsSystem`**: 조회·모디파이어·base 관리 (아래)

### `StatsSystem` API

- `float GetValue(Stats, int statType)` — 최종값 (`(Base+ΣFlat)×(1+ΣPercentAdd)×Π(1+PercentMult)`)
- `void AddModifier(Stats, StatModifier)`
- `bool RemoveModifiersBySourceId(Stats, string sourceId)`
- `void SetBase(Stats, int statType, float value)` / `float AddBase(Stats, int, float)` (새 base 반환)
- `void AddUnspent(Stats, int)` / `void SetUnspent(Stats, int)`
- `int Allocate(Stats, int statType)` — 미사용 포인트 1 소비 + base +1, 최종값 반환

> Health/Mana/Level과 달리 `StatsSystem`에는 `ApplyAuthoritativeState`가 **없다** — 스탯은
> per-tick 스냅샷을 보내지 않고 스폰/재접 시드 + `StatAllocation` 채널로만 싱크한다(하이브리드).

### LOP 도메인 시스템 (`namespace LOP`)

- **`LOPCombatSystem`** — `Attack(Entity attacker, Entity target, int baseDamage, long tick, int effectIndex, ulong matchSeed)`,
  `IsDodge`/`IsCritical`(결정론 RNG). Strength/Dexterity를 `statsSystem.GetValue`로 읽음
- **`MovementSystem`** — `Tick(Entity, long, float)` + `static MovementResult ProcessMovement(in MovementInput)`
  (공유 순수 커널). velocity 단일 writer. MoveSpeed/JumpPower 읽음
- **`KinematicMoveSystem`** — `Tick(Entity, float)`: 중력(분리된 수직 스텝) + collide-and-slide.
  Transform/Velocity에 직접 씀 (`ICollisionQuery` 포트로 sweep)
- **`AbilitySystem`** — `TryActivate`/`CanActivate`/`Grant`/`Tick` + `static HasActiveMotionEffect`
- **`StatusEffectSystem`** — `Apply`/`Tick`/`Remove`
- 보조: `MotionContributionSystem`, `InputBufferSystem`, `AbilityEffectExecutor`,
  effect handler(`DamageEffectHandler`/`KnockbackEffectHandler`/`StatusEffectApplyEffectHandler`)

## 엔티티 조합 예시

| 엔티티 | 컴포넌트 조합 (현재) |
|---|---|
| 플레이어 | `Health` + `Mana` + `Level` + `Stats` + `Ownership` + `Transform` + `Velocity` + `Abilities` + `StatusEffects` + `InputBuffer` |
| 적(AI) | `Health` + `Stats` + `Transform` + `Velocity` (+ 어빌리티/상태이상 필요 시) |

> 조합은 크리에이터/`EntityCreationData`가 결정한다. NPC/상호작용 오브젝트(Dialogue/Interactable)는
> 아직 콘텐츠가 없어 미구현.

## Assembly / 레이어

- **World Core**: `GameFramework.Runtime` asmdef. `noEngineReferences: true` 유지 — `Entity`,
  `Component`, 스탯, 공통 컴포넌트/시스템 모두 순수 C#. `Transform`/`Velocity`가 `System.Numerics`를
  쓰는 이유.
- **LOP 도메인**: `baegames.LOP.Shared.Runtime` asmdef (`GameFramework.Runtime` 참조).
- **테스트**: World Core는 GameFramework의 EditMode, LOP 도메인은 LeagueOfPhysical-Shared의 EditMode.

## Open Decisions

- [ ] NPC/상호작용 오브젝트 컴포넌트(Dialogue/Interactable류) — 해당 콘텐츠 착수 시
- [ ] 엔티티 설정 데이터 SO/MasterData 연동 확장
- [ ] `Ownership` 외 팀/진영 마커
