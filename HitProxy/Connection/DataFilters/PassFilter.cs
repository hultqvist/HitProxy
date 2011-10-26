using System;

namespace HitProxy.Connection
{
	/// <summary>
	/// Pass through all data unmodified
	/// </summary>
	public class PassFilter : IDataFilter
	{
		public void Send (byte[] inBuffer, int start, int inLength, IDataOutput output)
		{
			output.Send (inBuffer, start, inLength);
		}

		public void EndOfData (IDataOutput output)
		{
			output.EndOfData ();
		}
		
		public void Dispose ()
		{
		}
	}
}

