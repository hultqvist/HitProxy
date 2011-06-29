using System;
using System.Net;
using System.Web;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using HitProxy.Http;
using HitProxy.Connection;
using HitProxy.Session;

namespace HitProxy.Filters
{

	/// <summary>
	/// This is the Web User-Interface to view status
	/// and control the filters in the proxy
	/// </summary>
	public class WebUI : Filter
	{
		public static readonly string ConfigHost = "hit.silentorbit.com";
		public static WebUI webUI;

		readonly Proxy proxy;
		readonly ConnectionManager connectionManager;

		public WebUI (Proxy proxy, ConnectionManager connectionManager)
		{
			this.proxy = proxy;
			this.connectionManager = connectionManager;
			
			if (webUI != null)
				throw new InvalidOperationException ("There can only be one WebUI");
			webUI = this;
		}

		public override bool Apply (Request request)
		{
			bool direct = request.Uri.IsLoopback && request.Uri.Port == proxy.Port;
			//New default address and Legacy address
			bool webUI = (request.Uri.Host == ConfigHost || request.Uri.Host == "hit.endnode.se");
			
			if ((request.Uri.IsAbsoluteUri == true) && (webUI == false) && (direct == false))
				return false;
			
			string[] path = request.Uri.AbsolutePath.Split ('/');
			if (path.Length < 2)
				path = new string[] { "", "" };
			
			NameValueCollection httpGet = HttpUtility.ParseQueryString (request.Uri.Query);
			
			switch (path[1]) {
			case "Session":
				request.Response = SessionPage (path, httpGet);
				break;
			case "Connection":
				request.Response = ConnectionPage (path, httpGet);
				break;
			case "Filters":
			case "RequestFilter":
			case "RequestTrigger":
			case "ResponseFilter":
			case "ResponseTrigger":
				request.Response = new Response (HttpStatusCode.OK);
				FiltersPage (path, httpGet, request);
				break;
			case "style.css":
				request.Response = new Response (HttpStatusCode.OK);
				Html data = new Html ();
				string configPath = ConfigPath ("style.css");
				if (File.Exists (configPath) == false)
					configPath = Path.Combine (Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location), "style.css");
				if (File.Exists (configPath))
					data = Html.Format (File.ReadAllText (configPath));
				request.Response.SetData (data);
				request.Response.ReplaceHeader ("Content-Type", "text/css");
				break;
			case "favicon.ico":
				request.Response = new BlockedResponse ("No favicon");
				break;
			default:
				request.Response = MainPage (request, httpGet);
				break;
			}
			
			if (httpGet.Count > 0) {
				if (request.Response.GetHeader ("Location") == null) {
					request.Response = new Response (HttpStatusCode.Found);
					string location = "http://" + request.Uri.Host;
					if (request.Uri.IsDefaultPort == false)
						location += ":" + request.Uri.Port;
					request.Response.ReplaceHeader ("Location", location + request.Uri.AbsolutePath);
				}
			}
			request.Response.KeepAlive = true;
			
			return true;
		}

