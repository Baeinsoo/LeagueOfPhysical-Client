# 판치기 슬라이스 3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 판을 끌어서 놓으면 동전이 튀고 구르고 뒤집힌다 — 두 클라가 같은 것을 본다.

**Architecture:** 두 단계다. **3-0**은 물리 포트에서 엔티티 찾기를 떼어낸다(기능 변화 0) — 유니티처럼 히트가 콜라이더를 나르고, 매핑은 부르는 쪽이 확장 메서드로 한다. **3-1**은 그 위에 타격을 얹는다: 클라가 판을 끌어 `PanchigiStrikeToS` 한 통을 보내고, 서버가 검증한 뒤 동전마다 "판에 실제로 닿은 정도(덮임)"를 샘플로 재서 임펄스 하나를 준다. 힘 계산은 순수 함수라 EditMode 테스트가 붙는다.

**Tech Stack:** Unity 6000.3.16f1 · C# · VContainer · MessagePipe · Protobuf(Luban은 마스터데이터) · Mirror · NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-08-24-panchigi-slice3-strike-design.md`
(상위 spec: `docs/superpowers/specs/2026-08-24-panchigi-game-mode-design.md`)

## Global Constraints

- **레포 5개를 건드린다**: `GameFramework`, `LeagueOfPhysical-Shared`, `LeagueOfPhysical-Client`, `LeagueOfPhysical-Server`, `infrastructure`(+ 생성물이 `LeagueOfPhysical-MasterData-Client/Server`, `lop-backend`에 떨어짐). 레포마다 각각 피처 브랜치에서 작업한다.
- **main 직접 커밋 금지.** 각 레포에서 피처 브랜치를 판다. 푸시는 `CLAUDE.md`의 "푸시 규약"만 따른다(리베이스 → `--ff-only` → `--no-ff` 머지). **`git push --force` 금지.**
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고, 커밋 전 `git status --short`로 스테이지된 것이 의도한 파일뿐인지 확인한다. Unity 레포에는 커밋하면 안 되는 로컬 픽스처가 늘 있다(Addressables 그룹, `Assets/Art` 서브모듈 포인터, `Jua-Regular SDF.asset`, `PackageManagerSettings.asset`).
- **`.meta` 파일은 반드시 함께 커밋한다.** 직접 만들지 말고 Unity가 생성한 것만 커밋한다. 파일 이름을 바꿀 때는 `.cs`와 짝 `.meta`를 함께 `git mv`.
- **World 타입은 항상 풀 네임스페이스로 한정한다** — `GameFramework.World.Component`가 `UnityEngine.Component`와 겹친다. `using GameFramework.World;`를 추가하지 않는다. 예: `GameFramework.World.Entity worldEntity = ...`.
- **주석은 최소·일상어.** 코드로 자명한 것(무엇)은 쓰지 않고, 비자명한 의도(왜)만 짧게. 전문용어를 설명 없이 던지지 않는다. `public` 타입·멤버 문서화는 `/// <summary>`.
- **네이밍은 업계 표준 차용.** 이 계획의 새 이름은 전부 `UnityEngine.Physics`에서 따왔다(`Raycast` / `OverlapSphere` / `CapsuleCast`). 임의로 바꾸지 않는다.
- **레거시 `Input` 클래스 금지.** New Input System(`UnityEngine.InputSystem`)만 쓴다. `#if UNITY_EDITOR / #elif UNITY_IOS||UNITY_ANDROID` 분기 금지 — 그 분기가 원본에서 데스크톱 입력을 통째로 죽였다.
- **MonoBehaviour는 필드 주입(`[Inject]`), 순수 C#은 생성자 주입.** 이건 이 프로젝트의 의도된 배치다.
- **`Entity.Add<T>`는 `typeof(T)`를 키로 쓴다.** 베이스 타입으로 꺼낼 것은 제네릭을 명시해서 넣는다(`worldEntity.Add<GameFramework.World.PhysicsBody>(...)`).
- **검증은 컴파일 + EditMode 두 가지다.** 클라 앱 코드는 asmdef가 없어 EditMode를 못 붙이므로, 순수 로직은 패키지(LOP-Shared / GameFramework) 테스트에 놓는다.
- 컴파일·테스트는 실행 중인 에디터에 `unity` CLI로 붙어서 확인한다. **`--project-path`를 매번 명시**한다(클·서 에디터가 동시에 붙어 있다).

```bash
U="$HOME/AppData/Local/Unity/bin/unity"
CLIENT="C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client"
SERVER="C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server"

"$U" command recompile        --project-path "$SERVER"
"$U" command recompile_status --project-path "$SERVER"   # {"status":"up_to_date","failed":false,"errors":[]}
"$U" command run_tests --mode EditMode --async_tests true --project-path "$SERVER"
"$U" command test_status --project-path "$SERVER"
```

> ⚠️ `recompile_status`가 `failed:false`라고 해서 에디터가 멀쩡하다는 뜻은 아니다 — **안전 모드에 갇혀 있어도 그렇게 보고한다.** 컴파일이 진짜 됐는지는 **테스트가 실제로 도는지**까지 봐야 안다.

---

## File Structure

### 3-0 — 물리 포트 정리

| 파일 | 책임 |
|---|---|
| `GameFramework/Runtime/Scripts/Physics/CollisionHit.cs` | **수정** — `Collider` 필드 추가. 히트가 "맞은 것"을 나른다 |
| `GameFramework/Runtime/Scripts/Physics/ICollisionQuery.cs` | **수정** — `Raycast` 추가, `OverlapSphere` 흡수 |
| `GameFramework/Runtime/Scripts/Physics/UnityCollisionQuery.cs` | **수정** — 위 둘 구현 |
| `GameFramework/Runtime/Scripts/Physics/IOverlapQuery.cs` | **삭제** |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Entity/EntityActor.cs` | **신규** — 몸에 붙는 엔티티 신원(`entityId`). Shared가 볼 수 있는 타입 |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Physics/CollisionHitExtensions.cs` | **신규** — `hit.GetEntityId()` |
| `LeagueOfPhysical-Shared/Tests/EditMode/CollisionHitExtensionsTests.cs` | **신규** |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/DamageEffectHandler.cs` | **수정** — `ICollisionQuery`로 이전 + 중복 제거 |
| `LeagueOfPhysical-Shared/Tests/EditMode/DamageEffectHandlerTests.cs` | **수정** — 가짜 구현 교체 |
| `LeagueOfPhysical-Client/Assets/Scripts/Entity/LOPActor.cs` | **수정** — `EntityActor` 상속, 뷰 부분만 남김 |
| `LeagueOfPhysical-Server/Assets/Scripts/Entity/LOPActor.cs` | **수정** — `EntityActor` 상속, 빈 껍데기 |
| `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPOverlapQuery.cs` | **삭제** |
| `LeagueOfPhysical-Server/Assets/Scripts/Game/GameplayInstaller.cs` | **수정** — `IOverlapQuery` 등록 삭제 |

### 3-1 — 타격

| 파일 | 책임 |
|---|---|
| `GameFramework/Runtime/Scripts/World/PhysicsBody.cs` | **수정** — `AddImpulseAtPosition` 추가 |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/UnityPhysicsBody.cs` | **수정** — 위 구현 |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/PanchigiStrikeKernel.cs` | **신규** — 순수 힘 커널 + 샘플 배치 |
| `LeagueOfPhysical-Shared/Tests/EditMode/PanchigiStrikeKernelTests.cs` | **신규** |
| `LeagueOfPhysical-Shared/Protos/PanchigiStrikeToS.proto` | **신규** — 와이어 |
| `infrastructure/table/Datas/#PanchigiConfig.xlsx` | **수정** — 커널 노브 5개 |
| `LeagueOfPhysical-Server/Assets/Scripts/Game/MessageHandler/PanchigiStrikeMessageHandler.cs` | **신규** — 검증 + 샘플 걸러내기 + 임펄스 |
| `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiLifetimeScope.cs` | **수정** — 핸들러 등록 |
| `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiRuleSystem.cs` | **수정** — 동전 visualId |
| `LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiStrikeInput.cs` | **신규** — 포인터 캡처 + 조준선 + 송신 |
| `LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiLifetimeScope.cs` | **수정** — 입력 컴포넌트 등록 |
| `LeagueOfPhysical-Client/Assets/Art_Placeholder/Panchigi/Coin.prefab` | **신규** — 임시 동전 몸(실린더). 아트가 들어오면 교체 |
| `LeagueOfPhysical-Client/Assets/Scenes/Panchigi.unity` | **수정** — 판 두께·카메라·입력 컴포넌트 |
| `LeagueOfPhysical-Server/Assets/Scenes/Panchigi.unity` | **수정** — 판 두께(클라와 같은 값) |

---

## Task 1: `EntityActor` — 엔티티 신원을 Shared로

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Entity/EntityActor.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/LOPActor.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entity/LOPActor.cs`

**Interfaces:**
- Consumes: (없음 — 첫 태스크)
- Produces: `LOP.EntityActor` (MonoBehaviour) — `public string entityId { get; }`, `public void SetEntityId(string)`. Task 3의 `GetEntityId()`가 이 타입을 찾는다.

**왜:** Task 3에서 만들 `hit.GetEntityId()`는 LOP-Shared에 있어야 한다(`DamageEffectHandler`가 Shared라서). 그런데 Shared는 클라·서버 코드를 못 본다 — `LOPActor`를 쓸 수 없다. 그래서 `entityId`를 들고 있는 부분만 Shared로 올린다. 마침 그 부분이 클·서에 똑같이 복사돼 있어 중복도 없어진다.

- [ ] **Step 1: `EntityActor` 생성**

`LeagueOfPhysical-Shared/Runtime/Scripts/Entity/EntityActor.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 엔티티의 몸에 붙는 신원표. 물리 쿼리가 콜라이더를 돌려줬을 때 "이게 어느 엔티티냐"를
    /// 되찾는 실마리다. 클·서가 각자 더 붙일 것이 있어(클라는 뷰) 이 타입을 상속해서 쓴다.
    /// </summary>
    public class EntityActor : MonoBehaviour
    {
        public string entityId { get; private set; }

        public void SetEntityId(string entityId)
        {
            this.entityId = entityId;
        }
    }
}
```

- [ ] **Step 2: 서버 `LOPActor`를 껍데기로**

`LeagueOfPhysical-Server/Assets/Scripts/Entity/LOPActor.cs` 전체를 교체:

```csharp
namespace LOP
{
    /// <summary>서버 쪽 엔티티 몸의 신원. 지금은 <see cref="EntityActor"/>가 전부다.</summary>
    public class LOPActor : EntityActor
    {
    }
}
```

- [ ] **Step 3: 클라 `LOPActor`에서 중복 제거**

`LeagueOfPhysical-Client/Assets/Scripts/Entity/LOPActor.cs` 전체를 교체(뷰 부분만 남는다):

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>클라 쪽 엔티티 몸의 신원 + 그 몸이 쓰는 뷰로 가는 길.</summary>
    public class LOPActor : EntityActor
    {
        private LOPEntityView view;

        // 스포너가 뷰를 만든 뒤 등록한다(Actor 생성 시점엔 뷰가 아직 없음).
        public void SetView(LOPEntityView view)
        {
            this.view = view;
        }

        // 렌더되는 모델 GameObject. 뷰가 async 로드 전이거나 파괴됐으면 null.
        public GameObject visualGameObject => view != null ? view.visualGameObject : null;
    }
}
```

