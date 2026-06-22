# RNG 추상화 (`IRandom`) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 서버 시뮬레이션의 직접 `UnityEngine.Random` 호출을 주입된 `IRandom`(GameFramework) 포트 + `UnityRandom` 어댑터 뒤로 격리한다(동작 보존, wrap-only).

**Architecture:** GameFramework에 `IRandom` 포트 + `UnityRandom` 어댑터(기존 `IPhysicsSimulator` 옆). 서버 `LOPCombatSystem`(생성자 주입)·`LOPGame`(`[Inject]` 필드)이 받아 6개 `Random.Range` 호출을 `rng.Range`로 교체, 서버 `GameLifetimeScope`에 등록. 클라 무변경.

**Tech Stack:** Unity, C#, VContainer DI, UnityMCP(컴파일 검증). 2 git repo(GameFramework / LeagueOfPhysical-Server).

---

## 작업 규약 (모든 Task 공통)

- **테스트 없음(의도적):** 동작 보존 리팩터. `UnityRandom`은 정적 `UnityEngine.Random` 래핑이라 단위 테스트 부적합, `LOPCombatSystem`/`LOPGame`은 서버 Assembly-CSharp라 asmdef 테스트 불가. 검증 = **컴파일 클린 + 실행 동작 정상**. seam의 페이오프는 *향후* 고정 `IRandom` 주입(전투 결정론 테스트) + 시드 결정론. (가짜 테스트 만들지 말 것.)
- **필드명 = `rng`** (spec의 `random` 아님). 이유: `LOPGame`에 이미 로컬 변수 `int random`이 있어(`:104`/`:249`) 필드를 `random`으로 두면 충돌. `rng`로 양쪽 클래스 일관 + 로컬 리네임 0.
- **UnityMCP 인스턴스 타게팅(필수):** 모든 UnityMCP 호출에 `unity_instance` 명시.
  - Server: `LeagueOfPhysical-Server@f99391fa2dbaaf3c`
  - Client: `LeagueOfPhysical-Client@de70658b9450cbb4`
  - (해시는 바뀔 수 있으니 불일치 시 `mcpforunity://instances` 재확인.)
- **.meta 커밋:** 새 `.cs`는 Write 생성 후 `refresh_unity`로 Unity가 `.meta` 생성. `.cs`+`.meta` 함께 `git add`.
- **Git(LOP 패턴):** 각 repo에서 피처 브랜치 `feature/random-abstraction` → main `--no-ff` 머지. main 직접 커밋 금지. 워크트리 안 씀. **변경 파일만 선택적 `git add`**(절대 `git add -A`/`.`/`commit -am` 금지) — 서버 미커밋 픽스처(`Assets/Scenes/Room.unity`·`Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`·`Assets/Art/`)는 스테이징하지 않고 보존. (`LOPGame.cs`는 사용자가 로컬 변경을 폐기해 HEAD와 일치하는 **클린 상태** — 이 plan의 RNG 변경만 얹힌다. 픽스처 충돌 없음.)
- **순서 의존:** Task 1(GameFramework) 먼저 — 타입이 컴파일돼야 서버가 참조. Task 1에서 두 에디터 refresh + 컴파일 클린 확인 후 Task 2.
- **푸시:** 이 plan은 push하지 않는다(사용자가 별도 지시 시 푸시).

---

## Task 1: GameFramework — IRandom + UnityRandom

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Game/IRandom.cs`
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Game/UnityRandom.cs`
- (Repo: `C:/Users/re5na/workspace/LOP/GameFramework`, asmdef `baegames.GameFramework.Runtime`, noEngineReferences=false)

- [ ] **Step 1: 포트 인터페이스 생성**

