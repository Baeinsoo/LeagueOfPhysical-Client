using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 테마 소품 메시·재질 묶음. 게임 씬이 직접 참조해야 빌드에 셰이더가 따라간다(이름으로 찾으면 APK에서 빠질 수 있다).
    /// `LOP/Dodge/Build Prop Kit`이 만든다.
    /// </summary>
    [CreateAssetMenu(menuName = "LOP/Dodge Prop Kit")]
    public class DodgePropKit : ScriptableObject
    {
        public Mesh slipperMesh, jarMesh, melonMesh;
        public Material[] slipperMaterials;
        public Material jarMaterial, melonMaterial, ropeMaterial;
        [Header("바닥 층")]
        public Material warn, shadow, juice, hot, tileWarm, tileHot;
    }
}
