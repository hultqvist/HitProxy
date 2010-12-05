using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using HitProxy;
using System.Net;
using System.Threading;
using System.Collections.Specialized;

namespace HitProxy.Filters
{
	/// <summary>
	/// Filter requests using an adblock list
	/// </summary>
	public class AdBlock : Filter
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
						RegexFilter regex = RegexFilter.Parse (pattern);
						AddFilter(regex);
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
					writer.WriteLine (rf.Pattern);
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
					if (regex.Type == AdBlock.FilterType.Comment)
						continue;
					if (regex.Type == AdBlock.FilterType.NotImplemented)
						continue;
					
					if (regex.IsMatch (url) == false)
						continue;
					
					if (regex.Type == AdBlock.FilterType.Pass)
						return false;
						
					request.Block ("Adblock filter: " + regex.ToString () + "\n" + url);
					request.Response.Add ("X-AdBlock: BLOCKED: " + regex.ToString ());
					return true;
				}
			}
			
			return false;
		}

		public override string Status (NameValueCollection httpGet, Request request)
		{
			string html = "";
						
			if (httpGet["return"] != null) {
				request.Response.ReplaceHeader ("Location", httpGet["return"]);
				request.Response.HttpCode = HttpStatusCode.Redirect;
			}
			
			if (httpGet["delete"] != null) {
				int item = int.Parse (httpGet["delete"]);
				try {
					listLock.EnterWriteLock ();
					foreach (RegexFilter rf in filterList.ToArray ()) {
						if (rf.GetHashCode () == item)
						{
							filterList.Remove (rf);
							foreach(KeyValuePair<string, List<RegexFilter>> pair in hashList)
							{
								pair.Value.Remove(rf);
							}
						}
					}
				} finally {
					listLock.ExitWriteLock ();
				}
				
				SaveFilters ();
			}
						
			if (httpGet["action"] != null) {
				RegexFilter filter = RegexFilter.Parse(httpGet["pattern"]);
				
				try {
					listLock.EnterWriteLock ();
					AddFilter (filter);
				} finally {
					listLock.ExitWriteLock ();
				}
				SaveFilters ();
			}
			
			html += @"
				<h1>Add new filter</h1>
				<form action=""?"" method=""get"">
					<input type=""text"" name=""pattern"" value="""" />
					<input type=""submit"" name=""action"" value=""Block"" />
				</form>";
			
			try {
				listLock.EnterReadLock ();
				
				html += "<h1>Block List</h1>";
				foreach (RegexFilter regex in filterList)
				{
					html += "<p>" + Response.Html (regex.Pattern);
					html += " <small>" + Response.Html (regex.ToString ()) + "</small>";
					html += " <a href=\"?delete=" + regex.GetHashCode () + "\">delete</a></p>";
				}
			} finally {
				listLock.ExitReadLock ();
			}
			
			return html;
		}

		enum FilterType
		{
			Comment,
			Block,
			Pass,
			NotImplemented
		}

		class RegexFilter : Regex
		{
			private readonly string pattern;
			private readonly FilterType type;
			private readonly string wildcard;

			public string Pattern {
				get { return pattern; }
			}

			public FilterType Type {
				get { return type; }
			}

			public string Wildcard {
				get { return wildcard; }
			}

			private RegexFilter (string pattern, FilterType type, string regex, string wildcard) : base(regex)
			{
				this.pattern = pattern;
				this.type = type;
				this.wildcard = wildcard;
			}
			
			public static RegexFilter Parse (string pattern)
			{
				//Filter out headers and pattern extras
				if (pattern.Trim () == "")
					return new RegexFilter ("", FilterType.Comment, "", "");
				//Comments
				if (pattern.StartsWith ("!") || pattern.StartsWith ("["))
					return new RegexFilter (pattern, FilterType.Comment, "", "");
				//Whitelist, not implemented
				if (pattern.StartsWith ("@@"))
					return new RegexFilter (pattern, FilterType.NotImplemented, "", "");
				//Html element filters, proxy not designed to be able to implement
				if (pattern.Contains ("##"))
					return new RegexFilter (pattern, FilterType.NotImplemented, "", "");
				//Third party, not implemented
				if (pattern.EndsWith ("$third-party"))
					return new RegexFilter (pattern, FilterType.NotImplemented, "", "");
				
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
				
				return new RegexFilter (pattern, FilterType.Block, regex, wildcard);
			}
		}
	}
}
