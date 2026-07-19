# 로직/시뮬을 LOPActor에서 World.Entity로 분리 (설계)

> 엔티티 Unity 레이어 재구조화(S1~S5, 완료)의 **후속 구조 정리**. S5까지는 *뷰 레이어 구조*를
> 표준화했고, 이 슬라이스는 *로직↔뷰 결합*의 레거시를 걷어낸다. 상태는 `docs/ROADMAP.md`.

## 왜 (문제)

S5로 `LOPActor`는 얇은 신원 앵커(`entityId` + `Initialize`뿐, 나머진 MonoBehaviour 기본)가 됐다.
그런데 **소비처 감사(2026-07-19)** 결과, `LOPActor`를 참조하는 곳 대부분이 **레거시 과참조**였다:

- **진짜 Unity 앵커가 필요한 곳(LEGIT) ~6역할:** 크리에이터·뷰 스포너(`EntityBinder`/`EntityViewSpawner`)·
  `LOPEntityManager`(teardown)·콜라이더→엔티티 브리지(`LOPOverlapQuery`/`PhysicsFollower`). `actor.gameObject`/
  `GetComponentInParent<LOPActor>`로 **GameObject가 실제로 필요**.
- **ID-ONLY 과참조 ~28:** `LOPActor`를 통째로 들고 **`actor.entityId`만 읽어 `entityRegistry.Get(id)`** 하는 곳.
  `LOPEntity`가 뚱뚱했던 시절의 잔재. 이 중 **순수 로직/시뮬**(AI·입력처리·재조정·메시지 라우팅·게임룰·생성데이터
  팩토리)이 이 슬라이스의 대상.
- **즉시 명확 2건:** `RemoteEntityInterpolator.actor`(할당만·안 읽힘=죽은 필드), `PlayerContext.actor`
  (LEGIT 소비자 0 — 전부 id만).

**업계 표준 정합:** 결정론 ECS+Unity(Photon Quantum/DOTS)에서 **시뮬 로직은 엔티티/id(데이터)로 동작하고,
GameObject는 뷰 레이어만 보유**한다(Quantum: 로직=EntityRef / DOTS: "엔티티가 GameObject를 주도, 역은 금지").
LOP는 데이터가 `World.Entity`에 있는데도 로직이 `LOPActor`(GameObject)를 거쳐 id를 얻는 게 표준 이탈.

## 산업 표준 매핑

- **로직은 데이터 참조로 동작** — Quantum `Frame`/`EntityRef`, DOTS `Entity`/`SystemAPI`. LOP 대응 =
  `EntityRegistry` + `World.Entity`. 로직이 `LOPActor`(뷰)를 안 든다.
- **뷰 컴포넌트가 엔티티를 id로 참조**하는 건 표준(Quantum `EntityViewComponent`가 `EntityRef` 보유) —
  그래서 뷰/UI는 이 슬라이스 **대상 아님**(그대로 둠).

## 목표 (한 줄)

순수 로직/시뮬 사이트가 `LOPActor`/`LOPEntityManager`(뷰) 대신 **`GameFramework.World.Entity` +
`EntityRegistry`(데이터)** 로 동작하게 바꾼다. `LOPActor`는 뷰 레이어(~6역할)만 보유. **값 동치**(id 동일 →
동작 무변화).

## 설계

### 전환 패턴 (값 동치)

| 현재 (레거시 과참조) | 후 |
|---|---|
| `entityManager.GetEntities<LOPActor>()` | `entityRegistry.All` (World.Entity 순회) |
| `actor.entityId` | `worldEntity.Id` |
| `entityManager.GetEntity<LOPActor>(id)` | `entityRegistry.Get(id)` |
| `entityManager.GetEntityByUserId<LOPActor>(userId)` | userId→entityId 매핑 후 `entityRegistry.Get(id)` (매핑 유지처는 아래) |
| `playerContext.actor`(LOPActor) | `playerContext.entityId`(string) — `.entityView`(LOPEntityView)는 뷰라 유지 |
| `IBrain<LOPActor>.Think(LOPActor, dt)` | `IBrain.Think(World.Entity worldEntity, dt)` — 제네릭 드롭 |
| 서버 `IEntityCreationDataCreator.Create(LOPActor)` / `IEntityCreationDataFactory.Create(LOPActor)` | `Create(World.Entity)` |

### IBrain 제네릭 드롭 (배선)

`IBrain<T> where T : MonoBehaviour`(S5a에서 제약)는 `LOPActor`를 담으려던 것. 로직을 World.Entity로
옮기면 `World.Entity`는 MonoBehaviour가 아니라 `<T:MonoBehaviour>`에 못 들어간다 → **제네릭을 없애고**:

```csharp
public interface IBrain { void Think(GameFramework.World.Entity worldEntity, double deltaTime); }
public class EnemyBrain : IBrain { public void Think(World.Entity worldEntity, double dt) { ... } }
```

`LOPAIController`(actor GameObject에 붙은 뷰-브리지 MonoBehaviour)는 `actor`를 계속 들되, `[Inject]
EntityRegistry`로 `worldEntity = entityRegistry.Get(actor.entityId)`를 해석해 `brain.Think(worldEntity, dt)`
호출. (컨트롤러는 뷰 레이어라 actor 보유 정당; 브레인은 순수 로직이라 World.Entity만 본다.)

### 범위 (로직 사이트만)

**서버:**
- `AI/EnemyBrain.cs` (+ `AI/IBrain.cs` 제네릭 드롭 + `AI/LOPAIController.cs` 배선)
- `Game/LOPRunner.cs` — 3루프(ProcessInput / SendInputTimingFeedback / GetEntityByUserId 경로)
- `Game/MessageHandler/Game.Entity/Input/Info.MessageHandler.cs`
- `Game/GameRuleSystem.cs`
- `EntityCreationDataFactory/EntityCreationDataFactory.cs` + `IEntityCreationDataFactory.cs` +
  `IEntityCreationDataCreator.cs` + `CharacterCreationDataCreator.cs` + `ItemCreationDataCreator.cs`
- `World/DeathCascadeSystem.cs`

**클라:**
- `Entity/Reconciler.cs`, `Game/LOPRunner.cs`, 클 메시지핸들러
- `Game/PlayerContext.cs` + `Game/IPlayerContext.cs` — `actor`(LOPActor) → `entityId`(string). 강등에 딸린
  사소 변경: `UI/CharacterHud/CharacterHudViewModel.cs`·`UI/Stats/StatsViewModel.cs`가 `playerContext.actor.
  entityId` → `playerContext.entityId`.
- `Entity/RemoteEntityInterpolator.cs` — 죽은 `actor` 필드 삭제(+ `EntityBinder`의 할당 제거).

### 범위 밖 (의도적)

- **뷰/UI 컴포넌트 본체**: `LOPEntityView`·`Local/RemoteEntityInterpolator`(actor 필드 삭제 외)·
  `DamageFloaterEmitter`·`CharacterNameplate`·UI VM — 표준 뷰 패턴(id로 entity 참조)이라 유지.
- **GameFramework / LOP-Shared** 무변경(`IEntityManager` 등 GF 추상은 그대로 — 로직이 *안 쓸* 뿐).
- **후속 슬라이스(사용자 아이디어, 별도)**: `LOPEntityView`를 `LOPActor`에 **통합**(이름은 **Actor 유지** —
  Unreal식 뷰+컨트롤러 겸 앵커). B 이후 별도 brainstorm. `[[entity-unity-layer-rearchitecture]]`.

## 미정 세부 (plan에서 확정)

- `userEntityMap`(userId→entityId, 서버 매니저 보유)을 계속 매니저에 둘지 — 로직이 `GetEntityByUserId
  <LOPActor>` 대신 매핑+registry를 쓰려면 매핑 접근이 필요. 매니저에 `GetEntityIdByUserId(userId):string`을
  두거나 기존 유지. (뷰-매니저에 얇게 남기는 게 현실적.)
- 로직 클래스가 `Runner.current.entityManager` 대신 `[Inject] EntityRegistry`를 직접 받도록 DI 배선(대부분
  이미 registry 주입 보유 — EnemyBrain/Reconciler/CreationDataFactory 등).
- `Create(World.Entity)` 전환 시 팩토리 호출부(GameRuleSystem/DeathCascade)가 `GetEntity<LOPActor>` 대신
  `entityRegistry.Get(id)`로 World.Entity를 얻어 넘김.

## 분해 (서브슬라이스, 각 컴파일+인게임 그린)

| # | 슬라이스 | 무엇 | 그린 판정 |
|---|---|---|---|
| **B1** | 클라 로직 + 즉시 2건 | `PlayerContext.actor→entityId`, 죽은 `RemoteEntityInterpolator.actor` 삭제, `Reconciler`/클 `LOPRunner`/클 메시지핸들러/VM을 `entityId`·`entityRegistry`로 | 클 컴파일 클린, 인게임(내 캐릭 스폰·이동·롤백·HUD/Stats·카메라) 무변화 |
| **B2** | 서버 로직 | `EnemyBrain`/`IBrain`/`LOPAIController`, `LOPRunner` 루프, 메시지핸들러, `GameRuleSystem`, `CreationDataFactory`/creators(`Create(World.Entity)`), `DeathCascade`를 `World.Entity`/`entityRegistry`로 | 서 컴파일 클린, 인게임(AI 이동·공격·스폰·디스폰/exp·아이템·입력) 무변화 |

## 테스트 / 그린 판정

- **EditMode 없음(불가)**: 대상이 Assembly-CSharp + Unity 타입(Vector3 등) → `[[client-test-infra-constraint]]`.
  검증 = **컴파일(클·서 UnityMCP) + 인게임 스모크**.
- **값 동치**: `actor.entityId`와 `worldEntity.Id`는 같은 문자열 → 라우팅/판정 동일. 동작 무변화가 기대치.
- **do-not-commit 서버 픽스처** 미스테이징 유지.

## 위험 & 완화

- **로직이 실은 GameObject가 필요했던 곳** → 감사에서 대상 전부 ID-ONLY 확인. 만약 전환 중 `.gameObject`/
  `GetComponent` 접근이 발견되면 그 사이트는 LEGIT로 재분류(뷰 레이어 잔류). plan 태스크가 grep로 확인.
- **IBrain 시그니처 변경** → EnemyBrain 유일 구현체 + LOPAIController 유일 호출부(S5a 확인). 컴파일이 누락 검출.
- **`Create(World.Entity)` 전환** → 서버 인터페이스+구현+호출부(GameRuleSystem/DeathCascade) 한 슬라이스(B2)
  원자 변경. 컴파일 그린으로 검증.
- **`GetEntities<LOPActor>()` 잔여 호출** → 로직 전환 후 남는 호출자는 뷰-레이어/매니저뿐이어야. plan에서 확인
  (0이어도 GF 인터페이스라 메서드 자체는 유지).

## 상태

설계 승인 대기(2026-07-19). 다음 = plan(`writing-plans`). 진행 상태 `docs/ROADMAP.md`.
관련: `[[entity-unity-layer-rearchitecture]]`, audit(감사) 2026-07-19.
