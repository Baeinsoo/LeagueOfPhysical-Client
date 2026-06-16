# World Core 뷰 이행 — Slice B(기초): World Transform/Velocity 코어 컴포넌트 + 단방향 미러

**Date:** 2026-06-16
**Branch:** `feature/world-core-motion-slice-b`
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [Entity System Design](../../entity-system-design.md) · [Slice A spec](2026-06-16-world-core-view-slice-a-design.md)

## Goal

순수 C# 공간 컴포넌트 **`World.Transform`**(Position, Rotation:Quaternion)와 **`World.Velocity`**(Linear)를 신설(`System.Numerics`)하고, `CharacterCreator`가 초기값으로 채운 뒤, **`LOPEntity` 필드를 진실원본으로 유지한 채 매 틱 단방향 live 미러**(LOPEntity → World)로 동기한다. 읽는 소비자·netcode·이벤트→pull 전환은 **없음**. inside-out Motion 이행의 *기반*(코어 공간 데이터 + 수학 타입)을 박제하는 저위험 첫 입.

(World.Health/Mana/Stats/Level가 한동안 reader 없이 shadow였던 선례와 동일 — 데이터+타입만 먼저 세운다.)

## 잠긴 결정 (durable)

- **Model A 기초**: position 진실원본은 *아직 `LOPEntity`*; World는 미러(reader 없음). 진실원본 승격·소비자 전환은 후속(netcode/Stage ④ 결합이라 분리).
- **수학 타입 = `System.Numerics`** (`Vector3`, `Quaternion`). 코어 `noEngineReferences` 유지 — `UnityEngine` 타입은 경계(어댑터)에서만 변환.
- **구조 = `Transform`(pos+rot) + 별도 `Velocity`** — DOTS(`LocalTransform` + `PhysicsVelocity`)/Quantum(`Transform3D` + `PhysicsBody3D`) 표준 정합. ("Motion" 한 덩어리 명명은 폐기 — 비표준.)
- **Rotation = Quaternion** (오일러 아님). 오일러는 Unity Transform 표시용 표현일 뿐, 저장·보간·합성 표준은 Quaternion(DOTS quaternion, Quantum FPQuaternion). 현 `LOPEntity.rotation`은 euler지만 미러 시 quaternion으로 변환(Rigidbody가 이미 quaternion이라 무손실).
- **Angular velocity 생략(YAGNI)**: 현 LOP는 각속도 미사용(Rigidbody `FreezeRotation` + 회전은 코드 구동 + `angularVelocity` 비참조). 필요해지면 *새 컴포넌트가 아니라* `Velocity`에 `Angular` 필드 추가(DOTS `PhysicsVelocity{Linear,Angular}` 정합).
- **Scale 생략**: LOP는 엔티티별 scale을 추적하지 않음.

## Scope (In)

