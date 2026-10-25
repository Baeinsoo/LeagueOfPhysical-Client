namespace LOP.UI
{
    /// <summary>
    /// 날갯짓 안내를 언제 거둘지. 세 번 눌러 봤으면 조작을 안 것이다(시제품 `flapsDone<3`).
    /// 카운트다운 중에 누른 것도 센다 — 그때 누른 사람도 이미 아는 사람이다.
    /// </summary>
    public class FlapHintCounter
    {
        public const int FlapsToHide = 3;

        public int Flaps { get; private set; }

        public bool Visible => Flaps < FlapsToHide;

        public void Count()
        {
            if (Flaps < FlapsToHide)
            {
                Flaps++;
            }
        }
    }
}
