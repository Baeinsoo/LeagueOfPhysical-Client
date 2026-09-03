# Flappy 추격자 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 레이스 뒤에서 벽이 따라오고, 닿은 사람은 탈락해 등수 아래로 내려간다.

**Architecture:** 벽 위치는 출발 후 경과 시간만의 함수(닫힌 식)라 클·서가 각자 계산해도 같다 — 와이어에 싣지 않는다. 잡는 판단은 서버 전용 틱 시스템이 하고(결승선 판정과 같은 자리), 클라는 벽을 그리고 탈락 후 화면을 바꾸기만 한다.

**Tech Stack:** Unity 6 / C# / VContainer / R3 / Mirror / Luban(MasterData) / NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-09-03-flappy-chaser-design.md`

## Global Constraints

- **레포 6개가 걸린다**: LOP-Shared, LOP-Client, LOP-Server, MasterData-Client, MasterData-Server, infrastructure. 각 레포마다 따로 커밋·푸시한다.
- **푸시 절차(필수, 레포마다)**: `git fetch origin` → `git rebase --autostash origin/main` → `git checkout main` → `git merge --ff-only origin/main` → `git merge --no-ff <feature>` → `git push origin main`. **한 줄씩 결과를 확인하고 넘어간다. `&&`로 잇지 않는다.**
- **`git push --force` / `--force-with-lease` 금지.** 거절되면 다시 fetch → rebase → 재시도.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고 커밋 전에 `git status --short`로 확인한다.
- **main에 직접 커밋 금지.** 유니티 레포에서 **git worktree 금지** — 일반 브랜치로 전환한다.
- **커밋하지 않는 로컬 픽스처** — 클라: `Assets/Art`(서브모듈 포인터), `Assets/Scenes/Room.unity`, `Assets/UI/Theme/Fonts/Jua-Regular SDF.asset`, `ProjectSettings/PackageManagerSettings.asset`, `ProjectSettings/ProjectSettings.asset`. 서버: `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`, 볼륨 프로파일, URP 에셋, `ProjectSettings/ProjectSettings.asset`, 빌드 디렉터리, `test-results.xml`. **절대 스테이지하지 않는다.**
- **`.meta` 파일은 반드시 함께 커밋한다.** 직접 만들지 않는다 — 유니티가 만든 것만 커밋한다.
- **테스트를 위해 어셈블리를 옮기지 않는다.** 새 테스트는 지금 있는 테스트 어셈블리에 넣는다.
- **`run_tests`는 컴파일을 다시 하지 않는다.** 테스트를 추가·수정한 뒤에는 반드시 `unity cmd recompile`(또는 에디터 포커스)로 컴파일한 다음 돌린다. **테스트 개수(total)가 늘었는지 확인**해서 새 테스트가 실제로 실행됐는지 본다 — `total`이 그대로면 옛 어셈블리를 돌린 것이다.
- **테스트가 실패할 수 있는지 확인한다(뮤테이션).** 새 테스트마다 구현을 일부러 한 줄 망가뜨려 빨강을 본 뒤 되돌린다. 통과만으로 검증됐다고 하지 않는다.
- **유니티 CLI**: `unity cmd <command> --project-path <절대경로>`를 항상 쓴다. 테스트는 `--async_tests true --mode EditMode`. `refresh_unity`라는 명령은 없다. 에디터가 안 떠 있으면 조용히 다른 인스턴스로 갈 수 있으니 `--project-path`를 반드시 준다.
- **플레이 모드를 임의로 멈추지 않는다.** 플레이 중에 `recompile`하지 않는다.
- **주석 규칙**: 코드로 자명한 것은 주석을 달지 않는다. 비자명한 *의도(왜)* 만 일상어로 짧게 남긴다. 전문용어를 설명 없이 던지지 않는다.
- **와이어에 추격자를 싣지 않는다.** 새 proto 메시지를 만들지 않는다 — 벽 위치는 양쪽이 계산한다.
- **탈락 판정은 서버만 한다.** 클라는 예측하지 않는다.

### 값 (spec §5, 전부 MasterData 열)

| 이름 | 값 |
|---|---|
| `chaser_start_x` | −60 |
| `chaser_initial_speed` | 7 |
| `chaser_acceleration` | 0.075 |
| `chaser_max_speed` | 10 |

파생: 상한 도달 40.0초 / 무사고 완주 57.7초 / 벽이 결승선(632m) 도달 75.2초.

---

## 파일 구조

| 레포 | 파일 | 책임 |
|---|---|---|
| infrastructure | `table/Datas/#FlappyConfig.xlsx` | 튜닝값 네 개(P~S열) |
| LOP-Shared | `Runtime/Scripts/Game/FlappyChaserCurve.cs` (신규) | 경과 시간 → 벽 x. 순수 static 커널 |
| LOP-Shared | `Runtime/Scripts/Game/FlappyConfig.cs` | 값 네 개 |
| LOP-Shared | `Tests/EditMode/FlappyChaserCurveTests.cs` (신규) | 곡선 규칙 |
| MasterData-{Client,Server} | `Runtime.Generated/**` | Luban 생성물 |
| 서버 | `Assets/Scripts/Domain/FinishPlacements.cs` | 등수 무리 셋 → 넷 |
| 서버 | `Assets/Tests/Editor/FinishPlacementsTests.cs` | 탈락 무리 규칙 |
| 서버 | `Assets/Scripts/Game/TickSystems/FlappyChaserSystem.cs` (신규) | 매 틱 잡고 디스폰하고 순서를 적는다 |
| 서버 | `Assets/Scripts/Game/FlappyRaceRuleSystem.cs` | Watch 등록, 판 끝 조건, 등수 반영 |
| 서버 | `Assets/Scripts/Game/SkydiveRuleSystem.cs` | 호출부(빈 목록) |
| 서버 | `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` | 등록 + End 페이즈 |
| 서버 | `Assets/Scripts/Game/FlappyConfigProvider.cs` | 값 전달 |
| 클라 | `Assets/Scripts/Game/FlappyWatchTarget.cs` (신규) | "지금 누구를 보고 있나" 한 규칙 |
| 클라 | `Assets/Tests/Editor/FlappyWatchTargetTests.cs` (신규) | 그 규칙 |
| 클라 | `Assets/Scripts/Game/FlappyChaserView.cs` (신규) | 벽을 세우고 옮긴다. `X` 노출 |
| 클라 | `Assets/Scripts/UI/RaceEliminated/RaceEliminatedView.cs` (신규) | "탈락" 라벨 |
| 클라 | `Assets/UI/RaceEliminated/RaceEliminatedView.{uxml,uss}` (신규) | 그 레이아웃 |
| 클라 | `Assets/UI/UIViewCatalog.asset` | 새 뷰 등록 |
| 클라 | `Assets/Scripts/Game/FlappyHudCoordinator.cs` | 탈락 시 화면·카메라 전환 |
| 클라 | `Assets/Scripts/UI/FlapPad/*` | 추격자까지 남은 거리 |
| 클라 | `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` | 등록 |
| 클라 | `Assets/Scripts/Game/FlappyConfigProvider.cs` | 값 전달 |

---

## Task 1: 곡선과 값

벽 위치를 물으면 답이 나오는 상태까지. 아직 아무도 쓰지 않는다.

**Files:**
- Modify: `infrastructure/table/Datas/#FlappyConfig.xlsx`
- Modify: `LeagueOfPhysical-MasterData-Client/Runtime.Generated/**` (Luban 생성물)
- Modify: `LeagueOfPhysical-MasterData-Server/Runtime.Generated/**` (Luban 생성물)
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyConfig.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyChaserCurve.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyChaserCurveTests.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyConfigProvider.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyConfigProvider.cs`

**Interfaces:**
- Produces: `LOP.FlappyChaserCurve.XAt(in FlappyConfig config, float elapsedSeconds) -> float`,
  `LOP.FlappyChaserCurve.RampSeconds(in FlappyConfig config) -> float`
- Produces: `FlappyConfig.ChaserStartX`, `.ChaserInitialSpeed`, `.ChaserAcceleration`, `.ChaserMaxSpeed` (전부 `float`)
- Produces: `FlappyConfig` 생성자 꼬리에 선택 인자 넷 —
  `chaserStartX = 0f, chaserInitialSpeed = 0f, chaserAcceleration = 0f, chaserMaxSpeed = 0f`

- [ ] **Step 1: 브랜치를 판다 (레포 6개)**

유니티 레포는 워크트리를 쓰지 않는다. 각 레포에서:

```bash
cd <repo>
git fetch origin
git status --short          # 로컬 픽스처만 있는지 확인
git checkout -b feature/flappy-chaser origin/main
```

`infrastructure`, `LeagueOfPhysical-Shared`, `LeagueOfPhysical-Client`, `LeagueOfPhysical-Server`,
`LeagueOfPhysical-MasterData-Client`, `LeagueOfPhysical-MasterData-Server` 여섯 곳 전부.

- [ ] **Step 2: 엑셀에 열 넷을 넣는다**

`#FlappyConfig.xlsx`는 sharedStrings 없이 inline 문자열만 쓰는 단순한 파일이라 직접 고친다
(이 맥에는 openpyxl이 없다). 아래를 `infrastructure/table`에서 실행한다.

```python
python3 - <<'PY'
import zipfile, re, os
src = 'Datas/#FlappyConfig.xlsx'
cols = [('P', 'chaser_start_x', '-60'),
        ('Q', 'chaser_initial_speed', '7'),
        ('R', 'chaser_acceleration', '0.075'),
        ('S', 'chaser_max_speed', '10')]

z = zipfile.ZipFile(src)
items = [(i, z.read(i.filename)) for i in z.infolist()]
z.close()

def text(col, row, value):
    return f'<c r="{col}{row}" t="inlineStr"><is><t>{value}</t></is></c>'
def number(col, row, value):
    return f'<c r="{col}{row}" t="n"><v>{value}</v></c>'

def append_to_row(xml, row, extra):
    m = re.search(r'(<row r="%d"[^>]*>)(.*?)(</row>)' % row, xml, re.S)
    assert m, f'row {row} not found'
    return xml[:m.end(2)] + extra + xml[m.end(2):]

out = []
for info, data in items:
    if info.filename == 'xl/worksheets/sheet1.xml':
        xml = data.decode('utf-8')
        assert '<dimension ref="A1:O5" />' in xml, '이미 고쳐졌거나 모양이 다르다'
        xml = xml.replace('<dimension ref="A1:O5" />', '<dimension ref="A1:S5" />')
        xml = append_to_row(xml, 1, ''.join(text(c, 1, name) for c, name, _ in cols))
        xml = append_to_row(xml, 2, ''.join(text(c, 2, 'float') for c, _, _ in cols))
        xml = append_to_row(xml, 4, ''.join(text(c, 4, name) for c, name, _ in cols))
        xml = append_to_row(xml, 5, ''.join(number(c, 5, v) for c, _, v in cols))
        data = xml.encode('utf-8')
    out.append((info, data))

tmp = src + '.tmp'
with zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED) as w:
    for info, data in out:
        w.writestr(info, data)
os.replace(tmp, src)
print('ok')
PY
```

- [ ] **Step 3: 제대로 들어갔는지 읽어서 확인한다**

```bash
python3 - <<'PY'
import zipfile, re
xml = zipfile.ZipFile('Datas/#FlappyConfig.xlsx').read('xl/worksheets/sheet1.xml').decode('utf-8')
for row in (1, 2, 4, 5):
    body = re.search(r'<row r="%d"[^>]*>(.*?)</row>' % row, xml, re.S).group(1)
    cells = re.findall(r'<c r="([A-Z]+)\d+"[^>]*>(?:<is><t[^>]*>(.*?)</t></is>|<v>(.*?)</v>)</c>', body)
    print(row, [(c[0], c[1] or c[2]) for c in cells if c[0] in ('O', 'P', 'Q', 'R', 'S')])
PY
```

기대:
```
1 [('O', 'dash_charge_dive'), ('P', 'chaser_start_x'), ('Q', 'chaser_initial_speed'), ('R', 'chaser_acceleration'), ('S', 'chaser_max_speed')]
2 [('O', 'float'), ('P', 'float'), ('Q', 'float'), ('R', 'float'), ('S', 'float')]
4 [('O', 'dash_charge_dive'), ('P', 'chaser_start_x'), ('Q', 'chaser_initial_speed'), ('R', 'chaser_acceleration'), ('S', 'chaser_max_speed')]
5 [('O', '1.2'), ('P', '-60'), ('Q', '7'), ('R', '0.075'), ('S', '10')]
```

3행(`##group`)은 A열만 있고 비어 있다 — 그래야 이 값들이 클·서 양쪽 생성물에 다 들어간다.

- [ ] **Step 4: MasterData를 다시 생성하기 전에 두 패키지를 최신으로 맞춘다**

생성물의 `.meta`는 기계마다 GUID가 다르게 만들어진다. 다른 기계가 이미 올려 둔 생성물이 있으면
내가 만든 것과 GUID가 달라져 쓸데없는 충돌이 난다. **생성 전에 반드시 fetch한다.**

```bash
cd ../../LeagueOfPhysical-MasterData-Client && git fetch origin && git log --oneline -1 origin/main
cd ../LeagueOfPhysical-MasterData-Server && git fetch origin && git log --oneline -1 origin/main
```

두 브랜치가 Step 1에서 `origin/main`에서 갈라졌는지 확인한다.

- [ ] **Step 5: 생성한다**

```bash
cd /Users/insoobae/workspace/LOP/infrastructure/table
./gen.sh
```

`[done]`이 나와야 한다. 스크립트가 `.meta` 복원을 스스로 하지만(trap), 끝나고 두 패키지에서
`git status --short`로 **`.meta`가 삭제로 남아 있지 않은지** 확인한다.

```bash
git -C ../../LeagueOfPhysical-MasterData-Client status --short | head -20
git -C ../../LeagueOfPhysical-MasterData-Server status --short | head -20
```

- [ ] **Step 6: 생성된 스키마에 필드가 들어갔는지 확인한다**

```bash
grep -n "Chaser" ../../LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/LOP/MasterData/FlappyConfig.cs
grep -n "Chaser" ../../LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/LOP/MasterData/FlappyConfig.cs
```

`ChaserStartX` / `ChaserInitialSpeed` / `ChaserAcceleration` / `ChaserMaxSpeed` 네 프로퍼티가 보여야 한다.
경로가 다르면 `find ... -name 'FlappyConfig.cs'`로 찾는다.

- [ ] **Step 7: `FlappyConfig`에 값 넷을 붙인다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyConfig.cs`의 `DashChargeDive` 필드 **뒤**에 추가:

```csharp
        /// <summary>추격자(뒤에서 오는 벽)가 출발 전에 서 있는 x. 출발선 한참 뒤다.</summary>
        public readonly float ChaserStartX;

        /// <summary>추격자의 시작 속도. 새보다 한참 느려서 초반엔 실수 여유가 넉넉하다.</summary>
        public readonly float ChaserInitialSpeed;

        /// <summary>추격자가 빨라지는 정도(m/s²). 갈수록 실수 여유가 줄어든다.</summary>
        public readonly float ChaserAcceleration;

        /// <summary>
        /// 추격자의 속도 상한. <b><see cref="ForwardSpeed"/>보다 반드시 낮아야 한다</b> —
        /// 이것이 "한 번도 안 박은 사람은 절대 안 잡힌다"의 보증이다.
        /// </summary>
        public readonly float ChaserMaxSpeed;
```

생성자 시그니처의 마지막 인자(`float dashChargeDive`) 뒤에 추가:

```csharp
                            float dashChargeBase, float dashChargeDive,
                            //  추격자 값만 기본값을 준다. 추격자와 무관한 테스트가 열 몇 개짜리
                            //  자리채움을 적지 않아도 되게 하려는 것이다. 실제 provider는 항상
                            //  네 값을 명시하므로, 빠뜨리면 벽이 x=0에 멈춰 서서 출발하자마자
                            //  전원을 잡는다 — 조용히 틀리지 않고 즉시 드러난다.
                            float chaserStartX = 0f, float chaserInitialSpeed = 0f,
                            float chaserAcceleration = 0f, float chaserMaxSpeed = 0f)
```

생성자 본문 끝에 대입 네 줄:

```csharp
            ChaserStartX = chaserStartX;
            ChaserInitialSpeed = chaserInitialSpeed;
            ChaserAcceleration = chaserAcceleration;
            ChaserMaxSpeed = chaserMaxSpeed;
```

- [ ] **Step 8: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/FlappyChaserCurveTests.cs` 신규:

```csharp
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 추격자 벽의 위치 규칙. 이 곡선 하나가 "실수를 몇 번까지 봐주는가"를 정하므로,
    /// 상한에 닿는 시각과 그 뒤 등속이 이 게임의 난이도 그 자체다.
    /// </summary>
    public class FlappyChaserCurveTests
    {
        private const float Tolerance = 1e-3f;

        //  추격자와 전진속도만 실제 값이고 나머지는 이 테스트에 무의미한 자리채움이다.
        private static FlappyConfig Config()
            => new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                                stunTime: 0.8f, invulnTime: 0.6f,
                                dashMult: 2f, dashDuration: 0.2f, dashChargeBase: 0.13f, dashChargeDive: 1.2f,
                                chaserStartX: -60f, chaserInitialSpeed: 7f,
                                chaserAcceleration: 0.075f, chaserMaxSpeed: 10f);

        [Test]
        public void 출발_전에는_시작점에_서_있다()
        {
            Assert.That(FlappyChaserCurve.XAt(Config(), -1f), Is.EqualTo(-60f).Within(Tolerance));
            Assert.That(FlappyChaserCurve.XAt(Config(), 0f), Is.EqualTo(-60f).Within(Tolerance));
        }

        [Test]
        public void 상한에_닿는_시각은_속도차를_가속도로_나눈_값이다()
        {
            //  이 시각이 곧 압박 전환점이다 — 여기부터 실수 여유가 더 늘지 않는다.
            Assert.That(FlappyChaserCurve.RampSeconds(Config()), Is.EqualTo(40f).Within(Tolerance));
        }

        [Test]
        public void 가속하는_동안은_반가속도_제곱만큼_더_간다()
        {
            //  -60 + 7×40 + ½×0.075×40² = 280
            Assert.That(FlappyChaserCurve.XAt(Config(), 40f), Is.EqualTo(280f).Within(Tolerance));
        }

        [Test]
        public void 상한_뒤로는_등속이다()
        {
            //  280 + 10×20 = 480
            Assert.That(FlappyChaserCurve.XAt(Config(), 60f), Is.EqualTo(480f).Within(Tolerance));
        }

        [Test]
        public void 앞서_몇_번을_물었든_같은_시각이면_같은_답이다()
        {
            //  누적하지 않는다는 것이 이 곡선의 전부다. 누적하면 프레임 수에 따라 답이 갈리고
            //  되돌리기로 과거 틱을 물을 수도 없다.
            var config = Config();
            float once = FlappyChaserCurve.XAt(config, 75.2f);

            for (float t = 0f; t < 75.2f; t += 0.02f)
            {
                FlappyChaserCurve.XAt(config, t);
            }

            Assert.That(FlappyChaserCurve.XAt(config, 75.2f), Is.EqualTo(once).Within(0f));
        }

        [Test]
        public void 한_번도_안_박은_새는_영영_안_잡힌다()
        {
            //  상한이 전진속도보다 낮다는 것의 의미가 이것이다. 이 성질이 깨지면
            //  "완주가 기본"이라는 원칙 자체가 무너진다.
            var config = Config();
            float birdX = -3f;

            for (float t = 0f; t <= 120f; t += 0.02f)
            {
                birdX = -3f + config.ForwardSpeed * t;
                Assert.That(birdX - config.BodyRadius,
                    Is.GreaterThan(FlappyChaserCurve.XAt(config, t)), $"t={t}");
            }
        }
    }
}
```

- [ ] **Step 9: 컴파일하고 테스트를 돌려 빨간지 본다**

```bash
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --async_tests true
```

기대: `FlappyChaserCurve`가 없어 **컴파일 에러**. (테스트가 빨간 게 아니라 아예 안 돌아간다 —
이 단계에서는 그게 맞다.)

- [ ] **Step 10: 곡선을 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyChaserCurve.cs` 신규:

```csharp
namespace LOP
{
    /// <summary>
    /// 추격자(뒤에서 오는 벽)의 위치. 출발 후 지난 시간만 보고 답한다.
    ///
    /// <para>누적하지 않는 것이 핵심이다 — 어느 시점을 물어도 답이 하나라서, 클라와 서버가 각자
    /// 계산해도 같고(서버가 위치를 보낼 필요가 없다) 되돌리기로 과거 틱을 물어도 그때 값이 나온다.</para>
    ///
    /// <para>상태 없는 순수 계산이라 <c>*System</c>이 아니라 static 커널이다
    /// (<c>MovementMotor.CalcVelocity</c>와 같은 짝).</para>
    /// </summary>
    public static class FlappyChaserCurve
    {
        /// <summary>출발 후 <paramref name="elapsedSeconds"/>초 시점의 벽 x. 출발 전이면 시작점.</summary>
        public static float XAt(in FlappyConfig config, float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f)
            {
                return config.ChaserStartX;
            }

            float ramp = RampSeconds(config);
            if (elapsedSeconds <= ramp)
            {
                return config.ChaserStartX
                     + config.ChaserInitialSpeed * elapsedSeconds
                     + 0.5f * config.ChaserAcceleration * elapsedSeconds * elapsedSeconds;
            }

            return config.ChaserStartX
                 + config.ChaserInitialSpeed * ramp
                 + 0.5f * config.ChaserAcceleration * ramp * ramp
                 + config.ChaserMaxSpeed * (elapsedSeconds - ramp);
        }

        /// <summary>
        /// 상한 속도에 닿는 시각(초) = 압박 전환점. 가속이 없으면 영영 안 닿으므로 무한대를 준다
        /// (그러면 <see cref="XAt"/>이 계속 가속 구간을 쓴다).
        /// </summary>
        public static float RampSeconds(in FlappyConfig config)
        {
            float gap = config.ChaserMaxSpeed - config.ChaserInitialSpeed;
            if (gap <= 0f)
            {
                return 0f;
            }
            if (config.ChaserAcceleration <= 0f)
            {
                return float.PositiveInfinity;
            }
            return gap / config.ChaserAcceleration;
        }
    }
}
```

- [ ] **Step 11: 컴파일하고 테스트가 초록인지 본다**

```bash
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --async_tests true
```

기대: 전부 통과. **`total`이 6 늘었는지 확인한다** — 안 늘었으면 옛 어셈블리를 돌린 것이다.

- [ ] **Step 12: 테스트가 실패할 수 있는지 확인한다(뮤테이션)**

`FlappyChaserCurve.XAt`의 `0.5f * config.ChaserAcceleration`을 `1.0f * config.ChaserAcceleration`으로
바꾸고 Step 11을 다시 돌린다.

기대: `가속하는_동안은...`, `상한_뒤로는_등속이다`, `한_번도_안_박은_새는...`가 빨강.
확인한 뒤 `0.5f`로 되돌리고 다시 초록을 확인한다.

- [ ] **Step 13: 두 provider에 값을 붙인다**

클라 `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyConfigProvider.cs`와
서버 `LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyConfigProvider.cs` **양쪽 다**,
`dashChargeDive: r.DashChargeDive` 뒤를 이렇게 바꾼다:

```csharp
                dashChargeBase: r.DashChargeBase,
                dashChargeDive: r.DashChargeDive,
                chaserStartX: r.ChaserStartX,
                chaserInitialSpeed: r.ChaserInitialSpeed,
                chaserAcceleration: r.ChaserAcceleration,
                chaserMaxSpeed: r.ChaserMaxSpeed);
```

- [ ] **Step 14: 클라와 서버가 둘 다 컴파일되는지 본다**

```bash
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
```

**둘 다 한다.** 한쪽만 확인하고 푸시해서 main이 안 되는 상태로 올라간 적이 있다.

- [ ] **Step 15: 커밋한다 (레포 5개)**

레포마다 바꾼 파일만 경로로 지정한다. 커밋 전에 `git status --short`로 스테이지된 것이 의도한
파일뿐인지 확인한다 — 특히 클라·서버의 로컬 픽스처가 섞이지 않았는지.

```bash
# infrastructure
cd /Users/insoobae/workspace/LOP/infrastructure
git add table/Datas/#FlappyConfig.xlsx
git status --short
git commit -m "feat(table): 추격자 곡선 값 네 개를 FlappyConfig에 넣는다"

# MasterData-Client / MasterData-Server (각각)
cd ../LeagueOfPhysical-MasterData-Client
git add Runtime.Generated
git status --short
git commit -m "chore(masterdata): 추격자 값 반영 재생성"

# LOP-Shared
cd ../LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/FlappyChaserCurve.cs Runtime/Scripts/Game/FlappyChaserCurve.cs.meta \
        Runtime/Scripts/Game/FlappyConfig.cs Tests/EditMode/FlappyChaserCurveTests.cs \
        Tests/EditMode/FlappyChaserCurveTests.cs.meta
git status --short
git commit -m "feat(flappy): 추격자 벽 위치를 시간만의 함수로 만든다"

# 클라 / 서버 (각각 provider 한 파일)
git add Assets/Scripts/Game/FlappyConfigProvider.cs
git commit -m "feat(flappy): 추격자 값을 시뮬에 넘긴다"
```

`.meta`가 아직 없으면 유니티가 새 파일을 임포트해야 생긴다 — Step 11의 `recompile` 뒤면 있다.

---

## Task 2: 등수에 탈락 무리를 넣는다

순수 계산만. 아직 아무도 탈락시키지 않는다.

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Domain/FinishPlacements.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveRuleSystem.cs`
- Test: `LeagueOfPhysical-Server/Assets/Tests/Editor/FinishPlacementsTests.cs`

**Interfaces:**
- Produces: `FinishPlacements.Resolve(IReadOnlyList<FinishRecord> finished, IReadOnlyDictionary<string,string> entityIdToUserId, IReadOnlyList<(string userId, float progress)> unfinished, IReadOnlyList<string> eliminated, IReadOnlyList<string> left) -> MatchOutcome`
  — `eliminated`는 **먼저 잡힌 순**의 userId 목록이고 등수는 그 역순이다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/Editor/FinishPlacementsTests.cs`의 `Resolve` 헬퍼에 인자를 하나 더 단다
(기본값을 줘서 기존 테스트가 그대로 컴파일되게):

```csharp
        static MatchOutcome Resolve(
            IReadOnlyList<FinishRecord> finished,
            IReadOnlyDictionary<string, string> map,
            IReadOnlyList<(string, float)> unfinished = null,
            IReadOnlyList<string> eliminated = null,
            IReadOnlyList<string> left = null)
            => FinishPlacements.Resolve(finished, map,
                unfinished ?? new List<(string, float)>(),
                eliminated ?? new List<string>(),
                left ?? new List<string>());
```

그리고 파일 끝(마지막 `}` 두 개 앞)에 테스트 넷을 추가한다:

```csharp
        [Test]
        public void 늦게_잡힌_사람이_위다()
        {
            //  오래 버틴 것이 곧 더 잘한 것이다. 목록은 먼저 잡힌 순으로 들어오므로 등수는 역순이다.
            var outcome = Resolve(new FinishRecord[0], Map("a", "b", "c"),
                eliminated: new List<string> { "user-a", "user-b", "user-c" });

            Assert.AreEqual(1, PlacementOf(outcome, "user-c"));
            Assert.AreEqual(2, PlacementOf(outcome, "user-b"));
            Assert.AreEqual(3, PlacementOf(outcome, "user-a"));
        }

        [Test]
        public void 탈락자는_아직_달리는_사람보다_아래다()
        {
            //  살아 있으면 나중에 잡히더라도 지금 잡힌 사람보다는 위다.
            var outcome = Resolve(new FinishRecord[0], Map("a", "b"),
                unfinished: new List<(string, float)> { ("user-a", 100f) },
                eliminated: new List<string> { "user-b" });

            Assert.AreEqual(1, PlacementOf(outcome, "user-a"));
            Assert.AreEqual(2, PlacementOf(outcome, "user-b"));
        }

        [Test]
        public void 탈락자는_나간_사람보다_위다()
        {
            //  끝까지 달리다 잡힌 것과 도중에 나간 것은 다르다.
            var outcome = Resolve(new FinishRecord[0], Map("a", "b"),
                eliminated: new List<string> { "user-a" },
                left: new List<string> { "user-b" });

            Assert.AreEqual(1, PlacementOf(outcome, "user-a"));
            Assert.AreEqual(2, PlacementOf(outcome, "user-b"));
        }

        [Test]
        public void 완주자_뒤에_탈락자가_붙는다()
        {
            //  전원이 잡히지 않은 흔한 판. 완주한 둘이 1·2등, 잡힌 둘이 늦게 잡힌 순으로 3·4등.
            var outcome = Resolve(
                new[] { Rec("a", 10, 0.5f), Rec("b", 12, 0.2f) }, Map("a", "b", "c", "d"),
                eliminated: new List<string> { "user-c", "user-d" });

            Assert.AreEqual(1, PlacementOf(outcome, "user-a"));
            Assert.AreEqual(2, PlacementOf(outcome, "user-b"));
            Assert.AreEqual(3, PlacementOf(outcome, "user-d"));
            Assert.AreEqual(4, PlacementOf(outcome, "user-c"));
        }
```

- [ ] **Step 2: 컴파일해서 빨간지 본다**

```bash
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
```

기대: 인자 다섯 개짜리 `Resolve`가 없어 **컴파일 에러**.

- [ ] **Step 3: `FinishPlacements`에 무리를 넣는다**

시그니처를 바꾸고:

```csharp
        /// <param name="eliminated">추격자에게 잡힌 사람. <b>먼저 잡힌 순</b>으로 넘긴다 —
        /// 등수는 그 역순이다(오래 버틴 사람이 위). 탈락이 없는 게임은 빈 목록을 넘긴다.</param>
        public static MatchOutcome Resolve(
            IReadOnlyList<FinishRecord> finished,
            IReadOnlyDictionary<string, string> entityIdToUserId,
            IReadOnlyList<(string userId, float progress)> unfinished,
            IReadOnlyList<string> eliminated,
            IReadOnlyList<string> left)
```

`unfinished` 루프와 `left` 루프 **사이**에 넣는다:

```csharp
            //  늦게 잡힌 사람이 위다 — 오래 버틴 것이 더 잘한 것이다(배틀로얄 표준).
            for (int i = eliminated.Count - 1; i >= 0; i--)
            {
                outcome.placements.Add(new MatchPlacement { userId = eliminated[i], placement = next++ });
            }
```

클래스 XML 주석의 "세 무리" 문장도 고친다:

```csharp
    /// <para>네 무리를 이 순서로 놓는다: <b>닿은 사람</b>(먼저 닿은 순, 완전히 같으면 공동 순위) →
    /// <b>못 닿은 사람</b>(더 멀리 간 순) → <b>잡힌 사람</b>(늦게 잡힌 순) →
    /// <b>몸이 사라진 사람</b>(나간 사람).</para>
```

- [ ] **Step 4: Skydive 호출부를 고친다**

`Assets/Scripts/Game/SkydiveRuleSystem.cs`의 마지막 줄:

```csharp
            //  스카이다이브에는 탈락이 없다.
            return FinishPlacements.Resolve(finishSystem.Ordered, entityIdToUserId,
                unfinished, System.Array.Empty<string>(), left);
```

- [ ] **Step 5: 컴파일하고 테스트가 초록인지 본다**

```bash
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server --mode EditMode --async_tests true
```

기대: 전부 통과. **`total`이 4 늘었는지 확인한다.**

- [ ] **Step 6: 뮤테이션으로 확인한다**

새로 넣은 루프의 `for (int i = eliminated.Count - 1; i >= 0; i--)`를
`for (int i = 0; i < eliminated.Count; i++)`로 바꾸고 다시 돌린다.

기대: `늦게_잡힌_사람이_위다`, `완주자_뒤에_탈락자가_붙는다`가 빨강. 되돌리고 초록 확인.

- [ ] **Step 7: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Domain/FinishPlacements.cs Assets/Scripts/Game/SkydiveRuleSystem.cs \
        Assets/Tests/Editor/FinishPlacementsTests.cs
git status --short
git commit -m "feat(race): 등수에 탈락 무리를 넣는다 — 늦게 잡힌 사람이 위"
```

---

## Task 3: 서버가 잡는다

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/FlappyChaserSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceRuleSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`

**Interfaces:**
- Consumes: `FlappyChaserCurve.XAt(in FlappyConfig, float)` (Task 1),
  `FinishPlacements.Resolve(..., eliminated, left)` (Task 2)
- Produces: `FlappyChaserSystem.Watch(string entityId)`, `.Reset()`,
  `.EliminatedOrder -> IReadOnlyList<string>` (먼저 잡힌 순의 entityId),
  `.IsEliminated(string entityId) -> bool`

- [ ] **Step 1: 잡는 시스템을 만든다**

`Assets/Scripts/Game/TickSystems/FlappyChaserSystem.cs` 신규:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 뒤에서 오는 벽(추격자)이 누구를 잡았는지 매 틱 지켜본다. 벽 위치는 시계만 보면 나오므로
    /// (<see cref="FlappyChaserCurve"/>) 클라에 보낼 것이 없다 — 잡는 판단만 서버 권위다.
    ///
    /// <para>순서를 적는 일과 판을 끝내는 일을 나누는 것은 <see cref="FinishLineTrackingSystem"/>과
    /// 같은 구조다. 룰에는 틱이 없어서다.</para>
    /// </summary>
    public class FlappyChaserSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.IWorld world;
        private readonly FinishLineTrackingSystem finishSystem;
        private readonly EntitySpawner entitySpawner;
        private readonly FlappyConfig config;

        private readonly List<string> watched = new List<string>();
        private readonly List<string> eliminated = new List<string>();

        public FlappyChaserSystem(GameFramework.World.EntityRegistry entityRegistry,
                                  GameFramework.World.IWorld world,
                                  FinishLineTrackingSystem finishSystem,
                                  EntitySpawner entitySpawner,
                                  FlappyConfig config)
        {
            this.entityRegistry = entityRegistry;
            this.world = world;
            this.finishSystem = finishSystem;
            this.entitySpawner = entitySpawner;
            this.config = config;
        }

        /// <summary>먼저 잡힌 순. 등수는 이 역순이다 — 오래 버틴 사람이 위다.</summary>
        public IReadOnlyList<string> EliminatedOrder => eliminated;

        public bool IsEliminated(string entityId) => eliminated.Contains(entityId);

        public void Watch(string entityId) => watched.Add(entityId);

        public void Reset()
        {
            watched.Clear();
            eliminated.Clear();
        }

        public void Tick(long tick, float deltaTime)
        {
            //  출발 전엔 벽이 시작점에 멈춰 있다. 출발틱이 아직 안 정해졌으면 long.MaxValue라
            //  이 비교가 그 경우도 같이 막는다.
            if (tick < world.GameplayStartTick)
            {
                return;
            }

            float wallX = FlappyChaserCurve.XAt(config, (tick - world.GameplayStartTick) * deltaTime);

            for (int i = 0; i < watched.Count; i++)
            {
                string entityId = watched[i];
                if (finishSystem.HasFinished(entityId) || eliminated.Contains(entityId))
                {
                    continue;
                }

                var body = entityRegistry.Get(entityId)?.Get<GameFramework.World.Transform>();
                if (body == null)
                {
                    continue;   // 나간 사람의 몸은 이미 없다
                }

                //  중심이 아니라 꼬리로 잰다 — 결승선도 형상으로 재므로(부리가 닿는 순간),
                //  여기만 중심으로 재면 화면에서 보이는 것과 결과가 어긋난다.
                if (body.Position.X - config.BodyRadius > wallX)
                {
                    continue;
                }

                eliminated.Add(entityId);
                entitySpawner.Despawn(entityId);
                Debug.Log($"[Chaser] {entityId} 탈락 — tick={tick} 벽={wallX:F1}m 새={body.Position.X:F1}m");
            }
        }
    }
}
```

- [ ] **Step 2: 룰이 감시 대상을 등록하고 등수에 반영하게 한다**

`Assets/Scripts/Game/FlappyRaceRuleSystem.cs`:

(a) 필드와 생성자에 추가

```csharp
        private readonly FinishLineTrackingSystem finishSystem;
        private readonly FlappyChaserSystem chaserSystem;
```

```csharp
        public FlappyRaceRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner,
                                    GameFramework.World.EntityRegistry entityRegistry,
                                    FinishLineTrackingSystem finishSystem,
                                    FlappyChaserSystem chaserSystem)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
            this.entityRegistry = entityRegistry;
            this.finishSystem = finishSystem;
            this.chaserSystem = chaserSystem;
        }
```

(b) `Initialize`의 참가자 루프에서 `finishSystem.Watch(entityId);` 바로 아래에

```csharp
                chaserSystem.Watch(entityId);
```

(c) `Deinitialize`에서 `finishSystem.Reset();` 아래에

```csharp
            chaserSystem.Reset();
```

(d) `IsMatchOver`를 바꾼다

```csharp
        /// <summary>
        /// 남아 있는 새가 전원 결승선에 닿거나, 아무도 달리고 있지 않으면 끝난다.
        /// 시간 상한은 러너가 따로 본다.
        /// </summary>
        public bool IsMatchOver => finishSystem.AllWatchedFinished || EveryoneAccountedFor;

        //  전원이 잡히는 판을 위한 조건이다. 결승선 추적은 몸이 없는 사람을 세지 않는데
        //  (나간 사람을 기다리면 판이 영영 안 끝난다), 전원이 잡히면 셀 사람이 하나도 없어져
        //  "전원 통과"가 거짓이 된다. 그대로 두면 90초 상한까지 빈 화면을 기다린다.
        //
        //  탈락자가 한 명 이상일 때만 보는 이유는 스폰 직전이다 — 아직 몸이 하나도 없는 그
        //  순간을 "전원 정리됨"으로 읽으면 시작하자마자 판이 끝난다.
        private bool EveryoneAccountedFor
        {
            get
            {
                if (chaserSystem.EliminatedOrder.Count == 0)
                {
                    return false;
                }
                foreach (var pair in entityIdToUserId)
                {
                    if (finishSystem.HasFinished(pair.Key))
                    {
                        continue;
                    }
                    if (entityRegistry.Get(pair.Key) != null)
                    {
                        return false;   // 아직 달리는 새가 있다
                    }
                }
                return true;
            }
        }
```

(e) `ResolveOutcome`을 바꾼다

```csharp
        public MatchOutcome ResolveOutcome()
        {
            var unfinished = new List<(string userId, float progress)>();
            var left = new List<string>();

            foreach (var pair in entityIdToUserId)
            {
                if (finishSystem.HasFinished(pair.Key))
                {
                    continue;
                }
                var body = entityRegistry.Get(pair.Key)?.Get<GameFramework.World.Transform>();
                if (body != null)
                {
                    unfinished.Add((pair.Value, body.Position.X));   // +x로 달리므로 클수록 앞선다
                    continue;
                }
                //  몸이 없는 사람은 둘이다 — 잡힌 사람과 그냥 나간 사람. 잡힌 쪽이 위다.
                if (chaserSystem.IsEliminated(pair.Key) == false)
                {
                    left.Add(pair.Value);
                }
            }

            //  잡힌 순서 그대로 옮긴다. 딕셔너리 순회 순서로 모으면 "늦게 잡힌 사람이 위"가 깨진다.
            var eliminated = new List<string>();
            foreach (string entityId in chaserSystem.EliminatedOrder)
            {
                if (entityIdToUserId.TryGetValue(entityId, out string userId))
                {
                    eliminated.Add(userId);
                }
            }

            return FinishPlacements.Resolve(finishSystem.Ordered, entityIdToUserId,
                unfinished, eliminated, left);
        }
```

- [ ] **Step 3: 배선한다**

`Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`의 `FinishLineTrackingSystem` 등록 **아래**에:

```csharp
            builder.Register<FlappyChaserSystem>(Lifetime.Singleton);
```

그리고 `RegisterBuildCallback`을 이렇게 바꾼다:

```csharp
            //  도착 감시를 러너의 End 페이즈에 문다. 시스템이 스스로 IRunner를 잡으면
            //  러너→룰→도착→러너로 고리가 생겨 컨테이너가 아예 안 만들어진다.
            //  추격자는 그 **뒤**여야 한다 — 앞에 두면 같은 틱에 결승선을 넘은 새를 잡는다.
            builder.RegisterBuildCallback(container =>
            {
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<FinishLineTrackingSystem>());
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<FlappyChaserSystem>());
            });
```

- [ ] **Step 4: 컴파일한다**

```bash
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server --mode EditMode --async_tests true
```

기대: 컴파일 통과, 기존 테스트 전부 초록.

> 이 태스크에는 자동 테스트가 없다 — 잡는 판단은 레지스트리·스포너·결승선 추적을 엮는 배선이고,
> 그 조각들은 각각 이미 검증돼 있다. 실제 동작은 Task 6의 2인 라이브에서 본다.

- [ ] **Step 5: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/TickSystems/FlappyChaserSystem.cs \
        Assets/Scripts/Game/TickSystems/FlappyChaserSystem.cs.meta \
        Assets/Scripts/Game/FlappyRaceRuleSystem.cs \
        Assets/Scripts/Game/FlappyRaceLifetimeScope.cs
git status --short
git commit -m "feat(flappy): 서버가 추격자에게 잡힌 새를 지우고 등수에 반영한다"
```

---

## Task 4: 클라가 벽을 그린다

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyWatchTarget.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyChaserView.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`
- Test: `LeagueOfPhysical-Client/Assets/Tests/Editor/FlappyWatchTargetTests.cs`

**Interfaces:**
- Consumes: `FlappyChaserCurve.XAt(in FlappyConfig, float)` (Task 1)
- Produces: `FlappyWatchTarget.Resolve(GameFramework.World.EntityRegistry registry, string myEntityId) -> string`
- Produces: `FlappyChaserView.X -> float` (지금 그려진 벽의 x)

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/Editor/FlappyWatchTargetTests.cs` 신규:

```csharp
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// "지금 누구를 보고 있나". 카메라와 벽 그리기가 같은 답을 봐야 해서 규칙이 한 곳에 있다 —
    /// 벽은 보고 있는 새와 같은 시각으로 그려야 하는데(내 새는 앞, 남의 새는 뒤),
    /// 둘이 다른 새를 고르면 벽이 엉뚱한 시각에 그려진다.
    /// </summary>
    public class FlappyWatchTargetTests
    {
        private static Entity Bird(string id, float x)
        {
            var bird = new Entity(id);
            bird.Add(new EntityKind(EntityType.Character));
            bird.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, 0f, 0f) });
            return bird;
        }

        private static EntityRegistry Registry(params Entity[] entities)
        {
            var registry = new EntityRegistry();
            foreach (var entity in entities)
            {
                registry.Add(entity);
            }
            return registry;
        }

        [Test]
        public void 내_새가_살아_있으면_내_새다()
        {
            var registry = Registry(Bird("me", 50f), Bird("other", 10f));

            Assert.AreEqual("me", FlappyWatchTarget.Resolve(registry, "me"));
        }

        [Test]
        public void 내_새가_없으면_가장_뒤처진_새다()
        {
            //  선두가 아니라 꼴찌를 본다 — 다음에 잡힐 사람이라 벽이 같은 화면에 있다.
            var registry = Registry(Bird("a", 50f), Bird("b", 10f), Bird("c", 30f));

            Assert.AreEqual("b", FlappyWatchTarget.Resolve(registry, "me"));
        }

        [Test]
        public void 같은_자리면_id가_작은_쪽이다()
        {
            //  레지스트리 순회 순서는 정해져 있지 않다. 안 정하면 프레임마다 카메라가 오간다.
            var registry = Registry(Bird("b", 10f), Bird("a", 10f));

            Assert.AreEqual("a", FlappyWatchTarget.Resolve(registry, "me"));
        }

        [Test]
        public void 새가_하나도_없으면_아무도_아니다()
        {
            Assert.IsNull(FlappyWatchTarget.Resolve(Registry(), "me"));
        }

        [Test]
        public void 새가_아닌_것은_세지_않는다()
        {
            //  아이템도 레지스트리에 있고 x가 더 작을 수 있다. 카메라가 그쪽으로 가면 안 된다.
            var item = new Entity("item");
            item.Add(new EntityKind(EntityType.Item));
            item.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(-100f, 0f, 0f) });

            Assert.AreEqual("bird", FlappyWatchTarget.Resolve(Registry(item, Bird("bird", 10f)), "me"));
        }
    }
}
```

- [ ] **Step 2: 컴파일해서 빨간지 본다**

```bash
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

기대: `FlappyWatchTarget`이 없어 **컴파일 에러**.

`EntityType.Item`이 없다면 `unity cmd`로 실제 enum 값을 확인하고
(`grep -rn "enum EntityType" ../LeagueOfPhysical-Shared`) 캐릭터가 아닌 값 하나로 바꾼다.

- [ ] **Step 3: 규칙을 만든다**

`Assets/Scripts/Game/FlappyWatchTarget.cs` 신규:

```csharp
namespace LOP
{
    /// <summary>
    /// 지금 누구를 보고 있나. 살아 있으면 내 새, 탈락했으면 <b>생존자 중 꼴찌</b>다 —
    /// 다음에 잡힐 사람이라 추격자가 같은 화면 안에 있다(선두를 보면 벽이 화면 밖이라
    /// 아무 일도 안 일어난다).
    ///
    /// <para>카메라와 벽 그리기가 같은 답을 봐야 해서 규칙을 한 곳에 둔다.</para>
    /// </summary>
    public static class FlappyWatchTarget
    {
        public static string Resolve(GameFramework.World.EntityRegistry entityRegistry, string myEntityId)
        {
            if (string.IsNullOrEmpty(myEntityId) == false && entityRegistry.Get(myEntityId) != null)
            {
                return myEntityId;
            }

            string best = null;
            float bestX = float.MaxValue;
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                var body = entity.Get<GameFramework.World.Transform>();
                if (body == null)
                {
                    continue;
                }
                //  같은 자리면 id가 작은 쪽. 레지스트리 순회 순서가 정해져 있지 않아서,
                //  안 정하면 프레임마다 카메라가 두 새 사이를 오갈 수 있다.
                if (body.Position.X < bestX ||
                    (body.Position.X == bestX && string.CompareOrdinal(entity.Id, best) < 0))
                {
                    best = entity.Id;
                    bestX = body.Position.X;
                }
            }
            return best;
        }
    }
}
```

- [ ] **Step 4: 컴파일하고 테스트가 초록인지 본다**

```bash
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --async_tests true
```

기대: 전부 통과. **`total`이 5 늘었는지 확인한다.**

- [ ] **Step 5: 뮤테이션으로 확인한다**

`body.Position.X < bestX`를 `body.Position.X > bestX`로 바꾸고 다시 돌린다.

기대: `내_새가_없으면_가장_뒤처진_새다`가 빨강. 되돌리고 초록 확인.

- [ ] **Step 6: 벽을 그린다**

`Assets/Scripts/Game/FlappyChaserView.cs` 신규:

```csharp
using System;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 추격자를 화면에 세운다. 위치는 시계만 보면 나오므로(<see cref="FlappyChaserCurve"/>)
    /// 서버에서 받는 것이 없다.
    ///
    /// <para><b>어느 시각으로 그릴지</b>가 이 클래스의 핵심이다. 화면엔 시간대가 둘이다 —
    /// 내 새는 예측이라 조금 앞, 남의 새는 지연 보간이라 조금 뒤. 벽은 하나뿐이라 둘 다 맞출 수
    /// 없어서 <b>지금 보고 있는 새</b>의 시각(<see cref="EntityRenderClock"/>)에 맞춘다.
    /// 대가로, 내가 살아 있는 동안 남이 잡히면 그 새가 벽 안으로 3m쯤 들어간 뒤에 사라진다.</para>
    ///
    /// <para>씬에 미리 놓을 대상이 아니고 프레임마다 트랜스폼 하나만 옮기므로 MonoBehaviour가
    /// 아니라 진입점이다. 카메라가 <c>LateUpdate</c>에서 움직이므로 그 뒤에 읽는다.</para>
    /// </summary>
    public class FlappyChaserView : ILateTickable, IDisposable
    {
        private const float WallHeight = 300f;
        private const float WallThickness = 2f;

        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.IWorld world;
        private readonly EntityRenderClock renderClock;
        private readonly IPlayerContext playerContext;
        private readonly CameraController cameraController;
        private readonly FlappyConfig config;

        private GameObject wall;

        /// <summary>지금 그려진 벽의 x. HUD가 이 값을 읽어야 숫자와 그림이 어긋나지 않는다.</summary>
        public float X { get; private set; }

        public FlappyChaserView(GameFramework.World.EntityRegistry entityRegistry,
                                GameFramework.World.IWorld world,
                                EntityRenderClock renderClock,
                                IPlayerContext playerContext,
                                CameraController cameraController,
                                FlappyConfig config)
        {
            this.entityRegistry = entityRegistry;
            this.world = world;
            this.renderClock = renderClock;
            this.playerContext = playerContext;
            this.cameraController = cameraController;
            this.config = config;
        }

        public void LateTick()
        {
            EnsureWall();

            X = FlappyChaserCurve.XAt(
                config, ElapsedSeconds(FlappyWatchTarget.Resolve(entityRegistry, playerContext.entityId)));

            Vector3 position = wall.transform.position;
            position.x = X;
            //  세로는 카메라를 따라간다 — 고도가 변하는 맵에서도 화면 세로를 늘 덮게.
            if (cameraController.MainCamera != null)
            {
                position.y = cameraController.MainCamera.transform.position.y;
            }
            wall.transform.position = position;
        }

        private float ElapsedSeconds(string watchedEntityId)
        {
            double secondsPerTick = renderClock.SecondsPerTick;
            if (secondsPerTick <= 0d || world.GameplayStartTick == long.MaxValue)
            {
                return 0f;   // 아직 출발 정보가 없다 — 벽은 시작점에 서 있는다
            }
            return (float)((renderClock.TickFor(watchedEntityId) - world.GameplayStartTick) * secondsPerTick);
        }

        private void EnsureWall()
        {
            if (wall != null)
            {
                return;
            }

            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "FlappyChaserWall";

            //  물리 몸을 주면 새를 밀어 클·서 시뮬이 갈린다. 판정은 서버의 x 비교뿐이다.
            var collider = wall.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            wall.transform.localScale = new Vector3(WallThickness, WallHeight, WallThickness);

            //  추격자의 정체는 아트 단계 몫이라 지금은 붉은 판으로 자리만 잡는다.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                wall.GetComponent<MeshRenderer>().sharedMaterial =
                    new Material(shader) { color = new Color(0.85f, 0.15f, 0.15f) };
            }
        }

        public void Dispose()
        {
            if (wall != null)
            {
                UnityEngine.Object.Destroy(wall);
                wall = null;
            }
        }
    }
}
```

- [ ] **Step 7: 배선한다**

`Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`의 `RegisterEntryPoint<FlappyHudCoordinator>();` **위**에:

```csharp
            //  AsSelf로도 등록한다 — FlapPad가 "추격자까지 몇 m"를 그리려면 벽 위치를 읽어야 하고,
            //  같은 값을 읽어야 숫자와 그림이 어긋나지 않는다.
            builder.RegisterEntryPoint<FlappyChaserView>().AsSelf();
```

- [ ] **Step 8: 컴파일한다**

```bash
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --async_tests true
```

기대: 컴파일 통과, 테스트 전부 초록.

- [ ] **Step 9: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/FlappyWatchTarget.cs Assets/Scripts/Game/FlappyWatchTarget.cs.meta \
        Assets/Scripts/Game/FlappyChaserView.cs Assets/Scripts/Game/FlappyChaserView.cs.meta \
        Assets/Scripts/Game/FlappyRaceLifetimeScope.cs \
        Assets/Tests/Editor/FlappyWatchTargetTests.cs Assets/Tests/Editor/FlappyWatchTargetTests.cs.meta
git status --short
git commit -m "feat(flappy): 클라가 추격자 벽을 보고 있는 새의 시각으로 그린다"
```

---

## Task 5: 탈락했을 때의 화면

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/UI/RaceEliminated/RaceEliminatedView.uxml`
- Create: `LeagueOfPhysical-Client/Assets/UI/RaceEliminated/RaceEliminatedView.uss`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/UI/RaceEliminated/RaceEliminatedView.cs`
- Modify: `LeagueOfPhysical-Client/Assets/UI/UIViewCatalog.asset`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyHudCoordinator.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/UI/FlapPad/FlapPadViewModel.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/UI/FlapPad/FlapPadView.cs`
- Modify: `LeagueOfPhysical-Client/Assets/UI/FlapPad/FlapPad.uxml`
- Modify: `LeagueOfPhysical-Client/Assets/UI/FlapPad/FlapPad.uss`

**Interfaces:**
- Consumes: `FlappyWatchTarget.Resolve(...)`, `FlappyChaserView.X` (Task 4)
- Produces: `LOP.UI.RaceEliminatedView` (VM 없음 — 바인딩할 라이브 상태가 없다)

> **spec에서 두 군데 고친다.**
>
> ① spec §8은 "추격자까지 남은 거리"를 디버그 HUD에 붙이라고 썼는데, `DebugHudViewModel`은
> 게임을 가리지 않는 공용이라 거기에 `FlappyConfig`를 주입하면 Flappy가 다른 게임 전부로 새어
> 나간다. 그래서 Flappy 전용 화면인 **FlapPad**에 붙인다.
>
> ② spec §6은 탈락 처리를 새 `FlappyEliminationCoordinator`에 두라고 썼는데, 입력면을 닫으려면
> **그 인스턴스를 들고 있어야** 하고(`IWindowManager.Close`는 뷰 객체를 받는다) 그걸 여는 것은
> `FlappyHudCoordinator`다. 코디네이터를 둘로 나누면 뷰 참조를 넘겨 주는 배선만 늘어난다. 열고
> 닫는 일이 한 책임이므로 **`FlappyHudCoordinator`를 늘린다.**

- [ ] **Step 1: 레이아웃을 만든다**

`Assets/UI/RaceEliminated/RaceEliminatedView.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="race-eliminated-root" class="race-eliminated-root" picking-mode="Ignore">
        <ui:Label name="race-eliminated-title" class="race-eliminated-title" text="탈락" picking-mode="Ignore" />
        <ui:Label name="race-eliminated-note" class="race-eliminated-note" text="남은 주자를 지켜봅니다" picking-mode="Ignore" />
    </ui:VisualElement>
</ui:UXML>
```

`Assets/UI/RaceEliminated/RaceEliminatedView.uss`:

```css
/* 아래 화면을 가리지 않는다 — 관전 중이라 월드가 계속 보여야 한다 */
.race-eliminated-root {
    flex-grow: 1;
    justify-content: flex-start;
    align-items: center;
    padding-top: 80px;
}

.race-eliminated-title {
    font-size: 72px;
    color: rgb(255, 92, 92);
    -unity-text-align: middle-center;
}

.race-eliminated-note {
    font-size: 26px;
    color: rgba(255, 255, 255, 0.6);
    -unity-text-align: middle-center;
}
```

- [ ] **Step 2: 뷰를 만든다**

`Assets/Scripts/UI/RaceEliminated/RaceEliminatedView.cs`:

```csharp
namespace LOP.UI
{
    /// <summary>
    /// 추격자에게 잡혔음을 알린다. 바뀌는 값이 없어 ViewModel이 없다 — 라벨은 UXML에 박혀 있고
    /// 이 클래스는 밴드와 입력 규칙만 정한다.
    ///
    /// <para>Notification이 아니라 Window인 이유는 <see cref="RaceStartView"/>와 같다:
    /// 이건 토스트가 아니라 게임 화면이라 로딩·결과 같은 전체화면 오버레이에 <b>가려져야</b> 한다.</para>
    /// </summary>
    public class RaceEliminatedView : UIView
    {
        public override UILayer Layer => UILayer.Window;
    }
}
```

- [ ] **Step 3: 유니티에 임포트시켜 `.meta`(GUID)를 만든다**

```bash
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
ls Assets/UI/RaceEliminated/
```

`RaceEliminatedView.uxml.meta`와 `RaceEliminatedView.uss.meta`가 생겨야 한다. 안 생기면
에디터 창을 한 번 포커스해 임포트를 돌린 뒤 다시 확인한다.

- [ ] **Step 4: 카탈로그에 등록한다**

먼저 GUID를 읽는다:

```bash
grep guid Assets/UI/RaceEliminated/RaceEliminatedView.uxml.meta
grep guid Assets/UI/RaceEliminated/RaceEliminatedView.uss.meta
```

`Assets/UI/UIViewCatalog.asset`에서 `- viewName: RaceStartView` 항목 **아래**에 같은 모양으로 넣는다.
`fileID`는 다른 항목과 같은 값을 그대로 쓴다(uxml은 `9197481963319205126`, uss는 `7433441132597879392`).

```yaml
  - viewName: RaceEliminatedView
    uxml: {fileID: 9197481963319205126, guid: <uxml의 guid>, type: 3}
    uss: {fileID: 7433441132597879392, guid: <uss의 guid>, type: 3}
```

- [ ] **Step 5: 뷰를 DI에 등록한다**

`Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`의 `ConfigureGame` 끝, `RaceStartView` 등록 아래:

```csharp
            builder.Register<RaceEliminatedView>(Lifetime.Transient);
```

같은 파일의 `RegisterViewFactories`에:

```csharp
            sink.Add(windowManager.RegisterViewFactory<RaceEliminatedView>(() => container.Resolve<RaceEliminatedView>()));
```

- [ ] **Step 6: 코디네이터가 탈락을 처리하게 한다**

`Assets/Scripts/Game/FlappyHudCoordinator.cs`를 통째로 이렇게 바꾼다:

```csharp
using GameFramework;
using LOP.Event.Entity;
using LOP.UI;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// Flappy 인게임 화면을 여닫는다. 내 새가 생기면 입력면을 열고, 추격자에게 잡히면 입력면을 닫고
    /// "탈락"을 띄운 뒤 카메라를 남은 사람에게 넘긴다.
    ///
    /// <para>화면 교체는 "큰 흐름"이라 코디네이터 책임이다(아키텍처 가이드라인 "흐름의 경계").
    /// 카메라 타깃도 같은 흐름이라 여기서 함께 다룬다 — 입력면 인스턴스를 이 클래스가 들고 있어야
    /// 닫을 수 있는 것도 이유다.</para>
    /// </summary>
    public class FlappyHudCoordinator : MessageHandlerBase
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IWindowManager windowManager;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ActorRegistry actorRegistry;
        private readonly CameraController cameraController;
        private readonly ISubscriber<EntityCreated> entityCreatedSubscriber;
        private readonly ISubscriber<EntityDestroyed> entityDestroyedSubscriber;
        private readonly ISubscriber<MatchEndedToC> matchEndedSubscriber;

        private bool _opened;
        private bool _matchEnded;
        private FlapPadView _flapPad;
        private string _cameraTargetId;

        public FlappyHudCoordinator(IGameDataStore gameDataStore, IWindowManager windowManager,
            GameFramework.World.EntityRegistry entityRegistry,
            ActorRegistry actorRegistry,
            CameraController cameraController,
            ISubscriber<EntityCreated> entityCreatedSubscriber,
            ISubscriber<EntityDestroyed> entityDestroyedSubscriber,
            ISubscriber<MatchEndedToC> matchEndedSubscriber)
        {
            this.gameDataStore = gameDataStore;
            this.windowManager = windowManager;
            this.entityRegistry = entityRegistry;
            this.actorRegistry = actorRegistry;
            this.cameraController = cameraController;
            this.entityCreatedSubscriber = entityCreatedSubscriber;
            this.entityDestroyedSubscriber = entityDestroyedSubscriber;
            this.matchEndedSubscriber = matchEndedSubscriber;
        }

        protected override void Subscribe()
        {
            Track(entityCreatedSubscriber.Subscribe(OnEntityCreated));
            Track(entityDestroyedSubscriber.Subscribe(OnEntityDestroyed));
            Track(matchEndedSubscriber.Subscribe(_ => _matchEnded = true));
        }

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            if (_opened || entityCreated.entityId != gameDataStore.userEntityId)
            {
                return;
            }

            // 입력면을 먼저 열어 Window 밴드 최하단에 깐다(전체화면이라 위 위젯 입력을 막지 않도록).
            _flapPad = windowManager.Open<FlapPadView>();
            windowManager.Open<DebugHudView>();
            windowManager.Open<RaceStartView>();
            _opened = true;
            _cameraTargetId = gameDataStore.userEntityId;
        }

        private void OnEntityDestroyed(EntityDestroyed entityDestroyed)
        {
            //  판이 끝나면 방을 정리하면서 엔티티도 사라진다. 그것까지 탈락으로 읽으면
            //  결과 화면 위에 "탈락"이 겹친다.
            if (_opened == false || _matchEnded)
            {
                return;
            }

            if (entityDestroyed.entityId == gameDataStore.userEntityId)
            {
                if (_flapPad != null)
                {
                    windowManager.Close(_flapPad);   // 대시 버튼도 함께 사라진다
                    _flapPad = null;
                }
                windowManager.Open<RaceEliminatedView>();
            }

            FollowNextRunner();
        }

        //  보고 있던 새가 사라졌으면 다음 사람에게 넘긴다. 규칙은 벽을 그리는 쪽과 같은 것을 쓴다
        //  — 둘이 다른 새를 고르면 벽이 화면 속 새와 다른 시각으로 그려진다.
        private void FollowNextRunner()
        {
            string next = FlappyWatchTarget.Resolve(entityRegistry, gameDataStore.userEntityId);
            if (next == null || next == _cameraTargetId)
            {
                return;
            }

            var visual = actorRegistry.Get(next)?.visualGameObject;
            if (visual == null)
            {
                return;   // 아직 몸이 안 붙었다 — 다음 소멸 때 다시 본다
            }

            _cameraTargetId = next;
            cameraController.SetTarget(visual.transform);
        }
    }
}
```

- [ ] **Step 7: FlapPad에 추격자까지 남은 거리를 붙인다**

`Assets/UI/FlapPad/FlapPad.uxml`의 `flap-surface` **다음**(대시 버튼 앞)에 라벨을 넣는다:

```xml
        <ui:Label name="chaser-gap" class="chaser-gap" text="" picking-mode="Ignore" />
```

`Assets/UI/FlapPad/FlapPad.uss` 끝에:

```css
/* 추격자까지 남은 거리 — 곡선을 튜닝하려면 이 숫자를 봐야 한다 */
.chaser-gap {
    position: absolute;
    left: 32px;
    top: 32px;
    font-size: 30px;
    color: rgba(255, 255, 255, 0.7);
}
```

`Assets/Scripts/UI/FlapPad/FlapPadViewModel.cs` — 네 군데를 고친다.

(a) 필드 추가 (`_entityRegistry` 아래):

```csharp
        private readonly FlappyChaserView _chaserView;
```

(b) 상태 필드와 프로퍼티 추가 (`_canDash` 아래):

```csharp
        private readonly ReactiveProperty<float> _chaserGap = new ReactiveProperty<float>(0f);
```

```csharp
        /// <summary>추격자까지 남은 거리(m). 벽을 그리는 쪽과 <b>같은 값</b>을 읽는다 —
        /// 각자 계산하면 숫자와 화면 속 벽이 어긋난다.</summary>
        public ReadOnlyReactiveProperty<float> ChaserGap => _chaserGap;
```

(c) 생성자에 인자를 하나 더 받는다:

```csharp
        public FlapPadViewModel(PlayerInputManager playerInputManager,
                                IPlayerContext playerContext,
                                GameFramework.World.EntityRegistry entityRegistry,
                                FlappyChaserView chaserView)
        {
            _playerInputManager = playerInputManager;
            _playerContext = playerContext;
            _entityRegistry = entityRegistry;
            _chaserView = chaserView;
        }
```

(d) `Refresh()`의 `_canDash.Value = charge >= 1f;` 아래에:

```csharp
            _chaserGap.Value = entity == null
                ? 0f
                : (entity.Get<GameFramework.World.Transform>()?.Position.X ?? 0f) - _chaserView.X;
```

`Assets/Scripts/UI/FlapPad/FlapPadView.cs`의 `OnOpen`에서 `CanDash` 구독 아래:

```csharp
            var chaserGap = Root.Q<Label>("chaser-gap");
            _viewModel.ChaserGap
                .Subscribe(gap => chaserGap.text = $"추격자 {gap:F0}m")
                .AddTo(_subscriptions);
```

`FlapPadView.cs` 맨 위 using에 `using UnityEngine.UIElements;`가 이미 있으므로 `Label`은 그대로 쓴다.

- [ ] **Step 8: 컴파일하고 테스트를 돌린다**

```bash
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --async_tests true
```

기대: 컴파일 통과, 테스트 전부 초록.

> 이 태스크에도 자동 테스트가 없다 — 창을 여닫고 카메라를 옮기는 배선이고, 규칙(`FlappyWatchTarget`)은
> Task 4에서 이미 검증했다. 실제 동작은 Task 6에서 본다.

- [ ] **Step 9: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/UI/RaceEliminated Assets/Scripts/UI/RaceEliminated \
        Assets/UI/UIViewCatalog.asset \
        Assets/Scripts/Game/FlappyHudCoordinator.cs \
        Assets/Scripts/Game/FlappyRaceLifetimeScope.cs \
        Assets/Scripts/UI/FlapPad/FlapPadViewModel.cs Assets/Scripts/UI/FlapPad/FlapPadView.cs \
        Assets/UI/FlapPad/FlapPad.uxml Assets/UI/FlapPad/FlapPad.uss
git status --short
git commit -m "feat(flappy): 잡히면 입력면을 닫고 카메라를 남은 꼴찌에게 넘긴다"
```

`git status --short`에서 로컬 픽스처 넷(`Assets/Art`, `Room.unity`, `Jua-Regular SDF.asset`,
`ProjectSettings/*`)이 스테이지되지 않았는지 반드시 확인한다.

---

## Task 6: 2인 라이브 검증과 푸시

**Files:** (코드 변경 없음 — 관측과 푸시)

- [ ] **Step 1: 여섯 레포를 순서대로 푸시한다**

**의존 순서를 지킨다**: infrastructure → MasterData 둘 → LOP-Shared → 서버·클라.
게임서버 CI는 형제 패키지를 러너 클론의 `main`으로 맞추므로, **Shared가 main에 올라가기 전에는
서버 콘텐츠 빌드가 새 코드를 못 본다.**

레포마다 (`&&`로 잇지 말고 한 줄씩):

```bash
cd <repo>
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/flappy-chaser
git push origin main
git branch -d feature/flappy-chaser
```

리베이스가 거부되면 거기서 멈추고 원인을 확인한다. 푸시가 거절되면 **force를 쓰지 말고**
다시 `fetch`부터 반복한다.

- [ ] **Step 2: 두 에디터를 띄운다**

서버 에디터를 열고(`ConfigureRoomComponent`의 명단이 2인인지 확인), 클라 에디터 + MPPM 클론을 띄운다.
MPPM 클론은 재시작할 때마다 새 익명 계정을 받으므로, 서버 콘솔의 "명단에 없는 참가자: \<uuid\>"가
보이면 그 값을 `ConfigureRoomComponent`의 두 번째 자리에 붙여넣는다(이 파일은 커밋하지 않는다).

- [ ] **Step 3: 오토파일럿을 켜고 한 판 돌린다**

`LOP ▸ Debug ▸ Auto Flap`을 양쪽 다 켠다.

- [ ] **Step 4: 다섯 가지를 확인한다**

1. **벽이 보이고 다가온다** — 붉은 판이 화면 왼쪽에서 들어온다.
2. **봇이 75초쯤 잡힌다** — 서버 콘솔의 `[Chaser] ... 탈락 — tick=... 벽=...m 새=...m`.
   벽 좌표가 곡선과 맞는지 본다: 75초면 `tick − 출발틱 ≈ 3750`, 벽 ≈ 632m.
3. **늦게 잡힌 쪽이 더 높은 등수** — 결과 화면의 등수를 `[Chaser]` 로그 순서와 대조한다.
4. **전원이 잡힌 판이 바로 끝난다** — 마지막 `[Chaser]` 로그와 결과 화면 사이가 90초 상한이
   아니라 즉시인지.
5. **잡힌 뒤 화면** — 입력면과 대시 버튼이 사라지고 "탈락"이 뜨고, 카메라가 남은 새로 넘어가고,
   그 화면에 벽이 보인다.

- [ ] **Step 5: 클라와 서버의 벽 위치가 같은지 대조한다**

클라 `FlappyChaserView.X`와 서버 `[Chaser]` 로그의 벽 좌표를, **같은 틱**에서 비교한다.
클라 쪽 값은 잠깐 `Debug.Log($"[ChaserView] tick={...} x={X:F2}")`를 넣어 찍고 확인 뒤 지운다.

기대: 내 새를 보고 있는 동안은 거의 같다(내 예측 틱이 서버보다 lead만큼 앞서므로 그만큼 앞).
0.1초 lead × 10m/s ≈ 1m 이내면 정상이다.

- [ ] **Step 6: 결과를 로드맵에 적는다**

`docs/ROADMAP.md`의 "이번 세션에 닫힌 것" 표에 한 줄 추가하고, 열린 항목
"Flappy에서 대시가 등수를 얼마나 가르는지 모른다"를 관측 결과에 맞게 갱신한다.
관측한 **수치**(잡힌 틱, 벽 좌표, 등수, 클·서 차이)를 함께 남긴다 — 나중에 곡선을 튜닝할 때
비교 기준이 된다.

`docs/` 변경은 별도 브랜치(`docs/roadmap-flappy-chaser`)에서 하고 같은 절차로 푸시한다.

---

## 마지막 확인

- [ ] 여섯 레포 전부 `git rev-list --left-right --count origin/main...HEAD`가 `0 0`
- [ ] 클라·서버 로컬 픽스처가 커밋되지 않았다
- [ ] 새 proto 메시지를 만들지 않았다(추격자는 와이어를 타지 않는다)
