using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>"곧 열려요"를 빠르게 여러 번 눌러도 토스트는 하나만 — 이전 것은 닫을 대상으로 돌려준다.</summary>
    public class ToastSlotTests
    {
        private sealed class T { }

        [Test]
        public void 새_토스트가_오면_이전_것을_돌려준다()
        {
            var slot = new ToastSlot<T>();
            var a = new T(); var b = new T();
            Assert.IsNull(slot.Replace(a));
            Assert.AreSame(a, slot.Replace(b));
            Assert.AreSame(b, slot.Current);
        }

        [Test]
        public void 늦게_온_타이머는_새_토스트를_닫지_않는다()
        {
            var slot = new ToastSlot<T>();
            var a = new T(); var b = new T();
            slot.Replace(a); slot.Replace(b);
            Assert.IsNull(slot.Clear(a));
            Assert.AreSame(b, slot.Current);
        }

        [Test]
        public void 토스트는_하나만_남는다()
        {
            var slot = new ToastSlot<T>();
            var closed = 0;
            for (int i = 0; i < 5; i++) if (slot.Replace(new T()) != null) closed++;
            Assert.AreEqual(4, closed);
            Assert.IsNotNull(slot.Current);
        }

        [Test]
        public void 제때_온_타이머는_비운다()
        {
            var slot = new ToastSlot<T>();
            var a = new T();
            slot.Replace(a);
            Assert.AreSame(a, slot.Clear(a));
            Assert.IsNull(slot.Current);
        }
    }
}
