# 물리 rb-follow 경로 통합 (③)

> **성격:** 엔티티 재구조화 S3에서 분리한 **behavioral 슬라이스**(타입 삭제 아님, 물리 동작 변경 → 플레이
> 검증 필요). umbrella `2026-07-18-entity-view-rearchitecture-umbrella-design.md`의 S3 "범위 밖 ③".
> 상태는 `docs/ROADMAP.md`.
> **범위:** 클라이언트 + 서버 (GameFramework·LOP-Shared 무변경 — `MotionBridge.PushMotion`은 이미 존재).

## 목표

Rigidbody가 World.Transform을 따라가게 하는 **3개 중복 경로**를 **호스트 단일 패스**로 통합한다:
- ① `MotionBridge.PushMotion` — `world.Tick`의 `Has<Simulated>()` 게이트 안(내 캐릭만).
- ② `LOPEntity.SyncPhysics`/`PushMotionToPhysics` — `LOPEntityController`가 물리 페이즈마다 구동(전 char+item).
- ③ `PhysicsFollower.OnPropertyChange` — position 변경 reactive.

②·③(레거시 경로)를 삭제하고, **`BeforePhysicsSimulation` 지점에서 `PhysicsBody` 가진 모든 엔티티에
`MotionBridge.PushMotion`을 한 번 도는 호스트 패스**로 대체한다. `LOPEntityController` 소멸.

## 실측 근거 (2026-07-18 오케스트레이션 감사)

- **틱 루프 = 코루틴**(`TickUpdaterBase`), Unity `Update` 후·`LateUpdate` 전 실행(프레임당 0..N 캐치업 틱).
  틱당 순서: `Reconcile → ProcessInput → UpdateEntity → world.Tick`(Mutation: Simulated만 PushMotion)
  `→ BeforePhysicsSimulation`(레거시 `LOPEntityController.PushMotionToPhysics`, 전 char+item)
  `→ Physics.Simulate → AfterPhysicsSimulation`(`SyncPhysics`=kinematic no-op) `→ End`.
- **`Before/AfterPhysicsSimulation` 리스너는 오직 `LOPEntityController`**(클·서). 삭제하면 그 이벤트는 리스너 0.
- **`LOPWorld.Mutation` 키네마틱 페이즈**(`:64-74`): `SyncTransforms()` 1회 후 Simulated만 `Depenetrate`/
  `Separate`/`KinematicMove`(World.Transform 씀)/`PushMotion`. 비-Simulated(원격·아이템)는 안 건드림.
- **보간기는 `LateUpdate`**(틱 루프 후). 원격 `entity.position/rotation`(=World.Transform)을 씀 →
  position은 `PropertyChange` 발행(→③ reactive rb.position), rotation은 발행 없음(②만 rb.rotation 밀어줌).
- **타이밍 파리티:** 틱 루프가 볼 때 원격 World.Transform은 **직전 프레임 LateUpdate 값**(한 프레임 stale).
  이 stale은 **현재도 동일**(reactive·controller 둘 다 F-1 값) → 통합 패스가 지연을 추가하지 않음.
- **Reconciler `:87-88`** — 롤백 하드복원 후 `entity.PushMotionToPhysics()`+`Physics.SyncTransforms()`.
  ②의 유일한 비-controller 호출처(대체 필요). 리플레이 루프는 이미 `world.Tick`(=①) 경로.
- **`MotionBridge.PushMotion(Entity)`** — `PhysicsBody` 읽어 kinematic이면 rb.position+rotation을 Transform에서.
  `PhysicsBody`/rb null이면 early-return. 유일 호출처=`LOPWorld.Mutation`(Simulated). DI 등록됨(클·서).
- **아이템엔 `PhysicsBody` 없음**(클·서 ItemCreator) → 통합 패스에서 빠짐. 서버 아이템은 `Simulated`도 없음.
- **서버**: 보간기 없음, 전 캐릭터 `Simulated`(world.Tick서 PushMotion 됨). 아이템만 `PhysicsBody`/`Simulated`
  없이 `LOPEntityController`+reactive로 따라감.

## 목표 설계

