# Runner 표준화 Slice 6 — 네이밍/네임스페이스 (E·F·H) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 호스트 계층의 마지막 정리 — `UpdateRunner()`를 `IRunner` 계약에서 빼 내부화(E), UI 용어 `IGamePresenter`/`MonoGamePresenter`를 GF에서 삭제하고 클라 `LOPGamePresenter`를 `LOPGameSceneCoordinator`로 rename(F), 호스트 클러스터 9파일을 `GameFramework.Runner` 네임스페이스로 그룹핑(H).

**Architecture:** 순수 네이밍/구조 정리 — **로직 변경 0**. 게이트는 새 유닛 테스트가 아니라 "여전히 컴파일 + EditMode 그린 + 플레이 스모크 동일 동작". 근거·잠금 결정은 umbrella spec `docs/superpowers/specs/2026-07-24-runner-standardization-refactor-design.md`의 발견 E/F/H + Open Decisions.

**Tech Stack:** Unity(3레포: GameFramework `file:` 공유 패키지, LeagueOfPhysical-Client/Server), C#, VContainer, MessagePipe.

## Global Constraints

- **로직 불변**: 시뮬/넷코드/UI 동작을 바꾸지 않는다. 이름·계약·네임스페이스만.
- **크로스-레포 원자성**: GF 시그니처/네임스페이스 변경은 클·서 use-side를 **같은 논리 단위로 동시 수정**한다. 각 태스크는 관련 레포 전부 컴파일 그린을 게이트로.
- **서버 로컬 픽스처 커밋 금지**: `Assets/DefaultVolumeProfile.asset`, `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`, `Assets/Scripts/Game/GameRuleSystem.cs`. `git add -A`/`git add .` 절대 금지 — 명시 파일만 스테이징.
- **.meta**: Unity 생성 `.meta`는 함께 커밋. 파일 rename 시 `.cs`와 `.cs.meta`를 **함께 이동**(GUID 보존 — 씬 참조 안 깨지게). `.meta`를 직접 만들지 않는다.
- **Git**: main 직접 커밋 금지. 각 레포 피처 브랜치 `refactor/runner-slice6-naming` → `--no-ff` 머지.
- **네임스페이스 이름(H) = `GameFramework.Runner`** (잠금). **Presenter rename(F) = `LOPGameSceneCoordinator`** (잠금).
- **C# 이름 해석 사실**: `GameFramework.Runner`의 타입은 바깥 `GameFramework`(루트) 타입을 using 없이 본다. 그래서 이동하는 GF 9파일은 `namespace` 줄만 바뀌고, use-side(`namespace LOP` / `GameFramework.Tests`)만 `using GameFramework.Runner;` 추가.

---

### Task 1: E — `UpdateRunner()` 내부화

`IRunner` 계약에서 `UpdateRunner()`를 제거하고 `RunnerBase`에서 `protected abstract`로 내려 캡슐화 누수를 막는다. 외부 호출자는 없음(`RunnerBase.OnTick` 내부 1곳뿐 — grep 확인됨).

**Files:**
- Modify: `GameFramework/Runtime/Scripts/Game/IRunner.cs:20`
- Modify: `GameFramework/Runtime/Scripts/Game/RunnerBase.cs:115`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/LOPRunner.cs:91`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs:107`

**Interfaces:**
- Consumes: 없음(제거만).
- Produces: `RunnerBase.UpdateRunner`가 `protected abstract`가 됨 — 파생 `LOPRunner`는 `protected override`.

- [ ] **Step 1: `IRunner`에서 메서드 제거**

`IRunner.cs`에서 이 줄 삭제:
```csharp
        void UpdateRunner();
```
(주변 `Run`/`Stop`/`RegisterSystem`/`UnregisterSystem`는 유지.)

- [ ] **Step 2: `RunnerBase`에서 `protected abstract`로**

