# UserLocationService Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 매치메이킹 FSM과 UI가 각자 하던 유저 위치 조회를 `UserLocationService` 하나로 모으고, 그 서비스가 티켓 id도 소유하게 한다.

**Architecture:** 새 Root 싱글턴 `UserLocationService`가 조회·재시도·폴링 루프·티켓 id를 소유한다. 값의 집은 기존 `UserDataStore`에 그대로 두되 `ReactiveProperty`로 바꿔 변화를 알릴 수 있게 하고, 서비스가 그것을 재노출한다. FSM 상태와 UI는 서비스만 보고 `WebAPI`를 직접 부르지 않는다. FSM 구조·상태 개수·상태 이름은 바뀌지 않는다.

**Tech Stack:** Unity / C# / VContainer(DI) / R3(반응형) / UniTask(비동기) / MessagePipe(pub-sub)

**Spec:** `docs/superpowers/specs/2026-08-14-user-location-service-design.md`

## Global Constraints

- **유닛 테스트 없음.** 클라에는 `Assets/Tests`도 asmdef도 없어 EditMode를 붙일 수 없다. **테스트를 위해 코드를 GameFramework로 쪼개지 않는다**(배치는 성격으로 정한다 — 사용자 결정). 각 태스크의 검증은 **컴파일 확인**이고, 마지막에 인게임 시나리오를 돈다.
- **컴파일 확인 방법:** UnityMCP는 이 프로젝트에서 **매 호출에 `unity_instance`를 명시**해야 한다. `mcpforunity://instances`를 읽어 `name`이 `LeagueOfPhysical-Client`인 인스턴스의 전체 `id`(`Name@hash`)를 얻고, 그 id로 콘솔을 읽어 **에러 0건**을 확인한다. 서버 인스턴스를 대상으로 조작하지 않는다.
- **브랜치:** `feature/user-location-service`. main에 직접 커밋 금지. 워크트리는 만들지 않는다(연결된 Unity 에디터가 main 체크아웃을 보므로, 여기서 편집해야 컴파일 검증이 된다).
- **World 타입 풀 한정 규칙은 이 슬라이스와 무관**(World Core를 안 건드림).
- **값 동치가 목표.** Task 5가 고치는 "요청 직후 취소" 하나를 빼면 겉보기 동작이 달라지면 안 된다.
- **네임스페이스는 평면 `namespace LOP`** (UI는 `LOP.UI`). 새 파일도 동일.
- **`Location`은 enum 이름이다.** 서비스가 노출하는 프로퍼티 이름은 `UserLocation`으로 한다 — `Location`으로 두면 클래스 안에서 `Location.Matchmaking`이 프로퍼티로 해석돼 컴파일이 깨진다.

---

### Task 1: 스토어를 관찰 가능하게

**Files:**
- Modify: `Assets/Scripts/Stores/IUserDataStore.cs`
- Modify: `Assets/Scripts/Stores/UserDataStore.cs`
- Modify: `Assets/Scripts/Matchmaking/MatchStateMachine/States/InGameRoom.cs:35`
- Modify: `Assets/Scripts/Matchmaking/MatchStateMachine/States/CancelMatchmaking.cs:34`

**Interfaces:**
- Consumes: (없음 — 첫 태스크)
- Produces: `IUserDataStore.userLocation`이 `R3.ReadOnlyReactiveProperty<UserLocation>` 타입이 된다. 값 읽기는 `.CurrentValue`, 변화 구독은 `.Subscribe(...)`.

> **왜:** 지금 `userLocation`은 평범한 프로퍼티라 "바뀌었다"를 알릴 수단이 없다. 그래서 소비자가 스토어 대신 HTTP 응답 DTO를 구독하고 있다(Task 6에서 정리). 여기서 알림 수단을 만든다.

- [ ] **Step 1: 인터페이스를 읽기 전용 관찰 프로퍼티로 바꾼다**

`Assets/Scripts/Stores/IUserDataStore.cs` 전체를 아래로 교체:

```csharp
using GameFramework;
using R3;
using UnityEngine;

namespace LOP
{
    public interface IUserDataStore : IDataStore
    {
        User user { get; set; }
        UserProfile userProfile { get; set; }
        //  위치는 바뀌는 걸 알아야 하는 소비자가 있어 관찰 가능하게 노출한다. 쓰기는 스토어 안에서만.
        ReadOnlyReactiveProperty<UserLocation> userLocation { get; }
        System.Collections.Generic.IReadOnlyDictionary<int, UserStats> userStatsByQueueId { get; }
    }
}
```

