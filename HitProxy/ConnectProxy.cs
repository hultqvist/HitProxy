
using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace HitProxy
{
	/// <summary>
	/// Proxy implementation for HTTP CONNECT
	/// </summary>
	public partial class ProxySession
	{
		/// <summary>
		/// Read and execute request.
		/// Return when connection is closed
		/// </summary>
		/// <param name="request">
		/// A <see cref="Request"/>
		/// </param>
		public void ProcessHttpConnect (CachedConnection remote)
		{			
			request.Response = new Response (HttpStatusCode.OK);
			request.Response.KeepAlive = false;
			request.Response.Add ("Proxy-Agent: HitProxy");
			request.Response.SendHeaders (clientSocket);
			request.Response.DataSocket = new SocketData (remote);
			
			//Pass data in both directions
			ManualResetEvent doneReq = request.DataSocket.PipeSocketAsync (remote.remoteSocket);
			ManualResetEvent doneRes = request.Response.DataSocket.PipeSocketAsync (clientSocket);
			
			//Wait until any side disconnects
			doneReq.WaitOne ();
			doneRes.WaitOne ();
			Status = "Connection closed";
			
			request.Response.Dispose ();
			request.Response = null;
		}
	}
}
