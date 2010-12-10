
using System;
using System.Net;
using System.IO;
using System.Net.Sockets;

namespace HitProxy
{
	public class Request : Header
	{
		//HTTP headers
		public string Method;
		public Uri Uri;
		public string HttpVersion;

		public string Accept;
		public string AcceptCharset;
		public string AcceptEncoding;
		public string AcceptLanguage;
		public string Authorization;
		public long ContentLength;
		public string Expect;
		public string From;
		public string Host;
		public string IfMatch;
		public string IfModifiedSince;
		public string IfNoneMatch;
		public string IfRange;
		public string IfUnmodifiedSince;
		public string MaxForwards;
		public string ProxyAuthorization;
		public string Range;
		public string Referer;
		public string TE;
		public string UserAgent;

		/// <summary>
		/// If set connection is made to this host rather than from Uri.
		/// Used to pipe to http proxies
		/// </summary>
		public Uri Proxy;

		/// <summary>
		/// Start time of request
		/// </summary>
		public DateTime Start = DateTime.Now;

		private Response response = null;
		public Response Response {
			get { return response; }
			set {
				response.NullSafeDispose ();
				response = value;
			}
		}

		/// <summary>
		/// Parse first line in a header
		/// </summary>
		public override string FirstLine {
			get {
				if (Proxy == null)
					return Method + " " + Uri.PathAndQuery + " " + HttpVersion;
				else
					return Method + " " + Uri.AbsoluteUri + " " + HttpVersion;
			}
		}

		public Request (Socket socket)
		{
			DataSocket = new SocketData (socket);
			Method = "NULL";
			Uri = new Uri ("http://localhost:" + MainClass.ProxyPort);
		}
		public override void Dispose ()
		{
			base.Dispose ();
			Response.NullSafeDispose ();
		}

		protected override void ParseFirstLine(string firstLine)
		{
			string[] parts = firstLine.Split (' ');
			if (parts.Length != 3)
				throw new HeaderException ("Invalid header: " + firstLine, HttpStatusCode.BadRequest);
				
			Method = parts[0].ToUpperInvariant ();
			HttpVersion = parts[2];
			if (System.Uri.TryCreate (parts[1], UriKind.Absolute, out this.Uri))
				return;
			if (System.Uri.TryCreate (parts[1], UriKind.Relative, out this.Uri))
				return;
		}

		public new void Parse (string header)
		{
			base.Parse (header);
			
			if (Method == "CONNECT")
				return;
			
			foreach (string line in this) {
				int keysep = line.IndexOf (':');
				if (keysep < 0)
					continue;
				
				string key = line.Substring (0, keysep).ToLowerInvariant ();
				string s = line.Substring (keysep + 1).Trim ();
				ParseHeader (key, s);
			}
			
			//Intercepting proxy get host from host header
			if (Uri.IsAbsoluteUri == false)
			{
				Uri baseUri = new Uri("http://" + Host);
				Uri = new Uri(baseUri, this.Uri);
			}
		}

		private void ParseHeader (string key, string value)
		{
			switch (key) {
			case "accept":
				Accept = value;
				break;
			case "accept-charset":
				AcceptCharset = value;
				break;
			case "accept-encoding":
				AcceptEncoding = value;
				break;
			case "accept-language":
				AcceptLanguage = value;
				break;
			case "authorization":
				Authorization = value;
				break;
			case "content-length":
				long.TryParse (value, out ContentLength);
				break;
			case "expect":
				Expect = value;
				break;
			case "from":
				From = value;
				break;
			case "host":
				Host = value;
				break;
			case "if-match":
				IfMatch = value;
				break;
			case "if-modified-since":
				IfModifiedSince = value;
				break;
			case "if-none-match":
				IfNoneMatch = value;
				break;
			case "if-range":
				IfRange = value;
				break;
			case "if-unmodified-since":
				IfUnmodifiedSince = value;
				break;
			case "max-forwards":
				MaxForwards = value;
				break;
			case "proxy-authorization":
				ProxyAuthorization = value;
				break;
			case "range":
				Range = value;
				break;
			case "referer":
				Referer = value;
				break;
			case "te":
				TE = value;
				break;
			case "user-agent":
				UserAgent = value;
				break;
			}
		}

		/// <summary>
		/// Send Headers and POST data.
		/// </summary>
		public override void SendHeaders (Socket socket)
		{
			base.SendHeaders (socket);
			
			//Send POST data, if available
			if (Method == "POST") {
				if (ContentLength > 0) {
					DataSocket.PipeTo (socket, ContentLength);
				} else {
					throw new HeaderException ("Missing Content-Length in POST request", HttpStatusCode.BadRequest);
				}
			}
		}

		/// <summary>
		/// Block the request with a custom message
		/// </summary>
		public void Block (string message)
		{
			Response = new BlockedResponse (message);
		}

		public void Block (string title, string htmlMessage)
		{
			//Determine content type requested
//			string cc = null;
//			while (true) {
//				if (Accept == null) {
//					if (Uri.AbsolutePath.EndsWith (".js")) {
//						cc = "application/x-javascript";
//						break;
//					}
//					if (Uri.AbsolutePath.EndsWith (".css")) {
//						cc = "text/css";
//						break;
//					}
//					if (Uri.AbsolutePath.EndsWith (".png")) {
//						cc = "image/png";
//						break;
//					}
//					if (Uri.AbsolutePath.EndsWith (".js")) {
//						cc = "application/javascript";
//						break;
//					}
//				}
//				if (Accept.StartsWith ("text/css")) {
//					cc = "text/css";
//					break;
//				}
//				
//				cc = "text/html";
//				break;
//			}
			
			Response = new BlockedResponse (title, htmlMessage);
		}

		public override string ToString ()
		{
			if (Uri == null)
				return "Broken Request";
			return Uri.ToString ();
		}
	}
}
