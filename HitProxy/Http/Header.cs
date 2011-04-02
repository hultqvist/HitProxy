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

		/// <summary>
		/// The raw, outermost Data
		/// </summary>
		public readonly SocketData DataSocket;

		public readonly Flags Flags = new Flags();
		
		/// <summary>
		/// Temporary protocol filters, e.g. chunked http encoding
		/// </summary>
		public IDataIO DataProtocol {
			get {
				if (dataProtocol == null)
					return DataSocket;
				return dataProtocol;
			}
			set { dataProtocol = value; }
		}
		private IDataIO dataProtocol = null;

		/// <summary>
		/// Chain of filters applied
		/// </summary>
		public IDataIO DataFiltered {
			get {
				if (dataFiltered == null)
					return DataProtocol;
				return dataFiltered;
			}
		}
		private IDataIO dataFiltered = null;

		protected Header (SocketData dataRaw)
		{
			this.DataSocket = dataRaw;
		}

		public virtual void Dispose ()
		{
			DataSocket.NullSafeDispose ();
			DataProtocol.NullSafeDispose ();
			DataFiltered.NullSafeDispose ();
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
			Clear ();
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

		#region Data Filter

		/// <summary>
		/// Set filter to be used on data.
		/// </summary>
		public void FilterData (IDataFilter filter)
		{
			dataFiltered = new FilteredData (filter, DataFiltered);
		}

		#endregion

		#region Header operations

		public virtual void SendHeaders (IDataOutput output)
		{
			//Headers
			string header = FirstLine + "\r\n";
			foreach (string line in this)
				header += line + "\r\n";
			header += "\r\n";
			byte[] buffer = System.Text.Encoding.ASCII.GetBytes (header);
			output.Send (buffer, 0, buffer.Length);
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

		#endregion

		#region Filter HTML
		
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
