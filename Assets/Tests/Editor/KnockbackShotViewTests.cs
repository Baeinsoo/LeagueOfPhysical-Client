using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>탄 그림은 화면 속 그려진 몸에 닿으면 사라진다 — 내 탄이든 남의 탄이든, 쏜 사람 몸만 빼고.</summary>
    public class KnockbackShotViewTests
    {
        const float Radius = 0.88f;
        static int NoTeams(string id) => 0;

        static readonly List<(string id, Vector2 position)> Bodies = new List<(string, Vector2)>
        {
            ("me", new Vector2(0f, 0f)),
            ("a", new Vector2(10f, 0f)),
            ("b", new Vector2(20f, 0f)),
        };

        [Test]
        public void 되감기로_탄_종류가_바뀌면_다시_입힌다()
        {
            Assert.IsTrue(KnockbackShotView.NeedsRestyle(false, true), "보통 탄으로 그렸는데 폭탄이 됨");
            Assert.IsTrue(KnockbackShotView.NeedsRestyle(true, false), "폭탄으로 그렸는데 보통 탄이 됨");
            Assert.IsFalse(KnockbackShotView.NeedsRestyle(true, true));
            Assert.IsFalse(KnockbackShotView.NeedsRestyle(false, false));
        }

        [Test]
        public void 내_탄은_남의_몸에_닿으면_숨는다()
        {
            Assert.IsTrue(KnockbackShotView.TouchesDrawnBody(new Vector2(10.5f, 0f), "me", Bodies, Radius, NoTeams));
            Assert.IsFalse(KnockbackShotView.TouchesDrawnBody(new Vector2(0.2f, 0f), "me", Bodies, Radius, NoTeams), "내 몸에는 안 닿는다");
        }

        [Test]
        public void 남의_탄도_제3자_몸에_닿으면_숨는다()
        {
            Assert.IsTrue(KnockbackShotView.TouchesDrawnBody(new Vector2(20.5f, 0f), "a", Bodies, Radius, NoTeams));
        }

        [Test]
        public void 남의_탄이_내_몸에_닿아도_숨는다()
        {
            Assert.IsTrue(KnockbackShotView.TouchesDrawnBody(new Vector2(0.3f, 0f), "a", Bodies, Radius, NoTeams));
        }

        [Test]
        public void 남의_탄은_쏜_사람_몸에는_안_닿는다()
        {
            Assert.IsFalse(KnockbackShotView.TouchesDrawnBody(new Vector2(10.2f, 0f), "a", Bodies, Radius, NoTeams));
            Assert.IsFalse(KnockbackShotView.TouchesDrawnBody(new Vector2(15f, 0f), "a", Bodies, Radius, NoTeams), "아무 몸에도 안 닿는 자리");
        }

        [Test]
        public void 서_있는_기둥의_반지름과_탄_반지름_안이면_숨는다()
        {
            var standing = new List<Vector2> { new Vector2(5f, 5f) };
            Assert.IsTrue(KnockbackShotView.InsideStandingPillar(new Vector2(5f, 5.9f), standing, 1f));
            Assert.IsFalse(KnockbackShotView.InsideStandingPillar(new Vector2(5f, 6.1f), standing, 1f));
            Assert.IsFalse(KnockbackShotView.InsideStandingPillar(new Vector2(5f, 5f), new List<Vector2>(), 1f));
        }

        [Test]
        public void 같은_팀_몸에_닿아도_탄을_숨기지_않는다()
        {
            int Team(string id) => id == "a" ? 1 : id == "me" ? 1 : 2;
            Assert.IsFalse(KnockbackShotView.TouchesDrawnBody(new Vector2(10.5f, 0f), "me", Bodies, Radius, Team), "같은 팀 a");
            Assert.IsTrue(KnockbackShotView.TouchesDrawnBody(new Vector2(20.5f, 0f), "me", Bodies, Radius, Team), "상대 b");
            Assert.IsTrue(KnockbackShotView.TouchesDrawnBody(new Vector2(10.5f, 0f), "me", Bodies, Radius, id => 0), "개인전은 숨는다");
        }

        const float Speed = 17f;
        const float Range = 13f;
        const float Trail = 0.18f;
        static readonly Vector2 Muzzle = new Vector2(2f, 3f);
        static readonly Vector2 Dir = new Vector2(1f, 0f);

        [Test]
        public void 제때_본_탄의_꼬리는_0점18초_뒤()
        {
            var (head, tail) = KnockbackShotView.TrailEnds(Muzzle, Dir, Speed, Range, 0.5f, 0f, Trail);
            Assert.AreEqual(Speed * 0.5f, head.x - Muzzle.x, 1e-3f);
            Assert.AreEqual(Speed * 0.32f, tail.x - Muzzle.x, 1e-3f);
        }

        [Test]
        public void 늦게_본_탄은_총구부터_그린다()
        {
            var (_, tail) = KnockbackShotView.TrailEnds(Muzzle, Dir, Speed, Range, 0.25f, 0.25f, Trail);
            Assert.AreEqual(Muzzle.x, tail.x, 1e-3f);
        }

        [Test]
        public void 늦게_본_꼬리는_줄어든다()
        {
            var (head, tail) = KnockbackShotView.TrailEnds(Muzzle, Dir, Speed, Range, 0.25f + Trail, 0.25f, Trail);
            Assert.AreEqual(Speed * Trail, head.x - tail.x, 1e-3f);
        }

        [Test]
        public void 사거리_끝에서_꼬리가_머리를_따라잡는다()
        {
            float seconds = Range / Speed + Trail + 0.01f;
            var (head, tail) = KnockbackShotView.TrailEnds(Muzzle, Dir, Speed, Range, seconds, 0f, Trail);
            Assert.AreEqual(Muzzle.x + Range, head.x, 1e-3f);
            Assert.AreEqual(head.x, tail.x, 1e-3f);
        }

        [TestCase(0f, 1f)]
        [TestCase(0.06f, 0.5f)]
        [TestCase(0.12f, 0f)]
        [TestCase(1f, 0f)]
        public void 사라지는_연출은_0점12초에_걸쳐_줄어든다(float elapsed, float expected)
        {
            Assert.AreEqual(expected, KnockbackShotView.FadeRemaining(elapsed), 1e-4f);
        }
    }
}
