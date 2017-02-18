﻿using System;
using System.Text;

namespace ArcGIS.ServiceModel
{
    public static class CryptoProviderFactory
    {
        public static Func<ICryptoProvider> Get { get; set; }

        public static bool Disabled { get; set; }

        static CryptoProviderFactory()
        {
            Get = (() => { return Disabled ? null : new RsaEncrypter(); });
        }
    }

    public class RsaEncrypter : ICryptoProvider
    {
        public Operation.GenerateToken Encrypt(Operation.GenerateToken tokenRequest, byte[] exponent, byte[] modulus)
        {
            Guard.AgainstNullArgument("tokenRequest", tokenRequest);
            Guard.AgainstNullArgument("exponent", exponent);
            Guard.AgainstNullArgument("modulus", modulus);

            using (var rsa = new System.Security.Cryptography.RSACryptoServiceProvider(512))
            {
                var rsaParms = new System.Security.Cryptography.RSAParameters
                {
                    Exponent = exponent,
                    Modulus = modulus
                };
                rsa.ImportParameters(rsaParms);

                var encryptedUsername = rsa.Encrypt(Encoding.UTF8.GetBytes(tokenRequest.Username), false).BytesToHex();
                var encryptedPassword = rsa.Encrypt(Encoding.UTF8.GetBytes(tokenRequest.Password), false).BytesToHex();
                var encryptedClient = string.IsNullOrWhiteSpace(tokenRequest.Client) ? "" : rsa.Encrypt(Encoding.UTF8.GetBytes(tokenRequest.Client), false).BytesToHex();
                var encryptedExpiration = rsa.Encrypt(Encoding.UTF8.GetBytes(tokenRequest.ExpirationInMinutes.ToString()), false).BytesToHex();
                var encryptedReferer = string.IsNullOrWhiteSpace(tokenRequest.Referer) ? "" : rsa.Encrypt(Encoding.UTF8.GetBytes(tokenRequest.Referer), false).BytesToHex();

                tokenRequest.Encrypt(encryptedUsername, encryptedPassword, encryptedExpiration, encryptedClient, encryptedReferer);

                return tokenRequest;
            }
        }
    }
}
