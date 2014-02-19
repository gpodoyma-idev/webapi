namespace CannonicalRESTWebApp.Infrastructure
{
    public class HttpResourceSet<TResource>
    {
        public TResource[] Resources;

        public int SetCount;

        public int Skip;

        public int Take;

        public int TotalCount;
    }
}