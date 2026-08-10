# 플레이어 빌드 환경 선택 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** CI가 `-buildEnv dev`를 넘기면 dev 백엔드를 보는 APK가 나오고, 안 넘기면 빌드가 실패한다.

**Architecture:** 선택한 `EnvironmentSettings.<env>.asset`을 빌드 직전에
`EnvironmentSettings.active.asset`으로 복사하고 빌드 후 지운다. 플레이어 런타임은 항상 `.active`
하나만 로드하고, 없으면 예외를 던진다. 굽기/치우기 로직은 `EnvironmentBaker` 한 곳이 소유하고
CLI 빌드(`BuildScript`)와 에디터 GUI 빌드(`EnvironmentBuildProcessor` 훅)가 함께 쓴다.

**Tech Stack:** Unity 6000.3.16f1, C#, `AssetDatabase`, `IPreprocessBuildWithReport` /
`IPostprocessBuildWithReport`, GitHub Actions (self-hosted 맥 러너), AWS S3, `qrencode`

**Spec:** `docs/superpowers/specs/2026-08-10-player-build-environment-selection-design.md`

## Global Constraints

- **브랜치:** `worktree-build-env-selection` — 워크트리 `.claude/worktrees/build-env-selection`.
  main에 직접 커밋 금지.
- **⚠️ 워크트리에서는 컴파일을 검증할 수 없다.** 연결된 Unity 에디터는 **main 체크아웃**을 본다.
  Task 1~5는 컴파일 확인 없이 진행하고, **Task 6이 진짜 게이트**다. 이 레포에서 늘 그래왔다.
- **⚠️ 새 `.cs`의 `.meta`도 워크트리에서는 안 생긴다.** Unity가 그 폴더를 안 보기 때문. Task 6에서
  main 에디터가 생성한 것을 커밋한다. **`.meta`를 손으로 만들지 않는다**(CLAUDE.md).
- **⚠️ 플레이 모드 중에 재컴파일을 걸지 않는다.** 과거 `LOPEntityView.LateUpdate()` NRE 폭풍이
  났다. 리프레시 전에 플레이가 멈춰 있는지 사용자에게 확인한다.
- **커밋 금지 로컬 픽스처** (main 체크아웃에 있음, 건드리지 말 것):
  `.gitignore`의 `.claude/worktrees/` 한 줄 · `Assets/Art` · `Assets/Scenes/Room.unity` ·
  `ProjectSettings/QualitySettings.asset`.
- **환경 이름 3종 고정:** `dev`, `local-k8s`, `local`. 자산은
  `Assets/Resources/EnvironmentSettings/EnvironmentSettings.<이름>.asset`.
- **구워지는 자산 이름은 `EnvironmentSettings.active.asset`** — 상수
  `EnvironmentSettings.ActiveEnvironment = "active"`가 단일 출처다.
- **CLI 인자:** `-buildEnv <이름>` (APK 빌드에 **필수**), `-development` (플래그, 있으면 개발 빌드).
  유니티 CLI 규약대로 단일 대시 + camelCase.
- **콘텐츠 빌드(`BuildAndroidContentFull` / `BuildAndroidContentUpdate`)는 손대지 않는다.**
  Addressables 빌드는 플레이어 빌드 콜백을 타지 않고 `-buildEnv`도 필요 없다.
- **Addressables 프로파일은 `dev` 고정 유지.** `-buildEnv`는 백엔드 URL만 정한다.
- **유닛 테스트를 붙일 수 없다.** 클라 코드가 전부 `Assembly-CSharp`라 asmdef 참조가 불가능하다
  (기존 제약). 검증은 Task 6의 실증으로 대신하며, 각 태스크는 실행 가능한 정적 점검만 돌린다.

---

## File Structure

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/EnvironmentSettings.cs` (수정) | 런타임 해석 — 에디터=EditorPrefs 이름, 플레이어=`.active`, 실패=예외 |
| `Assets/Scripts/Editor/EnvironmentSwitcher.cs` (수정) | 상수 이름 갱신만. 동작 무변화 |
| `Assets/Editor/EnvironmentBaker.cs` (신규) | 굽기·치우기·CLI 인자 파싱의 **단일 소유자** |
| `Assets/Editor/EnvironmentBuildProcessor.cs` (신규) | 빌드 훅 — GUI 빌드 보조 + 자기가 구운 것만 정리 |
| `Assets/Editor/BuildScript.cs` (수정) | CLI 진입점 — `-buildEnv` 필수, `-development`, `finally` 정리 |
| `.gitignore` (수정) | `.active` 자산 무시 |
| `.github/workflows/client-app-deploy.yml` (수정) | 입력 2개 · 인자 전달 · 환경별 S3 경로 · QR |
| `.github/workflows/content-deploy.yml` (수정) | baseline 경로를 환경별로 |

---

## Task 1: 런타임 환경 해석

**Files:**
- Modify: `Assets/Scripts/EnvironmentSettings.cs` (전체)
- Modify: `Assets/Scripts/Editor/EnvironmentSwitcher.cs:39`

**Interfaces:**
- Consumes: (없음 — 첫 태스크)
- Produces: `EnvironmentSettings.ActiveEnvironment` (`const string` = `"active"`),
  `EnvironmentSettings.ResourceDirectory` (`const string` = `"EnvironmentSettings"`),
  `EnvironmentSettings.EditorDefaultEnvironment` (`const string` = `"local-k8s"`),
  `EnvironmentSettings.EditorPrefsKey` (`const string`, 기존),
  `static string EnvironmentSettings.ResourcePathFor(string environment)`.
  `DefaultEnvironment`는 **사라진다**.

- [ ] **Step 1: `EnvironmentSettings.cs`를 아래 내용으로 교체**

`Assets/Scripts/EnvironmentSettings.cs`:

```csharp
using System;
using UnityEngine;

namespace LOP
{
    [CreateAssetMenu(fileName = "EnvironmentSettings", menuName = "LOP/Internal/Environment Settings")]
    public class EnvironmentSettings : ScriptableObject
    {
        /// <summary>플레이어 빌드에 구워지는 환경 자산의 이름. 빌드가 선택한 환경을 이 이름으로 복사한다.</summary>
        public const string ActiveEnvironment = "active";

        /// <summary>환경 자산들이 사는 Resources 하위 폴더.</summary>
        public const string ResourceDirectory = "EnvironmentSettings";

        /// <summary>에디터에서 아직 환경을 고른 적 없을 때 쓰는 값. 플레이어 빌드와는 무관하다.</summary>
        public const string EditorDefaultEnvironment = "local-k8s";

        public const string EditorPrefsKey = "LOP.Environment";

        public static EnvironmentSettings _active;
        public static EnvironmentSettings active
        {
            get
            {
                if (_active == null)
                {
                    _active = Load();
                }
                return _active;
            }
        }

        public static void Reload()
        {
            _active = null;
        }

        public static string ResourcePathFor(string environment)
        {
            return $"{ResourceDirectory}/EnvironmentSettings.{environment}";
        }

        private static EnvironmentSettings Load()
        {
#if UNITY_EDITOR
            var environment = UnityEditor.EditorPrefs.GetString(EditorPrefsKey, EditorDefaultEnvironment);
#else
            // 플레이어 빌드에는 빌드 시점에 고른 환경 하나가 이 이름으로 구워져 있다.
            var environment = ActiveEnvironment;
#endif
            var path = ResourcePathFor(environment);
            var loaded = Resources.Load<EnvironmentSettings>(path);
            if (loaded == null)
            {
                //  틀린 서버에 조용히 붙느니 여기서 죽는다.
                throw new InvalidOperationException(
                    $"환경 설정을 찾을 수 없다: Resources/{path}. " +
                    "플레이어 빌드라면 빌드 시 -buildEnv 인자가 누락된 것이다.");
            }

            Debug.Log($"[LOP] environment={environment} lobby={loaded.lobbyServerBaseUrl}");
            return loaded;
        }

        [SerializeField] private string lobbyServerBaseUrl;
        [SerializeField] private string matchmakingServerBaseUrl;
        [SerializeField] private string roomServerBaseUrl;

        [SerializeField] private bool useLocalRoomInstance;
        [SerializeField] private string localRoomHost = "localhost";
        [SerializeField] private ushort localRoomPort = 7777;

        public string lobbyBaseURL => lobbyServerBaseUrl;
        public string matchmakingBaseURL => matchmakingServerBaseUrl;
        public string roomBaseURL => roomServerBaseUrl;

        public bool UseLocalRoomInstance => useLocalRoomInstance;
        public string LocalRoomHost => localRoomHost;
        public ushort LocalRoomPort => localRoomPort;
    }
}
```

> 미사용이던 `using System.Collections;` / `using System.Collections.Generic;`는 뺐고,
> `InvalidOperationException` 때문에 `using System;`은 남긴다.

- [ ] **Step 2: `EnvironmentSwitcher.cs:39`의 상수 참조 갱신**

기존:

```csharp
            var current = EditorPrefs.GetString(EnvironmentSettings.EditorPrefsKey, EnvironmentSettings.DefaultEnvironment);
```

변경:

```csharp
            var current = EditorPrefs.GetString(EnvironmentSettings.EditorPrefsKey, EnvironmentSettings.EditorDefaultEnvironment);
```

- [ ] **Step 3: 옛 이름이 남아 있지 않은지 확인**

Run:
```bash
grep -rn "DefaultEnvironment" --include="*.cs" Assets/ | grep -v "EditorDefaultEnvironment"
grep -rn "GetSelectedEnvironment" --include="*.cs" Assets/
```
Expected: 두 명령 모두 **출력 없음**.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/EnvironmentSettings.cs Assets/Scripts/Editor/EnvironmentSwitcher.cs
git commit -m "feat(env): 플레이어 빌드는 구워진 active 환경만 읽는다

없으면 조용히 local-k8s로 가지 않고 예외를 던진다. DefaultEnvironment는
이제 에디터에서만 쓰이므로 EditorDefaultEnvironment로 바꿨다 — 서버 레포에
같은 뜻의 같은 이름이 이미 있다."
```

---

## Task 2: 굽기/치우기 + 빌드 훅

**Files:**
- Create: `Assets/Editor/EnvironmentBaker.cs`
- Create: `Assets/Editor/EnvironmentBuildProcessor.cs`
- Modify: `.gitignore` (끝에 추가)

**Interfaces:**
- Consumes: `EnvironmentSettings.ActiveEnvironment`, `EnvironmentSettings.ResourceDirectory`,
  `EnvironmentSettings.EditorDefaultEnvironment`, `EnvironmentSettings.EditorPrefsKey` (Task 1)
- Produces: `LOP.EditorTools.EnvironmentBaker` — `static string EnvironmentFromCommandLine()`
  (없으면 `null`), `static string EnvironmentFromEditorPrefs()`, `static bool IsBaked()`,
  `static void Bake(string environment)` (실패 시 예외), `static void Clear()`,
  `static string ActiveAssetPath { get; }`, `static string SourceAssetPath(string environment)`

- [ ] **Step 1: `EnvironmentBaker.cs` 생성**

`Assets/Editor/EnvironmentBaker.cs`:

```csharp
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 선택한 환경 자산을 플레이어 빌드가 읽는 고정 이름(<c>EnvironmentSettings.active</c>)으로
    /// 굽고 치운다. CLI 빌드와 에디터 GUI 빌드가 함께 쓰는 단일 소유자다.
    /// </summary>
    public static class EnvironmentBaker
    {
        public const string BuildEnvArgument = "-buildEnv";

        //  자산 이름 규칙은 EnvironmentSettings.ResourcePathFor 하나가 정한다 — 두 곳에 적으면 어긋난다.
        public static string ActiveAssetPath => SourceAssetPath(EnvironmentSettings.ActiveEnvironment);

        public static string SourceAssetPath(string environment) =>
            $"Assets/Resources/{EnvironmentSettings.ResourcePathFor(environment)}.asset";

        /// <summary>커맨드라인의 <c>-buildEnv &lt;이름&gt;</c>. 없거나 값이 비면 null.</summary>
        public static string EnvironmentFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, BuildEnvArgument);
            if (index < 0 || index + 1 >= args.Length)
            {
                return null;
            }

            var value = args[index + 1];
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public static string EnvironmentFromEditorPrefs()
        {
            return EditorPrefs.GetString(
                EnvironmentSettings.EditorPrefsKey, EnvironmentSettings.EditorDefaultEnvironment);
        }

        public static bool IsBaked()
        {
            return File.Exists(ActiveAssetPath);
        }

        public static void Bake(string environment)
        {
            if (string.IsNullOrWhiteSpace(environment))
            {
                throw new ArgumentException("환경 이름이 비어 있다.", nameof(environment));
            }

            var source = SourceAssetPath(environment);
            if (!File.Exists(source))
            {
                throw new InvalidOperationException($"환경 자산이 없다: {source}");
            }

            Clear();

            if (!AssetDatabase.CopyAsset(source, ActiveAssetPath))
            {
                throw new InvalidOperationException($"환경 자산 복사 실패: {source} -> {ActiveAssetPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[LOP] 활성 환경 구움: {environment} -> {ActiveAssetPath}");
        }

        public static void Clear()
        {
            if (!File.Exists(ActiveAssetPath))
            {
                return;
            }

            AssetDatabase.DeleteAsset(ActiveAssetPath);
            AssetDatabase.Refresh();
            Debug.Log($"[LOP] 활성 환경 치움: {ActiveAssetPath}");
        }
    }
}
```

- [ ] **Step 2: `EnvironmentBuildProcessor.cs` 생성**

`Assets/Editor/EnvironmentBuildProcessor.cs`:

```csharp
using System;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace LOP.EditorTools
{
    /// <summary>
    /// 플레이어 빌드 전후로 활성 환경 자산을 챙긴다.
    /// CLI 빌드는 <c>BuildScript</c>가 미리 구워 두므로 여기서는 손대지 않고,
    /// 에디터 Build Settings 창으로 굽지 않고 빌드한 경우만 현재 선택으로 대신 구워 준다.
    /// </summary>
    public class EnvironmentBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        //  이 훅이 구운 경우에만 치운다. CLI 빌드가 구운 것은 BuildScript의 finally가 책임진다.
        private static bool bakedHere;

        public void OnPreprocessBuild(BuildReport report)
        {
            bakedHere = false;

            if (EnvironmentBaker.IsBaked())
            {
                return;
            }

            var environment = EnvironmentBaker.EnvironmentFromEditorPrefs();
            try
            {
                EnvironmentBaker.Bake(environment);
            }
            catch (Exception e)
            {
                throw new BuildFailedException($"활성 환경을 구울 수 없다(환경: {environment}). {e.Message}");
            }

            bakedHere = true;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!bakedHere)
            {
                return;
            }

            bakedHere = false;
            EnvironmentBaker.Clear();
        }
    }
}
```

- [ ] **Step 3: `.gitignore` 끝에 추가**

`.gitignore` 마지막 줄(`*.slnx`) 다음에:

```
# 빌드 때 생성되는 활성 환경 자산 (EnvironmentBaker가 굽고 지운다)
/Assets/Resources/EnvironmentSettings/EnvironmentSettings.active.asset
/Assets/Resources/EnvironmentSettings/EnvironmentSettings.active.asset.meta
```

- [ ] **Step 4: 무시 규칙이 실제로 먹는지 확인**

Run:
```bash
touch Assets/Resources/EnvironmentSettings/EnvironmentSettings.active.asset
git status --short Assets/Resources/EnvironmentSettings/
rm Assets/Resources/EnvironmentSettings/EnvironmentSettings.active.asset
```
Expected: `git status --short` 출력이 **비어 있어야** 한다(무시되므로).

- [ ] **Step 5: 커밋**

```bash
git add Assets/Editor/EnvironmentBaker.cs Assets/Editor/EnvironmentBuildProcessor.cs .gitignore
git commit -m "feat(build): 선택한 환경 자산을 active로 굽고 치운다

굽기/치우기/인자 파싱을 EnvironmentBaker 하나가 소유하고, CLI 빌드와
GUI 빌드가 함께 쓴다. 훅은 자기가 구운 것만 치운다."
```

> `.meta`는 워크트리에서 안 생긴다. Task 6에서 main 에디터가 만든 것을 커밋한다.

---

## Task 3: `BuildScript` CLI 인자

**Files:**
- Modify: `Assets/Editor/BuildScript.cs:36-62` (`BuildAndroidApk`) + `using` 추가

**Interfaces:**
- Consumes: `LOP.EditorTools.EnvironmentBaker.EnvironmentFromCommandLine()`,
  `EnvironmentBaker.Bake(string)`, `EnvironmentBaker.Clear()` (Task 2)
- Produces: CLI 계약 — `BuildScript.BuildAndroidApk`는 `-buildEnv <이름>`을 요구하고
  `-development`를 선택으로 받는다. 인자 누락 시 종료 코드 2, 빌드 실패 시 1, 성공 시 0.

- [ ] **Step 1: 파일 상단에 `using` 추가**

`Assets/Editor/BuildScript.cs` 1~6행의 `using` 목록 끝(`using UnityEngine;` 다음)에 추가:

```csharp
using LOP.EditorTools;
```

- [ ] **Step 2: `BuildAndroidApk`를 아래로 교체**

기존 `BuildAndroidApk` 메서드(36~62행) 전체를 교체:

```csharp
    // ── APK 빌드 (③a). 디버그 서명(프로젝트 기본). 콘텐츠는 별도 스텝에서 이미 빌드했으므로 재빌드 안 함.
    //    -buildEnv <이름> 필수. -development면 개발 빌드로(평문 http가 개발 빌드에서만 통한다).
    public static void BuildAndroidApk()
    {
        var environment = EnvironmentBaker.EnvironmentFromCommandLine();
        if (string.IsNullOrEmpty(environment))
        {
            Debug.LogError("-buildEnv <환경> 인자가 필요하다. 예: -buildEnv dev");
            EditorApplication.Exit(2);
            return;
        }

        try
        {
            EnvironmentBaker.Bake(environment);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"활성 환경 굽기 실패: {e.Message}");
            EditorApplication.Exit(2);
            return;
        }

        var development = HasFlag("-development");
        int exitCode;
        try
        {
            var settings = EnsureSettings();
            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;

            var buildOptions = BuildOptions.None;
            if (development)
            {
                buildOptions |= BuildOptions.Development;
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Build/lop.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = buildOptions,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError($"APK build FAILED: {summary.result}, errors={summary.totalErrors}");
                exitCode = 1;
            }
            else
            {
                Debug.Log($"APK OK: {summary.outputPath}, size={summary.totalSize} bytes, " +
                          $"env={environment}, development={development}");
                exitCode = 0;
            }
        }
        finally
        {
            //  빌드가 실패하면 후처리 훅이 돌지 않는다.
            EnvironmentBaker.Clear();
        }

        EditorApplication.Exit(exitCode);
    }

    static bool HasFlag(string name)
    {
        return System.Array.IndexOf(System.Environment.GetCommandLineArgs(), name) >= 0;
    }
```

> **`EditorApplication.Exit`을 `try` 안에서 부르지 않는다** — 프로세스가 즉시 끝나 `finally`가
> 돌지 않고 `.active`가 남는다. 그래서 종료 코드를 담아 두고 `finally` 뒤에 한 번만 부른다.

- [ ] **Step 3: 콘텐츠 빌드 메서드가 안 바뀌었는지 확인**

Run:
```bash
grep -n "buildEnv\|BuildOptions.Development" Assets/Editor/BuildScript.cs
grep -c "EditorApplication.Exit" Assets/Editor/BuildScript.cs
```
Expected: 첫 명령은 `BuildAndroidApk` 안(그리고 주석)에서만 잡힌다 —
`BuildAndroidContentFull` / `BuildAndroidContentUpdate` 본문에는 없어야 한다.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Editor/BuildScript.cs
git commit -m "feat(build): APK 빌드에 -buildEnv 필수 + -development 플래그

인자를 빼먹으면 빌드가 실패한다 — 조용히 local-k8s로 나가지 않는다.
Exit은 finally 뒤에서 한 번만 부른다(try 안에서 부르면 정리가 안 돈다)."
```

---

## Task 4: 워크플로 — 환경 입력과 경로

**Files:**
- Modify: `.github/workflows/client-app-deploy.yml:1-3` (트리거), `:65-74` (APK 스텝),
  `:76-91` (보존 스텝)
- Modify: `.github/workflows/content-deploy.yml:45-46` (baseline 경로)

**Interfaces:**
- Consumes: Task 3의 CLI 계약 (`-buildEnv`, `-development`)
- Produces: S3 레이아웃 `s3://lop-client/builds/<env>/<sha>/{lop.apk,addressables_content_state.bin}`
  와 포인터 `s3://lop-client/builds/<env>/latest.json`. Task 5가 같은 `<env>/<sha>` 경로를 쓴다.

- [ ] **Step 1: `client-app-deploy.yml`의 트리거에 입력 두 개 추가**

1~3행:

```yaml
name: client-app-deploy
on:
  workflow_dispatch:
```

을 아래로 교체:

```yaml
name: client-app-deploy
on:
  workflow_dispatch:
    inputs:
      environment:
        description: '대상 백엔드 환경'
        type: choice
        options:
          - dev
          - local-k8s
          - local
        default: dev
      development:
        description: '개발 빌드 (평문 http 허용 · 로그/프로파일러 유지)'
        type: boolean
        default: true
```

- [ ] **Step 2: APK 빌드 스텝에 인자 전달**

"APK 빌드 (Android, 디버그 서명)" 스텝 전체를 교체:

```yaml
      - name: APK 빌드 (Android, 디버그 서명)
        env:
          BUILD_ENV: ${{ inputs.environment }}
          DEVELOPMENT: ${{ inputs.development }}
        run: |
          set -eo pipefail
          UNITY="/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity"
          DEV_FLAG=""
          if [ "$DEVELOPMENT" = "true" ]; then DEV_FLAG="-development"; fi
          if ! "$UNITY" -batchmode -quit -nographics -buildTarget Android -projectPath . \
                -executeMethod BuildScript.BuildAndroidApk \
                -buildEnv "$BUILD_ENV" $DEV_FLAG -logFile - > unity-apk.log 2>&1; then
            echo "::error::APK build failed"; tail -80 unity-apk.log; exit 1
          fi
          tail -15 unity-apk.log
          test -f Build/lop.apk
```

- [ ] **Step 3: 보존 스텝의 S3 경로를 환경별로**

"APK + content_state 보존 (버전드) + latest 갱신" 스텝 전체를 교체:

```yaml
      - name: APK + content_state 보존 (버전드) + latest 갱신
        env:
          AWS_ACCESS_KEY_ID: ${{ secrets.AWS_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          BUILD_ENV: ${{ inputs.environment }}
        run: |
          set -e
          SHA="${{ steps.tag.outputs.sha }}"
          DEST="s3://lop-client/builds/$BUILD_ENV/$SHA"
          aws s3 cp Build/lop.apk "$DEST/lop.apk"
          aws s3 cp Assets/AddressableAssetsData/Android/addressables_content_state.bin \
            "$DEST/addressables_content_state.bin"
          # latest 포인터 (③b가 읽음) — 환경별로 따로 둔다. 한 개면 dev 아닌 빌드가 baseline을 덮는다.
          printf '{"sha":"%s","environment":"%s","apk":"%s/lop.apk","content_state":"%s/addressables_content_state.bin"}\n' \
            "$SHA" "$BUILD_ENV" "$DEST" "$DEST" > latest.json
          aws s3 cp latest.json "s3://lop-client/builds/$BUILD_ENV/latest.json"
          echo "보존: $DEST/ , builds/$BUILD_ENV/latest.json -> $SHA"
          # 공유용 presigned URL (7일)
          aws s3 presign "$DEST/lop.apk" --expires-in 604800
```

- [ ] **Step 4: `content-deploy.yml`의 baseline 경로 갱신**

45~46행:

```yaml
          if ! aws s3 cp "s3://lop-client/builds/latest.json" latest.json 2>/dev/null; then
            echo "::error::latest.json 없음 — ③a(client-app-deploy)를 먼저 실행해 baseline을 만드세요."; exit 1
```

을 아래로 교체:

```yaml
          if ! aws s3 cp "s3://lop-client/builds/dev/latest.json" latest.json 2>/dev/null; then
            echo "::error::builds/dev/latest.json 없음 — ③a(client-app-deploy)를 dev로 먼저 실행해 baseline을 만드세요."; exit 1
```

- [ ] **Step 5: YAML이 유효한지 확인**

Run:
```bash
python3 -c "import yaml,sys
for f in ['.github/workflows/client-app-deploy.yml','.github/workflows/content-deploy.yml']:
    d=yaml.safe_load(open(f,encoding='utf-8'))
    print(f,'OK')
i=yaml.safe_load(open('.github/workflows/client-app-deploy.yml',encoding='utf-8'))[True]['workflow_dispatch']['inputs']
print('inputs:',sorted(i))
print('default env:',i['environment']['default'],'options:',i['environment']['options'])"
```
Expected:
```
.github/workflows/client-app-deploy.yml OK
.github/workflows/content-deploy.yml OK
inputs: ['development', 'environment']
default env: dev options: ['dev', 'local-k8s', 'local']
```

> YAML에서 맨 위 `on:` 키는 불리언 `True`로 파싱된다. 위 스크립트가 `[True]`로 읽는 이유다.

- [ ] **Step 6: 커밋**

```bash
git add .github/workflows/client-app-deploy.yml .github/workflows/content-deploy.yml
git commit -m "ci(client): 빌드 환경을 워크플로 입력으로 + S3 경로를 환경별로

경로를 안 가르면 dev 아닌 APK를 한 번만 뽑아도 콘텐츠 파이프라인의
baseline이 조용히 덮인다."
```

---

## Task 5: QR 코드 게시

**Files:**
- Modify: `.github/workflows/client-app-deploy.yml` (보존 스텝 끝의 `presign` 한 줄 제거 +
  새 스텝 추가)

**Interfaces:**
- Consumes: Task 4의 S3 레이아웃 (`s3://lop-client/builds/<env>/<sha>/lop.apk`)
- Produces: 잡 요약에 QR 이미지와 다운로드 링크. `qr.png`가 같은 `<env>/<sha>` 경로에 남는다.

- [ ] **Step 1: 보존 스텝 끝의 presign 두 줄 제거**

Task 4에서 넣은 보존 스텝의 마지막 두 줄을 지운다(다음 스텝으로 옮긴다):

```
          # 공유용 presigned URL (7일)
          aws s3 presign "$DEST/lop.apk" --expires-in 604800
```

- [ ] **Step 2: 파일 끝에 QR 스텝 추가**

`client-app-deploy.yml`의 마지막 스텝 다음에:

```yaml
      - name: QR + 다운로드 링크 게시
        env:
          AWS_ACCESS_KEY_ID: ${{ secrets.AWS_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          BUILD_ENV: ${{ inputs.environment }}
        run: |
          set -eo pipefail
          if ! command -v qrencode >/dev/null; then
            echo "::error::러너에 qrencode가 없다. 맥 러너에서 'brew install qrencode' 1회 실행할 것."
            exit 1
          fi
          SHA="${{ steps.tag.outputs.sha }}"
          DEST="s3://lop-client/builds/$BUILD_ENV/$SHA"
          APK_URL=$(aws s3 presign "$DEST/lop.apk" --expires-in 604800)
          # GitHub 요약은 data: URI 이미지를 걷어낸다 — PNG를 올려서 URL로 참조해야 한다.
          qrencode -o qr.png -s 8 "$APK_URL"
          aws s3 cp qr.png "$DEST/qr.png"
          QR_URL=$(aws s3 presign "$DEST/qr.png" --expires-in 604800)
          {
            echo "## $BUILD_ENV 빌드 \`$SHA\`"
            echo
            echo "![QR]($QR_URL)"
            echo
            echo "[APK 직접 다운로드]($APK_URL)"
            echo
            echo "링크는 7일 후 만료된다."
          } >> "$GITHUB_STEP_SUMMARY"
```

- [ ] **Step 3: YAML 유효성과 스텝 구성 확인**

Run:
```bash
python3 -c "import yaml
d=yaml.safe_load(open('.github/workflows/client-app-deploy.yml',encoding='utf-8'))
steps=d['jobs']['build-deploy']['steps']
print('steps:',len(steps))
print('last:',steps[-1]['name'])
print('presign in preserve step:','presign' in steps[-2]['run'])"
```
Expected:
```
steps: 9
last: QR + 다운로드 링크 게시
presign in preserve step: False
```

> 이 워크플로는 이 태스크 전까지 스텝이 8개다(체크아웃 · sha · UPM · NuGet · 콘텐츠 빌드 ·
> 콘텐츠 업로드 · APK 빌드 · 보존). QR이 9번째다.

- [ ] **Step 4: 커밋**

```bash
git add .github/workflows/client-app-deploy.yml
git commit -m "ci(client): 빌드 결과를 QR로 잡 요약에 게시

폰으로 찍어 바로 받는다. data: URI는 GitHub이 걷어내므로 QR PNG를 S3에
올려 참조하고, 이미지가 안 떠도 되도록 링크를 함께 남긴다."
```

---

## Task 6: 머지 · `.meta` · 실증 검증 (진짜 게이트)

**Files:**
- Modify: main 체크아웃 `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client`
- Create: 새 `.cs` 두 개의 `.meta` (Unity가 생성)

**Interfaces:**
- Consumes: Task 1~5 전부
- Produces: main에 머지된 동작하는 기능

- [ ] **Step 1: 사용자에게 플레이 모드 정지를 확인받는다**

플레이 중에 리프레시하면 `LOPEntityView.LateUpdate()` NRE가 쏟아진다. **반드시 먼저 물어본다.**

- [ ] **Step 2: main에 머지 (`.gitignore` 로컬 한 줄 보존)**

main 체크아웃의 `.gitignore`에는 커밋하지 않은 `.claude/worktrees/` 줄이 있고, 이 브랜치도
`.gitignore`를 건드렸으므로 머지가 거부될 수 있다.

**stash 스택은 다른 워크트리·세션과 공유된다.** 절대 맨 `git stash` / `git stash pop`을 쓰지 않고,
태그로 밀어 넣고 SHA로 되찾는다.

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
TAG="gitignore-local-worktree-line"
git stash push -u -m "$TAG" -- .gitignore
SHA=$(git stash list --format='%H %gs' | grep -F "$TAG" | head -1 | cut -d' ' -f1)
echo "stash SHA=$SHA"

git merge --no-ff worktree-build-env-selection \
  -m "Merge worktree-build-env-selection: 플레이어 빌드 환경 선택"

git stash apply "$SHA"          # pop 아님 — 스택을 공유하므로
```

`apply`가 충돌하면 두 변경을 **둘 다** 남긴다(브랜치의 `.active` 무시 두 줄 + 로컬
`.claude/worktrees/` 줄). 정리되면 그 항목을 태그로 다시 찾아 지운다:

```bash
git stash list --format='%gd %gs' | grep -F "$TAG"   # 현재 stash@{n} 확인
git stash drop 'stash@{n}'                            # 위에서 본 번호로
git status --short .gitignore                          # ' M .gitignore' 이어야 한다
```

- [ ] **Step 3: Unity 리프레시 — 컴파일 확인 + `.meta` 생성**

UnityMCP `refresh_unity`(scope=all, mode=force)를 클라 인스턴스에 걸고 `read_console`로 확인한다.
`unity_instance`는 `mcpforunity://instances`에서 `LeagueOfPhysical-Client`의 전체 id로.

Expected: **컴파일 에러 0.** 그리고:

```bash
git status --short Assets/Editor/
```
Expected: `?? Assets/Editor/EnvironmentBaker.cs.meta`, `?? Assets/Editor/EnvironmentBuildProcessor.cs.meta`

- [ ] **Step 4: `.meta` 커밋**

```bash
git add Assets/Editor/EnvironmentBaker.cs.meta Assets/Editor/EnvironmentBuildProcessor.cs.meta
git commit -m "chore: 새 에디터 스크립트 .meta 추가"
```

- [ ] **Step 5: 사용자에게 Unity 에디터를 닫아 달라고 요청**

**Unity는 프로젝트 폴더를 잠근다.** 에디터가 열려 있으면 아래 CLI 실행이
"project is already open"으로 실패한다. Step 3·4는 에디터가 **열려 있어야** 했고,
Step 6~8은 **닫혀 있어야** 한다. 닫혔는지 확인받고 진행한다.

- [ ] **Step 6: 음수 케이스 — 인자 누락**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe"
"$UNITY" -batchmode -quit -nographics -buildTarget Android -projectPath . \
  -executeMethod BuildScript.BuildAndroidApk -logFile - > /tmp/apk-noenv.log 2>&1
echo "exit=$?"
grep -n "buildEnv" /tmp/apk-noenv.log | head -3
```
Expected: `exit=2` + `-buildEnv <환경> 인자가 필요하다` 가 로그에 있다. `Build/lop.apk`가 생기지 않는다.

- [ ] **Step 7: 음수 케이스 — 없는 환경**

```bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe"
"$UNITY" -batchmode -quit -nographics -buildTarget Android -projectPath . \
  -executeMethod BuildScript.BuildAndroidApk -buildEnv nonexistent -logFile - > /tmp/apk-badenv.log 2>&1
echo "exit=$?"
grep -n "환경 자산이 없다" /tmp/apk-badenv.log | head -3
```
Expected: `exit=2` + `환경 자산이 없다: Assets/Resources/EnvironmentSettings/EnvironmentSettings.nonexistent.asset`

- [ ] **Step 8: 정상 케이스 — 로컬 CLI 빌드**

```bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe"
"$UNITY" -batchmode -quit -nographics -buildTarget Android -projectPath . \
  -executeMethod BuildScript.BuildAndroidApk -buildEnv dev -development -logFile - > /tmp/apk-dev.log 2>&1
echo "exit=$?"
grep -nE "활성 환경 구움|APK OK|활성 환경 치움" /tmp/apk-dev.log
git status --short Assets/Resources/EnvironmentSettings/
```
Expected: `exit=0`, 로그에 `활성 환경 구움: dev` → `APK OK: ... env=dev, development=True` →
`활성 환경 치움` 순으로 찍힌다. `git status`는 **비어 있어야** 한다(치워졌고 무시되므로).

> 이 윈도우 머신에 안드로이드 빌드 모듈이 없으면 이 스텝만 실패한다. 그때는 건너뛰고
> CI(Step 10)에서 확인한다 — **Step 6·7은 여전히 유효하다.** 인자 검사와 굽기는
> `BuildPipeline.BuildPlayer` 호출 전에 일어나므로 모듈과 무관하다.
>
> 끝나면 사용자가 에디터를 다시 열어도 된다.

- [ ] **Step 9: 맥 러너에 `qrencode` 설치 (사용자)**

러너에서 `brew install qrencode` 1회. 안 하면 QR 스텝이 실패한다(APK 업로드는 이미 끝난 뒤).

- [ ] **Step 10: CI 1회 실행**

```bash
gh workflow run client-app-deploy.yml -f environment=dev -f development=true
```

확인할 것:
1. 잡 요약에 QR 이미지가 **뜨는가** (안 뜨면 링크로 대체 — 별건으로 기록)
2. QR을 폰으로 찍어 APK가 받아지는가
3. 설치 후 `adb logcat | grep "\[LOP\] environment"` → `environment=active lobby=http://115.68.178.46:31000/lobby`
4. 로그인·로비까지 진행되는가

