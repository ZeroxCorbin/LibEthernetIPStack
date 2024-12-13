/**************************************************************************
*                           MIT License
* 
* Copyright (C) 2016 Frederic Chaxel <fchaxel@free.fr>
*
* Permission is hereby granted, free of charge, to any person obtaining
* a copy of this software and associated documentation files (the
* "Software"), to deal in the Software without restriction, including
* without limitation the rights to use, copy, modify, merge, publish,
* distribute, sublicense, and/or sell copies of the Software, and to
* permit persons to whom the Software is furnished to do so, subject to
* the following conditions:
*
* The above copyright notice and this permission notice shall be included
* in all copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
* EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
* MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
* IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
* CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
* TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
* SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*
*********************************************************************/
using LibEthernetIPStack.Shared;

namespace LibEthernetIPStack.Base;
public class ForwardOpen_Config
{
    public bool IsO2T = false;
    public bool O2T_Exculsive = false;
    public bool O2T_P2P = true;
    /// <summary>
    /// 0=Low; 1=High; 2=Scheduled; 3=Urgent
    /// </summary>
    public byte O2T_Priority = 0;
    public ushort O2T_datasize = 0;
    public uint O2T_RPI = 200 * 1000; // 200 ms

    public bool IsT2O = false;
    public bool T2O_Exculsive = false;
    public bool T2O_P2P = true;
    /// <summary>
    /// 0=Low; 1=High; 2=Scheduled; 3=Urgent
    /// </summary>
    public byte T2O_Priority = 0;
    public ushort T2O_datasize = 0;
    public uint T2O_RPI = 200 * 1000; // 200 ms

    public ForwardOpen_Config()
    {
    }

    public ForwardOpen_Config(EnIPAttribut Output, EnIPAttribut Input, bool InputP2P, uint cycleTime)
    {
        if (Output != null)
        {
            IsO2T = true;
            O2T_datasize = (ushort)Output.RawData.Length;
            O2T_RPI = cycleTime; // in microsecond,  here same for the two direction
            O2T_P2P = true; // by default in this direction
        }
        if (Input != null)
        {
            IsT2O = true;
            T2O_datasize = (ushort)Input.RawData.Length;
            T2O_RPI = cycleTime; // in microsecond, here same for the two direction
            T2O_P2P = InputP2P;
        }
    }
}
