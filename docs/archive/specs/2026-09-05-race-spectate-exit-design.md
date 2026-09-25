# 관전·나가기 UI 설계

Flappy Race에서 **완주했거나 탈락한 뒤** 할 수 있는 일을 만든다. 지금은 문구만 뜨고
아무것도 못 한다 — 보고 싶은 사람을 고를 수도, 판을 떠날 수도 없다.

- 관련: [2026-09-04-race-finish-design.md](2026-09-04-race-finish-design.md) (완주 화면),
  [2026-09-03-flappy-chaser-design.md](2026-09-03-flappy-chaser-design.md) (탈락과 자동 카메라 이관)

## 1. 지금 무엇이 문제인가

**완주하면** `RaceFinishView`가 "1등"을 띄우고, **탈락하면** `RaceEliminatedView`가 "탈락"을 띄운다.
그 뒤로는 화면이 굳는다:

- 카메라가 **자동으로** 남은 꼴찌를 따라간다. 사람이 고를 수 없다.
- 판이 끝날 때까지 **떠날 수 없다.** 두 화면을 닫는 코드조차 없다 — 씬이 내려가며
  뷰 팩토리 등록이 풀릴 때 함께 사라진다.

## 2. 무엇을 만드나

| 부품 | 정체 | 하는 일 |
|---|---|---|
| `FlappySpectate` | 순수 C#, 씬 싱글턴 | "지금 누구를 보는가"의 **유일한 주인**. 후보 목록·현재 대상·앞뒤 이동 |
| `RaceSpectateView` (+VM) | UIView, Window 밴드 | `◀ 관전 2 / 3 ▶` 와 `나가기` |
| `LeaveMatchConfirmView` (+VM) | UIPopup, `IResultView<bool>` | 나가기 확인 |
| `AppEvent.MatchLeft` | enum 값 하나 | 인매치 → 로비 전이의 **정직한 이름** |

없어지는 것: `FlappyWatchTarget`(정적 규칙), `FlappyHudCoordinator.FollowNextRunner`.
둘 다 `FlappySpectate`로 흡수된다.

## 3. 화면 — 왜 별도 View인가

조작을 기존 두 화면에 넣지 않고 **세 번째 화면**으로 뺀다. 완주했든 탈락했든 **같은 화면**이 열린다.

이유 셋:

1. **`RaceFinishView`/`RaceEliminatedView`는 루트가 `picking-mode="Ignore"`인 순수 오버레이다.**
   버튼을 넣으려면 그 규칙을 깨야 하고, 그 순간 아래 월드가 입력을 못 받는다.
2. **완주와 탈락이 필요로 하는 조작이 똑같다.** 한 곳에 두면 중복이 없다.
3. 두 화면은 "무슨 일이 일어났나"만 말하고, 조작은 조작 부품이 맡는다.

**가운데 문구는 `관전 2 / 3`** 이다. 사람 이름도, 순위처럼 보이는 문구도 쓰지 않는다 —
근거는 §7.

## 4. 관전 대상 — 주인을 하나로

지금 카메라 타깃을 정하는 주인은 `FlappyHudCoordinator.FollowNextRunner()` 하나다.
여기에 수동 선택이 끼면 **주인이 둘**이 되어, 자동 추적이 매 틱 수동 선택을 덮어쓴다.

그래서 "누구를 보는가"를 부품 하나에 몰아넣는다.

### 4.1 `FlappySpectate` API

```csharp
public class FlappySpectate
{
    public FlappySpectate(GameFramework.World.EntityRegistry entityRegistry,
                          IGameDataStore gameDataStore);

    /// 지금 볼 수 있는 사람. 앞선 사람부터.
    public IReadOnlyList<string> Candidates { get; }

    /// 지금 보는 사람. 볼 사람이 없으면 null.
    public string Current { get; }

    public void Next();      // 목록에서 한 칸 뒤로(= 화면 숫자가 하나 는다). 끝에서 되돌아온다
    public void Prev();      // 목록에서 한 칸 앞으로. 끝에서 되돌아온다

    /// 매 틱. 후보를 다시 만들고, 보던 사람이 사라졌으면 다시 고른다.
    public void Refresh();
}
```

