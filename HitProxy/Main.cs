using System;
using System.IO;
using HitProxy.Filters;
using System.Net;
using Mono.Options;
using System.Collections.Generic;
using HitProxy.Connection;

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
			
			ConnectionManager connectionManager = new ConnectionManager ();
			Proxy proxy = new Proxy (listenIP, port, connectionManager);
			System.Threading.Thread.CurrentThread.Name = "Main";
			
			//TODO: Read filter configuration from Filters.conf
			//TODO: Separate List, Trigger, Modify and Block-Filters.
			
			//Setup default filters
			FilterList list = new FilterListBlock ();
			proxy.FilterRequest = list;
			list.Add (new WebUI (proxy, connectionManager));
			//list.Add (new Tamper ("Before filtering"));
			//list.Add (new TransparentSSL ());
			list.Add (new AdBlock ());
			list.Add (new Rewrite ());
			list.Add (new Referer ());
			list.Add (new UserAgent ());
			list.Add (new Cookies ());
			list.Add (new ProxyHeaders ());
			list.Add (new I2PProxy ());
			list.Add (new Onion ());
			//list.Add (new Tamper ("After filtering"));
			
			FilterList response = new FilterList ();
			proxy.FilterResponse = response;
			//response.Add (new Tamper ("Response"));
			//response.Add (new CustomError ());
			response.Add (new Cookies ());
			
			proxy.Start ();
			if (startBrowser)
			{
				try
				{
					System.Threading.Thread.Sleep (3000);
					System.Diagnostics.Process.Start ("http://localhost:" + proxy.Port + "/");
				}
				catch (Exception e)
				{
					Console.Error.WriteLine ("Failed to launch browser: " + e.Message);
				}
			}
			proxy.Wait ();
			proxy.Stop ();
		}
	}
}
