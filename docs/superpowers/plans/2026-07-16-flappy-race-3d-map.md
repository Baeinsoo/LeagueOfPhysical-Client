# Flappy Race — 3D 맵 씬 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 오리지널 플래피 버드 톤의 3D(2.5D 공유 평면) 횡스크롤 멀티 레이스 맵을 스케치팹→블렌더→유니티 파이프라인으로 유니티 씬 하나로 완성한다.

**Architecture:** 스케치팹에서 CC 라이선스 에셋을 받아 블렌더에서 톤/스케일/피벗을 정규화하고 FBX로 export, Unity MCP로 씬 계층을 구성하고 코스를 레이아웃한다. 단순 형태(파이프)는 블렌더에서 직접 모델링한다. 게임 로직은 범위 밖.

**Tech Stack:** Blender (MCP: `mcp__blender__*`), Unity 6 URP (MCP via `unity-mcp-skill`), Sketchfab(블렌더 MCP 다운로드), FBX export.

## Global Constraints

- **좌표 규약:** X = 진행 방향(횡스크롤), Y = 상하(점프), Z = 얇은 깊이(모든 플레이어 동일 평면). 카메라는 -Z에서 측면.
- **톤:** 오리지널 플래피 버드 — 밝은 초록 파이프 / 하늘색 배경 / 노랑 포인트. 심플 카툰.
- **렌더 파이프라인:** URP Lit 호환. metallic=0, 텍스처 sRGB, 피벗은 바닥 중심, 트랜스폼 적용(스케일 1).
- **씬 경로:** `LeagueOfPhysical-Client/Assets/Art/Scenes/FlappyRace.unity` (기존 `FlapWangMap.unity`는 참고용 스텁 — 바닥/floor.mat 재활용 가능).
- **에셋 저장 경로:** `LeagueOfPhysical-Client/Assets/Art/Environment/FlappyRace/` 하위에 정리.
- **라이선스:** 다운로드 에셋은 CC 계열만. 출처/저작자 attribution을 README에 기록.
- **검증 방식:** 각 단계는 뷰포트 스크린샷(`get_viewport_screenshot`) 또는 씬 계층 조회로 시각 확인. git 커밋 없음(저장소 아님).

---

### Task 1: 준비 — 도구 연결 확인 및 작업 폴더 생성

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Art/Environment/FlappyRace/` (폴더)
- Create: `LeagueOfPhysical-Client/Assets/Art/Environment/FlappyRace/README.md` (attribution 기록용)

**Interfaces:**
- Consumes: 없음
- Produces: 이후 모든 에셋이 저장될 폴더 경로 `Assets/Art/Environment/FlappyRace/`, attribution README.

- [ ] **Step 1: Blender MCP 연결 확인**

Run: `mcp__blender__get_scene_info`
Expected: 현재 Blender 씬 정보가 반환됨(에러 없이). 실패 시 Blender에서 MCP 애드온이 켜져 있는지 사용자에게 확인 요청.

- [ ] **Step 2: Sketchfab 사용 가능 여부 확인**

Run: `mcp__blender__get_sketchfab_status`
Expected: Sketchfab 통합이 enabled. disabled면 사용자에게 Blender MCP 설정에서 켜달라고 요청.

- [ ] **Step 3: Unity MCP 연결 확인**

`unity-mcp-skill`을 로드하고 스킬 지침에 따라 Unity Editor 연결 및 열린 프로젝트가 `LeagueOfPhysical-Client`인지 확인.
Expected: Unity MCP가 응답하고 프로젝트 경로가 일치.

- [ ] **Step 4: 작업 폴더 + README 생성**

`Assets/Art/Environment/FlappyRace/README.md`를 생성하고 아래 골격을 기록:
```markdown
# FlappyRace 맵 에셋

플래피 버드 톤 3D 횡스크롤 레이스 맵. 스케치팹→블렌더→유니티 파이프라인.

