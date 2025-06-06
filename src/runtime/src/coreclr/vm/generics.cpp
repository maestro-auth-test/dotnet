// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// File: generics.cpp
//


//
// Helper functions for generics prototype
//

//
// ============================================================================

#include "common.h"
#include "method.hpp"
#include "field.h"
#include "eeconfig.h"
#include "generics.h"
#include "genericdict.h"
#include "typestring.h"
#include "typekey.h"
#include "dumpcommon.h"
#include "array.h"

/* static */
TypeHandle ClassLoader::CanonicalizeGenericArg(TypeHandle thGenericArg)
{
    CONTRACT(TypeHandle)
    {
        NOTHROW;
        GC_NOTRIGGER;
        POSTCONDITION(CheckPointer(RETVAL));
    }
    CONTRACT_END

#if defined(FEATURE_SHARE_GENERIC_CODE)
    CorElementType et = thGenericArg.GetSignatureCorElementType();

    // Note that generic variables do not share

    if (CorTypeInfo::IsObjRef_NoThrow(et))
        RETURN(TypeHandle(g_pCanonMethodTableClass));

    if (et == ELEMENT_TYPE_VALUETYPE)
    {
        // Don't share structs. But sharability must be propagated through
        // them (i.e. struct<object> * shares with struct<string> *)
        RETURN(TypeHandle(thGenericArg.GetCanonicalMethodTable()));
    }

    _ASSERTE(et != ELEMENT_TYPE_PTR && et != ELEMENT_TYPE_FNPTR);
    RETURN(thGenericArg);
#else
    RETURN (thGenericArg);
#endif // FEATURE_SHARE_GENERIC_CODE
}

 // Given the build-time ShareGenericCode setting, is the specified type
// representation-sharable as a type parameter to a generic type or method ?
/* static */ BOOL ClassLoader::IsSharableInstantiation(Instantiation inst)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        FORBID_FAULT;
    }
    CONTRACTL_END

    for (DWORD i = 0; i < inst.GetNumArgs(); i++)
    {
        if (CanonicalizeGenericArg(inst[i]).IsCanonicalSubtype())
            return TRUE;
    }
    return FALSE;
}

/* static */ BOOL ClassLoader::IsCanonicalGenericInstantiation(Instantiation inst)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        FORBID_FAULT;
    }
    CONTRACTL_END

    for (DWORD i = 0; i < inst.GetNumArgs(); i++)
    {
        if (CanonicalizeGenericArg(inst[i]) != inst[i])
            return FALSE;
    }
    return TRUE;
}

/* static */ BOOL ClassLoader::IsTypicalSharedInstantiation(Instantiation inst)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        FORBID_FAULT;
    }
    CONTRACTL_END

    for (DWORD i = 0; i < inst.GetNumArgs(); i++)
    {
        if (inst[i] != TypeHandle(g_pCanonMethodTableClass))
            return FALSE;
    }
    return TRUE;
}

#ifndef DACCESS_COMPILE

TypeHandle ClassLoader::LoadCanonicalGenericInstantiation(const TypeKey *pTypeKey,
                                                          LoadTypesFlag fLoadTypes/*=LoadTypes*/,
                                                          ClassLoadLevel level/*=CLASS_LOADED*/)
{
    CONTRACT(TypeHandle)
    {
        if (FORBIDGC_LOADER_USE_ENABLED()) NOTHROW; else THROWS;
        if (FORBIDGC_LOADER_USE_ENABLED()) GC_NOTRIGGER; else GC_TRIGGERS;
        if (FORBIDGC_LOADER_USE_ENABLED() || fLoadTypes != LoadTypes) { LOADS_TYPE(CLASS_LOAD_BEGIN); } else { LOADS_TYPE(level); }
        POSTCONDITION(CheckPointer(RETVAL, ((fLoadTypes == LoadTypes) ? NULL_NOT_OK : NULL_OK)));
        POSTCONDITION(RETVAL.IsNull() || RETVAL.CheckLoadLevel(level));
    }
    CONTRACT_END

    Instantiation inst = pTypeKey->GetInstantiation();
    DWORD ntypars = inst.GetNumArgs();

    // Canonicalize the type arguments.
    DWORD dwAllocSize = 0;
    if (!ClrSafeInt<DWORD>::multiply(ntypars, sizeof(TypeHandle), dwAllocSize))
        ThrowHR(COR_E_OVERFLOW);

    TypeHandle ret = TypeHandle();
    TypeHandle *repInst = (TypeHandle*) _alloca(dwAllocSize);

    for (DWORD i = 0; i < ntypars; i++)
    {
        repInst[i] = ClassLoader::CanonicalizeGenericArg(inst[i]);
    }

    // Load the canonical instantiation
    TypeKey canonKey(pTypeKey->GetModule(), pTypeKey->GetTypeToken(), Instantiation(repInst, ntypars));
    ret = ClassLoader::LoadConstructedTypeThrowing(&canonKey, fLoadTypes, level);

    RETURN(ret);
}

