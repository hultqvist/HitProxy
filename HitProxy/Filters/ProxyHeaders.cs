
using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// Filter Proxy headers.
	/// Prevent proxy specific headers from being transferred to the remote server.
	/// </summary>
	public class ProxyHeaders : Filter
	{
		public override bool Apply (Request request)
		{
			int headers = request.Count;
			
			string connection = request.GetHeader ("Proxy-Connection");
			request.RemoveHeader ("Proxy-Connection");
			if (connection == null)
				request.ReplaceHeader ("Connection", "close");
			else
				request.ReplaceHeader ("Connection", connection);
			
			if (headers != request.Count)
				return true;
			else
				return false;
		}

		public override Html Status ()
		{
			return Html.Format("<p>Transform Proxy headers to http headers.</p>");
		}
	}
}
