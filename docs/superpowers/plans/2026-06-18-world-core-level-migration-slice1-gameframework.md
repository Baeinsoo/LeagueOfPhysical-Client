# World Core Level Migration — Slice 1 (GameFramework) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** GameFramework `LevelSystem`에 statPoints 결합용 반환값(`AddExperience` → 증가 레벨 수)과 권위 스냅샷 적용 메서드(`ApplyAuthoritativeState`)를 추가하고, EditMode 테스트로 검증한다.

**Architecture:** 순수 C# World 코어(`GameFramework.World`)의 `LevelSystem`만 손댄다. Health/Mana `ApplyAuthoritativeState` 패턴을 그대로 미러. `Level` 컴포넌트는 무변경. 이 슬라이스는 **추가/시그니처 확장만**이라 비파괴이며, 현재 World `LevelSystem` 호출처가 0이라 클·서 런타임 영향이 없다(서버/클라 슬라이스 2·3의 게이트).

**Tech Stack:** C# (Unity), NUnit EditMode 테스트 (`baegames.GameFramework.World.Tests`), GameFramework는 클·서가 `file:` UPM 참조로 공유.

**Related spec:** `docs/superpowers/specs/2026-06-18-world-core-level-migration-design.md` (Slice 1)

---

## File Structure

- **Modify:** `C:\Users\re5na\workspace\LOP\GameFramework\Runtime\Scripts\World\Systems\LevelSystem.cs` — `AddExperience` 반환 타입 `void`→`int`, `ApplyAuthoritativeState` 추가.
- **Modify:** `C:\Users\re5na\workspace\LOP\GameFramework\Tests\World\LevelSystemTests.cs` — `AddExperience` 반환값 테스트 + `ApplyAuthoritativeState` 테스트 추가.
- 무변경: `Runtime\Scripts\World\Components\Level.cs` (필드 그대로).

> 모든 작업은 **GameFramework repo** (`C:\Users\re5na\workspace\LOP\GameFramework`)에서 수행한다. spec·plan 문서는 클라 repo에 이미 커밋됨 — 이 plan은 코드만 다룬다.

---

## Task 0: GameFramework 피처 브랜치 생성

**Files:** (없음 — git 작업만)

- [ ] **Step 1: GameFramework repo에서 현재 브랜치 확인**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/GameFramework" status --short --branch | head -3
```
Expected: 현재 브랜치 표시 (보통 `main`). working tree에 무관 변경이 있으면 그대로 둔다(이 plan은 `LevelSystem.cs`/`LevelSystemTests.cs`만 커밋).

- [ ] **Step 2: 피처 브랜치 생성**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/GameFramework" checkout -b feature/world-core-level-migration
```
Expected: `Switched to a new branch 'feature/world-core-level-migration'`

---

## Task 1: `AddExperience`가 증가한 레벨 수(int)를 반환

**Files:**
- Modify: `C:\Users\re5na\workspace\LOP\GameFramework\Runtime\Scripts\World\Systems\LevelSystem.cs:7-16`
- Test: `C:\Users\re5na\workspace\LOP\GameFramework\Tests\World\LevelSystemTests.cs`

- [ ] **Step 1: 반환값 검증 테스트 추가 (failing)**

`LevelSystemTests.cs`의 마지막 테스트(`AddExperience_does_not_level_up_when_threshold_is_not_positive`) 뒤, 클래스 닫는 `}` 앞에 아래 4개 테스트를 추가한다. 기존 테스트는 `_system.AddExperience(level, X);`를 void로 호출 중이라 변경 불필요(반환값 무시는 소스 호환).

```csharp
        [Test]
        public void AddExperience_below_threshold_returns_zero_levels_gained()
        {
            var level = NewLevel();

            int gained = _system.AddExperience(level, 50);

            Assert.AreEqual(0, gained);
        }

        [Test]
        public void AddExperience_reaching_threshold_returns_one_level_gained()
        {
            var level = NewLevel();

            int gained = _system.AddExperience(level, 120);

            Assert.AreEqual(1, gained);
        }

        [Test]
        public void AddExperience_spanning_multiple_levels_returns_levels_gained()
        {
            var level = NewLevel();

            int gained = _system.AddExperience(level, 250);

            Assert.AreEqual(2, gained);
        }

        [Test]
        public void AddExperience_with_nonpositive_threshold_returns_zero()
        {
            var level = NewLevel(value: 1, exp: 0, expToNext: 0);

            int gained = _system.AddExperience(level, 50);

            Assert.AreEqual(0, gained);
        }
```

