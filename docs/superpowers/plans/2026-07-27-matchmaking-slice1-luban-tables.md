# 매치메이킹 슬라이스 1 — Luban 테이블 신설 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 게임·맵·큐 마스터데이터를 Luban 단일 진실원본으로 신설하고, 매치메이킹 서버의 자체 XML 로더를 Luban 생성 TypeScript + JSON으로 대체한다. **런타임 동작 변경 0.**

**Architecture:** Excel(`infrastructure/table/Datas/`) → Luban → 세 갈래 출력. 클라·게임서버는 기존대로 `cs-bin`(`.cs` + `.bytes`), 매치메이킹 서버는 신규 `matchmaking` 타깃으로 `typescript-json`(`schema.ts`) + `json`. 비기본 그룹 `m`을 새로 만들어 매칭 서버가 **자기에게 필요한 3개 테이블만** 받게 한다.

**Tech Stack:** Luban 4.9.0 (`infrastructure/table/tools/Luban/Luban.dll`), Python openpyxl(Excel 저작), Unity EditMode(NUnit), Node 22 + TypeScript 5.7 + jest(신규)

## Global Constraints

- **기본키는 정수 `id`** + `code`(식별 문자열) / `name`(표시) 컬럼. 프로젝트 규약 `masterdata-key-convention`.
- **표시용 컬럼(`name`, `description`)은 `##group`을 `c`** 로 둔다 — 서버·매칭 타깃 산출물에서 빠진다(기존 `Ability.description` 선례).
- **새 Luban enum을 만들지 않는다.** 선택 정책은 `string` 컬럼(`"Player"` / `"Server"`) — 기존 `target_type` / `duration_policy` / `stack_policy` 선례와 동일.
- **런타임 동작 변경 0.** 기존 5개 서브게임을 값 그대로 이관한다(전부 `min_players=2`, `max_players=8`). 매칭 결과가 달라지면 이 슬라이스는 실패다.
- **`LOPMasterData.TableFiles` 갱신 필수** — 누락 시 Entrance에서 `KeyNotFoundException`. `TableFileManifestTests`가 지킨다(`masterdata-new-table-checklist`).
- **`.meta` 파일은 Unity가 생성한 것만 커밋한다.** `gen.sh` 직후에는 아직 없으므로, Unity 에디터가 재스캔한 뒤 `git add` 한다. 직접 만들지 않는다.
- **`AvailableMatchType`(기존 XML 필드)은 이관하지 않는다** — 읽는 코드가 0이고(`grep` 확인), 새 모델에서는 `TbQueue.allowed_game_mode_ids`가 그 역할을 반대 방향으로 수행한다.
- 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`.
- **main 직접 커밋 금지.** 각 저장소에서 피처 브랜치로 작업한다.

**저장소 경로 (절대):**

| 별칭 | 경로 |
|---|---|
| `INFRA` | `C:/Users/re5na/workspace/LOP/infrastructure` |
| `MD_C` | `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client` |
| `MD_S` | `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server` |
| `MM` | `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer/MatchmakingServer` |

---

## 파일 구조

| 파일 | 책임 |
|---|---|
| `INFRA/table/Datas/#GameMode.xlsx` | 게임 정의 + **정원(min/max)** |
| `INFRA/table/Datas/#Map.xlsx` | 맵 정의. `game_mode_id`로 게임에 종속 + `scene_path` |
| `INFRA/table/Datas/#Queue.xlsx` | 큐 정책 — 실력 폭·확장률·랭크 표시·선택 주체·허용 게임·최대 대기 |
| `INFRA/table/Datas/__tables__.xlsx` | 위 3개 테이블 등록(그룹 `c,s,m`) |
| `INFRA/table/luban.conf` | 그룹 `m` + 타깃 `matchmaking` 추가 |
| `INFRA/table/gen.sh` / `gen.bat` | 3번째 생성 단계 추가 |
| `MD_C`/`MD_S` `Runtime/Scripts/LOPMasterData.cs` | `TableFiles`에 3개 stem 추가 |
| `MM/src/loaders/generated/schema.ts` | **Luban 생성물.** 손으로 고치지 않음 |
| `MM/master_data/*.json` | **Luban 생성물.** 기존 `master_data/sub_game_data/*.xml`을 대체 |
| `MM/src/loaders/masterdata.loader.ts` | XML 스캔 → 생성 `Tables` 구성으로 재작성 |
| `MM/jest.config.js`, `MM/src/loaders/__tests__/masterdata.loader.test.ts` | 신규 테스트 인프라 + 첫 테스트 |

---

## Task 1: Luban 3테이블 저작 + `matchmaking` 타깃 추가 + 생성

**Files:**
- Create: `INFRA/table/Datas/#GameMode.xlsx`, `#Map.xlsx`, `#Queue.xlsx`
- Modify: `INFRA/table/Datas/__tables__.xlsx`
- Modify: `INFRA/table/luban.conf`
- Modify: `INFRA/table/gen.sh`, `INFRA/table/gen.bat`
- 생성물(커밋 대상): `MD_C/Runtime.Generated/**`, `MD_S/Runtime.Generated/**`, `MM/src/loaders/generated/schema.ts`, `MM/master_data/*.json`

