using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// Block requests flagged "block"
	/// </summary>
	public class Block : Filter
	{
		public override bool Apply (Request request)
		{
			if (request.Flags["block"] == false)
				return false;
			
			Html status = new Html ();
			Html hr = Html.Format ("<hr/>");
			foreach (Html h in request.GetTriggerHtml ())
				status += h + hr;
			
			request.Response = new BlockedResponse ("Blocked", status);
			return true;
		}
	}
}
