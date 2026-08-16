# 슬라이스 B2 — Flappy Race 게임플레이

게임 모드 축(`2026-08-15-game-mode-axis-design.md`)의 슬라이스 B2. B1이 "축이 갈라지는가"를
증명했다면, B2는 그 축 위에서 **실제로 날고 부딪히고 서로 밀치는** 게임을 만든다.

> **끝났다는 기준**: 여러 명이 클라 예측 위에서 실제로 난다. 새가 파이프·바닥에 막히고,
> 새끼리 부딪히면 서로 밀려난다. FlapWang은 이전과 똑같이 동작한다.

---

## 1. 무엇을 만드나

| 넣는 것 | 빼는 것 (D 이후 또는 범위 밖) |
|---|---|
| 고정 전진 + 플랩 + 중력 | 결승선 통과·순위·종료 판정 (슬라이스 D) |
| 파이프·바닥 충돌 (막히고 미끄러짐) | 추격자, 대시, 부스트존 |
| 새끼리 몸싸움 (밀어내기 + 세로속도 교환) | 유령정지 페널티 |
| 클라 예측 / 롤백 재생 | 맵 씬에 남은 프로토타입 오브젝트(`Player`/`Pacer_*`) 정리 |

---

## 2. 슬라이스 순서

```
B2-a  콘텐츠 경로 뚫기      서버가 맵·새 프리팹을 받는다
B2-c  넷코드 게임 비종속화   되감기가 게임 규칙을 모르게 한다 (FlapWang만으로 검증 가능)
B2-b  시뮬 코어             전진·플랩·중력·몸싸움
B2-d  엔티티·뷰 + 런타임 검증
```

**B2-c를 게임플레이보다 앞에 둔다.** 넷코드 구조를 건드리므로 회귀 위험이 있는데, Flappy
게임플레이가 아직 없는 상태에서 하면 **FlapWang만으로 회귀를 검증**할 수 있어 원인 분리가 깔끔하다.
게임플레이를 먼저 만들면 "예측이 이상한 게 새 이동 탓인지 넷코드 개편 탓인지" 구분이 안 된다.

---

## 3. B2-a — 서버 콘텐츠 경로

### 지금 상태 (2026-08-17 실측)

배선은 **이미 다 있다.** 서버는 부팅 시 원격 카탈로그를 받아 쓰도록 빌드돼 있다:

```
서버 이미지 /app/lop-server_Data/StreamingAssets/aa/settings.json
  AddressablesMainContentCatalogRemoteHash
    → https://lop-assets.s3.ap-northeast-2.amazonaws.com/dev/StandaloneLinux64/catalog_0.1.hash
  buildTarget = StandaloneLinux64
```

그 S3 경로에 콘텐츠도 실재한다(HTTP 200). 그래서 **FlapWang은 서버에서도 맵·캐릭터 비주얼이
정상 로드되고 있다** — 카탈로그에 `FlapWangMap`·`Knight`·`Archer`가 들어 있다.

문제는 **그 카탈로그가 2026-06-22자**라는 것이다. 8월에 승격한 `FlappyRaceMap`과 새 프리팹이
들어 있을 수가 없다. B1에서 서버가 낸 `ChainOperation failed`·`The Object you want to
instantiate is null`은 여기서 온다.

> ⚠️ **B1 검증 절의 진단 정정.** "서버 프로젝트에 아트가 없어서 실패한다"고 적었는데 틀렸다.
> 아트는 Addressables 원격 번들로 이미 서버에 도달하고 있고, 실패 원인은 **그 번들이 낡아
> Flappy 자산이 없기 때문**이다. 서버에 Art 서브모듈을 붙일 필요는 없다.

### 빠진 것

`content-deploy.yml`이 **Android만** 굽고 올린다(`BuildAndroidContentUpdate` →
`s3://lop-assets/dev/Android`). 게임서버가 쓰는 **StandaloneLinux64** 경로는 CI에 없어서
6월 로컬 산출물 이후 갱신이 끊겼다.

