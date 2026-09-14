# Archery 슬라이스 3 — 함정 과녁 · 점수 차감 · 손떨림 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 맞히면 안 되는 **함정 과녁**을 넣고, 맞히면 점수를 깎고, 오래 당기고 있으면 **조준이 흔들리게** 해서 "미리 당겨놓고 기다리기"에 대가를 붙인다.

**Architecture:** 함정은 과녁 종류의 속성(`is_trap`)일 뿐이고, 웨이브마다 함정 비율을 결정론 난수로 뽑는다 — 과녁은 여전히 **통신하지 않고 양쪽이 각자 계산**한다. 맞혔을 때 무슨 일이 일어나는가는 LOP-Shared의 **한 함수**(`ArcheryHitRules.Resolve`)로 모아 나중에 벌칙 종류를 바꿀 때 한 곳만 고치게 한다. 손떨림은 LOP-Shared의 **순수 함수**(`ArcheryShake.Offset`)로 두고 클라가 카메라에 얹는다.

**Tech Stack:** Unity 6000.3.16f1 · C# · Luban(MasterData) · VContainer · MessagePipe · Mirror · NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-09-11-archery-game-mode-design.md`
(앞 슬라이스: `docs/superpowers/plans/2026-09-11-archery-slice1.md`, `docs/superpowers/plans/2026-09-11-archery-slice2.md`)

---

## Global Constraints

- **결정론이 이 모드의 생명줄이다.** 과녁은 통신하지 않고 `(matchSeed, waveIndex)`로 양쪽이 계산한다. `ArcheryWaveGenerator.Fill`의 **난수를 꺼내는 순서가 곧 계약**이다 — 이 계획은 그 순서를 바꾸므로, 클·서가 **반드시 함께 배포**돼야 한다.
- **시뮬에 들어가는 코드는 구체 클래스를 클·서가 공유한다.** 인터페이스 seam을 두지 않는다(spec §6). I/O 어댑터에만 인터페이스를 쓴다.
- **`using GameFramework.World;`를 쓰지 않는다.** World 타입은 항상 풀 네임스페이스로 한정한다(`GameFramework.World.Entity`) — `UnityEngine.Component`와 이름이 겹친다.
- **Anemic Domain Model** — `Component` 서브클래스는 데이터와 읽기 전용 파생 속성만. 상태 변경 로직은 System에.
- **일반화된 효과 시스템을 미리 짓지 않는다**(spec §4). 효과가 둘(획득/차감)뿐인데 그걸 짓는 건 낭비다.
- **주석은 최소로, 쓸 때는 일상어로.** 코드로 자명한 것(무엇)은 쓰지 않고 비자명한 의도(왜)만 짧게. 전문용어를 설명 없이 던지지 않는다.
- **새 `.cs`마다 Unity가 만든 `.meta`를 함께 커밋**한다. `.meta`를 직접 만들거나 고치지 않는다.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고 커밋 전에 `git status --short`로 확인한다. 양쪽 Unity 레포에는 **커밋하면 안 되는 로컬 픽스처**가 늘 떠 있다(`Assets/Art` 포인터 제외한 `Jua-Regular SDF.asset`, `ProjectSettings/*`, `URPDefaultResources/*`, `ConfigureRoomComponent.cs`, `AddressableAssetSettings.asset`, `DefaultVolumeProfile.asset`).
- **커밋 메시지 꼬리표** (모든 커밋):
  ```
  Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
  ```
- **`#ArcheryConfig.xlsx` / `#ArcheryTarget.xlsx`에 컬럼을 더할 때는 반드시 맨 뒤에 붙인다.** Luban은 컬럼 *이름*이 아니라 *몇 번째 열*로 읽으므로, 중간에 끼우면 기존 값이 조용히 뒤바뀐다(`FlappyConfigColumnOrderTests`가 존재하는 이유).

---

## 이 슬라이스가 하지 않는 것

| 안 하는 것 | 왜 |
|---|---|
| **당길 때 줌인** | **이미 있다.** `ArcheryAimView`가 FOV 60 → 32로 좁힌다(슬라이스 1/2에서 들어감). spec §2.2의 대가 셋 중 첫째는 완료 상태다. |
| **사람을 맞혔을 때 쏜 사람 차감** | spec §4에 있지만 §12의 판정 질문("참는 것이 고민되나")과는 별개 축이다. 판정 뒤로 미룬다. |
| **조준점(reticle)** | 지금 게임에 조준점이 없다. 넣으면 *조준이 갑자기 정밀해져* 게임이 쉬워지고, 그건 이 슬라이스의 튜닝 목표와 정면으로 싸운다. 손떨림은 조준점 대신 **화면을 흔들어** 보여준다(아래 결정 참고). |
| **`ArcheryStateBroadcastSystem` 테스트** | 사용자가 유예한 항목. 이 슬라이스의 몸통이 아니다. |
| **직선맵** | spec §5의 둘째 맵. 원형맵으로 판정한 뒤. |

---

## 착수 전에 박아둘 결정 넷

계획을 실행하는 사람이 "왜 이렇게 했나"로 되돌아오지 않도록 먼저 못박는다.

### 결정 1 — 함정 비율은 설정의 범위에서 웨이브마다 뽑는다

spec §3은 *"함정 비율은 웨이브마다 다르다. 0개도, 전부도 가능"* 이라고 못박았다.

과녁 종류에 `is_trap`만 달고 **기존 가중치 뽑기에 맡기는** 방법도 있다(더 단순하다). 그러나 그러면 슬롯마다 독립적으로 뽑히므로 *"전부 함정인 웨이브"* 가 가중치의 세제곱으로만 나온다 — 전체 함정 빈도를 올리지 않고서는 그 순간을 늘릴 방법이 없다. **그런데 "전부 함정" 이야말로 §2.2가 말한 참기의 순간**이고, §12의 판정이 재려는 바로 그것이다. 그 빈도를 따로 조절할 손잡이가 없으면 판정을 못 한다.

그래서 **`#ArcheryConfig`에 `trap_ratio_min`/`trap_ratio_max`를 두고 웨이브마다 비율을 하나 뽑아**, 그 웨이브의 함정 개수를 정한다. 전체 함정 빈도와 "전부 함정" 빈도를 따로 움직일 수 있다.

### 결정 2 — 난수 소비 순서가 바뀐다 (계약 변경)

지금 순서:

```
개수 → 슬롯마다 (종류 → 각도 → 반지름 → 높이)
```

바뀐 뒤:

```
개수 → 함정비율 → 슬롯마다 (함정인가 → 종류 → 각도 → 반지름 → 높이)
```

**이것은 계약 변경이다.** 같은 씨앗이 슬라이스 2와 다른 과녁을 낸다. 클·서 한쪽만 배포되면 서로 다른 과녁을 보고 화면은 멀쩡한데 점수만 이상해진다 — **반드시 함께 배포**한다(머지 태스크에 명시).

"함정인가"를 슬롯마다 한 번 뽑는 이유: 함정 개수 `trapCount`를 먼저 정하고 앞 슬롯부터 채우면 난수를 아예 안 써도 되지만, 그러면 **슬롯 번호가 함정 여부와 상관**된다. 슬롯 번호는 와이어(`ArcheryStateToC`의 마스크)에 그대로 드러나므로, 상관이 생기면 마스크만 보고 함정 자리를 알아낼 수 있다. 슬롯마다 한 번 뽑아 그 상관을 없앤다.

### 결정 3 — 손떨림은 카메라를 흔든다 (조준점이 아니라)

이 게임의 조준은 **카메라가 보는 방향 그 자체**다(`ArcheryAimView`가 `camera.transform.eulerAngles`를 읽어 `SetAim`에 넣는다). 그래서 떨림을 얹을 곳이 둘이다:

| | 화면에 보이는 것 | 문제 |
|---|---|---|
| **조준점을 흔든다** | 화면은 가만, 조준 마커만 떠다님 | 지금 게임에 **조준점이 없다.** 넣는 순간 "정확히 여기로 간다"가 생겨 게임이 쉬워진다 — 튜닝 목표와 싸운다 |
| **카메라를 흔든다** ✅ | 세상이 흔들림. 조준 = 카메라 방향이라 자동으로 일치 | 없음. 총기 게임의 흔들림(weapon sway)과 같은 관용구 |

**카메라를 흔든다.** 조준이 곧 카메라 방향이므로 흔들린 방향이 그대로 `SetAim`에 실려 나가고, 보이는 것과 화살이 가는 곳이 저절로 일치한다. 새로 읽을 것을 하나도 안 만든다.

**흔드는 방법 — `CameraController`에 더할 각도를 준다.** `ArcheryAimView`가 카메라 transform을 직접 돌리면 **실행 순서에 걸린다**: `CameraController.LateUpdate`(실행 순서 3000)가 매 프레임 `mainCamera.transform.rotation`을 **통째로 덮어쓰므로**, `ArcheryAimView.LateTick`이 그보다 먼저 돌면 떨림이 조용히 사라진다. 그래서 `CameraController`가 **자기 회전을 만들 때 더하는 오프셋**을 노출하고, `ArcheryAimView`는 그 값만 채운다. 기본값 0이라 다른 모드는 영향이 없다.

**수식은 LOP-Shared의 순수 함수로 둔다** — spec §10이 *"당김 흔들림은 순수 계산이라 유니티 없이 단위 테스트가 된다"* 고 못박아 두었다. 난수가 아니라 **주기가 서로 안 맞는 사인파 둘의 합**으로 만든다: 난수를 틱마다 뽑으면 매끄럽지 않고 지직거려 "손떨림"이 아니라 잡음으로 보인다.

### 결정 4 — 와이어는 안 바뀐다

`ArcheryHitToC.points`가 이미 `int32`(부호 있음)다. 함정은 **음수**를 실어 보내면 되고, 클라의 점수 표시는 `EntitySnap.score`(스냅샷)가 진실원본이라 그대로 맞는다. **proto도 MessageId도 손대지 않는다** — 슬라이스 2가 겪은 코드생성 사고(`MessageIds.cs` 재생성)를 아예 안 건드리고 지나간다.

---

## 파일 구조

### LOP-Shared (`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared`)

| 파일 | 책임 |
|---|---|
| `Runtime/Scripts/Game/ArcheryTargetKind.cs` (수정) | 과녁 종류에 `IsTrap` 추가 |
| `Runtime/Scripts/Game/ArcheryTarget.cs` (수정) | 떠 있는 과녁 하나에 `IsTrap` 추가 |
| `Runtime/Scripts/Game/ArcheryConfig.cs` (수정) | 함정 비율 범위 · 흔들림 설정 + 성한/함정 종류 풀 분리(파생) |
| `Runtime/Scripts/Game/ArcheryWaveGenerator.cs` (수정) | 웨이브마다 함정 비율을 뽑고 슬롯마다 함정 여부를 정한다 |
| `Runtime/Scripts/Game/ArcheryHitRules.cs` (신규) | **맞혔을 때 무슨 일이 일어나는가** — spec §4가 요구한 "한 곳" |
| `Runtime/Scripts/Game/ArcheryShake.cs` (신규) | 당김 시간 → 조준 흔들림(도). 순수 함수 |
| `Runtime/Scripts/Game/ArcheryScore.cs` (수정 없음) | 이미 `Gained`/`Lost`로 갈라져 있다 |
| `Tests/EditMode/ArcheryWaveGeneratorTests.cs` (수정) | 함정 생성 테스트 추가 |
| `Tests/EditMode/ArcheryHitRulesTests.cs` (신규) | 획득/차감 규칙 |
| `Tests/EditMode/ArcheryShakeTests.cs` (신규) | 흔들림 수식 |

### MasterData (`LeagueOfPhysical-MasterData-Client` / `-Server`)

생성물만 바뀐다(`Runtime.Generated/`). 손으로 고치지 않는다.

| 파일 | 책임 |
|---|---|
| `Tests/EditMode/ArcheryTargetSeparationTests.cs` (서버, 수정) | 함정 종류가 최소 하나는 있는지도 함께 본다 |

### infrastructure (`C:/Users/re5na/workspace/LOP/infrastructure`)

| 파일 | 책임 |
|---|---|
| `table/Datas/#ArcheryTarget.xlsx` (수정) | `is_trap` 컬럼 + 함정 종류 줄 |
| `table/Datas/#ArcheryConfig.xlsx` (수정) | 함정 비율 범위 · 흔들림 설정 · 튜닝값 |

