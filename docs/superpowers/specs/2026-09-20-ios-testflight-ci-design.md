# iOS 빌드를 TestFlight로 — CI 파이프라인

## 1. 왜

안드로이드는 이미 버튼 하나로 APK가 구워져 S3에 올라가고 QR까지 나온다
(`client-app-deploy.yml`). iOS에는 그 길이 없다. 지금은 사람이 유니티를 돌리고 Xcode를 열어
케이블로 꽂아야 하고, 그마저도 무료 계정이면 **7일 뒤 앱이 안 열린다.**

iOS도 같은 수준으로 만든다 — 워크플로를 돌리면 폰에 설치할 수 있는 빌드가 나온다.

## 2. 전달 방식 — TestFlight

iOS는 안드로이드와 결정적으로 다르다. **APK는 파일만 받으면 깔리지만 ipa는 그렇지 않다.**
그래서 "어떻게 폰까지 가져갈 것인가"를 먼저 골라야 한다.

### 2.1 왜 ad-hoc OTA가 아닌가

후보는 둘이었다.

| | ad-hoc OTA | **TestFlight (채택)** |
|---|---|---|
| 설치 | QR 찍으면 바로 깔림 | TestFlight 앱을 통해 깔림 |
| 기기 등록 | **기기마다 UDID를 미리 등록**해야 하고, 기기를 추가하면 프로파일을 다시 받아 재빌드 | 불필요 |
| 인원 | 연 100대/기기종류 | 내부 100명, 외부 1만명 |
| 심사 | 없음 | 외부 그룹은 버전당 첫 빌드만 (보통 24시간) |

ad-hoc이 "QR 찍고 바로 설치"라는 점에서는 안드로이드에 더 가깝다. 그런데 **기기를 하나 추가할
때마다 프로파일을 다시 만들고 앱을 다시 구워야 한다** — 사람이 늘어날수록 손이 많이 가고, 그
손이 가는 자리가 하필 서명이라 틀리면 원인 찾기가 어렵다. TestFlight는 그 축이 아예 없다.

심사는 **버전당 한 번**이지 빌드마다가 아니다. `0.1` 안에서 빌드 번호만 올리는 동안은 대개 즉시
통과하므로, 평소 반복 주기에는 걸리지 않는다.

### 2.2 내부·외부 테스터를 둘 다 둔다

TestFlight의 "QR"은 **외부 그룹의 공개 링크**를 말한다. 내부 테스터에게는 공개 링크가 아예 없다
— 빌드가 올라가면 몇 분 만에 각자의 TestFlight 앱에 뜬다.

그래서 둘 다 쓴다:

- **내부 테스터** — 본인과 팀. 올리면 몇 분 만에 받는다. 심사 없음. 평소 반복은 이 경로.
- **외부 그룹 + 공개 링크** — 남에게 보여줄 때. 이 링크의 QR을 CI가 찍는다.

CI는 빌드를 한 번 올릴 뿐이고, 어느 그룹에 뿌릴지는 App Store Connect 쪽 설정이다.

## 3. 도구 — fastlane

### 3.1 왜 손으로 쓴 xcodebuild 스크립트가 아닌가

처음에는 `xcodebuild -allowProvisioningUpdates`로 Xcode가 인증서를 알아서 만들게 하는 안을
생각했다. 비밀값이 API 키 하나면 되니 간단해 보였다. **업계 표준을 확인하고 뒤집었다.**

유니티 CI의 사실상 표준인 **GameCI가 iOS 배포에 fastlane을 명시한다** — 빌드·서명·업로드 전부.
손으로 쓴 `xcodebuild`/`security` 조합은 "한 줄 바꾸면 서명이 조용히 깨지는 bespoke shell
incantation"으로 평가되고, fastlane이 그것을 검증된 액션 몇 개로 접는다.

### 3.2 왜 match이고, 왜 S3인가

`match`는 서명에 쓰는 인증서와 프로파일을 **암호화해서 한 곳에 모아두고 모든 기계가 같은 것을
가져다 쓰게** 한다.

