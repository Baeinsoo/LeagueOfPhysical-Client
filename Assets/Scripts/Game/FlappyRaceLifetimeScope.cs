using LOP.UI;
using MessagePipe;
using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Flappy Race 덩어리 — 새 월드와 새 생성기를 쓴다.</summary>
    public class FlappyRaceLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private CameraController cameraController;

        //  소리 파일 칸(spec §6). 비어 있으면 코드로 지은 임시 클립을 쓴다 — 씬에서 비워 둔 채로 둔다.
        [SerializeField] private FlappySoundBank soundBank;

        //  새 몸엔 체력이 없다 — HP 막대를 켜 두면 늘 가득 찬 막대가 머리 위에 뜬다.
        protected override bool ShowHealthBars => false;

        //  상대 이름은 기본 숨김(spec §2, 폴가이즈 문법) — 이름 여섯 줄이 좁은 통로를 덮는다. 설정 "상대 이름 보기"를
        //  켜면 이름표만 다시 켠다(HP 막대는 위에서 계속 꺼져 있다). 내 이름표도 같이 숨는다 — 내 새는 테두리·화살표가 찾아 준다.
        protected override bool ShowNameplates => FlappyDisplayPrefs.ShowOpponentNames;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);
            builder.Register<FlappyConfigProvider>(Lifetime.Singleton);
            builder.Register<FlappyConfig>(c => c.Resolve<FlappyConfigProvider>().Get(), Lifetime.Singleton);
            // 이 게임엔 외삽 대상이 없다(OwnerPredictedSyncPolicy는 예측 아니면 보간만 준다)
            // — 그래도 EntityBinder의 생성자 의존이라 등록은 필요하다. 값은 쓰이지 않는다.
            builder.Register<IExtrapolationAcceleration>(
                c => new FlappyExtrapolationAcceleration(c.Resolve<FlappyConfig>()), Lifetime.Singleton);

            builder.Register<FlappyMoveSystem>(Lifetime.Singleton);
            builder.Register<FlappyStunSystem>(Lifetime.Singleton);
            builder.Register<FlappyDashSystem>(Lifetime.Singleton);
            //  새는 +x로 달린다. 폴백을 주지 않는다 — 마커가 없으면 룰이 Initialize에서 터뜨린다.
            builder.Register(c => new FinishLineBounds(FinishAxis.X), Lifetime.Singleton);
            builder.Register(c => new FinishSystem(
                c.Resolve<FinishLineBounds>(), FinishAxis.X, increasing: true), Lifetime.Singleton);
            // 맵 씬의 FlappyWindmill 마커가 맵 로드 시 여기에 자기를 넣는다. 등록이 없으면 마커의
            // [Inject]가 그 자리에서 씬 주입을 끊는다(WindField/LaserField와 같은 이유).
            builder.Register<FlappyWindmillField>(Lifetime.Singleton);
            // 진자·셔터 마커도 맵 로드 때 여기 들어온다. 양쪽 다 등록해야 한다 — 한쪽만 돌리면 곧장 갈린다.
            builder.Register<FlappyPendulumField>(Lifetime.Singleton);
            builder.Register<FlappyShutterField>(Lifetime.Singleton);
            // 승강기·낙석 마커도 맵 로드 때 여기 들어온다(진자·셔터와 같은 이유) — 클·서 양쪽 다 등록해야 한다.
            builder.Register<FlappyLiftField>(Lifetime.Singleton);
            builder.Register<FlappyRockfallField>(Lifetime.Singleton);
            // 부스트 패드도 같은 이유로 등록이 필요하다. 이건 특히 양쪽 다 해야 한다 — 한쪽만
            // 등록하면 그쪽만 패드를 밟아 전진 속도가 갈린다.
            builder.Register<FlappyBoostPadField>(Lifetime.Singleton);
            // 빛 알갱이 무더기도 같은 이유로 양쪽 다 등록해야 한다 — 한쪽만 알면 게이지가 갈린다.
            builder.Register<FlappyMoteField>(Lifetime.Singleton);
            // 맵이 추격자·수동 대시를 켜는지. 맵 씬의 FlappyMapRules 마커가 맵 로드 시 여기에 값을
            // 넣는다. FlappyDashSystem·FlappyChaserView·FlapPadViewModel이 이걸 주입받으므로
            // 등록이 없으면 씬 주입이 끊긴다 — 부스트 패드와 같은 이유로 양쪽(클·서) 다 등록해야 한다.
            builder.Register<FlappyMapRulesField>(Lifetime.Singleton);
            // 기류 마커도 맵 로드 때 여기 들어온다. 양쪽 다 등록해야 한다 — 한쪽만 기류를 알면 세로 속도가 갈린다.
            builder.Register<FlappyAirflowField>(Lifetime.Singleton);
            // sweep이 볼 것은 맵 지오메트리뿐이다 — 새끼리는 아예 부딪히지 않는다(서로 통과한다).
            // 새의 물리 몸은 PhysicsBodyFactory가 만들면서 무조건 Character 레이어에 둔다. 그래서 이
            // 마스크에 Character가 없는 한 새끼리는 sweep에 걸리지 않는다.
            // (겉모습 프리팹 Bird.prefab에는 콜라이더가 없어 물리에는 아예 존재하지 않는다.)
            // FlappyWorld를 구체로도 해석할 수 있어야 보정 핸들러가 자기 게임 월드를 직접 본다.
            builder.Register<FlappyWorld>(c => new FlappyWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<FlappyMoveSystem>(),
                c.Resolve<FlappyStunSystem>(),
                c.Resolve<FlappyDashSystem>(),
                c.Resolve<FinishSystem>(),
                c.Resolve<FlappyWindmillField>(),
                c.Resolve<FlappyBoostPadField>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
                c.Resolve<GameFramework.World.IMotionBridge>(),
                LayerMask.GetMask("Default", FlappyHologram.LayerName), c.Resolve<FlappyPendulumField>(), c.Resolve<FlappyShutterField>(),
                c.Resolve<FlappyLiftField>(), c.Resolve<FlappyRockfallField>(), c.Resolve<FlappyMoteField>()), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();
            builder.Register<ICharacterCreator, FlappyBirdCreator>(Lifetime.Singleton);
            //  스턴은 서버 권위다. 내 새는 클라가 굴리므로 "내가 맵에 부딪혔나"를 예측하게 되는데,
            //  그 판정이 서버와 갈리면 1.2초 얼음이 통째로 어긋난다.
            builder.Register<IServerCorrectionHandler, FlappyServerCorrectionHandler>(Lifetime.Singleton);

            //  내 새만 예측하고, 남의 새는 서버 시간대의 스냅샷 보간으로 그린다.
            //
            //  남을 예측하려면 그 입력이 필요한데, 클라는 서버보다 앞서 달리므로 지금 그릴 구간의
            //  입력은 상대가 아직 보내지도 않았다. 서버가 남의 입력을 되뿌리게 해 봤지만
            //  (EntityInputsToC) 편도 75ms에서 100개 중 0개가 제때 왔다 — 지나간 구간만 정확해지고
            //  화면에 그리는 구간은 그대로였다. 남는 오차 = (속도폭 53) × (앞선 시간 0.105초) ≈ 5.6m이고
            //  상대가 날갯짓할 때마다 그만큼 튄다. 앞서 달리는 한 없앨 수 없는 값이다.
            //
            //  보간으로 그리면 그 튐이 0이 된다. 대가는 남의 새가 약 0.23초(내가 앞선 시간 + 편도 +
            //  보간 쿠션)만큼 뒤에 그려지는 것 — 11m/s로 2.5m다. 전진 속도가 모두 같아 새마다
            //  똑같이 걸리는 상수라 새들끼리의 순서는 정확하고, 레이스 중엔 보이지 않는다.
            //  결승선에서만 드러나므로 등수는 화면이 아니라 통과 틱으로 판정한다.
            //  (이 선택의 옛 대가였던 "몸싸움이 어긋난다"는 새끼리 충돌을 없애면서 사라졌다.)
            //
            //  전부 보간(AllInterpolatedSyncPolicy)도 실측했다 — 화면은 일관된 한 장이 되지만
            //  입력 지연이 RTT+쿠션이라 편도 50ms에서도 조작이 불가능했다. 탭 타이밍이 전부인
            //  게임이라 내 새의 예측은 포기할 수 없다.
            builder.Register<IEntitySyncPolicy>(
                c => new OwnerPredictedSyncPolicy(() => c.Resolve<IGameDataStore>().userEntityId),
                Lifetime.Singleton);