- [ ] **Step 2: 스토어 구현을 `ReactiveProperty`로 바꾼다**

`Assets/Scripts/Stores/UserDataStore.cs`에서 다음 네 곳을 고친다.

① `using`에 R3 추가 (파일 상단):

```csharp
using System;
using System.Collections.Generic;
using MessagePipe;
using R3;
```

② 프로퍼티 선언 교체 — 기존 `public UserLocation userLocation { get; set; } = new UserLocation();` 를 삭제하고:

```csharp
        private readonly ReactiveProperty<UserLocation> _userLocation = new(new UserLocation());
        public ReadOnlyReactiveProperty<UserLocation> userLocation => _userLocation;
```

③ 응답 핸들러 — `HandleGetUserLocation` 본문 교체:

```csharp
        private void HandleGetUserLocation(GetUserLocationResponse response)
        {
            _userLocation.Value = MapperConfig.mapper.Map<UserLocation>(response.userLocation);
        }
```

④ `Clear()`와 `Dispose()`:

```csharp
        public void Dispose()
        {
            subscriptions.Dispose();
            _userLocation.Dispose();
        }
```

```csharp
        public void Clear()
        {
            user = new User();
            userProfile = new UserProfile();
            _userLocation.Value = new UserLocation();
            statsByQueueId.Clear();
        }
```

- [ ] **Step 3: 기존 읽기 2곳을 `.CurrentValue`로 고친다**

`InGameRoom.cs`의 35번째 줄:

```csharp
            if (userDataStore.userLocation.CurrentValue.locationDetail is not GameRoomLocationDetail gameRoomLocationDetail)
```

`CancelMatchmaking.cs`의 34번째 줄:

```csharp
            if (userDataStore.userLocation.CurrentValue.locationDetail is not MatchmakingLocationDetail matchmakingLocationDetail)
```

> 이 두 줄은 Task 5에서 서비스를 보도록 다시 바뀐다. 지금은 **컴파일을 통과시키기 위한 최소 수정**이다.

- [ ] **Step 4: 컴파일 확인**

`mcpforunity://instances`에서 `LeagueOfPhysical-Client`의 id를 얻고, 그 `unity_instance`로 콘솔을 읽어 **컴파일 에러 0건**을 확인한다.

기대: 에러 없음. `userLocation`을 값으로 읽던 다른 곳이 남아 있으면 여기서 드러난다 — 드러나면 `.CurrentValue`를 붙여 고친다.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Stores/IUserDataStore.cs Assets/Scripts/Stores/UserDataStore.cs Assets/Scripts/Matchmaking/MatchStateMachine/States/InGameRoom.cs Assets/Scripts/Matchmaking/MatchStateMachine/States/CancelMatchmaking.cs
git commit -m "refactor(store): 유저 위치를 관찰 가능하게 (ReactiveProperty)"
```

---

### Task 2: `UserLocationService` 신설 + DI 등록

**Files:**
- Create: `Assets/Scripts/Matchmaking/IUserLocationService.cs`
- Create: `Assets/Scripts/Matchmaking/UserLocationService.cs`
- Modify: `Assets/Scripts/RootLifetimeScope.cs` (기존 `RoomConnector` 등록 줄 근처)

**Interfaces:**
- Consumes: `IUserDataStore.userLocation` (Task 1)
- Produces:
  - `IUserLocationService.UserLocation` → `ReadOnlyReactiveProperty<UserLocation>`
  - `IUserLocationService.TicketId` → `string` (매칭 중이 아니면 null)
  - `IUserLocationService.Faulted` → `Observable<Unit>` (조회를 연속 실패해 폴링을 포기했다)
  - `IUserLocationService.RefreshAsync(CancellationToken)` → `UniTask<bool>` (1회 조회 + 재시도, 성공 여부)
  - `IUserLocationService.OnMatchmakingRequested(string ticketId)` → `void`

> **이 태스크는 소비자를 아직 안 바꾼다.** 서비스만 만들어 등록하고, Task 3~6에서 하나씩 옮겨 붙인다.

- [ ] **Step 1: 인터페이스 파일 작성**

`Assets/Scripts/Matchmaking/IUserLocationService.cs`:

```csharp
using Cysharp.Threading.Tasks;
using R3;
using System.Threading;

namespace LOP
{
    /// <summary>
    /// 유저 위치를 서버에 물어보는 유일한 곳. 폴링 루프·재시도 정책·매칭 티켓 id를 소유하고,
    /// 위치는 R3로 노출한다. 소비자(FSM 상태·UI)는 여기만 보고 WebAPI를 직접 부르지 않는다.
    /// </summary>
    public interface IUserLocationService
    {
        /// <summary>현재 위치 + 변화 알림. 구독하면 현재 값부터 흘러온다.</summary>
        ReadOnlyReactiveProperty<UserLocation> UserLocation { get; }

