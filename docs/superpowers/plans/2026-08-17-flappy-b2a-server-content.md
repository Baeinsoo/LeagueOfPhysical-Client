# 슬라이스 B2-a — 서버 콘텐츠 경로 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 게임서버가 FlappyRace 맵과 새 프리팹을 Addressables로 실제로 받아, B1에서 나던 `ChainOperation failed`가 사라지게 한다.

**Architecture:** 서버는 이미 원격 카탈로그(`https://lop-assets.s3.../dev/StandaloneLinux64/catalog_0.1.hash`)를 받아 쓰도록 빌드돼 있고, 클라 프로젝트에 Addressable 엔트리(`FlappyRaceMap.unity`, `Bird.prefab`)도 이미 등록돼 있다. 빠진 것은 **StandaloneLinux64 타깃으로 콘텐츠를 굽고 S3에 올리는 경로**뿐이다. 기존 `BuildAndroidContentFull`은 이름만 Android이고 실제로는 활성 빌드 타깃으로 굽으므로(`AddressableAssetSettings.BuildPlayerContent()`), 타깃 비종속 메서드로 정리하고 CI에 Linux 잡을 더한다.

**Tech Stack:** Unity 6000.3.16f1 (Addressables), GitHub Actions(self-hosted `client` 러너), AWS S3(`lop-assets`), kind 로컬 클러스터, `unity` CLI.

**Spec:** `docs/superpowers/specs/2026-08-17-flappy-race-gameplay-b2-design.md` (§3)

## Global Constraints

- **main에 직접 커밋 금지.** 피처 브랜치에서 작업하고 완료 후 `--no-ff` 머지한다.
- **`git add -A` / `git add .` 금지.** 반드시 경로를 명시한다. 이 작업트리에는 다른 작업의 미추적 파일이 있다(`FlappyCalibration/` 등).
- **유니티 `.meta`는 유니티가 만든 것만 커밋한다.** 직접 만들지 않는다.
- **`unity` CLI를 쓴다.** 비대화형 셸에선 PATH에 없으므로 매번 `export PATH="$HOME/.unity/bin:$PATH"`.
- **에디터가 Play 모드면 재컴파일이 끝나지 않는다.** 빌드·컴파일 전에 `EditorApplication.isPlaying`을 확인하고, 켜져 있으면 사용자에게 정지를 요청한다(플레이 중 씬 편집분이 날아가므로 임의로 끄지 않는다).
- **배포 상태는 로컬 git이 아니라 실제(클러스터·S3)에서 확인한다.** 로컬 `infrastructure`/`lop-backend` 체크아웃은 뒤처져 있을 수 있다.
- **AWS 업로드는 CI가 한다.** 로컬 `aws` 세션은 만료 상태이며, 로컬에서 올리려면 사용자가 직접 `aws login` 해야 한다.
- 활성 Addressables 프로파일은 **dev** (원격 경로 = `s3://lop-assets/dev/[BuildTarget]`).

---

## File Structure

| 파일 | 책임 | 변경 |
|---|---|---|
| `Assets/Editor/BuildScript.cs` | 배치모드 빌드 진입점 | 타깃 비종속 콘텐츠 빌드 메서드 추가 |
| `.github/workflows/content-deploy.yml` | 콘텐츠 빌드·업로드 CI | Linux(게임서버용) 잡 추가 |
| `docs/superpowers/specs/2026-08-17-flappy-race-gameplay-b2-design.md` | 설계 문서 | §3에 결과 기록 |

산출물(커밋 대상 아님): `ServerData/StandaloneLinux64/` → `s3://lop-assets/dev/StandaloneLinux64/`

---

### Task 1: 타깃 비종속 콘텐츠 빌드 메서드

**Files:**
- Modify: `Assets/Editor/BuildScript.cs`

**Interfaces:**
- Consumes: 없음
- Produces: `BuildScript.BuildContentFull()` — 배치모드에서 `-executeMethod BuildScript.BuildContentFull`로 호출. 활성 빌드 타깃(`-buildTarget`으로 지정)의 Addressables 콘텐츠를 full 빌드하고 성공 시 exit 0, 실패 시 exit 1.

