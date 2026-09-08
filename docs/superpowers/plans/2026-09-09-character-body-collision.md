# 캐릭터끼리 부딪힌다 — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 스카이다이브에서 사람 몸이 서로 통과하지 않고, 부딪힌 속도를 운동량 보존으로 주고받으며, 남의 머리 위에 세게 떨어지면 지면과 똑같이 죽는다.

**Architecture:** 이미 있는 `BodyOverlap`(순수 산수 겹침) + `BodyCollisionSystem`(짝 순회·결정론)을 그대로 쓰고, 세로에만 묶여 있던 속도 교환을 3D 일반형 `ContactImpulse`로 푼다. `SkydiveWorld`가 **이동 뒤에** 한 번 부르고, 접지·착지 충격 판단을 이동에서 떼어 "맵 OR 사람"을 한곳에서 정한다.

**Tech Stack:** C# / Unity 6000.3.16f1 / NUnit EditMode / VContainer / Luban MasterData

**Spec:** `docs/superpowers/specs/2026-09-09-character-body-collision-design.md`

## Global Constraints

- **레포는 6개가 걸린다**: `infrastructure`(마스터데이터 원본) · `LOP-MasterData-Client` · `LOP-MasterData-Server` · `LOP-Shared` · `LOP-Client` · `LOP-Server`. 레포마다 자기 피처 브랜치에서 작업한다.
- **main에 직접 커밋 금지.** 각 레포에서 `feature/character-body-collision` 브랜치를 판다.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고, 커밋 전 `git status --short`와 `git diff --cached --name-only`로 스테이지된 것이 의도한 파일뿐인지 확인한다. Unity 레포에는 의도적으로 커밋하지 않는 로컬 픽스처가 늘 dirty하게 있다(`Assets/Art` 포인터, 폰트, `ProjectSettings/*`, URP 프로파일 등).
- **`.meta` 파일은 함께 커밋한다.** 새 `.cs`를 만들면 Unity가 만든 `.meta`도 같이 스테이지한다. 직접 손으로 쓰지 않는다.
- **클·서가 같은 값을 써야 한다.** 반발계수·축 마스크·몸 규격이 한쪽만 달라지면 예측이 서버와 갈려 러버밴딩이 난다.
- **World 타입은 항상 풀 네임스페이스로 한정한다** (`GameFramework.World.Entity`, `GameFramework.World.Transform` 등). `using GameFramework.World;`를 추가하지 않는다 — `UnityEngine.Component`와 이름이 겹친다.
- **주석은 최소로, 일상어로, 왜만.** 코드로 자명한 것은 적지 않는다.
- **테스트는 반드시 빨강을 먼저 본다.** 이 영역은 "검사하는 척만 하는 테스트" 사고가 두 번 났다.
- 확정된 값: 반발계수 `e = 0.35`, 몸 반지름 `0.4`, 몸 높이 `1.8`, 착지 치명 속도 `15`, 허용 겹침 `Slop = 0.01`, `RestingSpeed = 1.5`.
- **테스트는 떠 있는 에디터에 붙여서 돌린다**: `unity cmd run_tests --project-path <abs> --timeout 800 --mode EditMode [--filter <이름>]`. `unity test`(batchmode)는 에디터가 켜져 있으면 못 쓴다.
  - **에디터가 Play Mode면 테스트가 영원히 `running`에 머문다.** 돌리기 전에 확인하고, 켜져 있으면 끈다:
    `unity cmd eval --project-path <abs> 'return UnityEditor.EditorApplication.isPlaying;'` →
    `unity cmd eval --project-path <abs> 'UnityEditor.EditorApplication.isPlaying = false; return "stop";'`
  - 결과에서 `"Summary":{"Total":N,"Passed":N,"Failed":0,...}`를 확인한다. **기준선은 1164/1164**(Task 1 시점).
- **생성자에 인자를 더할 때는 풀 한정 형태도 같이 찾는다** — `new Foo(`만 grep하면 `new LOP.Foo(`로 쓴 호출부를 놓친다. Task 1에서 실제로 한 곳(`SkydiveLandingTests.cs`)이 그렇게 빠졌다.

## File Structure

| 파일 | 책임 | Task |
|---|---|---|
| `infrastructure/table/Datas/#SkydiveConfig.xlsx` | 반발계수의 진실원본 | 1 |
| `LOP-MasterData-{Client,Server}/Runtime.Generated/.../SkydiveConfig.cs` + `.bytes` | Luban 생성물 | 1 |
| `LOP-Shared/Runtime/Scripts/Game/SkydiveConfig.cs` | 시뮬이 읽는 설정 구조체 | 1 |
| `LOP-{Client,Server}/Assets/Scripts/Game/SkydiveConfigProvider.cs` | 마스터데이터 → 설정 | 1 |
| `LOP-Shared/Runtime/Scripts/Game/ContactImpulse.cs` | **3D 접촉 충격량 커널**(순수 함수) | 2 |
| `LOP-Shared/Runtime/Scripts/Game/VerticalBounce.cs` | 위 커널의 얇은 래퍼(프로토타입 호환) | 2 |
| `LOP-Shared/Runtime/Scripts/Game/BodyCollisionSystem.cs` | 축 마스크 + 아래쪽 접촉 반환 | 3 |
| `LOP-Shared/Runtime/Scripts/Game/SkydiveWorld.cs` | 접지·충격 분리(4) → 몸싸움 호출(5) | 4, 5 |
| `LOP-{Client,Server}/Assets/Scripts/Game/SkydiveLifetimeScope.cs` | `BodyCollisionSystem` 등록 | 5 |
| `LOP-Client/Assets/Scripts/Netcode/ReconciliationStats.cs` | 근접/비근접 보정량 분리 기록 | 7 |
| `LOP-Client/Assets/Scripts/UI/DebugHud*` | 그 값 표시 | 7 |
| `LOP-Shared/Tests/EditMode/SkydiveRemotePredictionErrorTests.cs` | 예측 오차 측정(스펙 §7①) | 6 |

---

### Task 1: 마스터데이터에 `restitution` 열을 더한다

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/infrastructure/table/Datas/#SkydiveConfig.xlsx`
- Generated (커밋 대상): `LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/SkydiveConfig.cs` + `.bytes`, 서버 패키지의 대응 파일
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveConfig.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveConfigProvider.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveConfigProvider.cs`
- Modify (생성자 인자 추가에 따라): Shared `Tests/EditMode/{SkydiveMoveSystemTests,SkydiveWorldTests,StaminaSystemTests,WindDriftSystemTests}.cs`, 클라 `Assets/Tests/Editor/SkydiveCorrectionFixture.cs`, 서버 `Assets/Tests/Editor/{SkydiveDoorSystemTests,SkydiveLandingSystemTests,SkydiveLaserSystemTests}.cs`

**Interfaces:**
- Produces: `LOP.MasterData.SkydiveConfig.Restitution` (float, 생성물) 과 `LOP.SkydiveConfig.Restitution` (float, 시뮬용). Task 5가 후자를 읽는다.

- [ ] **Step 1: 엑셀에 열을 더한다**

Luban Excel-embedded 형식은 헤더가 네 줄(`##var` / `##type` / `##group` / `##`)이고 5행부터 데이터다. 현재 29열(AC = `landing_lethal_speed`)까지 차 있으므로 30열에 넣는다. `##group`을 비우면 클·서 양쪽에 생성된다.

파이썬 스크립트를 임시 파일로 저장해 실행한다(셸 heredoc 중첩을 피한다).

```python
# add_column.py
import openpyxl
wb = openpyxl.load_workbook("#SkydiveConfig.xlsx")
ws = wb["Sheet"]
assert ws.cell(1, 1).value == "##var", "헤더 모양이 달라졌다 — 멈춰라"
assert ws.cell(1, ws.max_column).value == "landing_lethal_speed", "마지막 열이 다르다 — 멈춰라"
assert ws.cell(5, 2).value == 1, "데이터 행 위치가 달라졌다 — 멈춰라"
col = ws.max_column + 1
ws.cell(1, col, "restitution")
ws.cell(2, col, "float")
ws.cell(4, col, "restitution")
ws.cell(5, col, 0.35)
wb.save("#SkydiveConfig.xlsx")
print("추가한 열:", col)
```

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table/Datas"
python add_column.py
```

기대 출력: `추가한 열: 30`

- [ ] **Step 2: 생성기를 돌린다**

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table"
cmd.exe /c gen.bat
```

