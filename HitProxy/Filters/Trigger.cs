
using System;

namespace HitProxy.Filters
{
	/// <summary>
	/// Base class for filters that do not modify the request.
	/// These analyze the request and return Filtered to trigger other filters
	/// </summary>
	public abstract class Trigger : Filter
	{
	}
}
