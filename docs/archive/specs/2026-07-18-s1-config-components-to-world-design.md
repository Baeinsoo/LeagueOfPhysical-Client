# S1 — 설정 컴포넌트 → World 데이터 컴포넌트

> **부모:** `2026-07-18-entity-view-rearchitecture-umbrella-design.md` (S1/5). 상태는 `docs/ROADMAP.md`.
> **범위:** 클라이언트 + 서버. 이 슬라이스만. 끝에 게임이 그대로 돌아가야 함(strangler).

## 목표

레거시 Unity "엔티티 컴포넌트" 4종(`AppearanceComponent`/`CharacterComponent`/`ItemComponent`/
`EntityTypeComponent` — 모두 `LOPComponent : MonoComponent`)을 제거하고, 그 데이터를 **LOP-Shared 순수
C# World 컴포넌트**로 이관한다. 이로써 `LOPEntity.components` 병렬 리스트가 `PhysicsComponent`만 남게
되어 S2/S3의 substrate 제거를 준비한다.

## 실측 근거 (2026-07-18 감사)

- **4종 전부 write-once**(생성 시 `Initialize` 1회, 이후 불변). `visualId`는 setter+PropertyChanged
  배관이 있으나 **어떤 writer도 재호출하지 않음**(죽은 능력).
- **데이터는 이미 생성데이터 구조체에 흐름.** `LOP.CharacterCreationData`(client
  `Assets/Scripts/Entity/CharacterCreationData.cs`, server는 `userId` 필드 추가)·`LOP.ItemCreationData`가
  `visualId`·`characterCode`/`itemCode`를 이미 보유. 4종 컴포넌트는 이걸 다시 복사해 든 **잉여**로,
  다른 시스템이 `GetEntityComponent<T>`(`GameFramework/.../Extensions/Entity.Extensions.cs`, `components`
  선형 스캔)로 읽으려 존재.
- **런타임 재독자는 하나뿐:** 서버 `EnemyBrain.cs:52`가 `CharacterComponent.masterData.Speed`를 매 AI틱.
- **서버가 birth 정보를 되읽는 이유:** `EntityCreationDataFactory`가 스폰 방송 시 엔티티에서 값을 되읽어
  와이어 proto를 **재구성**(늦은 접속자에게도 스폰 재전송). 그래서 값이 엔티티에 남아 있어야 함 →
  World 컴포넌트가 그 역할을 이어받음.
- **enum `EntityType`은 클·서에 중복 정의**(client `Assets/Scripts/Entity/Entity.cs:4-11`, server 미러):
  `None=0, Character=1, Projectile=2, Item=3, Environment=4`. 실사용은 `Character`/`Item`뿐.

### 소비자 지도 (verdict: 모두 write-once)

| 값 | 비-크리에이터 독자 | 시점 |
|---|---|---|
| `AppearanceComponent.visualId` | client `LOPEntityView.cs:49-51`(Start→모델 로드), `:76-77`(OnPropertyChange, 미발화); server `CharacterCreationDataCreator.cs:54`/`ItemCreationDataCreator.cs:24`(proto) | 생성 · 스폰방송 |
| `CharacterComponent.characterCode` | server `CharacterCreationDataCreator.cs:53`(proto)만. 클라 미독 | 스폰방송 |
| `CharacterComponent.masterData`(Speed/Jump) | 생성 시 Stats 시드(`CharacterCreator` client `:100-101`/server `:82-83`); server `EnemyBrain.cs:52`(매틱); client `EntityBinder.cs:43`(존재 검사) | 생성 + 런타임 |
| `ItemComponent.itemCode` | server `ItemCreationDataCreator.cs:23`(proto)만 | 스폰방송 |
| `ItemComponent.masterData` | **없음 — 죽은 필드** | — |
| `EntityTypeComponent.entityType` | server `EntityCreationDataFactory.cs:24,32`(크리에이터 디스패치)만. 클라 미독 | 스폰방송 |

## 목표 설계

