# 판치기 타격 전파 지수 감쇠 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 타격의 영향이 친 자리 근처로 좁혀져, 손바닥을 벌려 넓게 쳐야 판 전체가 움직이게 만든다.

**Architecture:** 힘 커널의 거리 가중치를 멱함수 `1/(1+k·d²)`에서 지수 `e^(−d/ℓ)`로 바꾼다. 커널은 지금 서버 레포에 있어 EditMode 테스트를 못 붙이므로 먼저 공용 패키지로 옮긴다. 마스터데이터 노브도 뜻이 달라지므로 `falloff_rate` → `influence_radius`로 함께 바꾼다.

**Tech Stack:** C# (Unity 6000.3.16f1), LOP-Shared 패키지(순수 C#), NUnit EditMode, Luban 마스터데이터 파이프라인

**Spec:** `docs/superpowers/specs/2026-08-28-panchigi-strike-propagation-design.md`

## Global Constraints

- **타격 판정은 서버 전담** — 클라는 예측하지 않는다. 클라 런타임은 이 노브들을 읽지 않는다.
- **공유 시뮬 코드는 구체 클래스를 공유한다** — 커널에 인터페이스 seam을 만들지 않는다(`world-core-connection-architecture.md`의 "구체 공유" 규칙).
- **World 타입은 풀 네임스페이스로** — LOP 측 파일에서 `using GameFramework.World;`를 쓰지 않는다.
- **`git add -A` / `git commit -a` 금지** — Unity 레포에는 커밋하지 않는 로컬 픽스처가 늘 있다. 바꾼 파일만 경로로 지정하고, 커밋 전에 `git status --short`로 확인한다.
- **푸시는 `CLAUDE.md`의 푸시 규약대로** — fetch → rebase --autostash → checkout main → merge --ff-only → merge --no-ff → push. 한 줄씩 결과를 확인한다.
- **영향 반경 초기값 0.4 m, 수직 세기 10, 수평 세기 2.5** — 스펙 §4의 값.

---

### Task 1: 힘 커널을 공용 패키지로 옮기고 지금 동작을 고정한다

커널이 서버 레포(`Assembly-CSharp`)에 있어 EditMode 테스트를 못 붙인다. 곡선을 바꾸기 전에 옮겨서, **바꾸기 전 동작**을 테스트로 먼저 묶는다. 그래야 다음 태스크에서 무엇이 달라졌는지가 테스트로 드러난다.

**Files:**
- Move: `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiStrike.cs` → `LeagueOfPhysical-Shared/Runtime/Scripts/Game/PanchigiStrike.cs` (`.cs.meta`도 함께 — GUID가 보존돼야 참조가 안 끊긴다)
- Create: `LeagueOfPhysical-Shared/Tests/EditMode/PanchigiStrikeTests.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Editor/PanchigiVerification.cs` — `StrikeKernel`·`ContactSpread`·`SampleLayout` 세 메서드와 `Run()`의 호출부 삭제

**Interfaces:**
- Consumes: 없음 (기존 코드 이동)
- Produces: `LOP.PanchigiStrike`가 `baegames.LOP.Shared.Runtime` 어셈블리에 존재. 공개 API는 그대로 —
  `PanchigiStrike.StrikeInput(Vector3 strikePoint, Vector3 dragDelta, float holdTime)`,
  `PanchigiStrike.StrikeTuning(float forceMultiplier, float horizontalForceMultiplier, float falloffRate)`,
  `Vector3 ComputeImpulse(in StrikeInput, in StrikeTuning, Vector3[] liveSamples, int liveCount, int totalSamples)`,
  `void BuildSamples(Vector3 coinCenter, float radius, Vector3[] buffer)`
  (`Vector3`는 `System.Numerics.Vector3`)

- [ ] **Step 1: 커널 파일을 옮긴다**

```bash
cd /c/Users/re5na/workspace/LOP
S=LeagueOfPhysical-Server/Assets/Scripts/Game
H=LeagueOfPhysical-Shared/Runtime/Scripts/Game
mv "$S/PanchigiStrike.cs"      "$H/PanchigiStrike.cs"
mv "$S/PanchigiStrike.cs.meta" "$H/PanchigiStrike.cs.meta"
ls "$H" | grep PanchigiStrike
```

