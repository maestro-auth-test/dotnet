// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "common.h"
#include "assemblybindercommon.hpp"
#include "defaultassemblybinder.h"

using namespace BINDER_SPACE;

//=============================================================================
// Helper functions
//-----------------------------------------------------------------------------

HRESULT DefaultAssemblyBinder::BindAssemblyByNameWorker(BINDER_SPACE::AssemblyName *pAssemblyName,
                                                       BINDER_SPACE::Assembly **ppCoreCLRFoundAssembly,
                                                       bool excludeAppPaths)
{
    VALIDATE_ARG_RET(pAssemblyName != nullptr && ppCoreCLRFoundAssembly != nullptr);
    HRESULT hr = S_OK;

#ifdef _DEBUG
    // CoreLib should be bound using BindToSystem
    _ASSERTE(!pAssemblyName->IsCoreLib());
#endif

    hr = AssemblyBinderCommon::BindAssembly(this,
                                            pAssemblyName,
                                            excludeAppPaths,
                                            ppCoreCLRFoundAssembly);
    if (!FAILED(hr))
    {
        (*ppCoreCLRFoundAssembly)->SetBinder(this);
    }

    return hr;
}

// ============================================================================
// DefaultAssemblyBinder implementation
// ============================================================================
HRESULT DefaultAssemblyBinder::BindUsingAssemblyName(BINDER_SPACE::AssemblyName *pAssemblyName,
                                                     BINDER_SPACE::Assembly **ppAssembly)
{
    HRESULT hr = S_OK;
    VALIDATE_ARG_RET(pAssemblyName != nullptr && ppAssembly != nullptr);

    *ppAssembly = nullptr;

    ReleaseHolder<BINDER_SPACE::Assembly> pCoreCLRFoundAssembly;

    hr = BindAssemblyByNameWorker(pAssemblyName, &pCoreCLRFoundAssembly, false /* excludeAppPaths */);

#if !defined(DACCESS_COMPILE)
    if ((hr == HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND)) ||
        (hr == FUSION_E_APP_DOMAIN_LOCKED) || (hr == FUSION_E_REF_DEF_MISMATCH))
    {
        // If we are here, one of the following is possible:
        //
        // 1) The assembly has not been found in the current binder's application context (i.e. it has not already been loaded), OR
        // 2) An assembly with the same simple name was already loaded in the context of the current binder but we ran into a Ref/Def
        //    mismatch (either due to version difference or strong-name difference).
        //
        // Attempt to resolve the assembly via managed ALC instance. This can either fail the bind or return reference to an existing
        // assembly that has been loaded
        INT_PTR pAssemblyLoadContext = GetAssemblyLoadContext();
        if (pAssemblyLoadContext == (INT_PTR)NULL)
        {
            // For satellite assemblies, the managed ALC has additional resolution logic (defined by the runtime) which
            // should be run even if the managed default ALC has not yet been used. (For non-satellite assemblies, any
            // additional logic comes through a user-defined event handler which would have initialized the managed ALC,
            // so if the managed ALC is not set yet, there is no additional logic to run)
            if (!pAssemblyName->IsNeutralCulture())
            {
                // Make sure the managed default ALC is initialized.
                GCX_COOP();
                PREPARE_NONVIRTUAL_CALLSITE(METHOD__ASSEMBLYLOADCONTEXT__INITIALIZE_DEFAULT_CONTEXT);
                DECLARE_ARGHOLDER_ARRAY(args, 0);
                CALL_MANAGED_METHOD_NORET(args)

                pAssemblyLoadContext = GetAssemblyLoadContext();
                _ASSERTE(pAssemblyLoadContext != (INT_PTR)NULL);
            }
        }

        if (pAssemblyLoadContext != (INT_PTR)NULL)
        {
            hr = AssemblyBinderCommon::BindUsingHostAssemblyResolver(pAssemblyLoadContext, pAssemblyName,
                                                                     NULL, this, &pCoreCLRFoundAssembly);
            if (SUCCEEDED(hr))
            {
                // We maybe returned an assembly that was bound to a different AssemblyLoadContext instance.
                // In such a case, we will not overwrite the binding context (which would be wrong since it would not
                // be present in the cache of the current binding context).
                if (pCoreCLRFoundAssembly->GetBinder() == NULL)
                {
                    pCoreCLRFoundAssembly->SetBinder(this);
                }
            }
        }
    }
