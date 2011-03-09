using System;
using HitProxy.Http;
using HitProxy.Connection;
using System.IO;
using System.Net.Sockets;

namespace HitProxy.Filters
{
	/// <summary>
	/// Save files with class "save" onto disk
	/// </summary>
	public class Saver : Filter
	{

		public override bool Apply (Request request)
		{
			if (request.Response == null)
				return false;
			if (request.TestClass ("save") == false)
				return false;
			
			//Intercept data connection
			request.Response.DataSocket = new FilteredData (request.Response.DataSocket);
			
			return true;
		}

		public override Html Status ()
		{
			return Html.Format (@"<p>Saves request data with class <strong>save</strong> onto disk.</p>");
		}

		/// <summary>
		/// Pass request into remote SocketData and saves incoming bytes to file.
		/// </summary>
		class FilteredData : SocketData
		{
			private FileStream file;
			private SocketData remote;
				
			public FilteredData (SocketData remote)
			{
				this.remote = remote;
				
				string path = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.MyComputer), "Saver");
				DateTime now = DateTime.Now;
				path = Path.Combine (path, now.ToShortDateString () + " " + now.ToShortTimeString () + " " + new Random ().Next ());
				file = new FileStream (path, FileMode.Create);
			}
		}
	}
}
