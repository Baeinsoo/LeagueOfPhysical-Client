# `GameEngine` → `Runner` 리네임 — Slice 4 구조 정리 ①

**Date:** 2026-06-23
**Branch (제안):** `feature/runner-rename` (클·서·GameFramework 각 repo 동명 브랜치)
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (Engine↔Simulation 컴포지션) · [LOP 저장소 토폴로지](../../lop-repo-topology.md) (`LOPGameEngine` 명명 Open Decision) · 프로젝트 메모리 `world-core-runner-world-naming`

## Goal

외각 호스트(틱 구동기 + I/O)를 가리키는 `Engine` 계열 이름을 **`Runner` 계열로 리네임**한다. **순수 기계적·동작 100% 보존** 리팩터 — 타입/멤버 이름만 바뀌고 로직·구조·실행 흐름은 1바이트도 안 바뀐다. 이 슬라이스의 가치는 *이름 교정 그 자체* 다.

- `IGameEngine` → `IRunner`
- `GameEngineBase` → `RunnerBase`
- `GameEngine`(정적 facade) → `Runner`
- `GameEngineListenAttribute` / `[GameEngineListen]` → `RunnerListenAttribute` / `[RunnerListen]`
- `LOPGameEngine` → `LOPRunner` (클·서 각각)
- `IGameEngine.UpdateEngine()` → `IRunner.UpdateRunner()`
- `IGame.gameEngine` 속성 → `IGame.runner`
- 네임스페이스 `LOP.Event.LOPGameEngine.Update` → `LOP.Event.LOPRunner.Update`

## 배경 / 동기

리서치(2026-06-23, Quantum/DOTS/Unreal/Quake·Source 다중 조사)로 확정: **"Engine" = 재사용 *플랫폼*** (Unity/Unreal 자체, 프로세스 수명 싱글톤)을 뜻하는 업계어다. 우리 `LOPGameEngine`은 *per-match·사이드별*(클·서 다른 본문)으로 생성되는 외각 호스트라 "엔진"이 아니다. 이 역할의 표준어는 **`Runner`**(Photon `QuantumRunner` / Fusion `NetworkRunner` — "I/O 소유 + 틱 구동하는 per-session 객체"). `lop-repo-topology.md`에 *Open Decision*으로 박혀 있던 그 rename이다.

**왜 지금:** Slice 4가 새 추상(`World` sim)을 host *밑에* 추가하려는 참이다. 새 코드를 *틀린 이름의 host* 짝으로 올리면 나중에 또 rename churn이 난다(아키텍처 가이드라인 "임의 명명 → 표준 되돌리기 churn 금지"). 그래서 sim 추가(②) 전에 host 이름부터 옳게 박는다.

**"Game" 접두어 안 붙이는 이유:** 업계 Runner는 `<제품/도메인>Runner`(QuantumRunner/NetworkRunner)이고 "GameRunner" 표준은 없다. 맨이름 `Runner`/`IRunner`/`LOPRunner`가 ① 업계 순수 개념어와 ② 짝이 될 sim `World`/`LOPWorld`와의 대칭에 맞다. (사용자 확정 — "Game 빼고 담백하게".)

## 현재 상태 (실측)

### GameFramework (`baegames.GameFramework.Runtime`, `Runtime/Scripts/Game/`)
| 파일 | 내용 | 비고 |
|---|---|---|
| `IGameEngine.cs` | `interface IGameEngine` — `entityManager`/`tickUpdater`/`networkTime`, `Run`/`Stop`/`UpdateEngine`, `AddListener`/`RemoveListener`/`DispatchEvent` | |
| `GameEngineBase.cs` | `abstract class GameEngineBase : MonoBehaviour, IGameEngine` — `OnTick → UpdateEngine()`, `GameEngine.current` 설정/해제, 리스너 맵 | **MonoBehaviour** (씬 컴포넌트) |
| `GameEngine.cs` | `static class GameEngine` — `current`, nested `GameEngine.Time`(tick/interval/…), `GameEngine.NetworkTime`(serverNow/predictedTime/rtt/remoteBackTime) | 정적 facade, 광범위 사용 |
| `GameEngineListenAttribute.cs` | `class GameEngineListenAttribute : Attribute` | `[GameEngineListen(typeof(X))]`로 사용 |
| `IGame.cs` | `interface IGame` — `IGameEngine gameEngine { get; }` 속성 보유 | gameEngine 속성만 영향 |

> asmdef 이름(`baegames.GameFramework.Runtime`)은 **안 바뀜** — 타입만 리네임.

### 클라 (Assembly-CSharp) — `IGameEngine|GameEngineBase|GameEngineListen|LOPGameEngine|UpdateEngine|GameEngine.|LOP.Event.LOPGameEngine` = **62곳 / 16파일**
- `Game/LOPGameEngine.cs` — `class LOPGameEngine : GameEngineBase`, `UpdateEngine()` override, `CreateNetworkTime()`. **파일 rename 대상.**
- `Game/Event.LOPGameEngine.Update.cs` — `namespace LOP.Event.LOPGameEngine.Update`(Begin/ProcessInput/Before·AfterEntityUpdate/Before·AfterPhysicsSimulation/End). **파일+네임스페이스 rename 대상.**
- `Game/GameLifetimeScope.cs` — `[SerializeField] private LOPGameEngine gameEngine;` + `RegisterComponent(gameEngine).As<IGameEngine>()`. ⚠️ **직렬화 필드(아래 gotcha)**
- `Game/LOPGame.cs` — `[Inject] IGameEngine gameEngine` 속성 + `gameEngine.InitializeAsync/Run/Stop/DeinitializeAsync` 호출
- `Game/PlayerInputManager.cs` — `[GameEngineListen(typeof(ProcessInput))]`, `GameEngine.Time.tick`, `using LOP.Event.LOPGameEngine.Update`
- `Game/LOPTickUpdater.cs`, `Game/MessageHandler/Game.{Info,Entity,Input}.MessageHandler.cs`, `World/WorldMotionSync.cs`, `Component/Action.cs`, `Entity/{ServerStateReconciler,LOPEntityController,SnapInterpolator,SnapReconciler}.cs`, `UI/DebugHud/DebugHudViewModel.cs` — 대부분 `GameEngine.Time.*`/`GameEngine.NetworkTime.*` 읽기 + `[GameEngineListen]`/이벤트 ns `using`

### 서버 (LeagueOfPhysical-Server) — **평행 구조**
클라와 동일 패턴: 자체 `Game/LOPGameEngine.cs`(서버 본문), `GameEngine.Time`/`NetworkTime` 사용처, `[GameEngineListen]`, 이벤트 ns. 정확한 파일·곳 수는 plan 실행 시 UnityMCP grep로 확정. (서버 working-tree 픽스처 `Room.unity`/`ConfigureRoomComponent.cs`는 커밋 금지 — 메모리 참조.)

## 설계 — 리네임 맵

### 타입/멤버 (전 repo 공통)
| Before | After |
|---|---|
| `IGameEngine` | `IRunner` |
| `GameEngineBase` | `RunnerBase` |
| `GameEngine` (static facade) | `Runner` |
| `GameEngine.Time` / `GameEngine.NetworkTime` (nested) | `Runner.Time` / `Runner.NetworkTime` |
| `GameEngine.current` | `Runner.current` |
| `GameEngineListenAttribute` | `RunnerListenAttribute` |
| `[GameEngineListen(...)]` | `[RunnerListen(...)]` |
| `UpdateEngine()` (IRunner 메서드 + override) | `UpdateRunner()` |
| `IGame.gameEngine` (속성) | `IGame.runner` |
| `LOPGameEngine` (클·서 클래스) | `LOPRunner` |
| ns `LOP.Event.LOPGameEngine.Update` | `LOP.Event.LOPRunner.Update` |

### 파일 rename (`git mv` — `.cs` + `.cs.meta` 함께)
| repo | Before | After |
|---|---|---|
| GameFramework | `IGameEngine.cs` | `IRunner.cs` |
| GameFramework | `GameEngineBase.cs` | `RunnerBase.cs` |
| GameFramework | `GameEngine.cs` | `Runner.cs` |
| GameFramework | `GameEngineListenAttribute.cs` | `RunnerListenAttribute.cs` |
| 클·서 | `Game/LOPGameEngine.cs` | `Game/LOPRunner.cs` |
| 클(서 동일 시) | `Game/Event.LOPGameEngine.Update.cs` | `Game/Event.LOPRunner.Update.cs` |

> **`IGame.cs`는 파일 rename 없음** — `gameEngine` 속성만 `runner`로 수정.

### ⚠️ Unity 직렬화 gotcha (필수)
1. **MonoBehaviour 스크립트 GUID 보존** — `LOPGameEngine`(MonoBehaviour, `[DIMonoBehaviour]`)는 씬의 GameObject에 컴포넌트로 붙어 있다(GameLifetimeScope `RegisterComponent` + `GetComponent<ITickUpdater/IEntityManager>` 동거). **`.cs`와 `.cs.meta`를 함께 `git mv`** 하면 스크립트 에셋 GUID가 유지돼 씬의 컴포넌트 참조가 살아남는다. 클래스명은 파일명(`LOPRunner`)과 일치시킨다(Unity MonoBehaviour 규칙).
2. **`[SerializeField] gameEngine` 필드명 → `runner` (확정)** — `GameLifetimeScope`의 `[SerializeField] private LOPGameEngine gameEngine;`를 `[SerializeField, UnityEngine.Serialization.FormerlySerializedAs("gameEngine")] private LOPRunner runner;`로 바꾼다. 타입(`LOPRunner`)은 GUID 기반이라 자동 유지되고, **필드명을 `runner`로 바꾸므로** `FormerlySerializedAs("gameEngine")`로 기존 씬 직렬화 링크를 보존한다(이름 기반 직렬화).

### 코드 외 무변경
- 로직·실행 순서·DI 수명·이벤트 디스패치·틱 흐름 전부 동일. `Run`/`Stop`/`OnTick`/`InitializeAsync` 등 "Engine" 없는 이름은 무변경.
- 어셈블리(asmdef) 이름·참조 무변경.

## 동작 보존 / 검증
- **동작 보존:** 이름만 치환 → 컴파일 산출 의미 동일. 실행 시 이동/점프/물리/HP/데미지/UI/DebugHud(틱·RTT·reconciliation 표시)가 **교체 전과 육안 동일**.
- **컴파일 검증:** GameFramework rename이 클·서 둘 다 깨므로(공유 패키지) **세 곳을 한 작업 세션에 코디네이트**해 고친 뒤, 양쪽 Unity 인스턴스 `refresh_unity`(scope=all, force) → `read_console` **에러 0** 확인. (deleting-package-files-CS2001 메모리와 동류 — stale 재스캔 필요.)
- **씬 검증:** GamePlay/Room 씬 로드 시 `LOPRunner` 컴포넌트 참조·`GameLifetimeScope.gameEngine`(또는 `runner`) 직렬화 링크가 **null 아님** 확인.
- **유닛 테스트:** 신규 없음(동작 보존 rename). 클·서 게임코드는 Assembly-CSharp라 asmdef 테스트 불가(클라 테스트 인프라 제약 메모리).

## 산업 표준 매핑
- `Runner`(외각 호스트, per-session, I/O 소유 + 틱 구동) = Photon **`QuantumRunner`** / Fusion **`NetworkRunner`** 의 역할. "Engine"(플랫폼·프로세스 싱글톤, Unreal `UEngine`/id Tech/Source `engine.dll`)과 구분되는 표준어. → `world-core-connection-architecture.md`가 이미 인용한 Quantum `Runner` 대응.
- 맨이름(접두어 없음) = 업계 순수 개념어. 제품 접두어는 우리 맥락에서 concrete의 `LOP*`가 담당(`LOPRunner` = `QuantumRunner`의 우리판).

## Out of Scope (다음 슬라이스)
- **②sim 추출 (4b)** — `IWorld`/`WorldBase`/`LOPWorld` 신설 + `Runner`가 보유·ticked. **별도 spec.**
- **③`Game`→`Runner` 합치기 (2층화)** — 진행자층 흡수, Room↔Game 배선·`IGameFactory`/`IGameState` 재정리. **별도 spec(최대 수술, 마지막).** 이 슬라이스에서 `IGame`은 **그대로 둔다**(속성명 `gameEngine`→`runner`만).
- **`UpdateRunner()` 외 메서드 의미 변경** — 본 슬라이스는 이름만. 페이즈 흐름 재배치는 ②/③.

## 확정된 결정 (사용자 리뷰 2026-06-23)
- ✅ **`UpdateEngine()` → `UpdateRunner()`** (확정). `Update()`는 `RunnerBase : MonoBehaviour`라 Unity `Update` 콜백과 충돌 → 금지.
- ✅ **`GameLifetimeScope` 필드 `gameEngine` → `runner` + `[FormerlySerializedAs("gameEngine")]`** (확정, gotcha #2).

## Open Questions (plan에서 해소)
- 서버 정확한 파일·곳 목록(이벤트 ns 파일 존재 여부 포함) — plan 실행 시 서버 인스턴스 grep로 확정.
- 세 repo 리네임의 커밋 단위(repo별 3커밋) 및 머지 순서 — plan에서.

## 진행
- [x] 브레인스토밍 합의 (2층 Runner→World, Engine→Runner, 맨이름)
- [x] 이 spec 작성
- [x] spec self-review
- [x] 사용자 spec 리뷰 (UpdateRunner + 필드명 runner 확정)
- [x] `writing-plans`로 구현 plan 작성 (`docs/superpowers/plans/2026-06-23-runner-rename.md`)
