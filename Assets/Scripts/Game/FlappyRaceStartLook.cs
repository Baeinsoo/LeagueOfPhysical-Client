namespace LOP
{
    /// <summary>플래피 카운트다운(spec §3): 숫자 팝 + 박자, GO! 초록 + 강한 소리.</summary>
    public sealed class FlappyRaceStartLook : LOP.UI.IRaceStartLook
    {
        private readonly IFlappyAudio audio;

        public FlappyRaceStartLook(IFlappyAudio audio)
        {
            this.audio = audio;
        }

        public bool Animated => true;

        public void Beat() => audio.Play(FlappySfx.CountBeat);

        public void Go() => audio.Play(FlappySfx.Go);
    }
}
