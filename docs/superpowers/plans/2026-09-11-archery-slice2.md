# Archery 슬라이스 2 — 과녁이 뜨고, 맞히면 점수가 되고, 60초 뒤 순위가 나온다

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 판이 시작되면 **1.76초마다 무대 위에 과녁이 2~3개** 떴다 사라진다. 통신으로 받는 것이 아니라
**씨앗으로 각자 계산**하므로 모두가 같은 순간에 같은 과녁을 본다. 화살이 과녁에 닿으면 **서버가
확정해** 점수를 주고, 그 과녁은 모두에게서 사라진다. 60초가 끝나면 **점수 순위**가 결과 화면에 뜬다.

**Architecture:** 과녁은 엔티티가 아니다 — 화살과 같은 취급이다. `(매치 씨앗, 웨이브 번호)`를 넣으면
같은 과녁이 나오는 **순수 커널**(`ArcheryWaveGenerator`)을 LOP-Shared에 두고, 클·서가 그 *같은 구체
코드*를 실행한다. 적중 판정만 **서버 권위**(`ArcheryHitSystem`)이고, 그 결과는 **세 갈래**로 내려온다 —
**점수는 엔티티 스냅샷**, **어느 과녁이 남았나는 웨이브 상태**(둘 다 durable), **누가 몇 점을 먹었나는
사건**(연출·귀속). 클라는 적중을 예측하지 않는다.

**Tech Stack:** Unity 6 / VContainer / Mirror / Protobuf(wire) / Luban(MasterData) / NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-09-11-archery-game-mode-design.md`

---

## 이 슬라이스가 전체 어디쯤인가

| 슬라이스 | 내용 |
|---|---|
| 1 (완료) | 모드가 존재한다. 제자리에서 조준·당김·발사. 화살이 난다. 과녁 없음 |
| **2 (이 문서)** | **결정론 웨이브로 과녁이 뜬다. 맞히면 점수. 60초 뒤 순위.** `#ArcheryConfig`/`#ArcheryTarget` |
| 3 | 함정 과녁 + 점수 차감 + 미리 당기기의 대가(줌인 대가·흔들림). ← **재미 확인 지점** |
| 4 | 직선맵 추가 + 원형맵의 사람 오발 |

**슬라이스 2가 의도적으로 안 하는 것**과 그 이유:

| 안 하는 것 | 왜 |
|---|---|
| **함정 과녁·점수 차감** | 슬라이스 3의 몸통이다. `is_trap` 컬럼도 **지금 넣지 않는다** — 읽는 코드가 없는 컬럼은 죽은 데이터고, 슬라이스 3은 어차피 함정 비율 설정을 더하느라 테이블을 다시 굽는다 |
| **거리 축**(멀수록 비싸다) | 원형맵은 **모두가 가운데에서 같은 거리**라 거리 축이 성립하지 않는다. 직선맵(슬라이스 4)에서 의미가 생긴다. 지금은 *작을수록 비싸다*만 쓴다 |
| **당김 흔들림·줌인** | 슬라이스 3 |
| **적중 예측** | 스펙 §7.2 — 과녁이 2~3개뿐이라 남이 먼저 맞혔을 확률이 높다. 취소되는 점수를 보여 주지 않는다 |
| **맞은 자리 연출**(파편·소리) | 사라지는 것으로 충분하다. 연출은 몸/아트가 정해진 뒤 |

---

## Global Constraints

- **main에 직접 커밋 금지.** 각 레포에서 브랜치 `feature/archery-slice2`.
- **`git add -A` / `git commit -a` 금지.** 워킹트리에 상시 로컬 픽스처가 있다
  (`Assets/Art` 서브모듈 포인터, `Jua-Regular SDF.asset`, `ProjectSettings/*`). **바꾼 파일만 경로로
  지정**하고 커밋 전에 `git status --short`로 스테이지된 것을 확인한다.
- **`.cs`를 새로 만들면 Unity가 만든 `.meta`를 반드시 함께 커밋한다.** `.meta`를 손으로 만들지 않는다.
- **시뮬 코드는 LOP-Shared에 구체 클래스로 둔다.** 인터페이스 seam 금지 — 결정론을 보장하는 것은
  *공유 구체 코드*다.
- **`*System`은 무상태 DI 인스턴스**, **`static`은 컨텍스트 없는 순수 커널에만**. 순수 커널에
  `*System` 이름을 붙이지 않는다(`ArcheryTrajectory`가 선례 — `ArcheryWaveGenerator`·`ArcheryHitTest`도 같다).
- **World 타입 이름이 `UnityEngine`과 겹치면 풀 네임스페이스로 한정한다**
  (`GameFramework.World.Transform`, `GameFramework.World.Component`).
- **틱 레이트는 50Hz** — 60초 = 3000틱. `TickInterval = 0.02f`.
- **프로토 — 기존 id는 한 칸도 밀리면 안 된다.** 이 슬라이스는 세 가지를 더한다:
  `ArcheryHitToC`(`WorldEventToC` oneof 안의 payload, `@auto_generate` **없음** → id 없음),
  `EntitySnap.score`(필드 추가), 그리고 **`ArcheryStateToC`(새 top-level 메시지 → 새 id 하나)**.
  `generate_message_ids.sh`는 **기존 `MessageIds.cs`를 읽어 id를 보존**하고 새것만 뒤에 붙인다.
  위험한 것은 그 파일이 **지워진 채로** 도는 경우다(부모 `generate_protos.sh`가 그렇게 한다) —
  그러면 `find` 순서대로 전부 다시 매겨져 **와이어가 조용히 깨진다.**
  → **`MessageIds.cs`를 지우지 말고** 서브스크립트를 개별 실행한 뒤,
  `git diff Runtime.Generated/Scripts/MessageIds.cs`가 **`ArcheryStateToC = 19` 한 줄 추가뿐**이고
  1~18번이 그대로인지 눈으로 확인한다. 한 줄이라도 값이 바뀌었으면 되돌리고 다시 한다.
- **필드 번호를 재사용하지 않는다.** `EntitySnap`의 다음 빈 번호는 **24**(23까지 사용, 12·13은 reserved).
- **결정론 불변식(이 슬라이스의 생명줄):** 같은 `(matchSeed, waveIndex, config, kinds)`면 클라와 서버가
  **완전히 같은 과녁 목록**을 만든다. 부동소수 연산 순서를 바꾸지 말고, `DeterministicRandom`을
  **정해진 순서로만** 소비한다(개수 → 슬롯마다 종류 → 각도 → 반지름 → 높이).

---

## 새로 만들고 고치는 파일

### LeagueOfPhysical-Shared (시뮬 — 클·서 공유)

| 파일 | 책임 |
|---|---|
| `Runtime/Scripts/Game/ArcheryConfig.cs` (신규) | 웨이브 주기·개수·과녁이 뜨는 공간·과녁 종류 목록. 사이드 provider가 채운다 |
| `Runtime/Scripts/Game/ArcheryTargetKind.cs` (신규) | 과녁 종류 하나(반지름·점수·뽑힐 가중치) |
| `Runtime/Scripts/Game/ArcheryTarget.cs` (신규) | 떠 있는 과녁 하나(웨이브 번호·슬롯·위치·반지름·점수) |
| `Runtime/Scripts/Game/ArcheryWaveGenerator.cs` (신규) | **순수 커널** — `(씨앗, 웨이브)` → 과녁 목록, `tick` → 웨이브 번호 |
| `Runtime/Scripts/Game/ArcheryHitTest.cs` (신규) | **순수 커널** — 선분 대 구 교차 |
| `Runtime/Scripts/Game/ArcheryScore.cs` (신규) | 점수(데이터만). 처리는 서버가 |
| `Runtime/Scripts/Game/ArcheryTargetHitEvent.cs` (신규) | "어느 과녁이 누구에게 먹혔다"는 이산 사건 |
| `Protos/ArcheryHitToC.proto` · `Protos/ArcheryStateToC.proto` (신규) · `Protos/WorldEventToC.proto` · `Protos/EntitySnap.proto` (수정) | 와이어 — 사건 하나, **상태 하나**, 필드 하나 |
| `Runtime.Generated/Scripts/WorldEventWire.cs` (수정) | 사건 ↔ 와이어 매핑(수기 파일) |
| `Tests/EditMode/ArcheryWaveGeneratorTests.cs` · `ArcheryHitTestTests.cs` (신규) | 결정론·경계 |

### LeagueOfPhysical-Server

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Game/ArcheryConfigProvider.cs` (신규) | Luban 두 테이블 → `ArcheryConfig` |
| `Assets/Scripts/Game/ArcheryWaveState.cs` (신규) | 지금 웨이브에서 먹힌 슬롯(비트마스크) |
| `Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs` (신규) | **적중 판정 = 권위.** 점수 적립·사건 발행 |
| `Assets/Scripts/Game/TickSystems/ArcheryStateBroadcastSystem.cs` (신규) | 남은 과녁을 **상태**로 송출(재접속 대비) |
| `Assets/Scripts/Domain/ScorePlacements.cs` (신규) | 점수 → 등수(동점은 공동, 1·1·3) |
| `Assets/Scripts/Game/ArcheryRuleSystem.cs` (수정) | 점수로 순위를 낸다 |
| `Assets/Scripts/Game/ArcheryLifetimeScope.cs` (수정) | 배선 |
| `Assets/Scripts/Entity/ArcheryPlayerCreator.cs` (수정) | `ArcheryScore` 부착 |
| `Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs` (수정) | 점수를 스냅에 싣는다 |
| `Assets/Tests/Editor/ArcheryHitSystemTests.cs` · `ScorePlacementsTests.cs` (신규) | |

### LeagueOfPhysical-Client

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Game/ArcheryConfigProvider.cs` (신규) | 서버와 **같은 값**을 읽는다(다르면 다른 과녁을 본다) |
| `Assets/Scripts/Game/ArcheryTargetView.cs` (신규) | 떠 있는 과녁을 그린다 |
| `Assets/Scripts/Game/MessageHandler/ArcheryStateHandler.cs` (신규) | 사라진 **과녁**을 상태로 받는다 |
| `Assets/Scripts/Game/MessageHandler/ArcheryHitHandler.cs` (신규) | 박힌 **화살**과 "+N" 연출을 사건으로 받는다 |
| `Assets/Scripts/Netcode/EntitySnap.cs` (수정) · `MessageHandler/GameEntityMessageHandler.cs` (수정) | 점수 수신 |
| `Assets/Scripts/Entity/ArcheryPlayerCreator.cs` (수정) · `Game/ArcheryLifetimeScope.cs` (수정) | 배선 |
| `Assets/Scripts/UI/ArcheryPad/*` · `Assets/UI/ArcheryPad/*` (수정) | 점수 라벨 |
| `Assets/Art/Scenes/ArcheryCircleMap.unity` (수정, **Art 레포**) | `CenterStage` 콜라이더 정리 |

### infrastructure / MasterData

| 파일 | 책임 |
|---|---|
| `table/Datas/#ArcheryConfig.xlsx` · `#ArcheryTarget.xlsx` (신규) · `Datas/__tables__.xlsx` (수정) | 튜닝 데이터 |
| `LeagueOfPhysical-MasterData-{Client,Server}/Runtime/Scripts/LOPMasterData.cs` (수정) | **로더 목록** — 빠뜨리면 에러 없이 테이블만 비어 온다 |

---

## 설계 결정 (착수 전에 못박는다)

### 1. 웨이브 번호는 틱만으로 나온다

```
waveIndex(tick) = (tick - GameplayStartTick) / WavePeriodTicks      // 출발 전이면 -1
waveStartTick(i) = GameplayStartTick + i * WavePeriodTicks
```

`GameplayStartTick`은 **양쪽이 이미 갖고 있다** — 서버는 `MatchStartSystem`이 `world.GameplayStartTick`에
쓰고, 클라는 `MatchStartMessageHandler`가 같은 자리에 쓴다. 아직 모르면 `long.MaxValue`이므로
그때는 웨이브가 없다(−1).

**주기는 고정값 하나다.** 스펙의 "1.5~2초"를 웨이브마다 난수로 흔들면 `waveIndex(tick)`이 나눗셈
한 번으로 안 나오고 처음부터 훑어야 한다 — 매 프레임 도는 계산이라 그 대가가 크다. 주기를 흔들고
싶어지면 그때 **누적 합 테이블**을 캐시하는 식으로 넓힌다.

### 2. 과녁은 통신하지 않는다 — 그런데 "남은 과녁"은 **상태**로 보낸다

| | 어떻게 | 왜 |
|---|---|---|
| 과녁이 **뜨는 것** | 양쪽이 씨앗으로 계산 | 핑 낮은 사람이 먼저 보면 안 된다(스펙 §7.1) |
| **지금 어느 과녁이 남았나** | **상태**(`ArcheryStateToC` — 웨이브 번호 + 먹힌 슬롯 비트마스크) | 아래 |
| **누가 몇 점을 먹었나** | **사건**(`ArcheryHitToC`) | 귀속·연출("+2")은 상태로 복원할 수 없다 |
| **점수** | **엔티티 스냅샷**(`EntitySnap.score`) | durable — 유실돼도 다음 스냅이 덮는다 |

**왜 사건 하나로는 안 되나 (처음에 그렇게 썼다가 고친 자리).**
사건은 실제로 **reliable로 간다**(`LOPSession.Send`의 기본값) — 그래서 *연결되어 있는 동안에는*
아무도 안 놓친다. 구멍은 **끊겼다 돌아온 사람과 늦게 들어온 사람**이다. 미러는 새 연결에 지난
reliable 메시지를 다시 틀어 주지 않으므로, 그 사람은 **이미 먹힌 과녁을 살아 있는 것으로 본다.**
스스로는 틀렸다는 것조차 알 수 없고(사라진 것은 아무 흔적도 안 남긴다), 다음 웨이브가 올 때까지
최대 1.76초 동안 헛화살을 쏜다. 이것이 아키텍처 문서가 묻는 *"잃으면 스스로 못 고치나?"* 다 —
그러면 **상태**여야 한다.

**이 프로젝트가 같은 함정을 이미 한 번 밟았다.** `PanchigiTurnSystem.BroadcastIfChanged`의 주석이
그 자리다 — *"나중에 접속(또는 재접속)한 세션도 다음 틱에 반드시 현재 상태를 받는다"*, 그리고
*"재접속은 같은 sessionId를 그대로 다시 쓰기 때문에, 받은 기록을 지워 두지 않으면 돌아온
플레이어가 자기 차례를 통째로 놓친다."* **그 패턴을 그대로 쓴다.**

**값이 아주 작다.** 한 웨이브에 과녁이 최대 3개라 "먹힌 슬롯"은 **비트마스크 하나**다 —
`wave_index`와 합쳐 8바이트. 게다가 **마스크가 0이 아닐 때만** 보낸다(0은 클라의 기본값이라 보낼
것이 없다). 한 판에 많아야 웨이브 수만큼이다.

> **HP와 정확히 같은 갈라짐이다** — HP도 *값*은 스냅샷이고 데미지 숫자·크리·회피는 연출 이벤트다.
> 여기서는 *남은 과녁*이 값이고 *누가 먹었나*가 연출이다.

### 3. 먹힌 기록은 월드 밖에 둔다 (되감기와 안 엉키게)

`ArcheryWorld`는 **한 줄도 고치지 않는다.** 기록은 각 사이드가 자기 자리에 든다:

- **서버**: `ArcheryWaveState`(작은 값 보관소). `ArcheryHitSystem`이 쓰고
  `ArcheryStateBroadcastSystem`이 읽어 내보낸다. 서버는 되감지 않으므로 그냥 필드다.
- **클라**: `ArcheryConsumed`. **과녁은 상태 메시지가 채우고**, 화살은 사건이 채운다.

**월드의 `SaveGameState`/`LoadGameState`에 넣으면 안 된다.** 넣으면 되감을 때 *서버가 확정한 사실*이
옛 값으로 되돌아가 먹힌 과녁이 되살아난다. 서버 확정 사실은 예측 대상이 아니므로 롤백 밖이 맞다.

### 3-b. 화살은 왜 상태가 아니어도 되나

"이 화살은 이미 박혔다"는 기록은 **사건으로 충분하다.** 화살은 3초면 사라지는 transient이고,
재접속한 사람에게는 애초에 **날아가던 남의 화살이 하나도 없다** — 남의 발사도 사건으로 오므로,
그가 없던 동안 떠난 화살은 그의 세계에 존재한 적이 없다. 없는 화살을 "지워야 할 목록"에 넣을 일이
없다. 과녁과 갈리는 지점이 정확히 이것이다 — **과녁은 계산으로 되살아나지만 화살은 안 되살아난다.**

### 4. 같은 틱에 두 화살이 같은 과녁에 닿으면

**먼저 닿은 화살이 먹는다.** 이번 틱의 후보 적중을 다 모은 뒤 `(닿은 시각, 쏜 사람 id)` 순으로 정렬해
차례로 적용하고, 이미 먹힌 과녁은 건너뛴다. 발견하는 대로 즉시 적용하면 **엔티티 순회 순서**가
승자를 정하게 된다 — 아키텍처 문서의 "모으고 → 적용하고 → 판정한다"가 말하는 바로 그 함정이다.

### 5. 적중 사건은 한 틱 늦게 나간다 (알고 받아들인다)

러너의 틱 순서는 `world.Tick → … → worldEventDrainSystem → RunPhase<End>`다. `ArcheryHitSystem`은
화살이 생긴 **뒤**에 돌아야 하므로 `End`에 물리고, 그러면 그 틱에 쌓은 사건은 **다음 틱**에 나간다
(20ms). 점수는 어차피 스냅샷으로 가고, 20ms는 사람이 못 느낀다. 이걸 없애려면 러너의 틱 순서를
바꿔야 하는데 그건 모든 모드에 걸리는 변경이라 이 슬라이스의 몫이 아니다.

### 6. 과녁이 뜨는 공간은 **원점 기준**이다

원형맵의 무대가 원점에 있어서 성립한다. 직선맵(슬라이스 4)은 앞쪽 공간이라 **맵이 알려 주는 공간**이
필요해진다 — 그때 `WindVolume`/`LaserVolume` 같은 맵 마커 관례를 따른다. 지금 그 추상을 미리 짓지
않는다.

### 7. 이월된 것 처리 — `CenterStage` 콜라이더

무대가 원기둥(반지름 2m, 높이 0.4m)인데 콜라이더가 **캡슐**(스케일이 먹어 사실상 지름 4m 구)이다.
과녁이 무대 위에 뜨기 시작하는 슬라이스이므로 여기서 **볼록 메시 콜라이더**로 바꿔 보이는 모양과
맞춘다. (판정 자체는 우리 계산이라 이 콜라이더를 쓰지 않지만, 보이는 것과 다른 충돌체는 언젠가
반드시 누군가를 속인다.)

---

## Task 1: 마스터데이터 두 테이블

**Files:**
- Create: `infrastructure/table/Datas/#ArcheryConfig.xlsx`
- Create: `infrastructure/table/Datas/#ArcheryTarget.xlsx`
- Modify: `infrastructure/table/Datas/__tables__.xlsx`
- Modify: `LeagueOfPhysical-MasterData-Client/Runtime/Scripts/LOPMasterData.cs`
- Modify: `LeagueOfPhysical-MasterData-Server/Runtime/Scripts/LOPMasterData.cs`
- (생성물) `LeagueOfPhysical-MasterData-{Client,Server}/Runtime.Generated/**`

**Interfaces:**
- Produces: Luban 테이블 `TbArcheryConfig`(단일 행 id=1), `TbArcheryTarget`(종류 목록).
  이후 태스크의 provider가 `md.Tables.TbArcheryConfig.GetOrDefault(1)`와
  `md.Tables.TbArcheryTarget.DataList`로 읽는다.

- [ ] **Step 1: 세 레포에 브랜치를 판다**

```bash
for r in infrastructure LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server; do
  git -C "C:/Users/re5na/workspace/LOP/$r" fetch origin
  git -C "C:/Users/re5na/workspace/LOP/$r" checkout -b feature/archery-slice2 origin/main
done
```

- [ ] **Step 2: `#ArcheryConfig.xlsx`를 만든다**

Luban의 Excel-embedded 형식이다. 네 줄의 머리(`##var` / `##type` / `##group` / `##`) 다음에 데이터.
`##group`이 비어 있으면 클·서 양쪽에 나간다(둘 다 같은 과녁을 만들어야 하므로 **반드시 비워 둔다**).

```python
# infrastructure/table 에서 실행
import openpyxl
wb = openpyxl.Workbook(); ws = wb.active
cols = ["id", "wave_period_ticks", "min_targets", "max_targets",
        "spawn_radius", "spawn_min_y", "spawn_max_y", "min_separation"]
types = ["int", "int", "int", "int", "float", "float", "float", "float"]
ws.append(["##var"] + cols)
ws.append(["##type"] + types)
ws.append(["##group"] + [None] * len(cols))
ws.append(["##"] + cols)
#  88틱 = 1.76초(50Hz) — 스펙의 1.5~2초 안. 과녁은 2~3개(인원과 무관).
#  무대 반지름이 2m라 spawn_radius도 2 — 무대 바깥 허공에 뜨지 않게.
#  높이 2~6m: 무대 윗면(0.4m)보다 확실히 위이고, 사수 눈높이(1.4m)에서 올려다보는 범위.
#  min_separation 1.2m: 가장 큰 과녁 둘(반지름 0.6)이 겨우 안 겹치는 거리.
ws.append([None, 1, 88, 2, 3, 2.0, 2.0, 6.0, 1.2])
wb.save("Datas/#ArcheryConfig.xlsx")
```

- [ ] **Step 3: `#ArcheryTarget.xlsx`를 만든다**

```python
import openpyxl
wb = openpyxl.Workbook(); ws = wb.active
cols = ["id", "code", "radius", "points", "weight"]
types = ["int", "string", "float", "int", "int"]
ws.append(["##var"] + cols)
ws.append(["##type"] + types)
ws.append(["##group"] + [None] * len(cols))
ws.append(["##"] + cols)
#  작을수록 비싸다(스펙 §3). weight는 뽑힐 상대 비율 — 합이 100일 필요는 없다.
#  **id 오름차순으로 적는다** — 이 순서가 곧 가중치 뽑기의 기준 순서다.
ws.append([None, 1, "Big",    0.60, 1, 50])
ws.append([None, 2, "Medium", 0.40, 2, 35])
ws.append([None, 3, "Small",  0.25, 4, 15])
wb.save("Datas/#ArcheryTarget.xlsx")
```

- [ ] **Step 4: `__tables__.xlsx`에 두 줄을 더한다**

기존 줄과 **같은 열 구성**이어야 한다(`##var | full_name | value_type | read_schema_from_file |
input | index | mode | group | comment | tags | output`). `group`은 비운다.

**`value_type`을 `ArcheryTargetKind`로 둔다** — 한 줄이 "떠 있는 과녁 하나"가 아니라 *종류*라서
그 이름이 사실에 맞고, 덤으로 Shared의 `LOP.ArcheryTarget`(떠 있는 과녁)과 이름이 안 겹친다.
겹쳐 두면 provider 파일 안에서 둘을 늘 풀네임으로 구분해야 한다.

```python
import openpyxl
wb = openpyxl.load_workbook("Datas/__tables__.xlsx"); ws = wb.active
ws.append([None, "TbArcheryConfig", "ArcheryConfig", True, "#ArcheryConfig.xlsx",
           "id", "map", None, "ArcheryConfig(활쏘기 웨이브 튜닝)", None, None])
ws.append([None, "TbArcheryTarget", "ArcheryTargetKind", True, "#ArcheryTarget.xlsx",
           "id", "map", None, "ArcheryTargetKind(과녁 종류)", None, None])
wb.save("Datas/__tables__.xlsx")
```

- [ ] **Step 5: 굽는다**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table && ./gen.sh
```

`[done]`이 나와야 한다. 생성물이 셋으로 간다(클·서 패키지 + 매치메이킹). 새 테이블은 `group`이
비어 있어 **매치메이킹 타깃에는 안 나간다** — 매칭은 이 값을 쓰지 않으므로 맞다.

- [ ] **Step 6: 로더 목록을 갱신한다 (빠뜨리면 에러 없이 테이블만 빈다)**

두 패키지의 `Runtime/Scripts/LOPMasterData.cs`에서 `TableFiles` 배열 끝에 더한다:

```csharp
            "tbpanchigiconfig", "tbpanchigisetup", "tbskydiveconfig",
            "tbarcheryconfig", "tbarcherytarget"
        };
```

- [ ] **Step 7: 생성된 `.bytes`/`.cs`가 실제로 나왔는지 눈으로 확인한다**

```bash
ls C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime.Generated/StreamingAssets/MasterData/ | grep archery
ls C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/StreamingAssets/MasterData/ | grep archery
```
기대: 각각 `tbarcheryconfig.bytes`, `tbarcherytarget.bytes`.

- [ ] **Step 8: 커밋 (세 레포 각각)**

`.meta`는 유니티가 새로 만든다 — **에디터가 한 번 훑고 난 뒤에** `.meta`까지 포함해 커밋한다.
`gen.sh`가 지웠다 되살린 `.meta`가 "삭제됨"으로 남아 있지 않은지 `git status --short`로 확인한다.

```bash
git -C .../infrastructure add table/Datas/#ArcheryConfig.xlsx table/Datas/#ArcheryTarget.xlsx table/Datas/__tables__.xlsx
git -C .../infrastructure status --short
git -C .../infrastructure commit -m "feat(masterdata): 활쏘기 웨이브·과녁 종류 테이블을 만든다"
# MasterData-Client / -Server 도 각각 Runtime.Generated 와 LOPMasterData.cs 만 지정해 커밋
```

---

## Task 2: Shared — 설정 값 객체와 웨이브 생성 커널

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryTargetKind.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryConfig.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryTarget.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryWaveGenerator.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/ArcheryWaveGeneratorTests.cs`

**Interfaces:**
- Consumes: `GameFramework.Rng.DeterministicRandom`, `GameFramework.Rng.Hashing`.
- Produces:
  - `ArcheryTargetKind(float Radius, int Points, int Weight)` — `readonly struct`
  - `ArcheryConfig` — `sealed class`, 생성자
    `ArcheryConfig(int wavePeriodTicks, int minTargets, int maxTargets, float spawnRadius, float spawnMinY, float spawnMaxY, float minSeparation, IReadOnlyList<ArcheryTargetKind> kinds)`,
    같은 이름의 읽기 전용 속성들(`WavePeriodTicks`, `MinTargets`, `MaxTargets`, `SpawnRadius`,
    `SpawnMinY`, `SpawnMaxY`, `MinSeparation`, `Kinds`)
  - `ArcheryTarget(int WaveIndex, int SlotIndex, Vector3 Center, float Radius, int Points)` — `readonly struct`
  - `static class ArcheryWaveGenerator`:
    - `int WaveIndexAt(long tick, long gameplayStartTick, ArcheryConfig config)` — 출발 전이면 −1
    - `long WaveStartTick(int waveIndex, long gameplayStartTick, ArcheryConfig config)`
    - `void Fill(List<ArcheryTarget> into, ulong matchSeed, int waveIndex, ArcheryConfig config)`

- [ ] **Step 1: LOP-Shared에 브랜치를 판다**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared fetch origin
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared checkout -b feature/archery-slice2 origin/main
```

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/ArcheryWaveGeneratorTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryWaveGeneratorTests
    {
        private static ArcheryConfig Config()
        {
            return new ArcheryConfig(
                wavePeriodTicks: 88, minTargets: 2, maxTargets: 3,
                spawnRadius: 2f, spawnMinY: 2f, spawnMaxY: 6f, minSeparation: 1.2f,
                kinds: new[]
                {
                    new ArcheryTargetKind(0.60f, 1, 50),
                    new ArcheryTargetKind(0.40f, 2, 35),
                    new ArcheryTargetKind(0.25f, 4, 15),
                });
        }

        [Test]
        public void 출발_전에는_웨이브가_없다()
        {
            Assert.AreEqual(-1, ArcheryWaveGenerator.WaveIndexAt(500, 1000, Config()));
        }

        [Test]
        public void 출발틱이_아직_안_정해졌으면_웨이브가_없다()
        {
            Assert.AreEqual(-1, ArcheryWaveGenerator.WaveIndexAt(500, long.MaxValue, Config()));
        }

        [Test]
        public void 웨이브는_주기마다_한_칸씩_오른다()
        {
            var config = Config();
            Assert.AreEqual(0, ArcheryWaveGenerator.WaveIndexAt(1000, 1000, config));
            Assert.AreEqual(0, ArcheryWaveGenerator.WaveIndexAt(1087, 1000, config));
            Assert.AreEqual(1, ArcheryWaveGenerator.WaveIndexAt(1088, 1000, config));
            Assert.AreEqual(2, ArcheryWaveGenerator.WaveIndexAt(1176, 1000, config));
        }

        [Test]
        public void 웨이브_시작틱은_웨이브_번호의_역함수다()
        {
            var config = Config();
            for (int i = 0; i < 30; i++)
            {
                long start = ArcheryWaveGenerator.WaveStartTick(i, 1000, config);
                Assert.AreEqual(i, ArcheryWaveGenerator.WaveIndexAt(start, 1000, config));
            }
        }

        // 이 게임의 생명줄이다 — 깨지면 사람마다 다른 과녁을 보는데 화면은 멀쩡해 보이고 점수만 이상해진다.
        [Test]
        public void 같은_씨앗과_웨이브는_언제_몇_번_물어도_같은_과녁을_준다()
        {
            var config = Config();
            var a = new List<ArcheryTarget>();
            var b = new List<ArcheryTarget>();

            for (int wave = 0; wave < 40; wave++)
            {
                ArcheryWaveGenerator.Fill(a, 0xC0FFEEUL, wave, config);
                ArcheryWaveGenerator.Fill(b, 0xC0FFEEUL, wave, config);

                Assert.AreEqual(a.Count, b.Count, $"wave {wave}");
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.AreEqual(a[i].SlotIndex, b[i].SlotIndex);
                    Assert.AreEqual(a[i].Radius, b[i].Radius);
                    Assert.AreEqual(a[i].Points, b[i].Points);
                    //  부동소수도 *완전히* 같아야 한다 — 근사 비교로 두면 갈라지는 순간을 못 잡는다.
                    Assert.AreEqual(a[i].Center.x, b[i].Center.x);
                    Assert.AreEqual(a[i].Center.y, b[i].Center.y);
                    Assert.AreEqual(a[i].Center.z, b[i].Center.z);
                }
            }
        }

        [Test]
        public void 씨앗이_다르면_다른_과녁이_나온다()
        {
            var config = Config();
            var a = new List<ArcheryTarget>();
            var b = new List<ArcheryTarget>();
            ArcheryWaveGenerator.Fill(a, 1UL, 0, config);
            ArcheryWaveGenerator.Fill(b, 2UL, 0, config);
            Assert.AreNotEqual(a[0].Center, b[0].Center);
        }

        [Test]
        public void 웨이브가_다르면_다른_과녁이_나온다()
        {
            var config = Config();
            var a = new List<ArcheryTarget>();
            var b = new List<ArcheryTarget>();
            ArcheryWaveGenerator.Fill(a, 7UL, 0, config);
            ArcheryWaveGenerator.Fill(b, 7UL, 1, config);
            Assert.AreNotEqual(a[0].Center, b[0].Center);
        }

        [Test]
        public void 과녁은_정해진_공간_안에만_뜬다()
        {
            var config = Config();
            var targets = new List<ArcheryTarget>();
            for (int wave = 0; wave < 200; wave++)
            {
                ArcheryWaveGenerator.Fill(targets, 42UL, wave, config);
                Assert.That(targets.Count, Is.InRange(config.MinTargets, config.MaxTargets));
                for (int i = 0; i < targets.Count; i++)
                {
                    var c = targets[i].Center;
                    float horizontal = new Vector2(c.x, c.z).magnitude;
                    Assert.LessOrEqual(horizontal, config.SpawnRadius + 1e-4f);
                    Assert.That(c.y, Is.InRange(config.SpawnMinY, config.SpawnMaxY));
                    Assert.AreEqual(wave, targets[i].WaveIndex);
                    Assert.AreEqual(i, targets[i].SlotIndex);
                }
            }
        }

        [Test]
        public void 같은_웨이브의_과녁끼리는_겹치지_않는다()
        {
            var config = Config();
            var targets = new List<ArcheryTarget>();
            for (int wave = 0; wave < 200; wave++)
            {
                ArcheryWaveGenerator.Fill(targets, 99UL, wave, config);
                for (int i = 0; i < targets.Count; i++)
                {
                    for (int j = i + 1; j < targets.Count; j++)
                    {
                        float gap = Vector3.Distance(targets[i].Center, targets[j].Center);
                        float touching = targets[i].Radius + targets[j].Radius;
                        Assert.Greater(gap, touching, $"wave {wave}: {i}과 {j}가 겹친다");
                    }
                }
            }
        }

        [Test]
        public void 목록을_다시_채우면_앞의_것이_남지_않는다()
        {
            var config = Config();
            var targets = new List<ArcheryTarget> { default, default, default, default, default };
            ArcheryWaveGenerator.Fill(targets, 5UL, 0, config);
            Assert.That(targets.Count, Is.InRange(config.MinTargets, config.MaxTargets));
        }
    }
}
```

