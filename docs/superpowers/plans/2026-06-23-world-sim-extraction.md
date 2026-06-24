# 시뮬 코어 추출 `Runner → World` 빈 골격 Implementation Plan (Slice 4 ②, =4b)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 결정론 시뮬 코어를 빈 골격으로 신설(`IWorld`+`WorldBase`+`LOPWorld`), `LOPRunner`가 보유·매틱 `world.Tick(tick,dt)` 호출(ticked). **페이즈 훅 no-op, 게임 로직 0줄 이동, 동작 100% 보존.**

**Spec:** `docs/superpowers/specs/2026-06-23-world-sim-extraction-design.md`

**Architecture:** 4-repo 추가형(additive) 변경. 새 타입은 기존 컴파일을 깨지 않음(신규 추가). LOP-Shared asmdef에 `GameFramework.World` 참조 추가 + 클·서가 새 타입을 DI·주입·호출.

**Tech Stack:** Unity / C# / VContainer DI / UnityMCP(클·서 컴파일 검증).

---

## 레포 / 도구 참조
- **GameFramework:** `C:\Users\re5na\workspace\LOP\GameFramework`
- **LOP-Shared:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Shared`
- **Client:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client`
- **Server:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server`
- **UnityMCP:** `mcpforunity://instances`에서 Client/Server id(해시 변동). 모든 호출에 `unity_instance` 명시. (현재 Client `@de70658b9450cbb4` / Server `@f99391fa2dbaaf3c`.)

**⚠️ EOL 교훈(직전 슬라이스):** 이 git-bash에서 `find -exec sed -i`가 모든 .cs를 CRLF→LF로 바꿈. **이번엔 mass sed 금지** — 신규 파일은 Write, 기존 파일 편집은 Edit(정밀, 주변 EOL 보존). 신규 .cs는 worktree에 LF로 생기지만 git이 LF 저장이라 무해(커밋 깨끗, Unity가 저장 시 CRLF). repo는 `core.autocrlf=true`·LF 저장.

**.meta:** 신규 `.cs`는 Unity가 `refresh_unity` 시 `.meta`를 생성한다. **`.cs`와 생성된 `.cs.meta`를 함께 커밋.** .meta 수기 생성 금지.

**픽스처 보존:** `git add -A`/`commit -am` 금지. Task에 명시된 파일만 add. 서버 픽스처(`Room.unity`, `ConfigureRoomComponent.cs`) 커밋 금지.

**컨벤션:** LOP 측 파일은 `using GameFramework.World;` 금지, World 타입 풀 네임스페이스 한정.

---

## Task 0: 4개 repo 피처 브랜치
- [ ] **Step 1: 4 repo working tree 확인** (픽스처 외 깨끗; main 최신)
```bash
for r in GameFramework LeagueOfPhysical-Shared LeagueOfPhysical-Client LeagueOfPhysical-Server; do echo "== $r =="; git -C C:/Users/re5na/workspace/LOP/$r status --short; git -C C:/Users/re5na/workspace/LOP/$r branch --show-current; done
```
Expected: Client 픽스처(Art/UIRoot.prefab/Room.unity/ProjectSettings)·Server 픽스처(Room.unity/ConfigureRoomComponent.cs/Art)만, 모두 `main`. 다른 변경 있으면 멈추고 보고.
- [ ] **Step 2: 브랜치 생성**
```bash
for r in GameFramework LeagueOfPhysical-Shared LeagueOfPhysical-Client LeagueOfPhysical-Server; do git -C C:/Users/re5na/workspace/LOP/$r checkout -b feature/world-extraction; done
```

---

## Task 1: GameFramework — `IWorld` + `WorldBase`

- [ ] **Step 1: `Runtime/Scripts/World/IWorld.cs` 생성**
```csharp
namespace GameFramework.World
{
    public interface IWorld
    {
        EntityRegistry EntityRegistry { get; }
        WorldEventBuffer EventBuffer { get; }
        void Tick(long tick, float deltaTime);
    }
}
```

