namespace PCRBay
{
    partial class frmWipeCard
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.CbBayVehicleId = new System.Windows.Forms.ComboBox();
            this.Label1 = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.Label18 = new System.Windows.Forms.Label();
            this.CbBayCrId = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // CbBayVehicleId
            // 
            this.CbBayVehicleId.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbBayVehicleId.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.CbBayVehicleId.FormattingEnabled = true;
            this.CbBayVehicleId.Location = new System.Drawing.Point(167, 59);
            this.CbBayVehicleId.Name = "CbBayVehicleId";
            this.CbBayVehicleId.Size = new System.Drawing.Size(139, 24);
            this.CbBayVehicleId.TabIndex = 2;
            // 
            // Label1
            // 
            this.Label1.AutoSize = true;
            this.Label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(222)));
            this.Label1.ForeColor = System.Drawing.SystemColors.MenuText;
            this.Label1.Location = new System.Drawing.Point(27, 59);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(131, 20);
            this.Label1.TabIndex = 54;
            this.Label1.Text = "หมายเลขบัตรของรถ";
            this.Label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(167, 118);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(139, 41);
            this.button1.TabIndex = 55;
            this.button1.Text = "Wipe Card";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // Label18
            // 
            this.Label18.AutoSize = true;
            this.Label18.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(222)));
            this.Label18.ForeColor = System.Drawing.SystemColors.MenuText;
            this.Label18.Location = new System.Drawing.Point(6, 16);
            this.Label18.Name = "Label18";
            this.Label18.Size = new System.Drawing.Size(152, 20);
            this.Label18.TabIndex = 57;
            this.Label18.Text = "หมายเลขเครื่องอ่านบัตร";
            this.Label18.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // CbBayCrId
            // 
            this.CbBayCrId.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbBayCrId.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.CbBayCrId.FormattingEnabled = true;
            this.CbBayCrId.Location = new System.Drawing.Point(167, 12);
            this.CbBayCrId.Name = "CbBayCrId";
            this.CbBayCrId.Size = new System.Drawing.Size(139, 24);
            this.CbBayCrId.TabIndex = 56;
            // 
            // frmWipeCard
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(421, 171);
            this.Controls.Add(this.Label18);
            this.Controls.Add(this.CbBayCrId);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.CbBayVehicleId);
            this.MaximizeBox = false;
            this.Name = "frmWipeCard";
            this.Text = "Wipe Card";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        internal System.Windows.Forms.ComboBox CbBayVehicleId;
        internal System.Windows.Forms.Label Label1;
        private System.Windows.Forms.Button button1;
        internal System.Windows.Forms.Label Label18;
        internal System.Windows.Forms.ComboBox CbBayCrId;
    }
}