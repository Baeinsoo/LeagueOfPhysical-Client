# 통합 World Tick + 클라 시뮬 범위 표준화 — 재생==라이브

**Date:** 2026-07-13
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [Netcode 재설계](../../netcode-redesign.md) · audit `#6`(`docs/superpowers/audit-2026-07-13-structure.md`)
**Origin:** 전반 감사 Tier-2 `#6` — Reconciler 재생이 `LOPWorld.Mutation` 시스템 시퀀스를 수기 복제

## Goal

**"한 틱을 시뮬레이션하는 법"을 한 곳에만 두고, 라이브 경로와 롤백 재생 경로가 둘 다 그 한 곳을 부르게 한다.** 지금은 이 순서가 두 곳(라이브 `LOPWorld.Mutation`+host 루프 / 재생 `Reconciler`)에 손으로 각각 적혀 있어, 컴파일러가 아니라 사람 기억으로 lockstep을 유지한다 → 한쪽에 시스템을 추가하고 다른 쪽을 깜빡하면 조용히 desync(캐릭터 튐).

부수 목표(설계상 자연히 딸림):
- **클라 시뮬 범위를 업계 표준으로 정합** — 클라는 자기가 예측하는 엔티티(내 캐릭)만 시뮬하고, 남/NPC는 스냅샷 보간만. 현재는 클라가 남/NPC까지 시뮬 틱을 돌린다(표준 이탈).
- **AI 넉백 임시배치 부채 정산** — 외력 resolve를 이동 페이즈로 흡수(아래).

## 배경 — 문제와 표준

### 문제: 두 벌 수기 시퀀스 (audit `#6`)

빠른 게임 느낌을 위해 클라는 내 캐릭을 서버보다 먼저 **예측**하고, 서버 스냅샷과 어긋나면 **되감아 다시 실행(롤백 재생)**한다. 한 틱을 실행하는 순서 — **이동 → 어빌리티 → 상태 → 효과구동 → 물리이동** — 이 두 곳에 각각 적혀 있다:

| 단계 | 라이브 (클라 `LOPRunner.UpdateRunner`) | 재생 (`Reconciler.cs:129-149`) |
|---|---|---|
| 입력+발동 | `ProcessInput` (`PlayerInputManager`) | `inputBuffer.Current=cmd` + `TryActivate` |
| 이동→어빌리티→상태 | `world.Tick`→`LOPWorld.Mutation` (전 엔티티, 페이즈 배치) | 3줄 수기 (`movement`→`ability`→`status`) |
| 효과구동 | `DriveAbilityEffects` (별도 host 루프) | `DriveActiveEntity` 수기 |
| 물리이동 | `MoveLocalPlayer` (별도 host 루프) | 수기 (`SyncTransforms`+`Depenetrate`+`kinematicMove`+`PushMotion`) |

두 시퀀스가 어긋나면 재생 결과가 실제와 안 맞아 캐릭터가 튄다(desync). 지금은 일치가 사람 기억에 의존 = "desync 실패 클래스"(메모리 `[[hard-rollback-input-tick-alignment]]`).

### 표준: 클라는 "남"을 시뮬 안 한다 (서버 권위 fast-paced)

| | 내 캐릭 (내가 조종) | 남 / NPC / 서버권위 |
|---|---|---|
| 처리 | **예측(predict)** — 게임 시스템 실행 | **보간(interpolate)** — 시뮬 안 함, 스냅샷 사이 렌더만 |
| 시간선 | 서버보다 앞선 시계 | 서버보다 뒤처진 시계(~과거) |
| 시뮬 틱 대상 | ✅ | ❌ |

- **NPC도 "남"과 같은 바구니** — LOP는 서버 권위(오버워치 모델)라 AI는 서버가 유일 권위(`EnemyBrain`은 서버 전용, **클라에 AI 코드 0개**). 락스텝(RTS)이면 클라가 NPC를 돌리지만 LOP는 그 모델이 아니다.
- **근거:** Gambetta(Part2 예측 / Part3 남 보간) · Overwatch(Tim Ford GDC 2017 — 조종 엔티티만 predicted, 롤백도 그것만) · Source(prediction=로컬 전용, 나머지 `cl_interp` 보간) · 우리 문서 `world-core-connection-architecture.md`("동기화 모델": 내 캐릭=예측+리플레이 / 남=과거 보간, 미래 예측 안 함).

### 현재 코드의 표준 이탈

`CharacterCreator`(클라, `:106-111`)가 **남 캐릭터도 `EntityRegistry`에 등록**하고 `Abilities`/`StatusEffects`/`Stats` 등을 붙인다. `InputBuffer`만 내 캐릭 전용. 그래서 클라 `world.Tick`→`Mutation`이 **남/NPC까지 순회**하며 `AbilitySystem.Tick`/`StatusEffectSystem.Tick`/`DriveAbilityEffects`를 돌린다(이동만 `InputBuffer` 없어 no-op).

단, 이 남 틱은 **사실상 죽은 no-op**이다: 클라는 남에게 `TryActivate`를 하지 않아(내 캐릭만 발동) 남은 `ActiveAbility`가 안 실리고, 뷰도 `Abilities`/`StatusEffects`를 안 읽는다(남 연출은 서버 cue 이벤트로 구동 — 아래).

## 설계

### 핵심 통찰 — `Simulated` 마커 하나로 라이브==재생

"클라가 시뮬하는 것 = 클라가 입력을 주는 것 = 내 캐릭"을 **태그 컴포넌트 `Simulated`**로 명시한다. host가 엔티티 생성 시 정책대로 부착:

- **서버:** 모든 캐릭터에 `Simulated` (서버는 원래 전원 시뮬)
- **클라:** **내 캐릭에만** `Simulated` (남/NPC엔 안 붙임 → 자동 보간 전용)

`world.Tick`은 `EntityRegistry`에서 `Has<Simulated>`인 것만 순회한다. 그러면:

> **클라의 `world.Tick(t, dt)`은 라이브든 재생이든 항상 내 캐릭만 틱한다** → 재생은 "특별한 필터 호출"이 아니라 **과거 틱에 대해 똑같은 `world.Tick`을 다시 부르는 것**뿐. 두 벌 수기 시퀀스가 애초에 하나가 되어 `#6`이 근본적으로 사라진다.

ECS 표준 관용(DOTS `WithAll<Simulate>` 태그 필터). 시뮬 로직은 정책 무지(누가 Simulated인지는 host가 결정) — Engine/Simulation 분리와 정합.

### 통합 `LOPWorld.Tick` — 5페이즈, Simulated 순회

페이즈 배치를 유지한다(서버 cross-entity 판정 정확성: "전원 이동 후 전원 효과구동"이라야 A가 B의 이동 후 위치를 때림):

```
Tick(tick, dt):
    foreach e in Has<Simulated>: 이동(e)          // MovementSystem.Tick (외력 resolve 포함)
    foreach e in Has<Simulated>: 어빌리티(e); 상태(e)
    foreach e in Has<Simulated>: 효과구동(e)        // AbilityEffectExecutor.DriveActiveEntity
    foreach e in Has<Simulated>: 물리이동(e)        // 물리 브리지 포트 + KinematicMoveSystem
```

- 클라: Simulated={내 캐릭} → 페이즈가 사실상 한 엔티티지만 순서 보존.
- 서버: Simulated=전 캐릭 → 페이즈 배리어 보존(cross-entity 정확).
- `DriveAbilityEffects`(효과구동)·`MoveCharacters`/`MoveLocalPlayer`(물리)를 **host 루프에서 `LOPWorld.Tick` 안으로 흡수**. 효과구동(`AbilityEffectExecutor`)은 이미 순수 공유 코드라 그대로 흡수 가능.

### 물리 브리지 포트 — 키네마틱을 사이드 무관하게

키네마틱 단계만 Unity 결합이다(`KinematicMoveSystem`은 이미 `ICollisionQuery` 포트로 공유). 남은 host 결합 3개를 포트 뒤로 뺀다:

```
interface IMotionBridge {            // 구체는 클·서 각자 (LOPEntity/PhysicsComponent 래핑)
    void SyncTransforms();           // 페이즈 시작 1회 (Unity Physics.SyncTransforms)
    void Depenetrate(string entityId);  // per-entity (PhysicsComponent.ComputePenetration)
    void PushMotion(string entityId);   // per-entity (World 위치 → rb.position)
}
```

