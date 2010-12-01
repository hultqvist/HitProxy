using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using HitProxy;
using System.Net;

namespace HitProxy.Filters
{
	/// <summary>
	/// Filter requests using an adblock list
	/// </summary>
	public class AdBlock : Filter
	{
		/// <summary>
		/// Design from AdBlock Plus addon for firefox:
		/// First string is a continous string within the pattern without any wildcards.
		/// Second follows a list of all blocking regex.
		/// </summary>
		Dictionary<string, List<Regex>> blockList = new Dictionary<string, List<Regex>> ();
		List<Regex> regexList = new List<Regex> ();

		//Further ideas are to have a prioritized list where every regex is
		//given a point for every match, higher point get the regex earlier
		//on the list. This must be on a per regex basis not the prestring match

		//Hall of shame
		//Queue<string> hallOfShame = new Queue<string>(20);

		readonly char[] wildcards;

		private readonly string configPath;

		public AdBlock ()
		{
			wildcards = new char[] { '?', '*', '^' };
			blockList.Add ("", new List<Regex> ());
			
			configPath = ConfigPath ("AdBlock.txt");
			
			if (File.Exists (configPath) == false)
				return;
			
			using (TextReader reader = new StreamReader (new FileStream (configPath, FileMode.Open, FileAccess.Read))) {
				string pattern = reader.ReadLine ();
				//First line ignored, [AdBlock header
				while ((pattern = reader.ReadLine ()) != null) {
					
					//Filter out headers and pattern extras
					if (pattern.Trim () == "")
						continue;
					//Comments
					if (pattern.StartsWith ("!"))
						continue;
					//Whitelist, not implemented
					if (pattern.StartsWith ("@@"))
						continue;
					//Html element filters, proxy not designed to be able to implement
					if (pattern.Contains ("##"))
						continue;
					//Beginning of
					if (pattern.StartsWith ("|")) {
						//Beginning of domain
						if (pattern.StartsWith ("||")) {
							if (pattern.EndsWith ("^"))
								pattern = pattern.Substring (2, pattern.Length - 3);
							else
								pattern = pattern.Substring (2, pattern.Length - 2);
							
							pattern = "*" + pattern + "*";
						} else
							//Beginning of address
							pattern = pattern.Substring (1) + "*";
					} else
						
						pattern = "*" + pattern;
					
					//Third party, not implemented
					if (pattern.EndsWith ("$third-party"))
						continue;
					
					//Get continous strings from pattern
					string[] parts = pattern.Split (wildcards);
					
					//Wildcard to RegEx
					Regex regex = new Regex ("^" + Regex.Escape (pattern).Replace ("\\*", ".*").Replace ("\\?", ".") + "$");
					regexList.Add (regex);
					
					bool added = false;
					foreach (string part in parts) {
						if (part.Length == 0)
							continue;
						
						if (part.Length > 8) {
							AddFilter (part, regex);
							added = true;
						}
					}
					if (added == false)
						blockList[""].Add (regex);
				}
			}
		}

		private void AddFilter (string part, Regex regex)
		{
			try {
				blockList[part].Add (regex);
			} catch (KeyNotFoundException) {
				List<Regex> list = new List<Regex> ();
				list.Add (regex);
				blockList.Add (part, list);
			}
		}

		public override bool Apply (Request request)
		{
			Uri u = request.Uri;
			string url = u.Host + u.PathAndQuery;
			if (u.Scheme == "connect")
				url = "https://" + url;
			else
				url = u.Scheme + "://" + url;
			
			foreach (KeyValuePair<string, List<Regex>> kvp in blockList) {
				if (kvp.Key != "" && url.Contains (kvp.Key) == false)
					continue;
				
				foreach (Regex regex in kvp.Value) {
					if (regex.IsMatch (url) == false)
						continue;
					
					//hallOfShame.Enqueue (url);
					request.Block ("Adblock filter: " + regex.ToString () + "\n" + url);
					request.Response.Add ("X-AdBlock: BLOCKED: " + regex.ToString ());
					return true;
				}
			}
			
			return false;
		}

		public override string ToString ()
		{
			return string.Format ("[AdBlock]");
		}

		public override string Status ()
		{
			string text = "";
			
			text += "<h1>Filters</h1>";
			text += "<p>From: " + configPath + "</p>";
			
			foreach (Regex regex in regexList)
				text += "<p>" + regex + "</p>";
			
			//text += "<h1>Blocked requests</h1>";
			
			//foreach (string blocked in hallOfShame)
			//	text += "<p>" + blocked + "</p>";
			
			return text;
		}
	}
}
