using System;
using System.IO;
using HitProxy.Filters;

namespace HitProxy
{
	class MainClass
	{
		public const int ProxyPort = 8080;
		
		public static void Main (string[] args)
		{
			//Prepare config folder
			string configPath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), "HitProxy");
			Directory.CreateDirectory (configPath);
			
			ConnectionManager connectionManager = new ConnectionManager ();
			Proxy proxy = new Proxy (ProxyPort, connectionManager);
			System.Threading.Thread.CurrentThread.Name = "Main";
			
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
			//list.Add (new Tamper ("After filtering"));
			
			FilterList response = new FilterList ();
			proxy.FilterResponse = response;
			//response.Add (new Tamper ("Response"));
			//response.Add (new CustomError ());
			response.Add (new Cookies ());
			
			proxy.Start ();
#if !DEBUG			
			System.Threading.Thread.Sleep(3000);
			System.Diagnostics.Process.Start("http://localhost:"+proxy.Port+"/");
#endif	
			proxy.Wait ();
			proxy.Stop ();
		}
	}
}
