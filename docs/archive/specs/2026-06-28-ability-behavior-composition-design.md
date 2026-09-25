# 어빌리티 behavior 조합 — 타입 있는 Effect 리스트 (B 전환)

**Date:** 2026-06-28
**Branch (제안):** `feature/ability-behavior-composition` (LOP-Shared / Client / Server / infrastructure / MasterData-Client·Server)
**Related:** [어빌리티 페이즈 머신](2026-06-27-ability-phase-machine-design.md) (Startup/Active/Recovery — 이 위에 얹음) · [Ability/StatusEffect World Core 설계](2026-06-26-ability-statuseffect-world-core-design.md) · [마스터데이터 키 규약](../../lop-repo-topology.md) (int id) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [아키텍처 가이드라인](../../architecture-guidelines.md) ("업계 표준 명명"·anemic) · 메모리 [[ability-statuseffect-world-core]] · [[masterdata-key-convention]]

## Goal

어빌리티가 *하는 일*을 **타입 있는 작은 레코드(`AbilityEffect`)들의 리스트**로 조합하도록 데이터 모델을 바꾼다. 현재는 `TbAbility`의 평면 컬럼(`produces_effect_id`, `motion_speed`)을 **0/비0으로 추론**(덕 타이핑)해 동작을 가른다 — 타입 구분자가 없어 behavior가 늘수록(공격=데미지, 넉백 등) 컬럼이 비대해지고 판별이 지저분해진다.

이 전환으로 **Attack을 표준 방식으로** 얹을 수 있다: 공격 = "어빌리티에 `DamageEffect` 하나 추가" 가 된다. (Attack 어빌리티화의 토대 — 페이즈 머신 spec의 slice 3을 이 모델 위에서 수행.)

## 범위 — 바꾸는 것 / 유지하는 것 (좁힘)

오해 방지로 못박는다. **이 작업의 본질은 "어빌리티가 뭘 하는지"의 표현을 평면 덕타이핑 → 타입 있는 effect 리스트로 바꾸는 것** 하나다. 라이프사이클(효과를 어디서 처리하나)은 *이미 맞고 그대로 둔다.*

| | 상태 |
|---|---|
| **유지(이미 맞음)** | 라이프사이클 분리 — 오래 사는 효과(헤이스트 버프)는 독립 `StatusEffect` 컴포넌트로 떨어져 `StatusEffectSystem`이 틱(방식 3); 어빌리티가 살아있는 동안만의 즉각 행동(대시 push, 공격 판정)은 어빌리티가 처리(방식 2). 수명에 따라 2/3 혼합 = GAS와 동일, 변경 없음. |
| **바꿈(이 작업)** | ① 평면 컬럼(`motion_speed`/`produces_effect_id`/향후 `damage`…) 덕타이핑 → **타입 있는 `AbilityEffect` 리스트**(한 어빌리티가 여러 개·섞어서·같은 타입 다중 가능). ② 어빌리티 소유 effect 실행을 **중앙 실행기 + 타입 핸들러 디스패치**(방식 A 스캔 → 방식 B 표준). |

→ 종류 추가(공격·넉백…)가 "컬럼 + 스캔 시스템 추가"가 아니라 **"effect 타입 1개 + 핸들러 1개 추가"** 가 된다.

## 배경 / 동기 — 왜 B인가

현재 두 동작이 **서로 다른 비대칭 경로**로 처리된다:

| 동작 | 현재 처리 |
|---|---|
| 헤이스트(상태효과) | `AbilityData.ProducesEffectIds` → 코어 `AbilitySystem`이 Active 진입 시 `StatusEffectSystem.Apply` |
| 대시(이동) | side `AbilityMotionSystem`이 raw `TbAbility.MotionSpeed > 0`을 매 틱 직접 읽어 밂 (코어 `AbilityData`엔 없음) |
| 공격(데미지) | **아직 어빌리티 아님** — 레거시 `Action`/`Attack.cs`(서버) |

평면 와이드 테이블 자체는 업계에 존재하나(WoW `spell.dbc`), **진짜 표준은 "타입 있는 effect/behavior 조합"** 이다. 우리 [페이즈 머신 spec](2026-06-27-ability-phase-machine-design.md)도 이미 목표를 "StatusEffect / Damage / Motion **분리 위임**"으로 박아두었다. 이 spec이 그 분리를 *데이터·런타임 구조*로 실현한다.

