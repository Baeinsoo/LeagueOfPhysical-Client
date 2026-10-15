using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>세트가 무대를 세울 때 아는 것 — 랭크인지, 정렬된 등수표 순서의 등수들, 무승부인지.</summary>
    public readonly struct PodiumContext
    {
        public readonly bool Ranked;
        public readonly IReadOnlyList<int> Placements;
        public readonly bool IsDraw;

        public bool HasResult => Placements.Count > 0;

        public PodiumContext(bool ranked, IReadOnlyList<int> placements, bool isDraw)
        {
            Ranked = ranked;
            Placements = placements ?? new int[0];
            IsDraw = isDraw;
        }
    }

    /// <summary>세트가 세운 결과 — 선수 자리(등수표 순서), 카메라 자세, 등장 순간 장식 시작.</summary>
    public sealed class PodiumStage
    {
        public IReadOnlyList<PodiumSlot> Slots = new PodiumSlot[0];
        public StageCameraPose Camera;
        public System.Action OnReveal;
    }

    /// <summary>
    /// 시상대 세트 — 배경·조명·단·장식을 세운다. 결과 단계는 세트 종류를 모르고 이 계약만 쓴다.
    /// 종목·맵 전용 세트는 이걸 구현해 <see cref="PodiumSetCatalog"/>에 등록만 하면 된다.
    /// </summary>
    public interface IPodiumSet
    {
        PodiumStage Build(Transform root, PodiumContext context);
    }
}
