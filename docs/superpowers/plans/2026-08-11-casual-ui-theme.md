# 캐주얼 UI 테마 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 프론트엔드·인게임 UI 전반에 폴가이즈 톤 공통 테마를 입혀 "덜 만든 티"를 걷어낸다. 새 이미지 에셋 0장.

**Architecture:** 색·치수를 `Theme.uss`의 `:root` 변수(토큰)에 모으고 공통 부품 클래스(`.btn`/`.card`/`.title`)를 정의한다. 적용은 `LopTheme.tss` 한 곳 → 두 PanelSettings가 이를 참조하므로 화면 UI와 월드스페이스 UI에 동시 적용된다. 각 화면 USS에는 레이아웃(위치·크기·정렬)만 남긴다.

**Tech Stack:** Unity 6000.3.16f1 · UI Toolkit (UXML/USS/TSS) · TextCore Font Asset (SDF) · UnityMCP(에디터 구동·콘솔 확인)

**설계 문서:** `docs/superpowers/specs/2026-08-11-casual-ui-theme-design.md`

## Global Constraints

- **새 이미지 에셋을 만들지 않는다.** 색·테두리·라운드·폰트만으로 구현한다. 기존 PNG(`lobby_screen`/`loading_screen`/`title_screen`)는 사용 가능.
- **USS에 없는 기능을 쓰지 않는다:** `linear-gradient`, `box-shadow`, 배경 흐림(blur)은 UI Toolkit 미지원. 입체감은 `border-bottom-width` + (주 CTA만) 바닥면 자식 요소로 낸다.
- **`@import url("unity-theme://default")`를 반드시 유지한다.** 빼면 기본 컨트롤이 깨진다.
- **좌표계는 1920×1080.** 두 PanelSettings 모두 `Scale With Screen Size` / 기준 해상도 1920×1080 / `Match: 0`(가로폭 기준). USS의 px 값은 이 좌표계로 쓴다.
- **글자 테두리·그림자는 SDF Font Asset에서만 동작한다.** 비트맵으로 만들면 안 나온다.
- **`.meta` 파일을 직접 만들거나 고치지 않는다.** 파일을 만든 뒤 Unity가 임포트하며 생성한 `.meta`를 커밋한다. (`CLAUDE.md` 규칙)
- **UnityMCP 호출마다 `unity_instance`를 명시한다.** `mcpforunity://instances`에서 `name`이 `LeagueOfPhysical-Client`인 항목의 전체 `id`(`Name@hash`)를 쓴다. 서버 인스턴스를 건드리지 않는다. (`CLAUDE.md` 규칙)
- **의미를 가진 색은 바꾸지 않는다.** HP 빨강 / MP 파랑 / EXP 노랑 / 네임플레이트 HP 초록 / 데미지 숫자 노랑 — 전부 유지.
- **`DebugHud`는 손대지 않는다.** 개발용이라 꾸미면 가독성이 나빠진다.
- **버튼·라벨 문구를 바꾸지 않는다.** 이 작업은 스타일 작업이다.
- **UXML의 `name` 속성을 바꾸지 않는다.** View 코드가 `Root.Q<Button>("play-button")` 식으로 name을 찾는다. `class`만 추가·수정한다.

## 검증 방식에 대하여 — 읽고 시작할 것

**이 저장소에는 테스트 어셈블리가 없다.** `Assets` 아래에 `.asmdef`가 하나도 없고 EditMode/PlayMode 폴더도 없다(전부 `Assembly-CSharp`). USS 스타일링을 위해 테스트 인프라를 새로 세우는 것은 과설계이므로 **자동 단위 테스트를 만들지 않는다.**

대신 매 태스크는 아래 3단 검증으로 닫는다. 형식이 아니라 실제로 실패를 잡는다:

| 단계 | 방법 | 무엇을 잡나 |
|---|---|---|
| ① 콘솔 | `read_console` | **USS 문법 오류·에셋 참조 깨짐.** Unity는 USS 파싱 실패를 콘솔 에러로 남긴다 |
| ② 스크린샷 | 대상 화면을 띄워 캡처 | 실제로 적용됐는지 |
| ③ 명시적 육안 확인 | 태스크마다 "무엇이 보여야 하는가"를 문장으로 지정 | "적용됐다"는 착각 |

**"콘솔이 깨끗하다"를 확인 없이 주장하지 않는다.** 반드시 `read_console` 출력을 보고 판단한다.

### UnityMCP 인스턴스 해석 (매 태스크 시작 시 1회)

```
1. mcpforunity://instances 리소스를 읽는다
2. name == "LeagueOfPhysical-Client" 인 항목의 id 를 꺼낸다  (예: LeagueOfPhysical-Client@de70658b9450cbb4)
3. 이후 모든 UnityMCP 호출에 unity_instance=<그 id> 를 넘긴다
```

> MCP 첫 호출은 자주 실패한다. 실패하면 한 번 재시도한다.

---

## File Structure

| 파일 | 책임 | 태스크 |
|---|---|---|
| `Assets/UI/Theme/Theme.uss` | **토큰**(`:root` 색·치수 변수) + **공통 부품 클래스** | 1, 3 |
| `Assets/UI/Theme/LopTheme.tss` | 테마 진입점. 유니티 기본 테마 + `Theme.uss` import | 1 |
| `Assets/UI/UIPanelSettings.asset` | 화면 UI 패널 → `themeUss`를 `LopTheme.tss`로 | 1 |
| `Assets/Resources/UI/WorldSpaceNameplatePanelSettings.asset` | 월드스페이스 패널 → 동일 | 1 |
| `Assets/UI/Theme/Fonts/` | 주아체 TTF + OFL 라이선스 + SDF Font Asset | 2 |
| `Assets/UI/{Login,LobbyHome,Shell,Overlay,MatchResult,Stats}/` | 공통 부품 클래스 부착 + USS를 레이아웃만 남김 | 4 |
| `Assets/UI/{CharacterHud,GamePad}/` | 인게임 HUD 적용 | 5 |
| `Assets/Resources/UI/{Nameplate,DamageFloater}.uss` | 월드스페이스 UI 적용 | 5 |

### USS가 화면에 붙는 경로가 둘이다 (중요)

| 경로 | 대상 |
|---|---|
| `Assets/UI/UIViewCatalog.asset`의 `(viewName, uxml, uss)` 항목 | `LoginView` `StatsView` `GameLoadingView` `MatchingWaitingView` `LobbyHomeView` `MatchResultView` `ShopView` `SettingsView` `ProfileView` `CharacterHudView` `GamePadView` `DebugHudView` |
| UXML 안의 `<Style src="...">` | `CharacterHud` `GamePad` `DebugHud` `Nameplate` `DamageFloater` |

