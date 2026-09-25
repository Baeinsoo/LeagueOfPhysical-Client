# Netcode Phase 3 — Server Input Buffer 정렬 + Unreliable Redundancy

**Date:** 2026-06-20
**Branch (docs):** `feature/netcode-phase3-input-buffer` (클라 repo — 설계 허브)
**Branch (code):** 서버 repo(3a) + 클·서+LOP-Shared(3b) 피처 브랜치 (구현 시 생성)
**Related:** [Netcode 재설계](../../netcode-redesign.md) (§5 Phase3 / §9.6) · [Phase 2 clock sync](2026-06-20-netcode-phase2-clock-sync-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md)

## Goal

Phase 2(클라 client-ahead)가 만든 "입력이 서버에 미리 도착"을 서버가 제대로 활용하도록, **서버 입력 버퍼를 command-frame 정렬**(입력의 클라 tick == 서버 처리 tick)로 바꾸고 **jitter buffer**(처리 직전 도착 보장)를 둔다. 동시에 입력 전송을 **reliable → unreliable + sliding-window redundancy**로 전환해, reliable 채널의 head-of-line blocking 지연을 제거하고 패킷 유실을 무대기로 복구한다.

이로써 클·서가 **같은 tick·같은 상태·같은 입력**으로 처리해 reconciliation 갭을 추가로 줄인다(reconciliation 로직 자체는 무변경).

## 배경 / 조사 (구현 사실)

- **현 서버 `EntityInputComponent.GetInput(serverTick)`**: `targetTick = tick − INPUT_DELAY_TICKS`(현재 0), `inputBuffer.Where(k <= targetTick)` 중 **가장 오래된 것**을 반환. → "클라 tick == 서버 tick" 정렬 아님(Phase 2로 입력 tick이 서버 tick보다 크게 와도 `<= targetTick` 아니라 처리 안 됨), jitter buffer 없음.
- **호출**: `LOPGameEngine.ProcessInput`이 매 틱 `GetInput(GameEngine.Time.tick)` → 이동/액션 처리 + `InputSequenceToC` 회신.
- **⚠️ 입력 채널 = reliable (코드 확인)**: 클라 `PlayerInputManager` → `session.Send(playerInputToS)` → `LOPSession.Send` → `networkConnection.Send(msg)` (채널 인자 없음) → Mirror 기본 `Channels.Reliable(0)`. KCP reliable = ARQ 재전송 + 순서보장 → **유실은 없지만 head-of-line blocking 지연**(손실 시 재전송 대기 동안 후속 입력 줄줄이 막힘). fast-paced 입력엔 부적합(Gaffer: "never use TCP for time critical data").
- **KCP 두 채널 (코드 확인)**: `SendReliable`→`kcp`(ARQ, fast retransmit) / `SendUnreliable`→`RawSend`(raw UDP, 재전송 없음). redundancy는 **unreliable에만 유효**(reliable은 이미 재전송하니 사본 중복=낭비).
- **wire `PlayerInputToS`(proto)**: `session_id`, `tick`, `player_input`, `entity_transform` — **단일 입력만 담음**. redundancy(최근 N틱)엔 repeated 필드 추가 필요(LOP-Shared 변경).
- **`entity_transform`**: 서버 `ProcessInput`이 map은 하나 `LOPMovementManager.ProcessInput`이 실사용 안 함(클라 보고 transform 미사용 — netcode-redesign §6.4 cheating 주의와 정합). redundancy 번들에서 제외 가능.
- **리서치 근거**(별도 조사, netcode-redesign §9.6): command-frame 정렬 + 1~3틱(기본 2) jitter buffer + sliding-window redundancy(Overwatch ~10틱, Unity NetCode ~3틱, Source `cl_cmdbackup` 2) = 전 시스템 표준. miss 정책은 시스템마다 다름(반복/no-input/decay) — redundancy가 있으면 miss가 희귀해 부차적.

## 잠긴 결정 (브레인스토밍 + 리서치 합의)

| # | 축 | 결정 | 근거 |
|---|---|---|---|
| ① | 정렬 | **command-frame**: 처리 = `inputBuffer[serverTick − INPUT_DELAY_TICKS]` (클라 tick == 서버 tick) | Phase 3 핵심. Phase 2 client-ahead의 의미를 살림 |
| ② | jitter buffer | `INPUT_DELAY_TICKS = 2` | Unity NetCode 기본 2틱. 입력이 처리 직전 도착하게 여유 |
| ③ | miss(입력 없음) | **no-input**(반복·외삽 안 함) | redundancy가 miss를 희귀하게 → 단순이 정답. 드문 miss는 기존 recon이 처리. (반복/decay는 측정 후 옵션) |
| ④ | 지각(`Tick < targetTick`) | **drop**(prune) | 서버 롤백 안 함. 표준 |
| ⑤ | 입력 채널 | **reliable → unreliable** | redundancy는 unreliable에만 유효(코드+Gaffer 확인). reliable의 head-of-line blocking 지연 제거 |
| ⑥ | redundancy | 클라가 패킷마다 **최근 N틱(기본 3) 입력 묶어 전송**, 서버 `AddInput`이 tick 중복 무시 | 유실 무대기 복구(Overwatch/Source/Gaffer 표준) |
| ⑦ | 클라 보정 | 기존 `SnapReconciler` **무변경** | §6.3 — 갭 축소만 기대 |
| ⑧ | 범위 | 3a 서버 전용, 3b 클·서+wire | blast radius 분리 |

