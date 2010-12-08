using System;
using Microsoft.Win32;

namespace HitProxy
{
	/// <summary>
	/// Manages browser proxy settings
	/// </summary>
	public class BrowserProxy
	{
		readonly int defaultProxyEnabled;
		readonly string defaultProxyServer;
		readonly Proxy proxy;

		/// <summary>
		/// Read and store default settings
		/// </summary>
		public BrowserProxy (Proxy proxy)
		{
			this.proxy = proxy;
			try {
				RegistryKey registry = Registry.CurrentUser.OpenSubKey ("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", true);
				defaultProxyEnabled = (int)registry.GetValue ("ProxyEnable");
				defaultProxyServer = (string)registry.GetValue ("ProxyServer");
			} catch (Exception) {
				defaultProxyEnabled = 0;
				defaultProxyServer = null;
			}
		}

		bool enabled = false;
		public bool Enabled {
			get { return enabled; }
			set {
				if (value)
					enabled = Enable ();
				else
					enabled = Disable ();
			}
		}

		public bool CanSetProxy {
			get { return defaultProxyServer != null; }
		}

		/// <summary>
		/// Enable HitProxy Proxy Settings in Browser
		/// </summary>
		private bool Enable ()
		{
			try {
				RegistryKey registry = Registry.CurrentUser.OpenSubKey ("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", true);
				registry.SetValue ("ProxyEnable", 1);
				registry.SetValue ("ProxyServer", "127.0.0.1:" + proxy.Port);
				return true;
			} catch (Exception) {
				return false;
			}
		}

		/// <summary>
		/// Restore previous proxy settings
		/// </summary>
		private bool Disable ()
		{
			try {
				if (defaultProxyServer == null)
					return enabled;
				
				RegistryKey registry = Registry.CurrentUser.OpenSubKey ("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", true);
				registry.SetValue ("ProxyEnable", defaultProxyEnabled);
				registry.SetValue ("ProxyServer", defaultProxyServer);
				return false;
			} catch (Exception) {
				return enabled;
			}
		}
	}
}

