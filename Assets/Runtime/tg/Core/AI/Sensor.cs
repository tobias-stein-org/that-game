using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

using SensorOutputBuffer = Unity.Collections.FixedList512Bytes<tg.ai.Sensor.Output>;

namespace tg.ai
{
    public struct Sensor : IComponentData, IEnableableComponent
    {
        /// <summary>
        /// A description how a sensor should behave.
        /// </summary>
        public struct Description
        {
            /// <summary>
            /// Maximum number of generated sensor outputs per frame;
            /// </summary>
            internal static readonly int        MAX_SENSOR_OUTPUTS = new SensorOutputBuffer().Capacity;

            /// <summary>
            /// How far the sensor can see.
            /// </summary>
            public float                        senorPerceptionRange;

            /// <summary>
            /// What the sensor can see.
            /// </summary>
            public LayerMask                    sensorMask;

            /// <summary>
            /// Whether or not the sensor is allowed to see hidden objects.
            /// </summary>
            public bool                         allowSeeHidden;

            public Description(float range, LayerMask mask, bool allowSeeHidden)
            {
                this.senorPerceptionRange       = range;
                this.sensorMask                 = mask;
                this.allowSeeHidden             = allowSeeHidden;
            }

            /// <summary>
            /// Returns a default sensor description that will percive all object in the world.
            /// </summary>
            public static readonly Description  Default = new Description(10.0f, Physics2D.AllLayers, false);
        }

        /// <summary>
        /// The output of a sensor
        /// </summary>
        public struct Output
        {
            /// <summary>
            /// Point of collision.
            /// </summary>
            public readonly float2      point;

            /// <summary>
            /// Normal of the contact point.
            /// </summary>
            public readonly float2      normal;

            /// <summary>
            /// Distance to the contact point.
            /// </summary>
            public readonly float       distance;

            /// <summary>
            /// Hash value of the tag attached to the object.
            /// </summary>
            public readonly int         tagHash;

            /// <summary>
            /// Convert a raycast hit 2D into sensor output.
            /// </summary>
            /// <param name="hit2D"></param>
            public Output(in RaycastHit2D hit2D)
            {
                this.point              = new float2(hit2D.point.x, hit2D.point.y);
                this.normal             = hit2D.normal;
                this.distance           = hit2D.distance;
                this.tagHash            = hit2D.collider.gameObject.tag.GetHashCode();
            }

            public static implicit operator Output(in RaycastHit2D hit2D) { return new Output(hit2D); }
        }

        /// <summary>
        /// This sensors configuration.
        /// </summary>
        public Description              desc;

        /// <summary>
        /// This sensors frame outputs.
        /// </summary>
        public SensorOutputBuffer       outputs;

        public Sensor(Description desc)
        {
            this.desc                   = desc;
            this.outputs                = default;
            this.outputs.Clear();
        }

        public static readonly Sensor   Default = new Sensor(Sensor.Description.Default);
    }
}

