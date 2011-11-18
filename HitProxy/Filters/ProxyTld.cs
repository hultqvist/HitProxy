using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	public abstract class ProxyTld : Filter
	{
		private readonly string tld;
		DnsLookup proxyDns;
		Uri proxyUri;
		
		public ProxyTld (string topLevelDomain, string proxy)
		{
			this.tld = topLevelDomain;
			
			this.proxyUri = new Uri (proxy);
			this.proxyDns = DnsLookup.Get (proxyUri.Host);
		}
		
		/// <summary>
		/// Catch .<tld> urls and pass them through a proxy
		/// </summary>
		public override bool Apply (Request request)
		{
			if (!request.Uri.Host.EndsWith ("." + tld))
				return false;
			
			request.Proxy = proxyUri;
			request.ProxyDns = proxyDns;
			return true;
		}
		
		public override Html Status ()
		{
			return Html.Format ("<p>This filter intercepts all requests to domains ending in <strong>{0}</strong> and pass them to a local running http proxy.</p>", tld);
		}
	}
}
