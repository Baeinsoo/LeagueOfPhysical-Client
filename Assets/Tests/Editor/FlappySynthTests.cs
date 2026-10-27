using System;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 코드로 짓는 임시 소리(spec §6). 시제품에서 들어 본 조리법이 그대로 나오는지 — 소리가 나는지, 찢어지지 않는지,
    /// 길이·음높이가 조리법대로인지, 늘 같은 파형인지 — 를 고정한다.
    /// </summary>
    public class FlappySynthTests
    {
        private static float Peak(float[] data)
        {
            float peak = 0f;
            foreach (float s in data) { peak = Mathf.Max(peak, Mathf.Abs(s)); }
            return peak;
        }

        [Test]
        public void 모든_효과음이_들리는_소리를_낸다()
        {
            foreach (FlappySfx sfx in Enum.GetValues(typeof(FlappySfx)))
            {
                float[] data = FlappySynth.Render(sfx);
                Assert.Greater(data.Length, 0, sfx.ToString());
                Assert.Greater(Peak(data), 0.01f, sfx + " — 조리법이 빠지면 무음 클립이 된다");
            }
        }

        [Test]
        public void 어느_소리도_1을_넘지_않는다()
        {
            //  겹친 소리(쿵 = 낮은 음 + 잡음)가 1을 넘으면 폰 스피커에서 찢어진다 — master 0.5를 미리 곱한다.
            foreach (FlappySfx sfx in Enum.GetValues(typeof(FlappySfx)))
            {
                Assert.LessOrEqual(Peak(FlappySynth.Render(sfx)), 1f, sfx.ToString());
            }
            Assert.LessOrEqual(Peak(FlappySynth.MusicLoop()), 1f, "배경음");
        }

        [Test]
        public void 길이는_제일_늦게_끝나는_음까지_더하기_꼬리()
        {
            Assert.AreEqual(Mathf.CeilToInt((0.35f + FlappySynth.TailSeconds) * FlappySynth.Rate), FlappySynth.Render(FlappySfx.Crash).Length);
            //  결승: 마지막 긴 음이 0.4초에 시작해 0.6초 간다.
            Assert.AreEqual(Mathf.CeilToInt((0.4f + 0.6f + FlappySynth.TailSeconds) * FlappySynth.Rate), FlappySynth.Render(FlappySfx.Finish).Length);
        }

        [Test]
        public void 박자는_660Hz로_울린다()
        {
            //  네모파는 한 주기에 0을 두 번 지난다 — 660 Hz × 0.12초 × 2 ≈ 158번.
            float[] data = FlappySynth.Render(FlappySfx.CountBeat);
            int crossings = 0;
            for (int i = 1; i < data.Length; i++)
            {
                if (data[i - 1] * data[i] < 0f) { crossings++; }
            }
            Assert.That(crossings, Is.InRange(150, 166));
        }

        [Test]
        public void 같은_소리는_늘_같은_파형()
        {
            //  잡음 씨앗이 소리마다 정해져 있다 — 시험·파일 비교가 흔들리지 않는다.
            CollectionAssert.AreEqual(FlappySynth.Render(FlappySfx.Crash), FlappySynth.Render(FlappySfx.Crash));
        }

        [Test]
        public void 배경음은_124BPM_8분음표_32박_한_바퀴()
        {
            int expected = Mathf.RoundToInt(32 * (60f / 124f / 2f) * FlappySynth.Rate);
            Assert.AreEqual(expected, FlappySynth.MusicLoop().Length);
            //  소리 모양: 0.01초에 꼭대기, 끝에서 0.0001.
            Assert.AreEqual(0.12f, FlappySynth.Envelope(0.01f, 0.12f, 0.12f), 1e-4f);
            Assert.AreEqual(0.0001f, FlappySynth.Envelope(0.12f, 0.12f, 0.12f), 1e-5f);
        }

        [Test]
        public void 고비_화음은_추월_소리보다_조용하고_짧다()
        {
            //  10-26 사용자: 집중을 깨지 않게 — 연속 날갯짓 고비(10·25·50·100)엔 조용한 짧은 화음 한 번.
            float[] milestone = FlappySynth.Render(FlappySfx.Milestone);
            Assert.Less(Peak(milestone), Peak(FlappySynth.Render(FlappySfx.RankUp)));
            Assert.Less(milestone.Length, Mathf.CeilToInt(0.6f * FlappySynth.Rate));
        }
    }
}
