using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	public class Referer : Filter
	{
		public Referer ()
		{
		}
		
		public override bool Apply (Request request)
		{
			if (request.Flags["Fake"]) {
				request.ReplaceHeader ("Referer", "http://" + request.Uri.Host + "/");
				return true;
			}
			if (request.Flags["Clean"]) {
				string referer = "";
				if (request.Referer != null)
					referer = new Uri (request.Referer).Host;
			
				request.ReplaceHeader ("Referer", "http://" + referer + "/");
				return true;
			}
			if (request.Flags["Remove"])
			{
				request.RemoveHeader ("Referer");
				return true;
			}
			
			//else, pass unmodified
			return false;
		}
	}
}

