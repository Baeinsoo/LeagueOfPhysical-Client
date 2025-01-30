using GameFramework;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UniRx;

namespace LOP
{
    public class LOPEntity : MonoEntity, INotifyPropertyChanged
    {
        public Status[] statuses => components.OfType<Status>()?.ToArray();
        public Behavior[] behaviors => components.OfType<Behavior>()?.ToArray();

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

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(sender, e);
        }

        protected virtual void Awake()
        {
            LOPGameEngine.messageBroker.Receive<Message.LOPGameEngine.Update.Begin>().Subscribe(OnUpdateBegin).AddTo(this);
            LOPGameEngine.messageBroker.Receive<Message.LOPGameEngine.Update.End>().Subscribe(OnUpdateEnd).AddTo(this);
        }

        public virtual void Initialize<TEntityCreationData>(TEntityCreationData creationData) where TEntityCreationData : struct, IEntityCreationData
        {
            entityId = creationData.entityId;
            position = creationData.position;
            rotation = creationData.rotation;
            velocity = creationData.velocity;
        }

        public Vector3 beginPosition { get; private set; }
        public Vector3 beginRotation { get; private set; }

        private void OnUpdateBegin(Message.LOPGameEngine.Update.Begin message)
        {
            beginPosition = position;
            beginRotation = rotation;
        }

        public override void UpdateEntity()
        {
            UpdateStatuses();

            UpdateBehaviors();
        }

        private void OnUpdateEnd(Message.LOPGameEngine.Update.End message)
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

        private void UpdateBehaviors()
        {
            foreach (var behavior in behaviors.OrEmpty())
            {
                behavior.UpdateBehavior();
            }
        }

        private void UpdateNetworkState()
        {
        }
    }
}