> **miss=no-input 근거(durable)**: 서버는 miss 원인(유실/지각/클라 미전송) 구분 불가 → 반복은 "키 계속 누름" 가정이 틀리면 갭을 *만듦*. redundancy+jitter buffer+Phase2 lead가 miss를 희귀하게 만드는 게 본질이고, 남는 드문 miss는 no-input(서버 정직) + 클라 recon으로 처리. (Unity DOTS 클라 예측도 missing=skip. 반복/decay는 비표준 아님이나, redundancy 위에선 no-input이 가장 단순·충분.)

## Slice 3a — 서버 command-frame 정렬 + jitter buffer (서버 전용)

### `Assets/Scripts/Component/EntityInputComponent.cs` 변경

`INPUT_DELAY_TICKS = 0` → `2`. `GetInput` 재작성:

```csharp
public PlayerInputToS GetInput(long tick)
{
    long targetTick = tick - INPUT_DELAY_TICKS;

    // 1. 처리 시점 지난 입력 prune (지각 = drop)
    var stale = inputBuffer.Keys.Where(k => k < targetTick).ToList();
    foreach (var k in stale) inputBuffer.Remove(k);

    // 2. command-frame 정렬: 정확히 targetTick의 입력만 처리
    if (inputBuffer.TryGetValue(targetTick, out var input))
    {
        inputBuffer.Remove(targetTick);
        lastProcessedSequence = input.PlayerInput.SequenceNumber;
        return input;
    }

    // 3. miss → no-input (서버 정직, 클라 recon이 보정)
    return null;
}
```

- `tick <= targetTick 중 최소` → `tick == targetTick 정확 일치`로 변경(정렬).
- `< targetTick`(지각/처리불가)은 prune(drop) — 버퍼에 남아 나중에 잘못 처리되는 것 방지.
- miss = `null`(현 호출부 `if (input == null) continue;`가 이미 no-input 처리 — `LOPGameEngine.ProcessInput`).
- `AddInput`의 중복 tick / 시퀀스 가드는 유지(기존 보존). lastProcessedSequence/expectedNextSequence 의미 유지.

### 3a 효과
정렬 정확화 + 2틱 jitter buffer. 입력이 아직 reliable·단일 전송이지만, Phase 2 lead로 입력 tick이 서버 tick보다 앞서 도착 → `targetTick = serverTick − 2`에 맞춰 꺼냄. 클·서 같은 tick 처리 → recon 동등/개선. **클라/wire 무변경.**

## Slice 3b — 입력 unreliable 전환 + sliding-window redundancy (클·서+wire)

### wire (LOP-Shared) — `PlayerInputToS`에 redundancy 번들 추가

`Protos/PlayerInputToS.proto`에 최근 입력들을 담는 repeated 필드. 형태(plan에서 확정):
```proto
message PlayerInputEntry {
    int64 tick = 1;
    PlayerInput player_input = 2;
}
message PlayerInputToS {
    string session_id = 1;
    int64 tick = 2;                       // 최신(현재) 입력 tick — 기존 호환
    PlayerInput player_input = 3;         // 최신 입력 — 기존 호환
    ProtoTransform entity_transform = 4;  // 기존 유지(미사용이나 보존)
    repeated PlayerInputEntry recent_inputs = 5;  // 신규: 최근 N틱(현재 포함 or 직전 N-1)
}
```
proto 재생성(LOP-Shared 파이프라인). `recent_inputs` 추가는 비파괴(기존 필드 유지). **단일 소스 = `recent_inputs`로 통일할지(tick/player_input 중복 제거) vs 기존 필드 유지 병행할지는 plan에서 확정** — 호환·단순성 트레이드오프.

### 클라 — `PlayerInputManager`
- 최근 N틱(기본 3) 입력을 ring buffer로 보유, 매 전송 시 `recent_inputs`에 담아 송신.
- **전송 채널 unreliable로**: `session.Send`(→ `LOPSession.Send` → `networkConnection.Send`)에 채널 지정 경로 추가. `LOPSession.Send`/`ISession.Send`에 `channelId` 파라미터(기본 reliable 유지, 입력만 unreliable) 또는 입력 전용 송신 메서드. (다른 메시지는 reliable 유지 — StatAllocation 등.)