### 1. 호스트 단일 follow 패스 (클·서 `LOPRunner.SimulatePhysics`)
`physicsSimulator.Simulate` **직전**에 `entityRegistry`의 모든 엔티티에 `MotionBridge.PushMotion` 1회:
```csharp
private void SimulatePhysics()
{
    DispatchEvent<BeforePhysicsSimulation>();      // 유지(리스너 0이 되지만 이벤트 인프라 보존)
    // World.Transform → rb 팔로우: PhysicsBody 가진 모든 엔티티(내 캐릭=예측, 남·아이템=보간).
    // Simulated는 world.Tick서 이미 밀렸으나 idempotent. per-entity LOPEntityController 대체.
    foreach (var entity in entityRegistry.All)
    {
        motionBridge.PushMotion(entity);
    }
    physicsSimulator.Simulate(interval);
    DispatchEvent<AfterPhysicsSimulation>();
}
```
- `LOPRunner`에 `IMotionBridge motionBridge` 주입 추가(entityRegistry는 기존). `PhysicsBody` 없는 엔티티는
  `PushMotion`이 자동 스킵.
- **위치 = 파리티**(`BeforePhysicsSimulation` 지점, 레거시와 동일).

### 2. 아이템에 `PhysicsBody` 추가 (클·서 `ItemCreator`)
`CharacterCreator`와 동일하게 `Initialize` 후:
```csharp
worldEntity.Add(new PhysicsBody(physicsFollower.entityRigidbody, (CapsuleCollider)physicsFollower.entityColliders[0]));
```
→ 통합 패스가 아이템까지 커버. 클라 아이템=보간 따라감, 서버 아이템=정적이라 no-op(무해).

### 3. Reconciler 롤백-후 push 대체 (클라 `Reconciler`)
`entity.PushMotionToPhysics()` → `motionBridge.PushMotion(worldEntity)`. `Physics.SyncTransforms()` 유지
(또는 `motionBridge.SyncTransforms()`). `Reconciler`에 `IMotionBridge` 주입 추가.

### 4. 레거시 경로 삭제 (클·서)
- `LOPEntityController.cs` **삭제**(클·서) — 창조자의 `LOPEntityController` 생성·`SetEntity` 배선도 제거.
- `LOPEntity.SyncPhysics()`·`PushMotionToPhysics()` 메서드 삭제(클·서).
- `PhysicsFollower`(클·서): `OnPropertyChange` + `propertyChangeSubscription` + `Initialize`의 `PropertyChange`
  구독 제거. 구독만 없애면 `ICleanup.Cleanup`이 빔 → `ICleanup` 구현 제거(서버 트리거/`worldEntity`/init은 유지).

### 5. (기회적) 죽은 PropertyChange 발행 정리
③ 제거 후 `PropertyChange`(position) 구독자가 0이면(grep 확인) — `LOPEntity.position` setter의
`RaisePropertyChanged` 발행 + `RaisePropertyChanged` 메서드도 죽은 코드 → 제거. **구독자 0 확인 시에만.**

## 테스트 / 그린
- Assembly-CSharp라 유닛 불가 → 컴파일 + **인게임(핵심=물리 동작)**:
  - 내 캐릭 이동·점프·중력·**캐릭터끼리 충돌**(단단한 벽) 정상.
  - **원격 캐릭터**(2번째 클라/AI)가 내 화면에서 제자리 따라오고 충돌 정상(회전 포함 — ② 삭제로 회전이
    통합 패스로 넘어감).
  - **아이템 줍기**(트리거) 정상 — 아이템 PhysicsBody 추가 후에도.
  - **넉백·전역공격·Reconciler 롤백**(고RTT/손실) 후 위치 튐 없음.
  - 클·서 콘솔 신규 예외 0.
- 그린 판정: `LOPEntityController`/`SyncPhysics`/`PushMotionToPhysics`/`OnPropertyChange` 참조 0, 클·서 컴파일 클린.

## 범위 밖
- 보간을 `LateUpdate`→틱 내로 당겨 rb 신선도 개선(타이밍 B) — 별개, 지금 불필요.
- `LOPEntity` 자체(파사드)·`IEntity`·매니저 구조 = S4/S5.

## Open Decisions (확정)
- **① 아이템에 PhysicsBody 추가**(통합 패스 커버 → LOPEntityController 완전 삭제). ✅
- **② 패스 위치 = `BeforePhysicsSimulation`(파리티)**. ✅
- 호스트 패스 = `LOPRunner.SimulatePhysics` 인라인(전용 시스템 아님, GF 무변경). ✅
