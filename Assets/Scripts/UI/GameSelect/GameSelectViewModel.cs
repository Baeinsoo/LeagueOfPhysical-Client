using System;
using System.Collections.Generic;
using R3;

namespace LOP.UI
{
    /// <summary>
    /// 종목 선택 화면의 고르는 중인 값. 확인을 눌러야만 apply로 로비(매칭 VM)에 넘긴다 —
    /// 뒤로 나가면 로비 선택도, 마지막 선택 기억도 그대로다.
    /// </summary>
    public class GameSelectViewModel : IDisposable
    {
        private readonly Action<int, int> _apply;
        private readonly ReactiveProperty<int> _gameIndex;
        private readonly ReactiveProperty<int> _mapIndex;

        public IReadOnlyList<GameChoice> Games { get; }
        public ReadOnlyReactiveProperty<int> GameIndex => _gameIndex;
        public ReadOnlyReactiveProperty<int> MapIndex => _mapIndex;
        public IReadOnlyList<MapChoice> Maps =>
            Games.Count == 0 ? Array.Empty<MapChoice>() : Games[_gameIndex.Value].Maps ?? Array.Empty<MapChoice>();

        public GameSelectViewModel(IReadOnlyList<GameChoice> games, int gameIndex, int mapIndex, Action<int, int> apply)
        {
            Games = games;
            _apply = apply;
            int g = gameIndex >= 0 && gameIndex < games.Count ? gameIndex : 0;
            _gameIndex = new ReactiveProperty<int>(g);
            var maps = games.Count == 0 ? null : games[g].Maps;
            _mapIndex = new ReactiveProperty<int>(maps != null && mapIndex >= 0 && mapIndex < maps.Count ? mapIndex : 0);
        }

        public void SelectGame(int index)
        {
            if (index < 0 || index >= Games.Count) return;
            _mapIndex.Value = 0;
            _gameIndex.Value = index;
        }

        public void SelectMap(int index)
        {
            if (index < 0 || index >= Maps.Count) return;
            _mapIndex.Value = index;
        }

        public void Confirm() => _apply(_gameIndex.Value, _mapIndex.Value);

        public void Dispose()
        {
            _gameIndex.Dispose();
            _mapIndex.Dispose();
        }
    }
}