        /// <summary>매칭 대기 중이면 그 티켓 id, 아니면 null.</summary>
        string TicketId { get; }

        /// <summary>조회를 연속 실패해 폴링을 포기했다. 위치를 더는 못 믿는다는 신호.</summary>
        Observable<Unit> Faulted { get; }

        /// <summary>지금 한 번 조회한다(실패 시 재시도). 성공하면 true.</summary>
        UniTask<bool> RefreshAsync(CancellationToken ct = default);

        /// <summary>매칭 요청이 성공했을 때 응답으로 받은 티켓 id를 넘긴다. 폴링도 함께 시작된다.</summary>
        void OnMatchmakingRequested(string ticketId);
    }
}
```

- [ ] **Step 2: 구현 파일 작성**

`Assets/Scripts/Matchmaking/UserLocationService.cs`:

```csharp
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Threading;
using UnityEngine;

namespace LOP
{
    public sealed class UserLocationService : IUserLocationService, IDisposable
    {
        private const int RefreshAttempts = 3;                //  1회 조회가 실패하면 이만큼까지 다시 시도
        private const int RetryIntervalSeconds = 1;
        private const int PollIntervalSeconds = 1;
        private const int MaxConsecutivePollFailures = 5;     //  폴링이 이만큼 내리 실패하면 포기

        private readonly IUserDataStore userDataStore;
        private readonly Subject<Unit> faulted = new();
        private readonly IDisposable locationSubscription;

        private CancellationTokenSource pollCts;
        private string requestedTicketId;

        public UserLocationService(IUserDataStore userDataStore)
        {
            this.userDataStore = userDataStore;
            //  폴링을 켜고 끄는 판단은 서비스가 한다 — 호출자에게 넘기면 정책이 다시 흩어진다.
            locationSubscription = userDataStore.userLocation.Subscribe(OnUserLocationChanged);
        }

        public ReadOnlyReactiveProperty<UserLocation> UserLocation => userDataStore.userLocation;

        public Observable<Unit> Faulted => faulted;

        public string TicketId
        {
            get
            {
                if (UserLocation.CurrentValue.locationDetail is MatchmakingLocationDetail detail)
                {
                    return detail.matchmakingTicketId;
                }

                //  방금 요청해서 서버 위치가 아직 안 따라온 구간에는 응답으로 받은 id를 쓴다.
                return requestedTicketId;
            }
        }

        public async UniTask<bool> RefreshAsync(CancellationToken ct = default)
        {
            for (int attempt = 1; attempt <= RefreshAttempts; attempt++)
            {
                if (await TryFetchAsync(ct))
                {
                    return true;
                }

                Debug.LogError($"Failed to retrieve user location. (attempt {attempt}/{RefreshAttempts})");

                if (attempt < RefreshAttempts)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(RetryIntervalSeconds), cancellationToken: ct);
                }
            }

            return false;
        }

        public void OnMatchmakingRequested(string ticketId)
        {
            requestedTicketId = ticketId;
            StartPolling();
        }

        public void Dispose()
        {
            StopPolling();
            locationSubscription.Dispose();
            faulted.Dispose();
        }

        //  조회 1회. 값 반영은 스토어가 응답 구독으로 하므로 여기서는 성공 여부만 돌려준다.
        private async UniTask<bool> TryFetchAsync(CancellationToken ct)
        {
            try
            {
                var getUserLocation = await WebAPI.GetUserLocation(userDataStore.user.id, ct);
                return getUserLocation.code == ResponseCode.SUCCESS;
            }
            catch (GameFramework.Http.HttpRequestException e)
            {
                Debug.LogWarning($"User location request failed. Error: {e.Message}");
                return false;
            }
        }

        private void OnUserLocationChanged(UserLocation userLocation)
        {
            if (userLocation.location == Location.Matchmaking)
            {
                StartPolling();
                return;
            }

            //  매칭을 벗어났으면 들고 있던 티켓 id는 더 이상 유효하지 않다.
            requestedTicketId = null;
            StopPolling();
        }

        private void StartPolling()
        {
            if (pollCts != null)
            {
                return;
            }

            pollCts = new CancellationTokenSource();
            PollLoopAsync(pollCts.Token).Forget();
        }

        private void StopPolling()
        {
            if (pollCts == null)
            {
                return;
            }

            pollCts.Cancel();
            pollCts.Dispose();
            pollCts = null;
        }

        private async UniTaskVoid PollLoopAsync(CancellationToken ct)
        {
            int consecutiveFailures = 0;

            try
            {
                while (ct.IsCancellationRequested == false)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), cancellationToken: ct);

                    if (await TryFetchAsync(ct))
                    {
                        consecutiveFailures = 0;
                        continue;
                    }

                    if (++consecutiveFailures >= MaxConsecutivePollFailures)
                    {
                        Debug.LogError($"Giving up user location polling after {consecutiveFailures} failures.");
                        StopPolling();
                        faulted.OnNext(Unit.Default);
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //  폴링이 멈춰서 취소됨 — 정상.
            }
        }
    }
}
```

- [ ] **Step 3: DI 등록**

`Assets/Scripts/RootLifetimeScope.cs`에서 `builder.Register<RoomConnector>(Lifetime.Transient);` **바로 위**에 추가:

```csharp
            //  유저 위치를 물어보는 유일한 곳. 로딩 VM이 Root 싱글턴이고 씬 경계(로비→룸)를 넘으므로
            //  이것도 Root에 둔다. 로비 스코프의 FSM 상태들이 이 인스턴스를 주입받는다.
            builder.Register<UserLocationService>(Lifetime.Singleton)
                .As<IUserLocationService>()
                .AsSelf();