- [ ] **Step 3: 테스트가 *컴파일 실패*로 떨어지는 것을 확인한다**

Unity Test Runner(EditMode) 또는 `unity command --project-path <client> run_tests --mode EditMode`.
기대: `ArcheryWaveGenerator`가 없다는 컴파일 에러.

- [ ] **Step 4: 값 객체 셋을 만든다**

`ArcheryTargetKind.cs`:

```csharp
namespace LOP
{
    /// <summary>과녁 한 종류. 작을수록 맞히기 어렵고 그만큼 비싸다.</summary>
    public readonly struct ArcheryTargetKind
    {
        /// <summary>맞았다고 칠 반경(m).</summary>
        public readonly float Radius;

        /// <summary>맞히면 받는 점수.</summary>
        public readonly int Points;

        /// <summary>뽑힐 상대 비율. 합이 100일 필요는 없다 — 서로의 크기만 의미가 있다.</summary>
        public readonly int Weight;

        public ArcheryTargetKind(float radius, int points, int weight)
        {
            Radius = radius;
            Points = points;
            Weight = weight;
        }
    }
}
```

`ArcheryConfig.cs`:

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// Archery 튜닝값. MasterData(<c>TbArcheryConfig</c>/<c>TbArcheryTarget</c>)에서 사이드 provider가
    /// 채워 시뮬에 넘긴다(Shared는 MasterData 패키지를 참조하지 않는다 — <see cref="SkydiveConfig"/>와 같은 짝).
    ///
    /// <para><b>클·서가 반드시 같은 값을 들어야 한다.</b> 하나라도 다르면 웨이브 계산이 갈려
    /// 서로 다른 과녁을 보게 되는데, 화면은 멀쩡해 보이고 점수만 이상해진다.</para>
    /// </summary>
    public sealed class ArcheryConfig
    {
        /// <summary>웨이브 하나가 서 있는 틱 수. 과녁 수명도 이것과 같다 — 다음 웨이브가 오면 사라진다.</summary>
        public int WavePeriodTicks { get; }

        /// <summary>한 웨이브의 최소 과녁 수.</summary>
        public int MinTargets { get; }

        /// <summary>한 웨이브의 최대 과녁 수(이 값 포함).</summary>
        public int MaxTargets { get; }

        /// <summary>과녁이 뜨는 원기둥의 반지름(m). 가운데는 원점이다.</summary>
        public float SpawnRadius { get; }

        /// <summary>과녁이 뜨는 가장 낮은 높이(m).</summary>
        public float SpawnMinY { get; }

        /// <summary>과녁이 뜨는 가장 높은 높이(m).</summary>
        public float SpawnMaxY { get; }

        /// <summary>같은 웨이브의 과녁 중심끼리 최소한 이만큼은 떨어뜨린다(m).</summary>
        public float MinSeparation { get; }

        /// <summary>뽑을 수 있는 과녁 종류. 비어 있으면 과녁이 안 뜬다.</summary>
        public IReadOnlyList<ArcheryTargetKind> Kinds { get; }

        public ArcheryConfig(int wavePeriodTicks, int minTargets, int maxTargets,
                             float spawnRadius, float spawnMinY, float spawnMaxY, float minSeparation,
                             IReadOnlyList<ArcheryTargetKind> kinds)
        {
            WavePeriodTicks = wavePeriodTicks;
            MinTargets = minTargets;
            MaxTargets = maxTargets;
            SpawnRadius = spawnRadius;
            SpawnMinY = spawnMinY;
            SpawnMaxY = spawnMaxY;
            MinSeparation = minSeparation;
            Kinds = kinds;
        }
    }
}
```

`ArcheryTarget.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 지금 떠 있는 과녁 하나. <b>엔티티가 아니다</b> — 화살과 같은 취급으로, 씨앗과 웨이브 번호만
    /// 있으면 양쪽이 각자 계산해 낸다. <see cref="WaveIndex"/>+<see cref="SlotIndex"/>가 이름 노릇을
    /// 해서 "어느 과녁이 먹혔다"를 그 두 숫자로 가리킬 수 있다.
    /// </summary>
    public readonly struct ArcheryTarget
    {
        public readonly int WaveIndex;
        public readonly int SlotIndex;
        public readonly Vector3 Center;
        public readonly float Radius;
        public readonly int Points;

        public ArcheryTarget(int waveIndex, int slotIndex, Vector3 center, float radius, int points)
        {
            WaveIndex = waveIndex;
            SlotIndex = slotIndex;
            Center = center;
            Radius = radius;
            Points = points;
        }
    }
}
```

- [ ] **Step 5: 생성 커널을 만든다**

`ArcheryWaveGenerator.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 과녁이 언제 몇 개 어디에 뜨는지를 계산한다. 상태가 없는 순수 계산이라 클·서가 같은 입력을
    /// 넣으면 같은 답을 얻는다 — <b>그래서 과녁을 통신으로 보낼 필요가 없다</b>(핑 낮은 사람이 먼저
    /// 보는 일이 없다).
    /// </summary>
    public static class ArcheryWaveGenerator
    {
        // 겹치지 않는 자리를 못 찾아도 영원히 돌면 안 된다. 이 횟수를 넘기면 마지막 후보를 그냥 쓴다 —
        // 아주 드물게 두 과녁이 가깝게 뜨는 편이, 판이 멈추는 것보다 낫다.
        private const int MaxPlacementTries = 12;

        /// <summary>이 틱에 서 있는 웨이브 번호. 아직 출발 전이면 −1이다.</summary>
        public static int WaveIndexAt(long tick, long gameplayStartTick, ArcheryConfig config)
        {
            if (gameplayStartTick == long.MaxValue || tick < gameplayStartTick || config.WavePeriodTicks <= 0)
            {
                return -1;
            }
            return (int)((tick - gameplayStartTick) / config.WavePeriodTicks);
        }

        /// <summary>그 웨이브가 뜨는 틱. <see cref="WaveIndexAt"/>의 역이다.</summary>
        public static long WaveStartTick(int waveIndex, long gameplayStartTick, ArcheryConfig config)
        {
            return gameplayStartTick + (long)waveIndex * config.WavePeriodTicks;
        }

        /// <summary>
        /// 그 웨이브의 과녁을 <paramref name="into"/>에 채운다(먼저 비운다).
        /// <b>난수를 꺼내는 순서가 곧 계약이다</b> — 개수 → 슬롯마다 (종류 → 각도 → 반지름 → 높이).
        /// 이 순서를 바꾸면 같은 씨앗이 다른 과녁을 내놓아 클·서가 갈린다.
        /// </summary>
        public static void Fill(List<ArcheryTarget> into, ulong matchSeed, int waveIndex, ArcheryConfig config)
        {
            into.Clear();
            if (waveIndex < 0 || config.Kinds == null || config.Kinds.Count == 0)
            {
                return;
            }

            var rng = new GameFramework.Rng.DeterministicRandom(
                GameFramework.Rng.Hashing.Combine(matchSeed, (ulong)(long)waveIndex));

            int count = rng.Range(config.MinTargets, config.MaxTargets + 1);
            for (int slot = 0; slot < count; slot++)
            {
                ArcheryTargetKind kind = PickKind(config.Kinds, ref rng);
                Vector3 center = PickCenter(into, config, ref rng);
                into.Add(new ArcheryTarget(waveIndex, slot, center, kind.Radius, kind.Points));
            }
        }

        private static ArcheryTargetKind PickKind(IReadOnlyList<ArcheryTargetKind> kinds,
                                                  ref GameFramework.Rng.DeterministicRandom rng)
        {
            int total = 0;
            for (int i = 0; i < kinds.Count; i++)
            {
                total += Mathf.Max(kinds[i].Weight, 0);
            }
            if (total <= 0)
            {
                return kinds[0];   // 가중치를 다 0으로 넣어 둔 데이터 — 첫 종류로 버틴다
            }

            int roll = rng.Range(0, total);
            for (int i = 0; i < kinds.Count; i++)
            {
                roll -= Mathf.Max(kinds[i].Weight, 0);
                if (roll < 0)
                {
                    return kinds[i];
                }
            }
            return kinds[kinds.Count - 1];
        }

        // 이미 놓인 과녁과 너무 가까우면 다시 뽑는다. 난수 소비 횟수가 자리마다 달라지지만,
        // "앞 슬롯이 어디 놓였나"까지 양쪽이 똑같이 알고 있으므로 결과는 여전히 결정론적이다.
        private static Vector3 PickCenter(List<ArcheryTarget> placed, ArcheryConfig config,
                                          ref GameFramework.Rng.DeterministicRandom rng)
        {
            Vector3 candidate = default;
            for (int attempt = 0; attempt < MaxPlacementTries; attempt++)
            {
                float angle = rng.Range(0f, Mathf.PI * 2f);
                // 제곱근을 씌워야 원판 위에 고르게 퍼진다 — 그냥 뽑으면 가운데로 몰린다.
                float radius = config.SpawnRadius * Mathf.Sqrt(rng.NextFloat01());
                float y = rng.Range(config.SpawnMinY, config.SpawnMaxY);
                candidate = new Vector3(Mathf.Sin(angle) * radius, y, Mathf.Cos(angle) * radius);

                if (FarEnough(candidate, placed, config.MinSeparation))
                {
                    return candidate;
                }
            }
            return candidate;
        }

        private static bool FarEnough(Vector3 candidate, List<ArcheryTarget> placed, float minSeparation)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                if (Vector3.Distance(candidate, placed[i].Center) < minSeparation)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
```

- [ ] **Step 6: 테스트가 통과하는지 본다**

특히 `같은_웨이브의_과녁끼리는_겹치지_않는다`가 200웨이브에서 다 통과해야 한다. 떨어지면
`MinSeparation`(1.2m)이 가장 큰 과녁 둘의 반지름 합(1.2m)과 **같아서** 경계에서 걸리는 것이다 —
데이터를 1.3으로 올리지 말고 **`MaxPlacementTries`가 소진돼 그냥 쓴 경우**인지 먼저 확인할 것.
소진이 원인이면 `MinSeparation`을 1.4로 올린다(공간이 충분하다: 반지름 2m × 높이 4m).

- [ ] **Step 7: 커밋**

```bash
git -C .../LeagueOfPhysical-Shared add Runtime/Scripts/Game/Archery{TargetKind,Config,Target}.cs Runtime/Scripts/Game/ArcheryWaveGenerator.cs Tests/EditMode/ArcheryWaveGeneratorTests.cs
# .meta 도 함께 (에디터가 만든 뒤)
git -C .../LeagueOfPhysical-Shared status --short
git -C .../LeagueOfPhysical-Shared commit -m "feat(archery): 과녁 웨이브를 씨앗으로 계산하는 순수 커널을 만든다"
```

---

## Task 3: Shared — 선분 대 구 적중 판정 커널

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryHitTest.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/ArcheryHitTestTests.cs`

**Interfaces:**
- Produces: `static class ArcheryHitTest`
  - `static bool SegmentHitsSphere(Vector3 from, Vector3 to, Vector3 center, float radius, out float t)`
    — `t`는 0~1, 선분 위에서 처음 닿은 지점의 비율. 시작점이 이미 구 안이면 `t = 0`으로 참.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryHitTestTests
    {
        [Test]
        public void 한가운데를_지나면_맞는다()
        {
            bool hit = ArcheryHitTest.SegmentHitsSphere(
                new Vector3(-5f, 0f, 0f), new Vector3(5f, 0f, 0f),
                Vector3.zero, 1f, out float t);

            Assert.IsTrue(hit);
            //  −5에서 출발해 반지름 1인 구의 앞면(−1)에 닿는다 → 10 중 4 = 0.4
            Assert.AreEqual(0.4f, t, 1e-4f);
        }

        [Test]
        public void 스쳐_지나가면_안_맞는다()
        {
            bool hit = ArcheryHitTest.SegmentHitsSphere(
                new Vector3(-5f, 2f, 0f), new Vector3(5f, 2f, 0f),
                Vector3.zero, 1f, out _);
            Assert.IsFalse(hit);
        }

        [Test]
        public void 선분이_구에_닿기_전에_끝나면_안_맞는다()
        {
            bool hit = ArcheryHitTest.SegmentHitsSphere(
                new Vector3(-5f, 0f, 0f), new Vector3(-2f, 0f, 0f),
                Vector3.zero, 1f, out _);
            Assert.IsFalse(hit);
        }

        [Test]
        public void 구를_이미_지나쳐_버린_선분은_안_맞는다()
        {
            bool hit = ArcheryHitTest.SegmentHitsSphere(
                new Vector3(2f, 0f, 0f), new Vector3(5f, 0f, 0f),
                Vector3.zero, 1f, out _);
            Assert.IsFalse(hit);
        }

        // 한 틱이 20ms라 빠른 화살(65m/s)은 한 틱에 1.3m를 간다 — 지름 0.5m짜리 작은 과녁을
        // 점으로 검사하면 통째로 뚫고 지나간다. 선분으로 재는 이유가 이것이다.
        [Test]
        public void 한_틱에_통과해_버릴_작은_과녁도_잡는다()
        {
            bool hit = ArcheryHitTest.SegmentHitsSphere(
                new Vector3(0f, 0f, -0.7f), new Vector3(0f, 0f, 0.7f),
                Vector3.zero, 0.25f, out float t);
            Assert.IsTrue(hit);
            Assert.That(t, Is.InRange(0f, 1f));
        }

        [Test]
        public void 시작점이_이미_구_안이면_바로_맞는다()
        {
            bool hit = ArcheryHitTest.SegmentHitsSphere(
                Vector3.zero, new Vector3(5f, 0f, 0f), Vector3.zero, 1f, out float t);
            Assert.IsTrue(hit);
            Assert.AreEqual(0f, t, 1e-6f);
        }

        [Test]
        public void 길이가_0인_선분은_그_점이_구_안일_때만_맞는다()
        {
            Assert.IsTrue(ArcheryHitTest.SegmentHitsSphere(
                Vector3.zero, Vector3.zero, Vector3.zero, 1f, out _));
            Assert.IsFalse(ArcheryHitTest.SegmentHitsSphere(
                new Vector3(9f, 0f, 0f), new Vector3(9f, 0f, 0f), Vector3.zero, 1f, out _));
        }
    }
}
```

- [ ] **Step 2: 테스트가 컴파일 실패로 떨어지는 것을 확인한다**

- [ ] **Step 3: 커널을 만든다**

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 화살이 이번 틱에 지나온 <b>선분</b>이 과녁 구를 스쳤는지 본다. 매 틱의 점만 검사하면 빠른
    /// 화살이 작은 과녁을 통째로 뚫고 지나간다(65m/s면 한 틱에 1.3m — 지름 0.5m 과녁보다 길다).
    /// 상태 없는 순수 계산이다.
    /// </summary>
    public static class ArcheryHitTest
    {
        /// <summary>
        /// 맞았으면 참이고 <paramref name="t"/>에 <b>처음 닿은 지점</b>이 선분 위 어디인지(0~1) 담는다.
        /// 시작점이 이미 구 안이면 0이다.
        /// </summary>
        public static bool SegmentHitsSphere(Vector3 from, Vector3 to, Vector3 center, float radius, out float t)
        {
            t = 0f;

            Vector3 toCenter = from - center;
            //  시작점이 이미 안에 있으면 더 볼 것이 없다 — 바로 닿은 것이다.
            if (toCenter.sqrMagnitude <= radius * radius)
            {
                return true;
            }

            Vector3 segment = to - from;
            float a = Vector3.Dot(segment, segment);
            if (a <= 1e-12f)
            {
                return false;   // 길이가 0인데 위에서 안 걸렸다 = 구 밖의 한 점
            }

            //  |from + segment*t − center|² = radius² 를 t에 대해 푼다.
            float b = 2f * Vector3.Dot(toCenter, segment);
            float c = Vector3.Dot(toCenter, toCenter) - radius * radius;
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                return false;   // 아예 안 스친다
            }

            //  두 해 중 작은 쪽이 "들어가는 지점"이다. 위에서 밖이라고 확인했으므로 이 값이 답이다.
            float entry = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
            if (entry < 0f || entry > 1f)
            {
                return false;   // 닿는 지점이 이번 틱의 선분 밖이다
            }

            t = entry;
            return true;
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인 후 커밋**

```bash
git -C .../LeagueOfPhysical-Shared add Runtime/Scripts/Game/ArcheryHitTest.cs Tests/EditMode/ArcheryHitTestTests.cs
git -C .../LeagueOfPhysical-Shared commit -m "feat(archery): 화살이 지나온 선분으로 과녁 적중을 재는 커널을 만든다"
```

---

## Task 4: Shared — 점수 컴포넌트·적중 사건·와이어

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryScore.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryTargetHitEvent.cs`
- Create: `LeagueOfPhysical-Shared/Protos/ArcheryHitToC.proto`
- Create: `LeagueOfPhysical-Shared/Protos/ArcheryStateToC.proto`
- Modify: `LeagueOfPhysical-Shared/Protos/WorldEventToC.proto`
- Modify: `LeagueOfPhysical-Shared/Protos/EntitySnap.proto`
- Modify: `LeagueOfPhysical-Shared/Runtime.Generated/Scripts/WorldEventWire.cs`

**Interfaces:**
- Consumes: `GameFramework.World.Component`, `GameFramework.World.WorldEvent`.
- Produces:
  - `class ArcheryScore : GameFramework.World.Component { public int Value; }`
  - `sealed record ArcheryTargetHitEvent(string shooterId, long fireTick, int points) : WorldEvent`
  - 와이어: `WorldEventToC.archery_hit`(oneof 필드 4), `EntitySnap.score`(필드 24),
    **`ArcheryStateToC`(새 top-level 메시지, `MessageIds` 19)**
  - `WorldEventWire.ToWire`/`FromWire`가 위 사건을 다룬다

- [ ] **Step 1: 점수 컴포넌트를 만든다**

```csharp
namespace LOP
{
    /// <summary>
    /// 이 판에서 모은 점수(데이터만). <b>적립은 서버만</b> 한다 — 클라는 스냅샷으로 받은 값을
    /// 덮어쓸 뿐이다(적중을 예측하지 않기로 했으므로, 클라가 스스로 올릴 일이 없다).
    /// </summary>
    public class ArcheryScore : GameFramework.World.Component
    {
        public int Value;
    }
}
```

- [ ] **Step 2: 적중 사건을 만든다**

```csharp
namespace LOP
{
    /// <summary>
    /// 과녁 하나가 먹혔다는 사실. <b>서버가 확정해 내려보내는 이산 사건</b>이다 — 과녁이 *뜨는* 것은
    /// 양쪽이 계산으로 알지만, 두세 개뿐인 과녁을 <b>누가 먼저 맞혔는지</b>는 계산으로 알 수 없다.
    ///
    /// <para>점수 자체는 이 사건으로 보내지 않는다 — 점수는 유실되면 안 되는 값이라 스냅샷
    /// (<c>EntitySnap.score</c>)이 진실원본이다. 여기 실린 <paramref name="points"/>는 "+2" 같은
    /// 연출용이다.</para>
    /// </summary>
    public sealed record ArcheryTargetHitEvent(
        string shooterId,
        long fireTick,
        int points
    ) : GameFramework.World.WorldEvent;
}
```

- [ ] **Step 3: 프로토를 더한다**

`Protos/ArcheryHitToC.proto` (신규):

```proto
syntax = "proto3";

// 폴리모픽 래퍼 안에 담기는 payload — top-level 패킷 아님(@auto_generate 없음).
message ArcheryHitToC
{
	string shooter_id = 1;
	int64  fire_tick  = 2;   // shooter_id와 짝이 되어 "어느 화살"인지를 가리킨다
	int32  points     = 3;   // 연출용. 점수의 진실원본은 EntitySnap.score다
	//  "어느 과녁"은 싣지 않는다 — 그건 ArcheryStateToC(상태)가 말한다. 나중에 "+N"을 과녁
	//  자리에 띄우고 싶어지면 그때 필드를 더한다(proto 필드 추가는 뒤에 붙이면 되므로 싸다).
}
```

`Protos/WorldEventToC.proto` (수정) — import 한 줄과 oneof 한 줄:

```proto
import "ArcheryHitToC.proto";
...
		ArcheryHitToC       archery_hit       = 4;
```

`Protos/ArcheryStateToC.proto` (신규) — **이것만 top-level 패킷이다**:

```proto
syntax = "proto3";

// 지금 웨이브에서 어느 과녁이 먹혔나(서버 → 클라). 바뀔 때만 reliable로 간다.
// 과녁이 *뜨는* 것은 양쪽이 씨앗으로 계산하므로 보내지 않는다 — 보내는 것은 "사라진 것"뿐이다.
// 사건으로 보내면 끊겼다 돌아온 사람이 이미 먹힌 과녁을 살아 있는 것으로 본다(PanchigiStateToC와 같은 사정).
// @auto_generate
message ArcheryStateToC
{
	// 이 마스크가 말하는 웨이브. 받는 쪽이 자기 웨이브와 다르면 버린다(늦게 도착한 낡은 소식).
	int32 wave_index = 1;
	// 먹힌 슬롯의 비트마스크 — 슬롯 0이 1비트. 한 웨이브에 과녁이 최대 3개라 세 비트면 족하다.
	int32 consumed_mask = 2;
}
```

`Protos/EntitySnap.proto` (수정) — 마지막에 한 줄:

```proto
	// Archery: 이 판에서 모은 점수. 이 컴포넌트가 없는 게임에서는 0이 나가고 아무도 안 읽는다
	// (finish_placement와 같은 방식).
	int32 score = 24;
}
```

- [ ] **Step 4: 프로토를 컴파일한다 — 기존 id가 밀리지 않아야 한다**

스크립트는 `LeagueOfPhysical-Shared/Scripts/`에 있다. **부모 `generate_protos.sh`를 쓰지 말 것** —
그것은 `MessageIds.cs`를 지우고 다시 만들어 번호를 통째로 새로 매긴다.

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Scripts
./compile_protos.sh          # .proto → .cs
./generate_message_ids.sh    # 기존 MessageIds.cs를 읽어 id를 보존하고 새것만 붙인다
./generate_imessage.sh       # 새 top-level 메시지의 IMessage 구현
cd .. && git diff Runtime.Generated/Scripts/MessageIds.cs
```

기대하는 diff는 **정확히 한 줄 추가**다:

```
+        public const ushort ArcheryStateToC                = 19;
```

1~18번 중 하나라도 값이 바뀌었으면 **즉시 되돌린다**(`git checkout -- Runtime.Generated/Scripts/MessageIds.cs`)
— 그 상태로 커밋하면 클라와 서버가 서로 다른 번호를 쓰게 되어 **에러 없이 메시지만 안 통한다.**

`MessageInitializer.cs`에도 `ArcheryStateToC` 등록 한 줄이 들어갔는지 확인한다.

*(스크립트 이름은 슬라이스 1의 `2026-09-11-archery-shot-event.md`가 실제로 쓴 것을 우선한다 —
거기서 통한 명령이 있으면 그대로 쓸 것.)*

- [ ] **Step 5: `WorldEventWire`에 매핑을 더한다**

`ToWire`의 `ArcheryShotFiredEvent` case 아래:

```csharp
                case ArcheryTargetHitEvent h:
                    return new WorldEventToC
                    {
                        ArcheryHit = new ArcheryHitToC
                        {
                            ShooterId = h.shooterId,
                            FireTick  = h.fireTick,
                            Points    = h.points,
                        }
                    };
```

`FromWire`의 대응 case:

```csharp
                case WorldEventToC.EventOneofCase.ArcheryHit:
                    return new ArcheryTargetHitEvent(
                        shooterId: rec.ArcheryHit.ShooterId,
                        fireTick:  rec.ArcheryHit.FireTick,
                        points:    rec.ArcheryHit.Points);
```

- [ ] **Step 6: 왕복 테스트를 더한다**

`Tests/EditMode/`에 이미 `WorldEventWire` 테스트가 있으면 거기에, 없으면
`ArcheryHitWireTests.cs`를 새로 만든다:

```csharp
[Test]
public void 적중_사건은_와이어를_왕복해도_그대로다()
{
    var original = new ArcheryTargetHitEvent("e7", 1234L, 4);
    var restored = (ArcheryTargetHitEvent)WorldEventWire.FromWire(WorldEventWire.ToWire(original));

    Assert.AreEqual(original.shooterId, restored.shooterId);
    Assert.AreEqual(original.fireTick,  restored.fireTick);
    Assert.AreEqual(original.points,    restored.points);
}
```

- [ ] **Step 7: 커밋**

```bash
git -C .../LeagueOfPhysical-Shared add Runtime/Scripts/Game/ArcheryScore.cs Runtime/Scripts/Game/ArcheryTargetHitEvent.cs Protos/ArcheryHitToC.proto Protos/ArcheryStateToC.proto Protos/WorldEventToC.proto Protos/EntitySnap.proto Runtime.Generated/Scripts/Protobuf/ Runtime.Generated/Scripts/WorldEventWire.cs Runtime.Generated/Scripts/MessageIds.cs Runtime.Generated/Scripts/MessageInitializer.cs Tests/EditMode/
git -C .../LeagueOfPhysical-Shared status --short
git -C .../LeagueOfPhysical-Shared diff --cached Runtime.Generated/Scripts/MessageIds.cs   # 추가 한 줄뿐인가
git -C .../LeagueOfPhysical-Shared commit -m "feat(archery): 점수·적중 사건·웨이브 상태를 와이어에 올린다"
```

---

## Task 5: Server — 설정 provider와 배선

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryConfigProvider.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entity/ArcheryPlayerCreator.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryLifetimeScope.cs`

**Interfaces:**
- Consumes: `LOP.MasterData.LOPMasterData`, `ArcheryConfig`, `ArcheryTargetKind`.
- Produces: `class ArcheryConfigProvider { ArcheryConfig Get(); }` — DI에 `ArcheryConfig` 싱글턴으로 등록.

- [ ] **Step 1: 서버에 브랜치를 판다**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server fetch origin
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server checkout -b feature/archery-slice2 origin/main
```

- [ ] **Step 2: provider를 만든다**

`SkydiveConfigProvider`와 같은 짝이다. **과녁 종류는 id 오름차순으로 정렬해 담는다** — 목록 순서가
가중치 뽑기의 기준이 되므로 클·서가 같은 순서여야 한다.

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// Luban <c>TbArcheryConfig</c>(전역 단일 행, id=1)과 <c>TbArcheryTarget</c>(종류 목록)을
    /// LOP-Shared <see cref="ArcheryConfig"/>로 옮기는 사이드 로컬 어댑터
    /// (Shared는 MasterData 패키지 비참조 → 여기서 변환. <see cref="SkydiveConfigProvider"/> 대칭).
    ///
    /// <para><b>클라에 같은 이름의 쌍둥이가 있고, 둘이 같은 값을 내야 한다.</b> 다르면 웨이브 계산이
    /// 갈려 서로 다른 과녁을 본다.</para>
    /// </summary>
    public class ArcheryConfigProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;

        public ArcheryConfigProvider(LOP.MasterData.LOPMasterData md)
        {
            this.md = md;
        }

        public ArcheryConfig Get()
        {
            var r = md.Tables.TbArcheryConfig.GetOrDefault(1);
            if (r == null)
            {
                throw new System.InvalidOperationException(
                    "TbArcheryConfig id=1 행을 찾을 수 없음 — MasterData 미로드 또는 ArcheryConfig 데이터 누락");
            }

            var kinds = new List<ArcheryTargetKind>();
            //  뽑기가 이 목록 순서에 기대므로 id로 정렬해 클·서가 같은 순서를 보게 못박는다
            //  (DataList는 테이블에 적힌 순서라 지금도 맞지만, 누가 엑셀 줄을 옮기면 조용히 갈린다).
            //  타입 이름을 쓰지 않고 정렬한다 — 생성된 LOP.MasterData.ArcheryTargetKind가 Shared의
            //  같은 이름과 겹쳐서, 이름을 적는 순간 늘 풀네임으로 구분해야 한다.
            foreach (var row in System.Linq.Enumerable.OrderBy(md.Tables.TbArcheryTarget.DataList, x => x.Id))
            {
                kinds.Add(new ArcheryTargetKind(row.Radius, row.Points, row.Weight));
            }
            if (kinds.Count == 0)
            {
                throw new System.InvalidOperationException(
                    "TbArcheryTarget이 비어 있음 — 과녁 종류가 없으면 웨이브가 영원히 빈다");
            }

            return new ArcheryConfig(
                r.WavePeriodTicks, r.MinTargets, r.MaxTargets,
                r.SpawnRadius, r.SpawnMinY, r.SpawnMaxY, r.MinSeparation,
                kinds);
        }
    }
}
```

