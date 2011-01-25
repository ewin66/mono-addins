// 
// AddinInfoView.cs
//  
// Author:
//       Lluis Sanchez Gual <lluis@novell.com>
// 
// Copyright (c) 2011 Novell, Inc (http://www.novell.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using Mono.Addins.Setup;
using System.Text;
using Mono.Unix;

namespace Mono.Addins.Gui
{
	[System.ComponentModel.ToolboxItem(true)]
	partial class AddinInfoView : Gtk.Bin
	{
		List<AddinRepositoryEntry> selectedEntry = new List<AddinRepositoryEntry> ();
		List<Addin> selectedAddin = new List<Addin> ();
		SetupService service;
		
		public event EventHandler InstallClicked;
		public event EventHandler UninstallClicked;
		public event EventHandler UpdateClicked;
		public event EventHandler EnableDisableClicked;
		
		public AddinInfoView ()
		{
			this.Build ();
			AllowInstall = true;
			
			HeaderBox hb = new HeaderBox (1,1,1,1);
			hb.Show ();
			hb.Replace (this);
			
			hb = new HeaderBox (1,0,0,0);
			hb.SetPadding (6,6,6,6);
			hb.Show ();
			hb.GradientBackround = true;
			hb.Replace (eboxButs);
		}
		
		public void Init (SetupService service)
		{
			this.service = service;
		}
		
		public bool AllowInstall { get; set; }
		
		public List<AddinRepositoryEntry> SelectedEntries {
			get {
				return this.selectedEntry;
			}
		}

		public List<Addin> SelectedAddins {
			get {
				return this.selectedAddin;
			}
		}
		
		public void ShowAddins (object[] data)
		{
			selectedEntry.Clear ();
			selectedAddin.Clear ();
			eboxButs.Visible = true;
			
			if (data.Length == 1) {
				headerBox.Show ();
				ShowAddin (data[0]);
			}
			else if (data.Length > 1) {
				headerBox.Hide ();
				StringBuilder sb = new StringBuilder ();
				sb.Append (Catalog.GetString ("Multiple selection:\n\n"));
				bool allowUpdate = AllowInstall;
				bool allowInstall = true;
				bool allowUninstall = AllowInstall;
				bool allowEnable = true;
				bool allowDisable = true;
				
				foreach (object o in data) {
					Addin installed;
					if (o is Addin) {
						Addin a = (Addin)o;
						installed = a;
						selectedAddin.Add (a);
						sb.Append (a.Name);
					}
					else {
						AddinRepositoryEntry entry = (AddinRepositoryEntry) o;
						selectedEntry.Add (entry);
						sb.Append (entry.Addin.Name);
						installed = AddinManager.Registry.GetAddin (Addin.GetIdName (entry.Addin.Id));
					}
					if (installed != null) {
						if (GetUpdate (installed) == null)
							allowUpdate = false;
						allowInstall = false;
						if (installed.Enabled)
							allowEnable = false;
						else
							allowDisable = false;
					} else
						allowEnable = allowDisable = allowUninstall = allowUpdate = false;
					
					sb.Append ('\n');
					labelDesc.Text = sb.ToString ();
					
					if (allowEnable) {
						btnDisable.Visible = true;
						btnDisable.Label = Catalog.GetString ("Enable");
					} else if (allowDisable) {
						btnDisable.Visible = true;
						btnDisable.Label = Catalog.GetString ("Disable");
					} else
						btnDisable.Visible = false;
					btnInstall.Visible = allowInstall;
					btnUninstall.Visible = allowUninstall;
					btnUpdate.Visible = allowUpdate;
				}
			}
			else {
				headerBox.Hide ();
				btnDisable.Visible = false;
				btnInstall.Visible = false;
				btnUninstall.Visible = false;
				btnUpdate.Visible = false;
				eboxButs.Visible = false;
				labelDesc.Text = Catalog.GetString ("No selection");
			}
		}
		
		
		void ShowAddin (object data)
		{
			AddinHeader sinfo = null;
			Addin installed = null;
			AddinHeader updateInfo = null;
			
			if (data is Addin) {
				installed = (Addin) data;
				sinfo = SetupService.GetAddinHeader (installed);
				updateInfo = GetUpdate (installed);
				selectedEntry.Clear ();
			}
			else if (data is AddinRepositoryEntry) {
				sinfo = ((AddinRepositoryEntry)data).Addin;
				installed = AddinManager.Registry.GetAddin (Addin.GetIdName (sinfo.Id));
				if (installed != null && Addin.CompareVersions (installed.Version, sinfo.Version) > 0)
					updateInfo = sinfo;
				selectedEntry.Add ((AddinRepositoryEntry)data);
			} else
				selectedEntry.Clear ();
			
			selectedAddin.Add (installed);
			
			if (sinfo == null) {
				btnDisable.Visible = false;
				btnUninstall.Visible = false;
				btnUpdate.Visible = false;
			} else {
				string version;
				if (installed != null) {
					btnInstall.Visible = false;
					btnUpdate.Visible = updateInfo != null && AllowInstall;
					btnDisable.Visible = true;
					btnDisable.Label = installed.Enabled ? "Disable" : "Enable";
					btnDisable.Visible = installed.Description.CanDisable;
					btnUninstall.Visible = installed.Description.CanUninstall;
					version = installed.Version;
				} else {
					btnInstall.Visible = AllowInstall;
					btnUpdate.Visible = false;
					btnDisable.Visible = false;
					btnUninstall.Visible = false;
					version = sinfo.Version;
				}
				labelName.Markup = "<b><big>" + GLib.Markup.EscapeText(sinfo.Name) + "</big></b>";
				labelVersion.Text = "Version " + version;
				labelDesc.Text = sinfo.Description;
			}
		}
		
		public AddinHeader GetUpdate (Addin a)
		{
			AddinRepositoryEntry[] updates = service.Repositories.GetAvailableAddinUpdates (Addin.GetIdName (a.Id));
			AddinHeader best = null;
			string bestVersion = a.Version;
			foreach (AddinRepositoryEntry e in updates) {
				if (Addin.CompareVersions (bestVersion, e.Addin.Version) > 0) {
					best = e.Addin;
					bestVersion = e.Addin.Version;
				}
			}
			return best;
		}
		
		protected virtual void OnBtnInstallClicked (object sender, System.EventArgs e)
		{
			if (InstallClicked != null)
				InstallClicked (this, e);
		}
		
		protected virtual void OnBtnDisableClicked (object sender, System.EventArgs e)
		{
			if (EnableDisableClicked != null)
				EnableDisableClicked (this, e);
		}
		
		protected virtual void OnBtnUpdateClicked (object sender, System.EventArgs e)
		{
			if (UpdateClicked != null)
				UpdateClicked (this, e);
		}
		
		protected virtual void OnBtnUninstallClicked (object sender, System.EventArgs e)
		{
			if (UninstallClicked != null)
				UninstallClicked (this, e);
		}
		
		protected override void OnRealized ()
		{
			base.OnRealized ();
			HslColor gcol = ebox.Style.Background (Gtk.StateType.Normal);
			gcol.L -= 0.03;
			ebox.ModifyBg (Gtk.StateType.Normal, gcol);
			ebox2.ModifyBg (Gtk.StateType.Normal, gcol);
			scrolledwindow.ModifyBg (Gtk.StateType.Normal, gcol);
		}
	}
}