## Attribution
| 에셋 | 출처(Sketchfab URL) | 저작자 | 라이선스 |
|---|---|---|---|
| (다운로드 시 채움) | | | |
```

- [ ] **Step 5: 검증**

Unity MCP로 `Assets/Art/Environment/FlappyRace/` 폴더가 프로젝트에 존재하는지 조회.
Expected: 폴더와 README.md가 보임.

---

### Task 2: 파이프 장애물 모델링 + 프리팹

파이프는 형태가 단순해 스케치팹 의존 없이 블렌더에서 직접 모델링(톤 통일·경량화 유리).

**Files:**
- Create (Blender→export): `LeagueOfPhysical-Client/Assets/Art/Environment/FlappyRace/Pipe.fbx`
- Create (Unity): `LeagueOfPhysical-Client/Assets/Art/Environment/FlappyRace/Pipe.mat`
- Create (Unity): `LeagueOfPhysical-Client/Assets/Art/Environment/FlappyRace/PipePair.prefab`

**Interfaces:**
- Consumes: 폴더 경로(Task 1).
- Produces: `PipePair.prefab` — 위/아래 파이프 한 쌍을 담은 프리팹. 루트 피벗은 틈(gap) 중심의 X축 라인. 자식: `Pipe_Top`, `Pipe_Bottom`. 파이프 1개 지름 ≈ 2 유닛, 몸통 높이 ≈ 8 유닛, 갓(lip) 살짝 넓게. 이후 Task 7이 이 프리팹을 인스턴스화.

- [ ] **Step 1: 블렌더에서 파이프 모델 생성**

`mcp__blender__execute_blender_code`로 실린더 몸통 + 넓은 갓(lip)으로 파이프 1개를 만드는 파이썬 실행. 요구사항: 몸통 반지름 1.0, 높이 8, 상단에 반지름 1.2·높이 0.6 갓. 원점(피벗)은 파이프 하단 중심. 트랜스폼 적용(스케일 1). 오브젝트 이름 `Pipe`.

- [ ] **Step 2: 카툰 초록 머티리얼 지정**

동일 도구로 `Pipe`에 머티리얼 지정: base color 밝은 초록(약 RGB 0.20, 0.75, 0.25), metallic=0, roughness≈0.6. 갓 부분은 살짝 진한 초록으로 별도 슬롯(선택). URP Lit 호환을 위해 Principled BSDF 사용.

- [ ] **Step 3: 뷰포트 확인**

Run: `mcp__blender__get_viewport_screenshot`
Expected: 초록 파이프 1개가 보임(갓 있는 원기둥). 형태/색이 플래피 파이프처럼 보이는지 육안 확인.

- [ ] **Step 4: FBX export**

`mcp__blender__execute_blender_code`로 `Pipe` 오브젝트를 `Assets/Art/Environment/FlappyRace/Pipe.fbx`로 export(선택 오브젝트만, 스케일 적용, +Y up/-Z forward Unity 규약).
Expected: 파일 생성. 실패 시 절대경로/권한 확인.

- [ ] **Step 5: Unity에서 PipePair 프리팹 조립**

Unity MCP로: `Pipe.fbx` 임포트 확인 → 빈 GameObject `PipePair` 생성 → 자식으로 파이프 인스턴스 2개(`Pipe_Bottom` 바닥에서 위로, `Pipe_Top` 위에서 아래로 180° 회전) 배치, 사이에 틈(gap) ≈ 4 유닛 → `Pipe.mat`(초록) 적용 → `PipePair.prefab`로 저장. 루트 피벗 = gap 중심.

- [ ] **Step 6: 검증**

Unity MCP로 `PipePair.prefab` 계층 조회 + Scene 뷰 스크린샷.
Expected: 위/아래 초록 파이프 쌍, 가운데 통과 가능한 틈이 보임.

---

### Task 3: 지면(Ground) 타일

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Art/Environment/FlappyRace/Ground.prefab`
- Create: `LeagueOfPhysical-Client/Assets/Art/Environment/FlappyRace/Ground.mat`
- (재활용 가능) 기존 `Assets/Art/Scenes/floor.mat`

