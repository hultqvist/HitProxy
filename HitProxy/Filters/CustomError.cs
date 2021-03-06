using System;
using System.Net;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// Custom 404 messages and other alike
	/// Inspired by http://ilpolipo.free.fr/addons/?sn=omfg
	/// </summary>
	public class CustomError : Filter
	{

		public CustomError ()
		{
		}

		public override bool Apply (Request request)
		{
			if (request.Response == null)
				return false;
			
			if (request.Response.HttpCode != System.Net.HttpStatusCode.NotFound)
				return false;
			
			Html page = HtmlTemplate.Message (HttpStatusCode.NotFound, "Not Found", Html.Format (@"
<h1>Hmm, that page wasn't really there was it?</h1>
<p>This is a custom error page from HitProxy.</p>"));
			request.Response = new Response (HttpStatusCode.NotFound, page);
			return true;
		}

		public override Html Status ()
		{
			return Html.Format ("<p>Replaces <em>\"404 - file not found\"</em> error pages with a custom one.</p>");
		}
	}
}