**`Theme.uss`는 이 둘 중 어디에도 등록하지 않는다** — TSS를 통해 전역으로 적용되기 때문이다. 화면별 USS 항목은 그대로 둔다.

> `ShopView`/`SettingsView`/`ProfileView`는 셋 다 `ShellView.uxml` + 같은 USS를 가리킨다(준비 중 화면). 한 번 고치면 셋 다 반영된다.

### 설계 문서와 달라진 점 (의도된 축소)

설계 문서 §6은 공통 부품에 `.pill`(상단 재화·레벨 알약)을 넣었다. **이 계획은 `.pill`을 만들지 않는다** — 현재 어느 화면에도 재화·레벨 표시 요소가 없어서, 만들면 쓰는 곳 없는 죽은 코드가 된다. 목업의 알약은 "이런 걸 넣으면 이렇게 보인다"는 예시였지 지금 있는 UI가 아니다. 재화 표시를 실제로 추가하는 것은 스타일 작업이 아니라 기능 추가이므로 범위 밖이다.

---

## Task 1: 테마 배선 (토큰만)

색·치수 토큰을 만들고 두 패널에 물린다. **이 태스크는 화면 모습을 바꾸지 않는다.** 목표는 "테마가 연결됐고 기존 UI가 하나도 깨지지 않았다"를 확인하는 것이다.

**Files:**
- Create: `Assets/UI/Theme/Theme.uss`
- Create: `Assets/UI/Theme/LopTheme.tss`
- Modify: `Assets/UI/UIPanelSettings.asset` (15행 `themeUss`)
- Modify: `Assets/Resources/UI/WorldSpaceNameplatePanelSettings.asset` (15행 `themeUss`)

**Interfaces:**
- Produces: Task 3이 쓰는 토큰 이름 —
  `--lop-primary` `--lop-primary-shade` `--lop-secondary` `--lop-secondary-shade`
  `--lop-nav2` `--lop-nav2-shade` `--lop-nav3` `--lop-nav3-shade`
  `--lop-surface` `--lop-surface-sunken` `--lop-surface-line`
  `--lop-text` `--lop-text-on-fill` `--lop-outline` `--lop-title` `--lop-title-outline` `--lop-scrim`
  `--lop-border` `--lop-border-s` `--lop-bevel` `--lop-bevel-s`
  `--lop-radius-pill` `--lop-radius-l` `--lop-radius-m`
- Produces: `Assets/UI/Theme/LopTheme.tss` — Task 2가 여기 import된 `Theme.uss`에 폰트 지정을 추가한다

- [ ] **Step 1: `Assets/UI/Theme/Theme.uss` 생성**

```css
/* LOP 캐주얼 UI 테마 — 토큰
 *
 * 색은 Assets/UI/Title/title_screen.png(타이틀 로고)에서 뽑았다.
 * 로고와 UI가 같은 색을 쓰게 해서 "로고 따로 UI 따로"인 상태를 없애는 게 목적.
 *
 * 치수는 1920x1080 기준이다 (PanelSettings가 이 해상도 기준으로 스케일).
 * 시작값이며 눈으로 보고 조정한다.
 */
:root {
    /* 주 행동 — PLAY, 로그인, 확인 */
    --lop-primary: #F5931E;
    --lop-primary-shade: #B4510F;

    /* 보조 — 취소, 뒤로 */
    --lop-secondary: #4A9FE0;
    --lop-secondary-shade: #2C6FA8;

    /* 하단 네비 2·3번째 */
    --lop-nav2: #5FC94A;
    --lop-nav2-shade: #389030;
    --lop-nav3: #A87BD8;
    --lop-nav3-shade: #6F51B8;

    /* 카드·패널 */
    --lop-surface: #FDF6EA;
    --lop-surface-sunken: #E6D8C4;   /* 게이지 홈처럼 파인 자리 */
    --lop-surface-line: #C25A12;

    /* 글자 */
    --lop-text: #5A3A1E;
    --lop-text-on-fill: #FFFFFF;
    --lop-title: #FFC93C;
    --lop-title-outline: #B4510F;

    /* 모든 요소를 감싸는 흰 테두리 — 이 톤의 핵심 */
    --lop-outline: #FFFFFF;

    /* 모달 뒤에 까는 어두운 막 */
    --lop-scrim: rgba(20, 12, 6, 0.45);

    /* 테두리 두께 */
    --lop-border: 12px;
    --lop-border-s: 8px;

    /* 아래쪽만 두껍게 해서 입체감을 낸다 (box-shadow가 USS에 없음) */
    --lop-bevel: 24px;
    --lop-bevel-s: 16px;

    /* 모서리 둥글기 */
    --lop-radius-pill: 999px;
    --lop-radius-l: 44px;
    --lop-radius-m: 36px;
}
```

- [ ] **Step 2: `Assets/UI/Theme/LopTheme.tss` 생성**

```css
/* 이 파일이 프로젝트 전체 UI 테마의 진입점이다.
 * 두 PanelSettings(화면 UI / 월드스페이스)가 이 파일을 참조한다.
 *
 * 아래 기본 테마 import를 지우면 유니티 기본 컨트롤이 깨진다. 지우지 말 것.
 */
@import url("unity-theme://default");
@import url("Theme.uss");
```

- [ ] **Step 3: Unity에 임포트시키고 콘솔 확인**

UnityMCP로 에디터를 포커스해 애셋 리프레시를 유발한 뒤:

```
read_console(types=["error","warning"], unity_instance=<client-id>)
```

기대: `Theme.uss` / `LopTheme.tss` 관련 에러 0건. `.meta` 2개가 생성되어 있을 것.

**만약 `@import url("Theme.uss")`에서 임포트 에러가 나면** 상대 경로가 지원되지 않는 것이다. `Assets/UI/Theme/Theme.uss.meta`의 `guid`를 읽어 아래 형태로 교체한다(UI Builder가 생성하는 표기):

```css
@import url("project://database/Assets/UI/Theme/Theme.uss?fileID=7433441132597879392&guid=<Theme.uss.meta의 guid>&type=3#Theme");
```

교체 후 다시 `read_console`로 에러 0건을 확인한다.

- [ ] **Step 4: 두 PanelSettings를 새 테마로 돌리기**

`Assets/UI/Theme/LopTheme.tss.meta`에서 `guid`를 읽는다. 그 값으로 아래 두 파일의 `themeUss` 줄을 바꾼다 (`fileID`는 tss 에셋 공통값이라 그대로).

`Assets/UI/UIPanelSettings.asset` 15행:
```yaml
  themeUss: {fileID: -4733365628477956816, guid: <LopTheme.tss.meta의 guid>, type: 3}
```

