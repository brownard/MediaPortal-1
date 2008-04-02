#region Copyright (C) 2005-2008 Team MediaPortal

/* 
 *	Copyright (C) 2005-2008 Team MediaPortal
 *	http://www.team-mediaportal.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *   
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *   
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA. 
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */

#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using System.Collections.Specialized;
using System.IO;
using Microsoft.Win32;

namespace MediaPortal.DeployTool
{
  public partial class InstallDlg : DeployDialog, IDeployDialog
  {
    public InstallDlg()
    {
      InitializeComponent();
      type=DialogType.Installation;
      PopulateListView();
      UpdateUI();
    }

    #region IDeployDialog interface
    public override void UpdateUI()
    {
      if (InstallationProperties.Instance["InstallType"] == "download_only")
      {
          labelHeading.Text = Localizer.Instance.GetString("Install_labelHeadingDownload");
          buttonInstall.Text = Localizer.Instance.GetString("Install_buttonDownload");
      }
      else
      {
          labelHeading.Text = Localizer.Instance.GetString("Install_labelHeadingInstall");
          buttonInstall.Text = Localizer.Instance.GetString("Install_buttonInstall");
      }
      listView.Columns[0].Text = Localizer.Instance.GetString("Install_colApplication");
      listView.Columns[1].Text = Localizer.Instance.GetString("Install_colState");
      listView.Columns[2].Text = Localizer.Instance.GetString("Install_colAction");
      labelSectionHeader.Text = "";
    }
    public override DeployDialog GetNextDialog()
    {
      return DialogFlowHandler.Instance.GetDialogInstance(DialogType.Finished);
    }
    public override bool SettingsValid()
    {
      if (!InstallationComplete())
      {
        Utils.ErrorDlg(Localizer.Instance.GetString("Install_errAppsMissing"));
        return false;
      }
      else
        return true;
    }
    public override void SetProperties()
    {
      InstallationProperties.Instance.Set("finished", "yes");
    }
    #endregion

    private bool InstallationComplete()
    {
      bool isComplete = true;
      foreach (ListViewItem item in listView.Items)
      {
        IInstallationPackage package = (IInstallationPackage)item.Tag;
        CheckResult result = package.CheckStatus();
        if (result.state != CheckState.INSTALLED && result.state != CheckState.DOWNLOADED)
        {
          isComplete = false;
          break;
        }
      }
      return isComplete;
    }
    private void AddPackageToListView(IInstallationPackage package)
    {
      ListViewItem item=listView.Items.Add(package.GetDisplayName());
      item.Tag = package;
      CheckResult result = package.CheckStatus();
      switch (result.state)
      {
        case CheckState.INSTALLED:
          item.SubItems.Add(Localizer.Instance.GetString("Install_stateInstalled"));
          item.SubItems.Add(Localizer.Instance.GetString("Install_actionNothing"));
          item.ForeColor = System.Drawing.Color.Green;
          break;
        case CheckState.NOT_INSTALLED:
          item.SubItems.Add(Localizer.Instance.GetString("Install_stateNotInstalled"));
          if (result.needsDownload)
            item.SubItems.Add(Localizer.Instance.GetString("Install_actionDownloadInstall"));
          else
            item.SubItems.Add(Localizer.Instance.GetString("Install_actionInstall"));
          item.ForeColor = System.Drawing.Color.Red;
          break;
        case CheckState.CONFIGURED:
            item.SubItems.Add(Localizer.Instance.GetString("Install_stateConfigured"));
            item.SubItems.Add(Localizer.Instance.GetString("Install_actionNothing"));
            item.ForeColor = System.Drawing.Color.Green;
          break;
        case CheckState.NOT_CONFIGURED:
            item.SubItems.Add(Localizer.Instance.GetString("Install_stateNotConfigured"));
            item.SubItems.Add(Localizer.Instance.GetString("Install_actionConfigure"));
            item.ForeColor = System.Drawing.Color.Red;
          break;
        case CheckState.REMOVED:
            item.SubItems.Add(Localizer.Instance.GetString("Install_stateRemoved"));
            item.SubItems.Add(Localizer.Instance.GetString("Install_actionNothing"));
            item.ForeColor = System.Drawing.Color.Green;
            break;
        case CheckState.NOT_REMOVED:
            item.SubItems.Add(Localizer.Instance.GetString("Install_stateUninstall"));
            item.SubItems.Add(Localizer.Instance.GetString("Install_actionRemove"));
            item.ForeColor = System.Drawing.Color.Red;
            break;
        case CheckState.DOWNLOADED:
            item.SubItems.Add(Localizer.Instance.GetString("Install_stateDownloaded"));
            item.SubItems.Add(Localizer.Instance.GetString("Install_actionNothing"));
            item.ForeColor = System.Drawing.Color.Green;
            break;
        case CheckState.NON_DOWNLOADED:
            item.SubItems.Add(Localizer.Instance.GetString("Install_stateNotDownloaded"));
            item.SubItems.Add(Localizer.Instance.GetString("Install_actionDownload"));
            item.ForeColor = System.Drawing.Color.Red;
            break;
        case CheckState.VERSION_MISMATCH:
          item.SubItems.Add(Localizer.Instance.GetString("Install_stateVersionMismatch"));
          if (result.needsDownload)
            item.SubItems.Add(Localizer.Instance.GetString("Install_actionUninstallDownloadInstall"));
          else
            item.SubItems.Add(Localizer.Instance.GetString("Install_actionUninstallInstall"));
          item.ForeColor = System.Drawing.Color.Purple;
          break;
      }
    }
    private void PopulateListView()
    {
        listView.Items.Clear();
        if (InstallationProperties.Instance["InstallType"] != "download_only")
            AddPackageToListView(new OldPackageChecker());
        AddPackageToListView(new DirectX9Checker());
        AddPackageToListView(new VCRedistChecker());
        switch(InstallationProperties.Instance["InstallType"])
        {            
            case "singleseat":
                        AddPackageToListView(new MediaPortalChecker());
                        if (InstallationProperties.Instance["DBMSType"] == "mssql2005")
                            AddPackageToListView(new MSSQLExpressChecker());
                        if (InstallationProperties.Instance["DBMSType"] == "mysql")
                            AddPackageToListView(new MySQLChecker());
                        AddPackageToListView(new TvServerChecker());
                        AddPackageToListView(new TvPluginServerChecker());
                        AddPackageToListView(new WindowsFirewallChecker());
                        break;

            case "tvserver_master":
                        if (InstallationProperties.Instance["DBMSType"] == "mssql2005")
                          AddPackageToListView(new MSSQLExpressChecker());
                        if (InstallationProperties.Instance["DBMSType"] == "mysql")
                          AddPackageToListView(new MySQLChecker());
                        AddPackageToListView(new TvServerChecker());
                        AddPackageToListView(new WindowsFirewallChecker());
                        break;

            case "tvserver_slave":
                        AddPackageToListView(new TvServerChecker());
                        AddPackageToListView(new WindowsFirewallChecker());
                        break;

            case "client":
                        AddPackageToListView(new MediaPortalChecker());
                        AddPackageToListView(new TvPluginServerChecker());
                        break;

            case "mp_only":
                        AddPackageToListView(new MediaPortalChecker());
                        break;

            case "download_only":
                        AddPackageToListView(new MediaPortalChecker());
                        AddPackageToListView(new MSSQLExpressChecker());
                        AddPackageToListView(new MySQLChecker());
                        AddPackageToListView(new TvServerChecker());
                        AddPackageToListView(new TvPluginServerChecker());
                        break;

      }
      listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
      if (InstallationComplete())
        buttonInstall.Enabled = false;
    }

