using UnityEngine;
using UnityEditor;
using Unity.Collections;

[CustomEditor(typeof(MapGen_Layout))]
public class MapGen_LayoutEditor : Editor
{
    void OnSceneGUI()
    {
        var target = this.target as MapGen_Layout;
        if(target != null && target.done)
        {
            var data = target.executor.data;

            for(int c = 0; c < data.pathSteps.Length + 1; c++)
            {
                var chunkInfo = data.getChunkInfo(c);
                var chunkData = data.getChunkData(c);

                var restoreColor = GUI.color;
                GUI.color = Color.red;
                Handles.Label(new Vector3(chunkInfo.bounds.x + 0.25f, chunkInfo.bounds.y + 0.25f, 0) * 0.16f, $"{c}");
                GUI.color = restoreColor;

                for(int y = 0; y < chunkInfo.bounds.height; y++)
                for(int x = 0; x < chunkInfo.bounds.width; x++)
                {
                    int i           = (y * chunkInfo.bounds.width) + x;

                    var cType       = chunkData[i].constructionType;
                    var moduleId    = target.generatorSettings.modules[chunkData[i].floorModuleId];
                    var tilePos     = new Vector3(chunkInfo.bounds.x + x + 0.5f, chunkInfo.bounds.y + y + 0.5f, 0) * 0.16f;

                    Handles.Label(tilePos, $"{cType.ToString()[0]}");
                }
            }
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var instance = target as MapGen_Layout;
        if(instance != null)
        {
            GUI.enabled = instance.done;
            {
                if(Application.isPlaying && GUILayout.Button("Reset")) { instance.reset(); }
            }
            GUI.enabled = true;
        }
    }
}
