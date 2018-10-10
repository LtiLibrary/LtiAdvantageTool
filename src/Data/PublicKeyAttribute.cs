using System;
using System.ComponentModel.DataAnnotations;
using LtiAdvantageLibrary.NetCore.Utilities;

namespace AdvantageTool.Data
{
    public class PublicKeyAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            try
            {
                RsaHelper.PublicKeyFromPemString(value.ToString());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
