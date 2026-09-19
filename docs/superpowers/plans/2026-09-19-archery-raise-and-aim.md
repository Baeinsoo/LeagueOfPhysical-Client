# 활쏘기 조작 "들어 올려 겨누기" 구현 계획

> **작업자에게:** 필수 하위 스킬 — `superpowers:subagent-driven-development`(권장) 또는
> `superpowers:executing-plans`로 태스크 단위 실행. 단계는 체크박스(`- [ ]`)로 추적한다.

**Goal:** 손가락 하나로 활을 들어 올리고, 같은 손가락으로 겨누고, 화면 아래 띠로 내려놓아 취소한다.

**Architecture:** 당김을 "끈 거리"에서 "잡고 있는 시간"으로 옮긴다. 화면은 *"잡고 있다"* 만
보내고 **얼마나 당겨졌는지는 시뮬(`ArcheryAimSystem`)이 정한다** — 그래야 클라 예측과 서버 권위가
같은 값을 낸다. 풀린 손가락은 09-19에 만든 관성 없는 조준 경로(`CameraController.AimBy`)로 간다.

**Tech Stack:** Unity 6000.3.16f1 · UI Toolkit(UXML/USS) · C# · NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-09-19-archery-raise-and-aim-control-design.md`

## Global Constraints

- **당김 속도를 화면이 계산하지 않는다.** 화면은 `Drawing`과 `DrawRatio`(목표치)만 보내고, 실제
  값은 `ArcheryAimSystem.Tick`의 `MoveTowards`가 정한다. 어기면 프레임레이트가 다른 두 기기가
  서로 다른 화살을 쏜다.
- **만작 시간 = 0.5초.** 단 상수는 **하나**여야 한다 — `FullDrawSeconds`가 진실원본이고
  `DrawRisePerSecond`는 거기서 유도한다.
- **내려놓는 동안에도 `Drawing`은 true로 유지한다.** false로 내리면 다시 올릴 때
  `DrawStartTick`이 리셋돼 흔들림 피로가 초기화된다 — 띠에 담갔다 빼는 것이 이득이 되면 안 된다.
- 네임스페이스 풀 한정 규칙: LOP 측 파일에서 World 타입은 `GameFramework.World.X`로 쓴다.
- 커밋 메시지는 한국어, 끝에 `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`.

## 파일 구조

| 레포 | 파일 | 책임 |
|---|---|---|
| Shared | `Runtime/Scripts/Game/ArcheryAimSystem.cs` | 만작 시간 상수 하나 + 거기서 유도한 램프 속도 |
| Shared | `Tests/EditMode/ArcheryAimSystemTests.cs` | 램프가 틱 간격과 무관한지(이 변경의 계약) |
| Client | `Assets/Scripts/UI/ArcheryPad/ArcheryPadViewModel.cs` | 시간 당김 · 내려놓기 판정 · 취소 |
| Client | `Assets/Scripts/UI/ArcheryPad/ArcheryPadView.cs` | 포인터 하나로 당김+조준, 띠 표시, 게이지 제거 |
| Client | `Assets/UI/ArcheryPad/ArcheryPad.uxml` | `draw-gauge` 제거, `lower-band` 추가, 좌우 절반 제거 |
| Client | `Assets/UI/ArcheryPad/ArcheryPad.uss` | 위에 맞춘 스타일 |

---

## Task 1: 당김을 시뮬이 정하게 하고, 상수를 하나로 모은다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryAimSystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/ArcheryAimSystemTests.cs`

**Interfaces:**
- Produces: `ArcheryAimSystem.FullDrawSeconds = 0.5f` (진실원본),
  `ArcheryAimSystem.DrawRisePerSecond = 1f / FullDrawSeconds` (= 2f),
  `ArcheryAimSystem.DrawThreshold = 0.15f` (변경 없음 — 0.075초에 해당).