- [ ] **Step 4: 양쪽 컴파일 확인**

```bash
"$U" command recompile --project-path "$CLIENT"; "$U" command recompile_status --project-path "$CLIENT"
"$U" command recompile --project-path "$SERVER"; "$U" command recompile_status --project-path "$SERVER"
```
기대: 양쪽 `{"status":"up_to_date","failed":false,"errors":[]}`.

`SetEntityId(...)`를 부르던 곳(`EntityBinder` 클·서)은 상속이라 **한 줄도 안 바뀐다.** 바뀌었다면 뭔가 잘못된 것이니 멈추고 확인한다.

- [ ] **Step 5: 커밋 (레포 3개 각각)**

```bash
# LOP-Shared
git -C ../LeagueOfPhysical-Shared add Runtime/Scripts/Entity/EntityActor.cs Runtime/Scripts/Entity/EntityActor.cs.meta
git -C ../LeagueOfPhysical-Shared status --short
git -C ../LeagueOfPhysical-Shared commit -m "feat(entity): 엔티티 신원표를 공유 타입으로"

# Server
git -C ../LeagueOfPhysical-Server add Assets/Scripts/Entity/LOPActor.cs
git -C ../LeagueOfPhysical-Server status --short
git -C ../LeagueOfPhysical-Server commit -m "refactor(entity): LOPActor가 EntityActor를 상속한다"

# Client
git add Assets/Scripts/Entity/LOPActor.cs
git status --short
git commit -m "refactor(entity): LOPActor가 EntityActor를 상속한다"
```

> `.meta`가 아직 없으면 Unity가 만들 때까지 기다린 뒤 add한다(에디터 포커스가 필요할 수 있다).

---

## Task 2: 물리 포트에 `Raycast`·`OverlapSphere` 추가

**Files:**
- Modify: `GameFramework/Runtime/Scripts/Physics/CollisionHit.cs`
- Modify: `GameFramework/Runtime/Scripts/Physics/ICollisionQuery.cs`
- Modify: `GameFramework/Runtime/Scripts/Physics/UnityCollisionQuery.cs`

**Interfaces:**
- Consumes: (없음)
- Produces:
  - `GameFramework.Physics.CollisionHit` — 필드 `HasHit`(bool) / `Distance`(float) / `Normal`(UnityEngine.Vector3) / `Point`(UnityEngine.Vector3) / `Collider`(UnityEngine.Collider). 생성자 `CollisionHit(bool, float, Vector3, Vector3, Collider)`.
  - `GameFramework.Physics.ICollisionQuery.Raycast(Vector3 origin, Vector3 direction, float distance, int layerMask) → CollisionHit`
  - `GameFramework.Physics.ICollisionQuery.OverlapSphere(Vector3 center, float radius, int layerMask) → CollisionHit[]`
  - 기존 `CapsuleCast`는 시그니처 불변.

**왜:** 유니티 `RaycastHit`이 좌표와 `.collider`를 함께 나르듯, 우리 히트도 "맞은 것"을 나르게 한다. 그래야 엔티티 매핑을 포트 밖으로 뺄 수 있다.

- [ ] **Step 1: `CollisionHit`에 `Collider` 추가**

`GameFramework/Runtime/Scripts/Physics/CollisionHit.cs` 전체 교체:

```csharp
using UnityEngine;

namespace GameFramework.Physics
{
    /// <summary>
    /// 충돌 쿼리 결과(엔진 RaycastHit을 포트 경계에서 격리한 얇은 값 타입).
    /// <see cref="Collider"/>는 유니티 <c>RaycastHit.collider</c>와 같은 자리다 — 맞은 것이 게임의
    /// 무엇인지는 이 계층이 알지 않고, 부르는 쪽이 이걸로 되짚는다.
    /// </summary>
    public readonly struct CollisionHit
    {
        public readonly bool HasHit;
        public readonly float Distance;
        public readonly Vector3 Normal;
        public readonly Vector3 Point;
        public readonly Collider Collider;

        public CollisionHit(bool hasHit, float distance, Vector3 normal, Vector3 point, Collider collider)
        {
            HasHit = hasHit;
            Distance = distance;
            Normal = normal;
            Point = point;
            Collider = collider;
        }

        public static CollisionHit None => new CollisionHit(false, 0f, Vector3.zero, Vector3.zero, null);
    }
}
```

- [ ] **Step 2: `ICollisionQuery`에 두 질문 추가**

`GameFramework/Runtime/Scripts/Physics/ICollisionQuery.cs` 전체 교체:

```csharp
using UnityEngine;

namespace GameFramework.Physics
{
    /// <summary>
    /// 물리 충돌 쿼리 포트. 엔진 물리(PhysX)에 직결되지 않도록 주입한다.
    /// <see cref="IPhysicsSimulator"/>(스텝 구동)와 짝을 이루는 쿼리 추상.
    /// 클·서 양쪽 동일 구체(<see cref="UnityCollisionQuery"/>)를 사용한다 — 이동 판정이 양쪽에서
    /// 같아야 하기 때문이다.
    ///
    /// 메서드 이름은 <c>UnityEngine.Physics</c>에서 그대로 차용한다.
    /// </summary>
    public interface ICollisionQuery
    {
        /// <summary>
        /// 캡슐(양 끝 구 중심 <paramref name="point1"/>·<paramref name="point2"/>, 반지름
        /// <paramref name="radius"/>)을 <paramref name="direction"/> 방향으로
        /// <paramref name="distance"/>만큼 쓸어 첫 충돌을 반환한다. 없으면 <see cref="CollisionHit.None"/>.
        /// </summary>
        CollisionHit CapsuleCast(Vector3 point1, Vector3 point2, float radius,
            Vector3 direction, float distance, int layerMask);

        /// <summary>
        /// <paramref name="origin"/>에서 <paramref name="direction"/> 방향으로
        /// <paramref name="distance"/>만큼 광선을 쏴 첫 충돌을 반환한다. 없으면 <see cref="CollisionHit.None"/>.
        /// </summary>
        CollisionHit Raycast(Vector3 origin, Vector3 direction, float distance, int layerMask);

        /// <summary>
        /// 중심 <paramref name="center"/>·반지름 <paramref name="radius"/> 구에 겹치는 콜라이더들.
        /// 겹치는 게 없으면 빈 배열.
        ///
        /// ⚠️ 겹침 검사는 접촉 지점을 알려주지 않는다 — 반환된 히트의
        /// <see cref="CollisionHit.Point"/>·<see cref="CollisionHit.Normal"/>·
        /// <see cref="CollisionHit.Distance"/>는 **0으로 채워지며 의미가 없다.**
        /// <see cref="CollisionHit.Collider"/>만 유효하다.
        ///
        /// ⚠️ 한 엔티티가 콜라이더를 여럿 가질 수 있으므로 **같은 대상이 여러 번 나올 수 있다.**
        /// 중복 제거는 부르는 쪽 몫이다.
        /// </summary>
        CollisionHit[] OverlapSphere(Vector3 center, float radius, int layerMask);
    }
}
```

- [ ] **Step 3: `UnityCollisionQuery`에 구현**

`GameFramework/Runtime/Scripts/Physics/UnityCollisionQuery.cs` 전체 교체:

```csharp
using UnityEngine;

namespace GameFramework.Physics
{
    /// <summary>Unity 내장 물리(PhysX)로 <see cref="ICollisionQuery"/>를 구현하는 어댑터.</summary>
    public sealed class UnityCollisionQuery : ICollisionQuery
    {
        public CollisionHit CapsuleCast(Vector3 point1, Vector3 point2, float radius,
            Vector3 direction, float distance, int layerMask)
        {
            // 이동 sweep은 트리거(아이템 픽업 등)에 막히면 안 된다 → 트리거 무시.
            if (UnityEngine.Physics.CapsuleCast(point1, point2, radius, direction, out RaycastHit hit,
                    distance, layerMask, QueryTriggerInteraction.Ignore))
            {
                return new CollisionHit(true, hit.distance, hit.normal, hit.point, hit.collider);
            }
            return CollisionHit.None;
        }

        public CollisionHit Raycast(Vector3 origin, Vector3 direction, float distance, int layerMask)
        {
            // 트리거를 무시하는 이유는 CapsuleCast와 같다 — 통과해야 할 것에 막히면 안 된다.
            if (UnityEngine.Physics.Raycast(origin, direction, out RaycastHit hit,
                    distance, layerMask, QueryTriggerInteraction.Ignore))
            {
                return new CollisionHit(true, hit.distance, hit.normal, hit.point, hit.collider);
            }
            return CollisionHit.None;
        }

        public CollisionHit[] OverlapSphere(Vector3 center, float radius, int layerMask)
        {
            Collider[] colliders = UnityEngine.Physics.OverlapSphere(center, radius, layerMask,
                QueryTriggerInteraction.Ignore);

            var hits = new CollisionHit[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                // 겹침 검사는 접촉 지점을 주지 않는다 — 좌표 자리는 비워 두고 콜라이더만 싣는다.
                hits[i] = new CollisionHit(true, 0f, Vector3.zero, Vector3.zero, colliders[i]);
            }
            return hits;
        }
    }
}
```

- [ ] **Step 4: 양쪽 컴파일 확인**

```bash
"$U" command recompile --project-path "$SERVER"; "$U" command recompile_status --project-path "$SERVER"
"$U" command recompile --project-path "$CLIENT"; "$U" command recompile_status --project-path "$CLIENT"
```
기대: 에러 0. `CollisionHit` 생성자에 인자가 하나 늘었으므로 **다른 호출부가 있으면 여기서 깨진다** — 깨지면 그 자리에 `null`을 넘겨 고친다(그 자리는 콜라이더를 알 방법이 없다는 뜻이다).

- [ ] **Step 5: GameFramework EditMode 테스트**

```bash
"$U" command run_tests --mode EditMode --async_tests true --project-path "$SERVER"
"$U" command test_status --project-path "$SERVER"
```
기대: 기존 통과 개수가 그대로거나 늘어난다. 줄었으면 멈추고 확인한다.

- [ ] **Step 6: 커밋**

```bash
git -C ../GameFramework add Runtime/Scripts/Physics/CollisionHit.cs Runtime/Scripts/Physics/ICollisionQuery.cs Runtime/Scripts/Physics/UnityCollisionQuery.cs
git -C ../GameFramework status --short
git -C ../GameFramework commit -m "feat(physics): 히트가 콜라이더를 나르고 Raycast·OverlapSphere를 더한다"
```

---

