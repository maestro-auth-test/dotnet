// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XX                                                                           XX
XX               Intel hardware intrinsic Code Generator                     XX
XX                                                                           XX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
*/
#include "jitpch.h"
#ifdef _MSC_VER
#pragma hdrstop
#endif

#ifdef FEATURE_HW_INTRINSICS

#include "emit.h"
#include "codegen.h"
#include "sideeffects.h"
#include "lower.h"
#include "gcinfo.h"
#include "gcinfoencoder.h"

//------------------------------------------------------------------------
// assertIsContainableHWIntrinsicOp: Asserts that op is containable by node
//
// Arguments:
//    lowering       - The lowering phase from the compiler
//    containingNode - The HWIntrinsic node that has the contained node
//    containedNode  - The node that is contained
//
static void assertIsContainableHWIntrinsicOp(Lowering*           lowering,
                                             GenTreeHWIntrinsic* containingNode,
                                             GenTree*            containedNode)
{
#if DEBUG
    // The Lowering::IsContainableHWIntrinsicOp call is not quite right, since it follows pre-register allocation
    // logic. However, this check is still important due to the various containment rules that SIMD intrinsics follow.
    //
    // We use isContainable to track the special HWIntrinsic node containment rules (for things like LoadAligned and
    // LoadUnaligned) and we use the supportsRegOptional check to support general-purpose loads (both from stack
    // spillage and for isUsedFromMemory contained nodes, in the case where the register allocator decided to not
    // allocate a register in the first place).

    GenTree* node = containedNode;

    // Now that we are doing full memory containment safety checks, we can't properly check nodes that are not
    // linked into an evaluation tree, like the special nodes we create in genHWIntrinsic.
    // So, just say those are ok.
    //
    if (node->gtNext == nullptr)
    {
        return;
    }

    bool supportsRegOptional = false;
    bool isContainable       = lowering->IsContainableHWIntrinsicOp(containingNode, node, &supportsRegOptional);

    assert(isContainable || supportsRegOptional);
#endif // DEBUG
}

//------------------------------------------------------------------------
// AddEmbRoundingMode: Adds the embedded rounding mode to the insOpts
//
// Arguments:
//    instOptions - The existing insOpts
//    mode        - The embedded rounding mode to add to instOptions
//
// Return Value:
//    The modified insOpts
//
static insOpts AddEmbRoundingMode(insOpts instOptions, int8_t mode)
{
    // The full rounding mode is a bitmask in the shape of:
    // * RC: 2-bit rounding control
    // * RS: 1-bit rounding select
    // * P:  1-bit precision mask
    // *     4-bit reserved
    //
    // The embedded rounding form assumes that P is 1, indicating
    // that floating-point exceptions should not be raised and also
    // assumes that RS is 0, indicating that MXCSR.RC is ignored.
    //
    // Given that the user is specifying a rounding mode and that
    // .NET doesn't support raising IEEE 754 floating-point exceptions,
    // we simplify the handling below to only consider the 2-bits of RC.

    assert((instOptions & INS_OPTS_EVEX_b_MASK) == 0);
    unsigned result = static_cast<unsigned>(instOptions);

    switch (mode & 0x03)
    {
        case 0x01:
        {
            result |= INS_OPTS_EVEX_er_rd;
            break;
        }

        case 0x02:
        {
            result |= INS_OPTS_EVEX_er_ru;
            break;
        }

        case 0x03:
        {
            result |= INS_OPTS_EVEX_er_rz;
            break;
        }

        default:
        {
            break;
        }
    }

    return static_cast<insOpts>(result);
}

//------------------------------------------------------------------------
// AddEmbMaskingMode: Adds the embedded masking mode to the insOpts
//
// Arguments:
//    instOptions   - The existing insOpts
//    maskReg       - The register to use for the embedded mask
//    mergeWithZero - true if the mask merges with zero; otherwise, false
//
// Return Value:
//    The modified insOpts
//
static insOpts AddEmbMaskingMode(insOpts instOptions, regNumber maskReg, bool mergeWithZero)
{
    assert((instOptions & INS_OPTS_EVEX_aaa_MASK) == 0);
    assert((instOptions & INS_OPTS_EVEX_z_MASK) == 0);

    unsigned result = static_cast<unsigned>(instOptions);
    unsigned em_k   = (maskReg - KBASE) << 2;
    unsigned em_z   = mergeWithZero ? INS_OPTS_EVEX_em_zero : 0;

    assert(emitter::isMaskReg(maskReg));
    assert((em_k & INS_OPTS_EVEX_aaa_MASK) == em_k);

    result |= em_k;
    result |= em_z;

    return static_cast<insOpts>(result);
}

//------------------------------------------------------------------------
// GetImmediateMaxAndMask: Returns the max valid value and a bit mask for
//    a full-range immediate of an instruction that has documented
//    masking or clamping of the immediate.
//
// Arguments:
//    instruction   - The instruction to look up
//    simdSize      - The vector size for the instruction
//    maskOut       - A pointer to the location to return the mask
//
// Return Value:
//    The max useful immediate value
//
static unsigned GetImmediateMaxAndMask(instruction ins, unsigned simdSize, unsigned* maskOut)
{
    assert(maskOut != nullptr);
    assert((simdSize >= 16) && (simdSize <= 64));

    unsigned lanes = simdSize / genTypeSize(TYP_SIMD16);
    unsigned mask  = 0xFF;
    unsigned max   = 0;

    switch (ins)
    {
        // These byte-wise shift instructions are documented to return a zero vector
        // for shift amounts 16 or greater.
        case INS_pslldq:
        case INS_psrldq:
        {
            max = 16;
            break;
        }

        // palignr concatenates two 16-byte lanes and shifts the result by imm8 bytes.
        // It is documented to return a zero vector for shift amounts 32 or greater.
        case INS_palignr:
        {
            max = 32;
            break;
        }

        // The following groups of instructions extract/insert a scalar value from/to a
        // 128-bit vector and use a documented range of bits for element index.
        case INS_pextrq:
        case INS_pinsrq:
        {
            mask = 0b00000001;
            max  = mask;
            break;
        }

        case INS_extractps:
        case INS_pextrd:
        case INS_pinsrd:
        {
            mask = 0b00000011;
            max  = mask;
            break;
        }

        case INS_pextrw:
        case INS_pinsrw:
        {
            mask = 0b00000111;
            max  = mask;
            break;
        }

        case INS_pextrb:
        case INS_pinsrb:
        {
            mask = 0b00001111;
            max  = mask;
            break;
        }

        // The following instructions concatenate 128- or 256-bit vectors and shift the
        // result right by imm8 elements. The number of bits used depends on the
        // vector size / element size.
        case INS_valignd:
        {
            mask = (simdSize / genTypeSize(TYP_INT)) - 1;
            max  = mask;
            break;
        }

        case INS_valignq:
        {
            mask = (simdSize / genTypeSize(TYP_LONG)) - 1;
            max  = mask;
            break;
        }

        // The following groups of instructions operate in 128-bit lanes but use a
        // different range of bits from the immediate for each lane.
        case INS_blendpd:
        case INS_shufpd:
        case INS_vpermilpd:
        {
            assert(lanes <= 4);

            // two bits per lane
            mask = (1 << (lanes * 2)) - 1;
            max  = mask;
            break;
        }

        case INS_blendps:
        case INS_vpblendd:
        {
            assert(lanes <= 2);

            // four bits per lane
            mask = (1 << (lanes * 4)) - 1;
            max  = mask;
            break;
        }

        case INS_mpsadbw:
        {
            assert(lanes <= 2);

            // three bits per lane
            mask = (1 << (lanes * 3)) - 1;
            max  = mask;
            break;
        }

        // These instructions extract/insert a 128-bit vector from/to either a 256-bit or
        // 512-bit vector. The number of positions is equal to the number of 128-bit lanes.
        case INS_vextractf32x4:
        case INS_vextracti32x4:
        case INS_vextractf64x2:
        case INS_vextracti64x2:
        case INS_vinsertf32x4:
        case INS_vinserti32x4:
        case INS_vinsertf64x2:
        case INS_vinserti64x2:
        {
            assert(lanes >= 2);

            mask = lanes - 1;
            max  = mask;
            break;
        }

        // These instructions shuffle 128-bit lanes within a larger vector.
        // The number of bits used depends on the number of possible lanes.
        case INS_vshuff32x4:
        case INS_vshufi32x4:
        case INS_vshuff64x2:
        case INS_vshufi64x2:
        {
            assert(lanes >= 2);

            // log2(lanes) bits per lane for src selection
            mask = (1 << (lanes * BitOperations::Log2(lanes))) - 1;
            max  = mask;
            break;
        }

        // These instructions extract/insert a 256-bit vector from/to a 512-bit vector
        // and therefore only have two possible positions.
        case INS_vextractf32x8:
        case INS_vextracti32x8:
        case INS_vextractf64x4:
        case INS_vextracti64x4:
        case INS_vinsertf32x8:
        case INS_vinserti32x8:
        case INS_vinsertf64x4:
        case INS_vinserti64x4:
        {
            assert(simdSize == 64);

            mask = 0b00000001;
            max  = mask;
            break;
        }

        // The following instructions use documented ranges of bits with gaps in them.
        case INS_dppd:
        {
            // bits [1:0] are the result broadcast mask
            // bits [5:4] are the element selection mask
            mask = 0b00110011;
            max  = mask;
            break;
        }

        case INS_pclmulqdq:
        {
            // bit 0 selects the src1 qword
            // bit 4 selects the src2 qword
            mask = 0b00010001;
            max  = mask;
            break;
        }

        case INS_vperm2f128:
        case INS_vperm2i128:
        {
            // bits [1:0] select the src index for the low lane result
            // bits [5:4] select the src index for the high lane result
            // bits 3 and 7, if set, will zero the low or high lane, respectively
            mask = 0b10111011;
            max  = mask;
            break;
        }

        default:
        {
            max = 255;
            break;
        }
    }

    *maskOut = mask;
    return max;
}