// Create a non-canonical instantiation of a generic type, by
// copying the method table of the canonical instantiation
//
/* static */
TypeHandle
ClassLoader::CreateTypeHandleForNonCanonicalGenericInstantiation(
    const TypeKey         *pTypeKey,
    AllocMemTracker *pamTracker)
{
    CONTRACT(TypeHandle)
    {
        STANDARD_VM_CHECK;
        PRECONDITION(CheckPointer(pTypeKey));
        PRECONDITION(CheckPointer(pamTracker));
        PRECONDITION(pTypeKey->HasInstantiation());
        PRECONDITION(ClassLoader::IsSharableInstantiation(pTypeKey->GetInstantiation()));
        PRECONDITION(!TypeHandle::IsCanonicalSubtypeInstantiation(pTypeKey->GetInstantiation()));
        POSTCONDITION(CheckPointer(RETVAL));
        POSTCONDITION(RETVAL.CheckMatchesKey(pTypeKey));
    }
    CONTRACT_END

    Module *pLoaderModule = ClassLoader::ComputeLoaderModule(pTypeKey);
    LoaderAllocator* pAllocator=pLoaderModule->GetLoaderAllocator();

    Instantiation inst = pTypeKey->GetInstantiation();
    pAllocator->EnsureInstantiation(pTypeKey->GetModule(), inst);
    DWORD ntypars = inst.GetNumArgs();

#ifdef _DEBUG
    if (LoggingOn(LF_CLASSLOADER, LL_INFO1000) || g_pConfig->BreakOnInstantiationEnabled())
    {
        StackSString debugTypeKeyName;
        TypeString::AppendTypeKeyDebug(debugTypeKeyName, pTypeKey);
        LOG((LF_CLASSLOADER, LL_INFO1000, "GENERICS: New instantiation requested: %s\n", debugTypeKeyName.GetUTF8()));

        if (g_pConfig->ShouldBreakOnInstantiation(debugTypeKeyName.GetUTF8()))
            CONSISTENCY_CHECK_MSGF(false, ("BreakOnInstantiation: typename '%s' ", debugTypeKeyName.GetUTF8()));
    }
#endif // _DEBUG

    TypeHandle canonType;
    {
        OVERRIDE_TYPE_LOAD_LEVEL_LIMIT(CLASS_LOAD_APPROXPARENTS);
        canonType = ClassLoader::LoadCanonicalGenericInstantiation(pTypeKey, ClassLoader::LoadTypes, CLASS_LOAD_APPROXPARENTS);
    }

    // Now fabricate a method table
    MethodTable* pOldMT = canonType.AsMethodTable();

    // We only need true vtable entries as the rest can be found in the representative method table
    WORD cSlots = static_cast<WORD>(pOldMT->GetNumVirtuals());

    BOOL fContainsGenericVariables = MethodTable::ComputeContainsGenericVariables(inst);

    // These are all copied across from the old MT, i.e. don't depend on the
    // instantiation.
    BOOL fHasGenericsStaticsInfo = pOldMT->HasGenericsStaticsInfo();

#ifdef FEATURE_COMINTEROP
    BOOL fHasDynamicInterfaceMap = pOldMT->HasDynamicInterfaceMap();
#else // FEATURE_COMINTEROP
    BOOL fHasDynamicInterfaceMap = FALSE;
#endif // FEATURE_COMINTEROP

    // The number of bytes used for GC info
    size_t cbGC = pOldMT->ContainsGCPointers() ? ((CGCDesc*) pOldMT)->GetSize() : 0;

    // Bytes are required for the vtable itself
    S_SIZE_T safe_cbMT = S_SIZE_T( cbGC ) + S_SIZE_T( sizeof(MethodTable) );
    safe_cbMT += MethodTable::GetNumVtableIndirections(cSlots) * sizeof(MethodTable::VTableIndir_t);
    if (safe_cbMT.IsOverflow())
    {
        ThrowHR(COR_E_OVERFLOW);
    }
    const size_t cbMT = safe_cbMT.Value();

    // After the optional members (see below) comes the duplicated interface map.
    // For dynamic interfaces the interface map area begins one word
    // before the location returned by GetInterfaceMap()
    WORD wNumInterfaces = static_cast<WORD>(pOldMT->GetNumInterfaces());
    DWORD cbIMap = pOldMT->GetInterfaceMapSize();
    InterfaceInfo_t * pOldIMap = (InterfaceInfo_t *)pOldMT->GetInterfaceMap();

    // NonVirtualSlots, and DispatchMap are used
    // from the canonical methodtable, so we do not need to store them here.

    // We need space for the optional members.
    DWORD cbOptional = MethodTable::GetOptionalMembersAllocationSize(wNumInterfaces != 0);

    // We need space for the PerInstInfo, i.e. the generic dictionary pointers...
    DWORD cbPerInst = sizeof(GenericsDictInfo) + pOldMT->GetPerInstInfoSize();

    // Finally we need space for the instantiation/dictionary for this type
    // Note that it is possible for the dictionary layout to be expanded in size by other threads while we're still
    // creating this type. In other words: this type will have a smaller dictionary that its layout. This is not a
    // problem however because whenever we need to load a value from the dictionary of this type beyond its size, we
    // will expand the dictionary at that point.
    DWORD cbInstAndDictSlotSize;
    DWORD cbInstAndDictAllocSize = pOldMT->GetInstAndDictSize(&cbInstAndDictSlotSize);

    // Allocate from the high frequence heap of the correct domain
    S_SIZE_T allocSize = safe_cbMT;
    allocSize += cbOptional;
    allocSize += cbIMap;
    allocSize += cbPerInst;
    allocSize += cbInstAndDictAllocSize;

    if (allocSize.IsOverflow())
    {
        ThrowHR(COR_E_OVERFLOW);
    }

    if (allocSize.IsOverflow())
    {
        ThrowHR(COR_E_OVERFLOW);
    }

    BYTE* pMemory = (BYTE *) pamTracker->Track(pAllocator->GetHighFrequencyHeap()->AllocMem( allocSize ));

    // Head of MethodTable memory
    MethodTable *pMT = (MethodTable*) (pMemory + cbGC);

    // Copy of GC
    memcpy((BYTE*)pMT - cbGC, (BYTE*) pOldMT - cbGC, cbGC);

    // Allocate the private data block
    MethodTableStaticsFlags staticsFlags = MethodTableStaticsFlags::None;
    if (fHasGenericsStaticsInfo)
        staticsFlags |= MethodTableStaticsFlags::Generic | MethodTableStaticsFlags::Present;
    if (pOldMT->GetNumThreadStaticFields() > 0)
        staticsFlags |= MethodTableStaticsFlags::Thread;

    pMT->AllocateAuxiliaryData(pAllocator, pLoaderModule, pamTracker, staticsFlags);
    pMT->SetModule(pOldMT->GetModule());

    pMT->GetAuxiliaryDataForWrite()->SetIsNotFullyLoadedForBuildMethodTable();

    // <TODO> this is incredibly fragile.  We should just construct the MT all over agin. </TODO>
    pMT->CopyFlags(pOldMT);

    pMT->SetFlag(MethodTable::enum_flag_HasPerInstInfo);
    pMT->ClearFlag(MethodTable::enum_flag_HasDispatchMapSlot);

    // Set generics flags
    pMT->ClearFlag(MethodTable::enum_flag_GenericsMask);
    pMT->SetFlag(MethodTable::enum_flag_GenericsMask_GenericInst);

    pMT->m_pParentMethodTable = NULL;

    pMT->SetBaseSize(pOldMT->GetBaseSize());
    pMT->SetParentMethodTable(pOldMT->GetParentMethodTable());
    pMT->SetCanonicalMethodTable(pOldMT);

    pMT->m_wNumInterfaces = pOldMT->m_wNumInterfaces;

#ifdef FEATURE_TYPEEQUIVALENCE
    if (pMT->IsInterface() && !pMT->HasTypeEquivalence())
    {
        // fHasTypeEquivalence flag is "inherited" from generic arguments so we can quickly detect
        // types like IList<IFoo> where IFoo is an interface with the TypeIdentifierAttribute.
        for (DWORD i = 0; i < ntypars; i++)
        {
            if (inst[i].HasTypeEquivalence())
            {
                pMT->SetHasTypeEquivalence();
                break;
            }
        }
    }
#endif // FEATURE_TYPEEQUIVALENCE

    // Number of slots only includes vtable slots
    pMT->SetNumVirtuals(cSlots);

    // Fill out the vtable indirection slots
    MethodTable::VtableIndirectionSlotIterator it = pMT->IterateVtableIndirectionSlots();
    while (it.Next())
    {
        // Share the canonical chunk
        it.SetIndirectionSlot(pOldMT->GetVtableIndirections()[it.GetIndex()]);
    }

    // All flags on m_pNgenPrivateData data apart
    // are initially false for a dynamically generated instantiation.

    if (fContainsGenericVariables)
        pMT->SetContainsGenericVariables();

    // Since we are fabricating a new MT based on an existing one, the per-inst info should
    // be non-null
    _ASSERTE(pOldMT->HasPerInstInfo());

    // Fill in per-inst map pointer (which points to the array of generic dictionary pointers)
    pMT->SetPerInstInfo((MethodTable::PerInstInfoElem_t *) (pMemory + cbMT + cbOptional + cbIMap + sizeof(GenericsDictInfo)));

    if (fHasGenericsStaticsInfo)
        pMT->SetDynamicStatics();

    _ASSERTE(FitsIn<WORD>(pOldMT->GetNumDicts()));
    _ASSERTE(FitsIn<WORD>(pOldMT->GetNumGenericArgs()));
    pMT->SetDictInfo(static_cast<WORD>(pOldMT->GetNumDicts()), static_cast<WORD>(pOldMT->GetNumGenericArgs()));

    // Fill in the last entry in the array of generic dictionary pointers ("per inst info")
    // The others are filled in by LoadExactParents which copied down any inherited generic
    // dictionary pointers.
    Dictionary * pDict = (Dictionary*) (pMemory + cbMT + cbOptional + cbIMap + cbPerInst);
    MethodTable::PerInstInfoElem_t *pPInstInfo = (MethodTable::PerInstInfoElem_t *) (pMT->GetPerInstInfo() + (pOldMT->GetNumDicts()-1));
    *pPInstInfo = pDict;

    // Fill in the instantiation section of the generic dictionary.  The remainder of the
    // generic dictionary will be zeroed, which is the correct initial state.
    TypeHandle * pInstDest = (TypeHandle *)pDict->GetInstantiation();
    for (DWORD iArg = 0; iArg < ntypars; iArg++)
    {
        pInstDest[iArg] = inst[iArg];
    }

    PTR_DictionaryLayout pLayout = pOldMT->GetClass()->GetDictionaryLayout();
    if (pLayout != NULL)
    {
        _ASSERTE(pLayout->GetMaxSlots() > 0);
        PTR_Dictionary pDictionarySlots = pMT->GetPerInstInfo()[pOldMT->GetNumDicts() - 1];
        DWORD* pSizeSlot = (DWORD*)(pDictionarySlots + ntypars);
        *pSizeSlot = cbInstAndDictSlotSize;
    }

    // Copy interface map across
    InterfaceInfo_t * pInterfaceMap = (InterfaceInfo_t *)(pMemory + cbMT + cbOptional + (fHasDynamicInterfaceMap ? sizeof(DWORD_PTR) : 0));

#ifdef FEATURE_COMINTEROP
    // Extensible RCW's are prefixed with the count of dynamic interfaces.
    if (fHasDynamicInterfaceMap)
    {
        *(((DWORD_PTR *)pInterfaceMap) - 1) = 0;
    }
#endif // FEATURE_COMINTEROP

    for (WORD iItf = 0; iItf < wNumInterfaces; iItf++)
    {
        OVERRIDE_TYPE_LOAD_LEVEL_LIMIT(CLASS_LOAD_APPROXPARENTS);
        pInterfaceMap[iItf].SetMethodTable(pOldIMap[iItf].GetApproxMethodTable(pOldMT->GetLoaderModule()));
    }

    // Set the interface map pointer stored in the main section of the vtable (actually
    // an optional member) to point to the correct region within the newly
    // allocated method table.

    // Fill in interface map pointer
    pMT->SetInterfaceMap(wNumInterfaces, pInterfaceMap);
    if (pMT->IsNullable())
    {
        pMT->SetNullableDetails((UINT16)pOldMT->GetNullableValueAddrOffset(), (UINT16)pOldMT->GetNullableValueSize());
    }

    // Copy across extra flags for these interfaces as well. We may need additional memory for this.
    PVOID pExtraInterfaceInfo = NULL;
    SIZE_T cbExtraInterfaceInfo = MethodTable::GetExtraInterfaceInfoSize(wNumInterfaces);
    if (cbExtraInterfaceInfo)
        pExtraInterfaceInfo = pamTracker->Track(pAllocator->GetLowFrequencyHeap()->AllocMem(S_SIZE_T(cbExtraInterfaceInfo)));

    // Call this even in the case where pExtraInterfaceInfo == NULL (certain cases are optimized and don't
    // require extra buffer space).
    pMT->InitializeExtraInterfaceInfo(pExtraInterfaceInfo);

    for (UINT32 i = 0; i < pOldMT->GetNumInterfaces(); i++)
    {
        if (pOldMT->IsInterfaceDeclaredOnClass(i))
            pMT->SetInterfaceDeclaredOnClass(i);
    }

    pMT->SetLoaderAllocator(pAllocator);

#ifdef _DEBUG
    // Name for debugging
    StackSString debug_ClassNameString;
    TypeString::AppendTypeKey(debug_ClassNameString, pTypeKey, TypeString::FormatNamespace | TypeString::FormatAngleBrackets | TypeString::FormatFullInst);
    const char *debug_szClassNameBuffer = debug_ClassNameString.GetUTF8();
    S_SIZE_T safeLen = S_SIZE_T(strlen(debug_szClassNameBuffer)) + S_SIZE_T(1);
    if (safeLen.IsOverflow()) COMPlusThrowHR(COR_E_OVERFLOW);

    size_t len = safeLen.Value();
    char *debug_szClassName = (char *)pamTracker->Track(pAllocator->GetLowFrequencyHeap()->AllocMem(safeLen));
    strcpy_s(debug_szClassName, len, debug_szClassNameBuffer);
    pMT->SetDebugClassName(debug_szClassName);

    // Debugging information
    if (pOldMT->Debug_HasInjectedInterfaceDuplicates())
        pMT->Debug_SetHasInjectedInterfaceDuplicates();
#endif // _DEBUG

    // No need to generate IDs for open types.   However
    // we still leave the optional member in the MethodTable holding the value -1 for the ID.
    if (fHasGenericsStaticsInfo)
    {
        FieldDesc* pStaticFieldDescs = NULL;

        if (pOldMT->GetNumStaticFields() != 0)
        {
            pStaticFieldDescs = (FieldDesc*) pamTracker->Track(pAllocator->GetLowFrequencyHeap()->AllocMem(S_SIZE_T(sizeof(FieldDesc)) * S_SIZE_T(pOldMT->GetNumStaticFields())));
            FieldDesc* pOldFD = pOldMT->GetGenericsStaticFieldDescs();
            for (DWORD i = 0; i < pOldMT->GetNumStaticFields(); i++)
            {
                pStaticFieldDescs[i].InitializeFrom(pOldFD[i], pMT);
            }
        }
        pMT->SetupGenericsStaticsInfo(pStaticFieldDescs);
    }


    // VTS info doesn't depend on the exact instantiation but we make a copy
    // anyway since we can't currently deal with the possibility of having a
    // cross module pointer to the data block. Eventually we might be able to
    // tokenize this reference, but determine first whether there's enough
    // performance degradation to justify the extra complexity.

    pMT->SetCl(pOldMT->GetCl());

    // Check we've set up the flags correctly on the new method table
    _ASSERTE(!fContainsGenericVariables == !pMT->ContainsGenericVariables());
    _ASSERTE(!fHasGenericsStaticsInfo == !pMT->HasGenericsStaticsInfo());
#ifdef FEATURE_COMINTEROP
    _ASSERTE(!fHasDynamicInterfaceMap == !pMT->HasDynamicInterfaceMap());
#endif

    LOG((LF_CLASSLOADER, LL_INFO1000, "GENERICS: Replicated methodtable to create type %s\n", pMT->GetDebugClassName()));

#ifdef _DEBUG
    if (g_pConfig->ShouldDumpOnClassLoad(debug_szClassName))
    {
        LOG((LF_ALWAYS, LL_ALWAYS,
            "Method table summary for '%s' (instantiation):\n",
            pMT->GetDebugClassName()));
        pMT->Debug_DumpInterfaceMap("Approximate");
    }
#endif //_DEBUG

    // We never have non-virtual slots in this method table (set SetNumVtableSlots and SetNumVirtuals above)
    _ASSERTE(!pMT->HasNonVirtualSlots());

    RETURN(TypeHandle(pMT));
} // ClassLoader::CreateTypeHandleForNonCanonicalGenericInstantiation

