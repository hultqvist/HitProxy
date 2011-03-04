
using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// Inspired by TamperData http://tamperdata.mozdev.org/
	/// Return webpage with all parameters filled into visible fields, ready to manipulate and send back.
	/// </summary>
	public class Tamper : Filter
	{
		string name = "";
		string last = "";

		public Tamper ()
		{
		}

		public Tamper (string name)
		{
			this.name = name;
		}

		public override bool Apply (Request request)
		{
			lock (this) {
				last = request.FirstLine + "<br/>";
				foreach (string header in request)
					last += header + "<br/>";
				
				if (request.Response != null) {
					last += "<br/>" + request.Response.FirstLine + "<br/>";
					foreach (string header in request.Response)
						last += header + "<br/>";
				}
			}
			return false;
		}

		public override string ToString ()
		{
			if (name.Length > 0)
				return "Tamper: " + name;
			else
				return "Tamper";
		}

		public override Html Status ()
		{
			return Html.Format ("<p>{0}</p>", last);
		}
	}
}
