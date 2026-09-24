# 활쏘기 한 발 승부 — 슬라이스 1 (규칙 + 해설 자막) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 네 명이 같은 자리에서 같은 과녁에 라운드마다 한 발씩 쏘고, 서버가 라운드마다 가운데에 가까운 순서로 순위 점수를 주고, 클라가 바람·라운드·결과 목록·해설 자막을 보여 주는 `ShootOff` 모드를 끝까지 돌게 한다.

**Architecture:** 기존 사거리(`Range`) 코스 위에 새 코스 방식 `ShootOff`를 얹는다. 과녁·궤적·바람·순위 계산은 LOP-Shared의 순수 코드로 두고(클·서 동일), 라운드 마감과 점수 적립은 서버의 새 틱 시스템이 한다. 결과는 기존 `WorldEventToC` oneof에 한 줄을 더한 연출 사건으로 내려가고, 클라는 그 사건과 코스 데이터만으로 HUD와 해설을 그린다. 화면용 좌우 배치는 클라 뷰에만 있다.

**Tech Stack:** Unity 6 / C#, VContainer, MessagePipe, UI Toolkit(UXML/USS), Protobuf 3.28(proto → C#), Luban(Excel → `.cs`/`.bytes`), NUnit EditMode.

**Spec:** `docs/superpowers/specs/2026-09-24-archery-shoot-off-design.md` (클라 레포). 계획과 스펙이 어긋나면 스펙이 이긴다.

## Global Constraints

- 답변·주석은 한국어, 주석은 최소로 쉽게. 코드로 자명한 것엔 달지 않는다(`CLAUDE.md` "코드 주석").
- **시뮬 로직은 구체 클래스를 공유한다**(인터페이스 seam 금지). 인터페이스는 사이드가 달라야 하는 I/O에만.
- LOP 측 파일에서 World 타입은 풀네임(`GameFramework.World.Entity`)으로 쓴다 — `using GameFramework.World;` 추가 금지(Shared의 `noEngineReferences` 어셈블리 내부 제외).
- **클라·서버 EditMode를 둘 다 돌린다**(Shared를 고치면 둘 다). 결과는 콘솔의 "Run finished" 줄과 TestResults.xml로 읽는다 — `test_status` 스냅샷은 낡은 값을 준다. 새 시험은 **이름으로** 통과를 확인한다.
- 컴파일 확인은 `unity` CLI를 우선 쓴다(`--project-path` 필수). `recompile_status`의 `up_to_date`는 재컴파일을 안 했다는 뜻이다 — 콘솔 CS 에러를 시각과 대조한다.
- **`.meta`는 Unity가 만든 것만 커밋한다. 손으로 만들지 않는다.** 새 파일을 만들면 에디터 재스캔 후 생긴 `.meta`를 같이 커밋한다.
- **`git add -A` / `git commit -a` 금지.** 경로를 지정해 스테이지하고 `git diff --cached --stat`로 확인한다.
- **커밋하지 않는 로컬 픽스처:** 클라 `Assets/Art`, `Assets/UI/Theme/Fonts/Jua-Regular SDF.asset`, `ProjectSettings/DynamicsManager.asset`, `ProjectSettings/PackageManagerSettings.asset` / 서버 `Assets/AddressableAssetsData`, `Assets/DefaultVolumeProfile.asset`, `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`, `Assets/URPDefaultResources/*` / `output/`, 추적 안 되는 docs.
- **main 직접 커밋 금지.** Shared·Server·MasterData·infrastructure·lop-backend는 **메인 체크아웃에서** 브랜치를 판다(에디터가 메인 체크아웃을 보므로 — 로컬 픽스처는 `git stash push -u`로 뺐다가 `pop`). 레포마다 `feature/archery-shoot-off` 브랜치(원격 main에서 딴다 — 딴 직후 `git rev-list --left-right --count origin/main...HEAD`로 `0 0` 확인). 클라는 기존 워크트리 `../wt-client-shootoff`(브랜치 `docs/archery-shoot-off`)를 그대로 쓴다.
- 커밋 메시지 끝:
  ```
  Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
  ```
- 푸시는 레포마다 `CLAUDE.md` "푸시 규약"의 여섯 줄을 **한 줄씩** 확인하며. force push 금지. **푸시는 사용자 확인 후에만**(Task 12).
- 틱 간격은 0.02초(50Hz). 라운드 수치(스펙 §2.1): 쏘기 5초 = `exposure_ticks` 250, 소개+결과 4초 = `step_gap_ticks` 200, 12라운드.
- 순위 점수: `(인원 − 1 − 순위) × 배수`, 못 맞힘 = 순위 `인원−1`·0점, 동거리 = 공동 순위(높은 점수). 순위는 0부터.
- 바람 부호: **양수 = 사수가 보는 기준 오른쪽으로 민다.** 오른쪽 축 = `Cross(up, 사수 앞쪽)` = `ArcheryImpactLog.ToFaceOffset`과 같은 규약.
- 해설 문장은 클라 상수. 관중 관련 문장(관중석 명중)은 넣지 않는다(슬라이스 2).

## Review Focus

1. **바람이 클·서에서 다르게 채워지는 경우** — 한쪽만 레이아웃(맵 씬)이 안 떠 `WindAt`이 0을 주면 서버 판정과 클라 화면의 궤적이 갈린다. 발사하는 틱에는 과녁이 이미 서 있어야 하므로 레이아웃이 떠 있는 게 정상이지만, "레이아웃이 없으면 0"이 조용히 통과하는지 시험으로 못박는다(Task 4).
2. **라운드 마감이 두 번 또는 0번 일어나는 경우** — 서버 틱이 마감 틱을 건너뛰거나(프레임 히칭으로 틱을 몰아 돌 때) 같은 틱을 두 번 볼 때. `>=` 비교 + "다음에 닫을 라운드" 커서로 정확히 한 번(Task 6).
3. **판 도중 나간 사람** — 몸이 레지스트리에서 사라진 사수는 순위 인원에서 빠진다(점수 줄 곳이 없다). 인원이 줄면 1등 점수도 줄어든다 — 스펙 식 그대로이며 의도다. 시험으로 못박는다(Task 6).
4. **사거리 맵이 그대로인가** — `IsLaned` 도입으로 `Range`의 채우기·순서 섞기·검증·걷기가 한 줄이라도 바뀌면 이미 배포된 모드가 깨진다. 기존 사거리 시험 전부가 초록이어야 하고, `Range`에서 `IsShared == false`를 시험으로 확인한다(Task 4).
5. **한 점에 네 명을 세웠을 때 몸이 밀리는가** — 걷기가 꺼져 있어도 다른 경로(스폰 시 depenetrate 등)가 밀어낼 수 있다. 두 클라 실측으로 확인한다(Task 12).

---

## 파일 구조

**LeagueOfPhysical-Shared** (`Runtime/Scripts/Game/`, 시험은 `Tests/EditMode/`)

| 파일 | 할 일 |
|---|---|
| `ArcheryShootOffRanking.cs` (새) | 착탄 목록 → 순위·점수. 순수 함수 |
| `ArcheryFaceCoords.cs` (새) | 착탄점 → 과녁면 좌표. 클라 `ArcheryImpactLog.ToFaceOffset`의 몸통을 옮겨 온다 |
| `ArcheryTarget.cs` | `IsShared` |
| `ArcheryHitRules.cs` | 공유 과녁 규칙 |
| `ArcheryShot.cs` / `ArcheryTrajectory.cs` | 바람 |
| `ArcheryCourseKind.cs` | `ShootOff = 2` |
| `ArcheryRangeSettings.cs` | `ArcheryRangeStand.WindMps2`, `PointsMultiplier` |
| `ArcheryCourse.cs` | `IsLaned`, `IsShootOff`, ShootOff 채우기·순서·화살·걷기, `WindAt`, `MultiplierAt`, `RoundCloseTick`, `SharedLane` |
| `ArcheryTargetMotion.cs` | 좌우축 계산을 공개(`RightAxisFor`) |
| `ArcheryWorld.cs` | 쏠 때 샷에 바람 채우기 |
| `ArcheryRangeValidation.cs` | 방식별 검사 |
| `ArcheryRoundResultEvent.cs` (새) | 라운드 결과 사건 |
| `Protos/ArcheryRoundResultToC.proto` (새), `Protos/WorldEventToC.proto` | oneof 한 줄 |
| `Runtime.Generated/Scripts/WorldEventWire.cs` | 변환 |

**LeagueOfPhysical-Server**

| 파일 | 할 일 |
|---|---|
| `Assets/Scripts/Game/ArcheryRoundLog.cs` (새) | 라운드마다 사수별 착탄 기록 |
| `Assets/Scripts/Game/TickSystems/ArcheryRoundSystem.cs` (새) | 라운드 마감 → 순위 점수 적립 + 사건 |
| `Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs` | 공유 과녁 착탄 기록, 띠 점수를 사건에만 |
| `Assets/Scripts/Game/ArcheryRuleSystem.cs` | ShootOff면 모두 공유 레인 사대 |
| `Assets/Scripts/Game/ArcheryConfigProvider.cs` | 새 방식·열 |
| `Assets/Scripts/Game/ArcheryLifetimeScope.cs` | 등록 |
| `Assets/Tests/Editor/ArcheryRoundSystemTests.cs` (새), `ArcheryHitSystemTests.cs` | 시험 |

**LeagueOfPhysical-Client** (워크트리 `../wt-client-shootoff`)

| 파일 | 할 일 |
|---|---|
| `Assets/Scripts/Game/ArcheryConfigProvider.cs` | 새 방식·열 (서버와 쌍둥이) |
| `Assets/Scripts/Game/ArcheryImpactLog.cs` | `ToFaceOffset`을 Shared로 위임 |
| `Assets/Scripts/Game/MessageHandler/ArcheryRemoteShotHandler.cs` | 받은 샷에 바람 |
| `Assets/Scripts/Game/ArcheryShootOffLineup.cs` (새) | 화면용 좌우 간격 계산(순수) |
| `Assets/Scripts/Game/ArcheryShootOffLineupView.cs` (새) | 남의 몸을 좌우로 옮겨 그림 |
| `Assets/Scripts/Game/ArcheryArrowView.cs` | 남의 화살 출발점 간격 |
| `Assets/Scripts/Game/ArcheryCommentary.cs` (새) | 해설 문장 표 + 우선순위 고르기(순수) |
| `Assets/Scripts/Game/ArcheryShootOffNarrator.cs` (새) | 결과 사건 → 해설 종류(순수, 연승·지난 순위 기억) |
| `Assets/Scripts/UI/ArcheryShootOff/ArcheryShootOffHudViewModel.cs` / `...View.cs` (새) | HUD |
| `Assets/UI/ArcheryShootOff/ArcheryShootOffHud.uxml` / `.uss` (새) | HUD 레이아웃 |
| `Assets/Scripts/Game/ArcheryHudCoordinator.cs`, `ArcheryLifetimeScope.cs` | 열기·등록 |
| `Assets/Tests/Editor/ArcheryShootOffLineupTests.cs`, `ArcheryCommentaryTests.cs`, `ArcheryShootOffNarratorTests.cs` (새) | 시험 |

**infrastructure** `table/Datas/#ArcheryRange.xlsx`, `#ArcheryConfig.xlsx`, `#Map.xlsx` → `gen.sh`가 MasterData-Client/Server와 `lop-backend/apps/matchmaking-server/master_data/`를 다시 만든다.

---

