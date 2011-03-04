using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using HitProxy.Connection;

namespace HitProxy.Http
{
	/// <summary>
	/// Both http request and response headers
	/// </summary>
	public abstract class Header : List<string>, IDisposable
	{
		/// <summary>
		/// Return and parse the First line in a http header.
		/// </summary>
		public abstract string FirstLine { get; }

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

		protected abstract void ParseFirstLine (string firstLine);

		/// <summary>
		/// Parse complete headers
		/// </summary>
		/// <param name="header">
		/// All lines in a header
		/// </param>
		protected void Parse (string header)
		{
			TextReader reader = new StringReader (header);
			ParseFirstLine (reader.ReadLine ());
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
		/// Replace header in the same location
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
			//Add new if no previous existed
			this.Add (key + ": " + value);
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

		public List<string> GetHeaderList (string key)
		{
			List<string> headers = new List<string> ();
			
			key = key.ToLowerInvariant () + ":";
			
			foreach (string header in this) {
				if (header.ToLowerInvariant ().StartsWith (key)) {
					headers.Add (header.Split (new char[] { ':' }, 2)[1].Trim ());
				}
			}
			
			return headers;
		}

		public void AddHeader (string key, string value)
		{
			Add (key + ": " + value);
		}
		
		
		#region Trigger/Filter Classification
		
		/// <summary>
		/// Attributes set by triggers and used by filters.
		/// </summary>
		private readonly List<string> filterClass = new List<string> ();
		
		/// <summary>
		/// Add classes from a comma separated list
		/// </summary>
		/// <param name="classNames">
		/// A comma separated list of filtering classification names
		/// </param>
		public void SetClass (string classNames)
		{
			string[] fa = classNames.ToLowerInvariant().Split (',');
			foreach (string c in fa)
			{
				if (filterClass.Contains (c) == false)
					filterClass.Add (c);
			}
		}
		
		/// <summary>
		/// Test wether any of the supplied classes has been set
		/// </summary>
		/// <param name="classNames">
		/// A comma separated list of class names
		/// </param>
		/// <returns>
		/// Whether the request/response has been assigned the class
		/// </returns>
		public bool TestClass (string classNames)
		{
			string[] fa = classNames.ToLowerInvariant().Split (',');
			foreach (string c in fa) {
				if (filterClass.Contains (c))
					return true;
			}
			return false;
		}

		/// <summary>
		/// HTML to be presented on the block page.
		/// </summary>
		private readonly List<Html> filterHtml = new List<Html> ();

		public void SetTriggerHtml (Html html)
		{
			filterHtml.Add (html);
		}
		
		public List<Html> GetTriggerHtml ()
		{
			return filterHtml;
		}
		
		#endregion
	}
}