- [ ] **Step 2: 컴파일 실패 확인 (red)**

UnityMCP로 GameFramework를 호스트하는 클라 인스턴스에서 컴파일 상태를 확인한다. 먼저 클라 id 해석: `mcpforunity://instances` 리소스를 읽어 `name == "LeagueOfPhysical-Client"`인 instance의 `id`(`Name@hash`)를 얻는다.

Run (UnityMCP):
- `refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")`
- `read_console(unity_instance="LeagueOfPhysical-Client@<hash>", types=["error"])`

Expected: `int gained = _system.AddExperience(...)` 줄에서 컴파일 에러 — `void`를 `int`에 할당 불가 (CS0029 또는 유사). 이게 red 상태.

- [ ] **Step 3: `AddExperience` 반환 타입을 int로 변경 (green 구현)**

`LevelSystem.cs`의 `AddExperience` 전체를 아래로 교체:

```csharp
        /// <summary>경험치를 더하고, ExpToNext를 넘는 동안 레벨업하며 나머지를 이월한다. 증가한 레벨 수를 반환한다.</summary>
        public int AddExperience(Level level, long amount)
        {
            level.Exp += amount;

            int gained = 0;
            while (level.ExpToNext > 0 && level.Exp >= level.ExpToNext)
            {
                level.Exp -= level.ExpToNext;
                level.Value++;
                gained++;
            }

            return gained;
        }
```

- [ ] **Step 4: 컴파일 통과 확인**

Run (UnityMCP):
- `refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")`
- `read_console(unity_instance="LeagueOfPhysical-Client@<hash>", types=["error"])`

Expected: 에러 0건.

- [ ] **Step 5: EditMode 테스트 실행 (green 확인)**

Run (UnityMCP):
- `run_tests(unity_instance="LeagueOfPhysical-Client@<hash>", mode="EditMode", test_filter="LevelSystemTests")`

Expected: `LevelSystemTests`의 모든 테스트 PASS (기존 5개 + 신규 4개 = 9개). 기존 void-호출 테스트도 그대로 통과(반환값 무시).

> UnityMCP `run_tests`가 이 환경에서 불안정하면, 사용자에게 Unity Editor의 **Window > General > Test Runner > EditMode**에서 `LevelSystemTests` 실행을 요청하고 결과(9 passed)를 확인한다.

- [ ] **Step 6: 커밋**

```bash
git -C "C:/Users/re5na/workspace/LOP/GameFramework" add Runtime/Scripts/World/Systems/LevelSystem.cs Tests/World/LevelSystemTests.cs
git -C "C:/Users/re5na/workspace/LOP/GameFramework" commit -m "feat(world): LevelSystem.AddExperience returns levels gained

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: `LevelSystem.ApplyAuthoritativeState` 추가

**Files:**
- Modify: `C:\Users\re5na\workspace\LOP\GameFramework\Runtime\Scripts\World\Systems\LevelSystem.cs`
- Test: `C:\Users\re5na\workspace\LOP\GameFramework\Tests\World\LevelSystemTests.cs`

- [ ] **Step 1: `ApplyAuthoritativeState` 테스트 추가 (failing)**

`LevelSystemTests.cs`의 클래스 닫는 `}` 앞(Task 1에서 추가한 테스트 뒤)에 추가:

```csharp
        [Test]
        public void ApplyAuthoritativeState_overwrites_Value_and_Exp()
        {
            var level = NewLevel(value: 1, exp: 10, expToNext: 100);

            _system.ApplyAuthoritativeState(level, value: 5, exp: 250);

            Assert.AreEqual(5, level.Value);
            Assert.AreEqual(250, level.Exp);
        }

        [Test]
        public void ApplyAuthoritativeState_does_not_touch_ExpToNext()
        {
            var level = NewLevel(value: 1, exp: 0, expToNext: 100);

            _system.ApplyAuthoritativeState(level, value: 3, exp: 40);

            Assert.AreEqual(100, level.ExpToNext);
        }
