# 원격 엔티티 스냅샷 보간 (남 캐릭·아이템) — 표준 재설계

**Date:** 2026-07-07
**Branch:** `feature/remote-entity-interpolation`
**Related:** [netcode-redesign](../../netcode-redesign.md) · [world-core-connection-architecture](../../world-core-connection-architecture.md) · [architecture-guidelines](../../architecture-guidelines.md)

## Goal

원격 엔티티(남 캐릭터 + 아이템)의 렌더링을 **비표준 스프링-팔로우(`ServerStateReconciler`)에서 표준 시간-브래킷 스냅샷 보간으로 교체**한다. 대시로 남을 밀 때 부드럽게 밀리지 않고 "드르륵 뚝뚝" 끊기는 문제를 없앤다.

## 배경 / 문제

### 현재 (비표준)
남 캐릭·아이템 둘 다 `ServerStateReconciler`를 붙인다(`CharacterCreator:84`, `ItemCreator:60`). 이건 보간기가 아니라 **재조정기(reconciler)** 로 만들어졌다:

- 매 틱 `entity.position`을 **최신 스냅으로 `SmoothDamp`(스프링 팔로우)** + `distance > 0.48`이면 **하드스냅(순간이동)**.
- 원격엔 의미 없는 **dead-reckoning 잔재**(`localEntitySnaps` 누적 — 로컬 시뮬 있는 엔티티에만 의미, 원격은 diff≈0).
- 그 위에 `LateUpdate`가 **한 겹 더 lerp**(이중 보간).

**왜 끊기나:** 밀림(빠른 외력)에서 `SmoothDamp` 지연이 `속도×smoothTime≈0.4m`로 커져 0.48 하드스냅 문턱을 오르내림 → 간헐 순간이동 → 뚝뚝. 스프링 팔로우는 스냅 타임라인 정보를 버리는 **lag 필터**라, 등속 모션을 등속으로 재현 못 한다.

### 죽은 코드
`SnapInterpolator`가 표준에 가깝게 짜여 있으나 **어디에도 안 붙음(미사용)**. 게다가 (a) 클라 앞선 시계(`elapsedTime`)에서 `remoteBackTime`(=RTT 절반)만 빼 **핑을 이중/부족 반영**하고, (b) **매끄러운 재생 시계 없이** 매 프레임 직접 계산이라 도착 지터가 그대로 샌다(아래 crux/재생시계 참고).

## 산업 표준 매핑 (리서치 근거)

병렬 리서치 2건(back-time 공식 / 견고성)으로 못박음. 우리 방향이 표준으로 검증됨.

### Crux — 보간 지연은 RTT가 아니라 전송간격+지터
> **원격 보간 지연 = N × 전송간격 + 지터 여유 (N≈2~3). 레이턴시(RTT) 무관.**

원격 보간은 *이미 받은 두 스냅 사이*를 재생할 뿐 → 유일한 임무는 "다음 패킷 올 때까지 굶지 않기" = 전송 빈도 + 도착 불규칙성의 함수지 편도지연이 아니다. **RTT 절반은 *내 캐릭 예측 lead*의 축**(다른 타임라인) — 오버워치의 "½RTT+1프레임"도 *입력 예측*용이지 *원격 보간*용이 아님(흔한 혼동). RTT를 붙이면 핑 높을 때 과지연·낮을 때 부족지연이면서 정작 지터/손실엔 무용.

전송간격 = 33ms(30Hz, 서버가 매 틱 스냅 송신) → **쿠션 ≈ 2×33 + 지터 ≈ 80~100ms, 핑 무관**(적응형; 아래 "재생 시계") (Source 기본 100ms, Fiedler 30Hz 150ms와 정합).

### 표준 요소
- **시간 브래킷 보간**: `renderTime`을 감싸는 두 스냅을 타임스탬프로 찾아 사이를 보간. 예측·스프링 없음. (Gambetta/Valve/Fiedler)
- **위치 Hermite(속도 반영) + 회전 Slerp**: 순수 Lerp는 저전송률·빠른/휘는 모션에서 코너 깎임 → Fiedler가 **각 스냅 velocity로 Hermite**를 권함. 넉백(빠름)이 딱 그 케이스. 회전은 Slerp(최단호).
- **외삽 없음**: 강체엔 외삽이 나쁨(벽 뚫고 직진 추측) → 언더런 시 **직전 최신 스냅 hold**. 버퍼는 지터만큼 키워 언더런 자체를 줄임.
- **적응형 버퍼(모던 표준)**: 도착 지터를 측정해 타깃 지연을 계산, 부드럽게 수렴(rate/lerp, **역행 금지**), 범위 clamp. `ClockDilator`와 같은 규율.
- **kinematic 원격 콜라이더**: 보간된(과거) transform이 콜라이더 구동 = 표시 + 로컬 소프트충돌. **권위 피격은 서버 lag compensation**(이 spec 범위 밖).
- **스냅 unreliable**: full-state라 손실 OK, 오래된 스냅 재전송 무의미 = 표준.