### Task 1: 라운드 순위 계산 (Shared, 순수)

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryShootOffRanking.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/ArcheryShootOffRankingTests.cs`

**Interfaces:**
- Produces:
  - `public readonly struct ArcheryRoundShot { string ShooterId; bool Hit; Vector2 FaceOffset; float Distance; }` + 생성자 `(string shooterId, bool hit, Vector2 faceOffset, float distance)`
  - `public readonly struct ArcheryRoundPlacement { string ShooterId; bool Hit; Vector2 FaceOffset; float Distance; int Rank; int Points; }` + 생성자 같은 순서
  - `public static List<ArcheryRoundPlacement> ArcheryShootOffRanking.Rank(IReadOnlyList<ArcheryRoundShot> shots, int multiplier)` — 결과는 순위 오름차순, 같은 순위는 `ShooterId` 서수 오름차순. `FaceOffset`·`Distance`는 **미터**.

- [ ] **Step 1: 실패하는 시험을 쓴다**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryShootOffRankingTests
    {
        private static ArcheryRoundShot Hit(string id, float distance)
            => new ArcheryRoundShot(id, true, new Vector2(distance, 0f), distance);

        private static ArcheryRoundShot Miss(string id)
            => new ArcheryRoundShot(id, false, Vector2.zero, 0f);

        private static ArcheryRoundPlacement Of(List<ArcheryRoundPlacement> result, string id)
            => result.Find(p => p.ShooterId == id);

        [Test]
        public void 네_명이면_가까운_순서로_3_2_1_0점()
        {
            var result = ArcheryShootOffRanking.Rank(new[]
            {
                Hit("c", 0.30f), Hit("a", 0.05f), Hit("d", 0.50f), Hit("b", 0.10f),
            }, multiplier: 1);

            Assert.AreEqual(new[] { "a", "b", "c", "d" }, result.ConvertAll(p => p.ShooterId).ToArray());
            Assert.AreEqual(new[] { 3, 2, 1, 0 }, result.ConvertAll(p => p.Points).ToArray());
            Assert.AreEqual(new[] { 0, 1, 2, 3 }, result.ConvertAll(p => p.Rank).ToArray());
        }

        [Test]
        public void 거리가_같으면_공동_순위이고_둘_다_높은_점수()
        {
            var result = ArcheryShootOffRanking.Rank(new[]
            {
                Hit("a", 0.10f), Hit("b", 0.10f), Hit("c", 0.20f), Hit("d", 0.30f),
            }, 1);

            Assert.AreEqual(3, Of(result, "a").Points);
            Assert.AreEqual(3, Of(result, "b").Points);
            Assert.AreEqual(0, Of(result, "b").Rank);
            Assert.AreEqual(2, Of(result, "c").Rank);
            Assert.AreEqual(1, Of(result, "c").Points);
        }

        [Test]
        public void 못_맞힌_사람은_맞힌_사람_뒤이고_모두_0점()
        {
            var result = ArcheryShootOffRanking.Rank(new[]
            {
                Miss("a"), Hit("b", 0.40f), Miss("c"), Hit("d", 0.20f),
            }, 1);

            Assert.AreEqual(new[] { "d", "b", "a", "c" }, result.ConvertAll(p => p.ShooterId).ToArray());
            Assert.AreEqual(3, Of(result, "d").Points);
            Assert.AreEqual(2, Of(result, "b").Points);
            Assert.AreEqual(0, Of(result, "a").Points);
            Assert.AreEqual(0, Of(result, "c").Points);
            Assert.AreEqual(3, Of(result, "a").Rank);   // 공동 꼴찌 = 인원 − 1
            Assert.AreEqual(3, Of(result, "c").Rank);
        }

        [Test]
        public void 전원_못_맞히면_전원_0점()
        {
            var result = ArcheryShootOffRanking.Rank(new[] { Miss("a"), Miss("b") }, 2);
            Assert.IsTrue(result.TrueForAll(p => p.Points == 0 && p.Rank == 1));
        }

        [Test]
        public void 혼자면_맞혀도_0점()
        {
            var result = ArcheryShootOffRanking.Rank(new[] { Hit("a", 0f) }, 2);
            Assert.AreEqual(0, result[0].Points);
            Assert.AreEqual(0, result[0].Rank);
        }

        [Test]
        public void 배수_2면_점수가_두_배()
        {
            var result = ArcheryShootOffRanking.Rank(new[]
            {
                Hit("a", 0.1f), Hit("b", 0.2f), Hit("c", 0.3f), Hit("d", 0.4f),
            }, 2);
            Assert.AreEqual(new[] { 6, 4, 2, 0 }, result.ConvertAll(p => p.Points).ToArray());
        }

        [Test]
        public void 빈_목록이면_빈_결과()
        {
            Assert.AreEqual(0, ArcheryShootOffRanking.Rank(new ArcheryRoundShot[0], 1).Count);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다** — Shared를 참조하는 클라 에디터에서 EditMode `ArcheryShootOffRankingTests` 실행. 기대: 컴파일 에러(`ArcheryShootOffRanking` 없음).

- [ ] **Step 3: 구현한다**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>한 라운드에서 한 사수가 남긴 결과. 거리·좌표는 미터다.</summary>
    public readonly struct ArcheryRoundShot
    {
        public readonly string ShooterId;
        public readonly bool Hit;
        public readonly Vector2 FaceOffset;
        public readonly float Distance;

        public ArcheryRoundShot(string shooterId, bool hit, Vector2 faceOffset, float distance)
        {
            ShooterId = shooterId;
            Hit = hit;
            FaceOffset = faceOffset;
            Distance = distance;
        }
    }

    /// <summary>순위가 매겨진 결과. <see cref="Rank"/>는 0이 1등이다.</summary>
    public readonly struct ArcheryRoundPlacement
    {
        public readonly string ShooterId;
        public readonly bool Hit;
        public readonly Vector2 FaceOffset;
        public readonly float Distance;
        public readonly int Rank;
        public readonly int Points;

        public ArcheryRoundPlacement(string shooterId, bool hit, Vector2 faceOffset, float distance,
                                     int rank, int points)
        {
            ShooterId = shooterId;
            Hit = hit;
            FaceOffset = faceOffset;
            Distance = distance;
            Rank = rank;
            Points = points;
        }
    }

    /// <summary>
    /// 한 발 승부의 라운드 순위. 가운데에 가까운 순서로 <c>(인원 − 1 − 순위) × 배수</c>점을 준다.
    /// 못 맞힌 사람은 맞힌 사람 모두의 뒤, 공동 꼴찌로 0점이다.
    /// </summary>
    public static class ArcheryShootOffRanking
    {
        public static List<ArcheryRoundPlacement> Rank(IReadOnlyList<ArcheryRoundShot> shots, int multiplier)
        {
            var sorted = new List<ArcheryRoundShot>(shots);
            //  결과 순서가 입력 순서(엔티티 순회)에 기대지 않게 id로 끝까지 가른다.
            sorted.Sort((a, b) =>
            {
                if (a.Hit != b.Hit)
                {
                    return a.Hit ? -1 : 1;
                }
                int byDistance = a.Hit ? a.Distance.CompareTo(b.Distance) : 0;
                return byDistance != 0 ? byDistance : string.CompareOrdinal(a.ShooterId, b.ShooterId);
            });

            int count = sorted.Count;
            var result = new List<ArcheryRoundPlacement>(count);
            for (int i = 0; i < count; i++)
            {
                var shot = sorted[i];
                int rank;
                int points;
                if (shot.Hit)
                {
                    //  공동 순위: 나보다 확실히 가까운 사람 수가 내 순위다.
                    rank = 0;
                    while (rank < i && sorted[rank].Distance < shot.Distance)
                    {
                        rank++;
                    }
                    points = (count - 1 - rank) * multiplier;
                }
                else
                {
                    rank = count - 1;
                    points = 0;
                }
                result.Add(new ArcheryRoundPlacement(shot.ShooterId, shot.Hit, shot.FaceOffset,
                                                     shot.Distance, rank, points));
            }
            return result;
        }
    }
}
```

- [ ] **Step 4: 통과를 확인한다** — 같은 시험. 기대: 7개 모두 PASS(이름으로 확인).
- [ ] **Step 5: 커밋한다** (Shared, `feature/archery-shoot-off`). 에디터 재스캔 뒤 생긴 `.meta` 둘을 함께.

```bash
git add Runtime/Scripts/Game/ArcheryShootOffRanking.cs Runtime/Scripts/Game/ArcheryShootOffRanking.cs.meta \
        Tests/EditMode/ArcheryShootOffRankingTests.cs Tests/EditMode/ArcheryShootOffRankingTests.cs.meta
git commit -m "feat(archery): 한 발 승부의 라운드 순위 계산"
```

---

### Task 2: 공유 과녁 + 과녁면 좌표 (Shared)

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryTarget.cs` (필드·생성자 끝에 `bool isShared = false`)
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryHitRules.cs` (`CanTake`, `ConsumedOnHit`)
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryFaceCoords.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryImpactLog.cs:82-99` (몸통을 위임)
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/ArcheryHitRulesTests.cs`(추가), `ArcheryFaceCoordsTests.cs`(새)

**Interfaces:**
- Produces:
  - `ArcheryTarget.IsShared` (bool). 생성자 마지막 선택 인자 `bool isShared = false` — 기존 호출부는 그대로 컴파일된다.
  - `ArcheryHitRules.CanTake`: `IsShared`면 참. `ArcheryHitRules.ConsumedOnHit`: `IsShared`면 거짓.
  - `public static Vector2 ArcheryFaceCoords.ToFaceOffset(Vector3 offsetFromTarget, Vector3 facing, float radius)` — 클라의 기존 식 그대로(radius로 나눔). **반지름 1을 넣으면 미터**가 나온다.

- [ ] **Step 1: 실패하는 시험을 쓴다** — `ArcheryHitRulesTests.cs`에 추가:

```csharp
        private static ArcheryTarget SharedFace(string owner = "")
            => new ArcheryTarget(0, 0, Vector3.zero, 0f, 0, 0.6f, 5, false, ArcheryTargetShape.Face,
                                 null, Vector3.back, 5f, owner, 0f, 0f, isShared: true);

        [Test]
        public void 공유_과녁은_누구나_맞힌다()
        {
            Assert.IsTrue(ArcheryHitRules.CanTake(SharedFace(), "anyone"));
            Assert.IsTrue(ArcheryHitRules.CanTake(SharedFace(), "someone-else"));
        }

        [Test]
        public void 공유_과녁은_맞아도_안_사라진다()
        {
            Assert.IsFalse(ArcheryHitRules.ConsumedOnHit(SharedFace()));
        }

        [Test]
        public void 공유가_아니면_예전_규칙_그대로()
        {
            var unowned = new ArcheryTarget(0, 0, Vector3.zero, 0f, 0, 0.6f, 5, false,
                                            ArcheryTargetShape.Face, null, Vector3.back, 5f, "", 0f, 0f);
            Assert.IsFalse(unowned.IsShared);
            Assert.IsTrue(ArcheryHitRules.ConsumedOnHit(unowned));
        }
```

`ArcheryFaceCoordsTests.cs`(새):

```csharp
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryFaceCoordsTests
    {
        //  사수는 +z를 보고, 과녁은 사수 쪽(−z)을 본다.
        [Test]
        public void 사수_기준_오른쪽_위가_양수()
        {
            var face = ArcheryFaceCoords.ToFaceOffset(new Vector3(0.3f, 0.1f, 0f), Vector3.back, 1f);
            Assert.AreEqual(0.3f, face.x, 1e-5f);
            Assert.AreEqual(0.1f, face.y, 1e-5f);
        }

        [Test]
        public void 반지름으로_나눈다()
        {
            var face = ArcheryFaceCoords.ToFaceOffset(new Vector3(0.3f, 0f, 0f), Vector3.back, 0.6f);
            Assert.AreEqual(0.5f, face.x, 1e-5f);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다** — 기대: 컴파일 에러(`isShared`, `ArcheryFaceCoords` 없음).

- [ ] **Step 3: 구현한다**

`ArcheryTarget.cs` — 필드와 생성자:

```csharp
        /// <summary>
        /// 모두가 같이 쏘는 과녁인가(한 발 승부). 누구나 맞히고, 맞아도 안 사라진다.
        /// </summary>
        public readonly bool IsShared;
```

생성자 시그니처 끝에 `, bool isShared = false`를 더하고 본문 끝에 `IsShared = isShared;`.

`ArcheryHitRules.cs`:

```csharp
        public static bool CanTake(in ArcheryTarget target, string shooterUserId)
        {
            return target.IsShared
                || string.IsNullOrEmpty(target.OwnerUserId)
                || target.OwnerUserId == shooterUserId;
        }

        public static bool ConsumedOnHit(in ArcheryTarget target)
        {
            //  공유 과녁은 모두의 화살이 한 과녁에 꽂혀야 비교가 된다.
            return target.IsShared == false && string.IsNullOrEmpty(target.OwnerUserId);
        }
```

(두 함수의 기존 `<summary>`에 "공유 과녁(한 발 승부)은 누구나 맞히고 안 사라진다" 한 줄을 더한다.)

`ArcheryFaceCoords.cs`: 클라 `ArcheryImpactLog.ToFaceOffset`의 `<summary>`와 몸통을 그대로 옮긴다.

```csharp
using UnityEngine;

namespace LOP
{
    public static class ArcheryFaceCoords
    {
        /// <summary>
        /// 꽂힌 자리를 <b>과녁 면 위의 좌표</b>로 바꾼다 — 사수가 보는 기준으로 x는 오른쪽 y는 위,
        /// <paramref name="radius"/>로 나눈 값이다(1을 넣으면 미터).
        /// <para><paramref name="facing"/>은 과녁이 바라보는 쪽, 즉 <b>사수를 향한</b> 방향이다.</para>
        /// </summary>
        public static Vector2 ToFaceOffset(Vector3 offsetFromTarget, Vector3 facing, float radius)
        {
            if (radius <= 0f)
            {
                return Vector2.zero;
            }

            Vector3 shooterForward = -facing.normalized;
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, shooterForward).normalized;
            if (right.sqrMagnitude < 0.5f)
            {
                return Vector2.zero;   // 과녁이 바로 위나 아래를 보는 축퇴 — 이 게임엔 없다
            }

            return new Vector2(Vector3.Dot(offsetFromTarget, right),
                               Vector3.Dot(offsetFromTarget, up)) / radius;
        }
    }
}
```

클라 `ArcheryImpactLog.ToFaceOffset` 몸통을 `return ArcheryFaceCoords.ToFaceOffset(offsetFromTarget, facing, radius);` 한 줄로 바꾼다(공개 API와 기존 `ArcheryImpactLogTests`는 그대로).

- [ ] **Step 4: 통과를 확인한다** — Shared `ArcheryHitRulesTests`·`ArcheryFaceCoordsTests` 전부, 클라 `ArcheryImpactLogTests` 전부 PASS.
- [ ] **Step 5: 커밋한다** — Shared(`ArcheryTarget.cs`, `ArcheryHitRules.cs`, `ArcheryFaceCoords.cs`+`.meta`, 두 시험 파일+새 `.meta`). 클라 `ArcheryImpactLog.cs`는 클라 워크트리에서 따로 커밋: `refactor(archery): 과녁면 좌표 계산을 공유 코드로 위임`.

```bash
git commit -m "feat(archery): 공유 과녁 — 누구나 맞히고 안 사라진다"
```

---

### Task 3: 바람이 실린 궤적 (Shared)

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryShot.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryTrajectory.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/ArcheryTrajectoryTests.cs`(추가)

**Interfaces:**
- Produces:
  - `ArcheryShot.Wind` (Vector3, 가속도 m/s²). 생성자 끝 선택 인자 `Vector3 wind = default`.
  - `ArcheryShot WithWind(Vector3 wind)` — 나머지는 같고 바람만 바꾼 새 값.
  - `ArcheryTrajectory.PositionAt`: `+ 0.5 * Wind * t²`. `VelocityAt`: `+ Wind * t`.

- [ ] **Step 1: 실패하는 시험을 쓴다**

```csharp
        [Test]
        public void 바람이_없으면_궤적이_예전과_같다()
        {
            var still = new ArcheryShot("a", 0, Vector3.zero, new Vector3(0f, 5f, 60f));
            var calm = still.WithWind(Vector3.zero);
            for (float t = 0f; t < 1f; t += 0.1f)
            {
                Assert.AreEqual(ArcheryTrajectory.PositionAt(still, t), ArcheryTrajectory.PositionAt(calm, t));
            }
        }

        [Test]
        public void 바람은_반_a_t제곱만큼_민다()
        {
            var shot = new ArcheryShot("a", 0, Vector3.zero, new Vector3(0f, 0f, 60f), new Vector3(10f, 0f, 0f));
            var p = ArcheryTrajectory.PositionAt(shot, 0.5f);
            Assert.AreEqual(0.5f * 10f * 0.25f, p.x, 1e-5f);
            Assert.AreEqual(10f * 0.5f, ArcheryTrajectory.VelocityAt(shot, 0.5f).x, 1e-5f);
        }

        [Test]
        public void WithWind는_바람만_바꾼다()
        {
            var shot = new ArcheryShot("a", 7, Vector3.one, Vector3.forward);
            var windy = shot.WithWind(Vector3.right);
            Assert.AreEqual("a", windy.ShooterId);
            Assert.AreEqual(7, windy.FireTick);
            Assert.AreEqual(Vector3.one, windy.Origin);
            Assert.AreEqual(Vector3.forward, windy.Velocity);
            Assert.AreEqual(Vector3.right, windy.Wind);
        }
```

- [ ] **Step 2: 실패를 확인한다** — 기대: 컴파일 에러(`Wind`, `WithWind` 없음).