`Runtime/Scripts/Game/IRandom.cs`:
```csharp
namespace GameFramework
{
    /// <summary>
    /// 난수 굴림 포트. 시뮬레이션이 구체 RNG(UnityEngine.Random 등)에 직결되지 않도록 주입한다.
    /// 현재 구현(UnityRandom)은 비결정론 — 시드 기반 결정론 구현은 롤백 단계에서 drop-in.
    /// </summary>
    public interface IRandom
    {
        /// <summary>[minInclusive, maxInclusive] 범위의 float. (UnityEngine.Random.Range(float,float) 의미론 — max 포함)</summary>
        float Range(float minInclusive, float maxInclusive);

        /// <summary>[minInclusive, maxExclusive) 범위의 int. (UnityEngine.Random.Range(int,int) 의미론 — max 제외)</summary>
        int Range(int minInclusive, int maxExclusive);
    }
}
```

- [ ] **Step 2: Unity 어댑터 생성**

`Runtime/Scripts/Game/UnityRandom.cs` (UnityEngine.Random 풀 한정 — `using UnityEngine;` 불필요):
```csharp
namespace GameFramework
{
    /// <summary>UnityEngine.Random으로 <see cref="IRandom"/>을 구현하는 어댑터(비결정론, 전역 상태).</summary>
    public sealed class UnityRandom : IRandom
    {
        public float Range(float minInclusive, float maxInclusive)
            => UnityEngine.Random.Range(minInclusive, maxInclusive);

        public int Range(int minInclusive, int maxExclusive)
            => UnityEngine.Random.Range(minInclusive, maxExclusive);
    }
}
```

- [ ] **Step 3: 두 에디터 refresh + 컴파일 검증**