기대: 오류 없이 끝나고 네 파일이 갱신된다(클·서 각각 `.cs` + `.bytes`).

> `gen.bat:20`에 이스케이프 누락이 있어 실행마다 `table/MasterData-Server` 쓰레기 파일이 생긴다(알려진 것, 이번 범위 밖). 생겼으면 **커밋하지 말고 지운다.**

- [ ] **Step 3: 생성물에 필드가 생겼는지 확인한다**

```bash
grep -n "Restitution" "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/SkydiveConfig.cs"
grep -n "Restitution" "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/SkydiveConfig.cs"
```

기대: 양쪽에서 `public readonly float Restitution;` 과 `Restitution = _buf.ReadFloat();`이 보인다. 한쪽만 나오면 `##group`에 값이 들어간 것이니 Step 1로 돌아간다.

- [ ] **Step 4: `SkydiveConfig`에 필드를 더한다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveConfig.cs`

`LandingLethalSpeed` 선언 바로 아래에 추가:

```csharp
        /// <summary>
        /// 몸끼리 부딪혔을 때 얼마나 튕기나(0~1). 사람 몸은 잘 안 튕겨서 0.35다 —
        /// 맞부딪히면 날아가기보다 밀리며 엉킨다.
        /// </summary>
        public readonly float Restitution;
```

생성자 시그니처 마지막에 인자를 더하고 대입도 추가한다:

```csharp
            float landingLethalSpeed,
            float restitution)
```

```csharp
            LandingLethalSpeed = landingLethalSpeed;
            Restitution = restitution;
```

- [ ] **Step 5: 두 provider를 고친다**

`LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveConfigProvider.cs` 와 서버의 같은 파일에서, `new SkydiveConfig(...)` 마지막 인자를 바꾼다:

```csharp
                r.LandingLethalSpeed,
                r.Restitution);
```

- [ ] **Step 6: 나머지 8개 호출부를 고친다**

`new SkydiveConfig(` 를 부르는 곳이 전부 10곳이다. Step 5에서 2곳을 했으므로 8곳이 남는다. 전부 테스트 픽스처이며, 마지막 인자로 `0.35f`를 더한다.

```bash
cd "C:/Users/re5na/workspace/LOP"
grep -rn "new SkydiveConfig(" --include=*.cs \
  LeagueOfPhysical-Shared/Tests LeagueOfPhysical-Client/Assets LeagueOfPhysical-Server/Assets
```

기대 목록:
- `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveMoveSystemTests.cs`
- `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveWorldTests.cs`
- `LeagueOfPhysical-Shared/Tests/EditMode/StaminaSystemTests.cs`
- `LeagueOfPhysical-Shared/Tests/EditMode/WindDriftSystemTests.cs`
- `LeagueOfPhysical-Client/Assets/Tests/Editor/SkydiveCorrectionFixture.cs`
- `LeagueOfPhysical-Server/Assets/Tests/Editor/SkydiveDoorSystemTests.cs`
- `LeagueOfPhysical-Server/Assets/Tests/Editor/SkydiveLandingSystemTests.cs`
- `LeagueOfPhysical-Server/Assets/Tests/Editor/SkydiveLaserSystemTests.cs`

각 파일에서 마지막 인자(`landingLethalSpeed` 값) 뒤에 `, 0.35f`를 붙인다. 예:

```csharp
                landingLethalSpeed: 15f,
                restitution: 0.35f);
```

(이름 있는 인자를 안 쓰는 픽스처라면 값만 더한다.)

- [ ] **Step 7: 클라 EditMode 테스트 전체를 돌려 초록을 확인한다**

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" --timeout 800 --mode EditMode
```

기대: 전부 통과. 이 단계는 새 동작이 아니라 **인자 추가가 아무것도 안 깼다**를 확인하는 것이다. 실패하면 빠뜨린 호출부가 있다.

- [ ] **Step 8: 6개 레포에 브랜치를 파고 커밋한다**

```bash
for r in infrastructure LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server \
         LeagueOfPhysical-Shared LeagueOfPhysical-Client LeagueOfPhysical-Server; do
  cd "C:/Users/re5na/workspace/LOP/$r"
  git fetch origin
  git checkout -b feature/character-body-collision
done
```

각 레포에서 **바꾼 파일만** 경로로 지정해 커밋한다. 예(Shared):

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git add Runtime/Scripts/Game/SkydiveConfig.cs Tests/EditMode/SkydiveMoveSystemTests.cs \
        Tests/EditMode/SkydiveWorldTests.cs Tests/EditMode/StaminaSystemTests.cs \
        Tests/EditMode/WindDriftSystemTests.cs
git status --short
git diff --cached --name-only
git commit -m "feat(skydive): 반발계수를 마스터데이터에서 읽는다"
```

---

### Task 2: 3D 접촉 충격량 커널 `ContactImpulse`

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ContactImpulse.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/VerticalBounce.cs`
- Create: `LeagueOfPhysical-Shared/Tests/EditMode/ContactImpulseTests.cs`

**Interfaces:**
- Produces: `public static Vector3 LOP.ContactImpulse.Resolve(Vector3 vSelf, Vector3 vOther, Vector3 normal, float restitution)` — `normal`은 self를 상대 밖으로 밀어내는 방향. Task 3이 이것을 부른다.
- Produces: `public const float LOP.ContactImpulse.RestingSpeed = 1.5f`
- Consumes: 없음 (순수 함수)

- [ ] **Step 1: 실패하는 테스트를 쓴다**

Create `LeagueOfPhysical-Shared/Tests/EditMode/ContactImpulseTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ContactImpulseTests
    {
        const float Tolerance = 1e-4f;
        const float E = 0.35f;

        [Test]
        public void 정면으로_다가오면_법선_방향_속도를_주고받는다()
        {
            //  A가 위에서 -10으로 내려와 정지한 B를 때린다. 법선은 A를 위로 민다.
            Vector3 vA = new Vector3(0f, -10f, 0f);
            Vector3 vB = Vector3.zero;
            Vector3 n = Vector3.up;

            Vector3 afterA = ContactImpulse.Resolve(vA, vB, n, E);
            Vector3 afterB = ContactImpulse.Resolve(vB, vA, -n, E);

            //  다가오는 속도 -10에 (1+e)/2 = 0.675를 곱해 6.75만큼 주고받는다.
            Assert.AreEqual(-3.25f, afterA.y, Tolerance);
            Assert.AreEqual(-6.75f, afterB.y, Tolerance);
        }

        [Test]
        public void 가로_법선이면_가로_속도가_바뀐다()
        {
            //  세로만 다루던 옛 커널이 못 하던 일 — 이 테스트가 이번 변경의 이유다.
            Vector3 vA = new Vector3(8f, 0f, 0f);
            Vector3 vB = Vector3.zero;
            Vector3 n = Vector3.left;   // A를 -x로 밀어낸다

            Vector3 afterA = ContactImpulse.Resolve(vA, vB, n, E);

            Assert.AreEqual(8f - 0.675f * 8f, afterA.x, Tolerance);
            Assert.AreEqual(0f, afterA.y, Tolerance);
        }

        [Test]
        public void 이미_멀어지는_중이면_건드리지_않는다()
        {
            Vector3 vA = new Vector3(0f, 5f, 0f);
            Vector3 after = ContactImpulse.Resolve(vA, Vector3.zero, Vector3.up, E);
            Assert.AreEqual(vA, after);
        }

        [Test]
        public void 아주_천천히_닿으면_안_튕긴다()
        {
            //  RestingSpeed 아래는 e를 0으로 본다 — 얹혀 있을 때 떠는 것을 막는다.
            float slow = ContactImpulse.RestingSpeed * 0.5f;
            Vector3 after = ContactImpulse.Resolve(new Vector3(0f, -slow, 0f), Vector3.zero, Vector3.up, E);

            //  e=0이므로 다가오던 속도의 절반만 지워진다.
            Assert.AreEqual(-slow * 0.5f, after.y, Tolerance);
        }

        [Test]
        public void 스치면_거의_안_바뀐다()
        {
            Vector3 v = new Vector3(0f, -10f, 0f);
            Vector3 straight = ContactImpulse.Resolve(v, Vector3.zero, Vector3.up, E);
            Vector3 glancing = ContactImpulse.Resolve(
                v, Vector3.zero, new Vector3(0.866f, 0.5f, 0f), E);   // 법선이 60도 기운다

            Assert.Less(Mathf.Abs(glancing.y - v.y), Mathf.Abs(straight.y - v.y));
        }

        [Test]
        public void 반발계수_1이면_등질량_정면에서_속도가_통째로_교환된다()
        {
            Vector3 vA = new Vector3(0f, -10f, 0f);
            Vector3 afterA = ContactImpulse.Resolve(vA, Vector3.zero, Vector3.up, 1f);
            Vector3 afterB = ContactImpulse.Resolve(Vector3.zero, vA, Vector3.down, 1f);

            Assert.AreEqual(0f, afterA.y, Tolerance);
            Assert.AreEqual(-10f, afterB.y, Tolerance);
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" --timeout 800 --mode EditMode --filter ContactImpulseTests
```

