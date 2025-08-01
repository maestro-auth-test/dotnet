// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

// Enable calling OpenSSL functions through shims to enable support for
// different versioned so files naming and different configuration options
// on various Linux distributions.

#pragma once

// All the openssl includes need to be here to ensure that the APIs we use
// are overridden to be called through our function pointers.
#include <openssl/asn1.h>
#include <openssl/bio.h>
#include <openssl/bn.h>
#include <openssl/crypto.h>
#include <openssl/dsa.h>
#include <openssl/ec.h>
#include <openssl/ecdsa.h>
#include <openssl/err.h>
#include <openssl/evp.h>
#include <openssl/hmac.h>
#include <openssl/md5.h>
#include <openssl/objects.h>
#include <openssl/ocsp.h>
#include <openssl/opensslconf.h>
#include <openssl/pem.h>
#include <openssl/pkcs12.h>
#include <openssl/pkcs7.h>
#include <openssl/rand.h>
#include <openssl/rsa.h>
#include <openssl/sha.h>
#include <openssl/ssl.h>
#include <openssl/tls1.h>
#include <openssl/ui.h>
#include <openssl/x509.h>
#include <openssl/x509v3.h>

#include "pal_crypto_config.h"
#include "pal_compiler.h"
#define OPENSSL_VERSION_3_0_RTM 0x30000000L
#define OPENSSL_VERSION_1_1_1_RTM 0x10101000L
#define OPENSSL_VERSION_1_1_0_RTM 0x10100000L
#define OPENSSL_VERSION_1_0_2_RTM 0x10002000L

#if OPENSSL_VERSION_NUMBER >= OPENSSL_VERSION_3_0_RTM
#include <openssl/provider.h>
#include <openssl/store.h>
#include <openssl/params.h>
#include <openssl/core_names.h>
#include <openssl/kdf.h>
#endif

#if HAVE_OPENSSL_ENGINE
// Some Linux distributions build without engine support.
#include <openssl/engine.h>
#endif

#if OPENSSL_VERSION_NUMBER >= OPENSSL_VERSION_1_1_1_RTM
#define HAVE_OPENSSL_SET_CIPHERSUITES 1
#else
#define HAVE_OPENSSL_SET_CIPHERSUITES 0
#endif

// Defined by opensslconf.h if OpenSSL was build with -no-rc2
#ifndef OPENSSL_NO_RC2
#define HAVE_OPENSSL_RC2 1
#else
#define HAVE_OPENSSL_RC2 0
#endif


#if OPENSSL_VERSION_NUMBER < OPENSSL_VERSION_1_1_0_RTM

// Remove problematic #defines
#undef BN_abs_is_word
#undef BN_is_odd
#undef BN_is_one
#undef BN_is_zero
#undef SSL_get_state
#undef SSL_is_init_finished
#undef X509_get_X509_PUBKEY
#undef X509_get_version

#endif

#ifdef EVP_MD_CTX_create
#undef EVP_MD_CTX_create
#undef EVP_MD_CTX_init
#undef EVP_MD_CTX_destroy
#undef RSA_PKCS1_SSLeay
#undef SSLv23_method
#endif

#ifdef ERR_put_error
#undef ERR_put_error
void ERR_put_error(int32_t lib, int32_t func, int32_t reason, const char* file, int32_t line);
#endif

// The value -1 has the correct meaning on 1.0.x, but the constant wasn't named.
#ifndef RSA_PSS_SALTLEN_DIGEST
#define RSA_PSS_SALTLEN_DIGEST -1
#endif

#ifndef EVP_PKEY_RSA_PSS
#define EVP_PKEY_RSA_PSS 912
#endif

// ERR_R_UNSUPPORTED was introduced in OpenSSL 3. We need it for building with older OpenSSLs.
// Add a static assert so we know if OpenSSL changes the value.
#ifndef ERR_R_UNSUPPORTED
#define ERR_R_UNSUPPORTED 0x8010C
#else
c_static_assert(ERR_R_UNSUPPORTED == 0x8010C);
#endif

#ifndef EVP_KDF_HKDF_MODE_EXTRACT_AND_EXPAND
#define EVP_KDF_HKDF_MODE_EXTRACT_AND_EXPAND 0
#else
c_static_assert(EVP_KDF_HKDF_MODE_EXTRACT_AND_EXPAND == 0);
#endif

#ifndef EVP_KDF_HKDF_MODE_EXTRACT_ONLY
#define EVP_KDF_HKDF_MODE_EXTRACT_ONLY 1
#else
c_static_assert(EVP_KDF_HKDF_MODE_EXTRACT_ONLY == 1);
#endif

#ifndef EVP_KDF_HKDF_MODE_EXPAND_ONLY
#define EVP_KDF_HKDF_MODE_EXPAND_ONLY 2
#else
c_static_assert(EVP_KDF_HKDF_MODE_EXPAND_ONLY == 2);
#endif

#ifndef OSSL_SIGNATURE_PARAM_MESSAGE_ENCODING
#define OSSL_SIGNATURE_PARAM_MESSAGE_ENCODING "message-encoding"
#endif

#ifndef OSSL_PKEY_PARAM_ML_DSA_SEED
#define OSSL_PKEY_PARAM_ML_DSA_SEED "seed"
#endif

#ifndef OSSL_PKEY_PARAM_ML_KEM_SEED
#define OSSL_PKEY_PARAM_ML_KEM_SEED   "seed"
#endif

#ifndef OSSL_PKEY_PARAM_PUB_KEY
#define OSSL_PKEY_PARAM_PUB_KEY "pub"
#endif

#ifndef OSSL_PKEY_PARAM_PRIV_KEY
#define OSSL_PKEY_PARAM_PRIV_KEY "priv"
#endif

#ifndef EVP_PKEY_KEYPAIR
#define EVP_PKEY_KEYPAIR 135
#else
c_static_assert(EVP_PKEY_KEYPAIR == 135);
#endif

#ifndef EVP_PKEY_PUBLIC_KEY
#define EVP_PKEY_PUBLIC_KEY 134
#else
c_static_assert(EVP_PKEY_PUBLIC_KEY == 134);
#endif

#ifndef OSSL_SIGNATURE_PARAM_CONTEXT_STRING
#define OSSL_SIGNATURE_PARAM_CONTEXT_STRING "context-string"
#endif

#ifndef OSSL_SIGNATURE_PARAM_MU
#define OSSL_SIGNATURE_PARAM_MU "mu"
#endif

#if defined FEATURE_DISTRO_AGNOSTIC_SSL || OPENSSL_VERSION_NUMBER >= OPENSSL_VERSION_3_0_RTM
#include "apibridge_30_rev.h"
#endif
#if defined FEATURE_DISTRO_AGNOSTIC_SSL || OPENSSL_VERSION_NUMBER < OPENSSL_VERSION_3_0_RTM
#include "apibridge_30.h"
#endif
#if defined FEATURE_DISTRO_AGNOSTIC_SSL || OPENSSL_VERSION_NUMBER < OPENSSL_VERSION_1_1_0_RTM
#include "apibridge.h"
#endif

#ifdef FEATURE_DISTRO_AGNOSTIC_SSL

#define NEED_OPENSSL_1_0 true
#define NEED_OPENSSL_1_1 true
#define NEED_OPENSSL_3_0 true

int OpenLibrary(void);
void InitializeOpenSSLShim(void);

#if !HAVE_OPENSSL_EC2M
// In portable build, we need to support the following functions even if they were not present
// on the build OS. The shim will detect their presence at runtime.
#undef HAVE_OPENSSL_EC2M
#define HAVE_OPENSSL_EC2M 1
const EC_METHOD* EC_GF2m_simple_method(void);
int EC_GROUP_get_curve_GF2m(const EC_GROUP* group, BIGNUM* p, BIGNUM* a, BIGNUM* b, BN_CTX* ctx);
int EC_GROUP_set_curve_GF2m(EC_GROUP* group, const BIGNUM* p, const BIGNUM* a, const BIGNUM* b, BN_CTX* ctx);
int EC_POINT_get_affine_coordinates_GF2m(const EC_GROUP* group, const EC_POINT* p, BIGNUM* x, BIGNUM* y, BN_CTX* ctx);
int EC_POINT_set_affine_coordinates_GF2m(
    const EC_GROUP* group, EC_POINT* p, const BIGNUM* x, const BIGNUM* y, BN_CTX* ctx);
#endif

#if !HAVE_OPENSSL_SET_CIPHERSUITES
#undef HAVE_OPENSSL_SET_CIPHERSUITES
#define HAVE_OPENSSL_SET_CIPHERSUITES 1
int SSL_CTX_set_ciphersuites(SSL_CTX *ctx, const char *str);
int SSL_set_ciphersuites(SSL *s, const char *str);
const SSL_CIPHER* SSL_CIPHER_find(SSL *ssl, const unsigned char *ptr);
#endif

#if OPENSSL_VERSION_NUMBER >= OPENSSL_VERSION_3_0_RTM
#include "osslcompat_102.h"
#elif OPENSSL_VERSION_NUMBER >= OPENSSL_VERSION_1_1_0_RTM
#include "osslcompat_30.h"
#include "osslcompat_102.h"
#else
#include "osslcompat_30.h"
#include "osslcompat_111.h"
#endif

#if !HAVE_OPENSSL_ALPN
#undef HAVE_OPENSSL_ALPN
#define HAVE_OPENSSL_ALPN 1
int SSL_CTX_set_alpn_protos(SSL_CTX* ctx, const unsigned char* protos, unsigned int protos_len);
void SSL_CTX_set_alpn_select_cb(SSL_CTX* ctx,
                                int (*cb)(SSL* ssl,
                                          const unsigned char** out,
                                          unsigned char* outlen,
                                          const unsigned char* in,
                                          unsigned int inlen,
                                          void* arg),
                                void* arg);
void SSL_get0_alpn_selected(const SSL* ssl, const unsigned char** protocol, unsigned int* len);
#endif

#if !HAVE_OPENSSL_CHACHA20POLY1305
#undef HAVE_OPENSSL_CHACHA20POLY1305
#define HAVE_OPENSSL_CHACHA20POLY1305 1
const EVP_CIPHER* EVP_chacha20_poly1305(void);
#define EVP_CTRL_AEAD_GET_TAG 0x10
#define EVP_CTRL_AEAD_SET_TAG 0x11
#endif

#if !HAVE_OPENSSL_SHA3
#undef HAVE_OPENSSL_SHA3
#define HAVE_OPENSSL_SHA3 1
const EVP_MD *EVP_sha3_256(void);
const EVP_MD *EVP_sha3_384(void);
const EVP_MD *EVP_sha3_512(void);
const EVP_MD *EVP_shake128(void);
const EVP_MD *EVP_shake256(void);
int EVP_DigestFinalXOF(EVP_MD_CTX *ctx, unsigned char *md, size_t len);
#endif

#if !HAVE_OPENSSL_SHA3_SQUEEZE
#undef HAVE_OPENSSL_SHA3_SQUEEZE
#define HAVE_OPENSSL_SHA3_SQUEEZE 1
int EVP_DigestSqueeze(EVP_MD_CTX *ctx, unsigned char *out, size_t outlen);
#endif

#if !HAVE_OPENSSL_EVP_PKEY_SIGN_MESSAGE_INIT
#undef HAVE_OPENSSL_EVP_PKEY_SIGN_MESSAGE_INIT
#define HAVE_OPENSSL_EVP_PKEY_SIGN_MESSAGE_INIT 1
EVP_SIGNATURE *EVP_SIGNATURE_fetch(OSSL_LIB_CTX *ctx, const char *algorithm, const char *properties);
void EVP_SIGNATURE_free(EVP_SIGNATURE *signature);
int EVP_PKEY_sign_message_init(EVP_PKEY_CTX *ctx, EVP_SIGNATURE *algo, const OSSL_PARAM params[]);
int EVP_PKEY_verify_message_init(EVP_PKEY_CTX *ctx, EVP_SIGNATURE *algo, const OSSL_PARAM params[]);
const char *EVP_PKEY_get0_type_name(const EVP_PKEY *key);
#endif

#if !HAVE_OPENSSL_ENGINE
#undef HAVE_OPENSSL_ENGINE
#define HAVE_OPENSSL_ENGINE 1

ENGINE *ENGINE_by_id(const char *id);
int ENGINE_init(ENGINE *e);
int ENGINE_finish(ENGINE *e);
ENGINE *ENGINE_new(void);
int ENGINE_free(ENGINE *e);
typedef EVP_PKEY *(*ENGINE_LOAD_KEY_PTR)(ENGINE *, const char *,
                                         UI_METHOD *ui_method,
                                         void *callback_data);
EVP_PKEY *ENGINE_load_private_key(ENGINE *e, const char *key_id,
                                  UI_METHOD *ui_method, void *callback_data);
EVP_PKEY *ENGINE_load_public_key(ENGINE *e, const char *key_id,
                                 UI_METHOD *ui_method, void *callback_data);

#endif

#if !HAVE_OPENSSL_RC2
#undef HAVE_OPENSSL_RC2
#define HAVE_OPENSSL_RC2 1
const EVP_CIPHER* EVP_rc2_ecb(void);
const EVP_CIPHER* EVP_rc2_cbc(void);
#endif

#define API_EXISTS(fn) (fn != NULL)

