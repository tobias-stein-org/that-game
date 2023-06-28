using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

namespace tg.ability
{
    using tg.physics;

    public class Attack : Ability
    {
        [AbilityProperty]
        public float            range = 2.0f;

        [AbilityProperty]
        public float            knowbackForce = 500.0f;

        [AbilityProperty]
        public float            attackAnimationTime = 0.1f;

        public SpriteRenderer   sr;
        
        private float           attackEnd;

        // Start is called before the first frame update
        void Start()
        {
            this.attackEnd = Time.time + this.attackAnimationTime;

            //this.sr.gameObject.transform.Translate(0f, this.range, 0f);
            this.transform.localScale *= this.range;

            transform.rotation = Quaternion.AngleAxis(Mathf.Atan2(this.initialDirection.x, -this.initialDirection.y) * Mathf.Rad2Deg, Vector3.forward);
        }

        // Update is called once per frame
        void Update()
        {
            float t = Mathf.Clamp01((this.attackEnd - Time.time) / this.attackAnimationTime);
            sr.material.color = Color.white * t;

            if(Time.time > this.attackEnd)
            {
                Destroy(this.gameObject.transform.root.gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            var entity = collider2entity.toEntity(collision);
            // no self-damage
            if(entity == this.caster) { return; }

            collision.attachedRigidbody?.AddForce(this.initialDirection * this.knowbackForce, ForceMode2D.Force);

            Debug.Log($"{this.caster}'s melee attack dealt damage to {entity}");
        }
    }
}
