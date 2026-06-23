# `GameEngine` → `Runner` 리네임 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 외각 호스트의 `Engine` 계열 이름을 `Runner` 계열로 리네임한다. **순수 기계적·동작 100% 보존.** 로직·구조·실행 흐름 무변경.

**Spec:** `docs/superpowers/specs/2026-06-23-runner-rename-design.md`

**Architecture:** 3-repo 코디네이티드 리네임. GameFramework(공유 패키지)의 타입 rename이 클·서 둘 다 깨므로, **세 repo를 모두 고친 뒤에 컴파일 검증**한다(중간 컴파일 깨짐은 정상).

**Tech Stack:** Unity / C# / VContainer DI / UnityMCP(클·서 양쪽 인스턴스 컴파일 검증).

---

## 레포 / 도구 참조

- **GameFramework:** `C:\Users\re5na\workspace\LOP\GameFramework` (패키지 `com.baegames.gameframework`, file: 참조)
- **Client:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client`
- **Server:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server`
- **UnityMCP 인스턴스:** `mcpforunity://instances`에서 name=`LeagueOfPhysical-Client` / `LeagueOfPhysical-Server`의 `id`(해시 변동 가능). 모든 UnityMCP 호출에 `unity_instance` 명시.

**원자성(중요):** GameFramework 타입 rename → 클·서 동시 깨짐. **Task 1~3을 한 작업 세션에 모두 적용한 뒤 Task 4에서 컴파일 검증.** 중간 단계 컴파일 시도 금지.

**리네임 레시피 (토큰 치환 — 케이스 민감, 이 순서로):**
1. `GameEngine` → `Runner` — 이게 대부분을 한 방에 처리: `IGameEngine`→`IRunner`, `GameEngineBase`→`RunnerBase`, `GameEngineListenAttribute`→`RunnerListenAttribute`, `[GameEngineListen]`→`[RunnerListen]`, `LOPGameEngine`→`LOPRunner`, `GameEngine.Time/NetworkTime/current`→`Runner.*`, ns `LOP.Event.LOPGameEngine.Update`→`LOP.Event.LOPRunner.Update`. (모두 "GameEngine" 어간을 포함하므로 1회 치환으로 정확히 변환됨.)
2. `UpdateEngine` → `UpdateRunner` — "GameEngine" 어간이 없어 1번이 안 건드림. 별도 치환.
3. `gameEngine` → `runner` — 소문자 g(필드·속성·지역변수·`[Inject]`·`RegisterComponent(gameEngine)` 등). 1번이 안 건드림(케이스 다름).

> **⚠️ 치환 예외 — `FormerlySerializedAs("gameEngine")`**: GameLifetimeScope 필드에 붙일 이 문자열 리터럴의 **"gameEngine"은 옛 이름이라 *그대로 둬야* 한다** → 3번 치환 *후* 수기로 attribute를 추가한다(치환 대상 아님). 자세히는 Task 2 Step 3.

> **권장 실행:** 파일별 `replace_all`(검토 가능) 또는 sed. 치환 후 **반드시 `git diff`로 의도치 않은 매치 검토**(주석/로그 내 "GameEngine"이 "Runner"로 바뀐 건 무방).

**픽스처 보존:** `git add -A`/`commit -am` 금지. Task에 명시된 파일만 `git add`. 서버 픽스처(`Assets/Scenes/Room.unity`, `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`)는 커밋 금지(메모리 참조).

**.meta:** 파일 rename 시 `.cs`와 `.cs.meta`를 **함께 `git mv`**(GUID 보존 — 씬 컴포넌트 참조 유지).

**컨벤션:** LOP 측 파일은 `using GameFramework.World;` 추가 금지, World 타입은 풀 네임스페이스 한정(이번 슬라이스는 World 타입 미추가).

---

## Task 0: 세 repo 피처 브랜치 셋업

- [ ] **Step 1: 세 repo working tree 확인 (픽스처 외 깨끗)**

