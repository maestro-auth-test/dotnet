// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef TARGET_LOONGARCH64
#error Should only include "cgencpu.h" for LOONGARCH64 builds
#endif

#ifndef __cgencpu_h__
#define __cgencpu_h__

#define INSTRFMT_K64
#include <stublink.h>

#ifndef TARGET_UNIX
#define USE_REDIRECT_FOR_GCSTRESS
#endif // TARGET_UNIX

#define ENUM_CALLEE_SAVED_REGISTERS() \
    CALLEE_SAVED_REGISTER(Fp) \
    CALLEE_SAVED_REGISTER(Ra) \
    CALLEE_SAVED_REGISTER(S0) \
    CALLEE_SAVED_REGISTER(S1) \
    CALLEE_SAVED_REGISTER(S2) \
    CALLEE_SAVED_REGISTER(S3) \
    CALLEE_SAVED_REGISTER(S4) \
    CALLEE_SAVED_REGISTER(S5) \
    CALLEE_SAVED_REGISTER(S6) \
    CALLEE_SAVED_REGISTER(S7) \
    CALLEE_SAVED_REGISTER(S8)

#define ENUM_FP_CALLEE_SAVED_REGISTERS() \
    CALLEE_SAVED_REGISTER(F[24]) \
    CALLEE_SAVED_REGISTER(F[25]) \
    CALLEE_SAVED_REGISTER(F[26]) \
    CALLEE_SAVED_REGISTER(F[27]) \
    CALLEE_SAVED_REGISTER(F[28]) \
    CALLEE_SAVED_REGISTER(F[29]) \
    CALLEE_SAVED_REGISTER(F[30]) \
    CALLEE_SAVED_REGISTER(F[31])

EXTERN_C void getFPReturn(int fpSize, INT64 *pRetVal);
EXTERN_C void setFPReturn(int fpSize, INT64 retVal);


class ComCallMethodDesc;

extern PCODE GetPreStubEntryPoint();

#define COMMETHOD_PREPAD                        24   // # extra bytes to allocate in addition to sizeof(ComCallMethodDesc)
#ifdef FEATURE_COMINTEROP
#define COMMETHOD_CALL_PRESTUB_SIZE             24
#define COMMETHOD_CALL_PRESTUB_ADDRESS_OFFSET   16   // the offset of the call target address inside the prestub
#endif // FEATURE_COMINTEROP

#define STACK_ALIGN_SIZE                        16

#define JUMP_ALLOCATE_SIZE                      40  // # bytes to allocate for a jump instruction
#define BACK_TO_BACK_JUMP_ALLOCATE_SIZE         40  // # bytes to allocate for a back to back jump instruction

#define HAS_PINVOKE_IMPORT_PRECODE              1

#define HAS_FIXUP_PRECODE                       1
#define HAS_FIXUP_PRECODE_CHUNKS                1

// ThisPtrRetBufPrecode one is necessary for closed delegates over static methods with return buffer
#define HAS_THISPTR_RETBUF_PRECODE              1

#define CODE_SIZE_ALIGN                         8
#define CACHE_LINE_SIZE                         64
#define LOG2SLOT                                LOG2_PTRSIZE

#define ENREGISTERED_RETURNTYPE_MAXSIZE         16  // bytes (two FP registers: f0 and f1)
#define ENREGISTERED_RETURNTYPE_INTEGER_MAXSIZE 16  // bytes (two int registers: a0 and a1)
#define ENREGISTERED_PARAMTYPE_MAXSIZE          16  // bytes (max value type size that can be passed by value)

#define CALLDESCR_ARGREGS                       1   // CallDescrWorker has ArgumentRegister parameter
#define CALLDESCR_FPARGREGS                     1   // CallDescrWorker has FloatArgumentRegisters parameter

#define FLOAT_REGISTER_SIZE                     8   // each register in FloatArgumentRegisters is 8 bytes.

// Given a return address retrieved during stackwalk,
// this is the offset by which it should be decremented to arrive at the callsite.
#define STACKWALK_CONTROLPC_ADJUST_OFFSET 4

//**********************************************************************
// Parameter size
//**********************************************************************

