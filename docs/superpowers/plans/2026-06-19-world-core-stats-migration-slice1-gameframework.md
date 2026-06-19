# World Core Stats Migration — Slice 1 (GameFramework) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** GameFramework에 `EntityStatType` enum(4 primary)과 `StatsSystem`의 베이스 스탯 변경 메서드(`SetBase`/`AddBase`)를 추가하고 EditMode 테스트로 검증한다.

**Architecture:** 순수 C# World 코어(`GameFramework.World`)의 `StatsSystem`과 신규 enum만 손댄다. `Stats` 컴포넌트(dict `BaseStats` + `Modifiers`)는 무변경. 기존 `GetValue`/`AddModifier`/`RemoveModifiersBySourceId` 유지. 추가만이라 비파괴 — 현재 World `StatsSystem` 호출처가 0(클·서 어디서도 미사용)이라 런타임 영향 없음(서버/클라/Shared 후속 슬라이스의 게이트).

**Tech Stack:** C# (Unity), NUnit EditMode 테스트(`baegames.GameFramework.World.Tests`), GameFramework는 클·서가 `file:` UPM으로 공유.

**Related spec:** `docs/superpowers/specs/2026-06-19-world-core-stats-migration-design.md` (Slice 1)

**Resolved Unity instances (매 UnityMCP 호출에 명시 — HTTP stateless):**
- Client: `LeagueOfPhysical-Client@de70658b9450cbb4`
- Server: `LeagueOfPhysical-Server@f99391fa2dbaaf3c`

---

## File Structure

GameFramework repo: `C:\Users\re5na\workspace\LOP\GameFramework`.

- **Create:** `Runtime/Scripts/World/EntityStatType.cs` — 4 primary 스탯 enum. (기존 `ModifierType.cs`/`StatModifier.cs`가 `World/` 루트에 있으므로 `World/Enums/`가 아니라 **루트에 배치** — 기존 패턴 일관.)
- **Modify:** `Runtime/Scripts/World/Systems/StatsSystem.cs` — `SetBase`/`AddBase` 추가.
- **Modify:** `Tests/World/StatsSystemTests.cs` — `SetBase`/`AddBase` 테스트 추가(`EntityStatType` 사용).
- 무변경: `Runtime/Scripts/World/Components/Stats.cs`, `StatModifier.cs`, `ModifierType.cs`.

---

## Task 0: GameFramework 피처 브랜치 생성

**Files:** (git 작업만)

- [ ] **Step 1: 현재 상태 확인**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/GameFramework" status --short --branch | head -3
```
Expected: 보통 `main`. 무관 변경이 있어도 그대로 둔다(이 plan은 `EntityStatType.cs`/`StatsSystem.cs`/`StatsSystemTests.cs`만 커밋).

- [ ] **Step 2: 피처 브랜치 생성**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/GameFramework" checkout -b feature/world-core-stats-migration
```
Expected: `Switched to a new branch 'feature/world-core-stats-migration'` (이미 있으면 checkout).

---

## Task 1: EntityStatType enum 생성

**Files:**
- Create: `C:\Users\re5na\workspace\LOP\GameFramework\Runtime\Scripts\World\EntityStatType.cs`

- [ ] **Step 1: enum 파일 작성**

`Runtime/Scripts/World/EntityStatType.cs` 신규 생성:
```csharp
namespace GameFramework.World
{
    /// <summary>
    /// 엔티티 primary 스탯 종류. <see cref="Stats"/>의 BaseStats/Modifiers dict 키로 (int) 캐스트해 사용한다.
    /// 새 스탯이 필요하면 값을 추가한다.
    /// </summary>
    public enum EntityStatType
    {
        Strength,
        Dexterity,
        Intelligence,
        Vitality,
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Run (UnityMCP):
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`

Expected: 에러 0건.

- [ ] **Step 3: .meta 확인**

Unity가 `EntityStatType.cs.meta`를 생성했는지 확인(커밋에 포함되어야 함):
```bash
ls "C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/World/EntityStatType.cs.meta"
```
Expected: 파일 존재. (없으면 refresh_unity 후 재확인 — Unity가 import 시 생성.)

---

## Task 2: StatsSystem.SetBase / AddBase 추가 (TDD)

**Files:**
- Modify: `C:\Users\re5na\workspace\LOP\GameFramework\Runtime\Scripts\World\Systems\StatsSystem.cs`
- Test: `C:\Users\re5na\workspace\LOP\GameFramework\Tests\World\StatsSystemTests.cs`

- [ ] **Step 1: 실패 테스트 추가 (red)**

