using GameFramework;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UniRx;

namespace LOP
{
    public class LOPEntityPresenter : MonoEntityPresenter<LOPEntity>
    {
        public Rigidbody modelRigidbody { get; set; }

        protected virtual void Awake()
        {
            entity = gameObject.GetComponent<LOPEntity>();
            entity.PropertyChanged += OnPropertyChanged;
        }

        protected virtual void OnDestroy()
        {
            entity.PropertyChanged -= OnPropertyChanged;
            entity = null;
        }

        public virtual void Initialize<TEntityCreationData>(TEntityCreationData creationData) where TEntityCreationData : struct, IEntityCreationData
        {
            modelRigidbody = entity.gameObject.AddComponent<Rigidbody>();

            LOPGameEngine.messageBroker.Receive<Message.LOPGameEngine.Update.BeforePhysicsSimulation>().Subscribe(OnUpdateBeforePhysicsSimulation).AddTo(this);
            LOPGameEngine.messageBroker.Receive<Message.LOPGameEngine.Update.AfterPhysicsSimulation>().Subscribe(OnUpdateAfterPhysicsSimulation).AddTo(this);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(entity.position):
                    modelRigidbody.position = entity.position;
                    break;
                
                case nameof(entity.rotation):
                    modelRigidbody.rotation = Quaternion.Euler(entity.rotation);
                    break;

                case nameof(entity.velocity):
                    modelRigidbody.linearVelocity = entity.velocity;
                    break;

                case "modelId":
                    break;
            }
        }

        private void OnUpdateBeforePhysicsSimulation(Message.LOPGameEngine.Update.BeforePhysicsSimulation message) { }

        private void OnUpdateAfterPhysicsSimulation(Message.LOPGameEngine.Update.AfterPhysicsSimulation message)
        {
            SyncPhysics();
        }

        private void SyncPhysics()
        {
            entity.position = modelRigidbody.position;
            entity.rotation = modelRigidbody.rotation.eulerAngles;
            entity.velocity = modelRigidbody.linearVelocity;
        }
    }
}
