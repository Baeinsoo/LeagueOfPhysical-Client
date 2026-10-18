using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LOP
{
    /// <summary>
    /// 터널에 내 치비를 세우고 장착 룩을 입힌다. 몸·얼굴 재질은 경기 화면과 같은 어드레서블.
    /// 룩이 몸보다 먼저 오면 들고 있다가 몸이 붙을 때 입힌다. 몸을 못 받으면 터널만 남는다.
    /// </summary>
    public sealed class LobbyPlayer : MonoBehaviour, IPodiumFigure
    {
        public const string ChibiKey = "Assets/Characters/Chibi/Chibi.prefab";
        public const string FaceKey = "Assets/Characters/Chibi/Materials/ChibiFace.mat";
        public static readonly Vector3 StandPosition = new Vector3(0f, 0f, 4f);

        private CosmeticCatalog _catalog;
        private AppearanceColorPalette _palette;
        private Material _face;
        private PlayerLook _look;
        private AsyncOperationHandle<GameObject> _bodyHandle;
        private AsyncOperationHandle<Material> _faceHandle;

        private Vector3 _standPosition = StandPosition;
        private float _standYaw = 180f;   // 카메라(-z 쪽)를 본다
        private float _standScale = 1f;
        //  몸 프리팹 뿌리의 원래 크기(치비는 3) — 자리 크기는 이 위에 곱한다.
        private Vector3 _bodyScale = Vector3.one;
        private ResultReaction _pendingReaction = ResultReaction.None;
        private bool _absent;
        private bool _greyed;   // 지금 회색이 칠해져 있나 — 칠한 적 없으면 블록을 건드리지 않는다

        public GameObject Visual { get; private set; }
        public PlayerLook AppliedLook { get; private set; }
        public bool HasBody => Visual != null;
        public ResultReaction LastReaction { get; private set; }

        /// <summary>반응 트리거를 실제로 걸었나 — 컨트롤러에 그 트리거가 없으면 false로 남는다.</summary>
        public bool ReactionApplied { get; private set; }

        /// <summary>반응 트리거를 건 횟수(기뻐하는 선수는 되풀이한다).</summary>
        public int ReactionTriggers { get; private set; }

        /// <summary>기뻐하는 선수가 기쁨 동작을 다시 하는 간격(동작 2초 + 쉼).</summary>
        public const float CelebrateEverySeconds = 3f;

        private bool _celebrating;
        private float _celebrateTimer;

        public void Init(CosmeticCatalog catalog, AppearanceColorPalette palette)
        {
            _catalog = catalog;
            _palette = palette;
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
            if (AppliedLook != null && SameLook(AppliedLook, look))
            {
                _look = look;
                return;
            }
            _look = look;
            Dress();
        }

        private static bool SameLook(PlayerLook a, PlayerLook b)
        {
            if (b == null || a.Slots.Count != b.Slots.Count || a.CharacterAppearance != b.CharacterAppearance) return false;
            foreach (var kv in a.Slots)
                if (b.SlotOrNull(kv.Key) != kv.Value) return false;
            return true;
        }

        public void AttachVisual(GameObject visual)
        {
            Visual = visual;
            _bodyScale = visual.transform.localScale;
            PlaceVisual();
            Dress();
            if (_pendingReaction != ResultReaction.None)
            {
                ApplyReaction(_pendingReaction);
                _pendingReaction = ResultReaction.None;
            }
        }

        /// <summary>설 자리 — 로비는 기본값(터널 가운데)을 쓰고, 결과 시상대는 단마다 다른 자리를 준다.</summary>
        public void Stand(Vector3 position, float yaw, float scale)
        {
            _standPosition = position;
            _standYaw = yaw;
            _standScale = scale;
            if (Visual != null) PlaceVisual();
        }

        /// <summary>한 번만 — 동작이 끝나면 컨트롤러가 서 있기로 돌려보낸다. 몸이 없으면 붙을 때 건다.</summary>
        public void React(ResultReaction reaction)
        {
            LastReaction = reaction;
            if (Visual == null)
            {
                _pendingReaction = reaction;
                return;
            }
            ApplyReaction(reaction);
        }

        public void SetAbsent(bool absent)
        {
            _absent = absent;
            ApplyAbsent();
        }

        private void PlaceVisual()
        {
            Visual.transform.position = _standPosition;
            Visual.transform.rotation = Quaternion.Euler(0f, _standYaw, 0f);
            Visual.transform.localScale = _bodyScale * _standScale;
        }

        private void ApplyReaction(ResultReaction reaction)
        {
            ApplyFace();
            if (reaction == ResultReaction.None) return;
            _celebrating = reaction == ResultReaction.Happy;
            _celebrateTimer = 0f;
            FireTrigger(reaction == ResultReaction.Happy ? "Happy" : "Sad");
        }

        private void FireTrigger(string trigger)
        {
            var animator = Visual.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var p in animator.parameters)
            {
                if (p.name != trigger || p.type != AnimatorControllerParameterType.Trigger) continue;
                animator.SetTrigger(trigger);
                ReactionApplied = true;
                ReactionTriggers++;
                return;
            }
        }

        //  결과 반응 표정은 판이 떠 있는 동안 유지한다(환호 / 낙담, 덤덤은 기본 표정 그대로). 얼굴은 입히기 뒤에 생겨 그때도 다시 건다.
        private void ApplyFace()
        {
            if (Visual == null || LastReaction == ResultReaction.None) return;
            var face = Visual.GetComponent<ChibiFace>();
            if (face == null) return;
            face.SetReaction(LastReaction == ResultReaction.Happy ? ChibiExpression.Cheer : ChibiExpression.Despair);
        }

        /// <summary>기뻐하는 선수는 간격마다 기쁨 동작을 다시 한다. 매 프레임 Update가 부른다(시험은 직접 부른다).</summary>
        public void TickCelebration(float dt)
        {
            if (!_celebrating || Visual == null) return;
            _celebrateTimer += dt;
            if (_celebrateTimer < CelebrateEverySeconds) return;
            _celebrateTimer = 0f;
            FireTrigger("Happy");
        }

        private void Update() => TickCelebration(Time.deltaTime);

        //  나간 선수는 어둡게 — 툰 셰이더의 바탕색에 곱한다. 그 속성이 없는 재질은 그대로 둔다.
        //  블록은 옷 색(ChibiOutfit)도 함께 쓰므로 지우지 않고 _BaseColor 한 칸만 바꾼다.
        private void ApplyAbsent()
        {
            if (Visual == null || !_absent && !_greyed) return;
            var block = new MaterialPropertyBlock();
            foreach (var r in Visual.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null || !m.HasProperty("_BaseColor")) continue;
                r.GetPropertyBlock(block);
                var baseColor = m.GetColor("_BaseColor");
                block.SetColor("_BaseColor", _absent ? baseColor * new Color(0.45f, 0.45f, 0.5f, 1f) : baseColor);
                r.SetPropertyBlock(block);
            }
            _greyed = _absent;
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
            PlaceVisual();
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
            if (ChibiDresser.IsChibi(Visual)) ChibiDresser.Dress(Visual, _face, _look, _catalog, _palette);
            AppliedLook = _look;
            ApplyAbsent();   // 입히기가 재질을 바꿔도 회색이 남게
            ApplyFace();
        }

        private void OnDestroy()
        {
            if (_bodyHandle.IsValid()) Addressables.Release(_bodyHandle);
            if (_faceHandle.IsValid()) Addressables.Release(_faceHandle);
        }
    }
}
