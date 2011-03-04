using System;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Collections.Specialized;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// Filter, remove or modify headers
	/// </summary>
	public class UserAgent : Filter
	{
		string[] platform = { "Windows", "X11", "Macintosh" };
		//, "iPad", "iPhone" 
		string[] arch = { "Linux x86_64", "Linux i686", "Linux i586", "FreeBSD i386", "Intel Mac OS X 10.5", "Intel Mac OS X 10_5_8", "Intel Mac OS X 10_6_3", "PPC Mac OS X 10.5", "Windows NT 5.1", "Windows NT 5.2",
		"Windows NT 6.0", "Windows NT 6.1" };
		//, "CPU iPhone OS 3_2 like Mac OS X", "CPU OS 3_2 like Mac OS X" 
		string[] lang;
		//, "AppleWebKit/531.21.10 (KHTML, like Gecko) Version/4.0.4 Mobile/7B314"
		string[] engine = { "AppleWebKit/533.16 (KHTML, like Gecko) Version/5.0", "AppleWebKit/533.16 (KHTML, like Gecko) Version/4.1", "AppleWebKit/533.4 (KHTML, like Gecko) Version/4.1", "AppleWebKit/531.22.7 (KHTML, like Gecko) Version/4.0.5 ", "AppleWebKit/528.16 (KHTML, like Gecko) Version/4.0 ", "Gecko/20100401", "Gecko/20121223", "Gecko/2008092313", "Gecko/20100614", "Gecko/20100625",
		"Gecko/20100403", "Gecko/20100401", "Gecko/20100404", "Gecko/20100401", "Gecko/20100101", "Gecko/20100115", "Gecko/20091215", "Gecko/20090612", "Gecko/20090624", "AppleWebKit/534.2 (KHTML, like Gecko)",
		"AppleWebKit/534.1 (KHTML, like Gecko)", "AppleWebKit/533.2 (KHTML, like Gecko)", "AppleWebKit/533.3 (KHTML, like Gecko)" };
		string[] browser = { "Safari/533.16", "Safari/533.4", "Safari/533.3", "Safari/534.1", "Safari/534.2", "Safari/528.16", "Firefox/4.0 (.NET CLR 3.5.30729)", "Firefox/3.5", "Firefox/3.6", "Firefox/3.5",
		"Firefox/3.5.6", "Chrome/6.0.428.0", "Chrome/6.0.422.0", "Chrome/6.0", "Chrome/5.0.357.0" };
		string[] os = { "Fedora/3.5.9-2.fc12 Firefox/3.5.9", "Ubuntu/8.04 (hardy)", "Ubuntu/9.10 (karmic)", "Gentoo", "Ubuntu/9.25 (jaunty)", "Ubuntu/10.04 (lucid)", "Fedora/3.6.3-4.fc13", "SUSE/3.6.3-1.1", "", "",
		"" };

		/// <summary>
		/// Keep a single useragent for specific pages
		/// key = domain name
		/// value = User-agent
		/// </summary>
		Dictionary<string, UserAgentRule> staticAgent = new Dictionary<string, UserAgentRule> ();
		ReaderWriterLockSlim listLock = new ReaderWriterLockSlim ();

		private Random rand = new Random ();
		private string GetRandom (string[] list)
		{
			lock (rand) {
				int index = rand.Next () % list.Length;
				return list[index];
			}
		}

		private string RandomUserAgent ()
		{
			return "Mozilla/5.0 (" + GetRandom (platform) + "; U; " + GetRandom (arch) + "; " + GetRandom (lang) + ") " + GetRandom (engine) + " " + GetRandom (browser) + " " + GetRandom (os);
		}

		public UserAgent ()
		{
			//Prepare random pool
			//Languages
			CultureInfo[] cultures = CultureInfo.GetCultures (CultureTypes.AllCultures);
			lang = new string[cultures.Length];
			for (int n = 0; n < cultures.Length; n++) {
				lang[n] = cultures[n].Name;
			}
			
			//Load static settings
			loadSettings ();
		}

		void loadSettings ()
		{
			try {
				listLock.EnterWriteLock ();
				string configPath = ConfigPath ("UserAgent");
				
				if (File.Exists (configPath) == false)
					return;
				
				using (TextReader reader = new StreamReader (new FileStream (configPath, FileMode.Open, FileAccess.Read))) {
					string pattern;
					while ((pattern = reader.ReadLine ()) != null) {
						string[] parts = pattern.Split (new char[] { ' ' }, 3);
						if (parts.Length != 3)
							continue;
						
						UserAgentRule rule = new UserAgentRule ();
						rule.Domain = parts[0];
						rule.Lang = parts[1];
						rule.UserAgent = parts[2];
						rule.Permanent = true;
						if (rule.UserAgent.ToLowerInvariant () == "random") {
							rule.Random = true;
							rule.UserAgent = RandomUserAgent ();
						}
						staticAgent.Add (rule.Domain, rule);
					}
				}
			} finally {
				listLock.ExitWriteLock ();
			}
		}

		void saveSettings ()
		{
			string configPath = ConfigPath ("UserAgent");
			
			TextWriter writer = null;
			try {
				listLock.EnterReadLock ();
				writer = new StreamWriter (new FileStream (configPath, FileMode.Create, FileAccess.Write));
				foreach (UserAgentRule rule in staticAgent.Values) {
					if (rule.Permanent == false)
						continue;
					if (rule.Random)
						writer.WriteLine (rule.Domain + " " + rule.Lang + " Random");
					else
						writer.WriteLine (rule.Domain + " " + rule.Lang + " " + rule.UserAgent);
				}
			} finally {
				listLock.ExitReadLock ();
				writer.NullSafeDispose ();
			}
		}

		public override bool Apply (Request request)
		{
			if (request.Response != null) {
				request.Response.Add ("X-PP-User-Agent: " + request.GetHeader ("User-Agent"));
				return false;
			}
			
			try {
				listLock.EnterReadLock ();
				
				if (staticAgent.ContainsKey (request.Uri.Host)) {
					UserAgentRule r = staticAgent[request.Uri.Host];
					
					if (r.UserAgent == "")
						request.RemoveHeader ("User-Agent"); else if (r.UserAgent.ToLowerInvariant () == "random")
						request.ReplaceHeader ("User-Agent", RandomUserAgent ()); else if (r.UserAgent.ToLowerInvariant () != "pass")
						request.ReplaceHeader ("User-Agent", r.UserAgent);
					
					if (r.Lang == "")
						request.RemoveHeader ("Accept-Language");
					else if (r.Lang.ToLowerInvariant () == "random")
						request.ReplaceHeader ("Accept-Language", GetRandom (lang));
					else if (r.Lang.ToLowerInvariant () != "pass")
						request.ReplaceHeader ("Accept-Language", r.Lang);
					
				} else {
					request.ReplaceHeader ("User-Agent", RandomUserAgent ());
					request.ReplaceHeader ("Accept-Language", GetRandom (lang));
				}
			} finally {
				listLock.TryExitReadLock ();
			}
			return true;
		}

		public override string Status (NameValueCollection httpGet, Request request)
		{
			string html = "<p>Replaces the User-Agent and Accept-Language headers with random ones</p>";
			html += "<p><strong>Your: </strong> " + Response.Html (request.GetHeader ("User-Agent")) + "</p>";
			html += "<p><strong>Random: </strong> " + Response.Html (RandomUserAgent ()) + "</p>";
			
			if (httpGet["delete"] != null) {
				try {
					listLock.EnterWriteLock ();
					staticAgent.Remove (httpGet["delete"]);
				} finally {
					listLock.TryExitWriteLock ();
				}
				
				saveSettings ();
			}
			
			if (httpGet["action"] != null) {
				UserAgentRule r = new UserAgentRule ();
				r.Domain = httpGet["domain"];
				r.Lang = httpGet["lang"];
				if (httpGet["action"] == "Permanent")
					r.Permanent = true;
				r.UserAgent = httpGet["agent"];
				if (r.UserAgent.ToLowerInvariant () == "random") {
					r.UserAgent = RandomUserAgent ();
					r.Random = true;
				}
				
				try {
					listLock.EnterWriteLock ();
					staticAgent.Add (r.Domain, r);
				} finally {
					listLock.TryExitWriteLock ();
				}
				
				if (r.Permanent)
					saveSettings ();
			}
			
			html += @"<form action=""?"" method=""get"">
								<p><label for=""domain"">Domain</label>: <input type=""text"" name=""domain"" value="""" /></p>
								<p><label for=""lang"">Language</label>: <input type=""text"" name=""lang"" value="""" /></p>
								<p><label for=""agent"">User-Agent</label>: <input type=""text"" name=""agent"" value="""" />
									""random"" = change every session.
									""pass"" = pass through unmodified</p>
								<input type=""submit"" name=""action"" value=""Permanent"" />
								<input type=""submit"" name=""action"" value=""Temporary"" />
							</form>";
			try {
				listLock.EnterReadLock ();
				
				foreach (UserAgentRule rule in staticAgent.Values) {
					html += "<p>" + rule + " <a href=\"?delete=" + rule.Domain + "\">delete</a></p>";
				}
			} finally {
				listLock.ExitReadLock ();
			}
			
			return html;
		}

		private class UserAgentRule
		{
			/// <summary>
			/// Domain to apply this rule
			/// </summary>
			public string Domain;
			/// <summary>
			/// User-Agent string
			/// </summary>
			public string UserAgent;
			/// <summary>
			/// User-Agent language
			/// </summary>
			public string Lang;
			/// <summary>
			/// Generate new random User-Agent at startup
			/// </summary>
			public bool Random = false;
			/// <summary>
			/// Store this rule to disk
			/// </summary>
			public bool Permanent = false;

			public override string ToString ()
			{
				string name = Domain + " => ";
				if (Random)
					name += "(random) ";
				if (Permanent)
					name += "(permanent) ";
				return name + this.Lang + ": " + this.UserAgent;
			}
		}
	}
}
