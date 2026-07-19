# Actor–View Facade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `LOPActor`를 엔티티의 단일 대표(파사드)로 만들어, 외부 코드가 `LOPEntityView`를 직접 참조하지 않고 `actor.visualGameObject` 하나로 뷰에 접근하게 한다.

**Architecture:** `LOPEntityView`(렌더링 전담)는 삭제하지 않고 유지한다. `LOPActor`가 그 View를 소유(`SetView`)하고 `visualGameObject`를 위임 노출한다. 스포너(`EntityBinder`)가 View 생성 직후 Actor에 등록한다. 소비처(보간기·플레이어 컨텍스트·카메라·월드스페이스 UI)의 `entityView`(LOPEntityView) 참조를 `actor`(LOPActor)로 전환한다.

**Tech Stack:** Unity (MonoBehaviour, GameObject), C#, VContainer, MessagePipe. 전부 클라이언트 `Assembly-CSharp`.

## Global Constraints

- **main 직접 커밋 금지** — 이미 `feature/actor-view-facade` 브랜치에서 작업 중.
- **범위: 클라이언트 프로젝트만.** 서버 `LOPEntityView`는 손대지 않는다.
- **파일 생성/삭제 없음** — 전부 기존 파일 수정. 새 `.meta` 없음.
- **World 타입은 풀 네임스페이스 한정** — `GameFramework.World.*` (기존 파일 컨벤션 유지).
- **테스트 전략(중요):** 이 슬라이스는 전부 Unity-결합 뷰 배선(MonoBehaviour/GameObject/위임 프로퍼티)이라 **추출 가능한 순수 로직이 없다** → EditMode/standalone .NET 유닛 테스트가 성립하지 않는다(핑계 아님, 대상 부재). 각 태스크 게이트 = **컴파일 클린(새 에러 0)**, 최종 게이트 = **인게임 1라운드 회귀 없음**.
- **UnityMCP 인스턴스 타깃(필수):** 모든 UnityMCP 호출에 `unity_instance`를 **명시**한다. 먼저 `mcpforunity://instances` 리소스를 읽어 `name == "LeagueOfPhysical-Client"` 인스턴스의 전체 `id`(`Name@hash`)를 얻고, 그 값을 매 호출 `unity_instance`로 전달한다. 서버 인스턴스는 절대 대상으로 하지 않는다.

### 컴파일 검증 절차 (각 태스크 공통)

수정 후 다음으로 새 컴파일 에러 0을 확인한다:
1. `mcp__UnityMCP__refresh_unity(unity_instance="<클라 id>")` — 에셋 리임포트/재컴파일 트리거.
2. `editor_state`의 `isCompiling`이 false가 될 때까지 대기(폴링).
3. `mcp__UnityMCP__read_console(unity_instance="<클라 id>", types=["error"])` — **이 슬라이스가 유발한 새 에러 0건** 확인(기존 무관 에러/경고는 무시).

---

## File Structure

수정 파일(9개, 전부 기존):

- `Assets/Scripts/Entity/LOPActor.cs` — 대표/파사드. `view` 소유 + `SetView` + `visualGameObject` 위임 추가.
- `Assets/Scripts/Entity/LOPEntityView.cs` — 렌더링 전담(유지). Actor 참조 제거, entityId만 보유(`SetEntityId`).
- `Assets/Scripts/Game/EntityBinder.cs` — 스포너. View를 Actor에 등록 + 소비처 배선을 actor로.
- `Assets/Scripts/Entity/LocalEntityInterpolator.cs` — `entityView` 필드 제거, `actor` 하나로.
- `Assets/Scripts/Entity/RemoteEntityInterpolator.cs` — `entityView` → `actor`.
- `Assets/Scripts/Game/IPlayerContext.cs` / `Assets/Scripts/Game/PlayerContext.cs` — `entityView` → `actor`.
- `Assets/Scripts/Game/LOPGamePresenter.cs` — 카메라 타깃을 `playerContext.actor.visualGameObject`로.
- `Assets/Scripts/UI/WorldSpace/DamageFloaterEmitter.cs` — `_entityView` 제거, 보유한 `actor.visualGameObject` 사용.
- `Assets/Scripts/UI/WorldSpace/CharacterNameplate.cs` — 동일.

---

## Task 1: LOPActor에 파사드 표면 추가 (additive)

`LOPActor`가 자기 View를 소유하고 `visualGameObject`를 위임 노출하도록 순수 추가한다. 이 태스크는 기존 코드를 깨지 않는다(아무도 아직 새 멤버를 안 씀).

**Files:**
- Modify: `Assets/Scripts/Entity/LOPActor.cs`

**Interfaces:**
- Produces:
  - `void LOPActor.SetView(LOPEntityView view)` — 스포너가 뷰 생성 후 등록.
  - `UnityEngine.GameObject LOPActor.visualGameObject { get; }` — 렌더 모델(뷰 미로드/파괴 시 null).

- [ ] **Step 1: `LOPActor.cs`를 아래 전체 내용으로 교체**

```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPActor : MonoBehaviour
    {
        public string entityId { get; private set; }

        private LOPEntityView view;

        // 스포너가 뷰를 만든 뒤 등록한다(Actor.Awake 시점엔 뷰가 아직 없음).
        public void SetView(LOPEntityView view)
        {
            this.view = view;
        }

        // 렌더되는 모델 GameObject. 뷰가 async 로드 전이거나 파괴됐으면 null.
        // 외부는 이 대표 표면만 읽는다(gameObject.transform 식 위임 접근자).
        public GameObject visualGameObject => view != null ? view.visualGameObject : null;

        public virtual void Initialize<TEntityCreationData>(TEntityCreationData creationData)
            where TEntityCreationData : struct, IEntityCreationData
        {
            entityId = creationData.entityId;
        }
    }
}
```

- [ ] **Step 2: 컴파일 검증** — "컴파일 검증 절차" 수행. 기대: 새 에러 0.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Entity/LOPActor.cs
git commit -m "feat(actor): LOPActor에 뷰 파사드 표면(SetView/visualGameObject) 추가"
```

---

## Task 2: View를 Actor에서 분리 + 스포너가 View를 Actor에 등록

`LOPEntityView`가 `LOPActor`를 참조하지 않고 entityId만 받도록 바꾸고(파사드는 서브시스템을 모른다), 스포너가 View를 Actor에 등록하도록 배선한다. 이후 `actor.visualGameObject` 파사드가 실제로 동작한다.

**Files:**
- Modify: `Assets/Scripts/Entity/LOPEntityView.cs`
- Modify: `Assets/Scripts/Game/EntityBinder.cs`

**Interfaces:**
- Consumes: `LOPActor.SetView` (Task 1).
- Produces: `void LOPEntityView.SetEntityId(string entityId)` — 스포너가 entityId 주입(구 `SetEntity(LOPActor)` 대체).

- [ ] **Step 1: `LOPEntityView.cs`에서 Actor 참조를 entityId로 교체**

`public LOPActor actor { get; private set; }` (15행 부근)과 그 아래 `SetEntity` 메서드를 아래로 교체:

```csharp
        public string entityId { get; private set; }

        public void SetEntityId(string entityId)
        {
            this.entityId = entityId;
        }
```

- [ ] **Step 2: `LOPEntityView.cs`의 `actor.entityId` 참조를 전부 `entityId`로 교체**

다음 5곳(`Start`의 두 구독 + Appearance 조회, `UpdateRunAnimation`, `UpdateVisual`):

```csharp
// Start()
GlobalMessagePipe.GetSubscriber<string, AbilityActivated>().Subscribe(entityId, OnAbilityActivated).AddTo(bag);
GlobalMessagePipe.GetSubscriber<string, EntityDamage>().Subscribe(entityId, OnEntityDamage).AddTo(bag);
...
var appearance = entityRegistry.Get(entityId)?.Get<Appearance>();
```

```csharp
// UpdateRunAnimation()
var worldEntity = entityRegistry.Get(entityId);
```

```csharp
// UpdateVisual()
var worldEntity = entityRegistry.Get(entityId);
```

- [ ] **Step 3: `LOPEntityView.cs`의 가드/정리 교체**

`UpdateRunAnimation` 첫 가드:

```csharp
            if (entityId == null || visualGameObject == null)
            {
                return;
            }
```

`Cleanup()` 마지막 줄 `actor = null;` 을:

```csharp
            entityId = null;
```

- [ ] **Step 4: `EntityBinder.cs`에서 View 생성부를 등록형으로 교체**

기존(63~65행):
```csharp
            LOPEntityView view = root.AddComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntity(actor);
```
교체:
```csharp
            LOPEntityView view = root.AddComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntityId(actor.entityId);
            actor.SetView(view);
```

- [ ] **Step 5: 컴파일 검증** — 새 에러 0. (이 시점 `playerContext.entityView`/`interpolator.entityView` 배선은 아직 그대로라 컴파일 유지.)

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Entity/LOPEntityView.cs Assets/Scripts/Game/EntityBinder.cs
git commit -m "refactor(view): LOPEntityView를 entityId만 보유하도록 분리 + 스포너가 Actor에 뷰 등록"
```

---

## Task 3: 플레이어 컨텍스트 참조를 actor로 전환

`IPlayerContext.entityView`(LOPEntityView) → `actor`(LOPActor). 카메라 타깃(`LOPGamePresenter`)과 스포너 세팅부를 함께 바꿔 컴파일을 유지한다. 로직용 `entityId`는 그대로 둔다(logic-decouple 원칙: 로직=entityId, 뷰=actor).

**Files:**
- Modify: `Assets/Scripts/Game/IPlayerContext.cs`
- Modify: `Assets/Scripts/Game/PlayerContext.cs`
- Modify: `Assets/Scripts/Game/LOPGamePresenter.cs`
- Modify: `Assets/Scripts/Game/EntityBinder.cs`

**Interfaces:**
- Consumes: `LOPActor.visualGameObject` (Task 1).
- Produces: `IPlayerContext.actor { get; set; }` (type `LOPActor`) — 구 `entityView` 대체.

- [ ] **Step 1: `IPlayerContext.cs`의 `entityView` 프로퍼티 교체**

```csharp
        LOPActor actor { get; set; }
```
(기존 `LOPEntityView entityView { get; set; }` 대체. `string entityId { get; set; }`는 유지.)

- [ ] **Step 2: `PlayerContext.cs`의 구현 교체**

```csharp
        public LOPActor actor { get; set; }
```
(기존 `public LOPEntityView entityView { get; set; }` 대체.)

- [ ] **Step 3: `LOPGamePresenter.cs`의 카메라 타깃 경로 교체 (63·65행)**

```csharp
            await UniTask.WaitUntil(() => playerContext.actor != null && playerContext.actor.visualGameObject != null);

            cameraController.SetTarget(playerContext.actor.visualGameObject.transform);
```

- [ ] **Step 4: `EntityBinder.cs`의 playerContext 세팅 교체 (72행)**

기존 `playerContext.entityView = view;` 를:
```csharp
                    playerContext.actor = actor;
```

- [ ] **Step 5: 컴파일 검증** — 새 에러 0.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Game/IPlayerContext.cs Assets/Scripts/Game/PlayerContext.cs Assets/Scripts/Game/LOPGamePresenter.cs Assets/Scripts/Game/EntityBinder.cs
git commit -m "refactor(context): playerContext.entityView → actor (카메라 타깃 파사드 경유)"
```

---

## Task 4: 보간기 참조를 actor로 전환

`LocalEntityInterpolator`는 이미 `actor`(entityId용)와 `entityView`(visualGameObject용) 둘을 든다 → `actor` 하나로 합친다. `RemoteEntityInterpolator`는 `entityView` → `actor`. 스포너 배선도 함께.

**Files:**
- Modify: `Assets/Scripts/Entity/LocalEntityInterpolator.cs`
- Modify: `Assets/Scripts/Entity/RemoteEntityInterpolator.cs`
- Modify: `Assets/Scripts/Game/EntityBinder.cs`

**Interfaces:**
- Consumes: `LOPActor.visualGameObject` (Task 1).
- Produces:
  - `LocalEntityInterpolator.actor` (기존 유지, `entityView` 제거).
  - `RemoteEntityInterpolator.actor { get; set; }` (type `LOPActor`) — 구 `entityView` 대체.

- [ ] **Step 1: `LocalEntityInterpolator.cs`의 `entityView` 필드 삭제**

24행 `public LOPEntityView entityView { get; set; }` 을 **삭제**한다. (23행 `public LOPActor actor { get; set; }` 는 유지.)

- [ ] **Step 2: `LocalEntityInterpolator.cs`의 `LateUpdate`에서 `entityView` → `actor` (73·92·93행)**

```csharp
            if (actor.visualGameObject == null || samples.Count == 0)
            {
                return;
            }
```
```csharp
            actor.visualGameObject.transform.position = Vector3.Lerp(lower.position, upper.position, bracket.Alpha);
            actor.visualGameObject.transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(lower.rotation), Quaternion.Euler(upper.rotation), bracket.Alpha);
```

- [ ] **Step 3: `RemoteEntityInterpolator.cs`의 `entityView` 필드 교체 (17행)**

```csharp
        public LOPActor actor { get; set; }
```
(기존 `public LOPEntityView entityView { get; set; }` 대체. 16행 `worldEntity`는 유지.)

- [ ] **Step 4: `RemoteEntityInterpolator.cs`의 `Apply`에서 `entityView` → `actor` (71·73·74행)**

```csharp
            if (actor.visualGameObject != null)
            {
                actor.visualGameObject.transform.position = pos;
                actor.visualGameObject.transform.rotation = rot;
            }
```

- [ ] **Step 5: `EntityBinder.cs`의 보간기 배선 교체 (77·84·102행)**

- 로컬(캐릭터, 74~78행 블록): `interpolator.entityView = view;` 줄을 **삭제**한다(바로 위 `interpolator.actor = actor;`가 이미 있음).
- 원격 캐릭터(81~85행 블록): `interpolator.entityView = view;` 를 `interpolator.actor = actor;` 로 교체.
- 원격 아이템(99~103행 블록): `interpolator.entityView = view;` 를 `interpolator.actor = actor;` 로 교체.

- [ ] **Step 6: 컴파일 검증** — 새 에러 0.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Entity/LocalEntityInterpolator.cs Assets/Scripts/Entity/RemoteEntityInterpolator.cs Assets/Scripts/Game/EntityBinder.cs
git commit -m "refactor(interp): 보간기 entityView → actor.visualGameObject (로컬은 actor 단일화)"
```

---

## Task 5: 월드스페이스 UI 참조를 actor로 전환

`DamageFloaterEmitter`/`CharacterNameplate`는 이미 `actor`를 보유하므로(`SetEntity`), 별도 `_entityView` GetComponent를 없애고 보유한 `actor.visualGameObject`를 쓴다.

**Files:**
- Modify: `Assets/Scripts/UI/WorldSpace/DamageFloaterEmitter.cs`
- Modify: `Assets/Scripts/UI/WorldSpace/CharacterNameplate.cs`

**Interfaces:**
- Consumes: `LOPActor.visualGameObject` (Task 1), `LOPActor.entityId`(기존).

- [ ] **Step 1: `DamageFloaterEmitter.cs`에서 `_entityView` 필드 삭제 (38행)**

`private LOPEntityView _entityView;` 를 **삭제**.

- [ ] **Step 2: `DamageFloaterEmitter.cs`의 `Start`에서 GetComponent 삭제 (73행)**

`_entityView = GetComponent<LOPEntityView>();` 줄을 **삭제**. (구독 세팅 줄은 유지.)

- [ ] **Step 3: `DamageFloaterEmitter.cs`의 머리 위치 계산 교체 (106~108행)**

```csharp
            var worldEntity = entityRegistry.Get(actor.entityId);
            Vector3 headPosition = (actor.visualGameObject != null)
                ? actor.visualGameObject.transform.position
                : worldEntity != null ? GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity) : Vector3.zero;
```

- [ ] **Step 4: `CharacterNameplate.cs`에서 `_entityView` 필드 삭제 (44행)**

`private LOPEntityView _entityView;` 를 **삭제**.

- [ ] **Step 5: `CharacterNameplate.cs`의 `Start`에서 GetComponent 삭제 (51행)**

`_entityView = GetComponent<LOPEntityView>();` 줄을 **삭제**.

- [ ] **Step 6: `CharacterNameplate.cs`의 `LateUpdate` 위치 계산 교체 (131~134행)**

```csharp
            var worldEntity = entityRegistry.Get(actor.entityId);
            Vector3 basePosition = (actor.visualGameObject != null)
                ? actor.visualGameObject.transform.position
                : worldEntity != null ? GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity) : Vector3.zero;
```

- [ ] **Step 7: 컴파일 검증** — 새 에러 0. 이 시점 `LOPEntityView` 타입을 외부에서 참조하는 곳은 `EntityBinder`의 생성부(`AddComponent<LOPEntityView>`)뿐이어야 한다(파사드 완성).

- [ ] **Step 8: 커밋**

```bash
git add Assets/Scripts/UI/WorldSpace/DamageFloaterEmitter.cs Assets/Scripts/UI/WorldSpace/CharacterNameplate.cs
git commit -m "refactor(ui): 월드스페이스 UI가 보유한 actor.visualGameObject 사용(_entityView 제거)"
```

---

## Task 6: 인게임 회귀 검증

컴파일이 아니라 실제 플레이로 파사드 경로 전체를 확인한다. (자동화 불가 — 사용자/실행자가 에디터에서 1라운드 플레이.)

**Files:** 없음(검증만).

- [ ] **Step 1: 클라이언트 에디터에서 게임 씬 진입, 1라운드 플레이**

- [ ] **Step 2: 아래 항목을 육안 확인 (전부 정상 = 회귀 없음)**

  - 내 캐릭터·원격 캐릭터·아이템 **비주얼 모델 로딩**(Addressables).
  - **걷기(Run) 애니** 재생, **공격/피격 애니** cue.
  - **데미지 숫자 플로터**가 머리 위에 뜸.
  - **네임플레이트 HP바**가 머리를 추종하고 피격 시 감소.
  - **카메라가 내 캐릭터를 추적**(`playerContext.actor.visualGameObject` 경로).
  - 원격/로컬 **보간**으로 메시가 부드럽게 이동.
  - 엔티티 **파괴 시 콘솔 에러 0**(ICleanup 정리) — `read_console(unity_instance="<클라 id>", types=["error"])`로 확인.

- [ ] **Step 3: 이상 있으면 systematic-debugging으로 진단, 없으면 완료**

---

## Self-Review (작성자 확인 결과)

**1. 스펙 커버리지:** 스펙 "변경 목록" 9파일 전부 태스크에 매핑됨(Task 1=LOPActor, 2=LOPEntityView+EntityBinder, 3=playerContext+presenter+EntityBinder, 4=보간기+EntityBinder, 5=UI). "산업 표준 매핑/파사드 표면/유지 항목"은 코드에 반영. 검증 절차 = Task 6.

**2. 플레이스홀더:** 없음. 모든 코드 스텝에 실제 코드/정확 경로/행 번호 포함.

**3. 타입 일관성:** `SetView(LOPEntityView)`/`visualGameObject:GameObject`(Task1) → 소비 Task3/4/5에서 동일 사용. `SetEntityId(string)`(Task2) → `LOPEntityView`가 entityId만 보유, `entityView` 잔재 없음. `IPlayerContext.actor:LOPActor`(Task3), `RemoteEntityInterpolator.actor:LOPActor`(Task4) 일관. `EntityBinder`는 Task2/3/4에서 서로 다른 행을 수정(충돌 없음).

**주의(실행자):** `EntityBinder.cs`는 Task 2·3·4가 각각 다른 부분을 건드린다 — 태스크 순서대로 진행하면 각 커밋이 컴파일 그린이다(중간 태스크에서 아직 안 바꾼 배선은 기존 `entityView` 필드가 살아 있어 유지됨).
