using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using HitProxy.Http;
using ProtoBuf;
using HitProxy.Filters;

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
		ReadWriteLock listLock = new ReadWriteLock ();

		public CrossDomain ()
		{
			LoadFilters ();
			//SaveFilters ();
		}

		void LoadFilters ()
		{
			using (listLock.Write) {
				string configPath = ConfigPath ();
				if (File.Exists (configPath) == false)
					return;
				
				using (Stream s = new FileStream (configPath, FileMode.Open)) {
					watchlist = Serializer.Deserialize<List<RefererPair>> (s);
				}
			}
		}

		void SaveFilters ()
		{
			using (listLock.Read) 
			using (Stream writer = new FileStream (ConfigPath (), FileMode.Create, FileAccess.Write)) {
				ProtoBuf.Serializer.Serialize (writer, watchlist);
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
			
			using (listLock.Read) {
				foreach (RefererPair pair in watchlist) {
					
					if (pair.MatchFrom (referer) == false)
						continue;
					if (pair.MatchTo (httpRequest.Dns.NameList) == false)
						continue;
					
					httpRequest.Flags.Set (pair.Flags);
						
					if (pair.Flags ["block"]) {
						httpRequest.SetTriggerHtml (Html.Format (@"
<h1 style=""text-align:center""><a href=""{0}"" style=""font-size: 3em;"">{1}</a></h1>
<p>Blocked by: {2} <a href=""{3}?delete={4}&amp;return={5}"">delete</a></p>", httpRequest.Uri, httpRequest.Uri.Host, pair, Filters.WebUI.FilterUrl (this), pair.GetHashCode (), Uri.EscapeUriString (httpRequest.Uri.ToString ())));
						return true;
					}
						
					httpRequest.SetTriggerHtml (Html.Escape (pair.ToString ()));
					return true;
				}
			}
			
			//Already blocked, don't add to blocked list
			if (httpRequest.Flags ["block"])
				return true;
			
			using (listLock.UpgradeableRead) {
				foreach (string h in httpRequest.Dns.NameList) {
					RefererPair rp = new RefererPair (referer, h);
					if (blocked.Contains (rp) == false) {
						using (listLock.Write) {
							blocked.Insert (0, rp);
						}
					}
				}
			}
			
			//Allow empty referers
			if (referer == "")
				return false;
			
			//Default action: none(pass)
			return false;
			
			//Default action: Remove
			//httpRequest.Flags.Set ("remove");
			//Default action: Block
			/*
			httpRequest.Flags.Set ("block");
			httpRequest.SetTriggerHtml (Html.Format (@"
<h1 style=""text-align:center""><a href=""{0}"" style=""font-size: 3em;"">{1}</a></h1>
<p style=""text-align:center""><a href=""{0}"">{2}</a></p>", httpRequest.Uri, httpRequest.Uri.Host, httpRequest.Uri.PathAndQuery));
			httpRequest.SetTriggerHtml (Form (referer, httpRequest.Uri.Host, httpRequest.Uri.ToString ()));
			
			return true;
			*/
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
			if (returnUrl != null)
				returnHtml = Html.Format (@"<input type=""hidden"" name=""return"" value=""{0}"" />", returnUrl);
			
			return Html.Format (@"
<form action=""{0}"" method=""get"">
	{1}
	<tr>
	<td><input type=""text"" name=""from"" value=""{2}"" /></td>
	<td><input type=""text"" name=""to"" value=""{3}"" /></td>
	<td>
		<input type=""text"" name=""flags"" value="""" />
		<input type=""submit"" name=""action"" value=""Add custom flags"" />
		<br/>
		<input type=""submit"" name=""action"" value=""Pass"" />
		<input type=""submit"" name=""action"" value=""Fake"" />
		<input type=""submit"" name=""action"" value=""Clean"" />
		<input type=""submit"" name=""action"" value=""Remove"" />
		<input type=""submit"" name=""action"" value=""Block"" />
	</td>
	</tr>
</form>", Filters.WebUI.FilterUrl (this), returnHtml, fromHost, toHost);
		}

		public override Response Status (NameValueCollection httpGet, Request request)
		{
			Html html = new Html ();
			
			if (httpGet ["delete"] != null) {
				int item = int.Parse (httpGet ["delete"]);
				using (listLock.Write) {
					foreach (RefererPair rp in watchlist.ToArray ()) {
						if (rp.GetHashCode () == item)
							watchlist.Remove (rp);
					}
				}
				
				SaveFilters ();
			}
			
			if (httpGet ["clear"] != null) {
				using (listLock.Write) {
					blocked.Clear ();
				} 
			}
			
			if (httpGet ["action"] != null || httpGet ["flags"] != null) {
				RefererPair p = new RefererPair (httpGet ["from"], httpGet ["to"]);
				
				p.Flags.Set (httpGet ["flags"]);
				if (httpGet ["action"].Contains (" ") == false)
					p.Flags.Set (httpGet ["action"]);
				
				using (listLock.Write) {
					watchlist.Add (p);
					
					foreach (RefererPair bp in blocked.ToArray ()) {
						if (p.Match (bp))
							blocked.Remove (bp);
					}
				}
				SaveFilters ();
			}
						
			if (httpGet ["return"] != null) {
				Response resp = new Response (HttpStatusCode.Redirect, new Html ());
				resp.ReplaceHeader ("Location", httpGet ["return"]);
				return resp;
			}
			
			html += Html.Format (@"<h2>Blocked <a href=""?clear=yes"">clear</a></h2>");
			html += Html.Format ("<table><tr><th>From Domain</th><th>To Domain</th><th>Flags</th></tr>");
			html += Form ("", "");
			using (listLock.Read) {
				foreach (RefererPair pair in blocked) {
					html += Form (pair);
				}
				html += Html.Format ("</table>");
				
				html += Html.Format ("<h2>Watchlist</h2>");
				
				html += Html.Format ("<table><tr><th>From Domain</th><th>To Domain</th><th>Flags</th><th>Delete</th></tr>");
				foreach (RefererPair pair in watchlist) {
					html += Html.Format ("<tr><td>{0}</td><td>{1}</td><td>{2}</td><td><a href=\"?delete={3}\">delete</a></td></tr>", pair.FromHost, pair.ToHost, pair.Flags, pair.GetHashCode ());
				}
				html += Html.Format ("</table>");
			}
			
			html += Html.Format (@"
			<div>
				<ul>
					<li><strong>Pass</strong> Allow request to pass through unmodified</li>
					<li><strong>Fake</strong> Change referer to the root of the target host</li>
					<li><strong>Clean</strong> Change referer to the root of the source host</li>
					<li><strong>Remove</strong> Remove the referer header</li>
					<li><strong>Slow</strong> Do not modify the request but slow down the transfer speed</li>
					<li><strong>Block</strong> Block the entire request</li>
				</ul>
				<p>From/To: Wildcard(*) allowed in start of domains, applies to subdomains only</p>
				<p>Example: *example.com matches xyz.example.com and example.com but not badexample.com</p>
			</div>");
			
			return WebUI.ResponseTemplate (ToString (), html);
		}
	}

	[ProtoContract]
	class RefererPair
	{
		[ProtoMember(1)]
		public string FromHost { get; set; }

		[ProtoMember(2)]
		public string ToHost { get; set; }

		[ProtoMember(3)]
		public List<string> flags { get; set; }

		/// <summary>
		/// Flags set to matching requests
		/// </summary>
		public Flags Flags { get { return new Flags (flags); } }

		public RefererPair ()
		{
			this.flags = new List<string> ();
		}

		public RefererPair (string fromHost, string toHost) : this()
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
		
		public bool MatchFrom (string refererHost)
		{
			return MatchStrings (FromHost, refererHost);
		}

		public bool MatchTo (List<string> hosts)
		{
			foreach (string h in hosts)
				if (MatchStrings (ToHost, h))
					return true;
			return false;
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
			return "[" + Flags + " " + FromHost + " => " + ToHost + " ]";
		}

		public override int GetHashCode ()
		{
			return (FromHost + ":" + ToHost).GetHashCode ();
		}
	}
}
