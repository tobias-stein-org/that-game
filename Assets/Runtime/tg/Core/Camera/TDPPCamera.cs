using UnityEngine;
using Cinemachine;
using UnityEngine.Experimental.Rendering.Universal;

using Unity.Entities;

namespace tg.camera
{
    /// <summary>
    /// Top-Dow-Pixel-Perfect (TDPP) camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(PixelPerfectCamera))]
    [RequireComponent(typeof(CinemachineBrain))]
    [RequireComponent(typeof(CinemachineVirtualCamera))]
    [RequireComponent(typeof(CinemachinePixelPerfect))]
    [RequireComponent(typeof(CinemachineConfiner))]
    public class TDPPCamera : MonoBehaviour
    {
        public Vector2Int   referenceResolution     = new Vector2Int(1920, 1080);
        public int          tilingFactor            = 40;

        public class Baker : Baker<TDPPCamera>
        {
            public override void Bake(TDPPCamera authoring)
            {
                var ppCamera                = authoring.GetComponent<PixelPerfectCamera>();
                ppCamera.refResolutionX     = authoring.referenceResolution.x;
                ppCamera.refResolutionY     = authoring.referenceResolution.y;
                ppCamera.assetsPPU          = authoring.tilingFactor;

                var cameraEntity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponentObject(cameraEntity, new CameraData
                {
                    camera                  = authoring.GetComponent<CinemachineVirtualCamera>(),
                    confiner                = authoring.GetComponent<CinemachineConfiner>()
                });

                AddComponent(cameraEntity, new CameraDataChanged {});
            }
        }
    }

    public partial class CameraData : IComponentData
    {
        public CinemachineVirtualCamera camera;
        public CinemachineConfiner      confiner;
    }
}
