
using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.IO;
using System.Text;

namespace HitProxy
{
	public partial class ProxySession
	{
		/// <summary>
		/// Read a new request
		/// </summary>
		/// <returns>
		/// A <see cref="Request"/>
		/// </returns>
		public Request ParseRequest (string header)
		{
			Request request = new Request (clientSocket);
			
			try {
				Status = "Got request";
				
				//Start reading 
				request.Parse (header);
				
				//Fix config urls
				if (request.Uri == null)
					request.Uri = new Uri ("http://localhost:" + MainClass.ProxyPort + "/");
				if (request.Uri.Scheme == "file")
					request.Uri = new Uri ("http://localhost:" + MainClass.ProxyPort + "/" + request.Uri.PathAndQuery);
				if (request.Uri.Host == "")
					request.Uri = new Uri ("connect://" + request.Uri.OriginalString);
			} catch (HeaderException e) {
				Console.Error.WriteLine ("RequestHeader" + e.Message);
				request.Response = new RequestErrorResponse (HttpStatusCode.BadRequest, "RemoteSocket: " + e.Message + "\n" + e.StackTrace);
			}
			return request;
		}

		/// <summary>
		/// From the data in the request,
		/// Connect, return connection if successful.
		/// </summary>
		public CachedConnection ConnectRequest (Request request, ConnectionManager connectionManager)
		{
			if (request.Uri.Host == "localhost" && request.Uri.Port == MainClass.ProxyPort) {
				request.Block ("Loopback protection");
				return null;
			}
			
			if (request.Uri.HostNameType == UriHostNameType.Unknown) {
				request.Response = new ErrorResponse (HttpStatusCode.BadRequest, "Invalid request: " + request);
				return null;
			}
			if (request.Uri.Scheme != "http") {
				request.Response = new ErrorResponse (HttpStatusCode.NotImplemented, "Scheme Not implemented: " + request.Uri.Scheme);
				return null;
			}
			
			try {
				Status = "Connecting to " + request.Uri.Host;
				
				CachedConnection remote = connectionManager.Connect (request);
				if (remote == null) {
					request.Response = new ErrorResponse (HttpStatusCode.GatewayTimeout, "Failed to get connection to " + request);
					return null;
				}
				
				return remote;
			} catch (SocketException e) {
				throw new HeaderException (e.Message, HttpStatusCode.BadGateway);
			} catch (IOException e) {
				throw new HeaderException (e.Message, HttpStatusCode.BadGateway);
			}
		}
		
	}
}
