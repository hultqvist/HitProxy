using System;
namespace HitProxy.Connection
{
	public interface IDataInput : IDisposable
	{		
		void PipeTo (IDataOutput output);
		void PipeTo (IDataOutput output, long length);
	}
}

