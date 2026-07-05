# Velocity 권위 통합 슬라이스 2 — 넉백 (Additive 첫 실사용 + 스냅샷/wire/재조정)

**Date:** 2026-07-05
**Branch:** `feature/velocity-knockback-slice2`
**Related:** [슬라이스 1 — 이동 시스템 단일 권위 + 기여 모델](2026-07-05-velocity-motor-contribution-slice1-design.md) · [stage4 ability-replay](2026-07-05-stage4-ability-replay-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [netcode-redesign](../../netcode-redesign.md) · [ability-behavior-composition](2026-06-28-ability-behavior-composition-design.md)

## Goal

슬라이스 1이 만든 Additive `MotionContribution` 채널의 **첫 실사용 = 넉백**을 끝-끝으로 구현한다. "공격에 맞으면 뒤로 밀린다"를 서버 권위로 발생시키고, **로컬 캐릭이 맞는 경우까지** 스냅샷·wire·재조정을 완주해 러버밴딩 없이 재현한다. 게임플레이는 얇게(공격에 밀림 하나), 무게중심은 **넷코드 골격**(Additive 실사용 + 대시 재생과 같은 스냅샷/재생 패턴)에 둔다.

## 배경 — 왜 넉백이 이런 모양일 수밖에 없나 (합의 근거)

브레인스토밍에서 확인한 세 가지가 넉백의 구조를 강제한다:

1. **"맞는 것"은 예측하지 않는다.** 예측은 내 입력으로 내가 결정론적으로 만드는 것만(대시=자기 유발, 이미 예측). 넉백은 *남이* 가하는 서버 권위 사건 → 클라는 예측하지 않고 **서버에서 받아 재조정**한다.

2. **brake-to-desired 모터가 raw 임펄스를 매 틱 지운다.** `MovementSystem`은 InputBuffer 엔티티의 수평 velocity를 매 틱 통째 재할당하고 Rigidbody `drag=0`. 그래서 velocity를 툭 쳐도 다음 틱 0으로 지워진다. 외부 힘이 모터에 지워지지 않고 *위에 얹히려면* 슬라이스 1의 **Additive `MotionContribution`** 채널이 필요하다 — 넉백이 그 첫 소비자. (업계 표준: Unreal CMC `RootMotionSource` / Mover `LayeredMove`.)

3. **감쇠는 순수 함수여야 한다 (물리 drag 아님).** 물리 drag 감쇠는 (a) LOP는 `drag=0`이라 물리가 수평을 안 깎고, (b) 클(Win)·서(Linux) PhysX 비결정 + 롤백 재생이 전역 `Physics.Simulate`에 의존 → 재현 불가. 대신 감쇠를 `(초기 임펄스, 시작/끝틱, 감쇠 계수)`의 **순수 함수**로 두면 클라가 서술자 하나만 받고 재생에서 결정론적으로 재현한다. 지수 감쇠 `v₀·k^elapsed`는 수학적으로 선형 drag(`v(t)=v₀·e^(−ct)`)와 같은 곡선이라, **물리 solver 없이 drag의 거동**을 얻는다.

> 모터 아키텍처 자체(brake-to-desired vs 유니티 물리 drag 기반)를 물리 drag로 되돌리는 선택지도 검토했으나 기각 — 예측+롤백 넷코드 게임(Source `gamemovement`, Quake, Overwatch, Valorant)은 예외 없이 플레이어 이동을 손코딩된 공유 코드로 구현하고 엔진 Rigidbody drag는 *예측 안 하는 물리 소품*에만 쓴다. 지형 마찰은 drag로 갈 필요 없이 **결정론 커널에 지형별 friction/accel 파라미터를 먹여** 얻는다(Source 방식). 이동 모터는 슬라이스 1 그대로 유지.

## 산업 표준 매핑

- **넉백 = 타임드 힘-소스 (CMC `RootMotionSource` / Mover `LayeredMove`)** — Additive + 활성 창 + 지수 감쇠 곡선. 물리 마찰 기반의 반대편, *물리 독립·정밀* 넉백 표준.
- **서버 권위, 클라 미예측** — Quake/Source/Overwatch: 남이 가한 효과는 서버 결정, 클라 수신·보정.
- **재조정 규칙:** 예측/재생에 영향 주는 상태는 전부 스냅샷·복원 대상(FishNet 명문, Quantum/DOTS 풀-프레임 resim, CMC SavedMove가 root-motion source 캡처). → 넉백 기여 서술자를 재조정 상태에 포함.
- **durable=snapshot / event=cosmetic** (connection-arch): 넉백 기여는 여러 틱 위치에 영향 주는 durable → **스냅샷**으로 나른다(이벤트 아님).

## 범위

**한다:**
- `KnockbackEffect`(effect 데이터) + 서버 `KnockbackEffectHandler`(targeting + Additive 기여 등록).
- `MotionContribution` 지수 감쇠 필드 + `MotionContributionSystem.Resolve` 감쇠 적용.
- 로컬 대상 넷코드 완주: 기여를 서버→클라 상태 스냅샷에 실어 보냄 + 재조정 캡처/앵커 복원/재생.
- `MotionContributions` 컴포넌트를 이동 엔티티에 부여(creator, 클·서).
- EditMode(감쇠 resolve, 재조정 라운드트립) + 서버 targeting 테스트 + 플레이 검증.

**안 한다 (별개):**
- Override 기반 완전 경직 / CC(조작 불가) 상태.
- Y축(수직) 넉백(띄우기), 넉백 중 충돌 반사·벽 처리.
- 축 B(Rigidbody→World.Entity velocity 권위 이전, 4e keystone).

## 설계

### A. 넉백의 자리 — 서버 권위, effect 조합 (LOP-Shared + 서버)

- **`KnockbackEffect : AbilityEffect`** (LOP-Shared, `AbilityEffect.cs`에 추가). 필드:
  - `float Strength` — 초기 세기 v₀.
  - `int DurationTicks` — 활성 창 길이.
  - `float DecayPerTick` — 지수 감쇠 계수 k(0<k≤1).
  - Damage/StatusEffectApply와 동형의 anemic 데이터.
- **`KnockbackEffectHandler` — 서버 전용** (`AbilityEffectHandler<KnockbackEffect>`, 클라 미등록 → executor가 무시 = 서버 권위, 클라는 스냅샷으로 받음). `DamageEffectHandler`와 동형:
  - `OnActiveEnter`에서 자체 `Physics.OverlapSphere` + 부채꼴(`IsInAttackSector`)로 대상을 찾는다.
  - 각 대상의 `MotionContributions`에 Additive 기여 등록: 방향 = `(target.pos − attacker.pos)` 수평 정규화 × `Strength`, 창 = `[currentTick, currentTick + DurationTicks)`, 감쇠 = `DecayPerTick`.
  - `DamageEffectHandler`와 targeting 로직이 중복되지만(각자 OverlapSphere), 조합 순수성(각 effect 독립)을 위해 감수한다 — 값싸고, 향후 targeting 공유는 별도. **알려진 minor 비용.**
- 공격 어빌리티 = effect 리스트 `[DamageEffect, KnockbackEffect]` 조합.

### B. 감쇠 표현 — 지수 곡선 (LOP-Shared)

- `MotionContribution`에 감쇠 필드 추가. Additive push는 활성 창 동안:
  ```
  push(tick) = Horizontal × DecayPerTick^(tick − StartTick)     // Horizontal = 초기 임펄스 v₀
  ```
  `DecayPerTick == 1`이면 기존 상수 거동(슬라이스 1 무회귀). Override 경로는 감쇠 미적용(대시는 파생이라 리스트 밖 — 무영향).
- `MotionContributionSystem.Resolve`가 Additive 합산 시 감쇠를 적용(현재는 상수 합산). `Prune`은 `EndTick` 기준 그대로 — `EndTick`은 세기가 무시할 수준으로 떨어지는 창 끝(핸들러가 `Duration`으로 지정).
- **Additive라 넉백 중 걷기 입력이 살아있다** (base 걷기 + 감쇠 push). 완전 경직을 원하면 Override지만, "밀리며 약간 저항"이 기본 손맛 + 슬라이스 1이 Additive로 지어둠. 완전 경직(CC)은 out of scope.

### C. 넷코드 — 로컬 대상 (핵심)

**결정: 기여를 이벤트가 아니라 "스냅샷 상태"로 나른다.** 넉백은 연출이 아니라 여러 틱 위치에 영향 주는 durable 상태 → 유실 시 재생이 그 창 내내 오예측(러버밴딩). 스냅샷-실림은 자가치유(다음 스냅이 덮음)라 connection-arch의 "durable=snapshot" 규칙과 정합. 이벤트로 durable 재생 상태를 몰면 "event=cosmetic" 규칙과 충돌.

**흐름:**
```
서버:  KnockbackEffectHandler가 대상 MotionContributions에 기여 등록
       → 서버 MovementSystem(공유)이 감쇠 push를 velocity에 반영 → 서버 position 스냅에 나타남
와이어: 서버가 로컬 캐릭 상태 스냅샷에 활성 MotionContribution 목록을 실어 보냄
       (활성 창 동안만; 작고 드묾; 자가치유)
클라:  스냅 수신 → 로컬 엔티티 MotionContributions에 반영
재조정: 기여를 예측 상태 캡처 + 앵커 복원에 포함
       → 재생 루프의 MovementSystem이 이미 복원된 기여를 resolve(결정론 지수 감쇠) → 재현
```

- **클라 `KnockbackEffectHandler` 없음.** 클라는 넉백을 *계산*하지 않고 스냅샷으로 *받는다*(데미지와 동일 권위). 재생은 복원된 기여를 `MovementSystem`이 resolve만 하면 됨 → 클라 변경이 작다:
  1. 스냅샷에서 로컬 캐릭 `MotionContributions` 수신·반영.
  2. 재조정 예측 상태 캡처/복원에 `MotionContributions` 포함(대시 재생의 `PredictedAbilityState`와 같은 뼈대 — 아래 D).
  3. 지수 감쇠는 공유 `Resolve`(B)라 클라 전용 로직 없음.
- **~RTT/2 지연 적용:** 넉백은 서버 틱 T 발생, 스냅은 T+RTT/2 도착 → 클라는 늦게 적용하지만 재조정이 앵커에 기여를 실어 복원+재생 → 부드럽게 따라잡는다(표준). 지연 렌더링(`LocalEntityInterpolator`)이 하드 보정 점프를 흡수.

### D. 재조정 상태 캡처/복원 — 기여를 예측 상태에 포함

대시 재생 슬라이스가 세운 패턴(`PredictedAbilityState` = 어빌리티/상태이상/스탯/마나 틱별 깊은복사 + 앵커 복원)에 **`MotionContributions`를 추가**한다:

- 매 틱 로컬 캐릭 예측 상태 기록에 활성 기여 목록(값 복사) 포함.
- 하드 복원 시 앵커 틱 기여 목록으로 로컬 `MotionContributions` 덮어쓰기.
- 재생 루프는 무변경(이미 `MovementSystem.Tick` 호출) — 복원된 기여를 resolve.

**배치(Open — plan에서 확정):** `PredictedAbilityState`에 `MotionContributions` 필드를 더할지, 별도 병렬 캡처로 둘지. `MotionContributions`가 LOP-Shared 타입이라 대시 재생과 같은 링을 재사용하는 쪽이 자연(대시 재생 spec의 "스냅샷 링 배치" open Q와 동급).

### E. 넷코드 — 원격 대상 (공짜)

원격 엔티티가 맞으면 클라는 그 position/velocity 스냅샷을 보간(`ServerStateReconciler`/`SnapInterpolator`)할 뿐 → **넉백이 위치 스냅샷으로 자연히 보인다.** 원격 대상엔 기여 wire·동기 불필요. (durable=snapshot 규칙이 원격에선 그냥 성립.)

### F. 배선 잔여

- **`MotionContributions` 컴포넌트 부여**: 이동 엔티티(플레이어/AI 등)에 creator가 부여(클·서 양쪽). 슬라이스 1이 넘긴 항목("creator 배선은 슬라이스 2 넉백서").
- **스냅샷 wire proto 자리**: 기존 로컬 상태 스냅(위치/velocity/HP 등 서버→클라)을 확장할지 신규 필드/메시지를 둘지 — plan에서 확정(대시 재생 링 배치 open Q와 동급의 배선 디테일).
- **어빌리티 데이터**: 공격 어빌리티에 `KnockbackEffect`를 조합(MasterData/effect 구성). 튜닝값(Strength/Duration/Decay)은 데이터.

## 검증

- **EditMode (공유):**
  - 지수 감쇠 resolve — 활성 창 안 `v₀·k^elapsed` 정확, 창 밖 무시, `EndTick` 프루닝, `k=1` 상수 무회귀, 여러 Additive 합산, Override+Additive 동시(Override가 base 대체 후 감쇠 Additive 가산).
  - **재조정 라운드트립(핵심):** 넉백 기여가 낀 N틱 시퀀스를 예측 상태로 캡처 → 앵커로 복원 → 재생 → 최종 `MotionContributions/Transform/Velocity`가 라이브와 완전 일치(대시 라운드트립 테스트에 기여 추가).
- **서버 EditMode/통합:** `KnockbackEffectHandler` targeting(부채꼴·range) + radial 방향(공격자→대상) + Additive 기여 등록 필드(창·감쇠).
- **플레이:** RTT 50/150/300ms 주입 + 로컬 캐릭 피격 → 넉백 밀림·러버밴딩 육안 소멸, 원격 피격 넉백 보간, 걷기/대시/정지/점프 무회귀. 넉백 중 걷기 저항(Additive) 체감.

## 영향 파일 (개략 — 세부는 plan)

- **신규(LOP-Shared):** `KnockbackEffect`(AbilityEffect 하위), EditMode 테스트(감쇠 resolve + 재조정 라운드트립).
- **신규(서버):** `KnockbackEffectHandler`, targeting 테스트.
- **수정(LOP-Shared):** `MotionContribution`(감쇠 필드), `MotionContributionSystem.Resolve`(감쇠 적용), 예측 상태 캡처/복원(`PredictedAbilityState` 또는 병렬 — D), `AbilityEffect.cs`.
- **수정(클라):** 스냅샷 수신에서 `MotionContributions` 반영, 재조정 캡처/복원에 기여 포함(`Reconciler`/`LOPRunner`), `GameLifetimeScope`(필요 배선). creator에 `MotionContributions` 부여.
- **수정(서버):** 상태 스냅샷 wire에 활성 `MotionContributions` 직렬화, `KnockbackEffectHandler` DI 등록, creator에 `MotionContributions` 부여.
- **무변경:** 이동 모터 구조(brake-to-desired), 대시(Override 파생), Rigidbody 브릿지, Damage/StatusEffect 경로, 원격 보간(E는 기존 경로 재사용).

## Out of Scope

- Override 기반 완전 경직 / CC(조작 불가) 상태 — 넉백을 Override로 바꾸면 되지만 별도.
- Y축(수직) 넉백(띄우기·에어본), 넉백 중 벽/충돌 반사.
- 축 B: Rigidbody→World.Entity velocity 권위 이전(4e keystone).
- targeting 공유(Damage↔Knockback OverlapSphere 통합) — 지금은 각자 쿼리.

## Open Questions (plan에서 해소 — 배선 디테일)

- **예측 상태 캡처 배치:** `PredictedAbilityState`에 `MotionContributions` 필드 추가 vs 별도 병렬 링. (권장: 대시 재생 링 재사용.)
- **스냅샷 wire proto:** 기존 로컬 상태 스냅 확장 vs 신규 필드/메시지. 활성 기여 목록의 직렬화 형태.
- **`MotionContributions` 부여 대상:** 모든 이동 엔티티 vs 필요 시(넉백 대상이 될 수 있는 것). (creator 배선.)
- **감쇠 파라미터 소유:** `DecayPerTick`을 effect 데이터로 둘지, 전역 상수로 둘지(넉백 종류가 하나뿐이면 상수도 가능).

## 진행

- [x] 브레인스토밍 합의 (넉백=서버 권위 Additive 기여, 지수 감쇠=drag 순수함수, 로컬 대상 완주, 모터 유지)
- [x] 이 spec 작성
- [x] spec self-review
- [x] 사용자 spec 리뷰
- [x] `writing-plans`로 구현 plan 작성 (`docs/superpowers/plans/2026-07-05-velocity-knockback-slice2.md`)
- [x] 구현 (Subagent-Driven, 7 코드 태스크) + 플레이 검증

## 구현 후기 (2026-07-05)

Subagent-Driven으로 7개 코드 태스크 구현·리뷰. **Shared**: `MotionContribution` 지수 감쇠(`DecayPerTick`, `v0·k^elapsed`) + `KnockbackEffect` + `CreateRadialKnockback` 순수 팩토리 + proto(`ProtoMotionContribution` + `EntitySnap.motion_contributions`) — EditMode 74/74 그린. **Server**: `KnockbackEffectHandler`(부채꼴 OverlapSphere, 서버 전용) + `MotionContributions` 부여 + `GetAllEntitySnaps` 직렬화 + TEMP 발동(공격에 넉백 얹기). **Client**: 스냅에서 기여 수신 + 로컬 엔티티 `MotionContributions` 부여 + `Reconciler`가 **서버 스냅에서** 기여 복원(예측 히스토리 아님 — 서버가 가한 것이라 클라 미예측).

**설계 교정(spec §D → plan):** brainstorm 당시 §D는 기여를 `PredictedAbilityState`(클라 예측 캡처)에 넣는 것으로 적었으나, 구현 중 `Reconciler`가 어빌리티 상태를 *클라 자기 예측 히스토리*에서 복원함을 확인 → 서버 넉백은 그 히스토리에 없어 복원 시 유실됨. 그래서 **기여를 `EntitySnap`(서버 권위 스냅샷)에 실어 스냅에서 복원**하도록 교정(position/velocity와 같은 권위 축). `PredictedAbilityState` 무변경.

**최종 전체 브랜치 리뷰(opus): READY TO MERGE** — 6 크로스-레포 불변식 통과(필드 round-trip, 절대 StartTick 결정론, 스냅 단일 복원원[RestoreTo가 MotionContributions 미접촉], 부여 대칭+null 가드, 걷기/대시 무회귀, 서버→클라 단방향). Critical/Important 0. 비차단 nice-to-have 2건(공격자==대상 시 0벡터 기여 직렬화 스킵, `IsInAttackSector` Damage/Knockback 공유).

**플레이 검증(사용자):** 원격 대상 넉백 밀림·감쇠 정상, **로컬 대상(내 캐릭 피격) 러버밴딩 없이 넉백 재현** 확인. 강도 TEMP `strength 8f→5f` 조율. 걷기/대시 무회귀.

**후속(비차단):** MasterData `KnockbackEffect` 승격(현 TEMP 코드 배선 은퇴), `generate_protos.sh`가 `MessageIds.cs`를 regen 전 삭제하는 footgun 수정, Override 완전경직/CC·Y축(띄우기) 넉백.
