using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

namespace tg.test.events
{
    public class SystemSpawner : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
            World.DefaultGameObjectInjectionWorld.Unmanaged
                .GetUnsafeSystemRef<FooSystem>(World.DefaultGameObjectInjectionWorld.CreateSystem<FooSystem>())
                .OnUpdate(ref World.DefaultGameObjectInjectionWorld.Unmanaged.GetExistingSystemState<FooSystem>());

            World.DefaultGameObjectInjectionWorld.Unmanaged
                .GetUnsafeSystemRef<IncSystem>(World.DefaultGameObjectInjectionWorld.CreateSystem<IncSystem>())
                ;//.OnUpdate(ref World.DefaultGameObjectInjectionWorld.Unmanaged.GetExistingSystemState<IncSystem>());


            World.DefaultGameObjectInjectionWorld.CreateSystemManaged<BarSystem>().Update();
        }

    }
}

