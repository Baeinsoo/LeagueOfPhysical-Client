using UnityEngine;
using TMPro;

namespace LOP
{
    public class IndividualTextOutline : MonoBehaviour
    {
        [Header("Outline Settings")]
        [SerializeField]
        private Color outlineColor = Color.black;

        [SerializeField]
        [Range(0f, 1f)]
        private float outlineWidth = 0.2f;

        private TextMeshProUGUI textComponent;
        private Material individualMaterial;

        private void Start()
        {
            SetupIndividualMaterial();
            ApplyOutlineSettings();
        }

        private void SetupIndividualMaterial()
        {
            textComponent = GetComponent<TextMeshProUGUI>();
            if (textComponent == null)
            {
                Debug.LogError("TextMeshProUGUI component not found!");
                return;
            }

            if (textComponent.fontSharedMaterial != null)
            {
                individualMaterial = new Material(textComponent.fontSharedMaterial);
                textComponent.fontSharedMaterial = individualMaterial;
            }
        }

        private void ApplyOutlineSettings()
        {
            if (individualMaterial == null)
            {
                return;
            }

            individualMaterial.SetColor("_OutlineColor", outlineColor);
            individualMaterial.SetFloat("_OutlineWidth", outlineWidth);
            individualMaterial.SetFloat("_OutlineSoftness", 0f);
        }

        public void SetOutlineColor(Color color)
        {
            outlineColor = color;
            if (individualMaterial != null)
                individualMaterial.SetColor("_OutlineColor", outlineColor);
        }

        public void SetOutlineWidth(float width)
        {
            outlineWidth = Mathf.Clamp01(width);
            if (individualMaterial != null)
                individualMaterial.SetFloat("_OutlineWidth", outlineWidth);
        }

        private void OnDestroy()
        {
            if (individualMaterial != null)
            {
                DestroyImmediate(individualMaterial);
                individualMaterial = null;
            }
        }
    }
}
