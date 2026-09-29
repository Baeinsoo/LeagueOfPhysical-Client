namespace LOP
{
    public enum ArcheryCutInKind { None, Comeback, RobinHood, Bull, LastPlace }

    /// <summary>한 라운드에서 컷인 후보가 된 사람들. 없으면 null.</summary>
    public struct ArcheryCutInCandidates
    {
        public string ComebackId;
        public string RobinHoodId;
        public string BullId;
        /// <summary>마지막 라운드에서만 채운다(총점 최하위).</summary>
        public string LastPlaceId;
    }

    public readonly struct ArcheryCutInPick
    {
        public readonly ArcheryCutInKind Kind;
        public readonly string SubjectId;

        public ArcheryCutInPick(ArcheryCutInKind kind, string subjectId)
        {
            Kind = kind;
            SubjectId = subjectId;
        }

        public static readonly ArcheryCutInPick None = new ArcheryCutInPick(ArcheryCutInKind.None, null);
    }

    /// <summary>
    /// 정색 컷인(멋지다 마사루) — 결과 창이 뜨는 순간 그 라운드의 가장 큰 사건 하나. 판당 세 번까지, 당기는 중엔 절대 안 뜬다.
    /// </summary>
    public static class ArcheryCutInPicker
    {
        public const int MaxPerMatch = 3;

        public static ArcheryCutInPick Pick(in ArcheryCutInCandidates c, int usedThisMatch, bool drawing)
        {
            if (drawing || usedThisMatch >= MaxPerMatch)
            {
                return ArcheryCutInPick.None;
            }
            if (c.ComebackId != null) return new ArcheryCutInPick(ArcheryCutInKind.Comeback, c.ComebackId);
            if (c.RobinHoodId != null) return new ArcheryCutInPick(ArcheryCutInKind.RobinHood, c.RobinHoodId);
            if (c.BullId != null) return new ArcheryCutInPick(ArcheryCutInKind.Bull, c.BullId);
            if (c.LastPlaceId != null) return new ArcheryCutInPick(ArcheryCutInKind.LastPlace, c.LastPlaceId);
            return ArcheryCutInPick.None;
        }
    }
}
