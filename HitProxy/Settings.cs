using System;
using ProtoBuf;
using System.Collections.Generic;
namespace HitProxy
{

	[ProtoContract]
	public class Settings
	{
		[ProtoMember(1)]
		public List<string> Active { get; set; }

		public Settings ()
		{
			this.Active = new List<string> ();
		}
	}
	
}