표준이라서만은 아니다. **이 프로젝트는 개발 머신이 두 대다.** 자동 서명은 기계마다 인증서를 따로
만들어 쌓고, 계정당 배포 인증서 한도에 걸리면 서로의 것을 무효화한다 — 두 대라는 사실 자체가
match를 가리킨다.

저장소는 **S3**를 쓴다. match는 git 저장소 / S3 / Google Cloud를 지원하는데, 이 프로젝트엔 이미
버킷과 CI 자격증명이 있다. 인증서 보관만을 위해 레포를 9개로 늘릴 이유가 없다.

⚠️ **둘 곳은 `s3://lop-client/certificates/` 다. `lop-assets`에 두면 안 된다.** `lop-assets`는
앱이 자격증명 없이 카탈로그를 받아가는 **공개 읽기 버킷**이다 — 거기에 서명 인증서를 두면 링크를
아는 누구나 받아갈 수 있다. `lop-client`는 APK를 프리사인 URL로만 내주는 비공개 버킷이라 안전하다.
인증서는 암호화돼 저장되지만(`MATCH_PASSWORD`), 애초에 공개 위치에 두지 않는 것이 먼저다.

CI에서는 반드시 **`readonly`** 로 쓴다. CI가 인증서를 새로 만들 수 있으면 사고가 난 날 조용히
기존 인증서를 무효화한다.

### 3.3 산업 표준 매핑

| 우리가 쓰는 것 | 대응하는 표준 |
|---|---|
| `fastlane match` | GameCI iOS 배포 가이드가 지정하는 서명 자산 관리 |
| `fastlane gym` (`build_app`) | Xcode archive + export의 표준 래퍼 |
| `fastlane pilot` (`upload_to_testflight`) | TestFlight 업로드 표준 |
| `fastlane setup_ci` | CI 임시 키체인 생성 (GitHub 공식 문서가 수동으로 안내하는 절차) |
| App Store Connect API 키(.p8) | 2FA 없이 CI가 애플과 통신하는 표준 인증 |
| 얇은 워크플로 YAML + `Fastfile`에 로직 | GameCI 패턴 |

## 4. 파이프라인

### 4.1 흐름

```
워크플로 실행 (environment, development 선택)
  │
  ├─ 체크아웃 + 형제 패키지(GameFramework/Shared/MasterData) + NuGet 복원
  │     └ 기존 워크플로들과 같은 앞부분
  │
  ├─ 유니티 배치: 어드레서블 콘텐츠 full 빌드 (iOS)
  │     └ ServerData/iOS → s3://lop-assets/dev/iOS
  │
  ├─ 유니티 배치: BuildScript.BuildIOSXcodeProject
  │     └ Build/iOS/Unity-iPhone.xcodeproj  (환경 구움 + 개발 빌드 + Info.plist 후처리)
  │
  └─ bundle exec fastlane ios beta
        ├ setup_ci            임시 키체인
        ├ match(readonly)     S3에서 인증서·프로파일
        ├ 서명 설정 덮어쓰기   자동 → 수동(match 프로파일)
        ├ 빌드 번호 채번
        ├ gym                 archive → ipa
        └ pilot               TestFlight 업로드
              ↓
        content_state + QR 게시
```

### 4.2 서명 — 로컬은 자동, CI는 match

두 경로의 요구가 반대다. 로컬에서 케이블로 꽂아 볼 때는 Xcode 자동 서명이 편하고, CI는 match가
준 프로파일을 정확히 써야 한다.

**유니티 설정은 자동 서명 ON으로 둔다**(로컬 편의). CI에서는 fastlane이 유니티가 만들어낸 Xcode
프로젝트에 `update_code_signing_settings(use_automatic_signing: false)`로 수동 서명을 덮어쓰고
match 프로파일을 지정한다. 유니티는 매번 프로젝트를 새로 만들므로 이 덮어쓰기도 매번 돈다.

