
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace HitProxy
{


	public static class Extensions
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="timeout">
		/// Timeout in microseconds
		/// </param>
		public static bool IsConnected (this Socket socket)
		{
			try {
				bool read = socket.Poll (1, SelectMode.SelectRead);
				bool avail = socket.Available == 0;
				return !(read && avail);
			} catch (SocketException) {
				return false;
			} catch (ObjectDisposedException) {
				return false;
			}
		}

		/// <summary>
		/// Send all bytes in buffer even when Socket.Send would return, this retries untill all bytes are sent.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="length"></param>
		public static void SendAll (this Socket socket, byte[] buffer, int length)
		{
			int sent = 0;
			while (sent < length) {
				int delta = socket.Send (buffer, sent, length - sent, SocketFlags.None);
				if (delta < 0)
					throw new InvalidOperationException ("Send less than zero bytes");
				sent += delta;
			}
			if (sent > length)
				throw new InvalidOperationException ("Sent more data than received");
		}
		/// <summary>
		/// Read headers in a chunked encoding
		/// Return a string with the chunk header
		/// </summary>
		public static string ReadChunkedHeader (this Socket socket)
		{
			byte[] header = new byte[30];
			int index = 0;
			while (true) {
				if (index >= header.Length)
					throw new HeaderException ("Chunked header is too large", HttpStatusCode.BadGateway);
				
				//Read one byte
				socket.Poll (60000000, SelectMode.SelectRead);
				int b = socket.Receive (header, index, 1, SocketFlags.None);
				if (b != 1) {
					if (socket.IsConnected () == false)
						throw new HeaderException ("Socket closed during read of chunk header", HttpStatusCode.BadGateway);
					else
						continue;
				}
				
				//Skip leading space and crlf
				if (index == 0) {
					if (header[index] == 0x20)
						continue;
				}
				
				index++;
				
				if (index > 2 && header[index - 1] == 0xa) {
					return Encoding.ASCII.GetString (header, 0, index);
				}
			}
		}

		/// <summary>
		/// Return the http headers read from the socket in a single string.
		/// This data can be sent directly to Header.Parse(string).
		/// </summary>
		/// <returns>
		/// Complete http headers.
		/// </returns>
		public static string ReadHeader (this Socket socket)
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
				
				b = header[index];
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
				if (header[index - 2] == 0xd)
					index -= 2;
				else
					index -= 1;
				
				return Encoding.ASCII.GetString (header, 0, index);
			}
		}


		public static void NullSafeDispose (this IDisposable dis)
		{
			if (dis == null)
				return;
			
			dis.Dispose ();
		}

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