#endif // !defined(DACCESS_COMPILE)

    IF_FAIL_GO(hr);

    *ppAssembly = pCoreCLRFoundAssembly.Extract();

Exit:;

    return hr;
}

#if !defined(DACCESS_COMPILE)
HRESULT DefaultAssemblyBinder::BindUsingPEImage( /* in */ PEImage *pPEImage,
                                                 /* in */ bool excludeAppPaths,
                                                 /* [retval][out] */ BINDER_SPACE::Assembly **ppAssembly)
{
    HRESULT hr = S_OK;

    EX_TRY
    {
        ReleaseHolder<BINDER_SPACE::Assembly> pCoreCLRFoundAssembly;
        ReleaseHolder<BINDER_SPACE::AssemblyName> pAssemblyName;

        // Using the information we just got, initialize the assemblyname
        SAFE_NEW(pAssemblyName, AssemblyName);
        IF_FAIL_GO(pAssemblyName->Init(pPEImage));

        // Validate architecture
        if (!AssemblyBinderCommon::IsValidArchitecture(pAssemblyName->GetArchitecture()))
        {
            IF_FAIL_GO(CLR_E_BIND_ARCHITECTURE_MISMATCH);
        }

        // Easy out for CoreLib
        if (pAssemblyName->IsCoreLib())
        {
            IF_FAIL_GO(HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND));
        }

        {
            // Ensure we are not being asked to bind to a TPA assembly
            //
            const SString& simpleName = pAssemblyName->GetSimpleName();
            SimpleNameToFileNameMap* tpaMap = GetAppContext()->GetTpaList();
            if (tpaMap->LookupPtr(simpleName.GetUnicode()) != NULL)
            {
                // The simple name of the assembly being requested to be bound was found in the TPA list.
                // Now, perform the actual bind to see if the assembly was really in the TPA assembly list or not.
                hr = BindAssemblyByNameWorker(pAssemblyName, &pCoreCLRFoundAssembly, true /* excludeAppPaths */);
                if (SUCCEEDED(hr))
                {
                    if (pCoreCLRFoundAssembly->GetIsInTPA())
                    {
                        *ppAssembly = pCoreCLRFoundAssembly.Extract();
                        goto Exit;
                    }
                }
            }
        }

        hr = AssemblyBinderCommon::BindUsingPEImage(this, pAssemblyName, pPEImage, excludeAppPaths, &pCoreCLRFoundAssembly);
        if (hr == S_OK)
        {
            _ASSERTE(pCoreCLRFoundAssembly != NULL);
            pCoreCLRFoundAssembly->SetBinder(this);
            *ppAssembly = pCoreCLRFoundAssembly.Extract();
        }
Exit:;
    }
    EX_CATCH_HRESULT(hr);

    return hr;
}
#endif // !defined(DACCESS_COMPILE)

HRESULT DefaultAssemblyBinder::SetupBindingPaths(SString  &sTrustedPlatformAssemblies,
                                                SString  &sPlatformResourceRoots,
                                                SString  &sAppPaths)
{
    HRESULT hr = S_OK;

    EX_TRY
    {
        hr = GetAppContext()->SetupBindingPaths(sTrustedPlatformAssemblies, sPlatformResourceRoots, sAppPaths, TRUE /* fAcquireLock */);
    }
    EX_CATCH_HRESULT(hr);
    return hr;
}

HRESULT DefaultAssemblyBinder::BindToSystem(BINDER_SPACE::Assembly** ppSystemAssembly)
{
    HRESULT hr = S_OK;
    _ASSERTE(ppSystemAssembly != NULL);

    EX_TRY
    {
        ReleaseHolder<BINDER_SPACE::Assembly> pAsm;
        StackSString systemPath(SystemDomain::System()->SystemDirectory());
        hr = AssemblyBinderCommon::BindToSystem(systemPath, &pAsm);
        if (SUCCEEDED(hr))
        {
            _ASSERTE(pAsm != NULL);
            *ppSystemAssembly = pAsm.Extract();
            (*ppSystemAssembly)->SetBinder(this);
        }

    }
    EX_CATCH_HRESULT(hr);

    return hr;
}