### 할 일

1. 클라 `Assets/Editor/BuildScript.cs`에 **Linux64 콘텐츠 빌드** 메서드 추가 (기존
   `BuildAndroidContentFull`/`BuildAndroidContentUpdate`와 짝을 맞춘 이름)
2. `content-deploy.yml`에 Linux 타깃 잡 추가 → `ServerData/StandaloneLinux64` →
   `s3://lop-assets/dev/StandaloneLinux64`
3. `FlappyRaceMap` 씬과 **새 프리팹**을 Addressable 엔트리로 등록 (`Scene`·`Character` 그룹)
4. 한 번 굽고 올린 뒤 서버 파드에서 확인

**끝났다는 기준**: 서버 파드 로그에서 맵 로드 성공 + 새 비주얼 인스턴스화 성공. 즉 B1에서 본
`ChainOperation failed`가 사라진다.

---

## 4. B2-c — 넷코드를 게임 비종속으로

### 문제

되감기(`Reconciler`)가 **게임 규칙을 알고 있다.** 두 군데다:

```
Reconciler
 ├─ 상태 저장/복원
 │    snapshotHistory               위치·속도 — 게임 무관, 정상
 │    predictedAbilityStateHistory  스킬 상태 — 게임 고유가 넷코드에 박힘  ❌
 └─ 재생 중 입력 적용
      world.Tick(t, dt)             정상
      abilityActivator.TryActivate  넷코드가 스킬을 직접 발동          ❌
```

그래서 스킬이 없는 게임(Flappy)은 `Reconciler`를 만들 수조차 없다 — 생성자가 `AbilityActivator`와
`SequenceBuffer<PredictedAbilityState>`를 반드시 요구한다. B1이 게임별 DI 분리를 포기하고
`GameplayInstaller` 한 벌로 간 이유가 이것이다.

### 정석 모양

되감기는 **무엇을 저장할지 몰라야 한다.** 월드에게 "네 상태를 저장해라 / 그 시점으로 되돌려라"만
시키고, 무엇이 담기는지는 각 게임이 정한다.

| 지금 | 바꾼 뒤 |
|---|---|
| 넷코드가 위치·속도 + 스킬 상태를 각각 저장 | 넷코드는 `world.Capture(tick)` / `world.RestoreTo(tick)`만 부른다 |
| 재생 중 넷코드가 `TryActivate`로 스킬 발동 | 입력을 **입력 버퍼에 되돌려 놓고** `world.Tick` 한 번. 그 입력으로 무엇을 하는지는 월드가 안다 |

결과: `FlappyWorld`는 위치·속도만 담고 `LOPWorld`는 거기에 스킬 상태를 더 담는다.
**넷코드 코드에서 "스킬"이라는 단어가 사라진다.**

### 산업 표준 매핑

| 표준 | 대응 |
|---|---|
| **GGPO** `save_game_state` / `load_game_state` — 롤백 엔진은 상태의 내용을 모르고 게임이 저장·복원한다 | `world.Capture` / `world.RestoreTo` |
| **Photon Quantum** frame snapshot — 프레임 전체를 통짜로 스냅샷 | 월드가 자기 스냅샷을 소유 |
| **입력-as-데이터** (Quantum `PollInput`, Unity NetCode for Entities `ICommandData`) — 입력은 데이터로 버퍼에 놓이고 시뮬이 소비 | 재생 = 입력 버퍼 되돌리고 `world.Tick` |

세 번째는 `netcode-redesign.md` §4d가 "Stage④ 몫"으로 예고해둔 표준 `IInputSource` 방향과 같다.
B2가 그 절반(재생 경로에서의 입력 소비)을 앞당긴다.

### 건드리는 곳과 위험

- **GameFramework** — 되감기 인터페이스(`IWorld` 확장 또는 별도 포트)
- **클라 `Reconciler`** — 스킬 의존 제거
- **LOP-Shared `LOPWorld`** — 스킬 입력을 월드가 소비하도록
- **신규 `FlappyWorld`** — 자기 상태만 담는 구현

