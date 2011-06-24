
using System;
using System.Collections.Specialized;
using System.IO;
using HitProxy.Http;

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

		public virtual Html Status (NameValueCollection httpGet, Request request)
		{
			return Status ();
		}

		public virtual Html Status ()
		{
			return Html.Format ("<p>(no description)</p>");
		}

		protected static string ConfigPath (string filterName)
		{
			string suffix = ".txt";
			if (filterName.Contains ("."))
				suffix = "";
			return Path.Combine (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), "HitProxy"), filterName + suffix);
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
