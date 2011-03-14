using System;
namespace HitProxy.Connection
{
	/// <summary>
	/// This interface is used to make it possible to intercept data streams.
	/// </summary>
	public interface IDataOutput
	{
		void Send (byte[] buffer, int start, int length);
		void EndOfData();
	}
}

