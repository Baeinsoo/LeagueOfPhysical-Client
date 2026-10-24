using System.Collections.Generic;

namespace LOP
{
    /// <summary>관전 화면이 "누구를 볼 수 있고 지금 누구를 보나"를 묻는 곳. 게임마다 후보를 고르는 규칙이 다르다.</summary>
    public interface ISpectateTargets
    {
        IReadOnlyList<string> Candidates { get; }

        /// <summary>지금 보는 사람. 볼 사람이 없으면 null.</summary>
        string Current { get; }

        /// <summary>후보를 다시 만들고, 보던 사람이 사라졌으면 다시 고른다. 매 틱 부른다.</summary>
        void Refresh();

        void Next();

        void Prev();
    }
}