#if UNITY_EDITOR
            //  손 떼고 남의 새를 관찰하기 위한 자동 비행. 토글은 LOP ▸ Debug ▸ Auto Flap.
            //  두 클라를 동시에 조종하면서는 관찰도, 설정을 바꿔 가며 비교하는 것도 불가능하다.
            builder.RegisterEntryPoint<FlappyAutoFlapSystem>();

            //  판정 모양을 게임 화면에 그린다. 토글은 LOP ▸ Debug ▸ Hitbox 보기.
            //  겉모습과 판정이 어긋나면 눈으로는 "닿은 것 같은데"까지만 알 수 있다.
            builder.RegisterEntryPoint<FlappyHitboxDebugView>();
#endif

            //  카메라와 벽이 같은 답을 봐야 한다 — 주인이 하나여야 하므로 싱글턴.
            builder.Register<FlappySpectate>(Lifetime.Singleton).As<ISpectateTargets>().AsSelf();

            //  AsSelf로도 등록한다 — FlapPad가 "추격자까지 몇 m"를 그리려면 벽 위치를 읽어야 하고,
            //  같은 값을 읽어야 숫자와 그림이 어긋나지 않는다.
            builder.RegisterEntryPoint<FlappyChaserView>().AsSelf();

            //  안개·하늘은 씬이 아니라 여기가 소유한다 — 맵이 additive라 씬의 RenderSettings는 무시된다.
            builder.RegisterEntryPoint<FlappyAtmosphere>();

            //  홀로그램 관문 흔들림·조각 연출 — 판정과 무관한 클라 전용 화면 효과라 여기서만 등록한다.
            builder.RegisterEntryPoint<FlappyHologramFx>();

            //  빌딩 앞벽 반투명 — 판정과 무관한 클라 전용 화면 효과.
            builder.RegisterEntryPoint<FlappyBuildingFacadeFx>();

            //  경기 소리(spec §6) — 효과음·배경음 한 곳. 진입점이라 IFlappyAudio로도 풀린다(AsImplementedInterfaces).
            builder.RegisterInstance(new FlappySoundBankSlot(soundBank));
            builder.RegisterEntryPoint<FlappyAudio>();

            //  카메라 흔들림 주인 하나(대시 몫 + 충돌 트라우마, "흔들림 끄기"). 대시·충돌 연출이 주입받으므로 AsSelf.
            builder.RegisterEntryPoint<FlappyCameraShake>().AsSelf();

            //  충돌 묶음(spec §3) — 트라우마·파편·쿵.
            builder.RegisterEntryPoint<FlappyCrashFx>();

            //  새마다 그림·기절 읽기 한 곳 — 충돌 묶음과 기절 별이 같은 답을 봐야 한다.
            builder.Register<IFlappyBirdLooks, FlappyBirdLooks>(Lifetime.Singleton);

            //  파편·꽃가루 조각 — 충돌 묶음과 결승 연출이 주입받으므로 AsSelf.
            builder.RegisterEntryPoint<FlappyBurstFx>().AsSelf();

            //  대시 흔들림·속도선·꼬리 — 판정과 무관한 클라 전용 연출.
            builder.RegisterEntryPoint<FlappyDashFx>();
            //  빛 알갱이 그림·빨려 드는 복사본·"+n알"(판정과 무관한 화면 연출). 글자는 우편함을 거쳐 HUD가 띄운다.
            builder.Register<FlappyMoteFeed>(Lifetime.Singleton);
            builder.RegisterEntryPoint<FlappyMoteFx>();
            //  다이브 충전 연출(기운·빛줄기·고리·충전음·"대시 1칸!") — 판정과 무관한 화면 연출.
            builder.RegisterEntryPoint<FlappyDiveFx>();
            //  낙석 예고(떨어지기 직전 균열에서 먼지·돌가루) — 판정과 무관한 화면 연출(10-24).
            builder.RegisterEntryPoint<FlappyRockfallWarnFx>();

            //  내 새 흰 테두리·노란 화살표(spec §2) — 판정과 무관한 클라 전용 연출.
            builder.RegisterEntryPoint<FlappyMyBirdMarker>();

            //  기절 별 셋(spec §3) — 내 새·남의 새 모두. 판정과 무관한 화면 연출.
            builder.RegisterEntryPoint<FlappyStunStars>();

            //  판의 큰 순간 — 배경음 시작·정지, 꽃가루(결승음·도장은 RaceFinishView).
            builder.RegisterEntryPoint<FlappyRaceCues>();

            //  낙하 내려다보기(FlappyFallLookDown)는 껐다 — 카메라가 새를 뒤따라 늦게 움직이는 느낌이라
            //  원조처럼 고정된 구도가 낫다(사용자 결정 2026-10-07). 클래스와 시험은 남겨 둔다.

            //  카메라 거리는 23m 고정, 내 대시 중에만 3m 빠진다(사용자 결정 2026-10-07, 20 → 23m는 10-08).
            builder.RegisterEntryPoint<FlappyCameraDistance>();

            //  카메라 중심을 통로에 고정 — 원조처럼 플레이필드 높이가 일정하다(맵에 FlappyCorridorLine
            //  표시가 없으면 아무것도 안 한다. 전통 코스는 표시가 없다 — 전통 굽기가 광산 표시를 뗀다).
            //  계산은 CameraController.BeforeFollow에서 한다(그려진 몸 위치 기준).
            builder.RegisterEntryPoint<FlappyCorridorCamera>();

            //  개인 기록(spec §7) — 완주 시간을 게임 공용 기록지에 적는다(서버 등수가 와야). 판정과 무관하다.
            builder.RegisterEntryPoint<FlappyFinishRecorder>();
            //  연속 날갯짓(spec §7) — 먹힌 날갯짓을 세고 최고를 기록지에 적는다. 머리 위 "+N"이 읽으므로 AsSelf.
            builder.RegisterEntryPoint<FlappyFlapStreak>().AsSelf();

            builder.RegisterEntryPoint<FlappyHudCoordinator>();
            //  날갯짓 소리 — 입력면이 누른 순간 부른다(사용자 결정 10-25).
            builder.Register<FlappyFlapSound>(Lifetime.Singleton);
            builder.Register<FlapPadViewModel>(Lifetime.Transient);
            builder.Register<FlapPadView>(Lifetime.Transient);
            //  정보 HUD(진행 막대·순위·기록 시간). 소스는 출발 x를 한 번 정해 판 내내 쥐므로 싱글턴.
            //  상대 점 색은 이제 FlappyRacePalette.OpponentNeutral 하나뿐이라 색 칸을 둘 이유가 없다(사용자 결정 10-25).
            builder.Register<IRaceHudSource, RaceHudSource>(Lifetime.Singleton);
            builder.Register<RaceHudViewModel>(Lifetime.Transient);
            builder.Register<RaceHudView>(Lifetime.Transient);
            //  카운트다운 모양·박자 — 창은 기마 궁술과 같이 쓰므로 개편(팝·GO 초록·소리)은 플래피 스코프가 꽂는다.
            builder.Register<IRaceStartLook, FlappyRaceStartLook>(Lifetime.Singleton);
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
        }

        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<FlapPadView>(() => container.Resolve<FlapPadView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceHudView>(() => container.Resolve<RaceHudView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceStartView>(() => container.Resolve<RaceStartView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceEliminatedView>(() => container.Resolve<RaceEliminatedView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceFinishView>(() => container.Resolve<RaceFinishView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceSpectateView>(() => container.Resolve<RaceSpectateView>()));
            sink.Add(windowManager.RegisterViewFactory<LeaveMatchConfirmView>(() => container.Resolve<LeaveMatchConfirmView>()));
        }
    }
}
