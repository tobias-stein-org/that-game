using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
using Unity.Entities;
using Cinemachine;


namespace tg.player
{
    /// <summary>
    /// Authoring component for player data.
    /// </summary>
    public class PlayerAuthoring : MonoBehaviour
    {
        /// <summary>
        /// Reference to the player prefab, which will be instanciated for a player entity.
        /// </summary>
        public GameObject   prefab;


        public class Baker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                if(!Application.isPlaying) { return; }

                // Note: Since Cinemachine does not provided a DOTS compatible implementation, yet. We need to
                // create a 'companion' GameObject for the player which can be followed be a virtual Cinemachine camera.
                // The transform of this companion GameObject will be updated via a system.

                var companionPlayer                 = new GameObject("Companion-Player");
                {
                    companionPlayer.tag             = "Player";
                }

                // Note: We also use this baker to initially setup a pixel=perfect camera.
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
                            ppCamera.assetsPPU      = 40;
                            ppCamera.cropFrame      = PixelPerfectCamera.CropFrame.StretchFill;
                        }
                    }
                    vcam                            = companionPlayerCamera.AddComponent<CinemachineVirtualCamera>();
                    {
                        var transposer              = vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
                        {
                        }

                        var pixelPerfect            = companionPlayerCamera.AddComponent<CinemachinePixelPerfect>();
                        {
                            vcam.AddExtension(pixelPerfect);
                        }

                        var confiner                = companionPlayerCamera.AddComponent<CinemachineConfiner>();
                        {
                            vcam.AddExtension(confiner);
                        }

                        vcam.Follow                 = companionPlayer.transform;
                    }
                }

                // entity that holds the player data, since its not going to move or render transform canm be set None.
                var playerData = GetEntity(TransformUsageFlags.None);
                var playerPref = GetEntity(authoring.prefab, TransformUsageFlags.Dynamic);
                AddComponentObject(playerData, new PlayerData
                {
                    companion                       = companionPlayer,
                    camera                          = vcam,
                    prefab                          = playerPref,
                    isAlive                         = false
                });
            }
        }
    }

    public class PlayerData : IComponentData
    {
        /// <summary>
        /// A GameObject companion object to mirror the player entities movement.
        /// </summary>
        public GameObject               companion;

        /// <summary>
        /// Reference to the player companions camera.
        /// </summary>
        public CinemachineVirtualCamera camera;

        /// <summary>
        /// player model.
        /// </summary>
        public Entity                   prefab;

        public bool                     isAlive;
    }

    /// <summary>
    /// Added to spawned player entities.
    /// </summary>
    public struct Player : IComponentData
    {
        public Entity                   playerData;
    }
}
