namespace WCFTestHelper
{
    using System;
    using System.Diagnostics;
    using System.Text;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    ///   Allows you to start/stop the ASP.NET Web Development Server when unit testing WCF Services
    /// </summary>
    public class WebDevServer40 : TestServerHelper
    {
        private const string WebDevServer40ProcessName = "WebDev.WebServer40";

        private const string WebDevWindowCaptionFormat = "ASP.NET Development Server - Port {0}";

        private static readonly string WebDevServer40Path =
            Environment.ExpandEnvironmentVariables(@"%CommonProgramFiles(x86)%\Microsoft Shared\DevServer\10.0\Webdev.WebServer40.exe");

        private readonly int serverPort;

        public WebDevServer40(int port)
        {
            this.serverPort = port;
        }

        protected override string ServerWindowCaption
        {
            get
            {
                return string.Format(WebDevWindowCaptionFormat, this.serverPort);
            }
        }

        protected override string ServerProcessName
        {
            get
            {
                return WebDevServer40ProcessName;
            }
        }

        public static void EnsureIsRunning(int port, string physicalPath, string virtualPath = null)
        {
            var server = new WebDevServer40(port);
            server.EnsureIsRunning(
                () =>
                    {
                        // Start ASP.NET Development Server
                        var webServerArgs = new StringBuilder();

                        webServerArgs.AppendFormat("/port:{0} /path:\"{1}\"", port, physicalPath);

                        if (virtualPath != null)
                        {
                            webServerArgs.AppendFormat(" /vpath:{0}", virtualPath);
                        }

                        Debug.WriteLine("Starting {0} {1}", WebDevServer40Path, webServerArgs);
                        Assert.IsNotNull(Process.Start(WebDevServer40Path, webServerArgs.ToString()));
                    });
        }

        public static void Close(int port)
        {
            var server = new WebDevServer40(port);
            server.CloseIfRunning();
        }
    }
}