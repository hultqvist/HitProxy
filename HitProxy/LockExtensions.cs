using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
namespace HitProxy
{
	public static class LockExtensions
	{
		/// <summary>
		/// Exit read lock if held.
		/// </summary>
		public static void TryExitReadLock (this ReaderWriterLockSlim rwLock)
		{
			if (rwLock.IsReadLockHeld)
				rwLock.ExitReadLock ();
		}

		/// <summary>
		/// Exit upgradeable lock if held
		/// </summary>
		public static void TryExitUpgradeableReadLock (this ReaderWriterLockSlim rwLock)
		{
			if (rwLock.IsUpgradeableReadLockHeld)
				rwLock.ExitUpgradeableReadLock ();
		}

		/// <summary>
		/// Exit write lock if held
		/// </summary>
		public static void TryExitWriteLock (this ReaderWriterLockSlim rwLock)
		{
			if (rwLock.IsWriteLockHeld)
				rwLock.ExitWriteLock ();
		}
	}
}
