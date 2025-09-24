using System;
using System.Windows.Forms;

namespace ServiceWatchdogArr
{
    public partial class SettingsForm : Form
    {
        private WatchdogConfig _config;

        public SettingsForm()
        {
            InitializeComponent();
            _config = WatchdogConfig.Load();

            numInterval.Value = Math.Max(1, _config.Interval.Value);
            cmbUnit.Items.Clear();
            cmbUnit.Items.AddRange(new object[] { "Minutes", "Hours", "Days" });
            cmbUnit.SelectedItem = string.IsNullOrWhiteSpace(_config.Interval.Unit) ? "Minutes" : _config.Interval.Unit;

            chkAutoStart.Checked = _config.AutoStart || AutoStartHelper.IsEnabled();
        }

        private void btnSave_Click(object? sender, EventArgs e)
        {
            _config.Interval.Value = (int)numInterval.Value;
            _config.Interval.Unit = cmbUnit.SelectedItem?.ToString() ?? "Minutes";
            _config.AutoStart = chkAutoStart.Checked;

            if (chkAutoStart.Checked) AutoStartHelper.Enable(Application.ExecutablePath);
            else AutoStartHelper.Disable();

            _config.Save();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}