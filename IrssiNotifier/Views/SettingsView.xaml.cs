﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using WindowsPhone.Recipes.Push.Client;
using System.IO.IsolatedStorage;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using Microsoft.Phone.Shell;

namespace IrssiNotifier.Views
{
	public partial class SettingsView : UserControl, INotifyPropertyChanged
	{
		public SettingsView()
		{
			DataContext = this;
			InitializeComponent();
		}

		public bool IsToastEnabled
		{
			get { return PushContext.Current.IsToastEnabled; }
			set
			{
				if (PushContext.Current.IsToastEnabled != value)
				{
					PushContext.Current.IsToastEnabled = value;
					SettingsView.UpdateSettings("toast", value, Dispatcher);
					NotifyPropertyChanged("IsToastEnabled");
				}
			}
		}

		public bool IsTileEnabled
		{
			get { return PushContext.Current.IsTileEnabled; }
			set
			{
				if (PushContext.Current.IsTileEnabled != value)
				{
					PushContext.Current.IsTileEnabled = value;
					NotifyPropertyChanged("IsTileEnabled");
					SettingsView.UpdateSettings("tile", value, Dispatcher, new Action(() => PinTile(value)));
				}
				
			}
		}

		private void PinTile(bool value)
		{
			var hiliteTile = ShellTile.ActiveTiles.FirstOrDefault(tile => tile.NavigationUri.ToString() == App.HILITEPAGEURL);
			//var activeTiles = ShellTile.ActiveTiles.ToList();
			if (value && hiliteTile == null)
			{
				var answer = MessageBox.Show("Tämä vaatii tiilen. Ok?", "Vahvista", MessageBoxButton.OKCancel);
				if (answer == MessageBoxResult.OK)
				{
					var NewTileData = new StandardTileData
					{
						BackgroundImage = new Uri("/Images/Tile.png", UriKind.Relative),
						Count = 0
					};
					ShellTile.Create(new Uri(App.HILITEPAGEURL, UriKind.Relative), NewTileData);
				}
			}
			else if (!value && hiliteTile != null)
			{
				var answer = MessageBox.Show("Poistetaanko myös tiili?", "Vahvista", MessageBoxButton.OKCancel);
				if (answer == MessageBoxResult.OK)
				{
					hiliteTile.Delete();
					//activeTiles.First(tile => tile.NavigationUri.ToString().Contains(App.HILITEPAGEURL)).Delete();
				}
			}
		}

		public bool IsRawEnabled
		{
			get { return PushContext.Current.IsRawEnabled; }
			set
			{
				if (PushContext.Current.IsRawEnabled != value)
				{
					PushContext.Current.IsRawEnabled = value;
					SettingsView.UpdateSettings("raw", value, Dispatcher);
					NotifyPropertyChanged("IsRawEnabled");
				}
			}
		}

		public bool IsPushEnabled
		{
			get { return PushContext.Current.IsPushEnabled; }
			set
			{
				if (PushContext.Current.IsConnected != value)
				{
					if (value)
					{
						PushContext.Current.Connect(c => SettingsView.RegisterChannelUri(c.ChannelUri, Dispatcher));
					}
					else
					{
						PushContext.Current.Disconnect();
					}
					NotifyPropertyChanged("IsPushEnabled");
				}
			}
		}

		public void NotifyPropertyChanged(string property){
			if(PropertyChanged != null){
				PropertyChanged(this,new PropertyChangedEventArgs(property));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;


		public static void RegisterChannelUri(Uri ChannelUri, Dispatcher dispatcher)
		{
			var webclient = new WebClient();
			webclient.UploadStringCompleted += (sender1, args) =>
			{
				if (args.Error != null) {
					dispatcher.BeginInvoke(() => MessageBox.Show(args.Result));
					return;
				}
				var result = JObject.Parse(args.Result);
				if (bool.Parse(result["success"].ToString()))
				{
					bool toastStatus = bool.Parse(result["toastStatus"].ToString());
					bool tileStatus = bool.Parse(result["tileStatus"].ToString());
					if (toastStatus != PushContext.Current.IsToastEnabled)
					{
						PushContext.Current.IsToastEnabled = toastStatus;
					}
					if (tileStatus != PushContext.Current.IsTileEnabled)
					{
						PushContext.Current.IsTileEnabled = tileStatus;
					}//TODO notifikaatio jos muuttuu...
				}
				else
				{
					dispatcher.BeginInvoke(() => MessageBox.Show(result["errorMessage"].ToString()));
				}
				if (PushContext.Current.IsConnected && PushContext.Current.IsPushEnabled && PushContext.Current.IsTileEnabled)
				{
					UpdateSettings("clearcount", true, dispatcher, new Action(() =>
					{
						foreach (var tile in ShellTile.ActiveTiles)
						{
							tile.Update(new StandardTileData() { Count = 0 });
						}
					}));

				}

			};
			webclient.Headers["Content-type"] = "application/x-www-form-urlencoded";
			webclient.UploadStringAsync(new Uri(App.BASEADDRESS + "client/update"), "POST", "apiToken=" + IsolatedStorageSettings.ApplicationSettings["userID"].ToString() + "&guid=" + App.AppGuid + "&newUrl=" + ChannelUri.ToString());
		}

		private static void UpdateSettings(string param, bool enabled, Dispatcher dispatcher, Action callback = null)
		{
			var webclient = new WebClient();
			webclient.UploadStringCompleted += (sender1, args) =>
			{
				if (args.Error != null)
				{
					dispatcher.BeginInvoke(() => MessageBox.Show(args.Result));
					return;
				}
				var result = JObject.Parse(args.Result);
				if (!bool.Parse(result["success"].ToString()))
				{
					dispatcher.BeginInvoke(() => MessageBox.Show(result["errorMessage"].ToString()));
				}
				else
				{
					if (callback != null)
					{
						callback();
					}
				}
			};
			webclient.Headers["Content-type"] = "application/x-www-form-urlencoded";
			webclient.UploadStringAsync(new Uri(App.BASEADDRESS + "client/settings"), "POST", "apiToken=" + IsolatedStorageSettings.ApplicationSettings["userID"].ToString() + "&guid=" + App.AppGuid + "&"+param+"=" + enabled);
		}
	}
}