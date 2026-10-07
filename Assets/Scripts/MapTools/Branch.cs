using System;

namespace LOP.MapTools
{
    /// <summary>갈림길을 강제로 태울 때 막을 다른 길이 어느 쪽인가.</summary>
    public enum BranchSide { Below, Above }

    public enum BranchKind { Valley, Building, Hill, Mine }

    /// <summary>
    /// 갈림길 하나 = 들어갈 길 칸 + 그 길을 강제할 때 막을 쪽(spec 2026-09-28 §3). 계곡 지름길(아래가 계곡),
    /// 빌딩 위층(아래가 아래층), 언덕 굴(위가 넘는 길), 광산 굴(고수 갈림길의 2번 점프 굴)이 같은 모양이라 검사기·빌더가 한 목록으로 다룬다.
    /// </summary>
    public readonly struct Branch
    {
        public readonly ShortcutRect Rect;
        public readonly BranchSide Other;
        public readonly BranchKind Kind;

        public Branch(ShortcutRect rect, BranchSide other, BranchKind kind)
        {
            Rect = rect; Other = other; Kind = kind;
        }

        public string Label
        {
            get
            {
                switch (Kind)
                {
                    case BranchKind.Building: return "빌딩 위층";
                    case BranchKind.Hill: return "언덕 굴";
                    case BranchKind.Mine: return "광산 굴";
                    default: return "계곡 지름길";
                }
            }
        }

        /// <summary>빌더가 남기는 빈 GameObject 이름. 위치 = 길 칸 가운데, 크기 = (길이, 세로 폭).</summary>
        public string MarkerName => $"Branch_{Rect.X0:F0}_{Other}_{Kind}";

        public static bool TryParse(string name, float cx, float cy, float w, float h, out Branch branch)
        {
            branch = default;
            string[] parts = name.Split('_');
            if (parts.Length != 4 || parts[0] != "Branch") { return false; }
            if (Enum.TryParse(parts[2], out BranchSide side) == false || Enum.IsDefined(typeof(BranchSide), side) == false) { return false; }
            if (Enum.TryParse(parts[3], out BranchKind kind) == false || Enum.IsDefined(typeof(BranchKind), kind) == false) { return false; }
            branch = new Branch(ShortcutRect.FromCenterSize(cx, cy, w, h), side, kind);
            return true;
        }
    }
}
