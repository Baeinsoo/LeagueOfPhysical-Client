# 플레이어 빌드 환경 선택 설계

CI가 만든 APK가 어느 백엔드를 볼지 **빌드 인자로 정한다**. 지금은 정할 방법이 없어 항상
`local-k8s`(= `http://localhost`)를 보고, 폰에서는 자기 자신을 가리켜 아무것도 안 된다.

## 배경 — 지금 무엇이 막혀 있나

`Assets/Scripts/EnvironmentSettings.cs`가 환경을 이렇게 고른다.

```csharp
private static string GetSelectedEnvironment()
{
#if UNITY_EDITOR
    return UnityEditor.EditorPrefs.GetString(EditorPrefsKey, DefaultEnvironment);
#else
    return DefaultEnvironment;   // = "local-k8s"  ← 플레이어 빌드는 선택지가 없다
#endif
}
```

에디터는 `LOP/Environment/` 메뉴(`EnvironmentSwitcher`)로 고르지만, **플레이어 빌드는 상수 하나로
고정**된다. `Assets/Editor/BuildScript.cs`에도 환경을 다루는 코드가 없다. 결과적으로
`client-app-deploy`가 뽑는 APK는 `http://localhost/lobby`를 호출한다.

### 같이 막고 있는 것 — 평문 http 차단

| | 값 | 뜻 |
|---|---|---|
| 클라 `insecureHttpOption` | `1` | **DevelopmentOnly** — 개발 빌드에서만 평문 http 허용 |
| 서버 `insecureHttpOption` | `2` | AlwaysAllowed |
| dev 백엔드 | `http://115.68.178.46:31000/...` | 평문 |

환경만 고쳐도 **릴리스 APK는 빌드에 성공하고 실행하면 모든 백엔드 호출이 죽는다.** 게임서버가
2026-07-30에 똑같이 당했고("에디터에선 되고 플레이어 빌드에서만 깨짐"), 그때 *"클라는
`DevelopmentOnly` 유지 — 플레이어 빌드 시 결정"* 으로 미뤄둔 항목이 이것이다.

**결정: 개발 빌드로 뽑는다.** 프로젝트 세팅을 안 건드리므로 `DevelopmentOnly`가 뜻 그대로 남고,
"나중에 되돌리기"를 잊을 위험이 없다. 로그·프로파일러가 살아 있어 **빌드에서 넷코드를 재보려던
미검증 항목**도 그때 함께 처리할 수 있다(릴리스 빌드면 그 측정 자체가 불가능하다). 유저 배포용
릴리스 APK가 필요해지는 시점에는 dev를 HTTPS로 올리는 것이 강제되는데, 그것이 올바른 순서다.

### 서버는 이 문제가 없다

서버 레포의 `local-k8s`와 `dev` 자산이 **같은 값**(`http://lobby-server-service` 등 클러스터 내부
DNS)이라, 하드코딩된 기본값이 어느 클러스터에서든 맞는다. 이 설계는 **클라 전용**이다.

## 결정 — 선택한 자산을 고정 이름으로 굽는다

빌드 직전에 선택된 `EnvironmentSettings.<env>.asset`을
`EnvironmentSettings.active.asset`으로 **복사**하고, 빌드가 끝나면 지운다. 플레이어 런타임은 항상
`.active` 하나만 로드한다.

### 검토한 대안

유니티에서 값을 플레이어로 넘기는 길은 **데이터로 굽기**와 **코드로 굽기** 둘뿐이다.
(`EditorPrefs`·정적 변수·빌드 옵션은 에디터 프로세스에만 살아서 쓸 수 없다.)

| 방식 | 빌드 때 건드리는 것 | 채택 안 한 이유 |
|---|---|---|
| **자산 복사 → `.active`** | 새 파일 생성 (git 미추적) | **채택** |
| `PlayerSettings.SetPreloadedAssets` | `ProjectSettings.asset` (커밋된 파일) | 유니티가 이 용도로 둔 공식 API지만, 실수로 커밋되면 **모든 빌드가 조용히 그 환경으로** 나간다 |
| `BuildPlayerOptions.extraScriptingDefines` | 없음 | 환경마다 `#if` 한 줄이 코드에 박힌다. 전투 상수까지 MasterData로 뺀 이 코드베이스의 방향과 반대 |
| 이름만 담은 텍스트 파일 | 새 파일 생성 | 자산 복사와 수명이 같은데 런타임에 문자열 대조가 남는다. 자산 복사가 상위 호환 |
| 커밋된 SO를 메모리에서 수정 | — | **동작하지 않는다.** 빌드 중 도메인 리로드가 일어나면 SO가 디스크 상태로 되돌아간다 |

**채택 근거:** 세 후보 모두 정당하지만, 자산 복사만이 **커밋된 파일을 하나도 안 건드린다.** 이
프로젝트가 반복해서 당한 실패 모양이 "빌드는 되는데 런타임에 조용히 틀림"(마스터데이터 누락,
평문 http 차단)이고, Preloaded Assets는 정확히 그 모양을 하나 더 만든다. 또한 자산 복사는
유니티 표준 런타임 설정 패턴(`Resources` + `Resources.Load`) 안에 그대로 머문다.

### 산업 표준 매핑

- **`Resources` + `Resources.Load` = 유니티 런타임 설정의 표준 형태.** 이미 이 프로젝트가 쓰는
  방식이고 Unity Manual·커뮤니티 정설(Hextant Studios "Custom Runtime and Editor Settings")이
  같은 모양이다. 이 설계는 그 패턴을 벗어나지 않는다.
- **`IPreprocessBuildWithReport` / `IPostprocessBuildWithReport` = 빌드 전후 개입의 공식 훅.**
  Unity Manual "Introduction to customizing the build pipeline"이 지정한 자리다.
- **`BuildFailedException` = 전처리에서 빌드를 중단시키는 유니티 표준 수단.**
- **CLI 인자 표기**는 유니티 자체 규약(`-buildTarget`, `-projectPath`, `-executeMethod`)을 따라
  단일 대시 + camelCase로 `-buildEnv`, `-development`.
- **클래스 이름 `EnvironmentBuildProcessor`** — 유니티 자체 타입 `BuildPlayerProcessor`의
  `...BuildProcessor` 접미사에 맞춘다.
- **환경 이름 상수 `EditorDefaultEnvironment`** — 서버 레포 `EnvironmentSettings`에 **이미 같은
  이름이 같은 뜻으로 존재**한다. 클라도 그 이름을 쓰면 두 레포 어휘가 맞는다.
- ⚠️ **유니티 문서가 경고하는 함정**: 배치 모드에서 `PlayerSettings.SetScriptingDefineSymbols`를
  호출하고 곧바로 `BuildPipeline.BuildPlayer`를 부르면 심볼이 적용되지 않는다(재컴파일 기회가
  없다). 이 설계는 심볼을 쓰지 않으므로 해당 없음. 후일 심볼 방식으로 선회한다면
  `BuildPlayerOptions.extraScriptingDefines`만 써야 한다.

## 흐름

```
GitHub Actions  [environment: dev, development: true]
  │
  └─ Unity -executeMethod BuildScript.BuildAndroidApk -buildEnv dev -development
       │
       ├─ EnvironmentBuildProcessor.OnPreprocessBuild
       │     환경 결정: CLI -buildEnv → 없으면 EditorPrefs
       │     EnvironmentSettings.dev.asset ──복사──▶ EnvironmentSettings.active.asset
       │
       ├─ BuildPipeline.BuildPlayer (BuildOptions.Development)
       │
       └─ EnvironmentBuildProcessor.OnPostprocessBuild
             EnvironmentSettings.active.asset ──삭제
  │
  ├─ s3://lop-client/builds/dev/<sha>/lop.apk
  └─ QR PNG + 잡 요약에 게시

[폰] APK 실행
  └─ Resources.Load("EnvironmentSettings/EnvironmentSettings.active") ──▶ dev URL
```

## 구성 요소

### 1. `Assets/Scripts/EnvironmentSettings.cs` — 런타임 해석

`GetSelectedEnvironment()`(이름 반환)를 없애고, **자산을 직접 해석**하는 형태로 바꾼다.
에디터는 지금처럼 이름으로 찾고, 플레이어는 구워진 `.active`를 찾는다.

```csharp
public const string ActiveAssetName = "active";
public const string ResourceDirectory = "EnvironmentSettings";
public const string EditorDefaultEnvironment = "local-k8s";
public const string EditorPrefsKey = "LOP.Environment";

private static EnvironmentSettings Resolve()
{
#if UNITY_EDITOR
    var name = UnityEditor.EditorPrefs.GetString(EditorPrefsKey, EditorDefaultEnvironment);
#else
    // 빌드 때 구워진 것. 이름 대조 없음.
    var name = ActiveAssetName;
#endif
    var loaded = Resources.Load<EnvironmentSettings>($"{ResourceDirectory}/EnvironmentSettings.{name}");
    if (loaded == null)
    {
        // 조용히 틀린 서버에 붙느니 여기서 죽는다.
        throw new InvalidOperationException(
            $"환경 설정을 찾을 수 없다: {ResourceDirectory}/EnvironmentSettings.{name}");
    }
    Debug.Log($"[LOP] environment={name} lobby={loaded.lobbyBaseURL}");
    return loaded;
}
```

- `DefaultEnvironment` → **`EditorDefaultEnvironment`** 리네임. 이제 에디터에서만 쓰인다.
- 로그 한 줄을 남긴다 — 개발 빌드라 `adb logcat`으로 어느 환경인지 확인할 수 있다.
- `active` 프로퍼티와 `Reload()`의 외부 계약은 그대로다.

### 2. `Assets/Scripts/Editor/EnvironmentSwitcher.cs`

`DefaultEnvironment` → `EditorDefaultEnvironment` 참조 갱신. 동작 변화 없음.

### 3. 굽기의 소유자 — `EnvironmentBaker` + 훅

굽기·치우기·CLI 인자 파싱은 **`Assets/Editor/EnvironmentBaker.cs` 한 곳이 소유**하고, 두 진입점이
그것을 부른다.

| 진입점 | 언제 굽나 | 왜 |
|---|---|---|
| **`BuildScript` (CLI/CI)** | `BuildPipeline.BuildPlayer` **호출 전** | 빌드가 시작하기 전이라 포함 여부에 의문이 없다 |
| **`EnvironmentBuildProcessor` (훅)** | `OnPreprocessBuild`에서, **아직 안 구워져 있을 때만** | 에디터 GUI 빌드 보조 |

```
IPreprocessBuildWithReport.OnPreprocessBuild
    이미 .active가 있으면 (= CLI 빌드가 구워 둠) 아무것도 안 함
    없으면 EditorPrefs 값으로 굽는다 → 실패 시 BuildFailedException

IPostprocessBuildWithReport.OnPostprocessBuild
    이 훅이 구운 경우에만 .active 삭제
```

`callbackOrder = 0`. CLI 인자는 `System.Environment.GetCommandLineArgs()`로 읽는다.

> **왜 CI 경로를 훅에 맡기지 않는가**: 유니티 문서·커뮤니티가 `OnPreprocessBuild`에서 만든
> Resources 자산이 **확실히 빌드에 포함된다고 보장하지 않는다**("timing may be tight — 빌드가
> 시작하기 전에 만들고 refresh 하는 편이 확실하다"). 여러 SDK가 실제로 그렇게 쓰고 있어 아마
> 동작하겠지만, **CI가 뽑는 산출물을 '아마'에 걸지 않는다.** 그래서 CI 경로는 `BuildPlayer` 호출
> 전으로 확정하고, 훅은 GUI 빌드 보조로만 둔다.
>
> **그래도 훅을 두는 이유**: 없으면 유니티 Build Settings 창으로 빌드한 사람이 `.active` 없는
> APK를 얻고 실행 즉시 예외를 본다. 훅이 EditorPrefs로 대신 구우면 그 경로도 맞는다. 이 경로의
> 신뢰성은 구현 후 GUI 빌드 1회로 실증한다(검증 1번).

### 4. `Assets/Editor/BuildScript.cs`

- **CLI 경로에서 `-buildEnv` 필수.** 없으면 오류 로그 + `EditorApplication.Exit(2)`.
  CI가 빼먹을 수 없게 하는 방어선이다(훅의 EditorPrefs 대체는 GUI 빌드용이지, CI가 조용히
  로컬 환경으로 나가는 것을 허용하려는 것이 아니다).
- `EnvironmentBaker.Bake(env)`를 **`BuildPipeline.BuildPlayer` 호출 전에** 부른다.
- `-development`가 있으면 `BuildPlayerOptions.options |= BuildOptions.Development`.
- `try/finally`로 `.active`를 정리한다 — **빌드가 실패하면 후처리 훅이 돌지 않기 때문.**
- ⚠️ **`EditorApplication.Exit`을 `try` 안에서 부르지 않는다.** 프로세스가 즉시 끝나 `finally`가
  돌지 않고 `.active`가 남는다. 종료 코드를 담아 두고 `finally` 뒤에 한 번만 부른다.
- 콘텐츠 빌드 메서드(`BuildAndroidContentFull` / `BuildAndroidContentUpdate`)는 **손대지 않는다.**
  Addressables 빌드는 플레이어 빌드 콜백을 타지 않고 `-buildEnv`도 필요 없다.

### 5. `.gitignore`

```
# 빌드 때 생성되는 활성 환경 자산 (EnvironmentBuildProcessor가 굽고 지운다)
/Assets/Resources/EnvironmentSettings/EnvironmentSettings.active.asset
/Assets/Resources/EnvironmentSettings/EnvironmentSettings.active.asset.meta
```

> ⚠️ main 체크아웃의 `.gitignore`에 **커밋하지 않은 로컬 한 줄**(`.claude/worktrees/`)이 있다.
> 이 브랜치를 머지할 때 그 줄이 덮이지 않게 주의한다.

### 6. `.github/workflows/client-app-deploy.yml`

```yaml
on:
  workflow_dispatch:
    inputs:
      environment:
        description: 대상 백엔드 환경
        type: choice
        options: [dev, local-k8s, local]
        default: dev
      development:
        description: 개발 빌드 (평문 http 허용 · 로그/프로파일러 유지)
        type: boolean
        default: true
```

- Unity 호출에 `-buildEnv ${{ inputs.environment }}` 추가, `development`가 참이면 `-development`.
- S3 경로에 환경을 넣는다: `s3://lop-client/builds/<env>/<sha>/` , `builds/<env>/latest.json`.
- 마지막에 QR + 링크를 잡 요약에 쓴다(아래).

> **경로에 환경을 넣는 이유**: 지금은 `builds/latest.json` 하나뿐이라, dev 아닌 APK를 한 번만
> 뽑아도 **콘텐츠 파이프라인의 baseline이 조용히 덮인다.** 환경을 고를 수 있게 만드는 순간
> 생기는 구멍이므로 같이 막는다.

### 7. `.github/workflows/content-deploy.yml`

baseline `content_state.bin`을 `s3://lop-client/builds/dev/latest.json`에서 읽도록 경로만 갱신한다.
콘텐츠 빌드는 dev 버킷 하나만 쓰므로 입력을 추가하지 않는다(아래 "범위 밖" 참조).

### 8. QR 코드 게시

APK presigned URL을 QR PNG로 만들어 S3에 올리고, 잡 요약에 이미지와 링크를 함께 쓴다.

```
qrencode -o qr.png -s 8 "<apk presigned url>"
aws s3 cp qr.png "s3://lop-client/builds/<env>/<sha>/qr.png"
QR_URL=$(aws s3 presign ".../qr.png" --expires-in 604800)

cat >> "$GITHUB_STEP_SUMMARY" <<EOF
## <env> 빌드 \`<sha>\`
![QR]($QR_URL)

[APK 직접 다운로드](<apk presigned url>)
EOF
```

- **`data:` URI는 쓸 수 없다** — GitHub 마크다운 소독 단계가 `img src`의 data URI를 걷어낸다.
  외부 URL 이미지만 렌더링되므로 PNG를 S3에 올린다.
- **외부 QR 생성 서비스는 쓰지 않는다** — presigned URL을 제3자에게 넘기는 셈이 된다.
- 이미지가 안 뜨더라도 **바로 아래 링크로 받을 수 있게** 둘 다 쓴다.
- presigned URL이 300자를 넘어 QR이 촘촘하다(대략 버전 15, 77×77 모듈). `-s 8`이면 약 700px라
  모니터에서 찍는 데 문제없다. 더 작게 하려면 짧은 리다이렉트 주소를 두어야 하는데 지금은 과하다.
- **러너 준비물**: 맥 러너에 `brew install qrencode` 1회. 없으면 그 스텝이 실패하므로
  조용히 넘어가지 않는다.

## 실패 처리

| 상황 | 결과 |
|---|---|
| CI가 `-buildEnv` 누락 | `BuildScript`가 오류 로그 + `Exit(2)` — 빌드 실패 |
| 없는 환경 이름 지정 | 전처리 훅이 `BuildFailedException` — 빌드 중단 |
| 폰에서 `.active` 로드 실패 | 첫 접근에 `InvalidOperationException` — 틀린 서버로 붙지 않는다 |
| 빌드 실패로 `.active` 잔존 | `BuildScript`의 `finally`가 정리. 놓쳐도 git 미추적이고 다음 빌드가 덮어쓴다 |
| 러너에 `qrencode` 없음 | 해당 스텝 실패 — APK는 이미 업로드된 뒤라 링크는 로그에 남는다 |

## 검증

클라 코드가 전부 `Assembly-CSharp`라 EditMode 테스트를 붙일 수 없다(기존 제약,
`docs/ROADMAP.md` 및 프로젝트 메모리에 기록됨). 실증으로 대신한다.

1. **에디터 GUI 빌드 1회** — `.active`가 생겼다 사라지는지, 콘솔에 `environment=<선택값>`이
   찍히는지. EditorPrefs를 `dev`로 두고 확인.
2. **음수 케이스** — CLI에서 `-buildEnv`를 빼고 실행 → 빌드가 **실패하는지**.
3. **없는 환경** — `-buildEnv nonexistent` → `BuildFailedException`으로 중단되는지.
4. **CI 1회 실행** — 잡 요약에 QR이 뜨는지, 스캔해서 APK가 받아지는지, 설치 후
   `adb logcat`에 `environment=dev`가 찍히는지.

> 4번에서 **게임 진입까지는 확인 못 할 수 있다.** dev 백엔드가 인증 cutover 1b/1c 이전 버전이라
> 클라와 계약이 어긋난다(`POST /lobby/auth/introspect` → 404 확인됨). 로그인·로비까지만 되어도
> "dev를 보고 있다"는 증명으로 충분하며, 그 이상은 dev 백엔드 최신화 뒤로 미룬다.

## 범위 밖 (의도적)

- **Addressables 프로파일은 `dev` 고정 유지.** 콘텐츠 버킷(`s3://lop-assets/dev/[BuildTarget]`)은
  백엔드 환경과 **별개 축**이고, 지금 쓰는 콘텐츠 버킷이 하나뿐이다. `-buildEnv`는 백엔드 URL만
  정한다. 콘텐츠 버킷을 환경별로 가를 필요가 생기면 그때 두 번째 인자로 붙인다.
- **환경별 번들 ID·앱 이름**(`LOP (Dev)` 등) — 여러 환경 빌드가 한 폰에 공존해야 할 때. 모바일
  업계 표준 관행이지만 지금은 dev 하나만 뽑는다.
- **나머지 환경 자산을 `Resources/` 밖으로** — 선택한 환경만 APK에 싣기. 현재 URL이 전부 내부망
  주소라 값이 낮다.
- **DebugHud에 환경 표시** — UXML까지 건드려야 한다. 지금은 시작 로그로 갈음.
- **iOS 빌드** — 러너가 이미 맥이라 전제조건은 충족돼 있으나, Apple Developer 가입·인증서·
  프로비저닝·`xcodebuild` 스텝이 필요하다. 이 설계의 `-buildEnv` 배관은 플랫폼과 무관해 그대로
  재사용된다. 단 **배포 방식이 달라 QR 스텝은 재검토가 필요하다** — TestFlight는 앱으로 설치하고,
  애드혹 `.ipa`를 링크로 뿌리려면 `itms-services://` 매니페스트가 따로 필요하다.
- **dev 백엔드 최신화 / dev HTTPS 전환** — 백엔드·인프라 작업이며 다른 기계에서 진행 중인
  인증 트랙과 겹친다.

## 참고

- [Unity Manual — Introduction to customizing the build pipeline](https://docs.unity3d.com/Manual/BuildPlayerPipeline.html)
- [Unity Manual — Custom scripting symbols](https://docs.unity3d.com/6000.0/Documentation/Manual/custom-scripting-symbols.html)
- [Unity ScriptReference — PlayerSettings.SetPreloadedAssets](https://docs.unity3d.com/ScriptReference/PlayerSettings.SetPreloadedAssets.html)
- [Hextant Studios — Custom Runtime and Editor Settings in Unity](https://hextantstudios.com/unity-custom-settings/)
- [Fix: Unity ScriptableObject Data Resets to Default Values in Build](https://bugnet.io/blog/fix-unity-scriptableobject-reset-on-build)
- [Stop Uninstalling Your App to Test Staging and Production](https://dev.to/jocanola/stop-uninstalling-your-app-to-test-staging-and-production-a-proper-multi-environment-setup-for-50j0)
