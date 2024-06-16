using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LOP.Editor
{
    [CustomEditor(typeof(MatchStateMachine))]
    public class MatchStateMachineEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var matchStateMachine = target as MatchStateMachine;

            if (EditorApplication.isPlaying)
            {
                var text = $"currentState: {matchStateMachine.currentState.GetType()}";

                EditorGUILayout.LabelField(text);
            }
            else
            {
                DrawDefaultInspector();

                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
