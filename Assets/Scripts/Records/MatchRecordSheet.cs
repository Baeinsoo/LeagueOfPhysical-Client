using System;
using System.Collections.Generic;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 이번 판 개인 기록지(spec §7) — 게임 스코프 싱글턴, 종목 공용. 종목이 경기 중 <see cref="Set"/>으로 지표 값을 적고
    /// (같은 지표는 마지막 값), 매치 종료 메시지 때(<see cref="MatchEndedMessageHandler"/>) <see cref="Commit"/>이
    /// 저장된 최고와 <b>한 번</b> 비교·저장해 결과에 싣는다.
    /// <para>매치 종료 전에 판을 떠나면(나가기·끊김) 스코프가 치워질 때 <see cref="Dispose"/>가 같은 일을 한다 — 결과 화면은 없어도 기록은 남는다.</para>
    /// <para>종목·맵은 방 정보에서 읽는다(<see cref="MatchResultCapture"/>와 같은 길). 기록지가 방 정보보다 먼저 지어져도 되게 적을 때·확정할 때 다시 읽는다.</para>
    /// </summary>
    public class MatchRecordSheet : IDisposable
    {
        private readonly IPersonalRecordStore _store;
        private readonly Func<(int gameModeId, int mapId)> _ids;
        private readonly List<RecordMetric> _metrics = new List<RecordMetric>();
        private readonly Dictionary<string, double> _values = new Dictionary<string, double>();
        private int _gameModeId;
        private int _mapId;
        private IReadOnlyList<RecordOutcome> _committed;

        [Inject]
        public MatchRecordSheet(IRoomDataStore room, IPersonalRecordStore store)
            : this(() => IdsOf(room?.match), store)
        {
        }

        //  시험용 — 종목·맵을 바로 준다.
        internal MatchRecordSheet(Func<(int gameModeId, int mapId)> ids, IPersonalRecordStore store)
        {
            _ids = ids;
            _store = store;
        }

        public bool Committed => _committed != null;

        /// <summary>지표 값을 적는다. 같은 지표는 마지막 값. 확정 뒤엔 버린다.</summary>
        public void Set(RecordMetric metric, double value)
        {
            if (_committed != null || metric == null || double.IsNaN(value) || double.IsInfinity(value))
            {
                return;
            }
            CatchIds();
            if (_values.ContainsKey(metric.Id) == false)
            {
                _metrics.Add(metric);
            }
            _values[metric.Id] = value;
        }

        /// <summary>판마다 한 번 비교·저장. 두 번째부터는 첫 결과를 그대로 돌려준다. 지표 순서(<see cref="RecordMetric.Order"/>)로.</summary>
        public IReadOnlyList<RecordOutcome> Commit()
        {
            if (_committed != null)
            {
                return _committed;
            }
            CatchIds();
            var sorted = new List<RecordMetric>(_metrics);
            sorted.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));
            var outcomes = new List<RecordOutcome>(sorted.Count);
            foreach (var metric in sorted)
            {
                double value = _values[metric.Id];
                string key = RecordKey.For(_gameModeId, _mapId, metric.Id);
                double previous = 0d;
                bool hasPrevious = key != null && _store.TryGetBest(key, out previous);
                var outcome = new RecordOutcome(metric, value, key != null, hasPrevious, previous);
                if (outcome.ShouldSave)
                {
                    _store.SaveBest(key, value);
                }
                outcomes.Add(outcome);
            }
            _committed = outcomes;
            return _committed;
        }

        public void Dispose() => Commit();

        //  한 번 알면 그대로 쥔다 — 판 끝에 방 정보가 비워져도 처음 안 값으로 저장한다.
        private void CatchIds()
        {
            if (_gameModeId > 0 && _mapId > 0)
            {
                return;
            }
            var (gameModeId, mapId) = _ids();
            if (gameModeId > 0 && mapId > 0)
            {
                _gameModeId = gameModeId;
                _mapId = mapId;
            }
        }

        /// <summary>지금 라운드의 종목·맵. 모르면 (0, 0).</summary>
        internal static (int gameModeId, int mapId) IdsOf(Match match)
        {
            var rounds = match?.rounds;
            if (rounds == null || rounds.Length == 0)
            {
                return (0, 0);
            }
            var round = rounds[MatchSceneResolver.CurrentRoundIndex(rounds.Length)];
            return (round.gameModeId, round.mapId);
        }
    }
}
