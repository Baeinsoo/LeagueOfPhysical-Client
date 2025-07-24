using GameFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LOP
{
    public class CharacterUI : MonoBehaviour
    {
        [SerializeField] private Slider hpBar;
        [SerializeField] private Slider mpBar;
        [SerializeField] private Slider expBar;
        [SerializeField] private TextMeshProUGUI currentHP;
        [SerializeField] private TextMeshProUGUI maxHP;
        [SerializeField] private TextMeshProUGUI currentMP;
        [SerializeField] private TextMeshProUGUI maxMP;
        [SerializeField] private TextMeshProUGUI currentExp;
        [SerializeField] private TextMeshProUGUI maxExp;
        [SerializeField] private TextMeshProUGUI level;
        [SerializeField] private Button pointButton;

        public LOPEntity entity { get; private set; }

        public void SetEntity(LOPEntity entity)
        {
            this.entity = entity;
        }

        private void Update()
        {
            if (entity == null)
            {
                return;
            }

            HealthComponent healthComponent = entity.GetEntityComponent<HealthComponent>();
            hpBar.value = (float)healthComponent.currentHP / healthComponent.maxHP;
            currentHP.text = healthComponent.currentHP.ToString();
            maxHP.text = healthComponent.maxHP.ToString();

            ManaComponent manaComponent = entity.GetEntityComponent<ManaComponent>();
            mpBar.value = (float)manaComponent.currentMP / manaComponent.maxMP;
            currentMP.text = manaComponent.currentMP.ToString();
            maxMP.text = manaComponent.maxMP.ToString();

            LevelComponent levelComponent = entity.GetEntityComponent<LevelComponent>();
            expBar.value = (float)levelComponent.currentExp / levelComponent.expToNextLevel;
            currentExp.text = levelComponent.currentExp.ToString();
            maxExp.text = levelComponent.expToNextLevel.ToString();
            level.text = levelComponent.level.ToString();
        }
    }
}
