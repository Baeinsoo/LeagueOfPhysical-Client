using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 코드로 만드는 소품·관중·맵 재질. 외곽선은 캐릭터(치비 사수)만이라 여기서는 끈다.
    /// 이름으로 찾으니 Graphics ▸ Always Included Shaders에 있어야 폰에서 분홍이 안 된다(RuntimeShaderInclusionTests).
    /// </summary>
    public static class LOPToonMaterials
    {
        public static Material Create(Color color)
        {
            //  이름은 상수로 빼지 않고 글자 그대로 둔다 — RuntimeShaderInclusionTests가 소스의 글자를 찾아 포함 여부를 검사한다.
            var material = new Material(Shader.Find("LOP/Toon"));
            material.SetColor("_BaseColor", color);
            material.SetShaderPassEnabled("SRPDefaultUnlit", false);
            return material;
        }
    }
}
