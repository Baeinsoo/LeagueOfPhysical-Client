# S4 — Unity 트리 재구성 (표준 정합 앵커 + 두 바디)

> 엔티티 Unity 레이어 재구조화 umbrella(`2026-07-18-entity-view-rearchitecture-umbrella-design.md`)의 **S4**.
> 상태는 `docs/ROADMAP.md`가 단일 원천. 선행 S1·S2·S3·③물리통합 완료.

## 왜 (문제)

현재 엔티티 Unity 트리는 "여러 GameObject 구성"이라는 개념 자체는 표준이지만 **실현이 비표준**이다. 실측(2026-07-18):

**클라 Character 현재 트리:**
```
Character_{id}                  ← 빈 루트 (컴포넌트 0)
├── Visual                      ← 빈 컨테이너 (모델 인스턴스가 이 밑)
├── Physics                     ← 빈 컨테이너
│   └── PhysicsGameObject       ← Rigidbody + CapsuleCollider (layer=Character)
├── LOPEntity (GO)             ← LOPEntity + PhysicsFollower + Local/RemoteEntityInterpolator
├── LOPEntityView (GO)         ← LOPEntityView
├── DamageFloaterEmitter (GO)  ← EntityBinder가 붙임
└── CharacterNameplate (GO)    ← EntityBinder가 붙임
```

**서버 Character 현재 트리:** (뷰 없음이지만 같은 골격)
```
Character_{id}                  ← 빈 루트
├── Visual                      ← 빈 컨테이너 (서버는 안 씀)
├── Physics
│   └── PhysicsGameObject       ← Rigidbody + CapsuleCollider + TriggerDetector
├── LOPEntity (GO)             ← LOPEntity + PhysicsFollower
├── LOPEntityView (GO)         ← 죽은 코드 (never used, 컴파일만)
└── LOPAIController (GO)       ← AI일 때만
```

### 표준과 어긋난 점 (5)

1. **빈 루트** — 루트가 컴포넌트 0인 상자. 표준은 루트가 곧 "이 엔티티"의 단일 앵커.
2. **rb+콜라이더가 두 겹 아래** (`root→Physics→PhysicsGameObject`). Unity 표준은 **rb 하나를 루트에**.
3. **behavior 스크립트가 형제 자식 GO로 흩어짐** (LOPEntity·View·floater·nameplate·AIController는 각자 GO, PhysicsFollower·interpolator만 컴포넌트) — 비일관·비표준. "스크립트 하나당 GameObject 하나"는 Unity 안티패턴.
4. **`Physics`/`Visual` 빈 래퍼** — 불필요한 중첩.
5. **문자열·구조 배선** — `Find("Visual")`/`Find("Physics")`/`GetComponentInChildren<LOPEntityView>()`/`transform.parent?.parent?.GetComponentInChildren<LOPEntity>()`. 이름·트리 깊이 바꾸면 조용히 깨짐.

### 이름 문제

`LOPEntity`는 진짜 엔티티(`GameFramework.World.Entity`)와 이름이 겹쳐 혼란스럽다 — 얘는 엔티티가 아니라 그걸 비추는 Unity 앵커/뷰다("뚱뚱한 그림자" 잔재). S3에서 얇은 앵커로 축소 확정됐으므로 정체성이 안정적 → 표준어로 rename할 시점.

## 산업 표준 근거 (2026-07-18 웹 리서치)

- **넷코드 — 두 바디 분리:** NGO `NetworkRigidbody`는 비권위 인스턴스에서 rb를 kinematic으로 두고 권위 위치를 동기; 소유 클라=예측, 원격=보간("권위 물리 시뮬 ↔ 클라가 보간하는 시각 표현" 분리). LOP `Local/RemoteEntityInterpolator`(로컬=예측+보간, 원격=보간)와 1:1.
- **Unity 계층:** compound collider는 "rb **하나를 루트 GameObject에**". 권장 형태 = 루트(Rigidbody) → 비주얼 모델 자식.
- **Unreal Actor:** 컨테이너(Actor=GameObject) + RootComponent(transform) + Mesh/Movement 등은 **컴포넌트**. `ACharacter`는 CapsuleComponent가 루트, 메시가 자식, CharacterMovementComponent가 컴포넌트.
- **Entitas:** Entity(데이터) + **View**(GameObject측 표현, 모델/애니 소유). DOTS Companion GameObject(엔티티→GO 단방향, 얇음).

### 산업 표준 매핑 (LOP → 프레임워크)