## 산업 표준 매핑 (리서치 박제, 2026-06-28)

웹 리서치(GAS · Dota · 격투게임 · ECS · WoW · Game Programming Patterns) 결론:

### 1) 조합 모델 = 평면 blob ❌ → "타입 있는 레코드 리스트" ✅ (만장일치)

| 시스템 | 어빌리티가 가진 것 | "하는 일" 단위 | 데미지 | 상태효과 | 이동/임펄스 |
|---|---|---|---|---|---|
| **Unreal GAS** | `GameplayAbility`가 effect/task 참조 | `GameplayEffect`(데이터 변화) / `AbilityTask`(시간동작) / `GameplayCue`(연출) | Instant GE + **ExecCalc** | Duration/Infinite GE + **Modifier** | **AbilityTask**(RootMotion) — GE 아님 |
| **Dota 2** | 모디파이어 + 데미지 + 모션컨트롤러 | 셋이 **별 메커니즘** | `ApplyDamage`(damage_type enum) | Modifier | **Motion controller**(H/V 채널) |
| **격투게임** | startup/active/recovery 타임라인 | active 창의 **히트박스** | hitbox damage 필드 | hitstun(지속) | knockback(angle+base+scale) **벡터** |
| **WoW** | `Spell` 헤더 + `SpellEffect` 자식 다수 | `SpellEffect`(effect_type enum) | `SCHOOL_DAMAGE` | `APPLY_AURA` | `KNOCK_BACK` |

→ **umbrella = "Effect"**, 타입 판별자로 구분되는 리스트. 평면 blob은 "god class 안티패턴"으로 명시됨(작고 고정된 집합에만 YAGNI 예외).

**실행도 표준 형태가 있다 — 타입별 핸들러 디스패치.** WoW TrinityCore는 `effect_type`으로 인덱싱된 핸들러 테이블(`Spell::EffectSchoolDamage`/`EffectApplyAura`/`EffectKnockBack`)을 한 곳에서 순회·디스패치한다. GAS는 Execution 객체(타입별 calc), ECS는 타입별 시스템. 공통 = **단일 실행기가 effect 리스트를 순회하며 타입별 핸들러로 디스패치**(아래 "Effect 실행" 참고). 레이어/사이드 분리는 *핸들러 등록 차이*로 표현(디스패치는 한 곳).

### 2) ⚠️ Motion은 "effect"가 아니라 별 메커니즘 (모든 시스템 공통)

데미지·상태효과는 effect 모델에 깔끔히 맞지만, **이동(대시/넉백)은 어디서나 별개 채널**이다 — GAS=Task, Dota=motion-controller(이동속도와 *무관*), 격투=hitbox의 launch 벡터. **데이터 authoring은 effect 행으로 통일해도, 런타임은 모디파이어 경로가 아니라 모션/물리 경로로 라우팅**한다 (WoW도 `KNOCK_BACK`을 effect 행으로 두되 런타임은 이동 처리). 우리 "dash=motion ability" 결정과 정확히 일치.

### 3) 데이터 표현 (rename 비용 최대 결정) = 타입 판별자 있는 1:N

스프레드시트형 마스터데이터의 표준 = **WoW `Spell` + `SpellEffect`** 패턴: 부모 헤더 + 자식 effect 행 다수, 각 행에 `effect_type` enum + 타입별로 해석되는 공유 파라미터 컬럼. TrinityCore/AzerothCore가 ~20년 검증.

> 출처: GASDocumentation(tranek) · Epic GameplayEffects 문서 · moddota/Valve API(ApplyDamage·Forced Movement) · dustloop/ssbwiki frame data · Overwatch GDC2017(Statescript) · wowdev.wiki `DB/SpellEffect` · TrinityCore `SpellEffectInfo` · Fowler STI · Game Programming Patterns(Component/Type Object).

## 설계

### Effect 조합 모델 (LOP-Shared 코어)

