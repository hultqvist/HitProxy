using System;
using System.IO;
using PersonalProxy.Filters;

namespace PersonalProxy
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			//Prepare config folder
			string configPath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), "PersonalProxy");
			Directory.CreateDirectory (configPath);
			
			ConnectionManager connectionManager = new ConnectionManager ();
			Proxy proxy = new Proxy (8080, connectionManager);
			System.Threading.Thread.CurrentThread.Name = "Main";
			
			//Setup default filters
			FilterList list = new FilterListBlock ();
			list.Add (new WebUI (proxy, connectionManager));
			//list.Add (new Tamper ("Before filtering"));
			//list.Add (new TransparentSSL ());
			list.Add (new AdBlock ());
			list.Add (new Rewrite ());
			list.Add (new Referer ());
			list.Add (new UserAgent ());
			list.Add (new ProxyHeaders ());
			//list.Add (new Tamper ("After filtering"));
			
			proxy.FilterRequest = list;
			
			FilterList response = new FilterList ();
			proxy.FilterResponse = response;
			//response.Add (new Tamper ("Response"));
			//response.Add (new CustomError ());
			
			proxy.Start ();
			
			//System.Threading.Thread.Sleep(3000);
			//System.Diagnostics.Process.Start("http://localhost:"+proxy.Port+"/");
			
			proxy.Wait ();
			proxy.Stop ();
		}
	}
}
