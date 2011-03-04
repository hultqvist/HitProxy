using System;
using System.Web;
namespace HitProxy.Http
{
	/// <summary>
	/// Html string representation.
	/// Using this class it safe to mix html and strings.
	/// Strings are automatically escaped when combined with html.
	/// </summary>
	public class Html
	{
		public readonly string HtmlString;

		public Html ()
		{
			this.HtmlString = "";
		}

		private Html (string rawHtml)
		{
			this.HtmlString = rawHtml;
		}
		
		public static Html Escape (string text)
		{
			return new Html (HttpUtility.HtmlEncode (text));
		}

/*		public static implicit operator Html (string text)
		{
			return new Html (HttpUtility.HtmlEncode (text));
		}
		 */
		public static explicit operator string (Html html)
		{
			return HttpUtility.HtmlDecode (html.HtmlString);
		}

		public static Html Format (string html)
		{
			return new Html (html);
		}

		public static Html Format (string html, params object[] values)
		{
			for (int n = 0; n < values.Length; n++)
			{
				if (values[n] is Html)
					html = html.Replace ("{" + n + "}", values[n].ToString ());
				else
					html = html.Replace ("{" + n + "}", HttpUtility.HtmlEncode (values[n].ToString()));
			}
			return new Html (html);
		}

		public static Html operator + (Html a, Html b)
		{
			return new Html (a.HtmlString + b.HtmlString);
		}
		
		public static Html operator + (Html a, object b)
		{
			return new Html (a.HtmlString + HttpUtility.HtmlEncode (b.ToString()));
		}
		
		public override string ToString ()
		{
			return this.HtmlString;
		}
		
		public override bool Equals (object obj)
		{
			return HtmlString.Equals (obj);
		}
		
		public override int GetHashCode ()
		{
			return HtmlString.GetHashCode ();
		}
	}
}

