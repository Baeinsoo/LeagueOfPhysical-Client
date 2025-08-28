using GameFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace LOP
{
    public class StatsPopup : Popup
    {
        [SerializeField] private TextMeshProUGUI strength;
        [SerializeField] private TextMeshProUGUI dexterity;
        [SerializeField] private TextMeshProUGUI intelligence;
        [SerializeField] private TextMeshProUGUI vitality;
        [SerializeField] private TextMeshProUGUI stats;

        [SerializeField] private Button strengthButton;
        [SerializeField] private Button dexterityButton;
        [SerializeField] private Button intelligenceButton;
        [SerializeField] private Button vitalityButton;

        [Inject] private IPlayerContext playerContext;

        private void Awake()
        {
            SceneLifetimeScope.Inject(this);

            strengthButton.onClick.AddListener(OnStrengthButtonClicked);
            dexterityButton.onClick.AddListener(OnDexterityButtonClicked);
            intelligenceButton.onClick.AddListener(OnIntelligenceButtonClicked);
            vitalityButton.onClick.AddListener (OnVitalityButtonClicked);
        }
        private void OnDestroy()
        {
            strengthButton.onClick.RemoveListener(OnStrengthButtonClicked);
            dexterityButton.onClick.RemoveListener(OnDexterityButtonClicked);
            intelligenceButton.onClick.RemoveListener(OnIntelligenceButtonClicked);
            vitalityButton.onClick.RemoveListener(OnVitalityButtonClicked);
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (playerContext.entity == null)
            {
                return;
            }

            StatsComponent statsComponent = playerContext.entity.GetEntityComponent<StatsComponent>();
            strength.text = statsComponent.strength.ToString();
            dexterity.text = statsComponent.dexterity.ToString();
            intelligence.text = statsComponent.intelligence.ToString();
            vitality.text = statsComponent.vitality.ToString();

            UserComponent userComponent = playerContext.entity.GetEntityComponent<UserComponent>();
            stats.text = userComponent.statPoints.ToString();

            strengthButton.gameObject.SetActive(userComponent.statPoints > 0);
            dexterityButton.gameObject.SetActive(userComponent.statPoints > 0);
            intelligenceButton.gameObject.SetActive(userComponent.statPoints > 0);
            vitalityButton.gameObject.SetActive(userComponent.statPoints > 0);
        }

        private void OnStrengthButtonClicked()
        {
            if (playerContext.entity.GetEntityComponent<UserComponent>().statPoints == 0)
            {
                return;
            }

            StatAllocationToS statAllocationToS = new StatAllocationToS
            {
                EntityId = playerContext.entity.entityId,
                Stat = nameof(StatsComponent.strength),
            };

            playerContext.session.Send(statAllocationToS);
        }

        private void OnDexterityButtonClicked()
        {
            if (playerContext.entity.GetEntityComponent<UserComponent>().statPoints == 0)
            {
                return;
            }

            StatAllocationToS statAllocationToS = new StatAllocationToS
            {
                EntityId = playerContext.entity.entityId,
                Stat = nameof(StatsComponent.dexterity),
            };

            playerContext.session.Send(statAllocationToS);
        }

        private void OnIntelligenceButtonClicked()
        {
            if (playerContext.entity.GetEntityComponent<UserComponent>().statPoints == 0)
            {
                return;
            }

            StatAllocationToS statAllocationToS = new StatAllocationToS
            {
                EntityId = playerContext.entity.entityId,
                Stat = nameof(StatsComponent.intelligence),
            };

            playerContext.session.Send(statAllocationToS);
        }

        private void OnVitalityButtonClicked()
        {
            if (playerContext.entity.GetEntityComponent<UserComponent>().statPoints == 0)
            {
                return;
            }

            StatAllocationToS statAllocationToS = new StatAllocationToS
            {
                EntityId = playerContext.entity.entityId,
                Stat = nameof(StatsComponent.vitality),
            };

            playerContext.session.Send(statAllocationToS);
        }
    }
}
