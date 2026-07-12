using UnityEngine;

namespace LOP
{
    /// <summary>서버가 GameInfo로 보낸 매치 씨앗 보관(클라). A2.1은 저장만 — 예측 소비는 A2.4.</summary>
    public class MatchSeed : IMatchSeed
    {
        public ulong Value { get; private set; }

        public void Set(ulong value)
        {
            Value = value;
            Debug.Log($"[MatchSeed] received {value}");
        }
    }
}
