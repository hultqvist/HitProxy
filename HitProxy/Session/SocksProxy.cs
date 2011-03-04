using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using HitProxy.Http;
using HitProxy.Connection;

namespace HitProxy.Session
{
	public partial class ProxySession
	{
		//See http://tools.ietf.org/html/rfc1928 for details

		/// <summary>
		/// Given a new connection to a socks proxy we initate the handshake.
		/// </summary>
		void PrepareSocks (CachedConnection remoteConnection)
		{
			Socket socket = remoteConnection.remoteSocket;
			
			//Send Version identifier
			byte[] version = new byte[3];
			//Socks5
			version [0] = 5;
			//Numberof methods following
			version [1] = 1;
			//No authentication
			version [2] = 0;
			socket.Send (version);
			
			//Read Selection message
			byte[] selection = new byte[2];
			socket.Receive (selection);
			//Socks version
			if (selection [0] != 5)
				throw new HeaderException ("Socks5 not supported, got Socks" + selection [0], HttpStatusCode.MethodNotAllowed);
			if (selection [1] != 0)
				throw new HeaderException ("No authentication not supported: " + selection [0], HttpStatusCode.MethodNotAllowed);
			
			//Send request
			byte[] name = Encoding.ASCII.GetBytes (request.Uri.Host);
			byte[] sreq = new byte[7 + name.Length];
			sreq [0] = 5;
			//Connect
			sreq [1] = 1;
			//(Reserved)
			sreq [2] = 0;
			//Address type: DomainName = 3
			sreq [3] = 3;
			//Length of domain name
			sreq [4] = (byte)name.Length;
			//Domain Name
			name.CopyTo (sreq, 5);
			//Port
			sreq [5 + name.Length] = (byte)(request.Uri.Port >> 8);
			sreq [6 + name.Length] = (byte)(request.Uri.Port & 0xFF);
			socket.Send (sreq);
			
			//Read response
			byte[] resp = new byte[5];
			socket.Receive (resp);
			if (resp [0] != 5)
				throw new HeaderException ("Socks5 not supported, got Socks" + selection [0], HttpStatusCode.MethodNotAllowed);
			byte[] resp2;
			switch (resp [3]) {
			case 01:
				//IPv4
				resp2 = new byte[4 + 2];
				resp2 [0] = resp [4];
				socket.Receive (resp2, 1, 4 - 1 + 2, SocketFlags.None);
				break;
			case 03:
				//Domain name
				resp2 = new byte[resp [4] + 2];
				socket.Receive (resp2);
				break;
			case 04:
				//IPv6
				resp2 = new byte[16 + 2];
				resp2 [0] = resp [4];
				socket.Receive (resp2, 1, 16 - 1 + 2, SocketFlags.None);
				break;
			}
			
			switch (resp [1]) {
			case 0:
				break;
			case 1:
				throw new HeaderException ("Socks connect failed: general SOCKS server failure", HttpStatusCode.InternalServerError);
			case 2:
				throw new HeaderException ("Socks connect failed: Connection not allowed by ruleset", HttpStatusCode.InternalServerError);
			case 3:
				throw new HeaderException ("Socks connect failed: Network unrechable", HttpStatusCode.InternalServerError);
			case 4:
				throw new HeaderException ("Socks connect failed: Host unreachable", HttpStatusCode.InternalServerError);
			case 5:
				throw new HeaderException ("Socks connect failed: Connection refused", HttpStatusCode.InternalServerError);
			case 6:

				throw new HeaderException ("Socks connect failed: TTL expired", HttpStatusCode.InternalServerError);
			case 7:
				throw new HeaderException ("Socks connect failed: Command not supported", HttpStatusCode.InternalServerError);
			case 8:
				throw new HeaderException ("Socks connect failed: Address type not supported", HttpStatusCode.InternalServerError);
			default:
				throw new HeaderException ("Socks connect failed: " + resp [0], HttpStatusCode.InternalServerError);
			}
			
			return;
		}
	}
}

