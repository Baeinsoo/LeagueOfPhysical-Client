# World Core 모션 이행 — 서버 패리티: EntityBinder 구조 + World Transform/Velocity 단방향 미러

**Date:** 2026-06-18
**Branch (docs):** `feature/world-core-motion-server` (클라 repo — 설계 허브)
**Branch (code):** 서버 repo 피처 브랜치 (구현 시 생성)
**Related:** [Slice B spec (클라)](2026-06-16-world-core-motion-slice-b-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [LOP 저장소 토폴로지](../../lop-repo-topology.md)

## Goal

클라가 받은 **World Core 모션 이행(Slice B)**을 **서버에 패리티로 이식**한다. 서버에 클라의 **엔티티 바인딩 구조**(`EntityCreated` 수명 신호 + `EntityBinder`)를 깔고, 그 위에 순수 C# **`World.Transform`/`World.Velocity`** 미러를 첫 바운드 어댑터(`WorldMotionSync`)로 얹는다. 뷰 부착만 빠진, 클라와 거의 동형 구조.

`LOPEntity` 필드를 진실원본으로 유지한 채 매 틱 단방향 미러(`LOPEntity → World`). **reader 없음 / 동작 변화 0** — 클라 Slice B와 동일한 저위험 그림자 슬라이스.

## 배경 — 왜 서버가 비어 있나 (패리티 감사 결과)

클라-서버 아키텍처 패리티 감사(커밋 이력 대조) 결과, 서버가 클라에 뒤처진 **비-UI 아키텍처 트랙은 정확히 둘**: (1) 이 Motion 슬라이스, (2) Health 진실원본 이행(다음). 나머지(World Core 1–3, LOP-Shared, MasterData Luban, game scene scope, MVC 디커플)는 모두 서버 패리티 확보됨. 클라 전용으로 서버가 스킵하는 것은 UI/뷰 레이어(UI Toolkit M1–M5, Slice A Nameplate, 뷰 스폰 리팩터)뿐.

클라 Slice B는 **`EntityBinder`(구 EntityViewSpawner)에 `WorldMotionSync` 생성**을 넣었다. 서버엔 그 바인더도, 그 전제인 **`EntityCreated` 수명 신호도 없다**(서버 `LOPEntityManager.CreateEntity`는 발행 안 함). 따라서 서버 이식은 바인딩 구조부터 깐다.

## 잠긴 결정 (durable — 브레인스토밍 합의)

- **EntityBinder 구조 채택(별도 컴포넌트 + 수명 신호)**: 미러를 `CharacterCreator`에 직접 넣지 않고, 클라처럼 `EntityCreated`에 반응하는 `EntityBinder`가 `WorldMotionSync`를 부착한다. 근거: `EntityBinder`는 *뷰 스포너*가 아니라 설계 문서의 **"엔티티 수명에 반응해 per-entity 바깥 어댑터를 붙이는 바인딩 시스템(view-resolver)"** — 뷰든 물리/모션 어댑터든 역할은 동일. 서버는 *뷰 부착만 빠진* 동형 바인더를 갖는다. 구조 대칭 유지(클·서 병렬 → 추론·추후 공통 추출 용이, "shared game code" 토폴로지 가치) + Health/Stage④에서 늘어날 per-entity 어댑터의 자연스러운 집.
- **순수 그림자**: reader 없음, 진실원본은 여전히 `LOPEntity`. World는 미러. 동작 변화 0. (진실원본 승격은 Stage④.)
- **수학 타입 = `System.Numerics`** (클라와 동일, 코어 `noEngineReferences` 유지). `UnityEngine` 변환은 경계 어댑터(`ToNumerics`)에서만.
- **미러 지점 = `AfterPhysicsSimulation`** (서버 `LOPEntityController.SyncPhysics()` readback 직후 권위 위치). 같은 이벤트의 리스너 순서로 인한 한 틱 지연은 **reader 없어 무해**(클라 Slice B와 동일 판단).
- **클·서 패리티 유지**: `LOPEntityController`/`LOPEntityView`는 클라처럼 `CharacterCreator`에 그대로 둔다(바인더로 이동 안 함). 바인더는 `WorldMotionSync`만 부착(서버는 뷰 없음).

## Scope (In) — 서버 7개 변경

> 위치/네임스페이스는 클라 대응물과 동일 형태. World 타입은 풀 네임스페이스 한정(프로젝트 컨벤션).

| # | 파일 (서버) | 변경 |
|---|---|---|
| 1 | `Assets/Scripts/Entity/Event.Entity.cs` | `EntityCreated` struct 추가 (클라 포팅 — `IEntity entity` 필드 + 생성자) |
| 2 | `Assets/Scripts/Entity/LOPEntityManager.cs` | `CreateEntity`에서 `entityMap` 세팅 직후 `EventBus.Default.Publish(nameof(EntityCreated), new EntityCreated(entity))` 추가 (클라 동일 위치) |
| 3 | `Assets/Scripts/World/NumericsConversionExtensions.cs` | **신규** — 클라에서 포팅. `UnityEngine.Vector3 → System.Numerics.Vector3`, `UnityEngine.Quaternion → System.Numerics.Quaternion` (`ToNumerics`, 현재 사용분만) |
| 4 | `Assets/Scripts/World/WorldMotionSync.cs` | **신규** — 클라에서 포팅. per-entity `MonoBehaviour, ICleanup`. `Start`에서 `EntityRegistry.Get(id)`로 World.Transform/Velocity 해석 + `gameEngine.AddListener(this)`. `[GameEngineListen(typeof(AfterPhysicsSimulation))]`에서 `LOPEntity.position/rotation/velocity`를 변환해 미러. `Cleanup`에서 리스너 해제 + 참조 null. |
| 5 | `Assets/Scripts/Game/EntityBinder.cs` | **신규(서버판)** — `IGameMessageHandler`. `Register/Unregister`로 `EntityCreated` 구독. `OnEntityCreated`: 캐릭터 엔티티(`CharacterComponent` 보유)에만 `root.CreateChildWithComponent<WorldMotionSync>()` + `objectResolver.Inject` + `SetEntity`. **뷰 부착 없음**(클라의 DamageFloaterEmitter/Nameplate 대응 없음). |
| 6 | `Assets/Scripts/EntityCreator/CharacterCreator.cs` | World.Entity 등록 블록에 `World.Transform`(Position, `Quaternion.Euler(rotation)`) + `World.Velocity`(velocity) 초기값 추가 (기존 `World.Health` 옆) |
| 7 | `Assets/Scripts/Game/GameLifetimeScope.cs` | `builder.Register<IGameMessageHandler, EntityBinder>(Lifetime.Transient)` 추가 (기존 3개 핸들러 옆) |

### 타이밍 (안전 확인)

`LOPEntityManager.CreateEntity` → `entityFactory.CreateEntity`(= `CharacterCreator.Create` 호출, 여기서 World.Entity+Health+**Transform+Velocity** 등록) → `entityMap` 세팅 → **`EntityCreated` 발행** → `EntityBinder`가 `WorldMotionSync` 부착 → `WorldMotionSync.Start`(다음 프레임)에서 Transform/Velocity 해석. 등록이 발행보다 먼저라 해석 안전(클라와 동일 흐름).

## 데이터 흐름

```
CharacterCreator: World.Entity{ Health, Transform, Velocity } 등록 (초기값)
       │
LOPEntityManager.CreateEntity → EntityCreated 발행
       │
EntityBinder.OnEntityCreated → WorldMotionSync 부착(캐릭터 한정)
       │
매 틱 AfterPhysicsSimulation (LOPEntityController.SyncPhysics() readback 직후)
  WorldMotionSync ──(UnityEngine→System.Numerics 변환)──▶ World.Transform / World.Velocity (미러)
                                                            (reader 없음 — 후속 슬라이스가 pull)
```

## Out of scope (defer)

- position/rotation/velocity **진실원본 승격** + 이벤트→pull + 물리/뷰/입력/reconciler 전환 = **Stage ④**.
- **Health 진실원본 이행**(서버) — 별도 슬라이스(다음). writer flip부터.
- `PlayerHudCoordinator` 등 **UI/뷰** — 서버 스킵.
- `LOPEntityController`/`LOPEntityView` 생성을 바인더로 이동 — 안 함(클라 패리티: 컨트롤러/뷰는 CharacterCreator 유지).
- Angular velocity, scale — 미사용(클라 Slice B와 동일).

## 조사 근거 (구현 사실)

- 서버 `LOPEntityManager.CreateEntity`(72–88줄)는 클라(65–76줄)와 거의 동일하나 **`EntityCreated` 발행 없음** + 서버 전용 user-entity 매핑 보유. → 발행 1줄 추가가 클라와 정합.
- 서버에 `EntityCreated` 참조 0(struct 부재). `Event.Entity.cs`(서버, `LOP.Event.Entity`)에 `PropertyChange`/`EntityDamage` 등 존재 → 여기에 `EntityCreated` 추가.
- 서버 `IGameMessageHandler` 수명은 `LOPGame`이 구동(`Register()` 77–79줄, `Unregister()` 143–145줄). → `EntityBinder`를 `IGameMessageHandler`로 등록하면 수명 자동 구동.
- 서버 `CharacterCreator`는 현재 `World.Health`만 등록(84–87줄, Transform/Velocity 없음). `World.Transform`/`Velocity`(GameFramework) 타입은 file: 공유라 서버가 이미 컴파일 가능.
- 서버 `LOPEntityController.OnUpdateAfterPhysicsSimulation`(44–48줄)이 `entity.SyncPhysics()`(Rigidbody→LOPEntity readback)를 `AfterPhysicsSimulation`에 실행 → 미러는 그 직후 시점에 권위 위치를 읽음(리스너 순서 무관, reader 없어 한 틱 지연 무해).
- `ToNumerics` 변환 확장은 서버에 없음(클라 `Assets/Scripts/World/` 전용) → 포팅 필요.

## 검증

- **컴파일**: 서버 0에러 (UnityMCP `refresh_unity` + `read_console`, **서버 인스턴스** `LeagueOfPhysical-Server@<hash>` 핀 — 이번 작업은 사용자가 명시 지시한 서버 작업이라 서버 인스턴스 조작 인가). GameFramework는 *불변*(추가 0)이라 클라 영향 없음.
- **런타임(수동)**: 서버 플레이에서 임시 디버그로 `World.Transform.Position`이 `LOPEntity.position`과 일치(이동 중에도 추종)하는지 관찰 후 로그 제거. 동작 변화 없음(reader 없음 → 회귀 0)이 성공.
- **자동화 테스트 신규 없음**(단일 Assembly-CSharp). 코어 `Transform`/`Velocity` 생성/기본값 검증은 GameFramework EditMode에 이미 존재(Slice B에서 추가).

## 문서/브랜치 정책

선례(MVC-decouple 서버 Slice 2 plan도 클라 repo에 커밋: 클라 `008eca7`)대로 **spec·plan은 클라 repo**(설계 허브) 피처 브랜치 `feature/world-core-motion-server`에, **코드는 서버 repo** 피처 브랜치에 커밋한다. 이 spec은 `CLAUDE.md` `@` 자동 로드 목록에 추가.

## 진행

- [x] 패리티 감사(서버 갭 = Motion + Health 확정) + 미러 배치(EntityBinder 구조) 합의
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
- [ ] 구현(서버 repo) → 검증 → 머지

> 후속: 서버 Health 진실원본 이행(writer flip), 그 다음 Stage④(진실원본 승격·물리·netcode). 각자 별도 spec.