namespace Generics
{

BOOL CheckInstantiation(Instantiation inst)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
    }
    CONTRACTL_END

    for (DWORD i = 0; i < inst.GetNumArgs(); i++)
    {
        TypeHandle th = inst[i];
        if (th.IsNull())
        {
            return FALSE;
        }

        CorElementType type = th.GetSignatureCorElementType();
        if (CorTypeInfo::IsGenericVariable_NoThrow(type))
        {
            return TRUE;
        }

        if (   type == ELEMENT_TYPE_BYREF
            || type == ELEMENT_TYPE_TYPEDBYREF
            || type == ELEMENT_TYPE_VOID
            || type == ELEMENT_TYPE_PTR
            || type == ELEMENT_TYPE_FNPTR)
        {
            return FALSE;
        }
    }
    return TRUE;
}

// Just records the owner and links to the previous graph.
RecursionGraph::RecursionGraph(RecursionGraph *pPrev, TypeHandle thOwner)
{
    LIMITED_METHOD_CONTRACT;

    m_pPrev   = pPrev;
    m_thOwner = thOwner;

    m_pNodes  = NULL;
}

RecursionGraph::~RecursionGraph()
{
    WRAPPER_NO_CONTRACT;
    if (m_pNodes != NULL)
        delete [] m_pNodes;
}

