# IL2CPP Runtime Function Reference

This document lists IL2CPP runtime functions that have been identified and renamed in Ghidra to improve readability when analyzing Menace game binaries.

## Overview

The Menace game uses Unity's IL2CPP runtime, which compiles C# code to native code. Many low-level runtime functions appear frequently in decompiled code. This reference helps analysts understand what these functions do.

## GC/Memory Functions

| Address | Name | Description |
|---------|------|-------------|
| `0x1804a7550` | `il2cpp_gc_alloc` | Allocates memory from GC heap (non-pinned) |
| `0x1804a7540` | `il2cpp_gc_alloc_pinned` | Allocates pinned memory from GC heap |
| `0x1804a7560` | `il2cpp_gc_alloc_internal` | Core GC allocator with free list management |
| `0x1804a48d0` | `il2cpp_gc_alloc_typed` | Allocates memory with type info for GC tracking |
| `0x1804a4a40` | `il2cpp_gc_alloc_atomic` | Allocates atomic/no-reference memory |
| `0x1804a4fd0` | `il2cpp_gc_alloc_large` | Allocates large objects (>2KB) |
| `0x1804a51c0` | `il2cpp_gc_block_alloc` | Block allocator with size buckets |
| `0x18045b840` | `il2cpp_gc_card_table_mark` | Marks page dirty in GC card table (write barrier) |
| `0x1804a0e60` | `il2cpp_gc_maybe_collect` | Triggers GC collection periodically |
| `0x18045b5c0` | `il2cpp_gc_register_finalizer` | Registers finalizer for object cleanup |
| `0x18043b470` | `il2cpp_notify_allocation_trackers` | Notifies memory profilers about allocations |
| `0x1804e5950` | `il2cpp_memset` | Optimized AVX memset implementation |
| `0x1804e52b0` | `il2cpp_memcpy` | Optimized AVX memcpy implementation |

### GC Handle Functions

| Address | Name | Description |
|---------|------|-------------|
| `0x180440fa0` | `il2cpp_gchandle_free` | Frees a GC handle |
| `0x180441060` | `il2cpp_gchandle_get_target` | Gets object from GC handle |
| `0x18045ad40` | `il2cpp_gchandle_get_target_internal` | Internal implementation of gchandle_get_target |
| `0x18045b330` | `il2cpp_gchandle_new_internal` | Creates new GC handle internally |

## Object System Functions

| Address | Name | Description |
|---------|------|-------------|
| `0x1804618f0` | `il2cpp_object_new` | Creates new managed object (allocates + sets vtable) |
| `0x180461c20` | `il2cpp_array_new_internal` | Creates new array with element count |
| `0x1804618c0` | `il2cpp_array_new_simple` | Creates 1D array of given type and length |
| `0x180427e50` | `il2cpp_value_box` | Boxes value type into object (exported) |
| `0x180428050` | `il2cpp_object_unbox` | Unboxes object to value (returns ptr + 0x10) |
| `0x18045d660` | `il2cpp_value_box_from_stack` | Boxes value from stack frame |
| `0x18046eca0` | `il2cpp_class_init_ensure` | Ensures class is initialized before use |
| `0x180429440` | `il2cpp_runtime_class_init_actual` | Actually runs class static constructor |
| `0x18046cac0` | `il2cpp_class_instance_size` | Gets instance size of class |
| `0x18046ba60` | `il2cpp_class_get_array_class` | Gets array class for element type |
| `0x18046bde0` | `il2cpp_class_get_cctor` | Gets class static constructor method |
| `0x1804285f0` | `il2cpp_class_is_assignable_from` | Checks type compatibility |
| `0x18046b300` | `il2cpp_type_get_class` | Gets class from Il2CppType |

## String Functions

