using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 경기 소리 한 곳(spec §6): 칸이 비면 합성 클립, 파일을 꽂으면 그게 이긴다. 쿵 때 음악을 0.35로 낮췄다 0.6초에 되돌리고,
    /// 마지막 구간엔 배경음을 1.068배로. 치울 때 자기가 지은 클립만 지운다(꽂힌 파일 에셋을 지우면 다음 판에 소리가 없다).
    /// </summary>
    public class FlappyAudioTests
    {
        private FlappyAudio audio;
        private FlappySoundBank bank;
        private AudioClip file;

        [TearDown]
        public void TearDown()
        {
            audio?.Dispose();
            if (bank != null) { UnityEngine.Object.DestroyImmediate(bank); }
            if (file != null) { UnityEngine.Object.DestroyImmediate(file); }
        }

        [Test]
        public void 칸이_비면_모든_소리가_합성_클립()
        {
            audio = new FlappyAudio(new FlappySoundBankSlot(null));
            audio.Initialize();

            foreach (FlappySfx sfx in Enum.GetValues(typeof(FlappySfx)))
            {
                var clip = audio.ClipOf(sfx);
                Assert.IsNotNull(clip, sfx.ToString());
                Assert.AreEqual(FlappySynth.Render(sfx).Length, clip.samples, sfx.ToString());
            }
            Assert.AreEqual(FlappySynth.MusicLoop().Length, audio.MusicClip.samples);
        }

        [Test]
        public void 파일_클립을_꽂으면_그게_이긴다()
        {
            bank = ScriptableObject.CreateInstance<FlappySoundBank>();
            file = AudioClip.Create("file-crash", 100, 1, 44100, false);
            bank.Set(FlappySfx.Crash, file);
            audio = new FlappyAudio(new FlappySoundBankSlot(bank));
            audio.Initialize();

            Assert.AreSame(file, audio.ClipOf(FlappySfx.Crash));
            Assert.AreNotSame(file, audio.ClipOf(FlappySfx.Go), "꽂지 않은 칸은 여전히 합성");
            Assert.IsNotNull(audio.ClipOf(FlappySfx.Go));
        }

        [Test]
        public void 재생은_그_소리의_클립을_튼다()
        {
            audio = new FlappyAudio(new FlappySoundBankSlot(null));
            var played = new List<AudioClip>();
            audio.playOneShot = played.Add;

            audio.Play(FlappySfx.Go);

            Assert.AreEqual(1, played.Count);
            Assert.AreSame(audio.ClipOf(FlappySfx.Go), played[0], "처음 부를 때 스스로 클립을 짓는다(Initialize 전이어도)");
        }

        [Test]
        public void 음악_낮추기는_0_35에서_0_6초에_되돌아온다()
        {
            audio = new FlappyAudio(new FlappySoundBankSlot(null));
            audio.Initialize();
            audio.Tick(0f);
            Assert.AreEqual(1f, audio.MusicVolume, 1e-4f, "평소엔 그대로");

            audio.DuckMusic();
            audio.Tick(0f);
            Assert.AreEqual(FlappyAudio.DuckLevel, audio.MusicVolume, 1e-4f);
            audio.Tick(0.3f);
            Assert.AreEqual(0.675f, audio.MusicVolume, 1e-3f, "곧게 되돌아간다");
            audio.Tick(0.31f);
            Assert.AreEqual(1f, audio.MusicVolume, 1e-4f);
        }

        [Test]
        public void 마지막_구간이면_빠르게_끄면_원래대로()
        {
            audio = new FlappyAudio(new FlappySoundBankSlot(null));
            audio.StartMusic();
            Assert.IsTrue(audio.MusicPlaying);

            audio.SetFinalStretch(true);
            Assert.AreEqual(FlappyAudio.FinalStretchPitch, audio.MusicPitch, 1e-5f);

            audio.StopMusic();
            Assert.IsFalse(audio.MusicPlaying);
            Assert.AreEqual(1f, audio.MusicPitch, 1e-5f, "다음에 틀 때 빠른 채로 시작하지 않게");
        }

        [Test]
        public void 치운_뒤엔_되살아나지_않는다()
        {
            //  Play·StartMusic·SetFinalStretch가 모두 Initialize()를 느슨하게 부른다 — disposed 깃발이
            //  없으면 치운 뒤에도 GameObject·클립을 다시 지어 되살아난다(이번 수정 전 버그).
            audio = new FlappyAudio(new FlappySoundBankSlot(null));
            audio.Initialize();
            var played = new List<AudioClip>();
            audio.playOneShot = played.Add;

            audio.Dispose();

            Assert.DoesNotThrow(() => audio.Play(FlappySfx.Go));
            Assert.DoesNotThrow(() => audio.StartMusic());
            Assert.DoesNotThrow(() => audio.SetFinalStretch(true));

            Assert.IsEmpty(played, "치운 뒤엔 소리가 나지 않는다");
            Assert.IsFalse(audio.MusicPlaying, "치운 뒤엔 배경음도 켜지지 않는다");
            Assert.IsNull(audio.MusicClip, "클립도 다시 짓지 않는다 — Initialize가 되살리면 안 된다");
        }

        [Test]
        public void 치우면_합성_클립만_지운다()
        {
            bank = ScriptableObject.CreateInstance<FlappySoundBank>();
            file = AudioClip.Create("file-crash", 100, 1, 44100, false);
            bank.Set(FlappySfx.Crash, file);
            audio = new FlappyAudio(new FlappySoundBankSlot(bank));
            audio.Initialize();
            var synth = audio.ClipOf(FlappySfx.Go);

            audio.Dispose();
            audio = null;

            Assert.IsTrue(synth == null, "합성 클립은 지운다(유니티 null)");
            Assert.IsFalse(file == null, "꽂힌 파일은 에셋이다 — 지우면 안 된다");
        }
    }
}
