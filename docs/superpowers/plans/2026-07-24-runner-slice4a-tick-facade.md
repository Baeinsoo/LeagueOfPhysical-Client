# Runner Slice 4a — Time/tick facade → ITickUpdater 주입 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`).

**Goal:** static `Runner.Time.*` 읽기(깨끗한 호출부)를 주입된 `ITickUpdater`로 옮긴다. DebugHud·LOPTickUpdater·static `Runner` 삭제는 4b로 미룬다.

**Architecture:** `ITickUpdater`에 계산값 `deltaTime`를 추가하고(죽은 `tickTime`은 static `Runner.Time`에만 있으니 4b에서 클래스째 삭제), `Runner.Time.tick/interval/elapsedTime/deltaTime`을 읽던 호출부를 이미 주입받는 `IRunner.tickUpdater`(또는 신규 `ITickUpdater` ctor 주입)로 교체한다. static `Runner` 클래스와 `Runner.Time` 중첩 클래스는 **이 슬라이스에선 그대로 둔다**(DebugHud·LOPTickUpdater가 아직 씀) → 각 교체가 독립적이라 원자성 부담 없음.

**Tech Stack:** Unity, C#, VContainer, GameFramework `ITickUpdater`.

## Global Constraints

- **동작 변화 0**: 순수 배선 리팩터링. 틱당 로직·넷코드 타이밍 불변.
- **static `Runner`/`Runner.Time`/DebugHud/LOPTickUpdater 건드리지 않음** — 전부 4b.
- **커밋 스테이징 명시 파일만**. `git add -A`/`.` 금지. **서버 레포 미커밋 로컬 픽스처**(`DefaultVolumeProfile.asset`, `ConfigureRoomComponent.cs`, `GameRuleSystem.cs`) 절대 커밋 금지 — `git status --short`로 확인.
- **새 단위 테스트 없음**(배선/계산 프로퍼티) — 검증 = 양쪽 에디터 컴파일 + GF EditMode 무회귀. **클라 코드는 워크트리서 컴파일 불가**(에디터가 main 바인딩) → 정적 검증 + 머지 후 main 에디터 게이트.
- **`deltaTime` 의미 보존**: `Runner.Time.deltaTime`은 `tick == 0 ? 0 : interval` — 그대로.

## Repos & Branches (생성됨)

- GameFramework `C:\Users\re5na\workspace\LOP\GameFramework` — `refactor/runner-slice4a-tick-facade` (base 0719ec6)
- Server `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server` — `refactor/runner-slice4a-tick-facade` (base c334d8f)
- Client — 이 워크트리 `worktree-refactor-runner-slice4a-tick-facade`

Unity: 클라 `LeagueOfPhysical-Client@de70658b9450cbb4`, 서버 `LeagueOfPhysical-Server@f99391fa2dbaaf3c`.

## File Structure

| 파일 | 작업 |
|---|---|
| GF `Runtime/Scripts/Game/ITickUpdater.cs` | `double deltaTime { get; }` 추가 |
| GF `Runtime/Scripts/Game/TickUpdaterBase.cs` | `deltaTime` 구현 |
| Client `Assets/Scripts/Game/LOPRunner.cs` | `Runner.Time.tick`×4 → `tickUpdater.tick` |
| Client `Assets/Scripts/Game/PlayerInputManager.cs` | `Runner.Time.tick` → `runner.tickUpdater.tick` |
| Client `Assets/Scripts/Netcode/LocalEntityInterpolator.cs` | `Runner.Time.tick/tickInterval/elapsedTime` → `runner.tickUpdater.*` |
| Server `Assets/Scripts/Game/LOPRunner.cs` | `Runner.Time.tick`×5 → `tickUpdater.tick` |
| Server `Assets/Scripts/Game/MessageHandler/GameInfoMessageHandler.cs` | `Runner.Time.tick/tickInterval/elapsedTime` → `runner.tickUpdater.*` |
| Server `Assets/Scripts/Entity/LOPAIController.cs` | `Runner.Time.deltaTime` → `runner.tickUpdater.deltaTime` |
| Server `Assets/Scripts/Netcode/WorldEventSink.cs` | `Runner.Time.tick` → 주입 `ITickUpdater.tick` |
| Server `Assets/Scripts/AI/EnemyBrain.cs` | `Runner.Time.tick` → 주입 `ITickUpdater.tick` |
| Server `Assets/Scripts/Game/MessageHandler/GameInputMessageHandler.cs` | `Runner.Time.tick` → 주입 `ITickUpdater.tick` |

