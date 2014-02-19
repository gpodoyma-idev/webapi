namespace CannonicalRESTWebApp.Resources
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.ServiceModel;
    using System.ServiceModel.Web;
    using System.Text;

    using CannonicalRESTWebApp.Infrastructure;
    using CannonicalRESTWebApp.Models;

    using Microsoft.ApplicationServer.Http;
    using Microsoft.ApplicationServer.Http.Dispatcher;

    using WebAPI.Helpers;

    [ServiceContract]
    public class SampleResource
    {
        #region Constants and Fields

        private static IResourceRepository<int, Sample> repository = SampleResourceRepository.Get();

        #endregion

        #region Public Methods

        /// <summary>
        ///   Deletes a resource
        /// </summary>
        /// <param name = "request">The Http Request</param>
        /// <param name = "key">The resource key</param>
        /// <returns>The deleted resource if found, otherwise no content</returns>
        /// <remarks>
        ///   DELETE Spec http://www.w3.org/Protocols/rfc2616/rfc2616-sec9.html#sec9.7
        /// 
        ///   DELETE SHOULD delete an entity that exists and return 200-OK with the deleted entity or 204-No Content if the response does not include the entity
        ///   DELETE SHOULD be idempotent
        ///   DELETE SHOULD return with 412-PreconditionFailed if no matching entity for If-Match etag 
        ///   DELETE SHOULD succeed if matching entity for If-Match etag 
        ///   DELETE SHOULD succeed if wildcard used in If-Match etag
        ///   DELETE SHOULD return 202-Accepted if the request to delete has not been enacted
        ///   DELETE SHOULD return 400-BadRequest if the key is invalid
        /// 
        ///   Note: This implementation supports a special key value "all" which will delete and recreate the respository for testing purposes
        /// </remarks>
        [WebInvoke(UriTemplate = "{key}", Method = "DELETE")]
        public HttpResponseMessage<Sample> Delete(HttpRequestMessage request, string key)
        {
            // Special case - delete everything
            if (key == "all")
            {
                // Reinitialize the repository
                repository = SampleResourceRepository.Get();
                return new HttpResponseMessage<Sample>(HttpStatusCode.NoContent);
            }

            RequestValidator.Validate(key);

            // Delete is an Idempotent method in HTTP
            // If you delete the same ID multiple times the result should be the same
            // Therefore if the item does not exist, do not return HttpStatusCode.NotFound
            // See http://www.w3.org/Protocols/rfc2616/rfc2616-sec9.html

            var resource = repository.Delete(ParseResourceKey(key), r => CheckConditionalUpdate(request, r));

            // If no resource was not found (because it was previously deleted), return No Content
            return resource == null ? new HttpResponseMessage<Sample>(HttpStatusCode.NoContent) : new HttpResponseMessage<Sample>(resource);
        }

        /// <summary>
        ///   Gets a collection of sample resources
        /// </summary>
        /// <returns>A set of resources</returns>
        /// <remarks>
        ///   GET Spec http://www.w3.org/Protocols/rfc2616/rfc2616-sec9.html#sec9.3
        /// GET MUST return resources in the collection 
        /// GET SHOULD support OData URI Conventions http://www.odata.org/developers/protocols/uri-conventions
        /// </remarks>
        [WebGet(UriTemplate = "")]
        public IQueryable<Sample> GetAll()
        {
            return repository.Resources.AsQueryable();
        }

        /// <summary>
        /// </summary>
        /// <param name = "request"></param>
        /// <param name = "key"></param>
        /// <returns></returns>
        /// <remarks>
        ///   GET Spec http://www.w3.org/Protocols/rfc2616/rfc2616-sec9.html#sec9.3
        /// 
        ///   GET MUST return a resource given a key if the resource with that key exists
        ///   GET MUST return 400-BadRequest if the key is invalid
        ///   GET MUST return 404-NotFound if the key is not found
        ///   GET MUST return 304-NotModified if Conditional GET conditions are met using If-None-Match 
        ///   GET SHOULD return an ETag header
        ///   Note: To Get multiple samples see the SamplesResource.Get method
        /// </remarks>
        [WebGet(UriTemplate = "{key}")]
        public HttpResponseMessage<Sample> Get(HttpRequestMessage request, string key)
        {
            // Note: Validate your arguments and return BadRequest (400) if invalid
            RequestValidator.Validate(key);

            var resource = repository.Get(ParseResourceKey(key));

            // Note: Return 404 - Not Found if you can't find the resource
            if (resource == null)
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            // Note: Check for ETag - return NotModified (304) if match
            CheckIfNoneMatch(request, resource);

            return CreateResponseWithETag(resource);
        }

        /// <summary>
        ///   Adds a new sample to the Samples resource
        /// </summary>
        /// <param name = "request">The Http Request message</param>
        /// <param name = "sample">The sample item</param>
        /// <returns>The created sample item</returns>
        /// <remarks>
        ///   POST Spec http://www.w3.org/Protocols/rfc2616/rfc2616-sec9.html#sec9.5
        ///   POST MUST append a valid resource to the resource collection using a server generated key and return 201 – Created 
        ///   with a location header, entity tag and entity body
        /// </remarks>
        [WebInvoke(UriTemplate = "/", Method = "POST")]
        public HttpResponseMessage<Sample> Post(HttpRequestMessage<Sample> request, Sample sample)
        {
            RequestValidator.Validate(sample);

            return CreatedSampleResponse(request, repository.Post(sample));
        }

        /// <summary>
        ///   PUT implementation which replaces the existing state with new state
        /// </summary>
        /// <param name = "request">The HTTP request</param>
        /// <param name = "key">The key of the sample resource</param>
        /// <returns>The updated sample resource</returns>
        /// <remarks>
        ///   PUT Spec http://www.w3.org/Protocols/rfc2616/rfc2616-sec9.html#sec9.6
        /// 
        ///   PUT MUST Update the entity identified by the URI if it exists and return 200-OK with the modified entity and etag header
        ///   PUT MAY Add a new entity using the key provided in the URI and return 201-Created with entity location and etag
        ///   PUT MUST respect the Precondition If-Match
        ///   PUT MUST be Idempotent 
        ///   PUT MUST NOT alter the key of the entity so that it does not match the key of the URI
        ///   PUT MUST return 400-BadRequest if the entity is invalid
        ///   PUT MUST return 400-BadRequest if the key is invalid
        ///   PUT MUST ignore writes to entity fields the server considers read only
        ///   PUT MUST return 404-NotFound if the server does not allow new entities to be added with PUT
        /// </remarks>
        [WebInvoke(UriTemplate = "{key}", Method = "PUT")]
        public HttpResponseMessage<Sample> Put(HttpRequestMessage request, int key)
        {
            var resourceToPut = request.Content.ReadAs<Sample>();

            // Validate arguments
            RequestValidator.Validate(key, resourceToPut);

            // Get the old version of the resource for comparison
            var existingResource = repository.Get(key);

            // Can't find it
            if (existingResource == null)
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            CheckConditionalUpdate(request, existingResource);

            var updatedResource = repository.Put(key, resourceToPut, existingResource);

            return CreateResponseWithETag(updatedResource);
        }

        /// <summary>
        ///   PUT implementation which adds a new resource if the key does not exist or replaces the existing state with new state if the key does exist
        /// </summary>
        /// <param name = "request">The HTTP request message</param>
        /// <param name = "key">The key of the resource</param>
        /// <returns>The updated sample resource</returns>
        /// <remarks>
        ///   PUT Spec http://www.w3.org/Protocols/rfc2616/rfc2616-sec9.html#sec9.6
        /// 
        ///   PUT MUST Update the entity identified by the URI if it exists and return 200-OK with the modified entity and etag header
        ///   PUT MAY Add a new entity using the key provided in the URI and return 201-Created with entity location and etag
        ///   PUT MUST respect the Precondition If-Match
        ///   PUT MUST be Idempotent 
        ///   PUT MUST NOT alter the key of the entity so that it does not match the key of the URI
        ///   PUT MUST return 400-BadRequest if the entity is invalid
        ///   PUT MUST return 400-BadRequest if the key is invalid
        ///   PUT MUST ignore writes to entity fields the server considers read only
        ///   PUT MUST return 404-NotFound if the server does not allow new entities to be added with PUT
        ///   Note: Because this sample shows both implementations of PUT, this one is at a different URI
        /// </remarks>
        [WebInvoke(UriTemplate = "AddOrUpdate/{key}", Method = "PUT")]
        public HttpResponseMessage<Sample> PutAddOrUpdate(HttpRequestMessage request, int key)
        {
            var resourceToPut = request.Content.ReadAs<Sample>();

            // Validate arguments
            RequestValidator.Validate(key, resourceToPut);

            var add = false;

            var resource = repository.AddOrUpdate(key, resourceToPut, resourceToAdd => add = true, existingResource => CheckConditionalUpdate(request, existingResource));

            return add ? CreatedSampleResponse(request, resource) : CreateResponseWithETag(resource);
        }

        #endregion

        #region Methods

        internal static HttpResponseMessage<Sample> CreatedSampleResponse(HttpRequestMessage request, Sample toAdd)
        {
            var response = new HttpResponseMessage<Sample>(toAdd) { StatusCode = HttpStatusCode.Created };

            // Set the status code: "201 - Created" and the absolute URI of the new resource
            response.Headers.Location = CreateLocationUri(request, toAdd);
            response.Headers.ETag = new EntityTagHeaderValue((QuotedString)(toAdd.Tag));
            return response;
        }

        private static void CheckConditionalUpdate(HttpRequestMessage request, Sample resource)
        {
            //if (request == null)
            //{
            //    throw new ArgumentNullException("request");
            //}

            //if (resource == null)
            //{
            //    throw new ArgumentNullException("resource");
            //}

            // No etags
            if (request.Headers.IfMatch.Count == 0)
            {
                return;
            }

            // If there is no matching etag, the pre-condition fails
            if (!request.Headers.IfMatch.Any(etag => IsMatchingTag(resource, etag.Tag)))
            {
                throw new HttpResponseException(HttpStatusCode.PreconditionFailed);
            }
        }

        private static void CheckIfNoneMatch(HttpRequestMessage request, Sample resource)
        {
            if (request.Headers.IfNoneMatch.Any(etag => IsMatchingTag(resource, etag.Tag)))
            {
                throw new HttpResponseException(HttpStatusCode.NotModified);
            }
        }

        private static HttpResponseException CreateHttpResponseException(HttpStatusCode code, string format, params object[] args)
        {
            return new HttpResponseException(new HttpResponseMessage { StatusCode = code, Content = new StringContent(string.Format(format, args)) });
        }

        private static Uri CreateLocationUri(HttpRequestMessage request, Sample resource)
        {
            var uriBuilder = new UriBuilder(request.RequestUri);
            var paths = uriBuilder.Path.Split('/');
            // There might be a key
            if (paths.Length > 1)
            {
                int key;
                if (int.TryParse(paths.Last(), out key))
                {
                    // Remove the key from the URI
                    var sb = new StringBuilder();

                    for (var i = 0; i < paths.Length - 1; i++)
                    {
                        if (sb.Length != 0)
                        {
                            sb.Append('/');
                        }
                        sb.Append(paths[i]);
                    }

                    uriBuilder.Path = sb.ToString();
                }
            }

            // Append the resource key
            uriBuilder.Path = string.Format("{0}/{1}", uriBuilder.Path, resource.Key);

            return uriBuilder.Uri;
        }

        private static HttpResponseMessage<Sample> CreateResponseWithETag(Sample resource)
        {
            var response = new HttpResponseMessage<Sample>(resource);
            response.Headers.ETag = new EntityTagHeaderValue((QuotedString)(resource.Tag));
            return response;
        }

        private static bool IsMatchingTag(Sample resource, string etag)
        {
            // "*" wildcard matches any value
            return etag == "\"*\"" || etag == (QuotedString)(resource.Tag);
        }

        private static int ParseResourceKey(string key)
        {
            int resourceKey;
            if (!int.TryParse(key, out resourceKey))
            {
                throw CreateHttpResponseException(HttpStatusCode.BadRequest, "Sample ID '{0}' is invalid - it cannot be converted to a number", key);
            }

            return resourceKey;
        }

        #endregion
    }
}