```

- [ ] **Step 4: 컴파일 확인**

`unity_instance`를 클라로 지정해 콘솔 읽기 → **에러 0건**.

기대: 에러 없음. 아직 아무도 이 서비스를 안 쓰므로 동작 변화도 없다.

- [ ] **Step 5: `.meta` 확인 후 커밋**

Unity가 새 `.cs` 두 개의 `.meta`를 생성했는지 확인하고 **함께** 커밋한다(누락 시 참조가 깨진다).

```bash
git add Assets/Scripts/Matchmaking/IUserLocationService.cs Assets/Scripts/Matchmaking/IUserLocationService.cs.meta Assets/Scripts/Matchmaking/UserLocationService.cs Assets/Scripts/Matchmaking/UserLocationService.cs.meta Assets/Scripts/RootLifetimeScope.cs
git commit -m "feat(matchmaking): UserLocationService 신설 (아직 소비자 없음)"
```

---

### Task 3: 1회 조회를 서비스로 — `CheckMatch`, `LoadUserComponent`

**Files:**
- Modify: `Assets/Scripts/Matchmaking/MatchStateMachine/States/CheckMatch.cs`
- Modify: `Assets/Scripts/Entrance/EntranceComponent/LoadUserComponent.cs`

**Interfaces:**
- Consumes: `IUserLocationService.RefreshAsync`, `IUserLocationService.UserLocation` (Task 2)
- Produces: (없음 — 소비자 전환)

> **왜:** `CheckMatch`가 자기 재시도 루프(3회·1초)를 들고 있다. 그 정책은 Task 2에서 서비스로 옮겨졌으므로 여기서는 삭제한다.

- [ ] **Step 1: `CheckMatch`를 서비스 호출로 바꾼다**

`CheckMatch.cs` 전체를 아래로 교체:

```csharp
using GameFramework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LOP
{
    public class CheckMatch : State<MatchEvent>
    {
        private readonly Func<InGameRoom> inGameRoom;
        private readonly Func<InMatchmaking> inMatchmaking;
        private readonly Func<Idle> idle;
        private readonly IUserLocationService userLocationService;

        public CheckMatch(Func<InGameRoom> inGameRoom, Func<InMatchmaking> inMatchmaking, Func<Idle> idle, IUserLocationService userLocationService)
        {
            this.inGameRoom = inGameRoom;
            this.inMatchmaking = inMatchmaking;
            this.idle = idle;
            this.userLocationService = userLocationService;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.LocationIsGameRoom => inGameRoom(),
                MatchEvent.LocationIsMatchmaking => inMatchmaking(),
                MatchEvent.LocationIsNone => idle(),
                _ => this,
            };
        }

        protected override async Task<MatchEvent?> OnExecuteAsync(CancellationToken ct)
        {
            //  재시도는 서비스가 한다. 여기서는 결과를 전이로만 옮긴다.
            if (await userLocationService.RefreshAsync(ct))
            {
                return ToEvent(userLocationService.UserLocation.CurrentValue.location);
            }

            //  반복 실패 → 초기 화면(Idle)으로 안전 복귀.
            return MatchEvent.LocationIsNone;
        }

        protected override MatchEvent? OnError(Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to retrieve user information. Error: {e.Message}");
            return MatchEvent.LocationIsNone;
        }

        private static MatchEvent ToEvent(Location location)
        {
            return location switch
            {
                Location.Matchmaking => MatchEvent.LocationIsMatchmaking,
                Location.GameRoom => MatchEvent.LocationIsGameRoom,
                _ => MatchEvent.LocationIsNone,
            };
        }
    }
}
```

> `MAX_ATTEMPTS`·`RetryInterval` 상수와 `IUserDataStore` 의존이 사라진 것을 확인한다. 남아 있으면 정책이 두 곳에 있는 것이다.

- [ ] **Step 2: `LoadUserComponent`가 서비스를 쓰게 한다**

`LoadUserComponent.cs`에서 생성자와 위치 조회 한 줄을 바꾼다:

```csharp
        private readonly IUserDataStore userDataStore;
        private readonly IUserLocationService userLocationService;

        public LoadUserComponent(IUserDataStore userDataStore, IUserLocationService userLocationService)
        {
            this.userDataStore = userDataStore;
            this.userLocationService = userLocationService;
        }
