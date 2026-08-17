# LeagueOfPhysical-Client

## 답변 스타일 (필수)

**답변은 항상 이해하기 쉬운 형태로 한다.** 전문용어·추상 개념(generation/application,
cascade, egress, CQRS 등)을 그대로 나열하지 말고, 일상어로 풀고 구체 예시·표·다이어그램을
곁들인다. 큰 그림(왜/무엇)을 먼저 제시하고 세부는 뒤에 둔다. 한 문단에 새 개념을 여러 개
몰아넣지 않는다. 개념 자체는 정교하게 다루되, *전달*은 쉬워야 한다.

## 코드 주석 (필수)

**불필요한 주석은 달지 않고, 다는 주석은 쉽게 쓴다.**

- **불필요한 주석 지양**: 코드로 자명한 것(무엇을 하는지)은 주석 없이 둔다. 변수명/함수명으로
  드러나면 주석 불필요. 비자명한 *의도(왜)* 만 짧게 남긴다.
- **쉽게 쓰기**: 주석은 일상어로. 설명 없이 전문용어(예: kernel, brake-to-desired,
  passthrough, momentum 등)를 던지지 말고, 그 자리에서 무슨 뜻인지 풀어 쓴다. 코드를 처음 보는
  사람이 한 줄로 이해되게.
- 아직 없는 미래 기능을 현재 주석에 섞지 않는다(혼동 유발). 상세 컨벤션은 `docs/architecture-guidelines.md`의 "주석 컨벤션" 참고.

## 푸시 규약 (필수) — 원격 main 리베이스 → `--no-ff` 머지

**모든 저장소(8개 전부)에서 main 푸시는 아래 순서로만 한다. 다른 방식으로 푸시하지 않는다.**

```bash
git fetch origin
git rebase --autostash origin/main   # 피처 브랜치를 원격 main 최신 위로
git checkout main
git merge --ff-only origin/main      # 로컬 main을 원격에 맞춤
git merge --no-ff <feature>          # 머지 커밋 생성
git push origin main
```

**한 줄씩 결과를 확인하고 넘어간다. `&&`로 길게 이어 붙이지 말 것** — 실패한 단계를 지나쳐도 뒤
단계가 성공해 버려서 *푸시는 됐는데 절차는 안 밟은* 상태가 된다(이 규약을 처음 적용한 날 실제로
그랬다: Unity 워킹트리가 dirty라 리베이스가 거부됐는데 그대로 머지·푸시까지 갔다. 마침 원격이 안
움직여 결과만 맞았을 뿐이다). **`--autostash`가 그 사고의 재발 방지책**이다 — 그래도 리베이스가
실패하면 거기서 멈춘다.

**왜 이 순서인가**

- **리베이스가 먼저**여야 피처 커밋이 *원격 최신* 위에서 검증된 상태가 된다. 로컬 main 기준으로
  머지하면 원격에만 있는 변경과 처음 만나는 지점이 main이 된다.
- **`--no-ff`** 로 머지 커밋을 남겨 "어느 커밋들이 한 슬라이스였는지"가 히스토리에 보존된다.
- **`--ff-only`** 로 로컬 main을 맞춘다 — 여기서 실패하면 로컬 main에 직접 커밋한 것이 있다는 뜻이니
  멈추고 확인한다(main 직접 커밋 금지 위반의 탐지기).

**반드시 지킬 것**

- **`git push --force` / `--force-with-lease` 금지.** 푸시가 거절되면 힘으로 밀지 말고
  **다시 `fetch` → 리베이스 → 재시도**한다. 이 프로젝트는 머신이 둘이라 그 사이 원격이 움직이는 일이
  실제로 잦다(`[[two-machines-check-origin-first]]`).
- **로컬 main을 기준으로 브랜치를 파지 말 것.** 로컬 main이 원격보다 뒤처져 있을 수 있다 — 브랜치
  생성 직후 `git fetch && git rev-list --left-right --count origin/main...HEAD`로 확인한다.
- **여러 레포가 걸린 변경은 레포마다 각각** 이 순서를 밟는다. 한 레포만 올라가면 계약이 어긋난다.

**Unity 레포(클라·서버) 추가 주의**

- 워킹트리에 **의도적으로 커밋하지 않는 로컬 픽스처**가 늘 있다(에디터 부팅 설정, 스폰 개수,
  볼륨 프로파일 재직렬화, 아트 서브모듈 포인터, 폰트 에셋). 리베이스 전에 `git stash push -u -m ...`로
  빼두고 끝나면 `pop`한다. **`git add -A` / `git commit -a` 금지** — 반드시 바꾼 파일만 경로로 지정하고,
  커밋 전에 `git status --short`로 스테이지된 것이 의도한 파일뿐인지 확인한다.
- 그 픽스처가 **upstream에서 개명된 파일** 위에 있을 수 있다. stash → 리베이스 → pop이면 git이 rename을
  추적해 새 파일로 옮겨 붙인다(실증됨). 충돌이 나면 픽스처는 사용자 것이니 임의 판단하지 말고 보고한다.
- 파일 이름을 바꿀 때는 **`.cs`와 짝 `.meta`를 함께 `git mv`** 한다 — GUID가 보존돼 씬·프리팹 참조가
  안 끊기고, 에디터가 안 떠 있어도 안전하다.

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

## Architecture & design docs (auto-loaded every session)

These files describe the **durable structure, design contracts, and conventions**
that all work in this repo must follow. They are imported via `@` syntax below so
their contents are injected into the context at session start — no hook needed.
Read and respect them **before** modifying anything related to entities, the
World Core, netcode, or matching FSM.

- @docs/architecture-guidelines.md
- @docs/entity-system-design.md
- @docs/lop-repo-topology.md
- @docs/world-core-connection-architecture.md
- @docs/netcode-redesign.md

> Only **durable** docs are auto-loaded: the five architecture docs above. Add an
> `@` line for a new spec **only while its work is active**; once a slice is
> implemented and merged, **remove its `@` line** (the file stays in
> `docs/superpowers/specs/` for reference, read on demand). This keeps the
> auto-load set small. (The `game-scene-scope` design was auto-loaded while parked;
> its `@` line was removed once it shipped — confirmed implemented in the 2026-07-13
> audit.)
>
> Completed slice specs (World Core Health/Mana/Level/Stats/Owner migration,
> server Health slices, Motion, MVC-decouple, UI-Toolkit M1, LOP-Shared,
> MasterData-Luban, netcode Phase 0–3, etc.) live in `docs/superpowers/specs/` but
> are **not** auto-loaded — their locked decisions are summarized in the
> architecture docs above and in project memory. Read a completed spec on demand
> only if you need its detail.
>
> `docs/superpowers/plans/` is likewise **not** auto-loaded — plans are per-task,
> read on demand only when executing that specific plan.
