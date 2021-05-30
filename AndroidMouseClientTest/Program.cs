
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Collections.Concurrent;

namespace AndroidMouseClientTest
{
    class Program
    {
        private static readonly byte SET_LEFT_CLICK_UP      = 0b0_0_0_0_0_0_0_1;   // 1
        private static readonly byte SET_RIGHT_CLICK_UP     = 0b0_0_0_0_0_0_1_0;   // 2
        private static readonly byte SET_ENTER              = 0b0_0_0_0_0_1_0_0;   // 4
        private static readonly byte SET_BACKSPACE          = 0b0_0_0_0_1_0_0_0;   // 8
        private static readonly byte SET_LEFT_CLICK_DOWN    = 0b0_0_0_1_0_0_0_0;   // 16
        private static readonly byte SET_RIGHT_CLICK_DOWN   = 0b0_0_1_0_0_0_0_0;   // 32
        private static readonly byte SET_MOUSE_RESET        = 0b0_1_0_0_0_0_0_0;   // 64

        private static short X = 1080;
        private static short Y = 720;

        private static WinApiMouseMoveWrapper moveMouse;
        private readonly static int[] noRequireShift = new int[45];

        static void Main(string[] args)
        {

            Rectangle r = Screen.PrimaryScreen.Bounds;
            //typedString += " height " + r.Height + " width " + r.Width;
            //Console.WriteLine(typedString);
            X = (short) r.Width;
            Y = (short) r.Height;

            ///////////////////////////////////////////////
            //// fills up the array of chars which require shift down
            noRequireShift[0] = 59;
            noRequireShift[1] = 61;
            noRequireShift[2] = 91;
            noRequireShift[3] = 92;
            noRequireShift[4] = 93;

            int itr = 5;
            for (int i = 97; i <= 122; i++) { 
                noRequireShift[itr] = i;
                itr++;
            }
            for (int i = 44; i <= 57; i++)
            {
                noRequireShift[itr] = i;
                itr++;
            }
            /////////////////////////////////////////////////////////////

            Console.Write(" Touchpad (T) / Gyroscope (G)         ");

            string c = Console.ReadLine();

            if (c.ToUpper() == "G") moveMouse = WinApiMouseMoveWrapperGyroscope.GetWinApiMouseMoveWrapperGyroscope(X, Y);
            
            else if (c.ToUpper() == "T") moveMouse = WinApiMouseMoveWrapperTouchpad.GetWinApiMouseMoveWrapperTouchpad(X, Y);
            
            else moveMouse = WinApiMouseMoveWrapperGyroscope.GetWinApiMouseMoveWrapperGyroscope(X, Y);


            UdpClient client = new UdpClient(49001);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 49001);
            byte[] data;
            short x, y;

            byte syncByte = 0;
            bool isLeftUp;
            bool isRightUp;
            bool isLeftDown;
            bool isRightDown;
            bool isEnter;
            bool isBackSpace;
            bool isReset;
            bool leftAlreadyDown = false;
            bool rightAlreadyDown = false;

            string typedString = " ";

            WinAPI.MouseMove(X / 2, Y / 2);


