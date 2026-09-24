# Flappy 지름길 입구 — 이어진 A자 굴 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 지름길 입구를 "날갯짓 호 모양 그대로 판 좁은 굴"로 바꾼다. 구간 2는 쉬운 굴(호 1개·두께 3.2m·턱 4m), 구간 3은 어려운 굴(호 3개·두께 2.6m·턱 6m). 굴 끝은 지금의 곧은 지름길(패드 포함)로 이어진다.

**Architecture:** 순수 계층(`LOP.MapTools`)에 날갯짓 한 번의 **틱 궤적**(`FlapArc`)과 입구 난이도(`ShortcutEntrance`)를 더하고, `ShortcutRect`가 굴 가운데선(`ChannelCenterAt`)을 계산한다. 굴을 판 지붕·혀는 오목해서 한 덩어리 볼록 콜라이더로 만들면 굴이 메워지므로, 순수 함수 `ShortcutRoof`/`ShortcutTongue`가 **세로로 자른 볼록 사각형 띠** 목록을 내고 빌더는 띠마다 `Prism`을 굽는다. 검사기·탐색은 바꾸지 않는다.

**Tech Stack:** Unity 6000.3 (C#), NUnit EditMode, `unity` CLI(`~/.unity/bin/unity`).

**Spec:** `docs/superpowers/specs/2026-09-24-flappy-terrain-shortcut-design.md` §13 (앞 절 §1–§12는 이 지름길의 바탕)

## Global Constraints

- 물리 값은 MasterData `FlappyConfig` 그대로: 날갯짓 18.6, 중력 59, 최대 낙하 30, 전진 6.8m/s, 틱 0.02s. **이 값을 코드에 새로 박지 않는다** — 빌더는 `config`에서 읽어 `FlapArc`를 만든다(테스트만 숫자를 쓴다).
- 굴은 **틱 궤적**을 따른다: 날갯짓 틱부터 t초 뒤 높이 = (v + g·dt/2)·t − g·t²/2. 한 호 = **32틱 = 4.352m**, 호마다 **+0.198m**, 꼭대기 **3.12m**. 연속 포물선(꼭대기 2.93m)으로 파면 호 끝에서 0.37m 어긋난다.
- 난이도 표: 구간 2 = 호 **1** · 두께 **3.2m** · 턱 **4m** / 구간 3 = 호 **3** · 두께 **2.6m** · 턱 **6m**. 구간 1은 지름길 없음(그대로).
- 입구(`X0`) = 내리막 천장선이 **굴 윗면**을 지나는 곳. 굴 가운데선 시작 높이 = 곧은 길 가운데(`CenterY`). 출구(`X1`)·곧은 길(`Y0`~`Y1`, 세로 폭 = 창 4.37m)은 전과 같다.
- 턱: 입구 바로 밑 혀 앞면이 **세로 벽**(굴 바닥에서 턱 길이만큼 아래까지). 턱 아래 계곡 틈은 **6m 이상**.
- 패드는 **곧은 길 안에만**(왼끝 > 굴 끝), 부스트가 출구 1.5m 전에 끝나는 자리 — 기존 규칙 그대로.
- 지붕·혀는 **세로 띠(볼록 사각형)** 로만 굽는다. 띠 경계는 0.25m마다 + **호 경계마다**(호 경계는 꺾인 점이라 띠가 걸치면 굴을 0.3m 넘게 파먹는다).
- 런타임·서버·Shared 변경 없음. 바뀌는 곳: `Assets/Scripts/MapTools/`, `Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs`, `Assets/Tests/EditMode/MapTools/`, 맵 씬(Art 서브모듈), `docs/ROADMAP.md`.
- **새 파일을 만들지 않는다**(새 `.meta` 불필요). `.meta`는 유니티가 만든 것만 커밋.
- 맵 씬 표시 `Shortcut_{X0}`는 **Transform만** 있는 빈 GameObject 그대로(서버가 씬을 읽는다).
- `git add -A` / `git commit -a` 금지. 커밋 전 `git status --short`. 로컬 픽스처(`Assets/Art` 포인터 — 의도적으로 올릴 때 제외, `Assets/UI/Theme/Fonts/Jua-Regular SDF.asset`, `ProjectSettings/PackageManagerSettings.asset`, `ProjectSettings/ProjectSettings.asset`)는 스테이지 금지.
- 작업 브랜치 `feature/flappy-shortcut-arch-entrance`(이미 있음, spec 커밋 `e957941e`). main 직접 커밋 금지. 푸시는 CLAUDE.md "푸시 규약" 순서로 **한 줄씩**, force 금지.
- 플레이 모드를 임의로 끄지 않는다. 유니티 명령 전에 재생 중인지 확인하고, 재생 중이면 멈추고 사용자에게 알린다.
- 코스 굽기 뒤에는 **곧바로 `unity cmd save_scene`**.
- 주석: 쉬운 한국어, 코드로 자명한 것엔 달지 않고 "왜"만.

### 명령 모음 (모든 태스크 공통)

```bash
. "$HOME/.unity/env"      # 또는 export PATH="$PATH:$HOME/.unity/bin"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
# 재생 중인지 먼저
unity cmd eval --project-path $P --code 'UnityEngine.Debug.Log("playing=" + UnityEditor.EditorApplication.isPlaying);'; unity cmd console --project-path $P --tail 3
# 컴파일
unity cmd recompile --project-path $P; until timeout 50 unity cmd recompile_status --project-path $P 2>&1 | grep -qE '"completed"|"up_to_date"'; do sleep 6; done; timeout 50 unity cmd recompile_status --project-path $P
# 전체 EditMode 테스트 (결과는 Editor.log와 TestResults.xml로 읽는다 — test_status는 비어 올 때가 있다)
before=$(grep -c "Run finished" ~/Library/Logs/Unity/Editor.log); unity cmd run_tests --project-path $P --detach; until [ "$(grep -c 'Run finished' ~/Library/Logs/Unity/Editor.log)" -gt "$before" ]; do sleep 10; done; grep "Run finished" ~/Library/Logs/Unity/Editor.log | tail -1
python3 -c "
import re; s=open('/Users/insoobae/Library/Application Support/BSGames/LeagueOfPhysical-Client/TestResults.xml',encoding='utf-8',errors='replace').read()
[print('RED:', re.search(r'fullname=\"([^\"]+)\"', m.group(1)).group(1)) for m in re.finditer(r'<test-case([^>]*result=\"Failed\"[^>]*)>', s)]"
# 맵 씬 열기 · 굽기 · 저장
unity cmd open_scene --project-path $P Assets/Art/Scenes/FlappyRaceMap.unity
unity cmd menu --project-path $P "LOP/Debug/Flappy 전통 코스 굽기"; unity cmd save_scene --project-path $P
# 빠른 검사 (30분 넘게 걸린다 — 띄워 두고 Logs/FlappyMapCheck.txt의 수정 시각이 바뀌길 기다린다)
unity cmd menu --project-path $P "LOP/Debug/Flappy 맵 검사 (빠름 — 위상 훑기 건너뜀)"
```

컴파일 에러가 이 작업과 무관한 타입(MasterData·Shared·Archery 등)에서 나면 형제 레포가 뒤처진 것이다 — `LeagueOfPhysical-Shared`, `LeagueOfPhysical-MasterData-Client`, `GameFramework`에서 `git pull --ff-only` 후 다시 컴파일.

### 이 플랜이 spec과 다르게/더 정한 것 (판단)

| 판단 | 이유 | 틀렸을 때 비용 |
|---|---|---|
| 굴 두께는 **세로로** 잰다(가운데선 ± 두께/2) | 띠가 세로라 세로 폭이 곧 판정 폭. 기울어진 곳에선 수직 폭이 조금 좁아져 더 어렵다 — 의도(어려운 입구)와 같은 방향 | 측정(Task 2)에서 제때 친 궤적이 닿으면 두께를 올린다 |
| 턱 아랫면 = 입구의 턱 끝에서 계곡 바닥 시작점 천장까지 곧은 선 | 턱 아래 계곡 틈이 입구(≥6m)부터 바닥(21.84m)까지 넓어지기만 한다 | 선 하나 |
| 곧은 길 최소 길이 2m를 규칙으로 둔다(`MinStraight`) | 호를 늘리다 굴이 출구를 넘어가는 실수를 던져서 잡는다 | 상수 하나 |

---

## File Structure

| 파일 | 할 일 |
|---|---|
| `Assets/Scripts/MapTools/CourseProfile.cs` (수정) | `FlapArc`·`ShortcutEntrance` 추가, `SectionTerrain.Entrance`, `ShortcutRect` 개편(굴 가운데선·턱), `ValleyShortcut`·`Compose`에 입구·호 인자, 띠 함수 `ShortcutRoof`/`ShortcutTongue` |
| `Assets/Scripts/MapTools/ShortcutRule.cs` (수정) | `PadCenterX`가 굴 끝 뒤에만 놓는다 |
| `Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs` (수정) | 프로필 조립을 `ComposeProfile`로 빼서 공개, 지붕·혀를 띠로 굽는다, 입구 로그 |
| `Assets/Tests/EditMode/MapTools/CourseProfileTests.cs` (수정) | 호·굴·띠·턱·던짐 시험 |
| `Assets/Tests/EditMode/MapTools/ShortcutRuleTests.cs` (수정) | 새 `ShortcutRect` 모양으로 |
| `docs/ROADMAP.md` (수정) | 이번 개정 기록 |

---

### Task 1: 굴 기하 — 틱 호·입구·띠 (순수 계층 + 빌더가 띠를 굽는다)

**Files:**
- Modify: `Assets/Scripts/MapTools/CourseProfile.cs`
- Modify: `Assets/Scripts/MapTools/ShortcutRule.cs:55-61`
- Modify: `Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs:115-117` (Compose 호출), `:298-333` (`Shortcuts`)
- Test: `Assets/Tests/EditMode/MapTools/CourseProfileTests.cs`, `Assets/Tests/EditMode/MapTools/ShortcutRuleTests.cs`

**Interfaces:**
- Consumes: `FlappyTickMath.NextVerticalSpeed(float vy, bool flap, float impulse, float gravity, float maxFall, float dt)`, `FlappyTickMath.AdvanceHeight(float y, float vy, float dt)` (둘 다 `LOP.MapTools`, `CleanRunSearch.cs`) — 테스트가 진짜 커널과 같은 틱 계산으로 비교할 때.
- Produces (Task 2·3이 쓴다):
  - `public readonly struct FlapArc(float flapImpulse, float gravity, float forwardSpeed, float tickSeconds)` — `EffectiveImpulse`, `TicksPerArc`, `Span`, `HeightAt(float dx)`, `RisePerArc`, `Apex`
  - `public readonly struct ShortcutEntrance(int arcs, float thickness, float lip)` — `Arcs`, `Thickness`, `Lip`
  - `SectionTerrain.Entrance`
  - `ShortcutRect` 필드 `X0, ChannelEnd, X1, Y0, Y1, Entrance, Arc, TongueEnd, ValleyBottom0, ValleyBottom1, BottomCeiling, RiseSlope`, 속성 `CenterY, Length, LipBottom`, 메서드 `ChannelCenterAt(float x)`, `FromCenterSize(cx, cy, w, h)`
  - `CourseProfileRule.ValleyShortcut(x0, baseY, depth, riseSlope, corridorHalf, window, ShortcutEntrance entrance, FlapArc arc)`
  - `CourseProfileRule.Compose(startX, length, spacing, corridorHalf, window, seed, leadIn, tail, FlapArc arc)`
  - `CourseProfileRule.ShortcutRoof(ShortcutRect r, float wallThickness, float step)` / `ShortcutTongue(ShortcutRect r, float step)` → `List<float[]>`, 각 원소는 8개 = (a,아래a)(b,아래b)(b,위b)(a,위a) 반시계
  - `CourseProfileRule.MinStraight = 2f`, `MinValleyGap = 6f`

- [ ] **Step 1: 실패하는 시험을 쓴다 — `CourseProfileTests.cs`**

맨 위 상수 블록에 호를 더하고 `Compose` 도우미를 바꾼다:

```csharp
        //  FlappyConfig 값(날갯짓 18.6 · 중력 59 · 전진 6.8 · 틱 0.02). 시험은 숫자를 직접 쓴다.
        static readonly FlapArc Arc = new FlapArc(18.6f, 59f, 6.8f, 0.02f);
        const float MaxFall = 30f;

        static CourseProfile Compose(ulong seed = 20260919UL)
            => CourseProfileRule.Compose(StartX, Length, Spacing, Half, Window, seed, LeadIn, Tail, Arc);
```

`지름길_입구와_출구에서_천장이_지름길_윗면과_만난다`의 입구 줄을 굴 윗면으로 바꾼다:

```csharp
                float mouthTop = r.ChannelCenterAt(r.X0) + r.Entrance.Thickness * 0.5f;
                Assert.AreEqual(mouthTop, p.CenterAt(r.X0) + Half, 1e-3f, "입구 = 천장이 굴 윗면을 지나는 곳");
```

`혀는_지름길_아래에_있고_두께가_최소_이상이다` 시험은 **지우고**(혀 다각형이 없어진다) 아래 시험들로 대신한다. `얕은_U에는_지름길을_못_낸다`는 인자를 늘린다:

```csharp
        [Test]
        public void 얕은_U에는_지름길을_못_낸다()
        {
            //  깊이 절반에 창을 뚫으면 혀가 남아야 한다. 20m면 혀가 없다.
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => CourseProfileRule.ValleyShortcut(0f, 0f, 20f, 1.3f, Half, Window, Easy, Arc));
        }
```

`관문은_평지에서만…`의 마지막 줄 `(r.X0 + r.Tongue[2]) * 0.5f` → `(r.X0 + r.ValleyBottom0) * 0.5f`.

그리고 새 시험들을 더한다(클래스 안, 도우미 포함):

```csharp
        static readonly ShortcutEntrance Easy = new ShortcutEntrance(1, 3.2f, 4f);
        static readonly ShortcutEntrance Hard = new ShortcutEntrance(3, 2.6f, 6f);

        [Test]
        public void 날갯짓_호는_틱_궤적의_숫자다()
        {
            Assert.AreEqual(32, Arc.TicksPerArc);
            Assert.AreEqual(4.352f, Arc.Span, 1e-3f);
            Assert.AreEqual(0.198f, Arc.RisePerArc, 2e-3f);
            Assert.AreEqual(3.12f, Arc.Apex, 1e-2f);
        }

        [Test]
        public void 호_높이는_진짜_커널을_틱마다_돌린_높이와_같다()
        {
            //  떨어지는 중(−12m/s)에 첫 틱에 친다 — 날갯짓은 세로 속도를 덮어쓰므로 그 전 속도는 상관없다.
            float vy = -12f, y = 0f;
            for (int n = 1; n <= Arc.TicksPerArc; n++)
            {
                vy = FlappyTickMath.NextVerticalSpeed(vy, n == 1, 18.6f, 59f, MaxFall, 0.02f);
                y = FlappyTickMath.AdvanceHeight(y, vy, 0.02f);
                Assert.AreEqual(y, Arc.HeightAt(n * 6.8f * 0.02f), 1e-3f, $"{n}틱");
            }
        }

        [Test]
        public void 구간마다_입구_난이도가_다르다()
        {
            Assert.IsFalse(CourseProfileRule.Sections[0].ValleyShortcut);
            ShortcutEntrance s2 = CourseProfileRule.Sections[1].Entrance;
            ShortcutEntrance s3 = CourseProfileRule.Sections[2].Entrance;
            Assert.AreEqual((1, 3.2f, 4f), (s2.Arcs, s2.Thickness, s2.Lip), "구간 2 = 쉬운 굴");
            Assert.AreEqual((3, 2.6f, 6f), (s3.Arcs, s3.Thickness, s3.Lip), "구간 3 = 어려운 굴");
        }

        [Test]
        public void 입구_굴_가운데선은_호마다_날갯짓한_새의_틱_궤적이다()
        {
            var arcs = new List<int>();
            foreach (ShortcutRect r in Compose().Shortcuts)
            {
                arcs.Add(r.Entrance.Arcs);
                float vy = -12f, y = r.CenterY, x = r.X0;
                int ticks = r.Entrance.Arcs * r.Arc.TicksPerArc;
                for (int n = 0; n < ticks; n++)
                {
                    vy = FlappyTickMath.NextVerticalSpeed(vy, n % r.Arc.TicksPerArc == 0, 18.6f, 59f, MaxFall, 0.02f);
                    y = FlappyTickMath.AdvanceHeight(y, vy, 0.02f);
                    x += 6.8f * 0.02f;
                    Assert.AreEqual(y, r.ChannelCenterAt(x), 2e-3f, $"x0={r.X0:F0} {n + 1}틱");
                }
                Assert.AreEqual(r.ChannelEnd, x, 1e-3f, "굴은 마지막 호에서 끝난다");
            }
            CollectionAssert.AreEquivalent(new[] { 1, 3 }, arcs, "쉬운 굴 하나, 어려운 굴 하나");
        }

        [Test]
        public void 호가_너무_많거나_턱이_너무_길거나_굴이_창보다_넓으면_던진다()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => CourseProfileRule.ValleyShortcut(0f, 0f, 30f, 1.3f, Half, Window, new ShortcutEntrance(10, 3.2f, 4f), Arc),
                "굴이 출구를 넘는다");
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => CourseProfileRule.ValleyShortcut(0f, 0f, 30f, 1.3f, Half, Window, new ShortcutEntrance(1, 3.2f, 20f), Arc),
                "턱 아래 계곡이 막힌다");
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => CourseProfileRule.ValleyShortcut(0f, 0f, 30f, 1.3f, Half, Window, new ShortcutEntrance(1, 5f, 4f), Arc),
                "굴이 곧은 길보다 넓다");
        }

        static bool Inside(List<float[]> strips, float x, float y)
        {
            foreach (float[] s in strips)
            {
                if (x < s[0] || x > s[2]) { continue; }
                float t = (x - s[0]) / (s[2] - s[0]);
                float bottom = s[1] + (s[3] - s[1]) * t;
                float top = s[7] + (s[5] - s[7]) * t;
                if (y >= bottom && y <= top) { return true; }
            }
            return false;
        }

        static IEnumerable<ShortcutRect> BothTiers()
        {
            yield return CourseProfileRule.ValleyShortcut(100f, 0f, 30f, 1.3f, Half, Window, Easy, Arc);
            yield return CourseProfileRule.ValleyShortcut(100f, 0f, 40f, 1.5f, Half, Window, Hard, Arc);
        }

        [Test]
        public void 띠는_세로변이고_빈틈없이_이어진다()
        {
            foreach (ShortcutRect r in BothTiers())
            {
                List<float[]> roof = CourseProfileRule.ShortcutRoof(r, 1f, 0.25f);
                List<float[]> tongue = CourseProfileRule.ShortcutTongue(r, 0.25f);
                foreach (var (name, strips, end) in new[] { ("지붕", roof, r.X1), ("혀", tongue, r.TongueEnd) })
                {
                    Assert.AreEqual(r.X0, strips[0][0], 1e-4f, $"{name}는 입구에서 시작");
                    Assert.AreEqual(end, strips[strips.Count - 1][2], 1e-4f, $"{name} 끝");
                    for (int i = 0; i < strips.Count; i++)
                    {
                        float[] s = strips[i];
                        Assert.AreEqual(s[0], s[6], 1e-6f, $"{name} {i} 왼변 세로");
                        Assert.AreEqual(s[2], s[4], 1e-6f, $"{name} {i} 오른변 세로");
                        Assert.Less(s[0], s[2], $"{name} {i} 폭");
                        Assert.GreaterOrEqual(s[7], s[1] - 1e-4f, $"{name} {i} 왼쪽 위≥아래");
                        Assert.GreaterOrEqual(s[5], s[3] - 1e-4f, $"{name} {i} 오른쪽 위≥아래");
                        if (i > 0) { Assert.AreEqual(strips[i - 1][2], s[0], 1e-5f, $"{name} {i} 이음새"); }
                    }
                }
            }
        }

        [Test]
        public void 굴은_열려_있고_바로_위는_지붕_바로_아래는_혀다()
        {
            foreach (ShortcutRect r in BothTiers())
            {
                List<float[]> roof = CourseProfileRule.ShortcutRoof(r, 1f, 0.25f);
                List<float[]> tongue = CourseProfileRule.ShortcutTongue(r, 0.25f);
                float half = r.Entrance.Thickness * 0.5f;
                for (float x = r.X0 + 0.01f; x < r.ChannelEnd - 0.01f; x += 0.05f)
                {
                    float c = r.ChannelCenterAt(x);
                    string at = $"x0={r.X0:F0} x={x:F2}";
                    //  윗면·바닥 5cm 안쪽까지 비어 있어야 한다 — 띠가 호 경계(꺾인 점)를 걸치면 여기서 걸린다.
                    Assert.IsFalse(Inside(roof, x, c + half - 0.05f), $"{at} 굴 윗부분이 지붕에 먹혔다");
                    Assert.IsFalse(Inside(tongue, x, c - half + 0.05f), $"{at} 굴 바닥이 혀에 먹혔다");
                    Assert.IsTrue(Inside(roof, x, c + half + 0.05f), $"{at} 굴 위가 비었다");
                    Assert.IsTrue(Inside(tongue, x, c - half - 0.05f), $"{at} 굴 아래가 비었다");
                }
                for (float x = r.ChannelEnd + 0.01f; x < r.TongueEnd - 0.1f; x += 0.05f)
                {
                    string at = $"x0={r.X0:F0} 곧은 길 x={x:F2}";
                    Assert.IsFalse(Inside(roof, x, r.CenterY) || Inside(tongue, x, r.CenterY), $"{at} 막혔다");
                    Assert.IsTrue(Inside(roof, x, r.Y1 + 0.05f), $"{at} 위가 비었다");
                    Assert.IsTrue(Inside(tongue, x, r.Y0 - 0.05f), $"{at} 아래가 비었다");
                }
            }
        }

        [Test]
        public void 입구_턱이_막고_턱_아래는_열려_있다()
        {
            foreach (ShortcutRect r in BothTiers())
            {
                List<float[]> tongue = CourseProfileRule.ShortcutTongue(r, 0.25f);
                Assert.IsTrue(Inside(tongue, r.X0 + 0.05f, r.LipBottom + 0.2f), $"x0={r.X0:F0} 턱이 비었다");
                Assert.IsFalse(Inside(tongue, r.X0 + 0.05f, r.LipBottom - 0.5f), $"x0={r.X0:F0} 턱 아래가 막혔다");
            }
        }
```

(`using System.Collections.Generic;`이 파일 위에 없으면 더한다.)

- [ ] **Step 2: 실패하는 시험을 쓴다 — `ShortcutRuleTests.cs`**

```csharp
        static readonly FlapArc Arc = new FlapArc(18.6f, 59f, 6.8f, 0.02f);

        //  구간 3의 U(깊이 40, 오르막 1.5)와 어려운 굴을 x=100에서 시작한다.
        static readonly ShortcutRect Deep = CourseProfileRule.ValleyShortcut(
            100f, 0f, 40f, 1.5f, Half, Window, new ShortcutEntrance(3, 2.6f, 6f), Arc);
```

`가운데_절반은_혀_위에_있다`를 다음으로 바꾼다(이름도 바꾼다):

```csharp
        [Test]
        public void 가운데_절반은_혀가_두_길을_가른다()
        {
            //  이게 참이어야 금지 영역(가운데 절반)에서 지름길 새는 Y0 위, 계곡 새는 Y0 아래에만 있다.
            int checkedShapes = 0;
            foreach (SectionTerrain t in CourseProfileRule.Sections)
            {
                if (t.ValleyShortcut == false) { continue; }
                ShortcutRect r = CourseProfileRule.ValleyShortcut(
                    100f, 0f, t.ValleyDepth, t.RiseSlope, Half, Window, t.Entrance, Arc);
                float quarter = r.Length * 0.25f;
                Assert.LessOrEqual(r.X1 - quarter, r.TongueEnd, $"깊이 {t.ValleyDepth}: 가운데 끝까지 혀가 있다");
                for (float x = r.X0 + quarter; x < r.ChannelEnd; x += 0.1f)
                {
                    Assert.Greater(r.ChannelCenterAt(x) - r.Entrance.Thickness * 0.5f, r.Y0,
                                   $"깊이 {t.ValleyDepth} x={x:F1}: 굴 바닥이 Y0 아래로 내려갔다");
                }
                checkedShapes++;
            }
            Assert.Greater(checkedShapes, 0, "지름길 구간이 하나도 없으면 이 시험은 아무것도 안 지킨다");
        }

        [Test]
        public void 계곡_금지는_입구_굴을_막지_않는다()
        {
            for (float x = Deep.X0; x <= Deep.ChannelEnd; x += 0.1f)
            {
                Assert.IsFalse(ShortcutRule.ForbidsValley(Deep, x, Deep.ChannelCenterAt(x)), $"x={x:F1}");
            }
        }
```

패드 시험의 마지막 줄을 바꾸고, 구간 두 개를 다 보는 시험을 더한다:

```csharp
            Assert.Greater(x.Value - width * 0.5f, Deep.ChannelEnd, "패드는 굴 뒤 곧은 길에 있다");
```

```csharp
        [Test]
        public void 두_난이도_모두_곧은_길에_패드가_선다()
        {
            foreach (SectionTerrain t in CourseProfileRule.Sections)
            {
                if (t.ValleyShortcut == false) { continue; }
                ShortcutRect r = CourseProfileRule.ValleyShortcut(
                    100f, 0f, t.ValleyDepth, t.RiseSlope, Half, Window, t.Entrance, Arc);
                float? x = ShortcutRule.PadCenterX(r, 8.16f, 1.5f, 1.5f);
                Assert.IsTrue(x.HasValue, $"깊이 {t.ValleyDepth}: 곧은 길이 짧다");
                Assert.Greater(x.Value - 0.75f, r.ChannelEnd, $"깊이 {t.ValleyDepth}");
            }
        }
```

`지름길이_짧으면_패드를_안_놓는다`의 `new ShortcutRect(0f, 8f, -2f, 2f, null)` → `ShortcutRect.FromCenterSize(4f, 0f, 8f, 4f)`.

- [ ] **Step 3: 컴파일이 깨지는 것을 본다** — 명령 모음의 컴파일. 기대: `FlapArc`/`ShortcutEntrance`/`ChannelCenterAt` 등이 없다는 에러(이 파일들에서만). 이게 이번 단계의 "빨강"이다.

- [ ] **Step 4: `CourseProfile.cs` — 새 구조체와 `SectionTerrain.Entrance`**

`SectionTerrain` 위에 두 구조체를 더한다:

```csharp
    /// <summary>
    /// 날갯짓 한 번이 그리는 호 — <b>틱 단위로 뗀</b> 궤적. 커널은 한 틱에 속도를 먼저 바꾸고 그 속도로
    /// 움직이므로, 날갯짓 틱부터 t초 뒤 높이는 연속 포물선이 아니라 (v + g·dt/2)·t − g·t²/2 다.
    /// 연속식으로 굴을 파면 한 호 끝에서 0.37m 어긋나 제때 쳐도 박는다(spec §13).
    /// </summary>
    public readonly struct FlapArc
    {
        public readonly float FlapImpulse, Gravity, ForwardSpeed, TickSeconds;

        public FlapArc(float flapImpulse, float gravity, float forwardSpeed, float tickSeconds)
        {
            FlapImpulse = flapImpulse; Gravity = gravity; ForwardSpeed = forwardSpeed; TickSeconds = tickSeconds;
        }

        public float EffectiveImpulse => FlapImpulse + Gravity * TickSeconds * 0.5f;
        /// <summary>한 호의 틱 수 — 친 높이로 돌아오기 직전까지(내림). 그래서 호마다 조금씩 오른다.</summary>
        public int TicksPerArc => (int)Math.Floor(2f * EffectiveImpulse / Gravity / TickSeconds);
        public float Span => TicksPerArc * ForwardSpeed * TickSeconds;
        public float RisePerArc => HeightAt(Span);
        public float Apex => EffectiveImpulse * EffectiveImpulse / (2f * Gravity);

        /// <summary>친 자리에서 앞으로 <paramref name="dx"/>m 갔을 때 친 높이보다 얼마나 위인가.</summary>
        public float HeightAt(float dx)
        {
            float t = dx / ForwardSpeed;
            return EffectiveImpulse * t - 0.5f * Gravity * t * t;
        }
    }

    /// <summary>지름길 입구 난이도. 굴이 좁을수록·호가 많을수록 박자가 빡빡하고, 놓치면 호마다 한 번씩 부딪힌다.</summary>
    public readonly struct ShortcutEntrance
    {
        /// <summary>이어진 호(날갯짓 박자) 수. 0이면 굴이 없다(씬 표시에서 되살린 값).</summary>
        public readonly int Arcs;
        /// <summary>굴의 세로 폭.</summary>
        public readonly float Thickness;
        /// <summary>입구 밑으로 내려온 턱 길이. 낮게 빗나간 새가 여기에 부딪혀 계곡으로 떨어진다.</summary>
        public readonly float Lip;

        public ShortcutEntrance(int arcs, float thickness, float lip)
        {
            Arcs = arcs; Thickness = thickness; Lip = lip;
        }
    }
```

`SectionTerrain`에 필드 `public readonly ShortcutEntrance Entrance;`를 더하고 생성자 마지막 인자로 `ShortcutEntrance entrance = default`를 받아 `Entrance = entrance;`.

- [ ] **Step 5: `ShortcutRect`를 바꾼다** (기존 구조체 전체를 대체)

```csharp
    /// <summary>
    /// U자 계곡을 가로지르는 지름길. 앞은 날갯짓 호 모양으로 판 좁은 굴(<see cref="X0"/>~<see cref="ChannelEnd"/>),
    /// 뒤는 곧은 수평 길(~<see cref="X1"/>, 높이 <see cref="Y0"/>~<see cref="Y1"/>)이다. <see cref="X0"/>은 내리막
    /// 천장이 굴 윗면을 지나는 곳(입구), <see cref="X1"/>은 오르막 천장이 곧은 길 윗면을 지나는 곳(출구).
    /// </summary>
    public readonly struct ShortcutRect
    {
        public readonly float X0, ChannelEnd, X1, Y0, Y1;
        public readonly ShortcutEntrance Entrance;
        public readonly FlapArc Arc;
        /// <summary>혀(지름길과 계곡 사이 덩어리)가 끝나는 x — 오르막 천장이 곧은 길 바닥을 지나는 곳.</summary>
        public readonly float TongueEnd;
        /// <summary>혀 아랫면을 그리는 데 쓰는 계곡 모양: 바닥 시작·끝 x, 바닥 위 천장 높이, 오르막 기울기.</summary>
        public readonly float ValleyBottom0, ValleyBottom1, BottomCeiling, RiseSlope;

        public ShortcutRect(float x0, float channelEnd, float x1, float y0, float y1,
                            ShortcutEntrance entrance, FlapArc arc, float tongueEnd,
                            float valleyBottom0, float valleyBottom1, float bottomCeiling, float riseSlope)
        {
            X0 = x0; ChannelEnd = channelEnd; X1 = x1; Y0 = y0; Y1 = y1;
            Entrance = entrance; Arc = arc; TongueEnd = tongueEnd;
            ValleyBottom0 = valleyBottom0; ValleyBottom1 = valleyBottom1;
            BottomCeiling = bottomCeiling; RiseSlope = riseSlope;
        }

        public float CenterY => (Y0 + Y1) * 0.5f;
        public float Length => X1 - X0;
        public float LipBottom => CenterY - Entrance.Thickness * 0.5f - Entrance.Lip;

        /// <summary>
        /// 굴 가운데선 높이. 입구에서 <see cref="CenterY"/>로 시작해, 호마다 한 번 친 새가 지나는 틱 궤적을
        /// 그대로 따른다. 굴 밖은 양 끝 값에 고정한다.
        /// </summary>
        public float ChannelCenterAt(float x)
        {
            if (Entrance.Arcs < 1 || x <= X0) { return CenterY; }
            float span = Arc.Span;
            float dx = Math.Min(x - X0, Entrance.Arcs * span);
            int k = Math.Min((int)(dx / span), Entrance.Arcs - 1);
            return CenterY + k * Arc.RisePerArc + Arc.HeightAt(dx - k * span);
        }

        /// <summary>씬 표시(빈 GameObject의 위치·크기)에서 되살린다. 검사기는 곧은 길 띠만 쓰므로 굴은 비운다.</summary>
        public static ShortcutRect FromCenterSize(float cx, float cy, float w, float h)
        {
            float x0 = cx - w * 0.5f, x1 = cx + w * 0.5f;
            return new ShortcutRect(x0, x0, x1, cy - h * 0.5f, cy + h * 0.5f,
                                    default, default, x1, x0, x1, cy - h * 0.5f, 0f);
        }
    }
```

- [ ] **Step 6: 구간 표·`Compose`·`ValleyShortcut`**

`Sections`:

```csharp
        public static readonly SectionTerrain[] Sections =
        {
            new SectionTerrain(valleyDepth: 15f, hillHeight: 12f, riseSlope: 1.0f, stepHeight: 0f, valleyShortcut: false),
            new SectionTerrain(valleyDepth: 30f, hillHeight: 15f, riseSlope: 1.3f, stepHeight: 10f, valleyShortcut: true,
                               entrance: new ShortcutEntrance(arcs: 1, thickness: 3.2f, lip: 4f)),
            new SectionTerrain(valleyDepth: 40f, hillHeight: 20f, riseSlope: 1.5f, stepHeight: 10f, valleyShortcut: true,
                               entrance: new ShortcutEntrance(arcs: 3, thickness: 2.6f, lip: 6f)),
        };
```

`MinTongue` 아래에 상수 둘:

```csharp
        /// <summary>굴 끝에서 출구까지 곧은 길의 최소 길이 — 호를 늘리다 굴이 출구를 넘는 실수를 막는다.</summary>
        public const float MinStraight = 2f;
        /// <summary>입구 턱 밑으로 계곡 새가 지나갈 최소 틈.</summary>
        public const float MinValleyGap = 6f;
```

`Compose` 시그니처 끝에 `FlapArc arc`를 더하고(`<param name="arc">` 문서: "입구 굴을 파는 날갯짓 호 — FlappyConfig에서 만든다."), 계곡 케이스의 호출을 `ValleyShortcut(x0, y, d, t.RiseSlope, corridorHalf, window, t.Entrance, arc)`로.

`ValleyShortcut`을 대체한다:

```csharp
        /// <summary>
        /// 계곡의 깊이 절반 높이에 지름길을 뚫는다. 입구는 내리막 벽 중간의 좁은 굴(날갯짓 호 모양)이고,
        /// 굴이 끝나면 곧은 수평 길이 출구까지 간다(spec §4, §13). 혀가 <see cref="MinTongue"/>보다 얇거나,
        /// 곧은 길이 <see cref="MinStraight"/>보다 짧거나, 턱 밑 틈이 <see cref="MinValleyGap"/>보다 좁으면 던진다.
        /// </summary>
        /// <param name="x0">계곡이 시작하는 x(평지 끝).</param>
        public static ShortcutRect ValleyShortcut(float x0, float baseY, float depth, float riseSlope,
                                                  float corridorHalf, float window,
                                                  ShortcutEntrance entrance, FlapArc arc)
        {
            float center = baseY - depth * 0.5f;
            float y0 = center - window * 0.5f;
            float y1 = center + window * 0.5f;
            float topCeiling = baseY + corridorHalf;
            float bottomCeiling = baseY - depth + corridorHalf;
            if (y0 - bottomCeiling < MinTongue)
            {
                throw new ArgumentOutOfRangeException(nameof(depth), depth,
                    $"깊이 {depth}m면 지름길 아래 혀가 {y0 - bottomCeiling:F2}m다 (최소 {MinTongue}m)");
            }
            if (entrance.Arcs < 1 || entrance.Thickness <= 0f || entrance.Thickness > window)
            {
                throw new ArgumentOutOfRangeException(nameof(entrance), entrance.Thickness,
                    $"입구 굴은 호 1개 이상, 두께 0~{window:F2}m여야 한다 (호 {entrance.Arcs}, 두께 {entrance.Thickness})");
            }
            float bottom0 = x0 + depth / DropSlope;
            float bottom1 = bottom0 + ValleyBottom;
            //  내리막 천장선 y = topCeiling − DropSlope·(x − x0), 오르막 천장선 y = bottomCeiling + rise·(x − bottom1).
            float mouth = x0 + (topCeiling - (center + entrance.Thickness * 0.5f)) / DropSlope;
            if (mouth >= bottom0)
            {
                throw new ArgumentOutOfRangeException(nameof(depth), depth,
                    $"입구 {mouth:F1}이 계곡 바닥 시작 {bottom0:F1}보다 뒤다");
            }
            float channelEnd = mouth + entrance.Arcs * arc.Span;
            float exit = bottom1 + (y1 - bottomCeiling) / riseSlope;
            float tongueEnd = bottom1 + (y0 - bottomCeiling) / riseSlope;
            if (exit - channelEnd < MinStraight)
            {
                throw new ArgumentOutOfRangeException(nameof(entrance), entrance.Arcs,
                    $"호 {entrance.Arcs}개면 굴이 {channelEnd:F1}까지라 곧은 길이 {exit - channelEnd:F1}m뿐이다 (최소 {MinStraight}m)");
            }
            float floorAtMouth = topCeiling - DropSlope * (mouth - x0) - 2f * corridorHalf;
            float lipBottom = center - entrance.Thickness * 0.5f - entrance.Lip;
            if (lipBottom - floorAtMouth < MinValleyGap)
            {
                throw new ArgumentOutOfRangeException(nameof(entrance), entrance.Lip,
                    $"턱 {entrance.Lip}m 밑 계곡 틈이 {lipBottom - floorAtMouth:F1}m다 (최소 {MinValleyGap}m)");
            }
            return new ShortcutRect(mouth, channelEnd, exit, y0, y1, entrance, arc, tongueEnd,
                                    bottom0, bottom1, bottomCeiling, riseSlope);
        }
```

- [ ] **Step 7: 띠 함수** — `CourseProfileRule` 안, `FloorPieces` 위에:

```csharp
        /// <summary>
        /// 지름길 윗덩어리(지붕)를 세로 띠로 자른다. 굴 구간은 굴 윗면을 따라, 곧은 길은 <see cref="ShortcutRect.Y1"/>
        /// 위에 얹는다. 호가 이어진 모양은 오목해서 한 덩어리 볼록 콜라이더로 만들면 굴이 메워진다 — 그래서 띠다.
        /// </summary>
        public static List<float[]> ShortcutRoof(ShortcutRect r, float wallThickness, float step)
        {
            var strips = new List<float[]>();
            List<float> cuts = ChannelCuts(r, step);
            float half = r.Entrance.Thickness * 0.5f;
            for (int i = 1; i < cuts.Count; i++)
            {
                float a = cuts[i - 1], b = cuts[i];
                float bottomA = r.ChannelCenterAt(a) + half;
                float bottomB = r.ChannelCenterAt(b) + half;
                strips.Add(Strip(a, bottomA, bottomA + wallThickness, b, bottomB, bottomB + wallThickness));
            }
            strips.Add(Strip(r.ChannelEnd, r.Y1, r.Y1 + wallThickness, r.X1, r.Y1, r.Y1 + wallThickness));
            return strips;
        }

        /// <summary>
        /// 지름길과 계곡 사이 덩어리(혀)를 세로 띠로 자른다. 윗면은 굴 바닥(곧은 길에선 <see cref="ShortcutRect.Y0"/>),
        /// 아랫면은 계곡 천장선 — 단 입구 밑은 턱(세로 벽)에서 계곡 바닥 시작점까지 곧게 내려온다.
        /// </summary>
        public static List<float[]> ShortcutTongue(ShortcutRect r, float step)
        {
            List<float> cuts = ChannelCuts(r, step);
            //  계곡 천장이 꺾이는 곳(바닥 시작·끝)과 혀 끝에서도 자른다 — 띠의 곧은 아랫변이 꺾인 선을 따라가게.
            foreach (float x in new[] { r.ValleyBottom0, r.ValleyBottom1, r.TongueEnd })
            {
                if (x > r.X0 && x <= r.TongueEnd) { cuts.Add(x); }
            }
            SortUnique(cuts);
            float half = r.Entrance.Thickness * 0.5f;
            var strips = new List<float[]>();
            for (int i = 1; i < cuts.Count; i++)
            {
                float a = cuts[i - 1], b = cuts[i];
                bool inChannel = (a + b) * 0.5f < r.ChannelEnd;
                float topA = inChannel ? r.ChannelCenterAt(a) - half : r.Y0;
                float topB = inChannel ? r.ChannelCenterAt(b) - half : r.Y0;
                strips.Add(Strip(a, TongueBottom(r, a), topA, b, TongueBottom(r, b), topB));
            }
            return strips;
        }

        //  굴 구간을 step마다 + 호 경계마다 자른 x들(입구·굴 끝 포함). 호 경계는 가운데선이 꺾이는 점이라
        //  띠가 그 점을 걸치면 곧은 변이 굴을 0.3m 넘게 파먹는다.
        static List<float> ChannelCuts(ShortcutRect r, float step)
        {
            var cuts = new List<float> { r.X0, r.ChannelEnd };
            for (float x = r.X0 + step; x < r.ChannelEnd; x += step) { cuts.Add(x); }
            for (int k = 1; k < r.Entrance.Arcs; k++) { cuts.Add(r.X0 + k * r.Arc.Span); }
            SortUnique(cuts);
            return cuts;
        }

        static void SortUnique(List<float> xs)
        {
            xs.Sort();
            for (int i = xs.Count - 1; i > 0; i--)
            {
                if (xs[i] - xs[i - 1] < 1e-4f) { xs.RemoveAt(i); }
            }
        }

        static float TongueBottom(ShortcutRect r, float x)
        {
            float ceiling;
            if (x <= r.ValleyBottom0) { ceiling = r.BottomCeiling + DropSlope * (r.ValleyBottom0 - x); }
            else if (x <= r.ValleyBottom1) { ceiling = r.BottomCeiling; }
            else { ceiling = r.BottomCeiling + r.RiseSlope * (x - r.ValleyBottom1); }
            if (x >= r.ValleyBottom0) { return ceiling; }
            float t = (x - r.X0) / (r.ValleyBottom0 - r.X0);
            float lip = r.LipBottom + (r.BottomCeiling - r.LipBottom) * t;
            return Math.Min(ceiling, lip);
        }

        //  반시계: 왼쪽 아래 → 오른쪽 아래 → 오른쪽 위 → 왼쪽 위 (빌더의 경사 조각과 같은 순서).
        static float[] Strip(float a, float bottomA, float topA, float b, float bottomB, float topB)
            => new[] { a, bottomA, b, bottomB, b, topB, a, topA };
```

(주의: `ShortcutTongue`의 마지막 띠는 `TongueEnd`에서 위·아래가 만나 삼각형이 된다 — 빌더가 겹친 점을 뺀다.)

- [ ] **Step 8: `ShortcutRule.PadCenterX`** — 줄 59를 `if (center - padWidth * 0.5f <= r.ChannelEnd) { return null; }`로. 문서 주석 끝에 "패드는 굴 뒤 곧은 길에만 선다 — 굴 안은 호를 따라 쳐야 하는 자리다." 한 문장을 더한다.

- [ ] **Step 9: 빌더가 컴파일되게 한다** — `FlappyClassicCourseBuilder.cs`

`Compose` 호출(115–117줄)에 호를 넘긴다:

```csharp
            var profile = LOP.MapTools.CourseProfileRule.Compose(
                StartX, length, spacing, ceilingY, window, Seed,
                leadIn: spacing * 4f, tail: spacing * 8f,
                arc: new LOP.MapTools.FlapArc(config.FlapImpulse, config.Gravity, config.ForwardSpeed, TickSeconds));
```

상수 모음(83줄 근처)에 `private const float ShortcutStripStep = 0.25f;`.

`Shortcuts()` 안의 `roof` Box 세 줄과 `tongue` 배열·`Prism` 두 줄을 다음으로 바꾼다:

```csharp
                var roof = LOP.MapTools.CourseProfileRule.ShortcutRoof(r, WallThickness, ShortcutStripStep);
                for (int i = 0; i < roof.Count; i++)
                {
                    Prism(parent, $"ShortcutRoof_{r.X0:F0}_{i}", ToPolygon(roof[i]), skin);
                }
                var tongue = LOP.MapTools.CourseProfileRule.ShortcutTongue(r, ShortcutStripStep);
                for (int i = 0; i < tongue.Count; i++)
                {
                    Prism(parent, $"ShortcutTongue_{r.X0:F0}_{i}", ToPolygon(tongue[i]), skin);
                }
                Debug.Log($"[전통 코스] 지름길 x={r.X0:F0}: 호 {r.Entrance.Arcs}개 · 굴 {r.Entrance.Thickness:F1}m · 턱 {r.Entrance.Lip:F0}m · 굴 끝 {r.ChannelEnd:F1} · 출구 {r.X1:F1}");
```

`Prism` 바로 위에 도우미:

```csharp
        //  띠(8개 수)를 다각형으로. 혀 끝 띠는 위·아래가 만나 삼각형이라 겹친 점을 뺀다.
        private static Vector2[] ToPolygon(float[] strip)
        {
            var points = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < strip.Length; i += 2)
            {
                var p = new Vector2(strip[i], strip[i + 1]);
                if (points.Count > 0 && (points[points.Count - 1] - p).sqrMagnitude < 1e-8f) { continue; }
                points.Add(p);
            }
            if (points.Count > 1 && (points[0] - points[points.Count - 1]).sqrMagnitude < 1e-8f)
            {
                points.RemoveAt(points.Count - 1);
            }
            return points.ToArray();
        }
```

`Shortcuts()` 위 주석 "지름길 하나 = 위쪽 상자 + 혀(돌출 다각형) + …"를 "지름길 하나 = 지붕 띠 + 혀 띠(굴을 판 덩어리를 세로로 자른 볼록 사각형) + 패드 + 검사기용 표시."로, `int shortcutPads = …` 위 주석의 "위쪽 상자"를 "지붕"으로 고친다.

- [ ] **Step 10: 컴파일 → 전체 테스트 PASS** — 명령 모음. `RED:` 줄이 없어야 하고 `Run finished`의 전체 개수가 직전 실행보다 **새 시험 수만큼 늘었는지** 본다(낡은 어셈블리로 돈 초록을 걸러낸다).

- [ ] **Step 11: 뮤테이션 두 개** — 각각 바꾸고 컴파일·테스트해서 **빨강**을 확인한 뒤 되돌린다:
  1. `FlapArc.EffectiveImpulse`를 `FlapImpulse`로(연속 포물선) → `호_높이는_진짜_커널…`, `입구_굴_가운데선은…`가 빨강.
  2. `ChannelCuts`에서 호 경계 줄(`for (int k = 1; …)`)을 지움 → `굴은_열려_있고…`가 빨강(어려운 굴).
  되돌린 뒤 다시 컴파일·테스트 초록.

- [ ] **Step 12: 커밋**

```bash
git add Assets/Scripts/MapTools/CourseProfile.cs Assets/Scripts/MapTools/ShortcutRule.cs Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs Assets/Tests/EditMode/MapTools/CourseProfileTests.cs Assets/Tests/EditMode/MapTools/ShortcutRuleTests.cs
git status --short
git commit -m "feat(flappy): 지름길 입구를 날갯짓 호 모양의 굴로 판다 — 쉬운 굴·어려운 굴, 지붕·혀를 세로 띠로"
```

---

### Task 2: 굽고 재 본다 — 제때 치면 안 닿고, 늦게 치면 닿는다

**Files:**
- Modify: `Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs` (프로필 조립을 공개 함수로)
- 맵 씬(굽기만 — 커밋은 Task 4)

**Interfaces:**
- Consumes: Task 1의 `ShortcutRect`(`X0`, `CenterY`, `ChannelEnd`, `Entrance`, `Arc`), `FlappyTickMath`.
- Produces: `public static LOP.MapTools.CourseProfile FlappyClassicCourseBuilder.ComposeProfile(LOP.MasterData.FlappyConfig config)`, `public static bool FlappyClassicCourseBuilder.TryReadConfig(out LOP.MasterData.FlappyConfig row)` — eval과 Task 3의 뮤테이션이 쓴다.

- [ ] **Step 1: 프로필 조립을 뺀다** — `Build()`에서 `window`·`spacing`·`ceilingY`·`length`를 구하고 `Compose`하는 부분을 공개 함수로 옮기고, `Build()`는 그것을 부른다(`Build()`가 그 지역 변수들을 뒤에서도 쓰면 그대로 남겨 둔다 — 조립만 옮긴다):

```csharp
        /// <summary>굽기와 같은 코스 프로필. 에디터 측정(eval)이 씬과 같은 기하를 다시 얻을 때 쓴다.</summary>
        public static LOP.MapTools.CourseProfile ComposeProfile(LOP.MasterData.FlappyConfig config)
        {
            float window = LOP.MapTools.GateRhythmRule.TargetWindow(
                config.FlapImpulse, config.Gravity, TickSeconds, config.BodyHeight);
            float spacing = LOP.MapTools.GateRhythmRule.TargetSpacing(config.ForwardSpeed);
            float ceilingY = LOP.MapTools.VisualHonesty.ScreenHalfHeight(CameraDistance, VerticalFov);
            float length = RaceSeconds * config.ForwardSpeed;
            return LOP.MapTools.CourseProfileRule.Compose(
                StartX, length, spacing, ceilingY, window, Seed,
                leadIn: spacing * 4f, tail: spacing * 8f,
                arc: new LOP.MapTools.FlapArc(config.FlapImpulse, config.Gravity, config.ForwardSpeed, TickSeconds));
        }
```

(`ceilingY`는 `Build()`의 `corridor * 0.5f`와 같은 값이다 — `ScreenHalfHeight(...) * 2f * 0.5f`.) `TryReadConfig`를 `private` → `public`으로.

- [ ] **Step 2: 컴파일 → 전체 테스트 PASS.**

- [ ] **Step 3: 굽는다** — 명령 모음대로 재생 중이 아닌지 보고, 맵 씬 열기 → 굽기 → `save_scene`. 콘솔에서:

```bash
unity cmd console --project-path $P --tail 30 | grep "전통 코스"
```

기대: `지름길 2개 (패드 2개)`, 지름길 줄 둘 — 하나는 `호 1개 · 굴 3.2m · 턱 4m`, 하나는 `호 3개 · 굴 2.6m · 턱 6m`.

- [ ] **Step 4: 재 본다** — `/tmp/ev/arch.cs`를 쓴다. 새 몸통(반지름 0.44)을 입구 가운데서 떨어지는 중(−12m/s)으로 두고, 호마다 한 번 치되 첫 박자를 `late`틱 늦게 친다. 굴 끝까지 틱마다 겹침을 센다:

```csharp
LOP.EditorTools.FlappyClassicCourseBuilder.TryReadConfig(out var c);
var p = LOP.EditorTools.FlappyClassicCourseBuilder.ComposeProfile(c);
var sb = new System.Text.StringBuilder();
int mask = UnityEngine.LayerMask.GetMask("Default");
foreach (var r in p.Shortcuts)
{
    sb.Append($"x0={r.X0:F1} 호 {r.Entrance.Arcs} 굴 {r.Entrance.Thickness}m:");
    foreach (int late in new[] { 0, 2, 4, 6 })
    {
        float vy = -12f, y = r.CenterY, x = r.X0;
        int hits = 0, tick = 0, per = r.Arc.TicksPerArc;
        while (x < r.ChannelEnd)
        {
            bool flap = tick >= late && (tick - late) % per == 0;
            vy = LOP.MapTools.FlappyTickMath.NextVerticalSpeed(vy, flap, c.FlapImpulse, c.Gravity, c.MaxFallSpeed, 0.02f);
            y = LOP.MapTools.FlappyTickMath.AdvanceHeight(y, vy, 0.02f);
            x += c.ForwardSpeed * 0.02f;
            if (UnityEngine.Physics.CheckSphere(new UnityEngine.Vector3(x, y, 0f), 0.44f, mask, UnityEngine.QueryTriggerInteraction.Ignore)) hits++;
            tick++;
        }
        sb.Append($"  늦게 {late}틱 → 닿은 틱 {hits}");
    }
    sb.AppendLine();
}
UnityEngine.Debug.Log("[굴 측정]\n" + sb);
```

```bash
mkdir -p /tmp/ev   # (위 코드를 /tmp/ev/arch.cs로 저장)
unity cmd eval_file --project-path $P /tmp/ev/arch.cs
unity cmd console --project-path $P --tail 5 | grep -A3 "굴 측정"
```

기대: 두 줄 모두 `늦게 0틱 → 닿은 틱 0`, `늦게 6틱`은 두 줄 모두 **0보다 크다**(쉬운 굴 여유 ±3.1틱, 어려운 굴 ±2.3틱이므로 4틱은 어려운 굴에서 닿아야 한다). 늦게 0틱이 닿으면 굴이 틱 궤적과 어긋난 것 — Task 1의 `ChannelCenterAt`·띠부터 본다(몸통 반지름 0.44 + 반 두께 1.3 이면 가운데선에서 0.86m 여유가 있어야 한다). 측정 결과 네 칸을 ledger/보고에 그대로 적는다.

그리고 씬을 닫았다 다시 열어 같은 결과가 나오는지 본다(띠 메시가 씬에 저장되는지):

```bash
unity cmd open_scene --project-path $P Assets/Scenes/Entrance.unity; unity cmd open_scene --project-path $P Assets/Art/Scenes/FlappyRaceMap.unity
unity cmd eval_file --project-path $P /tmp/ev/arch.cs
```

- [ ] **Step 5: 커밋** (클라 코드만 — 맵 씬은 Task 4에서 Art에 커밋)

```bash
git add Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs
git status --short
git commit -m "refactor(flappy): 코스 프로필 조립을 공개 함수로 — 에디터 측정이 굽기와 같은 기하를 쓴다"
```

---

### Task 3: 최종 검사 · 뮤테이션 · 기록

**Files:**
- Modify: `docs/ROADMAP.md`
- 맵 씬(다시 굽기)

- [ ] **Step 1: 빠른 검사** — Task 2에서 구운 맵으로 빠른 검사(30분+, 띄워 두고 `Logs/FlappyMapCheck.txt` 수정 시각이 바뀌길 기다린다). 끝나면:

```bash
sed -n '/① 클린런/,/── /p' Logs/FlappyMapCheck.txt | head -12
sed -n '/⚡/,/^$/p' Logs/FlappyMapCheck.txt
sed -n '/🔀 지름길/,/^$/p' Logs/FlappyMapCheck.txt
sed -n '/🎥/,/^$/p;/🧱/,/^$/p' Logs/FlappyMapCheck.txt | head -30
```

기대: ① 4자리 ✅ · ⚡ ✅ · 🔀 `지름길 없이 ✅` + 지름길 두 줄 모두 `✅ 들어가서 완주` · 🎥/🧱 새 위반 없음(굴 띠가 처음 들어간 것이라 특히 본다). 낌(좁은 틈) 보고에 굴 안 자리가 나오면 **의도된 좁은 굴**인지(굴 x 범위 안인지) 확인해 보고서에 적는다 — 굴 밖이면 맵 문제다. ❌가 나오면 검사가 아니라 맵(두께·턱)을 고칠 일이다: 리포트의 `탐색 x=`를 Task 2의 eval로 잰다.

- [ ] **Step 2: 뮤테이션 — 리듬이 어긋난 굴은 증명이 ❌가 되나** — 빌더의 `FlapArc` 생성(Compose 호출과 `ComposeProfile` 둘 다)에서 전진 속도를 `config.ForwardSpeed * 0.95f`로 바꿔 굴을 **실제보다 짧은 호**로 판다. 컴파일 → 굽기 → `save_scene` → 빠른 검사. 기대: 어려운 굴(호 3개) 줄이 **❌ 함정**. 여전히 ✅면 `0.9f`로 한 번 더 하고 결과를 적는다(0.9에서도 ✅면 멈추고 보고한다 — 굴이 검사를 못 막는다는 뜻이다). 확인 뒤 **되돌리고**, 컴파일 → 다시 굽기 → `save_scene`. `git diff Assets/Scripts`가 비어 있는지 본다.

- [ ] **Step 3: 로드맵 기록** — `docs/ROADMAP.md`의 "뾰족한 지형 조각과 곧은 지름길" 절 끝(그 절의 "최종 리뷰에서 고친 것" 아래)에 `### 입구 개정 — 이어진 A자 굴 (2026-09-25)` 소절을 더한다. 담을 것: 왜(비스듬한 혀 앞면 = 낮게 놓쳐도 손해 0), 모양(호 모양 굴 + 세로 턱, 굴 뒤 곧은 길·패드), 틱 궤적 숫자(32틱·4.35m·+0.2m, 연속식 0.37m 어긋남), 난이도 표(구간 2/3), Task 2 측정 네 칸, Step 1 검사 결과, Step 2 뮤테이션 결과, 열린 것(두께·턱 튜닝은 플레이 후, A 언덕 지름길).

- [ ] **Step 4: 커밋**

```bash
git add docs/ROADMAP.md
git status --short
git commit -m "docs(roadmap): 지름길 입구를 A자 굴로 바꾼 것을 기록한다"
```

---

### Task 4: 푸시 · 배포

**Files:**
- Art 서브모듈: `Scenes/FlappyRaceMap.unity`
- 클라: `Assets/Art` 포인터

- [ ] **Step 1: Art 먼저 푸시** — 한 줄씩, 각 줄 결과를 보고 넘어간다:

```bash
cd Assets/Art
git fetch origin
git checkout -b feature/flappy-shortcut-arch-entrance
git add Scenes/FlappyRaceMap.unity
git status --short      # 씬 하나만
git commit -m "feat(flappy): 지름길 입구를 날갯짓 호 모양의 굴로 다시 굽는다"
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/flappy-shortcut-arch-entrance -m "Merge feature/flappy-shortcut-arch-entrance: 지름길 입구 A자 굴"
git push origin main
cd ../..
```

- [ ] **Step 2: 클라 포인터 커밋 후 푸시**

```bash
git add Assets/Art
git status --short      # 포인터만 (Jua·PackageManagerSettings는 스테이지 금지)
git commit -m "chore(art): 지름길 입구 A자 굴 코스 포인터"
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/flappy-shortcut-arch-entrance -m "Merge feature/flappy-shortcut-arch-entrance: 지름길 입구를 이어진 A자 굴로"
git push origin main
```

리베이스 뒤 `git submodule update` 후 컴파일 확인(형제 레포가 뒤처졌으면 먼저 당긴다).

- [ ] **Step 3: 배포** — 맵 콘텐츠만 바뀌었다(서버 코드 없음 → 게임서버 재배포 불필요). `target=all`은 iOS를 포함하지 않으므로 둘 다 돌린다:

```bash
gh workflow run content-deploy.yml -f target=all -f package_ref=main -f prune=false
gh workflow run content-deploy.yml -f target=client-app-ios -f package_ref=main -f prune=false
gh run list --workflow=content-deploy.yml --limit 2
```

두 실행이 끝날 때까지 기다려 모든 잡이 success(건너뛴 잡은 skipped)인지 확인한다.
