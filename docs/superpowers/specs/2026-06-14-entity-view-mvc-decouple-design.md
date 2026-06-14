# Entity View/Controller — GameFramework MVC 베이스 디커플 + 제거

**Date:** 2026-06-14
**Branch:** `feature/entity-view-separation` (현재 작업 브랜치)
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [Entity System Design](../../entity-system-design.md) · [LOP 저장소 토폴로지](../../lop-repo-topology.md)

## Goal

GameFramework의 옛 MVC Entity 뷰 계층 — `IEntityView`, `IEntityController`, `MonoEntityView<,>`, `MonoEntityController<>` — 을 **제거**한다. 이 제네릭 베이스에 결합돼 있는 클·서 소비자(7개 클래스)를 평범한 `MonoBehaviour, ICleanup`로 **디커플**한 뒤, 더 이상 참조가 없어진 4개 GF 타입을 삭제한다.

이 작업은 **중간 단계**다. 최종 목표 구조(아래 "궤적")는 World Core 연결 아키텍처 문서에 박힌 "CBD 안쪽 + 바깥 뷰가 `World.Entity`를 pull / `WorldEventBuffer` event 구독, 스포너가 `EntityRegistry` 수명 구독"이며, 그 수렴은 **별도로 진행 중인 World Core 이행**이 담당한다. 이번 작업은 그 이행이 레거시 제네릭 베이스 indirection 없이 진행되도록 길을 닦으면서, 동시에 GF MVC 타입을 즉시 제거한다.

## 궤적 (이번 작업의 위치)

```
[지금]   LOPEntityView : MonoEntityView<LOPEntity, LOPEntityController>
         GF MVC 베이스에 결합, 레거시 LOPEntity 바인딩
   │  ← 이번 작업 (디커플 + GF 타입 삭제, 3 슬라이스)
   ▼
[중간]   LOPEntityView : MonoBehaviour, ICleanup
         GF 의존 제거. 여전히 레거시 LOPEntity 바인딩. 죽은 entityController 링크 제거됨
   │  ← 별도: World Core 이행 (위치/속도/외형/컴포넌트를 World.Entity로 이식 +
   │         스포너를 EntityRegistry 수명 구독으로 + 연속 pull / 이산 event 전환)
   ▼
[최종]   World.Entity 바인딩 뷰 (문서의 목표 구조). LOPEntity 자체 소멸
```

디커플 후 뷰는 제네릭 MVC 간접층이 사라진 평범한 MonoBehaviour 껍데기가 된다. 최종 이행 때는 이 껍데기에서 `entity`의 출처를 `LOPEntity` → `World.Entity`로 바꾸고 데이터 소스를 pull/event로 교체하면 된다 — 디커플은 그 재작성의 깨끗한 출발점이지 되돌릴 작업이 아니다. 중간 상태 자체가 현재보다 단순(불필요한 제네릭 + 죽은 `entityController` 링크 제거)하므로 이행이 늦어져도 순손해가 없다.

## 현재 구조 (조사 결과)

### 제거 대상 (GameFramework `Runtime/Scripts/Entity/`)

| 타입 | 내용 |
|---|---|
| `IEntityController : ICleanup` | `IEntity entity` |
| `IEntityController<T>` | `new T entity` |
| `IEntityView : ICleanup` | `IEntity entity`, `IEntityController entityController` |
| `IEntityView<TEntity, TController>` | `new TEntity entity`, `new TController entityController` |
| `MonoEntityController<T>` | `MonoBehaviour` + `entity`/`SetEntity`/virtual `Cleanup` |
| `MonoEntityView<TEntity, TController>` | `MonoBehaviour` + `entity`/`entityController`/`SetEntity`/`SetEntityController`/virtual `Cleanup` |

### 소비자 (베이스를 상속하는 7개)

**클라이언트:**
- `Assets/Scripts/Entity/LOPEntityView.cs` — `MonoEntityView<LOPEntity, LOPEntityController>`
- `Assets/Scripts/Entity/LOPEntityController.cs` — `MonoEntityController<LOPEntity>`
- `Assets/Scripts/UI/WorldSpace/DamageFloaterEmitter.cs` — `MonoEntityView<LOPEntity, LOPEntityController>`
- `Assets/Scripts/UI/WorldSpace/CharacterNameplate.cs` — `MonoEntityView<LOPEntity, LOPEntityController>`

**서버:**
- `Assets/Scripts/Entity/LOPEntityView.cs` — `MonoEntityView<LOPEntity, LOPEntityController>`
- `Assets/Scripts/Entity/LOPEntityController.cs` — `MonoEntityController<LOPEntity>`
- `Assets/Scripts/Entity/LOPAIController.cs` — `MonoEntityController<LOPEntity>`

### 디커플을 깨끗하게 만드는 두 사실 (전수 확인)

1. **`entityController` 링크는 사문(死文)** — `MonoEntityView`의 `entityController` getter를 읽는 코드는 클·서 어디에도 없다. `SetEntityController(...)` 호출만 존재한다(클라 4곳: `CharacterCreator:70`, `ItemCreator:45`, `EntityViewSpawner:50,55` / 서버 2곳: `CharacterCreator:64`, `ItemCreator:45`). 따라서 디커플 시 `entityController`/`SetEntityController`를 통째로 제거하고 그 호출도 지운다.
2. **다형적 인터페이스 의존 없음** — `IEntityView`/`IEntityController` 인터페이스는 위 11개 파일(GF 정의 4 + 소비자 7) 밖에서 전혀 참조되지 않는다. `entityView`를 보유하는 곳(`IPlayerContext`, `PlayerContext`, `SnapReconciler`, `SnapInterpolator`, `ServerStateReconciler`)은 모두 **구상 타입 `LOPEntityView`**로 선언하고 `entityView.visualGameObject`만 읽는다. GF의 다른 코드(`EntityFactory`, `IEntityManager` 등)도 이 4타입을 참조하지 않는다 → 삭제 안전.

### Cleanup 디스패치 (베이스와 무관, 유지)

엔티티 파괴 정리는 MVC 베이스가 아니라 `ICleanup`로 디스패치된다 — `LOPEntityManager.DestroyMarkedEntities`가 `root.GetComponentsInChildren<ICleanup>(true)`로 모아 `Cleanup()`을 호출. 디커플된 클래스가 `ICleanup`를 직접 구현하면 이 경로는 그대로 동작한다.

## 설계 결정

| 결정 | 선택 | 이유 |
|---|---|---|
| 끝점(scope) | **디커플 우선** | GF MVC 타입을 즉시 제거. 뷰의 World Core 완전 이행은 별도(아래 Out of Scope) |
| 공유 베이스 신설? | **안 함 (인라인)** | 7개 클래스를 각자 `MonoBehaviour, ICleanup`로. 제네릭 MVC를 LOP에 되살리지 않음. 중복은 클래스당 `entity`+`SetEntity` ~4줄로 무시 가능. 각 유닛이 자기완결적 |
| `entityController` | **제거** | 읽는 곳이 없는 사문 |
| `ICleanup` | **유지** | 정리 디스패치 계약. 각 클래스가 직접 구현 |
| `entity` 타입 | `LOPEntity` 유지 | 디커플은 데이터 소스를 바꾸지 않음(중간 단계). World.Entity 전환은 후속 이행 |

### 디커플 후 형태 (대표 — LOPEntityView)

```csharp
public class LOPEntityView : MonoBehaviour, ICleanup   // was: MonoEntityView<LOPEntity, LOPEntityController>
{
    public LOPEntity entity { get; private set; }
    public void SetEntity(LOPEntity entity) => this.entity = entity;

    // ... 기존 본문(visualGameObject, Start, OnPropertyChange, UpdateVisual 등) 동일 ...

    public void Cleanup()        // was: override + base.Cleanup()
    {
        // ... 기존 정리 본문 ...
        entity = null;           // base.Cleanup()이 하던 null 처리 보존(선택)
    }
}
```

- 컨트롤러(`LOPEntityController`, `LOPAIController`)도 동일: `MonoEntityController<LOPEntity>` → `MonoBehaviour, ICleanup`, `entity`/`SetEntity` 자체 보유, `Cleanup`에서 `override`/`base.Cleanup()` 제거.
- 장식 뷰(`DamageFloaterEmitter`, `CharacterNameplate`)는 위에 더해 **쓰지 않던 `entityController` 보유·세팅을 갖지 않는다** (이들은 `SetEntity`만 받음, 머리 위치는 자체 `_entityView` 조회로 처리).

## 슬라이스 (3개, 저장소별 원자적)

> Slice 1·2는 서로 독립(클라 디커플은 서버를, 서버는 클라를 필요로 하지 않음). Slice 3은 둘 다 끝나야 하는 게이트다 — GF 타입을 삭제하려면 양쪽 소비자가 먼저 의존을 끊어야 한다. 디커플은 동작 변화 없는 베이스 교체라 리스크는 컴파일 + `ICleanup` 디스패치 유지뿐이며 둘 다 확인됐다.

### Slice 1 — 클라이언트 디커플 (LeagueOfPhysical-Client)

**변환 (베이스 교체):**
- `Assets/Scripts/Entity/LOPEntityView.cs` → `MonoBehaviour, ICleanup`
- `Assets/Scripts/Entity/LOPEntityController.cs` → `MonoBehaviour, ICleanup`
- `Assets/Scripts/UI/WorldSpace/DamageFloaterEmitter.cs` → `MonoBehaviour, ICleanup` (+ `entityController` 미보유)
- `Assets/Scripts/UI/WorldSpace/CharacterNameplate.cs` → `MonoBehaviour, ICleanup` (+ `entityController` 미보유)

**와이어 정리 (`SetEntityController` 제거):**
- `Assets/Scripts/EntityCreator/CharacterCreator.cs:70` — `view.SetEntityController(controller);` 제거 (controller는 자체 동작 위해 계속 생성)
- `Assets/Scripts/EntityCreator/ItemCreator.cs:45` — `view.SetEntityController(controller);` 제거
- `Assets/Scripts/Game/EntityViewSpawner.cs` — emitter/nameplate에 대한 `SetEntityController(...)` 2줄(50,55) 제거 + 이를 위해서만 존재하던 `controller` 조회(45) 제거

**검증:** 클라 컴파일 통과(GF 베이스는 아직 존재, 단지 미사용). 플레이 1라운드 — 비주얼 로딩, 이동/공격/피격 애니메이션, 데미지 숫자 플로터, 머리 위 네임플레이트 HP바, 카메라 타깃팅(`playerContext.entityView.visualGameObject`) 정상. 엔티티 파괴 시 `ICleanup` 정리(에러 0) 정상. World Core slice 3 회귀(데미지→사망 로그) 정상.

### Slice 2 — 서버 디커플 (LeagueOfPhysical-Server)

**변환:**
- `Assets/Scripts/Entity/LOPEntityView.cs` → `MonoBehaviour, ICleanup`
- `Assets/Scripts/Entity/LOPEntityController.cs` → `MonoBehaviour, ICleanup`
- `Assets/Scripts/Entity/LOPAIController.cs` → `MonoBehaviour, ICleanup` (`brain` 보유 유지)

**와이어 정리:**
- `Assets/Scripts/EntityCreator/CharacterCreator.cs:64` — `view.SetEntityController(controller);` 제거
- `Assets/Scripts/EntityCreator/ItemCreator.cs:45` — `view.SetEntityController(controller);` 제거

**검증:** 서버 컴파일 통과. 한 라운드 — AI 동작(`LOPAIController.brain.Think`), 엔티티 스폰/동기화, 파괴 정리 정상.

### Slice 3 — GameFramework MVC 타입 삭제 (GameFramework)

**삭제 (+ `.meta`):**
- `Runtime/Scripts/Entity/IEntityView.cs`
- `Runtime/Scripts/Entity/IEntityController.cs`
- `Runtime/Scripts/Entity/MonoEntityView.cs`
- `Runtime/Scripts/Entity/MonoEntityController.cs`

**검증:** 클·서 양쪽 컴파일 통과(file: 참조라 GF 변경이 양쪽에 즉시 반영). 양쪽 한 라운드 정상.

## GUID / .meta 정책

- **삭제 파일(.meta 포함)**: 4개 GF 스크립트는 정적 클래스/인터페이스로 prefab·scene의 SerializedField가 GUID로 참조하지 않는다(MonoBehaviour지만 `MonoEntityView`/`MonoEntityController`는 *추상 제네릭 베이스*라 씬에 직접 부착되지 않고, 부착되는 건 구상 `LOPEntityView` 등). `git rm`으로 `.cs`+`.meta` 함께 삭제.
- **디커플 대상(LOP 7개)**: 파일·클래스명 유지 → `.meta` GUID 보존. 씬/prefab의 컴포넌트 참조 그대로 유효. 베이스만 바뀌므로 직렬화 영향 없음.

## 테스트 전략

- **신규 자동화 테스트 없음.** 디커플은 동작 변화 없는 베이스 교체이고, 클라/서버 모두 단일 Assembly-CSharp라 EditMode asmdef 직접 참조가 불가(기존 슬라이스 기조와 동일). 핵심 검증은 **컴파일 + 런타임 플레이**다.
- 각 슬라이스 끝에 위 "검증" 항목 수행. 한 번에 한 변경(슬라이스) 원칙.

## Out of Scope (별도 작업)

- **뷰 레이어 World Core 완전 이행** — `LOPEntity` 데이터(위치/속도/외형/컴포넌트)를 `World.Entity`로 이식, 뷰 스포너를 `EntityRegistry` 수명 구독으로, 뷰를 `World.Entity` pull + `WorldEventBuffer` event 구독으로 전환. 문서의 최종 목표 구조이며, 진행 중인 World Core 이행이 담당(현재 데이터/이벤트 slice 1~3 완료, `World.Entity`엔 `Health`만 이식됨). 이 spec은 그 이행의 출발점을 마련할 뿐 이행 자체를 포함하지 않는다.
- `EntityViewSpawner`를 `EntityCreated`(레거시) → `EntityRegistry` 수명 구독으로 바꾸는 것 — 위 이행의 일부.
- 작은 LOP-로컬 공유 베이스 도입(설계에서 기각).

## Open Decisions

- [ ] `Cleanup()`에서 `entity = null` 보존 여부 — base가 하던 동작이나 실효성 낮음. 슬라이스 1 구현 시 확정(보존 권장 — 거의 무비용, 기존 동작 parity).

## 진행

- [x] 현재 구조 조사 (소비자 7 + 죽은 entityController 링크 + ICleanup 디스패치 + 다형 의존 부재 확인)
- [x] 끝점/접근 결정 (디커플 우선, 인라인, 3 슬라이스)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 Slice 1 구현 plan 작성
- [ ] 실행

> 슬라이스는 **Slice 1부터** plan→실행→검증 순으로 진행한다. Slice 3(GF 삭제)는 1·2 완료 후 게이트로 수행한다.
