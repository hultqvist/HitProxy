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
		
		public override Response Status (NameValueCollection httpGet, Request request)
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
			
			return WebUI.ResponseTemplate (ToString (), html);
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
			request.Response.Stream = new SlowReader (request.Response.Stream, this);
			
			return true;
		}

		class SlowReader : Stream
		{
			public override void Close ()
			{
				backend.Close ();
			}
			
			public override void Flush ()
			{
				backend.Flush ();
			}

			public override long Seek (long offset, SeekOrigin origin)
			{
				return backend.Seek (offset, origin);
			}

			public override void SetLength (long value)
			{
				backend.SetLength (value);
			}

			public override void Write (byte[] buffer, int offset, int count)
			{
				throw new NotImplementedException ();
			}

			public override bool CanRead { get { return backend.CanRead; } }

			public override bool CanSeek {
				get { return backend.CanSeek; }
			}

			public override bool CanWrite {
				get { return backend.CanWrite; }
			}

			public override long Length {
				get { return backend.Length; }
			}

			public override long Position {
				get { return backend.Position; }
				set { backend.Position = value; }
			}

			readonly Slow settings;
			readonly Stream backend;

			public SlowReader (Stream input, Slow slow)
			{
				this.settings = slow;
				this.backend = input;
			}
			
			DateTime starttime;
			int totalSent = 0;

			public override int Read (byte[] buffer, int offset, int count)
			{
				if (totalSent == 0)
					starttime = DateTime.Now;
				
				if (count == 0)
					return 0;
				
				int tosend = 0;
				while (true) {
					tosend = (int)(DateTime.Now - starttime).TotalSeconds * settings.speedLimit - totalSent;
					if (tosend > count)
						tosend = count;
					if (tosend == 0) {
						Thread.Sleep (10);
						continue;
					}
					break;
				}
				int read = backend.Read (buffer, offset, tosend);
				totalSent += read;
				return read;
			}

			
		}
	}
}
