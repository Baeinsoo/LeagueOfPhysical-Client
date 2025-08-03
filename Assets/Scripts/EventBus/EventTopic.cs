
namespace LOP
{
    public static class EventTopic
    {
        public const string Entity = "Entity";
        public const string WebResponse = "WebResponse";

        public static string EntityId<T>(string entityId) => $"{typeof(T).Name}_{entityId}";
    }
}
