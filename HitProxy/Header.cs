
using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace HitProxy
{
	/// <summary>
	/// Both http request and response headers
	/// </summary>
	public abstract class Header : List<string>, IDisposable
	{
		/// <summary>
		/// Return and parse the First line in a http header.
		/// </summary>
		public abstract string FirstLine { get; set; }

		public SocketData DataSocket {
			get { return socketdata; }
			set {
				socketdata.NullSafeDispose ();
				socketdata = value;
			}
		}
		private SocketData socketdata;

		protected Header ()
		{
		}

		public virtual void Dispose ()
		{
			DataSocket.NullSafeDispose ();
		}

		protected void Parse (string header)
		{
			TextReader reader = new StringReader (header);
			FirstLine = reader.ReadLine ();
			while (true) {
				string line = reader.ReadLine ();
				
				if (line == null)
					break;
				
				if (line == "")
					continue;
				
				Add (line);
			}
		}

		public virtual void SendHeaders (Socket socket)
		{
			//Headers
			string header = FirstLine + "\r\n";
			foreach (string line in this)
				header += line + "\r\n";
			header += "\r\n";
			byte[] buffer = System.Text.Encoding.ASCII.GetBytes (header);
			socket.Send (buffer);
		}

		/// <summary>
		/// Remove a header from the header list
		/// </summary>
		/// <param name="header">
		/// A <see cref="System.String"/>
		/// </param>
		public void RemoveHeader (string header)
		{
			this.RemoveAll (line => { return line.ToLowerInvariant ().StartsWith (header.ToLowerInvariant () + ":"); });
		}

		/// <summary>
		/// </summary>
		public void ReplaceHeader (string key, string value)
		{
			string needle = key.ToLowerInvariant () + ":";
			int index;
			for (index = 0; index < this.Count; index++) {
				if (this[index].ToLowerInvariant ().StartsWith (needle)) {
					this.RemoveAt (index);
					this.Insert (index, key + ": " + value);
					
					return;
				}
			}
		}

		/// <summary>
		/// Return value for given key,
		/// null if key is missing
		/// </summary>
		public string GetHeader (string key)
		{
			key = key.ToLowerInvariant () + ":";
			
			foreach (string header in this) {
				if (header.ToLowerInvariant ().StartsWith (key))
					return header.Split (new char[] { ':' }, 2)[1].Trim ();
			}
			return null;
		}

		/// <summary>
		/// Replace and/or add a header
		/// null if key is missing
		/// </summary>
		public void SetHeader (string key, string value)
		{
			RemoveHeader (key);
			Add (key + ": " + value);
		}
	}
}