`LOPWorld.Tick`의 물리 페이즈: `bridge.SyncTransforms()` → `foreach Simulated: bridge.Depenetrate(id); kinematicMoveSystem.Tick(e); bridge.PushMotion(id)`. `ICollisionQuery`/`IOverlapQuery`와 같은 포트 관용(임의 발명 아님). 문서 4e 목표("물리 페이즈를 `LOPWorld.Tick`으로 흡수")와 정확히 일치.

### 외력(넉백) 통합 — 부채 정산

`Simulated` 마커가 `world.Tick`을 소유 엔티티로 게이트하면, **클라는 원격을 절대 안 틱한다** → 넉백 부채를 막던 제약("공유 `MovementSystem.Tick`의 외력 resolve를 게이트 밖으로 빼면 클라 원격 velocity를 건드림")이 사라진다. 이동 페이즈에서 **Simulated 전원의 외력을 `MovementSystem`이 resolve**하게 한다 — 즉 `MovementSystem.Tick`을 재구성해 **입력 없는 Simulated 엔티티(서버 AI)도 외력 resolve**(base=현재 수평 velocity). 그러면 서버 `MoveCharacters`의 임시 AI 분기(`MotionContributionSystem.ApplyToVelocity`, 2026-07-13 도입)를 **정식 흡수해 제거**. (이 재구성이 이전엔 클라 원격 velocity를 건드릴까 막혀 있었으나, `Simulated` 게이트로 클라가 원격을 안 틱하므로 이제 안전.) ROADMAP 파킹 "외력 처리 공통 루프 이전" 정산.

### 재생 = 라이브 `world.Tick`

`Reconciler`는 수기 5줄 시퀀스를 버리고:

```
상태 복원(스냅 + PredictedAbilityState + MotionContributions)
foreach 과거틱 t in (anchor+1 .. current-1):
    inputBuffer.Current = inputHistory[t]      // 입력만 재주입
    if cmd.AbilityId != 0: abilityActivator.TryActivate(...)   // 발동 재현(라이브와 같은 통로)
    world.Tick(t, dt)                          // ← 라이브와 동일한 단일 진입점
    (히스토리 갱신)
```

`worldEventBuffer.Suppress()`(재생 cue 억제)는 유지. 입력 재주입·발동 재현·상태 히스토리 갱신은 재생 고유 래퍼로 남지만, **틱 실행 자체는 라이브와 같은 `world.Tick`** — 시퀀스 중복 소멸.

### 남/NPC 연출 — 변경 없음

남 어빌리티 연출은 이미 **서버 cue 이벤트**로 구동된다: 서버 `AbilityActivatedToC` → `GameAbilityMessageHandler`(내 캐릭이면 스킵, 남이면 `AbilityActivatedEvent` cue) → `LOPEntityView.OnAbilityActivated`가 일회성 애니 재생. 위치는 `RemoteEntityInterpolator`(스냅 보간). 클라 sim 틱과 무관하므로 **남을 `Simulated`에서 빼도 연출 무회귀** — 죽은 남 틱만 정리된다.

## 분해 — Sub-slice A / B / C

> **⚠️ 보정 (2026-07-13, plan 착수 시):** 당초 A(서버)/B(클라) 2조각으로 잡았으나, **`LOPWorld.Tick`은 클·서 공유 코드**라 "서버만 바꾸고 클라 무변경"이 성립하지 않는다(world.Tick 구조 변경은 양쪽 동시). 경계를 *사이드*가 아니라 *동작 보존 여부*로 다시 긋는다. `RemoteEntityInterpolator`는 위치만 쓰고 velocity는 미기록(스냅 velocity를 Hermite 탄젠트로만 사용) + 클라 원격 `MotionContributions`는 항상 비어 있음 → 외력 fold가 클라 원격엔 "같은 값 재기록"이라 무해(A에서 안전).

