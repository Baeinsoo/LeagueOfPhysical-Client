# Flappy 강체 충돌 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 맵이 다시 막게 하고(강체는 서로 못 뚫는다), 그 과정에서 뜻과 어긋나게 된 "유령" 이름을 "스턴"으로 바로잡는다.

**Architecture:** `FlappyWorld.MoveThroughMap`이 sweep 결과를 "닿았나?" 판정에만 쓰던 것을 되돌려, `KinematicMover.Move`(collide-and-slide)로 닿기 직전까지만 이동시킨다. 페널티·무적 타이머 구조는 그대로 두고, 그 창들 **동안의 움직임**만 바꾼다. 매 틱 `Depenetrate`는 되살리지 않는다(진동의 원인).

**Tech Stack:** C# / Unity 6000.3.16f1 / VContainer / Mirror / Protobuf(Luban은 이번 범위 밖)

**Spec:** `docs/superpowers/specs/2026-08-26-flappy-solid-map-design.md`

## Global Constraints

- **브랜치**: 모든 레포에서 `feature/flappy-solid-map`. 클라는 이미 그 브랜치이고, 나머지는
  `feature/flappy-race-start-gate` 위에서 새로 판다. main에 직접 커밋 금지.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고, 커밋 전에 `git status --short`로
  확인한다. 의도적으로 커밋하지 않는 로컬 픽스처가 늘 있다:
  - 클라: `Assets/Art`(서브모듈 포인터), `Assets/UI/Theme/Fonts/Jua-Regular SDF.asset`,
    `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/PackageManagerSettings.asset`
  - 서버: `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`,
    `Assets/DefaultVolumeProfile.asset`, `Assets/URPDefaultResources/*.asset`,
    `Assets/UniversalRenderPipelineGlobalSettings.asset`, `ProjectSettings/ProjectSettings.asset`
- **파일 이름을 바꿀 때는 `.cs`와 짝 `.meta`를 함께 `git mv`** 한다 — GUID가 보존돼 씬·프리팹 참조가
  안 끊긴다.
- **World 타입은 항상 풀 네임스페이스**(`GameFramework.World.Velocity`). `using GameFramework.World;`
  추가 금지 — `Component`가 `UnityEngine.Component`와 겹친다.
- 주석은 최소화하고 쉬운 말로. 비자명한 *의도(왜)* 만. **아직 없는 동작을 현재형으로 쓰지 않는다**
  (직전 슬라이스에서 이 지적이 세 번 나왔다).
- **테스트는 일부러 깨서 빨강을 확인한 뒤** 커밋한다.
- 소프트웨어를 설치하지 않는다. 서브에이전트를 띄우지 않는다.

### ⚠️ 이름 변경에서 건드리면 안 되는 "ghost"

`ghost`라는 문자열이 **서로 다른 세 가지**를 뜻한다. 일괄 치환하면 무관한 코드가 깨진다.

| 어디 | 무슨 뜻 | 조치 |
|---|---|---|
| `Assets/Mirror/**` (`PredictedRigidbodyPhysicsGhost` 등) | Mirror 자체 코드 | **손대지 말 것** |
| `Assets/Scripts/FlappyRaceSlice/**` (`FlappyAutoPilot`, `FlappyPacer`, `FlappyPlayer`, `FlappyDashFx`, `FlappySimJudge`, `FlappyPlayRecorder`) | 옛 프로토타입의 자체 로직. 넷코드 경로와 무관 | **손대지 말 것** |
| `EntitySyncMode.cs`, `OwnerPredictedSyncPolicy.cs`의 `GhostMode` | **Unity Netcode for Entities의 네트워크 고스트** — 전혀 다른 개념 | **손대지 말 것** |
| `DamageEffectHandlerTests.cs:124`의 `new FakeOverlap("ghost")` | 그냥 문자열 리터럴(레지스트리에 없는 id) | **손대지 말 것** |
| 그 외 Flappy 페널티 관련 전부 | 이번에 바꾸는 대상 | 아래 표대로 |

### 마스터데이터 컬럼도 이번에 바꾼다

`FlappyConfig.GhostTime`은 Luban `TbFlappyConfig`(= `infrastructure/table/Datas/#FlappyConfig.xlsx`)에서
온다. **반쪽 rename이 가장 나쁘므로 컬럼까지 한 번에 바꾼다**(Task 3).

도구는 다 있다 — `dotnet 9`, `infrastructure/table/tools/Luban/Luban.dll`, `table/gen.sh`.
그리고 이 xlsx는 **inline string 방식**이라(`sharedStrings.xml`이 없다) 시트 XML의 텍스트를 바꾸면
끝이다. `ghost_time`은 시트에 **두 번** 나온다(타입 행 + 헤더 행).

