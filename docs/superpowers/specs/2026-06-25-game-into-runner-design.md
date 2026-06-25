# Game을 Runner에 흡수 — 핵심부 2층화 `Runner → World` (Slice 4 ③)

**Date:** 2026-06-25
**Branch (제안):** `feature/game-into-runner` (GameFramework / Client / Server)
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (Engine↔Simulation 컴포지션) · [LOP 저장소 토폴로지](../../lop-repo-topology.md) · [game-scene-scope 설계](2026-06-06-game-scene-scope-design.md) (이미 구현됨) · 프로젝트 메모리 `world-core-runner-world-naming` · 선행: ①Engine→Runner 리네임(완료) · ②sim추출=4b(완료) · 4c #1 이동 커널(완료)

## Goal

진행자층 `LOPGame`(IGame)을 호스트 `LOPRunner`(IRunner)에 흡수해, 핵심부를 **2층 `Runner → World`**로 만든다. 현재는 4b로 sim(World)이 분리되면서 핵심부가 *일시적으로 3층*(`Game → Runner → World`)인데, ③가 Game을 Runner에 합쳐 2층으로 수렴시킨다.

**동작 보존**(흡수·이동이지 기능 변경 0). 큰 수술이라 **단계별로 쪼개** 각 단계가 컴파일·동작을 보존하게 한다.

## 배경 / 동기

### 왜 2층화인가 (계층 history)

```
원래:        Room → Game ─────────→ GameEngine        (핵심부 2층, 엔진=구동+시뮬 뭉침)
4b 후(지금): Room → Game → Runner ──→ World            (핵심부 3층 — 과도기!)
③ 후(목표):  Room → Runner ─────────→ World            (핵심부 2층 — 깔끔)
```

- 원래 `GameEngine`이 *구동기 + 시뮬*을 뭉쳐 가져 지저분했다.
- ① 리네임 + ② sim추출(4b)로 시뮬을 `World`로 떼어냈더니, `Game`(진행자)·`Runner`(구동기)·`World`(시뮬)가 *셋 다 따로* = 일시적 3층.
- ③가 `Game`을 `Runner`에 합쳐 **구동기+진행자 = Runner / 시뮬 = World** 의 깔끔한 2층으로.
- 업계 정석: 핵심부 2층(Quantum `Runner→Game`, Quake `engine→qvm`, DOTS `loop→World`). 호스트(Runner)가 시뮬(World)을 보유·구동.

### game-scene-scope는 이미 구현됨 (씬/스코프 무변경)

`docs/superpowers/specs/2026-06-06-game-scene-scope-design.md`가 제안한 "Game을 별도 씬+Room 자식 스코프로"는 **이미 코드에 구현돼 있다**: Game은 "LOPGame" 씬에 살고, `LOPGameFactory`가 Room 자식 스코프로 동적 로드. → **③는 씬/스코프 경계를 바꾸지 않는다.** 그 씬 안의 *두 형제 MonoBehaviour(`LOPGame`+`LOPRunner`)를 하나로 합치는* 작업이다. 씬 이름("LOPGame")·스코프명(`GameLifetimeScope`)은 "게임플레이 씬/스코프"로 유지.

## 현재 상태 (실측)

`LOPGame`(클 `Assets/Scripts/Game/LOPGame.cs`, 서 동일 경로) = `MonoBehaviour, IGame`. `LOPRunner`와 **형제 컴포넌트**(둘 다 GameLifetimeScope에 `RegisterComponent`). `LOPGame`이 `[Inject] IRunner runner`로 받아 생명주기를 **얇게 위임**한다.

**`LOPGame`이 지는 책임:**

| # | 책임 | 위치 | 클/서 |
|---|---|---|---|
| 1 | Runner 생명주기 위임 (`InitializeAsync→runner.InitializeAsync`, `Run→runner.Run`, `Stop→runner.Stop`, `Deinit`) | LOPGame | 양쪽 |
| 2 | 물리 전역 설정/원복 (`simulationMode=Script`, `gravity×2`, `autoSyncTransforms`, `Restorer`) | InitializeAsync | 양쪽 |
| 3 | 맵 씬 로드/언로드 (`Addressables.LoadSceneAsync("...FlapWangMap")`, mapId 기반 예정) | InitializeAsync/Deinit | 양쪽 |
| 4 | gameMessageHandler 등록/해제 | Init/Deinit | 양쪽 |
| 5 | `gameState` + `onGameStateChanged` (Initializing/Initialized/Playing/Paused/GameOver) | LOPGame | 양쪽 |
| 6 | 게임 룰 — 적 스폰, ItemTouch→경험치, GameOver 타이머, 초기 플레이어 엔티티 생성 | LateUpdate 등 | **서버만** |