// Adds edges generated by the parent and implemented interfaces; returns TRUE iff
// an expanding cycle was found.
BOOL RecursionGraph::CheckForIllegalRecursion()
{
    CONTRACTL
    {
        THROWS;
        GC_TRIGGERS;
        PRECONDITION(!m_thOwner.IsTypeDesc());
    }
    CONTRACTL_END;

    MethodTable *pMT = m_thOwner.AsMethodTable();

    Instantiation inst = pMT->GetInstantiation();

    // Initialize the node array.
    m_pNodes = new Node[inst.GetNumArgs()];

    for (DWORD i = 0; i < inst.GetNumArgs(); i++)
    {
        m_pNodes[i].SetSourceVar(inst[i].AsGenericVariable());
    }

    // Record edges generated by inheriting from the parent.
    MethodTable *pParentMT = pMT->GetParentMethodTable();
    if (pParentMT)
    {
        AddDependency(pParentMT);
    }

    // Record edges generated by implementing interfaces.
    MethodTable::InterfaceMapIterator it = pMT->IterateInterfaceMap();
    while (it.Next())
    {
        MethodTable *pItfApprox = it.GetInterfaceApprox();
        if (!pItfApprox->IsTypicalTypeDefinition())
        {
            AddDependency(pItfApprox);
        }
    }

    // Check all owned nodes for expanding cycles. The edges recorded above must all
    // go from owned nodes so it suffices to look only at these.
    for (DWORD i = 0; i < inst.GetNumArgs(); i++)
    {
        if (HasExpandingCycle(&m_pNodes[i], &m_pNodes[i]))
            return TRUE;
    }

    return FALSE;
}

