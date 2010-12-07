
using System;
using System.Collections.Generic;

namespace HitProxy.Filters
{
	/// <summary>
	/// Block sending and receiving cookies
	/// Third party blocker
	/// Whitelist
	/// </summary>
	public class Cookies : Filter
	{
		static List<CookieHeader> cookieJar = new List<CookieHeader> ();
		static List<CookieHeader> blockedJar = new List<CookieHeader> ();

		public Cookies ()
		{
		}

		public override bool Apply (Request request)
		{
			Header head = request;
			if (request.Response != null)
				head = request.Response;
			
			List<string> cookieHeader;
			List<CookieHeader> cookies;
			
			//Parse set-cookie
			cookieHeader = head.GetHeaderList ("Set-Cookie");
			cookies = ParseHeader (request.Uri.Host, cookieHeader);
			foreach (CookieHeader cr in cookies)
				cookieJar.Add (cr);
			
			//Filter set-cookie
			FilterCookie (request, cookies);
			
			//Replace set-cookie
			head.RemoveHeader ("Se-Cookie");
			foreach (CookieHeader cr in cookies)
				head.AddHeader ("Set-Cookie", GenerateHeader (cr));
			
			//Parse cookie
			cookieHeader = head.GetHeaderList ("Cookie");
			cookies = ParseHeader (request.Uri.Host, cookieHeader);
			foreach (CookieHeader cr in cookies)
				cookieJar.Add (cr);
			
			//Filter cookie
			FilterCookie (request, cookies);
			
			//Replace cookie
			head.RemoveHeader ("Cookie");
			foreach (CookieHeader cr in cookies)
				head.AddHeader ("Cookie", GenerateHeader (cr));
			return true;
		}

		void FilterCookie (Request request, List<CookieHeader> list)
		{
			foreach (CookieHeader cr in list.ToArray ()) {
				//Block third party cookies
				if (cr.ContainsKey ("domain") && (("." + request.Uri.Host).EndsWith (cr["domain"]) == false)) {
					list.Remove (cr);
					blockedJar.Add (cr);
				}
				
				//Block cookies in cross domain requests
				if (request.Referer != null) {
					string referer = new Uri (request.Referer).Host;
					if (request.Uri.Host != referer) {
						list.Remove (cr);
						blockedJar.Add (cr);
					}
				}
			}
		}

		static internal List<CookieHeader> ParseHeader (string host, List<string> headerList)
		{
			List<CookieHeader> cookies = new List<CookieHeader> ();
			
			foreach (string header in headerList) {
				CookieHeader request = new CookieHeader (host);
				
				string[] parts = header.Trim ().Split (';');
				
				foreach (string part in parts) {
					string p = part.Trim ();
					string[] keyVal = p.Split (new char[] { '=' }, 2);
					try {
						if (keyVal.Length == 2)
							request.Add (keyVal[0], keyVal[1]);
						else
							request.Add (keyVal[0], "");
					}
					catch (ArgumentException ae)
					{
						//Ignore duplicates if values are the same
						if (keyVal.Length == 1 && request[keyVal[0]] == "")
							continue;
						if (keyVal.Length == 2 && request[keyVal[0]] == keyVal[1])
							continue;
						
						throw new ArgumentException (
							string.Format ("Duplicate cookie: key={0}, header={1}",
								keyVal[0], header),
							keyVal[0], ae);
					}
				}
				cookies.Add (request);
			}
			return cookies;
		}

		string GenerateHeader (CookieHeader request)
		{
			if (request == null)
				return null;
			
			string header = "";
			foreach (KeyValuePair<string, string> kvp in request) {
				if (kvp.Value == "")
					header += kvp.Key + "; ";
				else
					header += kvp.Key + "=" + kvp.Value + "; ";
			}
			return header;
		}

		public override string Status ()
		{
			string html = "<h1>Blocked</h1>";
			foreach (CookieHeader request in blockedJar)
				html += "<p>" + request.ToString () + "</p>";
			html += "<h1>All</h1>";
			foreach (CookieHeader request in cookieJar)
				html += "<p>" + request.ToString () + "</p>";
			return html;
		}
	}

	internal class CookieHeader : Dictionary<string, string>
	{
		public string Host;
		
		public CookieHeader (string host) : base(StringComparer.OrdinalIgnoreCase)
		{
			this.Host = host;
		}

		public override string ToString ()
		{
			string text = "[CookieRequest " + Host + ": ";
			foreach (KeyValuePair<string, string> kvp in this)
				text += "; " + kvp.Key + "=" + kvp.Value;
			return text + "]";
		}
	}
}
