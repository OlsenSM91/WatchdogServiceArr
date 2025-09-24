namespace ServiceWatchdogArr
{
    partial class SettingsForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabGeneral;
        private System.Windows.Forms.TabPage tabApplications;
        private System.Windows.Forms.NumericUpDown numInterval;
        private System.Windows.Forms.ComboBox cmbUnit;
        private System.Windows.Forms.CheckBox chkAutoStart;
        private System.Windows.Forms.CheckBox chkGlobalMonitoring;
        private System.Windows.Forms.ListView lvApplications;
        private System.Windows.Forms.ColumnHeader colName;
        private System.Windows.Forms.ColumnHeader colService;
        private System.Windows.Forms.ColumnHeader colProcesses;
        private System.Windows.Forms.ColumnHeader colExecutable;
        private System.Windows.Forms.ColumnHeader colMonitoring;
        private System.Windows.Forms.Button btnAddApplication;
        private System.Windows.Forms.Button btnEditApplication;
        private System.Windows.Forms.Button btnRemoveApplication;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabGeneral = new System.Windows.Forms.TabPage();
            this.tabApplications = new System.Windows.Forms.TabPage();
            this.numInterval = new System.Windows.Forms.NumericUpDown();
            this.cmbUnit = new System.Windows.Forms.ComboBox();
            this.chkAutoStart = new System.Windows.Forms.CheckBox();
            this.chkGlobalMonitoring = new System.Windows.Forms.CheckBox();
            this.lvApplications = new System.Windows.Forms.ListView();
            this.colName = new System.Windows.Forms.ColumnHeader();
            this.colService = new System.Windows.Forms.ColumnHeader();
            this.colProcesses = new System.Windows.Forms.ColumnHeader();
            this.colExecutable = new System.Windows.Forms.ColumnHeader();
            this.colMonitoring = new System.Windows.Forms.ColumnHeader();
            this.btnAddApplication = new System.Windows.Forms.Button();
            this.btnEditApplication = new System.Windows.Forms.Button();
            this.btnRemoveApplication = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            var tableGeneral = new System.Windows.Forms.TableLayoutPanel();
            var intervalPanel = new System.Windows.Forms.FlowLayoutPanel();
            var appsLayout = new System.Windows.Forms.TableLayoutPanel();
            var appsButtons = new System.Windows.Forms.FlowLayoutPanel();
            var actionsPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.tabControl.SuspendLayout();
            this.tabGeneral.SuspendLayout();
            this.tabApplications.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numInterval)).BeginInit();
            tableGeneral.SuspendLayout();
            intervalPanel.SuspendLayout();
            appsLayout.SuspendLayout();
            appsButtons.SuspendLayout();
            actionsPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabGeneral);
            this.tabControl.Controls.Add(this.tabApplications);
            this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl.Location = new System.Drawing.Point(0, 0);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(800, 450);
            this.tabControl.TabIndex = 0;
            // 
            // tabGeneral
            // 
            this.tabGeneral.Controls.Add(tableGeneral);
            this.tabGeneral.Location = new System.Drawing.Point(4, 24);
            this.tabGeneral.Name = "tabGeneral";
            this.tabGeneral.Padding = new System.Windows.Forms.Padding(10);
            this.tabGeneral.Size = new System.Drawing.Size(792, 422);
            this.tabGeneral.TabIndex = 0;
            this.tabGeneral.Text = "General";
            this.tabGeneral.UseVisualStyleBackColor = true;
            // 
            // tabApplications
            // 
            this.tabApplications.Controls.Add(appsLayout);
            this.tabApplications.Location = new System.Drawing.Point(4, 24);
            this.tabApplications.Name = "tabApplications";
            this.tabApplications.Padding = new System.Windows.Forms.Padding(10);
            this.tabApplications.Size = new System.Drawing.Size(792, 422);
            this.tabApplications.TabIndex = 1;
            this.tabApplications.Text = "Applications";
            this.tabApplications.UseVisualStyleBackColor = true;
            // 
            // numInterval
            // 
            this.numInterval.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numInterval.Maximum = new decimal(new int[] { 10080, 0, 0, 0 });
            this.numInterval.Value = new decimal(new int[] { 5, 0, 0, 0 });
            this.numInterval.Width = 80;
            // 
            // cmbUnit
            // 
            this.cmbUnit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbUnit.FormattingEnabled = true;
            this.cmbUnit.Items.AddRange(new object[] { "Minutes", "Hours", "Days" });
            this.cmbUnit.Width = 120;
            // 
            // chkAutoStart
            // 
            this.chkAutoStart.AutoSize = true;
            this.chkAutoStart.Text = "Run ServiceWatchdogArr at Windows startup";
            // 
            // chkGlobalMonitoring
            // 
            this.chkGlobalMonitoring.AutoSize = true;
            this.chkGlobalMonitoring.Text = "Enable monitoring on launch";
            // 
            // lvApplications
            // 
            this.lvApplications.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
                this.colName,
                this.colService,
                this.colProcesses,
                this.colExecutable,
                this.colMonitoring });
            this.lvApplications.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lvApplications.FullRowSelect = true;
            this.lvApplications.HideSelection = false;
            this.lvApplications.MultiSelect = false;
            this.lvApplications.View = System.Windows.Forms.View.Details;
            this.lvApplications.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            // 
            // Columns
            // 
            this.colName.Text = "Name";
            this.colName.Width = 160;
            this.colService.Text = "Service";
            this.colService.Width = 140;
            this.colProcesses.Text = "Processes";
            this.colProcesses.Width = 180;
            this.colExecutable.Text = "Executable";
            this.colExecutable.Width = 220;
            this.colMonitoring.Text = "Monitoring";
            this.colMonitoring.Width = 120;
            // 
            // Buttons
            // 
            this.btnAddApplication.AutoSize = true;
            this.btnAddApplication.Text = "Add…";
            this.btnEditApplication.AutoSize = true;
            this.btnEditApplication.Text = "Edit…";
            this.btnRemoveApplication.AutoSize = true;
            this.btnRemoveApplication.Text = "Remove";
            this.btnSave.AutoSize = true;
            this.btnSave.Text = "Save";
            this.btnCancel.AutoSize = true;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            // 
            // tableGeneral
            // 
            tableGeneral.ColumnCount = 2;
            tableGeneral.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            tableGeneral.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            tableGeneral.Dock = System.Windows.Forms.DockStyle.Fill;
            tableGeneral.RowCount = 4;
            tableGeneral.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            tableGeneral.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            tableGeneral.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            tableGeneral.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableGeneral.Padding = new System.Windows.Forms.Padding(10);
            var lblInterval = new System.Windows.Forms.Label { Text = "Check interval:", AutoSize = true, Padding = new System.Windows.Forms.Padding(0, 6, 0, 0) };
            var lblGlobal = new System.Windows.Forms.Label { Text = "Monitoring:", AutoSize = true, Padding = new System.Windows.Forms.Padding(0, 6, 0, 0) };
            tableGeneral.Controls.Add(lblInterval, 0, 0);
            intervalPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            intervalPanel.AutoSize = true;
            intervalPanel.Controls.Add(this.numInterval);
            intervalPanel.Controls.Add(this.cmbUnit);
            tableGeneral.Controls.Add(intervalPanel, 1, 0);
            tableGeneral.Controls.Add(this.chkAutoStart, 1, 1);
            tableGeneral.Controls.Add(lblGlobal, 0, 2);
            tableGeneral.Controls.Add(this.chkGlobalMonitoring, 1, 2);
            // 
            // appsLayout
            // 
            appsLayout.ColumnCount = 2;
            appsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            appsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 140F));
            appsLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            appsLayout.RowCount = 1;
            appsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            appsLayout.Controls.Add(this.lvApplications, 0, 0);
            appsButtons.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            appsButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            appsButtons.AutoSize = true;
            appsButtons.Controls.Add(this.btnAddApplication);
            appsButtons.Controls.Add(this.btnEditApplication);
            appsButtons.Controls.Add(this.btnRemoveApplication);
            appsLayout.Controls.Add(appsButtons, 1, 0);
            // 
            // actionsPanel
            // 
            actionsPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            actionsPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            actionsPanel.Padding = new System.Windows.Forms.Padding(10);
            actionsPanel.AutoSize = true;
            actionsPanel.Controls.Add(this.btnSave);
            actionsPanel.Controls.Add(this.btnCancel);
            // 
            // SettingsForm
            // 
            this.AcceptButton = this.btnSave;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(800, 490);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(actionsPanel);
            this.MinimumSize = new System.Drawing.Size(620, 420);
            this.Name = "SettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ServiceWatchdogArr Settings";
            this.tabControl.ResumeLayout(false);
            this.tabGeneral.ResumeLayout(false);
            this.tabApplications.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.numInterval)).EndInit();
            tableGeneral.ResumeLayout(false);
            tableGeneral.PerformLayout();
            intervalPanel.ResumeLayout(false);
            intervalPanel.PerformLayout();
            appsLayout.ResumeLayout(false);
            appsLayout.PerformLayout();
            appsButtons.ResumeLayout(false);
            appsButtons.PerformLayout();
            actionsPanel.ResumeLayout(false);
            actionsPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
