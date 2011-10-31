using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
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
		/// Read a new request
		/// </summary>
		/// <returns>
		/// A <see cref="Request"/>
		/// </returns>
		private void ParseRequest (string header)
		{
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
				request.SendHeaders (remoteConnection.Stream);
				
				//Send POST data, if available
				if (request.Method == "POST") {
					if (request.ContentLength > 0) {
						ClientStream.PipeTo (remoteConnection.Stream, request.ContentLength);
					} else {
						//Ignore, assume content-length of 0 is ok.
						//throw new HeaderException ("Missing Content-Length in POST request", HttpStatusCode.BadRequest);
					}
				}

			} catch (IOException e) {
				throw new HeaderException ("While sending request to remote: " + e.Message, HttpStatusCode.BadGateway, e);
			}
			
			//Read response header
			while (true) {
				Status = "Waiting for response";
				
				string respHeader = request.Response.ReadHeader ();
				request.Response.Parse (respHeader, request);
				
				//Apply chunked data
				if (request.Response.Chunked) {
					request.Response.Stream = new ChunkedInput (request.Response.Stream);
					request.Stream = new ChunkedOutput (request.Stream);
				}
				
				//Filter Response
				Status = "Filtering response";
				try {
					foreach (Trigger t in proxy.ResponseTriggers.ToArray ())
						if (t.Active)
							t.Apply (request);
					foreach (Filter f in proxy.ResponseFilters.ToArray ())
						if (f.Active)
							f.Apply (request);
				} catch (Exception e) {
					request.Response = new Response (e, Html.Format (@"<h1>In Filter</h1><p><a href=""{0}"">Manage filters</a></p>", Filters.WebUI.FilterUrl ()));
				}
				
				//Send response
				Status = "Sending response back to browser";
				SendResponse ();
				
				int code = (int)request.Response.HttpCode;
				if (code < 100 || 200 <= code)
					break;
			}
		}
	}
}
