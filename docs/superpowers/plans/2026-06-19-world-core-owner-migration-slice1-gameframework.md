# World Core Owner Migration — Slice 1 (GameFramework) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** GameFramework에 신규 `Owner` 컴포넌트(소유 마커+정체성)와 `Stats.UnspentPoints` 필드 + `StatsSystem`의 statPoints 메서드(`AddUnspent`/`Allocate`/`SetUnspent`)를 추가하고 EditMode 테스트로 검증한다.

**Architecture:** 순수 C# World 코어만 손댄다. `Owner`는 로직 없는 데이터 태그(시스템 없음). statPoints는 기존 `Stats`에 `UnspentPoints`로 응집하고 할당 로직은 `StatsSystem.Allocate`(원자적 spend+AddBase)에 둔다. 추가만이라 비파괴 — 현재 호출처 0(클·서 미사용)이라 런타임 영향 없음(서버/클라 후속 슬라이스의 게이트).

**Tech Stack:** C# (Unity), NUnit EditMode(`baegames.GameFramework.World.Tests`). GameFramework는 클·서 `file:` UPM 공유. `StatsSystem`은 `noEngineReferences`(UnityEngine 미사용) — `Mathf` 대신 `(int)` 캐스트.

**Related spec:** `docs/superpowers/specs/2026-06-19-world-core-owner-migration-design.md` (Slice 1)

**Resolved Unity instances (매 UnityMCP 호출에 명시 — HTTP stateless):**
- Client: `LeagueOfPhysical-Client@de70658b9450cbb4`
- Server: `LeagueOfPhysical-Server@f99391fa2dbaaf3c`

---

## File Structure (GameFramework repo: `C:\Users\re5na\workspace\LOP\GameFramework`)

- **Create:** `Runtime/Scripts/World/Components/Owner.cs` — 소유 마커 컴포넌트(`{ string OwnerId }`).
- **Modify:** `Runtime/Scripts/World/Components/Stats.cs` — `int UnspentPoints` 추가.
- **Modify:** `Runtime/Scripts/World/Systems/StatsSystem.cs` — `AddUnspent`/`Allocate`/`SetUnspent`.
- **Modify:** `Tests/World/StatsSystemTests.cs` — 신규 메서드 테스트.

---

## Task 0: GameFramework 피처 브랜치

- [ ] **Step 1:** `git -C "C:/Users/re5na/workspace/LOP/GameFramework" status --short --branch | head -3` — 무관 변경 있어도 그대로(이 plan은 Owner.cs/Stats.cs/StatsSystem.cs/StatsSystemTests.cs만 커밋).
- [ ] **Step 2:** `git -C "C:/Users/re5na/workspace/LOP/GameFramework" checkout -b feature/world-core-owner-migration` (이미 있으면 checkout).

---

## Task 1: Owner 컴포넌트 생성

**Files:** Create `C:\Users\re5na\workspace\LOP\GameFramework\Runtime\Scripts\World\Components\Owner.cs`

- [ ] **Step 1: 파일 작성**
```csharp
namespace GameFramework.World
{
    /// <summary>
    /// 엔티티가 유저/세션에 의해 소유·제어됨을 표시한다. 존재 자체가 "플레이어(비-NPC)" 마커.
    /// <see cref="OwnerId"/>는 소유자 식별자(LOP=userId). 로직 없음(anemic) — 생성 시 1회 세팅 후 불변.
    /// </summary>
    public class Owner : Component
    {
        public string OwnerId { get; set; }
    }
}
```
(시스템·테스트 없음 — `Transform`/`Velocity`처럼 순수 데이터. 마커는 `Entity.Has<Owner>()`로 소비.)

- [ ] **Step 2: 컴파일 확인**
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
Expected: 에러 0건.

- [ ] **Step 3: .meta 확인**
```bash
ls "C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/World/Components/Owner.cs.meta"
```
Expected: 파일 존재(없으면 refresh 재시도 — Unity import 시 생성). 커밋에 동반.

---

## Task 2: Stats.UnspentPoints + StatsSystem 메서드 (TDD)

**Files:**
- Modify: `Runtime/Scripts/World/Components/Stats.cs`
- Modify: `Runtime/Scripts/World/Systems/StatsSystem.cs`
- Test: `Tests/World/StatsSystemTests.cs`

- [ ] **Step 1: 실패 테스트 추가 (red)**

