namespace LOP
{
    /// <summary>로드아웃 변경 결과(로비 PUT /user/:id/economy/loadout).</summary>
    public class SetLoadoutResponse : HttpResponse
    {
        public LoadoutSlotDto[] loadout;
    }
}
