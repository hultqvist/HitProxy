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
		private Dictionary<string, IPAddress[]> dnsCache = new Dictionary<string, IPAddress[]> ();
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

		/// <summary>
		/// Get or create a new connection from cache.
		/// This call will hold until there is a connection available
		/// or return null on error, error message will be in request.Response
		/// </summary>
		public CachedConnection Connect (Uri uri)
		{
			//DNS lookup caching
			IPAddress[] 	dns = GetCachedDns (uri.Host);
			
			if (dns == null)
				throw new HeaderException ("Lookup of " + uri.Host + " failed", HttpStatusCode.BadGateway);
			
			return GetCachedConnection (dns, uri.Port, true, true);
		}

		/// <summary>
		/// Create a new connection - used for CONNECT where connections must be new.
		/// If the limit of simultaneous connections to a server is reached
		/// this call will hold until one is available.
		/// or return null on error, error message will be in request.Response
		/// </summary>
		public CachedConnection ConnectNew (Uri uri, bool hold)
		{
			//DNS lookup caching
			IPAddress[] dns = GetCachedDns (uri.Host);
			if (dns == null)
				throw new HeaderException ("Lookup of " + uri.Host + " failed", HttpStatusCode.BadGateway);
			
			CachedConnection connection = GetCachedConnection (dns, uri.Port, false, hold);
			
			return connection;
		}

		/// <summary>
		/// Get cached or create a new connection to specific ip-addresses.
		/// </summary>
		private CachedConnection GetCachedConnection (IPAddress[] ipaddress, int port, bool reuse, bool hold)
		{
			CachedConnection c = null;
			while (true) {
				//Search for cached connections, return if found.
				//Also calculates the server with the least number of connections to
				CachedServer leastUsedServer = null;
				lock (serverCache) {
					//Search for open connection and least used server
					foreach (IPAddress ip in ipaddress) {
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
						
						if (reuse == false)
							continue;
						
						c = server.GetActiveConnection ();
						if (c != null)
							return c;
					}
				}
				
				//No cached connection found, create one
				if (reuse)
					c = leastUsedServer.GetNewConnection ();
				else
					c = leastUsedServer.GetUnlimitedNewConnection ();
				if (c != null)
					return c;
				
				if (hold == false)
					return null;
				
				//Maximum number of connections to all servers were already reched.
				//Wait for new connections
				releasedConnection.WaitOne (TimeSpan.FromSeconds (5));
			}
		}

		/// <summary>
		/// Get a new dns cache from hostname
		/// </summary>
		private IPAddress[] GetCachedDns (string host)
		{
			lock (dnsCache) {
				if (dnsCache.ContainsKey (host))
					return dnsCache [host];
				else {
					IPAddress[] address = Dns.GetHostAddresses (host);
					
					if (address.Length == 0)
						return null;
					
					dnsCache.Add (host, address);
					
					return address;
				}
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