`.meta`를 함께 옮기는 이유: GUID가 보존돼야 씬·프리팹 참조가 안 끊긴다. 커널은 참조되는 에셋이 아니지만 규칙은 동일하게 지킨다.

- [ ] **Step 2: 지금 동작을 고정하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/PanchigiStrikeTests.cs`:

```csharp
using System.Numerics;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 타격 힘 커널. 거리에 따라 얼마나 약해지는가가 이 게임의 손맛을 정한다 —
    /// 곡선을 건드릴 때 무엇이 달라졌는지 여기서 드러나야 한다.
    /// </summary>
    public class PanchigiStrikeTests
    {
        //  세기 노브는 1로 두고 거리 효과만 본다. 수직 세기만 1이면 홀드 1초가 곧 임펄스 크기다.
        private static PanchigiStrike.StrikeTuning Tuning(float falloffRate)
            => new PanchigiStrike.StrikeTuning(1f, 1f, falloffRate);

        //  타격점은 원점, 동전은 x축으로 distance만큼 떨어진 곳에 샘플 하나.
        private static float ImpulseAt(float distance, float falloffRate, int totalSamples = 1)
        {
            var input = new PanchigiStrike.StrikeInput(Vector3.Zero, Vector3.Zero, 1f);
            var samples = new[] { new Vector3(distance, 0f, 0f) };

            return PanchigiStrike.ComputeImpulse(input, Tuning(falloffRate), samples, 1, totalSamples).Y;
        }

        [Test]
        public void 멀어질수록_약해진다()
        {
            Assert.Greater(ImpulseAt(0f, 4f), ImpulseAt(0.5f, 4f));
            Assert.Greater(ImpulseAt(0.5f, 4f), ImpulseAt(1.5f, 4f));
        }

        [Test]
        public void 살아남은_샘플이_없으면_힘이_없다()
        {
            var input = new PanchigiStrike.StrikeInput(Vector3.Zero, Vector3.One, 1f);
            var samples = new[] { Vector3.Zero };

            var impulse = PanchigiStrike.ComputeImpulse(input, Tuning(4f), samples, 0, 13);

            Assert.AreEqual(Vector3.Zero, impulse);
        }

        [Test]
        public void 샘플_개수를_늘려도_세기는_그대로다()
        {
            //  정밀도 노브(샘플 수)와 세기 노브가 갈려 있어야 한다 — 전체 샘플 수로 나누는 이유.
            var input = new PanchigiStrike.StrikeInput(Vector3.Zero, Vector3.Zero, 1f);

            var few = new[] { Vector3.Zero, Vector3.Zero };
            var many = new[] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

            float withFew = PanchigiStrike.ComputeImpulse(input, Tuning(4f), few, 2, 2).Y;
            float withMany = PanchigiStrike.ComputeImpulse(input, Tuning(4f), many, 4, 4).Y;

            Assert.AreEqual(withFew, withMany, 1e-5f);
        }

        [Test]
        public void 높이가_달라도_세기가_흔들리지_않는다()
        {
            //  감쇠는 판 위 평면 거리(XZ)로만 잰다 — 동전이 떠 있어도 같은 세기여야 한다.
            var input = new PanchigiStrike.StrikeInput(Vector3.Zero, Vector3.Zero, 1f);
            var onBoard = new[] { new Vector3(0.5f, 0f, 0f) };
            var lifted = new[] { new Vector3(0.5f, 3f, 0f) };

            float a = PanchigiStrike.ComputeImpulse(input, Tuning(4f), onBoard, 1, 1).Y;
            float b = PanchigiStrike.ComputeImpulse(input, Tuning(4f), lifted, 1, 1).Y;

            Assert.AreEqual(a, b, 1e-5f);
        }

        [Test]
        public void 샘플을_원판에_고르게_깐다()
        {
            //  해바라기 배치 — 개수가 몇이든 성립하고 난수를 안 써서 늘 같은 자리가 나온다.
            var buffer = new Vector3[13];

            PanchigiStrike.BuildSamples(new Vector3(1f, 2f, 3f), 0.15f, buffer);

            foreach (var sample in buffer)
            {
                float dx = sample.X - 1f;
                float dz = sample.Z - 3f;
                Assert.LessOrEqual(System.MathF.Sqrt(dx * dx + dz * dz), 0.15f + 1e-4f);
                Assert.AreEqual(2f, sample.Y, 1e-5f, "샘플은 동전과 같은 높이에 깔린다");
            }
        }
    }
}
```

- [ ] **Step 3: 테스트를 돌려 통과하는지 본다**

에디터가 응답하면:

```bash
export MSYS_NO_PATHCONV=1
CP="C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client"
unity cmd --project-path "$CP" eval 'UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceUpdate); return "r";'
unity cmd --project-path "$CP" run_tests mode=EditMode
```

기대: 전부 PASS. 이 태스크는 **동작을 안 바꾸므로** 처음부터 통과해야 한다 — 실패하면 이동 과정에서 무언가 깨진 것이다.

에디터가 막혀 있으면(`file:` 패키지 재임포트 대기) 사용자에게 창 포커스를 요청한다. 그동안 컴파일 게이트로 선검증할 수 있다.

- [ ] **Step 4: 수기 검증에서 커널 부분을 걷어낸다**

`PanchigiVerification.cs`에서 `StrikeKernel`·`ContactSpread`·`SampleLayout` 세 메서드와 `Run()` 안의 호출 세 줄을 지운다. 지운 자리에 남길 주석:

```csharp
        //  PanchigiStrike 커널 검증은 공용 패키지의 EditMode 테스트(PanchigiStrikeTests)로 옮겼다.
        //  여기 한 벌 더 두면 시그니처가 바뀔 때 한쪽만 고쳐져 조용히 어긋난다 - 실제로 그렇게
        //  게임서버 배포가 깨진 적이 있다.
