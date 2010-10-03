
using System;
using System.Net;
using System.Web;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace PersonalProxy.Filters
{

	/// <summary>
	/// This is the Web User-Interface to view status
	/// and control the filters in the proxy
	/// </summary>
	public class WebUI : Filter
	{
		public static readonly string ConfigHost = "pp";

		Proxy proxy;
		ConnectionManager connectionManager;

		public WebUI (Proxy proxy, ConnectionManager connectionManager)
		{
			this.proxy = proxy;
			this.connectionManager = connectionManager;
		}

		public override bool Apply (Request request)
		{
			if (request.Uri.IsAbsoluteUri == true && request.Uri.Host != ConfigHost && (request.Uri.Host == "localhost" && request.Uri.Port == proxy.Port) == false)
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
			case "Filter":
				request.Response = new Response (HttpStatusCode.OK);
				FiltersPage (path, httpGet, request);
				break;
			case "favicon.ico":
				request.Response = new BlockedResponse ("No favicon");
				break;
			default:
				request.Response = MainPage (request);
				break;
			}
			
			if (httpGet.Count > 0) {
				if (request.Response.GetHeader ("Location") == null)
				{
					request.Response = new Response (HttpStatusCode.Found);
					request.Response.SetHeader ("Location", "http://" + ConfigHost + request.Uri.AbsolutePath);
				}
			}
			request.Response.KeepAlive = true;
			
			return true;
		}

		private Response MainPage (Request request)
		{
			Response response = new Response (HttpStatusCode.OK);
			
			string data = "";
			if (request.Uri.Host == "localhost")
				data += "<p>Your proxy is running but you are currently accessing it directly via the localhost address.</p>" + "<p>Try to visit it via <a href=\"http://" + ConfigHost + "/\">proxy mode</a>.</p>" + "<p>If it did not work you must first configure you proxy settings.</p>" + "<p>Set them: host=localhost, port=" + proxy.Port + "</p>";
			
			data = Template ("Hi", "", data);
			response.SetData (data);
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
			
			string data = "";
			if (showSession == null)
				data = SessionList (sessionList);
			else
				data = SessionStatus (showSession);
			
			//data = Template ("Session", "<meta http-equiv=\"refresh\" content=\"1\">", data);
			data = Template ("Session", "", data);
			response.SetData (data);
			return response;
		}

		private string SessionStatus (PersonalProxy.ProxySession session)
		{
			string data = "";
			
			Request req = session.Request;
			Response resp = null;
			if (req != null)
				resp = req.Response;
			
			data += "<p>Status: " + session.Status + "</p>";
			data += "<p>Served " + session.served + "</p>";
			data += "<p>Close: <a href=\"?close=" + session.GetHashCode () + "\">close</a></p>";
			
			data += "<h2>Request/Client</h2>";
			if (req != null)
				data += RequestData (req);
			
			data += "<h2>Response/Remote</h2>";
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
		private string HeaderData (Header header)
		{
			string data = "";
			foreach (string h in header)
				data += "<li>" + h + "</li>";
			return "<ul>" + data + "</ul>";
		}

		private string SessionList (ProxySession[] sessionList)
		{
			string data = "";
			foreach (ProxySession session in sessionList) {
				
				data += "<li>";
				data += "<p><a href=\"?show=" + session.GetHashCode () + "\">Session</a>: ";
				data += session.served + " requests served ";
				data += " <a href=\"?close=" + session.GetHashCode () + "\">close</a> " + session.Status + "</p>";
				Request req = session.Request;
				if (req != null) {
					Response resp = req.Response;
					
					data += "<p>Request: " + req.Method + " <a href=\"" + req.Uri + "\">" + req.Uri.Scheme + "://" + req.Uri.Host + (req.Uri.IsDefaultPort ? "" : ":" + req.Uri.Port) + "/</a>";
					data += " " + ((int)(DateTime.Now - req.Start).TotalSeconds) + " s";
					if (req.DataSocket.Received > 0)
						data += "Sent: " + (req.DataSocket.Received / 1000) + " Kbytes";
					data += "</p>";
					if (resp != null && resp.DataSocket != null) {
						data += "<p>Response: " + ((int)resp.HttpCode) + " " + resp.HttpCode;
						if (resp.DataSocket.Received > 0)
							data += " Recv: " + (resp.DataSocket.Received / 1000) + " Kbytes";
						if (resp.HasBody)
							data += " Total: " + (resp.ContentLength / 1000) + " Kbytes"; else if (resp.Chunked)
							data += " Total: chunked";
						else
							data += " Total: unknown";
						data += "</p>";
					}
				}
				data += "</li>";
			}
			data = "<ul>" + data + "</ul>";
			
			return data;
		}

		private Response ConnectionPage (string[] path, NameValueCollection httpGet)
		{
			Response response = new Response (HttpStatusCode.OK);
			
			string data = "<h2>Remote connections</h2>";
			foreach (CachedServer s in connectionManager.ServerArray) {
				data += PrintCachedServer (s);
			}
			
			//data = Template ("Connections", "<meta http-equiv=\"refresh\" content=\"1\">", data);
			data = Template ("Connections", "", data);
			response.SetData (data);
			return response;
		}

		string PrintCachedServer (CachedServer server)
		{
			string data = "<h3>" + server.endpoint + "</h3>";
			foreach (CachedConnection c in server.Connections) {
				if (c.Busy)
					data += " <span style=\"background: green;\">busy</span> " + c.served;
				else
					data += " <span style=\"background: gray;\">free</span> " + c.served;
			}
			return data;
		}

		static internal string FilterUrl ()
		{
			return "http://" + ConfigHost + "/Filter/";
		}
		static internal string FilterUrl (Filter filter)
		{
			return FilterUrl () + filter.GetHashCode () + "/";
		}

		private void FiltersPage (string[] path, NameValueCollection httpGet, Request request)
		{
			Response response = request.Response;
			string data = "";
			
			if (path.Length <= 2 || path[2] == "") {
				//Add and remove commands
				try {
					AddFilter (httpGet);
					DeleteFilter (httpGet);
				} catch (Exception e) {
					data += "<p><strong>Error:</strong> " + Response.Html (e.Message) + "</p>";
					Console.Error.WriteLine ("WebUI, Filter error: " + e.Message);
				}
				
				data += "<h2>Request Filters</h2>";
				data += ListFilters (proxy.FilterRequest);
				
				data += "<h2>Response Filters</h2>";
				data += ListFilters (proxy.FilterResponse);
				
				data = Template ("Filters", "", data);
				
			} else {
				int filterCode = 0;
				int.TryParse (path[2], out filterCode);
				Filter f = FindFilter (proxy.FilterRequest, filterCode);
				if (f == null)
					f = FindFilter (proxy.FilterResponse, filterCode);
				if (f == null) {
					response.HttpCode = HttpStatusCode.Found;
					response.SetData ("<p>Filter not found.</p><p><a href=\"" + FilterUrl () + "\">back</a></p>");
					response.SetHeader ("Location", FilterUrl ());
					return;
				}
				data = Template ("" + f, "", f.Status (httpGet, request));
			}
			
			response.SetData (data);
			return;
		}

		private void DeleteFilter (NameValueCollection keys)
		{
			string args = keys["delete"];
			if (args == null)
				return;
			
			int deleteIndex = int.Parse (args);
			DeleteFilter (proxy.FilterRequest, deleteIndex);
			DeleteFilter (proxy.FilterResponse, deleteIndex);
		}

		private void AddFilter (NameValueCollection keys)
		{
			string args = keys["add"];
			if (args == null)
				return;
			int addIndex = int.Parse (args);
			FilterList list;
			list = FindFilter (proxy.FilterRequest, addIndex) as FilterList;
			if (list == null)
				list = FindFilter (proxy.FilterResponse, addIndex) as FilterList;
			if (list != null) {
				Filter f = FilterLoader.FromString (keys["type"]);
				if (f != null)
					list.Add (f);
			}
		}

		Filter FindFilter (Filter filter, int needle)
		{
			if (filter.GetHashCode () == needle)
				return filter;
			
			FilterList list = filter as FilterList;
			if (list != null) {
				foreach (Filter f in list.ToArray ()) {
					Filter test = FindFilter (f, needle);
					if (test != null)
						return test;
				}
			}
			return null;
		}


		void DeleteFilter (Filter filter, int needle)
		{
			FilterList list = filter as FilterList;
			if (list != null) {
				foreach (Filter f in list.ToArray ()) {
					if (f.GetHashCode () == needle) {
						//Prevent it from deleting itself
						if (f == this)
							return;
						
						list.Remove (f);
						return;
					} else
						
						DeleteFilter (f, needle);
				}
			}
		}


		private string ListFilters (Filter filter)
		{
			string data = "<li><a href=\"" + FilterUrl (filter) + "\">" + filter + "</a>";
			
			//Don't show delete on root filters
			if (filter != proxy.FilterRequest && filter != proxy.FilterResponse)
				data += " (<a href=\"" + FilterUrl () + "?delete=" + filter.GetHashCode () + "\">delete</a>)";
			
			FilterList list = filter as FilterList;
			if (list != null) {
				data += "<ul>";
				foreach (Filter f in list.ToArray ())
					data += ListFilters (f);
				
				data += "<li>" + "<form method=\"get\" action=\"" + FilterUrl () + "\">" + "<input type=\"hidden\" name=\"add\" value=\"" + list.GetHashCode () + "\" />" + "<input type=\"text\" name=\"type\" />" + "<input type=\"submit\" value=\"Add\" />" + "</form>" + "</li>";
				
				data += "</ul>";
			}
			
			data += "</li>";
			return data;
		}

		private string Template (string title, string header, string contents)
		{
			return "<!DOCTYPE html>" + "<html>" + "<head>" + "<title>" + Response.Html (title) + " - Personal Proxy</title>" + header + "<style>" + ".menu li {list-style-type: none; display: inline; margin: 1em;} " + "p, h2,h3,h4 {margin: 2pt;}" + "</style>" + "</head>" + "<body>" + "<ul class=\"menu\">" + "<li><a href=\"/\">About</a></li>" + "<li><a href=\"/Session\">Session</a></li>" + "<li><a href=\"/Connection\">Connection</a></li>" + "<li><a href=\"/Filter\">Filters</a></li>" + "<li><a href=\"/status\"></a></li>" + "</ul>" + "<h1>" + Response.Html (title) + "</h1>" + contents + "</body></html>";
		}

		public override string ToString ()
		{
			return string.Format ("[WebUI]");
		}

		public override string Status ()
		{
			return "Web based user interface, If you can read this, the filter is working.";
		}
	}
}
