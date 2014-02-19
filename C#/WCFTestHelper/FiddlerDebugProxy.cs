namespace WCFTestHelper
{
    using System;

    /// <summary>
    ///   Allows you to start/stop Fiddler HTTP Debugging Proxy http://www.fiddler2.com when testing WCF Services
    /// </summary>
    public class FiddlerDebugProxy : TestServerHelper
    {
        private const string FiddlerServerProcessName = "Fiddler.WebServer";

        private const string FiddlerWindowCaption = "Fiddler - HTTP Debugging Proxy";

        private const int FiddlerWarmUpDelay = 2000;

        private static readonly string FiddlerServerPath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Fiddler2\Fiddler.exe");

        protected override string ServerWindowCaption
        {
            get
            {
                return FiddlerWindowCaption;
            }
        }

        protected override string ServerProcessName
        {
            get
            {
                return FiddlerServerProcessName;
            }
        }

        public static void EnsureIsRunning()
        {
            var server = new FiddlerDebugProxy();
            server.EnsureIsRunning(FiddlerServerPath, FiddlerWarmUpDelay);
        }

        public static void Close()
        {
            var server = new FiddlerDebugProxy();
            server.CloseIfRunning();
        }
    }
}