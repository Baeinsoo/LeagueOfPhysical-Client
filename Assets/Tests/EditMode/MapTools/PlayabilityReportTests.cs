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

            StringAssert.Contains("38.2", report);
            //  세 필드(x/생존/높이 폭)가 서로 바뀌어도 위 "최협 회랑" 한 줄 존재 검사는 못
            //  잡는다 — 세 값이 서로 다르므로(31.0 / 34 / 0.6), 라벨+값을 붙여 확인하면
            //  둘 중 어느 자리가 뒤바뀌어도 반드시 하나는 어긋난다.
            StringAssert.Contains("x=31.0", report);
            StringAssert.Contains("생존 34", report);
            StringAssert.Contains("높이 폭 0.6m", report);
            //  spec §8 — 실패는 눈금 탓일 수도 있어 되짚어 볼 안내를 같이 준다.
            StringAssert.Contains("눈금", report);
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

            //  이모지 하나만 담긴 검색어는 NUnit의 StringAssert.Contains(문화권 비교)가 오탐한다
            //  — 이 환경에서 실측: 어떤 문자열에도 "✅"가 "있다"고 나온다(약한 콜레이션이
            //  기호를 사실상 와일드카드로 만든다). 그래서 순서(ordinal) 비교로 직접 확인한다.
            Assert.IsTrue(Contains(proven, "✅"));
            Assert.IsFalse(Contains(unproven, "✅"));
            //  증명 안 된 성공은 실패(❌)와도 다른 제 글자를 가져야 한다.
            Assert.IsFalse(Contains(unproven, "❌"));
            Assert.IsTrue(Contains(unproven, "🟡"));
        }

        static bool Contains(string haystack, string needle)
            => haystack.Contains(needle, System.StringComparison.Ordinal);

        [Test]
        public void 통과여부는_같아도_증명_여부가_갈리면_공정성_경고를_찍는다()
        {
            //  둘 다 클린런은 "된다"이지만 하나는 증명됐고 하나는 안 됐다 — 자리마다 안전을
            //  확신할 수 있는 정도가 다르다는 뜻이라, 이것도 공정성 문제로 알려야 한다.
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), true, botReached: false, botFlaps: 0, bot: default),
                new SpawnCleanRun("PlayerSpawn_2", -1f, new CleanRunResult(true, new bool[209], 0f, 0f, 0, 0f), false, botReached: false, botFlaps: 0, bot: default));

            StringAssert.Contains("증명", report);
        }

        [Test]
        public void 예산에_상한_가정을_함께_적는다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), true, botReached: false, botFlaps: 0, bot: default));

            StringAssert.Contains("19.0", report);
            StringAssert.Contains("14", report);
            //  이 숫자가 상한이라는 사실을 안 적으면 읽는 사람이 안전선으로 오해한다.
            StringAssert.Contains("실제는 이보다 나쁘다", report);
        }

        [Test]
        public void 봇이_통과한_자리는_증명된_것으로_찍는다()
        {
            string report = Build(new SpawnCleanRun("PlayerSpawn_1", -6f,
                new CleanRunResult(true, new bool[0], 0f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: true, botFlaps: 176, bot: default));

            Assert.IsTrue(report.Contains("✅", System.StringComparison.Ordinal));
            Assert.IsFalse(report.Contains("🟡", System.StringComparison.Ordinal));
            //  봇이 통과했으면 그 자리엔 탐색을 돌리지 않았다는 사실이 읽혀야 한다.
            StringAssert.Contains("봇 통과", report);
            StringAssert.Contains("176", report);
        }

        [Test]
        public void 봇도_탐색도_실패하면_불가능으로_찍는다()
        {
            string report = Build(new SpawnCleanRun("PlayerSpawn_4", 9f,
                new CleanRunResult(false, new bool[0], 38.2f, 31f, 34, 0.6f),
                verifiedByReplay: false, botReached: false, botFlaps: 51, bot: default));

            Assert.IsTrue(report.Contains("❌", System.StringComparison.Ordinal));
            Assert.IsFalse(report.Contains("✅", System.StringComparison.Ordinal));
        }

        [Test]
        public void 봇은_실패했는데_탐색이_찾으면_모름으로_찍는다()
        {
            string report = Build(new SpawnCleanRun("PlayerSpawn_2", -1f,
                new CleanRunResult(true, new bool[191], 0f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: false, botFlaps: 51, bot: default));

            Assert.IsTrue(report.Contains("🟡", System.StringComparison.Ordinal));
            Assert.IsFalse(report.Contains("✅", System.StringComparison.Ordinal));
            //  이 상태의 뜻이 "봇 한계일 수 있다"라는 것이 글로 남아야 한다.
            StringAssert.Contains("봇이 못 간 것", report);
        }

        //  ── Fix round 2 — 진단 ──────────────────────────────────────
        //  ① 봇이 실패했을 때 "어디서 왜 멈췄는지"가 리포트에 안 남으면, 네 자리가 전부
        //  🟡/❌로 나와도 "장애물 하나가 문제인지 봇이 약한 것인지" 구분할 방법이 없다.
        //  아래 테스트들은 그 진단 수치(끝난 자리·이유·틱·목표 못 찾은 틱)가 실제로
        //  리포트 문자열에 박히는지 확인한다.

        [Test]
        public void 실패한_자리는_봇이_멈춘_위치와_이유를_찍는다()
        {
            //  코스는 Build()에서 x −2 → 632 (634m). endX=143.82면 (143.82−(−2))/634*100
            //  ≈ 23.0%다 — 코디네이터가 요청한 예시(23%에서 충돌)와 같은 수를 쓴다.
            string report = Build(new SpawnCleanRun("PlayerSpawn_2", -1f,
                new CleanRunResult(true, new bool[191], 0f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: false, botFlaps: 51,
                bot: new BotDiagnostics(endX: 143.82f, endY: -57.6f, touched: true, ticks: 812, blindTicks: 40)));

            StringAssert.Contains("x=143.8", report);
            StringAssert.Contains("y=-57.6", report);
            StringAssert.Contains("코스 23%", report);
            StringAssert.Contains("닿음", report);
            StringAssert.Contains("812틱", report);
            StringAssert.Contains("목표 없음 40틱", report);
        }

        [Test]
        public void 안_닿고_틱만_소진하면_그렇게_적는다()
        {
            //  Touched=false — 아무것도 안 닿았는데 제한 틱을 다 썼다는 뜻이라 "닿음"과는
            //  다른 처방(제자리를 맴돌았다 등)이 필요하다. 원인 글자가 갈려야 한다.
            string report = Build(new SpawnCleanRun("PlayerSpawn_3", 2f,
                new CleanRunResult(false, new bool[0], 5f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: false, botFlaps: 3,
                bot: new BotDiagnostics(endX: -1f, endY: 0f, touched: false, ticks: 2000, blindTicks: 1990)));

            StringAssert.Contains("틱 소진(못 닿음)", report);
        }

        [Test]
        public void 실패한_자리의_진단은_불가능_줄에도_찍는다()
        {
            //  🟡뿐 아니라 ❌(탐색도 못 찾음)도 봇이 실제로 날았던 자리다 — 진단이 빠지면
            //  안 된다.
            string report = Build(new SpawnCleanRun("PlayerSpawn_4", 9f,
                new CleanRunResult(false, new bool[0], 38.2f, 31f, 34, 0.6f),
                verifiedByReplay: false, botReached: false, botFlaps: 51,
                bot: new BotDiagnostics(endX: 38.0f, endY: 12.5f, touched: true, ticks: 950, blindTicks: 12)));

            StringAssert.Contains("x=38.0", report);
            StringAssert.Contains("y=12.5", report);
            StringAssert.Contains("목표 없음 12틱", report);
        }

        [Test]
        public void 증명된_자리는_봇_진단을_안_찍는다()
        {
            //  ✅는 "부검할 실패"가 없다 — Bot에 값이 들어 있어도(방어적 쓰레기값) 리포트가
            //  그걸 읽으면 안 된다.
            string report = Build(new SpawnCleanRun("PlayerSpawn_1", -6f,
                new CleanRunResult(true, new bool[0], 0f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: true, botFlaps: 176,
                bot: new BotDiagnostics(endX: 999f, endY: 999f, touched: true, ticks: 1, blindTicks: 1)));

            StringAssert.DoesNotContain("목표 없음", report);
            StringAssert.DoesNotContain("x=999.0", report);
        }
    }
}
