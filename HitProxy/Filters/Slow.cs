using System;
using HitProxy.Http;
using HitProxy.Connection;
using System.Collections.Specialized;
using System.Threading;
using System.IO;
using ProtoBuf;

namespace HitProxy.Filters
{

	/// <summary>
	/// Simulates a slow network.
	/// Responsetime, increase the delay between request and response.
	/// Ratelimits the traffic to simulate a slow network.
	/// </summary>
	[ProtoContract]
	public class Slow : Filter
	{
		/// <summary>
		/// Bytes/second
		/// </summary>
		[ProtoMember(1)]
		int speedLimit = 1024;
		
		/// <summary>
		/// Milliseconds
		/// </summary>
		[ProtoMember(2)]
		int delay = 500;
		
		public Slow ()
		{
			LoadSettings ();
		}
		
		private void LoadSettings ()
		{
			if (File.Exists (ConfigPath ()) == false)
				return;
			
			using (Stream s = new FileStream(ConfigPath(), FileMode.Open)) {
				Serializer.Merge<Slow> (s, this);
			}
		}
		
		private void SaveSettings ()
		{
			using (Stream s = new FileStream(ConfigPath(), FileMode.Create)) {
				Serializer.Serialize (s, this);
			}
		}
		
		public override Html Status (NameValueCollection httpGet, Request request)
		{
			if (httpGet ["action"] != null) {
				int.TryParse (httpGet ["speedlimit"], out speedLimit);
				int.TryParse (httpGet ["delay"], out delay);
				SaveSettings ();
			}
			
			Html html = Html.Format (@"
				<p>Slows down the receiving speed to simulate slow websites, triggered by <strong>slow</strong> flag.</p>
				<form action=""?"" method=""get"">
					<table>
					<tr>
						<th>Speed limit</th><td><input type=""text"" name=""speedlimit"" value=""{0}"" /> bytes/second</td>
					</tr>
					<tr>
						<th>Delay</th><td><input type=""text"" name=""delay"" value=""{1}"" /> milliseconds</td>
					</tr>
					<tr>
						<th></th><td><input type=""submit"" name=""action"" value=""Save"" /></td>
					</tr>
				</form>", this.speedLimit, this.delay);
			
			return html;
		}

		public override bool Apply (Request request)
		{
			if (request.Response == null)
				return false;
			if ((request.Flags ["slow"] || request.Response.Flags ["slow"]) == false)
				return false;
			
			//Added delay
			Thread.Sleep (delay);
			
			//Intercept data connection
			request.Response.FilterData (new SlowOutput (this));
			
			return true;
		}

		class SlowOutput : IDataFilter
		{
			readonly Slow settings;

			public SlowOutput (Slow slow)
			{
				this.settings = slow;
			}
			
			DateTime starttime;
			int totalSent = 0;

			public void Send (byte[] inBuffer, int start, int inLength, IDataOutput output)
			{
				if (totalSent == 0) {
					starttime = DateTime.Now;
				}
				
				int sent = 0;
				while (sent < inLength) {
					int tosend = (int)(DateTime.Now - starttime).TotalSeconds * settings.speedLimit - sent - totalSent;
					if (start + sent + tosend > inLength)
						tosend = inLength - start - sent;
					if (tosend == 0) {
						Thread.Sleep (10);
						continue;
					}
					output.Send (inBuffer, start + sent, tosend);
					sent += tosend;
				}
				totalSent += inLength;
			}
			
			public void EndOfData (IDataOutput output)
			{
				output.EndOfData ();
			}
			
			public void Dispose ()
			{
				
			}
		}
	}
}
