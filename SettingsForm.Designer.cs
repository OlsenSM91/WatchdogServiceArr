using System.Windows.Forms;

namespace ServiceWatchdogArr
{
    partial class SettingsForm
    {
        private System.ComponentModel.IContainer components = null!;
        private NumericUpDown numInterval = null!;
        private ComboBox cmbUnit = null!;
        private CheckBox chkAutoStart = null!;
        private Button btnSave = null!;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.numInterval = new NumericUpDown();
            this.cmbUnit = new ComboBox();
            this.chkAutoStart = new CheckBox();
            this.btnSave = new Button();

            ((System.ComponentModel.ISupportInitialize)(this.numInterval)).BeginInit();
            this.SuspendLayout();

            // numInterval
            this.numInterval.Location = new System.Drawing.Point(20, 20);
            this.numInterval.Minimum = 1;
            this.numInterval.Maximum = 100000;
            this.numInterval.Value = 5;
            this.numInterval.Size = new System.Drawing.Size(80, 23);

            // cmbUnit
            this.cmbUnit.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbUnit.Items.AddRange(new object[] { "Minutes", "Hours", "Days" });
            this.cmbUnit.Location = new System.Drawing.Point(110, 20);
            this.cmbUnit.SelectedIndex = 0;
            this.cmbUnit.Size = new System.Drawing.Size(120, 23);

            // chkAutoStart
            this.chkAutoStart.Text = "Run at Windows startup";
            this.chkAutoStart.Location = new System.Drawing.Point(20, 60);
            this.chkAutoStart.Size = new System.Drawing.Size(200, 24);

            // btnSave
            this.btnSave.Text = "Save";
            this.btnSave.Location = new System.Drawing.Point(20, 100);
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);

            // SettingsForm
            this.ClientSize = new System.Drawing.Size(260, 150);
            this.Controls.Add(this.numInterval);
            this.Controls.Add(this.cmbUnit);
            this.Controls.Add(this.chkAutoStart);
            this.Controls.Add(this.btnSave);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "ServiceWatchdogArr Settings";

            ((System.ComponentModel.ISupportInitialize)(this.numInterval)).EndInit();
            this.ResumeLayout(false);
        }
    }
}