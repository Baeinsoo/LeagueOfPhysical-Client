using System.Collections.Generic;

namespace LOP.Tests
{
    /// <summary>"어느 사건에 어느 소리가 불렸나"만 적는 가짜 재생기(spec §8). 소리를 쓰는 연출 시험이 함께 쓴다.</summary>
    public sealed class FakeFlappyAudio : IFlappyAudio
    {
        public readonly List<FlappySfx> Played = new List<FlappySfx>();
        public int MusicStarts;
        public int MusicStops;
        public int Ducks;
        public bool FinalStretch;

        public void Play(FlappySfx sfx) => Played.Add(sfx);
        public void StartMusic() => MusicStarts++;
        public void StopMusic() => MusicStops++;
        public void SetFinalStretch(bool on) => FinalStretch = on;
        public void DuckMusic() => Ducks++;
    }
}
