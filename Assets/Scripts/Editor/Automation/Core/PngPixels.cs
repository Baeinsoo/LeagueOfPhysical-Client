using UnityEngine;

namespace LOP.EditorTools.Automation
{
    /// <summary>PNG ↔ 픽셀(텍스처 순서). 잠깐 만든 텍스처는 바로 지운다 — 비교를 수백 번 돌려도 메모리가 쌓이지 않게.</summary>
    public static class PngPixels
    {
        public static bool TryDecode(byte[] png, out int width, out int height, out Color32[] pixels)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (png == null || !texture.LoadImage(png, false))
                {
                    width = height = 0;
                    pixels = null;
                    return false;
                }
                width = texture.width;
                height = texture.height;
                pixels = texture.GetPixels32();
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        public static byte[] Encode(int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false);
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
