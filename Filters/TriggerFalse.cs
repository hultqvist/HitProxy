
using System;

namespace PersonalProxy.Filters
{
	/// <summary>
	/// This filter does nothing
	/// and reports doing so
	/// </summary>
	public class TriggerFalse : Filter
	{
		public override bool Apply (Request request)
		{
			return false;
		}
	}
}
