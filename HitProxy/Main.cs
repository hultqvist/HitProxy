using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using ProtoBuf;
using Mono.Options;
using HitProxy.Connection;
using System.Threading;
using HitProxy.Http;

namespace HitProxy
{
	class MainClass
	{
		public const int ProxyPort = 8080;

		public static void Main (string[] args)
		{
			IPAddress listenIP = IPAddress.Loopback;
			int port = ProxyPort;
			bool startBrowser = true;
			
			OptionSet options = new OptionSet ();
			options.Add ("l|listen=", "Listen on IP\nuse 0.0.0.0 for any, default(localhost)", v => listenIP = IPAddress.Parse (v));
			options.Add ("p|port=", "Listen on port", v => port = int.Parse (v));
			options.Add ("s|server|no-browser", "Server mode, do not invoke the browser", v => startBrowser = false);
			List<string> extra;
			try {
				extra = options.Parse (args);
			} catch (Exception e) {
				Console.Error.WriteLine ("HitProxy: " + e.Message);
				Console.WriteLine ();
				options.WriteOptionDescriptions (Console.Out);
				return;
			}
			
			if (extra.Count > 0) {
				foreach (string cmd in extra)
					Console.Error.WriteLine ("Unknown argument: " + cmd);
				Console.WriteLine ();
				options.WriteOptionDescriptions (Console.Out);
				return;
			}
			
			//Prepare config folder
			string configPath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), "HitProxy");
			Directory.CreateDirectory (configPath);

			//Prepare proxy			
			Proxy proxy = new Proxy (listenIP, port);
			Thread.CurrentThread.Name = "Main";
			
			#region Request
			
			//Triggers
			proxy.RequestTriggers.Add (new Triggers.AdBlock ());
			proxy.RequestTriggers.Add (new Triggers.CrossDomain ());
			
			//Filters
			proxy.RequestFilters.Add (new Filters.Block ());
			proxy.RequestFilters.Add (new Filters.BlockBreak ());
			proxy.RequestFilters.Add (new Filters.Tamper ("Before filtering"));
			proxy.RequestFilters.Add (new Filters.TransparentSSL ());
			proxy.RequestFilters.Add (new Filters.Referer ());
			proxy.RequestFilters.Add (new Filters.Rewrite ());
			proxy.RequestFilters.Add (new Filters.UserAgent ());
			proxy.RequestFilters.Add (new Filters.NoAccept ());
			proxy.RequestFilters.Add (new Filters.Cookies ());
			proxy.RequestFilters.Add (new Filters.ProxyHeaders ());
			proxy.RequestFilters.Add (new Filters.I2PProxy ());
			proxy.RequestFilters.Add (new Filters.Onion ());
			proxy.RequestFilters.Add (new Filters.Tamper ("After filtering"));
			
			#endregion
			
			#region Response
			
			//Triggers
			proxy.ResponseTriggers.Add (new Triggers.MediaTrigger ());
			
			//Filters
			proxy.ResponseFilters.Add (new Filters.Cookies ());
			proxy.ResponseFilters.Add (new Filters.Saver ());
			proxy.ResponseFilters.Add (new Filters.Slow ());
			proxy.ResponseFilters.Add (new Filters.Tamper ("Response"));
			proxy.ResponseFilters.Add (new Filters.CustomError ());
			
			#endregion
			
			proxy.Start ();
			if (startBrowser) {
				try {
					System.Threading.Thread.Sleep (3000);
					System.Diagnostics.Process.Start ("http://localhost:" + proxy.Port + "/");
				} catch (Exception e) {
					Console.Error.WriteLine ("Failed to launch browser: " + e.Message);
				}
			}
			proxy.Wait ();
			proxy.Stop ();
		}
	}
}
