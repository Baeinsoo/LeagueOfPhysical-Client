# MessageHandlerBase 통합 — 마커 인터페이스 제거 + 구독 배관 공유화

## 한 줄 요약

클·서에 중복된 빈 마커 인터페이스(`IGameMessageHandler`/`IRoomMessageHandler`)와 핸들러마다 복붙된 구독/해제 배관을 **GameFramework 공유 base class `MessageHandlerBase` 한 벌**로 흡수한다. 게임/룸 구분은 DI 스코프가 이미 하므로 타입으로 중복하지 않는다. 순수 리팩터 — 동작 무변화(값 동치).

## 배경 — 지금 뭐가 문제인가

메시지 핸들러는 "서버 패킷이 오면 이 함수를 실행"이 전부인데, 그걸 하려고 매 핸들러가 같은 배관을 반복한다:

```csharp
private IDisposable subscription;
public void Initialize() { subscription = sub.Subscribe(OnMsg); }  // 배관
public void Dispose()    { subscription?.Dispose(); }              // 배관
private void OnMsg(...) { /* 진짜 로직 */ }
```

세 가지 냄새가 겹쳐 있다:

1. **반복 배관 + 두 관용구 혼재.** 단일 구독 핸들러는 `IDisposable subscription` 필드 + `Dispose()`를 손으로 쓰고, 다중 구독 핸들러(`GameEntityMessageHandler`, `EntityBinder`)는 MessagePipe `DisposableBag` + `.AddTo(bag)`를 쓴다. 같은 개념을 두 방식으로 한다.

2. **똑같이 생긴 마커 2개.** `IGameMessageHandler`와 `IRoomMessageHandler`는 글자 하나 안 다르다(`: IInitializable, IDisposable`, 본문 빔). 게임/룸을 갈라주는 실제 주체는 **타입이 아니라 DI 스코프**(`GameLifetimeScope` vs `RoomLifetimeScope`가 각각 등록)다. 마커는 그 구분을 타입으로 한 번 더 표현할 뿐이고, 아무도 `IEnumerable<IGameMessageHandler>`로 모아 쓰지 않아(등록이 `RegisterEntryPoint<Concrete>()`) **기계적으로 하는 일이 0**이다.

3. **파일명이 클래스명과 불일치.** 파일은 `Game.Info.MessageHandler.cs`인데 클래스는 `GameInfoMessageHandler`다. C# 표준(파일명 = 첫 public 타입명, StyleCop `SA1649`)에 어긋난다. 점 표기의 그룹핑 의도는 이미 `Game/MessageHandler/` 폴더가 한다.

## 업계 표준 매핑 (비자명 결정의 근거)

- **pub/sub + 타입별 dispatch(현행 유지)** = 표준. MediatR notification, MassTransit, Prism EventAggregator, Cysharp MessagePipe가 모두 이 모양. `NetworkMessageDispatcher`의 리플렉션 없는 타입 라우팅은 IL2CPP 안전 표준. → **이벤트 방식 자체는 손대지 않는다.**
- **빈 마커 인터페이스로 스코프 구분** = 비표준(중복). 마커 인터페이스는 *그 타입으로 그룹을 resolve할 때만* 값을 한다("grouping type"). 여기선 안 쓰므로 장식이다. 구조가 동일한 둘을 모양만 다른 타입 2개로 나누는 대신, 구분을 스코프에 맡기고 **base class 한 벌**로 합치는 게 표준 정합.
- **`MessageHandlerBase` 이름** = 코드베이스·대화에서 이미 "메시지 핸들러"라 부르는 도메인 어휘. 메시지 핸들링은 범용 인프라 개념이라 앱 비종속 패키지(GameFramework)에 두어도 적절. (대안 `SubscriptionEntryPoint`는 VContainer "EntryPoint" 어휘 정합이나 기존 어휘와 멀어 기각.)
- **파일명 = 클래스명** = C# 표준(StyleCop SA1649).

## 결정 (brainstorm 확정)

| 항목 | 결정 |
|---|---|
| 적용 범위 | **클·서 둘 다** (대칭 — 최근 폴더 재편과 동일) |
| base 위치 | **GameFramework** 공유 한 벌 (GF가 이미 VContainer 참조 → 추가 의존 0) |
| 마커 인터페이스 | **둘 다 삭제** — 게임/룸 구분은 DI 스코프가 담당 |
| base 이름 | `MessageHandlerBase` |
| 파일명 | 클래스명과 일치시키기 (양쪽 repo) |
| 룸 `GameMessageHandler` | `RoomSessionMessageHandler`로 rename (하는 일=세션 세팅) |

## 설계

### 1. 공유 base class

`GameFramework/Runtime/Scripts/Messaging/MessageHandlerBase.cs` 신설:

