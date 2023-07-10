namespace tg.ai.behaviour.tree.node
{
    using tg.ai.entities;

    public class InRange : Action
    {
        private BlackboardValue<Sensor.Output> target;

        public float range = 1.0f;

        public override State evaluate(ref Context context)
        {
            return this.target.value.isValid && this.target.value.distance <= this.range
                ? State.success
                : State.failure;
        }
    }
}
