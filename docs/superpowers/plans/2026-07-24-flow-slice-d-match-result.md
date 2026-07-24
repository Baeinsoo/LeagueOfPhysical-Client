# Flow Slice D — 결과 화면(매치 종료 통보 경로) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 서버가 매치 종료를 클라에 통보하고, 클라가 로비로 돌아와 결과 화면을 한 번 띄운 뒤 로비 홈으로 복귀하는 경로를 만든다.

**Architecture:** 서버 `LOPRunner.EndMatch()` → `LOPRoom`이 전 세션에 `MatchEndedToC` 브로드캐스트 → 클라 `MatchEndedMessageHandler`가 결과를 Root 스코프 `MatchResultDataStore`에 남기고 클라 `LOPRunner.EndMatch()` 호출 → 기존 `LOPRoom.OnGameStateChanged`의 `case GameOver`가 로비 씬을 로드 → 로비의 `FrontEndCoordinator`가 대기 중인 결과를 보고 `MatchResultView`를 연다. 설계·근거: `docs/superpowers/specs/2026-07-24-flow-slice-d-match-result-design.md`.

**Tech Stack:** Protobuf(LOP-Shared 생성 파이프라인), Mirror(`ISession.Send`), VContainer(엔트리포인트·DataStore), MessagePipe(`ISubscriber`/`IPublisher`), Unity UI Toolkit(UXML/USS + `UIViewCatalog`).

## Global Constraints

- **3개 레포를 건드린다**: `LeagueOfPhysical-Shared`(와이어), `LeagueOfPhysical-Server`(송신), `LeagueOfPhysical-Client`(수신·화면). **각 레포마다 피처 브랜치** `feature/flow-slice-d-match-result`에서 작업한다. main 직접 커밋 금지.
- **어휘 규약(spec)**: 새로 만드는 LOP 도메인 이름은 **match**를 쓴다(`MatchEndedToC`, `EndMatch()`, `MatchResult*`). 기존 러너 상태 family(`None/Initializing/Initialized/Preparing/Prepared/Playing/Paused/GameOver`)는 **이름을 바꾸지 않는다**. 두 어휘가 만나는 곳은 `EndMatch()` 본문의 `gameState = GameOver.State` 한 줄뿐이다.
- **새 .cs/.uxml/.uss는 Unity가 생성한 `.meta`와 함께 커밋**(새 폴더의 폴더 `.meta` 포함). `.meta`를 직접 만들지 않는다.
- **테스트 전략 = 컴파일 체크 + 플레이테스트.** 클라·서버 코드는 전부 Assembly-CSharp이라 asmdef 테스트 어셈블리가 참조할 수 없다. 이번 슬라이스가 추가하는 로직은 (a) 스토어 대입/Clear (b) 메시지 핸들러(Unity·DI 결합) (c) 뷰 바인더뿐이라 순수 커널로 떼어낼 것이 없다 — 테스트 가능한 패키지(GameFramework/LOP-Shared)로 옮길 만한 알고리즘이 없다.
- **UnityMCP 대상 고정**: 클라 `unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4"`, 서버 `unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c"`. 해시가 바뀌었으면 `mcpforunity://instances`로 다시 확인한다(에러 메시지에도 실제 id가 노출된다).
- **와이어는 reliable 채널**(`ISession.Send`의 기본값). 유실되면 클라가 매치에 갇힌다 — `reliable: false`를 넘기지 않는다.
- **송신은 룸 상태 갱신보다 먼저**. 룸을 Closed로 바꾸면 클라 연결이 끊겨 메시지를 못 받는다.

## 범위 밖 (후속)

- 점수·순위·보상 표시(게임 모드 규칙 확정 후 별도 spec), 종료 사유 구분, 중도 이탈·서버 다운 등 비정상 종료.
- Slice A(앱 FSM 씬 전환 일원화) — 이 경로는 나중에 `MatchEnded` 앱 이벤트로 이관된다.
- 러너 상태 family 리네임.

---

## 파일 구조

