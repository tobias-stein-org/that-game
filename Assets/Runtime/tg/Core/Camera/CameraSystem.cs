using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace tg.camera
{
    using tg.player;

    public partial class CameraSystem : SystemBase
    {
        protected override void OnCreate()
        {
        }

        protected override void OnUpdate()
        {
            // nothing to do at the moment, so prevent this system from further updating.
            this.Enabled = false;
        }
    }
}

