using System;
using System.IO;
using System.Runtime.InteropServices;

namespace WPinternalsSDK
{
    public class FileStream : Stream
    {
        public FileStream(string Path, FileStreamMode Mode)
        {
            try
            {
                this.path = Path;
                this.mode = Mode;
                switch (this.mode)
                {
                    case FileStreamMode.Read:
                        this.length = (uint)FileSystem.GetFileSize(this.path);
                        if (this.length > 0U)
                        {
                            this.hFile = Win32.CreateFile(this.path, (FileAccess)2147483648U, ShareMode.Read | ShareMode.Write, IntPtr.Zero, CreationDisposition.OpenExisting, FileAttributes.Normal, IntPtr.Zero);
                        }
                        break;
                    case FileStreamMode.Write:
                        this.hFile = Win32.CreateFile(this.path, FileAccess.GenericWrite, ShareMode.None, IntPtr.Zero, CreationDisposition.CreateAlways, FileAttributes.Normal, IntPtr.Zero);
                        break;
                    case FileStreamMode.ReadWrite:
                        if (FileSystem.FileExists(this.path))
                        {
                            this.length = (uint)FileSystem.GetFileSize(this.path);
                        }
                        this.hFile = Win32.CreateFile(this.path, (FileAccess)3221225472U, ShareMode.None, IntPtr.Zero, CreationDisposition.OpenAlways, FileAttributes.Normal, IntPtr.Zero);
                        break;
                }
                this.open = true;
            }
            catch
            {
                throw;
            }
        }

        public override bool CanRead
        {
            get
            {
                return (this.mode == FileStreamMode.Read || this.mode == FileStreamMode.ReadWrite) && this.open;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return (this.mode == FileStreamMode.Write || this.mode == FileStreamMode.ReadWrite) && this.open;
            }
        }

        public override void Flush()
        {
        }

        public override long Length
        {
            get
            {
                return (long)((ulong)((this.mode == FileStreamMode.Read) ? this.length : this.position));
            }
        }

        public override long Position
        {
            get
            {
                return (long)((ulong)this.position);
            }
            set
            {
                if (value != (long)((ulong)this.position))
                {
                    if (value < 0L || value > -1)
                    {
                        throw new IndexOutOfRangeException("Position must be unsigned 32 bit");
                    }
                    int num;
                    if (Win32.SetFilePointer(this.hFile, (int)value, out num, MoveMethod.Begin) == 4294967295U)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to set position");
                    }
                    this.position = (uint)value;
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!this.open)
            {
                throw new InvalidOperationException("Stream is closed");
            }
            if (this.mode != FileStreamMode.Read && this.mode != FileStreamMode.ReadWrite)
            {
                throw new InvalidOperationException("Cannot read from this stream");
            }
            if (count < 0)
            {
                throw new ArgumentException("Count must have a positive value");
            }
            uint num = this.length - this.position;
            if (num < 0U)
            {
                num = 0U;
            }
            if (buffer == null)
            {
                throw new ArgumentNullException("Buffer cannot be null");
            }
            if ((ulong)num > (ulong)((long)count))
            {
                num = (uint)count;
            }
            if (num > 0U)
            {
                if ((long)offset + (long)((ulong)num) > (long)buffer.Length)
                {
                    throw new ArgumentException("Buffer too small");
                }
                if (!Win32.ReadFile(this.hFile, buffer, (uint)count, out num, UIntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to read file");
                }
                this.position += num;
            }
            return (int)num;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (offset < -2147483648L || offset > 2147483647L)
            {
                throw new IndexOutOfRangeException("Offset must be in 32 bit range");
            }
            int num2;
            uint num = Win32.SetFilePointer(this.hFile, (int)offset, out num2, (MoveMethod)origin);
            if (num == 4294967295U)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Seek failed");
            }
            this.position = num;
            return (long)((ulong)this.position);
        }

        public override void SetLength(long value)
        {
            if (this.mode != FileStreamMode.ReadWrite)
            {
                throw new InvalidOperationException("Only possible to set the lenght when FileAccessMode is ReadWrite");
            }
            if (value < 0L || value > -1)
            {
                throw new IndexOutOfRangeException("Length must be unsigned 32 bit");
            }
            int num;
            if (Win32.SetFilePointer(this.hFile, (int)value, out num, MoveMethod.Begin) == 4294967295U)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to set length");
            }
            if (!Win32.SetEndOfFile(this.hFile))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to set length");
            }
            if ((ulong)this.position > (ulong)value)
            {
                this.position = (uint)value;
            }
            if (Win32.SetFilePointer(this.hFile, (int)this.position, out num, MoveMethod.Begin) == 4294967295U)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to set length");
            }
            this.length = (uint)value;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!this.open)
            {
                throw new InvalidOperationException("Stream is closed");
            }
            if (this.mode != FileStreamMode.Write && this.mode != FileStreamMode.ReadWrite)
            {
                throw new InvalidOperationException("Cannot write to this stream");
            }
            if (count < 0)
            {
                throw new ArgumentException("Count must have a positive value");
            }
            uint num = (uint)(buffer.Length - offset);
            if (buffer == null)
            {
                throw new ArgumentNullException("Buffer cannot be null");
            }
            if ((ulong)num > (ulong)((long)count))
            {
                num = (uint)count;
            }
            if ((long)count > (long)((ulong)num))
            {
                throw new ArgumentException("Buffer too small");
            }
            if (num > 0U)
            {
                if (!Win32.WriteFile(this.hFile, buffer, (uint)count, out num, UIntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to write file");
                }
                this.position += num;
                if (this.length < this.position)
                {
                    this.length = this.position;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (this.open)
            {
                Win32.CloseHandle(this.hFile);
            }
            this.open = false;
            base.Dispose(disposing);
        }

        private readonly string path;

        private readonly FileStreamMode mode;

        public bool open;

        private uint position;

        private uint length;

        private IntPtr hFile = (IntPtr)(-1);
    }
}
