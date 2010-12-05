
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
	public class ConnectProxy
	{
		ProxySession proxysession;
		ConnectionManager connectionManager;

		public ConnectProxy (ProxySession session, ConnectionManager connectionManager)
		{
			this.proxysession = session;
			this.connectionManager = connectionManager;
		}

		string Status {
			set { proxysession.Status = value; }
		}

		/// <summary>
		/// Read and execute request.
		/// Return when connection is closed
		/// </summary>
		/// <param name="request">
		/// A <see cref="Request"/>
		/// </param>
		public void ProcessRequest (Request request)
		{
			CachedConnection remote = null;
			try {
				Status = "Connecting to " + request.Uri.Host;
				remote = connectionManager.ConnectNew (request, false);
				if (remote == null) {
					request.Response = new Response (HttpStatusCode.ServiceUnavailable, "Maximum Connections Reached", "Maximum number of simultaneous connections reached");
					return;
				}
				Status = "Connected";
			} catch (SocketException e) {
				request.Response = new Response (HttpStatusCode.ServiceUnavailable, "Connection Error", e.Message);
				return;
			}
			
			request.Response = new Response (HttpStatusCode.OK);
			request.Response.KeepAlive = false;
			request.Response.Add ("Proxy-Agent: HitProxy");
			request.Response.SendHeaders (proxysession.clientSocket);
			request.Response.DataSocket = new SocketData (remote);
			
			//Pass data in both directions
			ManualResetEvent doneReq = request.DataSocket.PipeSocketAsync (remote.remoteSocket);
			ManualResetEvent doneRes = request.Response.DataSocket.PipeSocketAsync (proxysession.clientSocket);
			
			//Wait until any side disconnects
			doneReq.WaitOne ();
			doneRes.WaitOne ();
			Status = "Connection closed";
			
			request.Response.Dispose ();
			request.Response = null;
		}

		public ConnectProxy ()
		{
		}
	}
}