// Returns TRUE iff the given type is already on the stack (in fact an analogue of
// code:TypeHandleList::Exists).
//
// static
BOOL RecursionGraph::HasSeenType(RecursionGraph *pDepGraph, TypeHandle thType)
{
    LIMITED_METHOD_CONTRACT;

    while (pDepGraph != NULL)
    {
        if (pDepGraph->m_thOwner == thType) return TRUE;
        pDepGraph = pDepGraph->m_pPrev;
    }
    return FALSE;
}

// Adds the specified MT as a dependency (parent or interface) of the owner.
void RecursionGraph::AddDependency(MethodTable *pMT, TypeHandleList *pExpansionVars /*= NULL*/)
{
    CONTRACTL
    {
        THROWS;
        GC_TRIGGERS;
        PRECONDITION(pMT != NULL);
    }
    CONTRACTL_END

    // ECMA:
    // - If T appears as the actual type argument to be substituted for U in some referenced
    //   type D<..., U, ...> add a non-expanding (->) edge from T to U.
    // - If T appears somewhere inside (but not as) the actual type argument to be substituted
    //   for U in referenced type D<..., U, ...> add an expanding (=>) edge from T to U.

    // Non-generic dependencies are not interesting.
    if (!pMT->HasInstantiation())
        return;

    // Get the typical instantiation of pMT to figure out its type vars.
    TypeHandle thTypical = ClassLoader::LoadTypeDefThrowing(
        pMT->GetModule(), pMT->GetCl(),
        ClassLoader::ThrowIfNotFound,
        ClassLoader::PermitUninstDefOrRef, tdNoTypes,
        CLASS_LOAD_APPROXPARENTS);

    Instantiation inst = pMT->GetInstantiation();
    Instantiation typicalInst = thTypical.GetInstantiation();

    _ASSERTE(inst.GetNumArgs() == typicalInst.GetNumArgs());

    for (DWORD i = 0; i < inst.GetNumArgs(); i++)
    {
        TypeHandle thArg = inst[i];
        TypeHandle thVar = typicalInst[i];
        if (thArg.IsGenericVariable())
        {
            // Add a non-expanding edge from thArg to i-th generic parameter of pMT.
            AddEdge(thArg.AsGenericVariable(), thVar.AsGenericVariable(), FALSE);

            // Process the backlog.
            TypeHandle thTo;
            TypeHandleList *pList = pExpansionVars;
            while (TypeHandleList::GetNext(&pList, &thTo))
            {
                AddEdge(thArg.AsGenericVariable(), thTo.AsGenericVariable(), TRUE);
            }
        }
        else
        {
            while (thArg.HasTypeParam())
            {
                thArg = thArg.GetTypeParam();

                if (thArg.IsGenericVariable()) // : A<!T[]>
                {
                    // Add an expanding edge from thArg to i-th parameter of pMT.
                    AddEdge(thArg.AsGenericVariable(), thVar.AsGenericVariable(), TRUE);
                    break;
                }
            }

            if (!thArg.IsTypeDesc()) // : A<B<!T>>
            {
                // We will add an expanding edge but we do not yet know from which variable(s).
                // Add the to-variable to the list and call recursively to inspect thArg's
                // instantiation.
                TypeHandleList newExpansionVars(thVar, pExpansionVars);
                AddDependency(thArg.AsMethodTable(), &newExpansionVars);
            }
            else
            {
                _ASSERTE(thArg.IsGenericVariable());
            }
        }
    }
}