```csharp
using System;
using System.Collections.Generic;

namespace GameFramework
{
    /// <summary>
    /// 스코프가 살아있는 동안 구독을 유지하고, 스코프 종료 시 한꺼번에 해제하는 DI 엔트리포인트 base.
    /// VContainer가 Initialize(구독)/Dispose(해제)를 구동한다.
    /// </summary>
    public abstract class MessageHandlerBase : VContainer.Unity.IInitializable, IDisposable
    {
        private readonly List<IDisposable> subscriptions = new();

        void VContainer.Unity.IInitializable.Initialize() => Subscribe();

        // 구독 외 추가 teardown이 필요한 핸들러(예: 서버 GameInfoMessageHandler의 runner 리스너 해제)는
        // 이 메서드를 override하고 base.Dispose()를 먼저 호출한다.
        public virtual void Dispose()
        {
            foreach (var s in subscriptions) s.Dispose();
            subscriptions.Clear();
        }

        /// <summary>구독을 걸고 그 결과를 <see cref="Track"/>로 등록한다. 스코프 종료 시 자동 해제된다.</summary>
        protected abstract void Subscribe();

        /// <summary>구독 핸들(IDisposable)을 스코프 수명에 묶는다.</summary>
        protected void Track(IDisposable subscription) => subscriptions.Add(subscription);
    }
}
```

**설계 근거:**
- **MessagePipe 비의존.** 구독 결과(`IDisposable`)만 모으면 되고 `ISubscriber<T>` 같은 MessagePipe 타입은 자식만 만진다 → GF에 MessagePipe 참조를 추가하지 않는다(GF는 현재 VContainer + World만 참조). 구독 보관은 순수 `List<IDisposable>`.
- **⚠️ gotcha — `IInitializable` 이름 충돌.** GameFramework에 자체 `GameFramework.IInitializable`(`Lifecycle/`)이 있고, VContainer 구동에 필요한 건 `VContainer.Unity.IInitializable`이다. base 파일은 `namespace GameFramework` 안이라 bare `IInitializable`이 GF 것으로 해석된다 → **반드시 `VContainer.Unity.IInitializable`을 풀네임으로** 쓴다(`using VContainer.Unity;` 안 함).
- **등록 무변화.** base가 VContainer 인터페이스를 구현하므로 `builder.RegisterEntryPoint<Concrete>()`가 그대로 Initialize/Dispose를 구동한다. 설치자 변경 없음.

### 2. 핸들러 변환 (동작 무변화)

**단일 구독** — 배관 3줄 제거, `Subscribe` 한 줄로:

```csharp
public class GameInfoMessageHandler : MessageHandlerBase
{
    [Inject] ISubscriber<GameInfoToC> sub;   // 다른 [Inject]는 그대로
    // ...
    protected override void Subscribe() => Track(sub.Subscribe(OnGameInfoToC));
    private void OnGameInfoToC(GameInfoToC m) { /* 로직 그대로 */ }
}
```

**다중 구독** — `DisposableBag`/`.AddTo(bag)`를 `Track(...)` 여러 번으로 통일:

```csharp
protected override void Subscribe()
{
    Track(snapsSubscriber.Subscribe(OnEntitySnapsToC));
    Track(spawnSubscriber.Subscribe(OnEntitySpawnToC));
    Track(despawnSubscriber.Subscribe(OnEntityDespawnToC));
    // ...
}
```

각 핸들러의 `OnXxx` 로직 본문은 **한 줄도 바꾸지 않는다**. `Initialize`/`Dispose`/`subscription` 필드/`DisposableBag` 필드만 걷어낸다.

**구독 외 teardown이 있는 핸들러 (서버 `GameInfoMessageHandler` 하나뿐):** 이 핸들러는 `Initialize`에서 `runner.AddListener(this)`도 하고 `Dispose`에서 `runner.RemoveListener(this)`도 한다. 리스너 등록은 `Subscribe()`에 두고, 해제는 `Dispose()` override로 처리한다:

```csharp
protected override void Subscribe()
{
    Track(gameInfoSubscriber.Subscribe(OnGameInfoToS));
    runner.AddListener(this);
}

public override void Dispose()
{
    base.Dispose();               // 구독 해제
    runner.RemoveListener(this);  // 추가 teardown
}
```

### 3. 마커 삭제 + rename 인벤토리

**GameFramework (신규):**
- `+ Runtime/Scripts/Messaging/MessageHandlerBase.cs`

**클라이언트 (`IGameMessageHandler` 구현체 7, `IRoomMessageHandler` 1):**

