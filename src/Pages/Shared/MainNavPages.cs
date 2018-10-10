using System;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AdvantageTool.Pages.Shared
{
    public class MainNavPages
    {
        public static string Index => "/Pages/Index";
        public static string Clients => "/Pages/Clients";
        public static string Platforms => "/Pages/Platforms";
        public static string About => "/Pages/About";
        public static string Contact => "/Pages/Contact";

        public static string IndexNavClass(ViewContext viewContext) => PageNavClass(viewContext, Index);
        public static string ClientsNavClass(ViewContext viewContext) => PageNavClass(viewContext, Clients);
        public static string PlatformsNavClass(ViewContext viewContext) => PageNavClass(viewContext, Platforms);
        public static string AboutNavClass(ViewContext viewContext) => PageNavClass(viewContext, About);
        public static string ContactNavClass(ViewContext viewContext) => PageNavClass(viewContext, Contact);

        public static string PageNavClass(ViewContext viewContext, string path)
        {
            var activePath = viewContext.View.Path;
            return activePath.StartsWith(path, StringComparison.OrdinalIgnoreCase) ? "active" : null;
        }
    }
}