```

- [ ] **Step 5: 컴파일 게이트를 돌린다**

클라·서버 × 런타임·에디터 네 어셈블리를 전부 통과해야 한다. 특히 **서버 Editor 어셈블리** — 수기 검증을 지웠으므로 여기가 깨지기 쉽다.

- [ ] **Step 6: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/PanchigiStrike.cs Runtime/Scripts/Game/PanchigiStrike.cs.meta Tests/EditMode/PanchigiStrikeTests.cs Tests/EditMode/PanchigiStrikeTests.cs.meta
git status --short
git commit -m "refactor(panchigi): 힘 커널을 공용 패키지로 옮기고 테스트를 붙인다

커널이 서버 Assets에 있어 EditMode 테스트를 못 붙였고, 그 자리를 수기 검증 스크립트가
메우고 있었다. 순수 C#이라 그대로 옮겨진다.

곡선을 바꾸기 전에 지금 동작을 먼저 묶는다 - 다음 변경에서 무엇이 달라졌는지가 테스트로
드러나야 한다."

cd ../LeagueOfPhysical-Server
git add Assets/Scripts/Game/PanchigiStrike.cs Assets/Scripts/Game/PanchigiStrike.cs.meta Assets/Editor/PanchigiVerification.cs
git status --short
git commit -m "refactor(panchigi): 힘 커널 이동에 맞춰 수기 검증을 걷어낸다

커널은 공용 패키지로 갔고 EditMode 테스트가 붙었다. 여기 한 벌 더 두면 시그니처가 바뀔 때
한쪽만 고쳐진다."
```

`.cs.meta`가 없으면 유니티가 아직 안 만든 것이다 — 에디터 창을 포커스해 생성되게 한 뒤 커밋한다. meta 없이 커밋하면 다른 머신에서 GUID가 새로 생겨 참조가 갈린다.

---

### Task 2: 감쇠 곡선을 지수로 바꾸고 노브 이름·값을 맞춘다

