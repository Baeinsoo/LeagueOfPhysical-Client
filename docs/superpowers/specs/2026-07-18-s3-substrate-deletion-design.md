# S3 — 레거시 엔티티/컴포넌트 substrate 완전 삭제

> **부모:** `2026-07-18-entity-view-rearchitecture-umbrella-design.md` (S3/5). 상태는 `docs/ROADMAP.md`.
> **범위:** 클라이언트 + 서버 + GameFramework. 끝에 게임이 그대로 돌아가야 함(strangler).
> **2 서브슬라이스:** S3a(컴포넌트 substrate 죽은 삭제) → S3b(엔티티 substrate 완전 삭제).

## 목표

S1+S2로 `entity.components` 병렬 리스트가 비었으므로, 이제 **레거시 GameFramework 엔티티/컴포넌트
substrate를 통째 삭제**한다: `IComponent`/`MonoComponent`/`LOP.LOPComponent`/`Status`(컴포넌트 반쪽) +
`IEntity`/`MonoEntity`(엔티티 반쪽) + `Entity.Extensions`. `LOPEntity`는 순수 `MonoBehaviour`(파사드 +
물리 브릿지만 든 얇은 뷰 앵커)로 남는다. 이로써 World Core와 병존하던 옛 MonoBehaviour 타입 시스템이
소멸한다.

## 실측 근거 (2026-07-18 blast-radius 감사)

- **컴포넌트 반쪽 = 전부 죽은 코드.** `IComponent`/`MonoComponent`/`LOPComponent`(클·서)/`Status`(서버)는
  살아있는 서브클래스·인스턴스 **0**. `Entity.Extensions`의 컴포넌트 메서드(AddEntityComponent×2/
  GetEntityComponent/GetEntityComponents/TryGet/Has/Find/GetEntityTransform) 호출 **0**(단 서버
  `LOPEntity.UpdateStatuses()`가 `GetEntityComponents<Status>()`를 부르나 항상 빈 집합 = 죽은 코드).
  매니저(`LOPEntityManager`)의 `components`/`DetachEntityComponent` 정리 루프 2개 = no-op.
- **엔티티 반쪽 = `LOPEntity` 밑 load-bearing이나 대부분 파사드.** `LOPEntity`의 지배적 참조는
  `entityId`(식별 키)와 `position/rotation/velocity` 파사드(World.Transform/Velocity 프록시). 둘 다 순수
  `MonoBehaviour`에 그대로 살 수 있음.
- **`IEntity` 결합점(전부 분리 가능):** `IEntityCreator<TEntity,…> where TEntity : IEntity`(크리에이터 4개),
  `IEntityManager`(where T : IEntity, `LOPEntityManager`), `IEntityFactory`/`EntityFactory`, `IBrain<T>
  where T : IEntity`(서버 `EnemyBrain`), `EntityCreated.entity : IEntity`(클·서 이벤트). `IEntityCreationData`·
  `EntityDestroyed`(entityId만)는 `IEntity` 비의존 → 무변경.
- **`IsGrounded(this IEntity)`** — `[Obsolete]`, 유일 호출 client `LOPEntityView.cs:97`(run-anim 게이트).
  `GetEntityTransform` = 0 호출.
- **물리 브릿지(③)는 이 슬라이스 범위 밖** — `SyncPhysics`/`PushMotionToPhysics`(비-Simulated rb 회전을
  유일하게 밀어줌)와 `LOPEntityController`는 유지. 통합은 별도 슬라이스(behavioral, MotionBridge 확장 필요).

## 설계 — S3a: 컴포넌트 substrate 죽은 삭제

동작 무변화(전부 죽은 코드). 삭제/정리:

- **삭제 파일:** GF `Runtime/Scripts/Component/IComponent.cs`·`MonoComponent.cs`; client·server
  `Assets/Scripts/Component/LOPComponent.cs`; server `Assets/Scripts/Component/Status.cs`.
- **`Entity.Extensions.cs`(GF):** 컴포넌트 메서드 전부 삭제(`GetEntityTransform`/`GetEntityComponent`/
  `GetEntityComponents`/`TryGetEntityComponent`/`HasEntityComponent`/`FindEntityComponent`/
  `AddEntityComponent`×2). `IsGrounded`만 잠정 잔류(S3b에서 이전 후 파일 삭제).
- **`IEntity`/`MonoEntity`:** `components`·`AttachEntityComponent`·`DetachEntityComponent` 멤버 제거
  (IComponent 참조라 함께 빠짐). 이후 `IEntity` = `{ entityId, position/rotation/velocity, UpdateEntity }`.
- **서버 `LOPEntity`:** `UpdateStatuses()` 삭제 + `UpdateEntity()` 본문에서 그 호출 제거(→ 빈 함수).
- **매니저:** client `LOPEntityManager.cs:96-98`·server `:110-112`의 `components`/`DetachEntityComponent`
  정리 루프 제거.

