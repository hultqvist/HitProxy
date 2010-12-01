using System;

namespace HitProxy.Filters
{
	/// <summary>
	/// Force https transparently by requesting the ssl version.
	/// This filter only does the scheme translation: http->https.
	/// 
	/// Block http->https requests?
	/// Not possible since referer is not sent from https sites, possibly block non html without referer
	/// 
	/// Use EFF:s https everywhere ruleset to give instant redirects.
	/// https://www.eff.org/https-everywhere
	/// </summary>
	public class TransparentSSL : Filter
	{
		public TransparentSSL ()
		{
		}
		
		public override bool Apply (Request request)
		{
			throw new NotImplementedException ();
		}
	}
}
