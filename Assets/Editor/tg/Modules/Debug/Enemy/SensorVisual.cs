using System.Linq;

using UnityEditor;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace tg.editor.debug
{
    using tg.ai;
    using tg.ai.behaviour;
    using tg.debug;
    using tg.application;

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
            colorKeys[0] = new GradientColorKey(Color.red,   0.0f);
            colorKeys[1] = new GradientColorKey(Color.white, 0.5f);
            colorKeys[2] = new GradientColorKey(Color.green, 1.0f);
        
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[3];
            alphaKeys[0] = new GradientAlphaKey(1.0f,        0.0f);
            alphaKeys[1] = new GradientAlphaKey(1.0f,        0.5f);
            alphaKeys[2] = new GradientAlphaKey(1.0f,        1.0f);
        
            this.behaviourValueColor.SetKeys(colorKeys, alphaKeys);
            this.behaviourValueColor.mode = GradientMode.PerceptualBlend;
        }

        private void OnEnable()
        {
            this.target.fetchData = true;
        }

        private void OnDisable()
        {
            this.target.fetchData = false;
        }

        void OnSceneGUI()
        {
            if((this.target.sensorDetails & EnemyDebugging.SensorDetails.Hide) == 0) { this.drawSensor(); }
            if(this.target.contextBehaviourDetails != EnemyDebugging.ContextBehaviourDetails.Hide) { this.drawBehaviourContext(); }

            var sceneView   = SceneView.lastActiveSceneView;
            if(this.target.followEntity && sceneView) { sceneView.LookAt(this.target.transform.position); }
        }

        private void drawBehaviourContext()
        {
            var context     = this.target.behaviourContextData;
            var position    = this.target.transform.position;
            var scale       = 2.0f;

            this.drawBehaviour();

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

            var i           = 0;

            var min         = context.context[0];
            var max         = context.context[0];
            var imin        = 0;
            var imax        = 0;

            for(i = 0; i < BehaviourContext.segmentDir.Length; i++)
            {
                var offset  = (Vector3)BehaviourContext.segmentDir[i] * scale;

                Handles.DrawLine(position, position + offset, 1.0f);
                if((this.target.sensorDetails & EnemyDebugging.SensorDetails.ShowTargetSegment) != 0) { Handles.Label(position + offset, $"{i}"); }

                // values are expected to be in the range of [-1; +1]
                var value   = context.context[i];
                
                points[i]   = position + (offset * Mathf.Abs(value));
                points1[i]  = position + (offset * Mathf.Abs(context.context1[i]));
                colors[i]   = this.behaviourValueColor.Evaluate((value + 1.0f) * 0.5f);

                if(value > max) { max = value; imax = i; }
                if(value < min) { min = value; imin = i; }
            }

            points[i]       = points[0];
            points1[i]      = points1[0];
            colors[i]       = colors[0];

            Handles.color = Color.white * 0.33f;
            Handles.DrawAAPolyLine(4.0f, points1);
            Handles.DrawAAPolyLine(6.0f, colors, points);

            Handles.color   = Color.cyan;
            Handles.DrawLine(position, position + (Vector3)BehaviourContext.segmentDir[imax], 1.0f);

            Handles.color   = Color.magenta;
            Handles.DrawLine(position, position + (Vector3)BehaviourContext.segmentDir[imin], 1.0f);

            Handles.Label(position, $"W: {context.weight}, B: {context.blend}", this.behaviourWeightLabel);
        }

        private void drawBehaviour()
        {
            var behaviour     = this.target.behaviourData;
            var position      = this.target.transform.position;

            if(behaviour == default)
            {
                Handles.Label(position + Vector3.up * 0.25f, $"No behaviour data available!", this.behaviourWeightLabel);
                return;
            }

            Handles.color = Color.yellow;

            switch(this.target.contextBehaviourDetails)
            {
                case EnemyDebugging.ContextBehaviourDetails.Wander:    { this.drawBehaviour((Wander)behaviour); break; }
                case EnemyDebugging.ContextBehaviourDetails.Flee:      { this.drawBehaviour((Flee)behaviour); break; }
                case EnemyDebugging.ContextBehaviourDetails.Avoid:     { this.drawBehaviour((Avoid)behaviour); break; }
                case EnemyDebugging.ContextBehaviourDetails.Pursue:    { this.drawBehaviour((Pursue)behaviour); break; }
            }
        }

        private void drawBehaviour(Wander data)
        {
            var position                    = this.target.transform.position;
            var velocity                    = this.target.GetComponent<Rigidbody2D>().velocity;
            var forward                     = velocity.normalized;

            // draw forward vector (heading)
            Handles.DrawLine(position, position + (Vector3)forward, 3.0f);

            // draw wander steering shape
            var steeringOffset              = (Vector3)forward * data.steeringOffset;
            Handles.DrawDottedLine(position, position + steeringOffset, 4.0f);
            Handles.DrawWireDisc(position + steeringOffset, Vector3.forward, data.steeringRadius);

            // draw steering force
            var steeringForce               = position + new Vector3(data.steeringForce.x, data.steeringForce.y, 0.0f);
            Handles.DrawDottedLine(position, steeringForce, 4.0f);
            Handles.DrawSolidDisc(steeringForce, Vector3.forward, 0.1f);
        }

        private void drawBehaviour(Avoid data)
        {
            var position                    = this.target.transform.position;
            Handles.DrawWireDisc(position, Vector3.forward, data.range);
        }

        private void drawBehaviour(Flee data)
        {
        }

        private void drawBehaviour(Pursue data)
        {
            var position    = this.target.transform.position;
            var target      = new Vector3(data.target.x, data.target.y, 0.0f);

            Handles.DrawDottedLine(position, target, 4.0f);
            Handles.DrawSolidDisc(target, Vector3.forward, 0.1f);
            Handles.DrawWireDisc(target, Vector3.forward, data.minDistance);
        }

        private void drawSensor()
        {
            var position    = this.target.transform.position;
            var sensor      = this.target.sensor;

            // draw sensor range
            if((this.target.sensorDetails & EnemyDebugging.SensorDetails.ShowRange) != 0)
            {
                Handles.color   = Color.white;
                Handles.DrawWireDisc(position, Vector3.forward, sensor.desc.range, 2.0f);
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
                    if((this.target.sensorDetails & EnemyDebugging.SensorDetails.ShowTargetSegment) != 0) { Handles.Label(target + Vector3.up * 0.25f, $"{output.segment}"); }
                }
            }
        }
    }
}
