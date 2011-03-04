using System;
using System.Threading;
using System.Collections.Generic;
using HitProxy;
using HitProxy.Http;

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
				filter.Parent = this;
			} finally {
				listLock.TryExitWriteLock ();
			}
		}

		public void Remove (Filter filter)
		{
			try {
				listLock.EnterWriteLock ();
				List.Remove (filter);
				filter.Parent = null;
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

		public override string Status ()
		{
			return "<p>A list of filters that will be applied in order.</p>";
		}
	}
}
