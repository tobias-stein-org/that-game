using UnityEngine;


namespace tg.ai.behaviour.tree.node
{
    using tg.ai.entities;

    public class Pursue : Action
    {
        public BlackboardValue<Sensor.Output> target;

        public override State evaluate(ref Context context)
        {
            if(!context.entityManager.HasComponent<tg.ai.steering.behaviour.entities.Pursue>(context.entity)) { return State.failure; }

            // start pursue the current percied player location
            var pursue = context.entityManager.GetComponentData<tg.ai.steering.behaviour.entities.Pursue>(context.entity);
            {
                pursue.target = this.target.value.point;
            }

            context.entityManager.SetComponentData<tg.ai.steering.behaviour.entities.Pursue>(context.entity, pursue);
            context.entityManager.SetComponentEnabled<tg.ai.steering.behaviour.entities.Wander>(context.entity, false);
            context.entityManager.SetComponentEnabled<tg.ai.steering.behaviour.entities.Pursue>(context.entity, true);

            return State.success;
        }
    }
}
