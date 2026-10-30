using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MessagePipe;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 서버발 메시지를 구독하는 곳이 있으면 디스패처와 루트 브로커에도 그 메시지가 있어야 한다.
    /// ISubscriber는 열린 제네릭으로 에러 없이 풀려서, 빠뜨려도 DI·컴파일·다른 시험이 다 초록이다.
    /// </summary>
    public class NetworkMessageRouteTests
    {
        static IEnumerable<Type> SubscribedServerMessages()
        {
            var assembly = typeof(NetworkMessageDispatcher).Assembly;
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var found = new HashSet<Type>();
            foreach (var type in assembly.GetTypes())
            {
                var candidates = type.GetConstructors(all).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
                    .Concat(type.GetFields(all).Select(f => f.FieldType));
                foreach (var candidate in candidates)
                {
                    if (!candidate.IsGenericType || candidate.GetGenericTypeDefinition() != typeof(ISubscriber<>)) continue;
                    var message = candidate.GetGenericArguments()[0];
                    if (typeof(GameFramework.IMessage).IsAssignableFrom(message) && message.Name.EndsWith("ToC"))
                    {
                        found.Add(message);
                    }
                }
            }
            return found;
        }

        sealed class RecordingPublisher<T> : IPublisher<T>
        {
            public readonly List<object> Published;
            public RecordingPublisher(List<object> published) { Published = published; }
            public void Publish(T message) => Published.Add(message);
        }

        [Test]
        public void 구독하는_서버_메시지는_디스패처가_발행한다()
        {
            //  생성자 인자를 다 가짜 발행기로 채워 실제로 보내 본다 — 인자만 있고 등록을 빠뜨린 경우도 잡는다.
            var published = new List<object>();
            var ctor = typeof(NetworkMessageDispatcher).GetConstructors().Single();
            var args = ctor.GetParameters()
                .Select(p => Activator.CreateInstance(
                    typeof(RecordingPublisher<>).MakeGenericType(p.ParameterType.GetGenericArguments()[0]), published))
                .ToArray();
            var dispatcher = (NetworkMessageDispatcher)ctor.Invoke(args);

            var subscribed = SubscribedServerMessages().ToList();
            Assert.That(subscribed, Does.Contain(typeof(KnockbackStateToC)), "구독 수집이 동작하지 않는다");
            var missing = new List<string>();
            foreach (var type in subscribed)
            {
                published.Clear();
                var message = (GameFramework.IMessage)Activator.CreateInstance(type);
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
                dispatcher.Dispatch(message);
                if (published.Count != 1 || published[0] != (object)message) missing.Add(type.Name);
            }
            Assert.IsEmpty(missing, "디스패처가 발행하지 않는 메시지: " + string.Join(", ", missing));
        }

        [Test]
        public void 구독하는_서버_메시지는_루트에_브로커가_있다()
        {
            // 루트 스코프는 MonoBehaviour라 컨테이너를 세우기 무겁다 — 등록 줄을 소스에서 찾는다.
            var source = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Scripts", "RootLifetimeScope.cs"));
            var missing = SubscribedServerMessages()
                .Where(t => !source.Contains($"RegisterOrderedMessageBroker<{t.Name}>()"))
                .Select(t => t.Name).ToList();
            Assert.IsEmpty(missing, "루트 브로커에 없는 메시지: " + string.Join(", ", missing));
        }
    }
}
