# Netcode Phase 1 — 회전 결정론적 snap (LOPMovementManager SmoothDampAngle 버그 픽스)

**Date:** 2026-06-20
**Branch (docs):** `feature/netcode-phase1-rotation-snap` (클라 repo — 설계 허브)
**Branch (code):** 클·서 각 repo 피처 브랜치 (구현 시 생성)
**Related:** [Netcode 재설계](../../netcode-redesign.md) (§3.3 Phase 1) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md)

## Goal

클·서 `LOPMovementManager.ProcessInput`의 회전 처리를 **결정론적 snap**으로 바꿔, `netcode-redesign.md` §3.3에서 식별한 `Mathf.SmoothDampAngle` 버그를 제거한다. 회전(`entity.rotation`)이 클·서에서 같은 입력에 같은 결과를 내도록(결정론) 만든다.

## 배경 — 버그 (netcode-redesign §3.3)

`LOPMovementManager.cs:30-33` (클·서 동일):
```csharp
float myFloat = 0;
var angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
var smooth = Mathf.SmoothDampAngle(entity.rotation.y, angle, ref myFloat, 0.01f);
entity.rotation = new Vector3(0, smooth, 0);
```

두 가지 결함:
1. **`ref myFloat` 매 호출 0 초기화** — `SmoothDampAngle`은 velocity 상태를 `ref`로 누적해야 의도대로 동작하는데, 매 호출 로컬 변수를 0으로 새로 만들어 누적이 파괴됨.
2. **내부 `Time.deltaTime` 사용** — 인자에 dt를 안 넘기는 오버로드라 내부적으로 `Time.deltaTime`을 씀. fixed-tick 시뮬과 어긋나고, 클·서 프레임레이트가 달라 **회전 결과가 발산**한다.

추가 관찰:
- 여기서 `entity.rotation`은 **velocity와 무관한 순수 facing 값**이다 — velocity는 `direction * Speed`에서 직접 계산되며 rotation을 참조하지 않는다. 즉 회전은 판정·이동에 영향 없는 cosmetic 상태(단, 스냅샷으로 동기되는 시뮬 상태).
- 현 `smoothTime = 0.01`은 **사실상 즉시 회전**이라, 의도된 smoothing 자체가 거의 없다.

## 잠긴 결정 (브레인스토밍 합의)

| # | 축 | 결정 | 근거 |
|---|---|---|---|
| ① | 레이어 분리 | **시뮬 = 결정론, 부드러운 연출 = 뷰** | 업계 표준. 시뮬은 클·서 동일 결과(결정론) 보장, 시각 ease는 렌더 측. `entity.rotation`은 시뮬 상태라 결정론화 대상 |
| ② | 시뮬 facing 방식 | **결정론적 snap** (`entity.rotation = new Vector3(0, angle, 0)`) | 회전이 cosmetic + 현 0.01s ≈ 즉시라 snap이 현 체감에 충실. 무상태·dt 비의존 → 클·서 동일. netcode-redesign이 제시한 옵션 |
| ③ | SmoothDamp 유지? | **기각** | `SmoothDampAngle`(지수 ease + velocity 상태)은 카메라/뷰 idiom. 시뮬에 두면 velocity 상태가 스냅샷/롤백에 끌려 들어가 결정론 비용↑(Stage④ 부담). Unity ThirdPersonController도 SmoothDamp는 렌더 측 |
| ④ | turn 시간 | **없음(즉시 facing)** | 현 동작에 "도는 시간" 의도 없음. 필요해지면 `MoveTowardsAngle`(고정 rate, 무상태·결정론)로 별도 도입 |
| ⑤ | 범위 | 클·서 `LOPMovementManager` 회전 블록만 | netcode-redesign §6.2 — 클·서 동일 코드라 동시 수정 |

## Scope (In) — 클·서 각 1파일

`Assets/Scripts/Game/LOPMovementManager.cs` (클라·서버, 현재 바이트 동일) — 이동 블록 내 회전 4줄을 교체:

```csharp
// 제거
float myFloat = 0;
var angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
var smooth = Mathf.SmoothDampAngle(entity.rotation.y, angle, ref myFloat, 0.01f);
entity.rotation = new Vector3(0, smooth, 0);

// 대체
float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
entity.rotation = new Vector3(0, angle, 0);
```

