using UnityEngine;


namespace tg.ai.behaviour.tree.node
{
    public class Chance : Decorator
    {
        [Range(0.0f, 1.0f)]
        public float chance;

        public bool successIfNot;

        public override State evaluate(ref Context context)
        {
            return UnityEngine.Random.value > this.chance ? this.node.evaluate(ref context) : (this.successIfNot ? State.success : State.failure);
        }
    }
}