### 테스트 실행 방법

에디터가 떠 있으면 배치모드 `unity test`는 **죽는다**. 반드시:

```bash
export PATH="$HOME/.unity/bin:$PATH"

unity command recompile --project-path <PROJECT> --no-banner
unity command recompile_status --project-path <PROJECT> --no-banner   # completed/failed:false 될 때까지 다시 호출
unity command run_tests --project-path <PROJECT> --no-banner 2>&1 | python3 -c "
import sys,json
s=sys.stdin.read(); i=s.find('{\"Summary\"')
d,_=json.JSONDecoder().raw_decode(s[i:])
print(d['Summary'])
[print(' >',r['FullName'],(r['Message'] or '')[:300]) for r in d['Results'] if r['Status']!='Passed']"
```

- `<PROJECT>`는 `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client` 또는 `.../LeagueOfPhysical-Server`.
- **`--project-path`를 항상 붙인다.** 빼면 "Multiple Unity Editor instances found"로 멈추거나
  조용히 다른 프로젝트로 간다.
- **재컴파일 전에 재생 중이 아닌지 확인한다**:
  `unity command eval --code 'return "playing=" + UnityEditor.EditorApplication.isPlaying;' --project-path <PROJECT> --no-banner`
  재생 중에 재컴파일하면 사용자의 라이브 매치가 끊기고, **Pipeline 브릿지가 안 돌아올 수 있다**
  (증상: `unity status`는 ready인데 모든 명령이 30초 타임아웃. 복구는 에디터 재시작).
- GameFramework·LOP-Shared 패키지의 EditMode 테스트는 클라/서버 테스트 실행에 함께 포함된다.

### 기준선 (착수 전)

- 클라 **598 passed / 0 failed**
- 서버 **568 passed / 0 failed**

---

## 이름 대응표

| 지금 | 바꿈 |
|---|---|
| `FlappyGhost` (컴포넌트) | `FlappyStun` |
| &nbsp;&nbsp;`Remaining` | `StunRemaining` |
| &nbsp;&nbsp;`InvulnRemaining` | 그대로 |
| `FlappyGhostSystem` | `FlappyStunSystem` |
| &nbsp;&nbsp;`IsStopped(entity)` | `IsStunned(entity)` |
| &nbsp;&nbsp;`Enter(entity)` | 그대로 |
| `FlappyConfig.GhostTime` / ctor `ghostTime` | `StunTime` / `stunTime` |
| `GhostAppearance` (클라 MonoBehaviour) | `StunAppearance` |
| &nbsp;&nbsp;`GhostColor` | `StunColor` |
| proto `EntitySnap.ghost` (필드번호 12) | `stunned` (**번호 12 유지**) |
| 클라 `EntitySnap.ghost` | `stunned` |
| `FlappyWorldGhostTests.cs` | `FlappyWorldStunTests.cs` |
| `FlappyGhostSystemTests.cs` | `FlappyStunSystemTests.cs` |
| `GhostFieldMapperTests.cs` | `StunFieldMapperTests.cs` |

**근거**: 이 상태는 "부딪혀서 잠시 못 움직임 + 그동안 무적"이다. 액션·격투 게임의 **stun / i-frames**가
그 표준어다. `ghost`는 "통과한다"는 뜻이었고 이제 통과하지 않으므로 어긋난다. `invuln`은 이미
표준어라 그대로 둔다.

---

## Task 1: 이름 변경 — C# (LOP-Shared + 클라 + 서버)

**동작은 하나도 바뀌지 않는다.** 순수 rename이라 테스트 개수·결과가 그대로여야 한다.

**Files:**
- LOP-Shared:
  - Rename: `Runtime/Scripts/Game/FlappyGhost.cs` → `FlappyStun.cs` (+ `.meta`)
  - Rename: `Runtime/Scripts/Game/FlappyGhostSystem.cs` → `FlappyStunSystem.cs` (+ `.meta`)
  - Modify: `Runtime/Scripts/Game/FlappyConfig.cs`, `FlappyWorld.cs`, `FlappySavedState.cs`
  - Rename: `Tests/EditMode/FlappyWorldGhostTests.cs` → `FlappyWorldStunTests.cs` (+ `.meta`)
  - Rename: `Tests/EditMode/FlappyGhostSystemTests.cs` → `FlappyStunSystemTests.cs` (+ `.meta`)
  - Modify: `Tests/EditMode/FlappyWorldFixture.cs`, `FlappyWorldTests.cs`, `FlappyWorldDeterminismTests.cs`,
    `FlappyWorldSaveStateTests.cs`, `FlappyBodyCollisionSystemTests.cs`, `FlappyMoveSystemTests.cs`,
    `FlappyWorldStartGateTests.cs`(있으면)