### 서버 — `Game.Input.MessageHandler` / `EntityInputComponent`
- 핸들러가 `recent_inputs`의 각 엔트리를 `AddInput`으로 투입 → 이미 있는 tick은 `AddInput`의 중복 가드가 무시(dedup). 유실된 tick이 다음 패킷의 redundancy로 채워짐.
- 서버 수신 채널은 transport가 채널 구분해 동일 핸들러로 전달(Mirror) — 핸들러 로직 변경 최소.

### 3b 효과
입력 unreliable → head-of-line blocking 제거(지각 감소). 유실은 redundancy로 무대기 복구 → miss 희귀. 3a 정렬과 합쳐 클·서 tick 정합 극대화 → recon 추가 감소.

## 데이터 흐름 (전환 후)

```
클라(Phase2 tick=server+lead): 입력 생성, 최근 N틱 묶어 unreliable 송신
  → (유실 가능하나) 다음 패킷 redundancy가 채움
서버: inputBuffer에 tick별 보관(중복 무시)
  ProcessInput: GetInput(serverTick) = inputBuffer[serverTick − 2]
    있으면 처리(클라 tick == 서버 tick), 없으면 no-input, 지각분 prune
  → 클·서 같은 tick·입력 → recon 갭 최소
```

## 슬라이스 분해 (구현 순서)

- **3a (서버 repo)**: `EntityInputComponent` 정렬+jitter+miss/지각. 클라/wire 무변경. 저위험·독립. **먼저.**
- **3b (LOP-Shared + 클·서 repo)**: proto redundancy 필드 + 클라 ring buffer·unreliable 송신 + 서버 핸들러 dedup 투입. wire 게이트(proto 먼저). **3a 후.**

> 3b는 입력 unreliable 전환과 redundancy를 **한 슬라이스로 묶음**(unreliable만 먼저면 redundancy 전이라 유실=miss 증가 위험).

## 검증

- **3a**: 서버 컴파일 0 (UnityMCP, **서버 인스턴스 핀** — 사용자 지시 서버 작업). 런타임(LatencySimulation): 정렬 정상(이동/액션), miss 시 과한 끊김 없음, recon 동등/개선, `INPUT_DELAY_TICKS=2`로 입력 적시 처리, 콘솔 에러 0.
- **3b**: LOP-Shared proto 재생성 → 클·서 컴파일 0. 런타임(LatencySimulation에 **packet loss % 주입**): 입력 유실이 화면에 안 보임(redundancy 복구), head-of-line 지연 제거로 recon 추가 개선, 중복 tick 정상 dedup(서버 경고 스팸 없음), 콘솔 에러 0.
- 자동 테스트 없음(클·서 단일 Assembly-CSharp, LOP 글루) — 컴파일+수동. (`EntityInputComponent`가 LOP 측이라 EditMode 불가.)

## GUID / .meta 정책

수정 위주(파일·클래스명 유지) → .meta 영향 없음. wire는 proto 재생성 산출물(LOP-Shared). 삭제 없음.

## Out of scope (defer)

- **Phase 4**: 서버 input-buffer 점유 기반 lead 동적 피드백(predictedTime이 일부 제공), drop count 클라 알림.
- miss 시 반복/decay (측정 후 필요하면).
- 예측/롤백/Snapshot-Restore (Stage④).
- redundancy N 튜닝, 적응형 jitter buffer.

## 문서/브랜치 정책

선례대로 **spec·plan은 클라 repo** 피처 브랜치 `feature/netcode-phase3-input-buffer`. 코드: 3a=서버 repo, 3b=LOP-Shared(proto)+클·서 repo 각 피처 브랜치. 서버 working-tree 로컬 픽스처(`LOPGame`/`ConfigureRoomComponent`) 커밋 금지·stash 보존. 이 spec은 `CLAUDE.md` `@` 자동 로드 목록에 추가.

## 진행

- [x] command-frame 정렬·jitter buffer·miss(no-input)·지각(drop)·입력 unreliable+redundancy 합의 (브레인스토밍 + 리서치: miss 시스템별 검증 + redundancy=UDP 한정 코드/1차자료 검증 + 입력 현재 reliable 확인)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 3a 구현 plan
- [ ] 구현(3a 서버 → 3b Shared+클·서) → 검증 → 머지

> netcode-redesign Phase 3. 이후 Phase 4(lead 동적 피드백)/Phase 5(점프 임펄스)/Stage④. miss=no-input은 redundancy 전제 하의 단순안 — 측정 후 반복/decay 재고 가능.