가장 큰 위험은 **FlapWang 회귀**다. 지금 잘 도는 예측·롤백을 건드린다.
→ **완료 기준에 "FlapWang이 이전과 똑같이 동작한다"를 명시적으로 넣는다**(공중 점프 시나리오 +
`DebugHud`의 reconciliation distance가 개편 전 수준).

---

## 5. B2-b — 시뮬 코어 (LOP-Shared)

### `TbFlappyConfig` (마스터데이터 신규)

`TbCombatConfig`와 같은 **단일 행 설정 테이블**. 단 group을 비워 **클·서 양쪽 패키지**에 나가게
한다(`TbCombatConfig`는 서버 전용 `s`지만, 이 값은 클라 예측이 같은 값을 써야 한다).

| 컬럼 | 뜻 |
|---|---|
| `forward_speed` | 고정 전진 속도 |
| `flap_impulse` | 플랩 시 세로 속도 |
| `gravity` | 중력 가속도 |
| `max_fall_speed` | 낙하 속도 상한 |
| `body_radius` / `body_height` | 새 캡슐 — **프리팹 콜라이더와 반드시 일치** |
| `restitution` | 몸싸움 반발계수 |

시작값은 프로토타입 씬의 검증된 값(전진 11, 플랩 23, 중력 70, 최대 낙하 30)에서 가져온다.

### `FlappyMoveSystem`

무상태 DI 시스템(`*System` 관례). 한 틱:

```
플랩 입력 소비 → vy += gravity·dt (max_fall_speed로 clamp) → 플랩이면 vy = flap_impulse
전진 속도는 상수로 고정 (vx = forward_speed)
```

### `FlappyBounce`

프로토타입의 순수 함수를 **그대로 이식**한다(반발계수 기반 세로속도 교환, 절반씩 밀어내기).
EditMode 테스트 8개도 함께 옮긴다. 지금은 클라 프로토타입 폴더에 있고, LOP-Shared에는 테스트
어셈블리가 이미 있다.

### `FlappyWorld.Mutation`

`LOPWorld`와 같은 페이즈 구조를 따른다:

```
① 입력·속도 갱신        FlappyMoveSystem
② 새끼리 몸싸움          분리 + FlappyBounce 세로속도 교환   ← 전원 이동 전 페이즈 배리어
③ 이동                   KinematicMover.Move (collide-and-slide) → Transform/Velocity 기록
```

물리 브릿지(`IMotionBridge`)는 `LOPWorld`와 같은 것을 쓴다 — 스크립트로 옮긴 위치를 물리에
반영(`SyncTransforms`), 시작 겹침 해소(`Depenetrate`), Rigidbody 팔로워에 반영(`PushMotion`)은
게임 규칙이 아니라 엔진 연동이라 게임별로 다를 이유가 없다.

**충돌은 기존 `KinematicMoveSystem`을 쓰지 않고 공유 커널 `KinematicMover.Move`를 직접 부른다.**
`KinematicMoveSystem`은 중력 −19.62·캡슐 0.35/1.5가 상수로 박혀 있어 Flappy와 맞지 않는다.
`KinematicMover.Move(in KinematicMoveInput, ICollisionQuery)`는 위치·속도·반지름·높이·dt·
레이어마스크를 전부 인자로 받는 순수 static 커널이라, 그대로 재사용하면 **FlapWang 회귀 위험이 0**이다.

---

## 6. B2-d — 엔티티와 뷰

- 클·서 `FlappyBirdCreator`에 **`Simulated`** 추가 — 서버는 모든 새, 클라는 **내 새만**.
  남의 새는 예측하지 않고 스냅샷 보간(`RemoteEntityInterpolator`)에 맡긴다. 나머지 컴포넌트
  조합은 B1 그대로(`Transform`/`Velocity`/`EntityKind`/`Appearance`/`MotionContributions`/`InputBuffer`)
