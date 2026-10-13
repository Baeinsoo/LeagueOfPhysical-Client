using R3;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 로비 홈 바인더. 버튼 목적지는 FrontEndRoutes 표, 화면 교체는 FrontEndCoordinator,
    /// 매칭 흐름은 MatchmakingCoordinator가 맡는다. 랭크면 루트에 .ranked를 붙여 톤만 바꾼다.
    /// </summary>
    public class LobbyHomeView : UIView
    {
        private const long CasualHalfBreathMs = 900;
        private const long RankedHalfBreathMs = 1300;
        private const long TickerIntervalMs = 4000;
        private const long NameRefreshMs = 500;
        private const long LiveBlinkMs = 600;

        private static readonly string[] CasualTicker = { "오늘의 경기 · 피지컬 리그", "새 시즌 진행 중" };
        private const string RankedTicker = "리그 경기 · 승점이 걸려 있습니다 · 나가면 꼴찌로 기록";

        private readonly MatchmakingViewModel _matchmaking;
        private readonly LobbyHomeViewModel _viewModel;
        private readonly EconomyStore _economy;
        private readonly IUserDataStore _userData;
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();

        private Breather _breather;
        private IVisualElementScheduledItem _tickerItem;
        private IVisualElementScheduledItem _nameItem;
        private IVisualElementScheduledItem _liveItem;
        private bool _liveOn = true;
        private string _shownName;
        private int _tickerIndex;

        public LobbyHomeView(MatchmakingViewModel matchmaking, LobbyHomeViewModel viewModel, EconomyStore economy, IUserDataStore userData)
        {
            _matchmaking = matchmaking;
            _viewModel = viewModel;
            _economy = economy;
            _userData = userData;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            foreach (var route in FrontEndRoutes.LobbyButtons)
            {
                var destination = route.Value;
                Root.Q<Button>(route.Key).clicked += () => _viewModel.Navigate(destination);
            }
            Root.Q<Button>("play-button").clicked += OnPlayClicked;
            Root.Q<Button>("queue-casual").clicked += () => _matchmaking.SelectQueue(QueueKind.Casual);
            Root.Q<Button>("queue-ranked").clicked += () => _matchmaking.SelectQueue(QueueKind.Ranked);

            RefreshName();
            //  이름 바꾸기는 로비 위 창에서 일어나고 스토어는 알림을 내지 않는다 — 가볍게 자주 다시 읽는다.
            _nameItem = Root.schedule.Execute(RefreshName).Every(NameRefreshMs);

            _breather = new Breather(Root.Q("play-button"), CasualHalfBreathMs);
            _tickerItem = Root.schedule.Execute(AdvanceTicker).Every(TickerIntervalMs);

            _matchmaking.SelectedQueue.Subscribe(OnQueueSelected).AddTo(_subscriptions);
            _matchmaking.SelectedGameIndex.Subscribe(_ => RefreshGameChip()).AddTo(_subscriptions);
            _matchmaking.SelectedMapIndex.Subscribe(_ => RefreshGameChip()).AddTo(_subscriptions);
            _matchmaking.RankCard.Subscribe(OnRankCard).AddTo(_subscriptions);
            _matchmaking.RecentPlacements.Subscribe(OnRecentPlacements).AddTo(_subscriptions);
            _liveItem = Root.schedule.Execute(BlinkLive).Every(LiveBlinkMs);

            _economy.Progress.CombineLatest(_economy.Coins, (progress, coins) => CoinChipText(progress, coins))
                .Subscribe(text => Root.Q<Label>("coin-value").text = $"🪙 {text}").AddTo(_subscriptions);
            _economy.Progress.Subscribe(progress => Root.Q<Label>("player-meta").text = $"· {LevelChipText(progress)}").AddTo(_subscriptions);
        }

        //  한 번도 못 받았으면 "0"·"Lv 1"이 아니라 "-" — 실패한 조회를 진짜 0원으로 보이게 하지 않는다.
        //  코인과 진행도는 같은 조회로 함께 오므로 진행도 유무로 "받았나"를 가른다.
        public static string CoinChipText(ProgressDto progress, long coins) => progress == null ? "-" : coins.ToString("N0");

        public static string LevelChipText(ProgressDto progress) => progress == null ? "-" : $"Lv {progress.level}";

        private void OnQueueSelected(QueueKind kind)
        {
            bool ranked = kind == QueueKind.Ranked;
            Root.Q("lobbyhome-root").EnableInClassList("ranked", ranked);
            Root.Q<Button>("queue-casual").EnableInClassList("kit-toggle__opt--on", !ranked);
            Root.Q<Button>("queue-ranked").EnableInClassList("kit-toggle__opt--on", ranked);
            Root.Q<Label>("play-label").text = ranked ? "출전" : "PLAY";
            Root.Q<Label>("ticker-tag").text = ranked ? "RANKED" : "LIVE";
            _breather.SetHalfPeriod(ranked ? RankedHalfBreathMs : CasualHalfBreathMs);
            _tickerIndex = 0;
            AdvanceTicker();
        }

        private void OnRankCard(RankCard card)
        {
            Root.Q<Label>("rank-head").text = card.Headline ?? string.Empty;
            Root.Q<Label>("rank-sub").text = card.Subline ?? string.Empty;
            Root.Q<TierShapeElement>("rank-tier").SetTier(card.TierCode);
            Root.Q<TierShapeElement>("player-tier").SetTier(card.TierCode);
            //  표시 여부는 USS가 정한다(.has-tier, 랭크면 숨김) — 인라인 display는 USS를 이겨서 쓰지 않는다.
            Root.Q("lobbyhome-root").EnableInClassList("has-tier", card.TierCode != null);
            Root.Q<Label>("lower-rank").text = string.IsNullOrEmpty(card.Headline) ? string.Empty : $"★ {card.Headline}";
            Root.Q<Label>("lower-sub").text = card.Subline ?? string.Empty;
        }

        private void RefreshName()
        {
            string name = _userData.user?.displayName ?? string.Empty;
            if (name == _shownName) return;
            _shownName = name;
            KitEllipsis.SetText(Root.Q<Label>("player-name"), name);
            KitEllipsis.SetText(Root.Q<Label>("lower-player"), name);
        }

        private void RefreshGameChip()
        {
            var games = _matchmaking.Games;
            int g = _matchmaking.SelectedGameIndex.CurrentValue;
            var maps = _matchmaking.CurrentMaps;
            int m = _matchmaking.SelectedMapIndex.CurrentValue;
            string game = g >= 0 && g < games.Count ? games[g].Name : string.Empty;
            string map = m >= 0 && m < maps.Count ? maps[m].Name : string.Empty;
            string text = string.IsNullOrEmpty(map) ? $"🎯 {game} ▾" : $"🎯 {game} · {map} ▾";
            KitEllipsis.SetText(Root.Q<Label>("game-chip-text"), text);
        }

        private void AdvanceTicker()
        {
            string text;
            if (_matchmaking.SelectedQueue.CurrentValue == QueueKind.Ranked)
            {
                text = RankedTicker;
            }
            else
            {
                var games = _matchmaking.Games;
                int g = _matchmaking.SelectedGameIndex.CurrentValue;
                int i = _tickerIndex++ % (CasualTicker.Length + 1);
                text = i < CasualTicker.Length
                    ? CasualTicker[i]
                    : $"오늘의 종목 · {(g >= 0 && g < games.Count ? games[g].Name : string.Empty)}";
            }
            KitEllipsis.SetText(Root.Q<Label>("ticker-text"), text);
        }

        public override void OnClose()
        {
            _subscriptions.Clear();
            _breather?.Dispose();
            _breather = null;
            _tickerItem?.Pause();
            _tickerItem = null;
            _nameItem?.Pause();
            _nameItem = null;
            _liveItem?.Pause();
            _liveItem = null;
            base.OnClose();
        }

        public static string PlacementChipText(int placement) => $"{placement}위";

        private void OnRecentPlacements(System.Collections.Generic.IReadOnlyList<int> placements)
        {
            var chips = Root.Q("lower-chips");
            chips.Clear();
            foreach (int p in placements)
            {
                var chip = new Label(PlacementChipText(p));
                chip.AddToClassList("lobbyhome-lower__chip");
                if (p == 1) chip.AddToClassList("lobbyhome-lower__chip--first");
                chips.Add(chip);
            }
            //  랭크 판이 하나도 없으면 "최근 5경기" 줄을 숨긴다 — 빈 칸으로 두지 않는다.
            Root.Q("lower-form").style.display = placements.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BlinkLive()
        {
            _liveOn = !_liveOn;
            Root.Q("broadcast-live").style.visibility = _liveOn ? Visibility.Visible : Visibility.Hidden;
        }

        private void OnPlayClicked() => _matchmaking.Play();
    }
}
