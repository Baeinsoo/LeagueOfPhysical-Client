# S5b — Creator 데이터/뷰 분리 (데이터 조립기 + 뷰 스포너) 설계

> 엔티티 Unity 레이어 재구조화 umbrella(`2026-07-18-entity-view-rearchitecture-umbrella-design.md`)의
> S5 뒤 절반. 앞 절반 S5a(IEntity 삭제 + 모션 접근자 통일)는 완료·머지. 상태는 `docs/ROADMAP.md`.

## 왜 (문제)

S5a로 `IEntity`가 사라지고 `LOPActor`가 순수 신원 앵커가 됐지만, **엔티티 생성 경로**는 아직 레거시다:
`CharacterCreator.Create`(클 121줄 / 서 100줄) 한 메서드가 **4가지를 뒤섞어** 한다 —
① GameObject `Actor_{id}` 생성, ② **World.Entity 데이터 조립**(Transform/Velocity/Health/Mana/Level/
Stats/Abilities/… 15종), ③ **Unity 뷰 조립**(LOPActor/PhysicsFollower/LOPEntityView/interpolator +
서버 AIController), ④ **게임 배선**(playerContext / 어빌리티 Grant). 데이터와 뷰가 라인 단위로 엉켜 있다.

**뷰 생성이 절반만 분리됨:** 장식 뷰(nameplate/floater)는 이미 `EntityBinder`가 `EntityCreated`에 반응해
만든다(반응형 뷰-리졸버). 하지만 **주요 뷰**(anchor 이후의 물리/모델/보간)는 여전히 크리에이터 인라인이다.
즉 표준 패턴(데이터↔뷰 분리 + 뷰 스포너)이 **반쯤만** 실현돼 있다.

## 산업 표준 매핑

- **데이터 조립기 + 반응형 뷰 스포너** = **Entitas 뷰 리졸버 / DOTS 컴패니언 GameObject**. 엔티티=데이터,
  뷰=GameObject, 스포너가 수명(add)을 보고 뷰 생성. `EntityBinder`가 이미 이 패턴의 씨앗(장식 뷰) — S5b는
  그 스포너를 **모든 뷰**로 키워 패턴을 완성.
- **물리 핸들의 처리**: `World.Entity`가 Unity `Rigidbody` 핸들(`PhysicsBody`)을 든 것은 **DOTS 하이브리드
  컴포넌트(컴패니언)** 패턴 — 순수 코어는 `PhysicsBody` 이름을 안 쓰고 `IMotionBridge` 순수 포트로 조작을
  격리한다(이미 표준 정합). 이 저장까지 순수 포트로 정화(Y)하는 건 **별도 물리 슬라이스로 보류**
  (`[[physicsbody-port-purity-deferred]]`). S5b는 **X — PhysicsBody 콘크리트 유지, 스포너가 구성.**

## 목표 (한 줄)

`CharacterCreator`를 **두 역할**로 가른다 — **Creator = 데이터 직원**(World.Entity 데이터 조립 **인라인** +
anchor + 등록 + LOPActor 반환), 반응형 **뷰 스포너 = 뷰 직원**(모든 Unity 뷰 컴포넌트 + PhysicsBody).
"데이터 직원 / 뷰 스포너" 구조를 완성하되, GF 계약·물리 레이어·매니저는 건드리지 않는다.

> **정정(설계 확정 시):** 초안은 순수 **데이터 조립기** 클래스를 세 번째 역할로 뒀으나, `CreationData`가
> `UnityEngine.Vector3`를 써서 조립 코드는 **Assembly-CSharp**에 살 수밖에 없고 → 프로젝트 제약상
> **EditMode 테스트 불가**(`[[client-test-infra-constraint]]`). 테스트 이득이 없어 별도 조립기 클래스는
> YAGNI. **데이터 조립은 Creator에 인라인 유지**하고 **뷰만 스포너로** 옮긴다(두 역할). 검증은 인게임 스모크.

## 설계

### 두 역할

| 역할 | 무엇 | Unity |
|---|---|---|
| **Creator = 데이터 직원** (`CharacterCreator`/`ItemCreator` 축소, per-side) | `CreationData` → World.Entity 컴포넌트 조립(Transform/Velocity/EntityKind/MasterDataRef/Appearance/Health/Mana/Level/Stats/Abilities/StatusEffects/MotionContributions/Ownership·InputBuffer·Simulated + 어빌리티 Grant, **인라인**). **PhysicsBody 제외**(뷰가 만든 rb 필요). + GameObject+`LOPActor` **앵커** 생성 → `EntityRegistry.Add` → `LOPActor` 반환. | 앵커 GameObject/LOPActor만 |
| **뷰 스포너 = 뷰 직원** (클: `EntityBinder` 확장 / 서: 신설, per-side) | `EntityCreated` 반응 → `PhysicsFollower`/`LOPEntityView`/interpolator(+장식 뷰, +서버 AIController) 부착 → PhysicsFollower의 rb로 `worldEntity.Add(new PhysicsBody(...))`. playerContext.entityView 세팅(클). | ✅ 뷰 전담 |

> **isUser/isPlayer 재판정 (스포너):** 스포너는 `EntityCreated(actor)`로 entityId만 받으므로 CreationData의
> isUser/isPlayer를 다시 판정한다 — 클라=`gameDataStore.userEntityId == actor.entityId`, 서버=World.Entity에
> `Ownership` 유무(`entityRegistry.Get(id).Has<Ownership>()`, 플레이어면 Creator가 이미 추가). Kind 분기는
> `EntityKind`(Character/Item)로. (Creator가 데이터에 그 마커들을 이미 넣어서 스포너가 되읽을 수 있음.)
> **playerContext.entity(=LOPActor)는 Creator가 앵커 생성 직후 세팅**(publish 전 동기라 다른 EntityCreated
> 구독자가 안전하게 읽음); .entityView만 스포너가 뷰 생성 후 세팅.

### 생성 흐름 (동기 — 한-프레임 공백 없음)

```
manager.CreateEntity<LOPActor, CharacterCreationData>(data)
 → Creator.Create(data):
      worldEntity = new Entity(id); worldEntity.Add(...World 데이터 컴포넌트, PhysicsBody 제외...) + 어빌리티 Grant
      entityRegistry.Add(worldEntity)
      root = new GameObject("Actor_{id}"); actor = root.AddComponent<LOPActor>(); actor.Initialize(data)
      (클) isUser면 playerContext.entity = actor
      return actor
 → manager: entityMap[id] = actor; publisher.Publish(new EntityCreated(actor))   // MessagePipe 동기
      → 뷰 스포너.OnEntityCreated(actor)(같은 호출 내 즉시 실행):
           PhysicsFollower 부착·Initialize → LOPEntityView·interpolator·(장식·AIController) 부착
           worldEntity.Add(new PhysicsBody(follower.rb, follower.collider))
           (클) isUser면 playerContext.entityView = view
 → return actor   // 완전히 조립된 상태로 반환
```

- **동기 발행**(GlobalMessagePipe.Publish는 동기)이라 스포너가 `CreateEntity` 반환 전에 뷰+PhysicsBody를
  다 붙인다 → 반환 계약(`CreateEntity<LOPActor,…> : LOPActor`) 유지, PhysicsBody 누락 틱 없음.
- **GF 추상(`IEntityCreator`/`IEntityManager`) 무변경** — 앵커를 크리에이터가 만들어 반환하므로 계약 그대로.

### 클·서 비대칭 (스포너도 사이드별)

- **클라 뷰 스포너 = `EntityBinder` 확장**: 지금 장식 뷰만 붙이던 것을 **주요 뷰(PhysicsFollower/
  LOPEntityView/Local·RemoteEntityInterpolator) → 장식 뷰 순서**로 확장. isUser 분기(playerContext.entity/
  entityView 세팅 + LocalEntityInterpolator vs RemoteEntityInterpolator)도 여기로. **한 구독자**가 순서를
  결정론적으로 소유.
- **서버 뷰 스포너 = 신설**: 서버엔 EntityBinder 없음. `EntityCreated` 구독 entry point 신설 → 비-플레이어에
  `LOPAIController`(+ `EnemyBrain` 주입), 테스트렌더 `LOPEntityView` 부착. (서버는 장식 뷰 없음.)

### 파괴 (무변경)

`LOPEntityManager.DestroyMarkedEntities`가 root GameObject 파괴 + `ICleanup` 정리 + `entityRegistry.Remove`.
스포너가 붙인 컴포넌트도 같은 root 자식이라 함께 정리됨(현행과 동일). 스포너에 별도 unbind 없음.

## 범위

- **포함:** 클라 + 서버. `CharacterCreator`(+`ItemCreator`) 분해, 데이터 조립기 신설, 뷰 스포너(클 확장/서
  신설). GF·LOP-Shared·물리 레이어 무변경.
- **제외 (보류/YAGNI):**
  - **`entityMap` → 스포너 이전 / `IEntityManager` 해체**: 매니저는 지금처럼 entityMap(뷰 인덱스)+수명 소유
    유지. 이전은 큰 ripple에 payoff 불확실 → 보류. (많은 `GetEntities<LOPActor>()` 호출이 실은 World 데이터만
    원해 `EntityRegistry`로 옮길 여지는 있으나 별건.)
  - **PhysicsBody 순수 포트화(Y)**: 별도 물리 슬라이스(`[[physicsbody-port-purity-deferred]]`).
  - **어휘 rename(`entity`→`actor`)**: 별도 기계적 패스(아래 S5b-3). 의미 변경과 분리(리뷰 가독).
  - **GF 계약 변경**(크리에이터가 World.Entity 반환): 앵커-in-creator 타협으로 불필요.

## 분해 (빌드 순서, 각 슬라이스 끝 컴파일·인게임 그린)

| # | 슬라이스 | 무엇 | 그린 판정 |
|---|---|---|---|
| **S5b-1** | 클라 뷰 → 스포너 | 클라 `CharacterCreator`/`ItemCreator`의 주요 뷰 컴포넌트(PhysicsFollower/LOPEntityView/interpolator) + PhysicsBody + playerContext.entityView를 빼 **`EntityBinder` 확장**(Kind·isUser 분기)으로. 크리에이터=데이터+앵커+등록(+playerContext.entity). | 컴파일 클린, 인게임(스폰/뷰/물리/보간/장식/playerContext/카메라·HUD) 무변화 |
| **S5b-2** | 서버 뷰 → 스포너 | 서버 `CharacterCreator`/`ItemCreator`의 뷰(LOPEntityView/AIController) + PhysicsBody를 빼 **서버 뷰 스포너 신설**(EntityCreated 구독 entry point). 크리에이터=데이터+앵커+등록. | 컴파일 클린, 인게임(스폰/AI/테스트렌더/물리) 무변화 |
| **S5b-3** | 어휘 rename | `entity`/`entities`/`LOPEntities` 식별자 → `actor`(타입은 이미 LOPActor). 순수 기계적. | 컴파일 클린 |

> 클·서를 별도 슬라이스로: 뷰 스포너가 사이드별(클=EntityBinder 확장 / 서=신설)이라 독립 그린·인게임 가능.
> 데이터 조립은 각 크리에이터에 **인라인 유지**(별도 조립기 클래스 없음 — 위 정정).

## 테스트 / 그린 판정

- **EditMode 없음(불가):** 대상 코드(크리에이터/스포너)는 전부 Assembly-CSharp(Unity 타입)라 EditMode 테스트
  불가(`[[client-test-infra-constraint]]`). 검증 = **컴파일 + 인게임 스모크**.
- **컴파일:** 클·서 UnityMCP `read_console`(unity_instance 명시).
- **인게임(사용자):** 스폰·모델 로드·이동·충돌·아이템·넉백/데미지·롤백·원격 보간·서버 AI·장식 뷰(HP바/데미지
  플로터)·playerContext(카메라/HUD) 무변화.
- **클·서 비대칭 유지.**

## 위험 & 완화

- **스포너 동기 실행 의존**: 뷰+PhysicsBody가 `CreateEntity` 반환 전에 붙어야 반환 계약·물리 틱이 성립.
  GlobalMessagePipe.Publish가 동기임을 전제 — plan에서 확인, 스폰 스모크로 검출.
- **구독자 순서**: 한 스포너가 주요→장식을 순서 소유(구독자 쪼개지 않음)로 순서 의존 제거. 기존 `EntityCreated`
  다른 구독자(`PlayerHudCoordinator`)와의 순서는 무변(그들은 주요 뷰에 비의존).
- **서버 스포너 신설 배선**: DI entry point 등록 + `EntityCreated` 구독 + `EnemyBrain` 주입 경로. 누락 시 AI/
  테스트렌더 안 뜸 → 스폰 스모크로 검출.
- **PhysicsBody 타이밍**: 스포너가 rb 만든 직후 `Add(PhysicsBody)` — 동기라 등록~PushMotion 사이 누락 없음.
- **do-not-commit 서버 픽스처**(GameRuleSystem/ConfigureRoomComponent/DefaultVolumeProfile/GraphicsSettings)
  미스테이징 유지.

## Open Decisions (plan/구현에서 확정)

- 클라 `EntityBinder` 확장 시 이름 유지 vs `EntityViewSpawner`류로 rename(역할이 커지므로). 업계어=뷰 리졸버/
  스포너. rename churn과 저울질(어휘 패스 S5b-3와 묶을지) — plan은 **이름 유지**(EntityBinder)로 잡고 rename은 S5b-3에 위임.
- 서버 뷰 스포너 클래스명(`EntityViewSpawner`류)/등록 위치(`GameLifetimeScope` entry point).
- 스포너 다중 구독자 순서: `playerContext.entityView`를 읽는 다른 `EntityCreated` 구독자(예: `PlayerHudCoordinator`)가
  스포너보다 먼저 도는지 확인 — 있으면 스포너를 선순위 등록 or 그 읽기를 .entity로 대체(plan에서 실확인).

## 상태

설계 승인 대기(2026-07-18). 다음 = plan(`writing-plans`). 진행 상태 `docs/ROADMAP.md`.
관련: umbrella `2026-07-18-entity-view-rearchitecture-umbrella-design`, `[[entity-unity-layer-rearchitecture]]`,
`[[physicsbody-port-purity-deferred]]`.
