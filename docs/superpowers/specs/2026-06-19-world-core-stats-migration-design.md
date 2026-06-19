# World Core 이행 — Stats: 레거시 StatsComponent → World.Stats (GameFramework+Shared+서버+클라)

**Date:** 2026-06-19
**Branch (docs):** `feature/world-core-stats-migration` (클라 repo — 설계 허브)
**Branch (code):** GameFramework + LOP-Shared + 클·서 각 repo 피처 브랜치 (구현 시 생성)
**Related:** [Level 이행](2026-06-18-world-core-level-migration-design.md) · [Mana 이행](2026-06-18-world-core-mana-migration-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [Entity System Design](../../entity-system-design.md) · [아키텍처 가이드라인](../../architecture-guidelines.md)

## Goal

레거시 `StatsComponent`(MonoBehaviour, `LOPComponent` — 클·서 각각, ~40 named int 필드)를 순수 C# 코어 **`GameFramework.World.Stats`**(dict 기반)로 이행한다. 스탯의 단일 진실원본을 World 코어로 일원화하고, 양쪽 레거시 컴포넌트를 제거하며, **재접/늦은접속 시 할당 스탯이 초기값으로 리셋되는 기존 버그**([[stat-allocation-reconnect-reset-bug]])를 동반 해소한다.

Health/Mana/Level이 간 길(Model A, inside-out, 상주 값)을 따르되, Stats 고유 특성에 따른 차이를 반영한다(아래).

## Stats 고유 특성 (조사 확인)

- **~40 필드 중 실제 활성 = 4개 primary**(strength/dexterity/intelligence/vitality). 나머지 ~36개는 **완전 사문**(seed·read·write·할당 0). combat은 그 중 **strength·dexterity 2개만** 읽음. → 이행 대상은 **4개 primary만**(사문 드롭).
- **형태 불일치**: 레거시 = ~40 named int 필드, `World.Stats` = `Dictionary<int,float> BaseStats` + `List<StatModifier> Modifiers`. dict 키 = `(int)EntityStatType`(신설 enum).
- **combat 결합**(서버): `LOPCombatSystem`이 `StatsComponent.strength/dexterity`(int)를 읽어 데미지/회피/치명 판정.
- **할당 채널 존재**: `StatAllocationToS/ToC`로 4 primary 할당. 서버가 `++` + `statPoints--`, 클라가 named 필드 set(PropertyChange→ViewModel).
- **재접 버그 확정**: `CharacterCreationData`에 stat 필드 없음 → 재접/늦은접속 재구성 시 할당 스탯 0으로 소실. (statPoints는 `UserEntitySnapToC` per-tick 동기라 복구됨 — stat *값*만 영속 채널 부재.)
- **스탯은 할당 시에만 변함**(드물고 이산적) — per-tick 권위 스냅이 부적합.

## 잠긴 결정 (브레인스토밍 합의)

| # | 축 | 결정 | 근거 |
|---|---|---|---|
| ① | 대상 스탯 | **4개 primary만**(Strength/Dexterity/Intelligence/Vitality). enum 4값 | 나머지 ~36 완전 사문. dict라 자연 드롭, 필요 시 enum 값 추가. YAGNI |
| ② | 싱크 모델 | **하이브리드** — `CharacterCreationData`에 4 stat 추가(시드·재접) + 기존 `StatAllocationToS/ToC` 유지(World.Stats로 repoint). **per-tick stat 스냅 없음** | 스탯은 할당 시에만 변함 → per-tick 낭비. 할당 채널이 이미 라이브 전달. 시드 갭만 메우면 버그 해소 |
| ③ | 할당 저장 | **BaseStats 증가**(모디파이어 아님). combat은 `GetValue`(base+modifiers) 경유 + int 캐스트 | 할당=영구 베이스 상승. GetValue 경유로 미래 버프(모디파이어) 호환 |
| ④ | statPoints | **이번 비대상** — Player/User 도메인. 단 할당 핸들러가 `stat`+`statPoints--` 한 메서드라 그 핸들러는 편집(>statPoints-- 줄 유지) | statPoints는 이미 snapshot 동기(재접 복구됨). User 이행 영역 |
| ⑤ | 라이브 HUD | World.Stats pull(초기) + 신규 이산 이벤트 `EntityStatChanged`(클라). statPoints는 `UserComponent` PropertyChange 유지 | Level/Mana 패턴. statPoints가 PropertyChange를 계속 써서 구독은 잔존(Level과 달리 완전 제거 X) |
| ⑥ | 상주 싱크 | 이벤트 Generation/Application 파이프라인 안 씀 — 상주 값 권위 싱크 | [[world-core-view-migration-status]]의 ⭐잠금(제네릭 옵저버/통합 fan-out은 Stage④) |

## Scope (In) — 4개 영역

### ① GameFramework (공유 코어)

- `World/Enums/EntityStatType.cs` **신설**:
  ```csharp
  namespace GameFramework.World
  {
      public enum EntityStatType { Strength, Dexterity, Intelligence, Vitality }
  }
  ```
- `World/Systems/StatsSystem.cs` 확장 (기존 `GetValue`/`AddModifier`/`RemoveModifiersBySourceId` 유지):
  - **`float AddBase(Stats stats, int statType, float delta)`** — `BaseStats[statType] = (기존값 0 기본) + delta` 후 새 값 반환(서버 할당; AddExperience 반환 패턴).
  - **`void SetBase(Stats stats, int statType, float value)`** — `BaseStats[statType] = value`(클라 적용 + 시드).
  - (`ApplyAuthoritativeState`는 per-tick 스냅 없으니 미추가.)
- EditMode 테스트(TDD): `SetBase`→`GetValue` 반영, `AddBase` 반환·누적, 미존재 키 GetValue=0.

### ② Wire (LeagueOfPhysical-Shared)

- `Protos/CharacterCreationData.proto`에 **4 stat 필드 추가**(다음 필드 번호로): `int32 strength`, `int32 dexterity`, `int32 intelligence`, `int32 vitality`. proto 재생성.
- 서버 도메인 struct `CharacterCreationData`(서버 `Entity/CharacterCreationData.cs`) + AutoMapper 매핑에 4필드 추가(서버 슬라이스에서). (`UserEntitySnapToC`·`StatAllocationToS/ToC` proto는 **무변경**.)

> CharacterCreationData는 wire(proto, Shared 생성) + 서버 도메인 struct + 매퍼의 3중 표면. plan에서 정확한 매핑 지점 확정.

### ③ 서버 (`LeagueOfPhysical-Server`)

- `EntityCreator/CharacterCreator.cs`: World.Entity 등록 블록에 `World.Stats` 생성·시드 추가 — 4 stat을 `BaseStats[(int)EntityStatType.X] = creationData.X`로(또는 `statsSystem.SetBase`). + 레거시 `StatsComponent` 생성 3줄 제거.
- `CombatSystem/LOPCombatSystem.cs`: `[Inject] StatsSystem` 추가. `attacker.GetEntityComponent<StatsComponent>()` → `entityRegistry.Get(attacker.entityId)?.Get<Stats>()`. strength/dexterity 읽기를 `Mathf.RoundToInt(statsSystem.GetValue(stats, (int)EntityStatType.Strength))` / `...Dexterity`로. null 가드(없으면 0 — 기존 0-스탯 동작과 동일). `IsDodge`/`IsCritical` 시그니처 그대로.
- `EntityCreationDataFactory/CharacterCreationDataCreator.cs`: CharacterCreationData의 4 stat 필드를 World.Stats에서 채움(`Mathf.RoundToInt(GetValue(...))` 또는 BaseStats 조회) — 늦은접속 시드.
- `Game/MessageHandler/Game.Entity.MessageHandler.cs` `OnStatAllocationToS`: `statsComponent.strength++` 등을 `statsSystem.AddBase(stats, (int)EntityStatType.X, 1)`로(반환값을 `StatAllocationToC.StatValue`에). `entity.GetEntityComponent<PlayerComponent>().statPoints--` **유지**. `entityRegistry.Get(...)?.Get<Stats>()`로 stats 해석.
- `Game/GameLifetimeScope.cs`: `GameFramework.World.StatsSystem` Singleton 등록.
- 서버 `Assets/Scripts/Component/StatsComponent.cs`(+`.meta`) 삭제.

### ④ 클라 (`LeagueOfPhysical-Client`)

- `EntityCreator/CharacterCreator.cs`: `World.Stats` 생성·시드(creationData 4필드) + 레거시 `StatsComponent` 생성 제거.
- `Entity/Event.Entity.cs`: 신규 이벤트 `struct EntityStatChanged { int statType; int value; }`(생성자 포함).
- `Game/MessageHandler/Game.Entity.MessageHandler.cs` `OnStatAllocationToC`: `statsComponent.strength = StatValue` 등을 `statsSystem.SetBase(stats, statType, StatValue)` + `EventBus.Publish(EntityStatChanged((int)statType, StatValue))`로. `[Inject] StatsSystem` 추가. (stat 이름 문자열 → EntityStatType 매핑 필요 — `nameof` 기반 switch 유지하되 World stat으로 라우팅.)
- `UI/Stats/StatsViewModel.cs`: 초기값 World.Stats pull(`entityRegistry.Get(id)?.Get<Stats>()` + `GetValue`), `EntityStatChanged` 구독 → 해당 stat reactive prop 갱신. 레거시 `StatsComponent` stat 읽기/PropertyChange-stat 케이스 제거. **statPoints는 `UserComponent`(PropertyChange) 그대로** — PropertyChange 구독 잔존.
- `UI/Stats/StatsView.cs`: 할당 버튼이 보내는 `nameof(StatsComponent.strength)` 문자열 → World 기반 식별자로(또는 동일 문자열 유지하되 핸들러가 매핑). `StatsView.uxml` 요소명 무변경.
- `Game/GameLifetimeScope.cs`: `StatsSystem` Singleton 등록. 클라 `Assets/Scripts/Component/StatsComponent.cs`(+`.meta`) 삭제.

> **stat 식별 문자열**: 현재 할당 wire(`StatAllocationToS/ToC.Stat`)가 `nameof(StatsComponent.strength)`("strength" 등) 문자열을 운반. `StatsComponent` 삭제 후 이 `nameof` 참조가 깨지므로, 안정 문자열 상수(예: `EntityStatType.Strength.ToString()` 또는 전용 상수)로 치환한다. 클·서 동일 문자열을 써야 함 — plan에서 단일 소스(예: `EntityStatType` enum 이름) 확정.

## 데이터 흐름 (전환 후)

```
서버 스폰: CharacterCreator → World.Stats 시드(creationData 4 stat; 신규=0)
할당: StatAllocationToS → OnStatAllocationToS:
        statsSystem.AddBase(World.Stats, stat, 1) → newValue
        PlayerComponent.statPoints--                     (User 도메인, 유지)
        StatAllocationToC{ Stat, StatValue=newValue } 회신
combat: LOPCombatSystem → statsSystem.GetValue(World.Stats, Strength/Dexterity) (int 캐스트)
늦은접속: CharacterCreationDataCreator → CharacterCreationData.{str,dex,int,vit} ← World.Stats
                    │ (wire: CharacterCreationData[+4 stat], StatAllocationToC[기존])
                    ▼
클라: CharacterCreator → World.Stats 시드(creationData 4 stat) — 재접 시 할당값 복구 ✅
      OnStatAllocationToC → statsSystem.SetBase(World.Stats, stat, StatValue)
                          → EventBus.Publish(EntityStatChanged(stat, StatValue))
      StatsViewModel: 초기 World.Stats pull + EntityStatChanged 구독 → (R3) View
      (statPoints: UserComponent PropertyChange 그대로)
```

`World.Stats`가 stat 단일 진실원본. CharacterCreationData 시드로 재접 버그 해소. statPoints는 별도(무변경).

## 슬라이스 (구현 순서)

Level 패턴 + wire. 의존: 서버·클라가 GameFramework(enum/StatsSystem) + Shared(CreationData proto)에 의존 → 그 둘이 게이트.

- **Slice 1 (GameFramework)**: `EntityStatType` enum + `StatsSystem.AddBase/SetBase` + EditMode 테스트. 비파괴(추가만).
- **Slice 2 (Shared/wire)**: `CharacterCreationData.proto` +4 stat 필드 + 재생성. 비파괴(필드 추가, 기존 0 기본). 양쪽 컴파일 확인.
- **Slice 3 (서버)**: 생성 시드 + combat flip + 할당 flip + CreationDataCreator fill + 도메인 struct/매퍼 +4필드 + DI + 레거시 제거. ⚠️ 서버 로컬 픽스처(`LOPGame.cs`/`ConfigureRoomComponent.cs`) **stash 보존**(이번 변경 파일과 무관해야 — plan에서 확인). 동작 보존(combat 동일 입력→동일 판정, 단 stat 출처만 World).
- **Slice 4 (클라)**: 생성 시드 + `EntityStatChanged` + 할당 적용 flip + HUD flip + DI + 레거시 제거. 동작 보존.

서버/클라 슬라이스는 wire 호환되면 독립. Slice 1·2가 게이트.

## 검증

- **컴파일**: 영역별 0에러(UnityMCP `refresh_unity`+`read_console`, 각 인스턴스 핀). GameFramework/Shared는 file: 공유 → 양쪽 확인.
- **GameFramework EditMode**: `StatsSystem.AddBase/SetBase`/`GetValue` 테스트 통과.
- **런타임(수동)**:
  - 스탯 할당(+버튼) → HUD 즉시 갱신, statPoints 감소.
  - combat이 strength/dexterity 반영(데미지/회피/치명) — 할당 전후 차이 관찰.
  - **재접/늦은접속**: 할당한 스탯이 **유지**(0으로 리셋 안 됨) ← 버그 해소 확인.
  - `[World] ... Stats not found` 경고 정상 플레이 중 안 뜸. 콘솔 에러 0. World Core 회귀(데미지→사망) 정상.
- **LOP 글루** = 컴파일 + 수동 플레이(단일 Assembly-CSharp). 코어만 EditMode.

## GUID / .meta 정책

- **삭제**: 클·서 `StatsComponent.cs`+`.meta` `git rm`. 코드로 `AddEntityComponent`되는 컴포넌트 — 씬/prefab GUID 참조 0.
- **수정/신규**: 기존 파일명 유지(.meta 무관), 신규 enum/이벤트는 Unity 생성 `.meta` 동반 커밋.

## Out of scope (defer)

- **statPoints의 World 이행** — User/Player 슬라이스(별도). 이번엔 무변경(이미 snapshot 동기).
- **사문 ~36 stat 필드** — 드롭(미이행).
- **모디파이어 기반 버프/장비 시스템** — `Modifiers`/`AddModifier`는 코어에 존재하나 이번 게임플레이 연결 안 함(GetValue 경유로 미래 호환만 확보).
- **per-tick stat 권위 스냅**(UserEntitySnapToC에 stat 추가) — 불필요(할당 채널로 충분).
- **User/Player 이행**(클 `UserComponent`/서 `PlayerComponent` 분기 통합) — 별도.
- position 진실원본/물리/통합 fan-out/예측 = Stage ④.

## 문서/브랜치 정책

선례대로 **spec·plan은 클라 repo**(설계 허브) 피처 브랜치 `feature/world-core-stats-migration`. **코드는 GameFramework + LOP-Shared + 클·서 각 repo** 피처 브랜치. 서버 working-tree 로컬 픽스처(`LOPGame.cs`/`ConfigureRoomComponent.cs`)는 **커밋 금지·stash 보존**. 이 spec은 `CLAUDE.md` `@` 자동 로드 목록에 추가.

## 진행

- [x] 스코프(4 primary) + 싱크(하이브리드: CreationData 시드 + 할당 채널) + 할당 저장(BaseStats) + statPoints 경계 합의
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 Slice 1 구현 plan 작성
- [ ] 구현(GameFramework→Shared→서버→클라) → 검증 → 머지

> 후속: statPoints/User·Player 이행(별도), 선택적 서버 Health Slice 3. 모디파이어 버프/통합 fan-out은 Stage④.
