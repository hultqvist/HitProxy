using System;
namespace HitProxy.Connection
{
	/// <summary>
	/// Used by filters who 
	/// </summary>
	class FilteredData : SocketData, IDataOutput
	{
		private IDataFilter filter;
		private SocketData remote;
		private IDataOutput output;

		public FilteredData (IDataFilter filter, SocketData remote)
		{
			this.filter = filter;
			this.remote = remote;
		}

		#region SocketData Methods

		public override int Received {
			get { return remote.Received; }
			set { remote.Received = value; }
		}

		public override void PipeTo (IDataOutput output, long length)
		{
			this.output = output;
			remote.PipeTo (this, length);
		}

		public override void PipeTo (IDataOutput output)
		{
			this.output = output;
			remote.PipeTo (this);
		}

		public override string SendChunkedResponse (IDataOutput output)
		{
			byte[] message = System.Text.ASCIIEncoding.ASCII.GetBytes ("Not yet implemented saving chunked response.");
			filter.Send (message, message.Length, nullOutput);
			Console.Error.WriteLine ("Not yet implemented saving chunked response.");
			
			return remote.SendChunkedResponse (output);
		}

		#endregion

		#region DataOutput methods

		public void Send (byte[] buffer)
		{
			filter.Send (buffer, buffer.Length, output);
		}

		public void Send (byte[] buffer, int length)
		{
			filter.Send (buffer, length, output);
		}

		#endregion

		public override void Dispose ()
		{
			base.Dispose ();
			filter.Dispose ();
			remote.Dispose ();
		}


		#region NullOutput

		private readonly NullOutput nullOutput = new NullOutput ();

		class NullOutput : IDataOutput
		{
			public void Send (byte[] buffer)
			{
				return;
			}
			public void Send (byte[] buffer, int length)
			{
				return;
			}
		}
		
		#endregion
		
	}
}

