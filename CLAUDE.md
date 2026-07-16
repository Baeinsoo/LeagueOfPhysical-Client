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