> **3번의 `environment=active`는 정상이다** — 플레이어 빌드는 구워진 자산 이름을 찍는다.
> 어느 환경인지는 뒤따르는 `lobby=` URL로 판별한다.
>
> **4번은 dev 백엔드가 옛 버전(1b/1c 이전)이라 중간에 막힐 수 있다.** `POST /lobby/auth/introspect`가
> 404임을 이미 확인했다. 로비까지만 되어도 "dev를 보고 있다"는 증명으로 충분하며, 그 이상은
> dev 백엔드 최신화 뒤로 미룬다.

- [ ] **Step 11: ROADMAP에 기록하고 커밋**

`docs/ROADMAP.md`의 "▶ 다음 (Next)" 절 바로 앞에 아래를 넣는다. **Step 10에서 실제로 관측한 것으로
대괄호 부분을 채운다** — 안 해본 것을 했다고 적지 않는다.

```markdown
## ✅ 플레이어 빌드 환경 선택 (2026-08-10)

CI가 만든 APK가 **항상 `local-k8s`(= `http://localhost`)를 봤다.** 폰에서는 자기 자신이라
아무것도 안 됐고, 고를 방법 자체가 없었다(`#else` 분기가 상수 하나로 고정).

**두 겹이 막고 있었다.** 환경 고정이 하나, 그리고 `insecureHttpOption=DevelopmentOnly` —
릴리스 APK는 dev의 평문 http를 통째로 차단한다. **빌드는 성공하고 실행하면 죽는** 그 실패
모양이다(07-30 게임서버와 동일). 그래서 개발 빌드로 뽑기로 했다. 프로젝트 세팅을 안 건드리니
`DevelopmentOnly`가 뜻 그대로 남고, 로그·프로파일러가 살아 **빌드에서 넷코드를 재보려던
미검증 항목**도 이 빌드로 처리할 수 있다.

**방식**: 선택한 `EnvironmentSettings.<env>.asset`을 빌드 직전 `.active`로 복사하고 후에 지운다.
검토한 셋 중 **커밋된 파일을 하나도 안 건드리는 유일한 길**이라 골랐다 — Preloaded Assets는
유니티가 이 용도로 둔 공식 API지만 `ProjectSettings.asset`에 저장돼, 실수로 커밋되면 모든 빌드가
조용히 그 환경으로 나간다. (⚠️ "커밋된 SO를 메모리에서 수정"은 **동작 자체를 안 한다** — 빌드 중
도메인 리로드가 디스크 상태로 되돌린다.)

- `-buildEnv <이름>` 필수 + `-development` 플래그. 누락 시 **빌드 실패**
- 굽기/치우기는 `EnvironmentBaker` 하나가 소유. CLI는 `BuildScript`가, GUI 빌드는 훅이 부른다
- S3 경로를 환경별로 갈랐다 — 안 가르면 dev 아닌 APK 한 번에 **콘텐츠 baseline이 조용히 덮인다**
- 빌드 결과를 **QR로 잡 요약에 게시**(폰으로 찍어 바로 설치). `data:` URI는 GitHub이 걷어내므로
  QR PNG를 S3에 올려 참조하고, 이미지가 안 떠도 되도록 링크를 함께 남긴다

**검증**: [인자 누락·없는 환경 → 빌드 실패 확인 여부] · [로컬 CLI 빌드 결과] ·
[CI 실행 + 폰 설치 + `adb logcat`의 `lobby=` URL] · [QR 렌더링 여부]

> **dev 백엔드가 인증 cutover 1b/1c 이전 버전이라 게임 진입까지는 검증 못 했다**
> (`POST /lobby/auth/introspect` → 404 확인). [실제로 어디까지 됐는지]. dev 최신화는 별건.

spec `2026-08-10-player-build-environment-selection-design.md`,
plan `2026-08-10-player-build-environment-selection.md`.
```

```bash
git add docs/ROADMAP.md
git commit -m "docs(roadmap): 플레이어 빌드 환경 선택 완료"
```

- [ ] **Step 12: 푸시 (사용자 확인 후)**

```bash
git push origin main
```

---

## 이번 범위 밖 (스펙과 동일 — 손대지 말 것)

- Addressables 프로파일 (`dev` 고정 유지)
- 환경별 번들 ID·앱 이름
- 나머지 환경 자산을 `Resources/` 밖으로
- DebugHud에 환경 표시
- iOS 빌드 (`-buildEnv` 배관은 그대로 재사용되지만 서명·배포 채널이 별건)
- dev 백엔드 최신화 / dev HTTPS 전환 (다른 기계의 인증 트랙과 겹친다)