//------------------------------------------------------------------------
// genHWIntrinsic: Generates the code for a given hardware intrinsic node.
//
// Arguments:
//    node        - The hardware intrinsic node
//
void CodeGen::genHWIntrinsic(GenTreeHWIntrinsic* node)
{
    NamedIntrinsic         intrinsicId = node->GetHWIntrinsicId();
    CORINFO_InstructionSet isa         = HWIntrinsicInfo::lookupIsa(intrinsicId);
    HWIntrinsicCategory    category    = HWIntrinsicInfo::lookupCategory(intrinsicId);
    size_t                 numArgs     = node->GetOperandCount();
    GenTree*               embMaskNode = nullptr;
    GenTree*               embMaskOp   = nullptr;

    // We need to validate that other phases of the compiler haven't introduced unsupported intrinsics
    assert(compiler->compIsaSupportedDebugOnly(isa));
    assert(HWIntrinsicInfo::RequiresCodegen(intrinsicId));
    assert(!HWIntrinsicInfo::NeedsNormalizeSmallTypeToInt(intrinsicId) || !varTypeIsSmall(node->GetSimdBaseType()));

    bool    isTableDriven = HWIntrinsicInfo::genIsTableDrivenHWIntrinsic(intrinsicId, category);
    insOpts instOptions   = INS_OPTS_NONE;

    if (GetEmitter()->UseEvexEncoding())
    {
        if (numArgs == 3)
        {
            GenTree* op2 = node->Op(2);

            if (op2->IsEmbMaskOp())
            {
                assert(intrinsicId == NI_AVX512_BlendVariableMask);
                assert(op2->isContained());
                assert(op2->OperIsHWIntrinsic());

                // We currently only support this for table driven intrinsics
                assert(isTableDriven);

                GenTree* op1 = node->Op(1);
                GenTree* op3 = node->Op(3);

                regNumber targetReg = node->GetRegNum();
                regNumber mergeReg  = op1->GetRegNum();
                regNumber maskReg   = op3->GetRegNum();

                // TODO-AVX512-CQ: Ensure we can support embedded operations on RMW intrinsics
                assert(!op2->isRMWHWIntrinsic(compiler));

                bool mergeWithZero = op1->isContained();

                if (mergeWithZero)
                {
                    // We're merging with zero, so we the target register isn't RMW
                    assert(op1->IsVectorZero());
                    mergeWithZero = true;
                }
                else
                {
                    // Make sure we consume the registers that are getting specially handled
                    genConsumeReg(op1);

                    // We're merging with a non-zero value, so the target register is RMW
                    emitAttr attr = emitActualTypeSize(Compiler::getSIMDTypeForSize(node->GetSimdSize()));
                    GetEmitter()->emitIns_Mov(INS_movaps, attr, targetReg, mergeReg, /* canSkip */ true);
                }

                // Update op2 to use the actual target register
                op2->ClearContained();
                op2->SetRegNum(targetReg);

                // Track the original mask node so we can call genProduceReg
                embMaskNode = node;

                // Fixup all the already initialized variables
                node        = op2->AsHWIntrinsic();
                intrinsicId = node->GetHWIntrinsicId();
                isa         = HWIntrinsicInfo::lookupIsa(intrinsicId);
                category    = HWIntrinsicInfo::lookupCategory(intrinsicId);
                numArgs     = node->GetOperandCount();

                // Add the embedded masking info to the insOpts
                instOptions = AddEmbMaskingMode(instOptions, maskReg, mergeWithZero);

                // We don't need to genProduceReg(node) since that will be handled by processing op2
                // likewise, processing op2 will ensure its own registers are consumed
                embMaskOp = op3;
            }
        }

        if (node->OperIsEmbRoundingEnabled())
        {
            GenTree* lastOp = node->Op(numArgs);

            // Now that we've extracted the rounding mode, we'll remove the
            // last operand, adjust the arg count, and continue. This allows
            // us to reuse all the existing logic without having to add new
            // specialized handling everywhere.

            switch (numArgs)
            {
                case 2:
                {
                    numArgs = 1;
                    node->ResetHWIntrinsicId(intrinsicId, compiler, node->Op(1));
                    break;
                }

                case 3:
                {
                    numArgs = 2;
                    node->ResetHWIntrinsicId(intrinsicId, compiler, node->Op(1), node->Op(2));
                    break;
                }

                case 4:
                {
                    numArgs = 3;
                    node->ResetHWIntrinsicId(intrinsicId, compiler, node->Op(1), node->Op(2), node->Op(3));
                    break;
                }

                default:
                {
                    unreached();
                }
            }

            if (lastOp->isContained())
            {
                assert(lastOp->IsCnsIntOrI());

                int8_t mode = static_cast<int8_t>(lastOp->AsIntCon()->IconValue());
                instOptions = AddEmbRoundingMode(instOptions, mode);
            }
            else
            {
                var_types baseType = node->GetSimdBaseType();

                instruction ins = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler);
                assert(ins != INS_invalid);

                emitAttr simdSize = emitActualTypeSize(Compiler::getSIMDTypeForSize(node->GetSimdSize()));
                assert(simdSize != 0);

                genConsumeMultiOpOperands(node);
                genConsumeRegs(lastOp);

                if (isTableDriven)
                {
                    if (embMaskOp != nullptr)
                    {
                        // Handle an extra operand we need to consume so that
                        // embedded masking can work without making the overall
                        // logic significantly more complex.

                        assert(embMaskNode != nullptr);
                        genConsumeReg(embMaskOp);
                    }

                    switch (numArgs)
                    {
                        case 1:
                        {
                            regNumber targetReg  = node->GetRegNum();
                            GenTree*  rmOp       = node->Op(1);
                            auto      emitSwCase = [&](int8_t i) {
                                insOpts newInstOptions = AddEmbRoundingMode(instOptions, i);
                                genHWIntrinsic_R_RM(node, ins, simdSize, targetReg, rmOp, newInstOptions);
                            };
                            regNumber baseReg = internalRegisters.Extract(node);
                            regNumber offsReg = internalRegisters.GetSingle(node);
                            genHWIntrinsicJumpTableFallback(intrinsicId, ins, simdSize, lastOp->GetRegNum(), baseReg,
                                                            offsReg, emitSwCase);
                            break;
                        }
                        case 2:
                        {
                            auto emitSwCase = [&](int8_t i) {
                                insOpts newInstOptions = AddEmbRoundingMode(instOptions, i);
                                genHWIntrinsic_R_R_RM(node, ins, simdSize, newInstOptions);
                            };
                            regNumber baseReg = internalRegisters.Extract(node);
                            regNumber offsReg = internalRegisters.GetSingle(node);
                            genHWIntrinsicJumpTableFallback(intrinsicId, ins, simdSize, lastOp->GetRegNum(), baseReg,
                                                            offsReg, emitSwCase);
                            break;
                        }

                        default:
                        {
                            unreached();
                        }
                    }
                }
                else
                {
                    // We shouldn't encounter embedded masking for non table-driven intrinsics
                    assert((embMaskNode == nullptr) && (embMaskOp == nullptr));

                    // There are a few embedded rounding intrinsics that need to be emitted with special handling.
                    genNonTableDrivenHWIntrinsicsJumpTableFallback(node, lastOp);
                }

                genProduceReg(node);

                if (embMaskNode != nullptr)
                {
                    // Similarly to the mask operand, we need to handle the
                    // mask node to ensure everything works correctly, particularly
                    // lifetimes and spilling if required. Doing it this way avoids
                    // needing to duplicate all our existing handling.

                    assert(embMaskOp != nullptr);
                    genProduceReg(embMaskNode);
                }
                return;
            }
        }
    }

    if (isTableDriven)
    {
        genConsumeMultiOpOperands(node);

        if (embMaskOp != nullptr)
        {
            // Handle an extra operand we need to consume so that
            // embedded masking can work without making the overall
            // logic significantly more complex.

            assert(embMaskNode != nullptr);
            genConsumeReg(embMaskOp);
        }

        regNumber targetReg = node->GetRegNum();
        var_types baseType  = node->GetSimdBaseType();

        GenTree* op1 = nullptr;
        GenTree* op2 = nullptr;
        GenTree* op3 = nullptr;
        GenTree* op4 = nullptr;

        regNumber op1Reg = REG_NA;
        regNumber op2Reg = REG_NA;
        regNumber op3Reg = REG_NA;
        regNumber op4Reg = REG_NA;
        emitter*  emit   = GetEmitter();

        assert(numArgs >= 0);

        instruction ins = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler);
        assert(ins != INS_invalid);

        emitAttr simdSize = emitActualTypeSize(Compiler::getSIMDTypeForSize(node->GetSimdSize()));
        assert(simdSize != 0);

        int ival = HWIntrinsicInfo::lookupIval(compiler, intrinsicId, baseType);

        switch (numArgs)
        {
            case 1:
            {
                op1 = node->Op(1);

                if (node->OperIsMemoryLoad())
                {
                    // Until we improve the handling of addressing modes in the emitter, we'll create a
                    // temporary GT_IND to generate code with.

                    assert(instOptions == INS_OPTS_NONE);
                    GenTreeIndir load = indirForm(node->TypeGet(), op1);
                    emit->emitInsLoadInd(ins, simdSize, node->GetRegNum(), &load);
                }
                else
                {
                    op1Reg = op1->GetRegNum();

                    if (ival != -1)
                    {
                        assert((ival >= 0) && (ival <= 127));
                        if (HWIntrinsicInfo::CopiesUpperBits(intrinsicId))
                        {
                            assert(!op1->isContained());
                            emit->emitIns_SIMD_R_R_R_I(ins, simdSize, targetReg, op1Reg, op1Reg,
                                                       static_cast<int8_t>(ival), instOptions);
                        }
                        else
                        {
                            genHWIntrinsic_R_RM_I(node, ins, simdSize, static_cast<int8_t>(ival), instOptions);
                        }
                    }
                    else if (HWIntrinsicInfo::CopiesUpperBits(intrinsicId))
                    {
                        assert(!op1->isContained());
                        emit->emitIns_SIMD_R_R_R(ins, simdSize, targetReg, op1Reg, op1Reg, instOptions);
                    }
                    else
                    {
                        genHWIntrinsic_R_RM(node, ins, simdSize, targetReg, op1, instOptions);
                    }
                }
                break;
            }

            case 2:
            {
                op1 = node->Op(1);
                op2 = node->Op(2);

                if (category == HW_Category_MemoryStore)
                {
                    // Until we improve the handling of addressing modes in the emitter, we'll create a
                    // temporary GT_STORE_IND to generate code with.

                    assert(instOptions == INS_OPTS_NONE);
                    GenTreeStoreInd store = storeIndirForm(node->TypeGet(), op1, op2);
                    emit->emitInsStoreInd(ins, simdSize, &store);
                    break;
                }

                op1Reg = op1->GetRegNum();
                op2Reg = op2->GetRegNum();

                if ((op1Reg != targetReg) && (op2Reg == targetReg) && node->isRMWHWIntrinsic(compiler))
                {
                    // We have "reg2 = reg1 op reg2" where "reg1 != reg2" on a RMW intrinsic.
                    //
                    // For non-commutative intrinsics, we should have ensured that op2 was marked
                    // delay free in order to prevent it from getting assigned the same register
                    // as target. However, for commutative intrinsics, we can just swap the operands
                    // in order to have "reg2 = reg2 op reg1" which will end up producing the right code.

                    noway_assert(node->OperIsCommutative());
                    op2Reg = op1Reg;
                    op1Reg = targetReg;
                }

                if (ival != -1)
                {
                    assert((ival >= 0) && (ival <= 127));
                    genHWIntrinsic_R_R_RM_I(node, ins, simdSize, static_cast<int8_t>(ival), instOptions);
                }
                else if (category == HW_Category_MemoryLoad)
                {
                    // Get the address and the 'other' register.
                    GenTree*  addr;
                    regNumber otherReg;
                    if (intrinsicId == NI_AVX_MaskLoad || intrinsicId == NI_AVX2_MaskLoad)
                    {
                        addr     = op1;
                        otherReg = op2Reg;
                    }
                    else
                    {
                        addr     = op2;
                        otherReg = op1Reg;
                    }
                    // Until we improve the handling of addressing modes in the emitter, we'll create a
                    // temporary GT_IND to generate code with.
                    GenTreeIndir load = indirForm(node->TypeGet(), addr);

                    assert(!node->isRMWHWIntrinsic(compiler));
                    inst_RV_RV_TT(ins, simdSize, targetReg, otherReg, &load, false, instOptions);
                }
                else if (HWIntrinsicInfo::isImmOp(intrinsicId, op2))
                {
                    auto emitSwCase = [&](int8_t i) {
                        if (HWIntrinsicInfo::CopiesUpperBits(intrinsicId))
                        {
                            assert(!op1->isContained());
                            emit->emitIns_SIMD_R_R_R_I(ins, simdSize, targetReg, op1Reg, op1Reg, i, instOptions);
                        }
                        else
                        {
                            genHWIntrinsic_R_RM_I(node, ins, simdSize, i, instOptions);
                        }
                    };

                    if (op2->IsCnsIntOrI())
                    {
                        ssize_t ival = op2->AsIntCon()->IconValue();
                        assert((ival >= 0) && (ival <= 255));
                        emitSwCase((int8_t)ival);
                    }
                    else
                    {
                        // We emit a fallback case for the scenario when the imm-op is not a constant.
                        // This should
                        // normally happen when the intrinsic is called indirectly, such as via
                        // Reflection. However, it
                        // can also occur if the consumer calls it directly and just doesn't pass a
                        // constant value.
                        regNumber baseReg = internalRegisters.Extract(node);
                        regNumber offsReg = internalRegisters.GetSingle(node);
                        genHWIntrinsicJumpTableFallback(intrinsicId, ins, simdSize, op2Reg, baseReg, offsReg,
                                                        emitSwCase);
                    }
                }
                else if (node->TypeIs(TYP_VOID))
                {
                    genHWIntrinsic_R_RM(node, ins, simdSize, op1Reg, op2, instOptions);
                }
                else
                {
                    genHWIntrinsic_R_R_RM(node, ins, simdSize, instOptions);
                }
                break;
            }

            case 3:
            {
                op1 = node->Op(1);
                op2 = node->Op(2);
                op3 = node->Op(3);

                op1Reg = op1->GetRegNum();
                op2Reg = op2->GetRegNum();
                op3Reg = op3->GetRegNum();

                assert(ival == -1);

                if (HWIntrinsicInfo::isImmOp(intrinsicId, op3))
                {
                    auto emitSwCase = [&](int8_t i) {
                        genHWIntrinsic_R_R_RM_I(node, ins, simdSize, i, instOptions);
                    };

                    if (op3->IsCnsIntOrI())
                    {
                        ssize_t ival = op3->AsIntCon()->IconValue();
                        assert((ival >= 0) && (ival <= 255));
                        emitSwCase((int8_t)ival);
                    }
                    else
                    {
                        // We emit a fallback case for the scenario when the imm-op is not a constant. This should
                        // normally happen when the intrinsic is called indirectly, such as via Reflection. However, it
                        // can also occur if the consumer calls it directly and just doesn't pass a constant value.
                        regNumber baseReg = internalRegisters.Extract(node);
                        regNumber offsReg = internalRegisters.GetSingle(node);
                        genHWIntrinsicJumpTableFallback(intrinsicId, ins, simdSize, op3Reg, baseReg, offsReg,
                                                        emitSwCase);
                    }
                }
                else if (category == HW_Category_MemoryLoad)
                {
                    bool mergeWithZero = false;

                    if (op3->isContained())
                    {
                        op3Reg        = targetReg;
                        mergeWithZero = true;
                    }

                    assert(emitter::isMaskReg(op2Reg));
                    assert(mergeWithZero == op3->IsVectorZero());

                    // Until we improve the handling of addressing modes in the emitter, we'll create a
                    // temporary GT_IND to generate code with.
                    GenTreeIndir load = indirForm(node->TypeGet(), op1);
                    emit->emitIns_Mov(INS_movaps, simdSize, targetReg, op3Reg, /* canSkip */ true);

                    instOptions = AddEmbMaskingMode(instOptions, op2Reg, mergeWithZero);
                    emit->emitIns_R_A(ins, simdSize, targetReg, &load, instOptions);
                }
                else if (category == HW_Category_MemoryStore)
                {
                    if (emitter::isMaskReg(op2Reg))
                    {
                        // Until we improve the handling of addressing modes in the emitter, we'll create a
                        // temporary GT_STORE_IND to generate code with.
                        GenTreeStoreInd store = storeIndirForm(node->TypeGet(), op1, op3);

                        instOptions = AddEmbMaskingMode(instOptions, op2Reg, false);
                        emit->emitInsStoreInd(ins, simdSize, &store, instOptions);
                        break;
                    }

                    // The Mask instructions do not currently support containment of the address.
                    assert(!op2->isContained());

                    if (intrinsicId == NI_AVX_MaskStore || intrinsicId == NI_AVX2_MaskStore)
                    {
                        emit->emitIns_AR_R_R(ins, simdSize, op2Reg, op3Reg, op1Reg, 0, instOptions);
                    }
                    else
                    {
                        assert(intrinsicId == NI_X86Base_MaskMove);
                        assert(targetReg == REG_NA);

                        // SSE2 MaskMove hardcodes the destination (op3) in DI/EDI/RDI
                        emit->emitIns_Mov(INS_mov, EA_PTRSIZE, REG_EDI, op3Reg, /* canSkip */ true);

                        emit->emitIns_R_R(ins, simdSize, op1Reg, op2Reg, instOptions);
                    }
                }
                else
                {
                    switch (intrinsicId)
                    {
                        case NI_SSE42_BlendVariable:
                        case NI_AVX_BlendVariable:
                        case NI_AVX2_BlendVariable:
                        case NI_AVX512_BlendVariableMask:
                        {
                            genHWIntrinsic_R_R_RM_R(node, ins, simdSize, instOptions);
                            break;
                        }

                        case NI_AVX512_CompressMask:
                        case NI_AVX512_ExpandMask:
                        {
                            bool mergeWithZero = false;

                            if (op1->isContained())
                            {
                                op1Reg        = targetReg;
                                mergeWithZero = true;
                            }

                            assert(emitter::isMaskReg(op2Reg));
                            assert(mergeWithZero == op1->IsVectorZero());

                            emitAttr attr = emitActualTypeSize(Compiler::getSIMDTypeForSize(node->GetSimdSize()));
                            emit->emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, /* canSkip */ true);

                            instOptions = AddEmbMaskingMode(instOptions, op2Reg, mergeWithZero);
                            emit->emitIns_R_R(ins, attr, targetReg, op3Reg, instOptions);
                            break;
                        }

                        case NI_AVXVNNI_MultiplyWideningAndAdd:
                        case NI_AVXVNNI_MultiplyWideningAndAddSaturate:
                        {
                            assert(targetReg != REG_NA);
                            assert(op1Reg != REG_NA);
                            assert(op2Reg != REG_NA);

                            genHWIntrinsic_R_R_R_RM(ins, simdSize, targetReg, op1Reg, op2Reg, op3, instOptions);
                            break;
                        }

                        default:
                        {
                            unreached();
                            break;
                        };
                    }
                }
                break;
            }

            case 4:
            {
                op1 = node->Op(1);
                op2 = node->Op(2);
                op3 = node->Op(3);
                op4 = node->Op(4);

                op1Reg = op1->GetRegNum();
                op2Reg = op2->GetRegNum();
                op3Reg = op3->GetRegNum();
                op4Reg = op4->GetRegNum();

                assert(ival == -1);

                if (HWIntrinsicInfo::isImmOp(intrinsicId, op4))
                {
                    auto emitSwCase = [&](int8_t i) {
                        genHWIntrinsic_R_R_R_RM_I(node, ins, simdSize, i, instOptions);
                    };

                    if (op4->IsCnsIntOrI())
                    {
                        ssize_t ival = op4->AsIntCon()->IconValue();
                        assert((ival >= 0) && (ival <= 255));
                        emitSwCase(static_cast<int8_t>(ival));
                    }
                    else
                    {
                        // We emit a fallback case for the scenario when the imm-op is not a constant. This should
                        // normally happen when the intrinsic is called indirectly, such as via Reflection. However, it
                        // can also occur if the consumer calls it directly and just doesn't pass a constant value.
                        regNumber baseReg = internalRegisters.Extract(node);
                        regNumber offsReg = internalRegisters.GetSingle(node);
                        genHWIntrinsicJumpTableFallback(intrinsicId, ins, simdSize, op4Reg, baseReg, offsReg,
                                                        emitSwCase);
                    }
                }
                else
                {
                    unreached();
                }
                break;
            }

            default:
                unreached();
                break;
        }

        genProduceReg(node);

        if (embMaskNode != nullptr)
        {
            // Similarly to the mask operand, we need to handle the
            // mask node to ensure everything works correctly, particularly
            // lifetimes and spilling if required. Doing it this way avoids
            // needing to duplicate all our existing handling.

            assert(embMaskOp != nullptr);
            genProduceReg(embMaskNode);
        }
        return;
    }

    // We shouldn't encounter embedded masking for non table-driven intrinsics
    assert((embMaskNode == nullptr) && (embMaskOp == nullptr));

    switch (isa)
    {
        case InstructionSet_Vector128:
        case InstructionSet_Vector256:
        case InstructionSet_Vector512:
        {
            genBaseIntrinsic(node, instOptions);
            break;
        }

        case InstructionSet_X86Base:
        case InstructionSet_X86Base_X64:
        {
            genX86BaseIntrinsic(node, instOptions);
            break;
        }

        case InstructionSet_SSE42:
        case InstructionSet_SSE42_X64:
        {
            genSse42Intrinsic(node, instOptions);
            break;
        }

        case InstructionSet_AVX:
        case InstructionSet_AVX2:
        case InstructionSet_AVX2_X64:
        case InstructionSet_AVX512:
        case InstructionSet_AVX512_X64:
        case InstructionSet_AVX512v2:
        case InstructionSet_AVXVNNIINT:
        case InstructionSet_AVXVNNIINT_V512:
        {
            genAvxFamilyIntrinsic(node, instOptions);
            break;
        }

        case InstructionSet_X86Serialize:
        case InstructionSet_X86Serialize_X64:
        {
            assert(instOptions == INS_OPTS_NONE);
            genX86SerializeIntrinsic(node);
            break;
        }

        default:
            unreached();
            break;
    }
}

