using System;
using System.Net;
using System.Net.Sockets;
using System.Web;
using System.Text;
using System.IO;
using PersonalProxy.Filters;

namespace PersonalProxy
{

	/// <summary>
	/// Standard blocked message
	/// </summary>
	public class BlockedResponse : Response
	{
		public BlockedResponse (string message) : this("Blocked Forever", Html(message))
		{
		}

		public BlockedResponse (string title, string htmlMessage) : base(HttpStatusCode.Gone)
		{
			//Console.WriteLine ("Blocked: " + message);
			KeepAlive = true;
			
			SetData (@"<!DOCTYPE htm>
		<html><head><title>Blocked</title></head>
		<body style=""border: 2px solid red; opacity: 0.5;"">
		<h1>"+Html(title)+@"</h1>
		" + htmlMessage + @"
		</body></html>
");
		}
	}
}
