using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

using Unity.Entities;

using System;
using Force.DeepCloner;


namespace tg.application
{
    using tg.events;
    using tg.application.events;
    using tg.ui.menu;
    using tg.enemy;
    using System.Diagnostics;
    using System.Text.RegularExpressions;

    namespace entities
    {
        public struct DataAccessKey : IEquatable<DataAccessKey>, IEquatable<int>
        {
            public static DataAccessKey invalid = default;

            public string               domain;
            public string               key;
            public string               type;
                                       
            public int                  hash;

            public DataAccessKey(string domain, string key, Type type)
            {
                this.domain     = domain;
                this.key        = key;
                this.type       = type.FullName;
                this.hash       = -1;
                
                this.hash       = this.GetHashCode();
            }

            public bool             Equals(DataAccessKey other) { return this.hash.Equals(other.hash); }
            public bool             Equals(int other) { return this.hash.Equals(other); }

            public override int     GetHashCode() { return $"{this.domain}:{this.key}".GetHashCode(); }
            public override string  ToString()    { return $"{this.domain}:{this.key}:{this.type}"; }

            public static implicit operator DataAccessKey((string, string, Type) value) { return new DataAccessKey(value.Item1, value.Item2, value.Item3); }
            public static implicit operator int(DataAccessKey dataAccessKey) { return dataAccessKey.hash; }
        }

        public interface IRuntimeStorageObject
        {
            public long         version { get; }
            public object       data    { get; set; }
            public DataScope    scope   { get; }
            public DataStorage  storage { get; }
            public string       domain  { get; }
            public string       key     { get; }
            public string       type    { get; }
            public int          hash    { get; }
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
                private DataContextInfo info;
                private DataAccessKey   access;
                public long             version { get; private set; }

                private object          _data;

                public object           data
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

                public DataScope        scope => this.info.scope;

                public DataStorage      storage => this.info.storage;

                public string           domain => this.access.domain;

                public string           key => this.access.key;

                public int              hash => this.access;

                public string           type => this.access.type;

                public RuntimeStorageObject(IDataContext context, object data)
                {
                    this.info       = context.info;
                    this.access     = context.access;
                    this.version    = 0;
                    this._data      = data;
                }
            }

            private static object                                    mutex  = false;

            public static bool                                       isBusy { get; private set; } = false;

            private static Dictionary<int, IRuntimeStorageObject> storage   = new Dictionary<int, IRuntimeStorageObject>(64);

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
                lock(mutex)
                {
                    isBusy = true;
                    try
                    {
                        // TODO: more sophisticated resource clean up on persistent or local objects.
                        storage.Remove(context.access);
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

                // sync storage

                // clean-up runtime storage
                storage.Clear();
            }

            protected override void OnUpdate()
            {
            }
        }

        public interface IDataContext
        {
            DataAccessKey   access  { get; }
            DataContextInfo info    { get; }

            void    initialize(IRuntimeStorageObject storageObject);

            void    delete();
        }

        public enum DataStorage
        {
            Memory      = 0,
            Local       = 1,
            Remote      = 2
        }

        public enum DataScope
        {
            Game        = 0,
            Session     = 1,
            Persistent  = 2
        }

        public enum DataVersioning
        {
            Ignore      = 0,
            Strict      = 1,
        }

        public struct DataContextInfo
        {
            public DataStorage      storage;
            public DataScope        scope;
            public DataVersioning   versioning;

            public static DataContextInfo Default = new DataContextInfo
            {
                storage     = DataStorage.Memory,
                scope       = DataScope.Session,
                versioning  = DataVersioning.Ignore
            };
        }

        public abstract class DataContext<TData> : IDataContext
        {
            public      DataContextInfo             info    { get; private set; }
            protected   IRuntimeStorageObject       storage { get; private set; }   = null;
            public      DataAccessKey               access  { get; private set; }   = DataAccessKey.invalid;
            public      long                        version { get; private set; }   =   0;

            public DataContext(string domain, string key, DataContextInfo info)
            {
                this.info       = info;
                this.access     = (domain, key, typeof(TData));

                RuntimeStorage.access(this, info);
            }

            public void initialize(IRuntimeStorageObject storageObject)
            {
                this.storage    = storageObject;
                this.version    = storageObject != null ? storageObject.version : -1;
            }

            #region CRUD

            public TData create(TData value)
            {
                RuntimeStorage.create(this, value ?? Activator.CreateInstance<TData>());

                return (TData)this.storage.data.DeepClone();
            }

            public TData read()
            {
                Unity.Assertions.Assert.IsNotNull(this.storage, $"Attempt to read not existing storage object [access key: {this.access.key}]. Object must be created first.");

                this.version        = this.storage.version;
                return (TData)this.storage.data.DeepClone();
            }

            public TData update(TData data)
            {
                Unity.Assertions.Assert.IsNotNull(this.storage, $"Attempt to update not existing storage object [access key: {this.access.key}]. Object must be created first.");

                this.storage.data   = data;
                this.version        = this.storage.version;

                return data;
            }

            public void delete()
            {
                this.storage = null;
                this.version = -1;
                RuntimeStorage.delete(this);
            }

            #endregion
        }

        public sealed class UserData<TData> : DataContext<TData>
        {
            public UserData(string key, DataContextInfo info) : base("user", key, info)
            {
            }
        }

        public sealed class AppData<TData> : DataContext<TData>
        {
            public AppData(string key, DataContextInfo info) : base("app", key, info)
            {
            }
        }

        public struct DAO<TData>
        {
            private const string                validKeyPattern = @"^(?!.*\d\.|.*\._|\._|\.\.|\.\d|\d|_)[a-zA-Z0-9_]+(\.[a-zA-Z0-9_]+)*$";


