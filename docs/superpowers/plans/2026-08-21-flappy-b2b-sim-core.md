# Flappy Race B2-b — 시뮬 코어 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Flappy Race의 새가 고정 전진하고, 플랩·중력으로 뜨고 떨어지고, 파이프에 막히고, 새끼리 부딪혀 밀리는 규칙을 클·서가 **같은 코드**로 돌리는 시뮬 코어를 LOP-Shared에 만든다.

**Architecture:** 빈 껍데기인 `FlappyWorld.Mutation`을 세 페이즈로 채운다 — ① `FlappyMoveSystem`이 속도를 정하고, ② `FlappyBodyCollisionSystem`이 새끼리 밀어내고 세로속도를 주고받고, ③ 공유 커널 `KinematicMover.Move`가 맵에 막히며 이동시킨다. 튜닝값은 새 마스터데이터 테이블 `TbFlappyConfig`에서 온다(클·서 양쪽 패키지에 생성 → 같은 값). 새끼리 겹침은 물리엔진이 아니라 **해석적 캡슐 계산**으로 구한다(되감아 다시 돌려도 같은 답).

**Tech Stack:** Unity 6 / C# · VContainer DI · Luban 마스터데이터 · NUnit EditMode 테스트 · 6개 git 저장소(infrastructure, MasterData-Client, MasterData-Server, LOP-Shared, LOP-Client, LOP-Server)

**Spec:** `docs/superpowers/specs/2026-08-17-flappy-race-gameplay-b2-design.md` (§5가 이 계획의 본문, §8이 확정된 결정)

## Global Constraints

- **`namespace LOP` 유지** — LOP-Shared의 모든 신규 타입은 `namespace LOP`. 호출부 변경 0.
- **World 타입은 항상 풀 네임스페이스** — LOP 측 파일은 `using GameFramework.World;`를 쓰지 않는다. `GameFramework.World.Entity`, `GameFramework.World.Transform`처럼 풀로 쓴다(`Component`가 `UnityEngine.Component`와 겹침).
- **시뮬 로직은 구체 클래스 공유** — 클·서가 갈릴 여지를 만드는 인터페이스 seam을 시뮬에 두지 않는다. 인터페이스는 사이드가 달라야 하는 I/O 어댑터에만(`ICollisionQuery`, `IMotionBridge`).
- **`*System` = 무상태 DI 인스턴스** / **`static`은 컨텍스트 없는 순수 커널에만**, 그리고 순수 커널에는 `*System` 이름을 붙이지 않는다.
- **Anemic** — 컴포넌트는 데이터만. 상태 변경 로직은 System에.
- **주석은 최소·일상어** — 코드로 자명한 것은 달지 않고, 비자명한 *의도(왜)* 만 짧게. 설명 없이 전문용어를 던지지 않는다.
- **`.meta` 파일은 유니티가 만든 것만 커밋** — 직접 만들거나 수정하지 않는다. 새 `.cs`를 만든 뒤 유니티 임포트를 돌려 `.meta`를 생성시키고 함께 커밋한다.
- **`git add -A` / `git commit -a` 금지** — 바꾼 파일만 경로로 지정하고, 커밋 전 `git status --short`로 스테이지된 것이 의도한 파일뿐인지 확인한다. 유니티 레포에는 커밋하지 않는 로컬 픽스처가 늘 있다(아트 서브모듈 포인터, 폰트 에셋, ProjectSettings).
- **푸시하지 않는다** — 각 태스크는 피처 브랜치에 커밋까지만. 6개 저장소의 리베이스·머지·푸시는 전 태스크가 끝난 뒤 컨트롤러가 사용자와 함께 한 번에 처리한다.
- **브랜치 이름은 6개 저장소 모두 `feature/flappy-b2b-sim-core`.**
- **유니티 저장소에 git worktree를 쓰지 않는다** — 일반 브랜치로 전환한다.

### 튜닝 시작값 (스펙 §5 + 프로토타입 실측)

| 값 | 숫자 | 출처 |
|---|---|---|
| `forward_speed` | 11 | 스펙 §5 |
| `flap_impulse` | 23 | 스펙 §5 |
| `gravity` | 70 | 스펙 §5 |
| `max_fall_speed` | 30 | 스펙 §5 |
| `body_radius` | 0.45 | 프로토타입 새 `SphereCollider` 반지름 0.4477941 |
| `body_height` | 0.9 | 반지름의 2배 = 캡슐이 구가 된다(프로토타입 몸이 구였다) |
| `restitution` | 0.35 | 프로토타입 `FlappyBird.Restitution` |

### 이 슬라이스가 뒤집는 스펙 결정 하나

스펙 §5·§6은 새끼리 몸싸움을 **프리팹 캡슐 콜라이더 + 물리엔진 겹침 조회**로 전제하고, 그래서 "프리팹 콜라이더 크기가 `body_radius`와 반드시 일치해야 한다"는 경고를 달았다.

**이 계획은 해석적(수식) 겹침 계산으로 간다.** 새 몸은 전부 같은 규격의 세로 캡슐이라 겹침을 거리 산수로 바로 구할 수 있고, 그러면 (a) 되감아 다시 돌려도 답이 같고, (b) 콜라이더 없이 EditMode에서 시험할 수 있고, (c) **"두 곳의 크기가 어긋나면 예측이 깨진다"는 위험 자체가 사라진다**(몸 규격이 `TbFlappyConfig` 한 곳에만 존재). 되감기 넷코드에서 게임플레이 충돌을 호스트 물리엔진에 맡기지 않는 것이 업계 표준이다(Photon Quantum이 자체 결정론 물리를 두는 이유, 격투게임 pushbox, 오버워치 캡슐 산수).

**맵(파이프·지형) 충돌은 그대로 물리엔진 sweep을 쓴다** — 임의 메시라 산수로 못 푼다.

---

## File Structure

### LeagueOfPhysical-Shared (`Runtime/Scripts/Game/`)

| 파일 | 책임 |
|---|---|
| `FlappyConfig.cs` (신규) | 튜닝값 순수 struct. MasterData 패키지를 참조할 수 없는 Shared가 값을 건네받는 그릇 |
| `FlappyBounce.cs` (이동) | 부딪힌 두 새의 세로 속도 교환 계산. 클라 프로토타입 폴더에서 옮겨온다 |
| `FlappyBodyOverlap.cs` (신규) | 세로 캡슐 둘의 겹침 기하 — 순수 static 커널 |
| `FlappyMoveSystem.cs` (신규) | 새 한 마리의 이번 틱 속도(중력·플랩·고정 전진) |
| `FlappyBodyCollisionSystem.cs` (신규) | 새들을 두 마리씩 맞대어 밀어내고 세로속도 교환 |
| `FlappyWorld.cs` (수정) | 세 페이즈를 순서대로 부르는 시뮬 진입점 |

### LeagueOfPhysical-Shared (`Tests/EditMode/`)

`FlappyBounceTests.cs`(이동) · `FlappyBodyOverlapTests.cs`(신규) · `FlappyMoveSystemTests.cs`(신규) · `FlappyBodyCollisionSystemTests.cs`(신규) · `FlappyWorldTests.cs`(수정)

### infrastructure (`table/`)

`Datas/#FlappyConfig.xlsx`(신규) · `Datas/__tables__.xlsx`(행 추가)

### LeagueOfPhysical-MasterData-Client / -Server

`Runtime.Generated/Scripts/MasterData/FlappyConfig.cs` + `.meta`, `Runtime.Generated/Scripts/MasterData/Tables.cs`(재생성), `Runtime.Generated/StreamingAssets/MasterData/tbflappyconfig.bytes` + `.meta` — 전부 Luban 생성물

### LeagueOfPhysical-Client

`Assets/Scripts/Game/FlappyConfigProvider.cs`(신규) · `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`(수정) · `Assets/Scripts/FlappyRaceSlice/Logic/FlappyBounce.cs`(삭제) · `Assets/Scripts/FlappyRaceSlice/FlappyBird.cs`(수정) · `Assets/Tests/EditMode/FlappyBounceTests.cs`(삭제)

### LeagueOfPhysical-Server

`Assets/Scripts/Game/FlappyConfigProvider.cs`(신규) · `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`(수정)

---

## 이 슬라이스가 하지 않는 것 (B2-d 몫)

- 클라 새에 `Simulated` 붙이기 — **그래서 이 슬라이스 뒤에도 클라는 새를 시뮬하지 않는다.** 서버만 돈다.
- 새 프리팹·스폰 마커·맵 씬의 클라 전용 프로토타입 스크립트 정리
- **플랩을 누를 수단** — 현재 `PlayerInputManager`에 입력을 넣어 주는 것은 FlapWang 스코프가 등록하는 화면 게임패드 UI뿐이고, FlappyRace 스코프에는 게임 UI가 없다. 즉 B2-b가 끝나도 **사람이 플랩을 시킬 방법이 없다**. B2-d 착수 시 이걸 먼저 확인할 것.

---

### Task 1: `TbFlappyConfig` 마스터데이터 테이블

**Files:**
- Create: `/Users/insoobae/workspace/LOP/infrastructure/table/Datas/#FlappyConfig.xlsx`
- Modify: `/Users/insoobae/workspace/LOP/infrastructure/table/Datas/__tables__.xlsx` (15행 추가)
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime/Scripts/LOPMasterData.cs` (수기 로더 목록 `TableFiles`)
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime/Scripts/LOPMasterData.cs` (수기 로더 목록 `TableFiles`)
- Generated (커밋 대상): `/Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime.Generated/**`, `/Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/**`

**Interfaces:**
- Consumes: 없음 (첫 태스크)
- Produces: 양쪽 MasterData 패키지에 `LOP.MasterData.FlappyConfig` 클래스(프로퍼티 `Id`·`ForwardSpeed`·`FlapImpulse`·`Gravity`·`MaxFallSpeed`·`BodyRadius`·`BodyHeight`·`Restitution`)와 `md.Tables.TbFlappyConfig.GetOrDefault(1)` 조회. Task 2의 `FlappyConfigProvider`가 이 이름들을 그대로 쓴다.

**배경 — Luban 스키마 xlsx가 어떻게 생겼나**

`__tables__.xlsx`의 A열은 태그 열(`##var`)이고 실제 데이터는 **B열부터**다. 기존 행 예시(8행):

```
B8=TbCombatConfig  C8=CombatConfig  D8=TRUE  E8=#CombatConfig.xlsx  F8=id  G8=map  H8=s  I8=주석
                                                                              └ group
```

**H열(group)이 비면 `luban.conf`의 default 그룹(`c`,`s`)에 들어간다** — 즉 클라·서버 양쪽 패키지에 생성된다. 우리가 원하는 것이 정확히 이것이다(`TbCombatConfig`는 `s`라 서버 전용, `TbCharacter`는 비어 있어 양쪽).

- [ ] **Step 1: xlsx를 만들 수 있는 파이썬 환경 준비**

이 맥의 시스템 파이썬에는 `openpyxl`이 없다. 스크래치패드에 가상환경을 만든다(저장소를 건드리지 않는다).

```bash
cd /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad
python3 -m venv xlsxvenv
./xlsxvenv/bin/pip install -q openpyxl
./xlsxvenv/bin/python -c "import openpyxl; print('OK', openpyxl.__version__)"
```

기대: `OK 3.1.5`. (이미 만들어져 있으면 마지막 줄만 돌려 확인하고 넘어간다.)

- [ ] **Step 2: 브랜치 생성 (infrastructure)**

```bash
cd /Users/insoobae/workspace/LOP/infrastructure
git status --short
git fetch origin
git checkout -b feature/flappy-b2b-sim-core origin/main
```

`git status --short`가 뭔가를 보여주면 **멈추고 보고한다** — 남의 작업물일 수 있다.

- [ ] **Step 3: 데이터 시트 `#FlappyConfig.xlsx` 생성 + `__tables__.xlsx`에 행 추가**

```bash
cd /Users/insoobae/workspace/LOP/infrastructure/table
/private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/xlsxvenv/bin/python - <<'PY'
import openpyxl

# ── 1) 데이터 시트: #CombatConfig.xlsx와 똑같은 4줄 헤더 + 1행 데이터 ──────────
cols = ["id", "forward_speed", "flap_impulse", "gravity", "max_fall_speed",
        "body_radius", "body_height", "restitution"]
types = ["int", "float", "float", "float", "float", "float", "float", "float"]
values = [1, 11.0, 23.0, 70.0, 30.0, 0.45, 0.9, 0.35]

wb = openpyxl.Workbook()
ws = wb.active
ws.title = "Sheet"
ws.cell(row=1, column=1, value="##var")
ws.cell(row=2, column=1, value="##type")
ws.cell(row=3, column=1, value="##group")
ws.cell(row=4, column=1, value="##")
for i, (c, t, v) in enumerate(zip(cols, types, values)):
    col = i + 2                      # A열은 태그 열이라 데이터는 B열부터
    ws.cell(row=1, column=col, value=c)
    ws.cell(row=2, column=col, value=t)
    ws.cell(row=4, column=col, value=c)
    ws.cell(row=5, column=col, value=v)
wb.save("Datas/#FlappyConfig.xlsx")

# ── 2) 테이블 목록에 한 행 추가 (H열=group을 비워 클·서 양쪽에 생성) ──────────
wb2 = openpyxl.load_workbook("Datas/__tables__.xlsx")
ws2 = wb2.active
r = ws2.max_row + 1
ws2.cell(row=r, column=2, value="TbFlappyConfig")
ws2.cell(row=r, column=3, value="FlappyConfig")
ws2.cell(row=r, column=4, value=True)
ws2.cell(row=r, column=5, value="#FlappyConfig.xlsx")
ws2.cell(row=r, column=6, value="id")
ws2.cell(row=r, column=7, value="map")
# H열(group)은 비워 둔다 — 비면 default 그룹(c,s) = 클라·서버 양쪽
ws2.cell(row=r, column=9, value="FlappyConfig(Flappy Race 튜닝, 클·서 공용)")
wb2.save("Datas/__tables__.xlsx")
print("wrote row", r)
PY
```

기대: `wrote row 15`

- [ ] **Step 4: 생성 전 상태를 찍어 둔다 (되돌릴 지점)**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Client && git status --short && git checkout -b feature/flappy-b2b-sim-core origin/main
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Server && git status --short && git checkout -b feature/flappy-b2b-sim-core origin/main
```

어느 쪽이든 `git status --short`가 비어 있지 않으면 **멈추고 보고한다**.

- [ ] **Step 5: Luban 생성 실행**

```bash
cd /Users/insoobae/workspace/LOP/infrastructure/table
./gen.sh
```

기대: `[gen] target=client ...`, `[gen] target=server ...`, `[gen] target=matchmaking ...`, `[done]`.

**실패하면** — `openpyxl`이 다시 저장한 `__tables__.xlsx`를 Luban이 못 읽는 경우가 있을 수 있다. 그때는 되돌리고 보고한다:

```bash
cd /Users/insoobae/workspace/LOP/infrastructure && git checkout -- table/Datas/__tables__.xlsx
```

- [ ] **Step 6: 생성물 검증**

```bash
cd /Users/insoobae/workspace/LOP
for R in LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server; do
  echo "=== $R"
  grep -n "ForwardSpeed\|FlapImpulse\|MaxFallSpeed\|BodyRadius\|BodyHeight\|Restitution" \
       "$R/Runtime.Generated/Scripts/MasterData/FlappyConfig.cs" | head
  ls -l "$R/Runtime.Generated/StreamingAssets/MasterData/tbflappyconfig.bytes"
  git -C "$R" status --short | head -20
done
```

기대: 양쪽 모두 `FlappyConfig.cs`에 여섯 프로퍼티가 있고, `tbflappyconfig.bytes`가 존재하고, `git status`에 `FlappyConfig.cs`·`tbflappyconfig.bytes`가 **새 파일**로, `Tables.cs`가 **수정**으로 뜬다.

**기존 테이블의 `.cs`/`.bytes`가 내용까지 바뀌어 있으면 멈추고 보고한다** — 이 태스크는 테이블 하나를 더하는 것이지 기존 것을 바꾸는 것이 아니다. (`git diff --stat`으로 확인.)

- [ ] **Step 7: 로더 목록에 새 테이블을 등록한다 (빠뜨리기 쉬운 함정)**

각 MasterData 패키지의 `LOPMasterData.cs`에는 **손으로 관리하는 테이블 파일 목록**(`TableFiles`)이 있다. 새 테이블을 여기 안 넣으면 게임이 시작 단계에서 `KeyNotFoundException`으로 죽는다 — 컴파일도 통과하고 다른 테스트도 통과하기 때문에 이 목록이 유일한 그물이다(그 파일 주석에 2026-07-26 실제 사고가 적혀 있다).

`/Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime/Scripts/LOPMasterData.cs`의 목록에 `"tbflappyconfig"`를 더한다:

```csharp
        public static readonly System.Collections.Generic.IReadOnlyList<string> TableFiles = new[]
        {
            "tbcharacter", "tbskin", "tbskinasset", "tbitem", "tbstatuseffect", "tbability",
            "tbcharacterloadout", "tbabilityview", "tbstatuseffectview",
            "tbgamemode", "tbmap", "tbqueue", "tbflappyconfig"
        };
```

`/Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime/Scripts/LOPMasterData.cs`도 같이 (서버 목록은 내용이 다르니 **끝에 더하기만** 한다):

```csharp
        public static readonly System.Collections.Generic.IReadOnlyList<string> TableFiles = new[]
        {
            "tbcharacter", "tbskin", "tbitem", "tbstatuseffect", "tbability", "tbcombatconfig",
            "tbcharacterloadout",
            "tbgamemode", "tbmap", "tbqueue", "tbflappyconfig"
        };
```

- [ ] **Step 8: 유니티에 임포트시켜 `.meta` 생성**

`.meta`는 유니티만 만든다. 클라 에디터가 떠 있으면 그대로 쓴다:

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd refresh
```

**⚠️ 플레이 모드면 컴파일이 끝나지 않는다.** 먼저 확인한다:

```bash
echo 'return UnityEditor.EditorApplication.isPlaying;' > /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/is-playing.cs
unity cmd eval_file --file /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/is-playing.cs
```

`True`면 사용자에게 정지를 요청한다 — 임의로 끄면 플레이 중 편집분이 날아간다.

에디터가 없거나 `unity`가 안 붙으면 batchmode로 임포트한다:

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client \
  -logFile /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/import-client.log
```

**서버 패키지(`MasterData-Server`)의 `.meta`는 서버 프로젝트를 임포트해야 생긴다** — 위 명령의 `-projectPath`를 `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server`로 바꿔 한 번 더 돌린다.

- [ ] **Step 9: `.meta`가 생겼는지 확인**

```bash
ls -l /Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/FlappyConfig.cs.meta \
      /Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime.Generated/StreamingAssets/MasterData/tbflappyconfig.bytes.meta \
      /Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/FlappyConfig.cs.meta \
      /Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/StreamingAssets/MasterData/tbflappyconfig.bytes.meta
```

넷 다 있어야 한다. 없으면 임포트가 안 돈 것이니 Step 8을 다시 한다.

- [ ] **Step 10: 이미 있는 그물 테스트로 검증한다**

`TableFileManifestTests`가 "생성물에 있는 모든 테이블이 로더 목록에 있는가"를 검사한다. 이 패키지는 `Packages/manifest.json`의 `testables`에 들어 있어 클라 EditMode 실행에 함께 돈다.

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd run_tests
```

기대: 전체 실패 0. 특히 `LoaderListCoversEveryShippedTable`과 `LoaderListHasNoEntryWithoutData`가 통과.

**이 테스트가 진짜로 실패할 수 있는지 확인한다** — Step 7에서 더한 `"tbflappyconfig"`를 클라 쪽 목록에서 잠깐 지우고 다시 돌린다. `LoaderListCoversEveryShippedTable`이 *"생성물에 있는데 로더 목록에 없다"* 로 **실패해야 한다**. 확인했으면 되돌린다.

서버 패키지의 같은 테스트는 서버 프로젝트에서 돈다. 서버 에디터를 쓸 수 있으면 그쪽에서도 `run_tests`를 돌리고, 못 쓰면 batchmode로:

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -runTests -testPlatform EditMode \
  -projectPath /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server \
  -logFile /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/server-tests.log \
  -testResults /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/server-tests.xml
grep -o 'result="[^"]*"' /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/server-tests.xml | head -1
```

기대: 첫 줄이 `result="Passed"`.

- [ ] **Step 11: 세 저장소에 커밋**

```bash
cd /Users/insoobae/workspace/LOP/infrastructure
git status --short
git add table/Datas/#FlappyConfig.xlsx table/Datas/__tables__.xlsx
git commit -m "feat(masterdata): Flappy Race 튜닝 테이블을 추가한다

새 전진·플랩·중력·몸 규격·반발계수를 기획이 엑셀로 조정한다. group을 비워
클·서 양쪽 패키지에 생성되게 했다 — 클라 예측이 서버와 같은 값을 써야 한다."

cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Client
git status --short
git add Runtime.Generated Runtime/Scripts/LOPMasterData.cs
git commit -m "feat(masterdata): TbFlappyConfig 생성물과 로더 등록"

cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Server
git status --short
git add Runtime.Generated Runtime/Scripts/LOPMasterData.cs
git commit -m "feat(masterdata): TbFlappyConfig 생성물과 로더 등록"
```

각 `git status --short`에 의도한 파일만 있는지 눈으로 확인하고 넘어간다.

---

### Task 2: `FlappyConfig`를 클·서 시뮬에 배달

**Files:**
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyConfig.cs`
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyConfigProvider.cs`
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyConfigProvider.cs`
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`

**Interfaces:**
- Consumes: Task 1의 `LOP.MasterData.FlappyConfig` 행(`md.Tables.TbFlappyConfig.GetOrDefault(1)`)과 그 프로퍼티들.
- Produces: `LOP.FlappyConfig` readonly struct — 필드 `ForwardSpeed`·`FlapImpulse`·`Gravity`·`MaxFallSpeed`·`BodyRadius`·`BodyHeight`·`Restitution` (전부 `float`), 생성자 인자 순서도 그 순서. Task 4·5·6이 주입받는다. 양쪽 `FlappyRaceLifetimeScope`에 `FlappyConfig`가 Singleton으로 등록된다.

**왜 이렇게 나눠져 있나:** LOP-Shared는 MasterData 패키지를 참조하지 않는다(클·서가 서로 다른 패키지를 보고, 그 둘은 서로 참조하지 않는다 — 서버 전용 컬럼이 클라 코드에 보이지 않게 하려는 격리다). 그래서 **Shared는 순수 struct를 받기만 하고, Luban 행을 그 struct로 옮기는 어댑터는 각 사이드에 둔다.** 이미 `CombatConfig`(Shared) ↔ `CombatConfigProvider`(서버)라는 같은 짝이 있으니 그 모양을 그대로 따른다.

- [ ] **Step 1: 세 저장소에 브랜치 생성**

```bash
for R in LeagueOfPhysical-Shared LeagueOfPhysical-Client LeagueOfPhysical-Server; do
  cd "/Users/insoobae/workspace/LOP/$R"
  echo "=== $R"; git status --short
  git fetch origin && git checkout -b feature/flappy-b2b-sim-core origin/main
done
```

유니티 저장소(Client/Server)는 커밋하지 않는 로컬 픽스처가 남아 있는 게 정상이다(`Assets/Art` 서브모듈 포인터, `Assets/UI/Theme/Fonts/Jua-Regular SDF.asset`, `ProjectSettings/ProjectSettings.asset`). **그 셋 말고 다른 게 보이면 멈추고 보고한다.** 브랜치 전환이 픽스처 때문에 거부되면 `git stash push -u -m flappy-b2b`로 빼두고 전환한 뒤 `git stash pop` 한다.

- [ ] **Step 2: `FlappyConfig` 작성**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyConfig.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// Flappy Race 튜닝값. MasterData <c>TbFlappyConfig</c>에서 사이드 provider가 채워 시뮬에 주입한다.
    /// Shared는 MasterData 패키지를 참조하지 않으므로 순수 struct로 건네받는다(<see cref="CombatConfig"/>와 같은 짝).
    /// </summary>
    public readonly struct FlappyConfig
    {
        /// <summary>고정 전진 속도(+X). 플레이어가 바꿀 수 없다 — 이 게임의 전진은 조작 대상이 아니다.</summary>
        public readonly float ForwardSpeed;

        /// <summary>플랩 순간의 세로 속도. 지금까지의 세로 속도를 덮어쓴다.</summary>
        public readonly float FlapImpulse;

        /// <summary>중력 가속도(아래로 당기는 크기라 양수).</summary>
        public readonly float Gravity;

        /// <summary>낙하 속도 상한(양수). 이보다 빠르게 떨어지지 않는다.</summary>
        public readonly float MaxFallSpeed;

        /// <summary>새 몸 캡슐의 반지름. 맵 충돌과 새끼리 몸싸움이 같은 값을 쓴다.</summary>
        public readonly float BodyRadius;

        /// <summary>새 몸 캡슐의 전체 높이(발밑부터 정수리까지).</summary>
        public readonly float BodyHeight;

        /// <summary>몸싸움 반발계수 — 0이면 부딪힌 자리에 얹히고, 1이면 온전히 튕겨 나간다.</summary>
        public readonly float Restitution;

        public FlappyConfig(float forwardSpeed, float flapImpulse, float gravity, float maxFallSpeed,
                            float bodyRadius, float bodyHeight, float restitution)
        {
            ForwardSpeed = forwardSpeed;
            FlapImpulse = flapImpulse;
            Gravity = gravity;
            MaxFallSpeed = maxFallSpeed;
            BodyRadius = bodyRadius;
            BodyHeight = bodyHeight;
            Restitution = restitution;
        }
    }
}
```

- [ ] **Step 3: 양쪽 `FlappyConfigProvider` 작성**

**클라와 서버에 같은 내용의 파일을 각각** 만든다(`LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyConfigProvider.cs`, `LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyConfigProvider.cs`). 두 저장소가 서로 다른 MasterData 패키지를 보므로 한 곳에 둘 수 없다 — `AbilityDataProvider`가 양쪽에 있는 것과 같은 이유다.

```csharp
namespace LOP
{
    /// <summary>
    /// Luban <c>TbFlappyConfig</c>(전역 단일 행, id=1)을 LOP-Shared <see cref="FlappyConfig"/>로 옮기는
    /// 사이드 로컬 어댑터. (Shared는 MasterData 패키지 비참조 → 여기서 변환. <see cref="AbilityDataProvider"/> 대칭.)
    /// </summary>
    public class FlappyConfigProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;

        public FlappyConfigProvider(LOP.MasterData.LOPMasterData md)
        {
            this.md = md;
        }

        public FlappyConfig Get()
        {
            // 없으면 Luban의 애매한 KeyNotFoundException 대신 원인을 짚어 크게 실패
            var r = md.Tables.TbFlappyConfig.GetOrDefault(1);
            if (r == null)
            {
                throw new System.InvalidOperationException(
                    "TbFlappyConfig id=1 행을 찾을 수 없음 — MasterData 미로드 또는 FlappyConfig 데이터 누락");
            }
            return new FlappyConfig(
                r.ForwardSpeed, r.FlapImpulse, r.Gravity, r.MaxFallSpeed,
                r.BodyRadius, r.BodyHeight, r.Restitution);
        }
    }
}
```

- [ ] **Step 4: 양쪽 스코프에 등록**

클라 `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`의 `ConfigureGame` 안, `builder.RegisterComponent(cameraController);` **바로 다음 줄**에 두 줄을 넣는다:

```csharp
            builder.Register<FlappyConfigProvider>(Lifetime.Singleton);
            builder.Register<FlappyConfig>(c => c.Resolve<FlappyConfigProvider>().Get(), Lifetime.Singleton);
```

서버 `LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`의 `ConfigureGame` **첫 두 줄**로 같은 두 줄을 넣는다.

- [ ] **Step 5: 컴파일 확인 + `.meta` 생성**

```bash
export PATH="$HOME/.unity/bin:$PATH"
unity cmd recompile && unity cmd console | tail -30
```

기대: 콘솔에 `error CS`가 없다. 서버 프로젝트도 같은 방식으로 확인한다(에디터가 떠 있지 않으면 Task 1 Step 7의 batchmode 임포트 명령으로 `-projectPath`만 바꿔 돌리고 로그에서 `error CS`를 찾는다).

**⚠️ 플레이 모드면 `recompile`이 끝나지 않는다.** 먼저 확인한다:

```bash
echo 'return UnityEditor.EditorApplication.isPlaying;' > /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/is-playing.cs
unity cmd eval_file --file /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/is-playing.cs
```

`True`면 사용자에게 정지를 요청한다 — 임의로 끄면 플레이 중 편집분이 날아간다.

- [ ] **Step 6: 새 파일의 `.meta`가 생겼는지 확인**

```bash
ls -l /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyConfig.cs.meta \
      /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyConfigProvider.cs.meta \
      /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyConfigProvider.cs.meta
```

셋 다 있어야 한다. (Shared는 패키지라 클라 임포트에서 생긴다. 서버 쪽은 서버 프로젝트 임포트가 필요하다.)

- [ ] **Step 7: 튜닝값이 제자리에 실렸는지 눈으로 확인한다**

컴파일만으로는 **열 순서가 어긋난 실수**(예: `gravity` 칸에 `flap_impulse` 값)를 못 잡는다.
숫자가 뒤바뀐 채로 넘어가면 B2-b가 끝나도 안 드러나고 B2-d 런타임에서야 이상하게 난다.

생성된 `.bytes`를 직접 열어 값을 찍어 본다. (`LOPMasterData.LoadAsync`는 `UnityWebRequest`
비동기라 eval의 5초 메인스레드 제한에 맞지 않는다 — Luban `Tables`를 파일에서 바로 만든다.)

```bash
cat > /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/read-flappy-config.cs <<'EOF'
var dir = System.IO.Path.GetFullPath(
    "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData");
var tables = new LOP.MasterData.Tables(
    file => new Luban.ByteBuf(System.IO.File.ReadAllBytes(System.IO.Path.Combine(dir, file + ".bytes"))));
var row = tables.TbFlappyConfig.GetOrDefault(1);
if (row == null) return "FAIL: TbFlappyConfig id=1 없음";
var config = new LOP.FlappyConfig(row.ForwardSpeed, row.FlapImpulse, row.Gravity, row.MaxFallSpeed,
                                  row.BodyRadius, row.BodyHeight, row.Restitution);
return $"forward={config.ForwardSpeed} flap={config.FlapImpulse} gravity={config.Gravity} "
     + $"maxFall={config.MaxFallSpeed} radius={config.BodyRadius} height={config.BodyHeight} "
     + $"restitution={config.Restitution}";
EOF
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd eval_file --file /private/tmp/claude-501/-Users-insoobae-workspace-LOP-LeagueOfPhysical-Client/5a5f749e-5f0e-4c69-9489-a8c0eff09e74/scratchpad/read-flappy-config.cs
```

기대: `forward=11 flap=23 gravity=70 maxFall=30 radius=0.45 height=0.9 restitution=0.35`

**하나라도 다르면 멈추고 보고한다** — Task 1의 엑셀 열 순서가 틀렸다는 뜻이다.

`unity`가 안 붙으면 이 스텝은 건너뛰되, 건너뛰었다는 사실을 보고에 반드시 적는다.

- [ ] **Step 8: 커밋 (3개 저장소)**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/FlappyConfig.cs Runtime/Scripts/Game/FlappyConfig.cs.meta
git commit -m "feat(flappy): 튜닝값을 담을 그릇을 만든다

Shared는 MasterData 패키지를 참조하지 않으므로 사이드가 채워 주는 순수 struct로
받는다 — CombatConfig와 같은 짝."

cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Game/FlappyConfigProvider.cs Assets/Scripts/Game/FlappyConfigProvider.cs.meta Assets/Scripts/Game/FlappyRaceLifetimeScope.cs
git commit -m "feat(flappy): 마스터데이터 튜닝값을 시뮬에 넣어 준다"

cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add Assets/Scripts/Game/FlappyConfigProvider.cs Assets/Scripts/Game/FlappyConfigProvider.cs.meta Assets/Scripts/Game/FlappyRaceLifetimeScope.cs
git commit -m "feat(flappy): 마스터데이터 튜닝값을 시뮬에 넣어 준다"
```

---

### Task 3: `FlappyBounce`를 공유 코드로 옮긴다

**Files:**
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyBounce.cs`
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/FlappyBounceTests.cs`
- Delete: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/FlappyRaceSlice/Logic/FlappyBounce.cs` (+ `.meta`)
- Delete: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Tests/EditMode/FlappyBounceTests.cs` (+ `.meta`)
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/FlappyRaceSlice/FlappyBird.cs`

**Interfaces:**
- Consumes: 없음.
- Produces: `LOP.FlappyBounce` — `public const float RestingSpeed = 1.5f`, `public static float ResolveVy(float vySelf, float vyOther, float normalY, float restitution)`. Task 5가 부른다.

**무엇을 옮기는가:** 프로토타입에서 검증된 규칙(부딪힌 두 새가 세로 속도를 주고받는 계산)을 **로직 변경 없이** 클라 프로토타입 폴더에서 LOP-Shared로 옮긴다. 네임스페이스만 `FlappyRace` → `LOP`으로 바꾼다. 테스트 8개도 함께 옮긴다.

프로토타입 스크립트(`FlappyBird.cs`)는 B2-d에서 걷어낼 때까지 살아 있어야 하므로, 옮긴 뒤 그쪽이 새 위치를 가리키게 고친다. `FlappyBird.cs`가 사는 `Assembly-CSharp`은 LOP-Shared를 자동 참조하므로 `using`만 바꾸면 된다.

- [ ] **Step 1: 옮긴 테스트를 먼저 놓고 빨간 것을 확인한다**

`LeagueOfPhysical-Shared/Tests/EditMode/FlappyBounceTests.cs` — 클라 원본을 그대로 옮기되 네임스페이스만 맞춘다:

```csharp
using NUnit.Framework;

namespace LOP.Tests
{
    public class FlappyBounceTests
    {
        const float E = 0.35f;   // TbFlappyConfig의 restitution 기본값

        // 위에 있는 새(normalY=+1)와 아래 있는 새(normalY=-1)가 같은 충돌을 각자 계산한다.
        static void Exchange(float vyUpper, float vyLower, float e, out float upperAfter, out float lowerAfter)
        {
            upperAfter = FlappyBounce.ResolveVy(vyUpper, vyLower, 1f, e);
            lowerAfter = FlappyBounce.ResolveVy(vyLower, vyUpper, -1f, e);
        }

        [Test]
        public void 떨어지며_부딪히면_위는_덜_떨어지고_아래는_더_밀린다()
        {
            Exchange(-10f, 0f, E, out float upper, out float lower);

            Assert.AreEqual(-3.25f, upper, 1e-4f);   // ((1-e)·-10 + (1+e)·0) / 2
            Assert.AreEqual(-6.75f, lower, 1e-4f);   // ((1-e)·0 + (1+e)·-10) / 2
        }

        [Test]
        public void 운동량이_보존된다()
        {
            Exchange(-10f, 0f, E, out float upper, out float lower);

            Assert.AreEqual(-10f, upper + lower, 1e-4f);
        }

        [Test]
        public void 반발계수만큼_다시_멀어진다()
        {
            Exchange(-10f, 0f, E, out float upper, out float lower);

            float closingBefore = -10f - 0f;
            float separatingAfter = upper - lower;
            Assert.AreEqual(E * -closingBefore, separatingAfter, 1e-4f);
        }

        [Test]
        public void 이미_멀어지는_중이면_속도를_건드리지_않는다()
        {
            // 위 새가 위로 올라가는 중 — 부딪힌 게 아니라 떨어지고 있다
            Assert.AreEqual(5f, FlappyBounce.ResolveVy(5f, 0f, 1f, E), 1e-4f);
        }

        [Test]
        public void 옆으로_스치면_세로_속도가_안_바뀐다()
        {
            Assert.AreEqual(-10f, FlappyBounce.ResolveVy(-10f, 0f, 0f, E), 1e-4f);
        }

        [Test]
        public void 느리게_닿으면_튕기지_않고_얹힌다()
        {
            // 접근 속도가 RestingSpeed 미만이면 반발 0 = 완전 비탄성 → 두 속도가 같아진다
            Exchange(-1f, 0f, E, out float upper, out float lower);

            Assert.Less(1f, FlappyBounce.RestingSpeed);
            Assert.AreEqual(-0.5f, upper, 1e-4f);
            Assert.AreEqual(-0.5f, lower, 1e-4f);
            Assert.AreEqual(upper, lower, 1e-4f);   // 같은 속도 = 더 이상 파고들지 않음
        }

        [Test]
        public void 얹힌_뒤에는_중력이_밀어넣는_만큼만_흡수한다()
        {
            // 한 프레임 중력(70 × 1/60 ≈ 1.17)으로 다시 다가와도 튕기지 않고 흡수된다
            float gravityStep = -70f / 60f;
            Exchange(gravityStep, 0f, E, out float upper, out float lower);

            Assert.AreEqual(upper, lower, 1e-4f);
            Assert.AreEqual(gravityStep * 0.5f, upper, 1e-4f);
        }

        [Test]
        public void 비스듬히_부딪히면_정면보다_약하게_주고받는다()
        {
            float straight = FlappyBounce.ResolveVy(-10f, 0f, 1f, E);
            float glancing = FlappyBounce.ResolveVy(-10f, 0f, 0.5f, E);

            Assert.Greater(straight, -10f);            // 정면은 크게 바뀌고
            Assert.Greater(glancing, -10f);            // 비스듬해도 바뀌긴 하지만
            Assert.Less(glancing, straight);           // 정면보다는 적게 바뀐다
        }
    }
}
```

- [ ] **Step 2: 컴파일이 깨지는 것을 확인한다**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client && unity cmd recompile; unity cmd console | grep "error CS" | head
```

기대: `LOP.FlappyBounce`가 없다는 `error CS0103`/`CS0117` 계열 오류. **오류가 안 나면 뭔가 잘못된 것이니 멈춘다** — 테스트가 실패할 수 있음을 확인하는 것이 이 스텝의 목적이다.

- [ ] **Step 3: `FlappyBounce`를 Shared에 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyBounce.cs` — 클라 원본과 본문 동일, 네임스페이스만 `LOP`:

```csharp
namespace LOP
{
    /// <summary>
    /// 새 두 마리가 부딪혔을 때 세로 속도를 주고받는 계산(질량은 서로 같다고 본다).
    ///
    /// 부딪힌 속도를 0으로 지우면 위에 있는 새가 아래 새를 발판처럼 밟고 서게 되고,
    /// 중력이 곧바로 다시 붙여서 매 프레임 재충돌한다. 서로 속도를 주고받아야 갈라진다.
    ///
    /// 전진 속도는 상수로 고정돼 손댈 수 없으므로 세로 성분만 오간다.
    /// </summary>
    public static class FlappyBounce
    {
        /// <summary>이보다 느리게 다가온 충돌은 튕기지 않는다 — 얹혀 있을 때 미세하게 떠는 걸 막는다.</summary>
        public const float RestingSpeed = 1.5f;

        /// <summary>
        /// 충돌 후 self의 세로 속도.
        /// <paramref name="normalY"/>는 self를 상대 밖으로 밀어내는 방향의 세로 성분(-1~1)이다.
        /// 옆에서 스치면 0에 가까워져 세로 속도가 거의 안 바뀐다.
        /// </summary>
        public static float ResolveVy(float vySelf, float vyOther, float normalY, float restitution)
        {
            float closing = (vySelf - vyOther) * normalY;
            if (closing >= 0f) return vySelf;   // 이미 멀어지는 중이면 건드리지 않는다

            float e = -closing < RestingSpeed ? 0f : restitution;
            return vySelf - (1f + e) * closing * 0.5f * normalY;
        }
    }
}
```

- [ ] **Step 4: 프로토타입 원본을 지우고 참조를 새 위치로 돌린다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git rm Assets/Scripts/FlappyRaceSlice/Logic/FlappyBounce.cs Assets/Scripts/FlappyRaceSlice/Logic/FlappyBounce.cs.meta
git rm Assets/Tests/EditMode/FlappyBounceTests.cs Assets/Tests/EditMode/FlappyBounceTests.cs.meta
```

`Assets/Scripts/FlappyRaceSlice/FlappyBird.cs` 3행의

```csharp
using FlappyRace;
```

을 지운다. 같은 파일 53행의 호출은 `LOP.FlappyBounce`로 바꾼다:

```csharp
                newVy = LOP.FlappyBounce.ResolveVy(newVy, otherBird.Vy, dir.y, Restitution);
```

- [ ] **Step 5: 컴파일 + 테스트 통과 확인**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd console | grep -c "error CS"
unity cmd run_tests
```

기대: `error CS` 0건. `run_tests`는 EditMode 전체(약 500개)를 돌린다 — `FlappyBounceTests` 8개가 **`LOP.Tests` 네임스페이스 아래에서** 통과하고, 전체 실패가 0이어야 한다.

- [ ] **Step 6: 커밋 (2개 저장소)**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/FlappyBounce.cs Runtime/Scripts/Game/FlappyBounce.cs.meta \
        Tests/EditMode/FlappyBounceTests.cs Tests/EditMode/FlappyBounceTests.cs.meta
git commit -m "feat(flappy): 몸싸움 속도 교환 규칙을 공유 코드로 옮긴다

프로토타입에서 손맛이 검증된 계산이라 로직은 그대로 두고 자리만 옮겼다.
클·서가 같은 코드를 돌려야 예측이 권위와 갈리지 않는다."

cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
# 삭제는 위 git rm이 이미 스테이지했다 — 여기서는 고친 파일 하나만 더한다
git add Assets/Scripts/FlappyRaceSlice/FlappyBird.cs
git commit -m "refactor(flappy): 프로토타입이 공유 몸싸움 규칙을 쓰게 한다"
```

---

### Task 4: `FlappyMoveSystem` — 중력·플랩·고정 전진

**Files:**
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyMoveSystem.cs`
- Test: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/FlappyMoveSystemTests.cs`

**Interfaces:**
- Consumes: `LOP.FlappyConfig`(Task 2) — `ForwardSpeed`·`FlapImpulse`·`Gravity`·`MaxFallSpeed`. 기존 `LOP.InputBuffer` 컴포넌트의 `Current`(`InputCommand`, 필드 `Jump`).
- Produces: `LOP.FlappyMoveSystem` — 생성자 `FlappyMoveSystem(FlappyConfig config)`, 메서드 `public void Tick(GameFramework.World.Entity entity, float deltaTime)`. Task 6이 부른다.

**한 틱에 무슨 일이 일어나나** (스펙 §5의 의사코드 그대로):

```
vy -= gravity · dt        →  낙하 상한(-max_fall_speed)에서 멈춤
플랩을 눌렀으면 vy = flap_impulse     ← 이번 틱 중력을 덮어쓴다(낙하 중에도 같은 높이로 뜬다)
vx = forward_speed (상수) ·  vz = 0
```

플랩이 중력 **뒤에** 오는 것이 중요하다. 앞에 두면 낙하 중 플랩이 그 틱 중력만큼 손해를 봐서 높이가 입력 타이밍에 따라 흔들린다.

`InputCommand.Jump`는 이미 **한 틱짜리 신호**다(`PlayerInputManager`가 `pendingJump`를 매 틱 소비하고 false로 되돌린다). 그래서 누르고 있어도 계속 뜨지 않는다 — 여기서 따로 눌림 판정을 할 필요가 없다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/FlappyMoveSystemTests.cs`:

```csharp
using GameFramework;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class FlappyMoveSystemTests
    {
        const float Tolerance = 1e-4f;

        // TbFlappyConfig 기본값
        static FlappyConfig Config()
            => new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f);

        static Entity Bird(Vector3 velocity, bool? jump = null)
        {
            var entity = new Entity("bird-1");
            entity.Add(new GameFramework.World.Transform());
            entity.Add(new Velocity { Linear = velocity.ToNumerics() });
            if (jump.HasValue)
            {
                var buffer = new InputBuffer();
                buffer.Current = new InputCommand { Jump = jump.Value };
                entity.Add(buffer);
            }
            return entity;
        }

        static Vector3 VelocityOf(Entity entity) => entity.Get<Velocity>().Linear.ToUnity();

        [Test]
        public void 중력이_세로_속도를_깎는다()
        {
            var bird = Bird(Vector3.zero, jump: false);

            new FlappyMoveSystem(Config()).Tick(bird, 0.1f);

            Assert.AreEqual(-7f, VelocityOf(bird).y, Tolerance);   // 70 × 0.1
        }

        [Test]
        public void 낙하_속도가_상한을_넘지_않는다()
        {
            var bird = Bird(new Vector3(0f, -30f, 0f), jump: false);

            new FlappyMoveSystem(Config()).Tick(bird, 0.1f);

            Assert.AreEqual(-30f, VelocityOf(bird).y, Tolerance);
        }

        [Test]
        public void 플랩은_낙하를_지우고_늘_같은_높이로_띄운다()
        {
            var falling = Bird(new Vector3(0f, -25f, 0f), jump: true);
            var rising = Bird(new Vector3(0f, 5f, 0f), jump: true);

            new FlappyMoveSystem(Config()).Tick(falling, 0.1f);
            new FlappyMoveSystem(Config()).Tick(rising, 0.1f);

            // 눌렀을 때의 세로 속도와 무관하게 같은 값 — 그래야 플랩 높이가 예측 가능하다
            Assert.AreEqual(23f, VelocityOf(falling).y, Tolerance);
            Assert.AreEqual(23f, VelocityOf(rising).y, Tolerance);
        }

        [Test]
        public void 전진_속도는_상수로_고정된다()
        {
            var bird = Bird(new Vector3(999f, 0f, 999f), jump: false);
            bird.Get<InputBuffer>().Current.Horizontal = 1f;   // 좌우 입력을 넣어도
            bird.Get<InputBuffer>().Current.Vertical = 1f;

            new FlappyMoveSystem(Config()).Tick(bird, 0.1f);

            Assert.AreEqual(11f, VelocityOf(bird).x, Tolerance);   // 전진은 조작 대상이 아니다
            Assert.AreEqual(0f, VelocityOf(bird).z, Tolerance);
        }

        [Test]
        public void 입력이_아예_없어도_중력과_전진은_돈다()
        {
            var bird = Bird(Vector3.zero);   // InputBuffer 없음 — 서버가 조종하지 않는 새

            new FlappyMoveSystem(Config()).Tick(bird, 0.1f);

            Assert.AreEqual(-7f, VelocityOf(bird).y, Tolerance);
            Assert.AreEqual(11f, VelocityOf(bird).x, Tolerance);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client && unity cmd recompile; unity cmd console | grep "error CS" | head -3
```

기대: `FlappyMoveSystem`이라는 이름이 없다는 컴파일 오류. 오류가 없으면 멈춘다.

- [ ] **Step 3: `FlappyMoveSystem`을 구현한다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyMoveSystem.cs`:

```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 새 한 마리의 이번 틱 속도를 정한다. 전진은 상수로 고정이고, 세로만 중력과 플랩이 바꾼다.
    /// 클·서가 같은 구체 클래스를 돌려 예측이 권위와 갈리지 않는다.
    /// </summary>
    public class FlappyMoveSystem
    {
        private readonly FlappyConfig config;

        public FlappyMoveSystem(FlappyConfig config)
        {
            this.config = config;
        }

        public void Tick(GameFramework.World.Entity entity, float deltaTime)
        {
            var worldVelocity = entity.Get<GameFramework.World.Velocity>();
            if (worldVelocity == null)
            {
                return;   // 이동 없는 엔티티
            }

            Vector3 velocity = worldVelocity.Linear.ToUnity();

            velocity.y -= config.Gravity * deltaTime;
            if (velocity.y < -config.MaxFallSpeed)
            {
                velocity.y = -config.MaxFallSpeed;
            }

            // 플랩은 지금까지의 세로 속도를 지우고 새로 준다 — 낙하 중에 눌러도 늘 같은 높이로 뜬다.
            // 중력 다음에 오는 것이 중요하다. 앞에 두면 누른 틱의 중력만큼 손해를 봐서 높이가 흔들린다.
            var input = entity.Get<InputBuffer>()?.Current;
            if (input != null && input.Jump)
            {
                velocity.y = config.FlapImpulse;
            }

            // 전진은 플레이어가 바꿀 수 없는 상수다. z를 0으로 붙잡아 코스 밖으로 새지 않게 한다.
            velocity.x = config.ForwardSpeed;
            velocity.z = 0f;

            worldVelocity.Linear = velocity.ToNumerics();
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd console | grep -c "error CS"
unity cmd run_tests
```

기대: `error CS` 0건, `FlappyMoveSystemTests` 5개 통과, 전체 실패 0.

- [ ] **Step 5: 테스트가 진짜로 실패할 수 있는지 확인한다**

`FlappyMoveSystem.cs`에서 `velocity.x = config.ForwardSpeed;`를 `velocity.x = 0f;`로 잠깐 바꾸고 테스트를 돌린다. `전진_속도는_상수로_고정된다`와 `입력이_아예_없어도_중력과_전진은_돈다`가 **실패해야 한다**. 확인했으면 되돌린다.

통과만 보고 "검증됐다"고 하지 않는다 — 일부러 깨뜨려 본다.

- [ ] **Step 6: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/FlappyMoveSystem.cs Runtime/Scripts/Game/FlappyMoveSystem.cs.meta \
        Tests/EditMode/FlappyMoveSystemTests.cs Tests/EditMode/FlappyMoveSystemTests.cs.meta
git commit -m "feat(flappy): 중력·플랩·고정 전진으로 새의 속도를 정한다

플랩을 중력 뒤에 둔다 — 앞에 두면 누른 틱의 중력만큼 손해를 봐서
플랩 높이가 입력 타이밍에 따라 흔들린다."
```

---

### Task 5: 새끼리 몸싸움 — 겹침 기하 + 충돌 응답

**Files:**
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyBodyOverlap.cs`
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyBodyCollisionSystem.cs`
- Test: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/FlappyBodyOverlapTests.cs`
- Test: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/FlappyBodyCollisionSystemTests.cs`

**Interfaces:**
- Consumes: `LOP.FlappyConfig`(Task 2) — `BodyRadius`·`BodyHeight`·`Restitution`. `LOP.FlappyBounce.ResolveVy`(Task 3).
- Produces:
  - `LOP.FlappyBodyOverlap` (static) — `public static bool TryCompute(Vector3 a, Vector3 b, float radius, float height, out Vector3 pushDir, out float depth)`. `pushDir`은 **a를 b 밖으로 밀어내는** 단위 방향, `depth`는 겹친 깊이.
  - `LOP.FlappyBodyCollisionSystem` — 생성자 `FlappyBodyCollisionSystem(FlappyConfig config)`, 메서드 `public void Resolve(IReadOnlyList<GameFramework.World.Entity> birds)`. Task 6이 부른다.

**왜 둘로 나누나:** 겹침 기하는 컨텍스트 없는 순수 계산이라 `static` 커널로 두고(`KinematicMover`와 같은 자리), 엔티티를 훑어 실제로 밀고 속도를 바꾸는 쪽은 무상태 DI `*System`으로 둔다. 이 저장소의 관례다 — 순수 static 커널에는 `*System` 이름을 붙이지 않는다.

**위치의 약속:** `Transform.Position`은 **캡슐 발밑(최하단)** 이다. `KinematicMover`가 쓰는 것과 같은 약속이라 여기서도 그대로 따른다. 그래서 캡슐의 심(축)은 `y + radius`부터 `y + height - radius`까지다. 기본값(`radius 0.45`, `height 0.9`)에서는 이 구간이 한 점으로 줄어 **캡슐이 구가 된다** — 프로토타입의 새 몸이 구였던 것과 같다.

**순서가 결과를 가르지 않게:** 두 마리씩 맞댈 때 **두 새 모두 부딪히기 전 속도를 보고** 각자의 결과를 계산한다. 먼저 계산한 쪽이 새 속도를 뒤쪽에 흘리면 순서가 결과를 바꿔 클·서가 갈린다.

- [ ] **Step 1: 겹침 기하 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/FlappyBodyOverlapTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class FlappyBodyOverlapTests
    {
        const float Tolerance = 1e-4f;

        // TbFlappyConfig 기본 몸 규격 — 높이가 반지름의 2배라 캡슐이 구가 된다
        const float Radius = 0.45f;
        const float Height = 0.9f;

        [Test]
        public void 떨어져_있으면_안_겹친다()
        {
            bool overlapped = FlappyBodyOverlap.TryCompute(
                Vector3.zero, new Vector3(5f, 0f, 0f), Radius, Height, out _, out _);

            Assert.IsFalse(overlapped);
        }

        [Test]
        public void 위아래로_겹치면_아래_새를_아래로_민다()
        {
            // a가 아래, b가 위 — 중심 간격 0.5, 서로 닿는 거리 0.9
            bool overlapped = FlappyBodyOverlap.TryCompute(
                Vector3.zero, new Vector3(0f, 0.5f, 0f), Radius, Height,
                out Vector3 pushDir, out float depth);

            Assert.IsTrue(overlapped);
            Assert.AreEqual(0.4f, depth, Tolerance);          // 0.9 - 0.5
            Assert.AreEqual(Vector3.down, pushDir);
        }

        [Test]
        public void 옆으로_겹치면_옆으로_민다()
        {
            bool overlapped = FlappyBodyOverlap.TryCompute(
                Vector3.zero, new Vector3(0.5f, 0f, 0f), Radius, Height,
                out Vector3 pushDir, out float depth);

            Assert.IsTrue(overlapped);
            Assert.AreEqual(0.4f, depth, Tolerance);
            Assert.AreEqual(Vector3.left, pushDir);           // a는 b 반대쪽(-X)으로
        }

        [Test]
        public void 키가_있는_몸은_높이가_겹치는_동안만_옆거리로_판정한다()
        {
            // 길쭉한 캡슐(반지름 0.4, 높이 2.0): 심이 y+0.4 ~ y+1.6
            // 두 새가 옆으로 0.5 떨어져 있고 세로로 0.6 어긋나 있어도 심 높이가 겹쳐 옆거리(0.5)로만 잰다
            bool overlapped = FlappyBodyOverlap.TryCompute(
                Vector3.zero, new Vector3(0.5f, 0.6f, 0f), 0.4f, 2.0f,
                out Vector3 pushDir, out float depth);

            Assert.IsTrue(overlapped);
            Assert.AreEqual(0.3f, depth, Tolerance);          // 0.8 - 0.5
            Assert.AreEqual(Vector3.left, pushDir);           // 세로 성분이 없다 = 세로 속도를 안 뺏는다
        }

        [Test]
        public void 심_높이가_안_겹치면_그_간격까지_거리에_넣는다()
        {
            // 같은 길쭉한 캡슐을 세로로 3.0 띄우면 심 간격이 3.4-1.6=1.8 > 0.8이라 안 닿는다
            bool overlapped = FlappyBodyOverlap.TryCompute(
                Vector3.zero, new Vector3(0f, 3.0f, 0f), 0.4f, 2.0f, out _, out _);

            Assert.IsFalse(overlapped);
        }

        [Test]
        public void 완전히_같은_자리면_정해진_방향으로_가른다()
        {
            // 방향을 구할 수 없는 자리 — 아무 방향이나 고르면 클·서가 갈리므로 규칙을 박아 둔다
            bool overlapped = FlappyBodyOverlap.TryCompute(
                Vector3.zero, Vector3.zero, Radius, Height,
                out Vector3 pushDir, out float depth);

            Assert.IsTrue(overlapped);
            Assert.AreEqual(0.9f, depth, Tolerance);
            Assert.AreEqual(Vector3.down, pushDir);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client && unity cmd recompile; unity cmd console | grep "error CS" | head -3
```

기대: `FlappyBodyOverlap`이 없다는 컴파일 오류.

- [ ] **Step 3: 겹침 기하를 구현한다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyBodyOverlap.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 세로로 선 같은 규격의 캡슐 둘이 얼마나 겹쳤는지 구한다(순수 계산 — 물리엔진을 부르지 않는다).
    ///
    /// 새 몸은 전부 같은 캡슐이라 겹침이 "두 심(축) 사이 거리 &lt; 지름"이라는 산수로 끝난다.
    /// 물리엔진에 물어보지 않는 이유는, 되감아 다시 돌렸을 때 답이 같아야 클라 예측이 서버와
    /// 맞기 때문이다 — 물리엔진은 그 보장을 하지 않는다. (모양이 제각각인 맵은 그럴 수 없어
    /// 물리엔진 sweep을 쓴다.)
    /// </summary>
    public static class FlappyBodyOverlap
    {
        /// <summary>
        /// 겹쳤으면 true와 함께 <paramref name="pushDir"/>(a를 b 밖으로 밀어낼 단위 방향)와
        /// <paramref name="depth"/>(겹친 깊이)를 준다. 위치는 캡슐 발밑 기준 — KinematicMover와 같은 약속이다.
        /// </summary>
        public static bool TryCompute(Vector3 a, Vector3 b, float radius, float height,
                                      out Vector3 pushDir, out float depth)
        {
            pushDir = Vector3.zero;
            depth = 0f;

            // 캡슐의 심 = 아래쪽 구 중심부터 위쪽 구 중심까지의 세로 선분.
            float aLow = a.y + radius, aHigh = a.y + height - radius;
            float bLow = b.y + radius, bHigh = b.y + height - radius;

            // 두 심 사이의 세로 간격. 높이가 서로 겹치면 0 — 그때는 옆거리만으로 판정된다.
            float dy = 0f;
            if (bLow > aHigh)
            {
                dy = bLow - aHigh;
            }
            else if (bHigh < aLow)
            {
                dy = bHigh - aLow;
            }

            Vector3 delta = new Vector3(b.x - a.x, dy, b.z - a.z);
            float touchDistance = radius * 2f;
            float distanceSquared = delta.sqrMagnitude;
            if (distanceSquared >= touchDistance * touchDistance)
            {
                return false;
            }

            float distance = Mathf.Sqrt(distanceSquared);
            depth = touchDistance - distance;
            if (distance < 1e-6f)
            {
                // 두 몸이 정확히 같은 자리 — 밀어낼 방향을 거리에서 구할 수 없다.
                // 아무 방향이나 고르면 클·서가 다르게 갈릴 수 있어 규칙을 하나 박아 둔다.
                // 부르는 쪽이 id 순으로 짝을 세우므로 늘 같은 새가 아래로 간다.
                pushDir = Vector3.down;
                return true;
            }

            pushDir = -delta / distance;
            return true;
        }
    }
}
```

- [ ] **Step 4: 기하 테스트 통과 확인**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client && unity cmd recompile && unity cmd run_tests
```

기대: `FlappyBodyOverlapTests` 6개 통과, 전체 실패 0.

- [ ] **Step 5: 충돌 응답 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/FlappyBodyCollisionSystemTests.cs`:

```csharp
using System.Collections.Generic;
using GameFramework;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class FlappyBodyCollisionSystemTests
    {
        const float Tolerance = 1e-4f;

        static FlappyConfig Config()
            => new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f);

        static Entity Bird(string id, Vector3 position, Vector3 velocity)
        {
            var entity = new Entity(id);
            entity.Add(new GameFramework.World.Transform { Position = position.ToNumerics() });
            entity.Add(new Velocity { Linear = velocity.ToNumerics() });
            return entity;
        }

        static Vector3 PositionOf(Entity e) => e.Get<GameFramework.World.Transform>().Position.ToUnity();
        static Vector3 VelocityOf(Entity e) => e.Get<Velocity>().Linear.ToUnity();

        [Test]
        public void 겹친_두_새가_절반씩_갈라진다()
        {
            var lower = Bird("bird-1", Vector3.zero, Vector3.zero);
            var upper = Bird("bird-2", new Vector3(0f, 0.5f, 0f), Vector3.zero);

            new FlappyBodyCollisionSystem(Config()).Resolve(new List<Entity> { lower, upper });

            // 겹침 0.4에서 허용 겹침 0.01을 뺀 0.39를 반씩 나눠 갖는다
            Assert.AreEqual(-0.195f, PositionOf(lower).y, Tolerance);
            Assert.AreEqual(0.695f, PositionOf(upper).y, Tolerance);
        }

        [Test]
        public void 부딪힌_세로_속도를_주고받는다()
        {
            var lower = Bird("bird-1", Vector3.zero, Vector3.zero);
            var upper = Bird("bird-2", new Vector3(0f, 0.5f, 0f), new Vector3(0f, -10f, 0f));

            new FlappyBodyCollisionSystem(Config()).Resolve(new List<Entity> { lower, upper });

            // FlappyBounce와 같은 값 — 위는 덜 떨어지고 아래는 더 밀린다
            Assert.AreEqual(-3.25f, VelocityOf(upper).y, Tolerance);
            Assert.AreEqual(-6.75f, VelocityOf(lower).y, Tolerance);
        }

        [Test]
        public void 안_겹친_새는_건드리지_않는다()
        {
            var a = Bird("bird-1", Vector3.zero, new Vector3(0f, -10f, 0f));
            var b = Bird("bird-2", new Vector3(0f, 5f, 0f), Vector3.zero);

            new FlappyBodyCollisionSystem(Config()).Resolve(new List<Entity> { a, b });

            Assert.AreEqual(Vector3.zero, PositionOf(a));
            Assert.AreEqual(-10f, VelocityOf(a).y, Tolerance);
            Assert.AreEqual(0f, VelocityOf(b).y, Tolerance);
        }

        [Test]
        public void 두_새가_같은_충돌을_각자_보고_계산한다()
        {
            // 한쪽을 먼저 고쳐 놓고 다른 쪽이 그 새 값을 보면 순서가 결과를 바꾼다.
            // 목록 순서를 뒤집어도 결과가 같아야 클·서가 갈리지 않는다.
            var forward = new List<Entity>
            {
                Bird("bird-1", Vector3.zero, Vector3.zero),
                Bird("bird-2", new Vector3(0f, 0.5f, 0f), new Vector3(0f, -10f, 0f)),
            };
            var reversed = new List<Entity>
            {
                Bird("bird-2", new Vector3(0f, 0.5f, 0f), new Vector3(0f, -10f, 0f)),
                Bird("bird-1", Vector3.zero, Vector3.zero),
            };

            new FlappyBodyCollisionSystem(Config()).Resolve(forward);
            new FlappyBodyCollisionSystem(Config()).Resolve(reversed);

            Assert.AreEqual(VelocityOf(forward[0]).y, VelocityOf(reversed[1]).y, Tolerance);
            Assert.AreEqual(VelocityOf(forward[1]).y, VelocityOf(reversed[0]).y, Tolerance);
            Assert.AreEqual(PositionOf(forward[0]).y, PositionOf(reversed[1]).y, Tolerance);
            Assert.AreEqual(PositionOf(forward[1]).y, PositionOf(reversed[0]).y, Tolerance);
        }
    }
}
```

- [ ] **Step 6: 실패를 확인한다**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client && unity cmd recompile; unity cmd console | grep "error CS" | head -3
```

기대: `FlappyBodyCollisionSystem`이 없다는 컴파일 오류.

- [ ] **Step 7: 충돌 응답을 구현한다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyBodyCollisionSystem.cs`:

```csharp
using System.Collections.Generic;
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 새끼리 부딪히면 서로 밀어내고 세로 속도를 주고받는다(맵 장애물과 달리 정지 페널티 없이 자리싸움만).
    /// 겹침은 <see cref="FlappyBodyOverlap"/>이 산수로 구하고, 속도 교환은 <see cref="FlappyBounce"/>가 맡는다.
    /// </summary>
    public class FlappyBodyCollisionSystem
    {
        /// <summary>허용 겹침. 딱 붙는 지점까지 밀어내면 다음 틱에 또 파고들어 떤다.</summary>
        private const float Slop = 0.01f;

        private readonly FlappyConfig config;

        public FlappyBodyCollisionSystem(FlappyConfig config)
        {
            this.config = config;
        }

        /// <summary>
        /// 넘겨받은 새들을 두 마리씩 모두 맞대어 겹친 짝을 푼다.
        /// 부르는 쪽은 <b>모든 새의 속도가 정해진 뒤</b> 한 번만 부르고, 목록을 엔티티 id 순으로 세워
        /// 넘긴다 — 푸는 순서가 클·서에서 같아야 두 쪽이 같은 결과에 이른다.
        /// </summary>
        public void Resolve(IReadOnlyList<GameFramework.World.Entity> birds)
        {
            for (int i = 0; i < birds.Count; i++)
            {
                for (int j = i + 1; j < birds.Count; j++)
                {
                    ResolvePair(birds[i], birds[j]);
                }
            }
        }

        private void ResolvePair(GameFramework.World.Entity a, GameFramework.World.Entity b)
        {
            var transformA = a.Get<GameFramework.World.Transform>();
            var transformB = b.Get<GameFramework.World.Transform>();
            var velocityA = a.Get<GameFramework.World.Velocity>();
            var velocityB = b.Get<GameFramework.World.Velocity>();
            if (transformA == null || transformB == null || velocityA == null || velocityB == null)
            {
                return;
            }

            Vector3 positionA = transformA.Position.ToUnity();
            Vector3 positionB = transformB.Position.ToUnity();
            if (!FlappyBodyOverlap.TryCompute(positionA, positionB, config.BodyRadius, config.BodyHeight,
                                              out Vector3 pushDir, out float depth))
            {
                return;
            }

            // 절반씩 — 양쪽을 합쳐야 완전히 떨어진다.
            float half = Mathf.Max(depth - Slop, 0f) * 0.5f;
            transformA.Position = (positionA + pushDir * half).ToNumerics();
            transformB.Position = (positionB - pushDir * half).ToNumerics();

            // 둘 다 부딪히기 *전* 속도를 보고 계산한다 — 한쪽을 먼저 고쳐 놓고 다른 쪽이 그 값을 보면
            // 짝을 어느 순서로 넘겼는지가 결과를 바꿔 클·서가 갈린다.
            Vector3 linearA = velocityA.Linear.ToUnity();
            Vector3 linearB = velocityB.Linear.ToUnity();
            float beforeA = linearA.y;
            float beforeB = linearB.y;
            linearA.y = FlappyBounce.ResolveVy(beforeA, beforeB, pushDir.y, config.Restitution);
            linearB.y = FlappyBounce.ResolveVy(beforeB, beforeA, -pushDir.y, config.Restitution);
            velocityA.Linear = linearA.ToNumerics();
            velocityB.Linear = linearB.ToNumerics();
        }
    }
}
```

- [ ] **Step 8: 테스트 통과 확인**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client && unity cmd recompile && unity cmd run_tests
```

기대: `FlappyBodyOverlapTests` 6개 + `FlappyBodyCollisionSystemTests` 4개 통과, 전체 실패 0.

- [ ] **Step 9: 테스트가 진짜로 실패할 수 있는지 확인한다**

`ResolvePair`의 둘째 `ResolveVy` 호출에서 `beforeA`를 `linearA.y`로 바꾼다 — 이미 고쳐 놓은 A의 새 값을 B가 보게 되어 짝을 넘긴 순서가 결과를 바꾼다:

```csharp
            linearB.y = FlappyBounce.ResolveVy(beforeB, linearA.y, -pushDir.y, config.Restitution);
```

`두_새가_같은_충돌을_각자_보고_계산한다`가 **실패해야 한다**(순서를 뒤집은 쪽과 값이 달라진다). 확인했으면 `beforeA`로 되돌린다.

- [ ] **Step 10: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/FlappyBodyOverlap.cs Runtime/Scripts/Game/FlappyBodyOverlap.cs.meta \
        Runtime/Scripts/Game/FlappyBodyCollisionSystem.cs Runtime/Scripts/Game/FlappyBodyCollisionSystem.cs.meta \
        Tests/EditMode/FlappyBodyOverlapTests.cs Tests/EditMode/FlappyBodyOverlapTests.cs.meta \
        Tests/EditMode/FlappyBodyCollisionSystemTests.cs Tests/EditMode/FlappyBodyCollisionSystemTests.cs.meta
git commit -m "feat(flappy): 새끼리 밀어내고 세로 속도를 주고받는다

겹침을 물리엔진에 묻지 않고 직접 구한다 — 새 몸은 전부 같은 캡슐이라 산수로
끝나고, 되감아 다시 돌려도 답이 같아야 클라 예측이 서버와 맞는다.
모양이 제각각인 맵은 그럴 수 없어 물리엔진 sweep을 그대로 쓴다."
```

---

### Task 6: `FlappyWorld.Mutation` — 세 페이즈를 잇는다

**Files:**
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`
- Test: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldTests.cs` (기존 테스트를 대체)
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`

**Interfaces:**
- Consumes: `LOP.FlappyMoveSystem.Tick(Entity, float)`(Task 4), `LOP.FlappyBodyCollisionSystem.Resolve(IReadOnlyList<Entity>)`(Task 5), `LOP.FlappyConfig`(Task 2), 기존 `LOP.KinematicMover.Move(in KinematicMoveInput, ICollisionQuery)`, 기존 `GameFramework.World.IMotionBridge`.
- Produces: `LOP.FlappyWorld` 생성자가 8인자로 바뀐다 — `(EntityRegistry, WorldEventBuffer, FlappyMoveSystem, FlappyBodyCollisionSystem, ICollisionQuery, IMotionBridge, FlappyConfig, int layerMask)`. 양쪽 `FlappyRaceLifetimeScope`가 이 순서로 조립한다.

**한 틱의 순서** (스펙 §5):

```
① 속도      전원의 FlappyMoveSystem.Tick        (중력·플랩·고정 전진)
② 몸싸움    FlappyBodyCollisionSystem.Resolve   ← 전원의 ①이 끝난 뒤 한 번
③ 이동      KinematicMover.Move → Transform/Velocity 기록
```

②를 전원의 ① 뒤에 두는 이유: 한 마리씩 "속도 정하고 바로 밀어내기"를 하면 목록에서 먼저 나온 새가 아직 갱신 안 된 상대 속도를 보게 되어, 순서가 결과를 가른다.

**왜 `KinematicMoveSystem`을 안 쓰나:** 그 시스템은 중력 −19.62와 캡슐 0.35/1.5가 코드에 박혀 있어 Flappy와 맞지 않는다. 공유 커널 `KinematicMover.Move`는 위치·속도·반지름·높이·dt·레이어마스크를 전부 인자로 받으므로 그대로 재사용하면 **FlapWang 회귀 위험이 0**이다(스펙 §5).

**`IMotionBridge`에서 무엇을 쓰나:** `SyncTransforms`(스크립트로 옮긴 자리를 물리에 알림) → `Depenetrate`(지형에 박힌 것 빼내기) → `PushMotion`(Rigidbody 팔로워 반영). **`Separate`는 부르지 않는다** — 새끼리 밀어내기는 ②가 이미 했다. 참고로 이 셋은 엔티티에 `PhysicsBody`가 붙기 전(B2-d)까지는 아무 일도 하지 않는다.

**시뮬 대상 목록의 순서:** `EntityRegistry.All`의 순회 순서는 보장되지 않으므로 엔티티 id로 정렬해 넘긴다. 클·서가 같은 순서로 짝을 풀어야 결과가 같다.

- [ ] **Step 1: 기존 테스트를 새 테스트로 갈아 끼운다**

`LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldTests.cs`를 **통째로** 아래로 바꾼다. 기존 `Tick_LeavesEntitiesUntouched_WhileMutationIsEmpty`는 "Mutation이 비어 있다"를 단언하므로 이 태스크에서 반드시 깨진다 — 지운다.

```csharp
using System.Collections.Generic;
using GameFramework;
using GameFramework.Physics;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class FlappyWorldTests
    {
        const float Tolerance = 1e-3f;

        // 아무데도 안 부딪히는 빈 하늘 — 맵 충돌 없이 이동 계산만 보고 싶을 때.
        private class EmptySkyQuery : ICollisionQuery
        {
            public CollisionHit CapsuleCast(Vector3 point1, Vector3 point2, float radius,
                Vector3 direction, float distance, int layerMask) => CollisionHit.None;
        }

        // 물리 바디가 아직 없는 단계라 브릿지는 아무 일도 하지 않는다(호출 여부만 세어 둔다).
        private class NoopMotionBridge : GameFramework.World.IMotionBridge
        {
            public int SyncTransformsCalls;
            public int SeparateCalls;

            public void SyncTransforms() => SyncTransformsCalls++;
            public void Depenetrate(Entity entity) { }
            public void Separate(Entity entity) => SeparateCalls++;
            public void PushMotion(Entity entity) { }
        }

        static FlappyConfig Config()
            => new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f);

        static Entity Bird(string id, Vector3 position, bool simulated)
        {
            var entity = new Entity(id);
            entity.Add(new GameFramework.World.Transform { Position = position.ToNumerics() });
            entity.Add(new Velocity());
            if (simulated)
            {
                entity.Add(new Simulated());
            }
            return entity;
        }

        static FlappyWorld World(EntityRegistry registry, GameFramework.World.IMotionBridge bridge)
            => new FlappyWorld(registry, new WorldEventBuffer(),
                               new FlappyMoveSystem(Config()),
                               new FlappyBodyCollisionSystem(Config()),
                               new EmptySkyQuery(), bridge, Config(), layerMask: ~0);

        static Vector3 PositionOf(Entity e) => e.Get<GameFramework.World.Transform>().Position.ToUnity();
        static Vector3 VelocityOf(Entity e) => e.Get<Velocity>().Linear.ToUnity();

        [Test]
        public void 한_틱이면_전진하면서_중력만큼_떨어진다()
        {
            var registry = new EntityRegistry();
            var bird = Bird("bird-1", Vector3.zero, simulated: true);
            registry.Add(bird);

            World(registry, new NoopMotionBridge()).Tick(1, 0.1f);

            Assert.AreEqual(11f, VelocityOf(bird).x, Tolerance);    // 고정 전진
            Assert.AreEqual(-7f, VelocityOf(bird).y, Tolerance);    // 70 × 0.1
            Assert.AreEqual(1.1f, PositionOf(bird).x, Tolerance);   // 11 × 0.1
            Assert.AreEqual(-0.7f, PositionOf(bird).y, Tolerance);  // 7 × 0.1
        }

        [Test]
        public void 시뮬_대상이_아닌_엔티티는_건드리지_않는다()
        {
            var registry = new EntityRegistry();
            var remote = Bird("bird-2", Vector3.zero, simulated: false);
            registry.Add(remote);

            World(registry, new NoopMotionBridge()).Tick(1, 0.1f);

            // 남의 새는 예측하지 않고 서버 스냅샷 보간에 맡긴다
            Assert.AreEqual(Vector3.zero, PositionOf(remote));
            Assert.AreEqual(Vector3.zero, VelocityOf(remote));
        }

        [Test]
        public void 겹쳐_있던_두_새는_이동_전에_갈라진다()
        {
            var registry = new EntityRegistry();
            var lower = Bird("bird-1", Vector3.zero, simulated: true);
            var upper = Bird("bird-2", new Vector3(0f, 0.5f, 0f), simulated: true);
            registry.Add(lower);
            registry.Add(upper);

            World(registry, new NoopMotionBridge()).Tick(1, 0.1f);

            // 몸싸움으로 위아래 0.39만큼 갈라진 뒤 각자 이동한다 — 서로 파고든 채로 남지 않는다
            Assert.Greater(PositionOf(upper).y - PositionOf(lower).y, 0.5f);
        }

        [Test]
        public void 새끼리_밀어내기를_물리엔진에_맡기지_않는다()
        {
            var registry = new EntityRegistry();
            registry.Add(Bird("bird-1", Vector3.zero, simulated: true));
            var bridge = new NoopMotionBridge();

            World(registry, bridge).Tick(1, 0.1f);

            Assert.AreEqual(0, bridge.SeparateCalls);       // 겹침은 우리 계산이 이미 풀었다
            Assert.AreEqual(1, bridge.SyncTransformsCalls); // 옮긴 자리는 물리에 알려 준다
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client && unity cmd recompile; unity cmd console | grep "error CS" | head -3
```

기대: `FlappyWorld` 생성자 인자 개수가 안 맞는다는 `error CS1729`.

- [ ] **Step 3: `FlappyWorld`를 구현한다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`를 통째로 바꾼다:

```csharp
using System.Collections.Generic;
using GameFramework;
using GameFramework.Physics;

namespace LOP
{
    /// <summary>
    /// Flappy Race의 시뮬 코어. 클·서가 같은 구체 클래스를 돌려 결과가 갈리지 않게 한다.
    /// 한 틱: ① 속도(중력·플랩·고정 전진) → ② 새끼리 몸싸움 → ③ 맵에 막히며 이동.
    /// ②를 전원의 ① 뒤에 두는 이유는, 한 마리씩 처리하면 먼저 나온 새가 아직 갱신되지 않은
    /// 상대 속도를 보게 돼 순서가 결과를 가르기 때문이다.
    /// </summary>
    public class FlappyWorld : GameFramework.World.WorldBase
    {
        private readonly FlappyMoveSystem _moveSystem;
        private readonly FlappyBodyCollisionSystem _bodyCollisionSystem;
        private readonly ICollisionQuery _collisionQuery;
        private readonly GameFramework.World.IMotionBridge _motionBridge;
        private readonly FlappyConfig _config;
        private readonly int _layerMask;

        // 매 틱 도는 코드라 목록을 새로 만들지 않고 비워서 다시 쓴다.
        private readonly List<GameFramework.World.Entity> _birds = new List<GameFramework.World.Entity>();

        public FlappyWorld(
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.WorldEventBuffer eventBuffer,
            FlappyMoveSystem moveSystem,
            FlappyBodyCollisionSystem bodyCollisionSystem,
            ICollisionQuery collisionQuery,
            GameFramework.World.IMotionBridge motionBridge,
            FlappyConfig config,
            int layerMask)
            : base(entityRegistry, eventBuffer)
        {
            _moveSystem = moveSystem;
            _bodyCollisionSystem = bodyCollisionSystem;
            _collisionQuery = collisionQuery;
            _motionBridge = motionBridge;
            _config = config;
            _layerMask = layerMask;
        }

        protected override void Mutation(long tick, float deltaTime)
        {
            CollectBirds();

            for (int i = 0; i < _birds.Count; i++)
            {
                _moveSystem.Tick(_birds[i], deltaTime);
            }

            // 전원의 속도가 정해진 뒤 한 번(페이즈 배리어). 새끼리 겹침은 여기서 다 풀리므로
            // 아래 물리 브릿지의 Separate는 부르지 않는다.
            _bodyCollisionSystem.Resolve(_birds);

            // 스크립트로 옮긴 자리를 물리에 먼저 알려야 sweep이 한 틱 전 자리에서 이뤄지지 않는다.
            _motionBridge.SyncTransforms();
            for (int i = 0; i < _birds.Count; i++)
            {
                MoveThroughMap(_birds[i], deltaTime);
            }
        }

        // 시뮬 대상만 모아 id 순으로 세운다. 레지스트리 순회 순서는 정해져 있지 않은데,
        // 몸싸움을 푸는 순서가 클·서에서 같아야 두 쪽이 같은 결과에 이른다.
        private void CollectBirds()
        {
            _birds.Clear();
            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Has<GameFramework.World.Simulated>())
                {
                    _birds.Add(entity);
                }
            }
            _birds.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        }

        private void MoveThroughMap(GameFramework.World.Entity entity, float deltaTime)
        {
            var transform = entity.Get<GameFramework.World.Transform>();
            var velocity = entity.Get<GameFramework.World.Velocity>();
            if (transform == null || velocity == null)
            {
                return;
            }

            _motionBridge.Depenetrate(entity);

            var result = KinematicMover.Move(new KinematicMoveInput(
                transform.Position.ToUnity(), velocity.Linear.ToUnity(),
                _config.BodyRadius, _config.BodyHeight, deltaTime, _layerMask), _collisionQuery);

            transform.Position = result.position.ToNumerics();
            velocity.Linear = result.velocity.ToNumerics();

            _motionBridge.PushMotion(entity);
        }
    }
}
```

- [ ] **Step 4: 양쪽 스코프가 새 생성자로 조립하게 고친다**

클라 `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`의 `ConfigureGame`에서

```csharp
            builder.Register<GameFramework.World.IWorld, FlappyWorld>(Lifetime.Singleton);
```

를 아래로 바꾼다:

```csharp
            builder.Register<FlappyMoveSystem>(Lifetime.Singleton);
            builder.Register<FlappyBodyCollisionSystem>(Lifetime.Singleton);
            // sweep이 볼 것은 맵 지오메트리뿐이다 — 새끼리는 물리엔진이 아니라 우리 계산으로 민다.
            builder.Register<GameFramework.World.IWorld>(c => new FlappyWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<FlappyMoveSystem>(),
                c.Resolve<FlappyBodyCollisionSystem>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
                c.Resolve<GameFramework.World.IMotionBridge>(),
                c.Resolve<FlappyConfig>(),
                LayerMask.GetMask("Default")), Lifetime.Singleton);
```

서버 `LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`에도 **같은 블록**을 넣는다. 서버 파일에는 `using UnityEngine;`이 없으므로 파일 맨 위 `using VContainer;` **앞에** 한 줄 추가한다:

```csharp
using UnityEngine;
```

(`LayerMask.GetMask`가 `UnityEngine`에 있다. 맵 콜라이더는 전부 `Default` 레이어(layer 0)임을 실측으로 확인했다 — `FlappyRaceMap.unity`의 `m_Layer` 228개가 전부 0.)

- [ ] **Step 5: 컴파일 + 테스트 통과 확인**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd console | grep -c "error CS"
unity cmd run_tests
```

기대: `error CS` 0건. `FlappyWorldTests` 4개 통과. **EditMode 전체 실패 0** — 특히 FlapWang 쪽 테스트(`KinematicMoverTests`, `AbilityReplayDeterminismTests` 등)가 전부 그대로 통과해야 한다. 이 슬라이스는 FlapWang 코드를 한 줄도 건드리지 않았으므로 하나라도 깨지면 멈추고 보고한다.

서버 프로젝트도 컴파일을 확인한다(에디터가 없으면 batchmode 임포트 후 로그에서 `error CS`를 찾는다).

- [ ] **Step 6: 테스트가 진짜로 실패할 수 있는지 확인한다**

`FlappyWorld.Mutation`에서 `_bodyCollisionSystem.Resolve(_birds);` 줄을 잠깐 주석 처리하고 테스트를 돌린다. `겹쳐_있던_두_새는_이동_전에_갈라진다`가 **실패해야 한다**. 확인했으면 되돌린다.

- [ ] **Step 7: 커밋 (3개 저장소)**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/FlappyWorld.cs Tests/EditMode/FlappyWorldTests.cs
git commit -m "feat(flappy): 시뮬 코어가 새를 날리고 막고 밀어낸다

한 틱을 속도 → 몸싸움 → 이동 세 페이즈로 나눈다. 몸싸움을 전원의 속도 갱신
뒤에 두는 이유는, 한 마리씩 처리하면 먼저 나온 새가 아직 갱신 안 된 상대 속도를
보게 돼 순서가 결과를 가르기 때문이다.

맵 충돌은 KinematicMoveSystem이 아니라 공유 커널 KinematicMover.Move를 직접
부른다 — 그 시스템은 중력·캡슐 규격이 박혀 있어 FlapWang 전용이다."

cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Game/FlappyRaceLifetimeScope.cs
git commit -m "feat(flappy): 시뮬 코어를 조립한다"

cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add Assets/Scripts/Game/FlappyRaceLifetimeScope.cs
git commit -m "feat(flappy): 시뮬 코어를 조립한다"
```

---

## 완료 기준

- [ ] EditMode 테스트 전체 통과 — Flappy 테스트 27개(이식한 `FlappyBounce` 8 + 신규 `FlappyMoveSystem` 5 + `FlappyBodyOverlap` 6 + `FlappyBodyCollisionSystem` 4 + `FlappyWorld` 4), 기존 테스트 실패 0
- [ ] 양쪽 MasterData 패키지의 수기 로더 목록(`TableFiles`)에 `tbflappyconfig`가 등록되고 `TableFileManifestTests`가 통과
- [ ] 각 신규 테스트가 **일부러 깨뜨렸을 때 실패하는 것**을 확인함 (Task 4 Step 5, Task 5 Step 9, Task 6 Step 6)
- [ ] 클·서 양쪽 프로젝트가 `error CS` 없이 컴파일됨
- [ ] `TbFlappyConfig`가 클·서 양쪽 MasterData 패키지에 생성되고 값이 읽힘
- [ ] 6개 저장소 각각 `feature/flappy-b2b-sim-core` 브랜치에 커밋됨 (푸시는 아직)
- [ ] 유니티 저장소의 로컬 픽스처가 커밋에 섞여 들어가지 않음

## 태스크가 끝난 뒤 (컨트롤러가 사용자와 함께)

1. 6개 저장소를 `CLAUDE.md`의 푸시 규약대로 리베이스 → `--ff-only` → `--no-ff` 머지 → 푸시. **한 줄씩 결과를 확인하고 넘어간다.**
2. 스펙 §5에 결과 절을 추가한다 — 특히 **해석적 겹침 계산으로 간 결정과 그 근거**, 그리고 스펙 §6의 "프리팹 콜라이더가 `body_radius`와 일치해야 한다"는 경고가 **더 이상 해당하지 않는다**는 것.
3. 스펙 §9의 열린 결정 "맵 콜라이더 레이어"를 `Default`로 닫는다(실측 근거: 맵 씬 `m_Layer` 228개 전부 0).
4. B2-d 선결 과제로 **"플랩을 누를 수단이 없다"** 를 스펙에 적는다.
