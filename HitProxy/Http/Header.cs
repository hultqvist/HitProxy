using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using HitProxy.Connection;
using System.Text;

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

		public readonly Flags Flags = new Flags ();
		
		/// <summary>
		/// Chain of filters applied
		/// </summary>
		public Stream DataStream { get; set; }
		
		protected Header (NetworkStream datastream)
		{
			if(datastream != null)
				this.DataStream = new DataStream (datastream);
		}

		public virtual void Dispose ()
		{
			DataStream.NullSafeDispose ();
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

		/// <summary>
		/// Return the http headers read from the socket in a single string.
		/// This data can be sent directly to Header.Parse(string).
		/// </summary>
		/// <returns>
		/// Complete http headers.
		/// </returns>
		public string ReadHeader ()
		{
			byte[] header = new byte[16 * 1024];
			int index = 0;
			int nlcount = 0;
			byte b;
			
			while (true) {
				int received = DataStream.Read (header, index, 1);
				;
				if (received != 1)
					throw new HeaderException ("ReadHeader: did not get data", HttpStatusCode.BadGateway);
				
				b = header [index];
				index += 1;
				
				if (index >= header.Length)
					throw new HeaderException ("Header too large, limit is at " + header.Length, HttpStatusCode.RequestEntityTooLarge);
				
				if (b != 0xa) {
					if (b != 0xd)
						nlcount = 0;
					continue;
				}
				//Test for end of header
				nlcount += 1;
				if (nlcount < 2 && !(nlcount == 1 && index <= 2))
					continue;
				
				//Remove last empty line
				if (header [index - 2] == 0xd)
					index -= 2;
				else
					index -= 1;
				
				return Encoding.ASCII.GetString (header, 0, index);
			}
		}

		#region Header operations

		public virtual void SendHeaders (Stream output)
		{
			//Headers
			string header = FirstLine + "\r\n";
			foreach (string line in this)
				header += line + "\r\n";
			header += "\r\n";
			byte[] buffer = System.Text.Encoding.ASCII.GetBytes (header);
			output.Write (buffer, 0, buffer.Length);
		}

		/// <summary>
		/// Remove a header from the header list
		/// </summary>
		/// <param name="header">
		/// A <see cref="System.String"/>
		/// </param>
		public void RemoveHeader (string header)
		{
			this.RemoveAll (line => {
				return line.ToLowerInvariant ().StartsWith (header.ToLowerInvariant () + ":"); });
		}

		/// <summary>
		/// Replace header in the same location
		/// </summary>
		public void ReplaceHeader (string key, string value)
		{
			string needle = key.ToLowerInvariant () + ":";
			int index;
			for (index = 0; index < this.Count; index++) {
				if (this [index].ToLowerInvariant ().StartsWith (needle)) {
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
					return header.Split (new char[] { ':' }, 2) [1].Trim ();
			}
			return null;
		}

		public List<string> GetHeaderList (string key)
		{
			List<string > headers = new List<string> ();
			
			key = key.ToLowerInvariant () + ":";
			
			foreach (string header in this) {
				if (header.ToLowerInvariant ().StartsWith (key)) {
					headers.Add (header.Split (new char[] { ':' }, 2) [1].Trim ());
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
