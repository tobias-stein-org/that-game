using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

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


    [CreateAfter(typeof(EventQueue))]
    partial class AssetManager : SystemBase, IEventListener<AssetManager>
    {
        private List<UntypedWeakReferenceId>     loaded;
        private List<RequestLoadAssetsEvent>     pending;
        private List<RequestLoadAssetsEvent>     done;


        //private NativeList<int>
        protected override void OnCreate()
        {
            this.loaded     = new List<UntypedWeakReferenceId>(16);
            this.pending    = new List<RequestLoadAssetsEvent>(16);
            this.done       = new List<RequestLoadAssetsEvent>(16);

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
                this.done.Clear();
                this.done.AddRange(this.pending
                    .Where(request => request.assets.Select(refId => RuntimeContentManager.GetObjectLoadingStatus(refId))
                    .All(status => status == ObjectLoadingStatus.Completed || status == ObjectLoadingStatus.Error)));

                foreach(var request in this.done)
                {
                    this.loaded.AddRange(request.assets);

                    request.onComplete?.Invoke(request.assets.Select(r => RuntimeContentManager.GetObjectLoadingStatus(r)).Any(s => s == ObjectLoadingStatus.Error));

                    request.assets.Dispose();
                    this.pending.Remove(request);
                }
            } 
        }

        void onRequestLoadAssetsEvent(RequestLoadAssetsEvent e)
        {
            if(e.assets.Length > 0)
            {
                lock(this.pending)
                {
                    unsafe
                    {
                        RuntimeContentManager.LoadObjectsAsync((UntypedWeakReferenceId*)e.assets.GetUnsafePtr(), e.assets.Length);
                    }

                    this.pending.Add(e);
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
