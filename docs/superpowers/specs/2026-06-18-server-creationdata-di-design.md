# 서버 Health 이행 — Slice 1a: EntityCreationDataFactory DI화 (순수 구조 리팩터)

**Date:** 2026-06-18
**Branch (docs):** `feature/server-creationdata-di` (클라 repo — 설계 허브)
**Branch (code):** 서버 repo 피처 브랜치 (구현 시 생성)
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [LOP 저장소 토폴로지](../../lop-repo-topology.md) · [서버 Motion 패리티 spec](2026-06-18-world-core-motion-server-design.md)

## Goal

서버 `EntityCreationDataFactory`를 정적+`Activator.CreateInstance`(reflection 등록) 패턴에서 **DI 인스턴스 팩토리**로 전환한다. 이미 DI인 `EntityFactory`(GameFramework)와 **동형**으로 통일하여, 정적/reflection 잔재와 그에 따른 *룸 재입장 시 stale 참조* 위험을 제거한다.

**동작 100% 동일** — 같은 `EntityCreationData`를 같은 입력에 대해 산출한다. 디스패치 로직(엔티티별 creator 선택)도 그대로이며 *선택 메커니즘만* attribute-reflection → DI로 바뀐다.

## 위치 — 서버 Health 이행 2-슬라이스의 첫 칸

서버 Health 진실원본 이행은 3슬라이스로 분해됐다(패리티 감사 후 결정):
- **Slice 1a (이 spec):** `EntityCreationDataFactory` DI화 — 순수 구조 리팩터. World.Health 무관.
- **Slice 1b:** HP read flip — snapshot(`UserEntitySnapToC`)·스폰데이터(`CharacterCreationDataCreator`)를 `World.Health`에서 읽기(1a가 깔아둔 DI로 creator가 `EntityRegistry` 주입). behavior-preserving(`World.Health == legacy`).
- **Slice 2:** writer flip + 사망 재배치 + 레거시 `HealthComponent` 제거.

1a를 먼저 떼는 이유: 정적 팩토리라 creator가 `[Inject]`로 `EntityRegistry`를 못 받는 게 1b의 걸림돌이다. 1a는 그 걸림돌을 *동작 변화 없이* 제거하는 독립 가치의 리팩터라, "구조 변경"과 "읽기-소스 변경(1b)"을 분리해 검증을 깔끔하게 한다.

## 잠긴 결정 (브레인스토밍 합의)

- **γ(팩토리 DI화) 채택** — α(Create에 EntityRegistry 인자 threading)·β(정적 서비스 로케이터) 기각. 근거: 엔티티 *생성* 쪽은 이미 DI(`IEntityCreator`/`EntityFactory`/`GameLifetimeScope` 등록)인데 생성-*데이터* 쪽만 옛 패턴이라 **불일치 해소**가 본질. `EntityFactory`가 정적/Activator를 버린 명시적 이유(주석: "스코프와 함께 생성·해제되어 룸 재입장 시 stale 참조가 생기지 않는다")가 이 팩토리에도 그대로 적용된다.
- **디스패치 키 = `EntityType`** (enum). Character/Item 둘 다 `LOPEntity`라 C# 타입으로 구분 불가 → 각 creator가 자기 `EntityType`을 선언한다(`EntityFactory`는 제네릭 타입 인자로 키를 잡지만, 여기선 EntityType enum이 분기축).
- **EntityType 선언 = 인터페이스 프로퍼티** (`EntityType EntityType { get; }`) — attribute(`[EntityCreationDataCreatorRegistration]`)+reflection 스캔을 통째로 제거. 선언이 코드(인터페이스 멤버)로 드러나 reflection 불필요.

## Scope (In) — 서버 변경

> 인터페이스/팩토리/creator/attribute 모두 **서버 로컬**(GameFramework 아님) — 변경이 서버에 갇힌다. 호출처는 3곳뿐.

