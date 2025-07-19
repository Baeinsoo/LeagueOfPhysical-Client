using UnityEngine;

namespace LOP
{
    public class LevelComponent : LOPComponent
    {
        public int level { get; private set; }
        public long currentExp { get; private set; }
        public long expToNextLevel { get; private set; }

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
    }
}
