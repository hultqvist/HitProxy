using System;
namespace HitProxy.Filters
{
	public abstract class ProxyTld : Filter
	{
		private readonly string tld;
		private readonly string proxy;
		
		public ProxyTld (string topLevelDomain, string proxy)
		{
			this.tld = topLevelDomain;
			this.proxy = proxy;
		}
		
		/// <summary>
		/// Catch .<tld> urls and pass them through a proxy
		/// </summary>
		public override bool Apply (Request request)
		{
			if (!request.Uri.Host.EndsWith ("."+tld))
				return false;
			
			request.Proxy = new Uri (proxy);
			return true;
		}
		
		public override string Status ()
		{
			return "This filter intercepts all requests to domains ending in " + tld
				+ " and pass them to a local running http proxy.";
		}
	}
}