### 새 World 컴포넌트 (LOP-Shared, `namespace LOP`, `GameFramework.World.Component` 상속)

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/`에 배치 (StatusEffects/Abilities와 같은 층). 클·서 단일
정의(현재 4종×2레포=8클래스 → 3종×1공유=3클래스로 중복 감소).

| 컴포넌트 | 필드 | 대체 | 시드 소스 |
|---|---|---|---|
| `Appearance` | `string VisualId` | `AppearanceComponent` | 생성데이터 `visualId` |
| `MasterDataRef` | `string Code` | characterCode/itemCode | 생성데이터 `code` |
| `EntityKind` | `EntityType Kind` | `EntityTypeComponent` | 크리에이터가 지정 |

- **enum `EntityType`**을 LOP-Shared로 이동해 **dedupe**(클·서 중복 제거), `EntityKind`가 보유.
  - 컴포넌트명을 `EntityKind`로 둔 이유: 값 홀더 컴포넌트가 enum `EntityType`·`LOP.MasterData.Character`와
    이름 충돌하지 않게. (빈 태그 마커 `Character`/`Item` 방식은 그 충돌로 기각.)
- 모두 anemic: 데이터+생성자 무결성만. 상태 변경 로직 없음.

### 소비자 재배선

| 소비자 | 지금 | S1 후 |
|---|---|---|
| client `LOPEntityView` 모델 로드 | `GetEntityComponent<AppearanceComponent>().visualId` | `entityRegistry.Get(id).Get<Appearance>().VisualId` |
| server `EntityCreationDataFactory` 디스패치 | `EntityTypeComponent.entityType` | `entity.Get<EntityKind>().Kind` |
| server `CharacterCreationDataCreator`/`ItemCreationDataCreator`(proto) | Appearance/Character/Item 컴포넌트 | `Appearance.VisualId` · `MasterDataRef.Code` |
| client `EntityBinder.cs:43` "캐릭터냐?" | `GetEntityComponent<CharacterComponent>() == null` | `entity.Get<EntityKind>().Kind == EntityType.Character` |
| server `EnemyBrain.cs:52`(매틱 Speed) | `CharacterComponent.masterData.Speed` | `statsSystem.GetValue(stats, (int)EntityStatType.MoveSpeed)` |
| 생성 시 Stats 시드(Speed/Jump) | `CharacterComponent.masterData` | 크리에이터가 인라인 `TbCharacter.Get(code)` 조회 후 시드 |

> `EnemyBrain`의 값 등가: `CharacterCreator`가 `Stats.MoveSpeed = masterData.Speed`로 시드하므로
> `GetValue(MoveSpeed)`는 동일 값(+ 모디파이어 반영이라 오히려 더 정확). 이 전환으로 `CharacterComponent`의
> 마지막 런타임 독자가 사라져 통째 삭제 가능.

### 시드 흐름

크리에이터(`CharacterCreator`/`ItemCreator`, 클·서 각자)가 GameObject 트리 조립 시:
- 기존 `AddEntityComponent<AppearanceComponent>().Initialize(...)` 등 4줄 → **World.Entity에 `Appearance`/
  `MasterDataRef`/`EntityKind` 추가**(`worldEntity.Add(new Appearance(creationData.visualId))` 식).
- Stats 시드는 크리에이터가 `md.Tables.TbCharacter.Get(code)`를 인라인 조회해 기존대로 MoveSpeed/JumpPower.
- **양쪽 다 3종 부착(엔티티 형상 대칭).** 클라는 `Appearance`(모델 로드)·`EntityKind`(바인더)를 읽고
  `MasterDataRef`는 부착만(미독) — 서버 relay 대칭 유지용, 무해(문자열 1개). 서버는 뷰 없이 3종 부착 →
  `EntityCreationDataFactory`가 되읽어 스폰 proto 재구성(무변).

### 삭제 (클·서 각각)

`AppearanceComponent` · `CharacterComponent` · `ItemComponent` · `EntityTypeComponent`(총 8파일) + Item
`masterData` 죽은 필드. `LOPComponent`/`MonoComponent`/substrate는 **S3까지 유지**(아직 `PhysicsComponent`가 씀).

## 범위 밖 / 주의

- **와이어 proto 무변경.** `MasterDataRef.Code`는 현재 클라가 안 읽고 서버 릴레이용으로만 존재 — 넉백
  range/angle 같은 향후 드롭 후보지만 S1은 proto·와이어를 안 건드림(별도 슬라이스).
- **`visualId` PropertyChange 재로드 경로 안 만듦**(write-once 확인). 스킨 교체 등 런타임 변경 콘텐츠가
  생기면 그때 이벤트 경로.
- `PhysicsComponent`·보간기·`LOPEntity` substrate는 S2/S3 소관.

## 테스트

- **EditMode (LOP-Shared, Unity 비의존):**
  - 새 3종 컴포넌트 생성자 무결성(필수 값) + `Add`/`Get` 왕복.
  - `EnemyBrain`의 이동 결정이 쓰던 값을 `StatsSystem.GetValue(MoveSpeed)`로 뽑아 등가임을 검증(순수부 추출).
- **인게임(플레이):** 플레이어/적/exp마블 스폰 · 모델 로드(visualId) · AI 이동(Speed) · 넉백 · 경험치 마블
  획득 정상. 클·서 콘솔 컴파일 에러 0.

## 그린 판정

클·서 컴파일 클린 + 위 인게임 시나리오 무변화 + `LOPEntity.components`에 `PhysicsComponent`만 남음.

## 산업 표준 매핑

- `Appearance{VisualId}` — ECS **RenderMesh/Appearance** 데이터 컴포넌트(복제되는 렌더 참조).
- `EntityKind{EntityType}` — ECS **type/kind 값 컴포넌트**(태그 대안).
- `MasterDataRef{Code}` — 설정 테이블 FK(안정 키). `[[masterdata-key-convention]]`.
- 전반: birth 정보를 뷰 MonoBehaviour가 아니라 **엔티티 데이터**로 — DOTS/Entitas의 데이터-컴포넌트 이관.

## Open Decisions (umbrella에서 내려온 것 — 여기서 확정)

- **EntityType → `EntityKind` 값 컴포넌트**(태그 마커 아님, 이름 충돌 회피). ✅ 확정.
- **visualId = 순수 시드**(런타임 변경 없음). ✅ 확정.
