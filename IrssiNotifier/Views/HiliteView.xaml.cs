﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Net;
using System.Windows;
using IrssiNotifier.Model;
using IrssiNotifier.Resources;
using Microsoft.Phone.Shell;
using Newtonsoft.Json.Linq;

namespace IrssiNotifier.Views
{
	public partial class HiliteView : INotifyPropertyChanged
	{
		private ObservableCollection<Hilite> _hiliteCollection;
		private bool _isBusy;
		private long _lastFetch;

		public HiliteView()
		{
			InitializeComponent();
			DataContext = this;
			IsBusy = true;
			SettingsView.GetInstance().Connect(() => Fetch(true));
			GenerateLocalizedAppBar();
		}

		public bool IsBusy
		{
			get { return _isBusy; }
			set
			{
				_isBusy = value;
				NotifyPropertyChanged("IsBusy");
				NotifyPropertyChanged("IsListEnabled");
			}
		}

		public bool IsListEnabled
		{
			get { return !IsBusy; }
		}

		public ObservableCollection<Hilite> HiliteCollection
		{
			get { return _hiliteCollection; }
			set
			{
				_hiliteCollection = value;
				NotifyPropertyChanged("HiliteCollection");
				NotifyPropertyChanged("NewHilites");
			}
		}

		public ObservableCollection<Hilite> NewHilites
		{
			get
			{
				if (_nextHilite != null && _nextHilite.Id < _lastFetch)
				{
					newListBox.ItemTemplate = (DataTemplate)Resources["ButtonlessHiliteTemplate"];
				}
				else
				{
					newListBox.ItemTemplate = (DataTemplate)Resources["HiliteTemplate"];
				}
				return HiliteCollection != null
						? new ObservableCollection<Hilite>(HiliteCollection.Where(hilite => hilite.Id > _lastFetch))
						: new ObservableCollection<Hilite>();
			}
		}

		private Hilite _nextHilite;

		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged;