곡선·컬럼명·조립부는 **한 덩어리**다. 쪼개면 중간에 컴파일이 안 되거나(컬럼만 바꿈) 값의 뜻이 안 맞는 상태(곡선만 바꿈 — 반경 4 m가 되어 판 전체가 움직인다)가 생긴다.

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/PanchigiStrike.cs` — `StrikeTuning.FalloffRate` → `InfluenceRadius`, `ComputeImpulse`의 가중치 한 줄
- Modify: `LeagueOfPhysical-Shared/Tests/EditMode/PanchigiStrikeTests.cs` — 새 곡선의 성질을 잡는 테스트 추가
- Modify: `infrastructure/table/Datas/#PanchigiConfig.xlsx` — `L` 열 이름 `falloff_rate` → `influence_radius`, 값 `4` → `0.4`; `J`(force_multiplier) `8` → `10`; `K`(horizontal_force_multiplier) `2` → `2.5`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/MessageHandler/PanchigiStrikeMessageHandler.cs:132-133` — `config.FalloffRate` → `config.InfluenceRadius`
- Regenerate: `LeagueOfPhysical-MasterData-Client`, `LeagueOfPhysical-MasterData-Server`, `lop-backend`(Luban 출력)

**Interfaces:**
- Consumes: Task 1이 옮겨 둔 `LOP.PanchigiStrike`
- Produces: `PanchigiStrike.StrikeTuning(float forceMultiplier, float horizontalForceMultiplier, float influenceRadius)` — 셋째 인자의 **이름과 뜻**이 바뀐다(계수 → 미터 단위 반경). 마스터데이터 `PanchigiConfig.InfluenceRadius`(float).

- [ ] **Step 1: 새 곡선의 성질을 잡는 실패 테스트를 쓴다**

`PanchigiStrikeTests.cs`에 추가. 기존 테스트의 `Tuning(4f)`는 그대로 두되(값의 의미가 바뀌므로 다음 스텝에서 함께 정리), 여기서는 새 이름으로 쓴다:

```csharp
        [Test]
        public void 영향_반경만큼_떨어지면_약_37퍼센트다()
        {
            //  e^-1 = 0.368. "반경"이라는 이름이 무엇을 뜻하는지를 이 테스트가 정의한다.
            float atCenter = ImpulseAt(0f, 0.4f);
            float atRadius = ImpulseAt(0.4f, 0.4f);

            Assert.AreEqual(0.368f, atRadius / atCenter, 0.01f);
        }

        [Test]
        public void 반경_네_배_거리에서는_거의_사라진다()
        {
            //  꼬리를 끊는 것이 이 곡선을 고른 이유다. 옛 곡선(1/(1+4d²))은 여기서 9%가 남았다.
            float atCenter = ImpulseAt(0f, 0.4f);
            float farAway = ImpulseAt(1.6f, 0.4f);

            Assert.Less(farAway / atCenter, 0.02f);
        }

        [Test]
        public void 옆_동전은_삼분의_일도_못_받는다()
        {
            //  동전 간격 0.5m. 한 점만 쳐서는 옆까지 확실히 못 넘기고, 손바닥을 벌려야 한다.
            float atCenter = ImpulseAt(0f, 0.4f);
            float neighbour = ImpulseAt(0.5f, 0.4f);

            Assert.Less(neighbour / atCenter, 0.33f);
        }
```

- [ ] **Step 2: 돌려서 실패하는 것을 확인한다**

```bash
unity cmd --project-path "$CP" run_tests mode=EditMode
```

기대: 새 테스트 3개가 FAIL. 옛 곡선에서 `d=0.4`는 `1/(1+4·0.16)=0.61`(37%가 아님), `d=1.6`은 `1/(1+10.24)=0.089`(2% 아님), `d=0.5`는 `0.5`(0.33 미만이 아님).

**여기서 통과하면 안 된다** — 통과했다면 테스트가 곡선을 안 재고 있다는 뜻이다.

- [ ] **Step 3: 커널을 바꾼다**

`PanchigiStrike.cs`의 `StrikeTuning`:

```csharp
        /// <summary>타격 세기를 정하는 값들. 마스터데이터에서 온다.</summary>
        public readonly struct StrikeTuning
        {
            public readonly float ForceMultiplier;
            public readonly float HorizontalForceMultiplier;

            //  이 거리만큼 떨어지면 세기가 약 37%(e⁻¹)로 준다. 계수가 아니라 미터다.
            public readonly float InfluenceRadius;

            public StrikeTuning(float forceMultiplier, float horizontalForceMultiplier, float influenceRadius)
            {
                ForceMultiplier = forceMultiplier;
                HorizontalForceMultiplier = horizontalForceMultiplier;
                InfluenceRadius = influenceRadius;
            }
        }
```

