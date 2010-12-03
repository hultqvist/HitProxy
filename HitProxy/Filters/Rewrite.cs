
using System;

namespace HitProxy.Filters
{
	/// <summary>
	/// Rewrite requests urls according to some rules
	/// </summary>
	public class Rewrite : Filter
	{

		public Rewrite ()
		{
		}

		public override bool Apply (Request request)
		{
			return false;
			//throw new System.NotImplementedException ();
		}
	}
}
