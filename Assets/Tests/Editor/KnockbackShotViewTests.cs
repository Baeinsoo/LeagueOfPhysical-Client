using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>탄 그림은 화면 속 그려진 몸에 닿으면 사라진다 — 내 탄이든 남의 탄이든, 쏜 사람 몸만 빼고.</summary>
    public class KnockbackShotViewTests
    {
        const float Radius = 0.88f;

        static readonly List<(string id, Vector2 position)> Bodies = new List<(string, Vector2)>
        {
            ("me", new Vector2(0f, 0f)),
            ("a", new Vector2(10f, 0f)),
            ("b", new Vector2(20f, 0f)),
        };

        [Test]
        public void 내_탄은_남의_몸에_닿으면_숨는다()
        {
            Assert.IsTrue(KnockbackShotView.TouchesDrawnBody(new Vector2(10.5f, 0f), "me", Bodies, Radius));
            Assert.IsFalse(KnockbackShotView.TouchesDrawnBody(new Vector2(0.2f, 0f), "me", Bodies, Radius), "내 몸에는 안 닿는다");
        }

        [Test]
        public void 남의_탄도_제3자_몸에_닿으면_숨는다()
        {
            Assert.IsTrue(KnockbackShotView.TouchesDrawnBody(new Vector2(20.5f, 0f), "a", Bodies, Radius));
        }

        [Test]
        public void 남의_탄이_내_몸에_닿아도_숨는다()
        {
            Assert.IsTrue(KnockbackShotView.TouchesDrawnBody(new Vector2(0.3f, 0f), "a", Bodies, Radius));
        }

        [Test]
        public void 남의_탄은_쏜_사람_몸에는_안_닿는다()
        {
            Assert.IsFalse(KnockbackShotView.TouchesDrawnBody(new Vector2(10.2f, 0f), "a", Bodies, Radius));
            Assert.IsFalse(KnockbackShotView.TouchesDrawnBody(new Vector2(15f, 0f), "a", Bodies, Radius), "아무 몸에도 안 닿는 자리");
        }
    }
}
