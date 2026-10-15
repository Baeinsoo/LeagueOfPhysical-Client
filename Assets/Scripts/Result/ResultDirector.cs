using System;
using System.Collections.Generic;
using LOP.UI;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 결과 단계 지휘 — 종목·맵으로 시상대 세트를 골라 세우고, 등수표 순서대로 선수를 자리에 세운 뒤,
    /// 등장 순간 한 번 반응과 장식을 건다. 세트 종류는 모른다(IPodiumSet 계약만).
    /// </summary>
    public sealed class ResultDirector
    {
        public const float ReadyTimeoutSeconds = 1f;

        private readonly PodiumSetCatalog _catalog;
        private readonly Func<Transform, string, IPodiumFigure> _figureFactory;
        private readonly List<IPodiumFigure> _figures = new List<IPodiumFigure>();
        private readonly List<ResultReaction> _reactions = new List<ResultReaction>();
        private bool _revealed;

        public PodiumStage Stage { get; private set; }
        public IReadOnlyList<IPodiumFigure> Figures => _figures;

        /// <param name="figureFactory">(부모, userId) → 선수 하나. 시험이 가짜를 꽂는다.</param>
        public ResultDirector(PodiumSetCatalog catalog, Func<Transform, string, IPodiumFigure> figureFactory)
        {
            _catalog = catalog;
            _figureFactory = figureFactory;
        }

        public void Begin(Transform root, MatchResult result, bool ranked)
        {
            var sorted = MatchResultOrder.Sort(result?.participants);
            var placements = sorted.ConvertAll(p => p.placement);
            bool isDraw = MatchResultViewModel.IsDrawn(placements);

            var set = _catalog.Resolve(result?.gameModeId ?? 0, result?.mapId ?? 0);
            Stage = set.Build(root, new PodiumContext(ranked, placements, isDraw)) ?? new PodiumStage();

            int count = Mathf.Min(sorted.Count, Stage.Slots.Count);
            for (int i = 0; i < count; i++)
            {
                var p = sorted[i];
                var slot = Stage.Slots[i];
                var figure = _figureFactory(root, p.userId);
                figure.Stand(slot.Position, slot.Yaw, slot.Scale);
                figure.SetLook(p.look);
                figure.SetAbsent(MatchResultViewModel.IsLeft(p.stats));
                _figures.Add(figure);
                _reactions.Add(ResultReactionRule.For(p.placement, isDraw));
            }
        }

        /// <summary>전원 몸이 붙었거나 기다린 시간이 다 됐다 — 못 붙은 선수는 붙는 순간 반응한다.</summary>
        public bool Ready(float waitedSeconds)
        {
            if (waitedSeconds >= ReadyTimeoutSeconds) return true;
            foreach (var f in _figures)
            {
                if (!f.HasBody) return false;
            }
            return true;
        }

        public void Reveal()
        {
            if (_revealed) return;
            _revealed = true;
            for (int i = 0; i < _figures.Count; i++) _figures[i].React(_reactions[i]);
            Stage?.OnReveal?.Invoke();
        }
    }
}
