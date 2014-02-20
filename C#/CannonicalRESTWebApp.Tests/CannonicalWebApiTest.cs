namespace CannonicalRESTWebApp.Tests
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;

    using Microsoft.ApplicationServer.Http;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using WCFTestHelper;

    using WebAPI.Helpers;

    [TestClass]
    public class CannonicalWebApiTest
    {
        #region Constants and Fields

        public const string Localhost = "localhost";

        /// <summary>
        ///   Indicates which web server you want to use
        /// </summary>
        /// <remarks>
        ///   TODO: Enable PUT/DELETE with IIS Express 
        ///   Modify C:\Program Files (x86)\IIS Express\AppServer\applicationhost.config
        ///   Add the PUT / DELETE verbs to the following
        ///   <add name = "ExtensionlessUrl-Integrated-4.0" path = "*." verb = "GET,HEAD,POST,DEBUG,PUT,DELETE"
        ///     type = "System.Web.Handlers.TransferRequestHandler" preCondition = "integratedMode,runtimeVersionv4.0" />
        /// </remarks>
        public static readonly WebServer Server = WebServer.IISExpress;

        /// <summary>
        ///   host name for use with fiddler
        /// </summary>
        /// <remarks>
        ///   TODO: Enable fiddler for IIS Express
        ///   Modify Fiddler Rules to contain this 
        ///   static function OnBeforeRequest(oSession:Fiddler.Session)
        ///   {
        ///   //...
        ///   // workaround the iisexpress limitation
        ///   // URL http://iisexpress:port can be used for capturing IIS Express traffic
        ///   if (oSession.HostnameIs("iisexpress")) { oSession.host = "localhost:"+oSession.port; }
        ///   //...
        ///   }
        /// </remarks>
        public static string FiddlerLocalhost;

        // Using a different port for unit testing

        private const string BaseUriFormat = "http://{0}:{1}/";

        private const string JsonContentType = "application/json";

        private const int Port = 2000;

        private const string ServicePath = "api";

        private const string XmlContentType = "application/xml";

        /// <summary>
        ///   Tip: Use this switch to control if you want to use fiddler for debugging
        /// </summary>
        private static readonly bool UseFiddler;

        #endregion

        #region Constructors and Destructors

        static CannonicalWebApiTest()
        {
            FiddlerLocalhost = Server == WebServer.IISExpress ? "iisexpress" : "ipv4.fiddler";
            UseFiddler = true;
        }

        #endregion

        #region Properties

        /// <summary>
        ///   URI for testing PUT with an Add or Update style
        /// </summary>
        public static string AddOrUpdateServiceUri
        {
            get
            {
                return ServiceUri + "/AddOrUpdate";
            }
        }

        public static string BaseUri
        {
            get
            {
                return string.Format(BaseUriFormat, UseFiddler ? FiddlerLocalhost : Localhost, Port);
            }
        }

        /// <summary>
        ///   TIP: Use a property for your URI to simplify test code
        /// </summary>
        public static string ServiceUri
        {
            get
            {
                return BaseUri + ServicePath;
            }
        }

        ///<summary>
        ///  Gets or sets the test context which provides
        ///  information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        #endregion

        #region Public Methods

        [ClassCleanup]
        public static void CloseServers()
        {
            // TIP: Use helper classes to close servers required for testing
            switch (Server)
            {
                case WebServer.WebDevServer:
                    WebDevServer40.Close(Port);
                    break;
                case WebServer.IISExpress:
                    IISExpressServer.Close();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Could also close Fiddler
            // FiddlerDebugProxy.Close();
        }

        [ClassInitialize]
        public static void StartServers(TestContext context)
        {
            switch (Server)
            {
                case WebServer.WebDevServer:
                    WebDevServer40.EnsureIsRunning(Port, TestServerHelper.GetWebPathFromSolutionPath(context));
                    break;
                case WebServer.IISExpress:
                    IISExpressServer.EnsureIsRunning(Port, TestServerHelper.GetWebPathFromSolutionPath(context));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // TIP: Fiddler is a great tool for understanding HTTP traffic http://www.fiddler2.com
            if (UseFiddler)
            {
                FiddlerDebugProxy.EnsureIsRunning();
            }
        }

        [TestMethod]
        public void DeleteShouldBeIdempotent()
        {
            // Arrange
            const int resourceKey = 1;

            // Act
            var testResult1 = DeleteResource(resourceKey);
            var testResult2 = DeleteResource(resourceKey);

            // Assert
            testResult1.Response.EnsureSuccessStatusCode();
            testResult2.Response.EnsureSuccessStatusCode();
        }

        [TestMethod]
        public void DeleteShouldDeleteAnEntityThatExists()
        {
            // Arrange
            const int resourceKey = 1;

            // Act
            var testResult = DeleteResource(resourceKey);
            testResult.Response.EnsureSuccessStatusCode();
        }

        [TestMethod]
        public void DeleteShouldReturn400BadRequestIfTheKeyIsInvalid()
        {
            // Arrange
            const string resourceKey = "badkey";
            // using key of type string to force bad request

            // Act
            using (var client = CreateHttpClient())
            {
                var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, string.Format("{0}/{1}", ServiceUri, resourceKey));
                AddIfMatchHeader(deleteRequest, null);

                var response = client.Send(deleteRequest);
                var result = new TestResult(response);

                // Assert
                Assert.AreEqual(HttpStatusCode.BadRequest, result.Response.StatusCode);
            }
        }

        [TestMethod]
        public void DeleteShouldReturnWith412PreconditionFailedIfNoMatchingEntityForIfMatchEtag()
        {
            // Arrange
            const int resourceKey = 1;

            // Act

            // Get the resource
            var resultGet = GetResource(resourceKey);
            resultGet.Response.EnsureSuccessStatusCode();

            // Update so the etag won't match
            var putResource = resultGet.Resource;
            putResource.Data = "modified";

            PutResource(resourceKey, putResource);

            // Try to delete - this should fail because of the precondition
            var result = DeleteResource(resourceKey, resultGet.Resource.Tag);

            // Assert
            Assert.AreEqual(HttpStatusCode.PreconditionFailed, result.Response.StatusCode);
        }

        [TestMethod]
        public void DeleteShouldSucceedIfMatchingEntityForIfMatchEtag()
        {
            // Arrange
            const int resourceKey = 1;

            // Act
            var resultGet = GetResource(resourceKey);
            resultGet.Response.EnsureSuccessStatusCode();

            // Delete with etag
            var result = DeleteResource(resourceKey, resultGet.Resource.Tag);
            var result2 = DeleteResource(resourceKey, resultGet.Resource.Tag);

            // Assert
            result.Response.EnsureSuccessStatusCode();
            result2.Response.EnsureSuccessStatusCode();
        }

        [TestMethod]
        public void DeleteShouldSucceedIfWildcardUsedInIfMatchEtag()
        {
            // Arrange
            const int resourceKey = 1;

            // Act
            var resultGet = GetResource(resourceKey);
            resultGet.Response.EnsureSuccessStatusCode();

            // Update so the etag won't match
            var putResource = resultGet.Resource;
            putResource.Data = "modified";
            PutResource(resourceKey, putResource);

            // Delete with wildcard etag
            var result = DeleteResource(resourceKey, "*");
            var result2 = DeleteResource(resourceKey, resultGet.Resource.Tag);

            // Assert
            result.Response.EnsureSuccessStatusCode();
            result2.Response.EnsureSuccessStatusCode();
        }

        [TestMethod]
        public void GetMustReturn304NotModifiedIfConditionalGetConditionsAreMet()
        {
            // Arrange
            const int expectedKey = 1;

            // Act
            var result1 = GetResource(expectedKey);
            result1.Response.EnsureSuccessStatusCode();

            var result2 = GetResource(expectedKey, result1.Response.Headers.ETag);

            // Assert
            Assert.AreEqual(HttpStatusCode.NotModified, result2.Response.StatusCode);
        }

        /// <summary>
        /// Verifies that BadRequest is returned when Skip is less than zero
        /// </summary>
        /// <remarks>
        /// Currently the WCF WebApi returns InternalServerError instead of BadRequest
        /// </remarks>
        [TestMethod]
        [Ignore]
        public void GetMustReturn400BadRequestIfSkipIsLessThan0()
        {
            // Arrange
            const int skip = -1;
            const int top = 3;

            // Act
            var result = GetResourceSet(skip, top);

            // Assert
            Assert.AreEqual(HttpStatusCode.BadRequest, result.Response.StatusCode);
        }

        /// <summary>
        /// Verifies that BadRequest is returned when Top is less than zero
        /// </summary>
        /// <remarks>
        /// Currently the WCF WebApi returns InternalServerError instead of BadRequest
        /// </remarks>
        [TestMethod]
        [Ignore]
        public void GetMustReturn400BadRequestIfTopIsLessThan0()
        {
            // Arrange
            const int top = -1;

            // Act
            var result = GetResourceSet(null, top);

            // Assert
            Assert.AreEqual(HttpStatusCode.BadRequest, result.Response.StatusCode);
        }

        [TestMethod]
        public void GetMustReturn400BadRequestIfTheKeyIsInvalid()
        {
            // Arrange
            using (var client = CreateHttpClient())
            {
                // Act
                var response = client.Get(string.Format("{0}/{1}", ServiceUri, "badkey"));

                // Assert
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            }
        }

        [TestMethod]
        public void GetMustReturn404NotFoundIfTheKeyIsNotFound()
        {
            // Arrange

            // Act
            var result = GetResource(0);

            // Assert
            Assert.AreEqual(HttpStatusCode.NotFound, result.Response.StatusCode);
        }

        [TestMethod]
        public void GetMustReturnAResourceGivenAKeyIfTheResourceWithThatKeyExistsJson()
        {
            GetMustReturnAResourceGivenAKeyIfTheResourceWithThatKeyExists(JsonContentType);
        }

        [TestMethod]
        public void GetMustReturnAResourceGivenAKeyIfTheResourceWithThatKeyExistsXml()
        {
            GetMustReturnAResourceGivenAKeyIfTheResourceWithThatKeyExists(XmlContentType);
        }

        [TestMethod]
        public void GetMustReturnResourcesStartingWithTheFirstOneWhenSkipIsNotDefinedJson()
        {
            GetMustReturnResourcesStartingWithTheFirstOneWhenSkipIsNotDefined(JsonContentType);
        }

        [TestMethod]
        public void GetMustReturnResourcesStartingWithTheFirstOneWhenSkipIsNotDefinedXml()
        {
            GetMustReturnResourcesStartingWithTheFirstOneWhenSkipIsNotDefined(XmlContentType);
        }

        [TestMethod]
        public void GetMustReturnZeroOrMoreResourcesWhenTakeIsNotProvidedJson()
        {
            GetMustReturnZeroOrMoreResourcesWhenTakeIsNotProvided(JsonContentType);
        }

        [TestMethod]
        public void GetMustReturnZeroOrMoreResourcesWhenTakeIsNotProvidedXml()
        {
            GetMustReturnZeroOrMoreResourcesWhenTakeIsNotProvided(XmlContentType);
        }

        [TestMethod]
        public void GetMustReturnZeroResourcesWhenSkipIsGreaterThanTheNumberOfResourcesInTheCollectionJson()
        {
            GetMustReturnZeroResourcesWhenSkipIsGreaterThanTheNumberOfResourcesInTheCollection(JsonContentType);
        }

        [TestMethod]
        public void GetMustReturnZeroResourcesWhenSkipIsGreaterThanTheNumberOfResourcesInTheCollectionXml()
        {
            GetMustReturnZeroResourcesWhenSkipIsGreaterThanTheNumberOfResourcesInTheCollection(XmlContentType);
        }

        [TestMethod]
        public void GetMustSkipSkipResourcesInTheCollectionAndReturnUpToTakeResourcesJson()
        {
            GetMustSkipSkipResourcesInTheCollectionAndReturnUpToTakeResources(JsonContentType);
        }

        [TestMethod]
        public void GetMustSkipSkipResourcesInTheCollectionAndReturnUpToTakeResourcesXml()
        {
            GetMustSkipSkipResourcesInTheCollectionAndReturnUpToTakeResources(XmlContentType);
        }

        [TestMethod]
        public void GetShouldReturnAnETagHeader()
        {
            // Arrange
            const int expectedKey = 1;

            // Act
            var result = GetResource(expectedKey);
            result.Response.EnsureSuccessStatusCode();

            // Assert
            Assert.IsNotNull(result.Response.Headers.ETag);
        }

        [TestInitialize]
        public void InitializeResourceCollection()
        {
            using (var client = new HttpClient())
            {
                // TIP: Initialize your service to a known state before each test
                // Delete all records - service has special case code to do this
                using (var request = new HttpRequestMessage(HttpMethod.Delete, ServiceUri + "/all"))
                {
                    client.Send(request);
                }
            }
        }

        [TestMethod]
        public void PostMustAppendAValidResourceToTheResourceCollectionJson()
        {
            PostMustAppendAValidResourceToTheResourceCollection(JsonContentType);
        }

        [TestMethod]
        public void PostMustAppendAValidResourceToTheResourceCollectionXml()
        {
            PostMustAppendAValidResourceToTheResourceCollection(XmlContentType);
        }

        [TestMethod]
        public void PostMustIgnoreWritesToEntityFieldsTheServerConsidersReadOnlyJson()
        {
            PostMustIgnoreWritesToEntityFieldsTheServerConsidersReadOnly(JsonContentType);
        }

        [TestMethod]
        public void PostMustIgnoreWritesToEntityFieldsTheServerConsidersReadOnlyXml()
        {
            PostMustIgnoreWritesToEntityFieldsTheServerConsidersReadOnly(XmlContentType);
        }

        [TestMethod]
        public void PostMustReturn400BadRequestIfTheEntityIsInvalidJson()
        {
            PostMustReturn400BadRequestIfTheEntityIsInvalid(JsonContentType);
        }

        [TestMethod]
        public void PostMustReturn400BadRequestIfTheEntityIsInvalidXml()
        {
            PostMustReturn400BadRequestIfTheEntityIsInvalid(XmlContentType);
        }

        [TestMethod]
        public void PostMustReturn409ConflictIfTheEntityConflictsWithAnotherEntityJson()
        {
            PostMustReturn409ConflictIfTheEntityConflictsWithAnotherEntity(JsonContentType);
        }

        [TestMethod]
        public void PostMustReturn409ConflictIfTheEntityConflictsWithAnotherEntityXml()
        {
            PostMustReturn409ConflictIfTheEntityConflictsWithAnotherEntity(XmlContentType);
        }

        [TestMethod]
        public void PutMayAddANewEntityUsingTheKeyProvidedInTheUriJson()
        {
            PutMayAddANewEntityUsingTheKeyProvidedInTheUri(JsonContentType);
        }

        [TestMethod]
        public void PutMayAddANewEntityUsingTheKeyProvidedInTheUriXml()
        {
            PutMayAddANewEntityUsingTheKeyProvidedInTheUri(XmlContentType);
        }

        [TestMethod]
        public void PutMustBeIdempotentAddOrUpdateJson()
        {
            PutMustBeIdempotent(JsonContentType, AddOrUpdateServiceUri);
        }

        [TestMethod]
        public void PutMustBeIdempotentAddOrUpdateXml()
        {
            PutMustBeIdempotent(XmlContentType, AddOrUpdateServiceUri);
        }

        [TestMethod]
        public void PutMustBeIdempotentJson()
        {
            PutMustBeIdempotent(JsonContentType);
        }

        [TestMethod]
        public void PutMustBeIdempotentXml()
        {
            PutMustBeIdempotent(XmlContentType);
        }

        [TestMethod]
        public void PutMustRespectThePreconditionIfMatchAddOrUpdateJson()
        {
            PutShouldRespectIfMatch(JsonContentType, AddOrUpdateServiceUri);
        }

        [TestMethod]
        public void PutMustRespectThePreconditionIfMatchAddOrUpdateXml()
        {
            PutShouldRespectIfMatch(XmlContentType, AddOrUpdateServiceUri);
        }

        [TestMethod]
        public void PutMustRespectThePreconditionIfMatchJson()
        {
            PutShouldRespectIfMatch(JsonContentType);
        }

        [TestMethod]
        public void PutMustRespectThePreconditionIfMatchXml()
        {
            PutShouldRespectIfMatch(XmlContentType);
        }

        [TestMethod]
        public void PutMustUpdateTheEntityIdentifiedByTheUriIfItExistsAddOrUpdateJson()
        {
            PutMustUpdateTheEntityIdentifiedByTheUriIfItExistsAddOrUpdate(JsonContentType);
        }

        [TestMethod]
        public void PutMustUpdateTheEntityIdentifiedByTheUriIfItExistsAddOrUpdateXml()
        {
            PutMustUpdateTheEntityIdentifiedByTheUriIfItExistsAddOrUpdate(XmlContentType);
        }

        [TestMethod]
        public void PutMustUpdateTheEntityIdentifiedByTheUriIfItExistsJson()
        {
            PutMustUpdateTheEntityIdentifiedByTheUriIfItExists(JsonContentType);
        }

        [TestMethod]
        public void PutMustUpdateTheEntityIdentifiedByTheUriIfItExistsXml()
        {
            PutMustUpdateTheEntityIdentifiedByTheUriIfItExists(XmlContentType);
        }

        [TestMethod]
        public void PutWithNullResourceWillReturnBadRequest()
        {
            // Arrange

            // Act
            var result = PutResource(1, null);

            // Assert
            Assert.AreEqual(HttpStatusCode.BadRequest, result.Response.StatusCode);
        }

        [TestMethod]
        public void PutWithNullResourceWillReturnBadRequestAddOrUpdate()
        {
            // Arrange

            // Act
            var result = PutResource(1, null, AddOrUpdateServiceUri);

            // Assert
            Assert.AreEqual(HttpStatusCode.BadRequest, result.Response.StatusCode);
        }

        #endregion

        #region Methods

        private static void AddIfMatchHeader(HttpRequestMessage deleteRequest, string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                deleteRequest.Headers.IfMatch.Add(new EntityTagHeaderValue((QuotedString)tag));
            }
        }

        private static HttpClient CreateHttpClient(string contentType = XmlContentType)
        {
            var client = new HttpClient();
            if (!string.IsNullOrEmpty(contentType))
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
            }
            return client;
        }

        private static TestResult DeleteResource(int resourceKey, string tag = null)
        {
            using (var client = CreateHttpClient())
            {
                var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, GetResourceKeyUri(resourceKey));
                AddIfMatchHeader(deleteRequest, tag);

                var response = client.Send(deleteRequest);

                return new TestResult(response);
            }
        }

        private static void GetMustReturnAResourceGivenAKeyIfTheResourceWithThatKeyExists(string contentType)
        {
            // Arrange
            const int expectedKey = 1;

            // Act
            var result = GetResource(expectedKey, contentType);
            result.Response.EnsureSuccessStatusCode();

            // Assert
            Assert.AreEqual(expectedKey, result.Resource.Key);
            Assert.AreEqual(contentType, result.Response.Content.Headers.ContentType.MediaType);
        }

        private static void GetMustReturnResourcesStartingWithTheFirstOneWhenSkipIsNotDefined(string contentType)
        {
            // Arrange
            const int expectedKey = 1;
            const int top = 3;

            var result = GetResourceSet(0, top, contentType);

            // Assert
            Assert.AreEqual(contentType, result.Response.Content.Headers.ContentType.MediaType);
            Assert.AreEqual(expectedKey, result.ResourceSet[0].Key);
        }

        private static void GetMustReturnZeroOrMoreResourcesWhenTakeIsNotProvided(string contentType)
        {
            // Arrange
            const int skip = 5;

            // Act
            var result = GetResourceSet(skip, null, contentType);

            // Assert
            Assert.AreEqual(contentType, result.Response.Content.Headers.ContentType.MediaType);
            Assert.IsTrue(result.ResourceSet.Length > 0);
        }

        private static void GetMustReturnZeroResourcesWhenSkipIsGreaterThanTheNumberOfResourcesInTheCollection(string contentType)
        {
            // Arrange
            const int skip = int.MaxValue;
            const int top = 3;

            // Act
            var result = GetResourceSet(skip, top, contentType);

            // Assert
            Assert.AreEqual(contentType, result.Response.Content.Headers.ContentType.MediaType);
            Assert.AreEqual(0, result.ResourceSet.Length);
        }

        private static void GetMustSkipSkipResourcesInTheCollectionAndReturnUpToTakeResources(string contentType)
        {
            // Arrange
            const int skip = 1;
            const int top = 3;

            var result = GetResourceSet(skip, top, contentType);

            // Assert
            Assert.AreEqual(contentType, result.Response.Content.Headers.ContentType.MediaType);
            Assert.AreEqual(top, result.ResourceSet.Length);
        }

        private static TestResult GetResource(int key, string contentType = XmlContentType)
        {
            return GetResource(key, null, contentType);
        }

        private static TestResult GetResource(int key, EntityTagHeaderValue eTag, string contentType = XmlContentType)
        {
            using (var client = CreateHttpClient(contentType))
            {
                var request = new HttpRequestMessage(HttpMethod.Get, GetResourceKeyUri(key));
                if (eTag != null)
                {
                    request.Headers.IfNoneMatch.Add(eTag);
                }

                var response = client.Send(request);
                return new TestResult(response);
            }
        }

        private static string GetResourceKeyUri(int expectedKey, string baseUri = null)
        {
            return string.Format("{0}/{1}", baseUri ?? ServiceUri, expectedKey);
        }

        private static TestResultSet GetResourceSet(int? skip, int? top, string contentType = XmlContentType)
        {
            using (var client = CreateHttpClient(contentType))
            {
                // Act
                var response = client.Get(GetResourceSetUri(skip, top));
                return new TestResultSet(response);
            }
        }

        /// <summary>
        ///   Builds a Uri with appropriate Skip / Take parameters
        /// </summary>
        /// <param name = "skip">Records to skip</param>
        /// <param name = "top">Records to take</param>
        private static string GetResourceSetUri(int? skip, int? top)
        {
            var index = 0;
            var sb = new StringBuilder("{0}");

            if (skip.HasValue || top.HasValue)
            {
                sb.Append("?");
            }

            if (skip.HasValue)
            {
                index++;
                sb.AppendFormat("$skip={{{0}}}", index);
            }

            if (index > 0)
            {
                sb.Append("&");
            }

            if (top.HasValue)
            {
                index++;
                sb.AppendFormat("$top={{{0}}}", index);
            }

            if (skip.HasValue && top.HasValue)
            {
                return string.Format(sb.ToString(), ServiceUri, skip, top);
            }

            return string.Format(sb.ToString(), ServiceUri, skip.HasValue ? skip : top);
        }

        private static int ParseKeyFromLocation(string pathAndQuery)
        {
            var paths = pathAndQuery.Split('/');
            return int.Parse(paths.Last());
        }

        private static TestResult Post(Sample sample, string contentType = XmlContentType)
        {
            using (var client = CreateHttpClient(contentType))
            {
                return new TestResult(client.Post(ServiceUri, new ObjectContent(typeof(Sample), sample, Sample.XmlMediaType)));
            }
        }

        private static void PostMustAppendAValidResourceToTheResourceCollection(string contentType)
        {
            // Arrange
            const string expectedData = "Post Data";

            var expectedResource = new Sample { Data = expectedData };

            var result = Post(expectedResource, contentType);

            // Assert
            Assert.AreEqual(HttpStatusCode.Created, result.Response.StatusCode);

            // Check entity
            Assert.AreEqual(expectedData, result.Resource.Data);

            // Check headers
            Assert.IsNotNull(result.Response.Headers.ETag, "Null etag");
            Assert.IsNotNull(result.Response.Headers.Location, "Null location");

            // Check server generated key and location header
            Assert.AreEqual(result.Resource.Key, ParseKeyFromLocation(result.Response.Headers.Location.PathAndQuery), "Location header key should match entity key");
            Assert.IsTrue(result.Resource.Key > 5, "Server generated key should be > 5 on test data set");
        }

        private static void PostMustIgnoreWritesToEntityFieldsTheServerConsidersReadOnly(string contentType)
        {
            // Arrange
            const string expectedData = "Post Data";
            const string notExpectedData = "Updated read only data";
            var expectedResource = new Sample { Data = expectedData, ReadOnlyData = notExpectedData };

            // Act
            var result = Post(expectedResource, contentType);

            // Assert
            Assert.AreEqual(HttpStatusCode.Created, result.Response.StatusCode);
            Assert.AreNotEqual(notExpectedData, result.Resource.ReadOnlyData);
        }

        private static void PostMustReturn400BadRequestIfTheEntityIsInvalid(string contentType)
        {
            // Arrange
            var expectedData = string.Empty;
            new Sample { Data = expectedData };

            var result = Post(new Sample { Data = expectedData }, contentType);

            // Assert
            Assert.AreEqual(HttpStatusCode.BadRequest, result.Response.StatusCode);
        }

        private static void PostMustReturn409ConflictIfTheEntityConflictsWithAnotherEntity(string contentType)
        {
            // Arrange
            const string expectedData = "Post Data";
            var expectedResource = new Sample { Data = expectedData };

            // Act
            var result1 = Post(expectedResource, contentType);
            var result2 = Post(expectedResource, contentType);

            // Assert
            Assert.AreEqual(HttpStatusCode.Created, result1.Response.StatusCode);
            Assert.AreEqual(HttpStatusCode.Conflict, result2.Response.StatusCode);
        }

        private static void PutMayAddANewEntityUsingTheKeyProvidedInTheUri(string contentType)
        {
            // Arrange
            const int resourceKey = 333;
            var expectedData = "Sample" + resourceKey;
            var putResource = new Sample { Data = expectedData };
            var expectedUri = new Uri(GetResourceKeyUri(resourceKey, AddOrUpdateServiceUri));

            // Act
            var result = PutResource(resourceKey, putResource, AddOrUpdateServiceUri, contentType);

            // Assert
            Assert.AreEqual(HttpStatusCode.Created, result.Response.StatusCode, "Resource should be added");
            Assert.AreEqual(expectedData, result.Resource.Data, "Added Resource was not returned correctly");
            Assert.AreEqual(expectedUri.PathAndQuery, result.Response.Headers.Location.PathAndQuery, "Location header was not set correctly");
            Assert.IsNotNull(result.Response.Headers.ETag, "Response should include etag");
        }

        private static void PutMustBeIdempotent(string contentType, string baseUri = null)
        {
            // Arrange
            const int resourceKey = 1;
            var getResult = GetResource(resourceKey);
            getResult.Response.EnsureSuccessStatusCode();

            // Act
            var putResource = getResult.Resource;
            putResource.Data = "modified";

            // This will modify the etag
            var put1Result = PutResource(resourceKey, putResource, baseUri, contentType);
            put1Result.Response.EnsureSuccessStatusCode();

            var putResource2 = put1Result.Resource;
            // Put the same resource again
            var put2Result = PutResource(resourceKey, putResource2, baseUri, contentType);
            put2Result.Response.EnsureSuccessStatusCode();

            // Assert
            Assert.AreEqual(put1Result.Response.Headers.ETag.Tag, put2Result.Response.Headers.ETag.Tag, "ETags should not change when put the same resource twice");
        }

        private static void PutMustUpdateResourceIfExists(string contentType, string baseUri = null)
        {
            // Arrange
            const int expectedKey = 1;
            const string expectedData = "modified";
            var resultGet = GetResource(expectedKey);
            var putResource = resultGet.Resource;
            putResource.Data = "modified";

            // Act
            var resultPut = PutResource(expectedKey, putResource, baseUri, contentType);
            resultPut.Response.EnsureSuccessStatusCode();

            // Assert
            Assert.AreEqual(expectedData, resultPut.Resource.Data);
            Assert.AreNotEqual(resultGet.Response.Headers.ETag, resultPut.Response.Headers.ETag, "Entity tags should have changed");
            Assert.AreNotEqual(resultGet.Resource.Tag, resultPut.Resource.Tag, "Sample version should have changed");
        }

        private static void PutMustUpdateTheEntityIdentifiedByTheUriIfItExists(string contentType)
        {
            PutMustUpdateResourceIfExists(contentType);
        }

        private static void PutMustUpdateTheEntityIdentifiedByTheUriIfItExistsAddOrUpdate(string contentType)
        {
            PutMustUpdateResourceIfExists(contentType, AddOrUpdateServiceUri);
        }

        private static TestResult PutResource(int key, Sample putResource, string baseUri = null, string contentType = XmlContentType)
        {
            using (var client = CreateHttpClient(contentType))
            {
                var request = new HttpRequestMessage(HttpMethod.Put, GetResourceKeyUri(key, baseUri)) { Content = new ObjectContent(typeof(Sample), putResource, contentType) };

                if (putResource != null && !string.IsNullOrWhiteSpace(putResource.Tag))
                {
                    request.Headers.IfMatch.Add(new EntityTagHeaderValue((QuotedString)putResource.Tag));
                }

                var response = client.Send(request);

                return new TestResult(response);
            }
        }

        private static void PutShouldRespectIfMatch(string contentType, string baseUri = null)
        {
            // Arrange
            const int resourceKey = 1;

            var resultGet = GetResource(resourceKey);

            var putResource = resultGet.Resource;
            putResource.Data = "modified";

            // Act
            // Update the resource - will modify the tag
            var resultPut1 = PutResource(resourceKey, putResource, baseUri, contentType);
            resultPut1.Response.EnsureSuccessStatusCode();

            // Try to update it again - should fail precondition
            var resultPut2 = PutResource(resourceKey, putResource, baseUri, contentType);

            // Assert
            Assert.AreEqual(HttpStatusCode.PreconditionFailed, resultPut2.Response.StatusCode);
        }

        #endregion

        private class TestResult
        {
            #region Constructors and Destructors

            public TestResult(HttpResponseMessage response)
            {
                this.Response = response;

                if (this.Response.IsSuccessStatusCode && this.Response.StatusCode != HttpStatusCode.NoContent)
                {
                    this.Resource = response.Content.ReadAs<Sample>();
                }
            }

            #endregion

            #region Properties

            public Sample Resource { get; private set; }

            public HttpResponseMessage Response { get; private set; }

            #endregion
        }

        private class TestResultSet
        {
            #region Constructors and Destructors

            internal TestResultSet(HttpResponseMessage response)
            {
                this.Response = response;

                if (this.Response.IsSuccessStatusCode && this.Response.StatusCode != HttpStatusCode.NoContent)
                {
                    this.ResourceSet = response.Content.ReadAs<Sample[]>();
                }
            }

            #endregion

            //public HttpResourceSet<Sample> ResourceSet { get; private set; }

            #region Properties

            public HttpResponseMessage Response { get; private set; }

            internal Sample[] ResourceSet { get; private set; }

            #endregion
        }
    }
}