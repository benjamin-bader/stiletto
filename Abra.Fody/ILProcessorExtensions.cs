using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Abra.Fody
{
    public static class ILProcessorExtensions
    {
        public static void EmitBoolean(this ILProcessor processor, bool value)
        {
            var opcode = value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0;
            processor.Emit(opcode);
        }

        public static void EmitType(this ILProcessor processor, TypeReference type)
        {
            processor.Emit(OpCodes.Ldtoken, type);
            processor.Emit(OpCodes.Call, References.Type_GetTypeFromHandle);
        }

        public static void Cast(this ILProcessor processor, TypeReference type)
        {
            if (type.IsGenericParameter) {
                processor.Emit(OpCodes.Unbox_Any, type);
            } else if (type.IsValueType) {
                processor.Emit(OpCodes.Unbox_Any, type);
            } else {
                processor.Emit(OpCodes.Castclass, type);
            }
        }
    }
}
