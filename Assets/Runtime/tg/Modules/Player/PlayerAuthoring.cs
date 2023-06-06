using UnityEngine;
using Unity.Entities;
using Unity.Rendering;
using Unity.Mathematics;

using Cinemachine;
using UnityEngine.Experimental.Rendering.Universal;

namespace tg.player
{
    public class PlayerAuthoring : MonoBehaviour
    {
        private void Awake()
        {
            CinemachineVirtualCamera vcam       = null;
            var companionPlayerCamera           = new GameObject("Companion-Player-Camera");
            {
                // retrieve main camera or create default if non exists
                var camera                      = GameObject.FindFirstObjectByType<UnityEngine.Camera>() ?? new GameObject("Camera").AddComponent<UnityEngine.Camera>();
                {
                    var ccBrain                 = camera.gameObject.GetComponent<CinemachineBrain>() ?? camera.gameObject.AddComponent<CinemachineBrain>();
                    var ppCamera                = camera.gameObject.GetComponent<PixelPerfectCamera>() ?? camera.gameObject.AddComponent<PixelPerfectCamera>();
                    {
                        ppCamera.refResolutionX = 1920;
                        ppCamera.refResolutionY = 1080;
                        ppCamera.assetsPPU      = 80;
                        ppCamera.cropFrame      = PixelPerfectCamera.CropFrame.StretchFill;
                    }
                }
                vcam                            = companionPlayerCamera.AddComponent<CinemachineVirtualCamera>();
                {
                    var transposer              = vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
                    {
                        transposer.m_LookaheadTime      = 0.2f;
                        transposer.m_LookaheadSmoothing = 4.0f;

                        transposer.m_DeadZoneWidth      = 0.0f;
                        transposer.m_DeadZoneHeight     = 0.0f;
                    }

                    var pixelPerfect            = companionPlayerCamera.AddComponent<CinemachinePixelPerfect>();
                    {
                        vcam.AddExtension(pixelPerfect);
                    }

                    var confiner                = companionPlayerCamera.AddComponent<CinemachineConfiner>();
                    {
                        vcam.AddExtension(confiner);
                    }

                    vcam.Follow                 = this.transform;
                }
            }
        }
    }
}

