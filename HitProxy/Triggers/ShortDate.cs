using System;
using HitProxy.Http;
using System.Globalization;

namespace HitProxy.Triggers
{
	public class ShortDate : Trigger
	{
		public ShortDate ()
		{
		}
		
		public override bool Apply (Request request)
		{
			if (request.Response == null)
				return false;
			
			Response r = request.Response;
			string expireString = r.GetHeader ("Expires");
			if (expireString == null)
				return false;
			DateTime expire = DateTime.Parse (expireString, CultureInfo.InvariantCulture.DateTimeFormat);
			
			if ((expire - DateTime.Now).TotalHours < 1) {
				r.Flags.Set ("shortdate");
				return true;
			}
			return false;
		}
		
		public override Html Status ()
		{
			return Html.Format ("Sets the <strong>shortdate</strong> flag to every response with an expire date shorter than 1 hour");
		}
	}
}

