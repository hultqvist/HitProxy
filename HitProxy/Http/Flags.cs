using System;
using System.Collections.Generic;
namespace HitProxy.Http
{
	public class Flags
	{
		/// <summary>
		/// Attributes set by triggers and used by filters.
		/// </summary>
		private readonly List<string> flags = new List<string> ();

		public Flags ()
		{
		}

		public Flags (List<string> flagStorage)
		{
			this.flags = flagStorage;
		}

		public Flags (string flags)
		{
			Set (flags);
		}

		public bool this[string flag] {
			get {
				flag = flag.ToLowerInvariant ();
				return flags.Contains (flag);
			}
			set {
				flag = flag.ToLowerInvariant ().Trim ();
				if (flag == "")
					throw new ArgumentException ("Flag cannot be empty string.");
				if (flag.Contains (","))
					throw new ArgumentException ("Only one flag allowed, no comma");
				if (flags.Contains (flag) == value)
					return;
				if (value)
					flags.Remove (flag);
				else
					flags.Add (flag);
			}
		}


		/// <summary>
		/// Add flags from a comma separated list
		/// </summary>
		/// <param name="flagNames">
		/// A comma separated list of filtering classification names
		/// </param>
		public void Set (string flagNames)
		{
			string[] fa = flagNames.ToLowerInvariant ().Split (',');
			foreach (string f in fa) {
				string fl = f.Trim ();
				if (fl == "")
					throw new ArgumentException ("Flag cannot be empty string.");
				if (flags.Contains (fl) == false)
					flags.Add (fl);
			}
		}

		public void Set (Flags other)
		{
			foreach (string f in other.flags) {
				if (flags.Contains (f) == false)
					flags.Add (f);
			}
		}

		public bool Any (string flags)
		{
			string[] fa = flags.ToLowerInvariant ().Split (',');
			foreach (string f in fa) {
				if (flags.Contains (f))
					return true;
			}
			return false;
		}

		public bool All (string flags)
		{
			string[] fa = flags.ToLowerInvariant ().Split (',');
			foreach (string f in fa) {
				if (flags.Contains (f) == false)
					return false;
			}
			return true;
		}

		/// <summary>
		/// Return string to save and use in Flags constructor.
		/// </summary>
		public string Serialize ()
		{
			string text = null;
			foreach (string f in flags) {
				if (text == null)
					text = f;
				else
					text = "," + f;
			}
			return text;
		}
		
		public override string ToString ()
		{
			return Serialize ();
		}
	}
}

