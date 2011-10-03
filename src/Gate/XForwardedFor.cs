using System;
using System.Linq;
using System.Collections.Generic;

namespace Gate
{
	public static class XForwardedFor
    {
        public static string[] GetRemoteHosts(this IDictionary<string, object> env)
        {
            var headers = (env as Gate.Environment ?? new Gate.Environment(env)).Headers;

            if (headers == null)
                return null;

            var xff = headers.ContainsKey("x-forwarded-for") ? headers["x-forwarded-for"] : null;

            if (string.IsNullOrWhiteSpace(xff))
                return null;

            return xff.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
        }

        public static string[] GetOriginatingHosts(this IDictionary<string, object> env, int numKnownProxies)
        {
            var hosts = env.GetRemoteHosts();
            return hosts.Take(hosts.Count() - numKnownProxies).ToArray();
        }
	}
}

