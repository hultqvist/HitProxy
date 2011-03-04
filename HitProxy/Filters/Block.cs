using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// Base Class for filters that block unconditionally
	/// </summary>
	public class Block : Filter
	{
		public override bool Apply (Request request)
		{
			if (request.TestClass ("block") == false)
				return false;
			
			Html status = new Html ();
			Html hr = Html.Format ("<hr/>");
			foreach (Html h in request.GetTriggerHtml ())
				status += hr + h;
			
			request.Response = new BlockedResponse ("Blocked", status);
			return true;
		}
	}
}
