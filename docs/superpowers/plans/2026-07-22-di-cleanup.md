# DI 정리 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** VContainer DI를 표준 관례로 정리한다 — 죽은 `SceneLifetimeScope` 제거(B), 씬 주입 속성을 씬 전용임이 드러나게 리네임(C), 클·서 순수 C# 서비스를 필드 주입 → 생성자 주입 전환(A).

**Architecture:** 세 작업은 독립. 실행 순서 B → C → A. B는 클·서 프로젝트 스코프 파일만, C는 GameFramework(정의) + 클·서(사용처) 원자적 변경, A는 클·서 프로젝트 Assembly-CSharp만(공유 패키지 무영향). 검증은 UnityMCP로 클·서 인스턴스를 각각 핀해 콘솔 컴파일 확인 + 마지막에 플레이 스모크로 순환 예외/씬 주입 동작 확인.

**Tech Stack:** Unity, VContainer, UnityMCP(컴파일 검증·콘솔·테스트). 클·서·GameFramework 3개 git repo.

## Global Constraints

- **3개 repo 각각 피처 브랜치**에서 작업. main 직접 커밋 금지.
  - 클라 `LeagueOfPhysical-Client`: 이미 `chore/di-cleanup`.
  - 서버 `LeagueOfPhysical-Server`: `git checkout -b chore/di-cleanup` (main에 로컬 픽스처 변경 있음 — 아래 참조).
  - `GameFramework`: `git checkout -b chore/di-cleanup`.
- **서버 로컬 픽스처 커밋 금지.** 서버 working-tree에 커밋하면 안 되는 로컬 변경이 있다: `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`, `Assets/Scripts/Game/GameRuleSystem.cs`, `Assets/DefaultVolumeProfile.asset`. **이 파일들은 A 전환 대상에서 제외**한다(픽스처 변경과 주입 변경이 한 파일에 섞이면 커밋 분리가 위험). 서버 커밋 시 `git add`로 대상 파일만 골라 담고, 위 3개는 절대 담지 않는다.
- **UnityMCP 인스턴스 핀 필수.** 매 UnityMCP 호출에 `unity_instance`를 명시한다. 실행 시작 시 `mcpforunity://instances`를 읽어 `name`이 `LeagueOfPhysical-Client`인 것과 `LeagueOfPhysical-Server`인 것의 전체 `id`(`Name@hash`)를 각각 확보한다(해시는 세션마다 바뀔 수 있음). 이하 단계에서 `<CLIENT>` / `<SERVER>`로 표기.
- **.meta 파일:** 스크립트 삭제·리네임 시 Unity가 만든 `.meta`도 함께 처리. 삭제는 `git rm`으로 `.cs`+`.meta` 동시, 리네임은 `git mv`로 `.cs`+`.meta` 동시. `.meta`를 직접 만들거나 수정하지 않는다.
- **패키지 파일 삭제/리네임 후 stale 참조:** GameFramework(file: 패키지)에서 `.cs`를 지우거나 옮기면 클·서에 stale `CS2001`이 뜰 수 있다. `refresh_unity`를 `scope=all force`로 양쪽 인스턴스에 돌려 재스캔한다.
- **컴파일 검증 절차(매 태스크 끝):** 대상 인스턴스에 `refresh_unity` → `editor_state`의 `isCompiling`이 false 될 때까지 대기 → `read_console`로 error 0 확인. GameFramework를 건드린 태스크는 클·서 **양쪽** 인스턴스에서 확인.
- **순환 검출:** A 전환 후 `VContainerException: Circular dependency detected`는 컴파일이 아니라 **해당 LifetimeScope가 빌드될 때(=그 씬 진입, 플레이)** 던져진다. 컴파일 clean은 순환 부재를 보장하지 않는다. 순환 발견 시 **필드 주입으로 되돌리지 말고**, 어느 두 클래스가 서로 참조하는지 기록한다(후속 6·7 판단 입력).

---

## File Structure

