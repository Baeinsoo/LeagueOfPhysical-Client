using System;
using System.Collections.Generic;
using VContainer.Unity;

namespace LOP
{
    /// <summary>연속 날갯짓의 한 걸음 — 그 수, 보상 걸음의 단계(<see cref="FlapComboLook.StepTier"/>, 아니면 −1),
    /// 대시 게이지를 실제로 줬는지, 큰 걸음(50과 그 뒤 50마다)인지.</summary>
    public readonly struct FlapComboBeat
    {
        public readonly int Count;
        public readonly int Tier;
        public readonly bool DashGain;
        public readonly bool Big;

        public FlapComboBeat(int count, int tier, bool dashGain, bool big = false)
        {
            Count = count;
            Tier = tier;
            DashGain = dashGain;
            Big = big;
        }
    }

    /// <summary>
    /// 연속 날갯짓(spec 2026-10-27 §5) — 내 새의 <b>시뮬 수</b>(<see cref="FlappyFlapCombo"/>, 서버가 고치는 값)를 매 장 읽어 연출에 알리고,
    /// 이번 판 최고를 기록지에 적는다. 보이는 수가 곧 고비 보상을 받는 수다. 판정과 무관하다(읽기만 한다).
    /// <para>수의 변화는 <see cref="FlapStreak"/>가 가른다. 보상 걸음(간격마다)은 맵 규칙(<see cref="FlappyMapRulesField.FlapCombo"/>)으로 보고,
    /// "대시+"는 그 걸음이 이 맵에서 게이지를 주고 직전 장 게이지가 가득이 아니었을 때만 — 시뮬이 실제로 더한 때와 같다.</para>
    /// <para>그리는 쪽(<see cref="FlappyFlapComboFx"/>)은 <see cref="Flapped"/>·<see cref="Crashed"/>·<see cref="Racing"/>만 본다.</para>
    /// </summary>
    public sealed class FlappyFlapStreak : ITickable
    {
        private readonly GameFramework.World.EntityRegistry _entityRegistry;
        private readonly IGameDataStore _gameDataStore;
        private readonly FlappyMapRulesField _rules;
        private readonly MatchRecordSheet _sheet;
        private readonly FlapStreak _streak = new FlapStreak();
        private readonly List<int> _shown = new List<int>();
        private float _lastCharge;
        private bool _racing;

        /// <summary>시뮬이 받은 날갯짓 하나 — 그 걸음.</summary>
        public event Action<FlapComboBeat> Flapped;

        /// <summary>내 새가 새로 기절해 연속이 0이 됐다.</summary>
        public event Action Crashed;

        internal int Count => _streak.Count;
        internal int Best => _streak.Best;

        /// <summary>내 새가 달리는 중(기절·결승·몸 없음이 아님). 꼬리는 이때만 그린다.</summary>
        public bool Racing => _racing;

        public FlappyFlapStreak(GameFramework.World.EntityRegistry entityRegistry, IGameDataStore gameDataStore,
                                FlappyMapRulesField rules, MatchRecordSheet sheet)
        {
            _entityRegistry = entityRegistry;
            _gameDataStore = gameDataStore;
            _rules = rules;
            _sheet = sheet;
        }

        public void Tick()
        {
            //  판 시작 직후엔 내 id가 비어 있다 — 사전에 null 키를 넣으면 터진다.
            string id = _gameDataStore.userEntityId;
            var mine = string.IsNullOrEmpty(id) ? null : _entityRegistry.Get(id);
            var combo = mine?.Get<FlappyFlapCombo>();
            if (combo == null)
            {
                _racing = false;
                return;
            }

            int bestBefore = _streak.Best;
            _shown.Clear();
            if (_streak.Observe(combo.Count, _shown))
            {
                Crashed?.Invoke();
            }
            var comboRules = _rules.FlapCombo;
            foreach (int n in _shown)
            {
                //  게이지를 주는 걸음마다(기본 10마다) 한마디·고리·화음 — 단계는 수로 오르고, 50과 그 뒤 50마다만 크게(사용자 결정 11-02).
                int tier = FlapComboLook.StepTier(comboRules, n);
                //  가득 찬 게이지엔 더해진 게 없다 — 그때 "대시+"를 붙이면 보이는 것과 받는 것이 갈린다.
                bool dashGain = tier >= 0 && _rules.ManualDash && comboRules.Gain(n) > 0f
                                && _lastCharge < FlappyDash.MaxCharge;
                Flapped?.Invoke(new FlapComboBeat(n, tier, dashGain, FlapComboLook.StepBig(comboRules, n)));
            }
            if (_streak.Best > bestBefore)
            {
                _sheet.Set(FlappyRecordMetrics.FlapStreak, _streak.Best);
            }

            _lastCharge = mine.Get<FlappyDash>()?.Charge ?? 0f;
            bool stunned = (mine.Get<FlappyStun>()?.StunRemaining ?? 0f) > 0f;
            _racing = stunned == false && mine.Get<FinishState>()?.Finished != true;
        }
    }
}
