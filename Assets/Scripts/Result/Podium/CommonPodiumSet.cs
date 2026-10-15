using UnityEngine;

namespace LOP
{
    /// <summary>공용 "피지컬 리그 시상식" 세트 — P1 자리.</summary>
    public sealed class CommonPodiumSet : IPodiumSet
    {
        public PodiumStage Build(Transform root, PodiumContext context) =>
            new PodiumStage { Slots = PodiumLayout.P1(context.Placements, context.IsDraw) };
    }
}
