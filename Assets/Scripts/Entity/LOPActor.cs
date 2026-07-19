using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPActor : MonoBehaviour
    {
        public string entityId { get; private set; }

        private LOPEntityView view;

        // 스포너가 뷰를 만든 뒤 등록한다(Actor.Awake 시점엔 뷰가 아직 없음).
        public void SetView(LOPEntityView view)
        {
            this.view = view;
        }

        // 렌더되는 모델 GameObject. 뷰가 async 로드 전이거나 파괴됐으면 null.
        // 외부는 이 대표 표면만 읽는다(gameObject.transform 식 위임 접근자).
        public GameObject visualGameObject => view != null ? view.visualGameObject : null;

        public virtual void Initialize<TEntityCreationData>(TEntityCreationData creationData)
            where TEntityCreationData : struct, IEntityCreationData
        {
            entityId = creationData.entityId;
        }
    }
}
