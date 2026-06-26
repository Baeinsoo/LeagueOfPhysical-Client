# 이동속도 → 런타임 스탯 Implementation Plan (Phase 3b)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 이동속도를 `characterComponent.masterData.Speed`(상수)에서 **런타임 스탯 `MoveSpeed`**로 옮긴다. 엔티티 생성 시 `BaseStats[MoveSpeed] = masterData.Speed`로 시드하고, `LOPMovementManager`가 `StatsSystem.GetValue(MoveSpeed)`를 읽어 이동 커널에 넘긴다. **값이 동일**(base = 기존 상수, 모디파이어 없음) → **거동 변화 0**. 이로써 StatusEffect 모디파이어(헤이스트 등)가 이동속도에 작용할 토대가 깔린다(실제 헤이스트 발동은 3d).

**Spec:** `docs/superpowers/specs/2026-06-26-ability-statuseffect-world-core-design.md` (Phase 3 Open Question — 이동→Stats 배선)
**선행:** Phase 3a(틱 스캐폴드) main 머지. 엔티티에 `Stats`(4 primary) 이미 부여됨.

**Architecture:** GameFramework `EntityStatType`에 `MoveSpeed` 추가 + 클·서 `CharacterCreator`(시드)·`LOPMovementManager`(읽기) 대칭 변경. 클·서 `LOPMovementManager`는 **바이트 동일** — 같은 Edit 양쪽.

**Tech Stack:** Unity / C# / VContainer DI / UnityMCP(클·서 컴파일·플레이 검증).

---

## 레포 / 도구 참조
- **GameFramework:** `C:\Users\re5na\workspace\LOP\GameFramework` (`EntityStatType`에 1값 추가)
- **Client:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client` (CharacterCreator + LOPMovementManager + 문서)
- **Server:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server` (CharacterCreator + LOPMovementManager)
- **LOP-Shared:** 무변경(이동 커널 `MovementSystem`은 primitives-in — speed 출처만 바뀜).
- **UnityMCP:** Client(`@de70658b9450cbb4`)/Server(`@f99391fa2dbaaf3c`) — 해시 변동 시 `mcpforunity://instances` 재확인. 모든 호출에 `unity_instance` 명시.

**⚠️ EOL:** mass `sed` 금지. 편집=Edit.
**픽스처 보존:** `git add -A`/`commit -am` 금지. Task 명시 파일만 add. (Client: Room.unity/ProjectSettings 제외. Server: Room.unity/ConfigureRoomComponent 제외.)
**컨벤션:** Microsoft C#. World 타입 풀 네임스페이스.

---

## 확정된 설계 결정 (실측)

1. **`MoveSpeed`를 `GameFramework.World.EntityStatType`에 추가**(enum 끝에 append → 값 4, 기존 0~3 불변). `Stats.BaseStats`는 (int) 키라 영향 없음. (primary 의미는 살짝 넓어지나, 별도 스탯 enum 신설보다 단일 enum 유지가 일관.)
2. **시드 = `CharacterCreator`의 World 블록**: `worldStats.BaseStats[(int)MoveSpeed] = characterComponent.masterData.Speed`. `characterComponent.masterData`(=TbCharacter 행, `.Speed` 보유)는 World 블록 전 `Initialize`로 이미 셋업됨(클·서 동일).
3. **읽기 = `LOPMovementManager`**: `EntityRegistry`+`StatsSystem` 주입 → `entityRegistry.Get(entity.entityId).Get<Stats>()` → `statsSystem.GetValue(stats, (int)MoveSpeed)`를 `MovementSystem.ProcessMovement`의 speed로. **JumpPower는 무변경**(`masterData.JumpPower` 유지 — 헤이스트 무관). 가드(Character/Physics) 유지.
4. **거동 0 보장**: 시드값 = 기존 상수, 모디파이어 없음 → `GetValue` = base = 동일 수치. 모든 이동 엔티티(플레이어·AI 적)는 CharacterCreator 생성이라 MoveSpeed 보유.
5. **World 엔티티 존재 불변식**: 이동 처리 시점엔 엔티티가 이미 생성·등록(Stats 시드 포함) → 직접 조회(가드 없이). 안전성은 플레이 검증.
6. **테스트**: 호스트 plumbing이라 신규 EditMode 없음(이동 커널·StatsSystem은 기존 테스트). 검증 = 컴파일 + 플레이 무회귀.