//------------------------------------------------------------------------
// genHWIntrinsic_R_RM: Generates code for a hardware intrinsic node that takes a
//                      register operand and a register/memory operand.
//
// Arguments:
//    node - The hardware intrinsic node
//    ins  - The instruction being generated
//    attr - The emit attribute for the instruction being generated
//    reg  - The register
//    rmOp - The register/memory operand node
//    instOptions - the existing intOpts
void CodeGen::genHWIntrinsic_R_RM(
    GenTreeHWIntrinsic* node, instruction ins, emitAttr attr, regNumber reg, GenTree* rmOp, insOpts instOptions)
{
    emitter* emit = GetEmitter();

    if (CodeGenInterface::IsEmbeddedBroadcastEnabled(ins, rmOp))
    {
        instOptions = AddEmbBroadcastMode(instOptions);
    }
    else if ((instOptions == INS_OPTS_NONE) && !GetEmitter()->IsVexEncodableInstruction(ins))
    {
        // We may have opportunistically selected an EVEX only instruction
        // that isn't actually required, so fallback to the VEX compatible
        // encoding to potentially save on the number of bytes emitted.

        switch (ins)
        {
            case INS_vbroadcastf64x2:
            {
                ins = INS_vbroadcastf32x4;
                break;
            }

            case INS_vbroadcasti64x2:
            {
                ins = INS_vbroadcasti32x4;
                break;
            }

            case INS_vmovdqa64:
            {
                ins = INS_movdqa32;
                break;
            }

            case INS_vmovdqu64:
            {
                ins = INS_movdqu32;
                break;
            }

            default:
            {
                break;
            }
        }
    }

    OperandDesc rmOpDesc = genOperandDesc(ins, rmOp);

    if (((instOptions & INS_OPTS_EVEX_b_MASK) != 0) && (rmOpDesc.GetKind() == OperandKind::Reg))
    {
        // As embedded rounding only applies in R_R case, we can skip other checks for different paths.
        regNumber op1Reg = rmOp->GetRegNum();
        assert(op1Reg != REG_NA);

        emit->emitIns_R_R(ins, attr, reg, op1Reg, instOptions);
        return;
    }

    if (rmOpDesc.IsContained())
    {
        assert(HWIntrinsicInfo::SupportsContainment(node->GetHWIntrinsicId()));
        assertIsContainableHWIntrinsicOp(compiler->m_pLowering, node, rmOp);
    }

    switch (rmOpDesc.GetKind())
    {
        case OperandKind::ClsVar:
            emit->emitIns_R_C(ins, attr, reg, rmOpDesc.GetFieldHnd(), 0, instOptions);
            break;

        case OperandKind::Local:
            emit->emitIns_R_S(ins, attr, reg, rmOpDesc.GetVarNum(), rmOpDesc.GetLclOffset(), instOptions);
            break;

        case OperandKind::Indir:
        {
            // Until we improve the handling of addressing modes in the emitter, we'll create a
            // temporary GT_IND to generate code with.
            GenTreeIndir  indirForm;
            GenTreeIndir* indir = rmOpDesc.GetIndirForm(&indirForm);
            emit->emitIns_R_A(ins, attr, reg, indir, instOptions);
        }
        break;

        case OperandKind::Reg:
        {
            regNumber rmOpReg = rmOpDesc.GetReg();

            if (emit->IsMovInstruction(ins))
            {
                assert(instOptions == INS_OPTS_NONE);
                emit->emitIns_Mov(ins, attr, reg, rmOpReg, /* canSkip */ false);
            }
            else
            {
                if (varTypeIsIntegral(rmOp))
                {
                    bool needsBroadcastFixup   = false;
                    bool needsInstructionFixup = false;

                    switch (node->GetHWIntrinsicId())
                    {
                        case NI_AVX2_BroadcastScalarToVector128:
                        case NI_AVX2_BroadcastScalarToVector256:
                        {
                            if (compiler->canUseEvexEncoding())
                            {
                                needsInstructionFixup = true;
                            }
                            else
                            {
                                needsBroadcastFixup = true;
                            }
                            break;
                        }

                        case NI_AVX512_BroadcastScalarToVector512:
                        {
                            needsInstructionFixup = true;
                            break;
                        }

                        default:
                        {
                            break;
                        }
                    }

                    if (needsBroadcastFixup)
                    {
                        // In lowering we had the special case of BroadcastScalarToVector(CreateScalarUnsafe(op1))
                        //
                        // This is one of the only instructions where it supports taking integer types from
                        // a SIMD register or directly as a scalar from memory. Most other instructions, in
                        // comparison, take such values from general-purpose registers instead.
                        //
                        // Because of this, we removed the CreateScalarUnsafe and tried to contain op1 directly
                        // that failed and we either didn't get marked regOptional or we did and didn't get spilled
                        //
                        // As such, we need to emulate the removed CreateScalarUnsafe to ensure that op1 is in a
                        // SIMD register so the broadcast instruction can execute successfully. We'll just move
                        // the value into the target register and then broadcast it out from that.

                        emitAttr movdAttr = emitActualTypeSize(node->GetSimdBaseType());

#if defined(TARGET_AMD64)
                        instruction movdIns = (movdAttr == EA_4BYTE) ? INS_movd32 : INS_movd64;
#else
                        instruction movdIns = INS_movd32;
#endif

                        emit->emitIns_Mov(movdIns, movdAttr, reg, rmOpReg, /* canSkip */ false);
                        rmOpReg = reg;
                    }
                    else if (needsInstructionFixup)
                    {
                        switch (ins)
                        {
                            case INS_vpbroadcastb:
                            {
                                ins = INS_vpbroadcastb_gpr;
                                break;
                            }

                            case INS_vpbroadcastd:
                            {
                                ins = INS_vpbroadcastd_gpr;
                                break;
                            }

                            case INS_vpbroadcastq:
                            {
                                ins = INS_vpbroadcastq_gpr;
                                break;
                            }

                            case INS_vpbroadcastw:
                            {
                                ins = INS_vpbroadcastw_gpr;
                                break;
                            }

                            default:
                            {
                                unreached();
                            }
                        }
                    }
                }

                emit->emitIns_R_R(ins, attr, reg, rmOpReg, instOptions);
            }
            break;
        }

        default:
            unreached();
    }
}

//------------------------------------------------------------------------
// genHWIntrinsic_R_RM_I: Generates the code for a hardware intrinsic node that takes a register/memory operand,
//                        an immediate operand, and that returns a value in register
//
// Arguments:
//    node - The hardware intrinsic node
//    ins  - The instruction being generated
//    ival - The immediate value
//
void CodeGen::genHWIntrinsic_R_RM_I(
    GenTreeHWIntrinsic* node, instruction ins, emitAttr simdSize, int8_t ival, insOpts instOptions)
{
    regNumber targetReg = node->GetRegNum();
    GenTree*  op1       = node->Op(1);

    // TODO-XArch-CQ: Commutative operations can have op1 be contained
    // TODO-XArch-CQ: Non-VEX encoded instructions can have both ops contained

    assert(targetReg != REG_NA);
    assert(!node->OperIsCommutative()); // One operand intrinsics cannot be commutative

    if (op1->isContained() || op1->isUsedFromSpillTemp())
    {
        assert(HWIntrinsicInfo::SupportsContainment(node->GetHWIntrinsicId()));
        assertIsContainableHWIntrinsicOp(compiler->m_pLowering, node, op1);
    }
    inst_RV_TT_IV(ins, simdSize, targetReg, op1, ival, instOptions);
}

//------------------------------------------------------------------------
// genHWIntrinsic_R_R_RM: Generates the code for a hardware intrinsic node that takes a register operand, a
//                        register/memory operand, and that returns a value in register
//
// Arguments:
//    node        - The hardware intrinsic node
//    ins         - The instruction being generated
//    attr        - The emit attribute for the instruction being generated
//    instOptions - The options that modify how the instruction is generated
//
void CodeGen::genHWIntrinsic_R_R_RM(GenTreeHWIntrinsic* node, instruction ins, emitAttr attr, insOpts instOptions)
{
    regNumber targetReg = node->GetRegNum();
    GenTree*  op1       = node->Op(1);
    GenTree*  op2       = node->Op(2);
    regNumber op1Reg    = op1->GetRegNum();

    assert(targetReg != REG_NA);
    assert(op1Reg != REG_NA);

    if (op2->isContained() || op2->isUsedFromSpillTemp())
    {
        assert(HWIntrinsicInfo::SupportsContainment(node->GetHWIntrinsicId()));
        assertIsContainableHWIntrinsicOp(compiler->m_pLowering, node, op2);
    }

    bool isRMW = node->isRMWHWIntrinsic(compiler);
    inst_RV_RV_TT(ins, attr, targetReg, op1Reg, op2, isRMW, instOptions);
}

//------------------------------------------------------------------------
// genHWIntrinsic_R_R_RM_I: Generates the code for a hardware intrinsic node that takes a register operand, a
//                        register/memory operand, an immediate operand, and that returns a value in register
//
// Arguments:
//    node - The hardware intrinsic node
//    ins  - The instruction being generated
//    ival - The immediate value
//
void CodeGen::genHWIntrinsic_R_R_RM_I(
    GenTreeHWIntrinsic* node, instruction ins, emitAttr simdSize, int8_t ival, insOpts instOptions)
{
    regNumber targetReg = node->GetRegNum();
    GenTree*  op1       = node->Op(1);
    GenTree*  op2       = node->Op(2);
    regNumber op1Reg    = op1->GetRegNum();

    assert(targetReg != REG_NA);

    if (ins == INS_insertps)
    {
        if (op1->isContained())
        {
            // insertps is special and can contain op1 when it is zero
            assert(op1->IsVectorZero());
            op1Reg = targetReg;
        }

        if (op2->isContained() && op2->IsVectorZero())
        {
            // insertps can also contain op2 when it is zero in which case
            // we just reuse op1Reg since ival specifies the entry to zero

            GetEmitter()->emitIns_SIMD_R_R_R_I(ins, simdSize, targetReg, op1Reg, op1Reg, ival, instOptions);
            return;
        }
    }

    if (op2->isContained() || op2->isUsedFromSpillTemp())
    {
        assert(HWIntrinsicInfo::SupportsContainment(node->GetHWIntrinsicId()));
        assertIsContainableHWIntrinsicOp(compiler->m_pLowering, node, op2);
    }

    assert(op1Reg != REG_NA);

    bool isRMW = node->isRMWHWIntrinsic(compiler);
    inst_RV_RV_TT_IV(ins, simdSize, targetReg, op1Reg, op2, ival, isRMW, instOptions);
}

//------------------------------------------------------------------------
// genHWIntrinsic_R_R_RM_R: Generates the code for a hardware intrinsic node that takes a register operand, a
//                          register/memory operand, another register operand, and that returns a value in register
//
// Arguments:
//    node - The hardware intrinsic node
//    ins  - The instruction being generated
//
void CodeGen::genHWIntrinsic_R_R_RM_R(GenTreeHWIntrinsic* node, instruction ins, emitAttr simdSize, insOpts instOptions)
{
    regNumber targetReg = node->GetRegNum();
    GenTree*  op1       = node->Op(1);
    GenTree*  op2       = node->Op(2);
    GenTree*  op3       = node->Op(3);
    emitter*  emit      = GetEmitter();

    regNumber op1Reg = REG_NA;
    regNumber op3Reg = op3->GetRegNum();

    if (op1->isContained())
    {
        assert(op1->IsVectorZero());
        instOptions = AddEmbMaskingMode(instOptions, REG_K0, true);
        op1Reg      = targetReg;
    }
    else
    {
        op1Reg = op1->GetRegNum();
    }

    assert(targetReg != REG_NA);
    assert(op1Reg != REG_NA);
    assert(op3Reg != REG_NA);

    if (CodeGenInterface::IsEmbeddedBroadcastEnabled(ins, op2))
    {
        instOptions = AddEmbBroadcastMode(instOptions);
    }

    OperandDesc op2Desc = genOperandDesc(ins, op2);

    if (op2Desc.IsContained())
    {
        assert(HWIntrinsicInfo::SupportsContainment(node->GetHWIntrinsicId()));
        assertIsContainableHWIntrinsicOp(compiler->m_pLowering, node, op2);
    }

    switch (op2Desc.GetKind())
    {
        case OperandKind::ClsVar:
            emit->emitIns_SIMD_R_R_C_R(ins, simdSize, targetReg, op1Reg, op3Reg, op2Desc.GetFieldHnd(), 0, instOptions);
            break;

        case OperandKind::Local:
            emit->emitIns_SIMD_R_R_S_R(ins, simdSize, targetReg, op1Reg, op3Reg, op2Desc.GetVarNum(),
                                       op2Desc.GetLclOffset(), instOptions);
            break;

        case OperandKind::Indir:
        {
            // Until we improve the handling of addressing modes in the emitter, we'll create a
            // temporary GT_IND to generate code with.
            GenTreeIndir  indirForm;
            GenTreeIndir* indir = op2Desc.GetIndirForm(&indirForm);
            emit->emitIns_SIMD_R_R_A_R(ins, simdSize, targetReg, op1Reg, op3Reg, indir, instOptions);
        }
        break;

        case OperandKind::Reg:
            emit->emitIns_SIMD_R_R_R_R(ins, simdSize, targetReg, op1Reg, op2Desc.GetReg(), op3Reg, instOptions);
            break;

        default:
            unreached();
    }
}

//------------------------------------------------------------------------
// genHWIntrinsic_R_R_R_RM: Generates the code for a hardware intrinsic node that takes two register operands,
//                          a register/memory operand, and that returns a value in register
//
// Arguments:
//    ins       - The instruction being generated
//    attr      - The emit attribute
//    targetReg - The target register
//    op1Reg    - The register of the first operand
//    op2Reg    - The register of the second operand
//    op3       - The third operand
//    instOptions - The options that modify how the instruction is generated
//
void CodeGen::genHWIntrinsic_R_R_R_RM(instruction ins,
                                      emitAttr    attr,
                                      regNumber   targetReg,
                                      regNumber   op1Reg,
                                      regNumber   op2Reg,
                                      GenTree*    op3,
                                      insOpts     instOptions)
{
    assert(targetReg != REG_NA);
    assert(op1Reg != REG_NA);
    assert(op2Reg != REG_NA);

    emitter* emit = GetEmitter();

    if (CodeGenInterface::IsEmbeddedBroadcastEnabled(ins, op3))
    {
        instOptions = AddEmbBroadcastMode(instOptions);
    }

    OperandDesc op3Desc = genOperandDesc(ins, op3);

    if (((instOptions & INS_OPTS_EVEX_b_MASK) != 0) && (op3Desc.GetKind() == OperandKind::Reg))
    {
        // As embedded rounding only applies in R_R case, we can skip other checks for different paths.
        regNumber op3Reg = op3->GetRegNum();
        assert(op3Reg != REG_NA);

        emit->emitIns_SIMD_R_R_R_R(ins, attr, targetReg, op1Reg, op2Reg, op3Desc.GetReg(), instOptions);
        return;
    }

    switch (op3Desc.GetKind())
    {
        case OperandKind::ClsVar:
            emit->emitIns_SIMD_R_R_R_C(ins, attr, targetReg, op1Reg, op2Reg, op3Desc.GetFieldHnd(), 0, instOptions);
            break;

        case OperandKind::Local:
            emit->emitIns_SIMD_R_R_R_S(ins, attr, targetReg, op1Reg, op2Reg, op3Desc.GetVarNum(),
                                       op3Desc.GetLclOffset(), instOptions);
            break;

        case OperandKind::Indir:
        {
            // Until we improve the handling of addressing modes in the emitter, we'll create a
            // temporary GT_IND to generate code with.
            GenTreeIndir  indirForm;
            GenTreeIndir* indir = op3Desc.GetIndirForm(&indirForm);
            emit->emitIns_SIMD_R_R_R_A(ins, attr, targetReg, op1Reg, op2Reg, indir, instOptions);
        }
        break;

        case OperandKind::Reg:
            emit->emitIns_SIMD_R_R_R_R(ins, attr, targetReg, op1Reg, op2Reg, op3Desc.GetReg(), instOptions);
            break;

        default:
            unreached();
    }
}