> 생성된 Luban 타입의 **정확한 속성명**(`WavePeriodTicks`, `SpawnMinY` 등)은 Task 1의 산출물
> (`Runtime.Generated/Scripts/MasterData/`)을 열어 확인하고 맞춘다. 추측하지 말 것.
> `SkydiveConfigProvider`가 같은 모양의 선례다 — 생성 타입 이름을 **한 번도 안 적고** `var`로만
> 받는다(그래서 이름이 겹쳐도 안전하다).

- [ ] **Step 3: 크리에이터에 `ArcheryScore`를 붙인다**

`ArcheryPlayerCreator`가 몸을 조립하는 자리에 한 줄:

```csharp
            worldEntity.Add(new ArcheryScore());
```

**확인:** 형제 크리에이터를 세어 빠뜨린 부품이 없는지 본다 —
`grep -c 'ArcheryScore' Assets/Scripts/Entity/*Creator.cs`에서 Archery만 1이고 나머지 0이면 정상이다.

- [ ] **Step 4: 스코프에 배선한다**

`ArcheryLifetimeScope.ConfigureGame` 맨 위:

```csharp
            builder.Register<ArcheryConfigProvider>(Lifetime.Singleton);
            builder.Register<ArcheryConfig>(c => c.Resolve<ArcheryConfigProvider>().Get(), Lifetime.Singleton);
```

**같은 자리에서 월드 등록도 고친다.** 지금 서버는 `IWorld`로만 등록해서 `ArcheryWorld` 자신을 꺼낼
수 없는데, Task 6의 적중 판정이 `world.Shots`를 읽어야 한다. 클라 스코프와 같은 모양으로 바꾼다:

```csharp
            builder.Register<ArcheryWorld>(c => new ArcheryWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<ArcheryAimSystem>(),
                TickInterval), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();
```

`IMatchSeed`가 이 스코프에서 해소되는지도 확인한다 — `PanchigiRuleSystem`/`LOPCombatSystem`이 어디서
받는지 보고, 상위 스코프에 `MatchSeed`가 `IMatchSeed`로 등록돼 있지 않으면 `.As<IMatchSeed>()`를 더한다.

- [ ] **Step 5: 컴파일이 되는지 본다**

```bash
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
```
(그 다음 `recompile_status`가 `up_to_date`만 뱉으면 **재컴파일을 안 한 것**이다 — 콘솔의 CS 에러를
시각과 대조해 확인할 것.)

- [ ] **Step 6: 커밋**

```bash
git -C .../LeagueOfPhysical-Server add Assets/Scripts/Game/ArcheryConfigProvider.cs Assets/Scripts/Game/ArcheryConfigProvider.cs.meta Assets/Scripts/Entity/ArcheryPlayerCreator.cs Assets/Scripts/Game/ArcheryLifetimeScope.cs
git -C .../LeagueOfPhysical-Server commit -m "feat(archery): 서버가 웨이브 설정을 마스터데이터에서 읽는다"
```

---

## Task 6: Server — 적중 판정과 웨이브 상태 송출

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryWaveState.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/ArcheryStateBroadcastSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryLifetimeScope.cs`
- Test: `LeagueOfPhysical-Server/Assets/Tests/Editor/ArcheryHitSystemTests.cs`
- Test: `LeagueOfPhysical-Server/Assets/Tests/Editor/ArcheryWaveStateTests.cs`

**Interfaces:**
- Consumes: `ArcheryWorld.Shots`, `ArcheryTrajectory`, `ArcheryHitTest`, `ArcheryWaveGenerator`,
  `ArcheryConfig`, `IMatchSeed`, `GameFramework.World.EntityRegistry`,
  `GameFramework.World.WorldEventBuffer`, `ISessionManager`.
- Produces:
  - `class ArcheryWaveState` — `int WaveIndex`, `int ConsumedMask`,
    `void BeginWave(int waveIndex)`, `bool IsConsumed(int slot)`, `bool TryConsume(int slot)`
  - `class ArcheryHitSystem : GameFramework.Runner.ITickSystem` — 생성자
    `(ArcheryWorld world, EntityRegistry entityRegistry, WorldEventBuffer eventBuffer, ArcheryConfig config, IMatchSeed matchSeed, ArcheryWaveState waveState, float tickInterval)`
  - `class ArcheryStateBroadcastSystem : GameFramework.Runner.ITickSystem` — 생성자
    `(ArcheryWaveState waveState, ISessionManager sessionManager)`

> 테스트용 조회 메서드를 시스템에 따로 만들지 않는다 — 점수는 엔티티의 `ArcheryScore`에, 먹힌 슬롯은
> `ArcheryWaveState`에 있으므로 테스트가 거기서 직접 읽는다.

- [ ] **Step 0: 웨이브 상태 보관소를 만든다**

```csharp
namespace LOP
{
    /// <summary>
    /// 지금 웨이브에서 어느 과녁이 먹혔나(서버 권위). <b>판정하는 쪽과 내보내는 쪽이 함께 보는 값</b>이라
    /// 둘 중 어느 시스템에도 넣지 않고 따로 둔다 — 그래야 판정을 세션 없이 테스트할 수 있다.
    ///
    /// <para>과녁이 최대 세 개라 "먹힌 슬롯"은 비트마스크 하나면 된다.</para>
    /// </summary>
    public class ArcheryWaveState
    {
        /// <summary>이 마스크가 말하는 웨이브. 아직 시작 전이면 −1이다.</summary>
        public int WaveIndex { get; private set; } = -1;

        /// <summary>먹힌 슬롯의 비트마스크. 슬롯 0이 1비트.</summary>
        public int ConsumedMask { get; private set; }

        /// <summary>웨이브가 넘어가면 과녁도 통째로 새것이다 — 기록을 비운다.</summary>
        public void BeginWave(int waveIndex)
        {
            WaveIndex = waveIndex;
            ConsumedMask = 0;
        }

        public bool IsConsumed(int slot) => (ConsumedMask & (1 << slot)) != 0;

        /// <summary>먹었다고 기록한다. 이미 먹힌 슬롯이면 거짓을 돌려준다.</summary>
        public bool TryConsume(int slot)
        {
            if (IsConsumed(slot))
            {
                return false;
            }
            ConsumedMask |= 1 << slot;
            return true;
        }
    }
}
```

`ArcheryWaveStateTests.cs`는 네 가지만 본다: 처음엔 아무것도 안 먹혔다 / 먹으면 그 슬롯만 선다 /
같은 슬롯을 두 번 먹을 수 없다 / 웨이브가 넘어가면 마스크가 0으로 돌아간다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using System.Collections.Generic;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryHitSystemTests
    {
        const float TickInterval = 0.02f;
        const long StartTick = 1000;
        const ulong Seed = 0xABCDEFUL;

        sealed class FixedSeed : IMatchSeed
        {
            public ulong Value { get; set; }
        }

        static ArcheryConfig Config()
            => new ArcheryConfig(
                wavePeriodTicks: 88, minTargets: 2, maxTargets: 3,
                spawnRadius: 2f, spawnMinY: 2f, spawnMaxY: 6f, minSeparation: 1.4f,
                kinds: new[]
                {
                    new ArcheryTargetKind(0.60f, 1, 50),
                    new ArcheryTargetKind(0.40f, 2, 35),
                    new ArcheryTargetKind(0.25f, 4, 15),
                });

        sealed class Fixture
        {
            public ArcheryHitSystem System;
            public EntityRegistry Registry;
            public ArcheryWorld World;
            public ArcheryConfig Config;
            public ArcheryWaveState WaveState;

            public Entity Archer(string id)
            {
                var entity = new Entity(id);
                entity.Add(new GameFramework.World.Transform());
                entity.Add(new Velocity());
                entity.Add(new ArcheryScore());
                Registry.Add(entity);
                return entity;
            }

            public int ScoreOf(string id) => Registry.Get(id).Get<ArcheryScore>().Value;

            public List<ArcheryTarget> TargetsOfWave(int wave)
            {
                var targets = new List<ArcheryTarget>();
                ArcheryWaveGenerator.Fill(targets, Seed, wave, Config);
                return targets;
            }

            public int HitEventCount()
            {
                int count = 0;
                foreach (var e in World.EventBuffer.Snapshot)
                {
                    if (e is ArcheryTargetHitEvent) { count++; }
                }
                return count;
            }
        }

        static Fixture Build(long startTick)
        {
            var registry = new EntityRegistry();
            var config = Config();
            var world = new ArcheryWorld(registry, new WorldEventBuffer(), new ArcheryAimSystem(), TickInterval);
            world.GameplayStartTick = startTick;
            var waveState = new ArcheryWaveState();

            return new Fixture
            {
                Registry = registry,
                World = world,
                Config = config,
                WaveState = waveState,
                System = new ArcheryHitSystem(
                    world, registry, world.EventBuffer, config,
                    new FixedSeed { Value = Seed }, waveState, TickInterval),
            };
        }

        /// <summary>
        /// 과녁 한가운데를 정확히 지나가는 화살 한 발. 한 틱(0.02초)에 정확히 <paramref name="distance"/>
        /// 만큼 나아가게 속도를 잡아, 그 틱의 선분이 −z 쪽 <paramref name="distance"/>에서 시작해
        /// 과녁 중심에서 끝나게 한다. 멀리서 출발할수록 선분 위에서 늦게 닿는다.
        /// </summary>
        static ArcheryShot ShotThrough(string shooterId, long fireTick, ArcheryTarget target, float distance)
        {
            Vector3 origin = target.Center + new Vector3(0f, 0f, -distance);
            return new ArcheryShot(shooterId, fireTick, origin, new Vector3(0f, 0f, distance / TickInterval));
        }

        [Test]
        public void 과녁을_지나간_화살은_점수가_된다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(target.Points, f.ScoreOf("a"));
        }

        [Test]
        public void 적중은_사건으로도_남는다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(1, f.HitEventCount());
        }

        [Test]
        public void 먹힌_과녁은_웨이브_상태에_남는다()
        {
            //  이 마스크가 곧 클라에 나가는 값이다 — 사건을 놓친(재접속한) 사람은 이것만 보고
            //  어느 과녁이 사라졌는지 안다.
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(0, f.WaveState.WaveIndex);
            Assert.IsTrue(f.WaveState.IsConsumed(target.SlotIndex));
        }

        [Test]
        public void 웨이브가_넘어가면_먹힌_기록이_비워진다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);
            //  다음 웨이브로 넘긴다(주기 88틱).
            f.System.Tick(StartTick + 88, TickInterval);

            Assert.AreEqual(1, f.WaveState.WaveIndex);
            Assert.AreEqual(0, f.WaveState.ConsumedMask);
        }

        [Test]
        public void 먹힌_과녁은_두_번_먹히지_않는다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            f.Archer("b");
            var target = f.TargetsOfWave(0)[0];

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            //  b가 한 틱 뒤에 같은 자리를 지나가도 이미 사라진 과녁이다.
            f.World.IngestRemoteShot(ShotThrough("b", StartTick + 1, target, 1.0f));
            f.System.Tick(StartTick + 2, TickInterval);

            Assert.AreEqual(target.Points, f.ScoreOf("a"));
            Assert.AreEqual(0, f.ScoreOf("b"));
        }

        [Test]
        public void 같은_틱에_두_발이_닿으면_먼저_닿은_쪽이_먹는다()
        {
            var f = Build(StartTick);
            f.Archer("near");
            f.Archer("far");
            var target = f.TargetsOfWave(0)[0];

            //  둘 다 이번 틱에 과녁 중심에서 끝나지만, 가까이서 출발한 쪽이 선분 위에서 먼저 닿는다
            //  (1 − r/d 가 d가 커질수록 크다).
            f.World.IngestRemoteShot(ShotThrough("far", StartTick, target, 2.0f));
            f.World.IngestRemoteShot(ShotThrough("near", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(target.Points, f.ScoreOf("near"));
            Assert.AreEqual(0, f.ScoreOf("far"));
        }

        [Test]
        public void 맞은_화살은_다른_과녁을_또_맞히지_않는다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var targets = f.TargetsOfWave(0);

            //  첫 과녁을 먹은 화살이, 다음 틱에 둘째 과녁 자리에 있어도 다시 먹지 않는다.
            //  (실제로 두 과녁을 잇는 궤적을 만들기 어려우므로, 같은 화살을 두 틱 굴려
            //   점수가 한 번만 오르는 것으로 확인한다.)
            f.World.IngestRemoteShot(ShotThrough("a", StartTick, targets[0], 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);
            f.System.Tick(StartTick + 2, TickInterval);

            Assert.AreEqual(targets[0].Points, f.ScoreOf("a"));
        }

        [Test]
        public void 빗나간_화살은_아무_일도_안_만든다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            //  과녁보다 100m 위를 지나간다.
            var origin = target.Center + new Vector3(0f, 100f, -1f);
            f.World.IngestRemoteShot(new ArcheryShot("a", StartTick, origin, new Vector3(0f, 0f, 50f)));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(0, f.ScoreOf("a"));
            Assert.AreEqual(0, f.HitEventCount());
        }

        [Test]
        public void 출발_전에는_판정하지_않는다()
        {
            var f = Build(long.MaxValue);       // 아직 출발 틱을 모른다
            f.Archer("a");

            //  웨이브가 없으므로 과녁 자리도 없다 — 원점을 지나는 화살을 넣어 본다.
            f.World.IngestRemoteShot(new ArcheryShot("a", 0, new Vector3(0f, 3f, -1f), new Vector3(0f, 0f, 50f)));
            f.System.Tick(10, TickInterval);

            Assert.AreEqual(0, f.ScoreOf("a"));
            Assert.AreEqual(0, f.HitEventCount());
        }

        [Test]
        public void 몸이_사라진_사람의_화살도_판을_죽이지_않는다()
        {
            var f = Build(StartTick);           // 쏜 사람을 등록하지 않는다(나간 사람)
            var target = f.TargetsOfWave(0)[0];

            f.World.IngestRemoteShot(ShotThrough("gone", StartTick, target, 1.0f));

            Assert.DoesNotThrow(() => f.System.Tick(StartTick + 1, TickInterval));
            Assert.AreEqual(1, f.HitEventCount());   // 과녁은 먹힌다 — 점수만 갈 데가 없을 뿐
        }
    }
}
```

> 조립 방식은 `LeagueOfPhysical-Server/Assets/Tests/Editor/SkydiveLaserSystemTests.cs`와 같은 짝이다
> (월드·레지스트리를 손으로 `new` 하고 엔티티를 직접 조립). 화살은 `ArcheryWorld`의 발사 경로를
> 타지 않고 **`IngestRemoteShot`으로 직접 넣는다** — 그 메서드가 목록에 그대로 더한다.
>
> ⚠️ `ShotThrough`가 만드는 선분이 과녁을 실제로 지나는지는 **생성된 과녁의 반지름에 달려 있다** —
> `distance`(1.0m)가 가장 큰 과녁의 반지름(0.6m)보다 커야 "밖에서 출발해 안으로 들어가는" 모양이
> 된다. 데이터의 반지름을 키우면 이 테스트의 `distance`도 같이 키워야 한다.

