using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using HitProxy.Http;
using HitProxy.Connection;
using HitProxy.Filters;

namespace HitProxy.Session
{
	public partial class ProxySession : IDisposable
	{
		/// <summary>
		/// Flag to signal that the thread should stop
		/// </summary>
		private bool active = true;
		public Socket clientSocket;
		Thread thread;
		ConnectionManager connectionManager;
		Proxy proxy;

		/// <summary>
		/// Current request being served
		/// </summary>
		public Request request;
		string name;
		public int served = 0;
		public string Status = "Initialized";

		/// <summary>
		/// Start a new session thread for the incoming client.
		/// </summary>
		public ProxySession (Socket socket, Proxy proxy, ConnectionManager connectionManager)
		{
			this.clientSocket = socket;
			this.proxy = proxy;
			this.connectionManager = connectionManager;
			thread = new Thread (Run);
			
			name = "[Session " + (socket.RemoteEndPoint as IPEndPoint).Port + "]";
			thread.Name = name;
		}

		public void Dispose ()
		{
			proxy.Remove (this);
			request.NullSafeDispose ();
			clientSocket.Close ();
		}

		public void Start ()
		{
			thread.Start ();
		}

		public void Stop ()
		{
			active = false;
			try {
				if (request.Response != null)
					request.Response.Dispose ();
			} catch (NullReferenceException) {
			}
		}

		private DateTime watchdogTimeout = new DateTime (0);
		/// <summary>
		/// Check if Thread is ok, force it to close otherwise
		/// </summary>
		/// <returns>
		/// True if session is to be removed.
		/// </returns>
		public bool WatchDog ()
		{
			if (active == false && thread.IsAlive == false)
				return true;
			
			if (watchdogTimeout.Ticks > 0 && watchdogTimeout < DateTime.Now) {
				Console.Error.WriteLine ("Watchdog: Stopping " + this);
				Stop ();
				return true;
			}
			
			if (active == false && watchdogTimeout.Ticks == 0 && clientSocket.IsConnected () == false) {
				Console.Error.WriteLine ("Watchdog: Countdown " + this);
				watchdogTimeout = DateTime.Now.AddSeconds (10);
			}
			return false;
		}

		/// <summary>
		/// Main loop for handling requests on a single proxy connection
		/// </summary>
		public void Run ()
		{
			Status = "Running";
			try {
				while (true) {
					
					if (active == false)
						break;
					
					Status = "Waiting for new request";
					
					//Waiting for new request
					if (GotNewRequest (clientSocket) == false)
						break;
					
					bool keepAlive = RunRequest ();
					
					//Flush the TCP connection, temporarily disable Nagle's algorithm
					if (keepAlive) {
						clientSocket.NoDelay = true;
						clientSocket.NoDelay = false;
					}
					
					served += 1;
					if (keepAlive == false)
						break;
					
					//Cleanup remaining resources
					request.Dispose ();
					request = null;
				}
			} finally {
				request.NullSafeDispose ();
				Dispose ();
				active = false;
			}
		}

		/// <summary>
		/// Process a single request
		/// Return true to keep connection open
		/// </summary>
		private bool RunRequest ()
		{
			try {
				request = new Request (clientSocket);
				string header = request.DataSocket.ReadHeader ();
				if (header.Length > 10000)
					Console.Error.WriteLine ("Large header");
				ParseRequest (header);
				if (request == null)
					throw new HeaderException ("No request received", HttpStatusCode.BadRequest);
			} catch (ObjectDisposedException) {
				return false;
			} catch (HeaderException) {
				return false;
			}
			
			Status = "Got request, filtering";
			FilterRequest (request);
			
			//If there is no error generated response, make connection to remote server
			CachedConnection remoteConnection = null;
			if (request.Response == null) {
				
				try {
					remoteConnection = ConnectRequest ();
				} catch (TimeoutException e) {
					request.Response = new Response (e, Html.Escape ("Connection Timeout"));
				} catch (HeaderException e) {
					request.Response = new Response (e, new Html ());
				}
				
			}
			
			if (request.Response != null) {
				//Fix response keep alive header
				if (request.KeepAlive && request.HttpVersion == "HTTP/1.0")
					request.Response.ReplaceHeader ("Connection", "Keep-Alive");
				
				Status = "Sending response";
				SendResponse ();
				return request.KeepAlive;
			}
			
			try {
				//Begin connection communication
				
				//Prepare socks connection
				if (request.Proxy != null && request.Proxy.Scheme == "socks")
					PrepareSocks (remoteConnection);
				
				//initiate HTTP CONNECT request
				if (request.Method == "CONNECT") {
					Status = "Connecting Socket";
					ProcessHttpConnect (remoteConnection);
					return false;
				}
				
				//All done, send the traffic to the remote server
				ProcessHttp (remoteConnection);
				
				Status = "Request done";
				
			} catch (HeaderException e) {
				Console.Error.WriteLine (e.GetType () + ": " + e.Message);
				if (Status == "Sending response")
					return false;
				request.Response = new Response (e);
				if (SendResponse () == false)
					return false;
			} catch (SocketException e) {
				Console.Error.WriteLine (e.GetType () + ": " + e.Message + "\n" + e.StackTrace);
				if (Status == "Sending response")
					return false;
				
				request.Response = new Response (e);
				if (SendResponse () == false)
					return false;
			} catch (IOException e) {
				Console.Error.WriteLine (e.GetType () + ": " + e.Message + "\n" + e.StackTrace);
				if (Status == "Sending response")
					return false;
				
				request.Response = new Response (e);
				if (SendResponse () == false)
					return false;
			} catch (ObjectDisposedException e) {
				Console.Error.WriteLine (e.GetType () + ": " + e.Message + "\n" + e.StackTrace);
				if (Status == "Sending response")
					return false;
				
				request.Response = new Response (e);
				if (SendResponse () == false)
					return false;
			}
			
			//Close connection
			if (request.Response.Chunked == false && request.Response.HasBody == false)
				return false;
			if (request.Response.KeepAlive == false)
				return false;
			
			//Check so there is no extra data sent from the remote server
			try {
				if (remoteConnection.remoteSocket.Available > 0 && clientSocket.IsConnected ()) {
					byte[] buffer = new byte[remoteConnection.remoteSocket.Available];
					remoteConnection.remoteSocket.Receive (buffer);
					string data = System.Text.Encoding.ASCII.GetString (buffer);
					Console.Error.WriteLine ("More data than meets the eye: " + data);
					return false;
				}
			} catch (ObjectDisposedException) {
				return false;
			}
			
			return request.KeepAlive;
		}

