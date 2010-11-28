using System;

namespace PersonalProxy.Filters
{
	
	/// <summary>
	/// Simulates network delay and low bandwidth
	/// </summary>
	public class DelaySlow : Filter
	{
		public DelaySlow ()
		{
		}
		
		public override bool Apply (Request request)
		{
			return true;
		}
	}
}

