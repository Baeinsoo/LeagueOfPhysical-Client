# Velocity 권위 통합 슬라이스 1 — 모터 단일 권위 + 기여(Contribution) 모델

**Date:** 2026-07-05
**Branch:** `feature/velocity-motor-contribution`
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [netcode-redesign](../../netcode-redesign.md) · [ability-behavior-composition](2026-06-28-ability-behavior-composition-design.md) · [stage4 ability-replay](2026-07-05-stage4-ability-replay-design.md)

## Goal

velocity 저작을 **단일 모터(MovementSystem) 권위**로 통합한다. 지금은 `MovementSystem`이 걷기를 쓰고 대시일 땐 게이트로 비켜주며, `MotionEffectHandler`(대시 효과)가 `World.Velocity`를 **직접** 쓴다 — velocity writer가 두 곳으로 갈린 "효과가 모터 뒤에서 직접 쓰기" 안티패턴. 이 슬라이스는 모터를 **유일 writer**로 만들고, 어빌리티/효과는 velocity를 직접 쓰지 않고 **기여(contribution)를 등록**만 하도록 바꾼다. 대시 = Override 기여로 이행한다. **동작·재조정은 현재와 동일(무회귀), 새 기능·wire 0.** 넉백(Additive 기여) + 넷코드는 슬라이스 2.

## 배경 — 현재 velocity 저작(실측)

- `MovementSystem.Tick`(`MovementSystem.cs`): 걷기/점프를 `World.Velocity.Linear`에 쓴다(`:100`). 단 `AbilitySystem.HasActiveMotionEffect(entity)`면 **early-return(비켜줌)**(`:79-82`). 회전도 여기서 씀(`:104-105`).
- `MotionEffectHandler.OnActiveTick`(`MotionEffectHandler.cs:29`): 대시 Active 동안 `forward(current rotation)×Speed`의 수평 velocity를 `World.Velocity.Linear`에 **직접** 쓴다(Y 보존).
- 둘은 같은 틱엔 안 겹침(게이트로 배타적 hand-off)이지만 velocity 저작이 두 파일로 갈려 있다. **대시가 velocity를 쓸 수 있는 건 "핸들러가 모터보다 늦게 실행돼 덮어쓰기" 때문**(파이프라인: 모터 pass1 → ability.Tick pass2 → DriveAbilityEffects 핸들러). 이 늦-덮어쓰기가 대시의 same-tick 반응을 만든다 — 아래 "순서" 참고.
- `MotionEffect`(`AbilityEffect.cs:39-47`): 필드 `float Speed`뿐. 방향은 런타임에 caster의 `World.Transform.Rotation` forward에서 파생.
- **좋은 소식:** `MotionEffectHandler`는 이미 순수 코어 `World.Velocity`에만 쓴다(Rigidbody 미접촉). 즉 pre-physics velocity는 이미 공유 코어 소유 — 이 슬라이스는 순수 시뮬 레벨이고 Rigidbody/물리 브릿지(축 B)는 안 건드린다.

## 산업 표준 매핑

이 모델은 새 발명이 아니라 표준 조립이다(리서치 2026-07-05, ability-replay 슬라이스와 동일 조사):