> **왜 유도하나:** 이 파일에는 시간 기반 정적 헬퍼 `DrawRatio(startTick, currentTick, interval)`가
> 이미 있고 `FullDrawSeconds`를 쓴다(런타임 호출자는 없고 시험만 쓴다). 램프 속도를 따로 박으면
> 두 값이 **서로 다른 만작 시간**을 말하게 된다. 하나에서 유도하면 그 함정이 구조적으로 닫힌다.

- [ ] **Step 1: 계약 시험을 먼저 쓴다 (실패해야 한다)**

`Tests/EditMode/ArcheryAimSystemTests.cs`의 클래스 안에 추가:

```csharp
        //  잡고 있는 동안 화면이 보내는 값은 늘 1이다 — 얼마나 당겨졌는지는 시뮬이 정한다.
        //  같은 **경과 시간**이면 틱 간격이 달라도 같은 값이 나와야 한다. 이게 이 변경의 계약이다:
        //  화면이 제 시계로 램프를 계산하면 프레임레이트가 다른 두 기기가 다른 화살을 쏜다.
        static float RampFor(float seconds, float tickInterval)
        {
            var archer = Archer(Vector3.zero);
            var system = new ArcheryAimSystem(NoSwayConfig());
            int ticks = Mathf.RoundToInt(seconds / tickInterval);
            for (int i = 0; i < ticks; i++)
            {
                Feed(archer, 0f, 0f, drawing: true, release: false, drawRatio: 1f);
                system.Tick(archer, 100 + i, tickInterval);
            }
            return archer.Get<ArcheryAim>().DrawRatio;
        }

        [Test]
        public void 같은_시간이면_틱_간격이_달라도_같은_힘이_된다()
        {
            //  50Hz와 30Hz — 같은 0.25초를 잡고 있었으면 같은 곳까지 당겨져 있어야 한다.
            Assert.AreEqual(RampFor(0.25f, 1f/50f), RampFor(0.25f, 1f/30f), 1e-2f,
                "틱 간격이 달라졌다고 당김이 달라진다 — 화면/프레임레이트가 힘에 새고 있다");
        }

        [Test]
        public void 만작까지_정해진_시간이_걸린다()
        {
            Assert.Less(RampFor(ArcheryAimSystem.FullDrawSeconds * 0.5f, TickInterval), 0.75f,
                "절반만 잡고 있었는데 거의 만작이다 — 램프가 너무 빠르다");
            Assert.AreEqual(1f, RampFor(ArcheryAimSystem.FullDrawSeconds + 0.05f, TickInterval), 1e-3f,
                "만작 시간을 넘겼는데 아직 1이 아니다");
        }

        [Test]
        public void 램프_속도는_만작_시간에서_유도된다()
        {
            //  두 상수가 서로 다른 만작 시간을 말하면 안 된다. 값을 바꿔도 이 관계는 남아야 한다.
            Assert.AreEqual(1f / ArcheryAimSystem.FullDrawSeconds,
                            ArcheryAimSystem.DrawRisePerSecond, 1e-5f);
        }
```

- [ ] **Step 2: 실패를 확인한다**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" recompile
# recompile_status가 completed가 될 때까지 폴링
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" \
  run_tests --mode EditMode --filter "ArcheryAimSystemTests"
