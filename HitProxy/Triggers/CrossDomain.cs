
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using HitProxy.Http;

namespace HitProxy.Triggers
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
	public class CrossDomain : Trigger
	{
		List<RefererPair> blocked = new List<RefererPair> ();
		List<RefererPair> watchlist = new List<RefererPair> ();
		ReaderWriterLockSlim listLock = new ReaderWriterLockSlim ();

		public CrossDomain ()
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
							p.Filter = RefererOperation.Pass;
						if (parts[0] == "Fake")
							p.Filter = RefererOperation.Fake;
						if (parts[0] == "Clean")
							p.Filter = RefererOperation.Clean;
						if (parts[0] == "Remove")
							p.Filter = RefererOperation.Remove;
						if (parts[0] == "Block")
							p.Filter = RefererOperation.Block;
						
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
						httpRequest.SetFlags ("Referer" + pair.Filter.ToString());
						
						if (pair.Filter == RefererOperation.Block) {
							httpRequest.SetFlags ("block");
							httpRequest.SetTriggerHtml (Html.Format(@"
<h1 style=""text-align:center""><a href=""{0}"" style=""font-size: 3em;"">{1}</a></h1>
<p>Blocked by: {2} <a href=""{3}?delete={4}&amp;return={5}"">delete</a></p>",
									httpRequest.Uri, httpRequest.Uri.Host, pair,
									Filters.WebUI.FilterUrl (this), pair.GetHashCode (), Uri.EscapeUriString (httpRequest.Uri.ToString ())));
							return true;
						}

						httpRequest.SetTriggerHtml (Html.Escape(pair.ToString()));
						return true;
					}
				}
			} finally {
				listLock.ExitReadLock ();
			}

			//Already blocked, don't add to blocked list
			if(httpRequest.TestFlags("block"))
				return true;
						
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

			httpRequest.SetFlags ("block");
			httpRequest.SetTriggerHtml (Html.Format (@"
<h1 style=""text-align:center""><a href=""{0}"" style=""font-size: 3em;"">{1}</a></h1>
<p style=""text-align:center""><a href=""{0}"">{2}</a></p>", httpRequest.Uri, httpRequest.Uri.Host, httpRequest.Uri.PathAndQuery));
			httpRequest.SetTriggerHtml (Form(requestPair, httpRequest.Uri.ToString()));
			return true;
		}

		private Html Form (RefererPair pair)
		{
			return Form (pair.FromHost, pair.ToHost, null);
		}

		private Html Form (RefererPair pair, string returnUrl)
		{
			return Form (pair.FromHost, pair.ToHost, returnUrl);
		}

		private Html Form (string fromHost, string toHost)
		{
			return Form (fromHost, toHost, null);
		}
		
		private Html Form (string fromHost, string toHost, string returnUrl)
		{
			Html returnHtml = new Html ();
			if(returnUrl != null)
				returnHtml = Html.Format(@"<input type=""hidden"" name=""return"" value=""{0}"" />", returnUrl);
			
			return Html.Format (@"
<form action=""{0}"" method=""get"">
	{1}
	<input type=""text"" name=""from"" value=""{2}"" />
	<input type=""text"" name=""to"" value=""{3}"" />
	<nobr>
		<input type=""submit"" name=""action"" value=""Pass"" />
		<input type=""submit"" name=""action"" value=""Fake"" />
		<input type=""submit"" name=""action"" value=""Clean"" />
		<input type=""submit"" name=""action"" value=""Remove"" />
		<input type=""submit"" name=""action"" value=""Block"" />
	</nobr>
</form>", Filters.WebUI.FilterUrl (this), returnHtml, fromHost, toHost);
		}
		
		public override Html Status (NameValueCollection httpGet, Request request)
		{
			Html html = Html.Format(@"
			<div style=""float:right; max-width: 40%;"">
				<ul>
					<li><strong>Pass</strong> Allow request to pass through unmodified</li>
					<li><strong>Fake</strong> Change referer to the root of the target host</li>
					<li><strong>Clean</strong> Change referer to the root of the source host</li>
					<li><strong>Remove</strong> Remove the referer header</li>
					<li><strong>Block</strong> Block the entire request</li>
				</ul>
				<p>From/To: Wildcard(*) allowed in start only, applies to subdomains only</p>
				<p>Example: *example.com matches xyz.example.com and example.com but not badexample.com</p>
			</div>");
			
			if (httpGet["return"] != null) {
				request.Response.ReplaceHeader ("Location", httpGet["return"]);
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
					p.Filter = RefererOperation.Pass;
				if (httpGet["action"] == "Fake")
					p.Filter = RefererOperation.Fake;
				if (httpGet["action"] == "Clean")
					p.Filter = RefererOperation.Clean;
				if (httpGet["action"] == "Remove")
					p.Filter = RefererOperation.Remove;
				if (httpGet["action"] == "Block")
					p.Filter = RefererOperation.Block;
				
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
			
			html += Html.Format(@"<h1>Blocked <a href=""?clear=yes"">clear</a></h1>");
			html += Form("", "");
			try {
				listLock.EnterReadLock ();
				
				foreach (RefererPair pair in blocked) {
					html += Form(pair);
				}
				
				html += Html.Format("<h1>Watchlist</h1>");
				foreach (RefererPair pair in watchlist) {
					html += Html.Format("<p>{0} <a href=\"?delete={1}\">delete</a></p>", pair, pair.GetHashCode ());
				}
			} finally {
				listLock.ExitReadLock ();
			}
			
			return html;
		}
	}

	public enum RefererOperation
	{
		/// <summary>
		/// Pass the referer unmodified.
		/// </summary>
		Pass,
		/// <summary>
		/// Remove the Referer header completely.
		/// </summary>
		Remove,
		/// <summary>
		/// Set the referer to the root of the target page.
		/// </summary>
		Fake,
		/// <summary>
		/// Set the referer to the root of the source page
		/// </summary>
		Clean,
		/// <summary>
		/// Block the entire request.
		/// </summary>
		Block
	}

	class RefererPair
	{
		public string FromHost;
		public string ToHost;
		public RefererOperation Filter = RefererOperation.Remove;

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
			if (pattern.StartsWith ("*")) {
				if (pattern.StartsWith ("*.")) {
					if ((match).EndsWith (pattern.Substring (1)))
						return true;
				} else if (("." + match).EndsWith ("." + pattern.Substring (1)))
					return true;
			}
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
