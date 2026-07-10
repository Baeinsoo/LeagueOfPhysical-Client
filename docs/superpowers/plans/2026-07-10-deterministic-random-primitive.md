# 결정론 난수 원시 도구 `DeterministicRandom` (A1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 씨앗 하나로 재현 가능한 결정론적 난수 스트림을 내는 순수 struct `DeterministicRandom`(SplitMix64)을 GameFramework에 추가하고 EditMode로 결정론을 검증한다.

**Architecture:** value 타입 struct. `new DeterministicRandom(ulong seed)` → `NextUInt64`(SplitMix64) / `NextFloat01`([0,1)) / `Range(float)`([min,max)) / `Range(int)`(max 제외). 정수 기반이라 플랫폼·사이드 결정론 안전. 소비자 없음(A2가 씨앗 유도·서버 배선).

**Tech Stack:** C# 순수 struct (엔진 비의존), NUnit EditMode. GameFramework 패키지(`file:` 참조).

## Global Constraints

- **저장소:** `GameFramework`(`C:/Users/re5na/workspace/LOP/GameFramework`). **피처 브랜치 `feature/deterministic-random-primitive`** 로 작업·커밋(현재 `main@22807e6`). main 직접 커밋 금지.
- **네임스페이스:** `GameFramework`(구현), `GameFramework.Tests`(테스트). NUnit.
- **엔진 비의존:** `DeterministicRandom`은 `UnityEngine` 참조 금지 — 순수 정수/float 연산만.
- **패키지 편집 후:** client Unity 인스턴스에 `refresh_unity` → `editor_state.isCompiling` false 폴링 → `read_console` 확인 → `run_tests`. 모든 UnityMCP 호출에 `unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4"`(에디터 2개 연결, client 타깃; 해시 바뀌면 `mcpforunity://instances` 재해석).
- **결정론 불변:** SplitMix64 상수/시프트를 임의 변경 금지 — 스트림이 바뀌면 넷코드 결정론 파괴. Task 1의 레퍼런스-벡터 테스트가 이를 잠근다.

---

### Task 1: `DeterministicRandom` struct + 결정론 테스트 (GameFramework, TDD)

**Files:**
- Create: `Runtime/Scripts/Game/DeterministicRandom.cs`
- Test: `Tests/Runtime/DeterministicRandomTests.cs`

**Interfaces:**
- Consumes: (없음)
- Produces:
  - `struct GameFramework.DeterministicRandom` — `DeterministicRandom(ulong seed)`, `ulong NextUInt64()`, `float NextFloat01()`(∈[0,1)), `float Range(float minInclusive, float maxExclusive)`(∈[min,max), min==max→min), `int Range(int minInclusive, int maxExclusive)`(∈[min,max), max≤min→min).
  - 같은 씨앗 → 동일 시퀀스(재현성 보장).

- [ ] **Step 1: 실패 테스트 작성** — `Tests/Runtime/DeterministicRandomTests.cs` 생성.

```csharp
using NUnit.Framework;

namespace GameFramework.Tests
{
    public class DeterministicRandomTests
    {
        [Test]
        public void Same_seed_produces_identical_sequence()
        {
            var a = new DeterministicRandom(12345UL);
            var b = new DeterministicRandom(12345UL);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(a.NextUInt64(), b.NextUInt64(), $"mismatch at draw {i}");
            }
        }

        [Test]
        public void Different_seeds_diverge()
        {
            var a = new DeterministicRandom(1UL);
            var b = new DeterministicRandom(2UL);

            Assert.AreNotEqual(a.NextUInt64(), b.NextUInt64());
        }

        // 알고리즘 고정(회귀 잠금): seed=0의 SplitMix64 표준 레퍼런스 벡터.
        // 이 테스트가 실패하면 구현이 SplitMix64에서 벗어난 것 — 기대값을 impl에 맞추지 말고 구현을 고쳐라.
        [Test]
        public void Seed_zero_matches_splitmix64_reference_vectors()
        {
            var r = new DeterministicRandom(0UL);

            Assert.AreEqual(0xE220A8397B1DCDAFUL, r.NextUInt64());
            Assert.AreEqual(0x6E789E6AA1B965F4UL, r.NextUInt64());
            Assert.AreEqual(0x06C45D188009454FUL, r.NextUInt64());
            Assert.AreEqual(0xF88BB8A8724C81ECUL, r.NextUInt64());
            Assert.AreEqual(0x1B39896A51A8749BUL, r.NextUInt64());
        }

        [Test]
        public void NextFloat01_is_in_unit_interval()
        {
            var r = new DeterministicRandom(999UL);
            for (int i = 0; i < 10000; i++)
            {
                float f = r.NextFloat01();
                Assert.GreaterOrEqual(f, 0f);
                Assert.Less(f, 1f);
            }
        }

        [Test]
        public void Range_float_is_in_half_open_interval()
        {
            var r = new DeterministicRandom(7UL);
            for (int i = 0; i < 10000; i++)
            {
                float f = r.Range(1.25f, 1.75f);
                Assert.GreaterOrEqual(f, 1.25f);
                Assert.Less(f, 1.75f);
            }
        }

        [Test]
        public void Range_float_equal_bounds_returns_min()
        {
            var r = new DeterministicRandom(7UL);
            Assert.AreEqual(3f, r.Range(3f, 3f));
        }

        [Test]
        public void Range_int_is_in_half_open_interval_and_covers_ends()
        {
            var r = new DeterministicRandom(42UL);
            bool sawMin = false, sawMax = false;
            for (int i = 0; i < 10000; i++)
            {
                int v = r.Range(2, 5); // {2,3,4}
                Assert.GreaterOrEqual(v, 2);
                Assert.Less(v, 5);
                if (v == 2) sawMin = true;
                if (v == 4) sawMax = true;
            }
            Assert.IsTrue(sawMin, "never produced min");
            Assert.IsTrue(sawMax, "never produced max-1");
        }

        [Test]
        public void Range_int_empty_or_inverted_returns_min()
        {
            var r = new DeterministicRandom(42UL);
            Assert.AreEqual(5, r.Range(5, 5));   // empty
            Assert.AreEqual(9, r.Range(9, 3));   // inverted (max<min) → min, no-op
        }
    }
}
```

