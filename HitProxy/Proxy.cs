
using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using HitProxy.Filters;

namespace HitProxy
{
	public class Proxy
	{
		private int port;
		public int Port {
			get { return port; }
		}
		Thread thread;

		/// <summary>
		/// List of active ProxySessions.
		/// </summary>
		private List<ProxySession> proxyList = new List<ProxySession> ();
		private ConnectionManager connectionManager;

		public Filter FilterRequest { get; set; }
		public Filter FilterResponse { get; set; }
		public Proxy (int port, ConnectionManager connectionManager)
		{
			this.port = port;
			this.connectionManager = connectionManager;
		}

		public void Start ()
		{
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
			TcpListener listener = new TcpListener (new IPEndPoint (IPAddress.Loopback, Port));
			
			bool retrying = false;
			
			while (true) {
				try {
					listener.Start ();
					Console.WriteLine ("Set your browser proxy settings to: localhost, port " + Port);
					
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