`RunnerBase.cs:115`:
```csharp
        public abstract void UpdateRunner();
```
→
```csharp
        protected abstract void UpdateRunner();
```
(내부 호출 `OnTick`의 `UpdateRunner();`(L112)는 그대로 — 같은 클래스라 접근 가능.)

- [ ] **Step 3: 클라 `LOPRunner` override 접근자 낮춤**

클라 `LOPRunner.cs:91`:
```csharp
        public override void UpdateRunner()
```
→
```csharp
        protected override void UpdateRunner()
```

- [ ] **Step 4: 서버 `LOPRunner` override 접근자 낮춤**

서버 `LOPRunner.cs:107`: 동일하게 `public override` → `protected override`.

- [ ] **Step 5: 컴파일 게이트**

UnityMCP `refresh_unity`(client) 후 `read_console`(client, unity_instance 클라 핀) — 에러 0 확인. 서버 인스턴스 콘솔도 확인. GF EditMode(`baegames.GameFramework.Runtime.Tests`) `run_tests` → 그린(90/90 유지).

- [ ] **Step 6: 커밋(각 레포)**

GF:
```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework add Runtime/Scripts/Game/IRunner.cs Runtime/Scripts/Game/RunnerBase.cs
git -C C:/Users/re5na/workspace/LOP/GameFramework commit -m "refactor(runner): UpdateRunner를 IRunner에서 제거·protected 내부화 (E)"
```
클라:
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add Assets/Scripts/Game/LOPRunner.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "refactor(runner): UpdateRunner protected override (E)"
```
서버(명시 파일만):
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Game/LOPRunner.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server commit -m "refactor(runner): UpdateRunner protected override (E)"
```

---

### Task 2: F — GF Presenter 삭제 + 클라 `LOPGameSceneCoordinator` rename

`IGamePresenter<T>`/`MonoGamePresenter<T>`는 UI/MVP 용어(자체 문서가 비-UI 사용 경고)이고 클라 전용. GF에서 삭제하고, 클라 소비자를 베이스 없는 평범한 씬 코디네이터로 rename.

**Files:**
- Delete: `GameFramework/Runtime/Scripts/Game/IGamePresenter.cs` (+`.meta`)
- Delete: `GameFramework/Runtime/Scripts/Game/MonoGamePresenter.cs` (+`.meta`)
- Rename: `LeagueOfPhysical-Client/Assets/Scripts/Game/LOPGamePresenter.cs` → `LOPGameSceneCoordinator.cs` (+`.meta`, GUID 보존)
- Modify: 위 파일 본문(클래스명·베이스·`runner` 필드·주석)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/GameLifetimeScope.cs:76` (주석 내 `LOPGamePresenter` → `LOPGameSceneCoordinator`)

**Interfaces:**
- Consumes: `LOPRunner`, `RunnerState`, `CameraController`, `IPlayerContext`, `IWindowManager`, `GameLoadingView`, `GameInfoToC`, `GlobalMessagePipe` (기존 그대로).
- Produces: `LOPGameSceneCoordinator` (씬 배치 MonoBehaviour, `[SceneInjectMonoBehaviour]`). 외부에서 타입으로 참조하는 코드 없음(씬 컴포넌트 + 주석뿐).

- [ ] **Step 1: GF Presenter 2파일 삭제(파일+meta)**

```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework rm Runtime/Scripts/Game/IGamePresenter.cs Runtime/Scripts/Game/IGamePresenter.cs.meta Runtime/Scripts/Game/MonoGamePresenter.cs Runtime/Scripts/Game/MonoGamePresenter.cs.meta
```

- [ ] **Step 2: 클라 파일 rename(cs + meta 함께 — GUID 보존)**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client mv Assets/Scripts/Game/LOPGamePresenter.cs Assets/Scripts/Game/LOPGameSceneCoordinator.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client mv Assets/Scripts/Game/LOPGamePresenter.cs.meta Assets/Scripts/Game/LOPGameSceneCoordinator.cs.meta
```
(`.meta`의 GUID는 그대로 → 씬의 GameObject 컴포넌트 참조 유지.)

