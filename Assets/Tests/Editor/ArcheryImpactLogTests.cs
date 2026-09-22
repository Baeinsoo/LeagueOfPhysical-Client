using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    using Log = LOP.ArcheryImpactLog;

    //  착탄 기록판이 재는 것은 "내 화살이 과녁 중심에서 **어느 쪽으로** 얼마나 빗나갔나"다.
    //  방향이 뒤집히면 "왼쪽으로 빠진다"를 오른쪽으로 읽어 **반대로 고치게 된다** — 기록판이
    //  없는 것보다 나쁘다. 그래서 부호를 못 박는다.
    public class ArcheryImpactLogTests
    {
        //  사수가 −Z에, 과녁이 +Z에 있는 배치. 과녁은 사수를 보므로 facing은 −Z다.
        static readonly Vector3 Facing = new Vector3(0f, 0f, -1f);

        [Test]
        public void 오른쪽으로_빗나가면_x가_양수다()
        {
            var v = Log.ToFaceOffset(new Vector3(0.5f, 0f, 0f), Facing, radius: 1f);

            Assert.AreEqual(0.5f, v.x, 1e-4f, "사수 기준 오른쪽인데 x가 그만큼이 아니다");
            Assert.AreEqual(0f, v.y, 1e-4f);
        }

        [Test]
        public void 위로_빗나가면_y가_양수다()
        {
            var v = Log.ToFaceOffset(new Vector3(0f, 0.25f, 0f), Facing, radius: 1f);

            Assert.AreEqual(0.25f, v.y, 1e-4f);
            Assert.AreEqual(0f, v.x, 1e-4f);
        }

        //  과녁이 반대쪽(사수가 +Z)에 서면 사수가 보는 "오른쪽"도 뒤집힌다.
        //  facing의 부호를 한 번만 빠뜨려도 여기서 걸린다.
        [Test]
        public void 과녁이_반대쪽을_보면_좌우도_뒤집힌다()
        {
            var a = Log.ToFaceOffset(new Vector3(0.5f, 0f, 0f), Facing, radius: 1f);
            var b = Log.ToFaceOffset(new Vector3(0.5f, 0f, 0f), -Facing, radius: 1f);

            Assert.AreEqual(-a.x, b.x, 1e-4f, "과녁을 돌렸는데 좌우가 그대로다");
        }

        //  면 반지름을 1로 본 값이라, 같은 거리라도 과녁이 크면 비율이 작아진다.
        //  이게 있어야 12m(40cm)와 90m(122cm) 점이 같은 그림 위에서 비교된다.
        [Test]
        public void 면_반지름으로_나눈_비율이다()
        {
            var big = Log.ToFaceOffset(new Vector3(0.305f, 0f, 0f), Facing, radius: 0.61f);
            var small = Log.ToFaceOffset(new Vector3(0.10f, 0f, 0f), Facing, radius: 0.20f);

            Assert.AreEqual(0.5f, big.x, 1e-3f, "122cm 과녁의 절반 지점이 0.5가 아니다");
            Assert.AreEqual(0.5f, small.x, 1e-3f, "40cm 과녁의 절반 지점이 0.5가 아니다");
        }

        //  자리가 바뀌면 비운다 — 거리마다 리드가 달라 점이 섞이면 경향을 못 읽는다.
        [Test]
        public void 자리가_바뀌면_지운다()
        {
            var log = new Log();
            log.Add(0, new Log.Shot(new Vector2(0.1f, 0f), 10, Vector3.zero));
            log.Add(0, new Log.Shot(new Vector2(0.2f, 0f), 8, Vector3.zero));
            Assert.AreEqual(2, log.Shots.Count);

            log.Add(1, new Log.Shot(new Vector2(0.3f, 0f), 5, Vector3.zero));

            Assert.AreEqual(1, log.Shots.Count, "자리가 바뀌었는데 앞 자리 점이 남아 있다");
            Assert.AreEqual(1, log.Wave);
            Assert.AreEqual(0.3f, log.Shots[0].FaceOffset.x, 1e-4f);
        }

        [Test]
        public void 같은_자리면_쌓인다()
        {
            var log = new Log();
            for (int i = 0; i < 3; i++)
            {
                log.Add(2, new Log.Shot(new Vector2(0.1f * i, 0f), 10, Vector3.zero));
            }

            Assert.AreEqual(3, log.Shots.Count, "같은 자리인데 지워졌다");
        }

        //  반지름이 0이면 나눗셈이 터진다. 과녁이 아직 안 선 틱 등에서 들어올 수 있다.
        [Test]
        public void 반지름이_0이면_0을_돌려준다()
        {
            Assert.AreEqual(Vector2.zero, Log.ToFaceOffset(new Vector3(1f, 1f, 0f), Facing, radius: 0f));
        }
    }
}
