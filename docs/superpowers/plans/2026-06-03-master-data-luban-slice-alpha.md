# MasterData Luban — Slice α (Tooling Bootstrap) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the Luban toolchain + Excel-embedded schema in the `infrastructure` repo so `gen` produces typed `.cs` + binary `.bytes` for client/server targets with correct group (c/s) split — **without touching any Unity runtime** (game still runs on the existing CSV path).

**Architecture:** `infrastructure/table/` becomes a Luban workspace: committed Luban v4.9.0 toolchain (`Tools/Luban/`), source Excel converted to Luban Excel-embedded format under `Datas/`, a `luban.conf` declaring groups `c`/`s` and `client`/`server` targets, and `gen.sh`/`gen.bat`. In Slice α the gen output goes to a **scratch dir** for verification only (packages are wired in Slice β/γ). The legacy 2a protobuf assets (`proto/`, `tools/protoc/`, Python `exporter/`, yaml mappings) are removed.

**Tech Stack:** Luban v4.9.0 (.NET 8 CLI tool, `dotnet Luban.dll`), Excel-embedded schema (`__tables__.xlsx` + `#Name.xlsx`), Python 3 + openpyxl (one-shot source→Luban conversion script).

---

## As-Built Notes (Slice α — discovered during execution, 2026-06-03)

The committed files in `infrastructure/table/` (`scripts/convert_source_to_luban.py`, `luban.conf`) are the **canonical source of truth**. Execution against real Luban v4.9.0 surfaced 5 version-specific requirements beyond the original plan; all are reflected in the committed files:

1. **`__tables__.xlsx` requires `output` (+ `tags`) columns** — the built-in `__TableRecord__` bean validates them. Header must be `full_name | value_type | read_schema_from_file | input | index | mode | group | comment | tags | output` (data rows leave tags/output blank). (`write_tables_index`.)
2. **`__beans__.xlsx` / `__enums__.xlsx` need their full required column headers**, not just `##var`. Beans: `full_name, parent, valueType, sep, alias, comment, tags, group`. Enums: `full_name, comment, flags, group, tags, unique`. (`write_beans_index` / `write_enums_index`.)
3. **Auto table-importer must be disabled** — Luban auto-scans `Datas/#*.xlsx` and re-registers them, duplicating the explicit `__tables__` entries (`type 'SkinAsset' duplicate`). Fix: `"xargs": ["tableImporter.name=none"]` in `luban.conf`.
4. **`class` is a rejected field name** (C# reserved keyword) — Luban errors `Action::class contains preserved keyword`. Fix: converter `FIELD_RENAME = {"Action": {"class": "category"}}` (aligns with the 2a proto decision). Generated property is `Category`. **β impact:** Action call sites using `.Class` become `.Category`.
5. **Committed Luban path is lowercase** `table/tools/Luban/` (matches the repo's existing `table/tools/` dir). gen scripts use `tools/Luban/Luban.dll`.

Verified working output: client target generates `{Character,Skin,SkinAsset,Action,Item}.cs` + `Tb*.cs` + `Tables.cs` (namespace `LOP.MasterData`) + `tb*.bytes`; server target omits `SkinAsset` and the `Description` field entirely (group `c` split confirmed). Branch `feature/master-data-luban-bootstrap`, commits `31d927b` → `4090992` → `3a3bffa` → `10d7f1a`.

> The Task 3/4/5 code blocks below show the original plan; the as-built converter/luban.conf differ per the 5 notes above. Treat the committed files as authoritative.

---

## Repos & branches

- **Primary work: `infrastructure` repo** — `C:/Users/re5na/workspace/LOP/infrastructure`, all `table/` changes. Create branch `feature/master-data-luban-bootstrap` there.
- **This plan doc + spec + topology doc: `LeagueOfPhysical-Client`** — already on worktree branch `worktree-feature+master-data-luban-migration` (Task 9 updates the topology doc here).
- Tasks 1–8 run in the **infrastructure repo**. Task 9 runs in the **client worktree**. Each task states its repo explicitly.

## Prerequisites (verify once before Task 1)

- [ ] **.NET 8 runtime present.** Run: `dotnet --version` → expect `8.x` (or higher). If absent, install .NET 8 SDK/runtime before proceeding.
- [ ] **Python + openpyxl.** Run: `python -c "import openpyxl; print(openpyxl.__version__)"`. If it errors, run `pip install openpyxl`.

## File Structure (created/modified in this slice)

**infrastructure repo — created:**
- `table/Tools/Luban/…` — committed Luban v4.9.0 dll toolchain
- `table/Datas/__tables__.xlsx` — table index (Excel-embedded mode)
- `table/Datas/#Character.xlsx`, `#Skin.xlsx`, `#SkinAsset.xlsx`, `#Action.xlsx`, `#Item.xlsx` — converted tables
- `table/Datas/__beans__.xlsx`, `table/Datas/__enums__.xlsx` — empty placeholders (schemaFiles canonical set)
- `table/luban.conf` — groups + targets
- `table/gen.sh`, `table/gen.bat` — gen scripts (scratch output in α)
- `table/scripts/convert_source_to_luban.py` — one-shot source→Luban converter

**infrastructure repo — removed (2a protobuf assets):**
- `table/proto/` (6 `.proto`), `table/tools/protoc-28.2-win64/`, `table/exporter/` (Python), `table/scripts/compile_masterdata_protos.sh`, `table/client_column_mapping.yaml`, `table/server_column_mapping.yaml`

**client worktree — modified:**
- `docs/lop-repo-topology.md` — MasterData packages: protobuf → Luban

---

## Task 1: infrastructure feature branch + clean baseline

**Repo:** infrastructure

- [ ] **Step 1: Create and switch to the feature branch**

Run:
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git checkout -b feature/master-data-luban-bootstrap
```
Expected: `Switched to a new branch 'feature/master-data-luban-bootstrap'`

- [ ] **Step 2: Confirm clean baseline + current 2a layout**

Run:
```bash
git status --short
ls table/
```
Expected: clean status; `table/` lists `client_column_mapping.yaml exporter proto scripts server_column_mapping.yaml source tools`.

---

## Task 2: Commit Luban v4.9.0 toolchain

**Repo:** infrastructure
**Files:** Create `table/Tools/Luban/…`

- [ ] **Step 1: Download Luban v4.9.0 release archive**

Run:
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
mkdir -p Tools
gh release download v4.9.0 --repo focus-creative-games/luban --pattern "Luban.7z" --dir Tools
ls -la Tools/Luban.7z
```
Expected: `Tools/Luban.7z` exists (several MB).

- [ ] **Step 2: Extract to `Tools/Luban/`**

Run (7-Zip; on Windows `7z` ships with Git-for-Windows or install p7zip):
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table/Tools
7z x Luban.7z -oLuban
rm Luban.7z
ls Luban/Luban.dll
```
Expected: `Luban/Luban.dll` exists (alongside `Luban.Core.dll`, `Luban.CSharp.dll`, etc.).
If `7z` is unavailable, extract `Luban.7z` with any archiver so that `Tools/Luban/Luban.dll` results.

- [ ] **Step 3: Verify the tool runs**

Run:
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
dotnet Tools/Luban/Luban.dll --help
```
Expected: Luban CLI help text printed (options like `-t`, `-c`, `-d`, `--conf`). No .NET error.

- [ ] **Step 4: Commit**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git add table/Tools/Luban
git commit -m "build(table): commit Luban v4.9.0 toolchain"
```

---

## Task 3: Author the source→Luban conversion script

**Repo:** infrastructure
**Files:** Create `table/scripts/convert_source_to_luban.py`

This one-shot script reads the existing `table/source/*.xlsx` (layout: row 1 = types, row 2 = column names, rows 3+ = data) and writes Luban Excel-embedded tables under `table/Datas/`, plus `__tables__.xlsx` and empty `__beans__.xlsx`/`__enums__.xlsx`.

- [ ] **Step 1: Write the conversion script**

```python
# table/scripts/convert_source_to_luban.py
# One-shot: existing source/*.xlsx (row1=types, row2=names, row3+=data)
#   -> Luban Excel-embedded Datas/#Name.xlsx (##var/##type/##group/## + data)
#   -> Datas/__tables__.xlsx (read_schema_from_file=TRUE)
import os
from openpyxl import load_workbook, Workbook

SRC = os.path.join(os.path.dirname(__file__), "..", "source")
OUT = os.path.join(os.path.dirname(__file__), "..", "Datas")

# Per-table config:
#   value_type : Luban bean class name (PascalCase)
#   index      : key field (snake_case column in source)
#   table_group: "" => all groups; "c" => whole table client-only
#   field_groups: {column_name: "c"} per-field overrides (blank = all groups)
TABLES = {
    "Character": {"value_type": "Character", "index": "code", "table_group": "",
                  "field_groups": {"description": "c"}},
    "Skin":      {"value_type": "Skin", "index": "code", "table_group": "",
                  "field_groups": {"description": "c"}},
    "SkinAsset": {"value_type": "SkinAsset", "index": "code", "table_group": "c",
                  "field_groups": {}},
    "Action":    {"value_type": "Action", "index": "code", "table_group": "",
                  "field_groups": {"description": "c"}},
    "Item":      {"value_type": "Item", "index": "code", "table_group": "",
                  "field_groups": {"description": "c"}},
}

def read_source(name):
    wb = load_workbook(os.path.join(SRC, f"{name}.xlsx"), data_only=True)
    ws = wb.active
    rows = [[c.value for c in row] for row in ws.iter_rows()]
    types = rows[0]
    names = rows[1]
    # trim trailing all-None columns
    ncol = max((i + 1 for i, n in enumerate(names) if n not in (None, "")), default=0)
    types = [("" if t is None else str(t)) for t in types[:ncol]]
    names = [("" if n is None else str(n)) for n in names[:ncol]]
    data = [[("" if v is None else v) for v in r[:ncol]] for r in rows[2:]
            if any(v not in (None, "") for v in r[:ncol])]
    return names, types, data

def write_table(name, cfg):
    names, types, data = read_source(name)
    groups = [cfg["field_groups"].get(n, "") for n in names]
    wb = Workbook(); ws = wb.active
    ws.append(["##var"]   + names)
    ws.append(["##type"]  + types)
    ws.append(["##group"] + groups)
    ws.append(["##"]      + names)        # human comment row
    for row in data:
        ws.append([""] + list(row))       # data rows: col A empty
    wb.save(os.path.join(OUT, f"#{name}.xlsx"))

def write_tables_index():
    wb = Workbook(); ws = wb.active
    # Luban 4.9.0's built-in __TableRecord__ bean REQUIRES the `output` column
    # (and accepts `tags`). The canonical header is:
    #   full_name | value_type | read_schema_from_file | input | index | mode | group | comment | tags | output
    ws.append(["##var", "full_name", "value_type", "read_schema_from_file",
               "input", "index", "mode", "group", "comment", "tags", "output"])
    for name, cfg in TABLES.items():
        ws.append(["", f"Tb{cfg['value_type']}", cfg["value_type"], "TRUE",
                   f"#{name}.xlsx", cfg["index"], "map", cfg["table_group"], name, "", ""])
    wb.save(os.path.join(OUT, "__tables__.xlsx"))

def write_empty(fname, header_tag):
    wb = Workbook(); ws = wb.active
    ws.append([header_tag])
    wb.save(os.path.join(OUT, fname))

def main():
    os.makedirs(OUT, exist_ok=True)
    for name, cfg in TABLES.items():
        write_table(name, cfg)
        print(f"[OK] Datas/#{name}.xlsx")
    write_tables_index();           print("[OK] Datas/__tables__.xlsx")
    write_empty("__beans__.xlsx", "##var"); print("[OK] Datas/__beans__.xlsx")
    write_empty("__enums__.xlsx", "##var"); print("[OK] Datas/__enums__.xlsx")
    print("[DONE]")

if __name__ == "__main__":
    main()
```

- [ ] **Step 2: Run the conversion script**

Run:
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
python scripts/convert_source_to_luban.py
```
Expected: `[OK] Datas/#Character.xlsx` … `[OK] Datas/__enums__.xlsx`, `[DONE]`.

- [ ] **Step 3: Verify generated table structure (spot-check Character)**

Run:
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
python -c "
from openpyxl import load_workbook
ws = load_workbook('Datas/#Character.xlsx').active
for r in list(ws.iter_rows(values_only=True))[:4]:
    print([c for c in r if c is not None])
"
```
Expected (order matches source columns):
```
['##var', 'code', 'name', 'speed', 'jump_power', 'description', 'default_skin_code']
['##type', 'string', 'string', 'float', 'float', 'string', 'string']
['##group', 'c']        # only 'description' carries c; blank cells dropped by the print filter
['##', 'code', 'name', 'speed', 'jump_power', 'description', 'default_skin_code']
```
(The `##group` row has `c` under the `description` column; other group cells are blank = all groups.)

- [ ] **Step 4: Commit**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git add table/scripts/convert_source_to_luban.py table/Datas
git commit -m "feat(table): convert source Excel to Luban Excel-embedded schema"
```

---

## Task 4: Write luban.conf

**Repo:** infrastructure
**Files:** Create `table/luban.conf`

- [ ] **Step 1: Write luban.conf**

```jsonc
{
    "groups": [
        { "names": ["c"], "default": true },
        { "names": ["s"], "default": true }
    ],
    "schemaFiles": [
        { "fileName": "Datas/__tables__.xlsx", "type": "table" },
        { "fileName": "Datas/__beans__.xlsx",  "type": "bean"  },
        { "fileName": "Datas/__enums__.xlsx",  "type": "enum"  }
    ],
    "dataDir": "Datas",
    "targets": [
        { "name": "client", "manager": "Tables", "groups": ["c"], "topModule": "LOP.MasterData" },
        { "name": "server", "manager": "Tables", "groups": ["s"], "topModule": "LOP.MasterData" }
    ]
}
```

- [ ] **Step 2: Commit**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git add table/luban.conf
git commit -m "feat(table): add luban.conf (groups c/s, client/server targets)"
```

---

## Task 5: Write gen scripts (scratch output) and run code+data generation

**Repo:** infrastructure
**Files:** Create `table/gen.sh`, `table/gen.bat`

Slice α outputs to a scratch dir (`./_gen_scratch/{client,server}`) — NOT the Unity packages. Package wiring happens in Slice β/γ.

- [ ] **Step 1: Write gen.sh**

```bash
#!/bin/bash
# Slice α: generate to scratch dirs for verification (packages wired in β/γ).
set -e
cd "$(dirname "$0")"

LUBAN="Tools/Luban/Luban.dll"
SCRATCH="_gen_scratch"
rm -rf "$SCRATCH"

for TARGET in client server; do
  echo "[gen] target=$TARGET"
  dotnet "$LUBAN" \
    -t "$TARGET" \
    -c cs-bin \
    -d bin \
    --conf luban.conf \
    -x outputCodeDir="$SCRATCH/$TARGET/cs" \
    -x outputDataDir="$SCRATCH/$TARGET/bytes"
done

echo "[done] output under $SCRATCH/{client,server}/{cs,bytes}"
```

- [ ] **Step 2: Write gen.bat (Windows convenience, same args)**

```bat
@echo off
setlocal
cd /d %~dp0
set LUBAN=Tools\Luban\Luban.dll
set SCRATCH=_gen_scratch
if exist %SCRATCH% rmdir /s /q %SCRATCH%

for %%T in (client server) do (
  echo [gen] target=%%T
  dotnet %LUBAN% -t %%T -c cs-bin -d bin --conf luban.conf ^
    -x outputCodeDir=%SCRATCH%\%%T\cs ^
    -x outputDataDir=%SCRATCH%\%%T\bytes
)
echo [done]
```

- [ ] **Step 3: Run gen**

Run:
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
bash gen.sh
```
Expected: `[gen] target=client`, `[gen] target=server`, `[done]`, no errors. (Luban prints a success banner ending in `bye~`.)

- [ ] **Step 4: Verify outputs exist for both targets**

Run:
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
ls _gen_scratch/client/cs && echo "---" && ls _gen_scratch/client/bytes && echo "===SERVER===" && ls _gen_scratch/server/cs && ls _gen_scratch/server/bytes
```
Expected: client `cs` has `Tables.cs` + per-table `.cs` (Character, Skin, SkinAsset, Action, Item, TbCharacter, …); client `bytes` has 5 `.bytes`; server `cs`/`bytes` present but **without SkinAsset**.

- [ ] **Step 5: Add scratch dir to infrastructure .gitignore**

Run:
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
grep -qxF 'table/_gen_scratch/' .gitignore 2>/dev/null || echo 'table/_gen_scratch/' >> .gitignore
```

- [ ] **Step 6: Commit (scripts only — scratch is ignored)**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git add table/gen.sh table/gen.bat .gitignore
git commit -m "feat(table): add Luban gen scripts (scratch output)"
```

---

## Task 6: Verify group (c/s) split in generated output

**Repo:** infrastructure
This is the security-critical verification: client-only fields/tables must be ABSENT on the server side.

- [ ] **Step 1: Server `Character` must NOT contain `Description` (client-only field)**

Run:
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
grep -rl "Description" _gen_scratch/server/cs/ || echo "NO_DESCRIPTION_ON_SERVER"
grep -rl "Description" _gen_scratch/client/cs/ && echo "DESCRIPTION_PRESENT_ON_CLIENT"
```
Expected: first line prints `NO_DESCRIPTION_ON_SERVER`; second prints a client file path + `DESCRIPTION_PRESENT_ON_CLIENT`.

- [ ] **Step 2: `SkinAsset` table (client-only) must NOT exist on the server**

Run:
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
ls _gen_scratch/server/cs/ | grep -i skinasset && echo "LEAK" || echo "NO_SKINASSET_ON_SERVER"
ls _gen_scratch/client/cs/ | grep -i skinasset && echo "SKINASSET_ON_CLIENT_OK"
```
Expected: server prints `NO_SKINASSET_ON_SERVER`; client prints a `SkinAsset`/`TbSkinAsset` file + `SKINASSET_ON_CLIENT_OK`.

- [ ] **Step 3: Confirm the generated namespace is `LOP.MasterData` and `Tables` manager exists**

Run:
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
grep -r "namespace LOP.MasterData" _gen_scratch/client/cs/Tables.cs && grep -E "TbCharacter|TbAction" _gen_scratch/client/cs/Tables.cs
```
Expected: namespace line found; `Tables` exposes `TbCharacter`, `TbAction`, etc.

> If any check fails, the group config in `Datas/__tables__.xlsx` (table_group) or the `##group` rows in `#*.xlsx` is wrong — fix via `scripts/convert_source_to_luban.py` config and re-run Task 3 Step 2 + Task 5 Step 3. No commit for this task (verification only).

---

## Task 7: Remove 2a protobuf assets

**Repo:** infrastructure

- [ ] **Step 1: Remove proto schema, protoc toolchain, Python exporter, compile script, yaml mappings**

Run:
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git rm -r table/proto table/tools/protoc-28.2-win64 table/exporter
git rm table/scripts/compile_masterdata_protos.sh table/client_column_mapping.yaml table/server_column_mapping.yaml
```
Expected: each path staged for deletion.

- [ ] **Step 2: Verify `table/` now reflects the Luban layout**

Run:
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
ls table/
```
Expected: `Datas Tools gen.bat gen.sh luban.conf scripts source` (and ignored `_gen_scratch`). `proto`, `exporter`, `tools/protoc-28.2-win64`, the yamls are gone. (`source/` kept as the human-authored origin of the one-shot conversion; designers now edit `Datas/` going forward — note in commit.)

- [ ] **Step 3: Commit**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git commit -m "chore(table): remove 2a protobuf assets (proto/protoc/exporter/yaml)"
```

---

## Task 8: Confirm Unity runtime is untouched (both projects still build on CSV)

**Repo:** infrastructure (verification spans LOP-Client / LOP-Server checkouts)
Slice α must not change any runtime. This task is a guard, not a code change.

- [ ] **Step 1: Confirm no MasterData package or Unity script was modified by Slice α**

Run:
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git log --oneline feature/master-data-luban-bootstrap --stat | grep -E "LeagueOfPhysical-(Client|Server|MasterData)" && echo "UNEXPECTED_RUNTIME_CHANGE" || echo "RUNTIME_UNTOUCHED"
```
Expected: `RUNTIME_UNTOUCHED` (all α commits are under `infrastructure/table/`).

- [ ] **Step 2: Manual editor check (record result)**

Open the LOP-Client Unity editor, let it compile. Then open LOP-Server editor.
Expected: both compile with **zero new errors**, MasterData still loads from existing `StreamingAssets/MasterData/*.csv` (unchanged). Note the result in the task checkbox.

---

## Task 9: Update topology doc (client worktree)

**Repo:** LeagueOfPhysical-Client (worktree `worktree-feature+master-data-luban-migration`)
**Files:** Modify `docs/lop-repo-topology.md`

- [ ] **Step 1: Update the MasterData package descriptions**

In `docs/lop-repo-topology.md`, change MasterData-Client/Server package descriptions from "Protobuf schema(`.cs` from protoc) + 데이터(`.bin`)" to Luban-based wording, e.g.:

> **LeagueOfPhysical-MasterData-Client** … **클라 전용 MasterData** — Luban 생성 schema(`.cs`) + 데이터(`.bytes`). description 등 클라 전용 컬럼 포함(Luban group `c`). Luban 전환 spec(`2026-06-03-master-data-luban-migration-design.md`)으로 부트스트랩.

- [ ] **Step 2: Update the `infrastructure` row + dependency note**

Change the `infrastructure` repo row's MasterData pipeline description from "Excel → `.proto` → protoc → `.cs` + `.bin`" to "Excel(Excel-embedded) → **Luban** → `.cs` + `.bytes`", and replace `Google.Protobuf via UnityNuGet` MasterData usage with `com.code-philosophy.luban` where it concerns MasterData. Add a note under Open Decisions that the protobuf/.proto MasterData path was superseded by Luban on 2026-06-03.

- [ ] **Step 3: Commit (client worktree)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/feature+master-data-luban-migration"
git add docs/lop-repo-topology.md
git commit -m "docs(topology): MasterData packages protobuf -> Luban (Slice α)"
```

---

## Slice α Done — Definition of Done

- [ ] `dotnet Tools/Luban/Luban.dll` runs (.NET 8); toolchain committed under `table/Tools/Luban/`.
- [ ] `bash table/gen.sh` produces `.cs` + `.bytes` for both `client` and `server` targets with no errors.
- [ ] Group split verified: server output lacks `Description` and lacks `SkinAsset`; client has both.
- [ ] Generated namespace is `LOP.MasterData`, manager `Tables` exposes `TbCharacter`/`TbAction`/….
- [ ] 2a protobuf assets removed; `table/` reflects the Luban layout.
- [ ] No Unity runtime changed; both editors compile on existing CSV.
- [ ] Topology doc updated.

## Out of scope (Slice β/γ — not this plan)

- Package wiring (`outputCodeDir`/`outputDataDir` → MasterData-Client/Server packages), `com.code-philosophy.luban` manifest entries, `Generated.asmdef` Luban ref.
- `LOPMasterData` thin wrapper, call-site/DI/loader switch, removal of hand POCO + CSV + GameFramework `IMasterData*`.
- `ref`/`range`/`required` validators in the Excel-embedded schema (the `##type`-cell validator syntax is unconfirmed for v4.9.0 — confirm against the Luban `define` doc and add in a β refinement; spec Open Decisions tracks this).

---

## Self-Review

**Spec coverage (Slice α section of the spec):**
- Tools/Luban committed → Task 2 ✅
- Excel 5종 Excel-embedded + `__tables__` → Task 3 ✅
- `luban.conf` (groups c/s, targets, topModule) → Task 4 ✅
- gen scripts, scratch output → Task 5 ✅
- group 분기 검증 (server no description / no SkinAsset) → Task 6 ✅
- 2a 자산 제거 → Task 7 ✅
- 런타임 무변경 (CSV 유지) → Task 8 ✅
- 토폴로지 doc 갱신 → Task 9 ✅
- `ref`(FK) 검증 — **deferred** (validator cell syntax unconfirmed; moved to β per spec Open Decisions). Noted in Out-of-scope. This is the one intentional deviation from the spec's α verification list.

**Placeholder scan:** No TBD/TODO. The one uncertainty (validator syntax) is explicitly deferred with rationale, not left as a vague step.

**Type/name consistency:** `Tb<ValueType>` full_name (Task 3) matches the `Tables.TbCharacter` checks (Task 6) and the wrapper's `md.Tables.TbCharacter` (spec). `topModule=LOP.MasterData` consistent across Tasks 4/6 and spec. Scratch dir name `_gen_scratch` consistent across Tasks 5/6/8.
