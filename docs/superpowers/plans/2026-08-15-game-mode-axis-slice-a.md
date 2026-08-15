# 게임 모드 축 슬라이스 A — 게임 씬을 데이터로 고른다

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 게임 씬 이름을 코드 상수에서 마스터데이터(`TbGameMode.ScenePath`)로 옮겨, `gameModeId`가 실제로 무언가를 결정하게 만든다.

**Architecture:** 클라·서버의 `LOPGameFactory`가 `match.rounds[0].gameModeId`로 `TbGameMode`를 조회해 게임 씬 경로를 얻어 로드한다. 조회 규칙(라운드 유무 검증, 경로 누락 검증)은 순수 static 클래스 `MatchSceneResolver`로 뽑아 EditMode에서 단위 테스트한다. 클라의 맵 경로 하드코딩도 서버와 동일한 마스터데이터 경로로 통일한다.

**Tech Stack:** Unity 6000.3 / C# / VContainer / Luban (마스터데이터) / NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-08-15-game-mode-axis-design.md`

## Global Constraints

- **작업 브랜치는 `feature/game-mode-axis`다.** 이미 체크아웃돼 있다. 브랜치를 새로 파거나 바꾸지 않는다.
- **워크트리를 만들지 않는다.** 유니티 레포에서는 금지다 — 현재 체크아웃 자리에서 작업한다.
- **⚠️ `git add -A` / `git add .` 절대 금지.** 이 저장소에는 **사용자의 미커밋 작업물 31개**가 살아 있다(FlappyRaceSlice 수정·삭제, ProjectSettings, Assets/Art 서브모듈 포인터 등). 커밋할 때는 **각 태스크가 명시한 경로만** `git add`한다. 커밋 직전 `git diff --cached --name-only`로 **별도 명령**으로 확인하고, 의도한 파일만 있는지 눈으로 본 뒤 커밋한다.
- **Unity `.meta` 파일은 반드시 함께 커밋한다.** 새 파일·폴더·에셋을 만들거나 옮기면 `.meta`도 같이 스테이징한다.
- **`GameFramework.World` 타입은 항상 풀 네임스페이스로 한정한다** (`GameFramework.World.Entity` 등). `using GameFramework.World;`를 추가하지 않는다 — `Component`가 `UnityEngine.Component`와 충돌한다.
- **주석은 최소로, 비자명한 *의도(왜)* 만.** 코드로 자명한 것은 주석 없이 둔다. 전문용어를 설명 없이 던지지 않는다.
- **클라와 서버를 같이 고친다.** 두 저장소에 같은 개념의 코드가 있으므로 한쪽만 고치면 깨진다.
- **이 슬라이스는 동작을 바꾸지 않는다.** 끝났을 때 게임은 지금과 똑같이 돌아야 한다. 화면에 보이는 변화가 없는 것이 정상이다.
- **이 슬라이스는 저장소 5개를 건드린다.** 각 저장소는 **자기 브랜치에서** 작업하고 **각자 커밋**한다. `main`에 직접 커밋하지 않는다 — LOP-Shared는 지금 `main`이므로 Task 1에서 `feature/game-mode-axis`를 판다.
- 저장소 경로:
  - 클라 `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client` (브랜치 `feature/game-mode-axis`, 이미 준비됨)
  - 서버 `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server`
  - 공유 `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared` (Task 1에서 브랜치 생성)
  - 마스터데이터 파이프라인 `/Users/insoobae/workspace/LOP/infrastructure/table`
  - 생성물 패키지 `/Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Client`, `-Server`

## 파일 구조

| 파일 | 책임 |
|---|---|
| **생성** Shared `Runtime/Scripts/Game/MatchSceneResolver.cs` | 라운드/씬경로 검증 규칙. 순수 C#. 클·서가 **같은 구체 코드**를 쓴다 |
| **생성** Shared `Tests/EditMode/MatchSceneResolverTests.cs` | 위 규칙의 단위 테스트 |
| **수정** `infrastructure/table/Datas/#GameMode.xlsx` | `scene_path` 컬럼 추가 + FlapWang 행 값 |
| **수정** 클라·서버 `Assets/Scripts/Game/LOPGameFactory.cs` | 씬 이름 상수 → 마스터데이터 조회 |
| **수정** 클라 `Assets/Scripts/Game/LOPRunner.cs` | `MapId` 상수 제거 → 서버와 동일한 마스터데이터 조회 |
| **수정** 서버 `Assets/Scripts/Game/LOPRunner.cs` | `ResolveScenePath`를 `MatchSceneResolver`로 통일 |
| **이름변경** 클라·서버 `Assets/Scenes/LOPGame.unity` → `FlapWang.unity` | "게임은 하나" 전제를 담은 이름 정리 |

> **왜 LOP-Shared인가**: 클·서가 똑같이 동작해야 하는 시뮬·도메인 로직은 **구체 클래스를 공유**하는 것이 이 프로젝트의 규칙이다(`world-core-connection-architecture.md`의 "시뮬 코드 형태"). 사본을 두면 한쪽만 고쳐도 모른다. GameFramework가 아니라 LOP-Shared인 이유는, 매치/라운드가 **LOP 매치메이킹 도메인 개념**이고 GameFramework는 Luban 전환 때 마스터데이터 추상을 의도적으로 걷어냈기 때문이다 — 씬 경로를 알 이유가 없다.
>
> 새 asmdef는 만들지 않는다. `Runtime/Scripts/Game/`은 이미 `baegames.LOP.Shared.Runtime`(`autoReferenced: true`)에 속해 클·서 양쪽 `Assembly-CSharp`이 그대로 쓸 수 있고, 테스트는 기존 `baegames.LOP.Shared.Tests.EditMode`에 넣으면 된다. 두 프로젝트 `manifest.json`의 `testables`에 `com.baegames.lop.shared`가 있어 양쪽 Test Runner에 그대로 뜬다.

