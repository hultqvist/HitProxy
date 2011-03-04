using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// Invoke commands based on filters
	/// </summary>
	public class Command : Filter
	{

		public Command ()
		{
		}

		public override bool Apply (Request request)
		{
			throw new System.NotImplementedException ();
		}
		
	}
}
