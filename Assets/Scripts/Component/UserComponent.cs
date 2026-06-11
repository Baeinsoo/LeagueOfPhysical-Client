using GameFramework;
using LOP.Event.Entity;
using System.ComponentModel;

namespace LOP
{
    public class UserComponent : LOPComponent
    {
        private int _statPoints;
        public int statPoints
        {
            get => _statPoints;
            set
            {
                this.SetProperty(ref _statPoints, value, RaisePropertyChanged);
            }
        }

        public void RaisePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            EventBus.Default.Publish(EventTopic.EntityId<LOPEntity>(entity.entityId), new PropertyChange(e.PropertyName));
        }
    }
}
