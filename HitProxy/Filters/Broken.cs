using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// This filter is broken and will crash everytime
	/// - by design
	/// </summary>
	public class Broken : Filter
	{
		public override bool Apply (Request request)
		{
			throw new Exception ("This filter is really broken");
		}
		
		public override string Status ()
		{
			throw new Exception ("This filter is really broken");
		}
		
		public override string ToString ()
		{
			throw new Exception ("This filter is really broken");
		}
	}
}

