using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

namespace PCRBay
{
    public partial class frmCRBay : Form
    {
        struct _CR
        {
            public string BayNo;
            public string BayName;
            public string Name;
            public Int16 Address;
            public Int32 ComportID;
            public string ComportNo;
            public string ComportSetting;
        }

        _CR mCR;
        _CR[] mCRBay;
        public cOracle mOraDb;
        public IniLib.CINI mIni=new IniLib.CINI();
        cCRProcess[] mCRProcess;
        //clogfile PLog;

        string mNodeName;
        int mSelectNodeIndex;
        int mBayIndex;
        int mCRIndex;

        #region Thread
        bool mConnect;
        bool mShutdown;
        bool mRunning;

        string mBayNo;
        string mCRName;
        string[] args;
        Thread mThread;

        private void StartThread()
        {
            System.Threading.Thread.Sleep(1000);
            timer1.Enabled = true;
            mOraDb = new cOracle(this);
            //mCRProcess = new cCRProcess[mCRBay.Length];
            //for (int i = 0; i < mCRBay.Length; i++)
            //{
            //    mCRProcess[i] = new cCRProcess(this);
            //}

                    //try
                    //{
                    //    if (mRunning)
                    //    {
                    //        return;
                    //    }
            mThread = new Thread(this.RunProcess);
            mThread.Name = this.Text;
            mThread.Start();
                    //}
                    //catch (Exception exp)
                    //{
                    //    mRunning = false;
                    //}
        }

        private void RunProcess()
        {
            while (!mRunning)
            {
                if (mOraDb.ConnectStatus())
                {
                    mRunning = true;
                    Thread.Sleep(1000);
                    InitialObject();
                    mCRProcess = new cCRProcess[mCRBay.Length];
                    for (int i = 0; i < mCRBay.Length; i++)
                    {
                        mCRProcess[i] = new cCRProcess(this);
                    }

                    for (int i = 0; i < mCRBay.Length; i++)
                    {
                        mCRProcess[i].InitialProcess(mCRBay[i].ComportID);
                    }
                   
                    //mThread.Abort();
                }
                //mThread.Abort();
                Thread.Sleep(1000);
            }
            
        }
        #endregion

        #region ListboxItem

        public void DisplayMessage(string pFileName, string iMsg)
        {
            if (this.lstMain.InvokeRequired)
            {
                // This is a worker thread so delegate the task.
                if (lstMain.Items.Count > 1000)
                    lstMain.Items.Clear();

                this.lstMain.Invoke(new DisplayMessageDelegate(this.DisplayMessage), pFileName, iMsg);
            }
            else
            {
                // This is the UI thread so perform the task.
                if (iMsg != null)
                {
                    if (lstMain.Items.Count > 1000)
                        lstMain.Items.Clear();

                    this.lstMain.Items.Insert(0, iMsg);
                    //logfile.WriteLog("System", item.ToString());
                    //PLog.WriteLog(pFileName, iMsg);
                }
            }
        }

        private delegate void DisplayMessageDelegate(string pFileName, string iMsg);

        public object AddListBox
        {
            set
            {
                AddListBoxItem(value);
                //PCRProcess.LogCardReader(value.ToString());
                //PLog.WriteLog("CardReaderBay", Convert.ToString(value));
            }
        }

        private delegate void AddListBoxItemDelegate(object item);

        private void AddListBoxItem(object item)
        {
            if (this.lstMain.InvokeRequired)
            {
                // This is a worker thread so delegate the task.
                if (lstMain.Items.Count > 1000)
                    lstMain.Items.Clear();

                this.lstMain.Invoke(new AddListBoxItemDelegate(this.AddListBoxItem), item);
            }
            else
            {
                // This is the UI thread so perform the task.
                if (item != null)
                {
                    if (lstMain.Items.Count > 1000)
                        lstMain.Items.Clear();

                    this.lstMain.Items.Insert(0, item);
                    //logfile.WriteLog("System", item.ToString());
                }
            }
        }
        #endregion

        #region DataGrid
        private delegate void AddDataGridItemDelegate(int pRow, int pCol, object pValue);
        private delegate void AddDataGridRowsDelegate(int pRows);

        private void AddDataGridRows(int pRows)
        {
            if (this.dataGridView1.InvokeRequired)
            {
                // This is a worker thread so delegate the task.

                this.dataGridView1.Invoke(new AddDataGridRowsDelegate(this.AddDataGridRows), pRows);
            }
            else
            {
                // This is the UI thread so perform the task.
                if (pRows != 0)
                {
                    dataGridView1.Rows.Add(pRows);
                }
            }
        }
        private void AddDataGridItem(int pRow,int pCol,object pValue)
        {
            if (this.dataGridView1.InvokeRequired)
            {
                // This is a worker thread so delegate the task.

                this.dataGridView1.Invoke(new AddDataGridItemDelegate(this.AddDataGridItem), pRow,pCol,pValue);
            }
            else
            {
                // This is the UI thread so perform the task.
                if (pRow >= 0)
                {
                    //dataGridView1.Rows[1].Cells[0].Value = "bay_no";
                    dataGridView1.Rows[pRow].Cells[pCol].Value = pValue;
                }
            }
        }
        #endregion

        #region Combobox
        private delegate void AddComboboxItemDelegate(object Item);

        public void AddComboboxItem(object Item)
        {
            if (this.cboCR.InvokeRequired)
            {
                // This is a worker thread so delegate the task.

                this.dataGridView1.Invoke(new AddComboboxItemDelegate(this.AddComboboxItem), Item);
            }
            else
            {
                // This is the UI thread so perform the task.
                cboCR.Items.Add(Item);
            }
        }
        #endregion

        #region MynotifyIcon
        private void FormResize()
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                mynotifyIcon1.Icon = this.Icon;
                mynotifyIcon1.Visible = true;
                mynotifyIcon1.BalloonTipText = this.Text;
                mynotifyIcon1.ShowBalloonTip(500);
                this.Hide();
            }
        }
        private void mynotifyIcon1_Click(object sender, MouseEventArgs e)
        {
            mynotifyIcon1.Visible = false;
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }
        #endregion

        #region CardReader
        TreeView vTreeView;

        private void Initial()
        {
            //timer1.Enabled = true;
            //mOraDb = new cOracle(this);
            //mThread = new Thread(this.RunProcess);
            //mRunning = true;
            //mThread.Name = this.Text;
            //mThread.Start();

            //while (!mOraDb.ConnectStatus())
            //{ };
            //InitialObject();
            //StartThread();
        }

        private void InitialObject()
        {
            //InitialTreeView();
            InitialDatagrid();

            lstMain.Left = 14;
            lstMain.Top = 26;
            //listBox1.Width = lstMain.Width;
            //listBox1.Location = lstMain.Location;

            //groupBox1.Width = listBox1.Width;
            //groupBox1.Top = listBox1.Top + listBox1.Height + 4;
        }

        private void InitialTreeView()
        {
            try
            {
                vTreeView = this.treeView1;
                vTreeView.Nodes.Clear();
                vTreeView.Nodes.Add("MainNode", "Card Reader Bay");

                string strSQL = "select t.bay_no,t.comp_id" +
                                " from tas.VIEW_CR_BAY_COMPORT t order by t.bay_no";

                DataSet vDataset = null;
                DataTable dt;
                if (mOraDb.OpenDyns(strSQL, "TableName", ref vDataset))
                {
                    dt = vDataset.Tables["TableName"];
                    mCRBay = new _CR[dt.Rows.Count];
                    int vIndex=0;
                    foreach (DataRow dr in dt.Rows)
                    {
                        mCRBay[vIndex].BayNo = dr["bay_no"].ToString(); mCRBay[vIndex].ComportID = Convert.ToInt32(dr["comp_id"]);
                        vTreeView.Nodes["MainNode"].Nodes.Add("Bay" + dr["bay_no"].ToString(), "Bay : " + dr["bay_no"].ToString());
                        vIndex += 1;
                    }
                }
                TreeNode mainNode = new TreeNode();
                mainNode = vTreeView.Nodes["MainNode"];

                for (int i = 0; i < mainNode.Nodes.Count; i++)
                {
                    strSQL = "select t.bay_no, t.card_reader_name,t.card_reader_address" +
                                    ",t.comp_id,t.comport_no,t.comport_setting" +
                                    " from tas.VIEW_CR_BAY t" +
                                    " where t.bay_no=" + (i + 1);
                    if (mOraDb.OpenDyns(strSQL, "TableName", ref vDataset))
                    {
                        dt = vDataset.Tables["TableName"];
                        
                        for (int j = 0; j < dt.Rows.Count; j++)
                        {
                            mCR.Address = Convert.ToInt16(dt.Rows[j]["card_reader_address"]);
                            mCR.ComportID = Convert.ToInt32(dt.Rows[j]["comp_id"]);
                            mCR.ComportNo = dt.Rows[j]["comport_no"].ToString();
                            mCR.ComportSetting=dt.Rows[j]["comport_setting"].ToString();
                            mCR.Name=dt.Rows[j]["card_reader_name"].ToString();

                            mainNode.Nodes["Bay" + (i + 1).ToString()].Nodes.Add(mCR.Name, mCR.Name);
                            TreeNode ChildNode = new TreeNode();
                            ChildNode = mainNode.Nodes[i].Nodes[mCR.Name].Nodes.Add(j.ToString(), "Address=" + mCR.Address + "," + mCR.ComportNo + ":" + mCR.ComportSetting);
                            ChildNode.ForeColor = Color.Gray;
                            Font fnt = new Font(label1.Font, FontStyle.Italic);
                            ChildNode.NodeFont = fnt;
                        }
                        //vTreeView.Nodes[i].Expand();
                    }
                    vTreeView.Nodes[0].Nodes[i].Expand();
                }

                vTreeView.Nodes[0].Expand();
                
            }
            catch (Exception exp)
            { }
        }

        private void DisplayMessageCardReader()
        {
            try
            {

                if ((mNodeName == null) || (mNodeName == ""))
                {
                    mBayIndex = -1;
                    return;
                }
                if (label1.Text != mNodeName)
                {
                    label1.Text = mNodeName;
                    if (mNodeName.ToLower().IndexOf("card reader") > -1)
                    {
                        if (listBox1.DataSource != null)
                            listBox1.DataSource = null;
                        VisibleObject(true);
                    }
                    else
                    {
                        if (label1.Text.ToLower().IndexOf("bay") > -1)
                        {
                            mBayIndex = SearchBayIndex(label1.Text);
                            VisibleObject(false);
                        }
                    }
                }


                if (mBayIndex > -1)
                {
                    listBox1.DataSource = null;
                    listBox1.DataSource = mCRProcess[mBayIndex].mMyList;
                    DisplayMessageComport(true);
                }
                else
                    DisplayMessageComport(false);
            }
            catch (Exception exp)
            { }
            
        }

        private void DisplayMessageComport(bool pDisplay)
        {
            try
            {
                if (pDisplay)
                {
                    if ((mBayIndex > -1) && (mCRIndex > -1))
                    {
                        txtSend.Text = DateTime.Now + "->" + Environment.NewLine +
                                    mCRProcess[mBayIndex].mCardReader[mCRIndex].mCR_NewValue.DataSend;
                        txtRecv.Text = DateTime.Now + "<-" + Environment.NewLine +
                                    mCRProcess[mBayIndex].mCardReader[mCRIndex].mCR_NewValue.DataReceive;
                    }
                }
                else
                {
                    txtRecv.Text = "";
                    txtSend.Text = "";
                }
            }
            catch (Exception exp)
            { }
                
        }

        private int SearchBayIndex(string pName)
        {
            int vIndex=-1;
            string[] vName = pName.Split(':');
            
            foreach (_CR c in this.mCRBay)
            {
                vIndex += 1;
                if (c.BayNo == vName[1].Trim())
                    break;
            }
            return vIndex;
        }

        private int SearchCRIndex()
        {
            int vIndex=-1;
            try
            {
                if (mCRName != "")
                {
                    foreach (cCRProcess c in mCRProcess)
                    {
                        for (vIndex = 0; vIndex < c.mCardReader.Length; vIndex++)
                        {
                            if (c.mCardReader[vIndex].mCR_NewValue.Name == mCRName)
                            {
                                return vIndex;
                            }
                        }
                    }
                }
            }
            catch (Exception exp)
            { }
            return vIndex;
        }

        private void VisibleObject(bool pVisible)
        {
            lstMain.Visible = pVisible;
            listBox1.Visible = !pVisible;
            gpComport.Visible = !pVisible;
        }

        private void InitialDatagrid()
        {
            string strSQL = "select t.bay_no,t.card_reader_name" +
                            ",'Addr=' || t.card_reader_address || ', ' || t.comport_no || ' [' || t.comport_setting || ']'  as Description" +
                            " from tas.view_cr_bay t where t.bay_no=" +  mBayNo + " order by t.card_reader_id";
            DataSet vDataset = null;
            DataTable dt;
            try
            {
                if (mOraDb.OpenDyns(strSQL, "TableName", ref vDataset))
                {
                    dt = vDataset.Tables["TableName"];
                    //dataGridView1.Rows.Add(dt.Rows.Count - 1);
                    AddDataGridRows(dt.Rows.Count - 1);
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        //dataGridView1.Rows[i].Cells[0].Value = dt.Rows[i]["bay_no"];
                        //dataGridView1.Rows[i].Cells[1].Value = dt.Rows[i]["card_reader_name"];
                        //dataGridView1.Rows[i].Cells[2].Value = dt.Rows[i]["Description"];
                        AddDataGridItem(i, 0, i+1);
                        AddDataGridItem(i, 1, dt.Rows[i]["card_reader_name"]);
                        AddDataGridItem(i, 2, dt.Rows[i]["Description"]);
                        AddComboboxItem(dt.Rows[i]["card_reader_name"]);
                    }
                }

                 strSQL = "select t.bay_no,t.comp_id" +
                                 " from tas.VIEW_CR_BAY_COMPORT t where t.bay_no=" + mBayNo;
                 if (mOraDb.OpenDyns(strSQL, "TableName", ref vDataset))
                 {
                     dt = vDataset.Tables["TableName"];
                     mCRBay = new _CR[dt.Rows.Count];
                     int vIndex = 0;
                     foreach (DataRow dr in dt.Rows)
                     {
                         mCRBay[vIndex].BayNo = dr["bay_no"].ToString(); mCRBay[vIndex].ComportID = Convert.ToInt32(dr["comp_id"]);
                         vIndex += 1;
                     }
                 }
                 dataGridView1.ClearSelection();
            }
            catch (Exception exp)
            { }
            vDataset = null;
            dt = null;

        }

        private void DisplayCardReader()
        {
            if (mNodeName != label1.Text)
            {
                mNodeName = label1.Text;
                if (mNodeName.ToLower().IndexOf("card reader") > -1)
                {
                    mBayIndex = -1;
                    if (listBox1.DataSource != null)
                        listBox1.DataSource = null;
                    VisibleObject(true);
                }
                else
                {
                    if (label1.Text.ToLower().IndexOf("bay") > -1)
                    {
                        mBayIndex = SearchBayIndex(label1.Text);
                        VisibleObject(false);
                    }
                }
            }

            if (mBayIndex > -1)
            {
                listBox1.DataSource = null;
                listBox1.DataSource = mCRProcess[mBayIndex].mMyList;
                DisplayCardReaderComport(true);
            }
            else
                DisplayCardReaderComport(false);
        }

        private void DisplayCardReaderComport(bool pDisplay)
        {
            try
            {
                if (pDisplay)
                {
                    if ((mBayIndex > -1) && (mCRIndex > -1))
                    {
                        txtSend.Text = DateTime.Now + "->" + Environment.NewLine +
                                    mCRProcess[mBayIndex].mCardReader[mCRIndex].mCR_NewValue.DataSend;
                        txtRecv.Text = DateTime.Now + "<-" + Environment.NewLine +
                                    mCRProcess[mBayIndex].mCardReader[mCRIndex].mCR_NewValue.DataReceive;
                    }
                }
                else
                {
                    txtRecv.Text = txtRecv.Text;
                    txtSend.Text = txtSend.Text;
                }
            }
            catch (Exception exp)
            { }
        }

        #endregion

        private bool IsSingleInstance()
        {
            foreach (Process process in Process.GetProcesses())
            {
                if (process.MainWindowTitle == this.Text)
                    return false;
            }
            return true;
        }

        public frmCRBay()
        {
            InitializeComponent();
            //OraDb = new cOracle(this);
            //PLog = new clogfile();
            //AddListBox = DateTime.Now+"><-------------Start------------->";
            //Initial();
            //InitialTreeView();
            try
            {
                args = Environment.GetCommandLineArgs();
                mBayNo = args[1];
            }
            catch (Exception exp)
            {
                mBayNo = "1";
                //AddListBoxItem(mBayNo);
            }
            this.Text = mIni.INIRead(Directory.GetCurrentDirectory() + "\\AppStartup.ini", mBayNo, "TITLE");
            if (!IsSingleInstance())
            {
                MessageBox.Show("Another instance of this app is running.",this.Text);
                //Application.Exit();
                System.Environment.Exit(1);
            }
            StartThread();
        }

        private void frmMercury_Resize(object sender, EventArgs e)
        {
            FormResize();
        }

        private void frmMercury_FormClosing(object sender, FormClosingEventArgs e)
        {
            mRunning = true;
            mIni = null;
            //VisibleObject(true);
            timer1.Enabled = false;
            Application.DoEvents();
            this.Cursor = Cursors.WaitCursor;
            //AddListBox = DateTime.Now+"><-------------Stop------------->";
            System.Threading.Thread.Sleep(500);
            try
            {
                {
                    for (int i = 0; i < mCRProcess.Length; i++)
                    {
                        Application.DoEvents();
                        mCRProcess[i].Dispose();
                        //Thread.Sleep(50);
                    }
                }
            }
            catch (Exception exp)
            { }
            System.Threading.Thread.Sleep(500);
            mOraDb.Close();
            mOraDb.Dispose();
            this.Cursor = Cursors.Default;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                toolStripStatusLabel1.Text = "Database connect = " + mOraDb.ConnectStatus().ToString() + 
                                                "[" + mOraDb.GetConnectDBName()+ "]" +
                                                "   [Date Time : " + DateTime.Now + " ]";

            }
            catch (Exception exp)
            { }
            //DisplayMessageCardReader();
            //DisplayCardReader();
            DisplayCardReaderComport(chkDiag.Checked);
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            //if (treeView1.SelectedNode.Text.IndexOf("Bay") > -1)
            //{
            //    mSelectNodeIndex = treeView1.SelectedNode.Index;
            //    mNodeName = treeView1.SelectedNode.Text;
            //}
            //else
            //{
            //    mSelectNodeIndex = -1;
            //    mNodeName = "Card Reader Bay";
            //}

            TreeNode obj = new TreeNode();
            obj = treeView1.SelectedNode;
            if (obj.Text.ToLower().IndexOf("bay") > -1)
            {
                mNodeName = obj.Text;
                mCRIndex = -1;
            }
            else
            {
                if (obj.Text.ToLower().IndexOf("cr-") > -1)
                {
                    mNodeName = obj.Parent.Text;
                    mCRIndex = obj.Index;
                }
                else
                {
                        mNodeName = "Card Reader Bay";
                        mCRIndex = -1;
                }
            }
        }

        private void treeView1_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.ForeColor == Color.Gray) 
                e.Cancel = true; 
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (listBox1.SelectedIndex > -1)
            {
                MessageBox.Show(listBox1.SelectedItem.ToString());
            }
        }

        private void lstMain_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (lstMain.SelectedIndex > -1)
            {
                MessageBox.Show(lstMain.SelectedItem.ToString());
            }
        }

        private void dataGridView1_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            return;
            if (e.RowIndex > -1)
            {
                //mBayNo = dataGridView1[0, e.RowIndex].Value.ToString();
                mCRName = dataGridView1[1, e.RowIndex].Value.ToString();
                label1.Text = "Bay : " + mBayNo;
                mCRIndex = SearchCRIndex();
                gpComport.Text = "Comport Diagnostic " + "=> " + dataGridView1.Rows[e.RowIndex].Cells[2].Value.ToString();
            }
            else
            {
                mCRName = "";
                label1.Text = "Card Reader Bay";
                mCRIndex = -1;
                gpComport.Text = "Comport Diagnostic";
                dataGridView1.ClearSelection();
            }
            //mNodeName = label1.Text;
        }

        private void frmCRBay_Load(object sender, EventArgs e)
        {
            //StartThread();
            //this.Hide();
        }

        private void frmCRBay_Activated(object sender, EventArgs e)
        {
            //StartThread();
        }

        private void chkDiag_CheckedChanged(object sender, EventArgs e)
        {
            mBayIndex = 0;
            mCRIndex = cboCR.SelectedIndex;
            DisplayCardReaderComport(chkDiag.Checked);
        }

        private void cboCR_SelectedIndexChanged(object sender, EventArgs e)
        {
            chkDiag_CheckedChanged(null, null);
        }
    }
}