| 파일 (서버) | 변경 |
|---|---|
| `IEntityCreationDataCreator`(비제네릭 인터페이스) | `EntityType EntityType { get; }` 추가 |
| `IEntityCreationDataFactory.cs` | **신규** 인터페이스 — `EntityCreationData Create(IEntity entity)` |
| `EntityCreationDataFactory.cs` | `static` → `public class EntityCreationDataFactory : IEntityCreationDataFactory`. 생성자 `(IEnumerable<IEntityCreationDataCreator> creators)`로 `Dictionary<object, IEntityCreationDataCreator>`를 `creator.EntityType` 키로 구성. 정적 생성자/`RegisterCreators`/`Activator.CreateInstance`/AppDomain reflection 제거. `Create(IEntity)`는 인스턴스 메서드(디스패치 로직 동일). |
| `CharacterCreationDataCreator.cs` | `[EntityCreationDataCreatorRegistration(EntityType.Character)]` 제거 + `public EntityType EntityType => EntityType.Character;` 추가 |
| `ItemCreationDataCreator.cs` | 어트리뷰트 제거 + `public EntityType EntityType => EntityType.Item;` 추가 |
| `EntityCreationDataCreatorRegistrationAttribute.cs` | **삭제**(+ `.meta`) — 미사용화 |
| `Game/GameLifetimeScope.cs` | `builder.Register<IEntityCreationDataFactory, EntityCreationDataFactory>(Singleton)` + `IEntityCreationDataCreator, CharacterCreationDataCreator` + `IEntityCreationDataCreator, ItemCreationDataCreator`(Singleton) 등록 — 기존 `IEntityCreator` 3줄 등록과 동형 |
| `Entity/LOPEntityManager.cs:193` | static `EntityCreationDataFactory.Create(entity)` → 주입된 `[Inject] IEntityCreationDataFactory` 인스턴스 호출 |
| `Game/LOPGame.cs:252, 280` | 동일 — `[Inject] IEntityCreationDataFactory` 추가 후 인스턴스 호출 |

### 팩토리 형태 (EntityFactory 미러)

```csharp
public class EntityCreationDataFactory : IEntityCreationDataFactory
{
    private readonly Dictionary<object, IEntityCreationDataCreator> creators = new();

    // creator는 DI가 생성·주입(IEnumerable). 정적 캐시/Activator 없음 → 스코프 생명주기 동행.
    public EntityCreationDataFactory(IEnumerable<IEntityCreationDataCreator> creators)
    {
        foreach (var creator in creators.OrEmpty())
        {
            this.creators[creator.EntityType] = creator;
        }
    }

    public EntityCreationData Create(IEntity entity)
    {
        if (entity.TryGetEntityComponent<EntityTypeComponent>(out var typeComp) == false)
            throw new InvalidOperationException($"Entity '{entity.entityId}' has no EntityTypeComponent.");

        if (creators.TryGetValue(typeComp.entityType, out var creator) == false)
            throw new InvalidOperationException($"No registered creation-data creator for '{typeComp.entityType}'.");

        return creator.Create(entity);
    }
}
```

> `EntityType` 키 타입: 현 attribute는 `object type`(boxed EntityType)으로 dict 키가 `object`였다. 인터페이스 프로퍼티 타입을 `EntityType`(enum)로 두고 dict 키도 `EntityType`로 강타입화하는 게 깔끔(아래 Open Decision).

## 데이터 흐름 (리팩터 후)

```
GameLifetimeScope: IEntityCreationDataCreator 2종 + IEntityCreationDataFactory 등록
   │ (DI가 creator 생성·주입)
EntityCreationDataFactory(ctor): creator.EntityType로 dict 구성
   │
호출처(LOPEntityManager / LOPGame) → [Inject] IEntityCreationDataFactory.Create(entity)
   → entityType으로 디스패치 → creator.Create(entity) → EntityCreationData
```

이전(정적 + AppDomain reflection 스캔 + Activator)과 **출력·디스패치 결과 동일**. 차이는 *인스턴스 생성·생명주기*뿐(스코프 동행 → stale 제거).

## Out of scope (defer)

