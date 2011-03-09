using System;
namespace HitProxy.Connection
{
	public interface IDataFilter : IDisposable
	{
		/// <summary>
		/// This call is made to the filter with remote incoming data in the buffer.
		/// The filter then send its output to output.
		/// </summary>
		void Send (byte[] inBuffer, int inLength, IDataOutput output);
	}
}

