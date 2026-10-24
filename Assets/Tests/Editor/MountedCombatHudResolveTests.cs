using GameFramework;
using System;
using MessagePipe;
using NUnit.Framework;
using VContainer;
using LOP.UI;

namespace LOP.Tests
{
    /// <summary>컨테이너가 전투 HUD를 실제로 만들 수 있는지 — 선택 인자가 끼면 해석이 터져 HUD가 안 열린다.</summary>
    public class MountedCombatHudResolveTests
    {
        private sealed class FakePlayerContext : IPlayerContext
        {
            public ISession session { get; set; }
            public string entityId { get; set; }
            public LOPActor actor { get; set; }
        }

        private sealed class FakeSubscriber : ISubscriber<WorldEventBatchToC>
        {
            public IDisposable Subscribe(IMessageHandler<WorldEventBatchToC> handler, params MessageHandlerFilter<WorldEventBatchToC>[] filters)
                => new Empty();

            private sealed class Empty : IDisposable { public void Dispose() { } }
        }

        [Test]
        public void 컨테이너가_전투_HUD_뷰모델을_만든다()
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(new MountedPredictedHits(null, null, null, null, null, null));
            builder.RegisterInstance(new MountedArcheryClientState());
            builder.RegisterInstance<IPlayerContext>(new FakePlayerContext());
            builder.RegisterInstance<ISubscriber<WorldEventBatchToC>>(new FakeSubscriber());
            builder.RegisterInstance(new GameFramework.World.EntityRegistry());
            builder.Register<MountedCombatHudViewModel>(Lifetime.Transient);
            builder.Register<MountedCombatHudView>(Lifetime.Transient);

            using var container = builder.Build();

            Assert.DoesNotThrow(() => container.Resolve<MountedCombatHudView>().Dispose());
        }
    }
}
