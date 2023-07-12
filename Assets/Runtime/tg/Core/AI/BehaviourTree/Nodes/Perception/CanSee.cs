using System.Linq;

namespace tg.ai.behaviour.tree.node
{
    using tg.application;
    using tg.ai.entities;

    [NodeContextMenuPath("Perception/Can See")]
    public class CanSee : Action
    {
        public BlackboardValue<Sensor.Output> target;

        public Tag targetTag;

        public override State evaluate(ref Context context)
        {
            var target = context.perception.query().withTag(this.targetTag).FirstOrDefault();
            if(target.isValid)
            {
                this.target.value = target;
                return State.success;
            }

            return State.failure;
        }
    }
}
