using System;
using System.Collections.Generic;
using LOP.UI;
using Mirror;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>카드 러시 덩어리(클라) — 물리 없는 상태 월드. 내 몸만 예측하고 남은 보간한다.</summary>
    public class CardRushLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private CameraController cameraController;

        //  체력이 의미 없는 모드다 — 늘 가득 찬 막대가 머리 위에 뜨지 않게 끈다.
        protected override bool ShowHealthBars => false;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);

            builder.Register<CardRushConfigProvider>(Lifetime.Singleton);
            builder.Register<CardRushConfig>(c => c.Resolve<CardRushConfigProvider>().Get(), Lifetime.Singleton);

            //  맵 씬의 CardRushMapRules 마커가 맵 로드 때 여기에 값을 넣는다. 등록이 없으면 마커의 [Inject]가
            //  씬 주입을 끊는다 — 클·서 양쪽 다 등록해야 한다(서버 CardRushLifetimeScope와 같은 줄).
            builder.Register<CardRushMapRulesField>(Lifetime.Singleton);

            //  서버와 같은 팩토리. 클라 씨앗은 GameInfo로 늦게 오지만 월드가 출발 전에 코스를 다시 만든다.
            //  자리는 월드 Transform에 적고, 그림·PhysX 대리 몸은 그 값을 그대로 읽는다 — 월드에 IMotionBridge가 필요 없다.
            builder.Register<CardRushWorld>(c => new CardRushWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<CardRushMapRulesField>(),
                c.Resolve<IMatchSeed>(),
                c.Resolve<CardRushConfig>().Timing), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, CardRushRunnerCreator>(Lifetime.Singleton);

            //  내 몸만 예측한다(탭 타이밍이 전부라 내 입력은 바로 보여야 한다). 남은 스냅 보간으로 충분하다.
            builder.Register<IEntitySyncPolicy>(
                c => new OwnerPredictedSyncPolicy(() => c.Resolve<IGameDataStore>().userEntityId),
                Lifetime.Singleton);

            //  위치 말고도 칸·멈춤 등 다섯 칸을 서버 판정에 맞춘다.
            builder.Register<IServerCorrectionHandler>(
                c => new CardRushServerCorrectionHandler(c.Resolve<CardRushWorld>()), Lifetime.Singleton);
            //  외삽 대상이 없다(예측 아니면 보간) — EntityBinder 생성자 의존이라 등록만 한다.
            builder.Register<IExtrapolationAcceleration, ZeroExtrapolationAcceleration>(Lifetime.Singleton);

            //  카메라·관전이 같은 답을 봐야 한다 — 주인이 하나여야 하므로 싱글턴(FlappySpectate와 같은 이유).
            builder.Register<CardRushSpectate>(Lifetime.Singleton).As<ISpectateTargets>().AsSelf();

            //  소리 한 곳 — 내 몸 상태 변화의 소리 + RaceHud·RaceFinish가 부르는 IFlappyAudio 자리.
            //  진입점이라 IFlappyAudio로도 풀린다(AsImplementedInterfaces, FlappyAudio와 같은 등록).
            builder.RegisterEntryPoint<CardRushAudio>().AsSelf();

            //  카운트다운은 개편(팝·GO 초록) 없이 예전 모양 그대로(기마 궁술·넉백과 같은 선택) — 소리 창구가
            //  없어도 되는 쪽이라 PlainRaceStartLook을 쓴다.
            builder.Register<IRaceStartLook, PlainRaceStartLook>(Lifetime.Singleton);
            builder.Register<RaceStartViewModel>(Lifetime.Transient);
            builder.Register<RaceStartView>(Lifetime.Transient);
            builder.Register<RaceEliminatedView>(Lifetime.Transient);
            builder.Register<RaceFinishViewModel>(Lifetime.Transient);
            builder.Register<RaceFinishView>(Lifetime.Transient);
            builder.Register<RaceSpectateViewModel>(Lifetime.Transient);
            builder.Register<RaceSpectateView>(Lifetime.Transient);
            builder.Register<LeaveMatchConfirmViewModel>(Lifetime.Transient);
            builder.Register<LeaveMatchConfirmView>(Lifetime.Transient);
            builder.Register(c => new MatchLeaveRequester(c.Resolve<ISubscriber<LeaveMatchToC>>(),
                message => NetworkClient.Send(new CustomMirrorMessage { payload = message })), Lifetime.Singleton);

            //  조작판 — 화면 아무 데나 탭하면 잡기. 카드 자체는 매 프레임 PlayerInputManager.NextInputTick으로 그린다(spec §7.3).
            builder.Register<CardRushPadViewModel>(Lifetime.Transient);
            builder.Register<CardRushPadView>(Lifetime.Transient);

            //  정보 HUD(진행 막대·순위·기록 시간) — RaceHudViewModel/View는 Flappy와 그대로, 소스만 갈아 끼운다.
            builder.Register<IRaceHudSource, CardRushRaceHudSource>(Lifetime.Singleton);
            builder.Register<RaceHudViewModel>(Lifetime.Transient);
            builder.Register<RaceHudView>(Lifetime.Transient);

            builder.RegisterEntryPoint<CardRushHudCoordinator>().AsSelf();

            //  돌·문은 씬에 없다 — 몸이 생기면 그 레인에 (칸 수, 코스 색)으로 깐다. 두 테마가 같은 식이다.
            builder.RegisterEntryPoint<CardRushCourseView>().AsSelf();
            //  뒤 위에서 진행 방향을 보는 고정 각 카메라. 관전 대상은 위 코디네이터가 매 틱 고른다.
            builder.RegisterEntryPoint<CardRushCamera>().AsSelf();
            //  몸개그(풍덩·박치기·폴짝·결승 몸짓) — 몸의 자식 Model만 움직이는 그림. 상태는 읽기만 한다.
            builder.RegisterEntryPoint<CardRushRunnerFxView>().AsSelf();
        }

        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<CardRushPadView>(() => container.Resolve<CardRushPadView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceHudView>(() => container.Resolve<RaceHudView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceStartView>(() => container.Resolve<RaceStartView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceEliminatedView>(() => container.Resolve<RaceEliminatedView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceFinishView>(() => container.Resolve<RaceFinishView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceSpectateView>(() => container.Resolve<RaceSpectateView>()));
            sink.Add(windowManager.RegisterViewFactory<LeaveMatchConfirmView>(() => container.Resolve<LeaveMatchConfirmView>()));
        }
    }
}
