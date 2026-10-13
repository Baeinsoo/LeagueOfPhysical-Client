using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LOP
{
    /// <summary>
    /// 터널에 내 치비를 세우고 장착 룩을 입힌다. 몸·얼굴 재질은 경기 화면과 같은 어드레서블.
    /// 룩이 몸보다 먼저 오면 들고 있다가 몸이 붙을 때 입힌다. 몸을 못 받으면 터널만 남는다.
    /// </summary>
    public sealed class LobbyPlayer : MonoBehaviour
    {
        public const string ChibiKey = "Assets/Characters/Chibi/Chibi.prefab";
        public const string FaceKey = "Assets/Characters/Chibi/Materials/ChibiFace.mat";
        public static readonly Vector3 StandPosition = new Vector3(0f, 0f, 4f);

        private string _userId = "lobby";
        private CosmeticCatalog _catalog;
        private Material _face;
        private PlayerLook _look;
        private AsyncOperationHandle<GameObject> _bodyHandle;
        private AsyncOperationHandle<Material> _faceHandle;

        public GameObject Visual { get; private set; }
        public PlayerLook AppliedLook { get; private set; }

        public void Init(string userId, CosmeticCatalog catalog)
        {
            if (!string.IsNullOrEmpty(userId)) _userId = userId;
            _catalog = catalog;
            _faceHandle = Addressables.LoadAssetAsync<Material>(FaceKey);
            _faceHandle.Completed += h =>
            {
                if (this == null) return;   // 로비를 떠난 뒤 도착
                if (h.Status == AsyncOperationStatus.Succeeded) AttachFace(h.Result);
                else Debug.LogWarning("[LobbyPlayer] 얼굴 재질을 못 받았다 — 표정 없이 선다: " + FaceKey);
            };
            _bodyHandle = Addressables.LoadAssetAsync<GameObject>(ChibiKey);
            _bodyHandle.Completed += h =>
            {
                if (this == null) return;
                if (h.Status != AsyncOperationStatus.Succeeded || h.Result == null)
                {
                    Debug.LogWarning("[LobbyPlayer] 치비를 못 받았다 — 선수 없이 터널만: " + ChibiKey);
                    return;
                }
                AttachVisual(Instantiate(h.Result, transform));
            };
        }

        public void SetLook(PlayerLook look)
        {
            _look = look;
            Dress();
        }

        public void AttachVisual(GameObject visual)
        {
            Visual = visual;
            visual.transform.position = StandPosition;
            visual.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // 카메라(-z 쪽)를 본다
            Dress();
        }

        public void AttachFace(Material face)
        {
            _face = face;
            Dress();
        }

        private void Dress()
        {
            if (Visual == null) return;
            if (ChibiDresser.IsChibi(Visual)) ChibiDresser.Dress(_userId, Visual, _face, _look, _catalog);
            AppliedLook = _look;
        }

        private void OnDestroy()
        {
            if (_bodyHandle.IsValid()) Addressables.Release(_bodyHandle);
            if (_faceHandle.IsValid()) Addressables.Release(_faceHandle);
        }
    }
}
