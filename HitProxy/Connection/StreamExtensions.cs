using System;
using System.IO;

namespace HitProxy.Connection
{
	public static class StreamExtensions
	{
		
		/// <summary>
		/// Send all data from incoming socket to output.
		/// Buffer is assumed to be empty.
		/// </summary>
		public static int PipeTo (this Stream source, Stream output)
		{
			int total = 0;
			byte[] buffer = new byte[0x10000];
			while (true) {
				int read = source.Read (buffer, 0, buffer.Length);
				if (read == 0) {
					return total;
				}

				output.Write (buffer, 0, read);
				total += read;
			}
		}

		/// <summary>
		/// Send length bytes of data from incoming socket to output.
		/// Buffer is assumed to be empty.
		/// </summary>
		public static void PipeTo (this Stream source, Stream output, long length)
		{
			if (length <= 0)
				return;
			
			byte[] buffer = new byte[0x10000];
			int total = 0;
			while (true) {
				long toread = length - total;
				if (toread > buffer.Length)
					toread = buffer.Length;				
				
				int read = source.Read (buffer, 0, (int)toread);
				if (read == 0)
					throw new EndOfStreamException ("With " + (length - total) + " bytes left to read");
				
				output.Write (buffer, 0, read);
				total += read;
				
				if (total == length) {
					return;
				}
				if (total > length)
					throw new InvalidOperationException ("too much data read");
			}
		}
	}
}

