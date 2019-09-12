﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Speech.Synthesis;

namespace Observatory
{
    public partial class ObservatoryFrm : Form
    {
        private LogMonitor logMonitor;
        public SpeechSynthesizer speech;
        private SettingsFrm settingsFrm;
        public bool settingsOpen = false;
        
        public ObservatoryFrm()
        {
            InitializeComponent();
            logMonitor = new LogMonitor("");
            logMonitor.LogEntry += LogEvent;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            notifyIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            notifyIcon.Visible = Properties.Observatory.Default.Notify;
            if (Properties.Observatory.Default.TTS)
            {
                speech = new SpeechSynthesizer();
                speech.SetOutputToDefaultAudioDevice();
            }
        }

        public bool Notify
        {
            get
            {
                return notifyIcon.Visible;
            }
            set
            {
                notifyIcon.Visible = value;
            }
        }

        private void BtnToggleMonitor_Click(object sender, EventArgs e)
        {
            if (logMonitor.IsMonitoring())
            {
                logMonitor.MonitorStop();
                btnToggleMonitor.Text = "Start Monitoring";
            }
            else
            {
                logMonitor.MonitorStart();
                btnToggleMonitor.Text = "Stop Monitoring";
            }
        }

        private void LogEvent(object source, EventArgs e)
        {
            if (logMonitor.LastScanValid)
            {
                ScanReader scan = new ScanReader(logMonitor);

                if (scan.IsInteresting())
                {
                    Invoke((MethodInvoker)delegate ()
                    {
                        if (!logMonitor.ReadAllInProgress && listEvent.Items[listEvent.Items.Count - 1].SubItems[1].Text == "Uninteresting")
                        {
                            listEvent.Items.RemoveAt(listEvent.Items.Count - 1);
                        }
                        bool addItem;
                        var pendingRemoval = new List<(string BodyName, string Description, string Detail)>();
                        foreach (var item in scan.Interest)
                        {
                            addItem = true;
                            if (item.Description == "All jumponium materials in system")
                            {
                                for (int i = Math.Max(0, listEvent.Items.Count - 10); i < listEvent.Items.Count; i++)
                                {
                                    if (listEvent.Items[i].SubItems[0].Text.Contains(logMonitor.CurrentSystem) && listEvent.Items[i].SubItems[1].Text == "All jumponium materials in system")
                                    {
                                        addItem = false;
                                    }
                                }
                            }

                            if (addItem)
                            {
                                AddListItem(item);
                            }
                            else
                            {
                                pendingRemoval.Add(item);
                            }
                        }
                        foreach (var item in pendingRemoval)
                        {
                            scan.Interest.Remove(item);
                        }
                        if (!logMonitor.ReadAllInProgress && scan.Interest.Count > 0) { AnnounceItems(scan.Interest); }
                    });
                }
                else if (!logMonitor.ReadAllInProgress)
                {

                    ListViewItem newItem = new ListViewItem(new string[] { scan.Interest[0].BodyName, "Uninteresting" })
                    {
                        UseItemStyleForSubItems = false
                    };
                    Invoke((MethodInvoker)delegate () {
                    if (listEvent.Items.Count > 0 && listEvent.Items[listEvent.Items.Count - 1].SubItems[1].Text == "Uninteresting")
                    {
                        listEvent.Items.RemoveAt(listEvent.Items.Count - 1); 
                    }

                    newItem.SubItems[0].ForeColor = Color.DarkGray;
                    newItem.SubItems[1].ForeColor = Color.DarkGray;
                    listEvent.Items.Add(newItem).EnsureVisible(); });
                }
            }
        }

