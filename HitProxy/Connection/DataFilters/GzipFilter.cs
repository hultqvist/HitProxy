using System;
using HitProxy.Connection;
using System.IO;
using System.IO.Compression;

namespace HitProxy.Http
{
	/// <summary>
	/// Decompress the incoming data, send it to filter and then compress the result back to the output
	/// </summary>
	public class GzipFilter : IDataFilter
	{
		IDataFilter filter;
		
		public GzipFilter (IDataFilter filter)
		{
			this.filter = filter;
			//Issue: headers are already sent and the new data length will most likely differ.
			throw new NotImplementedException("Does not work");
		}
		
		MemoryStream inStream = new MemoryStream ();
		
		public void Send (byte[] inBuffer, int start, int inLength, IDataOutput output)
		{
			inStream.Write (inBuffer, start, inLength);
		}
		
		public void EndOfData (IDataOutput output)
		{
			inStream.Seek (0, SeekOrigin.Begin);
			
			GzipOutput gzipOut = new GzipOutput (output);
			
			GZipStream gzip = new GZipStream (inStream, CompressionMode.Decompress);
			byte[] buffer = new byte[1024];
			while(true)
			{
				int read = gzip.Read(buffer, 0, buffer.Length);
				if(read == 0)
					break;
				filter.Send(buffer, 0, read, gzipOut);
			}
			inStream.Dispose ();
			filter.EndOfData (gzipOut);		
		}
		
		public void Dispose ()
		{
			
		}
		
		class GzipOutput : IDataOutput
		{
			IDataOutput output;
			
			public GzipOutput (IDataOutput output)
			{
				this.output = output;
			}
			
			MemoryStream inStream = new MemoryStream ();
			
			public void Send (byte[] buffer, int start, int length)
			{
				inStream.Write (buffer, start, length);
			}
			
			public void EndOfData ()
			{
				inStream.Seek (0, SeekOrigin.Begin);
				byte[] inBytes = inStream.ToArray ();
				inStream.Dispose ();
			
				MemoryStream ms = new MemoryStream ();
				GZipStream gzip = new GZipStream (ms, CompressionMode.Compress);
				gzip.Write (inBytes, 0, inBytes.Length);
				gzip.Close ();
			
				byte[] outBytes = ms.ToArray ();
				output.Send (outBytes, 0, outBytes.Length);
				output.EndOfData ();
			}
		}
	}
}