`ComputeImpulse`의 합산 루프:

```csharp
            float sum = 0f;
            for (int i = 0; i < liveCount; i++)
            {
                //  감쇠는 판 위 평면 거리로만 잰다 — 동전이 떠 있어도 세기가 흔들리면 안 된다.
                float dx = liveSamples[i].X - input.StrikePoint.X;
                float dz = liveSamples[i].Z - input.StrikePoint.Z;

                //  책이 매트 위에 놓여 있어 친 자리만 눌린다(탄성 지지 위의 판) — 그 변형은
                //  거리에 따라 지수로 준다. 멱함수(1/(1+k·d²))는 꼬리가 길어 한 점만 쳐도
                //  판 전체가 움직였다.
                sum += MathF.Exp(-MathF.Sqrt(dx * dx + dz * dz) / tuning.InfluenceRadius);
            }
```

클래스 요약 주석의 마지막 문단도 새 모델에 맞게 고친다:

```csharp
    /// 격자가 만든 것은 회전이 아니라 "동전이 판에 닿은 정도"라는 배수 하나였다. 여기서는 그 배수를
    /// 고정 개수 샘플로 직접 잰다. 거리 가중치는 지수 감쇠다 — 책이 바닥에 받쳐져 있어 친 자리만
    /// 눌리기 때문이다(설계: docs/superpowers/specs/2026-08-28-panchigi-strike-propagation-design.md).
```

- [ ] **Step 4: 기존 테스트의 인자도 새 뜻에 맞춘다**

Task 1에서 쓴 테스트들은 `Tuning(4f)`을 넘긴다. 새 뜻으로는 "반경 4 m"라 사실상 감쇠가 없는 값이다. 거리 효과를 보는 테스트가 무의미해지므로 `0.4f`로 바꾼다:

```csharp
        //  헬퍼 시그니처의 인자 이름도 바꾼다
        private static PanchigiStrike.StrikeTuning Tuning(float influenceRadius)
            => new PanchigiStrike.StrikeTuning(1f, 1f, influenceRadius);

        private static float ImpulseAt(float distance, float influenceRadius, int totalSamples = 1)
        {
            var input = new PanchigiStrike.StrikeInput(Vector3.Zero, Vector3.Zero, 1f);
            var samples = new[] { new Vector3(distance, 0f, 0f) };

            return PanchigiStrike.ComputeImpulse(input, Tuning(influenceRadius), samples, 1, totalSamples).Y;
        }
```

기존 테스트 본문의 `4f`를 전부 `0.4f`로 바꾼다(`멀어질수록_약해진다`, `살아남은_샘플이_없으면_힘이_없다`, `샘플_개수를_늘려도_세기는_그대로다`, `높이가_달라도_세기가_흔들리지_않는다`).

- [ ] **Step 5: 테스트를 돌려 전부 통과하는지 본다**

```bash
unity cmd --project-path "$CP" eval 'UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceUpdate); return "r";'
unity cmd --project-path "$CP" run_tests mode=EditMode
```

기대: 새 3개 포함 전부 PASS.

- [ ] **Step 6: 마스터데이터 컬럼과 값을 바꾼다**

`infrastructure/table/Datas/#PanchigiConfig.xlsx`는 Luban Excel-embedded 형식이다. 1행 `##var`(컬럼명), 2행 `##type`, 4행 `##`(다시 컬럼명), 5행이 값이다. **1행과 4행 둘 다** 고쳐야 한다.

| 셀 | 지금 | 바꾼 뒤 |
|---|---|---|
| `L1`, `L4` | `falloff_rate` | `influence_radius` |
| `L5` | `4` | `0.4` |
| `J5` | `8` | `10` |
| `K5` | `2` | `2.5` |

파이썬으로 열어 고친다(엑셀 앱 없이):

