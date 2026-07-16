# 캐릭터 소프트 분리 — 키네마틱 위 "몸으로 밀림"(BOTW식)

**Date:** 2026-07-16
**Branch:** `feature/character-soft-separation`
**Related:** [world-core-connection-architecture](../../world-core-connection-architecture.md) (이동 substrate) · [entity-system-design](../../entity-system-design.md) · `2026-07-09-shared-kinematic-character-controller-design` · `2026-07-13-unified-world-tick-client-sim-scope-design`

## 목표 (한 줄)

정면으로 붙은 캐릭터 둘이 **완전히 잠기는(wedge)** 버그를 없애고, 대신 **서로 몸으로 공간을 차지하되 부드럽게 밀려 비껴나가는**(젤다 BOTW/TOTK식) 캐릭터 간 충돌로 바꾼다.

## 배경 — wedge는 왜 나나

캐릭터 이동은 **공유 키네마틱 컨트롤러**다(다이나믹 PhysX 아님). 매 틱 `LOPWorld.Tick`의 모션 페이즈가 Simulated 엔티티마다:

```
SyncTransforms → [ Depenetrate(겹침 밀어내기) → KinematicMove.Tick(collide-and-slide sweep) → PushMotion ]
```

세 사실이 겹쳐 wedge를 만든다:

1. 캐릭터 캡슐(`PhysicsComponent`가 런타임 생성)에 **레이어 지정이 없어** 전부 layer 0 = **"Default"**. 지면(`Plane`)도 Default.
2. 이동 sweep 마스크가 클·서 `GameLifetimeScope`에서 `LayerMask.GetMask("Default")` → **상대 캐릭터 캡슐을 딱딱한 벽으로 봄**.
3. 둘 다 kinematic이라 PhysX 자동 밀어냄이 없음 + 정면 법선이 나를 향해 수직 → collide-and-slide가 전진을 통째 취소(옆으로도 못 미끄러짐) → **둘 다 그 자리에 잠김**.

> **다이나믹 바디(BOTW)와의 차이:** BOTW는 Havok 다이나믹 리지드바디라 겹침을 매 프레임 "서로 밀어내기(pushout)"로 해소 → 정면으로 붙어도 항상 빠져나갈 방향이 생겨 안 낀다. LOP는 키네마틱이라 그 상호 밀어내기를 **우리가 직접** 넣어야 한다.

## 산업 표준 매핑

이 설계는 새 발명이 아니라 키네마틱 캐릭터 컨트롤러의 표준 2층 구조다:

- **이동 sweep은 다른 캐릭터를 필터로 제외** — Rapier 캐릭터 컨트롤러 문서(*"필터 인자로 일부 장애물을 무시"*), PhysX 캐릭터 컨트롤러(지오메트리 sweep과 겹침 해소를 분리).
- **캐릭터끼리 소프트 충돌 = 별도 디펜(depenetration) 패스** — PhysX *"초기 디펜 패스로 시작 겹침 회복 + 최종 디펜 패스로 겹침 없음 보장"*. BOTW 자체가 이 디펜(pushout) 유파.
- **상호적(reciprocal) 해소** — 두 캐릭터가 각자 절반씩 밀어내 오버슈트/진동을 없앰. RVO(Reciprocal Velocity Obstacles)의 "reciprocal"과 같은 원리. (RVO/boids 스티어링 유파는 AI 내비게이션 회피용이라 채택 안 함 — 우리는 물리 바디 pushout.)
- **내 캐릭 예측** — Overwatch/Source식 "자기 캐릭터의 이동·충돌 응답은 클라가 예측, 서버 권위". 분리도 이동 충돌 응답의 일부라 함께 예측.

