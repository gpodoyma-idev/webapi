namespace CannonicalRESTWebApp.Infrastructure
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;

    using CannonicalRESTWebApp.Models;

    using Microsoft.ApplicationServer.Http.Dispatcher;

    public class SampleResourceRepository : IResourceRepository<int, Sample>
    {
        private readonly ConcurrentDictionary<int, Sample> repository = new ConcurrentDictionary<int, Sample>();

        #region IResourceRepository<int,HttpResource> Members

        public Sample Delete(int resourceKey, Action<Sample> checkPreCondition)
        {
            // Need to Get, Check PreCondition and Remove in one atomic operation
            lock (this.repository)
            {
                var resource = this.Get(resourceKey);

                if (resource != null)
                {
                    // Will throw if pre-condition fails
                    checkPreCondition(resource);

                    this.repository.TryRemove(resourceKey, out resource);
                }

                return resource;
            }
        }

        public Sample Get(int resourceKey)
        {
            Sample result;
            this.repository.TryGetValue(resourceKey, out result);

            // If not found, returns null
            return result;
        }

        public Sample[] GetResources(int skip, int take)
        {
            return this.repository.Values.Skip(skip).Take(take).ToArray();
        }

        public Sample Post(Sample sample)
        {
            // Sanitize the data provided by the caller using the version the caller supplied
            var sanitizedResource = Sample.CreateSanitizedResource(this.GenerateId(), sample, SampleResourceVersionOption.New);

            // Check to see if the resource that is being added has a conflict with an existing resource
            // For example, you might not allow the same email address more than once.
            if (this.ResourceConflict(sanitizedResource))
            {
                throw new HttpResponseException(HttpStatusCode.Conflict);
            }

            if (this.repository.TryAdd(sanitizedResource.Key, sanitizedResource))
            {
                return sanitizedResource;
            }

            throw new HttpResponseException(HttpStatusCode.InternalServerError);
        }

        public bool ResourceConflict(Sample sample)
        {
            // look for other resources with the same data
            return this.repository.Contains(new KeyValuePair<int, Sample>(sample.Key, sample), new SampleResourceConflictComparer());
        }

        public Sample Put(int resourceKey, Sample toPut, Sample comparison)
        {
            var sanitizedResource = Sample.CreateSanitizedResource(resourceKey, toPut, SampleResourceVersionOption.UseExisting);

            if (sanitizedResource.IsValid() && comparison.DataChanged(sanitizedResource))
            {
                if (this.repository.TryUpdate(resourceKey, sanitizedResource, comparison))
                {
                    sanitizedResource.UpdateVersion();
                    return sanitizedResource;
                }
                return null;
            }
            return sanitizedResource;
        }

        public Sample AddOrUpdate(int resourceKey, Sample resource, Action<Sample> onAdd, Action<Sample> onUpdate)
        {
            return this.repository.AddOrUpdate(resourceKey, 
                // If resource was not found
                // This delegate will return a resource to add to the store
                key =>
                    {
                        var sanitizedResource = Sample.CreateSanitizedResource(key, resource, SampleResourceVersionOption.New);

                        if (onAdd != null)
                        {
                            onAdd(sanitizedResource);
                        }

                        return sanitizedResource;
                    },
                // If the resource was found
                // This delegate will update the resource found based on the caller provided resource
                (key, existingResource) =>
                    {
                        if (onUpdate != null)
                        {
                            onUpdate(existingResource);
                        }

                        var sanitizedResource = Sample.CreateSanitizedResource(key, resource, SampleResourceVersionOption.UseExisting);

                        // Because PUT requests are Idempotent (multiple calls yield the same result)
                        // Don't change the version of the resource unless the data is really changed
                        return existingResource.UpdateFrom(sanitizedResource);
                    });
        }

        public IList<Sample> Resources
        {
            get
            {
                return new ReadOnlyCollection<Sample>(this.repository.Values.ToList());
            }
        }

        #endregion

        private int GenerateId()
        {
            return (from r in this.Resources select r.Key).Max() + 1;
        }

        public static SampleResourceRepository Get()
        {
            var resourceRepository = new SampleResourceRepository();

            for (var i = 1; i <= 20; i++)
            {
                resourceRepository.repository.AddOrUpdate(i, new Sample { Key = i, Data = "HttpResource" + i }, (key, existing) => existing);
            }

            return resourceRepository;
        }
    }
}