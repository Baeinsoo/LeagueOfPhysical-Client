# Game을 Runner에 흡수 (2층화) Implementation Plan (Slice 4 ③)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** `LOPGame`(IGame)을 `LOPRunner`(IRunner)에 흡수해 핵심부를 2층 `Runner → World`로. **먼저 비우고(맵·룰 추출) 그 다음 합친다.** 각 단계 컴파일·동작 보존.

**Spec:** `docs/superpowers/specs/2026-06-25-game-into-runner-design.md`

**Architecture:** 3-repo(GameFramework 인터페이스 + Client + Server). Phase 1(비우기)은 추가·이동형(저위험), Phase 2(합치기)는 인터페이스 재설계(코디네이트 필요).

**Tech Stack:** Unity / C# / VContainer DI / Addressables / UnityMCP(클·서 컴파일 검증).

---

## 레포 / 도구 참조
- **GameFramework:** `C:\Users\re5na\workspace\LOP\GameFramework`
- **Client:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client`
- **Server:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server`
- **UnityMCP:** `mcpforunity://instances`에서 Client/Server id(해시 변동). 모든 호출 `unity_instance` 명시. (직전 Client `@de70658b9450cbb4` / Server `@f99391fa2dbaaf3c`.)

**⚠️ EOL:** mass sed 금지. 신규=Write, 편집=Edit. 커밋 후 worktree CRLF는 git이 처리(autocrlf).
**.meta:** 신규 `.cs`는 `refresh_unity` 시 Unity가 `.meta` 생성 → `.cs`+`.cs.meta` 함께 커밋. 삭제 시 `.cs`+`.cs.meta` 함께 삭제(`git rm`).
**픽스처 보존:** `git add -A` 금지. 명시 파일만. Client 픽스처(Art/UIRoot.prefab/Room.unity/ProjectSettings)·Server 픽스처(Room.unity/ConfigureRoomComponent.cs/LOPGame... ⚠️ 주의: 서버 `LOPGame.cs`는 이 작업의 *대상*이라 픽스처 아님 — 변경 커밋 대상)·Art 커밋 금지.
**컨벤션:** World 타입 풀 네임스페이스. 씬("LOPGame")·스코프(`GameLifetimeScope`) 명 유지.

---

## 확정 설계 (실측 기반)

