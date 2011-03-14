using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using HitProxy.Http;

namespace HitProxy.Connection
{
	public static class SocketExtensions
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
	}
}