        private void AnnounceItems(List<(string BodyName, string Description, string Detail)> items)
        {
            if (Properties.Observatory.Default.Notify || Properties.Observatory.Default.TTS)
            {
                notifyIcon.BalloonTipTitle = "Discovery:";
                string fullSystemName = items[0].BodyName;
                StringBuilder announceText = new StringBuilder();
                
                foreach (var item in items)
                {
                    announceText.Append(item.Description);
                    if (!item.Equals(items.Last()))
                    {
                        announceText.AppendLine(", ");
                    }
                }
                if (Properties.Observatory.Default.Notify)
                {
                    notifyIcon.BalloonTipText = fullSystemName + ": " + announceText.ToString();
                    notifyIcon.ShowBalloonTip(3000);
                }
                if (Properties.Observatory.Default.TTS)
                {
                    string sector = fullSystemName.Substring(0, fullSystemName.IndexOf('-') - 2);
                    string system = fullSystemName.Remove(0, sector.Length).Replace('-', '–'); //Want it to say "dash", not "hyphen".

                    speech.SpeakSsmlAsync($"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">{sector}<say-as interpret-as=\"spell-out\">{system}</say-as><break strength=\"weak\"/>{announceText}</speak>");
                }
            }
        }

        private void AddListItem((string BodyName, string Description, string Detail) item)
        {
            ListViewItem newItem = new ListViewItem(
                                        new string[] {
                                        item.BodyName,
                                        item.Description,
                                        logMonitor.LastScan.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                                        item.Detail,
                                        (logMonitor.LastScan.Landable.GetValueOrDefault(false) && !(item.Description == "All jumponium materials in system")) ? "🌐" : string.Empty
                                        });
            if (item.Description.Contains("Interesting Object"))
            {
                newItem.UseItemStyleForSubItems = false;
                newItem.SubItems[1].Font = new Font(newItem.Font, FontStyle.Bold);
            }

            listEvent.Items.Add(newItem).EnsureVisible();
        }

        private void BtnReadAll_Click(object sender, EventArgs e)
        {
            if (logMonitor.ReadAllComplete)
            {
                DialogResult confirmResult;
                confirmResult = MessageBox.Show("This will clear the current list and re-read all journal logs. Contine?", "Confirm Refresh", MessageBoxButtons.OKCancel);
                if (confirmResult == DialogResult.OK)
                {
                    listEvent.Items.Clear();
                }
                else
                {
                    return;
                }
            }
            listEvent.BeginUpdate();
            logMonitor.ReadAll(progressReadAll);
            listEvent.EndUpdate();
        }

        private void ObservatoryFrm_FormClosing(object sender, FormClosingEventArgs e)
        {
            notifyIcon.Icon = null;
            speech?.Dispose();
        }

        private void ListEvent_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (listEvent.FocusedItem.Bounds.Contains(e.Location))
                {
                    contextCopy.Show(Cursor.Position);
                    contextCopy.Items[0].Enabled = listEvent.SelectedItems.Count == 1;
                }
            }
        }

        private void CopyNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(listEvent.FocusedItem.Text);
        }

        private void CopyAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyAllSelected();
        }

        private void ListEvent_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.C && e.Control)
            {
                CopyAllSelected();
            }
        }

        private void CopyAllSelected()
        {
            StringBuilder copyText = new StringBuilder();
            foreach (ListViewItem item in listEvent.SelectedItems)
            {
                if (item.SubItems.Count == 5)
                {
                    copyText.AppendLine(
                        Properties.Observatory.Default.CopyTemplate
                            .Replace("%body%", item.SubItems[0].Text)
                            .Replace("%bodyL%", item.SubItems[0].Text + (item.SubItems[4].Text.Length > 0 ? "- Landable" : string.Empty))
                            .Replace("%info%", item.SubItems[1].Text)
                            .Replace("%time%", item.SubItems[2].Text)
                            .Replace("%detail%", item.SubItems[3].Text)
                            );
                        //item.SubItems[2].Text + " - " +
                        //item.SubItems[0].Text + " - " +
                        //(item.SubItems[4].Text.Length > 0 ? "Landable - " : string.Empty) +
                        //item.SubItems[1].Text +
                        //(item.SubItems[3].Text.Length > 0 ? " - " + item.SubItems[3].Text : string.Empty)
                        //);
                }
                else
                {
                    copyText.AppendLine(item.SubItems[0].Text + " - Uninteresting");
                }
                    

            }
            Clipboard.SetText(copyText.ToString());
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            if (!settingsOpen)
            {
                settingsFrm = new SettingsFrm(this);
                settingsFrm.Show();
                settingsOpen = true;
            }
            else
            {
                settingsFrm.Activate();
            }
        }
    }
}