using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// Runs all filters in order and stops at the first non filtering
	/// Returns filtered status if all was filtered
	/// </summary>
	public class FilterListAnd : FilterList
	{
		public override bool Apply (Request request)
		{
			Filter[] list = this.ToArray ();
			foreach (Filter f in list)
				if (f.Apply (request) == false)
					return false;
			
			return true;
		}
	}
}
