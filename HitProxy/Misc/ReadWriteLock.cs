using System;
using System.Threading;

namespace HitProxy
{
	/// <summary>
	/// Wrapper for ReaderWriterLockSlim
	/// with help functions to be used with using(obj.Read){}
	/// </summary>
	public class ReadWriteLock
	{
		readonly ReaderWriterLockSlim l = new ReaderWriterLockSlim ();

		class UsingReadLock : IDisposable
		{
			readonly ReaderWriterLockSlim parent;

			public UsingReadLock (ReaderWriterLockSlim l)
			{
				parent = l;
				l.EnterReadLock ();
			}
			
			public void Dispose ()
			{
				parent.ExitReadLock ();
			}
			
		}
		
		class UsingUpgradeableReadLock : IDisposable
		{
			readonly ReaderWriterLockSlim parent;

			public UsingUpgradeableReadLock (ReaderWriterLockSlim l)
			{
				parent = l;
				l.EnterUpgradeableReadLock ();
			}
			
			public void Dispose ()
			{
				if (parent.IsWriteLockHeld)
					parent.ExitWriteLock ();
				parent.ExitUpgradeableReadLock ();
			}
			
		}
		
		class UsingWriteLock : IDisposable
		{
			readonly ReaderWriterLockSlim parent;

			public UsingWriteLock (ReaderWriterLockSlim l)
			{
				parent = l;
				l.EnterWriteLock ();
			}
			
			public void Dispose ()
			{
				parent.ExitWriteLock ();
			}
			
		}
		
		/// <summary>
		/// Enter a read lock state and return an object.
		/// The lock will be released once the returned object is disposed.
		/// </summary>
		public IDisposable Read {
			get { 
				return new UsingReadLock (l);
			}
		}
		
		/// <summary>
		/// Enter a write lock state and return an object.
		/// The lock will be released once the returned object is disposed.
		/// </summary>
		public IDisposable Write {
			get { 
				return new UsingWriteLock (l);
			}
		}
		
		public IDisposable UpgradeableRead {
			get {
				return new UsingUpgradeableReadLock (l);
			}
		}
	}
	
	
}

