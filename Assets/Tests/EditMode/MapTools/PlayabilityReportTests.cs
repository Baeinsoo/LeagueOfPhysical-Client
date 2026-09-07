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
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), true),
                new SpawnCleanRun("PlayerSpawn_4", 9f, new CleanRunResult(false, new bool[0], 38.2f, 31f, 34, 0.6f), false));

            StringAssert.Contains("PlayerSpawn_1", report);
            StringAssert.Contains("PlayerSpawn_4", report);
        }

        [Test]
        public void 자리마다_답이_다르면_공정성_경고를_찍는다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), true),
                new SpawnCleanRun("PlayerSpawn_4", 9f, new CleanRunResult(false, new bool[0], 38.2f, 31f, 34, 0.6f), false));

            //  "넷 중 하나라도 되면 통과"로 읽히면 안 된다 — 자리 배정이 곧 불이익이다.
            StringAssert.Contains("자리 배정", report);
        }

        [Test]
        public void 모두_같으면_공정성_경고가_없다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), true),
                new SpawnCleanRun("PlayerSpawn_2", -1f, new CleanRunResult(true, new bool[209], 0f, 0f, 0, 0f), true));

            StringAssert.DoesNotContain("자리 배정", report);
        }

        [Test]
        public void 막힌_자리는_어디서_끊겼는지와_최협_회랑을_같이_찍는다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_4", 9f, new CleanRunResult(false, new bool[0], 38.2f, 31f, 34, 0.6f), false));

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
                new SpawnCleanRun("PlayerSpawn_4", 9f, new CleanRunResult(false, new bool[0], 3.1f, 0f, 0, 0f), false));

            StringAssert.Contains("측정 안", report);
            StringAssert.DoesNotContain("최협 회랑 x=0", report);
        }

        [Test]
        public void 재생으로_증명되지_않은_성공은_그렇다고_적는다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), false));

            StringAssert.Contains("재생이 어긋", report);
        }

        [Test]
        public void 증명되지_않은_성공은_증명된_성공의_글자를_쓰지_않는다()
        {
            //  ✅는 spec §3.7이 "재생으로 증명됨"으로 정의한 글자다. 재생이 어긋난 성공까지
            //  ✅를 찍으면 읽는 사람이 증명된 것과 증명 안 된 것을 구분할 수 없다 — 실제 맵의
            //  네 자리가 전부 이 경우였다(탐색은 찾았지만 재생 전부 실패).
            string proven = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), true));
            string unproven = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), false));

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
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), true),
                new SpawnCleanRun("PlayerSpawn_2", -1f, new CleanRunResult(true, new bool[209], 0f, 0f, 0, 0f), false));

            StringAssert.Contains("증명", report);
        }

        [Test]
        public void 예산에_상한_가정을_함께_적는다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 0f, 0, 0f), true));

            StringAssert.Contains("19.0", report);
            StringAssert.Contains("14", report);
            //  이 숫자가 상한이라는 사실을 안 적으면 읽는 사람이 안전선으로 오해한다.
            StringAssert.Contains("실제는 이보다 나쁘다", report);
        }
    }
}
