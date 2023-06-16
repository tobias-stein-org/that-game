using UnityEditor;
using UnityEngine;

namespace tg.editor.debug
{
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

                var target      = new Vector3(output.position.x, output.position.y, output.position.z);
                var contact     = new Vector3(output.contact.x, output.contact.y, output.contact.z);

                Handles.color   = Color.magenta;
                Handles.DrawSolidDisc(target, Vector3.forward, 0.1f);
                Handles.DrawDottedLine(position, target, 1.0f);
            }
        }
    }
}
