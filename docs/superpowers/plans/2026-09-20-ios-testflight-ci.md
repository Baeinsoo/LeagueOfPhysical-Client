# iOS TestFlight CI 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 워크플로를 한 번 돌리면 iOS 빌드가 서명돼 TestFlight에 올라가고, 테스터 폰에 설치된다.

**Architecture:** 유니티는 배치모드로 (1) 어드레서블 콘텐츠와 (2) Xcode 프로젝트만 만든다. 서명·아카이브·업로드는 fastlane이 맡는다(`match`/`gym`/`pilot`). 워크플로 YAML은 얇게 두고 iOS 고유 복잡성은 `fastlane/Fastfile`에 둔다 — GameCI 패턴.

**Tech Stack:** Unity 6000.3.16f1 (iOS 모듈), Xcode 26.6, fastlane, GitHub Actions self-hosted 맥 러너, S3

**Spec:** `docs/superpowers/specs/2026-09-20-ios-testflight-ci-design.md`

## 진행 상태 (2026-09-24)

아래 체크박스는 갱신하지 않았다 — 실제 진행은 이 표가 기준이다.

| | |
|---|---|
| Task 0~4 | **끝남.** 손으로 TestFlight 빌드 1·2·3까지 올렸고 폰에서 로그인·매칭·게임 진입·플레이까지 확인했다. |
| Task 5 | **끝남.** `client-app-deploy-ios.yml` 한 번 실행으로 TestFlight 빌드 6까지 올라갔다(8분). |
| Task 6 | 남음 — TestFlight 배포가 시작됐으므로 **이제 필요하다.** 지금의 full 빌드는 이미 설치된 앱과 어긋날 수 있다. |
| Task 7 | 남음 — 외부 테스터 그룹·공개 링크는 사람이 App Store Connect에서 만들어야 한다. |

계획서를 쓴 뒤에 알게 되어 Task 5 본문과 **달라진 것 다섯**:

1. 형제 레포 체크아웃은 `git reset --hard @{u}` 가 아니다 — 러너 폴더가 upstream 없는 브랜치에
   올라가 있어 죽는다. `fetch origin main` + `checkout -B ci-build FETCH_HEAD` 로 간다.
2. Xcode 빌드 성공은 폴더 존재로 판정하면 안 된다 — 실패한 빌드가 지난번 폴더를 남겨 통과한다.
   `rm -rf Build/iOS` 후 로그의 `Xcode 프로젝트 OK` 로 판정한다.
3. **altool은 업로드를 마치고도 안 끝난다**(실측 두 번, 한 번은 3시간 44분). 종료 코드로 성공을
   판정하지 않고, 시간을 끊은 뒤 TestFlight 빌드 번호가 올라갔는지를 애플에 물어 판정한다
   (`fastlane ios latest_build_number_to_file`).
4. **서명용 키체인을 직접 만든다.** `setup_ci`가 만드는 키체인은 암호가 빈 문자열이라 codesign
   접근 허락(partition list)이 설정되지 않고, 그러면 macOS가 사람에게 창을 띄워 CI가 영영 멈춘다.
   그 창은 빈 암호를 받지도 않아 사람이 있어도 못 뚫는다. 랜덤 암호로 직접 만들되 `default_keychain:
   true`는 지킨다 — 한 번 false로 해 봤더니 codesign이 `errSecInternalComponent`로 죽었다.
   그리고 워크플로 마지막에 `if: always()` 스텝을 둬서, 프로세스가 죽어 정리가 못 돌아도
   기본 키체인과 검색 목록을 로그인 키체인으로 되돌린다(이 맥은 사람도 쓰는 기계다).
5. **업로드가 끝난 순간 끊는다.** altool이 매번 안 끝나므로 타임아웃을 다 기다리면 배포마다 90분이
   든다. fastlane을 뒤에서 돌리며 1분마다 애플에 빌드 번호를 묻고, 올라가면 프로세스 그룹째
   끊는다(자식까지 끊지 않으면 altool이 며칠씩 남는다). 93분 → 8분.

## Global Constraints

- 작업 위치는 **`/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client-iOS`**(iOS 전용 클론), 브랜치 `feature/ios-build`. 기존 프로젝트(`LeagueOfPhysical-Client`)는 건드리지 않는다.
- **유니티는 항상 배치모드로 실행한다.** 에디터 GUI로 어드레서블을 구우면 대화상자가 메인 스레드를 잡고 서서, 겉으로는 오래 걸리는 빌드와 구별되지 않는다.
- 유니티 실행 시 **`DEVELOPER_DIR=/Applications/Xcode-26.6.0.app/Contents/Developer`** 를 준다. 이 맥의 `xcode-select`는 CommandLineTools를 가리키고 있고, 전역 변경은 sudo가 필요해 하지 않는다.
- 번들 ID는 **`com.BAEGames.LeagueOfPhysicalClient`**, 배포 타깃 **iOS 15.0**.
- 커밋은 **바꾼 파일만 경로로 지정**한다. `git add -A` / `git commit -a` 금지 — 이 레포에는 늘 커밋하지 않는 로컬 재직렬화 noise(URP 품질 에셋, 볼륨 프로파일, 폰트, PackageManagerSettings)가 쌓여 있다.
- `.cs` 이름을 바꿀 때는 **`.meta`와 함께 `git mv`** 한다. GUID가 보존돼야 참조가 안 끊긴다.
- 서명 인증서는 **`s3://lop-client/certificates/`**(비공개). 공개 읽기 버킷 `lop-assets`에 두지 않는다.
- `AWS_ACCESS_KEY_ID`는 secret이 아니라 **`vars`** 다. secret으로 옮기면 프리사인 URL이 가려져 링크가 깨진다.

---

## Task 0: 사람 몫 선결 조건 (블로킹 게이트)

**이것은 코드 작업이 아니다.** Task 3부터는 아래가 없으면 한 줄도 돌지 않는다. Task 1·2는 이것 없이 진행할 수 있으므로 **먼저 하고**, 그동안 가입 승인을 기다린다.