`Assets/Resources/UI/WorldSpaceNameplatePanelSettings.asset` 15행: 같은 값으로 동일하게 변경.

> 두 파일 모두 지금은 `guid: 1368e93e5bb21824b84b8d6c2716cd79`(UnityDefaultRuntimeTheme.tss)를 가리키고 있다. 둘 다 바꿔야 월드스페이스 네임플레이트까지 적용된다.

- [ ] **Step 5: 검증 — 기본 테마 import가 살아있는지**

```bash
grep -n 'unity-theme://default' Assets/UI/Theme/LopTheme.tss
```
기대: 1건 매치.

> **왜 grep으로 확인하나:** 원래는 "스크롤바가 정상 렌더되는지"로 확인하려 했으나, 이 프로젝트의 UXML에는 **기본 컨트롤이 하나도 없다**(ScrollView·TextField·Toggle·Slider·ListView 전부 미사용. `VisualElement`·`Label`·`Button`뿐). 그래서 지금은 import를 빼도 눈에 보이는 증상이 없고, 나중에 기본 컨트롤을 쓰기 시작한 순간 깨진다. 육안으로 못 잡으므로 파일 내용으로 확인한다.

- [ ] **Step 6: 검증 — 콘솔 + 기존 UI 무손상**

```
read_console(types=["error","warning"], unity_instance=<client-id>)
```
기대: 에러 0건. 특히 "theme"·"stylesheet"·"import"가 든 에러가 없어야 한다.

이어서 프론트엔드 씬을 플레이해 로그인 화면을 캡처한다.

보여야 하는 것:
- 화면이 **Task 1 이전과 똑같이** 보인다 (토큰만 정의했고 쓰는 곳이 없으므로 당연)
- 버튼·라벨이 사라지거나 배치가 무너지지 않았다

- [ ] **Step 7: 커밋**

```bash
git add Assets/UI/Theme Assets/UI/UIPanelSettings.asset Assets/Resources/UI/WorldSpaceNameplatePanelSettings.asset
git commit -m "feat(ui): 캐주얼 테마 배선 — 토큰 정의 + 두 패널에 연결

색·치수를 Theme.uss의 :root 변수로 모으고 LopTheme.tss를 진입점으로 둔다.
화면 UI/월드스페이스 두 PanelSettings가 같은 테마를 본다.
유니티 기본 테마 import는 유지 — 빼면 기본 컨트롤이 깨진다.
아직 토큰을 쓰는 곳이 없어 화면 모습은 변하지 않는다."
```

---

## Task 2: 폰트 (주아체 SDF)

기본 폰트를 주아체로 바꾼다. **Task 3의 글자 테두리가 동작하려면 이 태스크가 먼저**여야 한다 — 글자 테두리·그림자는 SDF Font Asset에서만 동작하기 때문이다.

**Files:**
- Create: `Assets/UI/Theme/Fonts/Jua-Regular.ttf`
- Create: `Assets/UI/Theme/Fonts/OFL.txt`
- Create: `Assets/UI/Theme/Fonts/Jua-Regular SDF.asset` (Unity가 생성)
- Modify: `Assets/UI/Theme/Theme.uss` (`:root`에 폰트 지정 추가)

**Interfaces:**
- Consumes: Task 1의 `Theme.uss` `:root` 블록
- Produces: 전 UI의 기본 폰트가 주아체 SDF가 된다. Task 3의 `-unity-text-outline-width`가 이에 의존한다

- [ ] **Step 1: 폰트와 라이선스 파일 받기**

```bash
mkdir -p "Assets/UI/Theme/Fonts"
curl -sSL -o "Assets/UI/Theme/Fonts/Jua-Regular.ttf" "https://github.com/google/fonts/raw/main/ofl/jua/Jua-Regular.ttf"
curl -sSL -o "Assets/UI/Theme/Fonts/OFL.txt"        "https://github.com/google/fonts/raw/main/ofl/jua/OFL.txt"
ls -lh "Assets/UI/Theme/Fonts/"
```

기대: `Jua-Regular.ttf`가 약 2.0MB(한글 글리프 포함 크기). 수십 KB면 잘못 받은 것이니 다시 받는다.

> 주아체(Jua)는 우아한형제들이 만들어 Google Fonts에 SIL Open Font License로 공개한 폰트다. 상업 이용 가능하며 `OFL.txt`를 함께 두는 것이 라이선스 준수 방식이다.

- [ ] **Step 2: TTF 임포트 확인**

Unity 애셋 리프레시 후:
```
read_console(types=["error","warning"], unity_instance=<client-id>)
```
기대: 폰트 임포트 에러 0건. `Jua-Regular.ttf.meta` 생성됨.

- [ ] **Step 3: SDF Font Asset 생성**

Project 창에서 `Jua-Regular.ttf`를 선택하고 **Assets > Create > Text > Font Asset** 실행. `Assets/UI/Theme/Fonts/Jua-Regular SDF.asset`이 생긴다.

Inspector에서 아래 3개를 확인·설정한다:

| 항목 | 값 | 왜 |
|---|---|---|
| Atlas Population Mode | **Dynamic** | 한글은 음절이 많아 전부 미리 굽지 않고 필요한 글자만 올린다 |
| Atlas Render Mode | **SDFAA** | SDF여야 글자 테두리·그림자가 동작 |
| Atlas Width / Height | 1024 이상 | 한글 글리프가 들어갈 공간 |

- [ ] **Step 4: `Theme.uss`의 `:root`에 폰트 지정 추가**

`:root { ... }` 블록 **맨 위**(주석 다음, `--lop-primary` 앞)에 아래를 넣는다. `<guid>`는 `Jua-Regular SDF.asset.meta`에서 읽는다.

```css
    /* 폰트 — 주아체 SDF.
     * SDF여야 하는 이유: 글자 테두리(-unity-text-outline-*)와 그림자(text-shadow)가
     * SDF 폰트에서만 동작한다. 비트맵으로 만들면 테두리가 안 나온다. */
    -unity-font-definition: url("project://database/Assets/UI/Theme/Fonts/Jua-Regular%20SDF.asset?fileID=11400000&guid=<Jua-Regular SDF.asset.meta의 guid>&type=2#Jua-Regular SDF");
```

> 파일명의 공백은 URL에서 `%20`이다.

- [ ] **Step 5: 검증 — 콘솔**

```
read_console(types=["error","warning"], unity_instance=<client-id>)
```
기대: 에러 0건. 폰트 URL이 틀리면 여기서 잡힌다.

- [ ] **Step 6: 검증 — 한글이 실제로 렌더되는지**