`StatsSystemTests.cs`의 클래스 닫는 `}` 앞(마지막 테스트 뒤)에 추가. `EntityStatType`(Task 1) 실제 enum을 키로 사용:
```csharp
        [Test]
        public void SetBase_sets_base_value_reflected_in_GetValue()
        {
            var stats = new Stats();

            _system.SetBase(stats, (int)EntityStatType.Strength, 7f);

            Assert.AreEqual(7f, _system.GetValue(stats, (int)EntityStatType.Strength));
        }

        [Test]
        public void SetBase_overwrites_existing_base()
        {
            var stats = new Stats();
            _system.SetBase(stats, (int)EntityStatType.Strength, 7f);

            _system.SetBase(stats, (int)EntityStatType.Strength, 3f);

            Assert.AreEqual(3f, _system.GetValue(stats, (int)EntityStatType.Strength));
        }

        [Test]
        public void AddBase_from_unset_returns_delta()
        {
            var stats = new Stats();

            float result = _system.AddBase(stats, (int)EntityStatType.Dexterity, 5f);

            Assert.AreEqual(5f, result);
            Assert.AreEqual(5f, _system.GetValue(stats, (int)EntityStatType.Dexterity));
        }

        [Test]
        public void AddBase_accumulates_and_returns_new_value()
        {
            var stats = new Stats();
            _system.SetBase(stats, (int)EntityStatType.Vitality, 10f);

            float result = _system.AddBase(stats, (int)EntityStatType.Vitality, 1f);

            Assert.AreEqual(11f, result);
            Assert.AreEqual(11f, _system.GetValue(stats, (int)EntityStatType.Vitality));
        }

        [Test]
        public void AddBase_does_not_affect_modifiers()
        {
            var stats = new Stats();
            _system.AddModifier(stats, Flat(Stat.Attack, 20f));
            _system.SetBase(stats, (int)EntityStatType.Strength, 5f);

            _system.AddBase(stats, (int)EntityStatType.Strength, 3f);

            // Strength base = 8 (no modifiers on Strength); Attack modifier untouched
            Assert.AreEqual(8f, _system.GetValue(stats, (int)EntityStatType.Strength));
            Assert.AreEqual(20f, _system.GetValue(stats, (int)Stat.Attack));
        }
```

- [ ] **Step 2: 컴파일 실패 확인 (red)**

Run (UnityMCP):
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`

Expected: `_system.SetBase(...)` / `_system.AddBase(...)`에서 컴파일 에러(CS1061 — `StatsSystem`에 정의 없음). red 상태.

- [ ] **Step 3: SetBase/AddBase 구현 (green)**

`StatsSystem.cs`의 `GetValue` 메서드 뒤(또는 `AddModifier` 앞, 클래스 내부 적절한 위치)에 추가:
```csharp
        /// <summary>베이스 스탯 값을 설정(덮어쓰기)한다. 권위 시드/적용용. 모디파이어는 건드리지 않는다.</summary>
        public void SetBase(Stats stats, int statType, float value)
        {
            stats.BaseStats[statType] = value;
        }

        /// <summary>베이스 스탯에 delta를 더하고(없으면 0 기준) 새 베이스 값을 반환한다. 스탯 포인트 할당용.</summary>
        public float AddBase(Stats stats, int statType, float delta)
        {
            stats.BaseStats.TryGetValue(statType, out var current);
            float next = current + delta;
            stats.BaseStats[statType] = next;
            return next;
        }
```

- [ ] **Step 4: 컴파일 통과 확인**

Run (UnityMCP):
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`

Expected: 에러 0건.

- [ ] **Step 5: EditMode 테스트 실행 (green)**

Run (UnityMCP):
- `run_tests(unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4", mode="EditMode", test_filter="StatsSystemTests")`

Expected: `StatsSystemTests` 모든 테스트 PASS (기존 11개 + 신규 5개 = 16개). 기존 테스트 회귀 없음.

> `run_tests`가 이 환경에서 불안정하면 사용자에게 Test Runner > EditMode > `StatsSystemTests` 실행을 요청하고 결과(16 passed)를 확인한다. 단 컴파일이 깨끗하면 블록하지 말 것.

- [ ] **Step 6: 커밋**

```bash
git -C "C:/Users/re5na/workspace/LOP/GameFramework" add Runtime/Scripts/World/EntityStatType.cs Runtime/Scripts/World/EntityStatType.cs.meta Runtime/Scripts/World/Systems/StatsSystem.cs Tests/World/StatsSystemTests.cs
git -C "C:/Users/re5na/workspace/LOP/GameFramework" commit -m "feat(world): add EntityStatType enum + StatsSystem SetBase/AddBase (base stat mutation)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

> `EntityStatType.cs`와 `.meta`를 함께 staging(Unity 생성 .meta 동반 커밋 규칙). `.meta`가 아직 없으면 Task 1 Step 3로 돌아가 refresh 후 확인.

---

## Task 3: 서버 측 컴파일 회귀 확인 (file: 공유 비파괴 검증)

**Files:** (검증만)

- [ ] **Step 1: 서버 인스턴스 컴파일**

Run (UnityMCP):
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`

Expected: 에러 0건. (서버는 아직 World `StatsSystem`/`EntityStatType`을 호출하지 않으므로 추가는 비파괴. 서버 writer/combat flip은 Slice 3.)

- [ ] **Step 2: (커밋 없음)** — 검증 전용.

---

## Slice 1 완료 기준

- [ ] GameFramework `feature/world-core-stats-migration` 브랜치에 커밋 1개(Task 2; `EntityStatType.cs`+`.meta`+`StatsSystem.cs`+`StatsSystemTests.cs`).
- [ ] `StatsSystemTests` 16개 PASS.
- [ ] 클라·서버 양쪽 인스턴스 컴파일 0에러.
- [ ] 런타임 동작 변화 0 (World `StatsSystem` 호출처 없음 — 게이트 슬라이스).

다음: **Slice 2 (Shared/wire)** — `CharacterCreationData.proto`에 4 stat 필드 추가 + 재생성. (별도 plan)
