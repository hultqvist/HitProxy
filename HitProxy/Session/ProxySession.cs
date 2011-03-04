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
		public Request Request {
			get { return request; }
			set { request = value; }
		}
		private Request request;

		string name;
		public int served = 0;
		public string Status = "Initialized";

		/// <summary>
		/// Start a new session thread for the incoming client.
		/// </summary>
		public ProxySession (Socket socket,Proxy proxy,ConnectionManager connectionManager)
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
			Request.NullSafeDispose ();
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
					served += 1;
					if (keepAlive == false)
						break;
					
					//Release proxy client connection
					Request.DataSocket.Release ();
					
					//Cleanup remaining resources
					Request.Dispose ();
					Request = null;
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
				string header = clientSocket.ReadHeader ();
				if (header.Length > 10000)
					Console.Error.WriteLine ("Large header");
				request = ParseRequest (header);
				if (request == null)
					throw new HeaderException ("No request received", HttpStatusCode.BadRequest);
			} catch (ObjectDisposedException) {
				return false;
			} catch (HeaderException) {
				return false;
			}
			
			Status = "Got request, filtering";
			
			//Filter Request
			try {
				foreach (Trigger t in proxy.RequestTriggers.ToArray ())
					t.Apply (request);
				foreach (Filter f in proxy.RequestFilters.ToArray ())
					f.Apply (request);
			} catch (Exception e) {
				request.Response = FilterException (e);
			}
			
			//Send filter generated responses
			if (request.Response != null) {
				Status = "Sending filter response";
				request.Response.SendResponse (clientSocket);
				return true;
			}
			
			//Make connection
			CachedConnection remoteConnection = null;
			try {
				remoteConnection = ConnectRequest ();
			} catch (TimeoutException e) {
				request.Response = new Response (HttpStatusCode.GatewayTimeout, "Connection Timeout", e.Message);
			} catch (HeaderException e) {
				request.Response = new Response (HttpStatusCode.BadGateway, "Header Error", request + ", " + e.Message);
			}
			
			//So far all responses are generated from errors
			if (request.Response != null) {
				Status = "Sending response";
				request.Response.SendResponse (clientSocket);
				return true;
			}
			
			try {
				//Begin connection
				
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
				
				request.Response = new Response (HttpStatusCode.BadGateway, "Header Error", e.Message);
				if (request.Response.SendResponse (clientSocket) == false)
					return false;
			} catch (SocketException e) {
				Console.Error.WriteLine (e.GetType () + ": " + e.Message + "\n" + e.StackTrace);
				if (Status == "Sending response")
					return false;
				
				request.Response = new Response (HttpStatusCode.BadGateway, "Connection Error", e.Message);
				if (request.Response.SendResponse (clientSocket) == false)
					return false;
			} catch (IOException e) {
				Console.Error.WriteLine (e.GetType () + ": " + e.Message + "\n" + e.StackTrace);
				if (Status == "Sending response")
					return false;
				
				request.Response = new Response (HttpStatusCode.BadGateway, "IO Error", e.Message);
				if (request.Response.SendResponse (clientSocket) == false)
					return false;
			} catch (ObjectDisposedException e) {
				Console.Error.WriteLine (e.GetType () + ": " + e.Message + "\n" + e.StackTrace);
				if (Status == "Sending response")
					return false;
				
				request.Response = new Response (HttpStatusCode.BadGateway, "Connection Abruptly Closed", e.Message);
				if (request.Response.SendResponse (clientSocket) == false)
					return false;
			}
			
			//Close connection
			if (request.Response.Chunked == false && request.Response.HasBody == false)
				return false;
			if (request.Response.KeepAlive == false)
				return false;
			
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
			return true;
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
				throw new HeaderException (e.Message + extra, HttpStatusCode.BadGateway);
			} catch (IOException e) {
				throw new HeaderException (e.Message, HttpStatusCode.BadGateway);
			}
		}

		private Response FilterException (Exception e)
		{
			Response response = new Response (HttpStatusCode.InternalServerError);
			response.Template ("Filter Error", Html.Format (@"
<h2>{0}, {2}</h2>
<p>{1}</p>
<pre>{3}</pre>
<p><a href=""{4}"">Manage filters</a></p>", e.GetType ().Name, e.Message, e.Source, e.StackTrace, Filters.WebUI.FilterUrl ()));
			return response;
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
					
					if (DateTime.Now > timeout)
						return false;
					Thread.Sleep (20);
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
