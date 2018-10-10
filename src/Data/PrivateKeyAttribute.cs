using System;
using System.ComponentModel.DataAnnotations;
using LtiAdvantageLibrary.NetCore.Utilities;

namespace AdvantageTool.Data
{
    public class PrivateKeyAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            try
            {
                RsaHelper.PrivateKeyFromPemString(value.ToString());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
