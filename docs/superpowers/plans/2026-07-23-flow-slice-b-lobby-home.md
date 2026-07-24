# Flow Slice B — 로비 홈 허브 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 프론트엔드 씬의 베이스 화면을 "바 Play 버튼"에서 **로비 홈 허브**(Play + 하단 네비바 골격)로 재구성한다. 매칭은 기존과 동일하게 작동.

**Architecture:** 신규 `LobbyHomeView`(얇은 바인더)를 프론트엔드(현 Lobby) 씬의 `Window` 밴드 베이스로 연다. Play 버튼은 기존 `MatchmakingViewModel.Play()`를 재사용(매칭 로직/대기 오버레이 `MatchmakingCoordinator`는 불변). 하단 네비바(상점/설정/프로필 버튼)는 **레이아웃만** 넣고 동작은 Slice C에서 배선. 기존 `MatchmakingView`는 은퇴.

**Tech Stack:** Unity UI Toolkit(UXML/USS + `UIViewCatalog`), VContainer, R3(간접), 기존 `WindowManager`/`UIView` 인프라.

## Global Constraints

- **동작 보존**: 매칭(Play→대기 오버레이→취소/성사) 흐름은 리팩터 전후 동일해야 한다.
- **피처 브랜치**에서 작업(현재 `feature/front-end-flow-skeleton`). main 직접 커밋 금지.
- **새 `.cs`/`.uxml`/`.uss`는 Unity가 생성한 `.meta`와 함께 커밋**. `.meta` 직접 생성 금지.
- **테스트 전략 = 컴파일 체크 + 플레이테스트** (클라 Assembly-CSharp라 단위 테스트 인프라 없음). 각 변경 후 Unity 컴파일 오류 0 확인 + 마지막에 로비 홈 표시·Play 매칭 동작 플레이테스트.
- **UnityMCP 대상 고정**: 모든 호출에 `unity_instance`를 클라(`mcpforunity://instances`에서 이름 `LeagueOfPhysical-Client`인 id)로 명시.
- **파일 은퇴는 rename 아닌 create+delete로**: 폴더/파일 rename은 VS(Roslyn)/Unity 핸들 락으로 막힐 수 있음(이 repo에서 겪음). 신규는 create, 은퇴는 `git rm`(락이면 Unity·VS 닫고 재시도).
- **뷰 = 얇은 바인더 / VM = 신호·커맨드 / 코디네이터 = 네비게이션** (아키텍처 규칙, `MatchmakingCoordinator`가 템플릿).

## 범위 밖 (후속 스펙)

- 로비 홈의 **정교한 레이아웃·비주얼·콘텐츠**(로고, 프로필 요약, 재화, 모드 선택 등) — 별도 "로비 홈 콘텐츠" 스펙.
- 네비바 버튼의 **실제 동작**(상점/설정/프로필 윈도우 열기) — Slice C.
- 앱 FSM(씬 전환 일원화) — Slice A(마지막).

---

## 파일 구조

| 파일 | 책임 | 변경 |
|---|---|---|
| `Assets/UI/LobbyHome/LobbyHomeView.uxml` | 로비 홈 레이아웃(Play + 네비바 골격) | 신규 |
| `Assets/UI/LobbyHome/LobbyHomeView.uss` | 로비 홈 스타일(최소) | 신규 |
| `Assets/UI/UIViewCatalog.asset` | `LobbyHomeView` → uxml 매핑 추가, `MatchmakingView` 매핑 제거 | 수정 |
| `Assets/Scripts/UI/LobbyHome/LobbyHomeView.cs` | 얇은 바인더: Play→matchmaking, 네비바(골격) | 신규 |
| `Assets/Scripts/Lobby/LobbyLifetimeScope.cs` | `LobbyHomeView` 등록·오픈, `MatchmakingView` 오픈 제거 | 수정 |
| `Assets/Scripts/UI/Matchmaking/MatchmakingView.cs` (+ uxml/uss) | 은퇴(Play 역할 LobbyHome 흡수) | 삭제 |

> `MatchmakingViewModel`·`MatchmakingCoordinator`·`MatchingWaitingView`는 **불변**(매칭 로직/오버레이).

---

## Task 1: LobbyHomeView UXML + USS (신규 에셋)

**Files:**
- Create: `Assets/UI/LobbyHome/LobbyHomeView.uxml`
- Create: `Assets/UI/LobbyHome/LobbyHomeView.uss`

