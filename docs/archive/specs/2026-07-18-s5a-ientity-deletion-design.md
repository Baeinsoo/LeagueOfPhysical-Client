# S5a — `IEntity` 삭제 + 모션 접근자 통일 (설계)

> 엔티티 Unity 레이어 재구조화 umbrella(`2026-07-18-entity-view-rearchitecture-umbrella-design.md`)의
> S5를 **S5a / S5b**로 쪼갠 앞 절반. 상태(어디까지 왔나)는 `docs/ROADMAP.md`가 단일 원천.

## 왜 (문제)

S4까지 오면서 엔티티 Unity 트리는 표준(Actor 앵커 루트 + 얇은 뷰/팔로워)으로 수렴했다. 그런데
World Core 이행의 **미완결분** 하나가 남아 있다: `LOPActor`가 아직 GameFramework의 얇은 파사드
인터페이스 `IEntity`를 이고 있고, 그 인터페이스가 **모션(pos/rot/vel)을 유니티 타입으로 노출**한다.

- `IEntity`(4멤버: `entityId` + `position`/`rotation`/`velocity`)는 `LOPActor` 하나만 구현한다.
- 그런데 이 인터페이스 제약(`where TEntity : IEntity`)이 GameFramework의 **엔티티 추상 패밀리
  5종**(`IEntity`·`IEntityManager`·`IEntityFactory`·`IEntityCreator`·`EntityFactory`)에 박혀 있어,
  소비처가 콘크리트 `LOPActor`가 아니라 파사드를 통해 엔티티를 본다.
- `LOPActor.position/rotation/velocity`는 **상태를 저장하지 않는 순수 프록시**다 — `World.Transform`/
  `World.Velocity`(진실원본)로 값을 넘길 뿐. 즉 "그림자 상태"가 아니라 **유니티 Vector3 ↔ 순수숫자
  변환을 한곳에 모은 접근자**다. (진짜 그림자 필드는 S3에서 지운 옛 `MonoEntity`가 갖던 것.)

**진단:** `IEntity` *인터페이스*는 지울 명확한 이득이 있다(불필요한 GameFramework 추상 결합 제거 +
콘크리트 타입으로 직결). 하지만 그 모션 접근자가 하던 **변환+동등성 가드는 여전히 필요**하다 —
`World.Transform`이 `System.Numerics.Vector3`(noEngineReferences 코어)라 유니티 코드가 매번 변환해야
하기 때문이다. 그래서 접근자를 *없애는* 게 아니라 **인터페이스에서 떼어 공유 익스텐션 한 벌로 이전**한다.

## 목표 (한 줄)

`IEntity` 파사드 인터페이스를 삭제하고 소비처를 콘크리트 `LOPActor`로 재타입한다. 모션의 유니티↔순수숫자
변환은 **GameFramework 익스텐션 한 벌**(클·서 공유)로 통일하고, `LOPActor`는 **순수 신원 앵커**(entityId만)로
얇아진다. `World.Entity` = 유일한 데이터/시뮬 진실원본.

## 산업 표준 매핑

- **Companion GameObject(DOTS) / View(Entitas)** — 엔티티 데이터(`World.Entity`)와 뷰(GameObject)를
  단방향 링크로 분리, 뷰는 얇음. `LOPActor`를 신원 앵커로만 남기는 것이 이 표준의 완성.
- **모션 접근자 = 경계 변환 헬퍼** — 순수 코어(numerics)와 엔진(Unity) 경계에서의 값 변환은 엔진 참조
  어셈블리에 한 곳으로 모으는 게 표준(중복 변환·가드 산재 방지). GameFramework가 이미 `ToUnity`/
  `ToNumerics`/`Vector3EqualityComparer`를 그 자리에 두고 있어, 모션 접근자도 같은 자리에 둔다.
- **인터페이스는 사이드/구현이 *달라야* 할 때만**(프로젝트 컨벤션) — `IEntity`는 구현이 `LOPActor`
  하나뿐이라 seam이 불필요. 삭제해 콘크리트로 직결.

## 설계

### 1) 모션 접근자 익스텐션 (GameFramework, 클·서 공유 1벌)

`ToUnity`/`ToNumerics`(`Numerics.Extensions.cs`)·`Vector3EqualityComparer`(`Property.Extensions.cs`)가
이미 GameFramework(UnityEngine 참조)에 있다. 같은 어셈블리에 `World.Entity` 위 유니티-Vector3 접근자를
둔다:

```csharp
// GameFramework — 클·서 공유 1벌
public static Vector3 GetPosition(this World.Entity e) => e.Get<World.Transform>().Position.ToUnity();
public static void    SetPosition(this World.Entity e, Vector3 v)
{
    var t = e.Get<World.Transform>();
    if (Vector3EqualityComparer.instance.Equals(t.Position.ToUnity(), v)) return;   // 불필요 쓰기 스킵
    t.Position = v.ToNumerics();
}
// GetRotation/SetRotation (euler Vector3 ↔ Quaternion), GetVelocity/SetVelocity — 동일 패턴
```

