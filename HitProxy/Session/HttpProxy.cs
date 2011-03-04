using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.IO;
using System.Text;
using HitProxy.Http;
using HitProxy.Connection;

namespace HitProxy.Session
{
	public partial class ProxySession
	{
		/// <summary>
		/// Read a new request
		/// </summary>
		/// <returns>
		/// A <see cref="Request"/>
		/// </returns>
		private Request ParseRequest (string header)
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
				request.Response = new Response (HttpStatusCode.BadRequest, "Bad Request", e.Message + "\n" + e.StackTrace);
				request.Response.KeepAlive = false;
			}
			return request;
		}

		/// <summary>
		/// 
		/// </summary>
		private void ProcessHttp (CachedConnection remoteConnection)
		{
			//link connection to response so that it will get closed on error
			request.Response = new Response (remoteConnection);
			
			Status = "Sending request to server";
			try {
				request.SendHeaders (remoteConnection.remoteSocket);
			} catch (IOException e) {
				throw new HeaderException ("While sending request to remote: " + e.Message, HttpStatusCode.BadGateway);
			}
			//Read response header
			while (true) {
				Status = "Waiting for response";
				
				string respHeader = remoteConnection.remoteSocket.ReadHeader ();
				
				request.Response.Parse (respHeader, request);
				
				//Filter Response
				Status = "Filtering response";
				try {
					proxy.FilterResponse.Apply (request);
				} catch (Exception e) {
					request.Response = FilterException (e);
				}
				
				//Send response
				Status = "Sending response back to browser";
				request.Response.SendResponse (clientSocket);
				
				int code = (int)request.Response.HttpCode;
				if (code < 100 || code > 199)
					break;
			}
		}
	}
}
