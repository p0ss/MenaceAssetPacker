using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Menace.SDK.Internal;

namespace Menace.SDK;

/// <summary>
/// Safe wrapper for IL2CPP Dictionary&lt;K,V&gt; objects. Iterates the internal
/// _entries array, skipping tombstoned entries (hashCode &lt; 0).
/// </summary>
public readonly struct GameDict
{
    private readonly IntPtr _dictPointer;

    public GameDict(IntPtr dictPointer)
    {
        _dictPointer = dictPointer;
    }

    public GameDict(GameObj dictObj) : this(dictObj.Pointer) { }

    public bool IsValid => _dictPointer != IntPtr.Zero;

    /// <summary>
    /// Returns the actual number of entries in the dictionary.
    /// This is _count minus _freeCount (deleted entries awaiting reuse).
    /// </summary>
    public int Count
    {
        get
        {
            if (_dictPointer == IntPtr.Zero) return 0;

            try
            {
                var klass = IL2CPP.il2cpp_object_get_class(_dictPointer);

                // Read _count (total slots used including deleted)
                var countOffset = OffsetCache.GetOrResolve(klass, "_count");
                if (countOffset == 0)
                    countOffset = OffsetCache.GetOrResolve(klass, "count");
                if (countOffset == 0) return 0;

                var count = Marshal.ReadInt32(_dictPointer + (int)countOffset);

                // Read _freeCount (number of deleted entries)
                var freeCountOffset = OffsetCache.GetOrResolve(klass, "_freeCount");
                if (freeCountOffset == 0)
                    freeCountOffset = OffsetCache.GetOrResolve(klass, "freeCount");

                var freeCount = freeCountOffset != 0
                    ? Marshal.ReadInt32(_dictPointer + (int)freeCountOffset)
                    : 0;

                return count - freeCount;
            }
            catch
            {
                return 0;
            }
        }
    }

    public Enumerator GetEnumerator() => new(this);

    public struct Enumerator
    {
        private readonly IntPtr _entriesArray;
        private readonly int _count;
        private readonly int _entryStride;
        private readonly int _arrayHeader;
        private int _index;

        internal Enumerator(GameDict dict)
        {
            _entriesArray = IntPtr.Zero;
            _count = 0;
            _entryStride = 0;
            _arrayHeader = IntPtr.Size * 4; // IL2CPP array header
            _index = -1;
            Current = (GameObj.Null, GameObj.Null);

            if (dict._dictPointer == IntPtr.Zero) return;

            try
            {
                var klass = IL2CPP.il2cpp_object_get_class(dict._dictPointer);

                // Read _entries array pointer
                var entriesOffset = OffsetCache.GetOrResolve(klass, "_entries");
                if (entriesOffset == 0)
                    entriesOffset = OffsetCache.GetOrResolve(klass, "entries");
                if (entriesOffset == 0) return;

                _entriesArray = Marshal.ReadIntPtr(dict._dictPointer + (int)entriesOffset);
                if (_entriesArray == IntPtr.Zero) return;

                // Read _count
                var countOffset = OffsetCache.GetOrResolve(klass, "_count");
                if (countOffset == 0)
                    countOffset = OffsetCache.GetOrResolve(klass, "count");
                if (countOffset != 0)
                    _count = Marshal.ReadInt32(dict._dictPointer + (int)countOffset);

                // Resolve entry stride from the entries array's element class
                var arrKlass = IL2CPP.il2cpp_object_get_class(_entriesArray);
                if (arrKlass != IntPtr.Zero)
                {
                    var elemKlass = IL2CPP.il2cpp_class_get_element_class(arrKlass);
                    if (elemKlass != IntPtr.Zero)
                    {
                        _entryStride = (int)IL2CPP.il2cpp_class_instance_size(elemKlass)
                                       - (2 * IntPtr.Size); // subtract object header
                    }
                }

                // Fallback stride: assume int hash + int next + IntPtr key + IntPtr value
                if (_entryStride <= 0)
                    _entryStride = 8 + IntPtr.Size * 2;
            }
            catch (Exception ex)
            {
                ModError.ReportInternal("GameDict.Enumerator", "Init failed", ex);
            }
        }

        public (GameObj Key, GameObj Value) Current { get; private set; }

        public bool MoveNext()
        {
            if (_entriesArray == IntPtr.Zero || _entryStride <= 0)
                return false;

            while (true)
            {
                _index++;
                if (_index >= _count) return false;

                try
                {
                    var entryBase = _entriesArray + _arrayHeader + _index * _entryStride;

                    // Entry layout: [int hashCode][int next][K key][V value]
                    var hashCode = Marshal.ReadInt32(entryBase);
                    if (hashCode < 0) continue; // tombstoned entry

                    var keyPtr = Marshal.ReadIntPtr(entryBase + 8);
                    var valuePtr = Marshal.ReadIntPtr(entryBase + 8 + IntPtr.Size);

                    Current = (new GameObj(keyPtr), new GameObj(valuePtr));
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
