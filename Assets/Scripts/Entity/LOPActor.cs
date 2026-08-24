using UnityEngine;

namespace LOP
{
    /// <summary>클라 쪽 엔티티 몸의 신원 + 그 몸이 쓰는 뷰로 가는 길.</summary>
    public class LOPActor : EntityActor
    {
        private LOPEntityView view;

        // 스포너가 뷰를 만든 뒤 등록한다(Actor 생성 시점엔 뷰가 아직 없음).
        public void SetView(LOPEntityView view)
        {
            this.view = view;
        }

        // 렌더되는 모델 GameObject. 뷰가 async 로드 전이거나 파괴됐으면 null.
        public GameObject visualGameObject => view != null ? view.visualGameObject : null;
    }
}
