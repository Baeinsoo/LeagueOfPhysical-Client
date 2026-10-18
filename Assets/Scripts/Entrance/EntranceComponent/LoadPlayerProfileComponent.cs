using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 플레이어 프로필(재화·레벨·장착·보유, 캐릭터 외형)을 받아 앱 범위 스토어에 채운다(ADR 0021).
    /// 하나라도 못 받으면 부팅을 멈춘다 — 로비가 0코인·기본 모습으로 열리지 않게.
    /// 코인 계산에 마스터데이터 통화표가 필요해 마스터데이터 단계 뒤에 둔다.
    /// </summary>
    public class LoadPlayerProfileComponent : IEntranceComponent
    {
        public string StepLabel => "선수 장비 불러오는 중";

        private readonly Func<EconomyStore> economy;
        private readonly Func<CharacterAppearanceStore> appearance;

        //  입장 씬은 단계를 실행하기 전에 전부 한꺼번에 만든다. 스토어(코인 표)는 마스터데이터를 읽으며
        //  만들어지므로, 그 단계가 끝난 뒤 실행할 때 꺼낸다.
        [Inject]
        public LoadPlayerProfileComponent(IObjectResolver resolver)
            : this(() => resolver.Resolve<EconomyStore>(), () => resolver.Resolve<CharacterAppearanceStore>())
        {
        }

        public LoadPlayerProfileComponent(Func<EconomyStore> economy, Func<CharacterAppearanceStore> appearance)
        {
            this.economy = economy;
            this.appearance = appearance;
        }

        public async Task Execute()
        {
            var (economyOk, appearanceOk) = await UniTask.WhenAll(
                economy().RefreshAsync(CancellationToken.None),
                appearance().RefreshAsync(CancellationToken.None));

            if (!economyOk) throw new Exception("재화·장착 정보를 가져오는데 실패했습니다.");
            if (!appearanceOk) throw new Exception("캐릭터 외형을 가져오는데 실패했습니다.");
        }
    }
}
