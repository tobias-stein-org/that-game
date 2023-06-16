using UnityEngine;

using Unity.Entities;

namespace tg.ai
{
    using tg.application;

    /// <summary>
    /// This system will update all available sensors in the scene.
    /// </summary>
    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Perception : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<Perception>(state.SystemHandle));
            state.RequireForUpdate<Sensor>();
        }

        private static RaycastHit2D[]   hit2DBuffer         = new RaycastHit2D[1];
        private static Collider2D[]     rbAttachedCollider  = new Collider2D[1];
        private static Collider2D[]     collisionBuffer     = new Collider2D[Sensor.Description.MAX_SENSOR_OUTPUTS];
        private static int              levelLayerIndex     = LayerMask.NameToLayer("Level");
        private static int              levelLayerOnlyMask  = 1 << LayerMask.NameToLayer("Level");

        private static int              levelRaysResolution = 6;
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

                var numCollisions       = Physics2D.OverlapCircleNonAlloc(rb.Value.position, sensor.ValueRO.desc.senorPerceptionRange, Perception.collisionBuffer, sensor.ValueRO.desc.sensorMask);

                rb.Value.GetAttachedColliders(Perception.rbAttachedCollider);

                for(int i               = 0; i < numCollisions; i++)
                {
                    // stop, if sensor output buffer is full
                    if(sensor.ValueRO.outputs.Length == sensor.ValueRO.outputs.Capacity) { break; }

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
                            var hasHit          = rbAttachedCollider[0].Raycast(dir, Perception.hit2DBuffer, sensor.ValueRO.desc.senorPerceptionRange, Perception.levelLayerOnlyMask);

                            if(hasHit > 0)
                            {
                                // record this result
                                sensor.ValueRW.outputs.Add(new Sensor.Output(Perception.hit2DBuffer[0], BehaviourContext.dir2seg(in dir)));

                                // we stop, if sensor buffer is full
                                if(sensor.ValueRO.outputs.Length == sensor.ValueRO.outputs.Capacity) { break; }
                            }
                        }
                    }
                    else
                    {
                        var dir             = ((Vector2)collider.transform.position - (Vector2)rb.Value.position).normalized;
                        var hasHit          = rbAttachedCollider[0].Raycast(dir, Perception.hit2DBuffer, sensor.ValueRO.desc.senorPerceptionRange, sensor.ValueRO.desc.sensorMask);

                        if(hasHit > 0)
                        {
                            // Ignore this result, if the collider in our sensor range is obstructed by another object and the sensor is not allowed to see these objects
                            if(!sensor.ValueRO.desc.allowSeeHidden && (collider != Perception.hit2DBuffer[0].collider)) { continue; }

                            // record this result
                            sensor.ValueRW.outputs.Add(new Sensor.Output(Perception.hit2DBuffer[0], BehaviourContext.dir2seg(in dir)));
                        }
                    }
                }
            }
        }
    }
}

