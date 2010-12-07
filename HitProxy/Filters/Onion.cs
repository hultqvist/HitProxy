using System;
namespace HitProxy.Filters
{
	public class Onion : ProxyTld
	{
		public Onion () : base("onion", "socks://localhost:9050")
		{
		}
	}
}
