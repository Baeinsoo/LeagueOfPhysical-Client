using GameFramework;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UniRx;
using LOP.MasterData;

namespace LOP
{
    public class LOPEntity : MonoEntity
    {
        public readonly IMessageBrokerExtended eventBus = new MessageBrokerExtended();

        public Status[] statuses => components.OfType<Status>()?.ToArray();
        public Action[] actions => components.OfType<Action>()?.ToArray();

        public string characterCode { get; private set; }
        public MasterData.Character masterData { get; private set; }

        private Vector3 _position;
        public override Vector3 position
        {
            get => _position;
            set
            {
                if (entityRigidbody != null)
                {
                    entityRigidbody.position = value;
                }
                this.SetProperty(ref _position, value, RaisePropertyChanged);
            }
        }

        private Vector3 _rotation;
        public override Vector3 rotation
        {
            get => _rotation;
            set
            {
                if (entityRigidbody != null)
                {
                    entityRigidbody.rotation = Quaternion.Euler(value);
                }
                this.SetProperty(ref _rotation, value, RaisePropertyChanged);
            }
        }

        private Vector3 _velocity;
        public override Vector3 velocity
        {
            get => _velocity;
            set
            {
                if (entityRigidbody != null)
                {
                    entityRigidbody.linearVelocity = value;
                }
                this.SetProperty(ref _velocity, value, RaisePropertyChanged);
            }
        }

        private string _visualId;
        public string visualId
        {
            get => _visualId;
            set
            {
                this.SetProperty(ref _visualId, value, RaisePropertyChanged);
            }
        }

        private GameObject _physicsGameObject;
        public GameObject physicsGameObject
        {
            get => _physicsGameObject;
            set
            {
                this.SetProperty(ref _physicsGameObject, value, RaisePropertyChanged);
            }
        }

        public Rigidbody entityRigidbody { get; private set; }
        public Collider[] entityColliders { get; private set; }

        protected void RaisePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            eventBus.Publish(new Event.Entity.PropertyChange(e.PropertyName));
        }

        public virtual void Initialize<TEntityCreationData>(TEntityCreationData creationData) where TEntityCreationData : struct, IEntityCreationData
        {
            entityId = creationData.entityId;
            position = creationData.position;
            rotation = creationData.rotation;
            velocity = creationData.velocity;

            if (creationData is LOPEntityCreationData lopEntityCreationData)
            {
                characterCode = lopEntityCreationData.characterCode;
                masterData = SceneLifetimeScope.Resolve<IMasterDataManager>().GetMasterData<Character>(lopEntityCreationData.characterCode);
                visualId = lopEntityCreationData.visualId;

                if (physicsGameObject != null)
                {
                    Destroy(physicsGameObject);
                }

                GameObject physics = transform.parent.Find("Physics").gameObject;
                physicsGameObject = physics.CreateChild("PhysicsGameObject");

                entityRigidbody = physicsGameObject.AddComponent<Rigidbody>();
                entityRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
                entityRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                entityRigidbody.position = position;
                entityRigidbody.rotation = Quaternion.Euler(rotation);
                entityRigidbody.linearVelocity = velocity;

                CapsuleCollider capsuleCollider = physicsGameObject.AddComponent<CapsuleCollider>();
                capsuleCollider.radius = 0.35f;
                capsuleCollider.height = 1.5f;
                capsuleCollider.center = new Vector3(0, capsuleCollider.height * 0.5f, 0);
                entityColliders = new Collider[] { capsuleCollider };
            }

            bool isUserEntity = SceneLifetimeScope.Resolve<IGameDataStore>().userEntityId == creationData.entityId;
            if (isUserEntity)
            {
                SceneLifetimeScope.Resolve<IPlayerContext>().entity = this;
            }
        }

        public override void UpdateEntity()
        {
            UpdateStatuses();

            UpdateActions();
        }

        private void UpdateStatuses()
        {
            foreach (var status in statuses.OrEmpty())
            {
                status.UpdateStatus();
            }
        }

        private void UpdateActions()
        {
            foreach (var action in actions.OrEmpty())
            {
                action.UpdateAction();
            }
        }

        public void SyncPhysics()
        {
            if (entityRigidbody == null)
            {
                return;
            }

            position = entityRigidbody.position;
            rotation = entityRigidbody.rotation.eulerAngles;
            velocity = entityRigidbody.linearVelocity;
        }
    }
}