**Interfaces:**
- Produces: UXML 요소 이름 `play-button`(Button), `nav-shop`/`nav-settings`/`nav-profile`(Button) — Task 3·Slice C가 `Root.Q<Button>("...")`로 조회.

- [ ] **Step 1: `LobbyHomeView.uxml` 생성**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="lobbyhome-root" class="lobbyhome-root">
        <ui:VisualElement name="lobbyhome-top" class="lobbyhome-top">
            <ui:Label text="LEAGUE OF PHYSICAL" class="lobbyhome-title" />
        </ui:VisualElement>

        <ui:VisualElement name="lobbyhome-center" class="lobbyhome-center">
            <ui:Button name="play-button" text="PLAY" class="lobbyhome-play" />
        </ui:VisualElement>

        <!-- 하단 네비바: 레이아웃만. 실제 동작은 Slice C에서 배선. -->
        <ui:VisualElement name="nav-bar" class="lobbyhome-navbar">
            <ui:Button name="nav-shop" text="상점" class="lobbyhome-nav" />
            <ui:Button name="nav-profile" text="프로필" class="lobbyhome-nav" />
            <ui:Button name="nav-settings" text="설정" class="lobbyhome-nav" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: `LobbyHomeView.uss` 생성**

```css
.lobbyhome-root {
    flex-grow: 1;
    justify-content: space-between;
    align-items: center;
    padding: 24px;
}
.lobbyhome-top { align-items: center; margin-top: 24px; }
.lobbyhome-title { font-size: 32px; -unity-font-style: bold; color: white; }
.lobbyhome-center { flex-grow: 1; justify-content: center; align-items: center; }
.lobbyhome-play {
    width: 240px; height: 96px; font-size: 36px; -unity-font-style: bold;
}
.lobbyhome-navbar {
    flex-direction: row; justify-content: space-around;
    width: 100%; margin-bottom: 16px;
}
.lobbyhome-nav { width: 120px; height: 64px; font-size: 20px; }
```

- [ ] **Step 3: Unity 임포트 + 컴파일 확인**

UnityMCP 클라 인스턴스에 `refresh_unity`(scope=all, mode=force) → `editor_state.isCompiling` false 대기 → `read_console`(errors) 0 확인. 신규 `.uxml.meta`/`.uss.meta`가 Unity에 의해 생성됐는지 `git status`로 확인.
Expected: 오류 0, `LobbyHomeView.uxml.meta`·`LobbyHomeView.uss.meta` 생성됨.

- [ ] **Step 4: 커밋(.meta 포함)**

```bash
git add Assets/UI/LobbyHome/
git commit -m "feat(ui): 로비 홈 UXML/USS 골격(Play + 네비바)"
```

---

## Task 2: UIViewCatalog에 LobbyHomeView 등록

**Files:**
- Modify: `Assets/UI/UIViewCatalog.asset`

**Interfaces:**
- Consumes: Task 1의 `LobbyHomeView.uxml`/`.uss`.
- Produces: 카탈로그에 `viewName: LobbyHomeView` 엔트리(→ uxml/uss GUID). `WindowManager.Open<LobbyHomeView>()`가 이 키(`typeof(LobbyHomeView).Name`)로 조회.

> `UIViewCatalog`는 ScriptableObject이고 엔트리가 uxml/uss를 **GUID로 참조**한다. GUID를 텍스트로 손대면 취약하므로 **Unity 에디터/`manage_scriptable_object`로 편집**한다. `viewName` 문자열은 반드시 클래스명 `LobbyHomeView`와 일치해야 런타임 조회가 된다(대소문자 포함).

- [ ] **Step 1: 카탈로그에 엔트리 추가**

Unity 에디터에서 `Assets/UI/UIViewCatalog.asset` 선택 → `entries`에 항목 추가:
- `viewName` = `LobbyHomeView`
- `uxml` = `Assets/UI/LobbyHome/LobbyHomeView.uxml`
- `uss` = `Assets/UI/LobbyHome/LobbyHomeView.uss`

(UnityMCP로 할 경우 `manage_scriptable_object`로 `entries` 리스트에 위 필드의 항목을 append. uxml/uss는 에셋 참조(GUID)로 지정.)

- [ ] **Step 2: 저장 + 확인**

에셋 저장 후 `refresh_unity` → `read_console` 오류 0. `UIViewCatalog.asset` diff에 `viewName: LobbyHomeView`와 uxml/uss GUID 참조가 추가됐는지 확인.
Expected: 엔트리 추가됨, 오류 0.

