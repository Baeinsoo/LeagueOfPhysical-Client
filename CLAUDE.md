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

These files describe the **structure, design contracts, and conventions** that
all work in this repo must follow. They are imported via `@` syntax below so
their contents are injected into the context at session start — no hook needed.
Read and respect them **before** modifying anything related to entities, the
World Core, netcode, or matching FSM.

- @docs/architecture-guidelines.md
- @docs/entity-system-design.md
- @docs/lop-repo-topology.md
- @docs/world-core-connection-architecture.md
- @docs/netcode-redesign.md
- @docs/superpowers/specs/2026-05-30-world-core-slice3-design.md
- @docs/superpowers/specs/2026-05-31-lop-shared-package-design.md
- @docs/superpowers/specs/2026-06-03-master-data-luban-migration-design.md
- @docs/superpowers/specs/2026-06-06-game-scene-scope-design.md
- @docs/superpowers/specs/2026-06-07-ui-toolkit-migration-m1-design.md
- @docs/superpowers/specs/2026-06-14-entity-view-mvc-decouple-design.md
- @docs/superpowers/specs/2026-06-16-world-core-view-slice-a-design.md
- @docs/superpowers/specs/2026-06-16-world-core-motion-slice-b-design.md
- @docs/superpowers/specs/2026-06-18-world-core-health-replace-legacy-design.md
- @docs/superpowers/specs/2026-06-18-world-core-motion-server-design.md
- @docs/superpowers/specs/2026-06-18-server-creationdata-di-design.md
- @docs/superpowers/specs/2026-06-18-server-health-read-flip-design.md
- @docs/superpowers/specs/2026-06-18-server-health-slice2-design.md
- @docs/superpowers/specs/2026-06-18-world-core-mana-migration-design.md
- @docs/superpowers/specs/2026-06-18-world-core-level-migration-design.md
- @docs/superpowers/specs/2026-06-19-world-core-stats-migration-design.md
- @docs/superpowers/specs/2026-06-19-world-core-owner-migration-design.md
- @docs/superpowers/specs/2026-06-20-server-health-slice3-despawn-unify-design.md
- @docs/superpowers/specs/2026-06-20-owner-map-dedup-design.md
- @docs/superpowers/specs/2026-06-20-netcode-phase1-rotation-snap-design.md
- @docs/superpowers/specs/2026-06-20-netcode-phase0-debug-hud-design.md
- @docs/superpowers/specs/2026-06-20-netcode-phase2-clock-sync-design.md
- @docs/superpowers/specs/2026-06-20-netcode-phase3-input-buffer-design.md

If you add a new spec under `docs/superpowers/specs/`, append an `@` line above.
`docs/superpowers/plans/` is **not** auto-loaded — plans are per-task and read
on demand only when executing that specific plan.