// Add an edge from pFromVar to pToVar - either non-expanding or expanding.
void RecursionGraph::AddEdge(TypeVarTypeDesc *pFromVar, TypeVarTypeDesc *pToVar, BOOL fExpanding)
{
    CONTRACTL
    {
        THROWS;
        GC_NOTRIGGER;
        PRECONDITION(pFromVar != NULL);
        PRECONDITION(pToVar != NULL);
    }
    CONTRACTL_END

    LOG((LF_CLASSLOADER, LL_INFO10000, "GENERICS: Adding %s edge: from %x(0x%x) to %x(0x%x) into recursion graph owned by MT: %x\n",
        (fExpanding ? "EXPANDING" : "NON-EXPANDING"),
        pFromVar->GetToken(), pFromVar->GetModule(),
        pToVar->GetToken(), pToVar->GetModule(),
        m_thOwner.AsMethodTable()));

    // Get the source node.
    Node *pNode = &m_pNodes[pFromVar->GetIndex()];
    _ASSERTE(pFromVar == pNode->GetSourceVar());

    // Add the edge.
    ULONG_PTR edge = (ULONG_PTR)pToVar;
    if (fExpanding) edge |= Node::EDGE_EXPANDING_FLAG;

    IfFailThrow(pNode->GetEdges()->Append((void *)edge));
}

