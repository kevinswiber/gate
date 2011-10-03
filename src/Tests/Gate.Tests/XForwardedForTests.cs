using System;
using NUnit;
using System.Collections.Generic;
using Gate;
using NUnit.Framework;

namespace Gate.Tests
{
	[TestFixture]
	public class XForwardedForTests
	{
		IDictionary<string, object> dict;
		
		[SetUp]
		public void SetUp()
		{
			dict = new Dictionary<string, object>();
		}
		
		public void SetXFF(string xForwardedFor)
		{
			new Environment(dict).Headers = new Dictionary<string, string>() { { "x-forwarded-for", xForwardedFor } };
		}
		
		[Test]
		public void No_headers_returns_null()
		{
			var host = dict.GetRemoteHosts();
			Assert.That(host, Is.Null);
		}
		[Test]
		public void No_xff_header_returns_null()
		{
			var host = dict.GetRemoteHosts();
			new Environment(dict).Headers = new Dictionary<string, string>();
			Assert.That(host, Is.Null);
		}

        [Test]
        public void One_host()
        {
            SetXFF("127.0.0.1");
            var host = dict.GetRemoteHosts();
            Assert.That(host, Is.EqualTo(new string[] { "127.0.0.1" }));
        }

        [Test]
        public void Two_hosts()
        {
            SetXFF("8.8.8.8,127.0.0.1");
            var host = dict.GetRemoteHosts();
            Assert.That(host, Is.EqualTo(new string[] { "8.8.8.8", "127.0.0.1" }));
        }

        [Test]
        public void Originating_host()
        {
            SetXFF("8.8.8.8");
            var host = dict.GetOriginatingHosts(0);
            Assert.That(host, Is.EqualTo(new string[] { "8.8.8.8" }));
        }

        [Test]
        public void Originating_host_reverse_proxied()
        {
            SetXFF("8.8.8.8,127.0.0.1");
            var host = dict.GetOriginatingHosts(1);
            Assert.That(host, Is.EqualTo(new string[] { "8.8.8.8" }));
        }

        [Test]
        public void Originating_host_forward_proxied()
        {
            SetXFF("4.4.4.4,8.8.8.8");
            var host = dict.GetOriginatingHosts(0);
            Assert.That(host, Is.EqualTo(new string[] { "4.4.4.4", "8.8.8.8" }));
        }
        [Test]
        public void Originating_host_forward_and_reverse_proxied()
        {
            SetXFF("4.4.4.4,8.8.8.8,127.0.0.1");
            var host = dict.GetOriginatingHosts(1);
            Assert.That(host, Is.EqualTo(new string[] { "4.4.4.4", "8.8.8.8" }));
        }
	}
}

