# 시작 구간 클럭 동기화 — 추정이 자리를 잡은 뒤에 출발선을 긋는다

매치 입장 직후 몇 초간 눈에 보이는 러버밴딩("드르륵")을 없앤다. 원인은 **서버 시간 추정이 아직
익지 않은 순간에 시뮬레이션 시계의 출발선을 확정하는 것**이다.

## 문제

### 증상

입장 직후 걸으면 화면이 덜덜거린다. 10초 넘게 지속되다 저절로 사라진다. 가만히 서 있으면
드러나지 않는다.

### 측정된 사실 (2026-08-11, MPPM 클론, 적 0마리 구간)

| | 가만히 | 걷기 |
|---|---|---|
| `reconMax` | 0.000 | **0.640 (64cm)** |
| `corrections` | 20 | 28 |

64cm는 롤백 문턱(0.06)의 10배라 하드 롤백이 걸리고, 그게 러버밴딩으로 보인다.
시작 구간에 갇혀 있다 — 9.7초 `0.640` → 17.0초 `0.640`(7초를 더 걸어도 증가 0).

### 기전

`[ReconSpike]`가 그 순간을 남겼다:

```
tick=212  cur=264  err=0.640  snapAge=52          ← 정상 10~12
predPos=(5.00,0.00,-0.04)   srvPos=(5.00,0.00,-0.68)
predVel=(0.00,0.00,-2.00)   srvVel=(0.00,0.00,-4.00)
timing[dAvg=-43.9 dMax=-43 prune=0]               ← 정상 -2.5
```

**같은 틱 번호가 서로 다른 순간을 가리킨다.** 클라의 틱 212는 "걷기 1틱째"(`predVel 2.00` =
`MaxAcceleration 100 × dt 0.02`)인데 서버의 틱 212는 "0.68m 진행 + 최고속"이다.

### 근본 원인 — 퐁 원본 샘플이 갈랐다

Mirror의 `OnClientPong`에 원본 샘플 계측을 넣어 확인했다:

```
n=1   rawRtt=1052ms   rawOffset=-191.692    ← 오염
n=2   rawRtt= 192ms   rawOffset=-192.540    ← 이미 정상
n=3   rawRtt= 179ms   rawOffset=-192.563
...   (계속 180~200ms, offset −192.55 근처)
```

**첫 샘플 하나만 오염돼 있다.** Mirror는 접속 즉시 첫 핑을 보내는데
(`OnTransportConnected` → `SendPing()`), 그 순간이 인증·씬·스폰으로 가장 바쁘다. Mirror 문서도
RTT가 "순수 네트워크 지연"이 아니라 **처리 지연을 포함한 값**임을 명시한다.

그리고 Mirror의 `ExponentialMovingAverage`는 **첫 샘플을 평균 없이 그대로 채택**한다:

```csharp
if (initialized) { Value += alpha * (newValue - Value); }   // 이후: 9.5%씩
else             { Value = newValue; initialized = true; }  // 첫 샘플: 그대로
```

오염된 값이 기준선이 되고, 그 뒤로는 한 번에 9.5%씩만 교정된다.

**그 상태에서 우리는 퐁 3개째에 출발선을 긋는다:**

```
[ClockSeed] target=3.711 predicted=3.681 pongs=3 gameInfo[tick=139 elapsed=2.788]
```

시드 시점 추정 오차 **0.70초 = 35틱**. 이후 `ClockDilator`가 초당 5%만 교정하므로 복구에 13초가
걸린다(`[ClockTrace]` 실측: error 최대 +420ms @3초 → 0ms @13초).

### 이것은 Mirror의 결함이 아니다

Mirror의 `predictedTime`은 **계속 읽으면 점점 정확해지는 값**이고, 실제로 4~5초면 오차 10ms
이하로 수렴한다. 우리는 그것을 **한 번 읽어 출발선을 확정하는 용도**로 쓴다 — Mirror가 약속한 적
없는 사용법이다. 고정 틱 시뮬레이션은 우리가 얹은 것이므로, "연속 추정값을 언제 한 번의
출발선으로 바꿀지"도 우리 몫이다. 그 층이 비어 있었다.

**반증된 가설(기록):** "매치마다 새 서버 파드라 시차 EWMA가 이전 값에서 기어온다"고 의심했으나,
사용자가 매번 새 플레이 세션으로 측정했고 원본 샘플이 n=2부터 정상이므로 **첫 샘플 오염**이
실제 원인이다. (`_predictionErrorUnadjusted`가 `ResetStatics()`에서 초기화되지 않는 것은 upstream
master에서도 동일하나, 이번 증상의 원인은 아니다.)

## 결정

**추정이 자리를 잡은 뒤에 출발선을 긋는다.** 대기는 이미 떠 있는 로딩 화면 뒤에 숨는다
(`MatchLoadingViewModel`이 "위치=GameRoom && !gameLive"를 로딩으로 보고, 러너가 `Playing`이 될 때
닫는다). 게임 시작 후 13초 러버밴딩이 로딩 4~5초로 바뀐다.

### 검토한 대안