- [ ] **Step 3: 구현한다**

`ArcheryShot.cs`:

```csharp
        /// <summary>
        /// 이 화살을 옆으로 미는 바람(가속도, m/s²). 쏜 틱의 코스에서 정해진다 —
        /// 발사 틱만 알면 양쪽이 같은 값을 채우므로 통신하지 않는다.
        /// </summary>
        public readonly Vector3 Wind;

        public ArcheryShot(string shooterId, long fireTick, Vector3 origin, Vector3 velocity,
                           Vector3 wind = default)
        {
            ShooterId = shooterId;
            FireTick = fireTick;
            Origin = origin;
            Velocity = velocity;
            Wind = wind;
        }

        public ArcheryShot WithWind(Vector3 wind) => new ArcheryShot(ShooterId, FireTick, Origin, Velocity, wind);
```

`ArcheryTrajectory.cs`:

```csharp
        public static Vector3 PositionAt(in ArcheryShot shot, float secondsSinceFire)
        {
            float t = secondsSinceFire;
            return shot.Origin
                 + shot.Velocity * t
                 + (shot.Wind + new Vector3(0f, -Gravity, 0f)) * (0.5f * t * t);
        }

        public static Vector3 VelocityAt(in ArcheryShot shot, float secondsSinceFire)
        {
            return shot.Velocity + (shot.Wind + new Vector3(0f, -Gravity, 0f)) * secondsSinceFire;
        }
```

⚠️ 첫 시험 "예전과 같다"는 `==`(Unity `Vector3` 근사 비교)로 본다. 식을 합쳐 쓰면 마지막 비트가 달라질 수 있는데, 두 샷이 **같은 식**을 지나므로 서로는 정확히 같다. 기존 `ArcheryTrajectoryTests`(중력 값 비교)가 그대로 통과하는지 꼭 확인한다 — 안 되면 중력 항과 바람 항을 따로 더하는 원래 모양으로 되돌린다.

- [ ] **Step 4: 통과를 확인한다** — `ArcheryTrajectoryTests` 전부 + `ArcheryHitTestTests`·`ArcheryWorldTests` 회귀 없음.
- [ ] **Step 5: 커밋한다** — `feat(archery): 궤적에 바람을 싣는다`

---

### Task 4: 코스 방식 `ShootOff` + 바람을 쏠 때 채우기 (Shared)

**Files:**
- Modify: `ArcheryCourseKind.cs`, `ArcheryRangeSettings.cs`(`ArcheryRangeStand`), `ArcheryCourse.cs`, `ArcheryTargetMotion.cs`, `ArcheryWorld.cs`, `ArcheryRangeValidation.cs` (모두 `LeagueOfPhysical-Shared/Runtime/Scripts/Game/`)
- Test: `Tests/EditMode/ArcheryCourseTests.cs`, `ArcheryRangeValidationTests.cs`, `ArcheryWorldTests.cs`(추가)

**Interfaces:**
- Consumes: Task 2 `ArcheryTarget(..., isShared)`, Task 3 `ArcheryShot.WithWind`.
- Produces:
  - `ArcheryCourseKind.ShootOff = 2`
  - `ArcheryRangeStand(int standIndex, float distanceM, int exposureTicks, float lateralSpan, float lateralPeriod, float faceRadiusM = 0f, float windMps2 = 0f, int pointsMultiplier = 1)` + 필드 `WindMps2`, `PointsMultiplier`
  - `ArcheryCourse.IsLaned` (Range 또는 ShootOff), `ArcheryCourse.IsShootOff`
  - `ArcheryCourse.StepCount`/`ArrowsPerStand`/`ShootingBoxHalfWidth`/`ShootingBoxHalfDepth`/`MoveSpeed`/`MatchDurationTicks`/`IndexAt`: `Range` 대신 `IsLaned`로 판정. ShootOff는 `ArrowsPerStand = 1`, 박스·속도 0.
  - `Vector3 ArcheryCourse.WindAt(long tick, long gameplayStartTick)` — ShootOff가 아니거나 라운드 밖이거나 레이아웃이 없으면 `Vector3.zero`.
  - `int ArcheryCourse.MultiplierAt(int index)` — ShootOff 아니면 1, 범위 밖이면 1, 데이터 값이 1 미만이면 1.
  - `long ArcheryCourse.RoundCloseTick(int index, long gameplayStartTick)` — ShootOff 전용: 그 라운드 과녁이 사라지는 틱(`시작 + 앞 라운드들의 (노출+간격) 합 + 이 라운드 노출`).
  - `float ArcheryCourse.StandDistanceAt(int index)` — 범위 밖이면 0 (HUD의 "마지막 쏠 시각" 계산용).
  - `int ArcheryCourse.ExposureTicksAt(int index)` — ShootOff 전용(순서 = 데이터 순서), 범위 밖이면 0 (HUD 시간 막대용).
  - `ArcheryRangeLayout.Lane? ArcheryCourse.SharedLane` — ShootOff면 레인 0(레이아웃이 떠 있을 때), 아니면 null.
  - `public static Vector3 ArcheryTargetMotion.ShooterRightAxis(Vector3 facing)` — 사수 기준 오른쪽 수평 단위벡터(`Cross(up, -facing)`), 축퇴면 `Vector3.right`.
  - `ArcheryRangeValidation.Check(layout, config.Range, archerCount, courseKind)` — 넷째 인자 추가(기본 `Range`).

- [ ] **Step 1: 실패하는 시험을 쓴다** — `ArcheryCourseTests.cs`에 ShootOff 설정 도우미와 시험을 더한다.

```csharp
        //  라운드 넷: 자리 0,2,0,1을 다시 쓴다. 마지막은 두 배, 셋째는 바람.
        private static ArcheryConfig ShootOffConfig()
        {
            var stands = new List<ArcheryRangeStand>
            {
                new ArcheryRangeStand(0, 10f, 250, 0f, 0f),
                new ArcheryRangeStand(2, 30f, 250, 0f, 0f),
                new ArcheryRangeStand(0, 10f, 250, 0f, 0f, 0f, windMps2: 12f),
                new ArcheryRangeStand(1, 20f, 300, 0f, 0f, 0f, 0f, pointsMultiplier: 2),
            };
            //  데이터에 화살·박스·속도를 적어도 ShootOff는 무시해야 한다 — 일부러 채운다.
            var range = new ArcheryRangeSettings(FaceKind(), stands, stepGapTicks: 200, arrowsPerStand: 5,
                                                 boxHalfWidthM: 3f, boxHalfDepthM: 1.5f, moveSpeedMps: 4f);
            return new ArcheryConfig(120, 3, 5, 3.5f, 0.3f, 0.6f, 1.2f, 0f, 1f,
                                     1.2f, 2.5f, 0f, 1.2f, 2.4f, 12, 20,
                                     new List<ArcheryTargetKind> { FaceKind() },
                                     ArcheryCourseKind.ShootOff, 0, range);
        }

        private ArcheryCourse ShootOffCourse(ArcheryRangeLayout layout = null)
        {
            var l = layout ?? Layout(laneCount: 2, standCount: 3);
            return new ArcheryCourse(ShootOffConfig(), new FixedSeed(777UL),
                                     new[] { "user-a", "user-b", "user-c", "user-d" }, 0.02f, () => l);
        }

        [Test]
        public void ShootOff는_데이터_순서_그대로_선다()
        {
            var course = ShootOffCourse();
            var targets = new List<ArcheryTarget>();
            //  라운드 1(두 번째)은 자리 2(30m) — 섞였다면 씨앗에 따라 다른 자리가 나온다.
            course.Fill(targets, 1, 0);
            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual(30f, targets[0].Origin.z, 1e-4f);   // 자리 2 = z 30
            Assert.AreEqual(0f, targets[0].Origin.x, 1e-4f);    // 레인 0(x=0)
        }

        [Test]
        public void ShootOff는_레인_0에_공유_과녁_하나()
        {
            var course = ShootOffCourse();
            var targets = new List<ArcheryTarget>();
            course.Fill(targets, 0, 0);
            Assert.AreEqual(1, targets.Count);
            Assert.IsTrue(targets[0].IsShared);
            Assert.AreEqual(string.Empty, targets[0].OwnerUserId);
        }

        [Test]
        public void ShootOff는_씨앗이_달라도_같은_순서()
        {
            var layout = Layout(2, 3);
            var a = new ArcheryCourse(ShootOffConfig(), new FixedSeed(1UL), new[] { "u" }, 0.02f, () => layout);
            var b = new ArcheryCourse(ShootOffConfig(), new FixedSeed(999UL), new[] { "u" }, 0.02f, () => layout);
            var ta = new List<ArcheryTarget>();
            var tb = new List<ArcheryTarget>();
            for (int i = 0; i < 4; i++)
            {
                a.Fill(ta, i, 0);
                b.Fill(tb, i, 0);
                Assert.AreEqual(ta[0].Origin, tb[0].Origin);
            }
        }

        [Test]
        public void ShootOff는_한_발_박스_0_속도_0()
        {
            var course = ShootOffCourse();
            Assert.AreEqual(1, course.ArrowsPerStand);
            Assert.AreEqual(0f, course.ShootingBoxHalfWidth);
            Assert.AreEqual(0f, course.ShootingBoxHalfDepth);
            Assert.AreEqual(0f, course.MoveSpeed);
            Assert.AreEqual(4, course.StepCount);
            Assert.IsTrue(course.IsLaned);
            Assert.IsTrue(course.IsShootOff);
        }

        [Test]
        public void 라운드_마감_틱은_앞_라운드들의_노출과_간격의_합_더하기_이_노출()
        {
            var course = ShootOffCourse();
            Assert.AreEqual(1000 + 250, course.RoundCloseTick(0, 1000));
            Assert.AreEqual(1000 + 450 + 250, course.RoundCloseTick(1, 1000));
            Assert.AreEqual(1000 + 450 * 3 + 300, course.RoundCloseTick(3, 1000));
        }

        [Test]
        public void 배수는_데이터_값이고_범위_밖은_1()
        {
            var course = ShootOffCourse();
            Assert.AreEqual(1, course.MultiplierAt(0));
            Assert.AreEqual(2, course.MultiplierAt(3));
            Assert.AreEqual(1, course.MultiplierAt(9));
        }

        [Test]
        public void 바람은_사수_기준_오른쪽이_양수()
        {
            var course = ShootOffCourse();
            //  라운드 2가 시작하는 틱 = 450 × 2. 레인은 +z를 보므로 오른쪽은 +x.
            var wind = course.WindAt(900 + 10, 0);
            Assert.AreEqual(12f, wind.x, 1e-4f);
            Assert.AreEqual(0f, wind.y, 1e-4f);
            Assert.AreEqual(0f, wind.z, 1e-4f);
            Assert.AreEqual(Vector3.zero, course.WindAt(10, 0));     // 라운드 0은 바람 없음
        }

        [Test]
        public void 레이아웃이_없으면_바람은_0()
        {
            var course = new ArcheryCourse(ShootOffConfig(), new FixedSeed(1UL), new[] { "u" }, 0.02f,
                                           () => ArcheryRangeLayout.From(new ArcheryLane[0]));
            Assert.AreEqual(Vector3.zero, course.WindAt(910, 0));
        }

        [Test]
        public void 사거리는_공유_과녁이_아니고_예전처럼_레인마다_하나()
        {
            var course = RangeCourse();
            var targets = new List<ArcheryTarget>();
            course.Fill(targets, 0, 0);
            Assert.AreEqual(2, targets.Count);
            Assert.IsFalse(targets[0].IsShared);
            Assert.IsFalse(course.IsShootOff);
            Assert.AreEqual(Vector3.zero, course.WindAt(10, 0));
        }
```

`ArcheryRangeValidationTests.cs`에 추가(파일의 기존 레이아웃 도우미를 쓴다. 이름이 다르면 그 파일의 도우미로 맞춘다):

```csharp
        [Test]
        public void ShootOff는_레인_하나로_네_명을_받는다()
        {
            var layout = LayoutOf(laneCount: 1, standCount: 3);   // 기존 도우미
            var stands = new[] { new ArcheryRangeStand(0, 10f, 250, 0f, 0f), new ArcheryRangeStand(0, 10f, 250, 0f, 0f),
                                 new ArcheryRangeStand(2, 30f, 250, 0f, 0f), new ArcheryRangeStand(1, 20f, 250, 0f, 0f) };
            var range = new ArcheryRangeSettings(default, stands, 200);
            Assert.IsNull(ArcheryRangeValidation.Check(layout, range, 4, ArcheryCourseKind.ShootOff));
        }

        [Test]
        public void ShootOff도_범위_밖_자리_번호는_거절()
        {
            var layout = LayoutOf(laneCount: 1, standCount: 3);
            var range = new ArcheryRangeSettings(default, new[] { new ArcheryRangeStand(5, 10f, 250, 0f, 0f) }, 200);
            Assert.IsNotNull(ArcheryRangeValidation.Check(layout, range, 4, ArcheryCourseKind.ShootOff));
        }
```

`ArcheryWorldTests.cs`에 추가 — 쏠 때 샷에 바람이 실린다. 기존 `ArcheryWorldFixture`로 ShootOff 코스를 만들고 한 발을 쏘게 한 뒤:

```csharp
        [Test]
        public void ShootOff에서_쏜_화살에는_그_틱의_바람이_실린다()
        {
            //  기존 픽스처의 "사거리 코스로 한 발 쏘기" 도우미를 ShootOff 설정(라운드 0에 바람 8)으로 부른다.
            //  기대: world.Shots[0].Wind == course.WindAt(fireTick, world.GameplayStartTick), 그리고 x ≠ 0.
        }
```

⚠️ 이 시험의 몸통은 `ArcheryWorldFixture`의 실제 도우미 이름에 맞춰 쓴다(픽스처를 먼저 읽을 것). 단언 두 줄은 위 그대로다: `Assert.AreEqual(course.WindAt(shot.FireTick, world.GameplayStartTick), shot.Wind)` 와 `Assert.AreNotEqual(0f, shot.Wind.x)`.

- [ ] **Step 2: 실패를 확인한다** — 기대: 컴파일 에러(`ShootOff`, `windMps2`, `IsLaned` 등 없음).

- [ ] **Step 3: 구현한다**

`ArcheryCourseKind.cs`에:

```csharp
        /// <summary>한 발 승부. 모두 레인 0에서 공유 과녁 하나에 라운드마다 한 발. 순서는 데이터 그대로.</summary>
        ShootOff = 2,
```

`ArcheryRangeStand` — 필드 둘과 생성자 끝 선택 인자 둘:

```csharp
        /// <summary>이 라운드의 바람(가속도, m/s²). 양수면 사수가 보는 기준 오른쪽으로 민다. 한 발 승부만 쓴다.</summary>
        public readonly float WindMps2;

        /// <summary>이 라운드의 점수 배수(마지막 라운드 ×2). 한 발 승부만 쓴다.</summary>
        public readonly int PointsMultiplier;
```

`ArcheryTargetMotion.cs` — 공개 함수 하나:

```csharp
        /// <summary>사수가 보는 기준 오른쪽(수평 단위벡터). <paramref name="facing"/>은 과녁이 사수를 보는 쪽이다.</summary>
        public static Vector3 ShooterRightAxis(Vector3 facing)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, -facing);
            float sqr = axis.sqrMagnitude;
            return sqr < 1e-8f ? Vector3.right : axis / Mathf.Sqrt(sqr);
        }
```

`ArcheryCourse.cs`:

```csharp
        /// <summary>레인이 있는 코스인가(사거리, 한 발 승부). 웨이브(원형)만 아니다.</summary>
        public bool IsLaned => config.CourseKind == ArcheryCourseKind.Range
                            || config.CourseKind == ArcheryCourseKind.ShootOff;

        public bool IsShootOff => config.CourseKind == ArcheryCourseKind.ShootOff;
```

- `StepCount`, `MatchDurationTicks`, `IndexAt`의 `== Range` / `!= Range` 판정을 `IsLaned`로 바꾼다.
- `ArrowsPerStand => StepCount == 0 ? 0 : (IsShootOff ? 1 : Mathf.Max(1, config.Range.ArrowsPerStand));`
- `ShootingBoxHalfWidth`/`ShootingBoxHalfDepth`/`MoveSpeed`: `StepCount == 0 || IsShootOff`면 0.
- `Fill`: 레인형 분기 안에서 ShootOff면 레인 0에 과녁 하나.

```csharp
            if (IsShootOff)
            {
                var shared = layout.Lanes[0];
                into.Add(new ArcheryTarget(index, 0, shared.Stands[stand.StandIndex], 0f, spawnTick,
                                           faceRadius, kind.Points, kind.IsTrap, kind.Shape, kind.Bands,
                                           -shared.Forward, lifetime, string.Empty,
                                           stand.LateralSpan, stand.LateralPeriod, isShared: true));
                return;
            }
```

- `TryEnsureBuilt`: ShootOff면 피셔–예이츠를 건너뛰고 `draw[i] = i` 그대로 둔다(난수를 안 쓴다).

```csharp
            if (IsShootOff == false)
            {
                //  (기존 피셔–예이츠 블록 그대로)
            }
```

- 새 멤버:

```csharp
        public int MultiplierAt(int index)
        {
            if (IsShootOff == false || index < 0 || index >= StepCount)
            {
                return 1;
            }
            return Mathf.Max(1, config.Range.Stands[index].PointsMultiplier);
        }

        //  한 발 승부는 순서를 안 섞으므로 씬 없이도 계산된다 — 순서 = 데이터 순서.
        public long RoundCloseTick(int index, long gameplayStartTick)
        {
            long cursor = gameplayStartTick;
            for (int i = 0; i < index; i++)
            {
                cursor += config.Range.Stands[i].ExposureTicks + config.Range.StepGapTicks;
            }
            return cursor + config.Range.Stands[index].ExposureTicks;
        }

        public float StandDistanceAt(int index)
        {
            if (IsLaned == false || index < 0 || index >= StepCount || TryEnsureBuilt() == false)
            {
                return 0f;
            }
            return config.Range.Stands[order[index]].DistanceM;
        }

        public int ExposureTicksAt(int index)
            => IsShootOff && index >= 0 && index < StepCount ? config.Range.Stands[index].ExposureTicks : 0;

        public ArcheryRangeLayout.Lane? SharedLane
            => IsShootOff && TryEnsureBuilt() ? layout.Lanes[0] : (ArcheryRangeLayout.Lane?)null;

        /// <summary>
        /// 그 틱에 쏜 화살을 미는 바람. 한 발 승부가 아니거나, 라운드 밖이거나, 맵 씬이 아직
        /// 안 떴으면 0이다 — 쏘는 틱엔 과녁이 서 있어야 하므로 씬이 떠 있는 게 정상이다.
        /// </summary>
        public Vector3 WindAt(long tick, long gameplayStartTick)
        {
            if (IsShootOff == false)
            {
                return Vector3.zero;
            }
            int index = IndexAt(tick, gameplayStartTick);
            if (index < 0 || index >= StepCount || TryEnsureBuilt() == false)
            {
                return Vector3.zero;
            }
            float wind = config.Range.Stands[index].WindMps2;
            if (wind == 0f)
            {
                return Vector3.zero;
            }
            return ArcheryTargetMotion.ShooterRightAxis(-layout.Lanes[0].Forward) * wind;
        }
```

`ArcheryWorld.Mutation` — 샷을 목록에 넣기 전에:

```csharp
                var shot = aimSystem.Tick(entity, tick, tickInterval);
                if (shot.HasValue)
                {
                    var fired = shot.Value.WithWind(course.WindAt(tick, GameplayStartTick));
                    shots.Add(fired);
                    EventBuffer.Append(new ArcheryShotFiredEvent(
                        fired.ShooterId, fired.FireTick, fired.Origin, fired.Velocity));
                }
```

(바람은 사건에 싣지 않는다 — 받는 쪽이 발사 틱으로 다시 계산한다. Task 9.)

`ArcheryRangeValidation.Check` — 넷째 인자 `ArcheryCourseKind courseKind = ArcheryCourseKind.Range`:
- ShootOff면 "레인 수 < 사수 수" 검사를 "레인 0개"로, "씬 자리 수 == 데이터 행 수" 검사를 건너뛰고, 거리 검사는 레인 0만.
- 자리 번호 범위 검사는 두 방식 모두.

```csharp
            bool shootOff = courseKind == ArcheryCourseKind.ShootOff;
            if (shootOff == false && layout.Lanes.Count < archerCount) { /* 기존 메시지 */ }
            if (shootOff == false && layout.StandCount != range.Stands.Count) { /* 기존 메시지 */ }
            // (자리 번호 범위 검사: 그대로)
            int lanesToCheck = shootOff ? 1 : layout.Lanes.Count;
            for (int lane = 0; lane < lanesToCheck; lane++) { /* 기존 거리 검사 */ }
```

- [ ] **Step 4: 통과를 확인한다** — Shared EditMode `ArcheryCourseTests`·`ArcheryRangeValidationTests`·`ArcheryWorldTests`·`ArcheryMovementTests`·`ArcheryQuiverTests` **전부**(사거리 회귀 없음 — Review Focus 4). 이어서 서버 에디터 EditMode `ArcheryHitSystemTests` 전부.
- [ ] **Step 5: 커밋한다** — `feat(archery): 코스 방식 ShootOff — 공유 레인·고정 순서·바람·배수`

---

### Task 5: 라운드 결과 사건 + 와이어 (Shared)

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryRoundResultEvent.cs`
- Create: `LeagueOfPhysical-Shared/Protos/ArcheryRoundResultToC.proto`
- Modify: `LeagueOfPhysical-Shared/Protos/WorldEventToC.proto`
- Modify: `LeagueOfPhysical-Shared/Runtime.Generated/Scripts/WorldEventWire.cs`
- Regenerate: `Runtime.Generated/Scripts/Protobuf/*`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/WorldEventWireTests.cs`(추가)

**Interfaces:**
- Consumes: Task 1 `ArcheryRoundPlacement`.
- Produces: `public sealed record ArcheryRoundResultEvent(int roundIndex, int multiplier, IReadOnlyList<ArcheryRoundPlacement> placements) : GameFramework.World.WorldEvent`, 와이어 case `WorldEventToC.EventOneofCase.ArcheryRoundResult`.

- [ ] **Step 1: 실패하는 시험을 쓴다**

```csharp
        [Test]
        public void 라운드_결과는_와이어를_왕복해도_같다()
        {
            var placements = new List<ArcheryRoundPlacement>
            {
                new ArcheryRoundPlacement("e1", true, new Vector2(0.03f, -0.01f), 0.0316f, 0, 6),
                new ArcheryRoundPlacement("e2", false, Vector2.zero, 0f, 1, 0),
            };
            var wire = WorldEventWire.ToWire(new ArcheryRoundResultEvent(11, 2, placements));
            var back = (ArcheryRoundResultEvent)WorldEventWire.FromWire(wire);

            Assert.AreEqual(11, back.roundIndex);
            Assert.AreEqual(2, back.multiplier);
            Assert.AreEqual(2, back.placements.Count);
            Assert.AreEqual("e1", back.placements[0].ShooterId);
            Assert.IsTrue(back.placements[0].Hit);
            Assert.AreEqual(0.03f, back.placements[0].FaceOffset.x, 1e-6f);
            Assert.AreEqual(-0.01f, back.placements[0].FaceOffset.y, 1e-6f);
            Assert.AreEqual(0.0316f, back.placements[0].Distance, 1e-6f);
            Assert.AreEqual(6, back.placements[0].Points);
            Assert.IsFalse(back.placements[1].Hit);
            Assert.AreEqual(1, back.placements[1].Rank);
        }
```

- [ ] **Step 2: 실패를 확인한다** — 컴파일 에러.

- [ ] **Step 3: 구현한다**

`ArcheryRoundResultEvent.cs`:

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 한 발 승부의 한 라운드가 끝났다는 사실(연출용). 결과 화면이 <b>이 사건 하나로</b> 그려지게
    /// 모두의 착탄점을 싣는다. 점수의 진실원본은 스냅샷이다 — 유실돼도 점수는 맞다.
    /// </summary>
    public sealed record ArcheryRoundResultEvent(
        int roundIndex,
        int multiplier,
        IReadOnlyList<ArcheryRoundPlacement> placements
    ) : GameFramework.World.WorldEvent;
}
```

`Protos/ArcheryRoundResultToC.proto`(최상위 패킷 아님 — 마커 주석을 달지 않는다):

```proto
syntax = "proto3";

// WorldEventToC oneof 안에 담기는 payload. 따로 보내는 패킷이 아니다.
message ArcheryRoundPlacementToC
{
	string shooter_id = 1;
	bool   hit        = 2;
	float  face_x     = 3;   // 미터, 사수 기준 오른쪽
	float  face_y     = 4;   // 미터, 위
	float  distance   = 5;   // 미터
	int32  rank       = 6;   // 0이 1등
	int32  points     = 7;
}

message ArcheryRoundResultToC
{
	int32 round_index = 1;
	int32 multiplier  = 2;
	repeated ArcheryRoundPlacementToC placements = 3;
}
```

`WorldEventToC.proto`: `import "ArcheryRoundResultToC.proto";` 추가, oneof에 `ArcheryRoundResultToC archery_round_result = 5;`.

`WorldEventWire.ToWire`에:

```csharp
                case ArcheryRoundResultEvent r:
                {
                    var msg = new ArcheryRoundResultToC { RoundIndex = r.roundIndex, Multiplier = r.multiplier };
                    foreach (var p in r.placements)
                    {
                        msg.Placements.Add(new ArcheryRoundPlacementToC
                        {
                            ShooterId = p.ShooterId, Hit = p.Hit,
                            FaceX = p.FaceOffset.x, FaceY = p.FaceOffset.y,
                            Distance = p.Distance, Rank = p.Rank, Points = p.Points,
                        });
                    }
                    return new WorldEventToC { ArcheryRoundResult = msg };
                }
```

`FromWire`에:

```csharp
                case WorldEventToC.EventOneofCase.ArcheryRoundResult:
                {
                    var list = new System.Collections.Generic.List<ArcheryRoundPlacement>();
                    foreach (var p in rec.ArcheryRoundResult.Placements)
                    {
                        list.Add(new ArcheryRoundPlacement(p.ShooterId, p.Hit, new Vector2(p.FaceX, p.FaceY),
                                                           p.Distance, p.Rank, p.Points));
                    }
                    return new ArcheryRoundResultEvent(rec.ArcheryRoundResult.RoundIndex,
                                                       rec.ArcheryRoundResult.Multiplier, list);
                }
```

생성: Shared 루트에서 `bash Tools/Protobuf/generate_protos.sh`. 그다음 **반드시** `git diff -- Runtime.Generated/Scripts/MessageIds.cs`가 **비어 있어야 한다**(최상위 패킷을 안 더했으므로). 한 줄이라도 바뀌면 멈추고 보고한다(와이어 파손).

- [ ] **Step 4: 통과를 확인한다** — `WorldEventWireTests` 전부.
- [ ] **Step 5: 커밋한다** — 새 `.proto`, 새 생성 `.cs`와 `.meta`, `WorldEventToC.proto`, 다시 만든 `WorldEventToC.cs`, `WorldEventWire.cs`, 사건 파일, 시험. `feat(archery): 라운드 결과 사건 — WorldEventToC oneof 한 줄`

---

### Task 6: 서버 — 공유 과녁 착탄 기록과 라운드 마감

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryRoundLog.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/ArcheryRoundSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs` (생성자에 `ArcheryRoundLog`, `ApplyCandidates`)
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryLifetimeScope.cs`
- Test: `LeagueOfPhysical-Server/Assets/Tests/Editor/ArcheryRoundSystemTests.cs`(새), `ArcheryHitSystemTests.cs`(생성자 호출부 + 새 시험)

**Interfaces:**
- Consumes: Task 1 `ArcheryShootOffRanking.Rank`, Task 2 `ArcheryFaceCoords`, `IsShared`, Task 4 `RoundCloseTick`/`MultiplierAt`/`IsShootOff`/`StepCount`, Task 5 `ArcheryRoundResultEvent`.
- Produces:
  - `ArcheryRoundLog`: `void Record(int round, string shooterId, Vector2 faceOffsetMeters, float distanceMeters)`(같은 라운드·사수는 처음 것만), `bool TryGet(int round, string shooterId, out Vector2 face, out float distance)`, `void Forget(int round)`.
  - `ArcheryRoundSystem : GameFramework.Runner.ITickSystem` — 생성자 `(System.Func<long> gameplayStartTick, EntityRegistry registry, WorldEventBuffer eventBuffer, ArcheryCourse course, ArcheryRoundLog log)` — 월드 대신 출발 틱을 읽는 방법을 받는다(시험에서 월드를 조립하지 않으려고).
  - `ArcheryHitSystem` 생성자 끝에 `ArcheryRoundLog roundLog` 추가.

- [ ] **Step 1: 실패하는 시험을 쓴다** — `ArcheryRoundSystemTests.cs`. 사수 엔티티는 `ArcheryScore`만 있으면 된다(순위 인원 = `ArcheryScore`를 가진 엔티티).

```csharp
using System.Collections.Generic;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryRoundSystemTests
    {
        sealed class Seed : IMatchSeed { public ulong Value => 1UL; }
        const float TickInterval = 0.02f;
        const long Start = 1000;

        //  라운드 둘: 250틱 노출 + 200틱 간격, 둘째는 ×2. 순서 계산에 씬이 필요 없다.
        static ArcheryCourse Course()
        {
            var stands = new List<ArcheryRangeStand>
            {
                new ArcheryRangeStand(0, 10f, 250, 0f, 0f),
                new ArcheryRangeStand(0, 10f, 250, 0f, 0f, 0f, 0f, pointsMultiplier: 2),
            };
            var range = new ArcheryRangeSettings(default, stands, 200);
            var config = new ArcheryConfig(120, 3, 5, 3.5f, 0.3f, 0.6f, 1.2f, 0f, 1f,
                                           1.2f, 2.5f, 0f, 1.2f, 2.4f, 12, 20,
                                           new List<ArcheryTargetKind>(), ArcheryCourseKind.ShootOff, 0, range);
            return new ArcheryCourse(config, new Seed(), new string[0], TickInterval, () => null);
        }

        sealed class Fixture
        {
            public EntityRegistry Registry = new EntityRegistry();
            public WorldEventBuffer Events = new WorldEventBuffer();
            public ArcheryRoundLog Log = new ArcheryRoundLog();
            public ArcheryCourse Course = Course();
            public ArcheryRoundSystem System;
            public long StartTick = Start;

            public Fixture(params string[] archers)
            {
                foreach (var id in archers)
                {
                    var e = new Entity(id);
                    e.Add(new ArcheryScore());
                    Registry.Add(e);
                }
                System = new ArcheryRoundSystem(() => StartTick, Registry, Events, Course, Log);
            }

            public int Score(string id) => Registry.Get(id).Get<ArcheryScore>().Value;

            public List<ArcheryRoundResultEvent> Results()
            {
                var list = new List<ArcheryRoundResultEvent>();
                foreach (var e in Events.Snapshot)
                {
                    if (e is ArcheryRoundResultEvent r) { list.Add(r); }
                }
                return list;
            }
        }

        [Test]
        public void 마감_틱_전에는_아무_일도_없다()
        {
            var f = new Fixture("a", "b");
            f.Log.Record(0, "a", Vector2.zero, 0.1f);
            f.System.Tick(Start + 249, TickInterval);
            Assert.AreEqual(0, f.Results().Count);
            Assert.AreEqual(0, f.Score("a"));
        }

        [Test]
        public void 마감_틱에_순위_점수를_주고_사건을_낸다()
        {
            var f = new Fixture("a", "b", "c");
            f.Log.Record(0, "a", new Vector2(0.2f, 0f), 0.2f);
            f.Log.Record(0, "b", new Vector2(0.05f, 0f), 0.05f);
            f.System.Tick(Start + 250, TickInterval);

            Assert.AreEqual(1, f.Score("a"));
            Assert.AreEqual(2, f.Score("b"));
            Assert.AreEqual(0, f.Score("c"));
            var results = f.Results();
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(0, results[0].roundIndex);
            Assert.AreEqual(3, results[0].placements.Count);
            Assert.AreEqual("b", results[0].placements[0].ShooterId);
        }

        [Test]
        public void 같은_라운드는_한_번만_닫는다()
        {
            var f = new Fixture("a", "b");
            f.Log.Record(0, "a", Vector2.zero, 0.1f);
            f.System.Tick(Start + 250, TickInterval);
            f.System.Tick(Start + 250, TickInterval);
            f.System.Tick(Start + 251, TickInterval);
            Assert.AreEqual(1, f.Score("a"));
            Assert.AreEqual(1, f.Results().Count);
        }

        [Test]
        public void 마감_틱을_건너뛰어도_밀린_라운드를_모두_닫는다()
        {
            var f = new Fixture("a", "b");
            f.Log.Record(0, "a", Vector2.zero, 0.1f);
            f.Log.Record(1, "a", Vector2.zero, 0.1f);
            //  라운드 1 마감 = 1000 + 450 + 250 = 1700. 그 뒤 틱 하나로 둘 다 닫혀야 한다.
            f.System.Tick(Start + 800, TickInterval);
            Assert.AreEqual(1 + 2, f.Score("a"));
            Assert.AreEqual(2, f.Results().Count);
            Assert.AreEqual(2, f.Results()[1].multiplier);
        }

        [Test]
        public void 판_도중_나간_사람은_순위에서_빠진다()
        {
            var f = new Fixture("a", "b", "c");
            f.Log.Record(0, "a", Vector2.zero, 0.1f);
            f.Registry.Remove("c");
            f.System.Tick(Start + 250, TickInterval);
            //  인원 2명 → 1등 1점.
            Assert.AreEqual(1, f.Score("a"));
            Assert.AreEqual(2, f.Results()[0].placements.Count);
        }

        [Test]
        public void 출발_전이면_아무_일도_없다()
        {
            var f = new Fixture("a");
            f.StartTick = long.MaxValue;
            f.System.Tick(5000, TickInterval);
            Assert.AreEqual(0, f.Results().Count);
        }
    }
}
```

등록부에서 `() => world.GameplayStartTick`을 넘긴다.

`ArcheryHitSystemTests.cs`: `new ArcheryHitSystem(...)` 호출부 전부에 `new ArcheryRoundLog()`를 마지막 인자로 더하고, 공유 과녁 시험 둘을 더한다 — 기존 `RangeScene` 도우미로 ShootOff 코스(레인 1, 자리 1)를 만들고 두 사수가 과녁 정중앙과 약간 옆을 맞히게 쏜 뒤:

```csharp
            //  공유 과녁: 둘 다 맞고, 점수는 안 오르고(라운드 마감 전), 기록판에 둘 다 남는다.
            Assert.AreEqual(0, f.ScoreOf("e1"));
            Assert.AreEqual(0, f.ScoreOf("e2"));
            Assert.IsTrue(roundLog.TryGet(0, "e1", out _, out float d1));
            Assert.IsTrue(roundLog.TryGet(0, "e2", out _, out float d2));
            Assert.Less(d1, d2);
            //  사건의 points는 띠 점수(연출용) — 정중앙이면 가장 안쪽 띠 점수.
            Assert.AreEqual(10, f.LastHitPoints());
```

그리고 `같은_화살은_공유_과녁을_한_번만_기록한다`: 한 사수의 한 발이 두 틱에 걸쳐 판정돼도 기록이 바뀌지 않는다(`spentArrows`).

- [ ] **Step 2: 실패를 확인한다** — 서버 에디터 EditMode. 컴파일 에러.

- [ ] **Step 3: 구현한다**

`ArcheryRoundLog.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 한 발 승부에서 라운드마다 누가 어디를 맞혔나(서버 권위). 판정(<see cref="ArcheryHitSystem"/>)이
    /// 쓰고 라운드 마감(<see cref="ArcheryRoundSystem"/>)이 읽는다 — 둘 다 들여다보는 값이라 따로 둔다
    /// (<see cref="ArcheryWaveState"/>와 같은 이유).
    /// </summary>
    public class ArcheryRoundLog
    {
        private readonly Dictionary<(int round, string shooterId), (Vector2 face, float distance)> impacts
            = new Dictionary<(int, string), (Vector2, float)>();

        public void Record(int round, string shooterId, Vector2 faceOffsetMeters, float distanceMeters)
        {
            //  라운드당 한 발이다. 혹시 둘째가 와도 첫 발을 지킨다.
            var key = (round, shooterId);
            if (impacts.ContainsKey(key) == false)
            {
                impacts[key] = (faceOffsetMeters, distanceMeters);
            }
        }

        public bool TryGet(int round, string shooterId, out Vector2 face, out float distance)
        {
            if (impacts.TryGetValue((round, shooterId), out var v))
            {
                face = v.face;
                distance = v.distance;
                return true;
            }
            face = default;
            distance = 0f;
            return false;
        }

        public void Forget(int round)
        {
            var stale = new List<(int, string)>();
            foreach (var key in impacts.Keys)
            {
                if (key.round == round) { stale.Add(key); }
            }
            foreach (var key in stale) { impacts.Remove(key); }
        }
    }
}
```

`ArcheryHitSystem.ApplyCandidates` — `outcome` 계산 뒤 점수·사건 부분을 바꾼다. 착탄점이 필요하므로 `Candidate`에 `Vector3 Impact`(맞은 자리 - 과녁 중심)를 더하고 `CollectCandidates`에서 `Vector3.Lerp(from, to, t) - targetAt`으로 채운다.

```csharp
                var outcome = ArcheryHitRules.Resolve(target, candidate.Offset);
                spentArrows.Add((candidate.ShooterId, candidate.FireTick));

                if (target.IsShared)
                {
                    //  한 발 승부: 점수는 라운드 마감에 순위로 준다. 여기선 어디를 맞혔는지만 적는다.
                    //  사건의 points는 띠 점수 — 해설이 "10점!"을 고르는 데만 쓴다.
                    Vector2 face = ArcheryFaceCoords.ToFaceOffset(candidate.Impact, target.Facing, 1f);
                    roundLog.Record(target.WaveIndex, candidate.ShooterId, face, face.magnitude);
                    eventBuffer.Append(new ArcheryTargetHitEvent(
                        candidate.ShooterId, candidate.FireTick, outcome.Gained));
                    continue;
                }

                //  (기존 점수 적립 + 사건 그대로)
```

`ArcheryRoundSystem.cs`:

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 한 발 승부의 라운드를 닫는다. 과녁이 사라지는 틱이 지나면 그 라운드의 순위 점수를 주고
    /// 결과 사건을 낸다. <b>서버에서만 돈다.</b>
    /// </summary>
    public class ArcheryRoundSystem : GameFramework.Runner.ITickSystem
    {
        private readonly System.Func<long> gameplayStartTick;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.WorldEventBuffer eventBuffer;
        private readonly ArcheryCourse course;
        private readonly ArcheryRoundLog log;
        private readonly List<ArcheryRoundShot> shots = new List<ArcheryRoundShot>();

        //  다음에 닫을 라운드. 틱을 건너뛰어도 밀린 라운드를 차례로 다 닫는다.
        private int nextRound;

        public ArcheryRoundSystem(System.Func<long> gameplayStartTick,
                                  GameFramework.World.EntityRegistry entityRegistry,
                                  GameFramework.World.WorldEventBuffer eventBuffer,
                                  ArcheryCourse course, ArcheryRoundLog log)
        {
            this.gameplayStartTick = gameplayStartTick;
            this.entityRegistry = entityRegistry;
            this.eventBuffer = eventBuffer;
            this.course = course;
            this.log = log;
        }

        public void Tick(long tick, float deltaTime)
        {
            long start = gameplayStartTick();
            if (course.IsShootOff == false || start == long.MaxValue)
            {
                return;
            }

            while (nextRound < course.StepCount && tick >= course.RoundCloseTick(nextRound, start))
            {
                Close(nextRound);
                nextRound++;
            }
        }

        private void Close(int round)
        {
            shots.Clear();
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Has<ArcheryScore>() == false)
                {
                    continue;
                }
                bool hit = log.TryGet(round, entity.Id, out var face, out float distance);
                shots.Add(new ArcheryRoundShot(entity.Id, hit, face, distance));
            }

            int multiplier = course.MultiplierAt(round);
            var placements = ArcheryShootOffRanking.Rank(shots, multiplier);
            foreach (var p in placements)
            {
                var score = entityRegistry.Get(p.ShooterId)?.Get<ArcheryScore>();
                if (score != null)
                {
                    score.Gained += p.Points;
                }
            }

            eventBuffer.Append(new ArcheryRoundResultEvent(round, multiplier, placements));
            log.Forget(round);
        }
    }
}
```

`ArcheryLifetimeScope.cs`(서버):

```csharp
            builder.Register<ArcheryRoundLog>(Lifetime.Singleton);
            //  (ArcheryHitSystem 등록 람다에 c.Resolve<ArcheryRoundLog>()를 마지막 인자로)
            builder.Register(c => new ArcheryRoundSystem(
                () => c.Resolve<ArcheryWorld>().GameplayStartTick,
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<ArcheryCourse>(),
                c.Resolve<ArcheryRoundLog>()), Lifetime.Singleton);
```

빌드 콜백에서 `ArcheryHitSystem` **다음**, `ArcheryStateBroadcastSystem` **앞**에 `ArcheryRoundSystem`을 `Update.End`로 등록한다(같은 틱에 맞은 화살까지 셈에 넣으려고).

⚠️ `() => c.Resolve<ArcheryWorld>()` 람다가 틱마다 컨테이너를 부르지 않게, 람다 밖에서 한 번 resolve해 캡처한다:
```csharp
            builder.Register(c =>
            {
                var world = c.Resolve<ArcheryWorld>();
                return new ArcheryRoundSystem(() => world.GameplayStartTick, /* ... */);
            }, Lifetime.Singleton);
```

- [ ] **Step 4: 통과를 확인한다** — 서버 EditMode `ArcheryRoundSystemTests` 6개 + `ArcheryHitSystemTests` 전부 + 서버 전체 스위트.
- [ ] **Step 5: 커밋한다** — 서버 `feature/archery-shoot-off`. `feat(archery): 한 발 승부 라운드 마감 — 순위 점수 적립과 결과 사건`

---

### Task 7: 마스터데이터 — 열 둘 + ShootOff 맵 행

**Files:**
- Modify: `infrastructure/table/Datas/#ArcheryRange.xlsx` (열 `wind_mps2` float, `points_multiplier` int를 끝에 추가 + 맵 7의 12행)
- Modify: `infrastructure/table/Datas/#ArcheryConfig.xlsx` (행 id 7)
- Modify: `infrastructure/table/Datas/#Map.xlsx` (행 id 7)
- Regenerate: `LeagueOfPhysical-MasterData-Client`, `LeagueOfPhysical-MasterData-Server`, `lop-backend/apps/matchmaking-server/master_data/`

**Interfaces:**
- Produces: 생성 타입 `LOP.MasterData.ArcheryRange.WindMps2`(float), `.PointsMultiplier`(int). 맵 id **7**(`archery_shootoff`, 게임 모드 5, 씬 `Assets/Art/Scenes/ArcheryRangeMap.unity`).

- [ ] **Step 1: 헤더를 먼저 읽는다** — openpyxl로 세 파일의 1~4행을 출력해 열 순서를 확인한다(`#Map`은 **5열**: `id | game_mode_id | code | name | scene_path` — 생성된 json만 보고 옮기면 안 된다).

- [ ] **Step 2: 열과 행을 쓴다** — 스크립트로(openpyxl). 기존 맵 6의 행 `wind_mps2 = 0`, `points_multiplier = 1`로 채운다(빈칸 금지).

`#ArcheryRange.xlsx`에 추가할 맵 7 행(id 101~112). 레인 자리 번호는 사거리 맵 씬 그대로(0=12m, 1=20m, 2=30m, 3=45m, 4=65m, 5=90m). 노출 250, 바람 부호 양수 = 오른쪽. **숫자는 초안이다 — Task 12 실측 후 고친다.** 비행 시간이 0.1~0.5초라 바람 가속도는 두 자리여야 눈에 띄게 민다(30m·60m/s에서 `½·a·t²` = 0.125a·… → a=12면 약 0.15m).

| id | map_id | stand_index | distance_m | exposure_ticks | lateral_span_m | lateral_period_s | face_radius_m | wind_mps2 | points_multiplier |
|---|---|---|---|---|---|---|---|---|---|
| 101 | 7 | 0 | 12 | 250 | 0 | 0 | 0.3 | 0 | 1 |
| 102 | 7 | 1 | 20 | 250 | 0 | 0 | 0.3 | 0 | 1 |
| 103 | 7 | 1 | 20 | 250 | 0 | 0 | 0.3 | 10 | 1 |
| 104 | 7 | 1 | 20 | 250 | 2.5 | 3.2 | 0.3 | 0 | 1 |
| 105 | 7 | 1 | 20 | 250 | 0 | 0 | 0.18 | 0 | 1 |
| 106 | 7 | 2 | 30 | 250 | 0 | 0 | 0.3 | -18 | 1 |
| 107 | 7 | 1 | 20 | 250 | 3 | 2.0 | 0.3 | 0 | 1 |
| 108 | 7 | 4 | 65 | 250 | 0 | 0 | 0.5 | 0 | 1 |
| 109 | 7 | 2 | 30 | 250 | 2 | 2.8 | 0.3 | 12 | 1 |
| 110 | 7 | 1 | 20 | 250 | 3 | 2.2 | 0.2 | 0 | 1 |
| 111 | 7 | 3 | 45 | 250 | 0 | 0 | 0.4 | 20 | 1 |
| 112 | 7 | 3 | 45 | 250 | 2 | 3.0 | 0.4 | 12 | 2 |

`#ArcheryConfig.xlsx` 행 id 7: 맵 6 행을 복사하고 `course_kind = 2`, `step_gap_ticks = 200`, `arrows_per_stand = 1`, `box_half_width_m = 0`, `box_half_depth_m = 0`, `move_speed_mps = 0`.

`#Map.xlsx` 행 id 7: `7 | 5 | archery_shootoff | 한 발 승부 | Assets/Art/Scenes/ArcheryRangeMap.unity`.

- [ ] **Step 3: 생성한다** — `cd infrastructure/table && ./gen.sh`. 이어서:
  - MasterData-Client/Server: `git status --short`가 `ArcheryRange.cs`, `tbarcheryrange.bytes`, `tbarcheryconfig.bytes`, `tbmap.bytes`(+ 클라 쪽 `GameMap` 관련) 정도만 보여야 한다. 지워진 `.meta`가 있으면 `gen.sh`가 되돌렸는지 확인.
  - lop-backend `apps/matchmaking-server/master_data/tbmap.json`에 맵 7이 생겼는지 확인.
- [ ] **Step 4: 두 에디터에서 컴파일을 확인한다** — 패키지가 `file:` 참조라 에디터가 바로 본다. 콘솔 CS 에러 0.
- [ ] **Step 5: 커밋한다** — 레포 넷(infrastructure, MasterData-Client, MasterData-Server, lop-backend) 각자 `feature/archery-shoot-off`에서. 메시지: `feat(archery): 한 발 승부 맵(7)과 라운드 12개 — 바람·배수 열`.

---

### Task 8: 서버 — 모두 한 자리에 세우기 + 설정 읽기

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryRuleSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryConfigProvider.cs`

**Interfaces:**
- Consumes: Task 4 `ArcheryRangeValidation.Check(..., courseKind)`, `ArcheryRangeStand(..., windMps2, pointsMultiplier)`; Task 7에서 생기는 생성 필드 `ArcheryRange.WindMps2`(float)·`PointsMultiplier`(int).
- Produces: 없음(동작만).

- [ ] **Step 1: 구현한다**

`ArcheryRuleSystem.Initialize`:

```csharp
            bool laned = config.CourseKind == ArcheryCourseKind.Range
                      || config.CourseKind == ArcheryCourseKind.ShootOff;
            var lanes = laned ? ArcheryRangeLayout.FromOpenScenes() : null;

            if (lanes != null)
            {
                string problem = ArcheryRangeValidation.Check(lanes, config.Range, playerList.Length,
                                                              config.CourseKind);
                // (기존 throw 그대로)
            }
```

스폰 위치 분기:

```csharp
                if (lanes != null)
                {
                    //  한 발 승부는 모두 레인 0 사대에 선다 — 누구 화면에서나 과녁이 똑같이 보이게.
                    var lane = lanes.Lanes[config.CourseKind == ArcheryCourseKind.ShootOff ? 0 : i];
                    position = lane.ShooterPosition;
                    rotation = new Vector3(0f, Mathf.Atan2(lane.Forward.x, lane.Forward.z) * Mathf.Rad2Deg, 0f);
                }
```

`ArcheryConfigProvider.Get`:
- 알려진 방식 검사에 `ShootOff`를 더한다(메시지: "0(웨이브)·1(사거리)·2(한 발 승부)").
- `if (courseKind == ArcheryCourseKind.Range)` → `Range || ShootOff`.
- 자리 줄 정렬: **ShootOff면 `Id` 오름차순**(라운드 순서 — 같은 자리를 여러 번 쓴다), Range면 기존대로 `StandIndex`.

```csharp
                var rows = System.Linq.Enumerable.Where(md.Tables.TbArcheryRange.DataList, x => x.MapId == mapId);
                var ordered = courseKind == ArcheryCourseKind.ShootOff
                    ? System.Linq.Enumerable.OrderBy(rows, x => x.Id)
                    : System.Linq.Enumerable.OrderBy(rows, x => x.StandIndex);
                foreach (var row in ordered)
                {
                    stands.Add(new ArcheryRangeStand(row.StandIndex, row.DistanceM, row.ExposureTicks,
                                                     row.LateralSpanM, row.LateralPeriodS, row.FaceRadiusM,
                                                     row.WindMps2, row.PointsMultiplier));
                }
```

- [ ] **Step 2: 컴파일과 시험을 확인한다** — 서버 에디터 재컴파일(콘솔 CS 에러 0, 시각 대조), 서버 EditMode 전체.
- [ ] **Step 3: 커밋한다** — `feat(archery): 한 발 승부는 모두 레인 0에 서고 설정을 라운드 순서로 읽는다`

---

### Task 9: 클라 — 설정 읽기 + 받은 샷에 바람

**Files:**
- Modify: `Assets/Scripts/Game/ArcheryConfigProvider.cs` (Task 8의 서버 쪽과 **똑같이**)
- Modify: `Assets/Scripts/Game/MessageHandler/ArcheryRemoteShotHandler.cs`

**Interfaces:**
- Consumes: Task 4 `ArcheryCourse.WindAt`, Task 3 `ArcheryShot(…, wind)`, Task 7 생성 필드.

- [ ] **Step 1: 구현한다**

`ArcheryConfigProvider`: Task 8의 서버 변경과 한 글자도 다르지 않게(쌍둥이 — 다르면 클·서가 다른 과녁을 본다). 두 파일을 나란히 diff해 확인한다: `diff <(sed -n '/public ArcheryConfig Get/,/^        }/p' 서버파일) <(… 클라파일)` 가 비어야 한다.

`ArcheryRemoteShotHandler` — 생성자에 `ArcheryCourse course` 추가(스코프에 이미 싱글턴으로 있다):

```csharp
                archeryWorld.IngestRemoteShot(new ArcheryShot(
                    worldEvent.shooterId, worldEvent.fireTick, worldEvent.origin, worldEvent.velocity,
                    //  바람은 사건에 없다 — 쏜 틱만 알면 쏜 쪽과 같은 값이 나온다.
                    course.WindAt(worldEvent.fireTick, archeryWorld.GameplayStartTick)));
```

- [ ] **Step 2: 컴파일과 시험을 확인한다** — 클라 에디터(워크트리 코드는 에디터가 못 본다 — 메모리 `unity-editor-bound-to-main-checkout`. **Bee+Roslyn 우회 또는 메인 체크아웃에 임시 적용 없이**, 이 태스크까지는 컴파일 검증을 Task 11 끝의 통합 컴파일에서 한 번에 한다. 대신 이 단계에서는 `ArcheryConfigProvider` 쌍둥이 diff만 확인한다.)
- [ ] **Step 3: 커밋한다** — `feat(archery): 클라도 한 발 승부 설정을 읽고, 남의 화살에 바람을 싣는다`

---

### Task 10: 클라 — 화면용 좌우 배치 (내가 가운데)

**Files:**
- Create: `Assets/Scripts/Game/ArcheryShootOffLineup.cs`
- Create: `Assets/Scripts/Game/ArcheryShootOffLineupView.cs`
- Modify: `Assets/Scripts/Game/ArcheryArrowView.cs` (날아가는 화살 위치 계산 한 줄)
- Modify: `Assets/Scripts/Game/ArcheryLifetimeScope.cs`
- Test: `Assets/Tests/Editor/ArcheryShootOffLineupTests.cs`

**Interfaces:**
- Consumes: Task 4 `ArcheryCourse.IsShootOff`, `SharedLane`.
- Produces:
  - `public static class ArcheryShootOffLineup`
    - `public const float SpacingMeters = 1.6f;`
    - `public static float SlotOffset(int othersIndex)` — 남의 자리(0부터): `+1, −1, +2, −2 …` × 간격. (나는 0.)
    - `public static float ArrowBlend(float secondsSinceFire, float flightSeconds)` — 1(출발)에서 0(도착)으로. `flightSeconds <= 0`이면 0.
  - `ArcheryShootOffLineupView : ILateTickable` — 남의 몸(`visualGameObject`)을 사수 기준 오른쪽으로 `SlotOffset`만큼 옮겨 그린다. `Vector3 DisplayOffsetOf(string entityId)`를 공개한다(화살 뷰가 읽는다).

- [ ] **Step 1: 실패하는 시험을 쓴다**

```csharp
using NUnit.Framework;

namespace LOP.Tests
{
    public class ArcheryShootOffLineupTests
    {
        [Test]
        public void 남은_오른쪽_왼쪽을_번갈아_바깥으로()
        {
            float s = ArcheryShootOffLineup.SpacingMeters;
            Assert.AreEqual(s, ArcheryShootOffLineup.SlotOffset(0), 1e-5f);
            Assert.AreEqual(-s, ArcheryShootOffLineup.SlotOffset(1), 1e-5f);
            Assert.AreEqual(2f * s, ArcheryShootOffLineup.SlotOffset(2), 1e-5f);
            Assert.AreEqual(-2f * s, ArcheryShootOffLineup.SlotOffset(3), 1e-5f);
        }

        [Test]
        public void 화살_간격은_출발에서_1_도착에서_0()
        {
            Assert.AreEqual(1f, ArcheryShootOffLineup.ArrowBlend(0f, 0.4f), 1e-5f);
            Assert.AreEqual(0.5f, ArcheryShootOffLineup.ArrowBlend(0.2f, 0.4f), 1e-5f);
            Assert.AreEqual(0f, ArcheryShootOffLineup.ArrowBlend(0.4f, 0.4f), 1e-5f);
            Assert.AreEqual(0f, ArcheryShootOffLineup.ArrowBlend(0.9f, 0.4f), 1e-5f);
            Assert.AreEqual(0f, ArcheryShootOffLineup.ArrowBlend(0.1f, 0f), 1e-5f);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다** — 컴파일 에러.

- [ ] **Step 3: 구현한다**

`ArcheryShootOffLineup.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 한 발 승부의 <b>화면용</b> 좌우 배치. 판정에서는 모두 한 점에서 쏘고, 화면에서만 내가 가운데,
    /// 남은 좌우에 서 있는 것처럼 그린다. 판정 코드는 이 값을 절대 보지 않는다.
    /// </summary>
    public static class ArcheryShootOffLineup
    {
        public const float SpacingMeters = 1.6f;

        public static float SlotOffset(int othersIndex)
        {
            int step = othersIndex / 2 + 1;
            return (othersIndex % 2 == 0 ? step : -step) * SpacingMeters;
        }

        /// <summary>남의 화살이 그 캐릭터의 활에서 떠나 실제 꽂힌 점으로 모이도록, 간격을 비행 동안 줄인다.</summary>
        public static float ArrowBlend(float secondsSinceFire, float flightSeconds)
        {
            if (flightSeconds <= 0f)
            {
                return 0f;
            }
            return 1f - Mathf.Clamp01(secondsSinceFire / flightSeconds);
        }
    }
}
```

`ArcheryShootOffLineupView.cs` — 남의 순서는 **엔티티 id 서수 정렬**(모든 프레임에서 같은 자리를 지키게), 나는 빼고 센다:

```csharp
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 한 발 승부에서 남의 몸을 좌우로 옮겨 그린다. 몸의 월드 위치는 보간기가 매 프레임 덮어쓰므로
    /// <b>몸통(visual)의 로컬 위치</b>만 건드린다 — 루트(판정과 같은 자리)는 그대로다.
    /// </summary>
    public class ArcheryShootOffLineupView : ILateTickable
    {
        private readonly ArcheryCourse course;
        private readonly ActorRegistry actorRegistry;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IPlayerContext playerContext;
        private readonly List<string> others = new List<string>();
        private readonly Dictionary<string, Vector3> offsets = new Dictionary<string, Vector3>();

        public ArcheryShootOffLineupView(ArcheryCourse course, ActorRegistry actorRegistry,
                                         GameFramework.World.EntityRegistry entityRegistry,
                                         IPlayerContext playerContext)
        {
            this.course = course;
            this.actorRegistry = actorRegistry;
            this.entityRegistry = entityRegistry;
            this.playerContext = playerContext;
        }

        public Vector3 DisplayOffsetOf(string entityId)
            => offsets.TryGetValue(entityId, out var o) ? o : Vector3.zero;

        public void LateTick()
        {
            offsets.Clear();
            var lane = course.SharedLane;
            if (lane == null)
            {
                return;   // 한 발 승부가 아니거나 맵 씬이 아직 안 떴다
            }

            Vector3 right = ArcheryTargetMotion.ShooterRightAxis(-lane.Value.Forward);

            others.Clear();
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Has<ArcheryScore>() && entity.Id != playerContext.entityId)
                {
                    others.Add(entity.Id);
                }
            }
            others.Sort(string.CompareOrdinal);

            for (int i = 0; i < others.Count; i++)
            {
                Vector3 offset = right * ArcheryShootOffLineup.SlotOffset(i);
                offsets[others[i]] = offset;
                if (actorRegistry.TryGet(others[i], out var actor) && actor.visualGameObject != null)
                {
                    var visual = actor.visualGameObject.transform;
                    visual.localPosition = visual.parent.InverseTransformVector(offset);
                }
            }
        }
    }
}
```

`ArcheryArrowView.cs` — 날아가는 화살 위치(현재 `Vector3 position = ArcheryTrajectory.PositionAt(shots[i], seconds);`) 바로 뒤에:

```csharp
                //  한 발 승부: 남의 화살은 화면 속 그 캐릭터의 활에서 떠나 실제 꽂힐 점으로 모인다.
                //  꽂히는 자리는 진짜고, 날아가는 모양만 연출이다(판정은 이 값을 안 본다).
                position += lineupView.DisplayOffsetOf(shots[i].ShooterId)
                          * ArcheryShootOffLineup.ArrowBlend(seconds, FlightSecondsOf(shots[i]));
```

`FlightSecondsOf`: 과녁까지의 수평 거리 ÷ 수평 속도.

```csharp
        private float FlightSecondsOf(in ArcheryShot shot)
        {
            var lane = course.SharedLane;
            if (lane == null)
            {
                return 0f;
            }
            int step = course.IndexAt(shot.FireTick, world.GameplayStartTick);
            float distance = course.StandDistanceAt(step);
            float speed = new Vector2(shot.Velocity.x, shot.Velocity.z).magnitude;
            return speed > 0.01f ? distance / speed : 0f;
        }
```

`ArcheryArrowView` 생성자에 `ArcheryShootOffLineupView lineupView`를 더한다(`course`·`world`가 이미 필드에 없으면 함께 더한다 — 파일을 먼저 읽고 맞출 것). 꽂힌 화살(`impact` 분기)은 건드리지 않는다 — 이미 진짜 자리다.

`ArcheryLifetimeScope.cs`(클라): `builder.RegisterEntryPoint<ArcheryShootOffLineupView>().AsSelf();` 를 `ArcheryArrowView` 등록 **앞**에. (사거리·원형 맵에선 `SharedLane`이 null이라 아무 일도 안 한다.)

⚠️ 실행 순서: 이 뷰의 `LateTick`이 보간기(`LateUpdate`)보다 먼저 돌면 로컬 위치가 같은 프레임에 덮이지 않는다(보간기는 **루트**를 옮기므로 몸통 로컬 위치는 살아남는다). 몸통이 로드될 때 `LOPEntityView`가 몸통의 **월드** 위치를 엔티티 자리로 한 번 맞추는데, 그다음 프레임부터 이 뷰가 로컬 위치를 다시 쓰므로 문제없다. Task 12 실측에서 확인한다.

- [ ] **Step 4: 통과를 확인한다** — `ArcheryShootOffLineupTests` 2개(컴파일은 Task 11 끝에서 통합 확인).
- [ ] **Step 5: 커밋한다** — `feat(archery): 한 발 승부 화면 배치 — 내가 가운데, 남은 좌우`

---

### Task 11: 클라 — HUD(라운드·바람·시간·결과 목록) + 해설 자막

**Files:**
- Create: `Assets/Scripts/Game/ArcheryCommentary.cs`
- Create: `Assets/Scripts/Game/ArcheryShootOffNarrator.cs`
- Create: `Assets/Scripts/UI/ArcheryShootOff/ArcheryShootOffHudViewModel.cs`
- Create: `Assets/Scripts/UI/ArcheryShootOff/ArcheryShootOffHudView.cs`
- Create: `Assets/UI/ArcheryShootOff/ArcheryShootOffHud.uxml`, `ArcheryShootOffHud.uss`
- Modify: `Assets/Scripts/Game/ArcheryHudCoordinator.cs`, `Assets/Scripts/Game/ArcheryLifetimeScope.cs`
- Test: `Assets/Tests/Editor/ArcheryCommentaryTests.cs`, `ArcheryShootOffNarratorTests.cs`

**Interfaces:**
- Consumes: Task 4 `IndexAt`/`RoundCloseTick`/`MultiplierAt`/`WindAt`/`StandDistanceAt`/`StepCount`/`IsShootOff`, Task 5 `ArcheryRoundResultEvent`, `ArcheryTargetHitEvent`(ShootOff에서 `points` = 띠 점수).
- Produces:
  - `public enum ArcheryLine { Wind, Hush, Bull, Close, Streak, Comeback, NoHit, Win, Final }`
  - `public sealed class ArcheryCommentary`
    - 생성자 `(System.Func<int, int> pick)` — `pick(n)`은 `[0,n)` 정수. 시험은 늘 0을 준다.
    - `public static int PriorityOf(ArcheryLine line)` — Win 1, Wind 1, NoHit 2, Bull 3, Close 5, Streak 6, Comeback 6, Hush 7, Final 8.
    - `public bool TrySay(ArcheryLine line, string name, int number, float now, float duration = 2.2f)` — 떠 있는 자막보다 우선순위가 낮으면 거짓.
    - `public string Text` / `public bool IsShowing(float now)`
  - `public sealed class ArcheryShootOffNarrator`
    - `public ArcheryLine LineFor(ArcheryRoundResultEvent result, string myEntityId, out string winnerId, out int number)` — 연승·지난 순위를 기억한다(호출마다 갱신).
  - HUD VM: `RoundLabel`(string), `WindArrow`(float, −1~1 방향·세기), `WindText`(string), `TimeLeft01`(float), `ResultRows`(IReadOnlyList<(string name, int points, string detail)>), `ResultVisible`(bool), `Caption`(string) — 매 프레임 View가 읽는다.

**해설 규칙**(`LineFor`):
1. 1등의 **지난 라운드 순위가 꼴찌**였고 이번이 첫 라운드가 아니면 `Comeback`.
2. 아니면 1등의 연승이 3 이상이면 `Streak`(`number` = 연승).
3. 아니면 1·2등이 모두 맞혔고 거리 차가 0.03m 이하면 `Close`(`number` = cm, 최소 1).
4. 아니면 **내가** 못 맞혔으면 `NoHit`(이름 = 나).
5. 아니면 `Win`.
연승: 1등(순위 0인 사람 전원)은 +1, 나머지는 0. 전원 못 맞힌 라운드는 아무도 1등이 아니다(모두 0으로).

**문장 표**(`{n}` = 이름, `{k}` = 숫자). 이름은 나면 "당신", 남이면 "{이름} 선수" — 이름 조회는 VM이 한다(`Ownership`이 클라에 없으므로 **엔티티 id → 명단 순서의 표시 이름**; 표시 이름을 얻는 기존 경로가 없으면 이번 슬라이스는 "1P/2P/3P/4P"(명단 순서)로 쓴다 — 결정 기록).

```
Wind:     "깃발 보세요, 바람이 만만치 않습니다" / "바람이 {n}쪽으로 붑니다"   ({n} = 왼/오른)
Hush:     "마지막 한 발… 경기장이 조용해집니다" / "점수 두 배! 숨을 죽입니다"
Bull:     "{n}, 정중앙!!" / "10점! {n} 오늘 컨디션 최고!" / "{n}! 이건 교과서입니다!"
Close:    "단 {k}cm!! 숨막히는 차이입니다" / "사진 판독급! {k}cm!"
Streak:   "{k}연승! {n}을 막을 자가 없습니다" / "{n}! {n}! 이름이 연호됩니다!"
Comeback: "꼴찌에서 1등으로! {n} 대역전!" / "{n} 부활합니다!"
NoHit:    "{n}… 이건 조준이 아니라 기도였습니다" / "과녁은 저쪽입니다, {n}"
Win:      "이번 라운드 {n} 가져갑니다" / "{n}, 깔끔합니다" / "이번엔 {n}!"
Final:    "경기 종료! 오늘의 명사수는 {n}!" / "{n}, 우승입니다!"
```

- [ ] **Step 1: 실패하는 시험을 쓴다**

`ArcheryCommentaryTests.cs`:

```csharp
using NUnit.Framework;

namespace LOP.Tests
{
    public class ArcheryCommentaryTests
    {
        private static ArcheryCommentary First() => new ArcheryCommentary(n => 0);

        [Test]
        public void 이름과_숫자를_채운다()
        {
            var c = First();
            Assert.IsTrue(c.TrySay(ArcheryLine.Close, "민수 선수", 2, now: 0f));
            Assert.AreEqual("단 2cm!! 숨막히는 차이입니다", c.Text);
        }

        [Test]
        public void 떠_있는_자막보다_낮은_우선순위는_못_덮는다()
        {
            var c = First();
            c.TrySay(ArcheryLine.Comeback, "당신", 0, now: 0f);
            Assert.IsFalse(c.TrySay(ArcheryLine.Win, "민수 선수", 0, now: 1f));
            Assert.AreEqual("꼴찌에서 1등으로! 당신 대역전!", c.Text);
        }

        [Test]
        public void 시간이_지나면_무엇이든_뜬다()
        {
            var c = First();
            c.TrySay(ArcheryLine.Comeback, "당신", 0, now: 0f);
            Assert.IsFalse(c.IsShowing(3f));
            Assert.IsTrue(c.TrySay(ArcheryLine.Win, "민수 선수", 0, now: 3f));
        }

        [Test]
        public void 같은_우선순위는_덮는다()
        {
            var c = First();
            c.TrySay(ArcheryLine.Streak, "a", 3, now: 0f);
            Assert.IsTrue(c.TrySay(ArcheryLine.Comeback, "b", 0, now: 0.5f));
        }
    }
}
```

`ArcheryShootOffNarratorTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryShootOffNarratorTests
    {
        private static ArcheryRoundResultEvent Round(int index, params (string id, bool hit, float d)[] shots)
        {
            var list = new List<ArcheryRoundShot>();
            foreach (var s in shots) { list.Add(new ArcheryRoundShot(s.id, s.hit, Vector2.zero, s.d)); }
            return new ArcheryRoundResultEvent(index, 1, ArcheryShootOffRanking.Rank(list, 1));
        }

        [Test]
        public void 차이가_3cm_이하면_접전()
        {
            var n = new ArcheryShootOffNarrator();
            var line = n.LineFor(Round(0, ("a", true, 0.10f), ("b", true, 0.12f), ("me", true, 0.5f)), "me",
                                 out string winner, out int cm);
            Assert.AreEqual(ArcheryLine.Close, line);
            Assert.AreEqual("a", winner);
            Assert.AreEqual(2, cm);
        }

        [Test]
        public void 세_번_연속_1등이면_연승()
        {
            var n = new ArcheryShootOffNarrator();
            for (int i = 0; i < 2; i++) { n.LineFor(Round(i, ("a", true, 0.1f), ("me", true, 0.5f)), "me", out _, out _); }
            var line = n.LineFor(Round(2, ("a", true, 0.1f), ("me", true, 0.5f)), "me", out _, out int k);
            Assert.AreEqual(ArcheryLine.Streak, line);
            Assert.AreEqual(3, k);
        }

        [Test]
        public void 지난_라운드_꼴찌가_1등이면_역전()
        {
            var n = new ArcheryShootOffNarrator();
            n.LineFor(Round(0, ("a", true, 0.1f), ("b", true, 0.5f)), "me", out _, out _);
            var line = n.LineFor(Round(1, ("a", true, 0.5f), ("b", true, 0.1f)), "me", out string winner, out _);
            Assert.AreEqual(ArcheryLine.Comeback, line);
            Assert.AreEqual("b", winner);
        }

        [Test]
        public void 첫_라운드는_역전이_아니다()
        {
            var n = new ArcheryShootOffNarrator();
            var line = n.LineFor(Round(0, ("a", true, 0.1f), ("b", true, 0.5f)), "me", out _, out _);
            Assert.AreEqual(ArcheryLine.Win, line);
        }

        [Test]
        public void 내가_못_맞히면_그_얘기()
        {
            var n = new ArcheryShootOffNarrator();
            var line = n.LineFor(Round(0, ("a", true, 0.1f), ("b", true, 0.5f), ("me", false, 0f)), "me",
                                 out string who, out _);
            Assert.AreEqual(ArcheryLine.NoHit, line);
            Assert.AreEqual("me", who);
        }

        [Test]
        public void 전원_못_맞히면_연승이_끊긴다()
        {
            var n = new ArcheryShootOffNarrator();
            for (int i = 0; i < 2; i++) { n.LineFor(Round(i, ("a", true, 0.1f), ("me", true, 0.5f)), "me", out _, out _); }
            n.LineFor(Round(2, ("a", false, 0f), ("me", false, 0f)), "me", out _, out _);
            var line = n.LineFor(Round(3, ("a", true, 0.1f), ("me", true, 0.5f)), "me", out _, out _);
            Assert.AreNotEqual(ArcheryLine.Streak, line);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다** — 컴파일 에러.

- [ ] **Step 3: 순수 두 클래스를 구현한다**

`ArcheryCommentary.cs`:

```csharp
using System.Collections.Generic;

namespace LOP
{
    public enum ArcheryLine { Wind, Hush, Bull, Close, Streak, Comeback, NoHit, Win, Final }

    /// <summary>
    /// 해설 자막 한 줄. 떠 있는 자막보다 덜 중요한 말은 버린다 — "대역전!"이 "이번 라운드 가져갑니다"에
    /// 덮이면 안 된다. 같은 말이 반복되지 않게 종류마다 문장 몇 개 중 하나를 고른다.
    /// </summary>
    public sealed class ArcheryCommentary
    {
        private static readonly Dictionary<ArcheryLine, string[]> Lines = new Dictionary<ArcheryLine, string[]>
        {
            [ArcheryLine.Wind] = new[] { "깃발 보세요, 바람이 만만치 않습니다", "바람이 {n}쪽으로 붑니다" },
            [ArcheryLine.Hush] = new[] { "마지막 한 발… 경기장이 조용해집니다", "점수 두 배! 숨을 죽입니다" },
            [ArcheryLine.Bull] = new[] { "{n}, 정중앙!!", "10점! {n} 오늘 컨디션 최고!", "{n}! 이건 교과서입니다!" },
            [ArcheryLine.Close] = new[] { "단 {k}cm!! 숨막히는 차이입니다", "사진 판독급! {k}cm!" },
            [ArcheryLine.Streak] = new[] { "{k}연승! {n}을 막을 자가 없습니다", "{n}! {n}! 이름이 연호됩니다!" },
            [ArcheryLine.Comeback] = new[] { "꼴찌에서 1등으로! {n} 대역전!", "{n} 부활합니다!" },
            [ArcheryLine.NoHit] = new[] { "{n}… 이건 조준이 아니라 기도였습니다", "과녁은 저쪽입니다, {n}" },
            [ArcheryLine.Win] = new[] { "이번 라운드 {n} 가져갑니다", "{n}, 깔끔합니다", "이번엔 {n}!" },
            [ArcheryLine.Final] = new[] { "경기 종료! 오늘의 명사수는 {n}!", "{n}, 우승입니다!" },
        };

        private readonly System.Func<int, int> pick;
        private float until = float.NegativeInfinity;
        private int priority;

        public string Text { get; private set; } = string.Empty;

        public ArcheryCommentary(System.Func<int, int> pick)
        {
            this.pick = pick;
        }

        public static int PriorityOf(ArcheryLine line)
        {
            switch (line)
            {
                case ArcheryLine.Final: return 8;
                case ArcheryLine.Hush: return 7;
                case ArcheryLine.Streak:
                case ArcheryLine.Comeback: return 6;
                case ArcheryLine.Close: return 5;
                case ArcheryLine.Bull: return 3;
                case ArcheryLine.NoHit: return 2;
                default: return 1;
            }
        }

        public bool IsShowing(float now) => now < until;

        public bool TrySay(ArcheryLine line, string name, int number, float now, float duration = 2.2f)
        {
            int p = PriorityOf(line);
            if (IsShowing(now) && p < priority)
            {
                return false;
            }
            var pool = Lines[line];
            Text = pool[pick(pool.Length)].Replace("{n}", name).Replace("{k}", number.ToString());
            priority = p;
            until = now + duration;
            return true;
        }
    }
}
```

`ArcheryShootOffNarrator.cs`:

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>라운드 결과 → 해설 종류. 연승과 지난 라운드 순위를 기억한다(호출 순서가 곧 라운드 순서).</summary>
    public sealed class ArcheryShootOffNarrator
    {
        private readonly Dictionary<string, int> streaks = new Dictionary<string, int>();
        private readonly Dictionary<string, int> lastRank = new Dictionary<string, int>();
        private int lastCount;

        public ArcheryLine LineFor(ArcheryRoundResultEvent result, string myEntityId,
                                   out string subjectId, out int number)
        {
            var placements = result.placements;
            number = 0;
            subjectId = placements.Count > 0 ? placements[0].ShooterId : string.Empty;

            bool anyHit = placements.Count > 0 && placements[0].Hit;
            var winner = anyHit ? placements[0] : default;
            bool wasLast = anyHit && result.roundIndex > 0
                        && lastRank.TryGetValue(winner.ShooterId, out int prev) && prev == lastCount - 1
                        && lastCount > 1;

            foreach (var p in placements)
            {
                bool first = p.Hit && p.Rank == 0;
                streaks[p.ShooterId] = first ? (streaks.TryGetValue(p.ShooterId, out int s) ? s + 1 : 1) : 0;
            }
            int winnerStreak = anyHit ? streaks[winner.ShooterId] : 0;

            lastRank.Clear();
            foreach (var p in placements) { lastRank[p.ShooterId] = p.Rank; }
            lastCount = placements.Count;

            if (wasLast)
            {
                return ArcheryLine.Comeback;
            }
            if (winnerStreak >= 3)
            {
                number = winnerStreak;
                return ArcheryLine.Streak;
            }
            if (anyHit && placements.Count > 1 && placements[1].Hit
                && placements[1].Distance - winner.Distance <= 0.03f)
            {
                number = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(
                    (placements[1].Distance - winner.Distance) * 100f));
                return ArcheryLine.Close;
            }
            foreach (var p in placements)
            {
                if (p.ShooterId == myEntityId && p.Hit == false)
                {
                    subjectId = myEntityId;
                    return ArcheryLine.NoHit;
                }
            }
            return ArcheryLine.Win;
        }
    }
}
```

- [ ] **Step 4: 순수 시험 통과를 확인한다** — 두 파일 10개.

- [ ] **Step 5: HUD를 만든다**

`ArcheryShootOffHud.uxml` (루트는 입력을 막지 않는다 — 전부 `picking-mode="Ignore"`):

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="ArcheryShootOffHud.uss" />
    <ui:VisualElement name="shootoff-root" class="shootoff-root" picking-mode="Ignore">
        <ui:VisualElement class="shootoff-top" picking-mode="Ignore">
            <ui:Label name="round" class="shootoff-round" picking-mode="Ignore" />
            <ui:VisualElement class="shootoff-time" picking-mode="Ignore">
                <ui:VisualElement name="time-fill" class="shootoff-time-fill" picking-mode="Ignore" />
            </ui:VisualElement>
            <ui:VisualElement name="wind" class="shootoff-wind" picking-mode="Ignore">
                <ui:Label name="wind-arrow" class="shootoff-wind-arrow" picking-mode="Ignore" />
                <ui:Label name="wind-text" class="shootoff-wind-text" picking-mode="Ignore" />
            </ui:VisualElement>
        </ui:VisualElement>
        <ui:Label name="caption" class="shootoff-caption" picking-mode="Ignore" />
        <ui:VisualElement name="result" class="shootoff-result" picking-mode="Ignore" />
    </ui:VisualElement>
</ui:UXML>
```

`ArcheryShootOffHud.uss` — 기존 `ArcheryPad.uss`의 색·폰트 토큰을 그대로 쓴다(파일을 먼저 열어 변수 이름을 맞춘다). 배치: 위쪽 가운데 줄(라운드 · 시간 막대 220px · 바람), 그 아래 자막(어두운 반투명 알약 배경, 노란 🎙 없이 글자만), 결과 목록은 화면 오른쪽 가운데(행마다 "🥇 이름 +3 · 2cm").

`ArcheryShootOffHudViewModel` — 의존: `GameFramework.Runner.IRunner runner`, `ArcheryWorld world`, `ArcheryCourse course`, `IPlayerContext playerContext`, `IRoomDataStore roomDataStore`, `ISubscriber<WorldEventBatchToC> batchSubscriber`. `Tick(float now)`를 View가 매 프레임 부른다.

- 렌더 틱: `ArcheryTargetView`와 같은 식 `(elapsedTime − interval) / interval`.
- 라운드: `index = course.IndexAt(floor(renderTick), world.GameplayStartTick)` → `RoundLabel = $"{index + 1}/{course.StepCount}"` + 배수 2면 `" · 점수 ×2"`. 범위 밖이면 빈 문자열.
- 바람: `course.WindAt(floor(renderTick), start)`를 사수 오른쪽 축(`ShooterRightAxis(-SharedLane.Forward)`)에 내적한 값 `w`. `WindArrow = Clamp(w / 20, −1, 1)`, `WindText = w == 0 ? "" : $"바람 {Mathf.Abs(w):0}"`. View는 부호로 `←`/`→`, 크기로 글자 크기를 정한다.
- 시간: 마지막으로 쏠 수 있는 틱 = `RoundCloseTick(index) − (StandDistanceAt(index) / ArcheryAimSystem.MinSpeed) / interval`(가장 약하게 쏜 화살도 과녁이 사라지기 전에 닿는 시각 — 스펙 §3.5). `TimeLeft01 = Clamp01((마지막 − renderTick) / (마지막 − 라운드 시작 틱))`. 라운드 시작 틱 = `RoundCloseTick(index) − 그 라운드 노출`(`ExposureTicksAt` — Task 4).
- 라운드 결과: `WorldEventBatchToC`에서 `ArcheryRoundResult`를 받으면 `ResultRows`를 채우고 `ResultVisible = true`, 다음 라운드 인덱스가 바뀌면 `false`. 해설은 `narrator.LineFor(...)` → `commentary.TrySay(line, NameOf(subject), number, now)`.
- 적중 사건: `points == 10`이면 `commentary.TrySay(ArcheryLine.Bull, NameOf(shooter), 0, now)`.
- 라운드가 바뀌는 순간: 바람 `|w| >= 15`면 `Wind`(이름 칸에 "왼"/"오른"), 배수 2면 `Hush`.
- 판 끝(인덱스 ≥ StepCount로 처음 바뀔 때): 점수 1등으로 `Final`(동점이면 id 서수가 앞선 사람).
- `NameOf(entityId)`: 나면 "당신", 남이면 명단 순서 번호로 `"{n}P 선수"`. 명단 순서는 `roomDataStore.match.playerList`(userId)에서 얻어야 하는데 클라 엔티티에 userId가 없으면 **엔티티 id 서수 순서**로 번호를 매긴다(Task 10의 배치 순서와 같다 — 화면 좌우 순서와 이름 번호가 일치한다). 결정 원장에 적는다.
- 난수: `new ArcheryCommentary(n => UnityEngine.Random.Range(0, n))`.

`ArcheryShootOffHudView` — 기존 View들과 같은 모양(일반 C# 바인더, UXML 트리 소유, 매 프레임 VM 값을 읽어 라벨 갱신). 기존 `ArcheryPadView`의 창 등록 방식을 그대로 따른다(`IWindowManager`가 여는 View — 파일을 먼저 읽고 같은 기반 클래스/인터페이스를 쓸 것).

`ArcheryHudCoordinator.OnEntityCreated` — 패드 다음에:

```csharp
            if (course.IsShootOff)
            {
                windowManager.Open<ArcheryShootOffHudView>();
            }
```

(생성자에 `ArcheryCourse course` 추가.) `ArcheryLifetimeScope`(클라)에 VM·View 등록과 `RegisterViewFactories`에 View 팩토리 한 줄. 새 의존을 더했으니 등록처를 다시 센다(메모리 `new-ctor-dependency-misses-a-scope`): `ArcheryRemoteShotHandler`·`ArcheryArrowView`·`ArcheryHudCoordinator`는 활쏘기 스코프에서만 등록되는지 `grep -rn "ArcheryArrowView\|ArcheryRemoteShotHandler\|ArcheryHudCoordinator" Assets/Scripts --include=*LifetimeScope.cs`로 확인.

- [ ] **Step 6: 통합 컴파일과 시험을 확인한다** — 클라 워크트리 코드는 에디터가 못 본다. **클라 브랜치를 메인 체크아웃으로 가져와 확인**한다: 메인 체크아웃에서 로컬 픽스처를 `git stash push -u -m shootoff-fixtures` → `git checkout docs/archery-shoot-off`(워크트리가 그 브랜치를 쥐고 있으면 먼저 `git worktree remove ../wt-client-shootoff` — 커밋이 다 되어 있어야 한다) → `git stash pop`(Assets/Art 포인터가 달라도 서브모듈 워킹트리는 건드리지 않는다 — 픽스처 그대로) → 에디터 재컴파일 → 콘솔 CS 에러 0(시각 대조) → 클라 EditMode 전체("Run finished" 줄 + TestResults.xml). Shared·서버도 이 시점에 둘 다 전체 스위트를 한 번 더 돌린다.
- [ ] **Step 7: 커밋한다** — Unity가 만든 `.meta`(새 폴더 `UI/ArcheryShootOff`, `Scripts/UI/ArcheryShootOff` 포함)까지. `feat(archery): 한 발 승부 HUD와 해설 자막`

---

### Task 12: 두 클라 실측 → 수치 조정 → 배포

**Files:**
- Modify(필요 시): `infrastructure/table/Datas/#ArcheryRange.xlsx` (바람·크기·움직임 수치)
- Modify: `LeagueOfPhysical-Client/docs/ROADMAP.md`

- [ ] **Step 1: 로컬 판을 세운다** — 로컬 k8s(kind `lop`)가 떠 있는지(`kubectl get pods`), 서버는 로컬에서 **열린 씬**을 실행하므로 게임서버 에디터에 활쏘기 게임 씬을 연다. 두 클라(메인 + MPPM 클론, 클론은 사람이 켜야 한다 — 사용자에게 요청)를 `unity` CLI로 몬다(메모리 `driving-both-clients-via-unity-cli`, `two-client-test-setup-traps`). 맵 7을 고른다.
- [ ] **Step 2: 잰다** — 한 판을 끝까지.
  - 네 캐릭터가 한 점에서 **밀리지 않는가**(Review Focus 5) — 서버 로그/스냅샷의 위치가 사대 그대로인가.
  - 화면: 내가 가운데, 남이 좌우. 남의 화살이 그 캐릭터 쪽에서 떠나 과녁에 꽂히는가.
  - 바람 표시가 보이고, 바람 라운드에서 화살이 표시 방향으로 밀리는가(두 클라에서 같은 자리에 꽂히는가 — Review Focus 1).
  - 라운드마다 결과 목록이 뜨고 점수가 맞는가(서버 로그의 `ArcheryRoundResultEvent`와 대조).
  - 해설이 흐름을 끊지 않고 뜨는가. 한 판에 몇 줄인가(목표 약 30).
  - 판 길이(목표 1분 50초 안팎).
- [ ] **Step 3: 수치를 고친다** — 바람이 너무 약하거나 강하면 `wind_mps2`, 과녁이 너무 작으면 `face_radius_m`. `gen.sh` 다시, 레포 넷 커밋.
- [ ] **Step 4: ROADMAP을 갱신한다** — "한 발 승부 슬라이스 1" 행(무엇을 했나, 실측 결과, 다음 = 슬라이스 2 연출). 사거리 맵은 연습 모드로 남는다고 적는다.
- [ ] **Step 5: 사용자에게 보고하고 푸시 승인을 받는다.** 승인 뒤 레포마다 푸시 규약 여섯 줄(한 줄씩 확인). 순서: **Shared → MasterData-Client → MasterData-Server → infrastructure → lop-backend → Server → Client**(클·서가 새 Shared를 기대하므로 Shared 먼저). 그다음 배포:
  - `gameserver-deploy`(서버 코드·Shared·마스터데이터가 바뀌었다 — 이미지 재빌드).
  - `backend-deploy`에서 **matchmaking-server**(맵 7을 알아야 매칭이 고른다 — 안 하면 로비에 맵이 보여도 매칭이 못 고른다).
  - 씬은 안 바뀌었으므로 `content-deploy`는 필요 없다.