- [ ] **Step 3: 커밋**

```bash
git add Assets/UI/UIViewCatalog.asset
git commit -m "feat(ui): UIViewCatalog에 LobbyHomeView 매핑 추가"
```

---

## Task 3: LobbyHomeView 바인더 (Play → matchmaking)

**Files:**
- Create: `Assets/Scripts/UI/LobbyHome/LobbyHomeView.cs`

**Interfaces:**
- Consumes: `MatchmakingViewModel`(기존, `void Play()`), `UIView`/`UILayer`(기존 베이스), UXML 요소 `play-button`.
- Produces: `LobbyHomeView : UIView` (ctor `(MatchmakingViewModel)`), `Layer => UILayer.Window`.

> Slice B에서 네비바 버튼(`nav-shop`/`nav-profile`/`nav-settings`)은 **조회·핸들러를 달지 않는다**(레이아웃만). Slice C에서 핸들러 + 네비 신호를 추가한다. 지금 inert 버튼으로 두는 게 의도.

- [ ] **Step 1: `LobbyHomeView.cs` 생성**

```csharp
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 로비 홈 허브 View(프론트엔드 베이스). Play 버튼을 매칭 커맨드로 전달하는 얇은 바인더.
    /// 매칭 흐름·대기 오버레이는 MatchmakingCoordinator가 담당하므로 여기선 다루지 않는다.
    /// 하단 네비바(상점/프로필/설정)의 동작 배선은 Slice C에서 추가한다.
    /// </summary>
    public class LobbyHomeView : UIView
    {
        private readonly MatchmakingViewModel _matchmaking;

        private Button _playButton;

        public LobbyHomeView(MatchmakingViewModel matchmaking)
        {
            _matchmaking = matchmaking;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _playButton = Root.Q<Button>("play-button");
            _playButton.clicked += OnPlayClicked;
        }

        public override void OnClose()
        {
            if (_playButton != null) _playButton.clicked -= OnPlayClicked;
            base.OnClose();
        }

        private void OnPlayClicked() => _matchmaking.Play();
    }
}
```

- [ ] **Step 2: 컴파일 확인**

`refresh_unity`(scope=all) → `read_console` 오류 0. `git status`에 `LobbyHomeView.cs.meta` 생성 확인.
Expected: 오류 0(아직 어디서도 안 여니 동작 변화 없음), `.meta` 생성됨.

- [ ] **Step 3: 커밋(.meta 포함)**

```bash
git add Assets/Scripts/UI/LobbyHome/LobbyHomeView.cs Assets/Scripts/UI/LobbyHome/LobbyHomeView.cs.meta
git commit -m "feat(ui): LobbyHomeView 바인더(Play→matchmaking)"
```

---

## Task 4: LobbyLifetimeScope가 LobbyHomeView를 베이스로 오픈

**Files:**
- Modify: `Assets/Scripts/Lobby/LobbyLifetimeScope.cs`

**Interfaces:**
- Consumes: Task 3 `LobbyHomeView`, Task 2 카탈로그 엔트리.
- Produces: 로비 진입 시 열리는 베이스 = `LobbyHomeView`(기존 `MatchmakingView` 대체).

- [ ] **Step 1: 등록·오픈을 LobbyHomeView로 교체**

`LobbyLifetimeScope.cs`에서:

29~30행 교체:
```csharp
            // VM은 Scoped — LobbyHomeView(Play)와 Coordinator가 같은 인스턴스를 공유해야 신호가 이어진다.
            builder.Register<MatchmakingViewModel>(Lifetime.Scoped);
            builder.Register<LobbyHomeView>(Lifetime.Transient);
            builder.RegisterEntryPoint<MatchmakingCoordinator>();
```

37~41행(빌드 콜백 내부)에서 `MatchmakingView` → `LobbyHomeView`로:
```csharp
                // 전역 WindowManager에 LobbyHomeView 팩토리 기여: Open<LobbyHomeView>가 이 스코프 resolver로
                // 생성 → MatchmakingViewModel 주입. 로비 진입 시 허브 화면을 연다.
                var windowManager = container.Resolve<IWindowManager>();
                _matchmakingViewRegistration = windowManager.RegisterViewFactory<LobbyHomeView>(() => container.Resolve<LobbyHomeView>());
                windowManager.Open<LobbyHomeView>();
```

(필드 `_matchmakingViewRegistration`·OnDestroy 주석의 "MatchmakingView"는 "LobbyHomeView"로 문구만 갱신.)