**Interfaces:**
- Consumes: 없음 (첫 태스크)
- Produces — 이후 태스크가 의존하는 정확한 이름:
  - 테이블 stem(파일명): `tbgamemode`, `tbmap`, `tbqueue`
  - C# / TS 접근자: `Tables.TbGameMode`, `Tables.TbMap`, `Tables.TbQueue`
  - TS 로더 시그니처: `type JsonLoader = (file: string) => any`, `new Tables(loader)`
  - TS 테이블 API: `get(key: number)`, `getDataList()`, `getDataMap()`
  - `GameMode` 필드(TS, camelCase 변환됨): `id`, `code`, `minPlayers`, `maxPlayers` (`name`/`description`은 group `c`라 매칭 산출물에 **없음**)
  - `Queue` 필드: `id`, `code`, `ratingRangeStart`, `ratingRangeMax`, `ratingRelaxPerSec`, `hasVisibleRank`, `gameModeSelector`, `mapSelector`, `allowedGameModeIds: number[]`, `maxWaitSeconds`
  - `Map` 필드: `id`, `gameModeId`, `code`, `scenePath`

- [ ] **Step 1: 피처 브랜치 생성 (4개 저장소)**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure && git checkout -b feature/matchmaking-slice1-luban-tables
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client && git checkout -b feature/matchmaking-slice1-luban-tables
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server && git checkout -b feature/matchmaking-slice1-luban-tables
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer && git checkout -b feature/matchmaking-slice1-luban-tables
```

- [ ] **Step 2: Excel 3개 저작 + `__tables__` 등록**

`INFRA/table/`에서 아래 스크립트를 실행한다. Excel을 손으로 열지 말 것 — 인코딩·서식이 깨진다.

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table && python << 'PYEOF'
import openpyxl
from openpyxl import Workbook

def make(path, rows):
    wb = Workbook(); ws = wb.active
    for r in rows: ws.append(r)
    wb.save(path)

# 기존 master_data/sub_game_data/*.xml 5종을 값 그대로 이관 (전부 min 2 / max 8)
make('Datas/#GameMode.xlsx', [
 ['##var','id','code','name','description','min_players','max_players'],
 ['##type','int','string','string','string','int','int'],
 ['##group','','','c','c','',''],
 ['##','id','code','name','description','min_players','max_players'],
 [None,1,'FlapWang','플랩왕','',2,8],
 [None,2,'Dodgeball','닷지볼','',2,8],
 [None,3,'ObserverAvoid','관찰자 피하기','',2,8],
 [None,4,'RememberGame','기억력 게임','',2,8],
 [None,5,'TargetShooting','타겟 슈팅','',2,8],
])

# 현재 게임 서버가 하드코딩한 맵 하나(LOPRunner.MapId)를 데이터로 옮겨 적는다.
# 이 슬라이스에서는 아직 아무도 scene_path를 읽지 않는다(슬라이스 2에서 배선).
make('Datas/#Map.xlsx', [
 ['##var','id','game_mode_id','code','name','scene_path'],
 ['##type','int','int','string','string','string'],
 ['##group','','','','c',''],
 ['##','id','game_mode_id','code','name','scene_path'],
 [None,1,1,'FlapWangMap','플랩왕 맵','Assets/Art/Scenes/FlapWangMap.unity'],
])

make('Datas/#Queue.xlsx', [
 ['##var','id','code','name','rating_range_start','rating_range_max','rating_relax_per_sec','has_visible_rank','game_mode_selector','map_selector','allowed_game_mode_ids#sep=,','max_wait_seconds'],
 ['##type','int','string','string','int','int','int','bool','string','string','list,int','int'],
 ['##group','','','c','','','','','','','',''],
 ['##','id','code','name','rating_range_start','rating_range_max','rating_relax_per_sec','has_visible_rank','game_mode_selector','map_selector','allowed_game_mode_ids','max_wait_seconds'],
 [None,1,'Casual','친선전',500,2000,50,False,'Player','Player','1,2,3,4,5',30],
 [None,2,'Ranked','랭킹전',100,400,10,True,'Server','Server','1,2,3,4,5',60],
])

# __tables__ 에 3행 추가 (group 'c,s,m' = 클라·게임서버·매칭서버 모두)
p='Datas/__tables__.xlsx'
wb=openpyxl.load_workbook(p); ws=wb.worksheets[0]
hdr=[c.value for c in ws[1]]
def col(n): return hdr.index(n)+1
for full,vt,inp,cmt in [
    ('TbGameMode','GameMode','#GameMode.xlsx','GameMode'),
    ('TbMap','Map','#Map.xlsx','Map'),
    ('TbQueue','Queue','#Queue.xlsx','Queue'),
]:
    r=ws.max_row+1
    ws.cell(r,col('full_name')).value=full
    ws.cell(r,col('value_type')).value=vt
    ws.cell(r,col('read_schema_from_file')).value=True
    ws.cell(r,col('input')).value=inp
    ws.cell(r,col('index')).value='id'
    ws.cell(r,col('mode')).value='map'
    ws.cell(r,col('group')).value='c,s,m'
    ws.cell(r,col('comment')).value=cmt
wb.save(p)
print('OK: 3 tables authored + registered')
PYEOF
```

Expected: `OK: 3 tables authored + registered`

- [ ] **Step 3: `luban.conf`에 그룹 `m` + 타깃 `matchmaking` 추가**

`INFRA/table/luban.conf` 전체를 아래로 교체한다. 추가된 것은 `groups`의 `m` 한 줄과 `targets`의 `matchmaking` 한 줄뿐이다.

