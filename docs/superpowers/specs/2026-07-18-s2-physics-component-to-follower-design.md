# S2 — PhysicsComponent → PhysicsFollower

> **부모:** `2026-07-18-entity-view-rearchitecture-umbrella-design.md` (S2/5). 상태는 `docs/ROADMAP.md`.
> **범위:** 클라이언트 + 서버. 이 슬라이스만. 끝에 게임이 그대로 돌아가야 함(strangler).

## 목표

레거시 `PhysicsComponent : LOPComponent`(엔티티-컴포넌트)를 **순수 MonoBehaviour `PhysicsFollower`**
로 전환한다. 물리 바디(Rigidbody/Collider)는 명백한 Unity 엔진 자원이라 프레젠테이션 MonoBehaviour로
남는다(결정 규칙: Unity 이유 있음 → MonoBehaviour). 이로써 **S1 후 유일하게 남은 LOPComponent가 제거**
되어 `LOPEntity.components` 병렬 리스트가 완전히 비고, S3의 substrate 삭제가 준비된다.

## 실측 근거 (2026-07-18)

- `PhysicsComponent`(클·서 각자 복제, client `Assets/Scripts/Component/PhysicsComponent.cs`)는 rb/캡슐
  콜라이더를 `Physics/PhysicsGameObject` 자식에 생성·소유하고, `entityRigidbody`/`entityColliders`를 노출.
- **소비자(전부):**
  1. 창조자 4개(클·서 Character/Item Creator): `entity.AddEntityComponent<PhysicsComponent>()` +
     `.Initialize(isKinematic, isTrigger)` + `.entityRigidbody`/`.entityColliders[0]`를 읽어 `PhysicsBody`
     (World.Component) 공유.
  2. `LOPEntity.SyncPhysics()`/`PushMotionToPhysics()`(클·서): `this.GetEntityComponent<PhysicsComponent>()`
     → `.entityRigidbody`.
- **`PhysicsComponent.Depenetrate(int)`는 호출자 0 = 죽은 코드.** 겹침해소는 이미 공유
  `MotionBridge.Depenetrate`/`Separate`(PhysicsBody 기반)가 담당(그램: `.Depenetrate` 호출부 없음).
- `physicsGameObject`는 reactive 프로퍼티(PropertyChange 발행)지만 **외부 소비자 0**.
- `OnPropertyChange`(entity.position 변경 → rb.position)는 rb 팔로우의 reactive 반쪽.
- 서버 `PhysicsComponent`만 `TriggerDetector`(아이템 줍기 `ItemTouch`) + `entityRegistry`/`itemTouchPublisher`
  주입 보유(클라 버전엔 없음 — 파일이 다름).

## 목표 설계

### `PhysicsComponent : LOPComponent` → `PhysicsFollower : MonoBehaviour, ICleanup` (클·서 각자)

| 항목 | 지금 | S2 후 |
|---|---|---|
| 베이스 | `LOPComponent`(entity.components 등록) | `MonoBehaviour, ICleanup`(엔티티-컴포넌트 아님) |
| `entity`(LOPEntity 백레퍼런스) 의존 | `LOPComponent.entity` 통해 position/entityId | `Initialize`가 받은 **`GameFramework.World.Entity worldEntity` 보유** → Transform/Velocity 직접 읽음, entityId=`worldEntity.Id` |
| rb/콜라이더 생성·`entityRigidbody`/`entityColliders` | 유지 | 유지(창조자가 읽어 PhysicsBody 공유) |
| `Depenetrate(int)` | 죽은 코드 | **삭제** |
| `physicsGameObject` reactive 프로퍼티 | PropertyChange 발행(소비자 0) | 평범한 `private GameObject` 필드로 강등 |
| `OnPropertyChange`(rb.position 팔로우) | `entity.position` 읽음 | `worldEntity.Get<GameFramework.World.Transform>().Position` 읽음(값 동일) |
| 서버 `TriggerDetector`/`OnTriggerEnter` | `entity.entityId` 사용 | `worldEntity.Id` 사용, 주입(entityRegistry/itemTouchPublisher) 유지 |
| 정리 | `OnDetach`(구독 dispose) | `ICleanup.Cleanup`(구독 dispose) — 파괴 스윕이 호출 |