//------------------------------------------------------------------------
// genHWIntrinsic_R_R_R_RM_I: Generates the code for a hardware intrinsic node that takes two register operands,
//                          a register/memory operand, an immediate operand, and that returns a value in register
//
// Arguments:
//    node - The hardware intrinsic node
//    ins  - The instruction being generated
//    ival - The immediate value
//
void CodeGen::genHWIntrinsic_R_R_R_RM_I(
    GenTreeHWIntrinsic* node, instruction ins, emitAttr attr, int8_t ival, insOpts instOptions)
{
    regNumber targetReg = node->GetRegNum();
    GenTree*  op1       = node->Op(1);
    GenTree*  op2       = node->Op(2);
    GenTree*  op3       = node->Op(3);
    regNumber op1Reg    = op1->GetRegNum();
    regNumber op2Reg    = op2->GetRegNum();

    if (op1->isContained())
    {
        // op1 is never selected by the table so
        // we can contain and ignore any register
        // allocated to it resulting in better
        // non-RMW based codegen.

        assert(!node->isRMWHWIntrinsic(compiler));
        op1Reg = targetReg;

        if (op2->isContained())
        {
            // op2 is never selected by the table so
            // we can contain and ignore any register
            // allocated to it resulting in better
            // non-RMW based codegen.

#if defined(DEBUG)
            assert(node->GetHWIntrinsicId() == NI_AVX512_TernaryLogic);

            uint8_t                 control  = static_cast<uint8_t>(ival);
            const TernaryLogicInfo& info     = TernaryLogicInfo::lookup(control);
            TernaryLogicUseFlags    useFlags = info.GetAllUseFlags();

            assert(useFlags == TernaryLogicUseFlags::C);
#endif // DEBUG

            op2Reg = targetReg;
        }
        else
        {
#if defined(DEBUG)
            if (node->GetHWIntrinsicId() == NI_AVX512_TernaryLogic)
            {
                uint8_t                 control  = static_cast<uint8_t>(ival);
                const TernaryLogicInfo& info     = TernaryLogicInfo::lookup(control);
                TernaryLogicUseFlags    useFlags = info.GetAllUseFlags();

                assert(useFlags == TernaryLogicUseFlags::BC);
            }
#endif // DEBUG
        }
    }

    assert(targetReg != REG_NA);
    assert(op1Reg != REG_NA);
    assert(op2Reg != REG_NA);

    emitter* emit = GetEmitter();

    if (CodeGenInterface::IsEmbeddedBroadcastEnabled(ins, op3))
    {
        instOptions = AddEmbBroadcastMode(instOptions);
    }

    OperandDesc op3Desc = genOperandDesc(ins, op3);

    switch (op3Desc.GetKind())
    {
        case OperandKind::ClsVar:
        {
            emit->emitIns_SIMD_R_R_R_C_I(ins, attr, targetReg, op1Reg, op2Reg, op3Desc.GetFieldHnd(), 0, ival,
                                         instOptions);
            break;
        }

        case OperandKind::Local:
        {
            emit->emitIns_SIMD_R_R_R_S_I(ins, attr, targetReg, op1Reg, op2Reg, op3Desc.GetVarNum(),
                                         op3Desc.GetLclOffset(), ival, instOptions);
            break;
        }

        case OperandKind::Indir:
        {
            // Until we improve the handling of addressing modes in the emitter, we'll create a
            // temporary GT_IND to generate code with.
            GenTreeIndir  indirForm;
            GenTreeIndir* indir = op3Desc.GetIndirForm(&indirForm);
            emit->emitIns_SIMD_R_R_R_A_I(ins, attr, targetReg, op1Reg, op2Reg, indir, ival, instOptions);
        }
        break;

        case OperandKind::Reg:
        {
            emit->emitIns_SIMD_R_R_R_R_I(ins, attr, targetReg, op1Reg, op2Reg, op3Desc.GetReg(), ival, instOptions);
            break;
        }

        default:
            unreached();
    }
}

// genHWIntrinsicJumpTableFallback : generate the jump-table fallback for imm-intrinsics
//                       with non-constant argument
//
// Arguments:
//    intrinsic      - intrinsic ID
//    ins            - the instruction chosen for the intrinsic and base type
//    attr           - the emit attributes for the instruction
//    nonConstImmReg - the register contains non-constant imm8 argument
//    baseReg        - a register for the start of the switch table
//    offsReg        - a register for the offset into the switch table
//    emitSwCase     - the lambda to generate a switch case
//
// Return Value:
//    generate the jump-table fallback for imm-intrinsics with non-constant argument.
// Note:
//    This function can be used for all imm-intrinsics (whether full-range or not),
//    The compiler front-end (i.e. importer) is responsible to insert a range-check IR
//    (GT_BOUNDS_CHECK) for imm8 argument, so this function does not need to do range-check.
//
template <typename HWIntrinsicSwitchCaseBody>
void CodeGen::genHWIntrinsicJumpTableFallback(NamedIntrinsic            intrinsic,
                                              instruction               ins,
                                              emitAttr                  attr,
                                              regNumber                 nonConstImmReg,
                                              regNumber                 baseReg,
                                              regNumber                 offsReg,
                                              HWIntrinsicSwitchCaseBody emitSwCase)
{
    assert(nonConstImmReg != REG_NA);
    // AVX2 Gather intrinsics use managed non-const fallback since they have discrete imm8 value range
    // that does not work with the current compiler generated jump-table fallback
    assert(!HWIntrinsicInfo::isAVX2GatherIntrinsic(intrinsic));
    emitter* emit = GetEmitter();

    unsigned maxByte = (unsigned)HWIntrinsicInfo::lookupImmUpperBound(intrinsic);
    unsigned mask    = 0xFF;

    // Some instructions allow full-range immediates but are documented to ignore ranges of bits
    // or to clamp the value. We can implement the same masking/clamping here in order to reduce
    // the size of the generated code and jump table.

    if (HWIntrinsicInfo::HasFullRangeImm(intrinsic))
    {
        maxByte = GetImmediateMaxAndMask(ins, EA_SIZE(attr), &mask);

        if (mask != 0xFF)
        {
            emit->emitIns_R_I(INS_and, EA_4BYTE, nonConstImmReg, mask);
        }
        else if (maxByte < 255)
        {
            emit->emitIns_R_I(INS_cmp, EA_4BYTE, nonConstImmReg, maxByte);

            BasicBlock* skipLabel = genCreateTempLabel();
            inst_JMP(EJ_jbe, skipLabel);

            instGen_Set_Reg_To_Imm(EA_4BYTE, nonConstImmReg, maxByte);

            genDefineTempLabel(skipLabel);
        }
    }

    assert(maxByte <= 255);
    BasicBlock* jmpTable[256];
    unsigned    jmpTableBase = emit->emitBBTableDataGenBeg(maxByte + 1, true);

    // Emit the jump table
    for (unsigned i = 0; i <= maxByte; i++)
    {
        jmpTable[i] = genCreateTempLabel();
        emit->emitDataGenData(i, jmpTable[i]);
    }

    emit->emitDataGenEnd();

    // Compute and jump to the appropriate offset in the switch table
    emit->emitIns_R_C(INS_lea, emitTypeSize(TYP_I_IMPL), offsReg, compiler->eeFindJitDataOffs(jmpTableBase), 0);

    emit->emitIns_R_ARX(INS_mov, EA_4BYTE, offsReg, offsReg, nonConstImmReg, 4, 0);
    emit->emitIns_R_L(INS_lea, EA_PTR_DSP_RELOC, compiler->fgFirstBB, baseReg);
    emit->emitIns_R_R(INS_add, EA_PTRSIZE, offsReg, baseReg);
    emit->emitIns_R(INS_i_jmp, emitTypeSize(TYP_I_IMPL), offsReg);

    // Emit the switch table entries

    BasicBlock* switchTableBeg = genCreateTempLabel();
    BasicBlock* switchTableEnd = genCreateTempLabel();

    genDefineTempLabel(switchTableBeg);

    for (unsigned i = 0; i <= maxByte; i++)
    {
        genDefineTempLabel(jmpTable[i]);

        if ((i & mask) != i)
        {
            // This is a jump table entry that won't be hit, because the value can't exist after
            // masking. We define the labels for these values in order to pad out the jump table
            // so that the valid entries fall at the correct offsets, but we don't emit any code.
            continue;
        }

        emitSwCase((int8_t)i);
        emit->emitIns_J(INS_jmp, switchTableEnd);
    }

    genDefineTempLabel(switchTableEnd);
}

void CodeGen::genNonTableDrivenHWIntrinsicsJumpTableFallback(GenTreeHWIntrinsic* node, GenTree* lastOp)
{
    NamedIntrinsic      intrinsicId = node->GetHWIntrinsicId();
    HWIntrinsicCategory category    = HWIntrinsicInfo::lookupCategory(intrinsicId);

    assert(HWIntrinsicInfo::IsEmbRoundingCompatible(intrinsicId));
    assert(!lastOp->isContained());
    assert(!HWIntrinsicInfo::genIsTableDrivenHWIntrinsic(intrinsicId, category));

    var_types   baseType   = node->GetSimdBaseType();
    emitAttr    attr       = emitActualTypeSize(Compiler::getSIMDTypeForSize(node->GetSimdSize()));
    var_types   targetType = node->TypeGet();
    instruction ins        = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler);
    regNumber   targetReg  = node->GetRegNum();

    insOpts instOptions = INS_OPTS_NONE;
    switch (intrinsicId)
    {
        case NI_AVX512_ConvertToVector256Int32:
        case NI_AVX512_ConvertToVector256UInt32:
        {
            // This intrinsic has several overloads, only the ones with floating number inputs should reach this part.
            assert(varTypeIsFloating(baseType));
            GenTree* rmOp       = node->Op(1);
            auto     emitSwCase = [&](int8_t i) {
                insOpts newInstOptions = AddEmbRoundingMode(instOptions, i);
                genHWIntrinsic_R_RM(node, ins, attr, targetReg, rmOp, newInstOptions);
            };
            regNumber baseReg = internalRegisters.Extract(node);
            regNumber offsReg = internalRegisters.GetSingle(node);
            genHWIntrinsicJumpTableFallback(intrinsicId, ins, attr, lastOp->GetRegNum(), baseReg, offsReg, emitSwCase);
            break;
        }

        case NI_AVX512_ConvertToInt32:
        case NI_AVX512_ConvertToUInt32:
#if defined(TARGET_AMD64)
        case NI_AVX512_X64_ConvertToInt64:
        case NI_AVX512_X64_ConvertToUInt64:
#endif // TARGET_AMD64
        {
            assert(varTypeIsFloating(baseType));
            attr          = emitTypeSize(targetType);
            GenTree* rmOp = node->Op(1);

            auto emitSwCase = [&](int8_t i) {
                insOpts newInstOptions = AddEmbRoundingMode(instOptions, i);
                genHWIntrinsic_R_RM(node, ins, attr, targetReg, rmOp, newInstOptions);
            };
            regNumber baseReg = internalRegisters.Extract(node);
            regNumber offsReg = internalRegisters.GetSingle(node);
            genHWIntrinsicJumpTableFallback(intrinsicId, ins, attr, lastOp->GetRegNum(), baseReg, offsReg, emitSwCase);
            break;
        }

        case NI_AVX512_X64_ConvertScalarToVector128Single:
        case NI_AVX512_X64_ConvertScalarToVector128Double:
        {
            assert(varTypeIsLong(baseType));
            auto emitSwCase = [&](int8_t i) {
                insOpts newInstOptions = AddEmbRoundingMode(instOptions, i);
                genHWIntrinsic_R_R_RM(node, ins, EA_8BYTE, newInstOptions);
            };
            regNumber baseReg = internalRegisters.Extract(node);
            regNumber offsReg = internalRegisters.GetSingle(node);
            genHWIntrinsicJumpTableFallback(intrinsicId, ins, attr, lastOp->GetRegNum(), baseReg, offsReg, emitSwCase);
            break;
        }

        case NI_AVX512_FusedMultiplyAdd:
        case NI_AVX512_FusedMultiplyAddScalar:
        case NI_AVX512_FusedMultiplyAddNegated:
        case NI_AVX512_FusedMultiplyAddNegatedScalar:
        case NI_AVX512_FusedMultiplyAddSubtract:
        case NI_AVX512_FusedMultiplySubtract:
        case NI_AVX512_FusedMultiplySubtractAdd:
        case NI_AVX512_FusedMultiplySubtractNegated:
        case NI_AVX512_FusedMultiplySubtractNegatedScalar:
        case NI_AVX512_FusedMultiplySubtractScalar:
        {
            // For FMA intrinsics, since it is not possible to get any contained operand in this case: embedded rounding
            // is limited in register-to-register form, and the control byte is dynamic, we don't need to do any swap.
            assert(HWIntrinsicInfo::IsFmaIntrinsic(intrinsicId));

            GenTree* op1 = node->Op(1);
            GenTree* op2 = node->Op(2);
            GenTree* op3 = node->Op(3);

            regNumber op1Reg = op1->GetRegNum();
            regNumber op2Reg = op2->GetRegNum();

            auto emitSwCase = [&](int8_t i) {
                insOpts newInstOptions = AddEmbRoundingMode(instOptions, i);
                genHWIntrinsic_R_R_R_RM(ins, attr, targetReg, op1Reg, op2Reg, op3, newInstOptions);
            };
            regNumber baseReg = internalRegisters.Extract(node);
            regNumber offsReg = internalRegisters.GetSingle(node);
            genHWIntrinsicJumpTableFallback(intrinsicId, ins, attr, lastOp->GetRegNum(), baseReg, offsReg, emitSwCase);
            break;
        }

        default:
            unreached();
            break;
    }
}