- [ ] **Step 3: rename된 파일 본문 재작성**

`LOPGameSceneCoordinator.cs` 전체를 아래로 교체(베이스 제거 → 평범한 MonoBehaviour, `runner`를 자체 필드로):
```csharp
using Cysharp.Threading.Tasks;
using GameFramework;
using LOP.UI;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace LOP
{
    [SceneInjectMonoBehaviour]
    public class LOPGameSceneCoordinator : MonoBehaviour
    {
        [Inject]
        private CameraController cameraController;

        [Inject]
        private IPlayerContext playerContext;

        [Inject]
        private IWindowManager windowManager;

        private LOPRunner runner;

        private UIView gameLoadingView;
        private System.IDisposable gameInfoSubscription;

        private void Awake()
        {
            // LOPRunner은 이 코디네이터 GameObject의 자식("LOPGameEngine")에 있다.
            runner = GetComponentInChildren<LOPRunner>();
            runner.onGameStateChanged += OnGameStateChanged;

            // MonoBehaviour가 Awake에서 구독 — 주입 타이밍 의존을 피해 GlobalMessagePipe로 구독(구 정적 버스와 동형).
            gameInfoSubscription = GlobalMessagePipe.GetSubscriber<GameInfoToC>().Subscribe(OnGameInfoToC);
        }

        private void OnDestroy()
        {
            runner.onGameStateChanged -= OnGameStateChanged;
            runner = null;

            gameInfoSubscription?.Dispose();
        }

        private void OnGameStateChanged(RunnerState gameState)
        {
            switch (gameState)
            {
                case RunnerState.Initialized:
                    gameLoadingView = windowManager.Open<GameLoadingView>();
                    break;

                case RunnerState.Playing:
                    if (gameLoadingView != null)
                    {
                        windowManager.Close(gameLoadingView);
                        gameLoadingView = null;
                    }
                    break;
            }
        }

        private async void OnGameInfoToC(GameInfoToC gameInfoToC)
        {
            await UniTask.WaitUntil(() => playerContext.actor != null && playerContext.actor.visualGameObject != null);

            cameraController.SetTarget(playerContext.actor.visualGameObject.transform);
        }
    }
}
```
(변경점: `class LOPGamePresenter : MonoGamePresenter<LOPRunner>` → `class LOPGameSceneCoordinator : MonoBehaviour`; 상속 `runner` 프로퍼티 → `private LOPRunner runner;` 필드; 주석 "프레젠터"→"코디네이터". 나머지 로직 동일.)

- [ ] **Step 4: `GameLifetimeScope` 주석 갱신**

클라 `GameLifetimeScope.cs:76` 주석의 `LOPGamePresenter` → `LOPGameSceneCoordinator`:
```csharp
            // actor의 유일한 소비자(LOPGameSceneCoordinator)는 폴링이라 순서를 타지 않는다.
```

- [ ] **Step 5: 삭제 후 stale 참조 정리 + 컴파일 게이트**

GF `.cs`/`.meta` 삭제라 클·서에 stale `CS2001` 가능 → 각 인스턴스 `refresh_unity`(scope=all, force) 후 `read_console`로 에러 0 확인. 클라: 새 클래스명 컴파일 통과 + 씬 컴포넌트 참조 안 깨졌는지(`find_gameobjects`로 코디네이터 GameObject의 스크립트 "missing" 아님) 확인. GF EditMode 그린.

- [ ] **Step 6: 커밋(GF, 클라)**