**Core (`GameFramework.World`, 순수 C#):**
- `Transform : Component` — `System.Numerics.Vector3 Position`, `System.Numerics.Quaternion Rotation`. Anemic.
- `Velocity : Component` — `System.Numerics.Vector3 Linear`. Anemic.

**Client (`Assets/Scripts/World/`):**
- 변환 확장(메서드) — `UnityEngine.Vector3 ↔ System.Numerics.Vector3`, `UnityEngine.Quaternion ↔ System.Numerics.Quaternion`(x,y,z[,w] 매핑). 경계 전용(클라 측, `UnityEngine` 참조 OK).
- `WorldMotionSync`(신규 per-entity `MonoBehaviour, ICleanup`) — `[GameEngineListen(typeof(AfterPhysicsSimulation))]`로 매 틱 `LOPEntity.position/rotation/velocity`를 변환해 `World.Transform`/`World.Velocity`에 기록(단방향). `[Inject] EntityRegistry`로 World.Entity를 id로 해석(캐시). `CharacterCreator`가 생성.

**Client 기존 파일 변경:**
- `CharacterCreator` — World.Entity 등록 시 `World.Transform`(position, `Quaternion.Euler(rotation)`)과 `World.Velocity`(velocity)를 초기값으로 추가(World.Health 옆). `WorldMotionSync` 컴포넌트 생성+주입.

## 데이터 흐름

```
LOPEntity 필드(진실원본)
   └─(매 틱, AfterPhysicsSimulation; SyncPhysics 직후 = 권위 post-physics)
        WorldMotionSync ──(UnityEngine→System.Numerics 변환)──▶ World.Transform / World.Velocity (코어 미러)
                                                                  (reader 없음 — 후속 슬라이스가 pull)
```

## Out of scope (defer)

- position/rotation/velocity를 `World` *진실원본*으로 승격, 이벤트(`PropertyChange`)→pull 전환.
- 물리 어댑터(`PhysicsComponent`)·뷰(`LOPEntityView`)·입력(`LOPMovementManager`)·reconciler(`SnapReconciler`/`ServerStateReconciler`/`SnapInterpolator`)를 World로 전환 — **netcode/Stage ④** 영역.
- `IPhysicsSimulator` DIP + `Tick` 컴포지션(Slice 4).
- Angular velocity, scale.

## 조사 근거 (구현 사실)

- **진실원본 = `LOPEntity` C# 필드**(`_position/_rotation/_velocity`); Rigidbody는 필드에서 push되고 `SyncPhysics()`가 Rigidbody→필드 read-back.
- **Rigidbody `FreezeRotation`**(`PhysicsComponent.Initialize`) + 회전은 `LOPMovementManager`가 `SmoothDampAngle`로 구동 + `angularVelocity` 비참조 → 각속도 미사용 확정.
- **World 등록은 캐릭터만**(`CharacterCreator`만 `EntityRegistry.Add`; `ItemCreator` 미등록) → `WorldMotionSync`/초기화는 캐릭터 대상.
- **`AfterPhysicsSimulation`** 는 `LOPEntityController`의 `SyncPhysics()`(Rigidbody→필드) 직후 발화 → 그 틱의 권위 위치가 필드에 반영된 시점.
- 코어 asmdef `noEngineReferences: true` 확인. 기존 `EntityTransform`(GameFramework Model)은 `UnityEngine.Vector3` 사용 + World asmdef 밖이라 코어 재사용 불가 → System.Numerics 신규.

## 검증

- **컴파일**: 클라 0에러(UnityMCP `refresh_unity`+`read_console`, 클라 인스턴스 핀). 코어는 서버도 file: 참조하므로 양쪽 컴파일 영향 — Slice B는 *추가만*(Transform/Velocity 신규)이라 비파괴, 그래도 양쪽 컴파일 확인 권장.
- **런타임(수동)**: 임시 디버그로 `World.Transform.Position`이 `LOPEntity.position`과 일치(이동 중에도 추종)하는지 관찰 후 로그 제거. 동작 변화 없음(회귀 0)이 성공.
- 자동화 테스트 신규 없음(단일 Assembly-CSharp). 단, 코어 `Transform`/`Velocity`는 순수 C#이라 GameFramework 측 EditMode 테스트로 *생성/기본값* 검증을 추가할 수 있음(선택 — ROI 보고 plan에서 결정).

## Open (plan에서 해소)

- `WorldMotionSync`를 별도 per-entity 컴포넌트로 둘지, `LOPEntityController`에 흡수할지 — 격리(별도) 권장. plan에서 확정.
- 미러 지점 `AfterPhysicsSimulation` 고정 — reconciler의 프레임 내 보정은 Motion에 한 틱 지연 반영(reader 없어 무해).

## 진행

- [x] 범위(기초 Motion)·수학(System.Numerics)·구조(Transform+Velocity, Quaternion) 합의 + 표준 1차 확인
- [ ] 이 spec 사용자 리뷰
- [ ] `writing-plans`로 구현 plan
- [ ] 구현 → 검증 → 머지

> 후속: position 진실원본 승격 + 이벤트→pull + 물리/뷰/입력/reconciler 전환(Stage ④ 결합), `IPhysicsSimulator` DIP. 각자 별도 spec.
