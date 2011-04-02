using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using HitProxy;
using HitProxy.Http;
using System.Net;
using System.Threading;
using System.Collections.Specialized;

namespace HitProxy.Triggers
{
	/// <summary>
	/// Filter requests using an adblock list
	/// </summary>
	public class AdBlock : Trigger
	{
		/// <summary>
		/// Lock the lists during updates
		/// </summary>
		ReaderWriterLockSlim listLock = new ReaderWriterLockSlim ();
		/// <summary>
		/// Design from AdBlock Plus addon for firefox:
		/// First string is the longest continous string within the pattern without any wildcards.
		/// </summary>
		Dictionary<string, List<RegexFilter>> hashList = new Dictionary<string, List<RegexFilter>> ();
		/// <summary>
		/// A list of all blocking regex.
		/// </summary>
		List<RegexFilter> filterList = new List<RegexFilter> ();

		//Further ideas are to have a prioritized list where every regex is
		//given a point for every match, higher point get the regex earlier
		//on the list. This must be on a per regex basis not the presenting match

		readonly char[] wildcards = new char[] { '?', '*', '^' };

		private static readonly string configPath = ConfigPath ("AdBlock");

		public AdBlock ()
		{
			hashList.Add ("", new List<RegexFilter> ());
			
			LoadFilters ();
		}