- [ ] **Step 1: 현재 메서드 확인**

`Assets/Editor/BuildScript.cs`의 `BuildAndroidContentFull`이 아래와 같은지 읽어서 확인한다. `BuildPlayerContent()`에 타깃 인자가 없다는 점이 이 태스크의 근거다.

```csharp
public static void BuildAndroidContentFull()
{
    var settings = EnsureSettings();
    AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
    FinishContent(result, "FULL");
}
```

- [ ] **Step 2: 타깃 비종속 메서드 추가 + 기존 메서드는 위임으로**

`BuildAndroidContentFull`을 아래로 교체한다. 기존 이름을 남기는 이유는 `client-app-deploy.yml`이 그 이름을 부르고 있어서다 — 이 태스크에서 CI를 건드리지 않는다.

```csharp
    // ── 어드레서블: full 빌드. BuildPlayerContent는 **활성 빌드 타깃**으로 굽는다.
    //    타깃은 CI가 -buildTarget 으로 정한다(Android=클라 앱, StandaloneLinux64=게임서버).
    public static void BuildContentFull()
    {
        EnsureSettings();
        Debug.Log($"content full build target: {EditorUserBuildSettings.activeBuildTarget}");
        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        FinishContent(result, "FULL");
    }

    // ── 기존 이름 유지(호출하는 CI가 있다). 하는 일은 위와 같다.
    public static void BuildAndroidContentFull()
    {
        BuildContentFull();
    }
```

- [ ] **Step 3: 컴파일 확인**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd --timeout 60 eval 'return UnityEditor.EditorApplication.isPlaying ? "playing" : "stopped";'
```

Expected: `stopped`. `playing`이면 사용자에게 Play 정지를 요청하고 기다린다.

```bash
unity cmd --timeout 90 recompile
unity cmd --timeout 60 recompile_status
```

Expected: `{"status":"completed","failed":false,"errors":[]}`

- [ ] **Step 4: 메서드가 실제로 존재하는지 단언**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cat > /tmp/check_buildscript.cs <<'CS'
var t = System.Type.GetType("BuildScript, Assembly-CSharp-Editor");
if (t == null) return "BuildScript 타입 없음";
var m = t.GetMethod("BuildContentFull", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
return "BuildContentFull=" + (m != null);
CS
unity cmd --timeout 60 eval_file --file /tmp/check_buildscript.cs
```

Expected: `BuildContentFull=True`

- [ ] **Step 5: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git checkout -b feature/flappy-b2a-server-content
git add Assets/Editor/BuildScript.cs
git commit -m "$(cat <<'EOF'
chore(build): 콘텐츠 빌드 메서드를 타깃 비종속으로 만든다

BuildPlayerContent는 활성 빌드 타깃으로 굽는데 메서드 이름만 Android라
게임서버(StandaloneLinux64)용으로 부르기가 어색했다. 이름을 타깃 비종속으로
바꾸고 기존 이름은 위임으로 남긴다 — client-app-deploy가 그 이름을 부른다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Linux 콘텐츠 로컬 빌드와 내용 검증

CI를 고치기 전에 **로컬에서 한 번 구워** 산출물이 맞는지 먼저 본다. CI에서 처음 돌리면 실패 원인이 CI인지 콘텐츠인지 가려지지 않는다.

**Files:**
- 산출물: `ServerData/StandaloneLinux64/` (커밋하지 않음)

**Interfaces:**
- Consumes: `BuildScript.BuildContentFull()` (Task 1)
- Produces: `ServerData/StandaloneLinux64/catalog_0.1.{bin,hash}` + `*.bundle` — Task 3이 S3로 올릴 대상

- [ ] **Step 1: 에디터를 닫는다**

배치모드 빌드는 프로젝트를 단독 점유한다. 사용자에게 유니티 에디터를 닫아 달라고 요청하고, 아래로 확인한다.

```bash
ls /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Temp/UnityLockfile 2>/dev/null && echo "아직 열려 있음" || echo "닫힘"
```

Expected: `닫힘`

- [ ] **Step 2: Linux 타깃으로 콘텐츠 full 빌드**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -nographics -projectPath . \
  -buildTarget StandaloneLinux64 \
  -executeMethod BuildScript.BuildContentFull \
  -logFile - > /tmp/unity-linux-content.log 2>&1; echo "exit=$?"
```

Expected: `exit=0`. 실패하면 `tail -80 /tmp/unity-linux-content.log`로 원인을 본다.

- [ ] **Step 3: 산출물 확인**

```bash
ls -la /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/ServerData/StandaloneLinux64
```

Expected: `catalog_0.1.bin`, `catalog_0.1.hash`, `*.bundle` 여러 개. 날짜가 **오늘**이어야 한다(6월 것이 남아 있으면 빌드가 안 돈 것).

- [ ] **Step 4: 카탈로그에 Flappy 자산이 들어갔는지 단언**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
for k in FlappyRaceMap Bird.prefab FlapWangMap Knight; do
  if grep -qa "$k" ServerData/StandaloneLinux64/catalog_0.1.bin; then echo "$k: 있음"; else echo "$k: 없음"; fi
done
```

Expected: **네 개 모두 `있음`.** `FlappyRaceMap`이나 `Bird.prefab`이 없으면 Addressable 엔트리가 빠진 것이므로 클라 `Assets/AddressableAssetsData/AssetGroups/{Scene,Character}.asset`을 확인한다.

- [ ] **Step 5: 워킹트리가 더러워지지 않았는지 확인 (커밋 없음)**

`ServerData/`는 `.gitignore` 75번 줄에 이미 있으므로 빌드해도 워킹트리에 안 뜬다. 확인만 한다.

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git check-ignore -v ServerData
git status --short
```

Expected: `check-ignore`가 `.gitignore:75:ServerData/`를 출력. `git status`에 `ServerData/`가 **없어야** 한다.
`Assets/AddressableAssetsData/Linux/`(content_state)도 무시 대상이라 뜨지 않는다.

---

### Task 3: CI에 게임서버 콘텐츠 잡 추가

**Files:**
- Modify: `.github/workflows/content-deploy.yml`

**Interfaces:**
- Consumes: `BuildScript.BuildContentFull()` (Task 1)
- Produces: `s3://lop-assets/dev/StandaloneLinux64/` 최신 콘텐츠 — 서버 파드가 부팅 시 이 경로의 `catalog_0.1.hash`를 본다

- [ ] **Step 1: 기존 워크플로 읽기**

`.github/workflows/content-deploy.yml`을 끝까지 읽는다. 특히 (a) UPM 패키지 체크아웃 스텝, (b) NuGet 복원 스텝, (c) `aws s3 sync ServerData/Android "s3://lop-assets/dev/Android"` 스텝의 형태를 그대로 따라 쓴다.

- [ ] **Step 2: Linux 잡 추가**

파일 끝에 아래 잡을 더한다. 기존 `build-deploy`(Android, 증분)와 달리 **full 빌드**를 쓰는 이유는 게임서버 이미지에 로컬 번들이 없어(카탈로그가 비어 있음) 증분 baseline이 의미가 없기 때문이다.

```yaml
  # 게임서버(Linux)용 콘텐츠. 서버 이미지는 빈 카탈로그로 빌드되고 부팅 시 이 경로의
  # catalog_0.1.hash를 보고 원격 카탈로그를 받는다. 로컬 번들이 없으므로 증분이 아니라 full로 굽는다.
  build-deploy-gameserver:
    runs-on: [self-hosted, client]
    steps:
      - uses: actions/checkout@v4
        with:
          submodules: recursive

      - name: 의존 UPM 패키지 레포 체크아웃
        run: |
          set -e
          cd "$GITHUB_WORKSPACE/.."
          for r in GameFramework LeagueOfPhysical-Shared LeagueOfPhysical-MasterData-Client; do
            if [ -d "$r/.git" ]; then
              git -C "$r" fetch --depth 1 origin && git -C "$r" reset --hard @{u}
            else
              git clone --depth 1 "https://github.com/Baeinsoo/$r" "$r"
            fi
          done

      - name: NuGet 패키지 복원 (NuGetForUnity CLI, packages.config 기반)
        run: |
          set -e
          dotnet tool restore
          dotnet tool run nugetforunity restore .
          test -f Assets/NuGetForUnity/Packages/R3.1.3.1/lib/netstandard2.1/R3.dll

      - name: 어드레서블 콘텐츠 full 빌드 (StandaloneLinux64)
        run: |
          set -eo pipefail
          UNITY="/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity"
          if ! "$UNITY" -batchmode -quit -nographics -buildTarget StandaloneLinux64 -projectPath . \
                -executeMethod BuildScript.BuildContentFull -logFile - > unity-linux-content.log 2>&1; then
            echo "::error::linux content build failed"; tail -80 unity-linux-content.log; exit 1
          fi

      - name: 산출물 확인
        run: |
          set -e
          test -f ServerData/StandaloneLinux64/catalog_0.1.bin
          grep -qa FlappyRaceMap ServerData/StandaloneLinux64/catalog_0.1.bin
          grep -qa Bird.prefab ServerData/StandaloneLinux64/catalog_0.1.bin

      - name: S3 업로드
        env:
          AWS_ACCESS_KEY_ID: ${{ vars.AWS_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
        run: |
          set -e
          aws s3 sync ServerData/StandaloneLinux64 "s3://lop-assets/dev/StandaloneLinux64"
          echo "게임서버 콘텐츠 갱신 완료: s3://lop-assets/dev/StandaloneLinux64/"
```

- [ ] **Step 3: YAML 문법 확인**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
python3 -c "import yaml,sys; d=yaml.safe_load(open('.github/workflows/content-deploy.yml')); print('jobs:', list(d['jobs'].keys()))"
```

Expected: `jobs: ['build-deploy', 'build-deploy-gameserver']`
(`yaml` 모듈이 없으면 `python3 -m pip install --user pyyaml` 또는 scratchpad venv를 쓴다.)

- [ ] **Step 4: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add .github/workflows/content-deploy.yml
git commit -m "$(cat <<'EOF'
ci(content): 게임서버용 Linux 콘텐츠 빌드·업로드 잡을 더한다

서버 이미지는 빈 카탈로그로 빌드되고 부팅 시
s3://lop-assets/dev/StandaloneLinux64 의 해시를 보고 원격 카탈로그를 받는다.
그런데 그 경로를 갱신하는 CI가 없어 6월 산출물에 멈춰 있었고, 8월에 승격한
FlappyRace 맵·새 프리팹이 서버에 도달하지 못했다.

로컬 번들이 없으므로 증분(update)이 아니라 full로 굽는다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: S3 반영과 서버 파드 검증

**Files:** 없음(검증 전용)

**Interfaces:**
- Consumes: Task 3의 CI 잡

- [ ] **Step 1: 브랜치를 main에 머지하고 푸시**

CI는 main 기준으로 돈다.

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git fetch origin
git checkout feature/flappy-b2a-server-content && git rebase --rebase-merges origin/main
git checkout main && git reset --hard origin/main
git merge --no-ff feature/flappy-b2a-server-content -m "Merge feature/flappy-b2a-server-content: 게임서버 콘텐츠 경로"
git push origin main
```

- [ ] **Step 2: CI 실행 요청**

`content-deploy` 워크플로를 **수동 실행(workflow_dispatch)** 한다. 사용자에게 GitHub Actions에서 실행을 요청하거나, `gh` CLI가 인증돼 있으면:

```bash
gh workflow run content-deploy -R Baeinsoo/LeagueOfPhysical-Client
gh run list -R Baeinsoo/LeagueOfPhysical-Client --workflow content-deploy -L 1
```

- [ ] **Step 3: S3가 실제로 갱신됐는지 확인**