로비 화면을 띄워 캡처한다. 하단 네비에 "상점 · 프로필 · 설정"이 있다.

보여야 하는 것:
- 세 글자가 **네모(□)나 빈칸이 아니라 한글로** 보인다
- 글자 모양이 기본 고딕이 아니라 **둥글둥글한 주아체**다
- 영문("LEAGUE OF PHYSICAL", "PLAY")도 같은 폰트로 바뀌었다

- [ ] **Step 7: 폴백 폰트**

주아체에 없는 글자를 대비해 `Jua-Regular SDF.asset` Inspector의 **Fallback Font Assets**에 폰트 애셋을 1개 추가한다. 프로젝트에 다른 폰트 애셋이 없으면 이 스텝은 건너뛰고, Task 6 스윕에서 실제 누락 글자(□)가 나오는지로 판단한다. **어느 쪽이든 결과를 Task 6 Step 5에서 설계 문서 §9에 한 줄로 기록한다.**

- [ ] **Step 8: 커밋**

```bash
git add Assets/UI/Theme/Fonts Assets/UI/Theme/Theme.uss
git commit -m "feat(ui): 기본 폰트를 주아체 SDF로

Google Fonts의 Jua(OFL, 상업 이용 가능)를 SDF Font Asset으로 만들어
:root에 지정. Dynamic 모드라 한글은 필요한 글자만 아틀라스에 올린다.
SDF여야 하는 이유: 글자 테두리/그림자가 SDF에서만 동작한다."
```

---

## Task 3: 공통 부품 클래스

`.btn` `.card` `.title`과 눌림 반응을 만든다. 이 태스크만으로는 아직 어느 화면도 변하지 않는다 — 클래스를 붙이는 것은 Task 4·5다.

**Files:**
- Modify: `Assets/UI/Theme/Theme.uss` (`:root` 아래에 부품 클래스 추가)

**Interfaces:**
- Consumes: Task 1의 토큰 전부, Task 2의 SDF 폰트
- Produces: Task 4·5가 UXML `class` 속성에 쓰는 이름 —
  `btn` `btn--primary` `btn--secondary` `btn--nav1` `btn--nav2` `btn--nav3`
  `btn__base` `card` `title` `scrim` `card-text`

- [ ] **Step 1: `Theme.uss` 끝에 부품 클래스 추가**

```css
/* ────────────────────────────────────────────────────────────
 * 공통 부품
 *
 * 입체감 규칙: USS에는 box-shadow가 없다. 그래서
 *   - 기본   = 아래쪽 테두리만 두껍게 (border-bottom-width)
 *   - 주 CTA = 버튼 뒤에 바닥면 요소(.btn__base)를 한 겹 깐다
 * ──────────────────────────────────────────────────────────── */

.btn {
    color: var(--lop-text-on-fill);
    border-color: var(--lop-outline);
    border-width: var(--lop-border-s);
    border-bottom-width: var(--lop-bevel-s);
    border-radius: var(--lop-radius-l);
    -unity-text-align: middle-center;
    -unity-text-outline-width: 2px;
    padding-left: 28px;
    padding-right: 28px;

    /* 눌렀을 때 내려가는 움직임. 짧아야 조작이 굼떠 보이지 않는다 */
    transition-property: translate, border-bottom-width;
    transition-duration: 70ms;
}

/* PC 테스트용. 모바일에선 보이지 않는다 */
.btn:hover {
    border-color: #FFF6E0;
}

/* 눌리면 버튼이 아래로 내려가고 바닥 두께가 같은 만큼 줄어든다.
 * 두 값을 같이 움직여야 버튼의 바닥 위치가 제자리에 있는 것처럼 보인다. */
.btn:active {
    translate: 0 8px;
    border-bottom-width: 8px;
}

.btn--primary {
    background-color: var(--lop-primary);
    -unity-text-outline-color: var(--lop-primary-shade);
    border-radius: var(--lop-radius-pill);
}

.btn--secondary {
    background-color: var(--lop-secondary);
    -unity-text-outline-color: var(--lop-secondary-shade);
}

.btn--nav1 {
    background-color: var(--lop-secondary);
    -unity-text-outline-color: var(--lop-secondary-shade);
}

.btn--nav2 {
    background-color: var(--lop-nav2);
    -unity-text-outline-color: var(--lop-nav2-shade);
}

.btn--nav3 {
    background-color: var(--lop-nav3);
    -unity-text-outline-color: var(--lop-nav3-shade);
}

/* 주 CTA 뒤에 까는 바닥면. 버튼보다 아래로 내려 깔아 두께처럼 보이게 한다.
 * 남용하지 말 것 — PLAY 같은 주 행동 버튼에만. */
.btn__base {
    position: absolute;
    left: 0;
    right: 0;
    bottom: -10px;
    height: 40px;
    background-color: var(--lop-primary-shade);
    border-radius: var(--lop-radius-pill);
}

.card {
    background-color: var(--lop-surface);
    border-color: var(--lop-surface-line);
    border-width: var(--lop-border-s);
    border-bottom-width: var(--lop-bevel-s);
    border-radius: var(--lop-radius-m);
    padding: 48px;
    align-items: stretch;
}

.title {
    color: var(--lop-title);
    -unity-text-align: middle-center;
    -unity-text-outline-width: 6px;
    -unity-text-outline-color: var(--lop-title-outline);
    text-shadow: 0 6px 0 rgba(90, 40, 8, 0.45);
}

/* 모달 뒤에 까는 어두운 막 */
.scrim {
    background-color: var(--lop-scrim);
}

/* 카드 안의 본문 글자 — 크림 배경 위라 어두운 색 */
.card-text {
    color: var(--lop-text);
    -unity-text-align: middle-center;
}
```

- [ ] **Step 2: 검증 — 콘솔**

```
read_console(types=["error","warning"], unity_instance=<client-id>)
```

기대: 에러 0건. USS 문법 오류나 **정의되지 않은 변수**가 여기서 잡힌다. `var(--lop-*)` 이름을 Task 1의 Interfaces 목록과 하나씩 대조할 것.

- [ ] **Step 3: 검증 — 기존 화면이 안 변했는지**

로비 화면을 캡처한다.

보여야 하는 것:
- 아직 **아무 화면도 변하지 않았다** (클래스를 정의만 했고 UXML에 붙이지 않았으므로)
- 폰트는 Task 2의 주아체 그대로

뭔가 변했다면 새 클래스 이름이 기존 화면 클래스와 겹친 것이므로 이름을 확인한다.

- [ ] **Step 4: 커밋**

