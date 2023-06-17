using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

using SensorOutputBuffer = Unity.Collections.FixedList512Bytes<tg.ai.Sensor.Output>;

namespace tg.ai
{
    using System.Collections;
    using tg.application;

    public struct Sensor : IComponentData, IEnableableComponent, IEnumerable<Sensor.Output>
    {
        /// <summary>
        /// A description how a sensor should behave.
        /// </summary>
        public struct Description
        {
            /// <summary>
            /// Maximum number of generated sensor outputs per frame;
            /// </summary>
            public static readonly int          MAX_SENSOR_OUTPUTS = new SensorOutputBuffer().Capacity;

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
        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Output
        {
            /// <summary>
            /// Enumerator class to iterate over sensor outputs.
            /// </summary>
            public class Enumerator : IEnumerator<Output>, IEnumerable<Output>
            {
                #region Filters

                public interface IFilter
                {
                    bool check(in Output output);
                }

                private struct CombineFilter : IFilter
                {
                    public IFilter filter0;
                    public IFilter filter1;

                    public bool check(in Output output) { return this.filter0.check(in output) && this.filter1.check(in output); }
                }

                public struct FilterNone : IFilter
                {
                    public bool check(in Output output) { return true; }
                }

                public struct FilterWithTag : IFilter
                {
                    public TagHash      tag;

                    public bool check(in Output output) { return output.tag == this.tag; }
                }

                public struct FilterWithTags : IFilter
                {
                    public TagHash[]    tags;

                    public bool check(in Output output) { return this.tags.Contains(output.tag); }
                }

                public struct FilterWithInRange : IFilter
                {
                    public float minRange;
                    public float maxRange;

                    public bool check(in Output output) { return output.distance > this.minRange && output.distance < this.maxRange; }
                }

                #endregion

                public Output          Current { get; private set; }
                private int            index   = 0;


                private IFilter        _filter  = new FilterNone();

                private IFilter        filter
                {
                    get { return this._filter; }
                    set
                    {
                        this._filter = this._filter is FilterNone ? value : new CombineFilter { filter0 = this._filter, filter1 = value };
                    }
                }
                private readonly Sensor sensor;

                public Enumerator(in Sensor sensor)
                {
                    this.sensor = sensor;
                    this.index  = 0;
                    this._filter = new FilterNone();
                }

                object IEnumerator.Current  => null;

                public void Dispose() { }

                public bool MoveNext()
                {
                    if(this.index < this.sensor.outputs.Length)
                    {
                        var output = this.sensor.outputs[this.index];
                        this.index++;

                        if(this.filter.check(in output))
                        {
                            this.Current = output;
                            return true;
                        }
                        else
                        {
                            return this.MoveNext();
                        }
                    }

                    this.Current = default;
                    return false;
                }

                public void Reset() { this.index = 0; }

                public IEnumerator<Output> GetEnumerator()  { return this; }
                IEnumerator IEnumerable.GetEnumerator()     { return this; }

                #region Queries

                public Enumerator withTag(Tag tag)
                {
                    this.filter  = new FilterWithTag { tag = tag.hash };
                    return this;
                }

                public Enumerator withTags(params Tag[] tags)
                {
                    this.filter  = new FilterWithTags { tags = tags.Select(tag => tag.hash).ToArray() };
                    return this;
                }

                public Enumerator withInRange(float maxRange, float minRange = 0.0f)
                {
                    this.filter  = new FilterWithInRange { minRange = math.clamp(minRange, 0.0f, maxRange), maxRange = math.max(maxRange, 0.0f) };
                    return this;
                }

                #endregion
            }

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
            public readonly TagHash     tag;

            /// <summary>
            /// The segment index of the sensor where this output has been perceived.
            /// </summary>
            public readonly byte        segment;

            /// <summary>
            /// Convert a raycast hit 2D into sensor output.
            /// </summary>
            /// <param name="hit2D"></param>
            public Output(in RaycastHit2D hit2D, byte segment)
            {
                this.segment            = segment;

                this.point              = new float2(hit2D.point.x, hit2D.point.y);
                this.normal             = hit2D.normal;
                this.distance           = hit2D.distance;
                this.tag                = hit2D.rigidbody != null ? hit2D.rigidbody.tag : hit2D.collider.tag;
            }
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

        public IEnumerator<Output> GetEnumerator()  { return new Output.Enumerator(this); }
        IEnumerator IEnumerable.GetEnumerator()     { return this.GetEnumerator(); }

        public Output.Enumerator  query()         { return new Output.Enumerator(this); }
    }
}

