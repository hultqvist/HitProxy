using System;
namespace HitProxy.Connection
{
	/// <summary>
	/// Used by filters who intercept the datastream
	/// </summary>
	class FilteredData : IDataIO
	{
		private IDataFilter filter;
		private IDataInput input;
		private IDataOutput output;

		public FilteredData (IDataFilter filter, IDataInput input)
		{
			this.filter = filter;
			this.input = input;
		}

		#region IDataInput

		public void PipeTo (IDataOutput output, long length)
		{
			this.output = output;
			input.PipeTo (this, length);
			this.output = null;
		}

		public int PipeTo (IDataOutput output)
		{
			this.output = output;
			int total = input.PipeTo (this);
			this.output = null;
			return total;
		}

		#endregion

		#region DataOutput methods

		public void Send (byte[] buffer, int start, int length)
		{
			filter.Send (buffer, start, length, output);
		}

		public void EndOfData ()
		{
			filter.Send (null, 0, 0, output);
			output.EndOfData ();
		}
		
		#endregion

		public void Dispose ()
		{
			filter.Dispose ();
			input.Dispose ();
		}
	}
}

