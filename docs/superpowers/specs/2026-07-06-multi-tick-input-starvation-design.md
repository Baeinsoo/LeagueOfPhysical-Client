# 멀티틱 프레임 입력 기아 수정 — 이동을 held 상태로 매 틱 샘플

**Date:** 2026-07-06
**Branch:** `fix/multi-tick-input-starvation` (LOP-Client)
**Related:** [netcode-redesign](../../netcode-redesign.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md)

## Goal

한 유니티 프레임에 여러 논리 틱이 도는 경우(멀티틱 프레임), **둘째+ 틱이 이동 입력을 못 받아 brake-to-desired 모터가 수평속도를 0으로 제동**해 걷기가 끊기고 Run 애니가 Idle로 깜빡이는 문제를 없앤다. 이동 입력을 "프레임당 일회성 캡처"에서 **"held(눌린) 상태를 틱마다 샘플"** 로 바꿔, 멀티틱 프레임의 모든 틱이 held 이동을 받도록 한다.

## 배경 — 근본 원인 (커밋 이력으로 확정)

**논리 틱 vs 유니티 프레임:** 시뮬 틱은 고정(30Hz), 유니티 프레임은 가변(60~144fps). 프레임당 틱 수는 0/1/2…로 출렁이고(고정 timestep 누적 + 클럭 dilation으로 클라가 서버보다 앞서 달림), 한 프레임에 여러 틱이 몰릴 수 있다.

**결함:** 입력은 **프레임당 캡처**(`GamePadViewModel.FeedMove` → `PlayerInputManager.captured`), **틱당 소비 후 null**(`ProcessInput`). 이동을 *일회성*으로 다뤄, 한 프레임에 2틱이면 첫 틱이 `captured` 소비→null, **둘째 틱은 null → "무입력" → 모터가 수평속도 0 제동.** 계측(`[INPUT]` 로그)으로 확정: 걷는 중 `frame=2412 tick=3183 hasInput=True` + `frame=2412 tick=3184 hasInput=False`.

**언제 드러났나 (git):**
- 입력 one-shot 캡처 모델은 **원래부터**(a094d27, 2025-05). 멀티틱 기아는 늘 잠복.
- 과거엔 `linearDamping=0.1`(마찰=사실상 coast)라 무입력 틱이 velocity를 유지 → **안 보였음.**
- **`c188c03`(2026-06-28, "정지 모델 전환")** 이 `linearDamping=0` + "무입력 틱에도 movement 호출해 수평 0으로 제동(서버와 동형)"으로 바꾸며 그 잠복 버그가 **하드 깜빡임으로 드러남.**
- 렌더 스무딩 브랜치와는 **무관**(그 브랜치는 입력/모터 파일 미변경 — 스무딩 꺼도 재현됨).

`c188c03`의 의도("서버와 동형" — release 시 클·서 둘 다 제동)는 **옳다.** 버그는 오직 멀티틱의 **가짜 무입력 틱**이다. 이 수정은 그 가짜 무입력 틱에 held 이동을 실어 parity를 유지하면서 깜빡임만 제거한다.

## 산업 표준 매핑 (Photon Quantum / Unity Netcode)

결정론 고정틱 넷코드의 표준은 **입력을 "held 레벨 상태"로 틱마다 샘플**하는 것이다:

- **Photon Quantum** ([Input 문서](https://doc.photonengine.com/quantum/current/manual/input)): 엔진이 `PollInput` 콜백으로 **매 틱 입력을 폴(pull)**. 버튼은 **현재 눌림 상태(level)** 를 폴하고, `GetKeyDown/Up`(엣지)은 *"유니티가 Quantum과 같은 속도로 안 돌기 때문에 문제"* 라 금지 — 엔진이 상태 스트림에서 `WasPressed/IsDown/WasReleased`를 **파생**. 누락 틱은 `Repeatable` 플래그로 직전 입력 반복.
- **Unity Netcode for Entities/GameObjects**: 고정 틱 = `fixedDeltaTime`, 프레임당 다중 틱 처리. 입력은 틱당 command 스트림.

우리 `captured`/`FeedMove` 모델(이동을 프레임당 일회성 push, 틱이 소비)은 정확히 표준이 *"하지 말라"* 는 안티패턴. 표준 = **이동을 held 상태로 틱마다 샘플**(Quantum의 "현재 상태 폴 per tick").

> **범위 구분:** 이번 수정은 **이동(연속 축)만** held-per-tick으로 전환. 점프/어빌리티(이산 엣지)의 Quantum식 "held 상태 + 시뮬 파생 엣지" 전환은 *깜빡임 원인이 아니므로* 이번 범위 밖(후속). 점프의 현재 `wasPressedThisFrame`(엣지)도 같은 잠복 안티패턴이나 별개.

## 설계

### 핵심: 이동=연속(held) / 액션=이산(one-shot) 분리

`PlayerInputManager`가 **held 이동 상태**를 보유하고 매 틱 샘플한다. 점프/어빌리티는 기존대로 일회성.

```
// 연속 — 입력 소스가 매 프레임 갱신(뗄 때 0 포함), 틱마다 샘플(소비 안 함)
private float heldHorizontal;
private float heldVertical;

// 이산 — 소비 후 리셋
private bool pendingJump;
private int pendingAbilityId;
```

### 입력 소스(GamePad) — held 이동을 매 프레임 push (0 포함)

현재 `FeedMove`는 `_moveInput == Vector2.zero`면 early-return이라 **뗄 때 0을 안 민다.** 이걸 바꿔 **매 프레임 현재 이동 벡터(조이스틱/WASD, 없으면 0)를 카메라 상대 변환해 `SetMovement(h, v)` 로 push** 한다.

- `PlayerInputManager.SetMovement(float h, float v)` — held 이동 갱신(매 프레임 호출, idle이면 0).
- `SetJump(bool)` / `SetAbilityId(int)` — 이산 pending 설정(기존 유지).
- 기존 `SetHorizontal/SetVertical`(일회성 captured)는 제거하고 `SetMovement`로 대체.
- 프레임 구동부(`GamePadView`)는 매 프레임 정확히 한 번 현재 이동을 계산해 `SetMovement` 호출(입력 없으면 `SetMovement(0,0)`).

### 틱당 입력 확정 (`ProcessInput`, 매 틱)

```
bool hasMovement = heldHorizontal != 0 || heldVertical != 0;
bool hasAction   = pendingJump || pendingAbilityId != 0;

if (hasMovement || hasAction)
{
    var cmd = new InputCommand {
        Horizontal = heldHorizontal, Vertical = heldVertical,
        Jump = pendingJump, AbilityId = pendingAbilityId,
    };
    // 대시 등 조작 불가 상태: 이동 무시(기존 가드 유지)
    if (AbilitySystem.HasActiveMotionEffect(worldEntity)) { cmd.Horizontal = 0; cmd.Vertical = 0; }

    cmd.SequenceNumber = GenerateSequenceNumber();
    inputBufferSystem.Enqueue(buffer, tick, cmd);
    inputBufferSystem.SetCurrent(buffer, cmd);
    inputBufferSystem.TrimToWindow(buffer, RedundancyWindow);
    SendToServer(buffer, tick, cmd);
    if (cmd.AbilityId != 0) abilityActivator.TryActivate(entityId, cmd.AbilityId, tick);
    inputHistory.Record(tick, cmd);

    pendingJump = false; pendingAbilityId = 0;   // 이산만 소비 — held 이동은 유지
}
else
{
    // idle(held=0, 액션 없음): 기존 무입력 경로 — 로컬 제동 예측 + 기록 + redundancy 재전송
    var noInput = new InputCommand();
    inputBufferSystem.SetCurrent(buffer, noInput);
    inputHistory.Record(tick, noInput);
    if (buffer.Commands.Count > 0) SendToServer(buffer, tick, null);
}
```

**핵심 변화:** 이동이 있으면 **매 틱**(멀티틱 프레임의 둘째 틱 포함) held 이동으로 command를 만들어 enqueue·전송·기록·SetCurrent. 그 틱은 더 이상 "무입력"이 아니다 → 모터가 제동하지 않고 걷는다. `captured=null` 소비는 사라지고 **이산 액션만 소비**된다.

### 서버 — 변경 없음

서버는 입력을 **tick 번호로** 버퍼링·소비한다(`InputCommandToS.Tick`). 클라가 held 이동을 모든 walk 틱에 대해(tick 키로) enqueue·전송하면, 그 틱들이 `buffer.Commands`·redundancy(`RecentInputs`)에 실려 서버 버퍼를 채운다 → 서버도 매 틱 걷는다. `c188c03`의 "서버와 동형"(무입력=제동)은 그대로 — 진짜 release 틱만 무입력이 되어 양쪽 제동. **클라 단독 수정으로 충분.**

## API 변경 요약

- **`PlayerInputManager`**: `heldHorizontal/heldVertical`(연속) + `pendingJump/pendingAbilityId`(이산) 필드. `SetMovement(h,v)` 추가, `SetHorizontal/SetVertical` 제거. `ProcessInput`을 위 틱-샘플 형태로. `captured` 필드/`EnsureCaptured` 제거.
- **`GamePadViewModel`**: `FeedMove`/`TryFeedKeyboardMove`를 "매 프레임 현재 이동을 `SetMovement`로 push(0 포함)"로 정리. `SetJump/SetAbilityId`(→pending) 유지.
- **`GamePadView`**(프레임 구동부): 매 프레임 정확히 한 번 이동을 계산해 push(입력 없으면 0). *구체 배선은 plan에서 확인.*

## 넷코드 일관성 — 검토 포인트

- **seq cadence:** 이동이 있으면 이제 **틱당** seq 증가(전엔 입력-프레임당). reconcile/redundancy는 **tick 키** 기반이라(anchor=snap.tick) 영향 적을 것으로 보이나, 서버 `ExpectedNextSequence`(Game.Info 핸들러)·redundancy dedup이 per-tick seq를 문제없이 받는지 **plan/구현에서 확인**. (서버 레포 밖 — 필요 시 서버 확인 단계.)
- **idle 최적화 유지:** idle 틱은 새 command를 안 만들고 기존 redundancy 재전송 → 대역폭·seq 인플레 방지.

## 검증

- **주 검증 = 플레이 + 진단로그:** 임시 `[INPUT] frame/tick/hasInput` + `[RUN] spd/fast/grounded` 재삽입. RTT 300ms에서 방향키 꾹 누르고 걷기 → **모든 walk 틱이 이동 command를 가짐**(멀티틱 프레임의 둘째 틱도 hasMovement), `[RUN]`에 `IDLE spd=0.000`(가짜 무입력) **소멸**. 위치 멈춤·Idle 깜빡임 육안 소멸. 검증 후 진단로그 제거.
- **단위 테스트(가능하면):** held 이동 보유 + 틱 샘플 + 이산 소비의 순수 상태 로직을 작은 순수 클래스로 뽑아 테스트 가능한지 검토(tdd-first 원칙). 단 `InputCommand`·`InputBufferSystem`·session 결합이 크면 무리하게 옮기지 않고 플레이 검증으로. → plan에서 결정.
- **무회귀:** release 시 즉시 제동(칼정지 유지), 대시 입력락, 점프/어빌리티 1회 발동, redundancy 스트림 연속.

## 영향 파일 (개략 — 세부는 plan)

- **수정(클라):** `PlayerInputManager.cs`(held 모델), `GamePadViewModel.cs`(매 프레임 push), `GamePadView`(프레임 구동), 필요 시 진단로그 임시 재삽입.
- **무변경:** 서버, MovementSystem(모터), 렌더 스무딩(별 브랜치), reconcile 로직.

## Out of Scope

- 점프/어빌리티의 held-상태+시뮬-파생-엣지 전환(Quantum식) — 이번 깜빡임 원인 아님, 후속.
- 서버 입력 처리 변경(현재 tick-키 소비로 충분).
- 렌더 스무딩(별 브랜치 파킹).
- 원격 엔티티 입력(무관).

## Open Questions (plan/구현에서 해소)

- per-tick seq를 서버 `ExpectedNextSequence`·redundancy dedup이 문제없이 수용하는지(서버 확인 필요 시).
- held 이동 순수 상태 로직의 단위 테스트 위치(제자리 테스트 가능 여부).
- `GamePadView` 프레임 구동부의 정확한 push 지점(조이스틱/WASD 우선순위 + idle 0 push).

## 진행

- [x] systematic-debugging: 근본 원인 확정([INPUT] 계측 스모킹건 + git 이력 c188c03)
- [x] 산업 표준 확인(Quantum held-per-tick / Unity 고정틱)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan
