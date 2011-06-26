using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using HitProxy.Http;
using HitProxy.Session;
using HitProxy.Connection;
using System.IO;
using ProtoBuf;

namespace HitProxy
{
	public class Proxy
	{
		private IPAddress address;
		private int port;

		public int Port {
			get { return port; }
		}

		public Settings Settings { get; private set; }

		public readonly Filters.WebUI WebUI;

		Thread thread;
		public readonly BrowserProxy Browser;

		/// <summary>
		/// List of active ProxySessions.
		/// </summary>
		private List<ProxySession> proxyList = new List<ProxySession> ();
		private ConnectionManager connectionManager = new ConnectionManager ();

		public ConnectionManager ConnectionManager {
			get { return connectionManager; }
		}

		public readonly List<Trigger> RequestTriggers = new List<Trigger> ();
		public readonly List<Filter> RequestFilters = new List<Filter> ();

		public readonly List<Trigger> ResponseTriggers = new List<Trigger> ();
		public readonly List<Filter> ResponseFilters = new List<Filter> ();

		public Proxy (IPAddress address, int port)
		{
			this.address = address;
			this.port = port;
			this.Browser = new BrowserProxy (this);
			this.WebUI = new Filters.WebUI (this);
			
			//Read Settings
			if (File.Exists (SettingsPath)) {
				using (Stream s = new FileStream (SettingsPath, FileMode.Open)) {
					this.Settings = Serializer.Deserialize<Settings> (s);
				}
			} else
				this.Settings = new Settings ();
		}

		private string SettingsPath {
			get { return Path.Combine (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), "HitProxy"), "HitProxy.settings"); }
		}

		private List<Filter> allFilters = new List<Filter>();
		
		private void ApplyFilterSettings ()
		{
			foreach (Trigger rqt in RequestTriggers)
				allFilters.Add(rqt);
			foreach (Filter rsf in RequestFilters)
				allFilters.Add(rsf);
			foreach (Trigger rst in ResponseTriggers)
				allFilters.Add(rst);
			foreach (Filter rsf in ResponseFilters)
				allFilters.Add(rsf);
			
			foreach(Filter f in allFilters)
				f.Active = Settings.Active.Contains (f.Name);
		}

		public void WriteSettings ()
		{
			this.Settings.Active.Clear();
			foreach (Filter f in allFilters)
				 if(f.Active && Settings.Active.Contains(f.Name) == false)
					Settings.Active.Add (f.Name);
			
			using (Stream s = new FileStream (SettingsPath, FileMode.Create)) {
				Serializer.Serialize<Settings> (s, this.Settings);
			}
		}

		public void Start ()
		{
			ApplyFilterSettings ();
			
			thread = new Thread (Run);
			thread.Name = "Proxy Listener";
			thread.Start ();
		}

		public void Wait ()
		{
			thread.Join ();
		}

		public void Stop ()
		{
			thread.Interrupt ();
			if (thread.Join (500) == false) {
				Console.Error.WriteLine ("Bye bye " + this);
				thread.Abort ();
			}
			
			foreach (ProxySession ps in ToArray ()) {
				ps.Stop ();
			}
		}

		public void Run ()
		{
			TcpListener listener = new TcpListener (new IPEndPoint (address, Port));
			
			bool retrying = false;
			
			while (true) {
				try {
					listener.Start ();
					Console.WriteLine ("HTTP Proxy listening on " + address + ", port " + Port);
					
					while (true) {
						
						//Cached connection cleaning
						connectionManager.Cleanup ();
						
						//Watchdog
						foreach (ProxySession session in ToArray ()) {
							if (session.WatchDog ()) {
								Remove (session);
							}
						}
						
						if (listener.Server.Poll (5000000, SelectMode.SelectRead) == false) {
							GC.Collect ();
							Thread.Sleep (50);
							continue;
						}
						
						TcpClient c = listener.AcceptTcpClient ();
						
						//Limit proxy connections
						if (proxyList.Count > 1000) {
							Console.WriteLine ("Proxy limit reached, denying");
							c.Close ();
							continue;
						}
						
						ProxySession ps = new ProxySession (c.Client, this, connectionManager);
						proxyList.Add (ps);
						ps.Start ();
					}
				} catch (SocketException e) {
					if (e.ErrorCode == 10048) {
						if (retrying == false) {
							Console.Error.WriteLine ("Error: " + e.Message);
							Console.Error.WriteLine ("Retrying...");
						}
						retrying = true;
					} else
						
						Console.Error.WriteLine ("Error: " + e.Message);
					
					Thread.Sleep (500);
				} finally {
					listener.Stop ();
				}
			}
		}

		public void Remove (ProxySession session)
		{
			lock (proxyList) {
				proxyList.Remove (session);
			}
		}

		public ProxySession[] ToArray ()
		{
			lock (proxyList) {
				return proxyList.ToArray ();
			}
		}
	}
}