		public void NotifyPropertyChanged(string property)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(property));
			}
		}

		#endregion

		private void Fetch(bool ignoreCache)
		{
			if (ignoreCache || HiliteCollection == null)
			{
				HiliteCollection = new ObservableCollection<Hilite>();
				long lastFetch = 0;
				if (IsolatedStorageSettings.ApplicationSettings.Contains("LastHiliteFetch"))
				{
					try
					{
						lastFetch = long.Parse(IsolatedStorageSettings.ApplicationSettings["LastHiliteFetch"].ToString());
					}
					catch (FormatException)
					{
						lastFetch = 0;
					}
				}
				_lastFetch = lastFetch;
				FetchHilites();
			}
		}

		private void ParseResult(string response)
		{
			try
			{
				var collection = new ObservableCollection<Hilite>();
				var result = JObject.Parse(response);
				if (!bool.Parse(result["success"].ToString()))
				{
					Dispatcher.BeginInvoke(() => MessageBox.Show(result["errorMessage"].ToString(), AppResources.ErrorTitle, MessageBoxButton.OK));
				}
				else
				{
					if (!bool.Parse(result["isNextFetch"].ToString()))
					{
						IsolatedStorageSettings.ApplicationSettings["LastHiliteFetch"] = result["currentTimestamp"].ToString();
					}
					else
					{
						collection = HiliteCollection;
						var last = collection.LastOrDefault();
						if (last != null)
						{
							last.IsLast = false;
						}
					}
					var messages = JArray.Parse(result["messages"].ToString());
					foreach (var hilite in messages.Select(hiliteRow => JObject.Parse(hiliteRow.ToString())))
					{
						var hiliteObj = new Hilite
						                	{
						                		Channel = hilite["channel"].ToString(),
						                		Nick = hilite["nick"].ToString(),
						                		Message = hilite["message"].ToString(),
						                		TimestampString = hilite["timestamp"].ToString(),
						                		Id = long.Parse(hilite["id"].ToString())
						                	};
						collection.Add(hiliteObj);
					}
					if (result["nextMessage"].Type != JTokenType.Null)
					{
						var nextHilite = JObject.Parse(result["nextMessage"].ToString());
						_nextHilite = new Hilite
						              	{
						              		Channel = nextHilite["channel"].ToString(),
						              		Nick = nextHilite["nick"].ToString(),
						              		Message = nextHilite["message"].ToString(),
						              		TimestampString = nextHilite["timestamp"].ToString(),
						              		Id = long.Parse(nextHilite["id"].ToString())
						              	};
						var last = collection.LastOrDefault();
						if (last != null)
						{
							last.IsLast = true;
						}
					}
					else
					{
						_nextHilite = null;
						var last = collection.LastOrDefault();
						if (last != null)
						{
							last.IsLast = false;
						}
					}
					HiliteCollection = collection;
				}
			}
			catch (Exception e)
			{
				Dispatcher.BeginInvoke(() => MessageBox.Show(string.Format(AppResources.ErrorFetchingMessagesEx, e), AppResources.ErrorTitle, MessageBoxButton.OK));
			}
			IsBusy = false;
		}

		private void FetchHilites(long starting = 0)
		{
			IsBusy = true;
			var webclient = new WebClient();
			webclient.UploadStringCompleted += (sender1, args) =>
			{
				if (args.Error != null)
				{
					IsBusy = false;
					Dispatcher.BeginInvoke(() => MessageBox.Show(AppResources.ErrorFetchingMessages, AppResources.ErrorTitle, MessageBoxButton.OK));
					return;
				}
				ParseResult(args.Result);
			};
			var postMessage = "apiToken=" + IsolatedStorageSettings.ApplicationSettings["userID"] + "&guid=" +
			                  App.AppGuid + "&version=" + App.Version;
			if (starting != 0)
			{
				postMessage += "&starting=" + starting;
			}
			webclient.Headers["Content-type"] = "application/x-www-form-urlencoded";
			webclient.UploadStringAsync(new Uri(App.Baseaddress + "client/messages"), "POST",
										postMessage);
		}

		public void RefreshButtonClick(object sender, EventArgs e)
		{
			Fetch(true);
			SettingsView.GetInstance().ClearTileCount();
		}

		public void SettingsButtonClick(object sender, EventArgs e)
		{
			App.GetCurrentPage().NavigationService.Navigate(new Uri("/Pages/SettingsPage.xaml", UriKind.Relative));
		}

		public void AboutButtonClick(object sender, EventArgs e)
		{
			App.GetCurrentPage().NavigationService.Navigate(new Uri("/YourLastAboutDialog;component/AboutPage.xaml", UriKind.Relative));
		}

		private void MoreClick(object sender, RoutedEventArgs e)
		{
			FetchHilites(_nextHilite.Id);
		}

		public ApplicationBar ApplicationBar { get; private set; }

		private void GenerateLocalizedAppBar()
		{
			var appBar = new ApplicationBar {IsMenuEnabled = true, IsVisible = true};
			var refreshButton = new ApplicationBarIconButton(new Uri("/Images/appbar.refresh.rest.png", UriKind.Relative))
			                    	{Text = AppResources.RefreshButtonText};
			refreshButton.Click += RefreshButtonClick;
			appBar.Buttons.Add(refreshButton);
			var settingsButton = new ApplicationBarIconButton(new Uri("/Images/appbar.feature.settings.rest.png", UriKind.Relative))
									{Text = AppResources.SettingsButtonText};
			settingsButton.Click += SettingsButtonClick;
			appBar.Buttons.Add(settingsButton);
			var aboutButton = new ApplicationBarMenuItem(AppResources.AboutButtonText);
			aboutButton.Click += AboutButtonClick;
			appBar.MenuItems.Add(aboutButton);
			ApplicationBar = appBar;
		}
	}
}
