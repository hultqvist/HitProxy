using System;
namespace HitProxy.Filters
{
	public class I2PProxy : ProxyTld
	{
		public I2PProxy () : base("i2p", "http://localhost:4444")
		{
		}
	}
}

