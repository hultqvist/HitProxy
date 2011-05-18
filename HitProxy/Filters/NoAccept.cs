using System;
using HitProxy.Http;
namespace HitProxy.Filters
{
	public class NoAccept : Filter
	{
		public override bool Apply (Request request)
		{
			if (request.Flags["pass"] == true)
				return false;
			
			request.RemoveHeader ("Accept");
			request.RemoveHeader ("Accept-Charset");
			request.RemoveHeader ("Accept-Encoding");
			return true;
		}
		
	}
}

