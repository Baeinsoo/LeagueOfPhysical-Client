# World Core 이행 — Mana: 레거시 ManaComponent → World.Mana (서버+클라)

**Date:** 2026-06-18
**Branch (docs):** `feature/world-core-mana-migration` (클라 repo — 설계 허브)
**Branch (code):** 클·서 각 repo 피처 브랜치 (구현 시 생성)
**Related:** [Health 대체 슬라이스(클라)](2026-06-18-world-core-health-replace-legacy-design.md) · [서버 Health Slice 1b](2026-06-18-server-health-read-flip-design.md) · [서버 Health Slice 2](2026-06-18-server-health-slice2-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [아키텍처 가이드라인](../../architecture-guidelines.md)

## Goal

레거시 `ManaComponent`(MonoBehaviour, `LOPComponent` — 클·서 각각)를 순수 C# 코어 **`GameFramework.World.Mana`**로 이행한다. MP의 단일 진실원본을 World 코어로 일원화하고 양쪽 레거시 컴포넌트를 제거한다. Health가 간 길과 **동형**(Model A, inside-out)이되, MP 고유 특성에 따른 차이 두 가지(아래)를 반영한다.

서버·클라 HP를 `World.Health`로 모은 데 이은 다음 컴포넌트 이행. Mana/Level/Stats/User 중 **가장 동형·저위험인 Mana를 먼저** 한 슬라이스로 처리한다(Level=Player 결합, Stats=형태 불일치, Player=World 등가물 부재라 각자 별도 설계).

## Health와 다른 점 (MP 고유)

1. **게임플레이 writer 없음 → writer flip 없음.** MP는 현재 소비/회복 로직이 0이다(전수 확인 — `currentMP -=` 없음). 서버가 생성 시 값을 정하고 매 틱 스냅샷으로 클라에 전달할 뿐, 전투 같은 mutate 경로가 없다. 따라서 Health Slice 2의 combat writer flip에 해당하는 작업이 **없다**. (마나 소비가 생기면 그때 `ManaSystem`에 mutate 메서드 + writer를 추가 — 별도.)
2. **라이브 갱신 채널이 도메인 사건이 아니라 권위 스냅샷이다.** HP 라이브 갱신은 `EntityDamage`(combat의 `DamageDealtEvent` fan-out)라는 *도메인 사건*으로 온다. MP엔 그런 사건이 없고, 값이 바뀌는 유일한 경로는 매 틱 `UserEntitySnapToC`(권위 스냅샷)다. 그래서 HUD 라이브 MP는 **스냅샷 적용 시점에 발행하는 작은 presentation 이벤트**로 받는다(아래 ④).

## 잠긴 결정 (브레인스토밍 + 웹 리서치 합의)

| # | 축 | 결정 | 근거 |
|---|---|---|---|
| ① | 코어 컴포넌트 | `World.Mana`(이미 존재: `Max`/`Current`, ctor `Mana(int max)`) 사용. 신규 `ManaSystem.ApplyAuthoritativeState(Mana, max, current)`(HealthSystem 미러: `Max=max; Current=Clamp(current,0,max)`) 추가 | Health 패턴. 상태 변경은 System 경유(가이드라인). 순수 C# → EditMode 테스트 |
| ② | 라이브 HUD 갱신 | 초기값 = `World.Mana` **pull**(PushAll), 라이브 = 신규 이산 이벤트 `EntityManaChanged` 구독 | HP와 동형(pull + 이산 이벤트). `World.Mana`는 anemic이라 직접 바인딩 불가 |
| ③ | **제네릭 옵저버 레이어 — 만들지 않음** | `World.*`에 per-property 변경 이벤트/리액티브 래퍼를 두지 않는다. 현 pull + 이산 `WorldEventBuffer`/presentation 이벤트 유지 | 주류 ECS도 연속상태엔 동기 push 콜백을 안 쓰고 배치/버전만 씀(Entitas Collector=배치, DOTS=청크 version filter). **동기 per-property push는 롤백/재시뮬과 충돌**(SnapNet: 효과는 state로 추적 후 확정 시 트리거). 우리 `WorldEventBuffer`가 이미 그 deferred-data 옵저버. 현 소비자는 대부분 pull이라 통합 이득도 적음(YAGNI). |
| ④ | 라이브 이벤트 발행 위치/시점 | 클라 스냅샷 핸들러(`OnUserEntitySnapToC`)가 `World.Mana` 적용 후 **값이 바뀌었을 때만** `EntityManaChanged(current, max)` 발행 | 옛 `ManaComponent.PropertyChange`(값 변경 시 발행) 시맨틱을 World 위에서 복원. 정적 MP라 사실상 거의 안 터짐(스팸 없음), 마나 소비 생기면 즉시 동작(future-proof) |
| ⑤ | 범위 | **Mana만.** Level/Stats/User는 각자 별도 슬라이스 | Level=Player/statPoints 결합, Stats=형태 불일치+combat 결합, User=World 등가물 부재 |

> **누락 위험에 대한 메모(durable):** 브레인스토밍에서 "World.Health writer가 여럿(combat/applicator/스냅샷)인데 UI 신호(`EntityDamage`)는 combat만 fan-out → 권위 보정 시 라이브 누락" 위험을 식별했다. 정답은 *per-property 옵저버*가 아니라 **모든 상태 변경을 이산 버퍼로 흘려보내고 Bridge 한 곳에서 fan-out**(Generation/Application/Bridge, SnapNet의 "state로 추적 후 트리거"와 동형)이며, 이는 **Stage ④**(commit gate/통합 fan-out)의 일이다. 이 슬라이스 밖.

## Scope (In) — 3개 영역

### ① GameFramework (공유 코어)
- `World/Systems/ManaSystem.cs` **신설**: `ApplyAuthoritativeState(Mana mana, int max, int current)` — `mana.Max = max; mana.Current = Math.Clamp(current, 0, max);` (HealthSystem.ApplyAuthoritativeState 미러).
- **EditMode 테스트** `Tests/.../ManaSystemTests` 신설(또는 기존 `ManaTests`에 추가): Max/Current 덮어쓰기 + Current 클램프(음수→0, 초과→Max). 순수 C# → TDD 적용.

### ② 서버 (`LeagueOfPhysical-Server`)
- `CharacterCreator`: `World.Mana`(`new Mana(maxMP){ Current = currentMP }`) 생성 — World.Health 등록 블록 옆 + 레거시 `ManaComponent` 생성 제거.
- `LOPGameEngine`(스냅샷 빌드, 현 `currentMP/maxMP` read 줄): `World.Mana`에서 read(`entityRegistry.Get(id)?.Get<Mana>()`, null시 `Debug.LogWarning`+0 — Health 1b 패턴). 이미 `EntityRegistry` 주입됨.
- `CharacterCreationDataCreator`: `maxMP/currentMP`를 `World.Mana`에서 read(이미 `EntityRegistry` 주입됨).
- 서버 `ManaComponent.cs`(+`.meta`) 삭제. (서버는 Mana를 생성+read만 → `ManaSystem` 등록 불필요.)

### ③ 클라 (`LeagueOfPhysical-Client`)
- `CharacterCreator`: `World.Mana` 생성(World.Health 옆) + 레거시 `ManaComponent` 생성 제거.
- `Event.Entity.cs`: **신규 presentation 이벤트** `EntityManaChanged(int current, int max)` (클라 전용 — `EntityDamage`와 동형).
- `Game.Entity.MessageHandler.OnUserEntitySnapToC`: `[Inject] ManaSystem` 추가. 현 `ManaComponent` write 2줄을 `World.Mana` 적용으로 교체 — Health 블록 바로 아래에 동형으로(`entityRegistry.Get → Get<Mana>() → manaSystem.ApplyAuthoritativeState`, null 가드). **변경 시 `EntityManaChanged` 발행.**
- `CharacterHudViewModel`: `_mana`(레거시) 필드 제거. `PushAll`에서 초기 MP를 `World.Mana`에서 pull. `OnPropertyChange`의 Mana 케이스(`currentMP`/`maxMP`) 제거. `EntityManaChanged` 구독 → `_mp`/`_maxMp` 갱신(현 `OnEntityDamage`가 `_hp` 갱신하는 것과 동형). `PropertyChange` 구독 자체는 유지(Level이 아직 사용 — 별도 슬라이스).
- `GameLifetimeScope`: `ManaSystem` Singleton 등록(스냅샷 핸들러 주입용).
- 클라 `ManaComponent.cs`(+`.meta`) 삭제.

> View↔ViewModel R3 바인딩(`vm.Mp`→MP바)은 **이미 완성·무변경**. 이 슬라이스 작업은 전부 *ViewModel↔World 모델* 경로(pull + 이산 이벤트)다.

## 데이터 흐름 (전환 후)

```
서버: CharacterCreator → World.Mana 생성(권위 초기값)
      LOPGameEngine.EndUpdate → UserEntitySnapToC.{CurrentMP,MaxMP} ← World.Mana (per-tick pull)
      CharacterCreationDataCreator → CharacterCreationData.{MaxMP,CurrentMP} ← World.Mana (스폰/늦은접속)
                    │  (wire: UserEntitySnapToC / CharacterCreationData — 기존, 변경 없음)
                    ▼
클라: CharacterCreator → World.Mana 생성(수신 초기값)
      OnUserEntitySnapToC → manaSystem.ApplyAuthoritativeState(World.Mana, MaxMP, CurrentMP)
                          → (값 변경 시) EventBus.Publish(EntityManaChanged(current, max))
                                          │
      CharacterHudViewModel: 초기 pull(World.Mana) + EntityManaChanged 구독 → _mp/_maxMp → (R3) View MP바
```

`World.Mana`가 MP 단일 진실원본. wire 포맷(스냅샷/생성데이터의 MP 필드) 무변경. HUD는 HP와 동일하게 init-pull + 이산 이벤트.

## Out of scope (defer)

- **제네릭 옵저버/리액티브 레이어** — 위 결정 ③. 안 만듦.
- **통합 fan-out**(모든 World 상태 변경 → 이산 버퍼 → Bridge 단일 fan-out, 누락 위험 구조적 해소) = **Stage ④**.
- **마나 소비/회복**(`ManaSystem`에 mutate + writer) — 게임 디자인에 생길 때.
- **Level / Stats / User 이행** — 각자 별도 슬라이스(설계부터).
- position 진실원본 승격/물리/netcode = Stage ④.

## 조사 근거 (구현 사실)

- `World.Mana`(`GameFramework/Runtime/Scripts/World/Components/Mana.cs`) 이미 존재: `int Max`/`int Current`, ctor `Mana(int max)`(Current=max). `ManaSystem` 부재(주석 "이후 추가"). `Component`/`EntityRegistry`엔 변경 훅 없음(anemic + 평 dict).
- **레거시 `ManaComponent` 소비자(전수):**
  - 클라(4): `CharacterCreator`(생성+Initialize), `Game.Entity.MessageHandler.OnUserEntitySnapToC`(스냅샷 write), `CharacterHudViewModel`(초기 read + `PropertyChange` 라이브). 클라 `ManaComponent`는 INotifyPropertyChanged 보유.
  - 서버(3): `CharacterCreator`(생성+Initialize), `LOPGameEngine`(스냅샷 read), `CharacterCreationDataCreator`(생성데이터 read). 서버 `ManaComponent`는 평이.
- 클라 `Game.Entity.MessageHandler`는 이미 `[Inject] EntityRegistry`+`HealthSystem` 보유(Health 대체 슬라이스), `OnUserEntitySnapToC`가 `healthSystem.ApplyAuthoritativeState`로 HP 적용 중 — Mana는 그 바로 아래에 동형 추가.
- 클라 `CharacterHudViewModel`은 이미 `[Inject] EntityRegistry` 보유, `PushAll`에서 World.Health pull + `OnEntityDamage`로 라이브 HP — Mana도 동일 패턴.
- 서버 `LOPGameEngine`/`CharacterCreationDataCreator`는 Health 1b에서 이미 `EntityRegistry` 주입·World.Health read 중 — Mana read 추가.
- MP 게임플레이 writer 부재(전수 `currentMP -=` 0). `Stats`/`Level`/`Mana` World 컴포넌트는 현재 아무도 안 읽음(Health/Transform/Velocity만 소비). → Mana 이행 = 신규 소비 경로 확립.
- **웹 리서치 확인:** ECS 제네릭 변경관찰은 배치/버전(Entitas Collector, DOTS chunk version filter)이지 동기 push 아님 + 비용 존재. 롤백은 효과를 state-data로 deferred 처리(SnapNet). 순수 이벤트-only UI는 "implicit state machine" 누락 위험(Fall Damage) → init-pull+event 하이브리드 합리적. → 결정 ③ 보강.

## 검증

- **컴파일**: 클·서 0에러 (UnityMCP `refresh_unity`+`read_console`, **각 인스턴스 핀**). GameFramework `ManaSystem` 추가는 비파괴(추가만).
- **GameFramework EditMode 테스트**: `ManaSystem.ApplyAuthoritativeState`(Max/Current 덮어쓰기 + 클램프) — `baegames.GameFramework.World.Tests`에서 통과.
- **런타임(수동)**:
  - 유저 HUD **MP 바 초기값 정상**(스폰 시 World.Mana pull) + 스폰/**늦은접속** 다른 유저 MP 정상(생성데이터 경로).
  - (MP가 실제로 바뀌는 케이스가 현재 없으므로) 라이브 갱신은 무관측이 정상 — `EntityManaChanged`는 값 변경 시에만 발행되어 정상 플레이 중 스팸/에러 없음.
  - `[World] ... Mana not found` 경고가 정상 플레이 중 **안 뜸**(캐릭터는 항상 World.Mana 보유). 콘솔 에러 0. `ManaComponent` 클·서 완전 제거(grep 0).
- **LOP 글루**(creator/handler/VM/engine) = 컴파일 + 수동 플레이(단일 Assembly-CSharp 제약 — 자동 테스트는 코어 `ManaSystem`만).

## GUID / .meta 정책

- **삭제**: 클·서 `ManaComponent.cs`+`.meta` `git rm`. 코드로 `AddEntityComponent`되는 컴포넌트라 씬/prefab GUID 참조 0 → 안전.
- **수정/신규**: 기존 파일명 유지(.meta 무관), 신규 `ManaSystem.cs`/이벤트는 Unity 생성 `.meta` 동반 커밋.

## 문서/브랜치 정책

선례대로 **spec·plan은 클라 repo**(설계 허브) 피처 브랜치 `feature/world-core-mana-migration`. **코드는 클·서 각 repo** 피처 브랜치 + GameFramework(file: 공유, `ManaSystem` 추가는 양쪽 동시 반영). 서버 working-tree 로컬 픽스처(`LOPGame.cs`/`ConfigureRoomComponent.cs`)는 **커밋 금지·stash 보존**. 이 spec은 `CLAUDE.md` `@` 자동 로드 목록에 추가.

## 진행

- [x] 스코프(Mana 우선) + 라이브 채널(pull+이산 이벤트) + 제네릭 옵저버 기각(웹 리서치) 합의
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
- [ ] 구현(GameFramework→서버→클라) → 검증 → 머지

> 후속: Level(→Player 결합 처리), Stats(형태 매핑+combat 재작성), User/Player(World 등가물 설계) — 각자 별도 spec. 통합 fan-out/제네릭 채널은 Stage ④.