- 위치는 기존 그대로 이동 블록(`if (direction.sqrMagnitude > 0)`) 안 — 입력 없을 때 facing 유지(현 동작 보존).
- 무상태·dt 비의존 → 클·서 동일 facing. `Time.deltaTime` 발산 + velocity 리셋 버그 동시 제거.
- 클·서 두 파일을 **동일하게** 수정(현재처럼 바이트 동일 유지).

## 데이터 흐름 (전환 후)

```
입력(horizontal, vertical) → direction
 if direction != 0:
   velocity = direction * Speed          (기존, rotation 비참조)
   angle = Atan2(direction.x, direction.z)·Rad2Deg
   entity.rotation = (0, angle, 0)        ← 결정론 snap (무상태·dt 무관)
```

클·서가 같은 `direction`에 같은 `entity.rotation`을 산출 → 회전 발산 0.

## Out of scope (defer)

- **부드러운 facing 연출(뷰 ease)** — View가 `entity.rotation`(시뮬 target)으로 매 렌더 프레임 ease. 뷰가 World를 pull하는 인프라가 **Stage④**라 그때 자연스럽게 얹음. 지금 반쯤 만들지 않음(YAGNI).
- **시뮬 내 turn 시간(`MoveTowardsAngle` 고정 rate)** — 게임플레이로 "도는 시간"이 필요해질 때 별도. 결정론·무상태라 도입은 간단.
- **나머지 netcode Phase** — Phase 2 clock sync, Phase 3 input buffer, Phase 5 점프 임펄스 등. 각자 별도.
- **Phase 0 측정 HUD** — 이건 *튜닝*이 아니라 *명백한 버그 수정*이라 baseline 측정 불필요(netcode-redesign §3.3가 Phase 1을 "즉시 적용 가치"로 분류).

## 조사 근거 (구현 사실)

- 클·서 `LOPMovementManager.cs`는 **바이트 동일**(둘 다 `IMovementManager<LOPEntity>`, 같은 본문). 회전 블록 라인 30-33 동일.
- `entity.rotation`은 `Vector3`(euler), `.y`가 yaw. velocity는 `direction * Speed`로 산출 — rotation 비참조(회전=cosmetic 확인).
- 결정론 dt API 존재: `GameEngine.Time.tickInterval`(double), `GameEngine.Time.deltaTime`. (snap은 dt 불필요라 미사용 — 향후 `MoveTowardsAngle` 도입 시 사용처.)
- `LOPMovementManager`는 픽스처 아님(서버 픽스처는 `LOPGame.cs`/`ConfigureRoomComponent.cs`).

## 검증

- **컴파일**: 클·서 0에러 (UnityMCP `refresh_unity`+`read_console`, **각 인스턴스 핀** — 클라 `LeagueOfPhysical-Client@<hash>`, 서버 `LeagueOfPhysical-Server@<hash>`). GameFramework 무변경.
- **런타임(수동, 플레이)**: 캐릭터가 이동 방향으로 facing 정상, 회전 떨림/클·서 시각 불일치 없음, 정지 시 facing 유지. 동작 보존(≈즉시 회전)이 곧 성공.
- 자동화 테스트 신규 없음(클·서 단일 Assembly-CSharp, LOP 글루 — 컴파일 + 수동 플레이).

## GUID / .meta 정책

수정만(파일·클래스명 유지) → `.meta` 영향 0. 삭제·신규 파일 없음.

## 문서/브랜치 정책

선례대로 **spec·plan은 클라 repo**(설계 허브) 피처 브랜치 `feature/netcode-phase1-rotation-snap`, **코드는 클·서 각 repo** 피처 브랜치. 서버 working-tree 로컬 픽스처(`LOPGame.cs`/`ConfigureRoomComponent.cs`)는 **커밋 금지·stash 보존**(이번 변경 파일과 무관). 이 spec은 `CLAUDE.md` `@` 자동 로드 목록에 추가.

## 진행

- [x] 버그·레이어 분리(시뮬 결정론/뷰 ease) + snap 방식 합의 (브레인스토밍)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan
- [ ] 구현(클·서) → 검증 → 머지

> netcode-redesign Phase 1의 명백한 버그 수정. 후속(clock sync 등 Phase 2+)은 각자 별도 spec. 부드러운 facing 연출은 Stage④ 뷰-pull에서.
