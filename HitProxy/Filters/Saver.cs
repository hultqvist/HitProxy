using System;
using HitProxy.Http;
using HitProxy.Connection;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace HitProxy.Filters
{
	/// <summary>
	/// Save files with flag "save" onto disk
	/// </summary>
	public class Saver : Filter
	{
		List<FileOutput> savings = new List<FileOutput> ();

		public override bool Apply (Request request)
		{
			if (request.Response == null)
				return false;
			if ((request.Flags["save"] || request.Response.Flags["save"]) == false)
				return false;
			
			//Intercept data connection
			FileOutput save = new FileOutput (request, this);
			lock (savings) {
				savings.Add (save);
			}
			request.Response.FilterData (save);
			
			return true;
		}

		public override Html Status (NameValueCollection httpGet, Request request)
		{
			Html html = Html.Format (@"<p>Saves request data with flag <strong>save</strong> onto disk.</p><ul>");
			lock (savings) {
				foreach (FileOutput fo in savings) {
					html += Html.Format (@"<li>{0}</li>", fo);
				}
			}
			
			return html + Html.Format ("</ul>");
		}


		class FileOutput : IDataFilter
		{
			FileStream file;
			string path;
			Saver saver;

			public FileOutput (Request request, Saver saver)
			{
				try {
					path = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments), "Downloads");
					Directory.CreateDirectory (path);
					DateTime now = DateTime.Now;
					path = Path.Combine (path, now.ToShortDateString () + " " + now.ToLongTimeString () + " ");
					path += request.Uri.Host + " " + new Random ().Next (100) + " " + Path.GetFileName (request.Uri.AbsolutePath);
					
					file = new FileStream (path, FileMode.Create);
				} catch (Exception) {
					file.NullSafeDispose ();
					file = null;
				}
				
				this.saver = saver;
			}

			public void Send (byte[] inBuffer, int start, int inLength, IDataOutput output)
			{
				try {
					if (file != null)
						file.Write (inBuffer, start, inLength);
				}
				catch (Exception) {}
				output.Send (inBuffer, start, inLength);
			}

			public override string ToString ()
			{
				return path;
			}

			public void Dispose ()
			{
				file.NullSafeDispose ();
				lock (saver.savings) {
					saver.savings.Remove (this);
				}
			}
		}
	}
}
