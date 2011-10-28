using System;
using HitProxy.Http;
using System.Text;
using System.IO;

namespace HitProxy.Connection
{
	public class HtmlData : Stream
	{
		byte[] htmlBuffer;

		public HtmlData (Html html)
		{
			htmlBuffer = Encoding.UTF8.GetBytes (html.HtmlString);
		}
		
		public override void Close ()
		{
			htmlBuffer = null;
		}
		
		public override void Flush ()
		{
		}

		public override int Read (byte[] buffer, int offset, int count)
		{
			long toread = count;
			if (Position + toread > Length)
				toread = Length - Position;
			
			for (int n = 0; n < toread; n++)
				buffer [offset + n] = htmlBuffer [Position + n];
			Position += toread;
			return (int)toread;
		}

		public override long Seek (long offset, SeekOrigin origin)
		{
			throw new NotImplementedException ();
		}

		public override void SetLength (long value)
		{
			throw new InvalidOperationException ("Read only");
		}

		public override void Write (byte[] buffer, int offset, int count)
		{
			throw new InvalidOperationException ("Read only");
		}

		public override bool CanRead { get { return true; } }

		public override bool CanSeek { get { return true; } }

		public override bool CanWrite { get { return false; } }

		public override long Length { get { return htmlBuffer.Length; } }

		public override long Position { get; set; }
	}
}

