using System;
using System.Net;

namespace HitProxy.Http
{
	/// <summary>
	/// Indicates errors in the http headers
	/// </summary>
	public class HeaderException : Exception
	{
		public HttpStatusCode HttpCode { get; set; }

		public HeaderException (string message, HttpStatusCode httpCode) : base(message)
		{
			this.HttpCode = httpCode;
		}
		
		public HeaderException (string message, HttpStatusCode httpCode, Exception e) : base(message, e)
		{
			this.HttpCode = httpCode;
		}
	}
}

