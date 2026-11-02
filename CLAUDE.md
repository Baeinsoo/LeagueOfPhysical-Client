# LeagueOfPhysical-Client

@../league-of-physical/CLAUDE.md

위 줄이 모든 repo 공통 규칙(답변 스타일·주석·푸시 규약·문서 규칙)을 불러온다. 여러 repo에 걸친 문서는
형제 repo `league-of-physical`의 `docs/`에 있다(지도: `../league-of-physical/docs/README.md`).
이 repo 안에서만 의미 있는 README·운영 절차는 여기에 둔다.

## 아키텍처 문서 (매 세션 자동 로드)

엔티티·World Core·넷코드·매칭 FSM을 건드리기 전에 따른다.

- @../league-of-physical/docs/architecture-guidelines.md
- @../league-of-physical/docs/entity-system-design.md
- @../league-of-physical/docs/lop-repo-topology.md
- @../league-of-physical/docs/world-core-connection-architecture.md
- @../league-of-physical/docs/netcode-redesign.md
- @../league-of-physical/docs/superpowers/specs/2026-09-28-dodge-game-mode-design.md
- @../league-of-physical/docs/superpowers/specs/2026-10-25-knockback-royale-design.md

> 진행 중인 슬라이스의 spec은 **작업하는 동안만** `@../league-of-physical/docs/superpowers/specs/…` 줄을 더하고, 닫을 때 뺀다.

## 인게임 확인은 매크로로 — 사람에게 넘기기 전에 (필수)

UI·조작·화면·게임 흐름을 바꿨으면 **에디터 자동화(매크로, 우리식 monkeyrunner)로 직접 판을 돌려 본다.**
도구 설명·인자는 `Automation/README.md`. 공통 원칙은 `../league-of-physical/CLAUDE.md`의 "확인은 에이전트가 먼저".

1. `unity cmd set_autotick --enable true` (에디터가 뒤에 있어도 틱이 돈다).
2. 시나리오로 끝까지: 로그인 → 모드 선택 → 판 → 결과 → 로비. 확인할 조작은 `touch`·`drag`·`long_press`(`"device":"touch"`로 폰 손가락)와
   `screenshot`으로 남긴다. 다시 쓸 만한 확인은 `Automation/Scenarios/<이름>.json`으로 **커밋**한다(본보기: `knockback-controls`).
3. **스크린샷을 직접 열어 보고 숫자로 판정한다**(HUD 숫자, 탄 수, 상대 %, 남은 사람…). 단계 `ok`는 "입력이 들어갔다"는 뜻일 뿐이다.
   양성·음성을 같이 본다 — 예: 쏘면 탄이 줄고, 취소하면 그대로.
4. 끄는 중 같은 **중간 상태**는 `automation_step`으로 긴 `drag`(수 초)를 걸어 두고 그 사이 `unity cmd screenshot`(게임 뷰 — 3D 표시는
   찍히고 UI Toolkit 겹침은 안 찍힌다)이나 `eval`로 읽는다.
5. 보고에 "매크로로 확인한 것(스크린샷 경로·수치) / 사람 몫으로 남은 것(진동·소리·실기기 손맛·재미)"을 나눠 적는다.

**함정** (실제로 겪음)
- `wait_state InMatch`는 **카운트다운 전에** 켜진다 → 조작 전 10초쯤 기다리거나 화면으로 "GO!"를 확인한다. 안 그러면 입력이 무시된다.
- 봇 판에서 가만히 서 있으면 금방 떨어진다 → 확인할 조작을 판 시작 직후에 몰아 둔다.
- 재생 중엔 `set_environment`가 거절된다 → setup 맨 앞에 `play_exit`.
- **에디터가 뒤에 있거나 최소화되면 CLI 명령이 전부 30초 타임아웃**(`set_autotick`을 켜 둬도 생긴다) → 사람을 부르지 말고
  `powershell -NoProfile -ExecutionPolicy Bypass -File Automation/focus-editor.ps1 [-Project LeagueOfPhysical-Server]`로 창을 앞으로 올린 뒤 다시 시도한다
  (윈도우가 막는 앞으로 올리기를 Alt 한 번으로 우회한다 — `front=True`면 성공). 그래도 안 받으면 그때 사용자에게 재시작을 부탁한다.
- 매크로가 도는 동안은 입력이 게임으로 간다 — 다른 작업(특히 키 입력)을 같이 하지 않는다.
- 클라 에디터 하나에 시험·매크로를 동시에 걸지 않는다(서브에이전트가 시험을 돌리는 중이면 끝난 뒤에).

## UnityMCP instance targeting

This project is the **client**. The UnityMCP server may have both the server and
client Unity editors connected at the same time, so the target instance is
ambiguous unless pinned.

**`set_active_instance` does NOT reliably pin routing here** — the UnityMCP HTTP
transport treats calls statelessly, so a session pin does not carry over to the
next call and routing silently falls back to another instance (e.g. the server).

**Instead, pass `unity_instance` explicitly on EVERY UnityMCP tool call** in this
project, targeting the client:

1. Resolve the client id by name: read `mcpforunity://instances`, find the
   instance whose `name` is `LeagueOfPhysical-Client`, take its full `id`
   (`Name@hash`). At time of writing it is
   `LeagueOfPhysical-Client@de70658b9450cbb4`, but the hash can change.
2. Pass that id as the `unity_instance` argument on each tool call
   (e.g. `read_console(..., unity_instance="LeagueOfPhysical-Client@<hash>")`).

Resources (e.g. `mcpforunity://instances`) cannot take `unity_instance`; that is
fine for global resources. For per-instance reads, prefer the equivalent tool
with `unity_instance` set.

Never operate against the server instance from this project unless the user
explicitly asks.

