using UnityEngine;

namespace LOP
{
    public class LOPActor : MonoBehaviour
    {
        public string entityId { get; private set; }

        private LOPEntityView view;

        // 스포너(EntityBinder)가 actor를 만든 직후 id를 세팅한다.
        public void SetEntityId(string entityId)
        {
            this.entityId = entityId;
        }

        // 스포너가 뷰를 만든 뒤 등록한다(Actor 생성 시점엔 뷰가 아직 없음).
        public void SetView(LOPEntityView view)
        {
            this.view = view;
        }

        // 렌더되는 모델 GameObject. 뷰가 async 로드 전이거나 파괴됐으면 null.
        public GameObject visualGameObject => view != null ? view.visualGameObject : null;
    }
}
