using System;
using System.Net;

namespace HitProxy.Http
{
	public static class HtmlTemplate
	{
		public static Html Message (HttpStatusCode code, string title, Html htmlContents)
		{
			return Html.Format (@"<!DOCTYPE html>
<html>
<head>
	<meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"" />
	<link rel=""stylesheet"" type=""text/css"" href=""http://{0}/style.css"" />
	<title>{2} - HitProxy</title>
</head>
<body class=""{1}"">
	<h1>{2}</h1>
	{3}
</body>
</html>", Filters.WebUI.ConfigHost, code, title, htmlContents);
		}

	}
}

