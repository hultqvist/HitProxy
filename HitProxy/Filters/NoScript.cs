using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// Script block - filter on type, .js application/javascript
	/// Keep list of allowed sites
	/// </summary>
	public class NoScript : Filter
	{
		public NoScript ()
		{
		}
		public override bool Apply (Request request)
		{
			throw new System.NotImplementedException ();
		}
	}
}
