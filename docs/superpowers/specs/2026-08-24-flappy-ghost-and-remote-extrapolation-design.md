# Flappy Race — 유령정지 + 원격 외삽 설계

> 실플레이에서 나온 세 증상을 고친다. 셋은 서로 다른 버그가 아니라 **두 뿌리**에서 갈라져 나온다.

## 1. 무엇을 고치나

| 증상 (2026-08-24 실플레이 제보) | 뿌리 |
|---|---|
| 남의 새가 부드럽지 않고 **순간이동**한다 | 원격을 시뮬로 굴리는데 남의 플랩을 모른다 |
| 어딘가에 **끼이면 카메라가 진동**한다 | 맵이 솔리드하게 막는데 전진 속도가 고정이다 |
| **스폰에서 갇히고** 플랩 연타로 풀린다 | 위와 같음 |

| 넣는 것 | 빼는 것 (이번 범위 밖) |
|---|---|
| 유령정지 페널티 (맵 충돌 규칙 교체) | 결승선·순위·종료 판정 |
| 원격 외삽 (`Extrapolated` 동기화 모드) | 추격자, 대시, 부스트존 |
| 내 새만 원격에 부딪히는 로컬 몸싸움 | 유령 상태의 화려한 연출(반투명 정도만) |

---

## 2. 왜 이렇게 됐나 — 원인 확정

### 2-1. 원격이 순간이동하는 이유

2026-08-23 슬라이스에서 Flappy는 원격도 `Predicted`로 바꿨다(새끼리 몸싸움을 로컬에서 풀려고).
그런데 **원격 플레이어의 입력은 클라로 오지 않는다.** 클라가 받는 메시지에 `InputCommand`가 없다:

```
GameInfoToC / EntitySnapsToC / EntitySpawnToC / EntityDespawnToC /
UserEntitySnapToC / WorldEventBatchToC / StatAllocationToC / InputTimingToC / MatchEndedToC
```

그리고 시뮬은 플랩을 이렇게 읽는다 (`FlappyMoveSystem.cs:37`):

```csharp
var input = entity.Get<InputBuffer>()?.Current;   // 원격은 항상 비어 있다
...
velocity.y = config.FlapImpulse;                  // → 원격은 클라에서 절대 날갯짓하지 않는다
```

**결과**: 클라의 예측 속 남의 새는 중력만 받아 추락한다. 서버 스냅이 오면 실제로는 위에 있으니
차이가 크고, 렌더 스무더의 텔레포트 임계 **3m**(`RenderCorrectionSmoother(0.1f, 0.025f, 3f)`)를
넘겨 부드럽게 흡수하지 않고 **즉시 스냅**한다. 그게 순간이동으로 보인다.

> **어제 검증이 이걸 놓친 이유**: 접촉 순간의 *좌표 일치*만 확인했고, 가만히 보고 있을 때의
> *매끄러움*을 보지 않았다. 좌표가 맞는 것과 궤적이 매끄러운 것은 다른 성질이다.

### 2-2. 끼임·카메라 진동의 이유

`FlappyWorld.MoveThroughMap`은 매 틱 `_motionBridge.Depenetrate(entity)`로 밀어낸 뒤
캡슐 sweep으로 **막힌 지점까지만** 이동시킨다. 그런데 Flappy는 **전진 속도가 고정**이다
(`ForwardSpeed`, 플레이어가 바꿀 수 없음).

- 벽에 박히면 밀어냄 ↔ 전진이 매 틱 서로 밀어 **위치가 진동** → 카메라가 그걸 따라가 떤다
- 수평으로는 **영구히 못 빠져나온다** → 플랩(수직 속도)만이 탈출 수단
- 스폰 지점이 기하와 겹치면 시작부터 갇힌다

즉 끼임·진동·스폰갇힘 셋은 전부 **"솔리드하게 막는다"**에서 나온다.

---

## 3. 산업 표준 확인 (2026-08-24 리서치)