#if defined(FEATURE_DISTRO_AGNOSTIC_SSL) && defined(TARGET_ARM) && defined(TARGET_LINUX)
extern bool g_libSslUses32BitTime;
#endif

// List of all functions from the libssl that are used in the System.Security.Cryptography.Native.
// Forgetting to add a function here results in build failure with message reporting the function
// that needs to be added.

#define FOR_ALL_OPENSSL_FUNCTIONS \
    REQUIRED_FUNCTION(a2d_ASN1_OBJECT) \
    REQUIRED_FUNCTION(ASN1_d2i_bio) \
    REQUIRED_FUNCTION(ASN1_i2d_bio) \
    REQUIRED_FUNCTION(ASN1_GENERALIZEDTIME_free) \
    REQUIRED_FUNCTION(ASN1_INTEGER_get) \
    REQUIRED_FUNCTION(ASN1_OBJECT_free) \
    REQUIRED_FUNCTION(ASN1_OCTET_STRING_free) \
    REQUIRED_FUNCTION(ASN1_OCTET_STRING_new) \
    REQUIRED_FUNCTION(ASN1_OCTET_STRING_set) \
    REQUIRED_FUNCTION(ASN1_STRING_dup) \
    REQUIRED_FUNCTION(ASN1_STRING_free) \
    REQUIRED_FUNCTION(ASN1_STRING_print_ex) \
    REQUIRED_FUNCTION(ASN1_TIME_new) \
    REQUIRED_FUNCTION(ASN1_TIME_set) \
    FALLBACK_FUNCTION(ASN1_TIME_to_tm) \
    REQUIRED_FUNCTION(ASN1_TIME_free) \
    REQUIRED_FUNCTION(BIO_ctrl) \
    REQUIRED_FUNCTION(BIO_ctrl_pending) \
    REQUIRED_FUNCTION(BIO_free) \
    REQUIRED_FUNCTION(BIO_gets) \
    REQUIRED_FUNCTION(BIO_new) \
    REQUIRED_FUNCTION(BIO_new_file) \
    REQUIRED_FUNCTION(BIO_read) \
    FALLBACK_FUNCTION(BIO_up_ref) \
    REQUIRED_FUNCTION(BIO_s_mem) \
    REQUIRED_FUNCTION(BIO_write) \
    FALLBACK_FUNCTION(BN_abs_is_word) \
    REQUIRED_FUNCTION(BN_bin2bn) \
    REQUIRED_FUNCTION(BN_bn2bin) \
    REQUIRED_FUNCTION(BN_clear_free) \
    REQUIRED_FUNCTION(BN_cmp) \
    REQUIRED_FUNCTION(BN_div) \
    REQUIRED_FUNCTION(BN_dup) \
    REQUIRED_FUNCTION(BN_free) \
    REQUIRED_FUNCTION(BN_gcd) \
    FALLBACK_FUNCTION(BN_is_odd) \
    FALLBACK_FUNCTION(BN_is_one) \
    FALLBACK_FUNCTION(BN_is_zero) \
    REQUIRED_FUNCTION(BN_mod_inverse) \
    REQUIRED_FUNCTION(BN_mod_mul) \
    REQUIRED_FUNCTION(BN_mul) \
    REQUIRED_FUNCTION(BN_new) \
    REQUIRED_FUNCTION(BN_num_bits) \
    REQUIRED_FUNCTION(BN_set_word) \
    REQUIRED_FUNCTION(BN_sub) \
    REQUIRED_FUNCTION(BN_value_one) \
    REQUIRED_FUNCTION(BN_CTX_new) \
    REQUIRED_FUNCTION(BN_CTX_free) \
    LEGACY_FUNCTION(CRYPTO_add_lock) \
    REQUIRED_FUNCTION(CRYPTO_free) \
    REQUIRED_FUNCTION(CRYPTO_get_ex_new_index) \
    REQUIRED_FUNCTION(CRYPTO_malloc) \
    LEGACY_FUNCTION(CRYPTO_num_locks) \
    LEGACY_FUNCTION(CRYPTO_set_locking_callback) \
    REQUIRED_FUNCTION(CRYPTO_set_mem_functions) \
    REQUIRED_FUNCTION(d2i_OCSP_RESPONSE) \
    REQUIRED_FUNCTION(d2i_PKCS12_fp) \
    REQUIRED_FUNCTION(d2i_PKCS7) \
    REQUIRED_FUNCTION(d2i_PKCS7_bio) \
    REQUIRED_FUNCTION(d2i_PKCS8_PRIV_KEY_INFO) \
    REQUIRED_FUNCTION(d2i_PUBKEY) \
    REQUIRED_FUNCTION(d2i_RSAPublicKey) \
    REQUIRED_FUNCTION(d2i_X509) \
    REQUIRED_FUNCTION(d2i_X509_bio) \
    REQUIRED_FUNCTION(d2i_X509_CRL) \
    REQUIRED_FUNCTION(d2i_X509_NAME) \
    REQUIRED_FUNCTION(DSA_free) \
    REQUIRED_FUNCTION(DSA_generate_key) \
    REQUIRED_FUNCTION(DSA_generate_parameters_ex) \
    FALLBACK_FUNCTION(DSA_get0_key) \
    FALLBACK_FUNCTION(DSA_get0_pqg) \
    FALLBACK_FUNCTION(DSA_get_method) \
    REQUIRED_FUNCTION(DSA_new) \
    REQUIRED_FUNCTION(DSA_OpenSSL) \
    FALLBACK_FUNCTION(DSA_set0_key) \
    FALLBACK_FUNCTION(DSA_set0_pqg) \
    REQUIRED_FUNCTION(DSA_sign) \
    REQUIRED_FUNCTION(DSA_size) \
    REQUIRED_FUNCTION(DSA_up_ref) \
    REQUIRED_FUNCTION(DSA_verify) \
    REQUIRED_FUNCTION(ECDSA_sign) \
    REQUIRED_FUNCTION(ECDSA_size) \
    REQUIRED_FUNCTION(ECDSA_verify) \
    REQUIRED_FUNCTION(EC_GFp_mont_method) \
    REQUIRED_FUNCTION(EC_GFp_simple_method) \
    REQUIRED_FUNCTION(EC_GROUP_check) \
    REQUIRED_FUNCTION(EC_GROUP_free) \
    REQUIRED_FUNCTION(EC_GROUP_get0_generator) \
    REQUIRED_FUNCTION(EC_GROUP_get0_seed) \
    REQUIRED_FUNCTION(EC_GROUP_get_cofactor) \
    REQUIRED_FUNCTION(EC_GROUP_get_curve_GFp) \
    REQUIRED_FUNCTION(EC_GROUP_get_curve_name) \
    REQUIRED_FUNCTION(EC_GROUP_get_degree) \
    REQUIRED_FUNCTION(EC_GROUP_get_order) \
    REQUIRED_FUNCTION(EC_GROUP_get_seed_len) \
    LIGHTUP_FUNCTION(EC_GROUP_get_field_type) \
    REQUIRED_FUNCTION(EC_GROUP_method_of) \
    REQUIRED_FUNCTION(EC_GROUP_new) \
    LIGHTUP_FUNCTION(EC_GROUP_new_by_curve_name) \
    REQUIRED_FUNCTION(EC_GROUP_set_curve_GFp) \
    REQUIRED_FUNCTION(EC_GROUP_set_generator) \
    REQUIRED_FUNCTION(EC_GROUP_set_seed) \
    REQUIRED_FUNCTION(EC_KEY_check_key) \
    REQUIRED_FUNCTION(EC_KEY_free) \
    REQUIRED_FUNCTION(EC_KEY_generate_key) \
    REQUIRED_FUNCTION(EC_KEY_get0_group) \
    REQUIRED_FUNCTION(EC_KEY_get0_private_key) \
    REQUIRED_FUNCTION(EC_KEY_get0_public_key) \
    REQUIRED_FUNCTION(EC_KEY_new) \
    REQUIRED_FUNCTION(EC_KEY_new_by_curve_name) \
    REQUIRED_FUNCTION(EC_KEY_set_group) \
    REQUIRED_FUNCTION(EC_KEY_set_private_key) \
    REQUIRED_FUNCTION(EC_KEY_set_public_key) \
    REQUIRED_FUNCTION(EC_KEY_set_public_key_affine_coordinates) \
    REQUIRED_FUNCTION(EC_KEY_up_ref) \
    REQUIRED_FUNCTION(EC_METHOD_get_field_type) \
    REQUIRED_FUNCTION(EC_POINT_free) \
    REQUIRED_FUNCTION(EC_POINT_get_affine_coordinates_GFp) \
    REQUIRED_FUNCTION(EC_POINT_mul) \
    REQUIRED_FUNCTION(EC_POINT_new) \
    REQUIRED_FUNCTION(EC_POINT_set_affine_coordinates_GFp) \
    LIGHTUP_FUNCTION(EC_POINT_oct2point) \
    LIGHTUP_FUNCTION(ENGINE_by_id) \
    LIGHTUP_FUNCTION(ENGINE_finish) \
    LIGHTUP_FUNCTION(ENGINE_free) \
    LIGHTUP_FUNCTION(ENGINE_init) \
    LIGHTUP_FUNCTION(ENGINE_load_public_key) \
    LIGHTUP_FUNCTION(ENGINE_load_private_key) \
    REQUIRED_FUNCTION(ERR_clear_error) \
    REQUIRED_FUNCTION(ERR_error_string_n) \
    REQUIRED_FUNCTION(ERR_get_error) \
    LEGACY_FUNCTION(ERR_load_crypto_strings) \
    LIGHTUP_FUNCTION(ERR_new) \
    REQUIRED_FUNCTION(ERR_peek_error) \
    REQUIRED_FUNCTION(ERR_peek_error_line) \
    REQUIRED_FUNCTION(ERR_peek_last_error) \
    REQUIRED_FUNCTION(ERR_pop_to_mark) \
    FALLBACK_FUNCTION(ERR_put_error) \
    REQUIRED_FUNCTION(ERR_reason_error_string) \
    REQUIRED_FUNCTION(ERR_set_mark) \
    LIGHTUP_FUNCTION(ERR_set_debug) \
    LIGHTUP_FUNCTION(ERR_set_error) \
    REQUIRED_FUNCTION(EVP_aes_128_cbc) \
    REQUIRED_FUNCTION(EVP_aes_128_ccm) \
    REQUIRED_FUNCTION(EVP_aes_128_cfb128) \
    REQUIRED_FUNCTION(EVP_aes_128_cfb8) \
    REQUIRED_FUNCTION(EVP_aes_128_ecb) \
    REQUIRED_FUNCTION(EVP_aes_128_gcm) \
    REQUIRED_FUNCTION(EVP_aes_192_cbc) \
    REQUIRED_FUNCTION(EVP_aes_192_ccm) \
    REQUIRED_FUNCTION(EVP_aes_192_cfb128) \
    REQUIRED_FUNCTION(EVP_aes_192_cfb8) \
    REQUIRED_FUNCTION(EVP_aes_192_ecb) \
    REQUIRED_FUNCTION(EVP_aes_192_gcm) \
    REQUIRED_FUNCTION(EVP_aes_256_cbc) \
    REQUIRED_FUNCTION(EVP_aes_256_ccm) \
    REQUIRED_FUNCTION(EVP_aes_256_cfb128) \
    REQUIRED_FUNCTION(EVP_aes_256_cfb8) \
    REQUIRED_FUNCTION(EVP_aes_256_ecb) \
    REQUIRED_FUNCTION(EVP_aes_256_gcm) \
    LIGHTUP_FUNCTION(EVP_chacha20_poly1305) \
    LEGACY_FUNCTION(EVP_CIPHER_CTX_cleanup) \
    REQUIRED_FUNCTION(EVP_CIPHER_CTX_ctrl) \
    FALLBACK_FUNCTION(EVP_CIPHER_CTX_free) \
    LEGACY_FUNCTION(EVP_CIPHER_CTX_init) \
    FALLBACK_FUNCTION(EVP_CIPHER_CTX_new) \
    FALLBACK_FUNCTION(EVP_CIPHER_CTX_reset) \
    REQUIRED_FUNCTION(EVP_CIPHER_CTX_set_key_length) \
    REQUIRED_FUNCTION(EVP_CIPHER_CTX_set_padding) \
    RENAMED_FUNCTION(EVP_CIPHER_get_nid, EVP_CIPHER_nid) \
    REQUIRED_FUNCTION(EVP_CipherFinal_ex) \
    REQUIRED_FUNCTION(EVP_CipherInit_ex) \
    REQUIRED_FUNCTION(EVP_CipherUpdate) \
    REQUIRED_FUNCTION(EVP_des_cbc) \
    REQUIRED_FUNCTION(EVP_des_cfb8) \
    REQUIRED_FUNCTION(EVP_des_ecb) \
    REQUIRED_FUNCTION(EVP_des_ede3) \
    REQUIRED_FUNCTION(EVP_des_ede3_cbc) \
    REQUIRED_FUNCTION(EVP_des_ede3_cfb8) \
    REQUIRED_FUNCTION(EVP_des_ede3_cfb64) \
    REQUIRED_FUNCTION(EVP_DigestFinal_ex) \
    LIGHTUP_FUNCTION(EVP_DigestFinalXOF) \
    REQUIRED_FUNCTION(EVP_DigestInit_ex) \
    LIGHTUP_FUNCTION(EVP_DigestSqueeze) \
    REQUIRED_FUNCTION(EVP_DigestUpdate) \
    REQUIRED_FUNCTION(EVP_get_digestbyname) \
    LIGHTUP_FUNCTION(EVP_KDF_CTX_free) \
    LIGHTUP_FUNCTION(EVP_KDF_CTX_new) \
    LIGHTUP_FUNCTION(EVP_KDF_derive) \
    LIGHTUP_FUNCTION(EVP_KDF_fetch) \
    LIGHTUP_FUNCTION(EVP_KDF_free) \
    LIGHTUP_FUNCTION(EVP_KEM_fetch) \
    LIGHTUP_FUNCTION(EVP_KEM_free) \
    LIGHTUP_FUNCTION(EVP_MAC_fetch) \
    LIGHTUP_FUNCTION(EVP_MAC_final) \
    LIGHTUP_FUNCTION(EVP_MAC_free) \
    LIGHTUP_FUNCTION(EVP_MAC_CTX_new) \
    LIGHTUP_FUNCTION(EVP_MAC_CTX_free) \
    LIGHTUP_FUNCTION(EVP_MAC_CTX_set_params) \
    LIGHTUP_FUNCTION(EVP_MAC_CTX_dup) \
    LIGHTUP_FUNCTION(EVP_MAC_init) \
    LIGHTUP_FUNCTION(EVP_MAC_update) \
    REQUIRED_FUNCTION(EVP_md5) \
    REQUIRED_FUNCTION(EVP_MD_CTX_copy_ex) \
    RENAMED_FUNCTION(EVP_MD_CTX_free, EVP_MD_CTX_destroy) \
    RENAMED_FUNCTION(EVP_MD_CTX_new, EVP_MD_CTX_create) \
    REQUIRED_FUNCTION(EVP_MD_CTX_set_flags) \
    LIGHTUP_FUNCTION(EVP_MD_fetch) \
    RENAMED_FUNCTION(EVP_MD_get_size, EVP_MD_size) \
    REQUIRED_FUNCTION(EVP_PKCS82PKEY) \
    REQUIRED_FUNCTION(EVP_PKEY2PKCS8) \
    REQUIRED_FUNCTION(EVP_PKEY_CTX_ctrl) \
    REQUIRED_FUNCTION(EVP_PKEY_CTX_ctrl_str) \
    REQUIRED_FUNCTION(EVP_PKEY_CTX_free) \
    REQUIRED_FUNCTION(EVP_PKEY_CTX_get0_pkey) \
    REQUIRED_FUNCTION(EVP_PKEY_CTX_new) \
    REQUIRED_FUNCTION(EVP_PKEY_CTX_new_id) \
    LIGHTUP_FUNCTION(EVP_PKEY_CTX_new_from_name) \
    LIGHTUP_FUNCTION(EVP_PKEY_CTX_new_from_pkey) \
    LIGHTUP_FUNCTION(EVP_PKEY_CTX_set_params) \
    FALLBACK_FUNCTION(EVP_PKEY_CTX_set_rsa_keygen_bits) \
    FALLBACK_FUNCTION(EVP_PKEY_CTX_set_rsa_oaep_md) \
    FALLBACK_FUNCTION(EVP_PKEY_CTX_set_rsa_padding) \
    FALLBACK_FUNCTION(EVP_PKEY_CTX_set_rsa_pss_saltlen) \
    FALLBACK_FUNCTION(EVP_PKEY_CTX_set_signature_md) \
    FALLBACK_FUNCTION(EVP_PKEY_check) \
    LIGHTUP_FUNCTION(EVP_PKEY_decapsulate) \
    LIGHTUP_FUNCTION(EVP_PKEY_decapsulate_init) \
    REQUIRED_FUNCTION(EVP_PKEY_decrypt) \
    REQUIRED_FUNCTION(EVP_PKEY_decrypt_init) \
    REQUIRED_FUNCTION(EVP_PKEY_derive_set_peer) \
    REQUIRED_FUNCTION(EVP_PKEY_derive_init) \
    REQUIRED_FUNCTION(EVP_PKEY_derive) \
    LIGHTUP_FUNCTION(EVP_PKEY_encapsulate) \
    LIGHTUP_FUNCTION(EVP_PKEY_encapsulate_init) \
    REQUIRED_FUNCTION(EVP_PKEY_encrypt) \
    REQUIRED_FUNCTION(EVP_PKEY_encrypt_init) \
    REQUIRED_FUNCTION(EVP_PKEY_free) \
    LIGHTUP_FUNCTION(EVP_PKEY_fromdata) \
    LIGHTUP_FUNCTION(EVP_PKEY_fromdata_init) \
    RENAMED_FUNCTION(EVP_PKEY_get_base_id, EVP_PKEY_base_id) \
    RENAMED_FUNCTION(EVP_PKEY_get_bits, EVP_PKEY_bits) \
    FALLBACK_FUNCTION(EVP_PKEY_get0_RSA) \
    LIGHTUP_FUNCTION(EVP_PKEY_get0_type_name) \
    REQUIRED_FUNCTION(EVP_PKEY_get1_DSA) \
    REQUIRED_FUNCTION(EVP_PKEY_get1_EC_KEY) \
    REQUIRED_FUNCTION(EVP_PKEY_get1_RSA) \
    LIGHTUP_FUNCTION(EVP_PKEY_is_a) \
    REQUIRED_FUNCTION(EVP_PKEY_keygen) \
    REQUIRED_FUNCTION(EVP_PKEY_keygen_init) \
    REQUIRED_FUNCTION(EVP_PKEY_new) \
    FALLBACK_FUNCTION(EVP_PKEY_public_check) \
    REQUIRED_FUNCTION(EVP_PKEY_set1_DSA) \
    REQUIRED_FUNCTION(EVP_PKEY_set1_EC_KEY) \
    REQUIRED_FUNCTION(EVP_PKEY_set1_RSA) \
    REQUIRED_FUNCTION(EVP_PKEY_sign) \
    REQUIRED_FUNCTION(EVP_PKEY_sign_init) \
    LIGHTUP_FUNCTION(EVP_PKEY_sign_message_init) \
    FALLBACK_FUNCTION(EVP_PKEY_up_ref) \
    REQUIRED_FUNCTION(EVP_PKEY_verify) \
    REQUIRED_FUNCTION(EVP_PKEY_verify_init) \
    LIGHTUP_FUNCTION(EVP_PKEY_verify_message_init) \
    LIGHTUP_FUNCTION(EVP_PKEY_get_bn_param) \
    LIGHTUP_FUNCTION(EVP_PKEY_get_utf8_string_param) \
    LIGHTUP_FUNCTION(EVP_PKEY_get_octet_string_param) \
    LIGHTUP_FUNCTION(EVP_rc2_cbc) \
    LIGHTUP_FUNCTION(EVP_rc2_ecb) \
    REQUIRED_FUNCTION(EVP_sha1) \
    REQUIRED_FUNCTION(EVP_sha256) \
    REQUIRED_FUNCTION(EVP_sha384) \
    REQUIRED_FUNCTION(EVP_sha512) \
    LIGHTUP_FUNCTION(EVP_sha3_256) \
    LIGHTUP_FUNCTION(EVP_sha3_384) \
    LIGHTUP_FUNCTION(EVP_sha3_512) \
    LIGHTUP_FUNCTION(EVP_shake128) \
    LIGHTUP_FUNCTION(EVP_shake256) \
    LIGHTUP_FUNCTION(EVP_SIGNATURE_fetch) \
    LIGHTUP_FUNCTION(EVP_SIGNATURE_free) \
    REQUIRED_FUNCTION(GENERAL_NAMES_free) \
    REQUIRED_FUNCTION(HMAC) \
    LEGACY_FUNCTION(HMAC_CTX_cleanup) \
    REQUIRED_FUNCTION(HMAC_CTX_copy) \
    FALLBACK_FUNCTION(HMAC_CTX_free) \
    LEGACY_FUNCTION(HMAC_CTX_init) \
    FALLBACK_FUNCTION(HMAC_CTX_new) \
    REQUIRED_FUNCTION(HMAC_Final) \
    REQUIRED_FUNCTION(HMAC_Init_ex) \
    REQUIRED_FUNCTION(HMAC_Update) \
    REQUIRED_FUNCTION(i2d_ASN1_INTEGER) \
    REQUIRED_FUNCTION(i2d_ASN1_TYPE) \
    REQUIRED_FUNCTION(i2d_OCSP_REQUEST) \
    REQUIRED_FUNCTION(i2d_OCSP_RESPONSE) \
    REQUIRED_FUNCTION(i2d_PKCS7) \
    REQUIRED_FUNCTION(i2d_PKCS8_PRIV_KEY_INFO) \
    REQUIRED_FUNCTION(i2d_PUBKEY) \
    REQUIRED_FUNCTION(i2d_X509) \
    REQUIRED_FUNCTION(i2d_X509_PUBKEY) \
    REQUIRED_FUNCTION(OBJ_ln2nid) \
    REQUIRED_FUNCTION(OBJ_nid2ln) \
    REQUIRED_FUNCTION(OBJ_nid2sn) \
    REQUIRED_FUNCTION(OBJ_nid2obj) \
    REQUIRED_FUNCTION(OBJ_obj2nid) \
    REQUIRED_FUNCTION(OBJ_obj2txt) \
    REQUIRED_FUNCTION(OBJ_sn2nid) \
    REQUIRED_FUNCTION(OBJ_txt2nid) \
    REQUIRED_FUNCTION(OBJ_txt2obj) \
    REQUIRED_FUNCTION(OCSP_BASICRESP_free) \
    REQUIRED_FUNCTION(OCSP_basic_verify) \
    REQUIRED_FUNCTION(OCSP_CERTID_free) \
    REQUIRED_FUNCTION(OCSP_cert_to_id) \
    REQUIRED_FUNCTION(OCSP_check_nonce) \
    REQUIRED_FUNCTION(OCSP_request_add0_id) \
    REQUIRED_FUNCTION(OCSP_REQUEST_free) \
    REQUIRED_FUNCTION(OCSP_REQUEST_new) \
    REQUIRED_FUNCTION(OCSP_resp_find_status) \
    REQUIRED_FUNCTION(OCSP_response_get1_basic) \
    REQUIRED_FUNCTION(OCSP_RESPONSE_free) \
    REQUIRED_FUNCTION(OCSP_RESPONSE_new) \
    LEGACY_FUNCTION(OPENSSL_add_all_algorithms_conf) \
    REQUIRED_FUNCTION(OPENSSL_cleanse) \
    REQUIRED_FUNCTION_110(OPENSSL_init_ssl) \
    RENAMED_FUNCTION(OPENSSL_sk_free, sk_free) \
    RENAMED_FUNCTION(OPENSSL_sk_new_null, sk_new_null) \
    RENAMED_FUNCTION(OPENSSL_sk_num, sk_num) \
    RENAMED_FUNCTION(OPENSSL_sk_pop, sk_pop) \
    RENAMED_FUNCTION(OPENSSL_sk_pop_free, sk_pop_free) \
    RENAMED_FUNCTION(OPENSSL_sk_push, sk_push) \
    RENAMED_FUNCTION(OPENSSL_sk_value, sk_value) \
    FALLBACK_FUNCTION(OpenSSL_version_num) \
    LIGHTUP_FUNCTION(OSSL_LIB_CTX_free) \
    LIGHTUP_FUNCTION(OSSL_LIB_CTX_new) \
    LIGHTUP_FUNCTION(OSSL_PROVIDER_load) \
    LIGHTUP_FUNCTION(OSSL_PROVIDER_try_load) \
    LIGHTUP_FUNCTION(OSSL_PROVIDER_unload) \
    LIGHTUP_FUNCTION(OSSL_STORE_close) \
    LIGHTUP_FUNCTION(OSSL_STORE_eof) \
    LIGHTUP_FUNCTION(OSSL_STORE_INFO_free) \
    LIGHTUP_FUNCTION(OSSL_STORE_INFO_get_type) \
    LIGHTUP_FUNCTION(OSSL_STORE_INFO_get1_PKEY) \
    LIGHTUP_FUNCTION(OSSL_STORE_INFO_get1_PUBKEY) \
    LIGHTUP_FUNCTION(OSSL_STORE_load) \
    LIGHTUP_FUNCTION(OSSL_STORE_open_ex) \
    LIGHTUP_FUNCTION(OSSL_PARAM_construct_octet_string) \
    LIGHTUP_FUNCTION(OSSL_PARAM_construct_utf8_string) \
    LIGHTUP_FUNCTION(OSSL_PARAM_construct_int) \
    LIGHTUP_FUNCTION(OSSL_PARAM_construct_int32) \
    LIGHTUP_FUNCTION(OSSL_PARAM_construct_end) \
    REQUIRED_FUNCTION(PKCS8_PRIV_KEY_INFO_free) \
    REQUIRED_FUNCTION(PEM_read_bio_PKCS7) \
    REQUIRED_FUNCTION(PEM_read_bio_X509) \
    REQUIRED_FUNCTION(PEM_read_bio_X509_AUX) \
    REQUIRED_FUNCTION(PEM_read_bio_X509_CRL) \
    REQUIRED_FUNCTION(PEM_write_bio_X509_CRL) \
    REQUIRED_FUNCTION(PKCS5_PBKDF2_HMAC) \
    REQUIRED_FUNCTION(PKCS12_free) \
    REQUIRED_FUNCTION(PKCS12_parse) \
    REQUIRED_FUNCTION(PKCS7_sign) \
    REQUIRED_FUNCTION(PKCS7_free) \
    REQUIRED_FUNCTION(RAND_bytes) \
    REQUIRED_FUNCTION(RAND_poll) \
    REQUIRED_FUNCTION(RSA_check_key) \
    REQUIRED_FUNCTION(RSA_free) \
    REQUIRED_FUNCTION(RSA_generate_key_ex) \
    REQUIRED_FUNCTION(RSA_get_method) \
    FALLBACK_FUNCTION(RSA_get_multi_prime_extra_count) \
    FALLBACK_FUNCTION(RSA_get0_crt_params) \
    FALLBACK_FUNCTION(RSA_get0_factors) \
    FALLBACK_FUNCTION(RSA_get0_key) \
    FALLBACK_FUNCTION(RSA_meth_get_flags) \
    REQUIRED_FUNCTION(RSA_new) \
    FALLBACK_FUNCTION(RSA_pkey_ctx_ctrl) \
    RENAMED_FUNCTION(RSA_PKCS1_OpenSSL, RSA_PKCS1_SSLeay) \
    FALLBACK_FUNCTION(RSA_set0_crt_params) \
    FALLBACK_FUNCTION(RSA_set0_factors) \
    FALLBACK_FUNCTION(RSA_set0_key) \
    REQUIRED_FUNCTION(RSA_set_method) \
    REQUIRED_FUNCTION(RSA_size) \
    FALLBACK_FUNCTION(RSA_test_flags) \
    REQUIRED_FUNCTION(RSA_up_ref) \
    REQUIRED_FUNCTION(RSA_verify) \
    LIGHTUP_FUNCTION(SSL_CIPHER_find) \
    REQUIRED_FUNCTION(SSL_CIPHER_get_bits) \
    REQUIRED_FUNCTION(SSL_CIPHER_get_id) \
    LIGHTUP_FUNCTION(SSL_CIPHER_get_name) \
    LIGHTUP_FUNCTION(SSL_CIPHER_get_version) \
    REQUIRED_FUNCTION(SSL_ctrl) \
    REQUIRED_FUNCTION(SSL_add_client_CA) \
    REQUIRED_FUNCTION(SSL_set_alpn_protos) \
    REQUIRED_FUNCTION(SSL_set_quiet_shutdown) \
    REQUIRED_FUNCTION(SSL_CTX_callback_ctrl) \
    REQUIRED_FUNCTION(SSL_CTX_check_private_key) \
    FALLBACK_FUNCTION(SSL_CTX_config) \
    REQUIRED_FUNCTION(SSL_CTX_ctrl) \
    REQUIRED_FUNCTION(SSL_CTX_free) \
    REQUIRED_FUNCTION(SSL_CTX_get_ex_data) \
    FALLBACK_FUNCTION(SSL_is_init_finished) \
    REQUIRED_FUNCTION(SSL_CTX_new) \
    REQUIRED_FUNCTION(SSL_CTX_sess_set_new_cb) \
    REQUIRED_FUNCTION(SSL_CTX_sess_set_remove_cb) \
    REQUIRED_FUNCTION(SSL_CTX_remove_session) \
    LIGHTUP_FUNCTION(SSL_CTX_set_alpn_protos) \
    LIGHTUP_FUNCTION(SSL_CTX_set_alpn_select_cb) \
    REQUIRED_FUNCTION(SSL_CTX_set_cipher_list) \
    LIGHTUP_FUNCTION(SSL_CTX_set_ciphersuites) \
    REQUIRED_FUNCTION(SSL_CTX_set_client_cert_cb) \
    REQUIRED_FUNCTION(SSL_CTX_set_ex_data) \
    FALLBACK_FUNCTION(SSL_CTX_set_keylog_callback) \
    REQUIRED_FUNCTION(SSL_CTX_set_quiet_shutdown) \
    FALLBACK_FUNCTION(SSL_CTX_set_options) \
    FALLBACK_FUNCTION(SSL_CTX_set_security_level) \
    REQUIRED_FUNCTION(SSL_CTX_set_session_id_context) \
    REQUIRED_FUNCTION(SSL_CTX_set_verify) \
    REQUIRED_FUNCTION(SSL_CTX_use_certificate) \
    REQUIRED_FUNCTION(SSL_CTX_use_PrivateKey) \
    REQUIRED_FUNCTION(SSL_do_handshake) \
    REQUIRED_FUNCTION(SSL_free) \
    REQUIRED_FUNCTION(SSL_get_ciphers) \
    REQUIRED_FUNCTION(SSL_get_sigalgs) \
    REQUIRED_FUNCTION(SSL_get_client_CA_list) \
    REQUIRED_FUNCTION(SSL_get_current_cipher) \
    REQUIRED_FUNCTION(SSL_get_error) \
    REQUIRED_FUNCTION(SSL_get_ex_data) \
    REQUIRED_FUNCTION(SSL_get_finished) \
    REQUIRED_FUNCTION(SSL_get_peer_cert_chain) \
    REQUIRED_FUNCTION(SSL_get_peer_finished) \
    REQUIRED_FUNCTION(SSL_get_servername) \
    REQUIRED_FUNCTION(SSL_get_SSL_CTX) \
    REQUIRED_FUNCTION(SSL_get_version) \
    LIGHTUP_FUNCTION(SSL_get0_alpn_selected) \
    RENAMED_FUNCTION(SSL_get1_peer_certificate, SSL_get_peer_certificate) \
    REQUIRED_FUNCTION(SSL_get_certificate) \
    LEGACY_FUNCTION(SSL_library_init) \
    LEGACY_FUNCTION(SSL_load_error_strings) \
    REQUIRED_FUNCTION(SSL_new) \
    REQUIRED_FUNCTION(SSL_peek) \
    REQUIRED_FUNCTION(SSL_read) \
    REQUIRED_FUNCTION(SSL_renegotiate) \
    REQUIRED_FUNCTION(SSL_renegotiate_pending) \
    REQUIRED_FUNCTION(SSL_SESSION_free) \
    REQUIRED_FUNCTION(SSL_SESSION_get_ex_data) \
    REQUIRED_FUNCTION(SSL_SESSION_set_ex_data) \
    LIGHTUP_FUNCTION(SSL_SESSION_get0_hostname) \
    LIGHTUP_FUNCTION(SSL_SESSION_set1_hostname) \
    FALLBACK_FUNCTION(SSL_session_reused) \
    REQUIRED_FUNCTION(SSL_set_accept_state) \
    REQUIRED_FUNCTION(SSL_set_bio) \
    REQUIRED_FUNCTION(SSL_set_cert_cb) \
    REQUIRED_FUNCTION(SSL_set_cipher_list) \
    LIGHTUP_FUNCTION(SSL_set_ciphersuites) \
    REQUIRED_FUNCTION(SSL_set_connect_state) \
    REQUIRED_FUNCTION(SSL_set_ex_data) \
    FALLBACK_FUNCTION(SSL_set_options) \
    REQUIRED_FUNCTION(SSL_set_session) \
    REQUIRED_FUNCTION(SSL_get_session) \
    REQUIRED_FUNCTION(SSL_set_verify) \
    REQUIRED_FUNCTION(SSL_shutdown) \
    LEGACY_FUNCTION(SSL_state) \
    LEGACY_FUNCTION(SSLeay) \
    RENAMED_FUNCTION(TLS_method, SSLv23_method) \
    REQUIRED_FUNCTION(SSL_write) \
    REQUIRED_FUNCTION(SSL_use_certificate) \
    REQUIRED_FUNCTION(SSL_use_PrivateKey) \
    LIGHTUP_FUNCTION(SSL_verify_client_post_handshake) \
    LIGHTUP_FUNCTION(SSL_set_post_handshake_auth) \
    REQUIRED_FUNCTION(SSL_version) \
    REQUIRED_FUNCTION(UI_create_method) \
    REQUIRED_FUNCTION(UI_destroy_method) \
    FALLBACK_FUNCTION(X509_check_host) \
    REQUIRED_FUNCTION(X509_check_purpose) \
    REQUIRED_FUNCTION(X509_cmp_time) \
    REQUIRED_FUNCTION(X509_CRL_free) \
    FALLBACK_FUNCTION(X509_CRL_get0_nextUpdate) \
    REQUIRED_FUNCTION(X509_digest) \
    REQUIRED_FUNCTION(X509_dup) \
    REQUIRED_FUNCTION(X509_EXTENSION_create_by_OBJ) \
    REQUIRED_FUNCTION(X509_EXTENSION_free) \
    REQUIRED_FUNCTION(X509_EXTENSION_get_critical) \
    REQUIRED_FUNCTION(X509_EXTENSION_get_data) \
    REQUIRED_FUNCTION(X509_EXTENSION_get_object) \
    REQUIRED_FUNCTION(X509_free) \
    REQUIRED_FUNCTION(X509_get_default_cert_dir) \
    REQUIRED_FUNCTION(X509_get_default_cert_dir_env) \
    REQUIRED_FUNCTION(X509_get_default_cert_file) \
    REQUIRED_FUNCTION(X509_get_default_cert_file_env) \
    REQUIRED_FUNCTION(X509_get_ex_data) \
    REQUIRED_FUNCTION(X509_get_ext) \
    REQUIRED_FUNCTION(X509_get_ext_by_NID) \
    REQUIRED_FUNCTION(X509_get_ext_count) \
    REQUIRED_FUNCTION(X509_get_ext_d2i) \
    REQUIRED_FUNCTION(X509_get_issuer_name) \
    REQUIRED_FUNCTION(X509_get_serialNumber) \
    REQUIRED_FUNCTION(X509_get_subject_name) \
    FALLBACK_FUNCTION(X509_get_version) \
    FALLBACK_FUNCTION(X509_get_X509_PUBKEY) \
    FALLBACK_FUNCTION(X509_get0_notBefore) \
    FALLBACK_FUNCTION(X509_get0_notAfter) \
    FALLBACK_FUNCTION(X509_set1_notBefore) \
    FALLBACK_FUNCTION(X509_set1_notAfter) \
    FALLBACK_FUNCTION(X509_get0_pubkey_bitstr) \
    FALLBACK_FUNCTION(X509_get0_tbs_sigalg) \
    REQUIRED_FUNCTION(X509_issuer_name_hash) \
    REQUIRED_FUNCTION(X509_NAME_add_entry_by_txt) \
    REQUIRED_FUNCTION(X509_NAME_entry_count) \
    REQUIRED_FUNCTION(X509_NAME_ENTRY_get_data) \
    REQUIRED_FUNCTION(X509_NAME_ENTRY_get_object) \
    REQUIRED_FUNCTION(X509_NAME_free) \
    REQUIRED_FUNCTION(X509_NAME_get_entry) \
    REQUIRED_FUNCTION(X509_NAME_get_index_by_NID) \
    FALLBACK_FUNCTION(X509_NAME_get0_der) \
    REQUIRED_FUNCTION(X509_new) \
    REQUIRED_FUNCTION(X509_PUBKEY_get) \
    FALLBACK_FUNCTION(X509_PUBKEY_get0_param) \
    REQUIRED_FUNCTION(X509_set_ex_data) \
    REQUIRED_FUNCTION(X509_set_pubkey) \
    REQUIRED_FUNCTION(X509_sign) \
    REQUIRED_FUNCTION(X509_subject_name_hash) \
    REQUIRED_FUNCTION(X509_STORE_add_cert) \
    REQUIRED_FUNCTION(X509_STORE_add_crl) \
    REQUIRED_FUNCTION(X509_STORE_CTX_cleanup) \
    REQUIRED_FUNCTION(X509_STORE_CTX_free) \
    REQUIRED_FUNCTION(X509_STORE_CTX_get_current_cert) \
    REQUIRED_FUNCTION(X509_STORE_CTX_get_error) \
    REQUIRED_FUNCTION(X509_STORE_CTX_get_error_depth) \
    FALLBACK_FUNCTION(X509_STORE_CTX_get0_cert) \
    FALLBACK_FUNCTION(X509_STORE_CTX_get0_chain) \
    REQUIRED_FUNCTION(X509_STORE_CTX_get0_param) \
    FALLBACK_FUNCTION(X509_STORE_CTX_get0_store) \
    FALLBACK_FUNCTION(X509_STORE_CTX_get0_untrusted) \
    REQUIRED_FUNCTION(X509_STORE_CTX_get1_chain) \
    REQUIRED_FUNCTION(X509_STORE_CTX_get1_issuer) \
    REQUIRED_FUNCTION(X509_STORE_CTX_init) \
    REQUIRED_FUNCTION(X509_STORE_CTX_new) \
    REQUIRED_FUNCTION(X509_STORE_CTX_set_flags) \
    REQUIRED_FUNCTION(X509_STORE_CTX_set_verify_cb) \
    REQUIRED_FUNCTION(X509_STORE_CTX_set_ex_data) \
    REQUIRED_FUNCTION(X509_STORE_CTX_get_ex_data) \
    REQUIRED_FUNCTION(X509_STORE_free) \
    FALLBACK_FUNCTION(X509_STORE_get0_param) \
    REQUIRED_FUNCTION(X509_STORE_new) \
    REQUIRED_FUNCTION(X509_STORE_set_flags) \
    REQUIRED_FUNCTION(X509V3_EXT_print) \
    FALLBACK_FUNCTION(X509_up_ref) \
    REQUIRED_FUNCTION(X509_verify_cert) \
    REQUIRED_FUNCTION(X509_verify_cert_error_string) \
    REQUIRED_FUNCTION(X509_VERIFY_PARAM_clear_flags) \
    REQUIRED_FUNCTION(X509_VERIFY_PARAM_get_flags) \
    REQUIRED_FUNCTION(X509_VERIFY_PARAM_set_time) \
    LIGHTUP_FUNCTION(EC_GF2m_simple_method) \
    LIGHTUP_FUNCTION(EC_GROUP_get_curve_GF2m) \
    LIGHTUP_FUNCTION(EC_GROUP_set_curve_GF2m) \
    LIGHTUP_FUNCTION(EC_POINT_get_affine_coordinates_GF2m) \
    LIGHTUP_FUNCTION(EC_POINT_set_affine_coordinates_GF2m) \