**Interfaces:**
- Consumes: 폴더 경로(Task 1).
- Produces: `Ground.prefab` — 잔디 윗면 + 흙 옆면 느낌의 넓적한 박스/플레인. 길이(X) ≈ 40 유닛(코스 방향으로 반복 배치 가능), 상단 Y=0. 이후 Task 6이 배치.

- [ ] **Step 1: Ground 머티리얼 생성**

Unity MCP로 `Ground.mat`(URP Lit) 생성: 윗면 톤은 잔디 초록(약 RGB 0.35, 0.70, 0.30), metallic=0, smoothness 낮게. (텍스처 없으면 단색 카툰 톤으로 시작, 필요 시 Task 8에서 폴리싱.)

- [ ] **Step 2: Ground 오브젝트 생성**

Unity MCP로 폭 40(X) × 두께 2(Y) × 깊이 6(Z)의 박스(Cube 스케일 조정) 생성, 상단이 Y=0에 오도록 배치, 이름 `Ground`. `Ground.mat` 적용.

- [ ] **Step 3: 프리팹화**

`Ground`를 `Ground.prefab`으로 저장.

- [ ] **Step 4: 검증**

Scene 뷰 스크린샷.
Expected: 초록 상단의 넓적한 지면 띠가 X축을 따라 길게 보임.

---

### Task 4: 배경 — 스카이/구름/도시 실루엣

**Files:**
- Create (다운로드 or 생성): `LeagueOfPhysical-Client/Assets/Art/Environment/FlappyRace/Cloud.fbx` (또는 빌보드 카드)
- Create: `LeagueOfPhysical-Client/Assets/Art/Environment/FlappyRace/CitySilhouette.prefab`
- Create: `LeagueOfPhysical-Client/Assets/Art/Skybox/FlappySky.mat` (기존 Skybox 폴더 재활용)

**Interfaces:**
- Consumes: 폴더 경로(Task 1).
- Produces: 하늘색 스카이박스 머티리얼 `FlappySky.mat`, 구름 오브젝트/프리팹, 도시 실루엣 프리팹 `CitySilhouette.prefab`(원경, 코스 뒤 -Z에 배치). 이후 Task 6이 배치.

- [ ] **Step 1: 스카이박스 머티리얼**

Unity MCP로 URP Skybox(Procedural 또는 gradient) 머티리얼 `FlappySky.mat` 생성: 하늘색(약 RGB 0.35, 0.65, 0.95) → 밝은 하단 그라데이션.

- [ ] **Step 2: 구름 확보 (스케치팹 검색 → 없으면 블렌더 생성)**

먼저 `mcp__blender__search_sketchfab_models`로 `"cartoon cloud low poly"` 검색, CC 라이선스 후보 확인. 적합하면 `mcp__blender__download_sketchfab_model`로 받고 README attribution 기록. 없으면 블렌더에서 메타볼/구 합성으로 뭉게구름 1~2종 생성 후 흰색 무광 머티리얼. `Cloud.fbx`로 export.

- [ ] **Step 3: 도시 실루엣 생성**

블렌더 또는 Unity에서 단순 박스들을 높이 다르게 나열해 원경 도시 실루엣 생성(단색 어두운 파랑/보라, 원경용). `CitySilhouette.prefab`로 저장.

- [ ] **Step 4: 검증**

Unity Scene 뷰에서 스카이박스 적용 + 구름/도시 배치 후 스크린샷.
Expected: 하늘색 배경, 흰 구름, 뒤쪽 도시 실루엣이 원경으로 보임.

---

