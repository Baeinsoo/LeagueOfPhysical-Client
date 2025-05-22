using GameFramework;
using TMPro;
using UnityEngine;

namespace LOP
{
    public class TimeUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI tickUI;
        [SerializeField] private TextMeshProUGUI elapsedUI;
        [SerializeField] private TextMeshProUGUI rttUI;

        private void Update()
        {
            if (GameEngine.current == null)
            {
                return;
            }

            tickUI.text = $"Tick: {GameEngine.Time.tick}";
            elapsedUI.text = $"elapsed: {GameEngine.Time.elapsedTime:F2}";
            rttUI.text = $"RTT: {Mirror.NetworkTime.rtt * 1000:F0}";
        }
    }
}
