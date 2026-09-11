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

        [Test]
        public void 봇_줄은_누르려다_막힌_틱과_누를_뜻이_없던_틱을_따로_찍는다()
        {
            //  이 두 숫자가 다음에 무엇을 고칠지 정한다 — 거부가 압도적이면 아치가 통로에
            //  안 들어가는 것이고, "뜻 없음"이 압도적이면 겨냥 규칙이 목표를 너무 아래로
            //  잡는 것이다. 둘을 서로 다른 값으로 둬서 한쪽을 두 번 찍는 구현을 잡는다.
            string report = Build(Unproven(new BotDiagnostics(
                endX: 81.6f, endY: -9.8f, touched: true, ticks: 818, blindTicks: 0,
                farthestX: 81.6f, tickLimit: 3482,
                hitColliderPath: "ComposedMap/Cube", hitVerticalSpeed: -26f,
                vetoedTicks: 231, unwillingTicks: 138)));

            Assert.IsTrue(Contains(report, "누르려다 막힘 231틱"));
            Assert.IsTrue(Contains(report, "누를 뜻 없음 138틱"));
        }

        [Test]
        public void 높이_훑기의_각_줄에도_두_숫자가_붙는다()
        {
            //  스폰은 넷뿐이지만 훑기 줄은 수십 개다 — "못 눌렀나 안 눌렀나"의 진짜 표본은
            //  이쪽이라, 여기 안 붙으면 질문에 답할 데이터가 사실상 없다. 골인한 줄에도
            //  붙어야 한다: 통과한 비행과 실패한 비행의 두 숫자를 견줘야 뜻이 생긴다.
            var sweep = new List<HeightSweepRow>
            {
                new HeightSweepRow(-8f, reached: false, spawnBlocked: false,
                                   bot: new BotDiagnostics(endX: 60f, endY: 3f, touched: true,
                                       ticks: 500, blindTicks: 0, farthestX: 60f, tickLimit: 3482,
                                       hitColliderPath: "ComposedMap/Cube", hitVerticalSpeed: -22f,
                                       vetoedTicks: 311, unwillingTicks: 44)),
                new HeightSweepRow(-7f, reached: true, spawnBlocked: false,
                                   bot: new BotDiagnostics(endX: 632f, endY: 3f, touched: false,
                                       ticks: 2900, blindTicks: 0, farthestX: 632f, tickLimit: 3482,
                                       vetoedTicks: 77, unwillingTicks: 1802)),
            };
            string report = BuildWithSweep(sweep, Unproven(Hit("기둥/Cube_12", -30f)));

            Assert.IsTrue(Contains(report, "(막힘 311틱/뜻없음 44틱)"));
            Assert.IsTrue(Contains(report, "(막힘 77틱/뜻없음 1802틱)"));
        }

        [Test]
        public void 못_날린_훑기_줄에는_두_숫자를_안_붙인다()
        {
            //  지형 안이라 날려 보지도 못한 줄에 0/0을 찍으면 "날려 봤는데 둘 다 0이었다"로
            //  읽힌다 — 재지 못한 것을 쟀다고 말하지 않는다는 이 리포트의 규칙 그대로다.
            var sweep = new List<HeightSweepRow>
            {
                new HeightSweepRow(-6f, reached: false, spawnBlocked: true, bot: default),
            };
            string report = BuildWithSweep(sweep, Unproven(Hit("기둥/Cube_12", -30f)));

            Assert.IsTrue(Contains(report, "그 높이가 지형 안"));
            //  괄호와 뒤따르는 공백까지 포함해 훑기 줄의 꼴로만 찾는다. ①의 봇 줄도 두 숫자를
            //  찍고("누르려다 막힘 0틱"), ①의 계수기 경고도 "(막힘:뜻없음 …"으로 시작하므로,
            //  그보다 느슨하게 찾으면 그쪽에 걸려 늘 빨강이 된다.
            Assert.IsFalse(Contains(report, "(막힘 "));
        }

        [Test]
        public void 재생_어긋남_줄은_탐색이_그_틱에_본_높이도_찍는다()
        {
            //  "탐색은 거기를 통과 가능하다고 믿었다"의 그 믿음. 재생의 y(−2.7)와 탐색의
            //  y(+1.4)를 다르게 둬서, 차이(+4.1m)를 실제로 계산하는지까지 본다 — 한쪽 값을
            //  베껴 찍으면 차이가 0.0으로 나와 잡힌다.
            string report = Build(Unproven(
                Hit("지붕슬래브/Cube_77", 12.4f),
                new ReplayMismatch(detected: true, tick: 222, x: 46.7f, y: -2.7f,
                                   verticalSpeed: 18.8f, colliderPath: "ComposedMap/Cube",
                                   searchY: 1.4f, hasSearchY: true)));

            Assert.IsTrue(Contains(report, "탐색은 그 틱에 y=+1.4로 봤다"));
            //  "충돌 후"라는 말이 붙어 있어야 한다 — 이 값은 이동 커널이 벽에 잘라낸 뒤의
            //  차이라 실제 편향이 아니라 상한이다. 라벨 없이 "차이"라고만 쓰면 읽는 사람이
            //  이것을 편향으로 읽는다.
            Assert.IsTrue(Contains(report, "(충돌 후 차이 +4.1m)"));
        }

        [Test]
        public void 탐색이_본_높이를_모르면_그_줄을_안_찍는다()
        {
            //  hasSearchY=false를 0.0으로 찍으면 "탐색은 0m로 봤다"는 측정값으로 읽힌다.
            string report = Build(Unproven(
                Hit("지붕슬래브/Cube_77", 12.4f),
                new ReplayMismatch(detected: true, tick: 222, x: 46.7f, y: -2.7f,
                                   verticalSpeed: 18.8f, colliderPath: "ComposedMap/Cube")));

            Assert.IsTrue(Contains(report, "재생 어긋남: 222틱째"));
            Assert.IsFalse(Contains(report, "탐색은 그 틱에"));
        }

        //  ── Task 17 — 되돌리기 (양쪽으로 열린 측정) ──────────────────
        //  막힘:뜻없음 두 계수기는 정확하지만 두 가설에서 같은 답을 낸다 — 살아 있는 봇이면
        //  늘 "뜻 없음"이 압도한다. 되돌리기는 다르다: 겨냥이 놓친 것이면 살릴 자리가 나오고,
        //  지형이 막은 것이면 하나도 안 나온다. 아래 테스트들은 그 답이 실제로 리포트에
        //  박히는지, 그리고 재지 않은 것을 쟀다고 말하지 않는지 확인한다.

        static Counterfactual Savable(int tried, int savable, int byFlap,
                                      int earliestK, float gain, bool forcedFlap)
            => new Counterfactual(measured: true, tried: tried, savable: savable,
                                  savableByFlap: byFlap, earliestTicksBeforeDeath: earliestK,
                                  earliestGain: gain, earliestForcedFlap: forcedFlap);

        static BotDiagnostics WithCounterfactual(Counterfactual cf)
            => new BotDiagnostics(endX: 177.9f, endY: 0.8f, touched: true, ticks: 818, blindTicks: 0,
                                  farthestX: 177.9f, tickLimit: 3482,
                                  hitColliderPath: "ComposedMap/Cube", hitVerticalSpeed: -26f,
                                  vetoedTicks: 231, unwillingTicks: 138, counterfactual: cf);

        [Test]
        public void 되돌리기는_살릴_수_있던_자리_수와_가장_이른_자리를_찍는다()
        {
            //  네 숫자를 전부 다르게 둔다(60/17/15/41) — 하나를 다른 자리에 베껴 찍어도
            //  반드시 어느 하나가 어긋나게.
            string report = Build(Unproven(WithCounterfactual(
                Savable(tried: 60, savable: 17, byFlap: 15, earliestK: 41, gain: 38.2f, forcedFlap: true))));

            Assert.IsTrue(Contains(report, "죽기 전 60틱 중 17곳에서 다르게 눌렀으면 더 갔다"));
            Assert.IsTrue(Contains(report, "누름 강제 15곳"));
            Assert.IsTrue(Contains(report, "죽기 41틱 전"));
            Assert.IsTrue(Contains(report, "눌렀으면 +38.2m"));
        }

        [Test]
        public void 되돌리기의_뒤집기_방향이_문장에_드러난다()
        {
            //  "그 자리에서 눌렀으면"과 "안 눌렀으면"은 처방이 정반대다(겨냥이 소심한가
            //  성급한가). 같은 값에 방향만 뒤집어, 문장이 실제로 그 플래그를 따라가는지 본다.
            string flap = Build(Unproven(WithCounterfactual(
                Savable(60, 17, 15, 41, 38.2f, forcedFlap: true))));
            string hold = Build(Unproven(WithCounterfactual(
                Savable(60, 17, 15, 41, 38.2f, forcedFlap: false))));

            Assert.IsTrue(Contains(flap, "눌렀으면 +38.2m"));
            Assert.IsFalse(Contains(flap, "안 눌렀으면 +38.2m"));
            Assert.IsTrue(Contains(hold, "안 눌렀으면 +38.2m"));
        }

        [Test]
        public void 되돌려도_못_갔으면_그것도_결론으로_적는다()
        {
            //  0곳은 침묵이 아니라 결론이다 — "지형이 막은 것"이라는 답이다. 이 줄이 없으면
            //  두 가설 중 하나를 고를 수 없다.
            string report = Build(Unproven(WithCounterfactual(
                Savable(tried: 60, savable: 0, byFlap: 0, earliestK: 0, gain: 0f, forcedFlap: false))));

            Assert.IsTrue(Contains(report, "죽기 전 60틱 어디서 다르게 눌러도 더 못 갔다"));
            //  "이 창 안에서는"이 빠지면 이 줄이 "지형이 막았다"는 단정으로 읽힌다 —
            //  실측으로 같은 자리가 창 30틱에서 0곳, 60틱에서 6곳이었다.
            Assert.IsTrue(Contains(report, "이 창 안에서는"));
            //  0곳일 때 "가장 이른 곳"을 찍으면 없는 자리를 있다고 말하는 셈이다.
            Assert.IsFalse(Contains(report, "가장 이른 곳"));
        }

        [Test]
        public void 되돌리기를_안_쟀으면_줄이_아예_없다()
        {
            //  default(Counterfactual)은 Measured=false — 안 돌렸다는 뜻이다. 0곳으로 찍으면
            //  "되돌려 봤는데 하나도 못 살렸다"는 정반대 결론으로 읽힌다.
            string report = Build(Unproven(Hit("지붕슬래브/Cube_77", 12.4f)));

            //  줄머리 꼴로만 찾는다 — 바로 아래 계수기 경고 문단이 "되돌리기"라는 낱말을
            //  공짜로 주므로, 낱말만 찾으면 이 테스트가 늘 빨강이 된다.
            Assert.IsFalse(Contains(report, "되돌리기: 죽기"));
        }

        [Test]
        public void 증명된_자리는_되돌리기도_안_찍는다()
        {
            //  ✅는 부검할 죽음이 없다 — 값이 들어 있어도 읽으면 안 된다.
            string report = Build(new SpawnCleanRun("PlayerSpawn_1", -6f,
                new CleanRunResult(true, new bool[0], 0f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: true, botFlaps: 176,
                bot: WithCounterfactual(Savable(60, 17, 15, 41, 38.2f, true))));

            Assert.IsFalse(Contains(report, "되돌리기: 죽기"));
        }

        [Test]
        public void 훑기_줄에는_되돌리기_개수만_붙는다()
        {
            //  훑기 줄은 수십 개라 문장을 통째로 붙이면 표가 안 읽힌다 — 결론을 내는 한
            //  숫자만 붙인다. 못 날린 줄에는 아무것도 안 붙어야 한다.
            var sweep = new List<HeightSweepRow>
            {
                new HeightSweepRow(-8f, reached: false, spawnBlocked: false,
                                   bot: WithCounterfactual(Savable(60, 23, 20, 41, 38.2f, true))),
                //  날긴 했지만 되돌리기를 안 잰 줄(Measured=false). Measured 가드를 무시하는
                //  구현이면 여기에 "되돌리기 0곳"이 붙어 잡힌다 — 지형 안이라 못 난 줄로는
                //  그 회귀를 못 잡는다(그 줄은 애초에 두 숫자 자체를 안 붙인다).
                new HeightSweepRow(-7f, reached: false, spawnBlocked: false,
                                   bot: Hit("기둥/Cube_12", -30f)),
            };
            string report = BuildWithSweep(sweep, Unproven(Hit("기둥/Cube_12", -30f)));

            Assert.IsTrue(Contains(report, "되돌리기 23곳"));
            //  문장 꼴은 훑기 줄에 안 붙는다.
            Assert.IsFalse(Contains(report, "죽기 전 60틱 중"));
            //  안 잰 줄에는 0곳도 안 붙는다.
            Assert.IsFalse(Contains(report, "되돌리기 0곳"));
        }

        [Test]
        public void 두_계수기가_혼자서는_결론을_못_낸다고_적는다()
        {
            //  이 경고가 없으면 리포트가 그 비로 처방을 고르게 안내한다 — 이 프로젝트가 이미
            //  두 번 밟은 함정이다. 기준선 숫자(1:∞ / 1:2.4)까지 함께 있어야 한다.
            string report = Build(Unproven(Hit("지붕슬래브/Cube_77", 12.4f)));

            Assert.IsTrue(Contains(report, "막힘:뜻없음 비는 혼자서는 결론을 못 낸다"));
            Assert.IsTrue(Contains(report, "1:∞"));
            Assert.IsTrue(Contains(report, "1:2.4"));
        }

        [Test]
        public void 봇_부검이_없으면_그_경고도_없다()
        {
            //  계수기가 한 줄도 안 찍힌 리포트에 "그 비를 조심하라"만 뜨면 없는 숫자를
            //  조심하라는 말이 된다.
            string report = Build(new SpawnCleanRun("PlayerSpawn_1", -6f,
                new CleanRunResult(true, new bool[0], 0f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: true, botFlaps: 176, bot: default));

            Assert.IsFalse(Contains(report, "막힘:뜻없음 비는 혼자서는 결론을 못 낸다"));
        }

        [Test]
        public void 재생_어긋남은_직전_틱_차이도_함께_찍는다()
        {
            //  충돌 후 차이(+4.1)와 직전 틱 차이(+0.45)를 다르게 둔다 — 한쪽을 베껴 찍으면
            //  잡힌다. 직전 틱 값이 없으면 이 리포트는 상한을 편향으로 읽게 만든다.
            string report = Build(Unproven(
                Hit("지붕슬래브/Cube_77", 12.4f),
                new ReplayMismatch(detected: true, tick: 222, x: 46.7f, y: -2.7f,
                                   verticalSpeed: 18.8f, colliderPath: "ComposedMap/Cube",
                                   searchY: 1.4f, hasSearchY: true,
                                   prevDiff: 0.45f, hasPrevDiff: true)));

            Assert.IsTrue(Contains(report, "충돌 후 차이 +4.1m / 직전 틱 차이 +0.45m"));
        }

        [Test]
        public void 직전_틱_차이를_비율로도_읽어_준다()
        {
            //  "+0.45m"만 있으면 작아 보인다 — 올바른 독해는 반대다. 틱당 편향과 코스 진행률,
            //  전 구간 외삽까지 있어야 "작다"로 읽히지 않는다.
            string report = Build(Unproven(
                Hit("지붕슬래브/Cube_77", 12.4f),
                new ReplayMismatch(detected: true, tick: 222, x: 46.7f, y: -2.7f,
                                   verticalSpeed: 18.8f, colliderPath: "ComposedMap/Cube",
                                   searchY: 1.4f, hasSearchY: true,
                                   prevDiff: 0.45f, hasPrevDiff: true)));

            //  0.45 ÷ 222 = 0.002m/틱.
            Assert.IsTrue(Contains(report, "÷ 222틱 = +0.002m/틱"));
            //  (46.7−(−2)) ÷ 634 = 7.7%.
            Assert.IsTrue(Contains(report, "코스의 7.7%"));
            //  222 × (634 ÷ 48.7) ≈ 2890틱, × 0.002 ≈ 5.9m — ROADMAP의 7m와 같은 자릿수.
            Assert.IsTrue(Contains(report, "약 2890틱"));
            Assert.IsTrue(Contains(report, "약 5.9m"));
            //  상한이라는 사실이 글로 남아야 한다.
            Assert.IsTrue(Contains(report, "충돌 후 차이는 상한이다"));
        }

        [Test]
        public void 직전_틱_차이를_모르면_그_줄을_안_찍는다()
        {
            //  1틱째에 부딪혔으면 직전 틱이 출발점이라 차이가 늘 0이다 — 0을 찍으면
            //  "편향이 없다"는 측정값으로 읽힌다.
            string report = Build(Unproven(
                Hit("지붕슬래브/Cube_77", 12.4f),
                new ReplayMismatch(detected: true, tick: 222, x: 46.7f, y: -2.7f,
                                   verticalSpeed: 18.8f, colliderPath: "ComposedMap/Cube",
                                   searchY: 1.4f, hasSearchY: true)));

            Assert.IsTrue(Contains(report, "탐색은 그 틱에 y=+1.4로 봤다"));
            Assert.IsFalse(Contains(report, "직전 틱 차이"));
            Assert.IsFalse(Contains(report, "편향 읽기"));
        }

        //  ── ① 위상 훑기 ───────────────────────────────────────────────────
        //  장애물이 돌기 시작하면서 "통과 가능한가"의 답이 <b>언제 도착하느냐</b>에 달리게 됐다.
        //  이 절이 없으면 리포트는 그중 한 장면(틱 0)만 보고 맵 전체를 판정한 것처럼 읽힌다.

        static string BuildWithPhases(IReadOnlyList<PhaseSweepRow> phases, params SpawnCleanRun[] runs)
            => PlayabilityReport.Build("FlappyRaceMap", -2f, 632f, Config(), runs,
                                       trapSection: "  낀 자리 없음.",
                                       budget: new List<StunBudgetPoint>
                                       {
                                           new StunBudgetPoint(10f, 108f, 10, 7),
                                       },
                                       earliest: new EarliestCatch(true, 19.0f, 14),
                                       heightGrid: 0.1f, minY: -40f, maxY: 40f,
                                       heightSweep: null, phaseSweep: phases);

        //  위상 전부가 같은 자리에서 막힌 줄 — 정적 지형이 막았을 때의 모양.
        static PhaseSweepRow AllBlocked(string name, int space, float endX)
        {
            var outcomes = new List<PhaseOutcome>();
            for (int p = 0; p < space; p++)
            {
                outcomes.Add(new PhaseOutcome(p, reached: false, endX: endX));
            }
            return new PhaseSweepRow(name, space, stride: 1, tickSeconds: 0.02f, outcomes: outcomes);
        }

        [Test]
        public void 위상_훑기는_통과한_위상_수와_전체를_찍는다()
        {
            string report = BuildWithPhases(
                new List<PhaseSweepRow> { AllBlocked("PlayerSpawn_2", 82, 82.3f) },
                Unproven(Hit("ComposedMap/Cube", -30f)));

            //  절이 통째로 사라지는 돌연변이는 이 머리글로 잡힌다.
            Assert.IsTrue(Contains(report, "── ① 위상 훑기 (자리별)"));
            Assert.IsTrue(Contains(report, "위상 공간 82틱 전수"));
            Assert.IsTrue(Contains(report, "통과 0/82 위상"));
        }

        [Test]
        public void 한_위상도_통과_못_해도_가장_멀리_간_위상과_거리를_찍는다()
        {
            //  0/82여도 이 값이 벽이 어디인지 말해 준다 — 없으면 "전부 막혔다"만 남는다.
            var outcomes = new List<PhaseOutcome>();
            for (int p = 0; p < 82; p++)
            {
                float endX = p == 37 ? 291.4f : (p < 8 ? 82.3f : 276.0f);
                outcomes.Add(new PhaseOutcome(p, reached: false, endX: endX));
            }
            string report = BuildWithPhases(
                new List<PhaseSweepRow>
                {
                    new PhaseSweepRow("PlayerSpawn_1", 82, 1, 0.02f, outcomes),
                },
                Unproven(Hit("FillWindmill/ArmW", -17.6f)));

            //  거리·퍼센트·위상 셋을 한 덩어리로 짚는다 — 하나만 어긋나도 빨강이 되게.
            Assert.IsTrue(Contains(report, "최원거리 291.4 (46%) @위상 37"));
            //  어느 x에서 몇 위상이 막혔나 — 한두 곳에 몰리면 그 장애물이 범인이다.
            Assert.IsTrue(Contains(report, "x≈276 (73위상)"));
            Assert.IsTrue(Contains(report, "x≈82 (8위상)"));
        }

        [Test]
        public void 모든_위상이_같은_자리에서_멈추면_위상과_무관하다고_적는다()
        {
            //  정적 지형(ComposedMap/Cube)이 막은 자리의 모양이다. "@위상 0"이라 찍으면
            //  다른 위상은 다를지 모른다는 헛된 기대를 남긴다.
            string report = BuildWithPhases(
                new List<PhaseSweepRow> { AllBlocked("PlayerSpawn_2", 82, 82.3f) },
                Unproven(Hit("ComposedMap/Cube", -30f)));

            Assert.IsTrue(Contains(report, "최원거리 82.3 (13%) @모든 위상"));
            Assert.IsTrue(Contains(report, "막힌 곳: x≈82 (82위상)"));
        }

        [Test]
        public void 통과한_위상이_있으면_그_범위와_창의_크기를_찍는다()
        {
            //  창의 크기가 곧 난이도다 — 4틱이면 0.08초 안에 도착해야 한다는 뜻이다.
            var outcomes = new List<PhaseOutcome>();
            for (int p = 0; p < 20; p++)
            {
                bool pass = (p >= 3 && p <= 5) || (p >= 12 && p <= 15);
                outcomes.Add(new PhaseOutcome(p, reached: pass, endX: pass ? 632f : 100f));
            }
            string report = BuildWithPhases(
                new List<PhaseSweepRow> { new PhaseSweepRow("PlayerSpawn_1", 20, 1, 0.02f, outcomes) },
                Unproven(Hit("FillWindmill/ArmW", -17.6f)));

            Assert.IsTrue(Contains(report, "통과 7/20 위상"));
            Assert.IsTrue(Contains(report, "통과 위상: 3~5, 12~15"));
            //  4틱 창(12~15)이 3틱 창(3~5)보다 넓다 — 최댓값을 안 고르면 여기서 어긋난다.
            Assert.IsTrue(Contains(report, "가장 긴 창 4틱(0.08초)"));
        }

        [Test]
        public void 일부_위상만_통과하면_타이밍_관문이라고_말한다()
        {
            //  사람은 판이 언제 시작할지 못 고른다 — 이 문장이 없으면 "통과 7/20"이
            //  "이 자리는 된다"로 읽힌다.
            var outcomes = new List<PhaseOutcome>();
            for (int p = 0; p < 20; p++)
            {
                bool pass = p >= 12 && p <= 15;
                outcomes.Add(new PhaseOutcome(p, reached: pass, endX: pass ? 632f : 100f));
            }
            string report = BuildWithPhases(
                new List<PhaseSweepRow> { new PhaseSweepRow("PlayerSpawn_1", 20, 1, 0.02f, outcomes) },
                Unproven(Hit("FillWindmill/ArmW", -17.6f)));

            Assert.IsTrue(Contains(report, "타이밍 관문이다"));
            Assert.IsTrue(Contains(report, "시작 시점은 플레이어가 못 고르므로"));
        }

        [Test]
        public void 모든_위상이_통과하면_타이밍_관문이라고_하지_않는다()
        {
            var outcomes = new List<PhaseOutcome>();
            for (int p = 0; p < 5; p++)
            {
                outcomes.Add(new PhaseOutcome(p, reached: true, endX: 632f));
            }
            string report = BuildWithPhases(
                new List<PhaseSweepRow> { new PhaseSweepRow("PlayerSpawn_1", 5, 1, 0.02f, outcomes) },
                Unproven(Hit("FillWindmill/ArmW", -17.6f)));

            Assert.IsTrue(Contains(report, "통과 5/5 위상"));
            Assert.IsFalse(Contains(report, "타이밍 관문이다"));
            //  막힌 위상이 없으면 그 줄 자체가 없어야 한다 — 빈 "막힌 곳:"은 측정값으로 읽힌다.
            Assert.IsFalse(Contains(report, "막힌 곳:"));
        }

        [Test]
        public void 위상_고리의_양끝이_다_통과면_한_창이라고_적는다()
        {
            //  위상 공간은 고리다 — 9 다음이 0이다. 안 적으면 좁은 창 두 개로 읽혀
            //  난이도를 실제보다 가혹하게 말하게 된다.
            var outcomes = new List<PhaseOutcome>();
            for (int p = 0; p < 10; p++)
            {
                bool pass = p <= 1 || p >= 8;
                outcomes.Add(new PhaseOutcome(p, reached: pass, endX: pass ? 632f : 100f));
            }
            string report = BuildWithPhases(
                new List<PhaseSweepRow> { new PhaseSweepRow("PlayerSpawn_1", 10, 1, 0.02f, outcomes) },
                Unproven(Hit("FillWindmill/ArmW", -17.6f)));

            Assert.IsTrue(Contains(report, "위상은 고리다 — 9 다음이 0이라"));
            //  0~1과 8~9는 사실 한 창이라 4틱이다. 고리를 안 보면 2틱이라 적게 된다.
            Assert.IsTrue(Contains(report, "가장 긴 창 4틱(0.08초)"));
        }

        [Test]
        public void 성기게_훑었으면_놓칠_수_있다는_것을_적는다()
        {
            //  간격 2틱이면 1틱짜리 통과 창은 절반 확률로 안 보인다. 그 사실이 리포트 안에
            //  없으면 "0/41"이 전수 결과로 읽힌다.
            var outcomes = new List<PhaseOutcome>();
            for (int p = 0; p < 82; p += 2)
            {
                outcomes.Add(new PhaseOutcome(p, reached: false, endX: 276f));
            }
            string report = BuildWithPhases(
                new List<PhaseSweepRow> { new PhaseSweepRow("PlayerSpawn_1", 82, 2, 0.02f, outcomes) },
                Unproven(Hit("FillWindmill/ArmW", -17.6f)));

            Assert.IsTrue(Contains(report, "위상 공간 82틱 중 41개만 2틱 간격으로 훑었다"));
            Assert.IsTrue(Contains(report, "통째로 놓칠 수 있다"));
            //  전수일 때의 문구가 성긴 훑기에 새어 나오면 안 된다.
            Assert.IsFalse(Contains(report, "위상 공간 82틱 전수"));
        }

        [Test]
        public void 위상에서_파묻힌_스폰은_실패로_안_센다()
        {
            //  "그 위상엔 못 날렸다"와 "그 위상에선 못 지나간다"는 다른 말이다.
            var outcomes = new List<PhaseOutcome>();
            for (int p = 0; p < 10; p++)
            {
                bool buried = p < 3;
                outcomes.Add(new PhaseOutcome(p, reached: false, endX: buried ? -2f : 50f,
                                              spawnBlocked: buried));
            }
            string report = BuildWithPhases(
                new List<PhaseSweepRow> { new PhaseSweepRow("PlayerSpawn_1", 10, 1, 0.02f, outcomes) },
                Unproven(Hit("FillWindmill/ArmW", -17.6f)));

            Assert.IsTrue(Contains(report, "스폰이 지형 안이라 못 날렸다: 3위상"));
            Assert.IsTrue(Contains(report, "x≈50 (7위상)"));
            //  막힌 곳 집계에 섞이면 출발점(x=-2)이 장애물로 잡힌다 — 그 줄이 있으면
            //  레벨 디자이너가 있지도 않은 병목을 고치러 간다.
            Assert.IsFalse(Contains(report, "x≈-2"));
        }

        [Test]
        public void 안_훑었으면_위상_훑기_절이_아예_없다()
        {
            //  빈 절은 "훑었는데 아무 위상도 없었다"로 읽힌다.
            string none = Build(Unproven(Hit("ComposedMap/Cube", -30f)));
            string empty = BuildWithPhases(new List<PhaseSweepRow>(),
                                           Unproven(Hit("ComposedMap/Cube", -30f)));

            Assert.IsFalse(Contains(none, "위상 훑기"));
            Assert.IsFalse(Contains(empty, "위상 훑기"));
        }

        [Test]
        public void 위상_훑기는_자리별_판정에_섞이지_않는다()
        {
            //  진단 절이다 — 전 위상이 통과해도 ①의 판정은 여전히 틱 0 한 위상 기준이다.
            //  섞이면 "❌인데 위상 훑기가 다 통과"라는 상태를 리포트가 스스로 지워 버린다.
            var outcomes = new List<PhaseOutcome>();
            for (int p = 0; p < 5; p++)
            {
                outcomes.Add(new PhaseOutcome(p, reached: true, endX: 632f));
            }
            string report = BuildWithPhases(
                new List<PhaseSweepRow> { new PhaseSweepRow("PlayerSpawn_4", 5, 1, 0.02f, outcomes) },
                new SpawnCleanRun("PlayerSpawn_4", 9f,
                                  new CleanRunResult(false, new bool[0], 38.2f, 31f, 34, 0.6f),
                                  verifiedByReplay: false, botReached: false, botFlaps: 0, bot: default));

            Assert.IsTrue(Contains(report, "❌  탐색 x=38.2에서 막힘"));
            Assert.IsFalse(Contains(report, "✅"));
        }
    }
}
