using System;
using System.Net;
using System.Net.Sockets;
using System.Web;
using System.Text;
using System.IO;
using HitProxy.Filters;

namespace HitProxy.Http
{

	/// <summary>
	/// Standard blocked message
	/// </summary>
	public class BlockedResponse : Response
	{
		public BlockedResponse (string message) : this("Blocked Forever", Html.Format(message))
		{
		}

		public BlockedResponse (string title, Html htmlMessage) : base(HttpStatusCode.Gone)
		{
			//Console.WriteLine ("Blocked: " + message);
			KeepAlive = true;
		
			Template(title, htmlMessage);
		}
	}
}