- `refresh_unity(scope="scripts", compile="request", mode="force", wait_for_ready=true, unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `refresh_unity(... unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
- 각각 `read_console(action="get", types=["error"], unity_instance=<해당>)` → 에러 0 확인. (클라는 IRandom을 *사용*하진 않지만 같은 file: 패키지라 컴파일됨.)

Expected: 에러 0. 새 타입 `GameFramework.IRandom`/`GameFramework.UnityRandom` 양쪽 해석.

- [ ] **Step 4: 커밋 (GameFramework repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git checkout -b feature/random-abstraction
git add Runtime/Scripts/Game/IRandom.cs Runtime/Scripts/Game/IRandom.cs.meta Runtime/Scripts/Game/UnityRandom.cs Runtime/Scripts/Game/UnityRandom.cs.meta
git status --short
git commit -m "feat(game): IRandom port + UnityRandom adapter

Wrap UnityEngine.Random behind an injectable IRandom so simulation code
is not hard-coupled to the global RNG. Lives alongside IPhysicsSimulator
in baegames.GameFramework.Runtime. Wrap-only (non-deterministic);
seed-based determinism comes with the rollback slice.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git checkout main
git merge --no-ff feature/random-abstraction -m "Merge feature/random-abstraction: IRandom + UnityRandom"
git branch -d feature/random-abstraction
git log --oneline -2
```

커밋 전 `git status --short`로 4개 파일(.cs+.meta)만 스테이징 확인. `.meta` 미생성 시 refresh 재시도(수기 생성 금지).

---

## Task 2: Server — LOPCombatSystem + LOPGame 주입/교체 + DI 등록

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/CombatSystem/LOPCombatSystem.cs`
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/LOPGame.cs`
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/GameLifetimeScope.cs`
- (Repo: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server`)

- [ ] **Step 1: `LOPCombatSystem` — 필드 + 생성자 주입**

`using GameFramework;`이 이미 1행에 있어 `IRandom` 단축 해석됨.

(a) 필드 블록(현재 8-11행) 끝에 추가. 변경 전:
```csharp
        private readonly GameFramework.World.WorldEventBuffer worldEventBuffer;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.HealthSystem healthSystem;
        private readonly GameFramework.World.StatsSystem statsSystem;
```
변경 후:
```csharp
        private readonly GameFramework.World.WorldEventBuffer worldEventBuffer;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.HealthSystem healthSystem;
        private readonly GameFramework.World.StatsSystem statsSystem;
        private readonly IRandom rng;
```

(b) 생성자(현재 13-23행). 변경 전:
```csharp
        public LOPCombatSystem(
            GameFramework.World.WorldEventBuffer worldEventBuffer,
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.HealthSystem healthSystem,
            GameFramework.World.StatsSystem statsSystem)
        {
            this.worldEventBuffer = worldEventBuffer;
            this.entityRegistry = entityRegistry;
            this.healthSystem = healthSystem;
            this.statsSystem = statsSystem;
        }
```
변경 후:
```csharp
        public LOPCombatSystem(
            GameFramework.World.WorldEventBuffer worldEventBuffer,
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.HealthSystem healthSystem,
            GameFramework.World.StatsSystem statsSystem,
            IRandom rng)
        {
            this.worldEventBuffer = worldEventBuffer;
            this.entityRegistry = entityRegistry;
            this.healthSystem = healthSystem;
            this.statsSystem = statsSystem;
            this.rng = rng;
        }
```

- [ ] **Step 2: `LOPCombatSystem` — 3개 호출부 교체**

(a) 데미지 변동(현재 65행). 변경 전: `                damage = Mathf.RoundToInt(damage * Random.Range(1.25f, 1.75f));`
변경 후: `                damage = Mathf.RoundToInt(damage * rng.Range(1.25f, 1.75f));`

(b) `IsDodge`(현재 105행). 변경 전: `            double roll = Random.Range(0.0f, 1.0f);` (`IsDodge` 메서드 안)
변경 후: `            double roll = rng.Range(0.0f, 1.0f);`

(c) `IsCritical`(현재 113행). 변경 전: `            double roll = Random.Range(0.0f, 1.0f);` (`IsCritical` 메서드 안)
변경 후: `            double roll = rng.Range(0.0f, 1.0f);`

> (b)와 (c)는 문자열이 동일하다(`            double roll = Random.Range(0.0f, 1.0f);`). Edit 시 `replace_all`로 둘 다 교체하거나, 각 메서드 본문 컨텍스트로 개별 교체. 둘 다 `rng.Range`로 바뀌어야 한다.

- [ ] **Step 3: `LOPGame` — `[Inject]` 필드 추가**

`using GameFramework;` 이미 있음. 기존 `[Inject]` 필드 블록(예: 37-38행 `entityRegistry`) 뒤에 추가:
```csharp
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        [Inject]
        private IRandom rng;
```
(앵커: 위 `entityRegistry` `[Inject]` 선언 직후에 `[Inject] private IRandom rng;` 삽입. 기존 다른 `[Inject]` 필드 뒤여도 무방 — 클래스 필드 영역 안이면 됨.)

- [ ] **Step 4: `LOPGame` — 3개(=4호출) 호출부 교체**

필드명이 `rng`라 로컬 `int random`과 충돌 없음 — 로컬은 그대로 둔다.

(a) 현재 104행. 변경 전: `                int random = UnityEngine.Random.Range(0, 3);`
변경 후: `                int random = rng.Range(0, 3);`

(b) 현재 249행. 변경 전: `            int random = UnityEngine.Random.Range(0, 2);`
변경 후: `            int random = rng.Range(0, 2);`

(c) 현재 325행. 변경 전: `            return new Vector3(UnityEngine.Random.Range(-20f, 20f), 0, UnityEngine.Random.Range(-20f, 20f));`
변경 후: `            return new Vector3(rng.Range(-20f, 20f), 0, rng.Range(-20f, 20f));`

- [ ] **Step 5: `GameLifetimeScope` — DI 등록**

`Configure`에서 기존 물리 어댑터 등록 줄 뒤에 추가. 변경 전:
```csharp
            builder.Register<WorldEventBridge>(Lifetime.Singleton);
            builder.Register<GameFramework.IPhysicsSimulator, GameFramework.UnityPhysicsSimulator>(Lifetime.Singleton);
```
변경 후:
```csharp
            builder.Register<WorldEventBridge>(Lifetime.Singleton);
            builder.Register<GameFramework.IPhysicsSimulator, GameFramework.UnityPhysicsSimulator>(Lifetime.Singleton);
            builder.Register<GameFramework.IRandom, GameFramework.UnityRandom>(Lifetime.Singleton);
```

- [ ] **Step 6: refresh + 컴파일 검증 (서버)**

- `refresh_unity(scope="scripts", compile="request", mode="force", wait_for_ready=true, unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")` → 에러 0.

Expected: 에러 0. 특히 "No registration for IRandom"(DI) / "`Random` 모호" / "use of unassigned local `random`"(필드명 충돌) 없음.

- [ ] **Step 7: 커밋 (서버 repo)**

스테이징 대상 = 수정한 3개 파일만. `Room.unity`/`ConfigureRoomComponent.cs`/`Art/`는 스테이징하지 않는다. (`LOPGame.cs`는 클린 상태라 RNG 변경만 스테이징됨.)

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git checkout -b feature/random-abstraction
git add Assets/Scripts/CombatSystem/LOPCombatSystem.cs Assets/Scripts/Game/LOPGame.cs Assets/Scripts/Game/GameLifetimeScope.cs
git status --short
git diff --staged Assets/Scripts/Game/LOPGame.cs
git commit -m "refactor(combat,game): inject IRandom into server sim

Swap direct UnityEngine.Random for injected rng.Range in LOPCombatSystem
(damage variance, crit/dodge rolls) and LOPGame (character/monster pick,
spawn position). Register IRandom->UnityRandom in GameLifetimeScope.
Behavior-preserving. Client view jitter untouched.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git checkout main
git merge --no-ff feature/random-abstraction -m "Merge feature/random-abstraction: server IRandom injection"
git branch -d feature/random-abstraction
git log --oneline -2
```

`git status --short` BEFORE commit: 정확히 3개 파일(`M  ...LOPCombatSystem.cs`, `M  ...LOPGame.cs`, `M  ...GameLifetimeScope.cs`)이 스테이징되고 `Room.unity`/`ConfigureRoomComponent.cs`는 unstaged(` M`)·`Art/`는 untracked(`??`) 유지. `git diff --staged ...LOPGame.cs`로 **RNG 변경만** 들었는지 확인. 의도 밖 내용이면 STOP → BLOCKED 보고.

---

## Task 3: 통합 검증 (동작 보존 확인)

**Files:** (없음 — 실행 검증만)

- [ ] **Step 1: 서버 → 클라 플레이**

서버 Play → 클라 Play로 룸 접속.

- [ ] **Step 2: RNG 경로 동작 확인**

- **전투:** 공격 수행 → 데미지 변동/크리/회피가 정상 발생(이전과 체감 동일). 클·서 콘솔 신규 에러 없음.
- **스폰:** 게임 시작 시 캐릭터 선택(Knight/Archer/...), 몬스터 스폰(종류·위치)이 정상.

Expected: 동작 차이 0, 콘솔 에러 0. 차이 시 — `UnityRandom`이 `UnityEngine.Random.Range`를 같은 인자로 위임하는지, DI가 `UnityRandom`을 주입했는지, 필드명 `rng` 충돌 없는지 확인.

- [ ] **Step 3: 완료**

GameFramework·Server main에 머지됨(push 안 함). 서버 픽스처(Room.unity/ConfigureRoomComponent/Art) 보존. Slice 4 RNG 추상화 완료.

---

## Self-Review (작성자 확인 완료)

- **Spec 커버리지:** IRandom 2메서드(Task 1) / UnityRandom 어댑터(Task 1) / GameFramework 배치(Task 1) / LOPCombatSystem 3곳(Task 2 S1-2) / LOPGame 3곳(Task 2 S3-4) / GameLifetimeScope 등록(Task 2 S5) / 클라 무변경(범위 밖) / 동작 보존(Task 3) / 결정론 deferred(규약) — 전부 대응. ✅
- **Placeholder:** 모든 코드·경로·명령 실제값. `<해당>` 인스턴스만 규약에 해소. ✅
- **타입 일관:** `IRandom.Range(float,float)`/`Range(int,int)`, 필드명 `rng`(양쪽 클래스 동일) — 전 Task 일관. **spec은 필드명을 `random`으로 적었으나 plan은 `rng`로 변경**(LOPGame 로컬 `int random` 충돌 회피, 규약에 근거 명시). ✅
