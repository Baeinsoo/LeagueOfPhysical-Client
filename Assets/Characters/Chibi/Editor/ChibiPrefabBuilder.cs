using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LOP.CharacterEditor
{
    /// <summary>한 발 승부 사수 치비 프리팹을 처음부터 만든다(몇 번 돌려도 같은 결과). 스크립트는 붙이지 않는다 — 서버도 로드한다.</summary>
    public static class ChibiPrefabBuilder
    {
        public const string PrefabPath = "Assets/Characters/Chibi/Chibi.prefab";
        private const string Dir = "Assets/Characters/Chibi";
        private const string Root = "Assets/Art/PolyOne/Chibi Character";
        private const string Model = Root + "/Model/SM_Chibi_Character.fbx";
        private const string Texture = Root + "/Texture/Texture_ChibiCharacter_BaseColor.png";
        private const string Anim = Root + "/Animation/";
        private const string RegionsMesh = Dir + "/Mesh/SM_Chibi_Regions.asset";
        private const string FaceAtlas = Dir + "/Face/FaceAtlas.png";

        [MenuItem("LOP/Chibi/Build Prefab")]
        public static void Build()
        {
            Directory.CreateDirectory(Dir + "/Materials");
            var body = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/LOP/LOPToon.shader"));
            body.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Texture));
            body.SetFloat("_UseRegions", 1f);
            body.SetShaderPassEnabled("SRPDefaultUnlit", true);   // 외곽선은 사수만
            body = Save(Dir + "/Materials/Toon_ChibiBody.mat", body);   // 값을 다 넣은 뒤에 에셋으로 만든다

            var face = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/LOP/LOPToonDecal.shader"));
            face.SetTexture("_FaceMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FaceAtlas));
            Save(Dir + "/Materials/ChibiFace.mat", face);

            var controller = BuildController(Dir + "/ChibiArcher.controller");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            go.name = "Chibi";
            go.transform.localScale = Vector3.one * 3f;   // 모델 키 0.49m → 약 1.47m(캡슐 1.5m)
            var regions = AssetDatabase.LoadAssetAtPath<Mesh>(RegionsMesh);
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.sharedMaterial = body;
                if (smr.name == "SM_Chibi_Body") { smr.sharedMesh = regions; }
            }
            var animator = go.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            AssetDatabase.DeleteAsset(PrefabPath);
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            Debug.Log("[Chibi] 프리팹 완료: " + PrefabPath);
        }

        private static AnimatorController BuildController(string path)
        {
            AssetDatabase.DeleteAsset(path);
            var c = AnimatorController.CreateAnimatorControllerAtPath(path);
            c.AddParameter("Run", AnimatorControllerParameterType.Bool);
            c.AddParameter("Hit", AnimatorControllerParameterType.Trigger);   // LOPEntityView가 부른다 — 연결 없음(경고만 막는다)
            c.AddParameter("Happy", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Sad", AnimatorControllerParameterType.Trigger);
            var sm = c.layers[0].stateMachine;
            var idle = sm.AddState("Idle");
            idle.motion = Clip("Idle");
            var run = sm.AddState("Run");
            run.motion = Clip("Run");
            var happy = sm.AddState("Happy");
            happy.motion = Clip("Happy");
            var sad = sm.AddState("Sad");
            sad.motion = Clip("Sad");
            sm.defaultState = idle;
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

        private static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(Anim + name + ".anim");

        private static Material Save(string path, Material m)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
