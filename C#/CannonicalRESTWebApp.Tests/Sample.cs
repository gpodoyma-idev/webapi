namespace CannonicalRESTWebApp.Tests
{
    /// <summary>
    ///   Client view of the SampleResource class
    /// </summary>
    public class Sample
    {
        #region Constants and Fields

        public const string JsonMediaType = "application/json";

        public const string XmlMediaType = "application/xml";

        public string Data;

        public int Key;

        public string ReadOnlyData;

        public string Tag;

        #endregion
    }
}