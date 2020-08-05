using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PCRBay
{
    class cMercuryFnc 
    {
        public string CheckKeyPress(string pData)
        {
            if (pData.Length == 0)
                return "";
            pData.Trim();
            if(pData.IndexOf((char.ConvertFromUtf32(int.Parse("1B",System.Globalization.NumberStyles.HexNumber)))
                     + char.ConvertFromUtf32(int.Parse("4F", System.Globalization.NumberStyles.HexNumber))
                     + char.ConvertFromUtf32(int.Parse("50", System.Globalization.NumberStyles.HexNumber))) > -1)
            {
                return "F1"; 
            }
            if (pData.IndexOf((char.ConvertFromUtf32(int.Parse("1B", System.Globalization.NumberStyles.HexNumber)))
                     + char.ConvertFromUtf32(int.Parse("4F", System.Globalization.NumberStyles.HexNumber))
                     + char.ConvertFromUtf32(int.Parse("51", System.Globalization.NumberStyles.HexNumber))) > -1)
            {
                return "F2";
            }
            if (pData.IndexOf((char.ConvertFromUtf32(int.Parse("1B", System.Globalization.NumberStyles.HexNumber)))
                     + char.ConvertFromUtf32(int.Parse("4F", System.Globalization.NumberStyles.HexNumber))
                     + char.ConvertFromUtf32(int.Parse("52", System.Globalization.NumberStyles.HexNumber))) > -1)
            {
                return "F3";
            }
            if (pData.IndexOf((char.ConvertFromUtf32(int.Parse("1B", System.Globalization.NumberStyles.HexNumber)))
                     + char.ConvertFromUtf32(int.Parse("4F", System.Globalization.NumberStyles.HexNumber))
                     + char.ConvertFromUtf32(int.Parse("53", System.Globalization.NumberStyles.HexNumber))) > -1)
            {
                return "F4";
            }

            if (pData.Trim().IndexOf(char.ConvertFromUtf32(int.Parse("1B", System.Globalization.NumberStyles.HexNumber))
                     + char.ConvertFromUtf32(int.Parse("5B", System.Globalization.NumberStyles.HexNumber))
                     + char.ConvertFromUtf32(int.Parse("31", System.Globalization.NumberStyles.HexNumber))
                     + char.ConvertFromUtf32(int.Parse("37", System.Globalization.NumberStyles.HexNumber))
                     + char.ConvertFromUtf32(int.Parse("7E", System.Globalization.NumberStyles.HexNumber))) > -1)
            {
                return "F5";
            }
            if (pData.Trim().IndexOf(char.ConvertFromUtf32(int.Parse("1B", System.Globalization.NumberStyles.HexNumber))
                     + char.ConvertFromUtf32(int.Parse("5B", System.Globalization.NumberStyles.HexNumber))
                     + char.ConvertFromUtf32(int.Parse("31", System.Globalization.NumberStyles.HexNumber))
                     + char.ConvertFromUtf32(int.Parse("38", System.Globalization.NumberStyles.HexNumber))
                     + char.ConvertFromUtf32(int.Parse("7E", System.Globalization.NumberStyles.HexNumber))) > -1)
            {
                return "F6";
            }
            if (pData.Trim().IndexOf(char.ConvertFromUtf32(int.Parse("1B", System.Globalization.NumberStyles.HexNumber))
                     + char.ConvertFromUtf32(int.Parse("5B", System.Globalization.NumberStyles.HexNumber))
                     + char.ConvertFromUtf32(int.Parse("31", System.Globalization.NumberStyles.HexNumber))
                     + char.ConvertFromUtf32(int.Parse("39", System.Globalization.NumberStyles.HexNumber))
                     + char.ConvertFromUtf32(int.Parse("7E", System.Globalization.NumberStyles.HexNumber))) > -1)
            {
                return "F7";
            }

            if (pData.Trim().IndexOf(char.ConvertFromUtf32(int.Parse("08", System.Globalization.NumberStyles.HexNumber))) > -1)    
            {
                return "F8";
            }

            return "";
        }

        public string MoveCursor(int pRow, int pCol)
        {
            return char.ConvertFromUtf32(27) + "[" + pRow + ";" + pCol + "H";
        }

        public string ClearDisplay()
        {
            return char.ConvertFromUtf32(27) + "[2J";
        }

        public string ClearToEndOfLine()
        {
            return  char.ConvertFromUtf32(27) + "[?2z";
        }

        public string SetGraphicDisplayMode()
        {
            return char.ConvertFromUtf32(27) + "[?3z";
        }

        public string MakeCursorVisible()
        {
            return char.ConvertFromUtf32(27) + "[?25h"; }

        public string MakeCursorInvisible()
        {
            return char.ConvertFromUtf32(27) + "[?25l";
        }

        public string NewLine()
        {
            return char.ConvertFromUtf32(27) + "E";
        }

        public string CursorDown()
        {
            return char.ConvertFromUtf32(27) + "D";
        }

        public string CursorUp()
        {
            return char.ConvertFromUtf32(27) + "M";
        }

        public string SaveCursorPosition()
        {
            return char.ConvertFromUtf32(27) + "7";
        }

        public string RestoreCursorPosition()
        {
            return char.ConvertFromUtf32(27) + "8";
        }

        public string CursorHome()
        {
            return char.ConvertFromUtf32(27) + "[?6l";
        }

        public string SendNextQueueBlock()
        {
            return char.ConvertFromUtf32(27) + "[?9;1z";
        }

        public string ResendLastSentBlock()
        {
            return char.ConvertFromUtf32(27) + "[?9;2z";
        }

        public string DeleteAllStoreMessage()
        {
            return char.ConvertFromUtf32(27) + "[?10z";
        }

        public string SetKeyToNum()
        {
            return char.ConvertFromUtf32(27) + "(<";
        }

        public string SetKeyToUpper()
        {
            return char.ConvertFromUtf32(27) + ")>";
        }

        public string SetKeyToLower()
        {
            return char.ConvertFromUtf32(27) + "*<";
        }

        public string SetKeyToUN()
        {
            return char.ConvertFromUtf32(27) + "[?17;1z";
        }

        public string SetKeyToUNL()
        {
            return char.ConvertFromUtf32(27) + "[?17;2z";
        }

        public string DisableShiftKey()
        {
            return char.ConvertFromUtf32(27) + "[?13z";
        }

        public string EnableShiftKey()
        {
            return char.ConvertFromUtf32(27) + "[?12z";
        }
    }
}
