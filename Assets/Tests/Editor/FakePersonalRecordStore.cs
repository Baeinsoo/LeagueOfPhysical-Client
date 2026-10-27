using System.Collections.Generic;

namespace LOP.Tests
{
    /// <summary>기기 저장 대신 사전에 적는 가짜 기록 저장소. 저장 횟수도 센다 — "판마다 한 번 저장"을 본다.</summary>
    public sealed class FakePersonalRecordStore : IPersonalRecordStore
    {
        public readonly Dictionary<string, double> Values = new Dictionary<string, double>();
        public int Saves;

        public bool TryGetBest(string key, out double best)
        {
            best = 0d;
            return key != null && Values.TryGetValue(key, out best);
        }

        public void SaveBest(string key, double best)
        {
            Values[key] = best;
            Saves++;
        }
    }
}
