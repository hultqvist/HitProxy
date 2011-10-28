using System;
using HitProxy.Http;
using System.Net;
using System.Text;
using System.IO;

namespace HitProxy.Connection
{
	public class ChunkedInput : Stream
	{
		readonly Stream input;

		public ChunkedInput (Stream input)
		{
			this.input = input;
		}

		/// <summary>
		/// Read headers in a chunked encoding
		/// Return the number of bytes following in the next chunk
		/// </summary>
		int ReadChunkedHeader ()
		{
			byte[] header = new byte[30];
			int index = 0;
			while (true) {
				if (index >= header.Length)
					throw new HeaderException ("Chunked header is too large", HttpStatusCode.BadGateway);
				
				//Read one byte
				int read = input.Read (header, index, 1);
				if (read == 0)
					throw new EndOfStreamException ("While reading chunked header");
				
				//Skip leading space
				if (index == 0) {
					if (header [index] == 0x20)
						continue;
				}
				
				index++;
				
				if (index > 2 && header [index - 1] == 0xa) {
					string hex = Encoding.ASCII.GetString (header, 0, index);
					int length = int.Parse (hex, System.Globalization.NumberStyles.HexNumber);
					
					if (length == 0) {
						//Skip remaining 2 cr/nl
						byte[] end = new byte[2];
						if (input.Read (end, 0, 2) != 2) {
							throw new InvalidDataException ();
						}
						if (end [0] != 13 || end [1] != 10)
							throw new InvalidDataException ();
					}
						
					return length;
				}
			}
		}

		#region implemented abstract members of System.IO.Stream
		public override void Flush ()
		{
			throw new NotImplementedException ();
		}

		int chunkLeft = 0;
		
		public override int Read (byte[] buffer, int offset, int count)
		{
			if (chunkLeft == 0) {
				chunkLeft = ReadChunkedHeader ();
#if DEBUG
				Console.WriteLine("Got chunked chunk: " + chunkLeft);
#endif
				if (chunkLeft == 0)
					return 0;
			}
				
			int toread = count;
			if (toread > chunkLeft)
				toread = chunkLeft;
				
			int read = input.Read (buffer, offset, toread);
			if (read == 0)
				throw new EndOfStreamException ();
			offset += read;
			count -= read;
			chunkLeft -= read;
			return read;
		}
		
		public override long Seek (long offset, SeekOrigin origin)
		{
			throw new InvalidOperationException ();
		}

		public override void SetLength (long value)
		{
			throw new InvalidOperationException ();
		}

		public override void Write (byte[] buffer, int offset, int count)
		{
			throw new InvalidOperationException ();
		}

		public override bool CanRead { get { return true; } }

		public override bool CanSeek { get { return false; } }

		public override bool CanWrite { get { return false; } }

		public override long Length {
			get {
				throw new InvalidOperationException ();
			}
		}

		public override long Position {
			get {
				throw new NotImplementedException ();
			}
			set {
				throw new InvalidOperationException ();
			}
		}
		
		public override void Close ()
		{
			input.Close ();
		}
		#endregion
	}
}