**경계 타입 (load-bearing):**
- `IGame`(GameFramework) — `: IInitializableAsync, IDeinitializableAsync`, 멤버 `onGameStateChanged`/`gameState`/**`IRunner runner`**/`Run(tick,interval,elapsed)`/`Stop()`.
- `IRunner`(구 IGameEngine) — `entityManager`/`tickUpdater`/`networkTime`/`Run`/`Stop`/`UpdateRunner`/리스너.
- `IRoom.game : IGame` / `IGameFactory.CreateAsync : Task<IGame>` / `LOPGamePresenter : MonoGamePresenter<LOPGame>`.
- `Runner.current`/`Runner.Time`/`Runner.NetworkTime` 정적 facade — 이미 `IRunner` 기반이라 **무변경(2층화에 유리한 자산)**.

`gameState` 구독자: `LOPRoom`(GameOver→Lobby/Closed), `LOPGamePresenter`(Initialized→로딩뷰, Playing→닫기).

## 설계

### 큰 그림 — "먼저 비우고, 그 다음 합친다"

`LOPGame`의 책임 중 #1·#2·#4·#5는 Runner로 흡수해도 자연스럽다(순수 위임/호스트 셋업/상태). **걸리는 건 #3(맵 로딩)·#6(게임 룰)** — 둘을 Runner 몸통에 넣으면 호스트가 다시 비대해진다. 그래서 **먼저 #3·#6을 제 집으로 빼서 Game을 *얇게* 만든 뒤**(1단계), 얇아진 Game을 Runner에 흡수한다(2단계).

### 맵 로딩(#3) → `IMapLoader` 추출 (호출 위치는 `game.Init` 유지)

- 새 인터페이스 `IMapLoader`(맵 씬 load/unload). 구현 = Addressables 기반(`AddressablesMapLoader`, LOP-Shared 1벌).
- **호출 위치 = `game.InitializeAsync` 그대로 유지** — 인라인 `Addressables.LoadSceneAsync` 를 `mapLoader.LoadAsync()` 로 바꾸는 *순수 추출*만. **`runner.Init`과 병렬**(맵 로드 시작 → `await runner.Init` → `await 맵`) 패턴 보존. Phase 2엔 `game.Init`이 `Runner.Init`이 되니 자연히 거기로 감.
- **World엔 두지 않음** — World는 순수 시뮬(엔진-프리)이라 Unity 씬을 만질 수 없다.
- mapId는 `roomDataStore.match.mapId`(현재 하드코딩 `FlapWangMap`) 기반으로.

> ⚠️ **교정(구현 중 발견):** 당초 "맵 → `LOPGameFactory`로 이동"을 적었으나, 호출 위치를 Factory(=`game.Init` *앞*)로 옮기면 맵 로딩(멀티프레임 대기)이 `runner.Init` *앞*에 끼어들어 LateUpdate가 미초기화 `tickUpdater`를 건드리는 NRE가 생긴다(타이밍/구조 변경 = behavior-preserving 위반). → **호출 위치 이동은 설계 변경이지 추출이 아니므로 제외.** 추출은 *위치·순서 그대로, 구현만 교체*. (Factory 오케스트레이션 아이디어가 필요하면 별도 슬라이스로.)

### 게임 룰(#6) → 별도 서버 컴포넌트, **목적지 = World 시스템**

- **검증된 표준(2026-06-25 리서치):** 게임 룰(스폰/점수/승패)은 *시뮬의 "시스템"* 에 속하고 호스트/엔진엔 속하지 않는다 (Quantum `SpawnSystem`/`ScoreSystem`/상태머신, Quake 서버 game 모듈, ECS systems). **Runner(호스트)에 룰을 넣는 건 아키텍처상 틀림.** Quantum은 *"이벤트로 게임플레이 구현 금지(비결정론)"* 까지 명시 → 룰은 *결정론적 순서로 도는 시스템*.
- **그래서 ③에서는:** 서버 룰을 `LOPGame`에서 **별도 서버 룰 컴포넌트로 추출**(인터페이스화). Runner 몸통엔 넣지 않는다. **최종 집은 World 시스템**이며, 진짜 이주는 **별도 슬라이스(4c 룰 / Stage④)**. ③는 "Game→Runner 구조 합치기"에 집중하고 룰은 *잘못된 위치(Runner)로 가지 않게* 임시 분리만.
- 추출 형태: 룰 단위 인터페이스(예: `IGameRule`) + 서버 틱 구동, *결정론 순서*(이벤트 콜백 아님). 정확한 명명·구동 메커니즘은 plan에서 확정(목적지 World 시스템과 정합되게).

