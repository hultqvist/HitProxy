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
			if (request.TestClass ("RefererFake")) {
				request.ReplaceHeader ("Referer", "http://" + request.Uri.Host + "/");
				return true;
			}
			if (request.TestClass ("RefererClean")) {
				string referer = "";
				if (request.Referer != null)
					referer = new Uri (request.Referer).Host;
			
				request.ReplaceHeader ("Referer", "http://" + referer + "/");
				return true;
			}
			if (request.TestClass ("RefererRemove"))
			{
				request.RemoveHeader ("Referer");
				return true;
			}
			
			//else, pass unmodified
			return false;
		}
	}
}

