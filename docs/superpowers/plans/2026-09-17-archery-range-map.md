# 양궁 사거리 맵 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 활쏘기 모드에 **레인형 양궁 사거리 맵**을 더한다 — 사수마다 자기 레인과 자기 과녁을 갖고, 매치마다 뽑힌 순서대로 거리별 과녁이 노출되며, 꽂힌 자리(동심원 띠)로 점수가 갈리고, 화살은 과녁 수만큼만 주어진다.

**Architecture:** 모드는 그대로 하나다. 맵은 **값만** 준다 — 씬이 레인·사대·과녁 자리(공간)를, 마스터데이터가 거리·노출 시간·띠 점수(숫자)를 준다. 두 맵의 과녁 생성은 공유 구체 클래스 `ArcheryCourse` 한 곳에서 `course_kind` 값으로 갈린다(인터페이스 seam 금지 — 시뮬은 클·서가 같은 구체 코드를 돌린다). 과녁은 여전히 **통신하지 않는다**: 양쪽이 (매치 씨앗 + 레이아웃 + 명단)으로 각자 계산한다.

**Tech Stack:** Unity 6.3 (6000.3.16f1) · C# · LOP-Shared 패키지(순수 시뮬) · Luban MasterData · VContainer · Mirror(와이어, 이번엔 **변경 없음**) · NUnit EditMode

**Spec:** `docs/superpowers/specs/2026-09-15-archery-range-map-design.md` — 이 계획은 그 스펙을 근거로 논증한다. 실행자는 **둘 다** 읽는다.

**앞선 계획:** `docs/superpowers/plans/2026-09-16-archery-range-foundation.md` (완료·머지됨, 2026-09-17). 토대에서 이미 들어온 것: 과녁 모양(`ArcheryTargetShape`), 판 판정(`ArcheryHitTest.SegmentHitsFace`/`SegmentHitsTarget`), 띠 채점(`ArcheryRingBand`·`ArcheryHitRules`), 맵별 설정 키(`TbArcheryConfig.id = mapId`), 띠 표(`TbArcheryRing`). **이 계획은 그 위에 얹는다 — 그 기계장치를 다시 만들지 않는다.**

---

## Global Constraints

이 절은 **모든 태스크의 요구사항에 묵시적으로 포함된다.**

1. **원형 맵(`archery_circle`, mapId=5)의 동작이 한 군데도 바뀌면 안 된다.** 같은 씨앗이면 과녁 목록이 글자 단위로 같아야 하고, 점수(+1/+2/+4, −3/−5/−10)가 같아야 한다. 원형 맵은 모든 새 값의 **기본값 자리**에 놓인다.
2. **난수 소비 순서는 프로토콜이다.** `ArcheryWaveGenerator.Fill`의 뽑기 순서·횟수를 **건드리지 않는다.** 사거리 코스는 난수를 판 시작에 **한 번만** 쓴다(순서 뽑기). 그 외에는 전부 산수다.
3. **시뮬 코드는 구체 클래스를 공유한다.** 클·서가 다른 구현을 넣을 여지를 만드는 인터페이스 seam을 시뮬에 두지 않는다. 인터페이스는 사이드가 **달라야 하는** I/O 어댑터에만.
4. **쌍둥이 파일은 주석 한 줄만 달라야 한다.** `ArcheryConfigProvider.cs`(클·서)처럼 양쪽에 같은 이름으로 있는 파일은, `diff`가 "상대 쪽을 가리키는 주석" 한 줄만 내놓아야 한다.
5. **와이어는 건드리지 않는다.** proto·`MessageIds`·메시지 타입에 손대지 않는다. 슬롯 비트마스크(`ArcheryStateToC`)가 사거리에서도 그대로 쓰인다 — 사거리의 슬롯 = 사수 번호다.
6. **Luban 컬럼은 이름이 아니라 자리로 읽힌다.** 엑셀에 열을 넣을 때는 **맨 끝에 덧붙인다.** 기존 열 사이에 끼우면 기존 값이 조용히 밀린다.
7. **`git reset --hard` 금지.** 사용자의 로컬 픽스처(폰트·ProjectSettings·아트 서브모듈 포인터)가 워킹트리에 상시 떠 있다. 커밋은 **바꾼 파일만 경로로 지정**하고, `git add -A`/`git commit -a`를 쓰지 않는다. 커밋 전 `git status --short`로 스테이지된 것이 의도한 파일뿐인지 확인한다.
8. **Unity `.meta` 파일은 새 파일과 함께 커밋한다.** 없으면 GUID가 달라져 씬·프리팹 참조가 끊긴다. `.meta`를 손으로 만들지 않는다 — 에디터가 만든 것만 커밋한다.
9. **머지·푸시·배포는 컨트롤러만 한다.** 실행자는 자기 워크트리 안에서 브랜치에 커밋만 한다.
10. **테스트는 서버 에디터로 돌린다.** LOP-Shared·MasterData 패키지는 `file:` 참조라 서버 프로젝트가 같은 코드를 컴파일한다. 클라 에디터는 사용자가 Play로 쓰고 있을 수 있어 `run_tests`를 걸지 않는다(테스트 러너가 씬 저장 모달을 띄우면 에디터가 물린다).
    ```bash
    unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" \
      run_tests --mode EditMode --filter "<테스트클래스>"
    ```
    돌리기 **전에** 컴파일이 초록인지 본다: `unity command --project-path <서버> recompile_status` → `failed:false, errors:[]`.

### 산업 표준 매핑 (이름을 지어낸 것이 아님을 못박는다)

| 우리 이름 | 표준 | 근거 |
|---|---|---|
| `ArcheryCourse` | **course of fire** — 사격·양궁 경기에서 "무엇을 어느 순서로 쏘는가" | 순서·거리·노출을 묶은 것에 대한 표준 용어. `Round`는 이 코드베이스의 `MatchRound`와 충돌해 쓰지 않는다 |
| `ArcheryLane` | **lane** — 사대~과녁까지의 한 줄 | 실내 양궁장·사격장의 표준 단위 |
| `ArcheryQuiver` | **quiver** — 화살통 | 남은 화살을 들고 있는 것의 표준 이름 |
| `Stands` (레인의 과녁 자리) | **butt / bale** — 과녁을 세우는 받침 | 스펙 §10이 쓴 용어. 코드 가독성 때문에 `Stands`로 두고 문서 주석에 표준어를 적는다 |
| 과녁면 반경 0.61m | **122cm 표준 과녁면** | World Archery 표준(70m 경기용)의 지름 그대로 |
| 띠 채점 | 양궁 표준 과녁면(중심 10점 → 바깥으로) | 스펙 §3.2 |

---

## 파일 구조

### 새로 만드는 것

| 파일 | 책임 |
|---|---|
| `LOP-Shared/Runtime/Scripts/Game/ArcheryLane.cs` | 맵 씬에 찍는 레인 마커(MonoBehaviour). 사대 위치 + 과녁 자리들. `SpawnPoint`와 같은 성격 |
| `LOP-Shared/Runtime/Scripts/Game/ArcheryRangeLayout.cs` | 씬에서 읽어 낸 **값**(순수 C#). 레인별 사대 자리·바라보는 쪽·과녁 자리 목록 |
| `LOP-Shared/Runtime/Scripts/Game/ArcheryCourse.cs` | 과녁 생성의 **단일 진입점**. `course_kind`로 웨이브/사거리를 가른다. 순서 뽑기·단계 경계·화살 수 |
| `LOP-Shared/Runtime/Scripts/Game/ArcheryQuiver.cs` | 남은 화살(데이터만). 사거리 맵에서만 붙는다 |
| `LOP-Client/Assets/Scripts/Game/ArcheryRangeLayoutProvider.cs` | 씬 → `ArcheryRangeLayout` (사이드 로컬 어댑터, 서버와 쌍둥이) |
| `LOP-Server/Assets/Scripts/Game/ArcheryRangeLayoutProvider.cs` | 위와 쌍둥이 |
| `infrastructure/table/Datas/#ArcheryRange.xlsx` | 거리별 노출 시간·거리(m) |
| `Art/Assets/Art/Scenes/ArcheryRangeMap.unity` | 사거리 맵 씬(레인·사대·과녁 자리) |

### 고치는 것

| 파일 | 무엇을 |
|---|---|
| `LOP-Shared/.../ArcheryTarget.cs` | 수명을 파생값 → **값**으로, 주인(`OwnerUserId`) 추가 |
| `LOP-Shared/.../ArcheryConfig.cs` | `CourseKind`·`RangeTargetId`·`StepGapTicks`·`MatchDurationTicks` 추가 |
| `LOP-Shared/.../ArcheryWaveGenerator.cs` | 새 생성자 인자 두 개를 채운다(동작 불변) |
| `LOP-Shared/.../ArcheryAimSystem.cs` | 화살통이 비면 안 쏜다 |
| `LOP-Shared/.../ArcheryWorld.cs` | 되감기 저장에 화살통 추가 |
| `LOP-{Client,Server}/.../ArcheryConfigProvider.cs` | 새 컬럼 네 개를 읽어 넘긴다(쌍둥이) |
| `LOP-{Client,Server}/.../Entity/ArcheryPlayerCreator.cs` | 사거리 맵이면 화살통을 붙인다 |
| `LOP-Server/.../ArcheryRuleSystem.cs` | 레인에 세우기, 코스 길이로 판 끝, 레이아웃↔데이터 대조 |
| `LOP-Server/.../TickSystems/ArcheryHitSystem.cs` | 과녁 목록을 `ArcheryCourse`에서, **주인만 먹는다** |
| `LOP-Client/.../Game/ArcheryTargetView.cs` | 판(Face) 과녁을 띠 색으로 그린다 |
| `LOP-Client/.../TickSystems/ArcheryArrowStickSystem.cs` | 과녁 목록을 `ArcheryCourse`에서 |
| `LOP-{Client,Server}/.../ArcheryLifetimeScope.cs` | 새 등록(`ArcheryRangeLayout`·`ArcheryCourse`) |
| `LOP-Client/.../UI/ArcheryPad/ArcheryPad{View,ViewModel}.cs` | 남은 화살 표시 |
| `MasterData-{Client,Server}/Runtime/Scripts/LOPMasterData.cs` | `TableFiles`에 `tbarcheryrange` |
| `MasterData-Server/Tests/EditMode/ArcheryTargetSeparationTests.cs` | **웨이브가 뽑을 수 있는 종류만** 재도록 좁힌다(아래 Task 2에 이유) |
| `infrastructure/table/Datas/#ArcheryConfig.xlsx` · `#ArcheryTarget.xlsx` · `#ArcheryRing.xlsx` · `#Map.xlsx` · `__tables__.xlsx` | 값 |

### 이번에 **안** 하는 것 (스펙 §9)

클레이 맵 · 움직이는 구 판정 갈아엎기 · 레인 안 동시 노출 · 코스 편집기 · 바람/항력 · 원형 맵 난이도 조정. **와이어 변경도 없다.**

---

## Task 1: 과녁이 수명과 주인을 **값으로** 들고 다니게 한다

지금 과녁의 수명은 `2 × 솟는속도 ÷ 중력`이라는 **파생값**이다. 사거리 과녁은 솟지 않고 제자리에 서 있으므로(솟는 속도 0) 이 식은 수명 0을 내놓는다 — 서자마자 사라진다. 수명을 값으로 바꾸면 두 맵이 같은 판정 코드를 쓴다. 주인(`OwnerUserId`)도 같은 이유로 지금 넣는다 — 값 그릇을 두 번 고치지 않으려는 것이다.

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryTarget.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryTargetMotion.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryWaveGenerator.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryHitRules.cs`
- Create: `LeagueOfPhysical-Shared/Tests/EditMode/ArcheryTargetValueTests.cs`
- Modify(컴파일 맞추기): `LeagueOfPhysical-Shared/Tests/EditMode/{ArcheryFaceHitTests,ArcheryRingScoringTests,ArcheryHitRulesTests,ArcheryTargetMotionTests}.cs`
- Modify(컴파일 맞추기): `LeagueOfPhysical-Server/Assets/Tests/Editor/ArcheryHitSystemTests.cs`

**Interfaces:**
- Produces (뒤 태스크가 이 이름·차례를 그대로 쓴다):
  - `ArcheryTarget(int waveIndex, int slotIndex, Vector3 origin, float riseSpeed, long spawnTick, float radius, int points, bool isTrap, ArcheryTargetShape shape, IReadOnlyList<ArcheryRingBand> bands, Vector3 facing, float lifetimeSeconds, string ownerUserId)` — 인자 둘이 **맨 끝에** 붙는다
  - `float ArcheryTarget.LifetimeSeconds` (파생 프로퍼티 → **필드**), `string ArcheryTarget.OwnerUserId` (빈 문자열 = 주인 없음)
  - `static float ArcheryTargetMotion.LifetimeFor(float riseSpeed)`
  - `static bool ArcheryHitRules.CanTake(in ArcheryTarget target, string shooterUserId)`

- [ ] **Step 1: 지금 동작을 박제하는 특성 시험을 먼저 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/ArcheryTargetValueTests.cs`:

```csharp
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 과녁이 들고 다니는 <b>값</b>에 대한 시험. 값 그릇을 바꾸는 동안 원형 맵의 과녁이
    /// 한 글자도 안 바뀌는 것을 박제한다(특성 시험 — 고치기 <b>전</b>에 통과해야 한다).
    /// </summary>
    public class ArcheryTargetValueTests
    {
        private static ArcheryConfig CircleLikeConfig()
        {
            //  배포된 원형 맵 값 그대로(#ArcheryConfig.xlsx id=5, #ArcheryTarget.xlsx 6줄).
            //  여기 숫자가 그 파일과 같아야 이 시험이 실제로 도는 판을 재는 것이 된다.
            var kinds = new List<ArcheryTargetKind>
            {
                new ArcheryTargetKind(0.45f, 1, 30, false, ArcheryTargetShape.Sphere, null),
                new ArcheryTargetKind(0.30f, 2, 40, false, ArcheryTargetShape.Sphere, null),
                new ArcheryTargetKind(0.20f, 4, 30, false, ArcheryTargetShape.Sphere, null),
                new ArcheryTargetKind(0.45f, -3, 30, true, ArcheryTargetShape.Sphere, null),
                new ArcheryTargetKind(0.30f, -5, 40, true, ArcheryTargetShape.Sphere, null),
                new ArcheryTargetKind(0.20f, -10, 30, true, ArcheryTargetShape.Sphere, null),
            };
            return new ArcheryConfig(120, 3, 5, 3.5f, 0.3f, 0.6f, 1.2f, 0f, 1f,
                                     1.2f, 2.5f, 0f, 1.2f, 2.4f, 12, 20, kinds);
        }

        //  비교하기 쉬운 한 줄로 만든다. 부동소수는 반올림이 숨지 않게 비트로 적는다.
        private static string Canonical(List<ArcheryTarget> targets)
        {
            string B(float v) => System.BitConverter.SingleToInt32Bits(v).ToString("X8");
            var sb = new StringBuilder();
            foreach (var t in targets)
            {
                sb.Append(t.WaveIndex).Append('/').Append(t.SlotIndex)
                  .Append(' ').Append(B(t.Origin.x)).Append(',').Append(B(t.Origin.y)).Append(',').Append(B(t.Origin.z))
                  .Append(' ').Append(B(t.RiseSpeed)).Append(' ').Append(t.SpawnTick)
                  .Append(' ').Append(B(t.Radius)).Append(' ').Append(t.Points)
                  .Append(' ').Append(t.IsTrap ? 'T' : 'F').Append(' ').Append((int)t.Shape)
                  .Append('\n');
            }
            return sb.ToString();
        }

        [Test]
        public void 원형_맵_과녁_목록이_값_그릇을_바꿔도_그대로다()
        {
            var targets = new List<ArcheryTarget>();
            var sb = new StringBuilder();
            for (int wave = 0; wave < 5; wave++)
            {
                ArcheryWaveGenerator.Fill(targets, 4841841021168904955UL, wave, CircleLikeConfig(), 1000L);
                sb.Append(Canonical(targets));
            }

            //  이 문자열은 고치기 **전** 코드를 돌려서 얻은 것이다. 값 그릇을 바꾼 뒤에도 같아야 한다.
            //  다르면 원형 맵의 과녁이 실제로 달라진 것이다 — 문자열을 고쳐 통과시키지 말고 코드를 되돌릴 것.
            Assert.AreEqual(Golden, sb.ToString());
        }

        private const string Golden = "__여기에 붙여넣는다__";
    }
}
```

- [ ] **Step 2: 아직 아무것도 안 고친 상태에서 금값(golden)을 얻는다**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" \
  run_tests --mode EditMode --filter "ArcheryTargetValueTests"
```

Expected: FAIL. 실패 메시지에 기대값(빈 문자열)과 **실제값**이 함께 나온다. 그 실제값을 `Golden`에 그대로 넣는다(줄바꿈이 많으므로 `@"..."` 축자 문자열로 바꾸고 붙여넣는다). 다시 돌려 **PASS** 확인.

> **이 순서가 핵심이다.** 금값을 *고치기 전 코드*에서 떠야 "안 바뀌었다"를 증명할 수 있다. 고친 뒤에 뜨면 아무것도 증명하지 못한다.

- [ ] **Step 3: 새 값이 실린다는 시험을 쓴다(실패해야 한다)**

같은 파일에 덧붙인다:

```csharp
        [Test]
        public void 솟는_과녁의_수명은_솟는_속도에서_나온다()
        {
            var targets = new List<ArcheryTarget>();
            ArcheryWaveGenerator.Fill(targets, 12345UL, 0, CircleLikeConfig(), 0L);

            Assert.IsNotEmpty(targets, "과녁이 하나도 안 떴다 — 이 시험은 아무것도 재지 못한다");
            foreach (var t in targets)
            {
                //  값으로 실렸어도 웨이브 과녁의 수명은 예전 식 그대로여야 한다.
                Assert.AreEqual(2f * t.RiseSpeed / ArcheryTargetMotion.Gravity, t.LifetimeSeconds, 1e-6f);
            }
        }

        [Test]
        public void 웨이브_과녁은_주인이_없어_누구나_가져간다()
        {
            var targets = new List<ArcheryTarget>();
            ArcheryWaveGenerator.Fill(targets, 12345UL, 0, CircleLikeConfig(), 0L);

            Assert.IsNotEmpty(targets, "과녁이 하나도 안 떴다 — 이 시험은 아무것도 재지 못한다");
            foreach (var t in targets)
            {
                Assert.AreEqual(string.Empty, t.OwnerUserId);
                Assert.IsTrue(ArcheryHitRules.CanTake(t, "누구든"));
            }
        }

        [Test]
        public void 주인이_있으면_주인만_가져간다()
        {
            var mine = new ArcheryTarget(0, 0, Vector3.zero, 0f, 0L, 0.5f, 5, false,
                                         ArcheryTargetShape.Face, null, Vector3.back, 4f, "user-1");

            Assert.IsTrue(ArcheryHitRules.CanTake(mine, "user-1"));
            Assert.IsFalse(ArcheryHitRules.CanTake(mine, "user-2"),
                "남의 과녁을 가져갈 수 있으면 태워 버리는 방해가 열린다(스펙 5절)");
        }

        [Test]
        public void 서_있는_과녁은_수명이_솟는_속도와_무관하다()
        {
            var standing = new ArcheryTarget(0, 0, new Vector3(0f, 1f, 30f), 0f, 100L, 0.61f, 5, false,
                                             ArcheryTargetShape.Face, null, Vector3.back, 4f, "user-1");

            Assert.AreEqual(4f, standing.LifetimeSeconds);
            //  제자리에 선 과녁 — 어느 시각에 물어도 같은 자리다.
            Assert.AreEqual(standing.Origin, ArcheryTargetMotion.PositionAt(standing, 150d, 0.02f));
            Assert.IsTrue(ArcheryTargetMotion.IsAlive(standing, 150d, 0.02f));
            Assert.IsFalse(ArcheryTargetMotion.IsAlive(standing, 400d, 0.02f),
                "4초(200틱)가 지났는데 아직 살아 있다");
        }
```

- [ ] **Step 4: 돌려서 깨지는 것을 확인한다**

Expected: 컴파일 실패 — `ArcheryTarget` 생성자 인자 수가 안 맞고 `CanTake`가 없다.

- [ ] **Step 5: `ArcheryTarget`을 고친다**

파생 프로퍼티 `LifetimeSeconds`를 지우고 필드 둘을 더한다:

```csharp
        /// <summary>
        /// 솟았다 떨어지기까지, 또는 서 있다 사라지기까지의 시간(초).
        /// <b>파생값이 아니라 값이다</b> — 사거리 과녁은 솟지 않으므로(속도 0) 속도에서 수명을
        /// 유도하면 서자마자 사라진다. 웨이브 과녁은 <see cref="ArcheryTargetMotion.LifetimeFor"/>가
        /// 예전과 같은 값을 채운다.
        /// </summary>
        public readonly float LifetimeSeconds;

        /// <summary>
        /// 이 과녁의 주인(userId). <b>빈 문자열이면 주인이 없다</b> — 먼저 맞힌 사람이 먹는다(원형 맵).
        /// 주인이 있으면 주인이 맞혔을 때만 점수가 나고 그때만 사라진다.
        /// </summary>
        public readonly string OwnerUserId;
```

생성자 끝에 인자 둘을 붙이고 대입한다(널은 빈 문자열로 정규화 — 부르는 쪽마다 널 검사를 하지 않으려는 것이다):

```csharp
        public ArcheryTarget(int waveIndex, int slotIndex, Vector3 origin, float riseSpeed, long spawnTick,
                             float radius, int points, bool isTrap,
                             ArcheryTargetShape shape, IReadOnlyList<ArcheryRingBand> bands, Vector3 facing,
                             float lifetimeSeconds, string ownerUserId)
        {
            // ... 기존 대입은 그대로 두고 아래 둘만 더한다 ...
            LifetimeSeconds = lifetimeSeconds;
            OwnerUserId = ownerUserId ?? string.Empty;
        }
```

- [ ] **Step 6: 수명 계산을 한 곳으로 모은다**

`ArcheryTargetMotion.cs`에 더한다:

```csharp
        /// <summary>
        /// 그 속도로 솟은 과녁이 떠 있는 시간(초). 올라갔다 내려오므로 정점까지의 두 배다.
        /// <b>웨이브 과녁의 수명은 여기서만 나온다</b> — 식을 두 군데 적으면 한쪽만 고쳐진다.
        /// </summary>
        public static float LifetimeFor(float riseSpeed)
        {
            return 2f * riseSpeed / Gravity;
        }
```

- [ ] **Step 7: 웨이브 생성기가 새 값을 채우게 한다**

`ArcheryWaveGenerator.Fill`의 `into.Add(...)`만 바꾼다. **난수 뽑기 순서·횟수는 손대지 않는다.**

```csharp
                into.Add(new ArcheryTarget(waveIndex, slot, center, riseSpeed, spawnTick,
                                           kind.Radius, kind.Points, kind.IsTrap,
                                           kind.Shape, kind.Bands, Vector3.zero,
                                           ArcheryTargetMotion.LifetimeFor(riseSpeed), string.Empty));
```

- [ ] **Step 8: 가져갈 수 있는지를 정하는 자리를 만든다**

`ArcheryHitRules.cs`에 더한다(무슨 일이 일어나는지를 정하는 자리가 이미 여기다):

```csharp
        /// <summary>
        /// 이 화살이 이 과녁을 가져갈 수 있나. <b>주인이 없으면 누구든</b>(원형 맵), 주인이 있으면
        /// 주인만(사거리 맵). 점수뿐 아니라 <b>사라지는 것까지</b> 이 한 번으로 막는다 — 점수만
        /// 막고 사라지게 두면 남의 과녁을 태워 버리는 방해가 열린다.
        /// </summary>
        public static bool CanTake(in ArcheryTarget target, string shooterUserId)
        {
            return string.IsNullOrEmpty(target.OwnerUserId) || target.OwnerUserId == shooterUserId;
        }
```

- [ ] **Step 9: 시험 파일들의 생성자 호출을 맞춘다**

```bash
grep -rn "new ArcheryTarget(" --include=*.cs \
  "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" \
  "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets"
```

여덟 군데다(생성기 1 + 시험 7). 시험 쪽은 끝에 두 인자를 붙인다:
- 솟는 과녁을 다루는 시험 → `ArcheryTargetMotion.LifetimeFor(riseSpeed), string.Empty`
- 판정 모양만 보는 고정 과녁 → `10f, string.Empty` (수명이 판정에 안 걸리게 넉넉히)

> `ArcheryTargetMotionTests`에는 **수명 자체를 재는** 시험이 있다. 거기에 `10f`를 넣으면 시험이 재려던 것을 잃는다 — 반드시 `LifetimeFor(riseSpeed)`를 넣는다.

- [ ] **Step 10: 활쏘기 시험을 전부 돌린다**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" \
  run_tests --mode EditMode --filter "Archery"
```

Expected: 전부 PASS. **특히 `원형_맵_과녁_목록이_값_그릇을_바꿔도_그대로다`가 PASS여야 한다** — 이것이 "원형 맵이 안 바뀌었다"의 증거다.

- [ ] **Step 11: 이빨 확인 — 시험이 실제로 막고 있나**

`ArcheryTargetMotion.LifetimeFor`를 `2f * riseSpeed / Gravity` → `riseSpeed / Gravity`로 잠깐 바꿔 돌린다.
Expected: `솟는_과녁의_수명은_솟는_속도에서_나온다`와 `ArcheryTargetMotionTests`의 수명 시험이 FAIL.
확인했으면 되돌리고 다시 돌려 전부 PASS.

- [ ] **Step 12: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git status --short
git add Runtime/Scripts/Game/ArcheryTarget.cs Runtime/Scripts/Game/ArcheryTargetMotion.cs \
        Runtime/Scripts/Game/ArcheryWaveGenerator.cs Runtime/Scripts/Game/ArcheryHitRules.cs \
        Tests/EditMode/ArcheryTargetValueTests.cs Tests/EditMode/ArcheryTargetValueTests.cs.meta \
        Tests/EditMode/ArcheryFaceHitTests.cs Tests/EditMode/ArcheryRingScoringTests.cs \
        Tests/EditMode/ArcheryHitRulesTests.cs Tests/EditMode/ArcheryTargetMotionTests.cs
git commit -m "feat(archery): 과녁이 수명과 주인을 값으로 들고 다니게 한다"

cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git status --short
git add Assets/Tests/Editor/ArcheryHitSystemTests.cs
git commit -m "test(archery): 과녁 생성자에 수명·주인 인자를 맞춘다"
```

> 새 `.cs`의 `.meta`가 아직 없으면 서버 에디터에 `recompile`을 한 번 걸고(`recompile_status`가 `completed`가 될 때까지) 다시 본다 — 에셋 재스캔이 같이 돌아 `.meta`가 생긴다. `.meta`를 손으로 만들지 않는다.

---

## Task 2: 데이터 — 맵별 코스 값, 거리 표, 판 과녁과 띠

사거리 맵이 쓸 숫자를 전부 넣는다. **런타임 코드는 아직 아무것도 읽지 않는다** — 값이 먼저 있어야 다음 태스크가 그것을 읽는 코드를 쓸 수 있다.

> ⚠️ **Luban 컬럼은 이름이 아니라 자리로 읽힌다.** 새 열은 **맨 끝에** 붙인다. 기존 열 사이에 끼우면 기존 값이 조용히 한 칸씩 밀려, 예를 들어 `min_targets`에 `spawn_radius` 값이 들어간다.

**Files:**
- Modify: `infrastructure/table/Datas/#ArcheryConfig.xlsx` (열 4개 + 행 1개)
- Modify: `infrastructure/table/Datas/#ArcheryTarget.xlsx` (행 1개)
- Modify: `infrastructure/table/Datas/#ArcheryRing.xlsx` (행 3개)
- Modify: `infrastructure/table/Datas/#Map.xlsx` (행 1개)
- Modify: `infrastructure/table/Datas/__tables__.xlsx` (표 등록 1줄)
- Create: `infrastructure/table/Datas/#ArcheryRange.xlsx`
- Modify: `LeagueOfPhysical-MasterData-Client/Runtime/Scripts/LOPMasterData.cs` · `LeagueOfPhysical-MasterData-Server/Runtime/Scripts/LOPMasterData.cs` (`TableFiles`)
- Modify: `LeagueOfPhysical-MasterData-Server/Tests/EditMode/ArcheryTargetSeparationTests.cs`
- 생성물(직접 쓰지 않음, `gen.sh`가 만든다): 두 MasterData 패키지의 `Runtime.Generated/Scripts/MasterData/*.cs` · `Runtime.Generated/StreamingAssets/MasterData/*.bytes`

**Interfaces:**
- Consumes: 없음(첫 데이터 태스크)
- Produces (뒤 태스크가 이 이름 그대로 읽는다):
  - `TbArcheryConfig` 새 컬럼 → 생성 프로퍼티 `CourseKind`(int) · `RangeTargetId`(int) · `StepGapTicks`(int) · `MatchDurationTicks`(int)
  - `TbArcheryRange` 새 표 → 행 프로퍼티 `Id` · `MapId` · `StandIndex` · `DistanceM` · `ExposureTicks`
  - 맵 id **6** = `archery_range`, 과녁 종류 id **7** = `RangeFace`(shape 1, 반경 0.61)

- [ ] **Step 1: 엑셀을 고치는 스크립트를 쓴다**

`infrastructure/table/`에서 아래를 그대로 돌린다(한 번만 돌린다 — 두 번 돌리면 행이 두 번 들어간다. Step 2가 그것도 확인한다).

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table/Datas"
python - <<'PY'
import openpyxl

# ---------- 1) #ArcheryConfig: 새 열 4개를 맨 끝에, 그리고 사거리 맵 행 ----------
wb = openpyxl.load_workbook('#ArcheryConfig.xlsx'); ws = wb.active
assert ws.cell(1,1).value == '##var', '머리글 모양이 예상과 다르다'
start = ws.max_column + 1
cols = [('course_kind','int'), ('range_target_id','int'),
        ('step_gap_ticks','int'), ('match_duration_ticks','int')]
for i,(name,typ) in enumerate(cols):
    c = start + i
    ws.cell(1, c, name); ws.cell(2, c, typ); ws.cell(3, c, ''); ws.cell(4, c, name)

# 원형 맵(id=5) 행: 웨이브 코스(0), 판 과녁 안 씀(0), 단계 간격 없음(0), 지금과 같은 60초(3000틱)
circle_row = None
for r in range(5, ws.max_row+1):
    if str(ws.cell(r,2).value) == '5':
        circle_row = r
assert circle_row, '원형 맵(id=5) 행을 못 찾았다'
for i,v in enumerate([0, 0, 0, 3000]):
    ws.cell(circle_row, start+i, v)

# 사거리 맵(id=6) 행: 웨이브 칸은 원형 것을 그대로 복사한다(코스가 사거리면 안 읽히지만,
# 배포 데이터 검사가 그 칸들도 보므로 "말이 되는 값"으로 채워 둔다).
new_row = ws.max_row + 1
for c in range(1, start):
    ws.cell(new_row, c, ws.cell(circle_row, c).value)
ws.cell(new_row, 2, 6)                       # id = mapId
for i,v in enumerate([1, 7, 50, 0]):         # 사거리 코스 / 판 과녁 7번 / 단계 간격 1초 / 시간으론 안 끝냄
    ws.cell(new_row, start+i, v)
wb.save('#ArcheryConfig.xlsx')

# ---------- 2) #ArcheryTarget: 판(Face) 과녁 종류 ----------
wb = openpyxl.load_workbook('#ArcheryTarget.xlsx'); ws = wb.active
r = ws.max_row + 1
#  가중치 0 = 웨이브가 절대 안 뽑는다. 사거리 코스는 id로 집어 쓴다.
#  반경 0.61m = 지름 122cm, 양궁 표준 과녁면 그대로.
for c,v in enumerate(['', 7, 'RangeFace', 0.61, 5, 0, False, 1], start=1):
    ws.cell(r, c, v)
wb.save('#ArcheryTarget.xlsx')

# ---------- 3) #ArcheryRing: 판 과녁의 띠 셋 ----------
wb = openpyxl.load_workbook('#ArcheryRing.xlsx'); ws = wb.active
for i,(outer,points) in enumerate([(0.2,10), (0.5,8), (1.0,5)]):
    r = ws.max_row + 1
    for c,v in enumerate(['', 7+i, 7, outer, points], start=1):
        ws.cell(r, c, v)
wb.save('#ArcheryRing.xlsx')

# ---------- 4) #ArcheryRange: 새 표 ----------
wb = openpyxl.Workbook(); ws = wb.active
head = [('id','int'), ('map_id','int'), ('stand_index','int'),
        ('distance_m','float'), ('exposure_ticks','int')]
ws.cell(1,1,'##var'); ws.cell(2,1,'##type'); ws.cell(3,1,'##group'); ws.cell(4,1,'##')
for i,(name,typ) in enumerate(head):
    c = i + 2
    ws.cell(1,c,name); ws.cell(2,c,typ); ws.cell(3,c,''); ws.cell(4,c,name)
#  (거리m, 노출틱). 50Hz이므로 200틱 = 4초.
#  첫 값일 뿐이다 — 쳐 보며 맞춘다(스펙 11절).
rows = [(0, 12, 200), (1, 20, 200), (2, 30, 225), (3, 45, 250), (4, 65, 275), (5, 90, 300)]
for i,(stand, dist, expo) in enumerate(rows):
    r = 5 + i
    for c,v in enumerate(['', i+1, 6, stand, dist, expo], start=1):
        ws.cell(r, c, v)
wb.save('#ArcheryRange.xlsx')

# ---------- 5) __tables__: 새 표 등록 ----------
wb = openpyxl.load_workbook('__tables__.xlsx'); ws = wb.active
r = ws.max_row + 1
for c,v in enumerate(['', 'TbArcheryRange', 'ArcheryRange', True, '#ArcheryRange.xlsx',
                      'id', 'map', '', 'ArcheryRange(사거리 맵의 거리별 노출)', '', ''], start=1):
    ws.cell(r, c, v)
wb.save('__tables__.xlsx')

# ---------- 6) #Map: 사거리 맵 ----------
wb = openpyxl.load_workbook('#Map.xlsx'); ws = wb.active
r = ws.max_row + 1
for c,v in enumerate(['', 6, 5, 'archery_range', '양궁 사거리',
                      'Assets/Art/Scenes/ArcheryRangeMap.unity'], start=1):
    ws.cell(r, c, v)
wb.save('#Map.xlsx')
print('done')
PY
```

- [ ] **Step 2: 넣은 값을 눈으로 확인한다**

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table/Datas"
python - <<'PY'
import openpyxl
for f in ['#ArcheryConfig.xlsx','#ArcheryTarget.xlsx','#ArcheryRing.xlsx','#ArcheryRange.xlsx','#Map.xlsx']:
    ws = openpyxl.load_workbook(f).active
    print('====', f, ws.max_row, 'x', ws.max_column)
    for r in ws.iter_rows(values_only=True):
        print('   ', [('' if c is None else c) for c in r])
PY
```

확인할 것:
- `#ArcheryConfig`가 **22열**, 행이 **둘**(id 5, 6). id=5 행의 앞 18칸이 **하나도 안 변했다**
- `#ArcheryTarget`에 id=7 `RangeFace` 한 줄, 앞 6줄 그대로
- `#ArcheryRing`이 9줄(1~6은 그대로, 7~9가 target_id 7)
- `#ArcheryRange`가 6줄, `stand_index`가 0~5로 빈틈없다
- `#Map`에 id=6 한 줄

- [ ] **Step 3: 생성한다**

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table"
./gen.sh
```

Expected: `[done]`. 실패하면 대개 엑셀 머리글(`##type`) 오타다.

> `gen.sh`는 **매치메이킹 서버(`lop-backend`)에도** 생성한다(group `m`). `TbArcheryRange`는 group이 비어 있어(= c,s만) 매치메이킹 산출물은 안 바뀌어야 한다. `git -C "C:/Users/re5na/workspace/lop-backend" status --short`로 확인하고, **비어 있지 않으면 보고한다**(이 계획은 백엔드를 건드리지 않는다).

- [ ] **Step 4: 두 패키지의 로더 목록에 새 표를 더한다**

`LeagueOfPhysical-MasterData-Client/Runtime/Scripts/LOPMasterData.cs`와 `...-Server/...`의 `TableFiles`에 `"tbarcheryrange"`를 더한다(양쪽 다):

```csharp
            "tbarcheryconfig", "tbarcherytarget", "tbarcheryring", "tbarcheryrange"
```

> 이걸 빠뜨리면 Entrance 단계에서 `KeyNotFoundException`으로 죽는다. `__tables__.xlsx` 등록을 빠뜨리면 더 조용하다 — 표 자체가 안 생겨 컴파일에서 터진다.

- [ ] **Step 5: 기존 배포 데이터 검사를 좁힌다 (여기가 이 태스크의 함정이다)**

`MasterData-Server/Tests/EditMode/ArcheryTargetSeparationTests.cs`의 두 시험은 `TbArcheryTarget`의 **모든** 줄을 본다:

- `간격_기준이_가장_큰_과녁_둘을_떼어놓을_만큼은_된다` — 가장 큰 반경 × 2 ≤ `min_separation`. 판 과녁(0.61)이 들어오면 1.22 > 1.2라 **FAIL**한다.
- `함정과_깨끗한_과녁이_반경별로_같은_가중치를_공유한다` — 판 과녁은 짝이 되는 함정이 없어 **FAIL**한다.

둘 다 **웨이브 맵의 규칙**이다. 웨이브가 실제로 뽑을 수 있는 종류는 **가중치가 0보다 큰 것**뿐이므로(`ArcheryWaveGenerator.PickKind`가 가중치 합으로 뽑는다), 두 시험이 그 줄들만 보게 좁힌다.

두 시험의 행 순회 앞에 같은 필터를 넣는다:

```csharp
        //  웨이브가 실제로 뽑을 수 있는 종류만 잰다. 가중치 0은 뽑기에서 절대 안 걸리며
        //  (PickKind가 가중치 합으로 고른다), 사거리 맵이 id로 집어 쓰는 판 과녁이 그것이다.
        //  이 필터가 없으면 사거리용 큰 과녁 한 줄 때문에 원형 맵 규칙이 깨졌다고 잘못 잡는다.
        private static List<ArcheryTargetKind> WaveDrawableKinds(Tables tables)
        {
            var drawable = new List<ArcheryTargetKind>();
            foreach (var row in tables.TbArcheryTarget.DataList)
            {
                if (row.Weight > 0) { drawable.Add(row); }
            }
            Assert.IsNotEmpty(drawable, "웨이브가 뽑을 수 있는 과녁 종류가 하나도 없다 — 웨이브가 영원히 빈다");
            return drawable;
        }
```

> 타입 이름 주의: 여기서 `ArcheryTargetKind`는 **Luban 생성 타입** `LOP.MasterData.ArcheryTargetKind`다(시험이 `namespace LOP.MasterData.Tests`에 있어 그대로 짧게 쓰인다). LOP-Shared의 같은 이름과 다른 타입이다.

그리고 두 시험의 `foreach (var row in tables.TbArcheryTarget.DataList)` / `rows` 사용을 `WaveDrawableKinds(tables)`로 바꾼다. `함정_비율이_0보다_크면...`은 함정 종류를 세는데, 함정도 가중치가 있어야 뽑히므로 **같이 좁힌다.**

- [ ] **Step 6: 새 배포 데이터 검사를 쓴다**

같은 파일에 더한다:

```csharp
        //  화살 속도·중력의 원본은 LOP-Shared다 — MasterData 패키지는 Shared를 참조하지 않으므로
        //  (클·서 격리) 여기에 베껴 둘 수밖에 없다. 원본이 바뀌면 이 둘도 같이 고쳐야 한다.
        //    원본: LOP.ArcheryAimSystem.MaxSpeed = 65f, LOP.ArcheryTrajectory.Gravity = 20f,
        //          LOP.ArcheryAimSystem.FullDrawSeconds = 0.8f
        private const float ArrowMaxSpeed = 65f;
        private const float ArrowGravity = 20f;
        private const float FullDrawSeconds = 0.8f;
        private const float TickSeconds = 0.02f;

        private const int RangeMapId = 6;

        [Test]
        public void 사거리_맵의_거리가_빈틈없이_0부터_이어진다()
        {
            var tables = LoadTables();
            int checkedMaps = 0;

            foreach (var config in tables.TbArcheryConfig.DataList)
            {
                if (config.CourseKind != 1) { continue; }
                checkedMaps++;

                var indices = new List<int>();
                foreach (var row in tables.TbArcheryRange.DataList)
                {
                    if (row.MapId == config.Id) { indices.Add(row.StandIndex); }
                }
                indices.Sort();

                Assert.IsNotEmpty(indices,
                    $"맵 {config.Id}은 사거리 코스인데 TbArcheryRange에 줄이 하나도 없다 — 과녁이 영영 안 뜬다");
                for (int i = 0; i < indices.Count; i++)
                {
                    Assert.AreEqual(i, indices[i],
                        $"맵 {config.Id}의 stand_index가 0부터 빈틈없이 이어지지 않는다: "
                        + string.Join(",", indices) + " — 씬의 과녁 자리 번호와 짝이 안 맞는다");
                }
            }

            Assert.Greater(checkedMaps, 0,
                "사거리 코스 맵이 하나도 없다 — 이 시험은 아무것도 재지 못했다. "
                + "#ArcheryConfig.xlsx의 course_kind를 확인할 것");
        }

        [Test]
        public void 사거리_과녁이_꽉_당겨도_닿는_거리에_있다()
        {
            var tables = LoadTables();
            //  45도로 꽉 당겨 쏜 최대 사거리. 여기가 물리적인 벽이다.
            float maxRange = ArrowMaxSpeed * ArrowMaxSpeed / ArrowGravity;   // 211.25m
            //  벽에 딱 붙이면 각도가 1도만 어긋나도 못 닿는다 — 8할까지만 쓴다.
            float usable = maxRange * 0.8f;

            int checkedRows = 0;
            foreach (var row in tables.TbArcheryRange.DataList)
            {
                checkedRows++;
                Assert.LessOrEqual(row.DistanceM, usable,
                    $"거리 {row.DistanceM}m(맵 {row.MapId}, 자리 {row.StandIndex})는 "
                    + $"쓸 수 있는 사거리 {usable:0.#}m를 넘는다 — 그 과녁은 영영 못 맞히는데 에러도 안 난다");
            }
            Assert.Greater(checkedRows, 0, "TbArcheryRange가 비어 있다 — 아무것도 재지 못했다");
        }

        [Test]
        public void 노출_시간이_당기고_날아갈_시간보다_길다()
        {
            var tables = LoadTables();
            int checkedRows = 0;

            foreach (var row in tables.TbArcheryRange.DataList)
            {
                checkedRows++;
                //  45도로 꽉 당겨 쏘면 수평 속도는 65 × cos45 다. 그 거리까지 가는 데 걸리는 시간.
                float horizontalSpeed = ArrowMaxSpeed * Mathf.Cos(45f * Mathf.Deg2Rad);
                float flight = row.DistanceM / horizontalSpeed;
                float needed = FullDrawSeconds + flight;
                float exposure = row.ExposureTicks * TickSeconds;

                Assert.Greater(exposure, needed,
                    $"맵 {row.MapId} 자리 {row.StandIndex}({row.DistanceM}m)의 노출 {exposure:0.##}초는 "
                    + $"꽉 당기고({FullDrawSeconds}초) 날아가는 데({flight:0.##}초) 걸리는 {needed:0.##}초보다 짧다 "
                    + "— 물리적으로 못 맞히는 과녁이 된다");
            }
            Assert.Greater(checkedRows, 0, "TbArcheryRange가 비어 있다 — 아무것도 재지 못했다");
        }

        [Test]
        public void 사거리_맵이_가리키는_과녁_종류가_판이고_웨이브에는_안_뜬다()
        {
            var tables = LoadTables();
            int checkedMaps = 0;

            foreach (var config in tables.TbArcheryConfig.DataList)
            {
                if (config.CourseKind != 1) { continue; }
                checkedMaps++;

                var kind = tables.TbArcheryTarget.GetOrDefault(config.RangeTargetId);
                Assert.IsNotNull(kind,
                    $"맵 {config.Id}의 range_target_id({config.RangeTargetId})가 TbArcheryTarget에 없다");
                Assert.AreEqual(1, kind.Shape,
                    $"사거리 과녁 '{kind.Code}'는 판(shape=1)이어야 한다 — 공은 맞은 자리가 늘 가장자리라 "
                    + "띠 점수가 뜻을 잃는다");
                Assert.AreEqual(0, kind.Weight,
                    $"사거리 과녁 '{kind.Code}'의 가중치가 0이 아니다 — 원형 맵 웨이브가 이 판을 뽑아 "
                    + "허공에 세운다");
            }

            Assert.Greater(checkedMaps, 0, "사거리 코스 맵이 하나도 없다 — 아무것도 재지 못했다");
        }

        [Test]
        public void 판_과녁의_띠가_0부터_1까지_빈틈없이_덮는다()
        {
            var tables = LoadTables();
            int checkedKinds = 0;

            foreach (var kind in tables.TbArcheryTarget.DataList)
            {
                var bands = new List<ArcheryRing>();
                foreach (var ring in tables.TbArcheryRing.DataList)
                {
                    if (ring.TargetId == kind.Id) { bands.Add(ring); }
                }
                if (bands.Count == 0) { continue; }

                checkedKinds++;
                bands.Sort((a, b) => a.OuterRatio.CompareTo(b.OuterRatio));
                Assert.AreEqual(1f, bands[bands.Count - 1].OuterRatio, 1e-4f,
                    $"과녁 '{kind.Code}'의 바깥 띠가 1.0이 아니다 — 가장자리를 맞히면 점수가 엉뚱해진다");
                for (int i = 0; i < bands.Count; i++)
                {
                    Assert.Greater(bands[i].OuterRatio, 0f,
                        $"과녁 '{kind.Code}'에 바깥 경계가 0 이하인 띠가 있다 — 그 띠는 영영 안 걸린다");
                }
            }

            Assert.Greater(checkedKinds, 0, "띠가 달린 과녁이 하나도 없다 — 아무것도 재지 못했다");
        }
```

- [ ] **Step 7: 돌린다**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" \
  run_tests --mode EditMode --filter "ArcheryTargetSeparationTests"
```

Expected: 전부 PASS(기존 5 + 새 5). 하나라도 FAIL이면 데이터가 틀린 것이지 시험이 틀린 것이 아니다 — **시험을 느슨하게 고쳐 통과시키지 않는다.**

- [ ] **Step 8: 이빨 확인**

`#ArcheryRange.xlsx`의 `distance_m` 한 칸을 **250**으로 바꾸고 `gen.sh` → 시험.
Expected: `사거리_과녁이_꽉_당겨도_닿는_거리에_있다` FAIL. 되돌리고 다시 `gen.sh` → PASS.

- [ ] **Step 9: 클·서 데이터가 같은지 본다**

```bash
cd "C:/Users/re5na/workspace/LOP"
for t in tbarcheryconfig tbarcherytarget tbarcheryring tbarcheryrange; do
  md5sum "LeagueOfPhysical-MasterData-Client/Runtime.Generated/StreamingAssets/MasterData/$t.bytes" \
         "LeagueOfPhysical-MasterData-Server/Runtime.Generated/StreamingAssets/MasterData/$t.bytes"
done
```

Expected: 네 표 모두 양쪽 md5가 **같다**. 다르면 어느 컬럼에 group(`c`/`s`)이 잘못 붙은 것이다 — 갈리면 클·서가 다른 과녁을 본다.

- [ ] **Step 10: 커밋 (레포 셋)**

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure"
git status --short
git add table/Datas/#ArcheryConfig.xlsx table/Datas/#ArcheryTarget.xlsx table/Datas/#ArcheryRing.xlsx \
        table/Datas/#ArcheryRange.xlsx table/Datas/#Map.xlsx table/Datas/__tables__.xlsx
git commit -m "feat(masterdata): 사거리 맵의 거리·노출·판 과녁 값을 넣는다"

cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client"
git status --short
git add Runtime/Scripts/LOPMasterData.cs Runtime.Generated
git commit -m "feat(masterdata): TbArcheryRange 생성물과 로더 목록"

cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server"
git status --short
git add Runtime/Scripts/LOPMasterData.cs Runtime.Generated Tests/EditMode/ArcheryTargetSeparationTests.cs
git commit -m "feat(masterdata): TbArcheryRange 생성물과 사거리 데이터 검사"
```

> 새 `.cs`/`.bytes`의 `.meta`가 아직 없으면 서버 에디터에 `recompile`을 걸고 `recompile_status`가 `completed`가 된 뒤 다시 `git status`를 본다. `.meta`가 빠지면 다른 기계에서 GUID가 달라진다.

---

## Task 3: 맵 씬이 값을 주는 통로 — 레인 마커와 레이아웃

씬은 **공간**을 준다: 사대가 어디고, 그 앞 과녁 자리가 어디인가. 그 값을 읽어 순수 C# 값 객체로 만든다. **읽는 코드 전부를 공유 패키지에 둔다** — 찾는 조건(비활성 포함 여부)이나 정렬이 한 글자만 달라도 클·서가 다른 레인 목록을 얻기 때문이다. 마커 자체도 공유 패키지에 있어야 한다(`SpawnPoint`와 같은 이유 — 한쪽에만 있으면 반대쪽에서 missing script가 된다).

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryLane.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryRangeLayout.cs`
- Create: `LeagueOfPhysical-Shared/Tests/EditMode/ArcheryRangeLayoutTests.cs`

**Interfaces:**
- Consumes: 없음
- Produces:
  - `class ArcheryLane : MonoBehaviour { public int Order; public UnityEngine.Transform[] Stands; }`
  - `sealed class ArcheryRangeLayout` — `static ArcheryRangeLayout From(IEnumerable<ArcheryLane> lanes)`, `static ArcheryRangeLayout FromOpenScenes()`, `IReadOnlyList<ArcheryRangeLayout.Lane> Lanes`, `int StandCount`, `bool IsEmpty`
  - `readonly struct ArcheryRangeLayout.Lane { Vector3 ShooterPosition; Vector3 Forward; IReadOnlyList<Vector3> Stands; }`

- [ ] **Step 1: 실패하는 시험을 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/ArcheryRangeLayoutTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 맵 씬이 주는 값이 순수 C#으로 건너오는 통로. <b>클·서가 같은 씬을 읽어 같은 값을 얻어야</b>
    /// 과녁이 같은 자리에 선다 — 그래서 읽는 규칙(순서·검증)을 공유 코드에 둔다.
    /// </summary>
    public class ArcheryRangeLayoutTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) { Object.DestroyImmediate(go); }
            spawned.Clear();
        }

        private ArcheryLane MakeLane(string name, int order, Vector3 position, float yaw, params float[] standDistances)
        {
            var root = new GameObject(name);
            spawned.Add(root);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var lane = root.AddComponent<ArcheryLane>();
            lane.Order = order;
            lane.Stands = new Transform[standDistances.Length];
            for (int i = 0; i < standDistances.Length; i++)
            {
                var stand = new GameObject(name + "-stand" + i);
                stand.transform.SetParent(root.transform);
                //  사대 앞으로 그 거리만큼. 로컬 z가 곧 사거리다.
                stand.transform.localPosition = new Vector3(0f, 1.3f, standDistances[i]);
                lane.Stands[i] = stand.transform;
            }
            return lane;
        }

        [Test]
        public void 레인은_Order_순서대로_선다()
        {
            var b = MakeLane("B", 1, new Vector3(4f, 0f, 0f), 0f, 10f, 20f);
            var a = MakeLane("A", 0, new Vector3(0f, 0f, 0f), 0f, 10f, 20f);

            var layout = ArcheryRangeLayout.From(new[] { b, a });

            Assert.AreEqual(2, layout.Lanes.Count);
            Assert.AreEqual(new Vector3(0f, 0f, 0f), layout.Lanes[0].ShooterPosition,
                "Order가 작은 레인이 먼저 와야 한다 — 씬에서 찾아오는 순서는 실행마다 다를 수 있다");
            Assert.AreEqual(new Vector3(4f, 0f, 0f), layout.Lanes[1].ShooterPosition);
        }

        [Test]
        public void 과녁_자리는_세계_좌표로_건너온다()
        {
            MakeLane("A", 0, new Vector3(0f, 0f, 0f), 0f, 12f, 30f);

            var layout = ArcheryRangeLayout.From(Object.FindObjectsByType<ArcheryLane>(
                FindObjectsInactive.Include, FindObjectsSortMode.None));

            Assert.AreEqual(2, layout.StandCount);
            Assert.AreEqual(12f, layout.Lanes[0].Stands[0].z, 1e-4f);
            Assert.AreEqual(30f, layout.Lanes[0].Stands[1].z, 1e-4f);
            Assert.AreEqual(1.3f, layout.Lanes[0].Stands[0].y, 1e-4f, "과녁 높이는 씬이 정한다");
        }

        [Test]
        public void 레인이_돌아가_있으면_과녁도_그_방향으로_선다()
        {
            //  레인을 90도 돌리면 사대 앞은 +x 쪽이다.
            MakeLane("A", 0, Vector3.zero, 90f, 20f);

            var layout = ArcheryRangeLayout.From(Object.FindObjectsByType<ArcheryLane>(
                FindObjectsInactive.Include, FindObjectsSortMode.None));

            Assert.AreEqual(20f, layout.Lanes[0].Stands[0].x, 1e-3f);
            Assert.AreEqual(0f, layout.Lanes[0].Stands[0].z, 1e-3f);
            Assert.AreEqual(Vector3.right, layout.Lanes[0].Forward, "레인이 보는 쪽이 사대에서 과녁 쪽이다");
        }

        [Test]
        public void 레인마다_과녁_자리_수가_다르면_거부한다()
        {
            var a = MakeLane("A", 0, Vector3.zero, 0f, 10f, 20f);
            var b = MakeLane("B", 1, new Vector3(4f, 0f, 0f), 0f, 10f);

            //  같은 순서의 같은 거리를 모두가 본다는 것이 이 맵의 전제다(같은 시험지).
            //  한 레인만 자리가 모자라면 그 사수만 과녁이 안 뜨는데 에러가 안 난다 — 여기서 끊는다.
            Assert.Throws<System.InvalidOperationException>(
                () => ArcheryRangeLayout.From(new[] { a, b }));
        }

        [Test]
        public void 레인이_없으면_빈_레이아웃이다()
        {
            var layout = ArcheryRangeLayout.From(new ArcheryLane[0]);

            Assert.IsTrue(layout.IsEmpty, "원형 맵에는 레인이 없다 — 빈 레이아웃이 정상이다");
            Assert.AreEqual(0, layout.StandCount);
        }
    }
}
```

- [ ] **Step 2: 돌려서 실패를 확인한다**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" \
  run_tests --mode EditMode --filter "ArcheryRangeLayoutTests"
```
Expected: 컴파일 실패 — `ArcheryLane`·`ArcheryRangeLayout`이 없다.

- [ ] **Step 3: 레인 마커를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryLane.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 맵 씬에 찍어 두는 <b>레인</b> — 사수 한 명이 서는 자리와 그 앞에 과녁이 설 자리들.
    /// 게임 룰이 매치를 시작할 때 찾아 쓴다. <see cref="SpawnPoint"/>와 같은 성격의 표식이다.
    ///
    /// <para>이 마커가 <b>공용 패키지</b>에 있는 이유도 같다: 맵 씬은 클라에서 만들고 서버가 읽는데,
    /// 스크립트가 한쪽에만 있으면 반대쪽에서 missing script가 되고 그 빈 컴포넌트가 씬 주입을 끊는다.</para>
    ///
    /// <para>레인이 <b>보는 쪽</b>(transform.forward)이 사대에서 과녁 쪽이다. 과녁은 그 반대를
    /// 바라보고 선다 — 사수 쪽에서 온 화살만 맞는다.</para>
    /// </summary>
    public class ArcheryLane : MonoBehaviour
    {
        /// <summary>배정 순서. 작을수록 먼저 쓴다. 씬에서 찾아오는 순서는 보장되지 않아 이 값이 필요하다.</summary>
        public int Order;

        /// <summary>
        /// 과녁이 설 자리들. <b>가까운 것부터 먼 순서로</b> 넣는다 — 이 차례가 마스터데이터의
        /// <c>stand_index</c>와 짝이다. (양궁에서는 이 받침을 butt 또는 bale이라 부른다.)
        /// </summary>
        public Transform[] Stands;
    }
}
```

- [ ] **Step 4: 레이아웃 값 객체를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryRangeLayout.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 맵 씬에서 읽어 낸 <b>값</b>. 레인마다 사대 자리·보는 쪽·과녁 자리들을 담는다.
    /// 씬을 뒤지는 것부터 정렬·검증까지 <b>전부 여기 한 곳</b>에 둔다 — 클·서가 같은 씬에서
    /// 같은 값을 얻어야 과녁이 같은 자리에 선다.
    /// </summary>
    public sealed class ArcheryRangeLayout
    {
        public readonly struct Lane
        {
            /// <summary>사수가 서는 자리.</summary>
            public readonly Vector3 ShooterPosition;

            /// <summary>사대에서 과녁 쪽(단위 벡터). 과녁은 이 반대를 바라보고 선다.</summary>
            public readonly Vector3 Forward;

            /// <summary>과녁이 설 자리들(세계 좌표). 가까운 것부터 먼 순서.</summary>
            public readonly IReadOnlyList<Vector3> Stands;

            public Lane(Vector3 shooterPosition, Vector3 forward, IReadOnlyList<Vector3> stands)
            {
                ShooterPosition = shooterPosition;
                Forward = forward;
                Stands = stands;
            }
        }

        public IReadOnlyList<Lane> Lanes { get; }

        /// <summary>레인 하나가 가진 과녁 자리 수. 레인이 없으면 0이다.</summary>
        public int StandCount => Lanes.Count == 0 ? 0 : Lanes[0].Stands.Count;

        /// <summary>레인이 하나도 없나. 원형 맵이 그렇다.</summary>
        public bool IsEmpty => Lanes.Count == 0;

        private ArcheryRangeLayout(IReadOnlyList<Lane> lanes)
        {
            Lanes = lanes;
        }

        /// <summary>
        /// 지금 열려 있는 씬들에서 레인을 찾아 값으로 옮긴다. <b>맵 씬이 다 뜬 뒤에 불러야 한다</b> —
        /// 먼저 부르면 빈 레이아웃이 나오는데 에러가 안 난다.
        ///
        /// <para>씬을 뒤지는 이 한 줄도 공유 코드에 둔다 — 클·서가 <b>같은 조건</b>으로 찾아야
        /// 같은 레인 목록을 얻는다(비활성 오브젝트 포함 여부만 달라도 갈린다).</para>
        /// </summary>
        public static ArcheryRangeLayout FromOpenScenes()
        {
            return From(Object.FindObjectsByType<ArcheryLane>(
                FindObjectsInactive.Include, FindObjectsSortMode.None));
        }

        /// <summary>
        /// 씬에서 찾은 레인들을 값으로 옮긴다. <see cref="ArcheryLane.Order"/> 오름차순이고,
        /// Order가 같으면 오브젝트 이름으로 가른다 — 그러지 않으면 찾아온 순서가 그대로 남아
        /// 실행할 때마다 사수 배정이 바뀔 수 있다(<see cref="SpawnPlacement.Arrange"/>와 같은 규칙).
        /// </summary>
        public static ArcheryRangeLayout From(IEnumerable<ArcheryLane> lanes)
        {
            var result = new List<Lane>();
            if (lanes == null)
            {
                return new ArcheryRangeLayout(result);
            }

            var ordered = lanes
                .Where(lane => lane != null)
                .OrderBy(lane => lane.Order)
                .ThenBy(lane => lane.name, System.StringComparer.Ordinal)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                var lane = ordered[i];
                var stands = new List<Vector3>();
                if (lane.Stands != null)
                {
                    for (int s = 0; s < lane.Stands.Length; s++)
                    {
                        if (lane.Stands[s] == null)
                        {
                            throw new System.InvalidOperationException(
                                $"레인 '{lane.name}'의 과녁 자리 {s}가 비어 있다 — 씬에서 자리를 지우고 "
                                + "배열 칸을 안 지웠을 때 이렇게 된다");
                        }
                        stands.Add(lane.Stands[s].position);
                    }
                }

                if (i > 0 && stands.Count != result[0].Stands.Count)
                {
                    //  모두가 같은 순서로 같은 거리를 본다는 것이 이 맵의 전제다(같은 시험지).
                    //  한 레인만 자리가 모자라면 그 사수만 과녁이 안 뜨는데 에러가 안 난다.
                    throw new System.InvalidOperationException(
                        $"레인 '{lane.name}'의 과녁 자리가 {stands.Count}개인데 첫 레인은 "
                        + $"{result[0].Stands.Count}개다 — 레인마다 같아야 한다");
                }

                result.Add(new Lane(lane.transform.position, lane.transform.forward.normalized, stands));
            }

            return new ArcheryRangeLayout(result);
        }
    }
}
```

- [ ] **Step 5: 돌린다**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" \
  run_tests --mode EditMode --filter "ArcheryRangeLayoutTests"
```
Expected: 5개 전부 PASS.

- [ ] **Step 6: 이빨 확인**

`From`의 `OrderBy(lane => lane.Order)`를 지우고 돌린다.
Expected: `레인은_Order_순서대로_선다` FAIL. 되돌린다.

- [ ] **Step 7: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git status --short
git add Runtime/Scripts/Game/ArcheryLane.cs Runtime/Scripts/Game/ArcheryLane.cs.meta \
        Runtime/Scripts/Game/ArcheryRangeLayout.cs Runtime/Scripts/Game/ArcheryRangeLayout.cs.meta \
        Tests/EditMode/ArcheryRangeLayoutTests.cs Tests/EditMode/ArcheryRangeLayoutTests.cs.meta
git commit -m "feat(archery): 맵 씬이 레인과 과녁 자리를 값으로 넘기는 통로"
```

---

## Task 4: 코스 — 과녁이 언제 어디 서는지를 정하는 **한 곳**

지금은 소비처 셋(서버 판정·클라 뷰·클라 화살 꽂기)이 각자 `ArcheryWaveGenerator`를 직접 부른다. 맵이 둘이 되면 그 셋이 각자 분기하게 되고, 한 곳만 안 고쳐도 클·서가 갈린다. **진입점을 하나로 모으고 그 안에서 값(`course_kind`)으로 가른다** — 인터페이스 seam이 아니라 데이터 분기다(스펙 §3.1과 같은 방식).

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryCourseKind.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryRangeSettings.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryCourse.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryConfig.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryConfigProvider.cs` · `LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryConfigProvider.cs` (쌍둥이)
- Create: `LeagueOfPhysical-Shared/Tests/EditMode/ArcheryCourseTests.cs`

**Interfaces:**
- Consumes: Task 1의 `ArcheryTarget` 13인자 생성자 · Task 3의 `ArcheryRangeLayout` · Task 2의 MasterData 컬럼
- Produces:
  - `enum ArcheryCourseKind { Wave = 0, Range = 1 }`
  - `readonly struct ArcheryRangeStand { int StandIndex; float DistanceM; int ExposureTicks; }`
  - `sealed class ArcheryRangeSettings { ArcheryTargetKind Kind; IReadOnlyList<ArcheryRangeStand> Stands; int StepGapTicks; static ArcheryRangeSettings None; }`
  - `ArcheryConfig` 생성자 끝에 **기본값 있는** 인자 셋: `ArcheryCourseKind courseKind = ArcheryCourseKind.Wave, int matchDurationTicks = 0, ArcheryRangeSettings range = null`
  - `sealed class ArcheryCourse` — `ArcheryCourse(ArcheryConfig config, IMatchSeed matchSeed, IReadOnlyList<string> owners, float tickInterval, System.Func<ArcheryRangeLayout> layoutSource = null)`, `int IndexAt(long tick, long gameplayStartTick)`, `void Fill(List<ArcheryTarget> into, int index, long gameplayStartTick)`, `int StepCount`, `int ArrowsPerArcher`, `long MatchDurationTicks`

> **기본값을 쓰는 이유**: 원형 맵은 스펙 §7이 말하는 "모든 새 값의 기본값 자리"다. 기본값을 두면 지금 있는 시험 수십 곳의 `new ArcheryConfig(...)`가 그대로 컴파일되고, **그것들이 통과한다는 사실 자체가 "원형 맵이 안 바뀌었다"의 증거**가 된다.

- [ ] **Step 1: 실패하는 시험을 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/ArcheryCourseTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 과녁이 언제 어디 서는지를 정하는 한 곳. <b>사거리 맵은 난수를 판 시작에 딱 한 번</b>
    /// (순서 뽑기) 쓰고 그 뒤는 전부 산수다 — 그래서 여기 시험은 대부분 "산수가 맞나"를 잰다.
    /// </summary>
    public class ArcheryCourseTests
    {
        private sealed class FixedSeed : IMatchSeed
        {
            public FixedSeed(ulong value) { Value = value; }
            public ulong Value { get; }
        }

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) { Object.DestroyImmediate(go); }
            spawned.Clear();
        }

        //  레인 둘, 자리 셋. 사대는 원점과 x=4, 과녁은 앞(+z)으로 10/20/30m.
        private ArcheryRangeLayout Layout(int laneCount = 2, int standCount = 3)
        {
            var lanes = new List<ArcheryLane>();
            for (int i = 0; i < laneCount; i++)
            {
                var root = new GameObject("lane" + i);
                spawned.Add(root);
                root.transform.position = new Vector3(i * 4f, 0f, 0f);
                var lane = root.AddComponent<ArcheryLane>();
                lane.Order = i;
                lane.Stands = new Transform[standCount];
                for (int s = 0; s < standCount; s++)
                {
                    var stand = new GameObject("stand" + s);
                    stand.transform.SetParent(root.transform);
                    stand.transform.localPosition = new Vector3(0f, 1.3f, 10f * (s + 1));
                    lane.Stands[s] = stand.transform;
                }
                lanes.Add(lane);
            }
            return ArcheryRangeLayout.From(lanes);
        }

        private static ArcheryTargetKind FaceKind()
        {
            var bands = new List<ArcheryRingBand>
            {
                new ArcheryRingBand(0.2f, 10),
                new ArcheryRingBand(0.5f, 8),
                new ArcheryRingBand(1.0f, 5),
            };
            return new ArcheryTargetKind(0.61f, 5, 0, false, ArcheryTargetShape.Face, bands);
        }

        //  자리마다 노출이 다르다 — 단계 경계가 누적합이라는 것을 재려면 달라야 한다.
        private static ArcheryConfig RangeConfig(int standCount = 3)
        {
            var stands = new List<ArcheryRangeStand>();
            for (int s = 0; s < standCount; s++)
            {
                stands.Add(new ArcheryRangeStand(s, 10f * (s + 1), 100 + s * 50));
            }
            var range = new ArcheryRangeSettings(FaceKind(), stands, stepGapTicks: 25);

            return new ArcheryConfig(120, 3, 5, 3.5f, 0.3f, 0.6f, 1.2f, 0f, 1f,
                                     1.2f, 2.5f, 0f, 1.2f, 2.4f, 12, 20,
                                     new List<ArcheryTargetKind> { FaceKind() },
                                     ArcheryCourseKind.Range, 0, range);
        }

        private ArcheryCourse RangeCourse(ulong seed = 777UL, int laneCount = 2, int standCount = 3)
        {
            var layout = Layout(laneCount, standCount);
            return new ArcheryCourse(RangeConfig(standCount), new FixedSeed(seed),
                                     new[] { "user-a", "user-b" }, 0.02f, () => layout);
        }

        [Test]
        public void 같은_씨앗이면_같은_순서가_나온다()
        {
            var a = new List<ArcheryTarget>();
            var b = new List<ArcheryTarget>();
            RangeCourse(seed: 42UL).Fill(a, 0, 1000L);
            RangeCourse(seed: 42UL).Fill(b, 0, 1000L);

            Assert.AreEqual(a[0].Origin, b[0].Origin, "같은 씨앗인데 첫 과녁이 다른 자리에 섰다");
        }

        [Test]
        public void 다른_씨앗이면_순서가_달라진다()
        {
            //  자리가 셋뿐이라 우연히 같은 순열이 나올 수 있다 — 여러 씨앗 중 하나라도 달라지면 된다.
            var baseline = new List<ArcheryTarget>();
            RangeCourse(seed: 1UL).Fill(baseline, 0, 0L);

            bool anyDifferent = false;
            for (ulong seed = 2UL; seed <= 12UL; seed++)
            {
                var other = new List<ArcheryTarget>();
                RangeCourse(seed).Fill(other, 0, 0L);
                if (other[0].Origin != baseline[0].Origin) { anyDifferent = true; break; }
            }
            Assert.IsTrue(anyDifferent, "씨앗을 열한 번 바꿔도 첫 과녁이 늘 같은 자리다 — 순서를 안 뽑고 있다");
        }

        [Test]
        public void 모든_거리가_정확히_한_번씩_쓰인다()
        {
            var course = RangeCourse(standCount: 3);
            var seen = new List<float>();

            for (int step = 0; step < course.StepCount; step++)
            {
                var targets = new List<ArcheryTarget>();
                course.Fill(targets, step, 0L);
                Assert.IsNotEmpty(targets);
                seen.Add(targets[0].Origin.z);   // 레인 0의 과녁 거리
            }

            seen.Sort();
            CollectionAssert.AreEqual(new[] { 10f, 20f, 30f }, seen,
                "거리 목록의 순열이어야 한다 — 빠지거나 겹치면 화살 수와 과녁 수가 어긋난다");
        }

        [Test]
        public void 사수마다_자기_레인에_자기_과녁이_선다()
        {
            var course = RangeCourse(laneCount: 2);
            var targets = new List<ArcheryTarget>();
            course.Fill(targets, 0, 0L);

            Assert.AreEqual(2, targets.Count, "사수가 둘이면 과녁도 둘이다");
            Assert.AreEqual(0, targets[0].SlotIndex);
            Assert.AreEqual(1, targets[1].SlotIndex);
            Assert.AreEqual("user-a", targets[0].OwnerUserId);
            Assert.AreEqual("user-b", targets[1].OwnerUserId);
            Assert.AreEqual(0f, targets[0].Origin.x, 1e-4f, "첫 사수의 과녁은 첫 레인 위에 선다");
            Assert.AreEqual(4f, targets[1].Origin.x, 1e-4f);
            Assert.AreEqual(targets[0].Origin.z, targets[1].Origin.z, 1e-4f,
                "같은 단계에서는 모두가 같은 거리를 본다 — 같은 시험지다");
        }

        [Test]
        public void 과녁은_사수_쪽을_바라본다()
        {
            var course = RangeCourse();
            var targets = new List<ArcheryTarget>();
            course.Fill(targets, 0, 0L);

            //  레인이 +z를 보므로 과녁은 −z를 본다. 뒤에서 온 화살은 통과한다(토대 슬라이스).
            Assert.AreEqual(Vector3.back, targets[0].Facing);
            Assert.AreEqual(ArcheryTargetShape.Face, targets[0].Shape);
            Assert.AreEqual(0f, targets[0].RiseSpeed, "사거리 과녁은 솟지 않는다");
        }

        [Test]
        public void 단계_경계는_노출과_간격의_누적합이다()
        {
            var course = RangeCourse(standCount: 3);
            long start = 1000L;

            Assert.AreEqual(-1, course.IndexAt(start - 1, start), "출발 전에는 −1이다");
            Assert.AreEqual(0, course.IndexAt(start, start));

            //  단계 0의 길이 = 그 자리의 노출 + 간격. 순서가 씨앗마다 다르므로 값을 직접 읽어 잰다.
            var first = new List<ArcheryTarget>();
            course.Fill(first, 0, start);
            long firstLength = (long)Mathf.Round(first[0].LifetimeSeconds / 0.02f) + 25;

            Assert.AreEqual(0, course.IndexAt(start + firstLength - 1, start));
            Assert.AreEqual(1, course.IndexAt(start + firstLength, start));
        }

        [Test]
        public void 순서가_끝나면_판도_끝난다()
        {
            var course = RangeCourse(standCount: 3);
            long start = 0L;

            Assert.AreEqual(3, course.StepCount);
            Assert.GreaterOrEqual(course.IndexAt(course.MatchDurationTicks, start), course.StepCount,
                "코스 길이만큼 지나면 더 이상 단계가 없어야 한다");

            //  노출(100+150+200) + 간격(25×3) = 525틱
            Assert.AreEqual(525L, course.MatchDurationTicks);
        }

        [Test]
        public void 화살은_과녁_수만큼_주어진다()
        {
            Assert.AreEqual(3, RangeCourse(standCount: 3).ArrowsPerArcher);
            Assert.AreEqual(5, RangeCourse(standCount: 5).ArrowsPerArcher);
        }

        [Test]
        public void 웨이브_맵은_예전_생성기와_한_글자도_다르지_않다()
        {
            var kinds = new List<ArcheryTargetKind>
            {
                new ArcheryTargetKind(0.45f, 1, 30, false, ArcheryTargetShape.Sphere, null),
                new ArcheryTargetKind(0.30f, 2, 40, false, ArcheryTargetShape.Sphere, null),
                new ArcheryTargetKind(0.20f, 4, 30, false, ArcheryTargetShape.Sphere, null),
            };
            //  기본값 자리 — course_kind를 안 주면 웨이브다.
            var config = new ArcheryConfig(120, 3, 5, 3.5f, 0.3f, 0.6f, 1.2f, 0f, 1f,
                                           1.2f, 2.5f, 0f, 1.2f, 2.4f, 12, 20, kinds);
            var course = new ArcheryCourse(config, new FixedSeed(999UL), new[] { "user-a" }, 0.02f,
                                           () => ArcheryRangeLayout.From(new ArcheryLane[0]));

            var viaCourse = new List<ArcheryTarget>();
            var direct = new List<ArcheryTarget>();
            for (int wave = 0; wave < 4; wave++)
            {
                course.Fill(viaCourse, wave, 500L);
                ArcheryWaveGenerator.Fill(direct, 999UL, wave, config, 500L);

                Assert.AreEqual(direct.Count, viaCourse.Count, $"웨이브 {wave}의 과녁 수가 다르다");
                for (int i = 0; i < direct.Count; i++)
                {
                    Assert.AreEqual(direct[i].Origin, viaCourse[i].Origin);
                    Assert.AreEqual(direct[i].SpawnTick, viaCourse[i].SpawnTick);
                    Assert.AreEqual(direct[i].Points, viaCourse[i].Points);
                    Assert.AreEqual(direct[i].Radius, viaCourse[i].Radius);
                    Assert.AreEqual(direct[i].IsTrap, viaCourse[i].IsTrap);
                }
                Assert.AreEqual(ArcheryWaveGenerator.WaveIndexAt(600L, 500L, config),
                                course.IndexAt(600L, 500L));
            }

            Assert.AreEqual(0, course.ArrowsPerArcher, "웨이브 맵은 화살이 무제한이다");
        }

        [Test]
        public void 레인보다_사수가_많으면_있는_레인까지만_선다()
        {
            //  씬이 모자란 상태 — 서버 룰이 판 시작에 크게 실패시키지만(Task 6),
            //  코스 자체는 조용히 터지지 않아야 한다.
            var oneLane = Layout(laneCount: 1);
            var course = new ArcheryCourse(RangeConfig(), new FixedSeed(5UL),
                                           new[] { "user-a", "user-b" }, 0.02f, () => oneLane);
            var targets = new List<ArcheryTarget>();
            course.Fill(targets, 0, 0L);

            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual("user-a", targets[0].OwnerUserId);
        }
    }
}
```

- [ ] **Step 2: 돌려서 실패를 확인한다**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" \
  run_tests --mode EditMode --filter "ArcheryCourseTests"
```
Expected: 컴파일 실패.

- [ ] **Step 3: 값 타입 셋을 만든다**

`ArcheryCourseKind.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 이 맵의 과녁이 어떻게 뜨는가. <b>맵이 값으로 고른다</b>(<c>TbArcheryConfig.course_kind</c>) —
    /// 코드가 맵 이름을 알지 않게 하려는 것이다.
    /// </summary>
    public enum ArcheryCourseKind
    {
        /// <summary>묶음이 주기적으로 솟는다(원형 맵). 웨이브마다 난수를 여러 번 쓴다.</summary>
        Wave = 0,

        /// <summary>정해진 순서대로 거리별 과녁이 선다(사거리 맵). 난수는 판 시작에 한 번뿐이다.</summary>
        Range = 1,
    }
}
```

`ArcheryRangeSettings.cs`:

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>과녁이 설 자리 하나에 대한 값. 자리는 씬이, 이 숫자들은 마스터데이터가 준다.</summary>
    public readonly struct ArcheryRangeStand
    {
        /// <summary>레인 안에서 몇 번째 자리인가. 씬의 <see cref="ArcheryLane.Stands"/> 차례와 짝이다.</summary>
        public readonly int StandIndex;

        /// <summary>사대에서 이 자리까지의 거리(m). <b>씬이 진짜 자리를 갖고 있고 이 값은 그 선언</b>이다 —
        /// 둘이 어긋나면 서버가 판 시작에 잡는다(배포 데이터 검사가 이 값으로 사거리를 판단하기 때문).</summary>
        public readonly float DistanceM;

        /// <summary>이 자리의 과녁이 서 있는 시간(틱).</summary>
        public readonly int ExposureTicks;

        public ArcheryRangeStand(int standIndex, float distanceM, int exposureTicks)
        {
            StandIndex = standIndex;
            DistanceM = distanceM;
            ExposureTicks = exposureTicks;
        }
    }

    /// <summary>사거리 코스에만 쓰이는 값들. 웨이브 맵에서는 <see cref="None"/>이다.</summary>
    public sealed class ArcheryRangeSettings
    {
        /// <summary>사거리 과녁이 쓰는 종류(판 하나). 코스가 난수로 고르지 않는다 — 늘 이것이다.</summary>
        public ArcheryTargetKind Kind { get; }

        /// <summary>자리 목록. <see cref="ArcheryRangeStand.StandIndex"/> 오름차순으로 들어온다.</summary>
        public IReadOnlyList<ArcheryRangeStand> Stands { get; }

        /// <summary>과녁이 사라진 뒤 다음 과녁이 설 때까지의 틈(틱).</summary>
        public int StepGapTicks { get; }

        /// <summary>웨이브 맵이 드는 빈 값. 널 검사를 부르는 쪽마다 하지 않으려는 것이다.</summary>
        public static readonly ArcheryRangeSettings None =
            new ArcheryRangeSettings(default, new ArcheryRangeStand[0], 0);

        public ArcheryRangeSettings(ArcheryTargetKind kind, IReadOnlyList<ArcheryRangeStand> stands, int stepGapTicks)
        {
            Kind = kind;
            Stands = stands ?? new ArcheryRangeStand[0];
            StepGapTicks = stepGapTicks;
        }
    }
}
```

- [ ] **Step 4: `ArcheryConfig`에 값 셋을 더한다**

프로퍼티:

```csharp
        /// <summary>이 맵의 과녁이 뜨는 방식. 안 적으면 웨이브다 — 원형 맵이 기본값 자리다.</summary>
        public ArcheryCourseKind CourseKind { get; }

        /// <summary>시간으로 판을 끝내는 길이(틱). <b>0 이하면 시간으로는 안 끝낸다</b>(사거리는 코스가 끝낸다).</summary>
        public int MatchDurationTicks { get; }

        /// <summary>사거리 코스에만 쓰이는 값들. 웨이브 맵에서는 <see cref="ArcheryRangeSettings.None"/>이다.</summary>
        public ArcheryRangeSettings Range { get; }
```

생성자 끝에 **기본값과 함께** 더한다:

```csharp
        public ArcheryConfig(int wavePeriodTicks, int minTargets, int maxTargets,
                             float spawnRadius, float spawnMinY, float spawnMaxY, float minSeparation,
                             float trapRatioMin, float trapRatioMax,
                             float shakeFreeSeconds, float shakeRampSeconds, float shakeMaxDegrees,
                             float riseHeightMin, float riseHeightMax, int staggerTicks, int restTicks,
                             IReadOnlyList<ArcheryTargetKind> kinds,
                             ArcheryCourseKind courseKind = ArcheryCourseKind.Wave,
                             int matchDurationTicks = 0,
                             ArcheryRangeSettings range = null)
        {
            // ... 기존 대입 그대로 ...
            CourseKind = courseKind;
            MatchDurationTicks = matchDurationTicks;
            Range = range ?? ArcheryRangeSettings.None;
        }
```

- [ ] **Step 5: `ArcheryCourse`를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryCourse.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// <b>과녁이 언제 어디 서는지를 정하는 한 곳.</b> 맵이 값으로 고른 방식(<see cref="ArcheryCourseKind"/>)에
    /// 따라 갈리지만, 부르는 쪽(서버 판정·클라 뷰·화살 꽂기)은 그 갈림을 모른다 — 셋이 각자 분기하면
    /// 한 곳만 안 고쳐져도 클·서가 다른 과녁을 본다.
    ///
    /// <para>클·서가 <b>같은 구체 클래스</b>를 돌린다(인터페이스 seam 금지). 같은 씨앗·같은 씬·같은
    /// 명단이면 같은 과녁이 나오므로 과녁은 통신하지 않는다.</para>
    /// </summary>
    public sealed class ArcheryCourse
    {
        //  순서 뽑기용 씨앗을 웨이브 스트림과 섞이지 않게 가른다. 웨이브는 Combine(seed, waveIndex)를
        //  쓰므로 작은 정수 영역을 피해 문자열 해시를 쓴다.
        private static readonly ulong CourseSalt = GameFramework.Rng.Hashing.Fnv1a64("archery-course");

        private readonly ArcheryConfig config;

        //  레이아웃은 **맵 씬이 다 뜬 뒤에** 읽어야 한다. 이 객체는 씬 로드보다 먼저 만들어지므로
        //  (서버는 스코프가 세워질 때 판정 시스템을 즉시 resolve한다) 값을 미리 받으면 빈 레이아웃이
        //  영영 굳는다 — 과녁이 하나도 안 뜨는데 에러도 안 난다. 그래서 "읽는 방법"만 받아 둔다.
        private readonly System.Func<ArcheryRangeLayout> layoutSource;
        private ArcheryRangeLayout layout;

        private readonly IMatchSeed matchSeed;
        private readonly IReadOnlyList<string> owners;
        private readonly float tickInterval;

        //  판 시작에 한 번 뽑은 순서(거리 자리 번호)와, 각 단계가 시작하는 상대 틱.
        //  씨앗이 늦게 도착하므로(서버가 보내 준다) 처음 쓸 때 만든다.
        private int[] order;
        private long[] stepStartTicks;

        public ArcheryCourse(ArcheryConfig config, IMatchSeed matchSeed, IReadOnlyList<string> owners,
                             float tickInterval, System.Func<ArcheryRangeLayout> layoutSource = null)
        {
            this.config = config;
            //  기본은 "열려 있는 씬에서 읽기". 시험만 다른 방법을 넣는다.
            this.layoutSource = layoutSource ?? ArcheryRangeLayout.FromOpenScenes;
            this.matchSeed = matchSeed;
            this.owners = owners ?? new string[0];
            this.tickInterval = tickInterval;
        }

        /// <summary>사거리 코스의 단계 수. 웨이브 맵은 끝이 없으므로 0이다.</summary>
        public int StepCount => config.CourseKind == ArcheryCourseKind.Range ? config.Range.Stands.Count : 0;

        /// <summary>사수 한 명이 받는 화살 수. <b>0이면 무제한</b>(웨이브 맵).</summary>
        public int ArrowsPerArcher => StepCount;

        /// <summary>
        /// 이 판의 길이(틱). 사거리는 <b>노출과 간격의 합</b>이라 순서와 무관하다 — 그래서 씨앗이
        /// 없어도 답할 수 있다. 웨이브 맵은 데이터가 적어 둔 값을 그대로 쓴다.
        /// </summary>
        public long MatchDurationTicks
        {
            get
            {
                if (config.CourseKind != ArcheryCourseKind.Range)
                {
                    return config.MatchDurationTicks;
                }

                long total = 0;
                for (int i = 0; i < config.Range.Stands.Count; i++)
                {
                    total += config.Range.Stands[i].ExposureTicks + config.Range.StepGapTicks;
                }
                return total;
            }
        }

        /// <summary>
        /// 이 틱에 서 있는 단계(웨이브 맵에서는 웨이브 번호). 출발 전이면 −1,
        /// 사거리에서 순서가 끝난 뒤면 <see cref="StepCount"/> 이상이다.
        /// </summary>
        public int IndexAt(long tick, long gameplayStartTick)
        {
            if (config.CourseKind != ArcheryCourseKind.Range)
            {
                return ArcheryWaveGenerator.WaveIndexAt(tick, gameplayStartTick, config);
            }

            if (gameplayStartTick == long.MaxValue || tick < gameplayStartTick)
            {
                return -1;
            }

            EnsureBuilt();
            long elapsed = tick - gameplayStartTick;
            if (elapsed >= MatchDurationTicks)
            {
                return StepCount;   // 순서가 끝났다 — 부르는 쪽은 이 값을 "더 없다"로 읽는다
            }

            for (int i = stepStartTicks.Length - 1; i >= 0; i--)
            {
                if (elapsed >= stepStartTicks[i])
                {
                    return i;
                }
            }
            return 0;
        }

        /// <summary>그 단계의 과녁을 채운다(먼저 비운다).</summary>
        public void Fill(List<ArcheryTarget> into, int index, long gameplayStartTick)
        {
            if (config.CourseKind != ArcheryCourseKind.Range)
            {
                ArcheryWaveGenerator.Fill(into, matchSeed.Value, index, config, gameplayStartTick);
                return;
            }

            into.Clear();
            if (index < 0 || index >= StepCount)
            {
                return;   // 출발 전이거나 순서가 끝났다
            }

            EnsureBuilt();
            var stand = config.Range.Stands[order[index]];
            var kind = config.Range.Kind;
            long spawnTick = gameplayStartTick + stepStartTicks[index];
            float lifetime = stand.ExposureTicks * tickInterval;

            //  사수마다 자기 레인에 하나씩. 슬롯 번호가 곧 사수 번호라, 먹힌 과녁을 알리는
            //  비트마스크(ArcheryStateToC)를 그대로 쓸 수 있다.
            int count = Mathf.Min(owners.Count, layout.Lanes.Count);
            for (int slot = 0; slot < count; slot++)
            {
                var lane = layout.Lanes[slot];
                Vector3 center = lane.Stands[stand.StandIndex];

                into.Add(new ArcheryTarget(index, slot, center, 0f, spawnTick,
                                           kind.Radius, kind.Points, kind.IsTrap,
                                           kind.Shape, kind.Bands,
                                           -lane.Forward,        // 사수 쪽을 바라본다
                                           lifetime, owners[slot]));
            }
        }

        //  씨앗은 서버가 보내 주므로 만들 때는 아직 없을 수 있다 — 처음 쓸 때 만든다.
        private void EnsureBuilt()
        {
            if (order != null)
            {
                return;
            }

            //  여기 오는 것은 이미 판이 시작한 뒤다(위 호출부가 출발 전이면 먼저 돌아간다) —
            //  그래서 맵 씬은 확실히 떠 있다.
            layout = layoutSource();

            int count = config.Range.Stands.Count;
            var draw = new int[count];
            for (int i = 0; i < count; i++)
            {
                draw[i] = i;
            }

            //  피셔–예이츠. 뒤에서부터 한 칸씩 자리를 바꾼다 — 난수를 정확히 count−1번 쓴다.
            var rng = new GameFramework.Rng.DeterministicRandom(
                GameFramework.Rng.Hashing.Combine(matchSeed.Value, CourseSalt));
            for (int i = count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (draw[i], draw[j]) = (draw[j], draw[i]);
            }

            var starts = new long[count];
            long cursor = 0;
            for (int i = 0; i < count; i++)
            {
                starts[i] = cursor;
                cursor += config.Range.Stands[draw[i]].ExposureTicks + config.Range.StepGapTicks;
            }

            stepStartTicks = starts;
            order = draw;   // 마지막에 넣는다 — 중간에 끊겨도 반쯤 만들어진 상태가 안 보이게
        }
    }
}
```

> ⚠️ `config.Range.Stands`는 **`StandIndex` 오름차순**으로 들어온다고 가정한다(`order[index]`가 곧 자리 번호다). 그 정렬은 값을 만드는 쪽(사이드 provider)의 몫이고, Task 2의 배포 데이터 검사가 번호가 0부터 빈틈없음을 지킨다.

- [ ] **Step 6: 사이드 provider가 새 값을 채우게 한다(쌍둥이)**

`ArcheryConfigProvider.Get()`의 `return new ArcheryConfig(...)` 앞에 더한다:

```csharp
            var courseKind = (ArcheryCourseKind)r.CourseKind;
            var range = ArcheryRangeSettings.None;
            if (courseKind == ArcheryCourseKind.Range)
            {
                var faceRow = md.Tables.TbArcheryTarget.GetOrDefault(r.RangeTargetId);
                if (faceRow == null)
                {
                    throw new System.InvalidOperationException(
                        $"맵 {mapId}의 range_target_id({r.RangeTargetId})가 TbArcheryTarget에 없다");
                }

                //  자리 번호 오름차순으로 넘긴다 — 코스가 이 차례를 자리 번호로 그대로 쓴다.
                var stands = new List<ArcheryRangeStand>();
                foreach (var row in System.Linq.Enumerable.OrderBy(
                             System.Linq.Enumerable.Where(md.Tables.TbArcheryRange.DataList,
                                                          x => x.MapId == mapId),
                             x => x.StandIndex))
                {
                    stands.Add(new ArcheryRangeStand(row.StandIndex, row.DistanceM, row.ExposureTicks));
                }
                if (stands.Count == 0)
                {
                    throw new System.InvalidOperationException(
                        $"맵 {mapId}은 사거리 코스인데 TbArcheryRange에 줄이 없다 — 과녁이 영영 안 뜬다");
                }

                range = new ArcheryRangeSettings(
                    new ArcheryTargetKind(faceRow.Radius, faceRow.Points, faceRow.Weight, faceRow.IsTrap,
                                          (ArcheryTargetShape)faceRow.Shape, BandsOf(md, faceRow.Id)),
                    stands, r.StepGapTicks);
            }
```

그리고 `return new ArcheryConfig(...)`의 마지막에 인자 셋을 붙인다:

```csharp
                kinds,
                courseKind, r.MatchDurationTicks, range);
```

**두 파일을 똑같이 고친다.** 다 고치고 확인한다:

```bash
diff "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryConfigProvider.cs" \
     "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryConfigProvider.cs"
```
Expected: **10번째 줄 주석 한 줄만** 다르다(상대 쪽을 가리키는 문장). 다른 줄이 나오면 쌍둥이가 갈린 것이다.

- [ ] **Step 7: 돌린다**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" \
  run_tests --mode EditMode --filter "Archery"
```
Expected: 새 `ArcheryCourseTests` 10개 + 기존 활쏘기 시험 전부 PASS.

- [ ] **Step 8: 이빨 확인 두 가지**

1. `EnsureBuilt`의 피셔–예이츠 루프를 지운다(순서 = 0,1,2…).
   Expected: `다른_씨앗이면_순서가_달라진다` FAIL.
2. `Fill`의 `-lane.Forward`를 `lane.Forward`로 바꾼다.
   Expected: `과녁은_사수_쪽을_바라본다` FAIL.

둘 다 되돌리고 다시 전부 PASS 확인.

- [ ] **Step 9: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git status --short
git add Runtime/Scripts/Game/ArcheryCourseKind.cs Runtime/Scripts/Game/ArcheryCourseKind.cs.meta \
        Runtime/Scripts/Game/ArcheryRangeSettings.cs Runtime/Scripts/Game/ArcheryRangeSettings.cs.meta \
        Runtime/Scripts/Game/ArcheryCourse.cs Runtime/Scripts/Game/ArcheryCourse.cs.meta \
        Runtime/Scripts/Game/ArcheryConfig.cs \
        Tests/EditMode/ArcheryCourseTests.cs Tests/EditMode/ArcheryCourseTests.cs.meta
git commit -m "feat(archery): 과녁이 언제 어디 서는지를 정하는 코스 한 곳"

cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/Game/ArcheryConfigProvider.cs
git commit -m "feat(archery): 사거리 코스 값을 마스터데이터에서 읽는다"

cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/Game/ArcheryConfigProvider.cs
git commit -m "feat(archery): 사거리 코스 값을 마스터데이터에서 읽는다"
```

---

## Task 5: 소비처 셋을 코스 하나로 갈아끼운다

지금 `ArcheryWaveGenerator`를 직접 부르는 곳이 셋이다 — 서버 판정, 클라 과녁 그리기, 클라 화살 꽂기. 셋 다 `ArcheryCourse`를 보게 바꾼다. **이 태스크가 끝나도 화면은 한 군데도 안 바뀐다**(원형 맵만 있으므로) — 바뀌는 것은 "누구에게 묻는가"뿐이다.

> ⚠️ **등록과 소비는 같은 diff에 안 보인다.** DI 등록은 두 `ArcheryLifetimeScope`에, 소비는 각 시스템 파일에 있다. 등록을 빠뜨려도 **컴파일은 통과하고 방에 들어가야 터진다.** Step 5에서 두 스코프를 나란히 확인한다.

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryTargetView.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/TickSystems/ArcheryArrowStickSystem.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Tests/Editor/ArcheryHitSystemTests.cs`

**Interfaces:**
- Consumes: Task 4의 `ArcheryCourse`
- Produces:
  - `ArcheryHitSystem(ArcheryWorld world, EntityRegistry entityRegistry, WorldEventBuffer eventBuffer, ArcheryCourse course, ArcheryWaveState waveState, float tickInterval)` — `ArcheryConfig`·`IMatchSeed` 인자가 빠지고 `ArcheryCourse`가 들어온다
  - `ArcheryArrowStickSystem(ArcheryWorld world, ArcheryCourse course, ArcheryConsumed consumed, float tickInterval)`
  - `ArcheryTargetView(IRunner runner, IWorld world, ArcheryCourse course, ArcheryConsumed consumed)`
  - 두 스코프에 `ArcheryCourse` 싱글턴 등록

- [ ] **Step 1: 서버 판정이 코스에게 묻게 한다**

`ArcheryHitSystem`의 필드·생성자에서 `config`·`matchSeed`를 빼고 `course`를 넣는다. `Tick`의 앞부분만 바뀐다:

```csharp
        public void Tick(long tick, float deltaTime)
        {
            int step = course.IndexAt(tick, world.GameplayStartTick);
            if (step < 0)
            {
                return;   // 아직 출발 전
            }
            //  사거리 코스는 순서가 끝나면 더 이상 과녁이 없다(웨이브 맵은 StepCount가 0이라 안 걸린다).
            if (course.StepCount > 0 && step >= course.StepCount)
            {
                return;
            }

            if (step != waveState.WaveIndex)
            {
                course.Fill(targets, step, world.GameplayStartTick);
                // 지난 묶음의 과녁은 이미 사라졌다 — 기록을 들고 있을 이유가 없다.
                waveState.BeginWave(step);
            }

            CollectCandidates(tick);
            ApplyCandidates();
            ForgetOldArrows(tick);
        }
```

나머지(`CollectCandidates`/`ApplyCandidates`)는 **손대지 않는다.**

- [ ] **Step 2: 클라 화살 꽂기도 같게 바꾼다**

`ArcheryArrowStickSystem`에서 `config`·`matchSeed`를 `course`로 바꾸고, 웨이브를 묻는 곳을 `course.IndexAt(...)`, 채우는 곳을 `course.Fill(...)`로 바꾼다. 같은 두 줄짜리 가드(`step < 0`, `StepCount > 0 && step >= StepCount`)를 둔다.

- [ ] **Step 3: 클라 과녁 그리기도 같게 바꾼다**

`ArcheryTargetView`에서 `config`·`matchSeed`를 `course`로 바꾼다:

```csharp
            //  어느 단계인지는 틱 단위 사실이라 여기는 정수로 묻는다.
            int step = course.IndexAt((long)System.Math.Floor(renderTick), world.GameplayStartTick);

            targets.Clear();
            if (step >= 0 && (course.StepCount == 0 || step < course.StepCount))
            {
                course.Fill(targets, step, world.GameplayStartTick);
            }
```

> `renderTick`(소수 틱)과 `IsAlive`/`PositionAt`은 그대로 둔다 — 뷰가 쓰는 시각 규칙은 이 태스크의 주제가 아니다.

- [ ] **Step 4: 두 스코프에 코스를 등록한다**

`ArcheryLifetimeScope`(서버·클라 **양쪽**)의 `ConfigureGame`에서 `ArcheryConfig` 등록 바로 아래에 더한다:

```csharp
            //  과녁이 언제 어디 서는지를 정하는 한 곳. 명단은 매치 시작 시점의 것을 그대로 쓴다 —
            //  중간에 나간 사람이 있어도 과녁 주인이 밀리지 않게(스펙 6.2절).
            builder.Register<ArcheryCourse>(c => new ArcheryCourse(
                c.Resolve<ArcheryConfig>(),
                c.Resolve<IMatchSeed>(),
                c.Resolve<IRoomDataStore>().match.playerList,
                TickInterval), Lifetime.Singleton);
```

그리고 두 스코프의 수동 조립(`new ArcheryHitSystem(...)` / `new ArcheryArrowStickSystem(...)`)에서 `ArcheryConfig`·`IMatchSeed` 자리를 `c.Resolve<ArcheryCourse>()`로 바꾼다.

> 레이아웃은 **여기서 넘기지 않는다.** `ArcheryCourse`가 처음 쓰일 때(= 판이 시작한 뒤) 스스로 씬에서 읽는다. 서버 스코프는 빌드 콜백에서 판정 시스템을 **즉시** resolve하므로, 레이아웃을 여기서 읽으면 맵 씬이 뜨기 전이라 빈 값이 굳는다.

- [ ] **Step 5: 두 스코프를 나란히 놓고 확인한다**

```bash
grep -n "ArcheryCourse\|ArcheryConfig\|IMatchSeed" \
  "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryLifetimeScope.cs" \
  "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryLifetimeScope.cs"
```
Expected: 양쪽 다 `ArcheryCourse` 등록이 **한 줄씩** 있고, 판정/꽂기 조립이 그것을 받는다. 한쪽만 있으면 그 사이드가 방에 들어가자마자 `VContainerException`으로 죽는다.

- [ ] **Step 6: 서버 판정 시험의 픽스처를 코스로 바꾼다**

`ArcheryHitSystemTests`의 `Build`가 코스를 만들어 넣게 한다. **명단과 레이아웃을 인자로 받아 두면** Task 7의 사거리 시험이 같은 픽스처를 그대로 쓴다.

```csharp
        static Fixture Build(long startTick, ArcheryConfig config,
                             string[] owners = null,
                             System.Func<ArcheryRangeLayout> layoutSource = null)
        {
            var registry = new EntityRegistry();
            var world = new ArcheryWorld(registry, new WorldEventBuffer(), new ArcheryAimSystem(), TickInterval);
            world.GameplayStartTick = startTick;
            var waveState = new ArcheryWaveState();
            var course = new ArcheryCourse(
                config, new FixedSeed { Value = Seed },
                owners ?? new[] { "user-a" }, TickInterval,
                //  웨이브 판은 레인이 없다 — 빈 레이아웃이 정상이다.
                layoutSource ?? (() => ArcheryRangeLayout.From(new ArcheryLane[0])));

            return new Fixture
            {
                Registry = registry,
                World = world,
                Config = config,
                Course = course,
                WaveState = waveState,
                System = new ArcheryHitSystem(world, registry, world.EventBuffer, course, waveState, TickInterval),
            };
        }
```

`Fixture`에 코스를 들리고, 과녁도 코스에서 받게 한다 — **시험이 판정과 같은 길로 과녁을 얻어야** 둘이 갈리는 것을 잡는다:

```csharp
            public ArcheryCourse Course;

            public List<ArcheryTarget> TargetsOfWave(int wave)
            {
                var targets = new List<ArcheryTarget>();
                Course.Fill(targets, wave, World.GameplayStartTick);
                return targets;
            }
```


- [ ] **Step 7: 돌린다**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" \
  run_tests --mode EditMode --filter "Archery"
```
Expected: 전부 PASS. 특히 Task 1의 `원형_맵_과녁_목록이...`와 Task 4의 `웨이브_맵은_예전_생성기와_한_글자도_다르지_않다`가 PASS여야 한다.

- [ ] **Step 8: 클라가 컴파일되는지 본다**

클라 에디터는 사용자가 Play로 쓰고 있을 수 있다. **테스트는 걸지 않고** 컴파일만 본다:

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" recompile_status
```
Expected: `status: completed`, `failed: false`, `errors: []`. `triggered`에서 멈춰 있으면 에디터가 모달을 띄운 것이니 **손대지 말고 보고한다.**

- [ ] **Step 9: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git status --short
git add Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs Assets/Scripts/Game/ArcheryLifetimeScope.cs \
        Assets/Tests/Editor/ArcheryHitSystemTests.cs
git commit -m "refactor(archery): 과녁 목록을 코스 한 곳에서 받는다"

cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git status --short
git add Assets/Scripts/Game/ArcheryTargetView.cs Assets/Scripts/Game/TickSystems/ArcheryArrowStickSystem.cs \
        Assets/Scripts/Game/ArcheryLifetimeScope.cs
git commit -m "refactor(archery): 과녁 목록을 코스 한 곳에서 받는다"
```

---

## Task 6: 서버 룰 — 레인에 세우고, 순서가 끝나면 판을 끝낸다

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryRuleSystem.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryRangeValidation.cs`
- Create: `LeagueOfPhysical-Shared/Tests/EditMode/ArcheryRangeValidationTests.cs`

**Interfaces:**
- Consumes: Task 3의 `ArcheryRangeLayout` · Task 4의 `ArcheryCourse`·`ArcheryConfig.Range`
- Produces:
  - `static class ArcheryRangeValidation` — `static string Check(ArcheryRangeLayout layout, ArcheryRangeSettings range, int archerCount)` (문제가 없으면 null)
  - `ArcheryRuleSystem.MatchDurationTicks`가 `course.MatchDurationTicks`를 돌려준다

- [ ] **Step 1: 검증 규칙의 시험을 먼저 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/ArcheryRangeValidationTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 씬(자리)과 마스터데이터(거리·노출)는 서로를 모른다. 둘이 어긋나면 <b>에러 없이 판만 이상해진다</b> —
    /// 과녁이 선언보다 두 배 먼 데 서 있어도 아무 일도 안 일어난다. 판 시작에 한 번 대조한다.
    /// </summary>
    public class ArcheryRangeValidationTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) { Object.DestroyImmediate(go); }
            spawned.Clear();
        }

        private ArcheryRangeLayout Layout(int laneCount, params float[] distances)
        {
            var lanes = new List<ArcheryLane>();
            for (int i = 0; i < laneCount; i++)
            {
                var root = new GameObject("lane" + i);
                spawned.Add(root);
                root.transform.position = new Vector3(i * 4f, 0f, 0f);
                var lane = root.AddComponent<ArcheryLane>();
                lane.Order = i;
                lane.Stands = new Transform[distances.Length];
                for (int s = 0; s < distances.Length; s++)
                {
                    var stand = new GameObject("stand" + s);
                    stand.transform.SetParent(root.transform);
                    stand.transform.localPosition = new Vector3(0f, 1.3f, distances[s]);
                    lane.Stands[s] = stand.transform;
                }
                lanes.Add(lane);
            }
            return ArcheryRangeLayout.From(lanes);
        }

        private static ArcheryRangeSettings Settings(params float[] declaredDistances)
        {
            var stands = new List<ArcheryRangeStand>();
            for (int i = 0; i < declaredDistances.Length; i++)
            {
                stands.Add(new ArcheryRangeStand(i, declaredDistances[i], 200));
            }
            return new ArcheryRangeSettings(default, stands, 25);
        }

        [Test]
        public void 맞으면_아무_말도_안_한다()
        {
            Assert.IsNull(ArcheryRangeValidation.Check(Layout(2, 12f, 30f), Settings(12f, 30f), archerCount: 2));
        }

        [Test]
        public void 레인이_사수보다_적으면_말한다()
        {
            string problem = ArcheryRangeValidation.Check(Layout(1, 12f), Settings(12f), archerCount: 2);

            Assert.IsNotNull(problem, "레인이 모자라면 그 사수는 과녁이 영영 안 뜬다");
            StringAssert.Contains("레인", problem);
        }

        [Test]
        public void 자리_수가_데이터와_다르면_말한다()
        {
            //  씬에 자리가 둘인데 데이터는 셋을 말한다 — 화살 수와 과녁 수가 어긋난다.
            string problem = ArcheryRangeValidation.Check(Layout(2, 12f, 30f), Settings(12f, 30f, 60f), archerCount: 2);

            Assert.IsNotNull(problem);
            StringAssert.Contains("자리", problem);
        }

        [Test]
        public void 선언한_거리와_실제_자리가_멀면_말한다()
        {
            //  데이터는 30m라는데 씬은 45m에 세워 뒀다.
            string problem = ArcheryRangeValidation.Check(Layout(2, 12f, 45f), Settings(12f, 30f), archerCount: 2);

            Assert.IsNotNull(problem);
            StringAssert.Contains("30", problem, "어느 값이 어긋났는지 메시지에 있어야 고칠 수 있다");
        }

        [Test]
        public void 반올림_정도의_차이는_봐준다()
        {
            //  씬에서 손으로 놓은 자리다 — 센티미터 단위로 맞출 수는 없다.
            Assert.IsNull(ArcheryRangeValidation.Check(Layout(2, 12.4f, 29.7f), Settings(12f, 30f), archerCount: 2));
        }
    }
}
```

- [ ] **Step 2: 돌려서 실패를 확인한다**

Expected: 컴파일 실패 — `ArcheryRangeValidation`이 없다.

- [ ] **Step 3: 검증을 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryRangeValidation.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 씬과 마스터데이터가 같은 말을 하는지 판 시작에 한 번 대조한다.
    /// <b>둘은 서로를 모른다</b> — 엇갈려도 예외가 안 나고 판만 이상해지므로 여기서 잡는다.
    /// </summary>
    public static class ArcheryRangeValidation
    {
        /// <summary>손으로 놓는 자리라 이만큼(m)까지는 어긋나도 같은 것으로 본다.</summary>
        public const float DistanceToleranceMeters = 1.5f;

        /// <summary>문제가 있으면 사람이 읽을 한 문장, 없으면 null.</summary>
        public static string Check(ArcheryRangeLayout layout, ArcheryRangeSettings range, int archerCount)
        {
            if (layout == null || layout.IsEmpty)
            {
                return "맵에 ArcheryLane이 하나도 없다 — 사거리 맵인데 레인을 안 찍었거나 맵 씬이 안 떴다";
            }

            if (layout.Lanes.Count < archerCount)
            {
                return $"레인이 {layout.Lanes.Count}개인데 사수는 {archerCount}명이다 — "
                     + "남는 사수는 과녁이 영영 안 뜬다";
            }

            if (layout.StandCount != range.Stands.Count)
            {
                return $"씬의 과녁 자리가 {layout.StandCount}개인데 마스터데이터는 {range.Stands.Count}개를 말한다 "
                     + "— 화살 수(= 과녁 수)가 어긋난다";
            }

            for (int lane = 0; lane < layout.Lanes.Count; lane++)
            {
                for (int s = 0; s < range.Stands.Count; s++)
                {
                    var declared = range.Stands[s];
                    float actual = Vector3.Distance(
                        layout.Lanes[lane].ShooterPosition, layout.Lanes[lane].Stands[declared.StandIndex]);

                    if (Mathf.Abs(actual - declared.DistanceM) > DistanceToleranceMeters)
                    {
                        return $"레인 {lane}의 자리 {declared.StandIndex}가 실제로는 {actual:0.#}m인데 "
                             + $"마스터데이터는 {declared.DistanceM}m라고 적혀 있다 — "
                             + "배포 데이터 검사(사거리·노출 시간)가 틀린 거리로 판단하게 된다";
                    }
                }
            }

            return null;
        }
    }
}
```

- [ ] **Step 4: 룰이 레인에 세우고 코스로 끝내게 한다**

`ArcheryRuleSystem`에 `ArcheryConfig`·`ArcheryCourse`를 주입받고(생성자 인자 추가), `Initialize`를 바꾼다:

```csharp
        public void Initialize()
        {
            entityIdToUserId.Clear();

            var playerList = roomDataStore.match.playerList;

            //  사거리 맵은 사대가 레인 위에 있다. 원형 맵은 예전처럼 SpawnPoint를 쓴다 —
            //  맵이 값으로 방식을 고르고, 룰은 맵 이름을 모른다.
            var lanes = config.CourseKind == ArcheryCourseKind.Range
                ? ArcheryRangeLayout.FromOpenScenes()
                : null;

            if (lanes != null)
            {
                string problem = ArcheryRangeValidation.Check(lanes, config.Range, playerList.Length);
                if (problem != null)
                {
                    //  조용히 이상한 판을 시작하느니 여기서 끊는다 — 원인이 바로 보인다.
                    throw new System.InvalidOperationException("[Archery] " + problem);
                }
            }

            var slots = lanes == null
                ? SpawnPlacement.Arrange(UnityEngine.Object.FindObjectsByType<SpawnPoint>(
                      FindObjectsInactive.Include, FindObjectsSortMode.None))
                : null;
            if (slots != null && slots.Count == 0)
            {
                Debug.LogWarning("[Archery] 맵에 SpawnPoint가 없다 — 원 둘레에 등간격으로 세운다");
            }

            for (int i = 0; i < playerList.Length; i++)
            {
                Vector3 position;
                Vector3 rotation;

                if (lanes != null)
                {
                    //  자기 레인 사대에 서서 과녁 쪽을 본다.
                    position = lanes.Lanes[i].ShooterPosition;
                    var forward = lanes.Lanes[i].Forward;
                    rotation = new Vector3(0f, Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 0f);
                }
                else
                {
                    position = slots.Count > 0 ? slots[i % slots.Count] : RingSlot(i, playerList.Length);
                    // 가운데를 바라보게 세운다 — 사대는 원의 안쪽을 본다.
                    rotation = new Vector3(0f, Mathf.Atan2(-position.x, -position.z) * Mathf.Rad2Deg, 0f);
                }

                // ... 기존 Spawn 호출 그대로(position/rotation만 위 값으로) ...
            }
        }
```

그리고 판 길이를 코스에게 묻는다:

```csharp
        /// <summary>
        /// 이 판의 길이. <b>맵이 정한다</b> — 원형 맵은 데이터에 적힌 60초(3000틱),
        /// 사거리 맵은 순서가 다 지나가는 데 걸리는 시간이다(화살이 남아도 거기서 끝난다).
        /// </summary>
        public long MatchDurationTicks => course.MatchDurationTicks;
```

- [ ] **Step 5: 돌린다**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" \
  run_tests --mode EditMode --filter "Archery"
```
Expected: 새 `ArcheryRangeValidationTests` 5개 포함 전부 PASS.

- [ ] **Step 6: 이빨 확인**

`ArcheryRangeValidation.Check`의 거리 대조 루프를 통째로 지운다.
Expected: `선언한_거리와_실제_자리가_멀면_말한다` FAIL. 되돌린다.

- [ ] **Step 7: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git add Runtime/Scripts/Game/ArcheryRangeValidation.cs Runtime/Scripts/Game/ArcheryRangeValidation.cs.meta \
        Tests/EditMode/ArcheryRangeValidationTests.cs Tests/EditMode/ArcheryRangeValidationTests.cs.meta
git commit -m "feat(archery): 씬과 데이터가 같은 말을 하는지 판 시작에 대조한다"

cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/Game/ArcheryRuleSystem.cs Assets/Scripts/Game/ArcheryLifetimeScope.cs
git commit -m "feat(archery): 사수를 레인에 세우고 순서가 끝나면 판을 끝낸다"
```

---

## Task 7: 예약과 화살통 — 내 과녁은 내 것, 화살은 과녁 수만큼

여기서 사거리 맵이 **경기**가 된다. 두 규칙이 짝이다: 남의 과녁은 맞혀도 아무 일이 없고(태워 버리는 방해가 안 열린다), 화살은 과녁 수만큼만 있다(만회에 대가가 있다).

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryQuiver.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryAimSystem.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryWorld.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entity/ArcheryPlayerCreator.cs` · `LeagueOfPhysical-Client/Assets/Scripts/Entity/ArcheryPlayerCreator.cs`
- Create: `LeagueOfPhysical-Shared/Tests/EditMode/ArcheryQuiverTests.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Tests/Editor/ArcheryHitSystemTests.cs`

**Interfaces:**
- Consumes: Task 1의 `ArcheryHitRules.CanTake` · Task 4의 `ArcheryCourse.ArrowsPerArcher`
- Produces:
  - `class ArcheryQuiver : GameFramework.World.Component { public int Remaining; }` — **컴포넌트가 없으면 무제한**(원형 맵)
  - `ArcheryHitSystem`이 쏜 사람의 `Ownership.OwnerId`로 예약을 거른다

- [ ] **Step 1: 화살통 시험을 먼저 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/ArcheryQuiverTests.cs`:

```csharp
using GameFramework;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 화살 수. <b>컴포넌트가 없으면 무제한</b>이라 원형 맵은 아무것도 안 바뀐다 —
    /// 사거리 맵에서만 이 그릇이 붙는다.
    /// </summary>
    public class ArcheryQuiverTests
    {
        const float TickInterval = 0.02f;

        static Entity Archer(int arrows = -1)
        {
            var entity = new Entity("archer-1");
            entity.Add(new GameFramework.World.Transform { Position = Vector3.zero.ToNumerics() });
            entity.Add(new ArcheryAim());
            entity.Add(new InputBuffer());
            if (arrows >= 0)
            {
                entity.Add(new ArcheryQuiver { Remaining = arrows });
            }
            return entity;
        }

        static void Feed(Entity entity, bool drawing, bool release, float drawRatio = 1f)
        {
            entity.Get<InputBuffer>().Current = new InputCommand
            {
                AimYaw = 0f, AimPitch = 0f, Drawing = drawing, Release = release, DrawRatio = drawRatio,
            };
        }

        //  한 발 쏘는 데 필요한 두 틱: 당기고, 뗀다.
        static ArcheryShot? DrawAndRelease(Entity archer, ArcheryAimSystem system, long tick)
        {
            Feed(archer, drawing: true, release: false);
            system.Tick(archer, tick, TickInterval);
            Feed(archer, drawing: false, release: true);
            return system.Tick(archer, tick + 1, TickInterval);
        }

        [Test]
        public void 쏘면_화살이_하나_준다()
        {
            var archer = Archer(arrows: 3);
            var system = new ArcheryAimSystem();

            Assert.IsNotNull(DrawAndRelease(archer, system, 100));
            Assert.AreEqual(2, archer.Get<ArcheryQuiver>().Remaining);
        }

        [Test]
        public void 화살이_없으면_못_쏜다()
        {
            var archer = Archer(arrows: 0);
            var system = new ArcheryAimSystem();

            Assert.IsNull(DrawAndRelease(archer, system, 100), "화살이 0인데 화살이 나갔다");
            Assert.AreEqual(0, archer.Get<ArcheryQuiver>().Remaining, "0 아래로 내려가면 안 된다");
        }

        [Test]
        public void 화살통이_없으면_무제한이다()
        {
            var archer = Archer();   // 원형 맵 — 그릇 자체가 없다
            var system = new ArcheryAimSystem();

            for (int i = 0; i < 10; i++)
            {
                Assert.IsNotNull(DrawAndRelease(archer, system, 100 + i * 2), $"{i + 1}번째 발이 안 나갔다");
            }
        }

        [Test]
        public void 취소한_당김은_화살을_안_쓴다()
        {
            var archer = Archer(arrows: 3);
            var system = new ArcheryAimSystem();

            //  임계치를 못 넘고 뗐다 — 시위가 걸린 적이 없으니 화살도 안 나간다.
            Feed(archer, drawing: true, release: false, drawRatio: 0.05f);
            system.Tick(archer, 100, TickInterval);
            Feed(archer, drawing: false, release: true, drawRatio: 0.05f);

            Assert.IsNull(system.Tick(archer, 101, TickInterval));
            Assert.AreEqual(3, archer.Get<ArcheryQuiver>().Remaining, "안 나간 화살이 소모됐다");
        }

        [Test]
        public void 되감으면_화살_수도_되돌아온다()
        {
            var registry = new EntityRegistry();
            var world = new ArcheryWorld(registry, new WorldEventBuffer(), new ArcheryAimSystem(), TickInterval);

            var archer = Archer(arrows: 3);
            archer.Add(new Simulated());   // 되감기 대상은 내가 굴리는 몸뿐이다
            registry.Add(archer);

            world.SaveState(100);

            Feed(archer, drawing: true, release: false);
            world.Tick(101, TickInterval);
            Feed(archer, drawing: false, release: true);
            world.Tick(102, TickInterval);
            Assert.AreEqual(2, archer.Get<ArcheryQuiver>().Remaining, "쏘고도 안 줄었다 — 시험이 아무것도 재지 못한다");

            world.LoadState(100);

            Assert.AreEqual(3, archer.Get<ArcheryQuiver>().Remaining,
                "되감았는데 화살이 그대로다 — 예측이 빗나갈 때마다 화살이 사라진다");
        }
    }
}
```

- [ ] **Step 2: 돌려서 실패를 확인한다**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" \
  run_tests --mode EditMode --filter "ArcheryQuiverTests"
```
Expected: 컴파일 실패 — `ArcheryQuiver`가 없다.

- [ ] **Step 3: 화살통을 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryQuiver.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 남은 화살(데이터만). <b>이 컴포넌트가 붙어 있을 때만 화살에 한도가 있다</b> —
    /// 원형 맵은 안 붙이므로 예전처럼 무제한이다.
    ///
    /// <para>서버가 권위지만 클라도 <b>같은 규칙으로 세어</b> 예측한다(발사 자체가 이미 예측이다).
    /// 그래서 되감을 때 이 값도 같이 되돌아가야 한다 — <see cref="ArcheryWorld"/>가 저장한다.</para>
    /// </summary>
    public class ArcheryQuiver : GameFramework.World.Component
    {
        /// <summary>쏠 수 있는 화살 수. 0이면 더 못 쏜다.</summary>
        public int Remaining;
    }
}
```

- [ ] **Step 4: 조준 시스템이 화살을 센다**

`ArcheryAimSystem.Tick`에서 **시위가 걸린 것을 확인한 뒤**, 화살을 만들기 직전에 검사한다(취소된 당김은 화살을 안 쓰므로 순서가 중요하다):

```csharp
            //  화살이 없으면 시위를 걸었어도 안 나간다. 그릇이 없으면 무제한이다(원형 맵).
            var quiver = entity.Get<ArcheryQuiver>();
            if (quiver != null && quiver.Remaining <= 0)
            {
                aim.Drawing = false;
                return null;
            }

            float speed = SpeedFor(drawAtRelease);
            // ... 기존 origin·velocity 계산 그대로 ...

            if (quiver != null)
            {
                quiver.Remaining--;
            }

            aim.Drawing = false;
            return new ArcheryShot(entity.Id, tick, origin, velocity);
```

- [ ] **Step 5: 되감기 저장에 화살통을 넣는다**

`ArcheryWorld`의 `SavedState`에 `Quivers`를 더하고, `SaveGameState`/`LoadGameState`에서 조준값과 같은 방식으로 다룬다(원격 몸은 저장 대상이 아니라는 기존 가드 안쪽에 둔다):

```csharp
        private readonly struct SavedState
        {
            public readonly List<ArcheryShot> Shots;
            public readonly Dictionary<string, ArcheryAim> Aims;
            public readonly Dictionary<string, int> Quivers;

            public SavedState(List<ArcheryShot> shots, Dictionary<string, ArcheryAim> aims,
                              Dictionary<string, int> quivers)
            {
                Shots = shots;
                Aims = aims;
                Quivers = quivers;
            }
        }
```

`SaveGameState`에서 조준을 담는 자리 옆에:

```csharp
                var quiver = entity.Get<ArcheryQuiver>();
                if (quiver != null)
                {
                    quivers[entity.Id] = quiver.Remaining;
                }
```

`LoadGameState`에서:

```csharp
            foreach (var pair in state.Quivers)
            {
                var quiver = EntityRegistry.Get(pair.Key)?.Get<ArcheryQuiver>();
                if (quiver != null)
                {
                    quiver.Remaining = pair.Value;
                }
            }
```

- [ ] **Step 6: 두 크리에이터가 사거리 맵에서 화살통을 붙인다**

`ArcheryPlayerCreator`(서버·클라 **양쪽**)에 `ArcheryCourse`를 주입받고, `ArcheryScore` 다음 줄에 더한다:

```csharp
            //  사거리 맵은 화살이 과녁 수만큼이다. 웨이브 맵은 0을 돌려주므로 그릇을 안 붙인다 —
            //  붙이는 순간 한 발도 못 쏘게 되므로 이 조건이 곧 "원형 맵은 안 바뀐다"의 보증이다.
            int arrows = course.ArrowsPerArcher;
            if (arrows > 0)
            {
                worldEntity.Add(new ArcheryQuiver { Remaining = arrows });
            }
```

> **형제 크리에이터와 대조한다**: 다른 맵의 크리에이터들이 어떤 부품을 붙이는지 세어 보고, 내 것만 빠진 것이 없는지 본다.
> ```bash
> grep -c "Add(new" "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Entity/"*Creator.cs
> ```

- [ ] **Step 7: 판정이 주인을 가리게 한다**

`ArcheryHitSystem.CollectCandidates`에서, 화살 하나마다 **한 번** 쏜 사람의 userId를 찾고 후보를 거른다:

```csharp
                //  누가 쏜 화살인가. 예약된 과녁은 주인만 가져간다 —
                //  엔티티 id가 아니라 userId로 비교한다(과녁은 매치 시작 명단으로 예약되므로).
                string shooterUserId = entityRegistry.Get(shot.ShooterId)
                    ?.Get<GameFramework.World.Ownership>()?.OwnerId ?? string.Empty;

                for (int i = 0; i < targets.Count; i++)
                {
                    if (waveState.IsConsumed(targets[i].SlotIndex))
                    {
                        continue;
                    }

                    //  남의 과녁이면 여기서 끝난다 — 점수도 없고 과녁도 안 사라진다.
                    //  (사라지게 두면 남의 과녁을 태워 버리는 방해가 열린다.)
                    if (ArcheryHitRules.CanTake(targets[i], shooterUserId) == false)
                    {
                        continue;
                    }

                    // ... 기존 IsAlive·PositionAt·SegmentHitsTarget 그대로 ...
```

- [ ] **Step 8: 예약 시험을 서버 쪽에 쓴다**

`ArcheryHitSystemTests`에 사거리 판을 세우는 픽스처를 더한다. 기존 `Fixture`/`Build`/`ShotThrough`를 그대로 쓰되, 레인이 있는 씬과 사거리 설정만 새로 만든다.

```csharp
        //  레인 몇 개와 그 앞 과녁 자리 하나로 이루어진 임시 씬. EditMode에서도 GameObject는 만들 수 있다.
        sealed class RangeScene : System.IDisposable
        {
            private readonly List<GameObject> spawned = new List<GameObject>();

            public ArcheryRangeLayout Layout { get; }

            public RangeScene(int laneCount, float distance)
            {
                var lanes = new List<ArcheryLane>();
                for (int i = 0; i < laneCount; i++)
                {
                    var root = new GameObject("lane" + i);
                    spawned.Add(root);
                    //  레인을 10m씩 떼어 놓는다 — 옆 레인 과녁이 실수로 선분에 걸리지 않게.
                    root.transform.position = new Vector3(i * 10f, 0f, 0f);

                    var lane = root.AddComponent<ArcheryLane>();
                    lane.Order = i;

                    var stand = new GameObject("stand0");
                    stand.transform.SetParent(root.transform);
                    stand.transform.localPosition = new Vector3(0f, 1.3f, distance);
                    lane.Stands = new[] { stand.transform };

                    lanes.Add(lane);
                }
                Layout = ArcheryRangeLayout.From(lanes);
            }

            public void Dispose()
            {
                foreach (var go in spawned) { Object.DestroyImmediate(go); }
            }
        }

        //  자리 하나짜리 사거리 설정. 과녁은 제자리에 서 있으므로(솟지 않으므로) 쏘기가 쉽다.
        static ArcheryConfig RangeConfig()
        {
            var face = new ArcheryTargetKind(0.61f, 5, 0, false, ArcheryTargetShape.Face, null);
            var stands = new[] { new ArcheryRangeStand(0, 20f, 200) };

            return new ArcheryConfig(
                wavePeriodTicks: 88, minTargets: 2, maxTargets: 3,
                spawnRadius: 2f, spawnMinY: 2f, spawnMaxY: 6f, minSeparation: 1.0f,
                trapRatioMin: 0f, trapRatioMax: 0f,
                shakeFreeSeconds: 1f, shakeRampSeconds: 2f, shakeMaxDegrees: 3f,
                riseHeightMin: 1.2f, riseHeightMax: 2.4f, staggerTicks: 12, restTicks: 20,
                kinds: new[] { face },
                courseKind: ArcheryCourseKind.Range, matchDurationTicks: 0,
                range: new ArcheryRangeSettings(face, stands, 25));
        }

        static Fixture BuildRange(long startTick, RangeScene scene, params string[] owners)
        {
            return Build(startTick, RangeConfig(), owners, () => scene.Layout);
        }
```

`Build(long, ArcheryConfig, string[] owners, Func<ArcheryRangeLayout>)` 오버로드와 `Fixture.Course`·`TargetsOfWave`는 **Task 5에서 이미 만들어져 있다.** 여기서는 주인 있는 몸을 만드는 손잡이 하나만 더한다:

```csharp
            //  사거리 판은 "누가 쏜 화살인가"를 userId로 가린다 — 몸에 주인을 박아 둬야 한다.
            public Entity Archer(string id, string userId)
            {
                var entity = Archer(id);
                entity.Add(new Ownership(userId));
                return entity;
            }
```

그리고 시험 둘:

```csharp
        [Test]
        public void 남의_과녁은_맞혀도_아무_일이_없다()
        {
            using (var scene = new RangeScene(laneCount: 2, distance: 20f))
            {
                var f = BuildRange(StartTick, scene, "user-a", "user-b");
                f.Archer("e-a", "user-a");

                //  슬롯 1은 user-b의 과녁이다. user-a의 화살이 한가운데를 지나가게 쏜다.
                var theirs = f.TargetsOfWave(0)[1];
                f.World.IngestRemoteShot(ShotThrough("e-a", StartTick, theirs, 1.0f));
                f.System.Tick(StartTick + 1, TickInterval);

                Assert.AreEqual(0, f.ScoreOf("e-a"), "남의 과녁으로 점수가 났다");
                Assert.IsFalse(f.WaveState.IsConsumed(theirs.SlotIndex),
                    "남의 화살에 과녁이 사라졌다 — 태워 버리는 방해가 열린다");
            }
        }

        [Test]
        public void 주인이_맞히면_점수가_나고_과녁이_사라진다()
        {
            using (var scene = new RangeScene(laneCount: 2, distance: 20f))
            {
                var f = BuildRange(StartTick, scene, "user-a", "user-b");
                f.Archer("e-b", "user-b");

                //  바로 위 시험과 **같은 판, 같은 과녁, 같은 화살**이고 쏜 사람만 다르다.
                var theirs = f.TargetsOfWave(0)[1];
                f.World.IngestRemoteShot(ShotThrough("e-b", StartTick, theirs, 1.0f));
                f.System.Tick(StartTick + 1, TickInterval);

                Assert.Greater(f.ScoreOf("e-b"), 0, "주인이 맞혔는데 점수가 안 났다");
                Assert.IsTrue(f.WaveState.IsConsumed(theirs.SlotIndex));
            }
        }
```

> 두 시험이 **같은 판에서 쏜 사람만 바꾼다** — 그래야 갈라지는 것이 예약뿐임이 드러난다. 한쪽만 있으면 "원래 안 맞는 자리였다"와 구별되지 않는다.

- [ ] **Step 9: 돌린다**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" \
  run_tests --mode EditMode --filter "Archery"
```
Expected: 전부 PASS. 원형 맵 시험(주인 없는 과녁)이 그대로 통과하는 것이 "안 바뀌었다"의 증거다.

- [ ] **Step 10: 이빨 확인 둘**

1. `CanTake` 호출을 지운다 → `남의_과녁은_맞혀도_아무_일이_없다` FAIL.
2. `quiver.Remaining--`를 지운다 → `쏘면_화살이_하나_준다`·`화살이_없으면_못_쏜다` FAIL.

둘 다 되돌리고 전부 PASS 확인.

- [ ] **Step 11: 클라 컴파일 확인 후 커밋**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" recompile_status

cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git add Runtime/Scripts/Game/ArcheryQuiver.cs Runtime/Scripts/Game/ArcheryQuiver.cs.meta \
        Runtime/Scripts/Game/ArcheryAimSystem.cs Runtime/Scripts/Game/ArcheryWorld.cs \
        Tests/EditMode/ArcheryQuiverTests.cs Tests/EditMode/ArcheryQuiverTests.cs.meta
git commit -m "feat(archery): 화살은 과녁 수만큼, 되감으면 같이 되돌아온다"

cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs Assets/Scripts/Entity/ArcheryPlayerCreator.cs \
        Assets/Tests/Editor/ArcheryHitSystemTests.cs
git commit -m "feat(archery): 예약된 과녁은 주인만 가져간다"

cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/Entity/ArcheryPlayerCreator.cs
git commit -m "feat(archery): 사거리 맵에서 화살통을 붙인다"
```

---

## Task 8: 클라 — 판 과녁을 그리고 남은 화살을 보여 준다

**보이는 것이 곧 맞는 것**이어야 한다. 판 과녁은 반경만큼의 원판을 사수 쪽으로 세워 그리고, 띠는 데이터의 비율 그대로 색을 나눈다. 여기서 크기나 방향이 판정과 어긋나면 "안 보이는데 맞는다"가 된다.

> 이 태스크에는 자동 시험이 없다. 화면은 눈으로 봐야 하고, **그 확인이 Task 9의 실물 점검**이다. 대신 **판정과 같은 값에서 그린다**(반경·중심·방향·띠 비율을 목록에서 그대로 읽는다) — 그림용 숫자를 따로 두지 않는 것이 어긋남을 막는 방법이다.

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryTargetView.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/UI/ArcheryPad/ArcheryPadViewModel.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/UI/ArcheryPad/ArcheryPadView.cs`
- Modify: `LeagueOfPhysical-Client/Assets/UI/ArcheryPad/ArcheryPad.uxml` · `ArcheryPad.uss`

**Interfaces:**
- Consumes: Task 1의 `ArcheryTarget.{Shape,Bands,Facing,OwnerUserId}` · Task 7의 `ArcheryQuiver`
- Produces: 없음(화면)

- [ ] **Step 1: 과녁 뷰가 모양에 따라 다르게 그리게 한다**

`ArcheryTargetView`에서 과녁 하나를 그리는 자리를 모양으로 가른다. 공은 **지금 코드 그대로** 두고 판만 새로 만든다:

```csharp
        //  판은 띠마다 원판을 하나씩 겹쳐 그린다. 가장 바깥이 뒤, 가운데가 앞이다.
        //  유니티 실린더는 y축으로 2만큼 길고 반지름이 0.5라, 지름 배율은 (반경 x 2)이고
        //  y 배율이 곧 두께의 절반이다.
        private GameObject BuildFace(in ArcheryTarget target)
        {
            var root = new GameObject("archery-face");
            var bands = target.Bands;
            int count = bands == null || bands.Count == 0 ? 1 : bands.Count;

            for (int i = count - 1; i >= 0; i--)
            {
                float ratio = bands == null || bands.Count == 0 ? 1f : bands[i].OuterRatio;
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(disc.GetComponent<Collider>());   // 그림일 뿐이다 — 판정은 서버가 한다
                disc.transform.SetParent(root.transform, worldPositionStays: false);

                //  안쪽 띠일수록 사수 쪽으로 살짝 당겨 앞에 오게 한다(z-파이팅 방지).
                disc.transform.localPosition = new Vector3(0f, 0.01f * (count - i), 0f);
                float diameter = target.Radius * 2f * ratio;
                disc.transform.localScale = new Vector3(diameter, 0.02f, diameter);

                //  띠 색은 아래 SetBandMaterials가 매 프레임 정한다(내 것/남의 것이 바뀔 수 있어서).
                disc.name = "band" + i;
            }

            return root;
        }

        //  양궁 과녁면의 색차례(가운데 금색 → 빨강 → 파랑 → 검정 → 흰색). 띠가 더 많으면 돌려 쓴다.
        private static readonly Color[] BandColors =
        {
            new Color(1f, 0.85f, 0.1f), new Color(0.9f, 0.15f, 0.15f),
            new Color(0.15f, 0.35f, 0.9f), new Color(0.1f, 0.1f, 0.1f), Color.white,
        };
```

`BandMaterial`은 지금 `TargetMaterial()`/`TrapMaterial()`이 하는 것과 같은 방식으로 **색깔별 한 장씩 캐시**한다 — 과녁마다 새로 만들면 재질 인스턴스가 계속 쌓인다:

```csharp
        //  색깔당 한 장. 흐린 판(남의 것)은 같은 색을 어둡게 만든 별도 한 장이다 —
        //  불투명 재질에 알파만 낮추면 아무 변화가 없어서(투명 모드가 아니다) 명도로 가른다.
        private readonly Dictionary<(int band, bool dimmed), Material> _bandMaterials
            = new Dictionary<(int, bool), Material>();

        private Material BandMaterial(int bandIndex, bool dimmed)
        {
            var key = (bandIndex % BandColors.Length, dimmed);
            if (_bandMaterials.TryGetValue(key, out var material) == false || material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Color color = BandColors[key.Item1];
                material = new Material(shader) { color = dimmed ? color * 0.35f : color };
                _bandMaterials[key] = material;
            }
            return material;
        }
```

> `Dispose`에서 이 사전의 재질도 같이 `Destroy`한다 — 지금 두 장을 정리하는 그 자리에 한 줄을 더한다.

- [ ] **Step 2: 판을 사수 쪽으로 세운다**

만든 뒤 매 프레임 자리를 잡는 곳에서, 판이면 방향까지 맞춘다:

지금 코드는 그려 둔 오브젝트를 `sphere`라는 이름의 지역 변수로 들고 있다. 공만 있던 시절의 이름이니 `drawn`으로 바꾸고(같은 변수다), 자리를 잡는 자리에서 모양으로 가른다:

```csharp
                drawn.transform.position = ArcheryTargetMotion.PositionAt(targets[i], renderTick, (float)interval);

                if (targets[i].Shape == ArcheryTargetShape.Face)
                {
                    //  실린더의 축(y)을 과녁이 보는 쪽에 맞춘다 — 그래야 원판의 앞면이 사수를 향한다.
                    //  판정이 쓰는 Facing을 그대로 쓴다(그림용 각도를 따로 두면 어긋난다).
                    drawn.transform.rotation = Quaternion.FromToRotation(Vector3.up, targets[i].Facing);
                }
                else
                {
                    //  공은 예전 그대로 — 보이는 크기가 곧 맞는 크기다.
                    drawn.transform.localScale = Vector3.one * (targets[i].Radius * 2f);
                }
```

> 공을 만들고 색을 고르는 기존 코드(`CreatePrimitive(Sphere)`·`TargetMaterial`/`TrapMaterial`·크기)는 **한 줄도 건드리지 않는다.** 원형 맵이 한 픽셀도 안 바뀌어야 한다.

- [ ] **Step 3: 남의 과녁은 흐리게**

`IUserDataStore`를 주입받아 내 것과 남의 것을 가른다:

판을 그린 자리 바로 뒤에서 띠 재질을 정한다. 내 것인지 남의 것인지는 **틱마다 달라질 수 있는 값이 아니지만**(주인은 고정) 재질은 매 프레임 정해도 캐시라 싸다:

```csharp
        //  남의 레인도 보이지만 쏠 이유가 없다는 것이 한눈에 읽혀야 한다.
        //  주인이 없으면(원형 맵) 전부 내 것처럼 밝게 — 예전 그대로다.
        private void SetBandMaterials(GameObject face, in ArcheryTarget target)
        {
            bool mine = string.IsNullOrEmpty(target.OwnerUserId)
                     || target.OwnerUserId == userDataStore.user.id;

            for (int i = 0; i < face.transform.childCount; i++)
            {
                var renderer = face.transform.GetChild(i).GetComponent<Renderer>();
                if (renderer == null)
                {
                    continue;
                }
                //  자식 이름이 "band{i}"라 그 번호가 곧 띠 번호다(BuildFace가 그렇게 붙인다).
                int band = int.Parse(renderer.name.Substring("band".Length));
                renderer.sharedMaterial = BandMaterial(band, dimmed: mine == false);
            }
        }
```

`ArcheryTargetView` 생성자에 `IUserDataStore userDataStore`를 더한다(등록은 이미 앱 스코프에 있다).

- [ ] **Step 4: 남은 화살을 화면에 띄운다**

`ArcheryPadViewModel`에 더한다:

```csharp
        /// <summary>남은 화살. <b>−1이면 무제한</b>(원형 맵)이라 화면이 아예 안 띄운다.</summary>
        public int ArrowsLeft
        {
            get
            {
                var entity = entityRegistry.Get(playerContext.entityId);
                var quiver = entity?.Get<ArcheryQuiver>();
                return quiver?.Remaining ?? -1;
            }
        }
```

`ArcheryPad.uxml`의 점수 라벨 아래에:

```xml
        <ui:Label name="arrows" class="archery-arrows" text="" picking-mode="Ignore" />
```

`ArcheryPad.uss`에 `archery-score`를 본떠 `archery-arrows`를 더한다(점수 아래에 놓이게 `top` 값만 다르게).

`ArcheryPadView.OnOpen`에서 라벨을 잡고, 점수를 갱신하는 같은 스케줄러에서 갱신한다:

```csharp
            _arrows = Root.Q<Label>("arrows");
            ...
                //  무제한인 맵에서는 아예 안 보이게 한다 — 늘 같은 숫자가 떠 있으면 눈만 시끄럽다.
                int left = _viewModel.ArrowsLeft;
                _arrows.style.display = left >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (left >= 0)
                {
                    _arrows.text = $"화살 {left}";
                }
```

- [ ] **Step 5: 컴파일만 확인한다**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" recompile_status
```
Expected: `failed: false`, `errors: []`. (클라 에디터가 Play 중일 수 있으므로 **테스트는 걸지 않는다.**)

- [ ] **Step 6: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git status --short
git add Assets/Scripts/Game/ArcheryTargetView.cs \
        Assets/Scripts/UI/ArcheryPad/ArcheryPadViewModel.cs Assets/Scripts/UI/ArcheryPad/ArcheryPadView.cs \
        Assets/UI/ArcheryPad/ArcheryPad.uxml Assets/UI/ArcheryPad/ArcheryPad.uss
git commit -m "feat(archery): 판 과녁을 띠 색으로 그리고 남은 화살을 띄운다"
```

---

## Task 9: 맵 씬 — 레인을 세우고 어드레서블에 올린다

씬은 값을 주는 쪽이다. 여기서 만드는 것은 **뼈대(blockout)** 다 — 바닥, 레인 넷, 레인 사이 벽, 거리별 과녁 자리. 모양새는 나중에 아트가 올린다.

> ⚠️ 맵 씬은 **아트 서브모듈**(`Assets/Art/`)에 살고, 게임에는 **어드레서블**로 실려 간다. 씬 파일만 만들고 어드레서블에 안 올리면 매치는 시작되는데 맵이 안 뜬다 — 로더가 주소를 못 찾아 예외를 던진다.

**Files:**
- Create: `LeagueOfPhysical-Art`(클라 체크아웃) `Assets/Art/Scenes/ArcheryRangeMap.unity`
- Modify: `LeagueOfPhysical-Client/Assets/AddressableAssetsData/AssetGroups/Scene.asset`
- Modify: 클라·서버 레포의 아트 서브모듈 포인터

**Interfaces:**
- Consumes: Task 3의 `ArcheryLane` · Task 2의 `TbArcheryRange` 거리(12/20/30/45/65/90m)
- Produces: 주소 `Assets/Art/Scenes/ArcheryRangeMap.unity` (TbMap id=6이 가리키는 그 경로)

- [ ] **Step 1: 씬을 스크립트로 만든다**

손으로 놓으면 거리가 데이터와 어긋나기 쉽다(그러면 Task 6의 대조가 판을 막는다). 스크립트로 정확히 놓는다. 아래를 스크래치패드에 `build-archery-range-scene.cs`로 저장해 `eval_file`로 돌린다:

```csharp
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
    UnityEditor.SceneManagement.NewSceneMode.Single);

//  거리는 #ArcheryRange.xlsx(map_id=6)와 **같은 값**이어야 한다. 어긋나면 판 시작에 막힌다.
float[] distances = { 12f, 20f, 30f, 45f, 65f, 90f };
int laneCount = 4;
float laneSpacing = 8f;
float targetHeight = 1.3f;   // 과녁 중심 높이 — 선 사람 가슴께
float backstopZ = 100f;

UnityEngine.GameObject Cube(string name, Vector3 pos, Vector3 size, UnityEngine.Transform parent)
{
    var go = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.position = pos;
    go.transform.localScale = size;
    return go;
}

var root = new UnityEngine.GameObject("ArcheryRange");

//  바닥 — 레인 전체를 덮는다.
float width = laneSpacing * laneCount;
Cube("Ground", new Vector3(width * 0.5f - laneSpacing * 0.5f, -0.5f, backstopZ * 0.5f),
     new Vector3(width + 4f, 1f, backstopZ + 10f), root.transform);

//  뒷벽 — 빗나간 화살이 허공으로 날아가는 것보다 벽에 꽂히는 편이 거리감이 읽힌다.
Cube("Backstop", new Vector3(width * 0.5f - laneSpacing * 0.5f, 3f, backstopZ),
     new Vector3(width + 4f, 6f, 1f), root.transform);

for (int i = 0; i < laneCount; i++)
{
    float x = i * laneSpacing;
    var laneGo = new UnityEngine.GameObject($"Lane{i}");
    laneGo.transform.SetParent(root.transform, false);
    laneGo.transform.position = new Vector3(x, 0f, 0f);
    //  기본 방향(+z)이 곧 과녁 쪽이다.

    var lane = laneGo.AddComponent<LOP.ArcheryLane>();
    lane.Order = i;

    var stands = new UnityEngine.Transform[distances.Length];
    for (int s = 0; s < distances.Length; s++)
    {
        var stand = new UnityEngine.GameObject($"Stand{s}");
        stand.transform.SetParent(laneGo.transform, false);
        stand.transform.localPosition = new Vector3(0f, targetHeight, distances[s]);
        stands[s] = stand.transform;

        //  자리 표식 — 과녁이 없을 때도 어디에 설지 보이게 얇은 기둥을 세운다.
        Cube($"StandPost{s}", new Vector3(x, targetHeight * 0.5f, distances[s]),
             new Vector3(0.1f, targetHeight, 0.1f), laneGo.transform);
    }
    lane.Stands = stands;

    //  레인 사이 벽 — 남의 레인이 안 보이면 남의 과녁을 쏠 유혹 자체가 없다(스펙 5절).
    if (i > 0)
    {
        Cube($"Divider{i}", new Vector3(x - laneSpacing * 0.5f, 1.5f, backstopZ * 0.5f),
             new Vector3(0.2f, 3f, backstopZ), root.transform);
    }
}

//  빛이 없으면 전부 새까맣게 보인다. 맵은 Additive로 얹히므로 **씬의 렌더 설정(안개·앰비언트)은
//  안 쓰인다** — 활성 씬(게임 모드 씬)의 것이 쓰인다. 그래서 여기엔 직사광만 둔다.
var lightGo = new UnityEngine.GameObject("Directional Light");
lightGo.transform.SetParent(root.transform, false);
lightGo.transform.rotation = UnityEngine.Quaternion.Euler(50f, -30f, 0f);
var light = lightGo.AddComponent<UnityEngine.Light>();
light.type = UnityEngine.LightType.Directional;
light.intensity = 1f;

string path = "Assets/Art/Scenes/ArcheryRangeMap.unity";
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, path);
return "saved " + path;
```

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" \
  eval_file --file "<스크래치패드>/build-archery-range-scene.cs"
```

> ⚠️ **클라 에디터가 Play 중이면 씬을 새로 못 만든다.** `recompile_status`로 컴파일 초록을 먼저 보고, `eval`로 `UnityEditor.EditorApplication.isPlaying`이 `False`인지 확인한다. Play 중이면 **여기서 멈추고 보고한다**(사용자 앱이므로 임의로 정지시키지 않는다).

- [ ] **Step 2: 씬이 제대로 섰는지 값으로 확인한다**

```bash
unity command --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" \
  eval --code "var l = LOP.ArcheryRangeLayout.FromOpenScenes(); var sb = new System.Text.StringBuilder(); sb.Append(l.Lanes.Count).Append(\" lanes, \").Append(l.StandCount).Append(\" stands\"); for (int i = 0; i < l.Lanes.Count; i++) { for (int s = 0; s < l.Lanes[i].Stands.Count; s++) { sb.Append(\" | \").Append(UnityEngine.Vector3.Distance(l.Lanes[i].ShooterPosition, l.Lanes[i].Stands[s]).ToString(\"0.0\")); } } return sb.ToString();"
```
Expected: `4 lanes, 6 stands` 그리고 거리가 레인마다 `12.0 20.0 30.0 45.0 65.0 90.0`. 데이터(`#ArcheryRange.xlsx`)와 한 값이라도 1.5m 넘게 어긋나면 Task 6의 대조가 판 시작을 막는다.

- [ ] **Step 3: 어드레서블에 올린다**

```csharp
var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
var group = settings.FindGroup("Scene");
string path = "Assets/Art/Scenes/ArcheryRangeMap.unity";
string guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
var entry = settings.CreateOrMoveEntry(guid, group);
//  주소는 경로 그대로 — TbMap.scene_path가 그 문자열로 찾는다.
entry.address = path;
UnityEditor.AssetDatabase.SaveAssets();
return entry.address + " -> " + group.Name;
```

Expected: `Assets/Art/Scenes/ArcheryRangeMap.unity -> Scene`.

확인:
```bash
grep -n "ArcheryRangeMap" \
  "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/AddressableAssetsData/AssetGroups/Scene.asset"
```
Expected: `m_Address: Assets/Art/Scenes/ArcheryRangeMap.unity` 한 줄.

- [ ] **Step 4: 아트 서브모듈부터 커밋한다**

아트는 **별도 저장소**다. 씬 파일은 거기 있고, 클라·서버 레포에는 "어느 커밋을 볼지"만 적힌다.

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Art"
git status --short
git add Scenes/ArcheryRangeMap.unity Scenes/ArcheryRangeMap.unity.meta
git commit -m "feat(archery): 양궁 사거리 맵 뼈대"
```

> 아트 저장소 push와 양쪽 레포의 포인터 갱신은 **컨트롤러가 머지할 때** 한다(워크트리 밖으로 나가는 일).

- [ ] **Step 5: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git status --short
git add Assets/AddressableAssetsData/AssetGroups/Scene.asset
git commit -m "feat(archery): 사거리 맵 씬을 어드레서블에 올린다"
```

> 아트 서브모듈 포인터(`Assets/Art`)는 **일부러 여기서 커밋하지 않는다** — 사용자의 워킹트리에 늘 떠 있는 로컬 픽스처와 섞이기 쉬운 자리다. 컨트롤러가 머지 때 따로 다룬다.

---

## 마지막 — 머지·배포·실물 확인 (컨트롤러가 한다)

실행자는 여기까지 오지 않는다. 아래는 **워크트리 밖으로 나가는 일**이라 컨트롤러가 직접 한다.

### 1) 머지 순서

의존 방향의 아래쪽부터 올린다. 레포마다 `CLAUDE.md`의 푸시 규약(원격 main 리베이스 → `--ff-only` → `--no-ff` 머지)을 **한 줄씩 결과를 확인하며** 밟는다.

1. **LeagueOfPhysical-Art** (씬)
2. **GameFramework** — 이번엔 변경 없음(건드렸다면 순서상 여기)
3. **LeagueOfPhysical-Shared** (값 그릇·레이아웃·코스·화살통·검증)
4. **infrastructure** (엑셀)
5. **MasterData-Client** · **MasterData-Server** (생성물)
6. **lop-backend** (매치메이킹 마스터데이터 — 아래 ⚠️)
7. **LeagueOfPhysical-Server** (룰·판정·크리에이터)
8. **LeagueOfPhysical-Client** (뷰·HUD·어드레서블·크리에이터 + 아트 포인터)

> ⚠️ **백엔드도 걸린다(계획을 쓸 때 놓쳤던 것).** `TbMap`은 Luban group이 `c,s,m`이라 `gen.sh`가
> 매치메이킹 산출물(`apps/matchmaking-server/master_data/tbmap.json`)에도 쓴다. 매치메이킹은
> `ticketRequestValidation`에서 `TbMap.get(mapId)`로 **티켓을 검증**하므로, 이 줄이 없으면
> 맵 6을 고른 요청이 `INVALID_MAP`으로 거절된다 — 서버는 멀쩡해 보이고 그 맵만 매칭이 안 된다.
> (맵 5를 추가할 때도 같은 커밋이 있었다: `7811f7a`.) 커밋 `cd692d4`.

> **아트 마운트는 클라 레포에만 있다**(확인 2026-09-18: 서버 레포엔 `.gitmodules`도 `Assets/Art`도 없다).
> 서버(파드)는 맵을 **어드레서블로만** 받는다. 그래서 순서가 중요하다 — **아트를 먼저 main에 올리고**,
> 그 다음 클라 레포가 새 포인터를 커밋해야 한다. 거꾸로 하면 포인터가 *아직 없는 커밋*을 가리킨다.

### 2) 배포는 세 갈래고, 서로를 안 데려온다

| 갈래 | 무엇이 | 어떻게 |
|---|---|---|
| **게임 서버 이미지** | 서버 코드(룰·판정) | 서버 레포 배포 워크플로 → 이미지 태그 = 서버 main SHA → infra GitOps가 bump → ArgoCD 롤아웃 |
| **콘텐츠(어드레서블)** | **맵 씬** | 클라 레포 `content-deploy`. **`gameserver`(Linux)와 `standalone-windows`를 둘 다** 돌린다 — 파드와 에디터가 서로 다른 카탈로그를 읽는다 |
| **마스터데이터** | 표 값 | 게임 서버 이미지에 실려 간다(패키지) — 이미지가 새로 구워져야 반영된다 |
| **매치메이킹 서버** | `tbmap.json`(맵 6) | `backend-deploy` 워크플로, app=`matchmaking-server`. **게임 서버보다 먼저** 올린다 — 이게 없으면 맵을 고르는 순간 티켓이 거절된다 |

```bash
gh workflow run backend-deploy -f app=matchmaking-server     # 먼저
gh workflow run content-deploy -f target=gameserver
gh workflow run content-deploy -f target=standalone-windows
```

배포가 **실제로 닿았는지**는 워크플로 상태가 아니라 클러스터에서 확인한다:

```bash
kubectl get pods -n default | grep room-server
kubectl exec -n default <room-server-pod> -- printenv GAME_SERVER_IMAGE
```
Expected: 서버 main SHA와 같다. configmap이 바뀐 뒤에도 **파드는 한동안 옛 값을 들고 있다** — 파드까지 봐야 끝이다.

### 3) 실물 확인

**클라 둘을 켜야 한다**(사용자 몫). 환경은 `local-k8s`.

**① 원형 맵 한 판 — 아무것도 안 바뀌었는지**
- 과녁이 예전처럼 솟았다 떨어진다(공 모양, 노랑/파랑)
- 점수가 +1/+2/+4, 함정 −3/−5/−10
- 60초에 끝난다
- 콘솔에 새 예외가 없다

**② 사거리 맵 한 판 — 새로 되는지**
- 사수가 **자기 레인**에 서고 과녁 쪽을 본다
- 거리별 과녁이 **정해진 순서대로 하나씩** 서고 노출 시간 뒤 사라진다
- **내 과녁만** 점수가 난다 — 옆 레인 과녁을 맞혀 보고 점수가 0인지, 과녁이 그대로 서 있는지 확인
- 맞힌 자리에 따라 점수가 갈린다(가운데 10 / 중간 8 / 바깥 5)
- **남은 화살**이 쏠 때마다 줄고, 0이 되면 더 안 나간다
- **순서가 끝나면 판이 끝난다**(화살이 남아 있어도)
- 두 클라가 **같은 순서**를 본다 — 둘 다에서 "몇 번째에 어느 거리였는지"를 대조

**③ 갈림이 의심되면** 두 클라가 계산한 과녁 목록을 찍어 맞춰 본다. 판이 도는 중에 각 클라에서:

```csharp
//  eval_file로 돌린다. 매치 씨앗은 콘솔의 "[MatchSeed] received ..." 줄에 찍혀 있다.
var scope = UnityEngine.Object.FindFirstObjectByType<LOP.ArcheryLifetimeScope>();
var course = (LOP.ArcheryCourse)scope.Container.Resolve(typeof(LOP.ArcheryCourse));
var world = (GameFramework.World.IWorld)scope.Container.Resolve(typeof(GameFramework.World.IWorld));

var sb = new System.Text.StringBuilder();
var list = new System.Collections.Generic.List<LOP.ArcheryTarget>();
for (int step = 0; step < course.StepCount; step++)
{
    course.Fill(list, step, world.GameplayStartTick);
    foreach (var t in list)
    {
        //  부동소수는 비트로 찍는다 — 반올림이 차이를 숨기지 못하게.
        sb.Append(step).Append('/').Append(t.SlotIndex).Append(' ')
          .Append(System.BitConverter.SingleToInt32Bits(t.Origin.x).ToString("X8")).Append(',')
          .Append(System.BitConverter.SingleToInt32Bits(t.Origin.z).ToString("X8")).Append(' ')
          .Append(t.SpawnTick).Append(' ').Append(t.OwnerUserId).Append('
');
    }
}
return sb.ToString();
```

두 클라의 출력이 **글자 단위로 같아야 한다.** 다르면 씨앗·명단·레이아웃·마스터데이터 중 하나가 갈린 것이다 — 그 넷을 차례로 찍어 본다.

### 4) 쳐 보며 맞출 값 (스펙 §11 — 지금 정하지 않은 것)

첫 값은 넣어 뒀다. 한 판 쳐 보고 아래를 조정한다 — **전부 엑셀 한 줄**이라 코드를 안 고친다.

| 값 | 지금 | 어디 |
|---|---|---|
| 거리 여섯 | 12/20/30/45/65/90m | `#ArcheryRange.xlsx` + 씬(둘이 같아야 한다) |
| 노출 시간 | 4~6초 | `#ArcheryRange.xlsx` |
| 단계 사이 틈 | 1초 | `#ArcheryConfig.xlsx` `step_gap_ticks` |
| 띠 점수 | 10/8/5 (0.2/0.5/1.0) | `#ArcheryRing.xlsx` |
| 과녁 크기 | 반경 0.61m(=122cm 표준) | `#ArcheryTarget.xlsx` |
| 레인 수·간격 | 4개, 8m | 씬 |

> 거리를 바꾸면 **씬과 엑셀을 같이** 고쳐야 한다. 안 그러면 판 시작에 막힌다(그게 의도다 — 조용히 어긋나는 것보다 낫다).

---

## 이 계획이 남기는 위험

솔직하게 적어 둔다. 실물 확인에서 여기부터 본다.

1. **화살 수는 클·서가 각자 센다**(와이어 없음, 스펙 §6.3). 입력이 유실되면 서버가 그 발을 못 봐서 **클라가 서버보다 화살이 적어진다.** 방향은 안전한 쪽(클라가 먼저 멈춘다)이고, 어긋나면 그때 스냅샷에 실어 보낸다.
2. **카메라 값이 원형 맵 기준으로 따라온다.** 90m 과녁을 겨누려면 지금 상하 한계각·시야가 모자랄 수 있다. 실물 확인에서 "먼 과녁이 화면에 안 들어온다"가 나오면 그때 맵별 값으로 뺀다.
3. **레인이 사수보다 적으면 판이 시작되지 않는다**(Task 6이 막는다). 씬에 레인 넷을 뒀으므로 5인 이상 매칭이면 막힌다 — `TbGameMode`의 활쏘기 최대 인원이 8이므로, 사거리 맵으로 5인 이상이 잡히면 그 판은 시작 자체가 안 된다. **첫 실물 확인은 2인으로 한다.** 레인을 여덟로 늘리는 것은 씬 한 줄이다.
4. **과녁 자리 표식(기둥)이 화살에 걸릴 수 있다.** 콜라이더가 붙은 큐브라 판정과는 무관하지만(판정은 우리 선분 계산이다) 화살 **그림**이 거기 꽂혀 보일 수 있다. 거슬리면 표식의 콜라이더를 지운다.
5. **띠 그림과 판정 반경이 어긋나면 눈에 안 띈다.** Task 8이 판정과 같은 값에서 그리도록 했지만, 실물에서 "가운데를 맞혔는데 5점"이 나오면 **그림이 아니라 띠 순서**(중심→바깥)를 먼저 의심한다.
