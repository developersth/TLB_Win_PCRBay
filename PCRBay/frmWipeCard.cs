using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PCRBay
{
    public partial class frmWipeCard : Form
    {
        CCRProcess mCRProcess;
        frmCRBay f;
        int mStep = 0;
        public frmWipeCard()
        {
            InitializeComponent();
            //InitialCardReaderName();
            //InitialCard();
        }

        public void InheritForm(frmCRBay pF)
        {
            f = pF;
            InitialCardReaderName();
            InitialCard();
        }
        void InitialCardReaderName()
        {
            foreach (CCRProcess c in f.mCRProcess)
            {
                for (int i = 0; i < c.mCardReader.Length; i++)
                {
                    CbBayCrId.Items.Add(c.mCardReader[i].mCR_NewValue.ID + c.mCardReader[i].mCR_NewValue.Name);
                }
            }
        }
        void InitialCard()
        {
            DataSet mDataSet = new DataSet();
            DataTable dt;
            string strSQL = "SELECT DISTINCT  T.TU_CARD_NO FROM OIL_LOAD_HEADERS T" +
                            " WHERE T.LOAD_STATUS IN (21,31,54) AND T.CANCEL_STATUS=0 ORDER BY T.TU_CARD_NO ASC";
            if (f.mOraDb.OpenDyns(strSQL, "pTableName", ref mDataSet))
            {
                dt = mDataSet.Tables["pTableName"];
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    CbBayVehicleId.Items.Add(dt.Rows[i]["TU_CARD_NO"].ToString());
                }
            }
            mDataSet = null;
        }

        private void button1_Click(object sender, EventArgs e)
        {

            if (mStep == 0)
                button1.Text = "Wipe card -> IN";
            else
                button1.Text = "Wipe card -> Out";

        }
        void WipeCard()
        {

        }
    }
}