- [ ] **Apple Developer Program 가입** — Individual 권장(24~48시간). Organization은 D-U-N-S 확인으로 2~4주
- [ ] **App Store Connect 앱 레코드** — Identifiers에 `com.BAEGames.LeagueOfPhysicalClient` 등록 후 앱 생성
- [ ] **App Store Connect API 키 발급** (.p8, 역할 App Manager 이상) — Key ID / Issuer ID / .p8 파일
- [ ] **앱 아이콘 1024×1024** — 현재 iOS 아이콘 19슬롯이 전부 비어 있고, 없으면 업로드가 실패한다. 디자이너 작업물
- [ ] **외부 테스터 그룹 + 공개 링크** 생성
- [ ] **팀 ID 확인** — Apple Developer 계정 Membership 화면의 Team ID

**확인 방법:** App Store Connect에 앱이 보이고, .p8 파일을 내려받았고, Team ID를 안다.

---

## Task 1: Info.plist 후처리 — 이름 정정 + 암호화 수출 항목

지금 `IOSAppTransportSecurity`는 평문 http 허용만 한다. TestFlight 업로드에 필요한 암호화 수출 항목(`ITSAppUsesNonExemptEncryption`)이 없으면 **업로드할 때마다 사람이 웹에서 같은 질문에 답해야 한다.** 역할이 둘이 되므로 이름을 "iOS 빌드 후처리"로 맞춘다.

애플 계정 없이 할 수 있다.

**Files:**
- Rename: `Assets/Editor/IOSAppTransportSecurity.cs` → `Assets/Editor/IOSBuildPostProcess.cs` (`.meta` 동반)
- Test: `Assets/Tests/Editor/IOSBuildPostProcessTests.cs` (신규)
- Modify: `.gitignore`

**Interfaces:**
- Produces: `LOP.EditorTools.IOSBuildPostProcess.Apply(string plistPath, bool development)` — 빌드 리포트 없이 plist 하나만 놓고 부를 수 있는 static 메서드. 테스트와 빌드 훅이 같은 자리를 쓴다.

- [ ] **Step 1: 파일 이름을 GUID 보존하며 바꾼다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client-iOS
git mv Assets/Editor/IOSAppTransportSecurity.cs Assets/Editor/IOSBuildPostProcess.cs
git mv Assets/Editor/IOSAppTransportSecurity.cs.meta Assets/Editor/IOSBuildPostProcess.cs.meta
```

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`Assets/Tests/Editor/IOSBuildPostProcessTests.cs`:

```csharp
#if UNITY_IOS
using System.IO;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEditor.iOS.Xcode;

public class IOSBuildPostProcessTests
{
    private string plistPath;

    [SetUp]
    public void SetUp()
    {
        plistPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".plist");
        var plist = new PlistDocument();
        plist.root.SetString("CFBundleName", "test");
        plist.WriteToFile(plistPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(plistPath))
        {
            File.Delete(plistPath);
        }
    }

    private PlistDocument Read()
    {
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        return plist;
    }

    [Test]
    public void 개발빌드는_평문http를_허용한다()
    {
        IOSBuildPostProcess.Apply(plistPath, development: true);

        var ats = Read().root["NSAppTransportSecurity"].AsDict();
        Assert.IsTrue(ats["NSAllowsArbitraryLoads"].AsBoolean());
    }

    [Test]
    public void 개발빌드가_아니면_평문http를_열지_않는다()
    {
        IOSBuildPostProcess.Apply(plistPath, development: false);

        Assert.IsFalse(Read().root.values.ContainsKey("NSAppTransportSecurity"));
    }

    [Test]
    public void 암호화_수출_항목은_개발빌드가_아니어도_박힌다()
    {
        IOSBuildPostProcess.Apply(plistPath, development: false);

        Assert.IsFalse(Read().root["ITSAppUsesNonExemptEncryption"].AsBoolean());
    }
}
#endif
```

- [ ] **Step 3: 테스트가 실패하는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client-iOS
export DEVELOPER_DIR=/Applications/Xcode-26.6.0.app/Contents/Developer
UNITY=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -runTests -projectPath . -buildTarget iOS \
  -testPlatform EditMode -testFilter "IOSBuildPostProcessTests" \
  -testResults Logs/test-results.xml -logFile Logs/tests.log
grep -E "error CS" Logs/tests.log | head -3
```

기대: 실패. `IOSBuildPostProcess` 타입이 없다는 컴파일 에러(`error CS0103`).

> **배치모드로 돌린다.** GUI 에디터를 띄우면 대화상자가 끼어들 수 있고, 에디터가 둘일 때
> `unity` CLI에 등록되지 않는 경우도 있다. 배치모드는 프로젝트를 잠그므로 에디터를 먼저 닫는다.
> `-buildTarget iOS`여야 `#if UNITY_IOS`가 걸려 테스트가 컴파일된다.

- [ ] **Step 4: 구현한다**

`Assets/Editor/IOSBuildPostProcess.cs` 전체를 아래로 바꾼다:

```csharp
#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 유니티가 내놓은 Xcode 프로젝트의 Info.plist를 손본다. 두 가지를 한다.
    /// <para>1. 평문 http 허용 — iOS는 https가 아닌 요청을 운영체제 차원에서 막는다(ATS).
    /// dev 백엔드 주소가 평문이라 그대로 두면 로그인부터 실패한다. 유니티 쪽 차단이 개발
    /// 빌드에서만 평문을 허용하므로, 여는 기준을 거기에 맞춘다.</para>
    /// <para>2. 암호화 수출 규정 — 이 항목이 없으면 TestFlight에 올릴 때마다 사람이 웹에서
    /// 같은 질문에 답해야 한다.</para>
    /// </summary>
    public class IOSBuildPostProcess : IPostprocessBuildWithReport
    {
        //  환경 자산 굽기(EnvironmentBuildProcessor, 0) 뒤에 돈다.
        public int callbackOrder => 1;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            var plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning($"[LOP] Info.plist를 찾지 못해 후처리를 건너뛴다: {plistPath}");
                return;
            }

            var development = (report.summary.options & BuildOptions.Development) != 0;
            Apply(plistPath, development);
            Debug.Log($"[LOP] iOS Info.plist 후처리 완료 (개발 빌드={development})");
        }

        /// <summary>
        /// plist 하나만 놓고 부를 수 있게 갈라 둔 자리. 빌드를 돌리지 않고 테스트한다.
        /// </summary>
        public static void Apply(string plistPath, bool development)
        {
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            //  우리 앱은 표준 https 말고 따로 암호화를 쓰지 않는다.
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

            if (development)
            {
                //  주소가 환경마다 달라(dev는 고정 IP, local은 사설망) 도메인별 예외를 적을 수
                //  없다. 개발 빌드 전용이므로 전체 허용으로 둔다.
                var security = plist.root.CreateDict("NSAppTransportSecurity");
                security.SetBoolean("NSAllowsArbitraryLoads", true);
            }

            plist.WriteToFile(plistPath);
        }
    }
}
#endif
```

