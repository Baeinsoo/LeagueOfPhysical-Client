# EventBus → MessagePipe 이전 + 사용량 정리 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 전역 static 커스텀 이벤트 버스를 Cysharp MessagePipe(타입·keyed pub/sub + DI 스코프 브로커)로 이전하고 버스 사용량을 정리해, R3/DI 통일 + 룸 재입장 leak을 구조적으로 해소한다.

**Architecture:** 문자열 토픽 라우팅을 타입 라우팅으로 바꾼다. 네트워크·엔티티·게임플레이 이벤트는 **Room 스코프 브로커**(룸 종료 시 폐기 = leak 안전망), WebResponse는 **Root 스코프 브로커**. 엔티티별 이벤트는 keyed pub/sub(키=entityId). 구독은 `IDisposable`로 소유 수명에 묶는다. 마지막에 커스텀 버스를 전 레포에서 삭제한다.

**Tech Stack:** Unity 6000.3, VContainer, Cysharp MessagePipe(+MessagePipe.VContainer), UniTask, R3(UI 바인딩 유지). Mirror(네트워크 transport, 변경 없음).

## Global Constraints

- **IL2CPP 안전 필수**: LOP는 모바일 타깃(IL2CPP AOT) → open generics 미지원. **모든 메시지 타입을 `RegisterMessageBroker<T>`(또는 keyed `<TKey,TMessage>`)로 명시 등록**한다. 에디터(Mono)에서 자동 해석돼도 명시 등록을 생략하지 않는다.
- **브로커 스코프 = 전부 Root 싱글턴** (①-only 결정, 2026-07-16 실행 중 확정). 발행/구독이 어느 스코프든 같은 Root 싱글턴을 resolve → 교차 스코프 공유 문제 없음. `InstanceLifetime.Singleton`(MessagePipe 기본).
- **leak 해소 = ① 구독 처분만** (②[스코프 브로커] 드롭). 감사 leak(룸 재입장)은 ①이 구조적으로 보장: `Subscribe().AddTo(소유자수명)` → 소유자 파괴(씬 언로드/스코프 dispose) 시 구독 자동 해제 = 브로커에서 핸들러 제거 → 죽은 핸들러 누적 0. *깜빡할 수동 unsubscribe가 없음.* ②(브로커를 룸과 함께 폐기)는 교차스코프 공유·이중 `RegisterMessagePipe` 복잡도 대비 redundant라 채택 안 함.
- **구독 = `IDisposable` → 소유 수명에 묶음(필수)**: 순수 C#(VContainer)은 `CompositeDisposable`/MessagePipe `DisposableBag`, MonoBehaviour는 `destroyCancellationToken`/OnDestroy, ViewModel은 기존 `CompositeDisposable`. **모든 Subscribe는 반드시 AddTo — 이게 leak 해소의 유일 기전**(Rx 표준 관례, 코드리뷰로 커버).
- **범위 특정 = 타입 + 키 + (필요 시)필터**, 스코프 아님. 타입(`ISubscriber<T>`)이 주 채널, 엔티티별은 keyed(키=entityId), 세밀 조건은 `MessageHandlerFilter<T>`/핸들러 내 조건.
- **네임스페이스 풀 한정**: World 타입은 `GameFramework.World.*` 풀 네임스페이스(`using GameFramework.World;` 추가 금지 — `Component` 모호성).
- **패키지 파일 삭제 후**: `refresh_unity scope=all force`로 stale CS2001 방지(양쪽 에디터). UnityMCP는 `unity_instance="LeagueOfPhysical-Client@<hash>"` 명시(id는 `mcpforunity://instances`에서 조회).
- **클·서 동시**: 각 이벤트 패밀리는 클라·서버 양쪽에서 함께 이전한다. 커밋은 피처 브랜치 `feature/eventbus-messagepipe-migration`(양쪽 레포에 생성).
- **테스트 제약**: 클라/서버 게임 코드는 Assembly-CSharp라 EditMode 불가(`[[client-test-infra-constraint]]`). 순수 로직(디스패처 등)은 GameFramework/Shared 패키지에 두면 EditMode 가능. 통합은 인게임 관찰로 검증.

---

## 파일 구조 (생성/수정 맵)

