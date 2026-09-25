# 엔티티 매니저 → ActorRegistry + EntitySpawner 분리 설계

엔티티 리팩터(S1~S5) 후속. 지금의 `LOPEntityManager`는 **예전 뚱뚱한 엔티티 매니저 시절의 이름·구조**를 그대로 이고 있다.
데이터/로직은 이미 `World.Entity`(순수 C#, `EntityRegistry`)로 빠졌는데, 매니저에는 **두 가지 서로 다른 책임**이
한 클래스에 섞여 남아 있다:

1. **actor(뷰 게임오브젝트) 명부** — `id → LOPActor` 조회 인덱스
2. **출생·사망 조율** — 생성(`CreateEntity`)/제거(`DeleteEntityById`/`DestroyMarkedEntities`)

이 둘을 **표준(리액티브 뷰 리졸버)** 대로 갈라, "데이터가 대장 / 뷰는 반응 팔로워" 구조로 수렴한다.

> 관련: `docs/world-core-connection-architecture.md`("분리형 뷰 스포너"), `docs/ROADMAP.md`(엔티티 Unity 레이어 재구조화
> 트랙의 "의도적 보류 — entityMap→스포너 이전 / IEntityManager 규격 해체"), `[[entity-unity-layer-rearchitecture]]`.

---

## 1. 문제 (현재 코드)

`LOPEntityManager`(클·서 각자)가 하는 일 — 데이터(entity)와 뷰(actor)를 **한 손으로** 다룬다:

```
CreateEntity<T,TData>(data):
  1. 데이터 조립 + entityRegistry.Add   ← entity  (실제로는 creator 안에서 이미 함)
  2. actor 게임오브젝트/맵 등록           ← actor  ⚠️ 섞임
  3. EntityCreated(actor) 방송

DestroyMarkedEntities():
  1. actor cleanup + GameObject 파괴      ← actor  ⚠️ 섞임
  2. entityRegistry.Remove                ← entity
  3. actor 맵에서 삭제                     ← actor  ⚠️ 섞임
  4. (서버) despawn 와이어 송신
```

부수 증상:
- 매니저가 클·서 공유 인터페이스 `GameFramework.IEntityManager`(제네릭 `where T : MonoBehaviour`)를 구현하는데,
  절반이 안 쓰이거나 `throw new NotImplementedException`(클라)이다.
- 예전 "엔티티=로직+뷰 한 덩어리" 시절 어휘가 남아 실제 역할(뷰 앵커 관리)과 어긋난다.

---

## 2. 목표

**데이터 수명(entity)과 뷰 수명(actor)을 두 축으로 분리**한다. 출생·사망 조율은 **데이터만** 만지고,
actor는 "누가 태어났다/죽었다" 신호를 듣고 **뷰 담당이 반응**해 만들고 치운다.

```
 [데이터 축 — 대장]                          [뷰 축 — 반응 팔로워]
 EntitySpawner ──Add──► EntityRegistry        EntityBinder ──Add──► ActorRegistry
   │  (id→World.Entity)                          │  (id→LOPActor)
   ├─ Spawn:  데이터 생성·등록 → "태어났다(id)" 방송 ─────────► actor 생성 + 뷰 부착 + ActorRegistry.Add
   └─ Despawn: 등록 해제 → "죽었다(id)" 방송 ────────────────► cleanup + actor 파괴 + ActorRegistry.Remove
```

핵심 불변식: **`EntitySpawner`는 `LOPActor`/`ActorRegistry`를 한 번도 참조하지 않는다.** actor를 만지는 코드는
전부 `EntityBinder` 한 곳에 모인다.

---

## 3. 산업 표준 매핑

새 발명이 아니라 검증된 조립이다.

- **Entitas(Unity ECS)**: 엔티티(데이터) 수명을 관찰하는 **리액티브 뷰 시스템**이 GameObject(뷰)를 생성/파괴.
  데이터가 진실원본, 뷰는 반응 프로젝션.
- **Unity DOTS — Companion GameObject**: 엔티티가 진실원본이고 컴패니언 GameObject가 엔티티를 따라감.
- **오버워치식 ECS**: 엔티티=데이터, 렌더는 엔티티 상태를 읽는 별개 시스템.
- **CQRS/이벤트 소싱**: 상태 변경(mutation)과 프로젝션(뷰 갱신)의 분리.

LOP 매핑: `EntitySpawner`(+`EntityRegistry`) = 데이터 수명 / `EntityBinder`(+`ActorRegistry`) = 리액티브 뷰 프로젝션.
`world-core-connection-architecture.md`가 이미 이 역할을 "분리형 뷰 스포너 / view-resolver"라 부른다(*MVP 프레젠터 아님* — 그 어휘는 피한다).

---

## 4. 컴포넌트 구조

| 역할 | 이름 | 위치 | 성격 |
|---|---|---|---|
| 데이터 명부 (기존) | `EntityRegistry` | GameFramework | 순수 C# 저장소 (변경 없음) |
| **뷰 명부 (신규)** | **`ActorRegistry`** | 클·서 각자 (Assembly-CSharp) | dumb 인덱스 |
| **데이터 수명 (구 LOPEntityManager)** | **`EntitySpawner`** | 클·서 각자 | Spawn/Despawn + 방송, 데이터만 |
| **뷰 수명 (구 클 EntityBinder / 서 EntityViewSpawner)** | **`EntityBinder`** | 클·서 통일 | 리액티브 뷰 생성/파괴 |
| 옛 제네릭 인터페이스 3종 | **삭제** | GameFramework | (아래 §7) |

### 4.1 `ActorRegistry` (신규, 순수 인덱스)

`EntityRegistry`를 미러링한다. 로직·조율 없음. `EntityBinder`가 채우고 비운다. 소비자는 `id → LOPActor`가 필요할 때
여기를 조회한다(예: 서버 스냅 수신 → `RemoteEntityInterpolator`에 먹이기).

```csharp
public class ActorRegistry
{
    public void Add(LOPActor actor);           // 키 = actor.entityId
    public bool Remove(string entityId);
    public LOPActor Get(string entityId);      // 없으면 null
    public bool TryGet(string entityId, out LOPActor actor);
    public bool Contains(string entityId);
    public int Count { get; }
    public IEnumerable<LOPActor> All { get; }
}
```

> **왜 이 인덱스가 필요한가(죽은 코드 아님):** `EntityRegistry`는 순수 데이터라 Unity GameObject를 모른다. 서버 스냅이
> 도착했을 때 "이 id의 GameObject/컴포넌트를 찾아라"(원격 보간기 먹이기)와 디스폰 시 "이 id의 GameObject를 파괴하라"에
> `id → LOPActor` 인덱스가 반드시 하나 필요하다.

### 4.2 `EntitySpawner` (구 LOPEntityManager, 데이터 전용)

`EntityRegistry` + 이벤트 발행만 참조한다. **`LOPActor`/`ActorRegistry` 미참조.** 옛 `IEntityFactory`의 타입별
디스패치(생성데이터 → 데이터 creator)를 흡수한다. 지연 파괴 목록(`entitiesToDestroy`)을 보유하고 틱 끝에 flush한다.

```csharp
public class EntitySpawner   // 클라
{
    public void Spawn<TCreationData>(TCreationData creationData)   // 반환 없음
        where TCreationData : struct, IEntityCreationData;         //  → 데이터 creator 디스패치 → registry.Add → EntityCreated(id) 방송
    public void Despawn(string entityId);                          //  → "죽을 목록"에 마킹
    public void FlushDespawns();                                   //  → registry.Remove + EntityDestroyed(id) 방송 (LOPRunner가 틱 끝에 호출)
}
```

- 서버는 여기에 **데이터 사이드 부가**를 더한다(§6): `GenerateEntityId`, userId↔entityId 매핑, despawn 와이어 송신.
- 데이터 creator(`CharacterCreator`/`ItemCreator`)는 **순수 데이터로 축소** — `World.Entity` 조립 + `entityRegistry.Add` +
  어빌리티 Grant만. **actor 앵커 GameObject 생성(현 `CharacterCreator` 말미)은 `EntityBinder`로 이동.**

### 4.3 `EntityBinder` (구 클 EntityBinder + 서 EntityViewSpawner, 클·서 통일 이름)

`EntityCreated`/`EntityDestroyed`를 구독하는 리액티브 뷰 스포너.

- **`EntityCreated(id)`**: `Actor_{id}` GameObject 생성 + `LOPActor` + (사이드별) 뷰 컴포넌트 부착 + `ActorRegistry.Add`.
  - 클라: `PhysicsFollower`+`PhysicsBody`, `LOPEntityView`, 보간기(로컬/원격), 캐릭터 장식(nameplate/floater), `playerContext.actor`.
  - 서버: `PhysicsFollower`+`PhysicsBody`, 테스트렌더 뷰, 비-플레이어에 `LOPAIController`.
- **`EntityDestroyed(id)`**: `ActorRegistry`에서 actor 조회 → cleanup(`ICleanup`) → GameObject 파괴 → `ActorRegistry.Remove`.
  - 지금은 파괴가 매니저(`DestroyMarkedEntities`)에 있다. 이걸 `EntityDestroyed` 반응으로 옮긴다.

> 사이드별 내용(뷰 컴포넌트 종류)은 다르지만 **역할은 동일**하므로 이름을 통일한다(`LOPGameEngine` 등 host가
> per-side 본문을 갖되 이름은 공유하는 것과 동형).

---

## 5. 흐름 (before → after)

### 5.1 생성

**Before**: 메시지핸들러 → `entityManager.CreateEntity<LOPActor, XData>(data)` → factory → creator(데이터+**앵커**) →
매니저가 actor맵 등록 + `EntityCreated(actor)` 방송 → Binder가 기존 actor에 뷰 컴포넌트 부착.

**After**: 메시지핸들러 → `entitySpawner.Spawn(data)` → data creator(데이터만) + `registry.Add` → `EntityCreated(id)` 방송
→ **Binder가 actor GameObject + 뷰 통째로 생성** + `ActorRegistry.Add`.

### 5.2 파괴

**Before**: 메시지핸들러 → `entityManager.DeleteEntityById(id)`(마킹) → LOPRunner가 틱 끝에 `DestroyMarkedEntities()`
→ 매니저가 actor cleanup+파괴 + `registry.Remove` + actor맵 삭제 (+서버 despawn 송신).

**After**: 메시지핸들러 → `entitySpawner.Despawn(id)`(마킹) → LOPRunner가 틱 끝에 `entitySpawner.FlushDespawns()`
→ `registry.Remove` + `EntityDestroyed(id)` 방송 (+서버 despawn 송신) → **Binder가 반응해 actor cleanup+파괴 + `ActorRegistry.Remove`.**

> **동기 발행 전제(안전):** MessagePipe는 동기다(메모리 `[[messagepipe-migration]]`). 따라서 `Spawn`이 반환하기 전에
> Binder가 actor+`PhysicsBody`를 완결하고, `FlushDespawns` 반환 전에 Binder가 actor를 파괴한다 — 반환 계약·물리 틱 공백 없음.
> 단 `EntityCreated`의 여러 구독자 중 **actor를 조회하는 구독자(현재 없음)** 가 생기면 Binder가 먼저 반응해야 하므로,
> Binder 구독을 **가장 먼저 등록**한다(§11 리스크).

---

## 6. 이벤트 페이로드 변경

`EntityCreated`가 실어나르는 것을 **`LOPActor` → `entityId`(string)** 로 바꾼다. 발행 시점(데이터 등록 직후)엔 actor가
아직 없기 때문이다. `EntityDestroyed`는 이미 `entityId`라 변경 없음.

```csharp
public struct EntityCreated { public string entityId; }   // was: LOPActor actor
public struct EntityDestroyed { public string entityId; }  // 변경 없음
```

**소비자 영향(전수 확인됨):**
- 클 `EntityBinder`(구): `entityCreated.actor` → `entityCreated.entityId`로 시작, `entityRegistry.Get(id)`로 데이터 접근.
- 클 `PlayerHudCoordinator`: `actor.entityId` 비교만 → `entityCreated.entityId` 직접 사용(더 단순해짐).
- 서 `EntityViewSpawner`(구, →`EntityBinder`): 클라와 동형 수정.
- `EntityDestroyed` 구독자는 현재 **없음**(발행만 됨) → `EntityBinder`가 첫 소비자가 되어 파괴 트리거로 쓴다.

---

## 7. 삭제 대상 — GameFramework 옛 인터페이스 3종

`where T : MonoBehaviour` 제네릭 껍데기로, 뚱뚱한 시절 추상. 지금은 LOP만 쓰고 실익이 없어 삭제하고 콘크리트로 대체한다.

- **`IEntityManager`** → `ActorRegistry`(조회) + `EntitySpawner`(수명)로 분해 대체.
- **`IEntityFactory`** → `EntitySpawner`의 타입별 데이터 creator 디스패치로 흡수. (콘크리트 `EntityFactory`가 있으면 함께 은퇴.)
- **`IEntityCreator` / `IEntityCreator<TEntity, TCreationData>`** → **데이터 전용 LOP creator**로 대체. creator는 이제
  `World.Entity`를 조립·등록(반환 `LOPActor` 아님). 디스패치용 최소 LOP-side 계약(작은 인터페이스 또는 타입→creator 맵)은
  플랜에서 확정 — GameFramework의 제네릭 3종은 사라진다.

DI 등록도 `IEntityManager`/`IEntityFactory`/`IEntityCreator` 바인딩을 `EntitySpawner` + `ActorRegistry` + 데이터 creator로 교체.

---

## 8. 마이그레이션 매핑 (멤버별 행선지)

| 현재 (`LOPEntityManager`) | 행선지 | 비고 |
|---|---|---|
| `CreateEntity<T,TData>` | `EntitySpawner.Spawn(data)` | 제네릭·반환 제거, actor 안 만듦 |
| `DeleteEntityById` | `EntitySpawner.Despawn` | 마킹 |
| `DestroyMarkedEntities` | `EntitySpawner.FlushDespawns` (데이터) + `EntityBinder`(actor 파괴) | 조율/뷰 분리 |
| `GetEntity` / `GetEntity<T>` | `ActorRegistry.Get` | |
| `TryGetEntity` / `TryGetEntity<T>` | `ActorRegistry.TryGet` | |
| `GetEntities` / `GetEntities<T>` | `ActorRegistry.All` (또는 데이터 순회는 `entityRegistry.All`) | |
| `UpdateEntities` (빈 본문) | **삭제** | LOPRunner 호출부도 제거 |
| `entityMap` | `ActorRegistry` 내부 | |
| (서버) `GenerateEntityId` | `EntitySpawner`(서버) | id 할당 = 스폰 관심사 |
| (서버) `userEntityMap` / `GetEntityIdByUserId` / `GetEntityByUserId` | `EntitySpawner`(서버) | userId↔entityId 인덱스, 스폰 수명 결합 |
| (서버) `GetUserIdByEntityId` | `entityRegistry.Get(id).Get<Ownership>().OwnerId` | 맵 불필요(데이터 파생) |
| (서버) `GetAllEntitySnaps` / `GetAllEntityCreationDatas` | 서버 브로드캐스트 측 + `entityRegistry.All` 순회 | actor 미사용(id만 쓰던 것) — 정확 행선지는 플랜 |

---

## 9. 클·서 차이 (의도된 비대칭)

- `ActorRegistry` / `EntitySpawner` / `EntityBinder`는 **클·서 각자 콘크리트**다(현 `LOPEntityManager`가 이미 per-side).
  `LOPActor`가 사이드별 타입이라 `ActorRegistry`도 사이드별. 슬림한 중복(수십 줄)은 현 패턴과 일관 — 제네릭 공유화는 하지 않는다(YAGNI).
- 데이터 수명 정책이 사이드별로 다르다: 서버=권위(id 생성·despawn 송신·userId 매핑), 클라=서버 스폰/디스폰 수신. `EntitySpawner`가 이 차이를 담는다.

---

## 10. `LOPActor` — 클·서 각자 유지 (unify 안 함)

두 파일을 비교하면 **공통 뼈대는 `entityId` + `Initialize` 4줄뿐**이고, 클라만 `view`/`visualGameObject`/`SetView`를
가진다 — **클라 전용 렌더 타입 `LOPEntityView`에 묶인 정당한 diverge**(서버는 프로덕션 렌더 없음). 공유 베이스를
빼는 이득 < 비용(4줄 공유 + MonoBehaviour의 스코프 배치 문제: LOP-Shared=시뮬 스코프라 뷰 앵커 부적합)이라
**각자 유지**한다. (별도 파킹 후보로 남길 수 있으나 사실상 영구 YAGNI.)

---

## 11. 리스크 & 대응

- **구독 순서**: `EntityCreated`의 여러 구독자 중 actor를 조회하는 게 생기면 Binder가 먼저 반응해야 함 → **Binder 구독을
  가장 먼저 등록**(엔트리포인트 등록 순서). 현재 다른 구독자(`PlayerHudCoordinator`)는 id만 쓰므로 순서 무관.
- **스폰 트랜스폼 배치**: S4에서 확인된 대로 actor 루트가 움직이는 시뮬 바디라, Binder가 actor 생성 직후 `PhysicsFollower`가
  스폰 위치로 즉시 배치해야 함(원점→스폰 점프 방지). 현 코드 동작 보존.
- **`PhysicsBody` 시점**: 동기 발행이라 `Spawn` 반환 전 `PhysicsBody` 부착 완료 — 물리 틱 공백 없음(현 S5b 계약 유지).
- **서버 despawn 와이어**: 현재 `DestroyMarkedEntities`가 despawn 송신 → `FlushDespawns`로 그대로 이동(데이터 사이드).

---

## 12. 범위 / 비범위

**범위**: `LOPEntityManager` → `ActorRegistry`+`EntitySpawner` 분해, 클 `EntityBinder`/서 `EntityViewSpawner`를 `EntityBinder`로
통일 + actor 생성/파괴 흡수, 데이터 creator를 순수 데이터로 축소, `EntityCreated` 페이로드 flip, GameFramework 3 인터페이스 삭제,
DI 재배선. 클·서 양쪽.

**비범위**: `LOPActor` 공유 베이스 추출(§10), `PhysicsBody` 순수 포트화(`[[physicsbody-port-purity-deferred]]`),
`EntityRegistry` 자체 변경, 뷰 컴포넌트 내부 로직 변경. 동작 무변화(값 동치) 리팩터 — 게임플레이 변화 없음.

---

## 13. 테스트 & 검증

- **EditMode**: `ActorRegistry`(순수 인덱스 Add/Remove/Get/TryGet/Contains/All) 단위 테스트. `EntitySpawner`의 데이터
  수명(Spawn→registry.Add+방송 / Despawn→마킹 / FlushDespawns→registry.Remove+방송)은 순수 로직이라 테스트 가능.
- **컴파일**: 클·서 UnityMCP 클린.
- **인게임 스모크**(사용자): 스폰(캐릭·아이템·AI), 원격 보간, 디스폰, 아이템 줍기, 넉백/데미지, 롤백 — 현 동작과 동일해야 함.
- **최종 whole-branch 리뷰**: 값 동치 사이트 단위 확인(actor 처리가 Binder로 온전히 이동했는지, EntitySpawner가 actor 미참조인지).

---

## 14. Open questions (플랜에서 확정)

- 데이터 creator 디스패치의 정확한 형태(작은 LOP 인터페이스 vs 타입→creator 맵) — `IEntityFactory` 삭제 후 대체.
- 서버 `GetAllEntitySnaps`/`GetAllEntityCreationDatas`의 최종 행선지(서버 브로드캐스트 측 vs `EntitySpawner` 헬퍼).
- `userEntityMap`을 `EntitySpawner`에 둘지 별도 `UserEntityIndex`로 뺄지(지금은 스폰 수명 결합이라 `EntitySpawner` 기본).
- `EntityFactory` 콘크리트가 실제 존재하는지 확인 후 은퇴 처리.
