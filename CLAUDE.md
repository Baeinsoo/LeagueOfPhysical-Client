# LeagueOfPhysical-Client

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
- @docs/superpowers/specs/2026-06-06-game-scene-scope-design.md

> Only **durable** docs are auto-loaded: the five architecture docs above, plus
> the *parked, not-yet-implemented* `game-scene-scope` design. Add an `@` line for
> a new spec **only while its work is active**; once a slice is implemented and
> merged, **remove its `@` line** (the file stays in `docs/superpowers/specs/` for
> reference, read on demand). This keeps the auto-load set small.
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
