
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
		static List<CookieRequest> cookieJar = new List<CookieRequest> ();
		static List<CookieRequest> blockedJar = new List<CookieRequest> ();

		public Cookies ()
		{
		}

		public override bool Apply (Request request)
		{
			Header head = request;
			if (request.Response != null)
				head = request.Response;
			
			List<string> cookieHeader;
			List<CookieRequest> cookies;
			
			//Parse set-cookie
			cookieHeader = head.GetHeaderList ("Set-Cookie");
			cookies = ParseHeader (request.Uri.Host, cookieHeader);
			foreach (CookieRequest cr in cookies)
				cookieJar.Add (cr);
			
			//Filter set-cookie
			FilterCookie (request, cookies);
			
			//Replace set-cookie
			head.RemoveHeader ("Se-Cookie");
			foreach (CookieRequest cr in cookies)
				head.AddHeader ("Set-Cookie", GenerateHeader (cr));
			
			//Parse cookie
			cookieHeader = head.GetHeaderList ("Cookie");
			cookies = ParseHeader (request.Uri.Host, cookieHeader);
			foreach (CookieRequest cr in cookies)
				cookieJar.Add (cr);
			
			//Filter cookie
			FilterCookie (request, cookies);
			
			//Replace cookie
			head.RemoveHeader ("Cookie");
			foreach (CookieRequest cr in cookies)
				head.AddHeader ("Cookie", GenerateHeader (cr));
			return true;
		}

		void FilterCookie (Request request, List<CookieRequest> list)
		{
			foreach (CookieRequest cr in list.ToArray ()) {
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

		static internal List<CookieRequest> ParseHeader (string host, List<string> headerList)
		{
			List<CookieRequest> cookies = new List<CookieRequest> ();
			
			foreach (string header in headerList) {
				CookieRequest request = new CookieRequest (host);
				
				string[] parts = header.Trim ().Split (';');
				
				foreach (string part in parts) {
					string p = part.Trim ();
					string[] keyVal = p.Split (new char[] { '=' }, 2);
					if (keyVal.Length == 2)
						request.Add (keyVal[0], keyVal[1]);
					else
						request.Add (keyVal[0], "");
				}
				cookies.Add (request);
			}
			return cookies;
		}

		string GenerateHeader (CookieRequest request)
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
			foreach (CookieRequest request in blockedJar)
				html += "<p>" + request.ToString () + "</p>";
			html += "<h1>All</h1>";
			foreach (CookieRequest request in cookieJar)
				html += "<p>" + request.ToString () + "</p>";
			return html;
		}
	}

	internal class CookieRequest : Dictionary<string, string>
	{
		public string Host;
		
		public CookieRequest (string host) : base(StringComparer.OrdinalIgnoreCase)
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