> **DI 스코프 분해(`NetcodeInstaller`/`WorldCoreInstaller`)는 이 슬라이스에 없다.** 게임이 하나뿐인 지금은 무엇이 공통이고 무엇이 게임별인지 판단할 근거가 없다. 두 번째 게임이 생기는 **슬라이스 B**에서 실제 차이를 보고 가른다.

---

## Task 1: `MatchSceneResolver` 순수 로직 + 테스트 (LOP-Shared)

씬을 고를 때의 두 가지 검증 규칙을 엔진·DI 비의존 순수 함수로 만든다. 이후 태스크의 클라·서버 코드가 모두 이걸 쓴다.

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/MatchSceneResolver.cs`
- Create: `LeagueOfPhysical-Shared/Tests/EditMode/MatchSceneResolverTests.cs`

**작업 저장소:** `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared` (클·서와 **다른 git 저장소**다)

**Interfaces:**
- Consumes: (없음 — 첫 태스크)
- Produces: `namespace LOP`의 `public static class MatchSceneResolver`
  - `public static int CurrentRoundIndex(int roundCount)` — 이번 판이 쓸 라운드 인덱스. `roundCount <= 0`이면 `InvalidOperationException`. 그 외 항상 `0`.
  - `public static string RequireScenePath(string tableName, int id, string scenePath)` — `scenePath`가 null이거나 공백뿐이면 `InvalidOperationException`, 아니면 `scenePath` 그대로 반환.

- [ ] **Step 1: LOP-Shared에 작업 브랜치 생성**

LOP-Shared는 지금 `main`에 클린 상태로 있다. **main에 직접 커밋하지 않는다.**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git switch -c feature/game-mode-axis
git rev-parse --abbrev-ref HEAD
```

Expected: `git status --short`가 비어 있고, 브랜치가 `feature/game-mode-axis`로 바뀐다. 변경사항이 있으면 **멈추고 보고한다** — 남의 작업일 수 있다.

- [ ] **Step 2: 어셈블리가 이미 준비돼 있는지 확인 (새로 만들지 않는다)**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
grep -n 'autoReferenced' Runtime/baegames.LOP.Shared.Runtime.asmdef
grep -n 'baegames.LOP.Shared.Runtime' Tests/EditMode/baegames.LOP.Shared.Tests.EditMode.asmdef
```

Expected: Runtime이 `"autoReferenced": true`(클·서 `Assembly-CSharp`이 그대로 쓸 수 있다는 뜻)이고, EditMode 테스트 어셈블리가 Runtime을 참조한다.

**새 asmdef를 만들지 않는다.** 이 두 개로 충분하다.

- [ ] **Step 3: 실패하는 테스트 작성**

`LeagueOfPhysical-Shared/Tests/EditMode/MatchSceneResolverTests.cs`:

```csharp
using System;
using NUnit.Framework;
using LOP;

public class MatchSceneResolverTests
{
    [Test]
    public void CurrentRoundIndex_라운드가_있으면_첫_라운드를_가리킨다()
    {
        Assert.AreEqual(0, MatchSceneResolver.CurrentRoundIndex(1));
        Assert.AreEqual(0, MatchSceneResolver.CurrentRoundIndex(3));
    }

    [Test]
    public void CurrentRoundIndex_라운드가_없으면_예외()
    {
        Assert.Throws<InvalidOperationException>(() => MatchSceneResolver.CurrentRoundIndex(0));
    }

    [Test]
    public void CurrentRoundIndex_음수는_없는_것과_같다()
    {
        Assert.Throws<InvalidOperationException>(() => MatchSceneResolver.CurrentRoundIndex(-1));
    }

    [Test]
    public void RequireScenePath_값이_있으면_그대로_돌려준다()
    {
        Assert.AreEqual(
            "Assets/Scenes/FlapWang.unity",
            MatchSceneResolver.RequireScenePath("TbGameMode", 1, "Assets/Scenes/FlapWang.unity"));
    }

    [Test]
    public void RequireScenePath_null이면_예외()
    {
        Assert.Throws<InvalidOperationException>(
            () => MatchSceneResolver.RequireScenePath("TbGameMode", 1, null));
    }

    [Test]
    public void RequireScenePath_공백뿐이면_예외()
    {
        Assert.Throws<InvalidOperationException>(
            () => MatchSceneResolver.RequireScenePath("TbGameMode", 1, "   "));
    }

    [Test]
    public void RequireScenePath_예외_메시지에_테이블과_id가_들어간다()
    {
        var e = Assert.Throws<InvalidOperationException>(
            () => MatchSceneResolver.RequireScenePath("TbGameMode", 42, null));

        StringAssert.Contains("TbGameMode", e.Message);
        StringAssert.Contains("42", e.Message);
    }
}
```

- [ ] **Step 4: 테스트가 실패하는지 확인**

Unity Editor에서 **Window > General > Test Runner > EditMode > Run All**.

Expected: `MatchSceneResolverTests`가 컴파일 오류로 실패한다 — `MatchSceneResolver`라는 이름이 없다.

> CLI로 돌린다면: `~/.unity/bin/unity cmd --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client console` 로 컴파일 오류를 확인한다.

- [ ] **Step 5: 최소 구현 작성**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/MatchSceneResolver.cs`:

```csharp
using System;

namespace LOP
{
    /// <summary>
    /// 매치 데이터에서 이번 판이 쓸 씬을 고를 때의 공통 검증 규칙.
    /// 마스터데이터 조회 자체는 호출자가 하고, 여기서는 "무엇을 잘못된 상태로 볼지"만 정한다.
    /// </summary>
    public static class MatchSceneResolver
    {
        /// <summary>
        /// 이번 판이 쓸 라운드의 인덱스. 지금은 항상 첫 라운드다 —
        /// 한 매치에서 여러 게임을 연속으로 도는 로테이션은 아직 구현하지 않았다.
        /// </summary>
        public static int CurrentRoundIndex(int roundCount)
        {
            if (roundCount <= 0)
            {
                throw new InvalidOperationException("매치에 라운드가 없어 씬을 정할 수 없습니다.");
            }

            return 0;
        }

        /// <summary>
        /// 마스터데이터에서 찾은 씬 경로를 검증해 돌려준다.
        /// 데이터 누락을 조용히 넘기면 씬이 안 뜨는 이유를 런타임에 추적해야 하므로 여기서 끊는다.
        /// </summary>
        public static string RequireScenePath(string tableName, int id, string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new InvalidOperationException(
                    $"{tableName}의 씬 경로가 비어 있습니다. id: {id}");
            }

            return scenePath;
        }
    }
}
```

- [ ] **Step 6: 테스트가 통과하는지 확인**

클라 프로젝트 Unity Editor에서 **Test Runner > EditMode > Run All**. (LOP-Shared가 `manifest.json`의 `testables`에 있어 패키지 테스트가 여기 뜬다.)

Expected: `MatchSceneResolverTests`의 7개 테스트가 모두 PASS. 기존 `baegames.LOP.Shared.Tests.EditMode`와 `FlappyRaceSlice.Tests.EditMode` 테스트들도 그대로 PASS.

- [ ] **Step 7: 테스트가 진짜로 실패할 수 있는지 확인**

`RequireScenePath`의 `if` 조건을 일시적으로 `if (false)`로 바꾸고 Test Runner를 다시 돌린다.

Expected: `RequireScenePath_null이면_예외`, `RequireScenePath_공백뿐이면_예외`, `RequireScenePath_예외_메시지에_테이블과_id가_들어간다` 3개가 FAIL.

확인했으면 **원래대로 되돌린다**. (통과만 보고 검증됐다고 하지 않는다 — 일부러 깨뜨려 본다.)

- [ ] **Step 8: 커밋**

**커밋은 LOP-Shared 저장소에서 한다** (클·서와 다른 저장소다).

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git add \
  Runtime/Scripts/Game/MatchSceneResolver.cs \
  Runtime/Scripts/Game/MatchSceneResolver.cs.meta \
  Tests/EditMode/MatchSceneResolverTests.cs \
  Tests/EditMode/MatchSceneResolverTests.cs.meta
```

**커밋 전에 반드시 별도 명령으로 확인한다:**

```bash
git diff --cached --name-only
```

위 4개 경로만 있는지 눈으로 확인한 뒤에 커밋한다. 다른 것이 섞여 있으면 **커밋하지 말고** `git restore --staged <경로>`로 뺀다.

```bash
git commit -m "feat(game-mode): 씬 경로 검증 규칙을 클·서 공유 로직으로 추가

게임 씬을 마스터데이터에서 고르기 위한 첫 조각. 라운드 유무와 씬 경로 누락을
검증하는 규칙을 엔진·DI 비의존 static 클래스로 만든다.

클·서가 같은 구체 코드를 쓴다 — 사본을 두면 한쪽만 고쳐도 모른다. 매치/라운드는
LOP 매치메이킹 도메인 개념이라 GameFramework가 아니라 여기가 맞다(GameFramework는
Luban 전환 때 마스터데이터 추상을 걷어냈다).

슬라이스 D의 순위 산정·종료 판정 순수 로직도 같은 자리에 들어올 예정이다."
```

---

## Task 2: 마스터데이터에 `TbGameMode.scene_path` 추가

게임 모드가 자기 씬을 가리키게 한다. 코드 변경은 없고 데이터와 생성물만 바뀐다.

**Files:**
- Modify: `infrastructure/table/Datas/#GameMode.xlsx`
- Regenerate: `LeagueOfPhysical-MasterData-Client/Runtime.Generated/**`, `LeagueOfPhysical-MasterData-Server/Runtime.Generated/**`, `lop-backend/apps/matchmaking-server/{src/masterdata,master_data}`

**Interfaces:**
- Consumes: (없음)
- Produces: `LOP.MasterData.GameMode`에 `public readonly string ScenePath;` 필드. 클라·서버 양쪽 생성물에 존재하고, matchmaking(TypeScript) 생성물에는 **없다**.

**배경 — 현재 `#GameMode.xlsx` 모양** (읽기 전용 참고):

```
      A          B    C              D             E            F            G
r1  ##var        id   code           name          description  min_players  max_players
r2  ##type       int  string         string        string       int          int
r3  ##group                          c             c
r4  ##           id   code           name          description  min_players  max_players
r5               1    FlapWang       플랩왕                       2            8
r6               2    Dodgeball      닷지볼                        2            8
r7               3    ObserverAvoid  관찰자 피하기                  2            8
r8               4    RememberGame   기억력 게임                    2            8
r9               5    TargetShooting 타겟 슈팅                     2            8
```

`##group`이 빈 칸이면 `luban.conf`에서 `default: true`인 그룹(`c`, `s`)에만 들어가고 `m`(matchmaking)에는 안 들어간다. 씬 경로는 유니티만 쓰므로 **빈 칸으로 둔다.**

- [ ] **Step 1: xlsx 편집 도구 준비**

```bash
python3 -m venv /tmp/lop-xlsx
/tmp/lop-xlsx/bin/pip install --quiet openpyxl
/tmp/lop-xlsx/bin/python -c "import openpyxl; print(openpyxl.__version__)"
```

Expected: 버전 문자열이 찍힌다.

- [ ] **Step 2: `scene_path` 컬럼 추가**

```bash
/tmp/lop-xlsx/bin/python - <<'PY'
import openpyxl
p = '/Users/insoobae/workspace/LOP/infrastructure/table/Datas/#GameMode.xlsx'
wb = openpyxl.load_workbook(p)
ws = wb.worksheets[0]

ws['H1'] = 'scene_path'
ws['H2'] = 'string'
# H3(##group)은 비워 둔다 — c/s에만 들어가고 matchmaking에는 안 들어간다.
ws['H4'] = 'scene_path'
ws['H5'] = 'Assets/Scenes/FlapWang.unity'   # id 1 = FlapWang
# id 2~5(닷지볼·관찰자 피하기·기억력 게임·타겟 슈팅)는 아직 씬이 없어 빈 값으로 둔다.

wb.save(p)
print('saved')
PY
```

- [ ] **Step 3: 편집 결과 확인**

```bash
/tmp/lop-xlsx/bin/python - <<'PY'
import openpyxl
p = '/Users/insoobae/workspace/LOP/infrastructure/table/Datas/#GameMode.xlsx'
ws = openpyxl.load_workbook(p).worksheets[0]
for r in range(1, 10):
    print(r, [ws.cell(row=r, column=c).value for c in range(1, 9)])
PY
```

Expected: 1행 마지막이 `scene_path`, 2행 마지막이 `string`, 3행 마지막이 `None`, 5행 마지막이 `Assets/Scenes/FlapWang.unity`, 6~9행 마지막이 `None`.

- [ ] **Step 4: 생성 실행**

```bash
cd /Users/insoobae/workspace/LOP/infrastructure/table
./gen.sh
```

Expected: `[gen] target=client` / `target=server` / `target=matchmaking` 세 줄이 찍히고 마지막에 `[done]`. 오류 없이 끝나야 한다.

> 여기서 실패하면 대개 빈 `scene_path` 셀 때문이다. 그 경우 오류 메시지를 그대로 보고하고 멈춘다 — 임의로 더미 경로를 채워 넣지 않는다.

- [ ] **Step 5: 생성물 검증**

```bash
cd /Users/insoobae/workspace/LOP
echo "=== 클라 생성물에 ScenePath 있나 (있어야 함)"
grep -n 'ScenePath' LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/GameMode.cs
echo "=== 서버 생성물에 ScenePath 있나 (있어야 함)"
grep -n 'ScenePath' LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/GameMode.cs
echo "=== matchmaking 생성물에 scene 있나 (없어야 함)"
grep -rn 'scenePath\|scene_path' lop-backend/apps/matchmaking-server/src/masterdata/ | head
```

Expected: 앞의 두 개는 `public readonly string ScenePath;` 가 나오고, 마지막은 **아무것도 안 나온다.**

- [ ] **Step 6: 세 저장소에 각각 커밋**

각 저장소마다 `git status --short`로 무엇이 바뀌었는지 먼저 보고, 해당 경로만 스테이징한다.

```bash
cd /Users/insoobae/workspace/LOP/infrastructure
git status --short
git add table/Datas/#GameMode.xlsx
git diff --cached --name-only
git commit -m "feat(masterdata): TbGameMode에 scene_path 추가

게임 모드가 자기 게임 씬을 가리키게 한다. 지금까지 씬 이름은 유니티 코드에
상수로 박혀 있어 gameModeId가 아무것도 결정하지 못했다.

##group을 비워 둬 c/s(유니티)에만 들어가고 matchmaking 서버 생성물에는
포함되지 않는다 — 매치메이커는 씬을 알 필요가 없다."
```

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Client
git status --short
git add Runtime.Generated
git diff --cached --name-only
git commit -m "chore(masterdata): TbGameMode.scene_path 반영 (생성물)"
```

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Server
git status --short
git add Runtime.Generated
git diff --cached --name-only
git commit -m "chore(masterdata): TbGameMode.scene_path 반영 (생성물)"
```

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
git status --short
```

matchmaking 생성물은 재생성만 되고 내용이 같을 수 있다. **변경이 있으면** 커밋하고, 없으면 넘어간다.

```bash
git add apps/matchmaking-server/src/masterdata apps/matchmaking-server/master_data
git diff --cached --name-only
git commit -m "chore(masterdata): 테이블 재생성 반영"
```

---

## Task 3: 클라 — 게임 씬을 마스터데이터로 고르고 씬 이름을 정리

**Files:**
- Rename: `LeagueOfPhysical-Client/Assets/Scenes/LOPGame.unity` → `FlapWang.unity` (`.meta` 포함)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/LOPGameFactory.cs`
- Modify: `LeagueOfPhysical-Client/ProjectSettings/EditorBuildSettings.asset` (경로 갱신)

**Interfaces:**
- Consumes: `MatchSceneResolver.CurrentRoundIndex(int)`, `MatchSceneResolver.RequireScenePath(string, int, string)` (Task 1), `LOP.MasterData.GameMode.ScenePath` (Task 2)
- Produces: `LOPGameFactory`가 생성자 주입을 받는 형태로 바뀐다 — `LOPGameFactory(IRoomDataStore, LOP.MasterData.LOPMasterData)`. DI 등록(`RoomLifetimeScope`의 `builder.Register<IGameFactory, LOPGameFactory>`)은 그대로 두면 VContainer가 생성자 인자를 채운다.

> **왜 안전한가**: `IRoomDataStore`(RootLifetimeScope 싱글턴)에는 `LOPRoom.InitializeAsync`가 `WebAPI.GetMatch`를 부른 시점에 이미 `match`가 채워져 있다. `WebAPI.SendAsync`가 모든 응답을 메시지 파이프에 발행하고 `RoomDataStore.HandleGetMatch`가 받아 넣기 때문이다. 그 호출은 `gameFactory.CreateAsync()` **직전**에 일어난다.

- [ ] **Step 1: 씬 파일 이름 변경**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git mv Assets/Scenes/LOPGame.unity Assets/Scenes/FlapWang.unity
git mv Assets/Scenes/LOPGame.unity.meta Assets/Scenes/FlapWang.unity.meta
ls Assets/Scenes/
```

Expected: `FlapWang.unity`, `FlapWang.unity.meta`가 있고 `LOPGame.*`은 없다.

> `.meta`를 함께 옮기므로 GUID가 유지된다. GUID가 바뀌면 Build Settings와 Addressables 참조가 전부 끊긴다.

- [ ] **Step 2: Unity에서 에셋 갱신 + Build Settings 확인**

Unity Editor를 열어(이미 열려 있으면 포커스를 주어 리임포트가 돌게 한다) **File > Build Profiles/Settings**의 씬 목록을 확인한다.

Expected: 목록의 세 번째 항목이 `Assets/Scenes/FlapWang.unity`로 바뀌어 있다 (GUID가 같아 Unity가 경로를 자동으로 고친다).

```bash
grep -A 12 'm_Scenes' ProjectSettings/EditorBuildSettings.asset | grep -E 'path|enabled'
```

Expected: `path: Assets/Scenes/FlapWang.unity`가 보인다. 아직 `LOPGame`이면 Unity가 갱신하지 않은 것이니, Build Settings 창을 열어 목록을 한 번 클릭해 갱신시킨다.

- [ ] **Step 3: `LOPGameFactory`를 마스터데이터 기반으로 교체**

`LeagueOfPhysical-Client/Assets/Scripts/Game/LOPGameFactory.cs` 전체를 아래로 교체한다:

```csharp
using Cysharp.Threading.Tasks;
using GameFramework;
using GameFramework.Runner;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 이번 판의 게임 씬을 Room 스코프의 자식으로 additive 로드해 game을 생성한다.
    /// 어떤 씬인지는 매치가 정한 게임 모드에서 온다 — 게임마다 다른 씬이 통째로 올라온다.
    /// </summary>
    public class LOPGameFactory : IGameFactory
    {
        private readonly IRoomDataStore roomDataStore;
        private readonly LOP.MasterData.LOPMasterData masterData;

        private string loadedScenePath;

        public LOPGameFactory(IRoomDataStore roomDataStore, LOP.MasterData.LOPMasterData masterData)
        {
            this.roomDataStore = roomDataStore;
            this.masterData = masterData;
        }

        public async Task<IRunner> CreateAsync()
        {
            loadedScenePath = ResolveGameScenePath();

            var roomScope = LifetimeScope.Find<RoomLifetimeScope>();

            using (LifetimeScope.EnqueueParent(roomScope))
            {
                await SceneManager.LoadSceneAsync(loadedScenePath, LoadSceneMode.Additive).ToUniTask();
            }

            var gameScope = LifetimeScope.Find<GameLifetimeScope>();
            return gameScope.Container.Resolve<IRunner>();
        }

        public async Task DestroyAsync()
        {
            if (string.IsNullOrEmpty(loadedScenePath))
            {
                return;
            }

            var scene = SceneManager.GetSceneByPath(loadedScenePath);
            if (scene.isLoaded)
            {
                await SceneManager.UnloadSceneAsync(scene).ToUniTask();
            }

            loadedScenePath = null;
        }

        private string ResolveGameScenePath()
        {
            var rounds = roomDataStore.match?.rounds;
            var round = rounds[MatchSceneResolver.CurrentRoundIndex(rounds?.Length ?? 0)];
            var gameMode = masterData.Tables.TbGameMode.GetOrDefault(round.gameModeId);

            return MatchSceneResolver.RequireScenePath("TbGameMode", round.gameModeId, gameMode?.ScenePath);
        }
    }
}
```

- [ ] **Step 4: 컴파일 확인**

```bash
~/.unity/bin/unity cmd --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client console
```

Expected: `LOPGameFactory` 관련 컴파일 오류가 없다. (콘솔에 남아 있던 이전 오류와 구분하려면 먼저 `clear_console`를 돌린 뒤 `recompile` → `recompile_status`를 확인한다.)

- [ ] **Step 5: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add \
  Assets/Scenes/FlapWang.unity \
  Assets/Scenes/FlapWang.unity.meta \
  Assets/Scripts/Game/LOPGameFactory.cs \
  ProjectSettings/EditorBuildSettings.asset
git diff --cached --name-only
```

위 4개(rename이라 `LOPGame.unity` 삭제가 함께 보일 수 있다)만 있는지 확인한 뒤 커밋한다. **`ProjectSettings/ProjectSettings.asset`이나 `PackageManagerSettings.asset`이 섞여 있으면 뺀다** — 그건 사용자의 미커밋 작업물이다.

```bash
git commit -m "feat(game-mode): 클라가 게임 씬을 마스터데이터에서 고른다

씬 이름 상수 \"LOPGame\"을 지우고 match.rounds[0].gameModeId로 TbGameMode를
조회해 ScenePath를 얻는다. gameModeId가 지금까지 아무것도 결정하지 못했는데,
이제 어떤 게임이 올라올지를 정한다.

씬 이름도 LOPGame → FlapWang으로 바꿨다. \"게임은 하나\"라는 전제를 담은
이름이라 게임이 늘어나면 곧바로 헷갈린다."
```

---

## Task 4: 클라 — 맵 경로 하드코딩 제거

서버는 이미 `rounds[0].mapId`로 맵을 고르는데 클라만 상수를 쓰고 있다. 이 비대칭을 없앤다.

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/LOPRunner.cs`

**Interfaces:**
- Consumes: `MatchSceneResolver` (Task 1), `LOP.MasterData.GameMap.ScenePath` (기존 컬럼)
- Produces: (없음 — 내부 변경)

- [ ] **Step 1: 주입 추가와 상수 제거**

`LeagueOfPhysical-Client/Assets/Scripts/Game/LOPRunner.cs`에서 다음 줄을 찾는다:

```csharp
        private const string MapId = "Assets/Art/Scenes/FlapWangMap.unity";
```

이 줄을 **삭제**하고, `[Inject]` 필드 블록 끝(`private ReconcileSystem reconcileSystem;` 등이 있는 곳)에 아래 두 줄을 추가한다:

```csharp
        [Inject] private IRoomDataStore roomDataStore;
        [Inject] private LOP.MasterData.LOPMasterData masterData;
```

- [ ] **Step 2: 맵 경로 해석 메서드 추가**

`InitializeAsync` 아래(또는 `DeinitializeAsync` 위)에 서버와 같은 모양의 메서드를 추가한다:

```csharp
        /// <summary>이 판에서 로드할 맵 씬. 매치의 이번 라운드가 가리키는 맵에서 온다.</summary>
        private string ResolveMapScenePath()
        {
            var rounds = roomDataStore.match?.rounds;
            var round = rounds[MatchSceneResolver.CurrentRoundIndex(rounds?.Length ?? 0)];
            var map = masterData.Tables.TbMap.GetOrDefault(round.mapId);

            return MatchSceneResolver.RequireScenePath("TbMap", round.mapId, map?.ScenePath);
        }
```

- [ ] **Step 3: 호출부 교체**

`InitializeAsync` 안의 아래 줄을 찾는다:

```csharp
            var mapLoadTask = mapLoader.LoadAsync(MapId);
```

아래로 바꾼다:

```csharp
            var mapLoadTask = mapLoader.LoadAsync(ResolveMapScenePath());
```

- [ ] **Step 4: 컴파일 확인**

```bash
~/.unity/bin/unity cmd --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client console
```

Expected: 컴파일 오류 없음. `MapId` 참조가 남아 있으면 오류가 나므로 여기서 잡힌다.

- [ ] **Step 5: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/LOPRunner.cs
git diff --cached --name-only
git commit -m "fix(game-mode): 클라도 맵을 마스터데이터에서 고른다

서버는 rounds[0].mapId → TbMap.ScenePath로 맵을 고르는데 클라만 경로가
상수로 박혀 있었다. 맵이 늘어나면 클라만 엉뚱한 씬을 여는 비대칭이라 없앤다."
```

---

## Task 5: 서버 — 동일 배선

**Files:**
- Rename: `LeagueOfPhysical-Server/Assets/Scenes/LOPGame.unity` → `FlapWang.unity` (`.meta` 포함)
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPGameFactory.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs`
- Modify: `LeagueOfPhysical-Server/ProjectSettings/EditorBuildSettings.asset`

**Interfaces:**
- Consumes: `LOP.MatchSceneResolver` (Task 1 — LOP-Shared 패키지에 있고 `autoReferenced: true`라 서버 `Assembly-CSharp`이 그대로 쓴다. **파일을 복사하지 않는다**), `LOP.MasterData.GameMode.ScenePath` (Task 2)
- Produces: (없음 — 서버 내부 배선)

- [ ] **Step 1: 공유 로직이 서버에서 보이는지 확인**

Task 1에서 LOP-Shared에 추가한 `MatchSceneResolver`를 서버가 참조 없이 쓸 수 있는지 먼저 확인한다.

```bash
ls /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/MatchSceneResolver.cs
python3 -c "
import json
m = json.load(open('/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server/Packages/manifest.json'))
print('lop.shared:', m['dependencies'].get('com.baegames.lop.shared'))"
```

Expected: 파일이 존재하고, 서버 manifest가 `file:../../LeagueOfPhysical-Shared`로 그 패키지를 본다.

**서버에 사본을 만들지 않는다.** 클·서가 같은 구체 코드를 쓰는 것이 이 프로젝트의 규칙이다.

- [ ] **Step 2: 씬 파일 이름 변경**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git mv Assets/Scenes/LOPGame.unity Assets/Scenes/FlapWang.unity
git mv Assets/Scenes/LOPGame.unity.meta Assets/Scenes/FlapWang.unity.meta
ls Assets/Scenes/
```

Expected: `FlapWang.unity`, `FlapWang.unity.meta`.

- [ ] **Step 3: `LOPGameFactory` 교체**

`LeagueOfPhysical-Server/Assets/Scripts/Game/LOPGameFactory.cs` 전체를 아래로 교체한다 (클라와 같은 구조):

```csharp
using Cysharp.Threading.Tasks;
using GameFramework;
using GameFramework.Runner;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 이번 판의 게임 씬을 Room 스코프의 자식으로 additive 로드해 game을 생성한다.
    /// 어떤 씬인지는 매치가 정한 게임 모드에서 온다 — 게임마다 다른 씬이 통째로 올라온다.
    /// </summary>
    public class LOPGameFactory : IGameFactory
    {
        private readonly IRoomDataStore roomDataStore;
        private readonly LOP.MasterData.LOPMasterData masterData;

        private string loadedScenePath;

        public LOPGameFactory(IRoomDataStore roomDataStore, LOP.MasterData.LOPMasterData masterData)
        {
            this.roomDataStore = roomDataStore;
            this.masterData = masterData;
        }

        public async Task<IRunner> CreateAsync()
        {
            loadedScenePath = ResolveGameScenePath();

            var roomScope = LifetimeScope.Find<RoomLifetimeScope>();

            using (LifetimeScope.EnqueueParent(roomScope))
            {
                await SceneManager.LoadSceneAsync(loadedScenePath, LoadSceneMode.Additive).ToUniTask();
            }

            var gameScope = LifetimeScope.Find<GameLifetimeScope>();
            return gameScope.Container.Resolve<IRunner>();
        }

        public async Task DestroyAsync()
        {
            if (string.IsNullOrEmpty(loadedScenePath))
            {
                return;
            }

            var scene = SceneManager.GetSceneByPath(loadedScenePath);
            if (scene.isLoaded)
            {
                await SceneManager.UnloadSceneAsync(scene).ToUniTask();
            }

            loadedScenePath = null;
        }

        private string ResolveGameScenePath()
        {
            var rounds = roomDataStore.match?.rounds;
            var round = rounds[MatchSceneResolver.CurrentRoundIndex(rounds?.Length ?? 0)];
            var gameMode = masterData.Tables.TbGameMode.GetOrDefault(round.gameModeId);

            return MatchSceneResolver.RequireScenePath("TbGameMode", round.gameModeId, gameMode?.ScenePath);
        }
    }
}
```

> 지금 서버 `LOPGameFactory`에는 생성자가 없고 `private const string GameSceneName = "LOPGame";` 만 있다. 위 코드가 그 상수를 지우고 생성자를 추가한다. DI 등록은 `RoomLifetimeScope.cs:21`의 `builder.Register<IGameFactory, LOPGameFactory>(Lifetime.Singleton);` 그대로 두면 되고, VContainer가 생성자 인자를 채운다 — 등록 코드는 건드리지 않는다.

- [ ] **Step 4: 서버 `LOPRunner.ResolveScenePath`를 공통 규칙으로 통일**

`LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs`의 기존 메서드를 찾는다:

```csharp
        /// <summary>이 판에서 로드할 씬. 매치의 첫 라운드가 가리키는 맵에서 온다.</summary>
        private string ResolveScenePath()
        {
            var rounds = roomDataStore.match?.rounds;
            if (rounds == null || rounds.Length == 0)
            {
                throw new Exception("매치에 라운드가 없어 맵을 정할 수 없습니다.");
            }

            var mapId = rounds[0].mapId;
            var map = masterData.Tables.TbMap.GetOrDefault(mapId);
            if (map == null)
            {
                throw new Exception($"TbMap에 없는 mapId입니다. mapId: {mapId}");
            }

            return map.ScenePath;
        }
```

아래로 교체한다 (이름을 클라와 맞춰 `ResolveMapScenePath`로 바꾸고, 호출부도 함께 바꾼다):

```csharp
        /// <summary>이 판에서 로드할 맵 씬. 매치의 이번 라운드가 가리키는 맵에서 온다.</summary>
        private string ResolveMapScenePath()
        {
            var rounds = roomDataStore.match?.rounds;
            var round = rounds[MatchSceneResolver.CurrentRoundIndex(rounds?.Length ?? 0)];
            var map = masterData.Tables.TbMap.GetOrDefault(round.mapId);

            return MatchSceneResolver.RequireScenePath("TbMap", round.mapId, map?.ScenePath);
        }
```

`InitializeAsync` 안의 `ResolveScenePath()` 호출을 `ResolveMapScenePath()`로 바꾼다. `ResolveScenePath` 참조가 더 남아 있지 않은지 확인한다:

```bash
grep -rn 'ResolveScenePath' /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/
```

Expected: 아무것도 안 나온다.

> 예외 타입이 `Exception` → `InvalidOperationException`으로 바뀐다. 상위에서 `catch (Exception)`으로 받고 있어 잡히는 범위는 그대로다.

- [ ] **Step 5: Build Settings 확인 + 컴파일 확인**

Unity Editor(서버 프로젝트)에서 Build Settings 씬 목록이 `Assets/Scenes/FlapWang.unity`로 바뀌었는지 확인한다.

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
grep -E 'path' ProjectSettings/EditorBuildSettings.asset
```

Expected: `Assets/Scenes/FlapWang.unity`

컴파일 확인:

```bash
~/.unity/bin/unity cmd --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server console
```

Expected: 컴파일 오류 없음.

- [ ] **Step 6: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add \
  Assets/Scenes/FlapWang.unity \
  Assets/Scenes/FlapWang.unity.meta \
  Assets/Scripts/Game/LOPGameFactory.cs \
  Assets/Scripts/Game/LOPRunner.cs \
  ProjectSettings/EditorBuildSettings.asset
git diff --cached --name-only
```