```bash
git add Assets/UI/Theme/Theme.uss
git commit -m "feat(ui): 공통 부품 클래스 — btn/card/title + 눌림 반응

box-shadow가 USS에 없으므로 입체감은 아래쪽 테두리로 낸다.
주 CTA만 바닥면 자식 요소(.btn__base)를 한 겹 더 깐다.
눌림은 translate와 border-bottom-width를 같이 움직여
바닥 위치가 제자리인 것처럼 보이게 한다."
```

---

## Task 4: 프론트엔드 화면 적용

여기서 처음으로 화면이 눈에 띄게 변한다. 아래 파일들은 **전체 교체**다.

**Files:**
- Modify: `Assets/UI/Login/LoginView.uxml`, `LoginView.uss`
- Modify: `Assets/UI/LobbyHome/LobbyHomeView.uxml`, `LobbyHomeView.uss`
- Modify: `Assets/UI/Shell/ShellView.uxml`, `ShellView.uss`
- Modify: `Assets/UI/Overlay/MatchingWaitingView.uxml`, `MatchingWaitingView.uss`
- Modify: `Assets/UI/Overlay/GameLoadingView.uxml`, `GameLoadingView.uss`
- Modify: `Assets/UI/MatchResult/MatchResultView.uxml`, `MatchResultView.uss`
- Modify: `Assets/UI/Stats/StatsView.uxml`, `StatsView.uss`

**Interfaces:**
- Consumes: Task 3의 클래스 이름 전부

- [ ] **Step 1: 로그인 화면**

`Assets/UI/Login/LoginView.uxml` 전체 교체:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="login-root" class="login-root">
        <ui:VisualElement name="login-card" class="card login-card">
            <ui:Label text="LEAGUE OF PHYSICAL" class="title login-title" />
            <ui:Button name="guest-login" text="Guest Login" class="btn btn--primary login-button" />
            <ui:Button name="gpgs-login" text="Google Play" class="btn btn--secondary login-button" />
            <ui:Button name="gamecenter-login" text="Game Center" class="btn btn--secondary login-button" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

`Assets/UI/Login/LoginView.uss` 전체 교체:

```css
/* 색·테두리·라운드는 Theme.uss의 .card/.btn/.title이 담당한다.
 * 여기엔 이 화면에서만 다른 위치·크기만 둔다. */
.login-root {
    flex-grow: 1;
    align-items: center;
    justify-content: center;

    /* 쓰이지 않고 있던 타이틀 아트를 로그인 배경으로 쓴다 */
    background-image: url("../Title/title_screen.png");
    -unity-background-scale-mode: scale-and-crop;
}

.login-card {
    width: 760px;
}

.login-title {
    font-size: 52px;
    margin-bottom: 40px;
}

.login-button {
    height: 108px;
    margin-top: 20px;
    font-size: 38px;
}
```

- [ ] **Step 2: 로비 화면**

`Assets/UI/LobbyHome/LobbyHomeView.uxml` 전체 교체. PLAY 버튼은 바닥면 요소와 겹치기 위해 래퍼가 하나 필요하다:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="lobbyhome-root" class="lobbyhome-root">
        <ui:VisualElement name="lobbyhome-top" class="lobbyhome-top">
            <ui:Label text="LEAGUE OF PHYSICAL" class="title lobbyhome-title" />
        </ui:VisualElement>

        <ui:VisualElement name="lobbyhome-center" class="lobbyhome-center">
            <!-- 바닥면(.btn__base)을 버튼 뒤에 깔기 위한 래퍼. 버튼이 아니라 이 래퍼가 위치를 잡는다. -->
            <ui:VisualElement name="play-slot" class="lobbyhome-play-slot" picking-mode="Ignore">
                <ui:VisualElement class="btn__base" picking-mode="Ignore" />
                <ui:Button name="play-button" text="PLAY" class="btn btn--primary lobbyhome-play" />
            </ui:VisualElement>
        </ui:VisualElement>

        <!-- 하단 네비바: 레이아웃만. 실제 동작은 Slice C에서 배선. -->
        <ui:VisualElement name="nav-bar" class="lobbyhome-navbar">
            <ui:Button name="nav-shop" text="상점" class="btn btn--nav1 lobbyhome-nav" />
            <ui:Button name="nav-profile" text="프로필" class="btn btn--nav2 lobbyhome-nav" />
            <ui:Button name="nav-settings" text="설정" class="btn btn--nav3 lobbyhome-nav" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

`Assets/UI/LobbyHome/LobbyHomeView.uss` 전체 교체:

```css
.lobbyhome-root {
    flex-grow: 1;
    justify-content: space-between;
    align-items: center;
    padding: 40px;
    background-image: url("../Lobby/lobby_screen.png");
    -unity-background-scale-mode: scale-and-crop;
}

.lobbyhome-top {
    align-items: center;
    margin-top: 24px;
}

.lobbyhome-title {
    font-size: 64px;
}

.lobbyhome-center {
    flex-grow: 1;
    justify-content: center;
    align-items: center;
}

/* 바닥면과 버튼을 겹쳐 놓기 위한 컨테이너 */
.lobbyhome-play-slot {
    width: 460px;
    height: 150px;
}

.lobbyhome-play {
    width: 100%;
    height: 100%;
    font-size: 64px;
}

.lobbyhome-navbar {
    flex-direction: row;
    justify-content: center;
    margin-bottom: 32px;
}

.lobbyhome-nav {
    width: 240px;
    height: 110px;
    font-size: 36px;
    margin-left: 20px;
    margin-right: 20px;
}
```

- [ ] **Step 3: 매칭 대기**

`Assets/UI/Overlay/MatchingWaitingView.uxml` 전체 교체:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="matching-root" class="scrim matching-root">
        <ui:VisualElement name="matching-card" class="card matching-card">
            <ui:Label text="MATCHING" class="title matching-title" />
            <ui:Label text="Waiting for players..." class="card-text matching-message" />
            <ui:Button name="cancel-button" text="Cancel" class="btn btn--secondary matching-cancel" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

`Assets/UI/Overlay/MatchingWaitingView.uss` 전체 교체:

```css
.matching-root {
    flex-grow: 1;
    align-items: center;
    justify-content: center;
}

.matching-card {
    width: 680px;
}

.matching-title {
    font-size: 48px;
    margin-bottom: 16px;
}

.matching-message {
    font-size: 30px;
    margin-bottom: 36px;
}

.matching-cancel {
    height: 100px;
    font-size: 34px;
}
```

- [ ] **Step 4: 매치 결과** (매칭 대기와 같은 모양의 모달. 지금 두 USS는 값이 100% 같다)

