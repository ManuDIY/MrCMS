﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MrCMS.Apps;
using MrCMS.Web.Apps.Admin.Filters;
using MrCMS.Web.Apps.Admin.ModelBinders;

namespace MrCMS.Web.Apps.Admin
{
    public class MrCMSAdminApp : StandardMrCMSApp
    {
        public MrCMSAdminApp()
        {
            ContentPrefix = "/Areas/Admin";
            ViewPrefix = "/Areas/Admin";
        }

        public override string Name => "Admin";

        public override IRouteBuilder MapRoutes(IRouteBuilder routeBuilder)
        {
            routeBuilder.MapAreaRoute("Admin route",
                "Admin",
                "Admin/{controller}/{action}/{id?}",
                new {controller = "Home", action = "Index"}
            );
            return routeBuilder;
        }

        public override void SetupMvcOptions(MvcOptions options)
        {
            options.ModelBinderProviders.Insert(1, new UpdateAdminViewModelBinderProvider());
                    options.Filters.AddService(typeof(AdminAuthFilter));
        }
    }
}