### Task 5: 시작/결승 게이트 + placeholder 새 + 장식

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Art/Environment/FlappyRace/StartGate.prefab`
- Create: `LeagueOfPhysical-Client/Assets/Art/Environment/FlappyRace/FinishArch.prefab`
- Create: `LeagueOfPhysical-Client/Assets/Art/Environment/FlappyRace/Bush.prefab`
- Create: `LeagueOfPhysical-Client/Assets/Art/Environment/FlappyRace/ScoreRing.prefab`
- Create (다운로드): `LeagueOfPhysical-Client/Assets/Art/Characters/FlappyBird/Bird.fbx`

**Interfaces:**
- Consumes: 폴더 경로(Task 1).
- Produces: `StartGate.prefab`, `FinishArch.prefab`(아치형 구조물), `Bush.prefab`(장식), `ScoreRing.prefab`(코인/점수링), placeholder 새 프리팹 `Bird.prefab`. 이후 Task 6·7이 배치.

- [ ] **Step 1: 시작/결승 게이트 모델링**

블렌더에서 아치/게이트 구조(양 기둥 + 상단 보) 1종을 만들고, 색만 달리해 Start(초록 계열)·Finish(체크무늬/노랑) 변형. `StartGate`, `FinishArch`로 각각 export 또는 Unity에서 프리미티브 조합.

- [ ] **Step 2: placeholder 새 확보 (스케치팹)**

`mcp__blender__search_sketchfab_models`로 `"cartoon bird low poly"` 검색 → CC 라이선스 후보 미리보기(`get_sketchfab_model_preview`) → 적합한 것 다운로드, README attribution 기록. 블렌더에서 스케일/피벗 정규화(높이 ≈ 1 유닛, 피벗 몸통 중심) 후 `Bird.fbx` export.

- [ ] **Step 3: 장식 오브젝트**

블렌더/Unity에서 덤불(초록 뭉치) 1종, 점수링(노랑 토러스) 1종 생성 → `Bush.prefab`, `ScoreRing.prefab`.

- [ ] **Step 4: 프리팹화 + 검증**

각 오브젝트를 Unity 프리팹으로 저장 후 각각 Scene 뷰 스크린샷.
Expected: 게이트 2종, 새 1마리(카툰), 덤불, 점수링이 톤에 맞게 보임.

---

### Task 6: 씬 생성 + 환경 계층 구성

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Art/Scenes/FlappyRace.unity`

**Interfaces:**
- Consumes: `Ground.prefab`(T3), `FlappySky.mat`/`Cloud`/`CitySilhouette.prefab`(T4), `Bush.prefab`(T5).
- Produces: `FlappyRace.unity` 씬 파일 + 아래 빈 GameObject 계층. 이후 Task 7이 `---Course---`·`---Players---`를 채움.

- [ ] **Step 1: 새 씬 생성 + 계층 골격**

Unity MCP로 `FlappyRace.unity` 새 씬 생성. 빈 GameObject 그룹 생성: `---Environment---`, `---Course---`, `---Players---`, `---Lighting---`, `---Camera---`.

- [ ] **Step 2: 환경 배치**

`---Environment---` 아래: `Ground.prefab`를 X=0 기준으로 배치(코스 길이만큼 여러 개 이어붙임), `CitySilhouette`를 -Z 원경에, 구름 몇 개를 Y 높은 곳에 흩뿌림. Lighting 설정에서 Skybox를 `FlappySky.mat`로 지정.

- [ ] **Step 3: 라이팅 + 카메라**

`---Lighting---`에 Directional Light(맑은 낮, 약간 노란빛, 그림자 soft). `---Camera---`에 측면 카메라: 위치 대략 (코스중앙X, 5, -20), 회전 정면(+Z 바라봄), orthographic 또는 좁은 FOV로 횡스크롤 뷰.

- [ ] **Step 4: 검증**

Game/Scene 뷰 스크린샷.
Expected: 하늘색 배경 + 초록 지면 + 원경 도시 + 구름, 측면 카메라 구도가 플래피 화면처럼 보임.

---