		/// <summary>
		/// Send response, headers and data, back to client
		/// </summary>
		bool SendResponse ()
		{
			try {
				//Send back headers
				request.Response.SendHeaders (request.DataSocket);
				
				if (request.Response.HasBody == false)
					return true;
				
				if (request.Response.DataFiltered == null)
					return false;
				
				//Pipe result back to client
				if (request.Response.ContentLength > 0)
					request.Response.DataFiltered.PipeTo (request.DataFiltered, request.Response.ContentLength);
				if (request.Response.ContentLength < 0)
					request.Response.DataFiltered.PipeTo (request.DataFiltered);
				
				return true;
				
			} catch (SocketException se) {
				Console.Error.WriteLine (se.GetType ().ToString () + ": " + se.Message);
				return false;
			} catch (ObjectDisposedException ode) {
				Console.Error.WriteLine (ode.GetType ().ToString () + ": " + ode.Message);
				return false;
			}
		}

		private void FilterRequest (Request request)
		{
			try {
				if (proxy.WebUI.Apply (request))
					return;
				foreach (Trigger t in proxy.RequestTriggers.ToArray ())
					t.Apply (request);
				foreach (Filter f in proxy.RequestFilters.ToArray ())
					f.Apply (request);
			} catch (Exception e) {
				request.Response = new Response (e, Html.Format (@"<h1>In Filter</h1><p><a href=""{0}"">Manage filters</a></p>", Filters.WebUI.FilterUrl ()));
			}
		}

		/// <summary>
		/// From the data in the request,
		/// Connect, return connection if successful.
		/// </summary>
		private CachedConnection ConnectRequest ()
		{
			if (request.Uri.Host == "localhost" && request.Uri.Port == MainClass.ProxyPort) {
				request.Response = new BlockedResponse ("Loopback protection");
				return null;
			}
			
			if (request.Uri.HostNameType == UriHostNameType.Unknown) {
				request.Response = new Response (HttpStatusCode.BadRequest, "Invalid URL", "Invalid request: " + request);
				return null;
			}
			
			if (request.Uri.Scheme != "http" && request.Uri.Scheme != "connect") {
				request.Response = new Response (HttpStatusCode.NotImplemented, "Unsupported Scheme", "Scheme Not implemented: " + request.Uri.Scheme);
				return null;
			}
			
			try {
				CachedConnection remote;
				Status = "Connecting to " + request.Uri.Host;
				
				Uri remoteUri = request.Uri;
				if (request.Proxy != null)
					remoteUri = request.Proxy;
				
				if (request.Method == "CONNECT" || request.Proxy != null && request.Proxy.Scheme == "socks")
					remote = connectionManager.ConnectNew (remoteUri, false);
				else
					remote = connectionManager.Connect (remoteUri);
				
				if (remote == null) {
					request.Response = new Response (HttpStatusCode.GatewayTimeout, "Connection Failed", "Failed to get connection to " + request);
					return null;
				}
				
				Status = "Connected";
				return remote;
				
			} catch (SocketException e) {
				string extra = "";
				if (request.Proxy != null)
					extra = ": " + request.Proxy;
				throw new HeaderException (e.Message + extra, HttpStatusCode.BadGateway, e);
			} catch (IOException e) {
				throw new HeaderException (e.Message, HttpStatusCode.BadGateway, e);
			}
		}

		private bool GotNewRequest (Socket client)
		{
			DateTime timeout = DateTime.Now.AddSeconds (30);
			try {
				while (client.Available == 0) {
					if (active == false)
						return false;
					if (client.IsConnected () == false)
						return false;
					
					client.Poll (1000000, SelectMode.SelectRead);
					
					if (DateTime.Now > timeout)
						return false;
				}
				return true;
			} catch (SocketException) {
				return false;
			}
		}

		public override string ToString ()
		{
			return name;
		}
		
	}
}
