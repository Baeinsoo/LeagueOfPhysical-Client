# 매치메이킹 UI를 MVVM 템플릿으로 정리 (코디네이터 도입)

## 배경 — 왜 이 작업을 하나

전체 게임 플로우(로그인 → 로비 → 매치메이킹 → 게임 → 결과)와 화면별 UI를 설계하기 **직전**에,
현재 UI 구현이 확정된 아키텍처(`docs/architecture-guidelines.md`의 "UI 아키텍처" 절)를 잘 따르는지
감사했다. 결과: **코어(레이어×스택 윈도우 매니저, 다이얼로그 서비스, R3 바인딩)와 대부분의 View는
모범적으로 준수**하나, **`MatchMakingViewModel` 하나만 규칙을 어긴다.**

이 화면은 곧 만들 로비/결과 화면의 **본보기(템플릿)** 가 되므로, 새 화면들이 잘못된 예시를 복제하지
않도록 지금 바로잡는다.

## 현재 문제 — `MatchMakingViewModel`의 규칙 위반

`MatchMakingViewModel`이 두 가지 아키텍처 규칙을 어긴다:

1. **규칙: 라이브 상태는 R3로 노출** — 하지만 `IsMatching`이 평범한 `bool` 프로퍼티이고, 그
   setter의 **부수효과로** UI를 직접 조작한다.
2. **규칙: VM은 네비게이션(화면 교체)을 직접 실행하지 않고 도메인 신호만 노출, 코디네이터가 화면을
   바꾼다** — 하지만 VM이 `IsMatching` setter 안에서 `_windowManager.Open<MatchingWaitingView>()` /
   `Close`를 **직접 호출**한다. 코드 주석이 이 타협을 스스로 인정("전역 뷰라 VM이 직접 호출").

> **FSM은 이미 깨끗하다.** `MatchStateMachine`과 그 상태들(`Idle`/`InWaitingRoom`/`InGameRoom`…)은
> 순수 흐름 로직만 담고 UI를 전혀 건드리지 않는다(위치 폴링 = WebAPI, 방 접속 = `RoomConnector`).
> 이 "FSM은 UI를 모른다"는 규칙을 **그대로 유지**한다.

## 목표

- `MatchMakingViewModel`을 **신호만 노출하는** 순수 프레젠테이션 어댑터로 정리한다.
- 대기 오버레이의 열고/닫기(네비게이션)를 **새 코디네이터**로 옮긴다.
- 결과적으로 **"VM = 커맨드 + R3 신호 / 코디네이터 = 네비게이션"** 이라는 재사용 템플릿을 만든다.
- 동작은 **바꾸지 않는다**(순수 리팩터). 사용자가 보는 흐름은 이전과 동일.

## 설계

### 책임 경계 (핵심)

세 조각이 각자 한 가지만 한다:

| 조각 | 역할 | UI/네비게이션 접촉 |
|---|---|---|
| **`MatchMakingView`** (기존, 얇은 바인더) | Play 버튼 → `vm.Play()`. 자기 UXML 트리만 갱신 | ❌ WindowManager 안 씀 |
| **`MatchMakingViewModel`** (기존, 정리) | FSM 상태를 보고 **`IsMatching`을 R3 신호로 노출** + `Play()`/`Cancel()` 커맨드 | ❌ **WindowManager 제거** — 신호만 |
| **`MatchMakingCoordinator`** (신규) | `vm.IsMatching` 구독 → 대기 오버레이 Open/Close + 취소 버튼을 `vm.Cancel()`에 배선. 흐름 시작(`StartFlow`) 소유 | ✅ 여기서만 WindowManager 사용 |

### `MatchMakingViewModel` 변경

- **제거**: `IWindowManager` 생성자 의존, `_waitingView` 필드, setter 안의 `Open`/`Close`,
  `_waitingView.SetCancelCallback(...)` 배선.
- **변경**: `IsMatching`을 `bool` → `ReadOnlyReactiveProperty<bool>`(내부 `ReactiveProperty<bool>`).
  FSM `onStateChange`에서 `_isMatching.Value = current is InWaitingRoom` 만 갱신.
- **유지**: `Play()`(매칭 파라미터 세팅 + `PlayClicked` 발행), `Cancel()`(`CancelClicked` 발행),
  FSM 구독/시작/정지.
- **리네임**: `Start()` → `StartFlow()`. 이제 View가 아니라 **코디네이터**가 호출한다(흐름 시작
  소유권을 코디네이터로 일원화).
- `Dispose()`: FSM 구독 해제 + `fsm.Stop()` + `ReactiveProperty` Dispose. (오버레이 관련 정리는 사라짐.)

### `MatchMakingCoordinator` (신규 — 재사용 템플릿)

```
MatchMakingCoordinator : IStartable, IDisposable   // Lobby 스코프 엔트리포인트
  생성자(IWindowManager windowManager, MatchMakingViewModel viewModel)

  Start():
    vm.IsMatching 구독 →
      값이 true  : _waiting = wm.Open<MatchingWaitingView>();  _waiting.SetCancelCallback(vm.Cancel);
      값이 false : if (_waiting != null) { wm.Close(_waiting); _waiting = null; }
    vm.StartFlow();

  Dispose():
    구독 해제; if (_waiting != null) wm.Close(_waiting);
```