```

그리고 `await WebAPI.GetUserLocation(userId);` 를 아래로 교체:

```csharp
            await userLocationService.RefreshAsync();
```

- [ ] **Step 3: 컴파일 확인**

`unity_instance`를 클라로 지정해 콘솔 읽기 → **에러 0건**.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Matchmaking/MatchStateMachine/States/CheckMatch.cs Assets/Scripts/Entrance/EntranceComponent/LoadUserComponent.cs
git commit -m "refactor(matchmaking): 1회 위치 조회를 UserLocationService로"
```

---

### Task 4: 폴링을 서비스로 — `InMatchmaking`

**Files:**
- Modify: `Assets/Scripts/Matchmaking/MatchStateMachine/States/InMatchmaking.cs`

**Interfaces:**
- Consumes: `IUserLocationService.UserLocation`, `IUserLocationService.Faulted` (Task 2)
- Produces: (없음 — 소비자 전환)

> **왜:** `InMatchmaking`이 1초 폴링 루프와 실패 카운트를 직접 돌린다. Boss Room의 상태들이 그렇듯 **상태는 입력을 받아야** 한다. 폴링은 Task 2에서 서비스로 옮겨졌으니 여기서는 "위치가 매칭을 벗어나는 순간"만 기다린다.
>
> **R3 연산자(`Where`/`Merge`/`FirstAsync`)를 쓰지 않는다.** 이 코드베이스는 `Subscribe`와 `ReactiveProperty`만 써 왔고, 검증 안 된 연산자 이름으로 컴파일을 깨뜨리지 않기 위해서다.

- [ ] **Step 1: `InMatchmaking`을 구독 대기로 바꾼다**

`InMatchmaking.cs` 전체를 아래로 교체:

```csharp
using Cysharp.Threading.Tasks;
using GameFramework;
using R3;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LOP
{
    public class InMatchmaking : State<MatchEvent>
    {
        private readonly Func<CancelMatchmaking> cancelMatchmaking;
        private readonly Func<InGameRoom> inGameRoom;
        private readonly Func<Idle> idle;
        private readonly IUserLocationService userLocationService;

        public InMatchmaking(Func<CancelMatchmaking> cancelMatchmaking, Func<InGameRoom> inGameRoom, Func<Idle> idle, IUserLocationService userLocationService)
        {
            this.cancelMatchmaking = cancelMatchmaking;
            this.inGameRoom = inGameRoom;
            this.idle = idle;
            this.userLocationService = userLocationService;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.CancelClicked => cancelMatchmaking(),
                MatchEvent.LocationIsGameRoom => inGameRoom(),
                MatchEvent.LocationIsNone => idle(),
                _ => this,
            };
        }

        protected override async Task<MatchEvent?> OnExecuteAsync(CancellationToken ct)
        {
            //  폴링은 서비스가 돈다. 여기서는 위치가 매칭을 벗어나는 순간만 기다린다.
            //  구독하면 현재 값부터 흘러오므로, 진입 시점에 이미 벗어나 있으면 즉시 전이한다.
            var completion = new UniTaskCompletionSource<MatchEvent>();

            using var cancellation = ct.Register(() => completion.TrySetCanceled());

            using var locationSubscription = userLocationService.UserLocation.Subscribe(userLocation =>
            {
                switch (userLocation.location)
                {
                    case Location.GameRoom:
                        completion.TrySetResult(MatchEvent.LocationIsGameRoom);
                        break;

                    case Location.Matchmaking:
                        break;   //  아직 대기 중.

                    default:
                        completion.TrySetResult(MatchEvent.LocationIsNone);
                        break;
                }
            });

            //  서비스가 조회를 포기했으면 위치를 더는 못 믿으므로 초기 화면으로.
            using var faultedSubscription = userLocationService.Faulted.Subscribe(_ =>
            {
                completion.TrySetResult(MatchEvent.LocationIsNone);
            });

            return await completion.Task;
        }

        protected override MatchEvent? OnError(Exception e)
        {
            Debug.LogError($"Unexpected error while waiting. Error: {e.Message}");
            return MatchEvent.LocationIsNone;
        }
    }
}
```

