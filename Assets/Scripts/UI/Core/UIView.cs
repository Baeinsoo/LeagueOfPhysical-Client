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

        /// <summary>이 View가 속하는 밴드(z-order 등급).</summary>
        public abstract UILayer Layer { get; }

        /// <summary>모달이면 밴드에 백드롭(딤+입력 차단)을 깐다.</summary>
        public virtual bool IsModal => false;

        /// <summary>아래로의 입력을 막는지. 기본은 모달 여부. 전체화면 오버레이(로딩/매칭)는 모달이 아니어도 true로 override.</summary>
        public virtual bool BlocksUnderlyingInput => IsModal;

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

        private bool _disposed;

        /// <summary>
        /// 여러 번 불려도 안전하다. 파생은 이 메서드가 아니라 <see cref="Dispose(bool)"/>를 채운다 —
        /// 그래야 중복 호출 가드가 한 곳에만 있고 파생이 잊을 수 없다.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>파생이 자기 자원을 정리하는 자리. base 호출을 잊지 말 것.</summary>
        /// <param name="disposing">종료자가 아니라 Dispose()에서 왔으면 true. 이 계층엔 종료자가 없어 늘 true다.</param>
        protected virtual void Dispose(bool disposing)
        {
            Disposables.Dispose();
        }
    }
}
