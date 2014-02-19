
namespace WCFTestHelper
{
    using System;
    using System.Diagnostics;
    using System.IO;

    public class IISExpressServer : TestServerHelper
    {
        protected string ServerPath
        {
            get
            {
                var programfiles = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("programfiles"))
                                       ? Environment.GetEnvironmentVariable("programfiles(x86)")
                                       : Environment.GetEnvironmentVariable("programfiles");

                Debug.Assert(programfiles != null, "programfiles != null");
                return Path.Combine(programfiles, @"IIS Express\iisexpress.exe");
            }
        }

        protected override string ServerWindowCaption
        {
            get
            {
                return @"c:\Program Files (x86)\IIS Express\" + this.ServerProcessName;
            }
        }

        protected override string ServerProcessName
        {
            get
            {
                return "iisexpress.exe";
            }
        }

        public static void EnsureIsRunning(int port, string physicalPath, string virtualPath = null)
        {
            var server = new IISExpressServer();
            server.EnsureIsRunning(
                () =>
                    {
                        var webServerArgs = string.Format("/port:{0} /path:\"{1}\"", port, physicalPath);
                        Debug.WriteLine("Starting {0} {1}", server.ServerPath, webServerArgs);
                        var process = Process.Start(server.ServerPath, webServerArgs);
                        Debug.Assert(process != null, "process != null");
                        Debug.WriteLine(string.Format("Attach debugger to process {0} for server debugging", process.ProcessName));
                    });
        }

        public static void Close()
        {
            var server = new IISExpressServer();
            server.CloseIfRunning();
        }
    }
}