- [ ] **Step 5: 테스트가 통과하는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client-iOS
export DEVELOPER_DIR=/Applications/Xcode-26.6.0.app/Contents/Developer
UNITY=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -runTests -projectPath . -buildTarget iOS \
  -testPlatform EditMode -testFilter "IOSBuildPostProcessTests" \
  -testResults Logs/test-results.xml -logFile Logs/tests.log
python3 -c "
import xml.etree.ElementTree as ET
r = ET.parse('Logs/test-results.xml').getroot()
print('total=%s passed=%s failed=%s' % (r.get('total'), r.get('passed'), r.get('failed')))
"
```

기대: `total=3 passed=3 failed=0`.

- [ ] **Step 6: 테스트가 진짜 뭔가를 지키는지 확인한다**

`Apply`에서 `plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);` 한 줄을 잠시 지우고 Step 5를 다시 돌린다. **`암호화_수출_항목은_...` 하나가 실패해야 한다.** 실패하지 않으면 테스트가 아무것도 안 지키고 있는 것이니 테스트를 고친다. 확인했으면 지운 줄을 되돌린다.

- [ ] **Step 7: 실제 빌드에 반영되는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client-iOS
export DEVELOPER_DIR=/Applications/Xcode-26.6.0.app/Contents/Developer
UNITY=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -quit -nographics -buildTarget iOS -projectPath . \
  -executeMethod BuildScript.BuildIOSXcodeProject -buildEnv dev -development \
  -logFile - > Logs/ios-xcode-build.log 2>&1
/usr/libexec/PlistBuddy -c "Print :ITSAppUsesNonExemptEncryption" Build/iOS/Info.plist
/usr/libexec/PlistBuddy -c "Print :NSAppTransportSecurity:NSAllowsArbitraryLoads" Build/iOS/Info.plist
```

기대: 각각 `false`, `true`.

> 에디터가 이 프로젝트를 열고 있으면 배치모드가 프로젝트 잠금에 막힌다. 먼저 에디터를 닫는다.

- [ ] **Step 8: 콘텐츠 빌드가 남기는 부산물을 무시 목록에 넣는다**

`.gitignore`의 `ServerData/` 줄 아래에 추가:

```
# 어드레서블이 콘텐츠 빌드마다 새로 만든다 — 산출물이라 커밋하지 않는다.
/[Aa]ssets/[Aa]ddressable[Aa]ssets[Dd]ata/link.xml
```

- [ ] **Step 9: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client-iOS
git add Assets/Editor/IOSBuildPostProcess.cs Assets/Editor/IOSBuildPostProcess.cs.meta \
        Assets/Tests/Editor/IOSBuildPostProcessTests.cs \
        Assets/Tests/Editor/IOSBuildPostProcessTests.cs.meta .gitignore
git status --short
git commit -m "feat(ios): 빌드 후처리에 암호화 수출 항목 추가

TestFlight는 업로드마다 암호화 수출 여부를 묻는다. Info.plist에 박아두지
않으면 사람이 매번 웹에서 같은 답을 해야 한다.

역할이 평문 http 허용 하나에서 둘로 늘어 이름을 IOSBuildPostProcess로 맞췄다.
빌드를 돌리지 않고 검증할 수 있게 plist만 받는 Apply를 갈라 두고 테스트를 붙였다."
```

> `.meta`는 유니티가 만든 것만 커밋한다. Step 2 이후 에디터가 떠 있으면 자동 생성되고, 없으면 `unity command eval --code 'UnityEditor.AssetDatabase.Refresh();'`로 만든다.

---

## Task 2: Ruby와 fastlane 토대

fastlane을 깐다. 애플 계정 없이 할 수 있다.

**이 맥의 기본 Ruby는 2.6.10**(macOS 기본)인데 fastlane은 이를 권장하지 않고 요구 버전이 3.3+로 올라가는 중이다. Homebrew Ruby를 따로 깐다.

**Files:**
- Create: `Gemfile`
- Create: `Gemfile.lock` (`bundle install`이 만든다)
- Modify: `.gitignore`

**Interfaces:**
- Produces: `bundle exec fastlane` 이 도는 상태. 이후 모든 fastlane 태스크가 이 위에서 돈다.

- [ ] **Step 1: Homebrew Ruby 설치**

```bash
brew install ruby
/opt/homebrew/opt/ruby/bin/ruby -v
```

기대: 3.3 이상 출력(2026-09-20 실측 4.0.7). fastlane 2.240.1이 그 위에서 돌았다.

- [ ] **Step 2: Gemfile 작성**

`Gemfile`:

```ruby
source "https://rubygems.org"

# iOS 서명·빌드·TestFlight 업로드. 유니티 CI 표준(GameCI)이 지정하는 도구다.
gem "fastlane"
```

- [ ] **Step 3: 설치하고 도는지 확인**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client-iOS
export PATH="/opt/homebrew/opt/ruby/bin:$PATH"
gem install bundler --no-document
bundle config set --local path vendor/bundle
bundle install
bundle exec fastlane --version
```

기대: fastlane 버전이 찍힌다.

- [ ] **Step 4: 설치 산출물을 무시 목록에 넣는다**

`.gitignore` 끝에 추가:

```
# fastlane
/vendor/bundle/
/.bundle/
/fastlane/report.xml
/fastlane/Preview.html
/fastlane/test_output/
```

- [ ] **Step 5: 커밋**

```bash
git add Gemfile Gemfile.lock .gitignore
git status --short
git commit -m "build(ios): fastlane 토대

유니티 CI 표준인 GameCI가 iOS 배포에 fastlane을 지정한다. 손으로 쓴
xcodebuild/security 조합은 한 줄 바꾸면 서명이 조용히 깨진다.

맥 기본 Ruby 2.6은 fastlane이 권장하지 않아 Homebrew Ruby를 쓴다."
```

---

## Task 3: fastlane 설정 — Appfile / Matchfile / Fastfile

**Task 0 완료 후에만 진행할 수 있다** (팀 ID, 앱 레코드, API 키 필요).

**Files:**
- Create: `fastlane/Appfile`
- Create: `fastlane/Matchfile`
- Create: `fastlane/Fastfile`

**Interfaces:**
- Consumes: Task 2의 `bundle exec fastlane`
- Produces: `bundle exec fastlane ios beta` 레인. Task 5의 워크플로가 이 한 줄을 부른다.
- 환경변수로 받는 값: `ASC_KEY_ID`, `ASC_ISSUER_ID`, `ASC_KEY_P8`(base64), `APPLE_TEAM_ID`, `MATCH_S3_BUCKET`, `MATCH_PASSWORD`

- [ ] **Step 1: Appfile**

`fastlane/Appfile`:

```ruby
app_identifier("com.BAEGames.LeagueOfPhysicalClient")
team_id(ENV.fetch("APPLE_TEAM_ID"))
```

- [ ] **Step 2: Matchfile**

`fastlane/Matchfile`:

```ruby
# 인증서는 비공개 버킷에 둔다. lop-assets는 앱이 자격증명 없이 읽어가는 공개 버킷이라
# 거기 두면 링크를 아는 누구나 받아간다.
storage_mode("s3")
s3_bucket(ENV.fetch("MATCH_S3_BUCKET"))
s3_region("ap-northeast-2")
s3_prefix("certificates")

type("appstore")
app_identifier(["com.BAEGames.LeagueOfPhysicalClient"])
```

- [ ] **Step 3: Fastfile**

`fastlane/Fastfile`:

```ruby
default_platform(:ios)

APP_IDENTIFIER = "com.BAEGames.LeagueOfPhysicalClient"
XCODE_PROJECT  = "Build/iOS/Unity-iPhone.xcodeproj"
SCHEME         = "Unity-iPhone"

platform :ios do
  desc "유니티가 만들어 둔 Xcode 프로젝트를 서명해 TestFlight에 올린다"
  lane :beta do
    unless File.directory?(XCODE_PROJECT)
      UI.user_error!("#{XCODE_PROJECT} 가 없다. 유니티 배치 빌드(BuildScript.BuildIOSXcodeProject)를 먼저 돌려야 한다.")
    end

    #  CI에서만 임시 키체인을 만든다. 로컬에서는 아무 일도 하지 않는다.
    setup_ci

    api_key = app_store_connect_api_key(
      key_id: ENV.fetch("ASC_KEY_ID"),
      issuer_id: ENV.fetch("ASC_ISSUER_ID"),
      key_content: ENV.fetch("ASC_KEY_P8"),
      is_key_content_base64: true,
    )

    #  readonly가 핵심이다. CI가 인증서를 새로 만들 수 있으면 한도에 걸린 날 조용히
    #  기존 인증서를 무효화한다.
    match(
      type: "appstore",
      app_identifier: APP_IDENTIFIER,
      readonly: true,
      api_key: api_key,
    )

    #  유니티는 매번 Xcode 프로젝트를 새로 만들고 자동 서명으로 내놓는다. 매번 되돌린다.
    update_code_signing_settings(
      path: XCODE_PROJECT,
      use_automatic_signing: false,
      team_id: ENV.fetch("APPLE_TEAM_ID"),
      targets: [SCHEME],
      code_sign_identity: "Apple Distribution",
      profile_name: lane_context[SharedValues::MATCH_PROVISIONING_PROFILE_MAPPING][APP_IDENTIFIER],
    )

    #  빌드 번호는 애플이 들고 있는 값을 기준으로 올린다 — 러너나 레포 상태에 기대지 않아
    #  기계가 둘이어도 안 겹친다.
    increment_build_number(
      xcodeproj: XCODE_PROJECT,
      build_number: latest_testflight_build_number(
        api_key: api_key,
        app_identifier: APP_IDENTIFIER,
        initial_build_number: 0,
      ) + 1,
    )

    build_app(
      project: XCODE_PROJECT,
      scheme: SCHEME,
      export_method: "app-store",
      output_directory: "Build/ipa",
      output_name: "lop.ipa",
    )

    upload_to_testflight(
      api_key: api_key,
      #  처리 대기로 러너를 붙잡아 두지 않는다. 내부 테스터는 처리 끝나면 자동으로 받는다.
      skip_waiting_for_build_processing: true,
      #  외부 그룹 배포는 App Store Connect 쪽에서 관리한다(버전당 첫 빌드는 심사).
      distribute_external: false,
    )
  end
end
```

- [ ] **Step 4: 문법이 깨지지 않았는지 확인**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client-iOS
export PATH="/opt/homebrew/opt/ruby/bin:$PATH"
bundle exec fastlane lanes
```

기대: `ios beta` 레인이 목록에 보인다. (아직 돌리지는 않는다 — Task 4에서.)

- [ ] **Step 5: 커밋**

```bash
git add fastlane/Appfile fastlane/Matchfile fastlane/Fastfile
git status --short
git commit -m "feat(ios): fastlane 레인 — 서명부터 TestFlight까지

유니티가 만든 Xcode 프로젝트를 받아 match 인증서로 서명하고 올린다.

match는 readonly로만 쓴다. CI가 인증서를 새로 만들 수 있으면 한도에 걸린 날
기존 인증서를 조용히 무효화한다.

빌드 번호는 애플이 들고 있는 값 +1로 정한다. 러너나 레포 상태에 기대면
기계가 둘인 이 프로젝트에서 언젠가 겹친다."
```

---

## Task 4: 로컬에서 TestFlight까지 한 번 성공시킨다

**CI로 옮기기 전에 로컬에서 먼저 통과시킨다.** 서명은 CI에서 처음 만나면 원인 찾기가 몹시 어렵다.

**Files:** 없음 (실행과 확인만)

**Interfaces:**
- Consumes: Task 1·2·3 전부, Task 0의 애플 자산

- [ ] **Step 1: match를 처음 한 번 초기화한다 (쓰기 모드, 로컬에서만)**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client-iOS
export PATH="/opt/homebrew/opt/ruby/bin:$PATH"
export APPLE_TEAM_ID=<팀 ID>
export MATCH_S3_BUCKET=lop-client
export MATCH_PASSWORD=<새로 정한 암호 — 이후 CI secret에 넣을 값>
export ASC_KEY_ID=<키 ID>
export ASC_ISSUER_ID=<발급자 ID>
export ASC_KEY_P8=$(base64 -i <다운로드한 .p8 경로>)
aws login   # S3에 쓰려면 세션이 살아 있어야 한다

bundle exec fastlane match appstore
```

기대: 배포 인증서와 프로비저닝 프로파일이 만들어지고 `s3://lop-client/certificates/` 에 올라간다.

확인:
```bash
aws s3 ls s3://lop-client/certificates/ --recursive | head
```

- [ ] **Step 2: 유니티에 팀 ID를 넣는다**

CI 경로에서는 fastlane이 Xcode 프로젝트에 팀을 넣어주지만, 유니티에도 넣어두면 **로컬에서 Xcode를 직접 열어 케이블로 꽂을 때** 팀을 매번 고르지 않아도 된다.

에디터가 떠 있는 상태에서:

```bash
export PATH="$HOME/.unity/bin:$PATH"
unity command eval --code 'UnityEditor.PlayerSettings.iOS.appleDeveloperTeamID = "<팀 ID>"; UnityEditor.EditorApplication.ExecuteMenuItem("File/Save Project"); return UnityEditor.PlayerSettings.iOS.appleDeveloperTeamID;' \
  --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client-iOS --no-banner
```

> 에디터가 켜져 있을 때 `ProjectSettings.asset`을 파일로 직접 고치면 에디터가 저장하면서 덮어쓴다. 반드시 에디터를 통해 넣는다.

커밋:

```bash
git add ProjectSettings/ProjectSettings.asset
git status --short
git commit -m "chore(ios): 서명 팀 ID"
```

- [ ] **Step 3: 아이콘이 들어갔는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client-iOS
python3 - <<'PY'
import re
t = open('ProjectSettings/ProjectSettings.asset', encoding='utf-8').read()
m = re.search(r'- m_BuildTarget: iPhone\n    m_Icons:\n(.*?)(?=\n  - m_BuildTarget: |\n  m_BuildTargetBatching)', t, re.S)
body = m.group(1)
print('빈 슬롯:', body.count('m_Textures: []'), '/', body.count('m_Textures'))
PY
```

기대: 빈 슬롯 0. **비어 있으면 업로드가 실패하니 여기서 멈추고 Task 0-4로 돌아간다.**

- [ ] **Step 4: 유니티 배치 빌드 두 번**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client-iOS
export DEVELOPER_DIR=/Applications/Xcode-26.6.0.app/Contents/Developer
UNITY=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity

"$UNITY" -batchmode -quit -nographics -buildTarget iOS -projectPath . \
  -executeMethod BuildScript.BuildContentFull -logFile - > Logs/ios-content.log 2>&1
grep "Addressables FULL build OK" Logs/ios-content.log

"$UNITY" -batchmode -quit -nographics -buildTarget iOS -projectPath . \
  -executeMethod BuildScript.BuildIOSXcodeProject -buildEnv dev -development \
  -logFile - > Logs/ios-xcode.log 2>&1
grep "Xcode 프로젝트 OK" Logs/ios-xcode.log
```

기대: 두 grep 모두 한 줄씩 찍힌다.

- [ ] **Step 5: 콘텐츠를 S3에 올린다 — CI로**

로컬에서 `aws s3 sync`로 밀어 넣지 않는다. 그러면 다음에도 사람이 해야 한다.
`content-deploy`에 iOS 타깃이 있으므로 그걸 돌린다(CI가 자격증명을 들고 있어 `aws login`도 필요 없다).

```bash
gh workflow run content-deploy.yml --ref feature/ios-build -f target=ios
gh run list --workflow=content-deploy.yml --limit 1
```

기대: 잡이 초록이고 `s3://lop-assets/dev/iOS/`에 카탈로그와 번들이 올라간다.

> 앞 Step 4에서 콘텐츠를 로컬로 굽는 것은 **Xcode 프로젝트 빌드에 필요해서**다(로컬 그룹 번들이
> 플레이어에 들어간다). S3로 나가는 것은 CI가 구운 쪽이다.

- [ ] **Step 6: fastlane으로 서명·업로드**

```bash
export PATH="/opt/homebrew/opt/ruby/bin:$PATH"
export DELIVER_ALTOOL_ADDITIONAL_UPLOAD_PARAMETERS="--use-old-altool"
bundle exec fastlane ios beta
```

기대: `Build/ipa/lop.ipa`가 만들어지고 업로드가 성공한다.

> **`--use-old-altool`이 핵심이다.** Xcode 26의 `altool`에 패키지를 못 여는 회귀가 보고돼 있고, 우리가 쓰는 26.6이 해당된다. 이게 없으면 업로드 단계에서 실패한다.