| Address | Name | Description |
|---------|------|-------------|
| `0x180426a00` | `il2cpp_string_new_utf8` | Creates String from UTF-8 C string |
| `0x180426bc0` | `il2cpp_string_new_len` | Creates String with specified length |
| `0x180426bb0` | `il2cpp_string_new_wrapper` | Wrapper for string creation |
| `0x18045d630` | `il2cpp_string_alloc` | Allocates String object memory |
| `0x180467130` | `il2cpp_utf8_to_utf16` | Converts UTF-8 to UTF-16 |
| `0x180467100` | `il2cpp_utf8_to_utf16_strlen` | UTF-8 to UTF-16 with length calculation |
| `0x180378990` | `mono_string_chars` | Gets char* pointer from String (exported) |
| `0x180003750` | `mono_string_length` | Gets String length (exported) |

## Exception Functions

### Exception Creation

| Address | Name | Description |
|---------|------|-------------|
| `0x18043bc90` | `il2cpp_exception_from_name` | Creates exception by namespace/name |
| `0x1804842d0` | `il2cpp_get_mscorlib_image` | Gets mscorlib assembly image |
| `0x18042c1f0` | `il2cpp_exception_init` | Initializes exception object |
| `0x18043f0a0` | `il2cpp_exception_fill_stack_trace` | Fills exception stack trace |
| `0x18043aed0` | `il2cpp_store_exception_ref` | Stores exception reference |

### Specific Exception Getters

| Address | Name | Description |
|---------|------|-------------|
| `0x180428750` | `il2cpp_get_exception_argument_null` | Creates ArgumentNullException |
| `0x18043d980` | `il2cpp_get_null_reference_exception` | Creates NullReferenceException |
| `0x18043d530` | `il2cpp_get_index_out_of_range_exception` | Creates IndexOutOfRangeException |
| `0x18043d5a0` | `il2cpp_get_invalid_cast_exception` | Creates InvalidCastException |
| `0x18043da80` | `il2cpp_get_overflow_exception` | Creates OverflowException |
| `0x18043dba0` | `il2cpp_get_type_init_exception` | Creates TypeInitializationException |
| `0x18043e680` | `il2cpp_get_type_load_exception` | Creates TypeLoadException |
| `0x18043d8f0` | `il2cpp_get_entry_point_not_found_exception` | Creates EntryPointNotFoundException |
| `0x18043c1f0` | `il2cpp_hresult_to_exception` | Converts HRESULT to managed exception |
| `0x18043d920` | `il2cpp_get_missing_method_exception` | Creates MissingMethodException |
| `0x18043d950` | `il2cpp_get_not_supported_exception` | Creates NotSupportedException |

### Exception Raising

| Address | Name | Description |
|---------|------|-------------|
| `0x18047f840` | `il2cpp_raise_exception` | Raises managed exception (exported) |
| `0x18043f440` | `il2cpp_raise_exception_internal` | Internal exception raising |
| `0x1804b6490` | `il2cpp_raise_cpp_exception` | Raises C++ SEH exception (RaiseException) |
| `0x180461e10` | `il2cpp_raise_overflow_exception` | Raises OverflowException |
| `0x180450e20` | `il2cpp_raise_array_type_exception` | Raises array type error |
| `0x180426c60` | `il2cpp_raise_method_access_exception` | Raises method access error |
| `0x18043f5c0` | `il2cpp_raise_memory_exception` | Raises memory allocation failure |
| `0x18043f560` | `il2cpp_raise_null_reference` | Raises NullReferenceException |
| `0x18043f540` | `il2cpp_raise_index_out_of_range` | Raises IndexOutOfRangeException |
| `0x18043f580` | `il2cpp_raise_null_reference_with_trace` | Raises NullReferenceException with stack trace |
| `0x18043f420` | `il2cpp_raise_hresult_exception` | Raises exception from HRESULT error |
| `0x180428db0` | `il2cpp_throw_null_reference` | Wrapper that throws NullReferenceException |
| `0x180428da0` | `il2cpp_throw_index_out_of_range` | Wrapper that throws IndexOutOfRangeException |
| `0x180428d70` | `il2cpp_raise_exception_with_cleanup` | Raises exception after cleanup |
| `0x180428dc0` | `il2cpp_throw_exception_with_message` | Throws exception with formatted message |
| `0x180428e10` | `il2cpp_throw_if_not_locked` | Conditionally raises exception if lock not held |
| `0x180428530` | `il2cpp_throw_unsupported_array_move` | Throws NotSupportedException for Array.UnsafeMov |

