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
`SnapInterpolator`가 표준에 가깝게 짜여 있으나 **어디에도 안 붙음(미사용)**. 게다가 back-time을 `remoteBackTime`(=RTT 절반)으로 써서 **축이 틀렸다**(아래 crux 참고).

## 산업 표준 매핑 (리서치 근거)

병렬 리서치 2건(back-time 공식 / 견고성)으로 못박음. 우리 방향이 표준으로 검증됨.

### Crux — 보간 지연은 RTT가 아니라 전송간격+지터
> **원격 보간 지연 = N × 전송간격 + 지터 여유 (N≈2~3). 레이턴시(RTT) 무관.**

원격 보간은 *이미 받은 두 스냅 사이*를 재생할 뿐 → 유일한 임무는 "다음 패킷 올 때까지 굶지 않기" = 전송 빈도 + 도착 불규칙성의 함수지 편도지연이 아니다. **RTT 절반은 *내 캐릭 예측 lead*의 축**(다른 타임라인) — 오버워치의 "½RTT+1프레임"도 *입력 예측*용이지 *원격 보간*용이 아님(흔한 혼동). RTT를 붙이면 핑 높을 때 과지연·낮을 때 부족지연이면서 정작 지터/손실엔 무용.

전송간격 = 33ms(30Hz, 서버가 매 틱 스냅 송신) → **interpDelay ≈ 2×33 + 지터 ≈ 80~100ms, 핑 무관 고정값** (Source 기본 100ms, Fiedler 30Hz 150ms와 정합).

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
  1. `renderTime = serverNow − interpDelay`
  2. `renderTime`을 감싸는 두 스냅 `older/newer` 탐색
  3. **위치 = Hermite(older.pos, older.vel, newer.pos, newer.vel, u)**, **회전 = Slerp(older.rot, newer.rot, u)**
  4. 결과를 `entity.position/rotation`에 씀 → **콜라이더 + 비주얼 동시 구동(단일 보간)**
  5. 언더런(감싸는 쌍 없음) → **최신 스냅으로 hold**
- `ServerStateReconciler`의 이중 보간(sim SmoothDamp + 렌더 lerp)을 **단일 보간**으로 대체.

### 보간 시계 (핵심 결정) — 서버시각 기준
```
renderTime = Runner.NetworkTime.serverNow − interpDelay
```
- 스냅 `timestamp = 서버틱 × 틱간격`(이미 서버 타임라인, `Game.Entity.MessageHandler:55`)과 **같은 축** → 정확히 브래킷.
- **클라 앞선 시계(`elapsedTime − backTime`)를 쓰지 않는다** — 그건 lead(RTT 성분)와 버퍼를 뒤섞어 `SnapInterpolator`가 RTT를 끌어들인 원인. 서버시각 기준으로 빼면 `interpDelay`가 **순수 버퍼(RTT 무관)** 가 된다. 두 타임라인(예측=앞선 / 보간=서버−지연) 깨끗이 분리.

### 적응형 보간 지연: `InterpolationDelayEstimator` (GameFramework.Netcode, 순수·TDD)
`ClockDilator`/`InputTimingTracker` 짝. 순수 C#이라 EditMode 단위 테스트.

- 스냅 **도착 간격**을 기록 → 지터(도착 간격의 표준편차 σ) 추정.
- `target = N × sendInterval + K × σ` (N≈2~3, K≈2~3), **clamp [min≈1×interval, max≈3~5×interval]**.
- 현재 지연 → 타깃 **부드럽게 수렴**(lerp/rate). 지연 증가율 < 시계 진행율이라 `renderTime` **단조 증가(역행 없음)**.
- 정확한 N/K/bounds/수렴계수는 plan에서 확정 + 테스트로 고정.

