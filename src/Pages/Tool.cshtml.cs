using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Pages
{
    // Tool launches typically come from outsite this app. Order not required starting with AspNetCore 2.2.
    // See https://github.com/aspnet/Mvc/issues/7795#issuecomment-397071059
    [IgnoreAntiforgeryToken(Order = 1001)]
    public class ToolModel : PageModel
    {
        // Step 5 in the OpenId Implicit Flow
        // See https://www.imsglobal.org/spec/security/v1p0/#platform-originating-messages
        // See https://openid.net/specs/openid-connect-core-1_0.html#ImplicitFlowSteps
        public IActionResult OnPost()
        {
            // The Platform MUST send the id_token via the OAuth 2 Form Post
            // See https://www.imsglobal.org/spec/security/v1p0/#successful-authentication
            // See http://openid.net/specs/oauth-v2-form-post-response-mode-1_0.html
            
            if (string.IsNullOrEmpty(IdToken))
                return new BadRequestResult();

            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(IdToken))
                return new BadRequestResult();
            
            var token = handler.ReadJwtToken(IdToken);

            // Authentication Response Validation
            // See https://www.imsglobal.org/spec/security/v1p0/#authentication-response-validation

            // The Issuer Identifier for the Platform MUST exactly match the value of the iss (Issuer)
            // Claim (therefore the Tool MUST previously have been made aware of this identifier). The
            // Issuer Identifier was collected in an offline process.
            // See https://www.imsglobal.org/spec/security/v1p0/#dfn-issuer-identifier
            
            var issuer = token.Issuer; // Normally this would actually look it up!
            if (issuer == null)
                return new UnauthorizedResult();

            // The ID Token MUST contain a nonce Claim.
            var nonce = token.Claims.SingleOrDefault(c => c.Type == "nonce")?.Value;
            if (string.IsNullOrEmpty(nonce))
                return new UnauthorizedResult();

            // Using the JwtSecurityTokenHandler.ValidateToken method, validate four things:
            //
            // 1. The Issuer Identifier for the Platform MUST exactly match the value of the iss
            //    (Issuer) Claim (therefore the Tool MUST previously have been made aware of this
            //    identifier.
            // 2. The Tool MUST Validate the signature of the ID Token according to JSON Web Signature
            //    RFC 7515, Section 5; using the Public Key for the Platform which collected offline.
            // 3. The Tool MUST validate that the aud (audience) Claim contains its client_id value
            //    registered as an audience with the Issuer identified by the iss (Issuer) Claim. The
            //    aud (audience) Claim MAY contain an array with more than one element. The Tool MUST
            //    reject the ID Token if it does not list the client_id as a valid audience, or if it
            //    contains additional audiences not trusted by the Tool.
            // 4. The current time MUST be before the time represented by the exp Claim;

            // Prepare the TokenValidationParameters using information
            // gathered during previous registration process

            var publicKey =
@"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAxwNk5GjdXmb4iFWOe/Lf
kWYfuzUhU+rHef4FziWJq31RZUkdKjaul0MyUwPZ/u2Gpzpdr1hNSa3Kmtj4BQk8
IUgveVAyvNxTMinsEm6hSjihQHnM5LLWGM804uZ8ylS0Rt4ne31hIQSOnxBp6LXj
Uvxdavl5Zp+tt5aF+5zxE0Viu7s4oqwEdr25kCdo/H4zBadLGCmx1IFFYqd8voEM
AILwP02jbuOSeSxK86b2uxLl4BZb9qL1Itd2+Febtt8PW4vVkcl7jWXQUBhQRn1L
GNRmKF4nXZVVAYu1grC4jXqIYX0rY9BuQAgR3W1B+aBWfPCxkOFyCH5re6lNA+OH
oQIDAQAB
-----END PUBLIC KEY-----";
            var audiences = new [] {Request.GetDisplayUrl()};

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,

                ValidateAudience = true,
                ValidAudiences = audiences,
                
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new RsaSecurityKey(RsaHelper.PublicKeyFromPemString(publicKey)),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5.0)
            };

            try
            {
                handler.ValidateToken(IdToken, validationParameters, out _);
            }
            catch (Exception)
            {
                return new UnauthorizedResult();
            }

            return Page();
        }

        [BindProperty(Name = "id_token")] public string IdToken { get; set; }

        //private RsaSecurityKey GetRsaSecurityKey(string key)
        //{
        //    var x509Key = DecodeOpenSslPublicKey(key);
        //    var rsaCryptoServiceProvider = DecodeX509PublicKey(x509Key);
        //    return new RsaSecurityKey(rsaCryptoServiceProvider);
        //}

        //private static byte[] DecodeOpenSslPublicKey(string instr)
        //{
        //    const string pempubheader = "-----BEGIN PUBLIC KEY-----";
        //    const string pempubfooter = "-----END PUBLIC KEY-----";
        //    string pemstr = instr.Trim();
        //    byte[] binkey;
        //    if (!pemstr.StartsWith(pempubheader) || !pemstr.EndsWith(pempubfooter))
        //        return null;
        //    var sb = new StringBuilder(pemstr);
        //    sb.Replace(pempubheader, ""); //remove headers/footers, if present
        //    sb.Replace(pempubfooter, "");

        //    string pubstr = sb.ToString().Trim(); //get string after removing leading/trailing whitespace

        //    try
        //    {
        //        binkey = Convert.FromBase64String(pubstr);
        //    }
        //    catch (FormatException)
        //    {
        //        //if can't b64 decode, data is not valid
        //        return null;
        //    }

        //    return binkey;
        //}

        //private static RSACryptoServiceProvider DecodeX509PublicKey(byte[] x509Key)
        //{
        //    // encoded OID sequence for  PKCS #1 rsaEncryption szOID_RSA_RSA = "1.2.840.113549.1.1.1"
        //    byte[] seqOid = {0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00};
        //    // ---------  Set up stream to read the asn.1 encoded SubjectPublicKeyInfo blob  ------
        //    using (var mem = new MemoryStream(x509Key))
        //    {
        //        using (var binr = new BinaryReader(mem)) //wrap Memory Stream with BinaryReader for easy reading
        //        {
        //            try
        //            {
        //                var twobytes = binr.ReadUInt16();
        //                switch (twobytes)
        //                {
        //                    case 0x8130:
        //                        binr.ReadByte(); //advance 1 byte
        //                        break;
        //                    case 0x8230:
        //                        binr.ReadInt16(); //advance 2 bytes
        //                        break;
        //                    default:
        //                        return null;
        //                }

        //                var seq = binr.ReadBytes(15);
        //                if (!CompareBytearrays(seq, seqOid)) //make sure Sequence for OID is correct
        //                    return null;

        //                twobytes = binr.ReadUInt16();
        //                if (twobytes == 0x8103
        //                ) //data read as little endian order (actual data order for Bit String is 03 81)
        //                    binr.ReadByte(); //advance 1 byte
        //                else if (twobytes == 0x8203)
        //                    binr.ReadInt16(); //advance 2 bytes
        //                else
        //                    return null;

        //                var bt = binr.ReadByte();
        //                if (bt != 0x00) //expect null byte next
        //                    return null;

        //                twobytes = binr.ReadUInt16();
        //                if (twobytes == 0x8130
        //                ) //data read as little endian order (actual data order for Sequence is 30 81)
        //                    binr.ReadByte(); //advance 1 byte
        //                else if (twobytes == 0x8230)
        //                    binr.ReadInt16(); //advance 2 bytes
        //                else
        //                    return null;

        //                twobytes = binr.ReadUInt16();
        //                byte lowbyte;
        //                byte highbyte = 0x00;

        //                if (twobytes == 0x8102
        //                ) //data read as little endian order (actual data order for Integer is 02 81)
        //                    lowbyte = binr.ReadByte(); // read next bytes which is bytes in modulus
        //                else if (twobytes == 0x8202)
        //                {
        //                    highbyte = binr.ReadByte(); //advance 2 bytes
        //                    lowbyte = binr.ReadByte();
        //                }
        //                else
        //                    return null;

        //                byte[] modint =
        //                    {lowbyte, highbyte, 0x00, 0x00}; //reverse byte order since asn.1 key uses big endian order
        //                int modsize = BitConverter.ToInt32(modint, 0);

        //                byte firstbyte = binr.ReadByte();
        //                binr.BaseStream.Seek(-1, SeekOrigin.Current);

        //                if (firstbyte == 0x00)
        //                {
        //                    //if first byte (highest order) of modulus is zero, don't include it
        //                    binr.ReadByte(); //skip this null byte
        //                    modsize -= 1; //reduce modulus buffer size by 1
        //                }

        //                byte[] modulus = binr.ReadBytes(modsize); //read the modulus bytes

        //                if (binr.ReadByte() != 0x02) //expect an Integer for the exponent data
        //                    return null;
        //                int expbytes =
        //                    binr.ReadByte(); // should only need one byte for actual exponent data (for all useful values)
        //                byte[] exponent = binr.ReadBytes(expbytes);

        //                // We don't really need to print anything but if we insist to...
        //                //showBytes("\nExponent", exponent);
        //                //showBytes("\nModulus", modulus);

        //                // ------- create RSACryptoServiceProvider instance and initialize with public key -----
        //                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        //                RSAParameters rsaKeyInfo = new RSAParameters
        //                {
        //                    Modulus = modulus,
        //                    Exponent = exponent
        //                };
        //                rsa.ImportParameters(rsaKeyInfo);
        //                return rsa;
        //            }
        //            catch (Exception)
        //            {
        //                return null;
        //            }
        //        }
        //    }
        //}

        //static bool CompareBytearrays( byte[] a, byte[] b )
        //{
        //    if (a.Length != b.Length)
        //        return false;
        //    int i = 0;
        //    foreach (byte c in a)
        //    {
        //        if (c != b[i])
        //            return false;
        //        i++;
        //    }
        //    return true;
        //}
    }
}