using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using HitProxy.Http;

namespace HitProxy.Connection
{
	/// <summary>
	/// The connection to the remote server or local client.
	/// Handles reading of header and the following transfer of data.
	/// </summary>
	public class SocketData : IDataIO
	{
		/// <summary>
		/// Remote connections
		/// </summary>
		private CachedConnection connection;
		/// <summary>
		/// Client socket
		/// </summary>
		private Socket socket;

		/// <summary>
		/// Data received from this session
		/// </summary>
		public int Received { get; set; }

		/// <summary>
		/// From a remote connection with allocated buffer.
		/// </summary>
		public SocketData (CachedConnection connection) : this(connection.remoteSocket)
		{
			this.connection = connection;
		}

		/// <summary>
		/// For incoming sockets
		/// </summary>
		public SocketData (Socket socket)
		{
			this.socket = socket;
		}

		/// <summary>
		/// Release underlying resources so that they can be reused.
		/// </summary>
		public void ReleaseRemoteConnection ()
		{
			if (connection != null)
				connection.Release ();
			connection = null;
		}

		public void Dispose ()
		{
			connection.NullSafeDispose ();
		}

		/// <summary>
		/// Closes the client session.
		/// Currently only used in the end of HTTP Connect requests
		/// </summary>
		public void CloseClientSocket ()
		{
			socket.Close ();
		}

		#region IDataInput

		/// <summary>
		/// Return the http headers read from the socket in a single string.
		/// This data can be sent directly to Header.Parse(string).
		/// </summary>
		/// <returns>
		/// Complete http headers.
		/// </returns>
		public string ReadHeader ()
		{
			byte[] header = new byte[16 * 1024];
			int index = 0;
			int nlcount = 0;
			byte b;
			
			while (true) {
				while (false == socket.Poll (5 * 1000000, SelectMode.SelectRead)) {
				}
				if (socket.Available == 0)
					throw new HeaderException ("Connection closed", HttpStatusCode.BadGateway);
				int received = socket.Receive (header, index, 1, SocketFlags.None);
				if (received != 1)
					throw new HeaderException ("ReadHeader: did not get data", HttpStatusCode.BadGateway);
				
				b = header [index];
				index += 1;
				
				if (index >= header.Length)
					throw new HeaderException ("Header too large, limit is at " + header.Length, HttpStatusCode.RequestEntityTooLarge);
				
				if (b != 0xa) {
					if (b != 0xd)
						nlcount = 0;
					continue;
				}
				//Test for end of header
				nlcount += 1;
				if (nlcount < 2 && !(nlcount == 1 && index <= 2))
					continue;
				
				//Remove last empty line
				if (header [index - 2] == 0xd)
					index -= 2;
				else
					index -= 1;
				
				return Encoding.ASCII.GetString (header, 0, index);
			}
		}

		public void Receive (byte[] buffer, int start, int length)
		{
			int read = 0;
			while (read < length) {
				int rcvd = socket.Receive (buffer, start + read, length - read, SocketFlags.None);
				read += rcvd;
			}
		}

		/// <summary>
		/// Send all data from incoming socket to output.
		/// Buffer is assumed to be empty.
		/// </summary>
		public int PipeTo (IDataOutput output)
		{
			int total = 0;
			byte[] buffer = new byte[0x10000];
			while (true) {
				int read = socket.Receive (buffer);
				if (read == 0) {
					output.EndOfData ();
					return total;
				}
				output.Send (buffer, 0, read);
				Received += read;
				total += read;
			}
		}

		/// <summary>
		/// Send length bytes of data from incoming socket to output.
		/// Buffer is assumed to be empty.
		/// </summary>
		public void PipeTo (IDataOutput output, long length)
		{
			if (length <= 0)
				return;
			
			byte[] buffer = new byte[0x10000];
			int totalRead = 0;
			while (true) {
				int toread = buffer.Length;
				if (totalRead + buffer.Length > length)
					toread = (int)length - totalRead;
				int read = socket.Receive (buffer, 0, toread, SocketFlags.None);
				if (read == 0)
					throw new SocketException ((int)SocketError.ConnectionReset);
				
				output.Send (buffer, 0, read);
				
				Received += read;
				totalRead += read;
				if (totalRead >= length) {
					output.EndOfData ();
					return;
				}
			}
		}

		#endregion

		#region IDataOutput

		public void Send (byte[] buffer, int start, int length)
		{
			int sent = 0;
			while (true) {
				int delta = socket.Send (buffer, start + sent, length - sent, SocketFlags.None);
				if (delta < 0)
					throw new InvalidOperationException ("Send less than zero bytes");
				sent += delta;
				if (sent == length)
					return;
				if (sent < length)
					continue;
				if (sent > length)
					throw new InvalidOperationException ("Sent more data than received");
			}
		}

		public void EndOfData ()
		{
			
		}

		#endregion

		public override bool Equals (object obj)
		{
			SocketData o = obj as SocketData;
			if (o == null)
				return false;
			return (o.GetHashCode () == GetHashCode ());
		}

		public override int GetHashCode ()
		{
			if (socket != null)
				return socket.GetHashCode ();
			return connection.GetHashCode ();
		}
	}
}
