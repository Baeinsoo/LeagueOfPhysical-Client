# Entity View/Controller MVC 디커플 — Slice 2 (서버) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 서버의 엔티티 뷰/컨트롤러 3개 클래스(`LOPEntityView`, `LOPEntityController`, `LOPAIController`)를 GameFramework MVC 제네릭 베이스에서 떼어내 평범한 `MonoBehaviour, ICleanup`로 디커플하고, 죽은 `SetEntityController` 와이어를 제거한다.

**Architecture:** Slice 1(클라)과 동일한 동작 불변 베이스 교체. 각 소비자가 `entity`(LOPEntity)+`SetEntity`+`ICleanup.Cleanup()`만 자체 보유, `SetEntityController` 호출은 삭제. GameFramework 베이스 타입 자체의 삭제는 Slice 3(게이트)에서 한다 — 이 Slice에서는 GF 베이스가 그대로 존재하므로 서버는 항상 컴파일된다. **서버에는 클라의 WorldSpace 장식 뷰(`DamageFloaterEmitter`/`CharacterNameplate`)와 reconciler(`SnapReconciler` 등)가 없으므로** 대상이 3개 클래스 + creator 2곳으로 더 작다.

**Tech Stack:** Unity (C#, Assembly-CSharp 단일), VContainer, UniRx, Mirror, UnityMCP(컴파일 검증).

**Spec:** `docs/superpowers/specs/2026-06-14-entity-view-mvc-decouple-design.md` (LOP-Client 저장소)

**저장소:** `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server`

**Branch:** 작업 시작 전 서버 저장소에서 피처 브랜치를 보장한다(예: `feature/entity-view-separation`). main 직접 커밋 금지(CLAUDE.md 규칙).

> **인스턴스 타깃 주의:** 이 Slice는 **서버** 작업이다. LOP-Client 프로젝트의 CLAUDE.md는 평소 클라만 타깃하라 하지만, 이 Slice는 사용자가 명시적으로 요청한 서버 작업이므로 UnityMCP 검증 시 **서버 인스턴스**를 핀한다(아래 Verification Procedure).

---

## File Structure

수정만(생성/삭제 없음 → `.meta` 변경 없음). 모두 서버 저장소 경로:

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/EntityCreator/CharacterCreator.cs` | `view.SetEntityController(controller);` 1줄 제거 (line 64) |
| `Assets/Scripts/EntityCreator/ItemCreator.cs` | `view.SetEntityController(controller);` 1줄 제거 (line 45) |
| `Assets/Scripts/Entity/LOPEntityController.cs` | 베이스 교체 + `entity`/`SetEntity` 보유 + `using UnityEngine;` 추가 + `Cleanup` non-override |
| `Assets/Scripts/Entity/LOPEntityView.cs` | 베이스 교체 + `entity`/`SetEntity` 보유 + `Cleanup` non-override |
| `Assets/Scripts/Entity/LOPAIController.cs` | 베이스 교체 + `entity`/`SetEntity` 보유 + `using UnityEngine;` 추가 + `Cleanup` non-override |

> 서버 `CharacterCreator`는 `LOPAIController`도 생성하며 `aiController.SetEntity(entity)`를 호출한다 — `SetEntity`는 디커플 후에도 유지되므로 그 호출은 그대로 동작한다. `view` 지역변수는 `SetEntity`에서 계속 쓰이므로 `SetEntityController` 제거 후에도 미사용 경고가 없다.

**작업 순서 근거:** Slice 1과 동일 — Task 1에서 호출부를 먼저 제거(베이스 메서드는 아직 존재, 호출 제거는 무해)한 뒤 각 클래스를 디커플하면 댕글링 콜러 없이 각 Task가 독립 컴파일·커밋 가능.

---

## Verification Procedure (각 편집 Task의 "compile 검증" 단계에서 수행)

> UnityMCP 툴은 deferred다 — 먼저 로드: `ToolSearch("select:refresh_unity,read_console")` 후 `ReadMcpResourceTool`로 인스턴스 조회.

1. 서버 인스턴스 id 해석: 리소스 `mcpforunity://instances`를 읽어 `name == "LeagueOfPhysical-Server"`인 항목의 full `id`(`Name@hash`)를 얻는다. (작성 시점 예: `LeagueOfPhysical-Server@f99391fa2dbaaf3c` — hash는 바뀔 수 있으니 매번 조회.)
2. 재컴파일 강제: `refresh_unity(unity_instance="<서버 id>", mode="force", scope="scripts", compile="request", wait_for_ready=true)`.
3. readiness 확인 후 `read_console(unity_instance="<서버 id>", action="get", types=["error"], count="40", format="plain")`.
   - **기대:** 이번에 만진 파일 관련 컴파일 에러 **0건**.

---

## Task 1: `SetEntityController` 호출부 제거 (2 files)

**Files:**
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs`
- Modify: `Assets/Scripts/EntityCreator/ItemCreator.cs`

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

- [ ] **Step 3: compile 검증** — Verification Procedure 수행(서버 핀). 기대: 새 에러 0건.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/EntityCreator/ItemCreator.cs
git commit -m "$(cat <<'EOF'
refactor(entity): drop dead SetEntityController wiring (server)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: `LOPEntityController` 디커플 (서버)

**Files:**
- Modify: `Assets/Scripts/Entity/LOPEntityController.cs`

- [ ] **Step 1: `using UnityEngine;` 추가**

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

- [ ] **Step 2: 베이스 교체 + `entity`/`SetEntity` 보유**

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

- [ ] **Step 3: `Cleanup` non-override로**

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

- [ ] **Step 4: compile 검증** — Verification Procedure(서버). 기대: 새 에러 0건.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Entity/LOPEntityController.cs
git commit -m "$(cat <<'EOF'
refactor(entity): decouple LOPEntityController from GameFramework MVC base (server)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: `LOPEntityView` 디커플 (서버)

**Files:**
- Modify: `Assets/Scripts/Entity/LOPEntityView.cs`

(`using UnityEngine;` 이미 존재 — using 변경 없음.)

- [ ] **Step 1: 베이스 교체 + `entity`/`SetEntity` 보유**

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

            if (asyncOperationHandle.IsValid())
```
With:
```csharp
        public void Cleanup()
        {
            EventBus.Default.Unsubscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);

            if (asyncOperationHandle.IsValid())
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

- [ ] **Step 4: compile 검증** — Verification Procedure(서버). 기대: 새 에러 0건.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Entity/LOPEntityView.cs
git commit -m "$(cat <<'EOF'
refactor(entity): decouple LOPEntityView from GameFramework MVC base (server)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: `LOPAIController` 디커플 (서버)

**Files:**
- Modify: `Assets/Scripts/Entity/LOPAIController.cs`

- [ ] **Step 1: `using UnityEngine;` 추가**

Replace:
```csharp
using LOP.Event.LOPGameEngine.Update;
using VContainer;
```
With:
```csharp
using LOP.Event.LOPGameEngine.Update;
using UnityEngine;
using VContainer;
```

- [ ] **Step 2: 베이스 교체 + `entity`/`SetEntity` 보유** (`brain`/`SetBrain`은 그대로 둔다)

Replace:
```csharp
    public class LOPAIController : MonoEntityController<LOPEntity>
    {
        [Inject]
        private IGameEngine gameEngine;

        public IBrain brain { get; private set; }
```
With:
```csharp
    public class LOPAIController : MonoBehaviour, ICleanup
    {
        [Inject]
        private IGameEngine gameEngine;

        public LOPEntity entity { get; private set; }

        public void SetEntity(LOPEntity entity)
        {
            this.entity = entity;
        }

        public IBrain brain { get; private set; }
```

- [ ] **Step 3: `Cleanup` non-override로**

Replace:
```csharp
        public override void Cleanup()
        {
            base.Cleanup();

            gameEngine.RemoveListener(this);
        }
```
With:
```csharp
        public void Cleanup()
        {
            gameEngine.RemoveListener(this);
            entity = null;
        }
```

- [ ] **Step 4: compile 검증** — Verification Procedure(서버). 기대: 새 에러 0건. 이 시점에서 서버의 3개 소비자가 모두 GF MVC 베이스에서 분리되어, GF 베이스는 서버에서 더 이상 참조되지 않는다(클라도 Slice 1에서 동일 상태 → Slice 3 삭제 가능).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Entity/LOPAIController.cs
git commit -m "$(cat <<'EOF'
refactor(entity): decouple LOPAIController from GameFramework MVC base (server)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: 런타임 플레이 검증 (수동 — 사용자)

자동화 테스트 없음(단일 Assembly-CSharp). 서버를 호스트로 한 라운드 플레이로 확인한다.

- [ ] **Step 1: 서버 호스트 + 클라 1대 접속**으로 게임 진입.
- [ ] **Step 2: 다음 확인**
  - AI 적이 동작(`LOPAIController.brain.Think` 경로) — 추적/공격 정상
  - 엔티티 스폰 후 물리 동기화(`LOPEntityController` → `AfterPhysicsSimulation` → `SyncPhysics`) 정상
  - 아이템 엔티티 스폰 정상
  - 엔티티 디스폰/사망 시 서버 콘솔 에러 0(`ICleanup` 정리), `[World] Unregistered entity ...` 로그 정상
- [ ] **Step 3: 서버 콘솔 컴파일/런타임 에러 0건 확인**

> 통과하면 Slice 2 종료. 이어서 Slice 3(GameFramework MVC 타입 삭제)를 게이트로 수행한다 — Slice 1·2가 모두 끝나야 진행 가능.

---

## 진행

- [ ] Task 1 — SetEntityController 호출부 제거 (서버 creator 2곳)
- [ ] Task 2 — LOPEntityController 디커플 (서버)
- [ ] Task 3 — LOPEntityView 디커플 (서버)
- [ ] Task 4 — LOPAIController 디커플 (서버)
- [ ] Task 5 — 런타임 플레이 검증(수동)
