using System;

namespace Il2CppInterop.Runtime.InteropTypes;

public abstract class Il2CppObjectBase
{
    public IntPtr Pointer { get; }
    protected Il2CppObjectBase(IntPtr pointer) { Pointer = pointer; }
}