Run:
```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework status --short
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client status --short
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server status --short
```
Expected: 클라는 기존 픽스처(`Assets/Art`, `UIRoot.prefab`, `Room.unity`, `PackageManagerSettings.asset`, `URPProjectSettings.asset`)만, 서버는 픽스처(`Room.unity`, `ConfigureRoomComponent.cs`)만. 이번에 바꿀 `*Engine*` 파일은 HEAD-clean. 다른 변경 있으면 멈추고 보고.

- [ ] **Step 2: 세 repo 피처 브랜치 생성**

```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework checkout -b feature/runner-rename
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client checkout -b feature/runner-rename
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server checkout -b feature/runner-rename
```

---

## Task 1: GameFramework 리네임

**Files (`Runtime/Scripts/Game/`):** `IGameEngine.cs`, `GameEngineBase.cs`, `GameEngine.cs`, `GameEngineListenAttribute.cs`, `IGame.cs`

- [ ] **Step 1: 파일 rename (`git mv`, `.meta` 포함)**

```bash
cd C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Game
git mv IGameEngine.cs IRunner.cs;                       git mv IGameEngine.cs.meta IRunner.cs.meta
git mv GameEngineBase.cs RunnerBase.cs;                 git mv GameEngineBase.cs.meta RunnerBase.cs.meta
git mv GameEngine.cs Runner.cs;                         git mv GameEngine.cs.meta Runner.cs.meta
git mv GameEngineListenAttribute.cs RunnerListenAttribute.cs; git mv GameEngineListenAttribute.cs.meta RunnerListenAttribute.cs.meta
```
(`IGame.cs`는 rename 없음.)

- [ ] **Step 2: 토큰 치환 — GameFramework 전체**

`Runtime/Scripts/` 하위 `.cs`에 레시피 1·2·3 적용(케이스 민감). 영향 파일: 위 rename된 4파일 + `IGame.cs`(`IGameEngine gameEngine`→`IRunner runner`). 그 외 GameFramework 내 `GameEngine`/`IGameEngine`/`UpdateEngine`/`gameEngine` 참조도 함께(예상: 거의 없음 — grep로 확인).

검증용 grep(치환 후 **0건**이어야): `IGameEngine|GameEngineBase|GameEngineListen|UpdateEngine\b|GameEngine\.|\bgameEngine\b` (단 `Runner.cs` 내부 `Runner.current`/`Runner.Time` 등은 정상).

- [ ] **Step 3: 클래스명·파일명 일치 확인**

`IRunner.cs`=`interface IRunner`, `RunnerBase.cs`=`class RunnerBase : MonoBehaviour, IRunner`, `Runner.cs`=`static class Runner`(nested `Runner.Time`/`Runner.NetworkTime`), `RunnerListenAttribute.cs`=`class RunnerListenAttribute`. `RunnerBase.UpdateRunner()` abstract, `OnTick → UpdateRunner()`.

> 컴파일 검증은 Task 4에서(지금 하면 클·서가 깨진 상태).

---

## Task 2: Client 리네임

**Files:** `Game/LOPGameEngine.cs`, `Game/Event.LOPGameEngine.Update.cs`(rename) + `GameEngine`/`UpdateEngine`/`gameEngine` 참조 16파일(spec "현재 상태" 목록).

- [ ] **Step 1: 파일 rename (`git mv`, `.meta` 포함)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game
git mv LOPGameEngine.cs LOPRunner.cs;                       git mv LOPGameEngine.cs.meta LOPRunner.cs.meta
git mv Event.LOPGameEngine.Update.cs Event.LOPRunner.Update.cs; git mv Event.LOPGameEngine.Update.cs.meta Event.LOPRunner.Update.cs.meta
```

- [ ] **Step 2: 토큰 치환 — `Assets/Scripts/` 전체**

레시피 1·2·3 적용(케이스 민감, 파일별 `replace_all` 또는 sed). 대상(spec 실측 16파일): `Game/LOPRunner.cs`, `Game/Event.LOPRunner.Update.cs`, `Game/GameLifetimeScope.cs`, `Game/LOPGame.cs`, `Game/PlayerInputManager.cs`, `Game/LOPTickUpdater.cs`, `Game/MessageHandler/Game.{Info,Entity,Input}.MessageHandler.cs`, `World/WorldMotionSync.cs`, `Component/Action.cs`, `Entity/{ServerStateReconciler,LOPEntityController,SnapInterpolator,SnapReconciler}.cs`, `UI/DebugHud/DebugHudViewModel.cs`.

치환 후 grep `IGameEngine|GameEngineBase|GameEngineListen|UpdateEngine\b|LOPGameEngine|GameEngine\.|LOP\.Event\.LOPGameEngine` → **0건**.

- [ ] **Step 3: `GameLifetimeScope` 직렬화 필드 — `FormerlySerializedAs` 수기 추가**

Step 2의 3번 치환으로 필드가 `[SerializeField] private LOPRunner runner;`가 됐을 것. 여기에 옛 이름 보존 attribute를 추가(문자열 리터럴 "gameEngine"은 옛 이름 그대로):
```csharp
using UnityEngine.Serialization;   // 상단 using 추가(없으면)
// ...
[SerializeField, FormerlySerializedAs("gameEngine")] private LOPRunner runner;
```
그리고 `RegisterComponent(runner).As<IRunner>()` 확인(3번+1번 치환 결과).

- [ ] **Step 4: 클래스명·파일명 일치**

`LOPRunner.cs`=`class LOPRunner : RunnerBase`(`UpdateRunner()` override, `CreateNetworkTime()`), `Event.LOPRunner.Update.cs`=`namespace LOP.Event.LOPRunner.Update`. `LOPGame.cs`의 `runner` 속성 + `runner.InitializeAsync/Run/Stop/DeinitializeAsync`.

---

## Task 3: Server 리네임 (평행)

- [ ] **Step 1: 서버 영향 파일 발견**

서버 repo에서 grep: `IGameEngine|GameEngineBase|GameEngineListen|LOPGameEngine|UpdateEngine|GameEngine\.|LOP\.Event\.LOPGameEngine`. 클라와 평행한 파일 목록 확보(`Game/LOPGameEngine.cs` + 이벤트 ns 파일 존재 여부 + 참조처).

- [ ] **Step 2: 파일 rename (`git mv` + `.meta`)**

`Game/LOPGameEngine.cs`→`Game/LOPRunner.cs`(+meta). 서버에 이벤트 ns 파일(`Event.LOPGameEngine.Update.cs`)이 있으면 동일하게 rename. (서버는 `GameLifetimeScope` 대신 자체 스코프일 수 있음 — Step 1 grep 결과 따름.)

- [ ] **Step 3: 토큰 치환 — 서버 `Assets/Scripts/` 전체**

레시피 1·2·3 적용. 서버에도 `[SerializeField]`로 LOPGameEngine을 참조하는 스코프가 있으면 Task 2 Step 3과 동일하게 `FormerlySerializedAs("gameEngine")` 처리. 치환 후 grep 0건 확인.

- [ ] **Step 4: 클래스명·파일명 일치** (Task 2 Step 4 서버판)

---

## Task 4: 컴파일 + 씬 검증 (양쪽 인스턴스)

- [ ] **Step 1: 재스캔 + 컴파일 (클·서)**

각 인스턴스에 UnityMCP `refresh_unity`(`scope=all`, `mode=force`) → `read_console`(`unity_instance` 명시) **에러 0** 확인. (공유 패키지 rename이라 stale 재스캔 필수 — deleting-package-files-CS2001 동류.) `execute_code` 금지.

- [ ] **Step 2: 씬 직렬화 링크 확인**

클라 GamePlay/Room 씬 로드 → `GameLifetimeScope`의 `runner` 필드가 **null 아님**(FormerlySerializedAs로 옛 링크 보존) + `LOPRunner` 컴포넌트 GameObject 정상. 서버도 동등 확인.

---

## Task 5: 커밋 (repo별)

- [ ] **Step 1: GameFramework 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/GameFramework
git add Runtime/Scripts/Game/IRunner.cs Runtime/Scripts/Game/IRunner.cs.meta Runtime/Scripts/Game/RunnerBase.cs Runtime/Scripts/Game/RunnerBase.cs.meta Runtime/Scripts/Game/Runner.cs Runtime/Scripts/Game/Runner.cs.meta Runtime/Scripts/Game/RunnerListenAttribute.cs Runtime/Scripts/Game/RunnerListenAttribute.cs.meta Runtime/Scripts/Game/IGame.cs
git commit -m "refactor(game): rename GameEngine -> Runner (host is per-match Runner, not platform Engine)

IGameEngine->IRunner, GameEngineBase->RunnerBase, GameEngine facade->Runner,
GameEngineListenAttribute->RunnerListenAttribute, UpdateEngine->UpdateRunner,
IGame.gameEngine->runner. Mechanical, behavior-preserving. 'Engine' is an
industry misnomer for a per-match/per-side host; standard term is Runner
(Quantum/Fusion). Prep for Slice 4 sim extraction (Runner -> World).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

- [ ] **Step 2: Client 커밋** — `git add` 한 파일만(rename된 2파일 +meta + 치환된 14파일). 픽스처 제외.
```bash
git commit -m "refactor(game): rename GameEngine -> Runner (LOPGameEngine -> LOPRunner)

