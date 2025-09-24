using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace ServiceWatchdogArr
{
    public partial class SettingsForm : Form
    {
        private readonly WatchdogConfig _config;

        public event Action<WatchdogConfig> SettingsSaved;

        public SettingsForm(WatchdogConfig config)
        {
            InitializeComponent();
            _config = config?.Clone() ?? throw new ArgumentNullException(nameof(config));
            LoadConfig();
            HookEvents();
        }

        private void LoadConfig()
        {
            numInterval.Value = Math.Clamp(_config.Interval.Value, 1, 10080);
            string unitText = _config.Interval.Unit.ToString();
            int unitIndex = cmbUnit.Items.IndexOf(unitText);
            cmbUnit.SelectedIndex = unitIndex >= 0 ? unitIndex : 0;
            chkAutoStart.Checked = _config.AutoStart || AutoStartHelper.IsEnabled();
            chkGlobalMonitoring.Checked = _config.GlobalMonitoringEnabled;
            PopulateApplicationList();
        }

        private void HookEvents()
        {
            btnSave.Click += (_, _) => SaveAndClose();
            btnCancel.Click += (_, _) => Close();
            btnAddApplication.Click += (_, _) => AddApplication();
            btnEditApplication.Click += (_, _) => EditSelectedApplication();
            btnRemoveApplication.Click += (_, _) => RemoveSelectedApplication();
            lvApplications.DoubleClick += (_, _) => EditSelectedApplication();
            lvApplications.SelectedIndexChanged += (_, _) => UpdateApplicationButtons();
            UpdateApplicationButtons();
        }

        private void PopulateApplicationList()
        {
            lvApplications.Items.Clear();
            foreach (MonitoredApplication application in _config.Applications)
            {
                var item = new ListViewItem(application.Name)
                {
                    Tag = application
                };
                item.SubItems.Add(string.IsNullOrWhiteSpace(application.ServiceName) ? "-" : application.ServiceName);
                item.SubItems.Add(application.ProcessNames.Count == 0 ? "-" : string.Join(", ", application.ProcessNames));
                item.SubItems.Add(string.IsNullOrWhiteSpace(application.ExecutablePath) ? "-" : application.ExecutablePath);
                item.SubItems.Add(application.MonitoringEnabled ? "Enabled" : "Disabled");
                lvApplications.Items.Add(item);
            }
        }

        private void UpdateApplicationButtons()
        {
            bool hasSelection = lvApplications.SelectedItems.Count == 1;
            btnEditApplication.Enabled = hasSelection;
            btnRemoveApplication.Enabled = hasSelection;
        }

        private void AddApplication()
        {
            using var editor = new ApplicationEditorForm();
            if (editor.ShowDialog(this) == DialogResult.OK)
            {
                MonitoredApplication application = editor.GetApplication();
                if (_config.Applications.Any(app => string.Equals(app.Name, application.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show(this, "An application with this name already exists.", "Duplicate Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _config.Applications.Add(application);
                PopulateApplicationList();
            }
        }

        private void EditSelectedApplication()
        {
            if (lvApplications.SelectedItems.Count != 1)
            {
                return;
            }

            var item = lvApplications.SelectedItems[0];
            var application = (MonitoredApplication)item.Tag;

            using var editor = new ApplicationEditorForm(application);
            if (editor.ShowDialog(this) == DialogResult.OK)
            {
                MonitoredApplication updated = editor.GetApplication();
                if (_config.Applications.Any(app => !ReferenceEquals(app, application) && string.Equals(app.Name, updated.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show(this, "An application with this name already exists.", "Duplicate Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                application.Name = updated.Name;
                application.ServiceName = updated.ServiceName;
                application.ExecutablePath = updated.ExecutablePath;
                application.ProcessNames = new List<string>(updated.ProcessNames);
                application.MonitoringEnabled = updated.MonitoringEnabled;
                PopulateApplicationList();
            }
        }

        private void RemoveSelectedApplication()
        {
            if (lvApplications.SelectedItems.Count != 1)
            {
                return;
            }

            var item = lvApplications.SelectedItems[0];
            var application = (MonitoredApplication)item.Tag;
            var confirm = MessageBox.Show(this, $"Remove {application.Name} from monitoring?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                _config.Applications.Remove(application);
                PopulateApplicationList();
            }
        }

        private void SaveAndClose()
        {
            _config.Interval.Value = (int)numInterval.Value;
            if (!Enum.TryParse(cmbUnit.SelectedItem?.ToString(), out IntervalUnit unit))
            {
                unit = IntervalUnit.Minutes;
            }
            _config.Interval.Unit = unit;
            _config.AutoStart = chkAutoStart.Checked;
            _config.GlobalMonitoringEnabled = chkGlobalMonitoring.Checked;

            if (_config.AutoStart)
            {
                AutoStartHelper.Enable(Application.ExecutablePath);
            }
            else
            {
                AutoStartHelper.Disable();
            }

            _config.Normalize();
            SettingsSaved?.Invoke(_config.Clone());
            Close();
        }
    }
}
