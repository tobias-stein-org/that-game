using UnityEditor;
using UnityEngine;

namespace tg.editor.debug
{
    using tg.ai;
    using tg.debug;
    using tg.application;
    using static UnityEditor.FilePathAttribute;
    using UnityEngine.UIElements;

    [CustomEditor(typeof(EnemyDebugging))]
    public class SensorVisual : Editor
    {
        public new EnemyDebugging target { get { return base.target as EnemyDebugging; } }

        GUIStyle targetLabel;
        GUIStyle distanceLabel;
        GUIStyle behaviourWeightLabel;

        Gradient behaviourValueColor;

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

            this.behaviourWeightLabel = new GUIStyle();
            {
                this.behaviourWeightLabel.normal.textColor = Color.magenta;
            }


            // Create a new gradient
            this.behaviourValueColor = new Gradient();
        
            GradientColorKey[] colorKeys = new GradientColorKey[3];
            colorKeys[0] = new GradientColorKey(Color.green,    0.0f);
            colorKeys[1] = new GradientColorKey(Color.white,    0.5f);
            colorKeys[2] = new GradientColorKey(Color.red,      1.0f);
        
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[3];
            alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(1.0f, 0.5f);
            alphaKeys[2] = new GradientAlphaKey(1.0f, 1.0f);
        
            this.behaviourValueColor.SetKeys(colorKeys, alphaKeys);
            this.behaviourValueColor.mode = GradientMode.PerceptualBlend;
        }

        void OnSceneGUI()
        {
            if((this.target.sensorDetails & EnemyDebugging.SensorDetails.Hide) == 0) { this.drawSensor(); }
            if(this.target.contextBehaviourDetails != EnemyDebugging.ContextBehaviourDetails.Hide) { this.drawBehaviourContext(); }
        }

        private void drawBehaviourContext()
        {
            var context     = this.target.behaviourContextData;
            var position    = this.target.transform.position;
            var scale       = 2.0f;
            
            if(!context.context.isValid)
            {
                Handles.Label(position, $"Behaviour Context not available!", this.behaviourWeightLabel);
                return;
            }

            Handles.color = Color.white * 0.5f;
            Handles.DrawWireDisc(position, Vector3.forward, scale);

            var colors      = new Color[BehaviourContext.segmentDir.Length + 1];
            var points      = new Vector3[BehaviourContext.segmentDir.Length + 1];
            var points1     = new Vector3[BehaviourContext.segmentDir.Length + 1];

            int i = 0;
            for(i = 0; i < BehaviourContext.segmentDir.Length; i++)
            {
                var offset  = (Vector3)BehaviourContext.segmentDir[i] * scale;

                Handles.DrawLine(position, position + offset, 1.0f);

                // values are expected to be in the range of [-1; +1]
                var value   = context.context[i];

                points[i]   = position + (offset * Mathf.Abs(value));
                points1[i]  = position + (offset * Mathf.Abs(context.context1[i]));

                colors[i]   = this.behaviourValueColor.Evaluate((value + 1.0f) * 0.5f);
            }
            points[i]       = points[0];
            points1[i]      = points1[0];
            colors[i]       = colors[0];

            Handles.color = Color.white * 0.33f;
            Handles.DrawAAConvexPolygon(points1);
            Handles.DrawAAPolyLine(6.0f, colors, points);

            Handles.Label(position, $"W: {context.weight}, B: {context.blend}", this.behaviourWeightLabel);
        }

        private void drawSensor()
        {
            var position    = this.target.transform.position;
            var sensor      = this.target.sensor;

            // draw sensor range
            if((this.target.sensorDetails & EnemyDebugging.SensorDetails.ShowRange) != 0)
            {
                Handles.color   = Color.white;
                Handles.DrawWireDisc(position, Vector3.forward, sensor.desc.senorPerceptionRange, 2.0f);
            }

            if((this.target.sensorDetails & EnemyDebugging.SensorDetails.ShowOutput) != 0)
            {
                // draw percieved objects
                for(int i = 0; i < sensor.outputs.Length; i++)
                {
                    var output      = sensor.outputs[i];
                    var target      = new Vector3(output.point.x, output.point.y, 0);
                    var dir         = (target - position).normalized;

                    Handles.color   = Color.magenta;
                    Handles.DrawLine(position, target, 1.0f);

                    if((this.target.sensorDetails & EnemyDebugging.SensorDetails.ShowDistance) != 0) { Handles.Label(position + (dir * output.distance * 0.5f), $"{output.distance:.02}", distanceLabel); }
                    if((this.target.sensorDetails & EnemyDebugging.SensorDetails.ShowTargetTag) != 0) { Handles.Label(target, tg.application.tags.hash2Name(output.tag), this.targetLabel); }
                }
            }
        }
    }
}