// Declare pointers to all the used OpenSSL functions
#define REQUIRED_FUNCTION(fn) extern TYPEOF(fn)* fn##_ptr;
#define REQUIRED_FUNCTION_110(fn) extern TYPEOF(fn)* fn##_ptr;
#define LIGHTUP_FUNCTION(fn) extern TYPEOF(fn)* fn##_ptr;
#define FALLBACK_FUNCTION(fn) extern TYPEOF(fn)* fn##_ptr;
#define RENAMED_FUNCTION(fn,oldfn) extern TYPEOF(fn)* fn##_ptr;
#define LEGACY_FUNCTION(fn) extern TYPEOF(fn)* fn##_ptr;
FOR_ALL_OPENSSL_FUNCTIONS
#undef LEGACY_FUNCTION
#undef RENAMED_FUNCTION
#undef FALLBACK_FUNCTION
#undef LIGHTUP_FUNCTION
#undef REQUIRED_FUNCTION_110
#undef REQUIRED_FUNCTION
#if defined(TARGET_ARM) && defined(TARGET_LINUX)
extern TYPEOF(OPENSSL_gmtime)* OPENSSL_gmtime_ptr;
#endif
// Redefine all calls to OpenSSL functions as calls through pointers that are set
// to the functions from the libssl.so selected by the shim.
#define a2d_ASN1_OBJECT a2d_ASN1_OBJECT_ptr
#define ASN1_GENERALIZEDTIME_free ASN1_GENERALIZEDTIME_free_ptr
#define ASN1_d2i_bio ASN1_d2i_bio_ptr
#define ASN1_i2d_bio ASN1_i2d_bio_ptr
#define ASN1_INTEGER_get ASN1_INTEGER_get_ptr
#define ASN1_OBJECT_free ASN1_OBJECT_free_ptr
#define ASN1_OCTET_STRING_free ASN1_OCTET_STRING_free_ptr
#define ASN1_OCTET_STRING_new ASN1_OCTET_STRING_new_ptr
#define ASN1_OCTET_STRING_set ASN1_OCTET_STRING_set_ptr
#define ASN1_STRING_dup ASN1_STRING_dup_ptr
#define ASN1_STRING_free ASN1_STRING_free_ptr
#define ASN1_STRING_print_ex ASN1_STRING_print_ex_ptr
#define ASN1_TIME_free ASN1_TIME_free_ptr
#define ASN1_TIME_new ASN1_TIME_new_ptr
#define ASN1_TIME_set ASN1_TIME_set_ptr
#define ASN1_TIME_to_tm ASN1_TIME_to_tm_ptr
#define BIO_ctrl BIO_ctrl_ptr
#define BIO_ctrl_pending BIO_ctrl_pending_ptr
#define BIO_free BIO_free_ptr
#define BIO_gets BIO_gets_ptr
#define BIO_new BIO_new_ptr
#define BIO_new_file BIO_new_file_ptr
#define BIO_read BIO_read_ptr
#define BIO_up_ref BIO_up_ref_ptr
#define BIO_s_mem BIO_s_mem_ptr
#define BIO_write BIO_write_ptr
#define BN_abs_is_word BN_abs_is_word_ptr
#define BN_bin2bn BN_bin2bn_ptr
#define BN_bn2bin BN_bn2bin_ptr
#define BN_clear_free BN_clear_free_ptr
#define BN_cmp BN_cmp_ptr
#define BN_div BN_div_ptr
#define BN_dup BN_dup_ptr
#define BN_free BN_free_ptr
#define BN_gcd BN_gcd_ptr
#define BN_is_odd BN_is_odd_ptr
#define BN_is_one BN_is_one_ptr
#define BN_is_zero BN_is_zero_ptr
#define BN_mod_inverse BN_mod_inverse_ptr
#define BN_mod_mul BN_mod_mul_ptr
#define BN_mul BN_mul_ptr
#define BN_new BN_new_ptr
#define BN_num_bits BN_num_bits_ptr
#define BN_set_word BN_set_word_ptr
#define BN_sub BN_sub_ptr
#define BN_value_one BN_value_one_ptr
#define BN_CTX_free BN_CTX_free_ptr
#define BN_CTX_new BN_CTX_new_ptr
#define CRYPTO_add_lock CRYPTO_add_lock_ptr
#define CRYPTO_free CRYPTO_free_ptr
#define CRYPTO_get_ex_new_index CRYPTO_get_ex_new_index_ptr
#define CRYPTO_malloc CRYPTO_malloc_ptr
#define CRYPTO_num_locks CRYPTO_num_locks_ptr
#define CRYPTO_set_locking_callback CRYPTO_set_locking_callback_ptr
#define CRYPTO_set_mem_functions CRYPTO_set_mem_functions_ptr
#define d2i_OCSP_RESPONSE d2i_OCSP_RESPONSE_ptr
#define d2i_PKCS12_fp d2i_PKCS12_fp_ptr
#define d2i_PKCS7 d2i_PKCS7_ptr
#define d2i_PKCS7_bio d2i_PKCS7_bio_ptr
#define d2i_PKCS8_PRIV_KEY_INFO d2i_PKCS8_PRIV_KEY_INFO_ptr
#define d2i_PUBKEY d2i_PUBKEY_ptr
#define d2i_RSAPublicKey d2i_RSAPublicKey_ptr
#define d2i_X509 d2i_X509_ptr
#define d2i_X509_bio d2i_X509_bio_ptr
#define d2i_X509_CRL d2i_X509_CRL_ptr
#define d2i_X509_NAME d2i_X509_NAME_ptr
#define DSA_free DSA_free_ptr
#define DSA_generate_key DSA_generate_key_ptr
#define DSA_generate_parameters_ex DSA_generate_parameters_ex_ptr
#define DSA_get0_key DSA_get0_key_ptr
#define DSA_get0_pqg DSA_get0_pqg_ptr
#define DSA_get_method DSA_get_method_ptr
#define DSA_new DSA_new_ptr
#define DSA_OpenSSL DSA_OpenSSL_ptr
#define DSA_set0_key DSA_set0_key_ptr
#define DSA_set0_pqg DSA_set0_pqg_ptr
#define DSA_sign DSA_sign_ptr
#define DSA_size DSA_size_ptr
#define DSA_up_ref DSA_up_ref_ptr
#define DSA_verify DSA_verify_ptr
#define ECDSA_sign ECDSA_sign_ptr
#define ECDSA_size ECDSA_size_ptr
#define ECDSA_verify ECDSA_verify_ptr
#define EC_GFp_mont_method EC_GFp_mont_method_ptr
#define EC_GFp_simple_method EC_GFp_simple_method_ptr
#define EC_GROUP_check EC_GROUP_check_ptr
#define EC_GROUP_free EC_GROUP_free_ptr
#define EC_GROUP_get0_generator EC_GROUP_get0_generator_ptr
#define EC_GROUP_get0_seed EC_GROUP_get0_seed_ptr
#define EC_GROUP_get_cofactor EC_GROUP_get_cofactor_ptr
#define EC_GROUP_get_curve_GFp EC_GROUP_get_curve_GFp_ptr
#define EC_GROUP_get_curve_name EC_GROUP_get_curve_name_ptr
#define EC_GROUP_get_degree EC_GROUP_get_degree_ptr
#define EC_GROUP_get_order EC_GROUP_get_order_ptr
#define EC_GROUP_get_seed_len EC_GROUP_get_seed_len_ptr
#define EC_GROUP_get_field_type EC_GROUP_get_field_type_ptr
#define EC_GROUP_method_of EC_GROUP_method_of_ptr
#define EC_GROUP_new EC_GROUP_new_ptr
#define EC_GROUP_new_by_curve_name EC_GROUP_new_by_curve_name_ptr
#define EC_GROUP_set_curve_GFp EC_GROUP_set_curve_GFp_ptr
#define EC_GROUP_set_generator EC_GROUP_set_generator_ptr
#define EC_GROUP_set_seed EC_GROUP_set_seed_ptr
#define EC_KEY_check_key EC_KEY_check_key_ptr
#define EC_KEY_free EC_KEY_free_ptr
#define EC_KEY_generate_key EC_KEY_generate_key_ptr
#define EC_KEY_get0_group EC_KEY_get0_group_ptr
#define EC_KEY_get0_private_key EC_KEY_get0_private_key_ptr
#define EC_KEY_get0_public_key EC_KEY_get0_public_key_ptr
#define EC_KEY_new EC_KEY_new_ptr
#define EC_KEY_new_by_curve_name EC_KEY_new_by_curve_name_ptr
#define EC_KEY_set_group EC_KEY_set_group_ptr
#define EC_KEY_set_private_key EC_KEY_set_private_key_ptr
#define EC_KEY_set_public_key EC_KEY_set_public_key_ptr
#define EC_KEY_set_public_key_affine_coordinates EC_KEY_set_public_key_affine_coordinates_ptr
#define EC_KEY_up_ref EC_KEY_up_ref_ptr
#define EC_METHOD_get_field_type EC_METHOD_get_field_type_ptr
#define EC_POINT_free EC_POINT_free_ptr
#define EC_POINT_get_affine_coordinates_GFp EC_POINT_get_affine_coordinates_GFp_ptr
#define EC_POINT_mul EC_POINT_mul_ptr
#define EC_POINT_new EC_POINT_new_ptr
#define EC_POINT_set_affine_coordinates_GFp EC_POINT_set_affine_coordinates_GFp_ptr
#define EC_POINT_oct2point EC_POINT_oct2point_ptr
#define ENGINE_by_id ENGINE_by_id_ptr
#define ENGINE_finish ENGINE_finish_ptr
#define ENGINE_free ENGINE_free_ptr
#define ENGINE_init ENGINE_init_ptr
#define ENGINE_load_public_key ENGINE_load_public_key_ptr
#define ENGINE_load_private_key ENGINE_load_private_key_ptr
#define ERR_clear_error ERR_clear_error_ptr
#define ERR_error_string_n ERR_error_string_n_ptr
#define ERR_get_error ERR_get_error_ptr
#define ERR_load_crypto_strings ERR_load_crypto_strings_ptr
#define ERR_new ERR_new_ptr
#define ERR_peek_error ERR_peek_error_ptr
#define ERR_peek_error_line ERR_peek_error_line_ptr
#define ERR_peek_last_error ERR_peek_last_error_ptr
#define ERR_put_error ERR_put_error_ptr
#define ERR_pop_to_mark ERR_pop_to_mark_ptr
#define ERR_reason_error_string ERR_reason_error_string_ptr
#define ERR_set_debug ERR_set_debug_ptr
#define ERR_set_mark ERR_set_mark_ptr
#define ERR_set_error ERR_set_error_ptr
#define EVP_aes_128_cbc EVP_aes_128_cbc_ptr
#define EVP_aes_128_cfb8 EVP_aes_128_cfb8_ptr
#define EVP_aes_128_cfb128 EVP_aes_128_cfb128_ptr
#define EVP_aes_128_ecb EVP_aes_128_ecb_ptr
#define EVP_aes_128_gcm EVP_aes_128_gcm_ptr
#define EVP_aes_128_ccm EVP_aes_128_ccm_ptr
#define EVP_aes_192_cbc EVP_aes_192_cbc_ptr
#define EVP_aes_192_cfb8 EVP_aes_192_cfb8_ptr
#define EVP_aes_192_cfb128 EVP_aes_192_cfb128_ptr
#define EVP_aes_192_ecb EVP_aes_192_ecb_ptr
#define EVP_aes_192_gcm EVP_aes_192_gcm_ptr
#define EVP_aes_192_ccm EVP_aes_192_ccm_ptr
#define EVP_aes_256_cbc EVP_aes_256_cbc_ptr
#define EVP_aes_256_cfb8 EVP_aes_256_cfb8_ptr
#define EVP_aes_256_cfb128 EVP_aes_256_cfb128_ptr
#define EVP_aes_256_ecb EVP_aes_256_ecb_ptr
#define EVP_aes_256_gcm EVP_aes_256_gcm_ptr
#define EVP_aes_256_ccm EVP_aes_256_ccm_ptr
#define EVP_chacha20_poly1305 EVP_chacha20_poly1305_ptr
#define EVP_CIPHER_CTX_cleanup EVP_CIPHER_CTX_cleanup_ptr
#define EVP_CIPHER_CTX_ctrl EVP_CIPHER_CTX_ctrl_ptr
#define EVP_CIPHER_CTX_free EVP_CIPHER_CTX_free_ptr
#define EVP_CIPHER_CTX_init EVP_CIPHER_CTX_init_ptr
#define EVP_CIPHER_CTX_new EVP_CIPHER_CTX_new_ptr
#define EVP_CIPHER_CTX_reset EVP_CIPHER_CTX_reset_ptr
#define EVP_CIPHER_CTX_set_key_length EVP_CIPHER_CTX_set_key_length_ptr
#define EVP_CIPHER_CTX_set_padding EVP_CIPHER_CTX_set_padding_ptr
#define EVP_CIPHER_get_nid EVP_CIPHER_get_nid_ptr
#define EVP_CipherFinal_ex EVP_CipherFinal_ex_ptr
#define EVP_CipherInit_ex EVP_CipherInit_ex_ptr
#define EVP_CipherUpdate EVP_CipherUpdate_ptr
#define EVP_des_cbc EVP_des_cbc_ptr
#define EVP_des_cfb8 EVP_des_cfb8_ptr
#define EVP_des_ecb EVP_des_ecb_ptr
#define EVP_des_ede3 EVP_des_ede3_ptr
#define EVP_des_ede3_cfb8 EVP_des_ede3_cfb8_ptr
#define EVP_des_ede3_cfb64 EVP_des_ede3_cfb64_ptr
#define EVP_des_ede3_cbc EVP_des_ede3_cbc_ptr
#define EVP_DigestFinal_ex EVP_DigestFinal_ex_ptr
#define EVP_DigestFinalXOF EVP_DigestFinalXOF_ptr
#define EVP_DigestInit_ex EVP_DigestInit_ex_ptr
#define EVP_DigestSqueeze EVP_DigestSqueeze_ptr
#define EVP_DigestUpdate EVP_DigestUpdate_ptr
#define EVP_get_digestbyname EVP_get_digestbyname_ptr
#define EVP_md5 EVP_md5_ptr
#define EVP_KDF_CTX_free EVP_KDF_CTX_free_ptr
#define EVP_KDF_CTX_new EVP_KDF_CTX_new_ptr
#define EVP_KDF_derive EVP_KDF_derive_ptr
#define EVP_KDF_fetch EVP_KDF_fetch_ptr
#define EVP_KDF_free EVP_KDF_free_ptr
#define EVP_KEM_fetch EVP_KEM_fetch_ptr
#define EVP_KEM_free EVP_KEM_free_ptr
#define EVP_MAC_fetch EVP_MAC_fetch_ptr
#define EVP_MAC_final EVP_MAC_final_ptr
#define EVP_MAC_free EVP_MAC_free_ptr
#define EVP_MAC_CTX_new EVP_MAC_CTX_new_ptr
#define EVP_MAC_CTX_free EVP_MAC_CTX_free_ptr
#define EVP_MAC_CTX_set_params EVP_MAC_CTX_set_params_ptr
#define EVP_MAC_CTX_dup EVP_MAC_CTX_dup_ptr
#define EVP_MAC_init EVP_MAC_init_ptr
#define EVP_MAC_update EVP_MAC_update_ptr
#define EVP_MD_CTX_copy_ex EVP_MD_CTX_copy_ex_ptr
#define EVP_MD_CTX_free EVP_MD_CTX_free_ptr
#define EVP_MD_CTX_new EVP_MD_CTX_new_ptr
#define EVP_MD_CTX_set_flags EVP_MD_CTX_set_flags_ptr
#define EVP_MD_fetch EVP_MD_fetch_ptr
#define EVP_MD_get_size EVP_MD_get_size_ptr
#define EVP_PKCS82PKEY EVP_PKCS82PKEY_ptr
#define EVP_PKEY2PKCS8 EVP_PKEY2PKCS8_ptr
#define EVP_PKEY_CTX_ctrl EVP_PKEY_CTX_ctrl_ptr
#define EVP_PKEY_CTX_ctrl_str EVP_PKEY_CTX_ctrl_str_ptr
#define EVP_PKEY_CTX_free EVP_PKEY_CTX_free_ptr
#define EVP_PKEY_CTX_get0_pkey EVP_PKEY_CTX_get0_pkey_ptr
#define EVP_PKEY_CTX_new EVP_PKEY_CTX_new_ptr
#define EVP_PKEY_CTX_new_id EVP_PKEY_CTX_new_id_ptr
#define EVP_PKEY_CTX_set_params EVP_PKEY_CTX_set_params_ptr
#define EVP_PKEY_CTX_set_rsa_keygen_bits EVP_PKEY_CTX_set_rsa_keygen_bits_ptr
#define EVP_PKEY_CTX_set_rsa_oaep_md EVP_PKEY_CTX_set_rsa_oaep_md_ptr
#define EVP_PKEY_CTX_set_rsa_padding EVP_PKEY_CTX_set_rsa_padding_ptr
#define EVP_PKEY_CTX_set_rsa_pss_saltlen EVP_PKEY_CTX_set_rsa_pss_saltlen_ptr
#define EVP_PKEY_CTX_set_signature_md EVP_PKEY_CTX_set_signature_md_ptr
#define EVP_PKEY_check EVP_PKEY_check_ptr
#define EVP_PKEY_decapsulate EVP_PKEY_decapsulate_ptr
#define EVP_PKEY_decapsulate_init EVP_PKEY_decapsulate_init_ptr
#define EVP_PKEY_decrypt_init EVP_PKEY_decrypt_init_ptr
#define EVP_PKEY_decrypt EVP_PKEY_decrypt_ptr
#define EVP_PKEY_derive_set_peer EVP_PKEY_derive_set_peer_ptr
#define EVP_PKEY_derive_init EVP_PKEY_derive_init_ptr
#define EVP_PKEY_derive EVP_PKEY_derive_ptr
#define EVP_PKEY_encapsulate EVP_PKEY_encapsulate_ptr
#define EVP_PKEY_encapsulate_init EVP_PKEY_encapsulate_init_ptr
#define EVP_PKEY_encrypt_init EVP_PKEY_encrypt_init_ptr
#define EVP_PKEY_encrypt EVP_PKEY_encrypt_ptr
#define EVP_PKEY_free EVP_PKEY_free_ptr
#define EVP_PKEY_fromdata EVP_PKEY_fromdata_ptr
#define EVP_PKEY_fromdata_init EVP_PKEY_fromdata_init_ptr
#define EVP_PKEY_get_base_id EVP_PKEY_get_base_id_ptr
#define EVP_PKEY_get_bits EVP_PKEY_get_bits_ptr
#define EVP_PKEY_get0_RSA EVP_PKEY_get0_RSA_ptr
#define EVP_PKEY_get0_type_name EVP_PKEY_get0_type_name_ptr
#define EVP_PKEY_get1_DSA EVP_PKEY_get1_DSA_ptr
#define EVP_PKEY_get1_EC_KEY EVP_PKEY_get1_EC_KEY_ptr
#define EVP_PKEY_get1_RSA EVP_PKEY_get1_RSA_ptr
#define EVP_PKEY_is_a EVP_PKEY_is_a_ptr
#define EVP_PKEY_keygen EVP_PKEY_keygen_ptr
#define EVP_PKEY_keygen_init EVP_PKEY_keygen_init_ptr
#define EVP_PKEY_new EVP_PKEY_new_ptr
#define EVP_PKEY_CTX_new_from_name EVP_PKEY_CTX_new_from_name_ptr
#define EVP_PKEY_CTX_new_from_pkey EVP_PKEY_CTX_new_from_pkey_ptr
#define EVP_PKEY_CTX_new_from_name EVP_PKEY_CTX_new_from_name_ptr
#define EVP_PKEY_CTX_set_params EVP_PKEY_CTX_set_params_ptr
#define EVP_PKEY_public_check EVP_PKEY_public_check_ptr
#define EVP_PKEY_set1_DSA EVP_PKEY_set1_DSA_ptr
#define EVP_PKEY_set1_EC_KEY EVP_PKEY_set1_EC_KEY_ptr
#define EVP_PKEY_set1_RSA EVP_PKEY_set1_RSA_ptr
#define EVP_PKEY_sign_init EVP_PKEY_sign_init_ptr
#define EVP_PKEY_sign_message_init EVP_PKEY_sign_message_init_ptr
#define EVP_PKEY_sign EVP_PKEY_sign_ptr
#define EVP_PKEY_up_ref EVP_PKEY_up_ref_ptr
#define EVP_PKEY_verify_init EVP_PKEY_verify_init_ptr
#define EVP_PKEY_verify_message_init EVP_PKEY_verify_message_init_ptr
#define EVP_PKEY_verify EVP_PKEY_verify_ptr
#define EVP_PKEY_get_bn_param EVP_PKEY_get_bn_param_ptr
#define EVP_PKEY_get_utf8_string_param EVP_PKEY_get_utf8_string_param_ptr
#define EVP_PKEY_get_octet_string_param EVP_PKEY_get_octet_string_param_ptr
#define EVP_rc2_cbc EVP_rc2_cbc_ptr
#define EVP_rc2_ecb EVP_rc2_ecb_ptr
#define EVP_sha1 EVP_sha1_ptr
#define EVP_sha256 EVP_sha256_ptr
#define EVP_sha384 EVP_sha384_ptr
#define EVP_sha512 EVP_sha512_ptr
#define EVP_sha3_256 EVP_sha3_256_ptr
#define EVP_sha3_384 EVP_sha3_384_ptr
#define EVP_sha3_512 EVP_sha3_512_ptr
#define EVP_shake128 EVP_shake128_ptr
#define EVP_shake256 EVP_shake256_ptr
#define EVP_SIGNATURE_fetch EVP_SIGNATURE_fetch_ptr
#define EVP_SIGNATURE_free EVP_SIGNATURE_free_ptr
#define GENERAL_NAMES_free GENERAL_NAMES_free_ptr
#define HMAC HMAC_ptr
#define HMAC_CTX_cleanup HMAC_CTX_cleanup_ptr
#define HMAC_CTX_copy HMAC_CTX_copy_ptr
#define HMAC_CTX_free HMAC_CTX_free_ptr
#define HMAC_CTX_init HMAC_CTX_init_ptr
#define HMAC_CTX_new HMAC_CTX_new_ptr
#define HMAC_Final HMAC_Final_ptr
#define HMAC_Init_ex HMAC_Init_ex_ptr
#define HMAC_Update HMAC_Update_ptr
#define i2d_ASN1_INTEGER i2d_ASN1_INTEGER_ptr
#define i2d_ASN1_TYPE i2d_ASN1_TYPE_ptr
#define i2d_OCSP_REQUEST i2d_OCSP_REQUEST_ptr
#define i2d_OCSP_RESPONSE i2d_OCSP_RESPONSE_ptr
#define i2d_PKCS7 i2d_PKCS7_ptr
#define i2d_PKCS8_PRIV_KEY_INFO i2d_PKCS8_PRIV_KEY_INFO_ptr
#define i2d_PUBKEY i2d_PUBKEY_ptr
#define i2d_X509 i2d_X509_ptr
#define i2d_X509_PUBKEY i2d_X509_PUBKEY_ptr
#define OBJ_ln2nid OBJ_ln2nid_ptr
#define OBJ_nid2ln OBJ_nid2ln_ptr
#define OBJ_nid2sn OBJ_nid2sn_ptr
#define OBJ_nid2obj OBJ_nid2obj_ptr
#define OBJ_obj2nid OBJ_obj2nid_ptr
#define OBJ_obj2txt OBJ_obj2txt_ptr
#define OBJ_sn2nid OBJ_sn2nid_ptr
#define OBJ_txt2nid OBJ_txt2nid_ptr
#define OBJ_txt2obj OBJ_txt2obj_ptr
#define OCSP_basic_verify OCSP_basic_verify_ptr
#define OCSP_BASICRESP_free OCSP_BASICRESP_free_ptr
#define OCSP_cert_to_id OCSP_cert_to_id_ptr
#define OCSP_check_nonce OCSP_check_nonce_ptr
#define OCSP_CERTID_free OCSP_CERTID_free_ptr
#define OCSP_request_add0_id OCSP_request_add0_id_ptr
#define OCSP_REQUEST_free OCSP_REQUEST_free_ptr
#define OCSP_REQUEST_new OCSP_REQUEST_new_ptr
#define OCSP_resp_find_status OCSP_resp_find_status_ptr
#define OCSP_response_get1_basic OCSP_response_get1_basic_ptr
#define OCSP_RESPONSE_free OCSP_RESPONSE_free_ptr
#define OCSP_RESPONSE_new OCSP_RESPONSE_new_ptr
#define OPENSSL_add_all_algorithms_conf OPENSSL_add_all_algorithms_conf_ptr
#define OPENSSL_cleanse OPENSSL_cleanse_ptr
#define OPENSSL_gmtime OPENSSL_gmtime_ptr
#define OPENSSL_init_ssl OPENSSL_init_ssl_ptr
#define OPENSSL_sk_free OPENSSL_sk_free_ptr
#define OPENSSL_sk_new_null OPENSSL_sk_new_null_ptr
#define OPENSSL_sk_num OPENSSL_sk_num_ptr
#define OPENSSL_sk_pop OPENSSL_sk_pop_ptr
#define OPENSSL_sk_pop_free OPENSSL_sk_pop_free_ptr
#define OPENSSL_sk_push OPENSSL_sk_push_ptr
#define OPENSSL_sk_value OPENSSL_sk_value_ptr
#define OpenSSL_version_num OpenSSL_version_num_ptr
#define OSSL_LIB_CTX_free OSSL_LIB_CTX_free_ptr
#define OSSL_LIB_CTX_new OSSL_LIB_CTX_new_ptr
#define OSSL_PROVIDER_load OSSL_PROVIDER_load_ptr
#define OSSL_PROVIDER_try_load OSSL_PROVIDER_try_load_ptr
#define OSSL_PROVIDER_unload OSSL_PROVIDER_unload_ptr
#define OSSL_STORE_close OSSL_STORE_close_ptr
#define OSSL_STORE_eof OSSL_STORE_eof_ptr
#define OSSL_STORE_INFO_free OSSL_STORE_INFO_free_ptr
#define OSSL_STORE_INFO_get_type OSSL_STORE_INFO_get_type_ptr
#define OSSL_STORE_INFO_get1_PKEY OSSL_STORE_INFO_get1_PKEY_ptr
#define OSSL_STORE_INFO_get1_PUBKEY OSSL_STORE_INFO_get1_PUBKEY_ptr
#define OSSL_STORE_load OSSL_STORE_load_ptr
#define OSSL_STORE_open_ex OSSL_STORE_open_ex_ptr
#define OSSL_PARAM_construct_octet_string OSSL_PARAM_construct_octet_string_ptr
#define OSSL_PARAM_construct_utf8_string OSSL_PARAM_construct_utf8_string_ptr
#define OSSL_PARAM_construct_int OSSL_PARAM_construct_int_ptr
#define OSSL_PARAM_construct_int32 OSSL_PARAM_construct_int32_ptr
#define OSSL_PARAM_construct_end OSSL_PARAM_construct_end_ptr
#define PKCS8_PRIV_KEY_INFO_free PKCS8_PRIV_KEY_INFO_free_ptr
#define PEM_read_bio_PKCS7 PEM_read_bio_PKCS7_ptr
#define PEM_read_bio_X509 PEM_read_bio_X509_ptr
#define PEM_read_bio_X509_AUX PEM_read_bio_X509_AUX_ptr
#define PEM_read_bio_X509_CRL PEM_read_bio_X509_CRL_ptr
#define PEM_write_bio_X509_CRL PEM_write_bio_X509_CRL_ptr
#define PKCS5_PBKDF2_HMAC PKCS5_PBKDF2_HMAC_ptr
#define PKCS12_free PKCS12_free_ptr
#define PKCS12_parse PKCS12_parse_ptr
#define PKCS7_sign PKCS7_sign_ptr
#define PKCS7_free PKCS7_free_ptr
#define RAND_bytes RAND_bytes_ptr
#define RAND_poll RAND_poll_ptr
#define RSA_check_key RSA_check_key_ptr
#define RSA_free RSA_free_ptr
#define RSA_generate_key_ex RSA_generate_key_ex_ptr
#define RSA_get0_crt_params RSA_get0_crt_params_ptr
#define RSA_get0_factors RSA_get0_factors_ptr
#define RSA_get0_key RSA_get0_key_ptr
#define RSA_get_method RSA_get_method_ptr
#define RSA_get_multi_prime_extra_count RSA_get_multi_prime_extra_count_ptr
#define RSA_meth_get_flags RSA_meth_get_flags_ptr
#define RSA_new RSA_new_ptr
#define RSA_pkey_ctx_ctrl RSA_pkey_ctx_ctrl_ptr
#define RSA_PKCS1_OpenSSL RSA_PKCS1_OpenSSL_ptr
#define RSA_public_decrypt RSA_public_decrypt_ptr
#define RSA_public_encrypt RSA_public_encrypt_ptr
#define RSA_set0_crt_params RSA_set0_crt_params_ptr
#define RSA_set0_factors RSA_set0_factors_ptr
#define RSA_set0_key RSA_set0_key_ptr
#define RSA_set_method RSA_set_method_ptr
#define RSA_size RSA_size_ptr
#define RSA_test_flags RSA_test_flags_ptr
#define RSA_up_ref RSA_up_ref_ptr
#define RSA_verify RSA_verify_ptr
#define SSL_CIPHER_get_bits SSL_CIPHER_get_bits_ptr
#define SSL_CIPHER_find SSL_CIPHER_find_ptr
#define SSL_CIPHER_get_id SSL_CIPHER_get_id_ptr
#define SSL_CIPHER_get_name SSL_CIPHER_get_name_ptr
#define SSL_CIPHER_get_version SSL_CIPHER_get_version_ptr
#define SSL_ctrl SSL_ctrl_ptr
#define SSL_add_client_CA SSL_add_client_CA_ptr
#define SSL_set_alpn_protos SSL_set_alpn_protos_ptr
#define SSL_set_quiet_shutdown SSL_set_quiet_shutdown_ptr
#define SSL_CTX_callback_ctrl SSL_CTX_callback_ctrl_ptr
#define SSL_CTX_check_private_key SSL_CTX_check_private_key_ptr
#define SSL_CTX_config SSL_CTX_config_ptr
#define SSL_CTX_ctrl SSL_CTX_ctrl_ptr
#define SSL_CTX_free SSL_CTX_free_ptr
#define SSL_CTX_get_ex_data SSL_CTX_get_ex_data_ptr
#define SSL_CTX_new SSL_CTX_new_ptr
#define SSL_CTX_sess_set_new_cb SSL_CTX_sess_set_new_cb_ptr
#define SSL_CTX_sess_set_remove_cb SSL_CTX_sess_set_remove_cb_ptr
#define SSL_CTX_remove_session SSL_CTX_remove_session_ptr
#define SSL_CTX_set_alpn_protos SSL_CTX_set_alpn_protos_ptr
#define SSL_CTX_set_alpn_select_cb SSL_CTX_set_alpn_select_cb_ptr
#define SSL_CTX_set_cipher_list SSL_CTX_set_cipher_list_ptr
#define SSL_CTX_set_ciphersuites SSL_CTX_set_ciphersuites_ptr
#define SSL_CTX_set_client_cert_cb SSL_CTX_set_client_cert_cb_ptr
#define SSL_CTX_set_ex_data SSL_CTX_set_ex_data_ptr
#define SSL_CTX_set_options SSL_CTX_set_options_ptr
#define SSL_CTX_set_keylog_callback SSL_CTX_set_keylog_callback_ptr
#define SSL_CTX_set_quiet_shutdown SSL_CTX_set_quiet_shutdown_ptr
#define SSL_CTX_set_security_level SSL_CTX_set_security_level_ptr
#define SSL_CTX_set_session_id_context SSL_CTX_set_session_id_context_ptr
#define SSL_CTX_set_verify SSL_CTX_set_verify_ptr
#define SSL_CTX_use_certificate SSL_CTX_use_certificate_ptr
#define SSL_CTX_use_PrivateKey SSL_CTX_use_PrivateKey_ptr
#define SSL_do_handshake SSL_do_handshake_ptr
#define SSL_free SSL_free_ptr
#define SSL_get_ciphers SSL_get_ciphers_ptr
#define SSL_get_sigalgs SSL_get_sigalgs_ptr
#define SSL_get_client_CA_list SSL_get_client_CA_list_ptr
#define SSL_get_certificate SSL_get_certificate_ptr
#define SSL_get_current_cipher SSL_get_current_cipher_ptr
#define SSL_get_error SSL_get_error_ptr
#define SSL_get_ex_data SSL_get_ex_data_ptr
#define SSL_get_finished SSL_get_finished_ptr
#define SSL_get_peer_cert_chain SSL_get_peer_cert_chain_ptr
#define SSL_get_peer_finished SSL_get_peer_finished_ptr
#define SSL_get_servername SSL_get_servername_ptr
#define SSL_get_SSL_CTX SSL_get_SSL_CTX_ptr
#define SSL_get_version SSL_get_version_ptr
#define SSL_get0_alpn_selected SSL_get0_alpn_selected_ptr
#define SSL_get1_peer_certificate SSL_get1_peer_certificate_ptr
#define SSL_is_init_finished SSL_is_init_finished_ptr
#define SSL_library_init SSL_library_init_ptr
#define SSL_load_error_strings SSL_load_error_strings_ptr
#define SSL_new SSL_new_ptr
#define SSL_peek SSL_peek_ptr
#define SSL_state_string_long SSL_state_string_long_ptr
#define SSL_read SSL_read_ptr
#define SSL_renegotiate SSL_renegotiate_ptr
#define SSL_renegotiate_pending SSL_renegotiate_pending_ptr
#define SSL_SESSION_free SSL_SESSION_free_ptr
#define SSL_SESSION_get0_hostname SSL_SESSION_get0_hostname_ptr
#define SSL_SESSION_set1_hostname SSL_SESSION_set1_hostname_ptr
#define SSL_session_reused SSL_session_reused_ptr
#define SSL_SESSION_get_ex_data SSL_SESSION_get_ex_data_ptr
#define SSL_SESSION_set_ex_data SSL_SESSION_set_ex_data_ptr
#define SSL_set_accept_state SSL_set_accept_state_ptr
#define SSL_set_bio SSL_set_bio_ptr
#define SSL_set_cert_cb  SSL_set_cert_cb_ptr
#define SSL_set_cipher_list SSL_set_cipher_list_ptr
#define SSL_set_ciphersuites SSL_set_ciphersuites_ptr
#define SSL_set_connect_state SSL_set_connect_state_ptr
#define SSL_set_ex_data SSL_set_ex_data_ptr
#define SSL_set_options SSL_set_options_ptr
#define SSL_set_session SSL_set_session_ptr
#define SSL_get_session SSL_get_session_ptr
#define SSL_set_verify SSL_set_verify_ptr
#define SSL_shutdown SSL_shutdown_ptr
#define SSL_state SSL_state_ptr
#define SSLeay SSLeay_ptr
#define SSL_write SSL_write_ptr
#define SSL_use_certificate SSL_use_certificate_ptr
#define SSL_use_PrivateKey SSL_use_PrivateKey_ptr
#define SSL_verify_client_post_handshake SSL_verify_client_post_handshake_ptr
#define SSL_set_post_handshake_auth SSL_set_post_handshake_auth_ptr
#define SSL_version SSL_version_ptr
#define TLS_method TLS_method_ptr
#define UI_create_method UI_create_method_ptr
#define UI_destroy_method UI_destroy_method_ptr
#define X509_check_host X509_check_host_ptr
#define X509_check_purpose X509_check_purpose_ptr
#define X509_cmp_time X509_cmp_time_ptr
#define X509_CRL_free X509_CRL_free_ptr
#define X509_CRL_get0_nextUpdate X509_CRL_get0_nextUpdate_ptr
#define X509_digest X509_digest_ptr
#define X509_dup X509_dup_ptr
#define X509_EXTENSION_create_by_OBJ X509_EXTENSION_create_by_OBJ_ptr
#define X509_EXTENSION_free X509_EXTENSION_free_ptr
#define X509_EXTENSION_get_critical X509_EXTENSION_get_critical_ptr
#define X509_EXTENSION_get_data X509_EXTENSION_get_data_ptr
#define X509_EXTENSION_get_object X509_EXTENSION_get_object_ptr
#define X509_free X509_free_ptr
#define X509_get0_notAfter X509_get0_notAfter_ptr
#define X509_get0_notBefore X509_get0_notBefore_ptr
#define X509_set1_notAfter X509_set1_notAfter_ptr
#define X509_set1_notBefore X509_set1_notBefore_ptr
#define X509_get0_pubkey_bitstr X509_get0_pubkey_bitstr_ptr
#define X509_get0_tbs_sigalg X509_get0_tbs_sigalg_ptr
#define X509_get_default_cert_dir X509_get_default_cert_dir_ptr
#define X509_get_default_cert_dir_env X509_get_default_cert_dir_env_ptr
#define X509_get_default_cert_file X509_get_default_cert_file_ptr
#define X509_get_default_cert_file_env X509_get_default_cert_file_env_ptr
#define X509_get_ex_data X509_get_ex_data_ptr
#define X509_get_ext X509_get_ext_ptr
#define X509_get_ext_by_NID X509_get_ext_by_NID_ptr
#define X509_get_ext_count X509_get_ext_count_ptr
#define X509_get_ext_d2i X509_get_ext_d2i_ptr
#define X509_get_issuer_name X509_get_issuer_name_ptr
#define X509_get_serialNumber X509_get_serialNumber_ptr
#define X509_get_subject_name X509_get_subject_name_ptr
#define X509_get_X509_PUBKEY X509_get_X509_PUBKEY_ptr
#define X509_get_version X509_get_version_ptr
#define X509_issuer_name_hash X509_issuer_name_hash_ptr
#define X509_NAME_add_entry_by_txt X509_NAME_add_entry_by_txt_ptr
#define X509_NAME_entry_count X509_NAME_entry_count_ptr
#define X509_NAME_ENTRY_get_data X509_NAME_ENTRY_get_data_ptr
#define X509_NAME_ENTRY_get_object X509_NAME_ENTRY_get_object_ptr
#define X509_NAME_free X509_NAME_free_ptr
#define X509_NAME_get0_der X509_NAME_get0_der_ptr
#define X509_NAME_get_entry X509_NAME_get_entry_ptr
#define X509_NAME_get_index_by_NID X509_NAME_get_index_by_NID_ptr
#define X509_new X509_new_ptr
#define X509_PUBKEY_get0_param X509_PUBKEY_get0_param_ptr
#define X509_PUBKEY_get X509_PUBKEY_get_ptr
#define X509_set_ex_data X509_set_ex_data_ptr
#define X509_set_pubkey X509_set_pubkey_ptr
#define X509_subject_name_hash X509_subject_name_hash_ptr
#define X509_sign X509_sign_ptr
#define X509_STORE_add_cert X509_STORE_add_cert_ptr
#define X509_STORE_add_crl X509_STORE_add_crl_ptr
#define X509_STORE_CTX_cleanup X509_STORE_CTX_cleanup_ptr
#define X509_STORE_CTX_free X509_STORE_CTX_free_ptr
#define X509_STORE_CTX_get_current_cert X509_STORE_CTX_get_current_cert_ptr
#define X509_STORE_CTX_get0_cert X509_STORE_CTX_get0_cert_ptr
#define X509_STORE_CTX_get0_chain X509_STORE_CTX_get0_chain_ptr
#define X509_STORE_CTX_get0_param X509_STORE_CTX_get0_param_ptr
#define X509_STORE_CTX_get0_store X509_STORE_CTX_get0_store_ptr
#define X509_STORE_CTX_get0_untrusted X509_STORE_CTX_get0_untrusted_ptr
#define X509_STORE_CTX_get1_chain X509_STORE_CTX_get1_chain_ptr
#define X509_STORE_CTX_get1_issuer X509_STORE_CTX_get1_issuer_ptr
#define X509_STORE_CTX_get_error X509_STORE_CTX_get_error_ptr
#define X509_STORE_CTX_get_error_depth X509_STORE_CTX_get_error_depth_ptr
#define X509_STORE_CTX_init X509_STORE_CTX_init_ptr
#define X509_STORE_CTX_new X509_STORE_CTX_new_ptr
#define X509_STORE_CTX_set_flags X509_STORE_CTX_set_flags_ptr
#define X509_STORE_CTX_set_verify_cb X509_STORE_CTX_set_verify_cb_ptr
#define X509_STORE_CTX_set_ex_data X509_STORE_CTX_set_ex_data_ptr
#define X509_STORE_CTX_get_ex_data X509_STORE_CTX_get_ex_data_ptr
#define X509_STORE_free X509_STORE_free_ptr
#define X509_STORE_get0_param X509_STORE_get0_param_ptr
#define X509_STORE_new X509_STORE_new_ptr
#define X509_STORE_set_flags X509_STORE_set_flags_ptr
#define X509V3_EXT_print X509V3_EXT_print_ptr
#define X509_up_ref X509_up_ref_ptr
#define X509_verify_cert X509_verify_cert_ptr
#define X509_verify_cert_error_string X509_verify_cert_error_string_ptr
#define X509_VERIFY_PARAM_clear_flags X509_VERIFY_PARAM_clear_flags_ptr
#define X509_VERIFY_PARAM_get_flags X509_VERIFY_PARAM_get_flags_ptr
#define X509_VERIFY_PARAM_set_time X509_VERIFY_PARAM_set_time_ptr
#define EC_GF2m_simple_method EC_GF2m_simple_method_ptr
#define EC_GROUP_get_curve_GF2m EC_GROUP_get_curve_GF2m_ptr
#define EC_GROUP_set_curve_GF2m EC_GROUP_set_curve_GF2m_ptr
#define EC_POINT_get_affine_coordinates_GF2m EC_POINT_get_affine_coordinates_GF2m_ptr
#define EC_POINT_set_affine_coordinates_GF2m EC_POINT_set_affine_coordinates_GF2m_ptr


// STACK_OF types will have been declared with inline functions to handle the pointer casting.
// Since these inline functions are strongly bound to the OPENSSL_sk_* functions in 1.1 we need to
// rebind things here.
#if OPENSSL_VERSION_NUMBER >= OPENSSL_VERSION_1_1_0_RTM && OPENSSL_VERSION_NUMBER < OPENSSL_VERSION_3_0_RTM
// type-safe OPENSSL_sk_free
#define sk_GENERAL_NAME_free(stack) OPENSSL_sk_free((OPENSSL_STACK*)(1 ? stack : (STACK_OF(GENERAL_NAME)*)0))
#define sk_X509_free(stack) OPENSSL_sk_free((OPENSSL_STACK*)(1 ? stack : (STACK_OF(X509)*)0))

// type-safe OPENSSL_sk_num
#define sk_ASN1_OBJECT_num(stack) OPENSSL_sk_num((const OPENSSL_STACK*)(1 ? stack : (const STACK_OF(ASN1_OBJECT)*)0))
#define sk_GENERAL_NAME_num(stack) OPENSSL_sk_num((const OPENSSL_STACK*)(1 ? stack : (const STACK_OF(GENERAL_NAME)*)0))
#define sk_SSL_CIPHER_num(stack) OPENSSL_sk_num((const OPENSSL_STACK*)(1 ? stack : (const STACK_OF(SSL_CIPHER)*)0))
#define sk_X509_NAME_num(stack) OPENSSL_sk_num((const OPENSSL_STACK*)(1 ? stack : (const STACK_OF(X509_NAME)*)0))
#define sk_X509_num(stack) OPENSSL_sk_num((const OPENSSL_STACK*)(1 ? stack : (const STACK_OF(X509)*)0))

