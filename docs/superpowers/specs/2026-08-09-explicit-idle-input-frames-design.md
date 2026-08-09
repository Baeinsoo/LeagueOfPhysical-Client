# 무입력도 명시적으로 보낸다 — 연속 커맨드 프레임

> 표준 입력 파이프라인 3층 중 **2층의 구조적 결손**을 메운다. 3층(미스 시 마지막 입력 반복)의
> **전제**이며, 그것 없이는 3층을 지을 수 없다.

## 1. 문제 — 침묵이 두 가지 뜻이다

클라는 **입력이 있을 때만** 커맨드를 스트림에 넣는다(`PlayerInputManager.cs`). 무입력 틱에는
로컬 예측용으로 0 커맨드를 쓰고, 전송은 *이전* 윈도우를 재송출할 뿐 그 틱의 프레임을 만들지 않는다.

그래서 서버가 틱 T에서 보는 "빈칸"이 두 뜻이 된다:

| 실제 | 서버가 보는 것 |
|---|---|
| 플레이어가 아무것도 안 눌렀다 | 빈칸 |
| 눌렀는데 유실/지각됐다 | 빈칸 |

서버는 둘 다 **제동**으로 처리한다(`ServerInputSystem`: `SetCurrent(new InputCommand())`).
무입력에는 맞고, 유실에는 틀리다 — 그 틀림이 08-06에 규명한 4cm의 직접 원인이다.

> **지금은 미스 경로가 곧 정상 경로다.** 가만히 서 있으면 서버는 매 틱 `input == null`로 떨어진다.
> 속도가 이미 0이라 눈에 안 보일 뿐, 코드 상으로 무입력과 유실이 같은 길을 탄다.

## 2. 해법 — 빈칸을 없앤다

표준 command-frame 모델은 **매 틱 자기 틱 번호를 단 프레임**을 보낸다. 무입력은 침묵이 아니라
`{h:0, v:0}` **메시지**다. 그러면 뜻이 하나로 고정된다.

| 서버가 보는 것 | 뜻 | 대응 |
|---|---|---|
| `{0,0}` 도착 | 손 뗐다 | 제동 (맞음) |
| 빈칸 | 유실/지각 | 3층에서 마지막 입력 이어 쓰기 (**다음 슬라이스**) |

**이 슬라이스는 서버 동작을 바꾸지 않는다.** 미스 시 제동은 그대로 둔다 — 뜻을 명확히 하는 것과
대응을 바꾸는 것을 한 슬라이스에 섞지 않는다.

### 부수 효과 — 계측과 중복 전송이 제대로 작동한다

- **시퀀스가 진짜 연속**이 된다. 지금은 입력 있는 틱만 seq를 뽑아 `seqGap`이 손실 탐지기 노릇을
  못 한다. 매 틱 seq가 붙으면 구멍이 곧 유실이다.
- **중복 전송 윈도우가 최근 3틱을 담는다.** 지금은 무입력 구간에서 *이미 소비된 낡은 사본*을
  반복 재전송한다(수신 측이 dedup으로 버림 = 순수 낭비). 바뀌면 윈도우가 실제 슬라이딩한다.

## 3. 바꾸는 것

### 3.1 클라 — 분기를 없앤다 (`PlayerInputManager.Tick`)

입력 유무로 갈리던 if/else가 사라지고 **한 경로**가 된다. 값이 0인 것과 프레임이 없는 것은 다르다.

```csharp
var command = new InputCommand { Horizontal = …, Vertical = …, Jump = …, AbilityId = … };
if (AbilitySystem.HasActiveMotionEffect(worldEntity)) { command.Horizontal = 0f; command.Vertical = 0f; }
command.SequenceNumber = GenerateSequenceNumber();

inputBufferSystem.Enqueue(buffer, tick, command);
inputBufferSystem.SetCurrent(buffer, command);
inputBufferSystem.TrimToWindow(buffer, RedundancyWindow);
SendToServer(buffer, tick, command);
```

`SendToServer`의 `current == null` 분기도 없어진다(항상 있다).

### 3.2 서버 — 죽은 `InputSequenceToC` 송신 제거

클라 핸들러가 **빈 몸통**이다(`GameInputMessageHandler`, "틱 기반 하드 복원으로 앵커가 필요
없어졌다"). 지금은 입력 있는 틱에만 나가지만 ② 이후엔 **초당 50개**가 된다 — reliable 채널이라
head-of-line blocking 위험까지 있다. 유일한 생산자를 지우므로 소비자도 함께 지운다.

**proto 메시지 정의는 남긴다.** 지우면 `MessageIds`가 밀려 와이어가 깨진다(알려진 함정).
정리는 proto 전용 슬라이스에서.

## 4. 바뀌지 않는 것 (회귀 확인용)

| | 이유 |
|---|---|
| 서버 이동 결과 | 무입력 틱에 `Current`가 "0 커맨드"인 건 전후 동일 |
| 재생(`Reconciler`) | `inputHistory`를 **틱**으로 조회한다 — seq 무관 |
| 중복 dedup | `Enqueue`가 `seq <= LastProcessedSequence`로 거른다 — 매 틱 seq여도 동일 |
| prune 의미 | 여전히 "지각해서 폐기" |
| 입력 지연 | 보내는 *내용*만 바뀐다 |

## 5. 비용

패킷 수는 **그대로**다 — 무입력 틱에도 이미 매 틱 보내고 있다(Phase 3c). 패킷 크기도 사실상
동일하다(이미 커맨드 3개를 싣는다). 오히려 `InputSequenceToC` 제거로 reliable 트래픽이 준다.

## 6. 검증

| 대상 | 방법 |
|---|---|
| `InputBufferSystem` | **변경 없음.** LOP-Shared EditMode 테스트가 이미 덮는다 |
| 클라 입력 경로 | Assembly-CSharp라 EditMode 불가 |
| 실동작 | 플레이 — 걷기/정지/점프/대시가 **전과 똑같이** 느껴져야 한다(동작 무변경 슬라이스) |
| 계측 | `seqGapTot`이 이제 진짜 유실만 센다. 정지 상태로 오래 두어도 증가하지 않아야 한다 |
| 컴파일 | 클·서 양쪽 콘솔 에러 0 |

**성공 기준: 체감·동작이 그대로이면서, 서버가 무입력 틱에 더는 미스 경로로 떨어지지 않는다.**

## 7. 산업 표준 매핑

- **매 틱 command frame 전송** = Overwatch command frame 모델, Unity Netcode for Entities
  `ICommandData`(틱마다 하나), Quantum `PollInput`(틱마다 입력 요구). 무입력도 *값이 0인 입력*이지
  입력의 부재가 아니다.
- **입력 부재 = 유실 신호** = 위 시스템들이 입력 예측(마지막 입력 반복)을 켜는 조건.

## 8. 다음 (범위 밖)

| | |
|---|---|
| **3층 — 미스 시 마지막 입력 반복** | 이 슬라이스가 그 전제를 놓는다. 이산 액션(점프·어빌리티)은 반복 금지, 반복은 상한 후 중립 |
| `InputSequenceToC` proto 정의 제거 | MessageIds 재생성 슬라이스에서 |
| lead 정책 검증 | 실환경 측정 대기(로컬 잡음 바닥 5틱) |