## Method Dispatch Functions

| Address | Name | Description |
|---------|------|-------------|
| `0x18042b740` | `il2cpp_method_invoke` | Invokes method with class init check |
| `0x18042bcb0` | `il2cpp_method_invoke_internal` | Internal method invocation |
| `0x180429920` | `il2cpp_method_invoke_params` | Invokes method with output parameters |
| `0x180433b90` | `il2cpp_method_is_generic` | Checks if method is generic |
| `0x18048e850` | `il2cpp_generic_method_inflate` | Inflates generic method with type args |
| `0x18045e840` | `il2cpp_method_get_reflection_object` | Gets MethodBase reflection object |

### Virtual/Interface Dispatch

| Address | Name | Description |
|---------|------|-------------|
| `0x18047f770` | `il2cpp_object_get_virtual_method` | Resolves virtual method through vtable |
| `0x180425ec0` | `il2cpp_interface_resolve_slot` | Resolves interface method slot |
| `0x18043d0a0` | `il2cpp_interface_method_lookup` | Looks up interface method in hierarchy |

## Class/Type Functions

| Address | Name | Description |
|---------|------|-------------|
| `0x180483100` | `il2cpp_class_from_name_internal` | Finds class by namespace/name |
| `0x180470f90` | `il2cpp_class_set_exception` | Sets exception on class (type load error) |
| `0x180495b10` | `il2cpp_com_get_restricted_error` | Gets COM error info for interop |
| `0x180428b20` | `il2cpp_class_init_lock` | Lock for class initialization |

## Utility Functions

| Address | Name | Description |
|---------|------|-------------|
| `0x180428060` | `il2cpp_convert_value` | Converts between value types with type checking |
| `0x180427d40` | `il2cpp_convert_to_int64` | Converts value to 64-bit integer |
| `0x180427ca0` | `il2cpp_clear_ref` | Clears object reference to null |
| `0x180425d90` | `il2cpp_init_stack_trace` | Initializes stack trace buffer |
| `0x180428600` | `il2cpp_invoke_finalizer_callback` | Invokes registered finalizer callback |
| `0x180428630` | `il2cpp_check_hresult` | Validates HRESULT, throws on failure |
| `0x18043b4f0` | `il2cpp_register_finalizer` | Registers finalizer for object cleanup |

## Object Layout

Understanding IL2CPP object layout helps interpret decompiled code:

```
Il2CppObject (base of all managed objects):
  +0x00: Il2CppClass* klass (vtable/type info)
  +0x08: MonitorData* monitor (sync block)
  +0x10: [object fields start here]

Il2CppArray:
  +0x00: Il2CppClass* klass
  +0x08: MonitorData* monitor
  +0x10: Il2CppArrayBounds* bounds (null for 1D arrays)
  +0x18: uint64_t max_length
  +0x20: [array elements start here]

Il2CppString:
  +0x00: Il2CppClass* klass
  +0x08: MonitorData* monitor
  +0x10: int32_t length
  +0x14: char16_t chars[length] (UTF-16)
```

## Example Before/After

**Before renaming:**
```c
void GameMethod(void) {
    local_18 = FUN_1804618f0(DAT_183b94250);
    FUN_18045b840((ulonglong)(local_18 + 0x10));
    FUN_1804e52b0(local_18 + 0x10, param_1, 0x20);
}
```

**After renaming:**
```c
void GameMethod(void) {
    local_18 = il2cpp_object_new(String_class);
    il2cpp_gc_card_table_mark((ulonglong)(local_18 + 0x10));
    il2cpp_memcpy(local_18 + 0x10, param_1, 0x20);
}
```

## Notes

- All addresses are for the GameAssembly.dll analyzed in Ghidra
- Some functions have multiple addresses due to inlining or duplicate symbols
- The GC uses a card table write barrier for generational collection
- Object header is 0x10 bytes (16 bytes) containing klass and monitor pointers
- Array header is 0x20 bytes (32 bytes) including bounds and length

## References

- Unity IL2CPP source: `libil2cpp/` in Unity Editor installation
- IL2CPP internals: https://docs.unity3d.com/Manual/IL2CPP.html