기대: 컴파일 실패 — `ContactImpulse`가 없다.

- [ ] **Step 3: 커널을 만든다**

Create `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ContactImpulse.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 몸 둘이 부딪혔을 때 주고받는 속도(질량은 서로 같다고 본다).
    ///
    /// <para>부딪힌 속도를 0으로 지우면 위에 있는 몸이 아래 몸을 발판처럼 밟고 서게 되고,
    /// 중력이 곧바로 다시 붙여서 매 프레임 재충돌한다. 서로 속도를 주고받아야 갈라진다.</para>
    ///
    /// <para>일반형 <c>j = -(1+e)·v_closing / (1/m₁ + 1/m₂)</c>에 <c>m₁ = m₂</c>를 넣어 정리하면
    /// 아래의 0.5가 남는다. 질량을 다르게 하려면 그 상수 자리를 연다.</para>
    /// </summary>
    public static class ContactImpulse
    {
        /// <summary>이보다 느리게 다가온 충돌은 튕기지 않는다 — 얹혀 있을 때 미세하게 떠는 걸 막는다.</summary>
        public const float RestingSpeed = 1.5f;

        /// <summary>
        /// 충돌 후 self의 속도. <paramref name="normal"/>은 self를 상대 밖으로 밀어내는 방향이다.
        /// 부르는 쪽이 축을 제한하고 싶으면 <b>넣기 전에</b> 세 인자 모두에 마스크를 씌운다 —
        /// 결과에만 씌우면 다가오는 속도가 전 축으로 계산돼 답이 달라진다.
        /// </summary>
        public static Vector3 Resolve(Vector3 vSelf, Vector3 vOther, Vector3 normal, float restitution)
        {
            float closing = Vector3.Dot(vSelf - vOther, normal);
            if (closing >= 0f)
            {
                return vSelf;   // 이미 멀어지는 중이면 건드리지 않는다
            }

            float e = -closing < RestingSpeed ? 0f : restitution;
            return vSelf - (1f + e) * closing * 0.5f * normal;
        }
    }
}
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" --timeout 800 --mode EditMode --filter ContactImpulseTests
```

기대: 6개 전부 PASS.

- [ ] **Step 5: 이빨을 확인한다 (일부러 깨뜨린다)**

`ContactImpulse.cs`에서 `0.5f`를 `1.0f`로 바꾸고 테스트를 다시 돌린다.

기대: `정면으로_다가오면_...`, `반발계수_1이면_...` 이 **실패**한다. 확인 후 되돌린다.

또 한 번: `if (closing >= 0f)` 줄을 지우고 돌린다. 기대: `이미_멀어지는_중이면_...` 이 실패한다. 확인 후 되돌린다.

- [ ] **Step 6: `VerticalBounce`를 래퍼로 줄인다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/VerticalBounce.cs`의 `ResolveVy` 본문을 커널 호출로 바꾼다. **클래스와 `RestingSpeed`는 남긴다** — 클라 프로토타입 `FlappyRaceSlice/FlappyBird.cs`가 직접 부른다.

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// <see cref="ContactImpulse"/>의 세로 전용 입구. 전진 속도가 상수로 고정돼 손댈 수 없던
    /// 게임(Flappy Race)과 그 프로토타입이 이 모양으로 부른다.
    ///
    /// <para>세로만 넣는 것이 곧 "세로 축 마스크"다 — 다가오는 속도도 세로로만 계산된다.</para>
    /// </summary>
    public static class VerticalBounce
    {
        /// <inheritdoc cref="ContactImpulse.RestingSpeed"/>
        public const float RestingSpeed = ContactImpulse.RestingSpeed;

        /// <summary>
        /// 충돌 후 self의 세로 속도.
        /// <paramref name="normalY"/>는 self를 상대 밖으로 밀어내는 방향의 세로 성분(-1~1)이다.
        /// </summary>
        public static float ResolveVy(float vySelf, float vyOther, float normalY, float restitution)
        {
            return ContactImpulse.Resolve(
                new Vector3(0f, vySelf, 0f),
                new Vector3(0f, vyOther, 0f),
                new Vector3(0f, normalY, 0f),
                restitution).y;
        }
    }
}
```

- [ ] **Step 7: 옛 테스트가 그대로 초록인지 확인한다**

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" --timeout 800 --mode EditMode --filter VerticalBounceTests
```

기대: 전부 PASS. **이것이 일반형이 옛 식과 같음을 증명하는 회귀 검사다** — 손대지 않은 테스트가 새 구현 위에서 통과해야 한다.

- [ ] **Step 8: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git add Runtime/Scripts/Game/ContactImpulse.cs Runtime/Scripts/Game/ContactImpulse.cs.meta \
        Runtime/Scripts/Game/VerticalBounce.cs \
        Tests/EditMode/ContactImpulseTests.cs Tests/EditMode/ContactImpulseTests.cs.meta
git status --short
git commit -m "feat: 접촉 충격량을 3D로 푼다"
```

---

### Task 3: `BodyCollisionSystem`에 축 마스크와 아래쪽 접촉 반환

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/BodyCollisionSystem.cs`
- Modify: `LeagueOfPhysical-Shared/Tests/EditMode/BodyCollisionSystemTests.cs`

**Interfaces:**
- Consumes: `LOP.ContactImpulse.Resolve` (Task 2)
- Produces:
  - `new BodyCollisionSystem(float bodyRadius, float bodyHeight, float restitution, Vector3 axisMask)` — 기존 3인자 생성자는 `axisMask = Vector3.up`(세로만)로 두어 Flappy 호출부가 안 깨지게 한다.
  - `HashSet<string> Resolve(IReadOnlyList<GameFramework.World.Entity> bodies)` — **아래쪽으로 남에게 닿은 엔티티 id의 집합**을 돌려준다. Task 5가 이것을 읽는다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/BodyCollisionSystemTests.cs` 아래쪽에 추가한다. 기존 테스트는 **손대지 않는다** — 그것이 세로 마스크의 회귀 검사다.