```json
{
    "groups": [
        { "names": ["c"], "default": true },
        { "names": ["s"], "default": true },
        { "names": ["m"], "default": false }
    ],
    "schemaFiles": [
        { "fileName": "Datas/__tables__.xlsx", "type": "table" },
        { "fileName": "Datas/__beans__.xlsx",  "type": "bean"  },
        { "fileName": "Datas/__enums__.xlsx",  "type": "enum"  }
    ],
    "dataDir": "Datas",
    "targets": [
        { "name": "client", "manager": "Tables", "groups": ["c"], "topModule": "LOP.MasterData" },
        { "name": "server", "manager": "Tables", "groups": ["s"], "topModule": "LOP.MasterData" },
        { "name": "matchmaking", "manager": "Tables", "groups": ["m"], "topModule": "LOP.MasterData" }
    ],
    "xargs": ["tableImporter.name=none"]
}
```

> `m`이 `"default": false`인 것이 핵심이다. 그래서 그룹을 비워 둔 기존 테이블(캐릭터·어빌리티 등)은 매칭 타깃에 **안 들어가고**, `c,s,m`으로 명시한 3개만 들어간다.

- [ ] **Step 4: `gen.sh`에 3번째 단계 추가**

`INFRA/table/gen.sh`에서 `MM_PKG` 변수를 추가하고, `echo "[done]"` **앞에** 매칭 블록을 넣는다.

기존 6번째 줄 아래에 추가:

```bash
MM_PKG="../../LeagueOfPhysical-MatchmakingServer/MatchmakingServer"
```

`echo "[done]"` 앞에 추가:

```bash
echo "[gen] target=matchmaking -> MatchmakingServer"
rm -rf "$MM_PKG/src/loaders/generated" "$MM_PKG/master_data"
dotnet "$LUBAN" -t matchmaking -c typescript-json -d json --conf luban.conf \
  -x outputCodeDir="$MM_PKG/src/loaders/generated" \
  -x outputDataDir="$MM_PKG/master_data"
```

- [ ] **Step 5: `gen.bat`에 같은 단계 추가 (Windows 병행 유지)**

`SERVER_PKG` 줄 아래에 추가:

```bat
set MM_PKG=..\..\LeagueOfPhysical-MatchmakingServer\MatchmakingServer
```

`echo [done]` 앞에 추가:

```bat
echo [gen] target=matchmaking -^> MatchmakingServer
if exist "%MM_PKG%\src\loaders\generated" rmdir /s /q "%MM_PKG%\src\loaders\generated"
if exist "%MM_PKG%\master_data" rmdir /s /q "%MM_PKG%\master_data"
dotnet %LUBAN% -t matchmaking -c typescript-json -d json --conf luban.conf ^
  -x outputCodeDir=%MM_PKG%\src\loaders\generated ^
  -x outputDataDir=%MM_PKG%\master_data
```

- [ ] **Step 6: 생성 실행**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table && ./gen.sh
```

Expected: `[gen] target=client` / `[gen] target=server` / `[gen] target=matchmaking` / `[done]`, 에러 0.

> ⚠️ `gen.sh`가 `master_data` 폴더를 통째로 지운다. 기존 `master_data/sub_game_data/*.xml`이 이 시점에 사라진다 — 의도된 동작이고, Task 5에서 그 삭제를 커밋한다.

- [ ] **Step 7: 산출물 검증**

```bash
cd /c/Users/re5na/workspace/LOP
ls LeagueOfPhysical-MasterData-Client/Runtime.Generated/StreamingAssets/MasterData/ | grep -E "tbgamemode|tbmap|tbqueue"
ls LeagueOfPhysical-MasterData-Server/Runtime.Generated/StreamingAssets/MasterData/ | grep -E "tbgamemode|tbmap|tbqueue"
ls LeagueOfPhysical-MatchmakingServer/MatchmakingServer/master_data/
cat LeagueOfPhysical-MatchmakingServer/MatchmakingServer/master_data/tbqueue.json
```

Expected:
- 클라·서버 각각 `tbgamemode.bytes` / `tbmap.bytes` / `tbqueue.bytes` 3개
- 매칭 서버 `master_data/`에 **정확히** `tbgamemode.json`, `tbmap.json`, `tbqueue.json` 3개 (다른 테이블이 섞이면 그룹 설정이 잘못된 것)
- `tbqueue.json`에 `"allowed_game_mode_ids": [1,2,3,4,5]` 배열이 있고 **`name` 키는 없다**(group `c`라 제외됨)

- [ ] **Step 8: 매칭 서버 TypeScript 컴파일 확인**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer/MatchmakingServer && npm run build
```

Expected: 에러 0. (생성 `schema.ts`가 `src/` 아래라 `tsconfig.json`의 `include: ["src/**/*"]`에 자동 포함된다.)

- [ ] **Step 9: Unity가 `.meta`를 만들도록 재스캔 후 커밋**

