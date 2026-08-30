using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Skydive 덩어리(클라) — 떨어지는 월드, 캐릭터 전원 예측.</summary>
    public class SkydiveLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private CameraController cameraController;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);

            builder.Register<SkydiveMoveSystem>(Lifetime.Singleton);
            builder.Register<SkydiveWorld>(c => new SkydiveWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<SkydiveMoveSystem>()), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, SkydivePlayerCreator>(Lifetime.Singleton);

            // 플레이어끼리 부딪히기로 했으므로 남도 예측한다(스펙 §4.1). 충돌 자체는 슬라이스 6이
            // 켜지만, 정책을 지금 맞춰 두면 그때 이 줄을 고칠 일이 없다.
            builder.Register<IEntitySyncPolicy, CharactersPredictedSyncPolicy>(Lifetime.Singleton);
            builder.Register<IServerCorrectionHandler, NoServerCorrection>(Lifetime.Singleton);

            // 이 게임엔 외삽 대상이 없다(정책이 Extrapolated를 절대 안 준다) — 그래도 EntityBinder의
            // 생성자 의존이라 등록은 필요하다. 값은 쓰이지 않는다.
            builder.Register<IExtrapolationAcceleration, ZeroExtrapolationAcceleration>(Lifetime.Singleton);
        }
    }
}
