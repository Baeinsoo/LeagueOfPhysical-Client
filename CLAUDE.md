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

> 진행 중인 슬라이스의 spec은 **작업하는 동안만** `@../league-of-physical/docs/superpowers/specs/…` 줄을 더하고, 닫을 때 뺀다.

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

