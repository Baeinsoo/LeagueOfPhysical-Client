using GameFramework;
using LOP.Event.Entity;
using System.ComponentModel;
using UnityEngine;

namespace LOP
{
    public class LOPEntity : MonoEntity
    {
        private Vector3 _position;
        public override Vector3 position
        {
            get => _position;
            set
            {
                this.SetProperty(ref _position, value, RaisePropertyChanged);
            }
        }

        private Vector3 _rotation;
        public override Vector3 rotation
        {
            get => _rotation;
            set
            {
                this.SetProperty(ref _rotation, value, RaisePropertyChanged);
            }
        }

        private Vector3 _velocity;
        public override Vector3 velocity
        {
            get => _velocity;
            set
            {
                this.SetProperty(ref _velocity, value, RaisePropertyChanged);
            }
        }

        public void RaisePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            EventBus.Default.Publish(EventTopic.EntityId<LOPEntity>(entityId), new PropertyChange(e.PropertyName));
        }

        public virtual void Initialize<TEntityCreationData>(TEntityCreationData creationData) where TEntityCreationData : struct, IEntityCreationData
        {
            entityId = creationData.entityId;
            position = creationData.position;
            rotation = creationData.rotation;
            velocity = creationData.velocity;
        }

        public override void UpdateEntity()
        {
            UpdateStatuses();

            UpdateActions();
        }

        private void UpdateStatuses()
        {
            foreach (var status in this.GetEntityComponents<Status>().OrEmpty())
            {
                status.UpdateStatus();
            }
        }

        private void UpdateActions()
        {
            foreach (var action in this.GetEntityComponents<Action>().OrEmpty())
            {
                action.UpdateAction();
            }
        }

        public void SyncPhysics()
        {
            PhysicsComponent physicsComponent = this.GetEntityComponent<PhysicsComponent>();

            if (physicsComponent == null)
            {
                return;
            }

            position = physicsComponent.entityRigidbody.position;
            rotation = physicsComponent.entityRigidbody.rotation.eulerAngles;
            velocity = physicsComponent.entityRigidbody.linearVelocity;
        }
    }
}
