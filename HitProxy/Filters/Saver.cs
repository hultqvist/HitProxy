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
			if ((request.TestClass ("save") || request.Response.TestClass("save")) == false)
				return false;
			
			//Intercept data connection
			request.Response.FilterData(new FileOutput());
			
			return true;
		}

		public override Html Status ()
		{
			return Html.Format (@"<p>Saves request data with class <strong>save</strong> onto disk.</p>");
		}

		
		class FileOutput : IDataFilter
		{
			private FileStream file;

			public FileOutput ()
			{
				string path = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.MyComputer), "Saver");
				DateTime now = DateTime.Now;
				path = Path.Combine (path, now.ToShortDateString () + " " + now.ToShortTimeString () + " " + new Random ().Next ());
				file = new FileStream (path, FileMode.Create);
			}

			public void Send (byte[] inBuffer, int inLength, IDataOutput output)
			{
				file.Write (inBuffer, 0, inLength);
				output.Send (inBuffer, inLength);
			}
			
			public void Dispose ()
			{
				file.Close ();
			}
		}
	}
}