```

기대: `램프_속도는_만작_시간에서_유도된다` 실패(10 ≠ 1/0.8 = 1.25),
`만작까지_정해진_시간이_걸린다`의 첫 단언 실패(0.4초면 이미 1.0).

> 클라 에디터가 아니라 **서버 에디터**로 돌린다 — `file:` 패키지라 같은 코드를 컴파일하고,
> 클라는 EditMode 어셈블리가 많아 30초 상한에 걸린다(`[[run-tests-with-compile-error-wedges-editor]]`).

- [ ] **Step 3: 상수를 고친다**

`Runtime/Scripts/Game/ArcheryAimSystem.cs` — 기존 `FullDrawSeconds`/`DrawRisePerSecond` 선언을
아래로 교체:

```csharp
        /// <summary>
        /// 누르고 이만큼 지나면 만작이다(초). <b>당김 시간의 진실원본</b> — 아래 램프 속도가
        /// 여기서 유도되고, 시간 기반 헬퍼 <see cref="DrawRatio"/>도 이 값을 쓴다.
        /// </summary>
        public const float FullDrawSeconds = 0.5f;

        /// <summary>
        /// 시위가 당겨지는 속도(초당 당김 비율). <b>만작 시간에서 유도한다</b> — 따로 박으면 두
        /// 상수가 서로 다른 만작 시간을 말하게 된다.
        ///
        /// <para>예전엔 10f였다(0.1초). 그땐 당김을 <b>손가락이 끈 거리</b>가 정했고 이 값은
        /// "손가락이 순간이동해도 활은 못 그런다"는 상한일 뿐이었다. 지금은 화면이
        /// <c>DrawRatio = 1</c>만 보내고 <b>이 속도가 곧 당기는 동작 자체</b>다 —
        /// 그래서 힘이 양쪽의 틱 수로만 정해진다(클라가 고를 수 있는 크기가 없다).</para>
        /// </summary>
        public const float DrawRisePerSecond = 1f / FullDrawSeconds;
```

- [ ] **Step 4: 통과를 확인한다**

같은 명령으로 `ArcheryAimSystemTests` 전체를 돌린다. 기대: **전부 통과**.
이어서 `--filter "Archery"`로 활쏘기 전체(약 208개)를 돌려 회귀가 없는지 본다.

- [ ] **Step 5: 이빨 확인 — 일부러 망가뜨려 본다**

`DrawRisePerSecond`를 잠시 `10f`(옛 값)로 박고 같은 시험을 돌린다.
기대: `램프_속도는_만작_시간에서_유도된다`와 `만작까지_정해진_시간이_걸린다`가 **실패**.
확인 후 되돌린다. (통과하는 시험이 무엇도 안 막고 있으면 안 된다.)

- [ ] **Step 6: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git checkout -b feature/archery-raise-and-aim
git add Runtime/Scripts/Game/ArcheryAimSystem.cs Tests/EditMode/ArcheryAimSystemTests.cs
git commit   # 메시지: 당김을 시간으로 — 만작 0.5초, 램프를 상수 하나에서 유도
```

---

