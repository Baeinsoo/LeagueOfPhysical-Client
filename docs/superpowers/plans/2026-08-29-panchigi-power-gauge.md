# 판치기 세기 게이지 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 누른 시간이 상한의 몇 %인지를 화면 왼쪽 세로 막대로 보여주고, 손가락에 가려 안 보이는 조준선을 없앤다.

**Architecture:** 게이지가 말하는 값은 **누른 시간 ÷ 상한** 하나다. 힘은 계산하지 않는다 — 계산하면 서버 커널의 사본이 생겨, 커널만 바뀌었을 때 게이지가 조용히 거짓말을 한다. 계산은 `PanchigiContactCollector`의 순수 메서드에 두어 EditMode에서 시험하고, `PanchigiStrikeInput`이 매 프레임 값을 들고 있다가 ViewModel이 읽어 간다(연속 상태는 pull — 이 프로젝트의 월드 규약과 같다).

**Tech Stack:** Unity 6000.3.16f1, UI Toolkit(UXML/USS), VContainer, NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-08-29-panchigi-power-gauge-design.md`

## Global Constraints

- **힘을 클라에서 계산하지 않는다.** 게이지 값은 `누른 시간 ÷ hold_time_max`뿐이다. `ForceMultiplier`·`HorizontalForceMultiplier`·`InfluenceRadius`를 게이지 경로에서 읽지 않는다.
- **여러 손가락일 때는 가장 오래 눌린 접촉점**의 시간을 쓴다. 새 손가락이 닿아도 눈금이 줄면 안 된다.
- **낙 경계선을 그리지 않는다.** 눈금·표식·문턱 표시를 넣지 않는다.
- **서버·마스터데이터·인프라 레포를 건드리지 않는다.** 이 슬라이스는 `LeagueOfPhysical-Client` 단일 레포다.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고 커밋 전 `git status --short`로 확인한다. 워킹트리에 늘 있는 로컬 픽스처(`Assets/Art` 서브모듈 포인터, 폰트 `.asset`)를 절대 커밋하지 않는다.
- **`.cs`를 만들면 짝 `.meta`를 함께 커밋한다.**
- **주석은 "왜"만, 쉬운 말로.** 코드로 자명한 것은 적지 않는다.
- **브랜치**: `feat/panchigi-power-gauge` (main 직접 커밋 금지).

---

## File Structure

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Game/PanchigiContactCollector.cs` | 접촉점 수집(기존) + **누른 시간 정규화(신규, 순수)** |
| `Assets/Tests/Editor/PanchigiChargeTests.cs` | 위 순수 메서드의 계약 고정 |
| `Assets/Scripts/Game/PanchigiStrikeInput.cs` | 입력 수집(기존) + **매 프레임 정규화 값 보유(신규)**, 조준선 제거 |
| `Assets/Scripts/UI/PanchigiTurn/PanchigiTurnViewModel.cs` | 게이지를 보일지·값이 얼마인지 |
| `Assets/Scripts/UI/PanchigiTurn/PanchigiTurnView.cs` | 값 → 막대 높이·색 |
| `Assets/UI/PanchigiTurn/PanchigiTurn.uxml` / `.uss` | 막대 마크업·스타일 |

---

### Task 1: 누른 시간을 0~1로 (순수 계산 + 테스트)

