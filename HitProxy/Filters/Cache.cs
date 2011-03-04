using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// Caching - force caching(send cache headers to browser)
	/// 
	/// Local cached of popular sources
	/// </summary>
	public class Cache : Filter
	{
		public override bool Apply (Request request)
		{
			throw new System.NotImplementedException ();
		}
		
	}
}