//------------------------------------------------------------------------
// genBaseIntrinsic: Generates the code for a base hardware intrinsic node
//
// Arguments:
//    node - The hardware intrinsic node
//
// Note:
//    We currently assume that all base intrinsics have zero or one operand.
//
void CodeGen::genBaseIntrinsic(GenTreeHWIntrinsic* node, insOpts instOptions)
{
    NamedIntrinsic intrinsicId = node->GetHWIntrinsicId();
    regNumber      targetReg   = node->GetRegNum();
    var_types      baseType    = node->GetSimdBaseType();

    assert((baseType >= TYP_BYTE) && (baseType <= TYP_DOUBLE));

    GenTree* op1 = (node->GetOperandCount() >= 1) ? node->Op(1) : nullptr;
    GenTree* op2 = (node->GetOperandCount() >= 2) ? node->Op(2) : nullptr;
    GenTree* op3 = (node->GetOperandCount() >= 3) ? node->Op(3) : nullptr;

    genConsumeMultiOpOperands(node);
    regNumber op1Reg = (op1 == nullptr) ? REG_NA : op1->GetRegNum();

    emitter*    emit     = GetEmitter();
    var_types   simdType = Compiler::getSIMDTypeForSize(node->GetSimdSize());
    emitAttr    attr     = emitActualTypeSize(simdType);
    instruction ins      = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler);

    switch (intrinsicId)
    {
        case NI_Vector128_CreateScalar:
        case NI_Vector256_CreateScalar:
        case NI_Vector512_CreateScalar:
        case NI_Vector128_CreateScalarUnsafe:
        case NI_Vector256_CreateScalarUnsafe:
        case NI_Vector512_CreateScalarUnsafe:
        {
            if (varTypeIsIntegral(baseType))
            {
                emitAttr baseAttr = emitActualTypeSize(baseType);

#if defined(TARGET_X86)
                if (varTypeIsLong(baseType))
                {
                    assert(op1->isContained());

                    if (op1->OperIsLong())
                    {
                        node->SetSimdBaseJitType(CORINFO_TYPE_INT);

                        bool     canCombineLoad = false;
                        GenTree* loPart         = op1->gtGetOp1();
                        GenTree* hiPart         = op1->gtGetOp2();

                        if ((loPart->isContained() && hiPart->isContained()) &&
                            (loPart->OperIs(GT_LCL_FLD) && hiPart->OperIs(GT_LCL_FLD)))
                        {
                            GenTreeLclFld* loFld = loPart->AsLclFld();
                            GenTreeLclFld* hiFld = hiPart->AsLclFld();

                            canCombineLoad = (hiFld->GetLclNum() == loFld->GetLclNum()) &&
                                             (hiFld->GetLclOffs() == (loFld->GetLclOffs() + 4));
                        }

                        if (!canCombineLoad)
                        {
                            if (compiler->compOpportunisticallyDependsOn(InstructionSet_SSE42))
                            {
                                genHWIntrinsic_R_RM(node, ins, baseAttr, targetReg, loPart, instOptions);
                                inst_RV_RV_TT_IV(INS_pinsrd, EA_16BYTE, targetReg, targetReg, hiPart, 0x01,
                                                 !compiler->canUseVexEncoding(), instOptions);
                            }
                            else
                            {
                                regNumber tmpReg = internalRegisters.GetSingle(node);
                                genHWIntrinsic_R_RM(node, ins, baseAttr, targetReg, loPart, instOptions);
                                genHWIntrinsic_R_RM(node, ins, baseAttr, tmpReg, hiPart, instOptions);
                                emit->emitIns_R_R(INS_punpckldq, EA_16BYTE, targetReg, tmpReg, instOptions);
                            }
                            break;
                        }

                        op1 = loPart;
                    }

                    baseAttr = EA_8BYTE;
                }
#endif // TARGET_X86

                if (op1->isUsedFromMemory() && (baseAttr == EA_8BYTE))
                {
                    ins = INS_movq;
                }

                genHWIntrinsic_R_RM(node, ins, baseAttr, targetReg, op1, instOptions);
            }
            else
            {
                assert(varTypeIsFloating(baseType));

                attr = emitTypeSize(baseType);

                if (op1->isContained() || op1->isUsedFromSpillTemp())
                {
                    genHWIntrinsic_R_RM(node, ins, attr, targetReg, op1, instOptions);
                }
                else
                {
                    assert(instOptions == INS_OPTS_NONE);

                    if (HWIntrinsicInfo::IsVectorCreateScalar(intrinsicId))
                    {
                        // If this is CreateScalar, we need to ensure the upper elements are zeroed.
                        // Scalar integer loads and loads from memory always zero the upper elements,
                        // so reg to reg copies of floating types are the only place we need to
                        // do anything different.

                        if (baseType == TYP_FLOAT)
                        {
                            if (compiler->compOpportunisticallyDependsOn(InstructionSet_SSE42))
                            {
                                // insertps imm8 is:
                                //  * Bits 0-3: zmask
                                //  * Bits 4-5: count_d
                                //  * Bits 6-7: count_s (register form only)
                                //
                                // We want zmask 0b1110 (0xE) to zero elements 1/2/3
                                // We want count_d 0b00 (0x0) to insert the value to element 0
                                // We want count_s 0b00 (0x0) as we're just taking element 0 of the source

                                emit->emitIns_SIMD_R_R_R_I(INS_insertps, attr, targetReg, targetReg, op1Reg, 0x0E,
                                                           instOptions);
                            }
                            else
                            {
                                assert(targetReg != op1Reg);
                                emit->emitIns_SIMD_R_R_R(INS_xorps, attr, targetReg, targetReg, targetReg, instOptions);
                                emit->emitIns_Mov(INS_movss, attr, targetReg, op1Reg, /* canSkip */ false);
                            }
                        }
                        else
                        {
                            // `movq xmm xmm` zeroes the upper 64 bits.
                            emit->emitIns_Mov(INS_movq, attr, targetReg, op1Reg, /* canSkip */ false);
                        }
                        break;
                    }

                    // Just use movaps for reg->reg moves as it has zero-latency on modern CPUs
                    emit->emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, /* canSkip */ true);
                }
            }
            break;
        }

        case NI_Vector128_WithElement:
        case NI_Vector256_WithElement:
        case NI_Vector512_WithElement:
        {
            // Optimize the case where op2 is not a constant.

            assert(!op1->isContained());
            assert(!op2->OperIsConst());

            // We don't have an instruction to implement this intrinsic if the index is not a constant.
            // So we will use the SIMD temp location to store the vector, set the value and then reload it.
            // The range check will already have been performed, so at this point we know we have an index
            // within the bounds of the vector.

            unsigned simdInitTempVarNum = compiler->lvaSIMDInitTempVarNum;
            noway_assert(simdInitTempVarNum != BAD_VAR_NUM);

            bool isEBPbased;
            int  offs = compiler->lvaFrameAddress(simdInitTempVarNum, &isEBPbased);

#if !FEATURE_FIXED_OUT_ARGS
            if (!isEBPbased)
            {
                // Adjust the offset by the amount currently pushed on the CPU stack
                offs += genStackLevel;
            }
#else
            assert(genStackLevel == 0);
#endif // !FEATURE_FIXED_OUT_ARGS

            regNumber indexReg = op2->GetRegNum();
            regNumber valueReg = op3->GetRegNum(); // New element value to be stored

            // Store the vector to the temp location.
            GetEmitter()->emitIns_S_R(ins_Store(simdType, compiler->isSIMDTypeLocalAligned(simdInitTempVarNum)),
                                      emitTypeSize(simdType), op1Reg, simdInitTempVarNum, 0);

            // Set the desired element.
            GetEmitter()->emitIns_ARX_R(ins_Store(op3->TypeGet()),        // Store
                                        emitTypeSize(baseType),           // Of the vector baseType
                                        valueReg,                         // From valueReg
                                        (isEBPbased) ? REG_EBP : REG_ESP, // Stack-based
                                        indexReg,                         // Indexed
                                        genTypeSize(baseType),            // by the size of the baseType
                                        offs);                            // Offset

            // Write back the modified vector to the original location.
            GetEmitter()->emitIns_R_S(ins_Load(simdType, compiler->isSIMDTypeLocalAligned(simdInitTempVarNum)),
                                      emitTypeSize(simdType), targetReg, simdInitTempVarNum, 0);
            break;
        }

        case NI_Vector128_GetElement:
        case NI_Vector256_GetElement:
        case NI_Vector512_GetElement:
        {
            assert(instOptions == INS_OPTS_NONE);

            if (simdType == TYP_SIMD12)
            {
                // op1 of TYP_SIMD12 should be considered as TYP_SIMD16
                simdType = TYP_SIMD16;
            }

            // Optimize the case of op1 is in memory and trying to access i'th element.
            if (!op1->isUsedFromReg())
            {
                assert(op1->isContained());

                regNumber baseReg;
                regNumber indexReg;
                int       offset = 0;

                if (op1->OperIsLocal())
                {
                    // There are three parts to the total offset here:
                    // {offset of local} + {offset of vector field (lclFld only)} + {offset of element within vector}.
                    bool     isEBPbased;
                    unsigned varNum = op1->AsLclVarCommon()->GetLclNum();
                    offset += compiler->lvaFrameAddress(varNum, &isEBPbased);

#if !FEATURE_FIXED_OUT_ARGS
                    if (!isEBPbased)
                    {
                        // Adjust the offset by the amount currently pushed on the CPU stack
                        offset += genStackLevel;
                    }
#else
                    assert(genStackLevel == 0);
#endif // !FEATURE_FIXED_OUT_ARGS

                    if (op1->OperIs(GT_LCL_FLD))
                    {
                        offset += op1->AsLclFld()->GetLclOffs();
                    }
                    baseReg = (isEBPbased) ? REG_EBP : REG_ESP;
                }
                else if (op1->IsCnsVec())
                {
                    CORINFO_FIELD_HANDLE hnd =
                        GetEmitter()->emitSimdConst(&op1->AsVecCon()->gtSimdVal, emitTypeSize(op1));

                    baseReg = internalRegisters.GetSingle(node);
                    GetEmitter()->emitIns_R_C(INS_lea, emitTypeSize(TYP_I_IMPL), baseReg, hnd, 0, INS_OPTS_NONE);
                }
                else
                {
                    // Require GT_IND addr to be not contained.
                    assert(op1->OperIs(GT_IND));

                    GenTree* addr = op1->AsIndir()->Addr();
                    assert(!addr->isContained());
                    baseReg = addr->GetRegNum();
                }

                if (op2->OperIsConst())
                {
                    assert(op2->isContained());
                    indexReg = REG_NA;
                    offset += (int)op2->AsIntCon()->IconValue() * genTypeSize(baseType);
                }
                else
                {
                    indexReg = op2->GetRegNum();
                    assert(genIsValidIntReg(indexReg));
                }

                // Now, load the desired element.
                GetEmitter()->emitIns_R_ARX(ins_Move_Extend(baseType, false), // Load
                                            emitTypeSize(baseType),           // Of the vector baseType
                                            targetReg,                        // To targetReg
                                            baseReg,                          // Base Reg
                                            indexReg,                         // Indexed
                                            genTypeSize(baseType),            // by the size of the baseType
                                            offset);
            }
            else if (op2->OperIsConst())
            {
                assert(intrinsicId == NI_Vector128_GetElement);
                assert(varTypeIsFloating(baseType));
                assert(op1Reg != REG_NA);

                ssize_t ival = op2->AsIntCon()->IconValue();

                if (baseType == TYP_FLOAT)
                {
                    if (ival == 1)
                    {
                        if (compiler->compOpportunisticallyDependsOn(InstructionSet_SSE42))
                        {
                            emit->emitIns_R_R(INS_movshdup, attr, targetReg, op1Reg);
                        }
                        else
                        {
                            emit->emitIns_SIMD_R_R_R_I(INS_shufps, attr, targetReg, op1Reg, op1Reg,
                                                       static_cast<int8_t>(0x55), instOptions);
                        }
                    }
                    else if (ival == 2)
                    {
                        emit->emitIns_SIMD_R_R_R(INS_unpckhps, attr, targetReg, op1Reg, op1Reg, instOptions);
                    }
                    else
                    {
                        assert(ival == 3);
                        emit->emitIns_SIMD_R_R_R_I(INS_shufps, attr, targetReg, op1Reg, op1Reg,
                                                   static_cast<int8_t>(0xFF), instOptions);
                    }
                }
                else
                {
                    assert(baseType == TYP_DOUBLE);
                    assert(ival == 1);
                    emit->emitIns_SIMD_R_R_R(INS_unpckhpd, attr, targetReg, op1Reg, op1Reg, instOptions);
                }
            }
            else
            {
                // We don't have an instruction to implement this intrinsic if the index is not a constant.
                // So we will use the SIMD temp location to store the vector, and the load the desired element.
                // The range check will already have been performed, so at this point we know we have an index
                // within the bounds of the vector.

                unsigned simdInitTempVarNum = compiler->lvaSIMDInitTempVarNum;
                noway_assert(simdInitTempVarNum != BAD_VAR_NUM);

                bool isEBPbased;
                int  offs = compiler->lvaFrameAddress(simdInitTempVarNum, &isEBPbased);

#if !FEATURE_FIXED_OUT_ARGS
                if (!isEBPbased)
                {
                    // Adjust the offset by the amount currently pushed on the CPU stack
                    offs += genStackLevel;
                }
#else
                assert(genStackLevel == 0);
#endif // !FEATURE_FIXED_OUT_ARGS

                regNumber indexReg = op2->GetRegNum();

                // Store the vector to the temp location.
                GetEmitter()->emitIns_S_R(ins_Store(simdType, compiler->isSIMDTypeLocalAligned(simdInitTempVarNum)),
                                          emitTypeSize(simdType), op1Reg, simdInitTempVarNum, 0);

                // Now, load the desired element.
                GetEmitter()->emitIns_R_ARX(ins_Move_Extend(baseType, false), // Load
                                            emitTypeSize(baseType),           // Of the vector baseType
                                            targetReg,                        // To targetReg
                                            (isEBPbased) ? REG_EBP : REG_ESP, // Stack-based
                                            indexReg,                         // Indexed
                                            genTypeSize(baseType),            // by the size of the baseType
                                            offs);
            }
            break;
        }

        case NI_Vector128_AsVector128Unsafe:
        case NI_Vector128_AsVector2:
        case NI_Vector128_AsVector3:
        case NI_Vector128_ToScalar:
        case NI_Vector256_ToScalar:
        case NI_Vector512_ToScalar:
        {
            if (op1->isContained() || op1->isUsedFromSpillTemp())
            {
                if (varTypeIsIntegral(baseType))
                {
                    // We just want to emit a standard read from memory
                    ins  = ins_Move_Extend(baseType, false);
                    attr = emitTypeSize(baseType);
                }
                genHWIntrinsic_R_RM(node, ins, attr, targetReg, op1, instOptions);
            }
            else if (varTypeIsIntegral(baseType))
            {
                assert(!varTypeIsLong(baseType) || TargetArchitecture::Is64Bit);
                assert(HWIntrinsicInfo::IsVectorToScalar(intrinsicId));

                attr = emitActualTypeSize(baseType);
                genHWIntrinsic_R_RM(node, ins, attr, targetReg, op1, instOptions);

                if (varTypeIsSmall(baseType))
                {
                    emit->emitIns_Mov(ins_Move_Extend(baseType, /* srcInReg */ true), emitTypeSize(baseType), targetReg,
                                      targetReg, /* canSkip */ false);
                }
            }
            else
            {
                assert(varTypeIsFloating(baseType));
                assert(instOptions == INS_OPTS_NONE);

                // Just use movaps for reg->reg moves as it has zero-latency on modern CPUs
                emit->emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, /* canSkip */ true);
            }
            break;
        }

        case NI_Vector128_ToVector256:
        case NI_Vector128_ToVector512:
        case NI_Vector256_ToVector512:
        {
            // ToVector256 has zero-extend semantics in order to ensure it is deterministic
            // We always emit a move to the target register, even when op1Reg == targetReg,
            // in order to ensure that Bits MAXVL-1:128 are zeroed.

            if (intrinsicId == NI_Vector256_ToVector512)
            {
                attr = emitTypeSize(TYP_SIMD32);
            }
            else
            {
                attr = emitTypeSize(TYP_SIMD16);
            }

            if (op1->isContained() || op1->isUsedFromSpillTemp())
            {
                genHWIntrinsic_R_RM(node, ins, attr, targetReg, op1, instOptions);
            }
            else
            {
                assert(instOptions == INS_OPTS_NONE);

                // Just use movaps for reg->reg moves as it has zero-latency on modern CPUs
                emit->emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, /* canSkip */ false);
            }
            break;
        }

        case NI_Vector128_ToVector256Unsafe:
        case NI_Vector256_ToVector512Unsafe:
        case NI_Vector256_GetLower:
        case NI_Vector512_GetLower:
        case NI_Vector512_GetLower128:
        {
            if (op1->isContained() || op1->isUsedFromSpillTemp())
            {
                // We want to always emit the EA_16BYTE version here.
                //
                // For ToVector256Unsafe the upper bits don't matter and for GetLower we
                // only actually need the lower 16-bytes, so we can just be "more efficient"
                if ((intrinsicId == NI_Vector512_GetLower) || (intrinsicId == NI_Vector256_ToVector512Unsafe))
                {
                    attr = emitTypeSize(TYP_SIMD32);
                }
                else
                {
                    attr = emitTypeSize(TYP_SIMD16);
                }
                genHWIntrinsic_R_RM(node, ins, attr, targetReg, op1, instOptions);
            }
            else
            {
                assert(instOptions == INS_OPTS_NONE);

                // We want to always emit the EA_32BYTE version here.
                //
                // For ToVector256Unsafe the upper bits don't matter and this allows same
                // register moves to be elided. For GetLower we're getting a Vector128 and
                // so the upper bits aren't impactful either allowing the same.

                // Just use movaps for reg->reg moves as it has zero-latency on modern CPUs
                if ((intrinsicId == NI_Vector128_ToVector256Unsafe) || (intrinsicId == NI_Vector256_GetLower))
                {
                    attr = emitTypeSize(TYP_SIMD32);
                }
                else
                {
                    attr = emitTypeSize(TYP_SIMD64);
                }
                emit->emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, /* canSkip */ true);
            }
            break;
        }

        case NI_Vector128_op_Division:
        case NI_Vector256_op_Division:
        {
            // We can emulate SIMD integer division by converting the 32-bit integer -> 64-bit double,
            // perform a 64-bit double divide, then convert back to a 32-bit integer. This is generating
            // something similar to the following managed code:
            //      if (Vector128.EqualsAny(op2, Vector128<int>.Zero))
            //      {
            //          throw new DivideByZeroException();
            //      }
            //
            //      Vector128<int> overflowMask =
            //          Vector128.Equals(op1, Vector128.Create(int.MinValue)
            //          & Vector128.Equals(op2, Vector128.Create(-1));
            //      if (!Vector128.EqualsAll(overflowMask, Vector128<int>.Zero))
            //      {
            //          throw new OverflowException();
            //      }
            //
            //      Vector256<double> op1_f64 =
            //          Vector256.ConvertToDouble(Vector256.WidenLower(Vector128.ToVector256Unsafe(op1))));
            //      Vector256<double> op2_f64 =
            //          Vector256.ConvertToDouble(Vector256.WidenLower(Vector128.ToVector256Unsafe(op2))));
            //      Vector256<double> div_f64 = op1_f64 / op2_f64;
            //      Vector256<long>   div_i64 = Vector256.ConvertToInt64(div_f64);
            //      Vector128<int> div_i32 = Vector256.Narrow(div_i64.GetLower(), div_i64.GetUpper());
            //      return div_i32;
            regNumber op2Reg   = op2->GetRegNum();
            regNumber tmpReg1  = internalRegisters.Extract(node, RBM_ALLFLOAT);
            regNumber tmpReg2  = internalRegisters.Extract(node, RBM_ALLFLOAT);
            emitAttr  typeSize = emitTypeSize(node->TypeGet());
            noway_assert(typeSize == EA_16BYTE || typeSize == EA_32BYTE);
            emitAttr divTypeSize = typeSize == EA_16BYTE ? EA_32BYTE : EA_64BYTE;

            simd_t negOneIntVec = simd_t::AllBitsSet();
            simd_t minValueInt{};
            int    numElements = genTypeSize(node->TypeGet()) / 4;
            for (int i = 0; i < numElements; i++)
            {
                minValueInt.i32[i] = INT_MIN;
            }
            CORINFO_FIELD_HANDLE minValueFld = emit->emitSimdConst(&minValueInt, typeSize);
            CORINFO_FIELD_HANDLE negOneFld   = emit->emitSimdConst(&negOneIntVec, typeSize);

            // div-by-zero check
            emit->emitIns_SIMD_R_R_R(INS_xorpd, typeSize, tmpReg1, tmpReg1, tmpReg1, instOptions);
            emit->emitIns_SIMD_R_R_R(INS_pcmpeqd, typeSize, tmpReg1, tmpReg1, op2Reg, instOptions);
            emit->emitIns_R_R(INS_ptest, typeSize, tmpReg1, tmpReg1, instOptions);
            genJumpToThrowHlpBlk(EJ_jne, SCK_DIV_BY_ZERO);

            // overflow check
            emit->emitIns_SIMD_R_R_C(INS_pcmpeqd, typeSize, tmpReg1, op1Reg, minValueFld, 0, instOptions);
            emit->emitIns_SIMD_R_R_C(INS_pcmpeqd, typeSize, tmpReg2, op2Reg, negOneFld, 0, instOptions);
            emit->emitIns_SIMD_R_R_R(INS_pandd, typeSize, tmpReg1, tmpReg1, tmpReg2, instOptions);
            emit->emitIns_R_R(INS_ptest, typeSize, tmpReg1, tmpReg1, instOptions);
            genJumpToThrowHlpBlk(EJ_jne, SCK_OVERFLOW);

            emit->emitIns_R_R(INS_cvtdq2pd, divTypeSize, tmpReg1, op1Reg, instOptions);
            emit->emitIns_R_R(INS_cvtdq2pd, divTypeSize, tmpReg2, op2Reg, instOptions);
            emit->emitIns_SIMD_R_R_R(INS_divpd, divTypeSize, targetReg, tmpReg1, tmpReg2, instOptions);
            emit->emitIns_R_R(INS_cvttpd2dq, divTypeSize, targetReg, targetReg, instOptions);
            break;
        }

        default:
        {
            unreached();
            break;
        }
    }

    genProduceReg(node);
}

