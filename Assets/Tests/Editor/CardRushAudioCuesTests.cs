using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 소리 신호(spec 2026-11-05 §11.5): 장애물·구역 하나(번호)에 결과 소리 하나. 되감기가 같은 결과를 다시 만들어도,
    /// 보정이 결과 틱만 옮겨도 안 겹친다(최종 리뷰 m2). 보정이 결과 종류를 바꾸면 새로 운다. 내 몸만 — 남의 소리는 없다.
    /// </summary>
    public class CardRushAudioCuesTests
    {
        private CardRushAudioMemory memory;
        private CardRushAudioFrame last;

        [SetUp]
        public void SetUp()
        {
            memory = default;
            last = new CardRushAudioFrame { Has = true };
            //  첫 장면은 기준만 — 시험마다 빈 장면으로 기준을 잡고 시작한다(앞 장면 없음 = 들어온 직후).
            CardRushAudioCues.Detect(default, last, new List<CardRushSfx>(), ref memory);
        }

        private List<CardRushSfx> Step(CardRushAudioFrame now)
        {
            var into = new List<CardRushSfx>();
            CardRushAudioCues.Detect(last, now, into, ref memory);
            last = now;
            return into;
        }

        //  index = 결과가 난 장애물 번호. 상태는 치운 뒤 다음 번호로 넘어가 있다(ObstacleIndex = index + 1).
        private static CardRushAudioFrame Obstacle(CardRushObstacleEvent ev, long tick, int index = 0)
            => new CardRushAudioFrame { Has = true, ObstacleEvent = (int)ev, ObstacleEventTick = tick, ObstacleIndex = index + 1 };

        [Test]
        public void 와장창은_한_번()
        {
            CollectionAssert.AreEqual(new[] { CardRushSfx.Smash }, Step(Obstacle(CardRushObstacleEvent.Smashed, 1000)));
            CollectionAssert.IsEmpty(Step(Obstacle(CardRushObstacleEvent.Smashed, 1000)), "같은 장면을 또 봐도");
        }

        //  되감기: 앵커(결과 전)로 돌아갔다가 다시 시뮬하면 같은 (와장창, 1000)이 또 나온다 — 한 번만 운다.
        [Test]
        public void 되감아_같은_결과를_다시_봐도_한_번()
        {
            CollectionAssert.AreEqual(new[] { CardRushSfx.Smash }, Step(Obstacle(CardRushObstacleEvent.Smashed, 1000, 4)));
            CollectionAssert.IsEmpty(Step(Obstacle(CardRushObstacleEvent.Lifted, 900, 3)), "되감기 중 옛 결과(앞 장애물)가 보여도 조용");
            CollectionAssert.IsEmpty(Step(Obstacle(CardRushObstacleEvent.Smashed, 1000, 4)), "다시 같은 결과");
        }

        //  최종 리뷰 m2: 늦게 닿은 탭을 서버가 버려 진짜 결과가 더 늦은 틱으로 옮겨도 같은 장애물이면 다시 안 운다(끼익 두 번 금지).
        [Test]
        public void 보정이_결과_틱만_옮기면_같은_장애물은_한_번()
        {
            CollectionAssert.AreEqual(new[] { CardRushSfx.Lift }, Step(Obstacle(CardRushObstacleEvent.Lifted, 100, 4)));
            CollectionAssert.IsEmpty(Step(Obstacle(CardRushObstacleEvent.Lifted, 130, 4)), "같은 장애물, 늦은 결과 틱");
            CollectionAssert.AreEqual(new[] { CardRushSfx.Lift }, Step(Obstacle(CardRushObstacleEvent.Lifted, 400, 5)), "다음 장애물은 운다");
        }

        [Test]
        public void 보정이_결과_종류를_바꾸면_새_소리()
        {
            Step(Obstacle(CardRushObstacleEvent.Smashed, 1000, 2));
            CollectionAssert.AreEqual(new[] { CardRushSfx.Tumble }, Step(Obstacle(CardRushObstacleEvent.Tumbled, 1002, 2)), "같은 장애물 다른 결과");
        }

        [Test]
        public void 결과마다_제_소리()
        {
            var whiff = last; whiff.WhiffTick = 100;
            CollectionAssert.AreEqual(new[] { CardRushSfx.Whiff }, Step(whiff), "헛방은 헛방 틱으로");
            CollectionAssert.AreEqual(new[] { CardRushSfx.Lift }, Step(Obstacle(CardRushObstacleEvent.Lifted, 200, 0)));
            CollectionAssert.AreEqual(new[] { CardRushSfx.Clang }, Step(Obstacle(CardRushObstacleEvent.Blocked, 300, 1)), "막음");
            CollectionAssert.AreEqual(new[] { CardRushSfx.Tumble }, Step(Obstacle(CardRushObstacleEvent.Tumbled, 400, 2)), "꽈당");

            var held = last; held.ZoneEvent = (int)CardRushZoneEvent.Held; held.ZoneEventTick = 500; held.ZoneIndex = 1;
            CollectionAssert.AreEqual(new[] { CardRushSfx.Clang }, Step(held), "버팀");
            var hit = held; hit.ZoneEvent = (int)CardRushZoneEvent.Hit; hit.ZoneEventTick = 600; hit.ZoneIndex = 2;
            CollectionAssert.AreEqual(new[] { CardRushSfx.Squash }, Step(hit), "으악");
            var dodged = hit; dodged.ZoneEvent = (int)CardRushZoneEvent.Dodged; dodged.ZoneEventTick = 700; dodged.ZoneIndex = 3;
            CollectionAssert.IsEmpty(Step(dodged), "빠져나감은 조용");
        }

        //  리뷰 I1: 막음 결과 칸은 그대로 두고 헛방 틱만 바뀐다 — 헛방 소리 하나, 막음은 다시 안 운다.
        [Test]
        public void 막음_직후_헛방은_헛방만_운다()
        {
            CollectionAssert.AreEqual(new[] { CardRushSfx.Clang }, Step(Obstacle(CardRushObstacleEvent.Blocked, 500)));
            var whiff = Obstacle(CardRushObstacleEvent.Blocked, 500); whiff.WhiffTick = 505;
            CollectionAssert.AreEqual(new[] { CardRushSfx.Whiff }, Step(whiff));
            CollectionAssert.IsEmpty(Step(whiff), "되감아 같은 헛방");
        }

        [Test]
        public void 구역이_켜지면_띠링_한_번()
        {
            var on = last; on.ZoneTick = 800; on.ZoneIndex = 0;
            CollectionAssert.AreEqual(new[] { CardRushSfx.ZoneOn }, Step(on));
            CollectionAssert.IsEmpty(Step(on));
            var off = on; off.ZoneTick = 0; off.ZoneIndex = 1; off.ZoneEvent = (int)CardRushZoneEvent.Dodged; off.ZoneEventTick = 870;
            CollectionAssert.IsEmpty(Step(off), "터져서 꺼짐(빠져나감은 조용)");
            CollectionAssert.IsEmpty(Step(on), "되감아 같은 켜짐");
            var next = on; next.ZoneTick = 1500; next.ZoneIndex = 1;
            CollectionAssert.AreEqual(new[] { CardRushSfx.ZoneOn }, Step(next), "다음 구역");
        }

        //  최종 리뷰 m2: 트리거를 밟는 틱이 보정으로 밀려도 같은 구역이면 띠링·쿵은 한 번.
        [Test]
        public void 보정이_구역_틱을_옮겨도_같은_구역은_한_번()
        {
            var on = last; on.ZoneTick = 800; on.ZoneIndex = 3;
            CollectionAssert.AreEqual(new[] { CardRushSfx.ZoneOn }, Step(on));
            var later = on; later.ZoneTick = 830;
            CollectionAssert.IsEmpty(Step(later), "같은 구역, 늦게 켜짐");

            var hit = later; hit.ZoneTick = 0; hit.ZoneIndex = 4; hit.ZoneEvent = (int)CardRushZoneEvent.Hit; hit.ZoneEventTick = 870;
            CollectionAssert.AreEqual(new[] { CardRushSfx.Squash }, Step(hit));
            var hitLater = hit; hitLater.ZoneEventTick = 900;
            CollectionAssert.IsEmpty(Step(hitLater), "같은 구역, 늦게 터짐");
        }

        [Test]
        public void 방패_대시는_쓴_틱에_한_번()
        {
            var shield = last; shield.BlockEndTick = 1050;
            CollectionAssert.AreEqual(new[] { CardRushSfx.Shield }, Step(shield));
            CollectionAssert.IsEmpty(Step(shield));
            var dash = shield; dash.DashStartTick = 1100;
            CollectionAssert.AreEqual(new[] { CardRushSfx.Dash }, Step(dash));
            var ended = dash; ended.DashStartTick = 0;
            CollectionAssert.IsEmpty(Step(ended), "대시가 끝나 0이 되는 건 소리 없음");
        }

        [Test]
        public void 완주는_한_번_카드는_넘어갈_때_째깍()
        {
            var face = last; face.FaceUp = true; face.Color = CardColor.Red;
            CollectionAssert.AreEqual(new[] { CardRushSfx.CardTick }, Step(face), "새로 앞면");
            CollectionAssert.IsEmpty(Step(face));
            var yellow = face; yellow.Color = CardColor.Yellow;
            CollectionAssert.AreEqual(new[] { CardRushSfx.CardTick }, Step(yellow), "색이 바뀜");
            var back = yellow; back.FaceUp = false;
            CollectionAssert.IsEmpty(Step(back), "뒷면이 되는 건 소리 없음");

            var done = back; done.FinishTick = 5000;
            CollectionAssert.AreEqual(new[] { CardRushSfx.Fanfare }, Step(done));
            var moved = done; moved.FinishTick = 5002;
            CollectionAssert.IsEmpty(Step(moved), "보정이 결승 틱을 옮겨도 다시 안 운다");
        }

        [Test]
        public void 첫_장면은_기준만_잡는다()
        {
            memory = default;
            var into = new List<CardRushSfx>();
            var now = new CardRushAudioFrame { Has = true, ObstacleEvent = (int)CardRushObstacleEvent.Smashed, ObstacleEventTick = 900, ZoneTick = 950, FinishTick = 0 };
            CardRushAudioCues.Detect(default, now, into, ref memory);
            CollectionAssert.IsEmpty(into, "들어오자마자 지난 일로 소리 내지 않음");
            CardRushAudioCues.Detect(now, now, into, ref memory);
            CollectionAssert.IsEmpty(into);
        }

        // ── 재생(크기·클립) ──

        [Test]
        public void 소리는_다_지어지고_정한_크기로_난다()
        {
            var audio = new CardRushAudio(null, null, null, null);
            var played = new List<(AudioClip clip, float volume)>();
            audio.playOneShot = (c, v) => played.Add((c, v));
            try
            {
                foreach (CardRushSfx sfx in System.Enum.GetValues(typeof(CardRushSfx)))
                {
                    var clip = audio.ClipOf(sfx);
                    Assert.IsNotNull(clip, sfx.ToString());
                    Assert.Greater(clip.samples, 0, sfx.ToString());
                }
                audio.Observe(new CardRushAudioFrame { Has = true });
                audio.Observe(Obstacle(CardRushObstacleEvent.Smashed, 100));
                Assert.AreEqual(1, played.Count);
                Assert.AreSame(audio.ClipOf(CardRushSfx.Smash), played[0].clip);
                Assert.AreEqual(Mathf.Pow(10f, CardRushAudio.SmashVolumeDb / 20f), played[0].volume, 1e-4f);
                Assert.AreEqual(CardRushAudio.Gain(-18f), CardRushAudio.VolumeOf(CardRushSfx.CardTick), 1e-5f, "째깍 −18 dB");
                Assert.Greater(CardRushAudio.VolumeOf(CardRushSfx.Tumble), CardRushAudio.VolumeOf(CardRushSfx.Smash), "몸개그는 크게");
            }
            finally
            {
                audio.Dispose();
            }
        }

        [Test]
        public void 공용_경기_화면_소리도_나고_완주_팡파르는_한_번만()
        {
            var audio = new CardRushAudio(null, null, null, null);
            var played = new List<AudioClip>();
            audio.playOneShot = (c, v) => played.Add(c);
            try
            {
                audio.Play(FlappySfx.RankUp);
                audio.Play(FlappySfx.Stamp);
                audio.Play(FlappySfx.Finish);
                Assert.AreEqual(2, played.Count, "결승 팡파르는 상태에서 이미 났다 — 경기 화면의 Finish는 삼킨다");
                Assert.AreSame(audio.ClipOf(FlappySfx.RankUp), played[0]);
            }
            finally
            {
                audio.Dispose();
            }
        }
    }
}
