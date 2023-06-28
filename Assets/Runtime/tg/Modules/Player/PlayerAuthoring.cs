using UnityEngine;
using Unity.Entities;
using Unity.Rendering;
using Unity.Mathematics;

using Cinemachine;
using UnityEngine.Experimental.Rendering.Universal;

namespace tg.player
{
    using tg.events;
    using tg.player.events;

    public class PlayerAuthoring : MonoBehaviour
    {
        private void Awake()
        {
            CinemachineVirtualCamera vcam               = null;
            var cameraGO                                = new GameObject("Camera");
            {
                cameraGO.transform.SetParent(this.transform);

                // retrieve main camera or create default if non exists
                var camera                              = GameObject.FindFirstObjectByType<UnityEngine.Camera>() ?? new GameObject("Camera").AddComponent<UnityEngine.Camera>();
                {
                    var ccBrain                         = camera.gameObject.GetComponent<CinemachineBrain>() ?? camera.gameObject.AddComponent<CinemachineBrain>();
                    {
                        ccBrain.m_UpdateMethod          = CinemachineBrain.UpdateMethod.FixedUpdate;
                    }

                    var ppCamera                        = camera.gameObject.GetComponent<PixelPerfectCamera>() ?? camera.gameObject.AddComponent<PixelPerfectCamera>();
                    {
                        ppCamera.refResolutionX         = 1920;
                        ppCamera.refResolutionY         = 1080;
                        ppCamera.assetsPPU              = 80;
                        ppCamera.cropFrame              = PixelPerfectCamera.CropFrame.StretchFill;
                    }
                }
                vcam                                    = cameraGO.AddComponent<CinemachineVirtualCamera>();
                {
                    var transposer                      = vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
                    {
                        transposer.m_LookaheadTime      = 0.0f;
                        transposer.m_LookaheadSmoothing = 0.0f;

                        transposer.m_DeadZoneWidth      = 0.0f;
                        transposer.m_DeadZoneHeight     = 0.0f;
                    }

                    var pixelPerfect                    = cameraGO.AddComponent<CinemachinePixelPerfect>();
                    {
                        vcam.AddExtension(pixelPerfect);
                    }

                    var confiner                        = cameraGO.AddComponent<CinemachineConfiner>();
                    {
                        vcam.AddExtension(confiner);

                        confiner.m_Damping              = 0.5f;
                    }

                    vcam.Follow                         = this.transform;
                }
            }
        }
    }
}

