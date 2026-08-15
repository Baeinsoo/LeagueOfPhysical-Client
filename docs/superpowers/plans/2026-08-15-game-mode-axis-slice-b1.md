# 게임 모드 축 슬라이스 B1 — 두 번째 게임 덩어리를 세운다

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `gameModeId`를 FlappyRace로 바꾸면 **완전히 다른 게임 씬 + 다른 맵 + 다른 월드 + 다른 엔티티**가 뜨고, 새(bird)가 스폰돼 화면에 보인다. 움직임·입력·예측은 이 슬라이스에 없다(B2).

**Architecture:** 지금 한 벌뿐인 `GameLifetimeScope`를 **얇은 추상 베이스 + 게임별 스코프 2종**으로 가른다. 공통 등록은 `GameplayInstaller`(VContainer `IInstaller`) 한 벌로 뽑고, 게임별로 갈리는 건 딱 셋이다 — **월드(`IWorld`)·플레이어 몸 생성기(`ICharacterCreator`)·룰(`IGameRuleSystem`, 서버만)**. FlappyRace 스코프는 `FlappyWorld`(빈 Mutation) + `FlappyBirdCreator` + `FlappyRaceRuleSystem`(스폰만)을 끼운다.

**Tech Stack:** Unity 6000.3 / C# / VContainer / Mirror / Luban(마스터데이터) / Addressables / Protobuf

**Spec:** `docs/superpowers/specs/2026-08-15-game-mode-axis-design.md` (§4~§6, §9~§11)

---

## Global Constraints

- **작업 브랜치는 각 저장소에서 `feature/game-mode-axis-b1`이다.** 모든 저장소가 지금 `main`에 있다 — Task 1에서 각자 브랜치를 판다. **`main`에 직접 커밋하지 않는다.**
- **워크트리를 만들지 않는다.** 유니티 레포에서는 금지다 — 현재 체크아웃 자리에서 브랜치만 바꾼다.
- **⚠️ `git add -A` / `git add .` 절대 금지.** 클라 저장소에는 **사용자의 미커밋 작업물이 30개 넘게** 살아 있다(`Assets/Scripts/FlappyRaceSlice/*` 수정·삭제, `Assets/Scenes/DungeonPreview.unity`, `Assets/Screenshots/`, `Assets/Scripts/CatacombKnightAnimShowcase.cs`, `ProjectSettings/*`, `FlappyCalibration/` 등). 아트 서브모듈에도 **미커밋 에셋이 대량으로** 있다(`Characters/CatacombKnight/`, `Characters/Link*/`, `Environment/`, `Etc/`, `Items/Weapons/`). 커밋할 때는 **각 태스크가 명시한 경로만** `git add`한다. 커밋 직전 `git diff --cached --name-only`를 **별도 명령으로** 돌려 눈으로 확인한 뒤 커밋한다.
- **Unity `.meta` 파일은 반드시 함께 커밋한다.** 새 파일·폴더·에셋을 만들거나 옮기면 `.meta`도 같이 스테이징한다. `.meta`를 직접 만들거나 고치지 않는다 — 유니티가 만든 것만 커밋한다.
- **씬·프리팹의 생성/이름변경/컴포넌트 부착은 유니티 에디터에서 한다.** `.unity`/`.prefab` YAML을 손으로 쓰지 않는다 — GUID·fileID가 깨진다. UnityMCP를 쓸 때는 **매 호출에 `unity_instance`를 명시**한다(클라 인스턴스 id는 `mcpforunity://instances`에서 `LeagueOfPhysical-Client`로 찾는다). 서버 에디터를 이 작업으로 건드릴 때도 같은 방식으로 서버 인스턴스를 명시한다.
- **`GameFramework.World` 타입은 항상 풀 네임스페이스로 한정한다** (`GameFramework.World.Entity` 등). `using GameFramework.World;`를 추가하지 않는다 — `Component`가 `UnityEngine.Component`와 충돌한다.
- **주석은 최소로, 비자명한 *의도(왜)* 만.** 코드로 자명한 것은 주석 없이 둔다. 전문용어를 설명 없이 던지지 않는다.
- **클라와 서버를 같이 고친다.** 두 저장소에 같은 개념의 코드가 있으므로 한쪽만 고치면 깨진다.
- **이 슬라이스는 FlapWang의 동작을 바꾸지 않는다.** Task 3~5는 순수 리팩터다 — 끝났을 때 FlapWang은 지금과 똑같이 돌아야 한다.
- **저장소 경로**
  - 클라 `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client`
  - 서버 `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server`
  - 공유 `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared`
  - 아트 `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Art` (클라의 git submodule)
  - 마스터데이터 원본 `/Users/insoobae/workspace/LOP/infrastructure/table`
  - 마스터데이터 산출물 `/Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-{Client,Server}`
  - 백엔드 `/Users/insoobae/workspace/LOP/lop-backend`

### 이 슬라이스가 확정한 값

| 항목 | 값 |
|---|---|
| `TbGameMode` 새 행 | `id=6`, `code=FlappyRace`, `name=플래피 레이스`, `min_players=1`, `max_players=8`, `scene_path=Assets/Scenes/FlappyRace.unity` |
| `TbMap` 새 행 | `id=2`, `game_mode_id=6`, `code=FlappyRaceMap`, `name=플래피 레이스 맵`, `scene_path=Assets/Art/Scenes/FlappyRaceMap.unity` |
| `TbQueue` 수정 | `allowed_game_mode_ids`를 두 행 모두 `1,2,3,4,5,6`으로 |
| 새 비주얼 | `Assets/Art/Characters/FlappyBird/Bird.prefab` |

> **`id=2`가 아니라 `id=6`인 이유.** 스펙 §11이 "gameModeId를 2로 바꾸면"이라고 썼지만, 실제 `TbGameMode`의 id 2는 이미 `Dodgeball`이다(id 1~5가 차 있다). 그래서 FlappyRace는 **새 id 6**이다.
>
> **`min_players=1`인 이유.** 혼자 접속해도 매칭이 성사돼야 이 슬라이스를 검증할 수 있다(매치메이커는 `min_players`만큼 모여야 그룹을 만든다). 실제 인원 요건은 슬라이스 D에서 정한다.

### 스펙과 달라지는 점 (근거 포함)

- **스펙 §5는 공통 등록을 `NetcodeInstaller` + `WorldCoreInstaller` 둘로 쪼개라고 했지만, B1은 `GameplayInstaller` 한 벌로 간다.** 지금 `Reconciler`가 `AbilityActivator`·`StatusEffectSystem`·`StatusEffectDataProvider`·`SequenceBuffer<PredictedAbilityState>`를 생성자에서 받고, 공통 진입점인 `GameInfoMessageHandler`가 `PlayerInputManager`(→`AbilityActivator`)를 받는다. **즉 "넷코드"가 FlapWang의 어빌리티 스택에 물려 있어서, 지금 둘로 쪼개면 Flappy 스코프가 어빌리티 스택을 못 빼고 아무것도 안 뜬다.** 그 얽힘을 푸는 일은 Flappy가 *자기 입력·예측*을 갖는 B2의 몫이다. B1은 등록 세트를 통째로 Installer로 옮겨 **게임별로 갈리는 셋(월드·크리에이터·룰)만** 밖으로 뺀다.
- **스펙 §9의 `FlappyMoveSystem`·`FlappyRaceRule`의 결승선/순위는 B1에 없다.** B1의 `FlappyWorld.Mutation`은 비어 있고 `FlappyRaceRuleSystem`은 스폰만 한다.

---

## File Structure

### 새로 만드는 파일

| 경로 | 책임 |
|---|---|
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs` | Flappy 시뮬 코어. B1에선 `Mutation` 비어 있음 |
| 클라 `Assets/Scripts/Game/GameplayInstaller.cs` | 모든 게임이 공통으로 쓰는 등록 묶음 |
| 클라 `Assets/Scripts/Game/FlapWangLifetimeScope.cs` | FlapWang 덩어리 스코프 |
| 클라 `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` | FlappyRace 덩어리 스코프 |
| 클라 `Assets/Scripts/Entity/ICharacterCreator.cs` | "이 게임에서 플레이어의 몸을 무엇으로 만드나" seam |
| 클라 `Assets/Scripts/Entity/FlappyBirdCreator.cs` | 새 엔티티 조립(클라 측) |
| 클라 `Assets/Scenes/FlappyRace.unity` | FlappyRace 게임 덩어리 씬 |
| 서버 `Assets/Scripts/Game/GameplayInstaller.cs` | 위와 같은 역할(서버판) |
| 서버 `Assets/Scripts/Game/FlapWangLifetimeScope.cs` | FlapWang 덩어리 스코프(서버) |
| 서버 `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` | FlappyRace 덩어리 스코프(서버) |
| 서버 `Assets/Scripts/Game/IGameRuleSystem.cs` | 게임별 룰 seam(초기화/해제) |
| 서버 `Assets/Scripts/Game/FlappyRaceRuleSystem.cs` | Flappy 룰 — B1은 새 스폰만 |
| 서버 `Assets/Scripts/Entity/ICharacterCreator.cs` | 위와 같은 seam(서버판) |
| 서버 `Assets/Scripts/Entity/FlappyBirdCreator.cs` | 새 엔티티 조립(서버 측) |
| 서버 `Assets/Scenes/FlappyRace.unity` | FlappyRace 게임 덩어리 씬(서버) |

### 고치는 파일

| 경로 | 변경 |
|---|---|
| 아트 `Scenes/FlappyRace.unity` → `Scenes/FlappyRaceMap.unity` | rename + 프로토 전용 오브젝트 비활성 |
| `infrastructure/table/Datas/#GameMode.xlsx`, `#Map.xlsx`, `#Queue.xlsx` | 행 추가·수정 |
| 클·서 `Assets/Scripts/Game/GameLifetimeScope.cs` | 구체 스코프 → **추상 베이스**(공통 배선만) |
| 서버 `Assets/Scripts/Game/GameRuleSystem.cs` → `FlapWangRuleSystem.cs` | rename + `IGameRuleSystem` 구현 |
| 서버 `Assets/Scripts/Game/LOPRunner.cs` | `GameRuleSystem` → `IGameRuleSystem` 주입 |
| 서버 `Assets/Scripts/Diagnostics/DebugEnemySpawner.cs` | `GameRuleSystem` → `FlapWangRuleSystem` 주입 |
| 클·서 `Assets/Scripts/Entity/EntitySpawner.cs` | `CharacterCreator` → `ICharacterCreator` |
| 클라 `Assets/Scripts/Entity/CharacterCreator.cs`, 서버 동일 | `ICharacterCreator` 구현 선언만 추가 |
| 클라 `Assets/Scripts/UI/Matchmaking/MatchmakingViewModel.cs` | 하드코딩 `1` → 이름 있는 상수 |
| 클·서 `ProjectSettings/EditorBuildSettings.asset` | `Assets/Scenes/FlappyRace.unity` 등록 |
| 클라 `Assets/AddressableAssetsData/AssetGroups/Scene.asset`, `Character.asset` | 맵 씬·새 프리팹 주소 등록 |

---

## Task 1: 브랜치 준비 + 아트 에셋 커밋 + 맵 씬 승격

**Files:**
- Rename: `Assets/Art/Scenes/FlappyRace.unity` → `Assets/Art/Scenes/FlappyRaceMap.unity`
- Commit(아트): `Scenes/FlappyRaceMap.unity(.meta)`, `Characters/FlappyBird/**`

**Interfaces:**
- Produces: 맵 씬 경로 `Assets/Art/Scenes/FlappyRaceMap.unity`, 새 프리팹 주소 `Assets/Art/Characters/FlappyBird/Bird.prefab` — Task 2·6·7이 이 문자열을 그대로 쓴다.

- [ ] **Step 1: 각 저장소에 작업 브랜치를 만든다**

아트 서브모듈은 지금 **detached HEAD**라 브랜치부터 만들어야 커밋이 남는다.

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Art && git switch -c feature/flappy-race-map
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client && git switch -c feature/game-mode-axis-b1
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server && git switch -c feature/game-mode-axis-b1
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared && git switch -c feature/game-mode-axis-b1
cd /Users/insoobae/workspace/LOP/infrastructure && git switch -c feature/game-mode-axis-b1
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Client && git switch -c feature/game-mode-axis-b1
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Server && git switch -c feature/game-mode-axis-b1
cd /Users/insoobae/workspace/LOP/lop-backend && git switch -c feature/game-mode-axis-b1
```

- [ ] **Step 2: 유니티 에디터에서 맵 씬을 rename한다**

클라 유니티 에디터에서 `Assets/Art/Scenes/FlappyRace.unity`를 선택해 **`FlappyRaceMap`** 으로 rename한다. 에디터로 해야 `.meta`의 GUID가 유지돼 참조가 안 깨진다.

이름을 바꾸는 이유: 게임 덩어리 씬(`Assets/Scenes/FlappyRace.unity`, Task 7에서 만든다)과 파일 이름이 겹치면 `SceneManager`가 이름으로 찾을 때 헷갈린다.

- [ ] **Step 3: 프로토타입 전용 오브젝트를 비활성한다**

`FlappyRaceMap.unity`를 열고 아래 컴포넌트를 가진 **GameObject를 비활성(SetActive false)** 한다. 지우지 않는 이유는 프로토타입으로 되돌아가 튜닝할 일이 남아 있어서다.

- `FlappyRaceManager` — 봇 레이스 진행(새를 자기가 복제·스폰한다. B1에선 서버가 스폰하므로 충돌한다)
- `FlappyRaceStart` — 카운트다운 + `RaceFrozen` 전역 정지
- `FlappyHUD` — 프로토 전용 HUD
- `FlappyCameraFollow` — 프로토 카메라
- `FlappySimJudge`, `FlappyPlayRecorder` — 시뮬 계측/기록
- `FlappyPlayer`/`FlappyAutoPilot`/`FlappyChaser`/`FlappyPacer`를 가진 새 오브젝트 전부

남겨야 하는 것: 코스 지오메트리(파이프·바닥·천장), 조명, 스카이박스.

- [ ] **Step 4: 남은 오브젝트가 맞는지 확인한다**

씬을 저장하고, 하이어라키에 위 목록이 전부 비활성인지 눈으로 확인한다.
Play를 눌러 콘솔에 예외가 없는지 본다(맵 씬 단독 재생 — 새가 없으니 아무 일도 안 일어나는 게 정상이다).

- [ ] **Step 5: 아트 저장소에 커밋한다**

**`git add -A` 금지** — 이 저장소에는 Flappy와 무관한 미커밋 에셋이 대량으로 있다.

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Art
git add Scenes/FlappyRaceMap.unity Scenes/FlappyRaceMap.unity.meta Characters/FlappyBird Characters/FlappyBird.meta
git rm --cached Scenes/FlappyRace.unity Scenes/FlappyRace.unity.meta 2>/dev/null || true
git diff --cached --name-only
```

출력에 **Flappy 관련 경로만** 있는지 눈으로 확인한 뒤:

```bash
git commit -m "feat(flappy): 레이스 코스를 맵 씬으로 승격하고 새 프리팹을 추가한다

프로토타입 씬(FlappyRace.unity)을 FlappyRaceMap.unity로 옮긴다. 게임 덩어리
씬과 이름이 겹치면 씬을 이름으로 찾을 때 헷갈리기 때문이다. 봇 레이스 진행·
카운트다운·프로토 HUD·프로토 카메라처럼 정식 배선과 충돌하는 오브젝트는
비활성했다 — 지우지 않은 건 프로토로 돌아가 튜닝할 일이 남아서다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 6: 클라 저장소에 서브모듈 포인터를 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Art
git diff --cached --name-only
```

`Assets/Art` **한 줄만** 나오는지 확인한 뒤:

```bash
git commit -m "chore(art): Flappy 맵 씬·새 프리팹이 담긴 아트 커밋을 가리킨다

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: 마스터데이터에 FlappyRace 게임 모드·맵 행을 추가한다

**Files:**
- Modify: `infrastructure/table/Datas/#GameMode.xlsx`, `#Map.xlsx`, `#Queue.xlsx`
- Generated: `LeagueOfPhysical-MasterData-{Client,Server}/Runtime.Generated/**`, `lop-backend/apps/matchmaking-server/{src/masterdata,master_data}/**`

**Interfaces:**
- Produces: `masterData.Tables.TbGameMode.GetOrDefault(6).ScenePath == "Assets/Scenes/FlappyRace.unity"`, `TbMap.GetOrDefault(2).ScenePath == "Assets/Art/Scenes/FlappyRaceMap.unity"` — Task 7·8이 이 값에 의존한다.

- [ ] **Step 1: 지금 값을 확인한다 (변경 전 기준선)**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend/apps/matchmaking-server && cat master_data/tbgamemode.json master_data/tbmap.json
```

`TbGameMode`에 id 1~5, `TbMap`에 id 1만 있는지 확인한다. 이미 id 6이 있으면 이 태스크는 중복이니 멈추고 상황을 보고한다.

- [ ] **Step 2: 엑셀에 행을 추가한다**

`.xlsx`는 인라인 문자열(`<is><t>`)을 쓰는 단순한 구조라 파이썬 표준 라이브러리만으로 안전하게 고칠 수 있다. 아래 스크립트를 그대로 실행한다.

```bash
cd /Users/insoobae/workspace/LOP/infrastructure/table && python3 - <<'PY'
import re, shutil, zipfile

NS = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'

def cell(ref, value):
    return f'<c r="{ref}" t="inlineStr"><is><t>{value}</t></is></c>'

def append_row(path, row_index, values, last_col):
    """values = {'B': '6', 'C': 'FlappyRace', ...} — 시트 끝에 행 하나를 덧붙인다."""
    with zipfile.ZipFile(path) as z:
        items = {n: z.read(n) for n in z.namelist()}
    xml = items['xl/worksheets/sheet1.xml'].decode('utf-8')
    cells = ''.join(cell(f'{col}{row_index}', v) for col, v in values.items())
    row = f'<row r="{row_index}">{cells}</row>'
    xml = xml.replace('</sheetData>', row + '</sheetData>')
    xml = re.sub(r'<dimension ref="A1:[A-Z]+\d+" ?/>',
                 f'<dimension ref="A1:{last_col}{row_index}" />', xml)
    items['xl/worksheets/sheet1.xml'] = xml.encode('utf-8')
    shutil.copy(path, path + '.bak')
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as z:
        for name, data in items.items():
            z.writestr(name, data)

def replace_cell_text(path, ref, new_value):
    """이미 있는 인라인 문자열 셀의 값만 바꾼다."""
    with zipfile.ZipFile(path) as z:
        items = {n: z.read(n) for n in z.namelist()}
    xml = items['xl/worksheets/sheet1.xml'].decode('utf-8')
    pattern = re.compile(r'(<c r="' + ref + r'"[^>]*>)(.*?)(</c>)', re.S)
    assert pattern.search(xml), f'{path}: {ref} 셀을 못 찾았다'
    xml = pattern.sub(lambda m: m.group(1) + f'<is><t>{new_value}</t></is>' + m.group(3), xml, count=1)
    items['xl/worksheets/sheet1.xml'] = xml.encode('utf-8')
    shutil.copy(path, path + '.bak')
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as z:
        for name, data in items.items():
            z.writestr(name, data)

# #GameMode: id/code/name/description/min_players/max_players/scene_path = B..H, 데이터 마지막 행 9
append_row('Datas/#GameMode.xlsx', 10, {
    'B': '6', 'C': 'FlappyRace', 'D': '플래피 레이스',
    'E': '날개짓으로 코스를 통과해 결승선에 먼저 닿는 레이스',
    'F': '1', 'G': '8', 'H': 'Assets/Scenes/FlappyRace.unity',
}, last_col='H')

# #Map: id/game_mode_id/code/name/scene_path = B..F, 데이터 마지막 행 5
append_row('Datas/#Map.xlsx', 6, {
    'B': '2', 'C': '6', 'D': 'FlappyRaceMap', 'E': '플래피 레이스 맵',
    'F': 'Assets/Art/Scenes/FlappyRaceMap.unity',
}, last_col='F')

# #Queue: allowed_game_mode_ids = K열. Casual(5행)·Ranked(6행) 둘 다 6을 허용해야 티켓이 통과한다.
replace_cell_text('Datas/#Queue.xlsx', 'K5', '1,2,3,4,5,6')
replace_cell_text('Datas/#Queue.xlsx', 'K6', '1,2,3,4,5,6')
print('ok')
PY
```

- [ ] **Step 3: 백업 파일을 지우고 결과를 눈으로 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/infrastructure/table && rm -f Datas/*.bak && python3 - <<'PY'
import zipfile
from xml.etree import ElementTree as ET
ns = {'m': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
for f in ['Datas/#GameMode.xlsx', 'Datas/#Map.xlsx', 'Datas/#Queue.xlsx']:
    root = ET.fromstring(zipfile.ZipFile(f).read('xl/worksheets/sheet1.xml'))
    print('==', f)
    for row in root.findall('.//m:row', ns):
        vals = []
        for c in row.findall('m:c', ns):
            t = c.find('m:is/m:t', ns)
            if t is None:
                t = c.find('m:v', ns)
            vals.append(t.text if t is not None else '')
        print(vals)
PY
```

기대: `#GameMode`에 `['6','FlappyRace','플래피 레이스',...,'Assets/Scenes/FlappyRace.unity']` 행, `#Map`에 `['2','6','FlappyRaceMap',...]` 행, `#Queue`의 두 행 모두 `1,2,3,4,5,6`.

- [ ] **Step 4: 생성기를 돌린다**

```bash
cd /Users/insoobae/workspace/LOP/infrastructure/table && ./gen.sh
```

기대: `[gen] target=client`, `target=server`, `target=matchmaking`, `[done]`이 차례로 찍히고 오류가 없다.

- [ ] **Step 5: 생성물이 실제로 바뀌었는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend/apps/matchmaking-server && cat master_data/tbgamemode.json | tail -12 && cat master_data/tbmap.json | tail -10
```

기대: `"id": 6, "code": "FlappyRace"`와 `"id": 2, "game_mode_id": 6`이 보인다.

```bash
for r in LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server lop-backend; do
  echo "== $r =="; cd /Users/insoobae/workspace/LOP/$r && git status --short | head -20
done
```

기대: 세 저장소 모두 생성물에 변경이 있다. **삭제된 `.meta`가 남아 있으면 안 된다**(`gen.sh`가 복원한다 — 남아 있으면 멈추고 보고한다).

- [ ] **Step 6: 네 저장소에 각각 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/infrastructure && git add table/Datas && git diff --cached --name-only
git commit -m "feat(masterdata): FlappyRace 게임 모드와 맵을 추가한다

TbGameMode id=6, TbMap id=2. 큐의 allowed_game_mode_ids에도 6을 넣는다 —
안 넣으면 매치메이킹 서버가 티켓을 INVALID_GAME_MODE로 되돌린다.

min_players를 1로 둔 건 혼자 접속해도 매칭이 성사돼야 이 축이 실제로
갈라지는지 확인할 수 있어서다. 실제 인원 요건은 종료·순위 슬라이스에서 정한다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

나머지 세 저장소는 생성물이라 커밋 메시지를 통일한다.

```bash
for r in LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server; do
  cd /Users/insoobae/workspace/LOP/$r && git add Runtime.Generated && git diff --cached --name-only && \
  git commit -m "chore(masterdata): FlappyRace 게임 모드·맵 반영 (생성물)

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
done

cd /Users/insoobae/workspace/LOP/lop-backend && git add apps/matchmaking-server/src/masterdata apps/matchmaking-server/master_data && git diff --cached --name-only
git commit -m "chore(masterdata): FlappyRace 게임 모드·맵 반영 (생성물)

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 7: 매치메이킹 서버가 새 데이터를 읽게 한다**

**이 단계를 빠뜨리면 Task 9 검증에서 티켓이 조용히 거절된다.** 지금 돌고 있는 매치메이킹 서버를 새 `master_data`로 다시 띄운다 — 방법은 지금 쓰는 환경에 달렸다.

- 로컬(`EnvironmentSettings.local`): `lop-backend`에서 매치메이킹 서버 프로세스를 재시작한다.
- `local-k8s` / `dev`: `lop-backend`를 push하고 GitHub Actions `backend-deploy`로 `matchmaking-server`를 배포한다 → infrastructure의 태그가 bump되고 ArgoCD가 롤아웃한다. **`kubectl apply`로 직접 밀지 않는다** — selfHeal이 되돌린다.

어느 쪽인지 모르면 **여기서 멈추고 사용자에게 어떤 환경으로 검증할지 묻는다.**

---

## Task 3: FlappyWorld 골격을 공유 패키지에 만든다

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldTests.cs`

**Interfaces:**
- Produces: `public class LOP.FlappyWorld : GameFramework.World.WorldBase`, 생성자 `FlappyWorld(GameFramework.World.EntityRegistry, GameFramework.World.WorldEventBuffer)` — Task 7·8의 스코프가 `IWorld`로 등록한다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldTests.cs`:

```csharp
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class FlappyWorldTests
    {
        [Test]
        public void Tick_LeavesEntitiesUntouched_WhileMutationIsEmpty()
        {
            var registry = new EntityRegistry();
            var entity = new Entity("bird-1");
            entity.Add(new GameFramework.World.Transform());
            entity.Add(new Velocity());
            entity.Add(new Simulated());
            registry.Add(entity);

            var world = new FlappyWorld(registry, new WorldEventBuffer());
            world.Tick(1, 0.05f);

            Assert.AreEqual(System.Numerics.Vector3.Zero, entity.Get<GameFramework.World.Transform>().Position);
            Assert.AreEqual(System.Numerics.Vector3.Zero, entity.Get<Velocity>().Linear);
        }
    }
}
```

이 테스트가 지키는 건 "B1의 Flappy 월드는 아직 아무것도 움직이지 않는다"는 계약이다. B2가 이동을 넣으면 이 테스트는 **교체된다** — 그때 실패하는 게 정상이고, 그게 "여기서부터 움직이기 시작했다"는 신호다.

- [ ] **Step 2: 컴파일이 실패하는지 확인한다**

유니티 에디터(클라)에서 콘솔을 본다. `FlappyWorld`를 못 찾는다는 컴파일 에러가 나야 한다.

- [ ] **Step 3: 최소 구현을 쓴다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// Flappy Race의 시뮬 코어. 클·서가 같은 구체 클래스를 돌려 결과가 갈리지 않게 한다.
    /// 지금은 비어 있다 — 전진·플랩·중력은 다음 슬라이스에서 들어온다.
    /// </summary>
    public class FlappyWorld : GameFramework.World.WorldBase
    {
        public FlappyWorld(
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.WorldEventBuffer eventBuffer)
            : base(entityRegistry, eventBuffer)
        {
        }
    }
}
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

유니티 에디터에서 **Window > General > Test Runner > EditMode**를 열고 `FlappyWorldTests`를 돌린다.
기대: PASS. 같은 화면에서 `LOPWorldTests`·`MatchSceneResolverTests`도 여전히 PASS인지 확인한다.

- [ ] **Step 5: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/FlappyWorld.cs Runtime/Scripts/Game/FlappyWorld.cs.meta \
        Tests/EditMode/FlappyWorldTests.cs Tests/EditMode/FlappyWorldTests.cs.meta
git diff --cached --name-only
git commit -m "feat(flappy): Flappy 시뮬 코어 골격을 공유 패키지에 둔다

클·서가 같은 구체 클래스를 컴파일해야 결과가 갈리지 않으므로 공유에 둔다.
Mutation은 비어 있다 — 이번 슬라이스가 증명하려는 건 덩어리가 갈리는지이지
새가 어떻게 나는지가 아니다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: 클라 게임 스코프를 베이스 + Installer로 가른다 (순수 리팩터)

**Files:**
- Create: `클라 Assets/Scripts/Game/GameplayInstaller.cs`
- Create: `클라 Assets/Scripts/Game/FlapWangLifetimeScope.cs`
- Modify: `클라 Assets/Scripts/Game/GameLifetimeScope.cs`
- Modify: `클라 Assets/Scenes/FlapWang.unity` (스코프 컴포넌트 교체)

**Interfaces:**
- Consumes: 없음
- Produces:
  - `public abstract class LOP.GameLifetimeScope : LifetimeScope` — `protected LOPRunner runner` 직렬화 필드, `protected abstract void ConfigureGame(IContainerBuilder builder)`, `protected virtual void RegisterViewFactories(IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)`
  - `public class LOP.GameplayInstaller : IInstaller`
  - `public class LOP.FlapWangLifetimeScope : GameLifetimeScope`

> **이 태스크가 끝나도 화면은 그대로다.** 등록 내용은 한 줄도 바뀌지 않고 *어디에 적혀 있는지*만 바뀐다. `LOPGameFactory`는 `LifetimeScope.Find<GameLifetimeScope>()`로 스코프를 찾는데, VContainer가 내부적으로 `FindAnyObjectByType(type)`을 쓰므로 **추상 베이스 타입으로도 파생 스코프를 찾는다** — Factory는 손대지 않는다.

- [ ] **Step 1: `GameplayInstaller`를 만든다**

지금 `GameLifetimeScope.Configure`에 있는 등록 중 **게임별로 갈리지 않는 것 전부**를 옮긴다. 옮기지 *않는* 것은 넷 뿐이다 — `IWorld`, `CharacterCreator`, `RegisterComponent(runner/cameraController)`, FlapWang UI(Stats/CharacterHud/GamePad).

`클라 Assets/Scripts/Game/GameplayInstaller.cs`:

```csharp
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 게임 덩어리가 게임 종류와 무관하게 공통으로 쓰는 등록.
    /// 게임마다 갈리는 것(월드·플레이어 몸 생성기·게임 UI)은 각 게임 스코프가 따로 넣는다.
    /// </summary>
    public class GameplayInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.EntityRegistry>(Lifetime.Singleton);
            builder.Register<GameFramework.World.WorldEventBuffer>(Lifetime.Singleton);
            builder.Register<GameFramework.World.HealthSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.ManaSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.LevelSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.StatsSystem>(Lifetime.Singleton);
            builder.Register<MovementSystem>(Lifetime.Singleton);
            builder.Register<MotionContributionSystem>(Lifetime.Singleton);
            builder.Register<InputBufferSystem>(Lifetime.Singleton);
            builder.Register<StatusEffectSystem>(Lifetime.Singleton);
            builder.Register<AbilitySystem>(Lifetime.Singleton);
            builder.Register<StatusEffectDataProvider>(Lifetime.Singleton);
            builder.Register<AbilityDataProvider>(Lifetime.Singleton);
            builder.Register<CharacterLoadoutProvider>(Lifetime.Singleton);
            builder.Register<AbilityActivator>(Lifetime.Singleton);

            // effect 실행 — executor가 타입별 핸들러로 디스패치. AbilitySystem이 Active 창에서 구동.
            builder.Register<AbilityEffectExecutor>(Lifetime.Singleton);
            builder.Register<IAbilityEffectHandler>(c => new StatusEffectApplyEffectHandler(
                c.Resolve<StatusEffectSystem>(),
                id => c.Resolve<StatusEffectDataProvider>().Get(id),
                c.Resolve<GameFramework.World.EntityRegistry>()), Lifetime.Singleton);
            builder.Register<GameFramework.World.IEventSink, WorldEventSink>(Lifetime.Singleton);
            builder.Register<GameFramework.Physics.IPhysicsSimulator, GameFramework.Physics.UnityPhysicsSimulator>(Lifetime.Singleton);
            builder.Register<GameFramework.Physics.ICollisionQuery, GameFramework.Physics.UnityCollisionQuery>(Lifetime.Singleton);
            // sweep이 캐릭터도 막는다(Character 포함) — 캐릭터는 서로 통과 못 하는 단단한 벽. 서버와 동일 설정.
            builder.Register<KinematicMoveSystem>(c => new KinematicMoveSystem(
                c.Resolve<GameFramework.Physics.ICollisionQuery>(), LayerMask.GetMask("Default", "Character")), Lifetime.Singleton);
            // 클라: 내 캐릭만 움직인다(남은 벽). 겹치면 내가 전부 빠져나옴(1.0) — sweep 벽이 주로 막고
            // 밀어내기는 슬쩍 들어간 겹침만 복구. 남은 서버 스냅대로 보간해 따라옴.
            builder.Register<GameFramework.World.IMotionBridge>(_ => new MotionBridge(
                LayerMask.GetMask("Default"), LayerMask.GetMask("Character"), 1f), Lifetime.Singleton);
            builder.Register<GameFramework.Runner.IMapLoader, AddressablesMapLoader>(Lifetime.Singleton);

            // 메시지 핸들러: 컨테이너 엔트리포인트로 자기 구독 생명주기를 스스로 관리(스코프가 Initialize/Dispose 구동).
            builder.RegisterEntryPoint<GameInfoMessageHandler>();
            builder.RegisterEntryPoint<GameEntityMessageHandler>();
            builder.RegisterEntryPoint<GameInputTimingMessageHandler>();
            builder.RegisterEntryPoint<GameWorldEventMessageHandler>();
            builder.RegisterEntryPoint<MatchEndedMessageHandler>();
            builder.RegisterEntryPoint<EntityBinder>();
            builder.Register<PlayerInputManager>(Lifetime.Singleton).AsSelf();
            builder.Register<ItemCreator>(Lifetime.Singleton);
            builder.Register<EntitySpawner>(Lifetime.Singleton);
            builder.Register<ActorRegistry>(Lifetime.Singleton);

            builder.Register<DebugHudViewModel>(Lifetime.Transient);
            builder.Register<DebugHudView>(Lifetime.Transient);

            builder.Register<MatchSeed>(Lifetime.Singleton);
            builder.Register<ReconciliationStats>(Lifetime.Singleton);
            builder.Register(_ => new GameFramework.Netcode.RenderCorrectionSmoother(0.1f, 0.025f, 3f), Lifetime.Singleton);
            builder.Register<InputTimingStats>(Lifetime.Singleton);
            builder.Register<GameFramework.Netcode.SnapshotArrivalStats>(Lifetime.Singleton);
            builder.Register<LeadState>(Lifetime.Singleton);
            builder.Register<GameFramework.Netcode.INetworkTime, MirrorNetworkTime>(Lifetime.Singleton);
            builder.Register(_ => new GameFramework.Netcode.SnapshotHistory(128), Lifetime.Singleton);
            builder.Register(_ => new GameFramework.Netcode.SequenceBuffer<PredictedAbilityState>(128), Lifetime.Singleton);
            builder.Register(_ => new GameFramework.Netcode.SequenceBuffer<InputCommand>(128), Lifetime.Singleton);
            builder.Register<Reconciler>(Lifetime.Singleton);
            builder.Register<RemoteInterpolationClock>(Lifetime.Singleton);
            builder.Register<EntityRenderClock>(Lifetime.Singleton);

            builder.Register<ReconcileSystem>(Lifetime.Singleton);
            builder.Register<PhysicsSimulationSystem>(Lifetime.Singleton);
            builder.Register<WorldEventDrainSystem>(Lifetime.Singleton);
            builder.Register<LocalSnapshotSystem>(Lifetime.Singleton);
            builder.Register<DespawnFlushSystem>(Lifetime.Singleton);
        }
    }
}
```

- [ ] **Step 2: `GameLifetimeScope`를 추상 베이스로 바꾼다**

`클라 Assets/Scripts/Game/GameLifetimeScope.cs`를 아래로 **통째로 교체**한다:

```csharp
using LOP.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 게임 씬의 게임 스코프. EnqueueParent(Room)로 로드되면 Room 자식으로 빌드된다.
    /// 게임마다 무엇이 달라지는지는 파생 스코프가 <see cref="ConfigureGame"/>에서 정한다.
    /// </summary>
    public abstract class GameLifetimeScope : LifetimeScope
    {
        [SerializeField, FormerlySerializedAs("gameEngine")] protected LOPRunner runner;

        // 전역 WindowManager에 이 스코프가 기여한 View 팩토리 핸들(OnDestroy에서 해제).
        private readonly List<IDisposable> viewRegistrations = new List<IDisposable>();

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Install(new GameplayInstaller());

            // runner은 게임 서비스에 의존하므로 부모(Room)가 아닌 이 컨테이너에서 주입돼야 한다.
            // AsSelf는 LOP 전용 진입점(EndMatch 등)을 쓰는 소비자를 위한 것 — IRunner에는 없는 API다.
            builder.RegisterComponent(runner).As<GameFramework.Runner.IRunner>().AsSelf();

            ConfigureGame(builder);

            builder.RegisterBuildCallback(container =>
            {
                container.InjectSceneObjects(gameObject.scene);
                SceneManager.sceneLoaded += OnSceneLoaded;

                // 전역 WindowManager에 게임 스코프 View 팩토리 기여: Open<T>가 게임 스코프 resolver로 생성 → IPlayerContext 주입.
                var windowManager = container.Resolve<IWindowManager>();
                viewRegistrations.Add(windowManager.RegisterViewFactory<DebugHudView>(() => container.Resolve<DebugHudView>()));
                RegisterViewFactories(container, windowManager, viewRegistrations);
            });
        }

        /// <summary>이 게임에서만 쓰는 등록 — 월드, 플레이어 몸 생성기, 게임 UI 등.</summary>
        protected abstract void ConfigureGame(IContainerBuilder builder);

        /// <summary>이 게임에서만 여는 화면의 View 팩토리를 sink에 담는다(담긴 것은 스코프가 알아서 해제한다).</summary>
        protected virtual void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
        }

        protected override void OnDestroy()
        {
            // 팩토리 해제 + 열린 View Close (base가 컨테이너를 dispose하기 전에).
            foreach (var registration in viewRegistrations)
            {
                registration?.Dispose();
            }
            viewRegistrations.Clear();

            SceneManager.sceneLoaded -= OnSceneLoaded;
            base.OnDestroy();
        }

        // Factory가 additive 로드하는 맵 씬도 이 컨테이너로 주입한다.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 자기 씬은 빌드 콜백에서 이미 주입했다. (자기 씬 Awake 중 구독해 자기 sceneLoaded도 수신됨)
            if (scene == gameObject.scene)
            {
                Debug.Log($"[GameLifetimeScope] Skip re-injecting own scene '{scene.name}'; already injected in build callback.");
                return;
            }

            Container.InjectSceneObjects(scene);
        }
    }
}
```

- [ ] **Step 3: `FlapWangLifetimeScope`를 만든다**

`클라 Assets/Scripts/Game/FlapWangLifetimeScope.cs`:

```csharp
using LOP.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>FlapWang 덩어리 — 캐릭터 월드와 캐릭터 HUD·게임패드를 쓴다.</summary>
    public class FlapWangLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private CameraController cameraController;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);

            builder.Register<GameFramework.World.IWorld, LOPWorld>(Lifetime.Singleton);
            builder.Register<ICharacterCreator, CharacterCreator>(Lifetime.Singleton);

            builder.RegisterEntryPoint<PlayerHudCoordinator>();

            builder.Register<StatsViewModel>(Lifetime.Transient);
            builder.Register<StatsView>(Lifetime.Transient);

            builder.Register<CharacterHudViewModel>(Lifetime.Transient);
            builder.Register<CharacterHudView>(Lifetime.Transient);

            builder.Register<GamePadViewModel>(Lifetime.Transient);
            builder.Register<GamePadView>(Lifetime.Transient);
        }

        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<StatsView>(() => container.Resolve<StatsView>()));
            sink.Add(windowManager.RegisterViewFactory<CharacterHudView>(() => container.Resolve<CharacterHudView>()));
            sink.Add(windowManager.RegisterViewFactory<GamePadView>(() => container.Resolve<GamePadView>()));
        }
    }
}
```

> `ICharacterCreator`는 아직 없다 — Task 6에서 만든다. 그때까지 컴파일이 깨지는 게 정상이므로, **이 태스크를 Task 6과 이어서 진행하고 커밋은 Task 6 끝에 한 번에 한다.** 순서를 지키려면 Step 4로 먼저 간다.

- [ ] **Step 4: Task 6의 Step 1~3(`ICharacterCreator` seam)을 먼저 처리하고 돌아온다**

`ICharacterCreator` 인터페이스와 `CharacterCreator`/`EntitySpawner` 수정까지 마친 뒤 이 태스크로 돌아온다.

- [ ] **Step 5: FlapWang 씬의 스코프 컴포넌트를 교체한다**

클라 유니티 에디터에서 `Assets/Scenes/FlapWang.unity`를 연다.

1. 루트 GameObject **`LOPGame`의 이름을 `FlapWang`으로** 바꾼다 (슬라이스 A 리뷰의 후속 항목 — 코드가 이름으로 찾지 않으므로 기능 영향은 없다).
2. `GameLifetimeScope` GameObject에서 **`GameLifetimeScope` 컴포넌트를 제거**하고 **`FlapWangLifetimeScope`를 추가**한다.
3. 인스펙터에서 `runner`(구 `gameEngine`)와 `cameraController` 참조를 **다시 연결**한다. 컴포넌트를 갈아 끼우면 직렬화 참조가 끊긴다 — 비어 있으면 게임이 안 뜬다.
4. 씬을 저장한다.

- [ ] **Step 6: FlapWang이 그대로 도는지 확인한다**

콘솔에 컴파일 에러가 없는지 보고, **Window > General > Test Runner > EditMode 전체를 돌려** 기존 테스트가 모두 PASS인지 확인한다.

그 다음 서버·클라 에디터를 띄워 FlapWang 한 판을 돈다. 확인할 것:
- 캐릭터가 스폰되고 이동·점프가 된다
- 캐릭터 HUD·게임패드·스탯 화면이 뜬다
- DebugHud가 뜬다
- 콘솔에 VContainer 해결 실패(`VContainerException`)가 없다

> 이 확인이 이 슬라이스에서 **회귀를 잡을 수 있는 마지막 시점**이다. 여기서 통과하면 이후 Flappy 배선이 깨져도 FlapWang은 무죄다.

- [ ] **Step 7: 커밋한다** (Task 6 Step 1~3의 파일도 함께)

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/GameplayInstaller.cs Assets/Scripts/Game/GameplayInstaller.cs.meta \
        Assets/Scripts/Game/GameLifetimeScope.cs \
        Assets/Scripts/Game/FlapWangLifetimeScope.cs Assets/Scripts/Game/FlapWangLifetimeScope.cs.meta \
        Assets/Scripts/Entity/ICharacterCreator.cs Assets/Scripts/Entity/ICharacterCreator.cs.meta \
        Assets/Scripts/Entity/CharacterCreator.cs Assets/Scripts/Entity/EntitySpawner.cs \
        Assets/Scenes/FlapWang.unity
git diff --cached --name-only
git commit -m "refactor(game-mode): 게임 스코프를 베이스와 게임별 스코프로 가른다

지금까지 게임 스코프는 한 벌이라 '게임이 하나'라는 전제가 코드에 박혀 있었다.
공통 등록을 GameplayInstaller로 뽑고, 게임마다 달라지는 것(월드·플레이어 몸
생성기·게임 UI)만 파생 스코프가 정하게 한다.

공통 등록을 스펙대로 넷코드/월드코어 둘로 쪼개지는 않았다. Reconciler가
어빌리티·상태이상 스택을 생성자에서 받고 있어서, 지금 쪼개면 어빌리티가 없는
게임이 아예 못 뜬다. 그 얽힘은 Flappy가 자기 입력·예측을 갖는 다음 슬라이스에서 푼다.

등록 내용은 한 줄도 바뀌지 않았다 — 어디에 적혀 있는지만 바뀌었다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: 서버 게임 스코프를 베이스 + Installer로 가르고 룰을 게임별로 만든다

**Files:**
- Create: `서버 Assets/Scripts/Game/GameplayInstaller.cs`, `FlapWangLifetimeScope.cs`, `IGameRuleSystem.cs`
- Modify: `서버 Assets/Scripts/Game/GameLifetimeScope.cs`, `LOPRunner.cs`, `Assets/Scripts/Diagnostics/DebugEnemySpawner.cs`
- Rename: `서버 Assets/Scripts/Game/GameRuleSystem.cs` → `FlapWangRuleSystem.cs`
- Modify: `서버 Assets/Scenes/FlapWang.unity`

**Interfaces:**
- Consumes: 없음 (클라와 독립)
- Produces:
  - `public interface LOP.IGameRuleSystem { void Initialize(); void Deinitialize(); }`
  - `public class LOP.FlapWangRuleSystem : IGameRuleSystem` — 기존 `GameRuleSystem`의 본문 그대로 + `SpawnEnemies(int)`, `DespawnAllEnemies()` 공개 유지
  - `public abstract class LOP.GameLifetimeScope`, `public class LOP.GameplayInstaller : IInstaller`, `public class LOP.FlapWangLifetimeScope : GameLifetimeScope`

- [ ] **Step 1: `IGameRuleSystem`을 만든다**

`서버 Assets/Scripts/Game/IGameRuleSystem.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 게임별 서버 룰 — 누구를 어디에 스폰하고, 무엇으로 점수를 매기고, 언제 끝내는지.
    /// 언리얼의 GameMode에 해당한다. 호스트(Runner)가 초기화·해제만 구동하고 내용은 모른다.
    /// </summary>
    public interface IGameRuleSystem
    {
        void Initialize();
        void Deinitialize();
    }
}
```

- [ ] **Step 2: `GameRuleSystem`을 `FlapWangRuleSystem`으로 rename한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git mv Assets/Scripts/Game/GameRuleSystem.cs Assets/Scripts/Game/FlapWangRuleSystem.cs
git mv Assets/Scripts/Game/GameRuleSystem.cs.meta Assets/Scripts/Game/FlapWangRuleSystem.cs.meta
```

파일 안에서 클래스 선언을 바꾼다 — `public class GameRuleSystem` → `public class FlapWangRuleSystem : IGameRuleSystem`, 생성자 이름도 `FlapWangRuleSystem`으로. XML 주석의 첫 줄을 아래로 바꾼다:

```csharp
    /// <summary>
    /// FlapWang 룰 — 초기 플레이어 생성, 적 스폰(틱 구동), 아이템 획득 시 경험치.
```

기존 본문의 "⚠️ 임시 위치" 문단은 **그대로 둔다** — 룰의 목적지가 World 시스템이라는 사실은 아직 유효하다.

- [ ] **Step 3: 소비자 둘을 고친다**

`서버 Assets/Scripts/Game/LOPRunner.cs`:

```csharp
        [Inject] private IGameRuleSystem gameRuleSystem;
```

`서버 Assets/Scripts/Diagnostics/DebugEnemySpawner.cs`: 이건 FlapWang의 적 스폰을 조절하는 진단 도구라 **구체 타입**을 받는다.

```csharp
        [Inject] private FlapWangRuleSystem gameRuleSystem;
```

- [ ] **Step 4: `GameplayInstaller`(서버)를 만든다**

`서버 Assets/Scripts/Game/GameplayInstaller.cs` — 지금 서버 `GameLifetimeScope.Configure`의 등록 중 **`IWorld`·`CharacterCreator`·`GameRuleSystem`·`RegisterComponent(runner)`·`ITickUpdater` 등록을 뺀 나머지 전부**를 옮긴다:

```csharp
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 게임 덩어리가 게임 종류와 무관하게 공통으로 쓰는 등록(서버).
    /// 게임마다 갈리는 것(월드·플레이어 몸 생성기·룰)은 각 게임 스코프가 따로 넣는다.
    /// </summary>
    public class GameplayInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.EntityRegistry>(Lifetime.Singleton);
            builder.Register<GameFramework.World.WorldEventBuffer>(Lifetime.Singleton);
            builder.Register<GameFramework.World.HealthSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.LevelSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.StatsSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.ManaSystem>(Lifetime.Singleton);
            builder.Register<MovementSystem>(Lifetime.Singleton);
            builder.Register<MotionContributionSystem>(Lifetime.Singleton);
            builder.Register<InputBufferSystem>(Lifetime.Singleton);
            builder.Register<StatusEffectSystem>(Lifetime.Singleton);
            builder.Register<AbilitySystem>(Lifetime.Singleton);
            builder.Register<StatusEffectDataProvider>(Lifetime.Singleton);
            builder.Register<AbilityDataProvider>(Lifetime.Singleton);
            builder.Register<CharacterLoadoutProvider>(Lifetime.Singleton);
            builder.Register<AbilityActivator>(Lifetime.Singleton);
            builder.Register<MatchSeed>(Lifetime.Singleton).AsSelf().As<IMatchSeed>();

            // effect 실행 — executor가 타입별 핸들러로 디스패치. AbilitySystem이 Active 창에서 구동.
            builder.Register<AbilityEffectExecutor>(Lifetime.Singleton);
            builder.Register<IAbilityEffectHandler>(c => new StatusEffectApplyEffectHandler(
                c.Resolve<StatusEffectSystem>(),
                id => c.Resolve<StatusEffectDataProvider>().Get(id),
                c.Resolve<GameFramework.World.EntityRegistry>()), Lifetime.Singleton);
            // DamageEffectHandler = 서버 전용 등록. 클라엔 미등록이라 executor가 DamageEffect를 무시 → 데미지 서버권위.
            // 구체 타입으로 등록(.As) — Func 등록은 ImplementationType이 IAbilityEffectHandler라 다른 Func 핸들러와 충돌.
            builder.Register<DamageEffectHandler>(Lifetime.Singleton).As<IAbilityEffectHandler>();
            builder.Register<KnockbackEffectHandler>(Lifetime.Singleton).As<IAbilityEffectHandler>();
            builder.Register<GameFramework.World.IEventSink, WorldEventSink>(Lifetime.Singleton);
            builder.Register<DeathCascadeSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.Physics.IPhysicsSimulator, GameFramework.Physics.UnityPhysicsSimulator>(Lifetime.Singleton);
            builder.Register<GameFramework.Physics.ICollisionQuery, GameFramework.Physics.UnityCollisionQuery>(Lifetime.Singleton);
            builder.Register<GameFramework.Physics.IOverlapQuery, LOPOverlapQuery>(Lifetime.Singleton);
            // 클라와 동일: 캐릭터를 벽으로(sweep에 Character 포함) + 겹치면 풀 밀어내기(1.0).
            // 클·서 같은 충돌이라야 예측이 맞아 recon이 작다.
            builder.Register<KinematicMoveSystem>(c => new KinematicMoveSystem(
                c.Resolve<GameFramework.Physics.ICollisionQuery>(), LayerMask.GetMask("Default", "Character")), Lifetime.Singleton);
            builder.Register<GameFramework.World.IMotionBridge>(_ => new MotionBridge(
                LayerMask.GetMask("Default"), LayerMask.GetMask("Character"), 1f), Lifetime.Singleton);
            builder.Register<GameFramework.Rng.IRandom, GameFramework.Rng.UnityRandom>(Lifetime.Singleton);
            builder.Register<GameFramework.Runner.IMapLoader, AddressablesMapLoader>(Lifetime.Singleton);
            builder.Register<GameFramework.Netcode.INetworkTime, MirrorNetworkTime>(Lifetime.Singleton);

            // 메시지 핸들러: 컨테이너 엔트리포인트로 자기 구독 생명주기를 스스로 관리(스코프가 Initialize/Dispose 구동).
            builder.RegisterEntryPoint<GameInfoMessageHandler>();
            builder.RegisterEntryPoint<GameEntityMessageHandler>();
            builder.RegisterEntryPoint<GameInputMessageHandler>();
            builder.RegisterEntryPoint<EntityBinder>();   // 서버 뷰 스포너(EntityCreated/EntityDestroyed 반응)

            builder.Register<CombatConfigProvider>(Lifetime.Singleton);
            builder.Register<CombatConfig>(c => c.Resolve<CombatConfigProvider>().Get(), Lifetime.Singleton);
            builder.Register<LOPCombatSystem>(Lifetime.Singleton);
            builder.Register<ItemCreator>(Lifetime.Singleton);
            builder.Register<EntitySpawner>(Lifetime.Singleton);
            builder.Register<ActorRegistry>(Lifetime.Singleton);
            builder.Register<IEntityCreationDataCreator, CharacterCreationDataCreator>(Lifetime.Singleton);
            builder.Register<IEntityCreationDataCreator, ItemCreationDataCreator>(Lifetime.Singleton);
            builder.Register<IEntityCreationDataFactory, EntityCreationDataFactory>(Lifetime.Singleton);

            builder.Register<ServerInputSystem>(Lifetime.Singleton);
            builder.Register<PhysicsSimulationSystem>(Lifetime.Singleton);
            builder.Register<DeathResolveSystem>(Lifetime.Singleton);
            builder.Register<WorldEventDrainSystem>(Lifetime.Singleton);
            builder.Register<InputTimingFeedbackSystem>(Lifetime.Singleton);
            builder.Register<EntitySnapshotBroadcastSystem>(Lifetime.Singleton);
            builder.Register<UserEntitySnapshotSystem>(Lifetime.Singleton);
            builder.Register<DespawnFlushSystem>(Lifetime.Singleton);
        }
    }
}
```

- [ ] **Step 5: 서버 `GameLifetimeScope`를 추상 베이스로 바꾼다**

`서버 Assets/Scripts/Game/GameLifetimeScope.cs`를 통째로 교체한다(서버엔 UI가 없어 클라보다 짧다):

```csharp
using GameFramework.Runner;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 게임 씬의 게임 스코프. EnqueueParent(Room)로 로드되면 Room 자식으로 빌드된다.
    /// 게임마다 무엇이 달라지는지는 파생 스코프가 <see cref="ConfigureGame"/>에서 정한다.
    /// </summary>
    public abstract class GameLifetimeScope : LifetimeScope
    {
        [SerializeField, FormerlySerializedAs("gameEngine")] protected LOPRunner runner;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Install(new GameplayInstaller());

            // runner은 게임 서비스에 의존하므로 부모(Room)가 아닌 이 컨테이너에서 주입돼야 한다.
            builder.RegisterComponent(runner).As<IRunner>();
            // 룰이 sim 서비스로 쓰는 ITickUpdater (runner의 형제 컴포넌트). 호스트 역참조를 피하기 위해 직접 등록.
            builder.Register<ITickUpdater>(_ => runner.GetComponent<ITickUpdater>(), Lifetime.Singleton);

            ConfigureGame(builder);

            builder.RegisterBuildCallback(container =>
            {
                container.InjectSceneObjects(gameObject.scene);
                SceneManager.sceneLoaded += OnSceneLoaded;
            });
        }

        /// <summary>이 게임에서만 쓰는 등록 — 월드, 플레이어 몸 생성기, 룰.</summary>
        protected abstract void ConfigureGame(IContainerBuilder builder);

        protected override void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            base.OnDestroy();
        }

        // Factory가 additive 로드하는 맵 씬도 이 컨테이너로 주입한다.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 자기 씬은 빌드 콜백에서 이미 주입했다. (자기 씬 Awake 중 구독해 자기 sceneLoaded도 수신됨)
            if (scene == gameObject.scene)
            {
                Debug.Log($"[GameLifetimeScope] Skip re-injecting own scene '{scene.name}'; already injected in build callback.");
                return;
            }

            Container.InjectSceneObjects(scene);
        }
    }
}
```

- [ ] **Step 6: `FlapWangLifetimeScope`(서버)를 만든다**

`서버 Assets/Scripts/Game/FlapWangLifetimeScope.cs`:

```csharp
using VContainer;

namespace LOP
{
    /// <summary>FlapWang 덩어리(서버) — 캐릭터 월드와 캐릭터 룰을 쓴다.</summary>
    public class FlapWangLifetimeScope : GameLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.IWorld, LOPWorld>(Lifetime.Singleton);
            builder.Register<ICharacterCreator, CharacterCreator>(Lifetime.Singleton);
            // 진단 도구(DebugEnemySpawner)가 구체 타입을 주입받으므로 둘 다로 등록한다.
            builder.Register<FlapWangRuleSystem>(Lifetime.Singleton).AsSelf().As<IGameRuleSystem>();
        }
    }
}
```

- [ ] **Step 7: Task 6의 Step 4~6(서버 `ICharacterCreator` seam)을 처리하고 돌아온다**

- [ ] **Step 8: 서버 FlapWang 씬의 스코프 컴포넌트를 교체한다**

서버 유니티 에디터에서 `Assets/Scenes/FlapWang.unity`를 연다.

1. 루트 GameObject `LOPGame` → `FlapWang`으로 rename.
2. `GameLifetimeScope` 컴포넌트를 제거하고 `FlapWangLifetimeScope`를 추가한다.
3. `runner` 참조를 다시 연결한다 (`LOPGameEngine` GameObject의 `LOPRunner`).
4. 저장한다.

- [ ] **Step 9: 서버가 그대로 도는지 확인한다**

콘솔에 컴파일 에러가 없는지 보고, 서버·클라를 띄워 FlapWang 한 판을 돈다 — Task 4 Step 6과 같은 확인 목록에 더해 **적이 10초마다 스폰되는지**(`FlapWangRuleSystem.OnTick`)를 본다.

- [ ] **Step 10: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/GameplayInstaller.cs Assets/Scripts/Game/GameplayInstaller.cs.meta \
        Assets/Scripts/Game/GameLifetimeScope.cs \
        Assets/Scripts/Game/FlapWangLifetimeScope.cs Assets/Scripts/Game/FlapWangLifetimeScope.cs.meta \
        Assets/Scripts/Game/IGameRuleSystem.cs Assets/Scripts/Game/IGameRuleSystem.cs.meta \
        Assets/Scripts/Game/FlapWangRuleSystem.cs Assets/Scripts/Game/FlapWangRuleSystem.cs.meta \
        Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Diagnostics/DebugEnemySpawner.cs \
        Assets/Scripts/Entity/ICharacterCreator.cs Assets/Scripts/Entity/ICharacterCreator.cs.meta \
        Assets/Scripts/Entity/CharacterCreator.cs Assets/Scripts/Entity/EntitySpawner.cs \
        Assets/Scenes/FlapWang.unity
git diff --cached --name-only
git commit -m "refactor(game-mode): 서버 게임 스코프를 가르고 룰을 게임별로 만든다

GameRuleSystem은 이름과 달리 FlapWang 하나의 룰이었다. IGameRuleSystem으로
자리를 만들고 본문은 FlapWangRuleSystem으로 옮긴다 — 호스트는 룰을
초기화·해제만 구동하고 내용은 모른다(언리얼 GameMode와 같은 배치).

진단용 DebugEnemySpawner는 FlapWang의 적 스폰을 조절하는 도구라 인터페이스가
아니라 구체 타입을 받는다.

동작은 그대로다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: 플레이어 몸 생성기 seam과 `FlappyBirdCreator`

**Files:**
- Create: `클라 Assets/Scripts/Entity/ICharacterCreator.cs`, `FlappyBirdCreator.cs`
- Create: `서버 Assets/Scripts/Entity/ICharacterCreator.cs`, `FlappyBirdCreator.cs`
- Modify: `클·서 Assets/Scripts/Entity/CharacterCreator.cs`, `EntitySpawner.cs`

**Interfaces:**
- Produces: `public interface LOP.ICharacterCreator { void Create(CharacterCreationData creationData); }` (클·서 각자), `public class LOP.FlappyBirdCreator : ICharacterCreator`
- Consumes: Task 4·5의 스코프가 `ICharacterCreator`를 등록한다.

> **이름 근거.** 언리얼 GameMode의 `DefaultPawnClass`("이 게임에서 플레이어의 몸을 무엇으로 스폰하나")에 대응하는 자리다. 와이어 타입이 `CharacterCreationData`이므로 짝이 맞게 `ICharacterCreator`로 둔다 — 여기서 "character"는 클래스 종류가 아니라 *플레이어가 조종하는 몸*이라는 일반적 뜻이다.
>
> **와이어를 새로 만들지 않는 이유.** 새는 `CharacterCreationData`로 실려 온다. HP·MP·레벨 칸은 0으로 비워 보내고 `FlappyBirdCreator`가 읽지 않는다. 전용 proto를 새로 파면 `EntityCreationData` oneof·`EntityKind`·`EntityBinder` 분기까지 5개 저장소가 함께 움직여야 하는데, 이 슬라이스가 증명하려는 건 *덩어리가 갈리는가*이지 와이어 모양이 아니다.

- [ ] **Step 1: 클라 `ICharacterCreator`를 만든다**

`클라 Assets/Scripts/Entity/ICharacterCreator.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 이 게임에서 플레이어의 몸을 무엇으로 만드는지. 게임 덩어리마다 다른 구현이 끼워진다
    /// (언리얼 GameMode의 DefaultPawnClass에 해당). 데이터만 만들고 뷰는 EntityBinder가 붙인다.
    /// </summary>
    public interface ICharacterCreator
    {
        void Create(CharacterCreationData creationData);
    }
}
```

- [ ] **Step 2: 클라 `CharacterCreator`가 이 인터페이스를 구현하게 한다**

`클라 Assets/Scripts/Entity/CharacterCreator.cs`의 클래스 선언 한 줄만 바꾼다:

```csharp
    public class CharacterCreator : ICharacterCreator
```

- [ ] **Step 3: 클라 `EntitySpawner`가 인터페이스를 받게 한다**

`클라 Assets/Scripts/Entity/EntitySpawner.cs`에서 필드·생성자 파라미터 타입을 바꾼다:

```csharp
        private readonly ICharacterCreator characterCreator;
```

```csharp
        public EntitySpawner(
            GameFramework.World.EntityRegistry entityRegistry,
            ICharacterCreator characterCreator,
            ItemCreator itemCreator,
```

(본문의 `characterCreator.Create(creationData)` 호출은 그대로다.)

→ 여기서 **Task 4 Step 5로 돌아간다.**

- [ ] **Step 4: 서버 `ICharacterCreator`를 만든다**

`서버 Assets/Scripts/Entity/ICharacterCreator.cs` — 클라와 같은 내용(같은 파일을 복사하면 된다. 두 저장소에 같은 개념이 한 벌씩 있는 기존 방식 그대로다).

- [ ] **Step 5: 서버 `CharacterCreator`·`EntitySpawner`를 같은 방식으로 고친다**

`서버 Assets/Scripts/Entity/CharacterCreator.cs`: `public class CharacterCreator : ICharacterCreator`
`서버 Assets/Scripts/Entity/EntitySpawner.cs`: 필드·생성자 파라미터를 `ICharacterCreator`로.

→ 여기서 **Task 5 Step 8로 돌아간다.**

- [ ] **Step 6: 클라 `FlappyBirdCreator`를 만든다**

`클라 Assets/Scripts/Entity/FlappyBirdCreator.cs`:

```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Flappy Race의 플레이어 몸(새)을 만든다. 캐릭터와 달리 체력·마나·레벨·어빌리티가 없다 —
    /// 이 게임에는 그런 개념이 없으므로 안 붙인다.
    /// </summary>
    public class FlappyBirdCreator : ICharacterCreator
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public FlappyBirdCreator(
            IGameDataStore gameDataStore,
            IPlayerContext playerContext,
            GameFramework.World.EntityRegistry entityRegistry)
        {
            this.gameDataStore = gameDataStore;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
        }

        public void Create(CharacterCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            // EntityBinder가 뷰·보간을 붙일 때 보는 값이라 Character로 둔다 — 새도 플레이어가 조종하는 몸이다.
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new MotionContributions());

            if (string.IsNullOrEmpty(creationData.userId) == false)
            {
                worldEntity.Add(new GameFramework.World.Ownership(creationData.userId));
            }

            bool isUserEntity = gameDataStore.userEntityId == creationData.entityId;
            if (isUserEntity)
            {
                worldEntity.Add(new InputBuffer());
            }
            entityRegistry.Add(worldEntity);

            if (isUserEntity)
            {
                playerContext.entityId = creationData.entityId;   // .actor는 EntityBinder가 뷰 생성 후 세팅
            }

            Debug.Log($"[World] Registered flappy bird {worldEntity.Id}");
        }
    }
}
```

> **`Simulated`를 붙이지 않는다.** 클라가 시뮬하는 건 *예측하는 내 몸*인데, B1에는 예측할 움직임이 아직 없다. B2에서 붙인다.
>
> **`MotionContributions`는 붙인다.** 서버 스냅샷이 넉백류 외력 기여를 함께 실어 오고 `Reconciler`가 그걸 이 컴포넌트에 되돌려 놓는다 — 담을 자리가 없으면 그 권위 값이 갈 곳이 없다. B1에선 비어 있는 채로 존재만 한다.

- [ ] **Step 7: 서버 `FlappyBirdCreator`를 만든다**

`서버 Assets/Scripts/Entity/FlappyBirdCreator.cs`:

```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Flappy Race의 플레이어 몸(새)을 만든다(서버). 캐릭터와 달리 체력·마나·레벨·어빌리티가 없다.
    /// </summary>
    public class FlappyBirdCreator : ICharacterCreator
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public FlappyBirdCreator(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        public void Create(CharacterCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new MotionContributions());

            if (string.IsNullOrEmpty(creationData.userId) == false)
            {
                worldEntity.Add(new GameFramework.World.Ownership(creationData.userId));
                worldEntity.Add(new InputBuffer());
            }
            worldEntity.Add(new GameFramework.World.Simulated());   // 서버는 모든 몸을 시뮬한다
            entityRegistry.Add(worldEntity);

            Debug.Log($"[World] Registered flappy bird {worldEntity.Id}");
        }
    }
}
```

- [ ] **Step 8: 두 저장소에 각각 커밋한다**

이 파일들은 Task 7·8에서 스코프에 등록되므로 지금은 아무도 쓰지 않는다 — 컴파일만 통과하면 된다. 콘솔에 에러가 없는지 확인한 뒤:

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Entity/FlappyBirdCreator.cs Assets/Scripts/Entity/FlappyBirdCreator.cs.meta
git diff --cached --name-only
git commit -m "feat(flappy): 새 엔티티 생성기를 추가한다 (클라)

새는 체력·마나·레벨·어빌리티를 갖지 않는다 — Flappy Race에 그런 개념이 없다.
와이어는 CharacterCreationData를 그대로 쓴다: 안 쓰는 칸이 0으로 실려 올 뿐이고,
전용 proto를 새로 파면 oneof·EntityKind·뷰 분기까지 저장소 다섯 개가 함께 움직인다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Entity/FlappyBirdCreator.cs Assets/Scripts/Entity/FlappyBirdCreator.cs.meta
git diff --cached --name-only
git commit -m "feat(flappy): 새 엔티티 생성기를 추가한다 (서버)

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: 클라 FlappyRace 게임 씬과 스코프

**Files:**
- Create: `클라 Assets/Scenes/FlappyRace.unity`
- Create: `클라 Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`
- Modify: `클라 ProjectSettings/EditorBuildSettings.asset`
- Modify: `클라 Assets/AddressableAssetsData/AssetGroups/Scene.asset`, `Character.asset`

**Interfaces:**
- Consumes: `LOP.FlappyWorld`(Task 3), `LOP.FlappyBirdCreator`(Task 6), `GameLifetimeScope`(Task 4)
- Produces: 씬 `Assets/Scenes/FlappyRace.unity` — Task 2의 `TbGameMode.ScenePath`가 이 경로를 가리킨다.

- [ ] **Step 1: `FlappyRaceLifetimeScope`를 만든다**

`클라 Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`:

```csharp
using VContainer;

namespace LOP
{
    /// <summary>Flappy Race 덩어리 — 새 월드와 새 생성기를 쓴다. 게임 UI는 다음 슬라이스.</summary>
    public class FlappyRaceLifetimeScope : GameLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.IWorld, FlappyWorld>(Lifetime.Singleton);
            builder.Register<ICharacterCreator, FlappyBirdCreator>(Lifetime.Singleton);
        }
    }
}
```

- [ ] **Step 2: 게임 씬을 만든다**

클라 유니티 에디터에서 `Assets/Scenes/FlapWang.unity`를 연 뒤 **File > Save As**로 `Assets/Scenes/FlappyRace.unity`에 저장한다. 복제로 만드는 이유는 `LOPRunner`+`LOPTickUpdater` 조합과 스코프 배선을 처음부터 다시 맞추는 것보다 안전해서다.

그 다음 새 씬에서:

1. 루트 GameObject `FlapWang`(Task 4에서 rename한 것) → **`FlappyRace`** 로 rename.
2. 스코프 GameObject에서 `FlapWangLifetimeScope` 컴포넌트를 제거하고 **`FlappyRaceLifetimeScope`** 를 추가한다.
3. 인스펙터에서 `runner` 참조를 다시 연결한다(`LOPGameEngine` GameObject).
4. `CameraController`를 가진 GameObject가 있으면 **컴포넌트만 제거**하고 `Camera`는 남긴다 — `CameraController`는 FlapWang 스코프에서만 등록되므로 이 씬에 남아 있으면 주입이 실패한다.
5. 남은 카메라의 위치를 `(0, 2, -20)`, 회전 `(0, 0, 0)`으로 두어 스폰 지점이 화면에 들어오게 한다.
6. 저장한다.

- [ ] **Step 3: 씬을 빌드 설정에 등록한다**

**File > Build Profiles(또는 Build Settings)** 에서 `Assets/Scenes/FlappyRace.unity`를 목록에 추가한다. `TbGameMode.ScenePath`는 `SceneManager.LoadSceneAsync`가 읽으므로 **빌드 설정에 없으면 로드가 실패**한다(스펙 §6의 표).

- [ ] **Step 4: 맵 씬과 새 프리팹을 Addressable로 등록한다**

**Window > Asset Management > Addressables > Groups**에서:

- `Assets/Art/Scenes/FlappyRaceMap.unity`를 **Scene** 그룹에 넣고, 주소를 **`Assets/Art/Scenes/FlappyRaceMap.unity`** 로 둔다(기존 `FlapWangMap`과 같은 규칙 — 주소가 곧 에셋 경로).
- `Assets/Art/Characters/FlappyBird/Bird.prefab`을 **Character** 그룹에 넣고, 주소를 **`Assets/Art/Characters/FlappyBird/Bird.prefab`** 로 둔다.

맵은 `AddressablesMapLoader`가, 프리팹은 `Appearance.visualId`가 이 주소로 찾는다.

- [ ] **Step 5: 컴파일과 씬 로드를 확인한다**

콘솔에 에러가 없는지 보고, `Assets/Scenes/FlappyRace.unity`를 단독으로 열어 Play한다.
기대: Room 없이 단독 재생이라 매치 데이터가 없어 초기화가 진행되지 않는 게 정상이다. **`FlappyRaceLifetimeScope`가 빌드되는 단계에서 VContainer 해결 실패가 나오지 않아야 한다.**

- [ ] **Step 6: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/FlappyRaceLifetimeScope.cs Assets/Scripts/Game/FlappyRaceLifetimeScope.cs.meta \
        Assets/Scenes/FlappyRace.unity Assets/Scenes/FlappyRace.unity.meta \
        ProjectSettings/EditorBuildSettings.asset \
        Assets/AddressableAssetsData/AssetGroups/Scene.asset Assets/AddressableAssetsData/AssetGroups/Character.asset
git diff --cached --name-only
git commit -m "feat(flappy): FlappyRace 게임 덩어리 씬을 만든다 (클라)

이 씬이 곧 두 번째 게임이다 — 같은 룸 위에서 월드와 플레이어 몸만 갈아끼운다.
게임 씬은 SceneManager가 이름으로 로드하므로 빌드 설정에, 맵 씬과 새 프리팹은
Addressables가 주소로 찾으므로 그룹에 각각 등록했다. 둘은 요건이 다르다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: 서버 FlappyRace 게임 씬·스코프·룰

**Files:**
- Create: `서버 Assets/Scenes/FlappyRace.unity`
- Create: `서버 Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`, `FlappyRaceRuleSystem.cs`
- Modify: `서버 ProjectSettings/EditorBuildSettings.asset`

**Interfaces:**
- Consumes: `IGameRuleSystem`(Task 5), `FlappyBirdCreator`(Task 6), `FlappyWorld`(Task 3)
- Produces: `public class LOP.FlappyRaceRuleSystem : IGameRuleSystem`

- [ ] **Step 1: `FlappyRaceRuleSystem`을 만든다**

`서버 Assets/Scripts/Game/FlappyRaceRuleSystem.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Flappy Race 룰(서버). 지금은 매치 시작 시 참가자마다 새를 하나씩 세우는 일만 한다 —
    /// 결승선·순위·종료 판정은 다음 슬라이스에서 여기에 들어온다.
    /// </summary>
    public class FlappyRaceRuleSystem : IGameRuleSystem
    {
        // 새를 세로로 벌려 놓는 간격. 같은 자리에 겹쳐 세우면 누가 누군지 안 보인다.
        private const float SpawnSpacingY = 2f;
        private const string BirdVisualId = "Assets/Art/Characters/FlappyBird/Bird.prefab";

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;

        public FlappyRaceRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
        }

        public void Initialize()
        {
            var playerList = roomDataStore.match.playerList;
            for (int i = 0; i < playerList.Length; i++)
            {
                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = playerList[i],
                    entityId = entitySpawner.GenerateEntityId(),
                    visualId = BirdVisualId,
                    characterCode = "",
                    position = new Vector3(0f, i * SpawnSpacingY, 0f),
                    rotation = Vector3.zero,
                    velocity = Vector3.zero,
                });
            }
        }

        public void Deinitialize()
        {
        }
    }
}
```

> `characterCode`가 빈 문자열인 건 새가 `TbCharacter`를 안 보기 때문이다(속도·점프력을 스탯에서 읽지 않는다). `FlappyBirdCreator`가 이 값을 쓰지 않으므로 조회도 일어나지 않는다.

- [ ] **Step 2: 스폰 데이터를 와이어로 바꾸는 곳이 새를 감당하게 고친다**

`Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs`가 `World.Entity`를 읽어 클라로 보낼 `CharacterCreationData`를 만든다. `Health`/`Mana`/`Level`/`Stats`는 이미 `?? 0`으로 없어도 되지만 **`MasterDataRef`는 무방비다** — 새는 그 컴포넌트가 없으므로 스폰 즉시 null 참조로 죽는다.

```csharp
                CharacterCode = worldEntity.Get<MasterDataRef>().Code,
```

이 줄을 아래로 바꾼다:

```csharp
                // 마스터데이터로 스탯을 받지 않는 몸(Flappy의 새)은 이 참조가 아예 없다.
                CharacterCode = worldEntity.Get<MasterDataRef>()?.Code ?? "",
```

그리고 `Health`/`Mana`/`Level`/`Stats`가 없다고 찍는 경고 넷은 **마스터데이터를 쓰는 몸일 때만** 찍게 한다. 안 그러면 새가 스폰될 때마다 경고 넷이 쏟아져 진짜 문제를 가린다. 네 개의 `if (xxx == null)` 블록 앞에 이 값을 만들어 두고,

```csharp
            // 마스터데이터로 스탯을 받는 몸만 체력·마나·레벨·스탯을 갖는다 — 새에겐 없는 게 정상이다.
            bool masterDataBacked = worldEntity.Has<MasterDataRef>();
```

각 조건을 `if (masterDataBacked && health == null)` 꼴로 바꾼다(넷 모두).

- [ ] **Step 3: 스폰 데이터 구조를 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server && cat Assets/Scripts/Entity/CharacterCreationData.cs
```

기대: `struct CharacterCreationData : IEntityCreationData`이고 모든 필드가 자동 프로퍼티라 검증이 없다 — Step 1의 초기화 구문이 그대로 컴파일된다. 필드 이름이 다르면 실제 이름에 맞춰 고친다.

- [ ] **Step 4: `FlappyRaceLifetimeScope`(서버)를 만든다**

`서버 Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`:

```csharp
using VContainer;

namespace LOP
{
    /// <summary>Flappy Race 덩어리(서버) — 새 월드·새 생성기·레이스 룰.</summary>
    public class FlappyRaceLifetimeScope : GameLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.IWorld, FlappyWorld>(Lifetime.Singleton);
            builder.Register<ICharacterCreator, FlappyBirdCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, FlappyRaceRuleSystem>(Lifetime.Singleton);
        }
    }
}
```

- [ ] **Step 5: 서버 게임 씬을 만든다**

서버 유니티 에디터에서 `Assets/Scenes/FlapWang.unity`를 연 뒤 **Save As**로 `Assets/Scenes/FlappyRace.unity`에 저장하고:

1. 루트 GameObject → `FlappyRace`로 rename.
2. `FlapWangLifetimeScope` 제거 → `FlappyRaceLifetimeScope` 추가.
3. `runner` 참조를 다시 연결한다.
4. `DebugEnemySpawner`를 가진 GameObject가 있으면 **제거**한다 — 그건 FlapWang 룰의 구체 타입을 주입받으므로 이 씬에선 해결에 실패한다.
5. 저장한다.

- [ ] **Step 6: 씬을 빌드 설정에 등록한다**

서버 **Build Settings**에 `Assets/Scenes/FlappyRace.unity`를 추가한다.

- [ ] **Step 7: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/FlappyRaceLifetimeScope.cs Assets/Scripts/Game/FlappyRaceLifetimeScope.cs.meta \
        Assets/Scripts/Game/FlappyRaceRuleSystem.cs Assets/Scripts/Game/FlappyRaceRuleSystem.cs.meta \
        Assets/Scenes/FlappyRace.unity Assets/Scenes/FlappyRace.unity.meta \
        Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs \
        ProjectSettings/EditorBuildSettings.asset
git diff --cached --name-only
git commit -m "feat(flappy): FlappyRace 게임 덩어리 씬과 레이스 룰을 만든다 (서버)

룰은 아직 참가자마다 새를 하나 세우는 일만 한다. 결승선·순위·종료 판정은
게임별 종료 슬라이스의 몫이다.

스폰 데이터를 와이어로 옮기는 자리가 마스터데이터 참조를 무조건 읽고 있어
새에서 죽었다. 없으면 빈 코드로 보내고, 체력·마나 같은 건 마스터데이터를
쓰는 몸일 때만 없다고 경고한다 — 새에겐 없는 게 정상이다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: 끝-끝 검증 — 두 게임이 실제로 갈리는지 본다

**Files:**
- Modify: `클라 Assets/Scripts/UI/Matchmaking/MatchmakingViewModel.cs`

**Interfaces:**
- Consumes: Task 1~8 전부

- [ ] **Step 1: 게임 선택 하드코딩을 이름 있는 상수로 바꾼다**

`클라 Assets/Scripts/UI/Matchmaking/MatchmakingViewModel.cs`의 43번째 줄 근처:

```csharp
        // 로비에서 게임을 고르는 화면이 생기기 전까지의 임시값(TbGameMode.id).
        // 1 = FlapWang, 6 = FlappyRace.
        private const int TemporaryGameModeId = 1;
```

그리고 대입부:

```csharp
            _matchmakingDataStore.gameModeId = TemporaryGameModeId;
```

- [ ] **Step 2: FlapWang이 여전히 도는지 확인한다 (회귀)**

`TemporaryGameModeId = 1`인 채로 서버·클라 에디터를 띄우고 한 판 돈다.

확인:
- 캐릭터 스폰, 이동·점프, 공격
- 캐릭터 HUD·게임패드·스탯·DebugHud
- 적이 10초마다 스폰
- 콘솔에 `VContainerException`·NRE 없음

여기서 깨지면 **Flappy 쪽을 보지 말고 Task 4·5로 돌아간다.**

- [ ] **Step 3: FlappyRace로 바꿔 입장한다**

`TemporaryGameModeId = 6`으로 고치고 다시 한 판 돈다.

확인:
- 클라 콘솔에 `Assets/Scenes/FlappyRace.unity` 로드 로그
- 맵으로 `Assets/Art/Scenes/FlappyRaceMap.unity`(파이프 코스)가 뜬다
- `[World] Registered flappy bird 1` 로그가 클·서 양쪽에 뜬다
- 화면에 새가 보인다 (움직이지 않는 게 정상 — 이동은 다음 슬라이스)
- 캐릭터 HUD·게임패드가 **안 뜬다** (FlapWang 전용 UI라 정상)
- 콘솔에 `VContainerException`이 없다

- [ ] **Step 4: 관측한 것을 기록한다**

아래는 **이 슬라이스가 만든 문제가 아니라 이미 있던 것**이라 고치지 않고 기록만 한다. 실제로 나오는지 확인해 결과를 spec에 남긴다.

- **서버에는 맵 에셋이 없다.** 서버 프로젝트에 `Assets/Art`가 아예 없어 `AddressablesMapLoader.LoadAsync`가 맵을 못 찾는다. FlapWangMap도 마찬가지 상황이므로 Flappy에서 새로 생기는 문제가 아니다. 서버 콘솔에 Addressables 오류가 나오는지 보고, 나오면 **B2에서 반드시 다뤄야 할 항목**으로 기록한다(새가 바닥·파이프와 부딪히려면 서버에 지오메트리가 있어야 한다).
- **매치를 두 번 이상 돌 때 씬이 쌓이는지.** 슬라이스 A 리뷰의 후속 항목(`DestroyAsync`가 경로로 씬을 다시 찾는 문제)이 두 게임을 오갈 때 드러날 수 있다.

- [ ] **Step 5: `TemporaryGameModeId`를 1로 되돌리고 커밋한다**

기본값은 FlapWang으로 둔다 — 아직 넷코드 검증 베드가 FlapWang이다.

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/UI/Matchmaking/MatchmakingViewModel.cs
git diff --cached --name-only
git commit -m "chore(game-mode): 임시 게임 선택값을 이름 있는 상수로 뺀다

로비 선택 화면이 생기기 전까지는 이 상수를 바꿔 게임을 바꾼다.
기본값은 FlapWang이다 — 아직 넷코드 검증 베드가 그쪽이다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 6: spec에 슬라이스 B1 결과를 남긴다**

`docs/superpowers/specs/2026-08-15-game-mode-axis-design.md`의 §11 슬라이스 표에서 B 행을 B1/B2로 나누고, B1을 완료로 표시한다. §14 Open Decisions 아래에 "슬라이스 B1에서 관측한 것" 절을 만들어 Step 4의 결과를 적는다. 스펙과 달라진 두 가지(`id=6`, Installer 한 벌)도 여기에 남긴다.

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add docs/superpowers/specs/2026-08-15-game-mode-axis-design.md docs/superpowers/plans/2026-08-15-game-mode-axis-slice-b1.md
git diff --cached --name-only
git commit -m "docs: 슬라이스 B1 계획과 결과를 남긴다

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## 완료 기준

- [ ] `TemporaryGameModeId = 1` → FlapWang이 슬라이스 전과 똑같이 돈다
- [ ] `TemporaryGameModeId = 6` → FlappyRace 게임 씬 + Flappy 맵 + 새가 뜬다
- [ ] 두 게임에서 콘솔에 VContainer 해결 실패가 없다
- [ ] LOP-Shared EditMode 테스트 전체 PASS
- [ ] 8개 저장소 각각 `feature/game-mode-axis-b1`(아트는 `feature/flappy-race-map`)에 커밋이 남아 있고, 사용자의 미커밋 작업물은 하나도 스테이징되지 않았다

## 다음 슬라이스(B2)에 넘기는 것

- `FlappyMoveSystem` — 고정 전진 + 플랩 + 중력 (프로토 튜닝값: `forwardSpeed=7`, `flapImpulse=8`, `gravity=24`, `maxFall=16`)
- 플랩 입력 배선 (`InputCommand.Jump`) + 클라 예측/롤백 + `Simulated` 부착
- 새끼리 몸싸움(밀어내기 + 세로속도 교환)
- 서버에 맵 지오메트리를 어떻게 줄지 (Task 9 Step 4의 관측 결과에 달렸다)
- 공통 등록을 넷코드/월드코어로 쪼개기 — `Reconciler`의 어빌리티 의존을 푸는 것과 한 몸이다
