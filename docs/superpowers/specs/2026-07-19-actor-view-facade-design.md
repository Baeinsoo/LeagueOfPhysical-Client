# Actor = 대표 파사드 · View 별도 유지 (Actor–View Facade)

엔티티 Unity 레이어 재-아키텍처(S1~S5)의 후속. 지금은 한 `Actor_{id}` 루트에 `LOPActor`(앵커)와
`LOPEntityView`(시각 뷰)가 **형제 컴포넌트**로 붙어 있고, 외부 코드가 `actor`와 `entityView`를 **두 참조**로
따로 든다. 이 슬라이스는 **`LOPActor`를 엔티티의 단일 대표(파사드)로** 만들어, 외부가 `LOPActor` 하나만
참조하게 하고 뷰 서브시스템을 그 뒤로 숨긴다.

> **핵심: 합치기(merge)가 아니라 파사드다.** `LOPEntityView`는 삭제하지 않고 렌더링 전담 컴포넌트로
> **그대로 유지**한다. `LOPActor`가 그 View를 소유·대표하고 `visualGameObject`를 위임 노출한다.

## 목표 (무엇을 · 왜)

- **무엇을**: 외부 코드(보간기·플레이어 컨텍스트·카메라·월드스페이스 UI)가 뷰에 접근할 때 `entityView`
  (`LOPEntityView`)를 직접 들지 않고 **대표 `LOPActor`를 통해** 접근하도록 바꾼다. 접근 표면은
  `actor.visualGameObject` 하나.
- **왜**: 지금 `LOPActor`는 `entityId`만 든 "거의 빈 껍데기"다. 외부는 `actor`(entityId)와 `entityView`
  (visualGameObject)를 이중으로 참조한다. Actor를 **대표 파사드**로 세우면 (1) 외부 참조가 하나로 통일되고,
  (2) 뷰 타입(`LOPEntityView`)이 외부에서 숨겨져 결합이 준다.

## 산업 표준 매핑

이 구조는 새 발명이 아니라 두 검증된 관용구의 조립이다 (architecture-guidelines "임의 명명 금지" 준수):

