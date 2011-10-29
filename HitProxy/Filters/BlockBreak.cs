using System;
using HitProxy.Http;
using System.Net;

namespace HitProxy.Filters
{
	/// <summary>
	/// Replace the request with a break page
	/// </summary>
	public class BlockBreak : Block
	{
		public override bool Apply (Request request)
		{
			if (request.Flags ["break"] == false)
				return false;
			
			Html page = HtmlTemplate.Message (HttpStatusCode.ServiceUnavailable, "Take a break", Html.Format (@"<p>It's now time for a break.</p>"));
			request.Response = new Response (HttpStatusCode.ServiceUnavailable, page);
			return true;
		}
		
	}
}