> **`IPlayerContext`가 아니라 `IGameDataStore`를 쓴다.** 두 값(`playerContext.entityId`와
> `gameDataStore.userEntityId`)은 같은 것을 가리키지만 **채워지는 시점이 다르다** — 앞의 것은
> *내 새가 만들어진 뒤* 크리에이터가 채우고, 뒤의 것은 `GameInfoToC`를 받은 시점에 이미 있다.
> 매 틱 도는 부품이 앞의 것을 쓰면 스폰 전 몇 틱 동안 "나를 모르는" 상태가 되어 엉뚱한 사람을
> 고른다. (`FlappyChaserView`가 지금 `playerContext`를 쓰는데, 이 슬라이스에서 함께 넘어간다.)

### 4.2 규칙

| 항목 | 규칙 | 왜 |
|---|---|---|
| 후보 | 레지스트리에 있는 `EntityKind == Character` 중 **아직 결승선을 안 넘은** 것 | 결승선 너머에 멈춰 선 새를 보는 건 의미가 없다 |
| 순서 | **x 내림차순**(선두가 0번), 같으면 **id 내림차순** | 화면의 `관전 1 / 3`이 선두를 뜻한다. 동률 처리는 결정론을 위해 |
| 초기값 | 내가 후보에 있으면 **나**, 아니면 **목록의 마지막**(= 꼴찌) | 기존 규칙 그대로. 꼴찌를 봐야 추격자 벽이 같은 화면에 있다 |
| 재선택 | ① 내가 후보에 있으면 **무조건 나** ② 그 밖에는 `Current`가 후보에서 사라졌을 때만 | ①이 없으면, 내 새가 등록되기 전에 한 틱이라도 돌았을 때 남을 고른 채 굳는다(스폰 순서는 보장되지 않는다). 관전 조작은 내가 빠진 뒤에만 열리므로 ①이 수동 선택을 밟는 일은 없다 |
| 후보 0 | `Current = null`, 카메라를 그대로 둔다 | 판이 곧 끝나는 상황이라 화면을 흔들 이유가 없다 |

> **동률을 id 내림차순으로 하는 이유.** 기존 규칙(`FlappyWatchTarget`)은 폴백을
> "x가 가장 작고, 같으면 **id가 작은** 새"로 정했다. 목록을 x 내림차순 + id 내림차순으로 두면
> **마지막 원소가 정확히 그 새**가 되어, 폴백이 `Candidates[^1]` 한 줄이 된다. 정렬 기준을
> 둘로 나누면 "목록 순서"와 "폴백 규칙"이 따로 놀아 어긋날 수 있다.

### 4.3 "완주했나"를 클라가 아는 법 — 두 갈래다

**클라의 시뮬은 내 새만 굴린다**(`Simulated` 마커). 그래서:

| 대상 | 신호 | 지연 |
|---|---|---|
| 내 새 | `FinishState.Finished` — 클라 시뮬이 그 틱에 직접 판정 | 없음 |
| 남의 새 | `FinishPlacement.Value > 0` — 서버가 스냅샷에 실어 보낸다 | 약 0.2초 |

둘 중 **하나라도 참이면 후보에서 뺀다.** 남의 새가 완주한 직후 0.2초 동안 후보에 남아
있을 수 있으나, 그 사이 그 새는 감속해 서 있으므로 보이는 결함이 없다.

> 이 갈래가 필요한 이유를 잊지 말 것: 클라는 남의 새에 대해 **판정을 하지 않는다.**
> 남의 결승 통과는 서버가 알려주는 것뿐이다.

### 4.4 카메라를 실제로 옮기는 곳

`FlappyHudCoordinator.Tick()`이 매 틱:

```csharp
spectate.Refresh();
if (spectate.Current != _cameraTargetId)
{
    var visual = actorRegistry.Get(spectate.Current)?.visualGameObject;
    if (visual != null)           // 아직 몸이 안 붙었으면 다음 틱에 다시 본다
    {
        _cameraTargetId = spectate.Current;
        cameraController.SetTarget(visual.transform);
    }
}
```

### 4.5 추격자 벽도 같은 대상을 봐야 한다

`FlappyChaserView`는 벽을 **어느 시각으로 그릴지**를 정하려고 지금 `FlappyWatchTarget.Resolve`를
따로 부른다(내 새는 예측이라 앞선 시간, 남의 새는 보간이라 뒤처진 시간 — 벽은 하나뿐이라 둘 다
맞출 수 없다). 수동 선택이 생기면 **벽만 엉뚱한 시각으로 그려진다.**

→ `FlappySpectate.Current`를 읽게 바꾼다. 실행 순서는 이미 맞다: 코디네이터가 `ITickable`,
벽이 `ILateTickable`이라 벽이 나중에 읽는다.