- **언리얼 Actor = 컴포넌트 컨테이너·대표**: Epic 정의 — Actor는 *"하나 이상의 컴포넌트를 하나의 대표
  엔티티(single representative entity)로 조합해 담는 컨테이너"* 이며 **Actor 자체는 트랜스폼도 없고 루트
  컴포넌트에 의존**한다. 즉 언리얼 Actor는 렌더링을 자기가 하지 않고 메시 컴포넌트를 *소유·대표*한다. 본 설계의
  "Actor가 View를 소유·대표"와 1:1. ([Actors in Unreal Engine](https://dev.epicgames.com/documentation/unreal-engine/actors-in-unreal-engine))
- **유니티 `gameObject.transform` = 파사드-라이트 선례**: 유니티는 자주 쓰는 서브컴포넌트를
  `GetComponent<Transform>()` 강요 대신 **편의 프로퍼티로 노출**한다. "자주 필요한 건 대표 객체가 접근자로
  노출"이 유니티 네이티브 관용구 → `actor.visualGameObject` 위임 노출은 과설계(무거운 GoF Facade)가 아니라
  유니티 정합. ([Understanding Unity Engine Objects](https://blog.eyas.sh/2020/10/unity-for-engineers-pt5-object-component/))
- **GetComponent 규칙**: `GetComponent<T>()`는 매 프레임 호출이 안티패턴, init에서 캐시가 정석. 뷰 소비자는
  전부 `LateUpdate`(매 프레임)라 **매번 `actor.GetComponent<View>()`는 애초에 탈락** → 참조는 스포너가 한 번
  꽂는다(캐시). ([JetBrains ReSharper — Avoid GetComponent in perf context](https://github.com/JetBrains/resharper-unity/wiki/Avoid-usage-of-GetComponent-methods-in-performance-critical-context))

> **경계 (god object 회피)**: Actor가 대표로 소유·노출하는 것은 **시각 표면(visualGameObject)** 까지다.
> 물리(`PhysicsFollower`)·보간(`Local/RemoteEntityInterpolator`)·입력·AI·장식 UI는 **형제 컴포넌트로 유지**
> — 언리얼도 컨트롤러는 Actor에서 뗀다. 대표 = 렌더링 표면의 단일 핸들이지 만능 객체가 아니다.

## 아키텍처

```
Actor_{id} (GameObject, 루트)
├─ LOPActor            ← 대표/앵커/파사드: entityId, Initialize, view 소유, visualGameObject 위임 노출
├─ LOPEntityView       ← 렌더링 전담(별도 유지): 모델 로드·애니·cue·자기 ICleanup
├─ PhysicsFollower     ← 형제(무변)
├─ Local/RemoteEntityInterpolator  ← 형제, 참조를 actor로
└─ (캐릭터) DamageFloaterEmitter, CharacterNameplate  ← 형제, 참조를 actor로
   └─ visual model (LOPEntityView가 Instantiate한 자식)  ← 보간기가 매 프레임 transform 세팅
```

### 대표 표면 (파사드)

- `LOPActor`가 자기 View 참조를 든다. 스포너(`EntityBinder`)가 View 생성 직후 `actor.SetView(view)`로 등록
  (매 프레임 GetComponent 없음, 생성 순서 안전 — Actor.Awake 시점엔 View가 아직 없으므로 스포너가 꽂는다).
- `LOPActor.visualGameObject { get }` → `view != null ? view.visualGameObject : null` 위임. 외부는 이 하나만 읽는다.
- **뷰 서브시스템은 파사드를 모른다**: `LOPEntityView`는 `LOPActor`를 참조하지 않는다. 지금 `SetEntity(LOPActor)`로
  받던 것을 **`SetEntityId(string)`** 로 바꿔 entityId만 받는다 (구독·Appearance 조회에 필요한 건 entityId뿐).

### 데이터 흐름 (변화 없음)

- 연속 상태(위치/속도)는 지금처럼 World.Entity에서 pull, 이산 사건(어빌리티/피격)은 MessagePipe 구독.
  파사드는 이 흐름을 바꾸지 않는다 — 단지 외부가 뷰 GameObject에 닿는 *핸들*만 Actor로 통일.

## 변경 목록

| 파일 | 변화 |
|---|---|
| `Entity/LOPActor.cs` | `view` 필드 + `SetView(LOPEntityView)` + `visualGameObject` 위임 프로퍼티 추가. 기존 `entityId`/`Initialize` 유지 |
| `Entity/LOPEntityView.cs` | `SetEntity(LOPActor)` → `SetEntityId(string)`. Actor 참조 제거(entityId만 보유). 렌더링 로직 전부 무변 |
| `Game/EntityBinder.cs` | `view.SetEntity(actor)` → `view.SetEntityId(actor.entityId)` + `actor.SetView(view)`. 보간기 `.entityView` 배선 제거(actor만 연결) |
| `Entity/LocalEntityInterpolator.cs` | `entityView` 필드 삭제 → `actor` 하나로 통합(`actor.entityId` + `actor.visualGameObject`) |
| `Entity/RemoteEntityInterpolator.cs` | `entityView` → `actor`, 읽기 `actor.visualGameObject` (`worldEntity` 무변) |
| `Game/IPlayerContext.cs`·`Game/PlayerContext.cs` | `entityView`(LOPEntityView) → `actor`(LOPActor). 로직용 `entityId`는 유지(logic-decouple 원칙 — 로직은 entityId, 뷰는 actor) |
| `Game/LOPGamePresenter.cs` | `playerContext.entityView.visualGameObject` → `playerContext.actor.visualGameObject` (카메라 타깃) |
| `UI/WorldSpace/DamageFloaterEmitter.cs` | `GetComponent<LOPEntityView>()` → `GetComponent<LOPActor>()`, 읽기 `actor.visualGameObject` |
| `UI/WorldSpace/CharacterNameplate.cs` | 위와 동일 |

## 유지 (바꾸지 않음)

- `LOPEntityView`의 렌더링 로직(Addressables 모델 로드, Run 애니, 어빌리티/피격 cue, 자기 `ICleanup`) 전부 무변.
- 뷰 초기화 주체: `EntityBinder`(스포너)가 View를 만들고 View는 자기 `Start()`에서 자체 초기화 — **S5b 분리
  유지**(Creator=데이터+앵커, 스포너=뷰). 파사드는 "스포너가 View를 Actor에 등록"만 추가.
- 물리/보간/입력/AI/장식 UI = 형제 컴포넌트 유지.
- 파괴/정리: 루트 GameObject 파괴 + 기존 `ICleanup` 스윕이 각 컴포넌트를 정리(View의 ICleanup 그대로). Actor는
  view를 소유만 하고 별도 dispose 불필요(같은 GameObject라 원자적 파괴).

## 범위

**클라이언트 프로젝트만.** 서버 `LOPEntityView`(테스트 렌더)는 뷰 개념이 달라 이 슬라이스 밖 — 필요 시 후속.

## 검증

컴파일 클린(새 에러 0) + 인게임 1라운드에서 회귀 없음 확인:
- 캐릭터/아이템 비주얼 모델 로딩
- 걷기(Run) 애니, 공격/피격 애니 cue
- 데미지 숫자 플로터(머리 위)
- 네임플레이트 HP바 위치 추종
- 카메라가 내 캐릭터 추적(`playerContext.actor.visualGameObject` 경로)
- 엔티티 파괴 시 `ICleanup` 정리 에러 0

## Open Decisions

- [ ] 향후 Actor가 `visualGameObject` 외에 다른 뷰 표면(예: `Animator` 접근자)을 대표로 노출할지 — 지금은
  외부가 `visualGameObject`만 필요하므로 그 하나만 위임(YAGNI). 새 소비 표면이 생기면 그때 대표에 추가.
- [ ] 서버 뷰 파사드 패리티 — 서버는 뷰가 테스트 렌더뿐이라 보류.
