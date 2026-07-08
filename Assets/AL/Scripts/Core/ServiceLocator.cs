using System;
using System.Collections.Generic;

namespace AL.Core
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        public static void Register<T>(T service)
        {
            var type = typeof(T);
            if (Services.ContainsKey(type))
            {
                Services[type] = service;
            }
            else
            {
                Services.Add(type, service);
            }
        }

        public static T Get<T>()
        {
            var type = typeof(T);
            if (Services.TryGetValue(type, out var service))
            {
                return (T)service;
            }
            throw new Exception($"Service of type {type} not registered.");
        }
    }
}