- R3 `ReactiveProperty`는 **구독 시점에 현재값을 즉시 replay**하므로, 코디네이터가 늦게 구독해도
  상태를 놓치지 않는다(빌드 콜백 ↔ 엔트리포인트 시작 순서에 안전).
- 명명 근거: "Coordinator"는 흐름/네비게이션 오케스트레이션의 확립된 업계 용어(Coordinator 패턴).
  아키텍처 문서의 "큰 흐름 → 코디네이터" 규칙과 정합.

### `MatchMakingView` 변경

- **제거**: `OnOpen`의 `_viewModel.Start()` 호출(흐름 시작은 코디네이터로 이동),
  `Dispose`의 `_viewModel.Dispose()` 호출(VM 수명을 스코프가 소유 — 아래 DI 참고).
- **유지**: Play 버튼 → `vm.Play()` 배선.

### DI 수명 변경 (`LobbyLifetimeScope`)

- `MatchMakingViewModel`: **Transient → Scoped**. View와 Coordinator가 **같은 VM 인스턴스**를 공유해야
  신호가 이어진다. Scoped이므로 **스코프가 VM 수명을 소유**(View가 Dispose하지 않음 — 이중 Dispose 방지).
- `MatchMakingCoordinator`: `builder.RegisterEntryPoint<MatchMakingCoordinator>()`로 등록(스코프가
  `IStartable.Start`/`IDisposable.Dispose` 구동).
- 그 외(FSM 상태들, `MatchMakingView` 팩토리 등록, `Open<MatchMakingView>`)는 그대로.

> **스코프 위치는 이 작업의 범위가 아니다.** "매칭이 상점/설정까지 얼마나 지속되나 + front-end 세션
> 스코프"는 별도의 **플로우 아키텍처 결정**으로, 다음 플로우 설계에서 정한다. 이 리팩터가 VM/코디네이터를
> 깔끔히 분리하므로, 나중에 등록 위치(Lobby → front-end 세션 스코프)를 옮기는 것은 몇 줄짜리 일이다.
> 그때 VM/코디네이터 **코드는 바뀌지 않는다.**

### 곁다리 정리

- `WindowManager.cs`의 전체화면 래퍼 picking 관련 주석("아래 UGUI(조그패드 등)가 막힌다")은 UI Toolkit
  전환 후 **틀린 잔재**다(조그패드도 이제 UI Toolkit). 같은 파일을 건드리는 김에 주석을 현재 사실에 맞게
  정정한다.

## 데이터 흐름

```
[사용자 Play 클릭]
   → MatchMakingView.Play 버튼 → vm.Play()
   → VM: 매칭 파라미터 세팅 + FSM.Fire(PlayClicked)
   → FSM: Idle → RequestMatchmaking → InWaitingRoom
   → VM.OnStateChange: _isMatching.Value = true
   → Coordinator(구독): wm.Open<MatchingWaitingView>() + 취소 버튼 → vm.Cancel()

[매칭 취소 / 방 입장 등으로 InWaitingRoom 이탈]
   → FSM 상태 전이 → VM.OnStateChange: _isMatching.Value = false
   → Coordinator(구독): wm.Close(대기 오버레이)
```

## 테스트

- **`MatchMakingViewModel` (EditMode 단위 테스트)**: `IWindowManager` 의존이 사라져 순수 C#로 테스트
  가능해진다(정리의 부수 이득). FSM 상태를 `InWaitingRoom`으로/에서 전이시켰을 때 `IsMatching` 값이
  올바르게 true/false로 바뀌는지 검증.
- **`MatchMakingCoordinator`**: `IWindowManager`를 목(mock)으로 두고, `IsMatching` 신호가 true/false로
  바뀔 때 `Open<MatchingWaitingView>` / `Close`가 정확히 한 번씩 호출되는지 검증.

> 클라 프로젝트 코드는 `Assets-CSharp`라 asmdef 참조가 안 되므로, 순수 C# 단위 테스트는
> standalone .NET(dotnet test) 또는 리플렉션 기반 PlayMode 패턴을 따른다(프로젝트 테스트 인프라 관례).

## 범위 밖 (하지 않는 것)

- 매칭 스코프를 Root/front-end 세션으로 옮기는 것 — **플로우 설계로 미룸**.
- 지속 매칭 인디케이터(상점/설정 화면에서 "매칭 중…" 배너) — 플로우 설계 주제.
- 다른 View(Login/HUD/Stats/GamePad 등) 변경 — 이미 규칙을 준수하므로 손대지 않음.
- FSM/상태 로직 변경 — 이미 UI-free로 깨끗함.

## 산업 표준 매핑

- **MVVM + Coordinator 패턴**: VM은 상태/커맨드만, 화면 전환(네비게이션)은 Coordinator가 담당하는 것은
  iOS(Soroush Khanlou의 Coordinator), 일반 MVVM-C에서 확립된 분리다. 아키텍처 문서의 "작은 흐름=VM /
  큰 흐름=코디네이터" 규칙의 직접 적용.
- **R3 `ReactiveProperty` 구독 시 현재값 replay**: 관찰 가능한 상태(BehaviorSubject류)의 표준 의미.