### Sub-slice A — 기반 (양쪽 동작 보존)
`Simulated` 마커 도입 + **양쪽 `CharacterCreator`가 현재 틱하던 셋을 그대로 마킹**(서버=전 캐릭 / 클라=전 캐릭 = 현행 유지). `LOPWorld.Tick`(Mutation)이 `Has<Simulated>` 순회. `AbilityEffectExecutor`(driveeffects)를 `world.Tick`에 흡수 + 양쪽 `LOPRunner`가 `DriveAbilityEffects()` 호출 제거. `MovementSystem.Tick`을 재구성해 입력 없는 Simulated 엔티티(서버 AI)도 외력 resolve → 서버 `MoveCharacters`의 임시 AI 분기(`ApplyToVelocity`) 제거(넉백 부채 정산). **클·서 양쪽 동작 무변경**(컴파일 + 서버/클라 플레이 무회귀 + Shared EditMode). 키네마틱은 아직 host `MoveCharacters`/`MoveLocalPlayer`에 남김.

### Sub-slice B — 클라 scope 축소 + 물리 흡수 (양쪽 동작 보존)
> **⚠️ 보정 (2026-07-13, B 착수 시):** 키네마틱을 `world.Tick`(Simulated 순회)에 흡수하면 클라가 **전 캐릭 Simulated**(A)라 원격까지 키네마틱 이동시켜 스냅 팔로워를 깬다. 그래서 **클라 scope 축소(내 캐릭만)를 C에서 B로 앞당긴다.** 이는 무변경 — 클라 원격 틱은 전부 no-op(`AbilitySystem.Tick`은 `ActiveAbility==null`이면 즉시 return, 원격은 `TryActivate` 안 받음 / `MovementSystem`은 빈 contributions로 같은 값 재기록 / 상태·효과 no-op).

- **클라 `CharacterCreator`가 마커를 내 캐릭(`isUserEntity`)에만** 부착(남/NPC 마킹 제거 → 자동 보간 전용, 죽은 남 틱 제거).
- **`IMotionBridge` 포트**(`SyncTransforms`/`Depenetrate(id)`/`PushMotion(id)`) 신설 + 클·서 구체(`LOPMotionBridge`, `LOPEntity`/`PhysicsComponent` 래핑). 인터페이스는 GameFramework(다른 포트와 동일 레이어).
- **키네마틱을 `world.Tick`에 흡수**: `LOPWorld.Tick` 물리 페이즈 = `bridge.SyncTransforms()` → `foreach Simulated: bridge.Depenetrate(id); kinematicMoveSystem.Tick(e); bridge.PushMotion(id)`. `LOPWorld` ctor에 `KinematicMoveSystem`+`IMotionBridge` 추가.
- **양쪽 `LOPRunner`가 `MoveCharacters`/`MoveLocalPlayer` 제거.** **5페이즈 단일 진입점 완성.**
- 검증: 양쪽 동작 무변경(서버=전 캐릭 키네마틱 / 클라=내 캐릭만, 원격 보간 유지). Shared EditMode + 플레이(이동/점프/대시/넉백/원격 보간).

### Sub-slice C — `Reconciler` = `world.Tick` 재생 (`#6` 종결)
`Reconciler`를 재작성 — 수기 5줄 시퀀스(movement→ability→status→driveeffects→kinematic) 삭제, `상태 복원 → foreach 과거틱: 입력 재주입 + world.Tick`으로. 재생 중엔 클라 Simulated=내 캐릭만이라 `world.Tick`이 자연히 내 캐릭만 재생. **여기서 `#6`이 실제로 닫힘**(라이브==재생, 두 벌 시퀀스 소멸). 검증: 공중 점프/대시/넉백 롤백 재생 무회귀(육안 + Recon HUD).

## Out of Scope

- **완전 결정론 RNG / 확정 게이트 본격 / 틱 스탬프 롤백 폐기** — Stage④의 다른 트랙. 이 슬라이스는 "통합 틱 + 클라 scope"만.
- **남/NPC 상태이상 아이콘 등 새 연출** — 현재 없음(뷰가 안 읽음). 필요해질 때 별도.
- **`IWorld.Tick` 시그니처를 GameFramework 추상까지 손대기** — `Simulated` 마커/`IMotionBridge`의 코어(GameFramework.World) vs LOP 배치는 plan에서 확정.
- **아이템 등 비-캐릭터 엔티티** — `Simulated` 안 붙음(현행 no-op 유지).

## Open Questions (plan에서 해소)

