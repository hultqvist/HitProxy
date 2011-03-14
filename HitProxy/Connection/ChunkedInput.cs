using System;
using HitProxy.Http;
using System.Net;
using System.Text;
namespace HitProxy.Connection
{
	public class ChunkedInput : IDataIO
	{
		readonly SocketData input;

		public ChunkedInput (SocketData input)
		{
			this.input = input;
		}

		/// <summary>
		/// Read headers in a chunked encoding
		/// Return a string with the chunk header
		/// </summary>
		string ReadChunkedHeader ()
		{
			byte[] header = new byte[30];
			int index = 0;
			while (true) {
				if (index >= header.Length)
					throw new HeaderException ("Chunked header is too large", HttpStatusCode.BadGateway);
				
				//Read one byte
				input.Receive (header, index, 1);
				
				//Skip leading space
				if (index == 0) {
					if (header[index] == 0x20)
						continue;
				}
				
				index++;
				
				if (index > 2 && header[index - 1] == 0xa) {
					return Encoding.ASCII.GetString (header, 0, index);
				}
			}
		}

		#region IDataInput

		public void PipeTo (IDataOutput output)
		{
			while (true) {
				string header = ReadChunkedHeader ();
				int length = int.Parse (header, System.Globalization.NumberStyles.HexNumber);
				
				if (length == 0) {
					//End of Stream
					output.EndOfData ();
					
					//Footer
					string footer = input.ReadHeader ();
					byte[] footerBytes = Encoding.ASCII.GetBytes (footer);
					output.Send (footerBytes, 0, footerBytes.Length);
					
					return;
				}
				
				byte[] buffer = new byte[length];
				input.Receive (buffer, 0, length);
				output.Send (buffer, 0, length);
			}
		}

		public void PipeTo (IDataOutput output, long length)
		{
			throw new InvalidOperationException ();
		}

		public void Dispose ()
		{
			input.Dispose ();
		}

		#endregion

		#region IDataOutput

		public void Send (byte[] buffer, int start, int length)
		{
			throw new InvalidOperationException ();
		}
		
		public void EndOfData ()
		{
			throw new InvalidOperationException ();
		}
		#endregion
		
	}
}

