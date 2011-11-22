using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using HitProxy.Connection;
using HitProxy.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace HitProxy.Session
{
	/// <summary>
	/// Proxy implementation for HTTP CONNECT
	/// </summary>
	public static class ConnectProxy
	{
		/// <summary>
		/// Read and execute request.
		/// </summary>
		/// <param name="request">
		/// A <see cref="Request"/>
		/// </param>
		public static void ProcessHttpConnect (Request request, Stream clientStream, CachedConnection remote)
		{
			request.Response = new Response (remote);
			request.Response.HttpVersion = "HTTP/1.1";
			request.Response.HttpCode = HttpStatusCode.OK;
			request.Response.HTTPMessage = HttpStatusCode.OK.ToString ();
			request.Response.KeepAlive = false;
			request.Response.Add ("Proxy-Agent: HitProxy");
			request.Response.SendHeaders (clientStream);
			
			Thread t = new Thread (() => {
				try {
					request.Stream.PipeTo (request.Response.Stream);
				} catch (Exception) {
					remote.Dispose ();
				} finally {
				}
			});
			t.Name = Thread.CurrentThread.Name + "ConnectOutput";
			t.Start ();
			try {
				request.Response.Stream.PipeTo (request.Stream);				
			} catch (Exception e) {
				Console.WriteLine ("ConnectProxy " + e.GetType ().Name + " :" + e.Message);
				request.Stream.NullSafeDispose ();
			} finally {
				remote.Dispose ();
				clientStream.NullSafeDispose ();
			}
			t.Join ();
			
			request.Response.Dispose ();
			request.Response = null;
		}
		
		
		
		/// <summary>
		/// Intercepts a HTTP CONNECT so we can filter the encrypted requests
		/// </summary>
		public static Stream InterceptConnect (Request request, Stream clientStream, CachedConnection remote)
		{
			//This code may work but it has not been tested yet.
			string certPath = Path.Combine (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData),
				"HitProxy"), "server.pfx");
			if (File.Exists (certPath) == false)
				throw new FileNotFoundException ("Need a server certificate", certPath);
			
			X509Certificate cert = X509Certificate2.CreateFromCertFile (certPath);
			
			request.Response = new Response (remote);
			request.Response.HttpVersion = "HTTP/1.1";
			request.Response.HttpCode = HttpStatusCode.OK;
			request.Response.HTTPMessage = HttpStatusCode.OK.ToString ();
			request.Response.KeepAlive = false;
			request.Response.Add ("Proxy-Agent: HitProxy");
			request.Response.SendHeaders (clientStream);
			
			//Client
			SslStream ssl = new SslStream (clientStream, false, RemoteCertificateValidation, LocalCertValidation);
			try {
				ssl.AuthenticateAsServer (cert, false, System.Security.Authentication.SslProtocols.Tls, false);
			} catch (Exception e) {
				Console.WriteLine (e.Message);
				throw;
			}
			
			//Remote server
			SslStream remoteSsl = new SslStream (remote.Stream, false, RemoteCertificateValidation);
			remoteSsl.AuthenticateAsClient (request.Uri.Host);
			remote.Stream = remoteSsl;
			return ssl;
		}

		static X509Certificate LocalCertValidation (object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, String[] acceptableIssuers)
		{
			return null;
		}

		static bool RemoteCertificateValidation (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			return true;
		}
	}
}
