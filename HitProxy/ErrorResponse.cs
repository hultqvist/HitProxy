
using System;
using System.Net;
using System.Web;
using System.Text;
using System.IO;
using HitProxy.Filters;

namespace HitProxy
{
	/// <summary>
	/// Standard error response to client
	/// </summary>
	public class ErrorResponse : Response
	{
		string errorMessage;

		public ErrorResponse (HttpStatusCode code, string message) : base(code)
		{
			KeepAlive = true;
			errorMessage = message;
			
			Console.Error.WriteLine (this);
			
			SetData (@"<!DOCTYPE html>
<html>
<head><title>Error - Hit Proxy</title></head>
<body style=""background: orange;"">
<h1>Error</h1>
<p>" + Html (message) + @"</p>
</body></html>");
		}

		public override string ToString ()
		{
			return string.Format ("[ErrorResponse {0}]", errorMessage);
		}
	}

	/// <summary>
	/// Used to report errors in request.
	/// Here we won't keep the connection open.
	/// </summary>
	public class RequestErrorResponse : ErrorResponse
	{
		public RequestErrorResponse (HttpStatusCode code, string message) : base(code, message)
		{
			KeepAlive = false;
		}
	}
}