**신규:**
- `GameFramework/Runtime/Scripts/Netcode/`(또는 앱): `NetworkMessageDispatcher` — 런타임 `IMessage` → 타입별 `IPublisher<T>` 디스패치(Slice 2). *논의: 앱별 메시지 타입에 의존하므로 앱 프로젝트에 둔다.*
- 클라 `Assets/Scripts/Messaging/NetworkMessageDispatcher.cs`, 서버 상응.
- (선택) DI 등록 헬퍼: `RoomLifetimeScope`/`RootLifetimeScope` 내부.

**수정(등록):** 클·서 `RootLifetimeScope.cs`(WebResponse 브로커), `RoomLifetimeScope.cs`(게임플레이/네트워크/엔티티 브로커 + `RegisterMessagePipe`), `LOPRoom.cs`(수신 경계를 디스패처로).

**수정(호출처, 약 40곳):** 아래 각 슬라이스의 사이트 표.

**삭제(Slice 4):** `GameFramework/Runtime/Scripts/EventBus/{EventBus,IEventBus,HandlerWrapper}.cs`, 클·서 `Assets/Scripts/EventBus/{EventBus,EventTopic}.cs`.

---

## Slice 0: MessagePipe 도입 + DI 스코프 골격 + 메커니즘 검증 spike

**목표:** MessagePipe를 양쪽 앱에 설치하고, Root/Room 스코프 브로커 등록 관용구를 **실제로 검증**한다(발행/구독 동작 + 스코프 폐기 시 자동 해제). 이 슬라이스가 나머지가 복사할 "정답 패턴"을 못박는다. 호출처 변경 0.

**Files:**
- Modify: 클·서 `Packages/manifest.json` (MessagePipe UPM 의존성)
- Modify: 클 `Assets/Scripts/Installers/RootLifetimeScope.cs` (또는 실제 루트 스코프 파일), `Assets/Scripts/Room/RoomLifetimeScope.cs`
- Modify: 서버 상응 스코프 파일
- Create(임시 검증용): 클 `Assets/Scripts/Messaging/_MessagePipeSpike.cs` (검증 후 삭제)

**Interfaces:**
- Produces: Root 스코프에 `RegisterMessagePipe` + WebResponse 브로커, Room 스코프에 `RegisterMessagePipe` + 게임플레이 브로커. `IPublisher<T>`/`ISubscriber<T>`/`IPublisher<string,T>`/`ISubscriber<string,T>` 주입 가능.

- [ ] **Step 1: MessagePipe UPM 의존성 추가**

클·서 `Packages/manifest.json`의 `dependencies`에 추가:
```json
"com.cysharp.messagepipe": "https://github.com/Cysharp/MessagePipe.git?path=src/MessagePipe.Unity/Assets/Plugins/MessagePipe",
"com.cysharp.messagepipe.vcontainer": "https://github.com/Cysharp/MessagePipe.git?path=src/MessagePipe.Unity/Assets/Plugins/MessagePipe.VContainer"
```
(UniTask는 이미 있음. 없으면 UniTask도 추가.)

- [ ] **Step 2: 컴파일 확인**

`refresh_unity scope=all force compile=request` (클라 인스턴스) → `read_console types=[error]` → Expected: MessagePipe 심볼 해석됨, 에러 0.

- [ ] **Step 3: Root 스코프에 `RegisterMessagePipe` + 스파이크 브로커 등록**

`RootLifetimeScope.Configure`에(모든 브로커는 Root 싱글턴):
```csharp
var options = builder.RegisterMessagePipe();   // InstanceLifetime.Singleton 기본
builder.RegisterMessageBroker<EntityCreated>(options);            // plain 스파이크(IL2CPP 명시)
builder.RegisterMessageBroker<string, PropertyChange>(options);   // keyed 스파이크
```

- [ ] **Step 4: 검증 spike 작성 (`_MessagePipeSpike.cs`) + Root 엔트리포인트 등록**

