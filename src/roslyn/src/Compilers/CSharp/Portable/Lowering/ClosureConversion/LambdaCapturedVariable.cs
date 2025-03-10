﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Roslyn.Utilities;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.CSharp.Emit;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.CSharp
{
    /// <summary>
    /// A field of a frame class that represents a variable that has been captured in a lambda.
    /// </summary>
    internal class LambdaCapturedVariable : SynthesizedFieldSymbolBase, ISynthesizedMethodBodyImplementationSymbol
    {
        private readonly TypeWithAnnotations _type;
        private readonly bool _isThis;

        protected LambdaCapturedVariable(SynthesizedClosureEnvironment frame, TypeWithAnnotations type, string fieldName, bool isThisParameter)
            : base(frame,
                   fieldName,
                   isPublic: true,
                   isReadOnly: false,
                   isStatic: false)
        {
            Debug.Assert(type.HasType);

            // lifted fields do not need to have the CompilerGeneratedAttribute attached to it, the closure is already 
            // marked as being compiler generated.
            _type = type;
            _isThis = isThisParameter;
        }

        public SynthesizedClosureEnvironment Frame
            => (SynthesizedClosureEnvironment)ContainingType;

#nullable enable

        public static LambdaCapturedVariable Create(SynthesizedClosureEnvironment frame, Symbol captured, ref int uniqueId)
        {
            Debug.Assert(captured is LocalSymbol || captured is ParameterSymbol);

            string fieldName = GetCapturedVariableFieldName(captured, ref uniqueId);
            TypeSymbol type = GetCapturedVariableFieldType(frame, captured);
            bool isThis = IsThis(captured, out ParameterSymbol? parameter);
            return parameter is not null && !isThis ?
                new LambdaCapturedVariableForRegularParameter(frame, TypeWithAnnotations.Create(type), fieldName, parameter) :
                new LambdaCapturedVariable(frame, TypeWithAnnotations.Create(type), fieldName, isThis);
        }

        private static bool IsThis(Symbol captured)
        {
            return IsThis(captured, out _);
        }

        /// <param name="captured"></param>
        /// <param name="parameter">
        /// Is assigned to <paramref name="captured"/> when it is a <see cref="ParameterSymbol"/>,
        /// null otherwise. Note, the value is not correlated with returned boolean. 
        /// </param>
        private static bool IsThis(Symbol captured, out ParameterSymbol? parameter)
        {
            parameter = captured as ParameterSymbol;
            return (object?)parameter != null && parameter.IsThis;
        }

#nullable disable

        private static string GetCapturedVariableFieldName(Symbol variable, ref int uniqueId)
        {
            if (IsThis(variable))
            {
                return GeneratedNames.ThisProxyFieldName();
            }

            if (variable is LocalSymbol local)
            {
                switch (local.SynthesizedKind)
                {
                    case SynthesizedLocalKind.LambdaDisplayClass:
                        return GeneratedNames.MakeLambdaDisplayLocalName(uniqueId++);
                    case SynthesizedLocalKind.ExceptionFilterAwaitHoistedExceptionLocal:
                    case SynthesizedLocalKind.TryAwaitPendingException:
                    case SynthesizedLocalKind.TryAwaitPendingCaughtException:
                        return GeneratedNames.MakeHoistedLocalFieldName(local.SynthesizedKind, uniqueId++);
                    case SynthesizedLocalKind.InstrumentationPayload:
                        return GeneratedNames.MakeSynthesizedInstrumentationPayloadLocalFieldName(uniqueId++);
                }

                // should never be captured:
                Debug.Assert(local.SynthesizedKind != SynthesizedLocalKind.LocalStoreTracker);

                if (local.SynthesizedKind == SynthesizedLocalKind.UserDefined &&
                    (local.ScopeDesignatorOpt?.Kind() == SyntaxKind.SwitchSection ||
                     local.ScopeDesignatorOpt?.Kind() == SyntaxKind.SwitchExpressionArm))
                {
                    // The programmer can use the same identifier for pattern variables in different
                    // sections of a switch statement, but they are all hoisted into
                    // the same frame for the enclosing switch statement and must be given
                    // unique field names.
                    return GeneratedNames.MakeHoistedLocalFieldName(local.SynthesizedKind, uniqueId++, local.Name);
                }
            }

            Debug.Assert(variable.Name != null);
            return variable.Name;
        }

        private static TypeSymbol GetCapturedVariableFieldType(SynthesizedContainer frame, Symbol variable)
        {
            var local = variable as LocalSymbol;
            if ((object)local != null)
            {
                // if we're capturing a generic frame pointer, construct it with the new frame's type parameters
                var lambdaFrame = local.Type.OriginalDefinition as SynthesizedClosureEnvironment;
                if ((object)lambdaFrame != null)
                {
                    // lambdaFrame may have less generic type parameters than frame, so trim them down (the first N will always match)
                    var typeArguments = frame.TypeArgumentsWithAnnotationsNoUseSiteDiagnostics;
                    if (typeArguments.Length > lambdaFrame.Arity)
                    {
                        typeArguments = ImmutableArray.Create(typeArguments, 0, lambdaFrame.Arity);
                    }

                    return lambdaFrame.ConstructIfGeneric(typeArguments);
                }
            }

            return frame.TypeMap.SubstituteType(((object)local != null ? local.TypeWithAnnotations : ((ParameterSymbol)variable).TypeWithAnnotations).Type).Type;
        }

        public override RefKind RefKind => RefKind.None;

        public override ImmutableArray<CustomModifier> RefCustomModifiers => ImmutableArray<CustomModifier>.Empty;

        internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound)
        {
            return _type;
        }

        internal override bool IsCapturedFrame
        {
            get
            {
                return _isThis;
            }
        }

        internal override bool SuppressDynamicAttribute
        {
            get
            {
                return false;
            }
        }

        public IMethodSymbolInternal Method
            => Frame.TopLevelMethod;

        /// <summary>
        /// When the containing top-level method body is updated we don't need to attempt to update field (it has no "body").
        /// </summary>
        public bool HasMethodBodyDependency
            => false;
    }

    internal sealed class LambdaCapturedVariableForRegularParameter : LambdaCapturedVariable
    {
        private readonly ParameterSymbol _parameter;

        public LambdaCapturedVariableForRegularParameter(SynthesizedClosureEnvironment frame, TypeWithAnnotations type, string fieldName, ParameterSymbol parameter)
            : base(frame, type, fieldName, isThisParameter: false)
        {
            Debug.Assert(parameter is { IsThis: false });
            _parameter = parameter;
        }

        internal override void AddSynthesizedAttributes(PEModuleBuilder moduleBuilder, ref ArrayBuilder<CSharpAttributeData> attributes)
        {
            if (_parameter.OriginalDefinition is SourceParameterSymbolBase definition &&
                ContainingModule == definition.ContainingModule)
            {
                foreach (CSharpAttributeData attr in definition.GetAttributes())
                {
                    if (attr.AttributeClass is { HasCompilerLoweringPreserveAttribute: true } attributeType &&
                        (attributeType.GetAttributeUsageInfo().ValidTargets & System.AttributeTargets.Field) != 0)
                    {
                        AddSynthesizedAttribute(ref attributes, attr);
                    }
                }
            }

            base.AddSynthesizedAttributes(moduleBuilder, ref attributes);
        }
    }
}
