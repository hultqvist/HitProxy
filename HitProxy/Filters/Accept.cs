using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// Replace Accept headers with a common version
	/// </summary>
	public class Accept : Filter
	{
		public override bool Apply (Request request)
		{
			if (request.Flags ["pass"] == true)
				return false;
			
			request.ReplaceHeader ("Accept", "*/*");
			request.ReplaceHeader ("Accept-Charset", "utf-8");
			request.ReplaceHeader ("Accept-Encoding", "gzip, deflate");
			return true;
		}
		
	}
}

