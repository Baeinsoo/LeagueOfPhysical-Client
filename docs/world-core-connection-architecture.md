# World Core ↔ 프레젠테이션/네트워크 연결 아키텍처

순수 C# 시뮬레이션 코어(`GameFramework.World`: Entity / Component / System)와 Unity 프레젠테이션·네트워크 계층을 **어떻게 연결하는가**에 대한 결정. fast-paced(오버워치식) 예측/보정 + 결정론·롤백을 전제로 한다.

## 레이어와 의존성 방향

- **안쪽(inner)**: 순수 C# World Core — 데이터(`Component`) + 로직(`System`). 엔진/네트워크 비의존(`noEngineReferences`).
- **바깥(outer)**: Unity View/Controller, 물리, 네트워크 보정(Reconciler), UI.
- **의존성은 안쪽으로만 향한다**(클린/헥사고날). 코어는 바깥의 구체 타입을 **참조하거나 호출하지 않는다.**

## 정보가 경계를 넘어 바깥으로 가는 방법

두 경로만 사용한다 (둘 다 의존성은 안쪽 유지):

1. **연속 상태(위치/HP값/애니 등) → 바깥이 안쪽을 읽는다(pull).**
   View가 `World.Entity` 참조를 들고 매 프레임 읽어 갱신. 코어는 자신이 관찰됨을 모른다.

2. **사건(데미지/사망/레벨업 등) → 안쪽은 이벤트를 "데이터"로 버퍼에 쌓기만** 하고, 바깥이 꺼내 처리한다. 콜백 호출 아님.

**금지**: 코어가 View/Controller를 참조하거나 eventbus를 직접 호출하는 것(= 안쪽→바깥 호출). 지금 LOP가 컴포넌트 setter에서 `EventBus.Publish` 하는 방식은 새 구조에서 버린다.

## Deferred — "모아서 확정 후 지연 처리" (구조적 핵심)

예측/롤백에선 **같은 틱이 여러 번 재시뮬**된다. 시뮬 중 이펙트(사운드/파티클/UI)를 동기로 실행하면 재시뮬마다 중복·가짜로 터진다. 따라서:

- 시스템은 사건을 **이벤트 데이터로 버퍼(큐)에 append만** 한다.
- **확정 게이트(commit gate)**: **확정된(committed) 틱의 이벤트만** 바깥으로 통과시킨다. 재시뮬 틱의 버퍼는 버리거나 중복 제거.
- 바깥(브릿지/프레젠테이션)이 정해진 시점에 큐를 **드레인**해 처리하고, 거기서 eventbus(R3 등)로 팬아웃한다.
- **비동기/멀티스레드가 아니라 단일 스레드, 프레임 내 지연 드레인**(순서 결정론적).
- (뉘앙스) 내 캐릭터의 *예측* 사건은 반응성 위해 확정 전 즉시 표시 + 빗나가면 보정하는 변형 가능. 남/서버권위 사건은 확정분만.

이 패턴의 출처:
- **롤백 넷코드** — "롤백 때는 결정론적 내부 상태만 재시뮬하고, 오디오/비주얼 이펙트는 최종 확정 상태까지 미룬다": [SnapNet Rollback](https://www.snapnet.dev/blog/netcode-architectures-part-2-rollback/), [coherence Determinism/Prediction/Rollback](https://docs.coherence.io/manual/advanced-topics/competitive-games/determinism-prediction-rollback)
- **ECS 커맨드버퍼** — "기록은 해두고 `Playback()` 시점에 일괄 재생(deferred)": [Unity DOTS EntityCommandBuffer](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/systems-entity-command-buffers.html)

## 연결 배선

- View/Controller MonoBehaviour가 `World.Entity` 참조를 보유(생성 시 `Bind`). **이 참조가 곧 연결.**
- 프레젠터/매니저가 World를 보고 **entityId로** GameObject+View를 생성/바인드/파괴.
- eventbus(R3)는 **바깥(브릿지)에** 둔다. 확정 이벤트 버퍼가 그걸 먹인다. **코어 안엔 두지 않는다.**

## 동기화 모델 (fast-paced, 오버워치식) — 상세는 Stage ④에서

- **내 캐릭터**: +N틱 예측(prediction) + 입력 리플레이 보정(reconciliation). (LOP `SnapReconciler`)
- **남/서버권위 객체**: 과거 보간(interpolation), 미래 예측 안 함. (LOP `ServerStateReconciler`)
- **판정 공정성**: 서버 lag compensation(favor-the-shooter) — 남을 내가 본 과거 시점으로 되감아 판정.
- 내가 **단독·결정론적으로** 유발하는 기믹만 "예측 버블"에 넣어 내 캐릭과 함께 롤백/리시뮬. 경합/비결정적이면 서버 권위 대기.

## 코어에 요구되는 능력 (이 모델을 받치기 위해)

- 결정론적 `Step(input, dt)` 진입점.
- **Snapshot / Restore** (되감기·리플레이용).
- 이벤트는 **데이터로 출력**(버퍼). 코어는 이벤트 없이 순수·결정론 유지.

## 참고 링크

- 🎥 오버워치 GDC 2017 (ECS + 예측 + 결정론, 우리 모델의 원형): [YouTube](https://www.youtube.com/watch?v=W3aieHjyNvw) / [GDC Vault](https://www.gdcvault.com/play/1024001/-Overwatch-Gameplay-Architecture-and)
- 예측/보정/보간 기본기: [Gambetta Fast-Paced Multiplayer](https://www.gabrielgambetta.com/client-server-game-architecture.html)
- lag compensation: [Valve Source Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking), [Lag Compensation](https://developer.valvesoftware.com/wiki/Lag_Compensation)
- 스냅샷 보간/네트워크 물리: [Gaffer On Games](https://gafferongames.com/post/snapshot_interpolation/)
- deferred(롤백): [SnapNet](https://www.snapnet.dev/blog/netcode-architectures-part-2-rollback/), [coherence](https://docs.coherence.io/manual/advanced-topics/competitive-games/determinism-prediction-rollback)
- deferred(ECS 커맨드버퍼): [Unity DOTS ECB](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/systems-entity-command-buffers.html)
- 도메인 이벤트(일반): [Fowler, Domain Event](https://martinfowler.com/eaaDev/DomainEvent.html)

## 상태

모델 확정. 구현은 마이그레이션을 진행하며 구체화하고, 예측/롤백/확정게이트 본격 구현은 Stage ④. 현재 World Core는 이 모델을 받칠 수 있도록 순수·결정론·엔진비의존으로 구축 중.
