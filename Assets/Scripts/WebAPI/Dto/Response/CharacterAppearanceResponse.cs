namespace LOP
{
    /// <summary>캐릭터 외형 조회·변경 결과(로비 GET/PUT /user/:id/character-appearance).</summary>
    public class CharacterAppearanceResponse : HttpResponse
    {
        public CharacterAppearanceDto characterAppearance;
    }
}
