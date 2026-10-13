namespace LOP
{
    /// <summary>로비 무대 연출. 일반은 동네 경기(가랜드·색종이), 랭크는 중계 경기(나머지 전부).</summary>
    public enum StageEffect { Bunting, Confetti, Runway, Searchlights, HeroCamera, Signs, Flashes, LightShow, Crowd }

    public static class StageEffectSet
    {
        public static readonly StageEffect[] All = (StageEffect[])System.Enum.GetValues(typeof(StageEffect));

        public static bool IsOn(StageEffect e, bool ranked)
        {
            bool casual = e == StageEffect.Bunting || e == StageEffect.Confetti;
            return ranked ? !casual : casual;
        }
    }
}
