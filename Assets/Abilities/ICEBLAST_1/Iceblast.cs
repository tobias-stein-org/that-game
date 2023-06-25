using tg.physics;
using UnityEngine;

namespace tg.ability
{
    public class Iceblast : Ability
    {
        [AbilityProperty]
        public float radius = 2.0f;

        public GameObject blast;

        // Start is called before the first frame update
        void Start()
        {
            this.blast.transform.localScale = Vector3.one * this.radius;
            
            Destroy(this.gameObject, 0.1f);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            var entity = collider2entity.toEntity(collision);
            // no self-damage
            if(entity == this.caster) { return; }

            Debug.Log($"{this.caster}'s Iceblast dealt damage to {entity}");    
        }
    }
}