---

## Task 0: 피처 브랜치 (GameFramework / Client / Server)
- [ ] **Step 1: working tree 확인** (모두 main, 픽스처 외 깨끗)
```bash
for r in GameFramework LeagueOfPhysical-Client LeagueOfPhysical-Server; do echo "== $r =="; git -C C:/Users/re5na/workspace/LOP/$r status --short; echo "branch: $(git -C C:/Users/re5na/workspace/LOP/$r branch --show-current)"; done
```
Expected: GameFramework 깨끗(main); Client 픽스처 + 이번 plan(untracked); Server 픽스처(Room.unity/ConfigureRoomComponent). 다른 변경 있으면 멈추고 보고.
- [ ] **Step 2: 브랜치 생성**
```bash
for r in GameFramework LeagueOfPhysical-Client LeagueOfPhysical-Server; do git -C C:/Users/re5na/workspace/LOP/$r checkout -b feature/movespeed-stat; done
```

---

## Task 1: GameFramework — `EntityStatType`에 `MoveSpeed`

- [ ] **Step 1: `Runtime/Scripts/World/EntityStatType.cs`에 `MoveSpeed` 추가** (Edit). `Vitality,` 다음에:
```csharp
        Vitality,
        MoveSpeed,
```
- [ ] **Step 2: 컴파일 검증** — 클·서 `refresh_unity`(scope=all, force) → `read_console`(error) 0. (enum 추가는 비파괴.)

---

## Task 2: Client — 시드(CharacterCreator) + 읽기(LOPMovementManager)

- [ ] **Step 1: `Assets/Scripts/EntityCreator/CharacterCreator.cs` — MoveSpeed 시드** (Edit). `worldStats.BaseStats[...Vitality] = creationData.vitality;` 다음, `worldEntity.Add(worldStats);` 전에:
```csharp
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.MoveSpeed] = characterComponent.masterData.Speed;
```
- [ ] **Step 2: `Assets/Scripts/Game/LOPMovementManager.cs` — 주입 필드 추가** (Edit). `using` 블록에 `using VContainer;` 추가하고, 클래스 본문 맨 위(`public void ProcessInput` 전)에:
```csharp
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        [Inject]
        private GameFramework.World.StatsSystem statsSystem;

```
- [ ] **Step 3: `LOPMovementManager` — speed를 스탯에서 읽기** (Edit). `var result = MovementSystem.ProcessMovement(...)` 블록 교체:
```csharp
            var worldStats = entityRegistry.Get(entity.entityId).Get<GameFramework.World.Stats>();
            float speed = statsSystem.GetValue(worldStats, (int)GameFramework.World.EntityStatType.MoveSpeed);

            var result = MovementSystem.ProcessMovement(new MovementInput(
                entity.velocity, horizontal, vertical, speed));
```
> 가드·점프(`masterData.JumpPower`)·적용은 무변경.
- [ ] **Step 4: 컴파일 검증** — Client `refresh_unity` → `read_console`(error) 0.

---

## Task 3: Server — 시드 + 읽기 (클라와 동형, 바이트 동일 Edit)

