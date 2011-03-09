using System;
using System.Net.Sockets;
namespace HitProxy.Connection
{
	/// <summary>
	/// The standard data output where data is passed through without any actions.
	/// </summary>
	public class SocketOutput : IDataOutput
	{
		private Socket socket;
		public SocketOutput (Socket socket)
		{
			this.socket = socket;
		}

		public void Send (byte[] buffer)
		{
			socket.SendAll (buffer);
		}

		public void Send (byte[] buffer, int length)
		{
			socket.SendAll (buffer, length);
		}
	}
}

