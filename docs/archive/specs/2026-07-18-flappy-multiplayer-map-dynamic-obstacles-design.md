# Flappy Race — 다인용 맵 전환 + 동적 장애물 3종 이식 (설계)

작성일: 2026-07-18
선행 문서: `2026-07-16-flappy-race-3d-map-design.md`, 메모리 `flappy-race-map-project`

## 목표

현재 플레이 가능한 슬라이스(정통 단일-틈 · 직교 2D룩 · 물리 튜닝 완료)를 두 방향으로 확장한다.

1. **다인용 맵 전환** — 1인용 단일-틈 코스를 4인용 혼합 구간 코스로 변경 (지오메트리만).
2. **동적 장애물 3종 이식** — 브라우저 프로토에서 확정한 오르내리는 파이프·회전 도넛·조리개를 유니티 씬에 이식.

**범위 밖**: netcode, 실제 멀티/봇, 정식 아키텍처(World Core/VContainer) 통합. 플레이어는 로컬 1마리로 테스트하되 코스는 4인 폭으로 설계. 봇/멀티는 다음 세션 "멀티" 항목.

## 확정 결정 (이번 세션)

| 항목 | 결정 |
|---|---|
| 플레이어 수 기준 | **4인** |
| 정적 백 구조 | **혼합 구간별** (넓은 틈 ↔ 다중슬롯 번갈아) |
| 동적 장애물 범위 | **3종 다** (파이프·도넛·조리개) |
| 통과보장 원리 | **유지** (동적 장애물도 매 주기 도달 가능한 통과 창 보장) |
| 다인용 범위 | **지오메트리만** (봇/netcode 제외) |
| 구현 방식 | **A안** — 재사용 컴포넌트 `FlappyCourseGenerator` + 타입별 동적 장애물 스크립트 |

## 아키텍처

기존 독립 MonoBehaviour 방식 유지. 신규 스크립트는 `Assets/Scripts/FlappyRaceSlice/`에 추가.

기존 자산 재활용:
- `FlappyPlayer` (플랩/중력/전진/대시/유령/틸트) — 변경 없음.
- `FlappyObstacle` (충돌 마커, 유령 페널티 트리거) — 모든 신규 장애물이 붙임.
- `FlappyCameraFollow` — 변경 없음.
- 파이프 비주얼(실린더 몸통 + 넓은 립) — 생성기가 절차 생성으로 재현.

### 1. `FlappyCourseGenerator` (코스 조립기)

일회성 `execute_code` 생성을 대체하는 재사용 컴포넌트. 인스펙터에서 구간 리스트를 정의하고 ContextMenu 버튼으로 코스를 스폰/클리어한다.

**직렬화 데이터:**
- `List<Section> sections` — 순서대로 배치될 구간들.
- `Section`: `{ SectionType type; int count; DynamicKind dynamicKind; }`
  - `SectionType`: `WideGap | MultiSlot | Dynamic`
  - `DynamicKind` (type==Dynamic일 때): `MovingPipe | Donut | Iris`
- 전역 파라미터: `float startX`, `float wallSpacing`(기본 14), `int seed`, `float yMin`, `float yMax`(틈 중심 범위), `float wideGapSize`(≈9), `Transform courseRoot`.

**통과보장 계산:** `maxGapDelta = (flapImpulse/2) × (wallSpacing/forwardSpeed) × 0.45` (현 슬라이스의 ±6.9 방식 그대로). 연속 틈/슬롯 중심의 Y 델타를 이 값으로 클램프.

**구간 생성 규칙:**
- **WideGap**: 벽마다 위/아래 파이프 + 립, 틈 크기 `wideGapSize`. 틈 중심은 랜덤워크(델타 ≤ `maxGapDelta`), `[yMin,yMax]` 클램프. 4인 몸싸움 여지.
- **MultiSlot**: 벽마다 파이프 5단으로 슬롯 4개 형성. 슬롯 폭 ≈ 2.5~3유닛. 연속 벽의 슬롯 세트를 전체 델타 ≤ `maxGapDelta`로 이동 → 어느 슬롯에서든 다음 벽의 최소 한 슬롯 도달 가능.
- **Dynamic**: `dynamicKind`에 해당하는 프리팹/절차 오브젝트를 `count`개 `wallSpacing` 간격 배치. 각 오브젝트는 해당 동적 스크립트 + `FlappyObstacle` 콜라이더 포함.