**B — SceneLifetimeScope 제거 (클·서 프로젝트만)**
- Delete: `LeagueOfPhysical-Client/Assets/Scripts/Scene/SceneLifetimeScope.cs` (+`.meta`)
- Delete: `LeagueOfPhysical-Server/Assets/Scripts/Scene/SceneLifetimeScope.cs` (+`.meta`)
- Modify: 클라 `Assets/Scripts/Entrance/EntranceLifetimeScope.cs`, `Lobby/LobbyLifetimeScope.cs`, `Room/RoomLifetimeScope.cs`
- Modify: 서버 `Assets/Scripts/Entrance/EntranceLifetimeScope.cs`, `Room/RoomLifetimeScope.cs`

**C — 속성 리네임 (GameFramework 정의 + 클·서 사용처)**
- Rename: `GameFramework/Runtime/Scripts/DI/DIMonoBehaviourAttribute.cs` → `SceneInjectMonoBehaviourAttribute.cs`
- Rename: `GameFramework/Runtime/Scripts/DI/DIGameObjectAttribute.cs` → `SceneInjectGameObjectAttribute.cs`
- Modify: `GameFramework/Runtime/Scripts/Extensions/DI.Extensions.cs` (타입 인자 2곳)
- Modify: `GameFramework/Editor/Scripts/DI/DIAttributeValidator.cs` (`typeof` 2곳)
- Modify(클): `Game/LOPRunner.cs`, `Game/LOPGamePresenter.cs`, `Login/LoginService.cs`, `Room/LOPNetworkAuthenticator.cs`
- Modify(서): `Game/LOPRunner.cs`, `Room/LOPNetworkAuthenticator.cs`

**A — 생성자 주입 전환 (클·서 프로젝트 Assembly-CSharp)**
- Modify(클): §A 대상 목록의 순수 C# 파일들
- Modify(서): §A 대상 목록의 순수 C# 파일들 (로컬 픽스처 3파일 제외)

---

## Task 1: B — 클라 SceneLifetimeScope 제거

**Files:**
- Delete: `Assets/Scripts/Scene/SceneLifetimeScope.cs` (+`.meta`)
- Modify: `Assets/Scripts/Entrance/EntranceLifetimeScope.cs`
- Modify: `Assets/Scripts/Lobby/LobbyLifetimeScope.cs`
- Modify: `Assets/Scripts/Room/RoomLifetimeScope.cs`

**Interfaces:**
- Consumes: VContainer `LifetimeScope`, `IObjectResolver.InjectSceneObjects(Scene)` (GameFramework 확장, 변경 없음).
- Produces: 3개 스코프가 `LifetimeScope` 직접 상속 + 빌드 콜백에서 `container.InjectSceneObjects(gameObject.scene)` 호출(= `GameLifetimeScope` 기존 패턴).

- [ ] **Step 1: 세 스코프의 현재 형태 확인**

Read `Assets/Scripts/Entrance/EntranceLifetimeScope.cs`, `Lobby/LobbyLifetimeScope.cs`, `Room/RoomLifetimeScope.cs`. 각각 `: SceneLifetimeScope`, `base.Configure(builder)` 호출 유무, 기존 `RegisterBuildCallback` 유무를 파악한다. (Lobby는 `base.Configure` 미호출, 자체 build callback 있음. Room은 `base.Configure` 호출 + 빈 build callback. Entrance는 `base.Configure` 호출.)

- [ ] **Step 2: EntranceLifetimeScope 재배선**

`: SceneLifetimeScope` → `: LifetimeScope`. `base.Configure(builder)` 호출 제거. `Configure` 끝에 빌드 콜백 추가(기존 콜백 있으면 그 안에 첫 줄로):

```csharp
builder.RegisterBuildCallback(container =>
{
    container.InjectSceneObjects(gameObject.scene);
});
```

`using GameFramework;`(InjectSceneObjects 확장) / `using VContainer.Unity;`(LifetimeScope)가 필요하면 추가. 파일 상단 using 확인.

- [ ] **Step 3: RoomLifetimeScope 재배선**

`: SceneLifetimeScope` → `: LifetimeScope`. `base.Configure(builder)` 호출 제거. 기존 빈 `RegisterBuildCallback(container => { })`가 있으면 그 본문에 `container.InjectSceneObjects(gameObject.scene);`를 넣는다(콜백 새로 만들지 말고 재사용).

- [ ] **Step 4: LobbyLifetimeScope 재배선**