// Recursive worker that checks whether this node is part of an expanding cycle.
BOOL RecursionGraph::HasExpandingCycle(Node *pCurrentNode, Node *pStartNode, BOOL fExpanded /*= FALSE*/)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        PRECONDITION(CheckPointer(pCurrentNode));
        PRECONDITION(CheckPointer(pStartNode));
    }
    CONTRACTL_END;

    // This method performs a modified DFS. We are not looking for any cycle but for a cycle
    // which has at least one expanding edge. Therefore we:
    // 1) Pass aroung the fExpanded flag to indicate that we've seen an expanding edge.
    // 2) Explicitly check for returning to the starting point rather an arbitrary visited node.

    // Did we just find the cycle?
    if (fExpanded && pCurrentNode == pStartNode)
        return TRUE;

    // Have we been here before or is this a dead end?
    if (pCurrentNode->IsVisited() || pCurrentNode->GetEdges()->GetCount() == 0)
        return FALSE;

    pCurrentNode->SetVisited();

    ArrayList::Iterator iter = pCurrentNode->GetEdges()->Iterate();
    while (iter.Next())
    {
        ULONG_PTR edge = (ULONG_PTR)iter.GetElement();

        BOOL fExpanding = (edge & Node::EDGE_EXPANDING_FLAG);

        TypeVarTypeDesc *pToVar = (TypeVarTypeDesc *)(edge & ~Node::EDGE_EXPANDING_FLAG);
        unsigned int dwIndex = pToVar->GetIndex();

        Node *pNode = NULL;
        RecursionGraph *pGraph = this;

        // Find the destination node.
        do
        {
            if (pGraph->m_pNodes != NULL &&
                dwIndex < pGraph->m_thOwner.GetNumGenericArgs() &&
                pGraph->m_pNodes[dwIndex].GetSourceVar() == pToVar)
            {
                pNode = &pGraph->m_pNodes[dwIndex];
                break;
            }
            pGraph = pGraph->m_pPrev;
        }
        while (pGraph != NULL);

        if (pNode != NULL)
        {
            // The new path is expanding if it was expanding already or if the edge we follow is expanding.
            if (HasExpandingCycle(pNode, pStartNode, fExpanded || fExpanding))
                return TRUE;
        }
    }

    pCurrentNode->ClearVisited();

    return FALSE;
}

} // namespace Generics

#endif // !DACCESS_COMPILE