- **HP read를 World.Health로** = Slice 1b. 이번엔 creator 내부 읽기(legacy `HealthComponent`)를 *그대로 둔다* — DI 배선만 바꾸고 읽는 값/소스는 불변.
- writer flip / 사망 재배치 / 레거시 제거 = Slice 2.
- 클라 측 대응물 — 클라는 생성-데이터를 *수신*만 하지 *생산* 안 함(이 팩토리는 서버 전용). 클라 변경 없음.

## 조사 근거 (구현 사실)

- `EntityCreationDataFactory`(서버)는 정적 클래스 + 정적 생성자에서 `AppDomain.CurrentDomain.GetAssemblies()` 스캔 + `Activator.CreateInstance(type)`로 creator 인스턴스화 + `[EntityCreationDataCreatorRegistration(EntityType.X)]`로 등록. `Create(IEntity)`는 `entityType`으로 디스패치.
- `EntityFactory`(GameFramework)는 이미 DI: 생성자 `(IEnumerable<IEntityCreator>)`, 제네릭 타입 인자로 dict 키 구성, `GameLifetimeScope`가 `IEntityCreator` 구현체 + `IEntityFactory` 등록. **이 패턴을 그대로 미러**.
- creators 2종(`CharacterCreationDataCreator`/`ItemCreationDataCreator`)은 동형 — `IEntityCreationDataCreator<LOPEntity>` + `Create(LOPEntity)`/`Create(IEntity)` 보유, attribute로 EntityType 선언. 둘 다 `LOPEntity`(타입 동일, EntityType만 다름).
- `EntityCreationDataFactory.Create` 호출처 = 3곳: `LOPEntityManager.cs:193`(GetAllEntityCreationDatas — 늦은 접속용 전체 생성데이터), `LOPGame.cs:252`(SpawnEnemy), `LOPGame.cs:280`(SpawnExpMarble). `LOPGame`/`LOPEntityManager` 모두 DI 대상이라 `[Inject]` 가능.
- 인터페이스/attribute는 GameFramework에 없음(서버 로컬) → 변경 서버 한정.

## 검증

- **컴파일**: 서버 0에러 (UnityMCP `refresh_unity` + `read_console`, **서버 인스턴스** 핀 — 사용자 지시 서버 작업). GameFramework 불변이라 클라 영향 0.
- **런타임(수동)**: 서버 플레이 — (a) 적/플레이어 스폰 시 클라가 생성데이터 정상 수신(캐릭터 외형/HP 표시), (b) 경험치구슬(Item) 스폰 정상, (c) **늦은 접속** 클라가 기존 전체 엔티티 정상 수신(`GetAllEntityCreationDatas`), (d) `Registered Creator: ...` 정적 로그는 사라지고 DI 등록으로 대체되며 콘솔 에러 0. **출력은 이전과 동일**(회귀 0)이 성공.
- 자동화 테스트 신규 없음(단일 Assembly-CSharp).

## 문서/브랜치 정책

선례대로 **spec·plan은 클라 repo**(설계 허브) 피처 브랜치 `feature/server-creationdata-di`, **코드는 서버 repo** 피처 브랜치. 이 spec은 `CLAUDE.md` `@` 자동 로드 목록에 추가.

## Open Decisions (plan에서 해소)

- [ ] **dict 키 타입**: `object`(현 attribute 호환) vs `EntityType`(강타입). 강타입 권장 — 인터페이스 프로퍼티를 `EntityType`로 두면 자연. plan에서 `EntityTypeComponent.entityType` 타입과 맞춰 확정.
- [ ] **`IEntityCreationDataFactory.Create` 시그니처**: 비제네릭 `Create(IEntity)`만(현 사용 충족). 제네릭 오버로드는 YAGNI — 추가 안 함.

## 진행

- [x] 패리티 감사 + 3슬라이스 분해(1a/1b/2) + γ(DI화) 합의 + EntityFactory 패턴 확인
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] `writing-plans`로 구현 plan
- [ ] 구현(서버 repo) → 검증 → 머지 → Slice 1b

> 후속: Slice 1b(HP read flip → World.Health), Slice 2(writer flip + 사망 재배치 + 레거시 제거).