이 설계의 두 축 모두 1차 자료로 근거를 확인했다.

### 3-1. "다른 시간선" 문제는 이름이 붙어 있다

로켓리그 GDC 2018(Jared Cone) 발표 원문:

> **Hit Moving Object?** — Client predicts his vehicle / Server authoritative ball /
> Client interpolates ball / **different timelines**

### 3-2. 입력을 모르는 상대를 예측하는 것은 *선택한 게임조차* 약점으로 명시한다

같은 발표의 `Predict Everything` 슬라이드:

> Client drives to where the ball is going to be / Works well with ball (**predictable**) /
> **Not as well with cars (unpredictable)** / No server-side lag compensation /
> **Expensive corrections** — 200ms ping, 120hz = 24 correction frames

로켓리그는 "전부 예측"을 택했으면서도 *입력을 모르는 남*은 잘 안 된다고 적었다.
**우리 새는 자동차보다 나쁘다** — 플랩은 연속 조향이 아니라 **불연속 충격량**이라
한 번 놓치면 궤적이 즉시 갈라진다.

### 3-3. "원격을 로컬 시각으로 외삽해 상호작용시킨다"는 상용 기능이다

Photon Fusion 2.1 **Forecast Physics**:

> extrapolates the position received over the network to **place physics objects in the local
> time of all players** … enables **smooth interaction on all clients** … an alternative to
> **fully simulating physics predictions**, at significantly lower CPU cost

**§4-2(외삽)와 §4-3(로컬 몸싸움)은 별개의 두 선택이 아니라 한 묶음이다** — 외삽하는 목적 자체가
로컬 상호작용을 성립시키는 것이다. 떼어 놓으면 외삽의 값어치가 절반이 된다.

> 참고: **카트라이더 자체의 넷코드는 1차 자료가 없다.** "카트라이더 느낌"은
> *부딪히면 내 카트가 즉시 반응하고 남은 매끄럽게 흐른다*로 해석해 설계했다.

