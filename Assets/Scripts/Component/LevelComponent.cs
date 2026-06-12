using GameFramework;
using LOP.Event.Entity;
using System.ComponentModel;
using UnityEngine;

namespace LOP
{
    public class LevelComponent : LOPComponent
    {
        private int _level;
        public int level
        {
            get => _level;
            set => this.SetProperty(ref _level, value, RaisePropertyChanged);
        }

        private long _currentExp;
        public long currentExp
        {
            get => _currentExp;
            set => this.SetProperty(ref _currentExp, value, RaisePropertyChanged);
        }

        private long _expToNextLevel;
        public long expToNextLevel
        {
            get => _expToNextLevel;
            private set => this.SetProperty(ref _expToNextLevel, value, RaisePropertyChanged);
        }

        public void Initialize(int level, long currentExp)
        {
            this.level = level;
            this.currentExp = currentExp;
            this.expToNextLevel = 100;
        }

        public void AddExperience(int amount)
        {
            currentExp += amount;
            while (currentExp >= expToNextLevel)
            {
                currentExp -= expToNextLevel;
                level++;
                Debug.Log($"Level Up! New Level: {level}");
                expToNextLevel = 100;
            }
        }

        public void RaisePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            EventBus.Default.Publish(EventTopic.EntityId<LOPEntity>(entity.entityId), new PropertyChange(e.PropertyName));
        }
    }
}
