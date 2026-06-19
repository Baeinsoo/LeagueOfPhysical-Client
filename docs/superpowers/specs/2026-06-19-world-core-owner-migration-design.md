# World Core 이행 — User/Player: 레거시 UserComponent/PlayerComponent → World.Owner + Stats.UnspentPoints

**Date:** 2026-06-19
**Branch (docs):** `feature/world-core-owner-migration` (클라 repo — 설계 허브)
**Branch (code):** GameFramework + 클·서 각 repo 피처 브랜치 (구현 시 생성)
**Related:** [Stats 이행](2026-06-19-world-core-stats-migration-design.md) · [Level 이행](2026-06-18-world-core-level-migration-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [Entity System Design](../../entity-system-design.md)

## Goal

마지막 레거시 엔티티 컴포넌트 `UserComponent`(클라) / `PlayerComponent`(서버)를 제거한다. 이 둘이 섞고 있던 **두 직교 개념을 분해**해 각각의 일반적 집으로 옮긴다:

1. **소유/제어 마커 + 정체성**(userId, "이 엔티티는 NPC가 아니라 유저가 제어한다") → 신규 일반 컴포넌트 **`GameFramework.World.Owner { string OwnerId }`**.
2. **statPoints**(레벨업으로 얻고 스탯에 쓰는 할당 예산) → 기존 **`GameFramework.World.Stats`에 `int UnspentPoints`** 추가.

이로써 World Core 컴포넌트 이행(Health/Mana/Level/Stats에 이은 마지막)이 완료되고, "Player/User"라는 도메인 이름 대신 일반 `Owner` + 응집된 `Stats.UnspentPoints`가 된다.

## 현재 구조 (조사 확인)

- **`UserComponent`(클라)** = `statPoints`(int, observable/PropertyChange) **하나뿐**.
- **`PlayerComponent`(서버)** = `userId`(string, get/private set) + `statPoints`(int, plain).
- **옮길 가변 게임플레이 필드 = `statPoints` 하나** (HP/MP/level/exp/stats는 이미 World로 이행).
- **`userId`는 컴포넌트에서 읽는 곳이 0** — 모든 세션↔엔티티 조회는 `session.userId` + `LOPEntityManager`의 `userEntityMap`/`entityUserMap`. 컴포넌트 userId는 중복.
- **`statPoints` wire** = `UserEntitySnapToC` 필드 7(매 틱 권위 싱크). `CharacterCreationData`엔 없음 → 생성 시드 불필요(0 시작).
- **마커**: 서버 5곳이 `HasEntityComponent<PlayerComponent>()`로 "플레이어인가?" 판정 — `PhysicsComponent.cs:88`, `LOPCombatSystem.cs:27·28`, `EnemyBrain.cs:22`, `LOPActionManager.cs:38`. NPC도 World Stats/Level 보유 → "스탯 있음"은 마커 불가.
- 클라는 마커 미사용(5곳 전부 서버). 클라 statPoints는 로컬 유저 HUD 표시용.
- **GameFramework에 Player/User/Owner 컴포넌트 없음** — 신규. 기존 컴포넌트: Health/Mana/Level/Stats/Transform/Velocity. 컨벤션: 평이한 명사, anemic, 일부 ctor.

## 잠긴 결정 (브레인스토밍 합의)

| # | 축 | 결정 | 근거 |
|---|---|---|---|
| ① | 분해 | **소유 마커/정체성 ↔ statPoints 분리** | 레거시가 직교 개념을 conflate. 분리가 일반적·응집적 |
| ② | 소유 마커 | 신규 `World.Owner { string OwnerId }`. 존재=마커, OwnerId=소유 정체성 | "Player" 도메인명 회피, 일반 ownership 개념(재사용). userId에 실제 역할(소유 키) 부여 → 중복 해소 |
| ③ | statPoints | 기존 `Stats.UnspentPoints`(int). `StatsSystem.Allocate`(원자적 spend+AddBase)/`AddUnspent`(grant)/`SetUnspent`(스냅샷) | 할당 예산이 Stats/할당 로직과 응집. 신규 컴포넌트 불필요 |
| ④ | userId | 컴포넌트 redundant userId **드롭**. `Owner.OwnerId`가 새 집 | 컴포넌트 read 0. 세션 조회는 manager 맵 유지 |
| ⑤ | 클라 | 신규 클라 컴포넌트 없음 — statPoints는 이미 있는 World.Stats의 UnspentPoints로. `UserComponent` 통째 제거. 마커 미사용이라 클라엔 Owner도 불필요 | 클라는 로컬 유저 statPoints 표시만 |
| ⑥ | 라이브 HUD | 신규 이산 이벤트 `EntityStatPointsChanged(int)`. statPoints가 마지막 PropertyChange 케이스 → `StatsViewModel`에서 PropertyChange 통째 제거 | Mana/Level/Stats 패턴(init pull + 이산 이벤트), Level HUD처럼 PropertyChange 완전 제거 |
| ⑦ | 상주 싱크 | Generation/Application 파이프라인 안 씀 — 상주 값 권위 싱크 | [[world-core-view-migration-status]] ⭐잠금 |

## Scope (In)

### ① GameFramework (공유 코어)

- `World/Components/Owner.cs` **신설**:
  ```csharp
  namespace GameFramework.World
  {
      /// <summary>엔티티가 유저/세션에 의해 소유·제어됨을 표시. 존재 자체가 "플레이어(비-NPC)" 마커.
      /// OwnerId는 소유자 식별자(LOP=userId). 로직 없음(anemic) — 생성 시 1회 세팅 후 불변.</summary>
      public class Owner : Component
      {
          public string OwnerId { get; set; }
      }
  }
  ```
  (시스템 없음 — 순수 데이터 태그, per-tick 싱크 없음.)
- `World/Components/Stats.cs`: `public int UnspentPoints { get; set; }` 추가(기존 `BaseStats`/`Modifiers` 옆).
- `World/Systems/StatsSystem.cs` 확장(기존 GetValue/SetBase/AddBase 유지):
  - `void AddUnspent(Stats stats, int amount)` — `stats.UnspentPoints += amount;` (레벨업 보상).
  - `int Allocate(Stats stats, int statType)` — 원자적·가드:
    ```csharp
    if (stats.UnspentPoints <= 0) { return (int)GetValue(stats, statType); }
    stats.UnspentPoints--;
    AddBase(stats, statType, 1);
    return (int)GetValue(stats, statType);
    ```
    (현 서버엔 statPoints>0 가드 없음 → 가드 추가는 안전성 개선; 클라가 이미 가드해 실질 동작 동일.)
  - `void SetUnspent(Stats stats, int value)` — `stats.UnspentPoints = value;` (클라 권위 스냅샷 적용).
- EditMode 테스트(TDD): `AddUnspent` 누적, `Allocate`(포인트>0 → UnspentPoints-- + BaseStats+1 + 새 값 반환 / 포인트=0 → no-op·현재 값), `SetUnspent` 덮어쓰기.

### ② 서버 (`LeagueOfPhysical-Server`)

- `EntityCreator/CharacterCreator.cs`: 플레이어 분기(`!string.IsNullOrEmpty(creationData.userId)`)에서 `PlayerComponent` 생성(3줄) 제거 → `World.Entity` 등록 블록(또는 인접)에서 `worldEntity.Add(new GameFramework.World.Owner { OwnerId = creationData.userId });`. (userEntityMap/entityUserMap 채움은 그대로.)
- **마커 5곳** `HasEntityComponent<PlayerComponent>()` → `entityRegistry.Get(entity.entityId)?.Has<GameFramework.World.Owner>() == true`:
  - `LOPCombatSystem.cs:27·28` — `entityRegistry` 이미 보유(Stats 슬라이스).
  - `LOPActionManager.cs:38`, `EnemyBrain.cs:22`, `PhysicsComponent.cs:88` — `entityRegistry` 주입 경로 확인 필요. **plan에서 각 사이트 DI 확정**(주입 가능하면 주입; MonoBehaviour/AI라 어려우면 `LOPEntityManager`의 user-map 기반 `IsPlayer(entityId)` 헬퍼로 대체). ⚠️ **이번 이행의 주 구현 리스크**.
- `Game/LOPGame.cs` 레벨업: `toucher.GetEntityComponent<PlayerComponent>().statPoints += gained;` → World.Stats 해석 후 `statsSystem.AddUnspent(stats, gained)`. (⚠️ LOPGame.cs는 픽스처 — stash 댄스.)
- `Game/MessageHandler/Game.Entity.MessageHandler.cs` `OnStatAllocationToS`: `statsSystem.AddBase(...)` + `PlayerComponent.statPoints--` 두 경로 → `int statValue = statsSystem.Allocate(stats, statType);` 한 호출. 반환값을 `StatAllocationToC.StatValue`에. (`entityRegistry`/`statsSystem` 이미 주입.)
- `Game/LOPGameEngine.cs` 스냅샷 빌드: `entitySnapsToC.StatPoints = entity.GetEntityComponent<PlayerComponent>().statPoints;` → `stats.UnspentPoints`(World.Stats, `entityRegistry.Get` 이미 보유). null 가드(없으면 0, Debug.LogWarning).
- `Game/GameLifetimeScope.cs`: Owner는 시스템 없음(등록 불필요). StatsSystem 이미 등록. 변경 없음(확인만).
- 서버 `Assets/Scripts/Component/PlayerComponent.cs`(+`.meta`) 삭제.

### ③ 클라 (`LeagueOfPhysical-Client`)

- statPoints는 로컬 유저 엔티티에 이미 존재하는 `World.Stats.UnspentPoints`로. **신규 클라 컴포넌트 없음.**
- `EntityCreator/CharacterCreator.cs`: 레거시 `UserComponent` 생성(`isUserEntity` 분기) 제거. (클라는 Owner도 불필요 — 마커 미사용. World.Stats는 Stats 이행에서 이미 생성됨.)
- `Entity/Event.Entity.cs`: 신규 `struct EntityStatPointsChanged { int statPoints; + 생성자 }`.
- `Game/MessageHandler/Game.Entity.MessageHandler.cs` `OnUserEntitySnapToC`: `playerContext.entity.GetComponent<UserComponent>().statPoints = userEntitySnapToC.StatPoints;` → World.Stats 해석 후 `statsSystem.SetUnspent(stats, userEntitySnapToC.StatPoints)` + 변경 시 `EntityStatPointsChanged` 발행(Mana/Level 동형 change-detect). (`entityRegistry`/`statsSystem` 이미 주입.)
- `UI/Stats/StatsViewModel.cs`: `_user`(UserComponent) 필드·해석 제거. `PushAll`에서 `_statPoints.Value = stats.UnspentPoints`(World.Stats pull). `EntityStatPointsChanged` 구독 → `_statPoints`. **`OnPropertyChange` 핸들러 + `PropertyChange` 구독/해제 통째 제거**(statPoints가 마지막 케이스). `Allocate` 가드 `_user.statPoints == 0` → `_statPoints.CurrentValue == 0`.
- `Game/GameLifetimeScope.cs`: StatsSystem 이미 등록(Stats 이행). 변경 없음.
- 클라 `Assets/Scripts/Component/UserComponent.cs`(+`.meta`) 삭제.

## 데이터 흐름 (전환 후)

```
서버 스폰: CharacterCreator → World.Owner{OwnerId=userId}(플레이어만) + World.Stats(UnspentPoints=0 기본)
레벨업: LevelSystem.AddExperience→gained → statsSystem.AddUnspent(stats, gained)
할당: StatAllocationToS → OnStatAllocationToS: statValue = statsSystem.Allocate(stats, statType)  [UnspentPoints--·AddBase 원자]
                          → StatAllocationToC{Stat, StatValue} 회신
마커: entityRegistry.Get(id)?.Has<Owner>()  (5곳)
스냅샷: LOPGameEngine → UserEntitySnapToC.StatPoints = stats.UnspentPoints
                    │ (wire: UserEntitySnapToC.StatPoints[7] — 기존, 무변경)
                    ▼
클라: OnUserEntitySnapToC → statsSystem.SetUnspent(World.Stats, StatPoints)
                          → (변경 시) EventBus.Publish(EntityStatPointsChanged(value))
      StatsViewModel: 초기 World.Stats.UnspentPoints pull + EntityStatPointsChanged 구독 → (R3) View
```

`Stats.UnspentPoints`가 statPoints 단일 진실원본. `World.Owner` 존재가 플레이어 마커. wire 무변경.

## 슬라이스 (구현 순서)

- **Slice 1 (GameFramework)**: `Owner` 컴포넌트 + `Stats.UnspentPoints` + `StatsSystem.AddUnspent/Allocate/SetUnspent` + EditMode 테스트. 비파괴(추가만).
- **Slice 2 (서버)**: Owner 생성 + 마커 5곳 repoint + 레벨업 grant + 할당 Allocate + 스냅샷 read + 레거시 `PlayerComponent` 제거. ⚠️ `LOPGame.cs`(레벨업 줄) 수정 → 픽스처 **stash 댄스**(Level Slice 2 선례). 마커 사이트 DI는 plan에서 확정.
- **Slice 3 (클라)**: `EntityStatPointsChanged` + 스냅샷 apply(SetUnspent) + HUD(World.Stats pull + 구독, PropertyChange 제거) + `UserComponent` 제거. 무관 dirty 커밋 제외.

wire 슬라이스 없음(UserEntitySnapToC.StatPoints 기존). 서버/클라 슬라이스는 wire 호환되면 독립. Slice 1 게이트.

## 검증

- **컴파일**: 영역별 0에러(UnityMCP `refresh_unity`+`read_console`, 각 인스턴스 핀). GameFramework file: 공유 → 양쪽 확인.
- **GameFramework EditMode**: `StatsSystem.AddUnspent/Allocate/SetUnspent` 통과.
- **런타임(수동)**:
  - 할당(+버튼) → statValue 증가·statPoints(UnspentPoints) 감소, HUD 갱신.
  - 레벨업 → statPoints 증가.
  - **마커 동작 보존**: combat/AI/물리/액션이 플레이어 vs NPC 구분 정상(예: AI가 플레이어 타게팅, 액션 NPC 필터). 회귀 관찰.
  - `[World] ... Stats not found` 경고 정상 플레이 중 안 뜸. 콘솔 에러 0. World Core 회귀(데미지→사망) 정상.
- **LOP 글루** = 컴파일 + 수동 플레이(단일 Assembly-CSharp). 코어만 EditMode.

## GUID / .meta 정책

- **삭제**: 클 `UserComponent.cs`+`.meta`, 서 `PlayerComponent.cs`+`.meta` `git rm`. 코드로 `AddEntityComponent`되던 컴포넌트 — 씬/prefab GUID 참조 0.
- **수정/신규**: 기존 파일명 유지(.meta 무관), 신규 `Owner.cs`/이벤트는 Unity 생성 `.meta` 동반 커밋.

## Out of scope (defer)

- **`LOPEntityManager` 맵을 `Owner.OwnerId`로 dedup** — 현재 맵 유지(별도). Owner.OwnerId는 자기서술 정체성(현 read 0이나 소유 로직/lag-comp의 미래 집).
- **Owner 기반 소유/권위 로직**(ownership 일반화 활용) — 미래.
- position 진실원본/물리/통합 fan-out/예측 = Stage ④.

## 문서/브랜치 정책

선례대로 **spec·plan은 클라 repo** 피처 브랜치 `feature/world-core-owner-migration`. **코드는 GameFramework + 클·서 각 repo** 피처 브랜치. 서버 working-tree 로컬 픽스처(`LOPGame.cs`/`ConfigureRoomComponent.cs`)는 **커밋 금지·stash 보존**(Slice 2가 `LOPGame.cs` 레벨업 줄 수정 → stash 댄스). 이 spec은 `CLAUDE.md` `@` 자동 로드 목록에 추가.

## Open Decisions (plan에서 해소)

- [ ] **마커 5곳 DI** — `LOPActionManager`/`EnemyBrain`/`PhysicsComponent`가 `entityRegistry`를 주입받을 수 있는지 사이트별 확인. 가능하면 `Has<Owner>()`, 어려우면 `LOPEntityManager` user-map 기반 `IsPlayer(entityId)` 헬퍼로 통일. **이행의 주 리스크 — plan 1순위.**
- [ ] **`Owner.OwnerId` 채움 위치** — 클라는 Owner 미생성(마커 미사용) 확정. 서버만 생성.
- [ ] `Has<T>()` API — `GameFramework.World.Entity`에 존재 확인(entity-system-design 기준 존재). 없으면 `Get<Owner>() != null`.

## 진행

- [x] 분해(Owner + Stats.UnspentPoints) + userId 드롭 + 마커=Owner 존재 합의
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 Slice 1 구현 plan 작성
- [ ] 구현(GameFramework→서버→클라) → 검증 → 머지

> 후속: 이 이행으로 **레거시 엔티티 컴포넌트 전부(Health/Mana/Level/Stats/User/Player) World Core 이행 완료**. 남은 건 선택적 서버 Health Slice 3, Owner 맵 dedup, Stage④.