- 클라:
  - Rename: `Assets/Scripts/Entity/GhostAppearance.cs` → `StunAppearance.cs` (+ `.meta`)
  - Modify: `Assets/Scripts/Entity/EntityBinder.cs`, `Assets/Scripts/Entity/FlappyBirdCreator.cs`,
    `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`, `Assets/Scripts/Game/FlappyConfigProvider.cs`,
    `Assets/Scripts/Netcode/PredictedEntityInterpolator.cs`, `SnapshotEntityInterpolator.cs`,
    `ExtrapolatedEntityInterpolator.cs`
  - Modify: `Assets/Tests/EditMode/EntitySync/ExtrapolationAccelerationTests.cs` (ctor 인자명만)
- 서버:
  - Modify: `Assets/Scripts/Entity/FlappyBirdCreator.cs`, `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`,
    `Assets/Scripts/Game/FlappyConfigProvider.cs`

**Interfaces:**
- Consumes: (없음)
- Produces: 위 대응표의 새 이름들. 이후 태스크는 새 이름으로 쓴다.

> **왜 세 레포가 한 태스크인가**: `FlappyBirdCreator`(클·서)가 `new FlappyGhost()`를 직접 부른다.
> LOP-Shared만 바꾸면 클라·서버가 컴파일되지 않는다. 원자적으로 가야 한다.

- [ ] **Step 1: 대상 목록을 먼저 확정한다**

```bash
cd /Users/insoobae/workspace/LOP
for r in LeagueOfPhysical-Shared LeagueOfPhysical-Client LeagueOfPhysical-Server; do
  echo "=== $r"
  grep -rn "Ghost\|ghost" $r --include="*.cs" 2>/dev/null \
    | grep -v "/Library/\|Runtime.Generated\|/Mirror/\|FlappyRaceSlice/\|GhostMode\|FakeOverlap"
done
```

이 목록이 바꿀 전부다. **Global Constraints의 "건드리면 안 되는 ghost" 표와 대조**하고,
목록에 그 네 부류가 하나도 남아 있지 않은지 확인한 뒤 시작한다.

- [ ] **Step 2: LOP-Shared 파일 두 개를 `git mv` 한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git mv Runtime/Scripts/Game/FlappyGhost.cs Runtime/Scripts/Game/FlappyStun.cs
git mv Runtime/Scripts/Game/FlappyGhost.cs.meta Runtime/Scripts/Game/FlappyStun.cs.meta
git mv Runtime/Scripts/Game/FlappyGhostSystem.cs Runtime/Scripts/Game/FlappyStunSystem.cs
git mv Runtime/Scripts/Game/FlappyGhostSystem.cs.meta Runtime/Scripts/Game/FlappyStunSystem.cs.meta
git mv Tests/EditMode/FlappyWorldGhostTests.cs Tests/EditMode/FlappyWorldStunTests.cs
git mv Tests/EditMode/FlappyWorldGhostTests.cs.meta Tests/EditMode/FlappyWorldStunTests.cs.meta
git mv Tests/EditMode/FlappyGhostSystemTests.cs Tests/EditMode/FlappyStunSystemTests.cs
git mv Tests/EditMode/FlappyGhostSystemTests.cs.meta Tests/EditMode/FlappyStunSystemTests.cs.meta
```

`.meta`를 함께 옮기는 이유는 GUID 보존이다 — 안 그러면 Unity가 새 GUID를 발급해 참조가 끊긴다.

- [ ] **Step 3: 클래스·멤버 이름을 바꾼다**

대응표대로. 클래스명, 파일 안의 타입 참조, 필드명, 테스트 클래스명까지.

`FlappyStun`(구 `FlappyGhost`)의 XML 주석도 새 뜻에 맞게 고친다. 지금 "유령"이라는 말로
*통과*를 설명하고 있다면 그건 이제 틀린 설명이다 — "부딪혀서 잠시 못 움직이는 상태"로 고쳐 쓴다.

- [ ] **Step 4: 클라 `GhostAppearance`를 옮긴다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git mv Assets/Scripts/Entity/GhostAppearance.cs Assets/Scripts/Entity/StunAppearance.cs
git mv Assets/Scripts/Entity/GhostAppearance.cs.meta Assets/Scripts/Entity/StunAppearance.cs.meta
```

`EntityBinder`와 세 보간기(`PredictedEntityInterpolator`, `SnapshotEntityInterpolator`,
`ExtrapolatedEntityInterpolator`)가 이 타입을 필드로 들고 있다. 필드명도 `ghostAppearance` →
`stunAppearance`로 맞춘다.

**주의**: `ExtrapolatedEntityInterpolator`에는 `latest.ghost`(스냅 필드)를 읽는 곳이 있다.
그건 **Task 2**에서 바꾼다 — 이번 태스크에서는 손대지 않는다(아직 proto가 `ghost`다).

- [ ] **Step 5: 서버 세 파일을 고친다**

`FlappyBirdCreator`(`new FlappyGhost()`), `FlappyRaceLifetimeScope`(`FlappyGhostSystem` 등록),
`FlappyConfigProvider`(`r.GhostTime` → `stunTime:` 인자). 프로바이더에는 임시 매핑 주석을 남긴다
(`stunTime: r.GhostTime` — 컬럼은 Task 3에서 바꾼다).

- [ ] **Step 6: 양쪽 컴파일 + 테스트**

```bash
export PATH="$HOME/.unity/bin:$PATH"
# 재생 중이 아닌지 먼저 확인 → recompile → recompile_status → run_tests (양쪽 프로젝트)
```

기대: **클라 598 / 0, 서버 568 / 0 — 개수와 결과가 착수 전과 똑같아야 한다.**
개수가 달라졌으면 테스트를 지웠거나 중복 생성한 것이니 멈추고 확인한다.

- [ ] **Step 7: 남은 참조가 없는지 확인**

Step 1의 grep을 다시 돌린다. **결과가 비어 있어야 한다**(건드리면 안 되는 네 부류는 grep에서
이미 제외돼 있다).

- [ ] **Step 8: 커밋 (세 레포)**

각 레포에서 `git status --short`로 픽스처가 섞이지 않았는지 확인한 뒤, 바꾼 경로만 지정해 커밋한다.

```
refactor(flappy): 유령을 스턴으로 바로잡는다 (동작 변경 없음)
```

---

## Task 2: 이름 변경 — 와이어 필드

**Files:**
- LOP-Shared: `Protos/EntitySnap.proto` (수정), `Runtime.Generated/**` (재생성물)
- 클라: `Assets/Scripts/Netcode/EntitySnap.cs`, `ExtrapolatedEntityInterpolator.cs`,
  Rename `Assets/Tests/Editor/GhostFieldMapperTests.cs` → `StunFieldMapperTests.cs` (+ `.meta`)
- 서버: `Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs`

**Interfaces:**
- Consumes: Task 1의 새 이름들
- Produces: `EntitySnap.Stunned` (proto), 클라 `EntitySnap.stunned`

- [ ] **Step 1: proto 필드 이름을 바꾼다**

`Protos/EntitySnap.proto`에서:

```proto
  bool ghost = 12;
```
→
```proto
  bool stunned = 12;
```

**필드 번호 12를 절대 바꾸지 않는다.** 이름만 바꾸면 와이어 포맷이 동일하다(protobuf는 번호로 인코딩).

- [ ] **Step 2: 생성한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Scripts
./generate_protos.sh
```

> `generate_message_ids.sh`가 `declare -A`를 써서 macOS 기본 bash 3.2에서 죽는다. 그때는
> Homebrew bash로 하위 스크립트를 부르면 된다. **스크립트 파일 자체는 고치지 않는다**(범위 밖).

- [ ] **Step 3: MessageIds가 안 바뀌었는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git diff Runtime.Generated/Scripts/MessageIds.cs
```

**출력이 비어 있어야 한다.** 이번엔 메시지를 추가하지 않았으므로 id가 하나도 바뀔 이유가 없다.
하나라도 바뀌었으면 **멈추고 보고한다** — 배포본과 wire desync가 난다.

- [ ] **Step 4: 클라·서버 사용처를 고친다**

- 클라 `Assets/Scripts/Netcode/EntitySnap.cs`: `public bool ghost` → `public bool stunned`
- 클라 `ExtrapolatedEntityInterpolator.cs`: `latest.ghost` → `latest.stunned`
- 서버 `EntitySnapshotBroadcastSystem.cs`: 스냅에 값을 싣는 자리
- 매퍼 테스트 파일을 `git mv`로 이름 바꾸고(짝 `.meta` 포함), 테스트 클래스명·테스트 메서드명도
  새 이름에 맞춘다

AutoMapper가 이름 규약으로 매핑하므로 **양쪽 이름이 같아야 한다**(proto `Stunned` ↔ 클라 `stunned`).
매퍼 테스트가 정확히 그걸 지키고 있으니, 이름을 바꾼 뒤 그 테스트가 통과하는지가 검증이다.

- [ ] **Step 5: 컴파일 + 테스트**

기대: 클라 598 / 0, 서버 568 / 0 — 여전히 개수 불변.

- [ ] **Step 6: 커밋 (세 레포)**

```
refactor(wire): 스냅 필드 ghost를 stunned로 바꾼다 (번호 12 유지)
```

---

## Task 3: 이름 변경 — 마스터데이터 컬럼

**Files:**
- Modify: `infrastructure/table/Datas/#FlappyConfig.xlsx`
- Generated (커밋 대상): `LeagueOfPhysical-MasterData-Client/Runtime.Generated/**`,
  `LeagueOfPhysical-MasterData-Server/Runtime.Generated/**`
- Modify: 클라 `Assets/Scripts/Game/FlappyConfigProvider.cs`, 서버 같은 파일

**Interfaces:**
- Consumes: Task 1의 `FlappyConfig(stunTime:)`
- Produces: `TbFlappyConfig.StunTime` (Luban 생성 프로퍼티)

> Task 1에서 프로바이더가 `stunTime: r.GhostTime`으로 매핑해 두었다. 이 태스크가 그 매핑을
> `stunTime: r.StunTime`으로 정리한다.

- [ ] **Step 1: 브랜치를 판다**

`infrastructure`, `LeagueOfPhysical-MasterData-Client`, `LeagueOfPhysical-MasterData-Server` 세 레포에
`feature/flappy-solid-map`을 만든다(각각 현재 브랜치 위에서).

- [ ] **Step 2: xlsx의 컬럼 이름을 바꾼다**

이 파일은 **inline string 방식**이다(`sharedStrings.xml`이 없다). 시트 XML의 텍스트를 바꾸면 된다.
`ghost_time`은 두 번 나온다 — `##var` 행과 헤더 행.

```bash
cd /Users/insoobae/workspace/LOP/infrastructure
python3 - <<'EOF'
import zipfile, shutil, os
src = 'table/Datas/#FlappyConfig.xlsx'
tmp = src + '.tmp'
zin = zipfile.ZipFile(src)
before = zin.read('xl/worksheets/sheet1.xml').decode('utf-8')
assert before.count('ghost_time') == 2, f"예상과 다름: {before.count('ghost_time')}회"
zout = zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED)
for item in zin.infolist():
    data = zin.read(item.filename)
    if item.filename == 'xl/worksheets/sheet1.xml':
        data = data.decode('utf-8').replace('ghost_time', 'stun_time').encode('utf-8')
    zout.writestr(item, data)
zout.close(); zin.close()
shutil.move(tmp, src)
print('ok')
EOF
```

**`assert`가 핵심이다** — 2회가 아니면 파일 구조가 예상과 달라진 것이니 멈추고 보고한다.
`invuln_time`은 건드리지 않는다(이미 표준어).

- [ ] **Step 3: 바뀐 것을 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/infrastructure
python3 -c "
import zipfile,re
x=zipfile.ZipFile('table/Datas/#FlappyConfig.xlsx').read('xl/worksheets/sheet1.xml').decode('utf-8')
print('stun_time:', x.count('stun_time'), '/ ghost_time:', x.count('ghost_time'))
print([t for t in re.findall(r'<t[^>]*>(.*?)</t>', x) if 'time' in t])"
```

기대: `stun_time: 2 / ghost_time: 0`.

- [ ] **Step 4: Luban을 돌린다**

```bash
cd /Users/insoobae/workspace/LOP/infrastructure/table
./gen.sh
```

산출물이 MasterData-Client / MasterData-Server 두 패키지로 나간다.

- [ ] **Step 5: 생성물이 기대대로인지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Client
git diff --stat
grep -n "StunTime\|GhostTime" Runtime.Generated/Scripts/MasterData/FlappyConfig.cs
```

기대: `StunTime` 프로퍼티가 생기고 `GhostTime`이 사라졌다. **다른 테이블의 생성물이 함께
바뀌었으면 멈추고 보고한다** — 이 태스크는 FlappyConfig만 건드려야 한다.

서버 패키지도 같은 확인을 한다.

- [ ] **Step 6: 프로바이더 매핑을 정리한다**

클라·서버 `FlappyConfigProvider.cs`에서 Task 1이 남긴 매핑 주석을 지우고 `r.StunTime`으로 바꾼다.

- [ ] **Step 7: 컴파일 + 테스트**

기대: 클라 598 / 0, 서버 568 / 0 — 여전히 개수 불변.

> 마스터데이터는 `.bytes`로 로드되므로 **에디터가 실제로 새 데이터를 읽는지**가 컴파일만으로는
> 확인되지 않는다. `TbFlappyConfig` 로딩이 깨지면 Entrance 단계에서 죽는다. 라이브 검증(Task 5)의
> 첫 관문이 이것이다 — 게임이 뜨면 마스터데이터가 정상이라는 뜻이다.

- [ ] **Step 8: 커밋 (다섯 레포)**

`infrastructure`(xlsx), MasterData 두 패키지(생성물), 클라·서버(프로바이더).
각각 `git status --short`로 확인 후 경로 지정 커밋.

```
refactor(masterdata): ghost_time 컬럼을 stun_time으로 바꾼다
```

---

## Task 4: 맵이 다시 막는다

이 슬라이스의 본체다.

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldSolidMapTests.cs` (신규)

**Interfaces:**
- Consumes: Task 1~3의 새 이름
- Produces: (없음 — 내부 동작 변경)

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/EditMode/FlappyWorldSolidMapTests.cs`:

```csharp
using GameFramework.Physics;
using NUnit.Framework;

namespace LOP.Tests
{
    public class FlappyWorldSolidMapTests
    {
        //  앞을 막는 벽. 이웃 테스트 파일들의 스텁과 같은 모양으로 둔다.
        private class WallQuery : ICollisionQuery
        {
            public CollisionHit CapsuleCast(UnityEngine.Vector3 p1, UnityEngine.Vector3 p2, float radius,
                UnityEngine.Vector3 direction, float distance, int layerMask)
                => new CollisionHit(true, 0f, -direction, p1);
        }

        private const float Dt = 0.02f;

        private static FlappyWorld Started(ICollisionQuery query, out GameFramework.World.Entity bird)
        {
            var world = FlappyWorldFixture.Create(query, out bird);
            world.GameplayStartTick = 0;
            return world;
        }

        [Test]
        public void 앞이_막혀_있으면_벽을_넘어가지_않는다()
        {
            var world = Started(new WallQuery(), out var bird);
            world.Tick(0, Dt);

            //  거리 0인 벽이므로 한 틱을 굴려도 시작 위치를 벗어나면 안 된다.
            Assert.AreEqual(System.Numerics.Vector3.Zero, bird.Get<GameFramework.World.Transform>().Position);
        }

        [Test]
        public void 막힌_채로_여러_틱_굴려도_위치가_흔들리지_않는다()
        {
            var world = Started(new WallQuery(), out var bird);
            world.Tick(0, Dt);
            var afterFirst = bird.Get<GameFramework.World.Transform>().Position;

            for (long tick = 1; tick < 50; tick++)
            {
                world.Tick(tick, Dt);
                //  진동이란 "막힌 채 매 틱 위치가 흔들리는 것"이다. 불변이면 진동이 없다는 뜻이다.
                Assert.AreEqual(afterFirst, bird.Get<GameFramework.World.Transform>().Position,
                    $"tick {tick}에서 위치가 움직였다");
            }
        }

        [Test]
        public void 무적_중에도_벽을_넘어가지_않는다()
        {
            var world = Started(new WallQuery(), out var bird);
            var stun = bird.Get<FlappyStun>();

            //  스턴은 끝나고 무적만 남은 상태를 직접 만든다.
            stun.StunRemaining = 0f;
            stun.InvulnRemaining = 0.5f;

            world.Tick(0, Dt);

            Assert.AreEqual(System.Numerics.Vector3.Zero, bird.Get<GameFramework.World.Transform>().Position);
        }

        [Test]
        public void 아무것도_안_닿으면_그대로_간다()
        {
            var world = Started(new EmptySkyQuery(), out var bird);
            world.Tick(0, Dt);

            Assert.Greater(bird.Get<GameFramework.World.Transform>().Position.X, 0f);
        }

        //  이웃 테스트 파일들과 같은 모양의 "아무것도 안 맞는" 스텁.
        private class EmptySkyQuery : ICollisionQuery
        {
            public CollisionHit CapsuleCast(UnityEngine.Vector3 p1, UnityEngine.Vector3 p2, float radius,
                UnityEngine.Vector3 direction, float distance, int layerMask)
                => CollisionHit.None;
        }
    }
}
```

> `CollisionHit`의 생성자·`None`이 위와 다르면 **이웃 파일(`FlappyWorldTests.cs`,
> `FlappyWorldGhostTests`→`FlappyWorldStunTests`)의 스텁 모양을 그대로 따른다.** 새 API를 만들지 말 것.
> `FlappyStun`의 필드가 `public`이 아니어서 테스트에서 직접 못 세운다면, 세 번째 테스트는
> `AlwaysHit`으로 한 번 부딪혀 스턴을 걸고 `StunTime`만큼 틱을 굴려 무적 구간에 들어간 뒤 검사한다.

> **스펙 §7의 나머지 세 줄은 이미 덮여 있다** — "부딪힘 → 페널티 진입", "페널티 중 재접촉 시
> 재진입 없음", "무적 끝난 뒤 다시 페널티"는 **타이머 동작**이고 이번에 바뀌지 않는다.
> 기존 `FlappyStunSystemTests`(구 `FlappyGhostSystemTests`)가 그대로 지킨다. 여기서는
> **움직임이 바뀌는 것**만 새로 검사한다.

- [ ] **Step 2: 빨강을 확인한다**

기대: `앞이_막혀_있으면...`과 `막힌_채로...`, `무적_중에도...`가 **실패**한다(지금은 통과하므로).
`아무것도_안_닿으면...`은 지금도 통과한다.

- [ ] **Step 3: `MoveThroughMap`을 되돌린다**

```csharp
private void MoveThroughMap(GameFramework.World.Entity entity, float deltaTime)
{
    var transform = entity.Get<GameFramework.World.Transform>();
    var velocity = entity.Get<GameFramework.World.Velocity>();
    var body = entity.Get<GameFramework.World.CapsuleShape>();
    if (transform == null || velocity == null || body == null)
    {
        return;
    }

    // 닿으면 스턴을 건다. 무적 중이면 Enter가 알아서 무시한다.
    // 이동은 닿기 직전까지만 — 무적이든 아니든 맵은 뚫지 않는다.
    var result = KinematicMover.Move(new KinematicMoveInput(
        transform.Position.ToUnity(), velocity.Linear.ToUnity(),
        body.Radius, body.Height, deltaTime, _collisionQuery, _layerMask));

    if (result.HasHit)
    {
        _stunSystem.Enter(entity);
    }

    transform.Position = result.position.ToNumerics();
    _motionBridge.PushMotion(entity);
}
```

**`_motionBridge.Depenetrate(entity)`를 되살리지 않는다.** 그것이 진동의 원인이었다.

> `KinematicMoveInput`의 실제 시그니처와 `KinematicMoveResult`의 멤버명(`HasHit`이 있는지,
> 이름이 다른지)은 **`KinematicMover`를 직접 읽고 맞춘다.** 위 코드는 08-24에 지워진 형태를
> 복원한 것이라 인자 순서가 다를 수 있다. 다르면 실제 시그니처를 따르고 보고한다.
> `git show 82d7c3a -- Runtime/Scripts/Game/FlappyWorld.cs`로 지워진 원본을 볼 수 있다.

- [ ] **Step 4: 맵 밀어내기를 몸싸움과 이동 사이에 둔다**

`KinematicMover`는 `SkinWidth`(0.02) 만큼 띄워 멈추므로 **애초에 파고들지 않는다.** 그럼에도
새가 벽 안에 들어가는 경로가 하나 있다 — `FlappyBodyCollisionSystem`이 **맵을 모른 채** 새끼리
밀어내기 때문이다. 그 결과를 그대로 sweep에 넣으면 벽 안에서 시작해 거리 0이 나오고, 그게 낑김이다.

`Mutation`에서 몸싸움 **뒤**, `MoveThroughMap` **앞**에 맵 밀어내기를 둔다:

```csharp
_bodyCollisionSystem.Resolve(_birds, _bodies);

// 몸싸움은 맵을 모른 채 새끼리 민다 — 그 결과가 벽 안일 수 있다.
// 여기서 되돌려야 아래 sweep이 겹치지 않은 자리에서 시작한다.
// 겹침이 없으면 0을 돌려주므로 매 틱 불러도 공짜다(스폰 겹침도 첫 틱에 여기서 풀린다).
for (int i = 0; i < _birds.Count; i++)
{
    _motionBridge.Depenetrate(_birds[i]);
}

_motionBridge.SyncTransforms();
for (int i = 0; i < _birds.Count; i++)
{
    MoveThroughMap(_birds[i], deltaTime);
}
```

