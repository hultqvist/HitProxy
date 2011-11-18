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
		List<FileSaver> savings = new List<FileSaver> ();

		public override bool Apply (Request request)
		{
			if (request.Response == null)
				return false;
			if ((request.Flags ["save"] || request.Response.Flags ["save"]) == false)
				return false;
			
			//Intercept data connection
			FileSaver save = new FileSaver (request, this, request.Response.Stream);
			lock (savings) {
				savings.Add (save);
			}
			request.Response.Stream = save;
			
			return true;
		}

		public override Response Status (NameValueCollection httpGet, Request request)
		{
			Html html = Html.Format (@"<p>Saves request data with flag <strong>save</strong> onto disk.</p><ul>");
			lock (savings) {
				foreach (FileSaver fo in savings) {
					html += Html.Format (@"<li>{0}</li>", fo);
				}
			}
			return WebUI.ResponseTemplate (ToString (), html + Html.Format ("</ul>"));
		}

		/// <summary>
		/// Save the data read into a separate file
		/// </summary>
		class FileSaver : Stream
		{
			FileStream file;
			string path;
			Saver saver;
			Stream input;
			
			public FileSaver (Request request, Saver saver, Stream input)
			{
				try {
					path = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments), "Downloads");
					Directory.CreateDirectory (path);
					DateTime now = DateTime.Now;
					path = Path.Combine (path, now.ToShortDateString () + " " + now.ToLongTimeString () + " ");
					path += request.Uri.Host + " " + new Random ().Next (100) + " " + Path.GetFileName (request.Uri.AbsolutePath);
					
					file = new FileStream (path, FileMode.Create);
					this.input = input;
				} catch (Exception) {
					file.NullSafeDispose ();
					file = null;
				}
				
				this.saver = saver;
			}

			#region implemented abstract members of System.IO.Stream
			public override void Flush ()
			{
				throw new NotImplementedException ();
			}

			public override int Read (byte[] buffer, int offset, int count)
			{
				int read = input.Read (buffer, offset, count);
				try {
					if (file != null)
						file.Write (buffer, offset, read);
				} catch (Exception) {
				}
				return read;
			}

			public override long Seek (long offset, SeekOrigin origin)
			{
				throw new NotImplementedException ();
			}

			public override void SetLength (long value)
			{
				throw new NotImplementedException ();
			}

			public override void Write (byte[] buffer, int offset, int count)
			{
				throw new NotImplementedException ();
			}

			public override bool CanRead { get { return true; } }

			public override bool CanSeek { get { return false; } }

			public override bool CanWrite { get { return false; } }

			public override long Length { get { return input.Length; } }

			public override long Position {
				get { return input.Position; }
				set { throw new NotImplementedException (); }
			}

			#endregion			
			
			protected override void Dispose (bool disposing)
			{
				file.NullSafeDispose ();
				lock (saver.savings) {
					saver.savings.Remove (this);
				}
			}
		}
	}
}
