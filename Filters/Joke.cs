using System;

namespace PersonalProxy.Filters
{
	/// <summary>
	/// Random replace image urls
	/// Inject extra search terms in search queries
	/// Inject fake referers with randomized google search pages
	/// </summary>
	public class Joke : Filter
	{

		public Joke ()
		{
		}

		public override bool Apply (Request request)
		{
			throw new System.NotImplementedException ();
		}
		
	}
}
