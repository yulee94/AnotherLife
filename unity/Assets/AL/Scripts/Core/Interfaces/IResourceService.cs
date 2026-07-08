using System;
using AL.Core;

namespace AL.Core.Interfaces
{
    public interface IResourceService
    {
        long GetResourceCount(ResourceType type);
        void AddResource(ResourceType type, long amount);
        bool ConsumeResource(ResourceType type, long amount);
        bool HasEnough(ResourceType type, long amount);
        void TickProduction(double deltaSeconds);
        event Action<ResourceType, long> OnResourceChanged;
    }
}