		void LoadFilters ()
		{
			try {
				listLock.EnterWriteLock ();
				
				if (File.Exists (configPath) == false)
					return;
				
				using (TextReader reader = new StreamReader (new FileStream (configPath, FileMode.Open, FileAccess.Read))) {
					string pattern;
					while ((pattern = reader.ReadLine ()) != null) {
						string[] parts = pattern.Split ('\t');
						RegexFilter regex;
						if (parts.Length < 2)
							regex = RegexFilter.Parse (pattern, new Flags ("block"));
						else
							regex = RegexFilter.Parse (parts[0], new Flags (parts[1]));
						if (regex != null)
							AddFilter (regex);
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
				writer = new StreamWriter (new FileStream (configPath, FileMode.Create, FileAccess.Write));
				foreach (RegexFilter rf in filterList) {
					writer.WriteLine (rf.Pattern + "\t" + rf.Flags.Serialize ());
				}
			} finally {
				listLock.ExitReadLock ();
				writer.NullSafeDispose ();
			}
		}

		private void AddFilter (RegexFilter regex)
		{
			filterList.Add (regex);
			
			//Get continous strings from pattern
			string[] parts = regex.Wildcard.Split (wildcards);
			
			bool added = false;
			foreach (string part in parts) {
				if (part.Length == 0)
					continue;
				
				if (part.Length > 8) {
					try {
						hashList[part].Add (regex);
					} catch (KeyNotFoundException) {
						List<RegexFilter> list = new List<RegexFilter> ();
						list.Add (regex);
						hashList.Add (part, list);
					}
					added = true;
				}
			}
			if (added == false)
				hashList[""].Add (regex);
		}

		public override bool Apply (Request request)
		{
			Uri u = request.Uri;
			string url = u.Host + u.PathAndQuery;
			if (u.Scheme == "connect")
				url = "https://" + url;
			else
				url = u.Scheme + "://" + url;
			
			foreach (KeyValuePair<string, List<RegexFilter>> kvp in hashList) {
				if (kvp.Key != "" && url.Contains (kvp.Key) == false)
					continue;
				
				foreach (RegexFilter regex in kvp.Value) {
					
					if (regex.IsMatch (url) == false)
						continue;
					
					request.Flags.Set (regex.Flags);
					request.SetTriggerHtml (Html.Escape ("Adblock filter: " + regex.ToString () + "\n" + url));
					return true;
				}
			}
			
			return false;
		}

		public override Html Status (NameValueCollection httpGet, Request request)
		{
			Html html = new Html ();
			
			if (httpGet["return"] != null) {
				request.Response.ReplaceHeader ("Location", httpGet["return"]);
				request.Response.HttpCode = HttpStatusCode.Redirect;
			}
			
			if (httpGet["delete"] != null) {
				int item = int.Parse (httpGet["delete"]);
				try {
					listLock.EnterWriteLock ();
					foreach (RegexFilter rf in filterList.ToArray ()) {
						if (rf.GetHashCode () == item) {
							filterList.Remove (rf);
							foreach (KeyValuePair<string, List<RegexFilter>> pair in hashList) {
								pair.Value.Remove (rf);
							}
						}
					}
				} finally {
					listLock.ExitWriteLock ();
				}
				
				SaveFilters ();
			}
			
			if (httpGet["action"] != null) {
				RegexFilter filter = RegexFilter.Parse (httpGet["pattern"], new Flags (httpGet["flags"]));
				
				try {
					listLock.EnterWriteLock ();
					AddFilter (filter);
				} finally {
					listLock.ExitWriteLock ();
				}
				SaveFilters ();
			}
			
			html += Html.Format (@"
				<h1>Add new filter</h1>
				<form action=""?"" method=""get"">
						<input type=""text"" name=""pattern"" value="""" />
						<input type=""text"" name=""flags"" value=""block"" />
						<input type=""submit"" name=""action"" value=""Set"" />
				</form>");
			
			try {
				listLock.EnterReadLock ();
				
				html += Html.Format ("<h1>Block List</h1>");
				html += Html.Format ("<table>" +
					"<tr><th>Pattern</th><th>Flags</th><th></th></th>");
				foreach (RegexFilter regex in filterList) {
					html += Html.Format ("<tr><td>{1}</td><td><a href=\"?delete={2}\">delete</a></td><td>{0}</td></tr>", regex.Pattern, regex.Flags, regex.GetHashCode ());
				}
				html += Html.Format ("</table>");
			} finally {
				listLock.ExitReadLock ();
			}
			
			return html;
		}

		class RegexFilter : Regex
		{
			private readonly string fpattern;
			public readonly Flags Flags = new Flags ();
			private readonly string wildcard;

			public override string ToString ()
			{
				return string.Format ("[RegexFilter: Pattern={0}, Flags={1}]", wildcard, Flags);
			}

			public string Pattern {
				get { return fpattern; }
			}

			public string Wildcard {
				get { return wildcard; }
			}

			private RegexFilter (string pattern, Flags flags, string regex, string wildcard) : base(regex)
			{
				this.fpattern = pattern;
				this.Flags.Set (flags);
				this.wildcard = wildcard;
			}

			public static RegexFilter Parse (string pattern, Flags flags)
			{
				//Filter out headers and pattern extras
				if (pattern.Trim () == "")
					return null;
				//Comments
				if (pattern.StartsWith ("!") || pattern.StartsWith ("["))
					return null;
				//Whitelist, not implemented
				if (pattern.StartsWith ("@@"))
					return null;
				//Html element filters, proxy not designed to be able to implement
				if (pattern.Contains ("##"))
					return null;
				//Third party, not implemented
				if (pattern.EndsWith ("$third-party"))
					return null;
				
				string regex = pattern.Trim ();
				
				//Beginning of
				if (pattern.StartsWith ("|")) {
					//Beginning of domain
					if (pattern.StartsWith ("||")) {
						if (pattern.EndsWith ("^"))
							regex = regex.Substring (2, regex.Length - 3);
						else
							regex = regex.Substring (2, regex.Length - 2);
						
						regex = "*" + regex + "*";
					} else {
						//Beginning of address
						regex = regex.Substring (1) + "*";
					}
				} else {
					regex = "*" + regex;
				}
				
				string wildcard = regex;
				
				//Wildcard to RegEx
				regex = Regex.Escape (regex);
				regex = regex.Replace ("\\*", ".*").Replace ("\\?", ".");
				if (regex.StartsWith (".*"))
					regex = regex.Substring (2);
				else
					regex = "^" + regex;
				if (regex.EndsWith (".*"))
					regex = regex.Substring (0, regex.Length - 2);
				else
					regex = regex + "$";
				
				return new RegexFilter (pattern, flags, regex, wildcard);
			}
		}
	}
}
