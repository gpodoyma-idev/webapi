namespace CannonicalRESTWebApp.Infrastructure
{
    using System;
    using System.Collections.Generic;

    interface IResourceRepository<in TKey, TResource>
    {
        TResource AddOrUpdate(
            TKey resourceKey, 
            TResource resource, 
            Action<TResource> onAdd,
            Action<TResource> onUpdate);

        TResource Delete(
            TKey resourceKey, 
            Action<TResource> checkPreCondition);

        TResource Get(TKey resourceKey);

        TResource[] GetResources(TKey skip, TKey take);

        TResource Post(TResource resource);

        TResource Put(
            TKey resourceKey, 
            TResource toPut, 
            TResource comparison);

        bool ResourceConflict(TResource sample);

        IList<TResource> Resources { get; }
    }
}
