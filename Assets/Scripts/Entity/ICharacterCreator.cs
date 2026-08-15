namespace LOP
{
    /// <summary>
    /// 이 게임에서 플레이어의 몸을 무엇으로 만드는지. 게임 덩어리마다 다른 구현이 끼워진다
    /// (언리얼 GameMode의 DefaultPawnClass에 해당). 데이터만 만들고 뷰는 EntityBinder가 붙인다.
    /// </summary>
    public interface ICharacterCreator
    {
        void Create(CharacterCreationData creationData);
    }
}