### Server (`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server`)

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Game/ArcheryConfigProvider.cs` (수정) | 새 컬럼을 `ArcheryConfig`로 옮긴다 |
| `Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs` (수정) | 점수 적립을 `ArcheryHitRules.Resolve`에 위임 |
| `Assets/Tests/Editor/ArcheryHitSystemTests.cs` (수정) | 함정 적중 테스트 |

### Client (`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client`)

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Game/ArcheryConfigProvider.cs` (수정) | 서버 쌍둥이와 **같은 값**을 내야 한다 |
| `Assets/Scripts/Game/ArcheryTargetView.cs` (수정) | 함정 과녁을 다른 색으로 그린다 |
| `Assets/Scripts/Game/ArcheryAimView.cs` (수정) | 흔들림을 카메라에 얹는다 |
| `Assets/Scripts/Game/CameraController.cs` (수정) | 더할 조준 오프셋을 받는다(기본 0) |

---

## Task 1: 과녁 종류에 "함정인가"를 붙인다 (LOP-Shared)

**Files:**
- Modify: `Runtime/Scripts/Game/ArcheryTargetKind.cs`
- Modify: `Runtime/Scripts/Game/ArcheryTarget.cs`
- Test: `Tests/EditMode/ArcheryWaveGeneratorTests.cs`

**Interfaces:**
- Consumes: (없음 — 첫 태스크)
- Produces:
  - `ArcheryTargetKind(float radius, int points, int weight, bool isTrap)` — 생성자 인자 넷. `public readonly bool IsTrap;`
  - `ArcheryTarget(int waveIndex, int slotIndex, Vector3 center, float radius, int points, bool isTrap)` — 생성자 인자 여섯. `public readonly bool IsTrap;`

> **왜 `ArcheryTarget`에도 넣나:** 적중 판정(`ArcheryHitSystem`)과 그리기(`ArcheryTargetView`)가 둘 다 "이 과녁이 함정인가"를 알아야 한다. 지금 `Points`를 그렇게 실어 나르고 있으므로 같은 짝으로 둔다 — 종류 목록을 되짚어 찾게 하면 찾는 코드가 두 군데 생긴다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/EditMode/ArcheryWaveGeneratorTests.cs`의 `Kinds()`를 함정 종류가 섞인 것으로 바꾸고, 생성된 과녁이 그 표시를 이어받는지 본다.

`Kinds()`를 아래로 교체한다:

```csharp
        private static ArcheryTargetKind[] Kinds()
        {
            return new[]
            {
                new ArcheryTargetKind(0.60f, 1, 50, false),
                new ArcheryTargetKind(0.40f, 2, 35, false),
                new ArcheryTargetKind(0.25f, 4, 15, false),
            };
        }
```

그리고 파일 끝 `목록을_다시_채우면_앞의_것이_남지_않는다` 테스트 **앞에** 새 테스트를 넣는다:

```csharp
        [Test]
        public void 과녁은_자기_종류의_함정_표시를_이어받는다()
        {
            var kinds = new[]
            {
                new ArcheryTargetKind(0.60f, 1, 50, false),
                new ArcheryTargetKind(0.40f, -3, 50, true),
            };
            var config = ConfigWith(kinds, TouchingDistance(kinds));
            var targets = new List<ArcheryTarget>();

            for (int wave = 0; wave < 100; wave++)
            {
                ArcheryWaveGenerator.Fill(targets, 11UL, wave, config);
                for (int i = 0; i < targets.Count; i++)
                {
                    //  함정 종류는 반경 0.40 하나뿐이라, 표시가 제대로 따라왔으면 둘이 항상 같이 움직인다.
                    bool fromRadius = Mathf.Approximately(targets[i].Radius, 0.40f);
                    Assert.AreEqual(fromRadius, targets[i].IsTrap, $"wave {wave} slot {i}");
                }
            }
        }
```

- [ ] **Step 2: 실패를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
```

기대: `failed: true`이고 에러에 `ArcheryTargetKind`의 생성자 인자 개수(CS1729) 또는 `IsTrap` 없음(CS1061)이 나온다.

> **LOP-Shared의 EditMode 테스트는 서버 에디터로 돌린다.** LOP-Shared는 `file:` 패키지라 클·서 양쪽 프로젝트가 같은 코드를 컴파일한다. 클라 에디터의 테스트 러너는 지금 멈춰 있다(2026-09-13).

- [ ] **Step 3: 최소 구현**

`Runtime/Scripts/Game/ArcheryTargetKind.cs`를 아래로 만든다:

```csharp
namespace LOP
{
    /// <summary>과녁 한 종류. 작을수록 맞히기 어렵고 그만큼 비싸다.</summary>
    public readonly struct ArcheryTargetKind
    {
        /// <summary>맞았다고 칠 반경(m).</summary>
        public readonly float Radius;

        /// <summary>맞히면 점수가 이만큼 움직인다. 함정은 음수다.</summary>
        public readonly int Points;

        /// <summary>뽑힐 상대 비율. 합이 100일 필요는 없다 — 서로의 크기만 의미가 있다.</summary>
        public readonly int Weight;

        /// <summary>맞히면 안 되는 과녁인가. 함정끼리, 성한 것끼리 따로 뽑는다.</summary>
        public readonly bool IsTrap;

        public ArcheryTargetKind(float radius, int points, int weight, bool isTrap)
        {
            Radius = radius;
            Points = points;
            Weight = weight;
            IsTrap = isTrap;
        }
    }
}
```

`Runtime/Scripts/Game/ArcheryTarget.cs`의 struct 본문을 아래로 바꾼다(주석은 그대로 둔다):

```csharp
    public readonly struct ArcheryTarget
    {
        public readonly int WaveIndex;
        public readonly int SlotIndex;
        public readonly Vector3 Center;
        public readonly float Radius;
        public readonly int Points;

        /// <summary>맞히면 안 되는 과녁인가.</summary>
        public readonly bool IsTrap;

        public ArcheryTarget(int waveIndex, int slotIndex, Vector3 center, float radius, int points, bool isTrap)
        {
            WaveIndex = waveIndex;
            SlotIndex = slotIndex;
            Center = center;
            Radius = radius;
            Points = points;
            IsTrap = isTrap;
        }
    }
```

`Runtime/Scripts/Game/ArcheryWaveGenerator.cs`의 `Fill` 안에서 과녁을 만드는 줄을 고친다(이 태스크에서는 표시만 이어 붙인다 — 함정 뽑기는 Task 3):

```csharp
                into.Add(new ArcheryTarget(waveIndex, slot, center, kind.Radius, kind.Points, kind.IsTrap));
```

- [ ] **Step 4: 통과를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
```

기대: `failed: false`, `errors: []`. **서버 레포의 `ArcheryHitSystemTests.Config()`도 `ArcheryTargetKind` 생성자를 부르므로 같이 고쳐야 컴파일된다** — 그 세 줄에 `, false`를 붙인다:

```csharp
                new ArcheryTargetKind(0.60f, 1, 50, false),
                new ArcheryTargetKind(0.40f, 2, 35, false),
                new ArcheryTargetKind(0.25f, 4, 15, false),
```

그다음:

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
```

기대: Failed 0. 결과의 `Summary`만 보지 말고 `과녁은_자기_종류의_함정_표시를_이어받는다`가 **이름으로** 목록에 있는지 확인한다.

- [ ] **Step 5: 커밋 (레포 둘)**

LOP-Shared:

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git checkout -b feature/archery-slice3
git add Runtime/Scripts/Game/ArcheryTargetKind.cs Runtime/Scripts/Game/ArcheryTarget.cs Runtime/Scripts/Game/ArcheryWaveGenerator.cs Tests/EditMode/ArcheryWaveGeneratorTests.cs
git status --short
git commit -F - <<'EOF'
feat(archery): 과녁 종류에 "맞히면 안 되는 것" 표시를 단다

함정은 새 개념이 아니라 과녁 종류의 속성이다. 뜨는 과녁이 그 표시를
이어받게 해서, 판정과 그리기가 종류 목록을 되짚지 않고 바로 알 수 있게 한다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

Server:

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout -b feature/archery-slice3
git add Assets/Tests/Editor/ArcheryHitSystemTests.cs
git status --short
git commit -F - <<'EOF'
test(archery): 과녁 종류 생성자에 함정 표시를 맞춘다

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

---

## Task 2: 설정에 함정 비율과 흔들림 칸을 낸다 (LOP-Shared)

**Files:**
- Modify: `Runtime/Scripts/Game/ArcheryConfig.cs`
- Test: `Tests/EditMode/ArcheryWaveGeneratorTests.cs`

**Interfaces:**
- Consumes: `ArcheryTargetKind.IsTrap` (Task 1)
- Produces:
  - `ArcheryConfig(int wavePeriodTicks, int minTargets, int maxTargets, float spawnRadius, float spawnMinY, float spawnMaxY, float minSeparation, float trapRatioMin, float trapRatioMax, float shakeFreeSeconds, float shakeRampSeconds, float shakeMaxDegrees, IReadOnlyList<ArcheryTargetKind> kinds)`
  - `float TrapRatioMin { get; }` · `float TrapRatioMax { get; }`
  - `float ShakeFreeSeconds { get; }` · `float ShakeRampSeconds { get; }` · `float ShakeMaxDegrees { get; }`
  - `IReadOnlyList<ArcheryTargetKind> CleanKinds { get; }` — 함정이 아닌 종류만 (파생)
  - `IReadOnlyList<ArcheryTargetKind> TrapKinds { get; }` — 함정 종류만 (파생)

> 생성자 인자가 열셋으로 길어진다. 부르는 곳이 넷뿐이고(클·서 provider, 테스트 둘) 전부 이름 붙인 인자를 쓰므로 읽기에 문제가 없다. 빌더를 만들지 않는다(YAGNI).

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/EditMode/ArcheryWaveGeneratorTests.cs`의 `ConfigWith`를 아래로 바꾼다:

```csharp
        private static ArcheryConfig ConfigWith(ArcheryTargetKind[] kinds, float minSeparation)
        {
            return ConfigWith(kinds, minSeparation, trapRatioMin: 0f, trapRatioMax: 0f);
        }

        private static ArcheryConfig ConfigWith(ArcheryTargetKind[] kinds, float minSeparation,
                                                float trapRatioMin, float trapRatioMax)
        {
            return new ArcheryConfig(
                wavePeriodTicks: 88, minTargets: 2, maxTargets: 3,
                spawnRadius: 2f, spawnMinY: 2f, spawnMaxY: 6f, minSeparation: minSeparation,
                trapRatioMin: trapRatioMin, trapRatioMax: trapRatioMax,
                shakeFreeSeconds: 1f, shakeRampSeconds: 2f, shakeMaxDegrees: 3f,
                kinds: kinds);
        }
```

그리고 새 테스트 둘을 `목록을_다시_채우면_앞의_것이_남지_않는다` 앞에 넣는다:

```csharp
        [Test]
        public void 종류를_성한_것과_함정으로_갈라_들고_있는다()
        {
            var kinds = new[]
            {
                new ArcheryTargetKind(0.60f, 1, 50, false),
                new ArcheryTargetKind(0.40f, -3, 30, true),
                new ArcheryTargetKind(0.25f, 4, 20, false),
            };
            var config = ConfigWith(kinds, TouchingDistance(kinds));

            Assert.AreEqual(2, config.CleanKinds.Count);
            Assert.AreEqual(1, config.TrapKinds.Count);
            Assert.IsTrue(config.TrapKinds[0].IsTrap);
        }

        [Test]
        public void 종류가_한쪽뿐이면_다른_쪽은_빈_목록이다()
        {
            var kinds = new[] { new ArcheryTargetKind(0.60f, 1, 50, false) };
            var config = ConfigWith(kinds, TouchingDistance(kinds));

            Assert.AreEqual(1, config.CleanKinds.Count);
            Assert.AreEqual(0, config.TrapKinds.Count);
        }
```

- [ ] **Step 2: 실패를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
```

기대: `failed: true`, 에러에 `ArcheryConfig` 생성자 인자 개수(CS1739/CS7036) 또는 `CleanKinds` 없음(CS1061).

- [ ] **Step 3: 최소 구현**

`Runtime/Scripts/Game/ArcheryConfig.cs`에서 `MaxTargetRadius` 속성 **아래에** 다음 속성들을 더한다:

```csharp
        /// <summary>한 웨이브에서 함정이 차지하는 비율의 하한(0~1).</summary>
        public float TrapRatioMin { get; }

        /// <summary>
        /// 한 웨이브에서 함정이 차지하는 비율의 상한(0~1). 웨이브마다 이 사이에서 하나를 뽑는다 —
        /// 그래야 "전부 함정인 웨이브"의 빈도를 전체 함정 빈도와 <b>따로</b> 조절할 수 있다.
        /// </summary>
        public float TrapRatioMax { get; }

        /// <summary>이 시간(초)까지는 당기고 있어도 손이 안 떨린다.</summary>
        public float ShakeFreeSeconds { get; }

        /// <summary>흔들림이 0에서 최대까지 자라는 데 걸리는 시간(초).</summary>
        public float ShakeRampSeconds { get; }

        /// <summary>가장 심할 때의 흔들림 폭(도).</summary>
        public float ShakeMaxDegrees { get; }

        /// <summary>함정이 아닌 종류만. 비어 있으면 성한 과녁이 안 뜬다.</summary>
        public IReadOnlyList<ArcheryTargetKind> CleanKinds { get; }

        /// <summary>함정 종류만. 비어 있으면 함정이 안 뜬다(비율을 아무리 올려도).</summary>
        public IReadOnlyList<ArcheryTargetKind> TrapKinds { get; }
```

