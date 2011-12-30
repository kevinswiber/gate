﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Gate.Owin;

namespace Gate.Hosts.AspNet
{
    using BodyAction = Func<
        Func< //next
            ArraySegment<byte>, // data
            Action, // continuation
            bool>, // continuation was or will be invoked
        Action<Exception>, //error
        Action, //complete
        Action>; //cancel

    public class AppHandler
    {
        readonly AppDelegate _app;

        public AppHandler(AppDelegate app)
        {
            _app = app;
        }

        public IAsyncResult BeginProcessRequest(HttpContextBase httpContext, AsyncCallback callback, object state)
        {
            var taskCompletionSource = new TaskCompletionSource<Action>(state);
            if (callback != null)
                taskCompletionSource.Task.ContinueWith(task => callback(task), TaskContinuationOptions.ExecuteSynchronously);

            var httpRequest = httpContext.Request;
            var serverVariables = new ServerVariables(httpRequest.ServerVariables);

            var pathBase = httpRequest.ApplicationPath;
            if (pathBase == "/" || pathBase == null)
                pathBase = "";

            var path = httpRequest.Path;
            if (path.StartsWith(pathBase))
                path = path.Substring(pathBase.Length);

            var requestHeaders = httpRequest.Headers.AllKeys
                .ToDictionary(x => x, x => httpRequest.Headers.Get(x), StringComparer.OrdinalIgnoreCase);

            var env = new Dictionary<string, object>
            { 
                {"owin.Version", "1.0"},
                {"owin.RequestMethod", httpRequest.HttpMethod},
                {"owin.RequestScheme", httpRequest.Url.Scheme},
                {"owin.RequestPathBase", pathBase},
                {"owin.RequestPath", path},
                {"owin.RequestQueryString", serverVariables.QueryString},
                {"owin.RequestHeaders", requestHeaders},
                {"owin.RequestBody", RequestBody(httpRequest.InputStream)},
                {"aspnet.HttpContextBase", httpContext},
            };
            foreach (var kv in serverVariables.AddToEnvironment())
            {
                env["server." + kv.Key] = kv.Value;
            }


            _app.Invoke(
                env,
                (status, headers, body) =>
                {
                    try
                    {
                        httpContext.Response.BufferOutput = false;

                        httpContext.Response.Status = status;
                        foreach (var header in headers.SelectMany(kv => kv.Value.Split("\r\n".ToArray(), StringSplitOptions.RemoveEmptyEntries).Select(v => new { kv.Key, Value = v })))
                        {
                            httpContext.Response.AddHeader(header.Key, header.Value);
                        }

                        ResponseBody(
                            body,
                            httpContext.Response.OutputStream,
                            taskCompletionSource.SetException,
                            () => taskCompletionSource.SetResult(() => httpContext.Response.End()));
                    }
                    catch (Exception ex)
                    {
                        taskCompletionSource.SetException(ex);
                    }
                },
                taskCompletionSource.SetException);
            return taskCompletionSource.Task;
        }

        public void EndProcessRequest(IAsyncResult asyncResult)
        {
            var task = ((Task<Action>)asyncResult);
            if (task.IsFaulted)
            {
                var exception = task.Exception;
                exception.Handle(ex => ex is HttpException);
            }
            else if (task.IsCompleted)
            {
                task.Result.Invoke();
            }
        }

        class ServerVariables
        {
            readonly NameValueCollection _serverVariables;

            public ServerVariables(NameValueCollection serverVariables)
            {
                _serverVariables = serverVariables;
            }

            public string QueryString
            {
                get { return _serverVariables.Get("QUERY_STRING"); }
            }

            public string ServerName
            {
                get { return _serverVariables.Get("SERVER_NAME"); }
            }

            public string ServerPort
            {
                get { return _serverVariables.Get("SERVER_PORT"); }
            }

            public IEnumerable<KeyValuePair<string, object>> AddToEnvironment()
            {
                return _serverVariables
                    .AllKeys
                    .Where(key => !key.StartsWith("HTTP_"))
                    .Select(key => new KeyValuePair<string, object>(key, _serverVariables.Get(key)));
            }
        }


        static BodyDelegate RequestBody(Stream stream)
        {
            return (next, error, complete) => new PipeRequest(stream, next, error, complete).Go();
        }

        static void ResponseBody(BodyDelegate body, Stream stream, Action<Exception> error, Action complete)
        {
            new PipeResponse(stream, error, complete).Go(body);
        }
    }
}