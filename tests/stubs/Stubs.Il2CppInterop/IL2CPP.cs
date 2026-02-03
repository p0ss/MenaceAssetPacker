using System;

namespace Il2CppInterop.Runtime;

public static class IL2CPP
{
    public static IntPtr GetIl2CppClass(string assembly, string ns, string name) => IntPtr.Zero;
    public static IntPtr il2cpp_object_get_class(IntPtr obj) => IntPtr.Zero;
    public static IntPtr il2cpp_class_get_field_from_name(IntPtr klass, string name) => IntPtr.Zero;
    public static uint il2cpp_field_get_offset(IntPtr field) => 0;
    public static IntPtr il2cpp_class_get_parent(IntPtr klass) => IntPtr.Zero;
    public static bool il2cpp_class_is_assignable_from(IntPtr klass, IntPtr other) => false;
    public static IntPtr il2cpp_class_get_namespace(IntPtr klass) => IntPtr.Zero;
    public static IntPtr il2cpp_class_get_name(IntPtr klass) => IntPtr.Zero;
    public static uint il2cpp_class_instance_size(IntPtr klass) => 0;
    public static IntPtr il2cpp_class_get_element_class(IntPtr klass) => IntPtr.Zero;
    public static string Il2CppStringToManaged(IntPtr ptr) => null;
}
