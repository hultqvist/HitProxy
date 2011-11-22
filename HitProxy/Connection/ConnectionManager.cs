using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using HitProxy.Http;

namespace HitProxy.Connection
{

	/// <summary>
	/// Caches active connections and limits the number of
	/// simultaneous connections to each server
	/// </summary>
	public class ConnectionManager
	{
		private Dictionary<IPEndPoint, CachedServer> serverCache = new Dictionary<IPEndPoint, CachedServer> ();
		/// <summary>
		/// This event is triggered to let pending connections start.
		/// </summary>
		public AutoResetEvent releasedConnection = new AutoResetEvent (false);

		public CachedServer[] ServerArray {
			get {
				lock (serverCache) {
					CachedServer[] list = new CachedServer[serverCache.Values.Count];
					int n = 0;
					foreach (CachedServer s in serverCache.Values) {
						list [n] = s;
						n++;
					}
					return list;
				}
			}
		}

		public ConnectionManager ()
		{
		}
		
		/// <summary>
		/// Get cached or create a new connection to specific ip-addresses.
		/// </summary>
		/// <param name='dns'>
		/// Dns.
		/// </param>
		/// <param name='port'>
		/// Port.
		/// </param>
		/// <param name='forceNew'>
		/// Don't reuse, always start a new connection
		/// </param>
		/// <param name='wait'>
		/// Wait until there is a slot free, otherwise it will return null.
		/// </param>
		public CachedConnection Connect (DnsLookup dns, int port, bool forceNew, bool wait)
		{
			CachedConnection c = null;
			while (true) {
				//Search for cached connections, return if found.
				//Also calculates the server with the least number of connections to
				CachedServer leastUsedServer = null;
				lock (serverCache) {
					//Search for open connection and least used server
					foreach (IPAddress ip in dns.AList) {
						IPEndPoint ep = new IPEndPoint (ip, port);
						CachedServer server;
						if (serverCache.TryGetValue (ep, out server) == false) {
							server = new CachedServer (ep, this);
							serverCache.Add (ep, server);
						}
						
						//Test for least used server
						if (leastUsedServer == null) {
							leastUsedServer = server;
						}
						if (server.ConnectionCount < leastUsedServer.ConnectionCount)
							leastUsedServer = server;
						
						if (forceNew == false)
							continue;
						
						c = server.GetActiveConnection ();
						if (c != null)
							return c;
					}
				}
				
				//No cached connection found, create one
				if (forceNew)
					c = leastUsedServer.GetNewConnection ();
				else
					c = leastUsedServer.GetUnlimitedNewConnection ();
				if (c != null)
					return c;
				
				if (wait == false)
					return null;
				
				//Maximum number of connections to all servers were already reached.
				//Wait for new connections
				releasedConnection.WaitOne (TimeSpan.FromSeconds (5));
			}
		}

		public void Cleanup ()
		{
			lock (serverCache) {
				foreach (KeyValuePair<IPEndPoint, CachedServer> kvp in serverCache)
					kvp.Value.Cleanup ();
			}
		}
	}
}