## Task 2: 화면이 "잡고 있다"만 말하게 한다 (ViewModel)

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/UI/ArcheryPad/ArcheryPadViewModel.cs`

**Interfaces:**
- Consumes: `ArcheryAimSystem.DrawThreshold`, `ArcheryAimSystem.FullDrawSeconds` (Task 1).
- Produces (View가 쓴다):
  - `public const float LowerBandFraction = 0.15f`
  - `public bool Lowering { get; }` — 지금 내려놓기 자리에 있나
  - `public float DrawRatio { get; }` — **시뮬 값을 읽는다**(화면이 따로 안 센다)
  - `public bool DrawArmed { get; }`
  - `public void BeginDraw()` / `public void UpdatePointer(Vector2 positionFraction)` / `public void EndDraw()`
  - `public void LookBy(Vector2 deltaFraction)` — 변경 없음(이미 있음)

- [ ] **Step 1: 끈 거리 당김을 걷어낸다**

아래 멤버를 **삭제**한다: `FullDrawDragFraction`, `drawOrigin`, `drawCurrent`,
`DrawOrigin`, `DrawCurrent`, `FullDrawPixels`, `DragDraw(Vector2)`.
`DrawRatio`의 자동 프로퍼티(`{ get; private set; }`)도 삭제한다 — 아래에서 시뮬 읽기로 바뀐다.

- [ ] **Step 2: 시간 당김 + 내려놓기를 넣는다**

삭제한 자리에 아래를 넣는다:

```csharp
        /// <summary>
        /// 화면 아래 이만큼이 <b>내려놓기</b> 자리다. 여기서 떼면 화살이 안 나간다.
        ///
        /// <para>손가락 하나가 당김과 조준을 다 하므로 "떼기"는 발사일 수밖에 없다 — 취소는
        /// 따로 제스처를 줘야 한다. 젤다는 드는 버튼과 쏘는 버튼이 달라 취소가 공짜지만,
        /// 손가락 하나로 옮기면 그게 안 된다.</para>
        /// </summary>
        public const float LowerBandFraction = 0.15f;

        /// <summary>지금 내려놓기 자리에 손가락이 있나. 화면이 띠를 붉게 켜는 데 쓴다.</summary>
        public bool Lowering { get; private set; }

        /// <summary>
        /// 지금 얼마나 당겨졌나(0~1). <b>시뮬 값을 읽는다</b> — 화면이 따로 세면 클라와 서버가
        /// 다른 값을 보게 된다. 화면이 하는 일은 "잡고 있다"고 말하는 것뿐이다.
        /// </summary>
        public float DrawRatio
        {
            get
            {
                var entity = entityRegistry.Get(playerContext.entityId);
                return entity?.Get<ArcheryAim>()?.DrawRatio ?? 0f;
            }
        }

        /// <summary>임계치를 넘겨 시위가 걸렸나. 못 넘으면 떼도 안 쏜다.</summary>
        public bool DrawArmed => DrawRatio >= ArcheryAimSystem.DrawThreshold;

        /// <summary>손가락을 댔다 — 활이 올라오기 시작한다. 얼마나 올라오는지는 시뮬이 정한다.</summary>
        public void BeginDraw()
        {
            drawing = true;
            Lowering = false;
            input.SetDrawing(true);
            input.SetDrawRatio(1f);
        }

        /// <summary>
        /// 손가락이 움직였다 — 내려놓기 자리에 있는지만 갱신한다(겨누기는 <see cref="LookBy"/>).
        /// </summary>
        public void UpdatePointer(Vector2 positionFraction)
        {
            if (drawing == false)
            {
                return;
            }

            Lowering = positionFraction.y > (1f - LowerBandFraction);

            //  내리는 동안에도 Drawing은 **true로 둔다**. false로 내리면 다시 올릴 때
            //  DrawStartTick이 새로 찍혀 흔들림 피로가 초기화된다 — 띠에 담갔다 빼는 것이
            //  이득이 되면 안 된다. 목표만 0으로 낮춰 활이 내려가게 한다.
            input.SetDrawRatio(Lowering ? 0f : 1f);
        }

        /// <summary>두 번 불려도 한 번만 쏜다 — 아래 View가 뗌을 두 경로로 받기 때문이다.</summary>
        public void EndDraw()
        {
            if (drawing == false)
            {
                return;
            }
            drawing = false;
            input.SetDrawing(false);

            //  내려놓은 채로 뗐거나 시위가 안 걸렸으면 쏘라는 신호를 아예 안 보낸다.
            if (Lowering == false && DrawArmed)
            {
                input.SetRelease();
            }
            Lowering = false;
        }
```

- [ ] **Step 3: 컴파일을 확인한다**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" recompile
# completed + errors:[] 확인. 에디터가 물려 있으면 오프라인 게이트로:
#   Library/Bee/artifacts/*.dag/baegames.LOP.Shared.Runtime.rsp 로 Shared를 먼저 굽고,
#   Assembly-CSharp.rsp의 Shared 참조를 그 새 DLL로 치환해 굽는다
#   ([[client-compile-gate-without-editor]])
```

기대: `ArcheryPadView.cs`에서 `DragDraw`/`DrawOrigin`/`FullDrawPixels`를 못 찾는다는 에러.
**그게 정상이다** — Task 3에서 고친다.