- 변환 + 동등성 가드가 **이 한 곳**에 모인다(현 파사드가 6개 사이트에 제공하던 것과 동치).
- 사이드 분기 없음 — 클·서가 같은 구체 코드를 공유(결정론 컨벤션과 정합).
- 정확한 배치(신규 static 클래스 파일명/네임스페이스)는 plan에서. 후보: `World.EntityMotionExtensions`
  (GameFramework, `namespace GameFramework.World`).

### 2) `LOPActor` = 순수 신원 앵커

- **삭제:** `IEntity` 구현, `position`/`rotation`/`velocity` 프로퍼티, `LinkWorldMotion`,
  `worldTransform`/`worldVelocity` 필드.
- **남김:** `entityId`, `Initialize<TCreationData>(creationData)`(entityId 세팅).
- **`IsGrounded()` 이전(클라 전용):** 현재 `LOPActor.IsGrounded`가 파사드 `position`에 의존
  (`LOPEntityView`의 animator "Run" bool 판정에서만 호출). 접지 체크(`Physics.OverlapSphere`로
  "Plane" 탐지)를 **`LOPEntityView`로 이전** — 뷰가 `worldEntity.GetPosition()`으로 위치를 얻어
  직접 판정하거나 위치를 받는 작은 static 헬퍼로. `LOPActor`에서 제거. (마킹된 `// TODO 고도화`
  코드라 이동 부담 낮음. 접지 로직 고도화 자체는 범위 밖.)

결과 `LOPActor`: `MonoBehaviour` + `entityId` + `Initialize`. 모션은 어디에도 저장/프록시하지 않는다.

### 3) `IEntity` 삭제 + 소비처 재배선 (전수)

**GameFramework (클·서 원자적 — 공유 패키지):**
- `IEntity.cs` **삭제**.
- `IEntityManager`·`IEntityFactory`·`IEntityCreator`·`EntityFactory`의 `where TEntity : IEntity`
  → **`where TEntity : MonoBehaviour`**. 비제네릭 `IEntity` 반환/파라미터(`IEntityManager.GetEntity`/
  `GetEntities`/`TryGetEntity`) → `MonoBehaviour`.

**모션을 파사드로 읽/쓰던 6+ 사이트 → 익스텐션 + `World.Entity`로:**

| 사이트 | 현재 | 후 | 비고 |
|---|---|---|---|
| `RemoteEntityInterpolator.Apply` (클) | `entity.position=` | `worldEntity.SetPosition(...)` | 크리에이터가 `worldEntity` 참조 세팅(이미 `.entity`/`.entityView` 세팅 중 → 한 줄 추가) |
| `Reconciler` (클) | `entity.position/rotation/velocity=` | `worldEntity.SetPosition/Rotation/Velocity` | 이미 `worldEntity` 손에 있음(line 60) |
| `EnemyBrain` (서, AI) | `entity.position/velocity/rotation` | `registry.Get(id).GetPosition()/SetVelocity()` | 이미 `entityRegistry` 주입 |
| `GetAllEntitySnaps` (서) | `IEntity.position/…` | `registry.Get(id).Get*()` | 매니저에 `entityRegistry` 있음. `GetEntities<LOPActor>()`로 전환 |
| `LOPEntityView` (클·서) | `entity.position/rotation` | `worldEntity.GetPosition()/GetRotation()` | 뷰가 `worldEntity` 참조 보유(SetEntity 시) |
| `Character/ItemCreationDataCreator` (서) | `Create(IEntity)` 다운캐스트 | `Create(LOPActor)` 직접 + 모션은 registry | 이미 `Create(LOPActor)` 존재 |

**타입만 `IEntity`→`LOPActor` (모션 무관):**
- `EntityCreated.entity` (클 `Event.Entity.cs`, 서 `Event.Entity.cs`) → `LOPActor`. 소비처 EntityBinder/
  PlayerHudCoordinator(클) 재타입.
- `IBrain.Think(IEntity)` 오버로드 **제거** → 제네릭 `IBrain<LOPActor>.Think(LOPActor)`만. 호출부
  (AIController)가 제네릭 경로 사용.
- 서버 `IEntityCreationDataFactory.Create(IEntity)`/`IEntityCreationDataCreator.Create(IEntity)` →
  `Create(LOPActor)`, `where TEntity : IEntity` → `MonoBehaviour`.

### 4) `IEntityManager` = 유지 + 재타입 (S5a 최소)

`RunnerBase`(GameFramework)가 `GetComponent<IEntityManager>()`로 매니저를 잡아 보관·노출한다. S5a는
**인터페이스를 유지**하되 `where TEntity : IEntity` → `where TEntity : MonoBehaviour`, 비제네릭
`IEntity` 반환 → `MonoBehaviour`로 재타입만 한다. **RunnerBase 무변.**