| LOP | 대응 표준 |
|---|---|
| `LOPActor` (루트 앵커 = 시뮬 바디) | Unreal **Actor + RootComponent**(capsule) / Unity rb-on-root |
| `Rigidbody + CapsuleCollider` (루트, kinematic) | NGO kinematic follower / Unreal CapsuleComponent |
| `Visual` 자식 (독립 보간) | 넷코드 **interpolation body** / Gambetta entity interpolation |
| `LOPEntityView` (모델+애니 소유, 컴포넌트) | Entitas **View** / Unreal SkeletalMeshComponent |
| `PhysicsFollower` (rb 소유·follow, 컴포넌트) | Unreal CharacterMovementComponent |
| `EntityBinder` (수명 신호로 뷰 부착) | Entitas **reactive AddView 시스템** |

## 목표 모델

**한 문장:** 엔티티의 Unity 루트가 곧 앵커 겸 **시뮬 바디**(kinematic rb+콜라이더), 모든 behavior는 루트 **컴포넌트**, 렌더 바디(비주얼)만 자식.

**클라 Character 목표 트리:**
```
Actor_{id}   (루트 = 앵커 + 시뮬 바디)
   컴포넌트:  LOPActor  ·  Rigidbody + CapsuleCollider (kinematic, layer=Character)
             PhysicsFollower  ·  Local/RemoteEntityInterpolator
             LOPEntityView  ·  CharacterNameplate  ·  DamageFloaterEmitter
   └── Visual (자식 = 렌더/보간 바디; 모델 인스턴스가 이 밑, 독립 보간)
```

**클라 Item 목표 트리:** 동일 골격, nameplate/floater 없음. interpolator=Remote만.
```
Actor_{id}   (루트)
   컴포넌트:  LOPActor · Rigidbody + CapsuleCollider(trigger) · PhysicsFollower · RemoteEntityInterpolator · LOPEntityView
   └── Visual (자식)
```

**서버 Character 목표 트리:** (뷰/Visual 없음)
```
Actor_{id}   (루트 = 시뮬 바디)
   컴포넌트:  LOPActor · Rigidbody + CapsuleCollider(+TriggerDetector) · PhysicsFollower
             LOPAIController (AI일 때만)
```
서버는 `Visual`/`LOPEntityView`/nameplate/floater 없음. 죽은 `LOPEntityView.cs` **삭제**.

### 두 바디 명확화 (넷코드)

- **시뮬/권위 바디 = 루트**(kinematic rb+콜라이더). World.Transform을 호스트 `MotionBridge.PushMotion`이 밀어넣음(스무딩 X, 권위/예측 위치). 충돌 쿼리·콜라이더→엔티티 매핑의 대상.
- **렌더/보간 바디 = `Visual` 자식.** 인터폴레이터가 스냅 사이를 스무딩해 이 자식의 world transform에 씀. 루트(시뮬)와 스무딩 offset만큼 어긋나며 따라감 = 의도된 디커플링.
- 루트가 rb로 움직여도 `Visual` 자식은 인터폴레이터가 world position을 직접 세팅하므로 무관(child의 world .position 세팅은 부모 이동과 독립).

## 결정 규칙 (컴포넌트별 운명)

| 현재 | 운명 | 이유 |
|---|---|---|
| `LOPEntity` (자식 GO) | **rename `LOPActor` + 루트 컴포넌트로** | 앵커 = Actor 루트 |
| `PhysicsFollower` (컴포넌트) | 유지, rb+콜라이더를 **루트에** 생성(`Physics` 래퍼 제거) | rb-on-root 표준 |
| `Local/RemoteEntityInterpolator` (컴포넌트) | 유지, 루트 컴포넌트(이미 그러함) | behavior 컴포넌트 |
| `LOPEntityView` (자식 GO) | **루트 컴포넌트로**(이름 유지 — "View"는 표준어) | Entitas View = 컴포넌트 |
| `CharacterNameplate`/`DamageFloaterEmitter` (자식 GO) | **루트 컴포넌트로** (binder가 AddComponent) | world-UI 소유 스크립트 |
| `LOPAIController` (서버, 자식 GO) | **루트 컴포넌트로** | behavior 컴포넌트 |
| `Visual` 컨테이너 | **단일 렌더 바디 자식으로** 유지 | interpolation body |
| `Physics` 컨테이너 / `PhysicsGameObject` | **제거** — rb+콜라이더 루트로 | 불필요 중첩 |
| 서버 `LOPEntityView.cs` | **삭제** | 죽은 코드 |

## 배선 (문자열/구조 순회 제거)

`LOPActor`가 자기 파트 typed 핸들을 노출(크리에이터가 조립 시 세팅): **`View`**(LOPEntityView), **`VisualRoot`**(Visual 자식 Transform). 소비자는 문자열 대신 핸들/`GetComponent`로 획득.

