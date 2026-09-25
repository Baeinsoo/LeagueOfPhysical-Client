# 엔티티 Unity 레이어 재구조화 — Umbrella 설계

> **성격:** 이 문서는 *전체 방향·목표·분해*를 담는 umbrella다. 각 슬라이스(S1~S5)는 이 문서를
> 가리키는 **자기 spec/plan을 따로** 갖는다. 상태(어디까지 왔나)는 `docs/ROADMAP.md`가 단일 원천.

## 왜 (문제)

순수 C# World Core(`GameFramework.World`: `Entity` + `Component`)는 새로 다졌지만, 그걸 감싸는
**Unity 프레젠테이션 레이어는 레거시**다. 실측(2026-07-18 감사) 결과:

- **`LOPEntity` = World.Entity의 뚱뚱한 그림자.** 레거시 `MonoEntity`(→`IEntity`) 베이스를 이고
  있어, **병렬 컴포넌트 리스트**(`components: List<IComponent>`)와 **그림자 상태**(position/rotation/
  velocity)를 유지한다. 진실원본은 `World.Entity`인데 `LOPEntity`가 `entityId` 문자열로 물고 이중화.
- **빈 루트 + 형제 흩어짐.** 루트 `Character_{id}`는 컴포넌트 0개. 진짜 엔티티(`LOPEntity`)·`View`·
  `Controller`·`Physics`·`Visual`이 **형제로 나란히** 놓여 단일 앵커가 없다. 모두 `transform.parent`를
  암묵적 루트 핸들로 쓴다.
- **문자열 배선.** 형제 연결이 `Find("Visual")`/`Find("Physics")`/`GetComponentInChildren<...>`. 이름
  바꾸면 조용히 깨진다(typed ref 없음).
- **이중 레지스트리.** Unity측 `LOPEntityManager.entityMap` + World측 `EntityRegistry` + `PhysicsBody`
  핸들이 모두 `entityId` 문자열로만 이어져 desync 위험.
- **레거시 컴포넌트 시스템 병존.** `IComponent`/`MonoComponent`/`LOPComponent`가 World Core의
  `Component`와 **별개로** 살아 있다(옛 MonoBehaviour 엔티티 시스템의 잔재).

**핵심 진단:** "여러 GameObject로 구성"이라는 *개념*은 레거시가 아니다(아래 산업 표준). 문제는
*실현* — 뚱뚱한 그림자 + 빈 루트 + 문자열 배선 + 이중 레지스트리 + 병렬 컴포넌트 시스템. **배선
레거시**이자 **World Core 이행의 미완결분**이다.

## 산업 표준 근거 (2026-07-18 리서치)

- **"엔티티 = 여러 GameObject/Component" 자체는 표준.** Unity 권장 계층(루트=transform+rigidbody+
  controller, 자식=모델/콜라이더/VFX/네임플레이트) · Unreal Actor(컨테이너 + RootComponent[transform] +
  SceneComponent 트리 + 로직용 ActorComponent).
- **현대 표준의 핵심 = 데이터(시뮬) ↔ 뷰(GameObject) 분리.** ECS/Entitas 뷰 리졸버(엔티티=데이터,
  뷰=GameObject, 스포너가 생성, `EntityBinding` 단방향 링크) · Unity DOTS **Companion GameObject**
  (엔티티→GameObject 단방향, transform 복사, back-link 없음, 얇음).
- **네트워크:** 시각 GameObject를 권위 상태와 분리 + 보간("simulation body" vs "interpolation body").

> 출처: Unity Hierarchy 권장 / gamedevbeginner 프로젝트 구조 · Unreal Actors·Components 공식 문서 ·
> Entitas `gameobject-entity-binding` · DOTS Companion GameObject(`com.unity.entities` hybrid
> component) · Gambetta Entity Interpolation.

### 산업 표준 매핑 (LOP → 프레임워크)

| LOP 목표 | 대응 표준 |
|---|---|
| World.Entity = 유일 데이터/시뮬 | DOTS Entity / Entitas Entity (데이터) |
| Unity 트리 = 얇은 뷰/팔로워, 단방향 링크 | DOTS **Companion GameObject** / Entitas **View** |
| 앵커 루트(빈 루트 탈출) | Unreal **Actor + RootComponent** |
| 스포너가 엔티티 수명 보고 뷰 생성/파괴 | Entitas **뷰 리졸버 / reactive AddView 시스템** |
| PhysicsFollower(위치 따라가는 rb) | 키네마틱 컨트롤러(Photon Quantum KCC / Unreal CMC 계열)의 뷰측 follower |

## 목표 모델

**한 문장:** 엔티티 = 순수 C# `World.Entity` **하나**(데이터+시뮬). Unity는 그걸 비추는
**프레젠테이션(뷰/팔로워)만**. 병렬 엔티티/컴포넌트 시스템 없음.

- **단일 진실원본:** `World.Entity`(+ Systems). Unity측은 데이터·시뮬 상태를 **소유하지 않는다**.
- **단방향 링크:** `World.Entity` ──(`entityId`)──▶ Unity 뷰 트리. 뷰→엔티티 백레퍼런스는 조회
  (`EntityRegistry.Get(entityId)`)로 최소화.
- **Actor식 앵커 루트:** 루트가 곧 "이 엔티티"의 단일 앵커(빈 상자 아님). 모델/물리/뷰는 자식.
- **typed 배선:** `Find("...")` 문자열 제거, 직렬화/핸들 참조로.
- **클·서 비대칭(의도):** 둘 다 "World.Entity=진실 + 레거시 컴포넌트 시스템 0" 공유. 프레젠테이션만
  분기 — **클라 = 풀 뷰**(Visual/nameplate/floater), **서버 = 물리만**(충돌 쿼리용 콜라이더, 뷰 없음).

### 결정 규칙 (레거시 컴포넌트 각각의 운명)

> **Unity 엔진 특화 이유(GameObject/Rigidbody/Collider/렌더 등)가 명확하면** → 순수 MonoBehaviour
> (프레젠테이션)로 유지. **없으면(데이터/설정/로직)** → 순수 C# `World.Entity`/`Component`로 이관,
> 중복이면 삭제.

| 레거시 | 든 것 | Unity 이유 | 운명 |
|---|---|---|---|
| `AppearanceComponent` | `visualId`(문자열) | ❌ | **삭제** → visualId는 생성데이터로, View가 읽음 |
| `CharacterComponent`/`ItemComponent` | code + masterdata | ❌ | **삭제** → code는 생성데이터, masterdata는 필요한 쪽이 조회 |
| `EntityTypeComponent` | `EntityType`(enum) | ❌ | **이관** → World 마커 컴포넌트 or 생성데이터 |
| `PhysicsComponent` | Rigidbody/Collider/GameObject | ✅ | **유지(이관)** → 순수 MonoBehaviour `PhysicsFollower` |
| `Local`/`RemoteEntityInterpolator` | LateUpdate·transform 보간 | ✅ | 유지(이미 MonoBehaviour), 배선만 S4에서 정리 |
| `MonoEntity`/`IEntity`/`IComponent`/`MonoComponent`/`LOPComponent` | 병렬 엔티티/컴포넌트 시스템 | — | **삭제**(S3) |

## 범위

- **포함:** 클라이언트 + 서버 (공유 substrate = `GameFramework`라 양쪽 닿음).
- **제외 (YAGNI):** **권위 변경 핸드오프**(원격↔로컬 런타임 스위치). LOP는 스폰/디스폰 때 엔티티를
  재생성하지 생애 중 권위가 안 바뀐다. 단 목표 모델(플래그 기반 배선, 얇은 뷰)이 **나중에 붙일 길은
  열어둔다** — 현 baked-at-creation(생성 시 Local/Remote 못박음)이 그걸 막는 문제. 재개 조건: 몹
  홀리기/빙의/탈것 등 조종 주체가 바뀌는 콘텐츠가 실제로 생길 때.

## 분해 (빌드 순서)

**전략 = strangler(점진 교체).** 매 슬라이스 끝에 게임이 그대로 돌아가야 함(빅뱅 금지). 순서는
**leaf-first** — 딸린 게 적은 것부터 걷어내 그림자를 얇게 만든 뒤, 마지막에 뼈대를 바꾼다. 각
슬라이스는 자기 spec→plan→구현 사이클을 따로 돈다.

| # | 슬라이스 | 무엇 | 왜 이 순서 | 사이드 | 그린 판정 |
|---|---|---|---|---|---|
| **S1** | 설정값 컴포넌트 걷어내기 | Appearance/Character/Item/EntityType 4개를 LOPComponent에서 떼어 생성데이터/World로. MonoBehaviour 삭제 | 가장 안전(순수 데이터). `components`가 Physics만 남게 | 클·서 각자 | 스폰·모델 로드·동작 정상 |
| **S2** | `PhysicsComponent`→`PhysicsFollower` | 물리를 엔티티-컴포넌트 시스템에서 떼어 순수 MonoBehaviour로(World.Transform follow + 콜라이더/PhysicsBody 공유 유지) | 마지막 LOPComponent 제거 → 병렬 리스트 사문화 | 클·서 | 이동·충돌·넉백 정상 |
| **S3** | 레거시 substrate 삭제 | `components`/Attach 사용처 0 확인 후 `MonoEntity`/`IEntity`/`IComponent`/`MonoComponent`/`LOPComponent` 삭제. `LOPEntity`를 얇은 뷰 앵커로 축소(또는 신규 EntityView) | S1·S2가 리스트를 비워야 가능. 공유 GF라 클·서 원자적 | 클·서 동시(원자) | 컴파일 + 전 기능 |
| **S4** | Unity 트리 재구성 | 빈 루트 → 엔티티 앵커 루트, Visual/Physics/뷰를 자식으로, `Find` 문자열 → typed 핸들 | 컴포넌트 시스템 제거 후 앵커 책임이 명확 | 클(트리)/서(최소) | 렌더·수명·충돌 정상 |
| **S5** | 레지스트리 정리 + Creator→Spawner | `entityMap`↔`EntityRegistry` 이중화 축소, 거대 Creator를 표준 스포너/바인더로 분해 | 최상위 오케스트레이션, 아래가 정리된 뒤 | 클·서 | 스폰/디스폰/바인딩 정상 |

**중단 안전성:** S3까지만 해도(뼈대 통일) 그 자체로 개선이 남는다. S4/S5(트리·스포너)는 그 위의
가독·안전 향상.

## Open Decisions (슬라이스에서 확정)

- **S1:** `EntityType`을 World 마커 컴포넌트로 둘지 생성데이터 필드로 둘지(소비자 분포에 따라).
  visualId 런타임 변경(스킨 교체 등) 존재 여부 → 있으면 순수 시드 아님(이벤트 경로 필요).
- **S3:** `LOPEntity`를 *축소*할지 *신규 EntityView로 대체*할지(호출부 규모에 따라).
- **S4:** 앵커 루트에 둘 단일 핸들 컴포넌트의 형태(뷰 인덱스/핸들).
- **S5:** `entityMap`을 얇은 뷰 인덱스로 남길지 완전 제거하고 `EntityRegistry`만 쓸지.

## 상태

Umbrella 승인(2026-07-18). 다음 = **S1 상세 설계**(자기 spec). 진행 상태는 `docs/ROADMAP.md`.