| 레포 | 파일 | 책임 | 변경 |
|---|---|---|---|
| Shared | `Protos/MatchEndedToC.proto` | 매치 종료 통보(필드 없음) | 신규 |
| Shared | `Runtime.Generated/Scripts/Protobuf/MatchEndedToC*.cs`, `MessageIds.cs`, `MessageInitializer.cs` | 생성물 | 생성 |
| Server | `Assets/Scripts/Game/LOPRunner.cs` | `EndMatch()` 진입점, `LateUpdate`가 호출 | 수정 |
| Server | `Assets/Scripts/Room/LOPRoom.cs` | 종료 시 전 세션 브로드캐스트 | 수정 |
| Client | `Assets/Scripts/Stores/MatchResult.cs` | 결과 레코드(`matchId`) | 신규 |
| Client | `Assets/Scripts/Stores/IMatchResultDataStore.cs` / `MatchResultDataStore.cs` | 씬을 넘는 결과 보관 | 신규 |
| Client | `Assets/Scripts/RootLifetimeScope.cs` | 브로커 + 스토어 등록 | 수정 |
| Client | `Assets/Scripts/Network/NetworkMessageDispatcher.cs` | `MatchEndedToC` 라우트 | 수정 |
| Client | `Assets/Scripts/Game/LOPRunner.cs` | `EndMatch()` | 수정 |
| Client | `Assets/Scripts/Game/MessageHandler/MatchEndedMessageHandler.cs` | 종료 수신 → 결과 기록 + 러너 종료 | 신규 |
| Client | `Assets/Scripts/Game/GameLifetimeScope.cs` | 핸들러 등록 + 러너 `AsSelf` | 수정 |
| Client | `Assets/UI/MatchResult/MatchResultView.uxml` / `.uss` | 결과 화면 레이아웃 | 신규 |
| Client | `Assets/Scripts/UI/MatchResult/MatchResultView.cs` | 결과 화면 바인더 | 신규 |
| Client | `Assets/UI/UIViewCatalog.asset` | `MatchResultView` 엔트리 | 수정 |
| Client | `Assets/Scripts/UI/LobbyHome/FrontEndCoordinator.cs` | 대기 결과 오픈/닫기 | 수정 |
| Client | `Assets/Scripts/Lobby/LobbyLifetimeScope.cs` | 결과 뷰 등록·팩토리 기여 | 수정 |

---

## Task 1: 와이어 — `MatchEndedToC` (Shared 레포)

**Files:**
- Create: `../LeagueOfPhysical-Shared/Protos/MatchEndedToC.proto`
- Generated: `../LeagueOfPhysical-Shared/Runtime.Generated/Scripts/Protobuf/MatchEndedToC.cs`, `MatchEndedToC.IMessage.cs`, `MessageIds.cs`(추가만), `MessageInitializer.cs`(재생성)

**Interfaces:**
- Produces: `LOP.MatchEndedToC` (필드 없는 메시지, `GameFramework.IMessage` 구현), `LOP.MessageIds.MatchEndedToC`(새 ushort). Task 2가 송신, Task 4가 수신한다.

- [ ] **Step 1: 브랜치 생성**

```bash
cd ../LeagueOfPhysical-Shared
git checkout -b feature/flow-slice-d-match-result
```

- [ ] **Step 2: proto 파일 작성**

`../LeagueOfPhysical-Shared/Protos/MatchEndedToC.proto`:
```proto
syntax = "proto3";

// @auto_generate
message MatchEndedToC
{
}
```

> 필드를 넣지 않는다. 종료 사유 enum은 지금 화면이 쓰지 않고, proto3는 나중에 필드를 붙여도 하위호환이다. `// @auto_generate` 주석은 생성 스크립트가 IMessage 래퍼·MessageId를 만들 대상을 고르는 표시라 **반드시 있어야 한다**.

- [ ] **Step 3: 기존 MessageId 백업(비교용)**

```bash
cd ../LeagueOfPhysical-Shared
cp Runtime.Generated/Scripts/MessageIds.cs /tmp/MessageIds.before.cs
```

- [ ] **Step 4: 생성 스크립트 실행**

```bash
cd ../LeagueOfPhysical-Shared/Scripts
./generate_protos.sh
```
Expected: `All proto-related scripts executed successfully.`

- [ ] **Step 5: 기존 ID 불변 확인 (와이어 계약)**

