
using System;

namespace HitProxy.Filters
{
	/// <summary>
	/// Block sending and receiving cookies
	/// Third party blocker
	/// Whitelist
	/// </summary>
	public class Cookies : Filter
	{

		public Cookies ()
		{
		}

		public override bool Apply (Request request)
		{
			request.RemoveHeader ("Cookie");
			return true;
		}
		
	}
}