### 출처
- [Gabriel Gambetta — Entity Interpolation](https://www.gabrielgambetta.com/entity-interpolation.html)
- [Glenn Fiedler — Snapshot Interpolation](https://gafferongames.com/post/snapshot_interpolation/) / [Networked Physics](https://gafferongames.com/categories/networked-physics/)
- [Valve — Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking) / [Interpolation](https://developer.valvesoftware.com/wiki/Interpolation) / [Lag Compensation](https://developer.valvesoftware.com/wiki/Lag_Compensation)
- [Overwatch Gameplay Architecture and Netcode — GDC 2017 (Tim Ford)](https://www.gdcvault.com/play/1024001/-Overwatch-Gameplay-Architecture-and)
- [Unity Netcode for Entities — Interpolation](https://docs.unity3d.com/Packages/com.unity.netcode@1.5/manual/interpolation.html) · [Photon Fusion — Network Buffers](https://doc.photonengine.com/fusion/current/manual/advanced/network-buffers)

## 설계

### 컴포넌트: `RemoteEntityInterpolator` (LOP-Client)
`LocalEntityInterpolator`(내 캐릭)와 짝. 원격 엔티티(**남 캐릭 + 아이템**)에 붙는 per-entity MonoBehaviour.

- **스냅 버퍼**: 타임스탬프 정렬. unreliable이라 **순서 뒤바뀜·손실 가능** → 삽입 시 정렬, 최신보다 오래된 건 무시, 용량 초과 시 오래된 것 제거.
- **매 프레임(`LateUpdate`)**:
  1. `renderTime = 공유 재생 시계 값` (아래 "보간 시계"에서 굴림 — 여기선 읽기만)
  2. `renderTime`을 감싸는 두 스냅 `older/newer` 탐색
  3. **위치 = Hermite(older.pos, older.vel, newer.pos, newer.vel, u)**, **회전 = Slerp(older.rot, newer.rot, u)**
  4. 결과를 `entity.position/rotation`에 씀 → **콜라이더 + 비주얼 동시 구동(단일 보간)**
  5. 언더런(감싸는 쌍 없음) → **최신 스냅으로 hold**
- `ServerStateReconciler`의 이중 보간(sim SmoothDamp + 렌더 lerp)을 **단일 보간**으로 대체.

### 보간 시계 (핵심 결정) — receive-anchored 재생 시계
원격 렌더 시각은 `serverNow`(진짜 서버 now)도 `elapsedTime`(클라 앞선 시계)도 기준으로 쓰지 않는다. **둘 다 최신 받은 스냅보다 핑만큼 앞서** 있어, 거기서 순수 버퍼만 빼면 최신 스냅에 붙어(또는 앞질러) 핑 높을수록 **언더런**한다. 대신 **받은 스냅에 앵커된 매끄러운 재생 시계**를 굴린다:

- **목표(target)** = `최신 받은 스냅 소인 − 쿠션`. 도착 지터로 들쭉날쭉.
- **재생 시계(`renderTime`)** = 매 프레임 `+= deltaTime × rate`로 **부드럽게 전진**, 절대 점프·되감기 없음. `rate`를 미세 조정(≈0.98~1.02)해 target 추종(뒤처지면 빨리, 라이브에 붙으면 늦춤). → **지터는 target만 흔들고, 재생 위치는 매끄럽게 흡수.**
- 스냅 `timestamp = 서버틱 × 틱간격`(서버 타임라인, `Game.Entity.MessageHandler:55`)과 재생 시계가 같은 축 → 정확히 브래킷.
- 이렇게 **받은 스냅 기준으로 재면 쿠션 = N×전송간격 + 지터, 핑 무관**(핑은 "최신 스냅이 이미 얼마나 옛날인가"에만 반영, 쿠션엔 안 들어감). 미러 `localTimeline`(`deltaTime × localTimescale`, 버퍼 점유로 rate 조정)과 동일 원리. 두 타임라인(예측=서버+lead / 보간=최신스냅−쿠션) 깨끗이 분리.
- **단일 공유 재생 시계**: 서버가 모든 엔티티 스냅을 한 메시지(`EntitySnapsToC`, 한 틱)로 보내 모든 원격이 같은 타임라인 → 재생 시계 **하나**를 최신 배치 소인으로 구동, 각 `RemoteEntityInterpolator`가 그 `renderTime`을 읽어 자기 버퍼를 샘플.

### 적응형 쿠션 + 재생 시계 (GameFramework.Netcode, 순수·TDD)
두 순수 조각. `ClockDilator`/`InputTimingTracker` 짝, EditMode 단위 테스트.

**(1) `InterpolationDelayEstimator` — 적응형 쿠션.**
- 스냅 **도착 간격**을 기록 → 지터(도착 간격 표준편차 σ) 추정.
- `쿠션 = N × sendInterval + K × σ` (N≈2, K≈2~3), **clamp [min≈1×interval, max≈3~5×interval]**.

**(2) 재생 시계 — target 추종(rate).**
- `target = 최신스냅소인 − 쿠션`. 매 프레임 `renderTime += dt × rate`, `rate`로 target 추종, **역행 없음**. 큰 갭(오래 굶다 재개 등)만 스냅(teleport threshold), 평소엔 rate만.
- **`ClockDilator` 재사용 검토**: 내 캐릭 예측 시계도 "target으로 rate 수렴 + 역행 금지"라 같은 도구. API가 충분히 일반적이면 그대로 재사용, 아니면 형제 클래스(plan에서 API 확인).

- 정확한 N/K/bounds/rate 밴드는 plan에서 확정 + 테스트로 고정.

### Hermite 보간
- 두 스냅 `A(posA, velA, tA)`, `B(posB, velB, tB)`, 파라미터 `u∈[0,1]` (`u=(renderTime−tA)/(tB−tA)`).
- 큐빅 Hermite: `p(u) = h00·posA + h10·(dt)·velA + h01·posB + h11·(dt)·velB`, `dt=tB−tA`. **탄젠트를 구간 길이 dt로 스케일**(velocity는 m/s라 위치 단위로 환산).
- 회전은 Slerp(최단호 — 쿼터니언 double-cover 부호 처리).

### 채널 (LOP-Server) — reliable 유지
~~unreliable로 변경~~ **보류** — 메시지가 unreliable 상한(1184B) 초과(위 "채널 결정" 참고). `LOPRunner.EndUpdate`의 `session.Send(entitySnapsToC)` **그대로**.

### 정리 (삭제)
- **`ServerStateReconciler`** — 남 캐릭·아이템 이관 후 삭제(dead-reckoning 잔재 포함).
- **`SnapInterpolator`** — 죽은 코드 + 틀린 back-time. `RemoteEntityInterpolator`가 대체하므로 삭제.
- **`remoteBackTime`(=RTT 절반)** — 유일 소비자였던 `SnapInterpolator` 제거로 무용해지면 함께 제거/deprecate.

## 파일 영향 (개략 — 세부는 plan)

- **신규(Client)**: `RemoteEntityInterpolator.cs`(per-entity), 공유 재생 시계 홀더(예: `RemoteInterpolationClock`, 게임 스코프 DI — `ReconciliationStats`류)
- **신규(GameFramework)**: `Netcode/InterpolationDelayEstimator.cs`(쿠션), Hermite 순수 헬퍼, 재생 시계 rate 추종(`ClockDilator` 재사용 or 형제) — 각 EditMode 테스트
- **수정(Client)**: `CharacterCreator`/`ItemCreator`(남 엔티티에 `RemoteEntityInterpolator` 부착), `Game.Entity.MessageHandler`(최신 틱을 공유 재생 시계에 피드 + 원격 dispatch를 새 컴포넌트로), `GameLifetimeScope`(DI 등록)
- **삭제(Client)**: `ServerStateReconciler.cs`, `SnapInterpolator.cs`
- **수정(Server)**: `LOPRunner.cs`(스냅 `reliable: false`)

## 채널 결정 — reliable 유지 (unreliable 보류)
당초 스냅을 **unreliable**로 바꾸려 했으나(표준: full-state라 재전송 무의미), 실측에서 `EntitySnapsToC`가 **현재 스케일에서 이미 ~1200B**로 Mirror unreliable **단일 배치 상한(1184B)을 초과** → 매 틱 통째 드롭(원격 동기 붕괴)이 확인됨. reliable은 Mirror가 조각내 보내 이 크기도 문제없다. → **reliable 유지**(status quo). 보간(receive-anchored+쿠션+hold)은 reliable에서도 정상이고, reliable은 순서 보장이라 오히려 보간이 단순.
- **unreliable 채택은 별도 슬라이스**: 메시지를 1184B 밑으로 만드는 선행 작업 필요 — **interest management(근처 엔티티만) 또는 스냅 분할/델타 압축**. 규모 커지면 착수.

## Out of Scope
- **서버 lag compensation**(권위 피격을 클라가 본 과거 시점으로 되감기) — 별개 서버 트랙.
- **내 캐릭 보간**(`LocalEntityInterpolator`) — 변경 없음.
- **Stage④**(예측/롤백/Snapshot-Restore/velocity 권위) — 별개.
- **아이템 회전/velocity 특수 처리** — 캐릭과 동일 경로로 통일(아이템도 pos/rot 보간이면 충분).

## Open Questions (plan에서 해소)
- `ClockDilator` API가 재생 시계(target 추종+역행 금지)에 그대로 재사용 가능한지, 아니면 형제 클래스인지.
- 공유 재생 시계의 수명·DI 스코프(게임 스코프 싱글턴) + 시작 시드(첫 스냅 도착 전 렌더 억제).
- `entity.position` 쓰기 경로가 **kinematic 리지드바디**를 갱신해 내 예측 대시가 원격과 충돌하는지(`PushMotionToPhysics`/`LOPEntityController` 동기 확인).
- 적응형 쿠션 + rate 파라미터(N, K, bounds, rate 밴드) 초기값 — 관측으로 튜닝.
- Hermite velocity 단위/프레임(월드 m/s 가정) 최종 확인 + 넉백 코너깎임 육안/로그 검증.
- unreliable 전환 후 스냅 순서/손실 실측(버퍼·hold가 충분한지).

## 산업 표준 매핑 노트
- `RemoteEntityInterpolator` = Gambetta "entity interpolation" / Fiedler "snapshot interpolation" 그대로.
- **receive-anchored 재생 시계 + rate 추종** = 미러 `localTimeline`/`localTimescale`, NGO/Fusion의 보간 시계 time-dilation과 동일. 우리 `ClockDilator`(예측 시계)와 같은 "타깃으로 rate 수렴, 역행 금지" 규율을 보간 시계에 적용.
- `InterpolationDelayEstimator`(적응형 쿠션) = Fusion/NGO의 jitter-기반 adaptive interpolation window.
- 보간 시계 분리(예측=서버+lead / 보간=**최신스냅−쿠션**) = netcode-redesign §9.2 두(세) 타임라인 모델. 핵심: 원격 보간은 **받은 스냅 기준**이라 쿠션이 핑 무관.

## 진행
- [x] 브레인스토밍 합의 (스프링→시간브래킷, 적응형-지금, Hermite, 언더런=hold(외삽X), unreliable, 남캐릭+아이템 통일, 컴포넌트 통합 삭제)
- [x] 리서치 2건(back-time 공식 / 견고성)으로 표준 못박음
- [x] 코드 확인(타임스탬프=서버틱 기반 / 채널=현재 reliable / Send 시그니처 / serverNow=진짜 now)
- [x] 이 spec 작성
- [x] 정정: 보간 시계 = serverNow−delay(오류) → **receive-anchored 재생 시계 + rate 추종**(사용자 리뷰서 발견)
- [x] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
