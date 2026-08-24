using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 유령(맵에 부딪혀 멈춘) 상태를 눈에 띄는 색으로 보여 준다. 남이 이유 없이 멈춘 것처럼 보이지 않게
    /// 하는 최소한의 연출이라, 상태 판단은 하지 않고 받은 값을 그대로 그린다.
    /// </summary>
    public class GhostAppearance : MonoBehaviour, ICleanup
    {
        // 원래는 반투명(알파)을 노렸다. 하지만 URP + 새(bird) FBX에 딸려 온 머티리얼은 기본값이
        // Surface Type = Opaque라, 그 상태에서는 알파 값이 통째로 무시돼 색만 있는 그대로 불투명하게
        // 그려진다. 진짜 투명을 내려면 _Surface/_SrcBlend/_DstBlend/_ZWrite/renderQueue와 키워드까지
        // 갈아야 하는데, 이건 아트가 소유한 머티리얼 에셋을 건드리는 일이라 이번 슬라이스에서는 하지
        // 않기로 했다. 대신 알파 없이도 "지금 이 새는 경기에서 빠진 상태"임이 확실히 읽히는 색을 쓴다.
        //
        // 채도 낮은 청회색을 쓴다 — "색이 빠진(drained)" 느낌이라 업계 관용의 "비활성/스턴" 표시와
        // 맞고, 이 코스의 초록·갈색 배경과도 확실히 구분된다. 채도 높은 원색(특히 마젠타 계열)은
        // 쓰지 않는다 — Unity에서 마젠타는 "머티리얼/셰이더 유실" 에러 색으로 굳어 있어서, 새가 그
        // 색으로 번쩍이면 "경기에서 빠졌다"가 아니라 "이 에셋이 고장났다"로 읽힌다(서버 에디터가
        // 아트 파이프라인이 없어 씬 전체를 이 마젠타로 그리는 게 실제 사례).
        private static readonly Color GhostColor = new Color(0.6f, 0.6f, 0.7f);

        private LOPActor actor;

        // 지금 renderers/originalMaterials를 뽑아 온 모델. actor.visualGameObject가 이 값과
        // 달라지면(비주얼이 새로 로드됐거나 통째로 교체됐다는 뜻) 다시 뽑아야 한다 — 안 그러면
        // 사라진 옛 모델의 렌더러 배열을 계속 붙잡고 아무것도 안 그려진다.
        private GameObject capturedVisual;

        private Renderer[] renderers;

        // 원본 = 공유 에셋 그대로(복제 아님). sharedMaterial은 읽어도 인스턴스를 만들지 않으므로,
        // 한 번도 유령이 안 되는 캐릭터(FlapWang 등)는 이 컴포넌트를 달고 있어도 머티리얼 복제가
        // 전혀 안 생긴다.
        private Material[] originalMaterials;

        // 유령 전용 복제본. 실제로 유령이 되는 첫 순간에만 만들고, 그 뒤로는 재사용한다 —
        // 새는 레이스에서 여러 번 부딪히므로 매번 새로 만들면 계속 새는(leak) 셈이 된다.
        private Material[] ghostMaterials;

        private bool applied;

        public void SetEntity(LOPActor actor)
        {
            this.actor = actor;
        }

        // 렌더러는 모델이 실제로 붙은 뒤에야(pull) 뽑는다 — SetEntity가 불리는 시점엔 앵커
        // GameObject만 있고, 눈에 보이는 메시는 LOPEntityView.Start()가 Addressables로 비동기
        // 로드한 뒤에야 자식으로 붙는다(LOPEntityView.UpdateRunAnimation과 같은 pull 패턴). 여기서
        // SetEntity 시점에 바로 GetComponentsInChildren을 부르면 길이 0 배열만 붙잡고 끝난다.
        private bool TryResolveRenderers()
        {
            GameObject visual = actor != null ? actor.visualGameObject : null;
            if (visual == capturedVisual)
            {
                return renderers != null && renderers.Length > 0;
            }

            // 모델이 바뀌었다(처음 로드됐거나, visualId가 바뀌어 통째로 교체됐거나) — 옛 렌더러가
            // 가리키던 머티리얼은 더 이상 화면에 없으므로 유령 복제본부터 정리하고 새 모델 기준으로
            // 다시 뽑는다.
            ReleaseGhostMaterials();
            capturedVisual = visual;
            applied = false;

            if (visual == null)
            {
                renderers = null;
                originalMaterials = null;
                return false;
            }

            renderers = visual.GetComponentsInChildren<Renderer>(true);
            originalMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalMaterials[i] = renderers[i].sharedMaterial;
            }
            return renderers.Length > 0;
        }

        public void SetGhost(bool ghost)
        {
            if (TryResolveRenderers() == false || ghost == applied)
            {
                return;
            }
            applied = ghost;

            if (ghost && ghostMaterials == null)
            {
                ghostMaterials = new Material[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                {
                    ghostMaterials[i] = new Material(originalMaterials[i]) { color = GhostColor };
                }
            }

            Material[] target = ghost ? ghostMaterials : originalMaterials;
            for (int i = 0; i < renderers.Length; i++)
            {
                // sharedMaterial "쓰기"는 그 렌더러가 참조할 에셋을 바꿀 뿐 복제를 유발하지 않는다
                // (복제는 오직 .material을 "읽을" 때만 일어난다) — 원래대로 되돌릴 때도 복사본이
                // 아니라 진짜 원본 레퍼런스로 되돌아간다.
                renderers[i].sharedMaterial = target[i];
            }
        }

        private void ReleaseGhostMaterials()
        {
            if (ghostMaterials == null)
            {
                return;
            }
            for (int i = 0; i < ghostMaterials.Length; i++)
            {
                Destroy(ghostMaterials[i]);
            }
            ghostMaterials = null;
        }

        public void Cleanup()
        {
            SetGhost(false);
            ReleaseGhostMaterials();
        }
    }
}