Mirror GameFramework rename; LOPGameEngine->LOPRunner, event ns
LOP.Event.LOPGameEngine.Update->LOP.Event.LOPRunner.Update,
GameLifetimeScope field gameEngine->runner (+FormerlySerializedAs).
Mechanical, behavior-preserving.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

- [ ] **Step 3: Server 커밋** — Task 3 변경 파일만. 픽스처(`Room.unity`/`ConfigureRoomComponent.cs`) 제외. 메시지는 Client판과 동형.

---

## Task 6: 종합 검증 + 머지 (사용자)

- [ ] **Step 1: 최종 컴파일** — 양쪽 인스턴스 `read_console` 에러 0.
- [ ] **Step 2: 플레이 검증 (사용자)** — 클·서 플레이, **이전과 동일**: 이동/점프/물리, HP·데미지·HP바, DebugHud(틱/RTT/reconciliation), 죽음→디스폰+경험치 구슬. 성공 기준 = 동작 동일(이름만 변경).
- [ ] **Step 3: 사용자 OK 시 머지/푸시** — GameFramework·Client·Server 각 `feature/runner-rename`를 main에 `--no-ff` 머지 + push. **머지·push는 사용자 요청 시에만.** (file: 패키지라 GameFramework 먼저 머지 → 클·서 순.)

---

## Self-Review (작성자 체크)
- **Spec 커버리지:** 타입/파일/멤버 rename 맵 전부 = Task1(GF)+Task2(클)+Task3(서) / 직렬화 gotcha(.meta git mv + FormerlySerializedAs) = Task2 Step3 + Task4 Step2 / 컴파일·씬 검증 = Task4 / 동작보존 플레이 = Task6. ✅
- **치환 안전:** 레시피 1번(`GameEngine`→`Runner`)이 어간 포함 토큰 전부 정확 변환 + 2·3번이 케이스 차로 분리 + FormerlySerializedAs 리터럴은 치환 후 수기 추가(예외 명시). git diff 검토 단계 포함. ✅
- **원자성:** GF rename이 소비자 깨므로 Task1~3 후 Task4 검증(중간 컴파일 금지 명시). ✅
- **동작 보존:** 로직·흐름·DI·이벤트 무변경, 이름만. 신규 테스트 없음(rename 슬라이스). ✅
- **플레이스홀더:** 서버 파일 목록만 실행 시 grep 확정(repo 접근 필요) — 그 외 정확한 토큰/경로 명시. ✅
