namespace CannonicalRESTWebApp
{
    using System;
    using System.Web;
    using System.Web.Routing;

    using CannonicalRESTWebApp.Resources;

    using Microsoft.ApplicationServer.Http.Activation;

    public class Global : HttpApplication
    {
        #region Public Methods

        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.MapServiceRoute<SampleResource>("api");
        }

        #endregion

        #region Methods

        protected void Application_Start(object sender, EventArgs e)
        {
            RegisterRoutes(RouteTable.Routes);
        }

        #endregion
    }
}