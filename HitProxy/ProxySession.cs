
using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;

namespace HitProxy
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
				proxy.FilterRequest.Apply (request);
			} catch (Exception e) {
				request.Response = FilterException (e);
			}
			
			//Send filter generated responses
			if (request.Response != null) {
				Status = "Sending filter response";
				request.Response.SendResponse (clientSocket);
				return true;
			}
			
			//Begin connection
			if (request.Proxy != null && request.Proxy.Scheme == "socks")
			{
			}

			//Make connection
			CachedConnection remoteConnection = null;
			try {
				remoteConnection = ConnectRequest (request, connectionManager);
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

			if (request.Method == "CONNECT") {
				Status = "Connecting Socket";
				ProcessHttpConnect (remoteConnection);
				return false;
			}

			//All done, send the traffic to the remote server
			if (ProcessHttp (remoteConnection) == false)
				return false;
			
			Status = "Request done";
			
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

		private Response FilterException (Exception e)
		{
			Response response = new Response (HttpStatusCode.InternalServerError);
			response.Template ("Filter Error", string.Format (@"
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

	/// <summary>
	/// Indicates errors in the http headers
	/// </summary>
	public class HeaderException : Exception
	{
		public HttpStatusCode HttpCode { get; set; }

		public HeaderException (string message, HttpStatusCode httpCode) : base(message)
		{
			//Console.Error.WriteLine (httpCode + " " + message);
			this.HttpCode = httpCode;
		}
	}
}