- `Initialize(GameFramework.World.Entity worldEntity, bool isKinematic, bool isTrigger)`: worldEntity 보유,
  `transform.parent.Find("Physics")`(PhysicsFollower는 LOPEntity GameObject에 얹혀 부모=root 동일)에서
  PhysicsGameObject 생성, 초기 rb 포즈는 `worldEntity.Get<Transform>()`/`Get<Velocity>()`에서 시드,
  PropertyChange 구독(키=`worldEntity.Id`).

### 소비자 재배선

| 소비자 | 지금 | S2 후 |
|---|---|---|
| 창조자 4개 | `entity.AddEntityComponent<PhysicsComponent>()` | `entity.gameObject.AddComponent<PhysicsFollower>()`(보간기와 동일) + `Inject` + `Initialize(worldEntity, isKinematic, isTrigger)` |
| PhysicsBody 공유 | `physicsComponent.entityRigidbody`/`.entityColliders[0]` | `physicsFollower.entityRigidbody`/`.entityColliders[0]`(무변) |
| `LOPEntity.SyncPhysics`/`PushMotionToPhysics` | `this.GetEntityComponent<PhysicsComponent>()` | `GetComponent<PhysicsFollower>()`(같은 GameObject Unity 컴포넌트) — 동작 동일 |

> `LOPEntity`의 두 브릿지 메서드 자체(및 `LOPEntityController`의 물리 페이즈 구동, `OnPropertyChange`
> reactive 팔로우의 중복)는 **S3에서 LOPEntity 해체 시 `MotionBridge`로 수렴**한다 — S2는 물리 루프 동작을
> 안 건드린다(strangler, 최소 변경).

### 순서 주의
`Initialize`는 `worldEntity`에 Transform/Velocity가 이미 있어야 함(창조자가 최상단에서 추가 — OK).
PhysicsBody 공유는 `Initialize` 후(=rb 생성 후) 그대로.

## 삭제/강등

- `PhysicsComponent.Depenetrate`(죽은 코드) 삭제.
- `physicsGameObject` reactive 프로퍼티 → 평범한 필드.
- `PhysicsComponent`가 entity.components에서 빠짐 → **`entity.components`가 빈다**(`AddEntityComponent`
  호출 0). `LOPComponent`/`MonoComponent`/substrate 자체 삭제는 **S3**(아직 파일 존재).

## 범위 밖
- `LOPEntity.SyncPhysics`/`PushMotionToPhysics`, `LOPEntityController`, 물리 경로 중복 통합 → **S3**.
- `MotionBridge`/`PhysicsBody`는 무변경.

## 테스트

- **Assembly-CSharp라 유닛 불가**(client-test-infra-constraint) → 컴파일 + 인게임 검증.
- **인게임(플레이):** 캐릭터 이동·점프·**캐릭터끼리 충돌(단단한 벽)**·스폰 지면 안착(Depenetrate 경로=
  MotionBridge)·**아이템 줍기(서버 트리거→ItemTouch)** 정상. 클·서 콘솔 컴파일 에러 0.

## 그린 판정
클·서 컴파일 클린 + 위 인게임 무변화 + `entity.components`에 어떤 `AddEntityComponent`도 안 남음(=병렬
컴포넌트 리스트 빔).

## 산업 표준 매핑
- `PhysicsFollower` — 키네마틱 컨트롤러(Photon Quantum KCC / Unreal CMC 계열)의 **뷰측 rb 팔로워**:
  시뮬(World.Transform)이 권위, Rigidbody는 위치를 따라가는 물리 표현. 엔티티 데이터 아닌 프레젠테이션.

## Open Decisions (확정)
- **`PhysicsFollower`가 `worldEntity` 보유**(LOPEntity 의존 끊기) — 얇은 컴패니언 목표 정합. ✅
- **`Depenetrate` 삭제**(죽은 코드, MotionBridge가 담당). ✅
- **`LOPEntity` 브릿지는 `GetComponent`로 최소 수정, 통합은 S3**. ✅