```csharp
        //  Skydive용 — 전 축으로 밀리는 몸.
        static BodyCollisionSystem FreeAxisSystem()
            => new BodyCollisionSystem(bodyRadius: 0.4f, bodyHeight: 1.8f,
                                       restitution: 0.35f, axisMask: Vector3.one);

        static Entity Diver(string id, Vector3 position, Vector3 velocity) => Bird(id, position, velocity);

        [Test]
        public void 전_축이면_가로로도_밀린다()
        {
            //  같은 높이에서 옆으로 파고든 둘. 세로만 다루던 옛 동작에서는 x가 안 변했다.
            var left = Diver("diver-1", Vector3.zero, new Vector3(6f, 0f, 0f));
            var right = Diver("diver-2", new Vector3(0.5f, 0f, 0f), Vector3.zero);

            FreeAxisSystem().Resolve(new List<Entity> { left, right });

            Assert.Less(VelocityOf(left).x, 6f);
            Assert.Greater(VelocityOf(right).x, 0f);
        }

        [Test]
        public void 세로_마스크는_가로_속도를_남긴다()
        {
            var left = Diver("diver-1", Vector3.zero, new Vector3(6f, 0f, 0f));
            var right = Diver("diver-2", new Vector3(0.5f, 0f, 0f), Vector3.zero);

            new BodyCollisionSystem(0.4f, 1.8f, 0.35f, Vector3.up)
                .Resolve(new List<Entity> { left, right });

            Assert.AreEqual(6f, VelocityOf(left).x, Tolerance);
            Assert.AreEqual(0f, VelocityOf(right).x, Tolerance);
        }

        [Test]
        public void 위에_있는_쪽만_아래로_닿았다고_보고한다()
        {
            //  세로 간격 1.6은 일부러 고른 값이다. 캡슐 심 선분이 길이 1.0이라 간격이 1.0 이하면
            //  두 선분이 겹쳐 거리가 0이 되고, BodyOverlap이 기하 대신 "규칙으로" Vector3.down을
            //  돌려주는 예외 분기로 빠진다 — 그러면 이 테스트가 판별을 시험하지 않고 통과한다.
            //  진짜 세로 법선은 간격이 (1.0, 1.8)일 때 나온다(1.8 = 몸 높이 = 머리 위에 선 자세).
            var lower = Diver("diver-1", Vector3.zero, Vector3.zero);
            var upper = Diver("diver-2", new Vector3(0f, 1.6f, 0f), new Vector3(0f, -10f, 0f));

            var groundedIds = FreeAxisSystem().Resolve(new List<Entity> { lower, upper });

            Assert.IsTrue(groundedIds.Contains("diver-2"));
            Assert.IsFalse(groundedIds.Contains("diver-1"));
        }

        [Test]
        public void 옆으로만_부딪히면_아무도_아래로_닿지_않았다()
        {
            var left = Diver("diver-1", Vector3.zero, new Vector3(6f, 0f, 0f));
            var right = Diver("diver-2", new Vector3(0.5f, 0f, 0f), Vector3.zero);

            var groundedIds = FreeAxisSystem().Resolve(new List<Entity> { left, right });

            Assert.AreEqual(0, groundedIds.Count);
        }

        [Test]
        public void 짝을_넘기는_순서가_결과를_바꾸지_않는다()
        {
            var a1 = Diver("diver-1", Vector3.zero, new Vector3(0f, -4f, 0f));
            var b1 = Diver("diver-2", new Vector3(0.3f, 0.9f, 0f), new Vector3(0f, -12f, 0f));
            FreeAxisSystem().Resolve(new List<Entity> { a1, b1 });

            var a2 = Diver("diver-1", Vector3.zero, new Vector3(0f, -4f, 0f));
            var b2 = Diver("diver-2", new Vector3(0.3f, 0.9f, 0f), new Vector3(0f, -12f, 0f));
            FreeAxisSystem().Resolve(new List<Entity> { b2, a2 });

            Assert.AreEqual(VelocityOf(a1), VelocityOf(a2));
            Assert.AreEqual(VelocityOf(b1), VelocityOf(b2));
            Assert.AreEqual(PositionOf(a1), PositionOf(a2));
            Assert.AreEqual(PositionOf(b1), PositionOf(b2));
        }
```

파일 맨 위 `using`에 `System.Collections.Generic`이 이미 있다. 없으면 더한다.

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" --timeout 800 --mode EditMode --filter BodyCollisionSystemTests
```

기대: 컴파일 실패 — 4인자 생성자가 없고 `Resolve`가 값을 안 돌려준다.

- [ ] **Step 3: 구현한다**

`BodyCollisionSystem.cs`를 고친다.

필드와 생성자:

```csharp
        private readonly float bodyRadius;
        private readonly float bodyHeight;
        private readonly float restitution;
        private readonly Vector3 axisMask;

        //  아래로 남에게 닿은 몸들. 매 틱 도는 코드라 새로 만들지 않고 비워서 다시 쓴다.
        private readonly HashSet<string> groundedOnBody = new HashSet<string>();

        /// <summary>세로로만 밀리는 게임용(Flappy Race). 전진 속도가 상수라 가로를 손댈 수 없다.</summary>
        public BodyCollisionSystem(float bodyRadius, float bodyHeight, float restitution)
            : this(bodyRadius, bodyHeight, restitution, Vector3.up)
        {
        }

        /// <summary>
        /// <paramref name="axisMask"/>는 밀릴 수 있는 축(성분별 0 또는 1). 전 축이면
        /// <c>Vector3.one</c>. 제한은 결과가 아니라 <b>입력</b>에 걸린다 — 결과에만 걸면
        /// 다가오는 속도가 전 축으로 계산돼 답이 달라진다.
        /// </summary>
        public BodyCollisionSystem(float bodyRadius, float bodyHeight, float restitution, Vector3 axisMask)
        {
            this.bodyRadius = bodyRadius;
            this.bodyHeight = bodyHeight;
            this.restitution = restitution;
            this.axisMask = axisMask;
        }
```

`Resolve(IReadOnlyList<Entity>)`가 집합을 돌려주게 한다:

```csharp
        /// <summary>
        /// 넘겨받은 몸들을 둘씩 모두 맞대어 겹친 짝을 푼다.
        /// 부르는 쪽은 <b>모두의 속도가 정해진 뒤</b> 한 번만 부르고, 목록을 엔티티 id 순으로 세워
        /// 넘긴다 — 푸는 순서가 클·서에서 같아야 두 쪽이 같은 결과에 이른다.
        /// </summary>
        /// <returns>아래로 남에게 닿은 엔티티 id — 부르는 쪽이 접지로 쓴다.</returns>
        public HashSet<string> Resolve(IReadOnlyList<GameFramework.World.Entity> birds)
        {
            groundedOnBody.Clear();
            for (int i = 0; i < birds.Count; i++)
            {
                for (int j = i + 1; j < birds.Count; j++)
                {
                    ResolvePair(birds[i], birds[j]);
                }
            }
            return groundedOnBody;
        }
```

두 리스트 오버로드도 같은 모양으로 바꾼다(둘 다 `groundedOnBody.Clear()`로 시작해 `return groundedOnBody`).

`ResolvePair`의 속도 교환 부분을 커널 호출로 바꾸고, 아래쪽 접촉을 기록한다:

```csharp
            // 둘 다 부딪히기 *전* 속도를 보고 계산한다 — 한쪽을 먼저 고쳐 놓고 다른 쪽이 그 값을 보면
            // 짝을 어느 순서로 넘겼는지가 결과를 바꿔 클·서가 갈린다.
            Vector3 linearA = velocityA.Linear.ToUnity();
            Vector3 linearB = velocityB.Linear.ToUnity();
            velocityA.Linear = Exchange(linearA, linearB, pushDir).ToNumerics();
            velocityB.Linear = Exchange(linearB, linearA, -pushDir).ToNumerics();

            //  나를 위로 밀어내는 접촉 = 내가 상대 위에 얹혔다.
            if (pushDir.y > 0f) { groundedOnBody.Add(a.Id); }
            if (pushDir.y < 0f) { groundedOnBody.Add(b.Id); }
```

`Exchange` 헬퍼를 더한다 — 마스크를 **입력에** 씌우고, 허용된 축만 새 값으로 돌려준다:

```csharp
        // 제한된 축은 원래 값을 그대로 남긴다. 마스크를 입력에 씌우는 이유는 클래스 주석 참고.
        private Vector3 Exchange(Vector3 vSelf, Vector3 vOther, Vector3 normal)
        {
            Vector3 resolved = ContactImpulse.Resolve(
                Vector3.Scale(vSelf, axisMask),
                Vector3.Scale(vOther, axisMask),
                Vector3.Scale(normal, axisMask),
                restitution);
            return vSelf + Vector3.Scale(resolved - Vector3.Scale(vSelf, axisMask), axisMask);
        }
```

`ResolveOneSided`도 같은 헬퍼를 쓰고, `pushDir.y > 0f`면 `groundedOnBody.Add(mover.Id)`를 한다.

클래스 XML 주석의 *"다른 게임에 가져다 쓸 때 갈아끼울 곳 … 세로 성분만 오간다"* 문단을 지우고, 축 마스크 설명으로 바꾼다(그 문단이 가리키던 일을 이 태스크가 했다).

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" --timeout 800 --mode EditMode --filter BodyCollisionSystemTests
```

기대: 기존 테스트 + 새 테스트 전부 PASS. **기존 테스트가 하나라도 깨지면 세로 마스크가 옛 동작과 달라진 것**이니 멈추고 `Exchange`의 마스크 적용을 다시 본다.

- [ ] **Step 5: 이빨을 확인한다**

`Exchange`에서 마스크를 입력이 아니라 결과에만 씌우도록 바꾼다:

```csharp
            Vector3 resolved = ContactImpulse.Resolve(vSelf, vOther, normal, restitution);
            return vSelf + Vector3.Scale(resolved - vSelf, axisMask);
```

기대: `세로_마스크는_가로_속도를_남긴다`는 여전히 통과하지만 **기존 `부딪힌_세로_속도를_주고받는다`가 여전히 통과**할 수 있다(가로 속도가 0이라서). 그래서 확인은 이 케이스로 한다 — 새 테스트를 하나 임시로 넣어 가로 속도가 있는 세로 마스크 충돌에서 답이 달라지는 것을 본다:

```csharp
        [Test]
        public void 임시_이빨확인_가로속도가_세로답을_오염시키지_않는다()
        {
            var lower = Diver("d1", Vector3.zero, new Vector3(20f, 0f, 0f));
            var upper = Diver("d2", new Vector3(0f, 1.0f, 0f), new Vector3(0f, -10f, 0f));

            new BodyCollisionSystem(0.4f, 1.8f, 0.35f, Vector3.up)
                .Resolve(new List<Entity> { lower, upper });

            //  가로 속도 20은 세로 답에 영향을 주면 안 된다.
            Assert.AreEqual(-3.25f, VelocityOf(upper).y, Tolerance);
        }
```

이 테스트가 **깨진 구현에서 실패하고 옳은 구현에서 통과**하는 것을 확인한 뒤, 구현을 되돌리고 **이 테스트는 남긴다**(이름에서 `임시_이빨확인_`을 떼고 `가로속도가_세로답을_오염시키지_않는다`로 바꾼다).

- [ ] **Step 6: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git add Runtime/Scripts/Game/BodyCollisionSystem.cs Tests/EditMode/BodyCollisionSystemTests.cs
git status --short
git commit -m "feat: 몸싸움에 축 마스크와 아래쪽 접촉 보고를 더한다"
```

---

### Task 4: 접지·착지 충격을 이동에서 떼어낸다 (동작 변화 없음)

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveWorld.cs`
- Modify: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveWorldTests.cs`

**Interfaces:**
- Produces: `SkydiveWorld`의 private 단계 — `MoveBlockedByMap`이 더 이상 `GroundState`/`LandingImpact`를 쓰지 않고, 새 `SettleGroundAndImpact`가 유일한 writer가 된다. Task 5가 이 단계에 몸 접지를 얹는다.

> 이 태스크는 **동작을 바꾸지 않는다.** 기존 테스트가 전부 초록으로 남아야 성공이다.

- [ ] **Step 1: 지금 동작을 박는 테스트를 먼저 확인한다**

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" --timeout 800 --mode EditMode --filter SkydiveWorldTests
```

기대: 전부 PASS. 이 목록과 개수를 적어 둔다 — Step 4에서 **같은 개수**가 나와야 한다.

- [ ] **Step 2: 이동 전 상태를 담을 자리를 만든다**

`SkydiveWorld.cs` 필드에 추가:

```csharp
        //  이동이 속도와 접지를 덮어쓰기 전에 찍어 두는 값. 착지 판정이 "직전에 안 닿았는데
        //  지금 닿았나"를 물어야 해서 필요하다.
        private readonly Dictionary<string, (float downward, bool grounded)> _beforeMove
            = new Dictionary<string, (float, bool)>();
```

- [ ] **Step 3: `MoveBlockedByMap`에서 접지·충격 쓰기를 걷어내고 새 단계를 만든다**

`MoveBlockedByMap`을 이렇게 줄인다(접지·충격 관련 줄을 전부 뺀다):

```csharp
        private void MoveBlockedByMap(GameFramework.World.Entity entity, float deltaTime)
        {
            var transform = entity.Get<GameFramework.World.Transform>();
            var velocity = entity.Get<GameFramework.World.Velocity>();
            if (transform == null || velocity == null)
            {
                return;
            }

            //  떨어지는 몸은 턱을 오를 일이 없다. 0을 주면 막혔을 때의 추가 sweep 3발도 안 쏜다.
            var result = KinematicMover.Move(new KinematicMoveInput(
                transform.Position.ToUnity(), velocity.Linear.ToUnity(),
                _config.BodyRadius, _config.BodyHeight, deltaTime,
                _layerMask, stepOffset: 0f), _collisionQuery);

            transform.Position = result.position.ToNumerics();
            // 막힌 축의 속도도 같이 지운다 — 안 지우면 다음 틱 수렴이 "막힌 적 없다는 듯"
            // 옛 속도 위에 계속 쌓인다(KinematicMoveSystem과 같은 관례).
            velocity.Linear = result.velocity.ToNumerics();

            _mapGrounded[entity.Id] = result.grounded;
        }
```

`_mapGrounded` 필드를 더한다:

```csharp
        //  이번 틱에 맵에 닿았나. 사람에 닿았나와 합쳐 최종 접지를 정한다.
        private readonly Dictionary<string, bool> _mapGrounded = new Dictionary<string, bool>();
```

새 단계를 더한다:

```csharp
        //  닿는 대상이 맵과 사람 둘이라 판단을 이동 밖으로 뺐다. 접지의 유일한 writer다.
        private void SettleGroundAndImpact(GameFramework.World.Entity diver, bool groundedOnBody)
        {
            bool grounded = (_mapGrounded.TryGetValue(diver.Id, out bool onMap) && onMap) || groundedOnBody;

            var groundState = diver.Get<GameFramework.World.GroundState>();
            if (groundState != null)
            {
                groundState.IsGrounded = grounded;
            }

            var impact = diver.Get<LandingImpact>();
            if (impact == null || _beforeMove.TryGetValue(diver.Id, out var before) == false)
            {
                return;
            }
            //  "닿은 순간"만 남긴다. 서 있는 동안 값이 남아 있으면 다음 틱에 또 죽는다.
            impact.DownwardSpeed = (before.grounded == false && grounded && before.downward > 0f)
                ? before.downward
                : 0f;
        }
```

`Mutation`의 이동 루프 앞뒤를 이렇게 바꾼다:

```csharp
            //  이동이 속도와 접지를 덮기 전에 찍어 둔다.
            _beforeMove.Clear();
            _mapGrounded.Clear();
            for (int i = 0; i < _divers.Count; i++)
            {
                var diver = _divers[i];
                var velocity = diver.Get<GameFramework.World.Velocity>();
                var groundState = diver.Get<GameFramework.World.GroundState>();
                _beforeMove[diver.Id] = (
                    velocity != null ? -velocity.Linear.Y : 0f,   // 아래로 갈 때 양수
                    groundState != null && groundState.IsGrounded);
            }

            for (int i = 0; i < _divers.Count; i++)
            {
                MoveBlockedByMap(_divers[i], deltaTime);
            }

            for (int i = 0; i < _divers.Count; i++)
            {
                SettleGroundAndImpact(_divers[i], groundedOnBody: false);
            }
```

스태미나 루프는 그대로 뒤에 남는다.

- [ ] **Step 4: 기존 테스트가 전부 초록인지 확인한다**

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" --timeout 800 --mode EditMode
```

기대: Step 1과 **같은 개수**가 통과. 하나라도 줄거나 실패하면 리팩터가 동작을 바꾼 것이다 — 멈추고 되돌린다.

- [ ] **Step 5: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git add Runtime/Scripts/Game/SkydiveWorld.cs
git status --short
git commit -m "refactor(skydive): 접지와 착지 충격을 이동 밖 한곳으로 모은다"
```

---

### Task 5: 몸싸움을 틱에 넣고 배선한다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveWorld.cs`
- Modify: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveWorldTests.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveLifetimeScope.cs`

**Interfaces:**
- Consumes: `BodyCollisionSystem.Resolve(...) -> HashSet<string>` (Task 3), `SkydiveConfig.Restitution` (Task 1)
- Produces: `SkydiveWorld` 생성자에 `BodyCollisionSystem bodyCollisionSystem` 인자가 `DoorField doorField` **다음**, `SkydiveConfig config` **앞**에 들어간다. 호출부는 3곳(클 스코프, 서버 스코프, 클라 `SkydiveCorrectionFixture`)이다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/SkydiveWorldTests.cs`에 추가한다. 픽스처가 월드를 만드는 헬퍼를 쓰고 있으므로, 그 헬퍼에 `BodyCollisionSystem`을 넣어 준다(아래 Step 3에서 시그니처를 정한 뒤 맞춘다).

