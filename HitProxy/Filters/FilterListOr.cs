
using System;

namespace HitProxy.Filters
{
	/// <summary>
	/// Runs all filters in order and stops at the first filtering
	/// Returns filtered status if any was filtered
	/// </summary>
	public class FilterListOr : FilterList
	{
		public override bool Apply (Request request)
		{
			foreach (Filter f in ToArray ())
				if (f.Apply (request) == true)
					return true;
			
			return false;
		}

		public override string Status ()
		{
			return "<p>Apply filters in order, stop at first filter triggered</p>";
		}
	}
}
