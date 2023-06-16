using UnityEditor;
using UnityEngine;

namespace tg.editor.debug
{
    using tg.ai;
    using tg.debug;

    [CustomEditor(typeof(EnemyDebugging))]
    public class SensorVisual : Editor
    {
        public new EnemyDebugging target { get { return base.target as EnemyDebugging; } }

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

                Handles.color   = Color.magenta;
                Handles.DrawSolidDisc(target, Vector3.forward, 0.1f);
                Handles.DrawDottedLine(position, target, 1.0f);
            }
        }
    }
}
