using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace tg.camera
{
    public partial struct CameraSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach(var (data, entity) in SystemAPI.Query<CameraData>().WithAll<CameraDataChanged>().WithEntityAccess())
            {
                state.EntityManager.SetComponentEnabled<CameraDataChanged>(entity, false);
            }

        }
    }


    public partial struct CameraDataChanged : IComponentData, IEnableableComponent
    {
    }
}

