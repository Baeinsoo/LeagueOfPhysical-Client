# S3 — 레거시 substrate machinery 삭제 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 레거시 GameFramework 엔티티/컴포넌트 substrate machinery(`IComponent`/`MonoComponent`/`LOPComponent`/`Status` + `MonoEntity` + `Entity.Extensions`)를 삭제하고 `LOPEntity`를 순수 `MonoBehaviour, IEntity`로 만든다. `IEntity`는 4멤버 파사드 계약으로 축소해 잠정 유지(완전 삭제는 S5).

**Architecture:** 3레포(GameFramework/LOP-Client/LOP-Server) 원자 변경. Task 1(S3a)=컴포넌트 substrate 죽은 삭제(동작 무변화). Task 2(S3b)=`MonoEntity` 삭제 + `LOPEntity`를 `MonoBehaviour, IEntity`로 + `Entity.Extensions` 삭제(`IsGrounded`는 `LOPEntity`로 이전) + `UpdateEntity` 제거. `IEntity`(축소된 파사드)와 매니저/팩토리/`IBrain`/`EntityCreated` 제네릭은 **유지**(완전 삭제=S5).

**Tech Stack:** Unity 6 (C#), GameFramework(순수 C#+MonoBehaviour), UnityMCP(컴파일/콘솔/EditMode), NUnit(GF EditMode 회귀).

## Global Constraints

- **매 태스크 끝에 게임이 그대로 동작**(strangler). GF·클·서 세 editor 컴파일 에러 0.
- **`IEntity`는 삭제하지 않는다**(4멤버 파사드로 축소해 유지 — S5에서 매니저 재작업과 함께 삭제). 매니저/팩토리/`IBrain`/`EntityCreated`의 `where T : IEntity`/`IEntity` 참조는 **유지**.
- **③ 물리 브릿지**(`SyncPhysics`/`PushMotionToPhysics`/`LOPEntityController`)는 **유지**(별도 슬라이스).
- **UnityMCP는 컨트롤러가 수행**(구현 서브에이전트는 코드+git만). 인스턴스: client `LeagueOfPhysical-Client@de70658b9450cbb4`, server `LeagueOfPhysical-Server@f99391fa2dbaaf3c`(해시는 `mcpforunity://instances`로 확인). GameFramework는 **패키지**라 클·서 editor 양쪽에서 컴파일된다(GF 전용 editor 없음).
- 파일 삭제 시 `.meta`도(`git rm`이 `.cs.meta`를 자동 스테이징 안 하면 명시 `git rm`).
- **서버 do-not-commit 픽스처**(`Entrance/EntranceComponent/ConfigureRoomComponent.cs`·`Game/GameRuleSystem.cs`·`DefaultVolumeProfile.asset`·`GraphicsSettings.asset`) 스테이징 금지 — 명시 `git add`만.
- 브랜치: client `feature/entity-s3-substrate-deletion`(존재), GameFramework·server는 생성. 커밋 끝 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
- **Assembly-CSharp라 클·서 유닛 불가** — 검증=컴파일 + 인게임. GF는 EditMode 회귀(269 green 유지).

---

## File Structure

- **GameFramework** `Runtime/Scripts/`: Delete `Component/IComponent.cs`, `Component/MonoComponent.cs`, `Entity/MonoEntity.cs`(T2), `Extensions/Entity.Extensions.cs`(T2). Modify `Entity/IEntity.cs`(T1·T2), `Extensions/Entity.Extensions.cs`(T1).
- **Client** `Assets/Scripts/`: Delete `Component/LOPComponent.cs`. Modify `Entity/LOPEntity.cs`, `Entity/LOPEntityManager.cs`, `Entity/LOPEntityView.cs`(T2 — call site 무변경 확인만).
- **Server** `Assets/Scripts/`: Delete `Component/LOPComponent.cs`, `Component/Status.cs`. Modify `Entity/LOPEntity.cs`, `Entity/LOPEntityManager.cs`.

---

## Task 1: S3a — 컴포넌트 substrate 죽은 삭제

**Files:**
- GameFramework: Delete `Runtime/Scripts/Component/IComponent.cs`, `Runtime/Scripts/Component/MonoComponent.cs`; Modify `Runtime/Scripts/Extensions/Entity.Extensions.cs`, `Runtime/Scripts/Entity/IEntity.cs`, `Runtime/Scripts/Entity/MonoEntity.cs`.
- Client: Delete `Assets/Scripts/Component/LOPComponent.cs`; Modify `Assets/Scripts/Entity/LOPEntityManager.cs`.
- Server: Delete `Assets/Scripts/Component/LOPComponent.cs`, `Assets/Scripts/Component/Status.cs`; Modify `Assets/Scripts/Entity/LOPEntity.cs`, `Assets/Scripts/Entity/LOPEntityManager.cs`.

**Interfaces:**
- Produces: `IEntity`/`MonoEntity` 축소형(`{ entityId, position/rotation/velocity, UpdateEntity }`), `Extensions`에 `GetEntityTransform`+`IsGrounded`만.

- [ ] **Step 1: GameFramework·server 브랜치 생성**

```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework checkout -b feature/entity-s3-substrate-deletion
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server checkout -b feature/entity-s3-substrate-deletion
```

- [ ] **Step 2: GF 컴포넌트 타입·확장 삭제**

Delete files (with `.meta`):
```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework rm Runtime/Scripts/Component/IComponent.cs Runtime/Scripts/Component/MonoComponent.cs
```
Replace `Runtime/Scripts/Extensions/Entity.Extensions.cs` entirely with (컴포넌트 메서드 전부 제거, `GetEntityTransform`+`IsGrounded`만 유지):
```csharp
using System;
using System.Linq;
using UnityEngine;

namespace GameFramework
{
    public static partial class Extensions
    {
        public static EntityTransform GetEntityTransform(this IEntity entity)
        {
            return new EntityTransform
            {
                position = entity.position,
                rotation = entity.rotation,
                velocity = entity.velocity
            };
        }

        // TODO: 고도화 필요!
        [Obsolete("임시 구현: 고도화 필요!")]
        public static bool IsGrounded(this IEntity entity)
        {
            Vector3 checkPosition = entity.position + Vector3.down * 0.2f;
            Collider[] colliders = Physics.OverlapSphere(checkPosition, 0.4f);

            return colliders.Any(col => col.gameObject.name == "Plane");
        }
    }
}
```

- [ ] **Step 3: GF `IEntity`·`MonoEntity`에서 컴포넌트 멤버 제거**

Replace `Runtime/Scripts/Entity/IEntity.cs` with:
```csharp
using UnityEngine;

namespace GameFramework
{
    public interface IEntity
    {
        string entityId { get; }
        Vector3 position { get; set; }
        Vector3 rotation { get; set; }
        Vector3 velocity { get; set; }

        void UpdateEntity();
    }
}
```
Replace `Runtime/Scripts/Entity/MonoEntity.cs` with:
```csharp
using UnityEngine;

namespace GameFramework
{
    public abstract class MonoEntity : MonoBehaviour, IEntity
    {
        public string entityId { get; protected set; }
        public virtual Vector3 position { get; set; }
        public virtual Vector3 rotation { get; set; }
        public virtual Vector3 velocity { get; set; }

        public abstract void UpdateEntity();
    }
}
```

- [ ] **Step 4: 클라 `LOPComponent` 삭제 + 매니저 정리 루프 제거**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client rm Assets/Scripts/Component/LOPComponent.cs
```
`Assets/Scripts/Entity/LOPEntityManager.cs`의 `DestroyMarkedEntities`에서 아래 3줄 제거:
```csharp
                foreach (var component in lopEntity.components.ToArray())
                {
                    lopEntity.DetachEntityComponent(component);
                }
```
(그 다음 `ICleanup` 루프는 유지.)

- [ ] **Step 5: 서버 `LOPComponent`/`Status` 삭제 + `UpdateStatuses` 제거 + 매니저 루프 제거**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server rm Assets/Scripts/Component/LOPComponent.cs Assets/Scripts/Component/Status.cs
```
`Assets/Scripts/Entity/LOPEntity.cs`(서버): `UpdateStatuses()` 메서드 전체 삭제 + `UpdateEntity()`를 빈 본문으로:
```csharp
        public override void UpdateEntity()
        {
        }
```
(즉 `UpdateStatuses()` 호출 줄 + 메서드 정의 삭제.)
`Assets/Scripts/Entity/LOPEntityManager.cs`(서버)의 `DestroyMarkedEntities`에서 아래 3줄 제거:
```csharp
                foreach (var component in lopEntity.components.ToArray())
                {
                    lopEntity.DetachEntityComponent(component);
                }
```

- [ ] **Step 6: 커밋 (GF·클·서, 컨트롤러가 컴파일 검증)**

```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework add -A Runtime/Scripts
git -C C:/Users/re5na/workspace/LOP/GameFramework commit -m "refactor(entity): 컴포넌트 substrate 삭제 (IComponent/MonoComponent + 컴포넌트 확장 + IEntity/MonoEntity 컴포넌트 멤버)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add Assets/Scripts/Component Assets/Scripts/Entity/LOPEntityManager.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "refactor(entity): 클라 LOPComponent 삭제 + 매니저 죽은 정리 루프 제거

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Component Assets/Scripts/Entity/LOPEntity.cs Assets/Scripts/Entity/LOPEntityManager.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server commit -m "refactor(entity): 서버 LOPComponent/Status 삭제 + UpdateStatuses/매니저 루프 제거

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
(GF 삭제 파일의 `.meta`는 `git add -A Runtime/Scripts`가 스테이징. 클·서 `.meta`도 확인.)

---

## Task 2: S3b — MonoEntity 삭제 + LOPEntity → MonoBehaviour,IEntity

**Files:**
- GameFramework: Delete `Runtime/Scripts/Entity/MonoEntity.cs`, `Runtime/Scripts/Extensions/Entity.Extensions.cs`; Modify `Runtime/Scripts/Entity/IEntity.cs`.
- Client: Modify `Assets/Scripts/Entity/LOPEntity.cs`, `Assets/Scripts/Entity/LOPEntityManager.cs`. (`Assets/Scripts/Entity/LOPEntityView.cs` — 호출부 `entity.IsGrounded()` 무변경 확인만.)
- Server: Modify `Assets/Scripts/Entity/LOPEntity.cs`, `Assets/Scripts/Entity/LOPEntityManager.cs`.

**Interfaces:**
- Consumes: Task 1의 축소형 `IEntity`.
- Produces: `LOPEntity : MonoBehaviour, IEntity`(entityId+파사드 자체 선언, `IsGrounded()` 인스턴스 메서드[클라]). `MonoEntity`/`Entity.Extensions` 삭제됨.

- [ ] **Step 1: GF `IEntity`에서 `UpdateEntity` 제거 + `MonoEntity`/`Extensions` 삭제**

Replace `Runtime/Scripts/Entity/IEntity.cs` with:
```csharp
using UnityEngine;

namespace GameFramework
{
    public interface IEntity
    {
        string entityId { get; }
        Vector3 position { get; set; }
        Vector3 rotation { get; set; }
        Vector3 velocity { get; set; }
    }
}
```
Delete (with `.meta`):
```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework rm Runtime/Scripts/Entity/MonoEntity.cs Runtime/Scripts/Extensions/Entity.Extensions.cs
```
(`GetEntityTransform`은 호출 0이라 함께 소멸. `IsGrounded`는 Step 2에서 `LOPEntity`로 이전.)

- [ ] **Step 2: 클라 `LOPEntity` → `MonoBehaviour, IEntity` + IsGrounded 이전**

`Assets/Scripts/Entity/LOPEntity.cs`(클라):
① 클래스 선언 `public class LOPEntity : MonoEntity` → `public class LOPEntity : MonoBehaviour, IEntity`.
② `entityId` 선언 추가(MonoEntity가 주던 것 — 클래스 상단):
```csharp
        public string entityId { get; private set; }
```
③ `position`/`rotation`/`velocity`의 `public override Vector3` → `public Vector3`(3곳, `override` 제거, 본문 유지).
④ `public override void UpdateEntity() { }` 메서드 삭제.
⑤ `IsGrounded()` 인스턴스 메서드 추가(구 GF 확장에서 이전, 클래스 내 아무 곳):
```csharp
        // TODO: 고도화 필요! (구 GameFramework IsGrounded 확장에서 이전)
        public bool IsGrounded()
        {
            Vector3 checkPosition = position + Vector3.down * 0.2f;
            Collider[] colliders = Physics.OverlapSphere(checkPosition, 0.4f);
            return colliders.Any(col => col.gameObject.name == "Plane");
        }
```
⑥ 파일 상단 usings에 `using System.Linq;` 추가(`Any`용). `Initialize`의 `entityId = creationData.entityId;`는 그대로(같은 클래스라 private set 접근 OK).

- [ ] **Step 3: 클라 `LOPEntityView` 호출부 확인 (무변경)**

`Assets/Scripts/Entity/LOPEntityView.cs:97`의 `entity.IsGrounded()`는 이제 `LOPEntity.IsGrounded()`(인스턴스)로 자연 해소된다 — **편집 없음**. Grep로 다른 `.IsGrounded()` 호출부가 클·서에 없는지 확인만(있으면 그 타입이 LOPEntity인지 점검).

- [ ] **Step 4: 클라 `LOPEntityManager.UpdateEntities` 빈 본문화**

`Assets/Scripts/Entity/LOPEntityManager.cs`의 `UpdateEntities()`를:
```csharp
        public void UpdateEntities()
        {
        }
```
(`foreach ... entity.UpdateEntity()` 루프 제거 — `IEntity`에 `UpdateEntity`가 없어졌으므로 필수.)

- [ ] **Step 5: 서버 `LOPEntity` → `MonoBehaviour, IEntity`**

`Assets/Scripts/Entity/LOPEntity.cs`(서버):
① `public class LOPEntity : MonoEntity` → `public class LOPEntity : MonoBehaviour, IEntity`.
② `public string entityId { get; private set; }` 선언 추가.
③ `position`/`rotation`/`velocity` `public override Vector3` → `public Vector3`(override 제거, 본문 유지).
④ `public override void UpdateEntity() { }`(S3a에서 빈 함수 된 것) 삭제.
(서버는 `IsGrounded` 호출부 없음 → 추가 안 함.)

- [ ] **Step 6: 서버 `LOPEntityManager.UpdateEntities` 빈 본문화**

`Assets/Scripts/Entity/LOPEntityManager.cs`(서버)의 `UpdateEntities()`를 Step 4와 동일하게 빈 본문으로.

- [ ] **Step 7: 커밋 (GF·클·서, 컨트롤러가 컴파일+플레이 검증)**

```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework add -A Runtime/Scripts
git -C C:/Users/re5na/workspace/LOP/GameFramework commit -m "refactor(entity): MonoEntity + Entity.Extensions 삭제, IEntity를 파사드 계약으로 축소

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add Assets/Scripts/Entity/LOPEntity.cs Assets/Scripts/Entity/LOPEntityManager.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "refactor(entity): 클라 LOPEntity → MonoBehaviour,IEntity + IsGrounded 이전

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Entity/LOPEntity.cs Assets/Scripts/Entity/LOPEntityManager.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server commit -m "refactor(entity): 서버 LOPEntity → MonoBehaviour,IEntity

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: 통합 검증 + ROADMAP

**Files:** `docs/ROADMAP.md`(client)

- [ ] **Step 1: 클·서 동시 플레이 검증**

두 editor 플레이. 확인: 스폰·이동·점프·충돌·아이템 줍기·AI 이동/공격·넉백·전역공격(G)·데미지 플로터·네임플레이트 정상. 클·서 콘솔 CS 에러 0, 신규 예외 0(기존 파킹 NRE만).

- [ ] **Step 2: substrate 참조 0 확인**

Grep(클·서·GF): `MonoEntity`·`IComponent`·`MonoComponent`·`LOPComponent`·`Status`(서버 컴포넌트)·`Entity.Extensions` 참조 0. `AddEntityComponent`/`GetEntityComponent`/`components` 0. (`IEntity`는 잔존 = 정상.)

- [ ] **Step 3: ROADMAP 갱신 + 커밋**

`docs/ROADMAP.md`에 S3 완료 한 줄 추가(IEntity 얇게 유지→S5 명시) + 다음(③ 물리 통합 / S4). 커밋:
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add docs/ROADMAP.md
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "docs(roadmap): 엔티티 재구조화 S3(substrate machinery 삭제) 완료 반영

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Self-Review

**Spec coverage:** ✅ 컴포넌트 substrate 삭제(T1) · IEntity/MonoEntity 컴포넌트 멤버 제거(T1) · 서버 UpdateStatuses 제거(T1) · 매니저 정리 루프 제거(T1) · MonoEntity 삭제+LOPEntity→MonoBehaviour,IEntity(T2) · Entity.Extensions 삭제+IsGrounded 이전(T2) · UpdateEntity 제거+UpdateEntities 빈본문(T2) · IEntity 축소 유지(T1·T2, 삭제 안 함) · ③물리·매니저 재타입 이월(범위 밖) · 검증(T3). 스펙 요구 전부 매핑.

**Placeholder scan:** 코드 스텝 전부 실제 코드/전체 파일 내용. TODO/TBD 없음(코드 내 `// TODO: 고도화` 주석은 원본 보존).

**Type consistency:** `LOPEntity : MonoBehaviour, IEntity`가 축소 `IEntity`(entityId+position/rotation/velocity)를 구현 — entityId(get) + 파사드(get/set) 시그니처 일치. `IsGrounded()` 인스턴스 메서드가 확장 대체, 호출부 `entity.IsGrounded()` 무변경. `UpdateEntities()` 빈 본문이 삭제된 `UpdateEntity`와 정합.

**주의(구현자):** ① GF는 패키지 — 삭제/수정 후 클·서 editor 양쪽에서 컴파일된다(컨트롤러가 둘 다 refresh). ② `IEntity`는 **삭제 금지**(매니저/팩토리/IBrain/EntityCreated가 씀). ③ `MonoEntity`가 주던 `entityId`를 LOPEntity가 자체 선언(`{ get; private set; }`) — Initialize의 `entityId =` 대입이 같은 클래스라 유효. ④ 서버 매니저의 `GetAllEntitySnaps`/`GetAllEntityCreationDatas`는 `IEntity` 파사드를 그대로 읽음(IEntity 유지라 무변경).