레이스 중에는 `Current`가 나 자신이므로 **지금 동작과 완전히 같다.**

## 5. 나가기

### 5.1 흐름

```
나가기 버튼 → (콜백) → FlappyHudCoordinator
                        └ windowManager.OpenModalAsync<LeaveMatchConfirmView, bool>()
                          └ true → appStateMachine.Fire(AppEvent.MatchLeft)
                                   └ InMatch → FrontEnd → SceneLoader.Load("Lobby")
                                     └ 씬 파괴 → LOPRoom.OnDestroy → StopClient·정리
```

View는 콜백만 넘기고 자기를 닫거나 화면을 바꾸지 않는다 — `MatchResultView.SetConfirmCallback`이
이 저장소의 본이고, 아키텍처 가이드라인의 "흐름의 경계"(큰 흐름 = 코디네이터)가 그 근거다.

### 5.2 서버에는 아무것도 안 보낸다

씬이 내려가며 연결이 끊기고, 서버는 이미 나간 사람을 제대로 처리한다:

- **완주자가 나가도 등수가 유지된다** — `FinishOrderTracker`가 기록을 들고 있다
  (Part 1에서 이 부품을 못 지운 이유가 정확히 이것이다).
- **탈락자도 마찬가지** — `FlappyChaserSystem.eliminated` 목록에 남는다.

### 5.3 `AppEvent.MatchLeft`를 더하는 이유

지금 인매치 → 로비 화살표에 붙은 이름은 `MatchEnded` 하나뿐이다. 나가기에 그걸 재사용하면
**상태 머신이 거짓말을 한다** — 판은 남은 사람끼리 돌아가는데 "끝났다"고 적힌다.

```csharp
// AppEvent.cs
MatchLeft,

// InMatch.cs — 목적지는 같다
AppEvent.MatchEnded => frontEnd(),
AppEvent.MatchLeft  => frontEnd(),
```

동작은 완전히 같다. 값은 두 줄이고, 나중에 기권 처리(MMR 차감 등)가 생기면 갈라질 자리가
이미 나 있다. 재사용으로 시작하면 그때 모든 호출부를 뒤져 "이 `MatchEnded`는 진짜 끝난 건가,
나간 건가"를 구별해야 한다.

### 5.4 확인창

되돌릴 수 없는 행동이고 버튼이 `◀ ▶` 근처에 있어 오탭 위험이 있다.

> **판을 떠납니다**
> 다시 들어올 수 없고, 결과 화면도 볼 수 없습니다.
> `[나가기]` `[취소]`

**백드롭을 눌러도 취소**로 처리한다(안전한 쪽이 기본값). `AutoClose = true`(팝업 기본값)면 백드롭
클릭이 `Close`를 부르고, `Close`가 View → ViewModel을 Dispose한다. **ViewModel의 `Dispose()`가
결과를 `false`로 확정하면** 어떤 경로로 닫히든 `OpenModalAsync`의 대기가 풀린다 —
`ChangeDisplayNameViewModel.Dispose()`가 이미 그렇게 한다(`_result.TrySetResult(false)`).
이 규약을 안 지키면 대기가 영영 안 풀린다.

### 5.5 나간 뒤 결과 화면은 안 뜬다 — 의도된 것이다

`matchResultDataStore.result`에 값을 넣는 곳은 **한 군데뿐**이다: `MatchEndedMessageHandler`가
서버의 `MatchEndedToC`를 받았을 때. 먼저 나가면 그 메시지를 받기 전에 연결이 끊기므로 값이
비고, 로비의 `FrontEndCoordinator`는 null이면 그냥 넘어간다.

이 상태를 그대로 둔다. 근거:

- 나가는 사람은 **이미 자기 등수를 화면에서 보고** 나간다.
- 최종 MMR은 서버가 판이 끝날 때 계산하므로 **나가는 시점엔 존재하지 않는다.**
  MMR 칸이 빈 결과 화면을 띄우면 "왜 안 나오지?"가 된다.
- 판 끝까지 연결을 유지하는 대안은 "화면만 로비로"가 되어, 로비에서 다시 매칭을 걸 수 있게 되는
  순간 방 두 개에 속한다. 그걸 막는 규칙을 새로 만들어야 한다.

전적·MMR은 다음 접속 때 프로필에 반영된다.

> 예외 하나: `Fire`와 실제 씬 로드(프레임 끝) 사이에 `MatchEndedToC`가 도착하면 결과가 채워져
> 로비에서 결과 화면이 뜬다. 창이 좁고, 뜨더라도 진짜 데이터라 해롭지 않다.