**메서드:**
- `[ContextMenu("Generate Course")] void Generate()` — 기존 코스 클리어 후 `courseRoot` 아래 재생성. `seed`로 결정론적.
- `[ContextMenu("Clear Course")] void Clear()` — `courseRoot` 자식 전부 제거.

결정론적 재생성을 위해 `Random.InitState(seed)` 사용(유니티 C#이므로 제약 없음).

### 2. 동적 장애물 스크립트 (3종)

공통: 각자 자기 Transform 모션만 책임. 콜라이더 자식/본체에 `FlappyObstacle` 부착 → 충돌 시 기존 유령 페널티 발동. 통과보장은 진폭·주기 파라미터로 보장.

- **`FlappyMovingPipe`** — 위/아래 파이프쌍(틈 크기 고정 ≥ 통과 가능)이 수직 왕복.
  - `y = baseY + amp × sin(t × speed + phase)`
  - 통과보장: `amp`로 `[yMin,yMax]` 안 유지, 틈의 수직 이동속도(`amp×speed`) < 플레이어 제어 여력 → 어느 순간에 접근해도 도달 가능.
- **`FlappyRotatingDonut`** — 링(토러스/실린더 껍질) 둘레에 빈틈 1개, Z축(화면 축) 회전.
  - 빈틈 각폭 `gapAngle`, 회전속도 `rotSpeed`.
  - 통과보장: `rotSpeed`를 충분히 낮게 → 플레이어 접근 방향(-X)에 빈틈이 정렬되는 창이 매 주기 존재하고, 그 창이 통과 시간 이상 지속.
- **`FlappyIris`** — 위/아래 파이프 사이 구경이 개폐(조리개).
  - `gapSize = baseGap + amp × sin(t × speed + phase)`
  - 통과보장: **최소 구경(`baseGap − amp`) ≥ 새 통과 가능 크기** 유지 → 치명적으로 닫히지 않음(공정). v1은 보수적으로, 이후 튜닝으로 조일 수 있음.

### 3. 코스 레이아웃 (초기 시퀀스, 인스펙터 조정)

```
WideGap×3 (인트로) → MultiSlot×3 → Dynamic:MovingPipe×2 → WideGap×2
→ Dynamic:Donut×2 → MultiSlot×3 → Dynamic:Iris×2 → WideGap×2 (결승)
```
총 ~19벽 × 14 ≈ 266유닛. `wallSpacing`/`count`로 자유 조정.

## 데이터 흐름

1. 씬에 `FlappyCourseGenerator` 오브젝트(빈 GameObject) — 구간 리스트 + 파라미터 설정.
2. ContextMenu `Generate` → `courseRoot` 아래 정적 파이프 + 동적 장애물 스폰.
3. 동적 장애물은 각 스크립트가 `Update`에서 자기 Transform 갱신.
4. `FlappyPlayer`가 전진하다 콜라이더 트리거 → `FlappyObstacle`가 유령 페널티.

## 통과보장 원리 (요약)

플래피의 "항상 통과가능 = 실력" 유지. 정적/동적 공통 규칙:
- **정적**: 연속 틈/슬롯 중심 델타 ≤ `maxGapDelta`(도달거리).
- **동적**: 매 주기, 플레이어가 도달 가능한 위치·시점에 통과 창이 반드시 존재하도록 진폭/주기/최소구경 제한.

## 검증

- 유니티 플레이모드에서 로컬 새 1마리로 전체 코스 완주 시도 → 모든 구간이 타이밍만 맞추면 통과 가능한지 확인(막히는 구간 = 통과보장 위반 → 파라미터 조정).
- 각 동적 장애물이 유령 페널티를 정상 발동하는지 확인.
- 4인 폭: WideGap/MultiSlot이 4마리가 나란히/추월할 여유를 주는지 눈으로 확인(더미 없이 폭 수치로 검토).

## 미해결 / 다음 세션

- 봇/실제 멀티(상호작용 몸싸움/아이템), netcode, 정식 아키텍처 통합.
- 동적 장애물 통과보장 파라미터 정밀 튜닝(현 v1은 보수적).
- 배경 도시(파란박스) 평면 정리(이전 세션 잔여 항목).
