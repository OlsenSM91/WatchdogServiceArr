using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Windows.Forms;

namespace ServiceWatchdogArr
{
    internal sealed class ApplicationEditorForm : Form
    {
        private readonly TextBox _txtName = new TextBox();
        private readonly TextBox _txtService = new TextBox();
        private readonly TextBox _txtExecutable = new TextBox();
        private readonly TextBox _txtProcessInput = new TextBox();
        private readonly ListBox _lstProcesses = new ListBox();
        private readonly CheckBox _chkMonitoring = new CheckBox();
        private readonly ListView _lvServices = new ListView();
        private readonly ListView _lvProcesses = new ListView();
        private readonly Button _btnAddProcess = new Button();
        private readonly Button _btnRemoveProcess = new Button();
        private readonly Button _btnBrowseExecutable = new Button();
        private readonly Button _btnRefresh = new Button();
        private readonly Button _btnSave = new Button();
        private readonly Button _btnCancel = new Button();

        public ApplicationEditorForm()
        {
            InitializeComponent();
            LoadLists();
        }

        public ApplicationEditorForm(MonitoredApplication application)
            : this()
        {
            if (application != null)
            {
                _txtName.Text = application.Name;
                _txtService.Text = application.ServiceName;
                _txtExecutable.Text = application.ExecutablePath;
                _chkMonitoring.Checked = application.MonitoringEnabled;
                foreach (string process in application.ProcessNames)
                {
                    _lstProcesses.Items.Add(process);
                }
            }
        }

        public MonitoredApplication GetApplication()
        {
            var application = new MonitoredApplication
            {
                Name = _txtName.Text.Trim(),
                ServiceName = _txtService.Text.Trim(),
                ExecutablePath = _txtExecutable.Text.Trim(),
                MonitoringEnabled = _chkMonitoring.Checked,
                ProcessNames = _lstProcesses.Items.Cast<string>().Select(ProcessNameHelper.Normalize).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
            application.Normalize();
            return application;
        }

        private void InitializeComponent()
        {
            Text = "Application";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            Width = 800;
            Height = 520;

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2
            };
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            Controls.Add(rootLayout);

            var detailsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(10)
            };
            detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblName = new Label { Text = "Friendly name:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
            var lblService = new Label { Text = "Service name:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
            var lblExecutable = new Label { Text = "Executable path:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
            var lblProcesses = new Label { Text = "Process names:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) };

            _chkMonitoring.Text = "Enable monitoring";
            _chkMonitoring.Checked = true;

            _btnBrowseExecutable.Text = "Browse…";
            _btnBrowseExecutable.AutoSize = true;
            _btnBrowseExecutable.Click += (_, _) => BrowseExecutable();

            var executablePanel = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill };
            executablePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            executablePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            executablePanel.Controls.Add(_txtExecutable, 0, 0);
            executablePanel.Controls.Add(_btnBrowseExecutable, 1, 0);

            _lstProcesses.SelectionMode = SelectionMode.MultiExtended;
            _lstProcesses.Height = 120;

            _btnAddProcess.Text = "Add";
            _btnAddProcess.AutoSize = true;
            _btnAddProcess.Click += (_, _) => AddProcessFromText();

            _btnRemoveProcess.Text = "Remove";
            _btnRemoveProcess.AutoSize = true;
            _btnRemoveProcess.Click += (_, _) => RemoveSelectedProcesses();

            var processInputPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                Dock = DockStyle.Fill,
                AutoSize = true
            };
            _txtProcessInput.Width = 200;
            processInputPanel.Controls.Add(_txtProcessInput);
            processInputPanel.Controls.Add(_btnAddProcess);
            processInputPanel.Controls.Add(_btnRemoveProcess);

            detailsLayout.Controls.Add(lblName, 0, 0);
            detailsLayout.Controls.Add(_txtName, 1, 0);
            detailsLayout.Controls.Add(lblService, 0, 1);
            detailsLayout.Controls.Add(_txtService, 1, 1);
            detailsLayout.Controls.Add(lblExecutable, 0, 2);
            detailsLayout.Controls.Add(executablePanel, 1, 2);
            detailsLayout.Controls.Add(lblProcesses, 0, 3);
            detailsLayout.Controls.Add(_lstProcesses, 1, 3);
            detailsLayout.Controls.Add(processInputPanel, 1, 4);
            detailsLayout.Controls.Add(_chkMonitoring, 1, 5);

            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.Controls.Add(detailsLayout, 0, 0);

            var discoveryLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(10)
            };
            discoveryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            discoveryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            discoveryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _lvServices.View = View.Details;
            _lvServices.FullRowSelect = true;
            _lvServices.MultiSelect = false;
            _lvServices.HideSelection = false;
            _lvServices.Columns.Add("Service", 180);
            _lvServices.Columns.Add("Name", 160);
            _lvServices.Dock = DockStyle.Fill;
            _lvServices.DoubleClick += (_, _) => ApplySelectedService();

            _lvProcesses.View = View.Details;
            _lvProcesses.FullRowSelect = true;
            _lvProcesses.MultiSelect = false;
            _lvProcesses.HideSelection = false;
            _lvProcesses.Columns.Add("Process", 180);
            _lvProcesses.Columns.Add("PID", 80);
            _lvProcesses.Dock = DockStyle.Fill;
            _lvProcesses.DoubleClick += (_, _) => AddSelectedProcess();

            _btnRefresh.Text = "Refresh";
            _btnRefresh.AutoSize = true;
            _btnRefresh.Click += (_, _) => LoadLists();

            discoveryLayout.Controls.Add(_lvServices, 0, 0);
            discoveryLayout.Controls.Add(_lvProcesses, 0, 1);
            discoveryLayout.Controls.Add(_btnRefresh, 0, 2);

            rootLayout.Controls.Add(discoveryLayout, 1, 0);

            var actionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10),
                AutoSize = true
            };