> `CHECK_INTERVAL`·`MAX_CONSECUTIVE_FAILURES` 상수와 `WebAPI` 호출, `IUserDataStore` 의존이 모두 사라진 것을 확인한다.

- [ ] **Step 2: 컴파일 확인**

`unity_instance`를 클라로 지정해 콘솔 읽기 → **에러 0건**.

특히 확인할 것: `ct.Register(...)`의 반환형은 `CancellationTokenRegistration`(구조체)이고 `using`이 가능하다. `Subscribe`의 반환형은 `IDisposable`이다.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Matchmaking/MatchStateMachine/States/InMatchmaking.cs
git commit -m "refactor(matchmaking): InMatchmaking 폴링을 서비스 구독으로"
```

---

### Task 5: 티켓 id 배선 — `RequestMatchmaking`, `CancelMatchmaking`, `InGameRoom`

**Files:**
- Modify: `Assets/Scripts/Matchmaking/MatchStateMachine/States/RequestMatchmaking.cs`
- Modify: `Assets/Scripts/Matchmaking/MatchStateMachine/States/CancelMatchmaking.cs`
- Modify: `Assets/Scripts/Matchmaking/MatchStateMachine/States/InGameRoom.cs`

**Interfaces:**
- Consumes: `IUserLocationService.OnMatchmakingRequested`, `IUserLocationService.TicketId`, `IUserLocationService.UserLocation` (Task 2)
- Produces: (없음 — 소비자 전환)

> **왜:** `MatchmakingResponse.ticketId`를 받아놓고 버려서, 취소가 "마지막 폴링 결과"에 의존한다. 요청 직후 취소하면 폴링이 아직 안 돌아 `"User is not in matchmaking."` 후 위치 재확인을 한 바퀴 돈다. **이 태스크가 유일하게 겉보기 동작을 바꾼다.**

- [ ] **Step 1: `RequestMatchmaking`이 티켓 id를 서비스에 넘긴다**

`RequestMatchmaking.cs`에서 생성자 의존을 바꾸고:

```csharp
        private readonly Func<InMatchmaking> inMatchmaking;
        private readonly Func<CheckMatch> checkMatch;
        private readonly IMatchmakingDataStore matchmakingDataStore;
        private readonly IUserLocationService userLocationService;

        public RequestMatchmaking(Func<InMatchmaking> inMatchmaking, Func<CheckMatch> checkMatch, IMatchmakingDataStore matchmakingDataStore, IUserLocationService userLocationService)
        {
            this.inMatchmaking = inMatchmaking;
            this.checkMatch = checkMatch;
            this.matchmakingDataStore = matchmakingDataStore;
            this.userLocationService = userLocationService;
        }
```

`OnExecuteAsync`의 성공 반환 직전에 한 줄 추가:

```csharp
            if (requestMatchmaking.code != ResponseCode.SUCCESS)
            {
                Debug.LogError($"Matchmaking request failed. Response code: {requestMatchmaking.code}");
                return MatchEvent.MatchRequestFailed;
            }

            //  받은 티켓 id를 서비스에 넘긴다 — 취소가 위치 폴링을 기다리지 않게.
            userLocationService.OnMatchmakingRequested(requestMatchmaking.ticketId);

            return MatchEvent.MatchRequestSucceeded;
```

- [ ] **Step 2: `CancelMatchmaking`이 서비스의 티켓 id를 쓴다**

`CancelMatchmaking.cs`에서 `IUserDataStore` 의존을 `IUserLocationService`로 교체:

```csharp
        private readonly Func<InGameRoom> inGameRoom;
        private readonly Func<CheckMatch> checkMatch;
        private readonly IUserLocationService userLocationService;

        public CancelMatchmaking(Func<InGameRoom> inGameRoom, Func<CheckMatch> checkMatch, IUserLocationService userLocationService)
        {
            this.inGameRoom = inGameRoom;
            this.checkMatch = checkMatch;
            this.userLocationService = userLocationService;
        }