GF:
```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework commit -m "refactor(runner): IGamePresenter/MonoGamePresenter 삭제 (F)"
```
클라(명시 파일만 — rename된 cs/meta + GameLifetimeScope):
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add Assets/Scripts/Game/LOPGameSceneCoordinator.cs Assets/Scripts/Game/LOPGameSceneCoordinator.cs.meta Assets/Scripts/Game/LOPGamePresenter.cs Assets/Scripts/Game/LOPGamePresenter.cs.meta Assets/Scripts/Game/GameLifetimeScope.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "refactor(runner): LOPGamePresenter → LOPGameSceneCoordinator (F)"
```
(`git mv`로 스테이징된 이전 경로 + 새 경로 모두 add에 포함 — rename이 커밋에 온전히 기록되게.)

---

### Task 3: H — 호스트 클러스터를 `GameFramework.Runner` 네임스페이스로

호스트 9파일을 `GameFramework` 루트 → `GameFramework.Runner`로 옮겨 `.World`/`.Netcode`와 짝을 맞춘다. GF 파일은 `namespace` 줄만; use-side는 `using GameFramework.Runner;` 추가.

**Files:**
- Modify (namespace 줄만): GF `Runtime/Scripts/Game/` 아래 9개 — `IRunner.cs`, `RunnerBase.cs`, `RunnerState.cs`, `ITickUpdater.cs`, `TickUpdaterBase.cs`, `TickCatchUp.cs`, `ITickSystem.cs`, `IMapLoader.cs`, `IGameFactory.cs`
- Modify (using 추가): use-side 파일들 — 아래 목록
- Modify (using 추가): GF 테스트 `Tests/Runtime/TickCatchUpTests.cs`

**use-side 파일(grep 확정 — 각 파일에 `using GameFramework.Runner;` 추가):**

클라(16):
```
Assets/Scripts/Game/TickSystems/WorldEventDrainSystem.cs
Assets/Scripts/Game/TickSystems/ReconcileSystem.cs
Assets/Scripts/Game/TickSystems/PhysicsSimulationSystem.cs
Assets/Scripts/Game/TickSystems/LocalSnapshotSystem.cs
Assets/Scripts/Game/TickSystems/DespawnFlushSystem.cs
Assets/Scripts/Game/LOPRunner.cs
Assets/Scripts/Game/GameLifetimeScope.cs
Assets/Scripts/Netcode/LocalEntityInterpolator.cs
Assets/Scripts/Game/PlayerInputManager.cs
Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs
Assets/Scripts/Netcode/LOPTickUpdater.cs
Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs
Assets/Scripts/Room/LOPRoom.cs
Assets/Scripts/Game/LOPGameSceneCoordinator.cs   ← Task 2에서 rename된 파일(원 grep은 LOPGamePresenter.cs)
Assets/Scripts/Room/RoomLifetimeScope.cs
Assets/Scripts/Game/LOPGameFactory.cs
```
서버(22):
```
Assets/Scripts/Game/TickSystems/WorldEventDrainSystem.cs
Assets/Scripts/Game/TickSystems/UserEntitySnapshotSystem.cs
Assets/Scripts/Game/TickSystems/ServerInputSystem.cs
Assets/Scripts/Game/TickSystems/PhysicsSimulationSystem.cs
Assets/Scripts/Game/TickSystems/InputTimingFeedbackSystem.cs
Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs
Assets/Scripts/Game/TickSystems/DespawnFlushSystem.cs
Assets/Scripts/Game/TickSystems/DeathResolveSystem.cs
Assets/Scripts/Game/LOPRunner.cs
Assets/Scripts/Game/GameLifetimeScope.cs
Assets/Scripts/Game/MessageHandler/GameInfoMessageHandler.cs
Assets/Scripts/Entity/LOPAIController.cs
Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs
Assets/Scripts/UI/DebugHud/DebugHudHost.cs
Assets/Scripts/Netcode/LOPTickUpdater.cs
Assets/Scripts/Netcode/WorldEventSink.cs
Assets/Scripts/Game/MessageHandler/GameInputMessageHandler.cs
Assets/Scripts/AI/EnemyBrain.cs
Assets/Scripts/Room/LOPRoom.cs
Assets/Scripts/Game/GameRuleSystem.cs   ← 로컬 픽스처: 파일 편집은 하되 커밋 스테이징에서 제외
Assets/Scripts/Room/RoomLifetimeScope.cs
Assets/Scripts/Game/LOPGameFactory.cs
```
Shared(1): `Runtime/Scripts/Game/AddressablesMapLoader.cs`

- [ ] **Step 1: GF 9파일 namespace 변경**

각 파일의 `namespace GameFramework` → `namespace GameFramework.Runner`. (using 변경 없음 — 바깥 walk로 루트 타입 보임. `RunnerBase.cs`의 `using GameFramework.Netcode;`는 유지.)

- [ ] **Step 2: use-side `using` 추가**

위 목록 각 파일의 using 블록에 `using GameFramework.Runner;` 한 줄 추가(기존 `using GameFramework;`가 있으면 그 아래에). 이미 다른 곳에서 이 타입을 못 보던 파일은 이제 이 using으로 해소.

- [ ] **Step 3: GF 테스트 using 추가**

`GameFramework/Tests/Runtime/TickCatchUpTests.cs`(`namespace GameFramework.Tests`)에 `using GameFramework.Runner;` 추가 — `TickCatchUp`이 더는 바깥 walk로 안 보임.

- [ ] **Step 4: 컴파일 + EditMode 게이트**

GF `refresh_unity`(scope=all, force). 클·서 각 인스턴스 `read_console` 에러 0. GF EditMode `run_tests` → 그린(90/90). 누락된 `using`이 있으면 콘솔 `CS0246`(type not found)로 드러남 → 해당 파일에 추가.

- [ ] **Step 5: 플레이 스모크**

클·서 2에디터 룸 접속 → 이동/전투 정상(로직 불변 확인). 로딩 스크린 여닫힘·카메라 팔로우(Task 2 코디네이터) 정상.

- [ ] **Step 6: 커밋(3레포, 서버는 픽스처 제외)**

GF:
```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework add Runtime/Scripts/Game Tests/Runtime/TickCatchUpTests.cs
git -C C:/Users/re5na/workspace/LOP/GameFramework commit -m "refactor(runner): 호스트 클러스터 → GameFramework.Runner 네임스페이스 (H)"
```
클라:
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add <위 클라 16파일>
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "refactor(runner): using GameFramework.Runner (H)"
```
서버(**GameRuleSystem.cs 제외** — 나머지 21파일만 명시 add):
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server add <위 서버 목록에서 GameRuleSystem.cs 뺀 21파일>
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server commit -m "refactor(runner): using GameFramework.Runner (H)"
```
> ⚠️ 서버 `GameRuleSystem.cs`는 로컬 테스트 픽스처라 **커밋 금지**. H의 using 추가로 편집은 되지만(컴파일 위해 필요), 스테이징에서 제외한다. 커밋 후에도 working tree에 uncommitted로 남는 게 정상. `ServerInputSystem` 등 다른 서버 파일은 정상 커밋.

---

## Self-Review

- **Spec coverage:** E(§77)·F(§78)·H(§80) + Open Decisions(네임스페이스=`GameFramework.Runner`, presenter=`LOPGameSceneCoordinator`) 전부 태스크로 커버. G/기타는 이전 슬라이스 완료.
- **Placeholder scan:** 없음 — 모든 스텝에 구체 경로·정확 코드.
- **Type consistency:** `LOPGameSceneCoordinator`(F) 이름을 H의 클라 목록에서 rename 반영. `protected abstract UpdateRunner`(E)와 양쪽 `protected override` 일치. `GameFramework.Runner` 토큰 전 태스크 동일.
- **로컬 픽스처:** 서버 `GameRuleSystem.cs`는 H에서 편집되지만 커밋 제외 명시.
