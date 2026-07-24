# Runner Slice 5-A — 리플렉션 이벤트버스 → ITickSystem registry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`).

**Goal:** `RunnerBase`의 리플렉션 틱 이벤트버스(`AddListener`/`RemoveListener`/`DispatchEvent<T>` + `[RunnerListen]`)를 타입드 `ITickSystem` 페이즈 registry로 교체한다. 인라인 스텝은 그대로(5-B).

**Architecture:** GF에 `interface ITickSystem { void Tick(long tick, float dt); }` + `RunnerBase`에 페이즈키(타입) registry(`RegisterSystem<TPhase>`/`UnregisterSystem`/`protected RunPhase<TPhase>`). 리플렉션 대신 타입드 등록, 런타임 add/remove 지원(동적 리스너 필수). 4개 리스너(클: PlayerInputManager/LocalEntityInterpolator, 서: LOPAIController/GameInfoMessageHandler)가 `ITickSystem` 구현 + 등록으로 전환. 죽은 페이즈 4개 삭제. 이 registry가 5-B(인라인 스텝을 시스템으로 추가)의 토대.

**Tech Stack:** Unity, C#, VContainer, GameFramework.

## Global Constraints

- **원자적 3-레포**: 리플렉션 버스 삭제는 모든 소비처가 새 API로 옮겨져야 컴파일. GF 공유라 모든 편집 후 양쪽 컴파일 검증 → 레포별 커밋.
- **런타임 add/remove 유지**: LOPAIController(AI당)·LocalEntityInterpolator(내 캐릭)는 `RegisterSystem`/`UnregisterSystem`을 Start/Cleanup에서 호출. PlayerInputManager·GameInfoMessageHandler는 정적(등록만 or 스코프 teardown 해제).
- **동작 변화 0**: 각 리스너 로직 불변, 호출 시점 동일. 죽은 페이즈(구독자 0)는 삭제(무해).
- **커밋 스테이징 명시 파일만**. `git add -A`/`.` 금지. **서버 픽스처**(`DefaultVolumeProfile.asset`, `ConfigureRoomComponent.cs`, `GameRuleSystem.cs`) 커밋 금지 — `git status --short` 확인.
- **새 단위 테스트 없음**(배선) — 검증 = 양쪽 컴파일 + GF EditMode 무회귀 + 머지 후 플레이 스모크. 클라 코드는 워크트리서 컴파일 불가(머지 후 게이트).
- **5-B 토대 규격**: `ITickSystem`/registry가 5-B에서 인라인 스텝 추가에 그대로 쓰이도록 — 버리는 코드 0.

## Repos & Branches (생성됨)

- GameFramework `C:\Users\re5na\workspace\LOP\GameFramework` — `refactor/runner-slice5a` (base 124da69)
- Server `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server` — `refactor/runner-slice5a` (base 4b91d7d)
- Client — 이 워크트리 `worktree-slice5a`(최신)

Unity: 클라 `LeagueOfPhysical-Client@de70658b9450cbb4`, 서버 `LeagueOfPhysical-Server@f99391fa2dbaaf3c`.

---

### Task 1: 리플렉션 버스 → ITickSystem registry (원자적, 3-레포)

#### 파트 A — GameFramework

- [ ] **Step 1: `ITickSystem.cs` 생성**

Create `Runtime/Scripts/Game/ITickSystem.cs`:
```csharp
namespace GameFramework
{
    /// <summary>
    /// 틱 파이프라인의 한 스텝. Runner가 페이즈별로 등록된 순서대로 Tick을 호출한다.
    /// (구 리플렉션 이벤트버스를 대체 — 타입드 등록, 런타임 add/remove 지원.)
    /// </summary>
    public interface ITickSystem
    {
        void Tick(long tick, float deltaTime);
    }
}
```

- [ ] **Step 2: `RunnerBase.cs` — 리플렉션 버스 제거 + registry 추가**

`RunnerBase.cs`에서:
- 필드 `private Dictionary<Type, Dictionary<object, Action>> listenerMap = ...;` **삭제**.
- 메서드 `AddListener(object)`, `RemoveListener(object)`, `DispatchEvent<T>()` **3개 삭제**.
- `using System.Reflection;` 가 다른 데서 안 쓰이면 삭제.
- 아래 registry 멤버 추가(예: tickUpdater 프로퍼티 근처):
```csharp
        private readonly Dictionary<Type, List<ITickSystem>> _tickSystems = new Dictionary<Type, List<ITickSystem>>();

        public void RegisterSystem<TPhase>(ITickSystem system)
        {
            var key = typeof(TPhase);
            if (_tickSystems.TryGetValue(key, out var list) == false)
            {
                list = new List<ITickSystem>();
                _tickSystems[key] = list;
            }
            list.Add(system);
        }

        public void UnregisterSystem(ITickSystem system)
        {
            foreach (var list in _tickSystems.Values)
            {
                list.Remove(system);
            }
        }

        // 페이즈에 등록된 시스템을 실행. 역방향 순회 = Tick 중 자기 해제(엔티티 사망→Cleanup)해도 안전.
        // 페이즈 내 순서엔 의존하지 않는다(각 페이즈 소비자 ≤1종 또는 순서 무관 AI).
        protected void RunPhase<TPhase>(long tick, float deltaTime)
        {
            if (_tickSystems.TryGetValue(typeof(TPhase), out var list) == false)
            {
                return;
            }
            for (int i = list.Count - 1; i >= 0; i--)
            {
                list[i].Tick(tick, deltaTime);
            }
        }
```

