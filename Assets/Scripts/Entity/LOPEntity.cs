using GameFramework;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UniRx;
using UnityEngine.AddressableAssets;
using LOP.Event.LOPGameEngine.Update;

namespace LOP
{
    public class LOPEntity : MonoEntity
    {
        public readonly IMessageBrokerExtended eventBus = new MessageBrokerExtended();

        public Status[] statuses => components.OfType<Status>()?.ToArray();
        public Ability[] abilities => components.OfType<Ability>()?.ToArray();
 
        private Vector3 _position;
        public override Vector3 position
        {
            get => _position;
            set
            {
                if (visualRigidbody != null)
                {
                    visualRigidbody.position = value;
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
                if (visualRigidbody != null)
                {
                    visualRigidbody.rotation = Quaternion.Euler(value);
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
                if (visualRigidbody != null)
                {
                    visualRigidbody.linearVelocity = value;
                }
                this.SetProperty(ref _velocity, value, RaisePropertyChanged);
            }
        }

        public Rigidbody visualRigidbody { get; protected set; }

        private GameObject _visualGameObject;
        public GameObject visualGameObject
        {
            get => _visualGameObject;
            set
            {
                this.SetProperty(ref _visualGameObject, value, RaisePropertyChanged);
            }
        }

        public Vector3 beginPosition { get; private set; }
        public Vector3 beginRotation { get; private set; }

        protected void RaisePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            eventBus.Publish(new Event.Entity.PropertyChange(e.PropertyName));
        }

        protected virtual void Awake()
        {
            SceneLifetimeScope.Resolve<IGameEngine>().AddListener(this);
        }

        protected virtual void OnDestroy()
        {
            SceneLifetimeScope.Resolve<IGameEngine>().RemoveListener(this);
        }

        public virtual void Initialize<TEntityCreationData>(TEntityCreationData creationData) where TEntityCreationData : struct, IEntityCreationData
        {
            entityId = creationData.entityId;
            position = creationData.position;
            rotation = creationData.rotation;
            velocity = creationData.velocity;

            if (creationData is LOPEntityCreationData lopEntityCreationData)
            {
                var handle = Addressables.LoadAssetAsync<GameObject>(lopEntityCreationData.visualId);
                handle.Completed += (prefab) =>
                {
                    visualGameObject = Instantiate(prefab.Result, transform);
                    visualRigidbody = visualGameObject.AddComponent<Rigidbody>();
                    visualRigidbody.constraints = RigidbodyConstraints.FreezeRotation;

                    SphereCollider sphereCollider = visualGameObject.AddComponent<SphereCollider>();
                    sphereCollider.radius = 0.5f;
                    sphereCollider.center = new Vector3(0, 0.5f, 0);
                };
            }
        }

        [GameEngineListen(typeof(Begin))]
        private void OnUpdateBegin()
        {
            beginPosition = position;
            beginRotation = rotation;
        }

        public override void UpdateEntity()
        {
            UpdateStatuses();

            UpdateAbilities();
        }

        [GameEngineListen(typeof(End))]
        private void OnUpdateEnd()
        {
            UpdateNetworkState();
        }

        private void UpdateStatuses()
        {
            foreach (var status in statuses.OrEmpty())
            {
                status.UpdateStatus();
            }
        }

        private void UpdateAbilities()
        {
            foreach (var ability in abilities.OrEmpty())
            {
                ability.UpdateAbility();
            }
        }

        private void UpdateNetworkState()
        {
        }

        [GameEngineListen(typeof(BeforePhysicsSimulation))]
        private void OnUpdateBeforePhysicsSimulation() { }

        [GameEngineListen(typeof(AfterPhysicsSimulation))]
        private void OnUpdateAfterPhysicsSimulation()
        {
            SyncPhysics();
        }

        private void SyncPhysics()
        {
            if (visualRigidbody == null)
            {
                return;
            }

            position = visualRigidbody.position;
            rotation = visualRigidbody.rotation.eulerAngles;
            velocity = visualRigidbody.linearVelocity;
        }
    }
}
