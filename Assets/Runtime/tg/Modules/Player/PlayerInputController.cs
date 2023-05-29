using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

namespace tg.player
{
    public partial class PlayerInputController : SystemBase
    {
        private static readonly float3 UP       = new float3(0.0f,  1.0f, 0.0f);
        private static readonly float3 DOWN     = new float3(0.0f, -1.0f, 0.0f);
        private static readonly float3 LEFT     = new float3(-1.0f, 0.0f, 0.0f);
        private static readonly float3 RIGHT    = new float3( 1.0f, 0.0f, 0.0f);

        protected override void OnCreate()
        {
            this.RequireForUpdate<Player>();
        }

        protected override void OnUpdate()
        {
            float speed = 10.0f * this.World.Time.DeltaTime;

            foreach(var localTransform in SystemAPI.Query<RefRW<LocalTransform>>().WithAll<Player>())
            {
                if(Input.GetKey(KeyCode.W)) { localTransform.ValueRW.Position += UP * speed; }
                if(Input.GetKey(KeyCode.S)) { localTransform.ValueRW.Position += DOWN * speed; }
                if(Input.GetKey(KeyCode.A)) { localTransform.ValueRW.Position += LEFT * speed; }
                if(Input.GetKey(KeyCode.D)) { localTransform.ValueRW.Position += RIGHT * speed; }
            }
        }
    }
}