### Hermite 보간
- 두 스냅 `A(posA, velA, tA)`, `B(posB, velB, tB)`, 파라미터 `u∈[0,1]` (`u=(renderTime−tA)/(tB−tA)`).
- 큐빅 Hermite: `p(u) = h00·posA + h10·(dt)·velA + h01·posB + h11·(dt)·velB`, `dt=tB−tA`. **탄젠트를 구간 길이 dt로 스케일**(velocity는 m/s라 위치 단위로 환산).
- 회전은 Slerp(최단호 — 쿼터니언 double-cover 부호 처리).

### 채널 변경 (LOP-Server)
- `LOPRunner.EndUpdate`의 `session.Send(entitySnapsToC)` → **`session.Send(entitySnapsToC, reliable: false)`**. (입력은 이미 Phase 3b에서 unreliable — 스냅만 맞추면 됨.)

### 정리 (삭제)
- **`ServerStateReconciler`** — 남 캐릭·아이템 이관 후 삭제(dead-reckoning 잔재 포함).
- **`SnapInterpolator`** — 죽은 코드 + 틀린 back-time. `RemoteEntityInterpolator`가 대체하므로 삭제.
- **`remoteBackTime`(=RTT 절반)** — 유일 소비자였던 `SnapInterpolator` 제거로 무용해지면 함께 제거/deprecate.

## 파일 영향 (개략 — 세부는 plan)

- **신규(Client)**: `Assets/Scripts/Entity/RemoteEntityInterpolator.cs`
- **신규(GameFramework)**: `Netcode/InterpolationDelayEstimator.cs` + EditMode 테스트
- **수정(Client)**: `CharacterCreator`/`ItemCreator`(남 엔티티에 `RemoteEntityInterpolator` 부착), `Game.Entity.MessageHandler`(원격 dispatch를 새 컴포넌트로)
- **삭제(Client)**: `ServerStateReconciler.cs`, `SnapInterpolator.cs`
- **수정(Server)**: `LOPRunner.cs`(스냅 `reliable: false`)

## Out of Scope
- **서버 lag compensation**(권위 피격을 클라가 본 과거 시점으로 되감기) — 별개 서버 트랙.
- **내 캐릭 보간**(`LocalEntityInterpolator`) — 변경 없음.
- **Stage④**(예측/롤백/Snapshot-Restore/velocity 권위) — 별개.
- **아이템 회전/velocity 특수 처리** — 캐릭과 동일 경로로 통일(아이템도 pos/rot 보간이면 충분).

## Open Questions (plan에서 해소)
- `Runner.NetworkTime.serverNow`의 정확도/가용성(클라 시작 직후 등) — 브래킷 기준 시계로 안정적인지.
- `entity.position` 쓰기 경로가 **kinematic 리지드바디**를 갱신해 내 예측 대시가 원격과 충돌하는지(`PushMotionToPhysics`/`LOPEntityController` 동기 확인).
- 적응형 파라미터(N, K, bounds, 수렴계수)의 초기값 — 관측으로 튜닝.
- Hermite velocity 단위/프레임(월드 m/s 가정) 최종 확인 + 넉백 코너깎임 육안/로그 검증.
- unreliable 전환 후 스냅 순서/손실 실측(버퍼·hold가 충분한지).

## 산업 표준 매핑 노트
- `RemoteEntityInterpolator` = Gambetta "entity interpolation" / Fiedler "snapshot interpolation" 그대로.
- `InterpolationDelayEstimator`(적응형 버퍼) = Fusion/NGO의 jitter-기반 adaptive interpolation window에 대응. `ClockDilator`(clock time-dilation)와 같은 "타깃으로 rate 수렴, 역행 금지" 규율.
- 보간 시계 분리(예측=서버+lead / 보간=서버−delay) = netcode-redesign §9.2 두(세) 타임라인 모델.

## 진행
- [x] 브레인스토밍 합의 (스프링→시간브래킷, 적응형-지금, Hermite, 언더런=hold(외삽X), unreliable, 남캐릭+아이템 통일, 컴포넌트 통합 삭제)
- [x] 리서치 2건(back-time 공식 / 견고성)으로 표준 못박음
- [x] 코드 확인(타임스탬프=서버틱 기반 / 채널=현재 reliable / Send 시그니처)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
