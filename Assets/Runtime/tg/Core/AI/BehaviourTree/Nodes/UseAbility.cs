using UnityEngine;

namespace tg.ai.behaviour.tree.node
{
    using tg.ability.events;
    using tg.ai.entities;
    using tg.events;
    using Unity.Mathematics;


    public class UseAbility : Action
    {
        public string ability;

        public BlackboardValue<Sensor.Output> target;

        public override State evaluate(ref Context context)
        {
            var rb  = context.entityManager.GetComponentObject<Rigidbody2D>(context.entity);
            if(rb == null) { return State.failure; }


            var pos = (float2)rb.position;
            var dir = math.normalize(this.target.value.point - (float2)rb.position);

            EventQueue.publish(new UseAbilityEvent<Unity.Collections.FixedString64Bytes>
            {
                entity      = context.entity,
                ability     = this.ability,
                point       = pos + (dir * 1.5f),
                direction   = dir
            });
            return State.success;
        }
    }
}
