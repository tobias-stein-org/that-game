using System;

using Force.DeepCloner;

namespace tg.data
{
    public interface IDataContext
    {
        DataAccessKey access { get; }
        DataContextInfo info { get; }

        void initialize(IRuntimeStorageObject storageObject);

        void delete();
    }

    public enum DataDomain
    {
        User,
        App
    }

    public enum DataStorage
    {
        Memory = 0,
        Local = 1,
        Remote = 2
    }

    public enum DataScope
    {
        Game = 0,
        Session = 1,
        Persistent = 2
    }

    public enum DataVersioning
    {
        Ignore = 0,
        Strict = 1,
    }

    public struct DataContextInfo
    {
        public DataDomain       domain;
        public DataStorage      storage;
        public DataScope        scope;
        public DataVersioning   versioning;

        public static DataContextInfo Default = new DataContextInfo
        {
            domain      = DataDomain.App,
            storage     = DataStorage.Memory,
            scope       = DataScope.Session,
            versioning  = DataVersioning.Ignore
        };
    }

    public sealed class DataContext<TData> : IDataContext
    {
        public DataContextInfo          info { get; private set; }
        protected IRuntimeStorageObject storage { get; private set; } = null;
        public DataAccessKey            access { get; private set; } = DataAccessKey.invalid;
        public long                     version { get; private set; } = 0;

        public DataContext(string key, DataContextInfo info)
        {
            this.info           = info;
            this.access         = (info.domain.ToString(), key, typeof(TData));

            RuntimeStorage.access(this, info);
        }

        public void initialize(IRuntimeStorageObject storageObject)
        {
            this.storage        = storageObject;
            this.version        = storageObject != null ? storageObject.version : -1;
        }

        public bool isValid => RuntimeStorage.exists(this);

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

            this.storage.data   = data.DeepClone();
            this.version        = this.storage.version;

            return data;
        }

        public void delete()
        {
            this.storage        = null;
            this.version        = -1;
            RuntimeStorage.delete(this);
        }

        #endregion
    }
}
