using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class TightestClearanceTests
    {
        static ClearanceSample Sample(int tick, float above, float below)
            => new ClearanceSample(tick, x: tick * 0.22f, feetY: 0f, above: above, below: below);

        [Test]
        public void Gap_is_the_narrower_side_not_the_sum()
        {
            //  위가 활짝 열려 있어도 아래가 2cm면 그 틱은 2cm짜리 통과다.
            Assert.That(Sample(0, above: 5f, below: 0.02f).Gap, Is.EqualTo(0.02f).Within(1e-6f));
            Assert.That(Sample(0, above: 0.03f, below: 4f).Gap, Is.EqualTo(0.03f).Within(1e-6f));
        }

        [Test]
        public void Picks_the_smallest_gap()
        {
            var samples = new List<ClearanceSample>
            {
                Sample(0, above: 3f, below: 3f),
                //  이 틱이 답이다 — 위가 넉넉해도 아래가 제일 좁다.
                Sample(1, above: 9f, below: 0.4f),
                Sample(2, above: 1.2f, below: 2f),
            };

            Assert.That(TightestClearance.TryFind(samples, out var tightest), Is.True);
            Assert.That(tightest.Tick, Is.EqualTo(1));
            Assert.That(tightest.Gap, Is.EqualTo(0.4f).Within(1e-6f));
        }

        [Test]
        public void Ties_keep_the_earliest_tick()
        {
            var samples = new List<ClearanceSample>
            {
                Sample(0, above: 2f, below: 2f),
                Sample(1, above: 0.5f, below: 3f),
                Sample(2, above: 0.5f, below: 3f),
            };

            Assert.That(TightestClearance.TryFind(samples, out var tightest), Is.True);
            Assert.That(tightest.Tick, Is.EqualTo(1));
        }

        [Test]
        public void Empty_finds_nothing_rather_than_reporting_zero_clearance()
        {
            Assert.That(TightestClearance.TryFind(new List<ClearanceSample>(), out var tightest), Is.False);
            Assert.That(tightest.Gap, Is.EqualTo(0f));

            Assert.That(TightestClearance.TryFind(null, out _), Is.False);
        }
    }
}
