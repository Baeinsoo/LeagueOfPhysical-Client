namespace LOP
{
    /// <summary>캐릭터 머리 위 장식을 모드마다 켜고 끈다. 목숨으로 가는 모드(Dodge)는 체력 바가 헷갈린다.</summary>
    public class CharacterDecorationSettings
    {
        public bool ShowHealthBar { get; }

        public CharacterDecorationSettings(bool showHealthBar)
        {
            ShowHealthBar = showHealthBar;
        }
    }
}