```

`OnExecuteAsync` 앞부분의 캐스팅 분기를 교체:

```csharp
            string ticketId = userLocationService.TicketId;
            if (string.IsNullOrEmpty(ticketId))
            {
                Debug.LogError("User is not in matchmaking.");
                return MatchEvent.RecheckRequested;
            }

            var cancelMatchmaking = await WebAPI.CancelMatchmaking(ticketId);
```

이하 `switch (cancelMatchmaking.code)` 블록은 **그대로 둔다**.

- [ ] **Step 3: `InGameRoom`이 서비스를 본다**

`InGameRoom.cs`에서 `IUserDataStore` 의존을 `IUserLocationService`로 교체:

```csharp
        private readonly Func<CheckMatch> checkMatch;
        private readonly IUserLocationService userLocationService;
        private readonly RoomConnector roomConnector;
        private readonly AppStateMachine appStateMachine;

        public InGameRoom(Func<CheckMatch> checkMatch, IUserLocationService userLocationService, RoomConnector roomConnector, AppStateMachine appStateMachine)
        {
            this.checkMatch = checkMatch;
            this.userLocationService = userLocationService;
            this.roomConnector = roomConnector;
            this.appStateMachine = appStateMachine;
        }
```

`OnExecuteAsync` 첫 분기를 교체:

```csharp
            if (userLocationService.UserLocation.CurrentValue.locationDetail is not GameRoomLocationDetail gameRoomLocationDetail)
```

- [ ] **Step 4: 컴파일 확인**

`unity_instance`를 클라로 지정해 콘솔 읽기 → **에러 0건**.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Matchmaking/MatchStateMachine/States/RequestMatchmaking.cs Assets/Scripts/Matchmaking/MatchStateMachine/States/CancelMatchmaking.cs Assets/Scripts/Matchmaking/MatchStateMachine/States/InGameRoom.cs
git commit -m "fix(matchmaking): 요청 응답의 티켓 id를 보관해 취소가 폴링을 안 기다리게"
```

---

### Task 6: UI를 도메인으로 + 인게임 검증

**Files:**
- Modify: `Assets/Scripts/UI/Loading/MatchLoadingViewModel.cs`

**Interfaces:**
- Consumes: `IUserLocationService.UserLocation` (Task 2)
- Produces: (없음 — 마지막 태스크)

> **왜:** VM이 HTTP 응답 DTO(`GetUserLocationResponse`)를 구독한다. 통신 계약이 바뀌면 UI가 깨지는 결합이다. 도메인 값을 보게 바꾼다.

- [ ] **Step 1: VM이 서비스를 구독하게 바꾼다**

`MatchLoadingViewModel.cs`에서 `using MessagePipe;`를 지우고, 생성자와 핸들러를 교체:

```csharp
        public MatchLoadingViewModel(IUserLocationService userLocationService)
        {
            _subscription = userLocationService.UserLocation.Subscribe(OnUserLocation);
        }

        private void OnUserLocation(UserLocation userLocation)
        {
            _inGameRoom = userLocation.location == Location.GameRoom;
            // 룸을 벗어나면(연결 실패로 로비 복귀 등) 다음 매치를 위해 gameLive를 리셋한다.
            if (!_inGameRoom) _gameLive = false;
            Recompute();
        }
```

클래스 상단 XML 주석의 *"백엔드 유저 위치(GameRoom 여부)와 게임 라이브 사실을 조합해"* 는 그대로 맞으므로 두되, `GetUserLocationResponse`를 언급한 `RootLifetimeScope.cs`의 등록 주석은 아래처럼 고친다:

```csharp
            // 매치 진입 로딩 화면(룸 연결~게임 준비 구간을 연속으로 덮음).
            // VM은 UserLocationService의 위치를 관찰해 IsLoading을 파생하고,
            // 코디네이터가 그 신호로 로딩 창을 여닫는다(씬 경계를 넘어 뷰를 소유).
```

- [ ] **Step 2: 컴파일 확인**

`unity_instance`를 클라로 지정해 콘솔 읽기 → **에러 0건**.

- [ ] **Step 3: 죽은 배선이 남았는지 확인한다**

다음 검색이 **0건**이어야 한다(서비스 내부 제외):

```bash
grep -rn "WebAPI.GetUserLocation" Assets/Scripts --include=*.cs
grep -rn "ISubscriber<GetUserLocationResponse>" Assets/Scripts --include=*.cs
```