`Assets/UI/MatchResult/MatchResultView.uxml` 전체 교체:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="matchresult-root" class="scrim matchresult-root">
        <ui:VisualElement name="matchresult-card" class="card matchresult-card">
            <ui:Label text="결과" class="title matchresult-title" />
            <ui:Label text="매치 종료" class="card-text matchresult-message" />
            <ui:Button name="confirm-button" text="확인" class="btn btn--primary matchresult-confirm" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

`Assets/UI/MatchResult/MatchResultView.uss` 전체 교체:

```css
.matchresult-root {
    flex-grow: 1;
    align-items: center;
    justify-content: center;
}

.matchresult-card {
    width: 680px;
}

.matchresult-title {
    font-size: 48px;
    margin-bottom: 16px;
}

.matchresult-message {
    font-size: 30px;
    margin-bottom: 36px;
}

.matchresult-confirm {
    height: 100px;
    font-size: 34px;
}
```

- [ ] **Step 5: 로딩 화면**

`Assets/UI/Overlay/GameLoadingView.uxml` 전체 교체:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="loading-root" class="loading-root">
        <ui:Label text="LOADING..." class="title loading-text" />
    </ui:VisualElement>
</ui:UXML>
```

`Assets/UI/Overlay/GameLoadingView.uss` 전체 교체:

```css
.loading-root {
    flex-grow: 1;
    align-items: center;
    justify-content: flex-end;
    background-image: url("loading_screen.png");
    -unity-background-scale-mode: scale-and-crop;
}

.loading-text {
    font-size: 56px;
    margin-bottom: 72px;
}
```

> 원래 있던 `background-color: rgba(10,10,14,0.96)`는 배경 이미지 뒤에 깔린 검정이라 지운다. 이미지가 화면을 다 덮는다.

- [ ] **Step 6: 셸 화면** (상점·프로필·설정이 공유하는 "준비 중" 화면)

`Assets/UI/Shell/ShellView.uxml` 전체 교체:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="shell-root" class="shell-root">
        <ui:VisualElement name="shell-header" class="shell-header">
            <ui:Button name="back-button" text="◀ 뒤로" class="btn btn--secondary shell-back" />
            <ui:Label name="shell-title" text="" class="title shell-title" />
        </ui:VisualElement>
        <ui:VisualElement name="shell-body" class="shell-body">
            <ui:Label text="(준비 중)" class="card-text shell-placeholder" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

`Assets/UI/Shell/ShellView.uss` 전체 교체:

```css
.shell-root {
    flex-grow: 1;
    background-color: var(--lop-surface);
}

.shell-header {
    flex-direction: row;
    align-items: center;
    height: 130px;
    padding: 20px;
}

.shell-back {
    width: 220px;
    height: 90px;
    font-size: 32px;
}

.shell-title {
    flex-grow: 1;
    font-size: 48px;
}

.shell-body {
    flex-grow: 1;
    justify-content: center;
    align-items: center;
}

.shell-placeholder {
    font-size: 34px;
}
```

- [ ] **Step 7: 스탯 패널** (인게임 좌상단에 뜨는 작은 패널)

`Assets/UI/Stats/StatsView.uxml`은 **구조를 그대로 두고 class만 추가**한다 (행이 많아 전체 교체 시 실수 위험이 크다):

| 요소 | 지금 class | 바꿀 class |
|---|---|---|
| `stats-card` | `stats-card` | `card stats-card` |
| `STATS` 라벨 | `stats-title` | `title stats-title` |
| `STR`/`DEX`/`INT`/`VIT`/`Points` 라벨 | `stats-name` | `card-text stats-name` |
| 값 라벨 4개 | `stats-value` | `card-text stats-value` |
| `+` 버튼 4개 | `stats-button` | `btn btn--primary stats-button` |
| `statpoints-value` | `stats-points-value` | (그대로 — 아래 USS에서 색 지정) |

`Assets/UI/Stats/StatsView.uss` 전체 교체:

```css
.stats-root {
    flex-grow: 1;
    align-items: flex-start;
    justify-content: flex-start;
}

.stats-card {
    margin: 32px;
    padding: 28px;
    width: 460px;
}

.stats-title {
    font-size: 36px;
    margin-bottom: 20px;
}

.stats-row {
    flex-direction: row;
    align-items: center;
    margin-top: 12px;
}

.stats-points-row {
    flex-direction: row;
    align-items: center;
    margin-top: 24px;
}

.stats-name {
    width: 120px;
    font-size: 28px;
    -unity-text-align: middle-left;
}

.stats-value {
    flex-grow: 1;
    font-size: 30px;
    -unity-text-align: middle-right;
    margin-right: 20px;
}

/* 남은 포인트 — 초록으로 "쓸 수 있다"를 표시한다 */
.stats-points-value {
    flex-grow: 1;
    font-size: 30px;
    color: var(--lop-nav2-shade);
    -unity-text-align: middle-right;
    margin-right: 20px;
}

/* + 버튼은 작은 정사각이라 공통 .btn의 좌우 여백을 지운다 */
.stats-button {
    width: 64px;
    height: 64px;
    font-size: 30px;
    padding-left: 0;
    padding-right: 0;
    border-width: 5px;
    border-bottom-width: 10px;
}
```

- [ ] **Step 8: 검증 — 콘솔**

```
read_console(types=["error","warning"], unity_instance=<client-id>)
```

기대: 에러 0건. UXML 문법 오류, 배경 이미지 경로 오류가 여기서 잡힌다.

- [ ] **Step 9: 검증 — 화면이 실제로 변했는지**

로그인 → 로비 → 매칭 대기 순으로 띄워 각각 캡처한다.

보여야 하는 것:
- **회색 유니티 기본 버튼이 하나도 없다** ← 이 작업의 핵심 성공 기준
- 로그인 배경에 **타이틀 아트가 깔려 있다**
- PLAY 버튼이 주황 알약 모양이고 **흰 테두리와 아래쪽 두께**가 보인다
- 하단 네비 3개가 각각 파랑·초록·보라다
- 카드가 **검정이 아니라 크림색**이다
- 제목 글자에 **테두리와 그림자**가 보인다 ← SDF 설정이 맞다는 증거

- [ ] **Step 10: 검증 — 눌림 반응**

PLAY 버튼과 취소 버튼을 눌러본다.

보여야 하는 것: 누르는 동안 버튼이 살짝 아래로 내려갔다가 떼면 돌아온다.

- [ ] **Step 11: 커밋**

```bash
git add Assets/UI/Login Assets/UI/LobbyHome Assets/UI/Shell Assets/UI/Overlay Assets/UI/MatchResult Assets/UI/Stats
git commit -m "feat(ui): 프론트엔드 화면에 공통 테마 적용

각 화면 USS에는 레이아웃만 남기고 색·테두리·라운드는 공통 클래스로 옮겼다.
값이 100% 같던 MatchingWaiting/MatchResult USS를 .card로 흡수.
쓰이지 않던 title_screen.png를 로그인 배경으로 사용."
```

---

## Task 5: 인게임 HUD

**Files:**
- Modify: `Assets/UI/CharacterHud/CharacterHud.uss`
- Modify: `Assets/UI/GamePad/GamePad.uxml`, `GamePad.uss`
- Modify: `Assets/Resources/UI/Nameplate.uss`
- Modify: `Assets/Resources/UI/DamageFloater.uss`

**Interfaces:**
- Consumes: Task 1의 토큰, Task 3의 클래스
- **HP/MP/EXP 채움 색, 네임플레이트 초록, 데미지 숫자 노랑은 값을 바꾸지 않는다.**

- [ ] **Step 1: 캐릭터 HUD**

`Assets/UI/CharacterHud/CharacterHud.uss`에서 아래 규칙들을 교체한다. **`.hp-fill` `.mp-fill` `.exp-fill` 세 규칙은 손대지 않는다.**

```css
.hud-panel {
    position: absolute;
    bottom: 24px;
    left: 24px;
    width: 460px;
    padding: 18px;

    /* 검정 반투명 → 크림 카드 */
    background-color: var(--lop-surface);
    border-color: var(--lop-surface-line);
    border-width: 6px;
    border-bottom-width: 12px;
    border-radius: var(--lop-radius-m);
}

.level-text {
    color: var(--lop-text);
    font-size: 28px;
    margin-bottom: 12px;
}

.bar {
    height: 34px;
    margin-bottom: 10px;

    /* 게이지 홈. 흰 테두리를 둘러 톤을 맞춘다 */
    background-color: var(--lop-surface-sunken);
    border-color: var(--lop-outline);
    border-width: 4px;
    border-radius: 999px;
    justify-content: center;
    overflow: hidden;
}

.bar-fill {
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    width: 100%;
    border-radius: 999px;
}

/* 채움 색 위에 얹히는 글자라 테두리로 읽히게 한다 */
.bar-text {
    -unity-text-align: middle-center;
    color: #FFFFFF;
    font-size: 20px;
    -unity-text-outline-width: 2px;
    -unity-text-outline-color: rgba(60, 30, 10, 0.9);
}
```

- [ ] **Step 2: 게임패드 — UXML**

`Assets/UI/GamePad/GamePad.uxml`의 액션 버튼 5줄만 교체한다. 텍스트와 name은 그대로다:

```xml
            <ui:Button name="dash-button" class="btn btn--secondary action-button dash-button" text="DASH" />
            <ui:Button name="haste-button" class="btn btn--nav2 action-button haste-button" text="HASTE" />
            <ui:Button name="jump-button" class="btn btn--nav3 action-button jump-button" text="JUMP" />
            <ui:Button name="attack-button" class="btn btn--primary action-button attack-button" text="ATK" />
            <ui:Button name="global-attack-button" class="btn btn--primary action-button global-attack-button" text="AOE" />
```

- [ ] **Step 3: 게임패드 — USS**

`Assets/UI/GamePad/GamePad.uss`에서 아래 규칙들을 교체한다. `.gamepad-root` `.camera-drag` `.joystick-area` `.action-buttons`는 위치 규칙이라 그대로 둔다.

```css
.joystick-bg {
    position: absolute;
    width: 200px;
    height: 200px;
    border-radius: 999px;
    background-color: rgba(255, 255, 255, 0.18);
    border-width: 6px;
    border-color: var(--lop-outline);
}

.joystick-handle {
    position: absolute;
    left: 55px;
    top: 55px;
    width: 90px;
    height: 90px;
    border-radius: 999px;
    background-color: rgba(255, 255, 255, 0.6);
    border-width: 5px;
    border-color: var(--lop-outline);
}

/* 원형 버튼이라 공통 .btn의 좌우 여백을 지우고 라운드를 원으로 고정한다 */
.action-button {
    width: 120px;
    height: 120px;
    border-radius: 999px;
    margin-left: 18px;
    font-size: 26px;
    padding-left: 0;
    padding-right: 0;
    border-width: 6px;
    border-bottom-width: 12px;
}
```

`.attack-button` `.haste-button` `.global-attack-button` 세 규칙은 **삭제한다** — 색을 UXML의 `btn--*` 클래스로 옮겼으므로 남겨두면 그게 덮어써서 테마가 안 먹는다.

> 아이콘은 이번 범위가 아니다(설계 문서 §10, §14). 글자 그대로 둔다.

- [ ] **Step 4: 네임플레이트** (머리 위 HP 바 — **이름 글자는 없다**)

`Assets/Resources/UI/Nameplate.uss` 전체 교체:

```css
.nameplate-root {
    flex-grow: 1;
    justify-content: center;
    align-items: center;
}

/* 검정 판 → 흰 테두리를 두른 게이지 홈. 월드 위에 뜨는 것이라 얇게 유지한다 */
.hp-bar-bg {
    width: 92%;
    height: 55%;
    background-color: rgba(60, 40, 20, 0.75);
    border-color: var(--lop-outline);
    border-width: 3px;
    border-radius: 999px;
    padding: 2px;
}

/* 초록은 "적/타인의 남은 체력"이라는 의미를 가진 색이라 유지한다 */
.hp-bar-fill {
    height: 100%;
    width: 100%;
    background-color: rgb(60, 200, 90);
    border-radius: 999px;
}
```

- [ ] **Step 5: 데미지 숫자**

`Assets/Resources/UI/DamageFloater.uss` 전체 교체:

```css
.floater-root {
    flex-grow: 1;
    justify-content: center;
    align-items: center;
}

/* 월드 위에 뜨는 글자라 배경 판 대신 테두리로 배경과 분리한다.
 * 노랑은 데미지 표시 관습색이라 유지. */
.damage-text {
    font-size: 52px;
    color: rgb(255, 230, 120);
    -unity-text-align: middle-center;
    -unity-text-outline-width: 3px;
    -unity-text-outline-color: rgba(40, 20, 5, 0.95);
}
```

- [ ] **Step 6: 검증 — 콘솔**

```
read_console(types=["error","warning"], unity_instance=<client-id>)
```
기대: 에러 0건.

- [ ] **Step 7: 검증 — 인게임**

매치에 들어가 인게임 화면을 캡처한다.

보여야 하는 것:
- 좌하단 HUD 패널이 **크림색 카드**이고 게이지에 흰 테두리가 있다
- HP 바가 **여전히 빨강**, MP가 파랑, EXP가 노랑이다 ← 바꾸면 안 되는 것
- 액션 버튼 5개가 색이 들어간 원형이고 회색 기본 버튼이 아니다
- 조이스틱에 흰 테두리가 보인다
- 캐릭터 머리 위 **HP 바**에 흰 테두리가 생겼고 채움은 초록 그대로다
- 피격 시 데미지 숫자에 어두운 테두리가 생겨 배경에 묻히지 않는다

- [ ] **Step 8: 커밋**

```bash
git add Assets/UI/CharacterHud Assets/UI/GamePad Assets/Resources/UI/Nameplate.uss Assets/Resources/UI/DamageFloater.uss
git commit -m "feat(ui): 인게임 HUD에 테마 적용