생성자 시그니처를 바꾸고 본문 끝(`MaxTargetRadius = largest;` 뒤)에 풀 분리를 더한다:

```csharp
        public ArcheryConfig(int wavePeriodTicks, int minTargets, int maxTargets,
                             float spawnRadius, float spawnMinY, float spawnMaxY, float minSeparation,
                             float trapRatioMin, float trapRatioMax,
                             float shakeFreeSeconds, float shakeRampSeconds, float shakeMaxDegrees,
                             IReadOnlyList<ArcheryTargetKind> kinds)
        {
            WavePeriodTicks = wavePeriodTicks;
            MinTargets = minTargets;
            MaxTargets = maxTargets;
            SpawnRadius = spawnRadius;
            SpawnMinY = spawnMinY;
            SpawnMaxY = spawnMaxY;
            MinSeparation = minSeparation;
            TrapRatioMin = trapRatioMin;
            TrapRatioMax = trapRatioMax;
            ShakeFreeSeconds = shakeFreeSeconds;
            ShakeRampSeconds = shakeRampSeconds;
            ShakeMaxDegrees = shakeMaxDegrees;
            Kinds = kinds;

            float largest = 0f;
            var clean = new List<ArcheryTargetKind>();
            var traps = new List<ArcheryTargetKind>();
            if (kinds != null)
            {
                for (int i = 0; i < kinds.Count; i++)
                {
                    if (kinds[i].Radius > largest)
                    {
                        largest = kinds[i].Radius;
                    }
                    if (kinds[i].IsTrap)
                    {
                        traps.Add(kinds[i]);
                    }
                    else
                    {
                        clean.Add(kinds[i]);
                    }
                }
            }
            MaxTargetRadius = largest;
            CleanKinds = clean;
            TrapKinds = traps;
        }
```

- [ ] **Step 4: 통과를 확인한다**

**서버 레포의 `ArcheryHitSystemTests.Build()`도 같은 생성자를 부른다** — 아래로 고친다:

```csharp
        static ArcheryConfig Build(ArcheryTargetKind[] kinds, float minSeparation)
            => new ArcheryConfig(
                wavePeriodTicks: 88, minTargets: 2, maxTargets: 3,
                spawnRadius: 2f, spawnMinY: 2f, spawnMaxY: 6f, minSeparation: minSeparation,
                trapRatioMin: 0f, trapRatioMax: 0f,
                shakeFreeSeconds: 1f, shakeRampSeconds: 2f, shakeMaxDegrees: 3f,
                kinds: kinds);
```

그다음:

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
```

기대: 컴파일 `failed: false`, 테스트 Failed 0, 새 테스트 두 이름이 결과에 있다.

> 이 시점에 **클·서 `ArcheryConfigProvider`는 아직 안 고쳤으므로 컴파일이 깨진다.** 그 둘은 Task 5(서버)·Task 7(클라)에서 고친다. 그때까지 `recompile_status`에 `ArcheryConfigProvider.cs`의 CS7036이 남는 것은 **예상된 상태**다 — 그 파일 말고 다른 에러가 있으면 그것만 본다.
>
> **더 나은 방법:** 이 태스크에서 provider 둘의 호출부도 함께 고쳐 컴파일을 늘 초록으로 둔다. 아래 Step 5가 그렇게 한다.

- [ ] **Step 5: provider 둘의 호출부를 임시값으로 맞춰 컴파일을 초록으로 둔다**

서버 `Assets/Scripts/Game/ArcheryConfigProvider.cs`의 `return new ArcheryConfig(...)`를 아래로:

```csharp
            return new ArcheryConfig(
                r.WavePeriodTicks, r.MinTargets, r.MaxTargets,
                r.SpawnRadius, r.SpawnMinY, r.SpawnMaxY, r.MinSeparation,
                //  아직 데이터에 칸이 없다 — Task 4에서 마스터데이터를 구운 뒤 실제 컬럼으로 바꾼다.
                trapRatioMin: 0f, trapRatioMax: 0f,
                shakeFreeSeconds: 1f, shakeRampSeconds: 2f, shakeMaxDegrees: 3f,
                kinds);
```

클라 `Assets/Scripts/Game/ArcheryConfigProvider.cs`도 **똑같이** 고친다. 두 파일을 나란히 놓고 값이 한 글자도 다르지 않은지 확인한다 — 다르면 클·서가 다른 과녁을 본다.

또한 두 provider의 `kinds.Add(...)` 줄에 함정 표시를 더한다(아직 컬럼이 없으므로 `false` 고정):

```csharp
                kinds.Add(new ArcheryTargetKind(row.Radius, row.Points, row.Weight, false));
```

그다음 컴파일·테스트를 다시 돌려 초록을 확인한다.

- [ ] **Step 6: 커밋 (레포 셋)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/ArcheryConfig.cs Tests/EditMode/ArcheryWaveGeneratorTests.cs
git status --short
git commit -F - <<'EOF'
feat(archery): 설정에 함정 비율과 흔들림 칸을 낸다

함정 비율은 웨이브마다 범위에서 뽑는다 — 그래야 "전부 함정인 웨이브"의
빈도를 전체 함정 빈도와 따로 조절할 수 있다. 그 순간이 이 모드가 재려는
"참을까 말까"의 핵심이라 손잡이가 따로 있어야 한다.

종류 목록은 성한 것과 함정으로 갈라 파생 속성으로 들고 있는다 — 뽑을 때
매번 훑지 않게.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

Server (브랜치는 Task 1에서 이미 만들었다):

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/ArcheryConfigProvider.cs Assets/Tests/Editor/ArcheryHitSystemTests.cs
git status --short
git commit -F - <<'EOF'
chore(archery): 설정 생성자 변경에 호출부를 맞춘다

새 칸은 아직 마스터데이터에 없어 임시값이다. 데이터를 구운 뒤 실제
컬럼으로 바꾼다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

Client (**여기서 브랜치를 처음 만든다**):

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git checkout -b feature/archery-slice3
git add Assets/Scripts/Game/ArcheryConfigProvider.cs
git status --short
git commit -F - <<'EOF'
chore(archery): 설정 생성자 변경에 호출부를 맞춘다

새 칸은 아직 마스터데이터에 없어 임시값이다. 데이터를 구운 뒤 실제
컬럼으로 바꾼다. 서버 쌍둥이와 한 글자도 다르면 안 된다 — 다르면
클·서가 다른 과녁을 본다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

---

## Task 3: 웨이브 생성기가 함정을 뽑는다 (LOP-Shared)

**Files:**
- Modify: `Runtime/Scripts/Game/ArcheryWaveGenerator.cs`
- Test: `Tests/EditMode/ArcheryWaveGeneratorTests.cs`

**Interfaces:**
- Consumes: `ArcheryConfig.TrapRatioMin/TrapRatioMax/CleanKinds/TrapKinds` (Task 2), `ArcheryTargetKind.IsTrap` (Task 1)
- Produces: `ArcheryWaveGenerator.Fill`의 난수 소비 순서가 **개수 → 함정비율 → 슬롯마다 (함정인가 → 종류 → 각도 → 반지름 → 높이)** 로 바뀐다

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/EditMode/ArcheryWaveGeneratorTests.cs`에 넷을 더한다(`목록을_다시_채우면...` 앞):

```csharp
        //  이 비율 손잡이가 실제로 듣는지 본다 — 안 들으면 "참을까 말까"를 조절할 방법이 없다.
        [Test]
        public void 비율을_0으로_두면_함정이_하나도_안_뜬다()
        {
            var kinds = TrapMixedKinds();
            var config = ConfigWith(kinds, TouchingDistance(kinds), trapRatioMin: 0f, trapRatioMax: 0f);
            var targets = new List<ArcheryTarget>();

            for (int wave = 0; wave < 300; wave++)
            {
                ArcheryWaveGenerator.Fill(targets, 3UL, wave, config);
                for (int i = 0; i < targets.Count; i++)
                {
                    Assert.IsFalse(targets[i].IsTrap, $"wave {wave} slot {i}");
                }
            }
        }

        [Test]
        public void 비율을_1로_두면_전부_함정이다()
        {
            var kinds = TrapMixedKinds();
            var config = ConfigWith(kinds, TouchingDistance(kinds), trapRatioMin: 1f, trapRatioMax: 1f);
            var targets = new List<ArcheryTarget>();

            for (int wave = 0; wave < 300; wave++)
            {
                ArcheryWaveGenerator.Fill(targets, 3UL, wave, config);
                for (int i = 0; i < targets.Count; i++)
                {
                    Assert.IsTrue(targets[i].IsTrap, $"wave {wave} slot {i}");
                }
            }
        }

        //  spec 3절: "0개도, 전부도 가능". 범위를 열어 두면 양 끝이 실제로 나와야 한다.
        [Test]
        public void 범위를_열어_두면_전부_성한_웨이브와_전부_함정인_웨이브가_둘_다_나온다()
        {
            var kinds = TrapMixedKinds();
            var config = ConfigWith(kinds, TouchingDistance(kinds), trapRatioMin: 0f, trapRatioMax: 1f);
            var targets = new List<ArcheryTarget>();

            bool sawAllClean = false;
            bool sawAllTrap = false;
            for (int wave = 0; wave < 300; wave++)
            {
                ArcheryWaveGenerator.Fill(targets, 7UL, wave, config);
                int traps = 0;
                for (int i = 0; i < targets.Count; i++)
                {
                    traps += targets[i].IsTrap ? 1 : 0;
                }
                sawAllClean |= traps == 0;
                sawAllTrap |= traps == targets.Count && targets.Count > 0;
            }

            Assert.IsTrue(sawAllClean, "전부 성한 웨이브가 한 번도 안 나왔다");
            Assert.IsTrue(sawAllTrap, "전부 함정인 웨이브가 한 번도 안 나왔다");
        }

        //  데이터에 함정 종류가 없는데 비율만 올려 둔 경우. 조용히 성한 과녁을 함정으로 만들면 안 된다.
        [Test]
        public void 함정_종류가_없으면_비율이_1이어도_함정이_안_뜬다()
        {
            var kinds = Kinds();
            var config = ConfigWith(kinds, TouchingDistance(kinds), trapRatioMin: 1f, trapRatioMax: 1f);
            var targets = new List<ArcheryTarget>();

            for (int wave = 0; wave < 100; wave++)
            {
                ArcheryWaveGenerator.Fill(targets, 9UL, wave, config);
                Assert.That(targets.Count, Is.InRange(config.MinTargets, config.MaxTargets),
                            $"wave {wave}: 과녁이 아예 안 떴다");
                for (int i = 0; i < targets.Count; i++)
                {
                    Assert.IsFalse(targets[i].IsTrap, $"wave {wave} slot {i}");
                }
            }
        }

        private static ArcheryTargetKind[] TrapMixedKinds()
        {
            return new[]
            {
                new ArcheryTargetKind(0.60f, 1, 50, false),
                new ArcheryTargetKind(0.40f, 2, 35, false),
                new ArcheryTargetKind(0.50f, -3, 40, true),
            };
        }
```

- [ ] **Step 2: 실패를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
```

기대: `비율을_1로_두면_전부_함정이다`와 `범위를_열어_두면...`이 실패한다(아직 함정을 뽑지 않으므로 전부 성한 것만 나온다).

- [ ] **Step 3: 최소 구현**

`Runtime/Scripts/Game/ArcheryWaveGenerator.cs`의 `Fill` 본문(난수 생성 뒤)을 아래로 바꾼다:

```csharp
            int count = rng.Range(config.MinTargets, config.MaxTargets + 1);

            //  이 웨이브에 함정을 몇 개 둘지 먼저 정한다. 슬롯마다 따로 뽑으면 "전부 함정"이
            //  확률의 곱으로만 나와서, 그 순간의 빈도를 따로 조절할 수 없다.
            int trapCount = 0;
            if (config.TrapKinds.Count > 0)
            {
                float ratio = rng.Range(config.TrapRatioMin, config.TrapRatioMax);
                trapCount = Mathf.Clamp(Mathf.RoundToInt(ratio * count), 0, count);
            }

            int remainingTraps = trapCount;
            for (int slot = 0; slot < count; slot++)
            {
                //  남은 슬롯 중 남은 함정 수만큼의 확률로 이 자리를 함정으로 만든다. 슬롯 번호와
                //  함정 여부가 상관되지 않게 하려는 것이다 — 슬롯 번호는 와이어(먹힌 마스크)에
                //  그대로 드러나므로, 상관이 있으면 마스크만 보고 함정 자리를 알 수 있다.
                int remainingSlots = count - slot;
                bool isTrap = rng.Range(0, remainingSlots) < remainingTraps;
                if (isTrap)
                {
                    remainingTraps--;
                }

                var pool = isTrap ? config.TrapKinds : config.CleanKinds;
                if (pool.Count == 0)
                {
                    pool = config.Kinds;   // 한쪽 데이터가 비었다 — 판이 멈추는 것보다 낫다
                }

                ArcheryTargetKind kind = PickKind(pool, ref rng);
                Vector3 center = PickCenter(into, config, ref rng);
                into.Add(new ArcheryTarget(waveIndex, slot, center, kind.Radius, kind.Points, kind.IsTrap));
            }