    private void RequirementsDlg_ParentChanged(object sender, EventArgs e)
    {
      if (Parent != null)
        PopulateListView();
    }

    private bool PerformPackageAction(IInstallationPackage package,ListViewItem item)
    {
      CheckResult result = package.CheckStatus();
      if (result.state != CheckState.INSTALLED)
      {
        switch (result.state)
        {
          case CheckState.NOT_INSTALLED:
            if (result.needsDownload)
            {
              item.SubItems[1].Text=Localizer.Instance.GetString("Install_msgDownloading");
              Update();
              if (!package.Download())
              {
                
                Utils.ErrorDlg(string.Format(Localizer.Instance.GetString("Install_errInstallFailed"),package.GetDisplayName()));
                return false;
              }
            }
            item.SubItems[1].Text = Localizer.Instance.GetString("Install_msgInstalling");
            Update();
            if (!package.Install())
            {
              Utils.ErrorDlg(string.Format(Localizer.Instance.GetString("Install_errInstallFailed"), package.GetDisplayName()));
              return false;
            }
            break;
          case CheckState.NOT_CONFIGURED:
            item.SubItems[1].Text = Localizer.Instance.GetString("Install_msgConfiguring");
            Update();
            if (!package.Install())
            {
                Utils.ErrorDlg(string.Format(Localizer.Instance.GetString("Install_errConfigureFailed"), package.GetDisplayName()));
                return false;
            }
            break;
          case CheckState.REMOVED:
            item.SubItems[1].Text = Localizer.Instance.GetString("Install_msgUninstalling");
            Update();
            if (!package.Install())
            {
                Utils.ErrorDlg(string.Format(Localizer.Instance.GetString("Install_errRemoveFailed"), package.GetDisplayName()));
                return false;
            }
            break;
          case CheckState.VERSION_MISMATCH:
            item.SubItems[1].Text = Localizer.Instance.GetString("Install_msgUninstalling");
            Update();
            if (!package.UnInstall())
            {
              Utils.ErrorDlg(string.Format(Localizer.Instance.GetString("Install_errUinstallFailed"), package.GetDisplayName()));
              return false;
            }
            if (result.needsDownload)
            {
              item.SubItems[1].Text = Localizer.Instance.GetString("Install_msgDownloading");
              Update();
              if (!package.Download())
              {
                Utils.ErrorDlg(string.Format(Localizer.Instance.GetString("Install_errDownloadFailed"), package.GetDisplayName()));
                return false;
              }
            }
            item.SubItems[1].Text = Localizer.Instance.GetString("Install_msgInstalling");
            Update();
            if (!package.Install())
            {
              Utils.ErrorDlg(string.Format(Localizer.Instance.GetString("Install_errInstallFailed"), package.GetDisplayName()));
              return false;
            }
            break;
          case CheckState.NON_DOWNLOADED:
            item.SubItems[1].Text = Localizer.Instance.GetString("Install_msgDownloading");
            Update();
            if (!package.Download())
            {
                Utils.ErrorDlg(string.Format(Localizer.Instance.GetString("Install_errDownloadFailed"), package.GetDisplayName()));
                return false;
            }
            break;
        }
      }
      return true;
    }
    private void buttonInstall_Click(object sender, EventArgs e)
    {
      foreach (ListViewItem item in listView.Items)
      {
        IInstallationPackage package = (IInstallationPackage)item.Tag;
        if (!PerformPackageAction(package,item))
          break;
      }
      PopulateListView();
    }
  }
}
