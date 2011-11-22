using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// Intercept SSL (HTTP CONNECT) and filter any requests inside using the same filters as normal HTTP is filtered
	/// </summary>
	public class InterceptSSL : Filter
	{
		public InterceptSSL ()
		{
		}
		
		public override Html Status ()
		{
			return Html.Format ("<p><strong>{1}</strong></p><p>{0}</p>",
				"Intercept HTTP CONNECT/https/ssl connections and allow for normal filtering.");
		}
		
		public override bool Apply (Request request)
		{
			if (request.Method == "CONNECT") {
				request.InterceptSSL = true;
				return true;
			}
			return false;
		}
	}
}
