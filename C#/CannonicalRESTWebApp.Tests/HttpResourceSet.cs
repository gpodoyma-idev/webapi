namespace CannonicalRESTWebApp.Tests
{
    /// <summary>
    ///   This type is duplicated in the test project because we may want to make it different
    ///   than the server version
    /// </summary>
    /// <typeparam name = "TResource"></typeparam>
    public class HttpResourceSet<TResource>
    {
        public TResource[] Resources;

        public int SetCount;

        public int Skip;

        public int Take;

        public int TotalCount;
    }
}