> `Runner.Time.tickInterval`은 facade에서 `tickUpdater.interval`을 반환 → 교체 시 `.interval`을 쓴다.

---

### Task 1: `ITickUpdater.deltaTime` 추가 (GF, 선행 — additive)

**Files:** GF `Runtime/Scripts/Game/ITickUpdater.cs`, `Runtime/Scripts/Game/TickUpdaterBase.cs`

- [ ] **Step 1: `ITickUpdater`에 프로퍼티 추가**

`ITickUpdater.cs`의 `long processibleTick { get; }` 다음 줄에 추가:
```csharp
        double deltaTime { get; }
```

- [ ] **Step 2: `TickUpdaterBase`에 구현 추가**

`TickUpdaterBase.cs`의 `processibleTick` getter 블록 다음에 추가:
```csharp
        // 이전 static Runner.Time.deltaTime과 동일: 첫 틱(0)엔 0, 이후엔 고정 간격.
        public double deltaTime => tick == 0 ? 0 : interval;
```

- [ ] **Step 3: GF 컴파일 확인 (additive이라 무해)**

`refresh_unity(mode="force", scope="scripts", compile="request", unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")` → `read_console(types=["error"])` 0.

- [ ] **Step 4: 커밋 (GF)**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git add Runtime/Scripts/Game/ITickUpdater.cs Runtime/Scripts/Game/TickUpdaterBase.cs
git commit -m "feat(runner): ITickUpdater.deltaTime (구 Runner.Time.deltaTime 흡수)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: 호출부 이전 (클·서, `Runner.Time.*` → tickUpdater)

**Interfaces (consumes):** `ITickUpdater { long tick; double interval; double elapsedTime; double deltaTime; }` (Task 1); `IRunner.tickUpdater` (기존).