```bash
cd ../LeagueOfPhysical-Shared
diff /tmp/MessageIds.before.cs Runtime.Generated/Scripts/MessageIds.cs
```
Expected: **추가된 `MatchEndedToC` 한 줄만** 차이. 기존 값(WorldEventBatchToC=1 … InputTimingToC=14)이 하나라도 바뀌면 클·서 wire desync이므로 **중단하고 원인을 찾는다**(스크립트는 기존 파일을 읽어 ID를 보존하도록 되어 있다).

`MessageInitializer.cs`에 `MessageFactory.RegisterCreator(MessageIds.MatchEndedToC, () => new MatchEndedToC());`가 추가됐는지도 확인한다.

- [ ] **Step 6: 커밋**

```bash
cd ../LeagueOfPhysical-Shared
git add Protos/MatchEndedToC.proto Runtime.Generated/
git commit -m "feat(wire): 매치 종료 통보 MatchEndedToC 추가"
```

---

## Task 2: 서버 — 종료 진입점 + 브로드캐스트

**Files:**
- Modify: `../LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs`
- Modify: `../LeagueOfPhysical-Server/Assets/Scripts/Room/LOPRoom.cs`

**Interfaces:**
- Consumes: Task 1의 `MatchEndedToC`.
- Produces: 서버 `LOPRunner.EndMatch()` (public, 반환 없음). 클라 Task 4가 같은 이름의 메서드를 갖는다.

- [ ] **Step 1: 브랜치 생성**

```bash
cd ../LeagueOfPhysical-Server
git checkout -b feature/flow-slice-d-match-result
```

- [ ] **Step 2: 서버 `LOPRunner`에 `EndMatch()` 추가 + `LateUpdate`가 호출하도록 교체**

`../LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs`의 기존 `LateUpdate`:
```csharp
        private void LateUpdate()
        {
            if (initialized && tickUpdater.elapsedTime > 60 * 5)
            {
                gameState = GameOver.State;
            }
        }
```
를 다음으로 바꾼다:
```csharp
        private void LateUpdate()
        {
            if (initialized && tickUpdater.elapsedTime > 60 * 5)
            {
                EndMatch();
            }
        }

        /// <summary>매치 종료 진입점. 종료 판정은 서버 권위이고, 클라는 통보를 받아 같은 이름의 메서드로 들어온다.</summary>
        public void EndMatch()
        {
            gameState = GameOver.State;
        }
```

> 동작은 그대로다(같은 상태 전이). 클·서가 같은 이름의 진입점을 갖게 해 "판정 시점만 다르다"를 코드로 드러내는 변경이다.

- [ ] **Step 3: 서버 `LOPRoom`에서 브로드캐스트**

`../LeagueOfPhysical-Server/Assets/Scripts/Room/LOPRoom.cs`의 `OnGameStateChanged`를 다음으로 바꾼다(이 파일은 이미 `[Inject] private ISessionManager sessionManager;`를 갖고 있다):
```csharp
        private void OnGameStateChanged(IGameState gameState)
        {
            switch (gameState)
            {
                case GameOver:
                    Debug.Log("Game Over");

                    // 룸을 닫으면 클라 연결이 끊겨 못 받는다 — 상태 갱신보다 반드시 먼저 보낸다.
                    foreach (var session in sessionManager.GetAllSessions())
                    {
                        session.Send(new MatchEndedToC());
                    }

                    if (!EnvironmentSettings.active.Standalone)
                    {
                        WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                        {
                            roomId = roomDataStore.room.id,
                            status = RoomStatus.Closed,
                        });
                    }
                    break;
            }
        }
```

- [ ] **Step 4: 서버 컴파일 확인**

UnityMCP `refresh_unity`(scope=all, mode=force, compile=request, `unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c"`) → `read_console`(types=["error"], 같은 instance)로 **0 errors** 확인.

- [ ] **Step 5: 커밋**

```bash
cd ../LeagueOfPhysical-Server
git add Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Room/LOPRoom.cs
git commit -m "feat(match): 매치 종료 시 전 세션에 MatchEndedToC 브로드캐스트"
```

---

## Task 3: 클라 — 결과 보관소 (Root 스코프)