> **먼저 읽을 것 — 두 가지 함정이 있다.**
>
> **① 접촉 기하.** 몸 캡슐(r=0.4, h=1.8)의 심 선분은 길이 1.0이다. 두 몸의 **세로 간격이 1.0
> 이하**면 심 선분이 겹쳐 `BodyOverlap`의 거리가 0이 되고, 기하 대신 *규칙으로* `Vector3.down`을
> 돌려주는 예외 분기로 빠진다 — 아래쪽 접촉 판별을 시험하지 못한 채 통과한다. 진짜 세로 법선은
> 간격이 **(1.0, 1.8)** 구간일 때 나온다(1.8 = 몸 높이 = 머리 위에 선 정지 자세). 몸싸움은
> **이동 뒤**에 도므로 "틱이 끝난 뒤" 간격이 그 구간이어야 한다. 아래 두 상수는 계산으로 잡은
> 출발값이고, **가드 단언(`AssertVerticalContact`)이 빨강이면 픽스처를 돌려 다시 잡는다** —
> 그 단언은 예외 분기로 통과하는 것을 막는 장치이므로 지우지 않는다.
>
> **② 기존 픽스처를 따른다.** `SkydiveWorldTests`는 `World()`나 `AddDiver` 같은 헬퍼가 없다.
> 실제 모양은 `Diver(id)`(위치를 늘 `(0, 1000, 0)`으로 만든다) + `registry.Add(...)` +
> `World(registry, ...)` + `world.GameplayStartTick = 0`이고, 조회는 `HeightOf(registry, id)` ·
> `ImpactOf(registry, id)`다. 아래 코드는 그 모양에 맞춰 놨다 — **새 픽스처를 만들지 말고 이대로
> 얹는다.** 맵을 안 주면 `HalfSpaceQuery`가 면이 없는 하늘이라 맵 접지가 섞이지 않는다(원하는 바다).

```csharp
        //  "틱이 끝난 뒤" 세로 간격이 (1.0, 1.8)에 오게 하는 시작 간격. 떨어지는 속도가 다르면
        //  한 틱에 좁혀지는 양도 달라서 값이 둘이다 — 초속 60이면 1.2m, 6이면 0.12m를 간다.
        const float HardSpawnGap = 2.6f;   // 1.2m 좁혀져 ≈1.41
        const float SoftSpawnGap = 1.6f;   // 0.12m 좁혀져 ≈1.49

        static Entity DiverAt(string id, float x, float y)
        {
            var e = Diver(id);
            e.Get<GameFramework.World.Transform>().Position = new Vector3(x, y, 0f).ToNumerics();
            return e;
        }

        //  예외 분기(간격 ≤ 1.0에서 거리 0 → 규칙으로 정한 법선)로 통과하지 않았음을 못 박는다.
        static void AssertVerticalContact(EntityRegistry r, string lowerId, string upperId)
        {
            float gap = HeightOf(r, upperId) - HeightOf(r, lowerId);
            Assert.Greater(gap, 1.0f,
                $"세로 간격 {gap:F3}은 심 선분이 겹치는 구간이라 접촉 법선이 기하가 아니라 " +
                "규칙으로 정해진다 — 이 테스트는 판별을 시험하지 못한다");
        }

        [Test]
        public void 남의_머리에_세게_떨어지면_죽을_속도가_기록된다()
        {
            var registry = new EntityRegistry();
            registry.Add(DiverAt("a", 0f, 1000f));
            var upper = DiverAt("b", 0f, 1000f + HardSpawnGap);
            upper.Get<Velocity>().Linear = new Vector3(0f, -60f, 0f).ToNumerics();
            registry.Add(upper);

            var world = World(registry);   // 면이 없는 하늘 — 맵 접지가 섞이지 않는다
            world.GameplayStartTick = 0;

            world.Tick(0, 0.02f);

            AssertVerticalContact(registry, "a", "b");
            Assert.Greater(ImpactOf(registry, "b"), 15f);
        }

        [Test]
        public void 남의_머리에_살살_내려오면_서고_죽지_않는다()
        {
            var registry = new EntityRegistry();
            registry.Add(DiverAt("a", 0f, 1000f));
            var upper = DiverAt("b", 0f, 1000f + SoftSpawnGap);
            upper.Get<Velocity>().Linear = new Vector3(0f, -6f, 0f).ToNumerics();
            registry.Add(upper);

            var world = World(registry);
            world.GameplayStartTick = 0;

            world.Tick(0, 0.02f);

            AssertVerticalContact(registry, "a", "b");
            Assert.IsTrue(registry.Get("b").Get<GroundState>().IsGrounded);
            Assert.LessOrEqual(ImpactOf(registry, "b"), 15f);
        }

        [Test]
        public void 옆으로_부딪히면_접지도_충격도_없다()
        {
            var registry = new EntityRegistry();
            registry.Add(DiverAt("a", 0f, 1000f));
            registry.Add(DiverAt("b", 0.5f, 1000f));

            var world = World(registry);
            world.GameplayStartTick = 0;

            world.Tick(0, 0.02f);

            Assert.IsFalse(registry.Get("a").Get<GroundState>().IsGrounded);
            Assert.IsFalse(registry.Get("b").Get<GroundState>().IsGrounded);
            Assert.AreEqual(0f, ImpactOf(registry, "a"), 1e-4f);
            Assert.AreEqual(0f, ImpactOf(registry, "b"), 1e-4f);
        }

        [Test]
        public void 몸이_서로_통과하지_않는다()
        {
            var registry = new EntityRegistry();
            registry.Add(DiverAt("a", 0f, 1000f));
            registry.Add(DiverAt("b", 0.3f, 1000f));

            var world = World(registry);
            world.GameplayStartTick = 0;

            world.Tick(0, 0.02f);

            Vector3 pa = registry.Get("a").Get<GameFramework.World.Transform>().Position.ToUnity();
            Vector3 pb = registry.Get("b").Get<GameFramework.World.Transform>().Position.ToUnity();
            float horizontal = new Vector2(pb.x - pa.x, pb.z - pa.z).magnitude;
            Assert.GreaterOrEqual(horizontal, 0.79f);   // 지름 0.8 − 허용 겹침 0.01
        }
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" --timeout 800 --mode EditMode --filter SkydiveWorldTests
```

기대: 새 테스트 4개가 실패(몸싸움이 아직 없어 서로 통과한다).

- [ ] **Step 3: `SkydiveWorld`에 몸싸움을 넣는다**

필드와 생성자 인자를 더한다:

```csharp
        private readonly BodyCollisionSystem _bodyCollisionSystem;
```

생성자 파라미터를 `DoorField doorField,` 다음 줄에 넣고 대입한다:

```csharp
            BodyCollisionSystem bodyCollisionSystem,
```

```csharp
            _bodyCollisionSystem = bodyCollisionSystem;
```

`Mutation`의 이동 루프와 `SettleGroundAndImpact` 루프 **사이**에 몸싸움을 넣고, 결과를 접지에 넘긴다:

```csharp
            //  이동 뒤에 푼다 — 겹침은 이동이 만든다. 앞에 두면 이동이 새로 만든 겹침이 다음
            //  틱까지 남아, 초속 90m에서 한 틱(1.8m)만큼 몸을 뚫고 지나간 그림이 보인다.
            //  (표준 물리엔진은 속도 교환을 이동 앞에 두지만, 그 순서는 접촉이 여러 틱 지속되는
            //   것을 전제한다 — 우리 속도에서는 밀어내기가 겹침을 지워 충격이 아예 안 생긴다.)
            System.Collections.Generic.HashSet<string> groundedOnBody =
                _bodyCollisionSystem.Resolve(_divers);

            for (int i = 0; i < _divers.Count; i++)
            {
                SettleGroundAndImpact(_divers[i], groundedOnBody.Contains(_divers[i].Id));
            }
```

클래스 XML 주석의 한 틱 설명에 몸싸움 단계를 더한다.

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" --timeout 800 --mode EditMode --filter SkydiveWorldTests
```

기대: 새 4개 + 기존 전부 PASS.

- [ ] **Step 5: 이빨을 확인한다**

`_bodyCollisionSystem.Resolve(_divers)` 줄을 주석 처리하고 빈 집합을 쓰게 한 뒤 돌린다.

기대: 새 4개가 전부 실패한다. 확인 후 되돌린다.

- [ ] **Step 6: 클·서 스코프에 등록한다**

클라 `LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveLifetimeScope.cs` — `builder.Register<SkydiveWorld>(...)` **앞**에 넣는다:

```csharp
            //  전 축으로 밀린다 — 사람이 옆으로도 밀려나는 게임이다(Flappy는 세로만).
            //  값은 서버와 반드시 같아야 한다. 다르면 예측이 권위와 갈려 러버밴딩이 난다.
            builder.Register(c => new BodyCollisionSystem(
                c.Resolve<SkydiveConfig>().BodyRadius,
                c.Resolve<SkydiveConfig>().BodyHeight,
                c.Resolve<SkydiveConfig>().Restitution,
                Vector3.one), Lifetime.Singleton);
