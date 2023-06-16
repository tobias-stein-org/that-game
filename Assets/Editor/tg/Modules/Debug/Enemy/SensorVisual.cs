using UnityEditor;
using UnityEngine;

namespace tg.editor.debug
{
    using tg.ai;
    using tg.debug;
    using tg.application;
    using static UnityEditor.FilePathAttribute;

    [CustomEditor(typeof(EnemyDebugging))]
    public class SensorVisual : Editor
    {
        public new EnemyDebugging target { get { return base.target as EnemyDebugging; } }

        GUIStyle targetLabel;
        GUIStyle distanceLabel;

        private void Awake()
        {
            this.targetLabel = new GUIStyle();
            {
                this.targetLabel.normal.textColor = Color.cyan;
            }

            this.distanceLabel = new GUIStyle();
            {
                this.distanceLabel.normal.textColor = Color.white;
                this.distanceLabel.fontSize = 8;
            }
        }

        void OnSceneGUI()
        {
            var position    = this.target.transform.position;
            var sensor      = this.target.sensor;

            // draw sensor range
            {
                Handles.color   = Color.white;
                Handles.DrawWireDisc(position, Vector3.forward, sensor.desc.senorPerceptionRange, 2.0f);
            }

            // draw percieved objects
            for(int i = 0; i < sensor.outputs.Length; i++)
            {
                var output      = sensor.outputs[i];
                var target      = new Vector3(output.point.x, output.point.y, 0);
                var dir         = (target - position).normalized;

                Handles.Label(position + (dir * output.distance * 0.5f), $"{output.distance:.02}", distanceLabel);

                Handles.color   = Color.magenta;
                Handles.DrawSolidDisc(target, Vector3.forward, 0.1f);
                Handles.DrawLine(position, target, 1.0f);

                Handles.Label(target, tg.application.tags.hash2Name(output.tag), this.targetLabel);
            }
        }
    }
}