- [ ] **Step 4: 커밋하지 않는다**

Task 3과 같은 커밋으로 묶는다(중간 상태가 컴파일되지 않으므로).

---

## Task 3: 포인터 하나로 당기고 겨눈다 (View + UXML + USS)

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/UI/ArcheryPad/ArcheryPadView.cs`
- Modify: `LeagueOfPhysical-Client/Assets/UI/ArcheryPad/ArcheryPad.uxml`
- Modify: `LeagueOfPhysical-Client/Assets/UI/ArcheryPad/ArcheryPad.uss`

**Interfaces:**
- Consumes: Task 2의 `BeginDraw()`, `UpdatePointer(Vector2)`, `EndDraw()`, `LookBy(Vector2)`,
  `Lowering`, `DrawRatio`, `DrawArmed`, `LowerBandFraction`.

- [ ] **Step 1: UXML — 절반 두 개와 게이지를 걷고, 띠 하나를 놓는다**

`ArcheryPad.uxml`을 아래로 교체:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="ArcheryPad.uss" />
    <ui:VisualElement name="archery-pad-root" class="archery-pad-root">
        <!-- 화면 전체가 조작면이다. 누르면 활이 올라오고, 누른 채 움직이면 겨눈다. -->
        <ui:VisualElement name="surface" class="pad-surface" />
        <!-- 내려놓기: 잡고 있는 동안만 보인다. 여기서 떼면 화살이 안 나간다. -->
        <ui:VisualElement name="lower-band" class="lower-band" picking-mode="Ignore">
            <ui:Label name="lower-label" class="lower-label" text="여기서 떼면 안 쏨" picking-mode="Ignore" />
        </ui:VisualElement>
        <!-- 조준점: 화면 한가운데, 시위가 걸린 동안만 뜬다. 바깥 원 + 가운데 점. -->
        <ui:VisualElement name="reticle" class="archery-reticle" picking-mode="Ignore">
            <ui:VisualElement name="reticle-dot" class="archery-reticle-dot" picking-mode="Ignore" />
        </ui:VisualElement>
        <ui:Label name="score" class="archery-score" text="0" picking-mode="Ignore" />
        <ui:Label name="arrows" class="archery-arrows" text="" picking-mode="Ignore" />
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: USS — `.pad-half`/`.pad-left`/`.pad-right`/`.draw-gauge`/`.draw-threshold`/`.draw-knob` 블록을 지우고 아래를 넣는다**

```css
/* 화면 전체가 조작면이다 — 왼쪽/오른쪽 구분이 없어졌다. */
.pad-surface {
    position: absolute;
    left: 0; right: 0; top: 0; bottom: 0;
}