//------------------------------------------------------------------------
// genX86BaseIntrinsic: Generates the code for an X86 base hardware intrinsic node
//
// Arguments:
//    node - The hardware intrinsic node
//
void CodeGen::genX86BaseIntrinsic(GenTreeHWIntrinsic* node, insOpts instOptions)
{
    NamedIntrinsic intrinsicId = node->GetHWIntrinsicId();
    regNumber      targetReg   = node->GetRegNum();
    var_types      targetType  = node->TypeGet();
    var_types      baseType    = node->GetSimdBaseType();
    emitter*       emit        = GetEmitter();

    genConsumeMultiOpOperands(node);

    switch (intrinsicId)
    {
        case NI_X86Base_BitScanForward:
        case NI_X86Base_BitScanReverse:
        case NI_X86Base_X64_BitScanForward:
        case NI_X86Base_X64_BitScanReverse:
        {
            GenTree*    op1 = node->Op(1);
            instruction ins = HWIntrinsicInfo::lookupIns(intrinsicId, targetType, compiler);

            genHWIntrinsic_R_RM(node, ins, emitTypeSize(targetType), targetReg, op1, instOptions);
            break;
        }

        case NI_X86Base_Pause:
        {
            assert(node->GetSimdBaseType() == TYP_UNKNOWN);
            emit->emitIns(INS_pause);
            break;
        }

        case NI_X86Base_DivRem:
        case NI_X86Base_X64_DivRem:
        {
            assert(node->GetOperandCount() == 3);
            assert(instOptions == INS_OPTS_NONE);

            // SIMD base type is from signature and can distinguish signed and unsigned
            targetType = node->GetSimdBaseType();

            GenTree* op1 = node->Op(1);
            GenTree* op2 = node->Op(2);
            GenTree* op3 = node->Op(3);

            instruction ins = HWIntrinsicInfo::lookupIns(intrinsicId, targetType, compiler);

            regNumber op1Reg = op1->GetRegNum();
            regNumber op2Reg = op2->GetRegNum();
            regNumber op3Reg = op3->GetRegNum();

            emitAttr attr = emitTypeSize(targetType);

            // op1: EAX, op2: EDX, op3: free
            assert(op1Reg != REG_EDX);
            assert(op2Reg != REG_EAX);
            if (op3->isUsedFromReg())
            {
                assert(op3Reg != REG_EDX);
                assert(op3Reg != REG_EAX);
            }

            emit->emitIns_Mov(INS_mov, attr, REG_EAX, op1Reg, /* canSkip */ true);
            emit->emitIns_Mov(INS_mov, attr, REG_EDX, op2Reg, /* canSkip */ true);

            // emit the DIV/IDIV instruction
            emit->emitInsBinary(ins, attr, node, op3);

            break;
        }

        case NI_X86Base_X64_ConvertScalarToVector128Double:
        case NI_X86Base_X64_ConvertScalarToVector128Single:
        {
            assert(baseType == TYP_LONG || baseType == TYP_ULONG);
            instruction ins = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler);
            genHWIntrinsic_R_R_RM(node, ins, EA_8BYTE, instOptions);
            break;
        }

        case NI_X86Base_Prefetch0:
        case NI_X86Base_Prefetch1:
        case NI_X86Base_Prefetch2:
        case NI_X86Base_PrefetchNonTemporal:
        {
            assert(baseType == TYP_UBYTE);
            assert(instOptions == INS_OPTS_NONE);

            // These do not support containment.
            assert(!node->Op(1)->isContained());
            instruction ins = HWIntrinsicInfo::lookupIns(intrinsicId, node->GetSimdBaseType(), compiler);
            emit->emitIns_AR(ins, emitTypeSize(baseType), node->Op(1)->GetRegNum(), 0);
            break;
        }

        case NI_X86Base_StoreFence:
        {
            assert(baseType == TYP_UNKNOWN);
            emit->emitIns(INS_sfence);
            break;
        }

        case NI_X86Base_X64_ConvertScalarToVector128Int64:
        case NI_X86Base_X64_ConvertScalarToVector128UInt64:
        case NI_X86Base_ConvertToInt32:
        case NI_X86Base_ConvertToInt32WithTruncation:
        case NI_X86Base_ConvertToUInt32:
        case NI_X86Base_X64_ConvertToInt64:
        case NI_X86Base_X64_ConvertToInt64WithTruncation:
        case NI_X86Base_X64_ConvertToUInt64:
        {
            emitAttr attr;
            if (varTypeIsIntegral(baseType))
            {
                assert(baseType == TYP_INT || baseType == TYP_UINT || baseType == TYP_LONG || baseType == TYP_ULONG);
                attr = emitActualTypeSize(baseType);
            }
            else
            {
                assert(baseType == TYP_DOUBLE || baseType == TYP_FLOAT);
                attr = emitTypeSize(targetType);
            }

            instruction ins = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler);
            genHWIntrinsic_R_RM(node, ins, attr, targetReg, node->Op(1), instOptions);
            break;
        }

        case NI_X86Base_LoadFence:
        {
            assert(baseType == TYP_UNKNOWN);
            emit->emitIns(INS_lfence);
            break;
        }

        case NI_X86Base_MemoryFence:
        {
            assert(baseType == TYP_UNKNOWN);
            emit->emitIns(INS_mfence);
            break;
        }

        case NI_X86Base_StoreNonTemporal:
        case NI_X86Base_X64_StoreNonTemporal:
        {
            assert(baseType == TYP_INT || baseType == TYP_UINT || baseType == TYP_LONG || baseType == TYP_ULONG);
            instruction     ins   = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler);
            GenTreeStoreInd store = storeIndirForm(node->TypeGet(), node->Op(1), node->Op(2));
            emit->emitInsStoreInd(ins, emitTypeSize(baseType), &store);
            break;
        }

        default:
            unreached();
            break;
    }

    genProduceReg(node);
}

//------------------------------------------------------------------------
// genSse42Intrinsic: Generates the code for an SSE4.2 hardware intrinsic node
//
// Arguments:
//    node - The hardware intrinsic node
//
void CodeGen::genSse42Intrinsic(GenTreeHWIntrinsic* node, insOpts instOptions)
{
    NamedIntrinsic intrinsicId = node->GetHWIntrinsicId();
    regNumber      targetReg   = node->GetRegNum();
    GenTree*       op1         = node->Op(1);
    var_types      baseType    = node->GetSimdBaseType();
    var_types      targetType  = node->TypeGet();
    emitter*       emit        = GetEmitter();

    assert(targetReg != REG_NA);
    assert(!node->OperIsCommutative());

    genConsumeMultiOpOperands(node);

    switch (intrinsicId)
    {
        case NI_SSE42_ConvertToVector128Int16:
        case NI_SSE42_ConvertToVector128Int32:
        case NI_SSE42_ConvertToVector128Int64:
        {
            instruction ins = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler);

            if (!varTypeIsSIMD(op1->TypeGet()))
            {
                // Until we improve the handling of addressing modes in the emitter, we'll create a
                // temporary GT_IND to generate code with.
                GenTreeIndir load = indirForm(targetType, op1);
                emit->emitInsLoadInd(ins, EA_16BYTE, targetReg, &load);
            }
            else
            {
                genHWIntrinsic_R_RM(node, ins, EA_16BYTE, targetReg, op1, instOptions);
            }
            break;
        }

        case NI_SSE42_Crc32:
        case NI_SSE42_X64_Crc32:
        {
            assert(instOptions == INS_OPTS_NONE);

            instruction ins    = INS_crc32;
            regNumber   op1Reg = op1->GetRegNum();
            GenTree*    op2    = node->Op(2);

            assert(!op2->isUsedFromReg() || (op2->GetRegNum() != targetReg) || (op1Reg == targetReg));
            emit->emitIns_Mov(INS_mov, emitTypeSize(targetType), targetReg, op1Reg, /* canSkip */ true);

#ifdef TARGET_AMD64
            bool needsEvex = false;
            if (emit->IsExtendedGPReg(targetReg))
            {
                needsEvex = true;
            }
            else if (op2->isUsedFromReg() && emit->IsExtendedGPReg(op2->GetRegNum()))
            {
                needsEvex = true;
            }
            else if (op2->isIndir())
            {
                GenTreeIndir* indir = op2->AsIndir();

                // We don't need to check if they are actually enregistered.
                if (indir->HasBase() && emit->IsExtendedGPReg(indir->Base()->GetRegNum()))
                {
                    needsEvex = true;
                }

                if (indir->HasIndex() && emit->IsExtendedGPReg(indir->Index()->GetRegNum()))
                {
                    needsEvex = true;
                }
            }

            if (needsEvex)
            {
                ins = INS_crc32_apx;
            }
#endif // TARGET_AMD64

            if ((baseType == TYP_UBYTE) || (baseType == TYP_USHORT)) // baseType is the type of the second argument
            {
                assert(targetType == TYP_INT);
                genHWIntrinsic_R_RM(node, ins, emitTypeSize(baseType), targetReg, op2, instOptions);
            }
            else
            {
                assert((targetType == TYP_INT) || (targetType == TYP_LONG));
                genHWIntrinsic_R_RM(node, ins, emitTypeSize(targetType), targetReg, op2, instOptions);
            }
            break;
        }

        case NI_SSE42_Extract:
        case NI_SSE42_X64_Extract:
        {
            assert(!varTypeIsFloating(baseType));

            instruction ins  = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler);
            GenTree*    op2  = node->Op(2);
            emitAttr    attr = emitActualTypeSize(targetType);

            auto emitSwCase = [&](int8_t i) {
                inst_RV_TT_IV(ins, attr, targetReg, op1, i, instOptions);
            };

            if (op2->IsCnsIntOrI())
            {
                ssize_t ival = op2->AsIntCon()->IconValue();
                assert((ival >= 0) && (ival <= 255));
                emitSwCase((int8_t)ival);
            }
            else
            {
                // We emit a fallback case for the scenario when the imm-op is not a constant. This should
                // normally happen when the intrinsic is called indirectly, such as via Reflection. However, it
                // can also occur if the consumer calls it directly and just doesn't pass a constant value.
                regNumber baseReg = internalRegisters.Extract(node);
                regNumber offsReg = internalRegisters.GetSingle(node);
                genHWIntrinsicJumpTableFallback(intrinsicId, ins, EA_16BYTE, op2->GetRegNum(), baseReg, offsReg,
                                                emitSwCase);
            }
            break;
        }

        case NI_SSE42_PopCount:
        case NI_SSE42_X64_PopCount:
        {
            genXCNTIntrinsic(node, INS_popcnt);
            break;
        }

        default:
        {
            unreached();
            break;
        }
    }

    genProduceReg(node);
}

//------------------------------------------------------------------------
// genAvxFamilyIntrinsic: Generates the code for an AVX/AVX2/AVX512 hardware intrinsic node
//
// Arguments:
//    node        - The hardware intrinsic node
//    instOptions - The options used to when generating the instruction.
//
void CodeGen::genAvxFamilyIntrinsic(GenTreeHWIntrinsic* node, insOpts instOptions)
{
    NamedIntrinsic intrinsicId = node->GetHWIntrinsicId();

    if (HWIntrinsicInfo::IsFmaIntrinsic(intrinsicId))
    {
        genFmaIntrinsic(node, instOptions);
        return;
    }

    if (HWIntrinsicInfo::IsPermuteVar2x(intrinsicId))
    {
        genPermuteVar2x(node, instOptions);
        return;
    }

    var_types baseType   = node->GetSimdBaseType();
    var_types targetType = node->TypeGet();
    emitAttr  attr       = EA_UNKNOWN;

    if (baseType == TYP_UNKNOWN)
    {
        baseType = targetType;
        attr     = emitTypeSize(targetType);
    }
    else
    {
        attr = emitActualTypeSize(Compiler::getSIMDTypeForSize(node->GetSimdSize()));
    }

    instruction ins       = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler);
    size_t      numArgs   = node->GetOperandCount();
    GenTree*    op1       = node->Op(1);
    regNumber   op1Reg    = REG_NA;
    regNumber   targetReg = node->GetRegNum();
    emitter*    emit      = GetEmitter();

    genConsumeMultiOpOperands(node);

    switch (intrinsicId)
    {
        case NI_AVX2_AndNotScalar:
        case NI_AVX2_X64_AndNot:
        case NI_AVX2_BitFieldExtract:
        case NI_AVX2_X64_BitFieldExtract:
        case NI_AVX2_ParallelBitDeposit:
        case NI_AVX2_ParallelBitExtract:
        case NI_AVX2_X64_ParallelBitDeposit:
        case NI_AVX2_X64_ParallelBitExtract:
        case NI_AVX2_ZeroHighBits:
        case NI_AVX2_X64_ZeroHighBits:
        {
            assert((targetType == TYP_INT) || (targetType == TYP_LONG));
            genHWIntrinsic_R_R_RM(node, ins, attr, instOptions);
            break;
        }

        case NI_AVX2_ConvertToInt32:
        case NI_AVX2_ConvertToUInt32:
        {
            assert(instOptions == INS_OPTS_NONE);

            op1Reg = op1->GetRegNum();
            assert((baseType == TYP_INT) || (baseType == TYP_UINT));
            instruction ins = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler);
            emit->emitIns_Mov(ins, emitActualTypeSize(baseType), targetReg, op1Reg, /* canSkip */ false);
            break;
        }

        case NI_AVX2_ConvertToVector256Int16:
        case NI_AVX2_ConvertToVector256Int32:
        case NI_AVX2_ConvertToVector256Int64:
        {
            instruction ins = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler);

            if (!varTypeIsSIMD(op1->gtType))
            {
                // Until we improve the handling of addressing modes in the emitter, we'll create a
                // temporary GT_IND to generate code with.
                GenTreeIndir load = indirForm(targetType, op1);
                emit->emitInsLoadInd(ins, emitTypeSize(TYP_SIMD32), node->GetRegNum(), &load);
            }
            else
            {
                genHWIntrinsic_R_RM(node, ins, EA_32BYTE, targetReg, op1, instOptions);
            }
            break;
        }

        case NI_AVX2_ExtractLowestSetBit:
        case NI_AVX2_GetMaskUpToLowestSetBit:
        case NI_AVX2_ResetLowestSetBit:
        case NI_AVX2_X64_ExtractLowestSetBit:
        case NI_AVX2_X64_GetMaskUpToLowestSetBit:
        case NI_AVX2_X64_ResetLowestSetBit:
        {
            assert((targetType == TYP_INT) || (targetType == TYP_LONG));
            genHWIntrinsic_R_RM(node, ins, attr, targetReg, node->Op(1), instOptions);
            break;
        }

        case NI_AVX2_GatherVector128:
        case NI_AVX2_GatherVector256:
        case NI_AVX2_GatherMaskVector128:
        case NI_AVX2_GatherMaskVector256:
        {
            assert(instOptions == INS_OPTS_NONE);

            GenTree* op2     = node->Op(2);
            GenTree* op3     = node->Op(3);
            GenTree* lastOp  = nullptr;
            GenTree* indexOp = nullptr;

            op1Reg                 = op1->GetRegNum();
            regNumber op2Reg       = op2->GetRegNum();
            regNumber addrBaseReg  = REG_NA;
            regNumber addrIndexReg = REG_NA;
            regNumber maskReg      = internalRegisters.Extract(node, RBM_ALLFLOAT);

            if (numArgs == 5)
            {
                assert(intrinsicId == NI_AVX2_GatherMaskVector128 || intrinsicId == NI_AVX2_GatherMaskVector256);

                GenTree* op4 = node->Op(4);
                lastOp       = node->Op(5);

                regNumber op3Reg = op3->GetRegNum();
                regNumber op4Reg = op4->GetRegNum();

                addrBaseReg  = op2Reg;
                addrIndexReg = op3Reg;
                indexOp      = op3;

                // copy op4Reg into the tmp mask register,
                // the mask register will be cleared by gather instructions
                emit->emitIns_Mov(INS_movaps, attr, maskReg, op4Reg, /* canSkip */ false);

                // copy source vector to the target register for masking merge
                emit->emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, /* canSkip */ true);
            }
            else
            {
                assert(intrinsicId == NI_AVX2_GatherVector128 || intrinsicId == NI_AVX2_GatherVector256);
                addrBaseReg  = op1Reg;
                addrIndexReg = op2Reg;
                indexOp      = op2;
                lastOp       = op3;

                // generate all-one mask vector
                assert(!emitter::isHighSimdReg(targetReg));
                emit->emitIns_SIMD_R_R_R(INS_pcmpeqd, attr, maskReg, maskReg, maskReg, instOptions);
            }

            bool isVector128GatherWithVector256Index = (targetType == TYP_SIMD16) && indexOp->TypeIs(TYP_SIMD32);

            // hwintrinsiclistxarch.h uses Dword index instructions in default
            if (varTypeIsLong(node->GetAuxiliaryType()))
            {
                switch (ins)
                {
                    case INS_vpgatherdd:
                        ins = INS_vpgatherqd;
                        if (isVector128GatherWithVector256Index)
                        {
                            // YMM index in address mode
                            attr = emitTypeSize(TYP_SIMD32);
                        }
                        break;
                    case INS_vpgatherdq:
                        ins = INS_vpgatherqq;
                        break;
                    case INS_vgatherdps:
                        ins = INS_vgatherqps;
                        if (isVector128GatherWithVector256Index)
                        {
                            // YMM index in address mode
                            attr = emitTypeSize(TYP_SIMD32);
                        }
                        break;
                    case INS_vgatherdpd:
                        ins = INS_vgatherqpd;
                        break;
                    default:
                        unreached();
                }
            }

            assert(lastOp->IsCnsIntOrI());
            ssize_t ival = lastOp->AsIntCon()->IconValue();
            assert((ival >= 0) && (ival <= 255));

            assert(targetReg != maskReg);
            assert(targetReg != addrIndexReg);
            assert(maskReg != addrIndexReg);
            emit->emitIns_R_AR_R(ins, attr, targetReg, maskReg, addrBaseReg, addrIndexReg, (int8_t)ival, 0);

            break;
        }

        case NI_AVX2_LeadingZeroCount:
        case NI_AVX2_TrailingZeroCount:
        case NI_AVX2_X64_LeadingZeroCount:
        case NI_AVX2_X64_TrailingZeroCount:
        {
            assert((targetType == TYP_INT) || (targetType == TYP_LONG));
            genXCNTIntrinsic(node, ins);
            break;
        }

        case NI_AVX2_MultiplyNoFlags:
        case NI_AVX2_X64_MultiplyNoFlags:
        {

            assert(instOptions == INS_OPTS_NONE);

            size_t numArgs = node->GetOperandCount();
            assert(numArgs == 2 || numArgs == 3);

            GenTree* op1 = node->Op(1);
            GenTree* op2 = node->Op(2);

            regNumber op1Reg = op1->GetRegNum();
            regNumber op2Reg = op2->GetRegNum();
            regNumber op3Reg = REG_NA;
            regNumber lowReg = REG_NA;

            if (numArgs == 2)
            {
                lowReg = targetReg;
            }
            else
            {
                op3Reg = node->Op(3)->GetRegNum();

                assert(!node->Op(3)->isContained());
                assert(op3Reg != op1Reg);
                assert(op3Reg != targetReg);
                assert(op3Reg != REG_EDX);
                lowReg = internalRegisters.GetSingle(node);
                assert(op3Reg != lowReg);
                assert(lowReg != targetReg);
            }

            // These do not support containment
            assert(!op2->isContained());
            emitAttr attr = emitTypeSize(targetType);

            // mov the first operand into implicit source operand EDX/RDX
            assert((op2Reg != REG_EDX) || (op1Reg == REG_EDX));
            emit->emitIns_Mov(INS_mov, attr, REG_EDX, op1Reg, /* canSkip */ true);

            // generate code for MULX
            assert(!node->isRMWHWIntrinsic(compiler));
            inst_RV_RV_TT(ins, attr, targetReg, lowReg, op2, false, INS_OPTS_NONE);

            // If requires the lower half result, store in the memory pointed to by op3
            if (numArgs == 3)
            {
                emit->emitIns_AR_R(INS_mov, attr, lowReg, op3Reg, 0);
            }
            break;
        }

        case NI_AVX512_AddMask:
        {
            assert(instOptions == INS_OPTS_NONE);

            uint32_t simdSize = node->GetSimdSize();
            uint32_t count    = simdSize / genTypeSize(baseType);

            if (count <= 8)
            {
                assert((count == 2) || (count == 4) || (count == 8));
                ins = INS_kaddb;
            }
            else if (count == 16)
            {
                ins = INS_kaddw;
            }
            else if (count == 32)
            {
                ins = INS_kaddd;
            }
            else
            {
                assert(count == 64);
                ins = INS_kaddq;
            }

            op1Reg = op1->GetRegNum();

            GenTree*  op2    = node->Op(2);
            regNumber op2Reg = op2->GetRegNum();

            assert(emitter::isMaskReg(targetReg));
            assert(emitter::isMaskReg(op1Reg));
            assert(emitter::isMaskReg(op2Reg));

            // Use EA_32BYTE to ensure the VEX.L bit gets set
            emit->emitIns_R_R_R(ins, EA_32BYTE, targetReg, op1Reg, op2Reg);
            break;
        }

        case NI_AVX512_AndMask:
        {
            assert(instOptions == INS_OPTS_NONE);

            uint32_t simdSize = node->GetSimdSize();
            uint32_t count    = simdSize / genTypeSize(baseType);

            if (count <= 8)
            {
                assert((count == 2) || (count == 4) || (count == 8));
                ins = INS_kandb;
            }
            else if (count == 16)
            {
                ins = INS_kandw;
            }
            else if (count == 32)
            {
                ins = INS_kandd;
            }
            else
            {
                assert(count == 64);
                ins = INS_kandq;
            }

            op1Reg = op1->GetRegNum();

            GenTree*  op2    = node->Op(2);
            regNumber op2Reg = op2->GetRegNum();

            assert(emitter::isMaskReg(targetReg));
            assert(emitter::isMaskReg(op1Reg));
            assert(emitter::isMaskReg(op2Reg));

            // Use EA_32BYTE to ensure the VEX.L bit gets set
            emit->emitIns_R_R_R(ins, EA_32BYTE, targetReg, op1Reg, op2Reg);
            break;
        }

        case NI_AVX512_AndNotMask:
        {
            assert(instOptions == INS_OPTS_NONE);

            uint32_t simdSize = node->GetSimdSize();
            uint32_t count    = simdSize / genTypeSize(baseType);

            if (count <= 8)
            {
                assert((count == 2) || (count == 4) || (count == 8));
                ins = INS_kandnb;
            }
            else if (count == 16)
            {
                ins = INS_kandnw;
            }
            else if (count == 32)
            {
                ins = INS_kandnd;
            }
            else
            {
                assert(count == 64);
                ins = INS_kandnq;
            }

            op1Reg = op1->GetRegNum();

            GenTree*  op2    = node->Op(2);
            regNumber op2Reg = op2->GetRegNum();

            assert(emitter::isMaskReg(targetReg));
            assert(emitter::isMaskReg(op1Reg));
            assert(emitter::isMaskReg(op2Reg));

            // Use EA_32BYTE to ensure the VEX.L bit gets set
            emit->emitIns_R_R_R(ins, EA_32BYTE, targetReg, op1Reg, op2Reg);
            break;
        }

        case NI_AVX512_MoveMask:
        {
            assert(instOptions == INS_OPTS_NONE);

            uint32_t simdSize = node->GetSimdSize();
            uint32_t count    = simdSize / genTypeSize(baseType);

            if (count <= 8)
            {
                assert((count == 2) || (count == 4) || (count == 8));
                ins  = INS_kmovb_gpr;
                attr = EA_4BYTE;
            }
            else if (count == 16)
            {
                ins  = INS_kmovw_gpr;
                attr = EA_4BYTE;
            }
            else if (count == 32)
            {
                ins  = INS_kmovd_gpr;
                attr = EA_4BYTE;
            }
            else
            {
                assert(count == 64);
                ins  = INS_kmovq_gpr;
                attr = EA_8BYTE;
            }

            op1Reg = op1->GetRegNum();
            assert(emitter::isMaskReg(op1Reg));

            emit->emitIns_Mov(ins, attr, targetReg, op1Reg, INS_FLAGS_DONT_CARE);
            break;
        }

        case NI_AVX512_KORTEST:
        {
            assert(instOptions == INS_OPTS_NONE);

            uint32_t simdSize = node->GetSimdSize();
            uint32_t count    = simdSize / genTypeSize(baseType);

            if (count <= 8)
            {
                assert((count == 2) || (count == 4) || (count == 8));
                ins = INS_kortestb;
            }
            else if (count == 16)
            {
                ins = INS_kortestw;
            }
            else if (count == 32)
            {
                ins = INS_kortestd;
            }
            else
            {
                assert(count == 64);
                ins = INS_kortestq;
            }

            op1Reg           = op1->GetRegNum();
            regNumber op2Reg = op1Reg;

            if (node->GetOperandCount() == 2)
            {
                GenTree* op2 = node->Op(2);
                op2Reg       = op2->GetRegNum();
            }

            assert(emitter::isMaskReg(op1Reg));
            assert(emitter::isMaskReg(op2Reg));

            emit->emitIns_R_R(ins, EA_8BYTE, op1Reg, op1Reg);
            break;
        }

        case NI_AVX512_KTEST:
        {
            assert(instOptions == INS_OPTS_NONE);

            uint32_t simdSize = node->GetSimdSize();
            uint32_t count    = simdSize / genTypeSize(baseType);

            if (count <= 8)
            {
                assert((count == 2) || (count == 4) || (count == 8));
                ins = INS_ktestb;
            }
            else if (count == 16)
            {
                ins = INS_ktestw;
            }
            else if (count == 32)
            {
                ins = INS_ktestd;
            }
            else
            {
                assert(count == 64);
                ins = INS_ktestq;
            }

            op1Reg = op1->GetRegNum();

            GenTree*  op2    = node->Op(2);
            regNumber op2Reg = op2->GetRegNum();

            assert(emitter::isMaskReg(op1Reg));
            assert(emitter::isMaskReg(op2Reg));

            emit->emitIns_R_R(ins, EA_8BYTE, op1Reg, op1Reg);
            break;
        }

        case NI_AVX512_NotMask:
        {
            assert(instOptions == INS_OPTS_NONE);

            uint32_t simdSize = node->GetSimdSize();
            uint32_t count    = simdSize / genTypeSize(baseType);

            if (count <= 8)
            {
                assert((count == 2) || (count == 4) || (count == 8));
                ins = INS_knotb;
            }
            else if (count == 16)
            {
                ins = INS_knotw;
            }
            else if (count == 32)
            {
                ins = INS_knotd;
            }
            else
            {
                assert(count == 64);
                ins = INS_knotq;
            }

            op1Reg = op1->GetRegNum();

            assert(emitter::isMaskReg(targetReg));
            assert(emitter::isMaskReg(op1Reg));

            emit->emitIns_R_R(ins, EA_8BYTE, targetReg, op1Reg);
            break;
        }

        case NI_AVX512_OrMask:
        {
            assert(instOptions == INS_OPTS_NONE);

            uint32_t simdSize = node->GetSimdSize();
            uint32_t count    = simdSize / genTypeSize(baseType);

            if (count <= 8)
            {
                assert((count == 2) || (count == 4) || (count == 8));
                ins = INS_korb;
            }
            else if (count == 16)
            {
                ins = INS_korw;
            }
            else if (count == 32)
            {
                ins = INS_kord;
            }
            else
            {
                assert(count == 64);
                ins = INS_korq;
            }

            op1Reg = op1->GetRegNum();

            GenTree*  op2    = node->Op(2);
            regNumber op2Reg = op2->GetRegNum();

            assert(emitter::isMaskReg(targetReg));
            assert(emitter::isMaskReg(op1Reg));
            assert(emitter::isMaskReg(op2Reg));

            // Use EA_32BYTE to ensure the VEX.L bit gets set
            emit->emitIns_R_R_R(ins, EA_32BYTE, targetReg, op1Reg, op2Reg);
            break;
        }

        case NI_AVX512_ShiftLeftMask:
        {
            assert(instOptions == INS_OPTS_NONE);

            uint32_t simdSize = node->GetSimdSize();
            uint32_t count    = simdSize / genTypeSize(baseType);

            if (count <= 8)
            {
                assert((count == 2) || (count == 4) || (count == 8));
                ins = INS_kshiftlb;
            }
            else if (count == 16)
            {
                ins = INS_kshiftlw;
            }
            else if (count == 32)
            {
                ins = INS_kshiftld;
            }
            else
            {
                assert(count == 64);
                ins = INS_kshiftlq;
            }

            op1Reg = op1->GetRegNum();

            GenTree* op2 = node->Op(2);
            assert(op2->IsCnsIntOrI() && op2->isContained());

            assert(emitter::isMaskReg(targetReg));
            assert(emitter::isMaskReg(op1Reg));

            ssize_t ival = op2->AsIntCon()->IconValue();
            assert((ival >= 0) && (ival <= 255));

            emit->emitIns_R_R_I(ins, EA_8BYTE, targetReg, op1Reg, (int8_t)ival);
            break;
        }

        case NI_AVX512_ShiftRightMask:
        {
            assert(instOptions == INS_OPTS_NONE);

            uint32_t simdSize = node->GetSimdSize();
            uint32_t count    = simdSize / genTypeSize(baseType);

            if (count <= 8)
            {
                assert((count == 2) || (count == 4) || (count == 8));
                ins = INS_kshiftrb;
            }
            else if (count == 16)
            {
                ins = INS_kshiftrw;
            }
            else if (count == 32)
            {
                ins = INS_kshiftrd;
            }
            else
            {
                assert(count == 64);
                ins = INS_kshiftrq;
            }

            op1Reg = op1->GetRegNum();

            GenTree* op2 = node->Op(2);
            assert(op2->IsCnsIntOrI() && op2->isContained());

            assert(emitter::isMaskReg(targetReg));
            assert(emitter::isMaskReg(op1Reg));

            ssize_t ival = op2->AsIntCon()->IconValue();
            assert((ival >= 0) && (ival <= 255));

            emit->emitIns_R_R_I(ins, EA_8BYTE, targetReg, op1Reg, (int8_t)ival);
            break;
        }

        case NI_AVX512_XorMask:
        {
            assert(instOptions == INS_OPTS_NONE);

            uint32_t simdSize = node->GetSimdSize();
            uint32_t count    = simdSize / genTypeSize(baseType);

            if (count <= 8)
            {
                assert((count == 2) || (count == 4) || (count == 8));
                ins = INS_kxorb;
            }
            else if (count == 16)
            {
                ins = INS_kxorw;
            }
            else if (count == 32)
            {
                ins = INS_kxord;
            }
            else
            {
                assert(count == 64);
                ins = INS_kxorq;
            }

            op1Reg = op1->GetRegNum();

            GenTree*  op2    = node->Op(2);
            regNumber op2Reg = op2->GetRegNum();

            assert(emitter::isMaskReg(targetReg));
            assert(emitter::isMaskReg(op1Reg));
            assert(emitter::isMaskReg(op2Reg));

            // Use EA_32BYTE to ensure the VEX.L bit gets set
            emit->emitIns_R_R_R(ins, EA_32BYTE, targetReg, op1Reg, op2Reg);
            break;
        }

        case NI_AVX512_XnorMask:
        {
            assert(instOptions == INS_OPTS_NONE);

            uint32_t simdSize = node->GetSimdSize();
            uint32_t count    = simdSize / genTypeSize(baseType);

            if (count <= 8)
            {
                assert((count == 2) || (count == 4) || (count == 8));
                ins = INS_kxnorb;
            }
            else if (count == 16)
            {
                ins = INS_kxnorw;
            }
            else if (count == 32)
            {
                ins = INS_kxnord;
            }
            else
            {
                assert(count == 64);
                ins = INS_kxnorq;
            }

            op1Reg = op1->GetRegNum();

            GenTree*  op2    = node->Op(2);
            regNumber op2Reg = op2->GetRegNum();

            assert(emitter::isMaskReg(targetReg));
            assert(emitter::isMaskReg(op1Reg));
            assert(emitter::isMaskReg(op2Reg));

            // Use EA_32BYTE to ensure the VEX.L bit gets set
            emit->emitIns_R_R_R(ins, EA_32BYTE, targetReg, op1Reg, op2Reg);
            break;
        }

        case NI_AVX512_ConvertToInt32:
        case NI_AVX512_ConvertToUInt32:
        case NI_AVX512_ConvertToUInt32WithTruncation:
        case NI_AVX512_X64_ConvertToInt64:
        case NI_AVX512_X64_ConvertToUInt64:
        case NI_AVX512_X64_ConvertToUInt64WithTruncation:
        {
            assert(baseType == TYP_DOUBLE || baseType == TYP_FLOAT);
            emitAttr attr = emitTypeSize(targetType);

            instruction ins = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler);
            genHWIntrinsic_R_RM(node, ins, attr, targetReg, op1, instOptions);
            break;
        }

        case NI_AVX512_ConvertToVector128UInt32:
        case NI_AVX512_ConvertToVector128UInt32WithSaturation:
        case NI_AVX512_ConvertToVector256Int32:
        case NI_AVX512_ConvertToVector256UInt32:
        {
            if (varTypeIsFloating(baseType))
            {
                instruction ins = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler);
                genHWIntrinsic_R_RM(node, ins, attr, targetReg, op1, instOptions);
                break;
            }
            FALLTHROUGH;
        }

        case NI_AVX512_ConvertToVector128Byte:
        case NI_AVX512_ConvertToVector128ByteWithSaturation:
        case NI_AVX512_ConvertToVector128Int16:
        case NI_AVX512_ConvertToVector128Int16WithSaturation:
        case NI_AVX512_ConvertToVector128Int32:
        case NI_AVX512_ConvertToVector128Int32WithSaturation:
        case NI_AVX512_ConvertToVector128SByte:
        case NI_AVX512_ConvertToVector128SByteWithSaturation:
        case NI_AVX512_ConvertToVector128UInt16:
        case NI_AVX512_ConvertToVector128UInt16WithSaturation:
        case NI_AVX512_ConvertToVector256Byte:
        case NI_AVX512_ConvertToVector256ByteWithSaturation:
        case NI_AVX512_ConvertToVector256Int16:
        case NI_AVX512_ConvertToVector256Int16WithSaturation:
        case NI_AVX512_ConvertToVector256Int32WithSaturation:
        case NI_AVX512_ConvertToVector256SByte:
        case NI_AVX512_ConvertToVector256SByteWithSaturation:
        case NI_AVX512_ConvertToVector256UInt16:
        case NI_AVX512_ConvertToVector256UInt16WithSaturation:
        case NI_AVX512_ConvertToVector256UInt32WithSaturation:
        {
            instruction ins = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler);

            // These instructions are RM_R and so we need to ensure the targetReg
            // is passed in as the RM register and op1 is passed as the R register

            op1Reg = op1->GetRegNum();
            emit->emitIns_R_R(ins, attr, op1Reg, targetReg, instOptions);
            break;
        }

        case NI_AVX512_X64_ConvertScalarToVector128Double:
        case NI_AVX512_X64_ConvertScalarToVector128Single:
        {
            assert(baseType == TYP_ULONG || baseType == TYP_LONG);
            instruction ins = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler);
            genHWIntrinsic_R_R_RM(node, ins, EA_8BYTE, instOptions);
            break;
        }

        case NI_AVXVNNIINT_MultiplyWideningAndAddSaturate:
        case NI_AVXVNNIINT_V512_MultiplyWideningAndAddSaturate:
        {
            GenTree* op2 = node->Op(2);
            GenTree* op3 = node->Op(3);

            op1Reg           = op1->GetRegNum();
            regNumber op2Reg = op2->GetRegNum();
            assert(targetReg != REG_NA);
            assert(op1Reg != REG_NA);
            assert(op2Reg != REG_NA);

            var_types op3Type = node->GetAuxiliaryType();
            switch (baseType)
            {
                case TYP_UBYTE:
                {
                    ins = INS_vpdpbuuds;
                    break;
                }

                case TYP_BYTE:
                {
                    switch (op3Type)
                    {
                        case TYP_UBYTE:
                        {
                            ins = INS_vpdpbsuds;
                            break;
                        }

                        case TYP_BYTE:
                        {
                            ins = INS_vpdpbssds;
                            break;
                        }

                        default:
                        {
                            unreached();
                        }
                    }
                    break;
                }

                case TYP_SHORT:
                {
                    ins = INS_vpdpwsuds;
                    break;
                }

                case TYP_USHORT:
                {
                    switch (op3Type)
                    {
                        case TYP_USHORT:
                        {
                            ins = INS_vpdpwuuds;
                            break;
                        }

                        case TYP_SHORT:
                        {
                            ins = INS_vpdpwusds;
                            break;
                        }

                        default:
                        {
                            unreached();
                        }
                    }
                    break;
                }

                default:
                {
                    unreached();
                }
            }

            genHWIntrinsic_R_R_R_RM(ins, attr, targetReg, op1Reg, op2Reg, op3, instOptions);
            break;
        }

        case NI_AVXVNNIINT_MultiplyWideningAndAdd:
        case NI_AVXVNNIINT_V512_MultiplyWideningAndAdd:
        {
            GenTree* op2 = node->Op(2);
            GenTree* op3 = node->Op(3);

            op1Reg           = op1->GetRegNum();
            regNumber op2Reg = op2->GetRegNum();
            assert(targetReg != REG_NA);
            assert(op1Reg != REG_NA);
            assert(op2Reg != REG_NA);

            var_types op3Type = node->GetAuxiliaryType();
            switch (baseType)
            {
                case TYP_UBYTE:
                {
                    ins = INS_vpdpbuud;
                    break;
                }

                case TYP_BYTE:
                {
                    switch (op3Type)
                    {
                        case TYP_UBYTE:
                        {
                            ins = INS_vpdpbsud;
                            break;
                        }

                        case TYP_BYTE:
                        {
                            ins = INS_vpdpbssd;
                            break;
                        }

                        default:
                        {
                            unreached();
                        }
                    }
                    break;
                }

                case TYP_SHORT:
                {
                    ins = INS_vpdpwsud;
                    break;
                }

                case TYP_USHORT:
                {
                    switch (op3Type)
                    {
                        case TYP_USHORT:
                        {
                            ins = INS_vpdpwuud;
                            break;
                        }

                        case TYP_SHORT:
                        {
                            ins = INS_vpdpwusd;
                            break;
                        }

                        default:
                        {
                            unreached();
                        }
                    }
                    break;
                }

                default:
                {
                    unreached();
                }
            }

            genHWIntrinsic_R_R_R_RM(ins, attr, targetReg, op1Reg, op2Reg, op3, instOptions);
            break;
        }

        default:
            unreached();
            break;
    }

    genProduceReg(node);
}

