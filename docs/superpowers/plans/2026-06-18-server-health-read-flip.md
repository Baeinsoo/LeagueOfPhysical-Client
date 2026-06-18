# Server HP Read Flip → World.Health (Slice 1b) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 서버에서 HP를 읽는 두 소비자(`LOPGameEngine`의 UserEntitySnap, `CharacterCreationDataCreator`)를 레거시 `HealthComponent` → 코어 `World.Health`로 전환한다. 동작 보존(World.Health == legacy).

**Architecture:** 두 소비자가 `[Inject] EntityRegistry`로 `entityRegistry.Get(id)?.Get<Health>()`를 해석해 `Current`/`Max`를 읽는다. null이면 `Debug.LogWarning` + 안전값 0. HP만 이전(MP/Level 등은 legacy 유지). writer/death/legacy 제거는 Slice 2.

**Tech Stack:** Unity (서버, 단일 Assembly-CSharp), VContainer DI, GameFramework `World`(공유, 불변).

---

## Repo & Branch

전 작업이 **서버 repo 하나**(GameFramework·클라 불변).

| 항목 | 값 |
|---|---|
| 코드 repo | `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server` |
| 브랜치 | `feature/server-health-read-flip` (Task 1에서 생성) |
| spec/plan | 클라 repo (이미 커밋, 변경 없음) |

main 직접 커밋 금지. 모든 commit은 서버 repo 피처 브랜치에.

> **⚠️ 서버 로컬 픽스처 (건드리지 말 것):** 서버 working-tree에 미커밋 변경 `Assets/Scripts/Game/LOPGame.cs`(SpawnEnemies 수)·`Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`(playerList UUID)가 상존. **이 두 파일은 이 plan에서 안 건드림**(우리 변경 파일이 아님). 절대 스테이징/커밋/`git restore` 하지 말 것.

## UnityMCP 인스턴스 핀 — **서버**

사용자 지시 서버 작업. UnityMCP 호출마다 `unity_instance`를 **서버**로:
1. `mcpforunity://instances`에서 `name == "LeagueOfPhysical-Server"`의 전체 `id` resolve. (작성 시점: `LeagueOfPhysical-Server@f99391fa2dbaaf3c` — hash 변동 가능.)
2. `refresh_unity`/`read_console`에 `unity_instance="<서버 id>"`. **클라로 라우팅 금지. 실제 실행하고 보고.**

## 배경 사실 (조사)

- 서버 HP **reader는 정확히 2곳**: `LOPGameEngine.cs:145-146`(UserEntitySnapToC HP, EndUpdate) + `CharacterCreationDataCreator.cs:26-27`(MaxHP/CurrentHP). `GetAllEntitySnaps`(EntitySnapsToC)는 HP 미포함.
- `World.Health == legacy`: `WorldEventApplicator`(`LOPGameEngine.ProcessEvent`)가 매 틱 `Current = remaining`(legacy값) 갱신. 두 reader는 `ProcessEvent` 후(`EndUpdate`/스폰데이터)에 읽으므로 동일값.
- `LOPGameEngine`은 `[DIMonoBehaviour]`(필드 주입, `using UnityEngine;`·`using VContainer;` 보유) — `EntityRegistry` 주입 가능, `Debug` 가용.
- `CharacterCreationDataCreator`는 1a로 DI 등록됨 — `[Inject]` 가능. 현재 `using GameFramework; using System;`만 — `[Inject]` 위해 `using VContainer;` 추가 필요. `using UnityEngine;` 없음 → `Debug`는 풀 한정(`UnityEngine.Debug`).
- `Health.Current`/`Max`는 `int`. `entityRegistry.Get(id)` → `World.Entity`(없으면 null), `.Get<Health>()` → Health(없으면 null).

## File Structure (서버)

| 파일 | 변경 | 책임 |
|---|---|---|
| `Assets/Scripts/Game/LOPGameEngine.cs` | Modify | UserEntitySnapToC HP를 World.Health에서 |
| `Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs` | Modify | 생성데이터 MaxHP/CurrentHP를 World.Health에서 |

두 변경은 독립·각자 behavior-preserving(read-source swap). 한 Task에서 함께(동일 패턴) 처리.

> **테스트 전략:** 신규 자동화 테스트 없음(단일 Assembly-CSharp). read-source swap이라 검증 = 서버 컴파일 + 수동 플레이(값 동일=회귀 0). TDD 부적용.

