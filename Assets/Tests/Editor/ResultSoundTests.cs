using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>결과 씬 NEW BEST 소리(spec §6·§7) — 경기 소리(FlappyAudio)는 경기 스코프라 결과 씬엔 없다. 같은 조리법으로 클립 하나만 짓는다.</summary>
    public class ResultSoundTests
    {
        [Test]
        public void 경기_NEW_BEST와_같은_소리를_낸다()
        {
            var sound = new ResultSound();
            var played = new List<AudioClip>();
            sound.playOneShot = played.Add;

            sound.PlayNewBest();

            Assert.AreEqual(1, played.Count);
            Assert.AreEqual(FlappySynth.Render(FlappySfx.NewBest).Length, played[0].samples);
            Assert.AreEqual(FlappySynth.Rate, played[0].frequency);
            sound.Dispose();
        }

        [Test]
        public void 치운_뒤엔_소리를_안_낸다()
        {
            var sound = new ResultSound();
            int count = 0;
            sound.playOneShot = _ => count++;

            sound.Dispose();
            sound.PlayNewBest();

            Assert.AreEqual(0, count);
        }
    }
}
