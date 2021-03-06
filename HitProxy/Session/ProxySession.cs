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
	public partial class ProxySession
	{
		/// <summary>
		/// Flag to signal that the thread should stop
		/// </summary>
		private bool active = true;
		public Socket ClientSocket;
		public Stream ClientStream;
		Thread thread;
		ConnectionManager connectionManager;
		Proxy proxy;
		
		/// <summary>
		/// When HTTPS is intercepted, all future requests will be sent to the following connection.
		/// </summary>
		CachedConnection sslConnect = null;
			
		/// <summary>
		/// Current request being served
		/// </summary>
		public Request request;
		string name;
		public int served = 0;
#if xDEBUG
		public string Status {
			get{ return _status;}
			set {
				_status = value;
				Console.WriteLine(this + ": " + value);
				Console.Out.Flush();
			}
		}
		string _status = "Initialized";
#else
		public string Status = "Initialized";
#endif

		/// <summary>
		/// Start a new session thread for the incoming client.
		/// </summary>
		public ProxySession (Socket socket, Proxy proxy, ConnectionManager connectionManager)
		{
			this.ClientSocket = socket;
			this.ClientStream = new NetworkStream (socket, true);
			this.proxy = proxy;
			this.connectionManager = connectionManager;
			thread = new Thread (Run);
			
			name = "[Session " + (socket.RemoteEndPoint as IPEndPoint).Port + "]";
			thread.Name = name;
		}

		public void Start ()
		{
			thread.Start ();
		}

		public void Stop ()
		{
			active = false;
			ClientStream.Close ();
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
					if (GotNewRequest (ClientSocket) == false)
						break;
					
					bool keepAlive = RunRequest ();
					
					//Flush the TCP connection, temporarily disable Nagle's algorithm
					if (keepAlive) {
						ClientSocket.NoDelay = true;
						ClientSocket.NoDelay = false;
					}
					
					served += 1;
					if (keepAlive == false)
						break;
					
					//Cleanup remaining resources
					request.Dispose ();
					request = null;
				}
			} finally {
				active = false;
				proxy.Remove (this);
				request.NullSafeDispose ();
				ClientStream.Close ();
			}
		}

		/// <summary>
		/// Process a single request
		/// Return true to keep connection open
		/// </summary>
		private bool RunRequest ()
		{
			//Client side read request
			try {
				request = new Request (ClientStream);
				string header = request.ReadHeader ();
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

			try {
				//Lookup DNS for request
				Status = "Looking up " + request.Uri.Host;
				request.Dns = DnsLookup.Get (request.Uri.Host);

				//Client side request filtering
				Status = "Filtering request";
				FilterRequest (request);
			} catch (HeaderException e) {
				request.Response = new Response (e, new Html ());
			}
				
			//If there is no error generated response, make connection to remote server
			CachedConnection remoteConnection = null;
			try {
				if (request.Response == null) {
					try {
						if (sslConnect != null)
							remoteConnection = sslConnect;
						else
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
			
				return ProcessRequest (remoteConnection);


			} catch (Exception ioe) {
				Console.WriteLine (ioe.GetType ().Name);
				Console.WriteLine (ioe.StackTrace);
				Console.WriteLine ();
				if (remoteConnection != null)
					remoteConnection.Dispose ();
				return false;
					
			} finally {
				if (remoteConnection != sslConnect && remoteConnection != null) {
					remoteConnection.Release ();
				}
			}
		}
		
		/// <returns>
		/// True if Keep-alive
		/// </returns>
		bool ProcessRequest (CachedConnection remoteConnection)
		{
			try {
				//Begin connection communication
				
				//Prepare socks connection
				if (request.Proxy != null && request.Proxy.Scheme == "socks")
					PrepareSocks (remoteConnection);
				
				//initiate HTTP CONNECT request
				if (request.Method == "CONNECT") {
					Status = "Connecting Socket";
					if (request.InterceptSSL) {
						//Intercept SSL
						this.ClientStream = ConnectProxy.InterceptConnect (request, ClientStream, remoteConnection);
						this.sslConnect = remoteConnection;
						return true;
					} else {
						//Pass connect stream unmodified
						Status = "Connected";
						ConnectProxy.ProcessHttpConnect (request, ClientStream, remoteConnection);
						Status = "Connection closed";
						return false;
					}
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
			
			return request.KeepAlive;
		}
		
		/// <summary>
		/// Send response, headers and data, back to client
		/// </summary>
		bool SendResponse ()
		{
			try {
				//Send back headers
				request.Response.SendHeaders (ClientStream);
				
				if (request.Response.HasBody == false)
					return true;
				
				if (request.Response.Stream == null)
					return false;
				
				//Pipe result back to client
				if (request.Response.ContentLength > 0)
					request.Response.Stream.PipeTo (request.Stream, request.Response.ContentLength);
				if (request.Response.ContentLength < 0)
					request.Response.Stream.PipeTo (request.Stream);
				request.Response.Stream.Close ();
				request.Stream.Close ();
				return true;
				
			} catch (IOException ioe) {
				Console.Error.WriteLine (ioe.GetType ().ToString () + ": " + ioe.Message);
				return false;
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
					if (t.Active)
						t.Apply (request);
				foreach (Filter f in proxy.RequestFilters.ToArray ())
					if (f.Active)
						f.Apply (request);
#if !DEBUG
			} catch (Exception e) {
				request.Response = new Response (e, Html.Format (@"<h1>In Filter</h1><p><a href=""{0}"">Manage filters</a></p>", Filters.WebUI.FilterUrl ()));
#else
			} finally {
#endif
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
				DnsLookup dns = request.Dns;
				if (request.Proxy != null) {
					remoteUri = request.Proxy;
					dns = request.ProxyDns;
				}
				
				
				if (request.Method == "CONNECT" || request.Proxy != null && request.Proxy.Scheme == "socks")
					remote = connectionManager.Connect (dns, remoteUri.Port, false, false);
				else
					remote = connectionManager.Connect (dns, remoteUri.Port, true, true);
				
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
