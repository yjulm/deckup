using System;
using System.Runtime.InteropServices;

namespace Deckup.Extend
{
    public class ArrayPtrEx : IDisposable
    {
        private GCHandle _gch;
        private IntPtr _ptr;

        public ArrayPtrEx(Array array, int index)
        {
            _gch = GCHandle.Alloc(array, GCHandleType.Pinned);
            _ptr = Marshal.UnsafeAddrOfPinnedArrayElement(array, index);
        }

        ~ArrayPtrEx()
        {
            Dispose();
        }

        public static implicit operator IntPtr(ArrayPtrEx cls)
        {
            return cls._ptr;
        }

        public void Dispose()
        {
            _ptr = IntPtr.Zero;

            if (_gch != default)
                _gch.Free();
            _gch = default;

            GC.SuppressFinalize(this);
        }
    }
}