// type-safe OPENSSL_sk_new_null
#define sk_X509_new_null() (STACK_OF(X509)*)OPENSSL_sk_new_null()

// type-safe OPENSSL_sk_push
#define sk_X509_push(stack,value) OPENSSL_sk_push((OPENSSL_STACK*)(1 ? stack : (STACK_OF(X509)*)0), (const void*)(1 ? value : (X509*)0))

// type-safe OPENSSL_sk_pop
#define sk_X509_pop(stack) OPENSSL_sk_pop((OPENSSL_STACK*)(1 ? stack : (STACK_OF(X509)*)0))

// type-safe OPENSSL_sk_pop_free
#define sk_X509_pop_free(stack, freefunc) OPENSSL_sk_pop_free((OPENSSL_STACK*)(1 ? stack : (STACK_OF(X509)*)0), (OPENSSL_sk_freefunc)(1 ? freefunc : (sk_X509_freefunc)0))

// type-safe OPENSSL_sk_value
#define sk_ASN1_OBJECT_value(stack, idx) (ASN1_OBJECT*)OPENSSL_sk_value((const OPENSSL_STACK*)(1 ? stack : (const STACK_OF(ASN1_OBJECT)*)0), idx)
#define sk_GENERAL_NAME_value(stack, idx) (GENERAL_NAME*)OPENSSL_sk_value((const OPENSSL_STACK*)(1 ? stack : (const STACK_OF(GENERAL_NAME)*)0), idx)
#define sk_X509_NAME_value(stack, idx) (X509_NAME*)OPENSSL_sk_value((const OPENSSL_STACK*)(1 ? stack : (const STACK_OF(X509_NAME)*)0), idx)
#define sk_X509_value(stack, idx) (X509*)OPENSSL_sk_value((const OPENSSL_STACK*)(1 ? stack : (const STACK_OF(X509)*)0), idx)

