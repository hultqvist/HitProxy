
using System;

namespace PersonalProxy.Filters
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
				return string.Format ("[Tamper " + name + "]");
			else
				return string.Format ("[Tamper]");
		}

		public override string Status ()
		{
			return "<p>" + last + "</p>";
		}
	}
}
