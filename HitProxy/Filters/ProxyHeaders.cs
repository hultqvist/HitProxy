
using System;

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
				request.SetHeader ("Connection", "close");
			else
				request.SetHeader ("Connection", connection);
			
			if (headers != request.Count)
				return true;
			else
				return false;
		}

		public override string ToString ()
		{
			return string.Format ("[ProxyHeaders]");
		}

		public override string Status ()
		{
			return "Transform Proxy headers to http headers.";
		}
	}
}