---

## Task 1: 두 HP reader를 World.Health로 전환

**Files:**
- Modify `Assets/Scripts/Game/LOPGameEngine.cs`
- Modify `Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs`

- [ ] **Step 1: 피처 브랜치 생성 (server repo)**
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git checkout -b feature/server-health-read-flip
git branch --show-current   # → feature/server-health-read-flip
```
(이미 있으면 `git checkout feature/server-health-read-flip`.)

- [ ] **Step 2: LOPGameEngine에 EntityRegistry 주입 추가** — 기존 주입 필드 블록:
```csharp
        [Inject] private GameFramework.World.WorldEventBuffer worldEventBuffer;
        [Inject] private GameFramework.World.WorldEventApplicator worldEventApplicator;
        [Inject] private WireBroadcaster wireBroadcaster;
```
뒤에 한 줄 추가:
```csharp
        [Inject] private GameFramework.World.WorldEventBuffer worldEventBuffer;
        [Inject] private GameFramework.World.WorldEventApplicator worldEventApplicator;
        [Inject] private WireBroadcaster wireBroadcaster;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
```

- [ ] **Step 3: LOPGameEngine.EndUpdate의 UserEntitySnap HP 두 줄 교체** — 현재:
```csharp
                UserEntitySnapToC entitySnapsToC = new UserEntitySnapToC();
                entitySnapsToC.CurrentHP = entity.GetEntityComponent<HealthComponent>().currentHP;
                entitySnapsToC.MaxHP = entity.GetEntityComponent<HealthComponent>().maxHP;
                entitySnapsToC.CurrentMP = entity.GetEntityComponent<ManaComponent>().currentMP;
```
를 다음으로(앞에 World.Health 해석 + null 경고 추가, CurrentHP/MaxHP만 교체, MP 이하 줄은 그대로):
```csharp
                GameFramework.World.Entity worldEntity = entityRegistry.Get(entity.entityId);
                GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
                if (health == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Health not found for entity {entity.entityId}");
                }

                UserEntitySnapToC entitySnapsToC = new UserEntitySnapToC();
                entitySnapsToC.CurrentHP = health?.Current ?? 0;
                entitySnapsToC.MaxHP = health?.Max ?? 0;
                entitySnapsToC.CurrentMP = entity.GetEntityComponent<ManaComponent>().currentMP;
