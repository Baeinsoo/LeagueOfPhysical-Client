namespace LOP
{
    /// <summary>
    /// 캐릭터 머리 위 장식을 모드마다 켜고 끈다. 목숨으로 가는 모드(Dodge)는 체력 바가 헷갈려 그것만 끈다.
    /// 이름표(이름·칭호·배너·레벨)는 몸이 있는 모든 모드에서 켠다 — HP바와 별개 플래그라 기본값이 다르다.
    /// </summary>
    public class CharacterDecorationSettings
    {
        public bool ShowHealthBar { get; }
        public bool ShowNameplate { get; }

        public CharacterDecorationSettings(bool showHealthBar, bool showNameplate = true)
        {
            ShowHealthBar = showHealthBar;
            ShowNameplate = showNameplate;
        }
    }
}
