﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows;
using WForms = System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Input;
using The_Long_Dark_Save_Editor_2.Game_data;
using The_Long_Dark_Save_Editor_2.Helpers;
using The_Long_Dark_Save_Editor_2.ViewModels;

namespace The_Long_Dark_Save_Editor_2
{

	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		public static MainWindow Instance { get; set; }
		public static VersionData Version { get { return new VersionData() { version = "2.7.3" }; } }

		private GameSave currentSave;
		public GameSave CurrentSave { get { return currentSave; } set { SetPropertyField(ref currentSave, value); } }

		private Profile currentProfile;
		public Profile CurrentProfile
		{
			get { return currentProfile; }
			set { SetPropertyField(ref currentProfile, value); }
		}

		private bool testBranch;
		public bool TestBranch
		{
			get { return testBranch; }
			set
			{
				Properties.Settings.Default.TestBranch = value;
                SetPropertyField(ref testBranch, value);
                UpdateSaves();
            }
		}

		public bool IsDebug { get; set; }

		private ObservableCollection<EnumerationMember> saves;

		public ObservableCollection<EnumerationMember> Saves
		{
			get { return saves; }
			set { SetPropertyField(ref saves, value); }
		}

		public MainWindow()
		{
#if DEBUG
			IsDebug = true;
			Debug.WriteLine(System.Threading.Thread.CurrentThread.CurrentUICulture);
			//System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
#endif

            this.DataContext = this;
			Instance = this;
			InitializeComponent();

			TestBranch = Properties.Settings.Default.TestBranch;
			Title += " " + Version.ToString();

			JsonConvert.DefaultSettings = () => new JsonSerializerSettings
			{
				//MissingMemberHandling = MissingMemberHandling.Error,
				FloatFormatHandling = FloatFormatHandling.Symbol,
			};
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Debug.WriteLine("Window loaded");
#if !DEBUG
			if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
			{
				try
				{
					CheckForUpdates();
				}
				catch (Exception ex)
				{
					dialogHost.IsOpen = false;
					ErrorDialog.Show("Failed to check for new versions", ex != null ? (ex.Message + "\n" + e.ToString()) : null);
				}

			}

			if (!Properties.Settings.Default.BugReportWarningShown)
			{
				System.Windows.MessageBox.Show("Do NOT report any in-game bugs to Hinterland if you have edited your save. Bugs might be caused by the save editor. Only report bugs if you are able to reproduce them in fresh unedited save.");
				//System.Windows.MessageBox.Show("If you don't have test branch version of the game, untick the toggle button at top right corner.");

				Properties.Settings.Default.BugReportWarningShown = true;
				Properties.Settings.Default.Save();
			}
#endif
			UpdateSaves();
		}

		private void UpdateSaves()
		{
			var path = Path.Combine(Util.GetLocalPath(), testBranch ? "HinterlandTest2" : "Hinterland", "TheLongDark");
			Debug.WriteLine(path);

			Saves = Util.GetSaveFiles(path);
			if (Saves.Count == 0)
				CurrentSave = null;
			else
				ccSaves.SelectedIndex = 0;

			var profile = Path.Combine(path, "user001");
			if (File.Exists(profile) && (CurrentProfile == null || !Equals(profile, CurrentProfile.path)))
			{
				try
				{
					CurrentProfile = new Profile(profile);
				}
				catch (Exception ex)
				{
					WForms.MessageBox.Show(ex.Message + "\nFailed to load profile\n" + ex.ToString(), "Failed to load profile", WForms.MessageBoxButtons.OK, WForms.MessageBoxIcon.Exclamation);
				}
			}

		}

		public void CheckForUpdates()
		{
			WebClient webClient = new WebClient();
			webClient.DownloadStringCompleted += (sender, e) =>
			{
				try
				{
					string json = e.Result;
					List<VersionData> versions = JsonConvert.DeserializeObject<List<VersionData>>(json);
					if (versions[versions.Count - 1] > Version)
					{
						var newerVersions = versions.Where(version => version > Version).ToList();
						var viewModel = new NewVersionDialogViewModel() { Versions = newerVersions, Url = newerVersions[newerVersions.Count - 1].url };
						dialogHost.DialogContent = viewModel;
						dialogHost.IsOpen = true;
					}
				}
				catch (Exception ex)
				{
					dialogHost.IsOpen = false;
					ErrorDialog.Show("Failed to check for new versions", ex != null ? (ex.Message + "\n" + e.ToString()) : null);

				}
			};
			webClient.DownloadStringTaskAsync("https://tld-save-editor-2.firebaseio.com/Changelog.json");

		}

		private void MenuItem_Click(object sender, RoutedEventArgs e)
		{
			var scope = FocusManager.GetFocusScope(tabPanel);
			FocusManager.SetFocusedElement(scope, null);
			Keyboard.ClearFocus();

			if (CurrentSave != null)
				CurrentSave.Save();
			if (CurrentProfile != null)
				CurrentProfile.Save();
		}

		private void CurrentSaveSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (ccSaves == null && ccSaves.SelectedValue == null)
				return;

			if (ccSaves.SelectedValue != null)
			{
				var path = ccSaves.SelectedValue.ToString();
				try
				{
					var save = new GameSave();
					save.LoadSave(path);
					CurrentSave = save;
				}
				catch (Exception ex)
				{
					ErrorDialog.Show("Failed to load save", ex != null ? (ex.Message + "\n" + ex.ToString()) : null);
					tabPanel.IsEnabled = false;
				}
				tabPanel.IsEnabled = true;
			}
		}

		private void RefreshClicked(object sender, RoutedEventArgs e)
		{
			CurrentSaveSelectionChanged(null, null);
		}

        private void JoinDiscordClicked(object sender, RoutedEventArgs e)
        {
            Process.Start("https://discord.gg/evYPhQm");
        }

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			Properties.Settings.Default.Save();
		}

		protected void SetPropertyField<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null)
		{
			if (!EqualityComparer<T>.Default.Equals(field, newValue))
			{
				field = newValue;
				PropertyChangedEventHandler handler = PropertyChanged;
				if (handler != null)
					handler(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
	}

}