inline unsigned StackElemSize(unsigned parmSize, bool isValueType, bool isFloatHfa)
{
    const unsigned stackSlotSize = 8;
    return ALIGN_UP(parmSize, stackSlotSize);
}

//
// JIT HELPERS.
//
// Create alias for optimized implementations of helpers provided on this platform
//
#define JIT_GetDynamicGCStaticBase           JIT_GetDynamicGCStaticBase_SingleAppDomain
#define JIT_GetDynamicNonGCStaticBase        JIT_GetDynamicNonGCStaticBase_SingleAppDomain

//**********************************************************************
// Frames
//**********************************************************************

//--------------------------------------------------------------------
// This represents the callee saved (non-volatile) registers saved as
// of a FramedMethodFrame.
//--------------------------------------------------------------------
typedef DPTR(struct CalleeSavedRegisters) PTR_CalleeSavedRegisters;
struct CalleeSavedRegisters {
    INT64 fp; // frame pointer.
    INT64 ra; // return register
    INT64 s0;
    INT64 s1;
    INT64 s2;
    INT64 s3;
    INT64 s4;
    INT64 s5;
    INT64 s6;
    INT64 s7;
    INT64 s8;
};

//--------------------------------------------------------------------
// This represents the arguments that are stored in volatile registers.
// This should not overlap the CalleeSavedRegisters since those are already
// saved separately and it would be wasteful to save the same register twice.
// If we do use a non-volatile register as an argument, then the ArgIterator
// will probably have to communicate this back to the PromoteCallerStack
// routine to avoid a double promotion.
//--------------------------------------------------------------------
#define NUM_ARGUMENT_REGISTERS 8
typedef DPTR(struct ArgumentRegisters) PTR_ArgumentRegisters;
struct ArgumentRegisters {
    INT64 a[NUM_ARGUMENT_REGISTERS]; // a0 ....a7
};

#define ARGUMENTREGISTERS_SIZE sizeof(ArgumentRegisters)

//--------------------------------------------------------------------
// This represents the floating point argument registers which are saved
// as part of the NegInfo for a FramedMethodFrame. Note that these
// might not be saved by all stubs: typically only those that call into
// C++ helpers will need to preserve the values in these volatile
// registers.
//--------------------------------------------------------------------
#define NUM_FLOAT_ARGUMENT_REGISTERS 8
typedef DPTR(struct FloatArgumentRegisters) PTR_FloatArgumentRegisters;
struct FloatArgumentRegisters {
    double f[NUM_FLOAT_ARGUMENT_REGISTERS]; // fa0-fa7
};

#ifdef PROFILING_SUPPORTED
//**********************************************************************
// Profiling
//**********************************************************************

typedef struct _PROFILE_PLATFORM_SPECIFIC_DATA
{
    void*                  Fp;
    void*                  Pc;
    void*                  probeSp;
    void*                  profiledSp;
    void*                  hiddenArg;
    FunctionID             functionId;
    UINT32                 flags;
    ArgumentRegisters      argumentRegisters;
    FloatArgumentRegisters floatArgumentRegisters;
    // Scratch space to reconstruct struct passed two registers:
    // one float register and one general register. Including the return args.
    BYTE buffer[sizeof(ArgumentRegisters) + sizeof(FloatArgumentRegisters)];
} PROFILE_PLATFORM_SPECIFIC_DATA, *PPROFILE_PLATFORM_SPECIFIC_DATA;

#endif  // PROFILING_SUPPORTED

//**********************************************************************
// Exception handling
//**********************************************************************

inline PCODE GetIP(const T_CONTEXT * context) {
    LIMITED_METHOD_DAC_CONTRACT;
    return context->Pc;
}

inline void SetIP(T_CONTEXT *context, PCODE ip) {
    LIMITED_METHOD_DAC_CONTRACT;
    context->Pc = ip;
}

inline TADDR GetSP(const T_CONTEXT * context) {
    LIMITED_METHOD_DAC_CONTRACT;
    return TADDR(context->Sp);
}

inline TADDR GetRA(const T_CONTEXT * context) {
    LIMITED_METHOD_DAC_CONTRACT;
    return context->Ra;
}

inline void SetRA(T_CONTEXT * context, TADDR ip) {
    LIMITED_METHOD_DAC_CONTRACT;
    context->Ra = ip;
}

