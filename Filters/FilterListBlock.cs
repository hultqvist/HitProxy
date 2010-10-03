using System;
namespace PersonalProxy.Filters
{
	/// <summary>
	/// Run all filters in list until the first blocking one
	/// </summary>
	public class FilterListBlock : FilterList
	{
		public override bool Apply (Request request)
		{
			Filter[] list = this.ToArray ();
			bool filtered = false;
			foreach (Filter f in list)
			{
				if (f.Apply (request) == true)
					filtered = true;
				if (request.Response != null)
					return filtered;
			}
			return filtered;
		}
	}
}

