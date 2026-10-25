using System.Collections.Generic;

namespace LOP.Tests
{
    /// <summary>시험이 정한 새 목록을 그대로 돌려주는 가짜. 충돌 묶음·기절 별 시험이 쓴다.</summary>
    public sealed class FakeFlappyBirdLooks : IFlappyBirdLooks
    {
        public readonly List<FlappyBirdLook> Birds = new List<FlappyBirdLook>();

        public void Fill(List<FlappyBirdLook> into)
        {
            into.Clear();
            into.AddRange(Birds);
        }
    }
}