> "이 매니저 규격(뷰-인덱스 매니저)이 GameFramework에 존재할 가치가 있나 / 뷰-스포너로 합칠까"라는
> 깊은 논의는 **S5b**(매니저/스포너 재작업)로 미룬다. S5a는 `IEntity` 삭제의 최소 파장에 집중.

### 5) `entityMap` 재규정 (가벼움)

`Dictionary<string, IEntity>` → `Dictionary<string, LOPActor>`(클·서). 역할을 **"id → 뷰 앵커 인덱스"**
(World `EntityRegistry` = id → 데이터 진실원본과 **별개 축**)로 주석/명명 명확화. add(크리에이터가 World
`EntityRegistry.Add` / 매니저가 뷰 인덱스에 add)·remove(단일 `DestroyMarkedEntities`) 관계 확인.

> **완전 제거는 불가·범위 밖** — `World.Entity`는 GameObject를 안 들어서 id→뷰 조회 인덱스가 반드시
> 필요. 이 뷰-인덱스를 매니저에서 뷰-스포너/바인더로 옮기는 재배치는 **S5b**.

### 6) 어휘 수렴 (`entity`→`actor`) — S5a 밖, 별도 기계적 패스

S5a는 **의미 변경(IEntity 삭제 + 모션 이전)만** 담는다. `entity`/`entities`/`LOPEntities` 식별자
(예: `playerContext.entity`, `interpolator.entity`, `LOPRunner.LOPEntities`)의 `actor` 어휘 rename은
**분리된 순수-rename 커밋/패스**로 한다 — 의미 변경이 rename 노이즈에 묻히지 않게(리뷰 가독). S5a
마지막 단계나 S5b에서 수행.

## 범위

- **포함:** GameFramework(엔티티 추상 패밀리 재타입 + 모션 익스텐션) + 클라 + 서버(소비처 재배선).
  GameFramework 변경이라 클·서 **원자적**(동시 컴파일 통과 필요).
- **제외 (S5b/이후):** 매니저-규격 존재 재고·뷰-인덱스 재배치, 거대 `CharacterCreator` 데이터/뷰 분해,
  `entity`→`actor` rename, 접지 로직 고도화, 권위 핸드오프.

## 테스트 / 그린 판정

- **EditMode(GameFramework):** 신규 모션 익스텐션(Get/Set × pos/rot/vel, 동등성 가드로 no-op 스킵)
  단위 테스트 추가. 기존 154 + 신규 유지 green.
- **컴파일:** GameFramework·클·서 클린(UnityMCP `read_console`로 확인, 클라 인스턴스 명시 타깃팅).
- **인게임(사용자):** 스폰·이동·충돌·아이템 줍기·넉백/데미지·롤백 reconcile·원격 보간·서버 테스트렌더가
  **무변화**. (파사드 → 익스텐션은 값 동치 리팩터라 동작 불변이 기대치.)
- **클·서 비대칭 유지:** 클=풀 뷰(interpolator/nameplate/floater), 서=물리/스냅.

## 위험 & 완화

- **GameFramework 원자 변경** — `IEntity` 삭제는 클·서 동시 컴파일 깨짐. strangler 불가(인터페이스
  삭제는 원자). → 3레포를 한 슬라이스로 묶고, GameFramework부터 재타입 후 양쪽 소비처 정리, 세 곳
  모두 컴파일 green 확인 후 종료.
- **모션 익스텐션 = 값 동치여야** — 현 파사드의 동등성 가드(불필요 쓰기 스킵) 동작을 그대로 옮겨야
  회귀 없음. EditMode로 가드 동치 고정.
- **`worldEntity` 참조 배선 누락** — interpolator/뷰가 `worldEntity`를 못 받으면 NRE. 크리에이터가
  세팅하는 지점을 plan 태스크로 명시, 스폰 스모크로 조기 검출.
- **비제네릭 `GetEntities()` 잔여 소비자** — `MonoBehaviour`로 재타입 시 `.position` 등을 읽던 곳은
  깨짐 → 전수(위 6+ 사이트)가 `<LOPActor>` + registry로 이미 전환되므로 남지 않아야 함. plan에서
  비제네릭 오버로드 잔여 호출 0 확인.

## Open Decisions (plan/구현에서 확정)

- 모션 익스텐션 static 클래스 파일명·네임스페이스(후보: `GameFramework.World.EntityMotionExtensions`).
- `IsGrounded` 이전처: `LOPEntityView` 인라인 vs 위치 받는 static 헬퍼.
- 비제네릭 `IEntityManager.GetEntity(string)`/`GetEntities()` 오버로드를 `MonoBehaviour`로 남길지
  아예 제거할지(잔여 호출 수에 따라 — plan에서 집계).

## 상태

설계 승인 대기(2026-07-18). 다음 = plan(`writing-plans`). 진행 상태는 `docs/ROADMAP.md`.
관련: umbrella `2026-07-18-entity-view-rearchitecture-umbrella-design`,
`[[entity-unity-layer-rearchitecture]]`.