- [ ] **Step 2: 테스트가 컴파일 실패로 떨어지는 것을 확인한다**

- [ ] **Step 3: 시스템을 만든다**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 화살이 과녁에 닿았는지 매 틱 판정하고, 닿았으면 점수를 주고 그 과녁을 치운다.
    ///
    /// <para><b>서버에서만 돈다.</b> 클라는 적중을 예측하지 않는다 — 과녁이 두세 개뿐이라 남이 먼저
    /// 맞혔을 확률이 높고, 미리 띄운 점수가 눈앞에서 취소되는 것은 아예 안 보여 주는 것만 못하다
    /// (2026-07-12에 같은 이유로 클라 데미지 예측을 짓지 않기로 했다).</para>
    ///
    /// <para>과녁이 <b>뜨는</b> 것은 양쪽이 씨앗으로 계산하므로 통신하지 않는다. 여기서 통신하는 것은
    /// "누가 먼저 먹었나" 하나뿐이다.</para>
    /// </summary>
    public class ArcheryHitSystem : GameFramework.Runner.ITickSystem
    {
        private readonly ArcheryWorld world;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.WorldEventBuffer eventBuffer;
        private readonly ArcheryConfig config;
        private readonly IMatchSeed matchSeed;
        private readonly float tickInterval;

        private readonly ArcheryWaveState waveState;

        // 이미 무언가를 맞힌 화살. 서버는 되감지 않으므로 그냥 필드다
        // (월드의 저장/복원에 넣으면 서버가 확정한 사실이 되감기에 되살아난다).
        private readonly HashSet<(string shooterId, long fireTick)> spentArrows
            = new HashSet<(string, long)>();

        private readonly List<ArcheryTarget> targets = new List<ArcheryTarget>();
        private readonly List<Candidate> candidates = new List<Candidate>();

        private readonly struct Candidate
        {
            public readonly float T;
            public readonly string ShooterId;
            public readonly long FireTick;
            public readonly int Slot;

            public Candidate(float t, string shooterId, long fireTick, int slot)
            {
                T = t; ShooterId = shooterId; FireTick = fireTick; Slot = slot;
            }
        }

        public ArcheryHitSystem(ArcheryWorld world,
                                GameFramework.World.EntityRegistry entityRegistry,
                                GameFramework.World.WorldEventBuffer eventBuffer,
                                ArcheryConfig config,
                                IMatchSeed matchSeed,
                                ArcheryWaveState waveState,
                                float tickInterval)
        {
            this.world = world;
            this.entityRegistry = entityRegistry;
            this.eventBuffer = eventBuffer;
            this.config = config;
            this.matchSeed = matchSeed;
            this.waveState = waveState;
            this.tickInterval = tickInterval;
        }

        public void Tick(long tick, float deltaTime)
        {
            int wave = ArcheryWaveGenerator.WaveIndexAt(tick, world.GameplayStartTick, config);
            if (wave < 0)
            {
                return;   // 아직 출발 전
            }

            if (wave != waveState.WaveIndex)
            {
                ArcheryWaveGenerator.Fill(targets, matchSeed.Value, wave, config);
                // 지난 웨이브의 과녁은 이미 사라졌다 — 기록을 들고 있을 이유가 없다.
                waveState.BeginWave(wave);
            }

            CollectCandidates(tick);
            ApplyCandidates(wave);
            ForgetOldArrows(tick);
        }

        // 먼저 다 모은다 — 찾는 대로 바로 적용하면 엔티티 순회 순서가 승자를 정하게 된다.
        private void CollectCandidates(long tick)
        {
            candidates.Clear();

            var shots = world.Shots;
            for (int s = 0; s < shots.Count; s++)
            {
                var shot = shots[s];
                if (spentArrows.Contains((shot.ShooterId, shot.FireTick)))
                {
                    continue;
                }

                //  이번 틱에 지나온 선분. 쏜 바로 그 틱이면 출발점에서 한 틱만큼 나아간 구간이다.
                float toSeconds = (tick - shot.FireTick) * tickInterval;
                float fromSeconds = Mathf.Max(toSeconds - tickInterval, 0f);
                if (toSeconds < 0f)
                {
                    continue;   // 아직 떠나지 않은 화살(되감기 중에만 생긴다)
                }

                Vector3 from = ArcheryTrajectory.PositionAt(shot, fromSeconds);
                Vector3 to = ArcheryTrajectory.PositionAt(shot, toSeconds);

                for (int i = 0; i < targets.Count; i++)
                {
                    if (waveState.IsConsumed(targets[i].SlotIndex))
                    {
                        continue;
                    }
                    if (ArcheryHitTest.SegmentHitsSphere(from, to, targets[i].Center, targets[i].Radius, out float t))
                    {
                        candidates.Add(new Candidate(t, shot.ShooterId, shot.FireTick, targets[i].SlotIndex));
                    }
                }
            }
        }

        private void ApplyCandidates(int wave)
        {
            if (candidates.Count == 0)
            {
                return;
            }

            //  먼저 닿은 화살이 먹는다. 완전히 같은 시각이면 쏜 사람 id로 갈라 결과를 못박는다
            //  (안 그러면 목록 순서, 즉 엔티티 순회 순서가 승자를 정한다).
            candidates.Sort((a, b) =>
            {
                int byTime = a.T.CompareTo(b.T);
                return byTime != 0 ? byTime : string.CompareOrdinal(a.ShooterId, b.ShooterId);
            });

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (spentArrows.Contains((candidate.ShooterId, candidate.FireTick)))
                {
                    continue;   // 이 화살은 이 틱에 이미 다른 과녁을 먹었다
                }
                //  TryConsume이 거짓이면 이 틱에 더 먼저 닿은 화살이 이미 먹은 것이다.
                if (waveState.TryConsume(candidate.Slot) == false)
                {
                    continue;
                }

                int points = PointsOfSlot(candidate.Slot);
                spentArrows.Add((candidate.ShooterId, candidate.FireTick));

                var score = entityRegistry.Get(candidate.ShooterId)?.Get<ArcheryScore>();
                if (score != null)
                {
                    score.Value += points;
                }

                eventBuffer.Append(new ArcheryTargetHitEvent(
                    candidate.ShooterId, candidate.FireTick, points));
            }
        }

        private int PointsOfSlot(int slot)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].SlotIndex == slot)
                {
                    return targets[i].Points;
                }
            }
            return 0;
        }

        // 수명이 다한 화살은 목록에서도 사라지므로 기록을 들고 있을 이유가 없다.
        private void ForgetOldArrows(long tick)
        {
            long lifetimeTicks = (long)(ArcheryTrajectory.LifetimeSeconds / tickInterval) + 1;
            spentArrows.RemoveWhere(a => tick - a.fireTick > lifetimeTicks);
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

- [ ] **Step 4-b: 상태 송출 시스템을 만든다**

`PanchigiTurnSystem.BroadcastIfChanged`를 그대로 본뜬다 — **바뀔 때만** 보내되, "누가 받았는지"를
세션별로 적어 두고 **아직 못 받은 세션에는 다음 틱에 또 보낸다.** 그래야 늦게 들어온 세션과
끊겼다 돌아온 세션이 반드시 현재 상태를 받는다.

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 지금 웨이브에서 어느 과녁이 먹혔는지를 모두에게 알린다.
    ///
    /// <para><b>왜 사건이 아니라 상태인가.</b> 적중 사건은 reliable로 가서 연결된 사람은 안 놓치지만,
    /// 미러는 <b>새 연결에 지난 메시지를 다시 틀어 주지 않는다</b> — 끊겼다 돌아온 사람은 이미 먹힌
    /// 과녁을 살아 있는 것으로 보고, 스스로는 그게 틀렸다는 것조차 알 수 없다. 그래서 "지금 남은
    /// 과녁"은 상태로 보낸다(<see cref="PanchigiStateToC"/>와 같은 사정·같은 방식).</para>
    ///
    /// <para>마스크가 0인 웨이브는 보내지 않는다 — 아무것도 안 먹힌 것이 클라의 기본값이라 보낼 것이
    /// 없다. 웨이브가 넘어가 마스크가 0으로 돌아간 것도 보낼 필요가 없다: 메시지에 웨이브 번호가
    /// 실려 있어 받는 쪽이 자기 웨이브와 다른 소식을 버린다.</para>
    /// </summary>
    public class ArcheryStateBroadcastSystem : GameFramework.Runner.ITickSystem
    {
        private readonly ArcheryWaveState waveState;
        private readonly ISessionManager sessionManager;

        private readonly HashSet<string> receivedSessionIds = new HashSet<string>();
        private int sentWaveIndex = -1;
        private int sentMask;

        public ArcheryStateBroadcastSystem(ArcheryWaveState waveState, ISessionManager sessionManager)
        {
            this.waveState = waveState;
            this.sessionManager = sessionManager;
        }

        public void Tick(long tick, float deltaTime)
        {
            if (waveState.ConsumedMask == 0)
            {
                return;   // 아무것도 안 먹혔다 = 클라의 기본값과 같다
            }

            if (waveState.WaveIndex != sentWaveIndex || waveState.ConsumedMask != sentMask)
            {
                sentWaveIndex = waveState.WaveIndex;
                sentMask = waveState.ConsumedMask;
                receivedSessionIds.Clear();   // 새 소식이다 — 모두 다시 받아야 한다
            }

            var message = new ArcheryStateToC
            {
                WaveIndex = waveState.WaveIndex,
                ConsumedMask = waveState.ConsumedMask,
            };

            foreach (var session in sessionManager.GetAllSessions())
            {
                if (session.isConnected == false)
                {
                    //  끊긴 세션은 "받은 적 없음"으로 되돌린다 — 재접속은 같은 sessionId를 그대로
                    //  다시 쓰므로(LOPRoom.OnPlayerEnter가 세션 객체를 재사용한다), 지워 두지 않으면
                    //  돌아온 사람이 이 웨이브의 상태를 통째로 놓친다.
                    receivedSessionIds.Remove(session.sessionId);
                    continue;
                }

                if (receivedSessionIds.Contains(session.sessionId))
                {
                    continue;
                }

                session.Send(message);
                receivedSessionIds.Add(session.sessionId);
            }
        }
    }
}
```

> `ISessionManager`/`session.sessionId`/`session.isConnected`의 정확한 이름은
> `PanchigiTurnSystem`에서 확인해 맞춘다.

- [ ] **Step 5: 스코프에 물린다 — `End` 페이즈**

`ArcheryLifetimeScope.ConfigureGame` 끝:

```csharp
            builder.Register<ArcheryWaveState>(Lifetime.Singleton);
            builder.Register(c => new ArcheryHitSystem(
                c.Resolve<ArcheryWorld>(),
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<ArcheryConfig>(),
                c.Resolve<IMatchSeed>(),
                c.Resolve<ArcheryWaveState>(),
                TickInterval), Lifetime.Singleton);
            builder.Register<ArcheryStateBroadcastSystem>(Lifetime.Singleton);

            // 화살이 생긴 *뒤*에 판정해야 하므로 world.Tick 다음인 End에 문다. 그러면 여기서 쌓은
            // 사건은 이번 틱 드레인을 놓쳐 다음 틱(20ms 뒤)에 나간다 — 점수 자체는 스냅샷으로
            // 가므로 이 지연은 연출에만 걸린다.
            builder.RegisterBuildCallback(container =>
            {
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<ArcheryHitSystem>());
                //  판정 다음에 내보낸다 — 같은 틱의 결과가 그 틱에 나간다.
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<ArcheryStateBroadcastSystem>());
            });
```

> ⚠️ 서버 스코프는 지금 `IWorld`만 등록하고 `ArcheryWorld` 자신은 등록하지 않는다. 클라 스코프처럼
> `.As<GameFramework.World.IWorld>().AsSelf()`로 바꿔 `ArcheryWorld`도 꺼낼 수 있게 한다.
> `IMatchSeed`가 서버 스코프에서 이미 해소되는지도 확인할 것 — 안 되면 상위 스코프에
> `MatchSeed`가 `IMatchSeed`로 등록돼 있는지 보고 필요하면 `.As<IMatchSeed>()`를 더한다.

- [ ] **Step 6: 컴파일·테스트 후 커밋**

```bash
git -C .../LeagueOfPhysical-Server add Assets/Scripts/Game/ArcheryWaveState.cs* Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs* Assets/Scripts/Game/TickSystems/ArcheryStateBroadcastSystem.cs* Assets/Scripts/Game/ArcheryLifetimeScope.cs Assets/Tests/Editor/ArcheryHitSystemTests.cs* Assets/Tests/Editor/ArcheryWaveStateTests.cs*
git -C .../LeagueOfPhysical-Server commit -m "feat(archery): 적중을 확정하고 남은 과녁을 상태로 내보낸다"
```

---

## Task 7: Server — 점수를 내려보내고, 점수로 순위를 낸다

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Domain/ScorePlacements.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryRuleSystem.cs`
- Test: `LeagueOfPhysical-Server/Assets/Tests/Editor/ScorePlacementsTests.cs`

**Interfaces:**
- Produces: `static class ScorePlacements { static MatchOutcome Resolve(IReadOnlyList<(string userId, int score)> scores); }`
  — 점수 내림차순, **동점은 공동 순위**(1·1·3).

- [ ] **Step 1: 점수를 스냅에 싣는다**

`EntitySnapshotBroadcastSystem`의 `snap.FinishPlacement = ...` 줄 바로 아래:

```csharp
                //  이 컴포넌트가 없는 게임에는 0이 나가고 아무도 안 읽는다(finish_placement와 같은 방식).
                snap.Score = worldEntity.Get<ArcheryScore>()?.Value ?? 0;
```