룸 불필요 — 앱 시작(Root Start)에서 keyed pub/sub·키격리·처분을 한 곳에서 검증한다.
```csharp
using MessagePipe;
using UnityEngine;
using VContainer;

namespace LOP
{
    // Root 스코프에 등록해 앱 시작 시 검증 후 삭제한다.
    public class _MessagePipeSpike : VContainer.Unity.IStartable
    {
        [Inject] private IPublisher<string, PropertyChange> keyedPub;
        [Inject] private ISubscriber<string, PropertyChange> keyedSub;

        public void Start()
        {
            var bag = DisposableBag.CreateBuilder();
            keyedSub.Subscribe("entity7", p => Debug.Log($"[SPIKE] keyed received: {p.propertyName}")).AddTo(bag);
            var d = bag.Build();

            keyedPub.Publish("entity7", new PropertyChange("position"));  // [SPIKE] keyed received: position 기대
            keyedPub.Publish("entity8", new PropertyChange("x"));         // 로그 없어야(다른 키)

            d.Dispose();
            keyedPub.Publish("entity7", new PropertyChange("after"));     // 로그 없어야(구독 해제됨)
        }
    }
}
```
`RootLifetimeScope`에 `builder.RegisterEntryPoint<_MessagePipeSpike>();` 등록. `PropertyChange`는 `LOP.Event.Entity` 네임스페이스 — using 확인.

- [ ] **Step 5: 컴파일 확인** — `refresh_unity scope=all force` + `read_console types=[error]`. Expected: 에러 0(MessagePipe 심볼·open-generic 해석됨).

- [ ] **Step 6: Play mode 실행으로 검증**

`manage_editor`로 play mode 진입 → `read_console filter_text=SPIKE` 확인:
- `[SPIKE] keyed received: position` 1회 (entity7)
- entity8·다른키 로그 없음 (키 격리)
- Dispose 후 `after` 로그 없음 (구독 해제)
Play mode 종료. Expected: 위 3가지 충족 → keyed 라우팅·키격리·`AddTo` 처분 동작 확인.

> 기대와 다르면 멈추고 원인 파악(등록 위치·open-generic·IL2CPP 명시등록). 동작한 관용구를 이 plan에 기록.

- [ ] **Step 7: 스파이크 제거 + 커밋**

`_MessagePipeSpike.cs` 삭제 + `RegisterEntryPoint<_MessagePipeSpike>` 및 스파이크 브로커 등록(EntityCreated/PropertyChange — Slice 1/3서 정식 등록) 되돌림. `RootLifetimeScope`의 `var options = builder.RegisterMessagePipe();`는 유지(다음 슬라이스가 브로커를 이 options로 등록).
```bash
git add -A && git commit -m "feat(messaging): MessagePipe 도입 + Root 싱글턴 브로커 골격(검증됨)"
```

---

## Slice 1: 타입 라우팅 단순 패밀리 (WebResponse + 엔티티 라이프사이클 + ItemTouch)

**목표:** keyed가 필요 없는 plain 타입 이벤트를 MessagePipe로 이전한다. 가장 안전한 패밀리로 패턴을 굳힌다.

### 변환 패턴 (이 슬라이스 전 사이트에 적용)

**발행 (before → after):**
```csharp
// before
EventBus.Default.Publish(nameof(EntityCreated), new EntityCreated(entity));
// after — [Inject] private IPublisher<EntityCreated> entityCreatedPublisher; 후
entityCreatedPublisher.Publish(new EntityCreated(entity));
```

