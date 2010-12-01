
using System;
using System.Threading;
using System.Collections.Generic;

namespace HitProxy.Filters
{
	/// <summary>
	/// Runs all filters and return filtered status if any was filtered
	/// </summary>
	public class FilterList : Filter
	{
		private List<Filter> List = new List<Filter> ();
		private ReaderWriterLockSlim listLock = new ReaderWriterLockSlim ();
		
		public void Add (Filter filter)
		{
			try {
				listLock.EnterWriteLock ();
				List.Add (filter);
			}
			finally {
				listLock.TryExitWriteLock ();
			}
		}

		public void Remove (Filter filter)
		{
			try {
				listLock.EnterWriteLock ();
				List.Remove (filter);
			} finally {
				listLock.TryExitWriteLock ();
			}
		}

		public Filter[] ToArray ()
		{
			try {
				listLock.EnterReadLock ();
				return List.ToArray ();
			} finally {
				listLock.TryExitReadLock ();
			}
		}

		public override bool Apply (Request request)
		{
			bool filtered = false;
			foreach (Filter f in ToArray ()) {
				if (f.Apply (request) == true)
					filtered = true;
			}
			return filtered;
		}

		public override string ToString ()
		{
			return string.Format ("[FilterList]");
		}

		public override string Status ()
		{
			return "A list of filters that will be applied in order.";
		}
	}
}
