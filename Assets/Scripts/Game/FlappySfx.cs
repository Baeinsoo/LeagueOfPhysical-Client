namespace LOP
{
    /// <summary>
    /// 플래피 효과음 목록(spec §6). 소리 하나에 클립 하나 — 지금은 코드로 지은 임시 클립이고, 파일이 생기면
    /// <see cref="FlappySoundBank"/>의 같은 칸에 꽂는다.
    /// </summary>
    public enum FlappySfx
    {
        Flap,
        Crash,
        CrashFar,
        CountBeat,
        Go,
        Finish,
        Stamp,
        RankUp,
        RankDown,
        Section,
        LastStretch,
        Rumble,
        NewBest,
        Milestone,
    }

    /// <summary>
    /// 경기 소리 창구. 연출 쪽은 이것만 안다 — 시험은 가짜 재생기로 "어느 사건에 어느 소리"만 본다(spec §8).
    /// 실물은 <see cref="FlappyAudio"/>.
    /// </summary>
    public interface IFlappyAudio
    {
        void Play(FlappySfx sfx);

        /// <summary>배경음 루프를 튼다. 이미 틀었으면 아무것도 안 한다.</summary>
        void StartMusic();

        void StopMusic();

        /// <summary>마지막 구간이면 배경음을 빠르고 높게(조각 3이 부른다).</summary>
        void SetFinalStretch(bool on);

        /// <summary>쿵 같은 큰 소리 때 배경음을 잠깐 낮췄다가 되돌린다.</summary>
        void DuckMusic();
    }
}
