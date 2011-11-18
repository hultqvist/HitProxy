using System;
using System.Collections.Specialized;
using System.IO;
using HitProxy.Http;
using HitProxy.Filters;

namespace HitProxy.Http
{
	/// <summary>
	/// Return true on filtered/modified and false otherwise
	/// </summary>
	public abstract class Filter
	{
		/// <summary>
		/// Filter Codename
		/// </summary>
		public string Name {
			get { return this.GetType ().Name; }
		}

		public bool Active { get; set; }

		/// <summary>
		/// Can be used to filter both requests and responses
		/// For response filtering the request.Response is set
		/// </summary>
		/// <returns>True if some filter was applied</returns>
		public abstract bool Apply (Request request);

		public virtual Response Status (NameValueCollection httpGet, Request request)
		{
			return WebUI.ResponseTemplate (ToString (), Status ());
		}

		public virtual Html Status ()
		{
			
			return Html.Format ("<p>(no description)</p>");
		}

		protected string ConfigPath (string filename)
		{
			if (filename.Contains (".") == false)
				filename += ".txt";
			return Path.Combine (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), "HitProxy"), filename);
		}

		protected string ConfigPath ()
		{
			return Path.Combine (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), "HitProxy"), Name + ".settings");
		}

		public override string ToString ()
		{
			return GetType ().Name;
		}
	}

	public static class FilterLoader
	{
		public static Filter FromString (string name)
		{
			try {
				Type t = Type.GetType ("HitProxy.Filters." + name);
				object o = Activator.CreateInstance (t);
				return o as Filter;
			} catch {
				return null;
			}
		}
	}
}
