using UnityEngine;

namespace LOP
{
    public class ArcheryChickenDriver : MonoBehaviour
    {
        public ArcheryChickenView View { get; set; }

        private void Update()
        {
            View?.Apply();
        }
    }
}