            while (true)
            {
                typedString = "";
                data = client.Receive(ref endPoint);
                x = BitConverter.ToInt16(new byte[] { data[2], data[3] }, 0);
                y = BitConverter.ToInt16(new byte[] { data[4], data[5] }, 0);

                if (data[50] != syncByte)
                {
                    if (leftAlreadyDown) WinAPI.MouseClick("left", 1);
                    if (rightAlreadyDown) WinAPI.MouseClick("right", 1);
                    syncByte = data[50];
                    syncByte++;
                    if (syncByte == 127) syncByte = 0;
                    continue;
                }

                isLeftUp = (data[1] & SET_LEFT_CLICK_UP) == 1;
                isRightUp = (data[1] & SET_RIGHT_CLICK_UP) == 2;
                isLeftDown = (data[1] & SET_LEFT_CLICK_DOWN) == 16;           
                isRightDown = (data[1] & SET_RIGHT_CLICK_DOWN) == 32;          
                isEnter = (data[1] & SET_ENTER) == 4;
                isBackSpace = (data[1] & SET_BACKSPACE) == 8;
                isReset = (data[1] & SET_MOUSE_RESET) == 64;

                moveMouse.MouseMove(x,y);

                if (isReset) {
                    if (leftAlreadyDown) WinAPI.MouseClick("left",1);
                    if (rightAlreadyDown) WinAPI.MouseClick("right",1);
                    WinAPI.MouseMove(X/2,Y/2);
                    continue;
                }

                if (isLeftDown)
                {
                    WinAPI.MouseClick("left", 0);
                    leftAlreadyDown = true;
                }

                if (isLeftUp) {
                    if (leftAlreadyDown) WinAPI.MouseClick("left", 1);
                    leftAlreadyDown = false;
                }

                if (isRightDown)
                {
                    WinAPI.MouseClick("right", 0);
                    rightAlreadyDown = true;
                }

                if (isRightUp)
                {
                    if (rightAlreadyDown) WinAPI.MouseClick("right", 1);
                    rightAlreadyDown = false;
                }

                if(data[6]!=0){
                    int i = data[6];
                    if (i > 127) i -= 256;
                    WinAPI.scroll(i);
                }
                //testing git push

                if (isEnter) WinAPI.PressKey(13);
                if (isBackSpace) WinAPI.PressKey(8);


                ///////////////////////////////////////////////////////////////////////////////////////////
                /// key typing logic

                if (data[0] > 0) {

                    //int typeStringIter = 7;
                    for(int i = 7;i<(data[0]+7);i++) {
                        if (noRequireShift.Contains<int>(data[i])) WinAPI.PressKey((byte)WinAPI.ConvertCharToVirtualKey((char)data[i]));
                        else
                        {
                            WinAPI.PressKey((byte)Keys.LShiftKey,true);
                            WinAPI.PressKey((byte)WinAPI.ConvertCharToVirtualKey((char)data[i]));
                            WinAPI.PressKey((byte)Keys.LShiftKey,false);
                        }
                     //   typeStringIter++;
                    } 
                    
                }

                //////////////////////////////////////////////////////////////////////////////////////// 
               
                syncByte++;
                if (syncByte == 127) syncByte = 0;
 
                
                //////////////////////////////////////////////////////
                /// debug purposes
                
                typedString +=("x " +x + " y " + y + " ");
                if (data[0] > 0)
                {
                    typedString += "string : ";
                    for (int i = 7; i < (data[0] + 7); i++) typedString += (char)data[i];
                    typedString +=" ";
                }
                if (isLeftDown) typedString += " leftDown ";
                if (isRightDown) typedString += " righttDown ";
                if (isLeftUp) typedString += " leftUP ";
                if (isRightUp) typedString += " rightUp ";
                if (isEnter) typedString += " enter ";
                if (isBackSpace) typedString += " backspace ";
                if (isReset) typedString += " reset ";
                int j = data[50];
                if (j > 127) j -= 256;
                typedString += " syncbit "+j.ToString();
                typedString += " scroll " + data[6].ToString();
                Console.WriteLine(typedString);
                typedString = ""; 
                //////////////////////////////////////////////////////
                isEnter = isBackSpace = isReset = isLeftDown = isLeftUp = isRightUp = isRightDown = false;

            }
        }

        private abstract class WinApiMouseMoveWrapper
        {   
            protected readonly short X_GYROSCOPE = 1080;
            protected readonly short Y_GYROSCOPE = 720;

            protected WinApiMouseMoveWrapper(short X,short Y) {
                this.X_GYROSCOPE = X;
                this.Y_GYROSCOPE = Y;
            }

            public abstract void MouseMove(short x, short y);
        }

        private class WinApiMouseMoveWrapperGyroscope : WinApiMouseMoveWrapper {

            private static WinApiMouseMoveWrapper winApiMouseMoveWrapper= null;
            public static WinApiMouseMoveWrapper GetWinApiMouseMoveWrapperGyroscope(short x, short y) {
                winApiMouseMoveWrapper = winApiMouseMoveWrapper ?? new WinApiMouseMoveWrapperGyroscope(x, y);
                return winApiMouseMoveWrapper;
            }
            private WinApiMouseMoveWrapperGyroscope(short X, short Y) : base(X, Y) {}

            public override void MouseMove(short x, short y) {
                WinAPI.MouseMove(X_GYROSCOPE/2+x,Y_GYROSCOPE/2+y);
            }

        }

        private class WinApiMouseMoveWrapperTouchpad : WinApiMouseMoveWrapper
        {

            private static WinApiMouseMoveWrapper winApiMouseMoveWrapper = null;

            private short x ;
            private short y ;

            public static WinApiMouseMoveWrapper GetWinApiMouseMoveWrapperTouchpad(short x1, short y1)
            {
                winApiMouseMoveWrapper = winApiMouseMoveWrapper ?? new WinApiMouseMoveWrapperTouchpad(x1, y1);
                return winApiMouseMoveWrapper;
            }
            private WinApiMouseMoveWrapperTouchpad(short X, short Y) : base(X, Y) {
                this.x = (short)(X_GYROSCOPE/2);
                this.y = (short)(Y_GYROSCOPE/2);
            }

            public override void MouseMove(short x1, short y1)
            {
                update(x1, y1);
                WinAPI.MouseMove(x,y);
            }

            private void update(short x1, short y1) {
                x += x1;
                y += y1;
                if (x < 0 || x > (X_GYROSCOPE - 1)) {
                    if (x < 0) x = 0;
                    else x = (short)( X_GYROSCOPE - 1 );
                }
                if (y < 0 || y > (Y_GYROSCOPE - 1))
                {
                    if (y < 0) y = 0;
                    else y = (short)(Y_GYROSCOPE - 1);
                }
            }

        }

        public static class WinAPI
    {
        #region function imports
        [DllImport("user32.dll")]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y,
                             int nWidth, int nHeight,
                             bool bRepaint);

        [DllImport("USER32.DLL")]
        public static extern IntPtr FindWindow(string lpClassName,
                              string lpWindowName);

        [DllImport("USER32.DLL")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();

        [DllImport("user32")]
        public static extern int SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy,
                          int dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags,
                          UIntPtr dwExtraInfo);


        private const int SRCCOPY = 0x00CC0020;
        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hObject, int nXDest, int
                         nYDest, int nWidth, int nHeight,
                         IntPtr hObjectSource, int nXSrc,
                         int nYSrc, int dwRop);
        [DllImport("gdi32.dll")]
        private static extern IntPtr
          CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);
        [DllImport("gdi32.dll")]
        private static extern IntPtr
          CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr
                                       hDC);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr
                                           hObject);
        [DllImport("gdi32.dll")]
        private static extern IntPtr
          SelectObject(IntPtr hDC, IntPtr hObject);
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowRect(IntPtr hWnd, ref RECT rect);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);
        [Flags]
        public enum MouseEventFlags
        {
            LEFTDOWN = 0x00000002,
            LEFTUP = 0x00000004,
            MIDDLEDOWN = 0x00000020,
            MIDDLEUP = 0x00000040,
            MOVE = 0x00000001,
            ABSOLUTE = 0x00008000,
            RIGHTDOWN = 0x00000008,
            RIGHTUP = 0x00000010,
            WHEEL = 0x00000800
        }
        #endregion

        #region static methods

        /// <summary>
        /// simulates a keypress, see http://msdn2.microsoft.com/en-us/library/system.windows.forms.sendkeys(VS.71).aspx
        /// no winapi but this works just fine for me
        /// </summary>
        /// <param name="keys">the keys to press</param>
        /// 

        public static void scroll(int scrollValue)
        {
            mouse_event((uint)MouseEventFlags.WHEEL, 0, 0, scrollValue, 0);
        }

        public static IntPtr GetWindow()
        {
            return GetForegroundWindow();
        }

        public static void PressKey(byte keyCode)
        {
            const int KEYEVENTF_EXTENDEDKEY = 0x1;
            const int KEYEVENTF_KEYUP = 0x2;
            keybd_event(keyCode, 0x45, KEYEVENTF_EXTENDEDKEY, (UIntPtr)0);
            keybd_event(keyCode, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, (UIntPtr)0);
        }

            public static void PressKey(byte keyCode,bool isDown)
            {
                const int KEYEVENTF_EXTENDEDKEY = 0x1;
                const int KEYEVENTF_KEYUP = 0x2;
                if(isDown) keybd_event(keyCode, 0x45, KEYEVENTF_EXTENDEDKEY, (UIntPtr)0);
                else keybd_event(keyCode, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, (UIntPtr)0);
            }

            public static Keys ConvertCharToVirtualKey(char ch)
        {
            short vkey = VkKeyScan(ch);
            Keys retval = (Keys)(vkey & 0xff);
            int modifiers = vkey >> 8;

            if ((modifiers & 1) != 0) retval |= Keys.Shift;
            if ((modifiers & 2) != 0) retval |= Keys.Control;
            if ((modifiers & 4) != 0) retval |= Keys.Alt;

            return retval;
        }


        public static void ManagedSendKeys(string keys)
        {
            SendKeys.SendWait(keys);
        }

        /// <summary>
        /// checks if the correct window is active, then send keypress
        /// </summary>
        /// <param name="keys">keys to press</param>
        /// <param name="windowName">window to send the keys to</param>
        public static void ManagedSendKeys(string keys, string windowName)
        {
            if (WindowActive(windowName))
            {

                ManagedSendKeys(keys);
            }
        }
        /// <summary>
        /// sends a keystring to a window
        /// </summary>
        /// <param name="keys"></param>
        /// <param name="handle"></param>
        public static void ManagedSendKeys(string keys, IntPtr handle)
        {
            if (WindowActive(handle))
            {
                ManagedSendKeys(keys);
            }
        }
        /// <summary>
        /// sends a key to a window, pressing the button for x seconds
        /// </summary>
        /// <param name="key"></param>
        /// <param name="windowHandler"></param>
        /// <param name="delay"></param>
        public static void KeyboardEvent(Keys key, IntPtr windowHandler,
                          int delay)
        {
            const int KEYEVENTF_EXTENDEDKEY = 0x1;
            const int KEYEVENTF_KEYUP = 0x2;
            // I had some Compile errors until I Casted the final 0 to UIntPtr like this...
            keybd_event((byte)key, 0x45, KEYEVENTF_EXTENDEDKEY, (UIntPtr)0);
            Thread.Sleep(delay);
            keybd_event((byte)key, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP,
                 (UIntPtr)0);
        }

        /// <summary>
        /// sends a key to a window, state="up" lifts it up, state="down" presses it down
        /// </summary>
        /// <param name="key"></param>
        /// <param name="windowHandler"></param>
        /// <param name="state"></param>
        public static void KeyboardEvent(Keys key, IntPtr windowHandler,
                          string state)
        {
            byte bstate = 28;
            if (state == "up")
                bstate = 0x1 | 0x2;
            else
                bstate = 0x1;
            keybd_event((byte)key, 0x45, bstate, (UIntPtr)0);
        }

        /// <summary>
        /// checks for the currently active window then simulates a mouseclick
        /// </summary>
        /// <param name="button">which button to press (left middle up)</param>
        /// <param name="windowName">the window to send to</param>
        public static void MouseClick(string button, string windowName)
        {
            if (WindowActive(windowName))
                MouseClick(button);
        }

        /// <summary>
        /// simulates a mouse click see http://pinvoke.net/default.aspx/user32/mouse_event.html?diff=y
        /// </summary>
        /// <param name="button">which button to press (left middle up)</param>
        public static void MouseClick(string button)
        {
            switch (button)
            {
                case "left":
                    mouse_event((uint)MouseEventFlags.LEFTDOWN, 0, 0, 0, 0);
                    mouse_event((uint)MouseEventFlags.LEFTUP, 0, 0, 0, 0);
                    break;
                case "right":
                    mouse_event((uint)MouseEventFlags.RIGHTDOWN, 0, 0, 0, 0);
                    mouse_event((uint)MouseEventFlags.RIGHTUP, 0, 0, 0, 0);
                    break;
                case "middle":
                    mouse_event((uint)MouseEventFlags.MIDDLEDOWN, 0, 0, 0, 0);
                    mouse_event((uint)MouseEventFlags.MIDDLEUP, 0, 0, 0, 0);
                    break;
            }
        }

        /// <summary>
        /// sends a mouseclick to a window state=1 lifts it up state=0 presses it down
        /// </summary>
        /// <param name="button"></param>
        /// <param name="state"></param>
        public static void MouseClick(string button, int state)
        {
            switch (button.ToLower())
            {
                case "left":
                    switch (state)
                    {
                        case 1:
                            mouse_event((uint)MouseEventFlags.LEFTUP, 0, 0, 0, 0);
                            break;
                        case 0:
                            mouse_event((uint)MouseEventFlags.LEFTDOWN, 0, 0, 0, 0);
                            break;
                    }
                    break;
                case "right":
                    switch (state)
                    {
                        case 1:
                            mouse_event((uint)MouseEventFlags.RIGHTUP, 0, 0, 0, 0);
                            break;
                        case 0:
                            mouse_event((uint)MouseEventFlags.RIGHTDOWN, 0, 0, 0, 0);
                            break;
                    }
                    break;
                case "middle":
                    switch (state)
                    {
                        case 1:
                            mouse_event((uint)MouseEventFlags.MIDDLEUP, 0, 0, 0, 0);
                            break;
                        case 0:
                            mouse_event((uint)MouseEventFlags.MIDDLEDOWN, 0, 0, 0, 0);
                            break;
                    }
                    break;
            }
        }

        /// <summary>
        /// moves the mouse
        /// </summary>
        /// <param name="x">x position to move to</param>
        /// <param name="y">y position to move to</param>
        public static void MouseMove(int x, int y)
        {
            SetCursorPos(x, y);
        }

        /// <summary>
        /// moves a window and resizes it accordingly
        /// </summary>
        /// <param name="x">x position to move to</param>
        /// <param name="y">y position to move to</param>
        /// <param name="windowName">the window to move</param>
        /// <param name="width">the window's new width</param>
        /// <param name="height">the window's new height</param>
        public static void WindowMove(int x, int y, string windowName, int width,
                       int height)
        {
            IntPtr window = FindWindow(null, windowName);
            if (window != IntPtr.Zero)
                MoveWindow(window, x, y, width, height, true);
        }

        /// <summary>
        /// moves a window to a specified position
        /// </summary>
        /// <param name="x">x position</param>
        /// <param name="y">y position</param>
        /// <param name="windowName">the window to be moved</param>
        public static void WindowMove(int x, int y, string windowName)
        {
            WindowMove(x, y, windowName, 800, 600);
        }

        /// <summary>
        /// checks if a specified window is currently the topmost one
        /// </summary>
        /// <param name="windowName">the window to check for</param>
        /// <returns>true if windowName machtes the topmost window, false if not</returns>
        public static bool WindowActive(string windowName)
        {
            IntPtr myHandle = FindWindow(null, windowName);
            IntPtr foreGround = GetForegroundWindow();
            if (myHandle != foreGround)
                return false;
            else
                return true;
        }

        /// <summary>
        /// checks if a handle is the active window atm
        /// </summary>
        /// <param name="myHandle"></param>
        /// <returns></returns>
        public static bool WindowActive(IntPtr myHandle)
        {
            IntPtr foreGround = GetForegroundWindow();
            if (myHandle != foreGround)
                return false;
            else
                return true;
        }

        /// <summary>
        /// makes the specified window the topmost one
        /// </summary>
        /// <param name="windowName">the window to activate</param>
        public static void WindowActivate(string windowName)
        {
            IntPtr myHandle = FindWindow(null, windowName);
            SetForegroundWindow(myHandle);
        }

        /// <summary>
        /// makes the specified window the topmost one
        /// </summary>
        /// <param name="handle">the window handle</param>
        public static void WindowActivate(IntPtr handle)
        {
            SetForegroundWindow(handle);
        }
        #endregion

        /// <summary>
        /// makes a screenshot of your current desktop and returns a bitmap
        /// </summary>
        /// <returns></returns>
        //public static Bitmap CreateScreenshot()
        //{
        //    IntPtr hWnd = GetDesktopWindow();
        //    IntPtr hSorceDC = GetWindowDC(hWnd);
        //    RECT rect = new RECT();
        //    GetWindowRect(hWnd, ref rect);
        //    int width = rect.right - rect.left;
        //    int height = rect.bottom - rect.top;
        //    IntPtr hDestDC = CreateCompatibleDC(hSorceDC);
        //    IntPtr hBitmap = CreateCompatibleBitmap(hSorceDC, width, height);
        //    IntPtr hObject = SelectObject(hDestDC, hBitmap);
        //    BitBlt(hDestDC, 0, 0, width, height, hSorceDC, 0, 0, SRCCOPY);
        //    SelectObject(hDestDC, hObject);
        //    DeleteDC(hDestDC);
        //    ReleaseDC(hWnd, hSorceDC);
        //    Bitmap screenshot = Bitmap.FromHbitmap(hBitmap);
        //    DeleteObject(hBitmap);
        //    return screenshot;
        //}
    }

}
}
