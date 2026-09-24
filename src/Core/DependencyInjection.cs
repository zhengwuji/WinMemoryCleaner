using System;
using System.Collections.Concurrent;
using System.Linq;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Dependency Injection
    /// </summary>
    public static class DependencyInjection
    {
        /// <summary>
        /// IoC Container
        /// </summary>
        public static class Container
        {
            // The container is read from the UI thread and from background timers,
            // so registrations and resolved singletons live in thread-safe maps.
            private static readonly ConcurrentDictionary<Type, Func<object>> _container = new ConcurrentDictionary<Type, Func<object>>();

            // Lazy<T> both defers construction and guarantees that a singleton is
            // materialized exactly once even under concurrent resolution.
            private static readonly ConcurrentDictionary<Type, Lazy<object>> _singleton = new ConcurrentDictionary<Type, Lazy<object>>();

            // Guards registration so two callers cannot register the same type.
            private static readonly object _lock = new object();

            /// <summary>
            /// Registers the specified instance.
            /// </summary>
            /// <typeparam name="TImplementation">The type of the implementation.</typeparam>
            /// <param name="instance">The instance.</param>
            /// <exception cref="InvalidOperationException"></exception>
            public static void Register<TImplementation>(TImplementation instance)
            {
                var key = typeof(TImplementation);

                lock (_lock)
                {
                    if (_container.ContainsKey(key) || _singleton.ContainsKey(key))
                        throw new InvalidOperationException(string.Format(Localizer.Culture, "{0} is already registered.", key.Name));

                    _singleton.TryAdd(key, new Lazy<object>(() => instance));
                }
            }

            /// <summary>
            /// Registers the specified singleton.
            /// </summary>
            /// <typeparam name="TInterface">The type of the interface.</typeparam>
            /// <typeparam name="TImplementation">The type of the implementation.</typeparam>
            /// <param name="singleton">if set to <c>true</c> [singleton].</param>
            /// <exception cref="InvalidOperationException"></exception>
            public static void Register<TInterface, TImplementation>(bool singleton = false) where TImplementation : TInterface
            {
                var key = typeof(TInterface);

                lock (_lock)
                {
                    if (_container.ContainsKey(key) || _singleton.ContainsKey(key))
                        throw new InvalidOperationException(string.Format(Localizer.Culture, "{0} is already registered.", key.Name));

                    if (singleton)
                        _singleton.TryAdd(typeof(TImplementation), new Lazy<object>(() => CreateInstance(typeof(TImplementation))));

                    _container.TryAdd(key, () => Resolve<TImplementation>());
                }
            }

            /// <summary>
            /// Creates an instance of the specified type.
            /// </summary>
            /// <param name="type">The type.</param>
            /// <returns>The created instance.</returns>
            /// <exception cref="InvalidOperationException"></exception>
            private static object CreateInstance(Type type)
            {
                Func<object> func;
                object instance = null;

                if (type.IsInterface && _container.TryGetValue(type, out func))
                    instance = func();

                if (!type.IsInterface && instance == null)
                {
                    var constructor = type.GetConstructors().SingleOrDefault();

                    if (constructor == null)
                        throw new InvalidOperationException(string.Format(Localizer.Culture, "{0} has no public constructor.", type.Name));

                    instance = Activator.CreateInstance(type, constructor.GetParameters().Select(parameter => Resolve(parameter.ParameterType)).ToArray());
                }

                if (instance == null)
                    throw new InvalidOperationException(string.Format(Localizer.Culture, "{0} is not registered.", type.Name));

                return instance;
            }

            /// <summary>
            /// Resolves the specified type.
            /// </summary>
            /// <param name="type">The type.</param>
            /// <returns></returns>
            /// <exception cref="InvalidOperationException"></exception>
            private static object Resolve(Type type)
            {
                if (type == null)
                    throw new ArgumentNullException("type");

                Lazy<object> singleton;

                if (_singleton.TryGetValue(type, out singleton))
                    return singleton.Value;

                return CreateInstance(type);
            }

            /// <summary>
            /// Resolves this instance.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <returns></returns>
            public static T Resolve<T>()
            {
                return (T)Resolve(typeof(T));
            }
        }
    }
}