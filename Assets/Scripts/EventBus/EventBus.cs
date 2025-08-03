using GameFramework;

namespace LOP
{
    public static class EventBus
    {
        public static IEventBus Default { get; } = new GameFramework.EventBus();
    }
}
