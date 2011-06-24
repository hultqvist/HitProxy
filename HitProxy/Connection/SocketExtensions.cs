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
				//Timeout = no data but connection
				if (socket.Poll (1, SelectMode.SelectRead) == false)
					return true;
				//No data = end of connection
				return socket.Available > 0;
			} catch (SocketException) {
				return false;
			} catch (ObjectDisposedException) {
				return false;
			}
		}
	}
}