namespace Generics
{

/*
 * GetExactInstantiationsOfMethodAndItsClassFromCallInformation
 *
 * This routine takes in the various pieces of information of a call site to managed code
 * and returns the exact instatiations for the method and the class on which the method is defined.
 *
 * Parameters:
 *    pRepMethod - A MethodDesc to the representative instantiation method.
 *    pThis - The OBJECTREF that is being passed to pRepMethod.
 *    pParamTypeArg - The extra argument passed to pRepMethod when pRepMethod is either
 *       RequiresInstMethodTableArg() or RequiresInstMethodDescArg().
 *    pSpecificClass - A pointer to a TypeHandle for storing the exact instantiation
 *       of the class on which pRepMethod is defined, based on the call information
 *    pSpecificMethod - A pointer to a MethodDesc* for storing the exact instantiation
 *       of pRepMethod, based on the call information
 *
 * Returns:
 *    TRUE if successful.
 *    FALSE if could not get the exact TypeHandle & MethodDesc requested.  In this case,
 *      the SpecificClass may be correct, iff the class is not a generic class.
 *
 */
BOOL GetExactInstantiationsOfMethodAndItsClassFromCallInformation(
    /* in */  MethodDesc *pRepMethod,
    /* in */  OBJECTREF pThis,
    /* in */  PTR_VOID pParamTypeArg,
    /* out*/  TypeHandle *pSpecificClass,
    /* out*/  MethodDesc** pSpecificMethod
    )
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        CANNOT_TAKE_LOCK;
        PRECONDITION(CheckPointer(pRepMethod));
        SUPPORTS_DAC;
    }
    CONTRACTL_END;

    PTR_VOID pExactGenericArgsToken = NULL;

    if (pRepMethod->AcquiresInstMethodTableFromThis())
    {
        if (pThis != NULL)
        {
            // We could be missing the memory from a dump, or the target could have simply been corrupted.
            ALLOW_DATATARGET_MISSING_MEMORY(
                pExactGenericArgsToken = dac_cast<PTR_VOID>(pThis->GetMethodTable());
            );
        }
    }
    else
    {
        pExactGenericArgsToken = pParamTypeArg;
    }

    return GetExactInstantiationsOfMethodAndItsClassFromCallInformation(pRepMethod, pExactGenericArgsToken,
        pSpecificClass, pSpecificMethod);
}

BOOL GetExactInstantiationsOfMethodAndItsClassFromCallInformation(
    /* in */  MethodDesc *pRepMethod,
    /* in */  PTR_VOID pExactGenericArgsToken,
    /* out*/  TypeHandle *pSpecificClass,
    /* out*/  MethodDesc** pSpecificMethod
    )
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        CANNOT_TAKE_LOCK;
        PRECONDITION(CheckPointer(pRepMethod));
        SUPPORTS_DAC;
    }
    CONTRACTL_END;

    //
    // Start with some decent default values.
    //
    MethodDesc * pMD = pRepMethod;
    MethodTable * pMT = pRepMethod->GetMethodTable();

    *pSpecificMethod = pMD;
    *pSpecificClass = pMT;

    if (!pRepMethod->IsSharedByGenericInstantiations())
    {
        return TRUE;
    }

    if (pExactGenericArgsToken == NULL)
    {
        return FALSE;
    }

    BOOL retVal = FALSE;

    // The following target memory reads will not necessarily succeed against dumps, and will throw on failure.
    EX_TRY_ALLOW_DATATARGET_MISSING_MEMORY
    {
        if (pRepMethod->RequiresInstMethodTableArg())
        {
            pMT = dac_cast<PTR_MethodTable>(pExactGenericArgsToken);
            retVal = TRUE;
        }
        else if (pRepMethod->RequiresInstMethodDescArg())
        {
            pMD = dac_cast<PTR_MethodDesc>(pExactGenericArgsToken);
            pMT = pMD->GetMethodTable();
            retVal = TRUE;
        }
        else if (pRepMethod->AcquiresInstMethodTableFromThis())
        {
            // The exact token might actually be a child class of the class containing
            // the specified function so walk up the parent chain to make sure we return
            // an exact instantiation of the CORRECT parent class.
            pMT = pMD->GetExactDeclaringType(dac_cast<PTR_MethodTable>(pExactGenericArgsToken));
            retVal = pMT != NULL ? TRUE : FALSE;
        }
        else
        {
            _ASSERTE(!"Should not happen.");
        }
    }
    EX_END_CATCH_ALLOW_DATATARGET_MISSING_MEMORY

    *pSpecificMethod = pMD;
    *pSpecificClass = pMT;

    return retVal;
}

} // namespace Generics;

