
using System;

namespace HitProxy.Filters
{
	/// <summary>
	/// Replace the request with a break page
	/// </summary>
	public class BlockBreak : Block
	{
		public override bool Apply (Request request)
		{
			Response resp = new Response (System.Net.HttpStatusCode.OK);
			request.Response = resp;
			
			string data = @"<!DOCTYPE html>
<html>
<head><title>Take a break</title></head>
<body>
	<h1>Take a break!</h1>
	<p>It's now time for a break.</p>
</body></html>";
			
			resp.SetData (data);
			
			return true;
		}
		
	}
}