**출처**
- [It IS Rocket Science! — Jared Cone, GDC 2018 (PDF)](https://media.gdcvault.com/gdc2018/presentations/Cone_Jared_It_Is_Rocket.pdf)
- [Fusion 2 — Physics (Forecast Physics)](https://doc.photonengine.com/fusion/current/manual/physics)
- [Fusion 2.1 Stable Release](https://blog.photonengine.com/fusion-2-1-stable-release/)
- [Gambetta — Entity Interpolation](https://www.gabrielgambetta.com/entity-interpolation.html)

---

## 4. 설계

### 4-1. 맵 충돌 = 유령정지 (막기 → 통과 + 페널티)

프로토타입에 규칙이 남아 있다 (`FlappyAutoPilot.cs:17-18, 98, 152`):

```csharp
public float ghostTime  = 0.8f;   // 충돌 시 그 자리 정지
public float invulnTime = 0.6f;   // 풀린 뒤 잠깐 무적(연속 충돌 방지)
```

**바꾸는 것** — `FlappyWorld.MoveThroughMap`:

```
지금:  Depenetrate → sweep으로 막힌 지점까지만 이동
바꿈:  sweep은 "닿았나?"만 판정 → 이동은 그대로 통과
       닿았고 무적이 아니면 → 유령 진입(정지 0.8초 → 무적 0.6초)
```

- **`Depenetrate` 호출이 사라진다** → 위치 진동 소멸 → 카메라 떨림 소멸
- 벽에 박힐 수가 없다 → 끼임·스폰갇힘 소멸
- **맵 콜라이더는 솔리드 그대로 둔다.** `ICollisionQuery.CapsuleCast`가
  `QueryTriggerInteraction.Ignore`로 트리거를 걸러내므로, 트리거로 되돌리면 감지가 아예 안 된다.
  아트 서브모듈 씬 수술이 필요 없다.

**잠긴 결정 — 유령 중에는 완전 정지한다.** 전진도 멈춘다(프로토타입 그대로). 0.8초의 시간 손실이
페널티 그 자체다. "전진은 유지하고 조작만 불가"는 레이스가 멈춰 보이지 않는 대신 페널티가
흐려져서 택하지 않는다.

**새 컴포넌트** (LOP-Shared, Anemic):

```csharp
public class FlappyGhost : GameFramework.World.Component
{
    public float Remaining;        // 정지 남은 시간(0이면 정상)
    public float InvulnRemaining;  // 무적 남은 시간
}
```

**튜닝값** — `FlappyConfig`에 `GhostTime`, `InvulnTime` 추가. 출처는 마스터데이터
`TbFlappyConfig`이므로 **infrastructure(Excel+Luban) → MasterData-Client/Server 두 패키지**가
함께 움직인다. 초기값은 프로토타입과 같은 `0.8` / `0.6`.

**⚠️ 딸려오는 것 — 되감기.** 유령 타이머는 롤백 때 되감겨야 한다. 지금 `FlappyWorld`는
`WorldBase`의 저장/복원 훅을 오버라이드하지 않는다(위치·속도만으로 충분했다).
**이번에 `SaveGameState`/`LoadGameState`를 처음 구현한다** — `netcode-redesign.md` §6.5가
정한 그 자리다. 저장 대상은 `FlappyGhost`의 두 타이머.

### 4-2. 원격 = 외삽 (`Extrapolated` 모드 신설)

```
지금:  원격에 Simulated → 시뮬이 굴림(플랩을 몰라 계속 추락) → 3m 넘으면 텔레포트
바꿈:  원격에서 Simulated 제거 → 시뮬이 굴리지 않음
       마지막 스냅(위치+속도)에서 렌더 시각까지 이어 그림
```

- `EntitySyncMode`에 **`Extrapolated`** 추가 (기존 `Interpolated` / `Predicted` 옆)
- `CharactersPredictedSyncPolicy` → **`OwnerPredictedRemotesExtrapolatedSyncPolicy`** 로 교체
  (내 새 = `Predicted`, 남 = `Extrapolated`). FlapWang의 `OwnerPredictedSyncPolicy`는 그대로.
- **중력을 외삽에 포함한다(포물선 외삽).** 중력은 우리가 아는 값이라, 모르는 것이 "플랩했는가"
  하나로 줄어 오차가 크게 준다. 단순 등속 외삽보다 정확하다.
- **외삽 상한 0.25초.** 넘으면 마지막 위치에 세운다(freeze). 값의 근거는 Source의
  `cl_extrapolate_amount` 기본값 0.25s — 이 바닥의 사실상 표준이다.
- 새 스냅이 오면 **끊지 않고 섞는다**(현재 외삽 위치 → 새 기준으로 짧게 블렌드).

**뷰 컴포넌트**: `SnapshotEntityInterpolator`(과거 보간) 옆에
`ExtrapolatedEntityInterpolator`(로컬 시각 외삽)를 새로 둔다. `EntityBinder`가 모드로 고른다.

**계산 커널은 GameFramework에 둔다.** `GameFramework.Netcode`에 이미 짝이 되는 순수 커널들이
산다 — `SnapshotInterpolation.Solve`(브래킷 탐색), `Hermite`/`HermiteVelocity`(속도 인지 보간).
외삽도 게임 무관 산수이므로 같은 자리에 `SnapshotExtrapolation`으로 둔다. 중력은 인자로 받으므로
(게임값은 `FlappyConfig.Gravity`) 커널 자체는 도메인을 모른다. 클라 뷰 컴포넌트는 그 커널을
부르는 얇은 껍데기다.

### 4-3. 몸싸움 = 내 새만 로컬 판정

원격이 시뮬에 없으므로 `FlappyBodyCollisionSystem`은 클라에서 내 새 하나만 보게 된다.
그러면 어제 만든 로컬 몸싸움이 사라지므로:

**내 새는 원격의 *외삽된 위치*에 부딪혀 밀린다.** 원격은 그 반작용을 로컬에서 받지 않는다
(서버가 정한다). 즉 **한쪽만 로컬**이고, 이것이 §3-3의 Forecast Physics가 파는 그 모양이다.

- 서버는 지금처럼 양쪽 다 푼다(권위 불변)
- 클라는 "내가 밀리는 것"만 즉시 보여 준다
- 어긋나면 내 새가 보정을 받는데, 그 크기를 줄이는 것이 아래 안전장치다

### 4-4. 안전장치 — 로켓리그가 짚은 "expensive corrections"

| 장치 | 왜 |
|---|---|
| 외삽 창 0.25초 상한 | 오차 누적을 끊는다 |
| 보정 스무딩 임계 재조정 | 지금 텔레포트 임계 3m는 *원격을 시뮬로 굴리던* 시절 값이다. 외삽으로 바뀌면 오차가 작아지므로 임계도 촘촘히 내린다 — **실측 후 결정** |
| 유령정지(§4-1)가 위험을 줄인다 | 맵이 더는 막지 않으니 클라의 로컬 충돌은 **새끼리 하나만** 남는다 |

### 4-5. 스폰과 카메라

- **스폰 갇힘**: 유령정지로 자연 해소된다(겹쳐도 통과하며 페널티 한 번). 스폰 지점 검증 로직을
  따로 만들지 않는다 — 증상이 사라지는데 규칙을 하나 더 만들 이유가 없다.
- **카메라 진동**: 위치 진동의 *결과*라 원인이 사라지면 함께 사라진다. 카메라 스무딩을 이번에
  넣지 않는다(YAGNI). 유령정지 후에도 떨리면 그때 별도로 본다.

---

## 5. 유령 상태를 남에게 보이기

**잠긴 결정 — 보여준다.** 남이 이유 없이 0.8초 멈추면 버그로 느껴진다.

- `EntitySnap.proto`에 필드 추가 (`bool ghost = 12;` — 다음 번호)
- 서버가 스냅을 만들 때 `FlappyGhost.Remaining > 0`을 실어 보낸다
- 클라는 그 값으로 **반투명 렌더**만 한다 (프로토타입의 `Color(0.6, 0.6, 0.7, 0.7)` 정도)

**왜 이벤트가 아니라 스냅인가**: `world-core-connection-architecture.md`의 기준 —
"잃으면 영구 desync 되나?"에 해당한다. 유령은 0.8초 지속되는 *상태*라 한 번 놓치면 그동안
계속 틀리게 보인다. 연출용 일회성 사건이 아니다.

> 남의 새는 §4-2에서 시뮬이 굴리지 않으므로, 클라가 유령 타이머를 스스로 셀 수 없다.
> 스냅으로 받는 것이 유일한 경로이기도 하다.

---

## 6. 건드리는 곳

| 저장소 | 무엇 |
|---|---|
| **infrastructure** | `TbFlappyConfig`에 `ghost_time`/`invuln_time` 추가 → Luban 재생성 |
| **MasterData-Client / -Server** | 위 생성물 반영(자동) |
| **LOP-Shared** | `FlappyGhost` 컴포넌트, `FlappyConfig` 필드 2개, `FlappyWorld` 충돌 규칙 + `SaveGameState`/`LoadGameState`, `EntitySnap.proto` 필드 |
| **Client** | `EntitySyncMode.Extrapolated`, 정책 교체, `ExtrapolatedEntityInterpolator`, `EntityBinder` 분기, 유령 반투명 렌더, 로컬 몸싸움(§4-3), 스무딩 임계 |
| **Server** | 스냅에 유령 싣기, `FlappyConfigProvider`에 새 두 값 |
| **GameFramework** | `Netcode/SnapshotExtrapolation` 순수 커널 (기존 `SnapshotInterpolation`/`Hermite`의 짝) |

**FlapWang 무영향**: 정책이 `OwnerPredictedSyncPolicy` 그대로고, 유령정지는 `FlappyWorld`
안에서만 산다. 회귀 확인은 FlapWang 한 판으로 갈음한다.

---

## 7. 슬라이스 순서

```
G1  유령정지        충돌 규칙 교체 + 롤백 저장/복원 + 마스터데이터 두 값   → 끼임·진동·스폰갇힘 소멸
G2  유령 보이기      스냅 필드 + 반투명 렌더                              → 남이 왜 멈췄는지 보인다
G3  원격 외삽        Extrapolated 모드 + 뷰 + 정책 교체                   → 순간이동 소멸
G4  로컬 몸싸움      내 새만 원격 외삽 위치에 부딪힘 + 스무딩 임계 재조정   → 부딪힘이 즉시 보인다
```

**G1을 먼저 두는 이유**: 끼임이 남아 있으면 G3의 외삽이 매끄러운지 판단할 수 없다
(끼여서 튀는 건지 외삽이 틀린 건지 구분 불가). 또 G1만으로도 지금 제보된 증상 셋 중 둘이 사라진다.

**G4를 마지막에 두는 이유**: 로켓리그가 짚은 "expensive corrections"가 여기서 나올 수 있다.
G3까지 끝낸 상태를 기준선으로 두면 "내 새가 튀기 시작한 것이 G4 탓"임을 바로 가릴 수 있다.

---

## 8. 테스트와 검증

### 단위 (EditMode)

| 대상 | 확인 |
|---|---|
| 유령 진입/해제 | 닿으면 `Remaining=GhostTime`, 무적 중엔 재진입 안 함, 정지 중 속도 0 |
| 유령 타이머 | 틱마다 감소, 0에서 무적으로 넘어감 |
| 저장/복원 | `SaveState`→진행→`LoadState`로 타이머가 그 틱 값으로 돌아옴 |
| 외삽 커널 | 중력 포함 포물선, 0.25초 상한에서 멈춤, 새 스냅에 블렌드 |

외삽 커널은 **순수 함수로 뽑아** 테스트한다(입력: 마지막 스냅 + 경과시간 → 위치).

### 런타임 (2인, `unity` CLI로 양쪽 구동)

| 슬라이스 | 대표 확인 |
|---|---|
| G1 | 파이프에 부딪혀도 **끼이지 않는다**. 카메라가 떨리지 않는다. 스폰에서 갇히지 않는다 |
| G2 | 남이 멈출 때 **반투명으로 보인다** |
| G3 | 남의 새가 **순간이동하지 않는다** — Recon HUD가 아니라 **육안**이 기준이다 |
| G4 | 남과 부딪히면 **내 새가 즉시 밀린다**. 밀린 뒤 내 새가 크게 튀지 않는다 |

> **⚠️ 어제의 교훈을 검증 항목으로 박는다.** 좌표 일치만 보고 넘어가지 않는다 —
> G3·G4는 **가만히 지켜보며 매끄러운지**를 반드시 육안으로 본다. 수치가 맞는 것과
> 눈에 매끄러운 것은 다른 성질이고, 이번 버그가 정확히 그 틈에서 나왔다.

---

## 9. 열린 결정

- [ ] **보정 스무딩 임계값** — 지금 `(tau 0.1, min 0.025, teleport 3.0)`은 원격을 시뮬로 굴리던
      시절 값이다. G3 실측 뒤 결정한다.
- [ ] **유령 중 몸싸움** — 유령인 새를 남이 밀 수 있나? 지금 설계는 "정지"이므로 밀리지 않는 쪽이
      자연스럽지만, 겹친 채 굳어 보일 수 있다. G4에서 실제로 겹쳐 보고 정한다.
- [ ] **유령 진입 판정을 클라가 예측하나** — 내 새의 유령 진입은 로컬에서 즉시 보여 주는 편이
      반응이 좋지만, 틀리면 0.8초 정지를 잘못 보여 준다. G1은 서버 권위로만 시작하고 필요하면 뒤에 붙인다.
