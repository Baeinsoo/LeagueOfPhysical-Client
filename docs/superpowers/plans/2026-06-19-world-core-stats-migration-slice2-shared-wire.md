# World Core Stats Migration — Slice 2 (Shared / Wire) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `CharacterCreationData` wire 메시지에 4 stat 필드(strength/dexterity/intelligence/vitality)를 추가하고 proto를 재생성한다 — 스폰/늦은접속 시 World.Stats 시드를 위한 wire 채널 확보(재접 버그 수정의 토대).

**Architecture:** LOP-Shared 패키지의 `Protos/CharacterCreationData.proto`에 4 int32 필드를 append하고, `compile_protos.sh`(protoc만, in-place 덮어쓰기)로 생성 `.cs`를 갱신한다. **전체 orchestrator(`generate_protos.sh`)는 쓰지 않는다** — 그건 Protobuf 폴더를 `rm -rf`해 모든 생성 파일의 `.meta` GUID를 churn시킨다. 새 *메시지*가 아니라 기존 메시지에 *필드만* 추가하므로 `compile_protos.sh`가 `CharacterCreationData.cs`만 in-place 덮어써 GUID 보존 + 최소 diff. 추가만이라 비파괴(기존 필드/번호 불변, proto3 default=0).

**Tech Stack:** protobuf3, protoc-28.2-win64(Shared `Tools/`에 커밋됨), bash(Git Bash로 .sh 실행). 생성 `.cs`는 Shared `Runtime.Generated/Scripts/Protobuf/`에 커밋됨. 클·서는 Shared를 `file:` UPM으로 공유.

**Related spec:** `docs/superpowers/specs/2026-06-19-world-core-stats-migration-design.md` (Slice 2)

**Resolved Unity instances (매 UnityMCP 호출에 명시 — HTTP stateless):**
- Client: `LeagueOfPhysical-Client@de70658b9450cbb4`
- Server: `LeagueOfPhysical-Server@f99391fa2dbaaf3c`

---

## File Structure

LOP-Shared repo: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Shared`.

- **Modify:** `Protos/CharacterCreationData.proto` — 4 int32 stat 필드 추가(번호 10–13).
- **Regenerate (in-place):** `Runtime.Generated/Scripts/Protobuf/CharacterCreationData.cs` — `compile_protos.sh` 산출(`.meta` 불변).
- 무변경: 다른 proto/생성 파일, `MessageIds.cs`, `MessageInitializer.cs`, `.IMessage.cs`(CharacterCreationData는 wire 메시지 아닌 nested data라 IMessage 없음).

> **현재 proto(참고)** — 필드 9까지:
> ```proto
> message CharacterCreationData {
>     BaseEntityCreationData base_entity_creation_data = 1;
>     string character_code = 2;
>     string visual_id = 3;
>     int32 max_HP = 4;
>     int32 current_HP = 5;
>     int32 max_MP = 6;
>     int32 current_MP = 7;
>     int32 level = 8;
>     int64 current_exp = 9;
> }
> ```

---

## Task 0: Shared 피처 브랜치 생성

**Files:** (git 작업만)

- [ ] **Step 1: 현재 상태 확인**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" status --short --branch | head -3
```
Expected: `main`, 깨끗(또는 무관 변경 — 그대로 둠).

- [ ] **Step 2: 피처 브랜치 생성**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" checkout -b feature/world-core-stats-migration
```
Expected: `Switched to a new branch 'feature/world-core-stats-migration'`.

---

## Task 1: proto에 4 stat 필드 추가

**Files:**
- Modify: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Shared\Protos\CharacterCreationData.proto`

- [ ] **Step 1: 필드 추가**

`int64 current_exp = 9;` 줄 다음, 닫는 `}` 앞에 추가:
```proto
	int32 strength = 10;
	int32 dexterity = 11;
	int32 intelligence = 12;
	int32 vitality = 13;
```
(들여쓰기는 기존 파일 스타일[탭]에 맞춤. 필드 번호 10–13은 기존 1–9 다음 — 기존 번호 변경 금지.)

---

## Task 2: proto 재생성 (compile_protos.sh — in-place)

**Files:**
- Regenerate: `Runtime.Generated/Scripts/Protobuf/CharacterCreationData.cs`

- [ ] **Step 1: compile_protos.sh 실행**

Run (Scripts 디렉터리에서 — 스크립트가 상대경로 사용):
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Scripts" && ./compile_protos.sh
```
Expected: `Successfully compiled N .proto file(s).` 출력, 에러 0. (protoc-28.2-win64 exe가 Git Bash에서 실행됨. 만약 Bash 환경에서 win64 exe 실행이 막히면 사용자에게 `! cd .../Scripts && ./compile_protos.sh` 실행을 요청.)

- [ ] **Step 2: diff가 CharacterCreationData만인지 검증 (GUID churn 없음)**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" status --short
```
Expected: **정확히 2개 파일만** 변경 —
```
 M Protos/CharacterCreationData.proto
 M Runtime.Generated/Scripts/Protobuf/CharacterCreationData.cs
```
다른 생성 `.cs`나 `.meta`가 나타나면 중단·조사(protoc 버전 drift 또는 잘못 orchestrator 실행). `.meta` 변경은 0이어야 함(in-place 덮어쓰기라 GUID 보존).

- [ ] **Step 3: 생성된 필드 확인**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" grep -n "Strength\|Dexterity\|Intelligence\|Vitality" -- Runtime.Generated/Scripts/Protobuf/CharacterCreationData.cs | head
```
Expected: 4개 stat 프로퍼티(`public int Strength`, `Dexterity`, `Intelligence`, `Vitality`)가 생성됨.

---

## Task 3: 클·서 컴파일 검증 (file: 공유 비파괴)

**Files:** (검증만)

- [ ] **Step 1: 클라 인스턴스 컴파일**

Run (UnityMCP):
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`

Expected: 에러 0건. (필드 추가는 기존 코드에 비파괴 — 아직 새 필드를 읽는 코드 없음.)

- [ ] **Step 2: 서버 인스턴스 컴파일**

Run (UnityMCP):
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`

Expected: 에러 0건.

---

## Task 4: 커밋 (Shared)

**Files:** (git)

- [ ] **Step 1: 변경 스테이징 + 커밋**

```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" add Protos/CharacterCreationData.proto Runtime.Generated/Scripts/Protobuf/CharacterCreationData.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" commit -m "feat(wire): add 4 primary stat fields to CharacterCreationData (Stats migration seed channel)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 2: 커밋 확인**

```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" show --stat HEAD | head -12
```
Expected: 2개 파일만(proto + 생성 .cs). 다른 파일/.meta 포함되면 중단·조사.

---

## Slice 2 완료 기준

- [ ] Shared `feature/world-core-stats-migration`에 커밋 1개(proto + 생성 .cs, 2파일).
- [ ] GUID churn 0(`.meta` 변경 없음), 다른 생성 파일 변경 없음.
- [ ] `CharacterCreationData.cs`에 Strength/Dexterity/Intelligence/Vitality int 프로퍼티 생성됨.
- [ ] 클·서 양쪽 컴파일 0에러.
- [ ] 런타임 동작 변화 0(새 필드 미사용 — 기본 0; 서버 fill/클라 read는 Slice 3·4).

다음: **Slice 3 (서버)** — World.Stats 생성 시드 + combat read-flip + 할당 flip + CreationDataCreator fill + 도메인 struct/매퍼 +4필드 + DI + 레거시 제거. (별도 plan)
