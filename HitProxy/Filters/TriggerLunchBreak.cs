using System;
using HitProxy.Http;

namespace HitProxy.Filters
{
	/// <summary>
	/// This triggers on special time of day.
	/// </summary>
	public class TriggerLunchBreak : Trigger
	{
		public override bool Apply (Request request)
		{
			TimeSpan now = DateTime.Now.TimeOfDay;
			if (now.Hours < 12 || now.Hours > 13)
				return false;
			else
				return true;
		}
	}
}