`: SceneLifetimeScope` → `: LifetimeScope`. Lobby는 이미 `RegisterBuildCallback`에서 `IWindowManager`를 resolve해 `MatchMakingView`를 여는 콜백이 있다. 그 콜백 **본문 첫 줄**에 `container.InjectSceneObjects(gameObject.scene);`를 추가한다(WindowManager resolve보다 먼저). `base.Configure` 미호출이었으므로 제거할 base 호출 없음.

- [ ] **Step 5: SceneLifetimeScope 삭제**

```bash
git rm Assets/Scripts/Scene/SceneLifetimeScope.cs Assets/Scripts/Scene/SceneLifetimeScope.cs.meta
```

- [ ] **Step 6: 컴파일 검증 (클라 인스턴스)**

`refresh_unity(scope="all", force=true, unity_instance="<CLIENT>")` → `editor_state`의 `isCompiling==false` 대기 → `read_console(types=["error"], unity_instance="<CLIENT>")`. Expected: error 0. `SceneLifetimeScope` 잔여 참조가 있으면 여기서 CS0246으로 잡힌다(이전 조사상 참조 0이라 없어야 정상).

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Scene Assets/Scripts/Entrance/EntranceLifetimeScope.cs Assets/Scripts/Lobby/LobbyLifetimeScope.cs Assets/Scripts/Room/RoomLifetimeScope.cs
git commit -m "refactor(di): SceneLifetimeScope 제거, 씬 주입을 빌드 콜백으로 통일 (클라)"
```

---

## Task 2: B — 서버 SceneLifetimeScope 제거

**Files:**
- Delete: `Assets/Scripts/Scene/SceneLifetimeScope.cs` (+`.meta`) — 서버 repo
- Modify: 서버 `Assets/Scripts/Entrance/EntranceLifetimeScope.cs`, `Room/RoomLifetimeScope.cs`

> 경로는 모두 `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/` 기준.

**Interfaces:**
- Consumes/Produces: Task 1과 동일 패턴을 서버 스코프에 적용. 서버엔 Lobby 스코프가 없다(Entrance/Room만 `SceneLifetimeScope` 상속).

- [ ] **Step 1: 서버 브랜치 생성**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server checkout -b chore/di-cleanup
```

로컬 픽스처 변경(`ConfigureRoomComponent.cs`/`GameRuleSystem.cs`/`DefaultVolumeProfile.asset`)은 브랜치로 딸려온다 — 건드리지 않고 그대로 둔다.

- [ ] **Step 2: 서버 두 스코프 형태 확인**

Read 서버 `Entrance/EntranceLifetimeScope.cs`, `Room/RoomLifetimeScope.cs`. `base.Configure` / 기존 build callback 유무 파악.

- [ ] **Step 3: 서버 EntranceLifetimeScope 재배선**

Task 1 Step 2와 동일: `: SceneLifetimeScope` → `: LifetimeScope`, `base.Configure` 제거, 빌드 콜백에 `container.InjectSceneObjects(gameObject.scene);` 추가. using 보강.

- [ ] **Step 4: 서버 RoomLifetimeScope 재배선**

Task 1 Step 3과 동일: `: LifetimeScope`, `base.Configure` 제거, 빌드 콜백에 `container.InjectSceneObjects(gameObject.scene);` 추가(기존 콜백 있으면 재사용).

- [ ] **Step 5: 서버 SceneLifetimeScope 삭제**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server rm Assets/Scripts/Scene/SceneLifetimeScope.cs Assets/Scripts/Scene/SceneLifetimeScope.cs.meta
```

- [ ] **Step 6: 컴파일 검증 (서버 인스턴스)**

`refresh_unity(scope="all", force=true, unity_instance="<SERVER>")` → `isCompiling==false` 대기 → `read_console(types=["error"], unity_instance="<SERVER>")`. Expected: error 0.

- [ ] **Step 7: 커밋 (대상 파일만)**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Scene Assets/Scripts/Entrance/EntranceLifetimeScope.cs Assets/Scripts/Room/RoomLifetimeScope.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server commit -m "refactor(di): SceneLifetimeScope 제거, 씬 주입을 빌드 콜백으로 통일 (서버)"
```

