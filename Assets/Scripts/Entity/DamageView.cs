using GameFramework;
using LOP.Event.Entity;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class DamageView : MonoEntityView<LOPEntity, LOPEntityController>
    {
        private const int MAX_DAMAGE_UI = 4;
        private List<DamageUI> damageUIs = new List<DamageUI>();

        private bool isCleanup = false;

        private void Awake()
        {
            for (int i = 0; i < MAX_DAMAGE_UI; i++)
            {
                DamageUI damageUI = Instantiate(Resources.Load<DamageUI>("DamageUI"));
                damageUI.isActive = false;
                damageUIs.Add(damageUI);
            }
        }

        protected void Start()
        {
            EventBus.Default.Subscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnEntityDamage);
        }

        public override void Cleanup()
        {
            Debug.Log($"DamageView Cleanup. EntityId: {entity.entityId}");
            isCleanup = true;

            EventBus.Default.Unsubscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnEntityDamage);

            foreach (var damageUI in damageUIs)
            {
                if (damageUI != null)
                {
                    Destroy(damageUI.gameObject);
                }
            }
            damageUIs.Clear();

            base.Cleanup();
        }

        private void OnEntityDamage(EntityDamage entityDamage)
        {
            if (isCleanup) return;

            List<DamageUI> activeUIs = damageUIs.FindAll(d => d.isActive);
            foreach (var activeUI in activeUIs)
            {
                activeUI.index++;
                activeUI.offset = new Vector3(activeUI.offset.x, 20f + 20f * activeUI.index, activeUI.offset.z);
                if (activeUI.index >= MAX_DAMAGE_UI)
                {
                    activeUI.isActive = false;
                }
            }

            List<DamageUI> inactiveUIs = damageUIs.FindAll(d => !d.isActive);
            DamageUI targetUI = inactiveUIs.First();
            targetUI.Clear();

            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            string text = entityDamage.isDodged ? "회피"
                : entityDamage.isCritical ? "Critical!\n" + entityDamage.damage
                : entityDamage.damage.ToString();

            targetUI.ShowDamage(
                entity,
                text,
                new Vector3(Random.Range(-5f, 5f), 20f, 0),
                canvas
            );
        }
    }
}
