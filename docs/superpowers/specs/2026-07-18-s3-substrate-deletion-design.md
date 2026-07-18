# S3 — 레거시 엔티티/컴포넌트 substrate 완전 삭제

> **부모:** `2026-07-18-entity-view-rearchitecture-umbrella-design.md` (S3/5). 상태는 `docs/ROADMAP.md`.
> **범위:** 클라이언트 + 서버 + GameFramework. 끝에 게임이 그대로 돌아가야 함(strangler).
> **2 서브슬라이스:** S3a(컴포넌트 substrate 죽은 삭제) → S3b(엔티티 substrate 완전 삭제).

## 목표

S1+S2로 `entity.components` 병렬 리스트가 비었으므로, 이제 **레거시 GameFramework 엔티티/컴포넌트
substrate machinery를 삭제**한다: `IComponent`/`MonoComponent`/`LOP.LOPComponent`/`Status`(컴포넌트 반쪽) +
`MonoEntity`(엔티티 베이스) + `Entity.Extensions`. `LOPEntity`는 순수 `MonoBehaviour`(파사드 + 물리
브릿지만 든 얇은 뷰 앵커)로 남는다. **`IEntity`는 4멤버 파사드 계약으로 축소해 잠정 유지**(매니저가 파사드를
IEntity로 읽어 완전 삭제는 매니저 재작업=S5와 묶임 — 아래 정정 참조). 이로써 World Core와 병존하던 옛
MonoBehaviour *machinery*(컴포넌트 시스템 + MonoEntity 베이스)가 소멸한다.

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

## 설계 — S3b: `MonoEntity` 삭제 + `LOPEntity` 순수 MonoBehaviour화 (얇은 IEntity 유지)

> **범위 정정 (계획 단계 실측):** 서버 `LOPEntityManager.GetAllEntitySnaps`(`:178-190`)가 `GetEntities()`로
> 얻은 엔티티에서 **`IEntity`의 파사드(entityId/position/rotation/velocity)를 제네릭으로 읽는다.** 따라서
> `IEntity`를 `MonoBehaviour`로 재타입하면 매니저가 깨져 — **`IEntity` 완전 삭제는 매니저를 LOP-concrete로
> 재작업하는 S5와 묶인다.** 사용자 지침("힘들면 S5로 이월")대로 **S3는 `IEntity`를 얇은 계약으로 남기고**
> machinery만 제거한다.

- **`IEntity` 축소:** `{ entityId, position/rotation/velocity }` 4멤버 파사드 계약만 남긴다(S3a에서 이미
  components/Attach/Detach 제거, 아래에서 `UpdateEntity`도 제거). **삭제는 안 함 → S5.**
- **`LOPEntity : MonoEntity` → `LOPEntity : MonoBehaviour, IEntity`**(클·서). `entityId`·`position`/
  `rotation`/`velocity`를 `override`가 아니라 자체 선언(`IEntity` 구현, 본문·PropertyChange 발행 동일 유지).
  `LinkWorldMotion`/`Initialize`/`RaisePropertyChanged`/`SyncPhysics`/`PushMotionToPhysics`는 평범한 메서드로 유지.
- **`MonoEntity.cs` 삭제(GF)** — `LOPEntity`가 더는 상속 안 함.
- **`UpdateEntity` 제거:** `IEntity`에서 `UpdateEntity()` 멤버 제거 + `LOPEntity` override 삭제 + 매니저
  `UpdateEntities()`(client `:123-129`/server `:149-155`)와 그 호출부 제거(빈 순회).
- **`IsGrounded`를 `LOPEntity` 인스턴스 메서드로 이전**(`this IEntity` 확장 → `this.position` 읽는 메서드),
  호출부 `LOPEntityView.cs:97` `entity.IsGrounded()` 유지. `GetEntityTransform`(0 호출) 삭제 →
  **`Entity.Extensions.cs` 통째 삭제.**
- `EntityCreated.entity : IEntity` **유지**(IEntity 살아있음) — 소비자 무변경.

**S5로 이월(명시):** `IEntity` 삭제 + `IEntityCreator`/`IEntityManager`/`IEntityFactory`/`IBrain` 제네릭
재타입 + `EntityCreated` 재타입 + 서버 매니저 파사드-읽기(GetAllEntitySnaps)를 LOP-concrete로. 매니저/팩토리
구조 재작업과 한 슬라이스로 묶어 처리(목표는 IEntity/IComponent 레거시 완전 청소).

**그린(S3b):** `MonoEntity` 참조 0, `Entity.Extensions` 삭제됨, `LOPEntity : MonoBehaviour, IEntity`,
클·서·GF 컴파일 클린, GF EditMode 269 green, 인게임(스폰·이동·충돌·아이템·AI·넉백·전역공격) 무변화.

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
- **IEntity는 S3에서 얇은 계약(4멤버 파사드)으로 축소·유지, 완전 삭제는 S5**(매니저가 파사드를 IEntity로
  읽어 매니저 재작업과 묶임). 사용자 "완전 삭제가 목표, 힘들면 S5 이월" 지침 반영. ✅
- **물리 브릿지 유지, ③ 통합은 별도 슬라이스.** ✅
- **S3a→S3b 순서**(컴포넌트 반쪽 먼저 죽은 삭제, 그다음 엔티티 반쪽). ✅
