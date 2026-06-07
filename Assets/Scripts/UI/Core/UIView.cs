using System;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>순수 C# 뷰 컨트롤러 베이스. UIManager가 UXML 클론을 Initialize로 주입한다.</summary>
    public abstract class UIView : IDisposable
    {
        public VisualElement Root { get; private set; }

        protected CompositeDisposable Disposables { get; } = new();

        /// <summary>UIManager가 UXML 클론 직후 1회 호출. 파생은 base 호출 후 바인딩.</summary>
        public virtual void Initialize(VisualElement root)
        {
            Root = root;
        }

        /// <summary>레이어에 부착되고 표시 직전 호출.</summary>
        public virtual void OnOpen() { }

        /// <summary>레이어에서 제거되기 직전 호출.</summary>
        public virtual void OnClose() { }

        /// <summary>(M1 no-op 훅) 열기 연출.</summary>
        protected virtual UniTask PlayOpenAsync() => UniTask.CompletedTask;

        /// <summary>(M1 no-op 훅) 닫기 연출.</summary>
        protected virtual UniTask PlayCloseAsync() => UniTask.CompletedTask;

        public virtual void Dispose()
        {
            Disposables.Dispose();
        }
    }
}
