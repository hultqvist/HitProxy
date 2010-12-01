
using System;

namespace HitProxy.Filters
{

	/// <summary>
	/// Simulates a slow network.
	/// Responsetime, increase the delay between request and response.
	/// Ratelimits the traffic to simulate a slow network.
	/// </summary>
	public class Slow : Filter
	{

		public Slow ()
		{
		}

		public override bool Apply (Request request)
		{
			throw new System.NotImplementedException ();
		}
	}
}
