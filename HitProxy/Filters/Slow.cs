using System;
using HitProxy.Http;
using HitProxy.Connection;
using System.Collections.Specialized;
using System.Threading;

namespace HitProxy.Filters
{

	/// <summary>
	/// Simulates a slow network.
	/// Responsetime, increase the delay between request and response.
	/// Ratelimits the traffic to simulate a slow network.
	/// </summary>
	public class Slow : Filter
	{
		public override bool Apply (Request request)
		{
			if (request.Response == null)
				return false;
			if ((request.Flags["slow"] || request.Response.Flags["slow"]) == false)
				return false;
			
			//Intercept data connection
			request.Response.FilterData (new SlowOutput ());
			
			return true;
		}

		public override Html Status (NameValueCollection httpGet, Request request)
		{
			Html html = Html.Format (@"<p>Slows down the receiving speed to simulate slow websites, triggered by <strong>slow</strong> flag.</p>");
			return html;
		}

		class SlowOutput : IDataFilter
		{
			int rate = 1024;
			//bytes per second
			DateTime starttime;
			int totalSent = 0;

			public void Send (byte[] inBuffer, int start, int inLength, IDataOutput output)
			{
				if (totalSent == 0)
					starttime = DateTime.Now;
				
				int sent = 0;
				while (sent < inLength) {
					int tosend = (int)(DateTime.Now - starttime).TotalSeconds * rate - sent - totalSent;
					if (start + sent + tosend > inLength)
						tosend = inLength - start - sent;
					if (tosend == 0)
					{
						Thread.Sleep (1000);
						continue;
					}
					output.Send (inBuffer, start + sent, tosend);
					sent += tosend;
				}
				totalSent += inLength;
			}
			
			public void Dispose ()
			{
				
			}
		}
	}
}