```

- [ ] **Step 2: 컴파일 실패 확인 (red)**

Run (UnityMCP):
- `refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")`
- `read_console(unity_instance="LeagueOfPhysical-Client@<hash>", types=["error"])`

Expected: `_system.ApplyAuthoritativeState(...)` 에서 컴파일 에러 — `LevelSystem`에 `ApplyAuthoritativeState` 정의 없음 (CS1061). red 상태.

- [ ] **Step 3: `ApplyAuthoritativeState` 구현 (green)**

`LevelSystem.cs`의 `AddExperience` 메서드 뒤(클래스 닫는 `}` 앞)에 추가:

```csharp
        /// <summary>
        /// 권위 스냅샷 등으로 Value/Exp를 통째로 덮어쓴다. 결정/계산/가드 없음 (Application 메서드).
        /// ExpToNext는 wire 미전송 상수라 건드리지 않는다(생성 시 시드 값 유지).
        /// </summary>
        public void ApplyAuthoritativeState(Level level, int value, long exp)
        {
            level.Value = value;
            level.Exp = exp;
        }
```

- [ ] **Step 4: 컴파일 통과 확인**

Run (UnityMCP):
- `refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")`
- `read_console(unity_instance="LeagueOfPhysical-Client@<hash>", types=["error"])`

Expected: 에러 0건.

- [ ] **Step 5: EditMode 테스트 실행 (green 확인)**

Run (UnityMCP):
- `run_tests(unity_instance="LeagueOfPhysical-Client@<hash>", mode="EditMode", test_filter="LevelSystemTests")`

Expected: `LevelSystemTests` 모든 테스트 PASS (11개 = 기존 5 + Task1 4 + Task2 2).

- [ ] **Step 6: 커밋**

```bash
git -C "C:/Users/re5na/workspace/LOP/GameFramework" add Runtime/Scripts/World/Systems/LevelSystem.cs Tests/World/LevelSystemTests.cs
git -C "C:/Users/re5na/workspace/LOP/GameFramework" commit -m "feat(world): add LevelSystem.ApplyAuthoritativeState (authoritative snapshot apply)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: 서버 측 컴파일 회귀 확인 (file: 공유 비파괴 검증)

GameFramework는 클·서가 `file:` UPM으로 공유한다. Slice 1은 추가/시그니처 확장이라 비파괴여야 하므로 **서버 인스턴스 컴파일**도 확인한다(서버 working tree·픽스처는 건드리지 않음 — 읽기 검증만).

**Files:** (없음 — 검증만)

- [ ] **Step 1: 서버 인스턴스 컴파일 확인**

서버 id 해석: `mcpforunity://instances`에서 `name == "LeagueOfPhysical-Server"`인 instance의 `id`(`Name@hash`).

Run (UnityMCP):
- `refresh_unity(unity_instance="LeagueOfPhysical-Server@<hash>")`
- `read_console(unity_instance="LeagueOfPhysical-Server@<hash>", types=["error"])`

Expected: 에러 0건. (서버는 아직 World `LevelSystem`을 호출하지 않으므로 시그니처 확장 영향 없음. 서버 writer flip은 Slice 2.)

- [ ] **Step 2: (커밋 없음)**

이 태스크는 검증 전용. 새 커밋 없음.

---

## Slice 1 완료 기준

- [ ] GameFramework `feature/world-core-level-migration` 브랜치에 2개 커밋(Task 1·2).
- [ ] `LevelSystemTests` 11개 PASS.
- [ ] 클라·서버 양쪽 인스턴스 컴파일 에러 0.
- [ ] 런타임 동작 변화 0 (World `LevelSystem` 호출처가 아직 없음 — 게이트 슬라이스).

다음: **Slice 2 (서버)** plan — 생성 + writer flip(+statPoints) + 읽기 flip + DI + 레거시 `LevelComponent` 제거. (별도 plan 작성)
