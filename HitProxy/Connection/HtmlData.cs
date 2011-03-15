using System;
using HitProxy.Http;
using System.Text;
namespace HitProxy.Connection
{
	public class HtmlData : IDataIO
	{
		byte[] buffer;

		public HtmlData (Html html)
		{
			buffer = Encoding.UTF8.GetBytes (html.HtmlString);
		}

		public int Length {
			get { return buffer.Length; }
		}

		#region IDataInput
		
		public int PipeTo (IDataOutput output)
		{
			if (buffer == null)
				throw new InvalidOperationException ("Buffer already sent");
			
			int total = buffer.Length;
			output.Send (buffer, 0, buffer.Length);
			buffer = null;
			return total;
		}

		public void PipeTo (IDataOutput output, long length)
		{
			if (buffer.Length != length)
				throw new InvalidOperationException ("Must send entire buffer in one go");
			
			PipeTo (output);
		}

		#endregion
		
		#region IDataOutput
		
		public void Send (byte[] buffer, int start, int length)
		{
			throw new InvalidOperationException ("Can only read from HtmlData");
		}

		public void EndOfData ()
		{
			throw new InvalidOperationException ();
		}
		
		#endregion
		
		public void Dispose ()
		{
			//nothing to dispose
		}
	}
}