### 4.3 빌드 번호와 버전

TestFlight는 **같은 버전에 같은 빌드 번호를 두 번 받지 않는다.** 지금 `buildNumber: iPhone`이 0에
고정돼 있어 두 번째 업로드부터 거부된다.

- **마케팅 버전**(`bundleVersion`, 지금 `0.1`) — 사람이 올린다. 이걸 올리면 외부 그룹은 심사를
  다시 한 번 거친다.
- **빌드 번호** — CI가 채번한다. `latest_testflight_build_number + 1`(fastlane 표준)로, 애플이
  들고 있는 값을 기준으로 삼는다. 러너나 레포 상태에 의존하지 않아 두 기계에서 돌려도 안 겹친다.

### 4.4 개발 빌드로 굽는다 — 평문 http 때문

dev 백엔드 주소가 `http://115.68.178.46:31000/...` 로 **평문**이다. 이걸 통과시키는 장치가 두
군데에 있고 **둘 다 "개발 빌드일 때만" 열린다**:

- 유니티의 `insecureHttpOption = DevelopmentOnly`
- iOS 운영체제의 차단(ATS) — 후처리 스크립트가 개발 빌드에만 예외를 넣는다

따라서 dev 환경을 보는 TestFlight 빌드는 **유니티 개발 빌드**로 굽는다. TestFlight는 개발 빌드를
받아준다. 백엔드가 https로 가면 이 제약은 사라진다(이 문서 범위 밖).

### 4.5 어드레서블 콘텐츠 — 첫 빌드는 full, 이후 증분

TestFlight로 깔린 앱은 **폰에 남는다.** 그래서 안드로이드와 같은 규칙이 적용된다 — 이미 깔린 앱이
들고 있는 번들과 카탈로그가 어긋나면 안 된다.

| 언제 | 콘텐츠 |
|---|---|
| 앱을 새로 배포할 때 (이 워크플로) | **full** — 그리고 그 결과 `content_state`가 다음 증분의 기준이 된다 |
| 앱은 그대로 두고 콘텐츠만 고칠 때 | **증분** — `content-deploy.yml`에 iOS 잡을 추가 (슬라이스 4) |

S3 경로는 이렇게 둔다:

```
s3://lop-assets/dev/iOS/                                   콘텐츠(번들·카탈로그)
s3://lop-client/builds/<env>/ios/<sha>/addressables_content_state.bin
s3://lop-client/builds/<env>/ios/latest.json               증분 빌드가 읽는 기준점
```

**안드로이드의 `builds/<env>/latest.json`은 건드리지 않는다.** 플랫폼별로 나누면 대칭이 예쁘지만,
잘 돌아가는 안드로이드 배포를 정리하겠다고 깨뜨릴 이유가 없다. 비대칭은 주석으로 남긴다.

### 4.6 QR

TestFlight 공개 링크는 **한 번 만들면 고정 URL**이라 빌드마다 바뀌지 않는다. App Store Connect
API로 조회할 수도 있지만 그럴 값을 하지 않는다 — `vars.TESTFLIGHT_PUBLIC_LINK`에 한 번 넣어두고
CI는 그것으로 QR만 그린다. 안드로이드가 쓰는 `qrencode`를 그대로 쓴다.

안드로이드와 다른 점: 안드로이드 QR은 **매번 새로 만드는 프리사인 URL**(7일 만료)이지만, iOS QR은
**늘 같은 링크**다.

## 5. 파일