#elif OPENSSL_VERSION_NUMBER < OPENSSL_VERSION_1_1_0_RTM

#define sk_free OPENSSL_sk_free_ptr
#define sk_new_null OPENSSL_sk_new_null_ptr
#define sk_num OPENSSL_sk_num_ptr
#define sk_pop OPENSSL_sk_pop_ptr
#define sk_pop_free OPENSSL_sk_pop_free_ptr
#define sk_push OPENSSL_sk_push_ptr
#define sk_value OPENSSL_sk_value_ptr

#endif


#else // FEATURE_DISTRO_AGNOSTIC_SSL

#define API_EXISTS(fn) true

#if OPENSSL_VERSION_NUMBER >= OPENSSL_VERSION_3_0_RTM
#define NEED_OPENSSL_3_0 true
#elif OPENSSL_VERSION_NUMBER >= OPENSSL_VERSION_1_1_0_RTM
#define NEED_OPENSSL_1_1 true
#else
#define NEED_OPENSSL_1_0 true
#endif

#if OPENSSL_VERSION_NUMBER < OPENSSL_VERSION_3_0_RTM

// Undo renames for renamed-in-3.0
#define EVP_MD_get_size EVP_MD_size
#define EVP_PKEY_get_base_id EVP_PKEY_base_id
#define EVP_PKEY_get_bits EVP_PKEY_bits
#define SSL_get1_peer_certificate SSL_get_peer_certificate
#define EVP_CIPHER_get_nid EVP_CIPHER_nid
#endif

