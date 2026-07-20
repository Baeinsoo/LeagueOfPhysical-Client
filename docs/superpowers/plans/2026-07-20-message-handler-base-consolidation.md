# MessageHandlerBase 통합 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 클·서에 중복된 빈 마커 인터페이스(`IGameMessageHandler`/`IRoomMessageHandler`)와 핸들러마다 반복된 구독/해제 배관을 GameFramework 공유 `MessageHandlerBase` 한 벌로 흡수한다.

**Architecture:** GameFramework에 VContainer 엔트리포인트 base class를 신설(구독 결과 IDisposable을 모아 스코프 종료 시 해제). 모든 메시지 핸들러가 이걸 상속하고, 게임/룸 구분은 DI 스코프가 담당하므로 마커 인터페이스는 삭제한다. 순수 리팩터 — 동작 무변화(값 동치).

**Tech Stack:** Unity, C#, VContainer(DI 엔트리포인트), MessagePipe(pub/sub, 핸들러 쪽만), NUnit(GF EditMode 테스트), UnityMCP(컴파일·테스트 검증).

## Global Constraints

- **적용 범위: 클·서 둘 다** — 동일 변환을 각 repo에 대칭 적용.
- **base 위치: GameFramework** — GF는 VContainer만 참조하면 됨(이미 참조). MessagePipe 참조 추가 금지.
- **`VContainer.Unity.IInitializable` 풀네임 필수** — GF에 자체 `GameFramework.IInitializable`(`Lifecycle/`)이 있어 bare 이름은 충돌. base 파일에서 `using VContainer.Unity;` 쓰지 말 것.
- **값 동치** — 핸들러의 `OnXxx` 로직 본문·`[Inject]` 필드·`RegisterEntryPoint` 등록 순서를 바꾸지 않는다. `Initialize`/`Dispose`/구독 필드만 걷어낸다.
- **`.meta` 동반 처리** — 파일 rename 시 `.cs`와 `.cs.meta`를 함께 `git mv`(GUID 보존). 파일 삭제 시 `.cs`와 `.cs.meta`를 함께 `git rm`.
- **등록 순서 불변식** — `GameLifetimeScope`의 `EntityBinder` → `PlayerHudCoordinator` 등록 순서(= Initialize 순서)를 건드리지 않는다.
- **repo별 브랜치** — 3 repo 각각 `refactor/message-handler-base` 브랜치에서 작업. 클라는 이미 브랜치 존재(spec 커밋됨).
- **UnityMCP 인스턴스 타깃팅** — 모든 UnityMCP 호출에 `unity_instance`를 명시. 클라 id는 `mcpforunity://instances`에서 name=`LeagueOfPhysical-Client`, 서버는 name=`LeagueOfPhysical-Server`의 전체 id(`Name@hash`)를 읽어 사용.

---

## Task 1: GameFramework MessageHandlerBase + EditMode 테스트

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Messaging/MessageHandlerBase.cs`
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Tests/Runtime/MessageHandlerBaseTests.cs`
- Modify: `C:/Users/re5na/workspace/LOP/GameFramework/Tests/Runtime/baegames.GameFramework.Runtime.Tests.asmdef` (VContainer 참조 추가)

**Interfaces:**
- Produces: `GameFramework.MessageHandlerBase` — `abstract class`, `VContainer.Unity.IInitializable` + `IDisposable` 구현. `protected abstract void Subscribe()`, `protected void Track(IDisposable)`, `public virtual void Dispose()`.

