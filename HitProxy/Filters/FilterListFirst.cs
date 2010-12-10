
using System;

namespace HitProxy.Filters
{
	/// <summary>
	/// Runs the first filter, if it returns filtered all the remaning filters will be run.
	/// if(first) {run all remaining}
	/// </summary>
	public class FilterListFirst : FilterList
	{
		public override bool Apply (Request request)
		{
			Filter[] list = ToArray ();
			if (list.Length == 0)
				return false;
			
			if (list[0].Apply (request) == false)
				return false;
			
			foreach (Filter f in ToArray ())
				f.Apply (request);
			
			return true;
		}

		public override string Status ()
		{
			return "<p>If the first filter triggers, all remaining will be applied.</p>";
		}
	}
}