| 파일 | 조치 |
|---|---|
| `Game/MessageHandler/IGameMessageHandler.cs` | 삭제 |
| `Room/MessageHandler/IRoomMessageHandler.cs` | 삭제 |
| `Game/MessageHandler/Game.Info.MessageHandler.cs` | → `GameInfoMessageHandler.cs`, base 변경 |
| `Game/MessageHandler/Game.Entity.MessageHandler.cs` | → `GameEntityMessageHandler.cs`, base 변경 |
| `Game/MessageHandler/Game.Input.MessageHandler.cs` | → `GameInputMessageHandler.cs`, base 변경 |
| `Game/MessageHandler/Game.InputTiming.MessageHandler.cs` | → `GameInputTimingMessageHandler.cs`, base 변경 |
| `Game/MessageHandler/Game.WorldEvent.MessageHandler.cs` | → `GameWorldEventMessageHandler.cs`, base 변경 |
| `Entity/EntityBinder.cs` | 파일명 유지, base 변경 (다중 구독) |
| `Game/PlayerHudCoordinator.cs` | 파일명 유지, base 변경 (단일 구독) |
| `Room/MessageHandler/GameMessageHandler.cs` | → `RoomSessionMessageHandler.cs` (클래스도 rename), base 변경 |

**서버 (`IGameMessageHandler` 구현체 4, `IRoomMessageHandler` 구현체 0):**

| 파일 | 조치 |
|---|---|
| `Game/MessageHandler/IGameMessageHandler.cs` | 삭제 |
| `Room/MessageHandler/IRoomMessageHandler.cs` | 삭제 (죽은 인터페이스 — 구현체 없음) |
| `Game/MessageHandler/Game.Info.MessageHandler.cs` | → `GameInfoMessageHandler.cs`, base 변경 |
| `Game/MessageHandler/Game.Entity.MessageHandler.cs` | → `GameEntityMessageHandler.cs`, base 변경 |
| `Game/MessageHandler/Game.Input.MessageHandler.cs` | → `GameInputMessageHandler.cs`, base 변경 |
| `Entity/EntityBinder.cs` | 파일명 유지, base 변경 |

> 파일 rename 시 `.meta`를 **함께** rename해 GUID를 보존한다(Unity 표준). 이 핸들러들은 MonoBehaviour가 아니라 씬/프리팹에서 GUID로 참조되지 않으므로 rename이 참조를 깨지 않는다. 클래스명 변경(`GameMessageHandler`→`RoomSessionMessageHandler`)은 등록부(`RoomLifetimeScope`)의 타입 참조도 함께 갱신한다.

### 4. 불변식 — 등록 순서 보존

`GameLifetimeScope`는 `EntityBinder`를 `PlayerHudCoordinator`보다 **먼저** 등록해야 한다(EntityBinder가 먼저 구독해 actor를 만들어야 후속 구독자가 읽음). `RegisterEntryPoint` 등록 순서 = Initialize 순서다. 이 리팩터는 **등록 순서를 건드리지 않는다.**

## 테스트

- **GameFramework EditMode** (`baegames.GameFramework.Runtime.Tests`): `MessageHandlerBase` 단위 테스트 1개.
  - fake 구독을 Track하는 테스트용 서브클래스로: `IInitializable.Initialize()` 호출 시 `Subscribe()`가 돌고, `Dispose()` 시 Track된 fake `IDisposable`들이 전부 `Dispose`되는지 검증. VContainer 컨테이너 불필요(인터페이스로 캐스트해 직접 호출).
  - Tests asmdef가 VContainer를 참조하는지 확인(캐스트에 필요). 없으면 추가.
- **클·서 컴파일**: UnityMCP `read_console`로 에러 0 확인(각 repo).
- **인게임 스모크**: 스폰(엔티티 생성 = `GameInfoMessageHandler`/`EntityBinder`), 세션 세팅(`RoomSessionMessageHandler`), 실시간 갱신(`GameEntityMessageHandler`) 정상 확인.

## 롤아웃 순서

1. **GameFramework** — `MessageHandlerBase` + EditMode 테스트 추가, 컴파일·테스트 green.
2. **클라이언트** — 7+1 핸들러 base 전환 + 마커 삭제 + 파일/클래스 rename, 컴파일·스모크.
3. **서버** — 4 핸들러 base 전환 + 마커 2개 삭제 + 파일 rename, 컴파일·스모크.

각 단계 독립 검증. Shared는 이번 무관(핸들러가 Shared에 없음).

## 위험 / 값 동치

순수 구조 리팩터. 유일한 동작 인접 변화는 구독 해제 순서가 forward로 통일되는 것뿐인데, 구독은 서로 독립이라 해제 순서와 무관 → **값 동치**. 최종 whole-branch 리뷰로 각 핸들러가 컴포넌트 단위로 동작 보존됨을 교차 확인.

## 범위 밖 (안 건드림)

- pub/sub 이벤트 방식·`NetworkMessageDispatcher`·MessagePipe 브로커 배선 (표준 정합, 유지)
- 핸들러의 도메인 로직 본문
- `RegisterEntryPoint` 등록 순서
- 앞서 파킹된 `PhysicsFollower` 접기 등 무관 정리
