using System;
using System.Linq;
using System.Collections.Generic;

using Unity.Entities;

namespace tg.data
{
    using tg.events;
    using tg.game.events;
    using tg.application.events;

    public struct DataAccessKey : IEquatable<DataAccessKey>, IEquatable<int>
    {
        public static DataAccessKey invalid = default;

        public string               domain;
        public string               key;
        public string               type;

        public int                  hash;

        public DataAccessKey(string domain, string key, Type type)
        {
            this.domain             = domain;
            this.key                = key;
            this.type               = type.FullName;
            this.hash               = -1;

            this.hash               = this.GetHashCode();
        }

        public bool Equals(DataAccessKey other) { return this.hash.Equals(other.hash); }
        public bool Equals(int other) { return this.hash.Equals(other); }

        public override int GetHashCode() { return $"{this.domain}:{this.key}".GetHashCode(); }
        public override string ToString() { return $"{this.domain}:{this.key}:{this.type}"; }

        public static implicit operator DataAccessKey((string, string, Type) value) { return new DataAccessKey(value.Item1, value.Item2, value.Item3); }
        public static implicit operator int(DataAccessKey dataAccessKey) { return dataAccessKey.hash; }
    }

    public interface IRuntimeStorageObject
    {
        public long         version { get; }
        public object       data { get; set; }
        public DataScope    scope { get; }
        public DataStorage  storage { get; }
        public DataDomain   domain { get; }
        public string       key { get; }
        public string       type { get; }
        public int          hash { get; }
    }

    public partial class RuntimeStorageGroup : ComponentSystemGroup
    {
        internal const uint UPDATE_RATE_MS = 1000; // every second

        protected override void OnCreate()
        {
            base.OnCreate();
            this.RateManager = new RateUtils.VariableRateManager(RuntimeStorageGroup.UPDATE_RATE_MS, true);
        }
    }

    [CreateAfter(typeof(EventQueue))]
    [UpdateInGroup(typeof(RuntimeStorageGroup))]
    public partial class RuntimeStorage : SystemBase, IEventListener<RuntimeStorage>
    {
        private class RuntimeStorageObject : IRuntimeStorageObject
        {
            private DataContextInfo     info;
            private DataAccessKey       access;
            public long                 version { get; private set; }

            private object              _data;

            public object               data
            {
                get
                {
                    lock(this._data)
                    {
                        return this._data;
                    }
                }
                set
                {
                    lock(this._data)
                    {
                        this._data = value;
                        this.version++;
                    }
                }
            }

            public DataScope            scope   => this.info.scope;

            public DataStorage          storage => this.info.storage;

            public DataDomain           domain  => this.info.domain;

            public string               key     => this.access.key;

            public int                  hash    => this.access;

            public string               type    => this.access.type;

            public RuntimeStorageObject(IDataContext context, object data)
            {
                this.info = context.info;
                this.access = context.access;
                this.version = 0;
                this._data = data;
            }
        }

        private static object mutex = false;

        public static bool isBusy { get; private set; } = false;

        private static Dictionary<int, IRuntimeStorageObject> storage = new Dictionary<int, IRuntimeStorageObject>(64);

        public static bool exists(IDataContext context) { return storage.ContainsKey(context.access); }

        public static void access(IDataContext context, DataContextInfo info)
        {
            lock(mutex)
            {
                isBusy = true;
                try
                {
                    if(storage.TryGetValue(context.access, out IRuntimeStorageObject storageObject))
                    {
                        Unity.Assertions.Assert.AreEqual(storageObject.type, context.access.type, $"Found two runtime storage objects with the same key '{storageObject.domain}:{storageObject.key}', but different tyes ['{storageObject.type}' '{context.access.type}'].");
                    }
                    else
                    {
                        storageObject = null;
                    }

                    context.initialize(storageObject);
                }
                finally
                {
                    isBusy = false;
                }
            }
        }

        public static void create(IDataContext context, object data)
        {
            lock(mutex)
            {
                isBusy = true;
                try
                {
                    Unity.Assertions.Assert.IsFalse(storage.TryGetValue(context.access, out IRuntimeStorageObject storageObject), $"Storage object [access key: {context.access.key}] realdy exits! Object must be delete first or use update to change its state.");

                    storageObject = new RuntimeStorageObject(context, data);
                    storage[context.access] = storageObject;
                    context.initialize(storageObject);
                }
                finally
                {
                    isBusy = false;
                }
            }
        }

        public static void delete(IDataContext context)
        {
            RuntimeStorage.delete(context.access);
        }

        public static void delete(int key)
        {
            lock(mutex)
            {
                isBusy = true;
                try
                {
                    // TODO: more sophisticated resource clean up on persistent or local objects.
                    storage.Remove(key);
                }
                finally
                {
                    isBusy = false;
                }
            }
        }

        protected override void OnCreate()
        {
            EventQueue.subscribe(this);
        }

        protected override void OnStopRunning()
        {
            EventQueue.unsubscribe(this);
        }

        protected override void OnUpdate()
        {
        }

        void onRequestApplicationQuitEvent(RequestApplicationQuitEvent e)
        {
            // sync storage

            // clean-up runtime storage
            storage.Clear();
        }

        void onStartGameEvent(StartGameEvent e)
        {
            foreach(var key in storage.Where(entry => entry.Value.scope == DataScope.Game).Select(entry => entry.Key).ToArray())
            {
                RuntimeStorage.delete(key);
            }
        }
    }
}
