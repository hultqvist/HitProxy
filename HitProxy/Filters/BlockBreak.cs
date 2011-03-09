using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// Replace the request with a break page
	/// </summary>
	public class BlockBreak : Block
	{
		public override bool Apply (Request request)
		{
			if (request.TestFlags ("break") == false)
				return false;
			
			Response resp = new Response (System.Net.HttpStatusCode.ServiceUnavailable);
			request.Response = resp;
			
			resp.Template ("Take a break", Html.Format(@"<p>It's now time for a break.</p>"));
			
			return true;
		}
		
	}
}