두 교체 패턴:
- **패턴 A — 이미 `IRunner`/`tickUpdater` 보유**: `Runner.Time.tick` → `tickUpdater.tick`(자기 자신이 Runner인 `LOPRunner`) 또는 `runner.tickUpdater.tick`(IRunner 주입 클래스). `Runner.Time.tickInterval` → `.interval`, `Runner.Time.elapsedTime` → `.elapsedTime`, `Runner.Time.deltaTime` → `.deltaTime`.
- **패턴 B — 주입 없음(순수 C#)**: 생성자에 `ITickUpdater tickUpdater` 파라미터 추가 + `private readonly ITickUpdater tickUpdater;` 필드 + 대입, 그리고 `Runner.Time.tick` → `tickUpdater.tick`. (서버 스코프엔 `ITickUpdater`가 이미 등록됨 — `GameLifetimeScope.cs:62`.)

패턴 B 템플릿(예: `WorldEventSink`):
```csharp
// 필드
private readonly ITickUpdater tickUpdater;
// 생성자 파라미터 추가 + 대입
public WorldEventSink(ISessionManager sessionManager, ITickUpdater tickUpdater)
{
    this.sessionManager = sessionManager;
    this.tickUpdater = tickUpdater;
}
// 사용처
var batch = new WorldEventBatchToC { Tick = tickUpdater.tick };
```

- [ ] **Step 1: 패턴 A 파일 편집** (각 파일에서 `Runner.Time.<member>`를 위 규칙으로 치환)
  - Client `Game/LOPRunner.cs` — `Runner.Time.tick`(L98/108/214/219) → `tickUpdater.tick` (이미 `RunnerBase.tickUpdater` 상속).
  - Client `Game/PlayerInputManager.cs` — `Runner.Time.tick`(L56) → `runner.tickUpdater.tick` (`[Inject] IRunner runner` 보유).
  - Client `Netcode/LocalEntityInterpolator.cs` — L63 `Runner.Time.tick`→`runner.tickUpdater.tick`, `Runner.Time.tickInterval`→`runner.tickUpdater.interval`; L67 `Runner.Time.tickInterval`→`runner.tickUpdater.interval`; L78 `Runner.Time.elapsedTime`→`runner.tickUpdater.elapsedTime`, `Runner.Time.tickInterval`→`runner.tickUpdater.interval` (`[Inject] IRunner runner` 보유).
  - Server `Game/LOPRunner.cs` — `Runner.Time.tick`(L121/159/185/192/207) → `tickUpdater.tick`.
  - Server `Game/MessageHandler/GameInfoMessageHandler.cs` — L78 `Runner.Time.tick`→`runner.tickUpdater.tick`, L79 `Runner.Time.tickInterval`→`runner.tickUpdater.interval`, L80 `Runner.Time.elapsedTime`→`runner.tickUpdater.elapsedTime` (`IRunner runner` 주입 보유).
  - Server `Entity/LOPAIController.cs` — L47 `Runner.Time.deltaTime`→`runner.tickUpdater.deltaTime` (`[Inject] IRunner runner` 보유).

- [ ] **Step 2: 패턴 B 파일 편집** (생성자에 `ITickUpdater` 추가)
  - Server `Netcode/WorldEventSink.cs` — 위 템플릿대로. `Runner.Time.tick`(L22) → `tickUpdater.tick`.
  - Server `AI/EnemyBrain.cs` — ctor에 `ITickUpdater tickUpdater` 추가·필드·대입. `Runner.Time.tick`(L49) → `tickUpdater.tick`.
  - Server `Game/MessageHandler/GameInputMessageHandler.cs` — ctor에 `ITickUpdater tickUpdater` 추가·필드·대입. `Runner.Time.tick`(L47) → `tickUpdater.tick`.
  - 각 파일 최상단에 `using GameFramework;`가 없으면 추가(대개 이미 있음 — `ISessionManager` 등 GameFramework 타입 사용).

- [ ] **Step 3: 잔재 확인** — 이 3레포 대상 파일에 `Runner.Time.` 이 **DebugHud 2파일 외엔 0**이어야 함:
```bash
grep -rn "Runner.Time." "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/refactor-runner-slice4a-tick-facade/Assets/Scripts" "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts"
# 기대: DebugHudViewModel.cs(클·서)만 남음 (4b에서 처리)
```

- [ ] **Step 4: 컴파일 검증 (양쪽 에디터)**
  - 서버: `refresh_unity(mode="force", scope="scripts", compile="request", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")` → `read_console(types=["error"])` 0.
  - 클라: `refresh_unity(...client...)` → `read_console(...client...)` 0. (⚠️ 클라 에디터는 main 체크아웃 → 워크트리 클라 편집은 **여기서 컴파일 안 됨**. 서버 컴파일 그린 + 정적 grep으로 대신 확인하고, 실제 클라 컴파일은 머지 후 게이트.)

- [ ] **Step 5: GF EditMode 무회귀**
`run_tests(mode="EditMode", assembly_names=["baegames.GameFramework.Runtime.Tests"], unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")` → 전부 PASS(`get_test_job` 폴링).

- [ ] **Step 6: 커밋 (Client 워크트리)** — 명시 파일만
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/refactor-runner-slice4a-tick-facade"
git add Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Game/PlayerInputManager.cs Assets/Scripts/Netcode/LocalEntityInterpolator.cs
git commit -m "refactor(runner): 클라 Runner.Time.* → 주입 tickUpdater (DebugHud 제외=4b)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 7: 커밋 (Server)** — ⚠️ 픽스처 제외, 명시 파일만
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Game/MessageHandler/GameInfoMessageHandler.cs \
        Assets/Scripts/Entity/LOPAIController.cs Assets/Scripts/Netcode/WorldEventSink.cs \
        Assets/Scripts/AI/EnemyBrain.cs Assets/Scripts/Game/MessageHandler/GameInputMessageHandler.cs
git status --short   # LOPGameState 픽스처 3개는 여전히 ' M'(unstaged)이어야 함
git commit -m "refactor(runner): 서버 Runner.Time.* → 주입 tickUpdater (DebugHud 제외=4b)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

## 완료 후

- 최종 whole-branch 리뷰(3-레포 결합 diff).
- `finishing-a-development-branch` 로컬 `--no-ff` 머지 → **머지 후 main 클라 에디터 리프레시(scope=all force 필요 시)로 실제 클라 컴파일 확인**. 서버 픽스처 미커밋 보존 확인.

## 스코프 밖 (4b)

DebugHud(클·서) 전체 이전, `LOPTickUpdater` NetworkTime 주입(LeadState 잠복 유지), `Runner.current` is-running 가드 → `gameState`, `CreateNetworkTime`·`RunnerBase.networkTime`·static `Runner`(Time 중첩 포함) 통째 삭제, 죽은 `tickTime` 제거.