inline TADDR GetReg(T_CONTEXT * context, int Regnum)
{
    LIMITED_METHOD_DAC_CONTRACT;
    _ASSERTE((Regnum > 0) && (Regnum < 32));
     return (TADDR)(&context->R0 + Regnum);
}

extern "C" LPVOID __stdcall GetCurrentSP();

inline void SetSP(T_CONTEXT *context, TADDR sp) {
    LIMITED_METHOD_DAC_CONTRACT;
    context->Sp = DWORD64(sp);
}

inline void SetFP(T_CONTEXT *context, TADDR fp) {
    LIMITED_METHOD_DAC_CONTRACT;
    context->Fp = DWORD64(fp);
}

inline TADDR GetFP(const T_CONTEXT * context)
{
    LIMITED_METHOD_DAC_CONTRACT;
    return (TADDR)(context->Fp);
}

inline void SetFirstArgReg(T_CONTEXT *context, TADDR value)
{
    LIMITED_METHOD_DAC_CONTRACT;
    context->A0 = DWORD64(value);
}

inline TADDR GetFirstArgReg(T_CONTEXT *context)
{
    LIMITED_METHOD_DAC_CONTRACT;
    return (TADDR)(context->A0);
}

inline void SetSecondArgReg(T_CONTEXT *context, TADDR value)
{
    LIMITED_METHOD_DAC_CONTRACT;
    context->A1 = DWORD64(value);
}

inline TADDR GetSecondArgReg(T_CONTEXT *context)
{
    LIMITED_METHOD_DAC_CONTRACT;
    return (TADDR)(context->A1);
}

inline TADDR GetMem(PCODE address, SIZE_T size, bool signExtend)
{
    TADDR mem;
    LIMITED_METHOD_DAC_CONTRACT;
    EX_TRY
    {
        switch (size)
        {
        case 4:
            if (signExtend)
                mem = *(int32_t*)address;
            else
                mem = *(uint32_t*)address;
            break;
        case 8:
            mem = *(uint64_t*)address;
            break;
        default:
            UNREACHABLE();
        }
    }
    EX_CATCH
    {
        _ASSERTE(!"Memory read within jitted Code Failed, this should not happen!!!!");
    }
    EX_END_CATCH
    return mem;
}


#ifdef FEATURE_COMINTEROP
void emitCOMStubCall (ComCallMethodDesc *pCOMMethodRX, ComCallMethodDesc *pCOMMethodRW, PCODE target);
#endif // FEATURE_COMINTEROP

inline BOOL ClrFlushInstructionCache(LPCVOID pCodeAddr, size_t sizeOfCode, bool hasCodeExecutedBefore = false)
{
    return FlushInstructionCache(GetCurrentProcess(), pCodeAddr, sizeOfCode);
}

//------------------------------------------------------------------------
inline void emitJump(LPBYTE pBufferRX, LPBYTE pBufferRW, LPVOID target)
{
    LIMITED_METHOD_CONTRACT;
    UINT32* pCode = (UINT32*)pBufferRW;

    // We require 8-byte alignment so the LD instruction is aligned properly
    _ASSERTE(((UINT_PTR)pCode & 7) == 0);

    // pcaddi  $r21,4
    // ld.d  $r21,$r21,0
    // jirl  $r0,$r21,0
    // nop    //padding.

    pCode[0] = 0x18000095; //pcaddi  $r21,4
    pCode[1] = 0x28c002b5; //ld.d  $r21,$r21,0
    pCode[2] = 0x4c0002a0; //jirl  $r0,$r21,0
    pCode[3] = 0x03400000; //padding nop.

    // Ensure that the updated instructions get updated in the I-Cache
    ClrFlushInstructionCache(pBufferRX, 16);

    *((LPVOID *)(pCode + 4)) = target;   // 64-bit target address

}

//------------------------------------------------------------------------
//  Given the same pBuffer that was used by emitJump this method
//  decodes the instructions and returns the jump target
inline PCODE decodeJump(PCODE pCode)
{
    LIMITED_METHOD_CONTRACT;

    TADDR pInstr = PCODEToPINSTR(pCode);

    return *dac_cast<PTR_PCODE>(pInstr + 16);
}

