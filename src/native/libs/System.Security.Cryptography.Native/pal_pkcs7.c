// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "pal_pkcs7.h"

PKCS7* CryptoNative_PemReadBioPkcs7(BIO* bp)
{
    ERR_clear_error();
    return PEM_read_bio_PKCS7(bp, NULL, NULL, NULL);
}

PKCS7* CryptoNative_DecodePkcs7(const uint8_t* buf, int32_t len)
{
    ERR_clear_error();

    if (!buf || !len)
    {
        return NULL;
    }

    return d2i_PKCS7(NULL, &buf, len);
}

PKCS7* CryptoNative_D2IPkcs7Bio(BIO* bp)
{
    ERR_clear_error();
    return d2i_PKCS7_bio(bp, NULL);
}

PKCS7* CryptoNative_Pkcs7CreateCertificateCollection(X509Stack* certs)
{
    ERR_clear_error();
    return PKCS7_sign(NULL, NULL, certs, NULL, PKCS7_PARTIAL);
}

void CryptoNative_Pkcs7Destroy(PKCS7* p7)
{
    if (p7 != NULL)
    {
        PKCS7_free(p7);
    }
}

int32_t CryptoNative_GetPkcs7Certificates(PKCS7* p7, X509Stack** certs)
{
    // No error queue impact.

    if (!p7 || !certs)
    {
        return 0;
    }

    switch (OBJ_obj2nid(p7->type))
    {
        case NID_pkcs7_signed:
            if (!p7->d.sign)
            {
                return 0;
            }

            *certs = p7->d.sign->cert;
            return 1;
        case NID_pkcs7_signedAndEnveloped:
            if (!p7->d.signed_and_enveloped)
            {
                return 0;
            }

            *certs = p7->d.signed_and_enveloped->cert;
            return 1;
    }

    return 0;
}

int32_t CryptoNative_GetPkcs7DerSize(PKCS7* p7)
{
    ERR_clear_error();
    return i2d_PKCS7(p7, NULL);
}

int32_t CryptoNative_EncodePkcs7(PKCS7* p7, uint8_t* buf)
{
    ERR_clear_error();
    return i2d_PKCS7(p7, &buf);
}
