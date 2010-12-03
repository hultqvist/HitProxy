
using System;
using System.Net;

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
			
			request.Response = new Response (HttpStatusCode.NotFound);
			request.Response.SetData (@"<!DOCTYPE html>
<html>
<head>
	<title>NotFound</title>
</header>
<body>
	<h1>Hmm, that page wasn't really there was it?</h1>
	<p>This is a custom error page from HitProxy.</p>
</body>
</html>");
			
			return true;
		}

		public override string Status ()
		{
			return "Replaces <em>\"404 - file not found\"</em> error pages with a custom one.";
		}
	}
}
