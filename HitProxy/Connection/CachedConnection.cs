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
		/// Usually a NetworkStream but can be a SslStream when HTTP CONNECT is intercepted.
		/// </summary>
		public Stream Stream;
		
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
			remoteSocket = new Socket (server.endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			remoteSocket.Connect (server.endpoint);
			this.Stream = new NetworkStream (remoteSocket);
		}

		public void Dispose ()
		{
			Stream.NullSafeDispose ();
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
			//Make sure there is no data left on the line
			if (remoteSocket.IsConnected () == false) {
				Dispose ();
			} else if (remoteSocket.Available > 0) {
				byte[] buffer = new byte[remoteSocket.Available];
				remoteSocket.Receive (buffer);
				string data = System.Text.Encoding.ASCII.GetString (buffer);
				Console.Error.WriteLine ("More data than meets the eye: " + buffer.Length + ": " + data);
				Dispose ();
			}

			//Release connection
			served += 1;
			busy = false;
			server.manager.releasedConnection.Set ();
		}

		public override string ToString ()
		{
			return string.Format ("[CachedConnection busy: {0}, served: {1}, avail: {2}, cacheServer: {3} ]", Busy, served, remoteSocket.Available, server);
		}
		
		public override int GetHashCode ()
		{
			return remoteSocket.GetHashCode ();
		}
	}
}
