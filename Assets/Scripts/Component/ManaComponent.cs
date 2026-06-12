using GameFramework;
using LOP.Event.Entity;
using System.ComponentModel;

namespace LOP
{
    public class ManaComponent : LOPComponent
    {
        private int _maxMP;
        public int maxMP
        {
            get => _maxMP;
            set => this.SetProperty(ref _maxMP, value, RaisePropertyChanged);
        }

        private int _currentMP;
        public int currentMP
        {
            get => _currentMP;
            set => this.SetProperty(ref _currentMP, value, RaisePropertyChanged);
        }

        public void Initialize(int maxMP, int currentMP)
        {
            this.maxMP = maxMP;
            this.currentMP = currentMP;
        }

        public void RaisePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            EventBus.Default.Publish(EventTopic.EntityId<LOPEntity>(entity.entityId), new PropertyChange(e.PropertyName));
        }
    }
}