- 새 프리팹에 **캡슐 콜라이더** — 서버 sweep 대상이자 몸싸움 분리 기준.
  크기는 `TbFlappyConfig`의 `body_radius`/`body_height`와 **일치해야 한다**(어긋나면 클·서가
  다른 몸으로 밀어내 예측이 깨진다)
- 스폰 지점: 맵 씬의 `PlayerSpawn_1~4` 마커가 **비활성**이다. 켜고 서버 룰이 읽게 한다(B1 숙제)

**입력은 새로 만들 게 없다.** 플랩 = 기존 `InputCommand.Jump`. 와이어 포맷·서버 입력 버퍼·유실
대비 재전송이 전부 그대로 재사용된다.

---

## 7. 테스트와 검증

| 대상 | 방법 |
|---|---|
| `FlappyBounce` 속도 교환 | EditMode (이식한 8개) |
| `FlappyMoveSystem` 속도 갱신 | EditMode — 순수 계산이라 단위 테스트 가능 |
| **FlapWang 회귀** (B2-c 직후) | 2에디터 + 공중 점프, reconciliation distance가 개편 전 수준 |
| 서버 콘텐츠 (B2-a 직후) | 서버 파드 로그에 맵 로드·비주얼 성공 |
| 나는 감각·충돌 | 로비에서 FlappyRace 선택 → 날고 파이프에 막히는지 |
| 몸싸움 | MPPM 가상 플레이어 2인 |

---

## 8. 확정된 결정

| 항목 | 결정 | 이유 |
|---|---|---|
| 파이프·바닥 충돌 | **막히고 미끄러진다**(collide-and-slide) | 별도 상태가 없어 예측·롤백과 가장 잘 맞는다. 전진이 막혀 뒤처지는 것 자체가 페널티 |
| 튜닝값 | **마스터데이터 신규 테이블** | 기획이 엑셀로 조정. 클·서가 같은 생성물을 본다 |
| 몸싸움 | **프로토타입 규칙 이식** | 순수 함수 + 테스트가 이미 있고 손맛이 검증됨 |
| 예측 범위 | 서버=모든 새 / 클라=내 새만 | 기존 `Simulated` 마커 규약 그대로 |
| 서버 콘텐츠 | **Addressables 원격 번들**(이미 동작) | 서버에 아트 소스를 붙일 필요가 없다 |

---

## 9. Open Decisions

- [ ] **`world.Capture`/`RestoreTo`의 정확한 시그니처** — `IWorld`에 직접 붙일지, 별도 포트
  (`IRollbackState` 등)로 뺄지. `netcode-redesign.md` §6.5는 "Snapshot/Restore를 시뮬 코어에 두지
  않는다"고 적었는데, 그 취지는 *보관 정책*을 코어에 두지 말라는 것이다. 상태를 **뜨고 넣는 것**은
  월드만 할 수 있으므로 이 결정과 충돌하지 않는다 — 구현 시 문구를 함께 정리한다.
- [ ] **맵 콜라이더 레이어** — 새 sweep이 쓸 레이어마스크. 프로토타입은 `~0`(전부)였다.
- [ ] **`TbFlappyConfig` 이름** — 게임이 늘면 `TbGameConfig`+게임모드 키가 나을 수 있다. 지금은
  게임이 둘뿐이라 단순한 쪽으로 간다.
- [ ] **콘텐츠 빌드 주기** — Linux 콘텐츠를 매 배포마다 굽을지, 자산이 바뀔 때만 수동으로 굽을지.

---

## 10. 관련 문서

- `docs/superpowers/specs/2026-08-15-game-mode-axis-design.md` — 게임 모드 축 전체, B1/C 결과
- `docs/netcode-redesign.md` — 예측·보정 구조, §4d 입력 소스 표준
- `docs/world-core-connection-architecture.md` — 월드 코어, `Simulated`, 키네마틱 이동 substrate
- `docs/superpowers/specs/2026-07-09-shared-kinematic-character-controller-design.md` — `KinematicMover` 커널