HUD 패널을 크림 카드로, 게이지에 흰 테두리. HP/MP/EXP 채움 색과
네임플레이트 초록, 데미지 노랑은 의미를 가진 색이라 그대로 둔다.
게임패드 버튼별 색 규칙은 삭제 — UXML의 btn--* 클래스로 옮겼다.
게임패드 아이콘은 이미지가 필요해 범위 밖 — 글자 유지."
```

---

## Task 6: 마무리 스윕

남은 하드코딩 색을 걷어내고 설계 문서의 검증 항목 7개를 전부 통과시킨다.

**Files:**
- Modify: 스윕에서 발견되는 파일들
- Modify: `docs/superpowers/specs/2026-08-11-casual-ui-theme-design.md`

- [ ] **Step 1: 하드코딩된 색이 남았는지 전수 확인**

```bash
grep -rn "rgb(\|rgba(\|#[0-9A-Fa-f]\{6\}" Assets/UI Assets/Resources/UI --include=*.uss \
  | grep -v "Assets/UI/Theme/Theme.uss" \
  | grep -v "DebugHud"
```

기대: 남은 것이 **전부 "의미를 가진 색"**이어야 한다. 아래가 허용 목록이고, 그 외에 나오면 토큰(`var(--lop-*)`)으로 바꾼다.

| 허용되는 잔여 색 | 파일 | 이유 |
|---|---|---|
| HP 빨강 / MP 파랑 / EXP 노랑 | `CharacterHud.uss` | 장르 관습 |
| `.bar-text` 흰색 + 어두운 테두리 | `CharacterHud.uss` | 채움 색 위 가독성 |
| 네임플레이트 초록 + 반투명 홈 | `Nameplate.uss` | 타인 체력 표시 관습 |
| 데미지 노랑 + 어두운 테두리 | `DamageFloater.uss` | 데미지 표시 관습 |
| 조이스틱 반투명 흰색 | `GamePad.uss` | 시야를 가리면 안 되는 요소 |

- [ ] **Step 2: 좁은 화면에서 잘리는지 확인**

Game 뷰 해상도를 세로로 긴 비율(예: 2340×1080)로 바꾸고 로비를 캡처한다.

기대: **하단 네비바 3개가 잘리지 않고 다 보인다.**

잘린다면: `.lobbyhome-navbar`의 `margin-bottom`을 줄이거나 `.lobbyhome-root`의 `padding`을 줄인다. (원인은 PanelSettings의 `Match: 0` — 가로폭 기준으로 스케일하므로 세로가 기준보다 짧아진다.)

- [ ] **Step 3: 설계 문서의 검증 표 7항목 확인**

`docs/superpowers/specs/2026-08-11-casual-ui-theme-design.md` §11을 하나씩 확인한다. 통과 못 한 항목을 고친다.

| 항목 | 기대 | 확인 방법 |
|---|---|---|
| 톤 일치 | 화면들이 한 게임의 UI로 보임 | 캡처 나열 비교 |
| 기본 스킨 잔존 | 회색 유니티 기본 버튼 0개 | 전 화면 육안 |
| 한글 렌더 | "상점·프로필·설정·뒤로"가 □로 안 나옴 | 로비·셸 캡처 |
| 글자 테두리 | 제목·버튼 라벨에 테두리와 그림자 | 로비 캡처 확대 |
| 눌림 반응 | 누르는 동안 내려갔다 돌아옴 | 직접 눌러보기 |
| 기본 테마 유지 | `unity-theme://default` 존재 | Task 1 Step 5의 grep 재실행 |
| 비율 | 세로로 긴 화면에서 하단 안 잘림 | Step 2 |

> §11 원문의 "스크롤바 정상 렌더" 항목은 **확인 불가**다 — 이 프로젝트 UXML에 ScrollView가 없다. grep 확인으로 대체하고, 그 사실을 Step 4에서 설계 문서에 반영한다.

- [ ] **Step 4: 설계 문서에 결과 반영**

`2026-08-11-casual-ui-theme-design.md`를 아래대로 고친다:

1. §6 치수 토큰 표의 값을 **실제 확정값**으로 갱신 (구현 중 조정했다면)
2. §6에서 `.pill`을 공통 부품 목록에서 제거하고, "재화·레벨 표시 UI가 아직 없어 만들지 않음"을 한 줄로 기록
3. §9에 폴백 폰트를 지정했는지 / 누락 글자(□)가 있었는지 한 줄 기록
4. §11 검증 표에서 "기본 컨트롤 — 스크롤바 정상"을 "기본 테마 import 유지 — 파일 내용 확인(프로젝트에 기본 컨트롤 미사용)"으로 정정
5. §14 Open Decisions에서 이번에 결정된 항목 표시

- [ ] **Step 5: 커밋**

```bash
git add -A
git commit -m "chore(ui): 테마 마무리 스윕 — 잔여 하드코딩 색 정리 + 설계 문서 정정

설계 문서 §11 검증 항목 확인. 스크롤바 검증 항목은 프로젝트에
기본 컨트롤이 없어 성립하지 않으므로 파일 내용 확인으로 정정.
.pill은 재화 표시 UI가 없어 만들지 않았음을 기록."
```

---

## 완료 후

`superpowers:finishing-a-development-branch` 스킬로 main 통합 방식을 결정한다. `CLAUDE.md` 규칙상 main 직접 커밋은 금지이며 `--no-ff` 머지로 합친다.

## 이 계획이 다루지 않는 것

설계 문서 §14 Open Decisions 그대로다. 착수하지 말 것:

- **게임패드 아이콘** — 이미지가 필요한 유일한 항목
- **브롤스타즈 톤 승급** — 9-slice 스프라이트 필요. 최종 아트 방향이 정해질 때
- **타이틀 로고 이미지 잘라내기** — 알파 편집 필요. 이번엔 USS 글자로 간다
- **화면 등장 애니메이션** — 윈도우 매니저 레이어의 일
- **재화·레벨 표시 UI 추가** — 스타일이 아니라 기능 추가
