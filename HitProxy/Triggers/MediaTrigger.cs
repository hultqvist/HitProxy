using System;
using HitProxy.Http;

namespace HitProxy.Triggers
{
	public class MediaTrigger : Trigger
	{
		public MediaTrigger ()
		{
		}

		public override Html Status ()
		{
			return Html.Escape (@"Sets the ""save"" flag to all responses > 2MB");
		}

		public override bool Apply (Request request)
		{
			if (request.Response == null)
				return false;
			
			string type = request.Response.GetHeader ("Content-Type");
			
			if (type.StartsWith ("video/")) {
				request.Response.SetFlags ("save");
				return true;
			}
			
			// > 2MB
			if (request.Response.ContentLength < 0 || request.Response.ContentLength > 2000000)
			{
				if (type.EndsWith ("octet-stream")) {
					request.Response.SetFlags ("save");
					return true;
				}
			}
			return false;
		}
	}
}

