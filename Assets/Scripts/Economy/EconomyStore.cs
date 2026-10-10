using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace LOP
{
    /// <summary>
    /// 로비가 보는 "내 재화" 단일 진실원본. 조회(<see cref="RefreshAsync"/>)와 구매·로드아웃 변경
    /// 응답 반영을 한곳에 모아, 코인·레벨 칩 같은 여러 화면이 같은 값을 보게 한다.
    /// 조회가 실패하면 이전 값을 그대로 둔다 — 로비가 0코인으로 깜빡이는 것보다 낫다.
    /// </summary>
    public class EconomyStore : IDisposable
    {
        private readonly IUserDataStore _users;
        private readonly CosmeticCatalog _catalog;
        private readonly Func<string, CancellationToken, UniTask<GetEconomyResponse>> _fetch;

        private readonly ReactiveProperty<long> _coins = new(0);
        private readonly ReactiveProperty<int> _level = new(1);
        private readonly ReactiveProperty<ProgressDto> _progress = new(null);
        private readonly ReactiveProperty<IReadOnlyList<LoadoutSlotDto>> _loadout = new(Array.Empty<LoadoutSlotDto>());
        private readonly ReactiveProperty<IReadOnlyList<OwnedCosmeticDto>> _owned = new(Array.Empty<OwnedCosmeticDto>());

        /// <summary>코인 잔액(마스터데이터 코드 "COIN" 통화). 받기 전엔 0.</summary>
        public ReadOnlyReactiveProperty<long> Coins => _coins;

        /// <summary>내 레벨. 받기 전엔 1.</summary>
        public ReadOnlyReactiveProperty<int> Level => _level;

        /// <summary>레벨·경험치 전체 진행도. 받기 전엔 null.</summary>
        public ReadOnlyReactiveProperty<ProgressDto> Progress => _progress;

        /// <summary>슬롯별 장착 현황. 받기 전엔 빈 목록.</summary>
        public ReadOnlyReactiveProperty<IReadOnlyList<LoadoutSlotDto>> Loadout => _loadout;

        /// <summary>보유한 꾸밈 인스턴스 전체. 받기 전엔 빈 목록.</summary>
        public ReadOnlyReactiveProperty<IReadOnlyList<OwnedCosmeticDto>> Owned => _owned;

        [VContainer.Inject]
        public EconomyStore(IUserDataStore users, CosmeticCatalog catalog)
            : this(users, catalog, WebAPI.GetEconomy)
        {
        }

        /// <param name="fetch">(userId, ct) → 내 이코노미 조회. 시험이 가짜를 꽂는다.</param>
        public EconomyStore(IUserDataStore users, CosmeticCatalog catalog,
            Func<string, CancellationToken, UniTask<GetEconomyResponse>> fetch)
        {
            _users = users;
            _catalog = catalog;
            _fetch = fetch ?? WebAPI.GetEconomy;
        }

        /// <summary>서버에서 재화·진행도·로드아웃·보유목록을 통째로 받아온다. 실패(네트워크·비정상
        /// 코드)하면 이전 값을 그대로 두고 경고만 남긴다.</summary>
        public async UniTask RefreshAsync(CancellationToken cancellationToken)
        {
            string userId = _users.user?.id;
            if (string.IsNullOrEmpty(userId)) return;

            try
            {
                var response = await _fetch(userId, cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;

                if (response == null || response.code != ResponseCode.SUCCESS)
                {
                    UnityEngine.Debug.LogWarning($"[EconomyStore] Failed to refresh economy. code: {response?.code}");
                    return;
                }

                Apply(response);
            }
            catch (OperationCanceledException)
            {
                //  화면이 먼저 닫혔다 — 쓸 곳이 없다.
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[EconomyStore] Failed to refresh economy. error: {e.Message}");
            }
        }

        /// <summary>구매 응답 반영. 거절 응답은 code만 오고 나머지가 null이라 자연히 아무것도 안 바뀐다.</summary>
        public void ApplyPurchase(PurchaseCosmeticResponse response)
        {
            if (response == null || response.code != ResponseCode.SUCCESS) return;

            ApplyWallets(response.wallets);
            if (response.loadout != null) _loadout.Value = response.loadout;
            if (response.owned != null) AddOwned(response.owned);
        }

        /// <summary>로드아웃 변경 응답 반영. 이 응답은 로드아웃만 실어 온다(지갑·보유목록은 안 바뀐다).</summary>
        public void ApplyLoadout(SetLoadoutResponse response)
        {
            if (response == null || response.code != ResponseCode.SUCCESS) return;
            if (response.loadout != null) _loadout.Value = response.loadout;
        }

        private void Apply(GetEconomyResponse response)
        {
            ApplyWallets(response.wallets);

            if (response.progress != null)
            {
                _progress.Value = response.progress;
                _level.Value = response.progress.level;
            }

            if (response.loadout != null) _loadout.Value = response.loadout;
            if (response.owned != null) _owned.Value = response.owned;
        }

        private void ApplyWallets(WalletDto[] wallets)
        {
            if (wallets == null) return;

            foreach (var wallet in wallets)
            {
                if (wallet.currencyId == _catalog.CoinCurrencyId)
                {
                    _coins.Value = wallet.balance;
                    return;
                }
            }
        }

        //  구매 응답은 새로 생긴(또는 재구매로 이미 가진) 인스턴스 하나만 실어 온다 — 전체 목록이 아니다.
        //  이미 보유 목록에 같은 id가 있으면 중복으로 더하지 않는다.
        private void AddOwned(OwnedCosmeticDto newItem)
        {
            var current = _owned.Value;
            foreach (var owned in current)
            {
                if (owned.id == newItem.id) return;
            }

            var updated = new List<OwnedCosmeticDto>(current) { newItem };
            _owned.Value = updated;
        }

        public void Dispose()
        {
            _coins.Dispose();
            _level.Dispose();
            _progress.Dispose();
            _loadout.Dispose();
            _owned.Dispose();
        }
    }
}