```bash
B=https://lop-assets.s3.ap-northeast-2.amazonaws.com/dev/StandaloneLinux64
curl -s -D - -o /tmp/linux_catalog.bin "$B/catalog_0.1.bin" | grep -i last-modified
for k in FlappyRaceMap Bird.prefab; do
  grep -qa "$k" /tmp/linux_catalog.bin && echo "$k: 있음" || echo "$k: 없음"
done
```

Expected: `Last-Modified`가 **오늘**, 둘 다 `있음`.

- [ ] **Step 4: 게임서버 파드에서 런타임 확인**

클라 에디터를 Play로 넣고 로비에서 **플래피 레이스**를 골라 입장한다. 그러면 `room-pod-<id>`가 뜬다.

```bash
kubectl get pods | grep room-pod
POD=$(kubectl get pods | grep room-pod | awk '{print $1}' | head -1)
kubectl logs "$POD" | grep -iE "ChainOperation|instantiate is null|FlappyRaceMap|Registered flappy bird" | head -10
```

Expected:
- `ChainOperation failed` **없음**
- `The Object you want to instantiate is null` **없음**
- `[World] Registered flappy bird` 있음

- [ ] **Step 5: 파드 정리**

FlappyRace는 종료 조건이 없어 매치가 스스로 끝나지 않는다. 확인이 끝나면 **이름을 명시해** 지운다.

```bash
kubectl delete pod "$POD" --wait=false
```

> ⚠️ `kubectl delete pod -l ''` 처럼 빈 셀렉터를 쓰지 말 것. 네임스페이스의 모든 파드가 지워진다(실제로 한 번 발생).

---

### Task 5: 결과를 spec에 기록

**Files:**
- Modify: `docs/superpowers/specs/2026-08-17-flappy-race-gameplay-b2-design.md`

- [ ] **Step 1: §3에 "결과" 절 추가**

아래를 §3 끝(§4 앞)에 넣는다. 제목의 날짜는 `date +%Y-%m-%d`로 얻은 오늘 날짜를 쓴다.
마지막 줄에는 Task 2~4에서 **실제로 관측한 것**을 적는다 — 계획대로 흘렀으면 "없음"이라고 쓴다.

```markdown
### 결과 (YYYY-MM-DD)

- `BuildScript.BuildContentFull` — 타깃 비종속. CI가 `-buildTarget`으로 정한다.
- `content-deploy.yml`에 `build-deploy-gameserver` 잡 추가 → `s3://lop-assets/dev/StandaloneLinux64`.
  로컬 번들이 없는 서버에는 증분이 의미 없어 **full 빌드**로 간다.
- 검증: S3 카탈로그에 `FlappyRaceMap`·`Bird.prefab` 포함 확인, 서버 파드 로그에서
  `ChainOperation failed`·`instantiate is null` 소멸 확인.
- 예상과 달랐던 것: (관측한 것을 적는다)
```

- [ ] **Step 2: 커밋 + 머지**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git checkout -b docs/b2a-result
git add docs/superpowers/specs/2026-08-17-flappy-race-gameplay-b2-design.md
git commit -m "$(cat <<'EOF'
docs(flappy): B2-a 결과를 남긴다

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
git checkout main && git merge --no-ff docs/b2a-result -m "Merge docs/b2a-result: B2-a 결과 기록"
git branch -d docs/b2a-result
git push origin main
```

---

## 완료 기준

1. `s3://lop-assets/dev/StandaloneLinux64/catalog_0.1.bin`에 `FlappyRaceMap`과 `Bird.prefab`이 있다.
2. 게임서버 파드 로그에 `ChainOperation failed` / `instantiate is null`이 없다.
3. FlapWang도 이전과 같이 동작한다(같은 카탈로그에 `FlapWangMap`·`Knight`가 그대로 있는지 Task 2 Step 4에서 확인).
4. CI를 다시 돌리면 같은 결과가 재현된다(수기 단계 없음).

## 다음 슬라이스

B2-c(넷코드 게임 비종속화) 계획은 이 슬라이스가 끝난 뒤 `Reconciler`·`SnapshotHistory`·`LOPWorld`의 실제 코드를 읽고 별도 문서로 쓴다. 지금 쓰면 추측이 섞인다.
