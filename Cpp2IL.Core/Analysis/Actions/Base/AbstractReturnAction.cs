﻿using System;
using System.Collections.Generic;
using Cpp2IL.Core.Analysis.ResultModels;
using Cpp2IL.Core.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Cpp2IL.Core.Analysis.Actions.Base
{
    public abstract class AbstractReturnAction<T> : BaseAction<T>
    {
        protected IAnalysedOperand? returnValue;
        protected bool _isVoid;

        protected AbstractReturnAction(MethodAnalysis<T> context, T instruction) : base(context, instruction)
        {
            _isVoid = context.IsVoid();
        }

        public override Mono.Cecil.Cil.Instruction[] ToILInstructions(MethodAnalysis<T> context, ILProcessor processor)
        {
            var ret = new List<Mono.Cecil.Cil.Instruction>();

            if (!_isVoid)
            {
                if (returnValue == null)
                    throw new TaintedInstructionException("Return value is missing");

                if (returnValue is LocalDefinition loc && loc.Type?.Resolve() != context.ReturnType.Resolve())
                    throw new TaintedInstructionException($"Return value has a type of {loc.Type}, expecting an object of type {context.ReturnType}");
                
                ret.AddRange(returnValue.GetILToLoad(context, processor));
            }
            
            ret.Add(processor.Create(OpCodes.Ret));

            return ret.ToArray();
        }

        public override string? ToPsuedoCode()
        {
            if (_isVoid)
                return "return";
            
            return $"return {returnValue?.GetPseudocodeRepresentation()}";
        }

        public override string ToTextSummary()
        {
            if (_isVoid)
                return "[!] Returns from the function\n";
            
            return $"[!] Returns {returnValue} from the function\n";
        }

        public override bool IsImportant()
        {
            return true;
        }

        protected void TryCorrectConstant(MethodAnalysis<T> context)
        {
            if (_isVoid || returnValue is not ConstantDefinition constantDefinition || !typeof(IConvertible).IsAssignableFrom(constantDefinition.Type) || constantDefinition.Type == typeof(string)) 
                return;
            
            if (context.ReturnType.Resolve() is {IsEnum: true} returnTypeDefinition)
            {
                var underLyingType = typeof(int).Module.GetType(returnTypeDefinition.GetEnumUnderlyingType().FullName);
                constantDefinition.Type = underLyingType;
                constantDefinition.Value = MiscUtils.ReinterpretBytes((IConvertible) constantDefinition.Value, underLyingType);
            }
            else if (!string.IsNullOrEmpty(context.ReturnType?.FullName))
            {
                var returnValueType = typeof(int).Module.GetType(context.ReturnType!.FullName);
                if (string.IsNullOrEmpty(returnValueType?.FullName) || returnValueType!.IsArray) 
                    return;
                if (!TypeDefinitions.IConvertible.IsAssignableFrom(context.ReturnType) || !context.ReturnType.IsPrimitive || context.ReturnType.Name == "String")
                    return;
                constantDefinition.Value = MiscUtils.ReinterpretBytes((IConvertible) constantDefinition.Value, context.ReturnType);
                constantDefinition.Type = returnValueType;
            }
        }
    }
}