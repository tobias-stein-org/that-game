using System.Text.RegularExpressions;

namespace tg.data
{
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

            this.key = key.ToLower();
            this.info = DataContextInfo.Default;

            this._context = null;
        }

        public DAO(string key, DataDomain domain = DataDomain.App, DataStorage storage = DataStorage.Memory, DataScope scope = DataScope.Session, DataVersioning versioning = DataVersioning.Ignore)
        {
            Unity.Assertions.Assert.IsFalse(string.IsNullOrWhiteSpace(key), "DAO key must be set!");
            Unity.Assertions.Assert.IsTrue(Regex.IsMatch(key, validKeyPattern), $"DAO key '{key}' must follow the namespace notation. Example: 'foo.bar.bazz'");

            this.key        = key.Replace(':', '.');
            this.info       = new DataContextInfo
            {
                domain      = domain,
                storage     = storage,
                scope       = scope,
                versioning  = versioning
            };

            this._context   = null;
        }

        private DataContext<TData> initializeContext()
        {
            Unity.Assertions.Assert.IsFalse(string.IsNullOrWhiteSpace(key), "DAO key must be set! Make sure to provide a valid key in the constructor.");

            this._context = new DataContext<TData>(this.key, this.info);
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

        public bool isValid                                 => this._context != null;

        public static bool operator true(DAO<TData> dao)    => dao.isValid;
        public static bool operator false(DAO<TData> dao)   => !dao.isValid;
    }
}