		private void Template (Response response, string title, Html html)
		{
			Html menu = Html.Format (@"<ul class=""menu"">
	<li><a href=""/"">About</a></li>
	<li><a href=""/Session/"">Session</a></li>
	<li><a href=""/Connection/"">Connection</a></li>
	<li><a href=""/Filters/"">Filters</a></li>
</ul>");
			response.Template (title, menu + html);
		}

		private Response MainPage (Request request, NameValueCollection httpGet)
		{
			Response response = new Response (HttpStatusCode.OK);
			
			Html data = new Html ();
			if (request.Uri.IsLoopback) {
				data += Html.Format (@"
<p>Your proxy is running but you are currently accessing it directly via the localhost address.</p>
<p>Try to visit it via <a href=""http://{0}"">proxy mode</a>.</p>
<p>If it did not work you must first configure you proxy settings.</p>
<p>Set them: host=localhost, port={1}</p>", ConfigHost, proxy.Port);
			}
			
			if (proxy.Browser.CanSetProxy) {
				if (httpGet["active"] == "true")
					proxy.Browser.Enabled = true;
				if (httpGet["active"] == "false")
					proxy.Browser.Enabled = false;
				
				data += Html.Format ("<h1>Browser Proxy Status</h1>");
				if (proxy.Browser.Enabled)
					data += Html.Format (@"<p><strong>Enabled</strong> <a href=""?active=false"">Disable proxy settings</a></p>");
				else
					data += Html.Format (@"<p><strong>Disabled</strong> <a href=""?active=true"">Enable proxy settings</a></p>");
			}
			
			Template (response, "HitProxy", data);
			return response;
		}

		private Response SessionPage (string[] path, NameValueCollection httpGet)
		{
			Response response = new Response (HttpStatusCode.OK);
			
			int closeID;
			int.TryParse (httpGet["close"], out closeID);
			
			int showID;
			int.TryParse (httpGet["show"], out showID);
			
			ProxySession[] sessionList = proxy.ToArray ();
			
			ProxySession showSession = null;
			foreach (ProxySession session in sessionList) {
				if (session.GetHashCode () == showID) {
					showSession = session;
					break;
				}
			}
			
			foreach (ProxySession session in sessionList) {
				if (closeID == session.GetHashCode ()) {
					session.Stop ();
				}
			}
			
			Html data;
			if (showSession == null)
				data = SessionList (sessionList);
			else
				data = SessionStatus (showSession);
			
			Template (response, "Session", data);
			
			return response;
		}

		private Html SessionStatus (HitProxy.Session.ProxySession session)
		{
			Html data = new Html ();
			;
			
			Request req = session.request;
			Response resp = null;
			if (req != null)
				resp = req.Response;
			
			data += Html.Format (@"
<p>Status: {0}</p>
<p>Served {1}</p>
<p>Close: <a href=""?close={2}"">close</a></p>", session.Status, session.served, session.GetHashCode ());
			
			data += Html.Format ("<h2>Request/Client</h2>");
			if (req != null)
				data += RequestData (req);
			
			data += Html.Format ("<h2>Response/Remote</h2>");
			if (resp != null)
				data += HeaderData (resp);
			
			return data;
		}

		private string RequestData (Request req)
		{
			string data = "";
			if (req != null) {
				data += "<p>Request: <a href=\"" + req.Uri + "\">" + req.Uri.Scheme + "://" + req.Uri.Host + (req.Uri.IsDefaultPort ? "" : ":" + req.Uri.Port) + "/</a></p>";
				data += "<p>" + ((int)(DateTime.Now - req.Start).TotalSeconds) + " s ago</p>";
				if (req.DataSocket.Received > 0)
					data += "Sent " + (req.DataSocket.Received / 1000) + " Kbytes";
				if (req.Response != null && req.Response.DataSocket != null && req.Response.DataSocket.Received > 0)
					data += " Recv " + (req.Response.DataSocket.Received / 1000) + " Kbytes";
				
				data += HeaderData (req);
			} else
				
				data += "Waiting for request";
			
			return "<div>" + data + "</div>";
		}
		private Html HeaderData (Header header)
		{
			Html data = new Html ();
			foreach (string h in header)
				data += Html.Format ("<li>{0}</li>", h);
			return Html.Format ("<ul>{0}</ul>", data);
		}

		private Html SessionList (Session.ProxySession[] sessionList)
		{
			Html data = new Html ();
			foreach (ProxySession session in sessionList) {
				
				data += Html.Format (@"
<li>
	<p>
		<a href=""?show={0}"">Session</a>: {1} requests served
		<a href=""?close={0}"">close</a> {2}
	</p>", session.GetHashCode (), session.served, session.Status);
				
				Request req = session.request;
				if (req != null) {
					Response resp = req.Response;
					
					data += Html.Format (@"<p>Request: {0} <a href=""{1}"">{2}://{3}</a>", req.Method, req.Uri, req.Uri.Scheme, req.Uri.Host + (req.Uri.IsDefaultPort ? "" : ":" + req.Uri.Port));
					data += " " + ((int)(DateTime.Now - req.Start).TotalSeconds) + " s";
					if (req.DataSocket.Received > 0)
						data += "Sent: " + (req.DataSocket.Received / 1000) + " Kbytes";
					data += Html.Format ("</p>");
					if (resp != null && resp.DataSocket != null) {
						data += Html.Format ("<p>Response: ") + ((int)resp.HttpCode) + " " + resp.HttpCode;
						if (resp.DataSocket.Received > 0)
							data += " Recv: " + (resp.DataSocket.Received / 1000) + " Kbytes";
						if (resp.HasBody)
							data += " Total: " + (resp.ContentLength / 1000) + " Kbytes"; else if (resp.Chunked)
							data += " Total: chunked";
						else
							data += " Total: unknown";
						data += Html.Format ("</p>");
					}
				}
				data += Html.Format ("</li>");
			}
			data = Html.Format ("<ul>{0}</ul>", data);
			
			return data;
		}

		private Response ConnectionPage (string[] path, NameValueCollection httpGet)
		{
			Response response = new Response (HttpStatusCode.OK);
			
			Html data = Html.Format ("<h2>Remote connections</h2>");
			foreach (CachedServer s in connectionManager.ServerArray) {
				data += PrintCachedServer (s);
			}
			
			Template (response, "Connections", data);
			return response;
		}

		Html PrintCachedServer (CachedServer server)
		{
			Html data = Html.Format ("<h3>{0}</h3>", server.endpoint);
			foreach (CachedConnection c in server.Connections) {
				if (c.Busy)
					data += Html.Format (" <span style=\"background: green;\">busy</span> ") + c.served;
				else
					data += Html.Format (" <span style=\"background: gray;\">free</span> ") + c.served;
			}
			return data;
		}

		static internal string FilterUrl ()
		{
			return "http://" + ConfigHost + "/Filters/";
		}

		static internal string FilterUrl (Filter filter)
		{
			string path = "http://" + ConfigHost + "/";
			
			if (webUI.proxy.RequestFilters.Contains (filter))
				path += "RequestFilter/";
			if (webUI.proxy.RequestTriggers.Contains (filter as Trigger))
				path += "RequestTrigger/";
			if (webUI.proxy.ResponseFilters.Contains (filter))
				path += "ResponseFilter/";
			if (webUI.proxy.ResponseTriggers.Contains (filter as Trigger))
				path += "ResponseTrigger/";
			return path + filter.GetType ().Name + "/";
			;
		}

		private void FiltersPage (string[] path, NameValueCollection httpGet, Request request)
		{
			Response response = request.Response;
			Html data = new Html ();
			string page = path[1].ToLowerInvariant ();
			
			if (page == "filters" || path.Length < 3) {
				//Add and remove commands
				try {
					ActivateFilter (httpGet);
				} catch (Exception e) {
					data += Html.Format ("<p><strong>Error:</strong> ") + e.Message + Html.Format ("</p>");
					Console.Error.WriteLine ("WebUI, Filter error: " + e.Message);
				}
				
				data += Html.Format ("<h2>Request Triggers</h2>");
				data += ListFilters (proxy.RequestTriggers);
				data += Html.Format ("<h2>Request Filters</h2>");
				data += ListFilters (proxy.RequestFilters);
				
				data += Html.Format ("<h2>Response Triggers</h2>");
				data += ListFilters (proxy.ResponseTriggers);
				data += Html.Format ("<h2>Response Filters</h2>");
				data += ListFilters (proxy.ResponseFilters);
				
				Template (response, "Filters", data);
				return;
			}
			
			Filter f = null;
			if (page == "requesttrigger")
				f = Find (proxy.RequestTriggers, path[2]);
			if (page == "requestfilter")
				f = Find (proxy.RequestFilters, path[2]);
			if (page == "responsetrigger")
				f = Find (proxy.ResponseTriggers, path[2]);
			if (page == "responsefilter")
				f = Find (proxy.ResponseFilters, path[2]);
			
			if (f == null) {
				response.HttpCode = HttpStatusCode.Found;
				Template (response, "Filter not found", Html.Format (@"<p><a href=""{0}"">back</a></p>", FilterUrl ()));
				response.ReplaceHeader ("Location", FilterUrl ());
				return;
			}
			Template (response, f.ToString (), f.Status (httpGet, request));
			return;
		}

		#region Filter Management

		private void ActivateFilter (NameValueCollection keys)
		{
			string args = keys["active"];
			if (args == null)
				return;
			
			foreach (Filter f in proxy.RequestTriggers)
				if (f.Name == args)
					f.Active = !f.Active;
			foreach (Filter f in proxy.RequestFilters)
				if (f.Name == args)
					f.Active = !f.Active;
			foreach (Filter f in proxy.ResponseTriggers)
				if (f.Name == args)
					f.Active = !f.Active;
			foreach (Filter f in proxy.ResponseFilters)
				if (f.Name == args)
					f.Active = !f.Active;
			
			proxy.WriteSettings ();
		}

		Filter Find (List<Filter> filters, string path)
		{
			foreach (Filter f in filters.ToArray ()) {
				if (path == f.GetType ().Name)
					return f;
			}
			return null;
		}

		Filter Find (List<Trigger> filters, string path)
		{
			foreach (Filter f in filters.ToArray ()) {
				if (path == f.GetType ().Name)
					return f;
			}
			return null;
		}

		Filter Find (List<Filter> filters, int id)
		{
			foreach (Filter f in filters.ToArray ()) {
				if (f.GetHashCode () == id)
					return f;
			}
			return null;
		}

		private Html ListFilters (List<Filter> filters)
		{
			Html data = Html.Format ("<ul>");
			foreach (Filter f in filters.ToArray ()) {
				data += Html.Format ("<li><a href=\"{0}\">{1}</a>", FilterUrl (f), f.Name);
				if (f.Active)
					data += Html.Format (" active ");
				else
					data += Html.Format (" inactive ");
				
				data += Html.Format (" (<a href=\"{0}?active={1}\">change</a>)", FilterUrl (), f.Name);
				data += Html.Format ("</li>");
			}
			data += Html.Format ("</ul>");
			return data;
		}
		private Html ListFilters (List<Trigger> filters)
		{
			Html data = Html.Format ("<ul>");
			foreach (Filter f in filters.ToArray ()) {
				data += Html.Format ("<li><a href=\"{0}\">{1}</a>", FilterUrl (f), f.Name);
				if (f.Active)
					data += Html.Format (" active ");
				else
					data += Html.Format (" inactive ");
				
				data += Html.Format (" (<a href=\"{0}?active={1}\">change</a>)", FilterUrl (), f.Name);
				data += Html.Format ("</li>");
			}
			data += Html.Format ("</ul>");
			return data;
		}

		#endregion

		public override Html Status ()
		{
			return Html.Escape ("Web based user interface, If you can read this, the filter is working.");
		}
	}
}
