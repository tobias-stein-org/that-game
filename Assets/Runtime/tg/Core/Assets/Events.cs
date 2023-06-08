using System;

using Unity.Collections;
using Unity.Entities.Serialization;


namespace tg.assets.events
{
    using tg.events;

    public delegate void AssetRequestCompleteCallback(bool hasErrors);

    public struct RequestLoadAssetsEvent : IEvent
    {
        public NativeArray<UntypedWeakReferenceId>      assets;
        public AssetRequestCompleteCallback             onComplete;
    }
}