namespace LOP
{
    /// <summary>
    /// 컷인 방아쇠 — 결과 이벤트 때 고른 것을 들고 있다가, 결과 창이 <b>보이기 시작하는 프레임</b>에 한 번 쏜다.
    /// 그 순간 당기는 중이거나 예산이 없으면 버린다. 쏘든 버리든 그 결과의 것은 다시 안 뜬다.
    /// </summary>
    public sealed class ArcheryCutInTrigger
    {
        private ArcheryCutInPick pending = ArcheryCutInPick.None;
        private bool lastVisible;

        public void OnResult(ArcheryCutInPick pick) => pending = pick;

        /// <returns>이번 프레임에 시작할 컷인. 없으면 None.</returns>
        public ArcheryCutInPick Tick(bool resultVisible, bool drawing, int used)
        {
            bool opened = resultVisible && lastVisible == false;
            lastVisible = resultVisible;
            if (opened == false)
            {
                return ArcheryCutInPick.None;
            }
            var pick = pending;
            pending = ArcheryCutInPick.None;
            if (pick.Kind == ArcheryCutInKind.None || drawing || used >= ArcheryCutInPicker.MaxPerMatch)
            {
                return ArcheryCutInPick.None;
            }
            return pick;
        }
    }
}