| 파일 | 상태 |
|---|---|
| `.github/workflows/client-app-deploy-ios.yml` | 신규 — 얇게. 유니티 배치 두 번 + fastlane 호출 |
| `fastlane/Fastfile` | 신규 — `ios beta` 레인 |
| `fastlane/Appfile`, `fastlane/Matchfile` | 신규 — 앱 식별자·팀·match 저장소 설정 |
| `Gemfile`, `Gemfile.lock` | 신규 — fastlane 버전 고정 |
| `Assets/Editor/IOSBuildPostProcess.cs` | 기존 `IOSAppTransportSecurity.cs`를 rename·확장 |
| `Assets/Editor/BuildScript.cs` | 변경 없음 — `BuildIOSXcodeProject`가 이미 있다 |
| `ProjectSettings/ProjectSettings.asset` | 앱 아이콘, 팀 ID |
| `.gitignore` | fastlane 산출물·`Build/` 제외 |
| `content-deploy.yml` | 슬라이스 4에서 iOS 증분 잡 추가 |

**후처리 스크립트 이름을 바꾸는 이유**: 지금 이름은 평문 http 허용 전용인데 역할이 하나 더 는다.
한 파일이 "iOS 빌드 후처리"를 맡게 되므로 이름을 역할에 맞춘다. 레포에 같은 개념을 가리키는
어휘가 둘이 되는 것을 피한다.

담당하는 일 두 가지:

1. **평문 http 허용** (`NSAllowsArbitraryLoads`) — 개발 빌드에만
2. **암호화 수출 규정** (`ITSAppUsesNonExemptEncryption = false`) — 항상. 이게 없으면 업로드할
   때마다 사람이 웹에서 같은 질문에 답해야 한다

## 6. 비밀값과 변수

| 이름 | 종류 | 쓰임 |
|---|---|---|
| `ASC_KEY_ID` | secret | App Store Connect API 키 ID |
| `ASC_ISSUER_ID` | secret | 같은 키의 발급자 ID |
| `ASC_KEY_P8` | secret | .p8 본문 (base64) |
| `MATCH_PASSWORD` | secret | match 저장소 암호화 암호 |
| `APPLE_TEAM_ID` | vars | 팀 식별자 |
| `MATCH_S3_BUCKET` | vars | 인증서를 둘 버킷 — `lop-client`(비공개). 공개 버킷 `lop-assets` 금지 |
| `TESTFLIGHT_PUBLIC_LINK` | vars | QR로 그릴 공개 링크 |
| `AWS_ACCESS_KEY_ID` | **vars** (기존) | ⚠️ secret으로 옮기지 말 것 — 프리사인 URL이 이 값을 URL에 담는데, secret이면 GitHub이 가려버려 링크가 깨진다 (`client-app-deploy.yml` 머리 주석 참고) |
| `AWS_SECRET_ACCESS_KEY` | secret (기존) | |

## 7. 사람이 먼저 해야 하는 것

이 파이프라인은 아래가 갖춰지기 전에는 한 줄도 돌지 않는다. **가입 승인에 시간이 걸리니 먼저
시작하는 게 좋다.**

1. **Apple Developer Program 가입** — Individual 권장(24~48시간). Organization은 D-U-N-S 번호와
   법인 확인 때문에 2~4주. 개인 명의로 시작해도 나중에 이관할 수 있다
2. **App Store Connect에 앱 레코드 생성** — 번들 ID `com.BAEGames.LeagueOfPhysicalClient`를
   Identifiers에 등록한 뒤 앱을 만든다
3. **App Store Connect API 키 발급** (.p8) — 역할은 App Manager 이상
4. **앱 아이콘** — 현재 iOS 아이콘 슬롯 **19개가 전부 비어 있다.** 아이콘이 없으면 TestFlight
   업로드가 실패한다. 1024×1024 원본이 필요하고, 이건 디자이너 작업물이다
5. **외부 테스터 그룹 + 공개 링크 생성** → `vars.TESTFLIGHT_PUBLIC_LINK`
6. **match 최초 1회 초기화** — 로컬에서 `fastlane match appstore`를 한 번 돌려 인증서를 만들고
   S3에 넣는다. 이후 CI는 `readonly`로 읽기만 한다

## 8. 알려진 지뢰

