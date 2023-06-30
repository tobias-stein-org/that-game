using UnityEngine;

using Unity.Entities;
using Unity.Transforms;

namespace tg.ai
{
    using tg.application.entities;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.HID;

    /// <summary>
    /// This system will update all available sensors in the scene.
    /// </summary>
    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    [UpdateInGroup(typeof(TransformSystemGroup))]
    public partial struct Perception : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<Perception>(state.SystemHandle)));
            state.RequireForUpdate<Sensor>();
        }

        private static RaycastHit2D[]   hit2DBuffer         = new RaycastHit2D[1];
        private static Collider2D[]     rbAttachedCollider  = new Collider2D[1];
        private static Collider2D[]     collisionBuffer     = new Collider2D[Sensor.Description.MAX_SENSOR_OUTPUTS];
        private static int              levelLayerIndex     = LayerMask.NameToLayer("Level");
        private static ContactFilter2D  levelFilter         = new ContactFilter2D
        {
                                        layerMask           = 1 << LayerMask.NameToLayer("Level"),
                                        useLayerMask        = true,
        };

        private static int              levelRaysResolution = 8;
        private static Vector2[]        levelRays           = new Vector2[Perception.levelRaysResolution];

        static Perception()
        {
            var offset = Quaternion.AngleAxis(360.0f / Perception.levelRaysResolution, Vector3.forward);
            Perception.levelRays[0] = Vector3.right;
            for(int i = 1; i < Perception.levelRaysResolution; i++) { Perception.levelRays[i] = offset * Perception.levelRays[i - 1]; }
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach(var (sensor, rb) in SystemAPI.Query<RefRW<Sensor>, SystemAPI.ManagedAPI.UnityEngineComponent<Rigidbody2D>>())
            {
                // clear old sensor outputs from last frame
                sensor.ValueRW.outputs.Clear();

                var sensorFilter    = new ContactFilter2D
                {
                    layerMask       = sensor.ValueRO.desc.sensorMask,
                    useLayerMask    = true,
                };

                var numCollisions       = Physics2D.OverlapCircleNonAlloc(rb.Value.position, sensor.ValueRO.desc.range, Perception.collisionBuffer, sensor.ValueRO.desc.sensorMask);

                rb.Value.GetAttachedColliders(Perception.rbAttachedCollider);

                for(int i               = 0; i < numCollisions; i++)
                {
                    // stop, if sensor output buffer is full
                    if(sensor.ValueRO.outputs.Length == sensor.ValueRO.desc.maxOutouts) { break; }

                    var collider        = Perception.collisionBuffer[i];

                    // we ignore ourself
                    if(collider.attachedRigidbody == rb.Value) { continue; }

                    // We will treat a collision with the level collider differently then others.
                    // Since the level collider is a huge mesh collider simply using the collider information and a single raycast is to less information
                    // and also will yield wrong sensor output.
                    if(collider.gameObject.layer == Perception.levelLayerIndex)
                    {
                        // in order to get a better perception, we will cast a rays in all directions and see where they hit the level
                        foreach(var dir in Perception.levelRays)
                        {
                            var hasHit      = rbAttachedCollider[0].Cast(dir, Perception.levelFilter, Perception.hit2DBuffer, sensor.ValueRO.desc.range);
                            if(hasHit       > 0)
                            {
                                // record this result
                                sensor.ValueRW.outputs.Add(new Sensor.Output(
                                    point       : Perception.hit2DBuffer[0].point,
                                    normal      : Perception.hit2DBuffer[0].normal,
                                    distance    : Perception.hit2DBuffer[0].distance,
                                    tag         : Perception.hit2DBuffer[0].rigidbody != null ? Perception.hit2DBuffer[0].rigidbody.tag : Perception.hit2DBuffer[0].collider.tag,
                                    segment     : BehaviourContext.dir2seg(in dir)
                                ));

                                // we stop, if sensor buffer is full
                                if(sensor.ValueRO.outputs.Length == sensor.ValueRO.desc.maxOutouts) { break; }
                            }
                        }
                    }
                    else
                    {
                        var dif             = ((Vector2)collider.transform.position - (Vector2)rb.Value.position);
                        var dis             = dif.magnitude;
                        var dir             = dif.normalized;
                        
                        // in case the sesnor should only see things that are not hidden, we have to perform an additional raycast
                        if(!sensor.ValueRO.desc.allowSeeHidden)
                        {
                            var hasHit          = rbAttachedCollider[0].Cast(dir, sensorFilter, Perception.hit2DBuffer, sensor.ValueRO.desc.range);
                            if(hasHit           > 0 && (collider != Perception.hit2DBuffer[0].collider))
                            {
                                // Ignore this result, if the collider in our sensor range is obstructed by another object
                                continue;
                            }

                            // record this result
                            sensor.ValueRW.outputs.Add(new Sensor.Output(
                                point       : Perception.hit2DBuffer[0].point,
                                normal      : Perception.hit2DBuffer[0].normal,
                                distance    : Perception.hit2DBuffer[0].distance,
                                tag         : Perception.hit2DBuffer[0].rigidbody != null ? Perception.hit2DBuffer[0].rigidbody.tag : Perception.hit2DBuffer[0].collider.tag,
                                segment     : BehaviourContext.dir2seg(in dir)
                            ));
                        }
                        else
                        {
                            sensor.ValueRW.outputs.Add(new Sensor.Output(
                                point       : collider.transform.position,
                                normal      : new Vector2(-dir.y, dir.x),
                                distance    : dis,
                                tag         : collider.attachedRigidbody != null ? collider.attachedRigidbody.tag : collider.tag,
                                segment     : BehaviourContext.dir2seg(in dir)
                            ));
                        }
                    }
                }
            }
        }
    }
}