**Files:**
- Modify: `Assets/Scripts/Game/PanchigiContactCollector.cs`
- Create: `Assets/Tests/Editor/PanchigiChargeTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: 기존 `PanchigiContactCollector.Aim.PressTime`, `Begin/Update/End/Clear`
- Produces: `public float ChargeNormalized(float now, float holdTimeMax)` — 0~1. Task 2가 이것을 읽는다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/Editor/PanchigiChargeTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 게이지가 말하는 값을 고정한다. 이 값은 <b>누른 시간</b>이지 힘이 아니다 — 힘을 클라에서
    /// 다시 계산하면 서버 커널의 사본이 생겨, 커널만 바뀌었을 때 화면이 조용히 거짓말을 한다.
    /// </summary>
    public class PanchigiChargeTests
    {
        private const float HoldMax = 1f;

        private static PanchigiContactCollector Collector() => new PanchigiContactCollector(4);

        [Test]
        public void 아무도_안_눌렀으면_0이다()
        {
            Assert.AreEqual(0f, Collector().ChargeNormalized(10f, HoldMax), 1e-5f);
        }

        [Test]
        public void 절반만큼_눌렀으면_절반이다()
        {
            var c = Collector();
            c.Begin(1, Vector3.zero, 10f);

            Assert.AreEqual(0.5f, c.ChargeNormalized(10.5f, HoldMax), 1e-5f);
        }

        [Test]
        public void 상한을_넘겨_눌러도_1을_안_넘는다()
        {
            //  서버는 상한 초과를 클램프가 아니라 거절한다. 클라도 상한에서 자르므로
            //  화면이 더 차오르면 실제보다 세 보이는 거짓말이 된다.
            var c = Collector();
            c.Begin(1, Vector3.zero, 10f);

            Assert.AreEqual(1f, c.ChargeNormalized(14f, HoldMax), 1e-5f);
        }

        [Test]
        public void 손가락이_더_닿아도_눈금이_안_준다()
        {
            //  늦게 닿은 손가락은 항상 더 짧다. 그걸 기준으로 삼으면 손가락을 하나씩
            //  드르륵 댈 때마다 눈금이 뒤로 밀린다.
            var c = Collector();
            c.Begin(1, Vector3.zero, 10f);
            c.Begin(2, Vector3.zero, 10.4f);

            Assert.AreEqual(0.6f, c.ChargeNormalized(10.6f, HoldMax), 1e-5f);
        }

        [Test]
        public void 손을_다_떼면_0으로_돌아간다()
        {
            //  다음 조준이 반쯤 찬 채로 시작하면 안 된다.
            var c = Collector();
            c.Begin(1, Vector3.zero, 10f);
            c.End(1, Vector3.zero, 10.5f, HoldMax, 3f);

            Assert.AreEqual(0f, c.ChargeNormalized(10.6f, HoldMax), 1e-5f);
        }

        [Test]
        public void 상한이_0이면_0이다()
        {
            //  마스터데이터가 잘못 들어와도 0으로 나눠 NaN이 화면에 흘러가면 안 된다.
            var c = Collector();
            c.Begin(1, Vector3.zero, 10f);

            Assert.AreEqual(0f, c.ChargeNormalized(10.5f, 0f), 1e-5f);
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
unity cmd --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" \
  run_tests --mode EditMode --filter "LOP.Tests.PanchigiChargeTests"
```

기대: **컴파일 에러** — `ChargeNormalized`가 아직 없다.

- [ ] **Step 3: 최소 구현을 넣는다**

`PanchigiContactCollector.cs`의 `IsComplete` 아래에 추가:

```csharp
        /// <summary>
        /// 지금 얼마나 눌렀나 — 0~1. <b>가장 오래 눌린 손가락</b> 기준이다. 늦게 닿은 쪽을
        /// 보면 손가락을 하나씩 드르륵 댈 때마다 눈금이 뒤로 밀린다.
        /// </summary>
        public float ChargeNormalized(float now, float holdTimeMax)
        {
            if (holdTimeMax <= 0f || pressed.Count == 0)
            {
                return 0f;
            }

            float longest = 0f;
            for (int i = 0; i < pressed.Count; i++)
            {
                float held = now - pressed[i].PressTime;
                if (held > longest)
                {
                    longest = held;
                }
            }
            return Mathf.Clamp01(longest / holdTimeMax);
        }
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```bash
unity cmd --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" \
  run_tests --mode EditMode --filter "LOP.Tests.PanchigiChargeTests"
```

기대: 6개 전부 PASS.

- [ ] **Step 5: 이빨을 확인한다**

`ChargeNormalized`에서 `Mathf.Clamp01`을 빼고 Step 4를 다시 돌려 **`상한을_넘겨_눌러도_1을_안_넘는다`가 실패하는지** 본다. 이어서 `longest`를 "가장 짧은 것"으로 바꿔 **`손가락이_더_닿아도_눈금이_안_준다`가 실패하는지** 본다. 둘 다 확인했으면 **원래대로 되돌린다.**

> 통과만 확인하면 아무것도 안 재는 테스트를 못 걸러낸다. 이 프로젝트에서 실제로 겪은 실수다.

- [ ] **Step 6: 커밋**

```bash
git status --short
git add Assets/Scripts/Game/PanchigiContactCollector.cs \
        Assets/Tests/Editor/PanchigiChargeTests.cs \
        Assets/Tests/Editor/PanchigiChargeTests.cs.meta
