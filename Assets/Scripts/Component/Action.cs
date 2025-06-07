using GameFramework;
using UnityEngine;

namespace LOP
{
    public abstract class Action : LOPComponent, IInitializable<string>
    {
        public bool isActive { get; protected set; }
        public string actionCode { get; private set; }
        public MasterData.Action masterData { get; private set; }
        
        public bool isCasting => GameEngine.Time.tick < startTick + masterData.CastTime / GameEngine.Time.tickInterval;
        public double remainCooldown { get; protected set; }

        protected long startTick;
        protected long endTick;
        protected double elapsedTime => (GameEngine.Time.tick - startTick) * GameEngine.Time.tickInterval;
        private long lastUpdateTick;

        protected new LOPEntity entity => (LOPEntity)base.entity;

        public bool initialized { get; protected set; }

        public virtual void Initialize(string actionCode)
        {
            this.actionCode = actionCode;
            this.masterData = SceneLifetimeScope.Resolve<IMasterDataManager>().GetMasterData<MasterData.Action>(actionCode);
            this.initialized = true;
        }

        void IInitializable.Initialize() { }

        public virtual bool TryActionStart()
        {
            if (isActive)
            {
                Debug.LogWarning("Action is already active.");
                return false;
            }

            if (remainCooldown > 0)
            {
                Debug.LogWarning($"Action is on cooldown. Remaining time: {remainCooldown} seconds.");
                return false;
            }
           
            ActionStart();

            return true;
        }

        protected void ActionStart()
        {
            isActive = true;
            startTick = GameEngine.Time.tick;

            OnActionStart();

            entity.eventBus.Publish(new Event.Entity.ActionStart(actionCode));
        }

        protected void ActionEnd()
        {
            isActive = false;
            endTick = GameEngine.Time.tick;
            remainCooldown = masterData.Cooldown;

            OnActionEnd();

            entity.eventBus.Publish(new Event.Entity.ActionEnd(actionCode));
        }

        public void UpdateAction()
        {
            if (GameEngine.Time.tick == lastUpdateTick)
            {
                Debug.LogWarning("Action update called with the same tick. Skipping update.");
                return;
            }

            if (isActive)
            {
                OnActionUpdate();

                if (elapsedTime >= masterData.Duration)
                {
                    ActionEnd();
                }
            }
            else
            {
                remainCooldown = System.Math.Max(remainCooldown - GameEngine.Time.tickInterval, 0);
            }
        }

        protected virtual void OnActionStart() { }
        protected virtual void OnActionUpdate() { }
        protected virtual void OnActionEnd() { }
    }
}
