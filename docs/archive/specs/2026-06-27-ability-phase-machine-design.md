# 어빌리티 페이즈 머신 — Startup/Active/Recovery (dash/attack 어빌리티화 Slice 1)

**Date:** 2026-06-27
**Branch (제안):** `feature/ability-phase-machine` (LOP-Shared / Client / Server)
**Related:** [Ability/StatusEffect World Core 설계](2026-06-26-ability-statuseffect-world-core-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (결정론 틱·deferred) · [netcode-redesign](../../netcode-redesign.md) (결정론 서버권위) · [아키텍처 가이드라인](../../architecture-guidelines.md) ("업계 표준 명명"·anemic) · 메모리 [[ability-statuseffect-world-core]]

## Goal

어빌리티에 **시간 구조(페이즈)** 를 도입한다 — 현재 어빌리티는 *즉발*(발동 즉시 효과 적용, 시간 0)인데, 실제 액션(공격·대시)은 **준비 → 판정 구간 → 후딜**의 시간 흐름을 가진다. `Startup → Active → Recovery` 틱 기반 프레임 데이터 머신을 코어(`AbilitySystem`)에 추가하고, **Active 창 = behavior가 살아있는 구간**으로 삼는다. 이 슬라이스는 *그 토대*만 — dash 임펄스·attack 데미지(side behavior)는 후속 슬라이스가 Active 창에 꽂는다.

**거동 보존:** 유일 어빌리티 헤이스트를 `startup0/active1/recovery0`(즉발 등가)로 두어 HASTE 버튼/H키 결과가 3e와 동일.

## 배경 / 동기 (brainstorm 결론)

dash/attack를 어빌리티로 옮기려는데, brainstorm에서 드러난 것:
- **공격·대시는 시간 구조가 표준** — 격투/액션게임의 `startup/active/recovery`(준비/판정/후딜). 히트 판정은 **active 창에만**. "칼 들기→궤도 중 피격→칼집에", "총 뽑고 쏜다"가 정확히 이것. 우리 레거시 Attack조차 이미 `Duration` + `Duration*0.5`에 타격(조잡한 페이즈).
- **즉발(Slice 검토 초기 ⓐ)은 비표준** — 폐기. 페이즈(ⓑ) 채택.
- **타이밍은 시뮬 틱으로 구동, 애니 아님** — 결정론 서버권위+롤백 netcode라 애니구동(GAS AnimNotify)은 비결정적이라 깨짐. 격투 롤백 netcode가 프레임 데이터를 쓰는 바로 그 이유. 애니는 cosmetic 팔로워.

## 산업 표준 매핑 (리서치 박제)

| 개념 | 표준 | 우리 |
|---|---|---|
| 공격 시간 구조 | 격투 **frame data: startup / active / recovery** (active 창에만 히트박스) | `StartupTicks / ActiveTicks / RecoveryTicks` + `AbilityPhase` |
| 타이밍 구동 | 격투/롤백 = **시뮬 틱**(결정론). GAS = 애니 notify(비채택) | `AbilitySystem.Tick`(LOPWorld가 매 틱) |
| 캐스팅 | = 효과 전 윈드업 = **Startup의 한 모습**(시전시간=긴 Startup) | 별 페이즈 아님 → Startup |
| 쿨타임 | **별개 타이머**(상태머신 아님) — MOBA/MMO 공통 | `AbilitySlot.CooldownEndTick`(이미 존재, enum 아님) |
| 채널링 | 지속 active(빔) | Active hold 변형(후속) |
| 효과 모델 | GAS도 데미지=Execution·임펄스=task(효과에 안 넣음) / Dota=modifier·damage instance·motion controller **분리** | `StatusEffect`(modifier) / `Damage` / `Impulse` 분리 위임 |
| busy 잠금 | 격투 recovery lock | `Phase != Ready` 파생 |
| 코스메틱 | GAS **GameplayCue** | 발동 cosmetic 이벤트(후속 와이어 슬라이스) |

> 출처: 격투 frame data(Capcom SF), GAS GameplayEffect/AnimNotify(Epic Docs), Dota modifier/motion controller(Valve). 핵심: **즉발 아님 + 시뮬틱 구동 + 효과/데미지/임펄스 분리** = 표준 3원칙.

## 설계

### 페이즈 모델

```
발동(Activate)  ─ CanActivate(ready+쿨+자원) 통과 시:
  │
  │  페이즈 축(이번 발동의 진행 — 엔티티당 1 active cast):
  │     Startup ──(startupTicks)──> Active ──(activeTicks)──> Recovery ──(recoveryTicks)──> (cast 종료 → Ready)
  │                                   ▲ behavior 살아있는 창
  │
  └  쿨타임 축(별개): CooldownEndTick = 발동틱 + CooldownTicks  (Recovery보다 길 수 있음)

재발동 = Phase==Ready  AND  현재틱 ≥ CooldownEndTick  AND  자원
```

- **엔티티당 동시 1 active ability**(격투 "한 번에 한 동작"). `Abilities`에 nullable `ActiveAbility` 보유. `ActiveAbility == null` ⇔ Ready(busy 아님). 멀티캐스트/콤보는 후속. *(명명: "cast"는 주문 뉘앙스라 근접 포함 위해 `ActiveAbility` — GAS "active ability" 정합.)*
- **Active 창 = behavior 발화 구간**: 이 슬라이스는 코어 concern인 **StatusEffect를 Active 진입 시 1회 적용**으로 이전(현재 TryActivate 즉시 적용 → Active로 이동). dash 임펄스·attack 데미지(side behavior)는 후속 슬라이스가 *같은 Active 창*에서 발화(아래 "behavior 발화" 참고).
- **busy 게이팅**: `CanActivate`에 `ActiveAbility == null` 추가(진행 중엔 재발동 불가).

### `AbilityPhase` enum (신규, namespace LOP)

```
public enum AbilityPhase { Ready, Startup, Active, Recovery }
```
- 캐스팅 → Startup 흡수. 쿨타임 → enum 아님(별 필드). 채널링/중단/시전바 → 후속(플래그·cue).

### 구조 변경

**`AbilityData`** (LOP-Shared, readonly struct) — 프레임 데이터 3필드:
- `CastTimeTicks` → **`StartupTicks`로 리네임**(CastTime=윈드업=startup; 아직 미사용이라 안전). + **`ActiveTicks`**, **`RecoveryTicks`** 추가.
- ctor 변경 → side `AbilityDataProvider`(클·서)가 매핑. **이 슬라이스에선 TbAbility 컬럼 미추가** — provider가 startup/active/recovery를 기본값(헤이스트=0/1/0)으로 채움. *프레임 데이터의 TbAbility 컬럼 외부화는 dash/attack 슬라이스*(실제 프레임 값이 필요해질 때).

**`Abilities` / `AbilitySlot` / `ActiveAbility`** (LOP-Shared):
- `AbilitySlot`(durable, 유지): `AbilityId`, `CooldownEndTick`.
- 신규 `ActiveAbility`(transient, 진행 중 발동): `AbilityId`, `AbilityPhase Phase`, `long PhaseEndTick`, `Entity Target`, `StatusEffectData[] PendingEffects`. (struct 교체 or 클래스 — 아래 Open Question.)
- `Abilities` 컴포넌트: `Dictionary<int, AbilitySlot> Slots`(유지) + `ActiveAbility ActiveAbility`(nullable, 엔티티당 1).

**`AbilitySystem`** (LOP-Shared):
- `CanActivate`: + `abilities.ActiveAbility == null`(busy 게이트). 쿨다운·자원·보유는 유지.
- `TryActivate`: **즉시 효과 적용 → 머신 시작으로 변경**. Commit(자원 차감 + `CooldownEndTick` 설정) 후 `ActiveAbility = { Startup, PhaseEndTick = currentTick + StartupTicks, Target, PendingEffects = producedEffects }`. **효과는 여기서 적용 안 함**(Active로 이연). 시그니처(`...,StatusEffectData[] producedEffects, currentTick`)는 **유지** → side `AbilityActivator` 무변경.
- **신규 `Tick(Entity entity, long currentTick)`**: `ActiveAbility` 진행.
  - Startup 종료(`currentTick >= PhaseEndTick`) → **Active 진입**: `PendingEffects`를 `StatusEffectSystem.Apply`(여기서 효과 발화) + Phase=Active, PhaseEndTick += ActiveTicks.
  - Active 종료 → Recovery, PhaseEndTick += RecoveryTicks.
  - Recovery 종료 → `ActiveAbility = null`(Ready).
  - (즉발 헤이스트 0/1/0: 발동틱에 Startup 즉시 종료 → 같은 틱 Tick에서 Active 진입·효과 적용 → 1틱 후 Ready. 3e와 동일 타이밍.)

**`LOPWorld`** (LOP-Shared) — `Mutation`에서 `EntityRegistry.All` sweep에 `AbilitySystem.Tick` 추가(기존 `StatusEffectSystem.Tick` 옆). ctor에 `AbilitySystem` 주입(클·서 `GameLifetimeScope`에 3d부터 등록됨).

> ⚠️ **틱 순서(결정론)**: 한 틱 안에서 입력→`TryActivate`(ActiveAbility 시작) 후 `Mutation`에서 `Tick`(페이즈 전진)이 흐름. Collection→Mutation 순서 유지.

### behavior 발화 — Active 창 (이 슬라이스 = StatusEffect만; dash/attack 후속)

- **StatusEffect(코어)**: Active 진입 시 `AbilitySystem.Tick`이 직접 적용(위). 이 슬라이스 범위.
- **Damage/Impulse(side)**: 후속 dash/attack 슬라이스가 발화. 설계 방향(이 슬라이스에선 미구현, 박제):
  - 타이밍은 코어가 소유(결정론). **side는 매 틱 코어의 `Abilities.ActiveAbility`(Phase==Active)를 읽어 자기 behavior 발화** — dash=active 매 틱 임펄스(클·서 예측), attack=active 진입 1회 hit 검사(서버 권위 combat). 코어→side 콜백 없음(side가 결정론 상태를 폴). anemic·결정론 유지.
  - "1회만"(attack) vs "매 틱"(dash)은 *side behavior가 결정*. 코어는 "active, N/M 틱" 사실만 노출.

### 거동 보존 / 검증

- **EditMode(LOP-Shared)** — 페이즈 머신 단위 검증:
  - 헤이스트(0/1/0): 발동 → Tick → Active서 효과 적용, 쿨다운 설정, 1틱 busy 후 Ready.
  - startup>0: 효과가 startup 경과·Active 도달 전엔 미적용.
  - busy: 진행 중 `CanActivate`/`TryActivate` false.
  - recovery: recovery 동안 busy, 종료 후 Ready.
- **플레이 무회귀**: HASTE 버튼/H키 → +30% 100틱(3e와 동일). 다른 거동 0 변화(데미지/임펄스 미도입).
- **컴파일**: LOP-Shared file: 패키지 → 클·서 `refresh_unity`(scope=all,force) → 0 에러.

## Phasing (dash/attack 어빌리티화 전체 로드맵에서 이 슬라이스 위치)

1. **(이 spec) 페이즈 머신 코어** — Startup/Active/Recovery + `Tick` + busy + StatusEffect를 Active로 이전. 거동 보존(haste 0/1/0). EditMode.
2. **Dash 어빌리티** — Active 창에 임펄스(side, 클·서 예측). 프레임 데이터 TbAbility 컬럼 외부화 시작. 점프 임펄스 패턴.
3. **Attack 어빌리티** — Active 진입에 hit 검사(서버 `LOPCombatSystem`) + 타게팅(OverlapSphere+cone) + 연출.
4. **발동 cosmetic 와이어** — 어빌리티 발동/히트 이벤트(GameplayCue 대응) — 레거시 `ActionStartToC` 대체.
5. **3f 레거시 은퇴** — `Action`/`LOPActionManager`/`TbAction`/`Spawn`/액션 와이어 일괄 제거.

## Out of Scope (후속/별도)
- **Damage/Impulse behavior 구현** — 2·3 슬라이스. 이 슬라이스는 발화 *창*과 폴 *지점*만 박제.
- **프레임 데이터 TbAbility 컬럼 외부화** — provider 기본값으로 시작, dash/attack에서 컬럼 추가.
- **채널링(지속 active hold)·중단(interrupt)·시전바·멀티히트/스윕 히트박스·콤보/입력버퍼** — 필요해질 때.
- **캐릭터별 어빌리티 로드아웃**(knight/archer/necromancer attack) — grant-all 잔존, 별도.
- **AI 발동·발동 cosmetic 와이어·쿨다운 UI** — 후속.

## Open Questions (plan에서 해소)
- **`ActiveAbility` = struct 교체 vs 클래스(mutable).** 매 틱 갱신이라 클래스가 자연스러우나, 롤백(Stage④) 값 복사엔 struct 유리. 기존 idiom=struct 교체. → 권고: 우선 struct 교체(값 의미 유지), 무거우면 재논의.
- **쿨다운 시작 시점** — 현재 발동틱(Commit). 유지(표준 변형 중 하나). cast-start vs cast-end는 콘텐츠가 요구하면.
- **side behavior 폴 지점** — `LOPRunner`/전용 side 시스템 중 어디서 `ActiveAbility` 폴. dash/attack 슬라이스에서 확정.
- **`Tick` sweep 비용** — 전 엔티티 매 틱 `ActiveAbility==null` 체크(대부분 즉시 반환). StatusEffectSystem.Tick과 동일 패턴이라 OK.

## 진행
- [x] brainstorm — 효과 모델 C(분리 위임) + 페이즈 ⓑ + 시뮬틱 구동 확정(리서치 기반)
- [x] 명명 검증 — Startup/Active/Recovery(액션 표준), 캐스팅=Startup, 쿨다운=별 타이머, AbilityPhase enum rename
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] plan 작성(`writing-plans`) → 구현 착수
