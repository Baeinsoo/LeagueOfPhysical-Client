# 결정론 난수 원시 도구 — `DeterministicRandom` (A1)

**Date:** 2026-07-10
**Branch (제안):** `feature/deterministic-random-primitive` (GameFramework)
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (I/O 어댑터 — `IRandom`) · [ROADMAP](../../ROADMAP.md) (Next #1 A — 클라 예측 전투의 첫 슬라이스) · 메모리 `[[deterministic-rng-counter-based]]`

## Goal

씨앗(seed) 하나로 **재현 가능한 결정론적 난수 스트림**을 내는 순수 struct `DeterministicRandom`을 GameFramework에 추가한다. 클라 예측 전투(A2)에서 클·서가 크리/회피를 **같은 값으로** 굴리기 위한 토대. **이 슬라이스는 원시 도구 + 결정론 검증까지** — 씨앗 유도·서버 배선·`IRandom` 교체는 하지 않는다(A2).

## 배경 / 동기

### 왜 필요한가 (A의 첫 조각)

Stage④ "클라 예측 전투 생성(A)"에서, 내 히트의 크리/회피를 클라가 로컬 예측하고 서버 확정과 일치시키려면 **양쪽이 같은 난수를 굴려야** 한다. 현재 `IRandom` 구현 `UnityRandom`은 `UnityEngine.Random`(전역 상태, 비결정)이라 재현 불가.

### 왜 "시드 스트림"이 아니라 이벤트별 씨앗인가 (박제)

일반 PRNG는 스트림이라 **같은 씨앗이라도 호출 횟수·순서가 다르면 값이 어긋난다**. 예측-보정에서 클(내 캐릭만)·서(전 플레이어)는 **같은 글로벌 스트림을 안 돌려** 호출 횟수가 근본적으로 다르다 → 단일 공유 스트림 불가. 해법 = **counter-based/키 기반**: 굴림을 안정된 키에서 유도해 독립 재현. 상세·웹 근거는 `[[deterministic-rng-counter-based]]`.

**단, 한 이벤트(한 공격 해소) 안에서는** 클·서가 *같은 공유 코드*를 돌려 굴림 순서가 동일하므로, **이벤트별로 씨앗을 하나 잡고 그 스트림을 순서대로 소비**하는 게 안전하고 자연스럽다(결정론 시뮬 관례). `DeterministicRandom`은 바로 그 "이벤트별 씨앗 스트림"이다. (이벤트 씨앗을 무엇으로 유도할지 = 키 정책은 A2 소관.)

### 현재 상태 (실측)

- `GameFramework/Runtime/Scripts/Game/IRandom.cs` — `Range(float,float)`(max 포함 의미론), `Range(int,int)`(max 제외).
- `.../UnityRandom.cs` — `UnityEngine.Random` 어댑터(비결정, 전역).
- 소비자: 서버 `LOPCombatSystem`(한 공격에 회피·크리·크리배수 **3회** 굴림), `GameRuleSystem`. 클라는 `IRandom` 미등록·미소비.
- 테스트: `GameFramework/Tests/Runtime/baegames.GameFramework.Runtime.Tests.asmdef`(NUnit, `namespace GameFramework.Tests`), `Tests/World/…`도 별도 존재.

## 설계

### 컴포넌트

`GameFramework/Runtime/Scripts/Game/DeterministicRandom.cs`, `namespace GameFramework` (IRandom 짝, 폴더 일관). 순수 정수 연산 — 엔진 비의존.

```csharp
namespace GameFramework
{
    /// <summary>
    /// 씨앗 하나로 재현 가능한 결정론적 난수 스트림(SplitMix64). value 타입 — 이벤트별로
    /// 새로 만들어 순서대로 소비한다(예: 한 공격 해소의 회피→크리→크리배수).
    /// 씨앗 유도(매치시드+tick+entity 등 키 해시)는 소비자(예측 전투) 책임 — 여기선 ulong 씨앗만 받는다.
    /// 비결정 전역 RNG가 필요한 곳은 UnityRandom(IRandom) 유지; 이건 결정론 재현용.
    /// </summary>
    public struct DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed) { _state = seed; }

        /// <summary>다음 64비트 난수(SplitMix64). 스트림을 한 칸 감는다.</summary>
        public ulong NextUInt64()
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>[0,1) float. 정수 상위 24비트를 2^24로 나눠 부동소수 누적 없이 결정론 유지.</summary>
        public float NextFloat01()
        {
            return (NextUInt64() >> 40) * (1.0f / 16777216.0f); // 1/2^24
        }

        /// <summary>[minInclusive, maxExclusive) float. min==max면 min.</summary>
        public float Range(float minInclusive, float maxExclusive)
        {
            return minInclusive + (maxExclusive - minInclusive) * NextFloat01();
        }

        /// <summary>[minInclusive, maxExclusive) int. maxExclusive<=minInclusive면 minInclusive(no-op).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            ulong span = (ulong)((long)maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt64() % span);
        }
    }
}
```

### 의미론 — 기존 `IRandom`과의 정합

- `Range(int,int)` = **max 제외**, `IRandom`과 동일.
- `Range(float,float)` = **[min,max)** (반개구간). `IRandom` 주석은 float를 "max 포함"이라 했지만, 실제 전투 사용처가 전부 `roll < 확률`(회피/크리 판정) + 배수 곱(`Range(1.25f,1.75f)`)이라 **반개구간으로 결과 동일**. A2에서 `LOPCombatSystem`이 `DeterministicRandom`으로 갈아탈 때 호출부 변경은 인스턴스 획득뿐(굴림 코드 그대로). 이 미세한 의미 차이는 의도적(연속 분포에서 max 포함은 무의미).

### 알고리즘 — SplitMix64 (직접 소유)

- 잘 알려진 표준 finalizer, 통계 품질 양호, **정수 기반**이라 플랫폼·사이드 결정론 안전. float도 정수 비트에서 변환(누적 오차 0).
- Unity.Mathematics.`Random`(seeded xorshift)이 유사하나, **넷코드 결정론의 핵심**이라 알고리즘을 코드베이스가 소유(~10줄)해 패키지 impl 변동/의존에 안 묶인다. (산업 표준 매핑 참고.)

### 이 슬라이스가 **안** 하는 것 (전부 A2)

- **씨앗 유도 / 키 정책** — `hash(매치시드, tick, entityId, 굴림컨텍스트)`로 이벤트 씨앗 만들기. 무엇을 키로 삼는지는 예측 전투가 정의.
- **`IRandom` 구현/교체** — `DeterministicRandom`은 이벤트별 value 타입이라 DI 싱글톤 `IRandom`과 성격이 다르다. combat이 씨앗에서 인스턴스를 어떻게 얻을지(팩토리/서비스)도 A2.
- **서버 `LOPCombatSystem` 배선 · 매치시드 플럼빙 · 클라 예측 전투** — 전부 A2.
- **문자열 entityId → ulong 해시 등 seed-building 헬퍼** — A2(키 정책과 함께).

## 테스트 (EditMode, `GameFramework.Tests`, `Tests/Runtime/`)

순수 struct라 Unity 없이 단위 테스트.

- **재현성:** 같은 씨앗 두 인스턴스가 `NextUInt64()`(그리고 `NextFloat01`) **N개 시퀀스 완전 일치**.
- **씨앗 구분:** 다른 씨앗은 (거의 확실히) 다른 첫 출력.
- **알고리즘 고정(회귀 잠금):** 씨앗 고정 시 출력이 **SplitMix64 공개 레퍼런스 벡터와 일치**. 나중 리팩터가 스트림을 몰래 바꾸면(=넷코드 결정론 파괴) 이 테스트가 잡는다. *정확한 골든 hex는 구현 plan에서 레퍼런스로 핀.*
- **`NextFloat01`** 다수 draw가 전부 [0,1).
- **`Range(float,float)`**: 결과 ∈ [min,max); `min==max`면 항상 min.
- **`Range(int,int)`**: 결과 ∈ [min,max); `maxExclusive<=minInclusive`면 min(no-op); 다수 draw가 양 끝(min, max-1) 포함.

> 서버/클라 런타임 무영향(소비자 없음) → Unity 플레이 검증 불필요. GameFramework `file:` 패키지라 클·서 양쪽 컴파일되나 미사용.

## 산업 표준 매핑

- **counter-based / 키 기반 결정론 RNG** = 넷코드 표준 해법(단일 공유 스트림의 호출-횟수 desync 회피). Salmon et al. 2011(Philox/Threefry/ARS), SplitMix64. 웹 검증(2026-07-10): Factorio same-seed-diff-count desync, coherence/yal.cc "same seed + same order&count". 상세 `[[deterministic-rng-counter-based]]`.
- **이벤트별 씨앗 스트림** = 결정론 시뮬 관례(프레임/이벤트 RNG를 전역 씨앗+좌표로 파생 — KineticSim `coord(m,a,s,c)`). 한 이벤트 내 순서 의존은 클·서 공유 코드로 상쇄.
- **네이밍** = 기존 `IRandom`/`UnityRandom` 짝. Unity.Mathematics.`Random`(seeded struct)과 개념 동형이나 소유 구현.

## Out of Scope

- 씨앗 유도·키 정책, `IRandom` 교체, 서버 combat 배선, 매치시드 플럼빙, 클라 예측 전투 (전부 **A2**).
- 무편향(rejection) int 범위 — 게임 규모 범위에선 단순 modulo 편향이 무시 가능. 필요 시 A2 이후.

## Open Questions

- (없음 — 설계 확정. 골든 벡터 hex는 plan에서 SplitMix64 레퍼런스로 확정.)

## 진행

- [x] 브레인스토밍 (A1 범위=원시 도구만, 방식 1=이벤트 씨앗 스트림, SplitMix64 직접 소유, float [min,max))
- [x] spec self-review (플레이스홀더/일관성/범위/모호성 — 이상 없음)
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