//------------------------------------------------------------------------
inline void emitBackToBackJump(LPBYTE pBufferRX, LPBYTE pBufferRW, LPVOID target)
{
    WRAPPER_NO_CONTRACT;
    emitJump(pBufferRX, pBufferRW, target);
}

//------------------------------------------------------------------------
inline PCODE decodeBackToBackJump(PCODE pBuffer)
{
    WRAPPER_NO_CONTRACT;
    return decodeJump(pBuffer);
}


//----------------------------------------------------------------------

struct IntReg
{
    int reg;
    IntReg(int reg):reg(reg)
    {
        _ASSERTE(0 <= reg && reg < 32);
    }

    operator int () { return reg; }
    operator int () const { return reg; }
    int operator == (IntReg other) { return reg == other.reg; }
    int operator != (IntReg other) { return reg != other.reg; }
    WORD Mask() const { return 1 << reg; }
};

struct VecReg
{
    int reg;
    VecReg(int reg):reg(reg)
    {
        _ASSERTE(0 <= reg && reg < 32);
    }

    operator int() { return reg; }
    int operator == (VecReg other) { return reg == other.reg; }
    int operator != (VecReg other) { return reg != other.reg; }
    WORD Mask() const { return 1 << reg; }
};

const IntReg RegSp  = IntReg(3);
const IntReg RegFp  = IntReg(22);
const IntReg RegRa  = IntReg(1);

#define GetEEFuncEntryPoint(pfn) GFN_TADDR(pfn)

class StubLinkerCPU : public StubLinker
{

private:
    void EmitLoadStoreRegImm(DWORD flags, int regNum, IntReg Rn, int offset, BOOL isVec, int log2Size = 3);
public:

    // BitFlags for EmitLoadStoreReg(Pair)Imm methods
    enum {
        eSTORE = 0x0,
        eLOAD  = 0x1,
    };

    static void Init();
    static bool isValidSimm12(int value) {
        return -( ((int)1) << 11 ) <= value && value < ( ((int)1) << 11 );
    }
    static int emitIns_O_R_R_I(int op, int rd, int rj, int imm) {
        _ASSERTE(isValidSimm12(imm));
        _ASSERTE(!(rd >> 5));
        _ASSERTE(!(rj >> 5));
        _ASSERTE(op < 0x400);
        return ((op & 0x3ff)<<22) | ((rj & 0x1f)<<5) | (rd & 0x1f) | ((imm & 0xfff)<<10);
    }

    void EmitCallManagedMethod(MethodDesc *pMD, BOOL fTailCall);
    void EmitCallLabel(CodeLabel *target, BOOL fTailCall, BOOL fIndirect);

    void EmitShuffleThunk(struct ShuffleEntry *pShuffleEntryArray);

#if defined(FEATURE_SHARE_GENERIC_CODE)
    void EmitComputedInstantiatingMethodStub(MethodDesc* pSharedMD, struct ShuffleEntry *pShuffleEntryArray, void* extraArg);
#endif // FEATURE_SHARE_GENERIC_CODE

    void EmitMovConstant(IntReg Rd, UINT64 constant);
    void EmitJumpRegister(IntReg regTarget);
    void EmitMovReg(IntReg dest, IntReg source);

    void EmitAddImm(IntReg Rd, IntReg Rn, unsigned int value);

    void EmitLoadStoreRegImm(DWORD flags, IntReg Rt, IntReg Rn, int offset=0, int log2Size = 3);
};


// preferred alignment for data
#define DATA_ALIGNMENT 8

struct HijackArgs
{
    DWORD64 Fp; // frame pointer
    union
    {
        DWORD64 Ra;
        size_t ReturnAddress;
    };
    DWORD64 S0, S1, S2, S3, S4, S5, S6, S7, S8;
    union
    {
        struct {
             DWORD64 A0;
             DWORD64 A1;
         };
        size_t ReturnValue[2];
    };
    union
    {
        struct {
             DWORD64 F0;
             DWORD64 F1;
         };
        size_t FPReturnValue[2];
    };
};

EXTERN_C VOID STDCALL PrecodeFixupThunk();

#endif // __cgencpu_h__
