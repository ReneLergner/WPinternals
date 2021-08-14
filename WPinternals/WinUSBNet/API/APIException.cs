/*  WinUSBNet library
 *  (C) 2010 Thomas Bleeker (www.madwizard.org)
 *  
 *  Licensed under the MIT license, see license.txt or:
 *  http://www.opensource.org/licenses/mit-license.php
 */

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MadWizard.WinUSBNet.API
{
    /// <summary>
    /// Exception used internally to catch Win32 API errors. This exception should
    /// not be thrown to the library's caller.
    /// </summary>
    internal class APIException : Exception
    {
        public APIException(string message) :
            base(message)
        {
        }
        public APIException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public APIException() : base()
        {
        }

        public static APIException Win32(string message)
        {
            return Win32(message, Marshal.GetLastWin32Error());

            // TEST!!
            // int ErrorCode = Marshal.GetLastWin32Error();
            // if ((ErrorCode & 0xffff) == 0x1f)
            //     ErrorCode = ErrorCode; // Break here
            // return APIException.Win32(message, ErrorCode);
        }

        public static APIException Win32(string message, int errorCode)
        {
            return new APIException(message, new Win32Exception(errorCode));
        }
    }
}
