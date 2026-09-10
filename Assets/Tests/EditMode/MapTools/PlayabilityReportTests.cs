using System.Collections.Generic;
using LOP;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class PlayabilityReportTests
    {
        static FlappyConfig Config()
            => new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                                stunTime: 0.8f, invulnTime: 0.6f,
                                dashMult: 2f, dashDuration: 0.2f, dashChargeBase: 0.13f, dashChargeDive: 1.2f,
                                chaserStartX: -60f, chaserInitialSpeed: 7f,
                                chaserAcceleration: 0.075f, chaserMaxSpeed: 10f);

        static string Build(params SpawnCleanRun[] runs)
            => PlayabilityReport.Build("FlappyRaceMap", -2f, 632f, Config(), runs,
                                       trapSection: "  낀 자리 없음.",
                                       budget: new List<StunBudgetPoint>
                                       {
                                           new StunBudgetPoint(10f, 108f, 10, 7),
                                       },
                                       earliest: new EarliestCatch(true, 19.0f, 14),
                                       heightGrid: 0.1f, minY: -40f, maxY: 40f);

        //  이모지 하나만 담긴 검색어는 NUnit의 StringAssert.Contains(문화권 비교)가 오탐한다
        //  — 이 환경에서 실측: 어떤 문자열에도 "✅"가 "있다"고 나온다(약한 콜레이션이
        //  기호를 사실상 와일드카드로 만든다). 숫자·기호가 많은 검색어(x=, y=-, %, / 등)도
        //  같은 위험이 있다 — 부호 하나 뒤집혀도 약한 콜레이션 아래서는 "있다"고 나올 수
        //  있다. 그래서 값 검증은 전부 순서(ordinal) 비교로 한다.
        static bool Contains(string haystack, string needle)
            => haystack.Contains(needle, System.StringComparison.Ordinal);

        static string BuildWithSweep(IReadOnlyList<HeightSweepRow> sweep, params SpawnCleanRun[] runs)
            => PlayabilityReport.Build("FlappyRaceMap", -2f, 632f, Config(), runs,
                                       trapSection: "  낀 자리 없음.",
                                       budget: new List<StunBudgetPoint>
                                       {
                                           new StunBudgetPoint(10f, 108f, 10, 7),
                                       },
                                       earliest: new EarliestCatch(true, 19.0f, 14),
                                       heightGrid: 0.1f, minY: -40f, maxY: 40f, heightSweep: sweep);

        static int Count(string haystack, string needle)
        {
            int count = 0;
            for (int at = 0; ; count++)
            {
                at = haystack.IndexOf(needle, at, System.StringComparison.Ordinal);
                if (at < 0) { return count; }
                at += needle.Length;
            }
        }

        //  🟡 한 자리 — 봇이 못 갔고 탐색은 찾은, 실제 맵의 네 자리가 전부 이랬던 그 상태.
        //  진단 값은 인자로 받아 테스트마다 갈아 끼운다.
        static SpawnCleanRun Unproven(BotDiagnostics bot, ReplayMismatch replay = default)
            => new SpawnCleanRun("PlayerSpawn_2", -1f,
                                 new CleanRunResult(true, new bool[191], 0f, 0f, 0, 0f),
                                 verifiedByReplay: false, botReached: false, botFlaps: 51,
                                 bot: bot, spawnInsideTerrain: false, replay: replay);

        static BotDiagnostics Hit(string colliderPath, float verticalSpeed)
            => new BotDiagnostics(endX: 177.9f, endY: 0.8f, touched: true, ticks: 818, blindTicks: 0,
                                  farthestX: 177.9f, tickLimit: 3482,
                                  hitColliderPath: colliderPath, hitVerticalSpeed: verticalSpeed);

        [Test]
        public void 자리마다_한_줄씩_찍는다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), true, botReached: false, botFlaps: 0, bot: default),
                new SpawnCleanRun("PlayerSpawn_4", 9f, new CleanRunResult(false, new bool[0], 38.2f, 31f, 34, 0.6f), false, botReached: false, botFlaps: 0, bot: default));

            StringAssert.Contains("PlayerSpawn_1", report);
            StringAssert.Contains("PlayerSpawn_4", report);
        }

        [Test]
        public void 자리마다_답이_다르면_공정성_경고를_찍는다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), true, botReached: false, botFlaps: 0, bot: default),
                new SpawnCleanRun("PlayerSpawn_4", 9f, new CleanRunResult(false, new bool[0], 38.2f, 31f, 34, 0.6f), false, botReached: false, botFlaps: 0, bot: default));

            //  "넷 중 하나라도 되면 통과"로 읽히면 안 된다 — 자리 배정이 곧 불이익이다.
            StringAssert.Contains("자리 배정", report);
        }

        [Test]
        public void 모두_같으면_공정성_경고가_없다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), true, botReached: false, botFlaps: 0, bot: default),
                new SpawnCleanRun("PlayerSpawn_2", -1f, new CleanRunResult(true, new bool[209], 0f, 0f, 0, 0f), true, botReached: false, botFlaps: 0, bot: default));

            StringAssert.DoesNotContain("자리 배정", report);
        }

        [Test]
        public void 막힌_자리는_어디서_끊겼는지와_최협_회랑을_같이_찍는다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_4", 9f, new CleanRunResult(false, new bool[0], 38.2f, 31f, 34, 0.6f), false, botReached: false, botFlaps: 0, bot: default));

            Assert.IsTrue(Contains(report, "38.2"));
            //  세 필드(x/생존/높이 폭)가 서로 바뀌어도 위 "최협 회랑" 한 줄 존재 검사는 못
            //  잡는다 — 세 값이 서로 다르므로(31.0 / 34 / 0.6), 라벨+값을 붙여 확인하면
            //  둘 중 어느 자리가 뒤바뀌어도 반드시 하나는 어긋난다.
            Assert.IsTrue(Contains(report, "x=31.0"));
            Assert.IsTrue(Contains(report, "생존 34"));
            Assert.IsTrue(Contains(report, "높이 폭 0.6m"));
            //  spec §8 — 실패는 눈금 탓일 수도 있어 되짚어 볼 안내를 같이 준다.
            //  "눈금"만 찾으면 공허하다 — 머리말이 늘 "높이눈금 0.10"을 찍으므로 ❌ 처방을
            //  통째로 지워도 초록이었다(리뷰어가 돌연변이로 확인). 그 처방에만 있는
            //  문자열로 짚는다.
            Assert.IsTrue(Contains(report, "0.05로 줄여"));
        }

        [Test]
        public void 최협_회랑이_측정_안_됐으면_숫자_대신_이유를_적는다()
        {
            //  R11 — NarrowestCount == 0은 "0폭 회랑을 쟀다"가 아니라 "출발 직후 과도기에
            //  막혀 회랑 측정 자체를 못 했다"는 뜻이다. 숫자를 그대로 찍으면 사람이 없는
            //  좌표를 고치러 간다.
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_4", 9f, new CleanRunResult(false, new bool[0], 3.1f, 0f, 0, 0f), false, botReached: false, botFlaps: 0, bot: default));

            StringAssert.Contains("측정 안", report);
            StringAssert.DoesNotContain("최협 회랑 x=0", report);
        }

        [Test]
        public void 재생으로_증명되지_않은_성공은_그렇다고_적는다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), false, botReached: false, botFlaps: 0, bot: default));

            StringAssert.Contains("재생이 어긋", report);
        }

        [Test]
        public void 증명되지_않은_안내는_눈금_처방을_주지_않는다()
        {
            //  🟡의 원인은 반올림 편향의 누적이지 눈금 굵기가 아니다 — docs/ROADMAP.md가
            //  이미 "눈금을 좁혀도 소용없다"고 결론 냈다. 옛 합쳐진 문장(❌/🟡 공용 처방)으로
            //  되돌아가면 이 안 맞는 처방이 다시 붙는다 — 그 회귀를 여기서 잡는다.
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), false, botReached: false, botFlaps: 0, bot: default));

            StringAssert.Contains("ROADMAP", report);
            StringAssert.DoesNotContain("줄여", report);
        }

        [Test]
        public void 증명되지_않은_성공은_증명된_성공의_글자를_쓰지_않는다()
        {
            //  ✅는 spec §3.7이 "재생으로 증명됨"으로 정의한 글자다. 재생이 어긋난 성공까지
            //  ✅를 찍으면 읽는 사람이 증명된 것과 증명 안 된 것을 구분할 수 없다 — 실제 맵의
            //  네 자리가 전부 이 경우였다(탐색은 찾았지만 재생 전부 실패).
            string proven = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), true, botReached: false, botFlaps: 0, bot: default));
            string unproven = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), false, botReached: false, botFlaps: 0, bot: default));

            Assert.IsTrue(Contains(proven, "✅"));
            Assert.IsFalse(Contains(unproven, "✅"));
            //  증명 안 된 성공은 실패(❌)와도 다른 제 글자를 가져야 한다.
            Assert.IsFalse(Contains(unproven, "❌"));
            Assert.IsTrue(Contains(unproven, "🟡"));
        }

        [Test]
        public void 통과여부는_같아도_증명_여부가_갈리면_공정성_경고를_찍는다()
        {
            //  둘 다 클린런은 "된다"이지만 하나는 증명됐고 하나는 안 됐다 — 자리마다 안전을
            //  확신할 수 있는 정도가 다르다는 뜻이라, 이것도 공정성 문제로 알려야 한다.
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), true, botReached: false, botFlaps: 0, bot: default),
                new SpawnCleanRun("PlayerSpawn_2", -1f, new CleanRunResult(true, new bool[209], 0f, 0f, 0, 0f), false, botReached: false, botFlaps: 0, bot: default));

            //  "증명"만 찾으면 공허하다 — 바로 옆 🟡 처방 문단이 "…재생에서 어긋나 증명하지
            //  못했다…"로 그 낱말을 공짜로 주고, 그 문단은 이 경고가 뜰 수 있는 모든 상황에
            //  함께 뜬다. 그래서 검사 대상 분기를 통째로 지워도 초록이었다(리뷰어가 돌연변이로
            //  확인). 이 경고에만 있는 문자열로 짚는다.
            Assert.IsTrue(Contains(report, "일부 자리만 증명됨"));
        }

        [Test]
        public void 예산에_상한_가정을_함께_적는다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), true, botReached: false, botFlaps: 0, bot: default));

            Assert.IsTrue(Contains(report, "19.0"));
            Assert.IsTrue(Contains(report, "14번째"));
            //  이 숫자가 상한이라는 사실을 안 적으면 읽는 사람이 안전선으로 오해한다.
            StringAssert.Contains("실제는 이보다 나쁘다", report);
        }

        [Test]
        public void 봇이_통과한_자리는_증명된_것으로_찍는다()
        {
            string report = Build(new SpawnCleanRun("PlayerSpawn_1", -6f,
                new CleanRunResult(true, new bool[0], 0f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: true, botFlaps: 176, bot: default));

            Assert.IsTrue(Contains(report, "✅"));
            Assert.IsFalse(Contains(report, "🟡"));
            //  봇이 통과했으면 그 자리엔 탐색을 돌리지 않았다는 사실이 읽혀야 한다.
            StringAssert.Contains("봇 통과", report);
            Assert.IsTrue(Contains(report, "176"));
        }

        [Test]
        public void 지형에_파묻힌_스폰은_세_글자_어디에도_안_들어간다()
        {
            //  ①의 구멍: 스폰이 슬래브에 박혀 있으면 봇이 슬래브 안을 미끄러져 결승선에
            //  닿아 ✅가 나왔다(캡슐 스윕은 출발 자리의 겹침을 보고하지 않는다). 그건
            //  "통과"가 아니라 "검사 불가"다 — ✅·🟡·❌ 셋 중 아무것도 쓰면 안 된다.
            string report = Build(new SpawnCleanRun("PlayerSpawn_3", 2f,
                new CleanRunResult(false, new bool[0], 0f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: false, botFlaps: 0, bot: default,
                spawnInsideTerrain: true));

            Assert.IsFalse(Contains(report, "✅"));
            Assert.IsFalse(Contains(report, "🟡"));
            Assert.IsFalse(Contains(report, "❌"));
            Assert.IsTrue(Contains(report, "스폰이 지형 안"));
            //  눈에 띄게 — 자리 줄 하나로 끝내지 않고 요약 경고도 함께 찍는다.
            Assert.IsTrue(Contains(report, "지형에 파묻힌 스폰이 있다"));
        }

        [Test]
        public void 파묻힌_스폰은_통과로도_실패로도_세지_않는다()
        {
            //  파묻힌 자리를 "된다"나 "안 된다" 한쪽으로 세면 옆 자리와 짝지어 엉뚱한
            //  공정성 경고가 뜨고(✅ 하나 + 파묻힘 하나 → "일부 자리만 불가"), ❌ 전용
            //  눈금 처방까지 딸려 온다.
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[0], 0f, 0f, 0, 0f),
                                  verifiedByReplay: false, botReached: true, botFlaps: 176, bot: default),
                new SpawnCleanRun("PlayerSpawn_3", 2f, new CleanRunResult(false, new bool[0], 0f, 0f, 0, 0f),
                                  verifiedByReplay: false, botReached: false, botFlaps: 0, bot: default,
                                  spawnInsideTerrain: true));

            Assert.IsFalse(Contains(report, "일부 자리만 불가"));
            Assert.IsFalse(Contains(report, "0.05로 줄여"));
            //  ✅ 자리는 그대로 ✅여야 한다 — 파묻힌 자리가 옆 자리의 판정을 지우면 안 된다.
            Assert.IsTrue(Contains(report, "봇 통과"));
        }

        [Test]
        public void 봇도_탐색도_실패하면_불가능으로_찍는다()
        {
            string report = Build(new SpawnCleanRun("PlayerSpawn_4", 9f,
                new CleanRunResult(false, new bool[0], 38.2f, 31f, 34, 0.6f),
                verifiedByReplay: false, botReached: false, botFlaps: 51, bot: default));

            Assert.IsTrue(Contains(report, "❌"));
            Assert.IsFalse(Contains(report, "✅"));
            //  ❌ 줄의 x는 탐색이 막힌 지점이다 — 라벨 없이 "x="만 찍으면 바로 아래 봇
            //  자리의 x와 구분이 안 된다(Fix round 3, Important 1).
            Assert.IsTrue(Contains(report, "탐색 x=38.2에서 막힘"));
        }

        [Test]
        public void 봇은_실패했는데_탐색이_찾으면_모름으로_찍는다()
        {
            string report = Build(new SpawnCleanRun("PlayerSpawn_2", -1f,
                new CleanRunResult(true, new bool[191], 0f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: false, botFlaps: 51, bot: default));

            Assert.IsTrue(Contains(report, "🟡"));
            Assert.IsFalse(Contains(report, "✅"));
            //  이 상태의 뜻이 "봇 한계일 수 있다"라는 것이 글로 남아야 한다.
            StringAssert.Contains("봇이 못 간 것", report);
            //  🟡 줄의 날갯짓 수는 탐색 경로의 것이다 — 라벨 없이 찍으면 바로 아래 봇의
            //  실제 날갯짓 수와 헷갈린다(Fix round 3, Important 1). 이 픽스처는 bool[191]이
            //  전부 false라 CountFlaps==0.
            Assert.IsTrue(Contains(report, "탐색 경로 날갯짓 0회"));
        }

        //  ── Fix round 2 — 진단 ──────────────────────────────────────
        //  봇이 실패했을 때 "어디서 왜 멈췄는지"가 리포트에 안 남으면, 네 자리가 전부
        //  🟡/❌로 나와도 "장애물 하나가 문제인지 봇이 약한 것인지" 구분할 방법이 없다.
        //  아래 테스트들은 그 진단 수치(끝난 자리·이유·틱·목표 못 찾은 틱)가 실제로
        //  리포트 문자열에 박히는지 확인한다.

        [Test]
        public void 실패한_자리는_봇이_멈춘_위치와_이유를_찍는다()
        {
            //  코스는 Build()에서 x −2 → 632 (634m). endX=60이면 올바른 분모((finishX−startX))로는
            //  (60−(−2))/634*100 ≈ 9.78% → "10%"인데, startX를 빼먹은 틀린 식(60/632*100
            //  ≈ 9.49%)은 "9%"로 반올림된다 — 두 식이 화면에 다른 정수로 찍혀야 어느 식을
            //  썼는지 테스트가 실제로 가려낼 수 있다(전 라운드의 endX=143.82는 두 식 다
            //  "23%"로 같이 반올림돼 아무것도 못 가렸다 — 리뷰에서 지적됨).
            string report = Build(new SpawnCleanRun("PlayerSpawn_2", -1f,
                new CleanRunResult(true, new bool[191], 0f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: false, botFlaps: 51,
                bot: new BotDiagnostics(endX: 60f, endY: -57.6f, touched: true, ticks: 812, blindTicks: 40,
                                        farthestX: 60f, tickLimit: 3248)));

            Assert.IsTrue(Contains(report, "x=60.0"));
            Assert.IsTrue(Contains(report, "y=-57.6"));
            Assert.IsTrue(Contains(report, "코스 10%"));
            //  분모에서 startX를 빼먹으면 이 자리에 "9%"가 찍힌다 — 그 회귀를 여기서 잡는다.
            Assert.IsFalse(Contains(report, "코스 9%"));
            Assert.IsTrue(Contains(report, "닿음"));
            //  틱 수는 예산(TickLimit) 대비 비율과 함께 찍혀야 한다 — 812라는 숫자만으로는
            //  크고 작음을 판단할 수 없다.
            Assert.IsTrue(Contains(report, "812/3248틱(25%)"));
            //  이 값이 핵심이다 — 탐색 경로의 날갯짓(위 줄, 0회)과 봇 자신의 날갯짓(51회)이
            //  섞이면 "봇이 몇 번 날갯짓했나"를 완전히 잘못 짚는다. 검색 카운트를 대신
            //  찍어도(버그) 이 fixture는 CountFlaps==0이라 discriminable하다.
            Assert.IsTrue(Contains(report, "봇 날갯짓 51회"));
            Assert.IsTrue(Contains(report, "목표 없음 40틱"));
        }

        [Test]
        public void 안_닿고_틱만_소진하면_그렇게_적는다()
        {
            //  Touched=false — 아무것도 안 닿았는데 제한 틱을 다 썼다는 뜻이라 "닿음"과는
            //  다른 처방(제자리를 맴돌았다 등)이 필요하다. 원인 글자가 갈려야 한다.
            //  ticks==tickLimit로 예산을 정확히 다 썼다는 경우도 함께 확인한다.
            string report = Build(new SpawnCleanRun("PlayerSpawn_3", 2f,
                new CleanRunResult(false, new bool[0], 5f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: false, botFlaps: 3,
                bot: new BotDiagnostics(endX: -1f, endY: 0f, touched: false, ticks: 2000, blindTicks: 1990,
                                        farthestX: -1f, tickLimit: 2000)));

            Assert.IsTrue(Contains(report, "틱 소진(못 닿음)"));
            Assert.IsTrue(Contains(report, "2000/2000틱(100%)"));
        }

        [Test]
        public void 실패한_자리의_진단은_불가능_줄에도_찍는다()
        {
            //  🟡뿐 아니라 ❌(탐색도 못 찾음)도 봇이 실제로 날았던 자리다 — 진단이 빠지면
            //  안 된다. 이 픽스처는 부딪혀 뒤로 밀린 경우도 겸한다: FarthestX(41.5)가
            //  EndX(38.0)보다 앞이다 — 최고 도달점도 함께 찍혀야 "실제로 얼마나 갔었는지"를
            //  과소평가하지 않는다.
            string report = Build(new SpawnCleanRun("PlayerSpawn_4", 9f,
                new CleanRunResult(false, new bool[0], 38.2f, 31f, 34, 0.6f),
                verifiedByReplay: false, botReached: false, botFlaps: 51,
                bot: new BotDiagnostics(endX: 38.0f, endY: 12.5f, touched: true, ticks: 950, blindTicks: 12,
                                        farthestX: 41.5f, tickLimit: 1900)));

            Assert.IsTrue(Contains(report, "x=38.0"));
            Assert.IsTrue(Contains(report, "y=12.5"));
            Assert.IsTrue(Contains(report, "목표 없음 12틱"));
            //  BlockedX(38.2, 탐색이 막힌 지점)와 EndX(38.0, 봇이 멈춘 지점)는 다른 값인데
            //  둘 다 소수점 한 자리라 라벨 없이는 거의 안 갈린다 — 라벨이 있는지 확인한다.
            Assert.IsTrue(Contains(report, "탐색 x=38.2에서 막힘"));
            //  최고 도달점 — free data(FarthestX)가 실제로 찍혀야 한다.
            Assert.IsTrue(Contains(report, "최고 도달 x=41.5"));
            Assert.IsTrue(Contains(report, "코스 7%"));   // 최고 도달점의 퍼센트 — (41.5+2)/634*100 ≈ 6.86% → 7%
        }

        [Test]
        public void 증명된_자리는_봇_진단을_안_찍는다()
        {
            //  ✅는 "부검할 실패"가 없다 — Bot에 값이 들어 있어도(방어적 쓰레기값) 리포트가
            //  그걸 읽으면 안 된다.
            string report = Build(new SpawnCleanRun("PlayerSpawn_1", -6f,
                new CleanRunResult(true, new bool[0], 0f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: true, botFlaps: 176,
                bot: new BotDiagnostics(endX: 999f, endY: 999f, touched: true, ticks: 1, blindTicks: 1,
                                        farthestX: 999f, tickLimit: 1)));

            StringAssert.DoesNotContain("목표 없음", report);
            StringAssert.DoesNotContain("x=999.0", report);
            StringAssert.DoesNotContain("최고 도달", report);
        }

        //  ── Fix round 3 ─────────────────────────────────────────────

        [Test]
        public void 봇_진단이_없으면_0을_찍지_않고_없다고_적는다()
        {
            //  default(BotDiagnostics)의 Ticks는 0이다 — 실제로 봇을 날린 적이 없다는
            //  뜻이다(FlyBot은 최소 1틱은 돌고서야 return한다). 0들을 그대로 찍으면
            //  "x=0.0에서 죽었다"처럼 측정값으로 보인다 — 최협 회랑의 "측정 안 됨"과 같은
            //  원칙으로, 재지 못했으면 쟀다고 말하면 안 된다.
            string report = Build(new SpawnCleanRun("PlayerSpawn_4", 9f,
                new CleanRunResult(false, new bool[0], 38.2f, 31f, 34, 0.6f),
                verifiedByReplay: false, botReached: false, botFlaps: 0, bot: default));

            Assert.IsFalse(Contains(report, "x=0.0"));
            Assert.IsTrue(Contains(report, "봇: 측정 안 됨"));
        }
        //  ── Task 15 — ①이 자기 결론의 근거를 보인다 ─────────────────
        //  "봇이 못 갔다"만 있고 무엇이 막았는지가 없으면, 결과를 받아 든 사람이 *맵이 어려운
        //  것*인지 *도구가 못 푸는 것*인지 가릴 수 없다. 아래 테스트들은 그 근거(무엇에·어느
        //  방향으로 닿았나, 재생이 몇 틱째 어디서 갈렸나, 시작 높이를 바꾸면 어떻게 되나)가
        //  실제로 리포트 문자열에 박히는지 확인한다.

        [Test]
        public void 닿은_원인은_콜라이더_이름과_방향과_세로속도까지_찍는다()
        {
            string report = Build(Unproven(Hit("지붕슬래브/Cube_77", 12.4f)));

            //  세 정보가 다 있어야 한다 — 하나라도 빠지면 씬을 열어 눈으로 찾아야 한다.
            Assert.IsTrue(Contains(report, "지붕슬래브/Cube_77"));
            Assert.IsTrue(Contains(report, "오르다 부딪힘"));
            Assert.IsTrue(Contains(report, "(vy=+12.4)"));
        }

        [Test]
        public void 세로속도의_부호가_오르다와_떨어지다를_가른다()
        {
            //  부호가 이 줄의 전부다 — 부호를 잃으면 "천장에 박았나 바닥에 박았나"를 못 읽는다.
            //  같은 콜라이더에 vy만 뒤집어, 방향 문구가 부호를 실제로 따라가는지 본다.
            string rising = Build(Unproven(Hit("지붕슬래브/Cube_77", 12.4f)));
            string falling = Build(Unproven(Hit("지붕슬래브/Cube_77", -30f)));

            Assert.IsTrue(Contains(falling, "떨어지다 부딪힘"));
            Assert.IsTrue(Contains(falling, "(vy=-30.0)"));
            Assert.IsFalse(Contains(falling, "오르다 부딪힘"));
            Assert.IsFalse(Contains(rising, "떨어지다 부딪힘"));
        }

        [Test]
        public void 완주한_자리는_닿은_원인_줄을_안_찍는다()
        {
            //  ✅는 부검할 실패가 없다 — Bot에 값이 들어 있어도 읽으면 안 된다.
            string report = Build(new SpawnCleanRun("PlayerSpawn_1", -6f,
                new CleanRunResult(true, new bool[0], 0f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: true, botFlaps: 176,
                bot: Hit("지붕슬래브/Cube_77", 12.4f)));

            Assert.IsFalse(Contains(report, "지붕슬래브/Cube_77"));
            Assert.IsFalse(Contains(report, "부딪힘"));
        }

        [Test]
        public void 안_닿고_멈춘_비행은_닿은_원인_줄을_안_찍는다()
        {
            //  틱을 다 써서 멈춘 것은 "무엇에 닿았다"가 아니다 — 남아 있던 옛 콜라이더 이름을
            //  그대로 찍으면 없는 충돌을 있다고 보고하는 셈이다.
            string report = Build(Unproven(new BotDiagnostics(
                endX: 60f, endY: 3f, touched: false, ticks: 2000, blindTicks: 1990,
                farthestX: 60f, tickLimit: 2000,
                hitColliderPath: "지붕슬래브/Cube_77", hitVerticalSpeed: 12.4f)));

            Assert.IsFalse(Contains(report, "지붕슬래브/Cube_77"));
            Assert.IsFalse(Contains(report, "부딪힘"));
        }

        [Test]
        public void 재생이_어긋난_자리는_몇_틱째_어디서_갈렸는지_찍는다()
        {
            //  봇의 값(x=177.9 / 지붕슬래브)과 재생의 값(x=143.8 / 기둥)을 일부러 다르게 둔다 —
            //  두 줄이 서로의 숫자를 베껴 찍어도 테스트가 알아채야 한다.
            string report = Build(Unproven(
                Hit("지붕슬래브/Cube_77", 12.4f),
                new ReplayMismatch(detected: true, tick: 812, x: 143.8f, y: -6.3f,
                                   verticalSpeed: -18.5f, colliderPath: "기둥/Cube_12")));

            Assert.IsTrue(Contains(report, "재생 어긋남: 812틱째"));
            Assert.IsTrue(Contains(report, "x=143.8"));
            Assert.IsTrue(Contains(report, "y=-6.3"));
            Assert.IsTrue(Contains(report, "(vy=-18.5)"));
            Assert.IsTrue(Contains(report, "기둥/Cube_12"));
        }

        [Test]
        public void 재생을_돌리지_않았으면_어긋남_줄이_없다()
        {
            //  default(ReplayMismatch)는 Detected=false — 재생을 안 돌렸다는 뜻이다.
            //  0틱째 x=0.0에서 갈렸다고 찍으면 재지 않은 것을 쟀다고 말하는 셈이다.
            string report = Build(Unproven(Hit("지붕슬래브/Cube_77", 12.4f)));

            Assert.IsFalse(Contains(report, "재생 어긋남"));
            Assert.IsFalse(Contains(report, "0틱째"));
        }

        [Test]
        public void 높이_훑기는_준_줄_수만큼_찍고_각_줄의_결과를_구분한다()
        {
            var sweep = new List<HeightSweepRow>
            {
                new HeightSweepRow(-8f, reached: false, spawnBlocked: false,
                                   bot: Hit("지붕슬래브/Cube_77", 12.4f)),
                new HeightSweepRow(-7f, reached: true, spawnBlocked: false, bot: default),
                new HeightSweepRow(-6f, reached: false, spawnBlocked: true, bot: default),
            };
            string report = BuildWithSweep(sweep, Unproven(Hit("기둥/Cube_12", -30f)));

            Assert.IsTrue(Contains(report, "시작 높이 훑기"));
            Assert.AreEqual(3, Count(report, "  시작 y="));
            Assert.IsTrue(Contains(report, "시작 y=-8.0"));
            Assert.IsTrue(Contains(report, "지붕슬래브/Cube_77"));
            Assert.IsTrue(Contains(report, "시작 y=-7.0"));
            Assert.IsTrue(Contains(report, "골인"));
            Assert.IsTrue(Contains(report, "시작 y=-6.0"));
            Assert.IsTrue(Contains(report, "그 높이가 지형 안"));
        }

        [Test]
        public void 훑지_않았으면_높이_훑기_절이_아예_없다()
        {
            //  빈 표를 찍으면 "훑었는데 아무것도 없었다"로 읽힌다 — 안 훑은 것과 다르다.
            string notSwept = Build(Unproven(Hit("지붕슬래브/Cube_77", 12.4f)));
            string emptySweep = BuildWithSweep(new List<HeightSweepRow>(),
                                               Unproven(Hit("지붕슬래브/Cube_77", 12.4f)));

            Assert.IsFalse(Contains(notSwept, "시작 높이 훑기"));
            Assert.IsFalse(Contains(emptySweep, "시작 높이 훑기"));
        }

        [Test]
        public void 높이_훑기는_판정에_섞이지_않는다()
        {
            //  훑기는 맵의 스폰이 아닌 자리다 — 거기서 실패했다고 "이 맵은 일부 자리만 불가"가
            //  되면 안 되고, ❌ 전용 눈금 처방도 딸려 오면 안 된다.
            var sweep = new List<HeightSweepRow>
            {
                new HeightSweepRow(-8f, reached: false, spawnBlocked: false,
                                   bot: Hit("지붕슬래브/Cube_77", 12.4f)),
                new HeightSweepRow(-7f, reached: false, spawnBlocked: true, bot: default),
            };
            string report = BuildWithSweep(sweep, new SpawnCleanRun("PlayerSpawn_1", -6f,
                new CleanRunResult(true, new bool[0], 0f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: true, botFlaps: 176, bot: default));

            Assert.IsFalse(Contains(report, "일부 자리만 불가"));
            Assert.IsFalse(Contains(report, "0.05로 줄여"));
            Assert.IsFalse(Contains(report, "❌"));
            Assert.IsFalse(Contains(report, "지형에 파묻힌 스폰이 있다"));
            Assert.IsTrue(Contains(report, "봇 통과"));
        }
    }
}