| 현재 배선 | 교체 |
|---|---|
| 클 `PhysicsFollower`: `parent.Find("Physics")` | rb+콜라이더를 자기(루트) GameObject에 생성 — Find 소멸 |
| 클 `LOPEntityView`: `parent.Find("Visual")` | `VisualRoot` 핸들(크리에이터 세팅) 밑에 모델 인스턴스 |
| 클 `DamageFloaterEmitter`/`CharacterNameplate`: `parent.GetComponentInChildren<LOPEntityView>()` | `GetComponent<LOPEntityView>()` (같은 루트) |
| 클 `EntityBinder`: `entity.transform.parent.gameObject` | `entity.gameObject`(= 루트) + `AddComponent` |
| 서 `PhysicsFollower.OnTriggerEnter`: `other.transform.parent?.parent?.GetComponentInChildren<LOPEntity>()` | `other.GetComponentInParent<LOPActor>()` |
| 서 `LOPOverlapQuery`: `hit.transform.parent?.parent?.GetComponentInChildren<LOPEntity>()` | `hit.GetComponentInParent<LOPActor>()` |
| 매니저/핸들러: `TryGetEntity<LOPEntity>` | `TryGetEntity<LOPActor>` (rename) |

`IEntity` 인터페이스(GameFramework 공유)는 **유지** — 서버 매니저 `GetAllEntitySnaps`가 파사드를 `IEntity`로 읽어 완전 삭제는 매니저 재작업(S5)과 묶임. rename은 concrete `LOPEntity`→`LOPActor`만.

## 빌드 순서 (strangler, 격리 스텝)

매 스텝 끝에 게임이 그대로 돌아야 함. leaf-first가 아니라 이번엔 **rename → 골격 → 배선** 순서(구조 변경을 rename diff와 분리).

1. **rename만** — `LOPEntity`→`LOPActor` (클·서 + 매니저/핸들러 호출부). 순수 rename 격리 커밋, 동작·구조 무변화. (파일 rename + `.meta` 동반.)
2. **시뮬 바디 루트 이전** — 크리에이터가 루트 GameObject에 `LOPActor`를 직접 붙임(자식 GO 아님). `PhysicsFollower`가 rb+콜라이더를 **루트에** 생성(`Physics` 래퍼 제거, layer=Character). `PhysicsFollower`/interpolator/(서버)AIController도 루트 컴포넌트. 콜라이더→엔티티 매핑을 `GetComponentInParent<LOPActor>()`로 교체(서버 trigger + LOPOverlapQuery). `EntityBinder`가 `entity.gameObject`(루트)에 부착.
3. **뷰/UI 컴포넌트 루트 이전** — `LOPEntityView`·`CharacterNameplate`·`DamageFloaterEmitter`를 루트 컴포넌트로(binder AddComponent). `GetComponentInChildren`→`GetComponent`. 서버 죽은 `LOPEntityView.cs` 삭제.
4. **렌더 바디 정리** — `Visual`을 단일 렌더 바디 자식으로, `LOPActor.VisualRoot` 핸들. `LOPEntityView`가 모델을 `VisualRoot` 밑에 로드, 인터폴레이터가 그것을 보간. 문자열 배선 최종 제거 확인.

## 범위

- **포함:** 클라이언트 + 서버.
- **제외 (S5):** `entityMap`↔`EntityRegistry` 이중화 축소, `IEntity` 완전 삭제, 거대 Creator→표준 스포너/바인더 분해.
- **제외 (YAGNI):** 프리팹화(현 코드 조립 유지 — 프리팹/스포너는 S5), 권위 핸드오프.

## 그린 판정

- 클·서 컴파일 클린 (UnityMCP).
- 인게임(사용자): 모델 로드/애니(걷기·공격·피격), 이동·**캐릭터끼리 충돌**, **아이템 줍기**(서버 trigger), **넉백/데미지 판정**(서버 OverlapSphere), HP바(nameplate)·데미지 플로터, 롤백(로컬 예측), 원격 보간.
- 콜라이더→엔티티 매핑이 새 트리에서 정확(전투 히트·아이템 접촉이 옳은 엔티티를 찾음).

## 검증 포인트 (plan에서 확정)

- `Character` 레이어 충돌 매트릭스 — 루트가 Character 레이어를 이게 됨(루트에 다른 레이어 의존 없는지).
- 클라 kinematic sweep(`ICollisionQuery`/`KinematicMover`)의 self-collider 제외 방식이 트리 구조에 의존하는지.
- 서버 working-tree 로컬 픽스처(테스트 auth/playerList 등) 커밋 금지 — plan에서 격리.
- 각 저장소 feature 브랜치(`feature/entity-s4-*`), 클·서 원자적으로(같은 rename).

## Open Decisions (plan/구현에서)

- `LOPActor` 핸들 표면: `View`+`VisualRoot`만으로 충분한지(다른 소비자 없으면 최소).
- `Visual` 렌더 바디를 지속 노드로 둘지(인터폴레이터가 항상 이동, 모델은 그 자식) vs 모델 인스턴스 자체를 이동(현행). 지속 노드가 async 로드 전에도 보간 대상이 있어 더 깨끗 — plan에서 확정.
