
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;

namespace HitProxy.Connection
{
	/// <summary>
	/// The connection to the remote server or local client.
	/// Handles reading of header and the following transfer of data.
	/// </summary>
	public class SocketData : IDisposable
	{
		private CachedConnection connection;
		private Socket remoteSocket;
		
		/// <summary>
		/// Data received from this connection
		/// </summary>
		public int Received { get; set; }
		
		/// <summary>
		/// For use by filter replacements
		/// </summary>
		protected SocketData ()
		{
			
		}
		
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
			this.remoteSocket = socket;
		}

		/// <summary>
		/// Release underlying resources so that they can be reused.
		/// </summary>
		public void Release ()
		{
			if (connection != null)
				connection.Release ();
			connection = null;
		}

		public virtual void Dispose ()
		{
			connection.NullSafeDispose ();
		}

		/// <summary>
		/// Send response data delivered in chunked format.
		/// Returns footer.
		/// </summary>
		public virtual string SendChunkedResponse (IDataOutput output)
		{
			while (true) {
				string header = remoteSocket.ReadChunkedHeader ();
				int length = int.Parse (header, System.Globalization.NumberStyles.HexNumber);
				
				byte[] cHeader = Encoding.ASCII.GetBytes (header);
				output.Send (cHeader);
				
				if (length == 0) {
					//Closing crnl
					string footer = remoteSocket.ReadHeader ();
					byte[] footerBytes = Encoding.ASCII.GetBytes (footer);
					output.Send (footerBytes);
					byte[] crnl = new byte[2] { 0xD, 0xA };
					output.Send (crnl);
					return footer;
				}
				
				PipeTo (output, length);
			}
		}

		/// <summary>
		/// Send all data from incoming socket to output.
		/// Buffer is assumed to be empty.
		/// </summary>
		public virtual void PipeTo (IDataOutput output)
		{
			byte[] buffer = new byte[0x10000];
			while (true) {
				remoteSocket.Poll (5000000, SelectMode.SelectRead);
				if (remoteSocket.IsConnected () == false)
					return;
				int read = remoteSocket.Receive (buffer);
				output.Send (buffer, read);
				Received += read;
			}
		}

		/// <summary>
		/// Send length bytes of data from incoming socket to output.
		/// Buffer is assumed to be empty.
		/// </summary>
		public virtual void PipeTo (IDataOutput output, long length)
		{
			if (length <= 0)
				return;
			
			byte[] buffer = new byte[0x10000];
			int totalRead = 0;
			while (true) {
				int toread = buffer.Length;
				if (totalRead + buffer.Length > length)
					toread = (int)length - totalRead;
				bool stat = remoteSocket.Poll (5000000, SelectMode.SelectRead);
				if (stat == false)
					continue;
				if (remoteSocket.IsConnected () == false)
					return;
				int read = remoteSocket.Receive (buffer, 0, toread, SocketFlags.None);
				if (read <= 0) {
					if (totalRead == length)
						return;
					continue;
				}
				output.Send (buffer, read);
				
				Received += read;
				totalRead += read;
				if (totalRead >= length)
					return;
			}
		}

		/// <summary>
		/// Invokes an async copying from input socket to output
		/// until the input end is closed.
		/// </summary>
		public ManualResetEvent PipeSocketAsync (Socket output)
		{
			SocketAsyncPipe pipe = new SocketAsyncPipe ();
			pipe.input = remoteSocket;
			pipe.output = output;
			pipe.done = new ManualResetEvent (false);
			
			SocketAsyncEventArgs e = new SocketAsyncEventArgs ();
			e.Completed += PipeSocketCallback;
			byte[] buffer = new byte[0x10000];
			e.SetBuffer (buffer, 0, buffer.Length);
			e.UserToken = pipe;
			try {
				if (pipe.input.ReceiveAsync (e) == false)
					PipeSocketCallback (null, e);
			} catch (ObjectDisposedException) {
				pipe.done.Set ();
			} catch (Exception ex) {
				Console.Error.WriteLine ("PipeSocketAsync: " + ex.Message);
				pipe.done.Set ();
			}
			return pipe.done;
		}

		private void PipeSocketCallback (object sender, SocketAsyncEventArgs e)
		{
			SocketAsyncPipe pipe = e.UserToken as SocketAsyncPipe;
			
			if (e.SocketError != SocketError.Success) {
				switch (e.SocketError) {
				case SocketError.Interrupted:
				case SocketError.ConnectionReset:
					break;
				default:
					Console.Error.WriteLine ("Pipe error: " + e.SocketError);
					break;
				}
				pipe.Close ();
				return;
			}
			if (pipe.output.IsConnected () == false) {
				//if (e.LastOperation == SocketAsyncOperation.Receive && e.BytesTransferred > 0)
				//	Console.Error.WriteLine ("Pipe error: " + pipe.output.RemoteEndPoint + " disconnected, got at least " + e.BytesTransferred + " more bytes to send");
				pipe.Close ();
				return;
			}
			
			//receive on false
			bool send;
			
			if (e.LastOperation == SocketAsyncOperation.Receive) {
				if (e.BytesTransferred == 0) {
					e.SetBuffer (0, e.Buffer.Length);
					send = false;
					
					try {
						if (pipe.input.Poll (5000000, SelectMode.SelectRead) == true) {
							if (pipe.input.Available == 0) {
								//Connection closed
								pipe.done.Set ();
								return;
							}
						}
					} catch (ObjectDisposedException) {
						pipe.done.Set ();
						return;
					}
				} else {
					Received += e.BytesTransferred;
					e.SetBuffer (0, e.BytesTransferred);
					send = true;
				}
			} else if (e.LastOperation == SocketAsyncOperation.Send) {
				if (e.BytesTransferred < e.Count) {
					e.SetBuffer (e.Offset + e.BytesTransferred, e.Count - e.BytesTransferred);
					send = true;
				} else {
					e.SetBuffer (0, e.Buffer.Length);
					send = false;
				}
			} else {
				Console.Error.WriteLine ("PipeSocketCallback, unhandled operation: " + e.LastOperation);
				pipe.done.Set ();
				return;
			}
			
			try {
				if (send) {
					if (pipe.output.SendAsync (e) == false)
						PipeSocketCallback (null, e);
				} else {
					if (pipe.input.ReceiveAsync (e) == false)
						PipeSocketCallback (null, e);
				}
			} catch (ObjectDisposedException) {
				pipe.Close ();
			} catch (Exception ex) {
				Console.Error.WriteLine ("PipeSocketAsync Exception: " + ex.Message);
				pipe.Close ();
			}
		}

		private class SocketAsyncPipe
		{
			public Socket input;
			public Socket output;
			public ManualResetEvent done;

			public void Close ()
			{
				input.Close ();
				output.Close ();
				done.Set ();
			}
		}
	}
}