**구독 (before → after):**
```csharp
// before (Initialize/ctor)
EventBus.Default.Subscribe<EntityCreated>(nameof(EntityCreated), OnEntityCreated);
// after — [Inject] private ISubscriber<EntityCreated> entityCreatedSubscriber; 후
entityCreatedSubscriber.Subscribe(OnEntityCreated).AddTo(disposables);
// before (Dispose/OnDetach) 의 Unsubscribe 줄은 삭제 (disposables.Dispose()가 대신)
```
`disposables` = 소유자의 `CompositeDisposable`(순수 C#/VContainer) 또는 MessagePipe `DisposableBag`. MonoBehaviour면 `.AddTo(this.destroyCancellationToken)` 또는 OnDestroy에서 dispose.

**등록 (스코프 Configure — IL2CPP 명시):**
```csharp
// Root 스코프 (WebResponse)
builder.RegisterMessageBroker<CreateUserResponse>(options);
builder.RegisterMessageBroker<GetUserResponse>(options);
builder.RegisterMessageBroker<GetUserLocationResponse>(options);
builder.RegisterMessageBroker<GetUserStatsResponse>(options);
builder.RegisterMessageBroker<UpdateUserProfileResponse>(options);
builder.RegisterMessageBroker<GetMatchResponse>(options);
builder.RegisterMessageBroker<RoomJoinableResponse>(options);   // 클라
// 서버 Root: GetMatchResponse, GetRoomResponse, UpdateRoomStatusResponse
// Room 스코프 (엔티티 라이프사이클/ItemTouch)
builder.RegisterMessageBroker<EntityCreated>(options);
builder.RegisterMessageBroker<EntityDestroyed>(options);        // 클라만 발행(서버는 발행 안 함)
builder.RegisterMessageBroker<ItemTouch>(options);              // 서버 발행/구독
```

### 사이트 (클라)

| 파일 | 역할 | 타입 | 스코프 |
|---|---|---|---|
| `WebAPI/LOPWebRequestInterceptor.cs:14` | publish | (web response) | Root |
| `Data/UserDataStore.cs:15-19` | subscribe×5 | CreateUser/GetUserLocation/GetUser/GetUserStats/UpdateUserProfile Response | Root |
| `Data/RoomDataStore.cs:13-14` | subscribe×2 | GetMatch/RoomJoinable Response | Root |
| `Entity/LOPEntityManager.cs:73` | publish | EntityCreated | Room |
| `Entity/LOPEntityManager.cs:106` | publish | EntityDestroyed | Room |
| `Game/EntityBinder.cs:22/27` | sub/unsub | EntityCreated | Room |
| `Game/PlayerHudCoordinator.cs:24/29` | sub/unsub | EntityCreated | Room |

### 사이트 (서버)

| 파일 | 역할 | 타입 | 스코프 |
|---|---|---|---|
| `WebAPI/LOPWebRequestInterceptor.cs:15` | publish | (web response) | Root |
| `Data/RoomDataStore.cs:13-15` | subscribe×3 | GetMatch/GetRoom/UpdateRoomStatus Response | Root |
| `Entity/LOPEntityManager.cs:83` | publish | EntityCreated | Room |
| `Component/PhysicsComponent.cs:102` | publish | ItemTouch | Room |
| `Game/GameRuleSystem.cs:57/65` | sub/unsub | ItemTouch | Room |

> **WebResponse publish의 타입 주름:** `LOPWebRequestInterceptor.Publish(EventTopic.WebResponse, response)`에서 `response`는 다형(여러 응답 타입). Slice 2의 네트워크 경계와 같은 문제 → 같은 해법(런타임 타입 디스패치) 또는, 응답 타입이 인터셉터에서 이미 구체 타입이면 제네릭 메서드로 타입별 `IPublisher<T>` 호출. **구현 시 인터셉터의 `response` 정적 타입 확인**: 구체 타입별 호출 경로가 있으면 제네릭 헬퍼, 다형이면 Slice 2 디스패처 재사용.

- [ ] **Step 1: Root/Room 스코프에 위 브로커 명시 등록** (클·서)
- [ ] **Step 2: WebResponse 발행/구독 이전** (클·서 인터셉터·DataStore) — 위 패턴. `EventBus` 줄 제거.
- [ ] **Step 3: EntityCreated/Destroyed/ItemTouch 발행/구독 이전** (클·서) — 위 패턴.
- [ ] **Step 4: 컴파일 확인** — `refresh_unity scope=all force`(클) + `read_console types=[error]`. Expected: 에러 0. (이 패밀리의 `EventBus.Default` 호출처가 0이 됐는지 grep.)
- [ ] **Step 5: 인게임 검증** — 로그인→룸목록→룸입장→엔티티 스폰 관찰. Expected: 유저/룸 데이터 로드 정상, 엔티티 스폰 시 HUD/바인더 정상 동작(EntityCreated 도달).
- [ ] **Step 6: 커밋** — `git commit -m "refactor(messaging): WebResponse·엔티티 라이프사이클·ItemTouch → MessagePipe (Slice 1)"`

---

## Slice 2: 네트워크 수신 (`nameof(IMessage)`) — 타입 디스패치 브릿지

**목표:** Mirror 수신 경계가 다형 `IMessage`를 타입별 `IPublisher<T>`로 디스패치하도록 브릿지를 도입하고, 전 MessageHandler를 `ISubscriber<T>`로 이전한다.

**핵심 설계:** MessagePipe는 컴파일-타입이라 `IMessage`(런타임 다형)를 바로 못 넣는다. IL2CPP도 리플렉션 open-generic을 싫어한다. → **등록 테이블 디스패처**: 메시지 타입마다 `msg => publisher_T.Publish((T)msg)` 델리게이트를 사전 등록(리플렉션 없음, IL2CPP 안전).

**Files:**
- Create: 클 `Assets/Scripts/Messaging/NetworkMessageDispatcher.cs`, 서버 상응
- Modify: 클·서 `Room/LOPRoom.cs`(수신 핸들러가 디스패처 호출), `Room/RoomLifetimeScope.cs`(디스패처 + 메시지 브로커 등록), 전 `Game/MessageHandler/*.cs`

**Interfaces:**
- Produces: `NetworkMessageDispatcher.Dispatch(IMessage message)` — 구체 타입의 `IPublisher<T>`로 발행. `Register<T>()`로 타입별 델리게이트 구성(ctor에서 주입된 publisher들로).

- [ ] **Step 1: `NetworkMessageDispatcher` 작성 (클라)**

```csharp
using System;
using System.Collections.Generic;
using MessagePipe;
using VContainer;

namespace LOP
{
    /// <summary>
    /// Mirror 수신 경계가 넘겨준 다형 IMessage를 그 구체 타입의 IPublisher&lt;T&gt;로 보낸다.
    /// 타입별 델리게이트를 사전 등록해 리플렉션 없이(IL2CPP 안전) 디스패치한다.
    /// </summary>
    public class NetworkMessageDispatcher
    {
        private readonly Dictionary<Type, Action<IMessage>> routes = new();

        [Inject]
        public NetworkMessageDispatcher(
            IPublisher<GameInfoToC> gameInfo,
            IPublisher<DamageEventToC> damage,
            IPublisher<AbilityActivatedToC> ability,
            IPublisher<EntitySnapsToC> snaps,
            IPublisher<EntitySpawnToC> spawn,
            IPublisher<EntityDespawnToC> despawn,
            IPublisher<UserEntitySnapToC> userSnap,
            IPublisher<StatAllocationToC> statAlloc,
            IPublisher<InputSequenceToC> inputSeq,
            IPublisher<InputTimingToC> inputTiming)
        {
            Register(gameInfo); Register(damage); Register(ability); Register(snaps);
            Register(spawn); Register(despawn); Register(userSnap); Register(statAlloc);
            Register(inputSeq); Register(inputTiming);
        }

        private void Register<T>(IPublisher<T> publisher) where T : IMessage
            => routes[typeof(T)] = msg => publisher.Publish((T)msg);

        public void Dispatch(IMessage message)
        {
            if (routes.TryGetValue(message.GetType(), out var route)) route(message);
            else UnityEngine.Debug.LogWarning($"[NetworkMessageDispatcher] 미등록 메시지 타입: {message.GetType()}");
        }
    }
}
```
서버는 수신 타입(GameInfoToS/InputCommandToS/StatAllocationToS)으로 상응 작성.

- [ ] **Step 2: Room 스코프에 디스패처 + 메시지 브로커 등록 (클라)**
```csharp
builder.RegisterMessageBroker<GameInfoToC>(options);
builder.RegisterMessageBroker<DamageEventToC>(options);
builder.RegisterMessageBroker<AbilityActivatedToC>(options);
builder.RegisterMessageBroker<EntitySnapsToC>(options);
builder.RegisterMessageBroker<EntitySpawnToC>(options);
builder.RegisterMessageBroker<EntityDespawnToC>(options);
builder.RegisterMessageBroker<UserEntitySnapToC>(options);
builder.RegisterMessageBroker<StatAllocationToC>(options);
builder.RegisterMessageBroker<InputSequenceToC>(options);
builder.RegisterMessageBroker<InputTimingToC>(options);
builder.Register<NetworkMessageDispatcher>(Lifetime.Singleton);
```
> **스코프 주의:** `LOPRoom`이 Room 스코프면 디스패처도 Room 스코프. `NetworkMessageDispatcher`가 Room 스코프에서 resolve되고 그 안의 `IPublisher<T>`도 Room 브로커여야 한다(핸들러가 자식 Game 스코프서 같은 Room 브로커 구독). Slice 0에서 검증한 토폴로지 준수.

- [ ] **Step 3: `LOPRoom` 수신 경계를 디스패처로 (클라)**
```csharp
// before (LOPRoom.cs:80-83)
NetworkClient.RegisterHandler<CustomMirrorMessage>(message =>
    EventBus.Default.Publish(nameof(IMessage), message.payload));
// after — [Inject] private NetworkMessageDispatcher dispatcher; 후
NetworkClient.RegisterHandler<CustomMirrorMessage>(message =>
    dispatcher.Dispatch(message.payload));
```
> `LOPRoom`이 디스패처를 주입받으려면 디스패처가 `LOPRoom`과 같은(또는 부모) 스코프여야 함. 둘 다 Room 스코프 → OK.

- [ ] **Step 4: 전 MessageHandler를 `ISubscriber<T>`로 (클라)**

각 핸들러: `EventBus.Default.Subscribe<T>(nameof(IMessage), OnX)` → `[Inject] ISubscriber<T>` + `subscriber.Subscribe(OnX).AddTo(disposables)`. Dispose의 Unsubscribe 줄 삭제. 대상:

| 파일 | 타입 |
|---|---|
| `Game/MessageHandler/Game.Info.MessageHandler.cs` | GameInfoToC |
| `Game/MessageHandler/Game.Input.MessageHandler.cs` | InputSequenceToC |
| `Game/MessageHandler/Game.InputTiming.MessageHandler.cs` | InputTimingToC |
| `Game/MessageHandler/Game.Ability.MessageHandler.cs` | AbilityActivatedToC |
| `Game/MessageHandler/Game.Damage.MessageHandler.cs` | DamageEventToC |
| `Game/MessageHandler/Game.Entity.MessageHandler.cs` | EntitySnapsToC, EntitySpawnToC, EntityDespawnToC, UserEntitySnapToC, StatAllocationToC |
| `Room/MessageHandler/GameMessageHandler.cs` | GameInfoToC |
| `Game/LOPGamePresenter.cs:29/37` | GameInfoToC |
| `Data/GameDataStore.cs:13/20` | GameInfoToC |

> **주의:** GameInfoToC를 구독하는 소비자가 4곳(Presenter/DataStore/GameMessageHandler/Info.MessageHandler). 타입 브로커라 4곳 모두 같은 스트림을 받음 — 기존 동작과 동일(옛 버스도 같은 토픽+타입에 여러 구독).

- [ ] **Step 5: 서버 상응 이전** — `LOPRoom`(서버) 수신 경계 + 디스패처 + `Game.Entity/Info/Input.MessageHandler`(StatAllocationToS/GameInfoToS/InputCommandToS).
- [ ] **Step 6: 컴파일 확인** — `refresh_unity scope=all force`(클·서) + `read_console types=[error]`. Expected: 에러 0.
- [ ] **Step 7: 인게임 검증** — 룸 입장→게임 시작→이동/공격. Expected: GameInfo 수신(게임 시작), 스냅/스폰/디스폰 정상(캐릭터 보임·이동), 입력/데미지 반영. `[NetworkMessageDispatcher] 미등록` 경고 없음.
- [ ] **Step 8: 커밋** — `git commit -m "refactor(messaging): 네트워크 수신을 타입 디스패치 브릿지+ISubscriber로 (Slice 2)"`

---

## Slice 3: 엔티티별 keyed 패밀리 + 죽은 코드 트림

**목표:** `EntityId<LOPEntity>(id)` 패밀리를 keyed pub/sub(키=entityId)로 이전하고, 죽은 모션 이벤트를 제거한다.

### 죽은 코드 트림 (먼저)

- [ ] **Step 1: `rotation`/`velocity` PropertyChange 발행 삭제 (클·서 `LOPEntity.cs`)**

`rotation`/`velocity` setter의 `RaisePropertyChanged(...)` 호출 삭제(소비자 0 — 뷰 무시·물리 브릿지·컨트롤러 no-op). `position`·`visualId` 발행은 유지. (setter의 값 쓰기·equality 가드는 유지.)

- [ ] **Step 2: `LOPEntityController`의 no-op PropertyChange 구독 삭제 (클·서)**

`LOPEntityController.cs`의 `EventBus.Default.Subscribe<PropertyChange>(...)`/`Unsubscribe`/`OnPropertyChange`(빈 switch) 전부 삭제. 나머지(runner listen, PushMotion/SyncPhysics)는 유지.

### keyed 이전 패턴

**발행:**
```csharp
// before
EventBus.Default.Publish(EventTopic.EntityId<LOPEntity>(id), new EntityDamage(...));
// after — [Inject] private IPublisher<string, EntityDamage> damagePublisher;
damagePublisher.Publish(id, new EntityDamage(...));
```
**구독:**
```csharp
// before
EventBus.Default.Subscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnEntityDamage);
// after — [Inject] private ISubscriber<string, EntityDamage> damageSubscriber;
damageSubscriber.Subscribe(entity.entityId, OnEntityDamage).AddTo(disposables);
```
**등록 (Room 스코프, keyed):**
```csharp
builder.RegisterMessageBroker<string, PropertyChange>(options);
builder.RegisterMessageBroker<string, EntityDamage>(options);
builder.RegisterMessageBroker<string, AbilityActivated>(options);
builder.RegisterMessageBroker<string, EntityHealthChanged>(options);
builder.RegisterMessageBroker<string, EntityManaChanged>(options);
builder.RegisterMessageBroker<string, EntityLevelChanged>(options);
builder.RegisterMessageBroker<string, EntityStatPointsChanged>(options);
builder.RegisterMessageBroker<string, EntityStatChanged>(options);
```

### 사이트 (클라)

| 파일 | 역할 | 타입 |
|---|---|---|
| `Entity/LOPEntity.cs:58` | publish | PropertyChange (position·visualId만 남음) |
| `Component/PhysicsComponent.cs:24/50` | sub/unsub | PropertyChange |
| `Entity/LOPEntityView.cs:40-42/52-54` | sub×3/unsub×3 | PropertyChange, AbilityActivated, EntityDamage |
| `World/WorldEventSink.cs:25` | publish | EntityDamage |
| `World/WorldEventSink.cs:37` | publish | AbilityActivated |
| `Game/MessageHandler/Game.Entity.MessageHandler.cs:89/185` | publish | EntityHealthChanged |
| `Game/MessageHandler/Game.Entity.MessageHandler.cs:202/219/235/273` | publish | EntityManaChanged/EntityLevelChanged/EntityStatPointsChanged/EntityStatChanged |
| `UI/CharacterHud/CharacterHudViewModel.cs:49-51/101-103` | sub×3 | EntityHealthChanged, EntityManaChanged, EntityLevelChanged |
| `UI/Stats/StatsViewModel.cs:49-50/100-101` | sub×2 | EntityStatChanged, EntityStatPointsChanged |
| `UI/WorldSpace/DamageFloaterEmitter.cs:68/73` | sub | EntityDamage |
| `UI/WorldSpace/CharacterNameplate.cs:78/83` | sub | EntityHealthChanged |

### 사이트 (서버)

| 파일 | 역할 | 타입 |
|---|---|---|
| `Entity/LOPEntity.cs:58` | publish | PropertyChange (position만; 서버는 visualId 발행 여부 확인) |
| `Component/PhysicsComponent.cs:29/60` | sub/unsub | PropertyChange |
| `Entity/LOPEntityView.cs:39/49` | sub/unsub | PropertyChange |

> **ViewModel 주의:** `CharacterHudViewModel`/`StatsViewModel`은 MessagePipe `ISubscriber<string,T>.Subscribe`로 데이터를 받아 기존 R3 `ReactiveProperty`에 쓴다(VM→View 바인딩 유지). 구독은 VM의 `CompositeDisposable`에 `.AddTo`.
> **PhysicsComponent 주의:** `OnPropertyChange`의 `case nameof(entity.position)` → `rb.position` 유지(살아있는 의존). keyed 구독으로만 바꾼다.

- [ ] **Step 3: keyed 브로커 등록 (Room 스코프, 클·서)**
- [ ] **Step 4: 발행 사이트 이전 (클·서)** — 위 패턴, 표의 publish 행.
- [ ] **Step 5: 구독 사이트 이전 (클·서)** — 위 패턴, 표의 sub 행. Unsubscribe 줄 삭제.
- [ ] **Step 6: 컴파일 확인** — `refresh_unity scope=all force`(클·서) + `read_console types=[error]`. Expected: 에러 0.
- [ ] **Step 7: 인게임 검증** — 두 캐릭터로 전투: 데미지 플로터·HP바·네임플레이트·스탯 팝업·피격 애니·외형 로드 확인. 여러 엔티티 동시에 두고 **키 격리**(A 데미지가 B UI에 안 뜸) 확인. Expected: 각 엔티티 UI가 자기 이벤트만 반영, 오작동 없음.
- [ ] **Step 8: 커밋** — `git commit -m "refactor(messaging): 엔티티별 이벤트 keyed pub/sub + 죽은 모션 이벤트 트림 (Slice 3)"`

---

## Slice 4: 커스텀 버스 삭제 + 최종 검증

**목표:** 이제 호출처 0인 커스텀 버스를 전 레포에서 삭제하고, 전체 회귀를 확인한다.

- [ ] **Step 1: 잔여 호출처 0 확인**

Grep(전 레포): `EventBus.Default`, `EventTopic`, `IEventBus`, `new GameFramework.EventBus`. Expected: 매치 0(스파이크·주석 제외). 남아 있으면 해당 슬라이스 패턴으로 이전.

- [ ] **Step 2: 파일 삭제 (.cs + .meta)**

- 클·서: `Assets/Scripts/EventBus/EventBus.cs`, `Assets/Scripts/EventBus/EventTopic.cs` (+ .meta)
- GameFramework: `Runtime/Scripts/EventBus/EventBus.cs`, `IEventBus.cs`, `HandlerWrapper.cs` (+ .meta)
```bash
# 각 경로마다 .cs와 .cs.meta 삭제
```

- [ ] **Step 3: 패키지 삭제 반영 (stale CS2001 방지)**

`refresh_unity scope=all force`(클·서 각각) → `read_console types=[error]`. Expected: 에러 0(CS2001 없음). `[[deleting-package-files-cs2001]]` 참고.

- [ ] **Step 4: EditMode 전체 회귀**

`run_tests mode=EditMode`(클라 인스턴스) → `get_test_job`. Expected: 전건 green(GameFramework/Shared에 EventBus 의존 테스트 없어 무영향 예상, 확인).

- [ ] **Step 5: 인게임 종합 검증**

로그인→룸→게임: 유저/룸 데이터, 엔티티 스폰/디스폰, 이동/점프, 전투(데미지·HP·플로터·네임플레이트), 스탯 팝업, 룸 재입장(leak 없음 — 재입장 후 이벤트 중복/누수 없음). Expected: 전 기능 정상 + 룸 재입장 반복해도 이상 없음.

- [ ] **Step 6: 커밋 + 문서 정합**

`ROADMAP.md`/`audit-2026-07-13-structure.md`의 #8을 완료로 갱신.
```bash
git commit -m "refactor(messaging): 커스텀 EventBus 삭제 + #8 완료 (Slice 4)"
```

- [ ] **Step 7: 양쪽 레포 브랜치 정리**

GameFramework(EventBus 삭제 포함)·클·서 각 피처 브랜치를 확인하고, 사용자 승인 시 `main`에 `--no-ff` 머지.

---

## Self-Review (spec 대비)

**Spec coverage:**
- 범위(양쪽+삭제) → Slice 1-4 ✓ / MessagePipe → Slice 0 ✓ / 타입·keyed 라우팅 → Slice 1(plain)·3(keyed) ✓ / 네트워크 주름 → Slice 2 ✓ / ①구독처분 → 전 슬라이스 `.AddTo` ✓ / ②스코프 브로커 → Slice 0 Room/Root ✓ / 트림 경계(rotation·velocity 발행+컨트롤러 no-op 제거, position pull 전환은 제외) → Slice 3 Step 1-2 ✓ / WebResponse 앱 스코프 → Slice 1 ✓.
- **갭 메모**: WebResponse 발행의 다형 타입 처리(Slice 1 주름 노트) — 구현 시 인터셉터 `response` 정적 타입 확인 후 제네릭 헬퍼 vs 디스패처 결정. `LOPRoom`/디스패처 스코프 일치는 Slice 0 검증 토폴로지에 의존.

**Placeholder scan:** MessagePipe API 코드는 공식 문서 기준 실제 시그니처. Slice 0의 "동작 다르면 조정"은 외부 라이브러리 스코프 검증 spike라 의도적(플레이스홀더 아님 — 던지기 코드+수용 기준 명시). 사이트 표는 실제 file:line.

**Type consistency:** `IPublisher<T>`/`ISubscriber<T>`(plain), `IPublisher<string,T>`/`ISubscriber<string,T>`(keyed), `.Subscribe(handler)`/`.Subscribe(key, handler)` → `IDisposable`, `.AddTo(bag/disposables)`, `RegisterMessageBroker<T>`/`<TKey,TMessage>` — 전 슬라이스 일관. `NetworkMessageDispatcher.Dispatch(IMessage)`/`Register<T>` 일관.