클라·서버 Unity 에디터에 포커스를 주어 재임포트를 유발한다. 그 다음:

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client && git status --short
```

Expected: 새 `.cs` / `.bytes`와 **짝이 되는 `.meta`가 함께** 보인다. `.meta`가 안 보이면 Unity 재스캔이 아직 안 끝난 것이니 기다린다.

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure && git add table/ && git commit -m "$(cat <<'EOF'
feat(masterdata): GameMode/Map/Queue 테이블 신설 + matchmaking 타깃 추가

매치메이킹 마스터데이터를 Luban 단일 진실원본으로 통합. 매칭 서버 전용
비기본 그룹 m을 만들어 3개 테이블만 typescript-json + json으로 뽑는다.
기존 sub_game_data XML 5종을 값 그대로 이관(전부 min 2 / max 8) — 동작 무변화.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client && git add -A && git commit -m "$(cat <<'EOF'
chore(gen): TbGameMode/TbMap/TbQueue 생성물 반영

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server && git add -A && git commit -m "$(cat <<'EOF'
chore(gen): TbGameMode/TbMap/TbQueue 생성물 반영

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

매칭 서버 생성물은 Task 5에서 XML 삭제와 함께 커밋한다(같은 변경의 양면이라 한 커밋이 읽기 좋다).

---

## Task 2: `TableFiles` 등록 (클·서 MasterData 패키지)

**Files:**
- Modify: `MD_C/Runtime/Scripts/LOPMasterData.cs:24-28`
- Modify: `MD_S/Runtime/Scripts/LOPMasterData.cs` (같은 배열)
- Test: `MD_C/Tests/EditMode/TableFileManifestTests.cs` (기존 — 수정 없음)

**Interfaces:**
- Consumes: Task 1의 테이블 stem `tbgamemode` / `tbmap` / `tbqueue`
- Produces: 게임 실행 시 3개 테이블이 실제로 로드됨 (`LOPMasterData.Tables.TbQueue` 등 접근 가능)

- [ ] **Step 1: 기존 테스트를 돌려 실패를 확인한다**

`TableFileManifestTests`는 *패키지가 싣고 오는 `.bytes`* 와 *`TableFiles` 배열*이 일치하는지 검사한다. Task 1이 `.bytes` 3개를 늘렸으므로 지금은 **실패해야 한다.**

UnityMCP로 실행 (CLAUDE.md 규약대로 `unity_instance`를 매번 명시):

```
run_tests(mode="EditMode", test_filter="TableFileManifestTests",
          unity_instance="LeagueOfPhysical-Client@<hash>")
```

`<hash>`는 `mcpforunity://instances`에서 `LeagueOfPhysical-Client` 인스턴스의 `id`로 확인한다.

Expected: **FAIL** — "패키지에 있으나 TableFiles에 없는 테이블: tbgamemode, tbmap, tbqueue" 취지의 메시지.

- [ ] **Step 2: 클라 `TableFiles`에 3개 추가**

`MD_C/Runtime/Scripts/LOPMasterData.cs`의 배열을 아래로 교체:

```csharp
        public static readonly System.Collections.Generic.IReadOnlyList<string> TableFiles = new[]
        {
            "tbcharacter", "tbskin", "tbskinasset", "tbitem", "tbstatuseffect", "tbability",
            "tbcharacterloadout", "tbabilityview", "tbstatuseffectview",
            "tbgamemode", "tbmap", "tbqueue"
        };
```

- [ ] **Step 3: 서버 `TableFiles`에도 같은 3개 추가**

`MD_S/Runtime/Scripts/LOPMasterData.cs`의 배열 끝에 `"tbgamemode", "tbmap", "tbqueue"`를 추가한다. 서버 배열은 클라와 항목이 다를 수 있으므로(`tbskinasset`·`tbabilityview`·`tbstatuseffectview`는 group `c`) **기존 항목을 지우지 말고 3개만 덧붙인다.**

- [ ] **Step 4: 테스트 재실행 — 클라·서버 양쪽**

```
run_tests(mode="EditMode", test_filter="TableFileManifestTests",
          unity_instance="LeagueOfPhysical-Client@<hash>")
run_tests(mode="EditMode", test_filter="TableFileManifestTests",
          unity_instance="LeagueOfPhysical-Server@<hash>")
```

Expected: 양쪽 **PASS**.

- [ ] **Step 5: 전체 EditMode 회귀**

```
run_tests(mode="EditMode", unity_instance="LeagueOfPhysical-Client@<hash>")
```

Expected: 이전 기준선(354) + 변동 없음, 실패 0. 테이블만 늘었으므로 기존 테스트에 영향이 없어야 한다.

- [ ] **Step 6: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client && git add -A && git commit -m "$(cat <<'EOF'
fix(masterdata): TableFiles에 tbgamemode/tbmap/tbqueue 등록

누락 시 Entrance에서 KeyNotFoundException. TableFileManifestTests가 지킨다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server && git add -A && git commit -m "$(cat <<'EOF'
fix(masterdata): TableFiles에 tbgamemode/tbmap/tbqueue 등록

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: 마스터데이터 무결성 테스트 (TDD)

**Files:**
- Create: `MD_C/Tests/EditMode/MatchmakingDataIntegrityTests.cs`

**Interfaces:**
- Consumes: Task 1의 `Tables.TbGameMode` / `TbMap` / `TbQueue`, Task 2의 로드 경로
- Produces: 없음 (테스트 전용)

> spec §9가 요구하는 검사다. 데이터가 서로를 참조하는데(큐→게임, 맵→게임) 그 참조가 깨져도
> 컴파일은 통과한다. 런타임에 매칭 도중 터지는 대신 여기서 잡는다. 기존
> `AbilityDataIntegrityTests`와 같은 자리·같은 패턴이다.

- [ ] **Step 1: 실패하는 테스트를 먼저 쓴다**

Create `MD_C/Tests/EditMode/MatchmakingDataIntegrityTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace LOP.MasterData.Tests
{
    /// <summary>
    /// 큐·맵이 가리키는 게임 id가 실제로 존재하는지, 큐 정책 문자열이 유효한지 검사.
    /// 깨져도 컴파일은 통과하므로(데이터라서) 여기서 잡지 않으면 매칭 도중 터진다.
    /// </summary>
    public class MatchmakingDataIntegrityTests
    {
        // 선택 주체는 Luban enum이 아니라 string 컬럼이다(target_type 선례).
        // 어셈블리 경계 때문에 여기서 값을 못 박아 둔다 — 정책 값이 늘면 함께 갱신할 것.
        private static readonly HashSet<string> ValidSelectors = new() { "Player", "Server" };

        private Tables LoadTables()
        {
            var masterData = new LOPMasterData();
            masterData.LoadAsync().GetAwaiter().GetResult();
            return masterData.Tables;
        }

        [Test]
        public void 큐가_허용한_게임_id가_모두_존재한다()
        {
            var tables = LoadTables();
            var gameModeIds = tables.TbGameMode.DataList.Select(x => x.Id).ToHashSet();

            foreach (var queue in tables.TbQueue.DataList)
            {
                Assert.IsNotEmpty(queue.AllowedGameModeIds,
                    $"큐 {queue.Code}(id={queue.Id})의 허용 게임 목록이 비었다 — 아무도 매칭될 수 없다.");

                foreach (var id in queue.AllowedGameModeIds)
                {
                    Assert.IsTrue(gameModeIds.Contains(id),
                        $"큐 {queue.Code}가 없는 게임 id {id}를 가리킨다.");
                }
            }
        }

        [Test]
        public void 맵이_가리키는_게임_id가_존재한다()
        {
            var tables = LoadTables();
            var gameModeIds = tables.TbGameMode.DataList.Select(x => x.Id).ToHashSet();

            foreach (var map in tables.TbMap.DataList)
            {
                Assert.IsTrue(gameModeIds.Contains(map.GameModeId),
                    $"맵 {map.Code}(id={map.Id})가 없는 게임 id {map.GameModeId}를 가리킨다.");
            }
        }

        [Test]
        public void 큐의_선택_주체_값이_유효하다()
        {
            var tables = LoadTables();

            foreach (var queue in tables.TbQueue.DataList)
            {
                Assert.IsTrue(ValidSelectors.Contains(queue.GameModeSelector),
                    $"큐 {queue.Code}의 game_mode_selector가 '{queue.GameModeSelector}' — Player/Server만 유효.");
                Assert.IsTrue(ValidSelectors.Contains(queue.MapSelector),
                    $"큐 {queue.Code}의 map_selector가 '{queue.MapSelector}' — Player/Server만 유효.");
            }
        }

        [Test]
        public void 게임의_정원이_말이_된다()
        {
            var tables = LoadTables();

            foreach (var gameMode in tables.TbGameMode.DataList)
            {
                Assert.Greater(gameMode.MinPlayers, 0,
                    $"게임 {gameMode.Code}의 최소 인원이 0 이하다.");
                Assert.GreaterOrEqual(gameMode.MaxPlayers, gameMode.MinPlayers,
                    $"게임 {gameMode.Code}의 최대 인원이 최소보다 작다.");
            }
        }
    }
}
```

- [ ] **Step 2: 테스트 실행**

```
run_tests(mode="EditMode", test_filter="MatchmakingDataIntegrityTests",
          unity_instance="LeagueOfPhysical-Client@<hash>")
```

Expected: **4 passed.** Task 1의 데이터가 올바르므로 처음부터 통과한다.

> 만약 컴파일 에러가 나면 생성 코드의 접근자 이름이 다르다는 뜻이다. `MD_C/Runtime.Generated/Scripts/MasterData/`의
> `Tables.cs`와 `Queue.cs`를 열어 실제 프로퍼티 이름(`DataList` / `AllowedGameModeIds` 등)을 확인하고 맞춘다.
> 기존 `AbilityDataIntegrityTests`가 같은 세대의 생성물을 어떻게 읽는지가 가장 정확한 본보기다.

- [ ] **Step 3: 일부러 깨뜨려 테스트가 실제로 잡는지 확인**

`INFRA/table/Datas/#Queue.xlsx`의 `Casual` 행 `allowed_game_mode_ids`를 `1,2,3,4,99`로 바꾸고 `./gen.sh` 후 테스트를 다시 돌린다.

Expected: `큐 Casual가 없는 게임 id 99를 가리킨다.` 로 **FAIL**.

확인했으면 `1,2,3,4,5`로 되돌리고 `./gen.sh`를 다시 돌려 통과 상태로 복구한다.

- [ ] **Step 4: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client && git add -A && git commit -m "$(cat <<'EOF'
test(masterdata): 큐/맵/게임 참조 무결성 EditMode 테스트

큐→게임, 맵→게임 참조가 깨져도 컴파일은 통과하므로 데이터 레벨에서 잡는다.
선택 주체 문자열과 정원 범위도 함께 검사. AbilityDataIntegrityTests와 같은 패턴.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: 매칭 서버 테스트 인프라 + Luban 로더 (TDD)

**Files:**
- Create: `MM/jest.config.js`
- Create: `MM/src/loaders/__tests__/masterdata.loader.test.ts`
- Modify: `MM/package.json` (devDependencies + `test` 스크립트)
- Modify: `MM/src/loaders/masterdata.loader.ts` (전면 재작성)

**Interfaces:**
- Consumes: Task 1의 `MM/src/loaders/generated/schema.ts` (`Tables`, `JsonLoader`), `MM/master_data/*.json`
- Produces — Task 5가 쓰는 것:
  - `export function getTables(): Tables` — 로드된 테이블 접근자. `load()` 전에 호출하면 throw
  - `export async function load(): Promise<void>` — 기존 이름·시그니처 유지(`loaders/index.ts`가 그대로 호출)
  - `export function findGameModeByCode(code: string): GameMode | undefined` — 슬라이스 2 이전까지 남는 문자열 키 호환용

> 이 저장소에는 테스트 인프라가 **없다.** 슬라이스 4(MatchFunction·Evaluator)가 단위 테스트를 요구하므로 여기서 최소 구성을 깔고 첫 테스트를 그 위에 얹는다.

- [ ] **Step 1: jest 설치**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer/MatchmakingServer
npm install --save-dev jest@^29 ts-jest@^29 @types/jest@^29
```

- [ ] **Step 2: `jest.config.js` 생성**

`tsconfig.json`의 경로 별칭을 jest도 알아야 한다. 별칭이 빠지면 `Cannot find module '@src/...'`로 죽는다.

```javascript
/** @type {import('ts-jest').JestConfigWithTsJest} */
module.exports = {
    preset: 'ts-jest',
    testEnvironment: 'node',
    rootDir: '.',
    testMatch: ['<rootDir>/src/**/__tests__/**/*.test.ts'],
    moduleNameMapper: {
        '^@src/(.*)$': '<rootDir>/src/$1',
        '^@controllers/(.*)$': '<rootDir>/src/controllers/$1',
        '^@exceptions/(.*)$': '<rootDir>/src/exceptions/$1',
        '^@interfaces/(.*)$': '<rootDir>/src/interfaces/$1',
        '^@middlewares/(.*)$': '<rootDir>/src/middlewares/$1',
        '^@models/(.*)$': '<rootDir>/src/models/$1',
        '^@routes/(.*)$': '<rootDir>/src/routes/$1',
        '^@services/(.*)$': '<rootDir>/src/services/$1',
        '^@utils/(.*)$': '<rootDir>/src/utils/$1',
        '^@dtos/(.*)$': '<rootDir>/src/dtos/$1',
        '^@daos/(.*)$': '<rootDir>/src/daos/$1',
        '^@repositories/(.*)$': '<rootDir>/src/repositories/$1',
        '^@databases/(.*)$': '<rootDir>/src/databases/$1',
        '^@caches/(.*)$': '<rootDir>/src/caches/$1',
        '^@loaders/(.*)$': '<rootDir>/src/loaders/$1',
        '^@factories/(.*)$': '<rootDir>/src/factories/$1',
        '^@mappers/(.*)$': '<rootDir>/src/mappers/$1',
        '^@config$': '<rootDir>/src/config',
    },
};
```

- [ ] **Step 3: `package.json`에 test 스크립트 추가**

`scripts` 블록에 한 줄 추가 (기존 항목은 그대로):

```json
        "test": "jest",
```

- [ ] **Step 4: 실패하는 테스트를 먼저 쓴다**

Create `MM/src/loaders/__tests__/masterdata.loader.test.ts`:

```typescript
import { load, getTables, findGameModeByCode } from '@loaders/masterdata.loader';

describe('masterdata.loader', () => {
    beforeAll(async () => {
        await load();
    });

    it('세 테이블을 모두 로드한다', () => {
        const tables = getTables();
        expect(tables.TbGameMode.getDataList().length).toBe(5);
        expect(tables.TbQueue.getDataList().length).toBe(2);
        expect(tables.TbMap.getDataList().length).toBe(1);
    });

    it('기존 XML과 같은 정원 값을 준다 (동작 무변화)', () => {
        const flapWang = findGameModeByCode('FlapWang');
        expect(flapWang).toBeDefined();
        expect(flapWang!.minPlayers).toBe(2);
        expect(flapWang!.maxPlayers).toBe(8);
    });

    it('큐가 정책을 데이터로 들고 있다', () => {
        const casual = getTables().TbQueue.get(1);
        expect(casual).toBeDefined();
        expect(casual!.code).toBe('Casual');
        expect(casual!.gameModeSelector).toBe('Player');
        expect(casual!.hasVisibleRank).toBe(false);
        expect(casual!.allowedGameModeIds).toEqual([1, 2, 3, 4, 5]);
    });

    it('없는 code는 undefined를 준다 (throw 아님)', () => {
        expect(findGameModeByCode('NoSuchGame')).toBeUndefined();
    });
});
```

- [ ] **Step 5: 테스트를 돌려 실패를 확인한다**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer/MatchmakingServer && npm test
```

Expected: FAIL — `getTables`/`findGameModeByCode`가 `masterdata.loader`에 없어서 컴파일 단계에서 터진다.

- [ ] **Step 6: 로더를 재작성한다**

`MM/src/loaders/masterdata.loader.ts` 전체를 아래로 교체:

```typescript
import * as fs from 'fs';
import path from 'path';
import { Tables, GameMode } from './generated/schema';

const MASTER_DATA_FOLDER = 'master_data';

let tables: Tables | undefined;

/** 로드된 테이블. load() 전에 부르면 원인을 짚어 주는 에러로 죽는다. */
export function getTables(): Tables {
    if (tables === undefined) {
        throw new Error('MasterData is not loaded. Call load() first.');
    }
    return tables;
}

/**
 * code로 게임을 찾는다.
 * 슬라이스 2에서 subGameId(string)가 gameModeId(int)로 바뀌면 이 헬퍼는 사라진다.
 */
export function findGameModeByCode(code: string): GameMode | undefined {
    return getTables().TbGameMode.getDataList().find(x => x.code === code);
}

export async function load(): Promise<void> {
    const cache = new Map<string, any>();
    tables = new Tables(file => {
        let json = cache.get(file);
        if (json === undefined) {
            const filePath = path.join(MASTER_DATA_FOLDER, `${file}.json`);
            json = JSON.parse(fs.readFileSync(filePath, 'utf-8'));
            cache.set(file, json);
        }
        return json;
    });
}
```

- [ ] **Step 7: 테스트 통과 확인**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer/MatchmakingServer && npm test
```

Expected: **4 passed**.

> 테스트가 `master_data/`를 cwd 기준 상대 경로로 읽는다. jest의 `rootDir`가 패키지 루트라 `npm test`를 패키지 루트에서 돌리면 맞는다. 다른 데서 돌리면 파일을 못 찾는다.

- [ ] **Step 8: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer && git add -A && git commit -m "$(cat <<'EOF'
feat(masterdata): 매칭 서버 로더를 Luban JSON으로 전환 + jest 도입

자체 XML 스캔 대신 Luban 생성 Tables를 구성한다. 타입도 생성물이라
손으로 쓴 인터페이스가 필요 없다. 슬라이스 4(MatchFunction/Evaluator)가
단위 테스트를 요구하므로 jest 최소 구성을 함께 깐다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: 소비처 전환 + XML 잔재 삭제

**Files:**
- Modify: `MM/src/services/waitingRoom.service.ts:8` (import), `:193` (조회)
- Delete: `MM/src/interfaces/masterdata/subGameData.interface.ts`
- Delete: `MM/src/utils/util.xml.ts`
- Delete: `MM/master_data/sub_game_data/` (Task 1의 `gen.sh`가 이미 지웠다 — 여기서 삭제를 커밋)
- Modify: `MM/package.json` (`fast-xml-parser` 의존 제거)

**Interfaces:**
- Consumes: Task 4의 `findGameModeByCode(code)`
- Produces: 없음 (슬라이스 종료)

> `MasterData` / `MasterDataType`를 쓰는 곳은 **`waitingRoom.service.ts:193` 한 곳뿐**이다(`grep` 확인). `util.xml.ts`를 쓰는 곳도 옛 로더 하나뿐이라 함께 지운다.
>
> ⚠️ **이름 충돌 주의:** 이 저장소에는 이미 `@interfaces/enums`의 `GameMode`(Normal/Ranked) enum이
> 있고, 생성 `schema.ts`에도 `GameMode` 클래스(닷지볼 등)가 있다. **한 파일에서 둘 다 import하지
> 말 것.** 이 태스크에서는 타입이 아니라 함수(`findGameModeByCode`)만 가져오므로 충돌하지 않는다.
> 두 `GameMode`가 서로 다른 것을 가리키는 이 상황 자체가 spec §1-1이 지적한 문제이며, 슬라이스 2의
> 리네임(`GameMode`{Normal,Ranked} → `Queue`)으로 해소된다.

- [ ] **Step 1: 소비처 전환**

`MM/src/services/waitingRoom.service.ts` 8번째 줄의 import를 교체:

```typescript
import { findGameModeByCode } from '@loaders/masterdata.loader';
```

193번째 줄 부근의 조회를 교체. 기존:

```typescript
                const subGameData = MasterData.get(MasterDataType.SubGameData)?.get(matchmakingTicket.subGameId);
                waitingRoom = await this.createWaitingRoom(new CreateWaitingRoomDto(
                    matchmakingTicket.matchType,
                    matchmakingTicket.subGameId,
                    matchmakingTicket.mapId,
                    matchmakingTicket.rating,
                    5,  //  ?
                    subGameData.MinPlayerCount,
                    subGameData.MaxPlayerCount
                ));
```

교체 후 (**값은 동일**. `MinPlayerCount`→`minPlayers` 등 이름만 새 테이블 필드로):

```typescript
                const gameMode = findGameModeByCode(matchmakingTicket.subGameId);
                if (gameMode === undefined) {
                    throw new Error(`Unknown gameMode code: ${matchmakingTicket.subGameId}`);
                }
                waitingRoom = await this.createWaitingRoom(new CreateWaitingRoomDto(
                    matchmakingTicket.matchType,
                    matchmakingTicket.subGameId,
                    matchmakingTicket.mapId,
                    matchmakingTicket.rating,
                    5,  //  ?
                    gameMode.minPlayers,
                    gameMode.maxPlayers
                ));
```

> 옛 코드는 `subGameData`가 `undefined`여도 그대로 `.MinPlayerCount`를 읽어 애매한 `TypeError`로 죽었다. 원인을 짚어 주는 에러로 바꾼다 — `CombatConfigProvider` fail-loud 선례와 같은 처리.

- [ ] **Step 2: 죽은 파일 삭제**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer/MatchmakingServer
git rm src/interfaces/masterdata/subGameData.interface.ts
git rm src/utils/util.xml.ts
git rm -r --ignore-unmatch master_data/sub_game_data
```