```python
import zipfile, re, shutil, io

src = r"C:\Users\re5na\workspace\LOP\infrastructure\table\Datas\#PanchigiConfig.xlsx"
tmp = src + ".new"

zin = zipfile.ZipFile(src)
zout = zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED)
for item in zin.infolist():
    data = zin.read(item.filename)
    if item.filename == 'xl/worksheets/sheet1.xml':
        xml = data.decode('utf-8')
        xml = xml.replace('<is><t>falloff_rate</t></is>', '<is><t>influence_radius</t></is>')
        xml = xml.replace('<c r="L5" t="n"><v>4</v></c>', '<c r="L5" t="n"><v>0.4</v></c>')
        xml = xml.replace('<c r="J5" t="n"><v>8</v></c>', '<c r="J5" t="n"><v>10</v></c>')
        xml = xml.replace('<c r="K5" t="n"><v>2</v></c>', '<c r="K5" t="n"><v>2.5</v></c>')
        data = xml.encode('utf-8')
    zout.writestr(item, data)
zout.close(); zin.close()
shutil.move(tmp, src)
```

위 문자열은 2026-08-28 시점의 실제 셀 XML을 확인하고 적은 것이다(`t="n"`·`t="inlineStr"` 포함). 그래도 **치환이 실제로 일어났는지 바꾼 뒤 다시 읽어 검증한다** — 파일이 그사이 달라졌으면 `replace`는 예외 없이 조용히 아무것도 안 바꾼다. 확인은 위 Task 2 Step 6 앞에서 셀 원문을 출력한 것과 같은 방법으로 하고, 기대값은 `L1`·`L4`가 `influence_radius`, `L5`가 `0.4`, `J5`가 `10`, `K5`가 `2.5`다.

- [ ] **Step 7: Luban을 돌려 생성물을 갱신한다**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
bash gen.sh
cd /c/Users/re5na/workspace/LOP
git -C LeagueOfPhysical-MasterData-Client diff --stat
git -C LeagueOfPhysical-MasterData-Server diff --stat
git -C lop-backend diff --stat
```

기대: `tbpanchigiconfig`만 바뀐다. 다른 테이블이 함께 바뀌면 원인을 확인하고 되돌린다(`[[masterdata-new-table-checklist]]`).

생성된 `PanchigiConfig.cs`에 `InfluenceRadius` 프로퍼티가 있는지 확인한다:

```bash
grep -n "InfluenceRadius\|FalloffRate" LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/PanchigiConfig.cs
```

- [ ] **Step 8: 서버 조립부를 새 이름으로 바꾼다**

`PanchigiStrikeMessageHandler.cs`:

```csharp
            var tuning = new PanchigiStrike.StrikeTuning(
                config.ForceMultiplier, config.HorizontalForceMultiplier, config.InfluenceRadius);
```

- [ ] **Step 9: 컴파일 게이트 + 테스트**

네 어셈블리(클·서 × 런타임·에디터)를 전부 통과해야 한다. 그다음 EditMode 전체를 돌린다.

일부러 곡선을 옛 것으로 되돌려 새 테스트 3개가 실패하는지 한 번 확인한 뒤 되돌린다 — 게이트에 이빨이 있는지 보는 것이다.

- [ ] **Step 10: 커밋**

레포마다 따로 커밋한다. 메시지 예:

```bash
# infrastructure
git add table/Datas/#PanchigiConfig.xlsx
git commit -m "feat(panchigi): 타격 노브를 영향 반경으로 바꾼다

falloff_rate(거리제곱 계수)는 지수 감쇠에서 뜻이 안 통한다. influence_radius(미터)로 바꾸고
0.4m로 둔다 - 게임 스케일 12.5배 기준 실제 3.2cm다. 곡선이 바뀌며 정타가 약해지므로 세기
계수도 1.2배 올린다."

# MasterData-Client / -Server / lop-backend: 생성물
git commit -m "chore(masterdata): 판치기 노브 재생성"

# LOP-Shared
git commit -m "feat(panchigi): 타격 전파를 지수 감쇠로 바꾼다

한 지점만 쳐도 판 전체가 움직여, 손바닥을 벌려 넓게 치는 조작이 결과를 바꾸지 못했다.
멱함수는 꼬리가 길어 상수를 키워도 모양이 안 바뀐다.

책이 매트 위에 놓여 있으므로 타격은 국소적이다(탄성 지지 위의 판 - 변형이 거리에 따라
지수로 준다). 옆 동전이 50% → 29%, 반대편이 10% → 2%로 준다."

