using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.IO.Ports;

namespace PCRBAY
{
    public partial class frmMain : Form
    {
        #region Constant, Struct and Enum
        enum _StepProcess : int
        {
            InitialDatabase = 0
                ,
            InitialCardReader = 1
                ,
            InitialComportCardReader = 2
                ,
            InitialDataGrid = 3
                ,
            InitialClassEvent = 4
                ,
            ChangeProcess = 5
        }

        #endregion

        public Comport[] ATGComport;
        SerialPort[] atgSerialPort;

        public Comport[] CRComport;
        SerialPort[] crSerialPort;

        public Logfile LogFile = new Logfile();

        //private ATGProcess[] atgProcess;
        CardReaderProcess[] crProcess;

        public Database OraDb;
        public IniLib.CINI iniFile = new IniLib.CINI();
        string arg;
        string[] args;
        string scanID;
        string logFileName;
        int processID;

        _StepProcess mainStepProcess;

        private bool IsSingleInstance()
        {
            foreach (Process process in Process.GetProcesses())
            {
                if (process.MainWindowTitle == this.Text)
                    return false;
            }
            return true;
        }

        public frmMain()
        {
            InitializeComponent();

            try
            {
                args = Environment.GetCommandLineArgs();
                arg = args[1];
            }
            catch (Exception exp)
            {
                arg = "1";
                //AddListBoxItem(mBayNo);
            }
            this.Text = iniFile.INIRead(Directory.GetCurrentDirectory() + "\\AppStartup.ini", arg, "TITLE");
            scanID = iniFile.INIRead(Directory.GetCurrentDirectory() + "\\AppStartup.ini", arg, "SCANID");
            logFileName = this.Text;
            if (!IsSingleInstance())
            {
                MessageBox.Show("Another instance of this app is running.", this.Text);
                //Application.Exit();
                System.Environment.Exit(1);
            }
            //StartThread();
            AddListBox = DateTime.Now + "><------------Application Start------------->";
        }

        #region Thread
        bool thrConnect;
        bool thrShutdown;
        bool thrRunning;

        Thread thrMain;

        private void StartThread()
        {
            //System.Threading.Thread.Sleep(1000);
            thrMain = new Thread(this.RunProcess);
            thrMain.Name = this.Text;
            thrMain.Start();
        }

        private void RunProcess()
        {
            thrRunning = true;
            System.Threading.Thread.Sleep(1000);
            if (mainStepProcess != _StepProcess.ChangeProcess)
            {
                InitialDataBase();
                mainStepProcess = _StepProcess.InitialDatabase;
            }
            while (thrRunning)
            {
                try
                {
                    if (thrShutdown)
                        return;
                    switch (mainStepProcess)
                    {
                        case _StepProcess.InitialDatabase:
                            if (OraDb.IsConnect())
                            {
                                mainStepProcess = _StepProcess.InitialCardReader;
                                GetIntervalTimeRestart();
                                //AddListBoxItem(mStepProcess.ToString());
                            }
                            Thread.Sleep(500);
                            break;
                        case _StepProcess.InitialCardReader:
                            //thrRunning = true;
                            //Thread.Sleep(500);
                            if (InitialCardReader())
                            {
                                if (InitialComportCardReader())
                                {
                                    mainStepProcess = _StepProcess.InitialDataGrid;
                                    //atgProcess[processID].StartThread();
                                    for (int i = 0; i < crProcess.Length; i++)
                                    {
                                        crProcess[i].StartThread();
                                    }
                                }
                                //AddListBoxItem(mStepProcess.ToString());
                            }
                            break;
                        case _StepProcess.InitialDataGrid:
                            //thrRunning = false;
                            InitialDataGrid();
                            //AddListBoxItem(mStepProcess.ToString());
                            mainStepProcess = _StepProcess.InitialClassEvent;
                            break;
                        case _StepProcess.InitialClassEvent:
                            thrRunning = false;
                            InitialClassEvent();
                            mainStepProcess = _StepProcess.ChangeProcess;
                            break;
                        case _StepProcess.ChangeProcess:
                            currentDay = DateTime.Now.Day - 1;
                            thrShutdown = true;
                            //thrShutdown = true;
                            //Thread.Sleep(300);
                            //while(atgProcess[processID].IsThreadAlive)
                            //{}
                            //atgProcess[processID].StartThread();
                            break;
                    }
                    DisplayDateTime();
                    Thread.Sleep(300);
                }
                catch (Exception exp)
                { AddListBoxItem(DateTime.Now + ">" + exp.Message + "[" + exp.Source + "-" + mainStepProcess.ToString() + "]"); }
                //finally
                //{
                //    mShutdown = true;
                //    mRunning = false;
                //}
            }
        }
        #endregion

        #region ListboxItem

        public void DisplayMessage(string pFileName, string pMsg)
        {
            if (this.lstMain.InvokeRequired)
            {
                // This is a worker thread so delegate the task.
                if (lstMain.Items.Count > 1000)
                {
                    //lstMain.Items.Clear();
                    this.Invoke((Action)(() => lstMain.Items.Clear()));
                }

                this.lstMain.Invoke(new DisplayMessageDelegate(this.DisplayMessage), pFileName, pMsg);
            }
            else
            {
                // This is the UI thread so perform the task.
                if (pMsg != null)
                {
                    if (lstMain.Items.Count > 1000)
                    {
                        //lstMain.Items.Clear();
                        this.Invoke((Action)(() => lstMain.Items.Clear()));
                    }

                    this.lstMain.Items.Insert(0, pMsg);
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
            }
        }

        private delegate void AddListBoxItemDelegate(object item);

        private void AddListBoxItem(object item)
        {
            try
            {
                if (this.lstMain.InvokeRequired)
                {
                    // This is a worker thread so delegate the task.
                    if (lstMain.Items.Count > 1000)
                    {
                        ClearListBoxItem();
                    }
                    //lstMain.Items.Clear();

                    this.lstMain.Invoke(new AddListBoxItemDelegate(this.AddListBoxItem), item);
                }
                else
                {
                    // This is the UI thread so perform the task.
                    if (item != null)
                    {
                        if (lstMain.Items.Count > 1000)
                            ClearListBoxItem();
                        //lstMain.Items.Clear();

                        this.lstMain.Items.Insert(0, item);
                        //logfile.WriteLog("System", item.ToString());
                        LogFile.WriteLog(logFileName, (string)item);
                    }
                }
            }
            catch (Exception exp)
            { }
        }

        private delegate void ClearListBoxItemDelegate();
        private void ClearListBoxItem()
        {
            if (this.lstMain.InvokeRequired)
            {
                this.lstMain.Invoke(new ClearListBoxItemDelegate(ClearListBoxItem));

            }
            else
            {
                this.lstMain.Items.Clear();
            }

        }

        #endregion

        #region DataGrid
        private delegate void AddDataGridItemEventHandler(int pRow, int pCol, object pValue);
        private delegate void AddDataGridRowsEventHandler(int pRows);

        private void AddDataGridRows(int pRows)
        {
            if (this.dataGridView1.InvokeRequired)
            {
                // This is a worker thread so delegate the task.

                this.dataGridView1.Invoke(new AddDataGridRowsEventHandler(this.AddDataGridRows), pRows);
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
        private void AddDataGridItem(int pRow, int pCol, object pValue)
        {
            if (this.dataGridView1.InvokeRequired)
            {
                // This is a worker thread so delegate the task.

                this.dataGridView1.Invoke(new AddDataGridItemEventHandler(this.AddDataGridItem), pRow, pCol, pValue);
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
        private delegate void AddComboboxItemEvenHandler(object pItem);

        public void AddComboboxItem(object pItem)
        {
            if (this.cboComport.InvokeRequired)
            {
                // This is a worker thread so delegate the task.

                this.cboComport.Invoke(new AddComboboxItemEvenHandler(this.AddComboboxItem), pItem);
            }
            else
            {
                // This is the UI thread so perform the task.
                cboComport.Items.Add(pItem);
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

        #region Class Events
        void InitialATGEventHandler()
        {
            //ATGProcess.ATGEventsHaneler handler1 = new ATGProcess.ATGEventsHaneler(WriteEventsHandler);
            //atgProcess[0].OnATGEvents += handler1;
        }

        void InitialComportEventHandler()
        {
            Comport.ComportEventsHandler hander1 = new Comport.ComportEventsHandler(WriteEventsHandler);
            for (int i = 0; i < CRComport.Length; i++)
            {
                CRComport[i].OnComportEvents += hander1;
            }
        }

        void WriteEventsHandler(object pSender, string pMessage)
        {
            AddListBoxItem(pMessage);
            //mLog.WriteLog(mLogFileName, message);
        }
        #endregion

        #region Main Step Process
        private void InitialDataBase()
        {
            OraDb = new Database(this);
        }

        #region ATG
        
        bool InitialComportATG()
        {
            string strSQL = "select" +
                            " t.comp_id,t.comport_no,t.comport_setting" +
                            " from tas.VIEW_ATG_CONFIG_COMPORT t " +
                            " order by t.atg_id";
            DataSet vDataset = null;
            DataTable dt;
            bool vRet = false;
            try
            {
                if (OraDb.OpenDyns(strSQL, "TableName", ref vDataset))
                {
                    dt = vDataset.Tables["TableName"];
                    CRComport = new Comport[dt.Rows.Count];
                    crSerialPort = new SerialPort[dt.Rows.Count];
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        atgSerialPort[i] = new SerialPort();
                        ATGComport[i] = new Comport(this, ref crSerialPort[i],Convert.ToDouble(dt.Rows[i]["comp_id"].ToString()));
                        ATGComport[i].InitialPort();
                    }
                }
                vRet = true;
            }
            catch (Exception exp)
            { LogFile.WriteErrLog(exp.Message); }
            vDataset = null;
            dt = null;
            return vRet;
        }
        #endregion

        #region Card Reader Bay
        bool InitialCardReader()
        {
            string strSQL = "select t.card_reader_id,t.card_reader_name,t.card_reader_address" +
                            " from tas.VIEW_CR_BAY t " +
                            " where t.island_no='" + scanID + "'" +
                            " order by t.card_reader_id";
            DataSet vDataset = null;
            DataTable dt;
            bool vRet = false;
            try
            {
                if (OraDb.OpenDyns(strSQL, "TableName", ref vDataset))
                {
                    dt = vDataset.Tables["TableName"];
                    crProcess = new CardReaderProcess[dt.Rows.Count];
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        crProcess[i] = new CardReaderProcess(this);
                        crProcess[i].InitialCardReader(dt.Rows[i]["card_reader_id"].ToString());
                    }
                    vRet = true;
                }
            }
            catch (Exception exp)
            { }
            vDataset = null;
            dt = null;
            return vRet;
        }

        bool InitialComportCardReader()
        {
             bool vRet = false;
            try
            {
                for (int i = 0; i < crProcess.Length; i++)
                {
                    crProcess[i].InitialComportCardReader();
                }
                vRet = true;
            }
            catch (Exception exp)
            { LogFile.WriteErrLog(exp.Message); }
            return vRet;
        }
        #endregion
        
        private bool InitialDataGrid()
        {
            string strSQL = "select t.bay_no,t.card_reader_name" +
                            ",'Addr=' || t.card_reader_address || ', ' || t.comport_no || ':' || t.comport_no1  as Description" +
                            ",t.comp_id,t.comp_id1,t.control_comport,t.control_comport1" +
                            " from tas.VIEW_CR_BAY t " +
                            " where t.island_no='" + scanID + "'" +
                            " order by t.card_reader_id";
            DataSet vDataset = null;
            DataTable dt;
            bool vRet = false;
            string vMsg;
            try
            {
                if (OraDb.OpenDyns(strSQL, "TableName", ref vDataset))
                {
                    dt = vDataset.Tables["TableName"];
                    //dataGridView1.Rows.Add(dt.Rows.Count - 1);
                    AddDataGridRows(dt.Rows.Count - 1);
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        AddDataGridItem(i, 0, i + 1);
                        AddDataGridItem(i, 1, dt.Rows[i]["card_reader_name"]);
                        AddDataGridItem(i, 2, dt.Rows[i]["Description"]);
                        AddComboboxItem(dt.Rows[i]["card_reader_name"]);
                        vMsg = string.Format("Initial Card Reader {0} {1}", dt.Rows[i]["card_reader_name"].ToString(), dt.Rows[i]["Description"].ToString());
                        AddListBoxItem(DateTime.Now + ">" + vMsg);
                    }
                }
                vRet = true;
            }
            catch (Exception exp)
            { }
            vDataset = null;
            dt = null;
            return vRet;
        }

        private void InitialClassEvent()
        {
            //InitialATGEventHandler();
            //InitialComportEventHandler();
        }

        public void ChangeProcess()
        {
            processID += 1;
            if (processID >= cboComport.Items.Count)
            {
                processID = 0;
            }
            thrShutdown = false;
            mainStepProcess = _StepProcess.ChangeProcess;
            StartThread();
        }
        #endregion

        private void DisplayDateTime()
        {
            toolStripStatusLabel1.Text = "Database connect = " + OraDb.IsConnect().ToString() +
                                                "[" + OraDb.ServiceName() + "]" +
                                                "   [Date Time : " + DateTime.Now + "]";
        }

        private void DiagnosticComport(bool pDiag)
        {
            try
            {
                if (pDiag)
                {
                    // to do some thing
                    txtSend.Text = crProcess[cboComport.SelectedIndex].DiagnosticSend();
                    //txtSend.WordWrap = false;
                    txtRecv.Text = crProcess[cboComport.SelectedIndex].DiagnosticReceive();
                    //txtRecv.WordWrap = false;
                    //LogFile.WriteComportLog(CRComport[cboComport.SelectedIndex].GetComportNo(), txtSend.Lines);
                    //LogFile.WriteComportLog(CRComport[cboComport.SelectedIndex].GetComportNo(), txtRecv.Lines);
                }
                else
                {
                    //txtRecv.Text = txtRecv.Text;
                    //txtSend.Text = txtSend.Text;
                }
            }
            catch (Exception exp)
            { }
        }

        private void frmMain_Resize(object sender, EventArgs e)
        {
            FormResize();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                toolStripStatusLabel1.Text = "Database connect = " + OraDb.IsConnect().ToString() +
                                                "[" + OraDb.ServiceName() + "]" +
                                                "   [Date Time : " + DateTime.Now + "]";

            }
            catch (Exception exp)
            { }
            //DisplayMessageCardReader();
            //DisplayCardReader();
            DiagnosticComport(chkDiag.Checked);
            RestartProcess();
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            AddListBox = DateTime.Now + "><------------Application Stop-------------->";
            thrShutdown = true;
            iniFile = null;
            //VisibleObject(true);
            timer1.Enabled = false;
            Application.DoEvents();
            this.Cursor = Cursors.WaitCursor;

            //if(CRComport!=null)
            //{
            //    foreach (Comport p in CRComport)
            //    {
            //        p.Dispose();
            //    }
            //}

            if (crProcess != null)
            {
                for (int i = 0; i < crProcess.Length; i++)
                {
                    crProcess[i].Dispose();
                }
            }
            if (CRComport != null)
            {
                foreach (Comport p in CRComport)
                {
                    p.Dispose();
                }
            }
            OraDb.DisconnectDatabase();
            OraDb.Dispose();         
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            StartThread();
            timer1.Enabled = true;
        }

        private void lstMain_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            MessageBox.Show(lstMain.SelectedItem.ToString(),this.Text,MessageBoxButtons.OK);
        }

        private void chkDiag_CheckedChanged(object sender, EventArgs e)
        {
            //foreach (ATGProcess p in atgProcess)
            //{
            //    p.DiagnasticATG(false);
            //}
            //if (cboComport.SelectedIndex > -1)
            //{
            //    atgProcess[cboComport.SelectedIndex].DiagnasticATG(chkDiag.Checked);
            //}
        }

        private void cboComport_SelectedIndexChanged(object sender, EventArgs e)
        {
            chkDiag_CheckedChanged(null, null);
        }
        #region Restart process
        bool isChangeDay = false;
        DateTime eodDate;
        int currentDay;
        int intervalTimeRestart;
        DateTime restartDateTime;
        void RestartProcess()
        {
            if (mainStepProcess != _StepProcess.ChangeProcess)
                return;

            int isIdle = 0;
            if (currentDay != DateTime.Now.Day)
            {
                AddListBox = DateTime.Now + "> Change day.";
                currentDay = DateTime.Now.Day;
                isChangeDay = true;
            }
            else
            {
                if (crProcess == null)
                    return;
                for (int i = 0; i < crProcess.Length; i++)
                {
                    if (crProcess[i].CRNewValue.LoadingMode == CardReaderProcess._LoadingMode.TouchCard)
                    {
                        isIdle++;
                    }
                }
                if ((DateTime.Now.TimeOfDay - restartDateTime.TimeOfDay).TotalMinutes > intervalTimeRestart && isIdle == crProcess.Length && isChangeDay == true)
                {
                    isChangeDay = false;
                    GetIntervalTimeRestart();
                    AddListBox = DateTime.Now + "> Reconnect database.";
                    OraDb.DisconnectDatabase();
                    Thread.Sleep(1000);
                    InitialDataBase();
                }
            }
        }

        void GetIntervalTimeRestart()
        {
            DataSet vDataset = null;
            DataTable dt;
            TimeSpan ts;
            intervalTimeRestart = 1; //Minute

            string strSQL = "select tas.get_tas_config(4) as value from dual";
            if (OraDb.OpenDyns(strSQL, "TableName", ref vDataset))
            {
                dt = vDataset.Tables["TableName"];
                //string s = Convert.ToDateTime(dt.Rows[0]["value"].ToString()).AddHours(-1).ToLongTimeString();
                restartDateTime = Convert.ToDateTime(dt.Rows[0]["value"].ToString());
            }
            else
                restartDateTime = DateTime.Now;

            dt = null;
            vDataset = null;

        }
        #endregion
    }
}
