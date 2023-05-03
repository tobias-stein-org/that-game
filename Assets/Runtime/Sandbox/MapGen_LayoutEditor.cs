using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MapGen_Layout))]
class MapGen_LayoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var instance = target as MapGen_Layout;
        GUI.enabled = instance.done;
        {
            if(Application.isPlaying && GUILayout.Button("Reset")) { instance.init(); }
        }
        GUI.enabled = true;
    }
}