```

`Fill`의 XML 주석에서 난수 순서를 적은 줄을 새 순서로 고친다:

```csharp
        /// <b>난수를 꺼내는 순서가 곧 계약이다</b> — 개수 → 함정 비율 → 슬롯마다
        /// (함정인가 → 종류 → 각도 → 반지름 → 높이).
        /// 이 순서를 바꾸면 같은 씨앗이 다른 과녁을 내놓아 클·서가 갈린다.
```

- [ ] **Step 4: 통과를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
```

기대: 컴파일 초록, Failed 0. **기존 결정론 테스트(`같은_씨앗과_웨이브는_언제_몇_번_물어도_같은_과녁을_준다`)가 여전히 통과해야 한다** — 이 슬라이스가 순서를 바꿨어도 *같은 코드끼리는* 여전히 같은 답을 내야 한다는 뜻이다.

- [ ] **Step 5: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/ArcheryWaveGenerator.cs Tests/EditMode/ArcheryWaveGeneratorTests.cs
git status --short
git commit -F - <<'EOF'
feat(archery): 웨이브마다 함정 비율을 뽑아 자리를 정한다

난수 소비 순서가 바뀐다(개수 -> 함정비율 -> 슬롯마다 함정인가 -> 종류 ->
자리). 순서가 곧 클·서 계약이므로 양쪽이 반드시 함께 배포돼야 한다.

함정 자리는 슬롯 번호와 상관되지 않게 고른다 — 슬롯 번호는 먹힌 마스크로
와이어에 드러나므로, 상관이 있으면 마스크만 보고 함정 자리를 알 수 있다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

---

## Task 4: 맞혔을 때 무슨 일이 일어나는가를 한 함수로 (LOP-Shared)

**Files:**
- Create: `Runtime/Scripts/Game/ArcheryHitRules.cs`
- Test: `Tests/EditMode/ArcheryHitRulesTests.cs` (신규)

**Interfaces:**
- Consumes: `ArcheryTarget.IsTrap`, `ArcheryTarget.Points` (Task 1)
- Produces:
  - `readonly struct ArcheryHitOutcome { public readonly int Gained; public readonly int Lost; public int Delta => Gained - Lost; }`
  - `static ArcheryHitOutcome ArcheryHitRules.Resolve(in ArcheryTarget target)`

> spec §4: *"벌칙의 종류는 한 곳으로 모은다 — 과녁을 맞혔을 때 무슨 일이 일어나는가를 한 함수에 모아, 나중에 그 한 곳만 고치면 되게 한다."* 이 태스크가 그 한 곳을 만든다.
>
> **`Gained`/`Lost`를 따로 돌려주는 이유:** `ArcheryScore`가 이미 그 둘로 갈라져 있고(결과 화면이 획득/벌점 내역을 보여준다), 합계만 돌려주면 부르는 쪽이 부호를 보고 다시 갈라야 한다 — 그 가르는 코드가 두 군데 생긴다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/EditMode/ArcheryHitRulesTests.cs`를 만든다:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryHitRulesTests
    {
        private static ArcheryTarget Target(int points, bool isTrap)
        {
            return new ArcheryTarget(0, 0, Vector3.zero, 0.5f, points, isTrap);
        }

        [Test]
        public void 성한_과녁은_점수를_준다()
        {
            var outcome = ArcheryHitRules.Resolve(Target(points: 3, isTrap: false));

            Assert.AreEqual(3, outcome.Gained);
            Assert.AreEqual(0, outcome.Lost);
            Assert.AreEqual(3, outcome.Delta);
        }

        //  데이터에 함정 점수를 음수로 적든 양수로 적든 같은 벌점이 나와야 한다.
        //  적는 사람이 부호를 어느 쪽으로 쓸지 정해 두지 않으면 값이 두 배로 틀린다.
        [Test]
        public void 함정은_음수로_적어도_양수로_적어도_같은_벌점이다()
        {
            var written = ArcheryHitRules.Resolve(Target(points: -3, isTrap: true));
            var writtenPositive = ArcheryHitRules.Resolve(Target(points: 3, isTrap: true));

            Assert.AreEqual(0, written.Gained);
            Assert.AreEqual(3, written.Lost);
            Assert.AreEqual(-3, written.Delta);

            Assert.AreEqual(written.Gained, writtenPositive.Gained);
            Assert.AreEqual(written.Lost, writtenPositive.Lost);
        }

        [Test]
        public void 함정_점수가_0이면_아무_일도_안_일어난다()
        {
            var outcome = ArcheryHitRules.Resolve(Target(points: 0, isTrap: true));

            Assert.AreEqual(0, outcome.Gained);
            Assert.AreEqual(0, outcome.Lost);
            Assert.AreEqual(0, outcome.Delta);
        }

        //  성한 과녁에 음수를 적어 둔 데이터. 점수를 몰래 깎는 대신 아무것도 주지 않는다.
        [Test]
        public void 성한_과녁에_음수가_적혀_있으면_점수를_주지_않는다()
        {
            var outcome = ArcheryHitRules.Resolve(Target(points: -5, isTrap: false));

            Assert.AreEqual(0, outcome.Gained);
            Assert.AreEqual(0, outcome.Lost);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
```

기대: `failed: true`, `ArcheryHitRules`를 찾을 수 없음(CS0103).

- [ ] **Step 3: 최소 구현**

`Runtime/Scripts/Game/ArcheryHitRules.cs`를 만든다:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>과녁 하나를 맞힌 결과. 획득과 벌점을 따로 들고 있다 — 결과 화면이 내역을 보여준다.</summary>
    public readonly struct ArcheryHitOutcome
    {
        public readonly int Gained;
        public readonly int Lost;

        /// <summary>점수가 실제로 움직이는 양. 연출("+2"/"−3")에 쓴다.</summary>
        public int Delta => Gained - Lost;

        public ArcheryHitOutcome(int gained, int lost)
        {
            Gained = gained;
            Lost = lost;
        }
    }

    /// <summary>
    /// <b>과녁을 맞혔을 때 무슨 일이 일어나는가.</b> 이 게임에서 그것을 정하는 곳은 여기 하나뿐이다 —
    /// 나중에 벌칙을 "점수 차감" 말고 다른 것(예: 몇 초간 활을 못 당김)으로 바꾸고 싶어지면
    /// 이 함수만 고치면 된다.
    ///
    /// <para>일반화된 효과 시스템을 짓지 않는다. 지금 효과가 둘뿐이라 그건 낭비다.</para>
    /// </summary>
    public static class ArcheryHitRules
    {
        public static ArcheryHitOutcome Resolve(in ArcheryTarget target)
        {
            if (target.IsTrap)
            {
                //  데이터에 -3으로 적든 3으로 적든 같은 벌점이 되게 한다. 적는 사람이 부호를
                //  어느 쪽으로 쓸지 헷갈려도 값이 두 배로 틀리지 않는다.
                return new ArcheryHitOutcome(0, Mathf.Abs(target.Points));
            }

            //  성한 과녁에 음수가 적혀 있으면 점수를 몰래 깎는 대신 아무것도 주지 않는다.
            return new ArcheryHitOutcome(Mathf.Max(target.Points, 0), 0);
        }
    }
}
```

- [ ] **Step 4: 통과를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
```

기대: 컴파일 초록, Failed 0, `ArcheryHitRulesTests`의 테스트 넷이 이름으로 보인다.

- [ ] **Step 5: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/ArcheryHitRules.cs Runtime/Scripts/Game/ArcheryHitRules.cs.meta Tests/EditMode/ArcheryHitRulesTests.cs Tests/EditMode/ArcheryHitRulesTests.cs.meta
git status --short
git commit -F - <<'EOF'
feat(archery): 맞혔을 때 무슨 일이 일어나는지를 한 함수로 모은다

벌칙 종류를 나중에 바꾸고 싶어질 수 있다(예: 몇 초간 활을 못 당김).
그때 고칠 곳이 하나가 되도록 "과녁을 맞혔을 때"의 결정을 한 자리에 둔다.
일반화된 효과 시스템은 짓지 않는다 — 효과가 둘뿐이다.

데이터에 함정 점수를 음수로 적든 양수로 적든 같은 벌점이 나오게 한다.
부호 규약을 안 정해 두면 값이 두 배로 틀린다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

> `.meta`는 Unity가 만든다. `recompile` 뒤에도 안 보이면 에디터가 에셋을 아직 못 봤다는 뜻이니 잠시 뒤 다시 `ls`로 확인한다. **직접 만들지 않는다.**

---

## Task 5: 손떨림 수식 (LOP-Shared)

**Files:**
- Create: `Runtime/Scripts/Game/ArcheryShake.cs`
- Test: `Tests/EditMode/ArcheryShakeTests.cs` (신규)

**Interfaces:**
- Consumes: `ArcheryConfig.ShakeFreeSeconds/ShakeRampSeconds/ShakeMaxDegrees` (Task 2)
- Produces: `static Vector2 ArcheryShake.Offset(float heldSeconds, int phaseSeed, ArcheryConfig config)` — `x`는 좌우(yaw) 도, `y`는 위아래(pitch) 도. 위가 양수인 조준 좌표계다.
- Produces: `static int ArcheryShake.PhaseSeedOf(string entityId)` — 사람마다 다른 위상

> **왜 난수가 아니라 사인파인가:** 틱마다 난수를 뽑으면 값이 매끄럽지 않아 지직거린다 — "손이 떨린다"가 아니라 화면이 고장 난 것처럼 보인다. 주기가 서로 안 맞는 사인파 둘을 더하면 규칙이 안 보이면서도 매끄럽다. 덤으로 **난수를 하나도 안 써서** 클·서 난수 소비 계약을 건드리지 않는다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/EditMode/ArcheryShakeTests.cs`를 만든다:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryShakeTests
    {
        private static ArcheryConfig Config(float free = 1f, float ramp = 2f, float max = 3f)
        {
            return new ArcheryConfig(
                wavePeriodTicks: 88, minTargets: 2, maxTargets: 3,
                spawnRadius: 2f, spawnMinY: 2f, spawnMaxY: 6f, minSeparation: 1.2f,
                trapRatioMin: 0f, trapRatioMax: 0f,
                shakeFreeSeconds: free, shakeRampSeconds: ramp, shakeMaxDegrees: max,
                kinds: new[] { new ArcheryTargetKind(0.6f, 1, 50, false) });
        }

        //  짧게 당겼다 놓는 평소 사격이 흔들리면 안 된다 — 그러면 벌이 아니라 잡음이다.
        [Test]
        public void 유예_시간_안에는_전혀_안_흔들린다()
        {
            var config = Config(free: 1f);
            for (int i = 0; i < 20; i++)
            {
                float held = i * 0.05f;
                Assert.AreEqual(Vector2.zero, ArcheryShake.Offset(held, 12345, config), $"held={held}");
            }

            //  경계 그 자체. 유예 시간과 정확히 같은 값이면 아직 안 흔들려야 한다.
            Assert.AreEqual(Vector2.zero, ArcheryShake.Offset(config.ShakeFreeSeconds, 12345, config));
        }

        [Test]
        public void 오래_들고_있을수록_더_흔들린다()
        {
            var config = Config(free: 1f, ramp: 2f, max: 3f);

            float early = Amplitude(config, heldSeconds: 1.5f);
            float late = Amplitude(config, heldSeconds: 2.9f);

            Assert.Greater(late, early);
        }

        [Test]
        public void 아무리_오래_들고_있어도_정해진_폭을_안_넘는다()
        {
            var config = Config(free: 1f, ramp: 2f, max: 3f);

            for (float t = 1f; t <= 30f; t += 0.05f)
            {
                var offset = ArcheryShake.Offset(t, 777, config);
                Assert.LessOrEqual(Mathf.Abs(offset.x), 3f + 1e-3f, $"held={t}");
                Assert.LessOrEqual(Mathf.Abs(offset.y), 3f + 1e-3f, $"held={t}");
            }
        }

        //  같은 입력이면 언제 물어도 같은 답 — 클라와 서버가 같은 값을 봐야 한다.
        [Test]
        public void 같은_입력이면_언제_물어도_같다()
        {
            var config = Config();
            for (float t = 0f; t <= 5f; t += 0.17f)
            {
                Assert.AreEqual(ArcheryShake.Offset(t, 42, config), ArcheryShake.Offset(t, 42, config));
            }
        }

        [Test]
        public void 사람마다_다르게_흔들린다()
        {
            var config = Config();
            int a = ArcheryShake.PhaseSeedOf("entity-a");
            int b = ArcheryShake.PhaseSeedOf("entity-b");

            Assert.AreNotEqual(a, b);
            //  위상은 해시의 아래 16비트만 쓰므로 서로 다른 id가 같은 위상이 될 확률이 6만분의 1쯤
            //  있다. 이 두 id는 25692와 26901로 갈린다(계획 단계에서 FNV를 직접 계산해 확인).
            //  실패하면 id를 바꾸지 말고 위상 비트 수를 늘릴 것 — 겹침이 실제로 난다는 신호다.
            Assert.AreNotEqual(ArcheryShake.Offset(2.5f, a, config), ArcheryShake.Offset(2.5f, b, config));
        }

        [Test]
        public void 같은_사람이면_위상도_같다()
        {
            Assert.AreEqual(ArcheryShake.PhaseSeedOf("entity-a"), ArcheryShake.PhaseSeedOf("entity-a"));
        }

        //  값이 뚝뚝 끊기면 손떨림이 아니라 화면 고장으로 보인다.
        [Test]
        public void 값이_매끄럽게_이어진다()
        {
            var config = Config(free: 1f, ramp: 2f, max: 3f);
            var previous = ArcheryShake.Offset(1f, 99, config);

            for (float t = 1.01f; t <= 6f; t += 0.01f)
            {
                var current = ArcheryShake.Offset(t, 99, config);
                //  끊김을 잡는 것이 목적이다. 값이 튀면(위상이 갑자기 바뀌면) 폭의 두 배인 6도쯤
                //  움직이므로, 매끄러울 때의 최대 변화(10ms에 약 0.3도)보다 넉넉한 0.5도를 문턱으로
                //  둔다 — 더 조이면 사인파의 정상 기울기에 걸려 거짓 실패가 난다.
                Assert.Less((current - previous).magnitude, 0.5f, $"held={t}");
                previous = current;
            }
        }

        private static float Amplitude(ArcheryConfig config, float heldSeconds)
        {
            //  한 시점의 값은 사인파의 위상 때문에 작을 수 있다 — 잠깐 동안의 최댓값으로 폭을 잰다.
            float peak = 0f;
            for (float t = heldSeconds; t < heldSeconds + 1f; t += 0.01f)
            {
                peak = Mathf.Max(peak, ArcheryShake.Offset(t, 12345, config).magnitude);
            }
            return peak;
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
```

기대: `failed: true`, `ArcheryShake`를 찾을 수 없음(CS0103).

- [ ] **Step 3: 최소 구현**

`Runtime/Scripts/Game/ArcheryShake.cs`를 만든다:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 오래 당기고 있으면 조준이 흔들린다. 미리 당겨놓고 무한정 기다리지 못하게 하는 대가다.
    ///
    /// <para><b>난수가 아니라 사인파</b>로 만든다. 틱마다 난수를 뽑으면 값이 매끄럽지 않아 지직거려서
    /// "손이 떨린다"가 아니라 화면이 고장 난 것처럼 보인다. 주기가 서로 안 맞는 사인파 둘을 더하면
    /// 규칙이 눈에 안 보이면서도 매끄럽다. 난수를 안 쓰므로 클·서 난수 소비 계약도 안 건드린다.</para>
    /// </summary>
    public static class ArcheryShake
    {
        //  두 주기가 서로 나누어떨어지지 않아야 같은 모양이 반복되지 않는다(초당 진동 수).
        private const float SlowHz = 0.7f;
        private const float FastHz = 1.7f;

        //  위아래는 좌우와 다른 주기로 움직여야 원을 그리지 않고 불규칙해 보인다.
        private const float SlowHzPitch = 0.9f;
        private const float FastHzPitch = 2.1f;

        /// <summary>빠른 쪽이 전체에서 차지하는 몫. 느린 흔들림 위에 잔떨림이 얹힌 모양이 된다.</summary>
        private const float FastWeight = 0.35f;

        /// <summary>
        /// 이만큼 당기고 있었을 때의 조준 흔들림(도). x는 좌우, y는 위아래이며 <b>위가 양수</b>다
        /// (조준 각도와 같은 좌표계 — 유니티 카메라의 x 회전은 반대다).
        /// </summary>
        public static Vector2 Offset(float heldSeconds, int phaseSeed, ArcheryConfig config)
        {
            float ramped = heldSeconds - config.ShakeFreeSeconds;
            if (ramped <= 0f || config.ShakeMaxDegrees <= 0f)
            {
                return Vector2.zero;
            }

            float grow = config.ShakeRampSeconds > 0f
                ? Mathf.Clamp01(ramped / config.ShakeRampSeconds)
                : 1f;
            float amplitude = config.ShakeMaxDegrees * grow;

            //  위상을 사람마다 다르게 준다 — 안 그러면 모두가 똑같이 흔들린다.
            //  0~1로 접어 넣는 것이 중요하다: 큰 수를 그대로 쓰면 사인에 넣는 값이 백만 단위가 되어
            //  float의 정밀도가 그 자리에서 1 근처로 떨어진다. 즉 흔들림이 뭉개진다.
            float phase = (phaseSeed & 0xFFFF) / 65536f;

            float yaw = Wave(heldSeconds, phase, SlowHz, FastHz);
            float pitch = Wave(heldSeconds, phase + 1.7f, SlowHzPitch, FastHzPitch);

            return new Vector2(yaw * amplitude, pitch * amplitude);
        }

        /// <summary>사람마다 다른 위상을 준다. 같은 사람이면 언제 물어도 같다.</summary>
        public static int PhaseSeedOf(string entityId)
        {
            if (string.IsNullOrEmpty(entityId))
            {
                return 0;
            }

            //  FNV-1a. 문자열 해시를 직접 쓰지 않는 이유는 런타임마다 값이 달라질 수 있어서다 —
            //  클라와 서버가 다른 위상을 보면 보이는 것과 화살 가는 곳이 갈린다.
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < entityId.Length; i++)
                {
                    hash ^= entityId[i];
                    hash *= 16777619u;
                }
                return (int)(hash & 0x7FFFFFFF);
            }
        }

        //  느린 파 + 빠른 파. 합이 -1~1을 넘지 않도록 몫을 나눠 둔다.
        private static float Wave(float seconds, float phase, float slowHz, float fastHz)
        {
            float slow = Mathf.Sin((seconds * slowHz + phase) * 2f * Mathf.PI);
            float fast = Mathf.Sin((seconds * fastHz + phase * 2f) * 2f * Mathf.PI);
            return slow * (1f - FastWeight) + fast * FastWeight;
        }
    }
}
```

- [ ] **Step 4: 통과를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
```

기대: 컴파일 초록, Failed 0, `ArcheryShakeTests`의 테스트 일곱이 이름으로 보인다.

- [ ] **Step 5: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/ArcheryShake.cs Runtime/Scripts/Game/ArcheryShake.cs.meta Tests/EditMode/ArcheryShakeTests.cs Tests/EditMode/ArcheryShakeTests.cs.meta
git status --short
git commit -F - <<'EOF'
feat(archery): 오래 당기고 있으면 조준이 흔들리게 하는 수식

미리 당겨놓고 무한정 기다리지 못하게 하는 대가다. 난수가 아니라 주기가
서로 안 맞는 사인파 둘로 만든다 — 틱마다 난수를 뽑으면 지직거려서
손떨림이 아니라 화면 고장으로 보인다. 덤으로 난수를 안 써서 클·서
난수 소비 계약을 안 건드린다.

위상은 엔티티 id를 FNV-1a로 해싱해 사람마다 다르게 준다. 문자열의
기본 해시를 안 쓰는 이유는 런타임마다 값이 달라질 수 있어서다 —
클·서가 다른 위상을 보면 보이는 것과 화살 가는 곳이 갈린다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

---

## Task 6: 데이터에 함정과 흔들림을 넣는다 (infrastructure + MasterData 둘)

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/infrastructure/table/Datas/#ArcheryTarget.xlsx`
- Modify: `C:/Users/re5na/workspace/LOP/infrastructure/table/Datas/#ArcheryConfig.xlsx`
- Generated: `LeagueOfPhysical-MasterData-Client/Runtime.Generated/**`, `LeagueOfPhysical-MasterData-Server/Runtime.Generated/**`
- Modify: `LeagueOfPhysical-MasterData-Server/Tests/EditMode/ArcheryTargetSeparationTests.cs`

**Interfaces:**
- Consumes: (없음 — 데이터 작업)
- Produces: Luban 생성 타입에 `ArcheryTargetKind.IsTrap`(bool), `ArcheryConfig.TrapRatioMin`/`TrapRatioMax`/`ShakeFreeSeconds`/`ShakeRampSeconds`/`ShakeMaxDegrees`(float)

> **컬럼은 반드시 맨 뒤에 붙인다.** Luban은 컬럼 이름이 아니라 몇 번째 열인지로 읽는다 — 중간에 끼우면 기존 값이 조용히 뒤바뀐다.

- [ ] **Step 1: 엑셀에 컬럼과 줄을 더한다**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
python - <<'PY'
import openpyxl

#  #ArcheryTarget: is_trap 컬럼을 맨 뒤에 붙이고 함정 종류 두 줄을 더한다.
wb = openpyxl.load_workbook('Datas/#ArcheryTarget.xlsx')
ws = wb.worksheets[0]
col = ws.max_column + 1
ws.cell(row=1, column=col, value='is_trap')   # ##var
ws.cell(row=2, column=col, value='bool')      # ##type
                                              # ##group 은 비워 둔다(클·서 공통)
ws.cell(row=4, column=col, value='is_trap')   # ## (헤더 표시)
for r in range(5, ws.max_row + 1):
    if ws.cell(row=r, column=2).value is not None:
        ws.cell(row=r, column=col, value=False)

#  함정 두 종류. 성한 과녁과 크기가 겹치게 둬서 "크기로 구분"이 안 되게 한다 —
#  색으로만 갈라야 순간 판단이 필요해진다.
nxt = ws.max_row + 1
ws.cell(row=nxt,     column=2, value=4); ws.cell(row=nxt,     column=3, value='TrapBig')
ws.cell(row=nxt,     column=4, value=0.60); ws.cell(row=nxt,   column=5, value=-3)
ws.cell(row=nxt,     column=6, value=55); ws.cell(row=nxt,     column=col, value=True)
ws.cell(row=nxt + 1, column=2, value=5); ws.cell(row=nxt + 1, column=3, value='TrapSmall')
ws.cell(row=nxt + 1, column=4, value=0.40); ws.cell(row=nxt + 1, column=5, value=-5)
ws.cell(row=nxt + 1, column=6, value=45); ws.cell(row=nxt + 1, column=col, value=True)
wb.save('Datas/#ArcheryTarget.xlsx')

#  #ArcheryConfig: 다섯 칸을 맨 뒤에 붙인다.
wb = openpyxl.load_workbook('Datas/#ArcheryConfig.xlsx')
ws = wb.worksheets[0]
added = [
    ('trap_ratio_min',      'float', 0.0),
    ('trap_ratio_max',      'float', 1.0),
    ('shake_free_seconds',  'float', 1.2),
    ('shake_ramp_seconds',  'float', 2.5),
    ('shake_max_degrees',   'float', 2.5),
]
start = ws.max_column + 1
for i, (name, typ, value) in enumerate(added):
    c = start + i
    ws.cell(row=1, column=c, value=name)
    ws.cell(row=2, column=c, value=typ)
    ws.cell(row=4, column=c, value=name)
    ws.cell(row=5, column=c, value=value)
wb.save('Datas/#ArcheryConfig.xlsx')
print('done')
PY
```

- [ ] **Step 2: 엑셀이 의도대로 됐는지 눈으로 확인한다**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
python -c "
import openpyxl
for f in ['#ArcheryConfig.xlsx','#ArcheryTarget.xlsx']:
    ws = openpyxl.load_workbook('Datas/'+f, data_only=True).worksheets[0]
    print('===', f)
    for row in ws.iter_rows(values_only=True):
        if any(c is not None for c in row): print(row)
"
```

기대:
- `#ArcheryConfig`: `##var` 줄 끝에 `trap_ratio_min, trap_ratio_max, shake_free_seconds, shake_ramp_seconds, shake_max_degrees`, `##type`에 `float` 다섯, 데이터 줄 끝에 `0.0, 1.0, 1.2, 2.5, 2.5`
- `#ArcheryTarget`: `is_trap` 컬럼이 맨 뒤, 기존 세 줄은 `False`, 새 두 줄(`TrapBig` 반경 0.60 점수 −3, `TrapSmall` 반경 0.40 점수 −5)이 `True`

> **기존 컬럼의 값이 하나라도 달라졌으면 멈추고 되돌린다.** 컬럼을 잘못 끼운 것이다.

- [ ] **Step 3: 굽고 네 출력처를 확인한다**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
./gen.sh
```

그다음 네 곳의 변경을 본다:

```bash
git -C /c/Users/re5na/workspace/LOP/infrastructure status --short
git -C /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client status --short
git -C /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server status --short
git -C /c/Users/re5na/workspace/LOP/lop-backend status --short
```

기대: 앞의 셋에 변경이 있고, `lop-backend`는 **변경 없음**(매칭 타깃은 gamemode/map/queue만 굽는다). 만약 lop-backend에 변경이 생겼다면 그것도 함께 커밋해야 한다.

생성된 타입에 새 필드가 들어갔는지 확인한다:

```bash
grep -n "IsTrap\|TrapRatioMin\|ShakeMaxDegrees" /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/ArcheryTargetKind.cs /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/ArcheryConfig.cs
```

기대: 세 이름이 모두 보인다. **`TableFiles` 목록은 손대지 않는다** — 새 *테이블*이 아니라 기존 테이블의 컬럼이라서다.

- [ ] **Step 4: 배포 데이터 검사에 "함정 종류가 있나"를 더한다**

`LeagueOfPhysical-MasterData-Server/Tests/EditMode/ArcheryTargetSeparationTests.cs`에 테스트를 하나 더한다(기존 테스트 뒤, 클래스 닫기 전):

```csharp
        //  비율만 올려 두고 함정 종류를 안 넣으면 함정이 영영 안 뜬다 — 에러 없이 게임만 밋밋해진다.
        [Test]
        public void 함정_비율이_0보다_크면_함정_종류가_적어도_하나는_있다()
        {
            var tables = LoadTables();

            var config = tables.TbArcheryConfig.GetOrDefault(1);
            Assert.IsNotNull(config, "TbArcheryConfig id=1 행이 없다");

            if (config.TrapRatioMax <= 0f)
            {
                Assert.Pass("함정 비율이 0이다 — 함정을 안 쓰기로 한 데이터");
            }

            int trapKinds = 0;
            foreach (var row in tables.TbArcheryTarget.DataList)
            {
                trapKinds += row.IsTrap ? 1 : 0;
            }

            Assert.Greater(trapKinds, 0,
                $"trap_ratio_max({config.TrapRatioMax})가 0보다 큰데 #ArcheryTarget에 is_trap=TRUE인 줄이 없다 "
                + "— 함정이 영영 안 뜬다");
        }
```

- [ ] **Step 5: 컴파일·테스트를 돌린다**

이 시점에 클·서 provider는 아직 새 컬럼을 안 읽으므로(Task 2에서 임시값을 넣어 뒀다) 컴파일은 초록이어야 한다.

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
```

기대: 컴파일 초록, Failed 0, `함정_비율이_0보다_크면_함정_종류가_적어도_하나는_있다`가 이름으로 보이고 통과한다.

- [ ] **Step 6: 커밋 (레포 셋)**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure
git checkout -b feature/archery-slice3
git add table/Datas/#ArcheryTarget.xlsx table/Datas/#ArcheryConfig.xlsx
git status --short
git commit -F - <<'EOF'
feat(archery): 데이터에 함정 과녁과 흔들림 설정을 넣는다

함정 두 종류를 성한 과녁과 크기가 겹치게 뒀다 — 크기로 구분되면 색을
볼 필요가 없어져 순간 판단이 사라진다.

컬럼은 전부 맨 뒤에 붙였다. Luban은 컬럼 이름이 아니라 몇 번째 열인지로
읽으므로 중간에 끼우면 기존 값이 조용히 뒤바뀐다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

MasterData-Client:

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client
git checkout -b feature/archery-slice3
git add Runtime.Generated
git status --short
git commit -F - <<'EOF'
chore(masterdata): 함정 과녁과 흔들림 설정을 굽는다

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

MasterData-Server:

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server
git checkout -b feature/archery-slice3
git add Runtime.Generated Tests/EditMode/ArcheryTargetSeparationTests.cs
git status --short
git commit -F - <<'EOF'
chore(masterdata): 함정 과녁과 흔들림 설정을 굽고, 함정 종류 유무를 지킨다

비율만 올려 두고 함정 종류를 안 넣으면 에러 없이 함정이 영영 안 뜬다.
배포된 바이트를 읽어 그 짝을 확인한다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

> 새 `.meta`가 생겼으면(생성물 폴더) 함께 스테이지한다. `git status --short`에 `??`로 남은 것이 없어야 한다.

---

## Task 7: 서버가 함정을 읽고 점수를 깎는다

**Files:**
- Modify: `Assets/Scripts/Game/ArcheryConfigProvider.cs`
- Modify: `Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs`
- Test: `Assets/Tests/Editor/ArcheryHitSystemTests.cs`

**Interfaces:**
- Consumes: `ArcheryHitRules.Resolve(in ArcheryTarget)` → `ArcheryHitOutcome{Gained,Lost,Delta}` (Task 4), `ArcheryConfig` 새 인자 (Task 2), Luban 새 필드 (Task 6)
- Produces: `ArcheryTargetHitEvent.points`가 **부호 있는 delta**가 된다(함정이면 음수)

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/Editor/ArcheryHitSystemTests.cs`를 고친다.

이 파일에는 이미 판을 세우는 헬퍼가 있다 — **새로 만들지 말고 그것을 쓴다**:

| 기존 헬퍼 | 하는 일 |
|---|---|
| `static Fixture Build(long startTick)` | 레지스트리·월드·시스템을 엮은 판 하나. 안에서 모듈 수준 `Config()`를 쓴다 |
| `f.Archer(string id)` | 사수 엔티티를 만들어 등록 |
| `f.TargetsOfWave(int wave)` | 그 웨이브의 과녁 목록(생성 커널을 직접 부른다) |
| `f.ScoreOf(string id)` | `ArcheryScore.Value` |
| `f.HitEventCount()` | 이벤트 버퍼의 `ArcheryTargetHitEvent` 개수 |
| `static ArcheryShot ShotThrough(string shooterId, long fireTick, ArcheryTarget target, float distance)` | 과녁 한가운데를 정확히 지나가는 화살 한 발 |

쏘는 방식도 기존과 같다: `f.World.IngestRemoteShot(ShotThrough(...))` 뒤에 `f.System.Tick(StartTick + 1, TickInterval)`.

**(a) `Build`가 설정을 받을 수 있게 오버로드를 더한다.** 지금은 모듈 `Config()`(전부 성한 과녁)만 쓰므로 함정 판을 세울 수가 없다. 기존 `Build(long startTick)` 바로 아래에 넣고, 기존 것은 새 것에 위임하게 한다:

```csharp
        static Fixture Build(long startTick) => Build(startTick, Config());

        static Fixture Build(long startTick, ArcheryConfig config)
        {
            var registry = new EntityRegistry();
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
```

> 기존 `Build(long startTick)`의 본문(`var config = Config();` 로 시작하는 것)은 **지운다** — 위임하는 한 줄로 대체된다.

**(b) 적중 사건의 값을 읽는 헬퍼를 `Fixture`에 더한다.** 기존 `HitEventCount()` 바로 아래:

```csharp
            public int LastHitPoints()
            {
                int points = 0;
                foreach (var e in World.EventBuffer.Snapshot)
                {
                    if (e is ArcheryTargetHitEvent hit) { points = hit.points; }
                }
                return points;
            }
```

**(c) 함정만 뜨는 판을 세우는 헬퍼.** `ShotThrough` 바로 아래에 넣는다:

```csharp
        //  함정만 든 판. 비율을 1로 두고 함정 종류를 하나만 넣으면 뜨는 과녁이 전부 그것이다.
        static ArcheryConfig TrapOnlyConfig()
        {
            var kinds = new[] { new ArcheryTargetKind(0.50f, -5, 100, true) };
            return new ArcheryConfig(
                wavePeriodTicks: 88, minTargets: 2, maxTargets: 3,
                spawnRadius: 2f, spawnMinY: 2f, spawnMaxY: 6f, minSeparation: 1.0f,
                trapRatioMin: 1f, trapRatioMax: 1f,
                shakeFreeSeconds: 1f, shakeRampSeconds: 2f, shakeMaxDegrees: 3f,
                kinds: kinds);
        }
```

**(d) 테스트 셋을 더한다.** 파일 끝의 마지막 `[Test]` 뒤에 넣는다:

```csharp
        [Test]
        public void 함정을_맞히면_점수가_깎인다()
        {
            var f = Build(StartTick, TrapOnlyConfig());
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];
            Assert.IsTrue(target.IsTrap, "함정만 든 설정인데 성한 과녁이 떴다");

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(-5, f.ScoreOf("a"));
            Assert.AreEqual(0, f.Registry.Get("a").Get<ArcheryScore>().Gained);
            Assert.AreEqual(5, f.Registry.Get("a").Get<ArcheryScore>().Lost);
        }

        //  연출용 값도 부호가 맞아야 화면에 "-5"로 뜬다.
        [Test]
        public void 함정_적중_사건은_음수를_싣는다()
        {
            var f = Build(StartTick, TrapOnlyConfig());
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(1, f.HitEventCount());
            Assert.AreEqual(-5, f.LastHitPoints());
        }

        //  성한 과녁은 예전 그대로여야 한다 — 규칙 함수를 끼우면서 획득이 벌점 칸으로 새면
        //  합계는 맞고 결과 화면의 내역만 틀린다(눈에 안 띈다).
        [Test]
        public void 성한_과녁은_획득_칸에만_쌓인다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(target.Points, f.Registry.Get("a").Get<ArcheryScore>().Gained);
            Assert.AreEqual(0, f.Registry.Get("a").Get<ArcheryScore>().Lost);
        }
```

> `Fixture.Registry`가 `public`인지 확인한다 — 기존 `Build`가 객체 초기자로 채우고 있으므로 접근 가능해야 한다. 아니라면 `f.ScoreOf`처럼 `Fixture`에 `GainedOf`/`LostOf` 헬퍼를 더해 쓴다.

- [ ] **Step 2: 실패를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
```

기대: 새 테스트 둘이 실패한다(점수가 `-5`가 아니라 `-5`만큼 *더해져* `-5`가 아닌 값이 나오거나, `Lost`가 0이다).

- [ ] **Step 3: provider가 새 컬럼을 읽게 한다**

`Assets/Scripts/Game/ArcheryConfigProvider.cs`에서 Task 2의 임시값을 실제 컬럼으로 바꾼다:

```csharp
                kinds.Add(new ArcheryTargetKind(row.Radius, row.Points, row.Weight, row.IsTrap));
```

```csharp
            return new ArcheryConfig(
                r.WavePeriodTicks, r.MinTargets, r.MaxTargets,
                r.SpawnRadius, r.SpawnMinY, r.SpawnMaxY, r.MinSeparation,
                r.TrapRatioMin, r.TrapRatioMax,
                r.ShakeFreeSeconds, r.ShakeRampSeconds, r.ShakeMaxDegrees,
                kinds);
```

- [ ] **Step 4: 적중 처리를 규칙 함수에 위임한다**

`Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs`의 `ApplyCandidates` 안에서 점수를 주는 부분을 바꾼다. 기존:

```csharp
                int points = PointsOfSlot(candidate.Slot);
                spentArrows.Add((candidate.ShooterId, candidate.FireTick));

                var score = entityRegistry.Get(candidate.ShooterId)?.Get<ArcheryScore>();
                if (score != null)
                {
                    score.Gained += points;
                }

                eventBuffer.Append(new ArcheryTargetHitEvent(
                    candidate.ShooterId, candidate.FireTick, points));
```

바뀐 뒤:

```csharp
                //  무슨 일이 일어나는지는 여기서 정하지 않는다 — 공유 규칙 함수 하나가 정한다.
                var outcome = ArcheryHitRules.Resolve(TargetOfSlot(candidate.Slot));
                spentArrows.Add((candidate.ShooterId, candidate.FireTick));

                var score = entityRegistry.Get(candidate.ShooterId)?.Get<ArcheryScore>();
                if (score != null)
                {
                    score.Gained += outcome.Gained;
                    score.Lost += outcome.Lost;
                }

                eventBuffer.Append(new ArcheryTargetHitEvent(
                    candidate.ShooterId, candidate.FireTick, outcome.Delta));
```

그리고 `PointsOfSlot`을 과녁 자체를 돌려주는 것으로 바꾼다(규칙 함수가 과녁을 받는다):

```csharp
        private ArcheryTarget TargetOfSlot(int slot)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].SlotIndex == slot)
                {
                    return targets[i];
                }
            }
            return default;
        }
```

> `PointsOfSlot`을 부르는 다른 곳이 없는지 확인하고 지운다: `grep -n "PointsOfSlot" Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs`

- [ ] **Step 5: 통과를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
```

기대: 컴파일 초록, Failed 0. 새 테스트 둘이 이름으로 보인다. **기존 적중 테스트 열 개가 전부 그대로 통과해야 한다.**

- [ ] **Step 6: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/ArcheryConfigProvider.cs Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs Assets/Tests/Editor/ArcheryHitSystemTests.cs
git status --short
git commit -F - <<'EOF'
feat(archery): 함정을 맞히면 점수를 깎는다

점수를 주는 결정을 시스템에서 빼내 공유 규칙 함수에 맡긴다 — 나중에
벌칙 종류를 바꿀 때 고칠 곳이 한 군데가 된다.

적중 사건에 싣는 값은 부호 있는 변화량이다. 함정이면 음수가 실려
연출이 "-5"로 뜬다. 점수의 진실원본은 여전히 스냅샷이다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

---

## Task 8: 클라가 함정을 다르게 그린다

**Files:**
- Modify: `Assets/Scripts/Game/ArcheryConfigProvider.cs`
- Modify: `Assets/Scripts/Game/ArcheryTargetView.cs`

**Interfaces:**
- Consumes: `ArcheryTarget.IsTrap` (Task 1), `ArcheryConfig` 새 인자 (Task 2), Luban 새 필드 (Task 6)
- Produces: (없음 — 화면만)

> **이 태스크가 없으면 슬라이스 전체가 무의미하다.** 함정을 **볼 수 없으면** 참을 수가 없고, §12의 판정("참는 것이 고민되나")에 답이 안 나온다. 함정과 성한 과녁은 **크기가 겹치도록** 데이터를 넣었으므로(Task 6) 색이 유일한 단서다.

- [ ] **Step 1: provider를 서버와 똑같이 맞춘다**

`Assets/Scripts/Game/ArcheryConfigProvider.cs`를 Task 7 Step 3과 **한 글자도 다르지 않게** 고친다:

```csharp
                kinds.Add(new ArcheryTargetKind(row.Radius, row.Points, row.Weight, row.IsTrap));
```

```csharp
            return new ArcheryConfig(
                r.WavePeriodTicks, r.MinTargets, r.MaxTargets,
                r.SpawnRadius, r.SpawnMinY, r.SpawnMaxY, r.MinSeparation,
                r.TrapRatioMin, r.TrapRatioMax,
                r.ShakeFreeSeconds, r.ShakeRampSeconds, r.ShakeMaxDegrees,
                kinds);
```

- [ ] **Step 2: 두 provider를 나란히 놓고 대조한다**

```bash
diff <(sed -n '/return new ArcheryConfig/,/kinds);/p' /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryConfigProvider.cs) \
     <(sed -n '/return new ArcheryConfig/,/kinds);/p' /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryConfigProvider.cs)
```

기대: **출력 없음**(완전히 같다). 한 줄이라도 다르면 클·서가 다른 과녁을 본다.

- [ ] **Step 3: 함정을 다른 색으로 그린다**

`Assets/Scripts/Game/ArcheryTargetView.cs`를 고친다.

재질 필드를 둘로 나눈다:

```csharp
        private Material _targetMaterial;
        private Material _trapMaterial;
```

과녁을 만들 때와 **매 프레임** 재질을 정한다(과녁 하나가 재사용될 때 종류가 바뀔 수 있으므로 만들 때만 정하면 안 된다 — 키는 `(wave, slot)`이고 웨이브가 넘어가면 같은 키가 다시 안 오지만, 한 번 더 확실히 해 둔다):

```csharp
                if (drawn.TryGetValue(key, out var sphere) == false || sphere == null)
                {
                    sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.Destroy(sphere.GetComponent<Collider>());   // 그림일 뿐이다 — 판정은 서버가 한다
                    drawn[key] = sphere;
                }

                var renderer = sphere.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = targets[i].IsTrap ? TrapMaterial() : TargetMaterial();
                }
```

재질 만드는 함수를 더한다(기존 `TargetMaterial` 옆):

```csharp
        //  함정은 성한 과녁과 크기가 겹치게 뒀다 — 색이 유일한 단서다. 노랑(성한 것)과
        //  가장 멀고, 붉은 화살과도 갈리는 쪽으로 고른다.
        private Material TrapMaterial()
        {
            if (_trapMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _trapMaterial = new Material(shader) { color = new Color(0.15f, 0.2f, 0.9f) };
            }
            return _trapMaterial;
        }
```

`Dispose`에서 새 재질도 지운다:

```csharp
            if (_trapMaterial != null)
            {
                Object.Destroy(_trapMaterial);
                _trapMaterial = null;
            }
```

클래스 XML 주석에 한 줄 더한다:

```csharp
    /// <para><b>함정은 색으로만 갈린다</b> — 크기가 성한 과녁과 겹치게 데이터를 넣었기 때문에,
    /// 색을 못 보면 구분할 방법이 없다.</para>
```

- [ ] **Step 4: 컴파일을 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile_status
```

기대: `failed: false`, `errors: []`.

> **클라 에디터의 테스트 러너는 2026-09-13 현재 멈춰 있다**(`run_tests`가 30초 타임아웃). 클라는 컴파일 확인까지만 하고, 공유 코드의 테스트는 서버 에디터로 돌린다. 러너가 살아 있으면 클라 EditMode도 돌린다.

- [ ] **Step 5: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/ArcheryConfigProvider.cs Assets/Scripts/Game/ArcheryTargetView.cs
git status --short
git commit -F - <<'EOF'
feat(archery): 함정 과녁을 다른 색으로 그린다

함정과 성한 과녁은 크기가 겹치게 데이터를 넣었다 — 색이 유일한 단서다.
크기로 구분되면 색을 볼 필요가 없어져 순간 판단이 사라진다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

---

## Task 9: 오래 당기면 화면이 흔들린다 (클라)

**Files:**
- Modify: `Assets/Scripts/Game/CameraController.cs`
- Modify: `Assets/Scripts/Game/ArcheryAimView.cs`

**Interfaces:**
- Consumes: `ArcheryShake.Offset(float heldSeconds, int phaseSeed, ArcheryConfig config)` · `ArcheryShake.PhaseSeedOf(string entityId)` (Task 5), `ArcheryConfig.Shake*` (Task 2)
- Produces: `CameraController.AimSwayDegrees` (`Vector2`, 기본 `Vector2.zero`) — x는 yaw에, y는 유니티 pitch에 더해진다

> **왜 `CameraController`에 손을 대나:** `CameraController.LateUpdate`(실행 순서 3000)가 매 프레임 `mainCamera.transform.rotation`을 **통째로 덮어쓴다.** `ArcheryAimView`가 transform을 직접 돌리면 둘의 실행 순서에 결과가 좌우되고, 순서가 어긋나면 **떨림이 조용히 사라진다**(에러 없이 기능만 죽는다). 카메라 주인이 자기 회전을 만들 때 더하게 하면 순서와 무관해진다.
>
> 다른 모드는 이 값을 안 건드리므로 `Vector2.zero`가 유지되고 동작이 바뀌지 않는다.

- [ ] **Step 1: `CameraController`가 더할 각도를 받는다**

`Assets/Scripts/Game/CameraController.cs`에 속성을 더한다(`public Camera MainCamera => mainCamera;` 옆):

```csharp
        /// <summary>
        /// 회전에 덧붙이는 각도(도). x는 좌우, y는 위아래이며 <b>유니티 부호</b>다(양수가 아래).
        /// 활쏘기의 손떨림이 이걸 쓴다 — 카메라 주인이 직접 더해야 <see cref="LateUpdate"/>가
        /// 회전을 덮어쓰는 것과 실행 순서로 다투지 않는다. 기본 0이라 안 쓰는 모드는 영향이 없다.
        /// </summary>
        public Vector2 AimSwayDegrees { get; set; }
```

`LateUpdate`의 회전 만드는 줄을 고친다:

```csharp
            // Apply transform
            Quaternion rotation = Quaternion.Euler(pitch + AimSwayDegrees.y, yaw + AimSwayDegrees.x, 0);
```

> `pitch`/`yaw` 자체는 건드리지 않는다 — 흔들림이 조준 상태에 **누적되면** 손을 떼도 화면이 제자리로 안 돌아온다.

- [ ] **Step 2: `ArcheryAimView`가 흔들림을 채운다**

`Assets/Scripts/Game/ArcheryAimView.cs`를 고친다.

생성자에 `ArcheryConfig`를 더한다(필드·인자·대입 셋 다):

```csharp
        private readonly ArcheryConfig config;
```

```csharp
        public ArcheryAimView(GameFramework.Runner.IRunner runner, PlayerInputManager input,
                              CameraController cameraController, IPlayerContext playerContext,
                              GameFramework.World.EntityRegistry entityRegistry,
                              ArcheryConfig config)
        {
            this.runner = runner;
            this.input = input;
            this.cameraController = cameraController;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.config = config;
        }
```

`LateTick`에서 조준을 넘기기 **전에** 흔들림을 채운다:

```csharp
        public void LateTick()
        {
            var camera = cameraController.MainCamera;
            if (camera == null)
            {
                return;
            }

            //  흔들림을 먼저 정한다. 카메라가 이 값을 얹어 돌고, 아래에서 읽는 방향이 그 결과라
            //  보이는 곳과 화살이 가는 곳이 저절로 같아진다.
            cameraController.AimSwayDegrees = SwayDegrees();

            // 유니티의 x 회전은 양수가 아래를 본다. 조준 각도는 양수가 위이므로 부호를 뒤집는다.
            float yaw = camera.transform.eulerAngles.y;
            float pitch = -Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x);
            input.SetAim(yaw, pitch);

            camera.fieldOfView = Mathf.Lerp(
                camera.fieldOfView,
                Mathf.Lerp(WideFov, DrawnFov, MyDrawRatio()),
                Time.deltaTime * FovLerpPerSecond);
        }

        private Vector2 SwayDegrees()
        {
            if (playerContext.entityId == null)
            {
                return Vector2.zero;
            }
            var aim = entityRegistry.Get(playerContext.entityId)?.Get<ArcheryAim>();
            if (aim == null || aim.Drawing == false)
            {
                return Vector2.zero;   // 안 당기고 있으면 안 흔들린다
            }
            if (runner?.tickUpdater == null)
            {
                return Vector2.zero;
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return Vector2.zero;
            }

            //  화면 시각은 정수 틱이 아니라 renderTick이다 — 시뮬과 같은 식에 같은 시각을 넣는다.
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            float held = (float)((renderTick - aim.DrawStartTick) * interval);

            var offset = ArcheryShake.Offset(held, ArcheryShake.PhaseSeedOf(playerContext.entityId), config);
            //  조준 좌표계는 위가 양수, 유니티 x 회전은 아래가 양수다 — 위아래를 뒤집어 넘긴다.
            return new Vector2(offset.x, -offset.y);
        }
```

> **주의:** `MyDrawRatio()`는 정수 틱(`Math.Floor(renderTick)`)을 쓰지만 흔들림은 **소수 틱**을 쓴다. 줌인은 0~1 비율이라 한 틱 단위로 끊겨도 눈에 안 띄지만, 흔들림은 매 프레임 매끄러워야 해서 정수 틱으로 물으면 20ms마다 계단이 생긴다.

- [ ] **Step 3: `ArcheryConfig`가 클라 게임 스코프에 등록돼 있는지 확인한다**

```bash
grep -n "ArcheryConfig" Assets/Scripts/Game/ArcheryLifetimeScope.cs
```

기대(계획 단계에서 확인함 — `ArcheryLifetimeScope.cs:19-20`):

```csharp
            builder.Register<ArcheryConfigProvider>(Lifetime.Singleton);
            builder.Register<ArcheryConfig>(c => c.Resolve<ArcheryConfigProvider>().Get(), Lifetime.Singleton);
```

**이 줄이 사라졌으면 멈춘다.** `ArcheryAimView`는 `RegisterEntryPoint`된 `ILateTickable`이라 해소에 실패하면 `EntryPointDispatcher`가 try/catch **밖**에서 터져 **게임 스코프가 통째로 안 선다**(과녁·화살·조작 패드까지 전부). 컴파일은 통과하므로 판에 들어가기 전에는 안 보인다 — 등록은 공용 파일에, 소비는 새 코드에 있어 어느 diff에도 함께 안 보이는 자리다.

- [ ] **Step 4: 컴파일을 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile_status
```

기대: `failed: false`, `errors: []`.

- [ ] **Step 5: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/CameraController.cs Assets/Scripts/Game/ArcheryAimView.cs
git status --short
git commit -F - <<'EOF'
feat(archery): 오래 당기고 있으면 화면이 흔들린다

조준이 곧 카메라 방향이라, 카메라를 흔들면 보이는 곳과 화살이 가는 곳이
저절로 같아진다. 조준점을 따로 띄우지 않는 이유는 그러면 "정확히 여기로
간다"가 생겨 게임이 오히려 쉬워지기 때문이다.

카메라 주인이 직접 더하게 한다 — 뷰가 transform을 돌리면 CameraController의
LateUpdate가 회전을 통째로 덮어쓰는 것과 실행 순서로 다투게 되고, 순서가
어긋나면 떨림이 에러 없이 사라진다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

---

## Task 10: 점수를 조인다 (데이터 튜닝)

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/infrastructure/table/Datas/#ArcheryConfig.xlsx`
- Modify: `C:/Users/re5na/workspace/LOP/infrastructure/table/Datas/#ArcheryTarget.xlsx`
- Generated: MasterData 둘

**Interfaces:**
- Consumes: Task 6의 컬럼들
- Produces: (없음 — 데이터)

> **왜 이 태스크가 이 슬라이스에 있나.** 슬라이스 2 실측에서 *"과녁을 안 맞혀도 점수가 오르는 느낌"* 이 나왔다. 12m에서 보면 과녁이 뜨는 공간이 대략 4m×4m 창인데 거기에 지름 1.2m짜리가 2~3개 들어차, **창 안 아무 데나 쏴도 대략 7발에 한 번 걸린다.** 난사에 대가가 없는 상태로 함정을 얹으면 §12의 판정("참는 것이 고민되나")에 **답이 안 나온다** — 참지 않아도 점수가 오르기 때문이다.

- [ ] **Step 1: 공간을 넓히고 과녁을 줄인다**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
python - <<'PY'
import openpyxl

def set_by_name(ws, row, name, value):
    header = [c.value for c in ws[1]]
    ws.cell(row=row, column=header.index(name) + 1, value=value)

wb = openpyxl.load_workbook('Datas/#ArcheryConfig.xlsx')
ws = wb.worksheets[0]
#  창을 넓힌다 — 같은 수의 과녁이 더 넓은 곳에 흩어져 아무 데나 쏘면 덜 걸린다.
set_by_name(ws, 5, 'spawn_radius', 3.5)
set_by_name(ws, 5, 'spawn_min_y',  1.5)
set_by_name(ws, 5, 'spawn_max_y',  8.0)
wb.save('Datas/#ArcheryConfig.xlsx')

wb = openpyxl.load_workbook('Datas/#ArcheryTarget.xlsx')
ws = wb.worksheets[0]
header = [c.value for c in ws[1]]
radius = header.index('radius') + 1
weight = header.index('weight') + 1
#  큰 과녁을 줄이고(0.60 -> 0.45) 비중도 낮춘다. 함정은 성한 것과 크기가 겹쳐 있어야 하므로
#  같은 폭으로 줄인다.
sizes  = {1: 0.45, 2: 0.30, 3: 0.20, 4: 0.45, 5: 0.30}
weights = {1: 30, 2: 40, 3: 30, 4: 55, 5: 45}
for r in range(5, ws.max_row + 1):
    key = ws.cell(row=r, column=2).value
    if key in sizes:
        ws.cell(row=r, column=radius, value=sizes[key])
        ws.cell(row=r, column=weight, value=weights[key])
wb.save('Datas/#ArcheryTarget.xlsx')
print('done')
PY
```

- [ ] **Step 2: 간격 조건이 여전히 성립하는지 확인한다**

과녁이 작아졌으므로 `min_separation`은 여유가 생긴다(최대 반경 0.45 × 2 = 0.9 ≤ 1.2). 그래도 **검사로 확인한다** — 손으로 계산한 값을 믿지 않는다.

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table && ./gen.sh
```

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
```

기대: Failed 0. 특히 `간격_기준이_가장_큰_과녁_둘을_떼어놓을_만큼은_된다`와 `함정_비율이_0보다_크면_함정_종류가_적어도_하나는_있다`가 통과한다.

- [ ] **Step 3: 커밋 (레포 셋)**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure
git add table/Datas/#ArcheryConfig.xlsx table/Datas/#ArcheryTarget.xlsx
git status --short
git commit -F - <<'EOF'
tune(archery): 아무 데나 쏴도 걸리던 것을 조인다

슬라이스 2 실측에서 "과녁을 안 맞혀도 점수가 오르는 느낌"이 나왔다.
12m에서 보면 과녁이 뜨는 공간이 대략 4m x 4m 창인데 거기에 지름 1.2m
짜리가 두세 개 들어차, 창 안 아무 데나 쏴도 7발에 한 번쯤 걸렸다.

공간을 넓히고(반지름 2 -> 3.5, 높이 2~6 -> 1.5~8) 과녁을 줄이고
(0.60/0.40/0.25 -> 0.45/0.30/0.20) 큰 것의 비중을 낮춘다.

난사에 대가가 없는 채로 함정을 얹으면 "참는 것이 고민되나"에 답이
안 나온다 - 참지 않아도 점수가 오르기 때문이다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

MasterData 둘도 각각 `git add Runtime.Generated` 후 커밋한다:

```
chore(masterdata): 조인 과녁 튜닝값을 굽는다
```

---

## Task 11: 여섯 레포 머지 · 배포 · 실측 준비

**Files:**
- Modify: `docs/ROADMAP.md` (클라)

**Interfaces:**
- Consumes: Task 1~10 전부

- [ ] **Step 1: 마지막으로 전부 초록인지 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile_status
```

기대: 서버 Failed 0, 양쪽 컴파일 `errors: []`.

- [ ] **Step 2: ROADMAP을 갱신한다**

`docs/ROADMAP.md`의 Archery 절 **위에** 슬라이스 3 절을 새로 쓴다. 담을 것:

- 무엇이 생겼나 — 함정 과녁, 점수 차감, 손떨림, 조인 튜닝값
- **결정 넷**(이 계획서의 "착수 전에 박아둘 결정 넷")을 요약해 남긴다. 특히 **난수 소비 순서가 바뀌어 클·서가 함께 배포돼야 한다**는 사실
- 줌인은 **이미 있었다**는 사실(슬라이스 1/2에서 들어감) — 다음에 spec §2.2를 읽는 사람이 또 찾아 헤매지 않도록
- 실측 항목(아래 Step 5)을 체크박스로 남긴다

- [ ] **Step 3: 여섯 레포를 규약대로 머지한다 (한 줄씩)**

레포: `LeagueOfPhysical-Shared` · `LeagueOfPhysical-MasterData-Client` · `LeagueOfPhysical-MasterData-Server` · `LeagueOfPhysical-Server` · `LeagueOfPhysical-Client` · `infrastructure`

레포마다 **한 줄씩 결과를 확인하며**:

```bash
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/archery-slice3
git push origin main
```

**`&&`로 잇지 말 것.** Unity 레포는 `--autostash`가 로컬 픽스처를 빼뒀다 되돌린다 — `git status --short`로 픽스처가 그대로 돌아왔는지 확인한다.

- [ ] **Step 4: 배포한다**

**클·서가 반드시 함께 나가야 한다** — 난수 소비 순서가 바뀌어 한쪽만 배포되면 서로 다른 과녁을 본다.

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
gh workflow run gameserver-deploy -f environment=local -f package_ref=main
```

마스터데이터만 바뀐 게 아니라 **서버 코드도 바뀌었으므로** 이미지 태그가 움직인다. 배포가 끝나면 실물로 대조한다:

```bash
kubectl get deploy room-server -o jsonpath='{.spec.template.spec.containers[0].image}'
git -C /c/Users/re5na/workspace/LOP/infrastructure fetch origin && git -C /c/Users/re5na/workspace/LOP/infrastructure log --oneline -3 origin/main
```

기대: `GAME_SERVER_IMAGE`가 서버 main의 새 SHA로 bump된 커밋이 보인다.

에셋(맵·과녁)은 이 슬라이스에서 안 바뀌었으므로 **`content-deploy`는 돌리지 않는다.**

- [ ] **Step 5: 두 클라로 실측한다 (⭐ 이 슬라이스의 존재 이유)**

spec §12가 지정한 확인 지점이다. 판정 질문은 하나:

> **참는 것이 실제로 고민되나?** 전부 함정인 웨이브에서 손이 멈추는가, 아니면 그냥 쏘고 마는가.

확인할 것:

- [ ] **양쪽이 같은 과녁을 본다** — 함정 자리까지 같은가 (결정론이 깨지면 여기서 드러난다)
- [ ] 함정이 **색으로 구분된다** — 순간에 알아볼 수 있는가
- [ ] 함정을 맞히면 **점수가 눈에 띄게 내려간다**
- [ ] 오래 당기고 있으면 **화면이 흔들린다** — 벌로 느껴지는가, 그냥 성가신가
- [ ] 짧게 당겼다 놓는 평소 사격은 **안 흔들린다**
- [ ] 전부 함정인 웨이브가 **실제로 나온다** (안 나오면 `trap_ratio_max`를 올린다)
- [ ] **난사가 예전만큼 이득이 아니다** (Task 10의 튜닝이 들었는가)
- [ ] 결과 화면의 **벌점 내역**에 함정으로 잃은 점수가 찍힌다

> 실측에 필요한 로컬 설정은 슬라이스 2에서 쓴 것과 같다: 서버 에디터는 **Entrance 씬에서** Play, `ConfigureRoomComponent`의 게스트 uuid가 지금 DB와 맞아야 한다(안 맞으면 로그의 `[Auth] 접속 거부: 명단에 없는 참가자: <uuid>`에 찍힌 값을 넣는다).

- [ ] **Step 6: 판정 결과를 ROADMAP에 적는다**

**답이 "아니오"면 그 사실을 크게 적는다.** spec §12: *"기반이 틀렸으면 그 위에 더 얹기 전에 알아야 한다."* 재미가 없다고 나오면 다음 슬라이스로 넘어가지 말고 무엇을 바꿀지 다시 정한다.

---

## 확인 목록 (머지 전에 한 번 더)

- [ ] `MessageIds.cs`와 `Protos/`를 **하나도 안 건드렸다**(이 슬라이스는 와이어를 안 바꾼다)
- [ ] 클·서 `ArcheryConfigProvider`의 `new ArcheryConfig(...)` 블록이 **diff에서 완전히 같다**
- [ ] `#ArcheryConfig`/`#ArcheryTarget`의 **기존 컬럼 값이 하나도 안 바뀌었다**(Task 10의 의도한 튜닝 제외)
- [ ] `TbArcheryTarget`에 `is_trap=TRUE`인 줄이 있고 `trap_ratio_max > 0`이다
- [ ] 새로 만든 `.cs` 셋(`ArcheryHitRules` / `ArcheryShake` / 테스트 둘)마다 `.meta`가 함께 스테이지됐다
- [ ] 두 Unity 레포의 `git status --short`에 로컬 픽스처가 **커밋에 섞이지 않았다**
- [ ] 서버 EditMode 전부 초록, 새 테스트가 **이름으로** 결과에 보인다(개수만 보지 않는다)
- [ ] `ArcheryConfig`가 클라 `ArcheryLifetimeScope`에 등록돼 있다(`ArcheryAimView`가 생성자로 받는다)
- [ ] **클·서를 함께 배포했다** — 난수 순서가 바뀌었다

---

## 자기 점검 (계획을 쓴 뒤)

**spec 대응:**

| spec | 태스크 |
|---|---|
| §2.2 미리 당기기 대가 ① 줌인 | **이미 있음** (`ArcheryAimView`) — 안 하는 것 표에 명시 |
| §2.2 대가 ② 손떨림 | Task 5(수식) + Task 9(화면) |
| §2.2 대가 ③ 참아야 할 때 | Task 3(전부 함정인 웨이브) + Task 8(색으로 보임) |
| §3 함정 비율이 웨이브마다 다르다 | Task 2(범위) + Task 3(뽑기) |
| §4 함정을 맞히면 점수 차감 | Task 4(규칙) + Task 7(적용) |
| §4 벌칙 *종류*를 한 곳으로 | Task 4 (`ArcheryHitRules.Resolve`) |
| §4 숫자는 데이터로 | Task 6 + Task 10 |
| §4 일반화된 효과 시스템 안 짓기 | Task 4가 함수 하나로 끝낸다 |
| §8 `#ArcheryTarget`에 함정 여부 / `#ArcheryConfig`에 함정 비율·흔들림 | Task 6 |
| §10 흔들림은 유니티 없이 단위 테스트 | Task 5 (`ArcheryShakeTests` 일곱) |
| §12 확인 지점 | Task 11 Step 5~6 |
| §4 사람을 맞혔을 때 쏜 사람 차감 | **안 함** — 판정 축과 별개, 안 하는 것 표에 명시 |
| §5 직선맵 | **안 함** — 판정 뒤 |
