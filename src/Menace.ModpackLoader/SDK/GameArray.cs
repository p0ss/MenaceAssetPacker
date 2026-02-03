using System;
using System.Runtime.InteropServices;

namespace Menace.SDK;

/// <summary>
/// Safe wrapper for IL2CPP native arrays. Reads elements directly from the
/// array's data region after the IL2CPP array header.
/// </summary>
public readonly struct GameArray
{
    private readonly IntPtr _arrayPointer;

    // IL2CPP array layout: [klass][monitor][bounds][max_length][data...]
    private static readonly int MaxLengthOffset = IntPtr.Size * 3;
    private static readonly int DataOffset = IntPtr.Size * 4;

    public GameArray(IntPtr arrayPointer)
    {
        _arrayPointer = arrayPointer;
    }

    public bool IsValid => _arrayPointer != IntPtr.Zero;

    public int Length
    {
        get
        {
            if (_arrayPointer == IntPtr.Zero) return 0;
            try
            {
                return Marshal.ReadInt32(_arrayPointer + MaxLengthOffset);
            }
            catch
            {
                return 0;
            }
        }
    }

    public GameObj this[int index]
    {
        get
        {
            if (_arrayPointer == IntPtr.Zero || index < 0) return GameObj.Null;

            try
            {
                var length = Marshal.ReadInt32(_arrayPointer + MaxLengthOffset);
                if (index >= length) return GameObj.Null;

                var ptr = Marshal.ReadIntPtr(_arrayPointer + DataOffset + index * IntPtr.Size);
                return new GameObj(ptr);
            }
            catch
            {
                return GameObj.Null;
            }
        }
    }

    /// <summary>
    /// Read a primitive int element at the given index.
    /// Useful for arrays of value types.
    /// </summary>
    public int ReadInt(int index)
    {
        if (_arrayPointer == IntPtr.Zero || index < 0) return 0;
        try
        {
            var length = Marshal.ReadInt32(_arrayPointer + MaxLengthOffset);
            if (index >= length) return 0;
            return Marshal.ReadInt32(_arrayPointer + DataOffset + index * 4);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Read a primitive float element at the given index.
    /// </summary>
    public float ReadFloat(int index)
    {
        if (_arrayPointer == IntPtr.Zero || index < 0) return 0f;
        try
        {
            var length = Marshal.ReadInt32(_arrayPointer + MaxLengthOffset);
            if (index >= length) return 0f;
            var raw = Marshal.ReadInt32(_arrayPointer + DataOffset + index * 4);
            return BitConverter.Int32BitsToSingle(raw);
        }
        catch
        {
            return 0f;
        }
    }

    public Enumerator GetEnumerator() => new(this);

    public struct Enumerator
    {
        private readonly GameArray _array;
        private int _index;
        private readonly int _length;

        internal Enumerator(GameArray array)
        {
            _array = array;
            _index = -1;
            _length = array.Length;
        }

        public GameObj Current => _array[_index];

        public bool MoveNext()
        {
            _index++;
            return _index < _length;
        }
    }
}