**Files:**
- Create: `Assets/Scripts/Stores/MatchResult.cs`, `Assets/Scripts/Stores/IMatchResultDataStore.cs`, `Assets/Scripts/Stores/MatchResultDataStore.cs`
- Modify: `Assets/Scripts/RootLifetimeScope.cs`

**Interfaces:**
- Produces: `LOP.MatchResult { string matchId }`; `LOP.IMatchResultDataStore : IDataStore { MatchResult result { get; set; } }`; `LOP.MatchResultDataStore`. Task 4가 `result`를 쓰고, Task 6이 읽고 `Clear()`한다.

- [ ] **Step 1: 클라 브랜치 확인**

클라 레포에는 spec 커밋 때 이미 같은 이름의 브랜치가 만들어져 있다. 없으면 만들고, 있으면 그대로 쓴다:
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git checkout feature/flow-slice-d-match-result || git checkout -b feature/flow-slice-d-match-result
```

- [ ] **Step 2: `MatchResult.cs`**

```csharp
namespace LOP
{
    /// <summary>직전 매치의 결과. 지금은 "어느 판이었는지"만 담는다 — 순위·점수는 후속 스펙.</summary>
    public class MatchResult
    {
        public string matchId;
    }
}
```

- [ ] **Step 3: `IMatchResultDataStore.cs`**

```csharp
using GameFramework;

namespace LOP
{
    public interface IMatchResultDataStore : IDataStore
    {
        MatchResult result { get; set; }
    }
}
```

- [ ] **Step 4: `MatchResultDataStore.cs`**

```csharp
namespace LOP
{
    /// <summary>
    /// 매치 결과를 로비까지 나르는 보관소. Match 씬의 Room/Game 스코프는 로비 씬을 로드할 때 파괴되므로
    /// 결과는 Root 스코프에 있어야 살아남는다. 결과 화면이 보여준 뒤 Clear한다(로비를 오갈 때 다시 뜨지 않게).
    /// </summary>
    public class MatchResultDataStore : IMatchResultDataStore
    {
        public MatchResult result { get; set; }

        public void Clear()
        {
            result = null;
        }
    }
}
```

- [ ] **Step 5: Root 스코프에 등록 + 메시지 브로커 등록**

`Assets/Scripts/RootLifetimeScope.cs`에서 네트워크 수신 브로커 목록의 `builder.RegisterMessageBroker<InputTimingToC>(options);` 바로 아래에 추가:
```csharp
            builder.RegisterMessageBroker<MatchEndedToC>(options);
```

그리고 `RoomDataStore` 등록 블록 아래에 추가:
```csharp
            builder.Register<MatchResultDataStore>(Lifetime.Singleton)
                .As<IMatchResultDataStore>()
                .As<IDataStore>()
                .AsSelf();
```

- [ ] **Step 6: 컴파일 확인 + 커밋**

UnityMCP `refresh_unity`(scope=all, mode=force, compile=request, `unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4"`) → `read_console`(types=["error"]) **0 errors**.

```bash
git add Assets/Scripts/Stores/MatchResult.cs Assets/Scripts/Stores/MatchResult.cs.meta \
        Assets/Scripts/Stores/IMatchResultDataStore.cs Assets/Scripts/Stores/IMatchResultDataStore.cs.meta \
        Assets/Scripts/Stores/MatchResultDataStore.cs Assets/Scripts/Stores/MatchResultDataStore.cs.meta \
        Assets/Scripts/RootLifetimeScope.cs
git commit -m "feat(match): 씬을 넘는 MatchResultDataStore(Root) + MatchEndedToC 브로커 등록"
```

---

## Task 4: 클라 — 종료 수신 경로