출처: [Rapier — Character controller](https://rapier.rs/docs/user_guides/rust/character_controller/) · [PhysX — Character Controllers](https://nvidia-omniverse.github.io/PhysX/physx/5.3.1/docs/CharacterControllers.html) · [Unreal CMC RVO(서버 전용, 스티어링 유파 참고)](https://dev.epicgames.com/documentation/en-us/unreal-engine/python-api/class/CharacterMovementComponent)

## 설계 — "지형은 하드 벽 + 캐릭터는 소프트 분리"

### 1. 전용 "Character" 레이어

- 클·서 `ProjectSettings/TagManager.asset`에 **동일 인덱스**(첫 빈 유저 레이어 = **8**)로 `"Character"` 추가. 인덱스가 같아야 `GetMask("Character")`가 양쪽 동일 + 콜라이더 직렬화 일관.
- 두 `PhysicsComponent`(클라 `Assets/Scripts/Component/PhysicsComponent.cs`, 서버 `Assets/Scripts/Component/PhysicsComponent.cs`)의 `Initialize`에서 런타임 생성한 `physicsGameObject.layer`를 `"Character"`로 설정. **아트 프리팹은 안 건드림** — 물리는 이 런타임 캡슐만 관여.

### 2. sweep에서 캐릭터 제외 → wedge 소멸

- 이동 sweep 마스크(`GameLifetimeScope`의 `KinematicMoveSystem` 주입값)는 **"Default" 그대로 유지.** 캐릭터가 Default를 떠나는 순간 sweep에서 자동 제외 → 상대를 벽으로 안 봄 → **wedge 소멸**. 공유 커널 `KinematicMover`/`KinematicMoveSystem` **무수정.**

### 3. 소프트 상호 분리 (디펜 2패스) — 핵심

현재 `MotionBridge`의 디펜 한 패스를 **두 패스**로 나눈다. 둘 다 기존 `KinematicDepenetration.ComputePushOut(collider, mask)`를 재사용(헬퍼 무수정), 마스크·배율만 다르게:

```
모션 페이즈 (Simulated 엔티티마다):
  Depenetrate(entity)   ← 지형 flush.   mask = envMask("Default"),  풀 밀어냄(×1)   [기존 동작 유지]
  Separate(entity)      ← 캐릭터 분리.   mask = charMask("Character"), 밀어냄 × separationScale   [신규]
  KinematicMove.Tick
  PushMotion
```

- **지형 디펜(`Depenetrate`)은 항상 풀(×1)** — 지면은 안 움직이니 겹침을 전부 해소해야 스폰 flush가 동작(기존 동작 보존).
- **캐릭터 분리(`Separate`)는 `separationScale` 배율** — **per-side 상수**로 주입:
  - **서버 = 0.5** — 모든 캐릭이 Simulated → 각자 상대에게서 절반 밀어냄 → 합쳐서 겹침 정확히 해소(reciprocal, 오버슈트/진동 없음).
  - **클라 = 1.0** — 클라엔 Simulated 캐릭이 내 캐릭 하나뿐(남·NPC은 스냅 팔로워, 안 밀림) → 내가 상대에게서 **전부** 빠져나옴(내가 미끄러짐).
- **왜 per-side 상수로 충분한가:** 모션 페이즈 시작에 `SyncTransforms` 한 번 → 루프 중엔 PhysX 콜라이더 미재동기 → 서버에서 A·B 둘 다 **같은 원본 겹침**을 봄 → 각자 절반 → **순서 무관 대칭**. 클라는 Simulated가 하나뿐이라 배율 판단이 "전부"로 자명. 따라서 콜라이더→엔티티 매핑이나 per-collider 분기가 **필요 없다.**
- `MotionBridge`는 `envMask`·`charMask`·`separationScale`을 **생성자로 주입**받는다(기존 sweep 마스크 주입과 대칭). 로직은 공유 concrete 한 벌.

### 4. 전투 마스크 재조준 (서버 전용)

- 서버 `LOPOverlapQuery`의 `GetMask("Default")` → `GetMask("Character")`. 캐릭터가 Default를 떠나므로 이걸 안 바꾸면 **공격이 아무도 못 맞힘.**
- 전투는 서버 전용(클라엔 `IOverlapQuery`/`DamageEffectHandler` 없음) → 이 변경은 서버만. (부수효과: 오늘 `OverlapSphere("Default")`가 지면 Plane까지 긁어 필터하던 것도 사라져 약간 더 깨끗/빠름 — 회귀 아님.)

### 5. 결정론 / 예측

- **클라는 내 캐릭의 self-분리를 예측** — `Separate`가 공유 `world.Tick` 안이라 예측·하드롤백 재생에 자동 포함. 몸으로 미는 느낌이 즉각.
- **알려진 tradeoff(감내):** 클라 분리는 원격의 *보간 위치*를 쿼리 → 롤백 재생 시 원격이 과거 위치가 아닌 *현재* 보간 위치라 미세 비결정. 또 클라(1.0)와 서버(0.5)의 배율 차로 내가 상대를 밀 때 서브-cm급 재조정이 생김. 둘 다 예측 넉백·이동 예측이 이미 감내하는 급이고 재조정 스무더가 흡수. 대안(클라도 0.5)은 상대에게 겹쳐 보여 오히려 나쁨.

### 6. 안 건드리는 것 (최소 침습)

- **충돌 매트릭스(`DynamicsManager.asset`) 미편집.** 우리 해소는 전부 **명시 마스크 쿼리**(`OverlapCapsule`/`CapsuleCast`/`OverlapSphere`에 mask 전달)라 Unity 충돌 매트릭스와 무관. 캐릭터는 kinematic이라 kinematic끼리 자동 접촉도 없음. → per-repo DynamicsManager 분기 안 만듦.
- **공유 커널·헬퍼 무수정** — `KinematicMover`, `KinematicMoveSystem`, `KinematicDepenetration`.

## 공유 코드 유지 (설계 원칙)

분리 **알고리즘 자체는 공유 코드 한 벌**이고, 클·서 차이는 DI 주입 상수 + 설정 파일뿐이다 — 아키텍처가 지시하는 "공유 concrete + per-side config" 패턴.

| 코드/설정 | 위치 | 공유 여부 |
|---|---|---|
| 분리 로직(2패스: `Depenetrate`+`Separate`) | `MotionBridge` (LOP-Shared) | ✅ 공유 concrete 1개 |
| 디펜 헬퍼 `ComputePushOut` | `KinematicDepenetration` (GameFramework) | ✅ 공유, 무수정 |
| Tick 조립(`Separate` 호출 추가) | `LOPWorld.Tick` (LOP-Shared) | ✅ 공유 |
| per-side 상수(`separationScale`)·마스크 주입 | `GameLifetimeScope` (클·서 각 1) | 이미 per-side인 배선 |
| 캡슐 레이어 지정(`physicsGameObject.layer`) | `PhysicsComponent` (클·서 각 1) | 이미 per-side인 설정(각 1줄) |
| 전투 마스크 재조준 | `LOPOverlapQuery` (서버만) | 서버 전용(원래 1개) |
| `"Character"` 레이어 정의 | `TagManager.asset` (클·서 각 1) | 설정 파일 |

## 영향 파일 (개략 — 단계·세부는 plan에서)

**LeagueOfPhysical-Shared**
- `Runtime/Scripts/Game/MotionBridge.cs` — 디펜 2패스(`Depenetrate` env + `Separate` char), 생성자에 `envMask`/`charMask`/`separationScale`.
- `Runtime/Scripts/Game/LOPWorld.cs` — 모션 페이즈 루프에 `_motionBridge.Separate(entity)` 추가.

**GameFramework**
- `Runtime/Scripts/Game/IMotionBridge.cs` — `Separate(Entity)` 시그니처 추가.

**LeagueOfPhysical-Client**
- `Assets/Scripts/Component/PhysicsComponent.cs` — `physicsGameObject.layer = "Character"`.
- `Assets/Scripts/Game/GameLifetimeScope.cs` — `MotionBridge` 생성자에 마스크 2개 + `separationScale=1.0` 주입.
- `ProjectSettings/TagManager.asset` — layer 8 = `"Character"`.

**LeagueOfPhysical-Server**
- `Assets/Scripts/Component/PhysicsComponent.cs` — `physicsGameObject.layer = "Character"`.
- `Assets/Scripts/Game/GameLifetimeScope.cs` — `MotionBridge` 생성자에 마스크 2개 + `separationScale=0.5` 주입.
- `Assets/Scripts/Game/LOPOverlapQuery.cs` — 전투 마스크 `"Default"` → `"Character"`.
- `ProjectSettings/TagManager.asset` — layer 8 = `"Character"`(클라와 동일 인덱스).

## 테스트 / 검증

- **In-play 검증이 1차** — `Separate`는 Unity `Physics.OverlapCapsule`/`ComputePenetration`(라이브 PhysX)라 순수 EditMode 불가(기존 `MotionBridge.Depenetrate`와 동일 성질). 검증: ① 정면충돌 시 wedge 없이 서로 비껴나감(클·서 육안), ② 몹 클러스터 스폰 시 서로 안 겹치고 퍼짐, ③ 공격이 여전히 명중(전투 마스크 회귀 확인), ④ 지형 통과/스폰 flush 회귀 없음.
- 공유 커널 EditMode 스위트(현 122/122)는 무수정이므로 그대로 green 유지 확인.
- 계측 로그(임시, 커밋 X): `Separate` push 크기·대상 수 → 클러스터 수렴 관찰.

## Out of Scope (재개 조건)

- **캐릭터 간 상호작용 심화**(넉백을 상대에게 밀기, 몸으로 벽 세우기 조작 등) — 이번은 "안 끼고 비껴남"까지. 밀치기 메카닉이 콘텐츠로 필요해질 때.
- **최종 디펜 패스(post-move)** — 현재는 pre-move 1패스(1틱 지연, BOTW 느낌엔 무해). 같은 틱 즉시 무겹침이 필요해지면 추가.
- **반복 분리(iteration/substep)** — 극단적 대량 클러스터에서 단일 패스 수렴이 느려지면 도입. 일반 플레이엔 불필요(YAGNI).
- **캐릭터↔캐릭터 충돌 매트릭스 정리** — 명시 마스크로 이미 제어되어 불필요. 정리 취향이면 나중.

## ⚠️ 실제 결론 (2026-07-16) — 소프트 분리 폐기, "단단한 벽" 채택

위 설계(BOTW식 소프트 분리 = 서버권위 분리 + 클라 예측)를 구현·플레이 검증한 결과 **폐기**하고
**"캐릭터는 서로 통과 못 하는 단단한 벽"** 모델로 확정했다. 실측으로 확인한 내용:

**왜 소프트 분리가 안 됐나 (측정으로 확정):**
- 근본 원인은 **타임라인 불일치** — 내 캐릭=예측(현재보다 앞선 틱), 남 캐릭=보간(뒤진 틱). 서버의
  "안 겹침" 보장은 *같은 틱*에서만 성립하는데 클라는 둘을 *다른 틱*으로 그린다.
- **클라가 분리를 밀면(예측)** → 원격의 과거 위치로 밀어 서버와 매 틱 어긋남 → **덜덜(recon 폭발)**.
- **클라가 안 밀면(scale 0)** → 밀어낼 게 없어 **관통**(sweep 벽은 start-overlap 무시라 한 번 들어가면 복구 못 함).
- 즉 config(배율/벽)로는 "안 겹침 + 안 덜덜"을 동시에 못 얻는다. 깨끗이 하려면 **predict-all**(남도 예측)
  또는 **통과**뿐. 둘 다 이번 범위 밖 → soft 분리 포기.
- 부수 발견: 겹침이 "터무니없이 커" 보인 건 **클·서 충돌이 달라서**였다(클=벽, 서=소프트 분리 →
  위치 크게 어긋남). **클·서를 동일 벽 모델로 맞추자 recon이 작아지고 겹침도 사라짐**(실측: 물리 캡슐
  위치=렌더 위치 일치, 8마리 군집 정상).

**최종 채택 모델 (클·서 동일):**
- 캐릭터를 **sweep 마스크에 포함**(`Default,Character`) → 서로 벽으로 막음.
- **디펜 full(1.0)** — 겹치면 내 캡슐이 전부 빠져나옴(start-overlap 복구 → 영구 끼임 방지). `separationScale`은
  seam으로 남기되 클·서 모두 1.0.
- 원래 우려한 **wedge/몹뭉침은 실전 문제 아님**(몹은 비스듬히 접근해 캡슐 곡면 타고 미끄러짐). 완벽 정면
  head-on만 드물게 멈춤 — 이건 정상적 "단단한 몸".

**동작상 원래(이 브랜치 전)와 사실상 동일** — 원래도 캐릭터가 Default에 있어 sweep 벽 + 디펜을 했다.
이 브랜치의 실질 산출물 = ① 전용 `Character` 레이어(마스크 분리 유연성·전투 마스크 명시), ② 명시적
디펜 패스(끼임 방지), ③ **위 측정 지식의 문서화**. 진짜 소프트 분리가 필요하면 predict-all 슬라이스로.

## 진행

- [x] 브레인스토밍 → spec → plan → 구현(Task 1/2) → 플레이 검증
- [x] **플레이 결과 soft 분리 폐기, 단단한 벽 확정** (클·서 동일 sweep+디펜 full)
- [x] 결론 문서화(이 절 + `[[kinematic-controller-migration]]`·ROADMAP)
