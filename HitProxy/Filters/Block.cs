using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// Base Class for filters that block unconditionally
	/// </summary>
	public abstract class Block : Filter
	{
		public override bool Apply (Request request)
		{
			request.Response = new BlockedResponse ("Always block filter");
			return true;
		}
	}
}