#if OPENSSL_VERSION_NUMBER >= OPENSSL_VERSION_3_0_RTM

#define ERR_put_error local_ERR_put_error

#elif OPENSSL_VERSION_NUMBER < OPENSSL_VERSION_1_1_0_RTM

// Alias "future" API to the local_ version.
#define ASN1_TIME_to_tm local_ASN1_TIME_to_tm
#define BN_abs_is_word local_BN_abs_is_word
#define BN_is_odd local_BN_is_odd
#define BN_is_one local_BN_is_one
#define BN_is_zero local_BN_is_zero
#define BIO_up_ref local_BIO_up_ref
#define DSA_get0_key local_DSA_get0_key
#define DSA_get0_pqg local_DSA_get0_pqg
#define DSA_get_method local_DSA_get_method
#define DSA_set0_key local_DSA_set0_key
#define DSA_set0_pqg local_DSA_set0_pqg
#define EVP_CIPHER_CTX_free local_EVP_CIPHER_CTX_free
#define EVP_CIPHER_CTX_new local_EVP_CIPHER_CTX_new
#define EVP_CIPHER_CTX_reset local_EVP_CIPHER_CTX_reset
#define EVP_PKEY_check local_EVP_PKEY_check
#define EVP_PKEY_get0_RSA local_EVP_PKEY_get0_RSA
#define EVP_PKEY_public_check local_EVP_PKEY_public_check
#define EVP_PKEY_up_ref local_EVP_PKEY_up_ref
#define HMAC_CTX_free local_HMAC_CTX_free
#define HMAC_CTX_new local_HMAC_CTX_new
#define OpenSSL_version_num local_OpenSSL_version_num
#define RSA_get_multi_prime_extra_count local_RSA_get_multi_prime_extra_count
#define RSA_get0_crt_params local_RSA_get0_crt_params
#define RSA_get0_factors local_RSA_get0_factors
#define RSA_get0_key local_RSA_get0_key
#define RSA_meth_get_flags local_RSA_meth_get_flags
#define RSA_set0_crt_params local_RSA_set0_crt_params
#define RSA_set0_factors local_RSA_set0_factors
#define RSA_set0_key local_RSA_set0_key
#define RSA_pkey_ctx_ctrl local_RSA_pkey_ctx_ctrl
#define RSA_test_flags local_RSA_test_flags
#define SSL_CTX_set_security_level local_SSL_CTX_set_security_level
#define SSL_is_init_finished local_SSL_is_init_finished
#define X509_CRL_get0_nextUpdate local_X509_CRL_get0_nextUpdate
#define X509_NAME_get0_der local_X509_NAME_get0_der
#define X509_PUBKEY_get0_param local_X509_PUBKEY_get0_param
#define X509_STORE_CTX_get0_cert local_X509_STORE_CTX_get0_cert
#define X509_STORE_CTX_get0_chain local_X509_STORE_CTX_get0_chain
#define X509_STORE_CTX_get0_untrusted local_X509_STORE_CTX_get0_untrusted
#define X509_STORE_get0_param local_X509_STORE_get0_param
#define X509_get0_notAfter local_X509_get0_notAfter
#define X509_get0_notBefore local_X509_get0_notBefore
#define X509_set1_notAfter local_X509_set1_notAfter
#define X509_set1_notBefore local_X509_set1_notBefore
#define X509_get0_pubkey_bitstr local_X509_get0_pubkey_bitstr
#define X509_get0_tbs_sigalg local_X509_get0_tbs_sigalg
#define X509_get_X509_PUBKEY local_X509_get_X509_PUBKEY
#define X509_get_version local_X509_get_version
#define X509_up_ref local_X509_up_ref
#define SSL_CTX_set_keylog_callback local_SSL_CTX_set_keylog_callback

#if OPENSSL_VERSION_NUMBER < OPENSSL_VERSION_1_0_2_RTM

#define X509_CHECK_FLAG_NO_PARTIAL_WILDCARDS 4

#define X509_check_host local_X509_check_host
#define X509_STORE_CTX_get0_store local_X509_STORE_CTX_get0_store

#endif

// Restore the old names for RENAMED_FUNCTION functions.
#define EVP_MD_CTX_free EVP_MD_CTX_destroy
#define EVP_MD_CTX_new EVP_MD_CTX_create
#define RSA_PKCS1_OpenSSL RSA_PKCS1_SSLeay
#define OPENSSL_sk_free sk_free
#define OPENSSL_sk_new_null sk_new_null
#define OPENSSL_sk_num sk_num
#define OPENSSL_sk_pop sk_pop
#define OPENSSL_sk_pop_free sk_pop_free
#define OPENSSL_sk_push sk_push
#define OPENSSL_sk_value sk_value
#define TLS_method SSLv23_method

#endif

#endif // FEATURE_DISTRO_AGNOSTIC_SSL
