// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Internal.TypeSystem;
using ILCompiler.DependencyAnalysis.ARM;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// ARM specific portions of ReadyToRunHelperNode
    /// </summary>
    public partial class ReadyToRunHelperNode
    {
        protected override void EmitCode(NodeFactory factory, ref ARMEmitter encoder, bool relocsOnly)
        {
            switch (Id)
            {
                case ReadyToRunHelperId.GetNonGCStaticBase:
                    {
                        MetadataType target = (MetadataType)Target;
                        bool hasLazyStaticConstructor = factory.PreinitializationManager.HasLazyStaticConstructor(target);

                        if (!hasLazyStaticConstructor)
                        {
                            encoder.EmitMOV(encoder.TargetRegister.Result, factory.TypeNonGCStaticsSymbol(target));
                            encoder.EmitRET();
                        }
                        else
                        {
                            // The fast path check is not necessary. It is always expanded by RyuJIT.
                            encoder.EmitMOV(encoder.TargetRegister.Arg1, factory.TypeNonGCStaticsSymbol(target));
                            encoder.EmitMOV(encoder.TargetRegister.Arg0/*Result*/, encoder.TargetRegister.Arg1);
                            encoder.EmitSUB(encoder.TargetRegister.Arg0, NonGCStaticsNode.GetClassConstructorContextSize(factory.Target));
                            encoder.EmitJMP(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnNonGCStaticBase));
                        }
                    }
                    break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                    {
                        MetadataType target = (MetadataType)Target;
                        encoder.EmitMOV(encoder.TargetRegister.Arg2, factory.TypeThreadStaticIndex(target));

                        // First arg: address of the TypeManager slot that provides the helper with
                        // information about module index and the type manager instance (which is used
                        // for initialization on first access).
                        encoder.EmitLDR(encoder.TargetRegister.Arg0, encoder.TargetRegister.Arg2);

                        // Second arg: index of the type in the ThreadStatic section of the modules
                        encoder.EmitLDR(encoder.TargetRegister.Arg1, encoder.TargetRegister.Arg2, factory.Target.PointerSize);

                        if (!factory.PreinitializationManager.HasLazyStaticConstructor(target))
                        {
                            encoder.EmitJMP(factory.HelperEntrypoint(HelperEntrypoint.GetThreadStaticBaseForType));
                        }
                        else
                        {
                            encoder.EmitMOV(encoder.TargetRegister.Arg2, factory.TypeNonGCStaticsSymbol(target));
                            encoder.EmitSUB(encoder.TargetRegister.Arg2, NonGCStaticsNode.GetClassConstructorContextSize(factory.Target));
                            // TODO: performance optimization - inline the check verifying whether we need to trigger the cctor
                            encoder.EmitJMP(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnThreadStaticBase));
                        }
                    }
                    break;

                case ReadyToRunHelperId.GetGCStaticBase:
                    {
                        MetadataType target = (MetadataType)Target;
                        encoder.EmitMOV(encoder.TargetRegister.Result, factory.TypeGCStaticsSymbol(target));
                        if (!factory.PreinitializationManager.HasLazyStaticConstructor(target))
                        {
                            encoder.EmitLDR(encoder.TargetRegister.Result, encoder.TargetRegister.Result);
                            encoder.EmitRET();
                        }
                        else
                        {
                            // The fast path check is not necessary. It is always expanded by RyuJIT.
                            encoder.EmitLDR(encoder.TargetRegister.Arg1, encoder.TargetRegister.Result);
                            encoder.EmitMOV(encoder.TargetRegister.Arg0, factory.TypeNonGCStaticsSymbol(target));
                            encoder.EmitSUB(encoder.TargetRegister.Arg0, NonGCStaticsNode.GetClassConstructorContextSize(factory.Target));
                            encoder.EmitJMP(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnGCStaticBase));
                        }
                    }
                    break;

                case ReadyToRunHelperId.DelegateCtor:
                    {
                        DelegateCreationInfo target = (DelegateCreationInfo)Target;

                        if (target.TargetNeedsVTableLookup)
                        {
                            Debug.Assert(!target.TargetMethod.CanMethodBeInSealedVTable(factory));

                            encoder.EmitLDR(encoder.TargetRegister.Arg2, encoder.TargetRegister.Arg1);

                            int slot = 0;
                            if (!relocsOnly)
                                slot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, target.TargetMethod, target.TargetMethod.OwningType);

                            Debug.Assert(slot != -1);
                            encoder.EmitLDR(encoder.TargetRegister.Arg2, encoder.TargetRegister.Arg2,
                                            EETypeNode.GetVTableOffset(factory.Target.PointerSize) + (slot * factory.Target.PointerSize));
                        }
                        else
                        {
                            encoder.EmitMOV(encoder.TargetRegister.Arg2, target.GetTargetNode(factory));
                        }

                        if (target.Thunk != null)
                        {
                            Debug.Assert(target.Constructor.Method.Signature.Length == 3);
                            encoder.EmitMOV(encoder.TargetRegister.Arg3, target.Thunk);
                        }
                        else
                        {
                            Debug.Assert(target.Constructor.Method.Signature.Length == 2);
                        }

                        encoder.EmitJMP(target.Constructor);
                    }
                    break;

                case ReadyToRunHelperId.ResolveVirtualFunction:
                    {
                        MethodDesc targetMethod = (MethodDesc)Target;
                        if (targetMethod.OwningType.IsInterface)
                        {
                            encoder.EmitMOV(encoder.TargetRegister.Arg1, factory.InterfaceDispatchCell(targetMethod));
                            encoder.EmitJMP(factory.ExternFunctionSymbol("RhpResolveInterfaceMethod"));
                        }
                        else
                        {
                            if (relocsOnly)
                                break;

                            encoder.EmitLDR(encoder.TargetRegister.Result, encoder.TargetRegister.Arg0);

                            Debug.Assert(!targetMethod.CanMethodBeInSealedVTable(factory));

                            int slot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, targetMethod, targetMethod.OwningType);
                            Debug.Assert(slot != -1);
                            encoder.EmitLDR(encoder.TargetRegister.Result, encoder.TargetRegister.Result,
                                            ((short)(EETypeNode.GetVTableOffset(factory.Target.PointerSize) + (slot * factory.Target.PointerSize))));
                            encoder.EmitRET();
                        }
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
