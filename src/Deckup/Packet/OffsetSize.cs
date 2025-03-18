using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Deckup.Packet
{
    public static class OffsetSize<TPktStruct> where TPktStruct : IPktStruct
    {
        public static readonly int Size;
        public static readonly Dictionary<string, FieldInfoPair> Offset;

        static OffsetSize()
        {
            Type t = typeof(TPktStruct);

            Size = Marshal.SizeOf(t);
            Offset = new Dictionary<string, FieldInfoPair>();

            FieldInfo[] infos = t.GetFields();
            foreach (FieldInfo info in infos)
                Offset.Add(info.Name, new FieldInfoPair()
                {
                    Offset = (int)Marshal.OffsetOf(t, info.Name),
                    Size = Marshal.SizeOf(info.FieldType)
                });
        }
    }

    public struct FieldInfoPair
    {
        public int Offset;
        public int Size;

        public int RealOffset
        {
            get { return _baseOffset + Offset; }
        }

        private int _baseOffset;

        public FieldInfoPair SetBase(int baseOffset)
        {
            _baseOffset = baseOffset;
            return this;
        }
    }
}