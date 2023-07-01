using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.AddressableAssets;

using Unity.Scenes;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace tg.assets
{
    using tg.events;
    using tg.assets.events;


    public struct WeakAssetReference<TObject> : IEquatable<WeakAssetReference<TObject>> where TObject : UnityEngine.Object
    {
	    [SerializeField]
	    internal UntypedWeakReferenceId id;

	    public bool isValid
        {
		    get
            {
			    if (!id.IsValid) {
				    return false;
			    }
			    if (id.GenerationType != WeakReferenceGenerationType.UnityObject) {
				    return false;
			    }
#if UNITY_EDITOR
			    if (UntypedWeakReferenceId.GetEditorObject (id) == (UnityEngine.Object)null) {
				    return false;
			    }
#endif
			    return true;
		    }
	    }

	    public ObjectLoadingStatus loadingStatus => RuntimeContentManager.GetObjectLoadingStatus (in id);

	    public TObject result => RuntimeContentManager.GetObjectValue<TObject> (id);

#if UNITY_EDITOR
	    public WeakAssetReference (TObject unityObject) { id = UntypedWeakReferenceId.CreateFromObjectInstance ((UnityEngine.Object)(object)unityObject); }
#endif

	    public WeakAssetReference (UntypedWeakReferenceId id) { this.id = id; }

	    public bool Equals (WeakAssetReference<TObject> other) { return id.Equals (other.id); }

	    public override int GetHashCode () { return id.GetHashCode (); }

        public static implicit operator WeakObjectReference<TObject>(WeakAssetReference<TObject> weakAssetReference)    => new WeakObjectReference<TObject>(weakAssetReference.id);
        public static implicit operator UntypedWeakReferenceId(WeakAssetReference<TObject> weakAssetReference)          => weakAssetReference.id;
        public static implicit operator TObject(WeakAssetReference<TObject> weakAssetReference)                         => RuntimeContentManager.GetObjectValue<TObject> (weakAssetReference.id);
    }

    namespace entities
    {
        [CreateAfter(typeof(EventQueue))]
        partial class AssetManager : SystemBase, IEventListener<AssetManager>
        {
            private interface ILoadingRequest
            {
                bool finsihed { get; }
                bool hasErorr { get; }
            }

            private struct LoadingRequest
            {
                public Func<bool>                           finsihed;
                public Func<bool>                           hasErorr;

                public NativeArray<UntypedWeakReferenceId>  assets;
                public AssetRequestCompleteCallback         onComplete;
            }

            private List<UntypedWeakReferenceId>            loaded;
            private List<LoadingRequest>                    pending;
            private List<LoadingRequest>                    complete;


            //private NativeList<int>
            protected override void OnCreate()
            {
                this.loaded     = new List<UntypedWeakReferenceId>(16);
                this.pending    = new List<LoadingRequest>(16);
                this.complete   = new List<LoadingRequest>(16);

                EventQueue.subscribe(this);
            }

            protected override void OnDestroy()
            {
                foreach(var asset in this.loaded) { RuntimeContentManager.ReleaseObjectAsync(asset); }
                this.loaded.Clear();

                foreach(var request in this.pending) { if(request.assets.IsCreated) { request.assets.Dispose(); } }
                this.pending.Clear();

                this.loaded = null;
                this.pending = null;
            }

            protected override void OnUpdate()
            {
                
                if(this.pending.Count == 0) { return; }

                lock(this.pending)
                {
                    this.complete.Clear();
                    this.complete.AddRange(this.pending.Where(req => req.finsihed()));

                    foreach(var request in this.complete)
                    {
                        this.loaded.AddRange(request.assets);

                        request.onComplete?.Invoke(request.hasErorr());

                        request.assets.Dispose();
                        this.pending.Remove(request);
                    }
                } 
            }

            private static readonly ObjectLoadingStatus[] RequestCompleteState = new ObjectLoadingStatus[] { ObjectLoadingStatus.Completed, ObjectLoadingStatus.Error };
            void onRequestLoadAssetsEvent(RequestLoadAssetsEvent e)
            {
                if(e.assets.Length > 0)
                {
                    lock(this.pending)
                    {
                        var finsihed = new List<Func<bool>>(e.assets.Length);
                        var hasError = new List<Func<bool>>(e.assets.Length);

                        foreach(var id in e.assets)
                        {
                            switch(id.GenerationType)
                            {
                                case WeakReferenceGenerationType.GameObjectScene:
                                {
                                    var sceneEntity = SceneSystem.LoadSceneAsync(this.World.Unmanaged, id.GlobalId.AssetGUID, new SceneSystem.LoadParameters
                                    {
                                        AutoLoad    = true,
                                        Flags       = SceneLoadFlags.LoadAdditive
                                    });

                                    
                                    finsihed.Add(new Func<bool>(() => SceneSystem.IsSceneLoaded(this.World.Unmanaged, sceneEntity)));
                                    hasError.Add(new Func<bool>(() =>
                                    {
                                    
                                        if(SceneSystem.GetSceneStreamingState(this.World.Unmanaged, sceneEntity) != SceneSystem.SceneStreamingState.LoadedSuccessfully)
                                        {
                                            Debug.LogError($"{sceneEntity} is invalid.");
                                            return true;
                                        }

                                        return false;
                                    }));

                                    break;
                                }

                                default:
                                {
                                    RuntimeContentManager.LoadObjectAsync(in id);
                                    finsihed.Add(new Func<bool>(() => RequestCompleteState.Contains(RuntimeContentManager.GetObjectLoadingStatus(id))));
                                    hasError.Add(new Func<bool>(() =>
                                    {
                                        if(RuntimeContentManager.GetObjectLoadingStatus(id) == ObjectLoadingStatus.Error)
                                        {
                                            Debug.LogError($"Failed to load {id}.");
                                            return true;
                                        }

                                        return false;
                                    }));
                                    break;
                                }
                            }
                        }

                        this.pending.Add(new LoadingRequest
                        {
                            finsihed    = new Func<bool>(() => finsihed.All(func => func())),
                            hasErorr    = new Func<bool>(() => hasError.Any(func => func())),

                            assets      = e.assets,
                            onComplete  = e.onComplete
                        });
                    }
                } 
            }
        }
    }


    public static class reqeust
    {
        public static void load(UntypedWeakReferenceId reference, AssetRequestCompleteCallback onComplete = null)
        {
            reqeust.load(new[] { reference }, onComplete);
        }

        public static void load(IEnumerable<UntypedWeakReferenceId> references, AssetRequestCompleteCallback onComplete = null)
        {
            EventQueue.publish(new RequestLoadAssetsEvent
            {
                assets      = new NativeArray<UntypedWeakReferenceId>(references.ToArray(), Allocator.Persistent),
                onComplete  = onComplete
            });
        }
    }
}
