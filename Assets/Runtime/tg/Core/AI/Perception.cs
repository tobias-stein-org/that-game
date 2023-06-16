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

        private static RaycastHit2D[] hit2DBuffer = new RaycastHit2D[1];

        public void OnUpdate(ref SystemState state)
        {
            foreach(var (sensor, rb) in SystemAPI.Query<RefRW<Sensor>, SystemAPI.ManagedAPI.UnityEngineComponent<Rigidbody2D>>())
            {
                // clear old sensor outputs from last frame
                sensor.ValueRW.outputs.Clear();

                var collisionBuffer     = new Collider2D[Sensor.Description.MAX_SENSOR_OUTPUTS];
                var numCollisions       = Physics2D.OverlapCircleNonAlloc(rb.Value.position, sensor.ValueRO.desc.senorPerceptionRange, collisionBuffer, sensor.ValueRO.desc.sensorMask);

                var rbAttachedCollider  = new Collider2D[1];
                rb.Value.GetAttachedColliders(rbAttachedCollider);

                for(int i               = 0; i < numCollisions; i++)
                {
                    var collider        = collisionBuffer[i];

                    // we ignore ourself
                    if(collider.attachedRigidbody == rb.Value) { continue; }

                    var hasHit          = rbAttachedCollider[0] != null
                        ? rbAttachedCollider[0].Raycast(((Vector2)collider.transform.position - (Vector2)rb.Value.position).normalized, Perception.hit2DBuffer, sensor.ValueRO.desc.senorPerceptionRange, sensor.ValueRO.desc.sensorMask)
                        : Physics2D.RaycastNonAlloc(rb.Value.position, ((Vector2)collider.transform.position - (Vector2)rb.Value.position).normalized, Perception.hit2DBuffer, sensor.ValueRO.desc.senorPerceptionRange, sensor.ValueRO.desc.sensorMask);

                    if(hasHit > 0)
                    {
                        // Ignore this result, if the collider in our sensor range is obstructed by another object and the sensor is not allowed to see these objects
                        if(!sensor.ValueRO.desc.allowSeeHidden && (collider != Perception.hit2DBuffer[0].collider)) { continue; }

                        // record this result
                        sensor.ValueRW.outputs.Add(Perception.hit2DBuffer[0]);
                    }
                }
            }
        }
    }
}

