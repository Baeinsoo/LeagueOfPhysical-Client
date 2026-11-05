using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LOP.CharacterEditor
{
    /// <summary>
    /// 선수 몸(치비 뼈대에 옮겨 입힌 RodinA) 프리팹을 처음부터 만든다(몇 번 돌려도 같은 결과). 스크립트는 붙이지 않는다 — 서버도 로드한다.
    /// 애니메이션은 PolyOne 치비 것을 그대로 쓴다(휴머노이드 근육 곡선이라 뼈 길이가 달라도 맞는다).
    /// </summary>
    public static class ChibiPrefabBuilder
    {
        public const string PrefabPath = "Assets/Characters/Chibi/Chibi.prefab";
        private const string Dir = "Assets/Characters/Chibi";
        private const string Anim = "Assets/Art/PolyOne/Chibi Character/Animation/";
        private const string Rodin = "Assets/Art/Characters/RodinA";
        private const string Model = Rodin + "/RodinA.fbx";
        private const string Detail = Rodin + "/Textures/RodinA_Detail.png";
        private const string FaceAtlas = Rodin + "/Textures/RodinA_FaceAtlas.png";

        //  옛 PolyOne 치비와 같은 키가 되는 배율 — 둘 다 모델 정수리가 0.4753이라 0.4753 × 3 ≈ 1.43m(캡슐 1.5m).
        private const float ModelScale = 3f;

        //  모자 자리 — 바보털을 뺀 머리카락 정수리(모델 단위, 기본 자세). 머리 정점 높이 상위 1%가 0.467이고 그 높이의 앞뒤 가운데가 −0.026이다.
        private static readonly Vector3 HatSpotInModel = new Vector3(0f, 0.468f, -0.026f);

        [MenuItem("LOP/Chibi/Build Prefab")]
        public static void Build()
        {
            Directory.CreateDirectory(Rodin + "/Materials");
            var toon = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/LOP/LOPToon.shader");

            //  영역 색 × 흑백 디테일(주름·그늘). 디테일은 sRGB를 끈 선형 값이다.
            var body = new Material(toon);
            body.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Detail));
            body.SetFloat("_UseRegions", 1f);
            body.SetShaderPassEnabled("SRPDefaultUnlit", true);   // 외곽선
            body = Save(Rodin + "/Materials/Toon_RodinA_Body.mat", body);   // 값을 다 넣은 뒤에 에셋으로 만든다

            //  얼굴 면은 UV가 표정 그림용이라 디테일을 깔면 엉뚱한 무늬가 나온다 — 피부 영역 색만 칠한다.
            var faceSkin = new Material(toon);
            faceSkin.SetFloat("_UseRegions", 1f);
            faceSkin.SetShaderPassEnabled("SRPDefaultUnlit", false);   // 얼굴 면 테두리에 선이 그어지지 않게
            faceSkin = Save(Rodin + "/Materials/Toon_RodinA_FaceSkin.mat", faceSkin);

            //  피부 위에 표정 칸을 겹쳐 그린다(ChibiFace가 칸을 고른다). 기본 칸 = 보통 얼굴.
            var faceDecal = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/LOP/LOPToonDecal.shader"));
            faceDecal.SetTexture("_FaceMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FaceAtlas));
            Vector4 normal = LOP.ChibiFaceAtlas.CellST(LOP.ChibiExpression.Normal);
            faceDecal.SetTextureScale("_FaceMap", new Vector2(normal.x, normal.y));
            faceDecal.SetTextureOffset("_FaceMap", new Vector2(normal.z, normal.w));
            faceDecal = Save(Rodin + "/Materials/RodinA_Face.mat", faceDecal);

            var controller = BuildController(Dir + "/ChibiArcher.controller");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            //  뿌리는 크기 1 — 쓰는 화면(로비 자리·거인·경기)이 뿌리 크기를 정한다. 모델 배율은 자식 Model이 맡는다.
            var go = new GameObject("Chibi");
            var modelGo = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(modelGo, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            modelGo.name = "Model";
            modelGo.transform.SetParent(go.transform, false);
            modelGo.transform.localScale = Vector3.one * ModelScale;
            foreach (var smr in modelGo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                //  서브메시 0 = 몸, 1 = 얼굴 면. 재질이 서브메시보다 하나 많으면 마지막 서브메시(얼굴)를 한 번 더 그린다 — 거기에 표정을 얹는다.
                smr.sharedMaterials = new[] { body, faceSkin, faceDecal };
            }
            var animator = modelGo.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            //  모자·머리 위 카드·별은 정수리 자리를, 철썩 카드는 얼굴 자리를 따른다(ChibiHeadSpots). 위 = 세상 위, 앞 = 몸 앞(기본 자세 기준).
            AddHeadSpot(animator, LOP.ChibiHeadSpots.HatAnchorName, HatSpotInModel, Quaternion.identity);
            var faceFront = FaceFrontInModel(modelGo.GetComponentInChildren<SkinnedMeshRenderer>(true));
            AddHeadSpot(animator, LOP.ChibiHeadSpots.FaceAnchorName, faceFront, Quaternion.identity);

            //  지우지 않고 덮어쓴다 — 지우면 GUID가 바뀌어 원격 에셋(Addressables) 등록이 끊긴다(서버·클라가 치비를 못 찾는다).
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            Debug.Log("[Chibi] 프리팹 완료: " + PrefabPath);
        }

        /// <summary>
        /// 머리 뼈 아래에 연출 자리(정수리·얼굴 앞)를 둔다. 위치·방향은 모델(기본 자세) 좌표.
        /// 머리 뼈를 못 찾으면(휴머노이드가 아니거나 Head 매핑이 빠짐) 멈춘다 — 그냥 두면 자리가 프리팹 밖에 생겨 조용히 빠진다.
        /// </summary>
        public static Transform AddHeadSpot(Animator animator, string name, Vector3 positionInModel, Quaternion rotationInModel)
        {
            var head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null)
            {
                throw new System.InvalidOperationException("[Chibi] 머리 뼈를 못 찾았다 — 모델 아바타가 휴머노이드이고 Head가 매핑됐는지 본다: " + Model);
            }
            var model = animator.transform;
            var spot = new GameObject(name).transform;
            spot.SetParent(head, false);
            spot.SetPositionAndRotation(model.TransformPoint(positionInModel), model.rotation * rotationInModel);
            return spot;
        }

        //  얼굴 그림 면(서브메시 1)의 앞 가운데 — 높이·좌우는 면의 가운데, 앞뒤는 가장 앞(기본 자세, 모델 좌표).
        private static Vector3 FaceFrontInModel(SkinnedMeshRenderer body)
        {
            var baked = new Mesh();
            body.BakeMesh(baked, true);
            var verts = baked.vertices;
            var model = body.GetComponentInParent<Animator>().transform;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (int i in baked.GetTriangles(1))
            {
                var p = model.InverseTransformPoint(body.transform.TransformPoint(verts[i]));
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            Object.DestroyImmediate(baked);
            return new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, max.z);
        }

        private static AnimatorController BuildController(string path)
        {
            AssetDatabase.DeleteAsset(path);
            var c = AnimatorController.CreateAnimatorControllerAtPath(path);
            c.AddParameter("Run", AnimatorControllerParameterType.Bool);
            c.AddParameter("Hit", AnimatorControllerParameterType.Trigger);   // LOPEntityView가 부른다 — 연결 없음(경고만 막는다)
            c.AddParameter("Happy", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Sad", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Falling", AnimatorControllerParameterType.Bool);   // 스카이다이브 — 공중이면 켠다
            var sm = c.layers[0].stateMachine;
            var idle = sm.AddState("Idle");
            idle.motion = LoopingCopy("Idle", Dir + "/Anim/Idle.anim");   // 원본은 루프가 꺼져 있어 한 번 돌고 멈춘다
            var run = sm.AddState("Run");
            run.motion = LoopingCopy("Run", Dir + "/Anim/Run.anim");
            var happy = sm.AddState("Happy");
            happy.motion = Clip("Happy");
            var sad = sm.AddState("Sad");
            sad.motion = Clip("Sad");
            var fall = sm.AddState("Fall");
            fall.motion = LoopingCopy("Jumping Down", Dir + "/Anim/Fall.anim");
            sm.defaultState = idle;
            foreach (var from in new[] { idle, run })
            {
                var t = from.AddTransition(fall);
                t.AddCondition(AnimatorConditionMode.If, 0, "Falling");
                t.duration = 0.15f;
            }
            var land = fall.AddTransition(idle);
            land.AddCondition(AnimatorConditionMode.IfNot, 0, "Falling");
            land.duration = 0.15f;
            idle.AddTransition(run).AddCondition(AnimatorConditionMode.If, 0, "Run");
            run.AddTransition(idle).AddCondition(AnimatorConditionMode.IfNot, 0, "Run");
            foreach (var (state, trigger) in new[] { (happy, "Happy"), (sad, "Sad") })
            {
                var enter = sm.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0, trigger);
                enter.canTransitionToSelf = false;
                enter.duration = 0.1f;
                var back = state.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = 1f;
                back.duration = 0.2f;
            }
            return c;
        }

        //  PolyOne 원본 클립은 루프가 아니다 — 원본은 두고 루프를 켠 복사본을 만든다.
        private static AnimationClip LoopingCopy(string source, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var copy = Object.Instantiate(Clip(source));
            copy.name = Path.GetFileNameWithoutExtension(path);
            var settings = AnimationUtility.GetAnimationClipSettings(copy);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        private static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(Anim + name + ".anim");

        //  이미 있으면 그 재질에 값만 옮긴다(GUID 유지).
        private static Material Save(string path, Material m)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(m, path);
                return m;
            }
            existing.shader = m.shader;
            existing.CopyPropertiesFromMaterial(m);
            existing.SetShaderPassEnabled("SRPDefaultUnlit", m.GetShaderPassEnabled("SRPDefaultUnlit"));
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(m);
            return existing;
        }
    }
}