### Task 7: 코스 레이아웃 — 파이프 배치 + 시작/결승 + 스폰 + 새

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Art/Scenes/FlappyRace.unity`

**Interfaces:**
- Consumes: `PipePair.prefab`(T2), `StartGate`/`FinishArch`/`Bird`/`Bush`/`ScoreRing`(T5), 씬 계층(T6).
- Produces: 완성된 코스가 담긴 `FlappyRace.unity`. `PipePair` 10~15개가 X축으로 일정 간격 배치되고 gap 높이가 변주됨. `PlayerSpawn` 8개, placeholder 새 1마리.

- [ ] **Step 1: 파이프 코스 배치**

`---Course---` 아래에 `PipePair.prefab` 12개를 X 간격 ≈ 8 유닛으로 배치. 각 쌍의 Y 오프셋(gap 중심 높이)을 3~7 유닛 사이에서 변주해 난이도 리듬 부여. 모두 Z=0 평면.

- [ ] **Step 2: 시작/결승 배치**

첫 파이프 앞(X 최소-)에 `StartGate`, 마지막 파이프 뒤(X 최대+)에 `FinishArch` 배치.

- [ ] **Step 3: 플레이어 스폰 + 새**

`---Players---` 아래 `PlayerSpawn` 빈 오브젝트 8개를 StartGate 근처에 Y로 살짝 계단식 배치(같은 Z=0). placeholder `Bird.prefab` 1마리를 첫 스폰 위치에 배치(스케일 확인용).

- [ ] **Step 4: 장식 흩뿌리기**

`Bush.prefab`를 지면 위 군데군데, `ScoreRing.prefab`를 일부 파이프 gap 중앙에 배치해 분위기 완성.

- [ ] **Step 5: 검증**

측면 카메라 기준 Game 뷰 스크린샷 + 씬 계층 조회.
Expected: 파이프 코스가 시작→결승으로 이어지고, 새가 스폰에 있고, 스케일이 자연스러움(새가 gap을 통과할 만한 크기).

---

### Task 8: 폴리싱 + 최종 확인

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Art/Scenes/FlappyRace.unity`
- Modify: `LeagueOfPhysical-Client/Assets/Art/Environment/FlappyRace/README.md`

**Interfaces:**
- Consumes: 완성된 씬(T7).
- Produces: 톤이 정리된 최종 씬 + attribution이 채워진 README.

- [ ] **Step 1: 톤/스케일 일관성 점검**

전체 스크린샷을 보고 색 튐(너무 채도 높거나 어두운 것), 스케일 어긋남, 피벗 문제(공중에 뜬 오브젝트)를 목록화하고 수정.

- [ ] **Step 2: 라이팅/카메라 미세조정**

그림자·앰비언트·카메라 구도를 플래피 톤(밝고 선명)으로 미세조정.

- [ ] **Step 3: Attribution 마무리**

다운로드한 모든 스케치팹 에셋의 출처/저작자/라이선스를 README 표에 채움.

- [ ] **Step 4: 최종 검증**

측면 Game 뷰 스크린샷 1장 + 사용자에게 제시.
Expected: "플래피 버드 톤의 완주 가능한 3D 레이스 맵"으로 한눈에 읽힘. 사용자 확인 후 완료.

---

## Self-Review 결과

- **Spec coverage:** 파이프(T2)·지면(T3)·배경/하늘/구름/도시(T4)·시작·결승·새·장식(T5)·2.5D 공유평면 좌표 규약(Global Constraints, T7)·측면 카메라(T6)·코스 10~15 파이프(T7) — 스펙 5개 섹션 모두 태스크로 매핑됨.
- **범위 밖 확인:** 게임 로직/네트워크/사운드는 스펙대로 제외.
- **경로 일관성:** 에셋 경로 `Assets/Art/Environment/FlappyRace/`, 씬 `Assets/Art/Scenes/FlappyRace.unity`로 전 태스크 통일. 프리팹 이름(`PipePair`, `Ground`, `StartGate`, `FinishArch`, `Bird`, `Bush`, `ScoreRing`)이 생성(T2~T5)과 소비(T6~T7)에서 일치.
- **기존 자산:** `FlapWangMap.unity`/`floor.mat`은 참고·재활용 대상으로 명시(신규 `FlappyRace.unity`와 별개).