- [ ] **Step 2: 테스트 실패(red) 확인** — client Unity에서 EditMode 실행.
Run(MCP): `refresh_unity(unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")` → `editor_state` isCompiling false까지 → `run_tests(mode="EditMode", filter="DeterministicRandomTests", unity_instance="…")`.
Expected: 컴파일 실패 — `The type or namespace name 'DeterministicRandom' could not be found`.

- [ ] **Step 3: 구현** — `Runtime/Scripts/Game/DeterministicRandom.cs` 생성.

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

        public DeterministicRandom(ulong seed)
        {
            _state = seed;
        }

        /// <summary>다음 64비트 난수(SplitMix64). 스트림을 한 칸 감는다.</summary>
        public ulong NextUInt64()
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>[0,1) float. 상위 24비트를 2^24로 나눠 부동소수 누적 없이 결정론 유지.</summary>
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
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }
            ulong span = (ulong)((long)maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt64() % span);
        }
    }
}
```

- [ ] **Step 4: 테스트 통과(green) 확인** — Step 2와 동일 실행.
Expected: 8개 테스트 PASS. **레퍼런스-벡터 테스트가 실패하면**: 위 상수/시프트가 그대로인지 먼저 확인(구현이 옳으면 통과해야 함). 기대 hex는 seed=0의 표준 SplitMix64 값이므로 **기대값을 impl 출력에 맞춰 바꾸지 말 것** — 구현을 고쳐라.

- [ ] **Step 5: 커밋** (GameFramework 저장소)

```bash
cd /c/Users/re5na/workspace/LOP/GameFramework
git checkout -b feature/deterministic-random-primitive
git add Runtime/Scripts/Game/DeterministicRandom.cs Runtime/Scripts/Game/DeterministicRandom.cs.meta Tests/Runtime/DeterministicRandomTests.cs Tests/Runtime/DeterministicRandomTests.cs.meta
git commit -m "feat(game): DeterministicRandom — SplitMix64 결정론 난수 스트림 (A1)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
> `.meta`는 Unity가 생성한 것을 함께 커밋(refresh 후 생겼는지 확인). 없으면 `git status`로 확인 후 add.

---

### Task 2: spec/ROADMAP 마무리 + 머지

**Files:**
- Modify: `docs/superpowers/specs/2026-07-10-deterministic-random-primitive-design.md` (Client repo — 진행 체크)
- Modify: `docs/ROADMAP.md` (Client repo — A1 완료 반영)

- [ ] **Step 1: spec 진행 체크** — 사용자 리뷰 / writing-plans / 구현 항목 체크.

- [ ] **Step 2: ROADMAP 갱신** — Done ledger에 A1 한 줄 추가(07-10, spec/plan 링크). Next의 A(#1)에 "A1(DeterministicRandom) 완료 — 다음은 씨앗 유도·서버 배선·클라 예측(A2)" 주석. (A는 여전히 미완 키스톤이므로 Next에 남김.)

- [ ] **Step 3: GameFramework 브랜치 main 머지** (`--no-ff`).
```bash
cd /c/Users/re5na/workspace/LOP/GameFramework
git checkout main && git merge --no-ff feature/deterministic-random-primitive -m "Merge feature/deterministic-random-primitive: DeterministicRandom (A1)"
```

- [ ] **Step 4: Client spec/plan/ROADMAP 커밋 + main 머지** (`--no-ff`).
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add docs/superpowers/specs/2026-07-10-deterministic-random-primitive-design.md docs/ROADMAP.md
git commit -m "docs: DeterministicRandom (A1) 구현 완료 — spec 진행 + ROADMAP Done"
git checkout main && git merge --no-ff feature/deterministic-random-primitive -m "Merge feature/deterministic-random-primitive: A1 spec/plan/ROADMAP"
```
> **주의:** Client working tree의 기존 무관 변경(`Assets/Art`, `UIRoot.prefab`, `GraphicsSettings.asset`)은 `git add`에 포함하지 말 것.

---

## Self-Review

**1. Spec coverage:**
- `DeterministicRandom` struct(생성자/NextUInt64/NextFloat01/Range float·int) → Task 1 Step 3 ✓
- SplitMix64, 정수 기반 float 변환 → Step 3 코드 ✓
- 재현성 + 레퍼런스-벡터(회귀 잠금) + 범위/no-op 테스트 → Task 1 Step 1의 8개 ✓
- 엔진 비의존(`UnityEngine` 미참조) → Step 3 코드에 UnityEngine 없음 ✓
- A2로 미룬 것(씨앗 유도/IRandom 교체/서버 배선) → 태스크 없음(의도적) ✓

**2. Placeholder scan:** 레퍼런스 hex는 실제 값(placeholder 아님) + 검증 지침 포함. TODO/TBD 없음. ✓

**3. Type consistency:** 테스트가 부르는 `NextUInt64`/`NextFloat01`/`Range(float,float)`/`Range(int,int)`가 Step 3 구현 시그니처와 일치 ✓. `Range(int)` no-op(=min) / inverted(=min) 동작이 테스트(`Range_int_empty_or_inverted_returns_min`)와 일치 ✓.

이상 없음.

## Execution Handoff

(작성자가 실행 방식 선택을 사용자에게 제시 — subagent-driven 권장)
