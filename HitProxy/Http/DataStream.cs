using System;
using System.IO;
using System.Net.Sockets;

namespace HitProxy.Http
{
	/// <summary>
	/// This stream will pass everything transparently except the Close.
	/// 
	/// </summary>
	public class DataStream : Stream
	{
		readonly NetworkStream backend;
		
		public DataStream (NetworkStream ns)
		{
			backend = ns;
		}

		#region implemented abstract members of System.IO.Stream
		public override void Flush ()
		{
			backend.Flush ();
		}

		public override int Read (byte[] buffer, int offset, int count)
		{
			return backend.Read (buffer, offset, count);
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
			backend.Write(buffer, offset, count);
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
				return backend.Position;
			}
			set {
				backend.Position = value;
			}
		}
		
		public override void Close ()
		{
			//Block close to allow Keep alive
		}
		#endregion
	}
}

