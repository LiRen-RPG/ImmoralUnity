using UnityEditor;
using UnityEngine;
using Immortal.Controllers;

namespace Immortal.Editor
{
    [CustomEditor(typeof(LShapeComponent))]
    public class LShapeComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var widthProp       = serializedObject.FindProperty("width");
            var depthProp       = serializedObject.FindProperty("depth");
            var totalLengthProp = serializedObject.FindProperty("totalLength");

            EditorGUILayout.PropertyField(widthProp);

            EditorGUILayout.PropertyField(depthProp);
            EditorGUILayout.PropertyField(totalLengthProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