            _btnSave.Text = "OK";
            _btnSave.AutoSize = true;
            _btnSave.Click += (_, _) => OnSave();

            _btnCancel.Text = "Cancel";
            _btnCancel.AutoSize = true;
            _btnCancel.DialogResult = DialogResult.Cancel;

            actionsPanel.Controls.Add(_btnSave);
            actionsPanel.Controls.Add(_btnCancel);
            Controls.Add(actionsPanel);

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;
        }

        private void BrowseExecutable()
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                Title = "Select executable"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _txtExecutable.Text = dialog.FileName;
            }
        }

        private void AddProcessFromText()
        {
            string process = ProcessNameHelper.Normalize(_txtProcessInput.Text);
            if (string.IsNullOrWhiteSpace(process))
            {
                return;
            }

            if (!_lstProcesses.Items.Contains(process))
            {
                _lstProcesses.Items.Add(process);
            }

            _txtProcessInput.Clear();
        }

        private void RemoveSelectedProcesses()
        {
            var items = _lstProcesses.SelectedItems.Cast<object>().ToList();
            foreach (var item in items)
            {
                _lstProcesses.Items.Remove(item);
            }
        }

        private void ApplySelectedService()
        {
            if (_lvServices.SelectedItems.Count != 1)
            {
                return;
            }

            var item = _lvServices.SelectedItems[0];
            string serviceName = item.SubItems[1].Text;
            string displayName = item.SubItems[0].Text;
            _txtService.Text = serviceName;
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                _txtName.Text = displayName;
            }
        }

        private void AddSelectedProcess()
        {
            if (_lvProcesses.SelectedItems.Count != 1)
            {
                return;
            }

            string processName = _lvProcesses.SelectedItems[0].SubItems[0].Text;
            string normalized = ProcessNameHelper.Normalize(processName);
            if (!_lstProcesses.Items.Contains(normalized))
            {
                _lstProcesses.Items.Add(normalized);
            }
        }

        private void LoadLists()
        {
            LoadServices();
            LoadProcesses();
        }

        private void LoadServices()
        {
            _lvServices.BeginUpdate();
            _lvServices.Items.Clear();
            try
            {
                foreach (ServiceController service in ServiceController.GetServices())
                {
                    var item = new ListViewItem(service.DisplayName);
                    item.SubItems.Add(service.ServiceName);
                    _lvServices.Items.Add(item);
                }
            }
            catch
            {
                // Ignore enumeration errors.
            }
            finally
            {
                _lvServices.EndUpdate();
            }
        }

        private void LoadProcesses()
        {
            _lvProcesses.BeginUpdate();
            _lvProcesses.Items.Clear();
            try
            {
                foreach (Process process in Process.GetProcesses())
                {
                    try
                    {
                        var item = new ListViewItem(process.ProcessName);
                        item.SubItems.Add(process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        _lvProcesses.Items.Add(item);
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch
            {
                // Ignore enumeration errors.
            }
            finally
            {
                _lvProcesses.EndUpdate();
            }
        }

        private void OnSave()
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show(this, "A friendly name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtService.Text) && _lstProcesses.Items.Count == 0)
            {
                var confirm = MessageBox.Show(this, "No service or process names are specified. Continue?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    return;
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
