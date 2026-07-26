using System.Collections.Generic;
using GameFramework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 이 캐릭터에 걸린 상태이상을 몸에 붙는 이펙트로 보여준다.
    /// 상태 목록을 매 프레임 읽어 맞춘다 — 새로 걸린 것만 켜고, 풀린 것만 끈다(걷기 애니와 같은 방식).
    /// </summary>
    public class StatusEffectVfxView : MonoBehaviour, ICleanup
    {
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private LOP.MasterData.LOPMasterData masterData;

        private string entityId;

        // 켜둔(또는 켜는 중인) 이펙트 하나.
        private class Vfx
        {
            public GameObject instance;                     // 아직 로딩 중이면 null
            public AsyncOperationHandle<GameObject> handle;
            // "로딩이 끝남"과 "완료 콜백이 이미 실행됨"은 다른 사건이다(콜백이 지연될 수 있음).
            // 그래서 handle 해제 권한을 상태로 추론하지 않고 여기에 직접 기록한다.
            public bool ownsHandle;
        }

        private readonly Dictionary<int, Vfx> vfxByEffectId = new Dictionary<int, Vfx>();

        // 매 프레임 재사용 — 프레임마다 새로 만들면 쓰레기가 쌓인다.
        private readonly HashSet<int> activeIds = new HashSet<int>();
        private readonly List<int> removedIds = new List<int>();

        public void SetEntityId(string entityId)
        {
            this.entityId = entityId;
        }

        private void Update()
        {
            if (entityId == null)
            {
                return;
            }

            var statusEffects = entityRegistry.Get(entityId)?.Get<StatusEffects>();
            activeIds.Clear();
            if (statusEffects != null)
            {
                foreach (var effect in statusEffects.Effects)
                {
                    activeIds.Add(effect.EffectId);
                }
            }

            foreach (int id in activeIds)
            {
                if (vfxByEffectId.ContainsKey(id) == false)
                {
                    Spawn(id);
                }
            }

            removedIds.Clear();
            foreach (var pair in vfxByEffectId)
            {
                if (activeIds.Contains(pair.Key) == false)
                {
                    removedIds.Add(pair.Key);
                }
            }
            foreach (int id in removedIds)
            {
                Despawn(id);
            }
        }

        private void Spawn(int effectId)
        {
            var view = masterData.Tables.TbStatusEffectView.GetOrDefault(effectId);
            if (view == null || string.IsNullOrEmpty(view.VfxAddress))
            {
                return;   // 연출을 정해두지 않은 상태이상
            }

            var vfx = new Vfx();
            vfxByEffectId[effectId] = vfx;    // 자리를 먼저 잡아 같은 효과를 두 번 로드하지 않는다
            vfx.handle = Addressables.LoadAssetAsync<GameObject>(view.VfxAddress);
            vfx.handle.Completed += handle =>
            {
                // 로딩이 끝나기 전에 상태이상이 풀렸거나 캐릭터가 사라졌을 수 있다(슬로우는 2초).
                // 그러면 받아온 것을 그대로 놓아준다.
                bool stillWanted = this != null
                    && vfxByEffectId.TryGetValue(effectId, out Vfx current)
                    && ReferenceEquals(current, vfx);
                if (stillWanted == false)
                {
                    Addressables.Release(handle);
                    return;
                }
                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    // 아직 원하는 상태지만 로딩 자체가 실패한 경우 — 다시 시도하지 않으므로
                    // 아무것도 소유하지 않는 죽은 항목을 남기지 말고 자리도 함께 치운다.
                    vfxByEffectId.Remove(effectId);
                    Addressables.Release(handle);
                    return;
                }

                // 여기서부터가 실제 소유권 이전 지점 — Despawn은 이 플래그만 보고 해제 여부를 판단한다.
                vfx.ownsHandle = true;
                // 모델(스킨) 밑이 아니라 루트에 붙인다 — 스킨이 갈릴 때 딸려 파괴되지 않게.
                vfx.instance = Instantiate(handle.Result, transform);
            };
        }

        private void Despawn(int effectId)
        {
            if (vfxByEffectId.TryGetValue(effectId, out Vfx vfx) == false)
            {
                return;
            }
            vfxByEffectId.Remove(effectId);

            if (vfx.instance != null)
            {
                Destroy(vfx.instance);
            }
            // 아직 콜백이 소유권을 넘겨받지 않았으면(로딩 중이거나, 완료됐지만 콜백이 이번 프레임
            // LateUpdate로 지연된 상태) 여기서 놓지 않는다 — 콜백이 "이미 풀렸다"를 보고 대신 놓는다(이중 해제 방지).
            if (vfx.ownsHandle)
            {
                Addressables.Release(vfx.handle);
            }
        }

        public void Cleanup()
        {
            removedIds.Clear();
            foreach (var pair in vfxByEffectId)
            {
                removedIds.Add(pair.Key);
            }
            foreach (int id in removedIds)
            {
                Despawn(id);
            }
            entityId = null;
        }
    }
}