로컬 픽스처 3파일이 `git status`에 여전히 남아 있는지 확인(커밋에 안 들어갔어야 함).

---

## Task 3: C — 씬 주입 속성 리네임 (GameFramework + 클·서 원자적)

**Files:**
- Rename: `GameFramework/Runtime/Scripts/DI/DIMonoBehaviourAttribute.cs` → `SceneInjectMonoBehaviourAttribute.cs` (+`.meta`)
- Rename: `GameFramework/Runtime/Scripts/DI/DIGameObjectAttribute.cs` → `SceneInjectGameObjectAttribute.cs` (+`.meta`)
- Modify: `GameFramework/Runtime/Scripts/Extensions/DI.Extensions.cs`
- Modify: `GameFramework/Editor/Scripts/DI/DIAttributeValidator.cs`
- Modify(클): `Game/LOPRunner.cs`, `Game/LOPGamePresenter.cs`, `Login/LoginService.cs`, `Room/LOPNetworkAuthenticator.cs`
- Modify(서): `Game/LOPRunner.cs`, `Room/LOPNetworkAuthenticator.cs`

**Interfaces:**
- Consumes: 없음(이름만 변경, 동작 동일).
- Produces: 속성명 `SceneInjectMonoBehaviourAttribute`(사용 시 `[SceneInjectMonoBehaviour]`), `SceneInjectGameObjectAttribute`(`[SceneInjectGameObject]`). 스캔 로직·시맨틱 불변.

> 이 태스크는 세 repo에 걸쳐 원자적이다. GameFramework 이름을 바꾸면 클·서 사용처가 즉시 깨지므로, 한 태스크에서 전부 고치고 마지막에 양쪽 컴파일을 함께 검증한다. 커밋은 repo별 3개.

- [ ] **Step 1: GameFramework 브랜치 생성**

```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework checkout -b chore/di-cleanup
```

- [ ] **Step 2: 속성 파일 rename (git mv, .cs+.meta)**

```bash
cd C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/DI
git mv DIMonoBehaviourAttribute.cs SceneInjectMonoBehaviourAttribute.cs
git mv DIMonoBehaviourAttribute.cs.meta SceneInjectMonoBehaviourAttribute.cs.meta
git mv DIGameObjectAttribute.cs SceneInjectGameObjectAttribute.cs
git mv DIGameObjectAttribute.cs.meta SceneInjectGameObjectAttribute.cs.meta
```

- [ ] **Step 3: 속성 클래스명 + 문서 주석 수정**

`SceneInjectMonoBehaviourAttribute.cs`:

```csharp
using System;

namespace GameFramework
{
    /// <summary>
    /// 씬에 미리 배치된(정적) MonoBehaviour를 표시한다. 씬 로드 시 씬 스코프의 스캔이
    /// 이 컴포넌트 하나에만 VContainer 주입을 수행한다(하위 자식 제외). 런타임에
    /// Instantiate/AddComponent로 생성되는 오브젝트는 이 스캔 대상이 아니며 별도로
    /// 주입해야 한다(IObjectResolver.Instantiate / Inject).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SceneInjectMonoBehaviourAttribute : Attribute { }
}
```

`SceneInjectGameObjectAttribute.cs`:

```csharp
using System;

namespace GameFramework
{
    /// <summary>
    /// 씬에 미리 배치된(정적) GameObject를 표시한다. 씬 로드 시 씬 스코프의 스캔이
    /// 이 GameObject와 그 하위 모든 컴포넌트에 VContainer 주입을 수행한다
    /// (InjectGameObject). 런타임 생성 오브젝트는 대상이 아니며 별도 주입이 필요하다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SceneInjectGameObjectAttribute : Attribute { }
}
```

(기존 파일의 namespace/using을 그대로 유지하되 클래스명·주석만 위와 같이. 파일 원본을 Read해 namespace가 `GameFramework`인지 확인 후 반영.)

- [ ] **Step 4: DI.Extensions.cs 타입 인자 수정**

`GameFramework/Runtime/Scripts/Extensions/DI.Extensions.cs`의 `InjectSceneObjects`에서:

