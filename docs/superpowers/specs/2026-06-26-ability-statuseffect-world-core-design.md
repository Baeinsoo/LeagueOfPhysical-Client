# Ability & StatusEffect — 표준 구조로 World Core 이전 (4c)

**Date:** 2026-06-26
**Branch (제안):** `feature/ability-statuseffect-world-core` (GameFramework / LOP-Shared / Client / Server / MasterData / infrastructure)
**Related:** [엔티티 시스템 설계](../../entity-system-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (Generation/Application·snapshot vs event) · [아키텍처 가이드라인](../../architecture-guidelines.md) ("업계 표준 명명"·anemic) · [LOP 저장소 토폴로지](../../lop-repo-topology.md) (Shared ↔ MasterData 비참조) · 프로젝트 메모리 `world-core-runner-world-naming`(4c)·`world-core-view-migration-status`(기존 Health/Mana/Level/Stats 이전 패턴) · 선행: 이동 커널 공유(4c #1, 완료)

## Goal

엔티티의 두 동적 동작 단위를 **업계 표준 구조·생명주기로 새로 설계해 World Core(순수 C#)로 이전**한다. 레거시 MonoBehaviour `Action`/`Status`를 은퇴시킨다.

- `Action`(MonoBehaviour) → **`Ability`**: 트리거형 타임드 스킬. 데이터 컴포넌트 `Abilities` + 로직 `AbilitySystem`.
- `Status`(MonoBehaviour, 빈 자리) → **`StatusEffect`**: 지속형 상태이상(버프/디버프/DoT). 데이터 컴포넌트 `StatusEffects` + 로직 `StatusEffectSystem`.

**기존 함수명·구조는 유지하지 않는다** — GAS 생명주기 어휘 + Quantum식 anemic(데이터 컴포넌트 + 무상태 시스템)으로 새로 짓는다. 레거시가 골격뿐(Dash만 동작, Attack/Spawn 빈 본문, StatusEffect 구현체 0)이라 *무거운 이전이 아니라 거의 greenfield*다.

## 배경 / 동기

### 왜 지금 (이름·구조를 박기 전 마지막 기회)

`Action`/`Status`는 아직 초기·골격이다. World 컴포넌트로 박힌 뒤 고치면 비용이 크다. 지금이 표준 구조로 새로 지을 적기. 동시에 4c의 목표(`world.Tick`을 *실제로 채우는* 첫 도메인 로직)와 일치한다.

### 핵심 적응 — GAS 어휘 + Quantum 구조

리서치(GAS / Photon Quantum / Unity DOTS / Dota2·LoL, 1차 출처)의 결론:

- **GAS는 fat object**(ability가 곧 behavior, cost/cooldown이 곧 GameplayEffect) → anemic 위반이라 *그대로 복사 불가*. **어휘와 생명주기 페이즈, Ability→Effect 이음새만** 차용한다.
- **구조 모델 = Photon Quantum**: `component { FP Cooldown; }` 데이터 + 무상태 시스템이 매 틱 갱신 = 정확히 anemic + 결정론. 우리 World Core가 따를 모양.
- **cost/cooldown은 필드로**(효과로 모델링하는 GAS식 우회 회피). 효과 정리는 이미 만든 `EntityStatModifier.SourceId` 일괄제거로.

> 상세 리서치(생명주기 페이즈, 스택 모델, 데이터-컴포넌트 vs 시스템, 출처)는 이 작업 브레인스토밍에서 수행. 핵심만 아래 박제.

## 현재 상태 (실측)

- **레거시 (클·서 각 독립 사본):** `Component/Action.cs`(`abstract Action : LOPComponent`), `Component/Action/{Attack,Dash,Spawn}.cs`, `Component/Status.cs`(`abstract Status`, 구현체 0), `Game/LOPActionManager.cs`(`: IActionManager<LOPEntity>`), `Entity/Event.Entity.cs`(내부 이벤트 `ActionStart`/`ActionEnd`). 공유 인터페이스 = GameFramework `IActionManager`.
- **동작하는 경로(보존 대상):** 입력/AI(`EnemyBrain.TryStartAction`) → `LOPActionManager` → 서버가 `ActionData`(actionCode/isActive/remainCooldown/startTick) 스냅 + `ActionStartToC`/`ActionEndToC` 와이어 → 클라 `Game.Entity.MessageHandler` → `Action` 컴포넌트 → `LOPEntityView` 연출. (실효 본문은 `Dash`의 임펄스뿐.)
- **설정 데이터:** Luban `TbAction`/`MasterData.Action`(필드 Code/Name/Description/Category/Duration/CastTime/Cooldown/HpCost/MpCost) — `AbilityData`의 ~70% 이미 존재.
- **World Core 자산(재사용 — 실측 시그니처):** `EntityRegistry`(string id 키), `WorldEventBuffer`, `Stats : Component`(`BaseStats Dict<int,float>` + `Modifiers List<StatModifier>` + `UnspentPoints`). 모디파이어 타입 = `StatModifier{int StatType, float Value, ModifierType Type, string SourceId}` — **`Source` 카테고리 enum 없음**(GAS-ideal `EntityStatModifier`와 다름). `ModifierType`은 이미 **Flat/PercentAdd/PercentMult** 지원. `StatsSystem`(인스턴스 클래스): `GetValue(stats,statType)` / `AddModifier(stats,modifier)` / `RemoveModifiersBySourceId(stats, string)`. 제거 축은 **SourceId 하나뿐**. 이미 끝낸 Health/Mana/Level/Stats 이전과 동형 패턴.
- **스탯 종류 (실측):** `EntityStatType` = **Strength/Dexterity/Intelligence/Vitality 4개뿐.** ⚠️ **"Speed"/이동속도 스탯 없음** — 이동속도는 `masterData.Speed`(상수)를 이동 커널이 직접 읽음. 따라서 *이동을 빠르게 하는* 헤이스트는 이동이 런타임 스탯을 읽도록 배선해야 성립(아래 설계 참고).
- **틱 자리:** `LOPRunner.UpdateRunner`의 `world.Tick(tick, interval)` (현재 no-op, `LOPWorld` 빈 골격).

## 설계

### 명명 (업계 표준, 레거시 비계승)

| 개념 | 이름 | 표준 근거 |
|---|---|---|
| 어빌리티 데이터 컴포넌트 | `Abilities` (슬롯 컬렉션) | GAS InstancedPerActor 상태 = 엔티티당 슬롯 |
| 어빌리티 런타임 슬롯 | `AbilitySlot` | |
| 어빌리티 로직 | `AbilitySystem` | DOTS `AbilitySystem` |
| 어빌리티 설정(Luban) | `AbilityData` (테이블 `TbAbility`) | |
| 상태이상 데이터 컴포넌트 | `StatusEffects` (효과 리스트) | DOTS `DynamicBuffer`/Quantum `list<T>` idiom |
| 상태이상 인스턴스 | `ActiveEffect` | |
| 상태이상 로직 | `StatusEffectSystem` | |
| 상태이상 설정(Luban) | `StatusEffectData` (테이블 `TbStatusEffect`, 신규) | |

레거시 `IActionManager`/`LOPActionManager`/`TryStartAction`/`actionCode`/`ActionStart` 등은 **은퇴**(보존 안 함). 생명주기 메서드는 GAS 용어(`CanActivate`/`Activate`/`Commit`/`Cancel`)로.

### Ability — 데이터/시스템 분리

**설정 (LOP-Shared `AbilityData` 구조체 — Luban 매핑 대상):**
`AbilityId`, `CooldownTicks`, `Cost(StatType+amount)`, `CastTimeTicks`(0=즉발), `Range`+`TargetingMode`(enum: Self/Unit/Point/Direction), `ProducesEffectIds[]`. *(NOW. `Blocked/RequiredTags`·`ChannelTicks`=DEFER.)*

**런타임 데이터 컴포넌트 (`GameFramework.World.Component` 파생, LOP-Shared):**
```
Abilities : Component
  Dictionary<int /*AbilityId*/, AbilitySlot> Slots      // 부여된 어빌리티

struct AbilitySlot                                       // 순수 데이터 + 파생
  int AbilityId
  int CooldownEndTick      // 0 = ready. 절대 end-tick(결정론·롤백 친화, 매틱 mutation 불필요)
  AbilityPhase Phase       // Ready (Casting/Channeling = DEFER)
  int PhaseEndTick         // DEFER
```

**로직 (`AbilitySystem`, LOP-Shared 순수 C#) — GAS 페이즈 매핑:**

| 페이즈 | GAS | `AbilitySystem` | NOW/DEFER |
|---|---|---|---|
| 게이트 체크 | `CanActivateAbility`(`CheckCost`+`CheckCooldown`) | `CanActivate(entity, abilityData, target)` → bool (순수 읽기) | NOW |
| 본문 실행 | `ActivateAbility` | `Activate(entity, abilityData, target)` | NOW |
| 코스트·쿨다운 확정 | `CommitAbility` | `Commit(slot, abilityData)`: 코스트 차감 + `CooldownEndTick = tick + CooldownTicks` | NOW |
| 매틱 구동 | (system loop) | `Tick(currentTick)`: 쿨다운 만료/캐스트 진행 — **슬롯 필드만 읽음, 설정 조회 없음** | NOW |
| 윈드업/채널 | `WaitDelay`/cast | `Phase=Casting`, `PhaseEndTick` | DEFER |
| 취소 | `CancelAbility` | `Cancel(slot)` | DEFER |

즉발 어빌리티: `CanActivate`→`Commit`→`ProducesEffectIds`마다 효과 생성(아래 이음새). 쿨다운=`CooldownEndTick` 정수 1개. 코스트=저장 안 함, commit 때 체크+차감만.

### StatusEffect — 데이터/시스템 + Stats 연동

**설정 (LOP-Shared `StatusEffectData` 구조체 — Luban 신규 테이블):**
`EffectId`, `DurationPolicy`(Instant/Duration/Infinite), `DurationTicks`, `Modifiers[]`(StatType, Value, `ModifierType` Flat/PercentAdd/PercentMult — 기존 `StatModifier` 그대로 재사용), `PeriodTicks`(0=없음), `PeriodicAmount`(DoT), `StackPolicy`(Refresh/StackMagnitude), `MaxStacks`. *(NOW. Independent/Extend/Override 스택·태그 = DEFER. 모디파이어 op는 GameFramework `ModifierType` 3종이 이미 있어 추가 작업 없음.)*

**런타임 데이터 컴포넌트 (LOP-Shared):**
```
StatusEffects : Component
  List<ActiveEffect> Effects

struct ActiveEffect                  // 순수 데이터
  int  EffectId
  int  ExpireTick                    // Duration. Infinite면 무시
  int  NextPeriodTick                // 주기. 0=없음
  int  PeriodicAmount                // 비정규화(틱 시 MasterData 조회 회피)
  int    StackCount
  string SourceEntityId              // 귀속(instigator) — EntityRegistry는 string id
  string SourceId                    // 고유 인스턴스 id → StatModifier.SourceId 링크 (string, Guid 아님)
```

**로직 (`StatusEffectSystem`, LOP-Shared) — apply→tick→remove:**

| 페이즈 | GAS/Dota | `StatusEffectSystem` | NOW/DEFER |
|---|---|---|---|
| 적용 | `ApplyGameplayEffectToTarget` | `Apply(target, effectData, sourceEntityId)` + 효과의 각 모디파이어를 `StatsSystem.AddModifier(stats, new StatModifier(StatType, Value, Type, SourceId=효과 인스턴스 id))` | NOW |
| 재적용 | 스택/`OnRefresh` | Refresh(`ExpireTick` 리셋) 또는 `StackCount` 증가(≤`MaxStacks`) | NOW (2모드만) |
| 주기 | Period/`OnIntervalThink` | `Tick`: `tick≥NextPeriodTick` → 서버 DoT 적용(기존 데미지 경로) + `NextPeriodTick += PeriodTicks` | NOW (DoT 있을 때) |
| 만료 | GE 제거 | `Remove`: `tick≥ExpireTick` → `StatsSystem.RemoveModifiersBySourceId(SourceId)` + 리스트 제거 | NOW |

**Stats 연동 = 새 기계장치 0.** 효과 적용 시 `SourceId=효과 인스턴스 id`로 모디파이어를 달고, 만료 시 `RemoveModifiersBySourceId(그 id)`로 일괄 제거 — 이미 만든 SourceId 추적/일괄제거가 정확히 이걸 위한 훅. (`StatModifier`에 Buff/Debuff 같은 Source 카테고리 enum은 없음 — SourceId가 곧 추적키.) `Current = Base + Σmodifiers`는 `StatsSystem.GetValue` 그대로. Instant 정책(즉발 데미지/힐)은 modifier 저장 없이 1회 적용(GAS Instant→BaseValue). 주기 데미지도 영구 적용(제거 대상 아님).

### Ability ↔ Effect 이음새 (서버권위·결정론틱)

**어빌리티는 Stats를 직접 안 건드린다 — 효과를 "요청"만** 하고 StatusEffect 시스템이 적용(GAS ability→GameplayEffect 동형). 서버 틱:

1. 입력/AI → activate intent(abilityId+target) 수집(Collection).
2. `AbilitySystem.Activate`(Generation/Mutation): `CanActivate`→`Commit`→ 즉발이면 `ProducesEffectIds`마다 `StatusEffectSystem.Apply(target, effectData, caster)` 호출(단 한 줄 이음새).
3. `StatusEffectSystem.Apply`: 스택 해소 + `StatsSystem`에 modifier push(또는 instant 데미지=기존 `WorldEvent` 경로).
4. 결과(HP 등)는 **스냅샷(durable state)** 으로, "어빌리티 발동/피격" 연출은 **WorldEvent(cosmetic)** 로 클라에 — connection-arch의 snapshot/event 분리 그대로. 클라는 연출만, 효과 재해소 안 함.

함정·아이템·AI 무엇이 쏘든 **효과 적용 경로는 `StatusEffectSystem.Apply` 하나**(효과는 여러 출처, 적용은 한 길 — GAS 교훈).

### 배치 / asmdef / 토폴로지 제약

- **컴포넌트(`Abilities`/`StatusEffects`) + 시스템(`AbilitySystem`/`StatusEffectSystem`) + 설정 구조체(`AbilityData`/`StatusEffectData`) = LOP-Shared 1벌** (클·서 공유, 결정론 강제 — 이동 커널·`LOPWorld`와 같은 자리 `Runtime/Scripts/Game/`). 컴포넌트는 `GameFramework.World.Component` 파생.
- **⚠️ LOP-Shared는 MasterData 패키지를 참조 못 한다**(토폴로지: Shared ↔ MasterData 비참조). 그래서:
  - 시스템은 Luban `LOPMasterData`를 **직접 조회하지 않는다.**
  - 진입점(`Activate`/`Apply`)은 호출자(사이드-로컬, MasterData 접근 가능)가 **resolve한 `AbilityData`/`StatusEffectData`(LOP-Shared 구조체)를 인자로 받는다** — 이동 커널의 primitives-in 일반화.
  - per-tick(`Tick`)이 필요로 하는 값은 **런타임 슬롯/효과에 비정규화**(CooldownEndTick·ExpireTick·NextPeriodTick·PeriodicAmount) → `Tick`은 슬롯 필드만 읽어 MasterData-free·결정론.
  - 각 사이드가 Luban `MasterData.Action`→`AbilityData` 얇은 매핑을 소유(데이터 출처 어댑터와 로직 분리).
- 구동: `LOPWorld`의 페이즈(또는 `world.Tick`)에서 `AbilitySystem.Tick`/`StatusEffectSystem.Tick` 호출 — `world.Tick`을 *처음으로 실제로 채움*. (현 `entityManager.UpdateEntities` 레거시 틱은 Phase 3 컷오버에서 대체.)

### anemic 준수

컴포넌트는 데이터 + 파생 읽기전용(`IsReady => CooldownEndTick==0` 등)만. 모든 상태변경·판정은 시스템. 레거시의 `UpdateAction`/`TryStart`/`UpdateStatus`(컴포넌트 내 로직)는 소멸.

## Phasing (각 단계 컴파일·동작 보존)

> 정확한 repo별 task·순서는 plan에서. 둘 다 범위지만 의존순(StatusEffect=greenfield 먼저, Ability가 그걸 참조)으로 단계화.

- **Phase 1 — StatusEffect (greenfield, 순수 추가):** LOP-Shared `StatusEffectData` 구조체 + `StatusEffects`/`ActiveEffect` 컴포넌트 + `StatusEffectSystem`(Apply/Tick/Remove) + Stats 연동(`StatModifier` + `RemoveModifiersBySourceId`). **검증용 예시 효과 = 헤이스트 버프**(N틱 동안 이동속도에 +X% [PercentAdd] 모디파이어, 만료 시 SourceId 일괄제거). 모디파이어 적용/해제 생명주기는 **EditMode로 검증**(`StatsSystem.GetValue` 변화→복귀). ⚠️ *인게임에서 실제로 빨라지려면* 이동이 런타임 스탯을 읽어야 하는데 현재 이동속도는 `masterData.Speed` 상수 → **이동→Stats 배선이 선행**(아래 Open Question). Phase 1 코어/테스트는 그 배선 없이도 성립. **Luban `TbStatusEffect` 테이블은 인게임 적용(배선) 단계로 미룸** — Phase 1 코어는 LOP-Shared 순수 C#만(Luban·인프라 파이프라인 비포함). 레거시 무변경.
- **Phase 2 — Ability:** LOP-Shared `AbilityData` 구조체 + `Abilities`/`AbilitySlot` 컴포넌트 + `AbilitySystem`(CanActivate/Activate/Commit/Tick). Luban `TbAction→TbAbility` 리네임 + 필드 추가(TargetingMode/ProducesEffectIds/Range). 이음새로 Phase 1 효과 생성. `world.Tick`에 양 시스템 Tick 배선. 아직 입력/AI 미연결(레거시와 병렬). EditMode 테스트.
- **Phase 3 — 컷오버:** 호출부 이전 — 입력→activate intent, `EnemyBrain`, 메시지핸들러, `LOPEntityView`를 신 시스템으로. 와이어 재설계(durable 쿨다운=스냅샷, "발동"=cosmetic event; `ActionData`/`ActionStartToC`→신 포맷, MessageId 안정성 주의[메모리 gotcha]). 레거시 `Action`/`Status`/`LOPActionManager`/`IActionManager` + `entityManager.UpdateEntities` 어빌리티 경로 은퇴. Dash/Attack 거동 보존.

## 동작 보존 / 검증

- **Phase 1·2:** 순수 추가 — 레거시 경로 무변경, 런타임 거동 0 변화. 신 시스템은 EditMode로 단위 검증(순수 C#의 첫 이득): `CanActivate` 게이트, 쿨다운 만료, `Apply→Tick→Remove`, Refresh/StackMagnitude 스택, Stats modifier add/remove-by-SourceId, DoT 주기.
- **Phase 3:** 컷오버 후 입장→플레이→어빌리티 발동(Dash 임펄스, Attack 연출, 쿨다운)·AI 발동·와이어 송수신·예시 효과 적용/만료가 *이전과 동일 또는 의도된 개선*.
- **컴파일:** GameFramework/LOP-Shared는 `file:` 패키지 — 타입 신설/은퇴 시 양 인스턴스 `refresh_unity`(scope=all,force) → `read_console` 0(메모리 `deleting-package-files-cs2001`).
- **씬 직렬화:** 레거시 `Action`/`Status`는 `MonoComponent` 파생 — 씬/프리팹 직렬화 인스턴스 제거 시 참조 정리(컷오버에서).
- **와이어:** Phase 3 proto 변경 시 `MessageIds` diff 확인(시프트=호환 깨짐).

## 산업 표준 매핑

- **`Ability`**(발동·쿨다운·캐스트) = GAS `UGameplayAbility` / Quantum ability 컴포넌트 / MOBA "ability"·ARPG "skill". 생명주기 = `CanActivate`/`Activate`/`Commit`/`Cancel`.
- **`StatusEffect`**(지속 결과) = GAS Duration/Infinite `GameplayEffect`(+Tag) / Dota2 "modifier" / 일반 "status effect". 엔티티당 컬렉션, 각자 틱.
- **데이터+시스템 분리** = Quantum/DOTS/Bevy 불문율(컴포넌트=데이터, 로직=시스템). GAS의 fat object는 *어휘만* 차용.
- **Ability→Effect 이음새** = GAS `ApplyGameplayEffectToTarget`. 효과는 다출처·단일적용.
- **Stats 연동** = GAS Attribute Modifier ≈ `StatModifier`(Flat/PercentAdd/PercentMult). SourceId 일괄제거가 GE 제거에 대응.
- **cost/cooldown=필드** = Quantum `FP Cooldown`(GAS GE-우회 비채택, 명시적 최소화).

## Out of Scope (다음 / 별도)

- **캐스트/채널/취소, 태그 그래프, 클라 어빌리티 예측** — 콘텐츠/Stage④가 요구할 때. 즉발 + 서버권위로 충분.
- **스택 변형(Independent/Extend/Override)·Source/Target 집계** — 특정 효과가 요구할 때 opt-in. (모디파이어 op Flat/PercentAdd/PercentMult는 이미 있음 — 미루는 것 아님.)
- **풀 deferred 큐 인프라**(OW `ModifyHealthQueue`급) — connection-arch의 YAGNI 그대로. 틱 mutation 단계 내 즉시 적용으로 충분.
- **이동을 어빌리티로 통합** — 이동 커널은 별도 유지(4c #1). Dash는 어빌리티지만 이동 시스템 흡수와 무관.
- **`Spawn`은 어빌리티가 아님 (제외 결정)** — 적 스폰은 *캐릭터 능력*이 아니라 *판 운영 로직*(스폰/점수/승패 = 게임 룰)이라 서버 `GameRuleSystem` 소관. 표준(Quantum `SpawnSystem`/ECS)도 동일. 새 어빌리티 시스템에 `Spawn`을 옮기지 않고, 레거시 `Spawn : Action`은 컷오버에서 폐기(스폰 책임은 `GameRuleSystem`에 이미 있음).

## Open Questions (plan에서 해소)

- **`AbilityData`/`StatusEffectData` 매핑 위치** — 각 사이드 어디서 Luban→LOP-Shared 구조체 매핑(주입 provider vs 호출 지점). 슬롯 비정규화 필드 범위 확정.
- **Phase 3 와이어 세부 포맷** — *방향 결정됨*: durable 쿨다운은 **본인 캐릭터 것만 스냅샷**(내 UI용), "발동"은 **cosmetic event**. 미정 = `ActionData`/`ActionStartToC` 교체 vs 확장의 정확한 포맷 + MessageId 안정성(컷오버에서 확정).
- **`AbilitySystem.Tick` 구동 위치** — `LOPWorld` 페이즈 override vs `world.Tick` 내 직접 호출. 기존 `entityManager.UpdateEntities`(Status/Action 레거시 틱)와의 교대 시점.
- **`TbAction→TbAbility` 리네임 범위** — Phase 2에서 infra(convert script `TABLES`/`FIELD_RENAME` 키 + Excel) + 양 패키지 regen + `.bytes` 스템 + 로더키 `"tbaction"`. proto `action_code` 필드는 별도 축(컷오버에서 함께 볼지).
- **이동속도 → 런타임 스탯 배선** — 헤이스트가 *인게임에서 실제로* 빨라지려면 이동 커널이 `masterData.Speed` 상수 대신 런타임 값을 읽어야 함. 선택: ⓐ Phase 1은 모디파이어 생명주기만(EditMode) 검증, 인게임 헤이스트는 후속 / ⓑ `EntityStatType`에 `MoveSpeed` 추가 + 엔티티 생성 시 `masterData.Speed`로 시드 + 이동 호출부(클·서)가 `StatsSystem.GetValue(MoveSpeed)` 사용 → 헤이스트 관찰 가능. *결정 필요.*

## 진행

- [x] 개념·네이밍 리서치 (`Ability`/`StatusEffect` 확정, 충돌 근거)
- [x] 구조·생명주기·메커니즘 리서치 (GAS 어휘 + Quantum anemic + Stats 연동 + Ability↔Effect 이음새)
- [x] 전 repo usage 인벤토리 (레거시 골격·동작경로·Luban 설정 위치)
- [x] 범위 확정 (World 이전, Ability+StatusEffect 둘 다, 시스템=LOP-Shared, 표준 비계승)
- [x] Open Question 1·2·3 결정 (① 쿨다운=본인 것만 스냅샷 / 발동=cosmetic event, ② `Spawn`=게임룰이라 어빌리티 제외, ③ 검증용 효과=헤이스트 버프[이동속도 +X% PercentAdd])
- [x] 실측 시그니처 교정 (`StatModifier` string SourceId·Source enum 없음·`ModifierType` 3종 기존, `EntityStatType`=4 primary로 Speed 스탯 부재 → 이동→Stats 배선 Open Question 추가)
- [x] 이 spec 작성
- [x] spec self-review (실측 시그니처 교정 반영)
- [x] **이동속도→Stats 배선 결정 = ⓐ** (Phase 1=모디파이어 생명주기 EditMode 검증만; MoveSpeed 스탯 배선 + 인게임 헤이스트는 후속 인게임-적용 단계로)
- [x] **Phase 1 구현 plan 작성** (`docs/superpowers/plans/2026-06-26-statuseffect-core.md`)
- [ ] 사용자 plan 리뷰 → Phase 1 구현 착수
- [ ] 사용자 spec 리뷰
- [ ] 구현 plan 작성 (`writing-plans`)