            private readonly string             key;
            private readonly DataContextInfo    info;

            private DataContext<TData>          _context;
            private DataContext<TData>          context
            {
                get { return this._context ?? this.initializeContext(); }
                set
                {
                    Unity.Assertions.Assert.IsNull(this._context, "Attempt to initialize data context twice!");
                    this._context = value;
                }
            }


            public DAO(string key)
            {
                Unity.Assertions.Assert.IsFalse(string.IsNullOrWhiteSpace(key), "DAO key must be set!");
                Unity.Assertions.Assert.IsTrue(Regex.IsMatch(key, validKeyPattern), $"DAO key '{key}' must follow the namespace notation. Example: 'foo.bar.bazz'");

                this.key        = key.ToLower();
                this.info       = DataContextInfo.Default;

                this._context   = null;
            }

            public DAO(string key, DataStorage storage = DataStorage.Memory, DataScope scope = DataScope.Session, DataVersioning versioning = DataVersioning.Ignore)
            {
                Unity.Assertions.Assert.IsFalse(string.IsNullOrWhiteSpace(key), "DAO key must be set!");
                Unity.Assertions.Assert.IsTrue(Regex.IsMatch(key, validKeyPattern), $"DAO key '{key}' must follow the namespace notation. Example: 'foo.bar.bazz'");

                this.key        = key.Replace(':', '.');
                this.info       = new DataContextInfo
                {
                    storage     = storage,
                    scope       = scope,
                    versioning  = versioning
                };

                this._context = null;
            }

            private DataContext<TData> initializeContext()
            {
                Unity.Assertions.Assert.IsFalse(string.IsNullOrWhiteSpace(key), "DAO key must be set! Make sure to provide a valid key in the constructor.");

                this._context = new AppData<TData>(this.key, this.info);
                return this._context;
            }

            public TData create(TData value = default)  { return this.context.create(value); }
            public TData read()                         { return this.context.read(); }
            public TData update(TData data)             { return this.context.update(data); }
            public void  delete()                       { this.context.delete(); }

            public TData data
            {
                get { return this.read(); }
                set { this.update(value); }
            }
        }

        [UpdateInGroup(typeof(InitializationSystemGroup))]
        [CreateAfter(typeof(EventQueue))]
        public partial class Applicaiton : SystemBase, IEventListener<Applicaiton>
        {
            protected override void OnCreate()
	        {
                EventQueue.subscribe(this);
	        }

            protected override void OnUpdate()
	        {
                this.Enabled = false;
	        }

            protected override void OnStartRunning()
            {
                // Initialize app data ...

                var appData = new ApplicationData();
                var op1 = Addressables.LoadAssetAsync<GameObject>("tg.player");
                op1.Completed += operation => { if(operation.Status == AsyncOperationStatus.Succeeded)
                {
                    appData.playerPrefab = operation.Result; }
                };
                var op2 = Addressables.LoadAssetAsync<GameObject>("tg.enemy");
                op2.Completed += operation => { if(operation.Status == AsyncOperationStatus.Succeeded) { appData.enemyPrefab = operation.Result; } };
                var op3 = Addressables.LoadAssetAsync<EnemyBehaviour>("tg.enemy.defaultBehaviour");
                op3.Completed += operation => { if(operation.Status == AsyncOperationStatus.Succeeded) { appData.defaultEnemyBehaviour = operation.Result; } };
                var op4 = Addressables.LoadAssetAsync<InputActionAsset>("tg.input.actions");
                op4.Completed += operation => { if(operation.Status == AsyncOperationStatus.Succeeded) { appData.inputActions = operation.Result; } };
                var op5 = Addressables.LoadAssetAsync<PanelSettings>("tg.ui.settings");
                op5.Completed += operation => { if(operation.Status == AsyncOperationStatus.Succeeded) { appData.uiSettings = operation.Result; } };

                var loadOp = Addressables.ResourceManager.CreateGenericGroupOperation(new List<AsyncOperationHandle> { op1, op2, op3, op4, op5 }, true);
                loadOp.Completed += operation =>
                {
                    if(operation.Status == AsyncOperationStatus.Succeeded)
                    {
                        World.DefaultGameObjectInjectionWorld.EntityManager.AddComponentObject(World.DefaultGameObjectInjectionWorld.GetExistingSystem<Applicaiton>(), appData);

                        // activate 'tg.input.actions' 
                        appData.inputActions.Enable();

                        EventQueue.publish(new ApplicationInitializedEvent { data = appData });

                        Menu.show(menus.MAIN_MENU, false);
                    }
                };
            }

            public void OnStopRunning(ref SystemState state)
            {
            }

            void onApplicationQuitEvent(RequestApplicationQuitEvent e)
            {
                EventQueue.publish(new QuitApplicationEvent {});
            }

            void onQuitApplicationEvent(QuitApplicationEvent e)
            {
    #if UNITY_EDITOR
                UnityEditor.EditorApplication.ExitPlaymode();
    #else
                UnityEngine.Application.Quit();
    #endif
            }


            /// <summary>
            /// Application internal evnet. Application manager will self induce this event once the "request quit" event has been received.
            /// Having this extra internal event, will give other systems listening to the "request quit" event to perform clean-up.
            /// </summary>
            private struct QuitApplicationEvent : IEvent {}

        }

        public class ApplicationData : IComponentData
        {
            public GameObject       playerPrefab;

            public GameObject       enemyPrefab;

            public EnemyBehaviour   defaultEnemyBehaviour;

            public InputActionAsset inputActions;

            public PanelSettings    uiSettings;
        }
    }
}

