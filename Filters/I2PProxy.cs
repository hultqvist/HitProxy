using System;
namespace PersonalProxy.Filters
{
	public class I2PProxy : Filter
	{
		public I2PProxy ()
		{
		}
		
		/// <summary>
		/// Catch .i2p urls and pass them through a proxy
		/// </summary>
		public override bool Apply (Request request)
		{
			if (!request.Uri.Host.EndsWith (".i2p"))
				return false;
			
			request.Proxy = new Uri ("http://localhost:4444");
			return true;
		}
		
		public override string Status ()
		{
			return @"This filter intercepts all requests to domains ending in .i2p
and pass them to a local running i2p http proxy.";
		}
		
		public override string ToString ()
		{
			return string.Format ("I2P Proxy");
		}
	}
}

