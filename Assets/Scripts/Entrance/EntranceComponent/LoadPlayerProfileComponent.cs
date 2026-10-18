using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using LOP.UI;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 플레이어 프로필(재화·레벨·장착·보유, 캐릭터 외형)을 받아 앱 범위 스토어에 채운다(ADR 0021).
    /// 하나라도 못 받으면 "연결할 수 없습니다" 팝업의 [다시 시도]로 받을 때까지 다음 단계로 넘어가지 않는다.
    /// 코인 계산에 마스터데이터 통화표가 필요해 마스터데이터 단계 뒤에 둔다.
    /// </summary>
    public class LoadPlayerProfileComponent : IEntranceComponent
    {
        public string StepLabel => "선수 장비 불러오는 중";

        private readonly Func<EconomyStore> economy;
        private readonly Func<CharacterAppearanceStore> appearance;
        private readonly Func<UniTask> waitForRetry;

        //  입장 씬은 단계를 실행하기 전에 전부 한꺼번에 만든다. 스토어(코인 표)는 마스터데이터를 읽으며
        //  만들어지므로, 그 단계가 끝난 뒤 실행할 때 꺼낸다.
        [Inject]
        public LoadPlayerProfileComponent(IObjectResolver resolver)
            : this(() => resolver.Resolve<EconomyStore>(), () => resolver.Resolve<CharacterAppearanceStore>(),
                   () => resolver.Resolve<IWindowManager>().OpenModalAsync<ServerUnreachableView, bool>())
        {
        }

        /// <param name="waitForRetry">못 받았을 때 사용자가 [다시 시도]를 누를 때까지 기다린다.</param>
        public LoadPlayerProfileComponent(Func<EconomyStore> economy, Func<CharacterAppearanceStore> appearance, Func<UniTask> waitForRetry)
        {
            this.economy = economy;
            this.appearance = appearance;
            this.waitForRetry = waitForRetry;
        }

        public async Task Execute()
        {
            bool economyOk = false, appearanceOk = false;
            while (true)
            {
                (economyOk, appearanceOk) = await UniTask.WhenAll(
                    economyOk ? UniTask.FromResult(true) : economy().RefreshAsync(CancellationToken.None),
                    appearanceOk ? UniTask.FromResult(true) : appearance().RefreshAsync(CancellationToken.None));
                if (economyOk && appearanceOk) return;

                //  0코인·기본 모습으로 로비에 들이지 않는다 — [다시 시도]를 누르면 못 받은 쪽만 다시 받는다.
                await waitForRetry();
            }
        }
    }
}
