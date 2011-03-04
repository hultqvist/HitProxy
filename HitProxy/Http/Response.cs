using System;
using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Web;
using HitProxy.Connection;

namespace HitProxy.Http
{
	public class Response : Header
	{
		//HTTP Headers
		public string HttpVersion;
		public HttpStatusCode HttpCode;
		public string Message;

		public string AcceptRanges;
		public string Age;
		public long ContentLength = -1;
		public string ETag;
		public string Location;
		public string ProxyAuthenticate;

		public string RetryAfter;
		public string Server;
		public string Vary;
		public string WWWAuthenticate;

		//Transfer parameters
		public bool KeepAlive = false;
		public bool Chunked = false;

		/// <summary>
		/// Whether a response is generated in the proxy or a remote response
		/// </summary>
		private byte[] GeneratedResponse = null;
		/// <summary>
		/// True if content-length is specified.
		/// Otherwise it is a Connection: close session.
		/// </summary>
		public bool HasBody { get; set; }

		public override string FirstLine {
			get { return HttpVersion + " " + ((int)HttpCode) + " " + Message; }
		}

		/// <summary>
		/// Used for local generated data such as
		/// error, block and configuration pages.
		/// </summary>
		public Response (HttpStatusCode code)
		{
			this.HttpVersion = "HTTP/1.1";
			this.HttpCode = code;
			this.Message = code.ToString ();
			this.KeepAlive = true;
			this.HasBody = true;
		}

		/// <summary>
		/// Default constructor for incoming responses.
		/// </summary>
		public Response (CachedConnection connection)
		{
			DataSocket = new SocketData (connection);
		}

		/// <summary>
		/// Generated response with message from the proxy
		/// </summary>
		public Response (HttpStatusCode code, string title, string message) : this(code)
		{
			Template (title, "<p>" + Html (message) + @"</p>");
		}

		protected override void ParseFirstLine (string firstLine)
		{
			string[] parts = firstLine.Split (new char[] { ' ' }, 3);
			if (parts.Length == 3)
				Message = parts[2];
			else if (parts.Length == 2)
				Message = "";
			else
				throw new HeaderException ("Invalid header: " + firstLine, HttpStatusCode.BadGateway);
			
			HttpVersion = parts[0];
			try {
				HttpCode = (HttpStatusCode)int.Parse (parts[1]);
			} catch (FormatException e) {
				throw new HeaderException ("StatusCode format " + e.Message + "\n" + firstLine, HttpStatusCode.BadRequest);
			}
		}

		public void Parse (string header, Request request)
		{
			//Read and parse header
			base.Parse (header);
			
			if (HttpVersion == "HTTP/1.1")
				KeepAlive = true;
			
			foreach (string line in this) {
				int keysep = line.IndexOf (':');
				if (keysep < 0)
					continue;
				
				string key = line.Substring (0, keysep).ToLowerInvariant ();
				string s = line.Substring (keysep + 1).Trim ();
				switch (key) {
				case "accept-ranges":
					AcceptRanges = s;
					break;
				case "age":
					Age = s;
					break;
				case "connection":
					if (s.ToLowerInvariant () == "close")
						KeepAlive = false; else if (s.ToLowerInvariant ().Contains ("keep-alive"))
						KeepAlive = true;
					else
						Console.WriteLine ("ResponseHeader: unknown Connection: " + s);
					break;
				case "content-length":
					long.TryParse (s, out ContentLength);
					break;
				case "etag":
					ETag = s;
					break;
				case "location":
					Location = s;
					break;
				case "proxy-authenticate":
					ProxyAuthenticate = s;
					break;
				case "retry-after":
					RetryAfter = s;
					break;
				case "server":
					Server = s;
					break;
				case "transfer-encoding":
					if (s == "chunked")
						Chunked = true;
					else
						Console.Error.WriteLine ("Unknown Transfer-Encoding: " + s);
					break;
				case "vary":
					Vary = s;
					break;
				case "www-authenticate":
					WWWAuthenticate = s;
					break;
				}
			}
			
			if (HttpCode == HttpStatusCode.NoContent || HttpCode == HttpStatusCode.NotModified || request.Method.ToLowerInvariant () == "head")
				HasBody = false;
			else
				HasBody = true;
		}

		/// <summary>
		/// Send response, headers and data, back to client
		/// </summary>
		public bool SendResponse (Socket outputSocket)
		{
			try {
				//Send back headers
				SendHeaders (outputSocket);
				
				if (GeneratedResponse != null) {
					outputSocket.Send (GeneratedResponse);
					return true;
				}
				
				if (Chunked) {
					DataSocket.SendChunkedResponse (outputSocket);
					return true;
				}
				
				if (HasBody == false)
					return true;
				
				if (DataSocket == null)
					return false;
				
				//Pipe result back to client
				if (ContentLength > 0)
					DataSocket.PipeTo (outputSocket, ContentLength); else if (ContentLength < 0)
					DataSocket.PipeTo (outputSocket);
				return true;
			} catch (SocketException se) {
				Console.Error.WriteLine (se.GetType ().ToString () + ": " + se.Message);
				return false;
			} catch (ObjectDisposedException ode) {
				Console.Error.WriteLine (ode.GetType ().ToString () + ": " + ode.Message);
				return false;
			}
		}

		/// <summary>
		/// Generates a custom data for the response.
		/// Content-Length and Content-Type headers are added automatically
		/// </summary>
		/// <param name="data">
		/// the content data
		/// </param>
		public void SetData (string data)
		{
			if (DataSocket != null) {
				DataSocket.Release ();
				DataSocket = null;
			}
			
			GeneratedResponse = Encoding.UTF8.GetBytes (data);
			
			ReplaceHeader ("Content-Length", GeneratedResponse.Length.ToString ());
			ReplaceHeader ("Content-Type", "text/html; charset=UTF-8");
			ContentLength = GeneratedResponse.Length;
		}

		public static string Html (string text)
		{
			return HttpUtility.HtmlEncode (text);
		}

		public void Template (string title, string htmlContents)
		{
			SetData (string.Format (@"<!DOCTYPE html>
<html>
<head>
	<meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"" />
	<link rel=""stylesheet"" type=""text/css"" href=""http://{0}/style.css"" />
	<title>{2} - HitProxy</title>
</head>
<body class=""{1}"">
	<h1>{2}</h1>
	{3}
</body>
</html>", Filters.WebUI.ConfigHost, HttpCode.ToString (), Html (title), htmlContents));
		}
		
	}
}