# LOP-Server
git commit -m "feat(panchigi): 타격 조립부를 영향 반경으로 맞춘다"
```

---

### Task 3: 머지·배포하고 실제로 달라졌는지 본다

**Files:** 없음 (배포·검증)

**Interfaces:**
- Consumes: Task 2까지의 모든 변경
- Produces: 없음

- [ ] **Step 1: 5레포를 푸시 규약대로 머지·푸시한다**

infrastructure, MasterData-Client, MasterData-Server, lop-backend, LOP-Shared, LOP-Server. 레포마다 한 줄씩 확인하며 진행한다.

- [ ] **Step 2: 게임서버를 배포한다**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
gh workflow run gameserver-deploy -f environment=both
```

마스터데이터는 게임서버 이미지에 함께 구워지므로 `content-deploy`는 필요 없다(맵·프리팹이 안 바뀌었다). 배포가 끝나면 `infrastructure`의 `game-server-config.env` 태그가 새 커밋으로 bump됐는지 확인한다.

- [ ] **Step 3: 한 점 타격과 벌린 네 점 타격을 비교한다**

두 클라를 띄워 판치기에 들어간 뒤, 프로브로 잰다.

```bash
# 상태(동전 z 좌표 포함)를 두 번 읽어 "멎음"을 확정한 뒤 친다
unity cmd --project-path "$P" eval 'return LOP.EditorTools.PanchigiPlayProbe.Status();'
unity cmd --project-path "$P" eval 'return LOP.EditorTools.PanchigiPlayProbe.StrikeWhenReady(0.75f, 3f, 1f);'
```

**한 점 타격**: `z=0.75`를 최대 세기로 친 뒤 동전 z 좌표를 본다. 기대 — 친 자리(0.75)와 바로 옆(0.25)은 움직이고, 먼 둘(−0.25, −0.75)은 거의 제자리.

**벌린 네 점**: 프로브의 `Strike`는 접촉점 하나만 넣으므로, 네 점 비교는 **사람이 직접** 손가락 네 개를 벌려 치거나(터치 기기) 마우스로는 불가능하다. 이 스텝은 **한 점 타격의 좁아짐**만 자동으로 확인하고, 손바닥 비교는 사용자에게 요청한다.

- [ ] **Step 4: 로드맵을 갱신한다**

`docs/ROADMAP.md`의 완료 표에 한 줄 추가. 무엇이 문제였고(한 점이 판 전체를 덮음), 왜 곡선을 바꿨는지(꼬리), 튜닝 노브가 무엇인지(영향 반경 0.4 m)를 남긴다.

- [ ] **Step 5: 클라 레포에 설계·계획 문서를 머지한다**

이 계획과 스펙이 있는 브랜치를 클라 main에 머지·푸시한다.

---

## Self-Review

**스펙 커버리지**

| 스펙 절 | 태스크 |
|---|---|
| §3 바꾸는 것 (공식) | Task 2 Step 3 |
| §4 마스터데이터 (컬럼·값) | Task 2 Step 6–8 |
| §5 만지는 곳 + 커널 이동 | Task 1, Task 2 |
| §6 테스트 5가지 | Task 1 Step 2(3개: 단조·0·정규화 + 높이·배치), Task 2 Step 1(2개: e⁻¹·꼬리) |
| §7 검증 (프로브 비교) | Task 3 Step 3 |
| §8 범위 밖 | 태스크 없음 — 의도적 |

**타입 일관성** — `StrikeTuning`의 셋째 인자가 Task 1에서는 `falloffRate`, Task 2 Step 3부터 `influenceRadius`다. Task 2 Step 4에서 테스트 헬퍼의 인자 이름도 함께 바꾸도록 명시했다. `config.FalloffRate` → `config.InfluenceRadius`는 Luban이 컬럼명에서 생성하므로 Step 6의 컬럼명 변경과 짝이 맞는다.

**빠진 것을 하나 고쳐 넣었다** — 스펙 §6의 테스트 목록에 "높이가 달라도 세기가 안 흔들린다"와 "샘플이 원판에 고르게 깔린다"는 없었지만, 옮기는 김에 커널의 기존 계약이라 Task 1에 포함했다. 곡선을 바꿀 때 이 둘이 깨지면 바로 드러난다.
