# Entity View/Controller MVC 디커플 — Slice 3 (GameFramework 타입 삭제) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** GameFramework의 옛 MVC Entity 베이스 타입 4개(`IEntityView`, `IEntityController`, `MonoEntityView<,>`, `MonoEntityController<>`)를 `.meta`와 함께 삭제하고, 클·서 양쪽이 컴파일·플레이됨을 확인한다.

**Architecture:** 단순 삭제 게이트. Slice 1(클라)·Slice 2(서버)에서 모든 소비자가 이미 GF MVC 베이스 의존을 끊었으므로, 이 4개 타입은 더 이상 어디서도 참조되지 않는다. GameFramework는 클·서가 `file:` 패키지 참조로 보므로, 삭제하면 양쪽 Unity가 즉시 반영한다.

**Tech Stack:** Unity 패키지(C#), git. UnityMCP(양쪽 컴파일 검증).

**Spec:** `docs/superpowers/specs/2026-06-14-entity-view-mvc-decouple-design.md` (LOP-Client 저장소)

**저장소:** `C:/Users/re5na/workspace/LOP/GameFramework`

---

## ⚠️ 선행 조건 (HARD PRECONDITION)

이 Slice는 **Slice 1과 Slice 2가 모두 완료된 뒤에만** 실행한다. 둘 중 하나라도 소비자가 아직 GF MVC 베이스를 상속하고 있으면, 삭제 즉시 그 쪽이 컴파일 깨진다.

**실행 전 확인 (전수 검색):** 두 저장소 어디에도 `MonoEntityView` / `MonoEntityController` / `IEntityView` / `IEntityController` 참조가 **GameFramework 정의부 4파일 외에는 0건**이어야 한다.

```bash
# 클·서 소스에서 잔존 참조 확인 (0건 기대)
grep -rn "MonoEntityView\|MonoEntityController\|IEntityView\|IEntityController" \
  "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts" \
  "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts"
```
출력이 비어 있어야 진행한다. (Grep 도구로 대체 가능: pattern `MonoEntityView|MonoEntityController|IEntityView|IEntityController`, 두 Assets/Scripts 경로.)

---

## File Structure

GameFramework 저장소에서 **삭제만** (수정/생성 없음):

| 삭제 파일 (각 `.meta` 동반) |
|---|
| `Runtime/Scripts/Entity/IEntityView.cs` (+ `.meta`) |
| `Runtime/Scripts/Entity/IEntityController.cs` (+ `.meta`) |
| `Runtime/Scripts/Entity/MonoEntityView.cs` (+ `.meta`) |
| `Runtime/Scripts/Entity/MonoEntityController.cs` (+ `.meta`) |

> `MonoEntityView`/`MonoEntityController`는 *추상 제네릭* 베이스라 씬·prefab에 직접 부착되지 않는다(부착되는 건 구상 `LOPEntityView` 등, 그 GUID는 그대로 유지). `IEntityView`/`IEntityController`는 인터페이스. 따라서 prefab/scene의 직렬화 참조가 이 4개 `.meta` GUID를 가리킬 수 없어 삭제 안전(spec GUID 정책).

---

## Verification Procedure (양쪽 컴파일 — Task 2에서 수행)

> UnityMCP 툴 deferred — 먼저 로드: `ToolSearch("select:refresh_unity,read_console")` + `ReadMcpResourceTool`.

1. `mcpforunity://instances`를 읽어 두 인스턴스 full id를 얻는다:
   - 클라: `name == "LeagueOfPhysical-Client"` (작성 시점 예 `LeagueOfPhysical-Client@de70658b9450cbb4`)
   - 서버: `name == "LeagueOfPhysical-Server"` (작성 시점 예 `LeagueOfPhysical-Server@f99391fa2dbaaf3c`)
   - hash는 바뀔 수 있으니 매번 조회.
2. 각 인스턴스에 대해:
   - `refresh_unity(unity_instance="<id>", mode="force", scope="scripts", compile="request", wait_for_ready=true)`
   - `read_console(unity_instance="<id>", action="get", types=["error"], count="40", format="plain")`
   - **기대:** 양쪽 모두 컴파일 에러 **0건** (특히 "type or namespace 'MonoEntityView' could not be found" 류가 없어야 함).

---

## Task 1: GameFramework MVC 타입 4개 삭제

**Files (GameFramework 저장소):**
- Delete: `Runtime/Scripts/Entity/IEntityView.cs` (+ `.meta`)
- Delete: `Runtime/Scripts/Entity/IEntityController.cs` (+ `.meta`)
- Delete: `Runtime/Scripts/Entity/MonoEntityView.cs` (+ `.meta`)
- Delete: `Runtime/Scripts/Entity/MonoEntityController.cs` (+ `.meta`)

- [ ] **Step 0: 선행 조건 재확인** — 위 "HARD PRECONDITION"의 grep이 0건인지 확인. 0건이 아니면 STOP하고 보고(아직 Slice 1/2 미완).

- [ ] **Step 1: GameFramework 저장소에서 피처 브랜치 보장**

GameFramework는 현재 `main`이다. main 직접 커밋 금지(CLAUDE.md). 피처 브랜치로 전환:
```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git checkout -b feature/entity-view-mvc-removal
```
(이미 적절한 피처 브랜치에 있으면 이 단계는 생략.)

- [ ] **Step 2: 4개 `.cs` + 4개 `.meta` 삭제**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git rm Runtime/Scripts/Entity/IEntityView.cs Runtime/Scripts/Entity/IEntityView.cs.meta \
       Runtime/Scripts/Entity/IEntityController.cs Runtime/Scripts/Entity/IEntityController.cs.meta \
       Runtime/Scripts/Entity/MonoEntityView.cs Runtime/Scripts/Entity/MonoEntityView.cs.meta \
       Runtime/Scripts/Entity/MonoEntityController.cs Runtime/Scripts/Entity/MonoEntityController.cs.meta
```
Run: 위 명령. Expected: 8개 파일이 staged deletion으로 잡힘 (`git status`로 확인 — `deleted:` 8줄).

- [ ] **Step 3: Commit**

```bash
git commit -m "$(cat <<'EOF'
refactor(entity): remove legacy MVC entity view/controller base types

IEntityView/IEntityController/MonoEntityView/MonoEntityController are no
longer referenced — LOP client (slice 1) and server (slice 2) decoupled
their consumers to plain MonoBehaviour + ICleanup.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: 양쪽 컴파일 게이트

- [ ] **Step 1: 클·서 양쪽 컴파일 0에러 확인** — 위 "Verification Procedure"를 클라·서버 두 인스턴스에 대해 수행. 양쪽 `editor_state` ready + 콘솔 error 0건.

  실패 시(예: 어떤 참조가 남아 "could not be found"): 해당 저장소에서 잔존 참조를 grep으로 찾아 디커플(Slice 1/2 누락분)한 뒤 재확인. GameFramework 삭제 자체는 되돌리지 않는다(참조 쪽을 고친다).

---

## Task 3: 런타임 플레이 검증 (수동 — 사용자)

- [ ] **Step 1: 서버 호스트 + 클라 접속**으로 한 라운드 플레이.
- [ ] **Step 2: 확인**
  - 클라: 캐릭터 비주얼/애니메이션, 데미지 플로터, 네임플레이트 HP바, 카메라 추적 정상
  - 서버: AI 동작, 물리 동기화, 스폰/디스폰 정상
  - 양쪽 콘솔 에러 0건, `[World] ...` 로그 정상 (World Core 회귀 없음)
- [ ] **Step 3: 정리** — 통과 시 GameFramework `feature/entity-view-mvc-removal` 브랜치를 main에 `--no-ff` 머지(CLAUDE.md 워크플로우). 클·서 피처 브랜치도 각 저장소 정책대로 마무리.

> 통과하면 전체 작업(Slice 1+2+3) 완료 — GF MVC 베이스가 제거되고, 뷰/컨트롤러가 평범한 `MonoBehaviour, ICleanup`로 디커플된 "중간 상태"에 도달. 이후 World Core 완전 이행이 이 출발점에서 진행된다(spec의 궤적 참고).

---

## 진행

- [ ] Task 0/1 — 선행 조건 확인 + 피처 브랜치 + 4타입(.cs+.meta) 삭제 + 커밋
- [ ] Task 2 — 클·서 양쪽 컴파일 게이트
- [ ] Task 3 — 런타임 플레이 검증(수동) + 머지
