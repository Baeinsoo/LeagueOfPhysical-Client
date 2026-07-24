# Slice A — 앱 FSM 씬 전환 일원화 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 흩어진 씬 로드 4곳을 Root 스코프 `AppStateMachine` 한 곳으로 모으고, 각 소스는 씬을 직접 로드하는 대신 도메인 신호(`BootCompleted`/`MatchFound`/`MatchEnded`)만 발행하게 한다.

**Architecture:** `MatchStateMachine`과 같은 GameFramework `StateMachine<TEvent>` 인프라 위에 앱-플로우 FSM(`Boot`/`FrontEnd`/`InMatch`)을 신설. 씬 로드는 `ISceneLoader` 포트 뒤로 두어 씬 이름을 중앙화하고 FSM을 `UnityEngine.SceneManagement` static에서 분리한다. FSM은 Root 싱글턴이라 자식 스코프(Entrance/Lobby/Room)가 주입받아 신호를 Fire한다.

**Tech Stack:** Unity (C#, Assembly-CSharp), VContainer(DI), GameFramework FSM, UnityMCP(컴파일 검증).

설계 근거·대안·순서 불변식은 spec 참고: `docs/superpowers/specs/2026-07-24-flow-slice-a-app-fsm-design.md`.

## Global Constraints

- **네임스페이스는 `LOP`(flat)** — 폴더는 조직화용, 네임스페이스 영향 없음.
- **World 타입 충돌 회피 규칙은 무관** — 이 슬라이스는 `GameFramework.World` 타입을 안 쓴다. FSM 타입은 `GameFramework.State`/`StateMachine`(World 아님)이라 `using GameFramework;` 사용 OK.
- **MonoBehaviour 필드 주입은 이 코드베이스의 의도된 패턴** — `EntranceScene`/`LOPRoom`은 MonoBehaviour이므로 `[Inject] private AppStateMachine ...` 필드 주입을 쓴다(생성자 주입 아님).
- **UnityMCP는 항상 클라 인스턴스를 명시 타깃** — 모든 UnityMCP 호출에 `unity_instance`를 전달한다. 클라 id 해석: `mcpforunity://instances` 리소스에서 `name == "LeagueOfPhysical-Client"`인 인스턴스의 전체 `id`(`Name@hash`)를 읽어 쓴다(해시는 바뀔 수 있음).
- **새 `.cs` 파일은 Unity가 생성한 `.meta`와 함께 커밋** — Write로 파일 생성 후 `refresh_unity`로 `.meta` 생성 → `.cs`+`.meta` 둘 다 `git add`.
- **테스트 제약** — 클라는 전부 Assembly-CSharp라 test asmdef가 참조 불가 → 이 슬라이스에 클라 유닛 테스트 없음. 각 태스크 검증 = UnityMCP 컴파일 클린. 최종 검증 = 엔드투엔드 플레이테스트(Task 8). FSM 메커니즘은 GameFramework EditMode `StateMachineTests`가 이미 커버.

## File Structure

**신규 (모두 `Assets/Scripts/App/` 아래, namespace `LOP`):**
- `App/AppEvent.cs` — enum `AppEvent { BootCompleted, MatchFound, MatchEnded }`
- `App/ISceneLoader.cs` — 씬 로드 포트 (`void Load(string sceneName)`)
- `App/SceneLoader.cs` — Unity 구현 (`SceneManager.LoadScene` 래핑)
- `App/AppStateMachine.cs` — `StateMachine<AppEvent>, IStartable`; 세 상태를 내부에서 조립
- `App/States/Boot.cs` — `State<AppEvent>`; `BootCompleted → FrontEnd` (씬 로드 없음)
- `App/States/FrontEnd.cs` — `State<AppEvent>`; 진입 시 `Lobby` 로드, `MatchFound → InMatch`
- `App/States/InMatch.cs` — `State<AppEvent>`; 진입 시 `Room` 로드, `MatchEnded → FrontEnd`

**수정:**
- `RootLifetimeScope.cs` — `ISceneLoader→SceneLoader` 싱글턴 + `AppStateMachine` 엔트리포인트(+AsSelf) 등록
- `Entrance/EntranceScene.cs` — `LoadScene("Lobby")` → `Fire(BootCompleted)`
- `Matchmaking/MatchStateMachine/States/InGameRoom.cs` — 조인 확인 성공 시 `Fire(MatchFound)`, 이중 재시도 루프 제거
- `Room/RoomConnector.cs` — `LoadScene("Room")` 한 줄 제거
- `Room/LOPRoom.cs` — `GameOver`·`Awake` catch → `Fire(MatchEnded)` (2곳)
- `Entrance/EntranceLifetimeScope.cs` — 주석 처리된 `CheckLocationComponent` 등록 줄 삭제

**삭제:**
- `Entrance/EntranceComponent/CheckLocationComponent.cs` (+ `.meta`)

---

### Task 1: 씨앗 타입 — AppEvent + ISceneLoader + SceneLoader

**Files:**
- Create: `Assets/Scripts/App/AppEvent.cs`
- Create: `Assets/Scripts/App/ISceneLoader.cs`
- Create: `Assets/Scripts/App/SceneLoader.cs`

**Interfaces:**
- Consumes: (없음)
- Produces: `enum AppEvent { BootCompleted, MatchFound, MatchEnded }`; `interface ISceneLoader { void Load(string sceneName); }`; `class SceneLoader : ISceneLoader`.

- [ ] **Step 1: `AppEvent.cs` 작성**

```csharp
namespace LOP
{
    public enum AppEvent
    {
        BootCompleted,
        MatchFound,
        MatchEnded,
    }
}
```

- [ ] **Step 2: `ISceneLoader.cs` 작성**

```csharp
namespace LOP
{
    /// <summary>씬 로드 포트. FSM이 UnityEngine.SceneManagement static에 직접 묶이지 않게 한다.</summary>
    public interface ISceneLoader
    {
        void Load(string sceneName);
    }
}
```

- [ ] **Step 3: `SceneLoader.cs` 작성**

```csharp
using UnityEngine.SceneManagement;

namespace LOP
{
    public class SceneLoader : ISceneLoader
    {
        public void Load(string sceneName) => SceneManager.LoadScene(sceneName);
    }
}
```

- [ ] **Step 4: 컴파일 검증(.meta 생성 포함)**

`mcpforunity://instances`에서 클라 id를 해석한 뒤:
`refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")` → `read_console(unity_instance="LeagueOfPhysical-Client@<hash>", types=["error"])`.
Expected: 에러 0. `App/` 폴더에 3개 `.cs` + 대응 `.meta` 생성됨.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/App/AppEvent.cs Assets/Scripts/App/AppEvent.cs.meta \
        Assets/Scripts/App/ISceneLoader.cs Assets/Scripts/App/ISceneLoader.cs.meta \
        Assets/Scripts/App/SceneLoader.cs Assets/Scripts/App/SceneLoader.cs.meta
git commit -m "feat(flow): AppEvent + ISceneLoader 포트 + SceneLoader 구현"
```

---

### Task 2: AppStateMachine + 상태 3종 (Boot/FrontEnd/InMatch)

**Files:**
- Create: `Assets/Scripts/App/States/Boot.cs`
- Create: `Assets/Scripts/App/States/FrontEnd.cs`
- Create: `Assets/Scripts/App/States/InMatch.cs`
- Create: `Assets/Scripts/App/AppStateMachine.cs`

**Interfaces:**
- Consumes: `AppEvent`, `ISceneLoader` (Task 1); `GameFramework.State<TEvent>`, `GameFramework.StateMachine<TEvent>`, `GameFramework.IState<TEvent>`; `VContainer.Unity.IStartable`.
- Produces: `class AppStateMachine : StateMachine<AppEvent>, IStartable` (ctor `AppStateMachine(ISceneLoader sceneLoader)`, 인스턴스 메서드 `Fire(AppEvent)`는 base에서 상속). 상태 3종은 `AppStateMachine`이 내부에서만 조립(외부 미사용).

배경: `State<AppEvent>` base는 `OnEnter()`(진입 부수효과), `GetNextState(ev)`(순수 전이), `OnExecuteAsync`/`OnError`(기본 no-op)를 제공한다. 이 세 상태는 자체 async 로직이 없어 `OnEnter`(씬 로드)와 `GetNextState`만 쓴다. `StateMachine<AppEvent>` base는 `Start()`(=`initState` 진입), `Fire(ev)`(재진입 큐 포함), `Stop()`을 제공한다.

- [ ] **Step 1: `States/Boot.cs` 작성**

```csharp
using GameFramework;
using System;

namespace LOP
{
    //  부트 페이즈. Entrance 씬은 앱 시작 시 이미 로드돼 있어 진입 시 로드할 씬이 없다.
    public class Boot : State<AppEvent>
    {
        private readonly Func<FrontEnd> frontEnd;

        public Boot(Func<FrontEnd> frontEnd)
        {
            this.frontEnd = frontEnd;
        }

        public override IState<AppEvent> GetNextState(AppEvent ev)
        {
            return ev switch
            {
                AppEvent.BootCompleted => frontEnd(),
                _ => this,
            };
        }
    }
}
```

- [ ] **Step 2: `States/FrontEnd.cs` 작성**

```csharp
using GameFramework;
using System;

namespace LOP
{
    //  프론트엔드 페이즈(로비). 진입 시 Lobby 씬을 로드한다.
    public class FrontEnd : State<AppEvent>
    {
        private const string LobbySceneName = "Lobby";

        private readonly Func<InMatch> inMatch;
        private readonly ISceneLoader sceneLoader;

        public FrontEnd(Func<InMatch> inMatch, ISceneLoader sceneLoader)
        {
            this.inMatch = inMatch;
            this.sceneLoader = sceneLoader;
        }

        protected override void OnEnter() => sceneLoader.Load(LobbySceneName);

        public override IState<AppEvent> GetNextState(AppEvent ev)
        {
            return ev switch
            {
                AppEvent.MatchFound => inMatch(),
                _ => this,
            };
        }
    }
}
```

- [ ] **Step 3: `States/InMatch.cs` 작성**

```csharp
using GameFramework;
using System;

namespace LOP
{
    //  인매치 페이즈. 진입 시 Room 씬을 로드한다(LOPGame이 additive로 얹힘).
    public class InMatch : State<AppEvent>
    {
        private const string RoomSceneName = "Room";

        private readonly Func<FrontEnd> frontEnd;
        private readonly ISceneLoader sceneLoader;

        public InMatch(Func<FrontEnd> frontEnd, ISceneLoader sceneLoader)
        {
            this.frontEnd = frontEnd;
            this.sceneLoader = sceneLoader;
        }

        protected override void OnEnter() => sceneLoader.Load(RoomSceneName);

        public override IState<AppEvent> GetNextState(AppEvent ev)
        {
            return ev switch
            {
                AppEvent.MatchEnded => frontEnd(),
                _ => this,
            };
        }
    }
}
```

- [ ] **Step 4: `AppStateMachine.cs` 작성**

세 상태를 내부에서 조립한다. 상태는 의존성이 `ISceneLoader`뿐이라 per-state DI 등록이 불필요하다. 전이 대상은 `Func<다음상태>`(팩토리 메서드)로 연결한다(`FrontEnd↔InMatch` 순환 참조를 메서드 그룹으로 해소).

`IStartable`을 구현하면 VContainer가 앱 시작 시 `Start()`를 호출한다 — base `StateMachine.Start()`가 그 시그니처(`void Start()`)를 그대로 만족하므로 별도 스타터 클래스가 필요 없다.

```csharp
using GameFramework;
using VContainer.Unity;

namespace LOP
{
    //  앱-플로우 상태 머신(Root). 씬 페이즈(Boot/FrontEnd/InMatch)를 소유하고 씬 로드를 일원화한다.
    //  전이는 전부 외부 신호(EntranceScene / 매칭 FSM / 매치 종료)가 Fire로 구동한다.
    //  IStartable: VContainer가 앱 시작 시 Start()를 호출 → initState(Boot) 진입. base의 Start()가 그 역할을 겸한다.
    public class AppStateMachine : StateMachine<AppEvent>, IStartable
    {
        private readonly ISceneLoader sceneLoader;

        public AppStateMachine(ISceneLoader sceneLoader)
        {
            this.sceneLoader = sceneLoader;
        }

        public override IState<AppEvent> initState => new Boot(CreateFrontEnd);

        private FrontEnd CreateFrontEnd() => new FrontEnd(CreateInMatch, sceneLoader);
        private InMatch CreateInMatch() => new InMatch(CreateFrontEnd, sceneLoader);
    }
}
```

- [ ] **Step 5: 컴파일 검증**

`refresh_unity` → `read_console(types=["error"])` (클라 인스턴스).
Expected: 에러 0. (특히 `AppStateMachine`이 `IStartable`을 상속 `Start()`로 만족하는지, `Func<FrontEnd>`/`Func<InMatch>` 메서드 그룹 변환이 되는지 확인.)

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/App/States/ Assets/Scripts/App/AppStateMachine.cs Assets/Scripts/App/AppStateMachine.cs.meta \
        Assets/Scripts/App/States.meta
git commit -m "feat(flow): AppStateMachine + Boot/FrontEnd/InMatch 상태"
```
> 주: `git add Assets/Scripts/App/States/`가 `.cs`+`.meta`를 모두 잡는다. 폴더 `.meta`(`States.meta`)도 함께 커밋한다.

---

### Task 3: Root DI 등록 + 앱 시작 시 기동

**Files:**
- Modify: `Assets/Scripts/RootLifetimeScope.cs` (around line 75, `RoomConnector` 등록 뒤 / `new UIInstaller().Install` 앞)

**Interfaces:**
- Consumes: `AppStateMachine`, `ISceneLoader`, `SceneLoader` (Task 1·2); `VContainer` `RegisterEntryPoint`/`Register`(이미 `using VContainer;`·`using VContainer.Unity;` 존재).
- Produces: 컨테이너에 `ISceneLoader`(→`SceneLoader`, Singleton)와 `AppStateMachine`(EntryPoint + AsSelf, Singleton) 등록. 이후 태스크가 `AppStateMachine`을 주입받아 `Fire`한다.

- [ ] **Step 1: 등록 코드 추가**

`Assets/Scripts/RootLifetimeScope.cs`의 `builder.Register<RoomConnector>(Lifetime.Transient);` 바로 다음 줄에 삽입:

```csharp
            // 앱-플로우 씬 페이즈 FSM(Root). IStartable로 앱 시작 시 Start()되어 Boot 진입.
            // AsSelf로 자식 스코프(Entrance/Lobby/Room)가 주입받아 신호를 Fire할 수 있게 한다.
            builder.Register<ISceneLoader, SceneLoader>(Lifetime.Singleton);
            builder.RegisterEntryPoint<AppStateMachine>().AsSelf();
```

- [ ] **Step 2: 컴파일 검증**

`refresh_unity` → `read_console(types=["error"])` (클라).
Expected: 에러 0.

- [ ] **Step 3: 스모크(선택, 서버 불필요)**

플레이 진입 후 `read_console`에 예외가 없는지 확인. 이 시점엔 `EntranceScene`이 여전히 옛 `LoadScene("Lobby")`로 로비에 들어가고 `AppStateMachine`은 `Boot`에 idle 상태다(회귀 없음).

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/RootLifetimeScope.cs
git commit -m "feat(flow): AppStateMachine + SceneLoader Root 등록·기동"
```

---

### Task 4: EntranceScene → Fire(BootCompleted)

**Files:**
- Modify: `Assets/Scripts/Entrance/EntranceScene.cs`

**Interfaces:**
- Consumes: `AppStateMachine`(Task 3에서 Root 등록), `AppEvent.BootCompleted`.
- Produces: 부트 완료 시 씬을 직접 로드하지 않고 `appStateMachine.Fire(AppEvent.BootCompleted)` → FSM이 `FrontEnd`로 전이하며 `Lobby`를 로드.

- [ ] **Step 1: `EntranceScene.cs`를 아래로 교체**

`LoadScene`을 신호 발행으로 바꾸고, 이제 안 쓰는 `using UnityEngine.SceneManagement;`를 제거한다. `AppStateMachine`은 필드 주입(MonoBehaviour 패턴).

```csharp
using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class EntranceScene : MonoBehaviour
    {
        [Inject]
        private IEnumerable<IEntranceComponent> entranceComponents;

        [Inject]
        private AppStateMachine appStateMachine;

        private async void Start()
        {
            if (await ExecuteEntranceComponents())
            {
                appStateMachine.Fire(AppEvent.BootCompleted);
            }
        }

        private async Task<bool> ExecuteEntranceComponents()
        {
            try
            {
                foreach (var entranceComponent in entranceComponents.OrEmpty())
                {
                    await entranceComponent.Execute();
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
                return false;
            }
        }
    }
}
```

- [ ] **Step 2: 컴파일 검증**

`refresh_unity` → `read_console(types=["error"])` (클라).
Expected: 에러 0.

- [ ] **Step 3: 스모크(선택, 서버 불필요) — 부트→로비**

플레이 진입 → 부트 컴포넌트 완료 후 Lobby 씬으로 넘어가는지(로비 홈 화면 표시) 확인. 이제 로비 진입이 `AppStateMachine`(`Boot→FrontEnd`) 경유다.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Entrance/EntranceScene.cs
git commit -m "feat(flow): EntranceScene가 LoadScene 대신 BootCompleted 발행"
```

---

### Task 5: 매치 진입 — RoomConnector 로드 제거 + InGameRoom Fire(MatchFound)

**Files:**
- Modify: `Assets/Scripts/Room/RoomConnector.cs`
- Modify: `Assets/Scripts/Matchmaking/MatchStateMachine/States/InGameRoom.cs`

**Interfaces:**
- Consumes: `AppStateMachine`(Root 등록), `AppEvent.MatchFound`, `RoomConnector.TryToEnterRoomById(string, int) -> Task<bool>`(로드 제거 후에도 시그니처 불변).
- Produces: `RoomConnector`는 조인 확인 성공 시 씬을 로드하지 않고 `true`만 반환(`RoomDataStore.room`은 `RoomJoinableResponse` 구독으로 여전히 채워짐). `InGameRoom`은 성공 시 `appStateMachine.Fire(MatchFound)` → FSM이 `InMatch`로 전이하며 `Room` 로드.

두 파일은 함께 바뀌어야 한다(로더가 사라지면 발행자가 붙어야 함) → 한 태스크.

- [ ] **Step 1: `RoomConnector.cs`에서 씬 로드 제거**

`SceneManager.LoadScene("Room");` 줄을 삭제하고, 이제 안 쓰는 `using UnityEngine.SceneManagement;`를 제거한다. 나머지(재시도 루프·`CheckRoomJoinable`·`RoomDataStore` 채움)는 유지.

교체 후 전체 파일:

```csharp
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using GameFramework;
using System;

namespace LOP
{
    public class RoomConnector
    {
        private const int DEFAULT_RETRY_COUNT = 10;
        private const int RETRY_INTERVAL_MILLISECONDS = 1000;

        private IRoomDataStore roomDataStore;

        public RoomConnector(IRoomDataStore roomDataStore)
        {
            this.roomDataStore = roomDataStore;
        }

        //  방 배정 확인(성공 시 RoomJoinableResponse 구독이 RoomDataStore.room을 채움).
        //  씬 로드는 하지 않는다 — 호출자가 성공을 받아 AppStateMachine에 MatchFound를 발행하고,
        //  실제 Room 씬 로드는 AppStateMachine의 InMatch 진입이 담당한다.
        public async Task<bool> TryToEnterRoomById(string roomId, int retryCount = DEFAULT_RETRY_COUNT)
        {
            for (int attempt = 0; attempt < retryCount; attempt++)
            {
                try
                {
                    var checkRoomJoinable = await WebAPI.CheckRoomJoinable(roomId);

                    if (checkRoomJoinable.response.code == ResponseCode.SUCCESS)
                    {
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error checking room joinability (Attempt {attempt + 1}/{retryCount}): {e.Message}");
                }

                if (attempt < retryCount - 1)
                {
                    await Task.Delay(RETRY_INTERVAL_MILLISECONDS);
                }
            }

            Debug.LogError($"Failed to join room after {retryCount} attempts");
            return false;
        }
    }
}
```

- [ ] **Step 2: `InGameRoom.cs`를 아래로 교체**

`AppStateMachine`을 생성자 주입(상태는 순수 C#이라 생성자 주입)하고, 성공 시 `MatchFound`를 발행한다. `RoomConnector`가 이미 자체 재시도를 하므로 기존 이중 재시도 루프(10×)를 제거한다. `UniTask`/`TimeSpan`을 더 안 쓰므로 `Cysharp.Threading.Tasks` using을 제거한다.

```csharp
using GameFramework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LOP
{
    public class InGameRoom : State<MatchEvent>
    {
        private readonly Func<CheckMatch> checkMatch;
        private readonly IUserDataStore userDataStore;
        private readonly RoomConnector roomConnector;
        private readonly AppStateMachine appStateMachine;

        public InGameRoom(Func<CheckMatch> checkMatch, IUserDataStore userDataStore, RoomConnector roomConnector, AppStateMachine appStateMachine)
        {
            this.checkMatch = checkMatch;
            this.userDataStore = userDataStore;
            this.roomConnector = roomConnector;
            this.appStateMachine = appStateMachine;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.RecheckRequested => checkMatch(),
                _ => this,
            };
        }

        protected override async Task<MatchEvent?> OnExecuteAsync(CancellationToken ct)
        {
            if (userDataStore.userLocation.locationDetail is not GameRoomLocationDetail gameRoomLocationDetail)
            {
                Debug.LogError("User is not in a game room.");
                return MatchEvent.RecheckRequested;
            }

            if (await roomConnector.TryToEnterRoomById(gameRoomLocationDetail.gameRoomId))
            {
                //  매치 진입 확정 → 앱 FSM이 Room 씬을 로드(InMatch). 씬 언로드로 이 매칭 FSM은
                //  LobbyLifetimeScope.OnDestroy에서 정리되므로 자기 전이는 하지 않는다(null 반환).
                appStateMachine.Fire(AppEvent.MatchFound);
                return null;
            }

            //  입장 실패 → 위치 재확인.
            return MatchEvent.RecheckRequested;
        }

        protected override MatchEvent? OnError(Exception e)
        {
            Debug.LogError($"Error while entering game room. Error: {e.Message}");
            return MatchEvent.RecheckRequested;
        }
    }
}
```

- [ ] **Step 3: 컴파일 검증**

`refresh_unity` → `read_console(types=["error"])` (클라).
Expected: 에러 0. (`InGameRoom` 생성자에 `AppStateMachine` 추가돼도 `LobbyLifetimeScope.RegisterState<InGameRoom>`는 DI가 자동 해소 — Lobby 스코프가 Root의 `AppStateMachine` 싱글턴을 주입.)

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Room/RoomConnector.cs Assets/Scripts/Matchmaking/MatchStateMachine/States/InGameRoom.cs
git commit -m "feat(flow): 매치 진입이 LoadScene 대신 MatchFound 발행"
```

---

### Task 6: 매치 종료/에러 — LOPRoom → Fire(MatchEnded)

**Files:**
- Modify: `Assets/Scripts/Room/LOPRoom.cs`

**Interfaces:**
- Consumes: `AppStateMachine`(Root 등록), `AppEvent.MatchEnded`.
- Produces: `GameOver` 상태 도달 시와 `Awake` 실패 시 씬을 직접 로드하지 않고 `appStateMachine.Fire(AppEvent.MatchEnded)` → FSM이 `FrontEnd`로 전이하며 `Lobby` 로드. (정상 종료는 `MatchEndedMessageHandler`가 이미 결과 스토어를 채워 둠 → `FrontEndCoordinator`가 결과 창 표시. 에러는 스토어가 비어 결과 창 안 뜸.)

- [ ] **Step 1: `AppStateMachine` 필드 주입 추가**

`LOPRoom`의 `[Inject]` 필드 블록(라인 16~21) 끝에 추가:

```csharp
        [Inject] private AppStateMachine appStateMachine;
```

- [ ] **Step 2: `Awake` catch의 LoadScene 교체**

라인 36~40 `catch` 블록을:

```csharp
            catch (Exception e)
            {
                Debug.LogError(e);
                appStateMachine.Fire(AppEvent.MatchEnded);
            }
```

- [ ] **Step 3: `OnGameStateChanged`의 LoadScene 교체**

`case GameOver:` 블록(라인 142~145)을:

```csharp
                case GameOver:
                    Debug.Log("Game Over");
                    appStateMachine.Fire(AppEvent.MatchEnded);
                    break;
```

- [ ] **Step 4: 안 쓰는 using 제거**

파일 상단 `using UnityEngine.SceneManagement;`(라인 9)를 제거한다(더 이상 `LoadScene` 없음 — 라인 97~103의 주석 처리된 죽은 코드는 그대로 두되 using은 불필요).

- [ ] **Step 5: 컴파일 검증**

`refresh_unity` → `read_console(types=["error"])` (클라).
Expected: 에러 0.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Room/LOPRoom.cs
git commit -m "feat(flow): 매치 종료·에러가 LoadScene 대신 MatchEnded 발행"
```

---

### Task 7: 죽은 CheckLocationComponent 삭제

**Files:**
- Delete: `Assets/Scripts/Entrance/EntranceComponent/CheckLocationComponent.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Entrance/EntranceLifetimeScope.cs` (주석 처리된 등록 줄 제거)

**Interfaces:**
- Consumes: (없음)
- Produces: (없음 — 순수 제거). 이 삭제 후 `RoomConnector`의 라이브 소비자는 매칭 `InGameRoom` 하나만 남는다.

배경: `CheckLocationComponent`는 부트 시 위치 확인 후 재접속하던 컴포넌트로, 역할이 매칭 FSM(`CheckMatch`→`InGameRoom` / `InWaitingRoom`·`Idle`)에 완전 흡수됐고 `EntranceLifetimeScope`에서 등록이 주석 처리돼 이미 비활성이다(라이브 참조 0).

- [ ] **Step 1: `EntranceLifetimeScope.cs`에서 주석 줄 제거**

라인 16 `//builder.Register<IEntranceComponent, CheckLocationComponent>(Lifetime.Transient);`을 삭제한다. 나머지 등록(Login/CheckUser/JoinLobby/LoadMasterData)은 유지.

- [ ] **Step 2: 파일 삭제(.cs + .meta)**

```bash
git rm Assets/Scripts/Entrance/EntranceComponent/CheckLocationComponent.cs \
       Assets/Scripts/Entrance/EntranceComponent/CheckLocationComponent.cs.meta
```

- [ ] **Step 3: 컴파일 검증**

`refresh_unity` → `read_console(types=["error"])` (클라).
Expected: 에러 0. (삭제 후 CS2001 등 stale 참조 경고가 뜨면 `refresh_unity`로 재스캔.)

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Entrance/EntranceLifetimeScope.cs
git commit -m "refactor(flow): 죽은 CheckLocationComponent 삭제(역할은 매칭 FSM에 흡수)"
```

---

### Task 8: 엔드투엔드 플레이테스트 + 로드맵 갱신

**Files:**
- Modify: `docs/ROADMAP.md` (Slice A ✅ 처리 + 프론트엔드 플로우 트랙 완료 표시)

**Interfaces:**
- Consumes: 전체 슬라이스 결과.
- Produces: (없음)

- [ ] **Step 1: 로컬 서버 기동**

로컬 룸 서버를 띄운다(플레이 흐름에 필요). 인증 픽스처 주의: DB 리셋 시 게스트 유저 uuid를 서버 playerList에 추가해야 함(`local-test-auth-fixture` 참고).

- [ ] **Step 2: 엔드투엔드 루프 플레이테스트**

클라 플레이 → 다음 고리를 확인:
- 부트(로그인) → 로비 홈 진입 (`Boot→FrontEnd`, Lobby 로드)
- Play → 매칭 대기 오버레이 → 매치 잡힘 → 인게임 진입 (`FrontEnd→InMatch`, Room 로드)
- 게임 종료(GameOver) → 로비 복귀 + 결과 창 (`InMatch→FrontEnd`, Lobby 로드)

⚠️ 파킹된 백엔드 desync: 로컬은 룸 close가 스킵돼 결과→로비에서 자동 재접속이 낄 수 있다(예상된 제약). 그 구간이 걸리면 "재접속 루프 관찰됨(파킹된 백엔드 이슈)"로 기록하고 나머지 전이(부트→로비, 로비→인게임)의 정상 동작을 확인하는 것으로 갈음한다.

`read_console(types=["error"])`로 예외 0 확인.

- [ ] **Step 3: ROADMAP 갱신**

`docs/ROADMAP.md`의 "▶ 다음 — 프론트엔드 플로우 골격 (Slice A~D)" 섹션에서 Slice A 항목을 `▶`에서 `✅`로 바꾸고 한 줄 요약(AppStateMachine 신설·LoadScene 4곳 일원화·CheckLocationComponent 삭제 + spec/plan 링크)을 적는다. 트랙 전체(A~D) 완료를 표시한다.

- [ ] **Step 4: 커밋**

```bash
git add docs/ROADMAP.md
git commit -m "docs(roadmap): Slice A 완료 — 프론트엔드 플로우 골격 트랙 종결"
```

---

## Self-Review (작성자 확인 완료)

- **Spec 커버리지**: AppStateMachine(Task 2)·ISceneLoader(Task 1)·Root 등록/기동(Task 3)·신호 4곳 재배선(Task 4 부트 / Task 5 매치진입 / Task 6 매치종료·에러)·RoomConnector 로드 제거(Task 5)·CheckLocationComponent 삭제(Task 7)·테스트/플레이테스트(Task 8). spec의 모든 컴포넌트 요약 행에 대응 태스크 있음.
- **Placeholder**: 없음. 모든 코드 스텝에 실제 코드 포함.
- **타입 일관성**: `AppEvent`(3값)·`ISceneLoader.Load(string)`·`AppStateMachine(ISceneLoader)`·상태 생성자 시그니처가 태스크 간 일치. `TryToEnterRoomById(string, int)->Task<bool>` 시그니처는 Task 5 전후 불변. `InGameRoom` 생성자에 `AppStateMachine` 추가는 `RegisterState<InGameRoom>` DI가 자동 해소.
- **순서 안전성**: Task 1~3은 machinery만(옛 LoadScene 경로 그대로라 회귀 없음, FSM은 Boot에 idle). Task 4~6이 각 Fire 사이트를 개별 교체. 중간 상태에서도 컴파일·실행 가능(옛/새 경로 혼재해도 각자 씬 로드). 완주 후 일관.
