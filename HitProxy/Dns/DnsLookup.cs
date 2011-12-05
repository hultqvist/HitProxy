using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using DnDns.Enums;
using DnDns.Query;
using DnDns.Records;
using DnDns.Security;
using System.Threading;
using System.Net;
using HitProxy.Http;

namespace HitProxy
{
	public class DnsLookup
	{
		public readonly List<IPAddress> AList = new List<IPAddress> ();
		/// <summary>
		/// The requested name + all domain names in the DNS response
		/// </summary>
		public readonly List<string> NameList = new List<string> ();
		public readonly DateTime Checked = DateTime.Now;
			
		private DnsLookup (string address)
		{
			NameList.Add (address);

#if OLD
			IPAddress[] ipList = Dns.GetHostAddresses (address);
			foreach (IPAddress ip in ipList) {
			if (ip.AddressFamily == AddressFamily.InterNetworkV6 && Proxy.IPv6 == false)
				continue;
				AList.Add (ip);
			}
					
			if (AList.Count == 0)
				throw new HeaderException ("Lookup of " + address + " failed", HttpStatusCode.BadGateway);
#else		
			
			//localhost
			if (address.ToLowerInvariant () == "localhost") {
				AList.Add (IPAddress.Loopback);
				return;
			}
			
			//Raw IP addresses
			IPAddress ipa;
			if (IPAddress.TryParse (address, out ipa)) {
				AList.Add (ipa);
				return;
			}
			
			//New method below
			DnsQueryRequest request = new DnsQueryRequest ();
			
			//TODO also do AAAA lookup if Proxy.ipv6 is enabled
			
			DnsQueryResponse dr;
			try {
				// UDP request
				dr = request.Resolve (address, NsType.A, NsClass.INET, ProtocolType.Udp);
				if (ParseDnsResponse (dr))
					return;
			
				// TCP request
				dr = request.Resolve (address, NsType.A, NsClass.INET, ProtocolType.Tcp);
				if (ParseDnsResponse (dr))
					return;
			} catch (Exception e) {
				throw new HeaderException ("DNS lookup failed", HttpStatusCode.NotFound, e);
			}
			throw new HeaderException ("Lookup of " + address + " failed: " + dr.RCode, HttpStatusCode.BadGateway);
#endif	
		}

		bool ParseDnsResponse (DnsQueryResponse ur)
		{
			if (ur.RCode != RCode.NoError) 
				return false;
			
			foreach (IDnsRecord record in ur.Answers) {
				//Console.WriteLine (record.Answer + ", " + record.DnsHeader.NsType);
				if (record.DnsHeader.NsType == NsType.A)
					AList.Add (IPAddress.Parse (record.Answer.Replace ("Address: ", "")));
				if (record.DnsHeader.NsType == NsType.AAAA && Proxy.IPv6)
					AList.Add (IPAddress.Parse (record.Answer.Replace ("Address: ", "")));
				if (record.DnsHeader.NsType == NsType.CNAME)
					NameList.Add (record.Answer.Replace ("Host: ", "").Trim ('.'));
			}
			return true;
		}
		
		#region Static functions
		
		static ReadWriteLock cacheLock = new ReadWriteLock ();
		static Dictionary<string, DnsLookup> cache = new Dictionary<string, DnsLookup> ();
		
		public static DnsLookup Get (string address)
		{
			using (cacheLock.Read) {
				DnsLookup d;
				if (cache.ContainsKey (address)) {
					d = cache [address];
					if (d.Checked.AddMinutes (10) > DateTime.Now) {
						return d;
					}
				}
			}
			using (cacheLock.Write) {
				DnsLookup d = new DnsLookup (address);
				cache.Remove (address);
				cache.Add (address, d);
				return d;
			}
		}
		
		#endregion
		
	}
}

