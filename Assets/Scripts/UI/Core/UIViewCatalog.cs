using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>View 타입 이름 → UXML/USS 매핑. 디자이너가 에디터에서 편집하는 불변 설정 데이터.</summary>
    [CreateAssetMenu(fileName = "UIViewCatalog", menuName = "LOP/UI/UIViewCatalog")]
    public class UIViewCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string viewName;          // typeof(TView).Name
            public VisualTreeAsset uxml;
            public StyleSheet uss;            // 선택(없으면 null)
        }

        [SerializeField] private List<Entry> entries = new();

        public bool TryGet(string viewName, out Entry entry)
        {
            foreach (var e in entries)
            {
                if (e.viewName == viewName)
                {
                    entry = e;
                    return true;
                }
            }
            entry = default;
            return false;
        }
    }
}