`StatsSystemTests.cs` 클래스 닫는 `}` 앞(마지막 테스트 뒤)에 추가(`EntityStatType` 실제 enum 사용):
```csharp
        [Test]
        public void AddUnspent_accumulates()
        {
            var stats = new Stats();

            _system.AddUnspent(stats, 3);
            _system.AddUnspent(stats, 2);

            Assert.AreEqual(5, stats.UnspentPoints);
        }

        [Test]
        public void SetUnspent_overwrites()
        {
            var stats = new Stats();
            _system.AddUnspent(stats, 3);

            _system.SetUnspent(stats, 10);

            Assert.AreEqual(10, stats.UnspentPoints);
        }

        [Test]
        public void Allocate_with_points_spends_one_raises_base_and_returns_new_value()
        {
            var stats = new Stats();
            _system.SetBase(stats, (int)EntityStatType.Strength, 5f);
            _system.SetUnspent(stats, 2);

            int result = _system.Allocate(stats, (int)EntityStatType.Strength);

            Assert.AreEqual(6, result);
            Assert.AreEqual(6f, _system.GetValue(stats, (int)EntityStatType.Strength));
            Assert.AreEqual(1, stats.UnspentPoints);
        }

        [Test]
        public void Allocate_with_zero_points_is_noop_and_returns_current()
        {
            var stats = new Stats();
            _system.SetBase(stats, (int)EntityStatType.Strength, 5f);
            _system.SetUnspent(stats, 0);

            int result = _system.Allocate(stats, (int)EntityStatType.Strength);

            Assert.AreEqual(5, result);
            Assert.AreEqual(5f, _system.GetValue(stats, (int)EntityStatType.Strength));
            Assert.AreEqual(0, stats.UnspentPoints);
        }
```

- [ ] **Step 2: 컴파일 실패 확인 (red)**
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
Expected: `stats.UnspentPoints` / `_system.AddUnspent` / `_system.Allocate` / `_system.SetUnspent`에서 컴파일 에러(CS1061 등). red.

- [ ] **Step 3: Stats에 UnspentPoints 추가 (green 1/2)**

`Stats.cs`의 `Modifiers` 프로퍼티 다음 줄에 추가:
```csharp
        public int UnspentPoints { get; set; }
```

- [ ] **Step 4: StatsSystem 메서드 추가 (green 2/2)**

`StatsSystem.cs`의 `AddBase` 메서드 뒤(클래스 닫는 `}` 앞)에 추가:
```csharp
        /// <summary>미할당 스탯 포인트를 더한다(레벨업 보상 등).</summary>
        public void AddUnspent(Stats stats, int amount)
        {
            stats.UnspentPoints += amount;
        }

        /// <summary>미할당 스탯 포인트를 권위 값으로 덮어쓴다(스냅샷 적용).</summary>
        public void SetUnspent(Stats stats, int value)
        {
            stats.UnspentPoints = value;
        }

        /// <summary>
        /// 포인트가 있으면 1 소비하고 해당 스탯 베이스를 1 올린다(원자적). 적용 후 스탯 최종값을 반환한다.
        /// 포인트가 없으면 no-op으로 현재 값을 반환한다. StatsSystem은 noEngineReferences라 Mathf 대신 (int) 캐스트.
        /// </summary>
        public int Allocate(Stats stats, int statType)
        {
            if (stats.UnspentPoints <= 0)
            {
                return (int)GetValue(stats, statType);
            }

            stats.UnspentPoints--;
            AddBase(stats, statType, 1);
            return (int)GetValue(stats, statType);
        }
```

- [ ] **Step 5: 컴파일 통과 확인**
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
Expected: 에러 0건.

- [ ] **Step 6: EditMode 테스트 실행 (green)**
- `run_tests(unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4", mode="EditMode", test_filter="StatsSystemTests")`
Expected: `StatsSystemTests` 모든 테스트 PASS(기존 15개 + 신규 4개 = 19개). 기존 회귀 없음.

> `run_tests` 불안정 시 사용자에게 Test Runner > EditMode > `StatsSystemTests` 실행 요청, 결과(19 passed) 확인. 컴파일 깨끗하면 블록하지 말 것.

- [ ] **Step 7: 커밋**
```bash
git -C "C:/Users/re5na/workspace/LOP/GameFramework" add Runtime/Scripts/World/Components/Owner.cs Runtime/Scripts/World/Components/Owner.cs.meta Runtime/Scripts/World/Components/Stats.cs Runtime/Scripts/World/Systems/StatsSystem.cs Tests/World/StatsSystemTests.cs
git -C "C:/Users/re5na/workspace/LOP/GameFramework" commit -m "feat(world): add Owner component + Stats.UnspentPoints + StatsSystem AddUnspent/Allocate/SetUnspent

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
(Owner.cs+.meta 동반 커밋. .meta 없으면 Task 1 Step 3로 돌아가 refresh 후 확인.)

---

## Task 3: 서버 측 컴파일 회귀 확인 (file: 공유 비파괴)

- [ ] **Step 1: 서버 인스턴스 컴파일**
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
Expected: 에러 0건(서버는 아직 Owner/UnspentPoints 미사용 — 추가는 비파괴).

- [ ] **Step 2: (커밋 없음)** — 검증 전용.

---

## Slice 1 완료 기준

- [ ] GameFramework `feature/world-core-owner-migration`에 커밋 1개(Owner.cs+.meta+Stats.cs+StatsSystem.cs+StatsSystemTests.cs).
- [ ] `StatsSystemTests` 19개 PASS.
- [ ] 클라·서버 양쪽 컴파일 0에러.
- [ ] 런타임 동작 변화 0 (Owner/UnspentPoints 호출처 없음 — 게이트).

다음: **Slice 2 (서버)** — Owner 생성 + 마커 5곳 repoint + 레벨업 grant + 할당 Allocate + 스냅샷 read + 레거시 PlayerComponent 제거(픽스처 stash 댄스). (별도 plan)