- [ ] **Step 2: 실패하는 등수 테스트를 쓴다**

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    public class ScorePlacementsTests
    {
        [Test]
        public void 점수가_높은_사람이_앞이다()
        {
            var outcome = ScorePlacements.Resolve(new[] { ("a", 3), ("b", 9), ("c", 5) });
            Assert.AreEqual(1, Placement(outcome, "b"));
            Assert.AreEqual(2, Placement(outcome, "c"));
            Assert.AreEqual(3, Placement(outcome, "a"));
        }

        [Test]
        public void 동점은_같은_등수이고_다음_등수를_건너뛴다()
        {
            var outcome = ScorePlacements.Resolve(new[] { ("a", 5), ("b", 5), ("c", 1) });
            Assert.AreEqual(1, Placement(outcome, "a"));
            Assert.AreEqual(1, Placement(outcome, "b"));
            Assert.AreEqual(3, Placement(outcome, "c"));   // 2위는 없다 — 스포츠 표준
        }

        [Test]
        public void 아무도_못_맞히면_전원_공동_1등이다()
        {
            var outcome = ScorePlacements.Resolve(new[] { ("a", 0), ("b", 0) });
            Assert.AreEqual(1, Placement(outcome, "a"));
            Assert.AreEqual(1, Placement(outcome, "b"));
        }

        [Test]
        public void 사람이_없어도_죽지_않는다()
        {
            Assert.AreEqual(0, ScorePlacements.Resolve(new (string, int)[0]).placements.Count);
        }

        [Test]
        public void 같은_점수끼리의_순서는_사람_id로_못박는다()
        {
            //  동점끼리도 목록에 담기는 차례가 매번 같아야 결과 화면이 흔들리지 않는다.
            var first = ScorePlacements.Resolve(new[] { ("b", 5), ("a", 5) });
            var second = ScorePlacements.Resolve(new[] { ("a", 5), ("b", 5) });
            Assert.AreEqual(first.placements[0].userId, second.placements[0].userId);
        }

        private static int Placement(MatchOutcome outcome, string userId)
        {
            foreach (var p in outcome.placements)
            {
                if (p.userId == userId) { return p.placement; }
            }
            return 0;
        }
    }
}
```

- [ ] **Step 3: 테스트가 컴파일 실패로 떨어지는 것을 확인한다**

- [ ] **Step 4: `ScorePlacements`를 만든다**

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 점수로 등수를 매기는 규칙. 게임을 모르는 순수 계산이다 — 점수로 겨루는 게임이 모두 같은 답을 낸다.
    /// 동점은 <b>공동 순위</b>이고 다음 등수는 그만큼 건너뛴다(1·1·3) — <see cref="FinishPlacements"/>와 같은 관례.
    /// </summary>
    public static class ScorePlacements
    {
        public static MatchOutcome Resolve(IReadOnlyList<(string userId, int score)> scores)
        {
            var sorted = new List<(string userId, int score)>(scores);
            //  점수가 같으면 사람 id로 갈라 순서를 못박는다 — 안 그러면 목록에 담긴 차례(딕셔너리
            //  순회 순서)가 결과 화면의 줄 순서를 정해 판마다 달라진다.
            sorted.Sort((a, b) =>
            {
                int byScore = b.score.CompareTo(a.score);
                return byScore != 0 ? byScore : string.CompareOrdinal(a.userId, b.userId);
            });

            var outcome = new MatchOutcome();
            int placement = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i == 0 || sorted[i].score != sorted[i - 1].score)
                {
                    placement = i + 1;
                }
                outcome.placements.Add(new MatchPlacement { userId = sorted[i].userId, placement = placement });
            }
            return outcome;
        }
    }
}
```

- [ ] **Step 5: 룰이 점수로 순위를 내게 고친다**

`ArcheryRuleSystem`:
- 생성자에 `GameFramework.World.EntityRegistry entityRegistry`를 더한다(점수를 읽어야 한다).
- `ResolveOutcome`을 갈아 끼운다:

```csharp
        /// <summary>점수가 높은 사람이 앞이다. 동점은 공동 순위.</summary>
        public MatchOutcome ResolveOutcome()
        {
            var scores = new List<(string userId, int score)>();
            foreach (var pair in entityIdToUserId)
            {
                //  판이 끝나기 전에 몸이 사라진 사람(나간 사람)은 점수를 0으로 본다.
                int score = entityRegistry.Get(pair.Key)?.Get<ArcheryScore>()?.Value ?? 0;
                scores.Add((pair.Value, score));
            }
            return ScorePlacements.Resolve(scores);
        }
```

클래스 주석의 *"점수는 슬라이스 2가 여기에 붙는다 — 지금은 순위를 가릴 근거가 없어 전원 동순위다"*
문장도 지금 상태에 맞게 고친다.

- [ ] **Step 6: 테스트·컴파일 확인 후 커밋**

```bash
git -C .../LeagueOfPhysical-Server add Assets/Scripts/Domain/ScorePlacements.cs Assets/Scripts/Domain/ScorePlacements.cs.meta Assets/Scripts/Game/ArcheryRuleSystem.cs Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs Assets/Tests/Editor/ScorePlacementsTests.cs Assets/Tests/Editor/ScorePlacementsTests.cs.meta
git -C .../LeagueOfPhysical-Server commit -m "feat(archery): 점수를 내려보내고 점수로 순위를 낸다"
```

---

## Task 8: Client — 설정·점수 수신 배선

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryConfigProvider.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryConsumed.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/EntitySnap.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/ArcheryPlayerCreator.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryLifetimeScope.cs`

**Interfaces:**
- Produces:
  - `class ArcheryConfigProvider { ArcheryConfig Get(); }` — 서버 쌍둥이와 **같은 값**을 낸다
  - `class ArcheryConsumed` — 먹힌 과녁·화살 보관소(뷰와 핸들러가 함께 본다):
    - `void ApplyState(int waveIndex, int consumedMask)` — **상태 메시지가 채운다**
    - `bool IsTargetGone(int waveIndex, int slotIndex)` — 웨이브가 다르면 늘 거짓
    - `void MarkArrow(string shooterId, long fireTick)` / `bool IsArrowGone(string shooterId, long fireTick)`
    - `void ForgetArrowsBefore(long fireTick)` — 수명 다한 화살 정리

- [ ] **Step 1: 클라에 브랜치를 판다**

**⚠️ 에디터가 main 체크아웃을 본다.** 클라 코드는 머지 전에는 워크트리에서 컴파일 검증이 안 된다 —
이 레포는 **워크트리를 쓰지 말고** 같은 체크아웃에서 브랜치만 갈아탄다.

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client fetch origin
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client checkout -b feature/archery-slice2 origin/main
```

- [ ] **Step 2: provider를 만든다**

Task 5의 서버 파일을 **그대로** 옮긴다(네임스페이스·클래스명 동일, 다른 레포). 클래스 주석에
"서버에 같은 이름의 쌍둥이가 있고 둘이 같은 값을 내야 한다"를 남긴다. 값이 갈리면 서로 다른 과녁을
보는데 **에러가 안 나므로** 이 주석이 유일한 경고다.

- [ ] **Step 3: 먹힌 것 보관소를 만든다**

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 서버가 알려 준 "사라진 것들". <b>월드가 아니라 여기</b>에 둔다 — 월드의 저장/복원에 넣으면
    /// 되감을 때 서버가 확정한 사실이 옛 값으로 되돌아가 먹힌 과녁이 되살아난다. 서버 확정 사실은
    /// 애초에 예측 대상이 아니므로 되감기 밖이 맞다.
    ///
    /// <para><b>과녁과 화살이 서로 다른 길로 온다.</b> 과녁은 <b>상태</b>(웨이브 번호 + 비트마스크)로
    /// 와서, 끊겼다 돌아온 사람도 다음 소식 한 번이면 지금 남은 과녁을 정확히 안다. 화살은
    /// <b>사건</b>으로 온다 — 3초면 사라지는 값이고, 재접속한 사람에게는 날아가던 남의 화살이
    /// 애초에 하나도 없다(남의 발사도 사건이라 그가 없던 동안의 화살은 그의 세계에 존재한 적이 없다).</para>
    /// </summary>
    public class ArcheryConsumed
    {
        private int stateWaveIndex = -1;
        private int consumedMask;

        private readonly HashSet<(string shooterId, long fireTick)> arrows
            = new HashSet<(string, long)>();

        /// <summary>서버가 알려 준 "지금 웨이브에서 먹힌 슬롯들".</summary>
        public void ApplyState(int waveIndex, int mask)
        {
            //  늦게 도착한 낡은 소식은 버린다. 새 웨이브의 과녁을 옛 마스크로 지우면
            //  멀쩡한 과녁이 화면에서 사라진다.
            if (waveIndex < stateWaveIndex)
            {
                return;
            }
            stateWaveIndex = waveIndex;
            consumedMask = mask;
        }

        /// <summary>
        /// 이 과녁이 사라졌나. <b>모르면 "살아 있다"로 답한다</b> — 소식이 아직 안 온 웨이브의 과녁을
        /// 미리 지우면 안 된다. 마스크가 0인 웨이브는 서버가 아예 안 보내므로 이것이 정상 경로다.
        /// </summary>
        public bool IsTargetGone(int waveIndex, int slotIndex)
        {
            return waveIndex == stateWaveIndex && (consumedMask & (1 << slotIndex)) != 0;
        }

        public void MarkArrow(string shooterId, long fireTick) => arrows.Add((shooterId, fireTick));
        public bool IsArrowGone(string shooterId, long fireTick) => arrows.Contains((shooterId, fireTick));

        /// <summary>수명이 다한 화살은 더 물어볼 일이 없다.</summary>
        public void ForgetArrowsBefore(long fireTick)
        {
            arrows.RemoveWhere(a => a.fireTick < fireTick);
        }
    }
}
```

- [ ] **Step 4: 점수 수신 경로를 잇는다**

`Assets/Scripts/Netcode/EntitySnap.cs`에 `finishPlacement` 옆:

```csharp
        public int score { get; set; }
```

`GameEntityMessageHandler`의 `finishPlacement`를 옮기는 자리 바로 아래:

```csharp
                var archeryScore = targetEntity?.Get<ArcheryScore>();
                if (archeryScore != null)
                {
                    archeryScore.Value = entitySnap.score;
                }
```

> ⚠️ 프로토 → `EntitySnap` POCO 변환이 AutoMapper를 탄다. `score`는 이름이 같은 int라 자동으로
> 붙지만, **실제로 값이 오는지 한 번은 눈으로 확인**할 것(중첩 타입 매핑 누락은 에러 없이 조용히
> 멈춘 전례가 있다).

- [ ] **Step 5: 크리에이터·스코프에 배선한다**

`ArcheryPlayerCreator`(클라)에도 `worldEntity.Add(new ArcheryScore());` — **남의 몸에도 붙여야**
남의 점수도 받는다.

`ArcheryLifetimeScope`(클라) `ConfigureGame` 맨 위:

```csharp
            builder.Register<ArcheryConfigProvider>(Lifetime.Singleton);
            builder.Register<ArcheryConfig>(c => c.Resolve<ArcheryConfigProvider>().Get(), Lifetime.Singleton);
            builder.Register<ArcheryConsumed>(Lifetime.Singleton);
```

- [ ] **Step 6: 컴파일 확인 후 커밋**

```bash
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile
git -C .../LeagueOfPhysical-Client add Assets/Scripts/Game/ArcheryConfigProvider.cs* Assets/Scripts/Game/ArcheryConsumed.cs* Assets/Scripts/Netcode/EntitySnap.cs Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs Assets/Scripts/Entity/ArcheryPlayerCreator.cs Assets/Scripts/Game/ArcheryLifetimeScope.cs
git -C .../LeagueOfPhysical-Client status --short
git -C .../LeagueOfPhysical-Client commit -m "feat(archery): 클라가 웨이브 설정과 점수를 받는다"
```

---

## Task 9: Client — 과녁을 그린다

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryTargetView.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryLifetimeScope.cs`

**Interfaces:**
- Consumes: `ArcheryWaveGenerator`, `ArcheryConfig`, `ArcheryConsumed`, `IMatchSeed`,
  `GameFramework.Runner.IRunner`, `GameFramework.World.IWorld`(출발 틱).
- Produces: `class ArcheryTargetView : ILateTickable, System.IDisposable`.

- [ ] **Step 1: 뷰를 만든다**

`ArcheryArrowView`와 **같은 모양**이다 — 매 프레임 계산한 것을 그리고, 사라진 것은 치우고,
`Dispose`에서 만든 것을 전부 되돌린다.

```csharp
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 지금 떠 있는 과녁을 그린다. <b>통신으로 받는 것이 아니라</b> 서버와 같은 커널에 같은 씨앗을
    /// 넣어 각자 계산한다 — 그래서 핑과 무관하게 모두가 같은 순간에 같은 과녁을 본다.
    /// 과녁은 엔티티가 아니라서 뷰가 직접 생성 커널을 부른다(<see cref="ArcheryArrowView"/>와 같은 짝).
    /// </summary>
    public class ArcheryTargetView : ILateTickable, System.IDisposable
    {
        private readonly GameFramework.Runner.IRunner runner;
        private readonly GameFramework.World.IWorld world;
        private readonly ArcheryConfig config;
        private readonly ArcheryConsumed consumed;
        private readonly IMatchSeed matchSeed;

        private readonly List<ArcheryTarget> targets = new List<ArcheryTarget>();
        private readonly Dictionary<(int wave, int slot), GameObject> drawn
            = new Dictionary<(int, int), GameObject>();
        private readonly List<(int, int)> stale = new List<(int, int)>();

        private Material _targetMaterial;

        public ArcheryTargetView(GameFramework.Runner.IRunner runner,
                                 GameFramework.World.IWorld world,
                                 ArcheryConfig config,
                                 ArcheryConsumed consumed,
                                 IMatchSeed matchSeed)
        {
            this.runner = runner;
            this.world = world;
            this.config = config;
            this.consumed = consumed;
            this.matchSeed = matchSeed;
        }

        public void LateTick()
        {
            if (runner?.tickUpdater == null)
            {
                return;   // 씬 진입 초기거나 언로드 도중
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;
            }

            //  과녁이 떠 있나 없나는 틱 단위 사실이라 소수 틱으로 물을 것이 없다 — 정수 틱으로 묻는다.
            //  (화살의 *자세*는 소수 틱이 필요하지만 과녁은 가만히 있다.)
            long renderTick = (long)System.Math.Floor((runner.tickUpdater.elapsedTime - interval) / interval);
            int wave = ArcheryWaveGenerator.WaveIndexAt(renderTick, world.GameplayStartTick, config);

            targets.Clear();
            if (wave >= 0)
            {
                ArcheryWaveGenerator.Fill(targets, matchSeed.Value, wave, config);
            }

            var alive = new HashSet<(int, int)>();
            for (int i = 0; i < targets.Count; i++)
            {
                var key = (targets[i].WaveIndex, targets[i].SlotIndex);
                if (consumed.IsTargetGone(key.Item1, key.Item2))
                {
                    continue;   // 누군가 먹었다
                }
                alive.Add(key);

                if (drawn.TryGetValue(key, out var sphere) == false || sphere == null)
                {
                    sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    var renderer = sphere.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = TargetMaterial();
                    }
                    Object.Destroy(sphere.GetComponent<Collider>());   // 그림일 뿐이다 — 판정은 서버가 한다
                    drawn[key] = sphere;
                }

                sphere.transform.position = targets[i].Center;
                //  보이는 크기가 곧 맞는 크기여야 한다 — 판정 반경이 0.25면 지름 0.5짜리 공이다.
                sphere.transform.localScale = Vector3.one * (targets[i].Radius * 2f);
            }

            stale.Clear();
            foreach (var pair in drawn)
            {
                if (alive.Contains(pair.Key) == false)
                {
                    stale.Add(pair.Key);
                }
            }
            for (int i = 0; i < stale.Count; i++)
            {
                Object.Destroy(drawn[stale[i]]);
                drawn.Remove(stale[i]);
            }
        }

        //  과녁마다 material을 새로 만들면 재질 인스턴스가 계속 쌓인다 — 한 장을 돌려 쓴다.
        private Material TargetMaterial()
        {
            if (_targetMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                //  임시 그림이라 실물보다 눈에 띄는 것이 우선이다. 붉은 화살과 갈리게 노랑.
                _targetMaterial = new Material(shader) { color = new Color(1f, 0.85f, 0.1f) };
            }
            return _targetMaterial;
        }

        public void Dispose()
        {
            foreach (var pair in drawn)
            {
                Object.Destroy(pair.Value);
            }
            drawn.Clear();

            if (_targetMaterial != null)
            {
                Object.Destroy(_targetMaterial);
                _targetMaterial = null;
            }
        }
    }
}
```