**Files:**
- Modify: `Assets/Scripts/Game/LOPRunner.cs`
- Create: `Assets/Scripts/Game/MessageHandler/MatchEndedMessageHandler.cs`
- Modify: `Assets/Scripts/Network/NetworkMessageDispatcher.cs`, `Assets/Scripts/Game/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: Task 1 `MatchEndedToC`, Task 3 `IMatchResultDataStore`/`MatchResult`, 기존 `IRoomDataStore.room.matchId`, 기존 `MessageHandlerBase`(추상 `void Subscribe()` + `Track(IDisposable)`).
- Produces: 클라 `LOPRunner.EndMatch()` (public); `MatchEndedMessageHandler`. 이후 로비 진입은 **기존** `LOPRoom.OnGameStateChanged`의 `case GameOver → SceneManager.LoadScene("Lobby")`가 처리한다(수정하지 않는다).

- [ ] **Step 1: 클라 `LOPRunner.EndMatch()` 추가**

`Assets/Scripts/Game/LOPRunner.cs`의 기존 `Stop()` 메서드 바로 아래에 추가:
```csharp
        /// <summary>서버의 MatchEndedToC를 받아 매치 종료 상태로 들어간다 — 판정은 서버 권위, 클라는 통보받을 뿐이다.</summary>
        public void EndMatch()
        {
            gameState = GameOver.State;
        }
```

- [ ] **Step 2: `MatchEndedMessageHandler.cs`**

```csharp
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 서버의 매치 종료 통보를 받아 ① 결과를 Root 스토어에 남기고 ② 러너를 종료 상태로 보낸다.
    /// 로비 씬 로드는 LOPRoom이 러너 상태 변화를 보고 수행하므로 여기서 씬을 만지지 않는다.
    /// </summary>
    public class MatchEndedMessageHandler : MessageHandlerBase
    {
        private readonly LOPRunner runner;
        private readonly IRoomDataStore roomDataStore;
        private readonly IMatchResultDataStore matchResultDataStore;
        private readonly ISubscriber<MatchEndedToC> matchEndedSubscriber;

        public MatchEndedMessageHandler(
            LOPRunner runner,
            IRoomDataStore roomDataStore,
            IMatchResultDataStore matchResultDataStore,
            ISubscriber<MatchEndedToC> matchEndedSubscriber)
        {
            this.runner = runner;
            this.roomDataStore = roomDataStore;
            this.matchResultDataStore = matchResultDataStore;
            this.matchEndedSubscriber = matchEndedSubscriber;
        }

        protected override void Subscribe() => Track(matchEndedSubscriber.Subscribe(OnMatchEnded));

        private void OnMatchEnded(MatchEndedToC message)
        {
            matchResultDataStore.result = new MatchResult { matchId = roomDataStore.room?.matchId };

            runner.EndMatch();
        }
    }
}
```

> `room`이 null일 수 있는 경로(재접속 실패 등)에서 결과 자체를 못 만들면 로비 복귀까지 막히므로, matchId만 null로 두고 진행한다(화면은 matchId를 쓰지 않는다).

- [ ] **Step 3: 디스패처에 라우트 추가**

`Assets/Scripts/Network/NetworkMessageDispatcher.cs`의 생성자 파라미터 마지막(`IPublisher<InputTimingToC> inputTiming`) 뒤에 추가하고, 본문에도 등록한다:
```csharp
            IPublisher<InputTimingToC> inputTiming,
            IPublisher<MatchEndedToC> matchEnded)
        {
            Register(gameInfo);
            Register(worldEventBatch);
            Register(snaps);
            Register(spawn);
            Register(despawn);
            Register(userSnap);
            Register(statAllocation);
            Register(inputSequence);
            Register(inputTiming);
            Register(matchEnded);
        }
```

- [ ] **Step 4: Game 스코프에 핸들러 등록 + 러너를 자기 타입으로도 resolve 가능하게**

`Assets/Scripts/Game/GameLifetimeScope.cs`에서 러너 등록 줄을 바꾼다(핸들러가 `LOPRunner.EndMatch()`를 부르려면 구체 타입 resolve가 필요):
```csharp
            // runner은 게임 서비스에 의존하므로 부모(Room)가 아닌 이 컨테이너에서 주입돼야 한다.
            // AsSelf는 LOP 전용 진입점(EndMatch 등)을 쓰는 소비자를 위한 것 — IRunner에는 없는 API다.
            builder.RegisterComponent(runner).As<IRunner>().AsSelf();
```
그리고 메시지 핸들러 등록 목록의 `builder.RegisterEntryPoint<GameWorldEventMessageHandler>();` 아래에 추가:
```csharp
            builder.RegisterEntryPoint<MatchEndedMessageHandler>();
