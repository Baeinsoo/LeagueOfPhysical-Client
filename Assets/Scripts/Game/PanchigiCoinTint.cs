using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 판 위 동전을 지금 프레임을 치는 사람의 색으로 칠한다 — 누구 프레임인지 한눈에 보이게.
    /// 동전 외형은 비동기로 붙으므로 매 프레임 확인하되, 색이 같으면 아무것도 안 한다.
    /// </summary>
    public class PanchigiCoinTint : ITickable
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly PanchigiStateStore store;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly Dictionary<Renderer, Color> painted = new();
        private readonly List<Renderer> gone = new();
        private readonly MaterialPropertyBlock block = new();

        public PanchigiCoinTint(PanchigiStateStore store, GameFramework.World.EntityRegistry entityRegistry)
        {
            this.store = store;
            this.entityRegistry = entityRegistry;
        }

        public void Tick()
        {
            int index = IndexOf(store.BoardOwnerEntityId);
            if (index < 0) { return; }
            Color color = PanchigiPlayerColors.For(index);

            ForgetDestroyed();

            foreach (LOPEntityView view in Object.FindObjectsByType<LOPEntityView>(FindObjectsSortMode.None))
            {
                if (view.visualGameObject == null || IsCoin(view.entityId) == false) { continue; }

                foreach (Renderer renderer in view.visualGameObject.GetComponentsInChildren<Renderer>())
                {
                    if (painted.TryGetValue(renderer, out Color current) && current == color) { continue; }
                    renderer.GetPropertyBlock(block);
                    block.SetColor(BaseColor, color);
                    renderer.SetPropertyBlock(block);
                    painted[renderer] = color;
                }
            }
        }

        private void ForgetDestroyed()
        {
            gone.Clear();
            foreach (Renderer renderer in painted.Keys)
            {
                if (renderer == null) { gone.Add(renderer); }
            }
            foreach (Renderer renderer in gone) { painted.Remove(renderer); }
        }

        private int IndexOf(string entityId)
        {
            if (string.IsNullOrEmpty(entityId)) { return -1; }
            for (int i = 0; i < store.PlayerEntityIds.Count; i++)
            {
                if (store.PlayerEntityIds[i] == entityId) { return i; }
            }
            return -1;
        }

        private bool IsCoin(string entityId)
        {
            return entityId != null && entityRegistry.Get(entityId)?.Get<EntityKind>()?.Kind == EntityType.Coin;
        }
    }
}
