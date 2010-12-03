
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading;

namespace HitProxy.Filters
{
	/// <summary>
	/// Inspired by RefControl http://www.stardrifter.org/refcontrol/
	/// Filter referers
	///  - Block
	///  - Pass
	///  - Smart(Set to root for third party requests)
	/// 
	/// Inspired by RequestPolicy http://www.requestpolicy.com/
	/// Block third party requests
	/// With a whitelist specifying allowed:
	/// - All from X
	/// - All to X
	/// - All from X to Y
	/// 
	/// Replace HTTP redirects with page reporting the redirect
	/// </summary>
	public class Referer : Filter
	{
		List<RefererPair> blocked = new List<RefererPair> ();
		List<RefererPair> watchlist = new List<RefererPair> ();
		ReaderWriterLockSlim listLock = new ReaderWriterLockSlim ();

		public Referer ()
		{
			LoadFilters ();
			SaveFilters ();
		}

		void LoadFilters ()
		{
			try {
				listLock.EnterWriteLock ();
				string configPath = ConfigPath ("Referer");
				
				if (File.Exists (configPath) == false)
					return;
				
				using (TextReader reader = new StreamReader (new FileStream (configPath, FileMode.Open, FileAccess.Read))) {
					string pattern;
					while ((pattern = reader.ReadLine ()) != null) {
						string[] parts = pattern.Split (' ');
						if (parts.Length != 3)
							continue;
						
						RefererPair p = new RefererPair (parts[1], parts[2]);
						if (parts[0] == "Pass")
							p.Filter = RefererFiltering.Pass;
						if (parts[0] == "Fake")
							p.Filter = RefererFiltering.Fake;
						if (parts[0] == "Remove")
							p.Filter = RefererFiltering.Remove;
						if (parts[0] == "Block")
							p.Filter = RefererFiltering.Block;
						
						watchlist.Add (p);
					}
				}
			} finally {
				listLock.ExitWriteLock ();
			}
		}

		void SaveFilters ()
		{
			TextWriter writer = null;
			try {
				listLock.EnterReadLock ();
				writer = new StreamWriter (new FileStream (ConfigPath ("Referer"), FileMode.Create, FileAccess.Write));
				foreach (RefererPair pair in watchlist) {
					writer.WriteLine (pair.Filter + " " + pair.FromHost + " " + pair.ToHost);
				}
			} finally {
				listLock.ExitReadLock ();
				writer.NullSafeDispose ();
			}
		}