`AbilityData`(readonly struct)가 **`AbilityEffect[] Effects`** 를 가진다. `AbilityEffect`는 타입 있는 작은 데이터 레코드(순수 C#, 엔진 비의존 — *스펙*만 담고 *행동*은 안 함):

```
AbilityEffect (추상 베이스: 공통 메타 — 예: 발화 시점)
 ├─ DamageEffect    { Amount, Range, Angle, ... }      // 데미지 + 타게팅 형상
 ├─ ModifierEffect  { StatusEffectId }                  // 적용할 StatusEffect 참조 (헤이스트)
 └─ MotionEffect    { Speed }                           // 전방 이동 속도 (대시)
```

- **순수 데이터**: `DamageEffect.Amount`/`Range`/`Angle`, `MotionEffect.Speed`는 그냥 숫자다 → LOP-Shared(noEngine) 안에 둬도 안전. *행동*(Physics.OverlapSphere, Rigidbody)은 side에 남는다.
- `AbilityData.ProducesEffectIds`(현재) → **`Effects`로 대체**. 헤이스트의 effect id는 `ModifierEffect.StatusEffectId`로 들어간다.
- 타게팅(현재 미사용인 ability-level `TargetingMode`/`Range`) → **effect별로 이동**(WoW 정합: 각 SpellEffect가 자기 타겟). `DamageEffect`가 자기 `Range`/`Angle`을 가짐. → ability-level `TargetingMode`/`Range`는 제거.

### Effect 실행 — 타입별 핸들러 디스패치 (표준)

표준(WoW `Spell::EffectXXX` — `effect_type`으로 인덱싱된 디스패치 테이블 / GAS Execution 객체 / ECS 타입별 시스템) = **한 실행기(executor)가 어빌리티의 effect 리스트를 순회하며 각 effect를 그 타입에 등록된 핸들러로 디스패치**한다. "여러 시스템이 각자 `ActiveAbility`를 폴링"이 아니라 **단일 순회 · 단일 디스패치 · 타입별 핸들러**.

우리 레이어 분리(코어 순수 / 데미지=서버권위 / 모션=side 물리)는 **핸들러를 사이드별로 등록**해 푼다 — 즉흥 폴링이 아니라 **포트&어댑터(의존성 역전)**:

- **LOP-Shared(코어)**: `IAbilityEffectHandler`(포트) + `AbilityEffectExecutor`(순수 — effect 리스트 순회·타입별 핸들러 호출). 페이즈 머신(`AbilitySystem.Tick`)이 Active 진입/매 틱에 executor를 구동. 코어는 핸들러를 **인터페이스로만** 알아 엔진/MasterData 비참조 유지.
- **side(어댑터)**: 타입별 핸들러 구현, DI로 **사이드별 등록**:

| Handler | 등록 | cadence | 행동 |
|---|---|---|---|
| `ModifierEffectHandler` | 클·서 | Active 진입 1회 | `ModifierEffect.StatusEffectId` resolve(MasterData) → `StatusEffectSystem.Apply` |
| `MotionEffectHandler` | 클·서 | Active 매 틱 | `MotionEffect.Speed`로 전방 물리 push (대시; 클라 예측) |
| `DamageEffectHandler` | **서버만** | Active 진입 1회 | `DamageEffect`의 Range/Angle로 OverlapSphere+cone → `LOPCombatSystem.Attack` |

- **서버/클라 차이 = 등록 핸들러 집합의 차이**(같은 어빌리티 데이터, 다른 핸들러 셋). 클라엔 `DamageEffectHandler` 미등록(서버권위) — 데미지 연출은 기존 `DamageEventToC` 경로. → server-authoritative + 클라 예측의 표준 실현.
- **resolve는 핸들러 안에서**(`ModifierEffectHandler`가 MasterData provider 보유): 코어는 effect의 *id*만 들고(pure ref), 핸들러가 런타임 데이터로 푼다. → 기존 `PendingEffects`(activator가 미리 resolve해 넘기던 우회)를 **버린다**.
- **executor 입력 = `ActiveAbility`에 실린 resolve된 `AbilityEffect[]`**: side `AbilityActivator`가 발동 시 `AbilityData.Effects`(순수 데이터)를 `ActiveAbility`에 실어두면, 코어 executor가 매 틱 그걸 순회(코어가 MasterData를 만지지 않음).
- **cadence**: instant(Active 진입 1회) vs per-tick(Active 매 틱) — executor가 진입 훅 + 틱 훅 둘 다 제공, 각 effect/handler가 자기 cadence 선언(GAS Instant vs Duration/periodic 대응).

> ⚠️ **구현 정정 — executor는 *host-driven* (B0.3 실측, DI 순환 회피).** 당초 이 절은 "코어 `AbilitySystem.Tick`이 executor 구동"으로 적었으나, 그렇게 하면 **DI 순환**이 생긴다: `runner → IWorld → AbilitySystem → executor → MotionEffectHandler → IEntityManager → entityManager(팩토리들) → … → AbilitySystem`(상호 의존, VContainer Lazy 재진입 예외). 게다가 *effect 적용이 side(Rigidbody/entityManager)를 만지는 것은 본디 코어가 아니라 host egress 책임*(WorldEventBuffer를 host가 드레인하는 결과 동일). → **수정:**
> - 코어 `AbilitySystem.Tick` = **페이즈 전진만**(effect 미적용, 순수).
> - `AbilityEffectExecutor.DriveActiveEntity(caster, entityManager, tick)`를 **host(`LOPRunner`)가 `world.Tick` 뒤 매 틱·엔티티마다 구동**. enter(=StartupEndTick)/tick cadence는 executor가 소유.
> - 핸들러는 `entityManager`를 **DI 주입이 아니라 `AbilityEffectContext`로 받음**(host가 채움). 핸들러에 DI로 박으면 위 순환 재발(entityManager 그래프 ↔ executor 그래프가 얽힘). ctx 전달 = 서비스 로케이터 아님(파라미터 주입).
>
> ⚠️ **`ctx.EntityManager` = interim (근본 아님).** host-driven 배치는 원칙 정합이지만, 핸들러가 entityManager를 통해 *side Rigidbody*에 닿는 것 자체는 **속도 권위가 아직 side(Rigidbody)에 있어서**다. **근본 수정 = Stage④ "접근 B"**(이동을 코어 `World.Entity`의 velocity에서 처리 = 속도 권위 Rigidbody→World 이전). 그러면 `MotionEffectHandler`가 *순수 코어*가 되어 `ctx.EntityManager` 자체가 사라진다. 그때까지 이 ctx 다리는 의도된 interim. (대시 reconciliation 갭 문제도 같은 Stage④ 자리에서 해소 — netcode-redesign §3.1.)

### 데이터 표현 (Luban) — 결정 필요

논리 모델 = WoW 패턴(타입 있는 1:N). 구체 Luban 형태 두 후보:

| 후보 | 형태 | 장점 | 단점 |
|---|---|---|---|
| **(권장) 다형 bean 리스트** | `TbAbility`에 `effects: list<AbilityEffect>` 컬럼, `$type`로 서브타입 선택 (Luban 네이티브 다형성) | 타입별 전용 필드(sparse 컬럼 없음), 타입 안전, 한 테이블 | 현 평면 convert 스크립트가 bean 상속·list 컬럼을 못 만들어 **Ability 스키마 수기 author 필요** |
| (대안) 자식 테이블 | `TbAbility` + `TbAbilityEffect(ability_id, index, effect_type, …공유 param)` | WoW 정확 대응, 평면 테이블 | 공유 param sparse NULL, 비-유니크 키 테이블이라 convert 파이프라인 확장 필요 |

둘 다 표준이다. **정확성 기준으로 선택**(파이프라인 편의 아님): 우리 런타임은 객체지향(C# 타입 있는 effect 객체)이라 **다형 bean 리스트가 런타임 객체 모델과 1:1**(임피던스 불일치 0) — 자식 테이블은 관계형 정통이나 런타임 객체로 한 겹 더 변환이 든다. → **다형 bean 리스트 채택.**

> ✅ **Step 0 검증 완료(2026-06-28, 격리 샌드박스 스파이크).** Luban 4.9.0이 다형 bean 리스트 컬럼을 생성·직렬화함 — `id=3 → [Motion, Damage]`처럼 **한 리스트에 섞인 다중 타입**까지 OK. 생성 C# = `abstract AbilityEffect : Luban.BeanBase`(타입-id 디스패치) + 서브타입들(`__ID__` 상수) + `Ability.Effects : List<AbilityEffect>`. 작성 문법 핵심(plan에 전체 레시피): ① 헤더 옵션 구분자 `#`(4.x, `&` 폐기) → 컬럼 `effects#sep=,`, 타입 `list,AbilityEffect`; ② `__beans__`의 `*fields`는 **셀 병합 필수**(미병합 시 `alias 列 누락` 에러); ③ 추상 베이스는 **암묵**(parent 없고 자식 있으면 자동 abstract, 플래그 없음); ④ 데이터 셀 = `Subtype,field…`(첫 토큰=`$type`=bean 이름), 리스트 원소는 **칸(열)으로** 나열. → **자식 테이블 폴백 불요**(도구 한계 없음).
>
> ⚠️ 단, 현 convert 스크립트(`convert_source_to_luban.py`)는 source 평면 → bean 자동유도라 다형 bean을 못 만든다. **Ability는 첫 수기 정의 bean이 됨** — convert가 Ability를 특수 처리하거나 Ability 스키마(`__beans__` 행 + `#Ability.xlsx`)를 수기 author. (B0.2 결정.)

### 명명 (업계 어휘 — 임의 명명 금지)

| 개념 | 채택(권장) | 근거 |
|---|---|---|
| umbrella "어빌리티가 하는 일 하나" | **`AbilityEffect`** | GAS GameplayEffect / WoW SpellEffect |
| 리스트 필드 | **`Effects`** (`AbilityEffect[]`) | GAS 다중 GE / WoW 1:N |
| 타입 판별자 | Luban `$type` / C# 서브타입 | WoW `Effect` enum / Luban `$type` |
| 데미지 | **`DamageEffect`** | WoW SCHOOL_DAMAGE / Dota ApplyDamage |
| 상태효과 적용 | **`StatusEffectApplyEffect`** | WoW `APPLY_AURA` 어조 — "StatusEffect를 적용하는 effect". 우리 `StatusEffect`/`StatusEffectData`와 혼동 없음(`ModifierEffect`보다 의미 또렷) |
| 이동/임펄스 | **`MotionEffect`** | Dota Motion Controller / WoW KNOCK_BACK |

> 명명 확정(2026-06-28): `ModifierEffect` 대신 **`StatusEffectApplyEffect`** — 코드베이스에 이미 `StatusEffect`가 있어 "modifier"보다 "apply status effect"가 또렷. (effect 끝 중복이 거슬리면 plan에서 `StatusEffectApply`로 줄임 가능.)

## Phasing

페이즈 머신 spec의 로드맵(slice 1 페이즈머신 ✅ / slice 2 dash ✅ / slice 3 attack)에서, **slice 3을 이 B 모델 위에서** 수행한다. 두 조각으로 나눔:

### B0 — 조합 모델 도입 + 기존 이전 (거동 보존)

평면 컬럼(`produces_effect_id`, `motion_speed`)을 `effects` 리스트로 전환. **새 게임플레이 0.**
- **데이터**: `TbAbility`에 `effects` 추가. 헤이스트 `produces_effect_id=1` → `[ModifierEffect{StatusEffectId=1}]`; 대시 `motion_speed=15` → `[MotionEffect{Speed=15}]`. 두 평면 컬럼 제거.
- **코어**(LOP-Shared): `AbilityEffect` 베이스+3 서브타입, `AbilityData.ProducesEffectIds`→`Effects`(+ ability-level `TargetingMode`/`Range` 제거), `IAbilityEffectHandler` 포트 + `AbilityEffectExecutor`, `ActiveAbility.PendingEffects`→resolve된 `Effects` 보유, `AbilitySystem.Tick`이 executor 구동.
- **side**: `ModifierEffectHandler`·`MotionEffectHandler` 구현+등록(클·서). 기존 `AbilityMotionSystem`의 raw `MotionSpeed` 읽기 → `MotionEffectHandler`로 흡수. `AbilityActivator`는 발동·resolve된 effects 전달만(타입별 추출은 executor/handler가).
- **검증**: EditMode(executor 디스패치·cadence·핸들러 등록별 동작) + 플레이 무회귀(헤이스트 +30%/대시 동일).

### B1 — Attack = DamageEffect 추가 (slice 3 본체)

조합 모델 위에 데미지 effect 타입을 얹어 공격을 어빌리티화.
- **데이터**: 공격 어빌리티 행(들) + `[DamageEffect{Amount,Range,Angle}]`. (레거시 `Attack.cs`의 `range=2`/`angle=90`/`Duration*0.5` 값이 여기로.)
- **서버**: 신규 `DamageEffectHandler`(서버만 등록) — executor가 Active 진입 시 디스패치 → `DamageEffect`의 Range/Angle로 OverlapSphere+cone 타게팅 → `LOPCombatSystem.Attack`. (클라는 핸들러 미등록 = 데미지 미적용, 연출은 `DamageEventToC`. combat 서버권위 유지.)
- **부여·발동**: 공격 어빌리티 grant + 입력 트리거(레거시 attack actionCode 경로 대체 또는 병행). 캐릭터별 로드아웃(knight/archer/necromancer)은 별도(아래 Out of Scope).
- **검증**: 데미지·사망·연출이 레거시와 동등(기존 `WorldEventBuffer→WorldEventSink→DamageEventToC` + HP 스냅샷 파이프라인 그대로).

### 이후 (페이즈 머신 spec 로드맵 그대로)
- **slice 4** — 발동 cosmetic 와이어(GameplayCue 대응, `ActionStartToC` 대체).
- **slice 5 (3f)** — 레거시 `Action`/`LOPActionManager`/`TbAction`/`Spawn`/액션 와이어 은퇴.

## 영향 받는 파일 (개략 — 세부는 plan)

- **LOP-Shared**: 신규 `AbilityEffect`/`DamageEffect`/`ModifierEffect`/`MotionEffect`, `IAbilityEffectHandler`(포트), `AbilityEffectExecutor`; `AbilityData`(Effects 필드, TargetingMode/Range 제거), `Abilities/ActiveAbility`(PendingEffects→Effects), `AbilitySystem.Tick`(executor 구동).
- **infrastructure**: `source/Ability.xlsx`(effects 다형 리스트 표현) + convert 스크립트(bean 상속·`$type`·list 지원) + `gen.sh` 재생성.
- **MasterData-Client/Server**: 재생성된 `Ability.cs`/`TbAbility.cs` + effect bean 클래스들 / `tbability.bytes`.
- **Client/Server 공통**: `AbilityDataProvider`(effects 매핑), 신규 `ModifierEffectHandler`·`MotionEffectHandler`, `AbilityActivator`(resolve된 effects 전달), `GameLifetimeScope`(핸들러 등록), 기존 `AbilityMotionSystem` 흡수/제거.
- **Server**: 신규 `DamageEffectHandler`(B1 — 타게팅+combat), `GameLifetimeScope` 등록.

## Out of Scope (후속/별도)
- **캐릭터별 어빌리티 로드아웃** — 현 grant-all(TEMP) 유지. 별도.
- **AI 공격 발동** — `EnemyBrain`의 레거시 attack 경로. slice 5 또는 별도.
- **타게팅 모드 일반화**(Unit/Point 락온 등) — B1은 Direction/cone(레거시 동등)만.
- **다중 동종 effect·effect별 발화 페이즈 커스터마이즈·채널링/멀티히트** — 필요해질 때.
- **발동 cosmetic 와이어 / 쿨다운 UI** — slice 4.
- **레거시 은퇴** — slice 5(3f).

## Open Questions (plan에서 해소)
- **Luban 다형 list 문법·convert 스크립트 영향** — 다형 bean 리스트가 우리 Luban 버전·평면 convert로 실현 가능한지 검증. (기능적으로) 불가 시에만 자식 테이블 폴백. (rename 비용 최대 항목 — **plan 1순위**.)
- **`AbilityEffect` C# 형태** — 추상 클래스 vs 태그된 struct(union). 롤백(Stage④) 값복사 고려 시 struct 유리하나 다형 리스트엔 클래스/인터페이스가 자연. 페이즈머신 `ActiveAbility`가 이미 `StatusEffectData[]` 참조 보유 → 참조형 허용 선례.
- **`DamageEffect` 발화 시점** — Active 진입 1회(레거시 `Duration*0.5` 동등) vs effect에 발화 오프셋 필드. B1은 진입 1회로 시작.
- **입력 트리거** — 공격을 ability_id 경로로 옮길지(기존 attack actionCode 병행/대체). strangler-fig로 병행 후 slice 5 제거 권장.

## 진행
- [x] brainstorm — B(behavior 조합) 채택, 글로벌 스탠다드 지향(사용자 결정)
- [x] 리서치 — 조합 모델(타입 리스트) + 데이터 표현(타입판별 1:N) + Motion=별 메커니즘 + 명명 (웹, 다중 소스 교차검증)
- [x] 이 spec 작성
- [x] spec self-review (런타임 적용을 표준 핸들러 디스패치로 교정, "기존 변경 최소" 프레이밍 제거)
- [x] 사용자 spec 리뷰 (라이프사이클 2/3 "이미 맞음 유지" 합의 / 범위 = 평면→타입리스트+실행기 / 명명 `StatusEffectApplyEffect` / 데미지 발동=데이터 틱)
- [ ] plan 작성(`writing-plans`) — Luban 다형 list 검증 1순위 → 구현 착수
