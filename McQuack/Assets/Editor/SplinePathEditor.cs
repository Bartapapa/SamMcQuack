#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SplinePath))]
public class SplinePathEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);

        SplinePath spline =
            (SplinePath)target;

        if (GUILayout.Button("Add Point"))
        {
            Undo.RecordObject(
                spline,
                "Add Spline Point");

            spline.AddPoint();

            EditorUtility.SetDirty(spline);
        }

        if (GUILayout.Button("Rebuild Cache"))
        {
            spline.RebuildCache();

            EditorUtility.SetDirty(spline);
        }

        if (GUILayout.Button("Clear Points"))
        {
            if (EditorUtility.DisplayDialog(
                "Clear Spline",
                "Delete all spline points?",
                "Delete",
                "Cancel"))
            {
                Undo.RecordObject(
                    spline,
                    "Clear Spline");

                spline.ClearPoints();

                EditorUtility.SetDirty(spline);
            }
        }
    }
}
#endif