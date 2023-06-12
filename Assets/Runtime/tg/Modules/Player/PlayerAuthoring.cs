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

    public class PlayerAuthoring : MonoBehaviour, IEventListener<PlayerAuthoring>
    {
        /// <summary>
        /// Reference to this players entity.
        /// </summary>
        private Player      player;

        private void Awake()
        {
            EventQueue.subscribe(this);

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

            this.createPlayerEntity();
        }

        private void OnDestroy()
        {
            EventQueue.unsubscribe(this);
            World.DefaultGameObjectInjectionWorld.EntityManager.DestroyEntity(this.player.entity);
        }

        /// <summary>
        /// Listen to spawn events, to fetch the player reference once the earlier spawn request for this player is done.
        /// </summary>
        /// <param name="e"></param>
        void onPlayerSpawnedEvent(PlayerSpawnedEvent e)
        {
            var playerTransform = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentObject<Transform>(e.player.entity);
            if(this.transform == playerTransform)
            {
                this.player = e.player;
            }
        }

        void onKillPlayerEvent(KillPlayerEvent e)
        {
            // kill this player if entity matches
            if(this.player.entity == e.player.entity)
            {
                EventQueue.publish(new PlayerDiedEvent { player = this.player });
                GameObject.Destroy(this.gameObject);
            }
        }

        /// <summary>
        /// Create a new player entity with its components
        /// </summary>
        private void createPlayerEntity()
        {
            var playerEntity = tg.spawn.request.create(out EntityCommandBuffer ECB);
            {
                ECB.AddComponent(playerEntity, new ComponentTypeSet(
                   typeof(Player),
                   typeof(PlayerInputData)
                ));

                ECB.SetComponent<Player>(playerEntity, new Player
                {
                    entity      = playerEntity
                });

                ECB.SetComponent<PlayerInputData>(playerEntity, new PlayerInputData
                {
                    moveSpeed   = 10.0f,
                    moveXY      = UnityEngine.Vector2.zero
                });

                ECB.AddComponent(playerEntity, this.transform);
                ECB.AddComponent(playerEntity, this.GetComponent<Rigidbody2D>());
                ECB.AddComponent(playerEntity, this.GetComponentInChildren<Animator>());
                ECB.AddComponent(playerEntity, this.GetComponentInChildren<CinemachineVirtualCamera>());
            }
        }
    }

     /// <summary>
    /// Added to spawned player entities.
    /// </summary>
    public struct Player : IComponentData
    {
        /// <summary>
        /// Reference to actual player entity.
        /// </summary>
        public Entity                   entity;
    }
}

