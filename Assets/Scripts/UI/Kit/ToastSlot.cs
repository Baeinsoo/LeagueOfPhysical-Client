namespace LOP.UI
{
    /// <summary>토스트 자리 하나. 새 것이 오면 이전 것을 돌려줘 닫게 하고, 늦게 온 닫기 요청은 무시한다.</summary>
    public sealed class ToastSlot<T> where T : class
    {
        public T Current { get; private set; }

        public T Replace(T next)
        {
            var previous = Current;
            Current = next;
            return previous;
        }

        public T Clear(T expected)
        {
            if (Current != expected) return null;
            Current = null;
            return expected;
        }
    }
}