- **Xcode 26 업로드 회귀** — 우리가 쓰는 26.6이 해당된다. Xcode 26의 `altool`이 패키지를 못 여는
  문제가 보고돼 있고, 레거시 경로를 강제하는 우회가 알려져 있다. 처음부터
  `DELIVER_ALTOOL_ADDITIONAL_UPLOAD_PARAMETERS="--use-old-altool"`를 넣고 시작한다
- **아이콘 없음 → 업로드 실패.** 위 7-4
- **외부 그룹 첫 빌드는 심사 대기.** 버전당 한 번, 보통 24시간
- **개발 빌드 + 평문 http 전면 허용**이라 외부 심사에서 질문을 받을 수 있다. 내부 테스터 경로는
  심사가 없으므로 막히지 않는다
- **러너가 한 대**다. 유니티를 동시에 두 개 돌릴 수 없어 기존 워크플로들이 `max-parallel: 1`을
  쓴다. 같은 제약을 따른다
- **GUI 대화상자 금지.** 에디터로 어드레서블을 구우면 "Modified Scenes must be saved to continue"
  대화상자가 메인 스레드를 잡고 선다 — 겉으로는 오래 걸리는 빌드와 구별되지 않는다(2026-09-19에
  90분을 그렇게 보냈다). 모든 유니티 실행은 배치모드로 한다

## 9. 시험

CI 파이프라인이라 유닛 테스트로 덮을 대상이 아니다. 각 슬라이스를 **실제로 돌려서** 확인한다.

| 확인할 것 | 방법 |
|---|---|
| 콘텐츠가 iOS로 구워졌나 | 로그의 `content full build target: iOS` + 카탈로그에 `FlappyRaceMap`·`Bird.prefab` (기존 워크플로가 쓰는 검사와 동일) |
| 서명이 match 것으로 됐나 | `codesign -dvvv`로 ipa의 서명 주체 확인 |
| 업로드가 됐나 | App Store Connect에 빌드가 뜨고 상태가 "처리 완료" |
| 실제로 깔리나 | 내부 테스터로 본인 폰에 설치 후 로그인·매칭까지 |
| QR이 맞나 | 잡 요약의 QR을 찍어 TestFlight 가입 화면이 뜨는지 |

마지막 항목이 진짜 통과 기준이다 — **폰에서 게임에 들어가야 끝난 것이다.**

## 10. 슬라이스

앞 슬라이스를 로컬에서 성공시킨 뒤에 CI로 옮긴다. 서명은 CI에서 처음 만나면 원인 찾기가 어렵다.

| 슬라이스 | 내용 | 끝났다고 보는 기준 |
|---|---|---|
| **1** | 사람 몫 준비 (7절 전부) | ASC에 앱 레코드가 있고, 아이콘이 들어갔고, match 초기화가 끝남 |
| **2** | fastlane 도입 — Gemfile/Appfile/Matchfile/Fastfile, 후처리 스크립트 rename·확장 | **로컬에서** `bundle exec fastlane ios beta`로 TestFlight에 올라감 |
| **3** | 워크플로 `client-app-deploy-ios.yml` 추가 | CI에서 돌려 내부 테스터 폰에 설치됨 |
| **4** | `content-deploy.yml`에 iOS 증분 잡 | 앱을 다시 안 깔고 콘텐츠만 갱신됨 |
| **5** | QR 게시 | 잡 요약의 QR로 남의 폰에서 설치됨 |

## 11. 여기서 정하지 않는 것

- **App Store 정식 출시** — 심사·스토어 메타데이터·개인정보 항목은 별건. 개발 빌드로는 못 낸다
- **백엔드 https 전환** — 되면 4.4의 개발 빌드 제약이 사라진다
- **안드로이드 baseline 경로를 플랫폼별로 대칭화** — 지금 깨뜨릴 이유 없음
- **공통 앞부분을 reusable workflow로 추출** — 플랫폼이 셋이 될 때
- **Organization 계정 이관** — 스토어에 회사 이름으로 낼 때
- **안드로이드 아이콘** — 18슬롯이 비어 있지만 사이드로드는 막히지 않는다. 스토어에 낼 때 같이