```
> MP/Exp/Level/StatPoints 줄과 `foreach (var action ...)` 이하는 변경하지 않는다.

- [ ] **Step 4: CharacterCreationDataCreator에 using + 주입 추가** — 파일 상단 using:
```csharp
using GameFramework;
using System;
```
를:
```csharp
using GameFramework;
using System;
using VContainer;
```
그리고 클래스 본문 첫 멤버(`public EntityType EntityType => EntityType.Character;` 앞)에 주입 필드 추가:
```csharp
    public class CharacterCreationDataCreator : IEntityCreationDataCreator<LOPEntity>
    {
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        public EntityType EntityType => EntityType.Character;
```

- [ ] **Step 5: CharacterCreationDataCreator.Create(LOPEntity)의 HP read 교체** — `baseEntityCreationData` 블록과 `characterCreationData` 초기화 사이에 World.Health 해석을 넣고, `MaxHP`/`CurrentHP` 두 줄을 교체. 현재 `Create(LOPEntity lopEntity)` 본문:
```csharp
            var baseEntityCreationData = new BaseEntityCreationData
            {
                EntityId = lopEntity.entityId,
                Position = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.position),
                Rotation = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.rotation),
                Velocity = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.velocity),
            };

            global::CharacterCreationData characterCreationData = new global::CharacterCreationData
            {
                BaseEntityCreationData = baseEntityCreationData,
                CharacterCode = lopEntity.GetEntityComponent<CharacterComponent>().characterCode,
                VisualId = lopEntity.GetEntityComponent<AppearanceComponent>().visualId,

                MaxHP = lopEntity.GetEntityComponent<HealthComponent>().maxHP,
                CurrentHP = lopEntity.GetEntityComponent<HealthComponent>().currentHP,
                MaxMP = lopEntity.GetEntityComponent<ManaComponent>().maxMP,
```
를 다음으로:
```csharp
            var baseEntityCreationData = new BaseEntityCreationData
            {
                EntityId = lopEntity.entityId,
                Position = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.position),
                Rotation = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.rotation),
                Velocity = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.velocity),
            };

            GameFramework.World.Entity worldEntity = entityRegistry.Get(lopEntity.entityId);
            GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
            if (health == null)
            {
                UnityEngine.Debug.LogWarning($"[World] CharacterCreationData: Health not found for entity {lopEntity.entityId}");
            }

            global::CharacterCreationData characterCreationData = new global::CharacterCreationData
            {
                BaseEntityCreationData = baseEntityCreationData,
                CharacterCode = lopEntity.GetEntityComponent<CharacterComponent>().characterCode,
                VisualId = lopEntity.GetEntityComponent<AppearanceComponent>().visualId,

                MaxHP = health?.Max ?? 0,
                CurrentHP = health?.Current ?? 0,
                MaxMP = lopEntity.GetEntityComponent<ManaComponent>().maxMP,
```
> `MaxMP` 이하(MP/Level/Exp)와 `return new EntityCreationData {...}`, `Create(IEntity)`는 변경하지 않는다. `UnityEngine.Debug`는 풀 한정(파일에 `using UnityEngine;` 없음 — 추가하지 말 것).

- [ ] **Step 6: 컴파일 확인** — UnityMCP (SERVER pin):
  - `refresh_unity(unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
  - `read_console(unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c", types=["error"])`
  Expected: 0 errors. **실제 실행하고 보고.**

- [ ] **Step 7: Commit** — 두 파일만 스테이징(LOPGame.cs/ConfigureRoomComponent.cs는 절대 포함 금지):
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/Game/LOPGameEngine.cs Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs
git status   # 위 2개만 staged인지 확인 (LOPGame.cs/ConfigureRoomComponent.cs는 unstaged로 남아야 함)
git commit -m "refactor(world): read HP from World.Health in server snapshot + creation data (Slice 1b)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: 런타임 검증 (수동)

> read-source swap — 값 동일이 성공.

- [ ] **Step 1: 서버 플레이 + 관찰** — 서버 에디터 Play(룸 연결, 캐릭터 피격 발생시키기). 콘솔/클라에서:
  - [ ] 유저 HUD **HP 초기값·피격 감소 정상**(UserEntitySnap 경로 → 클라 표시).
  - [ ] 피격으로 HP 깎인 캐릭터에 **늦게 접속**한(둘째) 클라가 **올바른 현재 HP** 수신(`GetAllEntityCreationDatas` → CharacterCreationDataCreator 경로).
  - [ ] `Health not found` 경고가 정상 플레이 중 **안 뜸**(캐릭터는 항상 World.Health 보유).
  - [ ] 콘솔 에러 0(DI 누락 NRE 없음 — EntityRegistry 주입 정상). MP/EXP/Level 표시도 정상(legacy 유지, 무영향).

- [ ] **Step 2: 검증 실패 시** — superpowers:systematic-debugging. 예상 위험: (a) `Health not found` 경고 다발 → 타이밍/등록 확인(스폰 시 CharacterCreator의 World.Health 등록이 creation-data 호출보다 먼저인지). (b) HP가 0으로 표시 → World.Health 미해석(EntityRegistry 주입/엔티티 id 확인).

---

## Self-Review (작성자 체크 결과)

- **Spec coverage:** spec의 2개 변경(LOPGameEngine UserEntitySnap + CharacterCreationDataCreator) + null 가드(LogWarning+0) + HP-only + 검증 → Task 1/2 매핑. ✓
- **Placeholder scan:** TBD/TODO 없음. 모든 코드 step에 실제 코드(before/after). 서버 `<서버 id>`는 구현 시 resolve. ✓
- **Type consistency:** `entityRegistry.Get(id)` → `GameFramework.World.Entity`, `?.Get<GameFramework.World.Health>()` → `Health`, `health?.Current ?? 0`/`health?.Max ?? 0`(int) — 두 파일 동일 패턴. `[Inject] GameFramework.World.EntityRegistry`. ✓
- **컴파일 순서:** 두 변경 독립·각자 behavior-preserving, 한 커밋. ✓
- **로컬 픽스처 보호:** LOPGame.cs/ConfigureRoomComponent.cs 미접촉 + 커밋 제외 명시. ✓
- **단일 repo:** 전부 서버. GameFramework/클라 불변. ✓
