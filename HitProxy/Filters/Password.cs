
using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// Listens for passwords
	/// - Warn about unencrypted password transfers
	/// - Log password
	/// - Store password for autologin
	/// </summary>
	public class Password : Filter
	{
		public Password ()
		{
		}

		public override bool Apply (Request request)
		{
			throw new System.NotImplementedException ();
		}
		
	}
}
