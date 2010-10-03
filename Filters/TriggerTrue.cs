
using System;

namespace PersonalProxy.Filters
{
	/// <summary>
	/// This filter does nothing,
	/// but it says it does.
	/// This can be useful in Conditional lists
	/// </summary>
	public class TriggerTrue : Filter
	{
		public override bool Apply (Request request)
		{
			return true;
		}
	}
}