```

- [ ] **Step 5: 컴파일 확인 + 커밋**

`refresh_unity`(클라 instance) → `read_console`(types=["error"]) **0 errors**.

```bash
git add Assets/Scripts/Game/LOPRunner.cs \
        Assets/Scripts/Game/MessageHandler/MatchEndedMessageHandler.cs Assets/Scripts/Game/MessageHandler/MatchEndedMessageHandler.cs.meta \
        Assets/Scripts/Network/NetworkMessageDispatcher.cs Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat(match): 클라 매치 종료 수신(MatchEndedMessageHandler → LOPRunner.EndMatch)"
```

---

## Task 5: 결과 화면 (UXML/USS + View + 카탈로그)

**Files:**
- Create: `Assets/UI/MatchResult/MatchResultView.uxml`, `Assets/UI/MatchResult/MatchResultView.uss`, `Assets/Scripts/UI/MatchResult/MatchResultView.cs`
- Modify: `Assets/UI/UIViewCatalog.asset`

**Interfaces:**
- Consumes: 기존 `UIView`/`UILayer`.
- Produces: `LOP.UI.MatchResultView : UIView` — `void SetConfirmCallback(System.Action)`, `Layer => UILayer.Window`. Task 6이 `Open<MatchResultView>()`로 연다.

- [ ] **Step 1: `MatchResultView.uxml`**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="matchresult-root" class="matchresult-root">
        <ui:VisualElement name="matchresult-card" class="matchresult-card">
            <ui:Label text="결과" class="matchresult-title" />
            <ui:Label text="매치 종료" class="matchresult-message" />
            <ui:Button name="confirm-button" text="확인" class="matchresult-confirm" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: `MatchResultView.uss`**

```css
.matchresult-root {
    flex-grow: 1;
    align-items: center;
    justify-content: center;
    background-color: rgba(8, 8, 12, 0.75);
}

.matchresult-card {
    width: 420px;
    padding: 32px;
    border-radius: 12px;
    background-color: rgba(20, 20, 28, 0.95);
    align-items: stretch;
}

.matchresult-title {
    font-size: 28px;
    color: rgb(235, 235, 245);
    -unity-text-align: middle-center;
    margin-bottom: 12px;
}

.matchresult-message {
    font-size: 18px;
    color: rgb(180, 180, 195);
    -unity-text-align: middle-center;
    margin-bottom: 24px;
}

.matchresult-confirm {
    height: 52px;
    font-size: 18px;
    border-radius: 8px;
}
```

- [ ] **Step 3: `MatchResultView.cs`**

```csharp
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 매치 결과 화면(플레이스홀더). 여는 쪽(FrontEndCoordinator)이 SetConfirmCallback으로 닫기 동작을 배선한다.
    /// 점수·순위 표시는 후속 스펙 — 지금은 판이 끝났음을 알리고 닫는 역할만 한다.
    /// </summary>
    public class MatchResultView : UIView
    {
        // LOP.Action(MonoBehaviour 컴포넌트)이 System.Action을 가리므로 풀 한정한다.
        private Button _confirmButton;
        private System.Action _onConfirm;

        public override UILayer Layer => UILayer.Window;

        public void SetConfirmCallback(System.Action onConfirm) => _onConfirm = onConfirm;

        public override void OnOpen()
        {
            base.OnOpen();

            _confirmButton = Root.Q<Button>("confirm-button");
            _confirmButton.clicked += OnConfirmClicked;
        }

        public override void OnClose()
        {
            if (_confirmButton != null) _confirmButton.clicked -= OnConfirmClicked;
            base.OnClose();
        }

        private void OnConfirmClicked() => _onConfirm?.Invoke();
    }
}
```

- [ ] **Step 4: Unity 임포트 + 카탈로그 엔트리 추가**

`refresh_unity`(클라 instance) → `read_console` 0 errors. 그다음 생성된 GUID를 읽는다:
```bash
grep guid Assets/UI/MatchResult/MatchResultView.uxml.meta Assets/UI/MatchResult/MatchResultView.uss.meta
```
`Assets/UI/UIViewCatalog.asset`의 `entries` 맨 끝(마지막 엔트리 `ProfileView` 아래)에 추가한다. `fileID`는 기존 엔트리와 동일한 상수를 그대로 쓰고 `guid`만 위에서 읽은 값으로 바꾼다:
```yaml
  - viewName: MatchResultView
    uxml: {fileID: 9197481963319205126, guid: <MatchResultView.uxml의 guid>, type: 3}
    uss: {fileID: 7433441132597879392, guid: <MatchResultView.uss의 guid>, type: 3}
