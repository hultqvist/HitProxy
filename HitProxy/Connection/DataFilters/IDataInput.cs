using System;
namespace HitProxy.Connection
{
	public interface IDataInput : IDisposable
	{		
		int PipeTo (IDataOutput output);
		void PipeTo (IDataOutput output, long length);
	}
}