- [ ] **Step 1: GF 피처 브랜치 생성**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework" && git checkout -b refactor/message-handler-base && git branch --show-current
```
Expected: `refactor/message-handler-base`

- [ ] **Step 2: Tests asmdef에 VContainer 참조 추가**

`baegames.GameFramework.Runtime.Tests.asmdef`의 `references` 배열에 `"VContainer"`를 추가한다. before:
```json
    "references": [
        "baegames.GameFramework.Runtime",
        "baegames.GameFramework.World",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
```
after:
```json
    "references": [
        "baegames.GameFramework.Runtime",
        "baegames.GameFramework.World",
        "VContainer",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
```

- [ ] **Step 3: 실패하는 테스트 작성**

`MessageHandlerBaseTests.cs`:
```csharp
using System;
using NUnit.Framework;

namespace GameFramework.Tests
{
    public class MessageHandlerBaseTests
    {
        private sealed class FakeDisposable : IDisposable
        {
            public bool Disposed { get; private set; }
            public void Dispose() => Disposed = true;
        }

        private sealed class TestHandler : MessageHandlerBase
        {
            public bool SubscribeCalled { get; private set; }
            public readonly FakeDisposable Sub = new FakeDisposable();

            protected override void Subscribe()
            {
                SubscribeCalled = true;
                Track(Sub);
            }
        }

        [Test]
        public void Initialize_calls_Subscribe()
        {
            var handler = new TestHandler();
            ((VContainer.Unity.IInitializable)handler).Initialize();
            Assert.IsTrue(handler.SubscribeCalled);
        }

        [Test]
        public void Dispose_disposes_tracked_subscriptions()
        {
            var handler = new TestHandler();
            ((VContainer.Unity.IInitializable)handler).Initialize();
            Assert.IsFalse(handler.Sub.Disposed);

            handler.Dispose();
            Assert.IsTrue(handler.Sub.Disposed);
        }
    }
}
```

- [ ] **Step 4: 컴파일 실패 확인 (MessageHandlerBase 없음)**

클라 Unity를 refresh하고 콘솔을 읽어 컴파일 에러를 확인한다(`MessageHandlerBase` 미정의).
```
refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")
read_console(unity_instance="LeagueOfPhysical-Client@<hash>", types=["error"])
```
Expected: `The type or namespace name 'MessageHandlerBase' could not be found` 류 에러.

- [ ] **Step 5: MessageHandlerBase 구현**

`MessageHandlerBase.cs`:
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

        /// <summary>구독을 걸고 그 결과를 <see cref="Track"/>로 등록한다.</summary>
        protected abstract void Subscribe();

        /// <summary>구독 핸들(IDisposable)을 스코프 수명에 묶는다. 스코프 종료 시 자동 해제.</summary>
        protected void Track(IDisposable subscription) => subscriptions.Add(subscription);
    }
}
```

> ⚠️ `namespace GameFramework` 안에서 bare `IInitializable`은 GF 자체 것으로 해석된다. VContainer 구동에 필요한 건 `VContainer.Unity.IInitializable`이므로 **풀네임으로** 쓴다(위 코드처럼). `using VContainer.Unity;` 추가 금지.

- [ ] **Step 6: GF EditMode 테스트 통과 확인**

클라 Unity를 refresh하고 새 테스트를 실행한다.
```
refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")
run_tests(unity_instance="LeagueOfPhysical-Client@<hash>", mode="EditMode", test_filter="MessageHandlerBaseTests")
```
Expected: 2 passed, 0 failed. 콘솔 에러 0.

- [ ] **Step 7: 커밋 (GF repo)**

Unity가 생성한 `.meta`(새 `Messaging/` 폴더 + 두 `.cs`)를 함께 커밋한다.
```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework" && git add Runtime/Scripts/Messaging Tests/Runtime/MessageHandlerBaseTests.cs Tests/Runtime/MessageHandlerBaseTests.cs.meta Tests/Runtime/baegames.GameFramework.Runtime.Tests.asmdef && git status
```
`git status`로 `.meta` 포함 확인 후:
```bash
git commit -m "feat(messaging): add MessageHandlerBase — 구독 생명주기 공유 base

스코프 엔트리포인트 base. Initialize=Subscribe/Dispose=구독 일괄 해제.
클·서 마커 인터페이스 + 반복 배관을 대체할 공유 한 벌.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: 클라이언트 핸들러 마이그레이션

**Files (변환 — 내용 편집):**
- Modify+Rename: `Assets/Scripts/Game/MessageHandler/Game.Info.MessageHandler.cs` → `GameInfoMessageHandler.cs`
- Modify+Rename: `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs` → `GameEntityMessageHandler.cs`
- Modify+Rename: `Assets/Scripts/Game/MessageHandler/Game.Input.MessageHandler.cs` → `GameInputMessageHandler.cs`
- Modify+Rename: `Assets/Scripts/Game/MessageHandler/Game.InputTiming.MessageHandler.cs` → `GameInputTimingMessageHandler.cs`
- Modify+Rename: `Assets/Scripts/Game/MessageHandler/Game.WorldEvent.MessageHandler.cs` → `GameWorldEventMessageHandler.cs`
- Modify (rename 없음): `Assets/Scripts/Entity/EntityBinder.cs`
- Modify (rename 없음): `Assets/Scripts/Game/PlayerHudCoordinator.cs`
- Modify+Rename+클래스rename: `Assets/Scripts/Room/MessageHandler/GameMessageHandler.cs` → `RoomSessionMessageHandler.cs`

**Files (삭제):**
- Delete: `Assets/Scripts/Game/MessageHandler/IGameMessageHandler.cs` (+ `.meta`)
- Delete: `Assets/Scripts/Room/MessageHandler/IRoomMessageHandler.cs` (+ `.meta`)

**Files (등록부 수정):**
- Modify: `Assets/Scripts/Room/RoomLifetimeScope.cs` (`GameMessageHandler` → `RoomSessionMessageHandler`)

**Interfaces:**
- Consumes: `GameFramework.MessageHandlerBase` (Task 1).

**변환 규칙 (각 핸들러에 동일 적용):**
1. 상속을 `: IGameMessageHandler`(또는 `IRoomMessageHandler`) → `: MessageHandlerBase`로 변경.
2. `public void Initialize()`, `public void Dispose()` 메서드와 구독 필드(`private IDisposable subscription;` 또는 `DisposableBag`/`subscriptions` 필드)를 삭제.
3. `protected override void Subscribe()`를 추가하고, `Initialize`에 있던 `.Subscribe(...)` 호출들을 각각 `Track(...)`으로 감싸 넣는다(아래 표의 정확한 본문).
4. 모든 `[Inject]` 필드와 `OnXxx` 핸들러 메서드는 **그대로**.
5. `using GameFramework;`는 이미 모든 핸들러에 있음(유지). `using MessagePipe;`도 유지(`ISubscriber`에 필요). `using VContainer;`도 유지(`[Inject]`).

**각 파일의 정확한 `Subscribe()` 본문:**

| 파일(→새 이름) | Subscribe() 본문 |
|---|---|
| `GameInfoMessageHandler.cs` | `protected override void Subscribe() => Track(gameInfoSubscriber.Subscribe(OnGameInfoToC));` |
| `GameEntityMessageHandler.cs` | 아래 (A) |
| `GameInputMessageHandler.cs` | `protected override void Subscribe() => Track(inputSequenceSubscriber.Subscribe(OnInputSequenceToC));` |
| `GameInputTimingMessageHandler.cs` | `protected override void Subscribe() => Track(inputTimingSubscriber.Subscribe(OnInputTimingToC));` |
| `GameWorldEventMessageHandler.cs` | `protected override void Subscribe() => Track(batchSubscriber.Subscribe(OnWorldEventBatchToC));` |
| `EntityBinder.cs` | 아래 (B) |
| `PlayerHudCoordinator.cs` | `protected override void Subscribe() => Track(entityCreatedSubscriber.Subscribe(OnEntityCreated));` |
| `RoomSessionMessageHandler.cs` | `protected override void Subscribe() => Track(gameInfoSubscriber.Subscribe(OnGameInfoToC));` + 클래스명 rename(아래 C) |

(A) `GameEntityMessageHandler` — 현재 `DisposableBag bag` + `.AddTo(bag)` 5개:
```csharp
protected override void Subscribe()
{
    Track(snapsSubscriber.Subscribe(OnEntitySnapsToC));
    Track(spawnSubscriber.Subscribe(OnEntitySpawnToC));
    Track(despawnSubscriber.Subscribe(OnEntityDespawnToC));
    Track(userSnapSubscriber.Subscribe(OnUserEntitySnapToC));
    Track(statAllocationSubscriber.Subscribe(OnStatAllocationToC));
}
```

(B) `EntityBinder`(클라) — 현재 `DisposableBag bag` + `.AddTo(bag)` 2개:
```csharp
protected override void Subscribe()
{
    Track(entityCreatedSubscriber.Subscribe(OnEntityCreated));
    Track(entityDestroyedSubscriber.Subscribe(OnEntityDestroyed));
}
```

(C) `RoomSessionMessageHandler` — `GameMessageHandler.cs`의 클래스 선언을 rename:
```csharp
// before: public class GameMessageHandler : IRoomMessageHandler
public class RoomSessionMessageHandler : MessageHandlerBase
```
로직(`OnGameInfoToC`에서 `playerContext.session = new LOPSession(...)`)은 그대로.

- [ ] **Step 1: 8개 핸들러 내용 변환**

위 변환 규칙 + 표대로 8개 파일의 내용을 편집한다(파일명은 아직 그대로 두고 내용만). 각 파일에서 `Initialize`/`Dispose`/구독 필드를 지우고 `Subscribe()` override를 추가.

- [ ] **Step 2: 마커 인터페이스 삭제**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git rm Assets/Scripts/Game/MessageHandler/IGameMessageHandler.cs Assets/Scripts/Game/MessageHandler/IGameMessageHandler.cs.meta Assets/Scripts/Room/MessageHandler/IRoomMessageHandler.cs Assets/Scripts/Room/MessageHandler/IRoomMessageHandler.cs.meta
```

- [ ] **Step 3: 파일 rename (`.meta` 동반)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler"
git mv Game.Info.MessageHandler.cs GameInfoMessageHandler.cs
git mv Game.Info.MessageHandler.cs.meta GameInfoMessageHandler.cs.meta
git mv Game.Entity.MessageHandler.cs GameEntityMessageHandler.cs
git mv Game.Entity.MessageHandler.cs.meta GameEntityMessageHandler.cs.meta
git mv Game.Input.MessageHandler.cs GameInputMessageHandler.cs
git mv Game.Input.MessageHandler.cs.meta GameInputMessageHandler.cs.meta
git mv Game.InputTiming.MessageHandler.cs GameInputTimingMessageHandler.cs
git mv Game.InputTiming.MessageHandler.cs.meta GameInputTimingMessageHandler.cs.meta
git mv Game.WorldEvent.MessageHandler.cs GameWorldEventMessageHandler.cs
git mv Game.WorldEvent.MessageHandler.cs.meta GameWorldEventMessageHandler.cs.meta
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Room/MessageHandler"
git mv GameMessageHandler.cs RoomSessionMessageHandler.cs
git mv GameMessageHandler.cs.meta RoomSessionMessageHandler.cs.meta
```

- [ ] **Step 4: RoomLifetimeScope 등록 타입 갱신**

`Assets/Scripts/Room/RoomLifetimeScope.cs`에서:
```csharp
// before: builder.RegisterEntryPoint<GameMessageHandler>();
builder.RegisterEntryPoint<RoomSessionMessageHandler>();
```

- [ ] **Step 5: 클라 컴파일 클린 확인**

```
refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")
read_console(unity_instance="LeagueOfPhysical-Client@<hash>", types=["error"])
```
Expected: 에러 0. (마커 삭제·rename·base 전환이 모두 반영돼 깨끗해야 함.)

- [ ] **Step 6: 인게임 스모크 (사용자)**

사용자가 클라를 실행해 확인: 엔티티 스폰(모델 로드)·로컬 유저 HUD 열림·세션 세팅·실시간 스냅 갱신·넉백/데미지가 이전과 동일하게 동작. (이 핸들러들이 담당하는 기능 전부.)

- [ ] **Step 7: 커밋 (클라 repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git add -A && git status
```
rename(R)·삭제(D)·수정(M)이 의도대로인지, `.meta`가 짝으로 처리됐는지 확인 후:
```bash
git commit -m "refactor(messaging): 클라 핸들러를 MessageHandlerBase로 통합

마커 인터페이스 삭제 + 구독 배관 제거 + 파일명=클래스명 표준화.
GameMessageHandler→RoomSessionMessageHandler. 값 동치.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: 서버 핸들러 마이그레이션

**Files (변환 — 내용 편집):**
- Modify+Rename: `Assets/Scripts/Game/MessageHandler/Game.Info.MessageHandler.cs` → `GameInfoMessageHandler.cs` (Dispose override 있음)
- Modify+Rename: `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs` → `GameEntityMessageHandler.cs`
- Modify+Rename: `Assets/Scripts/Game/MessageHandler/Game.Input.MessageHandler.cs` → `GameInputMessageHandler.cs`
- Modify (rename 없음): `Assets/Scripts/Entity/EntityBinder.cs`

**Files (삭제):**
- Delete: `Assets/Scripts/Game/MessageHandler/IGameMessageHandler.cs` (+ `.meta`)
- Delete: `Assets/Scripts/Room/MessageHandler/IRoomMessageHandler.cs` (+ `.meta`) — 구현체 없는 죽은 인터페이스

**Interfaces:**
- Consumes: `GameFramework.MessageHandlerBase` (Task 1).

**변환 규칙:** Task 2와 동일(상속 교체 / `Initialize`·`Dispose`·구독 필드 삭제 / `Subscribe()` override 추가 / `[Inject]`·`OnXxx` 유지).

**각 파일의 정확한 `Subscribe()` 본문:**

| 파일(→새 이름) | Subscribe() 본문 |
|---|---|
| `GameInfoMessageHandler.cs` | 아래 (A) — **Dispose override 포함** |
| `GameEntityMessageHandler.cs` | `protected override void Subscribe() => Track(statAllocationSubscriber.Subscribe(OnStatAllocationToS));` |
| `GameInputMessageHandler.cs` | `protected override void Subscribe() => Track(inputCommandSubscriber.Subscribe(OnInputCommandToS));` |
| `EntityBinder.cs` | 아래 (B) |

(A) 서버 `GameInfoMessageHandler` — `Initialize`에서 구독 + `runner.AddListener(this)`, `Dispose`에서 구독해제 + `runner.RemoveListener(this)`. `runner` 리스너 lifecycle을 보존해야 하므로 `Dispose`를 override한다. `List<GameInfoToS> gameInfoToSList` 필드와 `[RunnerListen(typeof(End))] OnEnd()`, `BuildAllEntityCreationDatas()`는 **그대로 둔다**:
```csharp
protected override void Subscribe()
{
    Track(gameInfoSubscriber.Subscribe(OnGameInfoToS));
    runner.AddListener(this);
}

public override void Dispose()
{
    base.Dispose();               // 구독 일괄 해제
    runner.RemoveListener(this);  // 추가 teardown
}
```
(즉 이 파일에서는 `private System.IDisposable subscription;` 필드와 옛 `Initialize`/`Dispose`만 지우고 위로 대체. `gameInfoToSList`·`OnGameInfoToS`·`OnEnd`·`BuildAllEntityCreationDatas`는 유지.)

(B) 서버 `EntityBinder` — 현재 `DisposableBag.CreateBuilder()` + `.AddTo(bag)` + `bag.Build()`를 `private IDisposable subscriptions;`에 저장:
```csharp
protected override void Subscribe()
{
    Track(entityCreatedSubscriber.Subscribe(OnEntityCreated));
    Track(entityDestroyedSubscriber.Subscribe(OnEntityDestroyed));
}
```
(`private IDisposable subscriptions;` 필드와 옛 `Initialize`/`Dispose` 삭제. `OnEntityCreated`/`OnEntityDestroyed` 및 `[Inject]` 필드 유지.)

- [ ] **Step 1: 서버 피처 브랜치 생성**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" && git checkout -b refactor/message-handler-base && git branch --show-current
```
Expected: `refactor/message-handler-base`

- [ ] **Step 2: 4개 핸들러 내용 변환**

위 변환 규칙 + 표대로 4개 파일 내용을 편집(파일명은 아직 그대로). 서버 `GameInfoMessageHandler`는 (A)의 Dispose override 포함.

- [ ] **Step 3: 마커 인터페이스 삭제**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" && git rm Assets/Scripts/Game/MessageHandler/IGameMessageHandler.cs Assets/Scripts/Game/MessageHandler/IGameMessageHandler.cs.meta Assets/Scripts/Room/MessageHandler/IRoomMessageHandler.cs Assets/Scripts/Room/MessageHandler/IRoomMessageHandler.cs.meta
```

- [ ] **Step 4: 파일 rename (`.meta` 동반)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/MessageHandler"
git mv Game.Info.MessageHandler.cs GameInfoMessageHandler.cs
git mv Game.Info.MessageHandler.cs.meta GameInfoMessageHandler.cs.meta
git mv Game.Entity.MessageHandler.cs GameEntityMessageHandler.cs
git mv Game.Entity.MessageHandler.cs.meta GameEntityMessageHandler.cs.meta
git mv Game.Input.MessageHandler.cs GameInputMessageHandler.cs
git mv Game.Input.MessageHandler.cs.meta GameInputMessageHandler.cs.meta
```

- [ ] **Step 5: 서버 컴파일 클린 확인**

```
refresh_unity(unity_instance="LeagueOfPhysical-Server@<hash>")
read_console(unity_instance="LeagueOfPhysical-Server@<hash>", types=["error"])
```
Expected: 에러 0.

> 서버 `Room/MessageHandler/` 폴더는 이제 비었을 수 있다(마커만 있었음). 빈 폴더의 `.meta`가 남아 경고를 내면 폴더 `.meta`도 `git rm`. (Room 폴더에 다른 파일이 있으면 그대로 둔다.)

- [ ] **Step 6: 인게임 스모크 (사용자)**

사용자가 클·서를 함께 실행해 확인: 클라 접속·엔티티 스폰·입력 처리·스탯 할당·AI 동작·넉백/데미지가 이전과 동일. (서버 핸들러가 담당하는 입력 수신·게임정보 송신·스탯 할당 전부.)

- [ ] **Step 7: 커밋 (서버 repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" && git add -A && git status
```
rename·삭제·수정·`.meta` 짝 확인 후:
```bash
git commit -m "refactor(messaging): 서버 핸들러를 MessageHandlerBase로 통합

마커 인터페이스 삭제(IRoomMessageHandler=죽은 코드) + 구독 배관 제거 +
파일명=클래스명 표준화. GameInfoMessageHandler는 runner 리스너 teardown
보존 위해 Dispose override. 값 동치.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Self-Review 결과 (작성자 체크)

- **Spec 커버리지:** ① base class(Task 1) ② 핸들러 변환·마커 삭제·rename(Task 2 클/Task 3 서) ③ 파일명 표준(Task 2/3 rename) ④ 등록 순서 불변식(Global Constraints + 미변경) ⑤ 서버 GameInfo runner-listener 예외(Task 3 A) — 전부 태스크에 매핑됨.
- **Placeholder 스캔:** `<hash>`는 실행 시 `mcpforunity://instances`에서 해석(Global Constraints 명시) — 의도된 런타임 값. 그 외 TBD/TODO 없음.
- **타입 일관성:** `MessageHandlerBase`/`Subscribe()`/`Track()`/`Dispose()` 시그니처가 Task 1 정의와 Task 2·3 사용에서 일치. 핸들러 클래스명·구독 필드명·핸들러 메서드명은 실제 소스에서 확인해 표에 기입.

## 롤아웃 순서 / 최종 검증

Task 1(GF) → Task 2(클) → Task 3(서) 순. `file:` 패키지라 Task 1 파일이 디스크에 생기면 클·서 Unity가 refresh 시 자동 인식. 3 repo 모두 커밋 후, 최종 whole-branch 리뷰(superpowers:requesting-code-review)로 각 핸들러의 컴포넌트 단위 동작 보존(값 동치)을 교차 확인하고, 이상 없으면 각 repo main에 `--no-ff` 머지.