- **모터 단일 권위 = 안티패턴 교정.** "어빌리티가 모터 뒤/옆에서 velocity 직접 쓰기"는 문서화된 안티패턴 — 모터가 유일 권위로 남고 어빌리티가 그걸 *구동*해야 한다. (Unreal GAS+GMC 가이드 [reznok "How NOT to Use GAS+GMC"](https://reznok.com/how-not-to-use-gas-gmc/).)
- **기여 모델 = Unreal CMC `RootMotionSource` / Unreal Mover `LayeredMove`.** CMC `FRootMotionSource`: `AccumulateMode {Override, Additive}` + `Priority` + `Duration`(StartTime/EndTime). Mover `FLayeredMoveBase`: `MixMode {OverrideVelocity, AdditiveVelocity, …}`. 우리 `MotionContribution{값, 모드(Override/Additive), 우선순위, 활성 창}`이 여기 1:1.
- **모드가 base velocity를 필터** = ECM2 "modes act as filters rather than external code writing velocity directly"(입력→desired velocity→모드가 실제 velocity로 조정).

> 네이밍: `MotionContribution`/`MotionContributions`(모드 `Override`/`Additive`)로 한다 — "RootMotion"은 애니메이션 루트모션과 혼동되고, 서술적이면서 위 표준(AccumulateMode/MixMode)에 매핑됨을 spec에 명시. (임의 명명 아님.)

## 범위

**한다:**
- `MotionContribution`(엔트리) + `MotionContributions`(World 컴포넌트) 도입.
- `MovementSystem`을 **유일 velocity writer**로: base(입력 걷기) + 기여 리스트를 모드/우선순위로 해소해 `World.Velocity.Linear`에 한 번만 write.
- 대시 이행: `MotionEffectHandler`의 velocity 직접 쓰기 제거 → 대시가 Override 기여로 표현. **동작 파리티**(속도·방향·입력 락·타이밍 현재와 동일).
- 입력 락(`HasActiveMotionEffect` → "활성 Override 기여 존재")도 단일 근거로.

**안 한다 (별개):**
- **넉백 / Additive 실사용 / 스냅샷·wire 확장 (슬라이스 2).** 이 슬라이스의 Additive 경로는 *구현하되 소비자 없음*(슬라이스 2 넉백이 첫 소비자) — 단, 리스트 인프라가 슬라이스 1에 *실사용 소비자(대시=Override)* 를 가지므로 speculative 아님.
- **축 B(Rigidbody→World.Entity 권위, 4e keystone).**
- **Y축(수직) 기여** — Y는 중력/점프 그대로. 기여는 수평만.

## 설계

### A. 데이터 모델 (LOP-Shared, namespace LOP, anemic)

- **`MotionContributionMode`** enum: `Override`, `Additive`.
- **`MotionContribution`**(struct 또는 class): `{ Vector3 Horizontal(수평 값), MotionContributionMode Mode, int Priority, long StartTick, long EndTick(활성 창; currentTick∈[Start,End)) }`. 값은 수평(x,z)만 — Y 미포함.
- **`MotionContributions : Component`**: 활성 기여 컬렉션 보유. 로직 없음(anemic) — 등록/프루닝/해소는 시스템.

### B. 모터 해소 — MovementSystem = 유일 writer

한 틱의 최종 velocity를 한 곳에서 계산·write:
```
base = 입력 desired 수평 velocity(걷기) 또는 무입력 시 0 제동   ← 현 ProcessMovement 재사용
활성 기여 = MotionContributions 중 currentTick∈[Start,End)
# 합성 규칙(표준: Override가 base를 대체, Additive는 그 위에 가산):
root = 활성 Override 있으면 최고 Priority Override값, 아니면 base   (Override면 입력 무시 = 락)
수평 = root + Σ(활성 Additive의 값)
velocity.x/z = 수평;  velocity.y = (중력/점프 그대로)
World.Velocity.Linear = velocity          ← 유일 write
회전: 입력 방향 있을 때만(현행). Override 활성 중엔 회전 미변경(대시가 방향 고정 — 현행과 동일)
만료된 기여(EndTick ≤ currentTick) 프루닝
```
(슬라이스 1엔 Additive 인스턴스가 없어 대시 시엔 `수평 = Override값`으로 현행과 동일. Additive 경로·합성은 모델로 갖추고 테스트하되 첫 실사용은 슬라이스 2 넉백.)
`HasActiveMotionEffect` 게이트(입력 락·회전 억제)는 "활성 Override 기여 존재"로 대체.

### C. 대시 이행 (동작 동일) — 파생(derived), CMC movement-mode 방식

- **대시 = 모터가 `ActiveAbility`에서 파생**(CMC "movement mode" — 내재적, 모터가 계산). 리스트 엔트리로 등록하지 않는다(등록 시점 배선·이중표현 회피). 대시는 어빌리티에만 있고, 모터가 그걸 읽어 velocity로 만든다.
- `MotionEffectHandler`(velocity 직접 쓰기)는 **제거**. 모터가 그 계산을 흡수한다.
- **파생 규칙:** 엔티티의 `ActiveAbility`에 `MotionEffect`가 있고 `currentTick ∈ [StartupEndTick, ActiveEndTick)`(활성 창)이면, 모터가 `forward(현재 rotation)×MotionEffect.Speed`(수평)를 Override로 쓴다(입력 무시=락, Y 보존).
- **파리티 불변식:** 창 `[StartupEnd, ActiveEnd)`·`forward×Speed`·입력 락·Y 보존이 현재와 동일. (창 검사라 전이 틱에도 same-tick 적용 — 현 "핸들러 늦-덮어쓰기"와 동일 타이밍.)
- **재조정 무영향:** 대시는 이미 스냅샷되는 `ActiveAbility`에서 파생 → 재생 때 복원·재활성된 어빌리티에서 모터가 그대로 재파생. **새 스냅샷 필드 0.**
- **리스트(`MotionContributions`)는 외부 Additive용**(넉백=슬라이스 2). 슬라이스 1엔 리스트에 실인스턴스 없음(모터가 null-safe로 합성) — 해소 로직은 갖추고 단위 테스트하되 첫 실사용은 슬라이스 2.

### D. 순서 (표준대로 확정 — 창 검사로 파생, 재정렬 없음)

현재 대시 same-tick 반응은 "핸들러가 모터보다 늦게 velocity를 덮어쓰기"로 성립한다. 모터가 대시를 `ActiveAbility` **경계틱 창** `[StartupEndTick, ActiveEndTick)`으로 판정하면, ability.Tick의 페이즈 전진 순서와 무관하게 same-tick에 대시를 적용한다(창이 곧 타이밍). 그래서 **파이프라인 재정렬 없이**:

**결정:**
- `MovementSystem`은 **현 위치(`LOPWorld.Mutation` pass1) 유지 + 유일 writer.** 단 `Tick` 시그니처에 `currentTick`을 추가한다(창 검사에 필요) — `Tick(entity, long currentTick, float deltaTime)`.
- `MovementSystem`이 `AbilitySystem.TryGetActiveMotionEffect(entity, currentTick)`(창 검사 헬퍼)로 대시를 파생. `HasActiveMotionEffect`(페이즈 기반, 전이 틱에 1틱 지연) 대신 창 기반으로 — 전이 틱 파리티 확보.
- 리스트 Additive 합성은 `MotionContributionSystem.Resolve`로. (슬라이스 1엔 리스트 비어 no-op.)
- `MotionEffectHandler`의 velocity 직접 쓰기 제거. 다른 효과(Damage/StatusEffectApply) 핸들러·`DriveActiveEntity`는 무변경.
- `LOPWorld.Mutation`의 "이동 먼저" 순서 주석은 창 검사로 무의미해짐 → 갱신(순서 자체는 유지, 최소 변경).

**시그니처 파급:** `MovementSystem.Tick(+currentTick)` → 호출처 `LOPWorld.Mutation`(공유), `Reconciler` 재생 루프(클라), `MovementSystemTickTests`(공유) 갱신. 파리티 테스트가 대시 velocity 불변(창 `[StartupEnd, ActiveEnd)`, forward×Speed, Y 보존, 입력 락)을 강제.

### E. 검증

- **EditMode(순수, LOP-Shared):** 모터 해소 — (a) Override 최고 우선순위가 base 대체, (b) 여러 Additive 합산, (c) Override+Additive 동시(Override가 base 대체 후 Additive 가산 여부는 결정 사항 — 아래 Open Q), (d) 활성 창 밖 기여 무시·만료 프루닝, (e) 입력 락(Override 시 입력 무시).
- **파리티(순수):** 대시 시나리오(활성 창 동안)가 리팩터 전 `MotionEffectHandler` 경로와 **동일 velocity 산출**(forward×Speed, Y 보존). 회귀 방지 핵심.
- **플레이:** 대시(지상/공중/방향전환), 걷기, 정지, 점프 — 현재와 체감 동일(무회귀). 재조정 중 대시도 무영향.

## 영향 파일 (개략 — 세부는 plan)

- **신규(LOP-Shared):** `MotionContribution`(+`MotionContributionMode`), `MotionContributions` 컴포넌트, `MotionContributionSystem`(Prune/Resolve), EditMode 테스트(해소 + 대시 파리티).
- **수정(LOP-Shared):** `MovementSystem.Tick`(+`currentTick`, 단일 writer, 대시 파생 + 리스트 합성), `AbilitySystem`(창 검사 헬퍼 `TryGetActiveMotionEffect`), `LOPWorld.Mutation`(`Tick`에 tick 전달 + 주석 갱신), `MovementSystemTickTests`(시그니처 + 대시 테스트 갱신).
- **삭제(LOP-Shared):** `MotionEffectHandler`.
- **수정(클라):** `Reconciler` 재생 루프(`movementSystem.Tick`에 tick 전달), `GameLifetimeScope`(MotionEffectHandler 등록 제거). **수정(서버):** `GameLifetimeScope`(MotionEffectHandler 등록 제거).
- **무변경:** 클·서 호스트 나머지, Rigidbody 브릿지(`LOPEntity`/`PhysicsComponent`/`LOPEntityController`), 스냅샷/wire, ability-replay 재조정(대시 파생 파리티라 무영향), `CharacterCreator`(`MotionContributions` 부여는 슬라이스 2 넉백서).

## Out of Scope

- 슬라이스 2: 넉백(`KnockbackEffect` + Additive 감쇠 기여) + 넉백 기여 스냅샷/복원/재생 + 서버→클라 wire + 재조정 통합.
- 축 B: Rigidbody→World.Entity velocity 권위 이전(4e keystone).
- Y축(수직) 기여, 외력(external force) 채널, CC(조작 불가) 상태.

## 표준대로 확정된 결정 (사용자 결정 아님 — 참고)

- **순서:** 늦은 단일 해소 단계(§D). **대시 표현:** 리스트 속 Override 기여, 재생 때 어빌리티가 결정론적 재등록(§C). **합성:** Override가 base 대체 + Additive 가산(§B). 전부 CMC/Mover 표준.

## Open Questions (plan에서 해소 — 사소한 배선 디테일만)

- **대시 값 방향 해소 시점:** 등록 시 고정 vs 모터-time에 현재 rotation서 계산. 파리티 제약(현행 "매 틱 facing×Speed")을 만족하는 쪽으로 — 입력 락으로 Active 중 rotation 고정이라 대개 무차이. 파리티 테스트로 확정.
- **`MotionContributions` 컴포넌트 부여 대상:** 모든 이동 엔티티 vs 필요 시. (creator 배선.)

## 진행

- [x] 브레인스토밍 합의 (모터 단일 권위 + 기여 모델 (3) 일반형, 대시=Override, 넷코드 무변경, 넉백은 슬라이스 2)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
