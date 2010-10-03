
using System;

namespace PersonalProxy.Filters
{
	/// <summary>
	/// Base Class for filters that block unconditionally
	/// </summary>
	public abstract class Block : Filter
	{
		public override bool Apply (Request request)
		{
			request.Block ("Always block filter");
			return true;
		}
	}
}