## Task 3: `hit.GetEntityId()` — 매핑을 포트 밖으로

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Physics/CollisionHitExtensions.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/CollisionHitExtensionsTests.cs`

**Interfaces:**
- Consumes: Task 1의 `LOP.EntityActor`, Task 2의 `GameFramework.Physics.CollisionHit`(생성자 5인자, `Collider` 필드)
- Produces: `LOP.CollisionHitExtensions.GetEntityId(this GameFramework.Physics.CollisionHit) → string` (엔티티가 아니면 `null`)

- [ ] **Step 1: 실패하는 테스트 작성**

`LeagueOfPhysical-Shared/Tests/EditMode/CollisionHitExtensionsTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class CollisionHitExtensionsTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        private GameObject MakeBody(string entityId)
        {
            var go = new GameObject("body");
            go.AddComponent<SphereCollider>();
            go.AddComponent<EntityActor>().SetEntityId(entityId);
            spawned.Add(go);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                Object.DestroyImmediate(go);
            }
            spawned.Clear();
        }

        private static GameFramework.Physics.CollisionHit HitOn(Collider collider)
            => new GameFramework.Physics.CollisionHit(true, 0f, Vector3.zero, Vector3.zero, collider);

        [Test]
        public void 콜라이더에_붙은_엔티티_id를_돌려준다()
        {
            GameObject body = MakeBody("entity-7");

            string id = HitOn(body.GetComponent<Collider>()).GetEntityId();

            Assert.AreEqual("entity-7", id);
        }

        [Test]
        public void 자식_콜라이더면_부모에서_찾는다()
        {
            GameObject body = MakeBody("entity-7");
            var child = new GameObject("visual");
            child.transform.SetParent(body.transform);
            var childCollider = child.AddComponent<BoxCollider>();

            string id = HitOn(childCollider).GetEntityId();

            Assert.AreEqual("entity-7", id);
        }

        [Test]
        public void 엔티티가_아닌_것을_맞으면_null()
        {
            var plain = new GameObject("board");
            plain.AddComponent<BoxCollider>();
            spawned.Add(plain);

            string id = HitOn(plain.GetComponent<Collider>()).GetEntityId();

            Assert.IsNull(id);
        }

        [Test]
        public void 아무것도_안_맞았으면_null()
        {
            Assert.IsNull(GameFramework.Physics.CollisionHit.None.GetEntityId());
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
"$U" command run_tests --mode EditMode --async_tests true --project-path "$SERVER"
"$U" command test_status --project-path "$SERVER"
```
기대: 컴파일 실패 — `GetEntityId`가 없다.

- [ ] **Step 3: 구현**

`LeagueOfPhysical-Shared/Runtime/Scripts/Physics/CollisionHitExtensions.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>물리 히트에서 게임 쪽 신원을 되짚는다. 물리 계층은 엔티티를 알지 않는다.</summary>
    public static class CollisionHitExtensions
    {
        /// <summary>맞은 몸의 엔티티 id. 엔티티가 아닌 것(판·지형)을 맞았으면 null.</summary>
        public static string GetEntityId(this GameFramework.Physics.CollisionHit hit)
        {
            if (hit.Collider == null)
            {
                return null;
            }

            EntityActor actor = hit.Collider.GetComponentInParent<EntityActor>();
            return actor != null ? actor.entityId : null;
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

```bash
"$U" command run_tests --mode EditMode --async_tests true --project-path "$SERVER"
"$U" command test_status --project-path "$SERVER"
```
기대: 새 테스트 4개 전부 통과.

- [ ] **Step 5: 커밋**

```bash
git -C ../LeagueOfPhysical-Shared add Runtime/Scripts/Physics/CollisionHitExtensions.cs Runtime/Scripts/Physics/CollisionHitExtensions.cs.meta Tests/EditMode/CollisionHitExtensionsTests.cs Tests/EditMode/CollisionHitExtensionsTests.cs.meta
git -C ../LeagueOfPhysical-Shared status --short
git -C ../LeagueOfPhysical-Shared commit -m "feat(physics): 히트에서 엔티티를 되짚는 확장"
```

---

## Task 4: `DamageEffectHandler`를 새 포트로 옮긴다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/DamageEffectHandler.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/DamageEffectHandlerTests.cs`

**Interfaces:**
- Consumes: Task 2의 `ICollisionQuery.OverlapSphere(Vector3, float, int) → CollisionHit[]`, Task 3의 `GetEntityId()`
- Produces: `DamageEffectHandler` 생성자가 `IOverlapQuery` 대신 `GameFramework.Physics.ICollisionQuery`를 받는다. Task 5의 등록 삭제가 이걸 전제한다.

**⚠️ 이 태스크의 함정:** 옛 `LOPOverlapQuery`는 `HashSet`으로 **같은 엔티티 중복을 없애 주고 있었다**(한 엔티티가 콜라이더를 여럿 가질 수 있다). 그 책임이 포트에서 나오므로 **핸들러가 이어받아야 한다.** 빠뜨려도 컴파일·테스트·게임이 다 정상으로 보이고, 데미지만 조용히 두 배가 된다.

- [ ] **Step 1: 실패하는 테스트 먼저 — 중복 방어**

`DamageEffectHandlerTests.cs`의 `FakeOverlap`을 지우고 아래로 바꾼다. 기존 7개 테스트의 `new FakeOverlap("a","b")` 호출은 `new FakeQuery(...)`로 이름만 바꾸면 된다.

```csharp
// 파일 맨 위 using에 추가: using System.Collections.Generic;
// 주의: 이 파일은 using System.Numerics; 라서 Vector3 = System.Numerics.Vector3다.
//       Unity 쪽 타입은 UnityEngine.으로 풀어 쓴다.

private sealed class FakeQuery : GameFramework.Physics.ICollisionQuery
{
    private readonly GameFramework.Physics.CollisionHit[] hits;

    public FakeQuery(params GameFramework.Physics.CollisionHit[] hits) { this.hits = hits; }

    public GameFramework.Physics.CollisionHit[] OverlapSphere(
        UnityEngine.Vector3 center, float radius, int layerMask) => hits;

    public GameFramework.Physics.CollisionHit CapsuleCast(
        UnityEngine.Vector3 p1, UnityEngine.Vector3 p2, float r,
        UnityEngine.Vector3 dir, float dist, int layerMask)
        => GameFramework.Physics.CollisionHit.None;

    public GameFramework.Physics.CollisionHit Raycast(
        UnityEngine.Vector3 origin, UnityEngine.Vector3 dir, float dist, int layerMask)
        => GameFramework.Physics.CollisionHit.None;
}

// 테스트가 쓸 가짜 몸. TearDown에서 지운다.
private readonly List<UnityEngine.GameObject> spawnedBodies = new List<UnityEngine.GameObject>();

private GameFramework.Physics.CollisionHit BodyHit(string entityId)
{
    var go = new UnityEngine.GameObject(entityId);
    var collider = go.AddComponent<UnityEngine.SphereCollider>();
    go.AddComponent<EntityActor>().SetEntityId(entityId);
    spawnedBodies.Add(go);
    return new GameFramework.Physics.CollisionHit(
        true, 0f, UnityEngine.Vector3.zero, UnityEngine.Vector3.zero, collider);
}

[TearDown]
public void TearDownBodies()
{
    foreach (var go in spawnedBodies) UnityEngine.Object.DestroyImmediate(go);
    spawnedBodies.Clear();
}
```

그리고 새 테스트를 추가한다:

```csharp
[Test]
public void 한_엔티티가_콜라이더를_여럿_가져도_한_번만_맞는다()
{
    var buf = new WorldEventBuffer();
    var reg = new EntityRegistry();
    var stats = new StatsSystem();

    Entity caster = Player("caster", reg, stats, new Vector3(0, 0, 0));
    Entity target = Player("target", reg, stats, new Vector3(0, 0, 2));

    // 같은 엔티티를 가리키는 히트 두 개 — 몸통 콜라이더 + 모델 콜라이더인 상황.
    GameFramework.Physics.CollisionHit first = BodyHit("target");
    var extra = new UnityEngine.GameObject("weapon");
    extra.transform.SetParent(first.Collider.transform);
    var extraCollider = extra.AddComponent<UnityEngine.BoxCollider>();
    GameFramework.Physics.CollisionHit second = new GameFramework.Physics.CollisionHit(
        true, 0f, UnityEngine.Vector3.zero, UnityEngine.Vector3.zero, extraCollider);

    var handler = Handler(buf, reg, stats, new FakeQuery(first, second));
    handler.OnActiveEnterForTest(Ctx(caster), new DamageEffect(...));   // 기존 테스트가 쓰는 호출 방식 그대로

    int damageEvents = 0;
    foreach (var e in buf.Snapshot) if (e is DamageDealtEvent) damageEvents++;
    Assert.AreEqual(1, damageEvents, "같은 엔티티는 한 번만 맞아야 한다");
}
```

> 기존 테스트들이 핸들러를 어떻게 호출하는지(`OnActiveEnter`가 protected라 어떤 진입점을 쓰는지)를 파일에서 확인하고 **그 방식 그대로** 쓴다. `DamageEffect`의 생성 인자도 기존 테스트에서 복사한다.

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
"$U" command run_tests --mode EditMode --async_tests true --project-path "$SERVER"
"$U" command test_status --project-path "$SERVER"
```
기대: 컴파일 실패(핸들러가 아직 `IOverlapQuery`를 받는다).

- [ ] **Step 3: 핸들러 이전**

`DamageEffectHandler.cs`에서:

1. 필드·생성자의 `GameFramework.Physics.IOverlapQuery overlapQuery` → `GameFramework.Physics.ICollisionQuery collisionQuery`
2. 클래스 XML 주석의 "IOverlapQuery(사이드 구체)에 위임하고" → "ICollisionQuery(클·서 공유)에 위임하고"
3. 레이어 마스크 상수 추가 + 루프 교체:

```csharp
// 옛 LOPOverlapQuery가 안에 박아 두던 값 — 포트에서 나오면서 부르는 쪽으로 옮겨왔다.
private static readonly int CharacterLayerMask = UnityEngine.LayerMask.GetMask("Character");
```

```csharp
// 전
string[] hitIds = overlapQuery.OverlapSphere(casterTransform.Position, effect.Range);
foreach (string id in hitIds)
{
    if (id == ctx.Caster.Id) continue;
    ...
}

// 후
GameFramework.Physics.CollisionHit[] hits =
    collisionQuery.OverlapSphere(casterTransform.Position.ToUnity(), effect.Range, CharacterLayerMask);

//  한 엔티티가 콜라이더를 여럿 가질 수 있어 같은 대상이 여러 번 나온다 — 합치지 않으면 두 번 맞는다.
var alreadyHit = new System.Collections.Generic.HashSet<string>();
foreach (GameFramework.Physics.CollisionHit hit in hits)
{
    string id = hit.GetEntityId();
    if (id == null || id == ctx.Caster.Id || alreadyHit.Add(id) == false)
    {
        continue;   // 엔티티 아님 / 자기제외 / 이미 맞음
    }
    ...
}
```

> `casterTransform.Position`은 `System.Numerics.Vector3`이므로 `.ToUnity()`가 필요하다.

- [ ] **Step 4: 테스트 통과 확인**

```bash
"$U" command run_tests --mode EditMode --async_tests true --project-path "$SERVER"
"$U" command test_status --project-path "$SERVER"
```
기대: 기존 7개 + 새 1개 전부 통과. **기존 7개의 결과가 바뀌면 안 된다** — 이 단계의 계약은 "기능 변화 0"이다.

- [ ] **Step 5: 커밋**

```bash
git -C ../LeagueOfPhysical-Shared add Runtime/Scripts/Game/DamageEffectHandler.cs Tests/EditMode/DamageEffectHandlerTests.cs
git -C ../LeagueOfPhysical-Shared status --short
git -C ../LeagueOfPhysical-Shared commit -m "refactor(combat): 범위 검색을 공유 물리 포트로, 중복 제거는 호출부가"
```

---

## Task 5: `IOverlapQuery` 삭제

**Files:**
- Delete: `GameFramework/Runtime/Scripts/Physics/IOverlapQuery.cs` (+ `.meta`)
- Delete: `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPOverlapQuery.cs` (+ `.meta`)
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/GameplayInstaller.cs:51`

**Interfaces:**
- Consumes: Task 4가 마지막 사용처를 없앤 상태
- Produces: (없음 — 삭제 태스크)

**⚠️ 삭제는 역방향으로 검증한다.** "옛 이름이 남았나"가 아니라 **"없앤 계약을 아직 부르는 곳이 있나"** 를 본다.

- [ ] **Step 1: 부르는 곳이 정말 없는지 먼저 확인**

```bash
cd C:/Users/re5na/workspace/LOP
grep -rn "IOverlapQuery\|LOPOverlapQuery" \
  GameFramework LeagueOfPhysical-Shared \
  LeagueOfPhysical-Client/Assets LeagueOfPhysical-Server/Assets \
  --include=*.cs
```
기대: 지울 파일 3곳(`IOverlapQuery.cs`, `LOPOverlapQuery.cs`, `GameplayInstaller.cs:51`)만 나온다. 다른 게 나오면 **멈추고** 그것부터 옮긴다.

- [ ] **Step 2: 삭제 + 등록 제거**

```bash
git -C ../GameFramework rm Runtime/Scripts/Physics/IOverlapQuery.cs Runtime/Scripts/Physics/IOverlapQuery.cs.meta
git -C ../LeagueOfPhysical-Server rm Assets/Scripts/Game/LOPOverlapQuery.cs Assets/Scripts/Game/LOPOverlapQuery.cs.meta
```

`LeagueOfPhysical-Server/Assets/Scripts/Game/GameplayInstaller.cs`에서 이 줄을 지운다:

```csharp
builder.Register<GameFramework.Physics.IOverlapQuery, LOPOverlapQuery>(Lifetime.Singleton);
```

`ICollisionQuery` 등록(그 바로 윗줄)은 **남긴다** — `DamageEffectHandler`가 이제 그걸 받는다.

- [ ] **Step 3: 컴파일 + 테스트**

```bash
"$U" command recompile --project-path "$SERVER"; "$U" command recompile_status --project-path "$SERVER"
"$U" command recompile --project-path "$CLIENT"; "$U" command recompile_status --project-path "$CLIENT"
"$U" command run_tests --mode EditMode --async_tests true --project-path "$SERVER"
"$U" command test_status --project-path "$SERVER"
```
기대: 에러 0, 테스트 전부 초록.

> 패키지(`file:`) 파일을 지우면 stale `CS2001`이 남을 수 있다. 그때는 패키지를 다시 해석시킨다:
> `"$U" command package_resolve --project-path "$SERVER"`

- [ ] **Step 4: 커밋**

```bash
git -C ../GameFramework status --short && git -C ../GameFramework commit -m "refactor(physics): 엔티티를 아는 물리 포트를 없앤다"
git -C ../LeagueOfPhysical-Server add Assets/Scripts/Game/GameplayInstaller.cs
git -C ../LeagueOfPhysical-Server status --short
git -C ../LeagueOfPhysical-Server commit -m "refactor(physics): LOPOverlapQuery 제거"
```

- [ ] **Step 5: 3-0 완료 검증 — 근접 공격이 전과 같은가**

이 단계의 계약은 **기능 변화 0**이다. 판치기와 무관한 근접 공격 경로를 건드렸으므로 눈으로 확인한다.

1. 서버·클라 플레이 모드 진입, FlapWang(게임모드 1)으로 매치
2. 근접 공격을 상대에게 사용
3. 확인: 데미지 숫자가 뜨고, **한 번에 한 대만** 맞고, HP가 예상대로 준다
4. 양쪽 콘솔에 새 예외가 없다

```bash
"$U" command get_console_logs --severity error --limit 20 --project-path "$SERVER"
"$U" command get_console_logs --severity error --limit 20 --project-path "$CLIENT"
```

---

## Task 6: `PhysicsBody.AddImpulseAtPosition`

**Files:**
- Modify: `GameFramework/Runtime/Scripts/World/PhysicsBody.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/UnityPhysicsBody.cs`

**Interfaces:**
- Consumes: (없음)
- Produces: `GameFramework.World.PhysicsBody.AddImpulseAtPosition(System.Numerics.Vector3 impulse, System.Numerics.Vector3 worldPoint)` — Task 10이 부른다.

**왜:** 지금 `PhysicsBody`에는 `SetVelocity`밖에 없어 **회전을 줄 수단이 없다.** 판치기의 "뒤집기"가 여기에 달렸다 — 중심에서 어긋난 지점에 힘을 줘야 돈다.

- [ ] **Step 1: 추상 메서드 추가**

`GameFramework/Runtime/Scripts/World/PhysicsBody.cs`의 `ComputePushOut` 위에 추가:

```csharp
/// <summary>
/// 월드 좌표 한 점에 순간 충격(임펄스)을 준다. 몸 중심에서 벗어난 지점이면 회전이 함께 생긴다.
/// 물리 엔진이 굴리는 몸(다이나믹)에만 의미가 있다.
/// </summary>
public abstract void AddImpulseAtPosition(Vector3 impulse, Vector3 worldPoint);
```

> 이 파일은 `using System.Numerics;`라 `Vector3` = `System.Numerics.Vector3`다.

- [ ] **Step 2: Unity 어댑터 구현**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/UnityPhysicsBody.cs`에 추가:

```csharp
public override void AddImpulseAtPosition(System.Numerics.Vector3 impulse, System.Numerics.Vector3 worldPoint)
{
    //  키네마틱 몸은 PhysX가 힘을 무시한다. 조용히 넘어가면 "쳤는데 안 움직인다"의
    //  원인을 런타임에 추적해야 하므로 여기서 알린다.
    if (_rigidbody.isKinematic)
    {
        UnityEngine.Debug.LogWarning(
            $"[UnityPhysicsBody] 키네마틱 몸에 임펄스를 줬다 — 무시된다. {_rigidbody.name}");
        return;
    }

    //  타격은 한 순간의 충격이다. ForceMode.Force로 주면 한 물리 스텝 동안만 적용돼
    //  결과가 프레임 간격에 끌려간다.
    _rigidbody.AddForceAtPosition(impulse.ToUnity(), worldPoint.ToUnity(), UnityEngine.ForceMode.Impulse);
}
```

> 필드 이름(`_rigidbody`)과 `ToUnity()` 확장은 그 파일의 기존 코드에서 확인해 그대로 쓴다.

- [ ] **Step 3: 컴파일 확인**

```bash
"$U" command recompile --project-path "$SERVER"; "$U" command recompile_status --project-path "$SERVER"
"$U" command recompile --project-path "$CLIENT"; "$U" command recompile_status --project-path "$CLIENT"
```
기대: 에러 0. `PhysicsBody`를 상속한 **다른 구체가 있으면 여기서 깨진다** — 있으면 그 클래스에도 구현을 추가한다(테스트용 가짜라면 빈 본문으로 충분하다).

- [ ] **Step 4: 커밋**

```bash
git -C ../GameFramework add Runtime/Scripts/World/PhysicsBody.cs
git -C ../GameFramework commit -m "feat(physics): 몸에 임펄스를 줄 수 있게 한다"
git -C ../LeagueOfPhysical-Shared add Runtime/Scripts/Game/UnityPhysicsBody.cs
git -C ../LeagueOfPhysical-Shared commit -m "feat(physics): 임펄스를 리지드바디에 전달한다"
```

---

## Task 7: 힘 커널 — 순수 함수

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/PanchigiStrikeKernel.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/PanchigiStrikeKernelTests.cs`

**Interfaces:**
- Consumes: (없음 — 순수 함수)
- Produces:
  - `LOP.StrikeInput` — `readonly struct`, 생성자 `StrikeInput(Vector3 strikePoint, Vector3 dragDelta, float holdTime)`, 필드 `StrikePoint`/`DragDelta`/`HoldTime`
  - `LOP.StrikeTuning` — `readonly struct`, 생성자 `StrikeTuning(float forceMultiplier, float horizontalForceMultiplier, float falloffRate)`, 필드 `ForceMultiplier`/`HorizontalForceMultiplier`/`FalloffRate`
  - `LOP.PanchigiStrikeKernel.BuildSamples(Vector3 coinCenter, float radius, Vector3[] buffer)` — buffer를 채운다
  - `LOP.PanchigiStrikeKernel.ComputeImpulse(in StrikeInput, in StrikeTuning, Vector3[] liveSamples, int liveCount, int totalSamples) → Vector3`
  - 전부 `System.Numerics.Vector3`. Task 10이 이 둘을 부른다.

**왜:** 원본의 접촉점 16,000개 계산은 부분 힘을 전부 같은 지점에 걸어 **수학적으로 이미 임펄스 하나**였다. 격자가 만들던 건 회전이 아니라 스칼라 배수 하나였고, 그 배수가 담은 정보는 "동전이 판에 실제로 닿은 면적을 타격점 거리로 가중한 값"이다. 그걸 고정 K개 샘플로 다시 만든다.

- [ ] **Step 1: 실패하는 테스트 작성**

`LeagueOfPhysical-Shared/Tests/EditMode/PanchigiStrikeKernelTests.cs`:

```csharp
using System.Numerics;
using NUnit.Framework;

namespace LOP.Tests
{
    public class PanchigiStrikeKernelTests
    {
        private static StrikeTuning Tuning(float falloffRate = 1f)
            => new StrikeTuning(forceMultiplier: 10f, horizontalForceMultiplier: 4f, falloffRate: falloffRate);

        private static StrikeInput Strike(Vector3 point, float dragX = 1f, float dragZ = 0f, float hold = 0.5f)
            => new StrikeInput(point, new Vector3(dragX, 0f, dragZ), hold);

        [Test]
        public void 살아남은_샘플이_없으면_임펄스는_0()
        {
            var impulse = PanchigiStrikeKernel.ComputeImpulse(
                Strike(Vector3.Zero), Tuning(), new Vector3[4], liveCount: 0, totalSamples: 4);

            Assert.AreEqual(Vector3.Zero, impulse);
        }

        [Test]
        public void 전부_살아남고_타격점이_샘플과_겹치면_감쇠가_없다()
        {
            var samples = new[] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

            var impulse = PanchigiStrikeKernel.ComputeImpulse(
                Strike(Vector3.Zero, dragX: 1f, hold: 0.5f), Tuning(), samples, 4, 4);

            //  덮임 = 1이므로 힘벡터 그대로: (1*4, 0.5*10, 0*4)
            Assert.AreEqual(4f, impulse.X, 1e-4f);
            Assert.AreEqual(5f, impulse.Y, 1e-4f);
            Assert.AreEqual(0f, impulse.Z, 1e-4f);
        }

        [Test]
        public void 타격점이_멀수록_약해진다()
        {
            var samples = new[] { Vector3.Zero };

            float near = PanchigiStrikeKernel.ComputeImpulse(
                Strike(new Vector3(0.1f, 0f, 0f)), Tuning(), samples, 1, 1).Length();
            float far = PanchigiStrikeKernel.ComputeImpulse(
                Strike(new Vector3(3f, 0f, 0f)), Tuning(), samples, 1, 1).Length();

            Assert.Less(far, near);
        }

        [Test]
        public void 높이_차이는_감쇠에_영향을_주지_않는다()
        {
            //  falloff는 판 위 평면 거리로만 잰다 — 동전이 떠 있어도 세기가 안 변해야 한다.
            var flat = new[] { Vector3.Zero };
            var raised = new[] { new Vector3(0f, 5f, 0f) };

            float a = PanchigiStrikeKernel.ComputeImpulse(Strike(Vector3.Zero), Tuning(), flat, 1, 1).Length();
            float b = PanchigiStrikeKernel.ComputeImpulse(Strike(Vector3.Zero), Tuning(), raised, 1, 1).Length();

            Assert.AreEqual(a, b, 1e-4f);
        }

        [Test]
        public void 샘플_개수를_늘려도_세기가_변하지_않는다()
        {
            //  이게 원본이 못 하던 것 — gridDivisions가 세기까지 바꿔 튜닝이 불가능했다.
            var four = new[] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };
            var eight = new Vector3[8];   // 전부 Vector3.Zero

            float a = PanchigiStrikeKernel.ComputeImpulse(Strike(Vector3.Zero), Tuning(), four, 4, 4).Length();
            float b = PanchigiStrikeKernel.ComputeImpulse(Strike(Vector3.Zero), Tuning(), eight, 8, 8).Length();

            Assert.AreEqual(a, b, 1e-4f);
        }

        [Test]
        public void 절반만_살아남으면_세기도_절반이다()
        {
            var samples = new[] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

            float full = PanchigiStrikeKernel.ComputeImpulse(Strike(Vector3.Zero), Tuning(), samples, 4, 4).Length();
            float half = PanchigiStrikeKernel.ComputeImpulse(Strike(Vector3.Zero), Tuning(), samples, 2, 4).Length();

            Assert.AreEqual(full * 0.5f, half, 1e-4f);
        }

        [Test]
        public void 누른_시간이_0이면_수직_성분이_없다()
        {
            var samples = new[] { Vector3.Zero };

            var impulse = PanchigiStrikeKernel.ComputeImpulse(
                Strike(Vector3.Zero, dragX: 1f, hold: 0f), Tuning(), samples, 1, 1);

            Assert.AreEqual(0f, impulse.Y, 1e-4f);
            Assert.Greater(impulse.X, 0f);
        }

        [Test]
        public void 같은_입력이면_같은_결과다()
        {
            var samples = new[] { new Vector3(0.1f, 0f, 0.2f) };

            var a = PanchigiStrikeKernel.ComputeImpulse(Strike(Vector3.Zero), Tuning(), samples, 1, 1);
            var b = PanchigiStrikeKernel.ComputeImpulse(Strike(Vector3.Zero), Tuning(), samples, 1, 1);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void 샘플_개수가_0_이하면_예외()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                PanchigiStrikeKernel.ComputeImpulse(Strike(Vector3.Zero), Tuning(), new Vector3[1], 1, 0));
        }

        [Test]
        public void 샘플은_전부_발자국_원_안에_깔린다()
        {
            var center = new Vector3(2f, 0.5f, -3f);
            const float radius = 0.15f;
            var buffer = new Vector3[13];

            PanchigiStrikeKernel.BuildSamples(center, radius, buffer);

            foreach (var p in buffer)
            {
                float dx = p.X - center.X;
                float dz = p.Z - center.Z;
                Assert.LessOrEqual(System.MathF.Sqrt(dx * dx + dz * dz), radius + 1e-4f);
                Assert.AreEqual(center.Y, p.Y, 1e-4f, "샘플은 동전과 같은 높이에 깔린다");
            }
        }

        [Test]
        public void 샘플_배치는_결정론적이다()
        {
            var a = new Vector3[13];
            var b = new Vector3[13];

            PanchigiStrikeKernel.BuildSamples(Vector3.Zero, 0.15f, a);
            PanchigiStrikeKernel.BuildSamples(Vector3.Zero, 0.15f, b);

            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void 샘플이_하나여도_동작한다()
        {
            var buffer = new Vector3[1];

            PanchigiStrikeKernel.BuildSamples(Vector3.Zero, 0.15f, buffer);

            Assert.LessOrEqual(buffer[0].Length(), 0.15f + 1e-4f);
        }

        [Test]
        public void 샘플_버퍼가_비어_있으면_예외()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                PanchigiStrikeKernel.BuildSamples(Vector3.Zero, 0.15f, new Vector3[0]));
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
"$U" command run_tests --mode EditMode --async_tests true --project-path "$SERVER"
"$U" command test_status --project-path "$SERVER"
```
기대: 컴파일 실패 — `PanchigiStrikeKernel`이 없다.

- [ ] **Step 3: 커널 구현**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/PanchigiStrikeKernel.cs`:

```csharp
using System;
using System.Numerics;

namespace LOP
{
    /// <summary>한 번의 타격이 무엇이었나 — 판 위 어디를, 어느 방향으로, 얼마나 오래 눌러서.</summary>
    public readonly struct StrikeInput
    {
        public readonly Vector3 StrikePoint;
        public readonly Vector3 DragDelta;
        public readonly float HoldTime;

        public StrikeInput(Vector3 strikePoint, Vector3 dragDelta, float holdTime)
        {
            StrikePoint = strikePoint;
            DragDelta = dragDelta;
            HoldTime = holdTime;
        }
    }

    /// <summary>타격 세기를 정하는 값들. 마스터데이터에서 온다.</summary>
    public readonly struct StrikeTuning
    {
        public readonly float ForceMultiplier;
        public readonly float HorizontalForceMultiplier;
        public readonly float FalloffRate;

        public StrikeTuning(float forceMultiplier, float horizontalForceMultiplier, float falloffRate)
        {
            ForceMultiplier = forceMultiplier;
            HorizontalForceMultiplier = horizontalForceMultiplier;
            FalloffRate = falloffRate;
        }
    }

    /// <summary>
    /// 판치기 타격의 힘 계산. 순수 함수 — 레이캐스트는 부르는 쪽이 하고, 여기엔 살아남은 샘플만 온다.
    ///
    /// 원본(ForceElement)은 동전 밑 접촉점 수천 개마다 힘을 나눠 줬지만, 그 힘을 전부 *같은 지점*에
    /// 걸었기 때문에 합이 임펄스 하나와 수학적으로 같았다. 즉 격자가 만든 것은 회전이 아니라
    /// "동전이 판에 닿은 정도"라는 배수 하나였다. 여기서는 그 배수를 고정 개수 샘플로 직접 잰다.
    /// </summary>
    public static class PanchigiStrikeKernel
    {
        //  황금각(라디안). 해바라기 씨앗 배치가 원판을 고르게 덮는 데 쓰는 각도다.
        private const float GoldenAngle = 2.39996323f;

        /// <summary>
        /// 동전 하나에 줄 임펄스. 살아남은 샘플이 없으면 <see cref="Vector3.Zero"/>.
        /// </summary>
        /// <param name="liveSamples">판 밖·포개짐을 걸러 내고 남은 샘플들의 월드 좌표.</param>
        /// <param name="liveCount"><paramref name="liveSamples"/> 앞쪽 유효 개수.</param>
        /// <param name="totalSamples">걸러 내기 전 전체 샘플 수(K). 세기를 이 값으로 정규화한다.</param>
        public static Vector3 ComputeImpulse(in StrikeInput input, in StrikeTuning tuning,
            Vector3[] liveSamples, int liveCount, int totalSamples)
        {
            if (totalSamples <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalSamples),
                    "샘플 개수는 1 이상이어야 한다 — 마스터데이터를 확인할 것.");
            }
            if (liveCount <= 0)
            {
                return Vector3.Zero;
            }

            float sum = 0f;
            for (int i = 0; i < liveCount; i++)
            {
                //  감쇠는 판 위 평면 거리로만 잰다 — 동전이 떠 있어도 세기가 흔들리면 안 된다.
                float dx = liveSamples[i].X - input.StrikePoint.X;
                float dz = liveSamples[i].Z - input.StrikePoint.Z;
                sum += 1f / (1f + tuning.FalloffRate * (dx * dx + dz * dz));
            }

            //  K로 나누기 때문에 샘플을 늘려도 세기가 변하지 않는다 — 정밀도 노브와 세기 노브가 갈린다.
            float coverage = sum / totalSamples;

            return new Vector3(
                input.DragDelta.X * tuning.HorizontalForceMultiplier,
                input.HoldTime * tuning.ForceMultiplier,
                input.DragDelta.Z * tuning.HorizontalForceMultiplier) * coverage;
        }

        /// <summary>
        /// 동전 발자국(원) 위에 샘플을 고르게 깐다. 해바라기 배치라 개수가 몇이든 성립하고,
        /// 난수를 안 써서 같은 동전이면 항상 같은 자리가 나온다.
        /// </summary>
        public static void BuildSamples(Vector3 coinCenter, float radius, Vector3[] buffer)
        {
            if (buffer == null || buffer.Length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(buffer),
                    "샘플 버퍼는 1개 이상이어야 한다 — 마스터데이터를 확인할 것.");
            }

            int count = buffer.Length;
            for (int i = 0; i < count; i++)
            {
                //  sqrt를 씌워야 바깥쪽이 성기지 않다 — 원판은 반지름이 아니라 면적에 비례해 넓어진다.
                float r = radius * MathF.Sqrt((i + 0.5f) / count);
                float theta = i * GoldenAngle;
                buffer[i] = new Vector3(
                    coinCenter.X + r * MathF.Cos(theta),
                    coinCenter.Y,
                    coinCenter.Z + r * MathF.Sin(theta));
            }
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

```bash
"$U" command run_tests --mode EditMode --async_tests true --project-path "$SERVER"
"$U" command test_status --project-path "$SERVER"
```
기대: 새 테스트 13개 전부 통과.

- [ ] **Step 5: 커밋**

```bash
git -C ../LeagueOfPhysical-Shared add Runtime/Scripts/Game/PanchigiStrikeKernel.cs Runtime/Scripts/Game/PanchigiStrikeKernel.cs.meta Tests/EditMode/PanchigiStrikeKernelTests.cs Tests/EditMode/PanchigiStrikeKernelTests.cs.meta
git -C ../LeagueOfPhysical-Shared status --short
git -C ../LeagueOfPhysical-Shared commit -m "feat(panchigi): 타격 힘 커널 — 덮임으로 세기를 정한다"
```

---

## Task 8: 마스터데이터 — 커널 노브 5개

**Files:**
- Modify: `infrastructure/table/Datas/#PanchigiConfig.xlsx`
- Generated: `LeagueOfPhysical-MasterData-{Client,Server}/Runtime.Generated/...`, `lop-backend/apps/matchmaking-server/...`

**Interfaces:**
- Consumes: (없음)
- Produces: `masterData.Tables.TbPanchigiConfig.Get(1)`이 `ForceMultiplier`(float) / `HorizontalForceMultiplier`(float) / `FalloffRate`(float) / `CoverageSamples`(int) / `HoldTimeMax`(float)를 갖는다. Task 10·11이 읽는다.

- [ ] **Step 1: 엑셀에 컬럼 5개 추가**

`infrastructure/table/Datas/#PanchigiConfig.xlsx`를 열어 기존 컬럼 뒤에 추가한다(Luban Excel-embedded 형식은 같은 파일의 기존 컬럼 정의 행을 그대로 따라 한다).

| 컬럼 | 타입 | id=1 값 | 뜻 |
|---|---|---|---|
| `force_multiplier` | float | `40` | 누른 시간 → 수직 힘 배수 |
| `horizontal_force_multiplier` | float | `20` | 끈 거리 → 수평 힘 배수 |
| `falloff_rate` | float | `4` | 거리 감쇠율. 클수록 급격히 약해진다 |
| `coverage_samples` | int | `13` | 동전당 샘플 개수 K |
| `hold_time_max` | float | `1.0` | 누른 시간 상한(초) |

> 이 값들은 **시작점이지 정답이 아니다.** Task 13에서 눈으로 보며 조정한다.

- [ ] **Step 2: 생성**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
./gen.sh
```

`gen.sh`는 **세 곳**에 쓴다: MasterData-Client / MasterData-Server 패키지 + `lop-backend/apps/matchmaking-server`. 컬럼만 늘리는 것이라 `TableFiles` 목록은 손대지 않는다(새 테이블이 아니다).

- [ ] **Step 3: 생성물 확인**

```bash
grep -n "ForceMultiplier\|CoverageSamples\|HoldTimeMax" \
  ../../LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/PanchigiConfig.cs
```
기대: 다섯 프로퍼티가 전부 보인다. 서버 패키지도 같은지 확인한다.

- [ ] **Step 4: 커밋 (레포 4개)**

`.meta`는 Unity가 만들 때까지 기다린 뒤 add한다.

```bash
git -C ../../infrastructure add table/Datas/'#PanchigiConfig.xlsx'
git -C ../../infrastructure status --short
git -C ../../infrastructure commit -m "feat(masterdata): 판치기 타격 커널 노브"

# MasterData 패키지 둘 + lop-backend는 생성물만 — 각 레포에서 상태 확인 후 커밋
git -C ../../LeagueOfPhysical-MasterData-Client status --short
git -C ../../LeagueOfPhysical-MasterData-Server status --short
git -C ../../lop-backend status --short
```

각각 커밋 메시지: `feat(masterdata): 판치기 타격 커널 노브 반영`

---

## Task 9: 와이어 — `PanchigiStrikeToS`

**Files:**
- Create: `LeagueOfPhysical-Shared/Protos/PanchigiStrikeToS.proto`
- Generated: `LeagueOfPhysical-Shared/Runtime.Generated/Scripts/Protobuf/PanchigiStrikeToS.cs`, `MessageIds.cs`, `MessageInitializer.cs`

**Interfaces:**
- Consumes: (없음)
- Produces: `LOP.PanchigiStrikeToS` — 프로퍼티 `StrikePoint`(`ProtoVector3`) / `DragDelta`(`ProtoVector3`) / `HoldTime`(float). `MessageIds.PanchigiStrikeToS`. Task 10(수신)·11(송신)이 쓴다.

- [ ] **Step 1: proto 작성**

`LeagueOfPhysical-Shared/Protos/PanchigiStrikeToS.proto`:

```proto
syntax = "proto3";

import "ProtoVector3.proto";

// @auto_generate
message PanchigiStrikeToS
{
	// 신원은 연결에서 도출한다 — 클라가 적어 보내지 않는다.
	ProtoVector3 strike_point = 1;   // 판 위 월드 좌표
	ProtoVector3 drag_delta   = 2;   // 판 평면 변위 (y = 0)
	float        hold_time    = 3;   // 초. 클라가 이미 상한을 적용해 보낸다
}
```

> 첫 줄 `// @auto_generate`와 탭 들여쓰기는 이 폴더의 기존 proto 규약이다 — `StatAllocationToS.proto`를 그대로 따른다.

- [ ] **Step 2: 생성 전 MessageIds 스냅샷**

```bash
cp ../LeagueOfPhysical-Shared/Runtime.Generated/Scripts/MessageIds.cs /tmp/MessageIds.before.cs
```

- [ ] **Step 3: proto 생성 스크립트 실행**

`LeagueOfPhysical-Shared/Tools/Protobuf/` 아래의 생성 스크립트를 찾아 실행한다.

> ⚠️ **부모 스크립트가 `MessageIds.cs`를 지우고 다시 만들어 기존 번호가 밀린 전례가 있다.** 여러 스크립트가 있으면 **서브 스크립트를 개별 실행**하고, 반드시 다음 단계로 검증한다.

- [ ] **Step 4: MessageId가 밀리지 않았는지 확인 — 필수**

```bash
diff /tmp/MessageIds.before.cs ../LeagueOfPhysical-Shared/Runtime.Generated/Scripts/MessageIds.cs
```
기대: **추가된 줄 하나뿐**(`PanchigiStrikeToS = 15;` 같은 새 상수). 기존 상수의 숫자가 하나라도 바뀌었으면 **와이어가 깨진 것이다** — 멈추고 되돌린 뒤 다시 한다.

- [ ] **Step 5: 컴파일 확인**

```bash
"$U" command recompile --project-path "$SERVER"; "$U" command recompile_status --project-path "$SERVER"
"$U" command recompile --project-path "$CLIENT"; "$U" command recompile_status --project-path "$CLIENT"
```

- [ ] **Step 6: 커밋**

```bash
git -C ../LeagueOfPhysical-Shared add Protos/PanchigiStrikeToS.proto Protos/PanchigiStrikeToS.proto.meta Runtime.Generated/Scripts/
git -C ../LeagueOfPhysical-Shared status --short
git -C ../LeagueOfPhysical-Shared commit -m "feat(wire): 판치기 타격 메시지"
```

---

## Task 10: 서버 — 타격 수신·검증·임펄스

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/MessageHandler/PanchigiStrikeMessageHandler.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiLifetimeScope.cs`

**Interfaces:**
- Consumes: Task 2(`ICollisionQuery.Raycast`), Task 3(`GetEntityId`), Task 6(`AddImpulseAtPosition`), Task 7(`PanchigiStrikeKernel`), Task 8(테이블), Task 9(`PanchigiStrikeToS`)
- Produces: (없음 — 종단)

- [ ] **Step 1: 핸들러 작성**

`LeagueOfPhysical-Server/Assets/Scripts/Game/MessageHandler/PanchigiStrikeMessageHandler.cs`:

```csharp
using GameFramework;
using MessagePipe;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 판치기 타격(서버). 클라가 판을 끌어 놓으면 한 통 온다 — 검증한 뒤 동전마다 "판에 닿은 정도"를
    /// 재서 임펄스를 준다. 굴리는 것은 우리 시뮬이 아니라 유니티 물리이고, 결과는
    /// PhysicsSimulationSystem이 World로 되읽어 스냅샷에 실린다.
    /// </summary>
    public class PanchigiStrikeMessageHandler : MessageHandlerBase
    {
        //  판·동전만 본다. 판 밖 지형이나 트리거에 걸리면 판정이 엉킨다.
        private static readonly int StrikeLayerMask = LayerMask.GetMask("Default", "Character");

        //  샘플은 동전 바로 위에서 아래로 쏜다 — 발자국 위에 무엇이 얹혀 있는지 보려는 것이라
        //  동전 두께보다 넉넉히 위에서 시작해 판까지 닿을 만큼만 간다.
        private const float SampleRayHeight = 1f;
        private const float SampleRayDistance = 2f;

        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.Physics.ICollisionQuery collisionQuery;
        private readonly LOP.MasterData.LOPMasterData masterData;
        private readonly IRoomDataStore roomDataStore;
        private readonly ISubscriber<ClientMessage<PanchigiStrikeToS>> strikeSubscriber;

        private Bounds boardBounds;
        private bool boardFound;

        public PanchigiStrikeMessageHandler(
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.Physics.ICollisionQuery collisionQuery,
            LOP.MasterData.LOPMasterData masterData,
            IRoomDataStore roomDataStore,
            ISubscriber<ClientMessage<PanchigiStrikeToS>> strikeSubscriber)
        {
            this.entityRegistry = entityRegistry;
            this.collisionQuery = collisionQuery;
            this.masterData = masterData;
            this.roomDataStore = roomDataStore;
            this.strikeSubscriber = strikeSubscriber;
        }

        protected override void Subscribe() => Track(strikeSubscriber.Subscribe(OnStrike));

        private void OnStrike(ClientMessage<PanchigiStrikeToS> received)
        {
            if (TryGetBoardBounds(out Bounds board) == false)
            {
                Debug.LogWarning("[Panchigi] 판을 찾지 못했다 — 타격을 버린다.");
                return;
            }

            var config = masterData.Tables.TbPanchigiConfig.GetOrDefault(1);
            if (config == null)
            {
                Debug.LogWarning("[Panchigi] TbPanchigiConfig(1)이 없다 — 타격을 버린다.");
                return;
            }

            string userId = received.Session.userId;
            if (IsParticipant(userId) == false)
            {
                Debug.LogWarning($"[Panchigi] 참가자가 아닌 타격 — {userId}");
                return;
            }

            PanchigiStrikeToS message = received.Message;
            Vector3 strikePoint = message.StrikePoint.ToUnityVector3();
            Vector3 dragDelta = message.DragDelta.ToUnityVector3();

            //  클라가 이미 상한을 걸어 보내지만 믿지 않는다. 클램프가 아니라 거절이다 —
            //  클램프하면 조작된 값이 조용히 게임에 들어오고 로그도 안 남는다.
            if (ContainsXZ(board, strikePoint) == false)
            {
                Debug.LogWarning($"[Panchigi] 판 밖 타격점 {strikePoint} — {userId}");
                return;
            }
            if (message.HoldTime < 0f || message.HoldTime > config.HoldTimeMax)
            {
                Debug.LogWarning($"[Panchigi] 누른 시간 범위 밖 {message.HoldTime} — {userId}");
                return;
            }
            if (dragDelta.magnitude > config.StrikePowerMax)
            {
                Debug.LogWarning($"[Panchigi] 세기 범위 밖 {dragDelta.magnitude} — {userId}");
                return;
            }

            ApplyStrike(strikePoint, dragDelta, message.HoldTime, board, config);
        }

        private void ApplyStrike(Vector3 strikePoint, Vector3 dragDelta, float holdTime,
            Bounds board, LOP.MasterData.PanchigiConfig config)
        {
            var input = new StrikeInput(strikePoint.ToNumerics(), dragDelta.ToNumerics(), holdTime);
            var tuning = new StrikeTuning(
                config.ForceMultiplier, config.HorizontalForceMultiplier, config.FalloffRate);

            int sampleCount = config.CoverageSamples;
            var samples = new System.Numerics.Vector3[sampleCount];
            var live = new System.Numerics.Vector3[sampleCount];

            foreach (GameFramework.World.Entity entity in entityRegistry.All)
            {
                var disc = entity.Get<GameFramework.World.DiscShape>();
                var body = entity.Get<GameFramework.World.PhysicsBody>();
                var transform = entity.Get<GameFramework.World.Transform>();
                if (disc == null || body == null || transform == null)
                {
                    continue;   // 동전이 아니다
                }

                PanchigiStrikeKernel.BuildSamples(transform.Position, disc.Radius, samples);

                int liveCount = 0;
                for (int i = 0; i < sampleCount; i++)
                {
                    Vector3 sample = samples[i].ToUnity();
                    if (ContainsXZ(board, sample) == false)
                    {
                        continue;   // 판 끄트머리 밖으로 삐져나온 부분
                    }

                    //  이 자리 위에 실제로 놓인 것이 이 동전인지 본다. 다른 동전이 먼저 맞으면
                    //  이 동전은 그 위에 얹혀 있다는 뜻이라 판에서 힘을 받지 못한다.
                    Vector3 origin = new Vector3(sample.x, sample.y + SampleRayHeight, sample.z);
                    GameFramework.Physics.CollisionHit hit =
                        collisionQuery.Raycast(origin, Vector3.down, SampleRayDistance, StrikeLayerMask);
                    if (hit.GetEntityId() != entity.Id)
                    {
                        continue;
                    }

                    live[liveCount++] = samples[i];
                }

                System.Numerics.Vector3 impulse =
                    PanchigiStrikeKernel.ComputeImpulse(input, tuning, live, liveCount, sampleCount);
                if (impulse == System.Numerics.Vector3.Zero)
                {
                    continue;
                }

                body.AddImpulseAtPosition(impulse, strikePoint.ToNumerics());
            }
        }

        private bool IsParticipant(string userId)
        {
            foreach (string participant in roomDataStore.match.playerList)
            {
                if (participant == userId)
                {
                    return true;
                }
            }
            return false;
        }

        private bool TryGetBoardBounds(out Bounds bounds)
        {
            if (boardFound)
            {
                bounds = boardBounds;
                return true;
            }

            GameObject board = GameObject.Find("Board");
            Collider collider = board != null ? board.GetComponent<Collider>() : null;
            if (collider == null)
            {
                bounds = default;
                return false;
            }

            boardBounds = collider.bounds;
            boardFound = true;
            bounds = boardBounds;
            return true;
        }

        //  판은 평면이라 높이는 보지 않는다 — 위아래로 얼마나 떨어져 있든 "판 위"다.
        private static bool ContainsXZ(Bounds bounds, Vector3 point)
            => point.x >= bounds.min.x && point.x <= bounds.max.x
            && point.z >= bounds.min.z && point.z <= bounds.max.z;
    }
}
```

> `ToUnityVector3()` / `ToNumerics()` / `ToUnity()` 확장의 정확한 이름은 이 레포의 기존 코드(예: `CoinCreationDataCreator`, `EntityBinder`)에서 확인해 그대로 쓴다. `roomDataStore.match.playerList`도 `PanchigiRuleSystem`이 쓰는 방식 그대로다.

- [ ] **Step 2: 스코프에 등록**

`LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiLifetimeScope.cs`:

```csharp
builder.Register<PanchigiStrikeMessageHandler>(Lifetime.Singleton)
    .AsImplementedInterfaces().AsSelf();
```

> 다른 `MessageHandlerBase` 구현이 어떻게 등록돼 있는지(`GameEntityMessageHandler` 등) 확인해 **그 방식 그대로** 쓴다. 구독 배관이 도는지가 핵심이다.

- [ ] **Step 3: 컴파일 확인**

```bash
"$U" command recompile --project-path "$SERVER"; "$U" command recompile_status --project-path "$SERVER"
```
기대: 에러 0.

- [ ] **Step 4: 커밋**

```bash
git -C ../LeagueOfPhysical-Server add Assets/Scripts/Game/MessageHandler/PanchigiStrikeMessageHandler.cs Assets/Scripts/Game/MessageHandler/PanchigiStrikeMessageHandler.cs.meta Assets/Scripts/Game/PanchigiLifetimeScope.cs
git -C ../LeagueOfPhysical-Server status --short
git -C ../LeagueOfPhysical-Server commit -m "feat(panchigi): 타격을 받아 동전에 임펄스를 준다"
```

---

## Task 11: 클라 — 조준과 송신

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiStrikeInput.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiLifetimeScope.cs`

**Interfaces:**
- Consumes: Task 8(테이블), Task 9(`PanchigiStrikeToS`)
- Produces: `LOP.PanchigiStrikeInput` (MonoBehaviour) — 씬에 두고 `PanchigiLifetimeScope`가 `[SerializeField]`로 참조

- [ ] **Step 1: 입력 컴포넌트 작성**

`LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiStrikeInput.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 판치기 조준·타격(클라). 판 위 한 점을 누르고, 끌고, 뗀다 — 끈 방향이 수평 힘이고
    /// 누른 시간이 수직 힘이다. 예측은 하지 않는다(판치기는 서버가 굴린 물리를 보기만 한다).
    /// 조준선은 서버 왕복 없이 로컬로만 그린다.
    /// </summary>
    public class PanchigiStrikeInput : MonoBehaviour
    {
        [SerializeField] private Camera aimCamera;
        [SerializeField] private LineRenderer aimLine;

        [Inject] private IPlayerContext playerContext;
        [Inject] private LOP.MasterData.LOPMasterData masterData;

        //  판만 맞힌다 — 동전을 눌러도 "판의 그 자리를 쳤다"로 읽어야 조작이 자연스럽다.
        private static readonly int BoardLayerMask = LayerMask.GetMask("Default");

        private bool aiming;
        private float pressTime;
        private Vector3 pressPoint;
        private Vector3 currentPoint;

        private void Awake()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }
            SetAimLineVisible(false);
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                return;   // 마우스도 터치도 없는 환경
            }

            //  누름과 뗌이 한 프레임에 몰릴 수 있다(빠른 탭·낮은 프레임율). else로 묶으면
            //  그 프레임의 뗌이 평가조차 안 돼 타격이 조용히 사라지고 조준 상태가 남는다.
            if (pointer.press.wasPressedThisFrame)
            {
                BeginAim(pointer.position.ReadValue());
            }

            //  뗌을 눌림보다 먼저 본다 — 떼는 프레임에도 isPressed가 아직 참일 수 있어,
            //  순서가 반대면 그 프레임의 뗌을 또 놓친다.
            if (aiming && pointer.press.wasReleasedThisFrame)
            {
                EndAim(pointer.position.ReadValue());
            }
            else if (aiming && pointer.press.isPressed)
            {
                UpdateAim(pointer.position.ReadValue());
            }
        }

        private void BeginAim(Vector2 screenPosition)
        {
            if (TryBoardPoint(screenPosition, out Vector3 point) == false)
            {
                return;   // 판 밖을 눌렀다 — 조준을 시작하지 않는다
            }

            aiming = true;
            pressTime = Time.time;
            pressPoint = point;
            currentPoint = point;
            SetAimLineVisible(true);
            DrawAimLine();
        }

        private void UpdateAim(Vector2 screenPosition)
        {
            if (TryBoardPoint(screenPosition, out Vector3 point))
            {
                currentPoint = point;
            }
            DrawAimLine();
        }

        private void EndAim(Vector2 screenPosition)
        {
            aiming = false;
            SetAimLineVisible(false);

            if (TryBoardPoint(screenPosition, out Vector3 point))
            {
                currentPoint = point;
            }

            var config = masterData.Tables.TbPanchigiConfig.GetOrDefault(1);
            if (config == null || playerContext.session == null)
            {
                return;
            }

            //  누른 시간에 상한이 없으면 오래 누를수록 힘이 무한히 커진다(원본의 문제).
            float holdTime = Mathf.Min(Time.time - pressTime, config.HoldTimeMax);

            Vector3 drag = currentPoint - pressPoint;
            drag.y = 0f;
            //  세기 상한도 여기서 자른다 — 서버는 넘으면 클램프가 아니라 거절한다.
            drag = Vector3.ClampMagnitude(drag, config.StrikePowerMax);

            playerContext.session.Send(new PanchigiStrikeToS
            {
                StrikePoint = currentPoint.ToProtoVector3(),
                DragDelta = drag.ToProtoVector3(),
                HoldTime = holdTime,
            });
        }

        private bool TryBoardPoint(Vector2 screenPosition, out Vector3 point)
        {
            Ray ray = aimCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, BoardLayerMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }
            point = default;
            return false;
        }

        private void DrawAimLine()
        {
            if (aimLine == null)
            {
                return;
            }
            aimLine.positionCount = 2;
            aimLine.SetPosition(0, pressPoint);
            aimLine.SetPosition(1, currentPoint);
        }

        private void SetAimLineVisible(bool visible)
        {
            if (aimLine != null)
            {
                aimLine.enabled = visible;
            }
        }
    }
}
```

> `ToProtoVector3()`의 정확한 이름은 클라의 기존 송신 코드(예: 입력 전송)에서 확인해 그대로 쓴다. 없으면 `new ProtoVector3 { X = v.x, Y = v.y, Z = v.z }`로 직접 만든다.

- [ ] **Step 2: 스코프에 등록**

`LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiLifetimeScope.cs`의 `ConfigureGame`에:

```csharp
[SerializeField] private PanchigiStrikeInput strikeInput;   // 클래스 필드로

// ConfigureGame 안, cameraController 등록 옆
builder.RegisterComponent(strikeInput);
```

- [ ] **Step 3: 컴파일 확인**

```bash
"$U" command recompile --project-path "$CLIENT"; "$U" command recompile_status --project-path "$CLIENT"
```

- [ ] **Step 4: 커밋** (씬 배선은 Task 12에서 함께)

```bash
git add Assets/Scripts/Game/PanchigiStrikeInput.cs Assets/Scripts/Game/PanchigiStrikeInput.cs.meta Assets/Scripts/Game/PanchigiLifetimeScope.cs
git status --short
git commit -m "feat(panchigi): 판을 끌어 치는 조준 입력"
```

---

## Task 12: 보이게 만들기 — 판 두께·카메라·동전 몸

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scenes/Panchigi.unity`
- Modify: `LeagueOfPhysical-Server/Assets/Scenes/Panchigi.unity`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiRuleSystem.cs`

**Interfaces:**
- Consumes: Task 11의 `PanchigiStrikeInput`
- Produces: (없음 — 씬·연출)

**왜:** 지금 화면은 베이지 한 덩어리다. 판은 두께 0 Plane이라 위에서 아래로 쏘는 샘플 레이의 시작점이 면의 어느 쪽인지 부동소수점에 달리고, 카메라는 판을 거의 옆에서 봐 조준이 불가능하다.

- [ ] **Step 1: 판을 얇은 Box로 (양쪽 씬 동일 값)**

두 씬 모두 `Board` GameObject를:
- `MeshFilter` 메시: Plane → **Cube**
- `MeshCollider` 제거 → **`BoxCollider`** 추가
- Transform: position `(0, -0.05, 0)`, scale `(10, 0.1, 10)`

> 위치·크기를 **클·서가 정확히 같게** 맞춘다. 어긋나면 서버가 "판 밖"이라고 거절하는데 클라 화면에선 판 안이라 원인을 찾기 어렵다.

- [ ] **Step 2: 카메라를 내려다보게 (클라 씬)**

`Main Camera`: position `(0, 9, -9)`, rotation `(45, 0, 0)`.

- [ ] **Step 3: 조준선 준비 (클라 씬)**

`Board` 옆에 빈 GameObject `AimLine`을 만들고 `LineRenderer`를 붙인다:
- `widthMultiplier` = `0.06`
- `useWorldSpace` = true
- 머티리얼: 기본값 그대로(색이 보이면 충분하다)
- 컴포넌트 자체는 `enabled = false`로 시작(스크립트가 켠다)

`PanchigiLifetimeScope`가 붙은 GameObject에 `PanchigiStrikeInput`을 추가하고, 인스펙터에서 `aimCamera`=Main Camera, `aimLine`=AimLine을 연결한 뒤 `PanchigiLifetimeScope`의 `strikeInput` 슬롯에 그 컴포넌트를 물린다.

- [ ] **Step 4: 동전이 보이게 — 임시 프리팹**

`LOPEntityView.UpdateVisual`은 `visualId`를 **Addressables 키**로 그대로 넘긴다
(`Addressables.LoadAssetAsync<GameObject>(visualId)`). 그리고 이 프로젝트의 주소 규약은
**에셋 전체 경로**다(`Assets/Art/Characters/Archer/Archer.prefab`). 임시 에셋은
`Assets/Art_Placeholder/` 아래에 두는 전례가 이미 있다(VFX가 그렇게 쓴다) — 진짜 아트 서브모듈
(`Assets/Art`)을 건드리지 않기 위해서다. 그 관례를 따른다.

프리팹 `LeagueOfPhysical-Client/Assets/Art_Placeholder/Panchigi/Coin.prefab`:
- `GameObject > 3D Object > Cylinder`로 만든다
- **local position·rotation을 0으로** 둔다 — 뷰가 `Instantiate(prefab, transform)`로 몸의 자식에 붙이므로, 값이 남아 있으면 동전이 몸에서 어긋난 자리에 그려진다
- scale `(0.3, 0.02, 0.3)` — `DiscShape(반지름 0.15, 두께 0.04)`에 맞춘다(실린더 기본 지름 1·높이 2라 반지름 0.15는 0.3, 두께 0.04는 0.02)
- **자동으로 붙은 `CapsuleCollider`를 제거한다** — 몸은 `PhysicsBodyFactory`가 따로 붙인다. 남겨 두면 한 엔티티에 콜라이더가 둘이 돼 샘플 레이의 주인 판정과 데미지 중복 판정이 엉킨다
- Addressables 그룹(`Item.asset` 등 적당한 기존 그룹)에 추가하고 주소가 `Assets/Art_Placeholder/Panchigi/Coin.prefab`인지 확인한다(기본값이 경로라 그대로 두면 된다)

그런 다음 `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiRuleSystem.cs`의 상수를 바꾼다:

```csharp
//  진짜 동전 아트가 아직 없다 — 임시 실린더로 모양만 세운다.
//  아트가 들어오면 이 상수만 갈아 끼우면 된다.
private const string CoinVisualId = "Assets/Art_Placeholder/Panchigi/Coin.prefab";
```

그 위에 붙어 있던 "동전 프리팹이 아직 없다"는 주석은 지운다 — 더 이상 사실이 아니다.

- [ ] **Step 5: 확인 — 눈으로**

서버·클라 플레이 모드로 판치기 매치에 들어가 본다.

```bash
"$U" command capture_game_view --width 700 --save_path "Temp/shots/panchigi.png" --source screen --project-path "$CLIENT"
```
기대: 판이 비스듬히 내려다보이고 그 위에 **원기둥 동전 4개**가 놓여 있다.

> ⚠️ `capture_game_view --save_path`는 프로젝트 루트 안이어야 하고, `Temp/shots/x.png`를 주면 실제로는 `Assets/Temp/…`에 저장된다. 확인이 끝나면 **지운다.**

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scenes/Panchigi.unity Assets/Art_Placeholder/Panchigi Assets/AddressableAssetsData
git status --short   # 로컬 픽스처가 섞이지 않았는지 반드시 확인
git commit -m "feat(panchigi): 판에 두께를 주고 동전을 보이게 한다"

git -C ../LeagueOfPhysical-Server add Assets/Scenes/Panchigi.unity Assets/Scripts/Game/PanchigiRuleSystem.cs
git -C ../LeagueOfPhysical-Server status --short
git -C ../LeagueOfPhysical-Server commit -m "feat(panchigi): 판에 두께를 주고 동전 몸을 지정한다"
```

> ⚠️ Addressables 그룹 에셋(`Assets/AddressableAssetsData/AssetGroups/*.asset`)은 **평소 커밋하지 않는 로컬 픽스처**다. 이번엔 진짜로 항목을 추가했으므로 커밋하지만, `git diff --cached`로 **의도한 항목 추가만** 들어갔는지 확인한다.

---

## Task 13: 끝-끝 — 배포하고 실제로 쳐 본다

**Files:** (코드 변경 없음)

**Interfaces:**
- Consumes: Task 1~12 전부
- Produces: (없음 — 검증)

- [ ] **Step 1: 레포별 푸시**

각 레포에서 `CLAUDE.md`의 푸시 규약을 **한 줄씩 결과를 확인하며** 밟는다. Unity 레포는 리베이스 전에 로컬 픽스처를 `git stash push -u`로 빼둔다.

```bash
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff <feature>
git push origin main
```

레포 순서: `GameFramework` → `LeagueOfPhysical-Shared` → `MasterData-Client` → `MasterData-Server` → `infrastructure` → `lop-backend` → `LeagueOfPhysical-Server` → `LeagueOfPhysical-Client`.

- [ ] **Step 2: 배포 — 두 갈래 전부**

**마스터데이터가 바뀌었으므로 매칭서버, 게임서버 코드가 바뀌었으므로 게임서버.** 하나만 돌리면 "매칭은 되는데 방이 4초 만에 Error"가 난다.

```bash
gh workflow run backend-deploy.yml --repo Baeinsoo/lop-backend --ref main -f app=matchmaking-server
gh workflow run gameserver-deploy.yml --repo Baeinsoo/LeagueOfPhysical-Server --ref main -f environment=local
```

- [ ] **Step 3: 배포가 실제로 반영됐는지 확인**

```bash
kubectl get cm -o jsonpath='{range .items[*]}{.metadata.name}{" "}{.data.GAME_SERVER_IMAGE}{"\n"}{end}' | grep game
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server rev-parse --short main
```
기대: ConfigMap의 태그가 서버 `main`의 sha와 같다. 다르면 ArgoCD 동기화를 기다린다.

- [ ] **Step 4: 두 클라로 입장**

클라 에디터 + MPPM 클론을 플레이 모드로 두고 판치기(게임모드 7)로 매치를 잡는다. 클론은 `--project-path`에 `Library/VP/<clone>` 경로를 준다.

- [ ] **Step 5: 쳐 본다 — 이 슬라이스의 합격 기준**

| | 확인할 것 |
|---|---|
| 1 | 판을 끌면 **조준선이 보인다** |
| 2 | 놓으면 **동전이 튄다** |
| 3 | **두 클라가 같은 것을 본다** — 동전이 멎은 자리가 양쪽에서 같다 |
| 4 | 동전이 **뒤집힌다** — 회전이 생긴다(임펄스가 중심에서 벗어난 지점에 걸리는 효과) |
| 5 | 타격점에서 **먼 동전이 덜 움직인다** |
| 6 | 동전 위에 얹힌 동전은 **직접 힘을 안 받는다**(아래 동전에 밀려서만 움직인다) |
| 7 | 양쪽 콘솔에 **새 예외가 없다** |
| 8 | **FlapWang 근접 공격이 전과 같다** — 3-0이 그 경로를 건드렸다. 데미지 숫자가 뜨고, **한 번에 한 대만** 맞고, HP가 예상대로 준다. (3-0에서 확인하려 했으나 그때는 배포된 게임서버가 옛 이미지라 검증이 성립하지 않았다 — 배포 후인 여기가 진짜 경로다) |

```bash
"$U" command get_console_logs --severity error --limit 20 --project-path "$CLIENT"
kubectl logs $(kubectl get pods -o name | grep room-pod | head -1) | tail -40
```

- [ ] **Step 6: 튜닝**

2·4·5가 밋밋하거나 과하면 `#PanchigiConfig.xlsx`의 `force_multiplier` / `horizontal_force_multiplier` / `falloff_rate`를 조정하고 `gen.sh` → 재배포 → 다시 친다. **감각이 나올 때까지 반복한다** — 이 슬라이스의 목적이 그것이다.

- [ ] **Step 7: 임시 파일 정리**

```bash
rm -rf Assets/Temp        # capture_game_view가 남긴 스크린샷
git status --short        # 로컬 픽스처 5개만 남아야 한다
```

---

## Self-Review

**1. Spec coverage**

| spec 절 | 태스크 |
|---|---|
| 3.2 층 1(`CollisionHit`·`ICollisionQuery`·`UnityCollisionQuery`) | Task 2 |
| 3.2 층 2(`GetEntityId`), `EntityActor` | Task 1, 3 |
| 3.3 없어지는 것 | Task 5 |
| 3.4 `DamageEffectHandler` 이전 + 중복 제거 | Task 4 |
| 3.5 3-0 완료 증거 | Task 5 Step 5 |
| 4.1 조작 | Task 11 |
| 4.2 와이어 | Task 9 |
| 4.3 서버 검증 | Task 10 Step 1 |
| 4.4 힘 커널 + 테스트 | Task 7 |
| 4.5 `AddImpulseAtPosition` | Task 6 |
| 4.6 임시 몸·카메라 | Task 12 |
| 4.7 마스터데이터 | Task 8 |
| 5 배포 두 갈래 | Task 13 |

빠진 것 없음.

**2. Placeholder scan**

"적절히 처리한다"류 없음. 모든 코드 단계에 실제 코드가 있다. 확인이 필요한 세 곳(`ToUnityVector3`/`ToProtoVector3` 이름, `MessageHandlerBase` 등록 방식, `DamageEffect` 테스트 호출 방식)은 **"기존 코드에서 확인해 그대로 쓴다"** 로 명시했고, 어디를 볼지도 지정했다.

**3. Type consistency**

- `CollisionHit` 생성자 5인자 — Task 2에서 정의, Task 3·4 테스트에서 같은 순서로 사용 ✓
- `GetEntityId()` 반환 `string`(없으면 `null`) — Task 3 정의, Task 4·10에서 `null` 검사 ✓
- `ICollisionQuery.OverlapSphere(Vector3, float, int)` — Task 2 정의, Task 4 사용 ✓
- `ICollisionQuery.Raycast(Vector3, Vector3, float, int)` — Task 2 정의, Task 10 사용 ✓
- `PanchigiStrikeKernel.ComputeImpulse(in, in, Vector3[], int, int)` — Task 7 정의, Task 10 사용 ✓
- `BuildSamples(Vector3, float, Vector3[])` — Task 7 정의(버퍼 길이가 곧 K), Task 10에서 `new Vector3[config.CoverageSamples]` ✓
- `AddImpulseAtPosition(System.Numerics.Vector3, System.Numerics.Vector3)` — Task 6 정의, Task 10 사용 ✓
- `EntityActor.entityId` / `SetEntityId` — Task 1 정의, Task 3 테스트·구현에서 사용 ✓
