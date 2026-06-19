# World Core 이행 — Level/Exp: 레거시 LevelComponent → World.Level (서버+클라)

**Date:** 2026-06-18
**Branch (docs):** `feature/world-core-level-migration` (클라 repo — 설계 허브)
**Branch (code):** GameFramework + 클·서 각 repo 피처 브랜치 (구현 시 생성)
**Related:** [Mana 이행](2026-06-18-world-core-mana-migration-design.md) · [Health 대체 슬라이스(클라)](2026-06-18-world-core-health-replace-legacy-design.md) · [서버 Health Slice 2](2026-06-18-server-health-slice2-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [아키텍처 가이드라인](../../architecture-guidelines.md)

## Goal

레거시 `LevelComponent`(MonoBehaviour, `LOPComponent` — 클·서 각각)를 순수 C# 코어 **`GameFramework.World.Level`**로 이행한다. level/exp의 단일 진실원본을 World 코어로 일원화하고 양쪽 레거시 컴포넌트를 제거한다.

Health/Mana가 간 길과 동형(Model A, inside-out, 상주 값 권위 싱크)이되, **Level 고유 특성 둘**을 반영한다:

1. **서버 런타임 writer 존재** — Mana는 정적이라 writer가 없었지만, Level은 경험치 획득 시 레벨업(`AddExperience` 루프)이라는 서버 권위 mutate 경로가 있다. → 서버 슬라이스에 **writer flip**이 들어간다(Health Slice 2와 유사).
2. **Player 결합(statPoints)** — 레벨업 1회당 `PlayerComponent.statPoints++`. statPoints는 별도 컴포넌트(`PlayerComponent` 서버 / `UserComponent` 클라)이고 이번 슬라이스 비대상이라, 이 결합을 **평범한 서버 상주-상태 변경**으로 보존한다(이벤트 파이프라인 없음).

## 잠긴 결정 (브레인스토밍 합의)

| # | 축 | 결정 | 근거 |
|---|---|---|---|
| ① | 모델 | **상주 값 권위 싱크**(HP값/MP값 등급). 이벤트 Generation/Application 파이프라인 **안 씀** | `world-core-connection-architecture.md`: 연속/상주 상태 = pull·권위 싱크, 이산 사건만 이벤트. level/exp/statPoints는 전부 상주 값. 레벨업에 별도 연출/캐스케이드 없음 → 이산 이벤트 불필요 |
| ② | statPoints 결합 | **`LevelSystem.AddExperience`가 증가한 레벨 수(int) 반환** → 서버 호출지에서 `statPoints += gained`. World 코어는 순수(legacy/Player 비참조) 유지, 결합은 Player가 사는 서버 LOP 레이어에 명시 | 단일 소비자에 이벤트 인프라는 과임. Mana의 잠금 결정("제네릭 옵저버/통합 fan-out은 Stage④까지 안 만듦")과 정합. 순수 반환값이라 Slice 4 공유 시뮬 추출 시 리프트 용이 |
| ③ | HUD 라이브 갱신 | 초기값 = `World.Level` pull, 라이브 = 신규 이산 신호 `EntityLevelChanged` 구독 (EntityManaChanged 동형, 얇은 presentation 신호) | `World.Level`은 anemic이라 직접 바인딩 불가. Mana 패턴 그대로 |
| ④ | `ExpToNext` | wire 미전송, **생성 시 100 시드**(레거시 parity). `ApplyAuthoritativeState`는 안 건드림 | 레거시가 `expToNextLevel=100` 하드코딩·비네트워크. 클라에선 HUD exp 바 분모 상수, 서버에선 레벨업 루프 임계 |
| ⑤ | 범위 | **Level/Exp만.** statPoints/Player·User, Stats는 각자 별도 | Player/User는 World 등가물 설계 미정(별도 슬라이스) |

> **로컬 서버 로직과의 정합(durable):** 상주 싱크 + 순수 `LevelSystem`(GameFramework, 엔진·네트워크 비참조, file: 공유)은 "원격 서버 vs 클라 로컬 시뮬" 어느 쪽이 구동해도 동작한다(연결 아키텍처 모드 표: 서버권위/클라단독/클라예측이 같은 코드 재사용). 로컬화의 실제 작업은 host 로직(`LOPGame`의 경험치 트리거)을 공유 시뮬(`LOPGameSimulation`)로 들어올리는 **Slice 4**이며 이번 결정과 직교. writer flip을 순수 `AddExperience` 호출로 두면 그 리프트가 쉬워진다(이벤트 파이프라인을 깔았다면 오히려 걸림돌).

## Scope (In) — 3개 영역

### ① GameFramework (공유 코어)

`GameFramework.World.Level`은 이미 존재(`int Value`, `long Exp`, `long ExpToNext`, anemic, ctor 없음). `LevelSystem.AddExperience(Level, long)`도 존재(레벨업 루프, 현재 `void` 반환, 호출처 0).

- `World/Systems/LevelSystem.cs`:
  - **`AddExperience` 시그니처를 `void` → `int` 반환**(증가한 레벨 수). 본문 루프 동일, `int gained` 카운트 후 반환:
    ```csharp
    public int AddExperience(Level level, long amount)
    {
        level.Exp += amount;
        int gained = 0;
        while (level.ExpToNext > 0 && level.Exp >= level.ExpToNext)
        {
            level.Exp -= level.ExpToNext;
            level.Value++;
            gained++;
        }
        return gained;
    }
    ```
  - **`ApplyAuthoritativeState(Level level, int value, long exp)` 신설** (Health/Mana 미러). 권위 값 덮어쓰기, 결정/계산/가드 없음:
    ```csharp
    public void ApplyAuthoritativeState(Level level, int value, long exp)
    {
        level.Value = value;
        level.Exp = exp;
    }
    ```
    `ExpToNext`는 인자에 없음 — wire 미전송, 생성 시 시드된 값 유지.
- EditMode 테스트(`baegames.GameFramework.World.Tests`):
  - `ApplyAuthoritativeState` — Value/Exp 덮어쓰기, ExpToNext 불변 검증.
  - `AddExperience` — (a) 임계 미만 → 0 반환, Exp만 증가, (b) 임계 1회 넘김 → 1 반환, Value+1·Exp 잔여, (c) 다중 레벨업(예: ExpToNext=100, amount=250) → 2 반환, Value+2·Exp=50.

### ② 서버 (`LeagueOfPhysical-Server`)

- `EntityCreator/CharacterCreator.cs`: `World.Level` 생성 — `worldEntity.Add(new GameFramework.World.Level { Value = creationData.level, Exp = creationData.currentExp, ExpToNext = 100 });` (World.Health/Mana 등록 블록 옆) + 레거시 `LevelComponent` 생성 3줄(`AddEntityComponent` + `Initialize`) 제거.
- **writer flip** — `Game/LOPGame.cs:172` 경험치 획득 지점. `[Inject] GameFramework.World.EntityRegistry`/`GameFramework.World.LevelSystem` 추가 후:
  ```csharp
  // 제거: toucher.GetEntityComponent<LevelComponent>().AddExperience(10);
  GameFramework.World.Entity worldEntity = entityRegistry.Get(toucher.entityId);
  GameFramework.World.Level worldLevel = worldEntity?.Get<GameFramework.World.Level>();
  if (worldLevel != null)
  {
      int gained = levelSystem.AddExperience(worldLevel, 10);
      if (gained > 0)
      {
          toucher.GetEntityComponent<PlayerComponent>().statPoints += gained;
      }
  }
  ```
- 스냅샷 빌드(`Game/LOPGameEngine.cs:163-164`, EndUpdate): `Level`/`CurrentExp`를 `entityRegistry.Get(id)?.Get<Level>()`의 `Value`/`Exp`에서. null이면 `Debug.LogWarning`+0/0 (Health 1b 패턴). `StatPoints`(165줄, `PlayerComponent`)는 그대로.
- 생성데이터(`EntityCreator/CharacterCreationDataCreator.cs:48-49`): `level`/`currentExp`를 World.Level의 `Value`/`Exp`에서. (이미 1a로 `EntityRegistry` 주입됨.) null이면 `Debug.LogWarning`+0.
- DI(`Game/GameLifetimeScope.cs`): `builder.Register<GameFramework.World.LevelSystem>(Lifetime.Singleton)` 추가. (현재 서버는 HealthSystem만 등록 — LevelSystem은 서버에 writer가 생기므로 필수.)
- 서버 `Assets/Scripts/Component/LevelComponent.cs`(+`.meta`) 삭제.

### ③ 클라 (`LeagueOfPhysical-Client`)

- `EntityCreator/CharacterCreator.cs`: `World.Level` 생성(ExpToNext=100, World.Health/Mana 옆) + 레거시 `LevelComponent` 생성 제거.
- `Entity/Event.Entity.cs`: 신규 presentation 이벤트 `struct EntityLevelChanged { int level; long currentExp; long expToNext; }` (생성자 포함, `EntityManaChanged`/`EntityDamage` 동형 — 클라 전용 HUD 갱신 신호).
- `Game/MessageHandler/Game.Entity.MessageHandler.cs` `OnUserEntitySnapToC`: `[Inject] GameFramework.World.LevelSystem` 추가. level/exp write 2줄(200-201)을 교체 — Mana 블록 바로 아래 동형:
  ```csharp
  GameFramework.World.Entity worldEntity = entityRegistry.Get(playerContext.entity.entityId);
  GameFramework.World.Level worldLevel = worldEntity?.Get<GameFramework.World.Level>();
  if (worldLevel != null)
  {
      int prevValue = worldLevel.Value;
      long prevExp = worldLevel.Exp;
      levelSystem.ApplyAuthoritativeState(worldLevel, userEntitySnapToC.Level, userEntitySnapToC.CurrentExp);
      if (worldLevel.Value != prevValue || worldLevel.Exp != prevExp)
      {
          EventBus.Default.Publish(
              EventTopic.EntityId<LOPEntity>(playerContext.entity.entityId),
              new EntityLevelChanged(worldLevel.Value, worldLevel.Exp, worldLevel.ExpToNext));
      }
  }
  ```
  **statPoints 줄(202, `UserComponent`)은 그대로 유지** — 별도(Level 비대상).
- `UI/CharacterHud/CharacterHudViewModel.cs`: `LevelComponent _level` 필드 제거. `PushAll`에서 초기 level/exp/expToNext를 `World.Level`에서 pull(`entityRegistry.Get(id)?.Get<Level>()`, null시 폴백). `EntityLevelChanged` 구독 → reactive props 갱신(현 `OnEntityManaChanged` 동형). `OnPropertyChange` switch의 Level 케이스(`level`/`currentExp`/`expToNextLevel`) 제거.
  - `PropertyChange` 구독 자체는 잔존 소비자(statPoints/User 등)가 있으면 유지, 없으면 제거 — **plan에서 잔존 케이스 전수 확인 후 확정**.
- DI(`Game/GameLifetimeScope.cs`): `LevelSystem` Singleton 등록(스냅샷 핸들러 주입용). (클라는 `ApplyAuthoritativeState`만 사용 — 클라 `AddExperience`는 사문이었음.)
- 클라 `Assets/Scripts/Component/LevelComponent.cs`(+`.meta`) 삭제.

> View↔ViewModel R3 바인딩(exp 바·레벨 표시)은 **이미 완성·무변경**. 이번 작업은 전부 *ViewModel↔World 모델* 경로(pull + 이산 신호).

## 데이터 흐름 (전환 후)

```
서버: CharacterCreator → World.Level{ Value, Exp, ExpToNext=100 } 생성(권위 초기값)
      LOPGame(경험치 획득): gained = levelSystem.AddExperience(worldLevel, 10)
                            if gained>0: PlayerComponent.statPoints += gained   ← 순수 서버 상주 변경
      LOPGameEngine.EndUpdate → UserEntitySnapToC.{Level,CurrentExp} ← World.Level (per-tick pull)
                                UserEntitySnapToC.StatPoints ← PlayerComponent (기존)
      CharacterCreationDataCreator → CharacterCreationData.{level,currentExp} ← World.Level
                    │  (wire: UserEntitySnapToC[5/6/7] / CharacterCreationData[8/9] — 기존, 변경 없음)
                    ▼
클라: CharacterCreator → World.Level{ ExpToNext=100 } 생성(수신 초기값)
      OnUserEntitySnapToC → levelSystem.ApplyAuthoritativeState(World.Level, Level, CurrentExp)
                          → (값 변경 시) EventBus.Publish(EntityLevelChanged(value, exp, expToNext))
                          → UserComponent.statPoints = StatPoints (기존, 별도)
                                          │
      CharacterHudViewModel: 초기 pull(World.Level) + EntityLevelChanged 구독 → (R3) View 레벨/exp 바
```

`World.Level`이 level/exp 단일 진실원본. wire 포맷 무변경. statPoints는 여전히 legacy Player/User(별도 이행).

## 슬라이스 (구현 순서)

Mana 패턴(GameFramework→서버→클라). Slice 1이 게이트(2·3이 `ApplyAuthoritativeState`+반환값에 의존). 서버/클라 슬라이스는 서로 독립(wire 불변).

- **Slice 1 (GameFramework)**: `AddExperience` int 반환 + `ApplyAuthoritativeState` 신설 + EditMode 테스트. 비파괴(추가/시그니처 확장, 호출처 0).
- **Slice 2 (서버)**: 생성 + writer flip(+statPoints) + 읽기 flip(스냅샷/생성데이터) + DI + 레거시 제거. 한 repo 원자적. 동작 보존(동일 루프·임계 100·statPoints++).
- **Slice 3 (클라)**: 생성 + `EntityLevelChanged` + 스냅샷 apply + HUD pull/구독 + DI + 레거시 제거. Mana 동형. 동작 보존.

## 검증

- **컴파일**: 영역별 0에러 (UnityMCP `refresh_unity`+`read_console`, **각 인스턴스 핀**). GameFramework는 file: 공유라 양쪽 영향 — Slice 1은 추가/확장이라 비파괴, 그래도 양쪽 컴파일 확인.
- **GameFramework EditMode**: `LevelSystem.ApplyAuthoritativeState` + `AddExperience` 반환값 테스트 통과.
- **런타임(수동)**:
  - 서버: 경험치구슬 획득 → level/exp 정상 증가, 레벨업 시 statPoints 증가, 클라 HUD 레벨/exp 바 갱신.
  - 클라: 유저 HUD 레벨/exp 초기값 정상(스폰 pull), 스냅샷 갱신 정상, 스폰/**늦은접속** 다른 유저 level/exp 정상(생성데이터 경로).
  - `[World] ... Level not found` 경고가 정상 플레이 중 **안 뜸**(캐릭터는 항상 World.Level 보유). 콘솔 에러 0. `LevelComponent` 클·서 완전 제거(grep 0).
- **LOP 글루**(creator/handler/VM/engine/LOPGame) = 컴파일 + 수동 플레이(단일 Assembly-CSharp — 자동 테스트는 코어 `LevelSystem`만).

## GUID / .meta 정책

- **삭제**: 클·서 `LevelComponent.cs`+`.meta` `git rm`. 코드로 `AddEntityComponent`되는 컴포넌트라 씬/prefab GUID 참조 0 → 안전.
- **수정/신규**: 기존 파일명 유지(.meta 무관), 신규 이벤트 struct는 기존 `Event.Entity.cs`에 추가(파일 신설 아님).

## Out of scope (defer)

- **statPoints / Player·User의 World 이행** — World 등가물 설계 미정. 별도 슬라이스(설계부터).
- **Stats 이행** — 형태 매핑 + combat 결합. 별도.
- **호스트 로직 → 공유 시뮬(`LOPGameSimulation`) 추출** = Slice 4. 경험치 트리거(`LOPGame`)의 공유화는 여기서.
- **position 진실원본 승격 / 물리 / 통합 fan-out / 예측·롤백** = Stage ④.

## 문서/브랜치 정책

선례대로 **spec·plan은 클라 repo**(설계 허브) 피처 브랜치 `feature/world-core-level-migration`. **코드는 GameFramework(file: 공유, Slice 1 양쪽 동시 반영) + 클·서 각 repo** 피처 브랜치. 서버 working-tree 로컬 픽스처(`LOPGame.cs`/`ConfigureRoomComponent.cs`)는 **커밋 금지·stash 보존**(Slice 2가 `LOPGame.cs`를 수정하므로 stash와의 충돌 주의 — plan에서 처리). 이 spec은 `CLAUDE.md` `@` 자동 로드 목록에 추가.

## 진행

- [x] 스코프(통합 스펙) + 모델(상주 싱크, 이벤트 파이프라인 X) + statPoints 결합(AddExperience 반환값) 합의
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 Slice 1 구현 plan 작성
- [ ] 구현(GameFramework→서버→클라) → 검증 → 머지

> 후속: statPoints/Player·User 이행, Stats 이행 — 각자 별도 spec. 공유 시뮬 추출은 Slice 4, 통합 fan-out/예측은 Stage ④.
