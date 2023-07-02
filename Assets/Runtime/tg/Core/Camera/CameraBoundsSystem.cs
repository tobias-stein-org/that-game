using System.Collections.Generic;

using Unity.Entities;
using UnityEngine;
using Unity.Transforms;

using Cinemachine;

namespace tg.camera
{
    using tg.events;
    using tg.level;
    using tg.level.events;
    using tg.player.events;
    using tg.spawn.events;

    namespace entities
    {
        using tg.player.entities;
        using tg.application.entities;

        /// <summary>
        /// This systems attepts to confine the players camera to the current level bounds.
        /// </summary>
        [CreateAfter(typeof(EventQueue))]
        [UpdateInGroup(typeof(TransformSystemGroup))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        public partial class CameraBoundsSystem : SystemBase, IEventListener<CameraBoundsSystem>
        {
            private GameObject                      cameraLevelBoundsGO     = null;
            private Dictionary<int, BoxCollider2D>  levelChunkBounds        = new Dictionary<int, BoxCollider2D>(16);
            private int                             currentLevelChunk       = LevelData.Chunk.INVALID.id;

            protected override void OnCreate()
            {
                this.RequireAnyForUpdate(StateManager.state(this));
            }

            protected override void OnStartRunning()
            {
                EventQueue.subscribe(this);
            }

            protected override void OnStopRunning()
            {
                EventQueue.unsubscribe(this);
            }


            protected override void OnDestroy()
            {
                this.destroyOldCameraLevelBounds();    
            }

            protected override void OnUpdate()
            {
                if(this.currentLevelChunk == LevelData.Chunk.INVALID.id) { return; }

                foreach(var vcam in SystemAPI.Query<SystemAPI.ManagedAPI.UnityEngineComponent<CinemachineVirtualCamera>>().WithAll<Player>())
                {
                    var transposer                      = vcam.Value.GetCinemachineComponent<CinemachineFramingTransposer>();
                    var chunkBounds                     = this.levelChunkBounds[this.currentLevelChunk];

                    var offset                          = chunkBounds.offset - (UnityEngine.Vector2)vcam.Value.Follow.position;
                    var offsetNormalized                = new Vector2(offset.x / chunkBounds.size.x, offset.y / chunkBounds.size.y);

                    // clamp offset to closest world axis (X or Y)
                    if(Mathf.Abs(offsetNormalized.x) < Mathf.Abs(offsetNormalized.y)) { offset.y = 0.0f; } else { offset.x = 0.0f; }

                    // this will keep the camera focus aligned with the level bounds
                    transposer.m_TrackedObjectOffset    = Vector2.Lerp(transposer.m_TrackedObjectOffset, offset, SystemAPI.Time.DeltaTime * 2);
                }
            }

            private void createNewCameraLevelBounds(in LevelData data)
            {
                this.cameraLevelBoundsGO = new GameObject("Level.bounds");
                {
                    var rigidBody                       = this.cameraLevelBoundsGO.AddComponent<Rigidbody2D>();
                    {
                        rigidBody.bodyType              = RigidbodyType2D.Static;
                        rigidBody.simulated             = false;
                    }

                    var composite                       = this.cameraLevelBoundsGO.AddComponent<CompositeCollider2D>();
                    {
                        composite.isTrigger             = true;
                        composite.geometryType          = CompositeCollider2D.GeometryType.Polygons;
                        composite.vertexDistance        = 0.0f;
                        composite.offsetDistance        = 0.0f;
                    }

                    for(int i = 0; i < data.numChunks; i++)
                    {
                        ref var chunk                   = ref data.getChunk(i);
                        var collider                    = this.cameraLevelBoundsGO.AddComponent<BoxCollider2D>();
                        {
                            collider.offset             = chunk.bounds.center;
                            collider.size               = chunk.bounds.size;
                            collider.usedByComposite    = true;
                        }

                        this.levelChunkBounds.Add(chunk.id, collider);
                    }
                }
            }

            private void destroyOldCameraLevelBounds()
            {
                if(this.cameraLevelBoundsGO != null)
                {
                    GameObject.Destroy(this.cameraLevelBoundsGO);
                }

                this.levelChunkBounds.Clear();
            }

            void onNewLevelGeneratedEvent(NewLevelGeneratedEvent e)
            {
                this.destroyOldCameraLevelBounds();
                this.createNewCameraLevelBounds(in e.levelData);
            }

            void onPlayerLevelChunkChangeEvent(PlayerLevelChunkChangeEvent e)
            {
                this.currentLevelChunk = e.enter;

                // sanity check: if player is still inside the level bounds.
                if(this.currentLevelChunk == LevelData.Chunk.INVALID.id) { return; }
            }

            void onEntitySpawnedEvent(GameObjectSpawnedEvent e)
            {
                if(e.gameObject.GetComponent<tg.player.PlayerAuthoring>() != null)
                {
                    e.gameObject.GetComponentInChildren<CinemachineConfiner>().m_BoundingShape2D = this.cameraLevelBoundsGO.GetComponent<CompositeCollider2D>();
                }
            }
        }
    }
}

