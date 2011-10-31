using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using HitProxy.Connection;
using HitProxy.Http;

namespace HitProxy.Session
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
			request.Response = new Response (remote);
			request.Response.HttpVersion = "HTTP/1.1";
			request.Response.HttpCode = HttpStatusCode.OK;
			request.Response.HTTPMessage = HttpStatusCode.OK.ToString ();
			request.Response.KeepAlive = false;
			request.Response.Add ("Proxy-Agent: HitProxy");
			request.Response.SendHeaders (this.ClientStream);
			
			Thread t = new Thread (() => {
				try {
					request.Stream.PipeTo (request.Response.Stream);
				} catch (Exception) {
					remote.Dispose ();
				} finally {
				}
			});
			t.Name = Thread.CurrentThread.Name + "ConnectOutput";
			t.Start ();
			Status = "Connected";
			try {
				request.Response.Stream.PipeTo (request.Stream);				
			} catch (Exception e) {
				Console.WriteLine (this + " " + e.GetType ().Name + " :" + e.Message);
				request.Stream.NullSafeDispose ();
			} finally {
				remote.Dispose ();
				ClientStream.NullSafeDispose ();
			}
			t.Join ();
			Status = "Connection closed";
			
			request.Response.Dispose ();
			request.Response = null;
		}
	}
}