- [ ] **Step 7: 서명 주체를 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client-iOS
unzip -o -q Build/ipa/lop.ipa -d /tmp/lop-ipa
codesign -dvvv /tmp/lop-ipa/Payload/*.app 2>&1 | grep -E "Authority|TeamIdentifier"
```

기대: Authority에 `Apple Distribution`, TeamIdentifier가 본인 팀 ID.

- [ ] **Step 8: App Store Connect에서 빌드를 확인한다**

TestFlight 탭에 빌드가 뜨고 상태가 "처리 중" → "테스트 준비 완료"로 바뀐다. 처리에 몇 분 걸린다.

- [ ] **Step 9: 폰에 설치해서 끝까지 확인한다**

본인을 내부 테스터로 추가하고 TestFlight 앱에서 설치한다. **로그인하고 매칭을 걸어 게임에 들어가는 것까지** 확인한다 — 여기까지 가야 콘텐츠·서버 주소·평문 http가 전부 맞았다는 뜻이다.

> 여기서 앱이 뜨자마자 죽으면 1순위 용의자는 **AutoMapper**다. 이 프로젝트가 IL2CPP로 도는 게 iOS가 처음이고, AutoMapper 11은 IL2CPP에 없는 `Reflection.Emit`에 기댄다. Xcode의 기기 콘솔이나 TestFlight 크래시 로그에 예외가 찍힌다.

---

## Task 5: 워크플로 `client-app-deploy-ios.yml`

로컬에서 통과한 순서를 그대로 CI에 옮긴다.

**Files:**
- Create: `.github/workflows/client-app-deploy-ios.yml`

**Interfaces:**
- Consumes: Task 3의 `ios beta` 레인
- Produces: `s3://lop-client/builds/<env>/ios/<sha>/addressables_content_state.bin` 와 `builds/<env>/ios/latest.json` — Task 6의 증분 빌드가 읽는다

- [ ] **Step 1: GitHub에 비밀값과 변수를 넣는다**

레포 Settings → Secrets and variables → Actions:

| 이름 | 종류 | 값 |
|---|---|---|
| `ASC_KEY_ID` | secret | API 키 ID |
| `ASC_ISSUER_ID` | secret | 발급자 ID |
| `ASC_KEY_P8` | secret | `base64 -i AuthKey_XXXX.p8` 결과 |
| `MATCH_PASSWORD` | secret | Task 4 Step 1에서 정한 암호 |
| `APPLE_TEAM_ID` | **vars** | 팀 ID |
| `MATCH_S3_BUCKET` | **vars** | `lop-client` |

- [ ] **Step 2: 워크플로 파일 작성**

`.github/workflows/client-app-deploy-ios.yml`:

```yaml
name: client-app-deploy-ios
#
# iOS 앱을 구워 TestFlight에 올린다. 안드로이드(client-app-deploy)의 짝이지만 전달 방식이
# 달라 파일을 나눴다 — APK는 받으면 깔리지만 ipa는 TestFlight를 거쳐야 한다.
#
# 서명·아카이브·업로드는 fastlane이 맡는다(fastlane/Fastfile). 여기서는 유니티 배치 빌드와
# S3 업로드만 한다.
#
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

concurrency:
  group: client-app-deploy-ios
  cancel-in-progress: false

env:
  AWS_DEFAULT_REGION: ap-northeast-2
  #  이 맥의 xcode-select는 CommandLineTools를 가리킨다. 전역 변경은 sudo가 필요하므로
  #  프로세스 단위로 Xcode를 지정한다.
  DEVELOPER_DIR: /Applications/Xcode-26.6.0.app/Contents/Developer
  #  Xcode 26의 altool에 패키지를 못 여는 회귀가 있다. 레거시 경로를 강제한다.
  DELIVER_ALTOOL_ADDITIONAL_UPLOAD_PARAMETERS: "--use-old-altool"

jobs:
  build-deploy:
    runs-on: [self-hosted, client]
    steps:
      - uses: actions/checkout@v4
        with:
          submodules: recursive

      - name: sha 산출
        id: tag
        run: echo "sha=$(git rev-parse --short HEAD)" >> "$GITHUB_OUTPUT"

      - name: 의존 UPM 패키지 레포 체크아웃 (file:../../ 형제 위치)
        run: |
          set -e
          cd "$GITHUB_WORKSPACE/.."
          for r in GameFramework LeagueOfPhysical-Shared LeagueOfPhysical-MasterData-Client; do
            if [ -d "$r/.git" ]; then
              git -C "$r" fetch --depth 1 origin && git -C "$r" reset --hard @{u}
            else
              git clone --depth 1 "https://github.com/Baeinsoo/$r" "$r"
            fi
            echo "$r @ $(git -C "$r" rev-parse --short HEAD)"
          done

      - name: NuGet 패키지 복원 (NuGetForUnity CLI, packages.config 기반)
        run: |
          set -e
          dotnet tool restore
          dotnet tool run nugetforunity restore .
          test -f Assets/NuGetForUnity/Packages/R3.1.3.1/lib/netstandard2.1/R3.dll

      - name: 어드레서블 콘텐츠 full 빌드 (iOS)
        run: |
          set -eo pipefail
          UNITY="/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity"
          if ! "$UNITY" -batchmode -quit -nographics -buildTarget iOS -projectPath . \
                -executeMethod BuildScript.BuildContentFull -logFile - > unity-content.log 2>&1; then
            echo "::error::content build failed"; tail -80 unity-content.log; exit 1
          fi
          # 빌드 서포트 모듈이 빠지면 -buildTarget이 조용히 무시되고 엉뚱한 플랫폼 것이 구워진다.
          grep -q "content full build target: iOS" unity-content.log
          test -f Assets/AddressableAssetsData/iOS/addressables_content_state.bin
          ls ServerData/iOS

      - name: 콘텐츠 업로드 (additive, --delete 금지)
        env:
          AWS_ACCESS_KEY_ID: ${{ vars.AWS_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
        run: |
          set -e
          aws s3 sync ServerData/iOS "s3://lop-assets/dev/iOS"
          echo "콘텐츠 업로드 완료: s3://lop-assets/dev/iOS/"

      - name: Xcode 프로젝트 빌드
        env:
          BUILD_ENV: ${{ inputs.environment }}
          DEVELOPMENT: ${{ inputs.development }}
        run: |
          set -eo pipefail
          UNITY="/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity"
          DEV_FLAG=""
          if [ "$DEVELOPMENT" = "true" ]; then DEV_FLAG="-development"; fi
          if ! "$UNITY" -batchmode -quit -nographics -buildTarget iOS -projectPath . \
                -executeMethod BuildScript.BuildIOSXcodeProject \
                -buildEnv "$BUILD_ENV" $DEV_FLAG -logFile - > unity-xcode.log 2>&1; then
            echo "::error::Xcode project build failed"; tail -80 unity-xcode.log; exit 1
          fi
          test -d Build/iOS/Unity-iPhone.xcodeproj

      - name: 서명 + TestFlight 업로드 (fastlane)
        env:
          PATH: /opt/homebrew/opt/ruby/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin
          ASC_KEY_ID: ${{ secrets.ASC_KEY_ID }}
          ASC_ISSUER_ID: ${{ secrets.ASC_ISSUER_ID }}
          ASC_KEY_P8: ${{ secrets.ASC_KEY_P8 }}
          MATCH_PASSWORD: ${{ secrets.MATCH_PASSWORD }}
          APPLE_TEAM_ID: ${{ vars.APPLE_TEAM_ID }}
          MATCH_S3_BUCKET: ${{ vars.MATCH_S3_BUCKET }}
          AWS_ACCESS_KEY_ID: ${{ vars.AWS_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
        run: |
          set -eo pipefail
          bundle config set --local path vendor/bundle
          bundle install
          bundle exec fastlane ios beta

      - name: content_state 보존 + latest 갱신
        env:
          AWS_ACCESS_KEY_ID: ${{ vars.AWS_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          BUILD_ENV: ${{ inputs.environment }}
        run: |
          set -e
          SHA="${{ steps.tag.outputs.sha }}"
          #  안드로이드는 builds/<env>/latest.json 을 쓴다. 돌아가는 그 경로를 건드리지 않으려고
          #  iOS는 한 칸 아래에 따로 둔다.
          DEST="s3://lop-client/builds/$BUILD_ENV/ios/$SHA"
          aws s3 cp Assets/AddressableAssetsData/iOS/addressables_content_state.bin \
            "$DEST/addressables_content_state.bin"
          printf '{"sha":"%s","environment":"%s","content_state":"%s/addressables_content_state.bin"}\n' \
            "$SHA" "$BUILD_ENV" "$DEST" > latest.json
          aws s3 cp latest.json "s3://lop-client/builds/$BUILD_ENV/ios/latest.json"
          echo "보존: $DEST/"

      - name: 빌드 로그 업로드
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: ios-build-logs
          path: |
            unity-content.log
            unity-xcode.log
          if-no-files-found: warn
          retention-days: 14
```

- [ ] **Step 3: 브랜치를 올리고 워크플로를 돌린다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client-iOS
git add .github/workflows/client-app-deploy-ios.yml
git commit -m "ci(ios): TestFlight 배포 워크플로

유니티 배치로 콘텐츠와 Xcode 프로젝트만 만들고, 서명부터 업로드까지는
fastlane에 넘긴다. 안드로이드와 파일을 나눈 것은 전달 방식이 다르기 때문이다."
git push -u origin feature/ios-build
```

GitHub Actions에서 `client-app-deploy-ios`를 `feature/ios-build` 브랜치로 실행한다(environment=dev, development=true).

- [ ] **Step 4: 성공을 확인한다**

- 잡이 초록
- App Store Connect TestFlight에 새 빌드(빌드 번호가 지난 것보다 큼)
- `aws s3 ls s3://lop-client/builds/dev/ios/` 에 sha 폴더와 `latest.json`
- 내부 테스터 폰에 설치해서 게임 진입까지

---

## Task 6: 콘텐츠만 갱신하는 증분 잡

앱을 다시 깔지 않고 콘텐츠만 고칠 길을 연다. TestFlight로 깔린 앱은 폰에 남으므로, 이미 깔린 앱이 든 번들·카탈로그와 어긋나지 않게 **증분**으로 구워야 한다.

**Files:**
- Modify: `.github/workflows/content-deploy.yml`
- Modify: `Assets/Editor/BuildScript.cs`

**Interfaces:**
- Consumes: Task 5가 올린 `s3://lop-client/builds/<env>/ios/latest.json`
- Produces: `BuildScript.BuildIOSContentUpdate()` — 워크플로가 `-executeMethod`로 부른다

- [ ] **Step 1: 증분 빌드 진입점을 추가한다**

`Assets/Editor/BuildScript.cs`의 `BuildAndroidContentUpdate` 아래에 추가:

```csharp
    // ── 어드레서블: iOS 증분 빌드. CI가 S3 baseline을 아래 경로에 미리 배치해야 함.
    public static void BuildIOSContentUpdate()
    {
        var settings = EnsureSettings();
        var statePath = ContentUpdateScript.GetContentStateDataPath(false);
        if (!System.IO.File.Exists(statePath))
        {
            Debug.LogError($"content_state 없음: {statePath}. client-app-deploy-ios를 먼저 실행해 baseline을 만드세요.");
            EditorApplication.Exit(2);
            return;
        }
        Debug.Log($"content update baseline: {statePath}");
        var result = ContentUpdateScript.BuildContentUpdate(settings, statePath);
        FinishContent(result, "UPDATE");
    }
```

> `GetContentStateDataPath(false)`는 **활성 빌드 타깃**의 경로를 돌려준다. CI가 `-buildTarget iOS`로 부르므로 `Assets/AddressableAssetsData/iOS/`를 가리킨다. 안드로이드용 메서드와 본문이 같아 보이지만 부르는 타깃이 달라 결과가 다르다.

- [ ] **Step 2: `content-deploy.yml`의 target 선택지에 iOS를 넣는다**

`options: [gameserver, standalone-windows, standalone, client-app, all]` →
`options: [gameserver, standalone-windows, standalone, client-app, client-app-ios, all]`

- [ ] **Step 3: iOS 증분 잡을 추가한다**

`build-deploy-client-app` 잡 아래에 추가 (앞부분 네 스텝은 그 잡에서 그대로 복사):

```yaml
  # 클라 앱(iOS) 콘텐츠. 안드로이드와 같은 이유로 증분이다 — TestFlight로 깔린 앱이
  # 폰에 남아 있어서, full로 다시 구우면 그 앱이 든 번들·카탈로그와 어긋난다.
  #
  # `all`에는 일부러 넣지 않는다. 러너가 한 대라 유니티를 동시에 여러 개 돌릴 수 없는데,
  # `all`은 이미 client-app(안드로이드)과 standalone 잡을 함께 띄운다. 여기에 iOS까지
  # 얹으면 유니티 셋이 같은 러너를 두고 다툰다. iOS 콘텐츠는 따로 고른다.
  build-deploy-client-app-ios:
    if: inputs.target == 'client-app-ios'
    runs-on: [self-hosted, client]
    env:
      DEVELOPER_DIR: /Applications/Xcode-26.6.0.app/Contents/Developer
    steps:
      - uses: actions/checkout@v4
        with:
          submodules: recursive

      - name: 의존 UPM 패키지 레포 체크아웃
        env:
          PACKAGE_REF: ${{ inputs.package_ref }}
        run: |
          set -e
          cd "$GITHUB_WORKSPACE/.."
          for r in GameFramework LeagueOfPhysical-Shared LeagueOfPhysical-MasterData-Client; do
            if [ ! -d "$r/.git" ]; then
              git clone --depth 1 "https://github.com/Baeinsoo/$r" "$r"
            fi
            TARGET="$PACKAGE_REF"
            if ! git -C "$r" fetch --depth 1 origin "$TARGET" 2>/dev/null; then
              echo "::warning::$r 에 '$PACKAGE_REF' 가 없다 — main으로 받는다"
              TARGET=main
              git -C "$r" fetch --depth 1 origin "$TARGET"
            fi
            git -C "$r" checkout -q -B ci-build FETCH_HEAD
          done

      - name: NuGet 패키지 복원 (NuGetForUnity CLI, packages.config 기반)
        run: |
          set -e
          dotnet tool restore
          dotnet tool run nugetforunity restore .
          test -f Assets/NuGetForUnity/Packages/R3.1.3.1/lib/netstandard2.1/R3.dll

      - name: baseline content_state 다운로드 (S3 latest)
        env:
          AWS_ACCESS_KEY_ID: ${{ vars.AWS_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
        run: |
          set -e
          if ! aws s3 cp "s3://lop-client/builds/dev/ios/latest.json" latest.json 2>/dev/null; then
            echo "::error::builds/dev/ios/latest.json 없음 — client-app-deploy-ios를 dev로 먼저 실행해 baseline을 만드세요."; exit 1
          fi
          STATE_KEY=$(python3 -c "import json;print(json.load(open('latest.json'))['content_state'])")
          echo "baseline: $STATE_KEY"
          mkdir -p Assets/AddressableAssetsData/iOS
          aws s3 cp "$STATE_KEY" Assets/AddressableAssetsData/iOS/addressables_content_state.bin

      - name: 어드레서블 콘텐츠 update 빌드 (Update a Previous Build)
        run: |
          set -eo pipefail
          UNITY="/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity"
          if ! "$UNITY" -batchmode -quit -nographics -buildTarget iOS -projectPath . \
                -executeMethod BuildScript.BuildIOSContentUpdate -logFile - > unity-content.log 2>&1; then
            echo "::error::content update failed"; tail -80 unity-content.log; exit 1
          fi
          tail -15 unity-content.log
          ls ServerData/iOS

      - name: 콘텐츠 업로드 (additive, --delete 금지)
        env:
          AWS_ACCESS_KEY_ID: ${{ vars.AWS_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
        run: |
          set -e
          aws s3 sync ServerData/iOS "s3://lop-assets/dev/iOS"
          echo "콘텐츠 갱신 완료: s3://lop-assets/dev/iOS/"
```

- [ ] **Step 4: 돌려서 확인한다**

콘텐츠를 눈에 띄게 하나 바꾼 뒤(예: 맵의 물체 색) `content-deploy`를 `target=client-app-ios`로 실행하고, **앱을 다시 깔지 않은 채** 폰에서 그 변경이 보이는지 본다.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Editor/BuildScript.cs .github/workflows/content-deploy.yml
git commit -m "ci(ios): 앱을 다시 깔지 않고 콘텐츠만 갱신하는 경로

TestFlight로 깔린 앱은 폰에 남으므로 안드로이드와 같은 이유로 증분이어야 한다.
full로 다시 구우면 이미 깔린 앱이 든 번들·카탈로그와 어긋난다."
```

---

## Task 7: QR 게시

**Files:**
- Modify: `.github/workflows/client-app-deploy-ios.yml`

- [ ] **Step 1: 공개 링크를 변수에 넣는다**

Settings → Variables에 `TESTFLIGHT_PUBLIC_LINK` = App Store Connect 외부 그룹의 공개 링크(`https://testflight.apple.com/join/XXXXXXXX`).

- [ ] **Step 2: QR 스텝을 추가한다**

`client-app-deploy-ios.yml`의 "content_state 보존" 스텝 뒤에 추가:

```yaml
      - name: QR + 설치 링크 게시
        if: vars.TESTFLIGHT_PUBLIC_LINK != ''
        env:
          AWS_ACCESS_KEY_ID: ${{ vars.AWS_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          BUILD_ENV: ${{ inputs.environment }}
          LINK: ${{ vars.TESTFLIGHT_PUBLIC_LINK }}
        run: |
          set -eo pipefail
          if ! command -v qrencode >/dev/null; then
            echo "::error::러너에 qrencode가 없다. 맥 러너에서 'brew install qrencode' 1회 실행할 것."
            exit 1
          fi
          SHA="${{ steps.tag.outputs.sha }}"
          # 안드로이드 QR은 매번 새로 만드는 프리사인 URL이지만, TestFlight 공개 링크는
          # 한 번 만들면 고정이다 — 그림만 새로 올린다.
          qrencode -o qr.png -s 8 "$LINK"
          aws s3 cp qr.png "s3://lop-client/builds/$BUILD_ENV/ios/$SHA/qr.png"
          QR_URL=$(aws s3 presign "s3://lop-client/builds/$BUILD_ENV/ios/$SHA/qr.png" --expires-in 604800)
          {
            echo "## iOS $BUILD_ENV 빌드 \`$SHA\`"
            echo
            echo "![QR]($QR_URL)"
            echo
            echo "[TestFlight로 설치]($LINK)"
            echo
            echo "외부 테스터는 버전당 첫 빌드가 베타 심사를 거친다 — 심사 통과 전에는 링크가 비어 보일 수 있다."
          } >> "$GITHUB_STEP_SUMMARY"
```

- [ ] **Step 3: 돌려서 확인한다**

워크플로 실행 후 잡 요약의 QR을 **다른 사람 폰으로** 찍어 TestFlight 가입 화면이 뜨는지 본다.

- [ ] **Step 4: 커밋하고 main에 올린다**

```bash
git add .github/workflows/client-app-deploy-ios.yml
git commit -m "ci(ios): 잡 요약에 TestFlight QR 게시"

# CLAUDE.md의 푸시 규약 — 한 줄씩 결과를 확인하며 진행한다
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/ios-build
git push origin main
```

---

## 완료 기준

- [ ] 워크플로 하나를 돌리면 TestFlight에 빌드가 올라간다
- [ ] 내부 테스터 폰에 설치돼 **로그인·매칭·게임 진입까지** 된다
- [ ] 앱을 다시 깔지 않고 콘텐츠만 갱신할 수 있다
- [ ] 잡 요약의 QR로 다른 사람이 설치할 수 있다
