using UnityEditor;

[CustomEditor(typeof(GarageButtonGraphic))]
[CanEditMultipleObjects]
public class GarageButtonGraphicEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("artwork"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("muted"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Color"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_RaycastTarget"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Maskable"));
        serializedObject.ApplyModifiedProperties();
    }
}