```csharp
// Before
foreach (var DIGameObject in scene.FindGameObjectsWithAttribute<DIGameObjectAttribute>().OrEmpty())
    resolver.InjectGameObject(DIGameObject);
foreach (var DIMonoBehaviour in scene.FindComponentsWithAttribute<DIMonoBehaviourAttribute>().OrEmpty())
    resolver.Inject(DIMonoBehaviour);

// After
foreach (var go in scene.FindGameObjectsWithAttribute<SceneInjectGameObjectAttribute>().OrEmpty())
    resolver.InjectGameObject(go);
foreach (var mb in scene.FindComponentsWithAttribute<SceneInjectMonoBehaviourAttribute>().OrEmpty())
    resolver.Inject(mb);
```

(지역변수명 `DIGameObject`/`DIMonoBehaviour`도 타입명과 헷갈리므로 `go`/`mb`로 정리.)

- [ ] **Step 5: DIAttributeValidator.cs typeof 수정**

`GameFramework/Editor/Scripts/DI/DIAttributeValidator.cs`:

```csharp
Type[] attributeTypes = new Type[]
{
    typeof(SceneInjectGameObjectAttribute),
    typeof(SceneInjectMonoBehaviourAttribute),
};
```

- [ ] **Step 6: 클라 사용처 4곳 수정**

각 파일의 클래스 속성을 바꾼다:
- `LeagueOfPhysical-Client/Assets/Scripts/Game/LOPRunner.cs`: `[DIMonoBehaviour]` → `[SceneInjectMonoBehaviour]`
- `.../Game/LOPGamePresenter.cs`: `[DIMonoBehaviour]` → `[SceneInjectMonoBehaviour]`
- `.../Login/LoginService.cs`: `[DIMonoBehaviour]` → `[SceneInjectMonoBehaviour]`
- `.../Room/LOPNetworkAuthenticator.cs`: `[DIMonoBehaviour]` → `[SceneInjectMonoBehaviour]`

(모두 `[DIMonoBehaviour]`. `[DIGameObject]`는 클라에서 미사용.)

- [ ] **Step 7: 서버 사용처 2곳 수정**

- `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs`: `[DIMonoBehaviour]` → `[SceneInjectMonoBehaviour]`
- `.../Room/LOPNetworkAuthenticator.cs`: `[DIMonoBehaviour]` → `[SceneInjectMonoBehaviour]`

- [ ] **Step 8: 잔여 참조 스윕**

Grep `DIMonoBehaviour|DIGameObject`(코드만, docs 제외)로 클·서·GameFramework에 남은 참조가 없는지 확인. 남으면 위 목록 밖 사용처이므로 마저 수정.

- [ ] **Step 9: 양쪽 컴파일 검증**

GameFramework를 바꿨으므로 클·서 둘 다:
- `refresh_unity(scope="all", force=true, unity_instance="<CLIENT>")` → 대기 → `read_console(types=["error"], unity_instance="<CLIENT>")` = error 0
- `refresh_unity(scope="all", force=true, unity_instance="<SERVER>")` → 대기 → `read_console(types=["error"], unity_instance="<SERVER>")` = error 0

(rename으로 인한 stale CS2001이 뜨면 force 재스캔으로 해소.)

- [ ] **Step 10: repo별 커밋 (3개)**

```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework add Runtime/Scripts/DI Runtime/Scripts/Extensions/DI.Extensions.cs Editor/Scripts/DI/DIAttributeValidator.cs
git -C C:/Users/re5na/workspace/LOP/GameFramework commit -m "refactor(di): DI 씬 주입 속성 → SceneInject* 리네임 (씬 전용임을 명시)"

git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Game/LOPGamePresenter.cs Assets/Scripts/Login/LoginService.cs Assets/Scripts/Room/LOPNetworkAuthenticator.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "refactor(di): SceneInject* 속성명 반영 (클라 사용처)"

git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Room/LOPNetworkAuthenticator.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server commit -m "refactor(di): SceneInject* 속성명 반영 (서버 사용처)"
```

---

## A 전환 공통 레시피 (Task 4~6이 참조)

각 대상 파일에 대해:

1. **MonoBehaviour 게이트:** 파일을 Read. 클래스가 `: MonoBehaviour`(또는 그 파생: `MonoSingleton`/`MonoBehaviour` 상속 베이스)면 **건너뛴다**(생성자 주입 불가 — 필드 주입 유지). 순수 C#만 진행.
2. **변환:** 모든 `[Inject]` 필드를 생성자 파라미터로 옮긴다.

```csharp
// Before
public class EntitySpawner
{
    [Inject] private EntityRegistry entityRegistry;
    [Inject] private ActorRegistry actorRegistry;
}

// After
public class EntitySpawner
{
    private readonly EntityRegistry entityRegistry;
    private readonly ActorRegistry actorRegistry;

    public EntitySpawner(EntityRegistry entityRegistry, ActorRegistry actorRegistry)
    {
        this.entityRegistry = entityRegistry;
        this.actorRegistry = actorRegistry;
    }
}
```

- 필드는 `private readonly`로. 파라미터명 = 필드명(카멜케이스).
- 필드가 많은 경우도 동일(예: `GameEntityMessageHandler` ~25개 → 생성자 파라미터 25개 + 각 `this.x = x;`). 순서는 필드 선언 순서 유지.
- `[Inject]`가 이미 **메서드**에 붙어 있으면(생성자 대용 `[Inject] void Construct(...)`) 그 메서드 본문을 생성자로 옮기고 메서드 삭제.
- 다른 `[Inject]`가 없는 필드/프로퍼티는 그대로 둔다.
- `using VContainer;`가 `[Inject]` 때문에만 있었다면 제거(다른 VContainer 타입 사용 없을 때).

3. **등록 코드는 건드리지 않는다.** `Register<T>(Lifetime.Singleton)`은 생성자를 자동으로 찾는다.
4. MonoBehaviour인데 `[Inject]` 필드가 많아 지저분하면 이번 범위 아님 — 그대로 둔다(A는 순수 C#만).

**검증(그룹 끝):** 대상 인스턴스 `refresh_unity(scope="all", force=true)` → 컴파일 error 0. (순환 예외는 플레이 시 — Task 7.)

---

## Task 4: A — 클라 Entrance/Lobby 스코프 순수 C# 전환

**Files (각 파일 레시피 적용, MonoBehaviour면 스킵):**
- `Assets/Scripts/Entrance/EntranceComponent/LoginComponent.cs`
- `Assets/Scripts/Entrance/EntranceComponent/CheckUserComponent.cs`
- `Assets/Scripts/Entrance/EntranceComponent/JoinLobbyComponent.cs`
- `Assets/Scripts/Entrance/EntranceComponent/LoadMasterDataComponent.cs`
- `Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs`
- Lobby FSM/VM 중 `[Inject]` 필드를 쓰는 순수 C#(있으면). `MatchStateMachine`/States는 생성자에서 `IObjectResolver`를 받는 형태 — **A 대상 아님**(6번 소관, 그대로 둔다).

- [ ] **Step 1: 각 파일 레시피 적용**

위 "A 전환 공통 레시피"를 목록의 각 파일에 적용. 각 파일 처리 전 MonoBehaviour 게이트 확인. `EntranceScene.cs`는 MonoBehaviour일 가능성이 높으니 게이트에서 스킵될 것.

- [ ] **Step 2: 컴파일 검증 (클라)**

`refresh_unity(scope="all", force=true, unity_instance="<CLIENT>")` → 대기 → `read_console(types=["error"], unity_instance="<CLIENT>")` = error 0.

- [ ] **Step 3: 커밋**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add Assets/Scripts/Entrance Assets/Scripts/UI/DebugHud
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "refactor(di): Entrance/Lobby 순수 C# 서비스 생성자 주입 전환 (클라)"
```

(실제로 변경된 파일만 담기도록 `git status`로 확인 후 경로 조정.)

---

## Task 5: A — 클라 Game/Room 스코프 순수 C# 전환

**Files (레시피 적용, MonoBehaviour면 스킵):**
- 메시지 핸들러: `Room/MessageHandler/RoomSessionMessageHandler.cs`, `Game/MessageHandler/GameInfoMessageHandler.cs`, `GameEntityMessageHandler.cs`, `GameInputMessageHandler.cs`, `GameInputTimingMessageHandler.cs`, `GameWorldEventMessageHandler.cs`
- 넷코드: `Netcode/Reconciler.cs`, `Netcode/WorldEventSink.cs`, `Netcode/RemoteEntityInterpolator.cs`, `Netcode/LocalEntityInterpolator.cs`, `Netcode/LOPTickUpdater.cs`, `Netcode/RemoteInterpolationClock.cs`
- 엔티티: `Entity/EntityBinder.cs`, `Entity/EntitySpawner.cs`, `Entity/CharacterCreator.cs`, `Entity/ItemCreator.cs`
- 게임: `Game/AbilityActivator.cs`, `Game/AbilityDataProvider.cs`, `Game/StatusEffectDataProvider.cs`, `Game/PlayerHudCoordinator.cs`

> 주의: `LOPRunner`/`LOPGamePresenter`/`LOPRoom`/`WindowManager`/`LOPEntityView`/`CharacterNameplate`/`DamageFloaterEmitter`는 MonoBehaviour → 게이트에서 스킵. 인터폴레이터·`PlayerHudCoordinator`는 파일을 열어 MonoBehaviour 여부를 반드시 확인(EntityBinder가 spawned 오브젝트에 주입하는 대상 중 일부는 MonoBehaviour일 수 있음).

- [ ] **Step 1: 각 파일 레시피 적용**

목록의 각 파일에 "A 전환 공통 레시피" 적용. 파일마다 MonoBehaviour 게이트 먼저.

- [ ] **Step 2: 컴파일 검증 (클라)**

`refresh_unity(scope="all", force=true, unity_instance="<CLIENT>")` → 대기 → `read_console(types=["error"], unity_instance="<CLIENT>")` = error 0.

- [ ] **Step 3: 커밋**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add Assets/Scripts/Game Assets/Scripts/Netcode Assets/Scripts/Entity Assets/Scripts/Room/MessageHandler
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "refactor(di): Game/Room 순수 C# 서비스 생성자 주입 전환 (클라)"
```

(변경 파일만 담기도록 `git status` 확인. Task 3에서 이미 커밋한 `LOPRunner.cs` 등이 섞이지 않게.)

---

## Task 6: A — 서버 순수 C# 전환

**Files (레시피 적용, MonoBehaviour면 스킵, 로컬 픽스처 제외):**
- 메시지 핸들러: `Game/MessageHandler/GameInfoMessageHandler.cs`, `GameEntityMessageHandler.cs`, `GameInputMessageHandler.cs`
- 엔티티: `Entity/EntityBinder.cs`, `Entity/EntitySpawner.cs`, `Entity/CharacterCreator.cs`, `Entity/ItemCreator.cs`, `Entity/LOPAIController.cs`
- 게임: `Game/AbilityActivator.cs`, `Game/AbilityDataProvider.cs`, `Game/StatusEffectDataProvider.cs`, `Game/CombatConfigProvider.cs`
- Entrance: `Entrance/EntranceComponent/LoadMasterDataComponent.cs`

> **제외(로컬 픽스처):** `Entrance/EntranceComponent/ConfigureRoomComponent.cs`, `Game/GameRuleSystem.cs` — working-tree에 커밋 금지 로컬 변경이 있으므로 A 전환에서 뺀다. `PhysicsFollower.cs`/`LOPEntityView.cs`/`LOPRoom.cs`/`LOPNetworkAuthenticator.cs`/`EntranceScene.cs`는 MonoBehaviour → 게이트에서 스킵. `NetworkMessageDispatcher.cs`는 이미 생성자 주입(스킵).

- [ ] **Step 1: 각 파일 레시피 적용**

목록의 각 파일에 레시피 적용. MonoBehaviour 게이트 먼저. **제외 목록 파일은 열지 않는다.**

- [ ] **Step 2: 컴파일 검증 (서버)**

`refresh_unity(scope="all", force=true, unity_instance="<SERVER>")` → 대기 → `read_console(types=["error"], unity_instance="<SERVER>")` = error 0.

- [ ] **Step 3: 커밋 (대상 파일만, 픽스처 제외)**

변경 파일을 `git status`로 확인하고, **로컬 픽스처 3파일이 staged에 없음을 확인**한 뒤 대상만 담는다:

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Game/MessageHandler Assets/Scripts/Entity/EntityBinder.cs Assets/Scripts/Entity/EntitySpawner.cs Assets/Scripts/Entity/CharacterCreator.cs Assets/Scripts/Entity/ItemCreator.cs Assets/Scripts/Entity/LOPAIController.cs Assets/Scripts/Game/AbilityActivator.cs Assets/Scripts/Game/AbilityDataProvider.cs Assets/Scripts/Game/StatusEffectDataProvider.cs Assets/Scripts/Game/CombatConfigProvider.cs Assets/Scripts/Entrance/EntranceComponent/LoadMasterDataComponent.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server status   # ConfigureRoomComponent/GameRuleSystem/DefaultVolumeProfile 이 unstaged로 남았는지 확인
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server commit -m "refactor(di): 순수 C# 서비스 생성자 주입 전환 (서버)"
```

---

## Task 7: 통합 검증 — 플레이 스모크 + 순환 확인

**Files:** 없음(런타임 검증만).

**Interfaces:**
- Consumes: Task 1~6의 모든 변경.
- Produces: 순환 발견 기록(있으면), 정상 동작 확인.

- [ ] **Step 1: 클라 플레이 진입**

클·서 에디터 + 로컬 룸서버 + auth 픽스처가 준비된 상태에서(로컬 테스트 auth: 현재 게스트 uuid가 서버 playerList에 있는지 확인 — 없으면 DB/서버 재시작 이슈), 클라를 플레이해 Entrance → Lobby → Room/Game까지 진입한다. 진입 자체가 각 LifetimeScope를 빌드하므로 순환/주입 실패가 여기서 드러난다.

- [ ] **Step 2: 순환 예외 확인**

각 씬 진입 후 `read_console(types=["error"], unity_instance="<CLIENT>")` 및 `unity_instance="<SERVER>"`. `VContainerException: Circular dependency detected`가 있으면:
- 예외 메시지의 타입 체인에서 서로 참조하는 두 클래스를 식별해 기록한다.
- **필드 주입으로 되돌리지 않는다.** 기록만 하고 후속(6번 FSM/7번 순서 의존 재검토)에서 다룬다. 단, 진입 자체가 막혀 스모크가 불가하면 해당 순환은 즉시 해결이 필요 — 그 경우 사용자에게 보고하고 최소 우회(`Func<T>` 팩토리 1건)를 논의.

- [ ] **Step 3: 씬 주입 동작 확인**

`[SceneInjectMonoBehaviour]` 대상(`LOPRunner`)이 정상 주입돼 게임이 돌아가는지(캐릭터 스폰·이동) 확인. 리네임/스코프 재배선이 씬 주입을 깨지 않았음을 실증.

- [ ] **Step 4: 결과 보고**

컴파일 clean(클·서) + 플레이 스모크 통과 여부, 발견된 순환 목록(있으면)을 사용자에게 보고. 6·7 진행 여부 결정 요청.

---

## Self-Review 결과

- **Spec coverage:** A(Task 4~6, 클·서), B(Task 1~2, 클·서), C(Task 3, 3 repo) 모두 태스크 존재. 서버 로컬 픽스처 제외(Global Constraints + Task 6) 반영. 인스턴스 핀(Global Constraints + 각 검증 스텝) 반영. 6·7·동적경로는 범위 밖으로 Task 7 Step 2/4에서 후속 처리.
- **Placeholder scan:** A 전환은 40여 파일이라 파일별 full before/after 대신 "A 전환 공통 레시피"(완전한 기계적 변환 절차 + 워크드 예시)를 정의하고 각 태스크가 대상 파일 목록 + MonoBehaviour 게이트를 명시 — 변환 소스(각 파일의 `[Inject]` 필드)는 파일 자체가 진실원본이라 중복 기재하지 않음(placeholder 아님, DRY).
- **Type consistency:** 속성명 `SceneInjectMonoBehaviourAttribute`/`SceneInjectGameObjectAttribute`(사용형 `[SceneInject...]`)를 정의(Task 3 Step 3)·확장(Step 4)·검증기(Step 5)·사용처(Step 6~7)에서 일관 사용. `InjectSceneObjects`(GameFramework 확장, 불변) B에서 재사용.
