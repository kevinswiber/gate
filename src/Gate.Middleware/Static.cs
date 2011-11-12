using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gate.Owin;

namespace Gate.Middleware
{
    public static class StaticExtensions
    {
        public static IAppBuilder Static(this IAppBuilder builder, string root, IEnumerable<string> urls)
        {
            return builder.Use(app => new Static(app, root, urls).Invoke);
        }

        public static IAppBuilder Static(this IAppBuilder builder, IEnumerable<string> urls)
        {
            return builder.Use(app => new Static(app, urls).Invoke);
        }

        public static IAppBuilder Static(this IAppBuilder builder, string root)
        {
            return builder.Use(app => new Static(app, root).Invoke);
        }

        public static IAppBuilder Static(this IAppBuilder builder)
        {
            return builder.Use(app => new Static(app).Invoke);
        }
    }

    public class Static
    {
        private readonly AppDelegate app;
        private readonly FileServer fileServer;
        private readonly IEnumerable<string> urls;

        public Static(AppDelegate app, IEnumerable<string> urls)
            : this(app, null, urls)
        { }

        public Static(AppDelegate app, string root = null, IEnumerable<string> urls = null)
        {
            this.app = app;

            if (root == null)
            {
                root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "public");
            }

            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException(string.Format("Invalid root directory: {0}", root));
            }

            if (urls == null)
            {
                var rootDirectory = new DirectoryInfo(root);
                var files = rootDirectory.GetFiles("*").Select(fi => "/" + fi.Name);
                var directories = rootDirectory.GetDirectories().Select(di => "/" + di.Name);
                urls = files.Concat(directories);
            }

            this.urls = urls;

            fileServer = new FileServer(root);
        }

        public void Invoke(IDictionary<string, object> env, ResultDelegate result, Action<Exception> fault)
        {
            var path = env["owin.RequestPath"].ToString();

            if (urls.Any(path.StartsWith))
            {
                fileServer.Invoke(env, result, fault);
                return;
            }

            Next(env, result, fault);
        }

        private void Next(IDictionary<string, object> env, ResultDelegate result, Action<Exception> fault)
        {
            if (app != null)
            {
                app.Invoke(env, result, fault);
            }
        }
    }
}