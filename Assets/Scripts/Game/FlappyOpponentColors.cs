using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 상대 id → 색 칸. 경기 스코프에 하나뿐이다 — 진행 막대의 점(RaceHudViewModel)과 새 칠(FlappyOpponentTint)이
    /// 같은 인스턴스에 물어야 "저 점이 저 새"가 된다. 누가 먼저 묻든 처음 물은 순서로 칸이 정해진다.
    /// <para>클라마다 칸이 달라도 된다 — 색은 내 화면 안에서만 뜻이 있다.</para>
    /// </summary>
    public class FlappyOpponentColors
    {
        private readonly Dictionary<string, int> slots = new Dictionary<string, int>();

        public Color ColorOf(string entityId)
        {
            if (slots.TryGetValue(entityId, out int slot) == false)
            {
                slot = slots.Count;
                slots[entityId] = slot;
            }
            return FlappyRacePalette.Opponents[slot % FlappyRacePalette.Opponents.Length];
        }
    }
}
