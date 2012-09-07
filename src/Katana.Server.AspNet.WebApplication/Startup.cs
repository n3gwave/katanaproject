﻿using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Gate;
using Owin;

namespace Katana.Server.AspNet.WebApplication
{
    public class Startup
    {
        public void Configuration(IAppBuilder builder)
        {
            var configuration = new HttpConfiguration(new HttpRouteCollection(HttpRuntime.AppDomainAppVirtualPath));
            configuration.Routes.MapHttpRoute("Default", "{controller}");

            builder.UseWebApi(configuration);
            builder.Run(this);
        }

        public Task Invoke(IDictionary<string, object> env)
        {
            var req = new Request(env);
            var resp = new Response(env);
            resp.ContentType = "text/plain";
            resp.Write("Hello world\r\n");
            resp.Flush();
            resp.Write("PathBase: " + req.PathBase + "\r\n");
            resp.Write("Path: " + req.Path + "\r\n");
            return resp.EndAsync();
        }
    }
}