**그린(S3a):** GF·클·서 컴파일 클린 + `IComponent`/`MonoComponent`/`LOPComponent`/`Status` 참조 0 +
GF EditMode 회귀(269 green). 인게임 무변화(죽은 코드라 자명하나 스모크 확인).

## 설계 — S3b: 엔티티 substrate 완전 삭제

- **`LOPEntity : MonoEntity` → `LOPEntity : MonoBehaviour`**(클·서). `entityId`·`position`/`rotation`/
  `velocity`를 `override`가 아니라 자체 선언(본문·PropertyChange 발행 동일 유지). `LinkWorldMotion`/
  `Initialize`/`RaisePropertyChanged`/`SyncPhysics`/`PushMotionToPhysics`는 평범한 메서드로 유지.
- **`UpdateEntity`(빈 함수) + 매니저 호출(client `:127`/server `:153`) 제거.**
- **`IsGrounded`를 `LOPEntity`의 메서드로 이전**(`this IEntity` 확장 → 인스턴스 메서드), 호출부
  `LOPEntityView.cs:97`를 `entity.IsGrounded()`로 유지. `GetEntityTransform` 삭제 → **`Entity.Extensions.cs`
  통째 삭제.**
- **`MonoEntity.cs` + `IEntity.cs` 삭제(GF).**
- **`IEntity` 결합점 재타입:**
  - `IEntityCreator<out TEntity,in TCreationData> where TEntity : IEntity` → `where TEntity : MonoBehaviour`.
  - `IEntityManager`(제네릭·반환 `IEntity`) → `MonoBehaviour`(entityMap `Dictionary<string,MonoBehaviour>`,
    `GetEntity<T> where T : MonoBehaviour`).
  - `IEntityFactory`/`EntityFactory` `where TEntity : IEntity` → `MonoBehaviour`.
  - server `IBrain<T> where T : IEntity` → `MonoBehaviour`.
  - `EntityCreated.entity : IEntity` → `LOPEntity`(클·서). 소비자 캐스트(`is not LOPEntity`)는 단순화 or 유지.

**안전장치:** 매니저/팩토리 *구조*(LOPEntityManager/EntityFactory 자체)는 S5 소관 — S3b는 **제약만 완화**.
- **알려진 얽힘 리스크:** 제네릭 코드가 `IEntity.entityId`를 *제네릭 참조로* 읽으면 `MonoBehaviour`엔
  `entityId`가 없어 깨진다(예: `LOPEntityManager.Add`가 `entity.entityId`로 키를 만들면). 계획 단계에서
  각 재타입 지점이 `.entityId`/파사드를 *제네릭 T로 접근*하는지 확인 — 접근하면 그 지점만 (a) `LOPEntity`로
  캐스트하거나 (b) **S5로 이월**(구조 재작업과 함께). 순수 저장·반환만 하는 제네릭은 `MonoBehaviour`로 즉시 완화.
- 재타입이 예상보다 얽히면 **그 부분만 S5로 이월**(사용자 승인). 목표는 IEntity/IComponent 레거시 완전 청소.

**그린(S3b):** `IEntity`/`MonoEntity` 참조 0, `Entity.Extensions` 삭제됨, 클·서·GF 컴파일 클린,
GF EditMode 269 green, 인게임(스폰·이동·충돌·아이템·AI·넉백·전역공격) 무변화.

## 범위 밖 (이월)

- **③ 물리 경로 통합**(SyncPhysics/PushMotionToPhysics/LOPEntityController 삭제 → MotionBridge를 비-Simulated
  까지 확장 + Reconciler 롤백-후 push 대체) = **별도 슬라이스**(behavioral, 플레이 검증). S4 트리 재구성과
  함께 또는 그 앞에.
- 매니저/팩토리/스포너 구조 재작업 = **S5**.

## 산업 표준 매핑
- 옛 `IEntity`+`MonoEntity`+`IComponent`+`MonoComponent`(병렬 MonoBehaviour 엔티티/컴포넌트 시스템)는
  World Core(`GameFramework.World.Entity`/`Component`, 순수 C# CBD)로 이미 대체됨. S3는 그 이행을 완결 —
  DOTS/Entitas에서 데이터=엔티티, GameObject=얇은 뷰(back-machinery 없음)와 정합.

## Open Decisions (확정)
- **IEntity 완전 삭제**(마커로 유지 아님) — 제네릭 제약 `MonoBehaviour`로 재타입. ✅ (얽히면 S5 이월)
- **물리 브릿지 유지, ③ 통합은 별도 슬라이스.** ✅
- **S3a→S3b 순서**(컴포넌트 반쪽 먼저 죽은 삭제, 그다음 엔티티 반쪽). ✅