- `Simulated` 마커를 `GameFramework.World` 코어 태그로 둘지 LOP(`namespace LOP`)에 둘지 — "로컬 시뮬이 이 엔티티를 소유"는 코어 개념이나, 부착 정책은 LOP host.
- `IMotionBridge` 포트를 신설할지 기존 `IPhysicsSimulator` 확장할지 — 책임(다이나믹 물체 적분 vs 키네마틱 sync) 구분 검토.
- 통합 `LOPWorld.Tick`이 물리 브리지를 생성자 주입으로 받을지(포트 하나 추가) — `IEventSink`/`ICollisionQuery` 주입 관용 따름.
- Sub-slice A에서 서버 `ProcessDeaths`(죽음 cascade)의 페이즈 위치 — 현재 `world.Tick` 뒤. 통합 후에도 egress 전 유지 확인.
- 클라 `SimulatePhysics`(다이나믹 물체 `physicsSimulator.Simulate`)와 키네마틱 페이즈 순서 — 현행 유지.

## 산업 표준 매핑

- **클라 예측 범위 = 조종 엔티티**: Overwatch(predicted entities), Source(`CPrediction` 로컬 전용), Gambetta(예측/보간 분리). "남/NPC는 보간"은 서버 권위 fast-paced의 정석.
- **태그 컴포넌트로 시스템 대상 필터**: Unity DOTS `WithAll<Tag>`, Photon Quantum 필터. `Simulated`는 그 관용.
- **I/O 포트(물리 브리지)**: `ICollisionQuery`/`IOverlapQuery`/`IPhysicsSimulator`(이 repo) = 시뮬이 엔진을 사이드 무관 포트로 호출. `IMotionBridge`는 그 연장.
- **단일 결정론 틱 진입점**: Photon Quantum `QuantumGame` 단일 시뮬 진입 + 예측/롤백이 같은 코드 재실행. 우리 `world.Tick` 재생==라이브가 그 정합.

## 진행

- [x] 브레인스토밍 합의 (표준 = 클라 예측 엔티티만 시뮬 / `Simulated` 마커 / 물리 브리지 포트 A안 / 5페이즈 통합 Tick / 재생=라이브 / 남 연출 무변경 / A·B 분해)
- [x] 이 spec 작성
- [x] spec self-review (placeholder/일관성/범위/모호성 — 서버 페이즈 순서 보존 확인, 외력 재구성 명시)
- [x] 사용자 spec 리뷰 (승인)
- [x] `writing-plans`로 Sub-slice A 구현 plan 작성 → `docs/superpowers/plans/2026-07-13-unified-world-tick-sub-slice-a.md` (분해를 A/B/C로 보정 — LOPWorld 공유라 A는 양쪽 동작 보존)
- [x] **Sub-slice A 구현·머지 (2026-07-13, 4 repo main)** — `Simulated` 마커 + `LOPWorld.Mutation` `Has<Simulated>` 순회 + driveeffects·외력 `world.Tick` 흡수. Shared EditMode 110/110, 플레이 무회귀. **넉백 부채 정산**(외력이 공통 이동 페이즈로).
- [x] **Sub-slice B 구현·머지 (2026-07-13, 4 repo main)** — 클라 `Simulated` 내 캐릭만으로 축소 + `IMotionBridge` 포트 + 키네마틱을 `LOPWorld.Tick` 물리 페이즈로 흡수 + host `MoveCharacters`/`MoveLocalPlayer` 제거. **`world.Tick` 5페이즈 단일 진입점 완성.** Shared EditMode 111/111, 플레이 무회귀.
- [x] **후속 리팩터: 모션 브릿지 공유화 (2026-07-13, 4 repo main)** — B는 per-side `LOPMotionBridge` 2개(→클라 `IEntityManager` DI gotcha)로 착수했으나, 사용자 지적("구조·개념 공통이면 shared로")에 따라 **공유 concrete 1개 `MotionBridge` + 공유 `PhysicsBody`(rb/콜라이더 핸들) 컴포넌트**로 통합. 포트(`IMotionBridge`, `GameFramework.World`로 이동—Entity 받으니 `IEventSink`와 동일 어셈블리) 유지 → 경계·테스트 seam 유지, `UnityCollisionQuery↔ICollisionQuery`와 동형. 중복 2개 + DI gotcha 동시 해소. 남음: **Sub-slice C**(Reconciler = world.Tick 재생 + `#6` 종결).
