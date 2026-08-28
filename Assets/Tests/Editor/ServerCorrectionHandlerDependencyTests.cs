using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 보정 핸들러가 러너를 되받지 못하게 막는다.
    ///
    /// 러너는 자기 의존 사슬 안에서 이 핸들러를 만든다(러너 → ReconcileSystem → Reconciler → 핸들러).
    /// 그래서 핸들러가 러너를 다시 받으면 DI 그래프에 고리가 생긴다. 컴파일도 되고 단위 테스트도
    /// 전부 통과한 뒤, 방에 들어가는 순간에만 VContainer가 "Circular dependency detected"로 터진다
    /// — 실제로 그렇게 한 번 나갔다.
    ///
    /// 틱 간격이 필요하면 러너 대신 <c>ApplyAuthoritative</c>의 deltaTime을 쓴다(같은 값이다).
    ///
    /// 한계: 생성자 인자에 러너가 <b>직접</b> 있는 경우만 잡는다. 다른 타입을 한 다리 건너
    /// 러너에 닿는 고리는 못 잡는다 — 그건 컨테이너를 실제로 세우는 테스트라야 잡힌다(아직 없다).
    /// </summary>
    public class ServerCorrectionHandlerDependencyTests
    {
        [Test]
        public void 보정_핸들러는_러너를_받지_않는다()
        {
            Type[] handlers = typeof(IServerCorrectionHandler).Assembly.GetTypes()
                .Where(t => typeof(IServerCorrectionHandler).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .ToArray();

            Assert.IsNotEmpty(handlers, "구현이 하나도 안 잡혔다면 이 테스트는 아무것도 지키지 않는다.");

            foreach (Type handler in handlers)
            {
                foreach (ConstructorInfo ctor in handler.GetConstructors())
                {
                    ParameterInfo offender = ctor.GetParameters().FirstOrDefault(
                        p => typeof(GameFramework.Runner.IRunner).IsAssignableFrom(p.ParameterType));

                    Assert.IsNull(offender,
                        $"{handler.Name}의 생성자가 러너({offender?.ParameterType.Name})를 받는다 — DI 고리가 생긴다.");
                }
            }
        }
    }
}
