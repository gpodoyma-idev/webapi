using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Microsoft.Http;
using Microsoft.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Diagnostics;
using System.ServiceModel.Channels;

namespace CannonicalRESTWebApp.Tests
{
    public class HttpTestHelper<TKey, TResource>
    {
        public HttpTestHelper(string serviceUri)
        {
            ServiceUri = serviceUri;
            ReadResourceContent = ReadResourceAsDataContract;
            ReadResourceSetContent = ReadResourceSetAsDataContract;
            WriteResourceContent = CreateResourceDataContract;
        }

        public ResourceResult<TResource> GetResource(
            TKey key,
            EntityTag requestETag = null,
            string requestUri = null,
            WebContentFormat format = WebContentFormat.Xml,
            bool trailingSlash = false
            )
        {
            return SendRequest(
                "GET",
                new Uri((requestUri ?? ServiceUri) + key.ToString() + (trailingSlash ? "/" : string.Empty)),
                format,
                beforeRequest: (request) =>
                {
                    if (requestETag != null)
                        request.Headers.IfNoneMatch.Add(requestETag);
                });
        }


        public ResourceSetResult<TResource> GetResourceSet(
            int? skip = null,
            int? take = null,
            WebContentFormat format = WebContentFormat.Xml)
        {
            var result = new ResourceSetResult<TResource>();

            using (HttpClient client = new HttpClient())
            {
                Uri requestUri = CreateSkipTakeQueryString(skip, take);
                using (HttpRequestMessage request = new HttpRequestMessage("GET", requestUri))
                {
                    Debug.WriteLine("Sending GET to {0}", requestUri);

                    request.Headers.Accept.Add(WebContentFormatString(format));

                    using (HttpResponseMessage response = client.Send(request))
                    {
                        Debug.WriteLine("Response: {0} ({1})", (int)response.StatusCode, response.StatusCode);

                        result.Status = response.StatusCode;
                        result.ResponseHeaders = response.Headers;

                        if (response.Content.HasLength() && response.Content.GetLength() > 0)
                        {
                            switch (result.Status)
                            {
                                case HttpStatusCode.OK:
                                    if (ReadResourceSetContent == null)
                                        result.ResponseContent = response.Content.ReadAsString();
                                    else
                                        result.ResourceSet = ReadResourceSetContent(response.Content, format);
                                    break;
                                case HttpStatusCode.BadRequest:
                                    result.ErrorMessage = response.Content.ReadAsDataContract<string>();
                                    break;
                            }
                        }
                    }
                }
            }
            return result;
        }

        public ResourceResult<TResource> Post(TResource resource, WebContentFormat format = WebContentFormat.Xml)
        {
            return SendRequest(
                "POST",
                new Uri(ServiceUri),
                format,
                resource);
        }

        public ResourceResult<TResource> PutResource(
            TKey key, 
            TResource resource, 
            EntityTag requestETag = null, 
            WebContentFormat format = WebContentFormat.Xml)
        {
            return SendRequest(
                "PUT",
                new Uri(ServiceUri + key.ToString()),
                format,
                resource,
                beforeRequest: (request) =>
                {
                    if (requestETag != null)
                        request.Headers.IfMatch.Add(requestETag);
                });
        }

        public ResourceResult<TResource> DeleteResource(TKey key, EntityTag requestETag = null)
        {
            return SendRequest(
                "DELETE",
                new Uri(ServiceUri + key.ToString()),
                beforeRequest: (request) =>
                {
                    if (requestETag != null)
                        request.Headers.IfMatch.Add(requestETag);
                });
        }