git commit -m "feat(panchigi): 누른 시간을 0~1로 재는 계산을 더한다"
```

---

### Task 2: 게이지를 화면 왼쪽에 그린다

**Files:**
- Modify: `Assets/Scripts/Game/PanchigiStrikeInput.cs`
- Modify: `Assets/Scripts/UI/PanchigiTurn/PanchigiTurnViewModel.cs`
- Modify: `Assets/Scripts/UI/PanchigiTurn/PanchigiTurnView.cs`
- Modify: `Assets/UI/PanchigiTurn/PanchigiTurn.uxml`
- Modify: `Assets/UI/PanchigiTurn/PanchigiTurn.uss`

**Interfaces:**
- Consumes: Task 1의 `PanchigiContactCollector.ChargeNormalized(now, holdTimeMax)`
- Produces: `PanchigiStrikeInput.Charge` (float 0~1), `PanchigiTurnViewModel.Charge()` (float 0~1, 내 조준 차례가 아니면 0), `PanchigiTurnViewModel.IsCharging()` (bool)

> **색에 대한 메모.** 시안은 아래 파랑 → 위 노랑 **그라데이션**이었지만 UI Toolkit USS에는 그라데이션이 없다. **채움 색 자체를 세기에 따라 파랑↔노랑으로 옮기는 것**으로 대신한다 — 얇은 막대에서는 보기에 거의 같고, "정도만 컬러로"라는 요구에 오히려 더 곧다. 텍스처 에셋도 필요 없다.

- [ ] **Step 1: `PanchigiStrikeInput`이 값을 들고 있게 한다**

`private PanchigiContactCollector collector;` 아래에 필드와 프로퍼티를 추가한다:

```csharp
        //  ViewModel이 매 프레임 읽어 간다. Time.time을 순수 C# 쪽으로 넘기지 않으려고
        //  여기서 재 둔다.
        private float charge;

        /// <summary>지금 얼마나 눌렀나 — 0~1. 아무도 안 누르고 있으면 0.</summary>
        public float Charge => charge;
```

`Update()`에서 config를 얻은 직후, 그리고 내 차례가 아닐 때 빠져나가는 분기보다 **먼저** 갱신한다:

```csharp
            charge = collector?.ChargeNormalized(Time.time, config.HoldTimeMax) ?? 0f;
```

> 차례가 아닐 때 `collector`가 `Clear()`되므로 값은 자연히 0이 된다. 분기 앞에 두는 이유는 차례가 넘어간 프레임에 **직전 값이 남지 않게** 하기 위해서다.

- [ ] **Step 2: ViewModel이 보일지·얼마인지를 정한다**

먼저 **이 ViewModel을 등록하는 곳이 몇 군데인지** 확인한다. 생성자에 의존을 더하면
등록처마다 고쳐야 하는데, 한 곳을 빠뜨리면 **컴파일은 통과하고 그 게임만 못 들어간다.**

```bash
grep -rn "PanchigiTurnViewModel" Assets/Scripts
```

기대: 등록은 `PanchigiLifetimeScope` 한 곳. 두 곳 이상이면 **전부** 고친다.

`PanchigiStrikeInput`은 이미 같은 스코프가 `RegisterComponent(strikeInput)`으로 등록해
두었으므로 배선을 새로 더할 것은 없다. 생성자에 인자만 늘린다:

```csharp
        private readonly LOP.MasterData.LOPMasterData masterData;
        private readonly PanchigiStrikeInput strikeInput;

        public PanchigiTurnViewModel(PanchigiStateStore store, IPlayerContext playerContext, IRunner runner,
            GameFramework.World.EntityRegistry entityRegistry, LOP.MasterData.LOPMasterData masterData,
            PanchigiStrikeInput strikeInput)
        {
            this.store = store;
            this.playerContext = playerContext;
            this.runner = runner;
            this.entityRegistry = entityRegistry;
            this.masterData = masterData;
            this.strikeInput = strikeInput;
        }
```

그리고 아래 두 메서드를 더한다:

```csharp
        /// <summary>게이지를 띄울 때인가 — 내 조준 차례일 때만.</summary>
        public bool IsCharging()
        {
            return store.IsEliminated(playerContext.entityId) == false
                && store.Phase.CurrentValue == AimingPhase
                && store.CurrentEntityId.CurrentValue == playerContext.entityId;
        }

        /// <summary>막대가 얼마나 찼나 — 0~1.</summary>
        public float Charge() => IsCharging() ? strikeInput.Charge : 0f;