- [ ] **Step 3: `fast-xml-parser` 의존 제거**

`MM/package.json`의 `dependencies`에서 `"fast-xml-parser": "^4.0.7",` 줄을 지운 뒤:

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer/MatchmakingServer && npm install
```

- [ ] **Step 4: 컴파일 + 테스트**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer/MatchmakingServer
npm run build && npm test
```

Expected: 빌드 에러 0, 테스트 4 passed. 빌드가 `MasterData`/`MasterDataType`/`readXmlAsync`를 못 찾는다고 하면 남은 참조가 있다는 뜻이니 그것부터 정리한다.

- [ ] **Step 5: 매칭 동작이 그대로인지 확인 (수동)**

매칭 서버를 띄우고 클라 2개로 매칭을 잡는다.

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer/MatchmakingServer && npm run buildNstart
```

확인할 것:
- 기동 로그에 `✌️ MasterData loaded!`가 뜬다
- 클라 2개로 Play → 매칭이 **이전과 똑같이** 잡히고 게임에 진입한다
- 대기방이 `minPlayerCount=2` / `maxPlayerCount=8`로 만들어진다 (XML 때와 같은 값)

- [ ] **Step 6: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer && git add -A && git commit -m "$(cat <<'EOF'
refactor(masterdata): sub_game_data XML 잔재 제거

유일한 소비처(waitingRoom.service)를 Luban 테이블 조회로 전환하고,
XML 스키마 인터페이스·파서 유틸·데이터 파일과 fast-xml-parser 의존을 제거.
정원 값은 동일하며, 못 찾은 code는 원인을 짚는 에러로 바꿨다(기존엔 TypeError).

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: ROADMAP 기록 + 브랜치 머지

**Files:**
- Modify: `LeagueOfPhysical-Client/docs/ROADMAP.md`

- [ ] **Step 1: ROADMAP에 슬라이스 1 완료 기록**

`## ▶ 다음 (Next — 순서 있음)` 위의 Done 원장 끝에 새 절을 추가한다:

```markdown
### 매치메이킹 표준화 트랙 (2026-07-27~)

개념 어휘를 업계 표준으로 바로잡고 매칭을 풀 기반 표준 배치로 전환하는 트랙.
spec `docs/superpowers/specs/2026-07-27-matchmaking-standardization-design.md`
(§7에 `WaitingRoom` 폐기 대상 5레포 41파일 체크리스트).

- ✅ **슬라이스 1 — Luban 테이블 신설** (07-27, 4레포) — `TbGameMode`/`TbMap`/`TbQueue` 신설 +
  매칭 서버 전용 비기본 그룹 `m` + `matchmaking` 타깃(`typescript-json` + `json`) 추가.
  매칭 서버의 자체 XML 로더를 Luban 생성 `Tables`로 교체(타입도 생성물이라 수기 인터페이스 소멸),
  `fast-xml-parser`·`util.xml`·`subGameData.interface` 제거. 매칭 서버에 **jest 최소 구성 도입**
  (슬라이스 4의 MatchFunction/Evaluator 테스트 토대). 기존 서브게임 5종을 값 그대로 이관해
  **동작 무변화**. plan `2026-07-27-matchmaking-slice1-luban-tables`.
- ▶ 다음 = 슬라이스 2(필드 어휘 리네임 + `Match` 라운드화)
```

- [ ] **Step 2: 4개 저장소 main에 `--no-ff` 머지**

> ⚠️ **`LeagueOfPhysical-MatchmakingServer`의 기본 브랜치는 `main`이 아니라 `master`다.** 나머지 셋은 `main`.

```bash
MSG="Merge feature/matchmaking-slice1-luban-tables: Luban 게임/맵/큐 테이블 신설"
for r in infrastructure LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server; do
  cd "/c/Users/re5na/workspace/LOP/$r" && git checkout main && git merge --no-ff feature/matchmaking-slice1-luban-tables -m "$MSG"
done
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer && git checkout master && git merge --no-ff feature/matchmaking-slice1-luban-tables -m "$MSG"
```

- [ ] **Step 3: 머지 후 최종 확인**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer/MatchmakingServer && npm run build && npm test
```

Expected: 빌드 에러 0, 테스트 4 passed.

클라·서버 Unity 에디터에서 컴파일 에러 0 + EditMode 전체 통과를 확인한다:

```
run_tests(mode="EditMode", unity_instance="LeagueOfPhysical-Client@<hash>")
```

---

## 완료 기준

- [ ] `TbGameMode`/`TbMap`/`TbQueue`가 Excel 한 곳에서 클라 `.bytes` · 게임서버 `.bytes` · 매칭서버 `.json`으로 동시에 나온다
- [ ] 매칭 서버 `master_data/`에 **3개 json만** 있다 (그룹 분리가 동작)
- [ ] 매칭 서버에 XML 관련 코드·의존이 하나도 남지 않았다
- [ ] `npm test` 4 passed — 매칭 서버에 처음으로 테스트가 생겼다
- [ ] 클라·서버 EditMode 전체 통과, `TableFileManifestTests` 포함
- [ ] **매칭 동작이 이전과 동일하다** (수동 확인)