| 방식 | 채택 안 한 이유 |
|---|---|
| **추정이 안정될 때까지 대기** | **채택** |
| 시계 교정 속도 제한(5%)을 초반에만 완화 | 틀린 출발을 인정하고 뒷수습하는 것. 시계가 점프하면 예측 기록이 어긋나 강제 복원이 늘어난다(관측된 `corrections` 20~28의 원인) |
| 수렴 전엔 예측을 끄고 서버 팔로우만 | 증상만 덮고 원인은 남는다 |
| 우리 핑/퐁 메시지를 따로 만든다 | 클라 메인스레드에서 재는 한 오염은 동일하다(`netcode-redesign.md` §9.8). 새 proto·핸들러 비용만 는다 |
| 원본 샘플 중 최소 왕복을 고른다(NTP clock filter) | 대기가 ~0.5초로 짧지만 Mirror 수정이 필요하다. 그리고 **대기 방식의 워스트 케이스가 로그 스케일**이라(아래) 이득이 크지 않다 |
| `NetworkTime.time`(스냅샷 타임라인)으로 갈아타기 | 서버 타임스탬프 기반이라 왕복 오염엔 강하지만, 첫 스냅샷이 늦으면 반대 방향으로 어긋난다(Mirror 주석이 "first snapshot may be a lagging packet ... requires catchup later"라고 명시). 문제는 *무엇을 읽느냐*가 아니라 *언제 읽느냐*다 |

**대기 방식의 워스트 케이스가 얕은 이유:** 평균이 한 번에 9.5%씩 좁히므로 목표 정밀도까지의
샘플 수는 초기 오차에 **로그로만** 비례한다. 오차 0.86초→45샘플(4.5초), 3초→57샘플(5.7초),
10초→69샘플(6.9초). 12배 나빠져도 대기는 1.5배다.

## 설계

### 감시할 값 — drift

```
drift = PredictedTime − 실제로 흐른 시간(Time.unscaledTimeAsDouble)
```

추정이 안정됐다면 이 값은 상수다(예측 시간이 실시간과 같은 속도로 흐른다). 보정 중이면 움직인다.

> **`unscaledTimeAsDouble`이어야 한다.** Mirror의 `NetworkTime.localTime`이 같은 값이라
> (`Time.unscaledTimeAsDouble`), `predictedTime = localTime + 시차`에서 실시간 항이 정확히
> 상쇄돼 drift가 곧 시차가 된다. `Time.timeAsDouble`을 쓰면 `timeScale`에 끌려 상쇄가 깨진다.

**Mirror 내부 구현에 의존하지 않는다.** "예측 시간이 실시간과 나란히 흐르는가"는 어떤 구현에서도
성립하는 성질이라, 나중에 시간 소스를 바꿔도 판정 코드는 그대로 쓴다.

실측 확인:

```
t=0.1  drift = -191.7     ← 움직이는 중
t=2.5  drift = -192.47
t=4.5  drift = -192.55    ← 멈춤 = 안정
```

### 판정 조건

```
최근 0.5초 창에서  max(drift) − min(drift) < 5ms   →  안정

단, 다음 둘을 모두 만족한 뒤에만 판정한다:
  · 창이 시간으로 0.5초 이상 찼다        (가장 오래된 샘플이 0.5초 이전)
  · 창 안 샘플이 5개 이상
```

평균이나 기울기가 아니라 **진폭**을 보는 이유는 한 샘플이 튀어도 창이 지나가면 정리되기 때문이다.

**샘플 수 조건이 따로 필요한 이유:** drift는 매 프레임 읽지만 값은 퐁이 올 때(0.1초)만 갱신된다.
로딩으로 프레임이 5fps까지 떨어지면 0.5초 창에 샘플이 2~3개뿐이라 **진폭이 우연히 작게** 나올 수
있다. 샘플 5개를 함께 요구하면 프레임이 느릴수록 자연히 더 기다린다 — 바쁠수록 더 기다리는 것이
우리가 원하는 동작이다.

**수치 근거(임의값 아님):** Mirror 평균이 샘플당 9.5%(`PredictionErrorWindowSize=20` → α=2/21)씩
좁히므로 0.5초(퐁 5개) 동안의 변화량은 남은 오차의 약 39%다. 따라서

```
창 진폭 < 5ms  ⟺  남은 오차 < 13ms  ( ≈ 0.65틱 )
```

즉 **조건을 만족하면 시드 오차가 1틱 미만**이다. 현재는 35틱이다.

> ⚠️ 이 대응 관계는 Mirror의 `PredictionErrorWindowSize`와 `PingInterval`에 의존한다. Mirror
> 업그레이드로 그 값이 바뀌면 임계값을 재유도해야 한다. 코드 주석에 근거를 남긴다.

### 타임아웃

**7초.** 실측 안정화가 4.5초라 5초는 여유가 얇다. 안정되면 즉시 빠져나오므로 값이 커도 정상
케이스에 손해가 없다. 초과 시 **경고 로그 + 현재 값으로 그대로 진행** — 무한 대기는 없다.

폴백은 현재 동작보다 항상 같거나 낫다(지금은 조건 없이 퐁 3개째 값을 쓴다).

