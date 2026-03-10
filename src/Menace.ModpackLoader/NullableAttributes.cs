// Nullable attribute polyfill for compatibility with System.Text.Json and other
// nullable-aware libraries when building for frameworks that don't have full
// nullable reference type support.
//
// This prevents CS0656: Missing compiler required member
// 'System.Runtime.CompilerServices.NullableAttribute..ctor'
//
// Note: Even though we target net6.0, some builds may not have these available.

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Event | AttributeTargets.Field |
                    AttributeTargets.GenericParameter | AttributeTargets.Module |
                    AttributeTargets.Parameter | AttributeTargets.Property |
                    AttributeTargets.ReturnValue | AttributeTargets.Struct,
                    AllowMultiple = false, Inherited = false)]
    internal sealed class NullableAttribute : Attribute
    {
        public readonly byte[] NullableFlags;

        public NullableAttribute(byte flag)
        {
            NullableFlags = new[] { flag };
        }

        public NullableAttribute(byte[] flags)
        {
            NullableFlags = flags;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct |
                    AttributeTargets.Method | AttributeTargets.Interface |
                    AttributeTargets.Delegate,
                    AllowMultiple = false, Inherited = false)]
    internal sealed class NullableContextAttribute : Attribute
    {
        public readonly byte Flag;

        public NullableContextAttribute(byte flag)
        {
            Flag = flag;
        }
    }
}