- [ ] **Step 2: 컴파일 확인**

`refresh_unity`(scope=all) → `read_console` 오류 0.
Expected: 오류 0. (이제 `MatchmakingView`는 아무 데서도 안 열림.)

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Lobby/LobbyLifetimeScope.cs
git commit -m "feat(ui): 로비 베이스를 LobbyHomeView로 교체(MatchmakingView 오픈 제거)"
```

---

## Task 5: MatchmakingView 은퇴(삭제)

**Files:**
- Delete: `Assets/Scripts/UI/Matchmaking/MatchmakingView.cs` (+ `.meta`)
- Delete: `Assets/UI/Matchmaking/MatchmakingView.uxml` (+ `.meta`), `Assets/UI/Matchmaking/MatchmakingView.uss` (+ `.meta`)
- Modify: `Assets/UI/UIViewCatalog.asset` (`MatchmakingView` 엔트리 제거)

**Interfaces:**
- Consumes: Task 4(더 이상 `MatchmakingView`를 열지 않음).

> `MatchmakingViewModel`·`MatchmakingCoordinator`·`MatchingWaitingView`는 **삭제하지 않는다**(매칭 로직/대기 오버레이 계속 사용).

- [ ] **Step 1: 카탈로그에서 MatchmakingView 엔트리 제거**

Unity 에디터/`manage_scriptable_object`로 `UIViewCatalog.asset`의 `entries`에서 `viewName: MatchmakingView` 항목 삭제.

- [ ] **Step 2: 파일 삭제**

```bash
git rm Assets/Scripts/UI/Matchmaking/MatchmakingView.cs Assets/Scripts/UI/Matchmaking/MatchmakingView.cs.meta \
       Assets/UI/Matchmaking/MatchmakingView.uxml Assets/UI/Matchmaking/MatchmakingView.uxml.meta \
       Assets/UI/Matchmaking/MatchmakingView.uss Assets/UI/Matchmaking/MatchmakingView.uss.meta
```
`git rm`이 "Permission denied"(VS/Unity 락)면 Unity·Visual Studio를 닫고 재시도(이 repo에서 겪은 락 이슈).

- [ ] **Step 3: 컴파일 확인 + 잔여 참조 0**

`refresh_unity`(scope=all) → `read_console` 오류 0. `git grep -n "MatchmakingView\b"`로 `MatchmakingView`(View) 잔여 참조가 없는지 확인(`MatchmakingViewModel`은 남아야 정상 — `\b`로 구분).
Expected: 오류 0, `MatchmakingView`(순수) 참조 없음.

- [ ] **Step 4: 커밋**

```bash
git add -A
git commit -m "refactor(ui): MatchmakingView 은퇴(Play 역할 LobbyHomeView로 흡수)"
```

---

## Task 6: 플레이테스트 (동작 보존 + 로비 홈 표시)

**Files:** (없음 — 검증)

> **선행**: 로컬 서버(매칭/룸) + auth 픽스처 필요(메모리 "Local test auth fixture"). 백엔드 불가 시 사용자 수동 검증으로 이관.

- [ ] **Step 1: 플레이 진입 → 로비 도달**

플레이 모드 → Entrance(로그인) → Lobby 씬. `read_console` 예외 0.
Expected: 로비에 **로비 홈 허브**(타이틀 + PLAY + 하단 네비바) 표시. (네비바 버튼은 눌러도 아무 일 없음 — 정상, Slice C에서 배선.)

- [ ] **Step 2: Play → 매칭 동작 확인(보존)**

PLAY 클릭 → 매칭 시작 → 대기 오버레이(`MatchingWaitingView`) 뜸 → 취소 시 닫힘. 리팩터 전과 동일.
Expected: 매칭 흐름 정상, `read_console` 예외 0.

- [ ] **Step 3: 플레이 종료**

플레이 종료, 최종 `read_console` 확인.
Expected: 전 흐름 예외 0, 로비 홈이 이전 매칭 화면을 대체하며 Play 동작 동일.

---

## 완료 기준

- 로비 진입 시 `LobbyHomeView` 허브가 베이스로 뜬다(PLAY + 네비바 골격).
- PLAY → 매칭 동작이 리팩터 전과 동일(대기 오버레이·취소 정상).
- `MatchmakingView`(View/UXML/USS/카탈로그) 은퇴, 잔여 참조 0, 컴파일 0 errors.
- 네비바 버튼 동작·상점/설정/프로필 윈도우는 Slice C, 앱 FSM은 Slice A.