기대: 첫 번째는 `Assets/Scripts/Matchmaking/UserLocationService.cs` **한 줄만**, 두 번째는 `Assets/Scripts/Stores/UserDataStore.cs` **한 줄만**(스토어가 값을 채우는 경로는 유지).

`RootLifetimeScope.cs`의 `RegisterMessageBroker<GetUserLocationResponse>`는 **지우지 않는다** — 스토어가 아직 그 브로커로 값을 받는다.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/UI/Loading/MatchLoadingViewModel.cs Assets/Scripts/RootLifetimeScope.cs
git commit -m "refactor(ui): 로딩 VM이 DTO 대신 도메인 위치를 구독"
```

- [ ] **Step 5: 인게임 검증 — 6가지**

에디터에서 실제로 플레이하며 확인한다. **①②④⑥은 예전과 똑같아야 하고, ③만 달라져야 한다.**

1. 로그인 → 로비 진입. 콘솔 에러 0건
2. 매칭 요청 → 대기 → 매치 성사 → 게임 진입. **로딩 화면이 뜨고 게임 시작 시 닫힌다**
3. **매칭 요청 직후 즉시 취소** — `"User is not in matchmaking."` 로그 **없이** 한 번에 취소된다 ← **유일하게 달라지는 지점**
4. 대기 중(몇 초 후) 취소 — 예전과 동일
5. **게임 진입 후 위치 폴링이 멈춘다** — 게임 씬에서 `/location/` 요청이 더 이상 나가지 않는지 확인(네트워크 로그 또는 서비스에 임시 로그를 넣어 확인 후 제거)
6. 매치 종료 → 로비 복귀. 콘솔 에러 0건

- [ ] **Step 6: 최종 리뷰**

테스트가 없으므로 이게 주 안전망이다. `superpowers:requesting-code-review`로 **whole-branch 리뷰**(main 대비 전체 diff)를 돌린다. 특히 볼 것:

- 재시도·폴링 상수가 상태 쪽에 **남아 있지 않은지**(정책이 두 곳이면 실패)
- `Clear()`로 위치가 빈 값이 될 때 소비자 3곳(`InMatchmaking`·`MatchLoadingViewModel`·`InGameRoom`)이 어떻게 반응하는지
- `StopPolling()`이 `PollLoopAsync` 안에서 자기 토큰을 취소하는 경로에 이중 `Dispose`가 없는지

---

## 자체 검토

**1. spec 커버리지**

| spec 항목 | 태스크 |
|---|---|
| ① 스토어가 벙어리 | Task 1 |
| ② VM이 DTO 구독 | Task 6 |
| ③ 폴링 주인 없음 | Task 2(서비스) + Task 3(1회 조회) + Task 4(폴링) |
| ④ 티켓 id 버림 | Task 5 |
| `UserLocationService` Root Singleton 등록 | Task 2 Step 3 |
| 폴링 자동 시작/중단 | Task 2 `OnUserLocationChanged` |
| 위험: 폴링이 안 멈춤 | Task 6 Step 5-⑤ |
| 위험: 구독 전 값 변화 놓침 | Task 4 Step 1(구독 시 현재 값부터 흘러옴) |
| 위험: 정책 잔존 | Task 3·4 주석 + Task 6 Step 6 리뷰 항목 |
| 위험: `Clear()` 알림 | Task 6 Step 6 리뷰 항목 |
| 검증: 컴파일 + 리뷰 + 인게임 6단계 | 각 태스크 + Task 6 |

빠진 항목 없음.

**2. 플레이스홀더**: "TBD"·"적절히 처리"·"Task N과 유사" 없음. 모든 코드 단계에 실제 코드가 있다.

**3. 타입 일관성**

- `IUserLocationService.UserLocation` → `ReadOnlyReactiveProperty<UserLocation>` — Task 2에서 정의, Task 3·4·5·6에서 `.CurrentValue` / `.Subscribe`로 동일하게 사용
- `TicketId` → `string` — Task 2 정의, Task 5에서 `string.IsNullOrEmpty`로 사용
- `Faulted` → `Observable<Unit>` — Task 2 정의, Task 4에서 `.Subscribe(_ => ...)`로 사용
- `RefreshAsync` → `UniTask<bool>` — Task 2 정의, Task 3에서 `await ... ` 불리언으로 사용
- `OnMatchmakingRequested(string)` → Task 2 정의, Task 5에서 사용
- **spec과의 차이 1건**: spec은 프로퍼티를 `Location`으로 적었으나 `Location` enum과 충돌해 **`UserLocation`으로 바꿨다**. spec에도 반영할 것.