//------------------------------------------------------------------------
// genFmaIntrinsic: Generates the code for an FMA hardware intrinsic node
//
// Arguments:
//    node - The hardware intrinsic node
//
void CodeGen::genFmaIntrinsic(GenTreeHWIntrinsic* node, insOpts instOptions)
{
    NamedIntrinsic intrinsicId = node->GetHWIntrinsicId();
    assert(HWIntrinsicInfo::IsFmaIntrinsic(intrinsicId));

    var_types   baseType = node->GetSimdBaseType();
    emitAttr    attr     = emitActualTypeSize(Compiler::getSIMDTypeForSize(node->GetSimdSize()));
    instruction _213form = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler); // 213 form
    instruction _132form = (instruction)(_213form - 1);
    instruction _231form = (instruction)(_213form + 1);
    GenTree*    op1      = node->Op(1);
    GenTree*    op2      = node->Op(2);
    GenTree*    op3      = node->Op(3);

    regNumber targetReg = node->GetRegNum();

    genConsumeMultiOpOperands(node);

    regNumber op1NodeReg = op1->GetRegNum();
    regNumber op2NodeReg = op2->GetRegNum();
    regNumber op3NodeReg = op3->GetRegNum();

    GenTree* emitOp1 = op1;
    GenTree* emitOp2 = op2;
    GenTree* emitOp3 = op3;

    const bool copiesUpperBits = HWIntrinsicInfo::CopiesUpperBits(intrinsicId);

    // Intrinsics with CopyUpperBits semantics cannot have op1 be contained
    assert(!copiesUpperBits || !op1->isContained());

    instruction ins = INS_invalid;

    if (op1->isContained() || op1->isUsedFromSpillTemp())
    {
        // targetReg == op3NodeReg or targetReg == ?
        // op3 = ([op1] * op2) + op3
        // 231 form: XMM1 = (XMM2 * [XMM3]) + XMM1
        ins = _231form;
        std::swap(emitOp1, emitOp3);

        if (targetReg == op2NodeReg)
        {
            // op2 = ([op1] * op2) + op3
            // 132 form: XMM1 = (XMM1 * [XMM3]) + XMM2
            ins = _132form;
            std::swap(emitOp1, emitOp2);
        }
    }
    else if (op3->isContained() || op3->isUsedFromSpillTemp())
    {
        // targetReg could be op1NodeReg, op2NodeReg, or not equal to any op
        // op1 = (op1 * op2) + [op3] or op2 = (op1 * op2) + [op3]
        // ? = (op1 * op2) + [op3] or ? = (op1 * op2) + op3
        // 213 form: XMM1 = (XMM2 * XMM1) + [XMM3]
        ins = _213form;

        if (!copiesUpperBits && (targetReg == op2NodeReg))
        {
            // op2 = (op1 * op2) + [op3]
            // 213 form: XMM1 = (XMM2 * XMM1) + [XMM3]
            std::swap(emitOp1, emitOp2);
        }
    }
    else if (op2->isContained() || op2->isUsedFromSpillTemp())
    {
        // targetReg == op1NodeReg or targetReg == ?
        // op1 = (op1 * [op2]) + op3
        // 132 form: XMM1 = (XMM1 * [XMM3]) + XMM2
        ins = _132form;
        std::swap(emitOp2, emitOp3);

        if (!copiesUpperBits && (targetReg == op3NodeReg))
        {
            // op3 = (op1 * [op2]) + op3
            // 231 form: XMM1 = (XMM2 * [XMM3]) + XMM1
            ins = _231form;
            std::swap(emitOp1, emitOp2);
        }
    }
    else
    {
        // When we don't have a contained operand we still want to
        // preference based on the target register if possible.

        if (targetReg == op2NodeReg)
        {
            ins = _213form;
            std::swap(emitOp1, emitOp2);
        }
        else if (targetReg == op3NodeReg)
        {
            ins = _231form;
            std::swap(emitOp1, emitOp3);
        }
        else
        {
            ins = _213form;
        }
    }

    assert(ins != INS_invalid);
    genHWIntrinsic_R_R_R_RM(ins, attr, targetReg, emitOp1->GetRegNum(), emitOp2->GetRegNum(), emitOp3, instOptions);
    genProduceReg(node);
}

//------------------------------------------------------------------------
// genPermuteVar2x: Generates the code for a PermuteVar2x hardware intrinsic node
//
// Arguments:
//    node - The hardware intrinsic node
//
void CodeGen::genPermuteVar2x(GenTreeHWIntrinsic* node, insOpts instOptions)
{
    NamedIntrinsic intrinsicId = node->GetHWIntrinsicId();
    assert(HWIntrinsicInfo::IsPermuteVar2x(intrinsicId));

    var_types baseType = node->GetSimdBaseType();
    emitAttr  attr     = emitActualTypeSize(Compiler::getSIMDTypeForSize(node->GetSimdSize()));
    GenTree*  op1      = node->Op(1);
    GenTree*  op2      = node->Op(2);
    GenTree*  op3      = node->Op(3);

    regNumber targetReg = node->GetRegNum();

    genConsumeMultiOpOperands(node);

    regNumber op1NodeReg = op1->GetRegNum();
    regNumber op2NodeReg = op2->GetRegNum();
    regNumber op3NodeReg = op3->GetRegNum();

    GenTree* emitOp1 = op1;
    GenTree* emitOp2 = op2;
    GenTree* emitOp3 = op3;

    // We need to keep this in sync with lsraxarch.cpp
    // Ideally we'd actually swap the operands in lsra and simplify codegen
    // but its a bit more complicated to do so for many operands as well
    // as being complicated to tell codegen how to pick the right instruction

    assert(!op1->isContained());
    assert(!op2->isContained());

    instruction ins = HWIntrinsicInfo::lookupIns(intrinsicId, baseType, compiler); // vpermt2

    if (targetReg == op2NodeReg)
    {
        std::swap(emitOp1, emitOp2);

        switch (ins)
        {
            case INS_vpermt2b:
            {
                ins = INS_vpermi2b;
                break;
            }

            case INS_vpermt2d:
            {
                ins = INS_vpermi2d;
                break;
            }

            case INS_vpermt2pd:
            {
                ins = INS_vpermi2pd;
                break;
            }

            case INS_vpermt2ps:
            {
                ins = INS_vpermi2ps;
                break;
            }

            case INS_vpermt2q:
            {
                ins = INS_vpermi2q;
                break;
            }

            case INS_vpermt2w:
            {
                ins = INS_vpermi2w;
                break;
            }

            default:
            {
                unreached();
            }
        }
    }

    assert(ins != INS_invalid);
    genHWIntrinsic_R_R_R_RM(ins, attr, targetReg, emitOp1->GetRegNum(), emitOp2->GetRegNum(), emitOp3, instOptions);
    genProduceReg(node);
}

//------------------------------------------------------------------------
// genXCNTIntrinsic: Generates the code for a lzcnt/tzcnt/popcnt hardware intrinsic node, breaks false dependencies on
// the target register
//
// Arguments:
//    node - The hardware intrinsic node
//    ins  - The instruction being generated
//
void CodeGen::genXCNTIntrinsic(GenTreeHWIntrinsic* node, instruction ins)
{
    // LZCNT/TZCNT/POPCNT have a false dependency on the target register on Intel Sandy Bridge, Haswell, and Skylake
    // (POPCNT only) processors, so insert a `XOR target, target` to break the dependency via XOR triggering register
    // renaming, but only if it's not an actual dependency.

    GenTree*  op1        = node->Op(1);
    regNumber sourceReg1 = REG_NA;
    regNumber sourceReg2 = REG_NA;

    if (!op1->isContained())
    {
        sourceReg1 = op1->GetRegNum();
    }
    else if (op1->isIndir())
    {
        GenTreeIndir* indir   = op1->AsIndir();
        GenTree*      memBase = indir->Base();

        if (memBase != nullptr)
        {
            sourceReg1 = memBase->GetRegNum();
        }

        if (indir->HasIndex())
        {
            sourceReg2 = indir->Index()->GetRegNum();
        }
    }

    regNumber targetReg = node->GetRegNum();
    if ((targetReg != sourceReg1) && (targetReg != sourceReg2))
    {
        GetEmitter()->emitIns_R_R(INS_xor, EA_4BYTE, targetReg, targetReg);
    }
    genHWIntrinsic_R_RM(node, ins, emitTypeSize(node->TypeGet()), targetReg, op1, INS_OPTS_NONE);
}

//------------------------------------------------------------------------
// genX86SerializeIntrinsic: Generates the code for an X86 serialize hardware intrinsic node
//
// Arguments:
//    node - The hardware intrinsic node
//
void CodeGen::genX86SerializeIntrinsic(GenTreeHWIntrinsic* node)
{
    NamedIntrinsic intrinsicId = node->GetHWIntrinsicId();

    genConsumeMultiOpOperands(node);

    switch (intrinsicId)
    {
        case NI_X86Serialize_Serialize:
        {
            assert(node->GetSimdBaseType() == TYP_UNKNOWN);
            GetEmitter()->emitIns(INS_serialize);
            break;
        }

        default:
            unreached();
            break;
    }

    genProduceReg(node);
}

#endif // FEATURE_HW_INTRINSICS