/* 내려놓기 띠 — 잡고 있는 동안만 View가 켠다. 들어가면 붉어진다. */
.lower-band {
    position: absolute;
    left: 0; right: 0; bottom: 0;
    height: 15%;
    display: none;
    align-items: center;
    justify-content: center;
    background-color: rgba(20, 24, 26, 0.42);
    border-top-width: 1px;
    border-top-color: rgba(255, 255, 255, 0.28);
}
.lower-band.is-lowering {
    background-color: rgba(200, 54, 47, 0.30);
    border-top-color: rgba(200, 54, 47, 0.9);
}
.lower-label {
    font-size: 16px;
    color: rgba(255, 255, 255, 0.55);
}
.lower-band.is-lowering .lower-label {
    color: rgb(240, 169, 163);
}
```

> `.lower-band`의 `height: 15%`는 `ArcheryPadViewModel.LowerBandFraction`과 **같은 값이어야 한다**.
> 어긋나면 보이는 띠와 판정하는 띠가 달라진다. Step 5의 시험이 그걸 막는다.

- [ ] **Step 3: View — 포인터 하나로 배선한다**

`ArcheryPadView.cs`의 `OnOpen`에서 `left`/`right`/`_gauge`/`_gaugeThreshold`/`_gaugeKnob` 관련
선언·질의·콜백과 `UpdateGauge()`/`SetCircle()` 메서드를 **전부 삭제**하고, 아래로 교체:

```csharp
            var surface = Root.Q<VisualElement>("surface");
            _lowerBand = Root.Q<VisualElement>("lower-band");
            _score = Root.Q<Label>("score");
            _arrows = Root.Q<Label>("arrows");
            _reticle = Root.Q<VisualElement>("reticle");

            // 누르면 활이 올라온다 — 어디를 눌러도 된다.
            surface.RegisterCallback<PointerDownEvent>(evt =>
            {
                surface.CapturePointer(evt.pointerId);
                _viewModel.BeginDraw();
                _viewModel.UpdatePointer(Fraction(evt.position));
            });

            //  같은 손가락이 겨눈다. 끈 만큼(화면 대비 비율)을 조준으로 넘기고, 지금 자리는
            //  내려놓기 판정에 쓴다 — 두 가지를 한 번에 받는 유일한 자리다.
            surface.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (surface.HasPointerCapture(evt.pointerId) == false)
                {
                    return;
                }
                Vector2 size = PanelSize();
                if (size.x > 0f && size.y > 0f)
                {
                    _viewModel.LookBy(new Vector2(evt.deltaPosition.x / size.x,
                                                  evt.deltaPosition.y / size.y));
                }
                _viewModel.UpdatePointer(Fraction(evt.position));
            });

            surface.RegisterCallback<PointerUpEvent>(evt =>
            {
                surface.ReleasePointer(evt.pointerId);
                _viewModel.EndDraw();
            });
            // 손가락이 화면 밖으로 나가면 위의 Up이 안 온다 — 그대로 두면 활을 든 채 영영 멈춘다.
            surface.RegisterCallback<PointerCaptureOutEvent>(_ => _viewModel.EndDraw());
```

같은 파일에 헬퍼를 추가한다:

```csharp
        //  UI Toolkit의 evt.position은 **패널** 좌표다 — 그래서 패널을 잰다. Screen 픽셀과
        //  단위가 다를 수 있어 여기서 재는 것이 정확하다.
        private Vector2 PanelSize()
        {
            return Root.panel?.visualTree.layout.size ?? Vector2.zero;
        }

        private Vector2 Fraction(Vector2 panelPosition)
        {
            Vector2 size = PanelSize();
            if (size.x <= 0f || size.y <= 0f)
            {
                return new Vector2(0.5f, 0.5f);
            }
            return new Vector2(panelPosition.x / size.x, panelPosition.y / size.y);
        }
```

필드 선언에서 `_gauge`/`_gaugeThreshold`/`_gaugeKnob`를 지우고 `private VisualElement _lowerBand;`를
추가한다. **`_padRoot`는 만들지 않는다** — 포인터 좌표가 패널 기준이라 패널을 재야 한다.

- [ ] **Step 4: View — 매 프레임 갱신에서 게이지를 띠로 바꾼다**

`Root.schedule.Execute(...)` 안의 `UpdateGauge();` 호출을 아래로 교체:

```csharp
                //  띠는 잡고 있는 동안만 보인다 — 안 그러면 화면 아래가 늘 가려진다.
                bool holding = _viewModel.Drawing;
                _lowerBand.style.display = holding ? DisplayStyle.Flex : DisplayStyle.None;
                _lowerBand.EnableInClassList("is-lowering", holding && _viewModel.Lowering);