- [ ] **Step 2: `Runtime/Scripts/World/WorldBase.cs` 생성**
```csharp
namespace GameFramework.World
{
    public abstract class WorldBase : IWorld
    {
        public EntityRegistry EntityRegistry { get; }
        public WorldEventBuffer EventBuffer { get; }

        protected WorldBase(EntityRegistry entityRegistry, WorldEventBuffer eventBuffer)
        {
            EntityRegistry = entityRegistry;
            EventBuffer = eventBuffer;
        }

        public void Tick(long tick, float deltaTime)
        {
            Collection(tick, deltaTime);
            Mutation(tick, deltaTime);
            Detection(tick, deltaTime);
        }

        protected virtual void Collection(long tick, float deltaTime) { }
        protected virtual void Mutation(long tick, float deltaTime) { }
        protected virtual void Detection(long tick, float deltaTime) { }
    }
}
```
> `EntityRegistry`/`WorldEventBuffer`는 같은 asmdef(`GameFramework.World`)이라 추가 참조 불필요. 엔진-프리 유지(UnityEngine 미사용).

- [ ] **Step 3: (선택) EditMode 골격 테스트** — GameFramework에 Tests.EditMode asmdef이 있으면, `WorldBase.Tick`이 `Collection→Mutation→Detection`을 각 1회·이 순서로 호출함을 검증하는 테스트 추가(호출 순서를 기록하는 fake 파생). asmdef 없으면 **스킵**(동작-보존 골격이라 필수 아님). 추가 시 .cs+.meta 함께 커밋.

- [ ] **Step 4: 컴파일 검증** — `refresh_unity`(scope=all, force) 후 `read_console` 에러 0 (양쪽 인스턴스 — GameFramework는 클·서 둘 다 소비). 신규 .meta 생성 확인.

---

## Task 2: LOP-Shared — asmdef 참조 + `LOPWorld`

- [ ] **Step 1: `Runtime/baegames.LOP.Shared.Runtime.asmdef` 참조 추가**

`references`에 `"baegames.GameFramework.World"` 추가:
```jsonc
"references": ["baegames.GameFramework.Runtime", "baegames.GameFramework.World"],
```
(나머지 필드 무변경.)

- [ ] **Step 2: `Runtime/Scripts/Game/LOPWorld.cs` 생성** (디렉터리 신설)
```csharp
namespace LOP
{
    public class LOPWorld : GameFramework.World.WorldBase
    {
        public LOPWorld(
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.WorldEventBuffer eventBuffer)
            : base(entityRegistry, eventBuffer) { }
    }
}
```
> 빈 골격(훅 override 없음). 엔진-프리 유지. 4c가 Collection/Mutation/Detection override로 로직 흡수.

- [ ] **Step 3: 컴파일 검증** — `refresh_unity` 후 `read_console` 에러 0 (양쪽). `LOPWorld`/asmdef 변경이 클·서에 전파. `Runtime/Scripts/Game/`의 신규 .meta(디렉터리 .meta 포함) 생성 확인.

---

## Task 3: Client — DI 등록 + Runner 보유·ticked

**Files:** `Assets/Scripts/Game/GameLifetimeScope.cs`, `Assets/Scripts/Game/LOPRunner.cs`

- [ ] **Step 1: `GameLifetimeScope.Configure` — IWorld 등록**

`EntityRegistry`/`WorldEventBuffer` 등록 라인 근처(World Core 블록)에 추가:
```csharp
builder.Register<GameFramework.World.IWorld, LOPWorld>(Lifetime.Singleton);
```

- [ ] **Step 2: `LOPRunner` — 주입 필드 + Tick 호출**

(a) `[Inject]` 필드 추가(다른 `[Inject]` 옆):
```csharp
[Inject] private GameFramework.World.IWorld world;
```
(b) `UpdateRunner()`에서 `UpdateAI();` 뒤·`SimulatePhysics();` 앞(결정론 sim 자리)에 삽입:
```csharp
            UpdateAI();

            world.Tick(Runner.Time.tick, (float)tickUpdater.interval);

            SimulatePhysics();
```
> 훅 no-op이라 무효과. 호출 지점만 확립. `Runner.Time.tick`(long)·`tickUpdater.interval`(double→float).

- [ ] **Step 3: 컴파일 검증** — Client 인스턴스 `read_console` 에러 0.

---

## Task 4: Server — DI 등록 + Runner 보유·ticked (평행)

- [ ] **Step 1: 서버 `LOPRunner.UpdateRunner` 루프 구조 확인** — 서버 repo `Assets/Scripts/Game/LOPRunner.cs` 읽어 결정론 sim 자리(입력/AI 의도 push 후 ~ 물리/egress 전) 파악.

- [ ] **Step 2: 서버 `GameLifetimeScope` — IWorld 등록** (Client Step 1과 동일 라인)

