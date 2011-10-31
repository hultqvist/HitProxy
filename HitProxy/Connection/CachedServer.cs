
using System;
using System.Net;
using System.Collections.Generic;

namespace HitProxy.Connection
{
	/// <summary>
	/// Holds connection cache for a server on one IP address
	/// </summary>
	public class CachedServer
	{
		public IPEndPoint endpoint;
		/// <summary>
		/// Maximum number of simultaneous connections
		/// </summary>
		int max = 4;
		private List<CachedConnection> connections = new List<CachedConnection> ();
		public ConnectionManager manager;

		public CachedServer (IPEndPoint endpoint, ConnectionManager manager)
		{
			this.endpoint = endpoint;
			this.manager = manager;
		}

		public CachedConnection[] Connections {
			get {
				lock (connections) {
					return connections.ToArray ();
				}
			}
		}

		public int ConnectionCount {
			get {
				lock (connections) {
					return connections.Count;
				}
			}
		}

		/// <summary>
		/// If available will return an unused
		/// connection that already is connected.
		/// Return null otherwise.
		/// </summary>
		/// <returns>
		/// Null if no active connections are available.
		/// </returns>
		public CachedConnection GetActiveConnection ()
		{
			lock (connections) {
				foreach (CachedConnection c in connections.ToArray ()) {
					if (c.Busy)
						continue;
					
					if (c.remoteSocket.IsConnected () == false) {
						connections.Remove (c);
						continue;
					}
					
					c.SetBusy ();
					return c;
				}
				
				return null;
			}
		}

		/// <summary>
		/// Creates and return a new connection.
		/// If the maximum number of connections to that server is reached,
		/// it will return null.
		/// </summary>
		public CachedConnection GetNewConnection ()
		{
			CachedConnection c = new CachedConnection (this);
			lock (connections) {
				if (connections.Count >= max) {
					return null;
				}
				connections.Add (c);
			}
			c.Connect ();
			return c;
		}

		public CachedConnection GetUnlimitedNewConnection ()
		{
			CachedConnection c = new CachedConnection (this);
			lock (connections) {
				connections.Add (c);
			}
			c.Connect ();
			return c;
		}

		/// <summary>
		/// Remove a connection from the ServerCache
		/// This will trigger an event allowing pending connections to start.
		/// </summary>
		/// <param name="connection">
		/// The connection to remove
		/// </param>
		public void Remove (CachedConnection connection)
		{
			lock (connections) {
				connections.Remove (connection);
			}
			manager.releasedConnection.Set ();
		}

		public override string ToString ()
		{
			return string.Format ("[CachedServer: Connections={0}, Endpoint={1}]", Connections, endpoint);
		}

		public void Cleanup ()
		{
			foreach (CachedConnection c in Connections) {
				if (c.remoteSocket == null)
					continue;
				if (c.remoteSocket.IsConnected () == false)
					c.Dispose ();
			}
		}
		
	}
}
