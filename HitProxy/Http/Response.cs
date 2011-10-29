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
		public string HTTPMessage;
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
		/// True if content-length is specified.
		/// Otherwise it is a Connection: close session.
		/// </summary>
		public bool HasBody { get; set; }

		public override string FirstLine {
			get { return HttpVersion + " " + ((int)HttpCode) + " " + HTTPMessage; }
		}
		
		public override string ToString ()
		{
			return string.Format ("[Response: {0}]", FirstLine);
		}
		
		/// <summary>
		/// Used for local generated data such as
		/// error, block and configuration pages.
		/// </summary>
		public Response (HttpStatusCode code, Html html) : base(new HtmlData(html))
		{
			ReplaceHeader ("Content-Length", Stream.Length.ToString ());
			ReplaceHeader ("Content-Type", "text/html; charset=UTF-8");
			ContentLength = Stream.Length;
		
			this.HttpVersion = "HTTP/1.1";
			this.HttpCode = code;
			this.HTTPMessage = code.ToString ();
			this.KeepAlive = true;
			this.HasBody = true;
		}

		/// <summary>
		/// Default constructor for incoming responses.
		/// </summary>
		public Response (CachedConnection connection) : base(connection.Stream)
		{
		}

		/// <summary>
		/// Generated response with message from the proxy
		/// </summary>
		public Response (HttpStatusCode code, string title, string message) 
			: this(code, HtmlTemplate.Message( HttpStatusCode.OK, title, Html.Format ("<p>{0}</p>", message)))
		{
		}

		/// <summary>
		/// Generated response with message from the proxy
		/// </summary>
		public Response (Exception e) : this(e, new Html ())
		{
		}

		/// <summary>
		/// Generated response with message from the proxy
		/// </summary>
		public Response (Exception e, Html message) 
			: this((e is TimeoutException) ? HttpStatusCode.GatewayTimeout : HttpStatusCode.BadGateway, FormatException(e, message))
		{
			
		}
		
		static Html FormatException (Exception e, Html message)
		{
			Exception ne = e;
			while (ne != null) {
				message += Html.Format (@"<h2>{0}</h2><p>{1}</p><pre>{2}</pre>", ne.GetType ().FullName, ne.Message, ne.StackTrace);
				
				ne = ne.InnerException;
			}
			
			return HtmlTemplate.Message (HttpStatusCode.InternalServerError, e.GetType ().Name, message);
		}
		
		protected override void ParseFirstLine (string firstLine)
		{
			string[] parts = firstLine.Split (new char[] { ' ' }, 3);
			if (parts.Length == 3)
				HTTPMessage = parts [2];
			else if (parts.Length == 2)
				HTTPMessage = "";
			else
				throw new HeaderException ("Invalid header: " + firstLine, HttpStatusCode.BadGateway);
			
			HttpVersion = parts [0];
			try {
				HttpCode = (HttpStatusCode)int.Parse (parts [1]);
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
						KeepAlive = false;
					else if (s.ToLowerInvariant ().Contains ("keep-alive"))
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
	}
}
