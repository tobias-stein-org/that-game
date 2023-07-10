using System.Linq;

namespace tg.ai.behaviour.tree.node
{
    using tg.ai.entities;

    public class CanSee : Action
    {
        private BlackboardValue<Sensor.Output> target;

        public string  targetTag;

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