```

> 같은 루프의 `_viewModel.PollKeyboard();`는 **그대로 둔다** — 활을 걸지 않고 둘러볼 때 쓸 데가
> 있고 비용이 0이다(스펙 §3.5). 점수·화살 수·조준점 갱신도 그대로다.

- [ ] **Step 5: 띠 값이 두 곳에서 어긋나지 않는지 시험한다**

`LeagueOfPhysical-Client/Assets/Tests/Editor/ArcheryPadBandTests.cs`를 새로 만든다:

```csharp
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    //  띠의 높이가 코드(LowerBandFraction)와 USS 두 곳에 적혀 있다. 어긋나면 **보이는 띠와
    //  판정하는 띠가 달라져** "분명히 띠 밖에서 뗐는데 안 나간다"가 된다 — 화면상 아무 표시가
    //  없어 원인을 짚기 어려운 종류다. 그래서 둘을 대조한다.
    public class ArcheryPadBandTests
    {
        [Test]
        public void USS의_띠_높이가_코드의_값과_같다()
        {
            string uss = File.ReadAllText("Assets/UI/ArcheryPad/ArcheryPad.uss");
            int expected = Mathf.RoundToInt(LOP.UI.ArcheryPadViewModel.LowerBandFraction * 100f);
            StringAssert.Contains("height: " + expected + "%", uss,
                "USS의 .lower-band 높이가 LowerBandFraction과 다르다 — 보이는 띠와 판정이 어긋난다");
        }
    }
}
```

> `Assets/Tests/Editor`(asmdef 없음)에 두면 Assembly-CSharp 클래스를 표준 Test Runner로 시험할 수
> 있다(`[[client-test-infra-constraint]]`). 시험이 읽는 경로는 프로젝트 루트 기준이라 그대로 동작한다.

- [ ] **Step 6: 컴파일과 시험**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" recompile
# completed + errors:[] 확인 — Task 2에서 났던 에러가 전부 사라져야 한다
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" \
  run_tests --mode EditMode --filter "ArcheryPadBandTests"
```

기대: 컴파일 통과, 시험 통과.

- [ ] **Step 7: 커밋 (Task 2와 묶어서)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git checkout -b feature/archery-raise-and-aim
git add Assets/Scripts/UI/ArcheryPad/ArcheryPadViewModel.cs \
        Assets/Scripts/UI/ArcheryPad/ArcheryPadView.cs \
        Assets/UI/ArcheryPad/ArcheryPad.uxml Assets/UI/ArcheryPad/ArcheryPad.uss \
        Assets/Tests/Editor/ArcheryPadBandTests.cs Assets/Tests/Editor/ArcheryPadBandTests.cs.meta
git status --short   # 로컬 픽스처(Assets/Art, 폰트, DynamicsManager)가 안 섞였는지 확인
git commit   # 메시지: 손가락 하나로 들어 올려 겨눈다 — 게이지 제거, 내려놓기 띠 추가
```

---

## 머지·배포 (컨트롤러가 한다)

1. **LeagueOfPhysical-Shared** → main (푸시 규약: fetch → rebase --autostash → ff-only → --no-ff 머지)
2. **LeagueOfPhysical-Client** → main
3. **게임서버 재배포 필요** — Shared 상수가 바뀌므로 서버 이미지에 실려야 한다.
   `gh workflow run gameserver-deploy -R Baeinsoo/LeagueOfPhysical-Server`
4. **닿았는지 클러스터에서 확인** — 워크플로 성공만으로 끝내지 않는다:
   ArgoCD `backend`의 revision이 bump 커밋인가 → 아니면 `refresh=hard` →
   room-server 파드가 새 태그를 드는가 (`[[argocd-gitops-cluster-rebuild]]`).
   서버 레포 SHA가 안 바뀌면 태그도 그대로이니 **레지스트리 푸시 시각**으로 판정한다.

## 실물 확인 (스펙 §6)

1. 누르는 순간 조준점이 **안 튄다**
2. 올라오는 0.5초 동안 **이미 겨눌 수 있다**
3. 띠로 내려 떼면 **화살 수가 안 준다**
4. 아래로 겨누다 **실수로 띠에 안 걸린다** ← 스펙이 지목한 위험
5. 짧게 톡 쳐도 안 나간다(0.075초 미만)