- [ ] **Step 3: 서버 `LOPRunner` — 주입 필드 + Tick 호출** — `[Inject] GameFramework.World.IWorld world;` + Step 1에서 정한 결정론 자리에 `world.Tick(Runner.Time.tick, (float)tickUpdater.interval);` 삽입.

- [ ] **Step 4: 컴파일 검증** — Server 인스턴스 `read_console` 에러 0.

---

## Task 5: 종합 컴파일 + 스모크 검증
- [ ] **Step 1: 양쪽 재스캔 + 컴파일** — Client·Server 각 `refresh_unity`(scope=all, force, wait_for_ready) → idle → `read_console`(types=error) **0**.
- [ ] **Step 2: 신규 .meta 확인** — GameFramework `IWorld.cs.meta`/`WorldBase.cs.meta`, LOP-Shared `LOPWorld.cs.meta`(+ Game 디렉터리 .meta) 생성됨.

---

## Task 6: 커밋 (repo별)
- [ ] **Step 1: GameFramework** — `git add Runtime/Scripts/World/IWorld.cs* Runtime/Scripts/World/WorldBase.cs*` (+테스트 추가했으면 그 파일·meta).
```
feat(world): IWorld + WorldBase skeleton (Generation phase hooks, no-op)

Inner deterministic sim skeleton. WorldBase references EntityRegistry +
WorldEventBuffer (no ownership move), Tick() calls Collection/Mutation/
Detection (virtual no-ops). Logic absorption deferred to 4c.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
- [ ] **Step 2: LOP-Shared** — `git add Runtime/baegames.LOP.Shared.Runtime.asmdef Runtime/Scripts/Game/LOPWorld.cs*` (+ Game 디렉터리 .meta).
```
feat(world): LOPWorld shared sim skeleton (empty, client/server single copy)

LOPWorld : WorldBase in LOP-Shared so client and server run one sim code
copy (determinism by construction). Empty; hooks filled in 4c. asmdef now
references GameFramework.World.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
- [ ] **Step 3: Client** — `git add Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/Game/LOPRunner.cs` (픽스처 제외).
```
feat(world): Runner holds + ticks World (no-op); register IWorld -> LOPWorld

LOPRunner injects IWorld and calls world.Tick() at the deterministic
position each tick (empty hooks -> no-op, behavior preserved). Seam for 4c.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
- [ ] **Step 4: Server** — Client Step 3와 동형. 변경 파일만(픽스처 제외).
- [ ] **Step 5: Client 문서 커밋** — `git add docs/superpowers/specs/2026-06-23-world-sim-extraction-design.md docs/superpowers/plans/2026-06-23-world-sim-extraction.md`.

---

## Task 7: 검증 + 머지 (사용자)
- [ ] **Step 1: 최종 컴파일** — 양쪽 `read_console` 에러 0.
- [ ] **Step 2: 플레이 검증(사용자)** — 이동/점프/물리/HP/데미지/죽음 **이전과 동일**. `IWorld` resolve 성공(스폰/플레이 시 NPE 없음), `world.Tick` 매틱 호출되나 무효과.
- [ ] **Step 3: OK 시 머지/푸시(사용자 요청 시)** — GameFramework → LOP-Shared → Client → Server 순 `--no-ff` (file: 의존: GF·Shared 먼저). 머지·push는 사용자 요청 시에만.

---

## Self-Review (작성자 체크)
- **Spec 커버리지:** IWorld/WorldBase=Task1 / asmdef+LOPWorld=Task2 / 클 DI·Runner ticked=Task3 / 서 동형=Task4 / 검증=Task5 / 커밋=Task6. ✅
- **추가형·동작 보존:** 신규 타입은 기존 컴파일 비파괴, world.Tick은 no-op 훅만 → 상태 변경 0. 로직 0줄 이동. ✅
- **상태 무이동(D2):** WorldBase가 기존 DI 싱글톤 참조만(생성자 주입) → 기존 주입처 무변경. ✅
- **EOL 안전:** mass sed 없음(신규=Write, 편집=Edit). 신규 .cs LF는 git LF 저장이라 무해. ✅
- **공유 1벌:** LOPWorld=LOP-Shared(클·서 동일 코드) = 결정론 구조 강제. ✅
- **플레이스홀더:** 서버 Tick 삽입 위치만 실행 시 루프 확인(Task4 Step1) — 그 외 정확한 코드 명시. ✅