```

- [ ] **Step 5: 임포트 확인 + 커밋**

`refresh_unity`(클라 instance) → `read_console` 0 errors. `git diff Assets/UI/UIViewCatalog.asset`으로 **기존 엔트리 불변 + 1엔트리 추가**만 확인.

```bash
git add Assets/UI/MatchResult/ Assets/UI/MatchResult.meta \
        Assets/Scripts/UI/MatchResult/ Assets/Scripts/UI/MatchResult.meta \
        Assets/UI/UIViewCatalog.asset
git commit -m "feat(ui): 매치 결과 화면(MatchResultView, 플레이스홀더)"
```

---

## Task 6: 로비 배선 — 대기 중인 결과를 한 번 띄우기

**Files:**
- Modify: `Assets/Scripts/UI/LobbyHome/FrontEndCoordinator.cs`, `Assets/Scripts/Lobby/LobbyLifetimeScope.cs`

**Interfaces:**
- Consumes: Task 3 `IMatchResultDataStore`, Task 5 `MatchResultView.SetConfirmCallback`, 기존 `IWindowManager.Open<T>/Close`/`RegisterViewFactory<T>`.

- [ ] **Step 1: `FrontEndCoordinator.cs` 전체 교체**

```csharp
using R3;
using System;
using VContainer.Unity;

namespace LOP.UI
{
    /// <summary>
    /// 프론트엔드 네비게이션 담당. LobbyHomeViewModel의 네비 신호를 구독해 상점/설정/프로필 셸 윈도우를 열고,
    /// 셸의 back으로 닫는다(로비 홈 위 push/pop). VM은 신호만 노출, 화면 교체는 여기서(작은 흐름=VM / 큰 흐름=코디네이터).
    /// 직전 매치 결과가 남아 있으면 로비 진입 직후 결과 화면도 한 번 띄운다.
    /// </summary>
    public class FrontEndCoordinator : IStartable, IDisposable
    {
        private readonly IWindowManager _windowManager;
        private readonly LobbyHomeViewModel _viewModel;
        private readonly IMatchResultDataStore _matchResultDataStore;

        private IDisposable _subscription;
        private ShellView _currentShell;
        private MatchResultView _matchResultView;

        public FrontEndCoordinator(
            IWindowManager windowManager,
            LobbyHomeViewModel viewModel,
            IMatchResultDataStore matchResultDataStore)
        {
            _windowManager = windowManager;
            _viewModel = viewModel;
            _matchResultDataStore = matchResultDataStore;
        }

        public void Start()
        {
            _subscription = _viewModel.NavigationRequested.Subscribe(OnNavigationRequested);

            ShowPendingMatchResult();
        }

        // 직전 매치 결과가 있으면 로비 진입 직후 한 번 보여준다.
        // VContainer는 IStartable을 플레이어 루프로 돌리므로 이 시점엔 LobbyLifetimeScope의 빌드 콜백이 이미 끝나 있다
        // (= 뷰 팩토리 등록·로비 홈 오픈 완료). 이 로직을 IInitializable 같은 더 이른 훅으로 옮기면
        // 팩토리 미등록 resolve 실패 또는 로비 홈이 결과 창 위로 올라오는 z-순서 역전이 난다.
        private void ShowPendingMatchResult()
        {
            if (_matchResultDataStore.result == null)
            {
                return;
            }

            _matchResultView = _windowManager.Open<MatchResultView>();
            _matchResultView.SetConfirmCallback(CloseMatchResult);
        }

        private void CloseMatchResult()
        {
            if (_matchResultView != null)
            {
                _windowManager.Close(_matchResultView);
                _matchResultView = null;
            }

            // 소비했으니 비운다 — 안 비우면 로비를 오갈 때마다 다시 뜬다.
            _matchResultDataStore.Clear();
        }

