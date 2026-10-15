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
            //  상점을 열면 진행도·장착이 한 프레임에 연달아 바뀌어 같은 룩이 두 번 온다. 다시 입히지 않는다 —
            //  플레이 중 옛 모자는 프레임 끝에야 지워져서, 같은 프레임에 또 입히면 모자가 하나씩 쌓인다.
            if (AppliedLook != null && SameSlots(AppliedLook, look))
            {
                _look = look;
                return;
            }
            _look = look;
            Dress();
        }

        private static bool SameSlots(PlayerLook a, PlayerLook b)
        {
            if (b == null || a.Slots.Count != b.Slots.Count) return false;
            foreach (var kv in a.Slots)
                if (b.SlotOrNull(kv.Key) != kv.Value) return false;
            return true;
        }

        public void AttachVisual(GameObject visual)
        {
            Visual = visual;
            visual.transform.position = StandPosition;
            visual.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // 카메라(-z 쪽)를 본다
            Dress();
        }

        /// <summary>출전 중 자세 — 출구 쪽으로 돌아서 달린다. 몸이 아직 없으면 아무것도 하지 않는다.</summary>
        public void PoseRunOut(float z, float yaw, bool running)
        {
            if (Visual == null) return;
            Visual.transform.position = new Vector3(StandPosition.x, StandPosition.y, z);
            Visual.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            SetRun(running);
        }

        /// <summary>출전을 되돌린다(방 접속 실패) — 제자리에서 카메라를 본다.</summary>
        public void ResetRunOut()
        {
            if (Visual == null) return;
            Visual.transform.position = StandPosition;
            Visual.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            SetRun(false);
        }

        private void SetRun(bool on)
        {
            var animator = Visual.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var p in animator.parameters)
            {
                if (p.name != "Run" || p.type != AnimatorControllerParameterType.Bool) continue;
                animator.SetBool("Run", on);
                return;
            }
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
