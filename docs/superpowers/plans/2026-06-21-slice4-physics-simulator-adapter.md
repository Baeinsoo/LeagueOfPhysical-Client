# 물리 시뮬레이션 어댑터 격리 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 클·서 `LOPGameEngine`의 직접 `Physics.Simulate(...)` 호출을 주입된 `IPhysicsSimulator`(GameFramework) 뒤로 격리하고, Unity 어댑터(`UnityPhysicsSimulator`)로 동일 동작을 위임한다.

**Architecture:** GameFramework에 포트 인터페이스 + Unity 어댑터를 추가(기존 `ITickUpdater`/`IGameEngine`와 같은 `Runtime` 어셈블리). 클·서 각 `GameLifetimeScope`가 `IPhysicsSimulator→UnityPhysicsSimulator`(Singleton)를 등록하고, `LOPGameEngine`이 `[Inject]`로 받아 `SimulatePhysics()`에서 호출. 동작 100% 보존(어댑터 = 1-홉 위임).

**Tech Stack:** Unity, C#, VContainer DI, UnityMCP(컴파일 검증). 3 git repo(GameFramework / LeagueOfPhysical-Client / LeagueOfPhysical-Server).

---

## 작업 규약 (모든 Task 공통)

- **테스트 없음(의도적):** 이 슬라이스는 *동작 보존 리팩터*다. `UnityPhysicsSimulator`는 정적 `Physics.Simulate` 래핑이라 단위 테스트 부적합하고, `LOPGameEngine`은 Assembly-CSharp라 asmdef 테스트가 불가하다. 검증은 **컴파일 클린 + 실행 동작 동일**로 한다. seam의 가치는 *향후* 모킹/결정론 물리 주입 가능성이지 이 슬라이스의 테스트가 아니다. (가짜 테스트를 만들지 말 것.)
- **UnityMCP 인스턴스 타게팅(필수):** 이 프로젝트는 클·서 두 에디터가 동시에 붙어 있을 수 있다. `read_console`/`refresh_unity` 등 모든 UnityMCP 호출에 **`unity_instance`를 명시**한다. `mcpforunity://instances`를 읽어 `LeagueOfPhysical-Client`/`LeagueOfPhysical-Server`의 `id`(`Name@hash`)를 얻어 해당 호출에 전달.
- **.meta 커밋:** 새 `.cs` 파일은 Write로 생성 후 `refresh_unity`로 임포트하면 Unity가 `.meta`를 생성한다. `.cs`와 `.meta`를 **함께** `git add`.
- **Git(LOP 패턴):** 각 repo에서 **피처 브랜치** `feature/physics-simulator-adapter`로 작업 → main에 `--no-ff` 머지 → **push 안 함**. main 직접 커밋 금지. 워크트리는 쓰지 않는다(Unity file:-package + 다중 repo라 비실용 — 기존 LOP 슬라이스 관습). 변경 파일만 선택적으로 `git add`해 **미커밋 픽스처 보존**(클: `Assets/Scenes/Room.unity`·`Assets/Resources/UI/UIRoot.prefab`·`ProjectSettings/PackageManagerSettings.asset` / 서: `LOPGame`·`ConfigureRoomComponent` 등).
- **순서 의존:** Task 1(GameFramework)이 먼저 — 타입이 존재·컴파일돼야 클·서가 참조 가능. Task 1에서 **두 에디터 모두 refresh + 컴파일 클린 확인** 후 Task 2/3 진행.

---

## Task 1: GameFramework — IPhysicsSimulator + UnityPhysicsSimulator

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Game/IPhysicsSimulator.cs`
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Game/UnityPhysicsSimulator.cs`
- (Repo: `C:/Users/re5na/workspace/LOP/GameFramework`, asmdef: `baegames.GameFramework.Runtime`, noEngineReferences=false)

- [ ] **Step 1: 인터페이스 파일 생성**

`Runtime/Scripts/Game/IPhysicsSimulator.cs`:

```csharp
namespace GameFramework
{
    /// <summary>
    /// 물리 시뮬레이션 한 스텝을 구동하는 포트 추상. 엔진 본체가 구체 물리 엔진(PhysX 등)에
    /// 직결되지 않도록 주입한다. 클·서 양쪽 동일 구체(<see cref="UnityPhysicsSimulator"/>)를 사용한다.
    /// </summary>
    public interface IPhysicsSimulator
    {
        /// <summary>지정한 delta time만큼 물리 시뮬레이션을 한 스텝 진행한다.</summary>
        void Simulate(float deltaTime);
    }
}
```

- [ ] **Step 2: Unity 어댑터 파일 생성**

`Runtime/Scripts/Game/UnityPhysicsSimulator.cs`:

```csharp
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// Unity 내장 물리(PhysX)로 <see cref="IPhysicsSimulator"/>를 구현하는 어댑터.
    /// <c>SimulationMode.Script</c> 설정은 호출자(LOPGame) 책임 — 이 어댑터는 한 스텝 실행만 위임한다.
    /// </summary>
    public sealed class UnityPhysicsSimulator : IPhysicsSimulator
    {
        public void Simulate(float deltaTime) => Physics.Simulate(deltaTime);
    }
}
```

- [ ] **Step 3: 두 에디터 refresh + 컴파일 검증**

`mcpforunity://instances`에서 클·서 인스턴스 id 확인 후:
- `refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")`
- `refresh_unity(unity_instance="LeagueOfPhysical-Server@<hash>")`
- 각 인스턴스 `editor_state`의 `isCompiling`이 false 될 때까지 대기.
- `read_console(unity_instance=<client>)` / `read_console(unity_instance=<server>)`

Expected: 에러 0. `GameFramework.IPhysicsSimulator` / `GameFramework.UnityPhysicsSimulator` 타입이 양쪽에서 해석됨(file: 패키지라 두 프로젝트가 동시에 인식).

- [ ] **Step 4: 커밋 (GameFramework repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git checkout -b feature/physics-simulator-adapter
git add Runtime/Scripts/Game/IPhysicsSimulator.cs Runtime/Scripts/Game/IPhysicsSimulator.cs.meta Runtime/Scripts/Game/UnityPhysicsSimulator.cs Runtime/Scripts/Game/UnityPhysicsSimulator.cs.meta
git status --short   # 위 4개(.cs+.meta)만 스테이징됐는지 확인
git commit -m "feat(game): IPhysicsSimulator port + UnityPhysicsSimulator adapter

Wrap UnityEngine.Physics.Simulate behind an injectable IPhysicsSimulator
so the engine host is not hard-coupled to PhysX. Lives alongside
ITickUpdater/IGameEngine in baegames.GameFramework.Runtime.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git checkout main
git merge --no-ff feature/physics-simulator-adapter -m "Merge feature/physics-simulator-adapter: IPhysicsSimulator + UnityPhysicsSimulator"
git branch -d feature/physics-simulator-adapter
```

Expected: main에 머지 커밋 생성. push 하지 않음.

---

## Task 2: Client — LOPGameEngine 주입 + 호출부 교체 + DI 등록

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/LOPGameEngine.cs` (inject 필드 추가 13-15 블록, 호출부 80)
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/GameLifetimeScope.cs` (등록 35행 뒤)
- (Repo: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client`)

- [ ] **Step 1: `IPhysicsSimulator` 주입 필드 추가**

`LOPGameEngine.cs`의 기존 `[Inject]` 필드 블록(현재 13-15행)에 한 줄 추가. 변경 전:

```csharp
        [Inject] private GameFramework.World.WorldEventBuffer worldEventBuffer;
        [Inject] private GameFramework.World.WorldEventApplicator worldEventApplicator;
        [Inject] private WorldEventBridge worldEventBridge;
```

변경 후:

```csharp
        [Inject] private GameFramework.World.WorldEventBuffer worldEventBuffer;
        [Inject] private GameFramework.World.WorldEventApplicator worldEventApplicator;
        [Inject] private WorldEventBridge worldEventBridge;
        [Inject] private IPhysicsSimulator physicsSimulator;
```

(`using GameFramework;`이 이미 4행에 있어 `IPhysicsSimulator` 단축 해석됨.)

- [ ] **Step 2: `SimulatePhysics()` 호출부 교체**

`LOPGameEngine.cs`의 `SimulatePhysics()` (현재 76-83행). 변경 전:

```csharp
        private void SimulatePhysics()
        {
            DispatchEvent<BeforePhysicsSimulation>();

            Physics.Simulate((float)tickUpdater.interval);

            DispatchEvent<AfterPhysicsSimulation>();
        }
```

변경 후:

```csharp
        private void SimulatePhysics()
        {
            DispatchEvent<BeforePhysicsSimulation>();

            physicsSimulator.Simulate((float)tickUpdater.interval);

            DispatchEvent<AfterPhysicsSimulation>();
        }
```

- [ ] **Step 3: DI 등록**

`GameLifetimeScope.cs`의 `Configure`, World Core 등록 끝(현재 35행 `builder.Register<WorldEventBridge>(Lifetime.Singleton);`) **바로 뒤**에 추가. 변경 전:

```csharp
            builder.Register<GameFramework.World.WorldEventApplicator>(Lifetime.Singleton);
            builder.Register<WorldEventBridge>(Lifetime.Singleton);
```

변경 후:

```csharp
            builder.Register<GameFramework.World.WorldEventApplicator>(Lifetime.Singleton);
            builder.Register<WorldEventBridge>(Lifetime.Singleton);
            builder.Register<GameFramework.IPhysicsSimulator, GameFramework.UnityPhysicsSimulator>(Lifetime.Singleton);
```

- [ ] **Step 4: refresh + 컴파일 검증 (클라이언트)**

- `refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")`
- `editor_state.isCompiling` false 대기 → `read_console(unity_instance=<client>)`

Expected: 에러 0. (특히 "No registration for IPhysicsSimulator" 류 DI 경고 없음 — Step 3 등록으로 해소.)

- [ ] **Step 5: 커밋 (클라이언트 repo, 픽스처 보존)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git checkout -b feature/physics-simulator-adapter
git add Assets/Scripts/Game/LOPGameEngine.cs Assets/Scripts/Game/GameLifetimeScope.cs
git status --short   # 위 2개만 스테이징, Room.unity/UIRoot.prefab/PackageManagerSettings는 unstaged 유지 확인
git commit -m "refactor(game): inject IPhysicsSimulator into client LOPGameEngine

Swap direct Physics.Simulate for injected physicsSimulator.Simulate at
SimulatePhysics. Register IPhysicsSimulator->UnityPhysicsSimulator in
GameLifetimeScope. Behavior-preserving.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git checkout main
git merge --no-ff feature/physics-simulator-adapter -m "Merge feature/physics-simulator-adapter: client physics adapter injection"
git branch -d feature/physics-simulator-adapter
```

Expected: 머지 커밋 생성, 픽스처 3개는 여전히 미커밋(modified)로 남음. push 안 함.

---

## Task 3: Server — LOPGameEngine 주입 + 호출부 교체 + DI 등록

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/LOPGameEngine.cs` (inject 필드 추가 26행 뒤, 호출부 158)
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/GameLifetimeScope.cs` (등록 27행 뒤)
- (Repo: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server`)

- [ ] **Step 1: `IPhysicsSimulator` 주입 필드 추가**

`LOPGameEngine.cs`의 World Core `[Inject]` 필드 블록(현재 22-26행) 끝에 추가. 변경 전:

```csharp
        [Inject] private GameFramework.World.WorldEventBuffer worldEventBuffer;
        [Inject] private GameFramework.World.WorldEventApplicator worldEventApplicator;
        [Inject] private WireBroadcaster wireBroadcaster;
        [Inject] private WorldEventBridge worldEventBridge;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
```

변경 후:

```csharp
        [Inject] private GameFramework.World.WorldEventBuffer worldEventBuffer;
        [Inject] private GameFramework.World.WorldEventApplicator worldEventApplicator;
        [Inject] private WireBroadcaster wireBroadcaster;
        [Inject] private WorldEventBridge worldEventBridge;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private IPhysicsSimulator physicsSimulator;
```

(`using GameFramework;`이 이미 4행에 있어 단축 해석됨.)

- [ ] **Step 2: `SimulatePhysics()` 호출부 교체**

`LOPGameEngine.cs`의 `SimulatePhysics()` (현재 154-161행). 변경 전:

```csharp
        private void SimulatePhysics()
        {
            DispatchEvent<BeforePhysicsSimulation>();

            Physics.Simulate((float)tickUpdater.interval);

            DispatchEvent<AfterPhysicsSimulation>();
        }
```

변경 후:

```csharp
        private void SimulatePhysics()
        {
            DispatchEvent<BeforePhysicsSimulation>();

            physicsSimulator.Simulate((float)tickUpdater.interval);

            DispatchEvent<AfterPhysicsSimulation>();
        }
```

- [ ] **Step 3: DI 등록**

`GameLifetimeScope.cs`의 `Configure`, 현재 27행 `builder.Register<WorldEventBridge>(Lifetime.Singleton);` **바로 뒤**에 추가. 변경 전:

```csharp
            builder.Register<WireBroadcaster>(Lifetime.Singleton);
            builder.Register<WorldEventBridge>(Lifetime.Singleton);
```

변경 후:

```csharp
            builder.Register<WireBroadcaster>(Lifetime.Singleton);
            builder.Register<WorldEventBridge>(Lifetime.Singleton);
            builder.Register<GameFramework.IPhysicsSimulator, GameFramework.UnityPhysicsSimulator>(Lifetime.Singleton);
```

- [ ] **Step 4: refresh + 컴파일 검증 (서버)**

- `refresh_unity(unity_instance="LeagueOfPhysical-Server@<hash>")`
- `editor_state.isCompiling` false 대기 → `read_console(unity_instance=<server>)`

Expected: 에러 0, DI 경고 없음.

- [ ] **Step 5: 커밋 (서버 repo, 픽스처 보존)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git checkout -b feature/physics-simulator-adapter
git add Assets/Scripts/Game/LOPGameEngine.cs Assets/Scripts/Game/GameLifetimeScope.cs
git status --short   # 위 2개만 스테이징, 서버 로컬 픽스처(LOPGame/ConfigureRoomComponent 등)는 unstaged 유지 확인
git commit -m "refactor(game): inject IPhysicsSimulator into server LOPGameEngine

Swap direct Physics.Simulate for injected physicsSimulator.Simulate at
SimulatePhysics. Register IPhysicsSimulator->UnityPhysicsSimulator in
GameLifetimeScope. Behavior-preserving.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git checkout main
git merge --no-ff feature/physics-simulator-adapter -m "Merge feature/physics-simulator-adapter: server physics adapter injection"
git branch -d feature/physics-simulator-adapter
```

Expected: 머지 커밋 생성, 서버 픽스처는 여전히 미커밋로 남음. push 안 함.

---

## Task 4: 통합 검증 (동작 보존 확인)

**Files:** (없음 — 실행 검증만)

- [ ] **Step 1: 서버 → 클라 플레이**

서버 에디터 Play → 클라 에디터 Play로 룸 접속.

- [ ] **Step 2: 물리 동작 동일 확인**

지상 이동 / 점프 / 공중 낙하 중 점프 / 가만히 있기를 수행하며 관찰:
- 클·서 `read_console`에 신규 에러·경고 없음.
- 이동·점프·낙하·충돌이 교체 전과 **육안 동일**(rubberbanding·발산·물리 멈춤 없음).

Expected: 동작 차이 0. 차이가 보이면 — 어댑터가 `Physics.Simulate(deltaTime)`를 인자 그대로 위임하는지, DI가 `UnityPhysicsSimulator`를 주입했는지(다른 구현 아님), `SimulationMode.Script`가 여전히 `LOPGame`에서 설정되는지 확인.

- [ ] **Step 3: 완료**

세 repo 모두 main에 머지됨(push 안 함). 픽스처 보존 확인. netcode-redesign Slice 4 첫 조각 완료.

---

## Self-Review 체크 (작성자 확인 완료)

- **Spec 커버리지:** 신규 타입(Task 1) / 호출부 교체·DI(Task 2 클·Task 3 서) / 검증(Task 4) — spec의 모든 절 대응. ✅
- **Placeholder:** 모든 코드·경로·커밋 명령 실제값. `<hash>`만 런타임 해석(인스턴스 id) — 규약에 해소법 명시. ✅
- **타입 일관:** `IPhysicsSimulator.Simulate(float deltaTime)` / `UnityPhysicsSimulator` / `physicsSimulator` 필드명 전 Task 일관. ✅
