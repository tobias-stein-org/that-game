using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace tg.ability
{
    using tg.physics;

    public class Fireball : Ability
    {
        // Start is called before the first frame update
        void Start()
        {
            var rb = GetComponentInChildren<Rigidbody2D>();
            rb.AddForce(this.initialDirection * 20.0f, ForceMode2D.Impulse);
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            var entity = collider2entity.toEntity(collision.collider);
            // no self-damage
            if(entity == this.caster) { return; }

            Debug.Log($"{this.caster}'s Fireball dealt damage to {entity}");

            // destroy the fire ball
            Destroy(this.gameObject, Time.deltaTime);
        }


        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
