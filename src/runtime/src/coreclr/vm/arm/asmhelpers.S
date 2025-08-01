// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "unixasmmacros.inc"
#include "asmconstants.h"

.syntax unified
.thumb

// LPVOID __stdcall GetCurrentIP(void)//
    LEAF_ENTRY GetCurrentIP, _TEXT
        mov     r0, lr
        bx      lr
    LEAF_END GetCurrentIP, _TEXT

// LPVOID __stdcall GetCurrentSP(void)//
    LEAF_ENTRY GetCurrentSP, _TEXT
        mov     r0, sp
        bx      lr
    LEAF_END GetCurrentSP, _TEXT

//-----------------------------------------------------------------------------
// This helper routine enregisters the appropriate arguments and makes the
// actual call.
//-----------------------------------------------------------------------------
//void CallDescrWorkerInternal(CallDescrData * pCallDescrData)//
        NESTED_ENTRY CallDescrWorkerInternal,_TEXT,NoHandler
        PROLOG_PUSH         "{r4,r5,r7,lr}"
        PROLOG_STACK_SAVE_OFFSET   r7, #8

        mov     r5,r0 // save pCallDescrData in r5

        ldr     r1, [r5,#CallDescrData__numStackSlots]
        cbz     r1, LOCAL_LABEL(Ldonestack)

        // Add frame padding to ensure frame size is a multiple of 8 (a requirement of the OS ABI).
        // We push four registers (above) and numStackSlots arguments (below). If this comes to an odd number
        // of slots we must pad with another. This simplifies to "if the low bit of numStackSlots is set,
        // extend the stack another four bytes".
        lsls    r2, r1, #2
        and     r3, r2, #4
        sub     sp, sp, r3

        // This loop copies numStackSlots words
        // from [pSrcEnd-4,pSrcEnd-8,...] to [sp-4,sp-8,...]
        ldr     r0, [r5,#CallDescrData__pSrc]
        add     r0,r0,r2
LOCAL_LABEL(Lstackloop):
        ldr     r2, [r0,#-4]!
        str     r2, [sp,#-4]!
        subs    r1, r1, #1
        bne     LOCAL_LABEL(Lstackloop)
LOCAL_LABEL(Ldonestack):

        // If FP arguments are supplied in registers (r3 != NULL) then initialize all of them from the pointer
        // given in r3. Do not use "it" since it faults in floating point even when the instruction is not executed.
        ldr     r3, [r5,#CallDescrData__pFloatArgumentRegisters]
        cbz     r3, LOCAL_LABEL(LNoFloatingPoint)
        vldm    r3, {s0-s15}
LOCAL_LABEL(LNoFloatingPoint):

        // Copy [pArgumentRegisters, ..., pArgumentRegisters + 12]
        // into r0, ..., r3

        ldr     r4, [r5,#CallDescrData__pArgumentRegisters]
        ldm     r4, {r0-r3}

        CHECK_STACK_ALIGNMENT

        // call pTarget
        // Note that remoting expect target in r4.
        ldr     r4, [r5,#CallDescrData__pTarget]
        blx     r4
LOCAL_LABEL(CallDescrWorkerInternalReturnAddress):

        ldr     r3, [r5,#CallDescrData__fpReturnSize]

        // Save FP return value if appropriate
        cbz     r3, LOCAL_LABEL(LFloatingPointReturnDone)

        // Float return case
        // Do not use "it" since it faults in floating point even when the instruction is not executed.
        cmp     r3, #4
        bne     LOCAL_LABEL(LNoFloatReturn)
        vmov    r0, s0
        b       LOCAL_LABEL(LFloatingPointReturnDone)
LOCAL_LABEL(LNoFloatReturn):

        // Double return case
        // Do not use "it" since it faults in floating point even when the instruction is not executed.
        cmp     r3, #8
        bne     LOCAL_LABEL(LNoDoubleReturn)
        vmov    r0, r1, s0, s1
        b       LOCAL_LABEL(LFloatingPointReturnDone)
LOCAL_LABEL(LNoDoubleReturn):

        add     r2, r5, #CallDescrData__returnValue

        cmp     r3, #16
        bne     LOCAL_LABEL(LNoFloatHFAReturn)
        vstm    r2, {s0-s3}
        b       LOCAL_LABEL(LReturnDone)
LOCAL_LABEL(LNoFloatHFAReturn):

        cmp     r3, #32
        bne     LOCAL_LABEL(LNoDoubleHFAReturn)
        vstm    r2, {d0-d3}
        b       LOCAL_LABEL(LReturnDone)
LOCAL_LABEL(LNoDoubleHFAReturn):

        EMIT_BREAKPOINT // Unreachable

LOCAL_LABEL(LFloatingPointReturnDone):

        // Save return value into retbuf
        str     r0, [r5, #(CallDescrData__returnValue + 0)]
        str     r1, [r5, #(CallDescrData__returnValue + 4)]

LOCAL_LABEL(LReturnDone):

#ifdef _DEBUG
        // trash the floating point registers to ensure that the HFA return values
        // won't survive by accident
        vldm    sp, {d0-d3}
#endif

        EPILOG_STACK_RESTORE_OFFSET   r7, #8
        EPILOG_POP              "{r4,r5,r7,pc}"

CallDescrWorkerInternalReturnAddressOffset:
    .global CallDescrWorkerInternalReturnAddressOffset
    .word LOCAL_LABEL(CallDescrWorkerInternalReturnAddress) - C_FUNC(CallDescrWorkerInternal)

        NESTED_END CallDescrWorkerInternal,_TEXT

// ------------------------------------------------------------------

//
// r12 = UMEntryThunkData*
//
        NESTED_ENTRY TheUMEntryPrestub,_TEXT,NoHandler

        PROLOG_PUSH "{r0-r4,r7,r8,lr}" // add r8 to make stack aligned by 8B
        PROLOG_STACK_SAVE_OFFSET r7, #20
        PROLOG_VPUSH {d0-d7}

        CHECK_STACK_ALIGNMENT

        mov     r0, r12
        bl      C_FUNC(TheUMEntryPrestubWorker)

        // Record real target address in r12.
        mov     r12, r0

        // Epilog
        EPILOG_VPOP {d0-d7}
        EPILOG_POP "{r0-r4,r7,r8,lr}"
        bx r12

        NESTED_END TheUMEntryPrestub,_TEXT

// ------------------------------------------------------------------

        NESTED_ENTRY ThePreStub, _TEXT, NoHandler

        PROLOG_WITH_TRANSITION_BLOCK

        add         r0, sp, #__PWTB_TransitionBlock // pTransitionBlock
        mov         r1, r12                         // pMethodDesc

        bl          C_FUNC(PreStubWorker)

        mov         r12, r0

        EPILOG_WITH_TRANSITION_BLOCK_TAILCALL
        bx r12

        NESTED_END ThePreStub, _TEXT

// ------------------------------------------------------------------
// This method does nothing. It's just a fixed function for the debugger to put a breakpoint on.
        LEAF_ENTRY ThePreStubPatch, _TEXT
        nop
ThePreStubPatchLabel:
        .global ThePreStubPatchLabel
        bx      lr
        LEAF_END ThePreStubPatch, _TEXT

// ------------------------------------------------------------------
// The call in PInvokeImportPrecode points to this function.
        NESTED_ENTRY PInvokeImportThunk, _TEXT, NoHandler

        PROLOG_PUSH "{r0-r4,r7,r8,lr}"                  // Spill general argument registers, return address and
        PROLOG_STACK_SAVE_OFFSET r7, #20
                                                        // arbitrary register to keep stack aligned
        PROLOG_VPUSH {d0-d7}                                   // Spill floating point argument registers

        CHECK_STACK_ALIGNMENT

        mov     r0, r12
        bl      C_FUNC(PInvokeImportWorker)
        mov     r12, r0

        EPILOG_VPOP {d0-d7}
        EPILOG_POP "{r0-r4,r7,r8,lr}"

        // If we got back from PInvokeImportWorker, the MD has been successfully
        // linked. Proceed to execute the original DLL call.
        bx r12

        NESTED_END PInvokeImportThunk, _TEXT

// ------------------------------------------------------------------
// void ResolveWorkerAsmStub(r0, r1, r2, r3, r4:IndirectionCellAndFlags, r12:DispatchToken)
//
// The stub dispatch thunk which transfers control to VSD_ResolveWorker.
        NESTED_ENTRY ResolveWorkerAsmStub, _TEXT, NoHandler

        PROLOG_WITH_TRANSITION_BLOCK

        add         r0, sp, #__PWTB_TransitionBlock // pTransitionBlock
        mov         r2, r12                         // token

        // indirection cell in r4 - should be consistent with REG_ARM_STUB_SPECIAL
        bic         r1, r4, #3          // indirection cell
        and         r3, r4, #3          // flags

        bl          C_FUNC(VSD_ResolveWorker)

        mov         r12, r0

        EPILOG_WITH_TRANSITION_BLOCK_TAILCALL
        bx r12

        NESTED_END ResolveWorkerAsmStub, _TEXT

// ------------------------------------------------------------------
// void ResolveWorkerChainLookupAsmStub(r0, r1, r2, r3, r4:IndirectionCellAndFlags, r12:DispatchToken)
        NESTED_ENTRY ResolveWorkerChainLookupAsmStub, _TEXT, NoHandler

        // ARMSTUB TODO: implement chained lookup
        b           C_FUNC(ResolveWorkerAsmStub)

        NESTED_END ResolveWorkerChainLookupAsmStub, _TEXT

#ifdef PROFILING_SUPPORTED

//
// EXTERN_C void JIT_ProfilerEnterLeaveTailcallStub(UINT_PTR ProfilerHandle)
//
LEAF_ENTRY JIT_ProfilerEnterLeaveTailcallStub, _TEXT
    bx lr
LEAF_END JIT_ProfilerEnterLeaveTailcallStub, _TEXT

// size of profiler data structure plus alignment padding
#define PADDED_SIZEOF__PROFILE_PLATFORM_SPECIFIC_DATA  SIZEOF__PROFILE_PLATFORM_SPECIFIC_DATA+4

// typedef struct _PROFILE_PLATFORM_SPECIFIC_DATA
// {
//     UINT32      r0;         // Keep r0 & r1 contiguous to make returning 64-bit results easier
//     UINT32      r1;
//     void       *R11;
//     void       *Pc;
//     union                   // Float arg registers as 32-bit (s0-s15) and 64-bit (d0-d7)
//     {
//         UINT32  s[16];
//         UINT64  d[8];
//     };
//     FunctionID  functionId;
//     void       *probeSp;    // stack pointer of managed function
//     void       *profiledSp; // location of arguments on stack
//     LPVOID      hiddenArg;
//     UINT32      flags;
// } PROFILE_PLATFORM_SPECIFIC_DATA, *PPROFILE_PLATFORM_SPECIFIC_DATA;

.macro GenerateProfileHelper helper, flags
NESTED_ENTRY \helper\()Naked, _TEXT, NoHandler
    PROLOG_PUSH "{r0,r3,r9,r12}"

    // for the 5 arguments that do not need popped plus 4 bytes of alignment
    alloc_stack 6*4

    // push fp regs
    vpush.64    { d0 - d7 }

    // next three fields pc, r11, r1
    push        { r1, r11, lr}

    // return value is in r2 instead of r0 because functionID is passed in r0
    push        { r2 }

    CHECK_STACK_ALIGNMENT

    // set the other args, starting with functionID
    str         r0, [sp, PROFILE_PLATFORM_SPECIFIC_DATA__functionId]

    // probeSp is the original sp when this stub was called
    add         r2, sp, PADDED_SIZEOF__PROFILE_PLATFORM_SPECIFIC_DATA+20
    str         r2, [sp, PROFILE_PLATFORM_SPECIFIC_DATA__probeSp]

    // get the address of the arguments from the frame pointer, store in profiledSp
    add         r2, r11, #8
    str         r2, [sp, PROFILE_PLATFORM_SPECIFIC_DATA__profiledSp]

    // clear hiddenArg
    movw        r2, #0
    str         r2, [sp, PROFILE_PLATFORM_SPECIFIC_DATA__hiddenArg]

    // set the flag to indicate what hook this is
    movw        r2, \flags
    str         r2, [sp, PROFILE_PLATFORM_SPECIFIC_DATA__flags]

    // sp is the address of PROFILE_PLATFORM_SPECIFIC_DATA, then call to C++
    mov         r1, sp
    bl          C_FUNC(\helper)

    // restore all our regs
    pop         { r2 }
    pop         { r1, r11, lr}
    vpop.64     { d0 - d7 }

    free_stack 6*4

    EPILOG_POP "{r0,r3,r9,r12}"

    bx          lr
NESTED_END \helper\()Naked, _TEXT
.endm

GenerateProfileHelper ProfileEnter, PROFILE_ENTER
GenerateProfileHelper ProfileLeave, PROFILE_LEAVE
GenerateProfileHelper ProfileTailcall, PROFILE_TAILCALL

#endif

#if 0
// ------------------------------------------------------------------
// Macro to generate Redirection Stubs
//
// $reason : reason for redirection
//                     Eg. GCThreadControl
// NOTE: If you edit this macro, make sure you update GetCONTEXTFromRedirectedStubStackFrame.
// This function is used by both the personality routine and the debugger to retrieve the original CONTEXT.
        .macro GenerateRedirectedHandledJITCaseStub reason

        NESTED_ENTRY RedirectedHandledJITCaseFor\reason\()_Stub, _TEXT, NoHandler

        PROLOG_PUSH "{r7,lr}"   // return address
        PROLOG_STACK_SAVE r7
        alloc_stack 4           // stack slot to save the CONTEXT *

        //REDIRECTSTUB_SP_OFFSET_CONTEXT is defined in asmconstants.h
        //If CONTEXT is not saved at 0 offset from SP it must be changed as well.
        //ASSERT REDIRECTSTUB_SP_OFFSET_CONTEXT == 0

        // Runtime check for 8-byte alignment. This check is necessary as this function can be
        // entered before complete execution of the prolog of another function.
        and r0, r7, #4
        sub sp, sp, r0

        // stack must be 8 byte aligned
        CHECK_STACK_ALIGNMENT

        //
        // Save a copy of the redirect CONTEXT*.
        // This is needed for the debugger to unwind the stack.
        //
        bl GetCurrentSavedRedirectContext
        str r0, [r7]

        //
        // Fetch the interrupted pc and save it as our return address.
        //
        ldr r1, [r0, #CONTEXT_Pc]
        str r1, [r7, #8]

        //
        // Call target, which will do whatever we needed to do in the context
        // of the target thread, and will RtlRestoreContext when it is done.
        //
        bl _RedirectedHandledJITCaseFor\reason\()_Stub@Thread@@CAXXZ

        EMIT_BREAKPOINT // Unreachable

// Put a label here to tell the debugger where the end of this function is.
RedirectedHandledJITCaseFor\reason\()_StubEnd:
        .global RedirectedHandledJITCaseFor\reason\()_StubEnd

        NESTED_END RedirectedHandledJITCaseFor\reason\()_Stub, _TEXT

        .endm

// ------------------------------------------------------------------
// Redirection Stub for GC in fully interruptible method
        GenerateRedirectedHandledJITCaseStub GCThreadControl
// ------------------------------------------------------------------
        GenerateRedirectedHandledJITCaseStub DbgThreadControl
// ------------------------------------------------------------------
        GenerateRedirectedHandledJITCaseStub UserSuspend

#ifdef _DEBUG
// ------------------------------------------------------------------
// Redirection Stub for GC Stress
        GenerateRedirectedHandledJITCaseStub GCStress
#endif

#endif

// ------------------------------------------------------------------
// Functions to probe for stack space
// Input reg r4 = amount of stack to probe for
// value of reg r4 is preserved on exit from function
// r12 is trashed
// The below two functions were copied from vctools\crt\crtw32\startup\arm\chkstk.asm

    NESTED_ENTRY checkStack, _TEXT, NoHandler
    subs        r12,sp,r4
    mrc         p15,#0,r4,c13,c0,#2 // get TEB *
    ldr         r4,[r4,#8]          // get Stack limit
    bcc         LOCAL_LABEL(checkStack_neg) // if r12 is less then 0 set it to 0
LOCAL_LABEL(checkStack_label1):
    cmp         r12, r4
    bcc         C_FUNC(stackProbe)  // must probe to extend guardpage if r12 is beyond stackLimit
    sub         r4, sp, r12         // restore value of r4
    bx lr
LOCAL_LABEL(checkStack_neg):
    mov         r12, #0
    b           LOCAL_LABEL(checkStack_label1)
    NESTED_END checkStack, _TEXT

    NESTED_ENTRY stackProbe, _TEXT, NoHandler
    PROLOG_PUSH "{r5,r6}"
    mov         r6, r12
    bfc         r6, #0, #0xc  // align down (4K)
LOCAL_LABEL(stackProbe_loop):
    sub         r4,r4,#0x1000 // dec stack Limit by 4K as page size is 4K
    ldr         r5,[r4]       // try to read ... this should move the guard page
    cmp         r4,r6
    bne         LOCAL_LABEL(stackProbe_loop)
    EPILOG_POP "{r5,r6}"
    sub r4,sp,r12
    bx lr
    NESTED_END stackProbe, _TEXT

//------------------------------------------------
// JIT_RareDisableHelper
//
// The JIT expects this helper to preserve registers used for return values
//
    NESTED_ENTRY JIT_RareDisableHelper, _TEXT, NoHandler

    PROLOG_PUSH "{r0-r1, r7,r8, r11, lr}" // save integer return value
    PROLOG_STACK_SAVE_OFFSET r7, #8
    PROLOG_VPUSH {d0-d3}                  // floating point return value

    CHECK_STACK_ALIGNMENT

    bl          C_FUNC(JIT_RareDisableHelperWorker)

    EPILOG_VPOP {d0-d3}
    EPILOG_POP "{r0-r1, r7,r8, r11, pc}"

    NESTED_END JIT_RareDisableHelper, _TEXT


#define __wbScratch r3
#define pShadow r7

    .macro START_WRITE_BARRIER name
        __\name\()__g_lowest_address_offset = 0xffff
        __\name\()__g_highest_address_offset = 0xffff
        __\name\()__g_ephemeral_low_offset = 0xffff
        __\name\()__g_ephemeral_high_offset = 0xffff
        __\name\()__g_card_table_offset = 0xffff
#ifdef FEATURE_USE_SOFTWARE_WRITE_WATCH_FOR_GC_HEAP
        __\name\()__g_sw_ww_table_offset = 0xffff
#endif
     .endm

    .macro LOAD_GC_GLOBAL name, regName, globalName
\name\()__\globalName\()_offset:
    __\name\()__\globalName\()_offset = (\name\()__\globalName\()_offset - \name)
        movw \regName, #0
        movt \regName, #0
    .endm

    .macro UPDATE_GC_SHADOW name, ptrReg, valReg
        // Todo: implement, debugging helper
    .endm

    .macro UPDATE_CARD_TABLE name, ptrReg, valReg, mp, postGrow, tmpReg

        LOAD_GC_GLOBAL \name, __wbScratch, g_ephemeral_low
        cmp \valReg, __wbScratch
        blo 0f

        .if(\postGrow)
            LOAD_GC_GLOBAL \name, __wbScratch, g_ephemeral_high
            cmp \valReg, __wbScratch
            bhs 0f
        .endif

        LOAD_GC_GLOBAL \name, __wbScratch, g_card_table
        add __wbScratch, __wbScratch, \ptrReg, lsr #10

        .if(\mp)
            ldrb \tmpReg, [__wbScratch]
            cmp \tmpReg, #0xff
            itt ne
            movne \tmpReg, 0xff
            strbne \tmpReg, [__wbScratch]
        .else
            mov \tmpReg, #0xff
            strb \tmpReg, [__wbScratch]
        .endif

0:
    .endm

#ifdef FEATURE_USE_SOFTWARE_WRITE_WATCH_FOR_GC_HEAP
    .macro UPDATE_WRITE_WATCH_TABLE name, ptrReg, mp, tmpReg

        LOAD_GC_GLOBAL \name, __wbScratch, g_write_watch_table
        cbz __wbScratch, 2f
        add __wbScratch, __wbScratch, \ptrReg, lsr #0xc  // SoftwareWriteWatch::AddressToTableByteIndexShift

        .if(\mp)
            ldrb \tmpReg, [__wbScratch]
            cmp \tmpReg, #0xff
            itt ne
            movne \tmpReg, 0xff
            strbne \tmpReg, [__wbScratch]
        .else
            mov \tmpReg, #0xff
            strb \tmpReg, [__wbScratch]
        .endif

2:
    .endm
#endif

    .macro CHECK_GC_HEAP_RANGE name, ptrReg, label
        LOAD_GC_GLOBAL \name, __wbScratch, g_lowest_address
        cmp \ptrReg, __wbScratch
        blo \label
        LOAD_GC_GLOBAL \name, __wbScratch, g_highest_address
        cmp \ptrReg, __wbScratch
        bhs \label
    .endm

    .macro JIT_WRITEBARRIER name, mp, post
    LEAF_ENTRY \name, _TEXT
        START_WRITE_BARRIER \name
        .if(\mp)
            dmb
        .endif

        str r1, [r0]
        UPDATE_GC_SHADOW \name, r0, r1
#ifdef FEATURE_USE_SOFTWARE_WRITE_WATCH_FOR_GC_HEAP
        UPDATE_WRITE_WATCH_TABLE \name, r0, \mp, r12
#endif
        UPDATE_CARD_TABLE \name, r0, r1, \mp, \post, r0
        bx lr
    LEAF_END_MARKED \name, _TEXT
    .endm

    .macro JIT_CHECKEDWRITEBARRIER_SP name, post
    LEAF_ENTRY \name, _TEXT
        START_WRITE_BARRIER \name
        str r1, [r0]
        CHECK_GC_HEAP_RANGE \name, r0, 1f
        UPDATE_GC_SHADOW \name, r0, r1
#ifdef FEATURE_USE_SOFTWARE_WRITE_WATCH_FOR_GC_HEAP
        UPDATE_WRITE_WATCH_TABLE \name, r0, 0, r12
#endif
        UPDATE_CARD_TABLE \name, r0, r1, 0, \post, r0
1:
        bx lr
    LEAF_END_MARKED \name, _TEXT
    .endm

    .macro JIT_CHECKEDWRITEBARRIER_MP name, post
    LEAF_ENTRY \name, _TEXT
        START_WRITE_BARRIER \name
        dmb
        str r1, [r0]
        CHECK_GC_HEAP_RANGE \name, r0, 1f
        UPDATE_GC_SHADOW \name, r0, r1
#ifdef FEATURE_USE_SOFTWARE_WRITE_WATCH_FOR_GC_HEAP
        UPDATE_WRITE_WATCH_TABLE \name, r0, 1, r12
#endif
        UPDATE_CARD_TABLE \name, r0, r1, 1, \post, r0
        bx lr
1:
        str r1, [r0]
        bx lr
    LEAF_END_MARKED \name, _TEXT
    .endm

    .macro JIT_BYREFWRITEBARRIER name, mp, post
    LEAF_ENTRY \name, _TEXT
        START_WRITE_BARRIER \name
        .if(\mp)
            dmb
        .endif

        ldr r2, [r1]
        str r2, [r0]
        CHECK_GC_HEAP_RANGE \name, r0, 1f
        UPDATE_GC_SHADOW \name, r0, r2
#ifdef FEATURE_USE_SOFTWARE_WRITE_WATCH_FOR_GC_HEAP
        UPDATE_WRITE_WATCH_TABLE \name, r0, \mp, r12
#endif
        UPDATE_CARD_TABLE \name, r0, r2, \mp, \post, r2
1:
        add r0, #4
        add r1, #4
        bx lr
    LEAF_END_MARKED \name, _TEXT
    .endm

    .macro JIT_WRITEBARRIER_DESCRIPTOR name
1:
        .word \name-1b
        .word \name\()_End-1b
        .word __\name\()__g_lowest_address_offset
        .word __\name\()__g_highest_address_offset
        .word __\name\()__g_ephemeral_low_offset
        .word __\name\()__g_ephemeral_high_offset
        .word __\name\()__g_card_table_offset
#ifdef FEATURE_USE_SOFTWARE_WRITE_WATCH_FOR_GC_HEAP
        .word __\name\()__g_write_watch_table_offset
#endif
    .endm

    // There 4 versions of each write barriers. A 2x2 combination of multi-proc/single-proc and pre/post grow version
    JIT_WRITEBARRIER JIT_WriteBarrier_SP_Pre,  0, 0
    JIT_WRITEBARRIER JIT_WriteBarrier_SP_Post, 0, 1
    JIT_WRITEBARRIER JIT_WriteBarrier_MP_Pre,  1, 0
    JIT_WRITEBARRIER JIT_WriteBarrier_MP_Post, 1, 1

    JIT_CHECKEDWRITEBARRIER_SP JIT_CheckedWriteBarrier_SP_Pre,  0
    JIT_CHECKEDWRITEBARRIER_SP JIT_CheckedWriteBarrier_SP_Post, 1
    JIT_CHECKEDWRITEBARRIER_MP JIT_CheckedWriteBarrier_MP_Pre,  0
    JIT_CHECKEDWRITEBARRIER_MP JIT_CheckedWriteBarrier_MP_Post, 1

    JIT_BYREFWRITEBARRIER JIT_ByRefWriteBarrier_SP_Pre,  0, 0
    JIT_BYREFWRITEBARRIER JIT_ByRefWriteBarrier_SP_Post, 0, 1
    JIT_BYREFWRITEBARRIER JIT_ByRefWriteBarrier_MP_Pre,  1, 0
    JIT_BYREFWRITEBARRIER JIT_ByRefWriteBarrier_MP_Post, 1, 1

//    .section .clrwb, "d"
g_rgWriteBarrierDescriptors:

    JIT_WRITEBARRIER_DESCRIPTOR JIT_WriteBarrier_SP_Pre
    JIT_WRITEBARRIER_DESCRIPTOR JIT_WriteBarrier_SP_Post
    JIT_WRITEBARRIER_DESCRIPTOR JIT_WriteBarrier_MP_Pre
    JIT_WRITEBARRIER_DESCRIPTOR JIT_WriteBarrier_MP_Post

    JIT_WRITEBARRIER_DESCRIPTOR JIT_CheckedWriteBarrier_SP_Pre
    JIT_WRITEBARRIER_DESCRIPTOR JIT_CheckedWriteBarrier_SP_Post
    JIT_WRITEBARRIER_DESCRIPTOR JIT_CheckedWriteBarrier_MP_Pre
    JIT_WRITEBARRIER_DESCRIPTOR JIT_CheckedWriteBarrier_MP_Post

    JIT_WRITEBARRIER_DESCRIPTOR JIT_ByRefWriteBarrier_SP_Pre
    JIT_WRITEBARRIER_DESCRIPTOR JIT_ByRefWriteBarrier_SP_Post
    JIT_WRITEBARRIER_DESCRIPTOR JIT_ByRefWriteBarrier_MP_Pre
    JIT_WRITEBARRIER_DESCRIPTOR JIT_ByRefWriteBarrier_MP_Post

    // Sentinel value
    .word 0

//    .text

    .global g_rgWriteBarrierDescriptors

// ------------------------------------------------------------------
// __declspec(naked) void F_CALL_CONV JIT_WriteBarrier_Callable(Object **dst, Object* val)
    LEAF_ENTRY JIT_WriteBarrier_Callable

    // Branch to the write barrier
#if defined(__clang__)
    ldr     r2, =JIT_WriteBarrier_Loc-(1f+4) // or R3? See targetarm.h
1:
    add     r2, pc
#else
    ldr     r2, =JIT_WriteBarrier_Loc
#endif
    ldr     pc, [r2]

    LEAF_END JIT_WriteBarrier_Callable

#ifdef FEATURE_READYTORUN

    NESTED_ENTRY DelayLoad_MethodCall_FakeProlog, _TEXT, NoHandler

    // Match what the lazy thunk has pushed. The actual method arguments will be spilled later.
    PROLOG_PUSH  {r1-r3}

        // This is where execution really starts.
DelayLoad_MethodCall:
    .global DelayLoad_MethodCall

    PROLOG_PUSH  {r0}

    PROLOG_WITH_TRANSITION_BLOCK 0x0, 1, DoNotPushArgRegs

    // Load the helper arguments
    ldr         r5, [sp,#(__PWTB_TransitionBlock+10*4)] // pModule
    ldr         r6, [sp,#(__PWTB_TransitionBlock+11*4)] // sectionIndex
    ldr         r8, [sp,#(__PWTB_TransitionBlock+12*4)] // indirection

    // Spill the actual method arguments
    str         r1, [sp,#(__PWTB_TransitionBlock+10*4)]
    str         r2, [sp,#(__PWTB_TransitionBlock+11*4)]
    str         r3, [sp,#(__PWTB_TransitionBlock+12*4)]

    add         r0, sp, #__PWTB_TransitionBlock // pTransitionBlock

    mov         r1, r8          // pIndirection
    mov         r2, r6          // sectionIndex
    mov         r3, r5          // pModule

    bl          C_FUNC(ExternalMethodFixupWorker)

    // mov the address we patched to in R12 so that we can tail call to it
    mov         r12, r0

    EPILOG_WITH_TRANSITION_BLOCK_TAILCALL

    bx r12

    NESTED_END DelayLoad_MethodCall_FakeProlog, _TEXT


    .macro DynamicHelper frameFlags, suffix

        NESTED_ENTRY DelayLoad_Helper\suffix\()_FakeProlog, _TEXT, NoHandler

        // Match what the lazy thunk has pushed. The actual method arguments will be spilled later.
        PROLOG_PUSH  {r1-r3}

        // This is where execution really starts.
DelayLoad_Helper\suffix:
        .global DelayLoad_Helper\suffix

        PROLOG_PUSH  {r0}

        PROLOG_WITH_TRANSITION_BLOCK 0x4, 0, DoNotPushArgRegs

        // Load the helper arguments
        ldr         r5, [sp,#(__PWTB_TransitionBlock+10*4)] // pModule
        ldr         r6, [sp,#(__PWTB_TransitionBlock+11*4)] // sectionIndex
        ldr         r8, [sp,#(__PWTB_TransitionBlock+12*4)] // indirection

        // Spill the actual method arguments
        str         r1, [sp,#(__PWTB_TransitionBlock+10*4)]
        str         r2, [sp,#(__PWTB_TransitionBlock+11*4)]
        str         r3, [sp,#(__PWTB_TransitionBlock+12*4)]

        add         r0, sp, #__PWTB_TransitionBlock // pTransitionBlock

        mov         r1, r8          // pIndirection
        mov         r2, r6          // sectionIndex
        mov         r3, r5          // pModule

        mov         r4, \frameFlags
        str         r4, [sp,#0]

        bl          C_FUNC(DynamicHelperWorker)

        cbnz        r0, 0f
        ldr         r0, [sp,#(__PWTB_TransitionBlock+9*4)]  // The result is stored in the argument area of the transition block

        EPILOG_WITH_TRANSITION_BLOCK_RETURN

0:
        mov         r12, r0
        EPILOG_WITH_TRANSITION_BLOCK_TAILCALL
        bx   r12

        NESTED_END DelayLoad_Helper\suffix\()_FakeProlog, _TEXT

    .endm

    DynamicHelper DynamicHelperFrameFlags_Default
    DynamicHelper DynamicHelperFrameFlags_ObjectArg, _Obj
    DynamicHelper DynamicHelperFrameFlags_ObjectArg | DynamicHelperFrameFlags_ObjectArg2, _ObjObj

#endif // FEATURE_READYTORUN

#ifdef FEATURE_HIJACK

// ------------------------------------------------------------------
// Hijack function for functions which return a value type
        NESTED_ENTRY OnHijackTripThread, _TEXT, NoHandler
        // saving r1 as well, as it can have partial return value when return is > 32 bits
        PROLOG_PUSH "{r0,r1,r2,r4-r11,lr}"

        PROLOG_VPUSH "{d0-d3}"    // saving as d0-d3 can have the floating point return value

        CHECK_STACK_ALIGNMENT

        add r0, sp, #32
        bl C_FUNC(OnHijackWorker)

        EPILOG_VPOP "{d0-d3}"

        EPILOG_POP "{r0,r1,r2,r4-r11,pc}"
        NESTED_END OnHijackTripThread, _TEXT
#endif

// ------------------------------------------------------------------
// The following helper will access ("probe") a word on each page of the stack
// starting with the page right beneath sp down to the one pointed to by r4.
// The procedure is needed to make sure that the "guard" page is pushed down below the allocated stack frame.
// The call to the helper will be emitted by JIT in the function/funclet prolog when stack frame is larger than an OS page.
// On entry:
//   r4 - points to the lowest address on the stack frame being allocated (i.e. [InitialSp - FrameSize])
//   sp - points to some byte on the last probed page
// On exit:
//   r4 - is preserved
//   r5 - is not preserved
//
// NOTE: this helper will probe at least one page below the one pointed to by sp.
#define PROBE_PAGE_SIZE      4096
#define PROBE_PAGE_SIZE_LOG2 12

        LEAF_ENTRY JIT_StackProbe, _TEXT
        PROLOG_PUSH "{r7}"
        PROLOG_STACK_SAVE r7

        mov r5, sp                         // r5 points to some byte on the last probed page
        bfc r5, #0, #PROBE_PAGE_SIZE_LOG2  // r5 points to the **lowest address** on the last probed page
        mov sp, r5

ProbeLoop:
                                           // Immediate operand for the following instruction can not be greater than 4095.
        sub sp, #(PROBE_PAGE_SIZE - 4)     // sp points to the **fourth** byte on the **next page** to probe
        ldr r5, [sp, #-4]!                 // sp points to the lowest address on the **last probed** page
        cmp sp, r4
        bhi ProbeLoop                      // If (sp > r4), then we need to probe at least one more page.

        EPILOG_STACK_RESTORE r7
        EPILOG_POP "{r7}"
        EPILOG_BRANCH_REG lr
        LEAF_END_MARKED JIT_StackProbe, _TEXT

#ifdef FEATURE_TIERED_COMPILATION

    NESTED_ENTRY OnCallCountThresholdReachedStub, _TEXT, NoHandler
        PROLOG_WITH_TRANSITION_BLOCK

        add     r0, sp, #__PWTB_TransitionBlock // TransitionBlock *
        mov     r1, r12 // stub-identifying token
        bl      C_FUNC(OnCallCountThresholdReached)
        mov     r12, r0

        EPILOG_WITH_TRANSITION_BLOCK_TAILCALL
        EPILOG_BRANCH_REG r12
    NESTED_END OnCallCountThresholdReachedStub, _TEXT

#endif // FEATURE_TIERED_COMPILATION

    LEAF_ENTRY JIT_PollGC, _TEXT
#if defined(__clang__)
        ldr     r2, =g_TrapReturningThreads-(1f+4)
1:
        add     r2, pc
#else
        ldr     r2, =g_TrapReturningThreads
#endif
        ldr     r2, [r2]
        cbnz    r2, LOCAL_LABEL(JIT_PollGCRarePath)
        bx lr
LOCAL_LABEL(JIT_PollGCRarePath):
#if defined(__clang__)
        ldr     r2, =g_pPollGC-(1f+4)
1:
        add     r2, pc
#else
        ldr     r2, =g_pPollGC
#endif
        ldr     r2, [r2]
        EPILOG_BRANCH_REG r2
    LEAF_END JIT_PollGC, _TEXT


//x0 -This pointer
//x1 -ReturnBuffer
LEAF_ENTRY ThisPtrRetBufPrecodeWorker, _TEXT
    ldr  r12, [r12, #ThisPtrRetBufPrecodeData__Target]
    // Use XOR swap technique to set avoid the need to spill to the stack
    eor  r0, r0, r1
    eor  r1, r0, r1
    eor  r0, r0, r1
    EPILOG_BRANCH_REG r12
LEAF_END ThisPtrRetBufPrecodeWorker, _TEXT
