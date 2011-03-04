
using System;
using System.Net.Sockets;
using System.IO;

namespace HitProxy.Connection
{

	public class CachedConnection : IDisposable
	{
		/// <summary>
		/// the connection is currently being used
		/// </summary>
		public bool Busy {
			get { return busy; }
		}
		private bool busy = true;
		public Socket remoteSocket;
		
		/// <summary>
		/// Statistics: Number of requests served using this connection
		/// </summary>
		public int served = 0;

		//Backlink
		public CachedServer server;

		public CachedConnection (CachedServer server)
		{
			this.server = server;
		}
		public void Connect ()
		{
			TcpClient connection = new TcpClient ();
			connection.Connect (server.endpoint);
			remoteSocket = connection.Client;
		}

		public void Dispose ()
		{
			remoteSocket.Close ();
			server.Remove (this);
		}

		/// <summary>
		/// Indicates that this connections now is busy serving a request
		/// </summary>
		public void SetBusy ()
		{
			busy = true;
		}

		/// <summary>
		/// Make connection available to other requests
		/// </summary>
		public void Release ()
		{
			served += 1;
			busy = false;
			
			server.manager.releasedConnection.Set ();
			
			if (remoteSocket.IsConnected () == false) {
				Dispose ();
			} else if (remoteSocket.Available > 0) {
				byte[] buffer = new byte[remoteSocket.Available];
				remoteSocket.Receive (buffer);
				string data = System.Text.Encoding.ASCII.GetString (buffer);
				Console.Error.WriteLine ("More data than meets the eye: " + data);
				Dispose ();
			}
		}

		public override string ToString ()
		{
			return string.Format ("[CachedConnection busy: {0}, served: {1}, avail: {2}, cacheServer: {3} ]", Busy, served, remoteSocket.Available, server);
		}
	}
}