### 얇아진 Game을 Runner에 흡수 (#1·#2·#4·#5)

- **`IGame`을 `IRunner`에 병합**: `IRunner`가 `onGameStateChanged`/`gameState`/`Run(tick,interval,elapsed)`/`Stop()`을 갖는다. `IGame.runner` 멤버는 *소멸*(Runner가 곧 자기 자신). `IGame` 인터페이스 삭제.
- **물리 설정(#2)** → `RunnerBase`/`LOPRunner`의 InitializeAsync (Runner가 이미 `physicsSimulator`로 물리 틱을 돌리므로 정합).
- **메시지 핸들러 등록(#4)** → Runner Init/Deinit.
- **gameState(#5)** → Runner가 보유·발화. 구독자(`LOPRoom`·`LOPGamePresenter`)는 *Runner를* 구독.
- **경계 타입 교체**: `IRoom.game:IGame` → `IRunner`(멤버명 `runner` 등); `IGameFactory.CreateAsync:Task<IGame>` → `Task<IRunner>`; `LOPGamePresenter : MonoGamePresenter<LOPGame>` → `<LOPRunner>`(또는 IRunner 의존).
- `LOPGame` 클래스 삭제. GameLifetimeScope의 `RegisterComponent(game).As<IGame>()` 제거(Runner 등록만 남음).

### 무변경 (자산)
- `Runner.current`/`Runner.Time`/`Runner.NetworkTime` 정적 facade(이미 IRunner 기반).
- 씬("LOPGame")·스코프(`GameLifetimeScope`)·Room↔Game 동적로드(EnqueueParent) 메커니즘.
- `IRoom`(세션) 자체 책임.

## Phasing (각 단계 컴파일·동작 보존)

> 정확한 repo별 task·순서는 plan에서. 여기선 단계 경계만.

- **1단계 — Game 비우기** (Game은 아직 존재, 책임만 이사):
  - **1a 맵 로딩 추출**: `IMapLoader`(GameFramework) + `AddressablesMapLoader`(LOP-Shared 1벌). `LOPGame.InitializeAsync`의 인라인 Addressables를 `mapLoader.LoadAsync()`로 교체(**호출 위치·`runner.Init` 병렬 순서 그대로**). `IMapLoader`는 게임 스코프 등록. 클·서. 동작 보존.
  - **1b 서버 룰 추출**: 서버 `LOPGame`의 룰(#6)을 별도 서버 룰 컴포넌트로. 서버만. 동작 보존.
  - → 이 시점 `LOPGame`은 클·서 *동일하게 얇은* wrapper(생명주기 위임 + 물리설정 + 핸들러등록 + gameState).
- **2단계 — Game→Runner 흡수** (인터페이스 재설계, repo 코디네이트):
  - **2a 인터페이스 병합**: GameFramework에서 `IGame`을 `IRunner`에 병합(또는 IRunner 확장), `IRoom`/`IGameFactory`/`IGamePresenter` 경계 타입 교체. RunnerBase에 흡수 멤버 구현.
  - **2b 구현 이전 + Game 삭제**: 물리설정·핸들러등록·gameState를 LOPRunner로, 구독자 재배선, `LOPGame` 삭제, GameLifetimeScope/LOPRoom/LOPGameFactory/LOPGamePresenter 갱신. 클·서.

## 동작 보존 / 검증

- **동작 보존:** 각 단계가 *기능 이동*이지 변경이 아님. 1단계 후 게임 시작/플레이/종료·맵 로드·적 스폰·경험치·게임오버 모두 이전과 동일. 2단계 후 동일 + Game 객체만 사라짐.
- **컴파일:** 단계마다 3 repo 코디네이트 후 양쪽 인스턴스 `refresh_unity`(scope=all,force) → `read_console` 에러 0.
- **런타임:** 입장→로딩뷰(Initialized)→플레이(Playing)→게임오버→로비(클)/Closed(서), 이동/점프/물리/데미지/죽음, (서버) 적 스폰·경험치·게임오버 타이머 *이전과 동일*.
- **씬 직렬화:** `LOPGame` GameObject 삭제 시 씬(LOPGame.unity)·프리팹의 직렬화 참조(LOPGamePresenter→game 등) 재배선 — 머지 후 `git diff`로 의도된 참조만 변경 확인.

## 산업 표준 매핑

- **2층 `Runner → World`** = Quantum `Runner→Game` / Quake `engine→qvm` / DOTS `loop→World`. 호스트가 시뮬 보유·구동.
- **게임 룰 = 시뮬 시스템**(스폰/점수/승패), 호스트/엔진 아님 = Quantum `SpawnSystem`/`ScoreSystem`, Quake 서버 game 모듈, ECS systems. → 룰을 Runner에 안 넣고 World行으로 예약하는 근거.
- **맵 로딩 = host 셋업**(`game.Init`→Phase2 `Runner.Init`), 시뮬(World)과 분리 = World 엔진-프리 유지(씬 I/O는 use-side 어댑터 `IMapLoader`).
- **gameState = 매치 라이프사이클(호스트)** = Quantum Runner가 sim 라이프사이클 상태 소유.

## Out of Scope (다음)

- **게임 룰을 World 시스템으로 진짜 이주** — ③는 *Runner로 잘못 가지 않게 임시 분리*만. World 시스템화(서버권위·RNG·엔티티생성 sim-호환)는 별도 슬라이스(4c 룰 / Stage④).
- **맵 선택 다양화** — mapId 기반 동적 맵. 현재 단일 맵 이동만.
- **Snapshot/Restore·예측·롤백** — Stage④.
- **`IGameFactory`/씬·스코프 명명 재정비** — "LOPGame" 씬/스코프 명을 유지(게임플레이 씬). 더 깊은 리네임은 범위 밖.

## Open Questions (plan에서 해소)

- **`IGame` 병합 형태**: `IRunner`에 멤버 추가 vs 별도 `IMatchLifecycle` 같은 인터페이스로 분리 후 Runner가 구현. `Run` 시그니처 충돌(`IGame.Run(tick,interval,elapsed)` vs RunnerBase 내부 `Run()`) 해소.
- **서버 룰 컴포넌트 형태/명명**: 단일 `IGameRule` 집합 vs 룰별 타입. 구동(결정론 순서) 메커니즘 — 목적지 World 시스템과 정합되게(이벤트 콜백 회피). 명명은 업계어(Quantum `*System`) 확인.
- **`IGameFactory` 명명**: `IRunnerFactory`로 리네임 vs 유지(게임 세션 팩토리). 반환 타입은 `Task<IRunner>`로 교체 확정.
- **`IMapLoader` 위치/형태**: GameFramework vs use-side. mapId 인자화 시점.
- **`LOPGamePresenter`**: 제네릭 인자 `LOPRunner`로 vs `IRunner` 의존으로 단순화.
- **gameState 소유 위치 재확인**: Runner가 적합(매치 상태) — Room으로 끌어올릴 이유 없음 확인.
- **물리 설정 위치**: `RunnerBase`(공통) vs `LOPRunner`(사이드별) — 양쪽 동일 설정이면 RunnerBase.

## 진행

- [x] 브레인스토밍 합의 (2층화 = Game→Runner 흡수, 맵→`IMapLoader` 추출[위치 `game.Init` 유지 — Factory 이동은 타이밍/구조 변경이라 제외], 룰→임시 서버 컴포넌트[목적지 World], phasing 1비우기/2합치기)
- [x] **1단계(비우기) 구현 완료**(2026-06-25): 맵 로딩 `IMapLoader` 추출(클·서, 위치·병렬 보존) + 서버 룰 `GameRuleSystem` 추출. 4 repo 커밋(GF/Shared/Client/Server). 플레이 검증 OK. 다음 = 2단계(Game→Runner 흡수).
- [x] 게임 룰 위치 업계 표준 검증 (룰=시뮬 시스템, 호스트 아님 — Quantum/Quake/ECS)
- [x] 이 spec 작성
- [x] spec self-review (깨진 링크 수정, Run 시그니처 충돌·gameState 소유·물리설정 위치를 Open Question에 명시)
- [ ] 사용자 spec 리뷰
- [x] 구현 plan 작성 (`docs/superpowers/plans/2026-06-25-game-into-runner.md`)
