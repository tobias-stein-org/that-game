using UnityEngine;
using UnityEditor;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using UnityEditor.Rendering;

[CustomEditor(typeof(MapGen_Layout))]
public class MapGen_LayoutEditor : Editor
{
    void OnSceneGUI()
    {
        var camCenter = SceneView.lastActiveSceneView.camera.transform.position * 1f/0.16f;

        var viewRect  = new RectInt
        {
            xMin = Mathf.FloorToInt(camCenter.x - 50f),
            yMin = Mathf.FloorToInt(-camCenter.y - 50f),
            xMax = Mathf.FloorToInt(camCenter.x + 50f),
            yMax = Mathf.FloorToInt(-camCenter.y + 50f),
        };

        var target = this.target as MapGen_Layout;
        if(Application.isPlaying && target != null && target.executor != null && !target.executor.isBusy)
        {
            var data = target.executor.data;
            float camDistance = Mathf.Abs(SceneView.lastActiveSceneView.camera.transform.position.z);

            for(int c = 0; c < data.numMapChunks; c++)
            {

                var chunkInfo = data.getChunkInfo(c);
                var chunkData = data.getChunkData(c);

                if(!viewRect.Contains(chunkInfo.bounds.min) && !viewRect.Contains(chunkInfo.bounds.max)) { continue; }

                this.drawMapChunkId(chunkInfo, c);

                if(camDistance < 7.0f)
                {
                    for(int y = 0; y<chunkInfo.bounds.height; y++)
                    for(int x = 0; x<chunkInfo.bounds.width; x++)
                    {
                        if(!viewRect.Contains(new Vector2Int(x, y))) { continue; }

                        int i = (y * chunkInfo.bounds.width) + x;

                        var cType = chunkData[i].constructionType;

                            //if(chunkData[i].obstrModuleId != -1)
                            //    Handles.Label(new Vector3(chunkInfo.bounds.x + x + 0.1f, -(chunkInfo.bounds.y + y) + 0.66f, 0) * 0.16f, $"{target.generatorSettings.modules[chunkData[i].obstrModuleId].name} [T: {target.generatorSettings.modules[chunkData[i].obstrModuleId].constructionType.ToString()[0]}, ID: {chunkData[i].obstrModuleId}]");

                        //if(chunkData[i][MapGenData.Layer.Floor] != -1)
                        //    Handles.Label(new Vector3(chunkInfo.bounds.x + x + 0.1f, -(chunkInfo.bounds.y + y) + 0.66f, 0) * 0.16f, $"{target.generatorSettings.modules[chunkData[i][MapGenData.Layer.Floor]].name}");


                        var prevColor = GUI.color;
                        switch(cType)
                        {
                            case TileConstructionType.Obstructed:
                                GUI.color = Color.gray;
                                Handles.Label(new Vector3(chunkInfo.bounds.x + x + 0.1f, -(chunkInfo.bounds.y + y) + 0.33f, 0) * 0.16f, "O");
                                break;

                            case TileConstructionType.Walkable:
                                GUI.color = Color.green;
                                Handles.Label(new Vector3(chunkInfo.bounds.x + x + 0.1f, -(chunkInfo.bounds.y + y) + 0.33f, 0) * 0.16f, "W");
                                break;
                        }

                        GUI.color = prevColor;
                    }
                }
            }
        }
    }

    private void drawMapChunkId(in MapChunkInfo chunkInfo, int chunkId)
    {
        var restoreColor = GUI.color;
        GUI.color = Color.red;
        Handles.Label(new Vector3(chunkInfo.bounds.x + 0.25f, -chunkInfo.bounds.y + 0.25f, 0) * 0.16f, $"{chunkId}");
        GUI.color = restoreColor;
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