**`MoveThroughMap` 안에서는 부르지 않는다** — 이동 직전에 부르면 밀어냄과 이동이 같은 틱에
같은 위치를 두고 다툰다. 자리는 한 곳뿐이어야 한다.

> `Depenetrate`가 물리 바디를 읽으므로 `SyncTransforms()`와의 순서가 중요할 수 있다.
> 위 배치로 진동이 남으면 `SyncTransforms()`를 밀어내기 **앞**으로 옮겨 보고, 그래도 남으면
> 스펙 §4의 "중재 규칙"으로 넘어간다. **어느 쪽이든 결과를 보고한다.**


- [ ] **Step 5: 초록을 확인한다**

기대: 실패 0. 총 개수는 클라 598 + 4 = **602**, 서버 568 + 4 = **572**.

- [ ] **Step 6: 테스트가 실제로 깨지는지 확인한다**

`transform.Position = result.position` 을 `transform.Position = (start + delta)` 로 잠깐 되돌린다
(= 통과 동작).
기대: `앞이_막혀_있으면...`·`막힌_채로...`·`무적_중에도...` 세 개가 **빨강**. 확인 후 되돌린다.

- [ ] **Step 7: 커밋**

```
feat(flappy): 맵이 다시 막는다 — 무적 중에도 뚫지 않는다
```

---

## Task 5: 라이브 검증 (사람이 한다)

**자동으로 확인할 수 없는 것들**이라 서브에이전트에 넘기지 않는다.

- [ ] **Step 1: 사전조건**

- 클라·서버 에디터 둘 다 env `local`
- 서버 픽스처 `playerList`에 **지금 떠 있는 클론의 uuid**가 있어야 한다. MPPM 클론은 재시작할
  때마다 새 익명 계정을 받으므로, 거부되면 서버 콘솔의
  `[Auth] 접속 거부: 명단에 없는 참가자: <uuid>`를 보고 넣는다. **클론은 재시작하지 않는다.**

- [ ] **Step 2: 확인 목록**

| 무엇 | 기대 |
|---|---|
| **카메라 진동** | 벽에 밀착해도 화면이 떨리지 않는다 ← 이 슬라이스의 핵심 가설 |
| **낑김** | 벽에 붙어도 날개짓으로 위아래를 더듬어 빠져나올 수 있다 |
| **스폰** | 출발 직후 새가 지오메트리에 갇혀 있지 않다 |
| **무한 추락 소멸** | 날개짓을 안 해도 바닥에 걸린다 |
| **연쇄 페널티** | 좁은 핀치(높이 7)에서 벌이 너무 가혹하지 않은가 (스펙 §9) |

- [ ] **Step 3: 미뤄 뒀던 검증도 같이 끝낸다**

맵이 막으면 두 새가 같은 구간에 머물러 화면에 같이 둘 수 있게 된다. 그 판에서
`feature/flappy-ghost-extrapolation`의 미완 검증을 끝낸다:

- 스턴 걸린 새가 **청회색**으로 변하는가
- 남의 새가 **매끄러운가, 순간이동처럼 튀는가**
- 새끼리 부딪혔을 때 양쪽 화면이 다르게 보이는가

- [ ] **Step 4: 결과를 스펙에 남긴다**

`docs/superpowers/specs/2026-08-26-flappy-solid-map-design.md`에 `## 10. 실측 결과` 절을 만들고
**본 그대로** 적는다. 특히 §4의 가설(진동은 `Depenetrate`만의 것이었나)이 맞았는지 명시한다.
틀렸으면 틀렸다고 적고 스펙을 다시 연다.

- [ ] **Step 5: 커밋**

```
docs(spec): 강체 충돌 실측 결과를 남긴다
```

---

## 머지

**이 계획은 머지를 포함하지 않는다.** Task 5의 결과를 사용자가 본 뒤 결정한다.

브랜치가 세 겹으로 쌓여 있다:

```
main
 └ feature/flappy-ghost-extrapolation   (유령정지 + 원격 외삽)
    └ feature/flappy-race-start-gate    (시작 게이트)
       └ feature/flappy-solid-map       (이 슬라이스)
```

이 슬라이스는 **레포 여섯 개**를 건드린다 — GameFramework를 뺀 나머지 전부
(LOP-Shared, 클라, 서버, MasterData-Client, MasterData-Server, infrastructure).

머지는 **아래에서 위로** 순서대로 하고, 레포마다 `CLAUDE.md`의 "푸시 규약"을 한 줄씩 밟는다.
`git push --force`는 어떤 경우에도 금지다.
