using UnityEngine;

namespace LOP
{
    /// <summary><see cref="ArcheryCrowdView"/>를 매 프레임 돌린다. 카메라와 상관없어 실행 순서를 따로 정하지 않는다.</summary>
    public class ArcheryCrowdDriver : MonoBehaviour
    {
        public ArcheryCrowdView View { get; set; }

        private void Update()
        {
            View?.Apply();
        }
    }
}