## 6. 언제 열고 닫나

**연다**: 지금 `RaceFinishView`/`RaceEliminatedView`를 여는 그 두 자리에서 함께.

**닫지 않는다**: 판이 끝나면 씬이 내려가며 뷰 팩토리 등록이 풀리고, `WindowManager`가 그 타입으로
열린 창을 전부 닫는다. 기존 두 화면과 같은 수명이다.

## 7. 이름을 못 쓰는 이유

**인게임에 표시 이름이 없다.** 엔티티는 uuid로만 식별되고, 머리 위 판때기
(`CharacterNameplate`)도 HP 바만 그린다. 와이어에도 이름 필드가 없다.

남의 이름을 띄우려면 스냅샷이나 스폰 메시지에 필드를 더해야 한다 — 이 슬라이스 밖이다.
그래서 `관전 2 / 3`으로 간다. 순위처럼 보이는 문구("2위 관전 중")도 쓰지 않는다 —
완주해서 앞서 있는 사람이 후보에서 빠지므로 **거짓말이 된다.**

## 8. 테스트

`FlappySpectate`가 순수 C#이라 규칙 전부를 EditMode로 못 박는다:

| # | 테스트 |
|---|---|
| 1 | 레이스 중엔 나를 본다 |
| 2 | 내가 없어지면 꼴찌를 본다 (기존 `FlappyWatchTargetTests` 5개를 여기로 옮긴다) |
| 3 | `Next()`가 한 칸 앞사람으로 가고, 끝에서 되돌아온다 |
| 4 | `Prev()`도 마찬가지 |
| 5 | 완주한 사람은 후보에서 빠진다 — 내 `FinishState`로도, 남의 `FinishPlacement`로도 |
| 6 | 보던 사람이 사라지면 다시 고른다 |
| 7 | 보던 사람이 그대로면 수동 선택이 유지된다 (자동이 덮어쓰지 않는다) |
| 8 | x가 같으면 id로 갈린다 (결정론) |
| 9 | 후보가 없으면 `Current`가 null이고 터지지 않는다 |

전부 뮤테이션으로 빨강을 확인한다. View/VM은 이 저장소에 테스트 선례가 없어 라이브로 본다.

### 라이브 검증 항목

1. 완주 후 `◀ ▶`로 남은 사람을 순환하고 카메라가 따라간다
2. 수동으로 딴 사람을 봐도 **추격자 벽이 튀지 않는다** (§4.5)
3. `나가기` → 확인창 → 로비
4. 확인창에서 `취소` → 화면 그대로
5. 탈락했을 때도 같은 화면이 뜬다
6. **레이스 중 동작이 그대로다** (회귀 — `Current`가 나 자신)

## 9. 범위 밖

- **이름 표시** — 와이어에 필드가 없다(§7). 별도 항목.
- **레이스 중 기권** — 나가기는 탈락·완주했을 때만 나온다. 달리는 중에 나가는 것은 다른 기능이다.
- **Skydive** — 탈락 개념도 완주 화면도 아직 없다. `FlappySpectate`가 x축을 읽으므로 Flappy 전용이다.
- **나간 사람의 결과 화면** — §5.5.

## 10. 산업 표준 매핑

- **관전 대상 순환(`◀ ▶`)** — 배틀로얄 표준(PUBG·Apex·포트나이트). 인원이 늘어도 그대로 쓰이고,
  화면을 거의 안 가린다는 것이 목록형 대비 이점이다.
- **관전 상태를 부품 하나가 소유** — 언리얼 `APlayerController::ViewTarget` +
  `ServerViewNextPlayer`/`ServerViewPrevPlayer`에 대응한다. 언리얼도 "지금 누구를 보는가"를
  한 곳에 두고 다음/이전만 노출한다.
- **View는 콜백만, 흐름은 코디네이터** — 이 저장소의 `MatchResultView`/`ShellView` 선례이자
  아키텍처 가이드라인 "흐름의 경계".
- **`MatchLeft`를 별도 사건으로** — 상태 머신에서 "정상 종료"와 "중도 이탈"을 구별하는 것은
  일반적이다(같은 목적지로 가더라도).

## 11. 열린 결정

- [ ] `LeaveMatchConfirmView`를 어느 스코프에 등록할지 — 지금은 Flappy 스코프.
      Skydive에도 나가기가 생기면 전역(`UIInstaller`)으로 옮긴다.