### 배치 — 대기를 gameInfo 왕복과 겹친다

```csharp
await ConnectRoomServerAsync();           // Mirror 접속 = 핑 시작
var settle = WaitForClockSettleAsync();   // 시작만 해둔다 (await 안 함)
await JoinRoomServerAsync();              // gameInfo 왕복이 그동안 진행
await settle;                             // 시드 직전에 합류
await StartGameAsync();                   // 여기서 출발선
```

순차로 두면 겹치지 않아 오히려 늦다. **시작 시점을 접속 직후로 앞당기고 합류 지점만 시드
직전에 둔다.**

### 중단 처리

- 대기 루프는 `destroyCancellationToken`에 묶는다 — 씬/오브젝트가 사라지면 함께 끝난다.
  (`await settle` 하기 전에 `JoinRoomServerAsync()`가 던져도 대기가 유령으로 남지 않는다.)
- 대기 중 **연결이 끊기면 즉시 빠져나온다**(`NetworkClient.ready == false`). 끊김 자체의 처리는
  기존 `LOPRoom` 흐름 소관이며, 여기서는 7초를 헛되이 기다리지 않게만 한다.

### 코드 위치

`LOPRoom`의 **private 메서드 하나**로 둔다. 새 클래스도, GameFramework 추가도 없다.

```csharp
private async Task WaitForClockSettleAsync()
```

**왜 별도 클래스로 빼지 않나:** 로직이 "큐의 max−min 비교"가 전부이고, 이 관심사는 매치 입장
1회에만 쓰인다. 조건이 복잡해지면(프레임 안정성까지 본다든가) 그때 `GameFramework.Netcode`로
빼면 된다 — 그 자리에 `ClockDilator`·`LeadController` 등 같은 성격의 순수 로직이 이미 있다.

**포기하는 것:** `Assets/`의 앱 코드라 asmdef가 없어 **유닛 테스트를 붙일 수 없다.** 이는
`[[tdd-first-always]]`가 경계한 형태이므로 의식적 선택임을 밝힌다 — 이 로직은 틀리면 "영원히
기다린다" 아니면 "안 기다린다" 둘 중 하나로 즉시 드러나서 조용히 잘못될 여지가 작다. 조건이
자라면 패키지로 옮겨 EditMode TDD로 전환한다.

### 계측 (영구)

```
[ClockSettle] settled elapsed=4.32s samples=44 amplitude=0.004 drift=-192.55
[ClockSettle] TIMEOUT elapsed=7.00s amplitude=0.081 drift=-192.31     ← 경고
```

**안정까지 걸린 시간을 항상 남긴다.** 다른 환경(느린 폰, 실제 네트워크)에서 대기가 길어지면
추측이 아니라 로그로 판단해 고도화 여부를 정한다. 이번에 13초를 헤맨 이유가 이 로그의 부재였다.

## 검증

**유닛 테스트 없음**(위 "포기하는 것"). 라이브 측정으로 판정한다.

| 볼 것 | 통과 기준 |
|---|---|
| `[ClockSettle]` | `settled`, elapsed가 타임아웃 미만 |
| `reconMax` (입장 직후 걷기, `entities=2`) | **0.06 미만** (현재 0.48~0.64) |
| `corrections` | 크게 감소 (현재 20~28) |
| `[ClockTrace]`의 `error` | 최대치가 100ms 미만 (현재 420ms) |
| 체감 | 입장 직후 "드르륵" 소멸 |

**측정 절차**(`[[local-two-editor-noise-floor]]`):
MPPM 클론에서 · Reset 누르지 말 것 · 입장하자마자 바로 걷기 · **~8초에 Dump**
(`entities=2`면 적 넉백 오염 없음이 증명된다 — 첫 스폰이 t=10초) ·
로그는 `Library/VP/<clone>/Logs/Editor.log`.

## 진단 코드 정리

현재 `main`에 임시 계측이 세 덩이 있다. 검증이 끝나면:

| 위치 | 처리 |
|---|---|
| `Assets/Mirror/Core/NetworkTime.cs` (`[PongSample]`, `LopPongCount`) | **반드시 되돌린다** — 벤더 코드 |
| `LOPRoom` `[ClockSeed]` | `[ClockSettle]`로 대체 |
| `LOPTickUpdater` `[ClockTrace]` | 검증에 쓰고 제거 |

## 산업 표준 매핑

- **NTP**: 초기 동기에서 표본을 모아 시계를 확정한 뒤 사용을 시작한다. "덜 익은 추정으로 먼저
  출발하고 나중에 고친다"가 아니라 **"확정 후 출발"** 이 표준이다.
- **Mirror**: `predictedTime`은 연속 추정치(self-healing)로 설계됐고 문서도 그렇게 쓴다. 한 번의
  스냅샷 값으로 쓰는 계약은 없다.
- **제어/계측**: 조건이 곧 **settling time**(정착 시간) 판정이며, 창 내 진폭이 임계 미만인지로
  보는 것이 통상적이다.

## 상태

설계 확정. 구현은 `LOPRoom` 한 파일 + 진단 코드 정리.