- **`IRunner`가 이미 `Run(long,double,double)`·`Stop()` 보유** (RunnerBase 구현). `IGame`이 더하는 건 `onGameStateChanged`·`gameState`뿐(+`runner` 자기참조=제거). → 병합 = IRunner에 2멤버 추가.
- **`IGameState`** = 빈 마커(GameFramework). 구체 상태(`Initializing/Initialized/Playing/Paused/GameOver`)는 LOP-side(`LOPGameState.cs`). → 상태 *값* 세팅은 LOPRunner(LOP), 저장·이벤트는 LOPRunner가 직접 구현(RunnerBase는 LOP 상태 모름).
- **`RunnerBase.Run/Stop`을 virtual로** → LOPRunner가 override해 `base.Run(); gameState=Playing;` 식으로 상태 전이 주입. `InitializeAsync`는 이미 virtual.
- **맵 로딩**: 클·서 `LOPGame.InitializeAsync`의 `Addressables.LoadSceneAsync(".../FlapWangMap.unity", Additive)` + Deinit의 `UnloadSceneAsync(handle)`. → `IMapLoader`로 추출, `LOPGameFactory`가 호출. (맵 씬 주입은 `GameLifetimeScope.OnSceneLoaded`가 sceneLoaded 구독으로 처리 — Factory가 *씬/스코프 빌드 후* 맵 로드하면 그대로 유효.)
- **서버 룰(#6)**: `HandleItemTouch`(exp), 초기 플레이어 생성 루프(Init 102-143), `SpawnEnemies`/`SpawnEnemy`/`GetRandomSpawnPosition`/`DespawnEntity`, `LateUpdate` 스폰 타이머. → 서버 룰 컴포넌트로. **GameOver 타이머(LateUpdate 219-222)는 gameState 전이라 Phase 2에서 LOPRunner로** (룰 컴포넌트 아님 — 매치 라이프사이클).

---

## Task 0: 3개 repo 피처 브랜치
- [ ] **Step 1: working tree 확인** (픽스처 외 깨끗, main 최신)
```bash
for r in GameFramework LeagueOfPhysical-Client LeagueOfPhysical-Server; do echo "== $r =="; git -C C:/Users/re5na/workspace/LOP/$r status --short; git -C C:/Users/re5na/workspace/LOP/$r branch --show-current; done
```
- [ ] **Step 2: 브랜치 생성**
```bash
for r in GameFramework LeagueOfPhysical-Client LeagueOfPhysical-Server; do git -C C:/Users/re5na/workspace/LOP/$r checkout -b feature/game-into-runner; done
```

---

## Task 1 — Phase 1a: 맵 로딩 → `IMapLoader` 추출 (`game.Init` 유지)

목표: 클·서 `LOPGame.InitializeAsync`의 인라인 Addressables를 `IMapLoader.LoadAsync()`로 교체. **호출 위치·`runner.Init` 병렬 순서 그대로. 동작 보존.**

> ⚠️ **구현 교정(2026-06-25):** 아래 Step들은 당초 "맵 → `LOPGameFactory`로 이동"을 가정했으나, 그러면 맵 로드(멀티프레임 대기)가 `runner.Init` *앞*에 끼어들어 LateUpdate가 미초기화 `tickUpdater`를 건드리는 NRE 발생(타이밍/구조 변경 = 동작 보존 위반). **실제 구현 = 맵은 `game.Init`에 유지**(인라인 Addressables → `var t = mapLoader.LoadAsync(MapId); await runner.Init; await t;` — 병렬 보존), **`IMapLoader`는 게임 스코프(`GameLifetimeScope`) 등록**, **`AddressablesMapLoader`는 LOP-Shared 1벌**(`handle.Task` await). Factory·RoomLifetimeScope는 무변경. 아래 Factory 기반 Step 3·4는 폐기.

- [ ] **Step 1: GameFramework `IMapLoader` 인터페이스 생성** — `Runtime/Scripts/Game/IMapLoader.cs`
```csharp
using System.Threading.Tasks;
namespace GameFramework
{
    public interface IMapLoader
    {
        Task LoadAsync(string mapId);
        Task UnloadAsync();
    }
}
```

- [ ] **Step 2: Client `AddressablesMapLoader` 구현** — `Assets/Scripts/Game/AddressablesMapLoader.cs`. `LoadAsync`가 `Addressables.LoadSceneAsync(mapId, Additive)` 후 handle 보관·완료 대기, `UnloadAsync`가 `UnloadSceneAsync(handle)`. (현 LOPGame의 map 로직 그대로 이전.)

- [ ] **Step 3: Client DI 등록** — `IMapLoader`를 **Room 스코프**(`RoomLifetimeScope`)에 등록(Factory가 Room 스코프라 주입 가능). `builder.Register<GameFramework.IMapLoader, AddressablesMapLoader>(Lifetime.Singleton);`

- [ ] **Step 4: Client `LOPGameFactory`가 맵 로드/언로드** — `[Inject] IMapLoader mapLoader` 추가. `CreateAsync`에서 씬 로드+scope 빌드 *후* `await mapLoader.LoadAsync(mapId)`. `DestroyAsync`에서 씬 언로드 *전* `await mapLoader.UnloadAsync()`. mapId는 일단 `"Assets/Art/Scenes/FlapWangMap.unity"` 상수(추후 roomDataStore.match.mapId — Open).

- [ ] **Step 5: Client `LOPGame`에서 맵 제거** — `LOPGame.cs`의 `handle` 필드, `InitializeAsync`의 `Addressables.LoadSceneAsync`(69)+`WaitUntil(handle.IsDone)`(73), `DeinitializeAsync`의 `UnloadSceneAsync`(91) 제거. (Initialized 상태는 runner.Init 직후로.)

- [ ] **Step 6: Server 평행 적용** — Server `AddressablesMapLoader` + RoomLifetimeScope 등록 + `LOPGameFactory` 주입·호출 + `LOPGame`에서 맵 제거(96, 100의 WaitUntil은 플레이어 생성 전이라 주의 — 플레이어 생성은 1b에서 룰로 이동, 여기선 맵만 제거하고 WaitUntil은 룰 이동까지 유지하거나 Factory가 보장). **순서:** Factory가 맵 로드 완료 후 runner 반환 → Room이 runner.Run → 룰이 플레이어 생성. 맵이 먼저 보장됨.

- [ ] **Step 7: 컴파일 검증** — 양쪽 `refresh_unity`(all,force) → `read_console` 0. 신규 .meta 확인.

---

## Task 2 — Phase 1b: 서버 게임 룰 → 룰 컴포넌트 (서버만)

목표: 서버 `LOPGame`의 월드-변형 룰(스폰/exp/초기플레이어)을 별도 컴포넌트로. **GameOver 타이머는 남김(Phase 2서 Runner로).** 동작 보존.

- [ ] **Step 1: Server `GameRuleSystem` 생성** (명명 Open — Quantum `*System` 정합) — 순수 C#(또는 게임 씬 컴포넌트), GameLifetimeScope 등록. 보유 의존: `roomDataStore, sessionManager, entityCreationDataFactory, entityRegistry, rng, levelSystem, statsSystem, IRunner`(entityManager·tick용). 책임:
  - **초기 플레이어 생성** (LOPGame.cs:102-143 이전) — init 시점 1회.
  - **적 스폰** (`SpawnEnemies`/`SpawnEnemy`/`GetRandomSpawnPosition`/`DespawnEntity` + 타이머) — **틱 구동**(`runner.tickUpdater.onTick` 구독, *결정론 순서*; LateUpdate 아님).
  - **ItemTouch→exp** (`HandleItemTouch` + EventBus 구독/해제) 이전.
  > 구동: init(IInitializableAsync 또는 명시 호출)에서 플레이어 생성 + 이벤트 구독 + onTick 구독. 정확한 라이프사이클 훅은 실행 시 결정(룰은 Runner init 후 활성).

- [ ] **Step 2: Server `LOPGame`에서 룰 제거** — `HandleItemTouch`, 초기 플레이어 루프(102-143), `#region Spawn` 전체, ItemTouch 구독/해제(74,152), `lastEnemySpawnTime`, LateUpdate의 스폰 블록(210-217) 제거. **남김:** LateUpdate의 GameOver 타이머(219-222), 물리설정, 핸들러등록, gameState, 생명주기 위임. → 서버 LOPGame이 클라와 거의 같은 얇은 wrapper(+GameOver 타이머)만.

- [ ] **Step 3: 미사용 의존 정리** — 룰로 옮겨간 `[Inject]`(roomDataStore/sessionManager/entityCreationDataFactory/entityRegistry/rng/levelSystem/statsSystem)를 LOPGame에서 제거.

- [ ] **Step 4: 컴파일 검증** — Server `read_console` 0. 신규 .meta.

---

## Task 3 — Phase 2: Game → Runner 흡수 (인터페이스 재설계, 3-repo 코디네이트)

> 이 Task는 인터페이스 변경이 컴파일을 깨므로 **GameFramework→Client→Server를 한 호흡으로** 진행 후 일괄 검증. 단계는 논리 순서.

### 2a — GameFramework 인터페이스
- [ ] **Step 1: `IRunner`에 gameState 멤버 추가** — `IRunner.cs`에:
```csharp
event System.Action<IGameState> onGameStateChanged;
IGameState gameState { get; }
```
- [ ] **Step 2: `RunnerBase.Run/Stop`을 virtual로** — `public virtual void Run(...)`, `public virtual void Stop()`. (LOPRunner가 상태 전이 override 가능하게.)
- [ ] **Step 3: `IGame` 삭제** — `IGame.cs` `git rm`(+ .meta). 참조처(IRoom/IGameFactory/MonoGamePresenter)는 아래서 교체.
- [ ] **Step 4: `IRoom.game` 타입 교체** — `IRoom.cs`의 `IGame game` → `IRunner runner`(멤버명도 `runner`로). `StartGameAsync` 유지.
- [ ] **Step 5: `IGameFactory` 반환 타입** — `CreateAsync : Task<IGame>` → `Task<IRunner>`. (명명 `IGameFactory` 유지 vs `IRunnerFactory` — Open; 유지 추천, 게임 세션 팩토리.)
- [ ] **Step 6: `IGamePresenter<T>`/`MonoGamePresenter<T>` 점검** — `T game` 필드가 `IGame` 제약인지 확인. 제약 있으면 풀거나 `IRunner` 기반으로. (LOPGamePresenter가 `<LOPRunner>`로 갈 수 있게.)

### 2b — Client 구현 이전 + LOPGame 삭제
- [ ] **Step 7: `LOPRunner`에 흡수** —
  - `gameState` 필드 + `onGameStateChanged` 이벤트 구현(LOPGame.cs:18,26-40의 setter 로직 그대로).
  - `InitializeAsync` override: `gameState=Initializing` → 물리설정(simMode/gravity/Restorer, LOPGame 51-62) + 핸들러 등록(64-67) → `await base.InitializeAsync()` → `gameState=Initialized`.
  - `DeinitializeAsync` override: `await base.Deinit()` 흐름 + 핸들러 해제 + restorer.Dispose.
  - `Run` override: `base.Run(...)` → `gameState=Playing`. `Stop` override: `base.Stop()` → `gameState=Paused`.
  - `[Inject] IEnumerable<IGameMessageHandler> gameMessageHandlers` 추가(LOPGame서 이전).
- [ ] **Step 8: `LOPGame` 삭제** (Client) — `git rm Assets/Scripts/Game/LOPGame.cs`(+.meta).
- [ ] **Step 9: `GameLifetimeScope`(Client)** — `[SerializeField] LOPGame game` 필드 + `RegisterComponent(game).As<IGame>()`(40) 제거. runner 등록 유지(이미 `As<IRunner>()`). OnSceneLoaded 주석의 "LOPGame이 로드" → "Factory가 로드"로.
- [ ] **Step 10: `LOPGamePresenter`(Client)** — `MonoGamePresenter<LOPGame>` → `<LOPRunner>`. `game = GetComponent<LOPGame>()` → `GetComponent<LOPRunner>()`. `game.onGameStateChanged` 구독 유지(이제 Runner가 발화). (필드명 `game`은 유지하거나 `runner`로 — 가독성 선택.)
- [ ] **Step 11: `LOPRoom`(Client)** — `IGame game` → `IRunner runner`(factory 반환). `game.InitializeAsync/Run/Stop/onGameStateChanged` → `runner.*`. `gameFactory.CreateAsync()` 반환 타입 변경 반영.
- [ ] **Step 12: 씬(LOPGame.unity) 조정** — LOPGame GameObject/컴포넌트 제거. **이건 Unity 에디터 작업**: `manage_gameobject`/씬 편집으로 LOPGame 컴포넌트 제거 + GameLifetimeScope.game 직렬화 참조 정리 + LOPGamePresenter가 같은 GameObject의 LOPRunner를 GetComponent하도록 colocate 확인. (LOPRunner·TickUpdater·EntityManager·LOPGamePresenter colocate 유지.) ⚠️ 픽스처 주의 — 씬 변경은 의도분만.

### 2c — Server 평행
- [ ] **Step 13: Server `LOPRunner` 흡수** — Client Step 7과 동형 + **GameOver 타이머**(LOPGame LateUpdate 219-222)를 LOPRunner로(서버): `UpdateRunner` 또는 별도 체크에서 `elapsedTime > 300 → gameState=GameOver`. 물리설정·핸들러등록 동일.
- [ ] **Step 14: Server `LOPGame` 삭제** + `GameLifetimeScope`/`LOPGamePresenter`(있으면)/`LOPRoom`(`IServerRoom`)/씬 조정 — Client Step 8~12 동형. 서버 룰 컴포넌트(1b)는 runner를 주입받게 유지.
- [ ] **Step 15: 컴파일 검증** — 양쪽 `refresh_unity`(all,force) → `read_console` 0.

---

## Task 4: 종합 검증
- [ ] **Step 1: 양쪽 재스캔+컴파일 0에러.**
- [ ] **Step 2: 신규/삭제 .meta 확인** — IMapLoader/AddressablesMapLoader/GameRuleSystem 신규 .meta 생성; IGame.cs.meta/LOPGame.cs.meta(클·서) 삭제됨.
- [ ] **Step 3: 플레이 검증(사용자)** — 입장→로딩뷰(Initialized)→플레이(Playing)→(서버)적 스폰·아이템→경험치·5분 GameOver→로비(클)/Closed(서). 이동/점프/물리/데미지/죽음 *이전과 동일*. 맵 로드·언로드 정상.

---

## Task 5: 커밋 (repo별, 단계별)
> Phase 1a/1b는 독립 커밋 가능. Phase 2는 3-repo 한 묶음(인터페이스 깨짐).

- [ ] **Step 1: Phase 1a 커밋** — GameFramework(IMapLoader), Client(AddressablesMapLoader+factory+scope+LOPGame), Server(동형). 메시지: `refactor(game): extract map loading to IMapLoader, factory orchestrates`.
- [ ] **Step 2: Phase 1b 커밋** — Server(GameRuleSystem + LOPGame 룰 제거). `refactor(game): extract server game rules to GameRuleSystem (destined for World)`.
- [ ] **Step 3: Phase 2 커밋** — GameFramework(IRunner+gameState/IGame삭제/IRoom/IGameFactory), Client(LOPRunner흡수/LOPGame삭제/scope/presenter/room/scene), Server(동형). `refactor(game): absorb Game into Runner — core is now 2-layer Runner -> World`.
- [ ] **Step 4: Client 문서 커밋** — spec+plan.

---

## Task 6: 검증 + 머지 (사용자)
- [ ] **Step 1: 최종 컴파일 0 + 플레이 OK.**
- [ ] **Step 2: 머지/푸시(사용자 요청 시)** — GameFramework → Client → Server `--no-ff`.

---

## Self-Review (작성자 체크)
- **Spec 커버리지:** 1a 맵=Task1 / 1b 룰=Task2 / 2 합치기=Task3 / 검증=Task4 / 커밋=Task5. ✅
- **Run 충돌 해소:** IRunner가 이미 Run/Stop 동일 시그니처 → 충돌 없음. IGame은 gameState/onGameStateChanged만 추가. ✅
- **상태 값 위치:** 구체 상태는 LOP-side라 LOPRunner가 gameState 구현·전이, RunnerBase는 Run/Stop virtual만 제공. ✅
- **걸리는 둘 처리:** 맵→Factory(World 아님=엔진-프리), 룰→서버 컴포넌트(World行 예약, Runner 몸통 아님). GameOver는 매치 라이프사이클이라 Runner로. ✅
- **동작 보존:** 각 Phase 기능 이동. 1a/1b는 LOPGame 존재 유지하며 책임만 이사 → 저위험. 2는 인터페이스 코디네이트. ✅
- **씬 직렬화 리스크:** Phase 2 Step12/14가 씬 편집(LOPGame 컴포넌트 제거 + 참조 정리). 머지 후 git diff로 의도분 확인. ⚠️ 명시.
- **EOL/픽스처:** 신규=Write/편집=Edit, 삭제=git rm(.cs+.meta), 명시 파일만 add. ✅
- **Open(plan 중 확정):** GameRuleSystem 명명, IGameFactory→IRunnerFactory 여부, mapId 인자화, MonoGamePresenter 제약, 룰 컴포넌트 구동 훅(onTick 구독 형태). ✅