        public ResourceResult<TResource> SendRequest(
            string method,
            Uri requestUri,
            WebContentFormat format = WebContentFormat.Xml,
            TResource resource = default(TResource),
            Action<HttpRequestMessage> beforeRequest = null,
            Action<HttpResponseMessage> afterResponse = null)
        {
            var result = new ResourceResult<TResource>();

            using (HttpClient client = new HttpClient())
            {
                using (HttpRequestMessage request = new HttpRequestMessage(method, requestUri))
                {
                    request.Headers.Accept.Add(WebContentFormatString(format));

                    if (resource != null)
                        request.Content = WriteResourceContent(resource, format);

                    if (beforeRequest != null)
                        beforeRequest(request);

                    Debug.WriteLine("Sending {0} to {1}", method, requestUri);

                    using (HttpResponseMessage response = client.Send(request))
                    {
                        Debug.WriteLine("Response: {0} ({1})", (int)response.StatusCode, response.StatusCode);

                        result.Status = response.StatusCode;
                        result.ResponseHeaders = response.Headers;

                        if (response.Content.HasLength() && response.Content.GetLength() > 0)
                        {
                            switch (result.Status)
                            {
                                case HttpStatusCode.Created:
                                    ReadContent(result, response, format);
                                    break;
                                case HttpStatusCode.OK:
                                    ReadContent(result, response, format);
                                    break;
                                case HttpStatusCode.BadRequest:
                                    switch (format)
                                    {
                                        case WebContentFormat.Xml:
                                            result.ErrorMessage = response.Content.ReadAsDataContract<string>();
                                            break;
                                        case WebContentFormat.Json:
                                            result.ErrorMessage = response.Content.ReadAsJsonDataContract<string>();
                                            break;
                                    }
                                    break;
                            }
                        }

                        if (afterResponse != null)
                            afterResponse(response);
                    }
                }
            }
            return result;
        }

        private StringWithOptionalQuality WebContentFormatString(WebContentFormat format)
        {
            switch (format)
            {
                case WebContentFormat.Xml:
                    return "application/xml";
                case WebContentFormat.Json:
                    return "application/json";
                default:
                    throw new ArgumentException(string.Format("Invalid content type for accept header: {0}", format));
            }
        }

        private void ReadContent(
            ResourceResult<TResource> result,
            HttpResponseMessage response,
            WebContentFormat format = WebContentFormat.Xml)
        {
            if (ReadResourceContent == null)
                result.ResponseContent = response.Content.ReadAsString();
            else
                result.Resource = ReadResourceContent(response.Content, format);
        }



        public Func<TKey, string> GetResourceUri { get; set; }
        public Func<HttpContent, WebContentFormat, ResourceSet<TResource>> ReadResourceSetContent { get; set; }
        public Func<HttpContent, WebContentFormat, TResource> ReadResourceContent { get; set; }
        public Func<TResource, WebContentFormat, HttpContent> WriteResourceContent { get; set; }
        public string ServiceUri { get; set; }

        public TResource ReadResourceAsDataContract(HttpContent content, WebContentFormat format = WebContentFormat.Xml)
        {
            switch (format)
            {
                case WebContentFormat.Xml:
                    return content.ReadAsDataContract<TResource>();
                case WebContentFormat.Json:
                    return content.ReadAsJsonDataContract<TResource>();
                default:
                    throw new NotImplementedException("Unsupported WebContentFormat");
            }
        }

        public ResourceSet<TResource> ReadResourceSetAsDataContract(HttpContent content, WebContentFormat format = WebContentFormat.Xml)
        {
            switch (format)
            {
                case WebContentFormat.Xml:
                    return content.ReadAsDataContract<ResourceSet<TResource>>();
                case WebContentFormat.Json:
                    return content.ReadAsJsonDataContract<ResourceSet<TResource>>();
                default:
                    throw new NotImplementedException("Unsupported WebContentFormat");
            }
        }

        public HttpContent CreateResourceDataContract(TResource resource, WebContentFormat format = WebContentFormat.Xml)
        {
            switch (format)
            {
                case WebContentFormat.Xml:
                    return HttpContentExtensions.CreateDataContract<TResource>(resource);
                case WebContentFormat.Json:
                    return HttpContentExtensions.CreateJsonDataContract<TResource>(resource);
                default:
                    throw new NotImplementedException("Unsupported WebContentFormat");
            }
        }

        const string resourceSetFormat = "?skip={0}&take{1}";

        private Uri CreateSkipTakeQueryString(int? skip, int? take)
        {
            StringBuilder sb = new StringBuilder(ServiceUri);
            if (skip.HasValue || take.HasValue)
            {
                sb.Append("?");
                if (skip.HasValue)
                {
                    sb.AppendFormat("skip={0}", skip);
                    if (take.HasValue)
                        sb.Append("&");
                }

                if (take.HasValue)
                {
                    sb.AppendFormat("take={0}", take);
                }
            }
            return new Uri(sb.ToString());
        }
    }
}
