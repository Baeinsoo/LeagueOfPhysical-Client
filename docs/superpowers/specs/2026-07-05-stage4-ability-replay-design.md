# Stage④ 슬라이스 — 어빌리티/상태이상 예측 재생 (풀 상태 스냅샷 + 단일 예측 틱)

**Date:** 2026-07-05
**Branch:** `feature/stage4-ability-replay`
**Related:** [netcode-redesign](../../netcode-redesign.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [slice ③ 롤백/재조정](2026-07-04-stage4-rollback-reconcile-design.md) · [slice ② SnapshotHistory](2026-07-04-stage4-snapshot-history-design.md)

## Goal

재조정(reconcile) 재생 구간에서 **이동뿐 아니라 어빌리티/상태이상까지 충실히 재현**한다. slice③은 하드 복원 후 `MovementSystem`만 재생해서, 재생 창(anchor+1 .. now-1) 안에 대시가 걸치면 대시 속도가 재생에서 빠져 위치가 어긋난다. 이 슬라이스는 그 갭을 **업계 표준 구조**(예측 틱을 통째로 재생 + 풀 상태 스냅샷)로 닫는다.

## 배경 — slice③이 남긴 갭

slice③ 재생 루프(`Reconciler.Reconcile`)는 과거 틱마다 `InputBuffer.Current`를 입력 히스토리로 채우고 **`MovementSystem.Tick`만** 호출한다. 어빌리티 발동(`AbilityActivator.TryActivate`)·페이즈 전진(`AbilitySystem.Tick`)·상태이상 만료(`StatusEffectSystem.Tick`)·효과 구동(`AbilityEffectExecutor.DriveActiveEntity`)은 재생하지 않는다.

그 결과, 재생 창 안에 대시가 있으면:
- 어빌리티 상태가 복원되지 않아 재생 중 `HasActiveMotionEffect`가 false → `MovementSystem`이 비켜주지 않고 (입력 0인) 걷기로 velocity를 덮음.
- 대시 속도를 매 틱 써주는 `DriveActiveEntity`가 재생 루프에서 안 불림.
- → 하드 복원이 앵커에서 대시 속도를 되살려도, 첫 재생 틱에서 걷기로 덮여 사라진다.

slice③ spec은 이를 후속(B)로 명시했다: *"대시가 재생 구간에서 정확히 재현되려면 어빌리티/상태이상의 상태까지 스냅/복원해야 한다."*

## 산업 표준 매핑 (리서치 근거 — 2026-07-05)

여러 넷코드 프레임워크를 교차 조사한 결과, **예측/롤백 재생의 표준 구조는 예외 없이 다음 3가지**다. 지금 LOP의 "이동만 재생 + 어빌리티 상태 스냅샷 없음"은 부분 구현이다.

| 프레임워크 | 재생 단위 | 근거 |
|---|---|---|
| **FishNet** (Mirror 계열, 우리와 가장 유사) | 단일 `Replicate` 메서드 = 이동+어빌리티+힘 **한 덩어리** 재실행. **예측에 영향 주는 값(어빌리티 타이머·쿨다운)은 반드시 reconcile 대상** — "replicate 밖에 저장하고 reconcile 안 하면 재생이 깨진다" (명문) | [FishNet Advanced Controls](https://fish-networking.gitbook.io/docs/guides/features/prediction/creating-code/advanced-controls) |
| **Unreal CMC** | `SavedMove` 큐를 통째로 재생. 한 SavedMove가 입력·velocity·가속·위치·루트모션 **전부 캡처** | [Unreal CMC Networked Movement](https://dev.epicgames.com/documentation/unreal-engine/understanding-networked-movement-in-the-character-movement-component-for-unreal-engine) |
| **Unreal GAS** | 어빌리티 예측은 **부분적**(단일 활성화 스코프, 체인 롤백 불가) | [GAS FPredictionKey](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Plugins/GameplayAbilities/FPredictionKey) |
| **DOTS Netcode** | `PredictedSimulationSystemGroup`이 가장 오래된 틱부터 **모든 예측 시스템** 재실행 | [DOTS Prediction](https://docs.unity3d.com/Packages/com.unity.netcode@1.2/manual/prediction.html) |
| **Photon Quantum** | **프레임 전체** 재시뮬. "모든 mutable 상태가 단일 `Frame`에 있어야 결정론 보장" | [Quantum Intro](https://doc.photonengine.com/quantum/current/quantum-intro) |

**표준 3원칙:**
1. **예측 틱 = 하나의 결정론 함수**(이동+어빌리티+타이머). reconcile은 이걸 **통째로** 재실행.
2. **풀 상태 스냅샷**(위치뿐 아니라 어빌리티/상태이상/쿨다운/타이머까지).
3. **velocity 권위는 모터 하나** — 어빌리티는 의도(desired-velocity)를 세팅하고 모터가 읽음. "어빌리티가 velocity 직접 쓰기 + 모터 비켜주기"는 문서화된 안티패턴([reznok](https://reznok.com/how-not-to-use-gas-gmc/)).

이 슬라이스는 **원칙 1·2를 구현**(= 레벨 1)한다. 원칙 3(velocity 권위 통합)은 문서가 이미 잡아둔 Stage④ keystone("velocity 권위 이전")이라 **별개 리팩터(레벨 2)로 분리** — 레벨 1만으로 대시 재생 버그는 해소된다(아래 "왜 레벨 1로 충분한가").

## 범위

**한다 (레벨 1):**
- 내 캐릭 **풀 예측 상태 스냅샷**(위치/회전/velocity + 어빌리티/상태이상/스탯/마나)을 틱별로 기록.
- 하드 복원 시 그 풀 상태를 앵커 틱 값으로 복원.
- 재생 루프를 **예측 틱 전체**(발동 → 이동 → 어빌리티 페이즈 → 상태이상 → 효과 구동 → 물리)로 확장 — 라이브 루프와 같은 순서.
- 재생 순서를 못박는 결정론 EditMode 테스트(라운드트립).

**안 한다 (별개 슬라이스):**
- **레벨 2 — velocity 권위 통합.** "효과가 velocity 직접 쓰기 + 모터 비켜주기"를 "모터가 대시 의도를 읽는 단일 권위"로 바꾸는 것. Stage④ velocity-권위 이전과 함께.
- **라이브 루프의 완전 단일화(FishNet식 single-source).** 라이브는 전 엔티티 2-패스(이동 전부 → 어빌리티 전부)를 유지. 재생은 *로컬 1 엔티티*만 전진하므로 2-패스와 결과 동일(아래 "단일 예측 틱" 참고). 라이브를 per-entity로 접는 건 교차-엔티티 효과 순서에 영향(YAGNI) — 후속.
- **원격 틱별 위치 정합**(클라 lag-comp), **전용 보정 스무싱** — slice③ 범위 그대로 유지.

## 왜 레벨 1로 충분한가 (velocity 권위 분할을 안 고쳐도 대시 재생이 맞는 이유)

현재 분할(대시 효과가 velocity를 쓰고 `MovementSystem`이 비켜줌)을 그대로 둬도, **어빌리티 상태를 복원+재생하면** 대시가 재현된다:

1. 앵커에서 어빌리티 상태 복원 → 재생 중 `ActiveAbility`가 대시 Active.
2. 재생 틱: `HasActiveMotionEffect` = true → `MovementSystem`이 비켜줌(velocity 안 덮음). ✅
3. `AbilitySystem.Tick`이 대시 페이즈 전진.
4. `DriveActiveEntity`가 대시 velocity를 다시 씀. ✅
5. 물리가 대시 velocity로 적분.

즉 레벨 1(풀 스냅샷 + 전체 틱 재생)은 **현재 velocity 분할과 무관하게** 버그를 고친다. 레벨 2는 정합성이 아니라 구조 청결(단일 권위) 문제라 분리 가능.

## 설계

### A. 재생 루프 — 예측 틱 전체 (라이브와 같은 순서)

라이브 로컬 캐릭의 틱 순서(현재 코드):

```
ProcessInput(PlayerInputManager):  발동  — cmd.AbilityId != 0 → AbilityActivator.TryActivate
world.Tick(LOPWorld.Mutation):     이동  — MovementSystem.Tick
                                   페이즈 — AbilitySystem.Tick
                                   상태  — StatusEffectSystem.Tick
LOPRunner.DriveAbilityEffects:     효과  — AbilityEffectExecutor.DriveActiveEntity
LOPRunner.SimulatePhysics:         물리  — physicsSimulator.Simulate
```

재생 한 틱을 **같은 순서**로 재구성한다(현 `Reconciler` 재생 루프 확장):

```
for t = anchorTick+1 .. currentTick-1:
    buffer.Current = inputHistory[t]  (없으면 null)
    발동:  cmd?.AbilityId != 0 → abilityActivator.TryActivate(entityId, cmd.AbilityId, t)
    이동:  movementSystem.Tick(worldEntity, dt)
    페이즈: abilitySystem.Tick(worldEntity, t)
    상태:  statusEffectSystem.Tick(worldEntity, t)
    효과:  abilityEffectExecutor.DriveActiveEntity(worldEntity, entityManager, t)
    물리:  PushMotionToPhysics → physicsSimulator.Simulate(dt) → SyncPhysics
    재기록: 풀 스냅샷[t] 갱신(보정값)
```

- `world.Tick`(전 엔티티 순회)이 아니라 **로컬 엔티티 시스템만 직접 호출** — 원격 엔티티 어빌리티/상태이상 이중 전진 방지.
- **단일 예측 틱 seam(권장):** 위 발동~효과 순서를 `AdvanceLocalEntity(worldEntity, tick, dt)` 한 메서드로 뽑아 재생이 호출. 순서 정의를 한 곳에 모아 드리프트를 막고, 결정론 테스트의 타깃으로 삼는다. (라이브 루프는 2-패스 유지 — 로컬 1 엔티티에선 per-entity ≡ 2-패스라 결과 동일. 완전 단일화는 후속.)

### B. 풀 상태 스냅샷 — 무엇을 캡처하나 (로컬 캐릭만)

| 컴포넌트 | 캡처 대상(가변) | 비고 |
|---|---|---|
| `Transform` | Position, Rotation | 이미 slice② 캡처 |
| `Velocity` | Linear | 이미 slice② 캡처 |
| `Abilities` | `ActiveAbility?`(struct), `Slots[*].CooldownEndTick` | Effects[]·Target는 불변/참조 — 깊은 복사 불필요, 참조 보존. Slots dict는 복사 |
| `StatusEffects` | `Effects` 리스트 | 리스트 복사 |
| `Stats` | `Modifiers` 리스트, `BaseStats`, `UnspentPoints` | 상태이상 적용/만료가 Modifiers/BaseStats를 건드림 |
| `Mana` | Current, Max | 발동 코스트 차감 |

- **왜 Stats/Mana까지:** 상태이상 apply/expire가 `Stats.Modifiers`(+Instant면 BaseStats)를 바꾸고, 발동이 `Mana.Current`를 깎는다. StatusEffects/Abilities만 복원하고 Stats/Mana를 안 하면 재생 상태가 어긋난다(FishNet "예측에 영향 주면 다 reconcile").
- **깊은 복사:** 컬렉션(dict/list)은 값 복사해 이후 라이브 변경이 히스토리를 오염시키지 않게. 크기 작음(어빌리티 3, 이펙트/모디파이어 0~몇 개).

### C. 복원

하드 복원 시 위치/회전/velocity와 **함께** B의 컴포넌트를 앵커 틱 스냅샷 값으로 덮어쓴다. 컴포넌트에 복원용 setter/replace를 추가(anemic 유지 — 상태 쓰기는 시스템/복원 헬퍼가).

### D. 스냅샷 자료구조

- 한 틱 = **풀 예측 상태 레코드 하나**(위치/velocity + 어빌리티/상태/스탯/마나). Quantum "단일 Frame" / FishNet "reconcile data"와 같은 결.
- errorGate는 이 레코드의 `.Position`만 비교(현 로직 유지).
- **배치(Open Question):** 위치/velocity는 GameFramework.Netcode `EntitySnapshot`(생성). 어빌리티/상태이상은 LOP 타입이라 GameFramework가 참조 불가. → 후보: (a) `SnapshotHistory<T>` 제네릭화 + 페이로드는 LOP 정의, (b) LOP-side 전용 풀-스냅샷 링. 권장 (a)(링 재사용 + 도메인 페이로드는 LOP). plan에서 확정.

### E. errorGate — 무변경

앵커 위치 vs `snapshotHistory[T]` 위치 차이 threshold 판정 그대로. 어빌리티/상태이상은 게이트에 넣지 않음(위치만으로 재생 트리거 충분 — 대시가 위치 발산의 원인이므로).

### F. 뷰 — 무변경

지연 렌더링(`LocalEntityInterpolator`) 유지. 하드 보정 점프를 흡수.

## 검증

- **EditMode (핵심 = 결정론 라운드트립):** 대시 발동이 낀 N틱 시퀀스를 `AdvanceLocalEntity`로 진행하며 틱별 풀 스냅샷 기록 → 0틱으로 복원 → 1..N 재생 → 최종 `Abilities/StatusEffects/Stats/Mana/Transform/Velocity`가 라이브와 **완전 일치** 단언. 이 테스트가 재생 순서·스냅샷·복원의 정확성을 못박는다.
- **EditMode (부품):** 풀 스냅샷 깊은복사 격리(스냅 후 라이브 변경이 스냅에 안 새는지), 복원 정확성.
- **플레이:** RTT(50/150/300ms) 주입 + 재조정 창 중 대시 → 위치 pop/러버밴딩 육안 소멸, 대시 재현. 이동/점프/정지 무회귀.

## 영향 파일 (개략 — 세부는 plan)

- **신규:** 풀 예측 상태 스냅샷 레코드 + (제네릭) 링, 복원 헬퍼, `AdvanceLocalEntity` seam, EditMode 결정론 테스트.
- **수정:** `Reconciler`(풀 복원 + 전체 틱 재생), `LOPRunner.RecordLocalSnapshot`(풀 스냅샷 기록), `GameLifetimeScope`(Reconciler에 `AbilityActivator`/`AbilitySystem`/`StatusEffectSystem`/`AbilityEffectExecutor`/`entityManager` 주입), 컴포넌트 복원 setter.
- **무변경:** 서버, World Core 시스템 본문(재사용), velocity 권위 구조(레벨 2), 뷰.

## Out of Scope

- **레벨 2 — velocity 권위 통합**(모터가 대시 의도 읽기, "비켜주기" 제거) — Stage④ velocity-권위 이전과 함께.
- 라이브 루프 완전 단일화(FishNet single-source) — 교차-엔티티 효과 순서 분석 후.
- 원격 틱별 위치 정합(클라 lag-comp), 전용 다중틱 보정 스무싱.

## Open Questions (plan에서 해소)

- 스냅샷 링 배치: `SnapshotHistory<T>` 제네릭화(GameFramework) vs LOP 전용 링.
- `AdvanceLocalEntity` 배치: LOP-Client(Reconciler 옆) vs LOP-Shared(재사용). 라이브 루프가 후일 재사용할지에 따라.
- 컴포넌트 복원 API 형태: 복원 전용 헬퍼 vs 컴포넌트 setter 추가(anemic 경계).
- `ActiveAbility.Target`가 원격 엔티티일 때 참조 보존이 재생 중 안전한지(원격 kinematic이라 정지 — 대개 안전, 확인).

## 진행

- [x] 브레인스토밍 합의 (레벨 1 = 단일 예측 틱 + 풀 스냅샷, 산업 표준 리서치 근거)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
