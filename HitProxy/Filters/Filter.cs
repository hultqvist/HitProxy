
using System;
using System.Collections.Specialized;
using System.IO;

namespace HitProxy.Filters
{
	/// <summary>
	/// Return true on filtered/modified and false otherwise
	/// </summary>
	public abstract class Filter
	{
		/// <summary>
		/// Can be used to filter both requests and responses
		/// For response filtering the request.Response is set
		/// </summary>
		/// <returns>True if some filter was applied</returns>
		public abstract bool Apply (Request request);

		public virtual string Status (NameValueCollection httpGet, Request request)
		{
			return Status ();
		}

		public virtual string Status ()
		{
			return "No status for filter";
		}
		
		protected static string ConfigPath (string filterName)
		{
			return Path.Combine (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), "HitProxy"), filterName + ".txt");
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