```

- [ ] **Step 3: 마크업을 더한다**

`Assets/UI/PanchigiTurn/PanchigiTurn.uxml`의 `panchigi-turn` **바깥**(형제)로 막대를 넣는다 — 기존 컨테이너는 화면 위쪽 가운데 정렬이라 그 안에 넣으면 같이 끌려간다:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="PanchigiTurn.uss" />
    <ui:VisualElement name="panchigi-turn" class="panchigi-turn">
        <ui:Label name="turn-label" class="turn-label" text="" />
        <ui:Label name="flip-label" class="flip-label" text="" />
        <ui:Label name="dropout-label" class="dropout-label" text="" />
    </ui:VisualElement>
    <ui:VisualElement name="charge-track" class="charge-track">
        <ui:VisualElement name="charge-fill" class="charge-fill" />
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 4: 스타일을 더한다**

`Assets/UI/PanchigiTurn/PanchigiTurn.uss` 끝에:

```css
.charge-track {
    position: absolute;
    left: 24px;
    top: 30%;
    height: 40%;
    width: 18px;
    border-radius: 9px;
    background-color: rgba(255, 255, 255, 0.08);
    border-width: 1px;
    border-color: rgba(255, 255, 255, 0.16);
    justify-content: flex-end;
}

.charge-fill {
    width: 100%;
    height: 0;
    border-radius: 9px;
}
```

> `justify-content: flex-end`가 채움을 **아래에서 위로** 자라게 한다. 위에서 내려오면 "차오른다"로 안 읽힌다.

- [ ] **Step 5: View가 값을 그리게 한다**

`PanchigiTurnView.OnOpen`의 스케줄러에 막대 갱신을 더한다:

```csharp
            var chargeTrack = Root.Q<VisualElement>("charge-track");
            var chargeFill = Root.Q<VisualElement>("charge-fill");
            _tick = Root.schedule.Execute(_ =>
            {
                turnLabel.text = _viewModel.Label();
                flipLabel.text = _viewModel.FlipLabel();
                dropOutLabel.text = _viewModel.DropOutLabel();

                bool charging = _viewModel.IsCharging();
                chargeTrack.style.display = charging ? DisplayStyle.Flex : DisplayStyle.None;
                if (charging)
                {
                    float t = _viewModel.Charge();
                    chargeFill.style.height = Length.Percent(t * 100f);
                    chargeFill.style.backgroundColor = Color.Lerp(Calm, Hot, t);
                }
            }).Every(0);
```

클래스에 색 상수를 둔다(파일 상단 `using UnityEngine;` 필요):

```csharp
        //  약할 땐 차분한 파랑, 셀수록 더운 노랑 — 높이만으로는 곁눈질에 잘 안 읽힌다.
        private static readonly Color Calm = new Color(0.36f, 0.58f, 0.78f);
        private static readonly Color Hot = new Color(0.85f, 0.64f, 0.25f);
```

- [ ] **Step 6: 컴파일과 기존 테스트를 확인한다**

```bash
unity cmd --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" recompile
unity cmd --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" run_tests --mode EditMode
```

기대: 컴파일 에러 0, EditMode 전부 PASS(Task 1의 6개 포함).

> `recompile`의 `failed:false`만 보면 안 된다. `status`가 `up_to_date`면 **재컴파일을 안 한 것**이다 — 콘솔의 CS 에러를 시각과 함께 확인한다.

- [ ] **Step 7: 커밋**

```bash
git status --short
git add Assets/Scripts/Game/PanchigiStrikeInput.cs \
        Assets/Scripts/UI/PanchigiTurn/PanchigiTurnViewModel.cs \
        Assets/Scripts/UI/PanchigiTurn/PanchigiTurnView.cs \
        Assets/UI/PanchigiTurn/PanchigiTurn.uxml \
        Assets/UI/PanchigiTurn/PanchigiTurn.uss