- [ ] **Step 1: 클·서 `LOPMovementManager` 동일성 재확인** — 두 파일 diff(현재 바이트 동일). 동일하면 Task 2 Step 2·3과 같은 Edit 적용.
- [ ] **Step 2: `Assets/Scripts/EntityCreator/CharacterCreator.cs` — MoveSpeed 시드** (Edit). 서버 `worldStats` 블록(`...Vitality = creationData.vitality;`) 다음, `worldEntity.Add(worldStats);` 전에 Task 2 Step 1과 동일 줄.
- [ ] **Step 3: `Assets/Scripts/Game/LOPMovementManager.cs`** — Task 2 Step 2·3과 동일 Edit(`using VContainer;` + 주입 2필드 + speed 읽기).
- [ ] **Step 4: 컴파일 검증** — Server `refresh_unity` → `read_console`(error) 0.

---

## Task 4: 종합 컴파일 검증
- [ ] **Step 1: 양쪽 재스캔** — Client·Server 각 `refresh_unity`(scope=all, force, wait_for_ready) → `read_console`(error) **0**.
- [ ] **Step 2: 기존 EditMode 회귀 없음** — `run_tests`(`baegames.LOP.Shared.Tests.EditMode`) green 유지(이번 슬라이스는 LOP-Shared 무변경이라 20 그대로).

---

## Task 5: 커밋 (repo별)
- [ ] **Step 1: GameFramework** — `git add Runtime/Scripts/World/EntityStatType.cs`
```
feat(world): add MoveSpeed to EntityStatType

Movement speed becomes a runtime stat so status-effect modifiers
(e.g. haste) can affect it. Appended (no value shift).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
- [ ] **Step 2: Client** — `git add Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/Game/LOPMovementManager.cs` (픽스처 제외)
```
feat(world): movement reads MoveSpeed stat instead of masterData constant (Phase 3b)

Seed BaseStats[MoveSpeed] = masterData.Speed at creation; LOPMovementManager
reads StatsSystem.GetValue(MoveSpeed) for the movement kernel. Value
identical (base = old constant, no modifiers) -> behavior unchanged.
Enables haste to affect movement speed (activation in 3d). JumpPower
unchanged.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
- [ ] **Step 3: Server** — Client Step 2와 동형. 변경 파일만(픽스처 제외).
- [ ] **Step 4: Client 문서** — `git add docs/superpowers/plans/2026-06-26-movespeed-stat.md`

---

## Task 6: 검증 + 머지 (사용자)
- [ ] **Step 1: 최종 컴파일** — 양쪽 `read_console` 에러 0. 테스트 green.
- [ ] **Step 2: 플레이 무회귀(사용자)** — 이동 속도가 *이전과 동일*(전후좌우 이동·AI 적 이동), 점프 동일, NRE 0. (값이 같으니 체감 변화 없어야 함.)
- [ ] **Step 3: 머지/푸시(사용자 요청 시)** — GameFramework → Client → Server 순 `--no-ff`. 머지·push는 사용자 요청 시에만.

---

## Self-Review (작성자 체크)
- **Spec 커버리지(3b):** enum=Task1 / 클 시드·읽기=Task2 / 서 시드·읽기=Task3 / 검증=Task4. ✅
- **거동 보존:** 시드값=기존 `masterData.Speed`, 모디파이어 0 → `GetValue`=동일 수치. JumpPower·가드·적용 무변경. ✅
- **대칭:** 클·서 `LOPMovementManager` 바이트 동일 → 같은 Edit. CharacterCreator 시드도 동형. ✅
- **enum 안전:** `MoveSpeed` append(값 4), 기존 0~3 불변, BaseStats는 (int) 키 → 직렬화 영향 0. ✅
- **불변식:** 이동 시점 World 엔티티 등록·Stats 시드 완료 → 직접 조회 안전(플레이 검증). 모든 이동 엔티티=CharacterCreator 생성=MoveSpeed 보유. ✅
- **범위 정직:** 실제 헤이스트 발동=3d, JumpPower 스탯화 안 함(불요), LOP-Shared 무변경. ✅
- **EOL/픽스처:** Edit만, mass sed 없음, 픽스처 제외. ✅
```
