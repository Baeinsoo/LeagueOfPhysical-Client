# A2.2a — 전투 해소(LOPCombatSystem)를 LOP-Shared 공유 concrete로

**Date:** 2026-07-12
**Branch (제안):** `feature/a2-2a-combat-resolution-shared` (LOP-Shared + Server)
**Related:** [ROADMAP](../../ROADMAP.md) (Next #1 A) · A2.1 spec `2026-07-12-a2-1-server-combat-keyed-rng-design` · [lop-repo-topology](../../lop-repo-topology.md) (시뮬=구체 공유)

## Goal

서버 전용이던 전투 **해소** 로직(`LOPCombatSystem` — 데미지/크리/회피 계산 + 이벤트 발행)을 **LOP-Shared로 옮겨 클·서 공유 concrete**로 만든다. 클라 예측 전투(A2.4)가 서버와 **같은 코드로 같은 결과**를 내기 위한 토대. 이 슬라이스는 **해소만** — 히트 판정(OverlapSphere/부채꼴)은 서버에 남겨 공유 resolver를 호출한다(판정 공유 = A2.2b).

## 배경 / 동기

### A2.2 = 공유화, 이 슬라이스 = 해소(a)

A2(클라 예측 전투) 에픽에서 클라가 히트/데미지/크리를 로컬 예측하려면 **서버와 동일한 전투 코드**를 돌려야 한다. A2.2는 이를 위한 공유화이고, 두 조각으로 나뉜다:
- **A2.2a (이 슬라이스)** — 전투 **해소**(계산+RNG+이벤트). RNG를 소비하는 핵심이라 먼저 공유.
- **A2.2b** — 히트 **판정**(OverlapSphere+부채꼴). 엔진 포트 필요, 예측 직전에.

### 현재 상태 (실측, post-A2.1)

`LOPCombatSystem`(서버 `Assets/Scripts/CombatSystem/`)의 `Attack(LOPEntity attacker, LOPEntity target, long tick, int effectIndex)`:
- `LOPEntity`를 받지만 **`.entityId`만** 읽고 나머진 `entityRegistry.Get(id)` → `World.Entity`로 처리(Ownership/Health/Stats).
- 씨앗을 `matchSeed.Value`(주입된 `MatchSeed` 홀더)로 조립 → `DeterministicRandom` 스트림으로 회피·크리·크리배수.
- `HealthSystem.TakeDamage` + `WorldEventBuffer`에 `DamageDealtEvent`/`DeathEvent` append.
- 엔진 사용: `Mathf.RoundToInt`/`Mathf.Clamp`, `Debug.LogWarning`. **`Vector3`·좌표는 안 씀**(그건 판정=A2.2b).
- `ICombatSystem` 인터페이스로 서버 DI 등록, 호출자는 `DamageEffectHandler` 하나.

핵심 관찰: **해소는 이미 거의 World 기반** — 공유화 블로커는 `LOPEntity` 파라미터와 사이드별 `MatchSeed` 의존뿐. `LOP-Shared`는 이미 `GameFramework.Runtime` 참조 + `UnityEngine` 참조(noEngineReferences=false)라 `Mathf`/`Debug`/`Hashing`/`DeterministicRandom`/`World.*`가 전부 접근 가능.

## 설계

### 1. `LOPCombatSystem` → LOP-Shared 공유 concrete

- 파일을 서버 `Assets/Scripts/CombatSystem/LOPCombatSystem.cs` → LOP-Shared `Runtime/Scripts/Game/LOPCombatSystem.cs`로 이동(`namespace LOP` 유지). **서버 사본 삭제.**
- **`ICombatSystem` 인터페이스 제거** — 시뮬 로직은 "구체 공유"가 컨벤션(사이드별 seam 금지; `lop-repo-topology`·`world-core-connection-architecture` "시뮬 코드 형태"). 양쪽이 동일 concrete를 컴파일·실행 = 결정론 자동. 서버 `ICombatSystem.cs`도 삭제.

### 2. 시그니처 — World.Entity + 씨앗 param

```csharp
public void Attack(GameFramework.World.Entity attacker, GameFramework.World.Entity target,
                   long tick, int effectIndex, ulong matchSeed)
```
- `LOPEntity` → `World.Entity` 직접(호출자가 변환). 내부 `entityRegistry.Get(...)` 룩업 제거 — `attacker`/`target`을 그대로 사용(`attacker.Has<Ownership>()`, `target.Get<Health>()` 등).
- **씨앗은 param** — `MatchSeed` 홀더 의존 제거. 씨앗 조립은 그대로:
  `Hashing.Combine(…(matchSeed, (ulong)tick, Fnv1a64(attacker.Id), Fnv1a64(target.Id), (ulong)effectIndex))` → `new DeterministicRandom(seed)`.
  (`entityId` → `World.Entity.Id`(string). 키 값은 A2.1과 동일하게 유지 — 서버 동작 불변.)

### 3. 의존 (ctor)

`WorldEventBuffer` + `HealthSystem` + `StatsSystem` (전부 `GameFramework.World`, LOP-Shared에서 접근 가능). **`EntityRegistry` 제거**(World.Entity 직접), **`MatchSeed` 제거**(param), IRandom은 A2.1에서 이미 제거됨.

### 4. 서버 `DamageEffectHandler` 배선

히트 판정은 그대로 두고, 해소 호출부를 새 시그니처로:
- attacker = `ctx.Caster`(이미 `World.Entity`).
- target = 히트한 `LOPEntity`를 `World.Entity`로 변환(`entityRegistry.Get(hit.entityId)` 또는 `ctx.EntityManager` 경유 — plan에서 정확한 경로 확정).
- `combatSystem.Attack(ctx.Caster, targetWorldEntity, ctx.CurrentTick, ctx.EffectIndex, matchSeed.Value)`.
- `DamageEffectHandler`에 `MatchSeed` 주입(씨앗 전달용). combatSystem은 concrete `LOPCombatSystem` 주입.

### 5. DI 등록

- 서버 `GameLifetimeScope`: `Register<ICombatSystem, LOPCombatSystem>` → **`Register<LOPCombatSystem>`**(concrete). (LOP-Shared로 옮겼어도 등록은 use-side.)
- 클라: A2.2a에선 **등록/사용 안 함**(클라는 공유 코드를 컴파일만 함, 미사용). 예측이 붙는 A2.4에서 클라도 `Register<LOPCombatSystem>` + 자기 씨앗으로 호출.

### 6. `Mathf`/`Debug` 유지

LOP-Shared가 UnityEngine 참조 가능 + 결정론 차이 없음(Mathf.RoundToInt·Mathf.Clamp = banker's rounding, 같은 플랫폼). **엔진 프리 재작성 안 함**(불필요한 diff·회귀 위험 회피). 좌표/Vector3가 없어 numerics 이슈도 없음.

## 테스트 (LOP-Shared EditMode — 신규 커버리지)

이동으로 전투 해소가 **처음으로 단위테스트 가능**해진다(서버 Assembly-CSharp였음). `Tests/EditMode/LOPCombatSystemTests.cs`:
- World.Entity 2개(Health/Stats/Ownership 세팅) + 고정 씨앗 → `Attack` 호출:
  - **결정론:** 같은 (엔티티 상태, tick, effectIndex, matchSeed) → 같은 크리/회피/데미지(반복 호출 동일).
  - **데미지 적용:** `Health.Current`가 dealtAmount만큼 감소(회피 시 0).
  - **이벤트:** `WorldEventBuffer`에 `DamageDealtEvent`(amount/isCritical/isDodged) append; HP≤0이면 `DeathEvent`도.
  - **effectIndex 분리:** 같은 (tick,attacker,target)라도 effectIndex 다르면 (대체로) 다른 굴림.
  - **비플레이어 가드:** attacker·target 모두 Ownership 없으면 no-op.
- `HealthSystem`/`StatsSystem`/`WorldEventBuffer`는 실제 GameFramework 구체로 조립(순수 C#).

> 서버 배선(`DamageEffectHandler` World.Entity 변환 + 등록)은 Assembly-CSharp라 컴파일 + 서버 실행 검증(전투 회귀 없음 — A2.1과 동일하게 공격→데미지/크리 정상).

## 산업 표준 매핑

- **시뮬 로직 = 구체 공유(인터페이스 seam 금지)** — `lop-repo-topology`·`world-core-connection-architecture`의 "시뮬 코드 형태" 컨벤션. 클·서가 *같은 구체*를 컴파일→결정론 자동. `ICombatSystem` 제거가 이 컨벤션 정합(인터페이스는 사이드가 달라야 하는 I/O 어댑터 전용).
- Quake/Source/Overwatch의 "shared game code": 전투 해소가 shared module, 히트 판정 I/O는 사이드 어댑터(A2.2b 포트) — 정확히 이 분리.

## Out of Scope

- **히트 판정 공유화(OverlapSphere+부채꼴 → 오버랩 쿼리 포트)** — A2.2b.
- **클라 예측 히트 생성**(클라가 `LOPCombatSystem` 등록·호출) — A2.4.
- **예측/확정 reconcile(B) — 예측 실패 보정** — A2.5.
- 전투 수식/밸런스 변경 — 없음(순수 이동+시그니처).

## Open Questions (plan에서 해소)

- `ICombatSystem` 사용처 grep — `DamageEffectHandler` + 서버 DI 외에 있으면 함께 정리.
- `DamageEffectHandler`에서 히트 `LOPEntity` → `World.Entity` 변환 경로(`entityRegistry.Get(entityId)` vs `ctx.EntityManager`) — 기존 코드 스타일에 맞춰 plan에서 확정.
- `World.Entity.Id`가 A2.1의 `LOPEntity.entityId`와 동일 문자열인지 확인(키 값 불변 보장 — 동일해야 서버 동작 무변경).

## 진행

- [x] 브레인스토밍 (A2.2 분해, A2.2a=해소, World.Entity+씨앗 param, ICombatSystem 제거, Mathf/Debug 유지)
- [x] spec self-review (시그니처/의존/ICombatSystem 일관·범위·Id 전제 명시 — 이상 없음)
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan
