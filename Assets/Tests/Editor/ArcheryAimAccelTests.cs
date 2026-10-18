using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    using Accel = LOP.UI.ArcheryAimAccel;
    using Strength = LOP.UI.ArcheryAimAccel.Strength;

    //  조준 가속: 손가락을 **빨리** 휙 그으면 더 많이 돈다. 투석기 맵은 과녁이 좌우 ±30°·위로 25°까지
    //  퍼져 있는데, 당기면 화면이 좁아져 엄지가 갈 거리가 모자랐다.
    //
    //  <para>지킬 것은 하나 — **천천히 움직일 때는 정확히 예전과 같다.** 정밀 조준이 가속에 흔들리면
    //  10점을 노리는 손맛이 사라진다. 그리고 같은 손짓은 기기의 프레임레이트와 상관없이 같은 만큼 돌아야 한다.</para>
    public class ArcheryAimAccelTests
    {
        const float Max = 3f;

        [Test]
        public void 천천히_움직이면_정확히_1배다()
        {
            Assert.AreEqual(1f, Accel.Gain(0f, Max));
            Assert.AreEqual(1f, Accel.Gain(0.1f, Max));
            Assert.AreEqual(1f, Accel.Gain(Accel.SlowSpeed, Max), "느림의 경계에서도 1배여야 한다");
            Assert.AreEqual(1f, Accel.Gain(Accel.SlowSpeed, 4f));
        }

        [Test]
        public void 빨라질수록_커지기만_하고_줄지_않는다()
        {
            float previous = Accel.Gain(0f, Max);
            for (float v = 0f; v <= 10f; v += 0.01f)
            {
                float g = Accel.Gain(v, Max);
                Assert.GreaterOrEqual(g, previous, $"{v} h/s에서 배율이 줄었다");
                previous = g;
            }
            Assert.Greater(Accel.Gain(1f, Max), 1.2f, "보통 빠르기에서 가속이 거의 안 걸린다");
        }

        [Test]
        public void 상한에서_멈춘다()
        {
            Assert.AreEqual(Max, Accel.Gain(Accel.FastSpeed, Max), 1e-5f);
            Assert.AreEqual(Max, Accel.Gain(50f, Max), 1e-5f);
            for (float v = 0f; v <= 20f; v += 0.05f)
            {
                Assert.LessOrEqual(Accel.Gain(v, Max), Max + 1e-5f);
            }
        }

        //  계단이 있으면 같은 손짓이 아주 조금 빨라졌을 뿐인데 조준이 툭 튄다.
        [Test]
        public void 끊김_없이_이어진다()
        {
            const float step = 0.001f;
            for (float v = 0f; v <= 5f; v += step)
            {
                float jump = Mathf.Abs(Accel.Gain(v + step, Max) - Accel.Gain(v, Max));
                Assert.Less(jump, 0.005f, $"{v} h/s 근처에서 배율이 끊긴다");
            }
        }

        [Test]
        public void 끔은_어떤_빠르기에서도_1배다()
        {
            float max = Accel.MaxGainOf(Strength.Off);
            for (float v = 0f; v <= 20f; v += 0.1f)
            {
                Assert.AreEqual(1f, Accel.Gain(v, max));
            }
        }

        [Test]
        public void 세기는_끔_약_중_강을_돌고_기본은_중이다()
        {
            Assert.AreEqual(Strength.Medium, Accel.DefaultStrength);
            Assert.AreEqual(Strength.Weak, Accel.Next(Strength.Off));
            Assert.AreEqual(Strength.Medium, Accel.Next(Strength.Weak));
            Assert.AreEqual(Strength.Strong, Accel.Next(Strength.Medium));
            Assert.AreEqual(Strength.Off, Accel.Next(Strength.Strong));

            Assert.AreEqual(1f, Accel.MaxGainOf(Strength.Off));
            Assert.Less(Accel.MaxGainOf(Strength.Off), Accel.MaxGainOf(Strength.Weak));
            Assert.Less(Accel.MaxGainOf(Strength.Weak), Accel.MaxGainOf(Strength.Medium));
            Assert.Less(Accel.MaxGainOf(Strength.Medium), Accel.MaxGainOf(Strength.Strong));
        }

        //  ── 손가락 빠르기 ──────────────────────────────────────────────────

        [Test]
        public void 같은_빠르기면_프레임_간격과_상관없이_그_빠르기가_나온다()
        {
            foreach (float fps in new[] { 30f, 60f, 120f })
            {
                var speed = new Accel.FingerSpeed();
                float dt = 1f / fps;
                float last = 0f;
                for (int i = 0; i < 20; i++)
                {
                    last = speed.Add(1.5f * dt, dt);
                }
                Assert.AreEqual(1.5f, last, 1e-3f, $"{fps}fps");
            }
        }

        //  한 프레임에 움직임이 두 번 올 수 있다(두 번째는 지난 시간이 0). 그게 빠르기를 무한대로 만들면 안 된다.
        [Test]
        public void 한_프레임에_두_번_와도_빠르기가_튀지_않는다()
        {
            var speed = new Accel.FingerSpeed();
            const float dt = 1f / 60f;
            for (int i = 0; i < 10; i++)
            {
                speed.Add(0.5f * dt, dt);
            }
            //  같은 프레임에 반씩 두 번 = 한 프레임에 한 번과 같은 거리
            speed.Add(0.25f * dt, dt);
            float v = speed.Add(0.25f * dt, 0f);
            Assert.AreEqual(0.5f, v, 0.02f);
        }

        [Test]
        public void 누르자마자_같은_프레임에_움직여도_0이다()
        {
            var speed = new Accel.FingerSpeed();
            Assert.AreEqual(0f, speed.Add(0.01f, 0f));
        }

        //  오래 가만히 있다가 움직이면 그 긴 시간 전부가 한 번의 움직임에 걸린 것처럼 읽힌다.
        //  쉰 시간은 끊어서 본다 — 그래야 "쉬다가 휙"의 첫 프레임부터 가속이 붙는다.
        [Test]
        public void 쉬었다가_움직여도_쉰_시간_전부로_나누지_않는다()
        {
            var speed = new Accel.FingerSpeed();
            float v = speed.Add(0.05f, 2f);
            Assert.AreEqual(0.05f / Accel.FingerSpeed.MaxGapSeconds, v, 1e-4f);
        }

        [Test]
        public void 초기화하면_예전_빠르기를_잊는다()
        {
            var speed = new Accel.FingerSpeed();
            for (int i = 0; i < 10; i++)
            {
                speed.Add(0.1f, 1f / 60f);
            }
            speed.Reset();
            Assert.AreEqual(0f, speed.Add(0f, 0f));
        }

        //  ── 같은 손짓, 다른 프레임레이트 ─────────────────────────────────────

        //  손가락 빠르기가 0에서 올라갔다 내려오는(사인 반 바퀴) 한 번의 긋기를 fps 간격으로 쪼개 넣고,
        //  배율을 곱한 움직임의 합을 돌려준다. 거리는 프레임마다 그 구간을 적분한 값이다.
        static float Swipe(float fps, float peakSpeed, float seconds, float max, out float distance)
        {
            var speed = new Accel.FingerSpeed();
            float dt = 1f / fps;
            int frames = Mathf.RoundToInt(seconds * fps);
            float turn = 0f;
            distance = 0f;
            for (int i = 0; i < frames; i++)
            {
                float a = i * dt, b = (i + 1) * dt;
                float d = peakSpeed * seconds / Mathf.PI
                          * (Mathf.Cos(Mathf.PI * a / seconds) - Mathf.Cos(Mathf.PI * b / seconds));
                turn += Accel.Gain(speed.Add(d, dt), max) * d;
                distance += d;
            }
            return turn;
        }

        [TestCase(4f, 0.2f)]
        [TestCase(2f, 0.3f)]
        [TestCase(1f, 0.5f)]
        public void 같은_손짓은_30_60_120fps에서_거의_같은_만큼_돈다(float peak, float seconds)
        {
            float t30 = Swipe(30f, peak, seconds, Max, out _);
            float t60 = Swipe(60f, peak, seconds, Max, out _);
            float t120 = Swipe(120f, peak, seconds, Max, out _);
            Assert.AreEqual(t60, t30, t60 * 0.05f, "30fps가 60fps와 다르게 돈다");
            Assert.AreEqual(t60, t120, t60 * 0.05f, "120fps가 60fps와 다르게 돈다");
        }

        [Test]
        public void 휙_그으면_실제로_더_돌고_천천히_밀면_그대로다()
        {
            float fast = Swipe(60f, 4f, 0.2f, Max, out float fastDistance);
            Assert.Greater(fast, fastDistance * 2f, "빠른 긋기에 가속이 거의 안 붙는다");

            float slow = Swipe(60f, 0.25f, 1f, Max, out float slowDistance);
            Assert.AreEqual(slowDistance, slow, 1e-6f, "천천히 미는데 조준이 달라졌다");
        }
    }
}
