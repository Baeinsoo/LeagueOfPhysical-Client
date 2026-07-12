# A2.1 — 서버 전투 키 기반 결정론 RNG + 매치시드

**Date:** 2026-07-12
**Branch (제안):** `feature/a2-1-server-combat-keyed-rng` (GameFramework + LOP-Shared + Server + Client)
**Related:** [ROADMAP](../../ROADMAP.md) (Next #1 A) · A1 spec `2026-07-10-deterministic-random-primitive-design` · 메모리 `[[deterministic-rng-counter-based]]`

## Goal

서버 전투(`LOPCombatSystem`)가 크리/회피를 **비결정 `IRandom` 대신 A1 `DeterministicRandom`을 키에서 씨앗 삼아** 굴리게 한다. 키 = `hash(matchSeed, tick, attackerId, targetId, effectIndex)`. 매치시드는 매치당 서버가 1회 생성해 **`GameInfo`로 클라에 동기**(클라는 보관만, A2.4까지 미사용). 결과: 같은 매치시드면 전투 굴림이 **재현 가능**해지고, 이후 클라 예측(A2.4)이 같은 값을 재현할 토대가 선다.

이것은 A2(클라 예측 전투) 에픽의 **슬라이스 1**. 전투 공유화·클라 예측 생성·reconcile은 A2.2~A2.5.

## 배경 / 동기

### 현재 전투 흐름 (실측)

- 서버 `DamageEffectHandler.OnActiveEnter`가 `Physics.OverlapSphere`+부채꼴로 대상을 찾아 `LOPCombatSystem.Attack(attacker, target)` 호출.
- `Attack`이 주입된 싱글톤 `IRandom rng`(`UnityRandom`, **비결정·전역**)로 회피·크리·크리배수 **3회** 굴림 → HP mutate + `DamageDealtEvent`/`DeathEvent` append.
- 클라는 결과를 와이어로 받음(데미지=서버권위, HP=스냅샷). 클라 시뮬은 RNG 미소비.

### 왜 키 기반인가 (박제)

예측-보정에서 클·서는 같은 글로벌 스트림을 안 돌려 **호출 횟수가 다름** → 단일 시드 스트림은 desync. 해법 = **counter-based/키 기반**: 굴림을 안정된 키에서 유도해 독립 재현. 상세 `[[deterministic-rng-counter-based]]`. 한 해소(한 공격) 안의 3굴림은 **이벤트 씨앗 하나 → 스트림 순서 소비**(A1 방식 1)로 처리 — 클·서 같은 공유 코드가 같은 순서를 보장(A2.2 이후; A2.1은 서버 단독이라 자명).

### 왜 effectIndex가 키에 필요한가

한 엔티티는 **Active 어빌리티가 단수**라, 같은 `(tick, attacker, target)`의 서로 다른 두 해소는 **반드시 같은 어빌리티의 서로 다른 데미지 효과**에서 나온다(다단 히트 어빌리티). 이를 구분하지 않으면 두 효과가 **같은 씨앗 → 같은 크리 굴림**을 공유(상관됨). **effectIndex(효과 리스트 내 위치)** 가 각 효과를 독립 sub-stream으로 분리 — counter-based RNG의 "채널" 개념(웹 검증 2026-07-12: Philox 키+카운터 독립 시퀀스 / sub-stream이 상관 방지). abilityId는 단수 Active라 상관 해소에 무용(둘 다 같은 어빌리티) — 넣지 않는다. 매치 간 다양성은 matchSeed가 담당.

## 설계 (저장소별)

### 1. GameFramework — 해시 헬퍼

`Runtime/Scripts/Game/Hashing.cs`, `namespace GameFramework` (엔진 비의존). 씨앗을 부품에서 유도하는 generic 원시.

```csharp
namespace GameFramework
{
    /// <summary>결정론 씨앗 조립용 순수 해시 유틸(FNV-1a 64 + 폴딩 combine). 플랫폼 독립 정수 연산.</summary>
    public static class Hashing
    {
        private const ulong Fnv64Offset = 14695981039346656037UL;
        private const ulong Fnv64Prime  = 1099511628211UL;

        /// <summary>문자열의 FNV-1a 64비트 해시(엔티티 id 등 문자열 키 → ulong).</summary>
        public static ulong Fnv1a64(string s)
        {
            ulong hash = Fnv64Offset;
            if (s != null)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    hash ^= s[i];
                    hash *= Fnv64Prime;
                }
            }
            return hash;
        }

        /// <summary>누적 해시에 값 하나를 접어 넣는다(씨앗 부품 결합).</summary>
        public static ulong Combine(ulong hash, ulong value)
        {
            hash ^= value;
            hash *= Fnv64Prime;
            return hash;
        }
    }
}
```

### 2. LOP-Shared — proto 필드 + effectIndex 컨텍스트

**(a) proto:** `Protos/GameInfo.proto`에 필드 추가:
```proto
message GameInfo {
  int64 tick = 1;
  double interval = 2;
  double elapsed_time = 3;
  repeated EntityCreationData entity_creation_datas = 4;
  uint64 match_seed = 5;
}
```
→ 재생성. **⚠️ MessageId 시프트 주의**(`[[proto-message-id-regen-gotcha]]`): 기존 message에 필드만 추가라 MessageId 불변이어야 함 — 재생성 후 `MessageIds.cs` diff로 **ID 변화 없음 확인**. 재생성 스크립트가 MessageIds를 지웠다 다시 만들면 순서 유지되는지 검증.

**(b) `AbilityEffectContext`에 `EffectIndex` 추가** (`Runtime/Scripts/Game/Ability/AbilityEffectContext.cs`):
```csharp
public readonly struct AbilityEffectContext
{
    public readonly Entity Caster;
    public readonly Entity Target;
    public readonly long CurrentTick;
    public readonly IEntityManager EntityManager;
    public readonly int EffectIndex;   // 효과 리스트 내 위치 — 결정론 RNG sub-stream 구분용

    public AbilityEffectContext(Entity caster, Entity target, long currentTick,
                                IEntityManager entityManager, int effectIndex)
    {
        Caster = caster;
        Target = target;
        CurrentTick = currentTick;
        EntityManager = entityManager;
        EffectIndex = effectIndex;
    }
}
```

**(c) `AbilityEffectExecutor` — 효과별 인덱스로 ctx 구성** (`OnActiveEnter`/`OnActiveTick`): `foreach` → 인덱스 `for`로 바꾸고, 효과마다 그 인덱스로 ctx를 만들어 핸들러에 전달.
```csharp
public void OnActiveEnter(AbilityEffectContext ctx, AbilityEffect[] effects)
{
    if (effects == null) return;
    for (int i = 0; i < effects.Length; i++)
    {
        if (_handlers.TryGetValue(effects[i].GetType(), out var handler))
        {
            var effectCtx = new AbilityEffectContext(
                ctx.Caster, ctx.Target, ctx.CurrentTick, ctx.EntityManager, i);
            handler.OnActiveEnter(effectCtx, effects[i]);
        }
    }
}
```
(`OnActiveTick` 동일 패턴. `DriveActiveEntity`가 만드는 base ctx는 `effectIndex: 0`으로 생성 — 루프가 실제 인덱스로 덮음.) 기존 핸들러(StatusEffectApply/Motion 등)는 `EffectIndex`를 안 읽으면 무영향.

### 3. Server — 매치시드 홀더 + combat 키화

**(a) `MatchSeed` 홀더** (`Assets/Scripts/.../MatchSeed.cs`, 싱글톤): 매치당 1회 랜덤 ulong 생성 후 보관. 생성 시 **로그**(재현용). 값 자체는 비결정 생성 무방(*값*이 매치마다 다르기만 하면 됨 — 결정론은 그 값의 *사용*에서).
```csharp
public class MatchSeed
{
    public ulong Value { get; }
    public MatchSeed()
    {
        Value = /* 매치당 1회 랜덤 ulong (예: Guid 기반). */;
        Debug.Log($"[MatchSeed] {Value}");   // 재현용 로그
    }
}
```
서버 `GameLifetimeScope`에 `Register<MatchSeed>(Singleton)`.

**(b) `GameInfoMessageHandler`**: `GameInfo`에 `MatchSeed = matchSeed.Value` 기록(주입). 모든 `GameInfoToC`가 같은 값을 실음.

**(c) `LOPCombatSystem`**: `[Inject] IRandom rng` **제거**, `[Inject] MatchSeed matchSeed` 추가. `Attack` 시그니처 → `Attack(LOPEntity attacker, LOPEntity target, long tick, int effectIndex)`. 굴림 전 씨앗 조립 + per-event 스트림 생성:
```csharp
ulong seed = Hashing.Combine(
    Hashing.Combine(
        Hashing.Combine(
            Hashing.Combine(matchSeed.Value, (ulong)tick),
            Hashing.Fnv1a64(attacker.entityId)),
        Hashing.Fnv1a64(target.entityId)),
    (ulong)effectIndex);
var rng = new GameFramework.DeterministicRandom(seed);
// 이하 IsDodge/IsCritical/크리배수가 이 rng를 순서대로 사용(회피→크리→배수).
```
`IsDodge`/`IsCritical`도 `rng`를 파라미터로 받도록(또는 `Attack` 내부 지역 rng 공유) 조정 — 3굴림이 한 스트림.

**(d) `DamageEffectHandler`**: `Attack(attacker, target, ctx.CurrentTick, ctx.EffectIndex)` 로 tick·effectIndex 전달.

### 4. Client — 매치시드 보관 (A2.3 완료)

클라 `MatchSeed` 홀더(싱글톤) + `GameInfoMessageHandler.OnGameInfoToC`에서 `gameInfoToC.GameInfo.MatchSeed`를 저장. **A2.1에선 저장만**(예측 소비는 A2.4).

## 스코프

- **combat만.** `GameRuleSystem`은 `IRandom`(UnityRandom) **유지** — 클 예측 대상 아님. → 서버 `IRandom` 등록 그대로.

## 테스트

- **GameFramework `Hashing` — EditMode TDD** (`Tests/Runtime/HashingTests.cs`): `Fnv1a64` 결정론(같은 문자열→같은 값)·다른 문자열 구분·null 안전; `Combine` 결정론·순서 의존(a,b ≠ b,a).
- **LOP-Shared `AbilityEffectExecutor` — EditMode** (기존 `AbilityEffectExecutorTests` 확장): 효과 여러 개일 때 핸들러가 **각기 다른 `EffectIndex`(0,1,2…)** 로 호출되는지.
- **서버 combat·홀더·proto = 컴파일 + 서버 실행 검증**(Assembly-CSharp라 단위테스트 어려움): 전투 정상, `[MatchSeed]` 로그 확인, 같은 시드 주입 시 크리/회피 재현. 클라: GameInfo 수신 후 매치시드 보관 확인(로그).

## 산업 표준 매핑

- **counter-based RNG를 (키+카운터)로 인덱싱** = 표준(Philox/counter-based; "각 난수는 키와 카운터의 결정론적 함수", 엔티티별 키·draw별 카운터로 독립성). effectIndex = **sub-stream/채널**(상관 방지). 웹 검증 2026-07-12.
- **예측-보정이라 키 기반**(락스텝 "같은 시드+순회 순서"는 클·서 동일 글로벌 시뮬 전제라 부적합). `[[deterministic-rng-counter-based]]`.
- FNV-1a 64 = 문자열→정수 결정론 해시 표준. SplitMix64(A1) = splittable 키 finalizer.

## Out of Scope (A2.2~A2.5)

- **전투 해소 공유화**(`LOPCombatSystem`→LOP-Shared) + **오버랩 쿼리 포트**(`Physics.OverlapSphere` 추상화) — A2.2.
- **클라 예측 히트 생성**(클 `DamageEffectHandler` 등록) — A2.4.
- **reconcile / 확인·취소(B)** — A2.5.
- **키에 abilityId/다중-소스 구분** — 단수 Active라 불필요. 향후 DoT 등 *다른 소스*가 같은 (tick,attacker,target)로 데미지를 내면 그때 소스 구분자 추가.
- **무편향 int 범위**(A1 Out of Scope 계승).

## Open Questions

- `IsDodge`/`IsCritical`를 `Attack` 지역 `DeterministicRandom`을 공유하도록 리팩터하는 형태(파라미터 전달 vs 내부화) — 구현 plan에서 확정(굴림 순서 = 회피→크리→배수 고정).
- 서버 매치시드 생성 소스(Guid 해시 등) 구체 — plan에서. (값의 비결정 생성은 무방, 로깅 필수.)

## 진행

- [x] 브레인스토밍 (A2 분해, A2.1 범위, matchseed=GameInfo, 키+effectIndex[abilityId 배제 근거], 업계 표준 검증)
- [x] spec self-review (키/Attack 시그니처/effectIndex 일관·스코프·모호성 — 매치시드 생성 소스만 plan 이월, 그 외 이상 없음)
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan
