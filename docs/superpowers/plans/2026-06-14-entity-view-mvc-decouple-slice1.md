# Entity View/Controller MVC 디커플 — Slice 1 (클라이언트) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 클라이언트의 엔티티 뷰/컨트롤러 4개 클래스를 GameFramework MVC 제네릭 베이스(`MonoEntityView<,>`/`MonoEntityController<>`)에서 떼어내 평범한 `MonoBehaviour, ICleanup`로 디커플하고, 죽은 `entityController` 와이어를 제거한다.

**Architecture:** 동작 변화 없는 베이스 교체. 각 소비자가 베이스에서 실제로 쓰던 `entity`(LOPEntity)+`SetEntity`+`ICleanup.Cleanup()`만 자체 보유하고, 읽는 곳이 없는 `entityController`/`SetEntityController`는 호출부와 함께 삭제한다. Cleanup 정리는 기존대로 `ICleanup` 디스패치(`LOPEntityManager.DestroyMarkedEntities`)로 동작한다. GameFramework 베이스 타입 자체의 삭제는 Slice 3(서버 디커플 후 게이트)에서 한다 — 이 Slice에서는 GF 베이스가 그대로 존재하므로 클라는 항상 컴파일된다.

**Tech Stack:** Unity (C#, Assembly-CSharp 단일), VContainer, UniRx, UnityMCP(컴파일 검증).

**Spec:** `docs/superpowers/specs/2026-06-14-entity-view-mvc-decouple-design.md`

**Branch:** `feature/entity-view-separation` (현재 체크아웃됨 — 새 브랜치 생성 불필요)

---

## File Structure

이 Slice에서 **수정**하는 파일(생성/삭제 없음 → `.meta` 변경 없음):

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/EntityCreator/CharacterCreator.cs` | `view.SetEntityController(controller);` 1줄 제거 |
| `Assets/Scripts/EntityCreator/ItemCreator.cs` | `view.SetEntityController(controller);` 1줄 제거 |
| `Assets/Scripts/Game/EntityViewSpawner.cs` | `SetEntityController` 2줄 + 전용 `controller` 조회 1줄 제거 |
| `Assets/Scripts/Entity/LOPEntityController.cs` | 베이스 교체 + `entity`/`SetEntity` 자체 보유 + `using UnityEngine;` 추가 + `Cleanup` non-override |
| `Assets/Scripts/Entity/LOPEntityView.cs` | 베이스 교체 + `entity`/`SetEntity` 자체 보유 + `Cleanup` non-override |
| `Assets/Scripts/UI/WorldSpace/DamageFloaterEmitter.cs` | 베이스 교체 + `entity`/`SetEntity` 자체 보유 + `Cleanup` non-override |
| `Assets/Scripts/UI/WorldSpace/CharacterNameplate.cs` | 베이스 교체 + `entity`/`SetEntity` 자체 보유 + `Cleanup` non-override |

**작업 순서 근거:** Task 1에서 `SetEntityController` **호출부를 먼저 제거**하면(베이스 메서드는 아직 존재하므로 호출 제거는 무해, 컴파일 유지), 이후 각 클래스를 디커플해도 댕글링 콜러가 없다. 유지하는 멤버(`entity`/`SetEntity`)는 변환 클래스가 그대로 노출하므로 콜러는 항상 컴파일된다. 따라서 각 Task가 독립적으로 컴파일 가능한 커밋이 된다.

---

## Verification Procedure (편의상 한 번만 정의 — 각 편집 Task의 "compile 검증" 단계에서 이 절차를 수행)

> 이 프로젝트는 **클라이언트**다. UnityMCP는 서버/클라 에디터가 동시에 붙어 있을 수 있으니 **모든 호출에 `unity_instance`를 클라로 명시**한다 (CLAUDE.md 규칙).
>
> 아래 UnityMCP 툴은 deferred다 — 먼저 로드: `ToolSearch("select:read_console,refresh_unity")`.

1. 클라 인스턴스 id 해석: 리소스 `mcpforunity://instances`를 읽어 `name == "LeagueOfPhysical-Client"`인 항목의 full `id`(`Name@hash`)를 얻는다.
2. 재컴파일 강제: `refresh_unity(unity_instance="<클라 id>")`.
3. 컴파일 에러 확인: `read_console(unity_instance="<클라 id>", types=["error"])`.
   - **기대:** 이번에 만진 파일(`LOPEntityView`/`LOPEntityController`/`DamageFloaterEmitter`/`CharacterNameplate`/`CharacterCreator`/`ItemCreator`/`EntityViewSpawner`) 관련 컴파일 에러 **0건**. (무관한 기존 경고/에러가 있으면 그대로 두고, 이 Task가 새로 만든 에러가 없음을 확인.)

---

## Task 1: `SetEntityController` 호출부 + 전용 controller 조회 제거

**Files:**
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs`
- Modify: `Assets/Scripts/EntityCreator/ItemCreator.cs`
- Modify: `Assets/Scripts/Game/EntityViewSpawner.cs`

- [ ] **Step 1: `CharacterCreator.cs`에서 SetEntityController 호출 제거**

Replace:
```csharp
            LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntity(entity);
            view.SetEntityController(controller);
```
With:
```csharp
            LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntity(entity);
```
(`controller`는 위쪽 `controller.SetEntity(entity)`에서 계속 쓰이므로 그대로 둔다.)

- [ ] **Step 2: `ItemCreator.cs`에서 SetEntityController 호출 제거**

Replace:
```csharp
            LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntity(entity);
            view.SetEntityController(controller);
```
With:
```csharp
            LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntity(entity);
```

- [ ] **Step 3: `EntityViewSpawner.cs`에서 SetEntityController 2줄 + 전용 controller 조회 제거**

Replace:
```csharp
            GameObject root = entity.transform.parent.gameObject;
            LOPEntityController controller = root.GetComponentInChildren<LOPEntityController>();

            DamageFloaterEmitter damageFloaterEmitter = root.CreateChildWithComponent<DamageFloaterEmitter>();
            objectResolver.Inject(damageFloaterEmitter);
            damageFloaterEmitter.SetEntity(entity);
            damageFloaterEmitter.SetEntityController(controller);

            CharacterNameplate nameplate = root.CreateChildWithComponent<CharacterNameplate>();
            objectResolver.Inject(nameplate);
            nameplate.SetEntity(entity);
            nameplate.SetEntityController(controller);
```
With:
```csharp
            GameObject root = entity.transform.parent.gameObject;

            DamageFloaterEmitter damageFloaterEmitter = root.CreateChildWithComponent<DamageFloaterEmitter>();
            objectResolver.Inject(damageFloaterEmitter);
            damageFloaterEmitter.SetEntity(entity);

            CharacterNameplate nameplate = root.CreateChildWithComponent<CharacterNameplate>();
            objectResolver.Inject(nameplate);
            nameplate.SetEntity(entity);
```

- [ ] **Step 4: compile 검증** — 위 "Verification Procedure" 수행. 기대: 새 에러 0건. (이 시점엔 베이스 `SetEntityController`가 여전히 존재하지만 더 이상 호출되지 않을 뿐.)

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/EntityCreator/ItemCreator.cs Assets/Scripts/Game/EntityViewSpawner.cs
git commit -m "$(cat <<'EOF'
refactor(entity): drop dead SetEntityController wiring (client)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: `LOPEntityController` 디커플

**Files:**
- Modify: `Assets/Scripts/Entity/LOPEntityController.cs`

- [ ] **Step 1: `using UnityEngine;` 추가** (MonoBehaviour 직접 상속에 필요)

Replace:
```csharp
using UniRx;
using VContainer;
```
With:
```csharp
using UniRx;
using UnityEngine;
using VContainer;
```

- [ ] **Step 2: 베이스 교체 + `entity`/`SetEntity` 자체 보유**

Replace:
```csharp
    public class LOPEntityController : MonoEntityController<LOPEntity>
    {
        [Inject]
        private IGameEngine gameEngine;
```
With:
```csharp
    public class LOPEntityController : MonoBehaviour, ICleanup
    {
        [Inject]
        private IGameEngine gameEngine;

        public LOPEntity entity { get; private set; }

        public void SetEntity(LOPEntity entity)
        {
            this.entity = entity;
        }
```

- [ ] **Step 3: `Cleanup`을 non-override `ICleanup` 구현으로**

Replace:
```csharp
        public override void Cleanup()
        {
            EventBus.Default.Unsubscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);
            gameEngine.RemoveListener(this);
            base.Cleanup();
        }
```
With:
```csharp
        public void Cleanup()
        {
            EventBus.Default.Unsubscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);
            gameEngine.RemoveListener(this);
            entity = null;
        }
```

- [ ] **Step 4: compile 검증** — Verification Procedure 수행. 기대: 새 에러 0건.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Entity/LOPEntityController.cs
git commit -m "$(cat <<'EOF'
refactor(entity): decouple LOPEntityController from GameFramework MVC base (client)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: `LOPEntityView` 디커플

**Files:**
- Modify: `Assets/Scripts/Entity/LOPEntityView.cs`

(`using UnityEngine;`는 이미 존재 — using 변경 없음.)

- [ ] **Step 1: 베이스 교체 + `entity`/`SetEntity` 자체 보유**

Replace:
```csharp
    public class LOPEntityView : MonoEntityView<LOPEntity, LOPEntityController>
    {
        private GameObject _visualGameObject;
```
With:
```csharp
    public class LOPEntityView : MonoBehaviour, ICleanup
    {
        public LOPEntity entity { get; private set; }

        public void SetEntity(LOPEntity entity)
        {
            this.entity = entity;
        }

        private GameObject _visualGameObject;
```

- [ ] **Step 2: `Cleanup` 시그니처 non-override로**

Replace:
```csharp
        public override void Cleanup()
        {
            EventBus.Default.Unsubscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);
            EventBus.Default.Unsubscribe<ActionStart>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnActionStart);
```
With:
```csharp
        public void Cleanup()
        {
            EventBus.Default.Unsubscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);
            EventBus.Default.Unsubscribe<ActionStart>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnActionStart);
```

- [ ] **Step 3: `base.Cleanup();`을 `entity = null;`로**

Replace:
```csharp
            if (_visualGameObject != null)
            {
                Destroy(_visualGameObject);
            }

            base.Cleanup();
        }
