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

### 서버 동작은 오늘 하나도 안 바뀐다

| 손 뗀 틱 | 전 | 후 |
|---|---|---|
| 서버가 확정하는 값 | 커맨드 없음 → `new InputCommand()` | `{0,0}` 도착 → 그대로 |
| 결과 | 수평 제동 | 수평 제동 — **동일** |

바뀌는 건 **정보**뿐이다: `input == null`의 뜻이 하나가 된다. 그 정보를 소비하는 건 ①이므로,
**①을 안 지으면 이 슬라이스는 사실상 값이 없다.** 둘은 한 덩어리다.

> **정정.** 초안에서 "지금은 seq가 연속이 아니라 `seqGap`이 손실 탐지기 노릇을 못 한다"고 썼는데
> 틀렸다. 예전에도 *보낸* 커맨드에만 seq가 붙었으므로 "보냈는데 안 온 것"은 그대로 구멍으로
> 잡혔고, 무입력 틱은 seq를 안 써서 가짜 구멍도 안 만들었다. 이미 맞게 동작하고 있었다.

### 부수 효과 — 중복 전송 윈도우가 의미를 갖는다

지금은 무입력 구간에서 *이미 소비된 낡은 사본*을 계속 재전송한다(수신 측이 dedup으로 버림 =
순수 낭비). 바뀌면 윈도우가 실제로 최근 3틱을 담고 슬라이딩한다.

### ①까지 켠 뒤 생길 새 고려사항 — "손 뗌"이 유실되면

`{0,0}`과 그 복구 사본이 전부 유실되면 서버가 마지막 이동 입력을 이어 써서 캐릭터가 잠깐 더 간다.
역설적이지만 지금은 그 경우가 *우연히* 맞게 처리된다(무조건 제동이라).

다만 한정적이다 — 20ms 뒤 다음 틱의 `{0,0}`이 도착하면 멈추므로 1~2틱짜리 오차이고, 클라 롤백이
잡는다. 표준이 감수하는 거래다.

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

### 3.3 클라 — 서버가 안 읽는 필드를 안 채운다

서버 수신 핸들러는 **`RecentInputs`만** 읽는다:

```csharp
foreach (var entry in inputCommandToS.RecentInputs)
{
    inputBufferSystem.Enqueue(buffer, entry.Tick, ToInputCommand(entry.InputCommand));
}
```

패킷 최상위의 `input_command`·`entity_transform`은 **참조 0건**이다(grep 확인). 특히
`entity_transform`은 *클라가 보고한 위치*라 서버가 쓰면 안 되는 값이다 —
`netcode-redesign.md` §6.4가 금지한다.

3.1로 모든 틱이 전송되면서 이 둘도 매 틱 실리게 됐으므로(무입력 틱당 ~50바이트, 초당 ~3KB/클라)
**채우지 않는다.** proto3는 unset 필드를 직렬화하지 않으므로 필드 정의를 건드릴 필요가 없고,
따라서 재생성 위험도 없다. 정의 삭제는 proto 정리 슬라이스에서.

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