- [ ] **Step 3: `IRunner.cs` — 인터페이스 멤버 교체**
  - `void AddListener(object listener);` / `void RemoveListener(object listener);` / `void DispatchEvent<T>();` **3줄 삭제**.
  - 추가: `void RegisterSystem<TPhase>(ITickSystem system);` / `void UnregisterSystem(ITickSystem system);` (RunPhase는 protected라 인터페이스에 안 올림).

- [ ] **Step 4: `RunnerListenAttribute.cs` 삭제** — Step 12의 `git rm`으로 `.cs`+`.meta`.

#### 파트 B — Client (이 워크트리)

- [ ] **Step 5: `Game/Event.LOPRunner.Update.cs`** — 클라는 `ProcessInput`, `End`만 남기고 나머지(`Begin`, `BeforeEntityUpdate`, `AfterEntityUpdate`, `BeforePhysicsSimulation`, `AfterPhysicsSimulation`) 구조체 삭제.

- [ ] **Step 6: `Game/LOPRunner.cs`** — `DispatchEvent<...>()` → `RunPhase<...>()` (죽은 페이즈는 호출째 삭제):
  - `BeginUpdate()`의 `DispatchEvent<Begin>()` → **호출 삭제**(클라 Begin 리스너 0; `BeginUpdate` 본문이 비면 메서드도 정리 가능).
  - `ProcessInput()`의 `DispatchEvent<ProcessInput>()` → `RunPhase<ProcessInput>(tickUpdater.tick, (float)tickUpdater.interval)`.
  - `UpdateEntity()`의 `DispatchEvent<BeforeEntityUpdate>()`/`<AfterEntityUpdate>()` → **삭제**(빈 메서드 정리 가능).
  - `SimulatePhysics()`의 `DispatchEvent<BeforePhysicsSimulation>()`/`<AfterPhysicsSimulation>()` → **삭제**(사이의 PushMotion 루프·Simulate는 유지).
  - `EndUpdate()`의 `DispatchEvent<End>()` → `RunPhase<End>(tickUpdater.tick, (float)tickUpdater.interval)` (RecordLocalSnapshot 뒤·FlushDespawns 앞 위치 유지).

- [ ] **Step 7: `Game/PlayerInputManager.cs`**
  - 클래스에 `ITickSystem` 구현 추가(`: ITickSystem`).
  - `[RunnerListen(typeof(ProcessInput))] private void ProcessInput()` → `public void Tick(long tick, float deltaTime)` (특성 삭제, 본문 유지; 기존에 tick이 필요하면 `tick` 파라미터 사용).
  - ctor의 `this.runner.AddListener(this);` → `this.runner.RegisterSystem<ProcessInput>(this);`.

- [ ] **Step 8: `Netcode/LocalEntityInterpolator.cs`**
  - `: MonoBehaviour, ICleanup` 에 `, ITickSystem` 추가.
  - `[RunnerListen(typeof(End))] private void OnEnd()` → `public void Tick(long tick, float deltaTime)` (특성 삭제, 본문 유지; `interval`이 필요하면 `deltaTime` 사용).
  - `Start()`의 `runner.AddListener(this);` → `runner.RegisterSystem<End>(this);`.
  - `Cleanup()`의 `runner.RemoveListener(this);` → `runner.UnregisterSystem(this);`.

#### 파트 C — Server

- [ ] **Step 9: `Game/Event.LOPRunner.Update.cs`** — 서버는 `Begin`, `End`만 남기고 나머지 4개 삭제.

- [ ] **Step 10: `Game/LOPRunner.cs`** — `DispatchEvent` → `RunPhase`/삭제:
  - `BeginUpdate()`의 `DispatchEvent<Begin>()` → `RunPhase<Begin>(tickUpdater.tick, (float)tickUpdater.interval)`.
  - `UpdateEntity()`의 `Before/AfterEntityUpdate` 디스패치 → **삭제**.
  - `SimulatePhysics()`의 `Before/AfterPhysicsSimulation` 디스패치 → **삭제**(PushMotion·Simulate 유지).
  - `EndUpdate()`의 `DispatchEvent<End>()` → `RunPhase<End>(tickUpdater.tick, (float)tickUpdater.interval)` (End 위치 = EndUpdate 맨 앞, 기존과 동일).

- [ ] **Step 11: 서버 소비자 2개**
  - `Entity/LOPAIController.cs`: `: MonoBehaviour, ICleanup` → `, ITickSystem` 추가. `[RunnerListen(typeof(Begin))] private void OnUpdateBegin()` → `public void Tick(long tick, float deltaTime)` (본문 `brain.Think(worldEntity, runner.tickUpdater.deltaTime)` 유지 — 또는 `deltaTime` 파라미터 사용). `Start()`의 `runner.AddListener(this)` → `runner.RegisterSystem<Begin>(this)`; `Cleanup()`의 `runner.RemoveListener(this)` → `runner.UnregisterSystem(this)`.
  - `Game/MessageHandler/GameInfoMessageHandler.cs`: `: MessageHandlerBase` → `: MessageHandlerBase, ITickSystem`. `[RunnerListen(typeof(End))] private void OnEnd()` → `public void Tick(long tick, float deltaTime)` (본문 유지). `Subscribe()`의 `runner.AddListener(this)` → `runner.RegisterSystem<End>(this)`; `Dispose()`의 `runner.RemoveListener(this)` → `runner.UnregisterSystem(this)`.

#### 삭제 + 검증 + 커밋

- [ ] **Step 12: `RunnerListenAttribute` 삭제**
  ```bash
  git -C "C:/Users/re5na/workspace/LOP/GameFramework" rm Runtime/Scripts/Game/RunnerListenAttribute.cs Runtime/Scripts/Game/RunnerListenAttribute.cs.meta
  ```

- [ ] **Step 13: 잔재 확인** — 3레포에 `RunnerListen`/`DispatchEvent`/`AddListener`/`RemoveListener`(Runner 관련) 이 0:
  ```bash
  grep -rn "RunnerListen\|DispatchEvent\|\.AddListener\|\.RemoveListener" \
    "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/slice5a/Assets/Scripts" \
    "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts" \
    "C:/Users/re5na/workspace/LOP/GameFramework/Runtime" | grep -v "Mirror"
  # 기대: 0 (Mirror의 AddListener 등 무관 항목 제외)
  ```

- [ ] **Step 14: 컴파일 검증(양쪽)** — Runner.cs 삭제 아님(수정만)이나 attribute 삭제 있으니 scope=all 권장.
  - 서버: `refresh_unity(mode="force", scope="all", compile="request", unity_instance="...Server...")` → `read_console(types=["error"])` 0.
  - 클라: 동일(...client...) → 0. (⚠️ 워크트리 클라 편집은 여기서 컴파일 안 됨 → 서버 그린 + 정적 grep, 실제 클라는 머지 후 게이트.)

- [ ] **Step 15: GF EditMode 무회귀** — `run_tests(mode="EditMode", assembly_names=["baegames.GameFramework.Runtime.Tests"], unity_instance="...Client...")` PASS.

- [ ] **Step 16~18: 커밋 (레포별, 명시 파일만)**
  - GF: `git add Runtime/Scripts/Game/ITickSystem.cs Runtime/Scripts/Game/ITickSystem.cs.meta Runtime/Scripts/Game/RunnerBase.cs Runtime/Scripts/Game/IRunner.cs` (RunnerListenAttribute 삭제는 Step 12서 스테이징). commit.
  - Client(워크트리): `git add Assets/Scripts/Game/Event.LOPRunner.Update.cs Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Game/PlayerInputManager.cs Assets/Scripts/Netcode/LocalEntityInterpolator.cs`. commit.
  - Server: `git add Assets/Scripts/Game/Event.LOPRunner.Update.cs Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Entity/LOPAIController.cs Assets/Scripts/Game/MessageHandler/GameInfoMessageHandler.cs`. `git status --short`로 픽스처 3개 미스테이징 확인. commit.
  - 각 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

## 완료 후

- 최종 whole-branch 리뷰(3-레포). `finishing-a-development-branch` 로컬 `--no-ff` 머지 → 머지 후 main 클라 에디터 컴파일(scope=all force) + GF EditMode + 플레이 스모크(입력/AI/보간/GameInfo 정상 = 4개 훅 다 작동). 서버 픽스처 보존.

## 스코프 밖 (5-B)

인라인 스텝(reconcile·world.Tick·physics·이벤트드레인·스냅샷·브로드캐스트·death·입력타이밍)을 `ITickSystem`으로 추출해 같은 registry에 페이즈 추가 → `UpdateRunner`를 `RunPhase` 순회로 축소 = god-object 완전 해체. 네이밍(마커 구조체 → 명확한 페이즈명 등)=슬라이스 6.