git commit -m "feat(panchigi): 누른 세기를 화면 왼쪽 막대로 보여준다"
```

---

### Task 3: 조준선을 없앤다

**Files:**
- Modify: `Assets/Scripts/Game/PanchigiStrikeInput.cs`
- Modify: `Assets/Scripts/Game/PanchigiContactCollector.cs` (주석만)

**Interfaces:**
- Consumes: 없음
- Produces: 없음 (삭제)

- [ ] **Step 1: 무엇이 사라지는지 먼저 적는다**

지우기 전에 `PanchigiStrikeInput.cs`에서 조준선에 딸린 것을 **전부 나열한다.** 최소 다음이 있다:

- `[SerializeField] private LineRenderer aimLine;`
- `private LineRenderer[] aimLines;`
- `Awake()`의 `aimLine.enabled = false;`
- `OnDisable()`·`OnDestroy()`의 조준선 정리
- `HideAllAimLines()`와 조준선을 그리는 코드·풀 확장 코드

```bash
grep -n "aimLine\|AimLine\|LineRenderer" Assets/Scripts/Game/PanchigiStrikeInput.cs
```

나열한 목록을 지운 뒤 **같은 grep이 0건**이 되어야 한다.

- [ ] **Step 2: 남은 참조를 반대 방향으로 훑는다**

"옛 이름이 남았나"가 아니라 **"없앤 것을 아직 부르는 곳이 있나"**를 본다.

```bash
grep -rn "aimLine\|HideAllAimLines" Assets/Scripts Assets/Editor
grep -rn "Pressed" Assets/Scripts
```

`Pressed`는 게이지가 쓰지 않는다(게이지는 `ChargeNormalized`를 부른다). 그래도 다른 소비처가 있는지 확인하고, 없으면 **`Aim` 구조체의 주석 "조준선을 그리는 데 쓴다"를 사실에 맞게 고친다** — `Pressed` 자체는 `ChargeNormalized`가 안에서 쓰므로 지우지 않는다.

- [ ] **Step 3: 지운다**

Step 1에서 나열한 것을 전부 지운다. `aimCamera`는 **남긴다** — 터치 좌표를 판 위 점으로 바꾸는 데 계속 쓰인다.

- [ ] **Step 4: 컴파일·테스트**

```bash
unity cmd --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" recompile
unity cmd --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" run_tests --mode EditMode
```

기대: 컴파일 에러 0, EditMode 전부 PASS.

- [ ] **Step 5: 씬에 남은 오브젝트를 보고한다 — 지우지 않는다**

`aimLine`은 씬에 배선된 `LineRenderer`였다. 필드를 지우면 그 오브젝트는 **씬에 남아 아무것도 안 하는 상태**가 된다.

```bash
grep -rn "LineRenderer" Assets/Scenes/*.unity | head
```

씬 파일은 에디터가 물고 있어 손으로 고치면 어긋난다. **찾은 것을 보고만 하고 지우지 않는다** — 사용자가 에디터에서 지운다.

- [ ] **Step 6: 커밋**

```bash
git status --short
git add Assets/Scripts/Game/PanchigiStrikeInput.cs \
        Assets/Scripts/Game/PanchigiContactCollector.cs
git commit -m "refactor(panchigi): 손가락에 가려 안 보이는 조준선을 없앤다"
```

---

## 마무리 — 인게임 확인

코드가 다 끝난 뒤 사용자와 함께 확인한다. **자동으로 못 재는 것이 섞여 있다.**

- **게이지가 차오르나** — 누르고 있으면 아래에서 위로. 두 에디터 중 내 차례인 쪽에서.
- **손을 떼면 0으로 돌아가나** — 다음 조준이 반쯤 찬 채로 시작하면 안 된다.
- **손가락을 드르륵 대도 눈금이 안 밀리나** — **터치 기기에서만 확인 가능**하다. 마우스는 손가락이 하나뿐이라 프로브로도 재현되지 않는다.
- **조준선이 안 보이나** — 그리고 없어도 칠 만한가.

앞의 둘은 `PanchigiPlayProbe`로 몰아볼 수 있고, 뒤의 둘은 **사용자 손이 필요하다.**

> ⚠️ **계측 함정.** 동전 좌표가 초기값이라고 "안 움직였다"로 읽지 말 것 — 판 밖으로 날아갔다가 낙 복귀로 되돌아온 것과 구분되지 않는다. 낙 카운트는 **친 쪽 클라**에서 읽어야 한다(보고 있는 쪽 값은 그 사람 것이다). 약하게 쳐서 왕복이 안 일어나게 하면 변위가 남는다.

## 다음 슬라이스를 위한 측정

게이지가 생겼으므로 이제 잴 수 있다. 이 슬라이스에서 **하지 않지만**, 이어서 할 때 필요한 값이다.

- **몇 %에서 동전이 뒤집히기 시작하나**
- **몇 %에서 낙이 나기 시작하나**

두 값이 나오면 스펙 §6의 미뤄 둔 셋(세기 곡선 / 힘 나누기 / 규칙 구멍)의 판단이 선다.
