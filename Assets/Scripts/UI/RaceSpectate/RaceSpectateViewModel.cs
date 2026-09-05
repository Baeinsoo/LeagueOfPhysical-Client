namespace LOP.UI
{
    /// <summary>
    /// 관전 화면 ViewModel. 지금 몇 번째 사람을 보는지만 알려준다 —
    /// 인게임에 표시 이름이 없어 이름은 띄울 수 없다(순위처럼 보이는 문구도 쓰지 않는다:
    /// 완주해서 앞서 있는 사람이 후보에서 빠지므로 거짓말이 된다).
    /// </summary>
    public class RaceSpectateViewModel
    {
        private readonly FlappySpectate _spectate;

        public RaceSpectateViewModel(FlappySpectate spectate)
        {
            _spectate = spectate;
        }

        /// <summary>화면에 띄울 문구. 볼 사람이 없으면 빈 문자열.</summary>
        public string StatusText()
        {
            int index = IndexOfCurrent();
            return index < 0 ? string.Empty : $"관전 {index + 1} / {_spectate.Candidates.Count}";
        }

        public void Next() => _spectate.Next();

        public void Prev() => _spectate.Prev();

        //  IReadOnlyList에는 IndexOf가 없다. 매 프레임 도는 코드라 LINQ 대신 직접 훑는다.
        private int IndexOfCurrent()
        {
            string current = _spectate.Current;
            if (current == null)
            {
                return -1;
            }

            var candidates = _spectate.Candidates;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == current)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