```

그리고 `new SkydiveWorld(...)` 인자에 `c.Resolve<BodyCollisionSystem>(),`을 `c.Resolve<DoorField>(),` 다음에 넣는다.

서버 `LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveLifetimeScope.cs`에도 같은 등록을 넣는다. 서버 파일은 `using UnityEngine;`이 없을 수 있으므로 `UnityEngine.Vector3.one`으로 쓴다.

- [ ] **Step 7: 클라 `SkydiveCorrectionFixture`를 고친다**

```bash
grep -n "new SkydiveWorld(" "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Tests/Editor/SkydiveCorrectionFixture.cs"
```

인자 목록에 `new BodyCollisionSystem(0.4f, 1.8f, 0.35f, Vector3.one),`를 `DoorField` 다음에 넣는다.

- [ ] **Step 8: 클·서 컴파일과 전체 테스트를 확인한다**

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" --timeout 800 --mode EditMode
```

기대: 전부 PASS.

서버는 에디터가 안 떠 있을 수 있다. 그럴 때는 컴파일만 확인한다:

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" --timeout 800 --mode EditMode
```

에디터가 없어 실패하면 그 사실을 보고에 남긴다 — **"돌렸다"고 적지 않는다.**

- [ ] **Step 9: 커밋 (3개 레포)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git add Runtime/Scripts/Game/SkydiveWorld.cs Tests/EditMode/SkydiveWorldTests.cs
git status --short
git commit -m "feat(skydive): 몸끼리 부딪히게 한다"

cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/Game/SkydiveLifetimeScope.cs Assets/Tests/Editor/SkydiveCorrectionFixture.cs
git status --short
git commit -m "feat(skydive): 몸싸움을 클라 시뮬에 등록한다"

cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/Game/SkydiveLifetimeScope.cs
git status --short
git commit -m "feat(skydive): 몸싸움을 서버 시뮬에 등록한다"
```

---

### Task 6: 남 예측 오차를 재는 테스트 (스펙 §7①)

**Files:**
- Create: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveRemotePredictionErrorTests.cs`

**Interfaces:**
- Consumes: `SkydiveWorld`(Task 5 시그니처), `SkydiveConfig`(Task 1)
- Produces: 없음 (측정 전용)

**왜 이 테스트가 필요한가:** 이 설계 전체가 *"남이 하던 자세를 계속한다고 두고 굴려도 된다"* 는 가정 위에 서 있다. Flappy는 같은 자리에서 실패했고(탭은 예측 불가), 스카이다이브는 입력이 지속되는 값이라 성립한다는 것이 **근거이지 결론이 아니다.** 숫자로 남긴다.

- [ ] **Step 1: 테스트를 쓴다**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 남을 "하던 자세를 계속한다"로 두고 굴렸을 때 9틱(≈180ms) 뒤에 얼마나 어긋나나.
    /// 자세를 바꾼 창이 이 설계의 진짜 위험이라 두 경우를 나눠 잰다.
    /// </summary>
    public class SkydiveRemotePredictionErrorTests
    {
        const int LeadTicks = 9;

        [Test]
        public void 자세를_안_바꾸면_9틱_예측이_거의_정확하다()
        {
            float error = PredictionError(changePostureMidway: false);
            Assert.Less(error, 0.01f, $"자세 유지 창에서 {error:F4}m 어긋났다 — 굴리는 규칙이 갈렸다는 뜻");
        }

        [Test]
        public void 자세를_바꾼_창의_오차를_기록한다()
        {
            float error = PredictionError(changePostureMidway: true);

            //  이 숫자가 곧 몸싸움 판정이 얼마나 어긋날 수 있는지다.
            //  0.3m는 스펙 §7의 합격선 — 넘으면 입력 지연(원 스펙 §4.3)을 검토한다.
            TestContext.WriteLine($"자세 변경 창 9틱 예측 오차: {error:F4} m");
            Assert.Less(error, 0.3f);
        }

        //  진실 = 자세 입력을 그대로 받은 월드. 예측 = 그 입력을 못 받고 마지막 자세로 굴린 월드.
        //  둘을 9틱 굴려 위치 차이를 잰다.
        static float PredictionError(bool changePostureMidway)
        {
            // 구현: SkydiveWorldTests의 월드 조립 헬퍼를 그대로 쓴다.
            // truth  — 다이버 하나를 만들고, changePostureMidway면 4틱째에 자세 축을 대자→다이브로 바꾼다.
            // predicted — 같은 초기 상태에서 자세 입력 없이 9틱 굴린다.
            // return (truth 위치 − predicted 위치).magnitude
            throw new System.NotImplementedException();
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" --timeout 800 --mode EditMode --filter SkydiveRemotePredictionErrorTests
```

기대: `NotImplementedException`으로 둘 다 실패.

- [ ] **Step 3: `PredictionError`를 구현한다**

`SkydiveWorldTests`가 월드를 만드는 방식을 그대로 따른다(같은 `SkydiveConfig` 픽스처, 같은 `HalfSpaceQuery`/모션 브리지 스텁). 두 월드를 각각 만들고, 다이버를 같은 위치·같은 자세로 넣고, 9틱을 `Tick`으로 굴린 뒤 위치 차이를 돌려준다.

`changePostureMidway`가 참이면 truth 쪽만 4틱째에 `Posture` 축 목표를 바꾼다(`ApplyPostureInput`이 읽는 컴포넌트를 기존 테스트가 어떻게 세팅하는지 그대로 따른다).

> **주의**: 스텁 `HalfSpaceQuery.Raycast`가 항상 `CollisionHit.None`을 돌려주던 전례가 있다. 이 테스트가 **0.0000m**를 내면 두 월드가 아예 안 움직인 것일 수 있으니, 9틱 뒤 위치가 시작 위치와 다른지도 함께 단언한다.

```csharp
            Assert.AreNotEqual(startPosition, truthPosition, "월드가 움직이지 않았다 — 스텁이 막고 있다");
```

- [ ] **Step 4: 테스트가 통과하는지 확인하고 숫자를 기록한다**

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" --timeout 800 --mode EditMode --filter SkydiveRemotePredictionErrorTests
```

기대: 둘 다 PASS. **`TestContext.WriteLine`이 출력한 오차 값을 보고에 적는다** — 이 숫자가 Task 8의 판정 근거다.

- [ ] **Step 5: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git add Tests/EditMode/SkydiveRemotePredictionErrorTests.cs Tests/EditMode/SkydiveRemotePredictionErrorTests.cs.meta
git status --short
git commit -m "test(skydive): 남 예측 오차를 자세 변경 유무로 나눠 잰다"
```

---

### Task 7: 근접 구간의 보정량을 따로 잰다 (스펙 §7②)

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/ReconciliationStats.cs`
- Modify: 그 값을 `Record`하는 호출부 (`grep -rn "reconciliationStats" LeagueOfPhysical-Client/Assets/Scripts` 로 찾는다)
- Modify: `DebugHud` (표시)
- Create: `LeagueOfPhysical-Client/Assets/Tests/Editor/ReconciliationStatsTests.cs`

**Interfaces:**
- Produces: `ReconciliationStats.Record(float distance, bool nearOther)` — 기존 1인자 `Record(float)`는 `nearOther: false`로 두어 다른 게임 호출부가 안 깨지게 한다.
- Produces: `ReconciliationStats.NearMax` / `NearAverage` / `FarMax` / `FarAverage`

**왜:** *"옆에 사람이 있을 때 보정량이 늘어나는가"* 가 몸싸움이 예측을 망치는지 가리는 지표다. Flappy가 이 지표로 원인을 짚었다(보정의 80%가 옆에 새가 있을 때).

- [ ] **Step 1: 실패하는 테스트를 쓴다**

Create `LeagueOfPhysical-Client/Assets/Tests/Editor/ReconciliationStatsTests.cs`:

```csharp
using NUnit.Framework;

namespace LOP.Tests
{
    public class ReconciliationStatsTests
    {
        [Test]
        public void 근접과_비근접을_따로_센다()
        {
            var stats = new ReconciliationStats();
            stats.Record(0.05f, nearOther: false);
            stats.Record(0.40f, nearOther: true);
            stats.Record(0.02f, nearOther: false);

            Assert.AreEqual(0.40f, stats.NearMax, 1e-4f);
            Assert.AreEqual(0.05f, stats.FarMax, 1e-4f);
            Assert.AreEqual(0.40f, stats.Max, 1e-4f);   // 전체 Max는 그대로 둘을 합친 값
        }

