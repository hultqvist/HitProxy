using System;
using System.Text;
using System.IO;

namespace HitProxy.Connection
{
	public class ChunkedOutput : Stream
	{
		readonly Stream output;
		
		public ChunkedOutput (Stream output)
		{
			this.output = output;
		}

		public override void Flush ()
		{
			output.Flush ();
		}

		public override int Read (byte[] buffer, int offset, int count)
		{
			throw new InvalidOperationException ();
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
			if (count == 0)
				return;
			
			string header = count.ToString ("X") + "\r\n";
			byte[] cHeader = Encoding.ASCII.GetBytes (header);
			output.Write (cHeader, 0, cHeader.Length);
			
			output.Write (buffer, offset, count);
			
			output.Write (new byte[] { 0xd, 0xa }, 0, 2);
		}

		public override bool CanRead { get { return false; } }

		public override bool CanSeek { get { return false; } }

		public override bool CanWrite { get { return true; } }

		public override long Length {
			get {
				throw new NotImplementedException ();
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

		bool closed = false;
		
		public override void Close ()
		{
			if (closed)
				return;
			closed = true;
			
			byte[] cHeader = Encoding.ASCII.GetBytes ("0\r\n\r\n");
			output.Write (cHeader, 0, cHeader.Length);
			output.Close ();	
		}
	}
}