목록을 눈으로 확인한 뒤 커밋한다.

```bash
git commit -m "feat(game-mode): 서버가 게임 씬을 마스터데이터에서 고른다

클라와 동일하게 match.rounds[0].gameModeId → TbGameMode.ScenePath로 게임 씬을
고른다. 맵 경로 해석도 LOP-Shared의 MatchSceneResolver로 통일했다 — 클·서가
같은 구체 코드를 쓰므로 검증 규칙이 갈라질 수 없다.

기존 ResolveScenePath는 예외를 bare Exception으로 던졌는데, 공유 규칙이
InvalidOperationException을 쓴다. 상위에서 catch (Exception)으로 받고 있어
잡히는 범위는 그대로다."
```

---

## Task 6: 회귀 검증 — 지금과 똑같이 동작하는가

이 슬라이스의 성공 기준은 **아무것도 달라지지 않는 것**이다. 이 태스크가 그걸 확인한다.

**Files:** (코드 변경 없음)

**Interfaces:**
- Consumes: Task 1~5의 모든 변경
- Produces: (없음)

- [ ] **Step 1: 양쪽 컴파일 확인**

```bash
~/.unity/bin/unity cmd --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client console
~/.unity/bin/unity cmd --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server console
```

Expected: 양쪽 다 컴파일 오류 0건.

- [ ] **Step 2: EditMode 테스트 전체 실행 (클라)**

Unity Editor에서 **Test Runner > EditMode > Run All**.

Expected: `MatchSceneResolverTests` 7개 + 기존 `FlappyRaceSlice.Tests.EditMode`(`FlappyChaserCurveTests`, `FlappyChaserOutcomeTests`, `FlappyBounceTests`) 전부 PASS.

- [ ] **Step 3: 서버 에디터 실행 → 룸 초기화 확인**

서버 프로젝트를 Play 모드로 실행한다. `ConfigureRoomComponent`가 에디터에서 `gameModeId = 1, mapId = 1`인 라운드를 만든다.

Expected (Console):
- 예외 없음
- `FlapWang` 게임 씬과 `FlapWangMap` 맵 씬이 additive로 로드된다
- `TbGameMode`/`TbMap` 관련 `InvalidOperationException`이 없다

Expected (Hierarchy): `FlapWang` 씬과 `FlapWangMap` 씬이 함께 올라와 있다.

- [ ] **Step 4: 클라 에디터 실행 → 접속·플레이 확인**

클라 프로젝트를 Play 모드로 실행해 서버에 접속한다.

Expected:
- 캐릭터가 스폰되고 이동이 된다
- 예외 없음
- 슬라이스 이전과 화면·조작이 동일하다

- [ ] **Step 5: 예측·롤백 회귀 확인**

이 슬라이스가 순수 리팩터이므로, 넷코드가 여기서 깨지지 않았다면 축 배선은 무죄다.

디버그 HUD를 열고 다음을 확인한다:
- reconciliation distance(last/avg/max)가 슬라이스 이전과 같은 수준이다
- 공중에서 점프해도 눈에 띄는 rubberbanding이 없다
- `lead`, RTT 값이 정상 범위다

> 이전 수치를 모른다면, 이 슬라이스 **직전 커밋으로 잠시 되돌려** 같은 조작을 해보고 비교한다.

- [ ] **Step 6: 데이터 누락 시 동작 확인**

`ConfigureRoomComponent`의 에디터 픽스처에서 `gameModeId = 1`을 **`gameModeId = 2`**(Dodgeball — 아직 `scene_path`가 비어 있다)로 잠시 바꾸고 서버를 실행한다.

Expected: `InvalidOperationException`이 나고 메시지에 `TbGameMode`와 `2`가 들어 있다. 조용히 넘어가거나 엉뚱한 씬이 뜨지 않는다.

확인했으면 **`gameModeId = 1`로 되돌린다.**

- [ ] **Step 7: 최종 상태 확인**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git log --oneline origin/main..HEAD
git status --short | wc -l
```

Expected: 이 슬라이스의 커밋들이 보이고, 미커밋 개수가 **31개 그대로**다 (사용자 작업물을 건드리지 않았다는 확인).

---

## 이 슬라이스에서 하지 않는 것

다음 슬라이스로 넘기는 것들을 명시해 둔다. 구현 중 "이것도 해야 하나?" 싶으면 여기를 본다.

| 항목 | 어디로 |
|---|---|
| `NetcodeInstaller` / `WorldCoreInstaller` 추출, 게임별 LifetimeScope 분리 | **슬라이스 B** — 두 번째 게임이 있어야 공통/게임별 경계를 판단할 수 있다 |
| `FlappyRace` 게임 씬·월드·룰 | **슬라이스 B** |
| 로비 게임 선택 UI, `MatchmakingViewModel`의 `gameModeId = 1` 하드코딩 제거 | **슬라이스 C** |
| 게임별 종료 조건, `MatchEndedToC`에 순위 싣기, 결과 화면 | **슬라이스 D** |
| `rounds[1..]` 로테이션 | 미니게임이 3개 이상 생긴 뒤 |
| FlapWang 제거 | Flappy가 넷코드 검증 베드를 넘겨받은 뒤 |