        [Test]
        public void 옛_한_인자_호출은_비근접으로_센다()
        {
            var stats = new ReconciliationStats();
            stats.Record(0.30f);

            Assert.AreEqual(0.30f, stats.FarMax, 1e-4f);
            Assert.AreEqual(0f, stats.NearMax, 1e-4f);
        }

        [Test]
        public void 리셋하면_둘_다_지워진다()
        {
            var stats = new ReconciliationStats();
            stats.Record(0.40f, nearOther: true);
            stats.Record(0.10f, nearOther: false);
            stats.Reset();

            Assert.AreEqual(0f, stats.NearMax, 1e-4f);
            Assert.AreEqual(0f, stats.FarMax, 1e-4f);
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" --timeout 800 --mode EditMode --filter ReconciliationStatsTests
```

기대: 컴파일 실패 — 2인자 `Record`와 새 프로퍼티가 없다.

- [ ] **Step 3: `ReconciliationStats`를 고친다**

기존 `Last`/`Max`/`Average`/`CorrectionCount`는 **그대로 두고**, 근접/비근접 두 벌을 더한다.

```csharp
        /// <summary>옆에 남의 몸이 있던 순간의 보정량. 몸싸움이 예측을 망치는지 가리는 지표다.</summary>
        public float NearMax { get; private set; }
        public float NearAverage { get; private set; }
        public float FarMax { get; private set; }
        public float FarAverage { get; private set; }
```

`Record(float distance)`는 `Record(distance, nearOther: false)`를 부르게 하고, 2인자 쪽이 실제 기록을 한다. 근접/비근접 각각 별도 창(`Queue<float>`)과 합계를 둔다. `Reset()`이 전부 지운다.

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" --timeout 800 --mode EditMode --filter ReconciliationStatsTests
```

기대: 3개 PASS.

- [ ] **Step 5: 보정 호출부가 근접 여부를 넘기게 한다**

`Record(`를 부르는 곳을 찾는다:

```bash
grep -rn "\.Record(" "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts" | grep -i recon
```

그 자리에서 "내 캐릭터 반경 3m 안에 다른 캐릭터가 있나"를 계산해 넘긴다. 3m는 몸 지름(0.8m)의 약 4배 — 부딪힐 만한 거리다.

```csharp
            //  몸싸움이 보정을 키우는지 가리려면 "옆에 사람이 있었나"를 같이 기록해야 한다.
            //  3m는 몸 지름 0.8m의 약 4배 — 이 안에 있으면 다음 몇 틱 안에 부딪힐 수 있다.
            const float NearRadius = 3f;
```

`EntityRegistry`를 훑어 `EntityKind.Character`이고 자기 자신이 아닌 엔티티 중 거리 `< NearRadius`가 하나라도 있으면 `true`.

- [ ] **Step 6: DebugHud에 한 줄을 더한다**

`DebugHud`가 이미 `Recon last/avg/max`를 그리고 있다. 그 아래에 한 줄을 더한다:

```
Recon near/far max: 0.42 / 0.06
```

- [ ] **Step 7: 전체 테스트를 돌린다**

```bash
unity cmd run_tests --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" --timeout 800 --mode EditMode
```

기대: 전부 PASS.

- [ ] **Step 8: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/Netcode/ReconciliationStats.cs Assets/Tests/Editor/ReconciliationStatsTests.cs \
        Assets/Tests/Editor/ReconciliationStatsTests.cs.meta
# 그리고 Step 5·6에서 바꾼 파일들을 경로로 추가한다
git status --short
git commit -m "feat(netcode): 옆에 사람이 있을 때의 보정량을 따로 잰다"
```

---

### Task 8: 배포하고 두 클라로 확인·측정한다

**Files:** (코드 변경 없음)

- [ ] **Step 1: 6개 레포를 main에 머지하고 푸시한다**

**레포마다** `CLAUDE.md`의 푸시 규약을 그대로 밟는다. `&&`로 이어 붙이지 말고 한 줄씩 결과를 확인한다.

```bash
cd "C:/Users/re5na/workspace/LOP/<repo>"
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/character-body-collision
git push origin main
```

순서는 **의존이 아래로 흐르는 쪽부터**: `infrastructure` → MasterData 둘 → `LOP-Shared` → `LOP-Client` / `LOP-Server`.

> `git merge --ff-only`가 `index.lock` 때문에 조용히 실패한 전례가 있다. 각 단계 뒤 `git log -1 --oneline`으로 실제로 움직였는지 확인한다.

- [ ] **Step 2: 게임서버를 배포한다**

`gameserver-deploy` 워크플로를 `environment=local`로 돌린다. 이미지 태그는 **서버 레포 SHA**다 — 서버 레포에 커밋이 없으면 태그가 안 움직인다(이번엔 Task 5에서 서버 스코프를 고쳤으므로 움직인다).

배포 후 확인:

```bash
kubectl get configmap -n <ns> | grep game-server-config
kubectl get pods -n <ns>
```

- [ ] **Step 3: 두 클라를 띄워 동작을 확인한다**

메인 에디터 + MPPM 클론. 확인 항목:

| | 확인 |
|---|---|
| 1 | 두 사람이 서로 **통과하지 않는다** |
| 2 | 옆에서 부딪히면 **둘 다 밀린다**(한쪽만 밀리면 절반 밀기가 안 걸린 것) |
| 3 | 다이브로 남의 머리에 박으면 **박은 쪽이 죽고 체크포인트로 간다** |
| 4 | 활공으로 살살 내려앉으면 **남의 머리 위에 서고 스태미나가 찬다** |
| 5 | 문·구멍에서 둘이 만났을 때 **떨거나 끼지 않는다** (스펙 §11-1) |

- [ ] **Step 4: 보정량을 잰다**

**MPPM 클론에서** DebugHud의 `Recon near/far max`를 본다. 메인 에디터는 부하를 져서 잡음이 판정을 삼킨다.

| | 기준 |
|---|---|
| 합격 | `near max < 0.3 m` |
| 불합격 | 0.3 m 이상이 반복 → 원 스펙 §4.3 입력 지연 검토 |

`near`와 `far`를 **같은 판 안에서** 비교한다(판끼리 비교하면 조작 차이가 섞인다).

- [ ] **Step 5: 결과를 로드맵에 적는다**

`LeagueOfPhysical-Client/docs/ROADMAP.md`에 완료 항목을 더한다. 반드시 담을 것:

- Task 6이 출력한 **자세 변경 창 9틱 예측 오차** 숫자
- Task 8 Step 4의 **near/far max** 숫자
- 스펙 §11의 위험 셋 중 실제로 나온 것과 안 나온 것
- 스카이다이브 남은 일감 절(`🪂`)에서 "슬라이스 6 — 넷코드 측정"을 갱신한다

로드맵 변경은 **별도 브랜치**(`docs/roadmap-body-collision`)에서 하고 `--no-ff`로 머지한다.

---

## Self-Review

**스펙 커버리지**

| 스펙 절 | 태스크 |
|---|---|
| §2 반발계수 0.35 / 질량 동일 | 1 |
| §3 `ContactImpulse` 일반화 | 2 |
| §3.1 `VerticalBounce` 래퍼 유지 | 2 (Step 6·7) |
| §3.2 축 마스크를 입력에 건다 | 3 (Step 3·5) |
| §4.4 몸싸움을 이동 뒤에 | 5 (Step 3) |
| §5 접지·충격을 한곳으로 | 4 |
| §5.1 밟으면 죽고 / 서면 스태미나 | 5 (Step 1 테스트) |
| §6 배선 (10개 호출부 포함) | 1 (Step 6), 5 (Step 6·7) |
| §6.1 측정용 토글 | — **의도적으로 뺐다**: 스위치 대신 `near`/`far`를 같은 판 안에서 비교한다(Task 7). 판끼리 비교가 조작 차이에 오염되므로 이쪽이 더 낫다 |
| §7① 예측 오차 | 6 |
| §7② 라이브 A/B | 7 (계측), 8 (측정) |
| §8 테스트 | 2·3·5·6·7 각 Step |
| §11 위험 확인 | 8 (Step 3) |

**§4.3 터널링 검산**은 코드가 아니라 판단 근거라 태스크가 없다. Task 8 Step 3의 항목 1이 실측으로 대신한다.
