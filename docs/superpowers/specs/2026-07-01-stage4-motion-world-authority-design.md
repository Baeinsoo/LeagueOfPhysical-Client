# Motion 권위를 World.Entity로 — 파사드 이행 (Stage④ 첫 슬라이스, 클라)

**Date:** 2026-07-01
**Branch (제안):** `feature/stage4-motion-world-authority` (GameFramework[ToUnity 추가] + LeagueOfPhysical-Client). **서버 코드 무변경.**
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (Stage④ · keystone = velocity 권위 이전) · [netcode-redesign](../../netcode-redesign.md) §6.5 · 메모리 [[world-core-view-migration-status]] · [[world-core-runner-world-naming]] (4e가 이 슬라이스로 잠금 해제)

## Goal

캐릭터의 **위치·회전·속도(motion) 데이터의 진실원본을 클라 `LOPEntity`(MonoBehaviour) → 순수 C# `World.Entity`(GameFramework)로 이전**한다. 방법 = `LOPEntity.position/rotation/velocity` 접근자의 *저장소만* `World.Transform`/`World.Velocity` 컴포넌트로 갈아끼우는 **파사드**(바깥 인터페이스 `entity.position` 무변경). 동작 100% 보존. **클라 단독.**

이건 Health/Mana/Level/Stats가 이미 마친 "레거시 엔티티 컴포넌트 → World.Entity 단일 진실원본" 이행의 **마지막 남은 조각(Motion)** 이다. Motion만 남은 이유 = PhysX가 매 틱 적분해 Rigidbody 왕복이 얽혀서.

## 배경 / 동기 — Stage④의 keystone

Stage④(클라 예측·롤백·Snapshot/Restore)의 **주춧돌**. 예측/롤백은 시뮬이 모션 상태를 *소유*해야 성립한다(같은 틱 재시뮬 → World 상태를 snapshot/restore). 현재 모션 권위가 Rigidbody(클라 MonoBehaviour side)에 있어서:
- 공유 시뮬 `LOPWorld`(LOP-Shared)가 모션을 못 만짐 → **4e(물리·어빌리티 effect를 `LOPWorld.Tick`으로) 막힘.**
- Snapshot/Restore가 찍을 "시뮬 소유 모션"이 없음.

이 슬라이스가 풀리면 4e·Snapshot/Restore가 이어서 가능. (bg_pmove의 `playerState`=순수 데이터 전제와 동형 — 4c brainstorm에서 "접근 B=Stage④"로 미뤘던 그것.)

## 현재 상태 (실측)

**권위 = Rigidbody.** 매 틱 사이클: 입력→커널 `AddForce`(Rigidbody) → `Physics.Simulate` 적분 → `SyncPhysics`(Rigidbody→LOPEntity) → `WorldMotionSync`(LOPEntity→World 수동 미러).

- **`LOPEntity`** (`Assets/Scripts/Entity/LOPEntity.cs`): `position/rotation/velocity`가 자기 `_position/_rotation/_velocity` 필드 백킹(`override`, MonoEntity 기반). setter가 `RaisePropertyChanged` → `PropertyChange` 이벤트 → `PhysicsComponent`가 Rigidbody 동기. `SyncPhysics()`가 Rigidbody→이 필드.
- **`World.Transform`/`World.Velocity`** (GameFramework `Runtime/Scripts/World/Components/`): `Transform{Position,Rotation}`/`Velocity{Linear}`. **수동 미러** — `WorldMotionSync`가 매 틱 LOPEntity에서 베낌.
- **`WorldMotionSync`** (`Assets/Scripts/World/WorldMotionSync.cs`): `AfterPhysicsSimulation`에 LOPEntity→World 단방향 복사. `Start()`에서 `entityRegistry.Get(entityId).Get<Transform/Velocity>()`로 참조 획득.
- **소비자**: 커널(`LOPMovementManager`)·reconciler(`SnapReconciler`/`ServerStateReconciler`)·뷰·GamePad가 전부 `entity.position` 등으로 접근.
- ⚠️ **아이템은 World.Entity가 없다**: `ItemCreator`는 `LOPEntity`만 만들고 World.Entity/Transform/Velocity/register를 **안 함**. `CharacterCreator`만 World.Entity 생성(`Transform`은 `entity.position.ToNumerics()`로 시드, line 91-96).
- **생성 순서 문제**(`CharacterCreator`): `entity.Initialize(creationData)`(위치 세팅, line 32)가 World.Entity의 Transform/Velocity 생성(line 91-96)보다 **먼저** — 파사드면 그 시점에 worldTransform이 없어 터짐. **순서 재배치 필요.**

## 설계

### ⓪ GameFramework — `ToUnity` 역변환 추가 (선행)

`Runtime/Scripts/Extensions/Numerics.Extensions.cs`에 `ToNumerics`의 짝 `ToUnity`(Numerics→Unity)를 추가. 현재 파일 주석이 *"World→Unity 역변환은 pull 소비가 생기는 후속 슬라이스에서 추가"* 로 예고 — **이 슬라이스가 그 pull 소비**(파사드 getter).
```csharp
public static Vector3 ToUnity(this System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
public static Quaternion ToUnity(this System.Numerics.Quaternion q) => new Quaternion(q.X, q.Y, q.Z, q.W);
```
additive(신규 메서드) → 서버 동작 무영향(미사용), 짝 일관을 위해 `ToNumerics` 옆에 배치.

### ① `LOPEntity` — 파사드 (필드 → World 컴포넌트)

`_position/_rotation/_velocity` 필드 제거, 접근자가 캐시된 `worldTransform`/`worldVelocity` 참조에 위임. **변경 감지는 기존 `SetProperty`와 동일하게 `Vector3EqualityComparer.instance`로**(GameFramework public):

```csharp
private GameFramework.World.Transform worldTransform;
private GameFramework.World.Velocity worldVelocity;

/// <summary>생성 시 크리에이터가 이 엔티티의 World.Entity 모션 컴포넌트를 연결한다(파사드 백킹).</summary>
public void LinkWorldMotion(GameFramework.World.Transform transform, GameFramework.World.Velocity velocity)
{
    this.worldTransform = transform;
    this.worldVelocity = velocity;
}

public override Vector3 position
{
    get => worldTransform.Position.ToUnity();
    set
    {
        var current = worldTransform.Position.ToUnity();
        if (Vector3EqualityComparer.instance.Equals(current, value)) return;   // 변경 시에만 발화(SetProperty 동치)
        worldTransform.Position = value.ToNumerics();
        RaisePropertyChanged(this, new PropertyChangedEventArgs(nameof(position)));
    }
}

public override Vector3 velocity  // worldVelocity.Linear, 동일 패턴
{
    get => worldVelocity.Linear.ToUnity();
    set { var c = worldVelocity.Linear.ToUnity(); if (Vector3EqualityComparer.instance.Equals(c, value)) return; worldVelocity.Linear = value.ToNumerics(); RaisePropertyChanged(this, new PropertyChangedEventArgs(nameof(velocity))); }
}

public override Vector3 rotation  // euler ↔ World.Transform.Rotation(Quaternion)
{
    get => worldTransform.Rotation.ToUnity().eulerAngles;
    set { var c = worldTransform.Rotation.ToUnity().eulerAngles; if (Vector3EqualityComparer.instance.Equals(c, value)) return; worldTransform.Rotation = Quaternion.Euler(value).ToNumerics(); RaisePropertyChanged(this, new PropertyChangedEventArgs(nameof(rotation))); }
}
```

- 바깥 API(`entity.position` 등) 무변경 → **소비자 0 수정.**
- `RaisePropertyChanged`(→ PhysicsComponent Rigidbody 동기)는 **변경 시에만** 발화(기존 `SetProperty` 동치).
- `SyncPhysics()` 무변경(Rigidbody→facade→World).

> ⚠️ **회전 nuance (검증 필요, 안전 판단):** rotation은 euler(Vector3)인데 World는 Quaternion 저장이라 **euler→quat→euler 왕복**을 거친다 → 값이 mod-360 *등가*로 바뀔 수 있음(예: -90 저장 후 읽으면 270). **기능·시각 무영향**으로 판단: 이동 커널은 rotation을 쓰기만(안 읽음), reconciler `SmoothDampAngle`은 wrap 안전, PhysicsComponent `Quaternion.Euler`는 등가, 서버는 클라 보고 rotation 미사용(netcode §6.4). 그래도 raw 값이 비-동일이라 **플레이 검증 시 회전/facing 시각 동일 확인**.

### ② `CharacterCreator` — 생성 순서 재배치

World.Entity의 Transform/Velocity가 **존재 + LOPEntity에 link된 뒤** 위치가 접근되도록:

1. `worldEntity = new Entity(id)`; `worldEntity.Add(new Transform())`; `worldEntity.Add(new Velocity())` — **먼저**.
2. `entity = CreateChild<LOPEntity>()`; inject; `entity.LinkWorldMotion(worldEntity.Get<Transform>(), worldEntity.Get<Velocity>())`; `entity.Initialize(creationData)` → 위치/회전/속도가 파사드 통해 World에 시드됨.
3. 나머지 엔티티 컴포넌트(EntityType/Character/Appearance/Physics)·Controller·View·Reconciler — 기존 순서.
4. 나머지 World 컴포넌트(Health/Mana/Level/Stats[characterComponent 필요]/Abilities/StatusEffects) 추가; `entityRegistry.Add(worldEntity)`; ability grant — 기존 위치.

> Transform/Velocity만 위로, Health/Stats는 characterComponent 뒤라 아래 유지(2단계). `Initialize`가 파사드로 시드 → 별도 직접 시드 불필요(기존 line 91-96의 `entity.position.ToNumerics()` 시드는 제거, 순환 회피).

### ③ `ItemCreator` — 아이템에도 World.Entity(Transform/Velocity) 부여

파사드가 균일하려면 모든 `LOPEntity`가 World.Entity를 가져야 함. 아이템에 **최소 World.Entity**(Transform/Velocity만, Health/Abilities 없음) 추가 + `Initialize` 전 link:
```csharp
var worldEntity = new GameFramework.World.Entity(creationData.entityId);
worldEntity.Add(new GameFramework.World.Transform());
worldEntity.Add(new GameFramework.World.Velocity());
entityRegistry.Add(worldEntity);
// entity 생성 후: entity.LinkWorldMotion(worldEntity.Get<Transform>(), worldEntity.Get<Velocity>()); entity.Initialize(...)
```
(`ItemCreator`에 `[Inject] EntityRegistry` 추가 필요 — 현재 없음.)

> **안전 확인:** `LOPWorld.Mutation`이 `EntityRegistry.All`을 순회하며 `AbilitySystem.Tick`/`StatusEffectSystem.Tick` 호출하는데, 둘 다 `Get<Abilities>()`/`Get<StatusEffects>()` **null 가드로 early-return**(실측). 아이템은 그 컴포넌트가 없어 **no-op** — 레지스트리 편입 안전.

### ④ `WorldMotionSync` — 삭제

LOPEntity가 곧 World이므로 "LOPEntity→World 복사"는 무의미. 파일 삭제 + `EntityBinder`(`Assets/Scripts/Game/EntityBinder.cs:53-55`)에서 생성 3줄 제거.

### 안 바뀌는 것 (중요)

- **커널**(`LOPMovementManager`): `AddForce`(Rigidbody) + `entity.rotation=` 그대로. 속도는 Rigidbody로 적분→`SyncPhysics`가 World에 되씀. Rigidbody=적분기 / World=틱 사이 저장소. **커널 무변경.**
- **reconciler**·**뷰**·**GamePad**·**PhysicsComponent**·**LOPEntityController.SyncPhysics**: `entity.position` 접근 그대로 → 무변경(파사드 뒤에서 World 조작).
- **GameFramework**: Transform/Velocity 컴포넌트 이미 존재. 유일 변경 = `ToUnity` 역변환 **추가**(additive, 서버 미사용).
- **서버**: 무변경(자기 Rigidbody 권위 + 자기 WorldMotionSync 유지). 와이어는 position/velocity *값*만 건너 → 클라 내부 권위 변경과 무관.

## 클라 단독인 이유

각 사이드의 모션 사이클(WorldMotionSync/커널/PhysicsComponent/creator)은 독립 사본이고, 와이어는 값만 운반. 클라만 World-권위로 바꿔도 서버 무영향 → **클라 세션 규율 준수 + 위험 한 쪽씩.** 서버 flip은 후속 슬라이스(필요 시).

## 동작 보존 / 검증

- **보존 논거**: 데이터가 사는 곳만 LOPEntity 필드 → World 컴포넌트. 값 흐름(입력→Rigidbody→Simulate→SyncPhysics→저장, reconciler 쓰기)은 동일. PropertyChange도 변경 시에만 발화(동치).
- **검증**: 클라 컴파일 클린(`read_console`). 실행: 룸 접속 후 **이동/회전/점프/대시** 정상, reconciliation(본인+타인) 무회귀, **아이템 표시·물리** 정상(신규 World.Entity), 콘솔 신규 에러 0. 서버 무변경이라 서버 영향 0.
- **유닛 테스트**: `LOPEntity`/creator는 클라 Assembly-CSharp라 asmdef 테스트 불가(동작 보존 슬라이스, 신규 테스트 없음). 페이오프는 Stage④(snapshot/restore·예측).

## 산업 표준 매핑

- 시뮬이 모션을 *순수 데이터*로 소유 = Quake `bg_pmove`의 `playerState`(순수 데이터, 양쪽 공통), DOTS `LocalTransform`/Unreal `UWorld`가 트랜스폼 소유. Rigidbody=적분기(transient), World 컴포넌트=틱 사이 canonical.
- 파사드(Unity Vector3 뷰 ↔ Numerics 코어) = 앞선 Health/Mana/Stats 이행과 짝 일관(단, 그것들은 완전 제거·소비자 직접 읽기였고 Motion은 물리·기반계약 특수성으로 파사드 유지 — 근거는 "안 바뀌는 것" 참고).

## Out of Scope

- **4e**(커널·물리·어빌리티 effect를 `LOPWorld.Tick`으로) — 이 슬라이스가 잠금 해제하는 *다음* 작업. 여기선 커널이 여전히 Rigidbody에 `AddForce`(host).
- **Snapshot/Restore·클라 예측·롤백·commit gate·결정론 RNG** — 후속 Stage④ 슬라이스.
- **서버 Motion World-권위 flip** — 후속(대칭 작업).
- **LOPEntity 껍데기 최종 제거 여부** — 미결(데이터는 이 슬라이스로 최종 집=World 도달; 껍데기 운명은 별도 나중 결정).

## Open Questions (구현 plan 전 확인)

- **link 메서드 명명**: `LinkWorldMotion(Transform, Velocity)` vs `BindWorldMotion` vs worldEntity 통째 전달 후 내부 Get. (추천: 컴포넌트 2개 명시 전달 = 결합 최소.)
- **`Initialize`의 위치 세팅 유지 vs 제거**: 파사드 통해 시드(유지) vs Transform/Velocity를 creationData로 직접 시드하고 Initialize에서 위치줄 제거. (추천: 유지 — 단일 시드 지점, 단 link가 Initialize보다 먼저여야.)
- **link 전 접근 가드**: 재배치로 link-first 보장되면 불필요(fail-loud). 방어 가드 추가 여부.

## 진행

- [x] 브레인스토밍 — Stage④ 첫 슬라이스=Motion 권위 이전, 파사드 방식, 클라 단독 합의
- [x] 현재 흐름·연결 메커니즘·아이템 비대칭·레지스트리 순회 안전성 실측
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