		public override bool Apply (Request httpRequest)
		{
			string referer;
			string request;
			if (httpRequest.Referer == null)
				referer = "";
			else
				referer = new Uri (httpRequest.Referer).Host;
			request = httpRequest.Uri.Host;
			
			if (request == referer)
				return false;
			
			RefererPair requestPair = new RefererPair (referer, request);
			
			try {
				listLock.EnterReadLock ();
				foreach (RefererPair pair in watchlist) {
				
					if (pair.Match (requestPair)) {
						if (pair.Filter == RefererFiltering.Block) {
							httpRequest.Block ("Blocked thirdparty", @"
<h1 style=""text-align:center""><a href=""" + httpRequest.Uri + @""" style=""font-size: 3em;"">" + Response.Html (httpRequest.Uri.Host) + @"</a></h1>
<p>Blocked by: " + pair + @"
<a href=""" + WebUI.FilterUrl (this) + "?delete=" + pair.GetHashCode () + "&amp;return=" + Uri.EscapeUriString(httpRequest.Uri.ToString())+"\">delete</a></p>");
							httpRequest.Response.SetHeader ("Cache-Control", "no-cache, must-revalidate");
							httpRequest.Response.SetHeader ("Pragma", "no-cache");
							httpRequest.Response.Add ("X-Referer-Filter: BLOCKED: " + pair);
							return true;
						}
						if (pair.Filter == RefererFiltering.Fake) {
							httpRequest.RemoveHeader ("Referer");
							httpRequest.Add ("Referer: http://" + httpRequest.Uri.Host + "/");
						}
						if (pair.Filter == RefererFiltering.Remove)
							httpRequest.RemoveHeader ("Referer");
						//else, pass unmodified
						return false;
					}
				}
			} finally {
				listLock.ExitReadLock ();
			}
			
			try {
				listLock.EnterUpgradeableReadLock ();
				if (blocked.Contains (requestPair) == false) {
					listLock.EnterWriteLock ();
					blocked.Insert (0, requestPair);
				}
			} finally {
				if (listLock.IsWriteLockHeld)
					listLock.ExitWriteLock ();
				listLock.ExitUpgradeableReadLock ();
			}
			
			if (requestPair.FromHost == "")
				return false;
			
			httpRequest.Block ("Referer mismatch", @"<h1 style=""text-align:center""><a href=""" + Response.Html(httpRequest.Uri.ToString()) + @""" style=""font-size: 3em;"">" + Response.Html (httpRequest.Uri.Host) + @"</a></h1>
<form action=""" + WebUI.FilterUrl (this) + @""" method=""get"">
	<input type=""hidden"" name=""return"" value=""" + Response.Html( httpRequest.Uri.ToString()) + @""" />
	<input type=""text"" name=""from"" value=""" + Response.Html(requestPair.FromHost.ToString()) + @""" />
	<input type=""text"" name=""to"" value=""" + Response.Html(requestPair.ToHost.ToString()) + @""" />
	<input type=""submit"" name=""action"" value=""Pass"" />
	<input type=""submit"" name=""action"" value=""Fake"" />
	<input type=""submit"" name=""action"" value=""Remove"" />
	<input type=""submit"" name=""action"" value=""Block"" />
</form>");
			httpRequest.Response.SetHeader ("Cache-Control", "no-cache, must-revalidate");
			httpRequest.Response.SetHeader ("Pragma", "no-cache");
			httpRequest.Response.HttpCode = HttpStatusCode.ServiceUnavailable;
			httpRequest.Response.Add ("X-Referer-Filter: BLOCKED: Unmatched");
			
			return true;
		}

		public override string Status (NameValueCollection httpGet, Request request)
		{
			string html = @"
			<div style=""float:right;"">
				<ul>
					<li><strong>Pass</strong> Allow request to pass through unmodified</li>
					<li><strong>Fake</strong> Change referer to the root of the target host</li>
					<li><strong>Remove</strong> Remove the referer header</li>
					<li><strong>Block</strong> Block the entire request</li>
				</ul>
				<p>From/To: Wildcard(*) allowed in start only, applies to subdomains only</p>
				<p>Example: *example.com matches xyz.example.com and example.com but not badexample.com</p>
			</div>";
			
			if (httpGet["return"] != null) {
				request.Response.SetHeader ("Location", httpGet["return"]);
				request.Response.HttpCode = HttpStatusCode.Redirect;
			}
			
			if (httpGet["delete"] != null) {
				int item = int.Parse (httpGet["delete"]);
				try {
					listLock.EnterWriteLock ();
					foreach (RefererPair rp in watchlist.ToArray ()) {
						if (rp.GetHashCode () == item)
							watchlist.Remove (rp);
					}
				} finally {
					listLock.ExitWriteLock ();
				}
				
				SaveFilters ();
			}
			
			if (httpGet["clear"] != null) {
				try {
					listLock.EnterWriteLock ();
					blocked.Clear ();
				} finally {
					listLock.ExitWriteLock ();
				}
			}
			
			if (httpGet["action"] != null) {
				RefererPair p = new RefererPair (httpGet["from"], httpGet["to"]);
				if (httpGet["action"] == "Pass")
					p.Filter = RefererFiltering.Pass;
				if (httpGet["action"] == "Fake")
					p.Filter = RefererFiltering.Fake;
				if (httpGet["action"] == "Remove")
					p.Filter = RefererFiltering.Remove;
				if (httpGet["action"] == "Block")
					p.Filter = RefererFiltering.Block;
								
				try {
					listLock.EnterWriteLock ();
					watchlist.Add (p);
					
					foreach (RefererPair bp in blocked.ToArray ()) {
						if (p.Match (bp))
							blocked.Remove (bp);
					}
				} finally {
					listLock.ExitWriteLock ();
				}
				SaveFilters ();
			}
			
			html += @"<h1>Blocked <a href=""?clear=yes"">clear</a></h1>
							<form action=""?"" method=""get"">
								<input type=""text"" name=""from"" value="""" />
								<input type=""text"" name=""to"" value="""" />
								<input type=""submit"" name=""action"" value=""Pass"" />
								<input type=""submit"" name=""action"" value=""Fake"" />
								<input type=""submit"" name=""action"" value=""Remove"" />
								<input type=""submit"" name=""action"" value=""Block"" />
							</form>";
			try {
				listLock.EnterReadLock ();
				
				foreach (RefererPair pair in blocked) {
					html += @"
									<form action=""?"" method=""get"">
									<input type=""text"" name=""from"" value=""" + pair.FromHost + @""" />
									<input type=""text"" name=""to"" value=""" + pair.ToHost + @""" />
									<input type=""submit"" name=""action"" value=""Pass"" />
									<input type=""submit"" name=""action"" value=""Fake"" />
									<input type=""submit"" name=""action"" value=""Remove"" />
									<input type=""submit"" name=""action"" value=""Block"" />
								</form>";
				}
				
				html += "<h1>Watchlist</h1>";
				foreach (RefererPair pair in watchlist) {
					html += "<p>" + pair + " <a href=\"?delete=" + pair.GetHashCode () + "\">delete</a></p>";
				}
			} finally {
				listLock.ExitReadLock ();
			}
			
			return html;
		}
	}

	public enum RefererFiltering
	{
		Pass,
		Remove,
		Fake,
		Block
	}

	class RefererPair
	{
		public string FromHost;
		public string ToHost;
		public RefererFiltering Filter = RefererFiltering.Remove;

		public RefererPair (string fromHost, string toHost)
		{
			this.FromHost = fromHost;
			this.ToHost = toHost;
		}

		public bool Match (RefererPair requestPair)
		{
			if (MatchStrings (FromHost, requestPair.FromHost) == false)
				return false;
			if (MatchStrings (ToHost, requestPair.ToHost) == false)
				return false;	
			return true;
		}
		
		/// <summary>
		/// Matches match against pattern where pattern
		/// can start with wildcard *
		/// </summary>
		bool MatchStrings (string pattern, string match)
		{
			if (pattern == "*")
				return true;
			if (pattern == match)
				return true;
			if (pattern.StartsWith ("*"))
				if (("." + match).EndsWith ("." + pattern.Substring (1)))
					return true;
			return false;
		}

		public override bool Equals (object obj)
		{
			return GetHashCode () == obj.GetHashCode ();
		}

		public override string ToString ()
		{
			return "[" + Filter + " " + FromHost + " => " + ToHost + " ]";
		}
		public override int GetHashCode ()
		{
			return (FromHost + ":" + ToHost).GetHashCode ();
		}
	}
}