```
With:
```csharp
            if (_visualGameObject != null)
            {
                Destroy(_visualGameObject);
            }

            entity = null;
        }
```

- [ ] **Step 4: compile 검증** — Verification Procedure 수행. 기대: 새 에러 0건. (`playerContext.entityView`/reconciler들은 `LOPEntityView`를 구상 타입으로 보유하고 `visualGameObject`만 읽으므로 영향 없음.)

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Entity/LOPEntityView.cs
git commit -m "$(cat <<'EOF'
refactor(entity): decouple LOPEntityView from GameFramework MVC base (client)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: `DamageFloaterEmitter` 디커플

**Files:**
- Modify: `Assets/Scripts/UI/WorldSpace/DamageFloaterEmitter.cs`

(`using UnityEngine;` 이미 존재.)

- [ ] **Step 1: 베이스 교체 + `entity`/`SetEntity` 자체 보유**

Replace:
```csharp
    public class DamageFloaterEmitter : MonoEntityView<LOPEntity, LOPEntityController>
    {
        private const int MAX_FLOATERS = 4;
```
With:
```csharp
    public class DamageFloaterEmitter : MonoBehaviour, ICleanup
    {
        public LOPEntity entity { get; private set; }

        public void SetEntity(LOPEntity entity)
        {
            this.entity = entity;
        }

        private const int MAX_FLOATERS = 4;
```

- [ ] **Step 2: `Cleanup` 시그니처 non-override로**

Replace:
```csharp
        public override void Cleanup()
        {
            EventBus.Default.Unsubscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnEntityDamage);
```
With:
```csharp
        public void Cleanup()
        {
            EventBus.Default.Unsubscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnEntityDamage);
```

- [ ] **Step 3: `base.Cleanup();`을 `entity = null;`로**

Replace:
```csharp
            _floaters.Clear();

            base.Cleanup();
        }
```
With:
```csharp
            _floaters.Clear();

            entity = null;
        }
```

- [ ] **Step 4: compile 검증** — Verification Procedure 수행. 기대: 새 에러 0건.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/WorldSpace/DamageFloaterEmitter.cs
git commit -m "$(cat <<'EOF'
refactor(entity): decouple DamageFloaterEmitter from GameFramework MVC base (client)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: `CharacterNameplate` 디커플

**Files:**
- Modify: `Assets/Scripts/UI/WorldSpace/CharacterNameplate.cs`

(`using UnityEngine;` 이미 존재. `[DefaultExecutionOrder(3100)]` 어트리뷰트는 그대로 둔다.)

- [ ] **Step 1: 베이스 교체 + `entity`/`SetEntity` 자체 보유**

Replace:
```csharp
    public class CharacterNameplate : MonoEntityView<LOPEntity, LOPEntityController>
    {
        private const string PanelSettingsResource = "UI/WorldSpaceNameplatePanelSettings";
```
With:
```csharp
    public class CharacterNameplate : MonoBehaviour, ICleanup
    {
        public LOPEntity entity { get; private set; }

        public void SetEntity(LOPEntity entity)
        {
            this.entity = entity;
        }

        private const string PanelSettingsResource = "UI/WorldSpaceNameplatePanelSettings";
```

- [ ] **Step 2: `Cleanup` 시그니처 non-override로**

Replace:
```csharp
        public override void Cleanup()
        {
            EventBus.Default.Unsubscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnEntityDamage);
```
With:
```csharp
        public void Cleanup()
        {
            EventBus.Default.Unsubscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnEntityDamage);
```

- [ ] **Step 3: `base.Cleanup();`을 `entity = null;`로**

Replace:
```csharp
            if (_panelGameObject != null)
            {
                Destroy(_panelGameObject);
                _panelGameObject = null;
            }

            base.Cleanup();
        }
```
With:
```csharp
            if (_panelGameObject != null)
            {
                Destroy(_panelGameObject);
                _panelGameObject = null;
            }

            entity = null;
        }
```

- [ ] **Step 4: compile 검증** — Verification Procedure 수행. 기대: 새 에러 0건. 이 시점에서 클라의 4개 소비자가 모두 GF MVC 베이스에서 분리되어, GF 베이스는 클라에서 더 이상 참조되지 않는다(삭제는 Slice 3).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/WorldSpace/CharacterNameplate.cs
git commit -m "$(cat <<'EOF'
refactor(entity): decouple CharacterNameplate from GameFramework MVC base (client)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: 런타임 플레이 검증 (수동 — 사용자)

자동화 테스트 없음(단일 Assembly-CSharp, spec 기조). 사용자가 클라 에디터에서 플레이로 확인한다.

- [ ] **Step 1: 클라 에디터 Play 진입**, 룸 연결 후 게임 씬까지 진행.
- [ ] **Step 2: 다음 항목 육안 확인**
  - 캐릭터 비주얼 로딩(`LOPEntityView.UpdateVisual`) — 모델이 보인다
  - 이동 시 Run 애니메이션, 공격 시 Attack, 피격 시 Hit 트리거 동작
  - 피격 시 머리 위 **데미지 숫자 플로터**(`DamageFloaterEmitter`) 출현
  - 머리 위 **네임플레이트 HP바**(`CharacterNameplate`)가 따라다니고 피격 시 감소
  - 카메라가 내 캐릭터를 추적(`playerContext.entityView.visualGameObject` 경로 정상)
- [ ] **Step 3: 엔티티 파괴 검증** — 캐릭터 디스폰/사망 처리 시 콘솔 에러 0건(`ICleanup` 정리 경로: 데미지 플로터/네임플레이트/뷰/컨트롤러가 함께 정리). `[World] Unregistered entity ...` 로그 정상.
- [ ] **Step 4: World Core 회귀** — 데미지→사망 경로에서 `[World] Death entity ... (killer=...)` 로그 정상.

> 모두 통과하면 Slice 1 완료. 이어서 Slice 2(서버 디커플)는 별도 plan으로 LOP-Server 저장소에서 진행하고, Slice 3(GF 베이스 삭제)는 Slice 2 완료 후 게이트로 수행한다.

---

## 진행

- [x] Task 1 — SetEntityController 호출부 + controller 조회 제거
- [x] Task 2 — LOPEntityController 디커플
- [x] Task 3 — LOPEntityView 디커플
- [x] Task 4 — DamageFloaterEmitter 디커플
- [x] Task 5 — CharacterNameplate 디커플
- [ ] Task 6 — 런타임 플레이 검증(수동 — 사용자 확인 대기)

> **완료 기록:** Task 1~5는 `feature/entity-view-separation` 브랜치에서 구현 후
> `ae389d6 Merge feature/entity-view-separation`로 main에 머지되었다
> (커밋 `d8587bb`/`154322a`/`4260979`/`b307c61` + 호출부 제거). 클라 4개 소비자
> (`LOPEntityView`/`LOPEntityController`/`DamageFloaterEmitter`/`CharacterNameplate`)가
> 모두 GF MVC 베이스에서 분리되어 `MonoBehaviour, ICleanup`로 동작한다.
> Slice 2(서버 디커플)·Slice 3(GF 베이스 삭제)는 별도 plan으로 잔존.