        private void OnNavigationRequested(FrontEndDestination destination)
        {
            CloseCurrentShell();

            _currentShell = destination switch
            {
                FrontEndDestination.Shop => _windowManager.Open<ShopView>(),
                FrontEndDestination.Settings => _windowManager.Open<SettingsView>(),
                FrontEndDestination.Profile => _windowManager.Open<ProfileView>(),
                _ => null,
            };

            _currentShell?.SetBackCallback(CloseCurrentShell);
        }

        private void CloseCurrentShell()
        {
            if (_currentShell != null)
            {
                _windowManager.Close(_currentShell);
                _currentShell = null;
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            CloseCurrentShell();

            if (_matchResultView != null)
            {
                _windowManager.Close(_matchResultView);
                _matchResultView = null;
            }
        }
    }
}
```

- [ ] **Step 2: `LobbyLifetimeScope.cs`에 결과 뷰 등록**

필드에 추가:
```csharp
        private IDisposable _matchResultViewRegistration;
```
셸 등록 옆(`builder.Register<ProfileView>(Lifetime.Transient);` 아래)에 추가:
```csharp
            builder.Register<MatchResultView>(Lifetime.Transient);
```
빌드 콜백의 셸 팩토리 기여 아래에 추가:
```csharp
                _matchResultViewRegistration = windowManager.RegisterViewFactory<MatchResultView>(() => container.Resolve<MatchResultView>());
```
`OnDestroy`의 셸 해제 아래에 추가:
```csharp
            _matchResultViewRegistration?.Dispose();
```

- [ ] **Step 3: 컴파일 확인 + 커밋**

`refresh_unity`(클라 instance) → `read_console`(types=["error"]) **0 errors**.

```bash
git add Assets/Scripts/UI/LobbyHome/FrontEndCoordinator.cs Assets/Scripts/Lobby/LobbyLifetimeScope.cs
git commit -m "feat(ui): 로비 진입 시 대기 중인 매치 결과 화면 표시"
```

---

## Task 7: 플레이테스트

**Files:** (검증 — 임시 수정 후 원복)

> **선행**: 로컬 서버 + 백엔드 필요. 불가하면 사용자 수동 검증.

- [ ] **Step 1: 타임리밋 임시 단축 (서버)**

`../LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs`의 `LateUpdate`에서 `60 * 5`를 `20`으로 임시 변경(커밋하지 않는다). 서버 `refresh_unity`로 컴파일.

- [ ] **Step 2: 끝-끝 확인**

플레이 → 매칭 → 인게임 진입 → 20초 뒤 매치 종료.
Expected:
- 클라가 자동으로 로비로 복귀한다
- 로비 홈 위에 결과 창("결과" / "매치 종료" / [확인])이 떠 있다
- [확인] → 창이 닫히고 로비 홈만 남는다
- 클라·서버 콘솔 예외 0 (`read_console` 양쪽 instance)

- [ ] **Step 3: 재표시 안 되는지 확인 (Clear 검증)**

결과 창을 닫은 뒤 상점/설정/프로필을 오가고 로비 홈으로 돌아온다.
Expected: 결과 창이 **다시 뜨지 않는다**.

- [ ] **Step 4: 기존 흐름 보존 확인**

PLAY → 매칭 대기 오버레이 정상(Slice B), 네비 셸 open/pop 정상(Slice C).

- [ ] **Step 5: 타임리밋 원복 + 확인**

`60 * 5`로 되돌린다. **이 슬라이스에서 가장 현실적인 사고가 원복 누락이다** — 반드시 확인한다:
```bash
cd ../LeagueOfPhysical-Server
git diff
```
Expected: **출력 없음**(Task 2 커밋 상태 그대로).

---

## 완료 기준

- 서버가 매치 종료 시 `MatchEndedToC`를 전 세션에 보내고, 클라가 받아 로비로 복귀한다.
- 로비 진입 시 결과 화면이 **한 번** 뜨고, [확인]으로 닫히며, 다시 뜨지 않는다.
- 기존 MessageId 불변(와이어 계약 유지), 3개 레포 컴파일 0 errors, Slice B/C 동작 보존.
- 서버 타임리밋은 `60 * 5` 원복 상태.
