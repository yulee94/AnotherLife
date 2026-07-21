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

        public static bool TryGet<T>(out T service)
        {
            if (TryGet(typeof(T), out var existing) && existing is T typed)
            {
                service = typed;
                return true;
            }

            service = default;
            return false;
        }

        public static bool IsRegistered<T>()
        {
            return Services.ContainsKey(typeof(T));
        }

        internal static bool TryGet(Type type, out object service)
        {
            if (type == null)
            {
                service = null;
                return false;
            }

            return Services.TryGetValue(type, out service);
        }

        internal static bool ContainsAny(IEnumerable<Type> serviceTypes)
        {
            foreach (var serviceType in serviceTypes)
            {
                if (serviceType != null && Services.ContainsKey(serviceType))
                {
                    return true;
                }
            }

            return false;
        }

        internal static ServicePublicationResult PublishBatch(
            IReadOnlyList<ServiceRegistrationEntry> registrations,
            Func<Type, object, bool> beforeRegister = null)
        {
            if (registrations == null || registrations.Count == 0)
            {
                return ServicePublicationResult.Failed(null, "No registrations were supplied.");
            }

            var seenTypes = new HashSet<Type>();
            var snapshot = new Dictionary<Type, object>();
            var hadExisting = new HashSet<Type>();

            foreach (var registration in registrations)
            {
                if (registration.ServiceType == null)
                {
                    return ServicePublicationResult.Failed(null, "A registration type was null.");
                }

                if (!seenTypes.Add(registration.ServiceType))
                {
                    return ServicePublicationResult.Failed(registration.ServiceType, $"Duplicate registration for {registration.ServiceType.FullName}.");
                }

                if (registration.Instance == null)
                {
                    return ServicePublicationResult.Failed(registration.ServiceType, "A registration instance was null.");
                }

                if (!registration.ServiceType.IsInstanceOfType(registration.Instance))
                {
                    return ServicePublicationResult.Failed(registration.ServiceType, $"Registration instance {registration.Instance.GetType().FullName} does not implement {registration.ServiceType.FullName}.");
                }

                if (Services.TryGetValue(registration.ServiceType, out var existing))
                {
                    hadExisting.Add(registration.ServiceType);
                    snapshot[registration.ServiceType] = existing;
                }
            }

            var transaction = new ServicePublicationTransaction(registrations, snapshot, hadExisting);

            try
            {
                foreach (var registration in registrations)
                {
                    if (beforeRegister != null && !beforeRegister(registration.ServiceType, registration.Instance))
                    {
                        throw new InvalidOperationException($"Publication was rejected for {registration.ServiceType.FullName}.");
                    }

                    Services[registration.ServiceType] = registration.Instance;
                }

                return ServicePublicationResult.Success(transaction);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return ServicePublicationResult.Failed(null, ex.Message);
            }
        }

        internal sealed class ServicePublicationTransaction
        {
            private readonly IReadOnlyList<ServiceRegistrationEntry> _registrations;
            private readonly IReadOnlyDictionary<Type, object> _snapshot;
            private readonly HashSet<Type> _hadExisting;
            private bool _completed;

            public ServicePublicationTransaction(
                IReadOnlyList<ServiceRegistrationEntry> registrations,
                IReadOnlyDictionary<Type, object> snapshot,
                IReadOnlyCollection<Type> hadExisting)
            {
                _registrations = registrations ?? Array.Empty<ServiceRegistrationEntry>();
                _snapshot = new Dictionary<Type, object>(snapshot ?? new Dictionary<Type, object>());
                _hadExisting = new HashSet<Type>(hadExisting ?? Array.Empty<Type>());
            }

            public void Commit()
            {
                _completed = true;
            }

            public void Rollback()
            {
                if (_completed)
                {
                    return;
                }

                foreach (var registration in _registrations)
                {
                    if (registration.ServiceType == null)
                    {
                        continue;
                    }

                    if (_hadExisting.Contains(registration.ServiceType))
                    {
                        Services[registration.ServiceType] = _snapshot[registration.ServiceType];
                    }
                    else if (Services.TryGetValue(registration.ServiceType, out var current) &&
                             ReferenceEquals(current, registration.Instance))
                    {
                        Services.Remove(registration.ServiceType);
                    }
                }

                _completed = true;
            }
        }
    }

    internal readonly struct ServiceRegistrationEntry
    {
        public ServiceRegistrationEntry(Type serviceType, object instance)
        {
            ServiceType = serviceType;
            Instance = instance;
        }

        public Type ServiceType { get; }
        public object Instance { get; }
    }

    internal readonly struct ServicePublicationResult
    {
        private ServicePublicationResult(bool succeeded, Type serviceType, string message, ServiceLocator.ServicePublicationTransaction transaction)
        {
            Succeeded = succeeded;
            ServiceType = serviceType;
            Message = message ?? string.Empty;
            Transaction = transaction;
        }

        public bool Succeeded { get; }
        public Type ServiceType { get; }
        public string Message { get; }
        public ServiceLocator.ServicePublicationTransaction Transaction { get; }

        public static ServicePublicationResult Success(ServiceLocator.ServicePublicationTransaction transaction)
        {
            return new ServicePublicationResult(true, null, string.Empty, transaction);
        }

        public static ServicePublicationResult Failed(Type serviceType, string message)
        {
            return new ServicePublicationResult(false, serviceType, message, null);
        }
    }
}