> 화살 기록 정리(`ForgetArrowsBefore`)는 **여기서 하지 않는다** — 화살을 도는 쪽이
> `ArcheryArrowView`이므로 거기서 한다(Task 10). 과녁 뷰가 화살을 치우면 둘이 서로를 모르는 채
> 엮인다.
>
> ⚠️ **매 프레임 `new HashSet`을 만들지 말 것** — 위 코드는 `ArcheryArrowView`의 모양을 그대로
> 따랐지만, 그쪽도 같은 자리에 매 프레임 할당이 있다. 이 태스크에서는 **필드로 올리고 `Clear()`**
> 해서 쓴다(과녁은 최대 3개라 크지 않지만, 매 프레임 쓰레기를 만드는 코드를 새로 복제할 이유는 없다).

- [ ] **Step 2: 스코프에 등록한다**

```csharp
            builder.RegisterEntryPoint<ArcheryTargetView>().AsSelf();
```

- [ ] **Step 3: 컴파일 확인 후 커밋**

```bash
git -C .../LeagueOfPhysical-Client add Assets/Scripts/Game/ArcheryTargetView.cs* Assets/Scripts/Game/ArcheryLifetimeScope.cs
git -C .../LeagueOfPhysical-Client commit -m "feat(archery): 과녁을 각자 계산해 그린다"
```

---

## Task 10: Client — 먹힌 것을 치우고 점수를 보여 준다

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/ArcheryHitHandler.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/ArcheryStateHandler.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryArrowView.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/UI/ArcheryPad/ArcheryPadViewModel.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/UI/ArcheryPad/ArcheryPadView.cs`
- Modify: `LeagueOfPhysical-Client/Assets/UI/ArcheryPad/ArcheryPad.uxml`
- Modify: `LeagueOfPhysical-Client/Assets/UI/ArcheryPad/ArcheryPad.uss`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryLifetimeScope.cs`

**Interfaces:**
- Consumes: `ISubscriber<WorldEventBatchToC>`, `ISubscriber<ArcheryStateToC>`, `WorldEventWire`,
  `ArcheryConsumed`, `IPlayerContext`, `GameFramework.World.EntityRegistry`.
- Produces: `class ArcheryHitHandler : MessageHandlerBase`(사건 → 박힌 화살),
  `class ArcheryStateHandler : MessageHandlerBase`(상태 → 사라진 과녁),
  `ArcheryPadViewModel.Score { get; }`(매 프레임 pull하는 int 속성).

- [ ] **Step 1: 적중 사건 핸들러를 만든다**

`ArcheryRemoteShotHandler`와 **같은 모양**이되, 이 사건은 **내 것도 버리지 않는다** — 내가 쏜
화살이 먹은 과녁도 치워야 하기 때문이다.

```csharp
using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 서버가 확정한 적중(<see cref="ArcheryTargetHitEvent"/>)을 받아 먹힌 과녁과 그 화살을 치운다.
    ///
    /// <para><see cref="ArcheryRemoteShotHandler"/>와 달리 <b>내 것도 거르지 않는다</b> — 발사는
    /// 내가 예측해 이미 알고 있지만, <b>적중은 예측하지 않으므로</b> 내 화살이 먹은 과녁도 이 사건으로
    /// 처음 안다.</para>
    /// </summary>
    public class ArcheryHitHandler : MessageHandlerBase
    {
        private readonly ArcheryConsumed consumed;
        private readonly ISubscriber<WorldEventBatchToC> batchSubscriber;

        public ArcheryHitHandler(ArcheryConsumed consumed, ISubscriber<WorldEventBatchToC> batchSubscriber)
        {
            this.consumed = consumed;
            this.batchSubscriber = batchSubscriber;
        }

        protected override void Subscribe() => Track(batchSubscriber.Subscribe(OnWorldEventBatchToC));

        private void OnWorldEventBatchToC(WorldEventBatchToC msg)
        {
            foreach (var rec in msg.Events)
            {
                if (rec.EventCase != WorldEventToC.EventOneofCase.ArcheryHit)
                {
                    continue;
                }

                var hit = (ArcheryTargetHitEvent)WorldEventWire.FromWire(rec);

                //  과녁이 사라지는 것은 여기서 처리하지 않는다 — 그건 상태(ArcheryStateToC)의 몫이다.
                //  이 사건은 "어느 화살이 박혔나"와 "누가 몇 점을 먹었나"만 말해 준다.
                consumed.MarkArrow(hit.shooterId, hit.fireTick);
            }
        }
    }
}
```

- [ ] **Step 1-b: 상태 핸들러를 만든다**

```csharp
using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 지금 웨이브에서 어느 과녁이 사라졌는지를 서버에게서 받는다.
    ///
    /// <para><b>사건이 아니라 상태로 받는 이유:</b> 적중 사건은 reliable로 가지만 미러는 새 연결에
    /// 지난 메시지를 다시 틀어 주지 않는다 — 끊겼다 돌아오면 이미 먹힌 과녁이 살아 있는 것으로
    /// 보이고, 스스로는 그게 틀렸다는 것조차 알 수 없다. 상태는 서버가 "아직 못 받은 세션"에
    /// 다시 보내 주므로 돌아온 사람도 한 번이면 맞춰진다.</para>
    /// </summary>
    public class ArcheryStateHandler : MessageHandlerBase
    {
        private readonly ArcheryConsumed consumed;
        private readonly ISubscriber<ArcheryStateToC> subscriber;

        public ArcheryStateHandler(ArcheryConsumed consumed, ISubscriber<ArcheryStateToC> subscriber)
        {
            this.consumed = consumed;
            this.subscriber = subscriber;
        }

        protected override void Subscribe() => Track(subscriber.Subscribe(OnArcheryStateToC));

        private void OnArcheryStateToC(ArcheryStateToC message)
        {
            consumed.ApplyState(message.WaveIndex, message.ConsumedMask);
        }
    }
}
```

> `ISubscriber<T>`로 새 메시지를 받으려면 **MessagePipe 등록이 필요할 수 있다** —
> `PanchigiStateToC`를 받는 쪽이 어디에 어떻게 등록돼 있는지(`GameplayInstaller` 또는
> 메시지 브로커 등록 목록) 확인해 같은 자리에 `ArcheryStateToC`를 더한다.
> IL2CPP에서는 `RegisterMessageBroker<T>` 명시 등록이 필요하다.

- [ ] **Step 2: 화살 뷰가 먹힌 화살을 안 그리게 한다**

`ArcheryArrowView` 생성자에 `ArcheryConsumed consumed`를 더하고, 그리는 루프 맨 위에:

```csharp
                if (consumed.IsArrowGone(shots[i].ShooterId, shots[i].FireTick))
                {
                    continue;   // 과녁에 박혔다 — 계속 날아가는 그림은 거짓말이다
                }
```

**주의:** `alive.Add(key)`보다 **먼저** `continue` 해야 이미 그려 둔 화살이 다음 정리 단계에서
치워진다. 순서를 바꾸면 박힌 화살이 그 자리에 멈춰 남는다.

같은 메서드에서 **수명이 다한 화살의 기록도 버린다** — 화살을 도는 쪽이 여기라서 이 정리도 여기가
자리다. `renderTick`을 계산한 직후에 한 줄:

```csharp
            //  목록에서 사라진 화살의 "박혔다" 기록을 계속 들고 있을 이유가 없다.
            consumed.ForgetArrowsBefore(
                (long)renderTick - (long)(ArcheryTrajectory.LifetimeSeconds / interval) - 1);
```

- [ ] **Step 3: 점수 라벨을 단다**

`ArcheryPad.uxml`의 `archery-pad-root` 안, 두 절반보다 **뒤에** (위에 그려지도록):

```xml
        <ui:Label name="score" class="archery-score" text="0" picking-mode="Ignore" />
```

`ArcheryPad.uss`:

```css
/* 점수는 화면 위 가운데. 조작을 가리면 안 되므로 입력은 통과시킨다. */
.archery-score {
    position: absolute;
    top: 24px;
    left: 0;
    right: 0;
    -unity-text-align: middle-center;
    font-size: 40px;
    color: rgb(255, 236, 150);
    -unity-font-style: bold;
}
```

`ArcheryPadViewModel`에 점수를 읽는 속성을 더한다. **연속 상태는 pull**이다 — 월드 컴포넌트의 값에
프로퍼티별 변경 이벤트를 걸지 않는다(아키텍처 문서의 결정).

```csharp
        /// <summary>내가 지금까지 모은 점수. 서버 스냅샷이 채우는 값이라 <b>매 프레임 읽어</b> 쓴다.</summary>
        public int Score
        {
            get
            {
                var entity = entityRegistry.Get(playerContext.entityId);
                return entity?.Get<ArcheryScore>()?.Value ?? 0;
            }
        }
```

`ArcheryPadView`는 `DebugHudView`와 같은 방식으로 패널 스케줄러를 걸어 라벨을 갱신한다
(`schedule.Execute(...).Every(...)`). `OnOpen`에서 걸고 닫을 때 푼다.

- [ ] **Step 4: 스코프에 등록한다**

```csharp
            builder.RegisterEntryPoint<ArcheryHitHandler>();
            builder.RegisterEntryPoint<ArcheryStateHandler>();
```

- [ ] **Step 5: 컴파일 확인 후 커밋**

```bash
git -C .../LeagueOfPhysical-Client add Assets/Scripts/Game/MessageHandler/ArcheryHitHandler.cs* Assets/Scripts/Game/MessageHandler/ArcheryStateHandler.cs* Assets/Scripts/Game/ArcheryArrowView.cs Assets/Scripts/UI/ArcheryPad/ Assets/UI/ArcheryPad/ Assets/Scripts/Game/ArcheryLifetimeScope.cs
git -C .../LeagueOfPhysical-Client commit -m "feat(archery): 먹힌 과녁과 화살을 치우고 점수를 띄운다"
```

---

## Task 11: 무대 콜라이더 정리 + 끝-끝 확인

**Files:**
- Modify: `LeagueOfPhysical-Art/Scenes/ArcheryCircleMap.unity` (= 클라 서브모듈 `Assets/Art/Scenes/`)
- Modify: `LeagueOfPhysical-Client/Assets/Art` (서브모듈 포인터 커밋)
- Modify: `docs/ROADMAP.md` (클라)

- [ ] **Step 1: `CenterStage` 콜라이더를 보이는 모양에 맞춘다**

지금은 `CapsuleCollider`(r=0.5, h=2)에 스케일 (4, 0.2, 4)가 먹어 **지름 4m짜리 납작한 구**다.
보이는 것은 반지름 2m·높이 0.4m 원기둥이다. 캡슐을 지우고 **볼록 `MeshCollider`**(같은 Cylinder
메시, `Convex = true`)를 붙인다.

**⚠️ 아트는 체크아웃이 둘이다.** 에디터가 여는 것은 **클라의 서브모듈**
(`LeagueOfPhysical-Client/Assets/Art`)이다. 독립 클론(`LOP/LeagueOfPhysical-Art`)에 만들면 에디터가
영영 못 본다. 서브모듈 안에서 브랜치를 파고 고친 뒤, 아트 main에 머지·푸시하고, **클라에서
`git add Assets/Art`만 지정해 포인터를 커밋**한다.

- [ ] **Step 2: 두 클라로 실제 판을 돌린다**

`unity` CLI로 메인 에디터와 MPPM 클론을 둘 다 몰 수 있다(`--project-path`에 클론은
`Library/VP/<clone>` 경로). 확인할 것:

| 보는 것 | 통과 기준 |
|---|---|
| 과녁이 뜬다 | 1.76초마다 2~3개, 무대 위 공중에 |
| **양쪽이 같은 과녁을 본다** | 두 화면의 과녁 위치·크기가 같다. **이게 깨지면 나머지는 볼 필요가 없다** |
| 맞히면 점수 | 라벨이 오르고, 그 과녁이 **양쪽 화면에서** 사라진다 |
| 남이 먹은 과녁 | 내 화면에서도 사라진다 |
| 60초 뒤 순위 | 점수 높은 쪽이 1등. 서버 로그의 `[Outcome]`과 결과 화면이 일치 |
| 양쪽 다 로비로 | 클론도 방에서 나온다(슬라이스 1에서 확인한 항목 — 회귀 없나) |

> 화면이 "내 차례"처럼 보여도 서버는 다른 상태일 수 있다 — 판정은 **서버 로그와 대조**한다.

- [ ] **Step 3: 배포 — 어느 파이프라인이 필요한지 가른다**

| 바뀐 것 | 필요한 것 |
|---|---|
| 마스터데이터만 | ⚠️ **이미지 태그가 안 움직인다.** 게임서버 파드가 `imagePullPolicy: Always`라 재시작으로 반영되지만, **서버 레포 커밋이 함께 있으면** 태그가 오르며 자연히 롤아웃된다 |
| 게임서버 코드 | `gameserver` 파이프라인 |
| 맵 씬(아트) | **클라 레포 `content-deploy`** — 안 돌리면 게임 서버가 새 맵을 영영 못 받는다 |

이 슬라이스는 **셋 다** 해당한다. 순서: 아트 → 클라 포인터 → 각 레포 머지 → `content-deploy` →
게임서버 배포.

- [ ] **Step 4: 여덟 레포 머지 (규약대로, 한 줄씩)**

`CLAUDE.md`의 푸시 규약을 **레포마다 각각** 밟는다. `&&`로 잇지 말 것.

```bash
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/archery-slice2
git push origin main
```

- [ ] **Step 5: ROADMAP을 갱신한다**

`docs/ROADMAP.md`의 Archery 절을 슬라이스 2 상태로 고친다. 실측에서 드러난 것(있다면)을 표로 남기고,
**슬라이스 3에 이월된 것**을 적는다. 슬라이스 3은 **재미 확인 지점**이라는 사실을 눈에 띄게 남길 것 —
스펙 §12가 "기반이 틀렸으면 그 위에 더 얹기 전에 알아야 한다"고 못박아 두었다.

---

## 확인 목록 (머지 전에 한 번 더)

- [ ] `MessageIds.cs`가 **추가 한 줄뿐**이고 1~18번 값이 그대로다
- [ ] `EntitySnap`의 필드 번호를 재사용하지 않았다(새 번호 24)
- [ ] 두 MasterData 패키지의 `TableFiles`에 새 테이블 둘이 들어 있다
      (`TableFileManifestTests`가 지켜 준다 — 그 테스트가 통과하는지 확인)
- [ ] 클·서 `ArcheryConfigProvider`가 **같은 값**을 낸다(둘을 나란히 놓고 읽어 볼 것)
- [ ] `ArcheryScore`가 **클·서 양쪽 크리에이터**에 붙어 있다
      (`grep -c 'ArcheryScore' Assets/Scripts/Entity/*Creator.cs`)
- [ ] 먹힌 것 보관소가 월드의 저장/복원에 **안** 들어 있다
- [ ] **판을 하던 중 한쪽 클라를 끊었다 다시 붙였을 때**, 이미 먹힌 과녁이 되살아나 보이지 않는다
      (이 슬라이스에서 사건 → 상태로 바꾼 이유가 바로 이것이다)
- [ ] 새로 만든 `.cs`마다 `.meta`가 함께 스테이지됐다
- [ ] `git status --short`에 로컬 픽스처(`Assets/Art` 포인터를 제외한 `Jua-Regular SDF.asset`,
      `ProjectSettings/*`)가 **커밋에 섞이지 않았다**
- [ ] EditMode 테스트 전부 초록(클라·Shared·서버)
