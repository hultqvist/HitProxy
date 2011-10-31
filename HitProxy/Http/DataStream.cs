using System;
using System.IO;
using System.Net.Sockets;

namespace HitProxy.Http
{
	/// <summary>
	/// This stream will pass everything transparently except the Close.
	/// Position will be recorded locally to show bytes transferred
	/// </summary>
	public class DataStream : Stream
	{
		readonly Stream backend;
		
		public DataStream (Stream ns)
		{
			if (ns == null)
				throw new ArgumentNullException ();
			backend = ns;
		}

		public long TotalRead { get; private set; }
		
		public long TotalWritten { get; private set; }
		
		#region implemented abstract members of System.IO.Stream
		public override void Flush ()
		{
			backend.Flush ();
		}

		public override int Read (byte[] buffer, int offset, int count)
		{
			int read = backend.Read (buffer, offset, count);
			TotalRead += read;
			return read;			
		}

		public override long Seek (long offset, SeekOrigin origin)
		{
			return backend.Seek (offset, origin);
		}

		public override void SetLength (long value)
		{
			backend.SetLength (value);
		}

		public override void Write (byte[] buffer, int offset, int count)
		{
			backend.Write (buffer, offset, count);
			TotalWritten += count;
		}

		public override bool CanRead {
			get {
				return backend.CanRead;
			}
		}

		public override bool CanSeek {
			get {
				return backend.CanSeek;
			}
		}

		public override bool CanWrite {
			get {
				return backend.CanWrite;
			}
		}

		public override long Length {
			get {
				return backend.Length;
			}
		}

		public override long Position {
			get {
				throw new NotSupportedException ();
			}
			set {
				throw new NotSupportedException ();
			}
		}
		
		public override void Close ()
		{
			//Block close to allow Keep alive
		}
		
		#endregion
	}
}

